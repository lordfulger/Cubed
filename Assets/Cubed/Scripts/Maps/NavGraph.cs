using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// Walkable-surface graph for bots, built from a map's block data: walking/stepping edges,
    /// jump-up edges (≤2 cells), drop-down edges, gap jumps (≤4 cells) and bounce-pad launches.
    /// </summary>
    public class NavGraph
    {
        public class Node
        {
            public int Id;
            public Vector3 Pos;
            public readonly List<Edge> Edges = new List<Edge>();
        }

        public struct Edge
        {
            public int To;
            public float Cost;
            /// <summary>Needs a jump (step up or gap). Drops and bounces don't.</summary>
            public bool Jump;
            /// <summary>Leaves solid ground on purpose (drop, gap, bounce): bots must not edge-guard.</summary>
            public bool LeavesGround;
        }

        public readonly List<Node> Nodes = new List<Node>();
        readonly Dictionary<int, int> byCell = new Dictionary<int, int>();
        int sx, sy, sz;
        byte[] occ; // 0 empty, 1 solid, 2 hazard, 3 bouncy

        static MapRuntime cachedMap;
        static NavGraph cached;

        /// <summary>Graph for the active map (built on first use, cached until the map changes).</summary>
        public static NavGraph For(MapRuntime map)
        {
            if (map == null) return null;
            if (cachedMap == map && cached != null) return cached;
            cachedMap = map;
            cached = Build(map.Data);
            return cached;
        }

        int Index(int x, int y, int z) => x + sx * (y + sy * z);
        byte At(int x, int y, int z) => x < 0 || y < 0 || z < 0 || x >= sx || y >= sy || z >= sz ? (byte)0 : occ[Index(x, y, z)];

        bool Walkable(int x, int y, int z)
        {
            if (y < 1 || x < 0 || z < 0 || x >= sx || z >= sz || y >= sy) return false;
            byte below = At(x, y - 1, z);
            return (below == 1 || below == 3) && At(x, y, z) == 0 && At(x, y + 1, z) == 0;
        }

        int NodeAt(int x, int y, int z) => byCell.TryGetValue(Index(x, y, z), out int id) ? id : -1;

        public static NavGraph Build(MapData data)
        {
            var g = new NavGraph { sx = data.sizeX, sy = data.sizeY, sz = data.sizeZ };
            long cells = (long)g.sx * g.sy * g.sz;
            if (cells > 6_000_000) return g; // too big; bots fall back to steering
            g.occ = new byte[cells];
            foreach (var b in data.blocks)
            {
                var type = BlockTypes.Get(b.type);
                byte v = type.DamagePerSecond > 0f ? (byte)2 : type.BounceSpeed > 0f ? (byte)3 : (byte)1;
                for (int x = Mathf.Max(0, b.x); x < Mathf.Min(g.sx, b.x + b.W); x++)
                    for (int y = Mathf.Max(0, b.y); y < Mathf.Min(g.sy, b.y + b.H); y++)
                        for (int z = Mathf.Max(0, b.z); z < Mathf.Min(g.sz, b.z + b.D); z++)
                            g.occ[g.Index(x, y, z)] = v;
            }

            for (int z = 0; z < g.sz; z++)
                for (int y = 1; y < g.sy - 1; y++)
                    for (int x = 0; x < g.sx; x++)
                        if (g.Walkable(x, y, z))
                        {
                            var n = new Node { Id = g.Nodes.Count, Pos = new Vector3(x + 0.5f, y, z + 0.5f) };
                            g.byCell[g.Index(x, y, z)] = n.Id;
                            g.Nodes.Add(n);
                        }

            int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
            foreach (var n in g.Nodes)
            {
                int x = Mathf.FloorToInt(n.Pos.x), y = Mathf.RoundToInt(n.Pos.y), z = Mathf.FloorToInt(n.Pos.z);
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + dx8[d], nz = z + dz8[d];
                    bool diag = d >= 4;
                    if (diag && (!g.Walkable(x + dx8[d], y, z) || !g.Walkable(x, y, z + dz8[d]))) continue;
                    float baseCost = diag ? 1.414f : 1f;
                    bool linked = false;
                    foreach (int dy in new[] { 0, -1, 1, 2 })
                    {
                        int to = g.NodeAt(nx, y + dy, nz);
                        if (to < 0) continue;
                        // Need headroom above our own cell to jump up.
                        if (dy > 0 && (g.At(x, y + 2, z) != 0 || (dy == 2 && g.At(x, y + 3, z) != 0))) continue;
                        n.Edges.Add(new Edge { To = to, Cost = baseCost + (dy > 0 ? dy * 0.8f : 0f), Jump = dy > 0 });
                        linked = true;
                        break;
                    }
                    if (linked || diag) continue;

                    // Drop down an edge (up to 10 cells).
                    for (int dy = 2; dy <= 10; dy++)
                    {
                        if (g.At(nx, y - dy + 1, nz) != 0 && dy > 2) break;
                        int to = g.NodeAt(nx, y - dy, nz);
                        if (to < 0) continue;
                        n.Edges.Add(new Edge { To = to, Cost = 1.5f + dy * 0.3f, LeavesGround = true });
                        break;
                    }

                    // Jump across a gap (2-4 cells), landing up to 1 higher or 2 lower.
                    for (int dist = 2; dist <= 4; dist++)
                    {
                        int gx = x + dx8[d] * dist, gz = z + dz8[d] * dist;
                        int found = -1;
                        foreach (int dy in new[] { 0, -1, 1, -2 })
                        {
                            found = g.NodeAt(gx, y + dy, gz);
                            if (found >= 0) break;
                        }
                        if (found < 0) continue;
                        bool clear = true;
                        for (int k = 1; k < dist && clear; k++)
                            if (g.At(x + dx8[d] * k, y, z + dz8[d] * k) != 0 || g.At(x + dx8[d] * k, y + 1, z + dz8[d] * k) != 0) clear = false;
                        if (clear) n.Edges.Add(new Edge { To = found, Cost = dist + 1.5f, Jump = true, LeavesGround = true });
                        break;
                    }
                }

                // Bounce pads launch you up and onto higher ground nearby.
                if (g.At(x, y - 1, z) == 3)
                {
                    int added = 0;
                    foreach (var m in g.Nodes)
                    {
                        float dyUp = m.Pos.y - n.Pos.y;
                        if (dyUp < 3f || dyUp > 11f) continue;
                        float h = new Vector2(m.Pos.x - n.Pos.x, m.Pos.z - n.Pos.z).magnitude;
                        if (h > 7f) continue;
                        n.Edges.Add(new Edge { To = m.Id, Cost = 4f + h * 0.5f, LeavesGround = true });
                        if (++added >= 24) break;
                    }
                }
            }
            return g;
        }

        public int Nearest(Vector3 p, float maxDistance = 6f)
        {
            int best = -1;
            float bestD = maxDistance * maxDistance;
            foreach (var n in Nodes)
            {
                Vector3 d = n.Pos - p;
                d.y *= 2f; // prefer nodes at our own height
                float sq = d.sqrMagnitude;
                if (sq < bestD) { bestD = sq; best = n.Id; }
            }
            return best;
        }

        /// <summary>A* path from a world position to another. Returns null when unreachable.</summary>
        public List<(Vector3 pos, Edge via)> FindPath(Vector3 from, Vector3 to)
        {
            int start = Nearest(from), goal = Nearest(to, 10f);
            if (start < 0 || goal < 0) return null;
            if (start == goal) return new List<(Vector3, Edge)>();
            var gScore = new Dictionary<int, float> { [start] = 0f };
            var came = new Dictionary<int, (int from, Edge edge)>();
            var open = new MinHeap();
            Vector3 goalPos = Nodes[goal].Pos;
            open.Push(start, Vector3.Distance(Nodes[start].Pos, goalPos));
            int expanded = 0;
            while (open.Count > 0 && expanded++ < 6000)
            {
                int cur = open.Pop();
                if (cur == goal) break;
                float gc = gScore[cur];
                foreach (var e in Nodes[cur].Edges)
                {
                    float ng = gc + e.Cost;
                    if (gScore.TryGetValue(e.To, out float old) && old <= ng) continue;
                    gScore[e.To] = ng;
                    came[e.To] = (cur, e);
                    open.Push(e.To, ng + Vector3.Distance(Nodes[e.To].Pos, goalPos));
                }
            }
            if (!came.ContainsKey(goal)) return null;
            var path = new List<(Vector3, Edge)>();
            for (int c = goal; c != start; c = came[c].from) path.Add((Nodes[c].Pos, came[c].edge));
            path.Reverse();
            return path;
        }

        class MinHeap
        {
            readonly List<(int id, float pri)> items = new List<(int, float)>();
            public int Count => items.Count;

            public void Push(int id, float pri)
            {
                items.Add((id, pri));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (items[p].pri <= items[i].pri) break;
                    (items[p], items[i]) = (items[i], items[p]);
                    i = p;
                }
            }

            public int Pop()
            {
                var top = items[0].id;
                items[0] = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < items.Count && items[l].pri < items[m].pri) m = l;
                    if (r < items.Count && items[r].pri < items[m].pri) m = r;
                    if (m == i) break;
                    (items[m], items[i]) = (items[i], items[m]);
                    i = m;
                }
                return top;
            }
        }
    }
}
