using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// A moving part of a map (door, platform, spinner, pendulum, saw, spike trap, piston).
    /// Motion is a pure function of the round clock, so every machine (host and clients) computes
    /// the same pose without sending transforms over the network.
    /// </summary>
    [Serializable]
    public class MechData
    {
        /// <summary>One of <see cref="MechKinds"/>.</summary>
        public string kind = MechKinds.Door;
        /// <summary>Anchor cell. For most kinds the body occupies x..x+w, y..y+h, z..z+d; a pendulum hangs from this cell.</summary>
        public int x, y, z;
        public int w = 1, h = 1, d = 1;
        /// <summary>Travel in cells (door, platform, elevator, piston, saw).</summary>
        public float dx, dy, dz;
        /// <summary>Rotation axis for spinners, pendulums and saws: 0 = X, 1 = Y, 2 = Z.</summary>
        public int axis = 1;
        /// <summary>Seconds per full cycle (negative spins the other way).</summary>
        public float period = 4f;
        /// <summary>Cycle offset, 0..1, so neighbouring parts can be staggered.</summary>
        public float phase;
        /// <summary>Pendulum swing amplitude in degrees.</summary>
        public float angle = 60f;
        /// <summary>Pendulum arm length in cells.</summary>
        public float length = 5f;
        /// <summary>Hurts on contact.</summary>
        public bool deadly;
        public float damage = 30f;
        /// <summary>Block type used for the body's look.</summary>
        public string type = "dark";

        public MechData Clone() => (MechData)MemberwiseClone();
        public Vector3 Origin => new Vector3(x, y, z);
        public Vector3 Size => new Vector3(Mathf.Max(1, w), Mathf.Max(1, h), Mathf.Max(1, d));
        public Vector3 Center => Origin + Size * 0.5f;
        public Vector3 Travel => new Vector3(dx, dy, dz);
    }

    public static class MechKinds
    {
        public const string Door = "door";
        public const string Platform = "platform";
        public const string Elevator = "elevator";
        public const string Piston = "piston";
        public const string Spinner = "spinner";
        public const string Pendulum = "pendulum";
        public const string Saw = "saw";
        public const string Spikes = "spikes";

        public static readonly string[] All = { Door, Platform, Elevator, Piston, Spinner, Pendulum, Saw, Spikes };

        public static string Name(string kind)
        {
            switch (kind)
            {
                case Door: return "Sliding Door";
                case Platform: return "Moving Platform";
                case Elevator: return "Elevator";
                case Piston: return "Piston";
                case Spinner: return "Spinner";
                case Pendulum: return "Swinging Spikes";
                case Saw: return "Saw Blade";
                case Spikes: return "Spike Trap";
                default: return kind;
            }
        }

        /// <summary>Sensible defaults for a part placed in the editor at a cell.</summary>
        public static MechData Preset(string kind, int x, int y, int z)
        {
            var m = new MechData { kind = kind, x = x, y = y, z = z };
            switch (kind)
            {
                case Door: m.w = 1; m.h = 3; m.d = 3; m.dy = 3f; m.period = 6f; m.type = "dark"; break;
                case Platform: m.w = 3; m.h = 1; m.d = 3; m.dx = 6f; m.period = 7f; m.type = "light"; break;
                case Elevator: m.w = 3; m.h = 1; m.d = 3; m.dy = 6f; m.period = 8f; m.type = "light"; break;
                case Piston: m.w = 2; m.h = 2; m.d = 2; m.dx = 3f; m.period = 3f; m.type = "solid"; break;
                case Spinner: m.w = 9; m.h = 1; m.d = 1; m.axis = 1; m.period = 5f; m.type = "dark"; break;
                case Pendulum: m.w = 2; m.h = 2; m.d = 2; m.axis = 2; m.period = 3.2f; m.length = 5f; m.angle = 65f; m.deadly = true; m.damage = 35f; break;
                case Saw: m.w = 3; m.h = 1; m.d = 3; m.axis = 1; m.dx = 6f; m.period = 5f; m.deadly = true; m.damage = 40f; break;
                case Spikes: m.w = 2; m.h = 1; m.d = 2; m.period = 3f; m.deadly = true; m.damage = 30f; break;
            }
            return m;
        }

        /// <summary>Death-message cause for a deadly part.</summary>
        public static string Cause(string kind) =>
            kind == Pendulum ? "pendulum" : kind == Saw ? "saw" : kind == Spikes ? "spikes" : kind == Piston ? "piston" : kind == Spinner ? "spinner" : "trap";
    }

    /// <summary>Runtime part of a <see cref="MechData"/>. Driven by <see cref="MapRuntime"/>.</summary>
    public class Mechanism : MonoBehaviour
    {
        public static readonly List<Mechanism> All = new List<Mechanism>();

        public MechData Data;
        public int Index;
        /// <summary>The part that moves. Players riding colliders under it are carried along.</summary>
        public Transform Body { get; private set; }
        /// <summary>0..1: how dangerous the part is right now (spike traps telegraph before 1).</summary>
        public float Threat { get; private set; } = 1f;

        enum HurtShape { Box, Sphere, Disc }

        struct Hurt
        {
            public Transform T;
            public HurtShape Shape;
            public Vector3 Size; // box: full size; sphere: x = radius; disc: x = radius, y = thickness
        }

        readonly List<Hurt> hurts = new List<Hurt>();
        readonly List<Collider> solids = new List<Collider>();
        readonly Dictionary<Player, float> hitCooldown = new Dictionary<Player, float>();
        Matrix4x4 prevBody, curBody;
        bool hasPose;
        Transform spin;         // saw disc / spike rods
        Transform spikeGroup;   // spike trap rods
        Material warnMat;
        Color warnBase;
        Vector3 lastBodyPos;
        Vector3 bodyVelocity;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
        void OnDestroy()
        {
            All.Remove(this);
            if (warnMat != null) Destroy(warnMat);
        }

        // ------------------------------------------------------------------ building

        static Mesh spikeMesh;

        /// <summary>A four-sided pyramid, base on y = 0, tip at y = 1.</summary>
        public static Mesh SpikeMesh
        {
            get
            {
                if (spikeMesh != null) return spikeMesh;
                var v = new List<Vector3>();
                var tris = new List<int>();
                Vector3 tip = new Vector3(0f, 1f, 0f);
                Vector3[] b = { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
                for (int i = 0; i < 4; i++)
                {
                    int s = v.Count;
                    v.Add(b[i]); v.Add(tip); v.Add(b[(i + 1) % 4]);
                    tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                }
                int bs = v.Count;
                v.AddRange(b);
                tris.AddRange(new[] { bs, bs + 1, bs + 2, bs, bs + 2, bs + 3 });
                spikeMesh = new Mesh { name = "Spike" };
                spikeMesh.SetVertices(v);
                spikeMesh.SetTriangles(tris, 0);
                spikeMesh.RecalculateNormals();
                spikeMesh.RecalculateBounds();
                return spikeMesh;
            }
        }

        static readonly Color Steel = new Color(0.72f, 0.75f, 0.82f);
        static readonly Color Danger = new Color(1f, 0.22f, 0.16f);

        public static Mechanism Create(MechData data, int index, Transform parent)
        {
            var root = new GameObject("Mech_" + data.kind);
            root.transform.SetParent(parent, false);
            var mech = root.AddComponent<Mechanism>();
            mech.Data = data;
            mech.Index = index;
            mech.Build();
            mech.Evaluate(0f);
            mech.prevBody = mech.curBody;
            return mech;
        }

        Material BodyMat(Color c) => Looks.Block(c);

        GameObject SolidBox(Transform parent, Vector3 localPos, Vector3 size, Material mat, bool tiled = true)
        {
            var go = Prim.Create(tiled ? Looks.TiledBox(size.x, size.y, size.z) : Prim.CubeMesh, "Part", parent, localPos, tiled ? Vector3.one : size, mat);
            var col = go.AddComponent<BoxCollider>();
            if (tiled) col.size = size;
            solids.Add(col);
            return go;
        }

        GameObject Spike(Transform parent, Vector3 localPos, Quaternion rot, float baseSize, float height, Material mat)
        {
            var go = Prim.Create(SpikeMesh, "Spike", parent, localPos, new Vector3(baseSize, height, baseSize), mat);
            go.transform.localRotation = rot;
            return go;
        }

        void Build()
        {
            var d = Data;
            var type = BlockTypes.Get(d.type);
            var bodyMat = BodyMat(type.Color);
            var steel = BodyMat(Steel);
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            Vector3 size = d.Size;

            switch (d.kind)
            {
                case MechKinds.Spinner:
                {
                    transform.position = d.Center;
                    SolidBox(Body, Vector3.zero, size, bodyMat);
                    // Hub.
                    var hubAxis = AxisVector(d.axis);
                    var hub = Prim.Create(Prim.SphereMesh, "Hub", transform, Vector3.zero, Vector3.one * (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) + 0.5f), steel);
                    hub.transform.localScale += hubAxis * 0.2f;
                    if (d.deadly)
                    {
                        AddDangerTrim(Body, size);
                        hurts.Add(new Hurt { T = Body, Shape = HurtShape.Box, Size = size + Vector3.one * 0.1f });
                    }
                    break;
                }
                case MechKinds.Pendulum:
                {
                    transform.position = d.Origin + Vector3.one * 0.5f;
                    float len = Mathf.Max(1.5f, d.length);
                    float head = Mathf.Max(0.8f, d.w);
                    // Ceiling mount stays put.
                    Prim.Cube("Mount", transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 1.2f), steel, true);
                    Prim.Create(Prim.SphereMesh, "Pivot", transform, Vector3.zero, Vector3.one * 0.6f, steel);
                    Prim.Cube("Arm", Body, new Vector3(0f, -len * 0.5f, 0f), new Vector3(0.18f, len, 0.18f), steel);
                    var ball = Prim.Create(Prim.SphereMesh, "Head", Body, new Vector3(0f, -len, 0f), Vector3.one * head, BodyMat(new Color(0.24f, 0.25f, 0.3f)));
                    var sc = ball.AddComponent<SphereCollider>();
                    solids.Add(sc);
                    var spikeMat = BodyMat(Steel);
                    // Spikes all around the ball.
                    Vector3[] dirs =
                    {
                        Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                        new Vector3(1, 1, 0).normalized, new Vector3(-1, 1, 0).normalized, new Vector3(1, -1, 0).normalized, new Vector3(-1, -1, 0).normalized,
                        new Vector3(0, 1, 1).normalized, new Vector3(0, -1, 1).normalized, new Vector3(0, 1, -1).normalized, new Vector3(0, -1, -1).normalized,
                    };
                    foreach (var dir in dirs)
                    {
                        if (dir == Vector3.up) continue; // that's where the arm is
                        Spike(Body, new Vector3(0f, -len, 0f) + dir * head * 0.42f, Quaternion.FromToRotation(Vector3.up, dir), head * 0.32f, head * 0.5f, spikeMat);
                    }
                    if (Mats.GlowAvailable)
                    {
                        var g = Prim.Create(Prim.SphereMesh, "Glow", Body, new Vector3(0f, -len, 0f), Vector3.one * (head * 0.6f), Mats.Glow(new Color(1f, 0.3f, 0.2f, 0.35f), 1.6f));
                        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    var hurtT = new GameObject("HurtHead").transform;
                    hurtT.SetParent(Body, false);
                    hurtT.localPosition = new Vector3(0f, -len, 0f);
                    hurts.Add(new Hurt { T = hurtT, Shape = HurtShape.Sphere, Size = new Vector3(head * 0.5f + head * 0.35f, 0f, 0f) });
                    break;
                }
                case MechKinds.Saw:
                {
                    transform.position = d.Center;
                    float radius = Mathf.Max(size.x, size.z) * 0.5f;
                    var axisRot = Quaternion.FromToRotation(Vector3.up, AxisVector(d.axis));
                    // Carrier rail (static) shows where the saw travels.
                    var travel = d.Travel;
                    if (travel.sqrMagnitude > 0.01f)
                    {
                        var rail = Prim.Cube("Rail", transform, travel * 0.5f, Vector3.one, BodyMat(new Color(0.16f, 0.17f, 0.2f)));
                        rail.transform.localRotation = Quaternion.LookRotation(travel.normalized, Mathf.Abs(Vector3.Dot(travel.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
                        rail.transform.localScale = new Vector3(0.25f, 0.12f, travel.magnitude + radius * 0.4f);
                    }
                    spin = new GameObject("Disc").transform;
                    spin.SetParent(Body, false);
                    spin.localRotation = axisRot;
                    var disc = Prim.Create(Prim.SphereMesh, "Blade", spin, Vector3.zero, new Vector3(radius * 2f, 0.14f, radius * 2f), steel);
                    disc.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    var teethMat = BodyMat(new Color(0.85f, 0.87f, 0.92f));
                    int teeth = Mathf.Clamp(Mathf.RoundToInt(radius * 9f), 10, 28);
                    for (int i = 0; i < teeth; i++)
                    {
                        float a = i * Mathf.PI * 2f / teeth;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Spike(spin, dir * radius * 0.92f, Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 45f, 0f), 0.16f, 0.34f, teethMat);
                    }
                    Prim.Create(Prim.SphereMesh, "Hub", spin, Vector3.zero, new Vector3(0.6f, 0.3f, 0.6f), BodyMat(Danger));
                    // A thin collider so bullets clink off the blade.
                    var bladeCol = new GameObject("BladeCollider");
                    bladeCol.transform.SetParent(Body, false);
                    bladeCol.transform.localRotation = axisRot;
                    var bc = bladeCol.AddComponent<BoxCollider>();
                    bc.size = new Vector3(radius * 1.5f, 0.12f, radius * 1.5f);
                    var hurtT = new GameObject("HurtDisc").transform;
                    hurtT.SetParent(Body, false);
                    hurtT.localRotation = axisRot;
                    hurts.Add(new Hurt { T = hurtT, Shape = HurtShape.Disc, Size = new Vector3(radius + 0.15f, 0.5f, 0f) });
                    break;
                }
                case MechKinds.Spikes:
                {
                    // Floor plate at the anchor cell's floor; spikes rise into the cell above.
                    transform.position = new Vector3(d.x + size.x * 0.5f, d.y, d.z + size.z * 0.5f);
                    var plateMat = BodyMat(new Color(0.22f, 0.22f, 0.26f));
                    var plate = Prim.Cube("Plate", transform, new Vector3(0f, 0.04f, 0f), new Vector3(size.x, 0.08f, size.z), plateMat, true);
                    plate.GetComponent<MeshRenderer>().receiveShadows = true;
                    warnBase = new Color(1f, 0.25f, 0.15f, 0.0f);
                    if (Mats.GlowAvailable)
                    {
                        warnMat = Mats.NewGlow(warnBase, 2.2f, 0f);
                        var warn = Prim.Cube("Warn", transform, new Vector3(0f, 0.085f, 0f), new Vector3(size.x * 0.96f, 0.01f, size.z * 0.96f), warnMat);
                        warn.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    spikeGroup = Body;
                    var spikeMat = BodyMat(Steel);
                    int nx = Mathf.Max(1, Mathf.RoundToInt(size.x * 2f)), nz = Mathf.Max(1, Mathf.RoundToInt(size.z * 2f));
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                        {
                            var p = new Vector3(-size.x * 0.5f + (ix + 0.5f) * size.x / nx, 0f, -size.z * 0.5f + (iz + 0.5f) * size.z / nz);
                            Spike(Body, p, Quaternion.identity, 0.32f, 0.9f, spikeMat);
                        }
                    var hurtT = new GameObject("HurtSpikes").transform;
                    hurtT.SetParent(Body, false);
                    hurtT.localPosition = new Vector3(0f, 0.45f, 0f);
                    hurts.Add(new Hurt { T = hurtT, Shape = HurtShape.Box, Size = new Vector3(size.x, 0.9f, size.z) });
                    break;
                }
                default: // door, platform, elevator, piston
                {
                    transform.position = d.Center;
                    SolidBox(Body, Vector3.zero, size, bodyMat);
                    if (d.kind == MechKinds.Platform || d.kind == MechKinds.Elevator)
                    {
                        // Glowing edge strip so moving platforms read at a glance.
                        if (Mats.GlowAvailable)
                        {
                            var strip = Prim.Cube("Edge", Body, new Vector3(0f, size.y * 0.5f + 0.005f, 0f), new Vector3(size.x + 0.02f, 0.02f, size.z + 0.02f), Mats.Glow(new Color(0.4f, 0.85f, 1f, 0.5f), 2f, 0f));
                            strip.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                    }
                    if (d.kind == MechKinds.Door) AddDoorFrameLines(size);
                    if (d.deadly)
                    {
                        // Spikes on the leading face (the travel direction).
                        var dir = d.Travel.sqrMagnitude > 0.01f ? d.Travel.normalized : Vector3.up;
                        var axisDir = DominantAxis(dir);
                        AddFaceSpikes(Body, size, axisDir);
                        var hurtT = new GameObject("HurtFace").transform;
                        hurtT.SetParent(Body, false);
                        float ext = Vector3.Scale(size, Abs(axisDir)).magnitude * 0.5f;
                        hurtT.localPosition = axisDir * (ext + 0.25f);
                        var hs = size - Vector3.Scale(size, Abs(axisDir)) + Abs(axisDir) * 0.6f;
                        hurts.Add(new Hurt { T = hurtT, Shape = HurtShape.Box, Size = hs });
                    }
                    break;
                }
            }

            if (solids.Count > 0)
            {
                var rb = Body.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        void AddDangerTrim(Transform parent, Vector3 size)
        {
            // Spikes along the long ends of a deadly spinner.
            int longAxis = size.x >= size.y && size.x >= size.z ? 0 : size.z >= size.y ? 2 : 1;
            var axisDir = AxisVector(longAxis);
            var spikeMat = BodyMat(Steel);
            float half = size[longAxis] * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                var dir = axisDir * s;
                Spike(parent, dir * half, Quaternion.FromToRotation(Vector3.up, dir), 0.6f, 0.7f, spikeMat);
            }
            if (Mats.GlowAvailable)
            {
                var g = Prim.Cube("Danger", parent, Vector3.zero, size + Vector3.one * 0.03f, Mats.Glow(new Color(1f, 0.25f, 0.15f, 0.18f), 1.6f, 0f));
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void AddFaceSpikes(Transform parent, Vector3 size, Vector3 axisDir)
        {
            var spikeMat = BodyMat(Steel);
            Vector3 a = Mathf.Abs(axisDir.x) > 0.5f ? Vector3.forward : Vector3.right;
            Vector3 b = Mathf.Abs(axisDir.y) > 0.5f ? Vector3.forward : Vector3.up;
            if (Mathf.Abs(axisDir.y) > 0.5f) a = Vector3.right;
            float sa = Mathf.Abs(Vector3.Dot(size, a)), sb = Mathf.Abs(Vector3.Dot(size, b));
            float face = Mathf.Abs(Vector3.Dot(size, axisDir)) * 0.5f;
            int na = Mathf.Max(1, Mathf.RoundToInt(sa * 1.5f)), nb = Mathf.Max(1, Mathf.RoundToInt(sb * 1.5f));
            var rot = Quaternion.FromToRotation(Vector3.up, axisDir);
            for (int i = 0; i < na; i++)
                for (int j = 0; j < nb; j++)
                {
                    var p = axisDir * face + a * (-sa * 0.5f + (i + 0.5f) * sa / na) + b * (-sb * 0.5f + (j + 0.5f) * sb / nb);
                    Spike(parent, p, rot, 0.4f, 0.55f, spikeMat);
                }
        }

        void AddDoorFrameLines(Vector3 size)
        {
            if (!Mats.GlowAvailable) return;
            // Hazard stripes along the bottom edge.
            var g = Prim.Cube("DoorEdge", Body, new Vector3(0f, -size.y * 0.5f + 0.06f, 0f), new Vector3(size.x + 0.02f, 0.1f, size.z + 0.02f), Mats.Glow(new Color(1f, 0.75f, 0.2f, 0.45f), 1.8f, 0f));
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 2 ? Vector3.forward : Vector3.up;
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Vector3 DominantAxis(Vector3 v)
        {
            var a = Abs(v);
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (a.z >= a.y) return new Vector3(0f, 0f, Mathf.Sign(v.z));
            return new Vector3(0f, Mathf.Sign(v.y), 0f);
        }

        // ------------------------------------------------------------------ motion

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>0 → 1 → 0 over a cycle, resting <paramref name="hold"/> of the cycle at each end.</summary>
        static float PingPong(float u, float hold)
        {
            hold = Mathf.Clamp(hold, 0f, 0.45f);
            float move = 0.5f - hold;
            if (u < hold) return 0f;
            if (u < 0.5f) return Smooth((u - hold) / move);
            if (u < 0.5f + hold) return 1f;
            return 1f - Smooth((u - 0.5f - hold) / move);
        }

        /// <summary>Pose the part for round time <paramref name="t"/>.</summary>
        public void Evaluate(float t)
        {
            var d = Data;
            float period = Mathf.Abs(d.period) < 0.2f ? 0.2f : Mathf.Abs(d.period);
            float dirSign = d.period < 0f ? -1f : 1f;
            float u = Mathf.Repeat(t / period + d.phase, 1f);
            Threat = 1f;
            switch (d.kind)
            {
                case MechKinds.Door:
                    Body.localPosition = d.Travel * PingPong(u, 0.32f);
                    break;
                case MechKinds.Platform:
                    Body.localPosition = d.Travel * PingPong(u, 0.08f);
                    break;
                case MechKinds.Elevator:
                    Body.localPosition = d.Travel * PingPong(u, 0.22f);
                    break;
                case MechKinds.Piston:
                {
                    // Slam out fast, hold, retract slowly, rest.
                    float s;
                    if (u < 0.08f) s = Mathf.Pow(u / 0.08f, 2f);
                    else if (u < 0.25f) s = 1f;
                    else if (u < 0.6f) s = 1f - Smooth((u - 0.25f) / 0.35f);
                    else s = 0f;
                    Body.localPosition = d.Travel * s;
                    Threat = u < 0.1f ? 1f : 0f; // only hurts while slamming
                    break;
                }
                case MechKinds.Spinner:
                    Body.localRotation = Quaternion.AngleAxis(u * 360f * dirSign, AxisVector(d.axis));
                    break;
                case MechKinds.Pendulum:
                    Body.localRotation = Quaternion.AngleAxis(d.angle * Mathf.Sin(u * Mathf.PI * 2f), AxisVector(d.axis));
                    break;
                case MechKinds.Saw:
                    Body.localPosition = d.Travel * PingPong(u, 0.04f);
                    if (spin != null) spin.localRotation = Quaternion.FromToRotation(Vector3.up, AxisVector(d.axis)) * Quaternion.AngleAxis(t * 900f * dirSign, Vector3.up);
                    break;
                case MechKinds.Spikes:
                {
                    // Hidden, telegraph, stab, hold, retract.
                    float s;
                    if (u < 0.45f) s = 0f;
                    else if (u < 0.6f) s = 0.08f * Mathf.Abs(Mathf.Sin((u - 0.45f) * 120f)); // rattling warning
                    else if (u < 0.64f) s = (u - 0.6f) / 0.04f;
                    else if (u < 0.85f) s = 1f;
                    else s = 1f - (u - 0.85f) / 0.15f;
                    Body.localPosition = new Vector3(0f, -0.9f + 0.9f * Mathf.Clamp01(s), 0f);
                    Threat = s > 0.45f ? 1f : 0f;
                    if (warnMat != null)
                    {
                        float warn = u >= 0.45f && u < 0.64f ? 0.25f + 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 30f)) : s > 0.5f ? 0.35f : 0f;
                        warnMat.color = new Color(warnBase.r, warnBase.g, warnBase.b, warn);
                    }
                    break;
                }
            }
        }

        /// <summary>Advance to round time t (called by the map once per frame, before players move).</summary>
        public void Drive(float t, float dt)
        {
            prevBody = hasPose ? curBody : Body.localToWorldMatrix;
            Evaluate(t);
            curBody = Body.localToWorldMatrix;
            hasPose = true;
            var bp = Body.position;
            if (dt > 0f) bodyVelocity = (bp - lastBodyPos) / dt;
            lastBodyPos = bp;
        }

        /// <summary>How far a point attached to the body moved this frame (for carrying riders).</summary>
        public Vector3 Carry(Vector3 point)
        {
            if (!hasPose) return Vector3.zero;
            var local = prevBody.inverse.MultiplyPoint3x4(point);
            return curBody.MultiplyPoint3x4(local) - point;
        }

        // ------------------------------------------------------------------ contact

        bool Touches(Hurt h, Vector3 p, float r)
        {
            if (h.T == null) return false;
            var local = h.T.InverseTransformPoint(p);
            switch (h.Shape)
            {
                case HurtShape.Sphere:
                    return local.sqrMagnitude < (h.Size.x + r) * (h.Size.x + r);
                case HurtShape.Disc:
                {
                    if (Mathf.Abs(local.y) > h.Size.y * 0.5f + r) return false;
                    float radial = new Vector2(local.x, local.z).magnitude;
                    return radial < h.Size.x + r * 0.5f;
                }
                default:
                {
                    var half = h.Size * 0.5f;
                    var c = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
                    return (local - c).sqrMagnitude < r * r;
                }
            }
        }

        /// <summary>True if the point is inside (or within margin of) a deadly part of this mechanism.</summary>
        public bool IsDangerAt(Vector3 p, float margin)
        {
            if (!Data.deadly) return false;
            foreach (var h in hurts) if (Touches(h, p, margin)) return true;
            return false;
        }

        public static bool AnyDangerAt(Vector3 p, float margin)
        {
            for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].IsDangerAt(p, margin)) return true;
            return false;
        }

        /// <summary>Host: hurt players touching deadly parts.</summary>
        public void HurtPlayers(float dt)
        {
            if (!Data.deadly || hurts.Count == 0 || Threat <= 0f) return;
            foreach (var p in Player.All)
            {
                if (p == null || !p.IsAlive || p.Frozen) continue;
                if (hitCooldown.TryGetValue(p, out float until) && Time.time < until) continue;
                Vector3 c = p.Center;
                float r = p.Radius;
                bool hit = false;
                foreach (var h in hurts) if (Touches(h, c, r)) { hit = true; break; }
                if (!hit) continue;
                hitCooldown[p] = Time.time + 0.5f;
                Vector3 away = c - (hurts[0].T != null ? hurts[0].T.position : transform.position);
                if (Data.kind == MechKinds.Pendulum || Data.kind == MechKinds.Saw || Data.kind == MechKinds.Piston)
                    away = Vector3.Lerp(away.normalized, bodyVelocity.normalized, bodyVelocity.sqrMagnitude > 1f ? 0.6f : 0f);
                if (Data.kind == MechKinds.Spikes) away = Vector3.up;
                if (away.sqrMagnitude < 0.0001f) away = Vector3.up;
                p.TakeDamage(new DamageInfo
                {
                    Amount = Mathf.Max(1f, Data.damage),
                    Type = DamageType.Trap,
                    Cause = MechKinds.Cause(Data.kind),
                    Point = c,
                    Direction = away.normalized + Vector3.up * 0.35f,
                    Knockback = Data.kind == MechKinds.Spikes ? 10f : 18f,
                    Blockable = true,
                });
            }
        }

        static readonly List<Player> pushed = new List<Player>();

        /// <summary>Push players out of solid parts that moved into them (doors closing, pistons).</summary>
        public void Depenetrate()
        {
            if (solids.Count == 0) return;
            foreach (var p in Player.All)
            {
                if (p == null || !p.IsAlive || p.RidingMech == this) continue;
                var cc = p.Controller;
                if (cc == null || !cc.enabled) continue;
                var pb = cc.bounds;
                foreach (var col in solids)
                {
                    if (col == null || !col.enabled || !col.bounds.Intersects(pb)) continue;
                    var ct = col.transform;
                    if (Physics.ComputePenetration(col, ct.position, ct.rotation, cc, p.transform.position, p.transform.rotation, out var dir, out float dist))
                    {
                        if (dist > 2f) continue; // deep inside: let the controller sort itself out
                        p.transform.position -= dir * (dist + 0.01f);
                        Physics.SyncTransforms();
                        pb = cc.bounds;
                    }
                }
            }
        }
    }
}
