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
            Fx.Pulse(center, new Color(color.r, color.g, color.b, 0.55f), radius * 0.4f, radius * 2f, 0.3f);
            Fx.Burst(center, color, Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 4, 24), radius * 4f, 0.14f, 0.45f);
            Sfx.Play("explode", Mathf.Clamp01(0.3f + radius * 0.12f));

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
