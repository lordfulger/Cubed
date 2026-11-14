using UnityEngine;

namespace Cubed
{
    /// <summary>Effects used by the built-in cards. Each is also available to JSON cards by id.</summary>
    public static class BuiltInEffects
    {
        internal static void RegisterAll()
        {
            CardEffectRegistry.Register<PulseGuardEffect>("pulse_guard", "Blocking releases a damaging shockwave.");
            CardEffectRegistry.Register<MirrorGuardEffect>("mirror_guard", "Blocked bullets are reflected.");
            CardEffectRegistry.Register<CubeWallEffect>("cube_wall", "Blocking raises a temporary wall.");
            CardEffectRegistry.Register<PhaseStepEffect>("phase_step", "Blocking teleports you forward.");
            CardEffectRegistry.Register<FrostTipsEffect>("frost_tips", "Hits slow the target.");
            CardEffectRegistry.Register<VenomEffect>("venom", "Hits poison the target.");
            CardEffectRegistry.Register<ArcCoilEffect>("arc_coil", "Periodically zaps a nearby enemy.");
            CardEffectRegistry.Register<RicochetBoostEffect>("ricochet_boost", "Bounces increase bullet damage.");
            CardEffectRegistry.Register<SplitterEffect>("splitter", "Bullets split when hitting terrain.");
            CardEffectRegistry.Register<AdrenalineEffect>("adrenaline", "Faster when low on health.");
            CardEffectRegistry.Register<ScavengerEffect>("scavenger", "Hits refund ammo.");
            CardEffectRegistry.Register<ThornsEffect>("thorns", "Reflects part of bullet damage to the attacker.");
            CardEffectRegistry.Register<OverchargeEffect>("overcharge", "Every 4th shot is supercharged.");
        }
    }

    public class PulseGuardEffect : CardEffect
    {
        public float Radius = 5.5f, Damage = 18f, Force = 16f;

        public override void OnBlock()
        {
            Vector3 c = Player.Center;
            Fx.Pulse(c, new Color(Player.Color.r, Player.Color.g, Player.Color.b, 0.45f), 1f, Radius * 2f, 0.3f);
            foreach (var p in Player.All.ToArray())
            {
                if (p == Player || !p.IsAlive || p.Team == Player.Team) continue;
                Vector3 to = p.Center - c;
                if (to.magnitude > Radius) continue;
                p.TakeDamage(new DamageInfo
                {
                    Amount = Damage,
                    Source = Player,
                    Type = DamageType.Effect,
                    Point = p.Center,
                    Direction = (to.normalized + Vector3.up * 0.4f).normalized,
                    Knockback = Force,
                });
            }
        }
    }

    public class MirrorGuardEffect : CardEffect
    {
        public override bool TryReflectBullet(Bullet bullet)
        {
            Vector3 dir;
            if (bullet.Owner != null && bullet.Owner.IsAlive) dir = bullet.Owner.Center - bullet.Position;
            else dir = -bullet.Velocity;
            bullet.Reflect(Player, dir);
            return true;
        }
    }

    public class CubeWallEffect : CardEffect
    {
        public override void OnBlock()
        {
            var map = GameManager.ActiveMap;
            if (map == null) return;
            Vector3 fwd = Player.AimDirection;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Player.transform.forward;
            fwd.Normalize();
            Vector3 center = Player.transform.position + fwd * 2.2f + Vector3.up * 1.1f;
            var wall = map.SpawnTemporaryBlock(center, new Vector3(3f, 2.2f, 0.5f), Quaternion.LookRotation(fwd), "glass", 4f);
            wall.Hp = 80f;
            Fx.Burst(center, BlockTypes.Get("glass").Color, 8, 4f, 0.2f, 0.5f);
        }
    }

    public class PhaseStepEffect : CardEffect
    {
        public float Distance = 7f;

        public override void OnBlock()
        {
            Vector3 dir = Player.AimDirection;
            dir.y = Mathf.Clamp(dir.y, -0.2f, 0.5f);
            dir.Normalize();
            Vector3 from = Player.Center;
            float dist = Distance;
            if (Physics.SphereCast(from, Player.Radius * 0.9f, dir, out var hit, Distance, Bullet.CollisionMask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0f, hit.distance - 0.1f);
            Vector3 target = Player.transform.position + dir * dist;
            var map = GameManager.ActiveMap;
            if (map != null)
            {
                var b = map.CurrentBounds;
                float m = Player.Radius + 0.3f;
                target.x = Mathf.Clamp(target.x, b.min.x + m, b.max.x - m);
                target.z = Mathf.Clamp(target.z, b.min.z + m, b.max.z - m);
                target.y = Mathf.Clamp(target.y, b.min.y + 0.1f, b.max.y - 2f);
            }
            Fx.Burst(from, Player.Color, 10, 4f, 0.15f, 0.4f, 0f);
            Player.Teleport(target);
            Fx.Line(from, Player.Center, new Color(Player.Color.r, Player.Color.g, Player.Color.b, 0.7f), 0.3f, 0.25f);
            Fx.Burst(Player.Center, Player.Color, 10, 4f, 0.15f, 0.4f, 0f);
            if (Player.Velocity.y < 0f) Player.Velocity.y = 0f;
        }
    }

