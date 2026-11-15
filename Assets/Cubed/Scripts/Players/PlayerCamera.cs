using UnityEngine;

namespace Cubed
{
    /// <summary>Over-the-shoulder camera for one human player (one per split-screen viewport).</summary>
    public class PlayerCamera : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Player Target;
        public HumanInput InputSource;
        public float Distance = 5.5f;
        public Vector3 ShoulderOffset = new Vector3(0.85f, 0.35f, 0f);
        public Player Following { get; private set; }

        /// <summary>All live player cameras (used for shake and positional audio).</summary>
        public static readonly System.Collections.Generic.List<PlayerCamera> Active = new System.Collections.Generic.List<PlayerCamera>();

        // Feedback state read by the HUD.
        public float HitMarker { get; private set; }
        public float DamageFlash { get; private set; }
        public bool LowHealth => Target != null && Target.IsAlive && Target.Health < Target.MaxHealth * 0.3f;

        float trauma, fovKick, baseFov = 70f, heartbeat;
        float lastHealth = -1f, lastDealt;
        int lastAmmo = -1;
        bool lastGrounded = true;
        float lastVy;

        static readonly RaycastHit[] buffer = new RaycastHit[24];

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>Set the resting field of view (split-screen layouts use wider FOVs).</summary>
        public void SetBaseFov(float fov) { baseFov = fov; Cam.fieldOfView = fov; }

        void Monitor(float dt)
        {
            var p = Target;
            if (p == null) return;
            if (lastHealth >= 0f && p.IsAlive && p.Health < lastHealth - 0.5f)
            {
                float lost = lastHealth - p.Health;
                AddTrauma(Mathf.Clamp(lost / 70f, 0.12f, 0.6f));
                DamageFlash = Mathf.Clamp01(DamageFlash + lost / 40f + 0.25f) * (Options.Current.reduceFlashes ? 0.4f : 1f);
                if (InputSource != null && InputSource.Joystick > 0) Rumble.Pulse(InputSource.Joystick, 0.6f, 0.9f, 0.18f);
            }
            lastHealth = p.Health;
            if (p.DamageDealt > lastDealt + 0.5f)
            {
                HitMarker = 0.18f;
                Sfx.Play("hitmark", 0.5f, 0.05f);
            }
            lastDealt = p.DamageDealt;
            int ammo = p.Weapon.Ammo;
            if (lastAmmo >= 0 && ammo < lastAmmo && p.IsAlive)
            {
                fovKick = Mathf.Min(fovKick + 2.2f, 5f);
                AddTrauma(0.05f);
                if (InputSource != null && InputSource.Joystick > 0) Rumble.Pulse(InputSource.Joystick, 0.15f, 0.35f, 0.06f);
            }
            lastAmmo = ammo;
            if (p.Grounded && !lastGrounded && lastVy < -12f) AddTrauma(Mathf.Clamp01((-lastVy - 12f) / 40f) + 0.08f);
            lastGrounded = p.Grounded;
            lastVy = p.Velocity.y;

            if (LowHealth)
            {
                heartbeat -= dt;
                if (heartbeat <= 0f)
                {
                    heartbeat = 0.85f;
                    Sfx.Play("heartbeat", 0.6f, 0f);
                }
            }
            HitMarker = Mathf.Max(0f, HitMarker - dt);
            DamageFlash = Mathf.Max(0f, DamageFlash - dt * 1.6f);
        }

        public static PlayerCamera Create(Player target, HumanInput input)
        {
            var go = new GameObject("Camera (" + target.DisplayName + ")");
            var pc = go.AddComponent<PlayerCamera>();
            pc.Cam = go.AddComponent<Camera>();
            pc.Cam.clearFlags = CameraClearFlags.SolidColor;
            pc.Cam.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            pc.Cam.fieldOfView = 70f;
            pc.Cam.nearClipPlane = 0.08f;
            pc.Cam.farClipPlane = 600f;
            CubedPostFX.Attach(pc.Cam);
            pc.Target = target;
            pc.InputSource = input;
            input.Camera = pc;
            target.Camera = pc;
            return pc;
        }

