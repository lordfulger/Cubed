using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Small procedural shape kit for cosmetics (hats, outfits, faces): cones, cylinders, domes, rings and
    /// arcs, flat stars and hearts, plus shell pieces that hug a sphere. Every mesh is generated once and
    /// cached; materials come from <see cref="Mats"/> or the small finish cache here.
    /// </summary>
    public static class Shape
    {
        // ------------------------------------------------------------------ materials

        static readonly Dictionary<long, Material> finishes = new Dictionary<long, Material>();

        public static Material M(Color c) => Mats.Solid(c);

        /// <summary>Shared lit material with its own smoothness/metallic (never mutates Mats.Solid's shared ones).</summary>
        public static Material Shiny(Color c, float gloss = 0.8f, float metal = 0f)
        {
            Color32 k = c;
            long key = ((long)((k.r << 24) | (k.g << 16) | (k.b << 8) | k.a) << 16) ^ (Mathf.RoundToInt(gloss * 100f) << 8) ^ Mathf.RoundToInt(metal * 100f);
            if (finishes.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Mats.Solid(c)) { name = "Spheres_Finish", color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            finishes[key] = m;
            return m;
        }

        /// <summary>Additive glow when available, otherwise a plain bright solid.</summary>
        public static Material Glow(Color c, float intensity = 2f, float soft = 0f) =>
            Mats.GlowAvailable ? Mats.Glow(c, intensity, soft) : Mats.Solid(c);

        public static Material Glass(Color c) => Mats.Transparent(c);

        // ------------------------------------------------------------------ placement helpers

        static GameObject Place(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler)
        {
            var go = Prim.Create(mesh, name, parent, pos, scale, mat);
            if (euler != Vector3.zero) go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        /// <summary>An empty transform to group or tilt parts.</summary>
        public static Transform Pivot(Transform parent, Vector3 pos, Vector3 euler = default, string name = "Pivot")
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            return t;
        }

        public static GameObject Ball(Transform p, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) =>
            Place(Prim.SphereMesh, "Ball", p, pos, scale, m, euler);

        public static GameObject Ball(Transform p, Vector3 pos, float diameter, Material m) =>
            Place(Prim.SphereMesh, "Ball", p, pos, Vector3.one * diameter, m, Vector3.zero);

        public static GameObject Box(Transform p, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) =>
            Place(Prim.CubeMesh, "Box", p, pos, scale, m, euler);

        /// <summary>Cylinder standing on its base center (rotations pivot around the base).</summary>
        public static GameObject Cyl(Transform p, Vector3 basePos, float d, float h, Material m, Vector3 euler = default) =>
            Place(TubeMesh(1f), "Cyl", p, basePos, new Vector3(d, h, d), m, euler);

        /// <summary>Round cone (or a frustum when <paramref name="top"/> &gt; 0) standing on its base center.</summary>
        public static GameObject Cone(Transform p, Vector3 basePos, float d, float h, Material m, Vector3 euler = default, float top = 0f) =>
            Place(TubeMesh(top), "Cone", p, basePos, new Vector3(d, h, d), m, euler);

        /// <summary>Half sphere with a flat base, standing on its base center.</summary>
        public static GameObject Dome(Transform p, Vector3 basePos, float d, float h, Material m, Vector3 euler = default) =>
            Place(DomeMesh, "Dome", p, basePos, new Vector3(d, h * 2f, d), m, euler);

        /// <summary>Flat ring (torus) in the XZ plane; <paramref name="sweep"/> &lt; 360 makes an arc centered on +Z.</summary>
        public static GameObject Ring(Transform p, Vector3 pos, float d, float thick, Material m, Vector3 euler = default, float sweep = 360f) =>
            Place(TorusMesh(Mathf.Clamp(thick / Mathf.Max(0.01f, d), 0.01f, 0.45f), sweep), "Ring", p, pos, Vector3.one * d, m, euler);

        /// <summary>Four-sided pyramid (base on y=0, tip at y=1 before scaling).</summary>
        public static GameObject Spike(Transform p, Vector3 basePos, Vector3 scale, Material m, Vector3 euler = default) =>
            Place(Mechanism.SpikeMesh, "Spike", p, basePos, scale, m, euler);

        /// <summary>Flat star facing +Z.</summary>
        public static GameObject Star(Transform p, Vector3 pos, float size, float depth, Material m, Vector3 euler = default, int points = 5) =>
            Place(StarMesh(points), "Star", p, pos, new Vector3(size, size, depth), m, euler);

        /// <summary>Flat heart facing +Z.</summary>
        public static GameObject Heart(Transform p, Vector3 pos, float size, float depth, Material m, Vector3 euler = default) =>
            Place(HeartMesh, "Heart", p, pos, new Vector3(size, size, depth), m, euler);

        /// <summary>A thin box from a to b.</summary>
        public static GameObject Rod(Transform p, Vector3 a, Vector3 b, float thick, Material m)
        {
            var d = b - a;
            var go = Prim.Create(Prim.CubeMesh, "Rod", p, (a + b) * 0.5f, new Vector3(thick, thick, Mathf.Max(0.001f, d.magnitude)), m);
            if (d.sqrMagnitude > 1e-6f) go.transform.localRotation = Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            return go;
        }

        /// <summary>Thin round stick from a to b.</summary>
        public static GameObject Stick(Transform p, Vector3 a, Vector3 b, float d, Material m)
        {
            var dir = b - a;
            var go = Prim.Create(TubeMesh(1f), "Stick", p, a, new Vector3(d, Mathf.Max(0.001f, dir.magnitude), d), m);
            if (dir.sqrMagnitude > 1e-6f) go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            return go;
        }

        // ------------------------------------------------------------------ mesh building

        /// <summary>Accumulates triangles; each triangle is wound to face the given outward hint.</summary>
        public class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int> T = new List<int>();

            public int Add(Vector3 v, Vector3 n) { V.Add(v); N.Add(n); return V.Count - 1; }

            public void Tri(int a, int b, int c, Vector3 outward)
            {
                var cross = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (Vector3.Dot(cross, outward) < 0f) { T.Add(a); T.Add(c); T.Add(b); }
                else { T.Add(a); T.Add(b); T.Add(c); }
            }

            /// <summary>Quad a-b-c-d (in order around its edge).</summary>
            public void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static readonly Dictionary<int, Mesh> tubes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> tori = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> stars = new Dictionary<int, Mesh>();
        static Mesh dome, heart;

        /// <summary>Frustum: radius 0.5 at y=0, radius 0.5*top at y=1, capped.</summary>
        public static Mesh TubeMesh(float top)
        {
            top = Mathf.Clamp01(top);
            int key = Mathf.RoundToInt(top * 100f);
            if (tubes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            const int n = 24;
            var b = new Builder();
            float rb = 0.5f, rt = 0.5f * top;
            float ny = rb - rt;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 n0 = (d0 + Vector3.up * ny).normalized, n1 = (d1 + Vector3.up * ny).normalized;
                int v0 = b.Add(d0 * rb, n0), v1 = b.Add(d1 * rb, n1);
                int v2 = b.Add(d1 * rt + Vector3.up, n1), v3 = b.Add(d0 * rt + Vector3.up, n0);
                var o = (d0 + d1).normalized;
                if (top > 0.001f) b.Quad(v0, v1, v2, v3, o);
                else b.Tri(v0, v1, v2, o);
                // Caps.
                int c0 = b.Add(Vector3.zero, Vector3.down), c1 = b.Add(d0 * rb, Vector3.down), c2 = b.Add(d1 * rb, Vector3.down);
                b.Tri(c0, c1, c2, Vector3.down);
                if (top > 0.001f)
                {
                    int t0 = b.Add(Vector3.up, Vector3.up), t1 = b.Add(d0 * rt + Vector3.up, Vector3.up), t2 = b.Add(d1 * rt + Vector3.up, Vector3.up);
                    b.Tri(t0, t1, t2, Vector3.up);
                }
            }
            mesh = b.Build("Tube");
            tubes[key] = mesh;
            return mesh;
        }

        /// <summary>Hemisphere of radius 0.5 (top at y=0.5) with a flat base.</summary>
        public static Mesh DomeMesh
        {
            get
            {
                if (dome != null) return dome;
                const int lon = 24, lat = 8;
                var b = new Builder();
                var grid = new int[lat + 1, lon + 1];
                for (int j = 0; j <= lat; j++)
                    for (int i = 0; i <= lon; i++)
                    {
                        float th = j * Mathf.PI * 0.5f / lat, ph = i * Mathf.PI * 2f / lon;
                        var d = new Vector3(Mathf.Cos(th) * Mathf.Cos(ph), Mathf.Sin(th), Mathf.Cos(th) * Mathf.Sin(ph));
                        grid[j, i] = b.Add(d * 0.5f, d);
                    }
                for (int j = 0; j < lat; j++)
                    for (int i = 0; i < lon; i++)
                    {
                        var o = b.N[grid[j, i]] + b.N[grid[j + 1, i + 1]];
                        if (j == lat - 1) b.Tri(grid[j, i], grid[j, i + 1], grid[j + 1, i], o);
                        else b.Quad(grid[j, i], grid[j, i + 1], grid[j + 1, i + 1], grid[j + 1, i], o);
                    }
                int c = b.Add(Vector3.zero, Vector3.down);
                for (int i = 0; i < lon; i++)
                {
                    float a0 = i * Mathf.PI * 2f / lon, a1 = (i + 1) * Mathf.PI * 2f / lon;
                    int e0 = b.Add(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f, Vector3.down);
                    int e1 = b.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f, Vector3.down);
                    b.Tri(c, e0, e1, Vector3.down);
                }
                dome = b.Build("Dome");
                return dome;
            }
        }

        /// <summary>Torus of major diameter 1 in the XZ plane; tube radius = <paramref name="minor"/>. Arcs are centered on +Z.</summary>
        public static Mesh TorusMesh(float minor, float sweep = 360f)
        {
            sweep = Mathf.Clamp(sweep, 10f, 360f);
            int key = Mathf.RoundToInt(minor * 400f) * 1000 + Mathf.RoundToInt(sweep);
            if (tori.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            bool closed = sweep >= 359.5f;
            int nu = Mathf.Max(6, Mathf.CeilToInt(40 * sweep / 360f)), nv = 10;
            var b = new Builder();
            var grid = new int[nu + 1, nv + 1];
            float R = 0.5f;
            for (int i = 0; i <= nu; i++)
            {
                float u = (-sweep * 0.5f + sweep * i / nu) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Sin(u), 0f, Mathf.Cos(u));
                for (int j = 0; j <= nv; j++)
                {
                    float v = j * Mathf.PI * 2f / nv;
                    var nrm = radial * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    grid[i, j] = b.Add(radial * R + nrm * minor, nrm);
                }
            }
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                    b.Quad(grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], b.N[grid[i, j]] + b.N[grid[i + 1, j + 1]]);
            if (!closed)
            {
                for (int e = 0; e < 2; e++)
                {
                    float u = (e == 0 ? -sweep * 0.5f : sweep * 0.5f) * Mathf.Deg2Rad;
                    var radial = new Vector3(Mathf.Sin(u), 0f, Mathf.Cos(u));
                    var tangent = new Vector3(Mathf.Cos(u), 0f, -Mathf.Sin(u)) * (e == 0 ? 1f : -1f);
                    int c = b.Add(radial * R, tangent);
                    for (int j = 0; j < nv; j++)
                    {
                        float v0 = j * Mathf.PI * 2f / nv, v1 = (j + 1) * Mathf.PI * 2f / nv;
                        int a0 = b.Add(radial * R + (radial * Mathf.Cos(v0) + Vector3.up * Mathf.Sin(v0)) * minor, tangent);
                        int a1 = b.Add(radial * R + (radial * Mathf.Cos(v1) + Vector3.up * Mathf.Sin(v1)) * minor, tangent);
                        b.Tri(c, a0, a1, tangent);
                    }
                }
            }
            mesh = b.Build("Torus");
            tori[key] = mesh;
            return mesh;
        }

        /// <summary>Flat extruded star (outer radius 0.5) facing +Z, depth 1.</summary>
        public static Mesh StarMesh(int points)
        {
            points = Mathf.Clamp(points, 3, 12);
            if (stars.TryGetValue(points, out var mesh) && mesh != null) return mesh;
            var outline = new Vector2[points * 2];
            float inner = points == 4 ? 0.2f : 0.42f;
            for (int k = 0; k < outline.Length; k++)
            {
                float a = Mathf.PI * 0.5f + k * Mathf.PI / points;
                float r = k % 2 == 0 ? 0.5f : 0.5f * inner;
                outline[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            mesh = Extrude(outline, Vector2.zero, "Star");
            stars[points] = mesh;
            return mesh;
        }

        /// <summary>Flat extruded heart (about 1 wide) facing +Z, depth 1.</summary>
        public static Mesh HeartMesh
        {
            get
            {
                if (heart != null) return heart;
                heart = Extrude(HeartOutline(36), new Vector2(0f, -0.05f), "Heart");
                return heart;
            }
        }

        public static Vector2[] HeartOutline(int n)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float t = i * Mathf.PI * 2f / n;
                float s = Mathf.Sin(t);
                float x = 16f * s * s * s;
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                pts[i] = new Vector2(x, y + 2.5f) / 34f;
            }
            return pts;
        }

        static Mesh Extrude(Vector2[] outline, Vector2 center, string name)
        {
            var b = new Builder();
            int n = outline.Length;
            foreach (float side in new[] { 0.5f, -0.5f })
            {
                var nrm = new Vector3(0f, 0f, side > 0f ? 1f : -1f);
                int c = b.Add(new Vector3(center.x, center.y, side), nrm);
                var ring = new int[n];
                for (int i = 0; i < n; i++) ring[i] = b.Add(new Vector3(outline[i].x, outline[i].y, side), nrm);
                for (int i = 0; i < n; i++) b.Tri(c, ring[i], ring[(i + 1) % n], nrm);
            }
            for (int i = 0; i < n; i++)
            {
                var a = outline[i];
                var d = outline[(i + 1) % n];
                var edge = d - a;
                var o = new Vector3(edge.y, -edge.x, 0f).normalized;
                // Outward = away from the center.
                var mid = (a + d) * 0.5f - center;
                if (Vector2.Dot(new Vector2(o.x, o.y), mid) < 0f) o = -o;
                int v0 = b.Add(new Vector3(a.x, a.y, 0.5f), o), v1 = b.Add(new Vector3(d.x, d.y, 0.5f), o);
                int v2 = b.Add(new Vector3(d.x, d.y, -0.5f), o), v3 = b.Add(new Vector3(a.x, a.y, -0.5f), o);
                b.Quad(v0, v1, v2, v3, o);
            }
            return b.Build(name);
        }

        // ------------------------------------------------------------------ sphere shells (body patterns)

        /// <summary>Unit direction from latitude/longitude in degrees (longitude 0 = +Z, 90 = +X).</summary>
        public static Vector3 Dir(float latDeg, float lonDeg)
        {
            float la = latDeg * Mathf.Deg2Rad, lo = lonDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(lo) * Mathf.Cos(la), Mathf.Sin(la), Mathf.Cos(lo) * Mathf.Cos(la));
        }

        /// <summary>
        /// Adds a band of sphere surface (radius <paramref name="r"/>) between two latitude curves, over a longitude
        /// range. Cells for which <paramref name="skip"/> returns true are left out.
        /// </summary>
        public static void ShellBand(Builder b, float r, System.Func<float, float> latLo, System.Func<float, float> latHi,
                                     float lon0, float lon1, int lonSegs, int latSegs, System.Func<Vector3, bool> skip = null)
        {
            for (int i = 0; i < lonSegs; i++)
            {
                float la = Mathf.Lerp(lon0, lon1, i / (float)lonSegs), lb = Mathf.Lerp(lon0, lon1, (i + 1) / (float)lonSegs);
                for (int j = 0; j < latSegs; j++)
                {
                    float fa = j / (float)latSegs, fb = (j + 1) / (float)latSegs;
                    float p00 = Mathf.Lerp(latLo(la), latHi(la), fa), p01 = Mathf.Lerp(latLo(la), latHi(la), fb);
                    float p10 = Mathf.Lerp(latLo(lb), latHi(lb), fa), p11 = Mathf.Lerp(latLo(lb), latHi(lb), fb);
                    var center = Dir((p00 + p11) * 0.5f, (la + lb) * 0.5f);
                    if (skip != null && skip(center)) continue;
                    Vector3 d00 = Dir(p00, la), d01 = Dir(p01, la), d10 = Dir(p10, lb), d11 = Dir(p11, lb);
                    int a = b.Add(d00 * r, d00), bb = b.Add(d10 * r, d10), c = b.Add(d11 * r, d11), d = b.Add(d01 * r, d01);
                    b.Quad(a, bb, c, d, center);
                }
            }
        }

        /// <summary>
        /// Adds a flat 2D shape (angular coordinates in degrees, star-shaped around 0,0) wrapped onto a sphere
        /// of radius <paramref name="r"/> around direction <paramref name="dir"/>.
        /// </summary>
        public static void ShellDecal(Builder b, float r, Vector3 dir, float rollDeg, Vector2[] outlineDeg)
        {
            dir = dir.normalized;
            var t1 = Vector3.Cross(Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up, dir).normalized;
            var t2 = Vector3.Cross(dir, t1).normalized;
            var roll = Quaternion.AngleAxis(rollDeg, dir);
            t1 = roll * t1;
            t2 = roll * t2;
            Vector3 Map(Vector2 deg)
            {
                var p = dir + t1 * Mathf.Tan(deg.x * Mathf.Deg2Rad) + t2 * Mathf.Tan(deg.y * Mathf.Deg2Rad);
                return p.normalized;
            }
            const int rings = 3;
            int n = outlineDeg.Length;
            int c = b.Add(dir * r, dir);
            var prev = new int[n];
            for (int i = 0; i < n; i++) prev[i] = c;
            for (int k = 1; k <= rings; k++)
            {
                float f = k / (float)rings;
                var cur = new int[n];
                for (int i = 0; i < n; i++)
                {
                    var d = Map(outlineDeg[i] * f);
                    cur[i] = b.Add(d * r, d);
                }
                for (int i = 0; i < n; i++)
                {
                    int i2 = (i + 1) % n;
                    if (k == 1) b.Tri(c, cur[i], cur[i2], dir);
                    else b.Quad(prev[i], cur[i], cur[i2], prev[i2], dir);
                }
                prev = cur;
            }
        }

        public static Vector2[] CircleOutline(float radiusDeg, float squash = 1f, int n = 20)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts[i] = new Vector2(Mathf.Cos(a) * radiusDeg, Mathf.Sin(a) * radiusDeg * squash);
            }
            return pts;
        }

        public static Vector2[] StarOutline(float radiusDeg, int points, float inner)
        {
            var pts = new Vector2[points * 2];
            for (int k = 0; k < pts.Length; k++)
            {
                float a = Mathf.PI * 0.5f + k * Mathf.PI / points;
                float r = k % 2 == 0 ? radiusDeg : radiusDeg * inner;
                pts[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }
    }

    /// <summary>Gentle procedural motion for cosmetic parts (bob, sway, flicker), in unscaled time.</summary>
    public class Wobble : MonoBehaviour
    {
        public Vector3 Move;          // position amplitude
        public Vector3 Turn;          // euler amplitude (degrees)
        public float Pulse;           // uniform scale amplitude (fraction)
        public float Speed = 2f;
        public float Spin;            // continuous spin around local Y (degrees per second)
        public bool Flicker;          // noisy instead of smooth
        Vector3 basePos, baseScale;
        Quaternion baseRot;
        float phase;

        void Start()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            baseScale = transform.localScale;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.unscaledTime * Speed + phase;
            float s = Flicker ? (Mathf.PerlinNoise(t, phase) - 0.5f) * 2f : Mathf.Sin(t);
            float c = Flicker ? (Mathf.PerlinNoise(phase, t) - 0.5f) * 2f : Mathf.Cos(t * 0.8f);
            transform.localPosition = basePos + new Vector3(Move.x * c, Move.y * s, Move.z * c);
            transform.localRotation = baseRot * Quaternion.Euler(Turn.x * s, Turn.y * c + Spin * Time.unscaledTime, Turn.z * c);
            transform.localScale = baseScale * (1f + Pulse * s);
        }
    }

    /// <summary>Rolls a wheel around its local X axis as its owner moves.</summary>
    public class Roller : MonoBehaviour
    {
        public float Radius = 0.12f;
        Vector3 last;
        float angle;

        void Start() => last = transform.position;

        void LateUpdate()
        {
            var p = transform.position;
            var fwd = transform.parent != null ? transform.parent.forward : transform.forward;
            float dist = Vector3.Dot(p - last, fwd);
            last = p;
            if (Mathf.Abs(dist) > 2f) return; // teleported
            angle += dist / Mathf.Max(0.01f, Radius) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(angle, 0f, 90f);
        }
    }
}
