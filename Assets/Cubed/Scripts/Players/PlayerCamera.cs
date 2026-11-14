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

        static readonly RaycastHit[] buffer = new RaycastHit[24];

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
            transform.SetPositionAndRotation(desired, rot);
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
