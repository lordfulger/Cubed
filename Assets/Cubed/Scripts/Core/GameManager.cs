using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    public enum GameState { MainMenu, Setup, CardLibrary, Mods, Editor, RoundIntro, Fighting, RoundEnd, CardPick, MatchEnd }

    /// <summary>One player's progress through a card pick phase.</summary>
    public class PickState
    {
        public Player Player;
        public int PicksRemaining;
        public int PicksTotal;
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
    public class GameManager : MonoBehaviour
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

        public Camera OverviewCamera { get; private set; }
        public MapEditor Editor { get; private set; }

        MapData editorTestMap;
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

            ContentLoader.LoadAll();
            MapRegistry.RefreshUserMaps();

            gameObject.AddComponent<CubedUI>();
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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ menu states

        public void SetState(GameState state)
        {
            State = state;
            StateTime = 0f;
        }

        /// <summary>Switch between menu screens (MainMenu, Setup, CardLibrary, Mods).</summary>
        public void GoToMenu(GameState menu)
        {
            if (InMatch || State == GameState.Editor) ReturnToMenu();
            SetState(menu);
        }

        public void ReturnToMenu()
        {
            ClearMatch();
            if (Editor.IsOpen) Editor.Close();
            editorTestMap = null;
            SetState(GameState.MainMenu);
            ShowMenuBackdrop();
        }

        void ShowMenuBackdrop()
        {
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
            Settings.Validate();
            Sfx.Volume = Settings.volume;
            Settings.Save();
        }

        // ------------------------------------------------------------------ match lifecycle

        /// <summary>Start a match with the current settings. testMap overrides the map (editor test play).</summary>
        public void StartMatch(MapData testMap = null)
        {
            Settings.Validate();
            string error = Settings.CheckPlayable();
            if (error != null)
            {
                Debug.LogWarning("[Cubed] " + error);
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
                IPlayerInput input = slot.control == ControlType.Bot ? new BotBrain(slot.difficulty) : (IPlayerInput)new HumanInput(slot.control);
                var p = Player.Create(slot.Clone(), index++, input);
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
            Bullet.ClearAll();
            Fx.Clear();
            foreach (var p in Players) if (p != null) Destroy(p.gameObject);
            foreach (var c in Cameras) if (c != null) Destroy(c.gameObject);
            Players.Clear();
            Cameras.Clear();
            ActiveTeams.Clear();
            Picks.Clear();
            CubedHud.Clear();
        }

        void LayoutCameras()
        {
            int n = Cameras.Count;
            Rect[] quads =
            {
                new Rect(0f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                new Rect(0f, 0f, 0.5f, 0.5f), new Rect(0.5f, 0f, 0.5f, 0.5f),
            };
            for (int i = 0; i < n; i++)
            {
                Rect r;
                if (n == 1) r = new Rect(0f, 0f, 1f, 1f);
                else if (n == 2) r = i == 0 ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0.5f, 0f, 0.5f, 1f);
                else r = quads[Mathf.Min(i, 3)];
                Cameras[i].SetViewport(r);
                Cameras[i].Cam.depth = 1 + i;
                Cameras[i].Cam.fieldOfView = n == 2 ? 75f : 70f;
            }
            OverviewCamera.enabled = n == 0 || n == 3;
            OverviewCamera.rect = n == 3 ? quads[3] : new Rect(0f, 0f, 1f, 1f);
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
                if (!first && Settings.mapRotation == MapRotation.Cycle && all.Count > 0)
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
            Bullet.ClearAll();
            Fx.Clear();
            Map.SetShrink(0f);
            SpawnPlayers();
            foreach (var p in Players) p.Frozen = true;
            RoundTime = 0f;
            SetState(GameState.RoundIntro);
            GameEvents.RaiseRoundStarted(RoundNumber);
        }

        void EndRound(int winner)
        {
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
            if (w >= 0 && TeamScores[w] >= Settings.roundsToWin)
            {
                MatchWinner = w;
                foreach (var p in Players) p.Frozen = true;
                Bullet.ClearAll();
                SetState(GameState.MatchEnd);
                Sfx.Play("win", 1f, 0f);
                GameEvents.RaiseMatchEnded(w);
                return;
            }

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
            if (st.Hand.Count == 0) st.PicksRemaining = 0;
        }

        /// <summary>Pick a card from a hand (also called by the UI for mouse clicks).</summary>
        public void Pick(PickState st, int index)
        {
            if (st == null || st.Done || State != GameState.CardPick) return;
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
                    BeginRound();
                }
            }
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            HandlePause();
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
                    UpdateShrink();
                    CheckRoundOver();
                    break;

                case GameState.RoundEnd:
                    if (Paused) break;
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
            if (!InMatch) return;
            bool pressed = Input.GetKeyDown(KeyCode.Escape);
            for (int j = 1; j <= 4 && !pressed; j++) pressed = Input.GetKeyDown(SafeInput.JoyButton(j, 7));
            if (pressed) SetPaused(!Paused);
        }

        public void SetPaused(bool paused)
        {
            if (!InMatch) paused = false;
            Paused = paused;
            if (paused) Time.timeScale = 0f;
            else Time.timeScale = State == GameState.RoundEnd && StateTime < 0.9f ? 0.35f : 1f;
        }

        void UpdateOverviewCamera(float dt)
        {
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
            bool lockIt = fighting && keyboardHuman && !Paused && Application.isFocused;
            var mode = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !lockIt;
        }
    }
}
