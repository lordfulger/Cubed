using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>Unlockable trails and victory poses (hats live in <see cref="Hats"/>).</summary>
    public static class Cosmetics
    {
        public class Item
        {
            public string Name;
            /// <summary>Achievement id that unlocks it, or null if free.</summary>
            public string UnlockedBy;
        }

        public static readonly Item[] Trails =
        {
            new Item { Name = "No trail" },
            new Item { Name = "Spark" },
            new Item { Name = "Comet", UnlockedBy = "first_blood" },
            new Item { Name = "Flame", UnlockedBy = "sharpshooter" },
            new Item { Name = "Frost", UnlockedBy = "wall_sitter" },
            new Item { Name = "Shadow", UnlockedBy = "bot_slayer" },
            new Item { Name = "Stardust", UnlockedBy = "collector" },
            new Item { Name = "Rainbow", UnlockedBy = "winner" },
            new Item { Name = "Sunset" },
            new Item { Name = "Bubblegum", UnlockedBy = "regular" },
            new Item { Name = "Neon", UnlockedBy = "round_pro" },
            new Item { Name = "Toxic", UnlockedBy = "hunter" },
            new Item { Name = "Gold Dust", UnlockedBy = "rare_taste" },
            new Item { Name = "Ghost", UnlockedBy = "untouchable" },
        };

        public static readonly Item[] Poses =
        {
            new Item { Name = "Bounce" },
            new Item { Name = "Spin" },
            new Item { Name = "Wiggle", UnlockedBy = "graduate" },
            new Item { Name = "Pulse", UnlockedBy = "card_shark" },
            new Item { Name = "Moonwalk", UnlockedBy = "online" },
            new Item { Name = "Backflip", UnlockedBy = "flawless" },
            new Item { Name = "Victory Lap", UnlockedBy = "veteran" },
            new Item { Name = "Bow" },
            new Item { Name = "Hop Scotch", UnlockedBy = "regular" },
            new Item { Name = "Float", UnlockedBy = "iron_wall" },
            new Item { Name = "Tornado", UnlockedBy = "round_pro" },
            new Item { Name = "Shake", UnlockedBy = "clutch" },
        };

        public static bool Unlocked(Item item) =>
            item.UnlockedBy == null || (Profile.Current != null && Profile.Current.Has(item.UnlockedBy));

        public static string UnlockHint(Item item)
        {
            if (item.UnlockedBy == null) return "";
            var a = Achievements.All.FirstOrDefault(x => x.Id == item.UnlockedBy);
            return a != null ? a.Title : item.UnlockedBy;
        }

        // ------------------------------------------------------------------ trails

        /// <summary>Attach (or replace) a fighter's movement trail.</summary>
        public static TrailRenderer ApplyTrail(Transform anchor, int trail, Color team)
        {
            if (anchor == null) return null;
            var old = anchor.Find("CosmeticTrail");
            if (old != null) Object.Destroy(old.gameObject);
            if (trail <= 0 || trail >= Trails.Length) return null;
            var go = new GameObject("CosmeticTrail");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.35f, -0.1f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Mats.HdrTrail;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.minVertexDistance = 0.12f;
            tr.numCapVertices = 2;
            Gradient g = new Gradient();
            float time = 0.35f, width = 0.18f;
            switch (trail)
            {
                case 1: g = Fade(team, team); time = 0.25f; width = 0.1f; break;
                case 2: g = Fade(Color.Lerp(team, Color.white, 0.5f), team); time = 0.55f; width = 0.35f; break;
                case 3: g = Fade(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.25f, 0.05f)); time = 0.4f; width = 0.3f; break;
                case 4: g = Fade(Color.white, new Color(0.4f, 0.85f, 1f)); time = 0.45f; width = 0.25f; break;
                case 5: g = Fade(new Color(0.35f, 0.1f, 0.5f), new Color(0.05f, 0.02f, 0.1f)); time = 0.6f; width = 0.4f; break;
                case 6: g = Fade(new Color(1f, 1f, 0.8f), new Color(0.7f, 0.6f, 1f)); time = 0.7f; width = 0.12f; break;
                case 7:
                    g.SetKeys(new[]
                    {
                        new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.25f),
                        new GradientColorKey(new Color(0.3f, 1f, 0.4f), 0.5f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 0.75f),
                        new GradientColorKey(new Color(0.8f, 0.4f, 1f), 1f),
                    }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                    time = 0.6f; width = 0.3f;
                    break;
                case 8: g = Keys(0.85f, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0.45f), new Color(0.55f, 0.25f, 0.85f)); time = 0.5f; width = 0.28f; break;
                case 9: g = Keys(0.85f, new Color(1f, 0.6f, 0.85f), Color.white, new Color(1f, 0.45f, 0.75f), new Color(0.6f, 0.9f, 1f)); time = 0.45f; width = 0.24f; break;
                case 10: g = Keys(0.95f, new Color(0.3f, 1f, 1f), new Color(1f, 0.25f, 0.9f)); time = 0.35f; width = 0.14f; break;
                case 11: g = Keys(0.85f, new Color(0.75f, 1f, 0.2f), new Color(0.2f, 0.7f, 0.15f), new Color(0.05f, 0.25f, 0.05f)); time = 0.55f; width = 0.32f; break;
                case 12: g = Keys(0.9f, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.75f, 0.2f), new Color(0.8f, 0.5f, 0.1f)); time = 0.8f; width = 0.1f; break;
                case 13: g = Keys(0.4f, new Color(0.9f, 0.95f, 1f), new Color(0.75f, 0.8f, 1f)); time = 0.7f; width = 0.5f; break;
            }
            tr.colorGradient = g;
            tr.time = time;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
            return tr;
        }

        /// <summary>Gradient through several colors, fading out from the given alpha.</summary>
        static Gradient Keys(float alpha, params Color[] colors)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], colors.Length == 1 ? 0f : i / (float)(colors.Length - 1));
            g.SetKeys(ck, new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Gradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ------------------------------------------------------------------ poses

        /// <summary>Animate a display fighter's rig for a victory pose at time t.</summary>
        public static void AnimatePose(Transform rig, int pose, float t)
        {
            if (rig == null) return;
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Vector3 scale = Vector3.one;
            switch (pose)
            {
                case 1: // spin
                    rot = Quaternion.Euler(0f, t * 360f, 0f);
                    pos.y = Mathf.Abs(Mathf.Sin(t * 3f)) * 0.15f;
                    break;
                case 2: // wiggle
                    rot = Quaternion.Euler(0f, Mathf.Sin(t * 10f) * 25f, Mathf.Sin(t * 10f + 1f) * 12f);
                    break;
                case 3: // pulse
                    float s = 1f + 0.12f * Mathf.Sin(t * 7f);
                    scale = new Vector3(s, 2f - s, s);
                    break;
                case 4: // moonwalk
                    pos.x = Mathf.PingPong(t * 0.8f, 1f) - 0.5f;
                    rot = Quaternion.Euler(0f, 90f, Mathf.Sin(t * 14f) * 4f);
                    break;
                case 5: // backflip every 1.6 s
                {
                    float k = Mathf.Repeat(t, 1.6f);
                    if (k < 0.7f)
                    {
                        float f = k / 0.7f;
                        pos.y = Mathf.Sin(f * Mathf.PI) * 1.2f;
                        rot = Quaternion.Euler(-f * 360f, 0f, 0f);
                    }
                    break;
                }
                case 6: // victory lap
                    pos = new Vector3(Mathf.Cos(t * 2.2f), Mathf.Abs(Mathf.Sin(t * 8f)) * 0.2f, Mathf.Sin(t * 2.2f)) * 0.7f;
                    rot = Quaternion.LookRotation(new Vector3(-Mathf.Sin(t * 2.2f), 0f, Mathf.Cos(t * 2.2f)));
                    break;
                case 7: // bow every 2 s
                {
                    float k = Mathf.Repeat(t, 2f);
                    float f = k < 0.9f ? Mathf.Sin(k / 0.9f * Mathf.PI) : 0f;
                    rot = Quaternion.Euler(f * 40f, 0f, 0f);
                    pos.z = f * 0.12f;
                    break;
                }
                case 8: // hop scotch: hop from side to side
                {
                    float ph = t * 2.6f;
                    float side = Mathf.Floor(ph) % 2f == 0f ? -1f : 1f;
                    float f = ph - Mathf.Floor(ph);
                    pos.x = Mathf.Lerp(-side, side, Mathf.SmoothStep(0f, 1f, f)) * 0.35f;
                    pos.y = Mathf.Sin(f * Mathf.PI) * 0.45f;
                    rot = Quaternion.Euler(0f, 0f, -side * Mathf.Sin(f * Mathf.PI) * 15f);
                    float sq = 1f - Mathf.Clamp01(0.15f - f) * 0.8f;
                    scale = new Vector3(2f - sq, sq, 2f - sq);
                    break;
                }
                case 9: // float: hover and drift
                    pos.y = 0.45f + Mathf.Sin(t * 1.6f) * 0.12f;
                    rot = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 6f, t * 40f, Mathf.Cos(t * 1.3f) * 6f);
                    break;
                case 10: // tornado: rise while spinning fast
                {
                    float k = Mathf.Repeat(t, 2.4f) / 2.4f;
                    float lift = Mathf.Sin(k * Mathf.PI);
                    pos.y = lift * 0.8f;
                    rot = Quaternion.Euler(0f, t * 360f + lift * 900f, 0f);
                    float st = 1f - lift * 0.15f;
                    scale = new Vector3(st, 2f - st, st);
                    break;
                }
                case 11: // shake it out
                {
                    float k = Mathf.Repeat(t, 1.5f);
                    float amp = k < 0.7f ? Mathf.Sin(k / 0.7f * Mathf.PI) : 0f;
                    rot = Quaternion.Euler(0f, Mathf.Sin(t * 28f) * 12f * amp, Mathf.Sin(t * 34f) * 10f * amp);
                    float sh = 1f + amp * 0.06f * Mathf.Sin(t * 30f);
                    scale = new Vector3(sh, 1f / sh, sh);
                    break;
                }
                default: // bounce
                    float b = Mathf.Abs(Mathf.Sin(t * 4.5f));
                    pos.y = b * 0.5f;
                    scale = new Vector3(1f + (1f - b) * 0.12f, 1f - (1f - b) * 0.12f, 1f + (1f - b) * 0.12f);
                    break;
            }
            rig.localPosition = pos;
            rig.localRotation = rot;
            rig.localScale = scale;
        }
    }

    /// <summary>A non-playing fighter for the podium and the main menu.</summary>
    public class DisplayFighter : MonoBehaviour
    {
        public FighterParts Parts { get; private set; }
        public int Pose;
        public bool Wander;
        float t, blink = 2f;
        Vector3 home, wanderTarget;
        float wanderWait;
        Bounds area;

        public static DisplayFighter Create(Transform parent, Vector3 pos, Quaternion rot, Color team, CharacterLook look, float scale = 1f)
        {
            var go = new GameObject("DisplayFighter");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            var f = go.AddComponent<DisplayFighter>();
            var model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);
            f.Parts = FighterModel.Build(model, team, look ?? new CharacterLook());
            f.Pose = look != null ? look.pose : 0;
            if (look != null) Cosmetics.ApplyTrail(f.Parts.Rig, look.trail, team);
            f.t = Random.value * 10f;
            f.home = pos;
            return f;
        }

        /// <summary>Walk around the given area (menu background).</summary>
        public void SetWander(Bounds bounds)
        {
            Wander = true;
            area = bounds;
            wanderTarget = home;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (Wander) UpdateWander(dt);
            else Cosmetics.AnimatePose(Parts.Rig, Pose, t);
            // Blink.
            blink -= dt;
            if (blink < 0f) blink = Random.Range(1.5f, 4f);
            float open = blink < 0.12f ? 0.1f : 1f;
            for (int i = 0; i < Parts.Eyes.Count && i < Parts.EyeScales.Count; i++)
                if (Parts.Eyes[i] != null) Parts.Eyes[i].localScale = Vector3.Scale(Parts.EyeScales[i], new Vector3(1f, open, 1f));
        }

        void UpdateWander(float dt)
        {
            var pos = transform.position;
            var to = wanderTarget - pos;
            to.y = 0f;
            if (to.magnitude < 0.3f)
            {
                wanderWait -= dt;
                Cosmetics.AnimatePose(Parts.Rig, Pose, t);
                if (wanderWait <= 0f)
                {
                    var p = home + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                    var b = area;
                    p.x = Mathf.Clamp(p.x, b.min.x + 2f, b.max.x - 2f);
                    p.z = Mathf.Clamp(p.z, b.min.z + 2f, b.max.z - 2f);
                    // Only walk to places with ground under them.
                    if (Physics.Raycast(new Vector3(p.x, pos.y + 2f, p.z), Vector3.down, out var hit, 4f, Bullet.CollisionMask, QueryTriggerInteraction.Ignore))
                        wanderTarget = hit.point;
                    wanderWait = Random.Range(1.5f, 4f);
                }
                return;
            }
            var step = to.normalized * Mathf.Min(to.magnitude, 3f * dt);
            transform.position = pos + step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-dt * 8f));
            // Waddle.
            Parts.Rig.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 10f)) * 0.12f, 0f);
            Parts.Rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 10f) * 6f);
            Parts.Rig.localScale = Vector3.one;
            if (Parts.FootL != null) Parts.FootL.localPosition = new Vector3(-0.22f, 0.09f + Mathf.Max(0f, Mathf.Sin(t * 10f)) * 0.1f, Mathf.Sin(t * 10f) * 0.12f);
            if (Parts.FootR != null) Parts.FootR.localPosition = new Vector3(0.22f, 0.09f + Mathf.Max(0f, -Mathf.Sin(t * 10f)) * 0.1f, -Mathf.Sin(t * 10f) * 0.12f);
        }
    }

    /// <summary>The winners' podium shown at the end of a match (host and clients).</summary>
    public static class Podium
    {
        static GameObject root;
        static float shownAt;
        public static bool Showing => root != null;
        public static Vector3 CameraPos { get; private set; }
        public static Vector3 CameraLook { get; private set; }

        public static void Show(GameManager gm)
        {
            Hide();
            if (gm == null || gm.Map == null) return;
            int win = gm.MatchWinner;
            var b = gm.Map.FullBounds;
            var center = new Vector3(b.center.x, b.max.y + 40f, b.center.z);
            root = new GameObject("Podium");
            root.transform.position = center;
            shownAt = Time.unscaledTime;

            var teamColor = win >= 0 ? Teams.GetColor(win) : Color.white;
            var stone = Looks.Block(new Color(0.85f, 0.87f, 0.92f));
            var accent = Mats.GlowAvailable ? Mats.Glow(new Color(teamColor.r, teamColor.g, teamColor.b, 0.6f), 2.2f, 0f) : Mats.Solid(teamColor);
            // Floor disc and three steps (2nd left, 1st middle, 3rd right).
            var floor = Prim.Create(Prim.SphereMesh, "Floor", root.transform, new Vector3(0f, -0.1f, 0f), new Vector3(16f, 0.2f, 16f), Looks.Block(new Color(0.16f, 0.17f, 0.22f)));
            floor.AddComponent<SphereCollider>();
            float[] heights = { 1.6f, 2.4f, 1.0f };
            float[] xs = { -2.4f, 0f, 2.4f };
            for (int i = 0; i < 3; i++)
            {
                Prim.Cube("Step", root.transform, new Vector3(xs[i], heights[i] * 0.5f, 0f), new Vector3(2.3f, heights[i], 2.3f), stone, true);
                Prim.Cube("Trim", root.transform, new Vector3(xs[i], heights[i] + 0.01f, 0f), new Vector3(2.32f, 0.04f, 2.32f), accent);
            }
            var light = new GameObject("Spot").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Spot;
            light.range = 30f;
            light.spotAngle = 60f;
            light.intensity = 3f;
            light.color = Color.Lerp(teamColor, Color.white, 0.6f);
            light.transform.localPosition = new Vector3(0f, 12f, 6f);
            light.transform.LookAt(center + Vector3.up * 2f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(root.transform, false);
            fill.type = LightType.Point;
            fill.range = 20f;
            fill.intensity = 1.2f;
            fill.transform.localPosition = new Vector3(-4f, 4f, 7f);

            // Winners on the steps, best first (MVP in the middle), everyone else watching from below.
            var winners = gm.Players.Where(p => p.HomeTeam == win).OrderByDescending(p => p.Kills * 100 + p.DamageDealt).ToList();
            int[] order = { 1, 0, 2 };
            var face = Quaternion.LookRotation(Vector3.back);
            for (int i = 0; i < winners.Count; i++)
            {
                var p = winners[i];
                var look = p.Slot != null ? CharacterLook.Decode(p.Slot.look) : new CharacterLook();
                Vector3 pos = i < 3 ? new Vector3(xs[order[i]], heights[order[i]], 0f) : new Vector3((i - 3) * 1.3f - 2f, 0f, 2.6f);
                DisplayFighter.Create(root.transform, center + pos, face, p.Color, look, i == 0 ? 1.2f : 1f);
            }
            var losers = gm.Players.Where(p => p.HomeTeam != win).ToList();
            for (int i = 0; i < losers.Count; i++)
            {
                var p = losers[i];
                var look = p.Slot != null ? CharacterLook.Decode(p.Slot.look) : new CharacterLook();
                look.pose = 0;
                float ang = Mathf.Lerp(-55f, 55f, losers.Count == 1 ? 0.5f : i / (float)(losers.Count - 1)) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(ang) * 6f, 0f, -Mathf.Cos(ang) * 6f + 9f) - new Vector3(0f, 0f, 9f) + new Vector3(0f, 0f, 3.5f);
                var f = DisplayFighter.Create(root.transform, center + pos, Quaternion.LookRotation(-pos.normalized + Vector3.zero), p.Color, look, 0.8f);
                f.Pose = 3; // polite applause-ish pulse
            }
            CameraLook = center + new Vector3(1.6f, 2.2f, 0f);
            CameraPos = center + new Vector3(1.6f, 4.2f, -11f);
            gm.SetPodiumCamera(true);
        }

        public static void Update()
        {
            if (root == null) return;
            // Confetti in bursts.
            float age = Time.unscaledTime - shownAt;
            if (Mathf.Repeat(age, 0.9f) < Time.unscaledDeltaTime)
            {
                Fx.LocalOnly = true;
                try
                {
                    var c = root.transform.position + new Vector3(Random.Range(-4f, 4f), 9f, Random.Range(-2f, 2f));
                    Fx.Burst(c, Teams.GetColor(Random.Range(0, Teams.Max)), 18, 6f, 0.1f, 3f, 3f);
                }
                finally { Fx.LocalOnly = false; }
            }
        }

        public static void Hide()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            GameManager.Instance?.SetPodiumCamera(false);
        }
    }

    /// <summary>Fighters wandering the preview map behind the main menu.</summary>
    public static class MenuShowcase
    {
        static GameObject root;
        static MapRuntime builtFor;

        public static void Ensure(GameManager gm)
        {
            if (gm == null || gm.Map == null) { Clear(); return; }
            if (root != null && builtFor == gm.Map) return;
            Clear();
            builtFor = gm.Map;
            root = new GameObject("MenuShowcase");
            var spawns = gm.Map.Data.spawns.Select(s => s.Position).OrderBy(_ => Random.value).Take(5).ToList();
            var mine = Profile.Current != null ? Profile.Current.Look(-1) : new CharacterLook();
            for (int i = 0; i < spawns.Count; i++)
            {
                var look = i == 0 ? mine.Clone() : CharacterLook.ForSeed("menu" + i + gm.Map.Data.name);
                if (i > 0) look.pose = Random.Range(0, Cosmetics.Poses.Length);
                var f = DisplayFighter.Create(root.transform, spawns[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Teams.GetColor(i % Teams.Max), look);
                f.SetWander(gm.Map.FullBounds);
            }
        }

        public static void Clear()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            builtFor = null;
        }
    }
}
