using System.Collections.Generic;
using UnityEngine;

namespace Cubed
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
                Debug.LogWarning("[Cubed] Input axis '" + name + "' is not configured. Run Cubed > Setup Project in the Unity editor.");
                return 0f;
            }
        }

        /// <summary>Joystick n (1-based) axis k (1-based), configured as "Cubed_J{n}_A{k}".</summary>
        public static float JoyAxis(int joystick, int axis) => Axis("Cubed_J" + joystick + "_A" + axis);

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

        bool prevBlockTrigger, prevMenuStick;

        public HumanInput(ControlType device) { Device = device; }

        public bool IsKeyboard => Device == ControlType.KeyboardMouse;
        public int Joystick => Device >= ControlType.Gamepad1 && Device <= ControlType.Gamepad4 ? (int)Device - (int)ControlType.Gamepad1 + 1 : 0;

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
            if (IsKeyboard)
            {
                float sens = s != null ? s.mouseSensitivity : 2f;
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                look = locked ? new Vector2(SafeInput.Axis("Mouse X"), SafeInput.Axis("Mouse Y")) * sens : Vector2.zero;
                move = new Vector2(
                    (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                    (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
                JumpPressed = Input.GetKeyDown(KeyCode.Space);
                FireHeld = locked && Input.GetMouseButton(0);
                BlockPressed = (locked && Input.GetMouseButtonDown(1)) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.F);
                ReloadPressed = Input.GetKeyDown(KeyCode.R);
                PausePressed = Input.GetKeyDown(KeyCode.Escape);
            }
            else
            {
                int j = Joystick;
                float sens = (s != null ? s.gamepadSensitivity : 160f) * dt;
                look = Deadzone(new Vector2(SafeInput.JoyAxis(j, 4), -SafeInput.JoyAxis(j, 5)), 0.15f) * sens;
                move = Deadzone(new Vector2(SafeInput.JoyAxis(j, 1), -SafeInput.JoyAxis(j, 2)), 0.2f);
                // Windows XInput maps triggers to axes 9/10; Linux/macOS drivers commonly use 3/6.
                bool windows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
                float rt = SafeInput.JoyAxis(j, windows ? 10 : 6);
                float lt = SafeInput.JoyAxis(j, windows ? 9 : 3);
                bool blockTrigger = lt > 0.5f;
                JumpPressed = Input.GetKeyDown(SafeInput.JoyButton(j, 0));
                FireHeld = rt > 0.4f || Input.GetKey(SafeInput.JoyButton(j, 5));
                BlockPressed = Input.GetKeyDown(SafeInput.JoyButton(j, 4)) || Input.GetKeyDown(SafeInput.JoyButton(j, 1)) || (blockTrigger && !prevBlockTrigger);
                prevBlockTrigger = blockTrigger;
                ReloadPressed = Input.GetKeyDown(SafeInput.JoyButton(j, 2));
                PausePressed = Input.GetKeyDown(SafeInput.JoyButton(j, 7));
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
            // Left stick or the shoulder buttons (d-pad axes differ between platforms).
            float x = SafeInput.JoyAxis(j, 1);
            bool stick = Mathf.Abs(x) > 0.6f;
            if (stick && !prevMenuStick) MenuHorizontal = x > 0 ? 1 : -1;
            prevMenuStick = stick;
            if (Input.GetKeyDown(SafeInput.JoyButton(j, 4))) MenuHorizontal = -1;
            if (Input.GetKeyDown(SafeInput.JoyButton(j, 5))) MenuHorizontal = 1;
            MenuConfirm = Input.GetKeyDown(SafeInput.JoyButton(j, 0));
        }

        static Vector2 Deadzone(Vector2 v, float dz)
        {
            float m = v.magnitude;
            if (m < dz) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - dz) / (1f - dz));
        }
    }
}
