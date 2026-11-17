using System;
using System.Collections.Generic;

namespace Spheres
{
    /// <summary>
    /// Paints the little cartoon on each card: a sphere character acting out what the card does (a vampire for
    /// lifesteal, a spring for jump height, buried in bullets for ammo...). Pure C# with anti-aliased shapes so it
    /// runs anywhere; <see cref="CardArt"/> turns the pixels into textures. All art is original and generated.
    /// </summary>
    public class CardArtPainter
    {
        public struct Col
        {
            public float R, G, B, A;
            public Col(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
            public Col WithA(float a) => new Col(R, G, B, a);
            public Col Mul(float k) => new Col(R * k, G * k, B * k, A);
            public static Col Lerp(Col a, Col b, float t) => new Col(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        public enum Mood { Happy, Angry, Panic, Smug, Dizzy, Cool, Sleepy, Sad }

        /// <summary>Logical size (what scenes draw in). The canvas is K times bigger and averaged down at the end.</summary>
        public readonly int W, H;
        const int K = 2;
        readonly int PW, PH;
        readonly float[] px; // RGBA at K x resolution, row 0 = top
        readonly Random rng;
        static readonly Col Ink = new Col(0.08f, 0.08f, 0.12f);
        static readonly Col White = new Col(1f, 1f, 1f);

        public CardArtPainter(int w, int h, int seed)
        {
            W = w; H = h;
            PW = w * K; PH = h * K;
            px = new float[PW * PH * 4];
            rng = new Random(seed);
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------- output

        /// <summary>RGBA bytes with row 0 at the bottom (texture order).</summary>
        public byte[] ToRGBA32BottomUp()
        {
            var o = new byte[W * H * 4];
            float inv = 1f / (K * K);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int dst = ((H - 1 - y) * W + x) * 4;
                    for (int ch = 0; ch < 4; ch++)
                    {
                        float sum = 0f;
                        for (int sy = 0; sy < K; sy++)
                            for (int sx = 0; sx < K; sx++)
                                sum += px[((y * K + sy) * PW + x * K + sx) * 4 + ch];
                        o[dst + ch] = (byte)Math.Round(Math.Max(0f, Math.Min(1f, sum * inv)) * 255f);
                    }
                }
            return o;
        }

        // ---------------------------------------------------------------- raster core

        void Blend(int x, int y, Col c, float cov)
        {
            if (x < 0 || y < 0 || x >= PW || y >= PH || cov <= 0f) return;
            float a = c.A * Math.Min(1f, cov);
            int i = (y * PW + x) * 4;
            px[i] += (c.R - px[i]) * a;
            px[i + 1] += (c.G - px[i + 1]) * a;
            px[i + 2] += (c.B - px[i + 2]) * a;
            px[i + 3] = px[i + 3] + (1f - px[i + 3]) * a;
        }

        /// <summary>Fill everything where sdf(x,y) &lt; 0, anti-aliased over one pixel.</summary>
        void Shape(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Col c) =>
            ShapeColored(x0, y0, x1, y1, sdf, null, c, 0f);

        /// <summary>
        /// Rasterize where sdf &lt; 0 (logical coords), at K x resolution. color(x,y) gives per-pixel color when set.
        /// soft &gt; 0 widens the edge into a soft falloff of that many logical pixels (shadows, glows).
        /// </summary>
        void ShapeColored(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Func<float, float, Col> color, Col c, float soft)
        {
            int ax = Math.Max(0, (int)Math.Floor(x0 * K) - 2), ay = Math.Max(0, (int)Math.Floor(y0 * K) - 2);
            int bx = Math.Min(PW - 1, (int)Math.Ceiling(x1 * K) + 2), by = Math.Min(PH - 1, (int)Math.Ceiling(y1 * K) + 2);
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                {
                    float lx = (x + 0.5f) / K, ly = (y + 0.5f) / K;
                    float d = sdf(lx, ly);
                    float cov = soft > 0f ? Clamp01(0.5f - d / soft) : 0.5f - d * K;
                    if (cov > 0f) Blend(x, y, color != null ? color(lx, ly) : c, cov);
                }
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Draw a filled shape with an ink outline of the given width.</summary>
        void Outlined(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Col fill, float outline)
        {
            // Outline as a band around the outside only, so see-through fills (ice, bubbles) stay see-through.
            if (outline > 0f) Shape(x0 - outline, y0 - outline, x1 + outline, y1 + outline, (x, y) => { float d = sdf(x, y); return Math.Max(d - outline, -d - 0.75f); }, Ink);
            Shape(x0, y0, x1, y1, sdf, fill);
        }

        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
            float t = Math.Max(0f, Math.Min(1f, (wx * vx + wy * vy) / Math.Max(1e-6f, vx * vx + vy * vy)));
            return Len(wx - vx * t, wy - vy * t);
        }

        // ---------------------------------------------------------------- primitives

        public void Gradient(Col top, Col bottom)
        {
            for (int y = 0; y < PH; y++)
            {
                var c = Col.Lerp(top, bottom, y / (float)(PH - 1));
                for (int x = 0; x < PW; x++)
                {
                    int i = (y * PW + x) * 4;
                    px[i] = c.R; px[i + 1] = c.G; px[i + 2] = c.B; px[i + 3] = 1f;
                }
            }
        }

        public void Rays(float cx, float cy, int n, Col c, float spin = 0f)
        {
            for (int k = 0; k < n; k++)
            {
                float a0 = spin + k * (float)Math.PI * 2f / n, a1 = a0 + (float)Math.PI / n;
                float r = W + H;
                Poly(new[] { cx, cy, cx + (float)Math.Cos(a0) * r, cy + (float)Math.Sin(a0) * r, cx + (float)Math.Cos(a1) * r, cy + (float)Math.Sin(a1) * r }, c, 0f);
            }
        }

        public void Dots(Col c, float spacing, float radius)
        {
            for (float y = spacing * 0.5f; y < H; y += spacing)
                for (float x = ((int)(y / spacing) % 2 == 0 ? 0f : spacing * 0.5f); x < W; x += spacing)
                    Circle(x, y, radius, c, 0f);
        }

        public void Circle(float cx, float cy, float r, Col fill, float outline = 3f) =>
            Outlined(cx - r, cy - r, cx + r, cy + r, (x, y) => Len(x - cx, y - cy) - r, fill, outline);

        public void Ellipse(float cx, float cy, float rx, float ry, float angleDeg, Col fill, float outline = 3f)
        {
            float a = angleDeg * (float)Math.PI / 180f, cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
            float m = Math.Max(rx, ry);
            Outlined(cx - m, cy - m, cx + m, cy + m, (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                float k = Len(lx / rx, ly / ry);
                return (k - 1f) * Math.Min(rx, ry);
            }, fill, outline);
        }

        /// <summary>A lit 3D-looking ball: diffuse light from the top left, a specular highlight and a rim light.</summary>
        public void Ball(float cx, float cy, float r, Col baseCol, float outline = 3f)
        {
            if (outline > 0f) Shape(cx - r - outline, cy - r - outline, cx + r + outline, cy + r + outline, (x, y) => { float d = Len(x - cx, y - cy) - r; return Math.Max(d - outline, -d - 0.75f); }, Col.Lerp(baseCol.Mul(0.25f), Ink, 0.5f));
            const float lx = -0.45f, ly = -0.62f, lz = 0.64f;
            ShapeColored(cx - r, cy - r, cx + r, cy + r, (x, y) => Len(x - cx, y - cy) - r, (x, y) =>
            {
                float nx = (x - cx) / r, ny = (y - cy) / r;
                float nz2 = 1f - nx * nx - ny * ny;
                float nz = nz2 > 0f ? (float)Math.Sqrt(nz2) : 0f;
                float lam = Math.Max(0f, nx * lx + ny * ly + nz * lz);
                // Blinn-ish highlight toward the viewer.
                float hx = lx, hy = ly, hz = lz + 1f, hl = Len(Len(hx, hy), hz);
                float spec = (float)Math.Pow(Math.Max(0f, (nx * hx + ny * hy + nz * hz) / hl), 28.0);
                float rim = (float)Math.Pow(1f - nz, 3.0) * 0.35f;
                float k = 0.52f + 0.62f * lam;
                return new Col(Math.Min(1f, baseCol.R * k + spec * 0.85f + rim * 0.5f), Math.Min(1f, baseCol.G * k + spec * 0.85f + rim * 0.6f), Math.Min(1f, baseCol.B * k + spec * 0.85f + rim * 0.8f), baseCol.A);
            }, baseCol, 0f);
        }

        /// <summary>A soft drop shadow (blurry ellipse).</summary>
        public void SoftShadow(float cx, float cy, float rx, float ry, float alpha = 0.35f)
        {
            float soft = ry * 1.2f;
            ShapeColored(cx - rx - soft, cy - ry - soft, cx + rx + soft, cy + ry + soft,
                (x, y) => (Len((x - cx) / rx, (y - cy) / ry) - 1f) * ry, null, new Col(0f, 0f, 0f, alpha), soft);
        }

        /// <summary>A soft radial glow.</summary>
        public void Glow(float cx, float cy, float r, Col c)
        {
            ShapeColored(cx - r, cy - r, cx + r, cy + r, (x, y) => Len(x - cx, y - cy) - r * 0.35f, null, c, r * 1.3f);
        }

        /// <summary>Darken the corners (drawn last).</summary>
        public void Vignette(float strength)
        {
            for (int y = 0; y < PH; y++)
                for (int x = 0; x < PW; x++)
                {
                    float dx = (x / (float)PW - 0.5f) * 2f, dy = (y / (float)PH - 0.5f) * 2f;
                    float v = Clamp01((dx * dx * 0.6f + dy * dy * 0.8f - 0.35f) * strength);
                    int i = (y * PW + x) * 4;
                    px[i] *= 1f - v; px[i + 1] *= 1f - v; px[i + 2] *= 1f - v;
                }
        }

        /// <summary>Arena backdrop: sky, soft clouds, a spotlight behind the hero, and a floor with perspective lines.</summary>
        void Backdrop(Col bg, bool legendary)
        {
            var top = Col.Lerp(bg, White, 0.5f);
            var mid = Col.Lerp(bg, White, 0.15f);
            Gradient(top, mid);
            if (legendary) Rays(W * 0.5f, H * 0.55f, 16, White.WithA(0.16f), Rand(0f, 1f));
            // Clouds.
            for (int i = 0; i < 4; i++)
            {
                float cx = Rand(0f, W), cy = Rand(H * 0.08f, H * 0.4f), s = Rand(10f, 20f);
                var cc = White.WithA(0.32f);
                Glow(cx, cy, s * 1.4f, cc);
                Glow(cx + s, cy + 3f, s, cc);
            }
            Glow(W * 0.5f, H * 0.5f, H * 0.6f, White.WithA(0.35f));
            // Floor.
            float horizon = H * 0.74f;
            var floorTop = Col.Lerp(bg, new Col(0.12f, 0.12f, 0.2f), 0.45f);
            var floorBottom = Col.Lerp(bg, new Col(0.05f, 0.05f, 0.1f), 0.7f);
            ShapeColored(0f, horizon, W, H, (x, y) => horizon - y, (x, y) => Col.Lerp(floorTop, floorBottom, (y - horizon) / (H - horizon)), floorTop, 0f);
            for (int i = -6; i <= 6; i++) Line(W * 0.5f + i * 12f, horizon, W * 0.5f + i * 45f, H + 2f, 1f, White.WithA(0.08f));
            for (int i = 1; i <= 3; i++) Line(0f, horizon + (H - horizon) * i * i / 10f, W, horizon + (H - horizon) * i * i / 10f, 1f, White.WithA(0.07f));
            Line(0f, horizon, W, horizon, 1.5f, White.WithA(0.18f));
        }

        public void Line(float x0, float y0, float x1, float y1, float width, Col c)
        {
            float h = width * 0.5f;
            Shape(Math.Min(x0, x1) - h, Math.Min(y0, y1) - h, Math.Max(x0, x1) + h, Math.Max(y0, y1) + h,
                (x, y) => SegDist(x, y, x0, y0, x1, y1) - h, c);
        }

        public void Polyline(float[] p, float width, Col c)
        {
            for (int i = 0; i + 3 < p.Length; i += 2) Line(p[i], p[i + 1], p[i + 2], p[i + 3], width, c);
        }

        public void RoundRect(float cx, float cy, float w, float h, float r, float angleDeg, Col fill, float outline = 3f)
        {
            float a = angleDeg * (float)Math.PI / 180f, cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
            float hw = w * 0.5f - r, hh = h * 0.5f - r, m = Len(w, h) * 0.5f;
            Outlined(cx - m, cy - m, cx + m, cy + m, (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float lx = Math.Abs(dx * cs + dy * sn) - hw, ly = Math.Abs(-dx * sn + dy * cs) - hh;
                return Len(Math.Max(lx, 0f), Math.Max(ly, 0f)) + Math.Min(Math.Max(lx, ly), 0f) - r;
            }, fill, outline);
        }

        public void Poly(float[] p, Col fill, float outline = 3f)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < p.Length; i += 2) { x0 = Math.Min(x0, p[i]); x1 = Math.Max(x1, p[i]); y0 = Math.Min(y0, p[i + 1]); y1 = Math.Max(y1, p[i + 1]); }
            int n = p.Length / 2;
            Outlined(x0, y0, x1, y1, (x, y) =>
            {
                bool inside = false;
                float d = float.MaxValue;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                    if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi + 1e-9f) + xi)) inside = !inside;
                    d = Math.Min(d, SegDist(x, y, xi, yi, xj, yj));
                }
                return inside ? -d : d;
            }, fill, outline);
        }

        public void Star(float cx, float cy, float ro, float ri, int points, float rotDeg, Col fill, float outline = 3f)
        {
            var p = new float[points * 4];
            for (int k = 0; k < points * 2; k++)
            {
                float a = (rotDeg - 90f) * (float)Math.PI / 180f + k * (float)Math.PI / points;
                float r = k % 2 == 0 ? ro : ri;
                p[k * 2] = cx + (float)Math.Cos(a) * r;
                p[k * 2 + 1] = cy + (float)Math.Sin(a) * r;
            }
            Poly(p, fill, outline);
        }

        public void Heart(float cx, float cy, float s, Col fill, float outline = 3f)
        {
            // Two lobes and a point, as one implicit shape.
            Outlined(cx - s * 1.1f, cy - s * 0.9f, cx + s * 1.1f, cy + s * 1.1f, (x, y) =>
            {
                float l = Len(x - (cx - s * 0.5f), y - (cy - s * 0.25f)) - s * 0.55f;
                float r = Len(x - (cx + s * 0.5f), y - (cy - s * 0.25f)) - s * 0.55f;
                float tri = TriDist(x, y, cx - s * 1.02f, cy - s * 0.05f, cx + s * 1.02f, cy - s * 0.05f, cx, cy + s);
                return Math.Min(Math.Min(l, r), tri);
            }, fill, outline);
        }

        static float TriDist(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d = Math.Min(SegDist(x, y, ax, ay, bx, by), Math.Min(SegDist(x, y, bx, by, cx, cy), SegDist(x, y, cx, cy, ax, ay)));
            float s1 = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
            float s2 = (cx - bx) * (y - by) - (cy - by) * (x - bx);
            float s3 = (ax - cx) * (y - cy) - (ay - cy) * (x - cx);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        public void Arc(float cx, float cy, float r, float a0Deg, float a1Deg, float width, Col c)
        {
            int steps = Math.Max(4, (int)(Math.Abs(a1Deg - a0Deg) / 10f));
            var p = new float[(steps + 1) * 2];
            for (int i = 0; i <= steps; i++)
            {
                float a = (a0Deg + (a1Deg - a0Deg) * i / steps) * (float)Math.PI / 180f;
                p[i * 2] = cx + (float)Math.Cos(a) * r;
                p[i * 2 + 1] = cy + (float)Math.Sin(a) * r;
            }
            Polyline(p, width, c);
        }

        public void Sweat(float x, float y, float s)
        {
            Poly(new[] { x, y - s * 1.3f, x - s * 0.7f, y + s * 0.1f, x + s * 0.7f, y + s * 0.1f }, new Col(0.55f, 0.85f, 1f), 0f);
            Circle(x, y + s * 0.25f, s * 0.7f, new Col(0.55f, 0.85f, 1f), 1.5f);
        }

        public void Puff(float x, float y, float s, Col c)
        {
            Circle(x, y, s, c, 2f);
            Circle(x + s * 0.8f, y - s * 0.2f, s * 0.75f, c, 2f);
            Circle(x - s * 0.8f, y + s * 0.1f, s * 0.7f, c, 2f);
        }

        // ---------------------------------------------------------------- the sphere guy

        public void Guy(float cx, float cy, float r, Col body, Mood mood, float lookX = 0f, float lookY = 0f, bool feet = true)
        {
            var dark = body.Mul(0.55f);
            if (feet)
            {
                SoftShadow(cx, cy + r * 1.08f, r * 1.05f, r * 0.2f, 0.4f);
                Ball(cx - r * 0.38f, cy + r * 0.95f, r * 0.24f, dark, 2.5f);
                Ball(cx + r * 0.38f, cy + r * 0.95f, r * 0.24f, dark, 2.5f);
            }
            Ball(cx, cy, r, body, 3f);
            Ellipse(cx - r * 0.42f, cy - r * 0.5f, r * 0.2f, r * 0.1f, -35f, White.WithA(0.55f), 0f);
            if (mood == Mood.Happy || mood == Mood.Smug || mood == Mood.Sleepy)
            {
                Ellipse(cx - r * 0.55f, cy + r * 0.2f, r * 0.14f, r * 0.08f, 0f, new Col(1f, 0.45f, 0.55f, 0.45f), 0f);
                Ellipse(cx + r * 0.55f, cy + r * 0.2f, r * 0.14f, r * 0.08f, 0f, new Col(1f, 0.45f, 0.55f, 0.45f), 0f);
            }

            // Face.
            float ex = r * 0.3f, ey = cy - r * 0.08f, ew = r * 0.17f, eh = r * 0.23f;
            switch (mood)
            {
                case Mood.Cool:
                    RoundRect(cx - ex, ey, ew * 2.6f, eh * 1.4f, 3f, 0f, Ink, 0f);
                    RoundRect(cx + ex, ey, ew * 2.6f, eh * 1.4f, 3f, 0f, Ink, 0f);
                    Line(cx - ex + ew, ey - eh * 0.3f, cx + ex - ew, ey - eh * 0.3f, 2.5f, Ink);
                    Ellipse(cx - ex - ew * 0.5f, ey - eh * 0.3f, ew * 0.4f, eh * 0.2f, -20f, White.WithA(0.7f), 0f);
                    break;
                case Mood.Dizzy:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Line(cx + s * ex - ew, ey - ew, cx + s * ex + ew, ey + ew, 3f, Ink);
                        Line(cx + s * ex - ew, ey + ew, cx + s * ex + ew, ey - ew, 3f, Ink);
                    }
                    break;
                case Mood.Sleepy:
                    Arc(cx - ex, ey, ew, 20f, 160f, 2.5f, Ink);
                    Arc(cx + ex, ey, ew, 20f, 160f, 2.5f, Ink);
                    break;
                default:
                {
                    float big = mood == Mood.Panic ? 1.25f : 1f;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Ellipse(cx + s * ex, ey, ew * big, eh * big, 0f, White, 2f);
                        float pr = mood == Mood.Panic ? ew * 0.35f : ew * 0.55f;
                        Circle(cx + s * ex + lookX * ew * 0.45f, ey + lookY * eh * 0.4f + (mood == Mood.Smug ? eh * 0.25f : 0f), pr, Ink, 0f);
                        Circle(cx + s * ex + lookX * ew * 0.45f - pr * 0.35f, ey + lookY * eh * 0.4f - pr * 0.4f, pr * 0.32f, White, 0f);
                        Circle(cx + s * ex + lookX * ew * 0.45f + pr * 0.35f, ey + lookY * eh * 0.4f + pr * 0.35f, pr * 0.15f, White.WithA(0.85f), 0f);
                        if (mood == Mood.Smug)
                            Shape(cx + s * ex - ew - 2, ey - eh - 2, cx + s * ex + ew + 2, ey + 1, (x, y) => Math.Max(Len((x - cx - s * ex) / ew, (y - ey) / eh) - 1f, y - ey + 0.5f) * ew, body);
                    }
                    if (mood == Mood.Angry)
                    {
                        Line(cx - ex - ew, ey - eh * 1.25f, cx - ex + ew, ey - eh * 0.7f, 3.5f, Ink);
                        Line(cx + ex + ew, ey - eh * 1.25f, cx + ex - ew, ey - eh * 0.7f, 3.5f, Ink);
                    }
                    if (mood == Mood.Sad)
                    {
                        Line(cx - ex - ew, ey - eh * 0.8f, cx - ex + ew, ey - eh * 1.25f, 3f, Ink);
                        Line(cx + ex + ew, ey - eh * 0.8f, cx + ex - ew, ey - eh * 1.25f, 3f, Ink);
                    }
                    break;
                }
            }

            // Mouth.
            float my = cy + r * 0.35f;
            switch (mood)
            {
                case Mood.Happy:
                case Mood.Cool:
                    Arc(cx, my - r * 0.18f, r * 0.24f, 25f, 155f, 3f, Ink);
                    break;
                case Mood.Angry:
                    RoundRect(cx, my, r * 0.5f, r * 0.17f, 2f, 0f, White, 2.5f);
                    for (int k = -1; k <= 1; k++) Line(cx + k * r * 0.12f, my - r * 0.08f, cx + k * r * 0.12f, my + r * 0.08f, 1.5f, Ink);
                    break;
                case Mood.Panic:
                    Ellipse(cx, my, r * 0.17f, r * 0.2f, 0f, new Col(0.3f, 0.05f, 0.08f), 2.5f);
                    Ellipse(cx, my + r * 0.09f, r * 0.1f, r * 0.06f, 0f, new Col(1f, 0.45f, 0.5f), 0f);
                    break;
                case Mood.Smug:
                    Arc(cx + r * 0.1f, my - r * 0.25f, r * 0.25f, 40f, 110f, 3f, Ink);
                    break;
                case Mood.Dizzy:
                    Polyline(new[] { cx - r * 0.25f, my, cx - r * 0.12f, my - r * 0.06f, cx, my, cx + r * 0.12f, my - r * 0.06f, cx + r * 0.25f, my }, 2.5f, Ink);
                    break;
                case Mood.Sleepy:
                    Ellipse(cx, my, r * 0.08f, r * 0.06f, 0f, Ink, 0f);
                    break;
                case Mood.Sad:
                    Arc(cx, my + r * 0.22f, r * 0.22f, 205f, 335f, 3f, Ink);
                    break;
            }
        }

        public void Blaster(float x, float y, float len, float angleDeg, Col c)
        {
            float a = angleDeg * (float)Math.PI / 180f;
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
            RoundRect(x + dx * len * 0.5f, y + dy * len * 0.5f, len, len * 0.34f, len * 0.12f, angleDeg, c, 2.5f);
            RoundRect(x + dx * len * 0.15f - dy * len * 0.2f, y + dy * len * 0.15f + dx * len * 0.2f, len * 0.18f, len * 0.3f, 2f, angleDeg, c.Mul(0.8f), 2f);
            Circle(x + dx * len, y + dy * len, len * 0.1f, new Col(1f, 0.85f, 0.35f), 2f);
        }

        public void Flash(float x, float y, float s)
        {
            Glow(x, y, s * 2f, new Col(1f, 0.85f, 0.4f, 0.55f));
            Star(x, y, s, s * 0.45f, 8, Rand(0f, 40f), new Col(1f, 0.85f, 0.25f), 2.5f);
            Star(x, y, s * 0.55f, s * 0.25f, 8, Rand(0f, 40f), new Col(1f, 1f, 0.85f), 0f);
        }

        public void Bullet(float x, float y, float s, float angleDeg, Col c, bool eyes = false)
        {
            RoundRect(x, y, s * 2.2f, s, s * 0.5f, angleDeg, c, 2f);
            if (eyes)
            {
                Circle(x - s * 0.1f, y - s * 0.12f, s * 0.22f, White, 1.2f);
                Circle(x + s * 0.4f, y - s * 0.12f, s * 0.22f, White, 1.2f);
                Circle(x + s * 0.05f, y - s * 0.12f, s * 0.1f, Ink, 0f);
                Circle(x + s * 0.55f, y - s * 0.12f, s * 0.1f, Ink, 0f);
            }
        }

        public void SpeedLines(float x, float y, float len, int n, float spacing, Col c)
        {
            for (int i = 0; i < n; i++) Line(x - Rand(0f, len * 0.3f), y + i * spacing, x - len, y + i * spacing, 2.5f, c);
        }

        // ---------------------------------------------------------------- scenes

        static readonly Col[] GuyColors =
        {
            new Col(0.95f, 0.35f, 0.35f), new Col(0.35f, 0.65f, 1f), new Col(0.45f, 0.85f, 0.4f), new Col(1f, 0.75f, 0.25f),
            new Col(0.8f, 0.45f, 1f), new Col(1f, 0.55f, 0.75f), new Col(0.35f, 0.9f, 0.85f), new Col(1f, 0.6f, 0.3f),
        };

        /// <summary>
        /// Paint a scene. Returns a short comic caption ("POW!") to letter over the art, or null.
        /// </summary>
        /// <param name="scene">Glyph id of the card (heart, bolt, sword...), or "curse".</param>
        /// <param name="bg">Background tint (the card's rarity color).</param>
        /// <param name="drawback">The card has a downside: the hero looks a bit worried.</param>
        public string Paint(string scene, Col bg, bool drawback, bool legendary)
        {
            Backdrop(bg, legendary);
            var guy = GuyColors[rng.Next(GuyColors.Length)];
            float cx = W * 0.5f, cy = H * 0.56f, r = H * 0.24f;
            var ground = new Col(0f, 0f, 0f, 0.18f);
            string caption = null;

            switch (scene)
            {
                case "heart":
                    Glow(W * 0.68f, H * 0.5f, H * 0.45f, new Col(1f, 0.5f, 0.6f, 0.45f));
                    Heart(W * 0.68f, H * 0.5f, H * 0.26f, new Col(1f, 0.3f, 0.4f));
                    Ellipse(W * 0.6f, H * 0.4f, 6f, 4f, -30f, White.WithA(0.7f), 0f);
                    Guy(W * 0.34f, cy + 4f, r, guy, drawback ? Mood.Panic : Mood.Happy, 1f, 0f);
                    Heart(W * 0.18f, H * 0.2f, 7f, new Col(1f, 0.45f, 0.55f), 2f);
                    Heart(W * 0.88f, H * 0.16f, 5f, new Col(1f, 0.45f, 0.55f), 2f);
                    caption = rng.Next(2) == 0 ? "AWW" : null;
                    break;

                case "bolt":
                    Ellipse(cx, H * 0.9f, r * 1.4f, 5f, 0f, ground, 0f);
                    SpeedLines(cx - r * 1.2f, cy - r * 0.6f, W * 0.35f, 4, r * 0.4f, White.WithA(0.85f));
                    Puff(cx - r * 1.6f, H * 0.82f, 7f, new Col(0.85f, 0.85f, 0.8f));
                    Guy(cx + 6f, cy, r, guy, drawback ? Mood.Panic : Mood.Happy, 1f, 0f);
                    Poly(new[] { W * 0.82f, H * 0.08f, W * 0.72f, H * 0.36f, W * 0.8f, H * 0.36f, W * 0.74f, H * 0.6f, W * 0.92f, H * 0.28f, W * 0.84f, H * 0.28f, W * 0.9f, H * 0.08f }, new Col(1f, 0.88f, 0.2f));
                    caption = "ZOOM!";
                    break;

                case "arrow_up":
                {
                    float sx = cx - 4f, sy = H * 0.92f;
                    var spring = new List<float>();
                    for (int i = 0; i <= 8; i++) { spring.Add(sx + (i % 2 == 0 ? -12f : 12f)); spring.Add(sy - i * 4f); }
                    Polyline(spring.ToArray(), 4f, new Col(0.7f, 0.72f, 0.78f));
                    RoundRect(sx, sy - 34f, 34f, 6f, 2f, 0f, new Col(0.5f, 0.52f, 0.6f), 2f);
                    for (int i = 0; i < 3; i++) Line(sx - 18f + i * 18f, sy - 44f, sx - 22f + i * 22f, sy - 54f, 2.5f, White);
                    Guy(cx, H * 0.26f, r * 0.85f, guy, drawback ? Mood.Panic : Mood.Happy, 0f, -0.5f);
                    caption = "BOING!";
                    break;
                }

                case "sword":
                    Ellipse(cx - 20f, H * 0.9f, r * 1.3f, 5f, 0f, ground, 0f);
                    Guy(cx - 26f, cy, r, guy, Mood.Angry, 1f, 0f);
                    Blaster(cx - 6f, cy + 4f, r * 2.1f, -6f, new Col(0.35f, 0.36f, 0.42f));
                    Flash(cx - 6f + r * 2.1f + 8f, cy - 1f, 16f);
                    caption = "POW!";
                    break;

                case "clock":
                {
                    float wx = W * 0.72f, wy = H * 0.42f, wr = H * 0.3f;
                    RoundRect(wx, wy - wr - 4f, 10f, 8f, 2f, 0f, new Col(0.7f, 0.72f, 0.78f), 2f);
                    Circle(wx, wy, wr, new Col(0.95f, 0.95f, 0.92f), 4f);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * (float)Math.PI / 6f;
                        Line(wx + (float)Math.Cos(a) * wr * 0.8f, wy + (float)Math.Sin(a) * wr * 0.8f, wx + (float)Math.Cos(a) * wr * 0.92f, wy + (float)Math.Sin(a) * wr * 0.92f, 2f, Ink);
                    }
                    Line(wx, wy, wx + wr * 0.55f, wy - wr * 0.35f, 3.5f, new Col(0.9f, 0.2f, 0.2f));
                    Line(wx, wy, wx - wr * 0.1f, wy - wr * 0.65f, 3f, Ink);
                    Guy(W * 0.28f, cy + 6f, r, guy, Mood.Panic, 1f, -0.3f);
                    Sweat(W * 0.28f - r * 0.9f, cy - r * 0.6f, 5f);
                    Sweat(W * 0.28f + r * 0.95f, cy - r * 0.4f, 4f);
                    caption = "HURRY!";
                    break;
                }

                case "ammo":
                    Guy(cx, cy - 4f, r, guy, drawback ? Mood.Panic : Mood.Smug, 0f, 0f, false);
                    for (int i = 0; i < 26; i++)
                    {
                        float bx = Rand(W * 0.08f, W * 0.92f), by = Rand(H * 0.72f, H * 0.98f);
                        Bullet(bx, by, Rand(5f, 7f), Rand(-80f, 80f), new Col(1f, 0.78f, 0.28f));
                    }
                    caption = rng.Next(2) == 0 ? "MORE!" : null;
                    break;

                case "reload":
                    Guy(cx, cy + 6f, r, guy, drawback ? Mood.Panic : Mood.Happy, 0f, -1f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (200f + i * 70f) * (float)Math.PI / 180f;
                        float mx = cx + (float)Math.Cos(a) * r * 1.8f, my2 = cy - r * 0.6f + (float)Math.Sin(a) * r * 1.2f;
                        RoundRect(mx, my2, 12f, 20f, 2f, i * 35f - 30f, new Col(0.4f, 0.42f, 0.5f), 2.5f);
                    }
                    Arc(cx, cy - r * 0.6f, r * 1.6f, 200f, 340f, 1.5f, White.WithA(0.7f));
                    break;

                case "shield":
                    Ellipse(cx, H * 0.92f, r * 1.4f, 5f, 0f, ground, 0f);
                    Guy(cx, cy + 4f, r * 0.8f, guy, Mood.Smug, 0f, 0f);
                    Circle(cx, cy, r * 1.45f, new Col(0.5f, 0.9f, 1f, 0.28f), 0f);
                    Arc(cx, cy, r * 1.45f, 0f, 360f, 3f, new Col(0.6f, 0.95f, 1f));
                    Arc(cx, cy, r * 1.2f, 200f, 250f, 3f, White.WithA(0.8f));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (i * 120f + Rand(0f, 60f)) * (float)Math.PI / 180f;
                        float bx = cx + (float)Math.Cos(a) * r * 1.6f, by = cy + (float)Math.Sin(a) * r * 1.6f;
                        Star(bx, by, 7f, 3f, 6, 0f, new Col(1f, 0.9f, 0.4f), 1.5f);
                    }
                    caption = rng.Next(2) == 0 ? "NOPE" : "BLOCKED";
                    break;

                case "drop":
                    Poly(new[] { cx - r * 1.6f, cy - r * 0.6f, cx + r * 1.6f, cy - r * 0.6f, cx + r * 1.3f, cy + r * 1.3f, cx - r * 1.3f, cy + r * 1.3f }, new Col(0.45f, 0.05f, 0.1f));
                    Poly(new[] { cx - r * 1.6f, cy - r * 0.6f, cx - r * 0.9f, cy - r * 1.3f, cx - r * 0.5f, cy - r * 0.5f }, new Col(0.6f, 0.08f, 0.14f));
                    Poly(new[] { cx + r * 1.6f, cy - r * 0.6f, cx + r * 0.9f, cy - r * 1.3f, cx + r * 0.5f, cy - r * 0.5f }, new Col(0.6f, 0.08f, 0.14f));
                    Guy(cx, cy, r, new Col(0.85f, 0.85f, 0.92f), Mood.Smug, 0f, 0f);
                    Poly(new[] { cx - r * 0.2f, cy + r * 0.32f, cx - r * 0.12f, cy + r * 0.32f, cx - r * 0.16f, cy + r * 0.5f }, White, 1.2f);
                    Poly(new[] { cx + r * 0.08f, cy + r * 0.32f, cx + r * 0.16f, cy + r * 0.32f, cx + r * 0.12f, cy + r * 0.5f }, White, 1.2f);
                    Sweat(W * 0.85f, H * 0.3f, 6f);
                    caption = "BLEH!";
                    break;

                case "plus":
                    RoundRect(cx, H * 0.86f, W * 0.7f, H * 0.22f, 8f, 0f, new Col(0.4f, 0.75f, 1f), 3f);
                    Guy(cx, cy - 2f, r, guy, Mood.Sleepy, 0f, 0f, false);
                    RoundRect(cx, H * 0.82f, W * 0.66f, 6f, 3f, 0f, new Col(0.7f, 0.9f, 1f, 0.8f), 0f);
                    for (int i = 0; i < 4; i++)
                    {
                        float px2 = W * (0.15f + 0.23f * i), py = Rand(H * 0.12f, H * 0.35f);
                        RoundRect(px2, py, 14f, 5f, 1.5f, 0f, new Col(0.35f, 0.95f, 0.45f), 1.5f);
                        RoundRect(px2, py, 5f, 14f, 1.5f, 0f, new Col(0.35f, 0.95f, 0.45f), 1.5f);
                    }
                    caption = "AHHH";
                    break;

                case "target":
                {
                    float tx = W * 0.75f, ty = H * 0.48f;
                    for (int i = 4; i >= 1; i--) Circle(tx, ty, i * 9f, i % 2 == 0 ? new Col(0.95f, 0.25f, 0.25f) : White, i == 4 ? 3f : 0f);
                    Bullet(W * 0.45f, H * 0.42f, 9f, 8f, new Col(1f, 0.8f, 0.3f), true);
                    Polyline(new[] { W * 0.08f, H * 0.7f, W * 0.2f, H * 0.4f, W * 0.32f, H * 0.5f, W * 0.4f, H * 0.42f }, 2f, White.WithA(0.7f));
                    Guy(W * 0.18f, H * 0.72f, r * 0.75f, guy, drawback ? Mood.Dizzy : Mood.Happy, 1f, -0.5f);
                    break;
                }

                case "bomb":
                    Glow(cx, cy - 6f, r * 2.6f, new Col(1f, 0.6f, 0.2f, 0.6f));
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 40f * (float)Math.PI / 180f;
                        Circle(cx + (float)Math.Cos(a) * r * 0.9f, cy - 6f + (float)Math.Sin(a) * r * 0.7f, r * Rand(0.55f, 0.75f), new Col(1f, 0.55f, 0.15f), 3f);
                    }
                    Circle(cx, cy - 6f, r * 1.05f, new Col(1f, 0.8f, 0.25f), 0f);
                    Circle(cx, cy - 6f, r * 0.6f, new Col(1f, 0.95f, 0.7f), 0f);
                    Guy(cx, cy - 4f, r * 0.55f, guy, Mood.Panic, 0f, 0f, false);
                    caption = "KABOOM!";
                    break;

                case "drill":
                    RoundRect(W * 0.78f, H * 0.5f, W * 0.24f, H * 0.95f, 3f, 0f, new Col(0.75f, 0.42f, 0.32f), 3f);
                    for (int i = 0; i < 6; i++) Line(W * 0.66f, H * (0.1f + i * 0.16f), W * 0.9f, H * (0.1f + i * 0.16f), 2f, new Col(0.45f, 0.22f, 0.15f));
                    Circle(W * 0.72f, H * 0.5f, 10f, new Col(0.2f, 0.15f, 0.15f), 0f);
                    Guy(W * 0.3f, cy, r, guy, Mood.Angry, 1f, 0f);
                    Poly(new[] { W * 0.42f, cy - 7f, W * 0.42f, cy + 7f, W * 0.7f, cy }, new Col(0.75f, 0.78f, 0.85f));
                    for (int i = 0; i < 3; i++) Line(W * (0.48f + i * 0.07f), cy - 5f + i * 1.5f, W * (0.51f + i * 0.07f), cy + 5f - i * 1.5f, 1.5f, Ink);
                    caption = "BRRRR";
                    break;

                case "star":
                    Glow(cx, cy, r * 2.2f, new Col(1f, 0.95f, 0.6f, 0.45f));
                    Ellipse(cx, cy - r * 1.25f, r * 0.6f, r * 0.15f, 0f, new Col(1f, 0.92f, 0.4f, 0f), 0f);
                    Arc(cx, cy - r * 1.25f, r * 0.55f, 0f, 360f, 4f, new Col(1f, 0.9f, 0.35f));
                    Ellipse(cx - r * 1.15f, cy - r * 0.1f, r * 0.55f, r * 0.3f, -25f, White, 2.5f);
                    Ellipse(cx + r * 1.15f, cy - r * 0.1f, r * 0.55f, r * 0.3f, 25f, White, 2.5f);
                    Guy(cx, cy, r, guy, Mood.Happy, 0f, -0.5f);
                    Star(W * 0.15f, H * 0.2f, 6f, 2.5f, 4, 0f, White, 0f);
                    Star(W * 0.85f, H * 0.28f, 5f, 2f, 4, 0f, White, 0f);
                    break;

                case "card":
                    Guy(W * 0.36f, cy + 4f, r, guy, Mood.Smug, 1f, 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = -40f + i * 20f;
                        float rad = (a - 90f) * (float)Math.PI / 180f;
                        float bx = W * 0.68f + (float)Math.Cos(rad) * 10f, by = H * 0.62f + (float)Math.Sin(rad) * 10f;
                        var cc = i == 4 ? new Col(1f, 0.75f, 0.2f) : White;
                        RoundRect(bx + (float)Math.Cos(rad) * 18f, by + (float)Math.Sin(rad) * 18f, 18f, 26f, 3f, a, cc, 2f);
                    }
                    break;

                case "bounce":
                    RoundRect(W * 0.04f, H * 0.5f, 8f, H, 0f, 0f, new Col(0.5f, 0.52f, 0.6f), 2f);
                    RoundRect(W * 0.96f, H * 0.5f, 8f, H, 0f, 0f, new Col(0.5f, 0.52f, 0.6f), 2f);
                    Polyline(new[] { W * 0.08f, H * 0.15f, W * 0.92f, H * 0.35f, W * 0.08f, H * 0.55f, W * 0.92f, H * 0.75f }, 2f, White.WithA(0.8f));
                    Bullet(W * 0.8f, H * 0.72f, 6f, 15f, new Col(1f, 0.8f, 0.3f));
                    Guy(cx, H * 0.62f, r * 0.8f, guy, Mood.Dizzy, 0f, 0f);
                    for (int i = 0; i < 3; i++) Star(cx - 14f + i * 14f, H * 0.62f - r * 1.05f, 4f, 1.8f, 5, i * 20f, new Col(1f, 0.9f, 0.3f), 1f);
                    caption = "PING!";
                    break;

                case "skull":
                    Poly(new[] { cx - r, cy, cx - r, cy + r * 1.1f, cx - r * 0.6f, cy + r * 0.85f, cx - r * 0.2f, cy + r * 1.15f, cx + r * 0.2f, cy + r * 0.85f, cx + r * 0.6f, cy + r * 1.15f, cx + r, cy + r * 0.85f, cx + r, cy }, new Col(0.9f, 0.93f, 1f, 0.95f), 3f);
                    Guy(cx, cy, r, new Col(0.9f, 0.93f, 1f), Mood.Panic, 0f, 0f, false);
                    caption = "BOO!";
                    break;

                case "gear":
                    Ellipse(cx, H * 0.95f, W * 0.4f, 5f, 0f, ground, 0f);
                    Guy(W * 0.38f, H * 0.5f, H * 0.36f, guy, Mood.Happy, 1f, 0.6f);
                    Guy(W * 0.8f, H * 0.82f, H * 0.09f, GuyColors[(Array.IndexOf(GuyColors, guy) + 3) % GuyColors.Length], Mood.Panic, -1f, -1f);
                    break;

                case "feather":
                    Line(cx + r * 0.2f, cy - r, cx + r * 0.9f, H * 0.2f, 1.5f, Ink);
                    Ball(cx + r * 1.05f, H * 0.15f, 14f, new Col(1f, 0.4f, 0.55f), 2.5f);
                    Ellipse(cx + r * 0.85f, H * 0.1f, 4f, 2.5f, -30f, White.WithA(0.7f), 0f);
                    Guy(cx - 6f, cy, r, guy, Mood.Happy, 0.5f, -1f);
                    Ellipse(W * 0.18f, H * 0.3f, 14f, 5f, -30f, White, 2f);
                    Line(W * 0.1f, H * 0.36f, W * 0.26f, H * 0.24f, 1.5f, Ink);
                    break;

                case "wave":
                    SpeedLines(W * 0.62f, H * 0.3f, W * 0.5f, 4, 10f, White.WithA(0.85f));
                    Guy(W * 0.75f, H * 0.42f, r * 0.85f, guy, Mood.Panic, -1f, 0f);
                    Puff(W * 0.12f, H * 0.82f, 8f, new Col(0.9f, 0.9f, 0.85f));
                    caption = "WHOOSH";
                    break;

                case "turret":
                    RoundRect(W * 0.65f, H * 0.82f, 34f, 14f, 3f, 0f, new Col(0.4f, 0.42f, 0.5f), 2.5f);
                    Circle(W * 0.65f, H * 0.66f, 15f, new Col(0.55f, 0.57f, 0.65f), 2.5f);
                    RoundRect(W * 0.78f, H * 0.62f, 26f, 8f, 3f, -10f, new Col(0.35f, 0.36f, 0.42f), 2f);
                    Circle(W * 0.61f, H * 0.64f, 4f, new Col(1f, 0.3f, 0.25f), 1.5f);
                    Flash(W * 0.92f, H * 0.58f, 10f);
                    Guy(W * 0.27f, cy + 4f, r, guy, Mood.Cool, 1f, 0f);
                    break;

                case "flame":
                    Glow(cx, cy - r * 0.4f, r * 2.4f, new Col(1f, 0.5f, 0.15f, 0.55f));
                    for (int i = 0; i < 5; i++)
                    {
                        float fx = cx - r + i * r * 0.5f;
                        Poly(new[] { fx - 10f, cy + 4f, fx, cy - r * Rand(1.3f, 1.8f), fx + 10f, cy + 4f }, new Col(1f, 0.45f, 0.1f), 2f);
                        Poly(new[] { fx - 5f, cy + 4f, fx, cy - r * Rand(0.8f, 1.1f), fx + 5f, cy + 4f }, new Col(1f, 0.9f, 0.3f), 0f);
                    }
                    Guy(cx, cy + 6f, r, guy, Mood.Angry, 0f, 0f);
                    caption = "FIRED UP";
                    break;

                case "snow":
                    Guy(cx, cy, r, guy, Mood.Panic, 0f, 0f);
                    RoundRect(cx, cy + 4f, r * 2.7f, r * 2.7f, 6f, 4f, new Col(0.7f, 0.92f, 1f, 0.55f), 3f);
                    Line(cx - r * 1.1f, cy - r * 1.1f, cx - r * 0.6f, cy - r * 1.2f, 3f, White.WithA(0.9f));
                    Line(cx - r * 1.15f, cy - r * 0.9f, cx - r * 1.15f, cy - r * 0.5f, 2.5f, White.WithA(0.7f));
                    Glow(cx, cy, r * 2f, new Col(0.7f, 0.95f, 1f, 0.25f));
                    for (int i = 0; i < 6; i++) Star(Rand(8f, W - 8f), Rand(6f, H * 0.3f), 4f, 1.5f, 6, 0f, White, 0f);
                    caption = "BRRR";
                    break;

                case "echo":
                    Guy(W * 0.3f, cy + 4f, r, guy, Mood.Angry, 1f, 0f);
                    for (int i = -1; i <= 1; i++)
                    {
                        Blaster(W * 0.38f, cy + 6f + i * 12f, r * 1.4f, i * 14f, new Col(0.35f, 0.36f, 0.42f));
                        float a = i * 14f * (float)Math.PI / 180f;
                        Flash(W * 0.38f + (float)Math.Cos(a) * (r * 1.4f + 8f), cy + 6f + i * 12f + (float)Math.Sin(a) * (r * 1.4f + 8f), 8f);
                    }
                    caption = "BRRAP!";
                    break;

                case "curse":
                    Puff(cx, H * 0.18f, 13f, new Col(0.35f, 0.33f, 0.4f));
                    for (int i = 0; i < 6; i++) Line(cx - 22f + i * 9f, H * 0.3f, cx - 26f + i * 9f, H * 0.42f, 2f, new Col(0.5f, 0.75f, 1f));
                    Guy(cx, cy + 10f, r * 0.85f, guy.Mul(0.8f), Mood.Sad, 0f, 0.5f);
                    caption = "UGH";
                    break;

                default:
                    Guy(cx, cy, r, guy, Mood.Happy, 0f, 0f);
                    caption = null;
                    break;
            }

            if (drawback && scene != "curse")
            {
                // Downsides: a little band-aid on the floor of the scene.
                RoundRect(W * 0.1f, H * 0.88f, 18f, 7f, 3f, -20f, new Col(1f, 0.82f, 0.65f), 1.5f);
            }
            Vignette(0.7f);
            return caption;
        }
    }
}
