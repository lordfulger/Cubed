using UnityEngine;

namespace Cubed
{
    /// <summary>The player's gun: ammo, automatic reloads, spread patterns.</summary>
    public class Weapon
    {
        readonly Player owner;
        public int Ammo;
        public bool Reloading { get; private set; }
        public float ReloadProgress => Reloading && reloadDuration > 0f ? 1f - reloadTimer / reloadDuration : 1f;
        public float Cooldown { get; private set; }
        /// <summary>Total shots fired this round (used by some effects).</summary>
        public int ShotsFired;

        float reloadTimer, reloadDuration;

        public Weapon(Player owner) { this.owner = owner; }

        public int MaxAmmo => Mathf.Max(1, owner.GetStatInt(Stat.Ammo));

        public void Reset()
        {
            Ammo = MaxAmmo;
            Reloading = false;
            Cooldown = 0f;
            ShotsFired = 0;
        }

        /// <summary>Network clients mirror the host's weapon state with this.</summary>
        public void NetSet(int ammo, bool reloading, float progress)
        {
            Ammo = ammo;
            Reloading = reloading;
            reloadDuration = 1f;
            reloadTimer = 1f - Mathf.Clamp01(progress);
        }

        public void AddAmmo(int amount)
        {
            Ammo = Mathf.Min(MaxAmmo, Ammo + amount);
            if (Reloading && Ammo > 0) Reloading = false;
        }

        public void StartReload()
        {
            if (Reloading || Ammo >= MaxAmmo) return;
            Reloading = true;
            reloadDuration = reloadTimer = owner.GetStat(Stat.ReloadTime);
        }

        public void Tick(float dt, bool fireHeld, bool reloadPressed)
        {
            Cooldown -= dt;
            if (Reloading)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0f)
                {
                    Reloading = false;
                    Ammo = MaxAmmo;
                    if (owner.IsHuman) Sfx.Play("reload", 0.5f);
                }
                return;
            }
            if (reloadPressed) StartReload();
            if (fireHeld && Cooldown <= 0f && Ammo > 0) Fire();
            if (Ammo <= 0) StartReload();
        }

        public void Fire()
        {
            int count = Mathf.Max(1, owner.GetStatInt(Stat.BulletsPerShot));
            float spread = owner.GetStat(Stat.Spread);
            float speed = owner.GetStat(Stat.BulletSpeed);
            Vector3 muzzle = owner.Muzzle.position;

            Vector3 baseDir = owner.Input != null ? owner.Input.AimPoint - muzzle : owner.AimDirection;
            if (baseDir.sqrMagnitude < 0.01f || Vector3.Dot(baseDir.normalized, owner.AimDirection) < 0.5f) baseDir = owner.AimDirection;
            baseDir.Normalize();

            // Don't spawn bullets inside a wall the muzzle is poking through.
            if (Physics.Linecast(owner.Head.position, muzzle, out var block, ~(1 << 2), QueryTriggerInteraction.Ignore) && block.collider.GetComponent<Player>() == null)
                muzzle = block.point - (muzzle - owner.Head.position).normalized * 0.1f;

            Vector3 right = Vector3.Cross(Vector3.up, baseDir);
            if (right.sqrMagnitude < 0.001f) right = owner.transform.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(baseDir, right);

            for (int i = 0; i < count; i++)
            {
                float yaw, pitch;
                if (count == 1)
                {
                    Vector2 r = Random.insideUnitCircle * spread * 0.5f;
                    yaw = r.x;
                    pitch = r.y;
                }
                else
                {
                    float t = i / (float)(count - 1);
                    yaw = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t) + Random.Range(-1f, 1f) * spread * 0.08f;
                    pitch = Random.Range(-1f, 1f) * spread * 0.15f;
                }
                Vector3 dir = Quaternion.AngleAxis(yaw, up) * (Quaternion.AngleAxis(pitch, right) * baseDir);
                var bullet = Bullet.Spawn(owner, muzzle, dir * speed);
                owner.ForEachEffect(e => e.OnFire(bullet));
                GameEvents.RaiseBulletFired(owner, bullet);
                // Favor the shooter: a remote player's bullet starts where it would be by the time they saw it fire.
                if (!bullet.IsDead && owner.Input is Net.NetworkInput ni && ni.Rtt > 0.02f) bullet.Advance(Mathf.Min(ni.Rtt * 0.5f, 0.12f));
            }

            Ammo--;
            ShotsFired++;
            Cooldown = owner.GetStat(Stat.AttackCooldown);
            Sfx.PlayAt("shoot", muzzle, owner.IsHuman ? 0.5f : 0.35f);
            Fx.Light(muzzle, Color.Lerp(owner.Color, Color.white, 0.5f), 4.5f, 2.2f, 0.07f);
            Fx.Pulse(muzzle, Color.Lerp(owner.Color, Color.white, 0.6f), 0.15f, 0.65f, 0.07f, true, 4f);
            Fx.Burst(muzzle, Color.Lerp(owner.Color, Color.white, 0.6f), 3, 3f, 0.08f, 0.15f, 0f);
        }
    }
}
