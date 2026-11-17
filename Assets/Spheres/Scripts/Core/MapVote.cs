using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    /// <summary>"Players vote" map rotation: three maps are offered during each card-pick phase.</summary>
    public partial class GameManager
    {
        /// <summary>Map ids on the ballot (empty when no vote is running).</summary>
        public readonly List<string> VoteMaps = new List<string>();
        /// <summary>Display names for the ballot (clients may not have every map).</summary>
        public readonly List<string> VoteNames = new List<string>();
        /// <summary>Player index → ballot choice.</summary>
        public readonly Dictionary<int, int> Votes = new Dictionary<int, int>();
        readonly Dictionary<int, float> botVoteAt = new Dictionary<int, float>();
        /// <summary>Raised on the host whenever the ballot or a vote changes (NetSession broadcasts it).</summary>
        public event System.Action VoteChanged;

        public bool VoteRunning => VoteMaps.Count > 0;

        void StartMapVote()
        {
            VoteMaps.Clear();
            VoteNames.Clear();
            Votes.Clear();
            botVoteAt.Clear();
            if (Settings.mapRotation != MapRotation.Vote || editorTestMap != null) return;
            var all = MapRegistry.All.Where(e => e.Id != CurrentMapId).ToList();
            if (all.Count == 0) all = MapRegistry.All.ToList();
            foreach (var e in all.OrderBy(_ => Random.value).Take(3))
            {
                VoteMaps.Add(e.Id);
                VoteNames.Add(e.Data.name);
            }
            foreach (var p in Players)
                if (p.IsBot) botVoteAt[p.Index] = Random.Range(1.5f, 6f);
            VoteChanged?.Invoke();
        }

        /// <summary>Cast or change a vote (host; clients send it through NetSession).</summary>
        public void CastVote(Player p, int choice)
        {
            if (p == null || !VoteRunning) return;
            if (NetClientMode)
            {
                Spheres.Net.NetSession.Instance?.SendMapVote(p.Index, choice);
                Votes[p.Index] = choice; // optimistic, corrected by the host's tally
                return;
            }
            choice = Mathf.Clamp(choice, 0, VoteMaps.Count - 1);
            if (Votes.TryGetValue(p.Index, out int old) && old == choice) return;
            Votes[p.Index] = choice;
            Sfx.Play("deal", 0.4f, 0f);
            VoteChanged?.Invoke();
        }

        /// <summary>Host: bot votes and local input during the pick phase.</summary>
        void UpdateMapVote(float dt)
        {
            if (!VoteRunning) return;
            foreach (var key in botVoteAt.Keys.ToList())
            {
                botVoteAt[key] -= dt;
                if (botVoteAt[key] > 0f) continue;
                botVoteAt.Remove(key);
                var bot = Players.FirstOrDefault(p => p.Index == key);
                if (bot != null) CastVote(bot, Random.Range(0, VoteMaps.Count));
            }
        }

        /// <summary>Local humans: 1/2/3 on keyboard, Y / Triangle cycles on a gamepad.</summary>
        void UpdateLocalVoteInput()
        {
            if (!VoteRunning || State != GameState.CardPick) return;
            foreach (var p in Players)
            {
                if (!(p.Input is HumanInput h)) continue;
                if (h.IsKeyboard)
                {
                    for (int i = 0; i < VoteMaps.Count; i++)
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) CastVote(p, i);
                }
                else
                {
                    bool y = Controllers.Held(h.Joystick, Controllers.Profile(h.Joystick), PadButton.North);
                    bool was = votePadPrev.Contains(h.Joystick);
                    if (y && !was)
                    {
                        int cur = Votes.TryGetValue(p.Index, out int c) ? c : -1;
                        CastVote(p, (cur + 1) % VoteMaps.Count);
                    }
                    if (y) votePadPrev.Add(h.Joystick); else votePadPrev.Remove(h.Joystick);
                }
            }
        }

        readonly HashSet<int> votePadPrev = new HashSet<int>();

        /// <summary>Host: the winning map id (ties broken randomly), or null when no vote ran.</summary>
        string ResolveMapVote()
        {
            if (!VoteRunning) return null;
            var tally = new int[VoteMaps.Count];
            foreach (var v in Votes.Values) if (v >= 0 && v < tally.Length) tally[v]++;
            int best = tally.Max();
            var tied = Enumerable.Range(0, tally.Length).Where(i => tally[i] == best).ToList();
            string id = VoteMaps[tied[Random.Range(0, tied.Count)]];
            string name = VoteNames[VoteMaps.IndexOf(id)];
            VoteMaps.Clear();
            VoteNames.Clear();
            Votes.Clear();
            VoteChanged?.Invoke();
            SpheresHud.AddFeedText("Next map: " + name + (best > 0 ? " (" + best + " vote" + (best == 1 ? "" : "s") + ")" : ""));
            return id;
        }

        /// <summary>Clients: the host's ballot.</summary>
        public void ClientSetVote(List<string> ids, List<string> names, Dictionary<int, int> votes)
        {
            VoteMaps.Clear(); VoteMaps.AddRange(ids);
            VoteNames.Clear(); VoteNames.AddRange(names);
            Votes.Clear();
            foreach (var kv in votes) Votes[kv.Key] = kv.Value;
        }
    }

    /// <summary>Top-down schematic thumbnails of maps, generated on the CPU and cached.</summary>
    public static class MapThumbnails
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string mapId)
        {
            var e = MapRegistry.Get(mapId);
            return e != null ? Get(e.Data, mapId) : null;
        }

        public static Texture2D Get(MapData m, string key = null)
        {
            if (m == null) return null;
            bool mystery = MapGenerator.IsRandom(key);
            key = key ?? m.name;
            key += "|" + m.blocks.Count + "|" + m.mechs.Count + "|" + m.sizeX + "x" + m.sizeZ;
            if (cache.TryGetValue(key, out var tex) && tex != null) return tex;
            tex = Render(m);
            if (mystery) QuestionMark(tex);
            cache[key] = tex;
            return tex;
        }

        static readonly string[] QuestionGlyph = { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." };

        /// <summary>Random Arena: dim the sample map and stamp a big "?" over it (a new map is made every round).</summary>
        static void QuestionMark(Texture2D tex)
        {
            int res = tex.width;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32((byte)(px[i].r * 0.45f), (byte)(px[i].g * 0.45f), (byte)(px[i].b * 0.45f), 255);
            int cell = res / 11, gw = 5 * cell, gh = 7 * cell;
            int x0 = (res - gw) / 2, y0 = (res - gh) / 2;
            var shadow = new Color32(10, 12, 20, 255);
            var ink = new Color32(255, 214, 92, 255);
            for (int pass = 0; pass < 2; pass++)
            {
                int off = pass == 0 ? 3 : 0;
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                    {
                        if (QuestionGlyph[gy][gx] != '#') continue;
                        // Texture rows run bottom-up.
                        int bx = x0 + gx * cell + off, by = y0 + (6 - gy) * cell - off;
                        for (int y = by; y < by + cell; y++)
                            for (int x = bx; x < bx + cell; x++)
                                if (x >= 0 && y >= 0 && x < res && y < res) px[y * res + x] = pass == 0 ? shadow : ink;
                    }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
        }

        public static void Clear()
        {
            foreach (var t in cache.Values) if (t != null) Object.Destroy(t);
            cache.Clear();
        }

        static Texture2D Render(MapData m)
        {
            const int res = 128;
            int sx = Mathf.Max(1, m.sizeX), sz = Mathf.Max(1, m.sizeZ);
            float scale = (res - 8f) / Mathf.Max(sx, sz);
            int ox = Mathf.RoundToInt((res - sx * scale) * 0.5f), oz = Mathf.RoundToInt((res - sz * scale) * 0.5f);
            var px = new Color32[res * res];
            var height = new float[res * res];
            Color bg = new Color(0.07f, 0.08f, 0.12f);
            if (!string.IsNullOrEmpty(m.background) && ColorUtility.TryParseHtmlString(m.background, out var b)) bg = b;
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % res, y = i / res;
                float v = 0.75f + 0.25f * (y / (float)res);
                px[i] = new Color(bg.r * v, bg.g * v, bg.b * v, 1f);
                height[i] = -1f;
            }

            void Paint(float x0, float z0, float x1, float z1, float top, Color c)
            {
                int ax = Mathf.Clamp(Mathf.FloorToInt(ox + x0 * scale), 0, res - 1), bx = Mathf.Clamp(Mathf.CeilToInt(ox + x1 * scale), 0, res);
                int az = Mathf.Clamp(Mathf.FloorToInt(oz + z0 * scale), 0, res - 1), bz = Mathf.Clamp(Mathf.CeilToInt(oz + z1 * scale), 0, res);
                float shade = Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(top / Mathf.Max(1f, m.sizeY * 0.7f)));
                for (int z = az; z < bz; z++)
                    for (int x = ax; x < bx; x++)
                    {
                        int i = z * res + x;
                        if (top < height[i]) continue;
                        height[i] = top;
                        bool edge = x == ax || z == az || x == bx - 1 || z == bz - 1;
                        float s = shade * (edge ? 0.8f : 1f);
                        px[i] = new Color(Mathf.Clamp01(c.r * s), Mathf.Clamp01(c.g * s), Mathf.Clamp01(c.b * s), 1f);
                    }
            }

            foreach (var blk in m.blocks)
            {
                var t = BlockTypes.Get(blk.type);
                var c = t.Color;
                if (t.Transparent) c = Color.Lerp(c, Color.white, 0.3f);
                Paint(blk.x, blk.z, blk.x + blk.W, blk.z + blk.D, blk.y + blk.H, c);
            }
            foreach (var mech in m.mechs)
            {
                var c = mech.deadly ? new Color(1f, 0.35f, 0.3f) : new Color(0.4f, 0.85f, 1f);
                if (mech.kind == MechKinds.Pendulum) Paint(mech.x - 0.5f, mech.z - 0.5f, mech.x + 1.5f, mech.z + 1.5f, m.sizeY, c);
                else Paint(mech.x, mech.z, mech.x + mech.Size.x, mech.z + mech.Size.z, mech.y + mech.Size.y + 0.5f, c);
            }
            foreach (var p in m.props)
            {
                var k = PropKinds.Get(p.kind);
                Paint(p.x - 0.4f, p.z - 0.4f, p.x + 0.4f, p.z + 0.4f, p.y + k.Size.y, k.Color);
            }
            foreach (var s in m.spawns)
            {
                var c = s.team >= 0 && s.team < Teams.Max ? Teams.GetColor(s.team) : Color.white;
                Paint(s.x, s.z, s.x + 1f, s.z + 1f, 999f, c);
            }

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MapThumb_" + m.name };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
