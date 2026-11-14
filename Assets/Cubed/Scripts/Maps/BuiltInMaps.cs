namespace Cubed
{
    /// <summary>Maps that ship with the game, generated in code.</summary>
    public static class BuiltInMaps
    {
        internal static void RegisterAll()
        {
            MapRegistry.Register("builtin:garden", Garden(), "Cubed");
            MapRegistry.Register("builtin:pillars", Pillars(), "Cubed");
            MapRegistry.Register("builtin:bounce_yard", BounceYard(), "Cubed");
            MapRegistry.Register("builtin:frost_ring", FrostRing(), "Cubed");
            MapRegistry.Register("builtin:spire", Spire(), "Cubed");
            MapRegistry.Register("builtin:crossfire", Crossfire(), "Cubed");
        }

        static MapData New(string name, string desc, int sx, int sy, int sz) =>
            new MapData { name = name, author = "Cubed", description = desc, sizeX = sx, sizeY = sy, sizeZ = sz };

        /// <summary>Floating islands around a central plateau.</summary>
        public static MapData Garden()
        {
            var m = New("Cube Garden", "Floating islands around a central plateau.", 44, 34, 44);
            m.Box(15, 8, 15, 14, 2, 14, "grass");
            m.Box(16, 7, 16, 12, 1, 12, "solid");
            m.Box(20, 10, 20, 4, 2, 4, "light");
            m.BoxMirror4(3, 11, 3, 8, 2, 8, "grass");
            m.BoxMirror4(4, 10, 4, 6, 1, 6, "solid");
            m.BoxMirror4(6, 13, 9, 1, 2, 3, "crate");
            m.BoxMirror4(12, 9, 12, 2, 1, 2, "bouncy");
            m.BoxMirror4(17, 10, 17, 2, 2, 1, "light");
            m.Spawn(7, 13, 7, 0).Spawn(36, 13, 36, 1).Spawn(36, 13, 7, 2).Spawn(7, 13, 36, 3);
            m.Spawn(5, 13, 9, 0).Spawn(38, 13, 34, 1).Spawn(38, 13, 9, 2).Spawn(5, 13, 34, 3);
            return m;
        }

        /// <summary>A broad floor with cover pillars and a lava pit.</summary>
        public static MapData Pillars()
        {
            var m = New("Pillar Hall", "Cover pillars, high bridges and a lava pit.", 40, 30, 40);
            m.Box(7, 7, 7, 26, 1, 26, "solid");
            m.Box(18, 7, 18, 4, 1, 4, "hazard");
            m.BoxMirror4(11, 8, 11, 2, 7, 2, "light");
            m.BoxMirror4(7, 8, 18, 1, 3, 4, "dark");
            m.Box(11, 10, 13, 18, 1, 2, "solid");
            m.Box(11, 10, 25, 18, 1, 2, "solid");
            m.BoxMirror4(14, 8, 15, 1, 1, 1, "light");
            m.BoxMirror4(15, 8, 9, 2, 2, 1, "crate");
            m.Spawn(9, 8, 20, 0).Spawn(30, 8, 20, 1).Spawn(20, 8, 9, 2).Spawn(20, 8, 30, 3);
            m.Spawn(9, 8, 16, 0).Spawn(30, 8, 23, 1).Spawn(16, 8, 9, 2).Spawn(23, 8, 30, 3);
            return m;
        }

        /// <summary>Separated platforms connected by bounce pads.</summary>
        public static MapData BounceYard()
        {
            var m = New("Bounce Yard", "Split platforms linked by bounce pads. Watch the gaps.", 46, 36, 30);
            m.Box(3, 9, 9, 10, 2, 12, "solid");
            m.Box(33, 9, 9, 10, 2, 12, "solid");
            m.Box(18, 12, 10, 10, 2, 10, "light");
            m.Box(14, 6, 13, 3, 1, 4, "bouncy");
            m.Box(29, 6, 13, 3, 1, 4, "bouncy");
            m.Box(21, 14, 14, 4, 1, 2, "bouncy");
            m.Box(8, 11, 12, 1, 2, 6, "glass");
            m.Box(37, 11, 12, 1, 2, 6, "glass");
            m.Box(20, 20, 21, 6, 1, 6, "solid");
            m.Spawn(5, 11, 14, 0).Spawn(40, 11, 14, 1).Spawn(5, 11, 18, 2).Spawn(40, 11, 18, 3);
            m.Spawn(5, 11, 11, 0).Spawn(40, 11, 11, 1);
            return m;
        }

        /// <summary>Slippery ice ring around a raised center.</summary>
        public static MapData FrostRing()
        {
            var m = New("Frost Ring", "Slippery ice ring. Keep your footing near the borders.", 40, 28, 40);
            m.Box(6, 8, 6, 28, 1, 6, "ice");
            m.Box(6, 8, 28, 28, 1, 6, "ice");
            m.Box(6, 8, 12, 6, 1, 16, "ice");
            m.Box(28, 8, 12, 6, 1, 16, "ice");
            m.Box(14, 9, 14, 12, 1, 12, "solid");
            m.Box(17, 10, 17, 6, 1, 6, "light");
            m.BoxMirror4(15, 10, 15, 1, 2, 1, "glass");
            m.BoxMirror4(8, 9, 8, 2, 2, 2, "crate");
            m.Spawn(8, 9, 20, 0).Spawn(31, 9, 20, 1).Spawn(20, 9, 8, 2).Spawn(20, 9, 31, 3);
            m.Spawn(8, 9, 24, 0).Spawn(31, 9, 16, 1);
            return m;
        }

        /// <summary>Vertical tower with rings of ledges.</summary>
        public static MapData Spire()
        {
            var m = New("Spire", "A tall tower with ledges. Height is power.", 36, 44, 36);
            m.Box(15, 6, 15, 6, 22, 6, "dark");
            m.Box(10, 8, 10, 16, 1, 16, "solid");
            m.BoxMirror4(5, 13, 5, 6, 1, 6, "light");
            m.BoxMirrorX(4, 18, 15, 6, 1, 6, "solid");
            m.Box(15, 18, 4, 6, 1, 6, "solid");
            m.Box(15, 18, 26, 6, 1, 6, "solid");
            m.Box(13, 28, 13, 10, 1, 10, "light");
            m.BoxMirror4(11, 9, 11, 1, 1, 1, "bouncy");
            m.BoxMirrorX(6, 19, 17, 2, 1, 2, "bouncy");
            m.Spawn(12, 9, 13, 0).Spawn(23, 9, 22, 1).Spawn(23, 9, 13, 2).Spawn(12, 9, 22, 3);
            m.Spawn(7, 14, 7, 0).Spawn(28, 14, 28, 1).Spawn(28, 14, 7, 2).Spawn(7, 14, 28, 3);
            return m;
        }

        /// <summary>Two bases facing each other over a gap with a middle bridge.</summary>
        public static MapData Crossfire()
        {
            var m = New("Crossfire", "Two bases and a narrow bridge. Built for team fights.", 52, 30, 28);
            m.Box(3, 8, 4, 14, 2, 20, "solid");
            m.Box(35, 8, 4, 14, 2, 20, "solid");
            m.Box(17, 9, 12, 18, 1, 4, "light");
            m.Box(25, 10, 12, 2, 2, 1, "crate");
            m.Box(25, 10, 15, 2, 2, 1, "crate");
            m.BoxMirrorX(12, 10, 6, 2, 3, 3, "dark");
            m.BoxMirrorX(12, 10, 19, 2, 3, 3, "dark");
            m.BoxMirrorX(6, 12, 10, 4, 1, 8, "solid");
            m.BoxMirrorX(10, 10, 13, 1, 1, 2, "light");
            m.BoxMirrorX(18, 6, 4, 3, 1, 3, "bouncy");
            m.BoxMirrorX(18, 6, 21, 3, 1, 3, "bouncy");
            m.Spawn(5, 10, 8, 0).Spawn(5, 10, 13, 0).Spawn(5, 10, 18, 0).Spawn(8, 10, 13, 0);
            m.Spawn(46, 10, 8, 1).Spawn(46, 10, 13, 1).Spawn(46, 10, 18, 1).Spawn(43, 10, 13, 1);
            return m;
        }
    }
}
