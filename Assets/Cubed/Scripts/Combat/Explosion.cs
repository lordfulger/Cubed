using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>Instant area damage with falloff and knockback.</summary>
    public static class Explosion
    {
        static readonly Collider[] buffer = new Collider[64];

        /// <param name="selfDamage">Should the owner be hurt by their own blast?</param>
        public static void Create(Vector3 center, float radius, float damage, Player owner, float knockback, Color color, bool selfDamage = false)
        {
            if (radius <= 0f) return;
            // Fireball: hot core, colored shell, smoke and a scorch mark.
            var fire = Color.Lerp(color, new Color(1f, 0.55f, 0.15f), 0.6f);
            Fx.Pulse(center, new Color(fire.r, fire.g, fire.b, 0.9f), radius * 0.5f, radius * 2.2f, 0.35f, true, 3f);
            Fx.Pulse(center, new Color(1f, 0.95f, 0.8f, 1f), radius * 0.3f, radius * 1.1f, 0.14f, true, 5f);
            Fx.Smoke(center, Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 3, 8), radius * 0.6f, new Color(0.22f, 0.22f, 0.25f, 0.5f));
            if (Physics.Raycast(center + Vector3.up * 0.2f, Vector3.down, out var ground, radius + 1f, ~(1 << 2), QueryTriggerInteraction.Ignore) && ground.collider.GetComponent<Player>() == null)
                Fx.Decal(ground.point, ground.normal, radius * 1.8f, new Color(0.03f, 0.03f, 0.03f, 0.75f));
            Fx.Burst(center, color, Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 4, 24), radius * 4f, 0.14f, 0.45f);
            Sfx.PlayAt("explode", center, Mathf.Clamp01(0.4f + radius * 0.12f));
            Fx.Light(center, Color.Lerp(color, new Color(1f, 0.7f, 0.3f), 0.5f), radius * 3f + 4f, 5f, 0.35f);
            CameraShake.Impulse(center, 0.25f + radius * 0.08f, radius * 4f + 10f);

            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~(1 << 2), QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<Player>();
            for (int i = 0; i < n; i++)
            {
                var col = buffer[i];
                if (col == null) continue;
                var p = col.GetComponent<Player>();
                if (p != null)
                {
                    if (!p.IsAlive || damaged.Contains(p)) continue;
                    if (p == owner && !selfDamage) continue;
                    damaged.Add(p);
                    Vector3 to = p.Center - center;
                    float dist = to.magnitude;
                    float falloff = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(dist / radius));
                    p.TakeDamage(new DamageInfo
                    {
                        Amount = damage * falloff,
                        Source = owner,
                        Type = DamageType.Explosion,
                        Point = center,
                        Direction = dist > 0.01f ? (to / dist + Vector3.up * 0.3f) : Vector3.up,
                        Knockback = (knockback + radius * 2f) * falloff,
                    });
                    continue;
                }
                var block = col.GetComponent<BlockInfo>();
                if (block != null) block.Damage(damage * 0.5f);
            }
        }
    }
}
