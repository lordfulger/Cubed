using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>Effects for the expansion card set (registered alongside the base effects).</summary>
    public static class ExpansionEffects
    {
        internal static void RegisterAll()
        {
            CardEffectRegistry.Register("echo_1", () => new EchoShotEffect { Extra = 1 }, "Each trigger pull fires a delayed echo shot.");
            CardEffectRegistry.Register("echo_2", () => new EchoShotEffect { Extra = 2 }, "Each trigger pull fires two delayed echo shots.");
            CardEffectRegistry.Register<RecoilJetsEffect>("recoil_jets", "Shooting in the air pushes you backwards.");
            CardEffectRegistry.Register<FullClipEffect>("full_clip", "The first shot of a full magazine hits much harder.");
            CardEffectRegistry.Register<RiposteEffect>("riposte", "After a successful block your next shot is empowered.");
            CardEffectRegistry.Register<GrowthRoundsEffect>("growth_rounds", "Bullets grow in size and damage as they fly.");
            CardEffectRegistry.Register<FuseShellsEffect>("fuse_shells", "Bullets detonate after a short fuse.");
            CardEffectRegistry.Register<GravitySlugEffect>("gravity_slug", "Bullets drag nearby enemies toward their path.");
            CardEffectRegistry.Register<ChainBounceEffect>("chain_bounce", "Bounces redirect toward the nearest visible enemy.");
            CardEffectRegistry.Register<GhostRoundsEffect>("ghost_rounds", "Bullets pass through one wall.");
            CardEffectRegistry.Register<BoomerangEffect>("boomerang", "Bullets curve back toward you.");
            CardEffectRegistry.Register<StunTipsEffect>("stun_tips", "Hits can briefly stun.");
            CardEffectRegistry.Register<HushRoundsEffect>("hush_rounds", "Hits lock the target's block for 2 seconds.");
            CardEffectRegistry.Register<MendingGuardEffect>("mending_guard", "Blocking heals you.");
            CardEffectRegistry.Register<BarrierCellEffect>("barrier_cell", "Blocking grants a temporary barrier.");
            CardEffectRegistry.Register<GuardBurstEffect>("guard_burst", "Blocking fires a ring of bullets.");
            CardEffectRegistry.Register<SentryDropEffect>("sentry_drop", "Blocking drops a temporary turret.");
            CardEffectRegistry.Register<MineLayerEffect>("mine_layer", "Periodically drop proximity mines.");
            CardEffectRegistry.Register<OrbitCubesEffect>("orbit_cubes", "Two cubes orbit you and destroy enemy bullets.");
            CardEffectRegistry.Register<LastStandEffect>("last_stand", "Once per round, survive a lethal hit.");
            CardEffectRegistry.Register<SecondGuardEffect>("second_guard", "Kills reset your block cooldown.");
            CardEffectRegistry.Register<TrophyEffect>("trophy", "Kills heal 40% of your max HP.");
            CardEffectRegistry.Register<ExecutionerEffect>("executioner", "+50% damage to targets below 35% HP.");
            CardEffectRegistry.Register<GiantSlayerEffect>("giant_slayer", "More damage against targets with more max HP than you.");
            CardEffectRegistry.Register<BackstabEffect>("backstab", "+60% damage when hitting from behind.");
            CardEffectRegistry.Register<CloseQuartersEffect>("close_quarters", "More damage up close, less far away.");
            CardEffectRegistry.Register<LongShotEffect>("long_shot", "Damage grows with distance travelled.");
            CardEffectRegistry.Register<BloodletterEffect>("bloodletter", "Damage grows as your health drops.");
            CardEffectRegistry.Register<MomentumEffect>("momentum", "Damage grows with your speed.");
            CardEffectRegistry.Register<SteadyHandsEffect>("steady_hands", "Standing still: perfect accuracy and +30% damage.");
            CardEffectRegistry.Register<FrenzyEffect>("frenzy", "Hits stack faster fire rate.");
            CardEffectRegistry.Register<DecayFieldEffect>("decay_field", "Nearby enemies take damage over time.");
            CardEffectRegistry.Register<FieldMedicEffect>("field_medic", "You and nearby allies regenerate.");
        }

        internal static Player NearestEnemy(Player self, Vector3 from, float range, bool requireSight)
        {
            Player best = null;
            float bestDist = range;
            foreach (var p in Player.All)
            {
                if (p == self || !p.IsAlive || (self != null && p.Team == self.Team)) continue;
                float d = Vector3.Distance(from, p.Center);
                if (d >= bestDist) continue;
                if (requireSight && Physics.Linecast(from, p.Center, out var hit, Bullet.CollisionMask, QueryTriggerInteraction.Ignore))
                {
                    var hp = hit.collider.GetComponent<Player>();
                    if (hp != p && hp != self) continue;
                }
                bestDist = d;
                best = p;
            }
            return best;
        }

        internal static Transform SummonParent => GameManager.ActiveMap != null ? GameManager.ActiveMap.transform : null;
    }

    /// <summary>Base for effects that act once per trigger pull (OnFire runs once per bullet).</summary>
    public abstract class PerShotEffect : CardEffect
    {
        int lastShot = -1;

        protected bool IsNewPull()
        {
            int s = Player.Weapon.ShotsFired;
            if (s == lastShot) return false;
            lastShot = s;
            return true;
        }

        public override void OnRoundStart() => lastShot = -1;
    }

    // ------------------------------------------------------------------ firing

    public class EchoShotEffect : PerShotEffect
    {
        public const string EchoFlag = "spheres.echo";
        public int Extra = 1;
        readonly List<float> pending = new List<float>();

        public override void OnRoundStart()
        {
            base.OnRoundStart();
            pending.Clear();
        }

        public override void OnFire(Bullet bullet)
        {
            if (bullet.Data.ContainsKey(EchoFlag) || !IsNewPull()) return;
            for (int i = 1; i <= Extra; i++) pending.Add(Time.time + 0.09f * i);
        }

        public override void OnUpdate(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i]) continue;
                pending.RemoveAt(i);
                Vector3 muzzle = Player.Muzzle.position;
                Vector3 dir = Player.Input != null ? Player.Input.AimPoint - muzzle : Player.AimDirection;
                if (dir.sqrMagnitude < 0.01f) dir = Player.AimDirection;
                dir = Quaternion.Euler(Random.insideUnitSphere * 2f) * dir.normalized;
                var echo = Bullet.Spawn(Player, muzzle, dir * Player.GetStat(Stat.BulletSpeed));
                echo.Damage *= 0.6f;
                echo.Data[EchoFlag] = true;
                Player.ForEachEffect(e => e.OnFire(echo));
                Sfx.Play("shoot", 0.25f);
            }
        }
    }

    public class RecoilJetsEffect : PerShotEffect
    {
        public override void OnFire(Bullet bullet)
        {
            if (!IsNewPull() || Player.Grounded) return;
            Player.Velocity += -Player.AimDirection * 7f + Vector3.up * 2f;
            Fx.Burst(Player.Center, new Color(1f, 0.7f, 0.3f), 4, 3f, 0.1f, 0.3f);
        }
    }

    public class FullClipEffect : PerShotEffect
    {
        bool boosted;

        public override void OnFire(Bullet bullet)
        {
            if (IsNewPull()) boosted = Player.Weapon.Ammo >= Player.Weapon.MaxAmmo;
            if (!boosted) return;
            bullet.Damage *= 1.8f;
            bullet.Color = Color.Lerp(bullet.Color, Color.white, 0.5f);
            bullet.RefreshVisual();
        }
    }

    public class RiposteEffect : PerShotEffect
    {
        bool armed, boosting;

        public override void OnRoundStart() { base.OnRoundStart(); armed = boosting = false; }
        public override void OnBlockedDamage(DamageInfo info) => armed = true;

        public override void OnFire(Bullet bullet)
        {
            if (IsNewPull())
            {
                boosting = armed;
                armed = false;
            }
            if (!boosting) return;
            bullet.Damage *= 1.8f;
            bullet.Knockback *= 1.5f;
            bullet.Color = Color.Lerp(bullet.Color, new Color(1f, 0.9f, 0.3f), 0.6f);
            bullet.RefreshVisual();
        }
    }

    // ------------------------------------------------------------------ bullets

    public class GrowthRoundsEffect : CardEffect
    {
        const string Key = "spheres.growth";

        public override void OnBulletUpdate(Bullet bullet, float dt)
        {
            float total = bullet.Data.TryGetValue(Key, out var v) ? (float)v : 1f;
            if (total >= 3f) return;
            float step = Mathf.Min(1f + dt * 1.1f, 3f / total);
            total *= step;
            bullet.Data[Key] = total;
            bullet.Damage *= step;
            bullet.SetRadius(bullet.Radius * step);
        }
    }

    public class FuseShellsEffect : CardEffect
    {
        const string Key = "spheres.fuse";

        public override void OnFire(Bullet bullet) => bullet.Data[Key] = true;

        public override void OnBulletUpdate(Bullet bullet, float dt)
        {
            if (bullet.Age < 0.7f || !bullet.Data.ContainsKey(Key)) return;
            bullet.ExplosionRadius = Mathf.Max(bullet.ExplosionRadius, 2.6f);
            bullet.Kill(true);
        }
    }

    public class GravitySlugEffect : CardEffect
    {
        public override void OnBulletUpdate(Bullet bullet, float dt)
        {
            foreach (var p in Player.All)
            {
                if (!p.IsAlive || p.Team == Player.Team) continue;
                Vector3 to = bullet.Position - p.Center;
                float d = to.magnitude;
                if (d > 4.5f || d < 0.2f) continue;
                p.Velocity += to / d * (22f * dt);
            }
        }
    }

    public class ChainBounceEffect : CardEffect
    {
        public override void OnBulletBounce(Bullet bullet, RaycastHit hit)
        {
            var target = ExpansionEffects.NearestEnemy(Player, bullet.Position, 30f, true);
            if (target == null) return;
            bullet.Velocity = (target.Center - bullet.Position).normalized * bullet.Velocity.magnitude;
        }
    }

    public class GhostRoundsEffect : CardEffect
    {
        public override void OnFire(Bullet bullet) => bullet.TerrainPierceLeft++;
    }

    public class BoomerangEffect : CardEffect
    {
        public override void OnFire(Bullet bullet)
        {
            bullet.Gravity = 0f;
            bullet.Lifetime = Mathf.Max(bullet.Lifetime, 2.5f);
        }

        public override void OnBulletUpdate(Bullet bullet, float dt)
        {
            if (bullet.Age < 0.4f || !Player.IsAlive) return;
            Vector3 to = Player.Center - bullet.Position;
            float speed = bullet.Velocity.magnitude;
            bullet.Velocity = Vector3.RotateTowards(bullet.Velocity, to.normalized * speed, 7f * dt, 0f);
            bullet.CanHitOwner = false;
            if (bullet.Age > 0.6f && to.magnitude < 1.2f) bullet.Kill(false);
        }
    }

    public class StunTipsEffect : CardEffect
    {
        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info)
        {
            if (Random.value < 0.3f) target.ApplyStun(0.5f);
        }
    }

    public class HushRoundsEffect : CardEffect
    {
        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info)
        {
            target.BlockCooldownTimer = Mathf.Max(target.BlockCooldownTimer, 2f);
            Fx.Burst(target.Top, new Color(0.6f, 0.6f, 0.7f), 4, 2f, 0.1f, 0.4f, -2f);
        }
    }

    // ------------------------------------------------------------------ block abilities

    public class MendingGuardEffect : CardEffect
    {
        public override void OnBlock()
        {
            Player.Heal(20f);
            Fx.Burst(Player.Center, new Color(0.4f, 1f, 0.5f), 8, 3f, 0.12f, 0.6f, -3f);
        }
    }

    public class BarrierCellEffect : CardEffect
    {
        public override void OnBlock()
        {
            Player.AddBarrier(30f, 4f);
            Fx.Pulse(Player.Center, new Color(0.85f, 0.95f, 1f, 0.5f), 1.5f, 2.6f, 0.3f);
        }
    }

    public class GuardBurstEffect : CardEffect
    {
        public override void OnBlock()
        {
            const int count = 8;
            float speed = Player.GetStat(Stat.BulletSpeed);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 360f / count, 0f) * Vector3.forward;
                var b = Bullet.Spawn(Player, Player.Center + dir * (Player.Radius + 0.4f), dir * speed);
                b.Damage *= 0.5f;
                CardSynergy.ApplyFireEffects(Player, b);
            }
            Sfx.Play("shoot", 0.4f);
        }
    }

    public class SentryDropEffect : CardEffect
    {
        public override void OnBlock()
        {
            Vector3 pos = Player.transform.position + Player.transform.forward * -0.8f;
            var go = new GameObject("Sentry");
            go.transform.SetParent(ExpansionEffects.SummonParent, true);
            go.transform.position = pos;
            var s = go.AddComponent<SentryTurret>();
            s.Owner = Player;
        }
    }

    /// <summary>Temporary turret that shoots the nearest visible enemy.</summary>
    public class SentryTurret : MonoBehaviour
    {
        public Player Owner;
        public float Life = 6f;
        public float FireInterval = 0.45f;
        float cooldown = 0.4f;

        void Start()
        {
            var c = Owner != null ? Owner.Color : Color.gray;
            Prim.Cube("Base", transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.7f), Mats.Solid(Color.Lerp(c, Color.black, 0.4f)));
            Prim.Cube("Head", transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.4f, 0.45f), Mats.Solid(c));
            if (Net.NetSession.Mirroring)
            {
                Net.NetSession.Instance.MirrorBox(transform.position + Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.7f, 0.6f, 0.7f), Color.Lerp(c, Color.black, 0.4f), Life);
                Net.NetSession.Instance.MirrorBox(transform.position + Vector3.up * 0.8f, Quaternion.identity, new Vector3(0.45f, 0.4f, 0.45f), c, Life);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Life -= dt;
            if (Life <= 0f || Owner == null)
            {
                Fx.Burst(transform.position + Vector3.up * 0.5f, Owner != null ? Owner.Color : Color.gray, 8, 4f, 0.15f, 0.5f);
                Destroy(gameObject);
                return;
            }
            cooldown -= dt;
            if (cooldown > 0f) return;
            Vector3 muzzle = transform.position + Vector3.up * 0.8f;
            var target = ExpansionEffects.NearestEnemy(Owner, muzzle, 22f, true);
            if (target == null) return;
            cooldown = FireInterval;
            var b = Bullet.Spawn(Owner, muzzle, (target.Center - muzzle).normalized * Owner.GetStat(Stat.BulletSpeed));
            b.Damage *= 0.35f;
            b.Gravity = 0f;
            b.BouncesLeft = 0;
            CardSynergy.ApplyFireEffects(Owner, b);
            Sfx.Play("shoot", 0.2f);
        }
    }

    public class MineLayerEffect : CardEffect
    {
        float timer = 3f;
        readonly List<ProximityMine> mines = new List<ProximityMine>();

        public override void OnRoundStart()
        {
            timer = 3f;
            mines.Clear();
        }

        public override void OnUpdate(float dt)
        {
            timer -= dt;
            if (timer > 0f || !Player.Grounded) return;
            timer = 3f;
            mines.RemoveAll(m => m == null);
            if (mines.Count >= 3) return;
            var go = new GameObject("Mine");
            go.transform.SetParent(ExpansionEffects.SummonParent, true);
            go.transform.position = Player.transform.position + Vector3.up * 0.05f;
            var mine = go.AddComponent<ProximityMine>();
            mine.Owner = Player;
            mines.Add(mine);
        }
    }

    public class ProximityMine : MonoBehaviour
    {
        public Player Owner;
        public float Life = 12f;
        float arm = 0.7f;

        void Start()
        {
            var c = Owner != null ? Owner.Color : Color.gray;
            Prim.Cube("Mine", transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.16f, 0.5f), Mats.Solid(Color.Lerp(c, Color.black, 0.3f)));
            Prim.Cube("Light", transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.14f, 0.06f, 0.14f), Mats.Solid(new Color(1f, 0.3f, 0.2f)));
            if (Net.NetSession.Mirroring)
                Net.NetSession.Instance.MirrorBox(transform.position + Vector3.up * 0.08f, Quaternion.identity, new Vector3(0.5f, 0.16f, 0.5f), Color.Lerp(c, Color.black, 0.3f), Life);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Life -= dt;
            arm -= dt;
            if (Life <= 0f || Owner == null) { Destroy(gameObject); return; }
            if (arm > 0f) return;
            foreach (var p in Player.All)
            {
                if (!p.IsAlive || p.Team == Owner.Team) continue;
                if (Vector3.Distance(p.transform.position, transform.position) > 1.8f) continue;
                Explosion.Create(transform.position + Vector3.up * 0.3f, 3.2f * CardSynergy.RadiusScale(Owner), 45f * CardSynergy.DamageScale(Owner), Owner, 12f + Owner.GetStat(Stat.Knockback) * 0.5f, Owner.Color);
                Destroy(gameObject);
                return;
            }
        }
    }

    public class OrbitCubesEffect : CardEffect
    {
        readonly Transform[] cubes = new Transform[2];
        float angle;

        public override void OnUpdate(float dt)
        {
            for (int i = 0; i < cubes.Length; i++)
                if (cubes[i] == null)
                    cubes[i] = Prim.Cube("Orbit", Player.Model, Vector3.zero, Vector3.one * 0.32f, Mats.Solid(Color.Lerp(Player.Color, Color.white, 0.4f))).transform;

            angle += dt * 200f;
            for (int i = 0; i < cubes.Length; i++)
            {
                float a = (angle + i * 180f) * Mathf.Deg2Rad;
                cubes[i].localPosition = new Vector3(Mathf.Cos(a) * 1.25f, 0.75f, Mathf.Sin(a) * 1.25f);
                cubes[i].localRotation = Quaternion.Euler(angle, angle * 0.7f, 0f);
                Vector3 c = cubes[i].position;
                foreach (var b in Bullet.Active.ToArray())
                {
                    if (b == null || b.Team == Player.Team) continue;
                    if ((b.Position - c).sqrMagnitude > 0.36f + b.Radius * b.Radius) continue;
                    Fx.Burst(b.Position, b.Color, 4, 3f, 0.08f, 0.2f);
                    b.Kill(false);
                }
            }
        }

        public override void OnRemoved()
        {
            foreach (var c in cubes) if (c != null) Object.Destroy(c.gameObject);
        }
    }

    public class LastStandEffect : CardEffect
    {
        bool used;

        public override void OnRoundStart() => used = false;

        public override void OnBeforeDamaged(DamageInfo info)
        {
            if (used || info.Amount < Player.Health || Player.LivesRemaining > 0) return;
            used = true;
            info.Amount = Mathf.Max(0f, Player.Health - 1f);
            Player.InvulnerableTimer = 1.2f;
            Fx.Pulse(Player.Center, new Color(1f, 0.4f, 0.3f, 0.6f), 1f, 4f, 0.4f);
            SpheresHud.AddFeedText(Player.DisplayName + " refuses to fall!");
        }
    }

    public class SecondGuardEffect : CardEffect
    {
        public override void OnKill(Player victim) => Player.BlockCooldownTimer = 0f;
    }

    public class TrophyEffect : CardEffect
    {
        public override void OnKill(Player victim)
        {
            Player.Heal(Player.Stats[Stat.MaxHealth] * 0.4f);
            Fx.Burst(Player.Center, new Color(0.4f, 1f, 0.5f), 10, 4f, 0.12f, 0.6f, -3f);
        }
    }

    // ------------------------------------------------------------------ conditional damage

    public class ExecutionerEffect : CardEffect
    {
        public override void OnBeforeDealDamage(Player target, DamageInfo info)
        {
            if (target.Health < target.Stats[Stat.MaxHealth] * 0.35f) info.Amount *= 1.5f;
        }
    }

    public class GiantSlayerEffect : CardEffect
    {
        public override void OnBeforeDealDamage(Player target, DamageInfo info)
        {
            float diff = target.Stats[Stat.MaxHealth] - Player.Stats[Stat.MaxHealth];
            if (diff > 0f) info.Amount *= 1f + Mathf.Clamp01(diff / 200f);
        }
    }

    public class BackstabEffect : CardEffect
    {
        public override void OnBeforeDealDamage(Player target, DamageInfo info)
        {
            if (info.Type != DamageType.Bullet || info.Direction.sqrMagnitude < 0.01f) return;
            Vector3 flat = new Vector3(info.Direction.x, 0f, info.Direction.z).normalized;
            if (Vector3.Dot(target.transform.forward, flat) > 0.4f) info.Amount *= 1.6f;
        }
    }

    public class CloseQuartersEffect : CardEffect
    {
        public override void OnBeforeDealDamage(Player target, DamageInfo info)
        {
            float d = Vector3.Distance(Player.Center, target.Center);
            if (d < 7f) info.Amount *= 1.35f;
            else if (d > 15f) info.Amount *= 0.8f;
        }
    }

    public class LongShotEffect : CardEffect
    {
        public override void OnBeforeDealDamage(Player target, DamageInfo info)
        {
            if (info.Bullet == null) return;
            float d = Vector3.Distance(info.Bullet.Position, info.Bullet.SpawnPosition);
            info.Amount *= 1f + Mathf.Min(d, 40f) / 40f * 0.8f;
        }
    }

    // ------------------------------------------------------------------ dynamic stats

    public class BloodletterEffect : CardEffect
    {
        public override void ModifyStat(Stat stat, ref float value)
        {
            if (stat != Stat.Damage) return;
            float max = Mathf.Max(1f, Player.Stats[Stat.MaxHealth]);
            value *= 1f + 0.8f * Mathf.Clamp01(1f - Player.Health / max);
        }
    }

    public class MomentumEffect : CardEffect
    {
        public override void ModifyStat(Stat stat, ref float value)
        {
            if (stat != Stat.Damage) return;
            float speed = new Vector3(Player.Velocity.x, 0f, Player.Velocity.z).magnitude;
            value *= 1f + Mathf.Min(speed, 20f) / 25f;
        }
    }

    public class SteadyHandsEffect : CardEffect
    {
        bool Steady => Player.Grounded && new Vector3(Player.Velocity.x, 0f, Player.Velocity.z).sqrMagnitude < 0.5f;

        public override void ModifyStat(Stat stat, ref float value)
        {
            if (!Steady) return;
            if (stat == Stat.Spread) value = 0f;
            else if (stat == Stat.Damage) value *= 1.3f;
        }
    }

    public class FrenzyEffect : CardEffect
    {
        int stacks;
        float timer;

        public override void OnRoundStart() { stacks = 0; timer = 0f; }

        public override void OnBulletHitPlayer(Bullet bullet, Player target, DamageInfo info)
        {
            stacks = Mathf.Min(5, stacks + 1);
            timer = 2.5f;
        }

        public override void OnUpdate(float dt)
        {
            if (timer <= 0f) return;
            timer -= dt;
            if (timer <= 0f) stacks = 0;
        }

        public override void ModifyStat(Stat stat, ref float value)
        {
            if (stat == Stat.AttackCooldown && stacks > 0) value /= 1f + 0.15f * stacks;
        }
    }

    // ------------------------------------------------------------------ auras

    public class DecayFieldEffect : CardEffect
    {
        float tick, pulse;

        public override void OnUpdate(float dt)
        {
            pulse -= dt;
            if (pulse <= 0f)
            {
                pulse = 1.2f;
                Fx.Pulse(Player.Center, new Color(0.6f, 0.2f, 0.8f, 0.18f), 1f, 10f, 0.8f);
            }
            tick -= dt;
            if (tick > 0f) return;
            tick = 0.5f;
            foreach (var p in Player.All.ToArray())
            {
                if (!p.IsAlive || p.Team == Player.Team) continue;
                if (Vector3.Distance(p.Center, Player.Center) > 5f) continue;
                p.TakeDamage(new DamageInfo { Amount = 3f, Source = Player, Type = DamageType.Effect, Point = p.Center, Blockable = false });
            }
        }
    }

    public class FieldMedicEffect : CardEffect
    {
        float tick, pulse;

        public override void OnUpdate(float dt)
        {
            pulse -= dt;
            if (pulse <= 0f)
            {
                pulse = 1.5f;
                Fx.Pulse(Player.Center, new Color(0.3f, 1f, 0.5f, 0.15f), 1f, 12f, 0.9f);
            }
            tick -= dt;
            if (tick > 0f) return;
            tick = 0.5f;
            foreach (var p in Player.All)
                if (p.IsAlive && p.Team == Player.Team && Vector3.Distance(p.Center, Player.Center) <= 6f)
                    p.Heal(2.5f);
        }
    }
}
