using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    public enum GameAction { MoveForward, MoveBack, MoveLeft, MoveRight, Jump, Fire, Block, Reload, Pause }

    /// <summary>Layout-independent gamepad buttons ("South" is A on Xbox, Cross on PlayStation, B on Switch).</summary>
    public enum PadButton { None, South, East, West, North, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, Select, Start, LeftStick, RightStick }

    public enum PadLayout { Xbox, PlayStation, Switch }

    /// <summary>Raw legacy-input indices for one controller family on one OS. Axes are 1-based (0 = none).</summary>
    public class PadProfile
    {
        public string Name;
        public PadLayout Layout;
        public int South, East, West, North, LB, RB, Select, Start, LS, RS;
        public int LX = 1, LY = 2, RX, RY, LT, RT, DX, DY;
        public int LTButton = -1, RTButton = -1;
        /// <summary>Some drivers report triggers resting at -1; we remap them to 0..1.</summary>
        public bool TriggersFromMinusOne;
        public bool InvertDY;

        public int ButtonIndex(PadButton b)
        {
            switch (b)
            {
                case PadButton.South: return South;
                case PadButton.East: return East;
                case PadButton.West: return West;
                case PadButton.North: return North;
                case PadButton.LeftShoulder: return LB;
                case PadButton.RightShoulder: return RB;
                case PadButton.Select: return Select;
                case PadButton.Start: return Start;
                case PadButton.LeftStick: return LS;
                case PadButton.RightStick: return RS;
                case PadButton.LeftTrigger: return LTButton;
                case PadButton.RightTrigger: return RTButton;
                default: return -1;
            }
        }

        public string Label(PadButton b)
        {
            switch (Layout)
            {
                case PadLayout.PlayStation:
                    switch (b)
                    {
                        case PadButton.South: return "Cross";
                        case PadButton.East: return "Circle";
                        case PadButton.West: return "Square";
                        case PadButton.North: return "Triangle";
                        case PadButton.LeftShoulder: return "L1";
                        case PadButton.RightShoulder: return "R1";
                        case PadButton.LeftTrigger: return "L2";
                        case PadButton.RightTrigger: return "R2";
                        case PadButton.Select: return "Share";
                        case PadButton.Start: return "Options";
                    }
                    break;
                case PadLayout.Switch:
                    switch (b)
                    {
                        case PadButton.South: return "B";
                        case PadButton.East: return "A";
                        case PadButton.West: return "Y";
                        case PadButton.North: return "X";
                        case PadButton.LeftShoulder: return "L";
                        case PadButton.RightShoulder: return "R";
                        case PadButton.LeftTrigger: return "ZL";
                        case PadButton.RightTrigger: return "ZR";
                        case PadButton.Select: return "Minus";
                        case PadButton.Start: return "Plus";
                    }
                    break;
                default:
                    switch (b)
                    {
                        case PadButton.South: return "A";
                        case PadButton.East: return "B";
                        case PadButton.West: return "X";
                        case PadButton.North: return "Y";
                        case PadButton.LeftShoulder: return "LB";
                        case PadButton.RightShoulder: return "RB";
                        case PadButton.LeftTrigger: return "LT";
                        case PadButton.RightTrigger: return "RT";
                        case PadButton.Select: return "View";
                        case PadButton.Start: return "Menu";
                    }
                    break;
            }
            return b == PadButton.LeftStick ? "L-Stick" : b == PadButton.RightStick ? "R-Stick" : b.ToString();
        }
    }

    /// <summary>
    /// Detects which kind of controller sits in each joystick slot (by name) and provides the matching
    /// button/axis layout for the current OS. Also tracks hot-plugging.
    /// </summary>
    public static class Controllers
    {
        static string[] names = new string[0];
        static float pollTimer;
        public static event Action<int, bool, string> Changed; // joystick (1-based), connected, name

        static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        static bool IsMac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

        static readonly PadProfile XboxWindows = new PadProfile { Name = "Xbox", Layout = PadLayout.Xbox, South = 0, East = 1, West = 2, North = 3, LB = 4, RB = 5, Select = 6, Start = 7, LS = 8, RS = 9, RX = 4, RY = 5, LT = 9, RT = 10, DX = 6, DY = 7, InvertDY = true };
        static readonly PadProfile XboxLinux = new PadProfile { Name = "Xbox", Layout = PadLayout.Xbox, South = 0, East = 1, West = 2, North = 3, LB = 4, RB = 5, Select = 6, Start = 7, LS = 9, RS = 10, RX = 4, RY = 5, LT = 3, RT = 6, DX = 7, DY = 8, TriggersFromMinusOne = true };
        static readonly PadProfile XboxMac = new PadProfile { Name = "Xbox", Layout = PadLayout.Xbox, South = 16, East = 17, West = 18, North = 19, LB = 13, RB = 14, Select = 10, Start = 9, LS = 11, RS = 12, RX = 3, RY = 4, LT = 5, RT = 6, TriggersFromMinusOne = true };
        static readonly PadProfile PlayStationDesktop = new PadProfile { Name = "PlayStation", Layout = PadLayout.PlayStation, West = 0, South = 1, East = 2, North = 3, LB = 4, RB = 5, LTButton = 6, RTButton = 7, Select = 8, Start = 9, LS = 10, RS = 11, RX = 3, RY = 6, LT = 4, RT = 5, DX = 7, DY = 8, TriggersFromMinusOne = true, InvertDY = true };
        static readonly PadProfile SwitchDesktop = new PadProfile { Name = "Switch Pro", Layout = PadLayout.Switch, South = 0, East = 1, West = 2, North = 3, LB = 4, RB = 5, LTButton = 6, RTButton = 7, Select = 8, Start = 9, LS = 10, RS = 11, RX = 4, RY = 5 };

        /// <summary>Called every frame from the game manager; raises Changed on hot-plug.</summary>
        public static void Poll()
        {
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer > 0f) return;
            pollTimer = 1f;
            string[] now;
            try { now = Input.GetJoystickNames(); }
            catch (Exception) { return; }
            int n = Mathf.Max(now.Length, names.Length);
            for (int i = 0; i < n; i++)
            {
                string before = i < names.Length ? names[i] : "";
                string after = i < now.Length ? now[i] : "";
                bool was = !string.IsNullOrEmpty(before), isNow = !string.IsNullOrEmpty(after);
                if (was != isNow) Changed?.Invoke(i + 1, isNow, isNow ? after : before);
            }
            names = now;
        }

        public static string Name(int joystick) => joystick >= 1 && joystick <= names.Length ? names[joystick - 1] : "";
        public static bool IsConnected(int joystick) => !string.IsNullOrEmpty(Name(joystick));

        public static PadLayout DetectLayout(int joystick)
        {
            string n = Name(joystick).ToLowerInvariant();
            if (n.Contains("wireless controller") || n.Contains("dualshock") || n.Contains("dualsense") || n.Contains("playstation") || n.Contains("ps4") || n.Contains("ps5"))
                return PadLayout.PlayStation;
            if (n.Contains("pro controller") || n.Contains("switch") || n.Contains("nintendo") || n.Contains("joy-con"))
                return PadLayout.Switch;
            return PadLayout.Xbox;
        }

        public static PadProfile Profile(int joystick)
        {
            var layout = InputBindings.Current.ForcedLayout >= 0 ? (PadLayout)InputBindings.Current.ForcedLayout : DetectLayout(joystick);
            switch (layout)
            {
                case PadLayout.PlayStation: return PlayStationDesktop;
                case PadLayout.Switch: return SwitchDesktop;
                default: return IsWindows ? XboxWindows : IsMac ? XboxMac : XboxLinux;
            }
        }

        public static float Trigger(int joystick, PadProfile p, bool right)
        {
            int axis = right ? p.RT : p.LT;
            int button = right ? p.RTButton : p.LTButton;
            float v = 0f;
            if (axis > 0)
            {
                v = SafeInput.JoyAxis(joystick, axis);
                if (p.TriggersFromMinusOne) v = Mathf.Abs(v) < 0.001f ? 0f : (v + 1f) * 0.5f;
            }
            if (button >= 0 && Input.GetKey(SafeInput.JoyButton(joystick, button))) v = 1f;
            return Mathf.Clamp01(v);
        }

        public static bool Held(int joystick, PadProfile p, PadButton b)
        {
            if (b == PadButton.None) return false;
            if (b == PadButton.LeftTrigger) return Trigger(joystick, p, false) > 0.45f;
            if (b == PadButton.RightTrigger) return Trigger(joystick, p, true) > 0.45f;
            int i = p.ButtonIndex(b);
            return i >= 0 && Input.GetKey(SafeInput.JoyButton(joystick, i));
        }

        public static Vector2 LeftStick(int j, PadProfile p) => new Vector2(SafeInput.JoyAxis(j, p.LX), -SafeInput.JoyAxis(j, p.LY));
        public static Vector2 RightStick(int j, PadProfile p) => p.RX > 0 ? new Vector2(SafeInput.JoyAxis(j, p.RX), -SafeInput.JoyAxis(j, p.RY)) : Vector2.zero;

        public static Vector2 DPad(int j, PadProfile p)
        {
            if (p.DX <= 0) return Vector2.zero;
            float y = SafeInput.JoyAxis(j, p.DY);
            return new Vector2(SafeInput.JoyAxis(j, p.DX), p.InvertDY ? y : -y);
        }
    }

    [Serializable]
    public class KeyBinding
    {
        public GameAction action;
        public KeyCode primary;
        public KeyCode secondary;
    }

    [Serializable]
    public class PadBinding
    {
        public GameAction action;
        public PadButton primary;
        public PadButton secondary;
    }

    /// <summary>Player-rebindable controls, saved in PlayerPrefs.</summary>
    [Serializable]
    public class InputBindings
    {
        const string PrefsKey = "cubed.bindings.v1";
        public List<KeyBinding> keys = new List<KeyBinding>();
        public List<PadBinding> pads = new List<PadBinding>();
        /// <summary>-1 = detect the controller type automatically, otherwise a PadLayout.</summary>
        public int ForcedLayout = -1;

        static InputBindings current;
        public static InputBindings Current => current ?? (current = Load());

        public static InputBindings Defaults()
        {
            var b = new InputBindings();
            void K(GameAction a, KeyCode p, KeyCode s = KeyCode.None) => b.keys.Add(new KeyBinding { action = a, primary = p, secondary = s });
            void P(GameAction a, PadButton p, PadButton s = PadButton.None) => b.pads.Add(new PadBinding { action = a, primary = p, secondary = s });
            K(GameAction.MoveForward, KeyCode.W, KeyCode.UpArrow);
            K(GameAction.MoveBack, KeyCode.S, KeyCode.DownArrow);
            K(GameAction.MoveLeft, KeyCode.A, KeyCode.LeftArrow);
            K(GameAction.MoveRight, KeyCode.D, KeyCode.RightArrow);
            K(GameAction.Jump, KeyCode.Space);
            K(GameAction.Fire, KeyCode.Mouse0);
            K(GameAction.Block, KeyCode.Mouse1, KeyCode.LeftShift);
            K(GameAction.Reload, KeyCode.R);
            K(GameAction.Pause, KeyCode.Escape);
            P(GameAction.Jump, PadButton.South);
            P(GameAction.Fire, PadButton.RightTrigger, PadButton.RightShoulder);
            P(GameAction.Block, PadButton.LeftTrigger, PadButton.LeftShoulder);
            P(GameAction.Reload, PadButton.West);
            P(GameAction.Pause, PadButton.Start);
            return b;
        }

        public KeyBinding Key(GameAction a)
        {
            var k = keys.FirstOrDefault(x => x.action == a);
            if (k == null) { k = Defaults().keys.First(x => x.action == a); keys.Add(k); }
            return k;
        }

        public PadBinding Pad(GameAction a) => pads.FirstOrDefault(x => x.action == a);

        public bool KeyHeld(GameAction a)
        {
            var k = Key(a);
            return (k.primary != KeyCode.None && Input.GetKey(k.primary)) || (k.secondary != KeyCode.None && Input.GetKey(k.secondary));
        }

        public bool KeyDown(GameAction a)
        {
            var k = Key(a);
            return (k.primary != KeyCode.None && Input.GetKeyDown(k.primary)) || (k.secondary != KeyCode.None && Input.GetKeyDown(k.secondary));
        }

        public bool PadHeld(GameAction a, int joystick, PadProfile p)
        {
            var b = Pad(a);
            return b != null && (Controllers.Held(joystick, p, b.primary) || Controllers.Held(joystick, p, b.secondary));
        }

        public void Save()
        {
            try { PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static InputBindings Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(PrefsKey))
                {
                    var b = JsonUtility.FromJson<InputBindings>(PlayerPrefs.GetString(PrefsKey));
                    if (b != null && b.keys != null && b.keys.Count > 0)
                    {
                        // Add actions introduced after the bindings were saved.
                        var d = Defaults();
                        foreach (var k in d.keys) if (b.keys.All(x => x.action != k.action)) b.keys.Add(k);
                        if (b.pads == null) b.pads = new List<PadBinding>();
                        foreach (var p in d.pads) if (b.pads.All(x => x.action != p.action)) b.pads.Add(p);
                        return b;
                    }
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            return Defaults();
        }

        public static void ResetToDefaults()
        {
            current = Defaults();
            current.Save();
        }

        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "-";
                case KeyCode.Mouse0: return "Left Mouse";
                case KeyCode.Mouse1: return "Right Mouse";
                case KeyCode.Mouse2: return "Middle Mouse";
                case KeyCode.LeftShift: return "L-Shift";
                case KeyCode.RightShift: return "R-Shift";
                case KeyCode.LeftControl: return "L-Ctrl";
                case KeyCode.LeftAlt: return "L-Alt";
                default: return k.ToString();
            }
        }

        static KeyCode[] allKeys;

        /// <summary>The key or mouse button pressed this frame (for rebinding), or None.</summary>
        public static KeyCode PressedKey()
        {
            if (!Input.anyKeyDown) return KeyCode.None;
            if (allKeys == null) allKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Where(k => k < KeyCode.JoystickButton0).ToArray();
            foreach (var k in allKeys) if (Input.GetKeyDown(k)) return k;
            return KeyCode.None;
        }
    }

    /// <summary>
    /// Gamepad rumble. Unity's legacy input has no vibration API, so this only works when the
    /// Input System package is installed (the asmdef defines CUBED_INPUT_SYSTEM automatically).
    /// </summary>
    public static class Rumble
    {
        public static float Strength = 1f;
        static readonly float[] until = new float[16];

        public static void Pulse(int joystick, float low, float high, float duration)
        {
            if (Strength <= 0f || joystick < 1) return;
#if CUBED_INPUT_SYSTEM
            var pads = UnityEngine.InputSystem.Gamepad.all;
            int i = joystick - 1;
            if (i >= pads.Count) return;
            pads[i].SetMotorSpeeds(Mathf.Clamp01(low * Strength), Mathf.Clamp01(high * Strength));
            until[i] = Time.unscaledTime + duration;
#endif
        }

        /// <summary>Stop finished pulses; called every frame.</summary>
        public static void Update()
        {
#if CUBED_INPUT_SYSTEM
            var pads = UnityEngine.InputSystem.Gamepad.all;
            for (int i = 0; i < pads.Count && i < until.Length; i++)
                if (until[i] > 0f && Time.unscaledTime > until[i])
                {
                    until[i] = 0f;
                    pads[i].SetMotorSpeeds(0f, 0f);
                }
#endif
        }

        public static bool Available
        {
            get
            {
#if CUBED_INPUT_SYSTEM
                return true;
#else
                return false;
#endif
            }
        }
    }
}
