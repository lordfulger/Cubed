using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cubed.EditorTools
{
    /// <summary>
    /// One-click project setup and BepInEx-friendly builds.
    /// The game builds itself from code at runtime, so the only asset it needs is an (empty) scene.
    /// </summary>
    [InitializeOnLoad]
    public static class CubedProjectSetup
    {
        public const string ScenePath = "Assets/Cubed/Scenes/Main.unity";

        static CubedProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("cubed.setup.done", false)) return;
                SessionState.SetBool("cubed.setup.done", true);
                Setup(false);
            };
        }

        [MenuItem("Cubed/Setup Project", priority = 0)]
        public static void SetupMenu() => Setup(true);

        static void Setup(bool interactive)
        {
            EnsureScene(interactive);
            EnsureInputAxes();
            ConfigurePlayerSettings();
            if (interactive) Debug.Log("[Cubed] Project setup complete. Press Play in any scene to start the game.");
        }

        static void EnsureScene(bool interactive)
        {
            if (!File.Exists(ScenePath))
            {
                var active = SceneManager.GetActiveScene();
                bool safe = string.IsNullOrEmpty(active.path) && !active.isDirty;
                if (!safe)
                {
                    if (!interactive) return;
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes;
            if (!scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(scenes).ToArray();
        }

        /// <summary>Adds Cubed_J{n}_A{k} joystick axes if ProjectSettings/InputManager.asset lacks them.</summary>
        static void EnsureInputAxes()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset").FirstOrDefault();
            if (asset == null) return;
            var so = new SerializedObject(asset);
            var axes = so.FindProperty("m_Axes");
            var names = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < axes.arraySize; i++)
                names.Add(axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue);

            void Add(string name, int type, int axis, int joy, float sensitivity, float dead)
            {
                if (names.Contains(name)) return;
                axes.arraySize++;
                var e = axes.GetArrayElementAtIndex(axes.arraySize - 1);
                e.FindPropertyRelative("m_Name").stringValue = name;
                e.FindPropertyRelative("descriptiveName").stringValue = "";
                e.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
                e.FindPropertyRelative("negativeButton").stringValue = "";
                e.FindPropertyRelative("positiveButton").stringValue = "";
                e.FindPropertyRelative("altNegativeButton").stringValue = "";
                e.FindPropertyRelative("altPositiveButton").stringValue = "";
                e.FindPropertyRelative("gravity").floatValue = 0f;
                e.FindPropertyRelative("dead").floatValue = dead;
                e.FindPropertyRelative("sensitivity").floatValue = sensitivity;
                e.FindPropertyRelative("snap").boolValue = false;
                e.FindPropertyRelative("invert").boolValue = false;
                e.FindPropertyRelative("type").intValue = type;
                e.FindPropertyRelative("axis").intValue = axis;
                e.FindPropertyRelative("joyNum").intValue = joy;
                names.Add(name);
            }

            Add("Mouse X", 1, 0, 0, 0.1f, 0f);
            Add("Mouse Y", 1, 1, 0, 0.1f, 0f);
            for (int j = 1; j <= 4; j++)
                for (int a = 1; a <= 10; a++)
                    Add("Cubed_J" + j + "_A" + a, 2, a - 1, j, 1f, 0.05f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>BepInEx 5 needs a Mono build with readable (unstripped) managed assemblies.</summary>
        static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Cubed";
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = "Cubed";
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
#endif
            PlayerSettings.stripEngineCode = false;
        }

        // ------------------------------------------------------------------ builds

        [MenuItem("Cubed/Build/Windows (x64)", priority = 20)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Cubed.exe");

        [MenuItem("Cubed/Build/Linux (x64)", priority = 21)]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Cubed.x86_64");

        [MenuItem("Cubed/Build/macOS", priority = 22)]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/Cubed.app");

        /// <summary>Command-line entry: Unity -batchmode -quit -executeMethod Cubed.EditorTools.CubedProjectSetup.BuildWindowsCI</summary>
        public static void BuildWindowsCI() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Cubed.exe", false);
        public static void BuildLinuxCI() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Cubed.x86_64", false);

        static void Build(BuildTarget target, string path, bool reveal = true)
        {
            Setup(!Application.isBatchMode);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Cubed] Build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[Cubed] Built " + path + " (" + report.summary.totalSize / (1024 * 1024) + " MB). " +
                      "Install BepInEx 5 (x64) next to the executable to enable code mods.");
            if (reveal && !Application.isBatchMode) EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Cubed/Open User Data Folder", priority = 40)]
        public static void OpenUserData() => EditorUtility.RevealInFinder(Application.persistentDataPath + "/");
    }
}
