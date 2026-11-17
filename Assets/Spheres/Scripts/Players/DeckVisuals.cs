using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Your deck shows on your fighter: extra barrels for multi-shot, a scope for long range, a drum mag for ammo,
    /// grenades for explosives, a coil for bounces, a radar for homing, a drill for pierce, orbiting plates for
    /// blocking, armor for tanky builds, thrusters for mobility, spikes for knockback, frost crystals, toxic bubbles,
    /// and an aura that grows with the size of the deck. Built from the cards alone, so it matches on every machine.
    /// </summary>
    public static class DeckVisuals
    {
        static readonly Dictionary<Player, int> built = new Dictionary<Player, int>();
        static float timer;

        /// <summary>Called every frame by the game: rebuilds gear for players whose cards changed.</summary>
        public static void Tick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            timer = 0.25f;
            foreach (var dead in built.Keys.Where(p => p == null).ToList()) built.Remove(dead);
            bool on = Options.Current.deckVisuals;
            foreach (var p in Player.All)
            {
                if (p == null || p.Model == null) continue;
                int sig = on ? Signature(p) : 0;
                if (built.TryGetValue(p, out int old) && old == sig && (sig == 0 || Find(p.Model, "DeckGear") != null)) continue;
                built[p] = sig;
                Rebuild(p, on);
            }
        }

        static int Signature(Player p)
        {
            unchecked
            {
                int h = 17 + p.Cards.Count;
                foreach (var c in p.Cards) h = h * 31 + c.Id.GetHashCode();
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

        static void Clear(Transform root)
        {
            if (root == null) return;
            var olds = new List<GameObject>();
            void Collect(Transform t)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    if (c.name == "DeckGear") olds.Add(c.gameObject);
                    else Collect(c);
                }
            }
            Collect(root);
            foreach (var g in olds) Object.Destroy(g);
        }

        static Transform Holder(Transform parent)
        {
            var go = new GameObject("DeckGear");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static readonly Color Steel = new Color(0.62f, 0.65f, 0.72f), DarkSteel = new Color(0.25f, 0.26f, 0.3f);

        static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = Prim.Create(mesh, "Part", parent, pos, scale, mat);
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Material Glow(Color c, float a = 0.6f, float i = 2.2f) => Mats.GlowAvailable ? Mats.Glow(new Color(c.r, c.g, c.b, a), i) : Mats.Solid(c);

        static void Rebuild(Player p, bool on)
        {
            Clear(p.Model);
            if (!on || p.Cards.Count == 0) return;
            var counts = Combos.Counts(p.Cards);
            int L(Keyword k) => Mathf.Min(5, counts[(int)k]);
            var gun = Find(p.Model, "Gun");
            var head = Find(p.Model, "Head");
            var rig = Find(p.Model, "Rig") ?? p.Model;
            var steel = Mats.Solid(Steel);
            var dark = Mats.Solid(DarkSteel);
            var team = p.Color;

            if (gun != null)
            {
                var g = Holder(gun);
                var spin = new GameObject("Spin").transform;
                spin.SetParent(g, false);
                spin.localPosition = new Vector3(0f, 0f, 0.05f);
                // Multi: extra barrels arranged around the main one (spin while firing if also Rapid).
                int barrels = Mathf.Min(6, p.GetStatInt(Stat.BulletsPerShot) - 1 + L(Keyword.Multi) / 2);
                for (int i = 0; i < barrels; i++)
                {
                    float a = i * Mathf.PI * 2f / Mathf.Max(1, barrels);
                    Part(spin, Prim.CubeMesh, new Vector3(Mathf.Cos(a) * 0.14f, Mathf.Sin(a) * 0.14f, 0.08f), new Vector3(0.07f, 0.07f, 0.62f), dark);
                }
                if (L(Keyword.Rapid) > 0)
                {
                    for (int i = 0; i < 3; i++) Part(g, Prim.CubeMesh, new Vector3(0f, 0f, -0.1f + i * 0.09f), new Vector3(0.27f, 0.27f, 0.03f), steel);
                    spin.gameObject.AddComponent<DeckGearSpin>().Owner = p;
                }
                // Sniper: barrel extension and scope.
                if (L(Keyword.Sniper) > 0)
                {
                    Part(g, Prim.CubeMesh, new Vector3(0f, 0f, 0.48f + 0.06f * L(Keyword.Sniper)), new Vector3(0.09f, 0.09f, 0.3f + 0.12f * L(Keyword.Sniper)), dark);
                    Part(g, Prim.CubeMesh, new Vector3(0f, 0.15f, 0.02f), new Vector3(0.07f, 0.07f, 0.34f), dark);
                    Part(g, Prim.SphereMesh, new Vector3(0f, 0.15f, 0.2f), new Vector3(0.08f, 0.08f, 0.03f), Glow(new Color(1f, 0.3f, 0.3f), 0.9f));
                }
                // Ammo: a drum magazine.
                if (L(Keyword.Ammo) > 0)
                {
                    float s = 0.18f + 0.03f * L(Keyword.Ammo);
                    Part(g, Prim.SphereMesh, new Vector3(0f, -0.16f - s * 0.4f, 0.06f), new Vector3(0.12f, s * 1.6f, s * 1.6f), dark);
                }
                // Explosive: grenades strapped to the side.
                for (int i = 0; i < L(Keyword.Explosive); i++)
                {
                    Part(g, Prim.SphereMesh, new Vector3(-0.14f, -0.04f, -0.12f + i * 0.11f), Vector3.one * 0.1f, Mats.Solid(new Color(0.3f, 0.45f, 0.2f)));
                    Part(g, Prim.CubeMesh, new Vector3(-0.14f, 0.02f, -0.12f + i * 0.11f), new Vector3(0.03f, 0.04f, 0.03f), steel);
                }
                // Bouncy: a spring coil on top.
                for (int i = 0; i < 2 + L(Keyword.Bouncy); i++)
                    Part(g, Prim.CubeMesh, new Vector3((i % 2 == 0 ? 0.04f : -0.04f), 0.12f + i * 0.035f, -0.05f), new Vector3(0.1f, 0.018f, 0.03f), Mats.Solid(new Color(0.95f, 0.45f, 0.85f)));
                // Homing: a little radar dish.
                if (L(Keyword.Homing) > 0)
                {
                    Part(g, Prim.CubeMesh, new Vector3(0.1f, 0.15f, -0.08f), new Vector3(0.02f, 0.12f, 0.02f), steel);
                    var dish = Part(g, Prim.SphereMesh, new Vector3(0.1f, 0.23f, -0.08f), new Vector3(0.16f, 0.05f, 0.16f), steel, Quaternion.Euler(-30f, 0f, 20f));
                    dish.AddComponent<DeckGearSpin>().Speed = 120f;
                }
                // Pierce: a drill bit on the muzzle.
                if (L(Keyword.Pierce) > 0)
                {
                    var drill = Part(g, Mechanism.SpikeMesh, new Vector3(0f, 0f, 0.42f), new Vector3(0.14f, 0.22f + 0.05f * L(Keyword.Pierce), 0.14f), steel, Quaternion.Euler(90f, 0f, 0f));
                    drill.AddComponent<DeckGearSpin>().Axis = Vector3.up;
                }
                // Giant: a glowing orb charging at the tip.
                float size = p.GetStat(Stat.BulletSize);
                if (size > 0.18f || L(Keyword.Giant) > 0)
                    Part(g, Prim.SphereMesh, new Vector3(0f, 0f, 0.46f), Vector3.one * Mathf.Clamp(size * 1.6f, 0.15f, 0.6f), Glow(Color.Lerp(team, Color.white, 0.4f), 0.75f, 2.6f));
            }

            var body = Holder(rig);
            float r = 0.5f; // body ball radius in model space
            // Tank: armor belt and shoulder plates.
            if (L(Keyword.Tank) > 0)
            {
                Part(body, Prim.SphereMesh, new Vector3(0f, 0.5f, 0f), new Vector3(1.06f, 0.14f + 0.02f * L(Keyword.Tank), 1.06f), steel);
                for (int s = -1; s <= 1; s += 2) Part(body, Prim.SphereMesh, new Vector3(s * 0.42f, 0.92f, 0f), new Vector3(0.3f, 0.12f, 0.36f), steel, Quaternion.Euler(0f, 0f, -s * 25f));
            }
            // Heavy: spikes all over.
            if (L(Keyword.Heavy) > 0)
            {
                int n = 4 + 2 * L(Keyword.Heavy);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 137.5f * Mathf.Deg2Rad, y = Mathf.Lerp(-0.6f, 0.75f, (i + 0.5f) / n);
                    var dir = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1f - y * y), y, Mathf.Sin(a) * Mathf.Sqrt(1f - y * y));
                    if (dir.z > 0.55f) continue; // keep the face clear
                    Part(body, Mechanism.SpikeMesh, new Vector3(0f, 0.7f, 0f) + dir * r * 0.95f, new Vector3(0.12f, 0.22f, 0.12f), dark, Quaternion.FromToRotation(Vector3.up, dir));
                }
            }
            // Mobility: back thrusters with glowing jets.
            if (L(Keyword.Mobility) > 0)
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(body, Prim.CubeMesh, new Vector3(s * 0.2f, 0.62f, -0.55f), new Vector3(0.14f, 0.3f, 0.14f), steel);
                    Part(body, Prim.SphereMesh, new Vector3(s * 0.2f, 0.42f, -0.55f), new Vector3(0.12f, 0.1f + 0.05f * L(Keyword.Mobility), 0.12f), Glow(new Color(0.4f, 0.8f, 1f), 0.8f, 3f));
                }
            // Frost: ice crystals on top.
            for (int i = 0; i < L(Keyword.Frost) * 2; i++)
            {
                float a = i * 70f;
                var dir = Quaternion.Euler(-60f + (i % 3) * 15f, a, 0f) * Vector3.forward;
                Part(body, Mechanism.SpikeMesh, new Vector3(0f, 0.7f, 0f) + dir * r * 0.9f, new Vector3(0.1f, 0.2f, 0.1f), Glow(new Color(0.6f, 0.9f, 1f), 0.75f, 1.8f), Quaternion.FromToRotation(Vector3.up, dir));
            }
            // Venom: toxic bubbles drifting around.
            for (int i = 0; i < L(Keyword.Venom) * 2; i++)
            {
                var b = Part(body, Prim.SphereMesh, new Vector3(Mathf.Cos(i * 2.3f) * 0.7f, 0.3f + (i % 3) * 0.3f, Mathf.Sin(i * 2.3f) * 0.7f), Vector3.one * 0.09f, Glow(new Color(0.45f, 1f, 0.3f), 0.7f));
                b.AddComponent<DeckGearBob>().Phase = i;
            }
            // Vampire: little bat wings on the head.
            if (L(Keyword.Vampire) > 0 && head != null)
            {
                var h = Holder(head);
                for (int s = -1; s <= 1; s += 2)
                    Part(h, Prim.CubeMesh, new Vector3(s * 0.38f, 0.12f, -0.1f), new Vector3(0.3f, 0.16f, 0.03f), Mats.Solid(new Color(0.35f, 0.05f, 0.1f)), Quaternion.Euler(0f, s * 20f, s * 25f));
            }
            // Shield: hex plates orbiting the body.
            if (L(Keyword.Shield) > 0)
            {
                var orbit = new GameObject("Orbit").transform;
                orbit.SetParent(body, false);
                orbit.localPosition = new Vector3(0f, 0.7f, 0f);
                orbit.gameObject.AddComponent<DeckGearSpin>().Speed = 60f;
                int n = 1 + L(Keyword.Shield);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n;
                    var pos = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.85f;
                    Part(orbit, Prim.CubeMesh, pos, new Vector3(0.22f, 0.26f, 0.03f), Glow(new Color(0.5f, 0.85f, 1f), 0.6f, 1.6f), Quaternion.Euler(0f, a, 0f));
                }
            }
            // Chaos: random little cubes tumbling around.
            if (L(Keyword.Chaos) > 0)
            {
                var orbit = new GameObject("Chaos").transform;
                orbit.SetParent(body, false);
                orbit.localPosition = new Vector3(0f, 0.9f, 0f);
                var sp = orbit.gameObject.AddComponent<DeckGearSpin>();
                sp.Speed = -90f;
                sp.Axis = new Vector3(0.3f, 1f, 0.2f).normalized;
                for (int i = 0; i < 2 + L(Keyword.Chaos); i++)
                    Part(orbit, Prim.CubeMesh, Random.onUnitSphere * 0.75f, Vector3.one * 0.1f, Mats.Solid(Teams.GetColor(i % Teams.Max)), Random.rotation);
            }
            // Aura: grows with deck size, colored by the strongest keyword; gold for legendaries.
            int n2 = p.Cards.Count;
            if (n2 >= 6)
            {
                int best = 0;
                for (int k = 1; k < counts.Length; k++) if (counts[k] > counts[best]) best = k;
                var col = KeywordColor((Keyword)best);
                if (p.Cards.Count(c => c.Rarity == Rarity.Legendary) >= 2) col = new Color(1f, 0.82f, 0.3f);
                float size = Mathf.Clamp(1.5f + (n2 - 6) * 0.06f, 1.5f, 2.6f);
                var aura = Part(body, Prim.SphereMesh, new Vector3(0f, 0.7f, 0f), Vector3.one * size, Glow(col, Mathf.Clamp(0.12f + (n2 - 6) * 0.01f, 0.12f, 0.3f), 1.4f));
                aura.AddComponent<DeckGearPulse>();
            }
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
        public float Phase;
        Vector3 basePos;
        void Start() => basePos = transform.localPosition;
        void Update() => transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(Time.time * 2f + Phase) * 0.12f, 0f);
    }

    public class DeckGearPulse : MonoBehaviour
    {
        Vector3 baseScale;
        void Start() => baseScale = transform.localScale;
        void Update() => transform.localScale = baseScale * (1f + 0.05f * Mathf.Sin(Time.time * 3f));
    }
}
