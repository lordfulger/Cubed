using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Party extras: a seeded Daily Challenge (the same mode, map and starting cards for everyone each day) and a
    /// single-elimination Tournament of 1v1 duels between everyone in Match Setup.
    /// </summary>
    public partial class GameManager
    {
        MatchSettings partyBackup;

        public bool InDaily { get; private set; }
        public Tournament Bracket { get; private set; }

        void EndParty()
        {
            if (partyBackup == null) return;
            partyBackup.CopyPreferencesFrom(Settings);
            Settings = partyBackup;
            partyBackup = null;
            InDaily = false;
            Bracket = null;
        }

        // ---------------------------------------------------------------- daily challenge

        public void StartDaily()
        {
            if (Net != null && (Net.IsHosting || Net.IsClient)) return;
            EndParty();
            var d = DailyChallenge.Today();
            var t = Settings.Clone();
            t.slots.Clear();
            var me = Settings.slots.FirstOrDefault(s => s.IsLocalHuman);
            t.slots.Add(new PlayerSlot { name = me != null ? me.name : "You", control = me != null ? me.control : ControlType.KeyboardMouse, team = 0 });
            for (int i = 0; i < d.Bots; i++)
                t.slots.Add(new PlayerSlot { name = DailyChallenge.BotNames[(d.Seed + i) % DailyChallenge.BotNames.Length], control = ControlType.Bot, team = d.FreeForAll ? i + 1 : 1, difficulty = BotDifficulty.Hard });
            t.mode = d.Mode;
            t.mapId = d.MapId;
            t.mapRotation = MapRotation.Fixed;
            t.roundsToWin = 3;
            t.pointsToWinRound = 2;
            t.endless = false;
            t.startingPicks = 0;
            t.picksPerRound = 1;
            t.weather = d.Weather;
            t.timeOfDay = d.Time;
            partyBackup = Settings;
            Settings = t;
            InDaily = true;
            StartMatch();
            // Everyone starts with today's cards.
            foreach (var p in Players)
                foreach (var id in d.StartingCards)
                {
                    var c = CardRegistry.Get(id);
                    if (c != null) p.ApplyCard(c);
                }
            SpheresHud.ShowBanner("DAILY CHALLENGE  " + d.Date, new Color(1f, 0.85f, 0.3f));
        }

        void RecordDaily()
        {
            if (!InDaily) return;
            var me = Players.FirstOrDefault(p => p.Input is HumanInput);
            if (me == null) return;
            DailyChallenge.Record(TeamScores[me.HomeTeam], me.Kills, MatchWinner == me.HomeTeam);
        }

        // ---------------------------------------------------------------- tournament

        /// <summary>Start a tournament with everyone currently in Match Setup.</summary>
        public string StartTournament()
        {
            if (Net != null && Net.IsClient) return "Only the host can start a tournament.";
            var entrants = Settings.ActiveSlots.Select(s => s.Clone()).ToList();
            if (entrants.Count < 3) return "A tournament needs at least 3 players (humans or bots) in Match Setup.";
            EndParty();
            partyBackup = Settings;
            Settings = partyBackup.Clone();
            Bracket = new Tournament(entrants);
            NextTournamentMatch();
            return null;
        }

        /// <summary>Play the next duel, or finish the tournament.</summary>
        public void NextTournamentMatch()
        {
            var b = Bracket;
            if (b == null) return;
            var pair = b.NextPair();
            if (pair == null)
            {
                SpheresHud.ShowBanner(b.Champion != null ? b.Champion.name + " WINS THE TOURNAMENT!" : "Tournament over", new Color(1f, 0.85f, 0.3f));
                return;
            }
            var t = Settings;
            t.slots.Clear();
            var a = pair.Value.a.Clone(); a.team = 0;
            var c = pair.Value.b.Clone(); c.team = 1;
            t.slots.Add(a);
            t.slots.Add(c);
            if (t.mode == GameMode.Boss || t.mode == GameMode.Practice || t.mode == GameMode.Infection) t.mode = GameMode.Elimination;
            StartMatch();
            SpheresHud.ShowBanner(b.RoundName + ":  " + a.name + "  vs  " + c.name, new Color(1f, 0.85f, 0.3f));
        }

        void RecordTournament()
        {
            if (Bracket == null || MatchWinner < 0) return;
            var winner = Players.FirstOrDefault(p => p.HomeTeam == MatchWinner);
            if (winner != null) Bracket.Report(winner.DisplayName);
        }

        void OnPartyMatchEnded(int winner)
        {
            RecordDaily();
            RecordTournament();
        }
    }

    /// <summary>Today's seeded challenge (UTC date), and your best result for it.</summary>
    public static class DailyChallenge
    {
        public class Day
        {
            public string Date;
            public int Seed;
            public GameMode Mode;
            public string MapId, MapName;
            public int Bots;
            public bool FreeForAll;
            public List<string> StartingCards = new List<string>();
            public WeatherKind Weather;
            public TimeOfDay Time;
        }

        public static readonly string[] BotNames = { "Dot", "Bubbles", "Marbles", "Pip", "Orbit", "Gumdrop", "Boing", "Roly", "Puck", "Nugget" };

        static Day cached;

        public static Day Today()
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (cached != null && cached.Date == date) return cached;
            int seed = 0;
            foreach (char c in date) seed = seed * 31 + c;
            seed &= 0x7fffffff;
            var rng = new System.Random(seed);
            var modes = new[] { GameMode.Elimination, GameMode.Elimination, GameMode.CardRush, GameMode.Deathmatch, GameMode.KingOfTheHill, GameMode.SphereBall, GameMode.HoldTheOrb };
            var maps = MapRegistry.All.Where(m => m.Source == "Spheres").OrderBy(m => m.Id).ToList();
            var map = maps.Count > 0 ? maps[rng.Next(maps.Count)] : MapRegistry.GetOrDefault("builtin:garden");
            var d = new Day { Date = date, Seed = seed, Mode = modes[rng.Next(modes.Length)], MapId = map.Id, MapName = map.Data.name };
            d.Bots = 1 + rng.Next(3);
            d.FreeForAll = rng.Next(3) == 0;
            if (d.Mode == GameMode.SphereBall || d.Mode == GameMode.HoldTheOrb) d.FreeForAll = false;
            var pool = CardRegistry.All.Where(c => c.CanAppear == null && c.Source == "Spheres" && !c.IsCurse).OrderBy(c => c.Id).ToList();
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                var c = pool[rng.Next(pool.Count)];
                d.StartingCards.Add(c.Id);
                pool.Remove(c);
            }
            d.Weather = (WeatherKind)rng.Next(6);
            d.Time = (TimeOfDay)rng.Next(5);
            cached = d;
            return d;
        }

        static string Key(string date) => "spheres.daily." + date;

        public static string BestText()
        {
            string v = PlayerPrefs.GetString(Key(Today().Date), "");
            return string.IsNullOrEmpty(v) ? "Not played yet today" : "Today's best: " + v;
        }

        public static void Record(int roundsWon, int kills, bool won)
        {
            var key = Key(Today().Date);
            int score = (won ? 1000 : 0) + roundsWon * 100 + kills * 10;
            int best = PlayerPrefs.GetInt(key + ".score", -1);
            if (score <= best) return;
            PlayerPrefs.SetInt(key + ".score", score);
            PlayerPrefs.SetString(key, (won ? "WON" : "lost") + ", " + roundsWon + " rounds, " + kills + " kills (score " + score + ")");
            PlayerPrefs.Save();
            Toasts.Show("New daily best!", "Score " + score + ". Same challenge for everyone today - compare with friends.", new Color(1f, 0.85f, 0.3f));
        }
    }

    /// <summary>A single-elimination bracket of 1v1 duels (byes for odd counts).</summary>
    public class Tournament
    {
        public readonly List<List<string>> Rounds = new List<List<string>>();
        readonly Dictionary<string, PlayerSlot> slots = new Dictionary<string, PlayerSlot>();
        int round, match;
        public PlayerSlot Champion { get; private set; }
        public bool Finished => Champion != null;

        public Tournament(List<PlayerSlot> entrants)
        {
            var names = new List<string>();
            foreach (var s in entrants.OrderBy(_ => UnityEngine.Random.value))
            {
                string n = s.name;
                int k = 2;
                while (slots.ContainsKey(n)) n = s.name + " " + k++;
                s.name = n;
                slots[n] = s;
                names.Add(n);
            }
            Rounds.Add(names);
        }

        public string RoundName
        {
            get
            {
                int left = Rounds[round].Count;
                return left <= 2 ? "FINAL" : left <= 4 ? "SEMI-FINAL" : "ROUND " + (round + 1);
            }
        }

        /// <summary>The next duel, advancing byes automatically; null when the tournament is over.</summary>
        public (PlayerSlot a, PlayerSlot b)? NextPair()
        {
            while (true)
            {
                if (Finished) return null;
                var r = Rounds[round];
                if (Rounds.Count <= round + 1) Rounds.Add(new List<string>());
                if (match * 2 + 1 < r.Count) return (slots[r[match * 2]], slots[r[match * 2 + 1]]);
                if (match * 2 < r.Count) { Rounds[round + 1].Add(r[match * 2]); match++; continue; } // bye
                // Round complete.
                if (Rounds[round + 1].Count == 1) { Champion = slots[Rounds[round + 1][0]]; return null; }
                round++;
                match = 0;
            }
        }

        public void Report(string winnerName)
        {
            if (Finished) return;
            var r = Rounds[round];
            if (match * 2 + 1 >= r.Count) return;
            string a = r[match * 2], b = r[match * 2 + 1];
            if (winnerName != a && winnerName != b) winnerName = a;
            if (Rounds.Count <= round + 1) Rounds.Add(new List<string>());
            Rounds[round + 1].Add(winnerName);
            match++;
        }

        /// <summary>Bracket drawing (used on the results panel).</summary>
        public void Draw(Rect area)
        {
            int cols = Rounds.Count;
            float cw = area.width / Mathf.Max(1, cols);
            for (int c = 0; c < cols; c++)
            {
                var list = Rounds[c];
                float rowH = Mathf.Min(26f, area.height / Mathf.Max(1, list.Count));
                for (int i = 0; i < list.Count; i++)
                {
                    var r = new Rect(area.x + c * cw + 4f, area.y + i * rowH, cw - 8f, rowH - 3f);
                    bool champ = Champion != null && list[i] == Champion.name && c == cols - 1;
                    UIKit.Fill(r, champ ? new Color(1f, 0.8f, 0.25f, 0.6f) : new Color(1f, 1f, 1f, 0.07f));
                    UIKit.Text(new Rect(r.x + 4f, r.y, r.width - 8f, r.height), list[i], UIKit.Small, Color.white);
                }
            }
        }
    }
}
