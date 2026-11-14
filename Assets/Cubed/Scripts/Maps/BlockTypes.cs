using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>Behaviour of a kind of block. Register new ones with <see cref="BlockTypes.Register"/>.</summary>
    public class BlockType
    {
        public string Id;
        public string Name;
        public Color Color = Color.gray;
        /// <summary>Upward launch speed when landed on. 0 = not bouncy.</summary>
        public float BounceSpeed;
        /// <summary>Damage per second while touching. 0 = harmless.</summary>
        public float DamagePerSecond;
        /// <summary>Ground acceleration multiplier (ice &lt; 1).</summary>
        public float GroundAccel = 1f;
        public bool Destructible;
        public float Health = 60f;
        public bool Transparent;
        /// <summary>Blocks the editor palette shows this type (hide internal types with false).</summary>
        public bool ShowInEditor = true;

        public BlockType(string id, string name, Color color)
        {
            Id = id;
            Name = name;
            Color = color;
        }
    }

    public static class BlockTypes
    {
        static readonly Dictionary<string, BlockType> byId = new Dictionary<string, BlockType>();
        static readonly List<BlockType> ordered = new List<BlockType>();

        public static readonly BlockType Solid;

        static BlockTypes()
        {
            Solid = new BlockType("solid", "Stone", new Color(0.42f, 0.45f, 0.53f));
            Register(Solid);
            Register(new BlockType("light", "Pale Stone", new Color(0.74f, 0.76f, 0.82f)));
            Register(new BlockType("dark", "Basalt", new Color(0.2f, 0.22f, 0.28f)));
            Register(new BlockType("bouncy", "Bouncer", new Color(0.95f, 0.42f, 0.85f)) { BounceSpeed = 24f });
            Register(new BlockType("hazard", "Lava", new Color(1f, 0.38f, 0.1f)) { DamagePerSecond = 40f });
            Register(new BlockType("ice", "Ice", new Color(0.68f, 0.9f, 1f)) { GroundAccel = 0.15f });
            Register(new BlockType("glass", "Glass", new Color(0.7f, 0.92f, 1f, 0.35f)) { Destructible = true, Health = 40f, Transparent = true });
            Register(new BlockType("crate", "Crate", new Color(0.66f, 0.46f, 0.26f)) { Destructible = true, Health = 90f });
            Register(new BlockType("grass", "Moss", new Color(0.36f, 0.62f, 0.36f)));
        }

        public static IReadOnlyList<BlockType> All => ordered;

        public static void Register(BlockType type)
        {
            if (type == null || string.IsNullOrEmpty(type.Id)) return;
            if (byId.TryGetValue(type.Id, out var old)) ordered[ordered.IndexOf(old)] = type;
            else ordered.Add(type);
            byId[type.Id] = type;
        }

        public static BlockType Get(string id) =>
            id != null && byId.TryGetValue(id, out var t) ? t : Solid;

        public static bool Exists(string id) => id != null && byId.ContainsKey(id);
    }
}
