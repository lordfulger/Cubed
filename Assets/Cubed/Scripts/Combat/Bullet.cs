using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// A simulated projectile (no rigidbody). Uses sphere casts each frame so fast bullets never tunnel.
    /// All fields are public so card effects and mods can tweak bullets in flight.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        public static readonly List<Bullet> Active = new List<Bullet>();
        /// <summary>Layer mask for bullet collision (everything except Ignore Raycast).</summary>
        public static int CollisionMask = ~(1 << 2);

        public Player Owner;
        public int Team;
        public Vector3 Velocity;
        public float Gravity;
        public float Damage;
        public float Radius;
        public float Knockback;
        public float Homing;
        public float ExplosionRadius;
        public float Lifetime;
        public int BouncesLeft;
        public int PierceLeft;
        public int BounceCount;
        /// <summary>Owner can be hit by their own bullet after it bounces.</summary>
        public bool CanHitOwner;
        public Color Color;
        public readonly HashSet<Player> AlreadyHit = new HashSet<Player>();
        /// <summary>Free storage for effects/mods (e.g. "split" flags).</summary>
        public readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public Vector3 Position => transform.position;

        static readonly RaycastHit[] hitBuffer = new RaycastHit[24];
        static readonly Collider[] overlapBuffer = new Collider[8];
        Transform visual;
        bool dead, checkedSpawnOverlap;

        public static Bullet Spawn(Player owner, Vector3 position, Vector3 velocity)
        {
            var go = new GameObject("Bullet");
            go.transform.position = position;
            var b = go.AddComponent<Bullet>();
            b.Owner = owner;
            b.Team = owner != null ? owner.Team : -1;
            b.Velocity = velocity;
            if (owner != null)
            {
                b.Gravity = owner.GetStat(Stat.BulletGravity);
                b.Damage = owner.GetStat(Stat.Damage);
                b.Radius = owner.GetStat(Stat.BulletSize);
                b.Knockback = owner.GetStat(Stat.Knockback);
                b.Homing = owner.GetStat(Stat.Homing);
                b.ExplosionRadius = owner.GetStat(Stat.ExplosionRadius);
                b.Lifetime = owner.GetStat(Stat.BulletLifetime);
                b.BouncesLeft = owner.GetStatInt(Stat.Bounces);
                b.PierceLeft = owner.GetStatInt(Stat.Pierce);
                b.Color = Color.Lerp(owner.Color, Color.white, 0.35f);
            }
            else
            {
                b.Damage = 20f;
                b.Radius = 0.12f;
                b.Lifetime = 4f;
                b.Color = Color.white;
            }
            b.BuildVisual();
            return b;
        }

        /// <summary>Spawn a copy of this bullet (used by splitting effects).</summary>
        public Bullet SpawnChild(Vector3 position, Vector3 velocity, float damageScale)
        {
            var b = Spawn(Owner, position, velocity);
            b.Team = Team;
            b.Gravity = Gravity;
            b.Damage = Damage * damageScale;
            b.Radius = Radius * 0.8f;
            b.Knockback = Knockback * damageScale;
            b.Homing = Homing;
            b.ExplosionRadius = ExplosionRadius * 0.6f;
            b.Lifetime = Mathf.Min(Lifetime, 1.5f);
            b.BouncesLeft = 0;
            b.PierceLeft = 0;
            b.CanHitOwner = CanHitOwner;
            b.Color = Color;
            b.BuildVisual();
            foreach (var kv in Data) b.Data[kv.Key] = kv.Value;
            return b;
        }

        void BuildVisual()
        {
            if (visual != null) Destroy(visual.gameObject);
            visual = Prim.Sphere("Visual", transform, Vector3.zero, Mathf.Max(0.08f, Radius * 2f), Mats.Solid(Color)).transform;
            var trail = gameObject.GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = Mathf.Max(0.06f, Radius * 1.6f);
            trail.endWidth = 0f;
            trail.sharedMaterial = Mats.Transparent(Color.white);
            var c = Color;
            trail.startColor = new Color(c.r, c.g, c.b, 0.7f);
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Refresh visuals after changing Radius or Color.</summary>
        public void RefreshVisual() => BuildVisual();

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public static void ClearAll()
        {
            foreach (var b in Active.ToArray())
            {
                if (b == null) continue;
                b.dead = true;
                b.gameObject.SetActive(false);
                Destroy(b.gameObject);
            }
            Active.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || dead) return;

            Lifetime -= dt;
            if (Lifetime <= 0f)
            {
                Kill(ExplosionRadius > 0f);
                return;
            }

            if (Homing > 0f) ApplyHoming(dt);
            Velocity += Vector3.down * (Gravity * dt);

            Vector3 pos = transform.position;
            if (!checkedSpawnOverlap)
            {
                // Sphere casts ignore colliders they start inside, so handle point-blank shots explicitly.
                checkedSpawnOverlap = true;
                int n = Physics.OverlapSphereNonAlloc(pos, Radius, overlapBuffer, CollisionMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n && !dead; i++)
                {
                    var p = overlapBuffer[i].GetComponent<Player>();
                    if (p == null || !p.IsAlive || (p == Owner && !CanHitOwner) || AlreadyHit.Contains(p)) continue;
                    var fake = new RaycastHit { point = pos, normal = -Velocity.normalized };
                    HandlePlayerHit(p, fake, ref pos);
                }
                if (dead) return;
            }
            float remaining = Velocity.magnitude * dt;
            int iterations = 0;
            while (remaining > 0.0001f && iterations++ < 5 && !dead)
            {
                Vector3 dir = Velocity.normalized;
                if (!FindHit(pos, dir, remaining, out var hit, out var hitPlayer))
                {
                    pos += dir * remaining;
                    break;
                }
                float travelled = Mathf.Max(0f, hit.distance);
                pos += dir * travelled;
                remaining -= travelled;
                transform.position = pos;

                if (hitPlayer != null) HandlePlayerHit(hitPlayer, hit, ref pos);
                else HandleTerrainHit(hit, ref pos);
            }

            if (dead) return;
            transform.position = pos;
            if (Velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(Velocity);

            var map = GameManager.ActiveMap;
            if (map != null)
            {
                var b = map.FullBounds;
                b.Expand(12f);
                if (!b.Contains(pos)) Kill(false);
            }
        }

        bool FindHit(Vector3 origin, Vector3 dir, float distance, out RaycastHit best, out Player bestPlayer)
        {
            best = default;
            bestPlayer = null;
            int n = Physics.SphereCastNonAlloc(origin, Radius, dir, hitBuffer, distance, CollisionMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hitBuffer[i];
                var col = h.collider;
                if (col == null) continue;
                var p = col.GetComponent<Player>();
                if (p != null)
                {
                    if (!p.IsAlive) continue;
                    if (p == Owner && !CanHitOwner) continue;
                    if (AlreadyHit.Contains(p)) continue;
                }
                if (h.distance <= 0f && h.point == Vector3.zero)
                {
                    // Started overlapping: treat as an immediate hit facing back along the path.
                    h.point = origin;
                    h.normal = -dir;
                }
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                    bestPlayer = p;
                    found = true;
                }
            }
            return found;
        }

        void HandlePlayerHit(Player target, RaycastHit hit, ref Vector3 pos)
        {
            if (target.IsBlocking)
            {
                bool keep = target.BlockBullet(this);
                if (!keep) Kill(false);
                return;
            }

            AlreadyHit.Add(target);
            var info = new DamageInfo
            {
                Amount = Damage,
                Source = Owner,
                Type = DamageType.Bullet,
                Point = hit.point,
                Direction = (Velocity.normalized + Vector3.up * 0.2f).normalized,
                Knockback = Knockback,
                Bullet = this,
            };
            bool dealt = target.TakeDamage(info);
            if (dealt && Owner != null)
            {
                var t = target;
                Owner.ForEachEffect(e => e.OnBulletHitPlayer(this, t, info));
            }
            Fx.Burst(hit.point, Color, 5, 5f, 0.1f, 0.3f);

            if (PierceLeft > 0)
            {
                PierceLeft--;
                pos += Velocity.normalized * 0.05f;
                return;
            }
            Kill(ExplosionRadius > 0f);
        }

        void HandleTerrainHit(RaycastHit hit, ref Vector3 pos)
        {
            var block = hit.collider.GetComponent<BlockInfo>();
            if (Owner != null) Owner.ForEachEffect(e => e.OnBulletHitTerrain(this, hit));
            if (dead) return;
            if (block != null) block.Damage(Damage);

            if (BouncesLeft > 0)
            {
                BouncesLeft--;
                BounceCount++;
                CanHitOwner = true;
                AlreadyHit.Clear();
                Velocity = Vector3.Reflect(Velocity, hit.normal);
                pos = hit.point + hit.normal * (Radius + 0.02f);
                transform.position = pos;
                if (Owner != null) Owner.ForEachEffect(e => e.OnBulletBounce(this, hit));
                Fx.Burst(hit.point, Color, 3, 3f, 0.08f, 0.2f);
                if (ExplosionRadius > 0f) Explosion.Create(hit.point + hit.normal * 0.1f, ExplosionRadius * 0.6f, Damage * 0.4f, Owner, Knockback, Color);
                return;
            }

            Fx.Burst(hit.point, Color, 4, 4f, 0.09f, 0.3f);
            pos = hit.point + hit.normal * 0.05f;
            transform.position = pos;
            Kill(ExplosionRadius > 0f);
        }

        void ApplyHoming(float dt)
        {
            Player best = null;
            float bestScore = float.MaxValue;
            Vector3 dir = Velocity.normalized;
            foreach (var p in Player.All)
            {
                if (!p.IsAlive || p.Team == Team) continue;
                Vector3 to = p.Center - transform.position;
                float dist = to.magnitude;
                if (dist > 35f || dist < 0.01f) continue;
                float dot = Vector3.Dot(dir, to / dist);
                if (dot < 0.2f) continue;
                float score = dist * (2f - dot);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            if (best == null) return;
            float speed = Velocity.magnitude;
            Vector3 desired = (best.Center - transform.position).normalized * speed;
            Velocity = Vector3.RotateTowards(Velocity, desired, Homing * 2.2f * dt, 0f);
        }

        /// <summary>Turns the bullet around and hands it to a new owner (used by Mirror Guard).</summary>
        public void Reflect(Player newOwner, Vector3 newDirection)
        {
            Owner = newOwner;
            Team = newOwner != null ? newOwner.Team : -1;
            Velocity = newDirection.normalized * Velocity.magnitude;
            CanHitOwner = false;
            AlreadyHit.Clear();
            Lifetime = Mathf.Max(Lifetime, 2f);
            if (newOwner != null)
            {
                Color = Color.Lerp(newOwner.Color, Color.white, 0.35f);
                BuildVisual();
            }
            transform.position += Velocity.normalized * (Radius + 0.6f);
        }

        public void Kill(bool explode)
        {
            if (dead) return;
            dead = true;
            if (explode && ExplosionRadius > 0f)
                Explosion.Create(transform.position, ExplosionRadius, Damage, Owner, Knockback, Color);
            Destroy(gameObject);
        }
    }
}
