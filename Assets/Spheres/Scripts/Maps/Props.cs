using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>A loose physics object placed in a map: crates, boxes, explosive barrels, beach balls.</summary>
    [Serializable]
    public class PropData
    {
        /// <summary>One of <see cref="PropKinds"/> (or a mod-registered kind).</summary>
        public string kind = PropKinds.Crate;
        /// <summary>Center of the prop's bottom, in world units (cells).</summary>
        public float x, y, z;
        /// <summary>Yaw in degrees.</summary>
        public float yaw;

        public PropData() { }
        public PropData(string kind, float x, float y, float z, float yaw = 0f)
        {
            this.kind = kind; this.x = x; this.y = y; this.z = z; this.yaw = yaw;
        }
        public PropData Clone() => (PropData)MemberwiseClone();
    }

    /// <summary>How a prop kind looks and behaves. Register new ones with <see cref="PropKinds.Register"/>.</summary>
    public class PropKind
    {
        public string Id, Name;
        public Color Color;
        public Vector3 Size = Vector3.one;
        public bool Round, Cylinder;
        public float Mass = 2f, Health = 60f, Bounciness;
        /// <summary>Explodes when broken (0 = no).</summary>
        public float ExplodeRadius, ExplodeDamage;
        /// <summary>How hard bullets shove it.</summary>
        public float BulletPush = 1f;
        /// <summary>Players kick it hard when they run into it (balls).</summary>
        public bool Kickable;
        /// <summary>Not offered in the map editor (mode-only props).</summary>
        public bool HideInEditor;
        /// <summary>Glows (bloom).</summary>
        public bool Glowing;
        /// <summary>Health 0 = can't be destroyed.</summary>
        public bool Indestructible => Health <= 0f;
    }

    public static class PropKinds
    {
        public const string Crate = "crate";
        public const string Box = "box";
        public const string Barrel = "barrel";
        public const string Ball = "ball";
        public const string Heavy = "heavy";
        /// <summary>Sphere Ball's giant ball (indestructible).</summary>
        public const string Soccer = "soccer";
        /// <summary>Hold the Orb's glowing orb (indestructible).</summary>
        public const string Orb = "orb";

        static readonly Dictionary<string, PropKind> byId = new Dictionary<string, PropKind>();
        static readonly List<PropKind> ordered = new List<PropKind>();
        public static IReadOnlyList<PropKind> All => ordered;

        static PropKinds()
        {
            Register(new PropKind { Id = Crate, Name = "Wooden Crate", Color = new Color(0.68f, 0.47f, 0.26f), Size = Vector3.one * 1f, Mass = 3f, Health = 70f });
            Register(new PropKind { Id = Box, Name = "Cardboard Box", Color = new Color(0.78f, 0.64f, 0.44f), Size = new Vector3(0.8f, 0.7f, 0.8f), Mass = 0.6f, Health = 18f, BulletPush = 2f });
            Register(new PropKind { Id = Barrel, Name = "Explosive Barrel", Color = new Color(0.86f, 0.2f, 0.16f), Size = new Vector3(0.8f, 1.1f, 0.8f), Cylinder = true, Mass = 2f, Health = 35f, ExplodeRadius = 4.5f, ExplodeDamage = 50f });
            Register(new PropKind { Id = Ball, Name = "Beach Ball", Color = new Color(0.3f, 0.75f, 1f), Size = Vector3.one * 1.3f, Round = true, Mass = 0.4f, Health = 45f, Bounciness = 0.85f, BulletPush = 2.5f, Kickable = true });
            Register(new PropKind { Id = Soccer, Name = "Sphere Ball", Color = new Color(0.95f, 0.95f, 0.98f), Size = Vector3.one * 1.8f, Round = true, Mass = 0.7f, Health = 0f, Bounciness = 0.6f, BulletPush = 3.2f, Kickable = true, HideInEditor = true });
            Register(new PropKind { Id = Orb, Name = "Orb", Color = new Color(1f, 0.85f, 0.3f), Size = Vector3.one * 0.9f, Round = true, Mass = 0.5f, Health = 0f, Bounciness = 0.4f, BulletPush = 1.5f, Glowing = true, HideInEditor = true });
            Register(new PropKind { Id = Heavy, Name = "Steel Crate", Color = new Color(0.45f, 0.5f, 0.58f), Size = Vector3.one * 1.4f, Mass = 12f, Health = 400f, BulletPush = 0.4f });
        }

        public static void Register(PropKind kind)
        {
            if (kind == null || string.IsNullOrEmpty(kind.Id)) return;
            if (byId.TryGetValue(kind.Id, out var old)) ordered[ordered.IndexOf(old)] = kind;
            else ordered.Add(kind);
            byId[kind.Id] = kind;
        }

        public static PropKind Get(string id) => id != null && byId.TryGetValue(id, out var k) ? k : byId[Crate];
    }

    /// <summary>
    /// Runtime prop. The host simulates physics and streams poses to clients (see NetSession), where
    /// props are kinematic and smoothly follow.
    /// </summary>
    public class Prop : MonoBehaviour
    {
        public static readonly List<Prop> All = new List<Prop>();

        public PropData Data;
        public PropKind Kind;
        public int Index;
        public float Hp;
        public bool Removed { get; private set; }
        public Rigidbody Body { get; private set; }
        /// <summary>Who last shot or blew it up (credited for barrel kills).</summary>
        public Player LastAttacker;
        /// <summary>If set, called instead of breaking when the prop falls out of the arena.</summary>
        public System.Action<Prop> OnFellOut;

        // Network state
        internal Vector3 SentPos;
        internal Quaternion SentRot;
        internal bool SentRemoved;
        Vector3 netPos;
        Quaternion netRot;
        bool hasNet, puppet;
        float spawnTime;
        static PhysicMaterial bouncyMat, woodMat;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public static Prop Create(PropData data, int index, Transform parent, bool puppet)
        {
            var kind = PropKinds.Get(data.kind);
            var go = new GameObject("Prop_" + kind.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(data.x, data.y + kind.Size.y * 0.5f + 0.01f, data.z);
            go.transform.rotation = Quaternion.Euler(0f, data.yaw, 0f);
            var prop = go.AddComponent<Prop>();
            prop.Data = data;
            prop.Kind = kind;
            prop.Index = index;
            prop.Hp = kind.Health;
            prop.puppet = puppet;
            prop.spawnTime = Time.time;
            prop.BuildVisual();
            prop.SentPos = go.transform.position;
            prop.SentRot = go.transform.rotation;
            return prop;
        }

        void BuildVisual()
        {
            var k = Kind;
            var mat = Looks.Block(k.Color);
            Collider col;
            if (k.Round)
            {
                var ball = Prim.Create(Prim.SphereMesh, "Ball", transform, Vector3.zero, k.Size, mat);
                ball.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // Stripes (beach ball) or panels (sphere ball).
                var white = Looks.Block(k.Id == PropKinds.Soccer ? new Color(0.15f, 0.16f, 0.2f) : Color.white);
                Prim.Create(Prim.SphereMesh, "Stripe", transform, Vector3.zero, new Vector3(k.Size.x * 1.01f, k.Size.y * 0.32f, k.Size.z * 1.01f), white);
                Prim.Create(Prim.SphereMesh, "Stripe2", transform, Vector3.zero, new Vector3(k.Size.x * 0.32f, k.Size.y * 1.01f, k.Size.z * 1.01f), Looks.Block(new Color(1f, 0.85f, 0.25f)));
                if (k.Glowing && Mats.GlowAvailable)
                {
                    var halo = Prim.Create(Prim.SphereMesh, "Halo", transform, Vector3.zero, k.Size * 1.6f, Mats.Glow(new Color(k.Color.r, k.Color.g, k.Color.b, 0.45f), 2.6f));
                    halo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.radius = k.Size.x * 0.5f;
                col = sc;
            }
            else if (k.Cylinder)
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(cyl.GetComponent<Collider>());
                cyl.name = "Barrel";
                cyl.transform.SetParent(transform, false);
                cyl.transform.localScale = new Vector3(k.Size.x, k.Size.y * 0.5f, k.Size.z);
                cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
                // Hazard bands and a glowing warning stripe.
                var band = Looks.Block(new Color(0.15f, 0.15f, 0.17f));
                foreach (float yy in new[] { -0.3f, 0.3f })
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(b.GetComponent<Collider>());
                    b.transform.SetParent(transform, false);
                    b.transform.localPosition = new Vector3(0f, yy * k.Size.y, 0f);
                    b.transform.localScale = new Vector3(k.Size.x * 1.04f, 0.05f, k.Size.z * 1.04f);
                    b.GetComponent<MeshRenderer>().sharedMaterial = band;
                }
                if (Mats.GlowAvailable)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(g.GetComponent<Collider>());
                    g.transform.SetParent(transform, false);
                    g.transform.localScale = new Vector3(k.Size.x * 1.02f, 0.06f, k.Size.z * 1.02f);
                    var r = g.GetComponent<MeshRenderer>();
                    r.sharedMaterial = Mats.Glow(new Color(1f, 0.75f, 0.2f, 0.55f), 2f, 0f);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var cc = gameObject.AddComponent<CapsuleCollider>();
                cc.radius = k.Size.x * 0.5f;
                cc.height = k.Size.y;
                col = cc;
            }
            else
            {
                Prim.Create(Looks.TiledBox(k.Size.x, k.Size.y, k.Size.z), "Box", transform, Vector3.zero, Vector3.one, mat);
                if (k.Id == PropKinds.Crate || k.Id == PropKinds.Heavy)
                {
                    // Cross braces on two faces.
                    var brace = Looks.Block(k.Color * 0.7f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var br = Prim.Cube("Brace", transform, new Vector3(0f, 0f, s * k.Size.z * 0.505f), new Vector3(k.Size.x * 1.25f, 0.12f, 0.02f), brace);
                        br.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                        var br2 = Prim.Cube("Brace", transform, new Vector3(s * k.Size.x * 0.505f, 0f, 0f), new Vector3(0.02f, 0.12f, k.Size.z * 1.25f), brace);
                        br2.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                    }
                }
                else if (k.Id == PropKinds.Box)
                {
                    Prim.Cube("Tape", transform, new Vector3(0f, k.Size.y * 0.5f, 0f), new Vector3(0.18f, 0.02f, k.Size.z * 1.01f), Looks.Block(new Color(0.9f, 0.85f, 0.7f)));
                }
                var bc = gameObject.AddComponent<BoxCollider>();
                bc.size = k.Size;
                col = bc;
            }

            if (k.Bounciness > 0f)
            {
                if (bouncyMat == null) bouncyMat = new PhysicMaterial("PropBouncy") { bounciness = 0.85f, bounceCombine = PhysicMaterialCombine.Maximum, dynamicFriction = 0.3f, staticFriction = 0.3f };
                col.sharedMaterial = bouncyMat;
            }
            else
            {
                if (woodMat == null) woodMat = new PhysicMaterial("PropWood") { bounciness = 0.1f, dynamicFriction = 0.55f, staticFriction = 0.6f };
                col.sharedMaterial = woodMat;
            }

            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = k.Mass;
            Body.drag = 0.05f;
            Body.angularDrag = 0.2f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (puppet)
            {
                Body.isKinematic = true;
                Body.interpolation = RigidbodyInterpolation.None;
            }
        }

        bool Authority => !puppet;

        /// <summary>Host: bullets, explosions and effects call this.</summary>
        public void Damage(float amount, Player source, Vector3 direction, Vector3 point, float force)
        {
            if (Removed || !Authority) return;
            if (source != null) LastAttacker = source;
            if (Body != null && !Body.isKinematic && direction.sqrMagnitude > 0.0001f && force > 0f)
                Body.AddForceAtPosition(direction.normalized * force * Kind.BulletPush, point, ForceMode.Impulse);
            if (amount <= 0f || Kind.Indestructible) return;
            Hp -= amount;
            if (Hp <= 0f) Break();
        }

        /// <summary>Host: destroy the prop with effects (and an explosion for barrels).</summary>
        public void Break()
        {
            if (Removed) return;
            Removed = true;
            var pos = transform.position;
            var k = Kind;
            Fx.Burst(pos, k.Color, Mathf.Clamp(Mathf.RoundToInt(k.Size.sqrMagnitude * 6f), 6, 26), 7f, 0.2f, 0.9f);
            if (!k.Round) Fx.Burst(pos, k.Color * 0.7f, 6, 4f, 0.32f, 1.2f);
            Sfx.PlayAt(k.Round ? "bounce" : "hit", pos, 0.7f);
            if (k.Round) Fx.Pulse(pos, new Color(1f, 1f, 1f, 0.5f), 0.5f, k.Size.x * 2.5f, 0.2f);
            gameObject.SetActive(false);
            if (k.ExplodeRadius > 0f)
            {
                // Short fuse so chain reactions ripple instead of happening all at once.
                var owner = LastAttacker;
                var map = GetComponentInParent<MapRuntime>();
                if (map != null) map.Delay(0.12f, () => Explosion.Create(pos, k.ExplodeRadius, k.ExplodeDamage, owner, 22f, new Color(1f, 0.55f, 0.15f), true, "barrel"));
                else Explosion.Create(pos, k.ExplodeRadius, k.ExplodeDamage, owner, 22f, new Color(1f, 0.55f, 0.15f), true, "barrel");
            }
        }

        /// <summary>Host: move instantly and stop (ball reset after a goal, orb dropped).</summary>
        public void Teleport(Vector3 pos)
        {
            if (Body != null && !Body.isKinematic)
            {
                Body.velocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            transform.position = pos;
            if (Body != null) Body.position = pos;
        }

        /// <summary>Host: a player is carrying this prop (it follows them; physics off).</summary>
        public void SetCarried(bool carried)
        {
            if (Body == null || puppet) return;
            Body.isKinematic = carried;
            if (carried) Body.interpolation = RigidbodyInterpolation.None;
            else Body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>Client: the host says this prop is gone (effects arrive separately).</summary>
        public void NetRemove()
        {
            if (Removed) return;
            Removed = true;
            gameObject.SetActive(false);
        }

        public void NetTarget(Vector3 pos, Quaternion rot)
        {
            netPos = pos;
            netRot = rot;
            if (!hasNet || (pos - transform.position).sqrMagnitude > 25f)
            {
                transform.SetPositionAndRotation(pos, rot);
                hasNet = true;
            }
        }

        void Update()
        {
            if (puppet && hasNet)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 18f);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPos, k), Quaternion.Slerp(transform.rotation, netRot, k));
            }
        }

        void FixedUpdate()
        {
            if (!Authority || Removed) return;
            var map = GameManager.ActiveMap;
            if (map != null && transform.position.y < map.FullBounds.min.y - 6f)
            {
                if (OnFellOut != null) OnFellOut(this);
                else Break();
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (!Authority || Removed || Time.time - spawnTime < 1f) return;
            float v = c.relativeVelocity.magnitude;
            if (v > 4f && Kind.Round) Sfx.PlayAt("bounce", transform.position, Mathf.Clamp01(v / 25f) * 0.4f);
            // Heavy impacts crack fragile props (boxes dropped from a height, barrels flung into walls).
            if (v > 14f && c.rigidbody == null && !Kind.Indestructible) Damage((v - 14f) * 4f, LastAttacker, Vector3.zero, transform.position, 0f);
        }
    }
}
