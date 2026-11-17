using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>Body patterns, feet, back items and blaster skins for the fighter model.</summary>
    public static partial class FighterModel
    {
        const float ShellRadius = 0.507f;
        static readonly Vector3 BodyCenter = new Vector3(0f, 0.7f, 0f);
        /// <summary>Patterns leave the face uncovered: directions within this cone around the face stay bare.</summary>
        static readonly Vector3 FaceDir = new Vector3(0f, 0.15f, 1f).normalized;
        const float FaceCone = 42f;
        static readonly Dictionary<int, Mesh> patternMeshes = new Dictionary<int, Mesh>();

        static bool InFace(Vector3 d, float margin = 0f) => Vector3.Angle(d, FaceDir) < FaceCone + margin;

        /// <summary>(Re)build pattern, feet, back item and blaster for a look.</summary>
        public static void BuildOutfit(FighterParts parts, Color team, CharacterLook look)
        {
            if (parts == null || parts.Rig == null) return;
            if (parts.Outfit != null) Object.Destroy(parts.Outfit.gameObject);
            parts.Outfit = Shape.Pivot(parts.Rig, Vector3.zero, Vector3.zero, "Outfit");
            var body = look.BodyColorOr(team);
            var gear = look.GearColor(team);

            // ---- pattern
            if (look.pattern > 0)
            {
                var mesh = PatternMesh(look.pattern);
                if (mesh != null)
                {
                    var pc = look.PatternColor(team);
                    Material pm;
                    switch (look.finish)
                    {
                        case CharacterLook.FinishGlow: pm = Shape.Glow(pc, 1.7f, 0f); break;
                        case CharacterLook.FinishMatte: pm = Shape.Shiny(pc, 0.12f); break;
                        case CharacterLook.FinishMetal: pm = Shape.Shiny(pc, 0.82f, 0.62f); break;
                        case CharacterLook.FinishPearl: pm = Shape.Shiny(Color.Lerp(pc, Color.white, 0.2f), 0.93f, 0.08f); break;
                        default: pm = Shape.Shiny(pc, 0.62f); break;
                    }
                    var go = Prim.Create(mesh, "Pattern", parts.Outfit, BodyCenter, Vector3.one, pm);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            BuildFeet(parts, team, look, body, gear);
            BuildBack(parts, team, look, gear);
            BuildBlaster(parts, team, look, body);
        }

        // ------------------------------------------------------------------ patterns

        static Mesh PatternMesh(int pattern)
        {
            if (patternMeshes.TryGetValue(pattern, out var cached) && cached != null) return cached;
            var b = new Shape.Builder();
            const float R = ShellRadius;
            System.Func<float, float> K(float v) => _ => v;
            // Skip whole columns whose middle is on the face.
            System.Func<Vector3, bool> face = d => InFace(d);
            switch (pattern)
            {
                case 1: // stripes
                    foreach (var s in new[] { new Vector2(-64f, -54f), new Vector2(-40f, -29f), new Vector2(22f, 32f), new Vector2(46f, 56f), new Vector2(68f, 76f) })
                        Shape.ShellBand(b, R, K(s.x), K(s.y), -180f, 180f, 72, 2, s.y > -16f ? face : null);
                    break;
                case 2: // spots
                    foreach (var (d, r) in Scatter(26, 1.1f, 10f, 3))
                        Shape.ShellDecal(b, R, d, 0f, Shape.CircleOutline(r, 0.9f, 18));
                    break;
                case 3: // two-tone: bottom half
                    Shape.ShellBand(b, R, K(-90f), K(-17f), -180f, 180f, 48, 8);
                    Shape.ShellBand(b, R, K(-17f), K(-5f), -180f, 180f, 48, 2, face);
                    break;
                case 4: // belt band with thin trims
                    Shape.ShellBand(b, R, K(-36f), K(-19f), -180f, 180f, 72, 3);
                    Shape.ShellBand(b, R, K(-58f), K(-55f), -180f, 180f, 72, 1);
                    Shape.ShellBand(b, R, K(28f), K(32f), -180f, 180f, 72, 1, face);
                    break;
                case 5: // stars and sparkles
                {
                    int i = 0;
                    foreach (var (d, r) in Scatter(22, 0.4f, 11f, 7))
                    {
                        bool sparkle = i++ % 3 == 2;
                        Shape.ShellDecal(b, R, d, (i * 37f) % 72f, sparkle ? Shape.StarOutline(r * 0.85f, 4, 0.28f) : Shape.StarOutline(r, 5, 0.45f));
                    }
                    break;
                }
                case 6: // zigzags
                {
                    float P = 360f / 14f;
                    float Tri(float lon) { float f = Mathf.Repeat(lon / P, 1f); return 2f * Mathf.Abs(2f * f - 1f) - 1f; }
                    Shape.ShellBand(b, R, lon => -35f + Tri(lon) * 7f, lon => -25f + Tri(lon) * 7f, -180f, 180f, 56, 2);
                    float P2 = 360f / 18f;
                    float Tri2(float lon) { float f = Mathf.Repeat(lon / P2, 1f); return 2f * Mathf.Abs(2f * f - 1f) - 1f; }
                    Shape.ShellBand(b, R, lon => 40f + Tri2(lon) * 5f, lon => 46f + Tri2(lon) * 5f, -180f, 180f, 72, 2, face);
                    Shape.ShellBand(b, R, lon => -60f - Tri2(lon) * 4f, lon => -55f - Tri2(lon) * 4f, -180f, 180f, 72, 2);
                    break;
                }
                case 7: // checker
                {
                    float[] lats = { -82f, -64f, -46f, -28f, -9f, 9f, 28f, 46f, 64f, 82f };
                    for (int row = 0; row < lats.Length - 1; row++)
                    {
                        if (row == 4) continue; // the team band
                        for (int col = 0; col < 12; col++)
                        {
                            if ((row + col) % 2 != 0) continue;
                            float lo0 = -180f + col * 30f;
                            var mid = Shape.Dir((lats[row] + lats[row + 1]) * 0.5f, lo0 + 15f);
                            if (InFace(mid, 6f)) continue;
                            Shape.ShellBand(b, R, K(lats[row]), K(lats[row + 1]), lo0, lo0 + 30f, 5, 3);
                        }
                    }
                    break;
                }
                case 8: // belly patch
                    Shape.ShellDecal(b, R, Shape.Dir(-52f, 0f), 0f, Shape.CircleOutline(36f, 0.78f, 28));
                    break;
                case 9: // hearts
                {
                    var outline = Shape.HeartOutline(24);
                    int i = 0;
                    foreach (var (d, r) in Scatter(24, 1f, 11f, 11))
                    {
                        var o = new Vector2[outline.Length];
                        for (int k = 0; k < o.Length; k++) o[k] = outline[k] * r * 2.1f;
                        Shape.ShellDecal(b, R, d, ((i++ % 3) - 1) * 18f, o);
                    }
                    break;
                }
                default:
                    return null;
            }
            var mesh = b.Build("Pattern" + pattern);
            patternMeshes[pattern] = mesh;
            return mesh;
        }

        /// <summary>Evenly spread directions (golden spiral) off the face and off the team band, with a radius each.</summary>
        static IEnumerable<(Vector3, float)> Scatter(int n, float twist, float radius, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / n;
                float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float a = i * 2.39996f + twist;
                var d = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                float r = radius * (0.75f + (float)rng.NextDouble() * 0.5f);
                if (InFace(d, r + 2f)) continue;
                if (Mathf.Abs(Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg) < r * 0.9f + 3f) continue; // keep the band clear
                yield return (d, r);
            }
        }

        // ------------------------------------------------------------------ feet

        static void BuildFeet(FighterParts parts, Color team, CharacterLook look, Color body, Color gear)
        {
            foreach (var foot in new[] { parts.FootL, parts.FootR })
            {
                if (foot == null) continue;
                for (int i = foot.childCount - 1; i >= 0; i--) Object.Destroy(foot.GetChild(i).gameObject);
                float s = foot == parts.FootL ? -1f : 1f;
                switch (look.feet)
                {
                    case 1: // boots
                    {
                        var boot = Shape.Shiny(look.color2 == 0 ? new Color(0.42f, 0.26f, 0.14f) : gear, 0.4f);
                        var sole = Mats.Solid(new Color(0.12f, 0.1f, 0.09f));
                        Shape.Ball(foot, new Vector3(0f, 0f, 0.035f), new Vector3(0.27f, 0.17f, 0.37f), boot);
                        Shape.Cyl(foot, new Vector3(0f, 0f, -0.03f), 0.21f, 0.15f, boot);
                        Shape.Ring(foot, new Vector3(0f, 0.15f, -0.03f), 0.21f, 0.045f, Shape.Shiny(Color.Lerp(boot.color, Color.white, 0.35f), 0.4f));
                        Shape.Box(foot, new Vector3(0f, -0.07f, 0.035f), new Vector3(0.26f, 0.04f, 0.38f), sole);
                        Shape.Box(foot, new Vector3(0f, -0.068f, -0.11f), new Vector3(0.2f, 0.05f, 0.1f), sole);
                        break;
                    }
                    case 2: // sneakers
                    {
                        var shoe = Shape.Shiny(new Color(0.96f, 0.96f, 0.98f), 0.5f);
                        var accent = Shape.Shiny(look.color2 == 0 ? team : gear, 0.6f);
                        Shape.Ball(foot, new Vector3(0f, 0.01f, 0.03f), new Vector3(0.26f, 0.16f, 0.38f), shoe);
                        Shape.Box(foot, new Vector3(0f, -0.065f, 0.03f), new Vector3(0.27f, 0.05f, 0.4f), Mats.Solid(new Color(0.85f, 0.86f, 0.9f)));
                        Shape.Ball(foot, new Vector3(0f, -0.005f, 0.17f), new Vector3(0.2f, 0.1f, 0.13f), accent);
                        Shape.Box(foot, new Vector3(0.125f * s, 0.01f, 0f), new Vector3(0.015f, 0.05f, 0.2f), accent, new Vector3(-12f, 0f, 0f));
                        Shape.Box(foot, new Vector3(0f, -0.02f, -0.15f), new Vector3(0.16f, 0.07f, 0.04f), accent);
                        var lace = Mats.Solid(new Color(0.3f, 0.32f, 0.36f));
                        Shape.Box(foot, new Vector3(0f, 0.085f, 0.0f), new Vector3(0.1f, 0.015f, 0.02f), lace);
                        Shape.Box(foot, new Vector3(0f, 0.08f, 0.05f), new Vector3(0.1f, 0.015f, 0.02f), lace);
                        break;
                    }
                    case 3: // hover: no feet (the glow ring is built below)
                        break;
                    case 4: // wheels
                    {
                        var wheel = Shape.Pivot(foot, new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0f, 90f), "Wheel");
                        var rubber = Shape.Shiny(new Color(0.12f, 0.12f, 0.14f), 0.25f);
                        var hub = Shape.Shiny(look.color2 == 0 ? new Color(0.8f, 0.82f, 0.88f) : gear, 0.85f, 0.5f);
                        Shape.Cyl(wheel, new Vector3(0f, -0.055f, 0f), 0.24f, 0.11f, rubber);
                        Shape.Cyl(wheel, new Vector3(0f, -0.06f, 0f), 0.13f, 0.12f, hub);
                        Shape.Ball(wheel, new Vector3(0f, -0.065f * s, 0f), new Vector3(0.06f, 0.02f, 0.06f), Mats.Solid(new Color(0.2f, 0.2f, 0.22f)));
                        Shape.Box(wheel, new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.125f, 0.03f), hub);
                        wheel.gameObject.AddComponent<Roller>().Radius = 0.12f;
                        Shape.Ball(foot, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.08f, 0.16f), Shape.Shiny(gear, 0.5f));
                        break;
                    }
                    case 5: // paws
                    {
                        var fur = Shape.Shiny(body, 0.35f);
                        var bean = Mats.Solid(new Color(1f, 0.55f, 0.65f));
                        Shape.Ball(foot, new Vector3(0f, 0f, 0.02f), new Vector3(0.28f, 0.17f, 0.32f), fur);
                        for (int k = -1; k <= 1; k++)
                            Shape.Ball(foot, new Vector3(k * 0.075f, 0.02f, 0.16f - Mathf.Abs(k) * 0.02f), new Vector3(0.09f, 0.08f, 0.09f), fur);
                        Shape.Ball(foot, new Vector3(0f, -0.07f, 0.0f), new Vector3(0.14f, 0.03f, 0.12f), bean);
                        for (int k = -1; k <= 1; k++)
                            Shape.Ball(foot, new Vector3(k * 0.075f, -0.055f, 0.15f), new Vector3(0.05f, 0.025f, 0.05f), bean);
                        break;
                    }
                    case 6: // flippers
                    {
                        var rub = Shape.Shiny(look.color2 == 0 ? new Color(1f, 0.55f, 0.15f) : gear, 0.55f);
                        Shape.Ball(foot, new Vector3(0f, 0.02f, -0.02f), new Vector3(0.18f, 0.15f, 0.2f), rub);
                        Shape.Spike(foot, new Vector3(0f, -0.065f, 0.3f), new Vector3(0.34f, 0.44f, 0.04f), rub, new Vector3(-90f, 0f, 0f));
                        break;
                    }
                    default: // round
                        Shape.Ball(foot, Vector3.zero, new Vector3(0.26f, 0.18f, 0.34f), Mats.Solid(gear));
                        break;
                }
            }

            if (look.feet == 3)
            {
                var hover = Shape.Pivot(parts.Outfit, new Vector3(0f, 0.12f, 0f), Vector3.zero, "Hover");
                Shape.Ring(hover, Vector3.zero, 0.56f, 0.05f, Shape.Glow(team, 2.4f));
                Shape.Ring(hover, new Vector3(0f, -0.05f, 0f), 0.36f, 0.035f, Shape.Glow(Color.Lerp(team, Color.white, 0.4f), 2f));
                Shape.Ball(hover, new Vector3(0f, -0.06f, 0f), new Vector3(0.5f, 0.03f, 0.5f), Mats.GlowAvailable ? Mats.Glow(new Color(team.r, team.g, team.b, 0.5f), 1.6f, 1.2f) : Mats.Solid(team));
                var w = hover.gameObject.AddComponent<Wobble>();
                w.Move = new Vector3(0f, 0.025f, 0f);
                w.Pulse = 0.06f;
                w.Speed = 4f;
            }
        }

        // ------------------------------------------------------------------ back items

        static void BuildBack(FighterParts parts, Color team, CharacterLook look, Color gear)
        {
            var root = Shape.Pivot(parts.Outfit, Vector3.zero, Vector3.zero, "Back");
            switch (look.back)
            {
                case 1: // jetpack
                {
                    var metal = Shape.Shiny(new Color(0.62f, 0.65f, 0.72f), 0.8f, 0.55f);
                    var tank = Shape.Shiny(look.color2 == 0 ? Color.Lerp(team, Color.white, 0.15f) : gear, 0.75f, 0.3f);
                    Shape.Box(root, new Vector3(0f, 0.8f, -0.5f), new Vector3(0.3f, 0.32f, 0.14f), metal);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var x = 0.14f * s;
                        Shape.Cyl(root, new Vector3(x, 0.56f, -0.57f), 0.17f, 0.42f, tank);
                        Shape.Cone(root, new Vector3(x, 0.98f, -0.57f), 0.17f, 0.12f, tank);
                        Shape.Ring(root, new Vector3(x, 0.72f, -0.57f), 0.18f, 0.03f, metal);
                        Shape.Cone(root, new Vector3(x, 0.49f, -0.57f), 0.15f, 0.08f, metal, default, 0.6f);
                        var flame = Shape.Pivot(root, new Vector3(x, 0.49f, -0.57f), new Vector3(180f, 0f, 0f), "Flame");
                        Shape.Cone(flame, Vector3.zero, 0.11f, 0.24f, Shape.Glow(Color.Lerp(team, new Color(1f, 0.8f, 0.4f), 0.4f), 2.6f));
                        Shape.Cone(flame, Vector3.zero, 0.06f, 0.14f, Shape.Glow(new Color(1f, 0.95f, 0.8f), 2.8f));
                        var w = flame.gameObject.AddComponent<Wobble>();
                        w.Flicker = true;
                        w.Pulse = 0.18f;
                        w.Speed = 9f;
                    }
                    break;
                }
                case 2: // cape
                {
                    var cloth = Shape.Shiny(look.color2 == 0 ? team : gear, 0.3f);
                    var lining = Shape.Shiny(Color.Lerp(cloth.color, Color.black, 0.35f), 0.3f);
                    var pivot = Shape.Pivot(root, new Vector3(0f, 1.0f, -0.34f), new Vector3(30f, 0f, 0f), "Cape");
                    Shape.Box(pivot, new Vector3(0f, -0.4f, 0f), new Vector3(0.32f, 0.8f, 0.025f), cloth);
                    Shape.Box(pivot, new Vector3(0f, -0.4f, 0.014f), new Vector3(0.3f, 0.78f, 0.01f), lining);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shape.Box(pivot, new Vector3(0.27f * s, -0.38f, 0.05f), new Vector3(0.26f, 0.76f, 0.025f), cloth, new Vector3(0f, -26f * s, 0f));
                        Shape.Ball(root, new Vector3(0.3f * s, 0.98f, -0.28f), 0.07f, Shape.Shiny(new Color(1f, 0.8f, 0.3f), 0.85f, 0.6f));
                    }
                    var w = pivot.gameObject.AddComponent<Wobble>();
                    w.Turn = new Vector3(6f, 0f, 2f);
                    w.Speed = 3f;
                    break;
                }
                case 3: // little wings
                {
                    var feather = Shape.Shiny(look.color2 == 0 ? new Color(0.97f, 0.97f, 1f) : gear, 0.45f);
                    var tip = Shape.Shiny(Color.Lerp(feather.color, Color.black, 0.15f), 0.45f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var wing = Shape.Pivot(root, new Vector3(0.18f * s, 0.88f, -0.44f), new Vector3(0f, 35f * s, -15f * s), "Wing");
                        Shape.Ball(wing, new Vector3(0.2f * s, 0.06f, 0f), new Vector3(0.4f, 0.14f, 0.05f), feather, new Vector3(0f, 0f, 12f * s));
                        Shape.Ball(wing, new Vector3(0.17f * s, -0.04f, 0f), new Vector3(0.32f, 0.11f, 0.045f), feather, new Vector3(0f, 0f, -6f * s));
                        Shape.Ball(wing, new Vector3(0.13f * s, -0.12f, 0f), new Vector3(0.24f, 0.09f, 0.04f), tip, new Vector3(0f, 0f, -22f * s));
                        var w = wing.gameObject.AddComponent<Wobble>();
                        w.Turn = new Vector3(0f, 18f * s, 6f * s);
                        w.Speed = 7f;
                    }
                    break;
                }
                case 4: // shell
                {
                    var shell = Shape.Shiny(look.color2 == 0 ? new Color(0.32f, 0.6f, 0.25f) : gear, 0.55f);
                    var plate = Shape.Shiny(Color.Lerp(shell.color, Color.black, 0.3f), 0.55f);
                    var rim = Shape.Shiny(new Color(0.9f, 0.82f, 0.55f), 0.4f);
                    var c = new Vector3(0f, 0.68f, -0.32f);
                    Shape.Dome(root, c, 0.86f, 0.3f, shell, new Vector3(-90f, 0f, 0f));
                    Shape.Ring(root, c, 0.88f, 0.06f, rim, new Vector3(90f, 0f, 0f));
                    var spots = new[] { Vector2.zero, new Vector2(0.21f, 0.15f), new Vector2(-0.21f, 0.15f), new Vector2(0.21f, -0.15f), new Vector2(-0.21f, -0.15f), new Vector2(0f, 0.27f), new Vector2(0f, -0.27f) };
                    foreach (var p in spots)
                    {
                        float k = 1f - (p.x * p.x + p.y * p.y) / (0.43f * 0.43f);
                        float dz = 0.3f * Mathf.Sqrt(Mathf.Max(0f, k));
                        var at = c + new Vector3(p.x, p.y, -dz);
                        var n = new Vector3(p.x / (0.43f * 0.43f), p.y / (0.43f * 0.43f), -dz / (0.3f * 0.3f)).normalized;
                        var g = Shape.Ball(root, at, new Vector3(0.17f, 0.15f, 0.04f), plate);
                        g.transform.localRotation = Quaternion.LookRotation(n, Vector3.up);
                    }
                    break;
                }
                case 5: // none
                    break;
                case 6: // balloon
                {
                    var pivot = Shape.Pivot(root, new Vector3(0.12f, 0.95f, -0.42f), Vector3.zero, "Balloon");
                    var col = look.color2 == 0 ? new Color(1f, 0.3f, 0.35f) : gear;
                    Shape.Stick(pivot, Vector3.zero, new Vector3(0.28f, 1.0f, -0.2f), 0.012f, Mats.Solid(new Color(0.95f, 0.95f, 0.95f)));
                    Shape.Cone(pivot, new Vector3(0.28f, 0.98f, -0.2f), 0.07f, 0.06f, Shape.Shiny(col, 0.8f));
                    Shape.Ball(pivot, new Vector3(0.28f, 1.24f, -0.2f), new Vector3(0.42f, 0.5f, 0.42f), Shape.Shiny(col, 0.92f));
                    Shape.Ball(pivot, new Vector3(0.2f, 1.36f, -0.06f), new Vector3(0.07f, 0.1f, 0.04f), Mats.Solid(Color.Lerp(col, Color.white, 0.75f)));
                    var w = pivot.gameObject.AddComponent<Wobble>();
                    w.Turn = new Vector3(7f, 10f, 7f);
                    w.Speed = 1.4f;
                    break;
                }
                default: // backpack pod
                {
                    var pack = Shape.Shiny(gear, 0.35f);
                    var trim = Shape.Shiny(Color.Lerp(gear, Color.black, 0.35f), 0.35f);
                    Shape.Ball(root, new Vector3(0f, 0.74f, -0.46f), new Vector3(0.46f, 0.42f, 0.26f), pack);
                    Shape.Ball(root, new Vector3(0f, 0.86f, -0.52f), new Vector3(0.4f, 0.16f, 0.2f), trim);
                    Shape.Ball(root, new Vector3(0f, 0.64f, -0.58f), new Vector3(0.24f, 0.14f, 0.08f), trim);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ blaster skins

        static void BuildBlaster(FighterParts parts, Color team, CharacterLook look, Color body)
        {
            var gun = parts.Gun;
            if (gun == null) return;
            for (int i = gun.childCount - 1; i >= 0; i--)
            {
                var ch = gun.GetChild(i);
                if (ch != parts.Muzzle) Object.Destroy(ch.gameObject);
            }
            var dark = Mats.Solid(Color.Lerp(body, Color.black, 0.55f));
            Material barrel = dark, grip = dark, ring = null, extra = null;
            switch (look.blaster)
            {
                case 1: barrel = Shape.Shiny(new Color(1f, 0.78f, 0.28f), 0.88f, 0.75f); grip = Shape.Shiny(new Color(0.3f, 0.16f, 0.08f), 0.4f); ring = Shape.Shiny(new Color(1f, 0.9f, 0.55f), 0.9f, 0.8f); break;
                case 2: barrel = Shape.Shiny(new Color(0.86f, 0.88f, 0.93f), 0.93f, 0.8f); grip = Shape.Shiny(new Color(0.22f, 0.23f, 0.26f), 0.6f, 0.3f); ring = Shape.Shiny(new Color(0.5f, 0.52f, 0.58f), 0.9f, 0.8f); break;
                case 3: barrel = Shape.Shiny(new Color(0.58f, 0.36f, 0.18f), 0.3f); grip = Shape.Shiny(new Color(0.38f, 0.22f, 0.1f), 0.3f); ring = Shape.Shiny(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.6f); break;
                case 4: barrel = Shape.Shiny(new Color(0.97f, 0.96f, 0.95f), 0.7f); grip = Shape.Shiny(new Color(0.9f, 0.15f, 0.2f), 0.7f); ring = grip; break;
                case 5: barrel = Shape.Shiny(new Color(0.07f, 0.07f, 0.1f), 0.92f); grip = barrel; ring = Shape.Glow(new Color(1f, 0.25f, 0.85f), 2.4f); extra = Shape.Glow(new Color(0.3f, 0.95f, 1f), 2.4f); break;
                case 6: barrel = Shape.Shiny(new Color(1f, 0.55f, 0.1f), 0.75f); grip = Shape.Shiny(new Color(1f, 0.85f, 0.15f), 0.75f); ring = Shape.Shiny(new Color(0.2f, 0.55f, 1f), 0.75f); extra = Shape.Shiny(new Color(0.35f, 0.85f, 0.3f), 0.75f); break;
            }
            var b = Prim.Sphere("Barrel", gun, Vector3.zero, 1f, barrel);
            b.transform.localScale = new Vector3(0.2f, 0.2f, 0.82f);
            var g = Prim.Sphere("Grip", gun, new Vector3(0f, -0.13f, -0.17f), 1f, grip);
            g.transform.localScale = new Vector3(0.13f, 0.22f, 0.15f);
            if (ring != null)
            {
                float[] zs = look.blaster == 4 ? new[] { -0.26f, -0.13f, 0f, 0.13f, 0.26f } : look.blaster == 3 ? new[] { -0.18f, 0.2f } : new[] { -0.12f, 0.12f };
                foreach (float z in zs)
                {
                    float rad = 0.1f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - (z / 0.41f) * (z / 0.41f)));
                    Shape.Ring(gun, new Vector3(0f, 0f, z), rad * 2f + 0.01f, 0.025f, ring, new Vector3(90f, 0f, 0f));
                }
            }
            if (look.blaster == 5 && extra != null)
                foreach (float s in new[] { -1f, 1f })
                    Shape.Box(gun, new Vector3(0.094f * s, 0f, 0f), new Vector3(0.012f, 0.02f, 0.46f), extra);
            if (look.blaster == 6 && extra != null)
                Shape.Box(gun, new Vector3(0f, 0.11f, -0.05f), new Vector3(0.05f, 0.06f, 0.2f), extra);
            if (look.blaster == 1 || look.blaster == 2)
                Shape.Box(gun, new Vector3(0f, 0.1f, -0.08f), new Vector3(0.03f, 0.04f, 0.16f), ring);
            Prim.Sphere("GunTip", gun, new Vector3(0f, 0f, 0.38f), 1f, Mats.GlowAvailable ? Mats.Glow(team, 2.2f, 0.6f) : Mats.Solid(team)).transform.localScale = new Vector3(0.17f, 0.17f, 0.1f);
        }
    }
}