        public void SetViewport(Rect normalized) => Cam.rect = normalized;

        Player PickFollow()
        {
            if (Target != null && Target.IsAlive) return Target;
            // Spectate a living teammate, otherwise anyone alive.
            Player any = null;
            foreach (var p in Player.All)
            {
                if (!p.IsAlive) continue;
                if (p.Team == Target.Team) return p;
                if (any == null) any = p;
            }
            return any != null ? any : Target;
        }

        void LateUpdate()
        {
            if (Target == null || InputSource == null) return;
            float dt = Time.unscaledDeltaTime;
            Monitor(dt);
            Following = PickFollow();
            float size = Following.Size;
            Quaternion rot = Quaternion.Euler(InputSource.Pitch, InputSource.Yaw, 0f);
            Vector3 pivot = Following.transform.position + Vector3.up * (1.35f * size);
            Vector3 offset = new Vector3(ShoulderOffset.x, ShoulderOffset.y, -Distance) * Mathf.Lerp(1f, size, 0.6f);
            Vector3 desired = pivot + rot * offset;

            // Keep the camera out of walls.
            Vector3 dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist > 0.01f)
            {
                dir /= dist;
                int n = Physics.SphereCastNonAlloc(pivot, 0.25f, dir, buffer, dist, Bullet.CollisionMask, QueryTriggerInteraction.Ignore);
                float closest = dist;
                for (int i = 0; i < n; i++)
                {
                    var h = buffer[i];
                    if (h.collider.GetComponent<Player>() != null) continue;
                    if (h.distance > 0f && h.distance < closest) closest = h.distance;
                }
                desired = pivot + dir * Mathf.Max(0.3f, closest - 0.05f);
            }
            // Screen shake (trauma squared feels natural) and firing FOV kick.
            float shake = trauma * trauma * Options.Current.screenShake;
            float time = Time.unscaledTime * 22f;
            var shakeRot = Quaternion.Euler(
                (Mathf.PerlinNoise(time, 1f) - 0.5f) * 6f * shake,
                (Mathf.PerlinNoise(2f, time) - 0.5f) * 6f * shake,
                (Mathf.PerlinNoise(time, 3f) - 0.5f) * 8f * shake);
            transform.SetPositionAndRotation(desired + rot * new Vector3(Mathf.PerlinNoise(time, 5f) - 0.5f, Mathf.PerlinNoise(6f, time) - 0.5f, 0f) * 0.35f * shake, rot * shakeRot);
            trauma = Mathf.Max(0f, trauma - dt * 1.5f);
            fovKick = Mathf.Lerp(fovKick, 0f, 1f - Mathf.Exp(-dt * 12f));
            Cam.fieldOfView = baseFov + Options.Current.fovOffset + fovKick * Mathf.Min(1f, Options.Current.screenShake);
        }

        /// <summary>The world point under the crosshair, ignoring the player and anything behind them.</summary>
        public Vector3 ComputeAimPoint(Player self)
        {
            var ray = new Ray(transform.position, transform.forward);
            float skip = Vector3.Dot(self.Head.position - transform.position, transform.forward);
            int n = Physics.RaycastNonAlloc(ray, buffer, 300f, Bullet.CollisionMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = ray.GetPoint(120f);
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                if (h.distance < skip + 0.5f) continue;
                var p = h.collider.GetComponent<Player>();
                if (p == self) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    point = h.point;
                }
            }
            return point;
        }

        /// <summary>The screen-space rect of this camera's viewport in GUI coordinates (top-left origin).</summary>
        public Rect GuiRect
        {
            get
            {
                var r = Cam.pixelRect;
                return new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
            }
        }
    }
}
