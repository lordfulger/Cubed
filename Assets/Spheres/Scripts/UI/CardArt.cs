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
        static readonly HashSet<string> pending = new HashSet<string>();
        static readonly Queue<(string id, byte[] pixels, string caption)> done = new Queue<(string, byte[], string)>();
        static readonly object gate = new object();
        static int frame = -1, uploadedThisFrame;
        const int MaxCached = 160, UploadPerFrame = 4, MaxPending = 12;

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
            if (Time.frameCount != frame) { frame = Time.frameCount; uploadedThisFrame = 0; }
            Upload();
            if (cache.TryGetValue(c.Id, out var e) && e.Tex != null)
            {
                e.LastUsed = frame;
                caption = e.Caption;
                return e.Tex;
            }
            Request(c);
            return null;
        }

        /// <summary>Paint a card's picture on a worker thread (the painter is plain C#).</summary>
        static void Request(CardDefinition c)
        {
            lock (gate)
            {
                if (pending.Contains(c.Id) || pending.Count >= MaxPending) return;
                pending.Add(c.Id);
            }
            var rc = CardDefinition.FrameColor(c);
            string id = c.Id;
            string scene = !string.IsNullOrEmpty(c.ArtScene) ? c.ArtScene : c.IsCurse ? "curse" : CardIcons.PrimaryGlyph(c);
            var bg = new CardArtPainter.Col(rc.r, rc.g, rc.b);
            bool drawback = CardIcons.HasDrawback(c), legendary = c.Rarity == Rarity.Legendary;
            int seed = StableHash(id);
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                byte[] pixels = null;
                string cap = null;
                try
                {
                    var painter = new CardArtPainter(Width, Height, seed);
                    cap = painter.Paint(scene, bg, drawback, legendary);
                    pixels = painter.ToRGBA32BottomUp();
                }
                catch (System.Exception) { pixels = null; }
                lock (gate) done.Enqueue((id, pixels, cap));
            });
        }

        /// <summary>Turn finished paintings into textures (main thread, a few per frame).</summary>
        static void Upload()
        {
            while (uploadedThisFrame < UploadPerFrame)
            {
                (string id, byte[] pixels, string caption) item;
                lock (gate)
                {
                    if (done.Count == 0) return;
                    item = done.Dequeue();
                    pending.Remove(item.id);
                }
                if (item.pixels == null) continue;
                uploadedThisFrame++;
                var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
                {
                    name = "CardArt " + item.id,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.LoadRawTextureData(item.pixels);
                tex.Apply(false, true);
                if (cache.TryGetValue(item.id, out var old) && old.Tex != null) Object.Destroy(old.Tex);
                cache[item.id] = new Entry { Tex = tex, Caption = item.caption, LastUsed = frame };
                Trim();
            }
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
