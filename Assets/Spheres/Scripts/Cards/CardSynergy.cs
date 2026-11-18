using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// The glue that makes every card mesh with every other card, Rounds-style. There are no hand-made combo lists:
    /// card effects hook into shared events, and the rules here make sure those events fire everywhere, so pairs
    /// combine on their own. For example:
    /// - On-hit cards (poison, frost, stun, hush, ammo refund, frenzy...) trigger from ALL your damage: explosions,
    ///   zaps, shockwaves, thorns, mines, turret shots, reflected bullets. Explosive + Venom = poison bombs.
    /// - Bullets made from your bullets (splits, echoes, guard bursts, turret shots, reflected bullets) carry your
    ///   bullet cards: homing, bouncing, piercing, ghosting, fuses, boomerangs, growth...
    /// - Explosive bullets also burst on every bounce and every enemy they pierce.
    /// - Card abilities scale with your stats: zaps, shockwaves and mines hit harder with more Damage,
    ///   shockwaves and mines get bigger with Blast Radius.
    /// </summary>
    public static class CardSynergy
    {
        static int depth;

        /// <summary>
        /// Run the owner's on-hit card effects for damage that didn't come straight from a bullet. <paramref name="bullet"/>
        /// is the bullet behind it if there was one (e.g. the bullet that exploded), otherwise null.
        /// Guarded against chains (an on-hit effect that causes damage won't proc again).
        /// </summary>
        public static void Proc(Player owner, Player target, DamageInfo info, Bullet bullet = null)
        {
            if (owner == null || target == null || target == owner || depth > 0) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.NetClientMode) return;
            depth++;
            try
            {
                var t = target;
                owner.ForEachEffect(e => e.OnBulletHitPlayer(bullet, t, info));
            }
            finally { depth--; }
        }

        /// <summary>True while on-hit effects are running from a proc (effects can use this to avoid feedback loops).</summary>
        public static bool InProc => depth > 0;

        /// <summary>Your Damage stat relative to the default (1 = default). Card abilities multiply by this.</summary>
        public static float DamageScale(Player p) =>
            p == null ? 1f : Mathf.Max(0.2f, p.GetStat(Stat.Damage) / Mathf.Max(0.01f, StatInfo.Get(Stat.Damage).Base));

        /// <summary>1 + your Blast Radius stat / 3, so explosive builds make card blasts bigger.</summary>
        public static float RadiusScale(Player p) => p == null ? 1f : 1f + Mathf.Max(0f, p.GetStat(Stat.ExplosionRadius)) / 3f;

        /// <summary>
        /// A bullet made by a card (turret, guard burst, reflected shot) gets the owner's per-bullet cards:
        /// fuses, ghosting, boomerang, overcharge... Marked as an echo so it doesn't spawn more echoes.
        /// </summary>
        public static void ApplyFireEffects(Player owner, Bullet b)
        {
            if (owner == null || b == null || b.IsDead) return;
            b.Data[EchoShotEffect.EchoFlag] = true;
            owner.ForEachEffect(e => e.OnFire(b));
        }
    }
}
