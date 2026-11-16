using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Spheres.Automation
{
    /// <summary>
    /// Plays through the whole game without a human and fails if anything logs an error or exception:
    /// every menu screen, the map editor (open, load every map, test-play, close), training, the pause menu
    /// with the options overlay, a bots-only match on every registered map (including card pick phases)
    /// and every non-elimination game mode.
    ///
    /// Options (command line):
    ///   -smokeSeconds 60          simulated seconds per map
    ///   -smokeModeSeconds 40      simulated seconds per extra game mode
    ///   -smokeBots 4              bots per match (split over two teams)
    ///   -smokeMaps a,b            only these map ids
    ///   -smokeSkip menus,editor,training,pause,maps,modes
    ///   -smokeReport path         report file (default Builds/smoke-report.txt)
    ///   -simFps 50 -simTimeScale 2 -automationTimeout 60 (minutes) -automationIgnore regex
    /// </summary>
    public class SmokeTestRunner : AutomationRunner
    {
        class SectionResult
        {
            public string Name;
            public float SimSeconds;
            public float WallSeconds;
            public int Rounds;
            public int PickPhases;
            public int CardsPicked;
            public int MatchesFinished;
            public string Note = "";
        }

        readonly List<SectionResult> sections = new List<SectionResult>();
        SectionResult current;
        SettingsBackup backup;
        HashSet<string> skip;
        int roundsEnded, pickPhasesEnded, cardsPicked, matchesEnded;

        protected override string Name => "Smoke test";

        protected override void Start()
        {
            WallLimit = 60f * 60f;
            base.Start();
            GameEvents.RoundEnded += OnRoundEnded;
            GameEvents.PickPhaseEnded += OnPickPhaseEnded;
            GameEvents.CardPicked += OnCardPicked;
            GameEvents.MatchEnded += OnMatchEnded;
        }

        protected override void OnDestroy()
        {
            GameEvents.RoundEnded -= OnRoundEnded;
            GameEvents.PickPhaseEnded -= OnPickPhaseEnded;
            GameEvents.CardPicked -= OnCardPicked;
            GameEvents.MatchEnded -= OnMatchEnded;
            base.OnDestroy();
        }

        void OnRoundEnded(int round, int winner) => roundsEnded++;
        void OnPickPhaseEnded() => pickPhasesEnded++;
        void OnCardPicked(Player p, CardDefinition c) => cardsPicked++;
        void OnMatchEnded(int winner) => matchesEnded++;

        bool Enabled(string section) => !skip.Contains(section);

        SectionResult BeginSection(string name)
        {
            EndSection();
            Log.Context = name;
            current = new SectionResult { Name = name, WallSeconds = WallTime };
            roundsEnded = pickPhasesEnded = cardsPicked = matchesEnded = 0;
            sections.Add(current);
            Say("Section " + name);
            // Every section starts from the main menu, even if the previous one was aborted half-way.
            if (name != "boot" && Gm != null && (Gm.InMatch || Gm.State == GameState.Editor || Gm.IsTraining)) Gm.ReturnToMenu();
            return current;
        }

        void EndSection()
        {
            if (current == null) return;
            current.WallSeconds = WallTime - current.WallSeconds;
            current.Rounds = roundsEnded;
            current.PickPhases = pickPhasesEnded;
            current.CardsPicked = cardsPicked;
            current.MatchesFinished = matchesEnded;
            current = null;
        }

        protected override IEnumerator Run()
        {
            skip = new HashSet<string>(Args.GetList("-smokeSkip").Select(s => s.ToLowerInvariant()));
            BeginSection("boot");
            float t0 = Time.realtimeSinceStartup;
            while (Gm == null)
            {
                if (Time.realtimeSinceStartup - t0 > 60f)
                {
                    Log.AddFailure("boot", "GameManager.Instance never appeared (Bootstrap did not run?)");
                    yield break;
                }
                yield return null;
            }
            yield return Frames(10);
            if (Gm.State != GameState.MainMenu) Log.AddFailure("boot", "Expected the main menu after boot, got " + Gm.State);
            current.Note = MapRegistry.All.Count + " maps, " + CardRegistry.All.Count + " cards registered";
            backup = new SettingsBackup(Gm.Settings);

            if (Enabled("menus")) yield return Menus();
            if (Enabled("editor")) yield return MapEditor();
            if (Enabled("training")) yield return Training();
            if (Enabled("pause")) yield return PauseAndOptions();
            if (Enabled("maps")) yield return AllMaps();
            if (Enabled("modes")) yield return Modes();

            BeginSection("shutdown");
            Gm.ReturnToMenu();
            yield return Frames(5);
            if (Gm.State != GameState.MainMenu) Log.AddFailure("shutdown", "ReturnToMenu left the game in state " + Gm.State);
            backup.Restore(Gm.Settings);
            backup = null;
            EndSection();
        }

        // ------------------------------------------------------------------ sections

        IEnumerator Menus()
        {
            BeginSection("menus");
            var screens = new[]
            {
                GameState.Setup, GameState.CardLibrary, GameState.Mods, GameState.Options, GameState.ProfileScreen,
                GameState.CharacterEditor, GameState.Online, GameState.MainMenu,
            };
            foreach (var s in screens)
            {
                Gm.GoToMenu(s);
                yield return Frames(15);
                if (Gm.State != s) Log.AddFailure("menus", "GoToMenu(" + s + ") ended in state " + Gm.State);
            }
            // Map previews in the menu background build every map once in play mode.
            foreach (var entry in MapRegistry.All.ToList())
            {
                Log.Context = "menus/preview:" + entry.Id;
                Gm.PreviewMap(entry.Id);
                yield return Frames(2);
            }
            Log.Context = "menus";
            Gm.PreviewMap(Gm.Settings.mapId);
            yield return Frames(5);
        }

        IEnumerator MapEditor()
        {
            BeginSection("editor");
            Gm.OpenEditor();
            yield return Frames(15);
            if (Gm.State != GameState.Editor || !Gm.Editor.IsOpen) Log.AddFailure("editor", "OpenEditor() did not open the map editor (state " + Gm.State + ")");

            // Load every map into the editor (editor-mode map build).
            foreach (var entry in MapRegistry.All.ToList())
            {
                Log.Context = "editor/load:" + entry.Id;
                Gm.OpenEditor(entry.Data);
                yield return Frames(3);
            }
            Log.Context = "editor";
            Gm.OpenEditor();
            yield return Frames(5);

            // Test play the map being edited with bots, then go back to the editor.
            Log.Context = "editor/test-play";
            var s = Gm.Settings;
            ConfigureBots(s, 2, 2, new[] { BotDifficulty.Normal }, "Editor Bot");
            ApplyMatchRules(s, GameMode.Elimination, Gm.Settings.mapId);
            s.startingPicks = 0;
            Gm.StartMatch(Gm.Editor.Data);
            yield return Frames(2);
            if (!Gm.InMatch || !Gm.TestingFromEditor) Log.AddFailure("editor/test-play", "StartMatch(editor map) did not start a test match (state " + Gm.State + ")");
            yield return WaitGame(10f);
            Gm.BackToEditor();
            yield return Frames(10);
            if (Gm.State != GameState.Editor || !Gm.Editor.IsOpen) Log.AddFailure("editor/test-play", "BackToEditor() ended in state " + Gm.State);

            Log.Context = "editor/close";
            Gm.ReturnToMenu();
            yield return Frames(10);
            if (Gm.Editor.IsOpen) Log.AddFailure("editor/close", "The map editor is still open after ReturnToMenu()");
            if (Gm.State != GameState.MainMenu) Log.AddFailure("editor/close", "ReturnToMenu() from the editor ended in state " + Gm.State);
        }

        IEnumerator Training()
        {
            var sec = BeginSection("training");
            Gm.StartTraining();
            yield return Frames(2);
            if (!Gm.IsTraining || !Gm.InMatch) { Log.AddFailure("training", "StartTraining() did not start training (state " + Gm.State + ")"); yield break; }
            yield return WaitGame(15f, () => Gm.State == GameState.Fighting);
            yield return WaitGame(4f);
            if (Gm.State == GameState.Fighting)
            {
                Gm.OpenPracticeCardPicker();
                yield return Frames(5);
                foreach (var st in Gm.Picks.ToList())
                    for (int guard = 0; guard < 20 && !st.Done && Gm.State == GameState.CardPick; guard++) Gm.Pick(st, 0);
                yield return WaitGame(5f, () => Gm.State == GameState.Fighting);
                if (Gm.State != GameState.Fighting) Log.AddFailure("training", "Practice card picker did not return to the fight (state " + Gm.State + ")");
            }
            else sec.Note = "never reached Fighting (state " + Gm.State + ")";
            yield return WaitGame(3f);
            Gm.ReturnToMenu();
            yield return Frames(5);
            if (Gm.IsTraining) Log.AddFailure("training", "Still in training after ReturnToMenu()");
        }

        IEnumerator PauseAndOptions()
        {
            BeginSection("pause");
            var s = Gm.Settings;
            ConfigureBots(s, 2, 2, new[] { BotDifficulty.Normal }, "Pause Bot");
            ApplyMatchRules(s, GameMode.Elimination, MapRegistry.GetOrDefault(s.mapId).Id);
            s.startingPicks = 0;
            Gm.StartMatch();
            yield return WaitGame(15f, () => Gm.State == GameState.Fighting);
            Gm.SetPaused(true);
            yield return Frames(10);
            if (!Gm.Paused) Log.AddFailure("pause", "SetPaused(true) did not pause the match");
            Gm.ShowOptionsOverlay = true;
            yield return Frames(15);
            Gm.ShowOptionsOverlay = false;
            Gm.SetPaused(false);
            yield return WaitGame(3f);
            if (Gm.Paused) Log.AddFailure("pause", "SetPaused(false) did not resume the match");
            Gm.Rematch();
            yield return WaitGame(5f);
            Gm.ReturnToMenu();
            yield return Frames(5);
        }

        IEnumerator AllMaps()
        {
            float seconds = Mathf.Max(5f, Args.GetFloat("-smokeSeconds", 60f));
            int bots = Mathf.Clamp(Args.GetInt("-smokeBots", 4), 2, 8);
            var only = Args.GetList("-smokeMaps");
            var maps = MapRegistry.All.ToList();
            if (only.Count > 0)
            {
                foreach (var id in only)
                    if (!maps.Any(m => m.Id == id)) Log.AddFailure("maps", "Map '" + id + "' from -smokeMaps is not registered");
                maps = maps.Where(m => only.Contains(m.Id)).ToList();
            }
            if (maps.Count == 0) Log.AddFailure("maps", "No maps registered");
            foreach (var entry in maps)
                yield return PlayMatch("map:" + entry.Id, entry.Id, GameMode.Elimination, seconds, bots);
        }

        IEnumerator Modes()
        {
            float seconds = Mathf.Max(5f, Args.GetFloat("-smokeModeSeconds", 40f));
            int bots = Mathf.Clamp(Args.GetInt("-smokeBots", 4), 2, 8);
            string map = MapRegistry.GetOrDefault("builtin:garden").Id;
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                if (mode == GameMode.Elimination || mode == GameMode.Practice) continue; // covered by maps / training
                yield return PlayMatch("mode:" + mode, map, mode, seconds, bots);
            }
        }

        /// <summary>A bots-only match on one map: at least `seconds` of simulated time and one completed pick phase.</summary>
        IEnumerator PlayMatch(string section, string mapId, GameMode mode, float seconds, int bots)
        {
            var sec = BeginSection(section);
            Gm.ReturnToMenu();
            yield return null;
            var s = Gm.Settings;
            ConfigureBots(s, bots, 2, new[] { BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Easy, BotDifficulty.Brutal }, "Smoke Bot");
            ApplyMatchRules(s, mode, mapId);
            Gm.StartMatch();
            yield return null;
            if (!Gm.InMatch) { Log.AddFailure(section, "StartMatch() did not start a match (state " + Gm.State + ")"); yield break; }
            if (Gm.CurrentMapId != mapId) Log.AddFailure(section, "Match loaded map '" + Gm.CurrentMapId + "' instead of '" + mapId + "'");

            float sim = 0f, wallStart = Time.realtimeSinceStartup;
            float wallLimit = Mathf.Max(60f, Args.GetFloat("-smokeMapWallLimit", 300f));
            bool badPositionReported = false;
            int frame = 0;
            while (true)
            {
                yield return null;
                sim += Time.deltaTime;
                frame++;
                if (Gm.State == GameState.MatchEnd && Gm.StateTime > 1.5f) Gm.Rematch();
                if (!badPositionReported && frame % 30 == 0 && HasBadPosition(Gm))
                {
                    badPositionReported = true;
                    Log.AddFailure(section, "A player's position became NaN/Infinity");
                }
                if (sim >= seconds && pickPhasesEnded >= 1) break;
                if (Time.realtimeSinceStartup - wallStart > wallLimit)
                {
                    if (pickPhasesEnded < 1)
                        Log.AddFailure(section, "No card pick phase completed within " + Mathf.RoundToInt(wallLimit) + " s wall time (state " + Gm.State + ", " + sim.ToString("0") + " s simulated)");
                    else sec.Note = "stopped by wall limit after " + sim.ToString("0") + " s simulated";
                    break;
                }
            }
            sec.SimSeconds = sim;
            if (roundsEnded == 0 && string.IsNullOrEmpty(sec.Note)) sec.Note = "no round finished";
            Gm.ReturnToMenu();
            yield return Frames(3);
        }

        static void ApplyMatchRules(MatchSettings s, GameMode mode, string mapId)
        {
            s.mode = mode;
            s.mapId = mapId;
            s.mapRotation = MapRotation.Fixed;
            s.roundsToWin = 3;
            s.killsToWinRound = 3;
            s.hillSeconds = 10f;
            s.roundTimeLimit = 0f;
            s.startingPicks = 1;
            s.picksPerRound = 1;
            s.pickRule = PickRule.Everyone;
            s.pickTimer = 5f;
            s.rerollBetweenPicks = true;
            s.borderShrinkAfter = 20f;
            s.borderShrinkDuration = 20f;
        }

        // ------------------------------------------------------------------ report

        protected override void WriteReport(string status)
        {
            EndSection();
            if (backup != null && Gm != null)
            {
                try { backup.Restore(Gm.Settings); } catch (Exception e) { Debug.LogException(e); }
                backup = null;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Spheres smoke test report");
            sb.AppendLine("Result:   " + status + " (" + Log.ErrorCount + " errors, " + Log.WarningCount + " warnings)");
            sb.AppendLine("Date:     " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            sb.AppendLine("Game:     Spheres " + SpheresAPI.GameVersion + " | Unity " + Application.unityVersion + " | " + Application.platform +
                          (Application.isEditor ? " (editor)" : "") + (Application.isBatchMode ? " batchmode" : "") +
                          " | graphics " + SystemInfo.graphicsDeviceType);
            sb.AppendLine("Settings: " + Args.GetFloat("-smokeSeconds", 60f) + " s per map, sim fps " + SimFps + ", time scale " + TimeScale +
                          ", wall time " + Mathf.RoundToInt(WallTime) + " s");
            if (Application.isBatchMode)
                sb.AppendLine("Note:     OnGUI does not run in batchmode, so menu/HUD drawing code is only covered by a run with graphics.");
            sb.AppendLine();
            sb.AppendLine("== Sections ==");
            foreach (var r in sections)
            {
                int errs = Log.ErrorsIn(r.Name);
                // Sub-contexts (e.g. "editor/load:<map>") count towards their section.
                foreach (var e in Log.Errors())
                    if (e.Context != r.Name && e.Context.StartsWith(r.Name + "/")) errs += e.Count;
                string line = (errs > 0 ? "[FAIL] " : "[ok]   ") + r.Name.PadRight(34) + " " + errs + " errors";
                if (r.SimSeconds > 0f) line += string.Format(" | sim {0:0}s, rounds {1}, pick phases {2}, cards {3}, matches {4}, wall {5:0.0}s",
                    r.SimSeconds, r.Rounds, r.PickPhases, r.CardsPicked, r.MatchesFinished, r.WallSeconds);
                if (!string.IsNullOrEmpty(r.Note)) line += " | " + r.Note;
                sb.AppendLine(line);
            }
            Log.AppendDetails(sb, 40);

            string text = sb.ToString();
            string path = ReportPath(Args.Get("-smokeReport", Path.Combine("Builds", "smoke-report.txt")));
            File.WriteAllText(path, text);
            // Print to stdout as well (Debug.Log goes to the -logFile, Console to the process output).
            Debug.Log("[SpheresAutomation] Report written to " + path + "\n" + text);
            try { Console.WriteLine(text); } catch (Exception) { }
        }
    }
}
