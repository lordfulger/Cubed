using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Press F8 (or Pause > Report a bug) to save a bug report folder: a screenshot, this session's log, the game's
    /// state (mode, map, players and their cards) and settings. Zip the folder and send it with a short note.
    /// </summary>
    public class BugReport : MonoBehaviour
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "BugReports");
        static BugReport instance;
        float cooldown;

        public static void Ensure(GameObject host)
        {
            if (instance == null && host != null) instance = host.AddComponent<BugReport>();
        }

        void Update()
        {
            cooldown -= Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.F8) && cooldown <= 0f) Capture();
        }

        public static void Capture()
        {
            if (instance == null) return;
            instance.cooldown = 1f;
            string dir = Path.Combine(Folder, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            try
            {
                Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screenshot.png"));
                // This session's log (all messages, warnings and errors with stack traces).
                if (!string.IsNullOrEmpty(CrashLog.CurrentLog) && File.Exists(CrashLog.CurrentLog))
                    File.Copy(CrashLog.CurrentLog, Path.Combine(dir, "session.log"), true);
                // Unity's own player log, when there is one.
                try
                {
                    string unityLog = Application.consoleLogPath;
                    if (!string.IsNullOrEmpty(unityLog) && File.Exists(unityLog))
                        using (var src = new FileStream(unityLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var dst = File.Create(Path.Combine(dir, "unity.log")))
                            src.CopyTo(dst);
                }
                catch (Exception) { }
                File.WriteAllText(Path.Combine(dir, "info.txt"), Describe());
                File.WriteAllText(Path.Combine(dir, "note.txt"), "Describe what happened here (what you did, what you expected, what you saw):\r\n\r\n");
                Toasts.Show("Bug report saved", "Folder: " + dir + "  (opened for you - zip it and send it)", new Color(0.5f, 0.85f, 1f));
                instance.StartCoroutine(OpenSoon(dir));
            }
            catch (Exception e)
            {
                Toasts.Show("Couldn't save bug report", e.Message, UIKit.Bad);
                Debug.LogException(e);
            }
        }

        static System.Collections.IEnumerator OpenSoon(string dir)
        {
            yield return new WaitForSecondsRealtime(0.6f); // let the screenshot finish writing
            FolderOpener.Open(dir);
        }

        static string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Spheres bug report " + DateTime.Now.ToString("u"));
            sb.AppendLine("Game version: " + Application.version + " (" + SpheresAPI.GameVersion + ")");
            sb.AppendLine("Unity: " + Application.unityVersion + "  Platform: " + Application.platform + "  Editor: " + Application.isEditor);
            sb.AppendLine("OS: " + SystemInfo.operatingSystem);
            sb.AppendLine("CPU: " + SystemInfo.processorType + " x" + SystemInfo.processorCount + "  RAM: " + SystemInfo.systemMemorySize + " MB");
            sb.AppendLine("GPU: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ", " + SystemInfo.graphicsMemorySize + " MB)");
            sb.AppendLine("Screen: " + Screen.width + "x" + Screen.height + " (" + Screen.currentResolution + ")  FPS now: " + Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)));
            var gm = GameManager.Instance;
            if (gm != null)
            {
                sb.AppendLine();
                sb.AppendLine("State: " + gm.State + "  Mode: " + gm.Settings.mode + "  Map: " + (gm.Map != null ? gm.Map.Data.name : "-") + " (" + gm.CurrentMapId + ")");
                sb.AppendLine("Round display: " + gm.RoundDisplay + "  Round time: " + gm.RoundTime.ToString("0.0") + "  Online: " + (gm.NetClientMode ? "client" : gm.Net != null && gm.Net.IsHosting ? "host" : "offline"));
                sb.AppendLine("Scores: " + string.Join(", ", gm.ActiveTeams.Select(t => Teams.GetName(t) + " " + gm.TeamScores[t])));
                foreach (var p in gm.Players)
                {
                    if (p == null) continue;
                    sb.AppendLine("  " + p.DisplayName + " team " + p.HomeTeam + (p.IsBot ? " (bot)" : "") + "  HP " + Mathf.RoundToInt(p.Health) + "/" + Mathf.RoundToInt(p.MaxHealth)
                        + (p.IsAlive ? "" : " DEAD") + "  pos " + p.transform.position + "  cards: " + string.Join(", ", p.Cards.Select(c => c.Id)));
                }
                sb.AppendLine();
                sb.AppendLine("Match settings: " + JsonUtility.ToJson(gm.Settings));
            }
            sb.AppendLine("Options: " + JsonUtility.ToJson(Options.Current));
            sb.AppendLine("Mods: " + string.Join(", ", SpheresAPI.Mods.Select(m => m.Name + " " + m.Version)) + "  Content packs: " + ContentLoader.Sources.Count);
            return sb.ToString();
        }
    }
}
