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

        void Update()
        {
            float dt = Time.deltaTime;
            Life -= dt;
            if (Life <= 0f) { Destroy(gameObject); return; }
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
        float t;
        Material mat;

        void Start()
        {
            mat = Mats.NewTransparent(Color);
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
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size = 0.15f, float life = 0.6f, float gravity = 20f)
        {
            if (Mirror) Net.NetSession.Instance.MirrorBurst(pos, color, count, speed, size, life, gravity);
            var mat = Mats.Solid(color);
            for (int i = 0; i < count; i++)
            {
                var go = Prim.Cube("fx", Root, pos, Vector3.one * size * Random.Range(0.6f, 1.3f), mat);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = go.AddComponent<FxParticle>();
                p.Velocity = Random.onUnitSphere * speed * Random.Range(0.4f, 1f);
                p.Life = life * Random.Range(0.7f, 1.2f);
                p.Gravity = gravity;
                p.Spin = Random.insideUnitSphere * 720f;
            }
        }

        public static void Pulse(Vector3 pos, Color color, float fromDiameter, float toDiameter, float life)
        {
            if (Mirror) Net.NetSession.Instance.MirrorPulse(pos, color, fromDiameter, toDiameter, life);
            var go = Prim.Sphere("pulse", Root, pos, fromDiameter, Mats.Transparent(color));
            var p = go.AddComponent<FxPulse>();
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
            lr.sharedMaterial = Mats.Transparent(Color.white);
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<FxLine>();
            l.Life = life;
            l.Color = color;
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
