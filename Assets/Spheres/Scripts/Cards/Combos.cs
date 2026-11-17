using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>What a card is "about". Derived automatically from its stats and effects.</summary>
    public enum Keyword { Bouncy, Explosive, Shield, Mobility, Rapid, Ammo, Vampire, Pierce, Tank, Homing, Frost, Venom, Heavy, Multi, Sniper, Giant, Chaos }

    /// <summary>
    /// Emergent combos: any two keywords in your deck combine. One keyword supplies a TRIGGER (when something happens:
    /// a bounce, a hit, a block, a jump...) and the other a PAYLOAD (what happens: a blast, a chill, a heal, shrapnel...).
    /// 17 keywords give 272 distinct combo mechanics, plus a "mastery" bonus for stacking one keyword, and they all
    /// stack with every card's own stats and effects. Combo strength grows with how many cards feed it.
    /// </summary>
    public static class Combos
    {
        public static readonly int KeywordCount = Enum.GetValues(typeof(Keyword)).Length;

        static readonly string[] TriggerWord =
        { "Ricochet", "Impact", "Guarded", "Leaping", "Rhythm", "Reloaded", "Bloodthirsty", "Piercing", "Steady", "Seeking", "Frozen", "Toxic", "Crushing", "Echoing", "Distant", "Massive", "Wild" };
        static readonly string[] PayloadWord =
        { "Shockwave", "Blast", "Recharge", "Dash", "Frenzy", "Refill", "Feast", "Lance", "Plating", "Swarm", "Chill", "Plague", "Quake", "Shrapnel", "Spark", "Growth", "Gamble" };
        static readonly string[] TriggerText =
        {
            "When your bullets bounce", "When your bullets hit something", "When you block", "When you jump", "Every 4th shot",
            "When you finish reloading", "When you hit an enemy", "When a bullet pierces an enemy", "Every 3 seconds", "When a bullet hits an enemy",
            "When you hit an enemy", "When you hit an enemy", "When you hit an enemy", "When you fire", "When you hit from far away",
            "When your bullets hit a wall", "Sometimes, when you fire",
        };
        static readonly string[] PayloadText =
        {
            "knock everyone nearby away", "set off a mini explosion", "your block recharges faster", "you dash forward", "your next shots fire faster",
            "refill 1 ammo", "heal a little", "fire a piercing lance", "gain a little barrier", "launch a homing bolt",
            "chill nearby enemies", "poison nearby enemies", "send out a crushing quake", "spray shrapnel", "zap the nearest enemy",
            "grow your next bullet", "something random happens",
        };

        /// <summary>Special names for some pairs (trigger keyword, payload keyword).</summary>
        static readonly Dictionary<(Keyword, Keyword), string> Named = new Dictionary<(Keyword, Keyword), string>
        {
            { (Keyword.Homing, Keyword.Explosive), "Smart Missiles" },
            { (Keyword.Bouncy, Keyword.Explosive), "Pinball Wizard" },
            { (Keyword.Shield, Keyword.Explosive), "Counter Bomb" },
            { (Keyword.Vampire, Keyword.Frost), "Cold Blooded" },
            { (Keyword.Mobility, Keyword.Vampire), "Leap of Faith" },
            { (Keyword.Rapid, Keyword.Multi), "Bullet Hose" },
            { (Keyword.Explosive, Keyword.Frost), "Snowball Fight" },
            { (Keyword.Bouncy, Keyword.Multi), "Disco Ball" },
            { (Keyword.Tank, Keyword.Shield), "Fortress" },
            { (Keyword.Sniper, Keyword.Explosive), "Artillery" },
            { (Keyword.Giant, Keyword.Heavy), "Wrecking Ball" },
            { (Keyword.Venom, Keyword.Explosive), "Stink Bomb" },
            { (Keyword.Chaos, Keyword.Chaos), "Pure Chaos" },
            { (Keyword.Pierce, Keyword.Vampire), "Kebab" },
            { (Keyword.Ammo, Keyword.Multi), "Magazine Dump" },
            { (Keyword.Mobility, Keyword.Explosive), "Rocket Boots" },
            { (Keyword.Frost, Keyword.Heavy), "Avalanche" },
            { (Keyword.Shield, Keyword.Homing), "Return to Sender" },
        };

        public class Combo
        {
            public Keyword Trigger, Payload;
            public string Name, Description;
            public bool Mastery => Trigger == Payload;
            public string Id => Trigger + "+" + Payload;
        }

        public static string Name(Keyword trigger, Keyword payload)
        {
            if (Named.TryGetValue((trigger, payload), out var n)) return n;
            if (trigger == payload) return TriggerWord[(int)trigger] + " Mastery";
            return TriggerWord[(int)trigger] + " " + PayloadWord[(int)payload];
        }

        public static string Describe(Keyword trigger, Keyword payload)
        {
            if (trigger == payload) return "3+ " + trigger + " cards: " + TriggerText[(int)trigger].ToLowerInvariant() + ", " + PayloadText[(int)payload] + " (stronger).";
            return TriggerText[(int)trigger] + ", " + PayloadText[(int)payload] + ".";
        }

        public static int TotalCombos => KeywordCount * (KeywordCount - 1) + KeywordCount;

        // ---------------------------------------------------------------- keywords

        static readonly Dictionary<string, Keyword[]> cache = new Dictionary<string, Keyword[]>();

        static readonly (Stat stat, Keyword kw)[] StatKeywords =
        {
            (Stat.Bounces, Keyword.Bouncy), (Stat.ExplosionRadius, Keyword.Explosive), (Stat.BlockCooldown, Keyword.Shield), (Stat.BlockDuration, Keyword.Shield),
            (Stat.MoveSpeed, Keyword.Mobility), (Stat.JumpHeight, Keyword.Mobility), (Stat.ExtraJumps, Keyword.Mobility), (Stat.AirControl, Keyword.Mobility),
            (Stat.AttackCooldown, Keyword.Rapid), (Stat.Ammo, Keyword.Ammo), (Stat.ReloadTime, Keyword.Ammo), (Stat.Lifesteal, Keyword.Vampire),
            (Stat.Pierce, Keyword.Pierce), (Stat.MaxHealth, Keyword.Tank), (Stat.Regen, Keyword.Tank), (Stat.DamageTaken, Keyword.Tank), (Stat.ExtraLives, Keyword.Tank),
            (Stat.Homing, Keyword.Homing), (Stat.Knockback, Keyword.Heavy), (Stat.BulletsPerShot, Keyword.Multi), (Stat.BulletSpeed, Keyword.Sniper),
            (Stat.BulletLifetime, Keyword.Sniper), (Stat.BulletSize, Keyword.Giant), (Stat.PlayerSize, Keyword.Giant), (Stat.Spread, Keyword.Chaos),
        };

        static readonly (string fragment, Keyword kw)[] EffectKeywords =
        {
            ("frost", Keyword.Frost), ("chill", Keyword.Frost), ("ice", Keyword.Frost), ("venom", Keyword.Venom), ("decay", Keyword.Venom), ("poison", Keyword.Venom),
            ("blood", Keyword.Vampire), ("leech", Keyword.Vampire), ("vamp", Keyword.Vampire), ("mine", Keyword.Explosive), ("fuse", Keyword.Explosive), ("explo", Keyword.Explosive),
            ("guard", Keyword.Shield), ("barrier", Keyword.Shield), ("mirror", Keyword.Shield), ("wall", Keyword.Shield), ("echo", Keyword.Multi), ("split", Keyword.Multi),
            ("bounce", Keyword.Bouncy), ("rebound", Keyword.Bouncy), ("orbit", Keyword.Chaos), ("sentry", Keyword.Chaos), ("gravity", Keyword.Heavy), ("thorns", Keyword.Heavy),
            ("medic", Keyword.Tank), ("mending", Keyword.Tank), ("last_stand", Keyword.Tank), ("ghost", Keyword.Pierce), ("momentum", Keyword.Mobility), ("recoil", Keyword.Mobility),
            ("frenzy", Keyword.Rapid), ("adrenaline", Keyword.Rapid), ("overcharge", Keyword.Rapid), ("refund", Keyword.Ammo), ("clip", Keyword.Ammo), ("long_shot", Keyword.Sniper),
            ("steady", Keyword.Sniper), ("growth", Keyword.Giant), ("stun", Keyword.Heavy), ("arc", Keyword.Chaos), ("boomerang", Keyword.Bouncy), ("execut", Keyword.Heavy),
        };

        /// <summary>A card's keywords (its improved stats and its effects).</summary>
        public static Keyword[] Of(CardDefinition c)
        {
            if (c == null) return Array.Empty<Keyword>();
            if (cache.TryGetValue(c.Id, out var k)) return k;
            var set = new List<Keyword>();
            foreach (var m in c.Modifiers)
            {
                StatInfo.Format(m, out int pol);
                if (pol <= 0) continue;
                foreach (var (stat, kw) in StatKeywords)
                    if (m.Stat == stat && !set.Contains(kw)) set.Add(kw);
            }
            foreach (var e in c.Effects)
                foreach (var (frag, kw) in EffectKeywords)
                    if (e.Contains(frag) && !set.Contains(kw)) set.Add(kw);
            if (c.IsCurse) set.Clear();
            k = set.ToArray();
            cache[c.Id] = k;
            return k;
        }

        /// <summary>How many cards feed each keyword.</summary>
        public static int[] Counts(IEnumerable<CardDefinition> cards)
        {
            var counts = new int[KeywordCount];
            foreach (var c in cards)
                foreach (var k in Of(c)) counts[(int)k]++;
            return counts;
        }

        /// <summary>The combos a set of cards activates, with levels. Each keyword pair runs one way (by order).</summary>
        public static List<(Combo combo, int level)> Active(IEnumerable<CardDefinition> cards)
        {
            var counts = Counts(cards);
            var list = new List<(Combo, int)>();
            for (int a = 0; a < KeywordCount; a++)
            {
                if (counts[a] == 0) continue;
                if (counts[a] >= 3) list.Add((Make((Keyword)a, (Keyword)a), counts[a] - 2));
                for (int b = 0; b < KeywordCount; b++)
                {
                    if (b == a || counts[b] == 0) continue;
                    // One direction per pair: the keyword with more cards is the trigger (ties: lower index).
                    bool aTriggers = counts[a] > counts[b] || (counts[a] == counts[b] && a < b);
                    if (!aTriggers) continue;
                    list.Add((Make((Keyword)a, (Keyword)b), Math.Min(counts[a], counts[b])));
                }
            }
            return list;
        }

        static readonly Dictionary<(Keyword, Keyword), Combo> made = new Dictionary<(Keyword, Keyword), Combo>();

        public static Combo Make(Keyword t, Keyword p)
        {
            if (made.TryGetValue((t, p), out var c)) return c;
            c = new Combo { Trigger = t, Payload = p, Name = Name(t, p), Description = Describe(t, p) };
            made[(t, p)] = c;
            return c;
        }

        /// <summary>Combos a player would newly unlock by taking this card (for the pick screen).</summary>
        public static List<Combo> WouldUnlock(Player p, CardDefinition card)
        {
            var now = new HashSet<string>(Active(p.Cards).Select(x => x.combo.Id));
            var after = Active(p.Cards.Concat(new[] { card }));
            return after.Where(x => !now.Contains(x.combo.Id)).Select(x => x.combo).ToList();
        }

        // ---------------------------------------------------------------- per-player runtime

        /// <summary>Rebuild a player's combo effects from their cards (host and clients; effects only act on the host).</summary>
        public static void Refresh(Player p, bool announce)
        {
            if (p == null) return;
            var before = new HashSet<string>(p.Effects.OfType<ComboEffect>().Select(e => e.Combo.Id));
            for (int i = p.Effects.Count - 1; i >= 0; i--)
                if (p.Effects[i] is ComboEffect) p.Effects.RemoveAt(i);
            var active = Active(p.Cards);
            foreach (var (combo, level) in active)
                p.Effects.Add(new ComboEffect { Player = p, Combo = combo, Level = level });
            if (!announce) return;
            foreach (var (combo, level) in active)
            {
                if (before.Contains(combo.Id)) continue;
                SpheresHud.AddFeedTextLocal("<color=#" + ColorUtility.ToHtmlStringRGB(p.Color) + ">" + p.DisplayName + "</color> unlocked combo: <b>" + combo.Name + "</b>");
                if (p.Input is HumanInput)
                {
                    SpheresHud.ShowBannerLocal("COMBO!  " + combo.Name, new Color(1f, 0.85f, 0.3f));
                    ComboBook.Discover(combo);
                }
            }
        }

        static bool hooked;

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            GameEvents.CardPicked += (p, c) => Refresh(p, true);
            GameEvents.PlayerSpawned += p => Refresh(p, false);
        }
    }

    /// <summary>The runtime side of one active combo on a player.</summary>
    public class ComboEffect : CardEffect
    {
        public Combos.Combo Combo;
        public int Level = 1;
        float cooldown, pulse, frenzy;
        int shots;
        bool wasGrounded = true, wasReloading;

        static float CooldownFor(Keyword trigger) =>
            trigger == Keyword.Rapid || trigger == Keyword.Chaos || trigger == Keyword.Multi ? 0.15f :
            trigger == Keyword.Bouncy || trigger == Keyword.Explosive || trigger == Keyword.Giant ? 0.2f : 0.35f;

        bool Ready() => cooldown <= 0f && GameManager.Instance != null && !GameManager.Instance.NetClientMode;

        void Fire(Vector3 at, Player target)
        {
            if (!Ready()) return;
            cooldown = CooldownFor(Combo.Trigger);
            Payload(Combo.Payload, at, target, Combo.Mastery ? Level + 1 : Level);
        }

        // ---- triggers

        public override void OnUpdate(float dt)
        {
            cooldown -= dt;
            frenzy -= dt;
            var p = Player;
            if (p == null || !p.IsAlive) return;
            switch (Combo.Trigger)
            {
                case Keyword.Mobility:
                    if (wasGrounded && !p.Grounded && p.Velocity.y > 3f) Fire(p.transform.position, null);
                    wasGrounded = p.Grounded;
                    break;
                case Keyword.Ammo:
                    bool reloading = p.Weapon.Reloading;
                    if (wasReloading && !reloading) Fire(p.Center, null);
                    wasReloading = reloading;
                    break;
                case Keyword.Tank:
                    pulse += dt;
                    if (pulse >= 3f) { pulse = 0f; Fire(p.Center, null); }
                    break;
            }
        }

        public override void OnBulletBounce(Bullet bullet, RaycastHit hit)
        {
            if (bullet.FromCombo) return;
            if (Combo.Trigger == Keyword.Bouncy) Fire(hit.point + hit.normal * 0.3f, null);
        }

        public override void OnBulletHitTerrain(Bullet bullet, RaycastHit hit)
        {
            if (bullet.FromCombo) return;
            if (Combo.Trigger == Keyword.Explosive || Combo.Trigger == Keyword.Giant) Fire(hit.point + hit.normal * 0.3f, null);
        }

        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info)
        {
            if (bullet.FromCombo) return;
            switch (Combo.Trigger)
            {
                case Keyword.Explosive:
                case Keyword.Vampire:
                case Keyword.Homing:
                case Keyword.Frost:
                case Keyword.Venom:
                case Keyword.Heavy:
                    Fire(target.Center, target);
                    break;
                case Keyword.Pierce:
                    if (bullet.PierceLeft >= 0 && bullet.AlreadyHit.Count >= 1) Fire(target.Center, target);
                    break;
                case Keyword.Sniper:
                    if ((bullet.Position - bullet.SpawnPosition).sqrMagnitude > 15f * 15f) Fire(target.Center, target);
                    break;
            }
        }

        public override void OnBlock()
        {
            if (Combo.Trigger == Keyword.Shield) Fire(Player.Center, null);
        }

        public override void OnFire(Bullet bullet)
        {
            if (bullet.FromCombo) return;
            if (frenzy > 0f) Player.Weapon.CooldownBonus(0.35f);
            shots++;
            if (Combo.Trigger == Keyword.Rapid && shots % 4 == 0) Fire(bullet.Position, null);
            else if (Combo.Trigger == Keyword.Multi) Fire(bullet.Position, null);
            else if (Combo.Trigger == Keyword.Chaos && UnityEngine.Random.value < 0.2f) Fire(bullet.Position, null);
            if (growNext > 0f)
            {
                bullet.Radius *= 1f + growNext;
                bullet.Damage *= 1f + growNext * 0.5f;
                bullet.BuildVisual();
                growNext = 0f;
            }
        }

        float growNext;

        // ---- payloads

        Player NearestEnemy(Vector3 from, float range)
        {
            Player best = null;
            float bd = range * range;
            foreach (var o in Player.All)
            {
                if (o == null || !o.IsAlive || o.Team == Player.Team) continue;
                float d = (o.Center - from).sqrMagnitude;
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        Bullet Shoot(Vector3 from, Vector3 dir, float speed, float damage, float size)
        {
            var b = Bullet.Spawn(Player, from, dir.normalized * speed);
            b.FromCombo = true;
            b.Damage = damage;
            b.Radius = size;
            b.Gravity = 0f;
            b.ExplosionRadius = 0f;
            b.BouncesLeft = 0;
            b.Lifetime = 1.5f;
            b.BuildVisual();
            return b;
        }

        void Payload(Keyword payload, Vector3 at, Player target, int lv)
        {
            var p = Player;
            float L = Mathf.Clamp(lv, 1, 6);
            var gold = new Color(1f, 0.85f, 0.35f);
            switch (payload)
            {
                case Keyword.Bouncy: // shockwave
                    foreach (var o in Player.All)
                    {
                        if (o == null || !o.IsAlive || o.Team == p.Team) continue;
                        var d = o.Center - at;
                        if (d.sqrMagnitude > 9f) continue;
                        o.Velocity += (d.normalized + Vector3.up * 0.5f) * (6f + 2f * L);
                    }
                    Fx.Pulse(at, new Color(1f, 1f, 1f, 0.5f), 0.5f, 6f, 0.25f);
                    break;
                case Keyword.Explosive:
                    Explosion.Create(at, 1.4f + 0.35f * L, 7f + 4f * L, p, 6f, new Color(1f, 0.55f, 0.2f), false, "combo");
                    break;
                case Keyword.Shield:
                    p.BlockCooldownTimer = Mathf.Max(0f, p.BlockCooldownTimer - (0.35f + 0.2f * L));
                    Fx.Pulse(p.Center, new Color(0.5f, 0.9f, 1f, 0.5f), 1f, 2.5f, 0.2f);
                    break;
                case Keyword.Mobility:
                {
                    var fwd = new Vector3(p.AimDirection.x, 0f, p.AimDirection.z).normalized;
                    p.Velocity += fwd * (7f + 2f * L);
                    Fx.Burst(p.transform.position, new Color(0.8f, 0.9f, 1f), 6, 4f, 0.12f, 0.3f);
                    break;
                }
                case Keyword.Rapid:
                    frenzy = 1f + 0.4f * L;
                    break;
                case Keyword.Ammo:
                    p.Weapon.Ammo = Mathf.Min(p.Weapon.Ammo + 1, p.GetStatInt(Stat.Ammo) + (int)L);
                    break;
                case Keyword.Vampire:
                    p.Heal(2f + 2f * L);
                    Fx.Burst(p.Center, new Color(1f, 0.3f, 0.35f), 5, 3f, 0.1f, 0.4f);
                    break;
                case Keyword.Pierce:
                {
                    var dir = target != null ? target.Center - p.Center : p.AimDirection;
                    var b = Shoot(p.Head.position, dir, 70f, 6f + 3f * L, 0.08f);
                    b.PierceLeft = 3;
                    break;
                }
                case Keyword.Tank:
                    p.Barrier = Mathf.Min(p.Barrier + 3f + 2f * L, 25f + 10f * L);
                    break;
                case Keyword.Homing:
                {
                    var e = NearestEnemy(at, 30f);
                    var b = Shoot(at + Vector3.up * 0.5f, e != null ? e.Center - at : Vector3.up, 22f, 5f + 3f * L, 0.1f);
                    b.Homing = 2.5f;
                    b.Lifetime = 2.5f;
                    break;
                }
                case Keyword.Frost:
                    foreach (var o in Player.All)
                        if (o != null && o.IsAlive && o.Team != p.Team && (o.Center - at).sqrMagnitude < 9f)
                            o.ApplySlow(0.25f + 0.08f * L, 1.2f);
                    Fx.Burst(at, new Color(0.7f, 0.95f, 1f), 8, 4f, 0.12f, 0.5f);
                    break;
                case Keyword.Venom:
                    foreach (var o in Player.All)
                        if (o != null && o.IsAlive && o.Team != p.Team && (o.Center - at).sqrMagnitude < 9f)
                            o.ApplyPoison(3f + 2f * L, 2f, p);
                    Fx.Smoke(at, 3, 1f, new Color(0.5f, 0.9f, 0.3f, 0.5f));
                    break;
                case Keyword.Heavy:
                    foreach (var o in Player.All)
                    {
                        if (o == null || !o.IsAlive || o.Team == p.Team) continue;
                        var d = o.Center - at;
                        if (d.sqrMagnitude > 16f) continue;
                        o.TakeDamage(new DamageInfo { Amount = 3f + 2f * L, Source = p, Type = DamageType.Effect, Point = at, Direction = d + Vector3.up, Knockback = 8f + 3f * L, Cause = "combo" });
                    }
                    CameraShake.Impulse(at, 0.2f, 15f);
                    Fx.Pulse(at, new Color(0.8f, 0.6f, 0.3f, 0.5f), 0.5f, 7f, 0.3f);
                    break;
                case Keyword.Multi:
                {
                    int n = 2 + (int)L;
                    for (int i = 0; i < n; i++)
                        Shoot(at, UnityEngine.Random.onUnitSphere + p.AimDirection, 30f, 3f + 1.5f * L, 0.07f);
                    break;
                }
                case Keyword.Sniper:
                {
                    var e = NearestEnemy(at, 25f);
                    if (e == null) break;
                    e.TakeDamage(new DamageInfo { Amount = 4f + 3f * L, Source = p, Type = DamageType.Effect, Point = e.Center, Direction = e.Center - at, Cause = "combo" });
                    Fx.Zap(at, e.Center, gold);
                    break;
                }
                case Keyword.Giant:
                    growNext = Mathf.Min(2.5f, growNext + 0.4f + 0.2f * L);
                    break;
                case Keyword.Chaos:
                {
                    var options = new[] { Keyword.Bouncy, Keyword.Explosive, Keyword.Frost, Keyword.Venom, Keyword.Multi, Keyword.Homing, Keyword.Heavy, Keyword.Vampire };
                    Payload(options[UnityEngine.Random.Range(0, options.Length)], at, target, lv);
                    break;
                }
            }
        }
    }

    /// <summary>Combos you've discovered (saved per PC).</summary>
    public static class ComboBook
    {
        static HashSet<string> found;
        static string FilePath => Path.Combine(Application.persistentDataPath, "combos.txt");

        public static IReadOnlyCollection<string> Found { get { Load(); return found; } }

        static void Load()
        {
            if (found != null) return;
            found = new HashSet<string>();
            try { if (File.Exists(FilePath)) foreach (var l in File.ReadAllLines(FilePath)) if (!string.IsNullOrWhiteSpace(l)) found.Add(l.Trim()); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static void Discover(Combos.Combo c)
        {
            Load();
            if (!found.Add(c.Id)) return;
            try { File.AppendAllLines(FilePath, new[] { c.Id }); }
            catch (Exception e) { Debug.LogException(e); }
            Toasts.Show("New combo discovered!", c.Name + ": " + c.Description + "  (" + found.Count + " / " + Combos.TotalCombos + " found)", new Color(1f, 0.85f, 0.3f));
        }
    }
}
