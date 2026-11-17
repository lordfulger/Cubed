using System;
using System.Collections.Generic;

namespace Spheres
{
    /// <summary>
    /// Builds random, playable arenas from a seed (the "Random Arena" map). Each map picks a theme
    /// (palette, background, traps, props) and a layout (plateau + islands, two bases, towers,
    /// scattered islands, terraced bowl), mirrored or rotated so every team gets the same ground.
    /// Generation is deterministic for a seed. Every result is checked with <see cref="Validate"/>;
    /// a layout that fails is thrown away and the next seed is tried.
    /// </summary>
    /// <remarks>Pure C# with no engine calls, so it also runs in the command-line test harness
    /// (Tools/test_map_generator.py).</remarks>
    public static class MapGenerator
    {
        /// <summary>Registry id of the "Random Arena" entry.</summary>
        public const string RandomId = "random";
        public const string RandomName = "Random Arena";
        public const string RandomDescription = "A brand-new generated arena every round";
        public const string Author = "Arena Generator";
        /// <summary>Registry source of the Random Arena entry (not "Spheres", so fixed-map features like the daily challenge skip it).</summary>
        public const string Source = "Generated";
        /// <summary>How many seeds (seed, seed+1, ...) are tried before falling back to a plain arena.</summary>
        public const int MaxAttempts = 40;

        /// <summary>The last unexpected exception thrown while building (null if none). For tests.</summary>
        public static string LastBuildError { get; private set; }

        public static bool IsRandom(string mapId) => mapId == RandomId;

        /// <summary>A fresh arena for <paramref name="seed"/>. <paramref name="teamsHint"/> = how many teams will play (2-8).</summary>
        public static MapData Generate(int seed, int teamsHint = 4) => Generate(seed, teamsHint, out _);

        /// <summary>Like <see cref="Generate(int,int)"/>; <paramref name="attempts"/> = seeds tried (MaxAttempts+1 = fallback map).</summary>
        public static MapData Generate(int seed, int teamsHint, out int attempts)
        {
            teamsHint = Clamp(teamsHint, 2, 8);
            for (int i = 0; i < MaxAttempts; i++)
            {
                attempts = i + 1;
                var m = GenerateOnce(seed, i, teamsHint, out _);
                if (m != null) return m;
            }
            attempts = MaxAttempts + 1;
            return Fallback(seed);
        }

        /// <summary>
        /// One attempt (attempt 0, 1, ... = the seed, then re-rolls of it). Returns null and the reason when
        /// the layout didn't fit or failed <see cref="Validate"/>. For tests and tools.
        /// </summary>
        public static MapData GenerateOnce(int seed, int attempt, int teamsHint, out string failure)
        {
            failure = null;
            MapData m;
            try { m = new Builder(seed, attempt, Clamp(teamsHint, 2, 8)).Build(); }
            catch (RetryException e) { failure = "build: " + e.Message; return null; }
            catch (Exception e) { LastBuildError = e.ToString(); failure = "exception: " + e.Message; return null; }
            var errors = Validate(m);
            if (errors.Count == 0) return m;
            failure = "validate: " + errors[0] + (errors.Count > 1 ? " (+" + (errors.Count - 1) + " more)" : "");
            return null;
        }

        /// <summary>A representative generated arena, used as the menu backdrop and thumbnail of the Random Arena entry.</summary>
        public static MapData Sample()
        {
            var m = Generate(SampleSeed, 4);
            m.name = RandomName;
            m.description = RandomDescription;
            return m;
        }

        const int SampleSeed = 4;

        /// <summary>One line for menus: what a random arena can turn out to be.</summary>
        public const string Summary = "8 themes (sky islands, castle, factory, ice lake, lava pit, garden, crate yard, glass city) × 5 layouts, 40-56 cells wide";

        /// <summary>A plain arena that always validates (used if every attempt failed).</summary>
        public static MapData Fallback(int seed)
        {
            var m = new MapData
            {
                name = "Random: Plain Square #" + SeedNumber(seed), author = Author, description = "A simple square arena.",
                sizeX = 40, sizeY = 30, sizeZ = 40, background = "#1b2233",
            };
            m.Box(4, 6, 4, 32, 2, 32, "solid");
            m.BoxMirror4(11, 8, 11, 2, 3, 2, "light");
            m.BoxMirror4(16, 8, 7, 3, 2, 1, "crate");
            m.Spawn(6, 8, 6, 0).Spawn(8, 8, 6, 0).Spawn(33, 8, 33, 1).Spawn(31, 8, 33, 1);
            m.Spawn(33, 8, 6, 2).Spawn(31, 8, 6, 2).Spawn(6, 8, 33, 3).Spawn(8, 8, 33, 3);
            m.Spawn(20, 8, 6, 4).Spawn(33, 8, 20, 5).Spawn(20, 8, 33, 6).Spawn(6, 8, 20, 7);
            m.Spawn(15, 8, 15, -1).Spawn(24, 8, 24, -1).Spawn(24, 8, 15, -1).Spawn(15, 8, 24, -1);
            return m;
        }

        static string SeedNumber(int seed) => ((uint)seed % 10000u).ToString();

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        // =====================================================================================
        //  Validation
        // =====================================================================================

        /// <summary>An axis-aligned box in world units (cells).</summary>
        public struct Box3
        {
            public double X0, Y0, Z0, X1, Y1, Z1;
            public Box3(double x0, double y0, double z0, double x1, double y1, double z1) { X0 = x0; Y0 = y0; Z0 = z0; X1 = x1; Y1 = y1; Z1 = z1; }
            public Box3 Grow(double m) => new Box3(X0 - m, Y0 - m, Z0 - m, X1 + m, Y1 + m, Z1 + m);
            public bool Intersects(Box3 o) => X0 < o.X1 && o.X0 < X1 && Y0 < o.Y1 && o.Y0 < Y1 && Z0 < o.Z1 && o.Z0 < Z1;
            public bool Contains(double x, double y, double z) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1 && z >= Z0 && z <= Z1;
            public override string ToString() => string.Format("[{0:0.#},{1:0.#},{2:0.#} - {3:0.#},{4:0.#},{5:0.#}]", X0, Y0, Z0, X1, Y1, Z1);
        }

        /// <summary>Everywhere a moving part can reach (same rules as Tools/validate_maps.py).</summary>
        public static Box3 Swept(MechData m)
        {
            int w = Math.Max(1, m.w), h = Math.Max(1, m.h), d = Math.Max(1, m.d);
            switch (m.kind)
            {
                case MechKinds.Pendulum:
                {
                    double reach = m.length * Math.Sin(Math.Min(90.0, m.angle) * Math.PI / 180.0) + w;
                    double r = m.length + w;
                    double cx = m.x + 0.5, cy = m.y + 0.5, cz = m.z + 0.5;
                    var b = new Box3(cx - reach, cy - r, cz - reach, cx + reach, cy + 0.5, cz + reach);
                    if (m.axis == 0) { b.X0 = cx - w; b.X1 = cx + w; }
                    else if (m.axis == 1) { b.Y0 = cy - w; b.Y1 = cy + w; }
                    else { b.Z0 = cz - w; b.Z1 = cz + w; }
                    return b;
                }
                case MechKinds.Spinner:
                {
                    double r = Math.Max(w, Math.Max(h, d)) * 0.5;
                    double cx = m.x + w * 0.5, cy = m.y + h * 0.5, cz = m.z + d * 0.5;
                    var b = new Box3(cx - r, cy - r, cz - r, cx + r, cy + r, cz + r);
                    if (m.axis == 0) { b.X0 = cx - w * 0.5; b.X1 = cx + w * 0.5; }
                    else if (m.axis == 1) { b.Y0 = cy - h * 0.5; b.Y1 = cy + h * 0.5; }
                    else { b.Z0 = cz - d * 0.5; b.Z1 = cz + d * 0.5; }
                    return b;
                }
                case MechKinds.Spikes:
                    return new Box3(m.x, m.y, m.z, m.x + w, m.y + 1, m.z + d);
                default:
                {
                    var b = new Box3(m.x, m.y, m.z, m.x + w, m.y + h, m.z + d);
                    if (m.dx < 0) b.X0 += m.dx; else b.X1 += m.dx;
                    if (m.dy < 0) b.Y0 += m.dy; else b.Y1 += m.dy;
                    if (m.dz < 0) b.Z0 += m.dz; else b.Z1 += m.dz;
                    return b;
                }
            }
        }

        /// <summary>The part of <see cref="Swept"/> that must be free of blocks (a pendulum's head may skim the floor under it).</summary>
        static Box3 MustBeClear(MechData m)
        {
            var b = Swept(m);
            if (m.kind == MechKinds.Pendulum) b.Y0 = m.y + 0.5 - m.length - Math.Max(1, m.w) * 0.5 + 0.4;
            return b;
        }

