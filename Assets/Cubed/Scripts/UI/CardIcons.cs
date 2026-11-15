using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// Original 12x12 pixel-art glyphs generated at runtime and chosen per card from its main stat or effect.
    /// Mods can register more glyphs and map effect ids to them.
    /// </summary>
    public static class CardIcons
    {
        static readonly Dictionary<string, string[]> art = new Dictionary<string, string[]>
        {
            ["heart"] = new[] { "............", ".XX....XX...", "XXXX..XXXX..", "XXXXXXXXXX..", "XXXXXXXXXX..", "XXXXXXXXXX..", ".XXXXXXXX...", "..XXXXXX....", "...XXXX.....", "....XX......", "............", "............" },
            ["bolt"] = new[] { "......XXX...", ".....XXX....", "....XXX.....", "...XXX......", "..XXXXXXX...", ".....XXX....", "....XXX.....", "...XXX......", "..XX........", ".X..........", "............", "............" },
            ["arrow_up"] = new[] { ".....XX.....", "....XXXX....", "...XXXXXX...", "..XXXXXXXX..", ".XXX.XX.XXX.", ".....XX.....", ".....XX.....", ".....XX.....", ".....XX.....", ".....XX.....", "............", "............" },
            ["sword"] = new[] { "..........XX", ".........XXX", "........XXX.", ".......XXX..", "......XXX...", ".X...XXX....", "..X.XXX.....", "...XXX......", "...XX.......", "..X.X.......", ".X..........", "............" },
            ["clock"] = new[] { "...XXXXXX...", "..X......X..", ".X...X....X.", ".X...X....X.", ".X...X....X.", ".X...XXX..X.", ".X........X.", ".X........X.", "..X......X..", "...XXXXXX...", "............", "............" },
            ["ammo"] = new[] { "..X..X..X...", ".XXX.XXX.XXX", ".XXX.XXX.XXX", ".XXX.XXX.XXX", ".XXX.XXX.XXX", ".XXX.XXX.XXX", ".XXX.XXX.XXX", ".XXX.XXX.XXX", ".XXX.XXX.XXX", "............", "............", "............" },
            ["reload"] = new[] { "...XXXXX....", "..X.....X.X.", ".X.......XX.", ".X......XXX.", ".X..........", ".X..........", ".XXX......X.", ".XX.......X.", ".X.X.....X..", "....XXXXX...", "............", "............" },
            ["shield"] = new[] { ".XXXXXXXXXX.", ".X........X.", ".X..XXXX..X.", ".X..XXXX..X.", ".X..XXXX..X.", "..X.XXXX.X..", "..X..XX..X..", "...X....X...", "....X..X....", ".....XX.....", "............", "............" },
            ["drop"] = new[] { ".....X......", ".....X......", "....XXX.....", "...XXXXX....", "..XXXXXXX...", "..XXXXXXX...", "..XXXXXXX...", "..XXXXXXX...", "...XXXXX....", "....XXX.....", "............", "............" },
            ["plus"] = new[] { "............", "....XXXX....", "....XXXX....", "....XXXX....", "XXXXXXXXXXXX", "XXXXXXXXXXXX", "XXXXXXXXXXXX", "....XXXX....", "....XXXX....", "....XXXX....", "............", "............" },
            ["target"] = new[] { "...XXXXXX...", "..X......X..", ".X..XXXX..X.", ".X.X....X.X.", ".X.X.XX.X.X.", ".X.X.XX.X.X.", ".X.X....X.X.", ".X..XXXX..X.", "..X......X..", "...XXXXXX...", "............", "............" },
            ["bomb"] = new[] { "........X.X.", ".......X.X..", "......X.....", "....XXXX....", "..XXXXXXXX..", ".XXXXXXXXXX.", ".XXXXXXXXXX.", ".XXXXXXXXXX.", ".XXXXXXXXXX.", "..XXXXXXXX..", "....XXXX....", "............" },
            ["drill"] = new[] { "............", "XXXX........", "XXXXXX......", "XXXXXXXX....", "XXXXXXXXXX..", "XXXXXXXXXXXX", "XXXXXXXXXX..", "XXXXXXXX....", "XXXXXX......", "XXXX........", "............", "............" },
            ["star"] = new[] { ".....XX.....", ".....XX.....", "....XXXX....", "XXXXXXXXXXXX", ".XXXXXXXXXX.", "..XXXXXXXX..", "...XXXXXX...", "..XXX..XXX..", ".XX......XX.", "X..........X", "............", "............" },
            ["card"] = new[] { "..XXXXXXX...", "..X.....XX..", "..X.....X.X.", "..X..X..XXX.", "..X.XXX...X.", "..X..X....X.", "..X.......X.", "..X.......X.", "..X.......X.", "..XXXXXXXXX.", "............", "............" },
            ["bounce"] = new[] { "............", "X.........X.", "X........X..", ".X......X...", ".X.....X....", "..X...X.....", "..X..X......", "...XX.......", "...X........", "XXXXXXXXXXXX", "............", "............" },
            ["skull"] = new[] { "..XXXXXXXX..", ".XXXXXXXXXX.", "XXXXXXXXXXXX", "XX..XXXX..XX", "XX..XXXX..XX", "XXXXXXXXXXXX", ".XXXX..XXXX.", "..XXXXXXXX..", "..X.X.X.X...", "............", "............", "............" },
            ["gear"] = new[] { ".....XX.....", "..X.XXXX.X..", "..XXXXXXXX..", ".XXXX..XXXX.", "XXXX....XXXX", "XXXX....XXXX", ".XXXX..XXXX.", "..XXXXXXXX..", "..X.XXXX.X..", ".....XX.....", "............", "............" },
            ["feather"] = new[] { ".........XX.", "........XXX.", ".......XXXX.", "......XXXX..", ".....XXXX...", "....XXXX....", "...XXXX.....", "..XXX.......", ".XX.........", "X...........", "............", "............" },
            ["wave"] = new[] { "............", "XX....XX....", "..X..X..X...", "...XX....XXX", "............", "XX....XX....", "..X..X..X...", "...XX....XXX", "............", "............", "............", "............" },
            ["turret"] = new[] { "............", "....XXXXXXXX", "...XXXXX....", "..XXXXXXX...", "..XXXXXXX...", "...XXXXX....", "....XXX.....", "..XXXXXXX...", ".XXXXXXXXX..", ".XXXXXXXXX..", "............", "............" },
            ["flame"] = new[] { ".....X......", "....XX......", "....XXX..X..", "...XXXX.XX..", "..XXXXXXXX..", "..XXX.XXXX..", ".XXX...XXXX.", ".XX.....XXX.", ".XXX...XXXX.", "..XXXXXXXX..", "............", "............" },
            ["snow"] = new[] { ".....XX.....", "..X..XX..X..", "...X.XX.X...", "....XXXX....", "XXXXXXXXXXXX", "....XXXX....", "...X.XX.X...", "..X..XX..X..", ".....XX.....", "............", "............", "............" },
            ["echo"] = new[] { "............", "X...X...X...", "XX..XX..XX..", "XXX.XXX.XXX.", "XXXXXXXXXXXX", "XXX.XXX.XXX.", "XX..XX..XX..", "X...X...X...", "............", "............", "............", "............" },
            ["down"] = new[] { "....XX......", "....XX......", "....XX......", "....XX......", ".XX.XX.XX...", "..XXXXXX....", "...XXXX.....", "....XX......", "............", "............", "............", "............" },
        };

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        static readonly Dictionary<Stat, string> statGlyph = new Dictionary<Stat, string>
        {
            { Stat.MaxHealth, "heart" }, { Stat.Regen, "plus" }, { Stat.Lifesteal, "drop" }, { Stat.ExtraLives, "star" },
            { Stat.MoveSpeed, "bolt" }, { Stat.JumpHeight, "arrow_up" }, { Stat.ExtraJumps, "arrow_up" }, { Stat.AirControl, "feather" },
            { Stat.Damage, "sword" }, { Stat.AttackCooldown, "clock" }, { Stat.Ammo, "ammo" }, { Stat.ReloadTime, "reload" },
            { Stat.BulletSpeed, "bolt" }, { Stat.BulletGravity, "feather" }, { Stat.BulletsPerShot, "echo" }, { Stat.Spread, "target" },
            { Stat.Bounces, "bounce" }, { Stat.BulletSize, "bomb" }, { Stat.BulletLifetime, "target" }, { Stat.Knockback, "wave" },
            { Stat.BlockCooldown, "shield" }, { Stat.BlockDuration, "shield" }, { Stat.Homing, "target" }, { Stat.ExplosionRadius, "bomb" },
            { Stat.Pierce, "drill" }, { Stat.PlayerSize, "gear" }, { Stat.DamageTaken, "shield" }, { Stat.HazardDamageTaken, "feather" },
            { Stat.HandSize, "card" },
        };

        /// <summary>Effect id fragment → glyph. Checked in order.</summary>
        public static readonly List<(string fragment, string glyph)> EffectGlyphs = new List<(string, string)>
        {
            ("frost", "snow"), ("venom", "drop"), ("decay", "skull"), ("medic", "plus"), ("mending", "plus"), ("trophy", "star"),
            ("sentry", "turret"), ("mine", "bomb"), ("fuse", "bomb"), ("orbit", "gear"), ("guard", "shield"), ("barrier", "shield"),
            ("mirror", "shield"), ("wall", "shield"), ("echo", "echo"), ("arc", "bolt"), ("stun", "bolt"), ("hush", "skull"),
            ("bounce", "bounce"), ("rebound", "bounce"), ("boomerang", "reload"), ("ghost", "drill"), ("gravity", "wave"),
            ("growth", "bomb"), ("split", "echo"), ("phase", "feather"), ("recoil", "arrow_up"), ("adrenaline", "flame"),
            ("frenzy", "flame"), ("blood", "drop"), ("execut", "skull"), ("slayer", "sword"), ("backstab", "sword"),
            ("quarters", "sword"), ("long_shot", "target"), ("steady", "target"), ("momentum", "bolt"), ("overcharge", "bolt"),
            ("refund", "ammo"), ("clip", "ammo"), ("riposte", "sword"), ("thorns", "wave"), ("last_stand", "heart"), ("second", "clock"),
        };

        /// <summary>Add a custom glyph (12 strings of 12 chars, 'X' = filled).</summary>
        public static void Register(string id, string[] rows)
        {
            art[id] = rows;
            textures.Remove(id);
        }

        public static Texture2D Glyph(string id)
        {
            if (textures.TryGetValue(id, out var t) && t != null) return t;
            if (!art.TryGetValue(id, out var rows)) rows = art["gear"];
            int h = rows.Length, w = rows[0].Length;
            t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    t.SetPixel(x, h - 1 - y, x < rows[y].Length && rows[y][x] == 'X' ? Color.white : Color.clear);
            t.Apply();
            textures[id] = t;
            return t;
        }

        /// <summary>The main glyph for a card: its first effect, else its first positive modifier.</summary>
        public static string PrimaryGlyph(CardDefinition c)
        {
            foreach (var e in c.Effects)
                foreach (var (fragment, glyph) in EffectGlyphs)
                    if (e.Contains(fragment)) return glyph;
            foreach (var m in c.Modifiers)
            {
                StatInfo.Format(m, out int pol);
                if (pol >= 0 && statGlyph.TryGetValue(m.Stat, out var g)) return g;
            }
            return c.Modifiers.Count > 0 && statGlyph.TryGetValue(c.Modifiers[0].Stat, out var f) ? f : "card";
        }

        public static bool HasDrawback(CardDefinition c)
        {
            foreach (var m in c.Modifiers)
            {
                StatInfo.Format(m, out int pol);
                if (pol < 0) return true;
            }
            return false;
        }

        /// <summary>Draw a card's icon tile.</summary>
        public static void Draw(Rect r, CardDefinition c, float alpha)
        {
            UIKit.Fill(r, new Color(c.Color.r * 0.35f, c.Color.g * 0.35f, c.Color.b * 0.35f, 0.95f * alpha));
            UIKit.Outline(r, new Color(c.Color.r, c.Color.g, c.Color.b, alpha), 2f);
            var old = GUI.color;
            var inner = new Rect(r.x + r.width * 0.14f, r.y + r.height * 0.14f, r.width * 0.72f, r.height * 0.72f);
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            GUI.DrawTexture(new Rect(inner.x + 2f, inner.y + 2f, inner.width, inner.height), Glyph(PrimaryGlyph(c)));
            GUI.color = Color.Lerp(c.Color, Color.white, 0.55f) * new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(inner, Glyph(PrimaryGlyph(c)));
            if (HasDrawback(c))
            {
                var corner = new Rect(r.xMax - r.width * 0.36f, r.yMax - r.height * 0.36f, r.width * 0.32f, r.height * 0.32f);
                GUI.color = new Color(0.1f, 0.02f, 0.02f, 0.85f * alpha);
                GUI.DrawTexture(corner, Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.4f, 0.35f, alpha);
                GUI.DrawTexture(corner, Glyph("down"));
            }
            GUI.color = old;
        }
    }
}