    public class FrostTipsEffect : CardEffect
    {
        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info)
        {
            target.ApplySlow(0.45f, 1.5f);
            Fx.Burst(target.Center, new Color(0.6f, 0.9f, 1f), 6, 3f, 0.12f, 0.5f, 2f);
        }
    }

    public class VenomEffect : CardEffect
    {
        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info) =>
            target.ApplyPoison(6f, 3f, Player);
    }

    public class ArcCoilEffect : CardEffect
    {
        public float Interval = 1.6f, Range = 8f, Damage = 10f;
        float timer;

        public override void OnRoundStart() => timer = Interval;

        public override void OnUpdate(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            timer = Interval;
            Player best = null;
            float bestDist = Range;
            foreach (var p in Player.All)
            {
                if (p == Player || !p.IsAlive || p.Team == Player.Team) continue;
                float d = (p.Center - Player.Center).magnitude;
                if (d >= bestDist) continue;
                if (Physics.Linecast(Player.Center, p.Center, out var hit, Bullet.CollisionMask, QueryTriggerInteraction.Ignore))
                {
                    var blocker = hit.collider.GetComponent<Player>();
                    if (blocker != p && blocker != Player) continue; // wall in the way
                }
                bestDist = d;
                best = p;
            }
            if (best == null) return;
            Fx.Zap(Player.Top, best.Center, new Color(0.75f, 0.6f, 1f, 0.9f));
            Sfx.Play("zap", 0.5f);
            best.TakeDamage(new DamageInfo
            {
                Amount = Damage,
                Source = Player,
                Type = DamageType.Effect,
                Point = best.Center,
                Direction = best.Center - Player.Center,
                Knockback = 2f,
            });
        }
    }

    public class RicochetBoostEffect : CardEffect
    {
        public override void OnBulletBounce(Bullet bullet, RaycastHit hit) => bullet.Damage *= 1.3f;
    }

    public class SplitterEffect : CardEffect
    {
        const string Flag = "cubed.split";

        public override void OnBulletHitTerrain(Bullet bullet, RaycastHit hit)
        {
            if (bullet.Data.ContainsKey(Flag)) return;
            bullet.Data[Flag] = true;
            Vector3 reflected = Vector3.Reflect(bullet.Velocity, hit.normal);
            Vector3 origin = hit.point + hit.normal * (bullet.Radius + 0.05f);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 v = Quaternion.AngleAxis(25f * i, hit.normal) * reflected * 0.8f;
                bullet.SpawnChild(origin, v, 0.5f);
            }
        }
    }

    public class AdrenalineEffect : CardEffect
    {
        bool Active => Player.Health < Player.Stats[Stat.MaxHealth] * 0.35f;

        public override void ModifyStat(Stat stat, ref float value)
        {
            if (!Active) return;
            if (stat == Stat.AttackCooldown) value *= 0.6f;
            else if (stat == Stat.MoveSpeed) value *= 1.25f;
            else if (stat == Stat.ReloadTime) value *= 0.7f;
        }
    }

    public class ScavengerEffect : CardEffect
    {
        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info) =>
            Player.Weapon.AddAmmo(1);
    }

    public class ThornsEffect : CardEffect
    {
        public override void OnDamaged(DamageInfo info)
        {
            if (info.Type != DamageType.Bullet || info.Source == null || info.Source == Player) return;
            info.Source.TakeDamage(new DamageInfo
            {
                Amount = info.Amount * 0.3f,
                Source = Player,
                Type = DamageType.Effect,
                Point = info.Source.Center,
                Blockable = false,
            });
        }
    }

    public class OverchargeEffect : CardEffect
    {
        int shots;
        int lastShot = -1;

        public override void OnRoundStart() { shots = 0; lastShot = -1; }

        public override void OnFire(Bullet bullet)
        {
            // OnFire runs per bullet; count trigger pulls instead.
            int shot = Player.Weapon.ShotsFired;
            if (shot != lastShot)
            {
                lastShot = shot;
                shots++;
            }
            if (shots % 4 != 0) return;
            bullet.Damage *= 2.5f;
            bullet.Radius *= 1.6f;
            bullet.Knockback *= 1.5f;
            bullet.Color = Color.Lerp(bullet.Color, new Color(1f, 0.95f, 0.4f), 0.7f);
            bullet.RefreshVisual();
        }
    }
}
