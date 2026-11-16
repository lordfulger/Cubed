using System;
using System.IO;
using Spheres.Automation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spheres.EditorTools
{
    /// <summary>
    /// Batchmode smoke test: plays every menu, the map editor, training and a bots-only match on every map
    /// in play mode, then exits with code 0 (no errors logged) or 1 (errors; see Builds/smoke-report.txt).
    ///
    ///   Unity.exe -batchmode -projectPath . -executeMethod Spheres.EditorTools.SmokeTest.Run -logFile -
    ///
    /// Do NOT pass -quit: the editor has to keep running to play the game; it exits by itself when done.
    /// Leave out -batchmode to watch it (and to cover the IMGUI drawing code, which batchmode never runs).
    /// Options: see <see cref="SmokeTestRunner"/> (e.g. -smokeSeconds 30 -smokeMaps builtin:garden).
    /// </summary>
    public static class SmokeTest
    {
        public static void Run() => AutomationLauncher.Launch(AutomationEntry.KindSmoke, "");

        [MenuItem("Spheres/Tests/Run Smoke Test", priority = 60)]
        static void RunMenu() => Run();

        [MenuItem("Spheres/Tests/Run Quick Smoke Test (15 s per map)", priority = 61)]
        static void RunQuickMenu() => AutomationLauncher.Launch(AutomationEntry.KindSmoke, "-smokeSeconds 15 -smokeModeSeconds 15");
    }

    /// <summary>
    /// Card balance simulator: many fast bot-vs-bot matches, writes Builds/balance-report.csv and .md.
    ///
    ///   Unity.exe -batchmode -projectPath . -executeMethod Spheres.EditorTools.BalanceSim.Run -balanceMatches 500 -logFile -
    ///
    /// Options: see <see cref="BalanceSimRunner"/>.
    /// </summary>
    public static class BalanceSim
    {
        public static void Run() => AutomationLauncher.Launch(AutomationEntry.KindBalance, "");

        [MenuItem("Spheres/Tests/Run Balance Simulator (100 matches)", priority = 62)]
        static void RunMenu() => AutomationLauncher.Launch(AutomationEntry.KindBalance, "-balanceMatches 100");
    }

    /// <summary>
    /// Enters play mode with an automation request stored in SessionState (which survives the domain reload
    /// that entering play mode causes). <see cref="AutomationEntry"/> picks the request up when the game
    /// boots; this class watches the run from the editor side and exits/stops play mode when it finishes.
    /// </summary>
    [InitializeOnLoad]
    public static class AutomationLauncher
    {
        const string KindKey = AutomationEntry.EditorSessionKey;
        const string StartKey = "spheres.automation.start";
        const string EnteredKey = "spheres.automation.entered";

        static AutomationLauncher()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <param name="extraArgs">Options (space separated) that override the command line, e.g. "-balanceMatches 100".</param>
        public static void Launch(string kind, string extraArgs)
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(KindKey, "")))
            {
                Debug.LogWarning("[SpheresAutomation] A run is already in progress.");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SpheresAutomation] Stop play mode before starting a test run.");
                if (Application.isBatchMode) EditorApplication.Exit(3);
                return;
            }
            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError("[SpheresAutomation] Scripts have compile errors; fix them first.");
                if (Application.isBatchMode) EditorApplication.Exit(3);
                return;
            }

            SpheresProjectSetup.PrepareForAutomation();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(SpheresProjectSetup.ScenePath))
                EditorSceneManager.OpenScene(SpheresProjectSetup.ScenePath, OpenSceneMode.Single);

            AutomationRunner.ResetStatus();
            SessionState.SetString(AutomationArgs.EditorExtraArgsKey, extraArgs ?? "");
            SessionState.SetString(KindKey, kind);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(EnteredKey, false);
            Debug.Log("[SpheresAutomation] Entering play mode for the " + kind + " run...");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Clear()
        {
            SessionState.EraseString(KindKey);
            SessionState.EraseString(AutomationArgs.EditorExtraArgsKey);
            SessionState.EraseFloat(StartKey);
            SessionState.EraseBool(EnteredKey);
        }

        static void Finish(int code, string message)
        {
            Clear();
            if (code == 0) Debug.Log("[SpheresAutomation] " + message);
            else Debug.LogError("[SpheresAutomation] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
                return;
            }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        /// <summary>Last resort when the runtime side could not write its own report.</summary>
        static void WriteFallbackReport(string kind, string message)
        {
            try
            {
                Directory.CreateDirectory("Builds");
                string path = kind == AutomationEntry.KindBalance ? "Builds/balance-report.md" : "Builds/smoke-report.txt";
                File.WriteAllText(path, "Spheres " + kind + " run\nResult: FAIL\n" + message + "\nSee the Unity log for details.\n");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Tick()
        {
            string kind = SessionState.GetString(KindKey, "");
            if (string.IsNullOrEmpty(kind)) return;
            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, (float)EditorApplication.timeSinceStartup);

            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(EnteredKey, true);
                if (AutomationRunner.Finished)
                {
                    int code = AutomationRunner.ExitCode;
                    Finish(code, "The " + kind + " run finished with exit code " + code + ".");
                    return;
                }
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (SessionState.GetBool(EnteredKey, false))
                {
                    WriteFallbackReport(kind, "Play mode ended before the run finished.");
                    Finish(4, "Play mode ended before the " + kind + " run finished.");
                    return;
                }
                if (elapsed > 30.0)
                {
                    WriteFallbackReport(kind, "The editor could not enter play mode (compile errors?).");
                    Finish(3, "Could not enter play mode for the " + kind + " run (compile errors?).");
                    return;
                }
            }

            // Backstop in case frames stop advancing; the runner's own -automationTimeout normally fires first.
            float minutes = 15f + ReadTimeoutMinutes(kind == AutomationEntry.KindBalance ? 600f : 60f);
            if (elapsed > minutes * 60.0)
            {
                WriteFallbackReport(kind, "The run did not finish within " + minutes + " minutes.");
                Finish(2, "The " + kind + " run did not finish within " + minutes + " minutes.");
            }
        }

        static float ReadTimeoutMinutes(float fallback) =>
            AutomationArgs.FromCommandLine().GetFloat("-automationTimeout", fallback);
    }
}
