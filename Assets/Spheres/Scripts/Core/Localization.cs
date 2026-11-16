using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Translations. The English text is the key: <c>Loc.T("Play")</c> returns the current language's
    /// text, or the English when there is none. UIKit widgets translate their labels automatically, so
    /// most screens need no changes. Built-in tables live in LocTables.cs; community translations can be
    /// dropped in as <c>*.spheresloc.json</c> files in any content folder:
    /// <code>{ "language": "es", "name": "Español", "entries": [ { "en": "Play", "text": "Jugar" } ] }</code>
    /// </summary>
    public static class Loc
    {
        [Serializable] public class Entry { public string en; public string text; }
        [Serializable] public class FileData { public string language; public string name; public List<Entry> entries = new List<Entry>(); }

        public class Language
        {
            public string Code, Name;
            public readonly Dictionary<string, string> Table = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        static readonly Dictionary<string, Language> languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);
        static readonly List<Language> ordered = new List<Language>();
        static Language current;
        static bool loaded;

        /// <summary>Raised when the language changes (cached text should be rebuilt).</summary>
        public static event Action Changed;

        public static IReadOnlyList<Language> Languages { get { EnsureLoaded(); return ordered; } }
        public static string CurrentCode => current != null ? current.Code : "en";

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Get("en", "English");
            try { LocTables.Fill(Add); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static Language Get(string code, string name = null)
        {
            if (!languages.TryGetValue(code, out var l))
            {
                l = new Language { Code = code, Name = name ?? code };
                languages[code] = l;
                ordered.Add(l);
            }
            else if (!string.IsNullOrEmpty(name)) l.Name = name;
            return l;
        }

        /// <summary>Add one translation (built-in tables, mods).</summary>
        public static void Add(string code, string english, string translated)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(english) || string.IsNullOrEmpty(translated)) return;
            EnsureLoaded();
            Get(code).Table[english] = translated;
        }

        /// <summary>Name a language for the Options menu (e.g. "es" → "Español").</summary>
        public static void Name(string code, string name) { EnsureLoaded(); Get(code, name); }

        /// <summary>Load a *.spheresloc.json file. Returns the number of entries.</summary>
        public static int LoadFile(string path)
        {
            var data = JsonUtility.FromJson<FileData>(File.ReadAllText(path));
            if (data == null || string.IsNullOrEmpty(data.language) || data.entries == null) return 0;
            EnsureLoaded();
            var l = Get(data.language, data.name);
            int n = 0;
            foreach (var e in data.entries)
                if (e != null && !string.IsNullOrEmpty(e.en) && !string.IsNullOrEmpty(e.text)) { l.Table[e.en] = e.text; n++; }
            return n;
        }

        /// <summary>Pick the language by code ("" = follow the system language).</summary>
        public static void SetLanguage(string code)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(code)) code = SystemCode();
            current = languages.TryGetValue(code, out var l) ? l : languages["en"];
            Changed?.Invoke();
        }

        public static string SystemCode()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Turkish: return "tr";
                default: return "en";
            }
        }

        /// <summary>Translate (returns the input when there is no translation).</summary>
        public static string T(string english)
        {
            if (current == null || string.IsNullOrEmpty(english) || current.Code == "en") return english;
            return current.Table.TryGetValue(english, out var t) ? t : english;
        }

        /// <summary>Translate a format string, then fill it: <c>Loc.F("Round {0}", n)</c>.</summary>
        public static string F(string english, params object[] args)
        {
            try { return string.Format(T(english), args); }
            catch (FormatException) { return string.Format(english, args); }
        }

        /// <summary>Translate every option of a list (cycle widgets).</summary>
        public static string[] T(string[] options)
        {
            if (current == null || current.Code == "en" || options == null) return options;
            var r = new string[options.Length];
            for (int i = 0; i < options.Length; i++) r[i] = T(options[i]);
            return r;
        }
    }
}
