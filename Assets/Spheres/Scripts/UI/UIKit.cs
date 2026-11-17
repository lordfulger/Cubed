using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Tiny IMGUI toolkit: resolution-independent scaling (virtual height 900) and a flat dark theme.
    /// </summary>
    public static class UIKit
    {
        public const float VirtualHeight = 900f;
        public static float Scale { get; private set; } = 1f;
        public static Vector2 Size { get; private set; } = new Vector2(1600f, 900f);

        public static readonly Color Bg = new Color(0.08f, 0.09f, 0.13f, 0.92f);
        public static readonly Color Panel = new Color(0.13f, 0.14f, 0.2f, 0.95f);
        public static readonly Color PanelLight = new Color(0.19f, 0.21f, 0.29f, 1f);
        public static readonly Color Accent = new Color(0.36f, 0.72f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.92f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
        public static readonly Color Muted = new Color(0.65f, 0.68f, 0.78f);

        public static GUIStyle Label, LabelCenter, Small, SmallCenter, Header, HeaderCenter, Title, Huge, Button, ButtonSmall, Field, Wrap, WrapCenter;

        static bool initialized;
        static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();

        public static Texture2D Tex(Color c)
        {
            Color32 k = c;
            int key = (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
            if (textures.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            textures[key] = t;
            return t;
        }

        static GUIStyle Text(int size, TextAnchor anchor, FontStyle style = FontStyle.Normal, bool wrap = false)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = style, wordWrap = wrap, richText = true };
            s.normal.textColor = Color.white;
            s.padding = new RectOffset(2, 2, 1, 1);
            s.margin = new RectOffset(0, 0, 0, 0);
            return s;
        }

        static void Init()
        {
            initialized = true;
            Label = Text(18, TextAnchor.MiddleLeft);
            LabelCenter = Text(18, TextAnchor.MiddleCenter);
            Small = Text(14, TextAnchor.MiddleLeft);
            SmallCenter = Text(14, TextAnchor.MiddleCenter);
            Header = Text(24, TextAnchor.MiddleLeft, FontStyle.Bold);
            HeaderCenter = Text(24, TextAnchor.MiddleCenter, FontStyle.Bold);
            Title = Text(40, TextAnchor.MiddleCenter, FontStyle.Bold);
            Huge = Text(84, TextAnchor.MiddleCenter, FontStyle.Bold);
            Wrap = Text(15, TextAnchor.UpperLeft, FontStyle.Normal, true);
            WrapCenter = Text(15, TextAnchor.UpperCenter, FontStyle.Normal, true);

            Button = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true };
            Button.normal.background = Tex(PanelLight);
            Button.hover.background = Tex(new Color(0.27f, 0.31f, 0.43f));
            Button.active.background = Tex(Accent * 0.8f);
            Button.focused.background = Button.normal.background;
            Button.normal.textColor = Button.hover.textColor = Button.active.textColor = Button.focused.textColor = Color.white;
            Button.border = new RectOffset(0, 0, 0, 0);
            Button.padding = new RectOffset(6, 6, 4, 4);

            ButtonSmall = new GUIStyle(Button) { fontSize = 14 };

            Field = new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            Field.normal.background = Tex(new Color(0.06f, 0.07f, 0.1f));
            Field.focused.background = Tex(new Color(0.04f, 0.05f, 0.08f));
            Field.normal.textColor = Field.focused.textColor = Color.white;
            Field.padding = new RectOffset(8, 8, 4, 4);
        }

        // ------------------------------------------------------------------ gamepad / keyboard navigation

        /// <summary>True while the menus are being driven by a gamepad or the arrow keys (mouse movement turns it off).</summary>
        public static bool NavActive { get; private set; }
        static int focus, counter, lastCount, frame = -1, pendingMove, pendingAdjust;
        static bool pendingSubmit, pendingBack, submit, back, focusMoved;
        static int adjust;
        static float repeatTimer;
        static int heldDir;
        static Vector3 lastMousePos;

        /// <summary>Read navigation input. Call once per frame from Update.</summary>
        public static void PollNavigation(bool allowKeyboardBack)
        {
            int vertical = 0, horizontal = 0;
            bool a = false, b = false;
            if (Input.GetKeyDown(KeyCode.UpArrow)) vertical = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) vertical = 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) horizontal = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) horizontal = 1;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) a = true;
            if (allowKeyboardBack && Input.GetKeyDown(KeyCode.Escape)) b = true;
            bool keyboardNav = vertical != 0 || horizontal != 0 || a;

            // Gamepads: left stick or d-pad (per detected controller layout) with key repeat.
            int dir = 0; // 1 up, 2 down, 3 left, 4 right
            for (int j = 1; j <= ControlTypes.MaxGamepads; j++)
            {
                if (!Controllers.IsConnected(j)) continue;
                var prof = Controllers.Profile(j);
                var st = Controllers.LeftStick(j, prof);
                var dp = Controllers.DPad(j, prof);
                float x = Mathf.Abs(dp.x) > 0.5f ? dp.x : st.x;
                float y = Mathf.Abs(dp.y) > 0.5f ? dp.y : st.y;
                if (y > 0.6f) dir = 1;
                else if (y < -0.6f) dir = 2;
                else if (x < -0.6f) dir = 3;
                else if (x > 0.6f) dir = 4;
                int south = prof.ButtonIndex(PadButton.South), east = prof.ButtonIndex(PadButton.East);
                if (south >= 0 && Input.GetKeyDown(SafeInput.JoyButton(j, south))) a = true;
                if (east >= 0 && Input.GetKeyDown(SafeInput.JoyButton(j, east))) b = true;
                if (dir != 0) break;
            }
            bool fire = false;
            if (dir != heldDir) { heldDir = dir; fire = dir != 0; repeatTimer = 0.4f; }
            else if (dir != 0)
            {
                repeatTimer -= Time.unscaledDeltaTime;
                if (repeatTimer <= 0f) { fire = true; repeatTimer = 0.11f; }
            }
            if (fire)
            {
                if (dir == 1) vertical = -1;
                else if (dir == 2) vertical = 1;
                else if (dir == 3) horizontal = -1;
                else horizontal = 1;
            }

            bool any = vertical != 0 || horizontal != 0 || a || b;
            if (any && (fire || keyboardNav || a))
            {
                if (!NavActive) { NavActive = true; vertical = 0; horizontal = 0; a = false; } // first press only shows focus
            }
            if ((Input.mousePosition - lastMousePos).sqrMagnitude > 4f || Input.GetMouseButtonDown(0)) NavActive = false;
            lastMousePos = Input.mousePosition;

            pendingMove += vertical;
            pendingAdjust += horizontal;
            pendingSubmit |= a && NavActive;
            pendingBack |= b;
        }

        /// <summary>Register a focusable control. Returns true when it has gamepad focus.</summary>
        public static bool NavFocus(Rect r)
        {
            int id = counter++;
            bool f = NavActive && id == focus;
            if (f && Event.current.type == EventType.Repaint)
            {
                Outline(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(1f, 0.85f, 0.3f), 3f);
                if (focusMoved) GUI.ScrollTo(new Rect(r.x, r.y - 40f, r.width, r.height + 80f));
            }
            return f;
        }

        /// <summary>True (once) when the focused control should activate.</summary>
        static bool TakeSubmit(bool focused)
        {
            if (!focused || !submit) return false;
            submit = false;
            return true;
        }

        static int TakeAdjust(bool focused)
        {
            if (!focused || adjust == 0) return 0;
            int a = adjust;
            adjust = 0;
            return a;
        }

        /// <summary>True (once per press) when B / Escape was pressed this frame.</summary>
        public static bool BackPressed()
        {
            if (!back) return false;
            back = false;
            return true;
        }

        /// <summary>An invisible clickable/focusable area (used for cards).</summary>
        public static bool Clickable(Rect r)
        {
            bool f = NavFocus(r);
            return GUI.Button(r, GUIContent.none, GUIStyle.none) || TakeSubmit(f);
        }

        /// <summary>Call at the end of OnGUI.</summary>
        public static void End()
        {
            if (Event.current.type == EventType.Repaint) lastCount = counter;
        }

        /// <summary>Call at the start of OnGUI.</summary>
        public static void Begin()
        {
            if (!initialized) Init();
            counter = 0;
            if (Event.current.type == EventType.Repaint && Time.frameCount != frame)
            {
                frame = Time.frameCount;
                submit = pendingSubmit;
                back = pendingBack;
                adjust = pendingAdjust;
                focusMoved = pendingMove != 0;
                if (lastCount > 0) focus = ((focus + pendingMove) % lastCount + lastCount) % lastCount;
                else focus = 0;
                pendingMove = pendingAdjust = 0;
                pendingSubmit = pendingBack = false;
            }
            else if (Event.current.type == EventType.Repaint)
            {
                submit = back = false;
                adjust = 0;
            }
            Scale = Mathf.Clamp(Screen.height / VirtualHeight * Options.Current.uiScale, 0.4f, 5f);
            Size = new Vector2(Screen.width / Scale, Screen.height / Scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        }

        /// <summary>Converts a pixel rect (GUI space, top-left origin) to virtual units.</summary>
        public static Rect FromPixels(Rect r) => new Rect(r.x / Scale, r.y / Scale, r.width / Scale, r.height / Scale);

        public static Vector2 MouseVirtual => Event.current != null ? Event.current.mousePosition : Vector2.zero;

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Outline(Rect r, Color c, float t = 2f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        public static void Text(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(r, Loc.T(text), style);
            style.normal.textColor = old;
        }

        /// <summary>Text with a drop shadow for readability over the 3D scene.</summary>
        public static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Text(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style, new Color(0f, 0f, 0f, color.a * 0.7f));
            Text(r, text, style, color);
        }

        public static void Bar(Rect r, float t, Color fg, Color bg)
        {
            Fill(r, bg);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), fg);
        }

        public static bool Btn(Rect r, string text, bool small = false)
        {
            bool focused = GUI.enabled && NavFocus(r);
            bool clicked = GUI.Button(r, Loc.T(text), small ? ButtonSmall : Button) || TakeSubmit(focused);
            if (clicked) Sfx.Play("ui", 0.4f, 0f);
            return clicked;
        }

        /// <summary>A button that is not a navigation stop (part of a larger focusable row).</summary>
        static bool RawBtn(Rect r, string text)
        {
            bool clicked = GUI.Button(r, text, Button);
            if (clicked) Sfx.Play("ui", 0.4f, 0f);
            return clicked;
        }

        /// <summary>Button styled as selected/unselected.</summary>
        public static bool Toggle(Rect r, string text, bool on, bool small = false)
        {
            if (on) Fill(r, Accent * 0.55f);
            var style = small ? ButtonSmall : Button;
            var old = style.normal.background;
            if (on) style.normal.background = Tex(Accent * 0.55f);
            bool focused = NavFocus(r);
            bool clicked = GUI.Button(r, Loc.T(text), style) || TakeSubmit(focused);
            style.normal.background = old;
            if (clicked) Sfx.Play("ui", 0.4f, 0f);
            return clicked;
        }

        /// <summary>"Label   [-] value [+]" row. Returns the new value.</summary>
        public static float Stepper(Rect r, string label, float value, float step, float min, float max, string format = "0", string suffix = "", string zeroText = null)
        {
            float bw = 34f;
            bool focused = NavFocus(r);
            Text(new Rect(r.x, r.y, r.width - bw * 2f - 90f, r.height), label, Label, Color.white);
            float x = r.xMax - bw * 2f - 90f;
            if (RawBtn(new Rect(x, r.y + 2f, bw, r.height - 4f), "-")) value = Mathf.Max(min, value - step);
            string shown = zeroText != null && Mathf.Approximately(value, 0f) ? zeroText : value.ToString(format) + suffix;
            Text(new Rect(x + bw, r.y, 90f, r.height), shown, LabelCenter, Accent);
            if (RawBtn(new Rect(x + bw + 90f, r.y + 2f, bw, r.height - 4f), "+")) value = Mathf.Min(max, value + step);
            int adj = TakeAdjust(focused);
            if (adj != 0)
            {
                value = Mathf.Clamp(value + adj * step, min, max);
                Sfx.Play("ui", 0.4f, 0f);
            }
            return value;
        }

        public static int StepperInt(Rect r, string label, int value, int min, int max, string zeroText = null) =>
            Mathf.RoundToInt(Stepper(r, label, value, 1f, min, max, "0", "", zeroText));

        /// <summary>"Label   [ < option > ]" row cycling through options. Returns the new index.</summary>
        public static int Cycle(Rect r, string label, int index, string[] options)
        {
            float w = Mathf.Min(220f, r.width * 0.55f);
            bool focused = NavFocus(r);
            Text(new Rect(r.x, r.y, r.width - w, r.height), label, Label, Color.white);
            var b = new Rect(r.xMax - w, r.y + 2f, w, r.height - 4f);
            if (RawBtn(b, "<  " + Loc.T(options[Mathf.Clamp(index, 0, options.Length - 1)]) + "  >") || TakeSubmit(focused))
            {
                bool backwards = Event.current != null && Event.current.button == 1;
                index = (index + (backwards ? -1 : 1) + options.Length) % options.Length;
            }
            int adj = TakeAdjust(focused);
            if (adj != 0)
            {
                index = (index + adj + options.Length) % options.Length;
                Sfx.Play("ui", 0.4f, 0f);
            }
            return index;
        }

        public static bool CheckRow(Rect r, string label, bool value)
        {
            float w = Mathf.Min(220f, r.width * 0.55f);
            bool focused = NavFocus(r);
            Text(new Rect(r.x, r.y, r.width - w, r.height), label, Label, Color.white);
            var b = new Rect(r.xMax - w, r.y + 2f, w, r.height - 4f);
            if (value) Fill(b, Accent * 0.55f);
            if (RawBtn(b, value ? "ON" : "OFF") || TakeSubmit(focused) || TakeAdjust(focused) != 0)
            {
                value = !value;
                Sfx.Play("ui", 0.4f, 0f);
            }
            return value;
        }

        // ------------------------------------------------------------------ cards

        /// <summary>Draws a card. Returns its stat/description layout height.</summary>
        static GUIStyle cardTitle, cardTitleSmall, cardDesc, cardDescSmall, cardStat, cardStatSmall, cardCaption, cardPill;

        /// <summary>
        /// A card: rarity-colored frame (silver / green / blue / gold, purple for curses) with a cartoon of what it
        /// does, a foil shine on Rare and Legendary cards and twinkles on Legendary ones.
        /// </summary>
        public static void DrawCard(Rect r, CardDefinition c, bool highlighted, float alpha = 1f)
        {
            if (c == null) return;
            if (cardTitle == null)
            {
                cardTitle = new GUIStyle(HeaderCenter) { fontSize = 20, wordWrap = true, clipping = TextClipping.Clip };
                cardTitleSmall = new GUIStyle(cardTitle) { fontSize = 16 };
                cardDesc = new GUIStyle(WrapCenter) { fontSize = 14 };
                cardDescSmall = new GUIStyle(cardDesc) { fontSize = 12 };
                cardStat = new GUIStyle(SmallCenter) { fontSize = 14, fontStyle = FontStyle.Bold };
                cardStatSmall = new GUIStyle(cardStat) { fontSize = 12 };
                cardCaption = new GUIStyle(HeaderCenter) { fontSize = 22, fontStyle = FontStyle.Bold };
                cardPill = new GUIStyle(SmallCenter) { fontSize = 11, fontStyle = FontStyle.Bold };
            }
            bool small = r.width < 170f;
            var rc = CardDefinition.FrameColor(c);
            float t = Time.unscaledTime;
            bool rare = c.Rarity >= Rarity.Rare && !c.IsCurse, legendary = c.Rarity == Rarity.Legendary && !c.IsCurse;
            if (highlighted) r.y -= 6f;

            // Shadow and glow.
            Fill(new Rect(r.x + 5f, r.y + 7f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f * alpha));
            if (rare || highlighted)
            {
                float glow = highlighted ? 0.45f : 0.16f + 0.12f * Mathf.Sin(t * (legendary ? 5f : 3f));
                for (int k = 3; k >= 1; k--)
                    Fill(new Rect(r.x - k * 3f, r.y - k * 3f, r.width + k * 6f, r.height + k * 6f), new Color(rc.r, rc.g, rc.b, glow * alpha / k));
            }

            var old = GUI.color;
            // Frame: rarity gradient.
            GUI.color = new Color(rc.r, rc.g, rc.b, alpha);
            GUI.DrawTexture(r, CardArt.Gradient);
            // Inner panel: dark with a hint of the rarity color.
            var inner = new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f);
            var dark = Color.Lerp(new Color(0.07f, 0.075f, 0.11f), rc, 0.12f);
            GUI.color = new Color(dark.r * 1.7f, dark.g * 1.7f, dark.b * 1.7f, alpha);
            GUI.DrawTexture(inner, CardArt.Gradient);
            GUI.color = old;
            Outline(r, new Color(Mathf.Min(1f, rc.r * 1.3f), Mathf.Min(1f, rc.g * 1.3f), Mathf.Min(1f, rc.b * 1.3f), alpha), highlighted ? 3f : 1.5f);

            // Title band.
            float pad = 9f;
            var band = new Rect(inner.x, inner.y, inner.width, small ? 30f : 36f);
            GUI.color = new Color(rc.r * 0.8f, rc.g * 0.8f, rc.b * 0.8f, alpha);
            GUI.DrawTexture(band, CardArt.Gradient);
            GUI.color = old;
            Shadowed(new Rect(band.x + 4f, band.y + 1f, band.width - 8f, band.height), c.Title, small ? cardTitleSmall : cardTitle, new Color(1f, 1f, 1f, alpha));
            float y = band.yMax + 5f;

            // Picture.
            float artW = inner.width - 8f, artH = artW * (CardArt.Height / (float)CardArt.Width);
            artH = Mathf.Min(artH, r.height * 0.4f);
            var art = new Rect(inner.x + 4f, y, artW, artH);
            Fill(new Rect(art.x - 2f, art.y - 2f, art.width + 4f, art.height + 4f), new Color(0.03f, 0.03f, 0.05f, alpha));
            var tex = CardArt.Get(c, out string caption);
            if (tex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(art, tex, ScaleMode.ScaleAndCrop);
                GUI.color = old;
            }
            else Fill(art, new Color(rc.r * 0.5f, rc.g * 0.5f, rc.b * 0.5f, alpha));
            // Category badge (the old pixel icon) in the picture's corner.
            float badge = small ? 20f : 24f;
            CardIcons.Draw(new Rect(art.x + 3f, art.y + 3f, badge, badge), c, alpha);
            // Comic caption.
            if (!string.IsNullOrEmpty(caption) && !small)
            {
                var m = GUI.matrix;
                var pivot = new Vector2(art.xMax - 46f, art.y + 18f);
                GUIUtility.RotateAroundPivot(-9f + Mathf.Sin(t * 2f + c.Id.Length) * 2f, pivot);
                var cr = new Rect(pivot.x - 60f, pivot.y - 14f, 120f, 28f);
                for (int dx = -2; dx <= 2; dx += 2)
                    for (int dy = -2; dy <= 2; dy += 2)
                        if (dx != 0 || dy != 0) Text(new Rect(cr.x + dx, cr.y + dy, cr.width, cr.height), caption, cardCaption, new Color(0.08f, 0.08f, 0.12f, alpha));
                Text(cr, caption, cardCaption, new Color(1f, 0.92f, 0.3f, alpha));
                GUI.matrix = m;
            }
            y = art.yMax + 6f;

            // Rarity pill.
            string rn = CardDefinition.RarityName(c);
            float pw = cardPill.CalcSize(new GUIContent(rn)).x + 18f;
            var pill = new Rect(r.center.x - pw * 0.5f, y, pw, 17f);
            Fill(pill, new Color(rc.r, rc.g, rc.b, 0.9f * alpha));
            Text(pill, rn, cardPill, new Color(0.06f, 0.06f, 0.09f, alpha));
            y = pill.yMax + 4f;
            // Keywords: any two in your deck form a combo.
            var kws = Combos.Of(c);
            if (kws.Length > 0 && !small)
            {
                Text(new Rect(inner.x + 4f, y, inner.width - 8f, 15f), string.Join("  ·  ", kws), cardPill, new Color(1f, 0.85f, 0.4f, 0.85f * alpha));
                y += 15f;
            }
            y += 2f;

            // Description and stats.
            var desc = small ? cardDescSmall : cardDesc;
            float descH = desc.CalcHeight(new GUIContent(c.Description), inner.width - pad * 2f);
            Text(new Rect(inner.x + pad, y, inner.width - pad * 2f, descH), c.Description, desc, new Color(0.93f, 0.94f, 0.98f, alpha));
            y += descH + 6f;
            var stat = small ? cardStatSmall : cardStat;
            foreach (var mod in c.Modifiers)
            {
                if (y > inner.yMax - 22f) break;
                string text = StatInfo.Format(mod, out int polarity);
                Color col = polarity > 0 ? Good : polarity < 0 ? Bad : Muted;
                col.a = alpha;
                Text(new Rect(inner.x + pad, y, inner.width - pad * 2f, 18f), text, stat, col);
                y += 18f;
            }
            if (c.Source != "Spheres" && c.Source != "Curse")
                Text(new Rect(inner.x + pad, inner.yMax - 18f, inner.width - pad * 2f, 16f), c.Source, SmallCenter, new Color(0.6f, 0.62f, 0.7f, alpha));

            // Foil shine sweeping across Rare and Legendary cards.
            if (rare && alpha > 0.05f)
            {
                GUI.BeginGroup(r);
                var m = GUI.matrix;
                float period = legendary ? 2.4f : 3.6f;
                float k = Mathf.Repeat(t / period + c.Id.Length * 0.37f, 1f);
                float bx = Mathf.Lerp(-r.width * 0.6f, r.width * 1.6f, k);
                GUIUtility.RotateAroundPivot(22f, new Vector2(bx, r.height * 0.5f));
                GUI.color = new Color(1f, 1f, 1f, (legendary ? 0.3f : 0.2f) * alpha);
                GUI.DrawTexture(new Rect(bx - 22f, -r.height * 0.4f, 44f, r.height * 1.8f), CardArt.Shine);
                GUI.matrix = m;
                GUI.color = old;
                GUI.EndGroup();
            }
            // Legendary twinkles.
            if (legendary && alpha > 0.05f)
            {
                for (int i = 0; i < 7; i++)
                {
                    float sx = Mathf.Repeat(Mathf.Sin(i * 12.9898f + c.Id.Length) * 43758.5453f, 1f);
                    float sy = Mathf.Repeat(Mathf.Sin(i * 78.233f + c.Id.Length) * 12543.123f, 1f);
                    float tw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.3f + i * 1.9f)), 6f);
                    if (tw < 0.02f) continue;
                    float sz = (small ? 10f : 14f) * (0.6f + tw * 0.6f);
                    GUI.color = new Color(1f, 0.95f, 0.75f, tw * alpha);
                    GUI.DrawTexture(new Rect(r.x + sx * r.width - sz * 0.5f, r.y + sy * r.height - sz * 0.5f, sz, sz), CardArt.Sparkle);
                }
                GUI.color = old;
            }
        }
    }
}
