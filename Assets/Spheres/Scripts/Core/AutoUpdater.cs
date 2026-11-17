using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Spheres
{
    /// <summary>
    /// Built game (Windows): checks the latest GitHub release of this repository and offers a one-click update.
    /// The new zip is downloaded and unpacked to a staging folder; a small script waits for the game to close,
    /// copies the new files over the install folder and starts the game again.
    /// Releases are made with Spheres > Publish Update in the editor (it builds, zips and opens the release page).
    /// </summary>
    public class AutoUpdater : MonoBehaviour
    {
        /// <summary>GitHub "owner/repo" whose releases carry the game builds.</summary>
        public const string Repo = "lordfulger/Spheres";

        public enum UpdateState { Idle, Checking, Available, Downloading, Installing, UpToDate, Failed }

        public static AutoUpdater Instance { get; private set; }
        public UpdateState State { get; private set; } = UpdateState.Idle;
        public string LatestVersion { get; private set; } = "";
        public string Notes { get; private set; } = "";
        public string Error { get; private set; } = "";
        public float Progress { get; private set; }
        public bool Dismissed;

        string downloadUrl;
        long downloadSize;

        [Serializable] class ReleaseJson { public string tag_name; public string name; public string body; public bool draft; public bool prerelease; public AssetJson[] assets; }
        [Serializable] class AssetJson { public string name; public string browser_download_url; public long size; }

        /// <summary>Only installed Windows builds update themselves (the editor uses Spheres > Update from GitHub).</summary>
        public static bool Supported => !Application.isEditor && Application.platform == RuntimePlatform.WindowsPlayer;

        public static void Ensure(GameObject host)
        {
            if (Instance != null || host == null) return;
            Instance = host.AddComponent<AutoUpdater>();
        }

        void Start()
        {
            if (Supported && Options.Current.checkForUpdates) Check();
        }

        public void Check()
        {
            if (State == UpdateState.Checking || State == UpdateState.Downloading || State == UpdateState.Installing) return;
            StartCoroutine(CheckRoutine());
        }

        IEnumerator CheckRoutine()
        {
            State = UpdateState.Checking;
            Error = "";
            using (var req = UnityWebRequest.Get("https://api.github.com/repos/" + Repo + "/releases/latest"))
            {
                req.SetRequestHeader("User-Agent", "Spheres-Updater");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 404 = no releases yet; treat as up to date rather than an error.
                    State = req.responseCode == 404 ? UpdateState.UpToDate : UpdateState.Failed;
                    Error = req.error;
                    yield break;
                }
                ReleaseJson rel = null;
                try { rel = JsonUtility.FromJson<ReleaseJson>(req.downloadHandler.text); }
                catch (Exception e) { Error = e.Message; }
                if (rel == null || string.IsNullOrEmpty(rel.tag_name)) { State = UpdateState.Failed; yield break; }
                LatestVersion = rel.tag_name.TrimStart('v', 'V');
                Notes = rel.body ?? "";
                var asset = rel.assets?.FirstOrDefault(a => a != null && a.name != null && a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                                            && a.name.IndexOf("win", StringComparison.OrdinalIgnoreCase) >= 0)
                            ?? rel.assets?.FirstOrDefault(a => a != null && a.name != null && a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (asset == null || Compare(LatestVersion, Application.version) <= 0)
                {
                    State = UpdateState.UpToDate;
                    yield break;
                }
                downloadUrl = asset.browser_download_url;
                downloadSize = asset.size;
                State = UpdateState.Available;
                Toasts.Show("Update available", "Spheres " + LatestVersion + " is out (you have " + Application.version + "). Update from the main menu.", new Color(0.4f, 0.85f, 1f));
            }
        }

        /// <summary>Compare dotted version strings numerically ("0.1.10" > "0.1.9").</summary>
        public static int Compare(string a, string b)
        {
            int[] Parse(string s) => (s ?? "").Split('.', '-', '+').Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 0).ToArray();
            var x = Parse(a);
            var y = Parse(b);
            for (int i = 0; i < Mathf.Max(x.Length, y.Length); i++)
            {
                int xi = i < x.Length ? x[i] : 0, yi = i < y.Length ? y[i] : 0;
                if (xi != yi) return xi.CompareTo(yi);
            }
            return 0;
        }

        public void Install()
        {
            if (State != UpdateState.Available || string.IsNullOrEmpty(downloadUrl)) return;
            StartCoroutine(InstallRoutine());
        }

        IEnumerator InstallRoutine()
        {
            State = UpdateState.Downloading;
            Progress = 0f;
            string root = Path.Combine(Application.persistentDataPath, "Update");
            string zip = Path.Combine(root, "Spheres-" + LatestVersion + ".zip");
            string staging = Path.Combine(root, "staging");
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
            }
            catch (Exception e) { Fail("Couldn't prepare the update folder: " + e.Message); yield break; }

            using (var req = new UnityWebRequest(downloadUrl, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
                req.SetRequestHeader("User-Agent", "Spheres-Updater");
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    Progress = req.downloadProgress;
                    yield return null;
                }
                if (req.result != UnityWebRequest.Result.Success) { Fail("Download failed: " + req.error); yield break; }
            }

            State = UpdateState.Installing;
            string install = Path.GetDirectoryName(Application.dataPath); // folder containing Spheres.exe
            string exe;
            string source;
            try
            {
                ZipFile.ExtractToDirectory(zip, staging);
                // The zip may hold the files directly or inside one folder: find the one with the executable.
                var found = Directory.GetFiles(staging, "*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(f => !Path.GetFileName(f).StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase));
                if (found == null) { Fail("The update didn't contain the game."); yield break; }
                source = Path.GetDirectoryName(found);
                exe = Path.Combine(install, Path.GetFileName(found));
                string script = Path.Combine(root, "apply-update.cmd");
                int pid = Process.GetCurrentProcess().Id;
                File.WriteAllText(script,
                    "@echo off\r\n" +
                    "title Updating Spheres\r\n" +
                    "echo Updating Spheres to " + LatestVersion + "...\r\n" +
                    ":wait\r\n" +
                    "tasklist /FI \"PID eq " + pid + "\" 2>NUL | find \"" + pid + "\" >NUL\r\n" +
                    "if not errorlevel 1 (timeout /t 1 /nobreak >NUL & goto wait)\r\n" +
                    "robocopy \"" + source + "\" \"" + install + "\" /E /R:3 /W:1 /NFL /NDL /NJH /NJS /NP >NUL\r\n" +
                    "start \"\" \"" + exe + "\"\r\n" +
                    "rmdir /s /q \"" + staging + "\"\r\n" +
                    "del \"" + zip + "\"\r\n");
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = root,
                });
            }
            catch (Exception e) { Fail("Couldn't install the update: " + e.Message); yield break; }

            Debug.Log("[Spheres] Installing update " + LatestVersion + " and restarting.");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        void Fail(string message)
        {
            Error = message;
            State = UpdateState.Failed;
            Debug.LogWarning("[Spheres] Update: " + message);
            Toasts.Show("Update failed", message, UIKit.Bad);
        }

        /// <summary>Main-menu banner (drawn by SpheresUI).</summary>
        public static void DrawBanner(Rect r)
        {
            var u = Instance;
            if (u == null || u.Dismissed) return;
            if (u.State != UpdateState.Available && u.State != UpdateState.Downloading && u.State != UpdateState.Installing) return;
            UIKit.Fill(r, new Color(0.08f, 0.2f, 0.3f, 0.92f));
            UIKit.Outline(r, new Color(0.4f, 0.85f, 1f), 2f);
            var text = new Rect(r.x + 14f, r.y + 6f, r.width - 28f, 24f);
            if (u.State == UpdateState.Available)
            {
                UIKit.Text(text, "<b>Update available:</b> " + u.LatestVersion + "  (you have " + Application.version + ")", UIKit.Label, Color.white);
                string mb = u.downloadSize > 0 ? "  (" + Mathf.CeilToInt(u.downloadSize / (1024f * 1024f)) + " MB)" : "";
                if (UIKit.Btn(new Rect(r.x + 14f, r.yMax - 40f, 200f, 32f), "Update & restart" + mb, true)) u.Install();
                if (UIKit.Btn(new Rect(r.x + 224f, r.yMax - 40f, 100f, 32f), "Later", true)) u.Dismissed = true;
            }
            else
            {
                string s = u.State == UpdateState.Downloading ? "Downloading " + u.LatestVersion + "..." : "Installing - the game will restart...";
                UIKit.Text(text, s, UIKit.Label, Color.white);
                UIKit.Bar(new Rect(r.x + 14f, r.yMax - 28f, r.width - 28f, 10f), u.State == UpdateState.Installing ? 1f : u.Progress, new Color(0.4f, 0.85f, 1f), new Color(0f, 0f, 0f, 0.5f));
            }
        }
    }
}
