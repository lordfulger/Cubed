using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
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
    /// filled with blocks. Serialized as JSON (*.spheresmap.json).
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
        /// <summary>Moving parts: doors, platforms, spinners, swinging spikes, saws, spike traps, pistons.</summary>
        public List<MechData> mechs = new List<MechData>();
        /// <summary>Loose physics props: crates, boxes, explosive barrels, beach balls.</summary>
        public List<PropData> props = new List<PropData>();

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
            if (mechs == null) mechs = new List<MechData>();
            if (props == null) props = new List<PropData>();
            blocks.RemoveAll(b => b == null);
            spawns.RemoveAll(s => s == null);
            mechs.RemoveAll(m => m == null || string.IsNullOrEmpty(m.kind));
            props.RemoveAll(p => p == null);
            if (mechs.Count > 256) mechs.RemoveRange(256, mechs.Count - 256);
            if (props.Count > 400) props.RemoveRange(400, props.Count - 400);
            foreach (var m in mechs)
            {
                m.w = Mathf.Clamp(m.w, 1, 64); m.h = Mathf.Clamp(m.h, 1, 64); m.d = Mathf.Clamp(m.d, 1, 64);
                m.axis = Mathf.Clamp(m.axis, 0, 2);
                m.length = Mathf.Clamp(m.length, 1f, 40f);
                m.damage = Mathf.Clamp(m.damage, 0f, 500f);
                if (string.IsNullOrEmpty(m.type)) m.type = "dark";
            }
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

        /// <summary>Adds a moving part (see <see cref="MechKinds.Preset"/> for defaults).</summary>
        public MapData Mech(MechData m)
        {
            mechs.Add(m);
            return this;
        }

        /// <summary>Adds a moving part from a preset and lets the caller tweak it.</summary>
        public MapData Mech(string kind, int x, int y, int z, System.Action<MechData> tweak = null)
        {
            var m = MechKinds.Preset(kind, x, y, z);
            tweak?.Invoke(m);
            mechs.Add(m);
            return this;
        }

        /// <summary>Adds a physics prop standing on (x, y, z), where y is the floor height.</summary>
        public MapData Prop(string kind, float x, float y, float z, float yaw = 0f)
        {
            props.Add(new PropData(kind, x, y, z, yaw));
            return this;
        }

        /// <summary>Adds a prop and its mirrors into all four quadrants.</summary>
        public MapData PropMirror4(string kind, float x, float y, float z)
        {
            float mx = sizeX - x, mz = sizeZ - z;
            Prop(kind, x, y, z);
            if (Mathf.Abs(mx - x) > 0.01f) Prop(kind, mx, y, z, 90f);
            if (Mathf.Abs(mz - z) > 0.01f) Prop(kind, x, y, mz, 180f);
            if (Mathf.Abs(mx - x) > 0.01f && Mathf.Abs(mz - z) > 0.01f) Prop(kind, mx, y, mz, 270f);
            return this;
        }

        /// <summary>A neat stack of props (a pyramid of crates, a pile of boxes).</summary>
        public MapData PropStack(string kind, float x, float y, float z, int baseCount, float spacing = 1.05f)
        {
            var k = PropKinds.Get(kind);
            for (int level = 0; level < baseCount; level++)
            {
                int n = baseCount - level;
                for (int i = 0; i < n; i++)
                    Prop(kind, x + (i - (n - 1) * 0.5f) * spacing * k.Size.x, y + level * k.Size.y, z);
            }
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
