using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>A fighter's customizable face, body, gear and accessories (the glowing band always shows the team).</summary>
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
        /// <summary>Index into <see cref="Patterns"/>.</summary>
        public int pattern;
        /// <summary>Secondary color (pattern, feet, backpack): index into <see cref="SecondaryNames"/> (0 = auto).</summary>
        public int color2;
        /// <summary>Index into <see cref="Finishes"/>.</summary>
        public int finish;
        /// <summary>Index into <see cref="FeetStyles"/>.</summary>
        public int feet;
        /// <summary>Index into <see cref="BackItems"/>.</summary>
        public int back;
        /// <summary>Index into <see cref="BlasterSkins"/>.</summary>
        public int blaster;

        public static readonly string[] BodyColorNames = { "Cherry", "Tangerine", "Sunflower", "Lime", "Mint", "Teal", "Sky", "Cobalt", "Grape", "Bubblegum", "Snow", "Charcoal" };
        public static readonly Color[] BodyColors =
        {
            new Color(0.9f, 0.18f, 0.22f), new Color(1f, 0.5f, 0.12f), new Color(1f, 0.84f, 0.2f), new Color(0.55f, 0.9f, 0.2f),
            new Color(0.35f, 0.95f, 0.65f), new Color(0.1f, 0.7f, 0.7f), new Color(0.35f, 0.75f, 1f), new Color(0.2f, 0.32f, 0.95f),
            new Color(0.6f, 0.3f, 0.95f), new Color(1f, 0.45f, 0.75f), new Color(0.92f, 0.93f, 0.96f), new Color(0.2f, 0.21f, 0.25f),
        };

        /// <summary>The color your body is painted (team color unless you picked one).</summary>
        public Color BodyColorOr(Color team) => bodyColor >= 0 && bodyColor < BodyColors.Length ? BodyColors[bodyColor] : team;

        public static readonly string[] EyeStyles = { "Round", "Sleepy", "Angry", "Happy", "Big", "Cyclops", "Visor", "Dots", "Star", "Heart", "X", "Spiral", "Wide", "Squint" };
        public static readonly string[] MouthStyles = { "None", "Smile", "Grin", "Frown", "Gasp", "Fangs", "Flat", "Tongue", "Smirk", "Teeth", "Oh", "Zigzag", "Kitty", "Beak" };
        public static readonly string[] Details = { "None", "Blush", "Freckles", "Mustache", "Glasses", "Scar", "Bandage", "Stripes", "Sunglasses", "Eyepatch", "Beard", "Whiskers", "Bandaid", "Heart cheeks" };
        public static readonly string[] EyeColorNames = { "White", "Cyan", "Lime", "Gold", "Pink", "Red", "Violet", "Orange" };
        public static readonly Color[] EyeColors =
        {
            new Color(0.95f, 0.97f, 1f), new Color(0.35f, 0.95f, 1f), new Color(0.6f, 1f, 0.4f), new Color(1f, 0.85f, 0.3f),
            new Color(1f, 0.55f, 0.85f), new Color(1f, 0.35f, 0.3f), new Color(0.75f, 0.55f, 1f), new Color(1f, 0.6f, 0.2f),
        };

        public static readonly string[] Patterns = { "None", "Stripes", "Spots", "Two-tone", "Band", "Stars", "Zigzag", "Checker", "Belly", "Hearts" };
        public static readonly string[] Finishes = { "Glossy", "Matte", "Metallic", "Glow pattern", "Pearl" };
        public const int FinishGlossy = 0, FinishMatte = 1, FinishMetal = 2, FinishGlow = 3, FinishPearl = 4;
        public static readonly string[] FeetStyles = { "Round", "Boots", "Sneakers", "Hover", "Wheels", "Paws", "Flippers" };
        public static readonly string[] BackItems = { "Backpack", "Jetpack", "Cape", "Little wings", "Shell", "None", "Balloon" };
        public static readonly string[] BlasterSkins = { "Default", "Gold", "Chrome", "Wood", "Candy stripe", "Neon", "Toy" };

        /// <summary>Secondary palette. 0 = auto (a shade of the body), the last entry = your team color.</summary>
        public static readonly string[] SecondaryNames = { "Auto", "White", "Black", "Gold", "Silver", "Red", "Orange", "Yellow", "Lime", "Green", "Teal", "Sky", "Blue", "Purple", "Pink", "Brown", "Team" };
        public static readonly Color[] SecondaryColors =
        {
            Color.gray, new Color(0.95f, 0.95f, 0.97f), new Color(0.1f, 0.1f, 0.12f), new Color(1f, 0.78f, 0.25f), new Color(0.72f, 0.75f, 0.8f),
            new Color(0.88f, 0.15f, 0.18f), new Color(1f, 0.52f, 0.12f), new Color(1f, 0.9f, 0.25f), new Color(0.6f, 0.95f, 0.25f), new Color(0.2f, 0.65f, 0.3f),
            new Color(0.1f, 0.68f, 0.68f), new Color(0.4f, 0.78f, 1f), new Color(0.18f, 0.3f, 0.9f), new Color(0.55f, 0.28f, 0.9f), new Color(1f, 0.5f, 0.78f),
            new Color(0.45f, 0.28f, 0.15f), Color.white,
        };
        public const int SecondaryTeam = 16;

        /// <summary>Secondary color for feet and gear (auto = a darker shade of the body).</summary>
        public Color GearColor(Color team)
        {
            if (color2 == SecondaryTeam) return team;
            if (color2 > 0 && color2 < SecondaryColors.Length) return SecondaryColors[color2];
            return Color.Lerp(BodyColorOr(team), Color.black, 0.55f);
        }

        /// <summary>Pattern color (auto = a light tint of the body, or a dark one on very light bodies).</summary>
        public Color PatternColor(Color team)
        {
            if (color2 == SecondaryTeam) return team;
            if (color2 > 0 && color2 < SecondaryColors.Length) return SecondaryColors[color2];
            var b = BodyColorOr(team);
            float lum = b.r * 0.3f + b.g * 0.59f + b.b * 0.11f;
            return lum > 0.75f ? Color.Lerp(b, Color.black, 0.45f) : Color.Lerp(b, Color.white, 0.62f);
        }

        public CharacterLook Clone() => (CharacterLook)MemberwiseClone();

        /// <summary>
        /// Compact form for the network and match slots:
        /// "eyes.mouth.detail.color.hat.trail.pose.body.pattern.color2.finish.feet.back.blaster" (older codes are shorter).
        /// </summary>
        public string Encode() => eyes + "." + mouth + "." + detail + "." + eyeColor + "." + (hat ?? "none") + "." + trail + "." + pose + "." + bodyColor
                                  + "." + pattern + "." + color2 + "." + finish + "." + feet + "." + back + "." + blaster;

        public static CharacterLook Decode(string code)
        {
            var l = new CharacterLook();
            if (string.IsNullOrEmpty(code)) return l;
            var p = code.Split(new[] { '.' }, 14);
            if (p.Length > 0) int.TryParse(p[0], out l.eyes);
            if (p.Length > 1) int.TryParse(p[1], out l.mouth);
            if (p.Length > 2) int.TryParse(p[2], out l.detail);
            if (p.Length > 3) int.TryParse(p[3], out l.eyeColor);
            if (p.Length > 4) l.hat = p[4];
            if (p.Length > 5) int.TryParse(p[5], out l.trail);
            if (p.Length > 6) int.TryParse(p[6], out l.pose);
            if (p.Length > 7) { if (!int.TryParse(p[7], out l.bodyColor)) l.bodyColor = -1; }
            if (p.Length > 8) int.TryParse(p[8], out l.pattern);
            if (p.Length > 9) int.TryParse(p[9], out l.color2);
            if (p.Length > 10) int.TryParse(p[10], out l.finish);
            if (p.Length > 11) int.TryParse(p[11], out l.feet);
            if (p.Length > 12) int.TryParse(p[12], out l.back);
            if (p.Length > 13) int.TryParse(p[13], out l.blaster);
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
            pattern = Mathf.Clamp(pattern, 0, Patterns.Length - 1);
            color2 = Mathf.Clamp(color2, 0, SecondaryNames.Length - 1);
            finish = Mathf.Clamp(finish, 0, Finishes.Length - 1);
            feet = Mathf.Clamp(feet, 0, FeetStyles.Length - 1);
            back = Mathf.Clamp(back, 0, BackItems.Length - 1);
            blaster = Mathf.Clamp(blaster, 0, BlasterSkins.Length - 1);
        }

        /// <summary>Deterministic random look (bots), so a bot keeps its face between rounds and machines.</summary>
        public static CharacterLook ForSeed(string seed)
        {
            int h = 17;
            foreach (char c in seed ?? "") h = h * 31 + c;
            var rng = new System.Random(h);
            var l = new CharacterLook
            {
                eyes = rng.Next(EyeStyles.Length),
                mouth = rng.Next(MouthStyles.Length),
                detail = rng.Next(3) == 0 ? rng.Next(Details.Length) : 0,
                eyeColor = rng.Next(EyeColors.Length),
                hat = Hats.ForBot(seed),
            };
            // Body color stays the team color; everything else is fair game (plain options are a bit more likely).
            l.pattern = rng.Next(5) < 2 ? 0 : rng.Next(Patterns.Length);
            l.color2 = rng.Next(3) == 0 ? 0 : rng.Next(SecondaryNames.Length);
            l.finish = rng.Next(3) == 0 ? rng.Next(Finishes.Length) : FinishGlossy;
            l.feet = rng.Next(3) == 0 ? 0 : rng.Next(FeetStyles.Length);
            l.back = rng.Next(3) == 0 ? 0 : rng.Next(BackItems.Length);
            l.blaster = rng.Next(2) == 0 ? 0 : rng.Next(BlasterSkins.Length);
            l.Clamp();
            return l;
        }
    }

    /// <summary>References to a built fighter model, for animation.</summary>
    public class FighterParts
    {
        public Transform Rig, Head, Face, FootL, FootR, Gun, Muzzle, HatRoot;
        /// <summary>Rebuildable outfit roots (pattern shell, back item, hover glow).</summary>
        public Transform Outfit;
        public Renderer Body;
        /// <summary>The painted body color (hats may tint parts with it).</summary>
        public Color BodyColor;
        public bool HasBodyColor;
        public readonly List<Transform> Eyes = new List<Transform>();
        public readonly List<Vector3> EyeScales = new List<Vector3>();
    }

    /// <summary>
    /// Builds the round fighter: glossy ball body with an optional pattern, glowing team band, face plate,
    /// customizable eyes/mouth/details, feet, a skinned blaster, a back item and a hat. Shared by players, the
    /// editor preview, the podium and the menu showcase.
    /// </summary>
    public static partial class FighterModel
    {
        public static readonly Vector3 GunRest = new Vector3(0.55f, -0.25f, 0.3f);
        /// <summary>Feet rest positions (players and display fighters animate these roots).</summary>
        public static readonly Vector3 FootRestL = new Vector3(-0.22f, 0.09f, 0f), FootRestR = new Vector3(0.22f, 0.09f, 0f);

        /// <summary>The body material for a look (its body color or the team color, with its finish).</summary>
        public static Material BodyMaterial(Color team, CharacterLook look)
        {
            var c = look != null ? look.BodyColorOr(team) : team;
            int finish = look != null ? look.finish : 0;
            switch (finish)
            {
                case CharacterLook.FinishMatte: return Shape.Shiny(c, 0.12f, 0f);
                case CharacterLook.FinishMetal: return Shape.Shiny(Color.Lerp(c, Color.white, 0.12f), 0.82f, 0.62f);
                case CharacterLook.FinishPearl: return Shape.Shiny(Color.Lerp(c, Color.white, 0.25f), 0.93f, 0.08f);
                default: return Shape.Shiny(c, 0.62f, 0f);
            }
        }

        static Material Gloss(Color c, float g) => Shape.Shiny(c, g, 0f);

        static GameObject Ball(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadow = false)
        {
            var go = Prim.Sphere(name, parent, pos, 1f, mat);
            go.transform.localScale = scale;
            if (shadow) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static FighterParts Build(Transform model, Color team, CharacterLook look)
        {
            look = look ?? new CharacterLook();
            var parts = new FighterParts();
            var rig = new GameObject("Rig").transform;
            rig.SetParent(model, false);
            parts.Rig = rig;

            // Your chosen body color; the glowing band and blaster tip always show your team.
            parts.Body = Ball("Body", rig, new Vector3(0f, 0.7f, 0f), Vector3.one, BodyMaterial(team, look), true).GetComponent<Renderer>();
            parts.FootL = Shape.Pivot(rig, FootRestL, Vector3.zero, "FootL");
            parts.FootR = Shape.Pivot(rig, FootRestR, Vector3.zero, "FootR");
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
            parts.Muzzle = new GameObject("Muzzle").transform;
            parts.Muzzle.SetParent(gun, false);
            parts.Muzzle.localPosition = new Vector3(0f, 0f, 0.48f);

            parts.HatRoot = new GameObject("Hat").transform;
            parts.HatRoot.SetParent(head, false);
            parts.HatRoot.localPosition = new Vector3(0f, 0.25f, 0f);

            BuildFace(parts, team, look);
            SetHat(parts, team, look.hat);
            return parts;
        }

        // ------------------------------------------------------------------ face

        /// <summary>A point just outside the face (head space): on the body ball or the face plate, whichever is in front.</summary>
        static Vector3 FacePoint(float x, float y, float lift = 0.01f)
        {
            float ry = y + 0.25f;
            float sb = 0.25f - x * x - ry * ry;
            float zb = sb > 0f ? Mathf.Sqrt(sb) : 0f;
            float u = x / 0.37f, v = (y + 0.05f) / 0.22f;
            float sv = 1f - u * u - v * v;
            float zv = sv > 0f ? 0.27f + 0.25f * Mathf.Sqrt(sv) : -1f;
            return new Vector3(x, y, Mathf.Max(zb, zv) + lift);
        }

        /// <summary>Rotation facing out of the body ball at a head-space point.</summary>
        static Quaternion BodyNormal(Vector3 p, float roll = 0f)
        {
            var n = (p - new Vector3(0f, -0.25f, 0f)).normalized;
            return Quaternion.LookRotation(n, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>(Re)build the face (plate, eyes, mouth, details) and the outfit (pattern, feet, back item, blaster).</summary>
        public static void BuildFace(FighterParts parts, Color team, CharacterLook look)
        {
            look = look ?? new CharacterLook();
            look.Clamp();
            parts.BodyColor = look.BodyColorOr(team);
            parts.HasBodyColor = true;
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

            void Eye(Vector3 pos, Vector3 scale, float roll, bool pupil = true, float pupilSize = 0.5f)
            {
                var e = Ball("Eye", face, pos, scale, eyeMat);
                e.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
                parts.Eyes.Add(e.transform);
                parts.EyeScales.Add(scale);
                if (!pupil) return;
                var p = Ball("Pupil", face, pos + new Vector3(0f, -0.01f, 0.03f), new Vector3(scale.x * pupilSize, scale.y * pupilSize, 0.04f), black);
                p.transform.localRotation = e.transform.localRotation;
                parts.Eyes.Add(p.transform);
                parts.EyeScales.Add(p.transform.localScale);
            }

            // Composite eyes: a root that blinks as one.
            Transform EyeRoot(Vector3 pos)
            {
                var r = Shape.Pivot(face, pos, Vector3.zero, "Eye");
                parts.Eyes.Add(r);
                parts.EyeScales.Add(Vector3.one);
                return r;
            }

            // A ring part facing the viewer (+Z), rolled in its own plane.
            GameObject FrontRing(Transform p, Vector3 pos, float d, float thick, Material m, float sweep, float roll)
            {
                var g = Shape.Ring(p, pos, d, thick, m, Vector3.zero, sweep);
                g.transform.localRotation = Quaternion.Euler(0f, 0f, roll) * Quaternion.Euler(90f, 0f, 0f);
                return g;
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
                case 8: // stars
                    foreach (float x in new[] { -0.14f, 0.14f })
                        Shape.Star(EyeRoot(new Vector3(x, -0.02f, ez + 0.01f)), Vector3.zero, 0.23f, 0.05f, eyeMat, new Vector3(0f, 0f, x < 0f ? 8f : -8f));
                    break;
                case 9: // hearts
                    foreach (float x in new[] { -0.14f, 0.14f })
                        Shape.Heart(EyeRoot(new Vector3(x, -0.02f, ez + 0.01f)), Vector3.zero, 0.21f, 0.05f, eyeMat);
                    break;
                case 10: // X eyes
                    foreach (float x in new[] { -0.14f, 0.14f })
                    {
                        var r = EyeRoot(new Vector3(x, -0.02f, ez + 0.01f));
                        Shape.Box(r, Vector3.zero, new Vector3(0.17f, 0.035f, 0.04f), eyeMat, new Vector3(0f, 0f, 45f));
                        Shape.Box(r, Vector3.zero, new Vector3(0.17f, 0.035f, 0.04f), eyeMat, new Vector3(0f, 0f, -45f));
                    }
                    break;
                case 11: // dizzy spirals
                    foreach (float x in new[] { -0.14f, 0.14f })
                    {
                        var r = EyeRoot(new Vector3(x, -0.02f, ez + 0.01f));
                        float s = x < 0f ? 1f : -1f;
                        FrontRing(r, Vector3.zero, 0.18f, 0.026f, eyeMat, 290f, 20f * s);
                        FrontRing(r, new Vector3(0.012f * s, 0.01f, 0f), 0.1f, 0.024f, eyeMat, 270f, 200f * s);
                        Shape.Ball(r, new Vector3(0f, 0.005f, 0f), 0.035f, eyeMat);
                    }
                    break;
                case 12: // wide, tiny pupils
                    Eye(new Vector3(-0.155f, -0.02f, ez), new Vector3(0.19f, 0.24f, 0.07f), 0f, true, 0.3f);
                    Eye(new Vector3(0.155f, -0.02f, ez), new Vector3(0.19f, 0.24f, 0.07f), 0f, true, 0.3f);
                    break;
                case 13: // squint: > <
                    foreach (float x in new[] { -0.14f, 0.14f })
                    {
                        var r = EyeRoot(new Vector3(x, -0.03f, ez + 0.01f));
                        float s = x > 0f ? 1f : -1f;
                        Shape.Box(r, new Vector3(0.025f * s, 0.028f, 0f), new Vector3(0.13f, 0.032f, 0.04f), eyeMat, new Vector3(0f, 0f, 24f * s));
                        Shape.Box(r, new Vector3(0.025f * s, -0.028f, 0f), new Vector3(0.13f, 0.032f, 0.04f), eyeMat, new Vector3(0f, 0f, -24f * s));
                    }
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
                case 8: // smirk: one corner up
                    Ball("Mouth", face, new Vector3(-0.08f, my - 0.005f, mz - 0.005f), new Vector3(0.05f, 0.035f, 0.04f), black);
                    Ball("Mouth", face, new Vector3(-0.015f, my - 0.015f, mz), new Vector3(0.06f, 0.04f, 0.04f), black);
                    Ball("Mouth", face, new Vector3(0.05f, my - 0.005f, mz), new Vector3(0.06f, 0.04f, 0.04f), black);
                    Ball("Mouth", face, new Vector3(0.1f, my + 0.025f, mz - 0.01f), new Vector3(0.05f, 0.04f, 0.04f), black);
                    break;
                case 9: // teeth
                    Ball("Lip", face, new Vector3(0f, my, mz - 0.005f), new Vector3(0.27f, 0.11f, 0.04f), black);
                    Ball("Teeth", face, new Vector3(0f, my, mz), new Vector3(0.24f, 0.085f, 0.05f), white);
                    foreach (float x in new[] { -0.06f, 0f, 0.06f })
                        Shape.Box(face, new Vector3(x, my, mz + 0.02f), new Vector3(0.008f, 0.075f, 0.02f), black);
                    Shape.Box(face, new Vector3(0f, my, mz + 0.022f), new Vector3(0.2f, 0.008f, 0.02f), black);
                    break;
                case 10: // "oh"
                    FrontRing(face, new Vector3(0f, my, mz + 0.01f), 0.1f, 0.03f, black, 360f, 0f);
                    Ball("Mouth", face, new Vector3(0f, my, mz), new Vector3(0.075f, 0.075f, 0.03f), Mats.Solid(new Color(0.45f, 0.08f, 0.12f)));
                    break;
                case 11: // zigzag
                {
                    var pts = new[] { new Vector3(-0.12f, my + 0.01f, mz - 0.01f), new Vector3(-0.06f, my - 0.018f, mz), new Vector3(0f, my + 0.012f, mz + 0.004f),
                                      new Vector3(0.06f, my - 0.018f, mz), new Vector3(0.12f, my + 0.01f, mz - 0.01f) };
                    for (int i = 0; i < pts.Length - 1; i++) Shape.Rod(face, pts[i], pts[i + 1], 0.022f, black);
                    break;
                }
                case 12: // kitty: a tiny nose over two little "u"s
                    FrontRing(face, new Vector3(-0.035f, my + 0.005f, mz + 0.005f), 0.07f, 0.018f, black, 180f, 0f);
                    FrontRing(face, new Vector3(0.035f, my + 0.005f, mz + 0.005f), 0.07f, 0.018f, black, 180f, 0f);
                    Shape.Spike(face, new Vector3(0f, my + 0.08f, mz - 0.005f), new Vector3(0.05f, 0.04f, 0.025f), Mats.Solid(new Color(1f, 0.5f, 0.6f)), new Vector3(0f, 0f, 180f));
                    break;
                case 13: // beak
                    Shape.Spike(face, new Vector3(0f, my + 0.02f, mz - 0.03f), new Vector3(0.15f, 0.13f, 0.07f), Mats.Solid(new Color(1f, 0.62f, 0.15f)), new Vector3(90f, 0f, 0f));
                    Shape.Spike(face, new Vector3(0f, my - 0.02f, mz - 0.03f), new Vector3(0.1f, 0.08f, 0.04f), Mats.Solid(new Color(0.9f, 0.48f, 0.1f)), new Vector3(90f, 0f, 0f));
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
                    var freckle = Mats.Solid(Color.Lerp(parts.BodyColor, Color.black, 0.45f));
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
                case 8: // sunglasses
                {
                    var lens = Shape.Shiny(new Color(0.04f, 0.04f, 0.06f), 0.97f, 0.3f);
                    var glint = Mats.Solid(new Color(0.85f, 0.9f, 1f));
                    foreach (float x in new[] { -0.145f, 0.145f })
                    {
                        Shape.Ball(face, new Vector3(x, -0.02f, 0.548f), new Vector3(0.25f, 0.17f, 0.05f), lens);
                        Shape.Box(face, new Vector3(x - 0.05f, 0.025f, 0.575f), new Vector3(0.05f, 0.012f, 0.01f), glint, new Vector3(0f, 0f, 20f));
                        float s = Mathf.Sign(x);
                        Shape.Rod(face, new Vector3(0.26f * s, 0.0f, 0.5f), FacePoint(0.42f * s, 0.0f, 0.0f), 0.025f, lens);
                    }
                    Shape.Box(face, new Vector3(0f, 0.01f, 0.56f), new Vector3(0.07f, 0.025f, 0.02f), lens);
                    break;
                }
                case 9: // eyepatch with a strap across the face plate
                {
                    var patch = Mats.Solid(new Color(0.06f, 0.06f, 0.07f));
                    Shape.Ball(face, new Vector3(0.14f, -0.02f, 0.55f), new Vector3(0.21f, 0.2f, 0.05f), patch);
                    var path = new[] { new Vector2(0.44f, -0.16f), new Vector2(0.3f, -0.08f), new Vector2(0.14f, -0.02f), new Vector2(-0.02f, 0.06f), new Vector2(-0.18f, 0.12f), new Vector2(-0.3f, 0.12f), new Vector2(-0.38f, 0.08f) };
                    for (int i = 0; i < path.Length - 1; i++)
                        Shape.Rod(face, FacePoint(path[i].x, path[i].y, 0.012f), FacePoint(path[i + 1].x, path[i + 1].y, 0.012f), 0.03f, patch);
                    break;
                }
                case 10: // beard
                {
                    var hair = Shape.Shiny(new Color(0.28f, 0.17f, 0.09f), 0.15f);
                    var pts = new[]
                    {
                        new Vector2(-0.32f, -0.3f), new Vector2(0.32f, -0.3f), new Vector2(-0.27f, -0.4f), new Vector2(0.27f, -0.4f),
                        new Vector2(-0.18f, -0.42f), new Vector2(0.18f, -0.42f), new Vector2(-0.09f, -0.45f), new Vector2(0.09f, -0.45f), new Vector2(0f, -0.46f),
                        new Vector2(-0.2f, -0.5f), new Vector2(0.2f, -0.5f), new Vector2(-0.1f, -0.54f), new Vector2(0.1f, -0.54f), new Vector2(0f, -0.56f),
                        new Vector2(-0.05f, -0.63f), new Vector2(0.05f, -0.63f),
                    };
                    foreach (var p in pts)
                    {
                        var at = FacePoint(p.x, p.y, -0.02f);
                        Shape.Ball(face, at, new Vector3(0.13f, 0.11f, 0.08f), hair).transform.localRotation = BodyNormal(at);
                    }
                    break;
                }
                case 11: // whiskers
                {
                    var wh = Mats.Solid(new Color(0.93f, 0.93f, 0.95f));
                    foreach (float s in new[] { -1f, 1f })
                        for (int k = -1; k <= 1; k++)
                            Shape.Rod(face, FacePoint(0.15f * s, -0.29f + k * 0.035f, 0.005f), FacePoint(0.36f * s, -0.27f + k * 0.07f, 0.06f), 0.012f, wh);
                    Shape.Ball(face, new Vector3(0f, -0.27f, 0.5f), new Vector3(0.06f, 0.04f, 0.04f), Mats.Solid(new Color(1f, 0.5f, 0.6f)));
                    break;
                }
                case 12: // bandaid on the cheek
                {
                    var at = FacePoint(-0.27f, -0.22f, 0.004f);
                    var rot = BodyNormal(at, -28f);
                    var b = Shape.Pivot(face, at, Vector3.zero, "Bandaid");
                    b.localRotation = rot;
                    Shape.Box(b, Vector3.zero, new Vector3(0.17f, 0.06f, 0.012f), Mats.Solid(new Color(0.96f, 0.78f, 0.6f)));
                    Shape.Box(b, new Vector3(0f, 0f, 0.004f), new Vector3(0.06f, 0.048f, 0.012f), Mats.Solid(new Color(1f, 0.9f, 0.82f)));
                    var dot = Mats.Solid(new Color(0.8f, 0.6f, 0.45f));
                    foreach (float x in new[] { -0.06f, 0.06f })
                        Shape.Box(b, new Vector3(x, 0f, 0.004f), new Vector3(0.012f, 0.012f, 0.012f), dot);
                    break;
                }
                case 13: // heart cheeks
                {
                    var pink = Mats.Solid(new Color(1f, 0.42f, 0.6f));
                    foreach (float x in new[] { -0.27f, 0.27f })
                    {
                        var at = FacePoint(x, -0.24f, 0.005f);
                        Shape.Heart(face, at, 0.1f, 0.02f, pink).transform.localRotation = BodyNormal(at);
                    }
                    break;
                }
            }

            BuildOutfit(parts, team, look);
        }

        /// <summary>Swap the hat (tinted with the fighter's body color when the hat uses one).</summary>
        public static void SetHat(FighterParts parts, Color team, string id)
        {
            if (parts.HatRoot == null) return;
            for (int i = parts.HatRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parts.HatRoot.GetChild(i).gameObject);
            if (string.IsNullOrEmpty(id) || id == "none") return;
            try { Hats.Get(id).Build(parts.HatRoot, parts.HasBodyColor ? parts.BodyColor : team); }
            catch (Exception e) { Debug.LogException(e); }
            foreach (var r in parts.HatRoot.GetComponentsInChildren<MeshRenderer>())
            {
                // Glass and glow parts don't cast shadows.
                var m = r.sharedMaterial;
                bool see = m != null && m.renderQueue >= 2900;
                r.shadowCastingMode = see ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
    }

    /// <summary>Renders a turnable fighter to a texture for the character editor.</summary>
    public class CharacterPreview : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }
        public FighterParts Parts { get; private set; }
        Camera cam;
        Transform model;
        float spin, blink = 2f, lastDrag = -10f, poseTime;
        /// <summary>Extra turn set by dragging the preview (degrees, 0 = facing the camera).</summary>
        public float UserYaw;
        /// <summary>Victory pose to play (-1 = idle bob).</summary>
        public int Pose = -1;
        int trail;

        /// <summary>Turn the preview by dragging (degrees).</summary>
        public void Drag(float degrees)
        {
            UserYaw = Mathf.Repeat(UserYaw + degrees + 180f, 360f) - 180f;
            lastDrag = Time.unscaledTime;
        }
        Color team;

        /// <summary>The team color used by the next rebuild.</summary>
        public Color Team { get => team; set => team = value; }

        public static CharacterPreview Create(Color team, CharacterLook look)
        {
            var go = new GameObject("CharacterPreview");
            go.transform.position = new Vector3(0f, -600f, 0f);
            var p = go.AddComponent<CharacterPreview>();
            p.team = team;
            p.Texture = new RenderTexture(512, 512, 24) { antiAliasing = QualitySettings.antiAliasing > 0 ? 4 : 1 };
            p.cam = new GameObject("PreviewCamera").AddComponent<Camera>();
            p.cam.transform.SetParent(go.transform, false);
            // Framed so tall hats, balloons and hovering poses stay in view.
            p.cam.transform.localPosition = new Vector3(0f, 1.12f, 3.75f);
            p.cam.transform.localRotation = Quaternion.Euler(2f, 180f, 0f);
            p.cam.fieldOfView = 34f;
            p.cam.clearFlags = CameraClearFlags.SolidColor;
            p.cam.backgroundColor = new Color(0.1f, 0.11f, 0.16f);
            p.cam.targetTexture = p.Texture;
            p.cam.farClipPlane = 20f;
            p.cam.allowHDR = true;
            if (Mats.GlowAvailable) SpheresPostFX.Attach(p.cam);
            var light = new GameObject("Key").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 1.2f;
            light.transform.localPosition = new Vector3(1.5f, 2.8f, 2.7f);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.transform.SetParent(go.transform, false);
            rim.type = LightType.Point;
            rim.range = 6f;
            rim.intensity = 0.6f;
            rim.color = new Color(0.7f, 0.8f, 1f);
            rim.transform.localPosition = new Vector3(-1.8f, 1.8f, -1.5f);
            p.Rebuild(look);
            return p;
        }

        public void Rebuild(CharacterLook look)
        {
            if (model != null) Destroy(model.gameObject);
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            Parts = FighterModel.Build(model, team, look);
            if (trail > 0) Cosmetics.ApplyTrail(Parts.Rig, trail, team);
        }

        public void SetTeamColor(Color c, CharacterLook look)
        {
            team = c;
            Rebuild(look);
        }

        /// <summary>Show a movement trail on the preview (0 = none).</summary>
        public void SetTrail(int index)
        {
            if (index == trail) return;
            trail = index;
            if (Parts != null) Cosmetics.ApplyTrail(Parts.Rig, trail, team);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            spin += dt * 35f;
            if (model == null || Parts == null) return;
            // The face is on the model's +Z side and the camera looks back along -Z, so 0 = facing you.
            // Drag in the editor to turn it; it gently sways when left alone.
            float sway = Time.unscaledTime - lastDrag > 2f && Pose < 0 ? Mathf.Sin(spin * Mathf.Deg2Rad * 2f) * 25f : 0f;
            model.localRotation = Quaternion.Euler(0f, UserYaw + sway, 0f);
            if (Pose >= 0)
            {
                poseTime += dt;
                Cosmetics.AnimatePose(Parts.Rig, Pose, poseTime);
            }
            else
            {
                Parts.Rig.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f)) * 0.06f, 0f);
                Parts.Rig.localRotation = Quaternion.identity;
                float s = 1f + Mathf.Sin(Time.unscaledTime * 4.8f) * 0.025f;
                Parts.Rig.localScale = new Vector3(1f / Mathf.Sqrt(s), s, 1f / Mathf.Sqrt(s));
            }
            blink -= dt;
            float e = 1f;
            if (blink < 0f) { e = 0.12f; if (blink < -0.12f) blink = UnityEngine.Random.Range(2f, 4.5f); }
            for (int i = 0; i < Parts.Eyes.Count; i++)
            {
                if (Parts.Eyes[i] == null) continue;
                var b = Parts.EyeScales[i];
                Parts.Eyes[i].localScale = new Vector3(b.x, b.y * e, b.z);
            }
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
