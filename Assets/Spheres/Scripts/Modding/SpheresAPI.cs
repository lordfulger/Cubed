using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>Information a code mod registers about itself (shown on the Mods screen).</summary>
    public class ModInfo
    {
        public string Guid;
        public string Name;
        public string Version;
        public string Author;
        public string Description;
    }

    /// <summary>
    /// The stable entry point for mods (BepInEx plugins). Everything here is safe to call from a
    /// plugin's Awake(), before the game has finished booting.
    /// </summary>
    /// <example>
    /// <code>
    /// [BepInPlugin("you.mycards", "My Cards", "1.0.0")]
    /// public class MyPlugin : BaseUnityPlugin {
    ///     void Awake() {
    ///         SpheresAPI.RegisterMod("you.mycards", "My Cards", "1.0.0", "you");
    ///         SpheresAPI.RegisterCard(new CardDefinition("you.sniper", "Sniper", "Huge damage", Rarity.Rare, Color.cyan)
    ///             .Mul(Stat.Damage, 3f).Mul(Stat.AttackCooldown, 2f));
    ///     }
    /// }
    /// </code>
    /// </example>
    public static class SpheresAPI
    {
        /// <summary>Bumped when the modding surface changes. Compare against this in your plugin.</summary>
        public const string ApiVersion = "1.0.0";
        public const string GameVersion = "0.1.0";

        static readonly List<ModInfo> mods = new List<ModInfo>();
        static readonly List<(string label, Action action)> menuButtons = new List<(string, Action)>();
        static readonly List<Action> guiDrawers = new List<Action>();

        public static IReadOnlyList<ModInfo> Mods => mods;
        internal static IReadOnlyList<(string label, Action action)> MenuButtons => menuButtons;
        internal static IReadOnlyList<Action> GuiDrawers => guiDrawers;

        // ---- mods

        public static void RegisterMod(string guid, string name, string version, string author = "", string description = "")
        {
            mods.RemoveAll(m => m.Guid == guid);
            mods.Add(new ModInfo { Guid = guid, Name = name, Version = version, Author = author, Description = description });
            Debug.Log("[Spheres] Mod registered: " + name + " " + version);
        }

        // ---- cards

        public static void RegisterCard(CardDefinition card) => CardRegistry.Register(card);
        public static bool RemoveCard(string id) => CardRegistry.Unregister(id);
        public static CardDefinition GetCard(string id) => CardRegistry.Get(id);
        public static IReadOnlyList<CardDefinition> AllCards => CardRegistry.All;

        public static void RegisterEffect(string id, Func<CardEffect> factory, string description = "") =>
            CardEffectRegistry.Register(id, factory, description);

        public static void RegisterEffect<T>(string id, string description = "") where T : CardEffect, new() =>
            CardEffectRegistry.Register<T>(id, description);

        // ---- maps & blocks

        public static void RegisterMap(string id, MapData map, string source = "Mod") => MapRegistry.Register(id, map, source);
        public static void RegisterBlockType(BlockType type) => BlockTypes.Register(type);

        /// <summary>Scan an extra folder for *.spherescards.json / *.spheresmap.json / *.spheresblocks.json and load it now.</summary>
        public static void LoadContentFolder(string folder)
        {
            ContentLoader.AddFolder(folder);
            if (GameManager.Instance != null) ContentLoader.LoadAll();
        }

        // ---- stats

        public static void SetBaseStat(Stat stat, float value) => StatInfo.SetBase(stat, value);
        public static void SetStatLimits(Stat stat, float min, float max) => StatInfo.SetLimits(stat, min, max);

        // ---- game state

        public static GameManager Game => GameManager.Instance;
        public static MatchSettings Settings => GameManager.Instance != null ? GameManager.Instance.Settings : null;
        public static IReadOnlyList<Player> Players => Player.All;
        public static MapRuntime Map => GameManager.ActiveMap;

        // ---- UI

        /// <summary>Add a button to the main menu (e.g. to open your mod's settings).</summary>
        public static void AddMenuButton(string label, Action onClick)
        {
            if (!string.IsNullOrEmpty(label) && onClick != null) menuButtons.Add((label, onClick));
        }

        /// <summary>Draw your own IMGUI on top of the game every frame. Runs inside OnGUI.</summary>
        public static void AddGuiDrawer(Action drawer)
        {
            if (drawer != null) guiDrawers.Add(drawer);
        }

        public static void RemoveGuiDrawer(Action drawer) => guiDrawers.Remove(drawer);

        // ---- sound

        public static void RegisterSound(string id, AudioClip clip) => Sfx.Register(id, clip);
        public static void PlaySound(string id, float volume = 1f) => Sfx.Play(id, volume);
    }
}
