using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>An axis-aligned box of blocks occupying cells [x, x+w) × [y, y+h) × [z, z+d).</summary>
    [Serializable]
    public class BlockData
    {
        public int x, y, z;
        public int w = 1, h = 1, d = 1;
        public string type = "solid";

        public BlockData() { }

        public BlockData(int x, int y, int z, int w, int h, int d, string type)
        {
            this.x = x; this.y = y; this.z = z;
            this.w = w; this.h = h; this.d = d;
            this.type = type;
        }

        public int W => Mathf.Max(1, w);
        public int H => Mathf.Max(1, h);
        public int D => Mathf.Max(1, d);
        public Vector3 Center => new Vector3(x + W * 0.5f, y + H * 0.5f, z + D * 0.5f);
        public Vector3 Size => new Vector3(W, H, D);

        public bool Contains(int cx, int cy, int cz) =>
            cx >= x && cx < x + W && cy >= y && cy < y + H && cz >= z && cz < z + D;

        public BlockData Clone() => new BlockData(x, y, z, w, h, d, type);
    }

    /// <summary>A spawn cell. Players stand on the bottom of the cell. team = -1 means any team.</summary>
    [Serializable]
    public class SpawnData
    {
        public int x, y, z;
        public int team = -1;

        public SpawnData() { }
        public SpawnData(int x, int y, int z, int team) { this.x = x; this.y = y; this.z = z; this.team = team; }

        public Vector3 Position => new Vector3(x + 0.5f, y + 0.02f, z + 0.5f);
        public SpawnData Clone() => new SpawnData(x, y, z, team);
    }

    /// <summary>
    /// A map: a box arena (0,0,0)–(sizeX,sizeY,sizeZ) whose faces are the damaging borders,
    /// filled with blocks. Serialized as JSON (*.cubedmap.json).
    /// </summary>
    [Serializable]
    public class MapData
    {
        public int formatVersion = 1;
        public string name = "New Map";
        public string author = "";
        public string description = "";
        public int sizeX = 40, sizeY = 32, sizeZ = 40;
        /// <summary>Background color as hex, e.g. "#1b2233". Empty = default.</summary>
        public string background = "";
        public List<BlockData> blocks = new List<BlockData>();
        public List<SpawnData> spawns = new List<SpawnData>();

        public Bounds Bounds => new Bounds(new Vector3(sizeX, sizeY, sizeZ) * 0.5f, new Vector3(sizeX, sizeY, sizeZ));

        public void Validate()
        {
            if (string.IsNullOrEmpty(name)) name = "Unnamed";
            if (author == null) author = "";
            if (description == null) description = "";
            if (background == null) background = "";
            sizeX = Mathf.Clamp(sizeX, 8, 256);
            sizeY = Mathf.Clamp(sizeY, 8, 128);
            sizeZ = Mathf.Clamp(sizeZ, 8, 256);
            if (blocks == null) blocks = new List<BlockData>();
            if (spawns == null) spawns = new List<SpawnData>();
            blocks.RemoveAll(b => b == null);
            spawns.RemoveAll(s => s == null);
            foreach (var b in blocks)
            {
                if (b.w <= 0) b.w = 1;
                if (b.h <= 0) b.h = 1;
                if (b.d <= 0) b.d = 1;
                if (string.IsNullOrEmpty(b.type)) b.type = "solid";
            }
        }

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        public static MapData FromJson(string json)
        {
            var m = JsonUtility.FromJson<MapData>(json);
            if (m == null) return null;
            m.Validate();
            return m;
        }

        public MapData Clone() => FromJson(ToJson(false));

        public BlockData BlockAt(int cx, int cy, int cz)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
                if (blocks[i].Contains(cx, cy, cz)) return blocks[i];
            return null;
        }

        // Builder helpers used by the built-in maps and handy for code mods.
        public MapData Box(int x, int y, int z, int w, int h, int d, string type = "solid")
        {
            blocks.Add(new BlockData(x, y, z, w, h, d, type));
            return this;
        }

        public MapData Spawn(int x, int y, int z, int team)
        {
            spawns.Add(new SpawnData(x, y, z, team));
            return this;
        }

        /// <summary>Adds a box and its mirror across the X center line.</summary>
        public MapData BoxMirrorX(int x, int y, int z, int w, int h, int d, string type = "solid")
        {
            Box(x, y, z, w, h, d, type);
            int mx = sizeX - x - w;
            if (mx != x) Box(mx, y, z, w, h, d, type);
            return this;
        }

        /// <summary>Adds a box mirrored into all four quadrants (X and Z).</summary>
        public MapData BoxMirror4(int x, int y, int z, int w, int h, int d, string type = "solid")
        {
            int mx = sizeX - x - w, mz = sizeZ - z - d;
            Box(x, y, z, w, h, d, type);
            if (mx != x) Box(mx, y, z, w, h, d, type);
            if (mz != z) Box(x, y, mz, w, h, d, type);
            if (mx != x && mz != z) Box(mx, y, mz, w, h, d, type);
            return this;
        }
    }
}
