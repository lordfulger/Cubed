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
            // Moving parts, destructibles and physics props.
            MapRegistry.Register("builtin:clockwork", Clockwork(), "Spheres");
            MapRegistry.Register("builtin:pendulum_hall", PendulumHall(), "Spheres");
            MapRegistry.Register("builtin:blast_doors", BlastDoors(), "Spheres");
            MapRegistry.Register("builtin:crate_yard", CrateYard(), "Spheres");
            MapRegistry.Register("builtin:sawmill", Sawmill(), "Spheres");
            MapRegistry.Register("builtin:spike_garden", SpikeGarden(), "Spheres");
            MapRegistry.Register("builtin:piston_press", PistonPress(), "Spheres");
            MapRegistry.Register("builtin:elevator_shaft", ElevatorShaft(), "Spheres");
            MapRegistry.Register("builtin:powder_keg", PowderKeg(), "Spheres");
            MapRegistry.Register("builtin:glasshouse", Glasshouse(), "Spheres");
            MapRegistry.Register("builtin:windmill", Windmill(), "Spheres");
            MapRegistry.Register("builtin:moat_run", MoatRun(), "Spheres");
            MapRegistry.Register("builtin:rush_hour", RushHour(), "Spheres");
            MapRegistry.Register("builtin:tumble_tower", TumbleTower(), "Spheres");
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
            // ================================================================== moving-part maps

        /// <summary>Spinning sweepers, a spiked rotor overhead and pistons punching in from every side.</summary>
        public static MapData Clockwork()
        {
            var m = New("Clockwork", "Jump the sweeping arm, duck the spiked rotor, and don't stand in front of the pistons.", 44, 34, 44);
            m.background = "#1d1726";
            m.Box(8, 7, 8, 28, 1, 28, "solid");
            m.Box(21, 8, 21, 2, 7, 2, "dark");
            // Low sweeper (jump it) and a spiked rotor at head height on the ledges.
            m.Mech(MechKinds.Spinner, 12, 8, 21, k => { k.w = 20; k.h = 1; k.d = 2; k.period = 7f; k.type = "light"; });
            m.Mech(MechKinds.Spinner, 14, 13, 21, k => { k.w = 16; k.h = 1; k.d = 2; k.period = -9f; k.deadly = true; k.damage = 30f; });
            // Pistons from each side, one after another.
            m.Mech(MechKinds.Piston, 8, 8, 20, k => { k.w = 2; k.h = 2; k.d = 4; k.dx = 4f; k.period = 4f; k.phase = 0f; });
            m.Mech(MechKinds.Piston, 34, 8, 20, k => { k.w = 2; k.h = 2; k.d = 4; k.dx = -4f; k.period = 4f; k.phase = 0.5f; });
            m.Mech(MechKinds.Piston, 20, 8, 8, k => { k.dx = 0f; k.w = 4; k.h = 2; k.d = 2; k.dz = 4f; k.period = 4f; k.phase = 0.25f; });
            m.Mech(MechKinds.Piston, 20, 8, 34, k => { k.dx = 0f; k.w = 4; k.h = 2; k.d = 2; k.dz = -4f; k.period = 4f; k.phase = 0.75f; });
            // Corner ledges with a step up.
            m.BoxMirror4(10, 10, 10, 4, 1, 4, "light");
            m.BoxMirror4(14, 8, 12, 1, 1, 2, "solid");
            m.PropMirror4(PropKinds.Box, 12f, 11f, 12.5f);
            m.PropMirror4(PropKinds.Barrel, 10.5f, 8f, 16.5f);
            m.Spawn(9, 8, 9, 0).Spawn(9, 8, 11, 0);
            m.Spawn(34, 8, 34, 1).Spawn(34, 8, 32, 1);
            m.Spawn(34, 8, 9, 2).Spawn(34, 8, 11, 2);
            m.Spawn(9, 8, 34, 3).Spawn(9, 8, 32, 3);
            m.Spawn(11, 11, 11, -1).Spawn(32, 11, 32, -1).Spawn(32, 11, 11, -1).Spawn(11, 11, 32, -1);
            return m;
        }

        /// <summary>A long hall of swinging spiked balls. Time your crossing.</summary>
        public static MapData PendulumHall()
        {
            var m = New("Pendulum Hall", "Five spiked pendulums swing across a long hall. Block or time it right.", 44, 36, 24);
            m.background = "#20141a";
            m.Box(2, 7, 5, 40, 1, 13, "solid");
            m.Box(2, 24, 10, 40, 1, 3, "dark");
            m.Mech(MechKinds.Pendulum, 9, 23, 11, k => { k.axis = 0; k.length = 14f; k.angle = 55f; k.period = 3.4f; k.phase = 0f; k.w = 2; });
            m.Mech(MechKinds.Pendulum, 15, 23, 11, k => { k.axis = 0; k.length = 14f; k.angle = 55f; k.period = 3.4f; k.phase = 0.2f; k.w = 2; });
            m.Mech(MechKinds.Pendulum, 21, 23, 11, k => { k.axis = 0; k.length = 14f; k.angle = 55f; k.period = 3.4f; k.phase = 0.4f; k.w = 2; });
            m.Mech(MechKinds.Pendulum, 27, 23, 11, k => { k.axis = 0; k.length = 14f; k.angle = 55f; k.period = 3.4f; k.phase = 0.6f; k.w = 2; });
            m.Mech(MechKinds.Pendulum, 33, 23, 11, k => { k.axis = 0; k.length = 14f; k.angle = 55f; k.period = 3.4f; k.phase = 0.8f; k.w = 2; });
            // Spike traps hugging the walls between the pendulums.
            m.Mech(MechKinds.Spikes, 17, 8, 5, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 17, 8, 16, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 29, 8, 5, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 29, 8, 16, k => { k.phase = 0.75f; });
            // Cover at each end.
            m.Box(6, 8, 7, 1, 2, 2, "crate").Box(6, 8, 14, 1, 2, 2, "crate");
            m.Box(37, 8, 7, 1, 2, 2, "crate").Box(37, 8, 14, 1, 2, 2, "crate");
            m.Prop(PropKinds.Crate, 12.5f, 8f, 6.5f).Prop(PropKinds.Crate, 24.5f, 8f, 16.5f).Prop(PropKinds.Box, 18.5f, 8f, 11.5f);
            m.Spawn(4, 8, 8, 0).Spawn(4, 8, 15, 0);
            m.Spawn(39, 8, 8, 1).Spawn(39, 8, 15, 1);
            m.Spawn(5, 8, 11, 2).Spawn(5, 8, 12, 2);
            m.Spawn(38, 8, 11, 3).Spawn(38, 8, 12, 3);
            m.Spawn(3, 8, 10, -1).Spawn(40, 8, 13, -1).Spawn(3, 8, 13, -1).Spawn(40, 8, 10, -1);
            return m;
        }

        /// <summary>Two halves split by a wall with sliding blast doors and a glass window.</summary>
        public static MapData BlastDoors()
        {
            var m = New("Blast Doors", "The doors open on a timer. Or take the bouncer over the wall.", 44, 30, 40);
            m.background = "#121a24";
            m.Box(4, 7, 4, 36, 1, 32, "solid");
            m.Box(21, 8, 4, 2, 6, 6, "dark");
            m.Box(21, 8, 14, 2, 2, 12, "dark");
            m.Box(21, 10, 14, 2, 2, 3, "dark").Box(21, 10, 17, 2, 2, 6, "glass").Box(21, 10, 23, 2, 2, 3, "dark");
            m.Box(21, 12, 14, 2, 2, 12, "dark");
            m.Box(21, 8, 30, 2, 6, 6, "dark");
            m.Mech(MechKinds.Door, 21, 8, 10, k => { k.w = 2; k.h = 6; k.d = 4; k.dy = 5.5f; k.period = 8f; k.phase = 0f; });
            m.Mech(MechKinds.Door, 21, 8, 26, k => { k.w = 2; k.h = 6; k.d = 4; k.dy = 5.5f; k.period = 8f; k.phase = 0.5f; });
            m.BoxMirrorX(17, 8, 19, 2, 1, 2, "bouncy");
            m.BoxMirror4(10, 8, 10, 2, 2, 2, "crate");
            m.BoxMirror4(14, 8, 15, 1, 2, 3, "brick");
            m.PropMirror4(PropKinds.Barrel, 19f, 8f, 8.5f);
            m.PropMirror4(PropKinds.Crate, 8.5f, 8f, 14.5f);
            m.Spawn(7, 8, 9, 0).Spawn(9, 8, 9, 0);
            m.Spawn(36, 8, 30, 1).Spawn(34, 8, 30, 1);
            m.Spawn(7, 8, 30, 2).Spawn(9, 8, 30, 2);
            m.Spawn(36, 8, 9, 3).Spawn(34, 8, 9, 3);
            m.Spawn(6, 8, 20, -1).Spawn(37, 8, 20, -1).Spawn(12, 8, 20, -1).Spawn(31, 8, 20, -1);
            return m;
        }

        /// <summary>A shipping yard full of crates and barrels, with a crane platform overhead.</summary>
        public static MapData CrateYard()
        {
            var m = New("Crate Yard", "Shoot the crates around, mind the barrels, and ride the crane.", 44, 30, 44);
            m.background = "#1a1d22";
            m.Box(4, 7, 4, 36, 1, 36, "solid");
            m.BoxMirror4(9, 8, 9, 6, 3, 3, "dark");
            m.BoxMirror4(9, 8, 15, 3, 3, 6, "dark");
            m.Box(19, 8, 19, 6, 1, 6, "light");
            m.Mech(MechKinds.Platform, 6, 12, 20, k => { k.w = 4; k.h = 1; k.d = 4; k.dx = 28f; k.period = 14f; k.type = "dark"; });
            m.PropStack(PropKinds.Crate, 16f, 8f, 26.5f, 3);
            m.PropStack(PropKinds.Crate, 28f, 8f, 17.5f, 3);
            m.PropMirror4(PropKinds.Barrel, 8f, 8f, 22.5f);
            m.PropMirror4(PropKinds.Box, 17f, 8f, 13f);
            m.PropMirror4(PropKinds.Box, 17.9f, 8f, 13.9f);
            m.PropMirror4(PropKinds.Heavy, 14f, 8f, 30.5f);
            m.Prop(PropKinds.Ball, 22f, 9f, 22f);
            m.Spawn(6, 8, 6, 0).Spawn(6, 8, 8, 0);
            m.Spawn(37, 8, 37, 1).Spawn(37, 8, 35, 1);
            m.Spawn(37, 8, 6, 2).Spawn(37, 8, 8, 2);
            m.Spawn(6, 8, 37, 3).Spawn(6, 8, 35, 3);
            m.Spawn(20, 9, 20, -1).Spawn(23, 9, 23, -1).Spawn(20, 9, 23, -1).Spawn(23, 9, 20, -1);
            return m;
        }

        /// <summary>Saw blades race along the floor under a breakable plank deck.</summary>
        public static MapData Sawmill()
        {
            var m = New("Sawmill", "Saw blades patrol the lanes. The plank deck above breaks if you shoot it.", 44, 30, 36);
            m.background = "#221a14";
            m.Box(4, 7, 4, 36, 1, 28, "solid");
            m.Mech(MechKinds.Saw, 8, 8, 9, k => { k.w = 3; k.d = 3; k.axis = 2; k.dx = 25f; k.period = 7f; k.phase = 0f; });
            m.Mech(MechKinds.Saw, 33, 8, 23, k => { k.w = 3; k.d = 3; k.axis = 2; k.dx = -25f; k.period = 7f; k.phase = 0.5f; });
            m.Box(8, 12, 15, 28, 1, 6, "wood");
            m.BoxMirror4(8, 8, 15, 1, 4, 1, "dark");
            m.BoxMirrorX(5, 8, 17, 2, 1, 2, "bouncy");
            m.BoxMirror4(14, 8, 5, 1, 2, 3, "wood");
            m.PropStack(PropKinds.Crate, 22f, 13f, 18f, 2);
            m.Prop(PropKinds.Barrel, 20f, 8f, 13.5f).Prop(PropKinds.Barrel, 24f, 8f, 21.5f);
            m.PropMirror4(PropKinds.Box, 11.5f, 8f, 14.5f);
            m.Spawn(5, 8, 14, 0).Spawn(6, 8, 14, 0);
            m.Spawn(38, 8, 20, 1).Spawn(37, 8, 20, 1);
            m.Spawn(5, 8, 20, 2).Spawn(6, 8, 20, 2);
            m.Spawn(38, 8, 14, 3).Spawn(37, 8, 14, 3);
            m.Spawn(12, 13, 17, -1).Spawn(31, 13, 17, -1).Spawn(12, 13, 19, -1).Spawn(31, 13, 19, -1);
            return m;
        }

        /// <summary>A mossy courtyard of spike traps firing in waves around a safe plaza.</summary>
        public static MapData SpikeGarden()
        {
            var m = New("Spike Garden", "Spike traps fire in waves. Watch for the red warning glow.", 40, 30, 40);
            m.background = "#14201a";
            m.Box(5, 7, 5, 30, 1, 30, "grass");
            m.Box(18, 8, 18, 4, 1, 4, "light");
            // Spike traps in diagonal waves (the plaza in the middle is safe).
            m.Mech(MechKinds.Spikes, 9, 8, 9, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 9, 8, 13, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 9, 8, 17, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 9, 8, 21, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 9, 8, 25, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 9, 8, 29, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 13, 8, 9, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 13, 8, 13, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 13, 8, 17, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 13, 8, 21, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 13, 8, 25, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 13, 8, 29, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 17, 8, 9, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 17, 8, 13, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 17, 8, 25, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 17, 8, 29, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 21, 8, 9, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 21, 8, 13, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 21, 8, 25, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 21, 8, 29, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 25, 8, 9, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 25, 8, 13, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 25, 8, 17, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 25, 8, 21, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 25, 8, 25, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 25, 8, 29, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 29, 8, 9, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 29, 8, 13, k => { k.phase = 0.5f; });
            m.Mech(MechKinds.Spikes, 29, 8, 17, k => { k.phase = 0.75f; });
            m.Mech(MechKinds.Spikes, 29, 8, 21, k => { k.phase = 0f; });
            m.Mech(MechKinds.Spikes, 29, 8, 25, k => { k.phase = 0.25f; });
            m.Mech(MechKinds.Spikes, 29, 8, 29, k => { k.phase = 0.5f; });
            m.BoxMirror4(12, 8, 12, 1, 2, 1, "solid");
            m.BoxMirror4(7, 8, 16, 1, 2, 2, "crate");
            m.PropMirror4(PropKinds.Ball, 16f, 8f, 7f);
            m.Spawn(6, 8, 6, 0).Spawn(7, 8, 6, 0);
            m.Spawn(33, 8, 33, 1).Spawn(32, 8, 33, 1);
            m.Spawn(33, 8, 6, 2).Spawn(32, 8, 6, 2);
            m.Spawn(6, 8, 33, 3).Spawn(7, 8, 33, 3);
            m.Spawn(19, 9, 19, -1).Spawn(20, 9, 20, -1).Spawn(19, 9, 20, -1).Spawn(20, 9, 19, -1);
            return m;
        }

        /// <summary>Spiked pistons slam across the floor and a crusher pounds the middle.</summary>
        public static MapData PistonPress()
        {
            var m = New("Piston Press", "Spiked pistons slam in from the walls. The middle gets pancaked regularly.", 40, 30, 40);
            m.background = "#1f1c16";
            m.Box(4, 7, 4, 32, 1, 32, "solid");
            m.Mech(MechKinds.Piston, 10, 8, 4, k => { k.dx = 0f; k.w = 3; k.h = 3; k.d = 2; k.dz = 8f; k.period = 5f; k.phase = 0f; k.deadly = true; k.damage = 35f; });
            m.Mech(MechKinds.Piston, 27, 8, 4, k => { k.dx = 0f; k.w = 3; k.h = 3; k.d = 2; k.dz = 8f; k.period = 5f; k.phase = 0.4f; k.deadly = true; k.damage = 35f; });
            m.Mech(MechKinds.Piston, 18, 8, 34, k => { k.dx = 0f; k.w = 3; k.h = 3; k.d = 2; k.dz = -8f; k.period = 5f; k.phase = 0.2f; k.deadly = true; k.damage = 35f; });
            m.Mech(MechKinds.Piston, 4, 8, 18, k => { k.w = 2; k.h = 3; k.d = 3; k.dx = 8f; k.period = 5f; k.phase = 0.6f; k.deadly = true; k.damage = 35f; });
            m.Mech(MechKinds.Piston, 34, 8, 22, k => { k.w = 2; k.h = 3; k.d = 3; k.dx = -8f; k.period = 5f; k.phase = 0.8f; k.deadly = true; k.damage = 35f; });
            m.Mech(MechKinds.Piston, 18, 14, 18, k => { k.dx = 0f; k.w = 4; k.h = 2; k.d = 4; k.dy = -5f; k.period = 6f; k.phase = 0.1f; k.deadly = true; k.damage = 60f; });
            m.BoxMirror4(14, 8, 14, 1, 3, 1, "dark");
            m.PropMirror4(PropKinds.Crate, 7f, 8f, 12.5f);
            m.Spawn(6, 8, 6, 0).Spawn(7, 8, 7, 0);
            m.Spawn(33, 8, 33, 1).Spawn(32, 8, 32, 1);
            m.Spawn(33, 8, 6, 2).Spawn(32, 8, 7, 2);
            m.Spawn(6, 8, 33, 3).Spawn(7, 8, 32, 3);
            m.Spawn(15, 8, 8, -1).Spawn(24, 8, 8, -1).Spawn(15, 8, 31, -1).Spawn(25, 8, 31, -1);
            return m;
        }

        /// <summary>Three floors joined by elevators and sliding bridges.</summary>
        public static MapData ElevatorShaft()
        {
            var m = New("Elevator Shaft", "Ride the elevators up three floors. Sliding bridges join the decks.", 36, 46, 36);
            m.background = "#151a26";
            m.Box(4, 7, 4, 28, 1, 28, "solid");
            m.BoxMirror4(4, 17, 4, 10, 1, 10, "light");
            m.Box(12, 27, 12, 12, 1, 12, "dark");
            m.Mech(MechKinds.Elevator, 15, 8, 5, k => { k.w = 3; k.d = 3; k.dy = 9f; k.period = 8f; k.phase = 0f; });
            m.Mech(MechKinds.Elevator, 18, 8, 28, k => { k.w = 3; k.d = 3; k.dy = 9f; k.period = 8f; k.phase = 0.5f; });
            m.Mech(MechKinds.Elevator, 16, 18, 25, k => { k.w = 4; k.d = 3; k.dy = 9f; k.period = 9f; k.phase = 0.25f; });
            m.Mech(MechKinds.Elevator, 16, 18, 8, k => { k.w = 4; k.d = 3; k.dy = 9f; k.period = 9f; k.phase = 0.75f; });
            m.Mech(MechKinds.Platform, 6, 17, 14, k => { k.dx = 0f; k.w = 3; k.d = 3; k.dz = 5f; k.period = 6f; });
            m.Mech(MechKinds.Platform, 27, 17, 19, k => { k.dx = 0f; k.w = 3; k.d = 3; k.dz = -5f; k.period = 6f; k.phase = 0.5f; });
            m.Box(16, 28, 16, 4, 1, 4, "glass");
            m.PropMirror4(PropKinds.Box, 11.5f, 18f, 11.5f);
            m.Prop(PropKinds.Ball, 14f, 28f, 14f).Prop(PropKinds.Barrel, 22f, 28f, 22f);
            m.Spawn(6, 8, 6, 0).Spawn(8, 8, 6, 0);
            m.Spawn(29, 8, 29, 1).Spawn(27, 8, 29, 1);
            m.Spawn(29, 8, 6, 2).Spawn(27, 8, 6, 2);
            m.Spawn(6, 8, 29, 3).Spawn(8, 8, 29, 3);
            m.Spawn(9, 18, 9, -1).Spawn(26, 18, 26, -1).Spawn(26, 18, 9, -1).Spawn(9, 18, 26, -1);
            return m;
        }

        /// <summary>Barrels, blast blocks and brick walls. Everything explodes eventually.</summary>
        public static MapData PowderKeg()
        {
            var m = New("Powder Keg", "Barrels everywhere, blast blocks in the walls. One stray shot starts a chain.", 40, 30, 40);
            m.background = "#24160f";
            m.Box(4, 7, 4, 32, 1, 32, "solid");
            m.BoxMirror4(9, 8, 12, 4, 2, 1, "brick");
            m.BoxMirror4(12, 8, 8, 1, 2, 3, "brick");
            m.BoxMirror4(16, 8, 10, 1, 1, 1, "blast");
            m.Box(18, 8, 18, 4, 3, 4, "brick");
            m.Box(19, 11, 19, 2, 1, 2, "blast");
            m.BoxMirror4(6, 8, 27, 2, 1, 2, "blast");
            m.PropMirror4(PropKinds.Barrel, 7f, 8f, 16f);
            m.PropMirror4(PropKinds.Barrel, 14.5f, 8f, 14.5f);
            m.PropMirror4(PropKinds.Barrel, 15.5f, 8f, 14.5f);
            m.PropMirror4(PropKinds.Crate, 11.5f, 8f, 20.5f);
            m.Spawn(6, 8, 6, 0).Spawn(7, 8, 6, 0);
            m.Spawn(33, 8, 33, 1).Spawn(32, 8, 33, 1);
            m.Spawn(33, 8, 6, 2).Spawn(32, 8, 6, 2);
            m.Spawn(6, 8, 33, 3).Spawn(7, 8, 33, 3);
            m.Spawn(20, 8, 8, -1).Spawn(20, 8, 31, -1).Spawn(9, 8, 20, -1).Spawn(30, 8, 20, -1);
            return m;
        }

        /// <summary>Glass walls, crystal pillars and a glass catwalk. Nothing here is safe cover for long.</summary>
        public static MapData Glasshouse()
        {
            var m = New("Glasshouse", "Glass walls and crystal pillars shatter under fire. So does the catwalk.", 40, 30, 40);
            m.background = "#102028";
            m.Box(4, 7, 4, 32, 1, 32, "light");
            m.BoxMirror4(10, 8, 14, 1, 3, 6, "glass");
            m.BoxMirror4(14, 8, 10, 6, 3, 1, "glass");
            m.BoxMirror4(15, 8, 15, 1, 4, 1, "crystal");
            m.Box(19, 8, 19, 2, 4, 2, "crystal");
            m.Box(8, 12, 19, 24, 1, 2, "glass");
            m.BoxMirrorX(8, 8, 19, 1, 4, 2, "dark");
            m.BoxMirrorX(5, 8, 19, 2, 1, 2, "bouncy");
            m.PropMirror4(PropKinds.Ball, 12f, 8f, 12f);
            m.Spawn(6, 8, 6, 0).Spawn(7, 8, 6, 0);
            m.Spawn(33, 8, 33, 1).Spawn(32, 8, 33, 1);
            m.Spawn(33, 8, 6, 2).Spawn(32, 8, 6, 2);
            m.Spawn(6, 8, 33, 3).Spawn(7, 8, 33, 3);
            m.Spawn(12, 13, 19, -1).Spawn(27, 13, 20, -1).Spawn(15, 13, 20, -1).Spawn(24, 13, 19, -1);
            return m;
        }

        /// <summary>A giant windmill turns through the middle of a meadow, sweeping everything in its way.</summary>
        public static MapData Windmill()
        {
            var m = New("Windmill", "The sails sweep through the meadow. Ride them up, or get batted away.", 44, 36, 30);
            m.background = "#18222e";
            m.Box(4, 7, 4, 36, 1, 22, "grass");
            m.Box(21, 8, 13, 2, 9, 2, "dark");
            m.Mech(MechKinds.Spinner, 10, 17, 12, k => { k.w = 24; k.h = 2; k.d = 1; k.axis = 2; k.period = 10f; k.type = "wood"; });
            m.Mech(MechKinds.Spinner, 21, 6, 12, k => { k.w = 2; k.h = 24; k.d = 1; k.axis = 2; k.period = 10f; k.type = "wood"; });
            m.BoxMirrorX(5, 11, 18, 5, 1, 5, "light");
            m.BoxMirrorX(11, 8, 20, 2, 1, 2, "bouncy");
            m.BoxMirrorX(6, 8, 13, 1, 2, 3, "wood");
            m.PropMirror4(PropKinds.Ball, 16f, 8f, 8f);
            m.PropMirror4(PropKinds.Box, 9.5f, 8f, 6.5f);
            m.Spawn(6, 8, 6, 0).Spawn(6, 8, 9, 0);
            m.Spawn(37, 8, 6, 1).Spawn(37, 8, 9, 1);
            m.Spawn(6, 8, 24, 2).Spawn(8, 8, 24, 2);
            m.Spawn(37, 8, 24, 3).Spawn(35, 8, 24, 3);
            m.Spawn(7, 12, 20, -1).Spawn(36, 12, 20, -1).Spawn(8, 12, 21, -1).Spawn(35, 12, 21, -1);
            return m;
        }

        /// <summary>Two castles across a lava moat, joined by sliding bridges.</summary>
        public static MapData MoatRun()
        {
            var m = New("Moat Run", "Cross the lava moat on the sliding bridges and storm the gate.", 48, 32, 40);
            m.background = "#2a1410";
            m.Box(16, 5, 4, 16, 1, 32, "hazard");
            m.Box(4, 6, 4, 12, 2, 32, "solid");
            m.Box(32, 6, 4, 12, 2, 32, "solid");
            m.Mech(MechKinds.Platform, 16, 7, 8, k => { k.w = 4; k.d = 4; k.dx = 12f; k.period = 8f; k.phase = 0f; });
            m.Mech(MechKinds.Platform, 28, 7, 18, k => { k.w = 4; k.d = 4; k.dx = -12f; k.period = 8f; k.phase = 0.33f; });
            m.Mech(MechKinds.Platform, 16, 7, 28, k => { k.w = 4; k.d = 4; k.dx = 12f; k.period = 8f; k.phase = 0.66f; });
            m.Box(22, 7, 14, 4, 1, 2, "solid").Box(22, 7, 24, 4, 1, 2, "solid");
            // Castle walls with openings, and a gate door each.
            m.BoxMirrorX(12, 8, 4, 1, 3, 4, "brick");
            m.BoxMirrorX(12, 8, 12, 1, 3, 6, "brick");
            m.BoxMirrorX(12, 8, 22, 1, 3, 6, "brick");
            m.BoxMirrorX(12, 8, 32, 1, 3, 4, "brick");
            m.Mech(MechKinds.Door, 12, 8, 18, k => { k.w = 1; k.h = 3; k.d = 4; k.dy = 3.2f; k.period = 7f; k.type = "wood"; });
            m.Mech(MechKinds.Door, 35, 8, 18, k => { k.w = 1; k.h = 3; k.d = 4; k.dy = 3.2f; k.period = 7f; k.phase = 0.5f; k.type = "wood"; });
            m.PropMirror4(PropKinds.Barrel, 14.5f, 8f, 15f);
            m.PropMirror4(PropKinds.Crate, 9.5f, 8f, 6.5f);
            m.Spawn(7, 8, 10, 0).Spawn(7, 8, 12, 0);
            m.Spawn(40, 8, 26, 1).Spawn(40, 8, 28, 1);
            m.Spawn(7, 8, 26, 2).Spawn(7, 8, 28, 2);
            m.Spawn(40, 8, 10, 3).Spawn(40, 8, 12, 3);
            m.Spawn(23, 8, 14, -1).Spawn(24, 8, 15, -1).Spawn(23, 8, 24, -1).Spawn(24, 8, 25, -1);
            return m;
        }

        /// <summary>Blocky trains rush across three lanes. They don't stop for spheres.</summary>
        public static MapData RushHour()
        {
            var m = New("Rush Hour", "Trains shove you toward the borders. Ride on top or wait on the islands.", 44, 30, 44);
            m.background = "#161822";
            m.Box(4, 7, 4, 36, 1, 36, "solid");
            m.Mech(MechKinds.Platform, 4, 8, 10, k => { k.w = 6; k.h = 2; k.d = 3; k.dx = 30f; k.period = 6f; k.phase = 0f; k.type = "dark"; });
            m.Mech(MechKinds.Platform, 34, 8, 20, k => { k.w = 6; k.h = 2; k.d = 3; k.dx = -30f; k.period = 6f; k.phase = 0.3f; k.type = "dark"; });
            m.Mech(MechKinds.Platform, 4, 8, 30, k => { k.w = 6; k.h = 2; k.d = 3; k.dx = 30f; k.period = 6f; k.phase = 0.6f; k.type = "dark"; });
            m.Box(18, 8, 15, 8, 1, 3, "light").Box(18, 8, 25, 8, 1, 3, "light");
            m.BoxMirrorX(8, 8, 16, 2, 2, 1, "crate");
            m.BoxMirrorX(8, 8, 26, 2, 2, 1, "crate");
            m.Prop(PropKinds.Crate, 19.5f, 9f, 16.5f).Prop(PropKinds.Crate, 24.5f, 9f, 26.5f);
            m.Spawn(6, 8, 6, 0).Spawn(8, 8, 6, 0);
            m.Spawn(35, 8, 37, 1).Spawn(37, 8, 37, 1);
            m.Spawn(35, 8, 6, 2).Spawn(37, 8, 6, 2);
            m.Spawn(6, 8, 37, 3).Spawn(8, 8, 37, 3);
            m.Spawn(20, 9, 16, -1).Spawn(23, 9, 16, -1).Spawn(20, 9, 26, -1).Spawn(23, 9, 26, -1);
            return m;
        }

        /// <summary>A rickety tower of planks and bricks with a blast block in the basement.</summary>
        public static MapData TumbleTower()
        {
            var m = New("Tumble Tower", "A wobbly tower of planks, bricks and crates. Shoot the base and watch it fall apart.", 36, 40, 36);
            m.background = "#1a1a2a";
            m.Box(10, 7, 10, 16, 1, 16, "solid");
            m.BoxMirror4(15, 8, 15, 1, 4, 1, "brick");
            m.Box(15, 12, 15, 6, 1, 6, "wood");
            m.BoxMirror4(15, 13, 15, 1, 3, 1, "brick");
            m.Box(15, 16, 15, 6, 1, 6, "wood");
            m.BoxMirror4(15, 17, 15, 1, 3, 1, "brick");
            m.Box(15, 20, 15, 6, 1, 6, "wood");
            m.Box(17, 8, 17, 2, 1, 2, "blast");
            m.BoxMirror4(13, 8, 13, 1, 1, 1, "bouncy");
            m.BoxMirror4(3, 9, 3, 5, 1, 5, "grass");
            m.PropStack(PropKinds.Crate, 18f, 13f, 18f, 2);
            m.Prop(PropKinds.Barrel, 16.5f, 17f, 16.5f).Prop(PropKinds.Ball, 18f, 21f, 18f);
            m.PropMirror4(PropKinds.Box, 12.5f, 8f, 18f);
            m.Spawn(11, 8, 11, 0).Spawn(12, 8, 11, 0);
            m.Spawn(24, 8, 24, 1).Spawn(23, 8, 24, 1);
            m.Spawn(24, 8, 11, 2).Spawn(23, 8, 11, 2);
            m.Spawn(11, 8, 24, 3).Spawn(12, 8, 24, 3);
            m.Spawn(5, 10, 5, -1).Spawn(30, 10, 30, -1).Spawn(30, 10, 5, -1).Spawn(5, 10, 30, -1);
            return m;
        }
    }
}
