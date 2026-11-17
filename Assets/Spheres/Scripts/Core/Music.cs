using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Spheres
{
    /// <summary>
    /// The soundtrack player. Each part of the game has its own playlist (menus, battles, card picks, the map
    /// editor, match end). Built-in songs are composed on a worker thread the first time they're needed, a few
    /// are kept in memory, and battles get a new song every round. Your own .ogg/.wav/.mp3 files in
    /// &lt;user data&gt;/Music/&lt;battle|menu|pick|victory|editor&gt; join the rotation.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public const int Rate = Composer.Rate;

        /// <summary>Mods can force a clip for a state: (state) => clip, or null to use the playlist.</summary>
        public static Func<GameState, AudioClip> TrackOverride;

        public class Entry
        {
            public string Name;
            public MusicMood Mood;
            public SongDef Def;
            public AudioClip Clip;
            public bool Custom;
            public float LastUsed;
            public bool Queued;
        }

        static readonly List<Entry> entries = new List<Entry>();
        public static IReadOnlyList<Entry> Entries => entries;
        public static Entry NowPlaying { get; private set; }
        public static MusicPlayer Instance { get; private set; }
        public static string CustomFolder => Path.Combine(Application.persistentDataPath, "Music");

        // Worker thread: renders queued songs one at a time.
        static readonly Queue<Entry> renderQueue = new Queue<Entry>();
        static readonly Queue<(Entry e, float[] data)> finished = new Queue<(Entry, float[])>();
        static readonly object gate = new object();
        static Thread worker;

        AudioSource a, b;
        Entry current, pending;
        MusicMood? mood;
        float fade = 1f, lastTime;
        int loops;
        readonly Dictionary<MusicMood, List<Entry>> shuffled = new Dictionary<MusicMood, List<Entry>>();
        int roundsSeen = -1;
        const int MaxBuiltInClips = 6;

        /// <summary>Register your own clip (mods): it joins the playlist for that part of the game.</summary>
        public static void AddClip(MusicMood mood, string name, AudioClip clip)
        {
            if (clip == null) return;
            entries.Add(new Entry { Name = name ?? clip.name, Mood = mood, Clip = clip, Custom = true });
        }

        /// <summary>Register another procedural song (mods).</summary>
        public static void AddSong(SongDef def)
        {
            if (def != null) entries.Add(new Entry { Name = def.Name, Mood = def.Mood, Def = def });
        }

        void Awake()
        {
            Instance = this;
            a = gameObject.AddComponent<AudioSource>();
            b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
            }
            if (entries.Count == 0)
                foreach (var def in Soundtrack.Songs) entries.Add(new Entry { Name = def.Name, Mood = def.Mood, Def = def });
            if (worker == null)
            {
                worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Spheres music" };
                worker.Start();
            }
            // The first menu song first, so there's music almost immediately.
            var first = entries.FirstOrDefault(e => e.Mood == MusicMood.Menu && e.Def != null);
            if (first != null) Queue(first);
            StartCoroutine(LoadCustomMusic());
        }

        static void WorkerLoop()
        {
            while (true)
            {
                Entry next = null;
                lock (gate)
                {
                    while (renderQueue.Count == 0) Monitor.Wait(gate);
                    next = renderQueue.Dequeue();
                }
                float[] data = null;
                try { data = Composer.Render(next.Def); }
                catch (Exception) { data = null; }
                lock (gate) finished.Enqueue((next, data));
            }
        }

        static void Queue(Entry e)
        {
            if (e == null || e.Clip != null || e.Queued || e.Def == null) return;
            e.Queued = true;
            lock (gate)
            {
                renderQueue.Enqueue(e);
                Monitor.Pulse(gate);
            }
        }

        static MusicMood MoodFor(GameState s)
        {
            switch (s)
            {
                case GameState.RoundIntro:
                case GameState.Fighting:
                case GameState.RoundEnd: return MusicMood.Battle;
                case GameState.CardPick: return MusicMood.Pick;
                case GameState.MatchEnd: return MusicMood.Victory;
                case GameState.Editor: return MusicMood.Editor;
                default: return MusicMood.Menu;
            }
        }

        bool Allowed(Entry e)
        {
            int src = Options.Current.musicSource; // 0 both, 1 Spheres only, 2 yours only
            if (src == 1 && e.Custom) return false;
            if (src == 2 && !e.Custom) return entries.All(x => !x.Custom || x.Mood != e.Mood); // fall back if you have none for this part
            return true;
        }

        /// <summary>Next song for a part of the game, shuffled without repeats.</summary>
        Entry NextFor(MusicMood m, Entry avoid)
        {
            if (!shuffled.TryGetValue(m, out var list) || list.Count == 0)
            {
                list = entries.Where(e => e.Mood == m && Allowed(e)).OrderBy(_ => UnityEngine.Random.value).ToList();
                if (list.Count > 1 && list[0] == avoid) { list.Add(list[0]); list.RemoveAt(0); }
                shuffled[m] = list;
            }
            if (list.Count == 0) return null;
            var e = list[0];
            list.RemoveAt(0);
            return e;
        }

        /// <summary>Skip to another song (Options button, mods).</summary>
        public void Skip()
        {
            if (mood.HasValue) Choose(mood.Value);
        }

        void Choose(MusicMood m)
        {
            var next = NextFor(m, current);
            if (next == null) return;
            pending = next;
            Queue(next);
            // Warm up whatever will be needed next.
            var peek = shuffled.TryGetValue(m, out var l) && l.Count > 0 ? l[0] : null;
            Queue(peek);
            if (m == MusicMood.Menu) QueueFirst(MusicMood.Battle);
            if (m == MusicMood.Battle) { QueueFirst(MusicMood.Pick); QueueFirst(MusicMood.Victory); }
            if (m == MusicMood.Pick) QueueFirst(MusicMood.Battle);
        }

        void QueueFirst(MusicMood m)
        {
            if (!shuffled.TryGetValue(m, out var list) || list.Count == 0)
            {
                list = entries.Where(e => e.Mood == m && Allowed(e)).OrderBy(_ => UnityEngine.Random.value).ToList();
                shuffled[m] = list;
            }
            if (list.Count > 0) Queue(list[0]);
        }

        void Update()
        {
            // Turn finished renders into clips (main thread only).
            while (true)
            {
                (Entry e, float[] data) item;
                lock (gate)
                {
                    if (finished.Count == 0) break;
                    item = finished.Dequeue();
                }
                item.e.Queued = false;
                if (item.data == null) continue;
                var clip = AudioClip.Create("Spheres - " + item.e.Name, item.data.Length, 1, Rate, false);
                clip.SetData(item.data, 0);
                item.e.Clip = clip;
                TrimCache();
            }

            var gm = GameManager.Instance;
            if (gm == null) return;
            var state = gm.State;
            var m = MoodFor(state);

            // A new song when the part of the game changes, every new round of a match, or after two loops.
            int rounds = gm.InMatch ? gm.TeamScores.Sum() : -1;
            bool newRound = false;
            if (m == MusicMood.Battle && state == GameState.RoundIntro)
            {
                newRound = roundsSeen >= 0 && rounds != roundsSeen;
                roundsSeen = rounds;
            }
            if (mood != m || newRound || (loops >= 2 && pending == null))
            {
                mood = m;
                loops = 0;
                Choose(m);
            }
            if (!gm.InMatch) roundsSeen = -1;

            var overrideClip = TrackOverride?.Invoke(state);
            if (overrideClip != null)
            {
                if (a.clip != overrideClip) SwapTo(overrideClip, null);
            }
            else if (pending != null && pending.Clip != null)
            {
                SwapTo(pending.Clip, pending);
                pending = null;
            }

            // Count loops of the playing song.
            if (a.isPlaying)
            {
                if (a.time < lastTime - 1f) loops++;
                lastTime = a.time;
            }

            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.6f);
            float vol = gm.Settings.musicVolume * (gm.Paused ? 0.4f : 1f) * (state == GameState.Editor ? 0.6f : 1f);
            a.volume = vol * fade;
            b.volume = vol * (1f - fade);
            if (b.volume <= 0.001f && b.isPlaying) b.Stop();
        }

        void SwapTo(AudioClip clip, Entry e)
        {
            if (a.clip == clip && a.isPlaying) return;
            var t = a; a = b; b = t;
            a.clip = clip;
            a.time = 0f;
            a.Play();
            fade = 0f;
            lastTime = 0f;
            loops = 0;
            if (e != null)
            {
                current = e;
                e.LastUsed = Time.unscaledTime;
                NowPlaying = e;
                if (Options.Current.showNowPlaying)
                    Toasts.Show("Now playing", e.Name + (e.Custom ? "  (your music)" : ""), new Color(0.6f, 0.75f, 1f));
            }
        }

        /// <summary>Keep only a few built-in songs in memory; they're re-rendered if needed again.</summary>
        void TrimCache()
        {
            var loaded = entries.Where(e => !e.Custom && e.Clip != null && e != current && e != pending && e.Clip != a.clip && e.Clip != b.clip)
                .OrderBy(e => e.LastUsed).ToList();
            int total = entries.Count(e => !e.Custom && e.Clip != null);
            for (int i = 0; i < loaded.Count && total > MaxBuiltInClips; i++)
            {
                // Don't evict something queued up as "next" in a playlist.
                if (shuffled.Values.Any(l => l.Count > 0 && l[0] == loaded[i])) continue;
                Destroy(loaded[i].Clip);
                loaded[i].Clip = null;
                total--;
            }
        }

        // ---------------------------------------------------------------- your own music

        IEnumerator LoadCustomMusic()
        {
            string root = CustomFolder;
            try
            {
                foreach (var sub in new[] { "battle", "menu", "pick", "victory", "editor" })
                    Directory.CreateDirectory(Path.Combine(root, sub));
                string readme = Path.Combine(root, "README.txt");
                if (!File.Exists(readme))
                    File.WriteAllText(readme,
                        "Put your own music here (.ogg, .wav or .mp3) and it joins Spheres' playlists:\r\n" +
                        "  battle  - during fights\r\n  menu    - menus and lobby\r\n  pick    - card picks\r\n" +
                        "  victory - match end / podium\r\n  editor  - the map editor\r\n" +
                        "Restart the game after adding files. Options > Audio can switch to only your music.\r\n");
            }
            catch (Exception e) { Debug.LogWarning("[Spheres] Music folder: " + e.Message); }

            var folders = new List<string> { root };
            try { folders.AddRange(ContentLoader.Folders.Where(Directory.Exists)); }
            catch (Exception) { }
            foreach (var folder in folders.Distinct())
            {
                string[] files;
                try { files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories); }
                catch (Exception) { continue; }
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    AudioType type = ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".wav" ? AudioType.WAV : ext == ".mp3" ? AudioType.MPEG : AudioType.UNKNOWN;
                    if (type == AudioType.UNKNOWN) continue;
                    // Inside content folders only files under a "music" folder count (so mods' sound effects don't).
                    string norm = file.Replace('\\', '/').ToLowerInvariant();
                    if (folder != root && !norm.Contains("/music/")) continue;
                    var mood = MoodFromPath(norm);
                    using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, type))
                    {
                        ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;
                        yield return req.SendWebRequest();
                        if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("[Spheres] Couldn't load music " + file + ": " + req.error); continue; }
                        var clip = DownloadHandlerAudioClip.GetContent(req);
                        if (clip == null) continue;
                        clip.name = Path.GetFileNameWithoutExtension(file);
                        AddClip(mood, clip.name, clip);
                    }
                }
            }
            shuffled.Clear();
        }

        static MusicMood MoodFromPath(string path)
        {
            if (path.Contains("/menu/")) return MusicMood.Menu;
            if (path.Contains("/pick/")) return MusicMood.Pick;
            if (path.Contains("/victory/")) return MusicMood.Victory;
            if (path.Contains("/editor/")) return MusicMood.Editor;
            return MusicMood.Battle;
        }
    }
}
