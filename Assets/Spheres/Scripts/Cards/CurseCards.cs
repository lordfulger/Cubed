using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Small handicaps handed to a runaway leader when "catch-up curses" is on. Never drawn into hands
    /// (CanAppear is false); tagged "curse" so the game and mods can find them.
    /// </summary>
    public static class CurseCards
    {
        static readonly Color CurseColor = new Color(0.55f, 0.25f, 0.7f);

        static CardDefinition Curse(string id, string title, string desc) =>
            new CardDefinition("curse_" + id, title, desc, Rarity.Common, CurseColor) { Source = "Curse", CanAppear = _ => false }.Tag("curse");

        public static void RegisterAll()
        {
            CardRegistry.Register(Curse("heavy_heart", "Heavy Heart", "Cursed for leading. -12% move speed.").Mul(Stat.MoveSpeed, 0.88f));
            CardRegistry.Register(Curse("glass_bones", "Glass Bones", "Cursed for leading. -15% max HP.").Mul(Stat.MaxHealth, 0.85f));
            CardRegistry.Register(Curse("butterfingers", "Butterfingers", "Cursed for leading. +25% reload time.").Mul(Stat.ReloadTime, 1.25f));
            CardRegistry.Register(Curse("dull_rounds", "Dull Rounds", "Cursed for leading. -12% damage.").Mul(Stat.Damage, 0.88f));
            CardRegistry.Register(Curse("slow_shield", "Slow Shield", "Cursed for leading. +30% block cooldown.").Mul(Stat.BlockCooldown, 1.3f));
            CardRegistry.Register(Curse("short_mag", "Short Magazine", "Cursed for leading. -1 ammo.").Add(Stat.Ammo, -1f));
            CardRegistry.Register(Curse("big_target", "Big Target", "Cursed for leading. +12% size.").Mul(Stat.PlayerSize, 1.12f));
            CardRegistry.Register(Curse("wobbly_aim", "Wobbly Aim", "Cursed for leading. +3° spread.").Add(Stat.Spread, 3f));
        }
    }
}
