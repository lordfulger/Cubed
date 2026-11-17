using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Spheres.EditorTools
{
    /// <summary>
    /// Keeps a project that was cloned from GitHub up to date: checks for new commits when Unity opens and
    /// offers to pull them (Spheres > Update from GitHub does it on demand). Uses Git for Windows if it's on
    /// PATH, otherwise the copy of git bundled with GitHub Desktop.
    /// </summary>
    [InitializeOnLoad]
    public static class SpheresGitSync
    {
        const string AutoKey = "spheres.git.autocheck";
        const string SessionKey = "spheres.git.checked";

        static SpheresGitSync()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionKey, false) || !EditorPrefs.GetBool(AutoKey, true)) return;
                SessionState.SetBool(SessionKey, true);
                if (Application.isBatchMode || !IsRepo()) return;
                Check(false);
            };
        }

        [MenuItem("Spheres/Update from GitHub", priority = 20)]
        public static void UpdateMenu()
        {
            if (!IsRepo())
            {
                EditorUtility.DisplayDialog("Spheres: not linked to GitHub",
                    "This project folder isn't a GitHub clone, so it can't update itself.\n\n" +
                    "Clone https://github.com/lordfulger/Spheres with GitHub Desktop (File > Clone repository) and open that folder in Unity Hub instead.",
                    "OK");
                return;
            }
            Check(true);
        }

        [MenuItem("Spheres/Check for updates when Unity opens", priority = 21)]
        static void ToggleAuto() => EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, true));

        [MenuItem("Spheres/Check for updates when Unity opens", true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked("Spheres/Check for updates when Unity opens", EditorPrefs.GetBool(AutoKey, true));
            return true;
        }

        static string Root => Path.GetDirectoryName(Application.dataPath);
        static bool IsRepo() => Directory.Exists(Path.Combine(Root, ".git")) || File.Exists(Path.Combine(Root, ".git"));

        static void Check(bool interactive)
        {
            string git = FindGit();
            if (git == null)
            {
                if (interactive)
                    EditorUtility.DisplayDialog("Spheres: git not found",
                        "Install GitHub Desktop (desktop.github.com) or Git for Windows (git-scm.com), then try again.\n\n" +
                        "You can also update with GitHub Desktop: open it and click 'Fetch origin', then 'Pull origin'.", "OK");
                return;
            }
            if (Run(git, "fetch --quiet", out string err) != 0)
            {
                if (interactive) EditorUtility.DisplayDialog("Spheres: couldn't reach GitHub", err, "OK");
                else Debug.LogWarning("[Spheres] Update check failed: " + err);
                return;
            }
            Run(git, "rev-list --count HEAD..@{u}", out string countText);
            int.TryParse(countText.Trim(), out int behind);
            if (behind <= 0)
            {
                if (interactive) EditorUtility.DisplayDialog("Spheres", "You're up to date.", "OK");
                return;
            }
            Run(git, "log --format=%s -n 8 HEAD..@{u}", out string log);
            if (!EditorUtility.DisplayDialog("Spheres: update available",
                    behind + " new change(s) on GitHub:\n\n" + log.Trim() + "\n\nUpdate now? (Stop Play mode first.)", "Update", "Later"))
                return;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            // Local changes Unity makes (settings, .meta files) are stashed and put back around the pull.
            int code = Run(git, "pull --ff-only --autostash", out string output);
            if (code != 0)
            {
                EditorUtility.DisplayDialog("Spheres: update failed",
                    "Git said:\n\n" + output + "\n\nOpen GitHub Desktop to see what's in the way (usually a file you edited that also changed on GitHub).", "OK");
                return;
            }
            Debug.Log("[Spheres] Updated from GitHub:\n" + output);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Spheres", "Updated! Unity is reloading the new code.", "OK");
        }

        static string FindGit()
        {
            if (Run("git", "--version", out _) == 0) return "git";
            try
            {
                // GitHub Desktop ships its own git: %LOCALAPPDATA%\GitHubDesktop\app-x.y.z\resources\app\git\cmd\git.exe
                string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");
                if (Directory.Exists(desktop))
                {
                    string best = null;
                    foreach (var dir in Directory.GetDirectories(desktop, "app-*"))
                    {
                        string exe = Path.Combine(dir, "resources", "app", "git", "cmd", "git.exe");
                        if (File.Exists(exe) && (best == null || string.CompareOrdinal(dir, best) > 0)) best = exe;
                    }
                    if (best != null) return best;
                }
            }
            catch (Exception) { }
            foreach (var p in new[] { @"C:\Program Files\Git\cmd\git.exe", "/usr/bin/git", "/opt/homebrew/bin/git", "/usr/local/bin/git" })
                if (File.Exists(p)) return p;
            return null;
        }

        static int Run(string exe, string args, out string output)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = Root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(60000)) { try { p.Kill(); } catch (Exception) { } output = "timed out"; return -1; }
                    output = (o + "\n" + e).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }
    }
}
