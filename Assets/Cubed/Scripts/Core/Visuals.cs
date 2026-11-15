using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// Runtime material factory. Everything in Cubed is built from code, so materials are cloned from
    /// Unity's built-in default material (always shipped in builds) or the always-included sprite shader.
    /// </summary>
    public static class Mats
    {
        static Material opaqueTemplate;
        static Shader transparentShader;
        static Material lineMaterial;
        static readonly Dictionary<int, Material> solid = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> transparent = new Dictionary<int, Material>();

        static int Key(Color c)
        {
            Color32 k = c;
            return (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
        }

        static Material OpaqueTemplate
        {
            get
            {
                if (opaqueTemplate == null)
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    probe.SetActive(false);
                    opaqueTemplate = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                    Object.Destroy(probe);
                }
                return opaqueTemplate;
            }
        }

        /// <summary>Shared opaque lit material for a color.</summary>
        public static Material Solid(Color c)
        {
            int key = Key(c);
            if (solid.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(OpaqueTemplate) { color = c, name = "Cubed_Solid" };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.18f);
            solid[key] = m;
            return m;
        }

        /// <summary>Shared unlit alpha-blended material for a color.</summary>
        public static Material Transparent(Color c)
        {
            int key = Key(c);
            if (transparent.TryGetValue(key, out var m) && m != null) return m;
            m = NewTransparent(c);
            transparent[key] = m;
            return m;
        }

        /// <summary>A fresh (unshared) transparent material, for objects that fade individually.</summary>
        public static Material NewTransparent(Color c)
        {
            if (transparentShader == null)
            {
                transparentShader = Shader.Find("Sprites/Default");
                if (transparentShader == null) transparentShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            }
            var m = transparentShader != null ? new Material(transparentShader) : new Material(OpaqueTemplate);
            m.color = c;
            m.name = "Cubed_Transparent";
            m.renderQueue = 3000;
            return m;
        }

        static Shader glowShader, shieldShader;
        static bool glowChecked;
        static readonly Dictionary<long, Material> glow = new Dictionary<long, Material>();
        static Material hdrTrail;

        static void CheckShaders()
        {
            if (glowChecked) return;
            glowChecked = true;
            glowShader = Shader.Find("Cubed/Glow");
            if (glowShader != null && !glowShader.isSupported) glowShader = null;
            shieldShader = Shader.Find("Cubed/Shield");
            if (shieldShader != null && !shieldShader.isSupported) shieldShader = null;
        }

        public static bool GlowAvailable { get { CheckShaders(); return glowShader != null; } }

        /// <summary>Additive HDR glow (feeds bloom). soft = edge falloff for spheres, 0 for flat glow.</summary>
        public static Material Glow(Color c, float intensity = 2f, float soft = 1.5f)
        {
            CheckShaders();
            long key = ((long)Key(c) << 16) ^ (Mathf.RoundToInt(intensity * 20f) << 8) ^ Mathf.RoundToInt(soft * 20f);
            if (glow.TryGetValue(key, out var m) && m != null) return m;
            m = NewGlow(c, intensity, soft);
            glow[key] = m;
            return m;
        }

        public static Material NewGlow(Color c, float intensity = 2f, float soft = 1.5f)
        {
            CheckShaders();
            if (glowShader == null)
            {
                var fallback = NewTransparent(new Color(Mathf.Min(1f, c.r * 1.3f), Mathf.Min(1f, c.g * 1.3f), Mathf.Min(1f, c.b * 1.3f), c.a * 0.8f));
                return fallback;
            }
            var m = new Material(glowShader) { name = "Cubed_Glow", color = c };
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Soft", soft);
            m.renderQueue = 3100;
            return m;
        }

        /// <summary>Energy-shield material (one per player so hit flashes are individual).</summary>
        public static Material NewShield(Color c)
        {
            CheckShaders();
            if (shieldShader == null) return NewTransparent(new Color(c.r, c.g, c.b, 0.28f));
            var m = new Material(shieldShader) { name = "Cubed_Shield", color = new Color(c.r, c.g, c.b, 0.85f) };
            return m;
        }

        /// <summary>Bright (HDR) trail material so bullet trails bloom.</summary>
        public static Material HdrTrail
        {
            get
            {
                if (hdrTrail == null)
                {
                    hdrTrail = NewTransparent(Color.white);
                    hdrTrail.color = new Color(2.2f, 2.2f, 2.2f, 1f);
                }
                return hdrTrail;
            }
        }

        /// <summary>Material for GL line drawing.</summary>
        public static Material Lines
        {
            get
            {
                if (lineMaterial == null)
                {
                    var shader = Shader.Find("Hidden/Internal-Colored");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    if (lineMaterial.HasProperty("_SrcBlend")) lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (lineMaterial.HasProperty("_DstBlend")) lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if (lineMaterial.HasProperty("_ZWrite")) lineMaterial.SetInt("_ZWrite", 0);
                    if (lineMaterial.HasProperty("_Cull")) lineMaterial.SetInt("_Cull", 0);
                }
                return lineMaterial;
            }
        }
    }

    /// <summary>Lightweight primitive creation without the throwaway colliders of CreatePrimitive.</summary>
    public static class Prim
    {
        static Mesh cubeMesh, sphereMesh;

        public static Mesh CubeMesh { get { EnsureMeshes(); return cubeMesh; } }
        public static Mesh SphereMesh { get { EnsureMeshes(); return sphereMesh; } }

        static void EnsureMeshes()
        {
            if (cubeMesh != null && sphereMesh != null) return;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.SetActive(false);
            cubeMesh = c.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(c);
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.SetActive(false);
            sphereMesh = s.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(s);
        }

        public static GameObject Create(Mesh mesh, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool collider = false)
        {
            var go = Create(CubeMesh, name, parent, localPos, localScale, mat);
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 localPos, float diameter, Material mat)
        {
            var go = Create(SphereMesh, name, parent, localPos, Vector3.one * diameter, mat);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>A tiny flying cube used for bursts, sparks and debris.</summary>
    public class FxParticle : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Life = 0.6f;
        public float Gravity = 20f;
        public Vector3 Spin;
        float maxLife;
        Vector3 startScale;

        void Start()
        {
            maxLife = Mathf.Max(0.01f, Life);
            startScale = transform.localScale;
        }

        public void Restart(float life)
        {
            Life = maxLife = Mathf.Max(0.01f, life);
            startScale = transform.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Life -= dt;
            if (Life <= 0f) { Fx.ReleaseParticle(this); return; }
            Velocity += Vector3.down * Gravity * dt;
            Velocity *= 1f - Mathf.Clamp01(1.5f * dt);
            transform.position += Velocity * dt;
            transform.Rotate(Spin * dt, Space.Self);
            transform.localScale = startScale * Mathf.Clamp01(Life / maxLife * 1.5f);
        }
    }

    /// <summary>An expanding, fading sphere (shields, explosions, shockwaves).</summary>
    public class FxPulse : MonoBehaviour
    {
        public float From = 0.5f, To = 3f, Life = 0.35f;
        public Color Color = Color.white;
        public bool Glow;
        public float GlowIntensity = 2.5f;
        float t;
        Material mat;

        void Start()
        {
            mat = Glow ? Mats.NewGlow(Color, GlowIntensity, 1.2f) : Mats.NewTransparent(Color);
            GetComponent<Renderer>().sharedMaterial = mat;
            transform.localScale = Vector3.one * From;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Life);
            transform.localScale = Vector3.one * Mathf.Lerp(From, To, 1f - (1f - k) * (1f - k));
            var c = Color;
            c.a *= 1f - k;
            mat.color = c;
            if (k >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }

    /// <summary>A soft smoke puff.</summary>
    public class FxSmoke : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Life = 1.5f, Growth = 1f;
        public Color Color = new Color(0.3f, 0.3f, 0.32f, 0.5f);
        float t, start;
        Material mat;

        void Start()
        {
            mat = Mats.NewTransparent(Color);
            GetComponent<Renderer>().sharedMaterial = mat;
            start = transform.localScale.x;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / Life);
            transform.position += Velocity * dt;
            Velocity *= 1f - dt * 1.2f;
            transform.localScale = Vector3.one * (start + Growth * Mathf.Sqrt(k));
            var c = Color;
            c.a *= (1f - k) * Mathf.Clamp01(t * 8f);
            mat.color = c;
            if (k >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }

    /// <summary>A short-lived fading line (zaps, teleports).</summary>
    public class FxLine : MonoBehaviour
    {
        public float Life = 0.2f;
        public Color Color = Color.white;
        LineRenderer lr;
        float t;

        void Start() { lr = GetComponent<LineRenderer>(); }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Life);
            var c = Color;
            c.a *= 1f - k;
            lr.startColor = c;
            lr.endColor = c;
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Destroys the GameObject after a delay.</summary>
    public class TimedDestroy : MonoBehaviour
    {
        public float Seconds = 1f;
        void Update()
        {
            Seconds -= Time.deltaTime;
            if (Seconds <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>Spawns simple procedural effects.</summary>
    public static class Fx
    {
        static Transform root;

        /// <summary>
        /// Set while spawning purely cosmetic effects that every machine generates itself (animation dust,
        /// footsteps) so the host doesn't also mirror them to clients.
        /// </summary>
        public static bool LocalOnly;
        static bool Mirror => !LocalOnly && Net.NetSession.Mirroring;

        /// <summary>A short point-light flash. Mirrored online.</summary>
        public static void Light(Vector3 pos, Color color, float range, float intensity, float life)
        {
            if (Mirror) Net.NetSession.Instance.MirrorLight(pos, color, range, intensity, life);
            if (Options.Current.reduceFlashes) intensity *= 0.35f;
            var go = new GameObject("flash");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForceVertex;
            go.AddComponent<FxLight>().Life = life;
        }

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("Fx").transform;
                return root;
            }
        }

        /// <summary>Removes every live effect (between rounds).</summary>
        public static void Clear()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            particlePool.Clear(); // pooled particles lived under the destroyed root
        }

        static readonly Stack<FxParticle> particlePool = new Stack<FxParticle>();

        internal static void ReleaseParticle(FxParticle p)
        {
            p.gameObject.SetActive(false);
            if (particlePool.Count < 800) particlePool.Push(p);
            else Object.Destroy(p.gameObject);
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size = 0.15f, float life = 0.6f, float gravity = 20f)
        {
            if (Mirror) Net.NetSession.Instance.MirrorBurst(pos, color, count, speed, size, life, gravity);
            var mat = Mats.Solid(color);
            var parent = Root;
            for (int i = 0; i < count; i++)
            {
                FxParticle p = null;
                while (particlePool.Count > 0 && p == null) p = particlePool.Pop();
                Vector3 scale = Vector3.one * size * Random.Range(0.6f, 1.3f);
                if (p == null)
                {
                    var go = Prim.Cube("fx", parent, pos, scale, mat);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    p = go.AddComponent<FxParticle>();
                }
                else
                {
                    p.transform.position = pos;
                    p.transform.localScale = scale;
                    p.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    p.gameObject.SetActive(true);
                }
                p.Velocity = Random.onUnitSphere * speed * Random.Range(0.4f, 1f);
                p.Gravity = gravity;
                p.Spin = Random.insideUnitSphere * 720f;
                p.Restart(life * Random.Range(0.7f, 1.2f));
            }
        }

        public static void Pulse(Vector3 pos, Color color, float fromDiameter, float toDiameter, float life) =>
            Pulse(pos, color, fromDiameter, toDiameter, life, false);

        /// <summary>Expanding sphere; glow = additive HDR (bright, blooms).</summary>
        public static void Pulse(Vector3 pos, Color color, float fromDiameter, float toDiameter, float life, bool glow, float intensity = 2.5f)
        {
            if (Mirror) Net.NetSession.Instance.MirrorPulse(pos, color, fromDiameter, toDiameter, life, glow, intensity);
            var go = Prim.Sphere("pulse", Root, pos, fromDiameter, Mats.Transparent(color));
            var p = go.AddComponent<FxPulse>();
            p.Glow = glow;
            p.GlowIntensity = intensity;
            p.From = fromDiameter;
            p.To = toDiameter;
            p.Life = life;
            p.Color = color;
        }

        public static void Line(Vector3 a, Vector3 b, Color color, float width = 0.08f, float life = 0.2f)
        {
            if (Mirror) Net.NetSession.Instance.MirrorLine(a, b, color, width, life);
            var go = new GameObject("line");
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sharedMaterial = Mats.HdrTrail;
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<FxLine>();
            l.Life = life;
            l.Color = color;
        }

        static Mesh quad;
        static readonly List<Transform> decals = new List<Transform>();
        static int nextDecal;
        const int MaxDecals = 160;

        static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "FxQuad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        /// <summary>A scorch/impact mark on a surface (ring buffer, mirrored online).</summary>
        public static void Decal(Vector3 point, Vector3 normal, float size, Color color)
        {
            if (Mirror) Net.NetSession.Instance.MirrorDecal(point, normal, size, color);
            decals.RemoveAll(d => d == null);
            Transform t;
            if (decals.Count < MaxDecals)
            {
                var mat = Mats.NewTransparent(color);
                mat.mainTexture = Looks.Glow;
                mat.renderQueue = 2990;
                var go = Prim.Create(Quad, "decal", Root, Vector3.zero, Vector3.one, mat);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t = go.transform;
                decals.Add(t);
            }
            else
            {
                t = decals[nextDecal % decals.Count];
                nextDecal++;
                t.GetComponent<MeshRenderer>().sharedMaterial.color = color;
            }
            t.position = point + normal * 0.012f;
            t.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            t.localScale = Vector3.one * size;
        }

        /// <summary>Rising, expanding, fading smoke puffs (mirrored online).</summary>
        public static void Smoke(Vector3 pos, int count, float size, Color color)
        {
            if (Mirror) Net.NetSession.Instance.MirrorSmoke(pos, count, size, color);
            for (int i = 0; i < count; i++)
            {
                var go = Prim.Sphere("smoke", Root, pos + Random.insideUnitSphere * size * 0.6f, size * Random.Range(0.6f, 1.1f), Mats.Transparent(color));
                var s = go.AddComponent<FxSmoke>();
                s.Color = color;
                s.Velocity = Vector3.up * Random.Range(0.8f, 2.2f) + Random.insideUnitSphere * 1.2f;
                s.Life = Random.Range(1.2f, 2.2f);
                s.Growth = size * Random.Range(0.8f, 1.6f);
            }
        }

        /// <summary>A temporary cube visual (no collider), mirrored to online clients.</summary>
        public static GameObject Box(Vector3 pos, Quaternion rot, Vector3 size, Color color, float life, bool mirror = true)
        {
            if (mirror && Mirror) Net.NetSession.Instance.MirrorBox(pos, rot, size, color, life);
            var mat = color.a < 0.99f ? Mats.Transparent(color) : Mats.Solid(color);
            var go = Prim.Cube("box", Root, pos, size, mat);
            go.transform.rotation = rot;
            if (life > 0f) go.AddComponent<TimedDestroy>().Seconds = life;
            return go;
        }

        /// <summary>A jagged lightning line made from several segments.</summary>
        public static void Zap(Vector3 a, Vector3 b, Color color)
        {
            Vector3 prev = a;
            const int segments = 6;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = Vector3.Lerp(a, b, i / (float)segments);
                if (i < segments) next += Random.insideUnitSphere * 0.35f;
                Line(prev, next, color, 0.07f, 0.18f);
                prev = next;
            }
        }
    }
}
