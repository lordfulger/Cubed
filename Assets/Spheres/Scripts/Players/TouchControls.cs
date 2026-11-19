using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// On-screen controls for phones and tablets: a floating move stick on the left half of the screen, drag
    /// anywhere on the right half to look, and buttons for fire (drag it to aim while shooting), jump, block,
    /// reload, scoreboard and pause. Feeds the first local keyboard/touch player (see HumanInput).
    /// </summary>
    public static class TouchControls
    {
        /// <summary>Touch controls are on (phones/tablets, or forced for testing).</summary>
        public static bool Active => Application.isMobilePlatform || Force;
        /// <summary>Show touch controls on desktop too (testing; touches come from a touchscreen).</summary>
        public static bool Force;
        /// <summary>Look speed in degrees per virtual pixel dragged.</summary>
        public static float Sensitivity = 0.28f;

        public static Vector2 Move { get; private set; }
        /// <summary>Degrees of yaw (x) and pitch (y) to turn this frame.</summary>
        public static Vector2 Look { get; private set; }
        public static bool FireHeld { get; private set; }
        public static bool JumpPressed { get; private set; }
        public static bool BlockPressed { get; private set; }
        public static bool ReloadPressed { get; private set; }
        public static bool PausePressed { get; private set; }
        public static bool ScoreboardHeld { get; private set; }

        enum Button { Fire, Jump, Block, Reload, Score, Pause }
        static readonly string[] Labels = { "FIRE", "JUMP", "BLOCK", "R", "TAB", "II" };

        const float StickRadius = 80f;
        static int moveId = -1, lookId = -1, fireId = -1, scoreId = -1;
        static Vector2 moveOrigin, moveNow, lookLast, fireLast;
        static int frame = -1;
        static Texture2D disc, ring;

        static bool InFight
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm == null) return false;
                var s = gm.State;
                return s == GameState.Fighting || s == GameState.RoundIntro || s == GameState.RoundEnd;
            }
        }

        /// <summary>Button layout in UIKit virtual coordinates (bottom-right thumb area, bigger on small screens).</summary>
        static Rect ButtonRect(Button b, out float radius)
        {
            var s = UIKit.Size;
            float k = Mathf.Clamp(1.15f * 900f / Mathf.Max(400f, Screen.dpi > 0 ? Screen.height / Screen.dpi * 160f : 900f), 1f, 1.4f);
            Vector2 c;
            switch (b)
            {
                case Button.Fire: radius = 78f * k; c = new Vector2(s.x - 170f * k, s.y - 200f * k); break;
                case Button.Jump: radius = 56f * k; c = new Vector2(s.x - 85f * k, s.y - 85f * k); break;
                case Button.Block: radius = 50f * k; c = new Vector2(s.x - 330f * k, s.y - 110f * k); break;
                case Button.Reload: radius = 38f * k; c = new Vector2(s.x - 320f * k, s.y - 280f * k); break;
                case Button.Score: radius = 32f * k; c = new Vector2(s.x - 170f * k, 60f * k); break;
                default: radius = 32f * k; c = new Vector2(s.x - 70f * k, 60f * k); break;
            }
            return new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f);
        }

        static bool Hit(Button b, Vector2 p)
        {
            var r = ButtonRect(b, out float radius);
            return (p - r.center).sqrMagnitude <= radius * radius * 1.3f;
        }

        static Vector2 ToVirtual(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y) / Mathf.Max(0.01f, UIKit.Scale);

        /// <summary>Read the touchscreen (call once per frame before players read their input).</summary>
        public static void Update()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            JumpPressed = BlockPressed = ReloadPressed = PausePressed = false;
            Look = Vector2.zero;
            if (!Active || !InFight)
            {
                moveId = lookId = fireId = scoreId = -1;
                Move = Vector2.zero;
                FireHeld = ScoreboardHeld = false;
                return;
            }

            Vector2 look = Vector2.zero;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var p = ToVirtual(t.position);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (Hit(Button.Pause, p)) PausePressed = true;
                        else if (Hit(Button.Score, p)) scoreId = t.fingerId;
                        else if (Hit(Button.Jump, p)) JumpPressed = true;
                        else if (Hit(Button.Block, p)) BlockPressed = true;
                        else if (Hit(Button.Reload, p)) ReloadPressed = true;
                        else if (Hit(Button.Fire, p) && fireId < 0) { fireId = t.fingerId; fireLast = p; }
                        else if (p.x < UIKit.Size.x * 0.45f && moveId < 0) { moveId = t.fingerId; moveOrigin = moveNow = p; }
                        else if (lookId < 0) { lookId = t.fingerId; lookLast = p; }
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (t.fingerId == moveId) moveNow = p;
                        else if (t.fingerId == lookId) { look += p - lookLast; lookLast = p; }
                        else if (t.fingerId == fireId) { look += (p - fireLast) * 0.8f; fireLast = p; } // aim while firing
                        break;
                    default: // Ended / Canceled
                        if (t.fingerId == moveId) moveId = -1;
                        if (t.fingerId == lookId) lookId = -1;
                        if (t.fingerId == fireId) fireId = -1;
                        if (t.fingerId == scoreId) scoreId = -1;
                        break;
                }
            }

            if (moveId >= 0)
            {
                Vector2 d = moveNow - moveOrigin;
                // The stick follows the thumb if it drifts too far, so you never lose it.
                if (d.magnitude > StickRadius * 1.6f) moveOrigin = moveNow - d.normalized * StickRadius * 1.6f;
                Vector2 v = Vector2.ClampMagnitude(d / StickRadius, 1f);
                Move = new Vector2(v.x, -v.y);
            }
            else Move = Vector2.zero;
            Look = new Vector2(look.x, -look.y) * Sensitivity;
            FireHeld = fireId >= 0;
            ScoreboardHeld = scoreId >= 0;
        }

        /// <summary>Draw the controls (inside the HUD's OnGUI, UIKit scaling active).</summary>
        public static void Draw()
        {
            if (!Active || !InFight) return;
            if (disc == null) { disc = MakeCircle(false); ring = MakeCircle(true); }
            var old = GUI.color;

            if (moveId >= 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.22f);
                GUI.DrawTexture(new Rect(moveOrigin.x - StickRadius, moveOrigin.y - StickRadius, StickRadius * 2f, StickRadius * 2f), ring);
                var knob = moveOrigin + Vector2.ClampMagnitude(moveNow - moveOrigin, StickRadius);
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.DrawTexture(new Rect(knob.x - 34f, knob.y - 34f, 68f, 68f), disc);
            }
            else
            {
                // Hint where the stick lives.
                var s = UIKit.Size;
                GUI.color = new Color(1f, 1f, 1f, 0.1f);
                GUI.DrawTexture(new Rect(150f, s.y - 290f, StickRadius * 2f, StickRadius * 2f), ring);
            }

            for (int i = 0; i < Labels.Length; i++)
            {
                var b = (Button)i;
                var r = ButtonRect(b, out _);
                bool down = (b == Button.Fire && FireHeld) || (b == Button.Score && ScoreboardHeld);
                var col = b == Button.Fire ? new Color(1f, 0.4f, 0.35f) : b == Button.Block ? new Color(0.45f, 0.8f, 1f) : Color.white;
                GUI.color = new Color(col.r, col.g, col.b, down ? 0.55f : 0.28f);
                GUI.DrawTexture(r, disc);
                GUI.color = new Color(col.r, col.g, col.b, 0.7f);
                GUI.DrawTexture(r, ring);
                GUI.color = old;
                UIKit.Shadowed(r, "<b>" + Labels[i] + "</b>", UIKit.SmallCenter, new Color(1f, 1f, 1f, 0.9f));
            }
            GUI.color = old;
        }

        static Texture2D MakeCircle(bool outline)
        {
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = outline ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * 25f) : Mathf.Clamp01((1f - d) * 30f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}
