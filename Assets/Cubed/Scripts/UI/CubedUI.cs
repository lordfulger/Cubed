using System;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>Menus: main menu, match setup, card library, mods screen and the pause menu.</summary>
    public class CubedUI : MonoBehaviour
    {
        GameManager gm;
        Vector2 rulesScroll, playersScroll, cardsScroll, modsScroll;
        string setupError;

        static readonly string[] controlNames = { "Keyboard + Mouse", "Gamepad 1", "Gamepad 2", "Gamepad 3", "Gamepad 4", "Bot" };
        static readonly ControlType[] controlValues = { ControlType.KeyboardMouse, ControlType.Gamepad1, ControlType.Gamepad2, ControlType.Gamepad3, ControlType.Gamepad4, ControlType.Bot };
        static readonly string[] difficultyNames = Enum.GetNames(typeof(BotDifficulty));
        static readonly string[] pickRuleNames = { "Losers pick", "Everyone picks", "Winners pick", "No cards" };
        static readonly string[] rotationNames = { "Same map", "Cycle maps", "Random map" };

        void Awake() { gm = GetComponent<GameManager>(); }

        void OnGUI()
        {
            if (gm == null) return;
            UIKit.Begin();
            GUI.depth = 0;

            switch (gm.State)
            {
                case GameState.MainMenu: DrawMainMenu(); break;
                case GameState.Setup: DrawSetup(); break;
                case GameState.CardLibrary: DrawCardLibrary(); break;
                case GameState.Mods: DrawMods(); break;
                case GameState.Editor: gm.Editor.DrawGUI(); break;
                default: CubedHud.Draw(gm); break;
            }
            if (gm.Paused) DrawPause();

            // Mod overlays draw in raw pixel coordinates.
            GUI.matrix = Matrix4x4.identity;
            foreach (var d in CubedAPI.GuiDrawers.ToArray())
            {
                try { d(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // ------------------------------------------------------------------ main menu

        void DrawMainMenu()
        {
            var s = UIKit.Size;
            UIKit.Fill(new Rect(0f, 0f, 460f, s.y), new Color(0.05f, 0.06f, 0.09f, 0.82f));
            var title = new GUIStyle(UIKit.Huge) { alignment = TextAnchor.MiddleLeft, fontSize = 96 };
            UIKit.Shadowed(new Rect(50f, 90f, 400f, 110f), "CUBED", title, Color.white);
            UIKit.Text(new Rect(54f, 190f, 380f, 24f), "Fight. Pick cards. Get weird.", UIKit.Label, UIKit.Muted);

            float y = 260f;
            const float w = 340f, h = 50f, gap = 12f;
            if (UIKit.Btn(new Rect(50f, y, w, h), "Play")) { setupError = null; gm.GoToMenu(GameState.Setup); }
            y += h + gap;
            if (UIKit.Btn(new Rect(50f, y, w, h), "Quick Match vs Bot"))
            {
                QuickPreset(1, 1);
                gm.StartMatch();
            }
            y += h + gap;
            if (UIKit.Btn(new Rect(50f, y, w, h), "Map Editor")) gm.OpenEditor();
            y += h + gap;
            if (UIKit.Btn(new Rect(50f, y, w, h), "Card Library")) gm.GoToMenu(GameState.CardLibrary);
            y += h + gap;
            if (UIKit.Btn(new Rect(50f, y, w, h), "Mods (" + (CubedAPI.Mods.Count + ContentLoader.Sources.Count) + ")")) gm.GoToMenu(GameState.Mods);
            y += h + gap;
            foreach (var (label, action) in CubedAPI.MenuButtons.ToArray())
            {
                if (UIKit.Btn(new Rect(50f, y, w, 40f), label, true))
                {
                    try { action(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                y += 40f + 8f;
            }
            if (UIKit.Btn(new Rect(50f, s.y - 90f, w, 44f), "Quit")) Application.Quit();
            UIKit.Text(new Rect(54f, s.y - 36f, 380f, 20f), "v" + CubedAPI.GameVersion + (ContentLoader.BepInExLoaded ? "  -  BepInEx active" : ""), UIKit.Small, UIKit.Muted);
        }

        /// <summary>Replace the slot list with a quick layout: the first slot is the keyboard player.</summary>
        void QuickPreset(int teamSize, int enemyTeams, bool fillAllyBots = true)
        {
            var st = gm.Settings;
            string myName = st.slots.FirstOrDefault(x => x.IsHuman)?.name ?? "You";
            st.slots.Clear();
            st.slots.Add(new PlayerSlot { name = myName, control = ControlType.KeyboardMouse, team = 0 });
            if (fillAllyBots)
                for (int i = 1; i < teamSize; i++)
                    st.slots.Add(new PlayerSlot { name = "Ally " + i, control = ControlType.Bot, team = 0 });
            for (int t = 1; t <= enemyTeams; t++)
                for (int i = 0; i < teamSize; i++)
                    st.slots.Add(new PlayerSlot { name = Teams.GetName(t) + " Bot" + (teamSize > 1 ? " " + (i + 1) : ""), control = ControlType.Bot, team = t });
            gm.SaveSettings();
        }

        // ------------------------------------------------------------------ setup

        void DrawSetup()
        {
            var s = UIKit.Size;
            var st = gm.Settings;
            float W = Mathf.Min(1400f, s.x - 40f), H = Mathf.Min(820f, s.y - 40f);
            var panel = new Rect(s.x * 0.5f - W * 0.5f, s.y * 0.5f - H * 0.5f, W, H);
            UIKit.Fill(panel, UIKit.Bg);
            UIKit.Text(new Rect(panel.x + 24f, panel.y + 14f, 400f, 40f), "Match Setup", UIKit.Header, Color.white);

            // ---- Players column
            float colW = W * 0.47f;
            var left = new Rect(panel.x + 20f, panel.y + 64f, colW, H - 150f);
            UIKit.Fill(left, UIKit.Panel);
            UIKit.Text(new Rect(left.x + 12f, left.y + 8f, 200f, 28f), "Players & Teams", UIKit.Header, UIKit.Accent);

            float px = left.x + 12f, py = left.y + 44f, bw = (colW - 24f - 30f) / 4f;
            if (UIKit.Btn(new Rect(px, py, bw, 32f), "1 v 1", true)) QuickPreset(1, 1);
            if (UIKit.Btn(new Rect(px + (bw + 10f), py, bw, 32f), "2 v 2", true)) QuickPreset(2, 1);
            if (UIKit.Btn(new Rect(px + (bw + 10f) * 2f, py, bw, 32f), "3 v 3", true)) QuickPreset(3, 1);
            if (UIKit.Btn(new Rect(px + (bw + 10f) * 3f, py, bw, 32f), "4-way FFA", true)) QuickPreset(1, 3);
            py += 44f;

            var view = new Rect(left.x + 8f, py, left.width - 16f, left.yMax - py - 54f);
            float rowH = 44f;
            var content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(view.height, st.slots.Count * rowH));
            playersScroll = GUI.BeginScrollView(view, playersScroll, content);
            int remove = -1;
            for (int i = 0; i < st.slots.Count; i++)
            {
                var slot = st.slots[i];
                float y = i * rowH;
                float cw = content.width;
                UIKit.Fill(new Rect(0f, y, cw, rowH - 6f), new Color(1f, 1f, 1f, 0.04f));
                UIKit.Fill(new Rect(0f, y, 5f, rowH - 6f), Teams.GetColor(slot.team));
                slot.name = GUI.TextField(new Rect(12f, y + 4f, cw * 0.26f, rowH - 14f), slot.name ?? "", 16, UIKit.Field);
                float x = 18f + cw * 0.26f;
                int ci = Mathf.Max(0, Array.IndexOf(controlValues, slot.control));
                if (UIKit.Btn(new Rect(x, y + 4f, cw * 0.27f, rowH - 14f), controlNames[ci], true))
                    slot.control = controlValues[(ci + 1) % controlValues.Length];
                x += cw * 0.27f + 6f;
                if (UIKit.Btn(new Rect(x, y + 4f, cw * 0.16f, rowH - 14f), "<color=#" + ColorUtility.ToHtmlStringRGB(Teams.GetColor(slot.team)) + ">" + Teams.GetName(slot.team) + "</color>", true))
                    slot.team = (slot.team + 1) % Teams.Max;
                x += cw * 0.16f + 6f;
                if (slot.control == ControlType.Bot)
                {
                    if (UIKit.Btn(new Rect(x, y + 4f, cw * 0.17f, rowH - 14f), difficultyNames[(int)slot.difficulty], true))
                        slot.difficulty = (BotDifficulty)(((int)slot.difficulty + 1) % difficultyNames.Length);
                }
                if (UIKit.Btn(new Rect(cw - 36f, y + 4f, 32f, rowH - 14f), "X", true)) remove = i;
            }
            GUI.EndScrollView();
            if (remove >= 0) st.slots.RemoveAt(remove);

            if (st.slots.Count < 8)
            {
                var addRect = new Rect(left.x + 12f, left.yMax - 46f, 180f, 36f);
                if (UIKit.Btn(addRect, "+ Add Bot", true))
                    st.slots.Add(new PlayerSlot { name = "Bot " + (st.slots.Count + 1), control = ControlType.Bot, team = NextTeam() });
                if (UIKit.Btn(new Rect(addRect.xMax + 10f, addRect.y, 180f, 36f), "+ Add Gamepad", true))
                {
                    var used = st.slots.Select(x => x.control).ToList();
                    var pad = controlValues.Skip(1).Take(4).FirstOrDefault(c => !used.Contains(c));
                    if (pad == ControlType.None) pad = ControlType.Gamepad1;
                    st.slots.Add(new PlayerSlot { name = "Player " + (st.slots.Count + 1), control = pad, team = NextTeam() });
                }
            }
            UIKit.Text(new Rect(left.x + 390f, left.yMax - 46f, left.width - 400f, 36f), "Click control/team/difficulty to cycle", UIKit.Small, UIKit.Muted);

            // ---- Rules column
            var right = new Rect(left.xMax + 20f, left.y, W - colW - 60f, left.height);
            UIKit.Fill(right, UIKit.Panel);
            var rview = new Rect(right.x + 8f, right.y + 8f, right.width - 16f, right.height - 16f);
            var rcontent = new Rect(0f, 0f, rview.width - 20f, 1060f);
            rulesScroll = GUI.BeginScrollView(rview, rulesScroll, rcontent);
            DrawRules(rcontent.width);
            GUI.EndScrollView();

            // ---- Footer
            float fy = panel.yMax - 70f;
            if (UIKit.Btn(new Rect(panel.x + 20f, fy, 200f, 50f), "Back"))
            {
                gm.SaveSettings();
                gm.GoToMenu(GameState.MainMenu);
            }
            setupError = st.CheckPlayable();
            if (setupError != null) UIKit.Text(new Rect(panel.x + 240f, fy, W - 520f, 50f), setupError, UIKit.LabelCenter, UIKit.Bad);
            GUI.enabled = setupError == null;
            if (UIKit.Btn(new Rect(panel.xMax - 280f, fy, 260f, 50f), "<b>START MATCH</b>")) gm.StartMatch();
            GUI.enabled = true;
        }

        int NextTeam()
        {
            var counts = new int[Teams.Max];
            foreach (var s in gm.Settings.slots) counts[s.team]++;
            int used = counts.Count(c => c > 0);
            if (used < 2) return counts[0] == 0 ? 0 : 1;
            int best = 0;
            for (int t = 0; t < Teams.Max; t++)
                if (counts[t] > 0 && counts[t] < counts[best]) best = t;
            return best;
        }

        void DrawRules(float w)
        {
            var st = gm.Settings;
            float y = 0f;
            const float h = 36f;
            void Section(string name)
            {
                y += 8f;
                UIKit.Text(new Rect(4f, y, w, 30f), name, UIKit.Header, UIKit.Accent);
                y += 34f;
            }
            Rect Row() { var r = new Rect(4f, y, w - 8f, h); y += h + 4f; return r; }

            Section("Match");
            st.roundsToWin = UIKit.StepperInt(Row(), "Rounds to win", st.roundsToWin, 1, 20);
            st.roundTimeLimit = UIKit.Stepper(Row(), "Round time limit", st.roundTimeLimit, 30f, 0f, 600f, "0", "s", "Off");
            st.friendlyFire = UIKit.CheckRow(Row(), "Friendly fire", st.friendlyFire);

            Section("Cards");
            st.cardsPerHand = UIKit.StepperInt(Row(), "Cards shown per pick", st.cardsPerHand, 1, 10);
            st.picksPerRound = UIKit.StepperInt(Row(), "Picks after each round", st.picksPerRound, 0, 5);
            st.startingPicks = UIKit.StepperInt(Row(), "Picks before round 1", st.startingPicks, 0, 5, "None");
            st.pickTimer = UIKit.Stepper(Row(), "Pick timer", st.pickTimer, 5f, 0f, 120f, "0", "s", "Off");
            st.pickRule = (PickRule)UIKit.Cycle(Row(), "Who picks", (int)st.pickRule, pickRuleNames);
            st.rerollBetweenPicks = UIKit.CheckRow(Row(), "New hand for each pick", st.rerollBetweenPicks);
            int enabled = CardRegistry.All.Count(c => CardRegistry.IsEnabled(c, st));
            var libRow = Row();
            UIKit.Text(libRow, "Cards enabled: " + enabled + " / " + CardRegistry.All.Count, UIKit.Label, Color.white);
            if (UIKit.Btn(new Rect(libRow.xMax - 220f, libRow.y + 2f, 220f, libRow.height - 4f), "Card Library", true)) gm.GoToMenu(GameState.CardLibrary);

            Section("Map");
            var maps = MapRegistry.All;
            int mi = Mathf.Max(0, maps.ToList().FindIndex(m => m.Id == st.mapId));
            string[] mapNames = maps.Select(m => m.Data.name + (m.Source != "Cubed" ? " (" + m.Source + ")" : "")).ToArray();
            if (mapNames.Length > 0)
            {
                int newMi = UIKit.Cycle(Row(), "Map", mi, mapNames);
                if (newMi != mi || st.mapId != maps[newMi].Id)
                {
                    st.mapId = maps[newMi].Id;
                    gm.PreviewMap(st.mapId);
                }
            }
            st.mapRotation = (MapRotation)UIKit.Cycle(Row(), "Between rounds", (int)st.mapRotation, rotationNames);

            Section("Borders & Falling");
            st.damagingBorders = UIKit.CheckRow(Row(), "Damaging borders", st.damagingBorders);
            if (st.damagingBorders) st.borderDamage = UIKit.Stepper(Row(), "   Border damage", st.borderDamage, 5f, 5f, 100f);
            st.fallDamage = UIKit.CheckRow(Row(), "Fall damage (block to negate)", st.fallDamage);
            if (st.fallDamage)
            {
                st.bottomDamage = UIKit.Stepper(Row(), "   Bottom border damage", st.bottomDamage, 5f, 5f, 100f);
                st.landingDamage = UIKit.CheckRow(Row(), "   Hard landing damage", st.landingDamage);
            }
            st.borderShrinkAfter = UIKit.Stepper(Row(), "Borders close in after", st.borderShrinkAfter, 15f, 0f, 300f, "0", "s", "Never");
            if (gm.Map != null) gm.Map.SetBorderStyle(st.damagingBorders, st.fallDamage);

            Section("Controls & Audio");
            st.mouseSensitivity = UIKit.Stepper(Row(), "Mouse sensitivity", st.mouseSensitivity, 0.2f, 0.2f, 10f, "0.0");
            st.gamepadSensitivity = UIKit.Stepper(Row(), "Gamepad look speed", st.gamepadSensitivity, 20f, 40f, 400f);
            st.invertY = UIKit.CheckRow(Row(), "Invert Y", st.invertY);
            float vol = UIKit.Stepper(Row(), "Volume", st.volume * 100f, 10f, 0f, 100f, "0", "%");
            st.volume = vol / 100f;
            Sfx.Volume = st.volume;
        }

        // ------------------------------------------------------------------ card library

        void DrawCardLibrary()
        {
            var s = UIKit.Size;
            var st = gm.Settings;
            var panel = new Rect(20f, 20f, s.x - 40f, s.y - 40f);
            UIKit.Fill(panel, UIKit.Bg);
            var all = CardRegistry.All;
            int enabled = all.Count(c => CardRegistry.IsEnabled(c, st));
            UIKit.Text(new Rect(panel.x + 24f, panel.y + 14f, 600f, 40f), "Card Library  (" + enabled + " / " + all.Count + " enabled)", UIKit.Header, Color.white);
            UIKit.Text(new Rect(panel.x + 24f, panel.y + 50f, 800f, 24f), "Click a card to enable or disable it for matches.", UIKit.Small, UIKit.Muted);

            if (UIKit.Btn(new Rect(panel.xMax - 420f, panel.y + 16f, 190f, 38f), "Enable all", true)) st.disabledCards.Clear();
            if (UIKit.Btn(new Rect(panel.xMax - 220f, panel.y + 16f, 190f, 38f), "Disable all", true))
            {
                st.disabledCards.Clear();
                st.disabledCards.AddRange(all.Select(c => c.Id));
            }

            const float cw = 190f, ch = 280f, gap = 16f;
            var view = new Rect(panel.x + 16f, panel.y + 84f, panel.width - 32f, panel.height - 160f);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((view.width - 20f + gap) / (cw + gap)));
            int rows = Mathf.CeilToInt(all.Count / (float)perRow);
            var content = new Rect(0f, 0f, view.width - 20f, rows * (ch + gap) + 20f);
            cardsScroll = GUI.BeginScrollView(view, cardsScroll, content);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                var r = new Rect(10f + (i % perRow) * (cw + gap), 10f + (i / perRow) * (ch + gap), cw, ch);
                bool on = CardRegistry.IsEnabled(c, st);
                UIKit.DrawCard(r, c, false, on ? 1f : 0.35f);
                if (!on) UIKit.Shadowed(new Rect(r.x, r.center.y - 15f, r.width, 30f), "DISABLED", UIKit.HeaderCenter, UIKit.Bad);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (on) st.disabledCards.Add(c.Id);
                    else st.disabledCards.Remove(c.Id);
                    Sfx.Play("ui", 0.4f, 0f);
                }
            }
            GUI.EndScrollView();

            if (UIKit.Btn(new Rect(panel.x + 20f, panel.yMax - 64f, 200f, 46f), "Back"))
            {
                gm.SaveSettings();
                gm.GoToMenu(GameState.MainMenu);
            }
            if (UIKit.Btn(new Rect(panel.xMax - 240f, panel.yMax - 64f, 220f, 46f), "Match Setup"))
            {
                gm.SaveSettings();
                gm.GoToMenu(GameState.Setup);
            }
        }

        // ------------------------------------------------------------------ mods

        void DrawMods()
        {
            var s = UIKit.Size;
            var panel = new Rect(s.x * 0.5f - Mathf.Min(1100f, s.x - 40f) * 0.5f, 20f, Mathf.Min(1100f, s.x - 40f), s.y - 40f);
            UIKit.Fill(panel, UIKit.Bg);
            UIKit.Text(new Rect(panel.x + 24f, panel.y + 14f, 600f, 40f), "Mods", UIKit.Header, Color.white);

            var view = new Rect(panel.x + 16f, panel.y + 64f, panel.width - 32f, panel.height - 140f);
            var content = new Rect(0f, 0f, view.width - 20f, 1400f);
            modsScroll = GUI.BeginScrollView(view, modsScroll, content);
            float y = 0f, w = content.width;

            string bep = ContentLoader.BepInExLoaded ? "<color=#7dff8a>BepInEx is loaded.</color>" : "<color=#ffb070>BepInEx is not loaded</color> - code mods (DLLs) need BepInEx 5. Data mods (JSON) still work.";
            UIKit.Text(new Rect(0f, y, w, 26f), bep, UIKit.Label, Color.white);
            y += 30f;
            var plugins = ContentLoader.BepInExPluginPath;
            UIKit.Text(new Rect(0f, y, w, 22f), "Plugin folder: " + (plugins ?? "(none)"), UIKit.Small, UIKit.Muted);
            y += 34f;

            UIKit.Text(new Rect(0f, y, w, 30f), "Code mods", UIKit.Header, UIKit.Accent);
            y += 34f;
            if (CubedAPI.Mods.Count == 0)
            {
                UIKit.Text(new Rect(10f, y, w, 22f), "None registered. Plugins call CubedAPI.RegisterMod(...) to appear here.", UIKit.Small, UIKit.Muted);
                y += 26f;
            }
            foreach (var m in CubedAPI.Mods)
            {
                UIKit.Text(new Rect(10f, y, w, 24f), "<b>" + m.Name + "</b>  " + m.Version + (string.IsNullOrEmpty(m.Author) ? "" : "  by " + m.Author) + "   <color=#8890a8>" + m.Guid + "</color>", UIKit.Label, Color.white);
                y += 24f;
                if (!string.IsNullOrEmpty(m.Description))
                {
                    UIKit.Text(new Rect(24f, y, w - 24f, 20f), m.Description, UIKit.Small, UIKit.Muted);
                    y += 22f;
                }
            }
            y += 14f;

            UIKit.Text(new Rect(0f, y, w, 30f), "Data content (JSON)", UIKit.Header, UIKit.Accent);
            y += 34f;
            if (ContentLoader.Sources.Count == 0)
            {
                UIKit.Text(new Rect(10f, y, w, 22f), "No *.cubedcards.json / *.cubedmap.json / *.cubedblocks.json files found.", UIKit.Small, UIKit.Muted);
                y += 26f;
            }
            foreach (var src in ContentLoader.Sources)
            {
                UIKit.Text(new Rect(10f, y, w, 22f), src.Folder, UIKit.Small, Color.white);
                y += 22f;
                UIKit.Text(new Rect(24f, y, w, 22f), src.Cards + " cards, " + src.Maps + " maps, " + src.Blocks + " block types", UIKit.Small, UIKit.Good);
                y += 22f;
                foreach (var err in src.Errors)
                {
                    UIKit.Text(new Rect(24f, y, w - 24f, 20f), err, UIKit.Small, UIKit.Bad);
                    y += 20f;
                }
                y += 6f;
            }
            y += 14f;

            UIKit.Text(new Rect(0f, y, w, 30f), "Content by source", UIKit.Header, UIKit.Accent);
            y += 34f;
            foreach (var g in CardRegistry.All.GroupBy(c => c.Source))
            {
                UIKit.Text(new Rect(10f, y, w, 22f), g.Key + ": " + g.Count() + " cards", UIKit.Small, Color.white);
                y += 22f;
            }
            foreach (var g in MapRegistry.All.GroupBy(m => m.Source))
            {
                UIKit.Text(new Rect(10f, y, w, 22f), g.Key + ": " + g.Count() + " maps", UIKit.Small, Color.white);
                y += 22f;
            }
            y += 14f;
            UIKit.Text(new Rect(0f, y, w, 30f), "Folders scanned", UIKit.Header, UIKit.Accent);
            y += 34f;
            foreach (var f in ContentLoader.Folders.Distinct())
            {
                UIKit.Text(new Rect(10f, y, w, 22f), f + (System.IO.Directory.Exists(f) ? "" : "  (missing)"), UIKit.Small, UIKit.Muted);
                y += 22f;
            }
            GUI.EndScrollView();

            float by = panel.yMax - 64f;
            if (UIKit.Btn(new Rect(panel.x + 20f, by, 200f, 46f), "Back")) gm.GoToMenu(GameState.MainMenu);
            if (UIKit.Btn(new Rect(panel.x + 240f, by, 240f, 46f), "Reload content"))
            {
                ContentLoader.LoadAll();
                MapRegistry.RefreshUserMaps();
            }
            if (UIKit.Btn(new Rect(panel.x + 500f, by, 280f, 46f), "Open content folder"))
                Application.OpenURL("file://" + ContentLoader.UserContentFolder);
        }

        // ------------------------------------------------------------------ pause

        void DrawPause()
        {
            var s = UIKit.Size;
            UIKit.Fill(new Rect(0f, 0f, s.x, s.y), new Color(0f, 0f, 0f, 0.55f));
            float w = 460f, h = 470f;
            var r = new Rect(s.x * 0.5f - w * 0.5f, s.y * 0.5f - h * 0.5f, w, h);
            UIKit.Fill(r, UIKit.Panel);
            UIKit.Text(new Rect(r.x, r.y + 16f, w, 40f), "Paused", UIKit.Title, Color.white);
            float y = r.y + 80f;
            var st = gm.Settings;
            st.mouseSensitivity = UIKit.Stepper(new Rect(r.x + 20f, y, w - 40f, 36f), "Mouse sensitivity", st.mouseSensitivity, 0.2f, 0.2f, 10f, "0.0");
            y += 42f;
            float vol = UIKit.Stepper(new Rect(r.x + 20f, y, w - 40f, 36f), "Volume", st.volume * 100f, 10f, 0f, 100f, "0", "%");
            st.volume = vol / 100f;
            Sfx.Volume = st.volume;
            y += 42f;
            st.invertY = UIKit.CheckRow(new Rect(r.x + 20f, y, w - 40f, 36f), "Invert Y", st.invertY);
            y += 56f;
            if (UIKit.Btn(new Rect(r.x + 40f, y, w - 80f, 46f), "Resume")) { gm.SaveSettings(); gm.SetPaused(false); }
            y += 56f;
            if (UIKit.Btn(new Rect(r.x + 40f, y, w - 80f, 46f), "Restart Match")) gm.Rematch();
            y += 56f;
            if (gm.TestingFromEditor)
            {
                if (UIKit.Btn(new Rect(r.x + 40f, y, w - 80f, 46f), "Back to Editor")) gm.BackToEditor();
                y += 56f;
            }
            if (UIKit.Btn(new Rect(r.x + 40f, y, w - 80f, 46f), "Quit to Menu")) { gm.SaveSettings(); gm.ReturnToMenu(); }
        }
    }
}
