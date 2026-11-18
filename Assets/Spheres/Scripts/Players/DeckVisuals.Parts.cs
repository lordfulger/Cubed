using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// The deck gear catalog: one small piece per stat direction (raised / lowered) and per card effect.
    /// Where things go: weapon stats and bullet effects on the gun, utility on the head, defense on the body,
    /// movement on the feet. Bulky or tall pieces stay on the left / low side, away from the over-the-right-shoulder
    /// camera's line of sight.
    /// </summary>
    public static partial class DeckVisuals
    {
        // ------------------------------------------------------------------ feature kinds

        const int S = (int)Stat.HandSize + 1;

        /// <summary>Card effects with their own piece, in <see cref="FxIds"/> order.</summary>
        enum Fx
        {
            Echo, RecoilJets, FullClip, Riposte, GrowthRounds, FuseShells, GravitySlug, ChainBounce, GhostRounds, Boomerang,
            StunTips, HushRounds, MendingGuard, BarrierCell, GuardBurst, SentryDrop, MineLayer, OrbitCubes, LastStand, SecondGuard,
            Trophy, Executioner, GiantSlayer, Backstab, CloseQuarters, LongShot, Bloodletter, Momentum, SteadyHands, Frenzy,
            DecayField, FieldMedic, PulseGuard, MirrorGuard, CubeWall, PhaseStep, FrostTips, Venom, ArcCoil, ReboundBoost,
            Splitter, Adrenaline, AmmoRefund, Thorns, Overcharge, Count,
        }

        static readonly string[] FxIds =
        {
            "echo", "recoil_jets", "full_clip", "riposte", "growth_rounds", "fuse_shells", "gravity_slug", "chain_bounce", "ghost_rounds", "boomerang",
            "stun_tips", "hush_rounds", "mending_guard", "barrier_cell", "guard_burst", "sentry_drop", "mine_layer", "orbit_cubes", "last_stand", "second_guard",
            "trophy", "executioner", "giant_slayer", "backstab", "close_quarters", "long_shot", "bloodletter", "momentum", "steady_hands", "frenzy",
            "decay_field", "field_medic", "pulse_guard", "mirror_guard", "cube_wall", "phase_step", "frost_tips", "venom", "arc_coil", "rebound_boost",
            "splitter", "adrenaline", "ammo_refund", "thorns", "overcharge",
        };

        const int FxN = (int)Fx.Count;
        // Kinds: [0,S) stat raised, [S,2S) stat lowered, then effects, curses, charms.
        const int FxKind = 2 * S, CurseKind = FxKind + FxN, CharmKind = CurseKind + 1, KindCount = CharmKind + 1;

        static Dictionary<string, int> fxIndex;

        static int FxIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            if (id.StartsWith("echo")) return (int)Fx.Echo; // echo_1, echo_2
            if (fxIndex == null)
            {
                fxIndex = new Dictionary<string, int>();
                for (int i = 0; i < FxIds.Length; i++) fxIndex[FxIds[i]] = i;
            }
            return fxIndex.TryGetValue(id, out int k) ? k : -1;
        }

        // ------------------------------------------------------------------ tally

        static readonly int[] up = new int[S], down = new int[S], fx = new int[FxN];
        static int curses;
        static readonly List<Color> charms = new List<Color>();
        static float tipBase, tip, ext;

        /// <summary>+1 if a modifier raises its stat, -1 if it lowers it, 0 if it does nothing.</summary>
        static int Dir(StatModifier m)
        {
            switch (m.Op)
            {
                case StatOp.Add: return m.Value > 1e-4f ? 1 : m.Value < -1e-4f ? -1 : 0;
                case StatOp.Multiply: return m.Value > 1.0001f ? 1 : m.Value < 0.9999f ? -1 : 0;
                default:
                    float b = StatInfo.Get(m.Stat).Base;
                    return m.Value > b + 1e-4f ? 1 : m.Value < b - 1e-4f ? -1 : 0;
            }
        }

        static void Tally(Player p)
        {
            System.Array.Clear(up, 0, S);
            System.Array.Clear(down, 0, S);
            System.Array.Clear(fx, 0, FxN);
            curses = 0;
            charms.Clear();
            foreach (var c in p.Cards)
            {
                if (c == null) continue;
                bool shown = false, unknown = false;
                foreach (var m in c.Modifiers)
                {
                    int s = (int)m.Stat;
                    if (s < 0 || s >= S) continue;
                    int d = Dir(m);
                    if (d > 0) up[s]++;
                    else if (d < 0) down[s]++;
                    if (d != 0) shown = true;
                }
                foreach (var id in c.Effects)
                {
                    int k = FxIndex(id);
                    if (k < 0) { unknown = true; continue; }
                    fx[k] += id == "echo_2" ? 2 : 1;
                    shown = true;
                }
                if (c.IsCurse) curses++;
                // Mod cards (code-only, or effects we have no piece for) still show up as a charm.
                if (!shown || unknown) charms.Add(c.Color);
            }

            ext = up[(int)Stat.BulletSpeed] > 0 ? 0.16f + 0.07f * Mathf.Min(5, up[(int)Stat.BulletSpeed]) : 0f;
            tipBase = 0.41f + ext;
            tip = tipBase + (fx[(int)Fx.HushRounds] > 0 ? 0.2f : 0f);
        }

        static int LevelOf(int k)
        {
            if (k < S) return up[k];
            if (k < 2 * S) return down[k - S];
            if (k < CurseKind) return fx[k - FxKind];
            if (k == CurseKind) return curses;
            return charms.Count;
        }

        /// <summary>Builds copy <paramref name="copy"/> of a feature; for copy 0 returns how many copies it wants.</summary>
        static int Build(int kind, int level, int copy)
        {
            if (kind < S) return Raised((Stat)kind, level, copy, kind);
            if (kind < 2 * S) return Lowered((Stat)(kind - S), level, copy, kind);
            if (kind < CurseKind) return Effect((Fx)(kind - FxKind), level, copy, kind);
            if (kind == CurseKind) return Curse(level, copy, kind);
            return Charm(copy, kind);
        }

        // ------------------------------------------------------------------ shared shapes

        static Vector3[] spikeDirs;

        /// <summary>Spike directions spread over the body, skipping the face.</summary>
        static Vector3[] SpikeDirs
        {
            get
            {
                if (spikeDirs != null) return spikeDirs;
                var list = new List<Vector3>();
                const int n = 16;
                for (int i = 0; i < n; i++)
                {
                    float a = i * 137.5f * Mathf.Deg2Rad, y = Mathf.Lerp(-0.6f, 0.75f, (i + 0.5f) / n), r = Mathf.Sqrt(1f - y * y);
                    var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    if (dir.z > 0.55f) continue; // keep the face clear
                    list.Add(dir);
                }
                spikeDirs = list.ToArray();
                return spikeDirs;
            }
        }

        static void BodySpike(int k, float len, Material m)
        {
            var dirs = SpikeDirs;
            var dir = dirs[k % dirs.Length];
            Spike(B, BC + dir * 0.47f, new Vector3(0.12f, len, 0.12f), m, Quaternion.FromToRotation(Vector3.up, dir));
        }

        // ------------------------------------------------------------------ stats raised

        static int Raised(Stat s, int n, int c, int kind)
        {
            int L = Mathf.Min(n, 5);
            var steel = Mats.Solid(Steel);
            var dark = Mats.Solid(DarkSteel);
            switch (s)
            {
                case Stat.MaxHealth: // red hearts on the back
                {
                    var red = Shiny(0.95f, 0.2f, 0.3f);
                    if (c == 0) Heart(B, BodyPt(14f, 180f, 0.52f), 0.2f + 0.03f * L, 0.06f, red, Face(14f, 180f));
                    else
                    {
                        float lon = c == 1 ? 150f : 210f;
                        Heart(B, BodyPt(-6f, lon, 0.52f), 0.13f, 0.05f, red, Face(-6f, lon));
                    }
                    return Mathf.Min(n, 3);
                }
                case Stat.MoveSpeed: // glowing speed fins on the heels
                {
                    var fin = Glow(new Color(0.4f, 0.85f, 1f), 0.85f, 2.6f);
                    for (int side = -1; side <= 1; side += 2)
                        Spike(Foot(side), new Vector3(side * 0.08f, 0.02f + 0.06f * c, -0.13f), new Vector3(0.04f, 0.15f + 0.03f * L, 0.09f), fin, E(-100f + 18f * c, -side * 15f, 0f));
                    return Mathf.Min(n, 2);
                }
                case Stat.JumpHeight: // spring coils on the feet
                {
                    var spring = Shiny(0.55f, 1f, 0.3f, 0.8f, 0.4f);
                    for (int side = -1; side <= 1; side += 2)
                        Ring(Foot(side), new Vector3(side * 0.17f, -0.03f + 0.045f * c, -0.04f), 0.1f, 0.025f, spring, I);
                    return Mathf.Min(n, 3);
                }
                case Stat.ExtraJumps: // a pair of little wings per air jump
                {
                    var feather = Shiny(0.97f, 0.97f, 1f, 0.5f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float lat = 8f - 14f * c, lon = 180f - side * 42f;
                        Box(B, BodyPt(lat, lon, 0.5f) + new Vector3(side * 0.12f, 0f, 0f), new Vector3(0.26f - 0.04f * c, 0.08f, 0.02f), feather,
                            Face(lat, lon) * E(0f, 0f, side * (20f + 12f * c)));
                    }
                    return Mathf.Min(n, 3);
                }
                case Stat.AirControl: // tail fin, then stabilizers
                {
                    var fin = Shiny(0.82f, 0.95f, 1f);
                    var pos = BodyPt(-42f, 180f, 0.5f) + new Vector3(0f, -0.02f, -0.08f);
                    if (c == 0) Box(B, pos, new Vector3(0.025f, 0.16f + 0.02f * L, 0.2f), fin, E(-25f, 0f, 0f));
                    else Box(B, pos, new Vector3(0.36f, 0.025f, 0.12f), fin, E(-25f, 0f, 0f));
                    return Mathf.Min(n, 2);
                }
                case Stat.Damage: // red bayonet under the barrel, a second blade on top
                {
                    var blade = Shiny(0.85f, 0.25f, 0.25f, 0.85f, 0.6f);
                    if (c == 0) Spike(G, new Vector3(0f, -0.08f, 0.26f), new Vector3(0.05f, 0.24f + 0.05f * L, 0.1f), blade, E(90f, 0f, 0f));
                    else Spike(G, new Vector3(0f, 0.08f, 0.24f), new Vector3(0.04f, 0.16f, 0.07f), blade, E(82f, 0f, 0f));
                    return Mathf.Min(n, 2);
                }
                case Stat.AttackCooldown: // slower: a wind-up key in the back of the gun
                {
                    var brass = Shiny(0.85f, 0.65f, 0.25f, 0.8f, 0.6f);
                    Stick(G, new Vector3(0f, 0.04f, -0.38f), new Vector3(0f, 0.04f, -0.52f), 0.03f, brass);
                    var key = Piv(kind * 8, G, new Vector3(0f, 0.04f, -0.56f), 90f, Vector3.forward);
                    Box(key, Vector3.zero, new Vector3(0.02f, 0.13f, 0.08f), brass);
                    return 1;
                }
                case Stat.Ammo: // drum magazine, then rounds sticking out of it
                {
                    float sz = 0.16f + 0.025f * L, y = -0.16f - sz * 0.4f;
                    if (c == 0) Ball(G, new Vector3(0f, y, 0.06f), new Vector3(0.12f, sz * 1.6f, sz * 1.6f), dark);
                    else Cyl(G, new Vector3(0.05f, y, -0.1f + 0.07f * c), 0.035f, 0.09f, Shiny(0.9f, 0.7f, 0.3f, 0.8f, 0.6f), E(0f, 0f, -90f));
                    return Mathf.Min(n, 4);
                }
                case Stat.ReloadTime: // slower: duct tape around the barrel
                {
                    var tape = Solid(0.72f, 0.72f, 0.7f);
                    Ring(G, new Vector3(0f, 0f, 0.12f - 0.2f * c), 0.23f, 0.05f, tape, E(105f, 0f, c == 0 ? 10f : -12f));
                    return Mathf.Min(n, 2);
                }
                case Stat.BulletSpeed: // longer barrel with a muzzle brake
                    Box(G, new Vector3(0f, 0f, 0.41f + ext * 0.5f), new Vector3(0.08f, 0.08f, ext), dark);
                    Box(G, new Vector3(0f, 0f, tipBase), new Vector3(0.13f, 0.05f, 0.06f), dark);
                    return 1;
                case Stat.BulletGravity: // more drop: a weight swinging under the barrel
                {
                    var w = Piv(kind * 8, G, new Vector3(0f, -0.08f, 0.33f));
                    var wob = w.gameObject.AddComponent<DeckGearWobble>();
                    wob.Angle = 18f;
                    wob.Speed = 4f;
                    Stick(w, Vector3.zero, new Vector3(0f, -0.17f, 0f), 0.015f, dark);
                    Ball(w, new Vector3(0f, -0.21f, 0f), 0.09f, dark);
                    return 1;
                }
                case Stat.BulletsPerShot: // extra barrels around the main one (spinning if it also fires faster)
                {
                    int total = Mathf.Min(6, Mathf.Max(n, owner.Stats.Int(Stat.BulletsPerShot) - 1));
                    var spin = Piv(kind * 8, G, new Vector3(0f, 0f, 0.05f));
                    if (c == 0 && down[(int)Stat.AttackCooldown] > 0) spin.gameObject.AddComponent<DeckGearSpin>().Owner = owner;
                    for (int i = c * 2; i < Mathf.Min(total, c * 2 + 2); i++)
                    {
                        float a = i * Mathf.PI * 2f / Mathf.Max(1, total);
                        Box(spin, new Vector3(Mathf.Cos(a) * 0.14f, Mathf.Sin(a) * 0.14f, 0.08f), new Vector3(0.07f, 0.07f, 0.62f), dark);
                    }
                    return (total + 1) / 2;
                }
                case Stat.Spread: // worse aim: a crooked, wobbling tip
                {
                    var w = Piv(kind * 8, G, new Vector3(0f, 0f, tip));
                    if (c == 0)
                    {
                        var wob = w.gameObject.AddComponent<DeckGearWobble>();
                        wob.Axis = new Vector3(1f, 1f, 0f).normalized;
                        wob.Angle = 10f;
                        wob.Speed = 7f;
                        Box(w, new Vector3(0.02f, 0.02f, 0.07f), new Vector3(0.07f, 0.07f, 0.15f), dark, E(-18f, 14f, 0f));
                    }
                    else Ring(w, new Vector3(0.04f, 0.04f, 0.14f), 0.1f, 0.02f, Shiny(1f, 0.55f, 0.75f), E(72f, 14f, 0f));
                    return Mathf.Min(n, 2);
                }
                case Stat.Bounces: // pink spring coil on top of the gun
                {
                    var coil = Shiny(0.95f, 0.45f, 0.85f);
                    int from = c == 0 ? 0 : 2 + c, to = c == 0 ? 3 : 3 + c;
                    for (int k = from; k < to; k++)
                        Box(G, new Vector3(k % 2 == 0 ? 0.04f : -0.04f, 0.11f + k * 0.035f, -0.28f), new Vector3(0.1f, 0.018f, 0.03f), coil);
                    return Mathf.Min(n, 4);
                }
                case Stat.BulletSize: // a glowing orb charging at the tip
                {
                    float size = Mathf.Clamp(owner.Stats[Stat.BulletSize] * 1.6f, 0.15f, 0.6f);
                    Ball(G, new Vector3(0f, 0f, tip + 0.05f), size, Glow(Color.Lerp(owner.Color, Color.white, 0.4f), 0.75f, 2.6f));
                    return 1;
                }
                case Stat.BulletLifetime: // scope
                {
                    float len = 0.3f + 0.04f * L;
                    Box(G, new Vector3(0f, 0.15f, 0.02f), new Vector3(0.07f, 0.07f, len), dark);
                    Ball(G, new Vector3(0f, 0.15f, 0.02f + len * 0.5f), new Vector3(0.08f, 0.08f, 0.03f), Glow(new Color(1f, 0.3f, 0.3f), 0.9f));
                    return 1;
                }
                case Stat.Knockback: // shockwave rings in front of the muzzle
                {
                    var ring = Ring(G, new Vector3(0f, 0f, tip + 0.05f + 0.07f * c), 0.2f + 0.06f * c, 0.025f, Glow(new Color(1f, 0.75f, 0.4f), 0.7f), E(90f, 0f, 0f));
                    var pulse = ring.AddComponent<DeckGearPulse>();
                    pulse.Amount = 0.12f;
                    pulse.Speed = 6f + c;
                    return Mathf.Min(n, 3);
                }
                case Stat.Lifesteal: // bat wings on the head, then fangs
                {
                    if (c == 0)
                    {
                        var wing = Mats.Solid(new Color(0.35f, 0.05f, 0.1f));
                        for (int side = -1; side <= 1; side += 2)
                            Box(H, new Vector3(side * 0.38f, 0.12f, -0.1f), new Vector3(0.26f + 0.03f * L, 0.14f + 0.015f * L, 0.03f), wing, E(0f, side * 20f, side * 25f));
                    }
                    else
                        for (int side = -1; side <= 1; side += 2)
                            Spike(H, new Vector3(side * 0.06f, -0.22f, 0.46f), new Vector3(0.035f, 0.08f, 0.035f), Solid(0.97f, 0.97f, 0.95f), E(180f, 0f, 0f));
                    return Mathf.Min(n, 2);
                }
                case Stat.BlockCooldown: // slower block: an hourglass on the right shoulder
                {
                    var hg = Piv(kind * 8, B, BodyPt(42f, 100f, 0.5f));
                    var glass = Shiny(0.95f, 0.8f, 0.45f, 0.9f);
                    Cone(hg, Vector3.zero, 0.12f, 0.08f, glass, I);
                    Cone(hg, new Vector3(0f, 0.16f, 0f), 0.12f, 0.08f, glass, E(180f, 0f, 0f));
                    return 1;
                }
                case Stat.BlockDuration: // round buckler on the left side
                {
                    Cyl(B, BodyPt(0f, -90f, 0.49f), 0.36f + 0.03f * L, 0.04f, Shape.Shiny(Color.Lerp(Steel, owner.Color, 0.35f), 0.8f, 0.5f), Out(0f, -90f));
                    Ball(B, BodyPt(0f, -90f, 0.54f), 0.09f, Mats.Solid(owner.Color));
                    return 1;
                }
                case Stat.Regen: // floating green plus signs
                {
                    var plus = Piv(kind * 8 + c, H, new Vector3(-0.3f - 0.14f * c, 0.45f - 0.12f * c, -0.05f), 0f, null, c * 1.7f);
                    float k = c == 0 ? 1f : 0.7f;
                    var green = Glow(new Color(0.35f, 1f, 0.45f), 0.9f, 2.4f);
                    Box(plus, Vector3.zero, new Vector3(0.14f, 0.045f, 0.045f) * k, green);
                    Box(plus, Vector3.zero, new Vector3(0.045f, 0.14f, 0.045f) * k, green);
                    return Mathf.Min(n, 2);
                }
                case Stat.Homing: // radar dish
                {
                    Box(G, new Vector3(0.1f, 0.15f, -0.1f), new Vector3(0.02f, 0.12f, 0.02f), steel);
                    var dish = Ball(G, new Vector3(0.1f, 0.23f, -0.1f), new Vector3(0.16f, 0.05f, 0.16f), steel, E(-30f, 0f, 20f));
                    dish.AddComponent<DeckGearSpin>().Speed = 120f;
                    return 1;
                }
                case Stat.ExplosionRadius: // grenades strapped to the side
                    Ball(G, new Vector3(-0.14f, -0.04f, -0.12f + c * 0.11f), Vector3.one * 0.1f, Solid(0.3f, 0.45f, 0.2f));
                    Box(G, new Vector3(-0.14f, 0.02f, -0.12f + c * 0.11f), new Vector3(0.03f, 0.04f, 0.03f), steel);
                    return Mathf.Min(n, 4);
                case Stat.Pierce: // spinning drill bit on the muzzle
                {
                    var drill = Spike(G, new Vector3(0f, 0f, tip - 0.02f), new Vector3(0.14f, 0.2f + 0.05f * L, 0.14f), steel, E(90f, 0f, 0f));
                    drill.AddComponent<DeckGearSpin>().Axis = Vector3.up;
                    return 1;
                }
                case Stat.ExtraLives: // a golden halo per extra life
                    Ring(H, new Vector3(0f, 0.5f + 0.07f * c, 0f), 0.42f - 0.05f * c, 0.035f, Glow(new Color(1f, 0.85f, 0.35f), 0.9f, 2.6f), I);
                    return Mathf.Min(n, 3);
                case Stat.PlayerSize: // bigger: a bullseye on the belly
                    Ring(B, BodyPt(-30f, 0f, 0.5f), 0.24f, 0.045f, Shiny(0.9f, 0.15f, 0.15f), Out(-30f, 0f));
                    Ball(B, BodyPt(-30f, 0f, 0.5f), 0.075f, Solid(0.97f, 0.97f, 0.97f));
                    return 1;
                case Stat.DamageTaken: // fragile: cracks across the body
                {
                    var crack = Solid(0.08f, 0.08f, 0.1f);
                    float lat = c == 0 ? 15f : c == 1 ? -10f : 32f, lon = c == 0 ? 105f : c == 1 ? -110f : 200f;
                    var a = BodyPt(lat, lon, 0.505f);
                    var b = BodyPt(lat - 12f, lon + 9f, 0.505f);
                    var d = BodyPt(lat - 24f, lon + 2f, 0.505f);
                    Stick(B, a, b, 0.025f, crack);
                    Stick(B, b, d, 0.025f, crack);
                    return Mathf.Min(n, 3);
                }
                case Stat.HazardDamageTaken: // hazard sign on the lower back
                {
                    var rot = Face(-25f, 215f) * E(0f, 0f, 45f);
                    var pos = BodyPt(-25f, 215f, 0.52f);
                    Box(B, pos, new Vector3(0.16f, 0.16f, 0.02f), Shiny(1f, 0.82f, 0.1f), rot);
                    Box(B, pos + Sph(-25f, 215f) * 0.012f, new Vector3(0.035f, 0.035f, 0.02f), Solid(0.08f, 0.08f, 0.08f), rot);
                    return 1;
                }
                case Stat.HandSize: // a fan of mini cards floating by the head
                {
                    var fan = Piv(kind * 8, H, new Vector3(-0.66f, 0.12f, -0.05f), 0f, null, 0.5f, 0.03f);
                    var card = c % 2 == 0 ? Solid(0.98f, 0.95f, 0.85f) : Mats.Solid(CardDefinition.RarityColor(Rarity.Rare));
                    Box(fan, new Vector3(0.035f * c, 0.01f * c, 0f), new Vector3(0.1f, 0.14f, 0.012f), card, E(0f, -60f, 25f - 18f * c));
                    return Mathf.Min(n, 4);
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ stats lowered

        static int Lowered(Stat s, int n, int c, int kind)
        {
            int L = Mathf.Min(n, 5);
            var steel = Mats.Solid(Steel);
            var dark = Mats.Solid(DarkSteel);
            switch (s)
            {
                case Stat.MaxHealth: // a grey, cracked heart on the chest
                {
                    var rot = Face(-20f, -55f);
                    Heart(B, BodyPt(-20f, -55f, 0.52f), 0.15f, 0.05f, Solid(0.6f, 0.55f, 0.58f), rot);
                    Box(B, BodyPt(-20f, -55f, 0.55f), new Vector3(0.02f, 0.17f, 0.02f), Solid(0.1f, 0.1f, 0.1f), rot * E(0f, 0f, 30f));
                    return 1;
                }
                case Stat.MoveSpeed: // ball and chain dragging behind a foot
                {
                    int side = c == 0 ? 1 : -1;
                    var f = Foot(side);
                    Stick(f, new Vector3(side * 0.05f, 0f, -0.15f), new Vector3(side * 0.08f, -0.03f, -0.29f), 0.025f, steel);
                    Ball(f, new Vector3(side * 0.08f, -0.02f, -0.36f), 0.13f + 0.01f * L, Solid(0.15f, 0.15f, 0.17f));
                    return Mathf.Min(n, 2);
                }
                case Stat.JumpHeight: // lead weights on the toes
                    for (int side = -1; side <= 1; side += 2)
                        Box(Foot(side), new Vector3(0f, 0.07f, 0.07f), new Vector3(0.18f, 0.06f, 0.14f), Shiny(0.35f, 0.37f, 0.42f, 0.3f, 0.6f));
                    return 1;
                case Stat.ExtraJumps: // a drooping grey feather
                    Box(B, BodyPt(5f, 138f, 0.5f) + new Vector3(0.1f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.02f), Solid(0.55f, 0.55f, 0.58f), Face(5f, 138f) * E(0f, 0f, -60f));
                    return 1;
                case Stat.AirControl: // a deflated cushion on the lower back
                    Ball(B, BodyPt(-40f, 180f, 0.5f), new Vector3(0.2f, 0.12f, 0.05f), Solid(0.5f, 0.52f, 0.55f), Face(-40f, 180f));
                    return 1;
                case Stat.Damage: // foam ball stuck on the barrel
                {
                    var foam = Solid(1f, 0.6f, 0.75f);
                    Ball(G, c == 0 ? new Vector3(0f, -0.07f, 0.32f) : new Vector3(0f, 0.08f, 0.3f), 0.09f + 0.01f * L, foam);
                    return Mathf.Min(n, 2);
                }
                case Stat.AttackCooldown: // faster: cooling rings (and spinning barrels)
                    Ring(G, new Vector3(0f, 0f, -0.3f + 0.08f * c), 0.27f, 0.03f, steel, E(90f, 0f, 0f));
                    return Mathf.Min(n, 4);
                case Stat.Ammo: // a stubby mag with a red stripe
                    Box(G, new Vector3(0f, -0.14f, 0.1f), new Vector3(0.07f, 0.08f, 0.1f), dark);
                    Box(G, new Vector3(0f, -0.17f, 0.1f), new Vector3(0.075f, 0.02f, 0.105f), Solid(0.9f, 0.15f, 0.15f));
                    return 1;
                case Stat.ReloadTime: // faster: speed loaders behind the grip
                    Cyl(G, new Vector3(0f, -0.1f, -0.38f - 0.09f * c), 0.11f, 0.07f, Shiny(0.9f, 0.7f, 0.25f, 0.8f, 0.6f), E(90f, 0f, 0f));
                    return Mathf.Min(n, 2);
                case Stat.BulletSpeed: // slow bullets: a snail shell on the gun
                    Ball(G, new Vector3(0f, 0.16f, -0.16f), new Vector3(0.07f, 0.15f, 0.15f), Shiny(0.6f, 0.4f, 0.2f));
                    Ring(G, new Vector3(0.04f, 0.16f, -0.16f), 0.09f, 0.02f, Solid(0.85f, 0.7f, 0.45f), E(0f, 0f, 90f));
                    return 1;
                case Stat.BulletGravity: // floaty bullets: balloons tied to the gun
                {
                    var bal = Piv(kind * 8 + c, G, new Vector3(-0.08f, 0.06f, 0.05f - 0.12f * c), 0f, null, c * 2.1f, 0.03f);
                    Stick(bal, Vector3.zero, new Vector3(-0.03f, 0.17f, 0f), 0.01f, Solid(0.9f, 0.9f, 0.9f));
                    var col = c == 0 ? new Color(0.5f, 0.8f, 1f) : c == 1 ? new Color(1f, 0.55f, 0.7f) : new Color(1f, 0.9f, 0.4f);
                    Ball(bal, new Vector3(-0.03f, 0.24f, 0f), new Vector3(0.12f, 0.15f, 0.12f), Shape.Shiny(col, 0.85f));
                    return Mathf.Min(n, 3);
                }
                case Stat.BulletsPerShot: // a bent stub barrel
                    Box(G, new Vector3(0f, 0.1f, 0.3f), new Vector3(0.05f, 0.05f, 0.18f), dark, E(-25f, 0f, 0f));
                    return 1;
                case Stat.Spread: // better aim: a laser sight
                    Box(G, new Vector3(0.11f, -0.04f, 0.16f), new Vector3(0.04f, 0.04f, 0.2f), dark);
                    Ball(G, new Vector3(0.11f, -0.04f, 0.27f), 0.045f, Glow(new Color(0.3f, 1f, 0.4f), 0.95f, 3f));
                    return 1;
                case Stat.Bounces: // a flat, deflated pad
                    Ball(G, new Vector3(0f, 0.1f, -0.28f), new Vector3(0.12f, 0.03f, 0.08f), Solid(0.65f, 0.45f, 0.6f));
                    return 1;
                case Stat.BulletSize: // tiny bullets: a needle tip
                    Spike(G, new Vector3(0f, 0f, tip), new Vector3(0.03f, 0.14f, 0.03f), Solid(0.95f, 0.95f, 0.97f), E(90f, 0f, 0f));
                    return 1;
                case Stat.BulletLifetime: // short range: a blunderbuss bell
                    Cone(G, new Vector3(0f, 0f, tip - 0.02f), 0.12f, 0.12f, Shiny(0.85f, 0.65f, 0.3f, 0.8f, 0.6f), E(90f, 0f, 0f), 2.2f);
                    return 1;
                case Stat.Knockback: // a pillow on the muzzle
                    Box(G, new Vector3(0f, 0f, tip + 0.03f), new Vector3(0.17f, 0.17f, 0.06f), Solid(0.95f, 0.93f, 0.88f));
                    return 1;
                case Stat.Lifesteal: // a leaking drop of blood
                    Ball(H, new Vector3(0.2f, -0.3f, 0.4f), new Vector3(0.05f, 0.08f, 0.05f), Shiny(0.7f, 0.05f, 0.1f, 0.9f));
                    return 1;
                case Stat.BlockCooldown: // faster block: shield plates orbiting the body
                {
                    int total = Mathf.Min(n + 1, 5);
                    var orbit = Piv(kind * 8, B, BC, 60f);
                    float a = c * 360f / total;
                    Box(orbit, Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.85f, new Vector3(0.22f, 0.26f, 0.03f), Glow(new Color(0.5f, 0.85f, 1f), 0.6f, 1.6f), E(0f, a, 0f));
                    return total;
                }
                case Stat.BlockDuration: // a broken half shield
                    Ring(B, BodyPt(0f, -90f, 0.5f), 0.3f, 0.03f, Solid(0.5f, 0.5f, 0.55f), Out(0f, -90f), 160f);
                    return 1;
                case Stat.Regen: // a wilted leaf on the head
                    Box(H, new Vector3(-0.1f, 0.24f, -0.05f), new Vector3(0.12f, 0.02f, 0.06f), Solid(0.5f, 0.38f, 0.2f), E(0f, 30f, -40f));
                    return 1;
                case Stat.Homing: // a bent antenna
                    Stick(G, new Vector3(0.08f, 0.08f, -0.1f), new Vector3(0.12f, 0.2f, -0.1f), 0.015f, steel);
                    return 1;
                case Stat.ExplosionRadius: // a corked dud grenade
                    Ball(G, new Vector3(-0.14f, -0.04f, -0.12f), 0.1f, Solid(0.45f, 0.45f, 0.45f));
                    Ball(G, new Vector3(-0.14f, 0.02f, -0.12f), new Vector3(0.05f, 0.04f, 0.05f), Solid(0.7f, 0.5f, 0.3f));
                    return 1;
                case Stat.Pierce: // a cap over the muzzle
                    Cyl(G, new Vector3(0f, 0f, tip - 0.02f), 0.17f, 0.03f, Solid(0.5f, 0.5f, 0.52f), E(90f, 0f, 0f));
                    return 1;
                case Stat.ExtraLives: // a broken halo
                    Ring(H, new Vector3(0f, 0.5f, 0f), 0.4f, 0.035f, Solid(0.55f, 0.52f, 0.45f), E(12f, 0f, 8f), 160f);
                    return 1;
                case Stat.PlayerSize: // smaller: tiny motes orbiting
                {
                    var orbit = Piv(kind * 8, B, BC, 120f);
                    Ball(orbit, Sph(10f * c, c * 120f) * 0.72f, 0.07f, Glow(new Color(1f, 1f, 0.9f), 0.9f, 2.6f));
                    return Mathf.Min(n + 1, 3);
                }
                case Stat.DamageTaken: // armor: belt, shoulder pads, helmet rim
                    if (c == 0) Ball(B, new Vector3(0f, 0.5f, 0f), new Vector3(1.06f, 0.14f + 0.02f * L, 1.06f), steel);
                    else if (c == 1)
                        for (int side = -1; side <= 1; side += 2)
                            Ball(B, new Vector3(side * 0.42f, 0.92f, 0f), new Vector3(0.3f, 0.12f, 0.36f), steel, E(0f, 0f, -side * 25f));
                    else Ring(H, new Vector3(0f, 0.12f, -0.03f), 0.7f, 0.06f, steel, E(-6f, 0f, 0f));
                    return Mathf.Min(n, 3);
                case Stat.HazardDamageTaken: // a swim ring around the waist
                    if (c == 0) Ring(B, new Vector3(0f, 0.32f, 0f), 0.76f, 0.12f, Shiny(1f, 0.45f, 0.15f), I);
                    else
                        for (int k = 0; k < 2; k++)
                            Ring(B, new Vector3(0f, 0.32f, 0f), 0.76f, 0.13f, Shiny(0.97f, 0.97f, 0.97f), E(0f, k * 180f + 45f, 0f), 50f);
                    return Mathf.Min(n, 2);
                case Stat.HandSize: // a torn card
                    Box(H, new Vector3(-0.62f, 0.1f, -0.05f), new Vector3(0.1f, 0.07f, 0.012f), Solid(0.6f, 0.58f, 0.55f), E(0f, -60f, -20f));
                    return 1;
            }
            return 0;
        }

        // ------------------------------------------------------------------ effects

        static int Effect(Fx e, int n, int c, int kind)
        {
            int L = Mathf.Min(n, 5);
            var steel = Mats.Solid(Steel);
            var dark = Mats.Solid(DarkSteel);
            switch (e)
            {
                case Fx.Echo: // translucent ghost barrels trailing the gun
                    Ball(G, new Vector3(-0.06f - 0.04f * c, 0.1f + 0.08f * c, -0.1f - 0.08f * c), new Vector3(0.17f, 0.17f, 0.7f), Mats.Transparent(new Color(owner.Color.r, owner.Color.g, owner.Color.b, 0.3f)));
                    return Mathf.Min(n, 2);
                case Fx.RecoilJets: // nozzles in the back of the gun
                    for (int side = -1; side <= 1; side += 2)
                        Cone(G, new Vector3(side * 0.07f, -0.03f, -0.36f), 0.07f, 0.1f, steel, E(-90f, 0f, 0f), 1.6f);
                    Ball(G, new Vector3(0f, -0.03f, -0.5f), 0.08f, Glow(new Color(1f, 0.6f, 0.2f), 0.8f, 3f)).AddComponent<DeckGearPulse>().Amount = 0.25f;
                    return 1;
                case Fx.FullClip: // a big golden round on the gun's side
                {
                    var gold = Shiny(1f, 0.8f, 0.25f, 0.9f, 0.7f);
                    Cyl(G, new Vector3(0.13f, -0.06f, -0.2f), 0.06f, 0.12f, gold, E(90f, 0f, 0f));
                    Cone(G, new Vector3(0.13f, -0.06f, -0.08f), 0.06f, 0.06f, gold, E(90f, 0f, 0f));
                    return 1;
                }
                case Fx.Riposte: // a rapier on the left side
                {
                    var rot = Face(10f, -120f) * E(0f, 0f, 35f);
                    var pos = BodyPt(10f, -120f, 0.53f);
                    Box(B, pos, new Vector3(0.03f, 0.42f, 0.012f), Shiny(0.85f, 0.87f, 0.92f, 0.9f, 0.8f), rot);
                    Box(B, pos + rot * new Vector3(0f, -0.13f, 0.01f), new Vector3(0.14f, 0.03f, 0.03f), Shiny(0.85f, 0.65f, 0.25f, 0.8f, 0.6f), rot);
                    return 1;
                }
                case Fx.GrowthRounds: // three growing glowing beads on the gun
                {
                    var g = Glow(new Color(0.7f, 1f, 0.35f), 0.85f, 2.4f);
                    Ball(G, new Vector3(-0.07f, 0.1f, 0.12f), 0.04f, g);
                    Ball(G, new Vector3(-0.07f, 0.11f, 0.19f), 0.06f, g);
                    Ball(G, new Vector3(-0.07f, 0.12f, 0.28f), 0.085f, g);
                    return 1;
                }
                case Fx.FuseShells: // a lit fuse sparking at the back of the gun
                    Stick(G, new Vector3(-0.04f, 0.08f, -0.33f), new Vector3(-0.1f, 0.2f, -0.42f), 0.018f, Solid(0.35f, 0.25f, 0.15f));
                    Ball(G, new Vector3(-0.1f, 0.21f, -0.43f), 0.07f, Glow(new Color(1f, 0.7f, 0.2f), 0.9f, 3f)).AddComponent<DeckGearPulse>().Amount = 0.3f;
                    return 1;
                case Fx.GravitySlug: // a tiny black hole spinning beside the gun
                {
                    var hole = Piv(kind * 8, G, new Vector3(0.24f, 0.02f, 0.1f), 150f);
                    Ball(hole, Vector3.zero, 0.09f, Solid(0.12f, 0.05f, 0.2f));
                    Ring(hole, Vector3.zero, 0.19f, 0.02f, Glow(new Color(0.7f, 0.35f, 1f), 0.8f, 2.6f), E(70f, 0f, 0f));
                    return 1;
                }
                case Fx.ChainBounce: // two chain links hanging off the gun
                    Ring(G, new Vector3(0.11f, -0.1f, 0.02f), 0.07f, 0.018f, steel, E(0f, 0f, 90f));
                    Ring(G, new Vector3(0.11f, -0.15f, 0.02f), 0.07f, 0.018f, steel, E(90f, 0f, 0f));
                    return 1;
                case Fx.GhostRounds: // a little ghost floating by the head
                {
                    var ghost = Piv(kind * 8, H, new Vector3(-0.5f, 0.3f, -0.3f), 0f, null, 0.8f, 0.05f);
                    var mat = Mats.Transparent(new Color(0.9f, 0.95f, 1f, 0.5f));
                    Ball(ghost, Vector3.zero, new Vector3(0.16f, 0.2f, 0.16f), mat);
                    Ball(ghost, new Vector3(0f, -0.13f, -0.02f), new Vector3(0.08f, 0.1f, 0.08f), mat);
                    return 1;
                }
                case Fx.Boomerang: // a boomerang clipped to the gun
                {
                    var holder = Piv(kind * 8, G, new Vector3(0.12f, 0.05f, 0f));
                    holder.localRotation = E(0f, 90f, 0f);
                    var wood = Shiny(0.75f, 0.5f, 0.25f, 0.5f);
                    Box(holder, new Vector3(-0.05f, 0f, 0f), new Vector3(0.13f, 0.03f, 0.035f), wood, E(0f, 0f, 30f));
                    Box(holder, new Vector3(0.05f, 0f, 0f), new Vector3(0.13f, 0.03f, 0.035f), wood, E(0f, 0f, -30f));
                    return 1;
                }
                case Fx.StunTips: // yellow stars circling the muzzle
                {
                    var spin = Piv(kind * 8, G, new Vector3(0f, 0f, tip), 300f, Vector3.forward);
                    Star(spin, new Vector3(c == 0 ? 0.11f : -0.11f, 0f, 0f), 0.08f, 0.02f, Glow(new Color(1f, 0.9f, 0.2f), 0.95f, 2.6f), I);
                    return Mathf.Min(n, 2);
                }
                case Fx.HushRounds: // a silencer
                    Cyl(G, new Vector3(0f, 0f, tipBase - 0.01f), 0.13f, 0.21f, Shiny(0.12f, 0.12f, 0.14f, 0.3f), E(90f, 0f, 0f));
                    return 1;
                case Fx.MendingGuard: // a band-aid cross on the right side
                {
                    var rot = Face(-15f, 80f);
                    var pos = BodyPt(-15f, 80f, 0.505f);
                    var band = Solid(0.95f, 0.8f, 0.65f);
                    Box(B, pos, new Vector3(0.2f, 0.06f, 0.01f), band, rot * E(0f, 0f, 45f));
                    Box(B, pos, new Vector3(0.2f, 0.06f, 0.01f), band, rot * E(0f, 0f, -45f));
                    return 1;
                }
                case Fx.BarrierCell: // a glowing battery cell on the upper back
                {
                    var dir = Sph(35f, 145f);
                    Cyl(B, BodyPt(35f, 145f, 0.47f), 0.11f, 0.17f, Glow(new Color(0.4f, 0.8f, 1f), 0.85f, 2f), Out(35f, 145f));
                    Cyl(B, BodyPt(35f, 145f, 0.47f) + dir * 0.17f, 0.05f, 0.03f, steel, Out(35f, 145f));
                    return 1;
                }
                case Fx.GuardBurst: // three sparks circling the waist
                {
                    var orbit = Piv(kind * 8, B, new Vector3(0f, 0.35f, 0f), 90f);
                    var spark = Glow(new Color(1f, 0.6f, 0.2f), 0.9f, 2.8f);
                    for (int i = 0; i < 3; i++) Ball(orbit, Sph(0f, i * 120f) * 0.62f, 0.08f, spark);
                    return 1;
                }
                case Fx.SentryDrop: // a mini turret on the left shoulder
                {
                    var pos = BodyPt(42f, -100f, 0.5f);
                    Box(B, pos + new Vector3(0f, 0.03f, 0f), new Vector3(0.13f, 0.08f, 0.13f), steel);
                    Box(B, pos + new Vector3(0f, 0.05f, 0.1f), new Vector3(0.03f, 0.03f, 0.14f), dark);
                    return 1;
                }
                case Fx.MineLayer: // mines clipped round the lower back
                {
                    float lon = c == 0 ? 140f : c == 1 ? 220f : 100f;
                    Cyl(B, BodyPt(-50f, lon, 0.48f), 0.15f, 0.035f, dark, Out(-50f, lon));
                    Ball(B, BodyPt(-50f, lon, 0.52f), 0.04f, Glow(new Color(1f, 0.2f, 0.15f), 0.95f, 3f));
                    return Mathf.Min(n, 3);
                }
                case Fx.OrbitCubes: // cubes orbiting the body
                {
                    var orbit = Piv(kind * 8, B, BC, 100f);
                    var cube = Shiny(0.8f, 0.85f, 0.95f, 0.8f, 0.4f);
                    var axis = c == 0 ? Vector3.right : Vector3.forward;
                    Box(orbit, axis * 0.82f, Vector3.one * 0.12f, cube);
                    Box(orbit, -axis * 0.82f, Vector3.one * 0.12f, cube);
                    return Mathf.Min(n, 2);
                }
                case Fx.LastStand: // a red headband with a tail
                {
                    var red = Shiny(0.85f, 0.1f, 0.1f, 0.4f);
                    Ring(H, new Vector3(0f, 0.15f, 0f), 0.62f, 0.05f, red, E(-8f, 0f, 0f));
                    Box(H, new Vector3(0.05f, 0.09f, -0.33f), new Vector3(0.05f, 0.16f, 0.02f), red, E(30f, 0f, 15f));
                    return 1;
                }
                case Fx.SecondGuard: // a slowly turning refresh arc around the left side
                {
                    var arc = Piv(kind * 8, B, BodyPt(0f, -90f, 0.6f), 60f);
                    arc.localRotation = Out(0f, -90f);
                    Ring(arc, Vector3.zero, 0.5f, 0.025f, Glow(new Color(0.4f, 0.7f, 1f), 0.85f, 2.4f), I, 280f);
                    return 1;
                }
                case Fx.Trophy: // a golden cup on the right hip
                {
                    var gold = Shiny(1f, 0.8f, 0.25f, 0.9f, 0.7f);
                    var pos = BodyPt(-40f, 50f, 0.56f);
                    Cone(B, pos, 0.08f, 0.05f, gold, I, 0.4f);
                    Cone(B, pos + new Vector3(0f, 0.05f, 0f), 0.05f, 0.09f, gold, I, 2.4f);
                    return 1;
                }
                case Fx.Executioner: // an axe on the right hip
                {
                    var pos = BodyPt(-20f, 120f, 0.55f);
                    var dir = Sph(-20f, 120f);
                    Stick(B, pos - new Vector3(0f, 0.2f, 0f), pos + new Vector3(0f, 0.2f, 0f), 0.03f, Solid(0.4f, 0.25f, 0.12f));
                    Box(B, pos + new Vector3(0f, 0.14f, 0f) + dir * 0.07f, new Vector3(0.03f, 0.13f, 0.14f), Shiny(0.5f, 0.1f, 0.12f, 0.6f, 0.5f), Quaternion.LookRotation(dir, Vector3.up));
                    return 1;
                }
                case Fx.GiantSlayer: // a slingshot on the left hip
                {
                    var wood = Shiny(0.55f, 0.35f, 0.18f, 0.4f);
                    var pos = BodyPt(-42f, -80f, 0.56f);
                    Stick(B, pos - new Vector3(0f, 0.12f, 0f), pos, 0.025f, wood);
                    Ring(B, pos + new Vector3(0f, 0.06f, 0f), 0.12f, 0.02f, wood, E(90f, 90f, 0f), 180f);
                    return 1;
                }
                case Fx.Backstab: // a dagger on the lower back
                {
                    var rot = Face(-28f, 150f) * E(0f, 0f, 35f);
                    var pos = BodyPt(-28f, 150f, 0.52f);
                    Box(B, pos, new Vector3(0.035f, 0.2f, 0.012f), Shiny(0.85f, 0.87f, 0.92f, 0.9f, 0.8f), rot);
                    Box(B, pos + rot * new Vector3(0f, -0.12f, 0f), new Vector3(0.05f, 0.07f, 0.03f), Solid(0.1f, 0.1f, 0.12f), rot);
                    return 1;
                }
                case Fx.CloseQuarters: // brass knuckles on the grip
                {
                    var brass = Shiny(0.9f, 0.7f, 0.3f, 0.85f, 0.7f);
                    for (int side = -1; side <= 1; side += 2)
                        Ring(G, new Vector3(side * 0.035f, -0.2f, -0.08f), 0.07f, 0.02f, brass, E(0f, 0f, 90f));
                    return 1;
                }
                case Fx.LongShot: // a monocle over the left eye
                    Ring(H, new Vector3(-0.13f, -0.02f, 0.53f), 0.14f, 0.02f, Shiny(1f, 0.8f, 0.3f, 0.9f, 0.7f), E(90f, 0f, 0f));
                    Ball(H, new Vector3(-0.13f, -0.02f, 0.53f), new Vector3(0.11f, 0.11f, 0.01f), Mats.Transparent(new Color(0.6f, 0.9f, 1f, 0.35f)));
                    return 1;
                case Fx.Bloodletter: // blood drops on the belly
                {
                    var blood = Shiny(0.7f, 0.04f, 0.08f, 0.9f);
                    if (c == 0)
                    {
                        Ball(B, BodyPt(-40f, 18f, 0.51f), new Vector3(0.05f, 0.08f, 0.05f), blood);
                        Ball(B, BodyPt(-50f, -8f, 0.51f), new Vector3(0.05f, 0.08f, 0.05f), blood);
                    }
                    else Ball(B, BodyPt(-30f, 35f, 0.51f), new Vector3(0.05f, 0.08f, 0.05f), blood);
                    return Mathf.Min(n, 2);
                }
                case Fx.Momentum: // speed streaks along the sides
                {
                    var streak = Glow(new Color(0.85f, 0.95f, 1f), 0.8f, 2.4f);
                    for (int side = -1; side <= 1; side += 2)
                        Box(B, BodyPt(25f, side * 110f, 0.56f) + new Vector3(0f, 0f, -0.08f), new Vector3(0.015f, 0.015f, 0.32f + 0.04f * L), streak);
                    return 1;
                }
                case Fx.SteadyHands: // a bipod under the barrel
                    for (int side = -1; side <= 1; side += 2)
                        Stick(G, new Vector3(0f, -0.08f, 0.3f), new Vector3(side * 0.1f, -0.3f, 0.38f), 0.02f, dark);
                    return 1;
                case Fx.Frenzy: // little red horns
                {
                    var horn = Shiny(0.85f, 0.12f, 0.1f, 0.8f);
                    for (int side = -1; side <= 1; side += 2)
                        Spike(H, new Vector3(side * 0.14f, 0.18f, 0.08f), new Vector3(0.06f, 0.11f + 0.015f * L, 0.06f), horn, E(0f, 0f, -side * 20f));
                    return 1;
                }
                case Fx.DecayField: // a sickly ring on the ground around you
                    Ring(B, new Vector3(0f, 0.03f, 0f), 1.7f, 0.05f, Glow(new Color(0.5f, 0.85f, 0.2f), 0.6f, 2f), I).AddComponent<DeckGearPulse>().Amount = 0.08f;
                    return 1;
                case Fx.FieldMedic: // a medkit on the back
                {
                    var rot = Face(-15f, 180f);
                    var pos = BodyPt(-15f, 180f, 0.53f);
                    var dir = Sph(-15f, 180f);
                    var red = Solid(0.9f, 0.1f, 0.12f);
                    Box(B, pos, new Vector3(0.2f, 0.15f, 0.07f), Solid(0.96f, 0.96f, 0.96f), rot);
                    Box(B, pos + dir * 0.04f, new Vector3(0.1f, 0.03f, 0.01f), red, rot);
                    Box(B, pos + dir * 0.04f, new Vector3(0.03f, 0.1f, 0.01f), red, rot);
                    return 1;
                }
                case Fx.PulseGuard: // pulsing shockwave rings round the body
                {
                    var ring = Ring(B, new Vector3(0f, 0.7f - 0.2f * c, 0f), 1.25f - 0.1f * c, 0.03f, Glow(new Color(1f, 0.75f, 0.3f), 0.7f, 2.2f), I);
                    var pulse = ring.AddComponent<DeckGearPulse>();
                    pulse.Amount = 0.08f;
                    pulse.Speed = 4f;
                    return Mathf.Min(n, 2);
                }
                case Fx.MirrorGuard: // a mirror disc orbiting the body
                {
                    var orbit = Piv(kind * 8, B, BC, -45f);
                    Cyl(orbit, new Vector3(0.82f, 0f, 0f), 0.3f, 0.03f, Shiny(0.92f, 0.96f, 1f, 0.98f, 0.9f), E(0f, 0f, 90f));
                    return 1;
                }
                case Fx.CubeWall: // a couple of bricks on the upper back
                {
                    var rot = Face(35f, 215f);
                    var pos = BodyPt(35f, 215f, 0.52f);
                    Box(B, pos, new Vector3(0.16f, 0.08f, 0.08f), Solid(0.75f, 0.35f, 0.25f), rot);
                    Box(B, pos + rot * new Vector3(0.04f, 0.085f, 0f), new Vector3(0.16f, 0.08f, 0.08f), Solid(0.65f, 0.3f, 0.22f), rot);
                    return 1;
                }
                case Fx.PhaseStep: // purple glowing anklets
                {
                    var glow = Glow(new Color(0.75f, 0.4f, 1f), 0.8f, 2.6f);
                    for (int side = -1; side <= 1; side += 2) Ring(Foot(side), new Vector3(0f, 0.1f, -0.02f), 0.3f, 0.03f, glow, I);
                    return 1;
                }
                case Fx.FrostTips: // ice crystals on top of the body
                {
                    var ice = Glow(new Color(0.6f, 0.9f, 1f), 0.75f, 1.8f);
                    for (int i = c * 2; i < c * 2 + 2; i++)
                    {
                        var dir = Quaternion.Euler(-60f + (i % 3) * 15f, i * 70f, 0f) * Vector3.forward;
                        Spike(B, BC + dir * 0.45f, new Vector3(0.1f, 0.2f, 0.1f), ice, Quaternion.FromToRotation(Vector3.up, dir));
                    }
                    return Mathf.Min(n, 3);
                }
                case Fx.Venom: // toxic bubbles drifting around
                {
                    var tox = Glow(new Color(0.45f, 1f, 0.3f), 0.7f);
                    for (int i = c * 2; i < c * 2 + 2; i++)
                    {
                        var b = Ball(B, new Vector3(Mathf.Cos(i * 2.3f) * 0.7f, 0.3f + (i % 3) * 0.3f, Mathf.Sin(i * 2.3f) * 0.7f), 0.09f, tox);
                        b.AddComponent<DeckGearBob>().Phase = i;
                    }
                    return Mathf.Min(n, 3);
                }
                case Fx.ArcCoil: // a tesla antenna on the back of the head
                    Stick(H, new Vector3(-0.17f, 0.15f, -0.2f), new Vector3(-0.22f, 0.45f, -0.26f), 0.025f, steel);
                    Ball(H, new Vector3(-0.22f, 0.48f, -0.26f), 0.09f, Glow(new Color(0.6f, 0.9f, 1f), 0.9f, 3f)).AddComponent<DeckGearPulse>().Amount = 0.25f;
                    return 1;
                case Fx.ReboundBoost: // a pink rubber bumper round the barrel
                    Ring(G, new Vector3(0f, 0f, 0.2f), 0.26f, 0.06f, Shiny(1f, 0.45f, 0.75f, 0.6f), E(90f, 0f, 0f));
                    return 1;
                case Fx.Splitter: // a forked muzzle
                    for (int side = -1; side <= 1; side += 2)
                        Box(G, new Vector3(side * 0.04f, 0f, tip + 0.06f), new Vector3(0.03f, 0.03f, 0.13f), steel, E(0f, side * 18f, 0f));
                    return 1;
                case Fx.Adrenaline: // a syringe stuck in the right side
                {
                    var dir = Sph(15f, 115f);
                    var pos = BodyPt(15f, 115f, 0.5f);
                    Stick(B, pos, pos + dir * 0.07f, 0.012f, steel);
                    Cyl(B, pos + dir * 0.06f, 0.06f, 0.14f, Glow(new Color(1f, 0.3f, 0.35f), 0.8f, 2f), Out(15f, 115f));
                    return 1;
                }
                case Fx.AmmoRefund: // a green recycling loop round the magazine
                    Ring(G, new Vector3(0f, -0.24f, 0.06f), 0.42f, 0.025f, Glow(new Color(0.4f, 1f, 0.5f), 0.8f, 2.2f), E(0f, 0f, 90f));
                    return 1;
                case Fx.Thorns: // dark spikes all over the body
                {
                    var thorn = Solid(0.2f, 0.18f, 0.16f);
                    float len = 0.18f + 0.02f * L;
                    if (c == 0) for (int k = 0; k < 4; k++) BodySpike(k, len, thorn);
                    else
                    {
                        BodySpike(2 + 2 * c, len, thorn);
                        BodySpike(3 + 2 * c, len, thorn);
                    }
                    return Mathf.Min(n, 4);
                }
                case Fx.Overcharge: // a glowing capacitor on the gun
                    Box(G, new Vector3(0.12f, 0.05f, -0.28f), new Vector3(0.05f, 0.08f, 0.12f), Glow(new Color(1f, 0.95f, 0.3f), 0.9f, 2.8f)).AddComponent<DeckGearPulse>().Amount = 0.15f;
                    return 1;
            }
            return 0;
        }

        // ------------------------------------------------------------------ curses and unknown cards

        /// <summary>Curses: purple wisps circling the head.</summary>
        static int Curse(int n, int c, int kind)
        {
            var orbit = Piv(kind * 8, H, new Vector3(0f, 0.3f, 0f), 70f);
            Ball(orbit, Sph(0f, c * 120f) * 0.55f, 0.09f, Glow(CardDefinition.CurseColor, 0.85f, 2.6f));
            return Mathf.Min(n, 3);
        }

        /// <summary>Cards with no known look (mods): a small floating card in the card's own color.</summary>
        static int Charm(int c, int kind)
        {
            var charm = Piv(kind * 8 + c, B, new Vector3(-0.72f, 1.0f - 0.2f * c, 0.15f - 0.1f * c), 0f, null, c * 1.3f, 0.04f);
            var col = charms[c];
            col.a = 1f;
            Box(charm, Vector3.zero, new Vector3(0.12f, 0.16f, 0.015f), Mats.Solid(col), E(0f, -40f, 8f * c));
            return Mathf.Min(charms.Count, 4);
        }
    }
}
