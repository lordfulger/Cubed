using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// The big hat collection. Every hat is built from primitives under the hat root, where the top of the head
    /// is local y=0 and the head ball (diameter 1) is centered at (0, -0.5, 0); the face looks down +Z. The color
    /// passed in is the fighter's body color (a few hats tint parts with it). Unlocks live on <see cref="Achievement.Hat"/>.
    /// </summary>
    public static partial class Hats
    {
        static Material S(Color c, float gloss = 0.5f, float metal = 0f) => Shape.Shiny(c, gloss, metal);
        static Color C(float r, float g, float b) => new Color(r, g, b);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Hat H(string id, string name, Action<Transform, Color> build) => new Hat { Id = id, Name = name, Build = build };
        static Wobble Wob(Transform t) => t.gameObject.AddComponent<Wobble>();
        static Wobble Wob(GameObject g) => g.AddComponent<Wobble>();
        static readonly Vector3 OverTop = new Vector3(-90f, 90f, 0f);   // ring arc from front to back over the head
        static readonly Vector3 EarToEar = new Vector3(-90f, 0f, 0f);   // ring arc from side to side over the head

        static List<Hat> More() => new List<Hat>
        {
            H("cap", "Baseball cap", (t, c) =>
            {
                var main = S(Color.Lerp(c, Color.black, 0.25f), 0.35f);
                var dark = S(Color.Lerp(c, Color.black, 0.55f), 0.35f);
                Shape.Ball(t, V(0f, -0.16f, 0f), V(0.8f, 0.58f, 0.8f), main);
                Shape.Ball(t, V(0f, -0.13f, 0.36f), V(0.52f, 0.05f, 0.46f), dark, V(6f, 0f, 0f));
                Shape.Ball(t, V(0f, 0.13f, 0f), 0.07f, dark);
                Shape.Star(t, V(0f, -0.03f, 0.37f), 0.13f, 0.02f, S(Color.white, 0.4f), V(-22f, 0f, 0f));
            }),
            H("beanie", "Beanie", (t, c) =>
            {
                var knit = S(C(0.2f, 0.55f, 0.85f), 0.12f);
                var cuff = S(C(0.14f, 0.4f, 0.68f), 0.12f);
                Shape.Ball(t, V(0f, -0.13f, 0f), V(0.82f, 0.66f, 0.82f), knit);
                Shape.Ring(t, V(0f, -0.17f, 0f), 0.76f, 0.12f, cuff);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * 22.5f, r = 0.43f;
                    Shape.Box(t, V(Mathf.Sin(a * Mathf.Deg2Rad) * r, -0.17f, Mathf.Cos(a * Mathf.Deg2Rad) * r), V(0.02f, 0.1f, 0.02f), knit, V(0f, a, 0f));
                }
                Shape.Ball(t, V(0f, 0.21f, 0f), 0.2f, S(C(0.97f, 0.97f, 0.97f), 0.1f));
            }),
            H("cowboy", "Cowboy hat", (t, c) =>
            {
                var hide = S(C(0.55f, 0.33f, 0.16f), 0.3f);
                Shape.Cyl(t, V(0f, -0.11f, 0f), 1.04f, 0.035f, hide);
                Shape.Ring(t, V(0f, -0.075f, 0f), 1.04f, 0.07f, hide);
                foreach (float s in new[] { -1f, 1f })
                    Shape.Ball(t, V(0.46f * s, -0.05f, 0f), V(0.18f, 0.045f, 0.62f), hide, V(0f, 0f, 30f * s));
                Shape.Cone(t, V(0f, -0.09f, 0f), 0.52f, 0.36f, hide, default, 0.82f);
                Shape.Ball(t, V(0f, 0.26f, 0f), V(0.34f, 0.07f, 0.4f), S(C(0.45f, 0.26f, 0.12f), 0.3f));
                Shape.Cyl(t, V(0f, -0.075f, 0f), 0.53f, 0.07f, S(C(0.22f, 0.12f, 0.06f), 0.4f));
                Shape.Star(t, V(0f, -0.04f, 0.268f), 0.08f, 0.02f, S(C(0.85f, 0.85f, 0.9f), 0.85f, 0.7f));
            }),
            H("wizard", "Wizard hat", (t, c) =>
            {
                var robe = S(C(0.25f, 0.2f, 0.6f), 0.4f);
                Shape.Cyl(t, V(0f, -0.13f, 0f), 1.0f, 0.03f, S(C(0.18f, 0.14f, 0.45f), 0.4f));
                var p = Shape.Pivot(t, V(0f, -0.11f, 0f), V(-12f, 0f, 0f));
                Shape.Cone(p, Vector3.zero, 0.74f, 0.6f, robe, default, 0.34f);
                Shape.Ring(p, V(0f, 0.05f, 0f), 0.71f, 0.05f, S(C(1f, 0.82f, 0.3f), 0.8f, 0.6f));
                var p2 = Shape.Pivot(p, V(0f, 0.6f, 0f), V(-32f, 0f, 0f));
                Shape.Cone(p2, Vector3.zero, 0.25f, 0.34f, robe);
                Shape.Ball(p2, V(0f, 0.34f, 0f), 0.08f, Shape.Glow(C(1f, 0.9f, 0.4f), 2.5f, 1f));
                var gold = Shape.Glow(C(1f, 0.85f, 0.3f), 2.2f);
                foreach (var st in new[] { V(30f, 0.2f, 0.1f), V(150f, 0.32f, 0.08f), V(255f, 0.14f, 0.09f), V(-40f, 0.4f, 0.07f) })
                {
                    float r = 0.37f - (0.37f - 0.126f) * (st.y / 0.6f) + 0.005f;
                    var pos = V(Mathf.Sin(st.x * Mathf.Deg2Rad) * r, st.y, Mathf.Cos(st.x * Mathf.Deg2Rad) * r);
                    Shape.Star(p, pos, st.z, 0.02f, gold).transform.localRotation = Quaternion.Euler(0f, st.x, 0f) * Quaternion.Euler(-22f, 0f, 0f);
                }
            }),
            H("chef", "Chef hat", (t, c) =>
            {
                var w = S(C(0.97f, 0.97f, 0.97f), 0.2f);
                Shape.Cyl(t, V(0f, -0.16f, 0f), 0.74f, 0.28f, w);
                Shape.Ring(t, V(0f, -0.04f, 0f), 0.75f, 0.02f, S(C(0.86f, 0.86f, 0.9f), 0.2f));
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    Shape.Ball(t, V(Mathf.Sin(a) * 0.21f, 0.17f, Mathf.Cos(a) * 0.21f), 0.36f, w);
                }
                Shape.Ball(t, V(0f, 0.25f, 0f), V(0.52f, 0.36f, 0.52f), w);
            }),
            H("viking", "Viking helmet", (t, c) =>
            {
                var steel = S(C(0.62f, 0.64f, 0.68f), 0.7f, 0.6f);
                var bronze = S(C(0.8f, 0.55f, 0.22f), 0.7f, 0.5f);
                var horn = S(C(0.95f, 0.9f, 0.78f), 0.5f);
                Shape.Ball(t, V(0f, -0.15f, 0f), V(0.86f, 0.62f, 0.86f), steel);
                Shape.Ring(t, V(0f, -0.16f, 0f), 0.84f, 0.07f, bronze);
                Shape.Ring(t, V(0f, -0.15f, 0f), 0.88f, 0.05f, bronze, OverTop, 180f);
                Shape.Box(t, V(0f, -0.21f, 0.5f), V(0.05f, 0.17f, 0.04f), bronze, V(-20f, 0f, 0f));
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Cone(t, V(0.36f * s, -0.1f, 0f), 0.17f, 0.24f, horn, V(0f, 0f, -55f * s), 0.55f);
                    Shape.Cone(t, V(0.557f * s, 0.038f, 0f), 0.094f, 0.2f, horn, V(0f, 0f, -12f * s));
                    for (int k = 0; k < 3; k++)
                        Shape.Ball(t, V(0.3f * s, -0.15f + k * 0.12f, 0.27f - k * 0.06f), 0.05f, bronze);
                }
            }),
            H("tricorn", "Pirate tricorn", (t, c) =>
            {
                var felt = S(C(0.1f, 0.1f, 0.12f), 0.3f);
                var gold = S(C(1f, 0.8f, 0.3f), 0.8f, 0.6f);
                Shape.Ball(t, V(0f, -0.06f, 0f), V(0.64f, 0.4f, 0.64f), felt);
                foreach (float a in new[] { 180f, 60f, -60f })
                {
                    var p = Shape.Pivot(t, V(0f, -0.08f, 0f), V(0f, a, 0f));
                    Shape.Box(p, V(0f, 0.04f, 0.27f), V(0.9f, 0.16f, 0.05f), felt, V(25f, 0f, 0f));
                    Shape.Box(p, V(0f, 0.11f, 0.305f), V(0.9f, 0.025f, 0.06f), gold, V(25f, 0f, 0f));
                }
                Shape.Ball(t, V(0.2f, 0.04f, 0.3f), V(0.1f, 0.1f, 0.04f), S(C(0.95f, 0.95f, 0.92f), 0.3f), V(-20f, 30f, 0f));
            }),
            H("bunny", "Bunny ears", (t, c) =>
            {
                var fur = S(C(0.97f, 0.97f, 0.97f), 0.3f);
                var pink = S(C(1f, 0.62f, 0.72f), 0.3f);
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = Shape.Pivot(t, V(0.13f * s, -0.03f, -0.02f), V(-8f, 0f, -10f * s));
                    Shape.Ball(p, V(0f, 0.27f, 0f), V(0.17f, 0.56f, 0.08f), fur);
                    Shape.Ball(p, V(0f, 0.27f, 0.035f), V(0.09f, 0.42f, 0.03f), pink);
                    var w = Wob(p);
                    w.Turn = V(4f, 0f, 5f);
                    w.Speed = 2f + s * 0.3f;
                }
            }),
            H("catears", "Cat ears", (t, c) =>
            {
                var ear = S(Color.Lerp(c, Color.black, 0.15f), 0.5f);
                var inner = S(C(1f, 0.6f, 0.7f), 0.3f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Spike(t, V(0.22f * s, -0.08f, 0.03f), V(0.26f, 0.3f, 0.12f), ear, V(0f, 0f, -20f * s));
                    Shape.Spike(t, V(0.215f * s, -0.06f, 0.075f), V(0.15f, 0.19f, 0.04f), inner, V(0f, 0f, -20f * s));
                }
            }),
            H("bearears", "Bear ears", (t, c) =>
            {
                var fur = S(Color.Lerp(c, Color.black, 0.2f), 0.35f);
                var inner = S(Color.Lerp(c, Color.white, 0.45f), 0.3f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Ball(t, V(0.28f * s, -0.02f, 0f), V(0.22f, 0.22f, 0.1f), fur, V(0f, 0f, -20f * s));
                    Shape.Ball(t, V(0.28f * s, -0.02f, 0.045f), V(0.12f, 0.12f, 0.03f), inner, V(0f, 0f, -20f * s));
                }
            }),
            H("mohawk", "Mohawk", (t, c) =>
            {
                var hair = S(C(1f, 0.22f, 0.55f), 0.6f);
                for (int i = 0; i < 7; i++)
                {
                    float a = 55f - i * 20f;
                    float h = 0.27f - 0.07f * Mathf.Abs(a) / 65f;
                    var pos = V(0f, -0.5f + 0.48f * Mathf.Cos(a * Mathf.Deg2Rad), 0.48f * Mathf.Sin(a * Mathf.Deg2Rad));
                    Shape.Spike(t, pos, V(0.08f, h, 0.17f), hair, V(a, 0f, 0f));
                }
            }),
            H("afro", "Afro", (t, c) =>
            {
                var hair = S(C(0.17f, 0.1f, 0.06f), 0.08f);
                var center = V(0f, -0.1f, -0.06f);
                Shape.Ball(t, center, V(1.06f, 0.78f, 1f), hair);
                foreach (float lat in new[] { 15f, 48f })
                    for (int i = 0; i < 8; i++)
                    {
                        float lon = i * 45f + (lat > 20f ? 22.5f : 0f);
                        var d = Shape.Dir(lat, lon);
                        Shape.Ball(t, center + V(d.x * 0.53f, d.y * 0.39f, d.z * 0.5f), 0.3f, hair);
                    }
                Shape.Ball(t, center + V(0f, 0.38f, 0f), 0.32f, hair);
            }),
            H("flower", "Flower", (t, c) =>
            {
                var p = Shape.Pivot(t, V(0.25f, -0.04f, 0.14f), V(-25f, 40f, -20f));
                var petal = S(C(1f, 0.55f, 0.75f), 0.4f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f;
                    Shape.Ball(p, V(Mathf.Cos(a * Mathf.Deg2Rad) * 0.1f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.1f, 0f), V(0.17f, 0.1f, 0.04f), petal, V(0f, 0f, a));
                }
                Shape.Ball(p, V(0f, 0f, 0.02f), V(0.1f, 0.1f, 0.06f), S(C(1f, 0.85f, 0.2f), 0.4f));
                Shape.Ball(p, V(-0.13f, -0.13f, -0.02f), V(0.17f, 0.05f, 0.03f), S(C(0.3f, 0.7f, 0.3f), 0.4f), V(0f, 0f, 40f));
            }),
            H("sprout", "Sprout", (t, c) =>
            {
                var green = S(C(0.35f, 0.8f, 0.3f), 0.5f);
                var p = Shape.Pivot(t, V(0f, -0.02f, 0f), Vector3.zero);
                Shape.Stick(p, Vector3.zero, V(0f, 0.23f, 0f), 0.035f, green);
                foreach (float s in new[] { -1f, 1f })
                    Shape.Ball(p, V(0.1f * s, 0.25f, 0f), V(0.2f, 0.04f, 0.11f), green, V(0f, 0f, 25f * s));
                Shape.Ball(p, V(0f, 0.24f, 0f), 0.05f, green);
                var w = Wob(p);
                w.Turn = V(6f, 0f, 6f);
                w.Speed = 2.2f;
            }),
            H("unicorn", "Unicorn horn", (t, c) =>
            {
                var pearl = S(C(1f, 0.97f, 0.92f), 0.85f);
                var gold = S(C(1f, 0.8f, 0.3f), 0.85f, 0.6f);
                var p = Shape.Pivot(t, V(0f, -0.08f, 0.25f), V(28f, 0f, 0f));
                Shape.Cone(p, Vector3.zero, 0.15f, 0.44f, pearl);
                foreach (float h in new[] { 0.1f, 0.2f, 0.3f })
                    Shape.Ring(p, V(0f, h, 0f), 0.15f * (1f - h / 0.44f) + 0.012f, 0.02f, gold);
                var mane = new[] { C(1f, 0.6f, 0.8f), C(0.75f, 0.55f, 1f), C(0.5f, 0.85f, 1f), C(0.6f, 1f, 0.75f), C(1f, 0.9f, 0.5f) };
                for (int i = 0; i < mane.Length; i++)
                {
                    float a = (-8f - i * 17f) * Mathf.Deg2Rad;
                    Shape.Ball(t, V(0f, -0.5f + 0.52f * Mathf.Cos(a), 0.52f * Mathf.Sin(a)), V(0.12f, 0.16f, 0.22f), S(mane[i], 0.5f), V(a * Mathf.Rad2Deg, 0f, 0f));
                }
            }),
            H("santa", "Santa hat", (t, c) =>
            {
                var red = S(C(0.85f, 0.12f, 0.15f), 0.35f);
                var fur = S(C(0.97f, 0.97f, 0.97f), 0.1f);
                var p = Shape.Pivot(t, V(0f, -0.1f, 0f), V(-28f, 0f, 8f));
                Shape.Cone(p, Vector3.zero, 0.74f, 0.42f, red, default, 0.28f);
                var p2 = Shape.Pivot(p, V(0f, 0.42f, 0f), V(-55f, 0f, 0f));
                Shape.Cone(p2, Vector3.zero, 0.21f, 0.26f, red);
                Shape.Ball(p2, V(0f, 0.27f, 0f), 0.15f, fur);
                Shape.Ring(t, V(0f, -0.11f, 0f), 0.78f, 0.14f, fur);
            }),
            H("gradcap", "Graduation cap", (t, c) =>
            {
                var k = S(C(0.08f, 0.08f, 0.1f), 0.4f);
                var gold = S(C(1f, 0.8f, 0.3f), 0.8f, 0.5f);
                Shape.Ball(t, V(0f, -0.15f, 0f), V(0.78f, 0.5f, 0.78f), k);
                Shape.Box(t, V(0f, 0.1f, 0f), V(0.78f, 0.04f, 0.78f), k, V(0f, 45f, 0f));
                Shape.Ball(t, V(0f, 0.125f, 0f), V(0.07f, 0.03f, 0.07f), gold);
                Shape.Box(t, V(0.27f, 0.124f, 0f), V(0.54f, 0.012f, 0.02f), gold);
                Shape.Stick(t, V(0.53f, 0.12f, 0f), V(0.53f, -0.06f, 0f), 0.02f, gold);
                Shape.Cone(t, V(0.53f, -0.18f, 0f), 0.08f, 0.13f, gold);
            }),
            H("headphones", "Headphones", (t, c) =>
            {
                var band = S(C(0.12f, 0.12f, 0.14f), 0.6f);
                var cup = S(Color.Lerp(c, Color.white, 0.15f), 0.7f);
                var glow = Shape.Glow(Color.Lerp(c, Color.white, 0.5f), 2f);
                Shape.Ring(t, V(0f, -0.28f, 0f), 0.98f, 0.05f, band, EarToEar, 180f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Cyl(t, V(0.44f * s, -0.28f, -0.03f), 0.3f, 0.11f, cup, V(0f, 0f, -90f * s));
                    Shape.Ring(t, V(0.555f * s, -0.28f, -0.03f), 0.2f, 0.03f, glow, V(0f, 0f, 90f));
                }
            }),
            H("fez", "Fez", (t, c) =>
            {
                var red = S(C(0.75f, 0.1f, 0.12f), 0.4f);
                var k = S(C(0.08f, 0.08f, 0.08f), 0.3f);
                Shape.Cone(t, V(0f, -0.13f, 0f), 0.48f, 0.4f, red, default, 0.78f);
                Shape.Stick(t, V(0f, 0.27f, 0f), V(0.21f, 0.2f, 0.08f), 0.015f, k);
                Shape.Stick(t, V(0.21f, 0.2f, 0.08f), V(0.22f, 0.04f, 0.09f), 0.015f, k);
                Shape.Cone(t, V(0.22f, -0.05f, 0.09f), 0.07f, 0.1f, S(C(1f, 0.8f, 0.3f), 0.7f, 0.4f));
            }),
            H("sombrero", "Sombrero", (t, c) =>
            {
                var straw = S(C(0.9f, 0.75f, 0.42f), 0.25f);
                var red = S(C(0.85f, 0.2f, 0.2f), 0.4f);
                var green = S(C(0.2f, 0.65f, 0.3f), 0.4f);
                Shape.Cyl(t, V(0f, -0.13f, 0f), 1.5f, 0.03f, straw);
                Shape.Ring(t, V(0f, -0.09f, 0f), 1.5f, 0.08f, straw);
                Shape.Cone(t, V(0f, -0.12f, 0f), 0.52f, 0.42f, straw, default, 0.62f);
                Shape.Ball(t, V(0f, 0.3f, 0f), V(0.33f, 0.1f, 0.33f), straw);
                Shape.Cyl(t, V(0f, -0.08f, 0f), 0.505f, 0.07f, red);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 36f * Mathf.Deg2Rad;
                    Shape.Ball(t, V(Mathf.Sin(a) * 0.68f, -0.08f, Mathf.Cos(a) * 0.68f), 0.07f, i % 2 == 0 ? red : green);
                }
            }),
            H("bowler", "Bowler", (t, c) =>
            {
                var k = S(C(0.08f, 0.08f, 0.1f), 0.6f);
                Shape.Dome(t, V(0f, -0.1f, 0f), 0.6f, 0.36f, k);
                Shape.Cyl(t, V(0f, -0.12f, 0f), 0.84f, 0.025f, k);
                Shape.Ring(t, V(0f, -0.1f, 0f), 0.84f, 0.04f, k);
                Shape.Cyl(t, V(0f, -0.1f, 0f), 0.61f, 0.06f, S(C(0.4f, 0.08f, 0.1f), 0.4f));
            }),
            H("beret", "Beret", (t, c) =>
            {
                var m = S(C(0.55f, 0.1f, 0.18f), 0.2f);
                Shape.Ball(t, V(0f, -0.07f, -0.01f), V(0.72f, 0.3f, 0.72f), m);
                Shape.Ball(t, V(0.04f, 0.03f, -0.02f), V(0.88f, 0.18f, 0.84f), m, V(0f, 0f, -12f));
                Shape.Cyl(t, V(0.03f, 0.1f, -0.02f), 0.05f, 0.06f, m);
            }),
            H("knight", "Knight helmet", (t, c) =>
            {
                var steel = S(C(0.76f, 0.78f, 0.83f), 0.82f, 0.7f);
                var dark = Mats.Solid(C(0.1f, 0.1f, 0.12f));
                var plume = S(C(0.85f, 0.15f, 0.18f), 0.3f);
                Shape.Ball(t, V(0f, -0.14f, 0f), V(0.88f, 0.64f, 0.88f), steel);
                Shape.Ring(t, V(0f, -0.14f, 0f), 0.92f, 0.05f, steel, OverTop, 170f);
                foreach (float s in new[] { -1f, 1f })
                    Shape.Box(t, V(0.42f * s, -0.3f, 0.12f), V(0.05f, 0.26f, 0.24f), steel, V(0f, -16f * s, 0f));
                var visor = Shape.Pivot(t, V(0f, -0.05f, 0.4f), V(-20f, 0f, 0f), "Visor");
                Shape.Box(visor, Vector3.zero, V(0.56f, 0.1f, 0.07f), steel);
                Shape.Box(visor, V(0f, 0.016f, 0.036f), V(0.4f, 0.015f, 0.01f), dark);
                Shape.Box(visor, V(0f, -0.016f, 0.036f), V(0.4f, 0.015f, 0.01f), dark);
                Shape.Cyl(t, V(0f, 0.15f, -0.03f), 0.07f, 0.08f, steel);
                Shape.Ball(t, V(0f, 0.28f, -0.05f), V(0.09f, 0.3f, 0.16f), plume, V(-25f, 0f, 0f));
                Shape.Ball(t, V(0f, 0.23f, -0.17f), V(0.08f, 0.26f, 0.14f), plume, V(-55f, 0f, 0f));
                Shape.Ball(t, V(0f, 0.14f, -0.27f), V(0.07f, 0.22f, 0.12f), plume, V(-80f, 0f, 0f));
            }),
            H("astronaut", "Astronaut bubble", (t, c) =>
            {
                Shape.Ball(t, V(0f, -0.36f, 0f), 1.2f, Shape.Glass(new Color(0.75f, 0.9f, 1f, 0.16f)));
                Shape.Ball(t, V(-0.27f, 0.08f, 0.3f), V(0.14f, 0.07f, 0.03f), Mats.GlowAvailable ? Mats.Glow(new Color(1f, 1f, 1f, 0.8f), 1.4f, 1.2f) : Shape.Glass(new Color(1f, 1f, 1f, 0.5f)), V(-30f, -40f, 20f));
                Shape.Ring(t, V(0f, -0.86f, 0f), 0.76f, 0.1f, S(C(0.95f, 0.95f, 0.97f), 0.6f));
                Shape.Stick(t, V(0.2f, 0.16f, -0.1f), V(0.24f, 0.36f, -0.12f), 0.025f, S(C(0.8f, 0.8f, 0.85f), 0.7f, 0.6f));
                Shape.Ball(t, V(0.24f, 0.38f, -0.12f), 0.06f, Shape.Glow(C(1f, 0.3f, 0.3f), 2.4f, 1f));
            }),
            H("bucket", "Bucket hat", (t, c) =>
            {
                var denim = S(C(0.32f, 0.45f, 0.62f), 0.15f);
                var stitch = Mats.Solid(C(0.85f, 0.8f, 0.6f));
                Shape.Cone(t, V(0f, -0.16f, 0f), 0.72f, 0.32f, denim, default, 0.82f);
                Shape.Cone(t, V(0f, -0.22f, 0f), 1.0f, 0.09f, denim, default, 0.72f);
                Shape.Ring(t, V(0f, -0.03f, 0f), 0.675f, 0.012f, stitch);
                Shape.Ring(t, V(0f, -0.18f, 0f), 0.885f, 0.012f, stitch);
            }),
            H("pumpkin", "Pumpkin", (t, c) =>
            {
                var orange = S(C(1f, 0.5f, 0.1f), 0.45f);
                var glow = Shape.Glow(C(1f, 0.85f, 0.3f), 2.4f);
                Shape.Ball(t, V(0f, 0.1f, 0f), V(0.5f, 0.4f, 0.5f), orange);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f + 30f;
                    Shape.Ball(t, V(Mathf.Sin(a * Mathf.Deg2Rad) * 0.13f, 0.1f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.13f), V(0.3f, 0.38f, 0.3f), orange, V(0f, a, 0f));
                }
                Shape.Cyl(t, V(0f, 0.26f, 0f), 0.07f, 0.12f, S(C(0.35f, 0.45f, 0.15f), 0.3f), V(0f, 0f, 12f));
                Shape.Ball(t, V(0.07f, 0.33f, 0.03f), V(0.14f, 0.03f, 0.08f), S(C(0.3f, 0.65f, 0.25f), 0.4f), V(0f, 30f, 10f));
                foreach (float s in new[] { -1f, 1f })
                    Shape.Spike(t, V(0.08f * s, 0.12f, 0.255f), V(0.07f, 0.07f, 0.02f), glow);
                Shape.Box(t, V(0f, 0.04f, 0.25f), V(0.14f, 0.03f, 0.02f), glow);
            }),
            H("frog", "Frog hat", (t, c) =>
            {
                var g = S(C(0.35f, 0.78f, 0.3f), 0.5f);
                var w = Mats.Solid(C(0.97f, 0.97f, 0.97f));
                var k = Mats.Solid(C(0.05f, 0.05f, 0.07f));
                Shape.Ball(t, V(0f, -0.15f, 0f), V(0.82f, 0.6f, 0.82f), g);
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Ball(t, V(0.17f * s, 0.12f, 0.1f), 0.24f, g);
                    Shape.Ball(t, V(0.17f * s, 0.14f, 0.18f), 0.17f, w);
                    Shape.Ball(t, V(0.17f * s, 0.145f, 0.26f), V(0.08f, 0.09f, 0.04f), k);
                    Shape.Ball(t, V(0.25f * s, -0.08f, 0.31f), V(0.07f, 0.04f, 0.03f), Mats.Solid(C(1f, 0.55f, 0.6f)));
                }
                Shape.Ring(t, V(0f, -0.03f, 0.385f), 0.2f, 0.02f, Mats.Solid(C(0.7f, 0.15f, 0.2f)), V(75f, 0f, 0f), 140f);
            }),
            H("duck", "Duck buddy", (t, c) =>
            {
                var y = S(C(1f, 0.88f, 0.2f), 0.5f);
                var o = S(C(1f, 0.55f, 0.12f), 0.5f);
                var k = Mats.Solid(C(0.05f, 0.05f, 0.07f));
                var p = Shape.Pivot(t, V(0f, -0.02f, 0f), Vector3.zero);
                Shape.Ball(p, V(0f, 0.12f, -0.02f), V(0.34f, 0.26f, 0.44f), y);
                Shape.Ball(p, V(0f, 0.3f, 0.1f), 0.22f, y);
                Shape.Ball(p, V(0f, 0.28f, 0.22f), V(0.13f, 0.05f, 0.11f), o);
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Ball(p, V(0.075f * s, 0.32f, 0.18f), 0.035f, k);
                    Shape.Ball(p, V(0.16f * s, 0.12f, -0.03f), V(0.06f, 0.15f, 0.26f), y, V(0f, 0f, 10f * s));
                    Shape.Ball(p, V(0.07f * s, 0.01f, 0.08f), V(0.08f, 0.02f, 0.1f), o);
                }
                Shape.Spike(p, V(0f, 0.15f, -0.22f), V(0.1f, 0.12f, 0.05f), y, V(-60f, 0f, 0f));
                var w = Wob(p);
                w.Turn = V(3f, 10f, 3f);
                w.Speed = 1.6f;
            }),
            H("mushroom", "Mushroom cap", (t, c) =>
            {
                var red = S(C(0.85f, 0.15f, 0.15f), 0.6f);
                var w = S(C(0.97f, 0.95f, 0.9f), 0.4f);
                var basePos = V(0f, -0.13f, 0f);
                Shape.Dome(t, basePos, 1.0f, 0.42f, red);
                Shape.Cyl(t, V(0f, -0.14f, 0f), 0.96f, 0.008f, w);
                foreach (var sp in new[] { new Vector2(30f, 20f), new Vector2(30f, 95f), new Vector2(30f, 170f), new Vector2(30f, 245f), new Vector2(30f, 320f), new Vector2(62f, 60f), new Vector2(60f, 210f), new Vector2(82f, 0f) })
                {
                    var d = Shape.Dir(sp.x, sp.y);
                    var p = basePos + V(d.x * 0.5f, d.y * 0.42f, d.z * 0.5f);
                    var n = V(d.x / 0.5f, d.y / 0.42f, d.z / 0.5f).normalized;
                    Shape.Ball(t, p + n * 0.004f, V(0.17f, 0.03f, 0.17f), w).transform.localRotation = Quaternion.FromToRotation(Vector3.up, n);
                }
            }),
            H("icecream", "Ice cream cone", (t, c) =>
            {
                var cream = S(C(1f, 0.68f, 0.8f), 0.5f);
                var cone = S(C(0.86f, 0.62f, 0.32f), 0.35f);
                var line = Mats.Solid(C(0.65f, 0.42f, 0.2f));
                Shape.Ball(t, V(0f, -0.04f, 0f), V(0.66f, 0.36f, 0.66f), cream);
                for (int i = 0; i < 7; i++)
                {
                    float a = (i * 51f + 10f) * Mathf.Deg2Rad;
                    Shape.Ball(t, V(Mathf.Sin(a) * 0.3f, -0.12f - (i % 3) * 0.02f, Mathf.Cos(a) * 0.3f), V(0.09f, 0.16f + (i % 2) * 0.06f, 0.09f), cream);
                }
                var sprinkle = new[] { C(1f, 0.3f, 0.3f), C(0.3f, 0.8f, 1f), C(1f, 0.9f, 0.3f), C(0.5f, 1f, 0.5f), C(0.85f, 0.5f, 1f) };
                for (int i = 0; i < 12; i++)
                {
                    float a = (i * 33f + 5f) * Mathf.Deg2Rad, rr = 0.12f + (i % 3) * 0.06f;
                    float yy = -0.04f + 0.18f * Mathf.Sqrt(Mathf.Max(0f, 1f - (rr / 0.33f) * (rr / 0.33f)));
                    Shape.Box(t, V(Mathf.Sin(a) * rr, yy, Mathf.Cos(a) * rr), V(0.05f, 0.014f, 0.014f), Mats.Solid(sprinkle[i % sprinkle.Length]), V(0f, i * 67f, 0f));
                }
                Shape.Cone(t, V(0f, 0.08f, 0f), 0.42f, 0.5f, cone);
                foreach (float h in new[] { 0.12f, 0.24f, 0.36f })
                    Shape.Ring(t, V(0f, 0.08f + h, 0f), 0.42f * (1f - h / 0.5f) + 0.01f, 0.012f, line);
            }),
            H("cake", "Birthday cake", (t, c) =>
            {
                var sponge = S(C(0.96f, 0.82f, 0.62f), 0.3f);
                var icing = S(C(1f, 0.6f, 0.75f), 0.6f);
                Shape.Cyl(t, V(0f, -0.06f, 0f), 0.62f, 0.2f, sponge);
                Shape.Ring(t, V(0f, 0.04f, 0f), 0.63f, 0.03f, S(C(0.98f, 0.97f, 0.95f), 0.4f));
                Shape.Cyl(t, V(0f, 0.14f, 0f), 0.64f, 0.05f, icing);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Shape.Ball(t, V(Mathf.Sin(a) * 0.315f, 0.13f, Mathf.Cos(a) * 0.315f), V(0.08f, 0.12f, 0.08f), icing);
                }
                var candles = new[] { (V(-0.14f, 0.19f, 0.05f), C(0.4f, 0.7f, 1f)), (V(0f, 0.19f, -0.1f), C(1f, 0.9f, 0.3f)), (V(0.14f, 0.19f, 0.05f), C(0.5f, 0.95f, 0.5f)) };
                foreach (var (pos, col) in candles)
                {
                    Shape.Cyl(t, pos, 0.045f, 0.16f, S(col, 0.5f));
                    var f = Shape.Ball(t, pos + V(0f, 0.21f, 0f), V(0.05f, 0.09f, 0.05f), Mats.GlowAvailable ? Mats.Glow(C(1f, 0.75f, 0.25f), 2.6f, 1f) : Mats.Solid(C(1f, 0.75f, 0.25f)));
                    var w = Wob(f);
                    w.Flicker = true;
                    w.Pulse = 0.15f;
                    w.Speed = 10f;
                }
            }),
            H("lightbulb", "Lightbulb", (t, c) =>
            {
                var metal = S(C(0.7f, 0.72f, 0.76f), 0.8f, 0.6f);
                Shape.Cyl(t, V(0f, -0.07f, 0f), 0.2f, 0.15f, metal);
                Shape.Ring(t, V(0f, -0.02f, 0f), 0.21f, 0.025f, metal);
                Shape.Ring(t, V(0f, 0.04f, 0f), 0.21f, 0.025f, metal);
                Shape.Stick(t, V(0f, 0.08f, 0f), V(0f, 0.22f, 0f), 0.015f, metal);
                var fil = Shape.Ball(t, V(0f, 0.27f, 0f), 0.14f, Mats.GlowAvailable ? Mats.Glow(C(1f, 0.85f, 0.35f), 3f, 1.5f) : Mats.Solid(C(1f, 0.9f, 0.4f)));
                Shape.Ball(t, V(0f, 0.29f, 0f), V(0.42f, 0.44f, 0.42f), Shape.Glass(new Color(1f, 0.97f, 0.75f, 0.35f)));
                var w = Wob(fil);
                w.Pulse = 0.12f;
                w.Speed = 3f;
            }),
            H("cloud", "Storm cloud", (t, c) =>
            {
                var gray = S(C(0.62f, 0.65f, 0.72f), 0.15f);
                var p = Shape.Pivot(t, V(0f, 0.42f, 0f), Vector3.zero, "Cloud");
                Shape.Ball(p, Vector3.zero, 0.36f, gray);
                Shape.Ball(p, V(-0.19f, -0.04f, 0.02f), 0.26f, gray);
                Shape.Ball(p, V(0.19f, -0.03f, -0.02f), 0.28f, gray);
                Shape.Ball(p, V(0.05f, 0.1f, -0.05f), 0.26f, gray);
                Shape.Ball(p, V(-0.08f, 0.07f, 0.08f), 0.22f, gray);
                Shape.Ball(p, V(0.1f, 0f, 0.12f), 0.2f, gray);
                Shape.Ball(p, V(0f, -0.1f, 0f), V(0.6f, 0.12f, 0.3f), gray);
                var w = Wob(p);
                w.Move = V(0.03f, 0.04f, 0.03f);
                w.Speed = 1.2f;
                var rain = Shape.Glow(C(0.4f, 0.8f, 1f), 2f);
                for (int i = 0; i < 4; i++)
                {
                    var d = Shape.Ball(p, V(-0.15f + i * 0.1f, -0.25f - (i % 2) * 0.06f, (i % 2) * 0.06f - 0.03f), V(0.03f, 0.07f, 0.03f), rain);
                    var dw = Wob(d);
                    dw.Move = V(0f, 0.05f, 0f);
                    dw.Speed = 6f + i;
                }
            }),
            H("flame", "Flame", (t, c) =>
            {
                Shape.Ring(t, V(0f, -0.07f, 0f), 0.5f, 0.06f, S(C(0.2f, 0.2f, 0.22f), 0.7f, 0.6f));
                var outer = Shape.Ball(t, V(0f, 0.2f, 0f), V(0.42f, 0.62f, 0.42f), Shape.Glow(C(1f, 0.42f, 0.08f), 2.2f, 1.2f));
                var mid = Shape.Ball(t, V(0f, 0.17f, 0f), V(0.3f, 0.46f, 0.3f), Shape.Glow(C(1f, 0.72f, 0.2f), 2.4f, 1.2f));
                Shape.Ball(t, V(0f, 0.1f, 0f), V(0.16f, 0.26f, 0.16f), Shape.Glow(C(1f, 1f, 0.75f), 2.6f, 1f));
                var tip = Shape.Spike(t, V(0f, 0.42f, 0f), V(0.12f, 0.24f, 0.12f), Shape.Glow(C(1f, 0.55f, 0.12f), 2.2f));
                foreach (var g in new[] { outer, mid, tip })
                {
                    var w = Wob(g);
                    w.Flicker = true;
                    w.Pulse = 0.1f;
                    w.Turn = g == tip ? V(0f, 0f, 12f) : Vector3.zero;
                    w.Speed = 6f;
                }
            }),
            H("lightning", "Lightning bolt", (t, c) =>
            {
                var core = S(C(1f, 0.85f, 0.2f), 0.8f);
                var glow = Shape.Glow(C(1f, 0.9f, 0.25f), 2.6f);
                var p = Shape.Pivot(t, V(0f, -0.02f, 0f), V(0f, 0f, 8f));
                var pts = new[] { V(-0.03f, 0f, 0f), V(0.08f, 0.2f, 0f), V(-0.04f, 0.22f, 0f), V(0.05f, 0.46f, 0f) };
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Shape.Rod(p, pts[i], pts[i + 1], 0.05f, core);
                    Shape.Rod(p, pts[i], pts[i + 1], 0.075f, glow);
                }
                Shape.Spike(p, V(0.05f, 0.44f, 0f), V(0.08f, 0.12f, 0.07f), glow, V(0f, 0f, -20f));
                var w = Wob(p);
                w.Flicker = true;
                w.Pulse = 0.05f;
                w.Speed = 8f;
            }),
            H("crystalcrown", "Crystal crown", (t, c) =>
            {
                var gold = S(C(1f, 0.8f, 0.3f), 0.85f, 0.7f);
                var gem = S(C(0.45f, 0.9f, 1f), 0.95f, 0.2f);
                Shape.Ring(t, V(0f, -0.08f, 0f), 0.62f, 0.07f, gold);
                Shape.Ring(t, V(0f, -0.02f, 0f), 0.6f, 0.03f, gold);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    float h = i == 0 ? 0.34f : (i == 1 || i == 7) ? 0.24f : 0.17f;
                    var piv = Shape.Pivot(t, V(Mathf.Sin(a * Mathf.Deg2Rad) * 0.3f, -0.06f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.3f), V(0f, a, 0f));
                    Shape.Spike(piv, Vector3.zero, V(0.1f, h, 0.1f), gem).transform.localRotation = Quaternion.Euler(10f, 0f, 0f) * Quaternion.Euler(0f, 45f, 0f);
                }
                Shape.Ball(t, V(0f, -0.06f, 0.335f), V(0.08f, 0.08f, 0.04f), Shape.Glow(C(0.4f, 0.85f, 1f), 2f, 1f));
            }),
            H("satellite", "Satellite dish", (t, c) =>
            {
                var metal = S(C(0.75f, 0.77f, 0.82f), 0.8f, 0.5f);
                Shape.Cyl(t, V(0f, -0.05f, 0f), 0.14f, 0.12f, metal);
                var yaw = Shape.Pivot(t, V(0f, 0.07f, 0f), Vector3.zero, "Yaw");
                Wob(yaw).Spin = 30f;
                Shape.Stick(yaw, Vector3.zero, V(0f, 0.12f, 0f), 0.04f, metal);
                var tilt = Shape.Pivot(yaw, V(0f, 0.14f, 0f), V(35f, 0f, 0f));
                Shape.Dome(tilt, Vector3.zero, 0.48f, 0.12f, S(C(0.92f, 0.93f, 0.96f), 0.6f), V(180f, 0f, 0f));
                Shape.Cyl(tilt, V(0f, 0.002f, 0f), 0.4f, 0.008f, S(C(0.8f, 0.82f, 0.88f), 0.5f));
                Shape.Stick(tilt, Vector3.zero, V(0f, 0.2f, 0f), 0.018f, metal);
                Shape.Ball(tilt, V(0f, 0.21f, 0f), 0.06f, Shape.Glow(C(1f, 0.3f, 0.3f), 2.4f, 1f));
            }),
            H("tiara", "Tiara", (t, c) =>
            {
                var gold = S(C(1f, 0.82f, 0.35f), 0.9f, 0.7f);
                var gem = S(C(1f, 0.45f, 0.75f), 0.95f);
                Shape.Ring(t, V(0f, -0.07f, 0f), 0.56f, 0.03f, gold, default, 200f);
                foreach (float a in new[] { -60f, -30f, 0f, 30f, 60f })
                {
                    float h = a == 0f ? 0.2f : Mathf.Abs(a) < 45f ? 0.13f : 0.09f;
                    var pos = V(Mathf.Sin(a * Mathf.Deg2Rad) * 0.28f, -0.06f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.28f);
                    Shape.Spike(t, pos, V(0.05f, h, 0.03f), gold, V(0f, a, 0f));
                    if (a != 0f) Shape.Ball(t, pos + V(0f, h + 0.01f, 0f), 0.04f, gem);
                }
                Shape.Heart(t, V(0f, 0.08f, 0.29f), 0.09f, 0.03f, gem, V(-10f, 0f, 0f));
                Shape.Heart(t, V(0f, 0.08f, 0.284f), 0.12f, 0.02f, Shape.Glow(C(1f, 0.4f, 0.75f), 1.8f), V(-10f, 0f, 0f));
            }),
            H("bow", "Ribbon bow", (t, c) =>
            {
                var pink = S(C(1f, 0.4f, 0.65f), 0.6f);
                var dark = S(C(0.85f, 0.25f, 0.5f), 0.6f);
                var p = Shape.Pivot(t, V(0.22f, -0.02f, 0.1f), V(0f, 25f, -25f));
                foreach (float s in new[] { -1f, 1f })
                {
                    Shape.Ball(p, V(0.12f * s, 0f, 0f), V(0.26f, 0.17f, 0.08f), pink, V(0f, 0f, 15f * s));
                    Shape.Box(p, V(0.05f * s, -0.1f, 0f), V(0.05f, 0.15f, 0.02f), dark, V(0f, 0f, 20f * s));
                }
                Shape.Ball(p, V(0f, 0f, 0.02f), 0.09f, dark);
            }),
            H("feather", "Feather band", (t, c) =>
            {
                Shape.Ring(t, V(0f, -0.15f, 0f), 0.74f, 0.07f, S(C(0.6f, 0.22f, 0.15f), 0.3f));
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 36f * Mathf.Deg2Rad;
                    Shape.Ball(t, V(Mathf.Sin(a) * 0.375f, -0.15f, Mathf.Cos(a) * 0.375f), 0.045f, Mats.Solid(i % 2 == 0 ? C(0.95f, 0.85f, 0.3f) : C(0.2f, 0.6f, 0.75f)));
                }
                var p = Shape.Pivot(t, V(0f, -0.1f, -0.33f), V(-18f, 0f, 0f));
                var cols = new[] { C(0.97f, 0.97f, 0.95f), C(0.55f, 0.35f, 0.2f), C(0.97f, 0.97f, 0.95f) };
                var tip = S(C(0.15f, 0.12f, 0.1f), 0.3f);
                for (int k = -1; k <= 1; k++)
                {
                    Shape.Ball(p, V(k * 0.08f, 0.2f, 0f), V(0.08f, 0.44f, 0.03f), S(cols[k + 1], 0.3f), V(0f, 0f, -k * 14f));
                    Shape.Ball(p, V(k * 0.124f, 0.375f, 0f), V(0.06f, 0.12f, 0.032f), tip, V(0f, 0f, -k * 14f));
                }
            }),
            H("samurai", "Samurai helmet", (t, c) =>
            {
                var navy = S(C(0.18f, 0.2f, 0.3f), 0.75f, 0.5f);
                var gold = S(C(1f, 0.8f, 0.3f), 0.85f, 0.7f);
                Shape.Ball(t, V(0f, -0.15f, 0f), V(0.86f, 0.6f, 0.86f), navy);
                Shape.Box(t, V(0f, -0.11f, 0.39f), V(0.6f, 0.025f, 0.18f), navy, V(15f, 0f, 0f));
                Shape.Ring(t, V(0f, 0.27f, 0.33f), 0.42f, 0.035f, gold, V(90f, 0f, 0f), 140f);
                Shape.Ball(t, V(0f, 0.06f, 0.36f), V(0.08f, 0.08f, 0.04f), gold);
                Shape.Ring(t, V(0f, -0.2f, 0f), 0.94f, 0.05f, navy, V(0f, 180f, 0f), 200f);
                Shape.Ring(t, V(0f, -0.27f, 0f), 1.0f, 0.05f, navy, V(0f, 180f, 0f), 200f);
                Shape.Ring(t, V(0f, -0.34f, 0f), 1.06f, 0.05f, gold, V(0f, 180f, 0f), 200f);
                foreach (float s in new[] { -1f, 1f })
                    Shape.Ball(t, V(0.4f * s, -0.16f, 0.12f), 0.06f, S(C(0.8f, 0.15f, 0.15f), 0.4f));
            }),
            H("jester", "Jester hat", (t, c) =>
            {
                var purple = S(C(0.5f, 0.2f, 0.75f), 0.5f);
                var gold = S(C(1f, 0.8f, 0.3f), 0.85f, 0.6f);
                Shape.Ball(t, V(0f, -0.15f, 0f), V(0.8f, 0.56f, 0.8f), purple);
                Shape.Ring(t, V(0f, -0.16f, 0f), 0.8f, 0.08f, gold);
                var arms = new[]
                {
                    (V(-0.1f, -0.03f, 0f), V(0f, 0f, 48f), V(0f, 0f, 45f), purple),
                    (V(0.1f, -0.03f, 0f), V(0f, 0f, -48f), V(0f, 0f, -45f), S(C(1f, 0.82f, 0.2f), 0.5f)),
                    (V(0f, -0.03f, -0.1f), V(-48f, 0f, 0f), V(-45f, 0f, 0f), S(C(0.25f, 0.7f, 0.35f), 0.5f)),
                };
                foreach (var (pos, tilt, bend, col) in arms)
                {
                    var p = Shape.Pivot(t, pos, tilt);
                    Shape.Cone(p, Vector3.zero, 0.3f, 0.3f, col, default, 0.5f);
                    var p2 = Shape.Pivot(p, V(0f, 0.3f, 0f), bend);
                    Shape.Cone(p2, Vector3.zero, 0.15f, 0.24f, col);
                    Shape.Ball(p2, V(0f, 0.26f, 0f), 0.09f, gold);
                }
            }),
            H("safari", "Safari hat", (t, c) =>
            {
                var khaki = S(C(0.82f, 0.74f, 0.52f), 0.2f);
                Shape.Dome(t, V(0f, -0.12f, 0f), 0.7f, 0.4f, khaki);
                Shape.Cone(t, V(0f, -0.18f, 0f), 1.0f, 0.08f, khaki, default, 0.7f);
                Shape.Cyl(t, V(0f, -0.12f, 0f), 0.71f, 0.06f, S(C(0.42f, 0.3f, 0.18f), 0.3f));
                Shape.Ball(t, V(0f, 0.28f, 0f), 0.06f, khaki);
            }),
            H("hardhat", "Hard hat", (t, c) =>
            {
                var y = S(C(1f, 0.82f, 0.1f), 0.75f);
                Shape.Dome(t, V(0f, -0.12f, 0f), 0.76f, 0.38f, y);
                Shape.Ring(t, V(0f, -0.12f, 0f), 0.78f, 0.04f, y);
                Shape.Ball(t, V(0f, -0.12f, 0.3f), V(0.62f, 0.04f, 0.36f), y);
                Shape.Ring(t, V(0f, -0.12f, 0f), 0.78f, 0.06f, y, OverTop, 160f);
                Shape.Star(t, V(0f, 0.04f, 0.355f), 0.12f, 0.02f, S(Color.Lerp(c, Color.white, 0.1f), 0.6f), V(-25f, 0f, 0f));
            }),
            H("nurse", "Nurse cap", (t, c) =>
            {
                var w = S(C(0.97f, 0.97f, 0.98f), 0.3f);
                var red = Mats.Solid(C(0.9f, 0.15f, 0.18f));
                var p = Shape.Pivot(t, V(0f, 0.04f, 0.13f), V(-28f, 0f, 0f));
                Shape.Box(p, Vector3.zero, V(0.52f, 0.17f, 0.12f), w);
                Shape.Box(p, V(0f, 0.1f, -0.03f), V(0.5f, 0.05f, 0.08f), S(C(0.9f, 0.9f, 0.93f), 0.3f));
                Shape.Box(p, V(0f, 0f, 0.062f), V(0.1f, 0.03f, 0.01f), red);
                Shape.Box(p, V(0f, 0f, 0.062f), V(0.03f, 0.1f, 0.01f), red);
            }),
            H("bee", "Bee antennae", (t, c) =>
            {
                var k = S(C(0.08f, 0.08f, 0.1f), 0.5f);
                var y = S(C(1f, 0.82f, 0.15f), 0.6f);
                Shape.Ring(t, V(0f, -0.15f, 0f), 0.74f, 0.06f, k);
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = Shape.Pivot(t, V(0.1f * s, -0.04f, 0.12f), V(12f, 0f, -22f * s));
                    Shape.Stick(p, Vector3.zero, V(0f, 0.34f, 0f), 0.03f, k);
                    Shape.Ball(p, V(0f, 0.37f, 0f), 0.1f, y);
                    Shape.Ring(p, V(0f, 0.37f, 0f), 0.1f, 0.02f, k);
                    var w = Wob(p);
                    w.Turn = V(8f, 0f, 6f);
                    w.Speed = 4f;
                }
            }),
            H("dino", "Dino spikes", (t, c) =>
            {
                var g = S(C(0.3f, 0.72f, 0.35f), 0.5f);
                var o = S(C(1f, 0.6f, 0.2f), 0.5f);
                for (int i = 0; i < 6; i++)
                {
                    float a = 50f - i * 24f;
                    float h = 0.2f + 0.08f * Mathf.Clamp01(1f - Mathf.Abs(a + 10f) / 60f);
                    var pos = V(0f, -0.5f + 0.47f * Mathf.Cos(a * Mathf.Deg2Rad), 0.47f * Mathf.Sin(a * Mathf.Deg2Rad));
                    Shape.Spike(t, pos, V(0.05f, h, 0.24f), i % 2 == 0 ? g : o, V(a, 0f, 0f));
                }
            }),
            H("shark", "Shark fin", (t, c) =>
            {
                Shape.Spike(t, V(0f, -0.06f, -0.04f), V(0.07f, 0.52f, 0.44f), S(C(0.5f, 0.58f, 0.68f), 0.6f), V(-22f, 0f, 0f));
                Shape.Spike(t, V(0f, -0.06f, 0.1f), V(0.072f, 0.16f, 0.16f), S(C(0.85f, 0.88f, 0.92f), 0.6f), V(-22f, 0f, 0f));
            }),
            H("snowman", "Snowman buddy", (t, c) =>
            {
                var w = S(C(0.97f, 0.98f, 1f), 0.35f);
                var k = Mats.Solid(C(0.06f, 0.06f, 0.07f));
                var red = S(C(0.85f, 0.15f, 0.18f), 0.3f);
                Shape.Ball(t, V(0f, 0.12f, 0f), 0.36f, w);
                Shape.Ball(t, V(0f, 0.38f, 0f), 0.26f, w);
                foreach (float s in new[] { -1f, 1f }) Shape.Ball(t, V(0.05f * s, 0.42f, 0.11f), 0.035f, k);
                Shape.Cone(t, V(0f, 0.38f, 0.12f), 0.05f, 0.13f, S(C(1f, 0.5f, 0.1f), 0.4f), V(90f, 0f, 0f));
                Shape.Ball(t, V(0f, 0.08f, 0.175f), 0.035f, k);
                Shape.Ball(t, V(0f, 0.16f, 0.175f), 0.035f, k);
                Shape.Ring(t, V(0f, 0.27f, 0f), 0.22f, 0.05f, red);
                Shape.Box(t, V(0.07f, 0.2f, 0.1f), V(0.05f, 0.13f, 0.02f), red, V(0f, 0f, -10f));
                Shape.Cyl(t, V(0f, 0.495f, 0f), 0.2f, 0.02f, k);
                Shape.Cyl(t, V(0f, 0.5f, 0f), 0.13f, 0.14f, k);
            }),
            H("traffic", "Traffic light", (t, c) =>
            {
                var house = S(C(0.15f, 0.15f, 0.17f), 0.6f);
                Shape.Cyl(t, V(0f, -0.04f, 0f), 0.1f, 0.06f, house);
                Shape.Box(t, V(0f, 0.28f, 0f), V(0.22f, 0.52f, 0.18f), house);
                var lights = new[] { (0.43f, C(1f, 0.2f, 0.15f)), (0.28f, C(1f, 0.8f, 0.15f)), (0.13f, C(0.3f, 1f, 0.35f)) };
                foreach (var (y, col) in lights)
                {
                    Shape.Ball(t, V(0f, y, 0.095f), V(0.13f, 0.13f, 0.04f), Shape.Glow(col, 2.4f));
                    Shape.Box(t, V(0f, y + 0.075f, 0.12f), V(0.17f, 0.025f, 0.07f), house);
                }
            }),
            H("orbit", "Orbit ring", (t, c) =>
            {
                var p = Shape.Pivot(t, V(0f, -0.25f, 0f), V(-20f, 0f, -12f), "Orbit");
                Shape.Ring(p, Vector3.zero, 1.25f, 0.025f, Shape.Glow(Color.Lerp(c, Color.white, 0.45f), 2.2f));
                var spin = Shape.Pivot(p, Vector3.zero, Vector3.zero, "Moons");
                Wob(spin).Spin = 70f;
                Shape.Ball(spin, V(0f, 0f, 0.625f), 0.11f, S(C(0.85f, 0.85f, 0.9f), 0.4f));
                Shape.Ball(spin, V(0f, 0f, -0.625f), 0.07f, S(C(1f, 0.75f, 0.4f), 0.5f));
            }),
            H("umbrella", "Tiny umbrella", (t, c) =>
            {
                var red = S(C(0.9f, 0.2f, 0.25f), 0.5f);
                var w = S(C(0.97f, 0.97f, 0.97f), 0.5f);
                var wood = S(C(0.5f, 0.32f, 0.18f), 0.4f);
                Shape.Ring(t, V(0f, -0.13f, 0f), 0.7f, 0.05f, S(C(0.15f, 0.15f, 0.18f), 0.5f));
                Shape.Stick(t, Vector3.zero, V(0f, 0.55f, 0f), 0.03f, wood);
                var p = Shape.Pivot(t, V(0f, 0.52f, 0f), Vector3.zero, "Canopy");
                Shape.Dome(p, Vector3.zero, 1.0f, 0.28f, red);
                for (int i = 0; i < 8; i++)
                {
                    float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                    Shape.Ball(p, V(Mathf.Sin(a) * 0.5f, 0f, Mathf.Cos(a) * 0.5f), 0.08f, w);
                }
                Shape.Ball(p, V(0f, 0.29f, 0f), 0.06f, wood);
                var wb = Wob(p);
                wb.Turn = V(4f, 0f, 4f);
                wb.Speed = 1.5f;
            }),
        };
    }
}
