using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    public class MapEntry
    {
        public string Id;
        public MapData Data;
        /// <summary>"Spheres" for built-ins, "User" for maps made in the editor, otherwise the mod name.</summary>
        public string Source;
        /// <summary>File path for user maps (null otherwise).</summary>
        public string FilePath;
        public bool IsUserMap => Source == "User";
    }

    /// <summary>All playable maps: built-in, user-made (editor) and mod-provided.</summary>
    public static class MapRegistry
    {
        public const string Extension = ".spheresmap.json";
        static readonly List<MapEntry> entries = new List<MapEntry>();
        static bool builtInsLoaded;

        public static string UserMapFolder => Path.Combine(Application.persistentDataPath, "maps");

        static void EnsureBuiltIns()
        {
            if (builtInsLoaded) return;
            builtInsLoaded = true;
            BuiltInMaps.RegisterAll();
        }

        public static IReadOnlyList<MapEntry> All
        {
            get { EnsureBuiltIns(); return entries; }
        }

        public static MapEntry Register(string id, MapData data, string source, string filePath = null)
        {
            EnsureBuiltIns();
            if (data == null || string.IsNullOrEmpty(id)) return null;
            data.Validate();
            var existing = entries.FirstOrDefault(e => e.Id == id);
            if (existing != null) entries.Remove(existing);
            var entry = new MapEntry { Id = id, Data = data, Source = source, FilePath = filePath };
            entries.Add(entry);
            return entry;
        }

        public static bool Unregister(string id)
        {
            EnsureBuiltIns();
            return entries.RemoveAll(e => e.Id == id) > 0;
        }

        public static MapEntry Get(string id)
        {
            EnsureBuiltIns();
            return entries.FirstOrDefault(e => e.Id == id);
        }

        /// <summary>Gets a map or falls back to the first one.</summary>
        public static MapEntry GetOrDefault(string id)
        {
            EnsureBuiltIns();
            return Get(id) ?? entries.FirstOrDefault();
        }

        // ------------------------------------------------------------------ user maps

        public static void RefreshUserMaps()
        {
            EnsureBuiltIns();
            entries.RemoveAll(e => e.IsUserMap);
            try
            {
                Directory.CreateDirectory(UserMapFolder);
                foreach (var file in Directory.GetFiles(UserMapFolder, "*" + Extension))
                {
                    var data = LoadFile(file);
                    if (data != null) Register("user:" + Path.GetFileName(file), data, "User", file);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static MapData LoadFile(string file)
        {
            try { return MapData.FromJson(File.ReadAllText(file)); }
            catch (Exception e)
            {
                Debug.LogWarning("[Spheres] Could not load map " + file + ": " + e.Message);
                return null;
            }
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "map";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim().Replace(' ', '_').ToLowerInvariant();
        }

        /// <summary>Saves a map to the user folder and returns its registry id.</summary>
        public static string SaveUserMap(MapData data)
        {
            data.Validate();
            Directory.CreateDirectory(UserMapFolder);
            string fileName = SanitizeFileName(data.name) + Extension;
            string path = Path.Combine(UserMapFolder, fileName);
            File.WriteAllText(path, data.ToJson(true));
            string id = "user:" + fileName;
            Register(id, data.Clone(), "User", path);
            return id;
        }

        public static bool DeleteUserMap(string id)
        {
            var e = Get(id);
            if (e == null || !e.IsUserMap || e.FilePath == null) return false;
            try { File.Delete(e.FilePath); }
            catch (Exception ex) { Debug.LogException(ex); return false; }
            entries.Remove(e);
            return true;
        }
    }
}
