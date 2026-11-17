using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    public enum GameState { MainMenu, Setup, CardLibrary, Mods, Options, ProfileScreen, CharacterEditor, Editor, Online, Lobby, RoundIntro, Fighting, RoundEnd, CardPick, MatchEnd }

    /// <summary>One player's progress through a card pick phase.</summary>
    public class PickState
    {
        public Player Player;
        public int PicksRemaining;
        public int PicksTotal;
        /// <summary>Unscaled time the current hand was dealt (drives the deal animation).</summary>
        public float DealtTime;
        public List<CardDefinition> Hand = new List<CardDefinition>();
        public int Highlight;
        public float TimeLeft;
        public float BotDelay;
        public float BotWander;
        public CardDefinition LastPicked;
        public readonly List<CardDefinition> PickedThisPhase = new List<CardDefinition>();
        public bool Done => PicksRemaining <= 0;
        public int PickNumber => PicksTotal - PicksRemaining + 1;
    }

    /// <summary>
    /// Owns the match: players, map, rounds, card pick phases, cameras and menu states.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        /// <summary>The arena currently in play (or being edited).</summary>
        public static MapRuntime ActiveMap => Instance != null ? (Instance.State == GameState.Editor && Instance.Editor != null ? Instance.Editor.Map : Instance.Map) : null;

        public const float IntroDuration = 3f;
        public const float RoundEndDuration = 3f;

        public MatchSettings Settings { get; private set; }
        public GameState State { get; private set; } = GameState.MainMenu;
        public MapRuntime Map { get; private set; }
        public string CurrentMapId { get; private set; }
        public readonly List<Player> Players = new List<Player>();
        public readonly List<PlayerCamera> Cameras = new List<PlayerCamera>();
        public readonly List<int> ActiveTeams = new List<int>();
        public readonly int[] TeamScores = new int[Teams.Max];
        public readonly List<PickState> Picks = new List<PickState>();
        public int RoundNumber { get; private set; }
        public int LastRoundWinner { get; private set; } = -1;
        public int MatchWinner { get; private set; } = -1;
        /// <summary>Unscaled seconds spent in the current state (does not advance while paused).</summary>
        public float StateTime { get; private set; }
        /// <summary>Scaled seconds of fighting in the current round.</summary>
        public float RoundTime { get; private set; }
        public bool Paused { get; private set; }
        public bool InMatch => State >= GameState.RoundIntro;
        public bool StartingPickPhase { get; private set; }
        public bool TestingFromEditor => editorTestMap != null;
        /// <summary>True while this machine is a network client in a match: the host simulates, we display.</summary>
        public bool NetClientMode { get; private set; }
        public Net.NetSession Net { get; private set; }

        public Camera OverviewCamera { get; private set; }
        public MapEditor Editor { get; private set; }

        MapData editorTestMap;
        readonly bool[] pausePrev = new bool[ControlTypes.MaxGamepads + 1];
        MatchSettings localSettingsBackup;
        float pickDoneTimer;
        float orbitAngle;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Settings = MatchSettings.Load();
            Options.Current.Apply(false);
            Controllers.Changed += OnControllerChanged;
            Achievements.Hook();
            CrashLog.Init();
            Spheres.Net.SteamIntegration.Init();
            if (Spheres.Net.SteamIntegration.PlayerName != null && (Settings.onlineName == "Player" || string.IsNullOrWhiteSpace(Settings.onlineName)))
                Settings.onlineName = Spheres.Net.SteamIntegration.PlayerName;
            Sfx.Init(gameObject);
            Sfx.Volume = Settings.volume;
            gameObject.AddComponent<AudioListener>();
            SetupLighting();

            OverviewCamera = new GameObject("Overview Camera").AddComponent<Camera>();
            OverviewCamera.transform.SetParent(transform, false);
            OverviewCamera.clearFlags = CameraClearFlags.SolidColor;
            OverviewCamera.fieldOfView = 55f;
            OverviewCamera.farClipPlane = 800f;
            OverviewCamera.depth = 0f;
            SpheresPostFX.Attach(OverviewCamera);

            ContentLoader.LoadAll();
            MapRegistry.RefreshUserMaps();

            Net = gameObject.AddComponent<Net.NetSession>();
            gameObject.AddComponent<MusicPlayer>();
            gameObject.AddComponent<SpheresUI>();
            AutoUpdater.Ensure(gameObject);
            Editor = gameObject.AddComponent<MapEditor>();
            ShowMenuBackdrop();
        }

        void SetupLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.38f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.17f, 0.21f);
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.antiAliasing = 4;
            QualitySettings.pixelLightCount = 6;

            // Cool fill light from the opposite side so shadowed faces keep their shape.
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.55f, 0.65f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 150f, 0f);
            Weather.RegisterLights(sun, fill);
        }

        void OnApplicationQuit()
        {
            Profile.Current.Save();
            Spheres.Net.SteamIntegration.Shutdown();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ menu states

        public void SetState(GameState state)
        {
            var old = State;
            State = state;
            StateTime = 0f;
            OnStateChanged(old, state);
        }

        void OnStateChanged(GameState old, GameState now)
        {
            if (old == now) return;
            if (now == GameState.MatchEnd) Podium.Show(this);
            else if (old == GameState.MatchEnd) Podium.Hide();
            bool menu = now == GameState.MainMenu || now == GameState.ProfileScreen || now == GameState.Online || now == GameState.Mods || now == GameState.Options || now == GameState.CardLibrary;
            if (!menu) MenuShowcase.Clear();
        }

        bool podiumCamera;

        /// <summary>Podium: show it full-screen on the overview camera (restores the normal layout when off).</summary>
        public void SetPodiumCamera(bool on)
        {
            podiumCamera = on;
            if (on)
            {
                foreach (var c in Cameras) if (c != null && c.Cam != null) c.Cam.enabled = false;
                OverviewCamera.enabled = true;
                OverviewCamera.rect = new Rect(0f, 0f, 1f, 1f);
                OverviewCamera.transform.position = Podium.CameraPos;
                OverviewCamera.transform.LookAt(Podium.CameraLook);
            }
            else if (InMatch)
            {
                foreach (var c in Cameras) if (c != null && c.Cam != null) c.Cam.enabled = true;
                LayoutCameras();
            }
        }

        /// <summary>Switch between menu screens (MainMenu, Setup, CardLibrary, Mods).</summary>
        public void GoToMenu(GameState menu)
        {
            if (InMatch || State == GameState.Editor) ReturnToMenu();
            SetState(menu);
        }

        public void ReturnToMenu()
        {
            Weather.Active = false;
            Weather.Clear();
            EndTraining();
            if (NetClientMode) ClientExitMatch(GameState.MainMenu);
            if (Net != null && Net.IsHosting && InMatch)
            {
                // The host leaving a match returns everyone to the lobby.
                ClearMatch();
                editorTestMap = null;
                Net.BroadcastMatchAbort();
                SetState(GameState.Setup);
                ShowMenuBackdrop();
                return;
            }
            ClearMatch();
            if (Editor.IsOpen) Editor.Close();
            editorTestMap = null;
            SetState(GameState.MainMenu);
            ShowMenuBackdrop();
        }

        void ShowMenuBackdrop()
        {
            Weather.Active = false;
            var entry = MapRegistry.GetOrDefault(Settings.mapId);
            if (entry == null) return;
            ReplaceMap(entry.Data.Clone());
            CurrentMapId = entry.Id;
            OverviewCamera.enabled = true;
            OverviewCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>Preview a map in the menu background.</summary>
        public void PreviewMap(string mapId)
        {
            if (InMatch || State == GameState.Editor) return;
            var entry = MapRegistry.Get(mapId);
            if (entry == null) return;
            ReplaceMap(entry.Data.Clone());
            CurrentMapId = entry.Id;
        }

        void ReplaceMap(MapData data)
        {
            if (Map != null) DestroyImmediate(Map.gameObject);
            Map = MapRuntime.Build(data, false);
            Weather.Apply(Map);
            Map.SetBorderStyle(Settings.damagingBorders, Settings.fallDamage);
            OverviewCamera.backgroundColor = Map.Background;
            foreach (var c in Cameras) c.Cam.backgroundColor = Map.Background;
        }

        public void OpenEditor(MapData data = null)
        {
            ClearMatch();
            if (Map != null) DestroyImmediate(Map.gameObject);
            Map = null;
            editorTestMap = null;
            OverviewCamera.enabled = false;
            SetState(GameState.Editor);
            Editor.Open(data);
        }

        public void SaveSettings()
        {
            if (NetClientMode || trainingBackup != null)
            {
                Sfx.Volume = Settings.volume;
                return; // temporary rules are active; preferences are copied back on exit
            }
            Settings.Validate();
            Sfx.Volume = Settings.volume;
            Settings.Save();
        }

        // ------------------------------------------------------------------ match lifecycle

        /// <summary>Start a match with the current settings. testMap overrides the map (editor test play).</summary>
        /// <summary>Round wins needed to take the match (grows when players choose "Keep going").</summary>
        public int RoundsTarget { get; private set; } = 5;
        public bool Endless => Settings.endless || endlessRun;
        bool endlessRun;

        public void StartMatch(MapData testMap = null)
        {
            Settings.Validate();
            RoundsTarget = Mathf.Max(1, Settings.roundsToWin);
            endlessRun = false;
            Settings.weatherSeed = UnityEngine.Random.Range(1, 1 << 30);
            Weather.Active = true;
            string error = Settings.CheckPlayable();
            if (error != null)
            {
                Debug.LogWarning("[Spheres] " + error);
                return;
            }
            SaveSettings();
            if (State == GameState.Editor) Editor.Hide();
            ClearMatch();

            editorTestMap = testMap;
            Array.Clear(TeamScores, 0, TeamScores.Length);
            RoundNumber = 0;
            MatchWinner = -1;
            LastRoundWinner = -1;

            int index = 0;
            foreach (var slot in Settings.ActiveSlots)
            {
                IPlayerInput input;
                if (slot.control == ControlType.Bot) input = new BotBrain(slot.difficulty);
                else if (slot.control == ControlType.Online) input = new Net.NetworkInput(slot.netOwner);
                else input = new HumanInput(slot.control);
                var slotCopy = slot.Clone();
                if (slotCopy.IsLocalHuman) slotCopy.look = Profile.Current.Look(slotCopy.lookIndex).Encode();
                else if (slotCopy.control == ControlType.Bot) slotCopy.look = CharacterLook.ForSeed(slotCopy.name).Encode();
                else if (string.IsNullOrEmpty(slotCopy.look)) slotCopy.look = CharacterLook.ForSeed(slotCopy.name).Encode();
                var p = Player.Create(slotCopy, index++, input);
                Players.Add(p);
                if (input is HumanInput human) Cameras.Add(PlayerCamera.Create(p, human));
            }
            ActiveTeams.AddRange(Players.Select(p => p.Team).Distinct().OrderBy(t => t));
            LayoutCameras();
            LoadMapForRound(true);
            GameEvents.RaiseMatchStarted(Settings);

            if (Settings.startingPicks > 0)
            {
                SpawnPlayers();
                StartPickPhase(Players.ToList(), Settings.startingPicks, true);
            }
            else BeginRound();
        }

        /// <summary>Restart the match with identical settings.</summary>
        public void Rematch() => StartMatch(editorTestMap);

        public void BackToEditor()
        {
            var map = editorTestMap;
            ClearMatch();
            editorTestMap = null;
            if (Map != null) DestroyImmediate(Map.gameObject);
            Map = null;
            OverviewCamera.enabled = false;
            SetState(GameState.Editor);
            Editor.Show();
            if (map == null) Editor.Open(null);
        }

        void ClearMatch()
        {
            Time.timeScale = 1f;
            Paused = false;
            ResetModeState();
            Bullet.ClearAll();
            Fx.Clear();
            // Deactivate first: Destroy is deferred and stale colliders/updates would leak into this frame.
            foreach (var p in Players) if (p != null) { p.gameObject.SetActive(false); Destroy(p.gameObject); }
            foreach (var c in Cameras) if (c != null) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            Players.Clear();
            Cameras.Clear();
            ActiveTeams.Clear();
            Picks.Clear();
            SpheresHud.Clear();
        }

        /// <summary>Split-screen cells (normalized, bottom-left origin) for n viewports: 1, 2 side by side, 2x2, 3x2 or 4x2.</summary>
        public static Rect[] SplitLayout(int n)
        {
            if (n <= 1) return new[] { new Rect(0f, 0f, 1f, 1f) };
            if (n == 2) return new[] { new Rect(0f, 0f, 0.5f, 1f), new Rect(0.5f, 0f, 0.5f, 1f) };
            int cols = n <= 4 ? 2 : n <= 6 ? 3 : 4;
            const int rows = 2;
            var cells = new Rect[cols * rows];
            for (int i = 0; i < cells.Length; i++)
            {
                int c = i % cols, r = i / cols;
                cells[i] = new Rect(c / (float)cols, 1f - (r + 1) / (float)rows, 1f / cols, 1f / rows);
            }
            return cells;
        }

        void LayoutCameras()
        {
            int n = Cameras.Count;
            var cells = SplitLayout(n);
            for (int i = 0; i < n; i++)
            {
                Cameras[i].SetViewport(cells[Mathf.Min(i, cells.Length - 1)]);
                Cameras[i].Cam.depth = 1 + i;
                Cameras[i].SetBaseFov(n == 2 ? 75f : n > 4 ? 78f : 70f);
            }
            // Spectator view fills the screen with no humans, or the first empty split-screen cell.
            OverviewCamera.enabled = n == 0 || cells.Length > n;
            OverviewCamera.rect = n == 0 ? new Rect(0f, 0f, 1f, 1f) : cells[Mathf.Min(n, cells.Length - 1)];
        }

        void LoadMapForRound(bool first)
        {
            MapData data;
            if (editorTestMap != null)
            {
                data = editorTestMap.Clone();
                CurrentMapId = "editor";
            }
            else
            {
                var all = MapRegistry.All;
                MapEntry entry = null;
                string voted = !first ? ResolveMapVote() : null;
                if (voted != null) entry = MapRegistry.Get(voted);
                else if (!first && Settings.mapRotation == MapRotation.Vote && all.Count > 0)
                    entry = all[UnityEngine.Random.Range(0, all.Count)];
                else if (!first && Settings.mapRotation == MapRotation.Cycle && all.Count > 0)
                {
                    int idx = all.ToList().FindIndex(e => e.Id == CurrentMapId);
                    entry = all[(idx + 1 + all.Count) % all.Count];
                }
                else if (!first && Settings.mapRotation == MapRotation.Random && all.Count > 0)
                    entry = all[UnityEngine.Random.Range(0, all.Count)];
                else if (!first)
                    entry = MapRegistry.Get(CurrentMapId);
                if (entry == null) entry = MapRegistry.GetOrDefault(Settings.mapId);
                CurrentMapId = entry.Id;
                data = entry.Data.Clone();
            }
            ReplaceMap(data);
        }

        void SpawnPlayers()
        {
            var used = new List<Vector3>();
            foreach (int team in ActiveTeams)
            {
                var members = Players.Where(p => p.Team == team).ToList();
                var spots = Map.GetSpawnPositions(team, members.Count, used);
                used.AddRange(spots);
                for (int i = 0; i < members.Count; i++)
                {
                    Vector3 look = Map.FullBounds.center - spots[i];
                    members[i].ResetForRound(spots[i] + Vector3.up * 0.05f, look);
                }
            }
        }

        void BeginRound()
        {
            RoundNumber++;
            if (RoundNumber > 1 || Map == null) LoadMapForRound(false);
            AddLateJoiners();
            Bullet.ClearAll();
            Fx.Clear();
            Map.SetShrink(0f);
            SpawnPlayers();
            foreach (var p in Players) p.Frozen = true;
            RoundTime = 0f;
            ModeRoundStart();
            SetState(GameState.RoundIntro);
            GameEvents.RaiseRoundStarted(RoundNumber);
        }

        void EndRound(int winner)
        {
            if (Mode == GameMode.Infection) RestoreHomeTeams();
            LastRoundWinner = winner;
            if (winner >= 0 && winner < TeamScores.Length) TeamScores[winner]++;
            SetState(GameState.RoundEnd);
            Time.timeScale = 0.35f;
            Sfx.Play("win", 0.7f, 0f);
            GameEvents.RaiseRoundEnded(RoundNumber, winner);
        }

        void AfterRound()
        {
            Time.timeScale = 1f;
            int w = LastRoundWinner;
            if (!Endless && w >= 0 && TeamScores[w] >= RoundsTarget)
            {
                MatchWinner = w;
                foreach (var p in Players) p.Frozen = true;
                Bullet.ClearAll();
                SetState(GameState.MatchEnd);
                Sfx.Play("win", 1f, 0f);
                foreach (var p in Players)
                {
                    if (p.Team != w) continue;
                    for (int k = 0; k < 4; k++)
                        Fx.Burst(p.Top + Vector3.up, Teams.GetColor(UnityEngine.Random.Range(0, Teams.Max)), 25, 12f, 0.14f, 2.5f, 6f);
                }
                GameEvents.RaiseMatchEnded(w);
                return;
            }
            ContinueAfterRound(w);
        }

        /// <summary>
        /// Match over, but keep going with everyone's cards: another set of rounds (endless = never end again).
        /// Host only; clients follow through snapshots.
        /// </summary>
        public void KeepGoing(bool endless)
        {
            if (State != GameState.MatchEnd || NetClientMode) return;
            if (endless) endlessRun = true;
            else RoundsTarget += Mathf.Max(1, Settings.roundsToWin);
            int w = MatchWinner >= 0 ? MatchWinner : LastRoundWinner;
            MatchWinner = -1;
            SpheresHud.ShowBanner(endless ? "ENDLESS MODE - the decks get crazier" : "KEEP GOING - first to " + RoundsTarget, new Color(1f, 0.85f, 0.3f));
            SpheresHud.AddFeedText(endless ? "The match is now endless!" : "Match extended: first to " + RoundsTarget + " rounds");
            ContinueAfterRound(w);
        }

        /// <summary>Clients: round target and endless flag from snapshots.</summary>
        public void ClientSetTarget(int target, bool endless)
        {
            RoundsTarget = Mathf.Max(1, target);
            endlessRun = endless;
        }

        void ContinueAfterRound(int w)
        {
            ApplyCatchUpCurses();
            List<Player> pickers;
            switch (Settings.pickRule)
            {
                case PickRule.Losers: pickers = Players.Where(p => w < 0 || p.Team != w).ToList(); break;
                case PickRule.Winners: pickers = Players.Where(p => w < 0 || p.Team == w).ToList(); break;
                case PickRule.Everyone: pickers = Players.ToList(); break;
                default: pickers = new List<Player>(); break;
            }
            if (Settings.picksPerRound <= 0 || pickers.Count == 0) BeginRound();
            else StartPickPhase(pickers, Settings.picksPerRound, false);
        }

        // ------------------------------------------------------------------ card picks

        void StartPickPhase(List<Player> pickers, int picks, bool starting)
        {
            Bullet.ClearAll();
            Picks.Clear();
            foreach (var p in Players) p.Frozen = true;
            foreach (var p in pickers)
            {
                var st = new PickState { Player = p, PicksRemaining = picks, PicksTotal = picks };
                DealHand(st);
                Picks.Add(st);
            }
            StartingPickPhase = starting;
            pickDoneTimer = 0f;
            if (!starting) StartMapVote();
            SetState(GameState.CardPick);
            GameEvents.RaisePickPhaseStarted();
        }

        void DealHand(PickState st)
        {
            int count = Mathf.Clamp(Settings.cardsPerHand + st.Player.GetStatInt(Stat.HandSize), 1, 10);
            st.Hand = CardRegistry.DrawHand(st.Player, count, Settings);
            st.Highlight = st.Hand.Count / 2;
            st.TimeLeft = Settings.pickTimer;
            st.BotDelay = UnityEngine.Random.Range(0.8f, 1.8f);
            st.DealtTime = Time.unscaledTime;
            if (st.Player.IsHuman) Sfx.Play("deal", 0.6f, 0f);
            if (st.Hand.Count == 0) st.PicksRemaining = 0;
        }

        /// <summary>Pick a card from a hand (also called by the UI for mouse clicks).</summary>
        public void Pick(PickState st, int index)
        {
            if (st == null || st.Done || State != GameState.CardPick || Paused) return;
            if (NetClientMode)
            {
                Net.SendPickChoice(st.Player.Index, index);
                return;
            }
            if (index < 0 || index >= st.Hand.Count) index = UnityEngine.Random.Range(0, st.Hand.Count);
            if (st.Hand.Count == 0) { st.PicksRemaining = 0; return; }
            var card = st.Hand[index];
            st.Player.ApplyCard(card);
            st.LastPicked = card;
            st.PickedThisPhase.Add(card);
            st.PicksRemaining--;
            if (st.Player.IsHuman) Sfx.Play("pick", 0.8f, 0f);
            if (st.Done) return;
            if (Settings.rerollBetweenPicks) DealHand(st);
            else
            {
                st.Hand.RemoveAt(index);
                st.Hand.RemoveAll(c => !CardRegistry.IsEligible(c, st.Player, Settings));
                if (st.Hand.Count == 0) DealHand(st);
                else
                {
                    st.Highlight = Mathf.Clamp(st.Highlight, 0, st.Hand.Count - 1);
                    st.TimeLeft = Settings.pickTimer;
                    st.BotDelay = UnityEngine.Random.Range(0.6f, 1.4f);
                }
            }
        }

        void UpdatePicks(float dt)
        {
            UpdateMapVote(dt);
            foreach (var st in Picks)
            {
                if (st.Done) continue;
                if (Settings.pickTimer > 0f)
                {
                    float before = st.TimeLeft;
                    st.TimeLeft -= dt;
                    if (st.Player.IsHuman && st.TimeLeft <= 3f && Mathf.Ceil(before) != Mathf.Ceil(st.TimeLeft) && st.TimeLeft > 0f)
                        Sfx.Play("tick", 0.6f, 0f);
                    if (st.TimeLeft <= 0f)
                    {
                        Pick(st, st.Highlight);
                        continue;
                    }
                }

                if (st.Player.IsBot)
                {
                    st.BotDelay -= dt;
                    st.BotWander -= dt;
                    if (st.BotWander <= 0f && st.Hand.Count > 0)
                    {
                        st.BotWander = 0.3f;
                        st.Highlight = UnityEngine.Random.Range(0, st.Hand.Count);
                    }
                    if (st.BotDelay <= 0f) Pick(st, BotBrain.ChooseCard(st.Player, st.Hand));
                }
                else if (st.Player.Input is IMenuInput menu)
                {
                    menu.TickMenu();
                    if (menu.MenuHorizontal != 0 && st.Hand.Count > 0)
                    {
                        st.Highlight = (st.Highlight + menu.MenuHorizontal + st.Hand.Count) % st.Hand.Count;
                        Sfx.Play("ui", 0.5f, 0f);
                    }
                    if (menu.MenuConfirm) Pick(st, st.Highlight);
                }
            }

            if (Picks.All(p => p.Done))
            {
                pickDoneTimer += dt;
                if (pickDoneTimer > 0.7f)
                {
                    GameEvents.RaisePickPhaseEnded();
                    Picks.Clear();
                    if (practicePickReturn) ResumePracticeAfterPick();
                    else BeginRound();
                }
            }
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            // Each subsystem is isolated: an error in one is logged (once) instead of stalling the match.
            Safe("controllers", Controllers.Poll);
            Safe("rumble", Rumble.Update);
            Safe("scoreboard", UpdateScoreboardKey);
            Safe("map vote", UpdateLocalVoteInput);
            Safe("achievements", () => Achievements.Tick(udt));
            Safe("weather", () => Weather.Update(udt));
            Safe("steam", Spheres.Net.SteamIntegration.Update);
            if (!launchJoinChecked && State == GameState.MainMenu)
            {
                launchJoinChecked = true;
                Safe("steam join", Spheres.Net.SteamIntegration.ProcessLaunchJoin);
                Safe("workshop", Spheres.Net.SteamWorkshop.LoadSubscribed);
            }
            Safe("pause", HandlePause);
            if (NetClientMode)
            {
                StateTime += udt;
                if (State == GameState.CardPick && !Paused) UpdateClientPicks(udt);
                UpdateOverviewCamera(udt);
                UpdateCursor();
                return;
            }
            if (!Paused) StateTime += udt;

            switch (State)
            {
                case GameState.RoundIntro:
                    if (StateTime >= IntroDuration)
                    {
                        foreach (var p in Players) p.Frozen = false;
                        SetState(GameState.Fighting);
                    }
                    break;

                case GameState.Fighting:
                    if (Paused) break;
                    RoundTime += Time.deltaTime;
                    ModeUpdate(Time.deltaTime);
                    UpdateShrink();
                    CheckRoundOver();
                    break;

                case GameState.RoundEnd:
                    if (Paused) break;
                    RoundTime += Time.deltaTime; // keeps moving parts moving through the slow-mo
                    if (StateTime > 0.9f) Time.timeScale = 1f;
                    if (StateTime >= RoundEndDuration) AfterRound();
                    break;

                case GameState.CardPick:
                    if (!Paused) UpdatePicks(udt);
                    break;
            }

            UpdateOverviewCamera(udt);
            UpdateCursor();
        }

        void UpdateShrink()
        {
            if (Map == null) return;
            float after = Settings.borderShrinkAfter;
            if (after <= 0f) return;
            float t = (RoundTime - after) / Mathf.Max(1f, Settings.borderShrinkDuration);
            Map.SetShrink(Mathf.Clamp01(t));
        }

        void CheckRoundOver()
        {
            if (ModeCheckRoundOver()) return;
            var aliveTeams = Players.Where(p => p.IsAlive).Select(p => p.Team).Distinct().ToList();
            if (aliveTeams.Count <= 1)
            {
                EndRound(aliveTeams.Count == 1 ? aliveTeams[0] : -1);
                return;
            }
            if (Settings.roundTimeLimit > 0f && RoundTime >= Settings.roundTimeLimit)
            {
                // Time's up: the team with the most remaining health (as a fraction) wins.
                int best = -1;
                float bestHp = -1f;
                foreach (int t in aliveTeams)
                {
                    float hp = Players.Where(p => p.Team == t && p.IsAlive).Sum(p => p.Health / Mathf.Max(1f, p.MaxHealth));
                    if (hp > bestHp) { bestHp = hp; best = t; }
                }
                EndRound(best);
            }
        }

        void HandlePause()
        {
            // Poll pause buttons directly (player input isn't ticked while time is stopped), and keep
            // edge state current outside matches so a held Start doesn't pause the moment a match begins.
            var binds = InputBindings.Current;
            bool pressed = Input.GetKeyDown(KeyCode.Escape) || binds.KeyDown(GameAction.Pause);
            for (int j = 1; j <= ControlTypes.MaxGamepads; j++)
            {
                bool held = Controllers.IsConnected(j) && binds.PadHeld(GameAction.Pause, j, Controllers.Profile(j));
                if (held && !pausePrev[j]) pressed = true;
                pausePrev[j] = held;
            }
            if (!InMatch) return;
            if (pressed) SetPaused(!Paused);
        }

        bool launchJoinChecked;
        readonly HashSet<string> safeLogged = new HashSet<string>();

        /// <summary>Run a per-frame subsystem; log its first exception instead of breaking the frame.</summary>
        void Safe(string name, System.Action action)
        {
            try { action(); }
            catch (System.Exception e)
            {
                if (safeLogged.Add(name + e.GetType().Name)) Debug.LogError("[Spheres] Error in " + name + " (logged once): " + e);
            }
        }

        /// <summary>True while a local player holds the scoreboard button (Tab / View).</summary>
        public bool ScoreboardOpen { get; private set; }

        void UpdateScoreboardKey()
        {
            bool open = false;
            if (InMatch)
            {
                var binds = InputBindings.Current;
                foreach (var p in Players)
                {
                    if (!(p.Input is HumanInput h)) continue;
                    if (h.IsKeyboard ? binds.KeyHeld(GameAction.Scoreboard) : binds.PadHeld(GameAction.Scoreboard, h.Joystick, Controllers.Profile(h.Joystick)))
                        open = true;
                }
            }
            ScoreboardOpen = open;
        }

        void OnControllerChanged(int joystick, bool connected, string name)
        {
            Toasts.Show(connected ? "Controller " + joystick + " connected" : "Controller " + joystick + " disconnected",
                connected ? name + " (" + Controllers.DetectLayout(joystick) + " layout)" : "Reconnect it to keep playing", connected ? UIKit.Good : UIKit.Bad);
            // A local player lost their pad mid-match: pause so nobody gets a free kill.
            if (!connected && InMatch && !Paused && Players.Any(p => p.Input is HumanInput h && h.Joystick == joystick))
                SetPaused(true);
        }

        /// <summary>Overlay the options screen on top of the pause menu.</summary>
        public bool ShowOptionsOverlay;

        public void SetPaused(bool paused)
        {
            if (!InMatch) paused = false;
            Paused = paused;
            if (NetClientMode) return; // a client's pause menu is only a local overlay
            if (paused) Time.timeScale = 0f;
            else Time.timeScale = State == GameState.RoundEnd && StateTime < 0.9f ? 0.35f : 1f;
        }

        void UpdateOverviewCamera(float dt)
        {
            if (podiumCamera && Podium.Showing)
            {
                // Slow push-in on the winners.
                Podium.Update();
                var want = Podium.CameraPos + (Podium.CameraLook - Podium.CameraPos) * Mathf.Clamp01(StateTime * 0.03f);
                OverviewCamera.transform.position = Vector3.Lerp(OverviewCamera.transform.position, want, 1f - Mathf.Exp(-dt * 2f));
                OverviewCamera.transform.LookAt(Podium.CameraLook);
                return;
            }
            if (State == GameState.MainMenu) MenuShowcase.Ensure(this);
            if (OverviewCamera == null || !OverviewCamera.enabled || Map == null) return;
            orbitAngle += dt * (InMatch ? 6f : 9f);
            var b = Map.FullBounds;
            float radius = Mathf.Max(b.size.x, b.size.z) * 0.95f;
            Vector3 focus = b.center + Vector3.down * (b.size.y * 0.15f);
            if (InMatch)
            {
                // Follow the action in spectator mode.
                var alive = Players.Where(p => p.IsAlive).ToList();
                if (alive.Count > 0)
                {
                    Vector3 avg = Vector3.zero;
                    foreach (var p in alive) avg += p.Center;
                    focus = Vector3.Lerp(focus, avg / alive.Count, 0.5f);
                }
            }
            Vector3 offset = Quaternion.Euler(28f, orbitAngle, 0f) * new Vector3(0f, 0f, -radius);
            var target = focus + offset;
            OverviewCamera.transform.position = Vector3.Lerp(OverviewCamera.transform.position, target, 1f - Mathf.Exp(-dt * 3f));
            OverviewCamera.transform.rotation = Quaternion.LookRotation(focus - OverviewCamera.transform.position);
        }

        void UpdateCursor()
        {
            if (State == GameState.Editor) return;
            bool fighting = State == GameState.RoundIntro || State == GameState.Fighting || State == GameState.RoundEnd;
            bool keyboardHuman = Players.Any(p => p.Input is HumanInput h && h.IsKeyboard);
            bool lockIt = fighting && keyboardHuman && !Paused && !ScoreboardOpen && Application.isFocused;
            var mode = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !lockIt;
        }
    
        // ------------------------------------------------------------------ network client API

        public struct NetPlayerInfo
        {
            public int Index, Team;
            public string Name, Hat;
            public bool Bot, Local;
        }

        /// <summary>Network client: build the match described by the host.</summary>
        public void ClientStartMatch(MatchSettings rules, List<NetPlayerInfo> infos, MapData map)
        {
            Weather.Active = true;
            if (Editor.IsOpen) Editor.Close();
            ClearMatch();
            if (localSettingsBackup == null) localSettingsBackup = Settings;
            rules.Validate();
            rules.CopyPreferencesFrom(localSettingsBackup);
            Settings = rules;
            NetClientMode = true;
            editorTestMap = null;
            Array.Clear(TeamScores, 0, TeamScores.Length);
            RoundNumber = 0;
            MatchWinner = -1;
            LastRoundWinner = -1;

            foreach (var info in infos)
            {
                var control = info.Local ? localSettingsBackup.onlineControl : info.Bot ? ControlType.Bot : ControlType.Online;
                if (info.Local && (control == ControlType.Bot || control == ControlType.Online || control == ControlType.None)) control = ControlType.KeyboardMouse;
                var slot = new PlayerSlot { name = info.Name, team = info.Team, control = control, look = info.Local ? Profile.Current.Look(-1).Encode() : info.Hat };
                IPlayerInput input = info.Local ? new HumanInput(control) : null;
                var p = Player.Create(slot, info.Index, input);
                p.MakePuppet(info.Local);
                p.Frozen = true;
                Players.Add(p);
                if (input is HumanInput human) Cameras.Add(PlayerCamera.Create(p, human));
            }
            ActiveTeams.AddRange(Players.Select(p => p.Team).Distinct().OrderBy(t => t));
            LayoutCameras();
            if (map != null) ClientLoadMap(map);
            SetState(GameState.RoundIntro);
        }

        public void ClientLoadMap(MapData map)
        {
            Fx.Clear();
            ReplaceMap(map);
            CurrentMapId = "net";
            BuildModeObjects();
            foreach (var p in Players)
            {
                p.NetResetForRound();
                var look = Map.FullBounds.center - p.transform.position;
                look.y = 0f;
                if (p.Input is HumanInput h && look.sqrMagnitude > 0.01f) h.OnSpawn(p, look.normalized);
            }
        }

        public void ClientSetState(GameState state, float stateTime, int round, float roundTime, int lastWinner, int matchWinner, int[] scores, bool hostPaused)
        {
            if (state != State) Picks.RemoveAll(_ => state != GameState.CardPick);
            if (state == GameState.MatchEnd && State != GameState.MatchEnd) Achievements.RecordMatch(matchWinner, true);
            var oldState = State;
            State = state;
            StateTime = stateTime;
            RoundNumber = round;
            RoundTime = roundTime;
            LastRoundWinner = lastWinner;
            MatchWinner = matchWinner;
            HostPaused = hostPaused;
            for (int i = 0; i < TeamScores.Length && i < scores.Length; i++) TeamScores[i] = scores[i];
            OnStateChanged(oldState, state);
        }

        /// <summary>True on a client while the host has the game paused.</summary>
        public bool HostPaused { get; private set; }

        public void ClientSetPicks(List<PickState> picks, bool starting)
        {
            // Keep the local player's own highlight so navigation doesn't jitter.
            foreach (var st in picks)
            {
                var old = Picks.FirstOrDefault(o => o.Player == st.Player);
                bool sameHand = old != null && old.Hand.Count == st.Hand.Count && old.Hand.Select(c => c.Id).SequenceEqual(st.Hand.Select(c => c.Id));
                st.DealtTime = sameHand ? old.DealtTime : Time.unscaledTime;
                if (!sameHand && st.Player.Input is HumanInput) Sfx.Play("deal", 0.6f, 0f);
                if (sameHand && st.Player.Input is HumanInput && old.PicksRemaining == st.PicksRemaining)
                    st.Highlight = old.Highlight;
            }
            Picks.Clear();
            Picks.AddRange(picks);
            StartingPickPhase = starting;
        }

        void UpdateClientPicks(float dt)
        {
            foreach (var st in Picks)
            {
                if (st.Done) continue;
                if (Settings.pickTimer > 0f) st.TimeLeft = Mathf.Max(0f, st.TimeLeft - dt);
                if (!(st.Player.Input is IMenuInput menu)) continue;
                menu.TickMenu();
                if (menu.MenuHorizontal != 0 && st.Hand.Count > 0)
                {
                    st.Highlight = (st.Highlight + menu.MenuHorizontal + st.Hand.Count) % st.Hand.Count;
                    Sfx.Play("ui", 0.5f, 0f);
                    Net.SendPickHighlight(st.Player.Index, st.Highlight);
                }
                if (menu.MenuConfirm) Pick(st, st.Highlight);
            }
        }

        /// <summary>Leave client mode (disconnect, or host went back to the lobby).</summary>
        public void ClientExitMatch(GameState next)
        {
            Weather.Active = false;
            Weather.Clear();
            ClearMatch();
            if (localSettingsBackup != null)
            {
                localSettingsBackup.CopyPreferencesFrom(Settings);
                Settings = localSettingsBackup;
                localSettingsBackup = null;
            }
            NetClientMode = false;
            HostPaused = false;
            SetState(next);
            ShowMenuBackdrop();
            Net.ClearBulletViews();
        }

        /// <summary>Host: a client dropped out mid-match, so a bot takes over its players.</summary>
        public void ReplaceRemoteWithBot(int owner)
        {
            foreach (var p in Players)
            {
                if (p.Input is Net.NetworkInput ni && ni.Owner == owner)
                {
                    var bot = new BotBrain(BotDifficulty.Normal);
                    bot.OnSpawn(p, p.AimDirection);
                    p.Input = bot;
                    p.Slot.control = ControlType.Bot;
                    p.DisplayName += " (bot)";
                }
            }
        }
    }
}
