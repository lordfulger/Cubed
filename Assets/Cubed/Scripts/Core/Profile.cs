using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    [Serializable]
    public class CardCount
    {
        public string id;
        public int count;
    }

    /// <summary>The local player's lifetime stats, achievements and cosmetics (persistentDataPath/profile.json).</summary>
    [Serializable]
    public class Profile
    {
        public int matchesPlayed, matchesWon, roundsWon, kills, deaths, blocks, cardsPicked, onlineMatches;
        public float damageDealt, playSeconds;
        public List<CardCount> cardCounts = new List<CardCount>();
        public List<string> achievements = new List<string>();
        public string hat = "none";
        public bool trainingDone;

        static Profile current;
        public static Profile Current => current ?? (current = Load());
        static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        /// <summary>Raised when an achievement unlocks (the Steam module mirrors these).</summary>
        public static event Action<Achievement> AchievementUnlocked;

        static Profile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var p = JsonUtility.FromJson<Profile>(File.ReadAllText(FilePath));
                    if (p != null)
                    {
                        if (p.cardCounts == null) p.cardCounts = new List<CardCount>();
                        if (p.achievements == null) p.achievements = new List<string>();
                        return p;
                    }
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            return new Profile();
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public bool Has(string achievementId) => achievements.Contains(achievementId);

        public void Unlock(string id)
        {
            if (Has(id)) return;
            var a = Achievements.All.FirstOrDefault(x => x.Id == id);
            if (a == null) return;
            achievements.Add(id);
            Save();
            Toasts.Show("Achievement unlocked: " + a.Title, a.Hat != null ? "New hat: " + Hats.Get(a.Hat).Name : a.Description, new Color(1f, 0.8f, 0.25f));
            try { AchievementUnlocked?.Invoke(a); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void CompleteTraining()
        {
            trainingDone = true;
            Unlock("graduate");
            Save();
        }

        public int DistinctCards => cardCounts.Count;

        public void AddCard(CardDefinition c)
        {
            cardsPicked++;
            var cc = cardCounts.FirstOrDefault(x => x.id == c.Id);
            if (cc == null) cardCounts.Add(new CardCount { id = c.Id, count = 1 });
            else cc.count++;
        }

        public IEnumerable<CardCount> FavoriteCards(int n) => cardCounts.OrderByDescending(c => c.count).Take(n);
    }

    public class Achievement
    {
        public string Id, Title, Description, Hat;
    }

    /// <summary>Achievement definitions and the tracker that awards them from game events.</summary>
    public static class Achievements
    {
        public static readonly List<Achievement> All = new List<Achievement>
        {
            new Achievement { Id = "first_blood", Title = "First Blood", Description = "Get a kill.", Hat = "headband" },
            new Achievement { Id = "winner", Title = "Champion", Description = "Win a match.", Hat = "crown" },
            new Achievement { Id = "veteran", Title = "Veteran", Description = "Win 25 matches.", Hat = "laurel" },
            new Achievement { Id = "card_shark", Title = "Card Shark", Description = "Pick 50 cards.", Hat = "tophat" },
            new Achievement { Id = "collector", Title = "Collector", Description = "Pick 100 different cards.", Hat = "monocle" },
            new Achievement { Id = "stacked", Title = "Stacked", Description = "Hold 12 cards in one match." },
            new Achievement { Id = "gambler", Title = "High Roller", Description = "Pick a Legendary card." },
            new Achievement { Id = "wall_sitter", Title = "Safety First", Description = "Block 25 border or fall hits.", Hat = "cone" },
            new Achievement { Id = "blocker", Title = "Bubble Boy", Description = "Successfully block 200 times.", Hat = "halo" },
            new Achievement { Id = "untouchable", Title = "Untouchable", Description = "Win a round without taking damage." },
            new Achievement { Id = "clutch", Title = "Clutch", Description = "Win a round with less than 10 HP." },
            new Achievement { Id = "sharpshooter", Title = "Sharpshooter", Description = "Deal 10,000 damage in total.", Hat = "visor" },
            new Achievement { Id = "flawless", Title = "Flawless", Description = "Win a match without losing a round." },
            new Achievement { Id = "brawler", Title = "Free-for-All Champion", Description = "Win a match with 6 or more teams." },
            new Achievement { Id = "bot_slayer", Title = "Machine Breaker", Description = "Win against a Brutal bot.", Hat = "horns" },
            new Achievement { Id = "architect", Title = "Architect", Description = "Save a map in the editor.", Hat = "antenna" },
            new Achievement { Id = "online", Title = "Connected", Description = "Finish an online match." },
            new Achievement { Id = "graduate", Title = "Graduate", Description = "Complete training.", Hat = "party" },
            new Achievement { Id = "modded", Title = "Tinkerer", Description = "Play a match with a mod card." },
            new Achievement { Id = "marathon", Title = "Marathon", Description = "Play for 2 hours.", Hat = "propeller" },
        };

        static bool hooked;
        static float roundDamageTaken;

        /// <summary>The local player whose stats feed the profile (first local human).</summary>
        public static Player Tracked(GameManager gm) =>
            gm.Players.FirstOrDefault(p => p.Input is HumanInput);

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            GameEvents.PlayerDied += (victim, info) =>
            {
                var gm = GameManager.Instance;
                if (gm == null || gm.IsTraining) return;
                var me = Tracked(gm);
                if (me == null) return;
                var killer = info != null && info.Source != null && info.Source != victim ? info.Source : victim.LastAttacker;
                if (killer == me && victim != me) { Profile.Current.kills++; Profile.Current.Unlock("first_blood"); }
                if (victim == me) Profile.Current.deaths++;
            };
            GameEvents.DamageBlocked += (p, info) =>
            {
                var gm = GameManager.Instance;
                if (gm == null || p != Tracked(gm)) return;
                var pr = Profile.Current;
                pr.blocks++;
                if (pr.blocks >= 200) pr.Unlock("blocker");
                if (info.IsEnvironmental && pr.blocks >= 0)
                {
                    environmentBlocks++;
                    if (environmentBlocks + PlayerPrefs.GetInt("cubed.envblocks", 0) >= 25) pr.Unlock("wall_sitter");
                }
            };
            GameEvents.CardPicked += (p, card) =>
            {
                var gm = GameManager.Instance;
                // Clients get cards re-applied by syncs; their picks are counted once in RecordMatch.
                if (gm == null || p != Tracked(gm) || gm.IsTraining || gm.NetClientMode) return;
                var pr = Profile.Current;
                pr.AddCard(card);
                if (pr.cardsPicked >= 50) pr.Unlock("card_shark");
                if (pr.DistinctCards >= 100) pr.Unlock("collector");
                if (card.Rarity == Rarity.Legendary) pr.Unlock("gambler");
                if (p.Cards.Count >= 12) pr.Unlock("stacked");
                if (card.Source != "Cubed" && card.Source != "Missing mod") pr.Unlock("modded");
            };
            GameEvents.PlayerDamaged += (p, info) =>
            {
                var gm = GameManager.Instance;
                if (gm != null && p == Tracked(gm)) roundDamageTaken += info.Amount;
            };
            GameEvents.RoundStarted += r => roundDamageTaken = 0f;
            GameEvents.RoundEnded += (round, winner) =>
            {
                var gm = GameManager.Instance;
                var me = gm != null ? Tracked(gm) : null;
                if (me == null || gm.IsTraining || winner != me.Team) return;
                Profile.Current.roundsWon++;
                if (roundDamageTaken <= 0.01f) Profile.Current.Unlock("untouchable");
                if (me.IsAlive && me.Health < 10f) Profile.Current.Unlock("clutch");
            };
            GameEvents.MatchEnded += winner => RecordMatch(winner, false);
        }

        static int environmentBlocks;
        static string lastRecorded;

        /// <summary>Record a finished match for the local player (also called on network clients).</summary>
        public static void RecordMatch(int winner, bool online)
        {
            var gm = GameManager.Instance;
            var me = gm != null ? Tracked(gm) : null;
            if (me == null || gm.IsTraining) return;
            // Guard against recording the same finished match twice (re-syncs on the end screen).
            string key = winner + ":" + gm.RoundNumber + ":" + string.Join(",", gm.TeamScores) + ":" + me.Kills + ":" + Mathf.RoundToInt(me.DamageDealt);
            if (key == lastRecorded) return;
            lastRecorded = key;
            var pr = Profile.Current;
            pr.matchesPlayed++;
            pr.damageDealt += me.DamageDealt;
            PlayerPrefs.SetInt("cubed.envblocks", PlayerPrefs.GetInt("cubed.envblocks", 0) + environmentBlocks);
            environmentBlocks = 0;
            if (online || gm.NetClientMode || (gm.Net != null && gm.Net.IsHosting)) { pr.onlineMatches++; pr.Unlock("online"); }
            if (gm.NetClientMode)
            {
                // Clients don't receive kill events; take the totals from the synced player.
                pr.kills += me.Kills;
                pr.deaths += me.Deaths;
                foreach (var c in me.Cards) pr.AddCard(c);
            }
            if (winner == me.Team)
            {
                pr.matchesWon++;
                pr.Unlock("winner");
                if (pr.matchesWon >= 25) pr.Unlock("veteran");
                int losses = gm.ActiveTeams.Where(t => t != winner).Sum(t => gm.TeamScores[t]);
                if (losses == 0) pr.Unlock("flawless");
                if (gm.ActiveTeams.Count >= 6) pr.Unlock("brawler");
                if (gm.Players.Any(p => p.Team != me.Team && p.Slot != null && p.Slot.control == ControlType.Bot && p.Slot.difficulty == BotDifficulty.Brutal))
                    pr.Unlock("bot_slayer");
            }
            if (pr.damageDealt >= 10000f) pr.Unlock("sharpshooter");
            pr.Save();
        }

        /// <summary>Accumulate play time; call every frame.</summary>
        public static void Tick(float unscaledDt)
        {
            var pr = Profile.Current;
            pr.playSeconds += unscaledDt;
            if (pr.playSeconds >= 7200f) pr.Unlock("marathon");
        }
    }

    public class Hat
    {
        public string Id, Name;
        /// <summary>Builds the hat under the given parent (top of the head at local y=0).</summary>
        public Action<Transform, Color> Build;
    }

    /// <summary>Cosmetic hats (unlocked by achievements; bots wear random ones).</summary>
    public static class Hats
    {
        static Material M(Color c) => Mats.Solid(c);

        public static readonly List<Hat> All = new List<Hat>
        {
            new Hat { Id = "none", Name = "No hat", Build = (t, c) => { } },
            new Hat { Id = "headband", Name = "Headband", Build = (t, c) => Prim.Cube("Band", t, new Vector3(0f, -0.12f, 0f), new Vector3(0.94f, 0.08f, 0.94f), M(new Color(0.9f, 0.15f, 0.15f))) },
            new Hat { Id = "crown", Name = "Crown", Build = (t, c) =>
                {
                    var gold = M(new Color(1f, 0.8f, 0.2f));
                    Prim.Cube("Ring", t, new Vector3(0f, 0.06f, 0f), new Vector3(0.6f, 0.12f, 0.6f), gold);
                    for (int i = 0; i < 4; i++)
                        Prim.Cube("Spike", t, new Vector3(i % 2 == 0 ? (i == 0 ? -0.24f : 0.24f) : 0f, 0.18f, i % 2 == 1 ? (i == 1 ? -0.24f : 0.24f) : 0f), new Vector3(0.1f, 0.14f, 0.1f), gold);
                } },
            new Hat { Id = "laurel", Name = "Laurel", Build = (t, c) =>
                {
                    var g = M(new Color(0.4f, 0.75f, 0.3f));
                    Prim.Cube("L", t, new Vector3(-0.38f, 0.02f, 0.05f), new Vector3(0.08f, 0.08f, 0.6f), g);
                    Prim.Cube("R", t, new Vector3(0.38f, 0.02f, 0.05f), new Vector3(0.08f, 0.08f, 0.6f), g);
                } },
            new Hat { Id = "tophat", Name = "Top hat", Build = (t, c) =>
                {
                    var k = M(new Color(0.08f, 0.08f, 0.1f));
                    Prim.Cube("Brim", t, new Vector3(0f, 0.03f, 0f), new Vector3(0.72f, 0.05f, 0.72f), k);
                    Prim.Cube("Crown", t, new Vector3(0f, 0.25f, 0f), new Vector3(0.46f, 0.42f, 0.46f), k);
                    Prim.Cube("Band", t, new Vector3(0f, 0.1f, 0f), new Vector3(0.47f, 0.07f, 0.47f), M(new Color(0.75f, 0.1f, 0.15f)));
                } },
            new Hat { Id = "monocle", Name = "Monocle", Build = (t, c) => Prim.Cube("Monocle", t, new Vector3(0.18f, -0.22f, 0.5f), new Vector3(0.2f, 0.2f, 0.02f), M(new Color(1f, 0.85f, 0.3f))) },
            new Hat { Id = "cone", Name = "Traffic cone", Build = (t, c) =>
                {
                    var o = M(new Color(1f, 0.45f, 0.1f));
                    Prim.Cube("Base", t, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.06f, 0.5f), o);
                    Prim.Cube("Mid", t, new Vector3(0f, 0.18f, 0f), new Vector3(0.3f, 0.24f, 0.3f), o);
                    Prim.Cube("Stripe", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.31f, 0.06f, 0.31f), M(Color.white));
                    Prim.Cube("Tip", t, new Vector3(0f, 0.36f, 0f), new Vector3(0.14f, 0.14f, 0.14f), o);
                } },
            new Hat { Id = "halo", Name = "Halo", Build = (t, c) =>
                {
                    var y = M(new Color(1f, 0.95f, 0.55f));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Prim.Cube("Seg", t, new Vector3(Mathf.Cos(a) * 0.32f, 0.35f, Mathf.Sin(a) * 0.32f), new Vector3(0.14f, 0.05f, 0.14f), y);
                    }
                } },
            new Hat { Id = "visor", Name = "Pro visor", Build = (t, c) => Prim.Cube("Visor", t, new Vector3(0f, -0.28f, 0.42f), new Vector3(0.86f, 0.12f, 0.12f), M(new Color(0.2f, 0.9f, 1f))) },
            new Hat { Id = "horns", Name = "Horns", Build = (t, c) =>
                {
                    var b = M(new Color(0.9f, 0.88f, 0.8f));
                    Prim.Cube("HL", t, new Vector3(-0.3f, 0.12f, 0.1f), new Vector3(0.1f, 0.26f, 0.1f), b);
                    Prim.Cube("HR", t, new Vector3(0.3f, 0.12f, 0.1f), new Vector3(0.1f, 0.26f, 0.1f), b);
                } },
            new Hat { Id = "antenna", Name = "Antenna", Build = (t, c) =>
                {
                    Prim.Cube("Rod", t, new Vector3(0f, 0.25f, 0f), new Vector3(0.04f, 0.5f, 0.04f), M(new Color(0.5f, 0.5f, 0.55f)));
                    Prim.Cube("Bulb", t, new Vector3(0f, 0.52f, 0f), new Vector3(0.12f, 0.12f, 0.12f), M(new Color(1f, 0.3f, 0.3f)));
                } },
            new Hat { Id = "party", Name = "Party hat", Build = (t, c) =>
                {
                    var p = M(new Color(0.95f, 0.4f, 0.9f));
                    Prim.Cube("A", t, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.14f, 0.36f), p);
                    Prim.Cube("B", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.24f, 0.12f, 0.24f), M(new Color(0.3f, 0.8f, 1f)));
                    Prim.Cube("C", t, new Vector3(0f, 0.31f, 0f), new Vector3(0.12f, 0.12f, 0.12f), p);
                } },
            new Hat { Id = "propeller", Name = "Propeller cap", Build = (t, c) =>
                {
                    Prim.Cube("Cap", t, new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), M(c));
                    Prim.Cube("Stem", t, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.12f, 0.05f), M(Color.gray));
                    var blades = Prim.Cube("Blades", t, new Vector3(0f, 0.22f, 0f), new Vector3(0.7f, 0.02f, 0.1f), M(new Color(1f, 0.85f, 0.2f)));
                    blades.AddComponent<Spinner>();
                } },
        };

        public static Hat Get(string id) => All.FirstOrDefault(h => h.Id == id) ?? All[0];

        public static bool Unlocked(string id)
        {
            if (id == "none") return true;
            var a = Achievements.All.FirstOrDefault(x => x.Hat == id);
            return a == null || Profile.Current.Has(a.Id);
        }

        /// <summary>Random hat for bots (deterministic per name so it stays the same between rounds).</summary>
        public static string ForBot(string name)
        {
            int h = 0;
            foreach (char ch in name ?? "") h = h * 31 + ch;
            var list = All.Where(x => x.Id != "none").ToList();
            return list[Mathf.Abs(h) % list.Count].Id;
        }
    }

    /// <summary>Spins a transform (propeller hats).</summary>
    public class Spinner : MonoBehaviour
    {
        public float Speed = 720f;
        void Update() => transform.Rotate(0f, Speed * Time.deltaTime, 0f, Space.Self);
    }
}
