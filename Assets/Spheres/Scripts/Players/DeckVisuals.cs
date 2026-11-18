using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Your deck shows on your fighter. Every stat a card raises or lowers and every card effect has its own small
    /// piece of gear (DeckVisuals.Parts.cs has the catalog): extra barrels for multi-shot, a scope for range, a drum
    /// mag for ammo, armor plates for less damage taken and cracks for more, springs for jump height, a halo per extra
    /// life, a silencer for hush rounds, a tesla antenna for arc coil, and so on. Curses add purple wisps and cards
    /// with no known look (mods) add a small charm in the card's color. Stacking a card scales or repeats its piece
    /// up to a cap, and the whole fighter is capped at <see cref="MaxPieces"/> pieces so huge decks stay cheap.
    /// Built from the cards alone (no randomness), so it matches on every machine, and rebuilt only when the deck changes.
    /// </summary>
    public static partial class DeckVisuals
    {
        /// <summary>Most renderers deck gear may add to one fighter (the last piece built may overshoot by up to 3 parts).</summary>
        public const int MaxPieces = 48;

        struct BuiltState { public int Sig, Holders; }

        static readonly Dictionary<Player, BuiltState> built = new Dictionary<Player, BuiltState>();
        static readonly List<Player> deadKeys = new List<Player>();
        static float timer;

        /// <summary>Called every frame by the game: rebuilds gear for players whose cards changed.</summary>
        public static void Tick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            timer = 0.25f;
            deadKeys.Clear();
            foreach (var k in built.Keys) if (k == null) deadKeys.Add(k);
            for (int i = 0; i < deadKeys.Count; i++) built.Remove(deadKeys[i]);
            bool on = Options.Current.deckVisuals;
            foreach (var p in Player.All)
            {
                if (p == null || p.Model == null) continue;
                int sig = on ? Signature(p) : 0;
                // Rebuild when the deck changed, or when some of our gear was wiped (e.g. the look editor rebuilt the feet).
                if (built.TryGetValue(p, out var old) && old.Sig == sig && CountHolders(p.Model) == old.Holders) continue;
                built[p] = new BuiltState { Sig = sig, Holders = Rebuild(p, on) };
            }
        }

        static int Signature(Player p)
        {
            unchecked
            {
                int h = 17 + p.Cards.Count;
                foreach (var c in p.Cards) h = h * 31 + (c != null && c.Id != null ? c.Id.GetHashCode() : 0);
                return h == 0 ? 1 : h;
            }
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = Find(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>How many gear holders hang under a model (holders are never nested).</summary>
        static int CountHolders(Transform t)
        {
            int n = 0;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == HolderName) n++;
                else n += CountHolders(c);
            }
            return n;
        }

        const string HolderName = "DeckGear";

        static void Clear(Transform root)
        {
            if (root == null) return;
            var olds = new List<GameObject>();
            void Collect(Transform t)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    if (c.name == HolderName) olds.Add(c.gameObject);
                    else Collect(c);
                }
            }
            Collect(root);
            foreach (var g in olds) Object.Destroy(g);
        }

        static int holders;

        static Transform Holder(Transform parent)
        {
            var go = new GameObject(HolderName);
            go.transform.SetParent(parent, false);
            holders++;
            return go.transform;
        }

        // ------------------------------------------------------------------ attach points (created on demand)

        static Player owner;
        static Transform gunT, headT, rigT, footLT, footRT;
        static Transform body, gun, head, footL, footR;

        /// <summary>Body holder, in rig space (body ball centered at <see cref="BC"/>, radius 0.5, facing +Z).</summary>
        static Transform B => body;

        /// <summary>Gun holder, in gun space (barrel along +Z, ending at z 0.41, radius 0.1).</summary>
        static Transform G
        {
            get
            {
                if (gun == null) gun = gunT != null ? Holder(gunT) : Shape.Pivot(body, new Vector3(0.55f, 0.7f, 0.3f), Vector3.zero, "GearGun");
                return gun;
            }
        }

        /// <summary>Head holder, in head space (body ball centered at (0,-0.25,0), face toward +Z).</summary>
        static Transform H
        {
            get
            {
                if (head == null) head = headT != null ? Holder(headT) : Shape.Pivot(body, new Vector3(0f, 0.95f, 0f), Vector3.zero, "GearHead");
                return head;
            }
        }

        /// <summary>Foot holder (side -1 left, +1 right), in foot space (foot about 0.27 x 0.17 x 0.37, toe toward +Z).</summary>
        static Transform Foot(int side)
        {
            if (side < 0)
            {
                if (footL == null) footL = footLT != null ? Holder(footLT) : Shape.Pivot(body, FighterModel.FootRestL, Vector3.zero, "GearFootL");
                return footL;
            }
            if (footR == null) footR = footRT != null ? Holder(footRT) : Shape.Pivot(body, FighterModel.FootRestR, Vector3.zero, "GearFootR");
            return footR;
        }

        // ------------------------------------------------------------------ piece helpers (all count against the budget)

        static int left;

        static readonly Color Steel = new Color(0.62f, 0.65f, 0.72f), DarkSteel = new Color(0.25f, 0.26f, 0.3f);
        static readonly Quaternion I = Quaternion.identity;
        static readonly Vector3 BC = new Vector3(0f, 0.7f, 0f);

        static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = Prim.Create(mesh, "Part", parent, pos, scale, mat);
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            left--;
            return go;
        }

        static GameObject Box(Transform p, Vector3 pos, Vector3 scale, Material m, Quaternion r) => Part(p, Prim.CubeMesh, pos, scale, m, r);
        static GameObject Box(Transform p, Vector3 pos, Vector3 scale, Material m) => Part(p, Prim.CubeMesh, pos, scale, m);
        static GameObject Ball(Transform p, Vector3 pos, Vector3 scale, Material m, Quaternion r) => Part(p, Prim.SphereMesh, pos, scale, m, r);
        static GameObject Ball(Transform p, Vector3 pos, Vector3 scale, Material m) => Part(p, Prim.SphereMesh, pos, scale, m);
        static GameObject Ball(Transform p, Vector3 pos, float d, Material m) => Part(p, Prim.SphereMesh, pos, Vector3.one * d, m);

        /// <summary>Cylinder standing on its base center along its local +Y.</summary>
        static GameObject Cyl(Transform p, Vector3 basePos, float d, float h, Material m, Quaternion r) =>
            Part(p, Shape.TubeMesh(1f), basePos, new Vector3(d, h, d), m, r);

        /// <summary>Cone along local +Y; top &gt; 1 flares out (a bell), 0 is a point.</summary>
        static GameObject Cone(Transform p, Vector3 basePos, float d, float h, Material m, Quaternion r, float top = 0f) =>
            Part(p, Shape.TubeMesh(top), basePos, new Vector3(d, h, d), m, r);

        /// <summary>Torus in the local XZ plane; a sweep under 360 makes an arc centered on +Z.</summary>
        static GameObject Ring(Transform p, Vector3 pos, float d, float thick, Material m, Quaternion r, float sweep = 360f) =>
            Part(p, Shape.TorusMesh(Mathf.Clamp(thick / Mathf.Max(0.01f, d), 0.01f, 0.45f), sweep), pos, Vector3.one * d, m, r);

        /// <summary>Four-sided spike with its base at pos and its tip along local +Y.</summary>
        static GameObject Spike(Transform p, Vector3 basePos, Vector3 scale, Material m, Quaternion r) => Part(p, Mechanism.SpikeMesh, basePos, scale, m, r);

        static GameObject Heart(Transform p, Vector3 pos, float size, float depth, Material m, Quaternion r) =>
            Part(p, Shape.HeartMesh, pos, new Vector3(size, size, depth), m, r);

        static GameObject Star(Transform p, Vector3 pos, float size, float depth, Material m, Quaternion r) =>
            Part(p, Shape.StarMesh(5), pos, new Vector3(size, size, depth), m, r);

        /// <summary>Thin round stick from a to b.</summary>
        static GameObject Stick(Transform p, Vector3 a, Vector3 b, float d, Material m)
        {
            var dir = b - a;
            var rot = dir.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(Vector3.up, dir.normalized) : I;
            return Cyl(p, a, d, Mathf.Max(0.001f, dir.magnitude), m, rot);
        }

        /// <summary>Unit direction on a sphere: lon 0 is the front (+Z), 90 the right (+X), 180 the back.</summary>
        static Vector3 Sph(float lat, float lon)
        {
            float la = lat * Mathf.Deg2Rad, lo = lon * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(la) * Mathf.Sin(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Cos(lo));
        }

        /// <summary>A point on (or just off) the body ball, rig space.</summary>
        static Vector3 BodyPt(float lat, float lon, float r = 0.5f) => BC + Sph(lat, lon) * r;

        /// <summary>Rotation pointing local +Y out of the body (cylinders, spikes, rings lying on the surface).</summary>
        static Quaternion Out(float lat, float lon) => Quaternion.FromToRotation(Vector3.up, Sph(lat, lon));

        /// <summary>Rotation pointing local +Z out of the body, up kept up (hearts, plates, flat signs).</summary>
        static Quaternion Face(float lat, float lon) => Quaternion.LookRotation(Sph(lat, lon), Vector3.up);

        static Material Glow(Color c, float a = 0.6f, float i = 2.2f) => Mats.GlowAvailable ? Mats.Glow(new Color(c.r, c.g, c.b, a), i) : Mats.Solid(c);
        static Material Solid(float r, float g, float b) => Mats.Solid(new Color(r, g, b));
        static Material Shiny(float r, float g, float b, float gloss = 0.7f, float metal = 0f) => Shape.Shiny(new Color(r, g, b), gloss, metal);

        static readonly Dictionary<int, Transform> pivots = new Dictionary<int, Transform>();

        /// <summary>A shared moving pivot (orbit, spin, bob) for one feature, created the first time a copy needs it.</summary>
        static Transform Piv(int key, Transform parent, Vector3 pos, float spin = 0f, Vector3? axis = null, float bob = -1f, float bobAmp = 0.05f)
        {
            if (pivots.TryGetValue(key, out var t) && t != null) return t;
            t = Shape.Pivot(parent, pos, Vector3.zero, "Gear");
            if (spin != 0f)
            {
                var s = t.gameObject.AddComponent<DeckGearSpin>();
                s.Speed = spin;
                s.Axis = axis ?? Vector3.up;
            }
            if (bob >= 0f)
            {
                var b = t.gameObject.AddComponent<DeckGearBob>();
                b.Phase = bob;
                b.Amp = bobAmp;
            }
            pivots[key] = t;
            return t;
        }

        // ------------------------------------------------------------------ rebuild

        struct Feature { public int Kind, Level, Copies; }

        static readonly List<Feature> features = new List<Feature>();
        static readonly System.Comparison<Feature> ByLevel = (a, b) => a.Level != b.Level ? b.Level.CompareTo(a.Level) : a.Kind.CompareTo(b.Kind);

        /// <returns>How many gear holders were created.</returns>
        static int Rebuild(Player p, bool on)
        {
            Clear(p.Model);
            holders = 0;
            if (!on || p.Cards.Count == 0) return 0;

            owner = p;
            gunT = Find(p.Model, "Gun");
            headT = Find(p.Model, "Head");
            rigT = Find(p.Model, "Rig") ?? p.Model;
            footLT = Find(p.Model, "FootL");
            footRT = Find(p.Model, "FootR");
            gun = head = footL = footR = null;
            pivots.Clear();
            body = Holder(rigT);
            left = MaxPieces;

            Tally(p);
            BuildAura(p);

            // Most-stacked first so the core of a build always shows; then every feature gets its base piece
            // before any feature gets a stack copy, so a 50-card deck still shows every kind of card it can afford.
            features.Clear();
            for (int k = 0; k < KindCount; k++)
            {
                int lv = LevelOf(k);
                if (lv > 0) features.Add(new Feature { Kind = k, Level = lv });
            }
            features.Sort(ByLevel);
            for (int i = 0; i < features.Count && left > 0; i++)
            {
                var f = features[i];
                f.Copies = Build(f.Kind, f.Level, 0);
                features[i] = f;
            }
            for (int copy = 1; copy < 6 && left > 0; copy++)
                for (int i = 0; i < features.Count && left > 0; i++)
                    if (copy < features[i].Copies) Build(features[i].Kind, features[i].Level, copy);

            owner = null;
            return holders;
        }

        /// <summary>Aura: grows with deck size, colored by the strongest keyword; gold for legendaries.</summary>
        static void BuildAura(Player p)
        {
            int n = p.Cards.Count;
            if (n < 6) return;
            var counts = Combos.Counts(p.Cards);
            int best = 0;
            for (int k = 1; k < counts.Length; k++) if (counts[k] > counts[best]) best = k;
            var col = KeywordColor((Keyword)best);
            int legends = 0;
            foreach (var c in p.Cards) if (c != null && c.Rarity == Rarity.Legendary) legends++;
            if (legends >= 2) col = new Color(1f, 0.82f, 0.3f);
            float size = Mathf.Clamp(1.5f + (n - 6) * 0.06f, 1.5f, 2.6f);
            var aura = Ball(B, BC, size, Glow(col, Mathf.Clamp(0.12f + (n - 6) * 0.01f, 0.12f, 0.3f), 1.4f));
            aura.AddComponent<DeckGearPulse>();
        }

        public static Color KeywordColor(Keyword k)
        {
            switch (k)
            {
                case Keyword.Explosive: return new Color(1f, 0.5f, 0.15f);
                case Keyword.Frost: return new Color(0.6f, 0.9f, 1f);
                case Keyword.Venom: return new Color(0.45f, 1f, 0.3f);
                case Keyword.Vampire: return new Color(0.9f, 0.15f, 0.25f);
                case Keyword.Shield: return new Color(0.5f, 0.85f, 1f);
                case Keyword.Tank: return new Color(0.75f, 0.8f, 0.9f);
                case Keyword.Mobility: return new Color(0.4f, 0.8f, 1f);
                case Keyword.Homing: return new Color(1f, 0.4f, 0.4f);
                case Keyword.Chaos: return new Color(0.85f, 0.45f, 1f);
                case Keyword.Bouncy: return new Color(1f, 0.45f, 0.85f);
                default: return new Color(1f, 0.9f, 0.6f);
            }
        }
    }

    /// <summary>Spins deck gear (barrel clusters while firing, radar, orbiting plates).</summary>
    public class DeckGearSpin : MonoBehaviour
    {
        public Player Owner;
        public float Speed = 360f;
        public Vector3 Axis = Vector3.forward;
        float spin;

        void Update()
        {
            float s = Speed;
            if (Owner != null) s = Owner.Weapon != null && Owner.Weapon.Cooldown > 0f ? 900f : 60f;
            spin = Mathf.MoveTowards(spin, s, 2000f * Time.deltaTime);
            transform.Rotate(Axis, spin * Time.deltaTime, Space.Self);
        }
    }

    public class DeckGearBob : MonoBehaviour
    {
        public float Phase, Amp = 0.12f, Speed = 2f;
        Vector3 basePos;
        void Start() => basePos = transform.localPosition;
        void Update() => transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(Time.time * Speed + Phase) * Amp, 0f);
    }

    public class DeckGearPulse : MonoBehaviour
    {
        public float Amount = 0.05f, Speed = 3f;
        Vector3 baseScale;
        void Start() => baseScale = transform.localScale;
        void Update() => transform.localScale = baseScale * (1f + Amount * Mathf.Sin(Time.time * Speed));
    }

    /// <summary>Rocks a piece back and forth (wobbly barrels, swinging weights).</summary>
    public class DeckGearWobble : MonoBehaviour
    {
        public float Angle = 12f, Speed = 5f, Phase;
        public Vector3 Axis = Vector3.right;
        Quaternion baseRot;
        void Start() => baseRot = transform.localRotation;
        void Update() => transform.localRotation = baseRot * Quaternion.AngleAxis(Mathf.Sin(Time.time * Speed + Phase) * Angle, Axis);
    }
}
