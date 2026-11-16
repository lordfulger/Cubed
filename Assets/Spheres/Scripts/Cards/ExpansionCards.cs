using UnityEngine;

namespace Spheres
{
    /// <summary>The expansion card set: original cards covering burst fire, summons, auras, executes, stuns and more.</summary>
    public static class ExpansionCards
    {
        static readonly Color Offense = new Color(0.95f, 0.38f, 0.3f);
        static readonly Color Defense = new Color(0.3f, 0.65f, 1f);
        static readonly Color Mobility = new Color(0.4f, 0.9f, 0.55f);
        static readonly Color Bullets = new Color(1f, 0.75f, 0.25f);
        static readonly Color Utility = new Color(0.75f, 0.5f, 1f);
        static readonly Color Summon = new Color(0.95f, 0.55f, 0.8f);

        static CardDefinition C(string id, string title, string desc, Rarity r, Color c) =>
            new CardDefinition("spheres." + id, title, desc, r, c).Tag("expansion");

        internal static void RegisterAll()
        {
            var cards = new[]
            {
                // ---- Firing patterns
                C("echo_shot", "Echo Shot", "Every shot is followed by a weaker echo.", Rarity.Uncommon, Offense)
                    .Effect("echo_1").Mul(Stat.AttackCooldown, 1.1f),
                C("triplet", "Triplet", "Every shot is followed by two echoes.", Rarity.Rare, Offense)
                    .Effect("echo_2").Mul(Stat.AttackCooldown, 1.25f).Mul(Stat.Damage, 0.85f),
                C("full_clip", "Full Clip", "The first shot from a full magazine deals +80% damage.", Rarity.Common, Offense)
                    .Effect("full_clip"),
                C("riposte", "Riposte", "After a successful block, your next shot deals +80% damage.", Rarity.Uncommon, Offense)
                    .Effect("riposte"),
                C("frenzy_chip", "Frenzy Chip", "Each hit makes you fire 15% faster (up to 5 stacks).", Rarity.Uncommon, Offense)
                    .Effect("frenzy"),

                // ---- Conditional damage
                C("executioner", "Executioner", "+50% damage to enemies below 35% HP.", Rarity.Uncommon, Offense)
                    .Effect("executioner"),
                C("giant_slayer", "Giant Slayer", "Deal up to double damage to enemies with more max HP than you.", Rarity.Uncommon, Offense)
                    .Effect("giant_slayer"),
                C("backstabber", "Backstabber", "+60% damage when you hit enemies from behind.", Rarity.Common, Offense)
                    .Effect("backstab"),
                C("close_quarters", "Close Quarters", "+35% damage within 7m, -20% beyond 15m.", Rarity.Common, Offense)
                    .Effect("close_quarters").Mul(Stat.MoveSpeed, 1.05f),
                C("long_shot", "Long Shot", "Damage grows with distance, up to +80%.", Rarity.Uncommon, Offense)
                    .Effect("long_shot").Mul(Stat.BulletSpeed, 1.2f),
                C("bloodletter", "Bloodletter", "The lower your health, the harder you hit (up to +80%).", Rarity.Uncommon, Offense)
                    .Effect("bloodletter"),
                C("momentum_core", "Momentum Core", "Damage grows with your speed.", Rarity.Uncommon, Offense)
                    .Effect("momentum").Mul(Stat.MoveSpeed, 1.1f),
                C("steady_hands", "Steady Hands", "While standing still: perfect accuracy and +30% damage.", Rarity.Common, Offense)
                    .Effect("steady_hands"),
                C("trophy_hunter", "Trophy Hunter", "Kills heal 40% of your max HP.", Rarity.Uncommon, Offense)
                    .Effect("trophy"),

                // ---- Bullet behaviour
                C("swelling_rounds", "Swelling Rounds", "Bullets grow bigger and deadlier the longer they fly (up to 3x).", Rarity.Rare, Bullets)
                    .Effect("growth_rounds").Mul(Stat.BulletSpeed, 0.75f),
                C("fuse_shells", "Fuse Shells", "Bullets detonate 0.7s after firing.", Rarity.Uncommon, Bullets)
                    .Effect("fuse_shells").Mul(Stat.Damage, 0.9f),
                C("gravity_slugs", "Gravity Slugs", "Bullets drag nearby enemies toward their path.", Rarity.Rare, Bullets)
                    .Effect("gravity_slug").Mul(Stat.BulletSpeed, 0.7f),
                C("chain_bounce", "Chain Bounce", "+2 bounces. Bounces home in on the nearest visible enemy.", Rarity.Rare, Bullets)
                    .Add(Stat.Bounces, 2).Effect("chain_bounce").Mul(Stat.Damage, 0.85f),
                C("ghost_rounds", "Ghost Rounds", "Bullets pass through one wall.", Rarity.Rare, Bullets)
                    .Effect("ghost_rounds").Mul(Stat.Damage, 0.85f),
                C("boomerang", "Boomerang", "Bullets loop back toward you and can hit twice.", Rarity.Uncommon, Bullets)
                    .Effect("boomerang").Add(Stat.Pierce, 1),
                C("stun_tips", "Stun Tips", "Hits have a 30% chance to stun for 0.5s.", Rarity.Rare, Bullets)
                    .Effect("stun_tips"),
                C("hush_rounds", "Hush Rounds", "Hits stop the target from blocking for 2s.", Rarity.Uncommon, Bullets)
                    .Effect("hush_rounds"),
                C("marksman", "Marksman", "Tighter spread and faster bullets.", Rarity.Common, Bullets)
                    .Mul(Stat.Spread, 0.3f).Mul(Stat.BulletSpeed, 1.3f),
                C("dense_core", "Dense Core", "Heavier bullets: more damage and knockback.", Rarity.Common, Bullets)
                    .Mul(Stat.Damage, 1.2f).Mul(Stat.Knockback, 1.5f).Mul(Stat.BulletSpeed, 0.8f),
                C("wide_load", "Wide Load", "+1 bullet and bigger bullets.", Rarity.Common, Bullets)
                    .Add(Stat.BulletsPerShot, 1).Mul(Stat.BulletSize, 1.5f).Add(Stat.Spread, 5f).Mul(Stat.Damage, 0.8f),
                C("sharpened", "Sharpened", "+8 damage.", Rarity.Common, Bullets)
                    .Add(Stat.Damage, 8f),

                // ---- Block abilities
                C("mending_guard", "Mending Guard", "Blocking heals 20 HP.", Rarity.Common, Defense)
                    .Effect("mending_guard"),
                C("barrier_cell", "Barrier Cell", "Blocking grants a 30 HP barrier for 4s.", Rarity.Uncommon, Defense)
                    .Effect("barrier_cell"),
                C("guard_burst", "Guard Burst", "Blocking fires a ring of 8 bullets.", Rarity.Uncommon, Utility)
                    .Effect("guard_burst"),
                C("second_guard", "Second Guard", "Kills instantly recharge your block.", Rarity.Common, Defense)
                    .Effect("second_guard"),
                C("snap_guard", "Snap Guard", "Block recharges twice as fast but lasts shorter.", Rarity.Common, Defense)
                    .Mul(Stat.BlockCooldown, 0.5f).Mul(Stat.BlockDuration, 0.6f),
                C("last_stand", "Last Stand", "Once per round, a lethal hit leaves you at 1 HP.", Rarity.Rare, Defense)
                    .Effect("last_stand").Stacks(1),
                C("iron_skin", "Iron Skin", "Take 20% less damage, move a little slower.", Rarity.Uncommon, Defense)
                    .Mul(Stat.DamageTaken, 0.8f).Mul(Stat.MoveSpeed, 0.9f),
                C("heavy_frame", "Heavy Frame", "+40 HP and slightly larger.", Rarity.Common, Defense)
                    .Add(Stat.MaxHealth, 40f).Mul(Stat.PlayerSize, 1.1f),

                // ---- Summons & auras
                C("sentry_drop", "Sentry Drop", "Blocking drops a turret that fires at enemies for 6s.", Rarity.Rare, Summon)
                    .Effect("sentry_drop").Mul(Stat.BlockCooldown, 1.1f),
                C("mine_layer", "Mine Layer", "Every 3s, drop a proximity mine (max 3).", Rarity.Rare, Summon)
                    .Effect("mine_layer"),
                C("orbit_cubes", "Orbit Cubes", "Two cubes circle you and destroy enemy bullets.", Rarity.Legendary, Summon)
                    .Effect("orbit_cubes").Stacks(1),
                C("rot_field", "Rot Field", "Enemies within 5m take 6 damage per second.", Rarity.Uncommon, Utility)
                    .Effect("decay_field"),
                C("field_medic", "Field Medic", "You and allies within 6m regenerate 5 HP per second.", Rarity.Uncommon, Utility)
                    .Effect("field_medic"),

                // ---- Mobility & handling
                C("recoil_jets", "Recoil Jets", "Shooting in mid-air pushes you away from your aim.", Rarity.Common, Mobility)
                    .Effect("recoil_jets").Mul(Stat.AirControl, 1.3f),
                C("sprinter", "Sprinter", "+40% move speed, -10% HP.", Rarity.Common, Mobility)
                    .Mul(Stat.MoveSpeed, 1.4f).Mul(Stat.MaxHealth, 0.9f),
                C("sky_hopper", "Sky Hopper", "+2 air jumps, slightly lower jumps.", Rarity.Uncommon, Mobility)
                    .Add(Stat.ExtraJumps, 2).Mul(Stat.JumpHeight, 0.85f),
                C("featherfall", "Featherfall", "Halved border and fall damage, better air control.", Rarity.Common, Mobility)
                    .Mul(Stat.HazardDamageTaken, 0.5f).Mul(Stat.AirControl, 1.3f),
                C("quick_hands", "Quick Hands", "Faster reloads and slightly faster firing.", Rarity.Common, Mobility)
                    .Mul(Stat.ReloadTime, 0.7f).Mul(Stat.AttackCooldown, 0.85f),
                C("deep_pockets", "Deep Pockets", "+8 ammo, slower reload.", Rarity.Common, Mobility)
                    .Add(Stat.Ammo, 8).Mul(Stat.ReloadTime, 1.3f),
            };

            foreach (var c in cards) CardRegistry.Register(c);
        }
    }
}
