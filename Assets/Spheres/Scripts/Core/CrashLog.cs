using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Spheres
{
    /// <summary>
    /// Writes a session log to persistentDataPath/logs (last 10 kept) and a separate crash report for
    /// every exception. Optionally uploads reports if <see cref="UploadUrl"/> is set (off by default).
    /// </summary>
    public static class CrashLog
    {
        /// <summary>Set to an HTTPS endpoint to receive crash reports as JSON POSTs. Empty = never upload.</summary>
        public static string UploadUrl = "";
        public static string Folder => Path.Combine(Application.persistentDataPath, "logs");
        public static string CurrentLog { get; private set; }

        static readonly object gate = new object();
        static StreamWriter writer;
        static float lastToast = -100f;
        static int reportsThisSession;
        static readonly Queue<string> recent = new Queue<string>();

        public static void Init()
        {
            if (writer != null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                var old = Directory.GetFiles(Folder, "session-*.log");
                Array.Sort(old);
                for (int i = 0; i < old.Length - 9; i++) File.Delete(old[i]);
                CurrentLog = Path.Combine(Folder, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                writer = new StreamWriter(CurrentLog, false, Encoding.UTF8) { AutoFlush = true };
                writer.WriteLine("Spheres " + SpheresAPI.GameVersion + " | " + Application.platform + " | " + SystemInfo.operatingSystem + " | " +
                                 SystemInfo.graphicsDeviceName + " | " + SystemInfo.systemMemorySize + " MB");
                Application.logMessageReceivedThreaded += OnLog;
            }
            catch (Exception e) { Debug.LogWarning("[Spheres] Could not open log file: " + e.Message); }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + type + "] " + message;
            lock (gate)
            {
                try
                {
                    writer?.WriteLine(line);
                    if (type == LogType.Exception || type == LogType.Error) writer?.WriteLine(stack);
                }
                catch (Exception) { }
                recent.Enqueue(line);
                while (recent.Count > 60) recent.Dequeue();
            }
            if (type != LogType.Exception) return;
            WriteReport(message, stack);
        }

        static void WriteReport(string message, string stack)
        {
            string body;
            lock (gate)
            {
                if (reportsThisSession >= 20) return;
                reportsThisSession++;
                var sb = new StringBuilder();
                sb.AppendLine("Spheres crash report " + DateTime.Now.ToString("u"));
                sb.AppendLine("Version: " + SpheresAPI.GameVersion + "  Platform: " + Application.platform + "  OS: " + SystemInfo.operatingSystem);
                sb.AppendLine("GPU: " + SystemInfo.graphicsDeviceName + "  RAM: " + SystemInfo.systemMemorySize + " MB");
                sb.AppendLine("Mods: " + string.Join(", ", SpheresAPI.Mods.Count > 0 ? ModNames() : new[] { "none" }));
                sb.AppendLine();
                sb.AppendLine(message);
                sb.AppendLine(stack);
                sb.AppendLine("--- recent log ---");
                foreach (var l in recent) sb.AppendLine(l);
                body = sb.ToString();
                try { File.WriteAllText(Path.Combine(Folder, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt"), body); }
                catch (Exception) { }
            }
            MainThread.Run(() =>
            {
                if (Time.unscaledTime - lastToast > 30f)
                {
                    lastToast = Time.unscaledTime;
                    Toasts.Show("Something went wrong", "A crash report was saved to the logs folder.", UIKit.Bad);
                }
                Upload(body);
            });
        }

        static string[] ModNames()
        {
            var list = new List<string>();
            foreach (var m in SpheresAPI.Mods) list.Add(m.Name + " " + m.Version);
            return list.ToArray();
        }

        static void Upload(string body)
        {
            if (string.IsNullOrEmpty(UploadUrl)) return;
            try
            {
                var req = new UnityWebRequest(UploadUrl, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Report { version = SpheresAPI.GameVersion, report = body }))),
                    downloadHandler = new DownloadHandlerBuffer(),
                };
                req.SetRequestHeader("Content-Type", "application/json");
                req.SendWebRequest();
            }
            catch (Exception) { }
        }

        [Serializable]
        class Report
        {
            public string version;
            public string report;
        }

        public static void OpenFolder() => Application.OpenURL("file://" + Folder);
    }

    /// <summary>Runs callbacks from background threads on the main thread.</summary>
    public class MainThread : MonoBehaviour
    {
        static readonly Queue<Action> queue = new Queue<Action>();
        static MainThread instance;

        public static void Run(Action a)
        {
            lock (queue) queue.Enqueue(a);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("MainThread");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MainThread>();
        }

        void Update()
        {
            while (true)
            {
                Action a;
                lock (queue)
                {
                    if (queue.Count == 0) return;
                    a = queue.Dequeue();
                }
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
