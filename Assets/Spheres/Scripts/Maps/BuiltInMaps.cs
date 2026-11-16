namespace Spheres
{
    /// <summary>Maps that ship with the game, generated in code.</summary>
    public static class BuiltInMaps
    {
        internal static void RegisterAll()
        {
            MapRegistry.Register("builtin:garden", Garden(), "Spheres");
            MapRegistry.Register("builtin:pillars", Pillars(), "Spheres");
            MapRegistry.Register("builtin:bounce_yard", BounceYard(), "Spheres");
            MapRegistry.Register("builtin:frost_ring", FrostRing(), "Spheres");
            MapRegistry.Register("builtin:spire", Spire(), "Spheres");
            MapRegistry.Register("builtin:crossfire", Crossfire(), "Spheres");
            MapRegistry.Register("builtin:sky_ziggurat", SkyZiggurat(), "Spheres");
            MapRegistry.Register("builtin:longwire_span", LongwireSpan(), "Spheres");
            MapRegistry.Register("builtin:shatter_lake", ShatterLake(), "Spheres");
            MapRegistry.Register("builtin:cargo_maze", CargoMaze(), "Spheres");
            MapRegistry.Register("builtin:ember_keep", EmberKeep(), "Spheres");
            MapRegistry.Register("builtin:halo_isles", HaloIsles(), "Spheres");
            MapRegistry.Register("builtin:grand_plaza", GrandPlaza(), "Spheres");
            MapRegistry.Register("builtin:duel_cell", DuelCell(), "Spheres");
        }

        static MapData New(string name, string desc, int sx, int sy, int sz) =>
            new MapData { name = name, author = "Spheres", description = desc, sizeX = sx, sizeY = sy, sizeZ = sz };

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
            // Floor with a sunken lava pit in the middle.
            m.Box(7, 7, 7, 26, 1, 11, "solid");
            m.Box(7, 7, 22, 26, 1, 11, "solid");
            m.Box(7, 7, 18, 11, 1, 4, "solid");
            m.Box(22, 7, 18, 11, 1, 4, "solid");
            m.Box(18, 6, 18, 4, 1, 4, "hazard");
            m.BoxMirror4(11, 8, 11, 2, 7, 2, "light");
            m.BoxMirror4(7, 8, 18, 1, 3, 4, "dark");
            m.Box(11, 10, 13, 18, 1, 2, "solid");
            m.Box(11, 10, 25, 18, 1, 2, "solid");
            m.BoxMirror4(14, 8, 15, 1, 1, 1, "light");
            m.BoxMirror4(15, 8, 9, 2, 2, 1, "crate");
            m.Spawn(9, 8, 20, 0).Spawn(30, 8, 20, 1).Spawn(20, 8, 9, 2).Spawn(20, 8, 30, 3);
            m.Spawn(9, 8, 16, 0).Spawn(30, 8, 23, 1).Spawn(18, 8, 10, 2).Spawn(21, 8, 29, 3);
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
            // Teams 2/3 start on the bridge; neutral spawns for teams 4-7.
            m.Spawn(20, 10, 13, 2).Spawn(21, 10, 14, 2).Spawn(31, 10, 14, 3).Spawn(30, 10, 13, 3);
            m.Spawn(10, 10, 6, -1).Spawn(41, 10, 21, -1).Spawn(10, 10, 21, -1).Spawn(41, 10, 6, -1);
            return m;
        }

        /// <summary>A stepped pyramid: four tiers linked by stairs, with a crown on top.</summary>
        public static MapData SkyZiggurat()
        {
            var m = New("Sky Ziggurat", "A stepped pyramid of four tiers. Climb the stairs and hold the crown.", 40, 40, 40);
            // Tiers (tops at y = 8, 12, 16, 18).
            m.Box(6, 6, 6, 28, 2, 28, "solid");
            m.Box(11, 8, 11, 18, 4, 18, "light");
            m.Box(15, 12, 15, 10, 4, 10, "solid");
            m.Box(17, 16, 17, 6, 2, 6, "grass");
            // Stairs tier 1 -> 2 on the Z faces, tier 2 -> 3 on the X faces.
            m.BoxMirror4(18, 8, 9, 4, 2, 2, "dark");
            m.BoxMirror4(13, 12, 18, 2, 2, 4, "dark");
            // Corner launch pads on the lowest tier reach the upper tiers.
            m.BoxMirror4(7, 8, 7, 2, 1, 2, "bouncy");
            // Cover.
            m.BoxMirror4(12, 8, 7, 2, 2, 1, "crate");
            m.BoxMirror4(7, 8, 12, 1, 2, 2, "crate");
            m.BoxMirror4(12, 12, 12, 1, 2, 1, "glass");
            m.BoxMirror4(17, 18, 17, 1, 2, 1, "glass");
            // Floating ledges off the X faces.
            m.BoxMirrorX(2, 9, 17, 3, 1, 6, "dark");
            m.Spawn(8, 8, 15, 0).Spawn(15, 8, 8, 0);
            m.Spawn(31, 8, 24, 1).Spawn(24, 8, 31, 1);
            m.Spawn(31, 8, 15, 2).Spawn(24, 8, 8, 2);
            m.Spawn(8, 8, 24, 3).Spawn(15, 8, 31, 3);
            m.Spawn(12, 12, 14, -1).Spawn(27, 12, 25, -1).Spawn(27, 12, 14, -1).Spawn(12, 12, 25, -1);
            return m;
        }

        /// <summary>Two long bases linked by a central bridge, stepping-stone flanks and a glass sky lane.</summary>
        public static MapData LongwireSpan()
        {
            var m = New("Longwire Span", "A long bridge between two bases, with stepping-stone flanks and a breakable sky lane.", 72, 30, 26);
            // Bases (top y = 10) with a back wall.
            m.BoxMirrorX(3, 8, 4, 10, 2, 18, "solid");
            m.BoxMirrorX(3, 10, 4, 2, 4, 18, "dark");
            m.BoxMirrorX(9, 10, 7, 2, 2, 2, "crate");
            m.BoxMirrorX(9, 10, 17, 2, 2, 2, "crate");
            m.BoxMirrorX(5, 10, 12, 2, 1, 2, "bouncy");
            // Central bridge (top y = 10) with cover.
            m.Box(13, 9, 11, 46, 1, 4, "light");
            m.BoxMirrorX(24, 10, 12, 1, 2, 2, "dark");
            m.Box(35, 10, 11, 2, 2, 1, "crate");
            m.Box(35, 10, 14, 2, 2, 1, "crate");
            // Stepping-stone flanks (top y = 8), gaps of 3 cells.
            m.BoxMirrorX(14, 7, 4, 6, 1, 4, "solid");
            m.BoxMirrorX(23, 7, 4, 6, 1, 4, "grass");
            m.Box(32, 7, 4, 8, 1, 4, "solid");
            m.BoxMirrorX(14, 7, 18, 6, 1, 4, "solid");
            m.BoxMirrorX(23, 7, 18, 6, 1, 4, "grass");
            m.Box(32, 7, 18, 8, 1, 4, "solid");
            m.BoxMirrorX(25, 8, 7, 2, 2, 1, "glass");
            m.BoxMirrorX(25, 8, 18, 2, 2, 1, "glass");
            // Breakable sky lane above the bridge (top y = 15), reached from the base pads.
            m.Box(18, 14, 12, 36, 1, 2, "glass");
            m.BoxMirrorX(13, 13, 11, 4, 1, 4, "dark");
            m.Spawn(6, 10, 6, 0).Spawn(6, 10, 9, 0);
            m.Spawn(65, 10, 16, 1).Spawn(65, 10, 19, 1);
            m.Spawn(6, 10, 16, 2).Spawn(6, 10, 19, 2);
            m.Spawn(65, 10, 6, 3).Spawn(65, 10, 9, 3);
            m.Spawn(35, 8, 5, -1).Spawn(36, 8, 20, -1).Spawn(30, 10, 12, -1).Spawn(41, 10, 13, -1);
            return m;
        }

        /// <summary>A frozen lake with a hole in the middle and glass islands you can shoot apart.</summary>
        public static MapData ShatterLake()
        {
            var m = New("Shatter Lake", "A frozen lake dotted with breakable glass islands. Mind the hole in the ice.", 48, 30, 48);
            // Ice sheet (top y = 8) around a central hole.
            m.Box(6, 7, 6, 36, 1, 14, "ice");
            m.Box(6, 7, 28, 36, 1, 14, "ice");
            m.Box(6, 7, 20, 14, 1, 8, "ice");
            m.Box(28, 7, 20, 14, 1, 8, "ice");
            // Mossy shores in the corners (top y = 9).
            m.BoxMirror4(6, 8, 6, 5, 1, 5, "grass");
            // Glass islands on the ice and one floating over the hole.
            m.BoxMirror4(12, 8, 12, 4, 2, 4, "glass");
            m.BoxMirror4(22, 8, 9, 4, 1, 3, "glass");
            m.BoxMirror4(9, 8, 22, 3, 1, 4, "glass");
            m.Box(22, 9, 22, 4, 1, 4, "glass");
            m.BoxMirror4(17, 8, 17, 1, 2, 1, "glass");
            // High glass perches reached with the pads beside the hole.
            m.BoxMirror4(10, 13, 21, 3, 1, 6, "glass");
            m.BoxMirror4(18, 8, 23, 1, 1, 2, "bouncy");
            m.Spawn(7, 9, 7, 0).Spawn(9, 9, 9, 0);
            m.Spawn(40, 9, 40, 1).Spawn(38, 9, 38, 1);
            m.Spawn(40, 9, 7, 2).Spawn(38, 9, 9, 2);
            m.Spawn(7, 9, 40, 3).Spawn(9, 9, 38, 3);
            m.Spawn(23, 8, 7, -1).Spawn(24, 8, 40, -1).Spawn(7, 8, 23, -1).Spawn(40, 8, 24, -1);
            return m;
        }

        /// <summary>A walled maze of breakable crates around a raised center.</summary>
        public static MapData CargoMaze()
        {
            var m = New("Cargo Maze", "A maze of stacked crates. Take the long way round or blast a shortcut.", 44, 28, 44);
            m.Box(4, 7, 4, 36, 1, 36, "solid");
            // Maze walls (3 high, too tall to jump), mirrored into every quadrant. Basalt walls can't be broken.
            m.BoxMirror4(4, 8, 6, 3, 3, 1, "crate");
            m.BoxMirror4(9, 8, 6, 1, 3, 4, "crate");
            m.BoxMirror4(15, 8, 6, 4, 3, 1, "crate");
            m.BoxMirror4(18, 8, 7, 1, 3, 3, "crate");
            m.BoxMirror4(6, 8, 9, 3, 3, 1, "crate");
            m.BoxMirror4(12, 8, 9, 1, 3, 4, "crate");
            m.BoxMirror4(15, 8, 9, 1, 3, 4, "crate");
            m.BoxMirror4(9, 8, 12, 1, 3, 7, "dark");
            m.BoxMirror4(18, 8, 12, 1, 3, 4, "crate");
            m.BoxMirror4(6, 8, 15, 1, 3, 4, "crate");
            m.BoxMirror4(12, 8, 15, 1, 3, 4, "crate");
            m.BoxMirror4(15, 8, 15, 3, 3, 1, "crate");
            m.BoxMirror4(7, 8, 18, 2, 3, 1, "crate");
            m.BoxMirror4(13, 8, 18, 6, 3, 1, "dark");
            // Raised center (top y = 10) with a pad that launches over the walls.
            m.Box(19, 8, 19, 6, 2, 6, "light");
            m.Box(21, 10, 21, 2, 1, 2, "bouncy");
            m.Spawn(5, 8, 5, 0).Spawn(5, 8, 9, 0);
            m.Spawn(38, 8, 38, 1).Spawn(38, 8, 34, 1);
            m.Spawn(38, 8, 5, 2).Spawn(38, 8, 9, 2);
            m.Spawn(5, 8, 38, 3).Spawn(5, 8, 34, 3);
            m.Spawn(20, 8, 8, -1).Spawn(23, 8, 35, -1).Spawn(8, 8, 23, -1).Spawn(35, 8, 20, -1);
            return m;
        }

        /// <summary>A walled castle on an island, ringed by a lava moat with four drawbridges.</summary>
        public static MapData EmberKeep()
        {
            var m = New("Ember Keep", "Storm the castle across the drawbridges. The moat is lava.", 48, 34, 48);
            m.Box(4, 5, 4, 40, 1, 40, "dark");
            // Outer banks (top y = 8).
            m.Box(4, 6, 4, 40, 2, 6, "solid");
            m.Box(4, 6, 38, 40, 2, 6, "solid");
            m.Box(4, 6, 10, 6, 2, 28, "solid");
            m.Box(38, 6, 10, 6, 2, 28, "solid");
            // Lava moat (top y = 7).
            m.Box(10, 6, 10, 28, 1, 4, "hazard");
            m.Box(10, 6, 34, 28, 1, 4, "hazard");
            m.Box(10, 6, 14, 4, 1, 20, "hazard");
            m.Box(34, 6, 14, 4, 1, 20, "hazard");
            // Courtyard (top y = 9) and drawbridges.
            m.Box(14, 6, 14, 20, 3, 20, "light");
            m.BoxMirror4(22, 8, 10, 4, 1, 4, "solid");
            m.BoxMirror4(10, 8, 22, 4, 1, 4, "solid");
            // Corner towers (top y = 14), walls (top y = 12) and steps up to them.
            m.BoxMirror4(14, 9, 14, 3, 5, 3, "dark");
            m.BoxMirror4(17, 9, 14, 5, 3, 1, "solid");
            m.BoxMirror4(14, 9, 17, 1, 3, 5, "solid");
            m.BoxMirror4(17, 9, 15, 2, 1, 2, "solid");
            // Central keep (top y = 13) with steps.
            m.Box(20, 9, 20, 8, 4, 8, "dark");
            m.BoxMirror4(22, 9, 18, 4, 2, 2, "solid");
            m.Box(23, 13, 23, 2, 2, 2, "glass");
            // Bank pads launch over the moat; crates for cover outside.
            m.BoxMirror4(6, 8, 6, 2, 1, 2, "bouncy");
            m.BoxMirror4(5, 8, 15, 2, 2, 1, "crate");
            m.BoxMirror4(15, 8, 5, 1, 2, 2, "crate");
            m.Spawn(6, 8, 19, 0).Spawn(6, 8, 28, 0);
            m.Spawn(41, 8, 19, 1).Spawn(41, 8, 28, 1);
            m.Spawn(19, 8, 6, 2).Spawn(28, 8, 6, 2);
            m.Spawn(19, 8, 41, 3).Spawn(28, 8, 41, 3);
            m.Spawn(17, 9, 23, -1).Spawn(30, 9, 24, -1).Spawn(23, 9, 17, -1).Spawn(24, 9, 30, -1);
            return m;
        }

        /// <summary>A ring of floating islands linked by bounce pads around a low hub.</summary>
        public static MapData HaloIsles()
        {
            var m = New("Halo Isles", "A ring of floating islands. Drop onto the pads to hop between them and the hub.", 52, 40, 52);
            // Four axis islands (top y = 11) and four diagonal islands (top y = 12).
            m.BoxMirror4(4, 9, 23, 7, 2, 6, "grass");
            m.BoxMirror4(23, 9, 4, 6, 2, 7, "grass");
            m.BoxMirror4(10, 10, 10, 6, 2, 6, "grass");
            m.BoxMirror4(5, 8, 24, 5, 1, 4, "dark");
            m.BoxMirror4(24, 8, 5, 4, 1, 5, "dark");
            m.BoxMirror4(11, 9, 11, 4, 1, 4, "dark");
            // Bounce pads between neighbouring islands and toward the hub.
            m.BoxMirror4(8, 7, 18, 2, 1, 2, "bouncy");
            m.BoxMirror4(18, 7, 8, 2, 1, 2, "bouncy");
            m.BoxMirror4(12, 6, 25, 2, 1, 2, "bouncy");
            m.BoxMirror4(25, 6, 12, 2, 1, 2, "bouncy");
            // Low hub (top y = 8) with a pad up to the glass halo.
            m.Box(20, 6, 20, 12, 2, 12, "light");
            m.Box(25, 8, 25, 2, 1, 2, "bouncy");
            m.BoxMirror4(20, 8, 20, 1, 1, 1, "bouncy");
            m.BoxMirror4(21, 17, 21, 4, 1, 4, "glass");
            m.BoxMirror4(22, 8, 22, 1, 2, 1, "dark");
            // Cover on the islands.
            m.BoxMirror4(9, 11, 23, 1, 2, 2, "crate");
            m.BoxMirror4(23, 11, 9, 2, 2, 1, "crate");
            m.BoxMirror4(14, 12, 14, 1, 2, 1, "glass");
            m.Spawn(6, 11, 24, 0).Spawn(6, 11, 27, 0);
            m.Spawn(45, 11, 24, 1).Spawn(45, 11, 27, 1);
            m.Spawn(24, 11, 6, 2).Spawn(27, 11, 6, 2);
            m.Spawn(24, 11, 45, 3).Spawn(27, 11, 45, 3);
            m.Spawn(12, 12, 12, -1).Spawn(39, 12, 39, -1).Spawn(39, 12, 12, -1).Spawn(12, 12, 39, -1);
            return m;
        }

        /// <summary>A large open floor for eight players with scattered cover.</summary>
        public static MapData GrandPlaza()
        {
            var m = New("Grand Plaza", "A wide open square for eight players, with scattered cover and glass perches.", 64, 30, 64);
            m.Box(6, 6, 6, 52, 2, 52, "light");
            // Scattered cover, mirrored into every quadrant.
            m.BoxMirror4(12, 8, 12, 3, 2, 1, "crate");
            m.BoxMirror4(12, 8, 13, 1, 2, 2, "crate");
            m.BoxMirror4(20, 8, 10, 1, 3, 5, "dark");
            m.BoxMirror4(10, 8, 20, 5, 3, 1, "dark");
            m.BoxMirror4(18, 8, 18, 2, 2, 2, "crate");
            m.BoxMirror4(26, 8, 16, 4, 1, 1, "solid");
            m.BoxMirror4(16, 8, 26, 1, 1, 4, "solid");
            m.BoxMirror4(24, 8, 24, 2, 4, 2, "dark");
            m.BoxMirror4(30, 8, 12, 4, 2, 1, "glass");
            m.BoxMirror4(12, 8, 30, 1, 2, 4, "glass");
            m.BoxMirror4(8, 8, 26, 2, 1, 2, "solid");
            // Center dais (top y = 9) and floating glass perches (top y = 14).
            m.Box(29, 8, 29, 6, 1, 6, "solid");
            m.Box(31, 9, 31, 2, 1, 2, "bouncy");
            m.BoxMirror4(22, 13, 22, 3, 1, 3, "glass");
            m.BoxMirror4(20, 8, 20, 1, 1, 1, "bouncy");
            // One spawn group per team at the eight compass points.
            m.Spawn(8, 8, 31, 0).Spawn(8, 8, 33, 0);
            m.Spawn(55, 8, 32, 1).Spawn(55, 8, 30, 1);
            m.Spawn(32, 8, 8, 2).Spawn(30, 8, 8, 2);
            m.Spawn(31, 8, 55, 3).Spawn(33, 8, 55, 3);
            m.Spawn(9, 8, 9, 4).Spawn(54, 8, 54, 5).Spawn(54, 8, 9, 6).Spawn(9, 8, 54, 7);
            m.Spawn(20, 8, 31, -1).Spawn(43, 8, 32, -1).Spawn(32, 8, 20, -1).Spawn(31, 8, 43, -1);
            return m;
        }

        /// <summary>A small, tight box for one-on-one fights.</summary>
        public static MapData DuelCell()
        {
            var m = New("Duel Cell", "A small, tight box for one-on-one duels. Nowhere to hide for long.", 24, 22, 24);
            m.Box(4, 6, 4, 16, 2, 16, "solid");
            m.Box(11, 8, 11, 2, 4, 2, "dark");
            m.BoxMirror4(7, 8, 7, 2, 2, 1, "crate");
            m.BoxMirror4(5, 8, 11, 1, 2, 2, "glass");
            // Side ledges (top y = 11) reached by a step at each end.
            m.BoxMirror4(9, 10, 4, 6, 1, 2, "light");
            m.BoxMirror4(15, 8, 4, 2, 1, 2, "solid");
            m.Spawn(5, 8, 7, 0).Spawn(5, 8, 16, 0);
            m.Spawn(18, 8, 16, 1).Spawn(18, 8, 7, 1);
            m.Spawn(9, 8, 7, 2).Spawn(14, 8, 7, 2);
            m.Spawn(9, 8, 16, 3).Spawn(14, 8, 16, 3);
            m.Spawn(10, 11, 4, -1).Spawn(13, 11, 5, -1).Spawn(10, 11, 19, -1).Spawn(13, 11, 18, -1);
            return m;
        }
    }
}
