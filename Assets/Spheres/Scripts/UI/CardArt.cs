using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Card pictures and card-frame textures. Pictures are painted by <see cref="CardArtPainter"/> on demand (a few
    /// per frame), cached, and evicted when many cards have been seen.
    /// </summary>
    public static class CardArt
    {
        public const int Width = 200, Height = 130;

        class Entry { public Texture2D Tex; public string Caption; public int LastUsed; }

        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        static int frame = -1, builtThisFrame;
        const int MaxCached = 160, BuildPerFrame = 3;

        /// <summary>Mods: return your own picture for a card (or null for the generated one).</summary>
        public static System.Func<CardDefinition, Texture2D> Override;

        /// <summary>The card's picture, or null if it's still waiting its turn to be painted.</summary>
        public static Texture2D Get(CardDefinition c, out string caption)
        {
            caption = null;
            if (c == null) return null;
            if (c.ArtTexture != null) return c.ArtTexture;
            if (Override != null)
            {
                try { var t = Override(c); if (t != null) return t; }
                catch (System.Exception ex) { Debug.LogException(ex); }
            }
            if (Time.frameCount != frame) { frame = Time.frameCount; builtThisFrame = 0; }
            if (cache.TryGetValue(c.Id, out var e) && e.Tex != null)
            {
                e.LastUsed = frame;
                caption = e.Caption;
                return e.Tex;
            }
            if (builtThisFrame >= BuildPerFrame) return null;
            builtThisFrame++;

            var painter = new CardArtPainter(Width, Height, StableHash(c.Id));
            var rc = CardDefinition.FrameColor(c);
            string scene = !string.IsNullOrEmpty(c.ArtScene) ? c.ArtScene : c.IsCurse ? "curse" : CardIcons.PrimaryGlyph(c);
            string cap;
            try { cap = painter.Paint(scene, new CardArtPainter.Col(rc.r, rc.g, rc.b), CardIcons.HasDrawback(c), c.Rarity == Rarity.Legendary); }
            catch (System.Exception ex) { Debug.LogException(ex); cap = null; }
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "CardArt " + c.Id,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.LoadRawTextureData(painter.ToRGBA32BottomUp());
            tex.Apply(false, true);
            cache[c.Id] = new Entry { Tex = tex, Caption = cap, LastUsed = frame };
            caption = cap;
            Trim();
            return tex;
        }

        static void Trim()
        {
            if (cache.Count <= MaxCached) return;
            var oldest = new List<KeyValuePair<string, Entry>>(cache);
            oldest.Sort((a, b) => a.Value.LastUsed.CompareTo(b.Value.LastUsed));
            for (int i = 0; i < oldest.Count && cache.Count > MaxCached * 3 / 4; i++)
            {
                if (oldest[i].Value.LastUsed == frame) break;
                Object.Destroy(oldest[i].Value.Tex);
                cache.Remove(oldest[i].Key);
            }
        }

        static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char ch in s ?? "") h = (h ^ ch) * 16777619;
                return h & 0x7fffffff;
            }
        }

        // ---------------------------------------------------------------- frame textures

        static Texture2D gradient, shine, sparkle;

        /// <summary>White at the top fading to 55% grey at the bottom (tint with GUI.color).</summary>
        public static Texture2D Gradient => gradient != null ? gradient : gradient = MakeGradient();
        /// <summary>A soft vertical band of light for the foil sweep.</summary>
        public static Texture2D Shine => shine != null ? shine : shine = MakeShine();
        /// <summary>A four-point twinkle.</summary>
        public static Texture2D Sparkle => sparkle != null ? sparkle : sparkle = MakeSparkle();

        static Texture2D MakeGradient()
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 64; y++)
            {
                float k = Mathf.Lerp(0.55f, 1f, y / 63f); // texture rows go bottom-up
                t.SetPixel(0, y, new Color(k, k, k, 1f));
            }
            t.Apply();
            return t;
        }

        static Texture2D MakeShine()
        {
            var t = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int x = 0; x < 32; x++)
            {
                float d = Mathf.Abs(x - 15.5f) / 15.5f;
                t.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f)));
            }
            t.Apply();
            return t;
        }

        static Texture2D MakeSparkle()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x - 15.5f) / 15.5f, dy = Mathf.Abs(y - 15.5f) / 15.5f;
                    float star = Mathf.Max(Mathf.Clamp01(1f - dx * 6f) * (1f - dy), Mathf.Clamp01(1f - dy * 6f) * (1f - dx));
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(star + core * core)));
                }
            t.Apply();
            return t;
        }
    }
}