        /// <summary>
        /// Checks a map the way Tools/validate_maps.py checks the built-in maps, plus a little more:
        /// everything inside the arena, spawns on solid safe ground with headroom, away from walls and
        /// moving parts, props resting on something, moving parts not passing through blocks or each
        /// other, teams 0-3 and 4 neutral spawns present, and every spawn reachable from every other
        /// (walking, or jumps of up to 2 cells up and about 5 across). Returns the errors (empty = OK).
        /// </summary>
        public static List<string> Validate(MapData m)
        {
            var errors = new List<string>();
            if (m == null) { errors.Add("map is null"); return errors; }
            int sx = m.sizeX, sy = m.sizeY, sz = m.sizeZ;
            if (sx < 8 || sy < 8 || sz < 8 || sx > 256 || sy > 128 || sz > 256)
            {
                errors.Add("bad arena size " + sx + "x" + sy + "x" + sz);
                return errors;
            }
            foreach (var b in m.blocks)
            {
                if (!BlockTypes.Exists(b.type)) errors.Add("unknown block type '" + b.type + "' " + Describe(b));
                if (b.x < 0 || b.y < 0 || b.z < 0 || b.x + b.W > sx || b.y + b.H > sy || b.z + b.D > sz)
                    errors.Add("block outside arena " + Describe(b));
            }
            var g = new Grid(m);

            // Moving parts.
            var swept = new List<Box3>();
            for (int i = 0; i < m.mechs.Count; i++)
            {
                var k = m.mechs[i];
                string tag = "moving part " + k.kind + "(" + k.x + "," + k.y + "," + k.z + ")";
                if (Array.IndexOf(MechKinds.All, k.kind) < 0) errors.Add("unknown kind " + tag);
                if (k.x < 0 || k.y < 0 || k.z < 0 || k.x >= sx || k.y >= sy || k.z >= sz) errors.Add(tag + " anchored outside arena");
                var s = Swept(k);
                swept.Add(s);
                if (s.X0 < -0.01 || s.Y0 < -0.01 || s.Z0 < -0.01 || s.X1 > sx + 0.01 || s.Y1 > sy + 0.01 || s.Z1 > sz + 0.01)
                    errors.Add(tag + " reaches outside the arena " + s);
                var hit = g.FirstBlockIn(MustBeClear(k));
                if (hit != null) errors.Add(tag + " moves through " + Describe(hit));
                if (k.kind == MechKinds.Spikes)
                    for (int x = k.x; x < k.x + Math.Max(1, k.w); x++)
                        for (int z = k.z; z < k.z + Math.Max(1, k.d); z++)
                            if (g.At(x, k.y - 1, z) == null) { errors.Add(tag + " has no floor at " + x + "," + z); goto nextMech; }
                nextMech:;
            }
            for (int i = 0; i < swept.Count; i++)
                for (int j = i + 1; j < swept.Count; j++)
                    if (swept[i].Intersects(swept[j]))
                        errors.Add("moving parts " + m.mechs[i].kind + "(" + m.mechs[i].x + "," + m.mechs[i].y + "," + m.mechs[i].z + ") and "
                            + m.mechs[j].kind + "(" + m.mechs[j].x + "," + m.mechs[j].y + "," + m.mechs[j].z + ") collide");

            // Props.
            foreach (var p in m.props)
            {
                string tag = string.Format("prop {0}({1:0.##},{2:0.##},{3:0.##})", p.kind, p.x, p.y, p.z);
                if (!(p.x >= 0 && p.x < sx && p.y >= 0 && p.y < sy && p.z >= 0 && p.z < sz)) { errors.Add(tag + " outside arena"); continue; }
                var size = PropKinds.Get(p.kind).Size;
                double hx = size.x * 0.45, hz = size.z * 0.45;
                int cy = (int)Math.Floor(p.y + 0.05);
                BlockData inside = g.At((int)Math.Floor(p.x - hx), cy, (int)Math.Floor(p.z - hz)) ?? g.At((int)Math.Floor(p.x + hx), cy, (int)Math.Floor(p.z - hz))
                    ?? g.At((int)Math.Floor(p.x - hx), cy, (int)Math.Floor(p.z + hz)) ?? g.At((int)Math.Floor(p.x + hx), cy, (int)Math.Floor(p.z + hz));
                if (inside != null) { errors.Add(tag + " is inside " + Describe(inside)); continue; }
                bool supported = g.At((int)Math.Floor(p.x), (int)Math.Round(p.y) - 1, (int)Math.Floor(p.z)) != null && Math.Abs(p.y - Math.Round(p.y)) < 0.01;
                if (!supported)
                    foreach (var q in m.props)
                    {
                        if (q == p) continue;
                        var qs = PropKinds.Get(q.kind).Size;
                        if (Math.Abs(q.x - p.x) < 0.6 && Math.Abs(q.z - p.z) < 0.6 && Math.Abs(q.y + qs.y - p.y) < 0.06) { supported = true; break; }
                    }
                if (!supported) errors.Add(tag + " is not resting on anything");
                for (int i = 0; i < swept.Count; i++)
                    if (swept[i].Grow(0.3).Contains(p.x, p.y + 0.3, p.z)) { errors.Add(tag + " is in the path of " + m.mechs[i].kind); break; }
            }

            // Spawns.
            var teamCount = new int[Teams_Max];
            int neutral = 0;
            var seen = new HashSet<long>();
            foreach (var s in m.spawns)
            {
                string tag = "spawn(" + s.x + "," + s.y + "," + s.z + " team " + s.team + ")";
                if (s.team >= 0 && s.team < Teams_Max) teamCount[s.team]++; else neutral++;
                if (!seen.Add(Key(s.x, s.y, s.z))) errors.Add("duplicate " + tag);
                if (!(s.x >= 0 && s.x < sx && s.y >= 1 && s.y + 1 < sy && s.z >= 0 && s.z < sz)) { errors.Add(tag + " outside arena"); continue; }
                var below = g.At(s.x, s.y - 1, s.z);
                if (below == null) errors.Add(tag + " is floating");
                else
                {
                    var t = BlockTypes.Get(below.type);
                    if (t.DamagePerSecond > 0f) errors.Add(tag + " stands on hazard " + Describe(below));
                    else if (t.BounceSpeed > 0f) errors.Add(tag + " stands on bouncy " + Describe(below));
                    else if (t.Destructible) errors.Add(tag + " stands on breakable " + Describe(below));
                }
                for (int dy = 0; dy < 2; dy++)
                {
                    var occ = g.At(s.x, s.y + dy, s.z);
                    if (occ != null) errors.Add(tag + " blocked at y=" + (s.y + dy) + " by " + Describe(occ));
                }
                for (int i = 0; i < swept.Count; i++)
                {
                    double margin = m.mechs[i].deadly ? 1.5 : 0.5;
                    if (swept[i].Grow(margin).Contains(s.x + 0.5, s.y + 0.5, s.z + 0.5)) errors.Add(tag + " is in the path of moving part " + m.mechs[i].kind);
                }
                foreach (var p in m.props)
                    if (Math.Abs(p.x - (s.x + 0.5)) < 1.2 && Math.Abs(p.z - (s.z + 0.5)) < 1.2 && Math.Abs(p.y - s.y) < 1.5) { errors.Add(tag + " has a prop on it"); break; }
                if (Math.Min(Math.Min(s.x, s.z), Math.Min(sx - 1 - s.x, sz - 1 - s.z)) < 2) errors.Add(tag + " within 2 cells of a side wall");
            }
            for (int t = 0; t < 4; t++)
                if (teamCount[t] < 1) errors.Add("team " + t + " has no spawn");
            if (neutral < 4) errors.Add("only " + neutral + " neutral spawns (need 4)");

            if (errors.Count == 0 && m.spawns.Count > 1)
            {
                var unreachable = Unreachable(g, m.spawns);
                foreach (var s in unreachable) errors.Add("spawn(" + s.x + "," + s.y + "," + s.z + " team " + s.team + ") can't be reached from / can't reach the others");
            }
            return errors;
        }

        const int Teams_Max = 8;

        static long Key(int x, int y, int z) => ((long)x << 40) ^ ((long)y << 20) ^ (uint)z;

        static string Describe(BlockData b) => b.type + "(" + b.x + "," + b.y + "," + b.z + " " + b.W + "x" + b.H + "x" + b.D + ")";

        /// <summary>A voxel grid of the arena: which block (if any) fills each cell. Later blocks win, like MapData.BlockAt.</summary>
        sealed class Grid
        {
            public readonly int Sx, Sy, Sz;
            readonly List<BlockData> blocks;
            readonly short[] cells;

            public Grid(MapData m)
            {
                Sx = m.sizeX; Sy = m.sizeY; Sz = m.sizeZ;
                blocks = m.blocks;
                cells = new short[Sx * Sy * Sz];
                for (int i = 0; i < blocks.Count && i < short.MaxValue - 1; i++) Fill(i);
            }

            public void Fill(int index)
            {
                var b = blocks[index];
                int x0 = Math.Max(0, b.x), y0 = Math.Max(0, b.y), z0 = Math.Max(0, b.z);
                int x1 = Math.Min(Sx, b.x + b.W), y1 = Math.Min(Sy, b.y + b.H), z1 = Math.Min(Sz, b.z + b.D);
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                    {
                        int row = (y * Sz + z) * Sx;
                        for (int x = x0; x < x1; x++) cells[row + x] = (short)(index + 1);
                    }
            }

            public bool In(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < Sx && y < Sy && z < Sz;
            public int Index(int x, int y, int z) => (y * Sz + z) * Sx + x;

            public BlockData At(int x, int y, int z)
            {
                if (!In(x, y, z)) return null;
                int c = cells[Index(x, y, z)];
                return c == 0 ? null : blocks[c - 1];
            }

            /// <summary>Empty and inside the arena.</summary>
            public bool Empty(int x, int y, int z) => In(x, y, z) && cells[Index(x, y, z)] == 0;

            public BlockData FirstBlockIn(Box3 b)
            {
                const double e = 1e-4;
                int x0 = (int)Math.Floor(b.X0 + e), y0 = (int)Math.Floor(b.Y0 + e), z0 = (int)Math.Floor(b.Z0 + e);
                int x1 = (int)Math.Ceiling(b.X1 - e), y1 = (int)Math.Ceiling(b.Y1 - e), z1 = (int)Math.Ceiling(b.Z1 - e);
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                        for (int x = x0; x < x1; x++)
                        {
                            var hit = At(x, y, z);
                            if (hit != null) return hit;
                        }
                return null;
            }

            /// <summary>A player can stand in this cell: safe block under it and two empty cells.</summary>
            public bool Standable(int x, int y, int z)
            {
                if (x < 0 || z < 0 || x >= Sx || z >= Sz || y < 1 || y + 1 >= Sy) return false;
                var b = At(x, y - 1, z);
                if (b == null || BlockTypes.Get(b.type).DamagePerSecond > 0f) return false;
                return Empty(x, y, z) && Empty(x, y + 1, z);
            }

            public bool Bouncy(int x, int y, int z)
            {
                var b = At(x, y - 1, z);
                return b != null && BlockTypes.Get(b.type).BounceSpeed > 0f;
            }
        }

        // ------------------------------------------------------------------ reachability

        const int JumpReach = 6;

        /// <summary>Can a player standing in cell a get to cell b in one walk / jump / drop / bounce?</summary>
        static bool CanMove(Grid g, int ax, int ay, int az, int bx, int by, int bz)
        {
            int dx = bx - ax, dz = bz - az, dy = by - ay;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            bool ok;
            if (dy <= 0) ok = dist <= 5.5;
            else if (dy == 1) ok = dist <= 5.0;
            else if (dy == 2) ok = dist <= 4.0;
            else ok = false;
            if (!ok && g.Bouncy(ax, ay, az)) ok = dy <= 10 && dist <= 6.0;
            if (!ok) return false;
            int top = Math.Max(ay, by);
            for (int y = ay; y <= top + 1; y++) if (!g.Empty(ax, y, az)) return false;
            for (int y = by; y <= top + 1; y++) if (!g.Empty(bx, y, bz)) return false;
            if (dist > 1.01)
            {
                int n = (int)Math.Ceiling(dist * 2.0);
                for (int i = 1; i < n; i++)
                {
                    double t = i / (double)n;
                    int cx = (int)Math.Floor(ax + 0.5 + dx * t), cz = (int)Math.Floor(az + 0.5 + dz * t);
                    if ((cx == ax && cz == az) || (cx == bx && cz == bz)) continue;
                    if (!g.Empty(cx, top, cz) || !g.Empty(cx, top + 1, cz)) return false;
                }
            }
            return true;
        }

        /// <summary>Spawns that can't reach spawn 0 or can't be reached from it.</summary>
        static List<SpawnData> Unreachable(Grid g, List<SpawnData> spawns)
        {
            int sx = g.Sx, sy = g.Sy, sz = g.Sz;
            // Standable cells per column.
            var cols = new List<int>[sx * sz];
            for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    for (int y = 1; y + 1 < sy; y++)
                        if (g.Standable(x, y, z)) (cols[z * sx + x] ?? (cols[z * sx + x] = new List<int>())).Add(y);

            var result = new List<SpawnData>();
            var fwd = Flood(g, cols, spawns[0], false, spawns);
            var back = Flood(g, cols, spawns[0], true, spawns);
            foreach (var s in spawns)
            {
                int i = g.Index(s.x, s.y, s.z);
                if (!fwd[i] || !back[i]) result.Add(s);
            }
            return result;
        }

        static bool[] Flood(Grid g, List<int>[] cols, SpawnData from, bool reverse, List<SpawnData> targets)
        {
            var seen = new bool[g.Sx * g.Sy * g.Sz];
            var queue = new Queue<int>();
            int start = g.Index(from.x, from.y, from.z);
            seen[start] = true;
            queue.Enqueue(start);
            int remaining = 0;
            var targetSet = new HashSet<int>();
            foreach (var t in targets) if (g.In(t.x, t.y, t.z) && targetSet.Add(g.Index(t.x, t.y, t.z)) && !seen[g.Index(t.x, t.y, t.z)]) remaining++;
            while (queue.Count > 0 && remaining > 0)
            {
                int c = queue.Dequeue();
                int x = c % g.Sx, z = (c / g.Sx) % g.Sz, y = c / (g.Sx * g.Sz);
                for (int dx = -JumpReach; dx <= JumpReach; dx++)
                    for (int dz = -JumpReach; dz <= JumpReach; dz++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= g.Sx || nz >= g.Sz) continue;
                        var col = cols[nz * g.Sx + nx];
                        if (col == null) continue;
                        foreach (int ny in col)
                        {
                            int ni = g.Index(nx, ny, nz);
                            if (seen[ni]) continue;
                            bool can = reverse ? CanMove(g, nx, ny, nz, x, y, z) : CanMove(g, x, y, z, nx, ny, nz);
                            if (!can) continue;
                            seen[ni] = true;
                            if (targetSet.Contains(ni)) remaining--;
                            queue.Enqueue(ni);
                        }
                    }
            }
            return seen;
        }

        // =====================================================================================
        //  Generation
        // =====================================================================================

        sealed class RetryException : Exception
        {
            public RetryException(string message) : base(message) { }
        }

