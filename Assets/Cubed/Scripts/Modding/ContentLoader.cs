using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Cubed
{
    // JSON DTOs (JsonUtility cannot read enums from strings, so these use strings and are converted).
    [Serializable]
    public class CardModifierJson
    {
        public string stat;
        public string op = "Add";
        public float value;
    }

    [Serializable]
    public class CardJson
    {
        public string id;
        public string title;
        public string description;
        public string rarity = "Common";
        public string color = "#9999aa";
        public int maxStacks;
        public float weight = 1f;
        public CardModifierJson[] modifiers;
        public string[] effects;
        public string[] tags;
    }

    [Serializable]
    public class CardPackJson
    {
        public string author;
        public CardJson[] cards;
    }

    [Serializable]
    public class BlockTypeJson
    {
        public string id;
        public string name;
        public string color = "#808080";
        public float bounceSpeed;
        public float damagePerSecond;
        public float groundAccel = 1f;
        public bool destructible;
        public float health = 60f;
        public bool transparent;
    }

    [Serializable]
    public class BlockPackJson
    {
        public BlockTypeJson[] blocks;
    }

    /// <summary>Summary of where content came from (shown on the Mods screen).</summary>
    public class ContentSource
    {
        public string Folder;
        public int Cards, Maps, Blocks;
        public readonly List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Loads data-only mods: *.cubedcards.json, *.cubedblocks.json and *.cubedmap.json files found in
    /// StreamingAssets/Content, the user's content folder, and every BepInEx plugin folder
    /// (so Thunderstore/r2modman packages with no DLL work too).
    /// </summary>
    public static class ContentLoader
    {
        public const string CardExt = ".cubedcards.json";
        public const string BlockExt = ".cubedblocks.json";
        public const string MapExt = MapRegistry.Extension;

        public static readonly List<ContentSource> Sources = new List<ContentSource>();
        static readonly List<string> extraFolders = new List<string>();

        public static string UserContentFolder => Path.Combine(Application.persistentDataPath, "Content");

        /// <summary>Additional folders to scan (e.g. a code mod's own folder). Takes effect on next reload.</summary>
        public static void AddFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder) && !extraFolders.Contains(folder)) extraFolders.Add(folder);
        }

        /// <summary>BepInEx's plugin folder, found by reflection so the game has no hard dependency on BepInEx.</summary>
        public static string BepInExPluginPath
        {
            get
            {
                try
                {
                    var paths = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("BepInEx.Paths", false))
                        .FirstOrDefault(t => t != null);
                    var prop = paths?.GetProperty("PluginPath", BindingFlags.Public | BindingFlags.Static);
                    if (prop?.GetValue(null) is string s && Directory.Exists(s)) return s;
                }
                catch (Exception e) { Debug.LogException(e); }
                string fallback = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "BepInEx", "plugins");
                return Directory.Exists(fallback) ? fallback : null;
            }
        }

        public static bool BepInExLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "BepInEx");

        public static IEnumerable<string> Folders
        {
            get
            {
                yield return Path.Combine(Application.streamingAssetsPath, "Content");
                yield return UserContentFolder;
                var bep = BepInExPluginPath;
                if (bep != null) yield return bep;
                foreach (var f in extraFolders) yield return f;
            }
        }

        public static void LoadAll()
        {
            Sources.Clear();
            try { Directory.CreateDirectory(UserContentFolder); }
            catch (Exception e) { Debug.LogException(e); }

            foreach (var folder in Folders.Distinct())
            {
                if (!Directory.Exists(folder)) continue;
                var src = new ContentSource { Folder = folder };
                // Blocks first so maps and cards can use them.
                foreach (var f in Find(folder, BlockExt)) Try(src, f, () => src.Blocks += LoadBlocks(f));
                foreach (var f in Find(folder, CardExt)) Try(src, f, () => src.Cards += LoadCards(f, SourceName(folder, f)));
                foreach (var f in Find(folder, MapExt)) Try(src, f, () => src.Maps += LoadMap(f, SourceName(folder, f)));
                if (src.Cards + src.Maps + src.Blocks > 0 || src.Errors.Count > 0) Sources.Add(src);
            }
            GameEvents.RaiseContentLoaded();
        }

        static IEnumerable<string> Find(string folder, string ext)
        {
            try { return Directory.GetFiles(folder, "*" + ext, SearchOption.AllDirectories); }
            catch (Exception e)
            {
                Debug.LogWarning("[Cubed] Can't scan " + folder + ": " + e.Message);
                return Array.Empty<string>();
            }
        }

        static void Try(ContentSource src, string file, Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                string msg = Path.GetFileName(file) + ": " + e.Message;
                src.Errors.Add(msg);
                Debug.LogWarning("[Cubed] " + msg);
            }
        }

        /// <summary>Use the top-level plugin folder name (e.g. "Author-ModName") as the content source.</summary>
        static string SourceName(string root, string file)
        {
            string rel = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int i = rel.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return i > 0 ? rel.Substring(0, i) : Path.GetFileNameWithoutExtension(file);
        }

        public static int LoadCards(string file, string source)
        {
            var pack = JsonUtility.FromJson<CardPackJson>(File.ReadAllText(file));
            if (pack?.cards == null) throw new Exception("no \"cards\" array");
            int n = 0;
            foreach (var cj in pack.cards)
            {
                var card = FromJson(cj, string.IsNullOrEmpty(pack.author) ? source : pack.author);
                if (card == null) continue;
                CardRegistry.Register(card);
                n++;
            }
            return n;
        }

        public static CardDefinition FromJson(CardJson cj, string source)
        {
            if (cj == null || string.IsNullOrEmpty(cj.id)) return null;
            var card = new CardDefinition
            {
                Id = cj.id,
                Title = string.IsNullOrEmpty(cj.title) ? cj.id : cj.title,
                Description = cj.description ?? "",
                MaxStacks = cj.maxStacks,
                WeightMultiplier = cj.weight <= 0f ? 1f : cj.weight,
                Source = source,
            };
            if (Enum.TryParse(cj.rarity ?? "Common", true, out Rarity r)) card.Rarity = r;
            if (!string.IsNullOrEmpty(cj.color) && ColorUtility.TryParseHtmlString(cj.color, out var c)) card.Color = c;
            if (cj.modifiers != null)
                foreach (var m in cj.modifiers)
                {
                    if (m == null || !Enum.TryParse(m.stat ?? "", true, out Stat stat))
                    {
                        Debug.LogWarning("[Cubed] Card " + cj.id + ": unknown stat '" + m?.stat + "'");
                        continue;
                    }
                    if (!Enum.TryParse(m.op ?? "Add", true, out StatOp op)) op = StatOp.Add;
                    card.Modifiers.Add(new StatModifier(stat, op, m.value));
                }
            if (cj.effects != null) card.Effects.AddRange(cj.effects.Where(e => !string.IsNullOrEmpty(e)));
            if (cj.tags != null) card.Tags.AddRange(cj.tags);
            return card;
        }

        public static int LoadBlocks(string file)
        {
            var pack = JsonUtility.FromJson<BlockPackJson>(File.ReadAllText(file));
            if (pack?.blocks == null) throw new Exception("no \"blocks\" array");
            int n = 0;
            foreach (var bj in pack.blocks)
            {
                if (bj == null || string.IsNullOrEmpty(bj.id)) continue;
                ColorUtility.TryParseHtmlString(bj.color ?? "#808080", out var c);
                BlockTypes.Register(new BlockType(bj.id, string.IsNullOrEmpty(bj.name) ? bj.id : bj.name, c)
                {
                    BounceSpeed = bj.bounceSpeed,
                    DamagePerSecond = bj.damagePerSecond,
                    GroundAccel = bj.groundAccel <= 0f ? 1f : bj.groundAccel,
                    Destructible = bj.destructible,
                    Health = bj.health,
                    Transparent = bj.transparent,
                });
                n++;
            }
            return n;
        }

        public static int LoadMap(string file, string source)
        {
            var data = MapRegistry.LoadFile(file);
            if (data == null) throw new Exception("invalid map JSON");
            MapRegistry.Register("mod:" + source + "/" + Path.GetFileName(file), data, source, null);
            return 1;
        }
    }
}
