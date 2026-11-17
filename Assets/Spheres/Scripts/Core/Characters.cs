using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>A fighter's customizable face and accessories. Body color is always the team color.</summary>
    [Serializable]
    public class CharacterLook
    {
        public string name = "Fighter";
        public int eyes;
        public int mouth = 1;
        public int detail;
        public int eyeColor;
        public string hat = "none";
        /// <summary>Index into <see cref="Cosmetics.Trails"/>.</summary>
        public int trail;
        /// <summary>Index into <see cref="Cosmetics.Poses"/> (victory pose on the podium).</summary>
        public int pose;
        /// <summary>Body color: index into <see cref="BodyColors"/>, or -1 to use your team's color.</summary>
        public int bodyColor = -1;

        public static readonly string[] BodyColorNames = { "Cherry", "Tangerine", "Sunflower", "Lime", "Mint", "Teal", "Sky", "Cobalt", "Grape", "Bubblegum", "Snow", "Charcoal" };
        public static readonly Color[] BodyColors =
        {
            new Color(0.9f, 0.18f, 0.22f), new Color(1f, 0.5f, 0.12f), new Color(1f, 0.84f, 0.2f), new Color(0.55f, 0.9f, 0.2f),
            new Color(0.35f, 0.95f, 0.65f), new Color(0.1f, 0.7f, 0.7f), new Color(0.35f, 0.75f, 1f), new Color(0.2f, 0.32f, 0.95f),
            new Color(0.6f, 0.3f, 0.95f), new Color(1f, 0.45f, 0.75f), new Color(0.92f, 0.93f, 0.96f), new Color(0.2f, 0.21f, 0.25f),
        };

        /// <summary>The color your body is painted (team color unless you picked one).</summary>
        public Color BodyColorOr(Color team) => bodyColor >= 0 && bodyColor < BodyColors.Length ? BodyColors[bodyColor] : team;

        public static readonly string[] EyeStyles = { "Round", "Sleepy", "Angry", "Happy", "Big", "Cyclops", "Visor", "Dots" };
        public static readonly string[] MouthStyles = { "None", "Smile", "Grin", "Frown", "Gasp", "Fangs", "Flat", "Tongue" };
        public static readonly string[] Details = { "None", "Blush", "Freckles", "Mustache", "Glasses", "Scar", "Bandage", "Stripes" };
        public static readonly string[] EyeColorNames = { "White", "Cyan", "Lime", "Gold", "Pink", "Red", "Violet", "Orange" };
        public static readonly Color[] EyeColors =
        {
            new Color(0.95f, 0.97f, 1f), new Color(0.35f, 0.95f, 1f), new Color(0.6f, 1f, 0.4f), new Color(1f, 0.85f, 0.3f),
            new Color(1f, 0.55f, 0.85f), new Color(1f, 0.35f, 0.3f), new Color(0.75f, 0.55f, 1f), new Color(1f, 0.6f, 0.2f),
        };

        public CharacterLook Clone() => (CharacterLook)MemberwiseClone();

        /// <summary>Compact form for the network and match slots: "eyes.mouth.detail.color.hat.trail.pose".</summary>
        public string Encode() => eyes + "." + mouth + "." + detail + "." + eyeColor + "." + (hat ?? "none") + "." + trail + "." + pose + "." + bodyColor;

        public static CharacterLook Decode(string code)
        {
            var l = new CharacterLook();
            if (string.IsNullOrEmpty(code)) return l;
            var p = code.Split(new[] { '.' }, 8);
            if (p.Length > 0) int.TryParse(p[0], out l.eyes);
            if (p.Length > 1) int.TryParse(p[1], out l.mouth);
            if (p.Length > 2) int.TryParse(p[2], out l.detail);
            if (p.Length > 3) int.TryParse(p[3], out l.eyeColor);
            if (p.Length > 4) l.hat = p[4];
            if (p.Length > 5) int.TryParse(p[5], out l.trail);
            if (p.Length > 6) int.TryParse(p[6], out l.pose);
            if (p.Length > 7) int.TryParse(p[7], out l.bodyColor);
            l.Clamp();
            return l;
        }

        public void Clamp()
        {
            eyes = Mathf.Clamp(eyes, 0, EyeStyles.Length - 1);
            mouth = Mathf.Clamp(mouth, 0, MouthStyles.Length - 1);
            detail = Mathf.Clamp(detail, 0, Details.Length - 1);
            eyeColor = Mathf.Clamp(eyeColor, 0, EyeColors.Length - 1);
            if (string.IsNullOrEmpty(hat)) hat = "none";
            trail = Mathf.Clamp(trail, 0, Cosmetics.Trails.Length - 1);
            pose = Mathf.Clamp(pose, 0, Cosmetics.Poses.Length - 1);
            bodyColor = Mathf.Clamp(bodyColor, -1, BodyColors.Length - 1);
        }

        /// <summary>Deterministic random look (bots), so a bot keeps its face between rounds and machines.</summary>
        public static CharacterLook ForSeed(string seed)
        {
            int h = 17;
            foreach (char c in seed ?? "") h = h * 31 + c;
            var rng = new System.Random(h);
            return new CharacterLook
            {
                eyes = rng.Next(EyeStyles.Length),
                mouth = rng.Next(MouthStyles.Length),
                detail = rng.Next(3) == 0 ? rng.Next(Details.Length) : 0,
                eyeColor = rng.Next(EyeColors.Length),
                hat = Hats.ForBot(seed),
            };
        }
    }

    /// <summary>References to a built fighter model, for animation.</summary>
    public class FighterParts
    {
        public Transform Rig, Head, Face, FootL, FootR, Gun, Muzzle, HatRoot;
        public Renderer Body;
        public readonly List<Transform> Eyes = new List<Transform>();
        public readonly List<Vector3> EyeScales = new List<Vector3>();
    }

    /// <summary>
    /// Builds the round fighter: glossy ball body, glowing team band, face plate, customizable eyes/mouth/
    /// details, round feet, a rounded blaster, a back pod and a hat. Shared by players and the editor preview.
    /// </summary>
    public static class FighterModel
    {
        public static readonly Vector3 GunRest = new Vector3(0.55f, -0.25f, 0.3f);

        /// <summary>The glossy body material for a look (its body color, or the team color).</summary>
        public static Material BodyMaterial(Color team, CharacterLook look) => Gloss(look != null ? look.BodyColorOr(team) : team, 0.62f);

        static Material Gloss(Color c, float g)
        {
            var m = Mats.Solid(c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", g);
            return m;
        }

        static GameObject Ball(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadow = false)
        {
            var go = Prim.Sphere(name, parent, pos, 1f, mat);
            go.transform.localScale = scale;
            if (shadow) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static FighterParts Build(Transform model, Color team, CharacterLook look)
        {
            var parts = new FighterParts();
            var rig = new GameObject("Rig").transform;
            rig.SetParent(model, false);
            parts.Rig = rig;

            // Your chosen body color; the glowing band and blaster tip always show your team.
            var bodyColor = look != null ? look.BodyColorOr(team) : team;
            var body = BodyMaterial(team, look);
            var dark = Mats.Solid(Color.Lerp(bodyColor, Color.black, 0.55f));

            parts.Body = Ball("Body", rig, new Vector3(0f, 0.7f, 0f), Vector3.one, body, true).GetComponent<Renderer>();
            Ball("Pack", rig, new Vector3(0f, 0.74f, -0.46f), new Vector3(0.46f, 0.42f, 0.26f), dark);
            parts.FootL = Ball("FootL", rig, new Vector3(-0.22f, 0.09f, 0f), new Vector3(0.26f, 0.18f, 0.34f), dark).transform;
            parts.FootR = Ball("FootR", rig, new Vector3(0.22f, 0.09f, 0f), new Vector3(0.26f, 0.18f, 0.34f), dark).transform;
            if (Mats.GlowAvailable)
            {
                var band = Ball("TeamStrip", rig, new Vector3(0f, 0.7f, 0f), new Vector3(1.035f, 0.07f, 1.035f), Mats.Glow(team, 2.4f, 0f));
                band.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var head = new GameObject("Head").transform;
            head.SetParent(rig, false);
            head.localPosition = new Vector3(0f, 0.95f, 0f);
            parts.Head = head;

            var gun = new GameObject("Gun").transform;
            gun.SetParent(head, false);
            gun.localPosition = GunRest;
            parts.Gun = gun;
            Ball("Barrel", gun, Vector3.zero, new Vector3(0.2f, 0.2f, 0.82f), dark);
            Ball("Grip", gun, new Vector3(0f, -0.13f, -0.17f), new Vector3(0.13f, 0.22f, 0.15f), dark);
            Ball("GunTip", gun, new Vector3(0f, 0f, 0.38f), new Vector3(0.17f, 0.17f, 0.1f), Mats.GlowAvailable ? Mats.Glow(team, 2.2f, 0.6f) : body);
            parts.Muzzle = new GameObject("Muzzle").transform;
            parts.Muzzle.SetParent(gun, false);
            parts.Muzzle.localPosition = new Vector3(0f, 0f, 0.48f);

            parts.HatRoot = new GameObject("Hat").transform;
            parts.HatRoot.SetParent(head, false);
            parts.HatRoot.localPosition = new Vector3(0f, 0.25f, 0f);

            BuildFace(parts, team, look);
            SetHat(parts, team, look != null ? look.hat : "none");
            return parts;
        }

        /// <summary>(Re)build the face: plate, eyes, mouth and details.</summary>
        public static void BuildFace(FighterParts parts, Color team, CharacterLook look)
        {
            look = look ?? new CharacterLook();
            look.Clamp();
            if (parts.Face != null) UnityEngine.Object.Destroy(parts.Face.gameObject);
            parts.Eyes.Clear();
            parts.EyeScales.Clear();
            var face = new GameObject("Face").transform;
            face.SetParent(parts.Head, false);
            parts.Face = face;

            var plate = Gloss(new Color(0.07f, 0.08f, 0.12f), 0.85f);
            var black = Mats.Solid(new Color(0.05f, 0.05f, 0.07f));
            var white = Mats.Solid(new Color(0.96f, 0.96f, 0.98f));
            var eyeColor = CharacterLook.EyeColors[look.eyeColor];
            var eyeMat = Mats.GlowAvailable ? Mats.Glow(eyeColor, 1.8f, 0f) : Mats.Solid(eyeColor);

            // Dark lens on the front of the ball that the eyes sit on.
            Ball("Visor", face, new Vector3(0f, -0.05f, 0.27f), new Vector3(0.74f, 0.44f, 0.5f), plate);

            void Eye(Vector3 pos, Vector3 scale, float roll, bool pupil = true)
            {
                var e = Ball("Eye", face, pos, scale, eyeMat);
                e.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
                parts.Eyes.Add(e.transform);
                parts.EyeScales.Add(scale);
                if (!pupil) return;
                var p = Ball("Pupil", face, pos + new Vector3(0f, -0.01f, 0.03f), new Vector3(scale.x * 0.5f, scale.y * 0.5f, 0.04f), black);
                p.transform.localRotation = e.transform.localRotation;
                parts.Eyes.Add(p.transform);
                parts.EyeScales.Add(p.transform.localScale);
            }

            const float ez = 0.5f;
            switch (look.eyes)
            {
                case 1: // sleepy: half-closed
                    Eye(new Vector3(-0.14f, -0.05f, ez), new Vector3(0.16f, 0.09f, 0.07f), 0f);
                    Eye(new Vector3(0.14f, -0.05f, ez), new Vector3(0.16f, 0.09f, 0.07f), 0f);
                    break;
                case 2: // angry: slanted inward
                    Eye(new Vector3(-0.14f, -0.03f, ez), new Vector3(0.17f, 0.15f, 0.07f), -22f);
                    Eye(new Vector3(0.14f, -0.03f, ez), new Vector3(0.17f, 0.15f, 0.07f), 22f);
                    break;
                case 3: // happy: thin upturned arcs
                    Eye(new Vector3(-0.14f, -0.01f, ez), new Vector3(0.18f, 0.06f, 0.06f), 12f, false);
                    Eye(new Vector3(0.14f, -0.01f, ez), new Vector3(0.18f, 0.06f, 0.06f), -12f, false);
                    break;
                case 4: // big
                    Eye(new Vector3(-0.15f, -0.02f, ez), new Vector3(0.22f, 0.27f, 0.08f), 0f);
                    Eye(new Vector3(0.15f, -0.02f, ez), new Vector3(0.22f, 0.27f, 0.08f), 0f);
                    break;
                case 5: // cyclops
                    Eye(new Vector3(0f, -0.02f, ez + 0.01f), new Vector3(0.28f, 0.3f, 0.08f), 0f);
                    break;
                case 6: // visor band
                    Eye(new Vector3(0f, -0.03f, ez - 0.01f), new Vector3(0.56f, 0.09f, 0.08f), 0f, false);
                    break;
                case 7: // dots
                    Eye(new Vector3(-0.12f, -0.03f, ez), new Vector3(0.08f, 0.08f, 0.06f), 0f, false);
                    Eye(new Vector3(0.12f, -0.03f, ez), new Vector3(0.08f, 0.08f, 0.06f), 0f, false);
                    break;
                default: // round
                    Eye(new Vector3(-0.14f, -0.02f, ez), new Vector3(0.16f, 0.21f, 0.07f), 0f);
                    Eye(new Vector3(0.14f, -0.02f, ez), new Vector3(0.16f, 0.21f, 0.07f), 0f);
                    break;
            }

            // Mouth on the ball below the face plate.
            const float my = -0.33f, mz = 0.48f;
            switch (look.mouth)
            {
                case 1: // smile: three beads in an arc
                    for (int i = -1; i <= 1; i++) Ball("Mouth", face, new Vector3(i * 0.07f, my + (i == 0 ? -0.02f : 0f), mz), new Vector3(0.06f, 0.04f, 0.04f), black);
                    break;
                case 2: // grin
                    Ball("Mouth", face, new Vector3(0f, my, mz), new Vector3(0.24f, 0.08f, 0.05f), white);
                    Ball("Lip", face, new Vector3(0f, my, mz - 0.005f), new Vector3(0.27f, 0.1f, 0.04f), black);
                    break;
                case 3: // frown
                    for (int i = -1; i <= 1; i++) Ball("Mouth", face, new Vector3(i * 0.07f, my + (i == 0 ? 0.02f : 0f), mz), new Vector3(0.06f, 0.04f, 0.04f), black);
                    break;
                case 4: // gasp
                    Ball("Mouth", face, new Vector3(0f, my, mz), new Vector3(0.09f, 0.11f, 0.05f), black);
                    break;
                case 5: // fangs
                    Ball("Mouth", face, new Vector3(0f, my, mz), new Vector3(0.2f, 0.05f, 0.04f), black);
                    Ball("Fang", face, new Vector3(-0.05f, my - 0.04f, mz + 0.01f), new Vector3(0.035f, 0.07f, 0.03f), white);
                    Ball("Fang", face, new Vector3(0.05f, my - 0.04f, mz + 0.01f), new Vector3(0.035f, 0.07f, 0.03f), white);
                    break;
                case 6: // flat
                    Ball("Mouth", face, new Vector3(0f, my, mz), new Vector3(0.18f, 0.03f, 0.04f), black);
                    break;
                case 7: // tongue out
                    Ball("Mouth", face, new Vector3(0f, my, mz), new Vector3(0.16f, 0.06f, 0.04f), black);
                    Ball("Tongue", face, new Vector3(0.02f, my - 0.05f, mz + 0.01f), new Vector3(0.07f, 0.08f, 0.04f), Mats.Solid(new Color(1f, 0.4f, 0.5f)));
                    break;
            }

            switch (look.detail)
            {
                case 1: // blush
                    var blush = Mats.Solid(new Color(1f, 0.5f, 0.6f));
                    Ball("Blush", face, new Vector3(-0.27f, -0.25f, 0.41f), new Vector3(0.1f, 0.05f, 0.04f), blush);
                    Ball("Blush", face, new Vector3(0.27f, -0.25f, 0.41f), new Vector3(0.1f, 0.05f, 0.04f), blush);
                    break;
                case 2: // freckles
                    var freckle = Mats.Solid(Color.Lerp(team, Color.black, 0.45f));
                    foreach (var f in new[] { new Vector2(-0.27f, -0.22f), new Vector2(-0.22f, -0.27f), new Vector2(-0.31f, -0.27f), new Vector2(0.27f, -0.22f), new Vector2(0.22f, -0.27f), new Vector2(0.31f, -0.27f) })
                        Ball("Freckle", face, new Vector3(f.x, f.y, 0.4f), new Vector3(0.03f, 0.03f, 0.02f), freckle);
                    break;
                case 3: // mustache
                    var stache = Mats.Solid(new Color(0.15f, 0.1f, 0.07f));
                    Ball("Stache", face, new Vector3(-0.07f, -0.27f, 0.49f), new Vector3(0.14f, 0.05f, 0.05f), stache).transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                    Ball("Stache", face, new Vector3(0.07f, -0.27f, 0.49f), new Vector3(0.14f, 0.05f, 0.05f), stache).transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                    break;
                case 4: // glasses
                    var frame = Mats.Solid(new Color(0.12f, 0.12f, 0.14f));
                    foreach (float x in new[] { -0.14f, 0.14f })
                    {
                        Prim.Cube("Frame", face, new Vector3(x, 0.1f, 0.52f), new Vector3(0.22f, 0.025f, 0.02f), frame);
                        Prim.Cube("Frame", face, new Vector3(x, -0.14f, 0.52f), new Vector3(0.22f, 0.025f, 0.02f), frame);
                        Prim.Cube("Frame", face, new Vector3(x - 0.11f, -0.02f, 0.52f), new Vector3(0.025f, 0.26f, 0.02f), frame);
                        Prim.Cube("Frame", face, new Vector3(x + 0.11f, -0.02f, 0.52f), new Vector3(0.025f, 0.26f, 0.02f), frame);
                    }
                    Prim.Cube("Bridge", face, new Vector3(0f, 0.0f, 0.52f), new Vector3(0.06f, 0.025f, 0.02f), frame);
                    break;
                case 5: // scar
                    Prim.Cube("Scar", face, new Vector3(0.15f, -0.02f, 0.53f), new Vector3(0.025f, 0.32f, 0.02f), Mats.Solid(new Color(0.95f, 0.8f, 0.75f))).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                    break;
                case 6: // bandage cross on the cheek
                    var tape = Mats.Solid(new Color(0.95f, 0.88f, 0.75f));
                    Prim.Cube("Tape", face, new Vector3(0.28f, -0.24f, 0.41f), new Vector3(0.12f, 0.035f, 0.02f), tape).transform.localRotation = Quaternion.Euler(0f, 30f, 35f);
                    Prim.Cube("Tape", face, new Vector3(0.28f, -0.24f, 0.41f), new Vector3(0.12f, 0.035f, 0.02f), tape).transform.localRotation = Quaternion.Euler(0f, 30f, -35f);
                    break;
                case 7: // war-paint stripes under the eyes
                    var paint = Mats.Solid(Color.Lerp(team, Color.white, 0.6f));
                    foreach (float x in new[] { -0.2f, -0.13f, 0.13f, 0.2f })
                        Prim.Cube("Paint", face, new Vector3(x, -0.24f, 0.44f), new Vector3(0.025f, 0.09f, 0.02f), paint);
                    break;
            }
        }

        public static void SetHat(FighterParts parts, Color team, string id)
        {
            if (parts.HatRoot == null) return;
            for (int i = parts.HatRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parts.HatRoot.GetChild(i).gameObject);
            if (string.IsNullOrEmpty(id) || id == "none") return;
            try { Hats.Get(id).Build(parts.HatRoot, team); }
            catch (Exception e) { Debug.LogException(e); }
            foreach (var r in parts.HatRoot.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    /// <summary>Renders a rotating fighter to a texture for the character editor.</summary>
    public class CharacterPreview : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }
        public FighterParts Parts { get; private set; }
        Camera cam;
        Transform model;
        float spin, blink = 2f, lastDrag = -10f;
        /// <summary>Extra turn set by dragging the preview (degrees, 0 = facing the camera).</summary>
        public float UserYaw;

        /// <summary>Turn the preview by dragging (degrees).</summary>
        public void Drag(float degrees)
        {
            UserYaw = Mathf.Repeat(UserYaw + degrees + 180f, 360f) - 180f;
            lastDrag = Time.unscaledTime;
        }
        Color team;

        public static CharacterPreview Create(Color team, CharacterLook look)
        {
            var go = new GameObject("CharacterPreview");
            go.transform.position = new Vector3(0f, -600f, 0f);
            var p = go.AddComponent<CharacterPreview>();
            p.team = team;
            p.Texture = new RenderTexture(512, 512, 24) { antiAliasing = QualitySettings.antiAliasing > 0 ? 4 : 1 };
            p.cam = new GameObject("PreviewCamera").AddComponent<Camera>();
            p.cam.transform.SetParent(go.transform, false);
            p.cam.transform.localPosition = new Vector3(0f, 1.05f, 3.1f);
            p.cam.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
            p.cam.fieldOfView = 32f;
            p.cam.clearFlags = CameraClearFlags.SolidColor;
            p.cam.backgroundColor = new Color(0.1f, 0.11f, 0.16f);
            p.cam.targetTexture = p.Texture;
            p.cam.farClipPlane = 20f;
            p.cam.allowHDR = true;
            if (Mats.GlowAvailable) SpheresPostFX.Attach(p.cam);
            var light = new GameObject("Key").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 1.2f;
            light.transform.localPosition = new Vector3(1.5f, 2.5f, 2.5f);
            p.Rebuild(look);
            return p;
        }

        public void Rebuild(CharacterLook look)
        {
            if (model != null) Destroy(model.gameObject);
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            Parts = FighterModel.Build(model, team, look);
        }

        public void SetTeamColor(Color c, CharacterLook look)
        {
            team = c;
            Rebuild(look);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            spin += dt * 35f;
            if (model != null)
            {
                // The face is on the model's +Z side and the camera looks back along -Z, so 0 = facing you.
                // Drag in the editor to turn it; it gently sways when left alone.
                float sway = Time.unscaledTime - lastDrag > 2f ? Mathf.Sin(spin * Mathf.Deg2Rad * 2f) * 25f : 0f;
                model.localRotation = Quaternion.Euler(0f, UserYaw + sway, 0f);
                Parts.Rig.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f)) * 0.06f, 0f);
                float s = 1f + Mathf.Sin(Time.unscaledTime * 4.8f) * 0.025f;
                Parts.Rig.localScale = new Vector3(1f / Mathf.Sqrt(s), s, 1f / Mathf.Sqrt(s));
                blink -= dt;
                float e = 1f;
                if (blink < 0f) { e = 0.12f; if (blink < -0.12f) blink = UnityEngine.Random.Range(2f, 4.5f); }
                for (int i = 0; i < Parts.Eyes.Count; i++)
                {
                    var b = Parts.EyeScales[i];
                    Parts.Eyes[i].localScale = new Vector3(b.x, b.y * e, b.z);
                }
            }
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
