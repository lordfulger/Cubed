using UnityEngine;

namespace Cubed
{
    /// <summary>Team colors and names. Teams are 0-based indices.</summary>
    public static class Teams
    {
        public const int Max = 8;

        static readonly Color[] colors =
        {
            new Color(0.95f, 0.32f, 0.28f),
            new Color(0.26f, 0.56f, 1.00f),
            new Color(0.32f, 0.86f, 0.42f),
            new Color(1.00f, 0.78f, 0.20f),
            new Color(0.72f, 0.42f, 1.00f),
            new Color(0.25f, 0.90f, 0.92f),
            new Color(1.00f, 0.55f, 0.18f),
            new Color(1.00f, 0.45f, 0.75f),
        };

        static readonly string[] names = { "Red", "Blue", "Green", "Gold", "Purple", "Cyan", "Orange", "Pink" };

        public static Color GetColor(int team) =>
            team >= 0 && team < colors.Length ? colors[team] : Color.gray;

        public static string GetName(int team) =>
            team >= 0 && team < names.Length ? names[team] : "None";

        static readonly Color[][] palettes =
        {
            null, // default: whatever is in colors (possibly modded)
            new[]
            {
                // Okabe-Ito based, distinguishable for the common color-vision deficiencies.
                new Color(0.90f, 0.62f, 0.00f), new Color(0.34f, 0.71f, 0.91f), new Color(0.00f, 0.62f, 0.45f), new Color(0.94f, 0.89f, 0.26f),
                new Color(0.00f, 0.45f, 0.70f), new Color(0.84f, 0.37f, 0.00f), new Color(0.80f, 0.47f, 0.65f), new Color(0.80f, 0.80f, 0.80f),
            },
            new[]
            {
                new Color(1f, 0.15f, 0.15f), new Color(0.15f, 0.45f, 1f), new Color(1f, 0.95f, 0.1f), new Color(1f, 1f, 1f),
                new Color(1f, 0.2f, 1f), new Color(0.1f, 1f, 1f), new Color(1f, 0.55f, 0f), new Color(0.3f, 1f, 0.3f),
            },
        };
        static readonly string[][] paletteNames =
        {
            null,
            new[] { "Orange", "Sky", "Teal", "Yellow", "Blue", "Vermilion", "Pink", "Silver" },
            new[] { "Red", "Blue", "Yellow", "White", "Magenta", "Cyan", "Orange", "Lime" },
        };
        static Color[] defaultColors;
        static string[] defaultNames;

        /// <summary>Switch to an accessibility palette (0 default, 1 colorblind-safe, 2 high contrast).</summary>
        public static void SetPalette(int palette)
        {
            if (defaultColors == null) { defaultColors = (Color[])colors.Clone(); defaultNames = (string[])names.Clone(); }
            palette = Mathf.Clamp(palette, 0, palettes.Length - 1);
            var c = palette == 0 ? defaultColors : palettes[palette];
            var n = palette == 0 ? defaultNames : paletteNames[palette];
            for (int i = 0; i < Max; i++) { colors[i] = c[i]; names[i] = n[i]; }
        }

        /// <summary>Lets mods recolor or rename teams.</summary>
        public static void SetTeam(int team, string name, Color color)
        {
            if (team < 0 || team >= Max) return;
            names[team] = name;
            colors[team] = color;
        }
    }
}
