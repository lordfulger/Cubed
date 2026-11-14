using System.Collections.Generic;
using UnityEngine;

namespace Cubed
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

        /// <summary>Call at the start of OnGUI.</summary>
        public static void Begin()
        {
            if (!initialized) Init();
            Scale = Mathf.Clamp(Screen.height / VirtualHeight, 0.5f, 4f);
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
            GUI.Label(r, text, style);
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
            bool clicked = GUI.Button(r, text, small ? ButtonSmall : Button);
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
            bool clicked = GUI.Button(r, text, style);
            style.normal.background = old;
            if (clicked) Sfx.Play("ui", 0.4f, 0f);
            return clicked;
        }

        /// <summary>"Label   [-] value [+]" row. Returns the new value.</summary>
        public static float Stepper(Rect r, string label, float value, float step, float min, float max, string format = "0", string suffix = "", string zeroText = null)
        {
            float bw = 34f;
            Text(new Rect(r.x, r.y, r.width - bw * 2f - 90f, r.height), label, Label, Color.white);
            float x = r.xMax - bw * 2f - 90f;
            if (Btn(new Rect(x, r.y + 2f, bw, r.height - 4f), "-")) value = Mathf.Max(min, value - step);
            string shown = zeroText != null && Mathf.Approximately(value, 0f) ? zeroText : value.ToString(format) + suffix;
            Text(new Rect(x + bw, r.y, 90f, r.height), shown, LabelCenter, Accent);
            if (Btn(new Rect(x + bw + 90f, r.y + 2f, bw, r.height - 4f), "+")) value = Mathf.Min(max, value + step);
            return value;
        }

        public static int StepperInt(Rect r, string label, int value, int min, int max, string zeroText = null) =>
            Mathf.RoundToInt(Stepper(r, label, value, 1f, min, max, "0", "", zeroText));

        /// <summary>"Label   [ < option > ]" row cycling through options. Returns the new index.</summary>
        public static int Cycle(Rect r, string label, int index, string[] options)
        {
            float w = Mathf.Min(220f, r.width * 0.55f);
            Text(new Rect(r.x, r.y, r.width - w, r.height), label, Label, Color.white);
            var b = new Rect(r.xMax - w, r.y + 2f, w, r.height - 4f);
            if (Btn(b, "<  " + options[Mathf.Clamp(index, 0, options.Length - 1)] + "  >"))
            {
                bool back = Event.current != null && Event.current.button == 1;
                index = (index + (back ? -1 : 1) + options.Length) % options.Length;
            }
            return index;
        }

        public static bool CheckRow(Rect r, string label, bool value)
        {
            float w = Mathf.Min(220f, r.width * 0.55f);
            Text(new Rect(r.x, r.y, r.width - w, r.height), label, Label, Color.white);
            if (Toggle(new Rect(r.xMax - w, r.y + 2f, w, r.height - 4f), value ? "ON" : "OFF", value)) value = !value;
            return value;
        }

        // ------------------------------------------------------------------ cards

        /// <summary>Draws a card. Returns its stat/description layout height.</summary>
        public static void DrawCard(Rect r, CardDefinition c, bool highlighted, float alpha = 1f)
        {
            if (c == null) return;
            var rc = CardDefinition.RarityColor(c.Rarity);
            if (highlighted) Fill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(rc.r, rc.g, rc.b, 0.35f * alpha));
            Fill(r, new Color(0.1f, 0.11f, 0.16f, 0.97f * alpha));
            Fill(new Rect(r.x, r.y, r.width, 54f), new Color(c.Color.r * 0.75f, c.Color.g * 0.75f, c.Color.b * 0.75f, alpha));
            Outline(r, new Color(rc.r, rc.g, rc.b, alpha), highlighted ? 4f : 2f);

            float pad = 10f;
            float y = r.y + 6f;
            var title = new GUIStyle(HeaderCenter) { fontSize = r.width < 170f ? 17 : 20, wordWrap = true };
            Shadowed(new Rect(r.x + pad, y, r.width - pad * 2f, 30f), c.Title, title, new Color(1f, 1f, 1f, alpha));
            y += 30f;
            Text(new Rect(r.x + pad, y, r.width - pad * 2f, 18f), c.Rarity.ToString().ToUpperInvariant(), SmallCenter, new Color(rc.r, rc.g, rc.b, alpha));
            y += 24f;

            // Big cube icon.
            float icon = Mathf.Min(r.width * 0.32f, 56f);
            var iconRect = new Rect(r.center.x - icon * 0.5f, y, icon, icon);
            Fill(iconRect, new Color(c.Color.r, c.Color.g, c.Color.b, 0.9f * alpha));
            Fill(new Rect(iconRect.x + icon * 0.15f, iconRect.y + icon * 0.15f, icon * 0.3f, icon * 0.3f), new Color(1f, 1f, 1f, 0.35f * alpha));
            y += icon + 10f;

            var wrap = new GUIStyle(WrapCenter) { fontSize = r.width < 170f ? 13 : 15 };
            float descH = wrap.CalcHeight(new GUIContent(c.Description), r.width - pad * 2f);
            Text(new Rect(r.x + pad, y, r.width - pad * 2f, descH), c.Description, wrap, new Color(0.92f, 0.93f, 0.97f, alpha));
            y += descH + 8f;

            var small = new GUIStyle(SmallCenter) { fontSize = r.width < 170f ? 13 : 15 };
            foreach (var m in c.Modifiers)
            {
                if (y > r.yMax - 36f) break;
                string text = StatInfo.Format(m, out int polarity);
                Color col = polarity > 0 ? Good : polarity < 0 ? Bad : Muted;
                col.a = alpha;
                Text(new Rect(r.x + pad, y, r.width - pad * 2f, 20f), text, small, col);
                y += 20f;
            }

            if (c.Source != "Cubed")
                Text(new Rect(r.x + pad, r.yMax - 22f, r.width - pad * 2f, 18f), c.Source, SmallCenter, new Color(0.6f, 0.62f, 0.7f, alpha));
        }
    }
}
