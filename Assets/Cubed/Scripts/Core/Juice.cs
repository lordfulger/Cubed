using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>Procedural textures and meshes that give the flat cube world some detail.</summary>
    public static class Looks
    {
        static Texture2D blockTexture, gridTexture, vignetteTexture, glowTexture;
        static readonly Dictionary<int, Material> blockMats = new Dictionary<int, Material>();
        static readonly Dictionary<long, Mesh> boxMeshes = new Dictionary<long, Mesh>();
        static Material opaqueTemplate;

        /// <summary>A beveled tile with subtle grain; tiles once per world unit on blocks.</summary>
        public static Texture2D BlockTexture
        {
            get
            {
                if (blockTexture != null) return blockTexture;
                const int size = 64;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Cubed Block", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                var rng = new System.Random(3);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float v = 0.9f + (float)rng.NextDouble() * 0.06f;
                        int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                        if (edge < 2) v *= 0.68f;                       // groove between tiles
                        else if (edge < 5)
                        {
                            bool lit = x < size - 5 && y > 4 && (x < 5 || y > size - 6);
                            v *= lit ? 1.12f : 0.86f;                    // bevel: lit top-left, shaded bottom-right
                        }
                        // faint diagonal sheen
                        v += Mathf.Sin((x + y) * 0.09f) * 0.015f;
                        px[y * size + x] = new Color(v, v, v, 1f);
                    }
                t.SetPixels(px);
                t.Apply(true);
                return blockTexture = t;
            }
        }

        /// <summary>Thin grid lines on transparency (borders, editor).</summary>
        public static Texture2D GridTexture
        {
            get
            {
                if (gridTexture != null) return gridTexture;
                const int size = 64;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Cubed Grid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                        float a = d == 0 ? 1f : d == 1 ? 0.6f : d < 4 ? 0.18f : 0.08f;
                        px[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                t.SetPixels(px);
                t.Apply(true);
                return gridTexture = t;
            }
        }

        /// <summary>Radial alpha falloff used for screen vignettes.</summary>
        public static Texture2D Vignette
        {
            get
            {
                if (vignetteTexture != null) return vignetteTexture;
                const int size = 128;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                        px[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                t.SetPixels(px);
                t.Apply();
                return vignetteTexture = t;
            }
        }

        /// <summary>Soft round dot (particles, glows in UI).</summary>
        public static Texture2D Glow
        {
            get
            {
                if (glowTexture != null) return glowTexture;
                const int size = 32;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                    }
                t.SetPixels(px);
                t.Apply();
                return glowTexture = t;
            }
        }

        static Material OpaqueTemplate
        {
            get
            {
                if (opaqueTemplate == null) opaqueTemplate = Mats.Solid(Color.white);
                return opaqueTemplate;
            }
        }

        /// <summary>Textured lit material for blocks.</summary>
        public static Material Block(Color c)
        {
            Color32 k = c;
            int key = (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
            if (blockMats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(OpaqueTemplate) { name = "Cubed_Block", color = c, mainTexture = BlockTexture };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.28f);
            blockMats[key] = m;
            return m;
        }

        /// <summary>A unit cube whose UVs tile once per world unit for a box of size w×h×d.</summary>
        public static Mesh TiledBox(float w, float h, float d)
        {
            long key = ((long)Mathf.RoundToInt(w * 8f) << 40) | ((long)Mathf.RoundToInt(h * 8f) << 20) | (long)Mathf.RoundToInt(d * 8f);
            if (boxMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Face(Vector3 normal, Vector3 up, float uScale, float vScale)
            {
                Vector3 right = Vector3.Cross(normal, up); // screen-right when looking at the face
                Vector3 c = normal * 0.5f;
                int i = v.Count;
                v.Add(c - right * 0.5f - up * 0.5f); uv.Add(new Vector2(0f, 0f));
                v.Add(c + right * 0.5f - up * 0.5f); uv.Add(new Vector2(uScale, 0f));
                v.Add(c + right * 0.5f + up * 0.5f); uv.Add(new Vector2(uScale, vScale));
                v.Add(c - right * 0.5f + up * 0.5f); uv.Add(new Vector2(0f, vScale));
                for (int k = 0; k < 4; k++) n.Add(normal);
                tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Face(Vector3.right, Vector3.up, d, h);
            Face(Vector3.left, Vector3.up, d, h);
            Face(Vector3.up, Vector3.forward, w, d);
            Face(Vector3.down, Vector3.back, w, d);
            Face(Vector3.forward, Vector3.up, w, h);
            Face(Vector3.back, Vector3.up, w, h);
            mesh = new Mesh { name = "TiledBox" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            boxMeshes[key] = mesh;
            return mesh;
        }

        /// <summary>Gradient sky dome (vertex colors, unlit), drawn behind everything.</summary>
        public static GameObject SkyDome(Transform parent, Vector3 center, Color top, Color horizon, Color bottom)
        {
            const int lon = 32, lat = 16;
            const float radius = 420f;
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float a = Mathf.PI * y / lat;                    // 0 = top
                float h = Mathf.Cos(a);
                Color c = h >= 0f ? Color.Lerp(horizon, top, Mathf.Pow(h, 0.6f)) : Color.Lerp(horizon, bottom, Mathf.Pow(-h, 0.5f));
                for (int x = 0; x <= lon; x++)
                {
                    float b = 2f * Mathf.PI * x / lon;
                    verts.Add(new Vector3(Mathf.Sin(a) * Mathf.Cos(b), h, Mathf.Sin(a) * Mathf.Sin(b)) * radius);
                    cols.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c);
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    tris.AddRange(new[] { i, i + lon + 1, i + 1, i + 1, i + lon + 1, i + lon + 2 });
                }
            var mesh = new Mesh { name = "SkyDome" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 4f);
            var mat = Mats.NewTransparent(Color.white);
            mat.renderQueue = 1000;
            var go = Prim.Create(mesh, "Sky", parent, center, Vector3.one, mat);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A star field and a soft sun glow on the sky (single draw call for the stars).</summary>
        public static void StarsAndSun(Transform parent, Vector3 center, Color tint)
        {
            if (!Mats.GlowAvailable) return;
            var rng = new System.Random(5);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < 260; i++)
            {
                // Upper hemisphere, biased toward the horizon band.
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float y = Mathf.Pow((float)rng.NextDouble(), 0.7f) * 0.95f + 0.03f;
                var dir = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1f - y * y), y, Mathf.Sin(a) * Mathf.Sqrt(1f - y * y));
                var p = dir * 380f;
                float s = 0.6f + (float)rng.NextDouble() * 1.8f;
                var right = Vector3.Cross(Vector3.up, dir).normalized * s;
                var up = Vector3.Cross(dir, right).normalized * s;
                int b = verts.Count;
                verts.Add(p - right - up); verts.Add(p + right - up); verts.Add(p + right + up); verts.Add(p - right + up);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) normals.Add(-dir);
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "Stars" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            var mat = Mats.NewGlow(new Color(tint.r, tint.g, tint.b, 0.8f), 1.6f, 0f);
            mat.mainTexture = Glow;
            mat.renderQueue = 1001;
            var stars = Prim.Create(mesh, "Stars", parent, Vector3.zero, Vector3.one, mat);
            stars.transform.position = center;
            stars.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Sun glow opposite the light direction.
            var sunDir = -(Quaternion.Euler(52f, -35f, 0f) * Vector3.forward);
            var sunMat = Mats.NewGlow(new Color(1f, 0.85f, 0.6f, 0.9f), 3f, 0f);
            sunMat.mainTexture = Glow;
            sunMat.renderQueue = 1002;
            var sun = Prim.Create(Prim.CubeMesh, "SunGlow", parent, Vector3.zero, Vector3.one, sunMat);
            sun.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sun.transform.position = center + sunDir * 360f;
            sun.transform.rotation = Quaternion.LookRotation(sunDir);
            sun.transform.localScale = new Vector3(70f, 70f, 0.1f);
        }

        /// <summary>Floating motes drifting through the arena.</summary>
        public static ParticleSystem Motes(Transform parent, Bounds bounds, Color color)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            go.transform.position = bounds.center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = color;
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.004f;
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Clamp(bounds.size.x * bounds.size.z * 0.025f, 15f, 60f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = bounds.size;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = Mats.NewTransparent(Color.white);
            mat.mainTexture = Glow;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Simulate(8f, true, true);
            ps.Play();
            return ps;
        }
    }

    /// <summary>A short-lived point light (muzzle flashes, explosions).</summary>
    public class FxLight : MonoBehaviour
    {
        public float Life = 0.1f;
        float t, start;
        Light l;

        void Start()
        {
            l = GetComponent<Light>();
            start = l.intensity;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Life);
            l.intensity = start * (1f - k) * (1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Trauma-based screen shake for player cameras.</summary>
    public static class CameraShake
    {
        /// <summary>Shake every player camera near a point (explosions, impacts). Mirrored online.</summary>
        public static void Impulse(Vector3 position, float strength, float radius = 25f)
        {
            if (Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorShake(position, strength);
            foreach (var cam in PlayerCamera.Active)
            {
                if (cam == null || cam.Target == null) continue;
                float d = Vector3.Distance(cam.Target.Center, position);
                cam.AddTrauma(strength * Mathf.Clamp01(1f - d / radius));
            }
        }
    }
}