        /// <summary>Small deterministic RNG (SplitMix64), identical on every runtime.</summary>
        sealed class Rng
        {
            ulong state;
            public Rng(int seed, int stream) { state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ 0x5DEECE66DUL ^ ((ulong)(uint)stream << 32) * 0xD1B54A32D192ED03UL; }
            ulong Next()
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
            /// <summary>Integer in [lo, hi). Returns lo when the range is empty.</summary>
            public int Range(int lo, int hi) => hi <= lo ? lo : lo + (int)(Next() % (ulong)(hi - lo));
            public float Value() => (Next() >> 40) / (float)(1UL << 24);
            public float Range(float lo, float hi) => lo + (hi - lo) * Value();
            public bool Chance(float p) => Value() < p;
            public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];
            public void Shuffle<T>(IList<T> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = Range(0, i + 1);
                    var t = list[i]; list[i] = list[j]; list[j] = t;
                }
            }
        }

        sealed class Theme
        {
            public string Title, Background, Surface, Under, Accent, Pillar, Wall, Breakable, Pane, Pool;
            public string[] Bridges, Adjectives, Traps, Props;
            public float PropDensity = 1f, Bouncers = 0.3f, Stones = 0.2f;
            public bool LavaSea, Crenels;
        }

        static readonly Theme[] Themes =
        {
            new Theme { Title = "sky-island", Background = "#22365a", Surface = "grass", Under = "solid", Accent = "light", Pillar = "light", Wall = "light", Breakable = "crate", Pane = "glass", Pool = "bouncy",
                Bridges = new[] { "wood", "light" }, Adjectives = new[] { "Breezy", "Cloudy", "Drifting", "Floating", "Lofty", "Windswept" },
                Traps = new[] { MechKinds.Platform, MechKinds.Pendulum, MechKinds.Spinner, MechKinds.Elevator }, Props = new[] { PropKinds.Ball, PropKinds.Box, PropKinds.Crate },
                PropDensity = 0.8f, Bouncers = 0.8f, Stones = 0.35f },
            new Theme { Title = "castle", Background = "#1b1622", Surface = "solid", Under = "dark", Accent = "light", Pillar = "brick", Wall = "brick", Breakable = "wood", Pane = "glass", Pool = "hazard",
                Bridges = new[] { "wood", "solid" }, Adjectives = new[] { "Royal", "Ancient", "Crumbling", "Haunted", "Regal", "Forgotten" },
                Traps = new[] { MechKinds.Pendulum, MechKinds.Spikes, MechKinds.Spinner }, Props = new[] { PropKinds.Barrel, PropKinds.Crate },
                PropDensity = 0.8f, Bouncers = 0.2f, Stones = 0.1f, Crenels = true },
            new Theme { Title = "factory", Background = "#16181c", Surface = "dark", Under = "solid", Accent = "light", Pillar = "dark", Wall = "solid", Breakable = "blast", Pane = "glass", Pool = "hazard",
                Bridges = new[] { "solid", "light" }, Adjectives = new[] { "Rusty", "Grinding", "Clanking", "Smoky", "Oily", "Humming" },
                Traps = new[] { MechKinds.Saw, MechKinds.Platform, MechKinds.Spinner, MechKinds.Spikes, MechKinds.Elevator }, Props = new[] { PropKinds.Barrel, PropKinds.Heavy, PropKinds.Box },
                PropDensity = 1.1f, Bouncers = 0.25f, Stones = 0.1f },
            new Theme { Title = "ice-lake", Background = "#0f1d2c", Surface = "ice", Under = "light", Accent = "light", Pillar = "light", Wall = "glass", Breakable = "crystal", Pane = "glass", Pool = "ice",
                Bridges = new[] { "ice", "light" }, Adjectives = new[] { "Frosty", "Frozen", "Glacial", "Shivering", "Polar", "Icy" },
                Traps = new[] { MechKinds.Spinner, MechKinds.Platform, MechKinds.Pendulum }, Props = new[] { PropKinds.Ball, PropKinds.Box },
                PropDensity = 0.7f, Bouncers = 0.35f, Stones = 0.3f },
            new Theme { Title = "lava-pit", Background = "#241109", Surface = "dark", Under = "solid", Accent = "solid", Pillar = "dark", Wall = "dark", Breakable = "blast", Pane = "crystal", Pool = "hazard",
                Bridges = new[] { "dark", "solid" }, Adjectives = new[] { "Molten", "Scorched", "Blazing", "Smoldering", "Volcanic", "Fiery" },
                Traps = new[] { MechKinds.Saw, MechKinds.Pendulum, MechKinds.Spikes }, Props = new[] { PropKinds.Barrel, PropKinds.Heavy },
                PropDensity = 0.6f, Bouncers = 0.3f, Stones = 0.35f, LavaSea = true },
            new Theme { Title = "garden", Background = "#13201a", Surface = "grass", Under = "solid", Accent = "light", Pillar = "light", Wall = "wood", Breakable = "crate", Pane = "glass", Pool = "bouncy",
                Bridges = new[] { "wood", "grass" }, Adjectives = new[] { "Mossy", "Blooming", "Verdant", "Leafy", "Sunny", "Overgrown" },
                Traps = new[] { MechKinds.Spikes, MechKinds.Platform, MechKinds.Pendulum }, Props = new[] { PropKinds.Ball, PropKinds.Box, PropKinds.Crate },
                PropDensity = 0.9f, Bouncers = 0.5f, Stones = 0.3f },
            new Theme { Title = "crate-yard", Background = "#1a1d22", Surface = "solid", Under = "dark", Accent = "light", Pillar = "dark", Wall = "crate", Breakable = "crate", Pane = "wood", Pool = null,
                Bridges = new[] { "wood", "solid" }, Adjectives = new[] { "Cluttered", "Stacked", "Dusty", "Busy", "Loaded", "Shipping" },
                Traps = new[] { MechKinds.Platform, MechKinds.Saw, MechKinds.Elevator }, Props = new[] { PropKinds.Crate, PropKinds.Box, PropKinds.Barrel, PropKinds.Heavy },
                PropDensity = 1.8f, Bouncers = 0.2f, Stones = 0.15f },
            new Theme { Title = "glass-city", Background = "#0e1626", Surface = "light", Under = "solid", Accent = "dark", Pillar = "glass", Wall = "glass", Breakable = "glass", Pane = "crystal", Pool = "bouncy",
                Bridges = new[] { "glass", "light" }, Adjectives = new[] { "Shining", "Neon", "Gleaming", "Mirrored", "Shimmering", "Crystal" },
                Traps = new[] { MechKinds.Platform, MechKinds.Spinner, MechKinds.Elevator }, Props = new[] { PropKinds.Ball },
                PropDensity = 0.6f, Bouncers = 0.6f, Stones = 0.2f },
        };

        enum Layout { Plateau, Bases, Towers, Islands, Bowl }

        static readonly string[][] LayoutNouns =
        {
            new[] { "Crossroads", "Summit", "Crown", "Hub", "Plateau", "Citadel" },
            new[] { "Standoff", "Divide", "Frontier", "Gulf", "Crossing", "Trenches" },
            new[] { "Spires", "Towers", "Heights", "Stacks", "Turrets", "Belfries" },
            new[] { "Archipelago", "Isles", "Reefs", "Shards", "Scatter", "Atolls" },
            new[] { "Bowl", "Colosseum", "Crater", "Basin", "Pit", "Amphitheater" },
        };

        static readonly string[] LayoutText =
        {
            "a central plateau ringed by islands",
            "two bases facing off across bridges",
            "towers and stairs over an open floor",
            "scattered islands linked by bridges",
            "a terraced bowl",
        };

        /// <summary>Mirror4: mirrored in X and Z (4 teams). Rot4: 90° turns (4 teams). Rot2: half turn. MirrorX: mirrored in X.</summary>
        enum Sym { Mirror4, Rot4, Rot2, MirrorX }

        struct Rect
        {
            public int X, Z, W, D;
            public Rect(int x, int z, int w, int d) { X = x; Z = z; W = w; D = d; }
            public bool Overlaps(Rect o) => X < o.X + o.W && o.X < X + W && Z < o.Z + o.D && o.Z < Z + D;
            public bool Same(Rect o) => X == o.X && Z == o.Z && W == o.W && D == o.D;
        }

        /// <summary>A flat walkable area: island, plateau, base, floor or terrace ring.</summary>
        sealed class Pad
        {
            public int X, Z, W, D, Top, Thick = 2;
            /// <summary>Centered hole (side length) in the top; filled with the theme's pool block one cell lower.</summary>
            public int Hole;
            public bool Rocky;
            /// <summary>Terrace ring width (0 = a solid pad).</summary>
            public int Ring;
            public string Role = "";
            public Pad(int x, int z, int w, int d, int top, int thick, string role) { X = x; Z = z; W = w; D = d; Top = top; Thick = thick; Role = role; }
            public int Area => W * D;
            public double Cx => X + W * 0.5;
            public double Cz => Z + D * 0.5;
        }

        /// <summary>A straight stretch of bridge with a constant height (where pendulums and spikes can go).</summary>
        sealed class Lane
        {
            public int X, Z, Len, Width, Axis, Top;
        }

        struct PathCell
        {
            public Rect R;
            public int Top;
            public int Axis;   // 0 = runs along X, 2 = along Z, 1 = corner
        }

        sealed class Builder
        {
            readonly Rng r;
            readonly int seed, teams;
            MapData m;
            Theme th;
            Layout layout;
            Sym sym;
            int sx, sy, sz, baseTop;
            float chaos;
            Grid g;
            bool[] keepClear, noMech;
            readonly HashSet<string> boxKeys = new HashSet<string>();
            readonly List<Pad> pads = new List<Pad>();
            readonly List<Lane> lanes = new List<Lane>();
            readonly List<Box3> mechBoxes = new List<Box3>();
            Pad centerPad;

            public Builder(int seed, int attempt, int teams)
            {
                this.seed = seed;
                this.teams = teams;
                r = new Rng(seed, attempt);
            }

            int Images => sym == Sym.Rot2 || sym == Sym.MirrorX ? 2 : 4;

            public MapData Build()
            {
                th = Themes[r.Range(0, Themes.Length)];
                chaos = r.Value();
                baseTop = r.Range(8, 11);
                int[] weights = { 3, teams <= 2 ? 5 : 2, 2, 3, 2 };
                int total = 0;
                foreach (int w in weights) total += w;
                int roll = r.Range(0, total);
                layout = Layout.Plateau;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (roll < weights[i]) { layout = (Layout)i; break; }
                    roll -= weights[i];
                }

                switch (layout)
                {
                    case Layout.Plateau: LayoutPlateau(); break;
                    case Layout.Bases: LayoutBases(); break;
                    case Layout.Towers: LayoutTowers(); break;
                    case Layout.Islands: LayoutIslands(); break;
                    default: LayoutBowl(); break;
                }

                PlaceMechs();
                PlaceCover();
                PlaceBouncers();
                PlaceProps();

                string adj = r.Pick(th.Adjectives), noun = r.Pick(LayoutNouns[(int)layout]);
                m.name = "Random: " + adj + " " + noun + " #" + SeedNumber(seed);
                m.author = Author;
                m.description = "Generated " + th.Title + " arena: " + LayoutText[(int)layout] + ".";
                m.background = th.Background;
                return m;
            }

            void Init()
            {
                m = new MapData { sizeX = sx, sizeY = sy, sizeZ = sz };
                g = new Grid(m);
                keepClear = new bool[sx * sy * sz];
                noMech = new bool[sx * sy * sz];
                if (th.LavaSea) Raw(0, 1, 0, sx, 1, sz, "hazard");
            }

            // ------------------------------------------------------------------ symmetry

            void MapPoint(int k, double x, double z, out double ox, out double oz)
            {
                ox = x; oz = z;
                switch (sym)
                {
                    case Sym.Mirror4:
                        if ((k & 1) != 0) ox = sx - x;
                        if ((k & 2) != 0) oz = sz - z;
                        break;
                    case Sym.MirrorX:
                        if (k == 1) ox = sx - x;
                        break;
                    case Sym.Rot2:
                        if (k == 1) { ox = sx - x; oz = sz - z; }
                        break;
                    default:
                        for (int i = 0; i < k; i++) { double t = ox; ox = sx - oz; oz = t; }
                        break;
                }
            }

            Rect MapRect(int k, Rect a)
            {
                MapPoint(k, a.X, a.Z, out double x0, out double z0);
                MapPoint(k, a.X + a.W, a.Z + a.D, out double x1, out double z1);
                int nx = (int)Math.Round(Math.Min(x0, x1)), nz = (int)Math.Round(Math.Min(z0, z1));
                return new Rect(nx, nz, (int)Math.Round(Math.Abs(x1 - x0)), (int)Math.Round(Math.Abs(z1 - z0)));
            }

            int MapAxis(int k, int axis) => sym == Sym.Rot4 && (k & 1) != 0 && axis != 1 ? 2 - axis : axis;

            static readonly int[] TeamsMirror4 = { 0, 2, 3, 1 };
            static readonly int[] TeamsRot4 = { 0, 2, 1, 3 };

            int TeamFor(int set, int k)
            {
                if (set < 0) return -1;
                int baseTeam = Images == 2 ? k : sym == Sym.Mirror4 ? TeamsMirror4[k] : TeamsRot4[k];
                return baseTeam + set * Images;
            }

            /// <summary>Images of a rect under the map's symmetry, without duplicates. Null if two images partly overlap.</summary>
            List<Rect> Images_(Rect a)
            {
                var list = new List<Rect>(4);
                for (int k = 0; k < Images; k++)
                {
                    var im = MapRect(k, a);
                    bool dup = false;
                    foreach (var o in list)
                    {
                        if (o.Same(im)) { dup = true; break; }
                        if (o.Overlaps(im)) return null;
                    }
                    if (!dup) list.Add(im);
                }
                return list;
            }

            /// <summary>Is the box free (no blocks, not reserved) in every image?</summary>
            bool Free(int x, int y, int z, int w, int h, int d, bool checkKeep = true, bool checkMech = true)
            {
                if (w <= 0 || h <= 0 || d <= 0 || y < 0 || y + h > sy) return false;
                var ims = Images_(new Rect(x, z, w, d));
                if (ims == null) return false;
                foreach (var im in ims)
                {
                    if (im.X < 0 || im.Z < 0 || im.X + im.W > sx || im.Z + im.D > sz) return false;
                    for (int yy = y; yy < y + h; yy++)
                        for (int zz = im.Z; zz < im.Z + im.D; zz++)
                            for (int xx = im.X; xx < im.X + im.W; xx++)
                            {
                                int i = g.Index(xx, yy, zz);
                                if (!g.Empty(xx, yy, zz)) return false;
                                if (checkKeep && keepClear[i]) return false;
                                if (checkMech && noMech[i]) return false;
                            }
                }
                return true;
            }

            void Reserve(int x, int y, int z, int w, int h, int d, bool keep, bool mech)
            {
                for (int k = 0; k < Images; k++)
                {
                    var im = MapRect(k, new Rect(x, z, w, d));
                    for (int yy = Math.Max(0, y); yy < Math.Min(sy, y + h); yy++)
                        for (int zz = Math.Max(0, im.Z); zz < Math.Min(sz, im.Z + im.D); zz++)
                            for (int xx = Math.Max(0, im.X); xx < Math.Min(sx, im.X + im.W); xx++)
                            {
                                int i = g.Index(xx, yy, zz);
                                if (keep) keepClear[i] = true;
                                if (mech) noMech[i] = true;
                            }
                }
            }

            void ReserveRaw(Box3 b, bool keep, bool mech)
            {
                int x0 = Math.Max(0, (int)Math.Floor(b.X0)), y0 = Math.Max(0, (int)Math.Floor(b.Y0)), z0 = Math.Max(0, (int)Math.Floor(b.Z0));
                int x1 = Math.Min(sx, (int)Math.Ceiling(b.X1)), y1 = Math.Min(sy, (int)Math.Ceiling(b.Y1)), z1 = Math.Min(sz, (int)Math.Ceiling(b.Z1));
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                        for (int x = x0; x < x1; x++)
                        {
                            int i = g.Index(x, y, z);
                            if (keep) keepClear[i] = true;
                            if (mech) noMech[i] = true;
                        }
            }

            /// <summary>Adds a block and its symmetric copies.</summary>
            void Box(int x, int y, int z, int w, int h, int d, string type)
            {
                if (w <= 0 || h <= 0 || d <= 0) return;
                for (int k = 0; k < Images; k++)
                {
                    var im = MapRect(k, new Rect(x, z, w, d));
                    Raw(im.X, y, im.Z, im.W, h, im.D, type);
                }
            }

            void Raw(int x, int y, int z, int w, int h, int d, string type)
            {
                if (w <= 0 || h <= 0 || d <= 0) return;
                if (!boxKeys.Add(x + "," + y + "," + z + "," + w + "," + h + "," + d)) return;
                m.blocks.Add(new BlockData(x, y, z, w, h, d, type));
                g.Fill(m.blocks.Count - 1);
            }

            Pad ImagePad(Pad p, int k)
            {
                var im = MapRect(k, new Rect(p.X, p.Z, p.W, p.D));
                return new Pad(im.X, im.Z, im.W, im.D, p.Top, p.Thick, p.Role);
            }

            static int Centered(int size, ref int w)
            {
                if (((size - w) & 1) != 0) w--;
                return (size - w) / 2;
            }

            // ------------------------------------------------------------------ pads

            bool TryPad(Pad p)
            {
                int depth = p.Thick + (p.Rocky ? 2 : 0);
                if (p.W < 3 || p.D < 3 || p.Top - depth < 2 || p.Top + 3 > sy) return false;
                if (!Free(p.X - 1, p.Top - depth, p.Z - 1, p.W + 2, depth + 3, p.D + 2, false, false)) return false;
                EmitPad(p);
                pads.Add(p);
                return true;
            }

            void PadOrRetry(Pad p)
            {
                if (!TryPad(p)) throw new RetryException("pad " + p.Role + " does not fit");
            }

            void EmitPad(Pad p)
            {
                int t = p.Thick - 1;
                if (p.Hole > 0)
                {
                    int hw = p.Hole;
                    int hx = p.X + (p.W - hw) / 2, hz = p.Z + (p.D - hw) / 2;
                    int ring = Math.Min(hx - p.X, hz - p.Z);
                    Frame(p.X, p.Z, p.W, p.D, ring, p.Top - 1, 1, th.Surface);
                    if (th.Pool != null)
                    {
                        Frame(p.X, p.Z, p.W, p.D, ring, p.Top - 2, 1, th.Under);
                        Box(hx, p.Top - 2, hz, hw, 1, hw, th.Pool);
                        if (t >= 2) Box(p.X + 1, p.Top - 1 - t, p.Z + 1, p.W - 2, t - 1, p.D - 2, th.Under);
                    }
                    else if (t >= 1) Frame(p.X, p.Z, p.W, p.D, ring, p.Top - 1 - t, t, th.Under);
                    return;
                }
                Box(p.X, p.Top - 1, p.Z, p.W, 1, p.D, th.Surface);
                if (t > 0)
                {
                    int inset = p.W >= 6 && p.D >= 6 && r.Chance(0.6f) ? 1 : 0;
                    Box(p.X + inset, p.Top - 1 - t, p.Z + inset, p.W - 2 * inset, t, p.D - 2 * inset, th.Under);
                    if (p.Rocky && p.W - 2 * inset >= 6 && p.D - 2 * inset >= 6)
                        Box(p.X + inset + 2, p.Top - 3 - t, p.Z + inset + 2, p.W - 2 * inset - 4, 2, p.D - 2 * inset - 4, th.Under == "solid" ? "dark" : "solid");
                }
            }

            /// <summary>A square ring (frame) of blocks. Centered frames are their own mirror images.</summary>
            void Frame(int x, int z, int w, int d, int s, int y, int h, string type)
            {
                if (s * 2 >= Math.Min(w, d)) { Box(x, y, z, w, h, d, type); return; }
                if (sym == Sym.Rot4 && w == d)
                {
                    // Pinwheel layout so each strip maps onto another under a quarter turn.
                    Box(x, y, z, w - s, h, s, type);
                    Box(x + w - s, y, z, s, h, d - s, type);
                    Box(x + s, y, z + d - s, w - s, h, s, type);
                    Box(x, y, z + s, s, h, d - s, type);
                }
                else
                {
                    Box(x, y, z, w, h, s, type);
                    Box(x, y, z + d - s, w, h, s, type);
                    Box(x, y, z + s, s, h, d - 2 * s, type);
                    Box(x + w - s, y, z + s, s, h, d - 2 * s, type);
                }
            }

            // ------------------------------------------------------------------ bridges

            static int Overlap(int a0, int a1, int b0, int b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

            /// <summary>Links two pads with a bridge, stepping stones or (when close) a jumpable gap.</summary>
            bool Connect(Pad a, Pad b, bool required)
            {
                int ox = Overlap(a.X, a.X + a.W, b.X, b.X + b.W), oz = Overlap(a.Z, a.Z + a.D, b.Z, b.Z + b.D);
                int dh = Math.Abs(a.Top - b.Top);
                // Close enough to jump?
                int gap = ox >= 3 && oz < 0 ? -oz : oz >= 3 && ox < 0 ? -ox : 99;
                if (gap <= 3 && dh <= 1 && r.Chance(0.35f))
                {
                    ReserveLanding(a, b);
                    return true;
                }
                // Both widths and both L orientations: a corner on a centered pad only mirrors cleanly with the right parity.
                int firstBw = r.Range(2, 4);
                bool firstX = r.Chance(0.5f);
                string why = "";
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    int bw = attempt % 2 == 0 ? firstBw : 5 - firstBw;
                    var path = attempt < 4 ? Path(a, b, bw, (attempt < 2) == firstX) : Dogleg(a, b, bw);
                    if (path == null || !PathFree(path)) { why += path == null ? "-" : "x"; continue; }
                    EmitPath(path, a, b);
                    return true;
                }
                if (required) throw new RetryException("no room for a bridge " + a.Role + "->" + b.Role + " " + why);
                return false;
            }

            void ReserveLanding(Pad a, Pad b)
            {
                // Keep the facing edges clear so the jump stays possible.
                foreach (var p in new[] { a, b })
                {
                    var o = p == a ? b : a;
                    int x0 = Math.Max(p.X, o.X), x1 = Math.Min(p.X + p.W, o.X + o.W);
                    int z0 = Math.Max(p.Z, o.Z), z1 = Math.Min(p.Z + p.D, o.Z + o.D);
                    if (x1 - x0 >= 1) { z0 = o.Z > p.Z ? p.Z + p.D - 2 : p.Z; z1 = z0 + 2; }
                    else { x0 = o.X > p.X ? p.X + p.W - 2 : p.X; x1 = x0 + 2; }
                    Reserve(x0, p.Top, z0, x1 - x0, 2, z1 - z0, true, false);
                }
            }

            /// <summary>Cells from pad a to pad b: straight when they line up, else an L with one corner.</summary>
            List<PathCell> Path(Pad a, Pad b, int bw, bool xFirst)
            {
                var cells = new List<PathCell>();
                int ox = Overlap(a.X, a.X + a.W, b.X, b.X + b.W), oz = Overlap(a.Z, a.Z + a.D, b.Z, b.Z + b.D);
                // Straight when the pads line up by at least a cell (a narrow overlap lets the deck stick out a little).
                if (oz >= 1 && ox < 0)
                {
                    int lo = Math.Max(a.Z, b.Z), hi = Math.Min(a.Z + a.D, b.Z + b.D);
                    int zc = BandStart(lo, hi, bw, sz);
                    int dir = b.X > a.X ? 1 : -1;
                    int xs = dir > 0 ? a.X + a.W : a.X - 1, xe = dir > 0 ? b.X - 1 : b.X + b.W;
                    for (int x = xs; dir > 0 ? x <= xe : x >= xe; x += dir) cells.Add(new PathCell { R = new Rect(x, zc, 1, bw), Axis = 0 });
                }
                else if (ox >= 1 && oz < 0)
                {
                    int lo = Math.Max(a.X, b.X), hi = Math.Min(a.X + a.W, b.X + b.W);
                    int xc = BandStart(lo, hi, bw, sx);
                    int dir = b.Z > a.Z ? 1 : -1;
                    int zs = dir > 0 ? a.Z + a.D : a.Z - 1, ze = dir > 0 ? b.Z - 1 : b.Z + b.D;
                    for (int z = zs; dir > 0 ? z <= ze : z >= ze; z += dir) cells.Add(new PathCell { R = new Rect(xc, z, bw, 1), Axis = 2 });
                }
                else if (ox <= 0 && oz <= 0)
                {
                    // L shape. xFirst: leave a along X, turn at b's column, arrive along Z.
                    Pad p = xFirst ? a : b, q = xFirst ? b : a;
                    int zc = p.Z + (p.D - bw) / 2, xc = q.X + (q.W - bw) / 2;
                    int dx = q.X > p.X ? 1 : -1, dz = q.Z > p.Z ? 1 : -1;
                    int xs = dx > 0 ? p.X + p.W : p.X - 1, xe = dx > 0 ? xc - 1 : xc + bw;
                    for (int x = xs; dx > 0 ? x <= xe : x >= xe; x += dx) cells.Add(new PathCell { R = new Rect(x, zc, 1, bw), Axis = 0 });
                    cells.Add(new PathCell { R = new Rect(xc, zc, bw, bw), Axis = 1 });
                    int zs = dz > 0 ? zc + bw : zc - 1, ze = dz > 0 ? q.Z - 1 : q.Z + q.D;
                    for (int z = zs; dz > 0 ? z <= ze : z >= ze; z += dz) cells.Add(new PathCell { R = new Rect(xc, z, bw, 1), Axis = 2 });
                    if (!xFirst) cells.Reverse();
                }
                else return null;
                return Ramp(cells, a, b);
            }

            /// <summary>Start of a bw-wide band across the shared range [lo, hi), touching it by at least a cell.</summary>
            int BandStart(int lo, int hi, int bw, int size)
            {
                int span = hi - lo;
                int c = span >= bw ? lo + (span - bw) / 2 + (span - bw >= 2 ? r.Range(-1, 2) : 0) : lo - (bw - span) / 2;
                c = Math.Max(c, span >= bw ? lo : lo - bw + 1);
                c = Math.Min(c, span >= bw ? hi - bw : hi - 1);
                return Clamp(c, 1, size - bw - 1);
            }

            /// <summary>A Z-shaped path: out of a, a sideways jog halfway, then into b. Works when the pads only partly line up.</summary>
            List<PathCell> Dogleg(Pad a, Pad b, int bw)
            {
                var cells = new List<PathCell>();
                int gx = Math.Max(a.X - (b.X + b.W), b.X - (a.X + a.W)), gz = Math.Max(a.Z - (b.Z + b.D), b.Z - (a.Z + a.D));
                bool alongZ = gz >= gx;
                if ((alongZ ? gz : gx) < bw + 2) return null;
                // Work in (u = along the gap, v = across) and map back.
                int au0 = alongZ ? a.Z : a.X, aus = alongZ ? a.D : a.W, av0 = alongZ ? a.X : a.Z, avs = alongZ ? a.W : a.D;
                int bu0 = alongZ ? b.Z : b.X, bus = alongZ ? b.D : b.W, bv0 = alongZ ? b.X : b.Z, bvs = alongZ ? b.W : b.D;
                int dir = bu0 > au0 ? 1 : -1;
                int us = dir > 0 ? au0 + aus : au0 - 1, ue = dir > 0 ? bu0 - 1 : bu0 + bus;
                int va = av0 + (avs - bw) / 2, vb = bv0 + (bvs - bw) / 2;
                if (va == vb) return null;
                int len = Math.Abs(ue - us) + 1;
                int um = us + dir * ((len - bw) / 2);
                Rect R(int u, int v, int uw, int vw) => alongZ ? new Rect(v, u, vw, uw) : new Rect(u, v, uw, vw);
                int crossAxis = alongZ ? 0 : 2, runAxis = alongZ ? 2 : 0;
                for (int u = us; u != um; u += dir) cells.Add(new PathCell { R = R(u, va, 1, bw), Axis = runAxis });
                int uLo = dir > 0 ? um : um - bw + 1;
                int vdir = vb > va ? 1 : -1;
                for (int v = vdir > 0 ? va : va + bw - 1; v != (vdir > 0 ? vb + bw : vb - 1); v += vdir)
                    cells.Add(new PathCell { R = R(uLo, v, bw, 1), Axis = crossAxis });
                for (int u = um + dir * bw; dir > 0 ? u <= ue : u >= ue; u += dir) cells.Add(new PathCell { R = R(u, vb, 1, bw), Axis = runAxis });
                return Ramp(cells, a, b);
            }

            /// <summary>Heights ramping evenly from a.Top to b.Top (steps of 2 are still an easy hop). Null if too steep.</summary>
            static List<PathCell> Ramp(List<PathCell> cells, Pad a, Pad b)
            {
                if (cells.Count == 0) return null;
                int n = cells.Count, prev = a.Top;
                for (int i = 0; i < n; i++)
                {
                    var c = cells[i];
                    c.Top = a.Top + (int)Math.Round((b.Top - a.Top) * (i + 1) / (double)(n + 1));
                    if (Math.Abs(c.Top - prev) > 2) return null;
                    prev = c.Top;
                    cells[i] = c;
                }
                if (Math.Abs(b.Top - prev) > 2) return null;
                return cells;
            }

            bool PathFree(List<PathCell> path)
            {
                foreach (var c in path)
                    if (!Free(c.R.X, c.Top - 1, c.R.Z, c.R.W, 3, c.R.D, false, false)) return false;
                return true;
            }

            void EmitPath(List<PathCell> path, Pad a, Pad b)
            {
                string type = r.Pick(th.Bridges);
                bool straight = path.TrueForAll(c => c.Axis == path[0].Axis) && path[0].Axis != 1;
                bool stones = straight && path.Count >= 4 && r.Chance(th.Stones);
                if (stones && TryStones(path, a, b)) return;

                // Merge runs of equal height into single boxes.
                int i = 0;
                while (i < path.Count)
                {
                    int j = i;
                    var u = path[i].R;
                    while (j + 1 < path.Count && path[j + 1].Top == path[i].Top && path[j + 1].Axis == path[i].Axis && path[i].Axis != 1)
                    {
                        j++;
                        var c = path[j].R;
                        int x0 = Math.Min(u.X, c.X), z0 = Math.Min(u.Z, c.Z);
                        u = new Rect(x0, z0, Math.Max(u.X + u.W, c.X + c.W) - x0, Math.Max(u.Z + u.D, c.Z + c.D) - z0);
                    }
                    int top = path[i].Top;
                    Box(u.X, top - 1, u.Z, u.W, 1, u.D, type);
                    if (path[i].Axis != 1 && j - i + 1 >= 3)
                    {
                        var lane = new Lane { X = u.X, Z = u.Z, Axis = path[i].Axis, Top = top, Len = path[i].Axis == 0 ? u.W : u.D, Width = path[i].Axis == 0 ? u.D : u.W };
                        lanes.Add(lane);
                        if (th.Crenels && r.Chance(0.5f)) Crenels(lane);
                    }
                    i = j + 1;
                }
                foreach (var c in path) Reserve(c.R.X, c.Top, c.R.Z, c.R.W, 2, c.R.D, true, false);
                ReserveEnds(path, a, b);
            }

            void ReserveEnds(List<PathCell> path, Pad a, Pad b)
            {
                // Two cells into each pad where the bridge lands.
                foreach (var end in new[] { (path[0], a), (path[path.Count - 1], b) })
                {
                    var c = end.Item1.R;
                    var p = end.Item2;
                    var grow = new Rect(c.X - 2, c.Z - 2, c.W + 4, c.D + 4);
                    int x0 = Math.Max(grow.X, p.X), x1 = Math.Min(grow.X + grow.W, p.X + p.W);
                    int z0 = Math.Max(grow.Z, p.Z), z1 = Math.Min(grow.Z + grow.D, p.Z + p.D);
                    if (x1 > x0 && z1 > z0) Reserve(x0, p.Top, z0, x1 - x0, 2, z1 - z0, true, false);
                }
            }

            bool TryStones(List<PathCell> path, Pad a, Pad b)
            {
                int n = path.Count;
                int k = Math.Max(1, (int)Math.Ceiling((n - 2) / 4.0));
                int gaps = n - 2 * k;
                if (gaps < 0) return false;
                var starts = new List<int>();
                int pos = 0;
                for (int s = 0; s < k; s++)
                {
                    int gapHere = gaps / (k + 1) + (s < gaps % (k + 1) ? 1 : 0);
                    pos += gapHere;
                    starts.Add(pos);
                    pos += 2;
                }
                int prev = a.Top;
                var tops = new List<int>();
                foreach (int s in starts)
                {
                    int t = path[s].Top;
                    if (Math.Abs(t - prev) > 1) return false;
                    tops.Add(t);
                    prev = t;
                }
                if (Math.Abs(b.Top - prev) > 1) return false;
                string type = th.Surface == "ice" ? "light" : th.Accent;
                int thick = r.Chance(0.5f) ? 2 : 1;
                for (int s = 0; s < k; s++)
                {
                    var c0 = path[starts[s]].R;
                    var c1 = path[starts[s] + 1].R;
                    int x0 = Math.Min(c0.X, c1.X), z0 = Math.Min(c0.Z, c1.Z);
                    int w = Math.Max(c0.X + c0.W, c1.X + c1.W) - x0, d = Math.Max(c0.Z + c0.D, c1.Z + c1.D) - z0;
                    Box(x0, tops[s] - thick, z0, w, thick, d, type);
                    Reserve(x0, tops[s], z0, w, 2, d, true, false);
                }
                ReserveEnds(path, a, b);
                return true;
            }

            void Crenels(Lane lane)
            {
                for (int i = 1; i < lane.Len - 1; i += 2)
                {
                    if (lane.Axis == 0)
                    {
                        TryBlock(lane.X + i, lane.Top, lane.Z - 1, 1, 1, 1, th.Accent);
                        TryBlock(lane.X + i, lane.Top, lane.Z + lane.Width, 1, 1, 1, th.Accent);
                    }
                    else
                    {
                        TryBlock(lane.X - 1, lane.Top, lane.Z + i, 1, 1, 1, th.Accent);
                        TryBlock(lane.X + lane.Width, lane.Top, lane.Z + i, 1, 1, 1, th.Accent);
                    }
                }
            }

            bool TryBlock(int x, int y, int z, int w, int h, int d, string type)
            {
                if (!Free(x, y, z, w, h, d, false, false)) return false;
                Box(x, y, z, w, h, d, type);
                return true;
            }

            // ------------------------------------------------------------------ spawns

            /// <summary>Places <paramref name="count"/> spawns per image on a pad (at least one, or retry). set &lt; 0 = neutral.</summary>
            void SpawnsOn(Pad p, int set, int count, bool outer, Func<int, int, bool> zone = null)
            {
                if (PlaceSpawns(p, set, count, outer, zone) == 0) throw new RetryException("no room for spawns on " + p.Role);
            }

            /// <summary>Spawns on the pads in order until all <paramref name="count"/> (per image) are placed.</summary>
            void SpawnsOnAny(int set, int count, params Pad[] options)
            {
                foreach (var p in options)
                {
                    if (p == null) continue;
                    count -= PlaceSpawns(p, set, count, false, null);
                    if (count <= 0) return;
                }
                throw new RetryException("no room for spawns (set " + set + ")");
            }

            int PlaceSpawns(Pad p, int set, int count, bool outer, Func<int, int, bool> zone)
            {
                int placed = 0;
                int fx = -1, fz = -1;
                for (int n = 0; n < count; n++)
                {
                    double best = double.MinValue;
                    int bx = -1, bz = -1;
                    for (int x = p.X + 1; x < p.X + p.W - 1; x++)
                        for (int z = p.Z + 1; z < p.Z + p.D - 1; z++)
                        {
                            if (zone != null && !zone(x, z)) continue;
                            if (!SpawnOk(x, p.Top, z)) continue;
                            double score = r.Value() * 4.0;
                            if (outer) score += Dist(x + 0.5, z + 0.5, sx * 0.5, sz * 0.5);
                            if (fx >= 0)
                            {
                                double d = Dist(x, z, fx, fz);
                                if (d < 2.0) continue;
                                score = r.Value() - Math.Abs(d - 3.0);
                            }
                            if (score > best) { best = score; bx = x; bz = z; }
                        }
                    if (bx < 0) break;
                    for (int k = 0; k < Images; k++)
                    {
                        var im = MapRect(k, new Rect(bx, bz, 1, 1));
                        m.spawns.Add(new SpawnData(im.X, p.Top, im.Z, TeamFor(set, k)));
                    }
                    Reserve(bx - 1, p.Top, bz - 1, 3, 3, 3, true, true);
                    if (fx < 0) { fx = bx; fz = bz; }
                    placed++;
                }
                return placed;
            }

            static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

            bool SpawnOk(int x, int y, int z)
            {
                var ims = new List<Rect>();
                for (int k = 0; k < Images; k++) ims.Add(MapRect(k, new Rect(x, z, 1, 1)));
                for (int i = 0; i < ims.Count; i++)
                {
                    var c = ims[i];
                    if (Math.Min(Math.Min(c.X, c.Z), Math.Min(sx - 1 - c.X, sz - 1 - c.Z)) < 3) return false;
                    if (!g.Standable(c.X, y, c.Z)) return false;
                    var below = g.At(c.X, y - 1, c.Z);
                    var t = BlockTypes.Get(below.type);
                    if (t.Destructible || t.BounceSpeed > 0f) return false;
                    if (noMech[g.Index(c.X, y, c.Z)]) return false;
                    for (int j = 0; j < i; j++) if (Dist(c.X, c.Z, ims[j].X, ims[j].Z) < 3.0) return false;
                    foreach (var s in m.spawns) if (Math.Abs(s.y - y) < 3 && Dist(c.X, c.Z, s.x, s.z) < 2.0) return false;
                }
                return true;
            }

            // ------------------------------------------------------------------ layouts

            void LayoutPlateau()
            {
                int S = r.Range(40, 55);
                sx = sz = S;
                sy = r.Range(30, 41);
                sym = r.Chance(0.5f) ? Sym.Mirror4 : Sym.Rot4;
                Init();
                int bt = baseTop;
                int kx = r.Range(3, 5), kz = r.Range(3, 5);
                int cw = Math.Min(r.Range(10, 17), S - 2 * (Math.Max(kx, kz) + 9));
                int c0 = Centered(S, ref cw);
                int ctop = bt + r.Range(1, 3);
                var C = new Pad(c0, c0, cw, cw, ctop, r.Range(2, 4), "center");
                float feature = r.Value();
                if (feature < 0.3f && th.Pool != null && cw >= 10)
                {
                    int hole = r.Range(4, cw - 5);
                    if (((cw - hole) & 1) != 0) hole--;
                    C.Hole = hole;
                }
                PadOrRetry(C);
                centerPad = C;
                if (C.Hole == 0 && feature < 0.6f)
                {
                    int dw = cw - r.Range(4, 8);
                    if (dw >= 3)
                    {
                        int d0 = Centered(S, ref dw);
                        Box(d0, ctop, d0, dw, 1, dw, th.Accent);
                        centerPad = new Pad(d0, d0, dw, dw, ctop + 1, 1, "dais");
                        pads.Add(centerPad);
                    }
                }

                int maxKw = Math.Min(11, c0 - 3 - kx), maxKd = Math.Min(11, c0 - 3 - kz);
                var K = new Pad(kx, kz, r.Range(6, maxKw + 1), r.Range(6, maxKd + 1), Clamp(ctop + r.Range(-2, 3), bt, bt + 4), r.Range(2, 4), "corner") { Rocky = r.Chance(0.5f) };
                PadOrRetry(K);

                Pad sideL = null, sideT = null;
                int sd = r.Range(5, 9);
                sd = Math.Min(sd, S - 2 * (K.Z + K.D + 2));
                if (sd >= 4 && r.Chance(0.7f))
                {
                    int s0 = Centered(S, ref sd);
                    int sw = r.Range(4, 8), sxx = r.Range(3, 5);
                    if (sxx + sw + 3 <= c0 && sd >= 4)
                    {
                        sideL = new Pad(sxx, s0, sw, sd, Clamp(ctop + r.Range(-1, 2), bt, bt + 4), 2, "side");
                        if (!TryPad(sideL)) sideL = null;
                        else if (sym == Sym.Mirror4)
                        {
                            sideT = new Pad(s0, sxx, sd, sw, sideL.Top, 2, "side");
                            if (!TryPad(sideT)) sideT = null;
                        }
                    }
                }

                if (sideL != null)
                {
                    Connect(K, sideL, true);
                    Connect(sideL, C, true);
                    if (sideT != null)
                    {
                        Connect(sideT, C, true);
                        if (r.Chance(0.6f)) Connect(K, sideT, false);
                    }
                }
                else
                {
                    Connect(K, C, true);
                    if (r.Chance(0.4f)) Connect(K, ImagePad(K, sym == Sym.Mirror4 ? 2 : 3), false);
                }

                SpawnsOn(K, 0, 2, true);
                if (teams > 4) SpawnsOnAny(1, 1, sideL, sideT, C, K);
                SpawnsOnAny(-1, 1, sideL, C, sideT, K);
            }

            void LayoutBases()
            {
                sx = r.Range(46, 57);
                sz = r.Range(28, 41);
                sy = r.Range(28, 39);
                sym = r.Chance(0.5f) ? Sym.Rot2 : Sym.MirrorX;
                Init();
                int bt = baseTop;
                int cw = r.Range(6, 11);
                int cx0 = Centered(sx, ref cw);
                int bx = r.Range(3, 5);
                int bw = Math.Min(r.Range(9, 14), cx0 - bx - 5);
                var B = new Pad(bx, 3, bw, sz - 6, bt + r.Range(0, 2), r.Range(2, 4), "base");
                PadOrRetry(B);
                if (r.Chance(0.6f))
                {
                    // A raised back deck for snipers.
                    int dd = r.Range(4, Math.Max(5, B.D / 3));
                    int dz = B.Z + r.Range(2, Math.Max(3, B.D - dd - 2));
                    int dh = r.Range(1, 3);
                    Box(B.X, B.Top, dz, 2, dh, dd, th.Accent);
                    pads.Add(new Pad(B.X, dz, 2, dd, B.Top + dh, 1, "deck"));
                }

                int cd = r.Range(6, Math.Max(7, sz - 14));
                int cz0 = Centered(sz, ref cd);
                var C = new Pad(cx0, cz0, cw, cd, bt + r.Range(0, 3), r.Range(2, 4), "center") { Rocky = r.Chance(0.5f) };
                PadOrRetry(C);
                centerPad = C;
                Connect(B, C, true);

                // Flank lanes straight across, outside the center island.
                int flanks = r.Range(0, 3);
                for (int f = 0; f < flanks; f++)
                {
                    int zl = f == 0 ? r.Range(B.Z + 1, Math.Max(B.Z + 2, cz0 - 4)) : r.Range(cz0 + cd + 1, Math.Max(cz0 + cd + 2, B.Z + B.D - 3));
                    if (zl + 2 > cz0 - 1 && zl < cz0 + cd + 1) continue;
                    var lanePad = new Pad(B.X, zl, B.W, 2, B.Top, B.Thick, "lane");
                    var far = ImagePad(lanePad, 1);
                    var path = Path(lanePad, far, 2, true);
                    if (path != null && PathFree(path)) EmitPath(path, lanePad, far);
                }

                // Islands on the sides of the gap, if there's room.
                if (r.Chance(0.5f))
                {
                    int iw = r.Range(4, 7), id = r.Range(4, 7);
                    int ix = B.X + B.W + r.Range(3, 5);
                    int iz = r.Chance(0.5f) ? B.Z + r.Range(0, 3) : B.Z + B.D - id - r.Range(0, 3);
                    var I = new Pad(ix, iz, iw, id, B.Top + r.Range(-1, 3), 2, "outpost") { Rocky = true };
                    if (ix + iw + 3 <= cx0 && TryPad(I))
                    {
                        Connect(B, I, true);
                        Connect(I, C, false);
                    }
                }

                int mid = sz / 2;
                SpawnsOn(B, 0, 2, false, (x, z) => z < mid - 2);
                SpawnsOn(B, 1, 2, false, (x, z) => z > mid + 1);
                if (teams > 4) SpawnsOn(B, 2, 1, false, (x, z) => Math.Abs(z - mid) <= 3);
                if (teams > 6) SpawnsOn(C, 3, 1, false);
                SpawnsOnAny(-1, 2, C, B);
            }

            void LayoutTowers()
            {
                int S = r.Range(40, 53);
                sx = sz = S;
                sy = r.Range(34, 41);
                sym = r.Chance(0.5f) ? Sym.Mirror4 : Sym.Rot4;
                Init();
                int bt = baseTop;
                var F = new Pad(3, 3, S - 6, S - 6, bt, r.Range(1, 3), "floor");
                bool centralTower = r.Chance(0.6f);
                if (!centralTower && th.Pool != null)
                {
                    int hole = r.Range(4, 9);
                    if (((F.W - hole) & 1) != 0) hole--;
                    F.Hole = hole;
                }
                PadOrRetry(F);
                centerPad = centralTower ? null : F;

                // A tower in the quadrant: platform on a column, stairs down to the floor.
                int pw = r.Range(5, 8);
                int qx = r.Range(8, Math.Max(9, S / 2 - pw - 3)), qz = r.Range(8, Math.Max(9, S / 2 - pw - 3));
                int qtop = bt + r.Range(4, 8);
                var Q = Tower(qx, qz, pw, pw, qtop, F.Top, "tower");
                if (Q == null) throw new RetryException("tower does not fit");
                if (!Stairs(Q, F.Top)) throw new RetryException("no stairs");

                if (centralTower)
                {
                    int cw = r.Range(6, 10);
                    int c0 = Centered(S, ref cw);
                    int ctop = Math.Min(sy - 14, qtop + r.Range(-1, 4));
                    var C = Tower(c0, c0, cw, cw, ctop, F.Top, "summit");
                    if (C == null) throw new RetryException("summit does not fit");
                    centerPad = C;
                    if (!Connect(Q, C, false) && !Stairs(C, F.Top)) throw new RetryException("summit unreachable");
                }
                if (r.Chance(0.5f)) Connect(Q, ImagePad(Q, sym == Sym.Mirror4 ? 1 : 1), false);

                int half = S / 2;
                SpawnsOn(F, 0, 2, true, (x, z) => x < half - 3 && z < half - 3);
                if (teams > 4) SpawnsOn(F, 1, 1, false, (x, z) => x < half - 2 && z > half - 6 && z < half - 2);
                SpawnsOnAny(-1, 1, Q, centerPad, F);
            }

            /// <summary>A platform on a column standing on the floor.</summary>
            Pad Tower(int x, int z, int w, int d, int top, int floorTop, string role)
            {
                int colH = top - 1 - floorTop;
                if (colH < 1) return null;
                if (!Free(x - 1, floorTop, z - 1, w + 2, top + 3 - floorTop, d + 2, false, false)) return null;
                int inset = w >= 5 && d >= 5 ? 1 : 0;
                Box(x + inset, floorTop, z + inset, w - 2 * inset, colH, d - 2 * inset, th.Pillar == "glass" ? th.Under : th.Pillar);
                Box(x, top - 1, z, w, 1, d, th.Surface == "ice" ? "light" : th.Surface);
                var p = new Pad(x, z, w, d, top, 1, role);
                pads.Add(p);
                return p;
            }

            /// <summary>A staircase from a raised pad down to the floor, on whichever side has room.</summary>
            bool Stairs(Pad p, int floorTop)
            {
                int rise = p.Top - floorTop;
                if (rise <= 1) return true;
                var dirs = new List<int> { 0, 1, 2, 3 };
                r.Shuffle(dirs);
                string type = th.Accent;
                foreach (int dir in dirs)
                {
                    // dir: 0 = -X, 1 = +X, 2 = -Z, 3 = +Z. Steps run away from the pad.
                    bool ok = true;
                    int along = dir < 2 ? p.D : p.W;
                    int off = (along - 2) / 2;
                    var steps = new List<Rect>();
                    for (int i = 1; i < rise; i++)
                    {
                        Rect c;
                        switch (dir)
                        {
                            case 0: c = new Rect(p.X - i, p.Z + off, 1, 2); break;
                            case 1: c = new Rect(p.X + p.W - 1 + i, p.Z + off, 1, 2); break;
                            case 2: c = new Rect(p.X + off, p.Z - i, 2, 1); break;
                            default: c = new Rect(p.X + off, p.Z + p.D - 1 + i, 2, 1); break;
                        }
                        int stepTop = p.Top - i;
                        if (!Free(c.X, floorTop, c.Z, c.W, stepTop - floorTop + 2, c.D, true, false)) { ok = false; break; }
                        steps.Add(c);
                    }
                    if (!ok) continue;
                    // The cell past the last step must be open floor.
                    for (int i = 0; i < steps.Count; i++)
                    {
                        var c = steps[i];
                        Box(c.X, floorTop, c.Z, c.W, p.Top - (i + 1) - floorTop, c.D, type);
                        Reserve(c.X, p.Top - (i + 1), c.Z, c.W, 2, c.D, true, false);
                    }
                    return true;
                }
                return false;
            }

            void LayoutIslands()
            {
                float sroll = r.Value();
                sym = sroll < 0.35f ? Sym.Rot4 : sroll < 0.7f ? Sym.Mirror4 : Sym.Rot2;
                if (sym == Sym.Rot2) { sx = r.Range(44, 57); sz = r.Range(36, 47); }
                else sx = sz = r.Range(42, 55);
                sy = r.Range(30, 41);
                Init();
                int bt = baseTop;
                int cw = r.Range(6, 11), cd = sym == Sym.Rot4 ? cw : r.Range(6, 11);
                int cx0 = Centered(sx, ref cw), cz0 = Centered(sz, ref cd);
                if (sym == Sym.Rot4 && cw != cd) { cd = cw; cz0 = Centered(sz, ref cd); }
                var C = new Pad(cx0, cz0, cw, cd, bt + r.Range(1, 4), r.Range(2, 4), "center") { Rocky = r.Chance(0.6f) };
                PadOrRetry(C);
                centerPad = C;

                int domX = sx / 2, domZ = Images == 4 ? sz / 2 : sz;
                int want = Images == 4 ? r.Range(3, 6) : r.Range(4, 8);
                var isles = new List<Pad>();
                for (int tries = 0; tries < 200 && isles.Count < want; tries++)
                {
                    int w = r.Range(4, 10), d = r.Range(4, 10);
                    int x = r.Range(3, domX - w), z = r.Range(3, domZ - d - (Images == 4 ? 0 : 3) + 1);
                    if (x + w > domX - 1 || z + d > (Images == 4 ? domZ - 1 : sz - 3)) continue;
                    var p = new Pad(x, z, w, d, bt + r.Range(0, 6), r.Range(2, 4), "isle") { Rocky = r.Chance(0.5f) };
                    if (TryPad(p)) isles.Add(p);
                }
                if (isles.Count < 2) throw new RetryException("too few islands");

                // Minimum spanning tree over the islands and the center.
                var nodes = new List<Pad>(isles) { C };
                var inTree = new bool[nodes.Count];
                inTree[nodes.Count - 1] = true;
                var edges = new List<(Pad, Pad)>();
                for (int added = 1; added < nodes.Count; added++)
                {
                    double best = double.MaxValue;
                    int bi = -1, bj = -1;
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (!inTree[i]) continue;
                        for (int j = 0; j < nodes.Count; j++)
                        {
                            if (inTree[j]) continue;
                            double c = GapCost(nodes[i], nodes[j]);
                            if (c < best) { best = c; bi = i; bj = j; }
                        }
                    }
                    inTree[bj] = true;
                    edges.Add((nodes[bj], nodes[bi]));
                }
                foreach (var e in edges) Connect(e.Item1, e.Item2, true);
                int extra = r.Range(0, 3);
                for (int i = 0; i < extra; i++)
                {
                    var a = r.Pick(nodes);
                    var b = r.Pick(nodes);
                    if (a != b && GapCost(a, b) < 16) Connect(a, b, false);
                }

                // Teams on the islands farthest from the center.
                isles.Sort((a, b) => Dist(b.Cx, b.Cz, sx * 0.5, sz * 0.5).CompareTo(Dist(a.Cx, a.Cz, sx * 0.5, sz * 0.5)));
                SpawnsOn(isles[0], 0, 2, true);
                int next = 1;
                if (Images == 2) SpawnsOn(isles[Math.Min(next++, isles.Count - 1)], 1, 2, true);
                if (teams > 4) SpawnsOn(isles[Math.Min(next++, isles.Count - 1)], Images == 2 ? 2 : 1, 1, false);
                if (teams > 6 && Images == 2) SpawnsOn(isles[Math.Min(next++, isles.Count - 1)], 3, 1, false);
                var neutralOptions = new List<Pad> { C };
                for (int i = isles.Count - 1; i >= 0; i--) neutralOptions.Add(isles[i]);
                SpawnsOnAny(-1, Images == 2 ? 2 : 1, neutralOptions.ToArray());
            }

            static double GapCost(Pad a, Pad b)
            {
                int gx = Math.Max(0, Math.Max(a.X - (b.X + b.W), b.X - (a.X + a.W)));
                int gz = Math.Max(0, Math.Max(a.Z - (b.Z + b.D), b.Z - (a.Z + a.D)));
                int dh = Math.Abs(a.Top - b.Top);
                // A bridge climbs at most 2 per cell; pairs too close for their height difference are a last resort.
                return gx + gz + 2.0 * dh + (dh > 2 * (gx + gz) ? 1000.0 : 0.0);
            }

            void LayoutBowl()
            {
                int S = r.Range(40, 53);
                sx = sz = S;
                sy = r.Range(28, 37);
                sym = r.Chance(0.5f) ? Sym.Mirror4 : Sym.Rot4;
                Init();
                int bt = baseTop;
                int rings = r.Range(2, 4), rw = r.Range(3, 6), rise = r.Range(1, 3);
                while (S - 6 - 2 * rings * rw < 10 && rw > 3) rw--;
                if (S - 6 - 2 * rings * rw < 10) rings--;
                var ringPads = new List<Pad>();
                int bottom = bt - 2;
                for (int k = 0; k < rings; k++)
                {
                    int a = 3 + k * rw, w = S - 2 * a;
                    int top = bt + (rings - k) * rise;
                    Frame(a, a, w, w, rw, top - 1, 1, th.Surface == "ice" && k == 0 ? "light" : th.Surface);
                    Frame(a, a, w, w, rw, bottom, top - 1 - bottom, th.Under);
                    var p = new Pad(a, a, w, w, top, top - bottom, "ring" + k) { Ring = rw };
                    pads.Add(p);
                    ringPads.Add(p);
                }
                int ia = 3 + rings * rw, iw = S - 2 * ia;
                var F = new Pad(ia, ia, iw, iw, bt, 2, "arena floor");
                if (th.Pool != null && r.Chance(0.5f))
                {
                    int hole = r.Range(3, Math.Max(4, iw - 6));
                    if (((iw - hole) & 1) != 0) hole--;
                    if (hole >= 3) F.Hole = hole;
                }
                EmitPad(F); // sits flush inside the innermost ring
                pads.Add(F);
                centerPad = F;

                SpawnsOn(ringPads[0], 0, 2, true, (x, z) => x < S / 2 - 2 && z < S / 2 - 2);
                var inner = ringPads.Count > 1 ? ringPads[ringPads.Count - 1] : F;
                if (teams > 4) SpawnsOnAny(1, 1, inner, F, ringPads[0]);
                SpawnsOnAny(-1, 1, F, inner, ringPads[0]);
            }

            // ------------------------------------------------------------------ moving parts

            void PlaceMechs()
            {
                int features = (int)Math.Round(chaos * 3.2f);
                if (layout == Layout.Towers && (th.Traps as IList<string>).Contains(MechKinds.Elevator) && r.Chance(0.5f)) features++;
                for (int f = 0; f < features; f++)
                {
                    string kind = r.Pick(th.Traps);
                    for (int attempt = 0; attempt < 3; attempt++)
                        if (TryMech(kind)) break;
                }
            }

            bool TryMech(string kind)
            {
                switch (kind)
                {
                    case MechKinds.Platform: return TryPlatform();
                    case MechKinds.Elevator: return TryElevator();
                    case MechKinds.Spinner: return TrySpinner();
                    case MechKinds.Pendulum: return TryPendulum();
                    case MechKinds.Spikes: return TrySpikes();
                    case MechKinds.Saw: return TrySaw();
                }
                return false;
            }

            /// <summary>Adds a part and its symmetric copies if they all fit. single = the part is its own image (centered).</summary>
            bool AddMech(MechData proto, bool single, float phaseStep)
            {
                var list = new List<MechData>();
                for (int k = 0; k < (single ? 1 : Images); k++)
                {
                    var c = proto.Clone();
                    if (c.kind == MechKinds.Pendulum)
                    {
                        var cell = MapRect(k, new Rect(c.x, c.z, 1, 1));
                        c.x = cell.X; c.z = cell.Z;
                    }
                    else
                    {
                        var body = MapRect(k, new Rect(c.x, c.z, Math.Max(1, c.w), Math.Max(1, c.d)));
                        c.x = body.X; c.z = body.Z; c.w = body.W; c.d = body.D;
                    }
                    MapPoint(k, c.dx, c.dz, out double tx, out double tz);
                    MapPoint(k, 0, 0, out double ox, out double oz);
                    c.dx = (float)(tx - ox); c.dz = (float)(tz - oz);
                    c.axis = MapAxis(k, c.axis);
                    c.phase = (float)((proto.phase + k * phaseStep) % 1.0);
                    list.Add(c);
                }
                var boxes = new List<Box3>();
                foreach (var c in list)
                {
                    var s = Swept(c);
                    if (s.X0 < 0 || s.Z0 < 0 || s.Y0 < 0 || s.X1 > sx || s.Z1 > sz || s.Y1 > sy - 1) return false;
                    if (g.FirstBlockIn(MustBeClear(c)) != null) return false;
                    foreach (var o in boxes) if (o.Intersects(s)) return false;
                    foreach (var o in mechBoxes) if (o.Intersects(s)) return false;
                    if (AnyReserved(s, noMech)) return false;
                    double margin = (c.deadly ? 1.5 : 0.5) + 0.6;
                    foreach (var sp in m.spawns)
                        if (s.Grow(margin).Contains(sp.x + 0.5, sp.y + 0.5, sp.z + 0.5)) return false;
                    if (c.kind == MechKinds.Spikes)
                        for (int x = c.x; x < c.x + c.w; x++)
                            for (int z = c.z; z < c.z + c.d; z++)
                            {
                                var b = g.At(x, c.y - 1, z);
                                if (b == null || BlockTypes.Get(b.type).DamagePerSecond > 0f) return false;
                            }
                    boxes.Add(s);
                }
                foreach (var c in list) m.mechs.Add(c);
                foreach (var s in boxes)
                {
                    mechBoxes.Add(s);
                    ReserveRaw(s.Grow(1.0), true, true);
                }
                return true;
            }

            bool AnyReserved(Box3 b, bool[] grid)
            {
                int x0 = Math.Max(0, (int)Math.Floor(b.X0)), y0 = Math.Max(0, (int)Math.Floor(b.Y0)), z0 = Math.Max(0, (int)Math.Floor(b.Z0));
                int x1 = Math.Min(sx, (int)Math.Ceiling(b.X1)), y1 = Math.Min(sy, (int)Math.Ceiling(b.Y1)), z1 = Math.Min(sz, (int)Math.Ceiling(b.Z1));
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                        for (int x = x0; x < x1; x++)
                            if (grid[g.Index(x, y, z)]) return true;
                return false;
            }

            List<Pad> ShuffledPads()
            {
                var list = new List<Pad>(pads);
                r.Shuffle(list);
                return list;
            }

            bool TryPlatform()
            {
                // A shuttle across the gap between two pads of the same height.
                var list = ShuffledPads();
                foreach (var a in list)
                    foreach (var b0 in list)
                        for (int k = 0; k < Images; k++)
                        {
                            var b = k == 0 ? b0 : ImagePad(b0, k);
                            if (a == b0 && k == 0) continue;
                            if (a.Top != b.Top || a.Hole > 0 || b.Hole > 0) continue;
                            int ox = Overlap(a.X, a.X + a.W, b.X, b.X + b.W), oz = Overlap(a.Z, a.Z + a.D, b.Z, b.Z + b.D);
                            int len, x, z;
                            bool alongX;
                            if (oz >= 3 && b.X > a.X + a.W) { alongX = true; len = b.X - (a.X + a.W); x = a.X + a.W; z = Math.Max(a.Z, b.Z) + r.Range(0, oz - 2); }
                            else if (ox >= 3 && b.Z > a.Z + a.D) { alongX = false; len = b.Z - (a.Z + a.D); z = a.Z + a.D; x = Math.Max(a.X, b.X) + r.Range(0, ox - 2); }
                            else continue;
                            if (len < 5 || len > 22) continue;
                            if (!Free(alongX ? x : x, a.Top - 1, alongX ? z : z, alongX ? len : 3, 3, alongX ? 3 : len, true, true)) continue;
                            var mech = MechKinds.Preset(MechKinds.Platform, x, a.Top - 1, z);
                            mech.w = 3; mech.h = 1; mech.d = 3;
                            mech.dx = alongX ? len - 3 : 0f;
                            mech.dz = alongX ? 0f : len - 3;
                            mech.period = 3f + len * 0.45f;
                            mech.phase = r.Value();
                            mech.type = th.Accent == "dark" ? "dark" : "light";
                            if (AddMech(mech, false, 0.5f)) return true;
                        }
                return false;
            }

            bool TryElevator()
            {
                // From the ground beside a high pad up to its edge.
                foreach (var p in ShuffledPads())
                {
                    for (int side = 0; side < 4; side++)
                    {
                        int ex, ez;
                        int along = side < 2 ? p.D : p.W;
                        if (along < 3) continue;
                        int off = r.Range(0, along - 2);
                        switch (side)
                        {
                            case 0: ex = p.X - 3; ez = p.Z + off; break;
                            case 1: ex = p.X + p.W; ez = p.Z + off; break;
                            case 2: ex = p.X + off; ez = p.Z - 3; break;
                            default: ex = p.X + off; ez = p.Z + p.D; break;
                        }
                        if (ex < 3 || ez < 3 || ex + 3 > sx - 3 || ez + 3 > sz - 3) continue;
                        // Find solid ground under the whole 3x3 footprint.
                        int floor = -1;
                        for (int y = p.Top - 3; y >= 2; y--)
                        {
                            bool solid = true, empty = true;
                            for (int x = ex; x < ex + 3 && solid; x++)
                                for (int z = ez; z < ez + 3 && solid; z++)
                                {
                                    var b = g.At(x, y - 1, z);
                                    if (b == null || BlockTypes.Get(b.type).DamagePerSecond > 0f || BlockTypes.Get(b.type).BounceSpeed > 0f) solid = false;
                                    if (!g.Empty(x, y, z)) empty = false;
                                }
                            if (!empty) break;
                            if (solid) { floor = y; break; }
                        }
                        if (floor < 0 || p.Top - floor - 1 < 3) continue;
                        if (!Free(ex, floor, ez, 3, p.Top - floor + 2, 3, false, true)) continue;
                        var mech = MechKinds.Preset(MechKinds.Elevator, ex, floor, ez);
                        mech.dx = 0f; mech.dz = 0f;
                        mech.dy = p.Top - floor - 1;
                        mech.period = 4f + mech.dy * 0.6f;
                        mech.phase = r.Value();
                        if (AddMech(mech, false, 0.25f)) return true;
                    }
                }
                return false;
            }

            bool TrySpinner()
            {
                var p = centerPad;
                if (p == null || p.Hole > 0) return false;
                int w = Math.Min(r.Range(9, 15), p.W + 2);
                int x0 = Centered(sx, ref w);
                int d = (sz & 1) == 0 ? 2 : 1;
                int z0 = (sz - d) / 2;
                bool deadly = chaos > 0.6f && r.Chance(0.6f);
                int y = deadly ? p.Top + 2 : p.Top;
                var mech = MechKinds.Preset(MechKinds.Spinner, x0, y, z0);
                mech.w = w; mech.h = 1; mech.d = d;
                mech.period = r.Range(5f, 9f) * (r.Chance(0.5f) ? -1f : 1f);
                mech.type = deadly ? "dark" : th.Accent;
                if (deadly) { mech.deadly = true; mech.damage = 25f; }
                return AddMech(mech, true, 0f);
            }

            bool TryPendulum()
            {
                var list = new List<Lane>(lanes);
                r.Shuffle(list);
                foreach (var lane in list)
                {
                    if (lane.Len < 5) continue;
                    int count = lane.Len >= 12 && chaos > 0.5f ? 2 : 1;
                    int length = r.Range(4, 8);
                    int y = lane.Top + 1 + length;
                    if (y + 2 >= sy) continue;
                    bool any = false;
                    float angle = r.Range(55f, 70f), period = r.Range(2.6f, 3.8f);
                    for (int i = 0; i < count; i++)
                    {
                        int at = lane.Len * (i + 1) / (count + 1);
                        int px = lane.Axis == 0 ? lane.X + at : lane.X + lane.Width / 2;
                        int pz = lane.Axis == 0 ? lane.Z + lane.Width / 2 : lane.Z + at;
                        var mech = MechKinds.Preset(MechKinds.Pendulum, px, y, pz);
                        mech.dx = 0f;
                        mech.axis = lane.Axis == 0 ? 0 : 2;
                        mech.length = length;
                        mech.angle = angle;
                        mech.period = period;
                        mech.phase = i * 0.5f;
                        mech.w = mech.h = mech.d = 2;
                        if (AddMech(mech, false, 0.25f)) any = true;
                    }
                    if (any) return true;
                }
                return false;
            }

            bool TrySpikes()
            {
                // A short row of spike traps firing in a wave, on a bridge or a pad.
                var spots = new List<(int x, int z, int top, int axis, int room)>();
                foreach (var lane in lanes)
                    if (lane.Width >= 2 && lane.Len >= 4)
                        spots.Add(lane.Axis == 0 ? (lane.X + 1, lane.Z, lane.Top, 0, lane.Len - 2) : (lane.X, lane.Z + 1, lane.Top, 2, lane.Len - 2));
                foreach (var p in pads)
                    if (p.W >= 6 && p.D >= 6 && p.Hole == 0)
                    {
                        bool alongX = r.Chance(0.5f);
                        int x = p.X + 1 + r.Range(0, p.W - 4), z = p.Z + 1 + r.Range(0, p.D - 4);
                        spots.Add(alongX ? (p.X + 1, z, p.Top, 0, p.W - 2) : (x, p.Z + 1, p.Top, 2, p.D - 2));
                    }
                r.Shuffle(spots);
                foreach (var s in spots)
                {
                    int n = Math.Min(s.room / 2, r.Range(2, 5));
                    if (n < 1) continue;
                    int start = r.Range(0, s.room - n * 2 + 1);
                    int placed = 0;
                    for (int i = 0; i < n; i++)
                    {
                        int x = s.axis == 0 ? s.x + start + i * 2 : s.x, z = s.axis == 0 ? s.z : s.z + start + i * 2;
                        var mech = MechKinds.Preset(MechKinds.Spikes, x, s.top, z);
                        mech.phase = (i * 0.25f) % 1f;
                        if (AddMech(mech, false, 0.5f)) placed++;
                    }
                    if (placed > 0) return true;
                }
                return false;
            }

            bool TrySaw()
            {
                foreach (var p in ShuffledPads())
                {
                    if (p.Hole > 0 || p.Ring > 0 || Math.Max(p.W, p.D) < 9 || Math.Min(p.W, p.D) < 5) continue;
                    bool alongX = p.W >= p.D;
                    int len = (alongX ? p.W : p.D) - 2;
                    int across = alongX ? p.D : p.W;
                    int off = 1 + r.Range(0, across - 4);
                    int x = alongX ? p.X + 1 : p.X + off, z = alongX ? p.Z + off : p.Z + 1;
                    var mech = MechKinds.Preset(MechKinds.Saw, x, p.Top, z);
                    mech.w = 3; mech.h = 1; mech.d = 3;
                    mech.axis = alongX ? 2 : 0;
                    mech.dx = alongX ? len - 3 : 0f;
                    mech.dz = alongX ? 0f : len - 3;
                    mech.period = 2.5f + len * 0.3f;
                    mech.phase = r.Value();
                    if (mech.dx + mech.dz < 3f) continue;
                    if (AddMech(mech, false, 0.5f)) return true;
                }
                return false;
            }

            // ------------------------------------------------------------------ cover, bouncers, props

            void PlaceCover()
            {
                foreach (var p in pads)
                {
                    if (p.W < 4 || p.D < 4 || p.Role == "deck" || p.Role == "dais") continue;
                    double area = p.Ring > 0 ? 4.0 * p.Ring * (p.W - p.Ring) : p.Area;
                    // Pieces get copied into every image; a centered pad is its own image, so place fewer.
                    var ims = Images_(new Rect(p.X, p.Z, p.W, p.D));
                    int distinct = ims == null ? 1 : ims.Count;
                    int count = (int)Math.Round(area / r.Range(28, 46) * distinct / Images);
                    count = Clamp(count, 1, 7);
                    for (int i = 0; i < count; i++)
                    {
                        for (int attempt = 0; attempt < 8; attempt++)
                            if (TryCoverPiece(p)) break;
                    }
                }
            }

            bool TryCoverPiece(Pad p)
            {
                int w = 1, d = 1, h = 2;
                string type;
                float roll = r.Value();
                if (roll < 0.3f) { w = d = r.Chance(0.4f) ? 2 : 1; h = r.Range(2, 5); type = th.Pillar; }
                else if (roll < 0.55f) { if (r.Chance(0.5f)) w = r.Range(3, 6); else d = r.Range(3, 6); h = 2; type = th.Wall; }
                else if (roll < 0.8f) { w = d = r.Chance(0.5f) ? 2 : 1; h = r.Range(1, 3); type = th.Breakable; }
                else if (roll < 0.9f) { if (r.Chance(0.5f)) w = r.Range(3, 5); else d = r.Range(3, 5); h = r.Range(2, 4); type = th.Pane; }
                else { if (r.Chance(0.5f)) w = r.Range(2, 4); else d = r.Range(2, 4); h = 1; type = th.Accent; }
                if (p.W < w + 2 || p.D < d + 2) return false;
                int x = p.X + r.Range(0, p.W - w + 1), z = p.Z + r.Range(0, p.D - d + 1);
                int y = p.Top;
                if (y + h + 1 >= sy) return false;
                // Must stand on the pad, keep a cell of space around it, and stay off paths and spawns.
                for (int xx = x; xx < x + w; xx++)
                    for (int zz = z; zz < z + d; zz++)
                    {
                        var b = g.At(xx, y - 1, zz);
                        if (b == null || BlockTypes.Get(b.type).DamagePerSecond > 0f || BlockTypes.Get(b.type).BounceSpeed > 0f) return false;
                    }
                if (!Free(x - 1, y, z - 1, w + 2, Math.Min(h, 2), d + 2, false, false)) return false;
                if (!Free(x, y, z, w, h, d, true, true)) return false;
                if (type == "blast" && NearSpawn(x, z, y, 5.0)) return false;
                Box(x, y, z, w, h, d, type);
                return true;
            }

            bool NearSpawn(double x, double z, int y, double radius)
            {
                for (int k = 0; k < Images; k++)
                {
                    MapPoint(k, x + 0.5, z + 0.5, out double px, out double pz);
                    foreach (var s in m.spawns)
                        if (Math.Abs(s.y - y) < 4 && Dist(px, pz, s.x + 0.5, s.z + 0.5) < radius) return true;
                }
                return false;
            }

            void PlaceBouncers()
            {
                int n = 0;
                if (r.Chance(th.Bouncers)) n++;
                if (r.Chance(th.Bouncers * 0.5f)) n++;
                for (int i = 0; i < n; i++)
                {
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        if (r.Chance(0.5f) && layout != Layout.Bowl)
                        {
                            // A catcher pad floating in a gap: falls become second chances.
                            int x = r.Range(3, sx / 2 - 2), z = r.Range(3, (Images == 4 ? sz / 2 : sz) - 4);
                            int y = baseTop - r.Range(2, 5);
                            if (y < 3) continue;
                            if (!Free(x - 1, y, z - 1, 4, Math.Min(12, sy - y - 2), 4, true, true)) continue;
                            bool nearPad = false;
                            foreach (var p in pads)
                                if (Dist(x + 1, z + 1, Clamp(x + 1, p.X, p.X + p.W), Clamp(z + 1, p.Z, p.Z + p.D)) < 5 && p.Top - y <= 10) { nearPad = true; break; }
                            if (!nearPad) continue;
                            Box(x, y, z, 2, 1, 2, "bouncy");
                            Reserve(x, y + 1, z, 2, 10, 2, true, true);
                            break;
                        }
                        else
                        {
                            // A launch pad on a pad.
                            var p = r.Pick(pads);
                            if (p.W < 5 || p.D < 5) continue;
                            int x = p.X + 1 + r.Range(0, p.W - 3), z = p.Z + 1 + r.Range(0, p.D - 3);
                            if (p.Top + 12 >= sy) continue;
                            var under = g.At(x, p.Top - 1, z);
                            if (under == null || g.At(x + 1, p.Top - 1, z + 1) == null) continue;
                            if (!Free(x, p.Top, z, 2, 10, 2, true, true)) continue;
                            if (NearSpawn(x + 0.5, z + 0.5, p.Top, 3.0)) continue;
                            Box(x, p.Top, z, 2, 1, 2, "bouncy");
                            Reserve(x, p.Top + 1, z, 2, 9, 2, true, true);
                            break;
                        }
                    }
                }
            }

            void PlaceProps()
            {
                int n = (int)Math.Round(th.PropDensity * (1.5f + chaos * 5f));
                if (Images == 2) n = n * 3 / 2;
                var weighted = new List<Pad>();
                foreach (var p in pads) if (p.W >= 3 && p.D >= 3) weighted.Add(p);
                if (weighted.Count == 0) return;
                for (int i = 0; i < n; i++)
                {
                    string kind = r.Pick(th.Props);
                    for (int attempt = 0; attempt < 12; attempt++)
                    {
                        var p = weighted[r.Range(0, weighted.Count)];
                        int x = p.X + r.Range(0, p.W), z = p.Z + r.Range(0, p.D);
                        int stack = kind == PropKinds.Crate && r.Chance(0.35f) ? r.Range(2, 4) : 1;
                        if (TryProp(kind, x, p.Top, z, stack)) break;
                    }
                }
            }

            bool TryProp(string kind, int x, int y, int z, int stack)
            {
                var size = PropKinds.Get(kind).Size;
                int need = (int)Math.Ceiling(size.y * stack) + 1;
                if (!Free(x, y, z, stack, need, 1, true, true)) return false;
                // Balls and steel crates are wider than a cell: their neighbours must be open too.
                if (Math.Max(size.x, size.z) > 1.05f && !Free(x - 1, y, z - 1, stack + 2, need, 3, false, false)) return false;
                for (int xx = x; xx < x + stack; xx++)
                {
                    var b = g.At(xx, y - 1, z);
                    if (b == null || BlockTypes.Get(b.type).DamagePerSecond > 0f || BlockTypes.Get(b.type).BounceSpeed > 0f) return false;
                }
                double keepAway = kind == PropKinds.Barrel ? 4.5 : 2.5;
                for (int xx = x; xx < x + stack; xx++)
                    if (NearSpawn(xx, z, y, keepAway)) return false;
                float yaw = r.Range(0f, 90f);
                double cx = x + stack * 0.5;
                for (int level = 0; level < stack; level++)
                {
                    int cnt = stack - level;
                    for (int i = 0; i < cnt; i++)
                    {
                        double px = cx + (i - (cnt - 1) * 0.5) * 1.02 * size.x;
                        AddProp(kind, px, y + level * size.y, z + 0.5, stack > 1 ? 0f : yaw);
                    }
                }
                Reserve(x, y, z, stack, need, 1, true, false);
                return true;
            }

            void AddProp(string kind, double x, double y, double z, float yaw)
            {
                for (int k = 0; k < Images; k++)
                {
                    MapPoint(k, x, z, out double px, out double pz);
                    m.props.Add(new PropData(kind, (float)px, (float)y, (float)pz, (yaw + k * 90f) % 360f));
                }
            }
        }
    }
}
