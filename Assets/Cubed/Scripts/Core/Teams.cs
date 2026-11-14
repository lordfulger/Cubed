using UnityEngine;

namespace Cubed
{
    /// <summary>Team colors and names. Teams are 0-based indices.</summary>
    public static class Teams
    {
        public const int Max = 4;

        static readonly Color[] colors =
        {
            new Color(0.95f, 0.32f, 0.28f),
            new Color(0.26f, 0.56f, 1.00f),
            new Color(0.32f, 0.86f, 0.42f),
            new Color(1.00f, 0.78f, 0.20f),
        };

        static readonly string[] names = { "Red", "Blue", "Green", "Gold" };

        public static Color GetColor(int team) =>
            team >= 0 && team < colors.Length ? colors[team] : Color.gray;

        public static string GetName(int team) =>
            team >= 0 && team < names.Length ? names[team] : "None";

        /// <summary>Lets mods recolor or rename teams.</summary>
        public static void SetTeam(int team, string name, Color color)
        {
            if (team < 0 || team >= Max) return;
            names[team] = name;
            colors[team] = color;
        }
    }
}
