using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// Builds a large, deterministic pool of mix-and-match cards from upgrade ("boon") and drawback
    /// ("bane") building blocks plus the effect library. The same seed produces the same cards on every
    /// machine, so online matches agree on ids. Cards are tagged "generated" and can be filtered in the
    /// Card Library.
    /// </summary>
    public static class CardGenerator
    {
        public const int Seed = 20261114;
        public const int TargetCount = 320;

        enum Theme { Offense, Defense, Mobility, Bullets, Utility }

        class Trait
        {
            public Stat Stat;
            public StatOp Op;
            public float[] Tiers;
            public Theme Theme;
            public string[] Words;
        }

        class EffectTrait
        {
            public string Id;
            public float Power;
            public Theme Theme;
            public string[] Words;
        }

        static readonly float[] BoonPower = { 1f, 2f, 3.2f };
        static readonly float[] BanePower = { 0.8f, 1.6f, 2.6f };

        static Trait B(Stat s, StatOp op, float t1, float t2, float t3, Theme theme, params string[] words) =>
            new Trait { Stat = s, Op = op, Tiers = new[] { t1, t2, t3 }, Theme = theme, Words = words };

        static readonly Trait[] Boons =
        {
            B(Stat.MaxHealth, StatOp.Add, 20, 40, 70, Theme.Defense, "Plate", "Bulwark", "Hide", "Shell", "Rampart"),
            B(Stat.MoveSpeed, StatOp.Multiply, 1.1f, 1.2f, 1.35f, Theme.Mobility, "Stride", "Dash", "Gale", "Sprint", "Rush"),
            B(Stat.JumpHeight, StatOp.Multiply, 1.15f, 1.3f, 1.5f, Theme.Mobility, "Spring", "Leap", "Hop", "Bound"),
            B(Stat.AirControl, StatOp.Multiply, 1.2f, 1.4f, 1.7f, Theme.Mobility, "Fin", "Glide", "Drift", "Wing"),
            B(Stat.Damage, StatOp.Multiply, 1.15f, 1.3f, 1.55f, Theme.Offense, "Edge", "Fang", "Hammer", "Spike", "Maul", "Blade"),
            B(Stat.AttackCooldown, StatOp.Multiply, 0.88f, 0.75f, 0.6f, Theme.Offense, "Trigger", "Tempo", "Flurry", "Rhythm", "Pulse"),
            B(Stat.Ammo, StatOp.Add, 1, 2, 4, Theme.Utility, "Magazine", "Drum", "Cache", "Belt"),
            B(Stat.ReloadTime, StatOp.Multiply, 0.85f, 0.7f, 0.5f, Theme.Utility, "Loader", "Clip", "Breech", "Latch"),
            B(Stat.BulletSpeed, StatOp.Multiply, 1.2f, 1.45f, 1.8f, Theme.Bullets, "Rail", "Bolt", "Dart", "Lance", "Streak"),
            B(Stat.BulletGravity, StatOp.Multiply, 0.7f, 0.45f, 0.2f, Theme.Bullets, "Float", "Levity", "Feather", "Hover"),
            B(Stat.Spread, StatOp.Multiply, 0.7f, 0.5f, 0.25f, Theme.Bullets, "Sight", "Scope", "Focus", "Lens"),
            B(Stat.Bounces, StatOp.Add, 1, 2, 3, Theme.Bullets, "Rebound", "Carom", "Bank", "Recoil"),
            B(Stat.BulletSize, StatOp.Multiply, 1.25f, 1.5f, 2f, Theme.Bullets, "Boulder", "Orb", "Globe", "Slug"),
            B(Stat.BulletLifetime, StatOp.Multiply, 1.3f, 1.6f, 2f, Theme.Bullets, "Range", "Reach", "Horizon", "Voyage"),
            B(Stat.Knockback, StatOp.Multiply, 1.3f, 1.7f, 2.3f, Theme.Offense, "Shove", "Ram", "Gust", "Quake"),
            B(Stat.Lifesteal, StatOp.Add, 0.08f, 0.15f, 0.25f, Theme.Defense, "Drain", "Siphon", "Tithe", "Thirst"),
            B(Stat.BlockCooldown, StatOp.Multiply, 0.88f, 0.75f, 0.6f, Theme.Defense, "Guard", "Parry", "Ward", "Aegis"),
            B(Stat.BlockDuration, StatOp.Multiply, 1.3f, 1.6f, 2f, Theme.Defense, "Shield", "Dome", "Bubble", "Bastion"),
            B(Stat.Regen, StatOp.Add, 2, 4, 7, Theme.Defense, "Bloom", "Mend", "Salve", "Remedy"),
            B(Stat.Homing, StatOp.Add, 0.3f, 0.6f, 1f, Theme.Bullets, "Seeker", "Hound", "Tracker", "Compass"),
            B(Stat.ExplosionRadius, StatOp.Add, 0.8f, 1.5f, 2.5f, Theme.Bullets, "Blast", "Burst", "Charge", "Fuse"),
            B(Stat.Pierce, StatOp.Add, 1, 1, 2, Theme.Bullets, "Needle", "Drill", "Awl", "Lancet"),
            B(Stat.DamageTaken, StatOp.Multiply, 0.92f, 0.85f, 0.75f, Theme.Defense, "Armor", "Carapace", "Mail", "Scale"),
            B(Stat.HazardDamageTaken, StatOp.Multiply, 0.75f, 0.5f, 0.25f, Theme.Defense, "Cushion", "Harness", "Anchor", "Tether"),
            B(Stat.ExtraJumps, StatOp.Add, 1, 1, 2, Theme.Mobility, "Flutter", "Updraft", "Kite", "Cloud"),
            B(Stat.PlayerSize, StatOp.Multiply, 0.92f, 0.85f, 0.78f, Theme.Mobility, "Pebble", "Mote", "Speck", "Sprite"),
        };

        static readonly Trait[] Banes =
        {
            B(Stat.MaxHealth, StatOp.Multiply, 0.9f, 0.8f, 0.65f, Theme.Defense, "Fragile", "Brittle", "Hollow", "Thin"),
            B(Stat.MoveSpeed, StatOp.Multiply, 0.92f, 0.85f, 0.75f, Theme.Mobility, "Heavy", "Sluggish", "Leaden", "Plodding"),
            B(Stat.JumpHeight, StatOp.Multiply, 0.9f, 0.8f, 0.7f, Theme.Mobility, "Grounded", "Weighted", "Earthbound"),
            B(Stat.Damage, StatOp.Multiply, 0.9f, 0.8f, 0.68f, Theme.Offense, "Blunt", "Dull", "Soft", "Gentle"),
            B(Stat.AttackCooldown, StatOp.Multiply, 1.1f, 1.25f, 1.45f, Theme.Offense, "Lazy", "Patient", "Measured", "Slow"),
            B(Stat.Ammo, StatOp.Add, -1, -1, -2, Theme.Utility, "Starved", "Lean", "Sparse"),
            B(Stat.ReloadTime, StatOp.Multiply, 1.15f, 1.35f, 1.6f, Theme.Utility, "Jammed", "Rusty", "Clumsy", "Stiff"),
            B(Stat.BulletSpeed, StatOp.Multiply, 0.88f, 0.75f, 0.62f, Theme.Bullets, "Lazy", "Drifting", "Lobbing", "Slack"),
            B(Stat.BulletGravity, StatOp.Multiply, 1.4f, 1.9f, 2.6f, Theme.Bullets, "Sinking", "Drooping", "Falling", "Plunging"),
            B(Stat.Spread, StatOp.Add, 3, 6, 10, Theme.Bullets, "Wild", "Shaky", "Erratic", "Scattered"),
            B(Stat.BlockCooldown, StatOp.Multiply, 1.12f, 1.3f, 1.5f, Theme.Defense, "Reckless", "Bold", "Exposed", "Open"),
            B(Stat.BlockDuration, StatOp.Multiply, 0.85f, 0.7f, 0.55f, Theme.Defense, "Flickering", "Fleeting", "Brief"),
            B(Stat.DamageTaken, StatOp.Multiply, 1.08f, 1.15f, 1.25f, Theme.Defense, "Glass", "Tender", "Bare", "Raw"),
            B(Stat.HazardDamageTaken, StatOp.Multiply, 1.25f, 1.5f, 2f, Theme.Defense, "Clumsy", "Careless", "Unsteady"),
            B(Stat.PlayerSize, StatOp.Multiply, 1.1f, 1.2f, 1.3f, Theme.Defense, "Bulky", "Hulking", "Towering", "Broad"),
            B(Stat.AirControl, StatOp.Multiply, 0.85f, 0.7f, 0.5f, Theme.Mobility, "Tumbling", "Flailing", "Spinning"),
            B(Stat.BulletLifetime, StatOp.Multiply, 0.85f, 0.7f, 0.55f, Theme.Bullets, "Short", "Stubby", "Myopic"),
            B(Stat.Knockback, StatOp.Multiply, 0.8f, 0.6f, 0.4f, Theme.Offense, "Weak", "Limp", "Feeble"),
        };

        static EffectTrait E(string id, float power, Theme theme, params string[] words) =>
            new EffectTrait { Id = id, Power = power, Theme = theme, Words = words };

        static readonly EffectTrait[] Effects =
        {
            E("pulse_guard", 1.6f, Theme.Defense, "Shockwave", "Pulse"),
            E("mirror_guard", 2.6f, Theme.Defense, "Mirror", "Reflector"),
            E("cube_wall", 1.5f, Theme.Defense, "Wall", "Barricade"),
            E("phase_step", 1.5f, Theme.Mobility, "Blink", "Phase"),
            E("frost_tips", 1.6f, Theme.Bullets, "Frost", "Chill"),
            E("venom", 1.5f, Theme.Bullets, "Venom", "Toxin"),
            E("arc_coil", 2.4f, Theme.Utility, "Storm", "Arc"),
            E("rebound_boost", 1.2f, Theme.Bullets, "Rebound", "Bank"),
            E("splitter", 2.2f, Theme.Bullets, "Splinter", "Fracture"),
            E("adrenaline", 1.5f, Theme.Offense, "Panic", "Rush"),
            E("ammo_refund", 1.1f, Theme.Utility, "Recycler", "Refund"),
            E("thorns", 1.4f, Theme.Defense, "Thorn", "Bramble"),
            E("overcharge", 1.6f, Theme.Offense, "Overcharge", "Surge"),
            E("echo_1", 1.8f, Theme.Offense, "Echo", "Repeater"),
            E("recoil_jets", 0.9f, Theme.Mobility, "Jet", "Thrust"),
            E("full_clip", 1.2f, Theme.Offense, "Opener", "Alpha"),
            E("riposte", 1.3f, Theme.Offense, "Riposte", "Counter"),
            E("growth_rounds", 2.4f, Theme.Bullets, "Swell", "Bloom"),
            E("fuse_shells", 1.8f, Theme.Bullets, "Fuse", "Timer"),
            E("gravity_slug", 2.2f, Theme.Bullets, "Gravity", "Undertow"),
            E("chain_bounce", 2.2f, Theme.Bullets, "Chain", "Seeker"),
            E("ghost_rounds", 2.4f, Theme.Bullets, "Ghost", "Phantom"),
            E("boomerang", 1.5f, Theme.Bullets, "Boomerang", "Return"),
            E("stun_tips", 2.4f, Theme.Bullets, "Stun", "Daze"),
            E("hush_rounds", 1.6f, Theme.Bullets, "Hush", "Silence"),
            E("mending_guard", 1.3f, Theme.Defense, "Mending", "Balm"),
            E("barrier_cell", 1.6f, Theme.Defense, "Barrier", "Cell"),
            E("guard_burst", 1.8f, Theme.Defense, "Nova", "Ring"),
            E("sentry_drop", 2.6f, Theme.Utility, "Sentry", "Turret"),
            E("mine_layer", 2.4f, Theme.Utility, "Mine", "Trap"),
            E("last_stand", 2.4f, Theme.Defense, "Stand", "Defiance"),
            E("second_guard", 1.1f, Theme.Defense, "Encore", "Reset"),
            E("trophy", 1.6f, Theme.Offense, "Trophy", "Hunter"),
            E("executioner", 1.6f, Theme.Offense, "Execution", "Verdict"),
            E("giant_slayer", 1.4f, Theme.Offense, "Slayer", "Underdog"),
            E("backstab", 1.2f, Theme.Offense, "Shadow", "Dagger"),
            E("close_quarters", 1.2f, Theme.Offense, "Brawl", "Knuckle"),
            E("long_shot", 1.4f, Theme.Offense, "Distance", "Far"),
            E("bloodletter", 1.6f, Theme.Offense, "Blood", "Wound"),
            E("momentum", 1.5f, Theme.Offense, "Momentum", "Velocity"),
            E("steady_hands", 1.2f, Theme.Offense, "Steady", "Calm"),
            E("frenzy", 1.6f, Theme.Offense, "Frenzy", "Fury"),
            E("decay_field", 1.8f, Theme.Utility, "Rot", "Blight"),
            E("field_medic", 1.6f, Theme.Utility, "Medic", "Haven"),
        };

        static readonly string[] Nouns = { "Core", "Module", "Charm", "Chip", "Rune", "Lens", "Coil", "Gear", "Shard", "Sigil", "Relic", "Engine", "Token", "Badge", "Prism", "Spark" };
        static readonly string[] Adjectives = { "Keen", "Bright", "Prime", "Noble", "Swift", "Iron", "Golden", "Lucky", "Bold", "Silver", "True", "Vivid", "Grand", "Royal", "Fierce", "Stout" };

        static readonly Dictionary<Theme, Color> ThemeColors = new Dictionary<Theme, Color>
        {
            { Theme.Offense, new Color(0.95f, 0.38f, 0.3f) },
            { Theme.Defense, new Color(0.3f, 0.65f, 1f) },
            { Theme.Mobility, new Color(0.4f, 0.9f, 0.55f) },
            { Theme.Bullets, new Color(1f, 0.75f, 0.25f) },
            { Theme.Utility, new Color(0.75f, 0.5f, 1f) },
        };

        enum Recipe { Pure, Twin, Tradeoff, DoubleTrade, EffectBane, EffectBoon, Gamble, Wildcard }

        static readonly string[] RecipeText =
        {
            "A clean upgrade.",
            "Two upgrades, no catch.",
            "Power at a price.",
            "Big gains, real costs.",
            "A new trick with a drawback.",
            "A new trick and a bonus.",
            "Everything, all at once. Brace yourself.",
            "Chaos in card form.",
        };

        internal static void RegisterAll()
        {
            foreach (var c in Generate()) CardRegistry.Register(c);
        }

        public static List<CardDefinition> Generate(int count = TargetCount, int seed = Seed)
        {
            var rng = new System.Random(seed);
            var cards = new List<CardDefinition>();
            var signatures = new HashSet<string>();
            var names = new HashSet<string>(CardRegistry.All.Select(c => c.Title));
            // Weighted recipe mix: lots of trade-offs, a healthy share of pure upgrades, a few big swings.
            var plan = new List<Recipe>();
            void Add(Recipe r, int n) { for (int i = 0; i < n; i++) plan.Add(r); }
            Add(Recipe.Pure, 5); Add(Recipe.Twin, 3); Add(Recipe.Tradeoff, 8); Add(Recipe.DoubleTrade, 4);
            Add(Recipe.EffectBane, 4); Add(Recipe.EffectBoon, 2); Add(Recipe.Gamble, 1); Add(Recipe.Wildcard, 1);

            int attempts = 0;
            while (cards.Count < count && attempts++ < count * 40)
            {
                var recipe = plan[rng.Next(plan.Count)];
                var mods = new List<(Trait trait, int tier, bool boon)>();
                EffectTrait effect = null;
                switch (recipe)
                {
                    case Recipe.Pure: PickBoons(rng, mods, 1, 0, 1); break;
                    case Recipe.Twin: PickBoons(rng, mods, 2, 0, 0); break;
                    case Recipe.Tradeoff: PickBoons(rng, mods, 1, 1, 2); PickBanes(rng, mods, 1, 0, 1); break;
                    case Recipe.DoubleTrade: PickBoons(rng, mods, 2, 1, 2); PickBanes(rng, mods, 1, 1, 2); break;
                    case Recipe.EffectBane: effect = Effects[rng.Next(Effects.Length)]; PickBanes(rng, mods, 1, 0, 1); break;
                    case Recipe.EffectBoon: effect = Effects[rng.Next(Effects.Length)]; PickBoons(rng, mods, 1, 0, 0); break;
                    case Recipe.Gamble: PickBoons(rng, mods, 3, 2, 2); PickBanes(rng, mods, 2, 2, 2); break;
                    case Recipe.Wildcard:
                        effect = Effects[rng.Next(Effects.Length)];
                        PickBoons(rng, mods, 2, 1, 2);
                        PickBanes(rng, mods, 2, 1, 2);
                        break;
                }
                if (mods.Count == 0 && effect == null) continue;

                string sig = (effect?.Id ?? "") + "|" + string.Join(",", mods.Select(m => (int)m.trait.Stat + ":" + m.tier + (m.boon ? "+" : "-")).OrderBy(x => x));
                if (!signatures.Add(sig)) continue;

                float power = (effect?.Power ?? 0f) + mods.Sum(m => m.boon ? BoonPower[m.tier] : -BanePower[m.tier]);
                if (recipe == Recipe.Gamble || recipe == Recipe.Wildcard) power = Mathf.Max(power, 3.7f);
                var rarity = power <= 1.3f ? Rarity.Common : power <= 2.4f ? Rarity.Uncommon : power <= 3.6f ? Rarity.Rare : Rarity.Legendary;

                var primaryBoon = mods.FirstOrDefault(m => m.boon);
                var primaryBane = mods.FirstOrDefault(m => !m.boon);
                Theme theme = effect != null ? effect.Theme : primaryBoon.trait != null ? primaryBoon.trait.Theme : Theme.Utility;

                string title = MakeName(rng, names, effect, primaryBoon.trait, primaryBane.trait);
                if (title == null) continue;
                names.Add(title);

                var color = ThemeColors[theme];
                float shift = (float)rng.NextDouble() * 0.16f - 0.08f;
                color = new Color(Mathf.Clamp01(color.r + shift), Mathf.Clamp01(color.g - shift * 0.5f), Mathf.Clamp01(color.b + shift * 0.5f));

                string desc = effect != null ? CardEffectRegistry.Describe(effect.Id) + " " + RecipeText[(int)recipe] : RecipeText[(int)recipe];
                var card = new CardDefinition("cubed.gen." + cards.Count.ToString("000"), title, desc, rarity, color)
                {
                    MaxStacks = recipe == Recipe.Gamble || recipe == Recipe.Wildcard ? 1 : 0,
                    // Keep the giant generated pool from drowning out hand-made cards.
                    WeightMultiplier = 0.45f,
                };
                card.Tag("generated").Tag(recipe.ToString().ToLowerInvariant());
                if (effect != null) card.Effect(effect.Id);
                foreach (var m in mods) card.Modifiers.Add(new StatModifier(m.trait.Stat, m.trait.Op, m.trait.Tiers[m.tier]));
                cards.Add(card);
            }
            return cards;
        }

        static void PickBoons(System.Random rng, List<(Trait, int, bool)> mods, int n, int minTier, int maxTier) =>
            Pick(rng, mods, Boons, true, n, minTier, maxTier);

        static void PickBanes(System.Random rng, List<(Trait, int, bool)> mods, int n, int minTier, int maxTier) =>
            Pick(rng, mods, Banes, false, n, minTier, maxTier);

        static void Pick(System.Random rng, List<(Trait trait, int tier, bool boon)> mods, Trait[] pool, bool boon, int n, int minTier, int maxTier)
        {
            for (int i = 0, guard = 0; i < n && guard < 40; guard++)
            {
                var t = pool[rng.Next(pool.Length)];
                if (mods.Any(m => m.trait.Stat == t.Stat)) continue; // never buff and nerf the same stat
                int tier = rng.Next(minTier, maxTier + 1);
                mods.Add((t, tier, boon));
                i++;
            }
        }

        static string MakeName(System.Random rng, HashSet<string> taken, EffectTrait effect, Trait boon, Trait bane)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                string first, second;
                if (bane != null) first = bane.Words[rng.Next(bane.Words.Length)];
                else first = Adjectives[rng.Next(Adjectives.Length)];
                if (effect != null) second = effect.Words[rng.Next(effect.Words.Length)];
                else if (boon != null) second = boon.Words[rng.Next(boon.Words.Length)];
                else second = Nouns[rng.Next(Nouns.Length)];
                if (first == second) continue;
                string name = first + " " + second;
                if (attempt > 10) name += " " + Nouns[rng.Next(Nouns.Length)];
                if (!taken.Contains(name)) return name;
            }
            return null;
        }
    }
}
