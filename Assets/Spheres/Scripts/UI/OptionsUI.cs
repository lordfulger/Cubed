using System;
using UnityEngine;

namespace Spheres
{
    /// <summary>The Options screen (from the main menu or the pause menu).</summary>
    public static class OptionsUI
    {
        static readonly string[] tabs = { "Gameplay", "Video", "Audio", "Controls", "Accessibility" };
        static int tab;
        static Vector2 scroll;
        static GameAction? capturing;
        static bool captureSecondary;
        static int captureFrame;
        static int pendingResolution = int.MinValue, pendingWindowMode = -1;

        static readonly GameAction[] actions = (GameAction[])Enum.GetValues(typeof(GameAction));
        static readonly PadButton[] padButtons = (PadButton[])Enum.GetValues(typeof(PadButton));

        /// <summary>Draws the screen; returns true when the player closes it.</summary>
        public static bool Draw(GameManager gm)
        {
            var s = UIKit.Size;
            float W = Mathf.Min(1100f, s.x - 40f), H = Mathf.Min(780f, s.y - 40f);
            var panel = new Rect(s.x * 0.5f - W * 0.5f, s.y * 0.5f - H * 0.5f, W, H);
            UIKit.Fill(new Rect(0f, 0f, s.x, s.y), new Color(0f, 0f, 0f, 0.4f));
            UIKit.Fill(panel, UIKit.Bg);
            UIKit.Text(new Rect(panel.x + 24f, panel.y + 14f, 300f, 40f), "Options", UIKit.Header, Color.white);

            float tw = (W - 48f - 8f * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                if (UIKit.Toggle(new Rect(panel.x + 24f + i * (tw + 8f), panel.y + 60f, tw, 36f), tabs[i], tab == i, true))
                {
                    tab = i;
                    scroll = Vector2.zero;
                    capturing = null;
                }

            var view = new Rect(panel.x + 16f, panel.y + 110f, W - 32f, H - 190f);
            var content = new Rect(0f, 0f, view.width - 20f, 900f);
            scroll = GUI.BeginScrollView(view, scroll, content);
            float y = 6f;
            Rect Row(float h = 36f) { var r = new Rect(8f, y, content.width - 16f, h); y += h + 6f; return r; }
            void Header(string text) { y += 6f; UIKit.Text(new Rect(8f, y, content.width, 28f), text, UIKit.Header, UIKit.Accent); y += 34f; }

            var o = Options.Current;
            var st = gm.Settings;
            switch (tab)
            {
                case 0:
                    Header("Language");
                    {
                        var langs = Loc.Languages;
                        var langNames = new string[langs.Count + 1];
                        langNames[0] = "Automatic (system)";
                        for (int i = 0; i < langs.Count; i++) langNames[i + 1] = langs[i].Name;
                        int cur = 0;
                        for (int i = 0; i < langs.Count; i++) if (langs[i].Code == o.language) cur = i + 1;
                        int pick = UIKit.Cycle(Row(), "Language", cur, langNames);
                        if (pick != cur)
                        {
                            o.language = pick == 0 ? "" : langs[pick - 1].Code;
                            Loc.SetLanguage(o.language);
                        }
                    }
                    Header("Camera & aim");
                    st.mouseSensitivity = UIKit.Stepper(Row(), "Mouse sensitivity", st.mouseSensitivity, 0.2f, 0.2f, 10f, "0.0");
                    st.gamepadSensitivity = UIKit.Stepper(Row(), "Gamepad look speed", st.gamepadSensitivity, 20f, 40f, 400f);
                    st.invertY = UIKit.CheckRow(Row(), "Invert Y", st.invertY);
                    o.fovOffset = UIKit.Stepper(Row(), "Field of view offset", o.fovOffset, 5f, -15f, 25f, "+0;-0;0", "°");
                    Header("HUD");
                    o.damageNumbers = UIKit.CheckRow(Row(), "Damage numbers", o.damageNumbers);
                    o.funnyDeathMessages = UIKit.CheckRow(Row(), "Funny death messages", o.funnyDeathMessages);
                    o.deckVisuals = UIKit.CheckRow(Row(), "Show decks on fighters (barrels, armor, auras)", o.deckVisuals);
                    if (AutoUpdater.Supported)
                    {
                        Header("Updates");
                        o.checkForUpdates = UIKit.CheckRow(Row(), "Check for updates at startup", o.checkForUpdates);
                        var u = AutoUpdater.Instance;
                        string status = u == null ? "" : u.State == AutoUpdater.UpdateState.UpToDate ? "Up to date (" + Application.version + ")"
                            : u.State == AutoUpdater.UpdateState.Available ? "Update available: " + u.LatestVersion
                            : u.State == AutoUpdater.UpdateState.Checking ? "Checking..." : u.State == AutoUpdater.UpdateState.Failed ? "Couldn't check: " + u.Error : "Version " + Application.version;
                        if (UIKit.Btn(Row(), "Check now   " + status, true) && u != null) { u.Dismissed = false; u.Check(); }
                    }
                    break;

                case 1:
                    Header("Display");
                    var res = Options.Resolutions;
                    var names = new string[res.Length + 1];
                    names[0] = "Desktop";
                    for (int i = 0; i < res.Length; i++) names[i + 1] = res[i].width + " x " + res[i].height;
                    int ri = pendingResolution != int.MinValue ? pendingResolution : o.resolutionIndex;
                    pendingResolution = UIKit.Cycle(Row(), "Resolution", Mathf.Clamp(ri + 1, 0, names.Length - 1), names) - 1;
                    pendingWindowMode = UIKit.Cycle(Row(), "Window mode", pendingWindowMode >= 0 ? pendingWindowMode : o.windowMode, Options.WindowModes);
                    if (UIKit.Btn(new Rect(content.width - 228f, y, 220f, 36f), "Apply display", true))
                    {
                        o.resolutionIndex = pendingResolution;
                        o.windowMode = pendingWindowMode;
                        o.Apply(true);
                    }
                    y += 46f;
                    o.vsync = UIKit.CheckRow(Row(), "V-Sync", o.vsync);
                    if (!o.vsync) o.fpsCap = Mathf.RoundToInt(UIKit.Stepper(Row(), "Frame rate cap", o.fpsCap, 30f, 0f, 360f, "0", " fps", "Unlimited"));
                    Header("Graphics");
                    o.quality = UIKit.Cycle(Row(), "Quality", o.quality, Options.QualityNames);
                    o.ambientParticles = UIKit.CheckRow(Row(), "Ambient particles", o.ambientParticles);
                    o.postFx = UIKit.CheckRow(Row(), "Post-processing (bloom, AO, fog)", o.postFx);
                    UIKit.Text(Row(24f), "Low: no shadows/post. Medium: bloom + grading. High: + ambient occlusion and fog. Ultra: max detail.", UIKit.Small, UIKit.Muted);
                    break;

                case 2:
                    Header("Volume");
                    st.volume = UIKit.Stepper(Row(), "Sound effects", st.volume * 100f, 10f, 0f, 100f, "0", "%") / 100f;
                    st.musicVolume = UIKit.Stepper(Row(), "Music", st.musicVolume * 100f, 10f, 0f, 100f, "0", "%") / 100f;
                    Sfx.Volume = st.volume;
                    Header("Music");
                    o.musicSource = UIKit.Cycle(Row(), "Songs", o.musicSource, new[] { "Spheres + your music", "Only Spheres' songs", "Only your music" });
                    o.showSongNames = UIKit.CheckRow(Row(), "Show song names", o.showSongNames);
                    var np = MusicPlayer.NowPlaying;
                    UIKit.Text(Row(), "Now playing: " + (np != null ? np.Name : "-") + "   (" + MusicPlayer.Entries.Count + " songs)", UIKit.Small, UIKit.Muted);
                    if (UIKit.Btn(Row(), "Next song", true)) MusicPlayer.Instance?.Skip();
                    if (UIKit.Btn(Row(), "Open my music folder", true)) FolderOpener.Open(MusicPlayer.CustomFolder);
                    break;

                case 3:
                    DrawControls(ref y, content.width);
                    break;

                case 4:
                    Header("Comfort");
                    o.screenShake = UIKit.Stepper(Row(), "Screen shake", o.screenShake * 100f, 25f, 0f, 150f, "0", "%") / 100f;
                    o.reduceFlashes = UIKit.CheckRow(Row(), "Reduce flashing lights", o.reduceFlashes);
                    o.rumble = UIKit.Stepper(Row(), "Controller rumble", o.rumble * 100f, 25f, 0f, 100f, "0", "%") / 100f;
                    if (!Rumble.Available) UIKit.Text(Row(24f), "Rumble needs the Input System package (see docs).", UIKit.Small, UIKit.Muted);
                    Header("Readability");
                    o.colorPalette = UIKit.Cycle(Row(), "Team colors", o.colorPalette, Options.PaletteNames);
                    o.uiScale = UIKit.Stepper(Row(), "Interface scale", o.uiScale * 100f, 10f, 70f, 150f, "0", "%") / 100f;
                    UIKit.Text(Row(24f), "Team color changes apply from the next match.", UIKit.Small, UIKit.Muted);
                    break;
            }
            GUI.EndScrollView();

            float by = panel.yMax - 64f;
            bool close = UIKit.Btn(new Rect(panel.x + 20f, by, 200f, 46f), "Back") || (capturing == null && UIKit.BackPressed());
            if (UIKit.Btn(new Rect(panel.xMax - 240f, by, 220f, 46f), "Reset controls", true))
                InputBindings.ResetToDefaults();
            if (close)
            {
                o.Apply(false);
                o.Save();
                InputBindings.Current.Save();
                gm.SaveSettings();
                capturing = null;
            }
            return close;
        }

        static void DrawControls(ref float y, float width)
        {
            var b = InputBindings.Current;
            UIKit.Text(new Rect(8f, y, width, 28f), "Keyboard & mouse", UIKit.Header, UIKit.Accent);
            y += 34f;

            if (capturing.HasValue && Time.frameCount > captureFrame + 1)
            {
                var key = InputBindings.PressedKey();
                if (key == KeyCode.Escape) capturing = null;
                else if (key != KeyCode.None)
                {
                    var kb = b.Key(capturing.Value);
                    if (captureSecondary) kb.secondary = key; else kb.primary = key;
                    capturing = null;
                    b.Save();
                }
            }

            foreach (var a in actions)
            {
                var kb = b.Key(a);
                var r = new Rect(8f, y, width - 16f, 34f);
                UIKit.Text(new Rect(r.x, r.y, 220f, r.height), Pretty(a), UIKit.Label, Color.white);
                for (int slot = 0; slot < 2; slot++)
                {
                    bool sec = slot == 1;
                    bool waiting = capturing == a && captureSecondary == sec;
                    string label = waiting ? "<color=#ffd75e>Press a key...</color>" : InputBindings.KeyName(sec ? kb.secondary : kb.primary);
                    if (UIKit.Btn(new Rect(r.x + 240f + slot * 200f, r.y, 190f, r.height), label, true))
                    {
                        capturing = a;
                        captureSecondary = sec;
                        captureFrame = Time.frameCount;
                    }
                }
                if (UIKit.Btn(new Rect(r.x + 650f, r.y, 70f, r.height), "Clear", true)) { kb.secondary = KeyCode.None; b.Save(); }
                y += 40f;
            }

            y += 10f;
            UIKit.Text(new Rect(8f, y, width, 28f), "Gamepad", UIKit.Header, UIKit.Accent);
            y += 34f;
            string[] layouts = { "Detect automatically", "Xbox layout", "PlayStation layout", "Switch layout" };
            b.ForcedLayout = UIKit.Cycle(new Rect(8f, y, width - 16f, 34f), "Button layout", b.ForcedLayout + 1, layouts) - 1;
            y += 42f;
            var profile = Controllers.Profile(1);
            foreach (var a in actions)
            {
                var pb = b.Pad(a);
                if (pb == null) continue;
                var r = new Rect(8f, y, width - 16f, 34f);
                UIKit.Text(new Rect(r.x, r.y, 220f, r.height), Pretty(a), UIKit.Label, Color.white);
                if (UIKit.Btn(new Rect(r.x + 240f, r.y, 190f, r.height), profile.Label(pb.primary), true)) pb.primary = NextPad(pb.primary);
                if (UIKit.Btn(new Rect(r.x + 440f, r.y, 190f, r.height), pb.secondary == PadButton.None ? "-" : profile.Label(pb.secondary), true)) pb.secondary = NextPad(pb.secondary);
                y += 40f;
            }
            UIKit.Text(new Rect(8f, y, width - 16f, 40f), "Move with the left stick and look with the right stick. Click a gamepad binding to cycle through the buttons.", UIKit.Wrap, UIKit.Muted);
            y += 46f;
            for (int j = 1; j <= ControlTypes.MaxGamepads; j++)
            {
                if (!Controllers.IsConnected(j)) continue;
                UIKit.Text(new Rect(8f, y, width - 16f, 22f), "Gamepad " + j + ": " + Controllers.Name(j) + "  (" + Controllers.Profile(j).Name + " layout)", UIKit.Small, Color.white);
                y += 24f;
            }
        }

        static PadButton NextPad(PadButton b) => padButtons[(Array.IndexOf(padButtons, b) + 1) % padButtons.Length];

        static string Pretty(GameAction a)
        {
            switch (a)
            {
                case GameAction.MoveForward: return "Move forward";
                case GameAction.MoveBack: return "Move back";
                case GameAction.MoveLeft: return "Move left";
                case GameAction.MoveRight: return "Move right";
                default: return a.ToString();
            }
        }
    }
}
