using System;

namespace Spheres
{
    /// <summary>Built-in translation tables (English text → translation). See <see cref="Loc"/>.</summary>
    public static partial class LocTables
    {
        /// <summary>Called once by <see cref="Loc"/>: add(languageCode, english, translation).</summary>
        public static void Fill(Action<string, string, string> add)
        {
            Loc.Name("es", "Español");
            Loc.Name("fr", "Français");
            Loc.Name("de", "Deutsch");
            Loc.Name("pt", "Português (Brasil)");
            Loc.Name("it", "Italiano");
            Loc.Name("pl", "Polski");
            FillTables(add);
        }

        static partial void FillTables(Action<string, string, string> add);
    }
}
