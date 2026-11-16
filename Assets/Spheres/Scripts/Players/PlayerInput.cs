using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Anything that can drive a player: keyboard/mouse, gamepads, bots, or a mod's network input.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Called once per frame by the player before it reads the properties below.</summary>
        void Tick(Player player, float dt);
        /// <summary>Called when the player (re)spawns, facing lookDirection.</summary>
        void OnSpawn(Player player, Vector3 lookDirection);

        /// <summary>Desired movement on the XZ plane in world space, magnitude 0..1.</summary>
        Vector3 Move { get; }
        /// <summary>World point the player is aiming at.</summary>
        Vector3 AimPoint { get; }
        bool JumpPressed { get; }
        bool FireHeld { get; }
        bool BlockPressed { get; }
        bool ReloadPressed { get; }
    }

    /// <summary>Input used by menus that need navigation (card picking).</summary>
    public interface IMenuInput
    {
        /// <summary>Poll menu buttons; call once per frame while a menu is open.</summary>
        void TickMenu();
        /// <summary>-1, 0 or +1 on the frame a left/right press happens.</summary>
        int MenuHorizontal { get; }
        bool MenuConfirm { get; }
    }

    /// <summary>Input.GetAxis wrapper that never throws if an axis is missing from the Input Manager.</summary>
    public static class SafeInput
    {
        static readonly HashSet<string> missing = new HashSet<string>();

        public static float Axis(string name)
        {
            if (missing.Contains(name)) return 0f;
            try { return Input.GetAxisRaw(name); }
            catch (System.ArgumentException)
            {
                missing.Add(name);
                Debug.LogWarning("[Spheres] Input axis '" + name + "' is not configured. Run Spheres > Setup Project in the Unity editor.");
                return 0f;
            }
        }

        /// <summary>Joystick n (1-based) axis k (1-based), configured as "Spheres_J{n}_A{k}".</summary>
        public static float JoyAxis(int joystick, int axis) => Axis("Spheres_J" + joystick + "_A" + axis);

        public static KeyCode JoyButton(int joystick, int button) =>
            (KeyCode)((int)KeyCode.Joystick1Button0 + (joystick - 1) * 20 + button);
    }

    /// <summary>A human on keyboard+mouse or a gamepad, with a third-person camera.</summary>
    public class HumanInput : IPlayerInput, IMenuInput
    {
        public readonly ControlType Device;
        public PlayerCamera Camera;
        public float Yaw, Pitch = 12f;

        public Vector3 Move { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool BlockPressed { get; private set; }
        public bool ReloadPressed { get; private set; }
        public int MenuHorizontal { get; private set; }
        public bool MenuConfirm { get; private set; }
        public bool PausePressed { get; private set; }

        bool prevBlockTrigger, prevMenuStick, prevJump, prevReload, prevPause, prevConfirm, prevLeft, prevRight;

        public HumanInput(ControlType device) { Device = device; }

        public bool IsKeyboard => Device == ControlType.KeyboardMouse;
        public int Joystick => ControlTypes.Joystick(Device);

        static MatchSettings Settings => GameManager.Instance != null ? GameManager.Instance.Settings : null;

        public void OnSpawn(Player player, Vector3 lookDirection)
        {
            Yaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
            Pitch = 12f;
        }

        public void Tick(Player player, float dt)
        {
            var s = Settings;
            float invert = s != null && s.invertY ? -1f : 1f;
            Vector2 look, move;
            var binds = InputBindings.Current;
            if (IsKeyboard)
            {
                float sens = s != null ? s.mouseSensitivity : 2f;
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                look = locked ? new Vector2(SafeInput.Axis("Mouse X"), SafeInput.Axis("Mouse Y")) * sens : Vector2.zero;
                move = new Vector2(
                    (binds.KeyHeld(GameAction.MoveRight) ? 1f : 0f) - (binds.KeyHeld(GameAction.MoveLeft) ? 1f : 0f),
                    (binds.KeyHeld(GameAction.MoveForward) ? 1f : 0f) - (binds.KeyHeld(GameAction.MoveBack) ? 1f : 0f));
                JumpPressed = binds.KeyDown(GameAction.Jump);
                // Mouse buttons only count while the cursor is captured (so clicking UI doesn't shoot).
                var fire = binds.Key(GameAction.Fire);
                FireHeld = binds.KeyHeld(GameAction.Fire) && (locked || (fire.primary != KeyCode.Mouse0 && fire.secondary != KeyCode.Mouse0));
                BlockPressed = binds.KeyDown(GameAction.Block) && (locked || !Input.GetMouseButtonDown(1));
                ReloadPressed = binds.KeyDown(GameAction.Reload);
                PausePressed = binds.KeyDown(GameAction.Pause);
            }
            else
            {
                int j = Joystick;
                var profile = Controllers.Profile(j);
                float sens = (s != null ? s.gamepadSensitivity : 160f) * dt;
                look = Deadzone(Controllers.RightStick(j, profile), 0.15f) * sens;
                move = Deadzone(Controllers.LeftStick(j, profile), 0.2f);
                bool jump = binds.PadHeld(GameAction.Jump, j, profile);
                bool block = binds.PadHeld(GameAction.Block, j, profile);
                bool reload = binds.PadHeld(GameAction.Reload, j, profile);
                bool pause = binds.PadHeld(GameAction.Pause, j, profile);
                JumpPressed = jump && !prevJump;
                BlockPressed = block && !prevBlockTrigger;
                ReloadPressed = reload && !prevReload;
                PausePressed = pause && !prevPause;
                FireHeld = binds.PadHeld(GameAction.Fire, j, profile);
                prevJump = jump;
                prevBlockTrigger = block;
                prevReload = reload;
                prevPause = pause;
            }

            Yaw = Mathf.Repeat(Yaw + look.x, 360f);
            Pitch = Mathf.Clamp(Pitch - look.y * invert, -75f, 80f);

            Quaternion yawRot = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 fwd = yawRot * Vector3.forward;
            Vector3 right = yawRot * Vector3.right;
            Vector3 m = fwd * move.y + right * move.x;
            Move = m.sqrMagnitude > 1f ? m.normalized : m;

            AimPoint = Camera != null ? Camera.ComputeAimPoint(player) : player.Head.position + Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward * 50f;
        }

        public void TickMenu()
        {
            MenuHorizontal = 0;
            MenuConfirm = false;
            if (IsKeyboard)
            {
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MenuHorizontal = -1;
                if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MenuHorizontal = 1;
                MenuConfirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                return;
            }
            int j = Joystick;
            var profile = Controllers.Profile(j);
            float x = Controllers.LeftStick(j, profile).x;
            float dpad = Controllers.DPad(j, profile).x;
            if (Mathf.Abs(dpad) > 0.5f) x = dpad;
            bool stick = Mathf.Abs(x) > 0.6f;
            if (stick && !prevMenuStick) MenuHorizontal = x > 0 ? 1 : -1;
            prevMenuStick = stick;
            bool left = Controllers.Held(j, profile, PadButton.LeftShoulder), right = Controllers.Held(j, profile, PadButton.RightShoulder);
            if (left && !prevLeft) MenuHorizontal = -1;
            if (right && !prevRight) MenuHorizontal = 1;
            prevLeft = left;
            prevRight = right;
            bool confirm = Controllers.Held(j, profile, PadButton.South);
            MenuConfirm = confirm && !prevConfirm;
            prevConfirm = confirm;
        }

        static Vector2 Deadzone(Vector2 v, float dz)
        {
            float m = v.magnitude;
            if (m < dz) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - dz) / (1f - dz));
        }
    }
}
