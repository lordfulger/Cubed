using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Spheres.Automation
{
    /// <summary>
    /// Plays many fast bot-vs-bot matches and measures, for every card, how often it was offered and picked
    /// and the match win rate of the players who held it.
    ///
    /// Bots normally pick with BotBrain.ChooseCard. Here the runner answers every bot pick itself, right when
    /// the hand is dealt (before the bot's think delay runs out), using GameManager.Pick - so offers and picks
    /// are counted exactly and the policy can be random (default, best card coverage and an unbiased
    /// "average deck" win rate) or the bot's own scoring. Everyone picks after each round so holding a card
    /// does not correlate with having lost the previous round.
    ///
    /// Options (command line):
    ///   -balanceMatches 500        matches to play
    ///   -balanceMap builtin:duel_cell
    ///   -balanceTeamSize 1         1 = 1v1, 2 = 2v2
    ///   -balancePicks random|bot   pick policy
    ///   -balanceDifficulty Hard    bot difficulty (Easy, Normal, Hard, Brutal)
    ///   -balanceRounds 3           rounds to win a match
    ///   -balanceStartingPicks 2    cards everyone picks before round 1
    ///   -balanceMinSamples 20      matches a card must be held in before it can be flagged
    ///   -balanceSeed 12345         seed for the random pick policy
    ///   -balanceOut Builds/balance-report   output prefix (.csv and .md are appended)
    ///   -balanceFailOnError        exit with code 1 if the game logged errors during the run
    ///   -simFps 50 -simTimeScale 2 -automationTimeout 600 (minutes)
    /// </summary>
    public class BalanceSimRunner : AutomationRunner
    {
        class CardStats
        {
            public string Id, Title, Source;
            public Rarity Rarity;
            public int Offered, Picked, MatchesHeld, Wins, CopiesHeld;
        }

        readonly Dictionary<string, CardStats> cards = new Dictionary<string, CardStats>();
        readonly int[] teamWins = new int[Teams.Max];
        int matchesPlayed, draws, aborted;
        int totalPicks;
        System.Random rng;
        bool randomPolicy;
        SettingsBackup backup;

        int matchesWanted, teamSize, rounds, startingPicks, minSamples;
        string mapId;
        BotDifficulty difficulty;

        protected override string Name => "Balance simulator";

        protected override void Start()
        {
            WallLimit = 600f * 60f;
            base.Start();
        }

        CardStats StatsFor(CardDefinition c)
        {
            if (!cards.TryGetValue(c.Id, out var s))
            {
                s = new CardStats { Id = c.Id, Title = c.Title, Source = c.Source, Rarity = c.Rarity };
                cards[c.Id] = s;
            }
            return s;
        }

        protected override IEnumerator Run()
        {
            Log.Context = "boot";
            float t0 = Time.realtimeSinceStartup;
            while (Gm == null)
            {
                if (Time.realtimeSinceStartup - t0 > 60f)
                {
                    Log.AddFailure("boot", "GameManager.Instance never appeared");
                    Complete("FAIL", 2);
                    yield break;
                }
                yield return null;
            }
            yield return Frames(5);

            matchesWanted = Mathf.Max(1, Args.GetInt("-balanceMatches", 500));
            teamSize = Mathf.Clamp(Args.GetInt("-balanceTeamSize", 1), 1, 4);
            rounds = Mathf.Clamp(Args.GetInt("-balanceRounds", 3), 1, 20);
            startingPicks = Mathf.Clamp(Args.GetInt("-balanceStartingPicks", 2), 0, 10);
            minSamples = Mathf.Max(1, Args.GetInt("-balanceMinSamples", 20));
            randomPolicy = !string.Equals(Args.Get("-balancePicks", "random"), "bot", StringComparison.OrdinalIgnoreCase);
            rng = new System.Random(Args.GetInt("-balanceSeed", Environment.TickCount));
            if (!Enum.TryParse(Args.Get("-balanceDifficulty", "Hard"), true, out difficulty) || difficulty == BotDifficulty.Dummy)
                difficulty = BotDifficulty.Hard;
            mapId = Args.Get("-balanceMap", "builtin:duel_cell");
            if (MapRegistry.Get(mapId) == null)
            {
                Log.AddFailure("setup", "Map '" + mapId + "' is not registered; using " + MapRegistry.GetOrDefault(mapId)?.Id);
                mapId = MapRegistry.GetOrDefault(mapId)?.Id;
            }
            foreach (var c in CardRegistry.All) StatsFor(c);

            backup = new SettingsBackup(Gm.Settings);
            var s = Gm.Settings;
            ConfigureBots(s, teamSize * 2, 2, new[] { difficulty }, "Sim Bot");
            s.mode = GameMode.Elimination;
            s.mapId = mapId;
            s.roundsToWin = rounds;
            s.startingPicks = startingPicks;
            s.picksPerRound = 1;
            s.pickRule = PickRule.Everyone;
            s.pickTimer = 5f;
            s.rerollBetweenPicks = true;
            s.roundTimeLimit = 120f;
            s.borderShrinkAfter = 30f;
            s.borderShrinkDuration = 30f;

            Say("Playing " + matchesWanted + " matches on " + mapId + " (" + teamSize + "v" + teamSize + ", " + difficulty +
                " bots, " + (randomPolicy ? "random" : "bot") + " picks)");
            for (int m = 0; m < matchesWanted; m++)
            {
                yield return PlayOne(m);
                if (m % 10 == 9 || m == matchesWanted - 1)
                    Say("Match " + (m + 1) + "/" + matchesWanted + " done (" + Mathf.RoundToInt(WallTime) + " s, " + Log.ErrorCount + " errors)");
            }
            Gm.ReturnToMenu();
            yield return Frames(3);
            backup.Restore(Gm.Settings);
            backup = null;
            Complete(null, Args.Has("-balanceFailOnError") && Log.ErrorCount > 0 ? 1 : matchesPlayed > 0 ? 0 : 1);
        }

        IEnumerator PlayOne(int index)
        {
            Log.Context = "match " + (index + 1);
            Gm.StartMatch();
            yield return null;
            if (!Gm.InMatch)
            {
                aborted++;
                Log.AddFailure(Log.Context, "StartMatch() did not start a match (state " + Gm.State + ")");
                yield break;
            }
            float sim = 0f, wallStart = Time.realtimeSinceStartup;
            while (Gm.State != GameState.MatchEnd)
            {
                if (Gm.State == GameState.CardPick) AnswerPicks();
                yield return null;
                sim += Time.deltaTime;
                if (sim > 1800f || Time.realtimeSinceStartup - wallStart > 600f)
                {
                    aborted++;
                    Log.AddFailure(Log.Context, "Match did not finish (" + sim.ToString("0") + " s simulated, state " + Gm.State + ")");
                    Gm.ReturnToMenu();
                    yield break;
                }
            }
            RecordResult(Gm.MatchWinner);
            Gm.ReturnToMenu();
            yield return null;
        }

        void AnswerPicks()
        {
            foreach (var st in Gm.Picks.ToList())
            {
                for (int guard = 0; guard < 20 && !st.Done && st.Player != null && st.Player.IsBot && st.Hand.Count > 0 && Gm.State == GameState.CardPick; guard++)
                {
                    foreach (var c in st.Hand) StatsFor(c).Offered++;
                    int idx = randomPolicy ? rng.Next(st.Hand.Count) : BotBrain.ChooseCard(st.Player, st.Hand);
                    if (idx < 0 || idx >= st.Hand.Count) idx = 0;
                    StatsFor(st.Hand[idx]).Picked++;
                    totalPicks++;
                    int before = st.PicksRemaining;
                    Gm.Pick(st, idx);
                    if (st.PicksRemaining == before) break; // pick refused (paused?): try again next frame
                }
            }
        }

        void RecordResult(int winner)
        {
            matchesPlayed++;
            if (winner < 0 || winner >= Teams.Max) { draws++; return; }
            teamWins[winner]++;
            foreach (var p in Gm.Players)
            {
                if (p == null) continue;
                bool won = p.Team == winner;
                foreach (var g in p.Cards.Where(c => c != null).GroupBy(c => c.Id))
                {
                    var st = StatsFor(g.First());
                    st.MatchesHeld++;
                    st.CopiesHeld += g.Count();
                    if (won) st.Wins++;
                }
            }
        }

        // ------------------------------------------------------------------ report

        static string F(double v, string fmt = "0.000") => v.ToString(fmt, CultureInfo.InvariantCulture);

        /// <summary>Wilson score interval (95%) for a win rate.</summary>
        static void Wilson(int wins, int n, out double lo, out double hi)
        {
            if (n <= 0) { lo = 0; hi = 1; return; }
            const double z = 1.96;
            double p = (double)wins / n, z2 = z * z;
            double denom = 1 + z2 / n;
            double centre = p + z2 / (2 * n);
            double margin = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n));
            lo = (centre - margin) / denom;
            hi = (centre + margin) / denom;
        }

        string Flag(CardStats c)
        {
            if (c.MatchesHeld < minSamples) return c.MatchesHeld == 0 ? "no-data" : "low-samples";
            double wr = (double)c.Wins / c.MatchesHeld;
            if (wr > 0.6) return "STRONG";
            if (wr < 0.4) return "WEAK";
            return "";
        }

        static string Csv(string s)
        {
            s = s ?? "";
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        static string Md(string s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ");

        protected override void WriteReport(string status)
        {
            if (backup != null && Gm != null)
            {
                try { backup.Restore(Gm.Settings); } catch (Exception e) { Debug.LogException(e); }
                backup = null;
            }
            string prefix = Args.Get("-balanceOut", Path.Combine("Builds", "balance-report"));
            string csvPath = ReportPath(prefix + ".csv");
            string mdPath = ReportPath(prefix + ".md");
            int decided = matchesPlayed - draws;
            var list = cards.Values.OrderByDescending(c => c.MatchesHeld > 0 ? (double)c.Wins / c.MatchesHeld : -1).ThenBy(c => c.Id).ToList();

            var csv = new StringBuilder();
            csv.AppendLine("card_id,title,rarity,source,offered,picked,pick_rate,matches_held,copies_held,wins,win_rate,win_rate_ci_low,win_rate_ci_high,flag");
            foreach (var c in list)
            {
                Wilson(c.Wins, c.MatchesHeld, out double lo, out double hi);
                csv.AppendLine(string.Join(",", Csv(c.Id), Csv(c.Title), c.Rarity.ToString(), Csv(c.Source), c.Offered.ToString(), c.Picked.ToString(),
                    c.Offered > 0 ? F((double)c.Picked / c.Offered) : "", c.MatchesHeld.ToString(), c.CopiesHeld.ToString(), c.Wins.ToString(),
                    c.MatchesHeld > 0 ? F((double)c.Wins / c.MatchesHeld) : "", c.MatchesHeld > 0 ? F(lo) : "", c.MatchesHeld > 0 ? F(hi) : "", Flag(c)));
            }
            File.WriteAllText(csvPath, csv.ToString());

            var md = new StringBuilder();
            md.AppendLine("# Spheres card balance report");
            md.AppendLine();
            md.AppendLine("- Result: **" + status + "**, " + matchesPlayed + " matches played (" + decided + " decided, " + draws + " draws, " + aborted + " aborted) in " + Mathf.RoundToInt(WallTime) + " s");
            md.AppendLine("- Setup: map `" + mapId + "`, " + teamSize + "v" + teamSize + ", " + difficulty + " bots, first to " + rounds + " rounds, " +
                          startingPicks + " starting picks + 1 pick per round (everyone picks), " + (randomPolicy ? "random" : "BotBrain.ChooseCard") + " pick policy");
            md.AppendLine("- Game: Spheres " + SpheresAPI.GameVersion + ", Unity " + Application.unityVersion + ", " + cards.Count + " cards, " + totalPicks + " picks");
            md.AppendLine("- Side balance: team 0 won " + teamWins[0] + ", team 1 won " + teamWins[1] + " matches");
            md.AppendLine("- Errors logged during the run: " + Log.ErrorCount + (Log.ErrorCount > 0 ? " (run the smoke test for details)" : ""));
            md.AppendLine("- A card is flagged when it was held in at least " + minSamples + " decided matches and its holders won more than 60% (STRONG) or less than 40% (WEAK). " +
                          "Win rate = matches won / matches in which the player held at least one copy. The 95% interval shows how much of that is noise.");
            md.AppendLine();

            var flagged = list.Where(c => Flag(c) == "STRONG" || Flag(c) == "WEAK").ToList();
            md.AppendLine("## Flagged cards (" + flagged.Count + ")");
            md.AppendLine();
            if (flagged.Count == 0) md.AppendLine("None" + (list.All(c => c.MatchesHeld < minSamples) ? " - no card reached the sample threshold; run more matches." : "."));
            else AppendTable(md, flagged);
            md.AppendLine();

            md.AppendLine("## By rarity");
            md.AppendLine();
            md.AppendLine("| Rarity | Cards | Offered | Picked | Held | Win rate |");
            md.AppendLine("|---|---:|---:|---:|---:|---:|");
            foreach (var g in list.GroupBy(c => c.Rarity).OrderBy(g => g.Key))
            {
                int held = g.Sum(c => c.MatchesHeld), wins = g.Sum(c => c.Wins);
                md.AppendLine("| " + g.Key + " | " + g.Count() + " | " + g.Sum(c => c.Offered) + " | " + g.Sum(c => c.Picked) + " | " + held + " | " +
                              (held > 0 ? F(100.0 * wins / held, "0.0") + "%" : "-") + " |");
            }
            md.AppendLine();

            var sampled = list.Where(c => c.MatchesHeld >= minSamples).ToList();
            md.AppendLine("## Top 15 / bottom 15 cards with enough samples (" + sampled.Count + " of " + list.Count + " cards)");
            md.AppendLine();
            if (sampled.Count == 0) md.AppendLine("No card reached the sample threshold.");
            else
            {
                AppendTable(md, sampled.Take(15).ToList());
                if (sampled.Count > 15)
                {
                    md.AppendLine();
                    AppendTable(md, sampled.Skip(Math.Max(15, sampled.Count - 15)).ToList());
                }
            }
            md.AppendLine();
            int never = list.Count(c => c.Offered == 0);
            md.AppendLine("Cards never offered: " + never + ". The full per-card table is in `" + Path.GetFileName(csvPath) + "`.");
            File.WriteAllText(mdPath, md.ToString());

            Debug.Log("[SpheresAutomation] Balance report written to " + csvPath + " and " + mdPath + "\n" + md);
            try { Console.WriteLine(md.ToString()); } catch (Exception) { }
        }

        void AppendTable(StringBuilder md, List<CardStats> rows)
        {
            md.AppendLine("| Card | Rarity | Offered | Picked | Held | Win rate | 95% CI | Flag |");
            md.AppendLine("|---|---|---:|---:|---:|---:|---|---|");
            foreach (var c in rows)
            {
                Wilson(c.Wins, c.MatchesHeld, out double lo, out double hi);
                md.AppendLine("| " + Md(c.Title) + " (`" + Md(c.Id) + "`) | " + c.Rarity + " | " + c.Offered + " | " + c.Picked + " | " + c.MatchesHeld + " | " +
                              (c.MatchesHeld > 0 ? F(100.0 * c.Wins / c.MatchesHeld, "0.0") + "%" : "-") + " | " +
                              (c.MatchesHeld > 0 ? F(100 * lo, "0") + "-" + F(100 * hi, "0") + "%" : "-") + " | " + Flag(c) + " |");
            }
        }
    }
}
