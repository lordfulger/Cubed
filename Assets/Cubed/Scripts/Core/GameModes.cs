using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>Round rules for each GameMode, respawns, the King of the Hill zone, and Training.</summary>
    public partial class GameManager
    {
        public const float RespawnDelay = 2.5f;
        public const float HillRadius = 3.5f;

        /// <summary>Deathmatch kills per team this round.</summary>
        public readonly int[] RoundKills = new int[Teams.Max];
        /// <summary>King of the Hill control seconds per team this round.</summary>
        public readonly float[] HillProgress = new float[Teams.Max];
        /// <summary>Team currently holding the hill (-1 none, -2 contested).</summary>
        public int HillHolder { get; private set; } = -1;
        public Vector3 HillCenter { get; private set; }
        public Tutorial Tutorial { get; private set; }

        readonly Dictionary<Player, float> respawnAt = new Dictionary<Player, float>();
        GameObject hillVisual;
        Material hillMaterial;
        MatchSettings trainingBackup;
        bool practicePickReturn, modeEventsHooked;

        public GameMode Mode => Settings.mode;
        public bool IsTraining => trainingBackup != null;
        bool RespawnMode => Mode == GameMode.Deathmatch || Mode == GameMode.KingOfTheHill || Mode == GameMode.Practice;

        public static string ModeName(GameMode m)
        {
            switch (m)
            {
                case GameMode.Deathmatch: return "Deathmatch";
                case GameMode.KingOfTheHill: return "King of the Hill";
                case GameMode.CardRush: return "Card Rush";
                case GameMode.Practice: return "Training";
                default: return "Elimination";
            }
        }

        public static string ModeDescription(GameMode m)
        {
            switch (m)
            {
                case GameMode.Deathmatch: return "Respawns on. First team to the kill target wins the round.";
                case GameMode.KingOfTheHill: return "Hold the glowing zone. First team to fill the timer wins the round.";
                case GameMode.CardRush: return "Elimination, but every kill instantly draws you a card.";
                case GameMode.Practice: return "Free practice with target dummies.";
                default: return "Last team standing wins the round.";
            }
        }

        void HookModeEvents()
        {
            if (modeEventsHooked) return;
            modeEventsHooked = true;
            GameEvents.PlayerDied += OnPlayerDiedForMode;
        }

        void ModeRoundStart()
        {
            HookModeEvents();
            System.Array.Clear(RoundKills, 0, RoundKills.Length);
            System.Array.Clear(HillProgress, 0, HillProgress.Length);
            respawnAt.Clear();
            HillHolder = -1;
            BuildHill();
        }

        /// <summary>Also used by network clients after loading a map so they see the same hill.</summary>
        public void BuildHill()
        {
            if (hillVisual != null) Destroy(hillVisual);
            hillVisual = null;
            if (Settings.mode != GameMode.KingOfTheHill || Map == null) return;
            var b = Map.FullBounds;
            Physics.SyncTransforms();
            HillCenter = Physics.Raycast(new Vector3(b.center.x, b.max.y - 0.5f, b.center.z), Vector3.down, out var hit, b.size.y, Bullet.CollisionMask, QueryTriggerInteraction.Ignore)
                ? hit.point : b.center;
            hillMaterial = Mats.GlowAvailable ? Mats.NewGlow(new Color(1f, 1f, 1f, 0.25f), 2f, 0.8f) : Mats.NewTransparent(new Color(1f, 1f, 1f, 0.25f));
            hillVisual = Prim.Sphere("Hill", Map.transform, HillCenter, 1f, hillMaterial);
            hillVisual.transform.localScale = new Vector3(HillRadius * 2f, 0.4f, HillRadius * 2f);
            var beam = Prim.Cube("HillBeam", hillVisual.transform, new Vector3(0f, 12f, 0f), new Vector3(0.08f, 60f, 0.08f), hillMaterial);
            beam.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Returns true when the mode handled the round-over check.</summary>
        bool ModeCheckRoundOver()
        {
            switch (Mode)
            {
                case GameMode.Practice:
                    return true; // training never ends on its own
                case GameMode.Deathmatch:
                    for (int t = 0; t < Teams.Max; t++)
                        if (RoundKills[t] >= Settings.killsToWinRound) { EndRound(t); return true; }
                    return CheckTimeLimitByScore(RoundKills.Select(k => (float)k).ToArray());
                case GameMode.KingOfTheHill:
                    for (int t = 0; t < Teams.Max; t++)
                        if (HillProgress[t] >= Settings.hillSeconds) { EndRound(t); return true; }
                    return CheckTimeLimitByScore(HillProgress);
                default:
                    return false;
            }
        }

        bool CheckTimeLimitByScore(float[] score)
        {
            if (Settings.roundTimeLimit <= 0f || RoundTime < Settings.roundTimeLimit) return true;
            int best = -1;
            float bestScore = -1f;
            bool tie = false;
            foreach (int t in ActiveTeams)
            {
                if (score[t] > bestScore) { bestScore = score[t]; best = t; tie = false; }
                else if (Mathf.Approximately(score[t], bestScore)) tie = true;
            }
            EndRound(tie ? -1 : best);
            return true;
        }

        void ModeUpdate(float dt)
        {
            if (RespawnMode)
            {
                foreach (var p in Players)
                {
                    if (p.IsAlive) { respawnAt.Remove(p); continue; }
                    if (!respawnAt.TryGetValue(p, out float at)) { respawnAt[p] = RoundTime + RespawnDelay; continue; }
                    if (RoundTime >= at)
                    {
                        respawnAt.Remove(p);
                        Respawn(p);
                    }
                }
            }
            if (Mode == GameMode.KingOfTheHill) UpdateHill(dt);
            if (Mode == GameMode.Practice) UpdatePractice();
        }

        /// <summary>Seconds until a dead player respawns (for the HUD), or -1.</summary>
        public float RespawnIn(Player p) => respawnAt.TryGetValue(p, out float at) ? Mathf.Max(0f, at - RoundTime) : -1f;

        void Respawn(Player p)
        {
            var options = Map.GetSpawnPositions(p.Team, 8, new List<Vector3>());
            var enemies = Players.Where(e => e.IsAlive && e.Team != p.Team).Select(e => e.transform.position).ToList();
            Vector3 best = options[0];
            float bestDist = -1f;
            foreach (var o in options)
            {
                float d = enemies.Count == 0 ? Random.value : enemies.Min(e => (e - o).sqrMagnitude);
                if (d > bestDist) { bestDist = d; best = o; }
            }
            p.ResetForRound(best + Vector3.up * 0.05f, Map.FullBounds.center - best, false);
            p.InvulnerableTimer = 1.2f;
        }

        void UpdateHill(float dt)
        {
            var inside = Players.Where(p => p.IsAlive &&
                new Vector2(p.transform.position.x - HillCenter.x, p.transform.position.z - HillCenter.z).sqrMagnitude <= HillRadius * HillRadius &&
                Mathf.Abs(p.transform.position.y - HillCenter.y) < 3f).Select(p => p.Team).Distinct().ToList();
            HillHolder = inside.Count == 1 ? inside[0] : inside.Count > 1 ? -2 : -1;
            if (HillHolder >= 0) HillProgress[HillHolder] += dt;
            UpdateHillVisual();
        }

        /// <summary>Recolor the hill (host and clients).</summary>
        public void UpdateHillVisual()
        {
            if (hillMaterial == null) return;
            Color c = HillHolder >= 0 ? Teams.GetColor(HillHolder) : HillHolder == -2 ? Color.white : new Color(0.8f, 0.85f, 1f);
            float pulse = 0.22f + 0.1f * Mathf.Sin(Time.time * (HillHolder == -2 ? 12f : 3f));
            hillMaterial.color = new Color(c.r, c.g, c.b, pulse);
        }

        /// <summary>Network clients receive hill/kill state in snapshots.</summary>
        public void ClientSetModeState(int holder, float[] hill, int[] kills)
        {
            HillHolder = holder;
            for (int i = 0; i < Teams.Max; i++)
            {
                HillProgress[i] = hill[i];
                RoundKills[i] = kills[i];
            }
            UpdateHillVisual();
        }

        void OnPlayerDiedForMode(Player victim, DamageInfo info)
        {
            if (NetClientMode || !InMatch) return;
            var killer = info != null && info.Source != null && info.Source != victim ? info.Source : victim.LastAttacker;
            if (killer == null || killer == victim || killer.Team == victim.Team) return;
            if (Mode == GameMode.Deathmatch) RoundKills[killer.Team]++;
            if (Mode == GameMode.CardRush && killer.IsAlive)
            {
                var card = CardRegistry.DrawHand(killer, 1, Settings).FirstOrDefault();
                if (card != null)
                {
                    killer.ApplyCard(card);
                    CubedHud.AddFeedText("<color=#" + ColorUtility.ToHtmlStringRGB(killer.Color) + ">" + killer.DisplayName + "</color> drew " + card.Title);
                    Fx.Pulse(killer.Center, new Color(card.Color.r, card.Color.g, card.Color.b, 0.5f), 1f, 4f, 0.4f);
                }
            }
        }

        /// <summary>Forget per-match mode state (called whenever a match is cleared).</summary>
        void ResetModeState()
        {
            lateJoiners.Clear();
            practicePickReturn = false;
            practiceSelectPrev = false;
            respawnAt.Clear();
        }

        bool practiceSelectPrev;

        // ------------------------------------------------------------------ late joiners

        readonly List<PlayerSlot> lateJoiners = new List<PlayerSlot>();

        /// <summary>Host: an online player joined mid-match; they get a fighter next round.</summary>
        public void QueueLateJoin(PlayerSlot slot) => lateJoiners.Add(slot);

        void AddLateJoiners()
        {
            if (lateJoiners.Count == 0) return;
            foreach (var slot in lateJoiners)
            {
                if (!Settings.slots.Contains(slot)) continue; // left again before the round started
                int index = Players.Count == 0 ? 0 : Players.Max(p => p.Index) + 1;
                var p = Player.Create(slot.Clone(), index, new Net.NetworkInput(slot.netOwner));
                Players.Add(p);
            }
            lateJoiners.Clear();
            ActiveTeams.Clear();
            ActiveTeams.AddRange(Players.Select(p => p.Team).Distinct().OrderBy(t => t));
            Net.BroadcastFullSync();
        }

        // ------------------------------------------------------------------ training

        /// <summary>Start the guided tutorial / practice range with target dummies.</summary>
        public void StartTraining()
        {
            if (Net != null && (Net.IsHosting || Net.IsClient)) return;
            var t = Settings.Clone();
            t.slots.Clear();
            var me = Settings.slots.FirstOrDefault(s => s.IsLocalHuman);
            t.slots.Add(new PlayerSlot { name = me != null ? me.name : "You", control = me != null ? me.control : ControlType.KeyboardMouse, team = 0 });
            for (int i = 0; i < 3; i++) t.slots.Add(new PlayerSlot { name = "Target " + (i + 1), control = ControlType.Bot, team = 1, difficulty = BotDifficulty.Dummy });
            t.mode = GameMode.Practice;
            t.roundsToWin = 50;
            t.startingPicks = 0;
            t.picksPerRound = 0;
            t.pickTimer = 0f;
            t.borderShrinkAfter = 0f;
            t.roundTimeLimit = 0f;
            t.mapId = "builtin:garden";
            t.mapRotation = MapRotation.Fixed;
            trainingBackup = Settings;
            Settings = t;
            Tutorial = new Tutorial();
            StartMatch();
        }

        void EndTraining()
        {
            if (trainingBackup == null) return;
            trainingBackup.CopyPreferencesFrom(Settings);
            Settings = trainingBackup;
            trainingBackup = null;
            Tutorial = null;
        }

        void UpdatePractice()
        {
            var me = Players.FirstOrDefault(p => p.Input is HumanInput);
            if (me == null) return;
            Tutorial?.Update(me, this);
            var h = (HumanInput)me.Input;
            var binds = InputBindings.Current;
            bool select = !h.IsKeyboard && Controllers.Held(h.Joystick, Controllers.Profile(h.Joystick), PadButton.Select);
            bool open = h.IsKeyboard ? Input.GetKeyDown(KeyCode.C) : select && !practiceSelectPrev;
            practiceSelectPrev = select;
            if (open && State == GameState.Fighting) OpenPracticeCardPicker();
        }

        public void OpenPracticeCardPicker()
        {
            practicePickReturn = true;
            var humans = Players.Where(p => p.Input is HumanInput).ToList();
            StartPickPhase(humans, 1, false);
        }

        void ResumePracticeAfterPick()
        {
            practicePickReturn = false;
            foreach (var p in Players) p.Frozen = false;
            SetState(GameState.Fighting);
        }
    }

    /// <summary>Step-by-step tutorial shown during Training.</summary>
    public class Tutorial
    {
        public class Step
        {
            public string Title, Hint;
            public System.Func<Player, bool> Done;
        }

        public readonly List<Step> Steps = new List<Step>();
        public int Index { get; private set; }
        public bool Complete => Index >= Steps.Count;
        public float StepTime { get; private set; }

        Vector3 lastPos;
        float moved, looked, lastYaw;
        bool jumped, blocked, fallBlocked;

        public Tutorial()
        {
            Steps.Add(new Step { Title = "Move", Hint = "WASD or the left stick", Done = p => moved > 8f });
            Steps.Add(new Step { Title = "Look around", Hint = "Mouse or the right stick", Done = p => looked > 150f });
            Steps.Add(new Step { Title = "Jump", Hint = "Space or A", Done = p => jumped });
            Steps.Add(new Step { Title = "Shoot a target", Hint = "Left mouse or RT. Bullets drop over distance.", Done = p => p.DamageDealt > 0f });
            Steps.Add(new Step { Title = "Block", Hint = "Right mouse or LT. The bubble protects you from every side.", Done = p => blocked });
            Steps.Add(new Step { Title = "Defeat a target", Hint = "Targets respawn after a few seconds", Done = p => p.Kills >= 1 });
            Steps.Add(new Step { Title = "Block a fall", Hint = "Jump off the island and block just before you hit the red floor", Done = p => fallBlocked });
            Steps.Add(new Step { Title = "Take a card", Hint = "Press C (or Select) to open the card picker", Done = p => p.Cards.Count >= 1 });
            GameEvents.DamageBlocked += OnBlocked;
        }

        void OnBlocked(Player p, DamageInfo info)
        {
            if (p.Input is HumanInput && (info.Type == DamageType.Fall || info.Type == DamageType.Border)) fallBlocked = true;
        }

        public void Update(Player p, GameManager gm)
        {
            if (Complete) return;
            float dt = Time.deltaTime;
            StepTime += dt;
            var pos = p.transform.position;
            if (p.Grounded) moved += new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude;
            lastPos = pos;
            if (p.Input is HumanInput h)
            {
                looked += Mathf.Abs(Mathf.DeltaAngle(lastYaw, h.Yaw));
                lastYaw = h.Yaw;
            }
            if (!p.Grounded && p.Velocity.y > 4f) jumped = true;
            if (p.IsBlocking) blocked = true;
            if (Steps[Index].Done(p))
            {
                Index++;
                StepTime = 0f;
                Sfx.Play("pick", 0.7f, 0f);
                if (Complete)
                {
                    GameEvents.DamageBlocked -= OnBlocked;
                    Toasts.Show("Training complete!", "Keep practicing, or head to Play.", UIKit.Good);
                    Profile.Current.CompleteTraining();
                }
            }
        }
    }
}
