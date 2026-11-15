using UnityEngine;

namespace Cubed
{
    /// <summary>The base card set. Ids are prefixed with "cubed." so mods can override or remove them.</summary>
    public static class BuiltInCards
    {
        static readonly Color Offense = new Color(0.95f, 0.38f, 0.3f);
        static readonly Color Defense = new Color(0.3f, 0.65f, 1f);
        static readonly Color Mobility = new Color(0.4f, 0.9f, 0.55f);
        static readonly Color Bullets = new Color(1f, 0.75f, 0.25f);
        static readonly Color Utility = new Color(0.75f, 0.5f, 1f);

        static CardDefinition C(string id, string title, string desc, Rarity r, Color c) =>
            new CardDefinition("cubed." + id, title, desc, r, c);

        internal static void RegisterAll()
        {
            var cards = new[]
            {
                // ---- Offense -------------------------------------------------------------
                C("overclock", "Overclock", "Shoot faster, hit a little softer.", Rarity.Common, Offense)
                    .Mul(Stat.AttackCooldown, 0.6f).Mul(Stat.Damage, 0.85f),
                C("heavy_slug", "Heavy Slug", "Big, slow, painful.", Rarity.Common, Offense)
                    .Mul(Stat.Damage, 1.7f).Mul(Stat.BulletSpeed, 0.8f).Mul(Stat.AttackCooldown, 1.25f),
                C("scatter_core", "Scatter Core", "Fire a fan of pellets.", Rarity.Uncommon, Offense)
                    .Add(Stat.BulletsPerShot, 2).Add(Stat.Spread, 10f).Mul(Stat.Damage, 0.6f),
                C("brittle_fury", "Brittle Fury", "Double damage. Half the health.", Rarity.Rare, Offense)
                    .Mul(Stat.Damage, 2f).Mul(Stat.MaxHealth, 0.5f),
                C("hosepipe", "Hosepipe", "A wall of lead with no aim to speak of.", Rarity.Uncommon, Offense)
                    .Mul(Stat.AttackCooldown, 0.35f).Add(Stat.Spread, 9f).Mul(Stat.Damage, 0.55f).Add(Stat.Ammo, 6),
                C("long_glass", "Long Glass", "Precise, punchy, slow to fire.", Rarity.Uncommon, Offense)
                    .Mul(Stat.Damage, 1.5f).Mul(Stat.BulletSpeed, 1.6f).Set(Stat.Spread, 0f).Mul(Stat.AttackCooldown, 1.6f).Mul(Stat.BulletGravity, 0.3f),
                C("pusher", "Pusher", "Hits shove enemies around. Great near borders.", Rarity.Common, Offense)
                    .Mul(Stat.Knockback, 2.6f),
                C("overcharge", "Overcharge", "Every 4th shot is supercharged (x2.5 damage).", Rarity.Uncommon, Offense)
                    .Effect("overcharge"),
                C("adrenaline", "Adrenaline", "Below 35% HP you fire and move much faster.", Rarity.Uncommon, Offense)
                    .Effect("adrenaline"),

                // ---- Bullets -------------------------------------------------------------
                C("rubber_rounds", "Rubber Rounds", "Bullets bounce off walls.", Rarity.Common, Bullets)
                    .Add(Stat.Bounces, 3).Mul(Stat.Damage, 0.9f),
                C("rail_charge", "Rail Charge", "Very fast, nearly flat bullets.", Rarity.Common, Bullets)
                    .Mul(Stat.BulletSpeed, 2f).Mul(Stat.BulletGravity, 0.2f),
                C("lobber", "Lobber", "Heavy arcing shells that burst on impact.", Rarity.Uncommon, Bullets)
                    .Mul(Stat.BulletGravity, 2.5f).Mul(Stat.Damage, 1.3f).Add(Stat.ExplosionRadius, 1.5f).Mul(Stat.BulletSpeed, 0.8f),
                C("blast_shells", "Blast Shells", "Bullets explode.", Rarity.Uncommon, Bullets)
                    .Add(Stat.ExplosionRadius, 2.2f).Mul(Stat.Damage, 0.85f),
                C("giant_rounds", "Giant Rounds", "Bigger bullets are easier to land.", Rarity.Common, Bullets)
                    .Mul(Stat.BulletSize, 2.2f).Mul(Stat.Damage, 1.2f).Mul(Stat.BulletSpeed, 0.85f),
                C("seeker_chip", "Seeker Chip", "Bullets curve toward enemies.", Rarity.Rare, Bullets)
                    .Add(Stat.Homing, 1.2f).Mul(Stat.BulletSpeed, 0.85f),
                C("drill_bits", "Drill Bits", "Bullets pass through two players.", Rarity.Uncommon, Bullets)
                    .Add(Stat.Pierce, 2),
                C("rebound_engine", "Rebound Engine", "+1 bounce. Each bounce adds 30% damage.", Rarity.Uncommon, Bullets)
                    .Add(Stat.Bounces, 1).Effect("rebound_boost"),
                C("splitter", "Splitter", "Bullets split in two when they hit terrain.", Rarity.Rare, Bullets)
                    .Effect("splitter"),
                C("frost_tips", "Frost Tips", "Hits slow the target for 1.5s.", Rarity.Uncommon, Bullets)
                    .Effect("frost_tips"),
                C("venom", "Venom", "Hits poison the target (18 damage over 3s).", Rarity.Uncommon, Bullets)
                    .Effect("venom"),

                // ---- Defense -------------------------------------------------------------
                C("thick_plating", "Thick Plating", "+60 HP, a little slower.", Rarity.Common, Defense)
                    .Add(Stat.MaxHealth, 60f).Mul(Stat.MoveSpeed, 0.92f),
                C("mender", "Mender", "Regenerate 6 HP per second.", Rarity.Common, Defense)
                    .Add(Stat.Regen, 6f),
                C("vampire_coil", "Vampire Coil", "Heal for 35% of damage dealt.", Rarity.Uncommon, Defense)
                    .Add(Stat.Lifesteal, 0.35f),
                C("second_wind", "Second Wind", "Revive once per round.", Rarity.Rare, Defense)
                    .Add(Stat.ExtraLives, 1).Mul(Stat.MaxHealth, 0.8f).Stacks(3),
                C("fortress", "Fortress", "Blocks last twice as long and recharge faster.", Rarity.Common, Defense)
                    .Mul(Stat.BlockDuration, 2f).Mul(Stat.BlockCooldown, 0.85f),
                C("quick_guard", "Quick Guard", "Block recharges 40% faster.", Rarity.Common, Defense)
                    .Mul(Stat.BlockCooldown, 0.6f),
                C("safety_net", "Safety Net", "Take 75% less border, fall and hazard damage.", Rarity.Common, Defense)
                    .Mul(Stat.HazardDamageTaken, 0.25f).Stacks(1),
                C("juggernaut", "Juggernaut", "Huge, tough and slow.", Rarity.Uncommon, Defense)
                    .Mul(Stat.PlayerSize, 1.3f).Add(Stat.MaxHealth, 110f).Mul(Stat.MoveSpeed, 0.85f),
                C("thorns", "Thorns", "Attackers take 30% of the bullet damage they deal to you.", Rarity.Uncommon, Defense)
                    .Effect("thorns"),

                // ---- Blocks with abilities -----------------------------------------------
                C("pulse_guard", "Pulse Guard", "Blocking releases a shockwave that damages and pushes enemies.", Rarity.Uncommon, Utility)
                    .Effect("pulse_guard").Mul(Stat.BlockCooldown, 0.9f),
                C("mirror_guard", "Mirror Guard", "Blocked bullets are reflected back.", Rarity.Rare, Utility)
                    .Effect("mirror_guard"),
                C("cube_wall", "Cube Wall", "Blocking raises a temporary wall in front of you.", Rarity.Uncommon, Utility)
                    .Effect("cube_wall"),
                C("phase_step", "Phase Step", "Blocking blinks you forward.", Rarity.Uncommon, Utility)
                    .Effect("phase_step").Mul(Stat.BlockCooldown, 0.9f),
                C("arc_coil", "Arc Coil", "Periodically zaps the nearest enemy within 8m.", Rarity.Rare, Utility)
                    .Effect("arc_coil"),
                C("recycler", "Recycler", "Hitting an enemy refunds one bullet.", Rarity.Common, Utility)
                    .Effect("ammo_refund"),
                C("collector", "Collector", "See one extra card in every hand.", Rarity.Uncommon, Utility)
                    .Add(Stat.HandSize, 1).Stacks(2),

                // ---- Mobility / Ammo -----------------------------------------------------
                C("featherweight", "Featherweight", "Faster and bouncier, but fragile.", Rarity.Common, Mobility)
                    .Mul(Stat.MoveSpeed, 1.25f).Mul(Stat.JumpHeight, 1.15f).Mul(Stat.MaxHealth, 0.85f),
                C("double_hop", "Double Hop", "+1 air jump.", Rarity.Common, Mobility)
                    .Add(Stat.ExtraJumps, 1),
                C("spring_soles", "Spring Soles", "Jump much higher with better air control.", Rarity.Common, Mobility)
                    .Mul(Stat.JumpHeight, 1.4f).Mul(Stat.AirControl, 1.4f),
                C("compact", "Compact", "Smaller and harder to hit.", Rarity.Uncommon, Mobility)
                    .Mul(Stat.PlayerSize, 0.75f).Mul(Stat.MaxHealth, 0.9f).Stacks(2),
                C("big_mag", "Big Mag", "+4 ammo.", Rarity.Common, Mobility)
                    .Add(Stat.Ammo, 4).Mul(Stat.ReloadTime, 1.1f),
                C("speed_loader", "Speed Loader", "Reload twice as fast.", Rarity.Common, Mobility)
                    .Mul(Stat.ReloadTime, 0.5f),
            };

            foreach (var c in cards) CardRegistry.Register(c);
            ExpansionCards.RegisterAll();
        }
    }
}
