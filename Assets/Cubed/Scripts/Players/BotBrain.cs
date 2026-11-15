using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// AI controller. Picks a target, keeps a preferred range, strafes, avoids ledges and hazards,
    /// leads its shots (including bullet drop), blocks incoming bullets and blocks before border/fall hits.
    /// </summary>
    public class BotBrain : IPlayerInput
    {
        public struct Profile
        {
            public float AimErrorDeg, TurnSpeed, BlockChance, ReactionWindow, FireTolerance, LeadFactor, Aggression;
        }

        public static readonly Profile[] Profiles =
        {
            new Profile { AimErrorDeg = 7f, TurnSpeed = 160f, BlockChance = 0.15f, ReactionWindow = 0.12f, FireTolerance = 14f, LeadFactor = 0.3f, Aggression = 0.4f },
            new Profile { AimErrorDeg = 4f, TurnSpeed = 280f, BlockChance = 0.4f, ReactionWindow = 0.2f, FireTolerance = 9f, LeadFactor = 0.7f, Aggression = 0.6f },
            new Profile { AimErrorDeg = 2.2f, TurnSpeed = 480f, BlockChance = 0.7f, ReactionWindow = 0.26f, FireTolerance = 6f, LeadFactor = 0.9f, Aggression = 0.75f },
            new Profile { AimErrorDeg = 1f, TurnSpeed = 900f, BlockChance = 0.92f, ReactionWindow = 0.3f, FireTolerance = 4f, LeadFactor = 1f, Aggression = 0.9f },
        };

        public BotDifficulty Difficulty;
        public Profile P => Profiles[Mathf.Clamp((int)Difficulty, 0, Profiles.Length - 1)];

        public Vector3 Move { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool BlockPressed { get; private set; }
        public bool ReloadPressed { get; private set; }

        public Player Target { get; private set; }

        Vector3 aimDir = Vector3.forward;
        float retargetTimer, strafeTimer, strafeDir = 1f, jumpCooldown, envBlockCooldown, commitJumpTimer, losTimer;
        bool hasLos;
        readonly float noiseSeed = Random.value * 100f;
        readonly HashSet<Bullet> rolled = new HashSet<Bullet>();
        static readonly RaycastHit[] buffer = new RaycastHit[16];

        public BotBrain(BotDifficulty difficulty) { Difficulty = difficulty; }

        static MatchSettings Settings => GameManager.Instance != null ? GameManager.Instance.Settings : null;

        public void OnSpawn(Player player, Vector3 lookDirection)
        {
            aimDir = lookDirection.normalized;
            AimPoint = player.Head.position + aimDir * 20f;
            Target = null;
            retargetTimer = 0f;
            rolled.Clear();
        }

        public void Tick(Player self, float dt)
        {
            JumpPressed = false;
            BlockPressed = false;
            ReloadPressed = false;
            FireHeld = false;
            if (self.Frozen) { Move = Vector3.zero; return; }
            if (Difficulty == BotDifficulty.Dummy)
            {
                // Training targets: drift side to side and sometimes block, never shoot.
                float t = Time.time * 0.6f + self.Index;
                Move = new Vector3(Mathf.Sin(t), 0f, 0f) * 0.35f;
                AimPoint = self.Head.position + self.transform.forward * 10f;
                if (self.BlockCooldownTimer <= 0f && Random.value < 0.004f) BlockPressed = true;
                return;
            }

            var map = GameManager.ActiveMap;
            jumpCooldown -= dt;
            envBlockCooldown -= dt;
            commitJumpTimer -= dt;
            retargetTimer -= dt;
            strafeTimer -= dt;
            losTimer -= dt;

            if (retargetTimer <= 0f || Target == null || !Target.IsAlive)
            {
                retargetTimer = Random.Range(0.4f, 0.8f);
                Target = ChooseTarget(self);
            }
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(0.8f, 2.4f);
                strafeDir = Random.value < 0.5f ? -1f : 1f;
                if (Random.value < 0.15f) strafeDir = 0f;
            }

            UpdateMovement(self, map);
            UpdateAim(self, dt);
            UpdateBlocking(self, map);
            if (self.Weapon.Ammo <= 0) ReloadPressed = true;
        }

        Player ChooseTarget(Player self)
        {
            var s = Settings;
            Player best = null;
            float bestScore = float.MaxValue;
            foreach (var p in Player.All)
            {
                if (p == self || !p.IsAlive) continue;
                if (p.Team == self.Team) continue;
                float score = (p.Center - self.Center).magnitude;
                score *= Mathf.Lerp(0.6f, 1f, p.Health / Mathf.Max(1f, p.MaxHealth)); // prefer weak targets
                if (p == Target) score *= 0.8f; // stickiness
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        float PreferredRange(Player self)
        {
            bool closeRange = self.GetStatInt(Stat.BulletsPerShot) >= 3 || self.GetStat(Stat.Spread) > 14f;
            float baseRange = closeRange ? 7f : 13f;
            return baseRange * Mathf.Lerp(1.3f, 0.8f, P.Aggression);
        }

        // ------------------------------------------------------------------ movement

        void UpdateMovement(Player self, MapRuntime map)
        {
            Vector3 pos = self.transform.position;
            Vector3 move = Vector3.zero;
            bool wantJump = false;

            if (Target != null)
            {
                Vector3 to = Target.transform.position - pos;
                float height = to.y;
                to.y = 0f;
                float dist = to.magnitude;
                Vector3 toN = dist > 0.01f ? to / dist : self.transform.forward;
                float pref = PreferredRange(self);
                if (!hasLos) move += toN; // reposition to see them
                else if (dist > pref + 3f) move += toN;
                else if (dist < pref - 3f) move -= toN * 0.8f;
                move += Vector3.Cross(Vector3.up, toN) * strafeDir * 0.75f;
                if (height > 2.2f && dist < 14f) wantJump = true;
            }
            else if (map != null)
            {
                Vector3 toCenter = map.FullBounds.center - pos;
                toCenter.y = 0f;
                if (toCenter.magnitude > 4f) move = toCenter.normalized * 0.5f;
            }

            if (map != null)
            {
                Bounds b = map.CurrentBounds;
                const float margin = 3.5f;
                if (pos.x < b.min.x + margin) move.x += 1.5f;
                if (pos.x > b.max.x - margin) move.x -= 1.5f;
                if (pos.z < b.min.z + margin) move.z += 1.5f;
                if (pos.z > b.max.z - margin) move.z -= 1.5f;
            }

            if (move.sqrMagnitude > 1f) move.Normalize();

            if (self.Grounded)
            {
                if (commitJumpTimer <= 0f) move = AvoidEdges(self, move, ref wantJump);
                // Hop over low obstacles in our path.
                if (move.sqrMagnitude > 0.1f &&
                    Physics.Raycast(pos + Vector3.up * 0.4f * self.Size, move.normalized, out var obstacle, 1.1f * self.Size, Bullet.CollisionMask, QueryTriggerInteraction.Ignore) &&
                    obstacle.collider.GetComponent<Player>() == null)
                    wantJump = true;
                if (Random.value < 0.004f * (1f + P.Aggression)) wantJump = true;
            }
            else
            {
                // Airborne with nothing below: steer back toward ground and use air jumps.
                bool groundBelow = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, 40f, Bullet.CollisionMask, QueryTriggerInteraction.Ignore);
                if (!groundBelow && map != null && commitJumpTimer <= 0f)
                {
                    Vector3 home = NearestGround(self, map) - pos;
                    home.y = 0f;
                    if (home.sqrMagnitude > 0.25f) move = home.normalized;
                    if (self.Velocity.y < -3f && self.CanAirJump) wantJump = true;
                }
            }

            if (wantJump && jumpCooldown <= 0f)
            {
                JumpPressed = true;
                jumpCooldown = Random.Range(0.35f, 0.7f);
            }
            Move = move;
        }

        Vector3 AvoidEdges(Player self, Vector3 move, ref bool wantJump)
        {
            if (move.sqrMagnitude < 0.01f) return move;
            Vector3 dir = move.normalized;
            if (IsSafe(self, dir, 1.4f)) return move;

            // Gap ahead: if there's ground a jump away, commit to the jump.
            if (IsSafe(self, dir, 4.5f) && self.GetStat(Stat.JumpHeight) > 1.2f)
            {
                wantJump = true;
                commitJumpTimer = 0.8f;
                return move;
            }
            float[] angles = { 45f, -45f, 90f, -90f, 135f, -135f };
            foreach (var a in angles)
            {
                Vector3 d = Quaternion.Euler(0f, a, 0f) * dir;
                if (IsSafe(self, d, 1.4f)) return d * move.magnitude;
            }
            return Vector3.zero;
        }

        bool IsSafe(Player self, Vector3 dir, float distance)
        {
            Vector3 probe = self.transform.position + dir * distance + Vector3.up * 0.6f;
            var map = GameManager.ActiveMap;
            if (map != null)
            {
                var b = map.CurrentBounds;
                if (probe.x < b.min.x + 1.5f || probe.x > b.max.x - 1.5f || probe.z < b.min.z + 1.5f || probe.z > b.max.z - 1.5f) return false;
            }
            if (!Physics.Raycast(probe, Vector3.down, out var hit, 5f, Bullet.CollisionMask, QueryTriggerInteraction.Ignore)) return false;
            var info = hit.collider.GetComponent<BlockInfo>();
            if (info != null && info.Type != null && info.Type.DamagePerSecond > 0f) return false;
            return hit.collider.GetComponent<Player>() == null;
        }

        Vector3 NearestGround(Player self, MapRuntime map)
        {
            Vector3 pos = self.transform.position;
            Vector3 best = map.FullBounds.center;
            float bestDist = float.MaxValue;
            foreach (var block in map.Blocks)
            {
                if (block.Type != null && block.Type.DamagePerSecond > 0f) continue;
                var bounds = block.GetComponent<Collider>().bounds;
                if (bounds.max.y > pos.y + 4f) continue;
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                Vector3 closest = bounds.ClosestPoint(new Vector3(pos.x, bounds.max.y, pos.z));
                float d = (new Vector3(closest.x, 0f, closest.z) - new Vector3(pos.x, 0f, pos.z)).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Vector3.Lerp(closest, top, 0.3f);
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ aiming

        void UpdateAim(Player self, float dt)
        {
            Vector3 eye = self.Head.position;
            if (Target == null)
            {
                Vector3 flat = Move.sqrMagnitude > 0.01f ? Move : self.transform.forward;
                aimDir = Vector3.RotateTowards(aimDir, flat.normalized, P.TurnSpeed * Mathf.Deg2Rad * dt, 0f);
                AimPoint = eye + aimDir * 20f;
                return;
            }

            Vector3 muzzle = self.Muzzle.position;
            float speed = Mathf.Max(1f, self.GetStat(Stat.BulletSpeed));
            float gravity = self.GetStat(Stat.BulletGravity);
            Vector3 tp = Target.Center;
            Vector3 tv = Target.Velocity;
            if (Target.Grounded) tv.y = 0f;
            float t = (tp - muzzle).magnitude / speed;
            Vector3 predicted = tp;
            for (int i = 0; i < 2; i++)
            {
                predicted = tp + tv * t * P.LeadFactor;
                t = (predicted - muzzle).magnitude / speed;
            }
            predicted.y += 0.5f * gravity * t * t;

            float dist = (predicted - muzzle).magnitude;
            float time = Time.time * 0.9f + noiseSeed;
            Vector3 noise = new Vector3(Mathf.PerlinNoise(time, 0f) - 0.5f, Mathf.PerlinNoise(0f, time) - 0.5f, Mathf.PerlinNoise(time, time) - 0.5f) * 2f;
            predicted += noise * Mathf.Tan(P.AimErrorDeg * Mathf.Deg2Rad) * dist;

            Vector3 desired = (predicted - muzzle).normalized;
            aimDir = Vector3.RotateTowards(aimDir, desired, P.TurnSpeed * Mathf.Deg2Rad * dt, 0f);
            AimPoint = muzzle + aimDir * dist;

            if (losTimer <= 0f)
            {
                losTimer = 0.15f;
                hasLos = HasLineOfSight(self, eye, Target);
            }
            float angle = Vector3.Angle(aimDir, desired);
            if (hasLos && angle < P.FireTolerance && dist < 70f) FireHeld = true;
        }

        static bool HasLineOfSight(Player self, Vector3 from, Player target)
        {
            Vector3 to = target.Center - from;
            float dist = to.magnitude;
            int n = Physics.RaycastNonAlloc(new Ray(from, to / dist), buffer, dist, Bullet.CollisionMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Player nearestPlayer = null;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                var p = h.collider.GetComponent<Player>();
                if (p == self) continue;
                var block = h.collider.GetComponent<BlockInfo>();
                if (block != null && block.Type != null && block.Type.Transparent) continue; // shoot through glass
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    nearestPlayer = p;
                    any = true;
                }
            }
            return !any || nearestPlayer == target;
        }

        // ------------------------------------------------------------------ blocking

        void UpdateBlocking(Player self, MapRuntime map)
        {
            if (self.BlockCooldownTimer > 0f) return;

            // Incoming bullets.
            Vector3 c = self.Center;
            float hitRadius = self.Radius + 0.6f;
            foreach (var b in Bullet.Active)
            {
                if (b == null) continue;
                if (b.Owner == self && !b.CanHitOwner) continue;
                if (b.Owner != self && b.Team == self.Team && (Settings == null || !Settings.friendlyFire)) continue;
                Vector3 r = c - b.Position;
                Vector3 v = b.Velocity;
                float vv = v.sqrMagnitude;
                if (vv < 1f) continue;
                float tStar = Vector3.Dot(r, v) / vv;
                if (tStar < 0f || tStar > P.ReactionWindow) continue;
                float miss = (r - v * tStar).magnitude;
                if (miss > hitRadius + b.Radius + b.ExplosionRadius * 0.6f) continue;
                if (rolled.Add(b) && Random.value < P.BlockChance)
                {
                    BlockPressed = true;
                    return;
                }
            }
            if (rolled.Count > 64) rolled.RemoveWhere(x => x == null);

            // Environmental hazards: bottom border, damaging walls, hard landings.
            if (map == null || envBlockCooldown > 0f) return;
            var s = Settings;
            Vector3 pos = self.transform.position;
            Vector3 vel = self.Velocity;
            Bounds bounds = map.CurrentBounds;
            bool danger = false;
            const float look = 0.14f;
            if (s == null || s.fallDamage)
            {
                if (vel.y < -2f && pos.y + vel.y * look < bounds.min.y + 0.2f) danger = true;
                if (s == null || s.landingDamage)
                    if (vel.y < -Player.SafeLandingSpeed && Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, -vel.y * look + 0.3f, Bullet.CollisionMask, QueryTriggerInteraction.Ignore))
                        danger = true;
            }
            if (s == null || s.damagingBorders)
            {
                Vector3 next = self.Center + vel * look;
                float r = self.Radius;
                if (next.x - r < bounds.min.x || next.x + r > bounds.max.x || next.z - r < bounds.min.z || next.z + r > bounds.max.z || next.y + r > bounds.max.y)
                    danger = true;
            }
            if (danger)
            {
                envBlockCooldown = 0.6f;
                if (Random.value < Mathf.Clamp01(P.BlockChance + 0.15f)) BlockPressed = true;
            }
        }

        // ------------------------------------------------------------------ card picks

        /// <summary>Score cards and pick one. Prefers rarer cards and avoids fragile ones when low on HP stats.</summary>
        public static int ChooseCard(Player self, IList<CardDefinition> hand)
        {
            if (hand == null || hand.Count == 0) return -1;
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                float score = (int)c.Rarity * 1.2f + c.Effects.Count * 0.6f + Random.Range(0f, 2.5f);
                foreach (var m in c.Modifiers)
                {
                    StatInfo.Format(m, out int polarity);
                    score += polarity * 0.35f;
                    if (m.Stat == Stat.MaxHealth && m.Op == StatOp.Multiply && m.Value < 1f && self != null && self.MaxHealth < 80f) score -= 2f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}
