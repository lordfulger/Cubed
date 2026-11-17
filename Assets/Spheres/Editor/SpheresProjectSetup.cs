using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spheres.EditorTools
{
    /// <summary>
    /// One-click project setup and BepInEx-friendly builds.
    /// The game builds itself from code at runtime, so the only asset it needs is an (empty) scene.
    /// </summary>
    [InitializeOnLoad]
    public static class SpheresProjectSetup
    {
        public const string ScenePath = "Assets/Spheres/Scenes/Main.unity";

        static SpheresProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("spheres.setup.done", false)) return;
                SessionState.SetBool("spheres.setup.done", true);
                Setup(false);
            };
        }

        [MenuItem("Spheres/Setup Project", priority = 0)]
        public static void SetupMenu() => Setup(true);

        static void Setup(bool interactive)
        {
            EnsureScene(interactive);
            EnsureInputAxes();
            EnsureAlwaysIncludedShaders();
            ConfigurePlayerSettings();
            if (interactive) Debug.Log("[Spheres] Project setup complete. Press Play in any scene to start the game.");
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

        /// <summary>Adds Spheres_J{n}_A{k} joystick axes if ProjectSettings/InputManager.asset lacks them.</summary>
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
            for (int j = 1; j <= 8; j++)
                for (int a = 1; a <= 10; a++)
                    Add("Spheres_J" + j + "_A" + a, 2, a - 1, j, 1f, 0.05f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The game creates all materials at runtime and the build scene is empty, so make sure the shaders
        /// it uses are always shipped in the player.
        /// </summary>
        static void EnsureAlwaysIncludedShaders()
        {
            var settings = UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings();
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            bool changed = false;
            foreach (var name in new[] { "Standard", "Sprites/Default", "Hidden/Internal-Colored" })
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                if (present) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>BepInEx 5 needs a Mono build with readable (unstripped) managed assemblies.</summary>
        /// <summary>
        /// The game keeps state in statics (player lists, pools, weather...), so entering Play mode must reload
        /// scripts. Newer Unity versions (6.6+) default new projects to skipping that, so turn it back on.
        /// Done by reflection because the property has moved between Unity versions.
        /// </summary>
        /// <summary>
        /// Spheres uses the built-in render pipeline (its own shaders and post effects). If the project was
        /// created from a URP/HDRP template, switch every quality level and the graphics default back to built-in.
        /// </summary>
        static void UseBuiltInRenderPipeline()
        {
            bool changed = false;
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
            {
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
                changed = true;
            }
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null)
                {
                    QualitySettings.renderPipeline = null;
                    changed = true;
                }
            }
            QualitySettings.SetQualityLevel(current, false);
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Spheres] Switched the project to the built-in render pipeline (Spheres doesn't use URP/HDRP).");
            }
        }

        /// <summary>
        /// Spheres reads input through the classic Input Manager (plus the Input System package for rumble when
        /// present). Unity 6 templates switch "Active Input Handling" to the new system only, which makes every
        /// UnityEngine.Input call throw, so set it to Both. Unity must restart for this to take effect.
        /// </summary>
        static void EnsureLegacyInputEnabled(bool interactive)
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return;
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop == null || prop.intValue != 1) return; // 0 = old, 1 = new only, 2 = both
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.LogWarning("[Spheres] Set Active Input Handling to 'Both'. Restart Unity for it to take effect.");
                if (EditorUtility.DisplayDialog("Spheres: restart needed",
                        "Spheres switched 'Active Input Handling' to 'Both' so keyboard, mouse and gamepads work.\n\nUnity needs to restart for this to take effect. Restart now?",
                        "Restart", "Later"))
                    EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
            }
            catch (System.Exception e) { Debug.LogWarning("[Spheres] Could not check Active Input Handling: " + e.Message); }
        }

        static void ForceDomainReload()
        {
            try
            {
                var t = typeof(EditorSettings);
                var enabled = t.GetProperty("enterPlayModeOptionsEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (enabled != null && enabled.CanWrite && (bool)enabled.GetValue(null)) enabled.SetValue(null, false);
                var opts = t.GetProperty("enterPlayModeOptions", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (opts != null && opts.CanWrite && System.Convert.ToInt32(opts.GetValue(null)) != 0)
                    opts.SetValue(null, System.Enum.ToObject(opts.PropertyType, 0)); // 0 = reload domain and scene
            }
            catch (System.Exception e) { Debug.LogWarning("[Spheres] Could not force domain reload: " + e.Message); }
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Spheres";
            // Linear lighting + HDR is what the post-processing stack is tuned for.
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            ForceDomainReload();
            UseBuiltInRenderPipeline();
            EnsureLegacyInputEnabled(true);
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = "Spheres";
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

        [MenuItem("Spheres/Build/Windows (x64)", priority = 20)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Spheres.exe");

        [MenuItem("Spheres/Build/Linux (x64)", priority = 21)]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Spheres.x86_64");

        [MenuItem("Spheres/Build/macOS", priority = 22)]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/Spheres.app");

        /// <summary>Command-line entry: Unity -batchmode -quit -executeMethod Spheres.EditorTools.SpheresProjectSetup.BuildWindowsCI</summary>
        public static void BuildWindowsCI() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Spheres.exe", false);
        public static void BuildLinuxCI() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Spheres.x86_64", false);

        /// <summary>Non-interactive setup (scene, input axes, shaders, player settings) for the automation tools.</summary>
        internal static void PrepareForAutomation() => Setup(false);

        /// <summary>
        /// Build entry for GameCI's unity-builder (buildMethod: Spheres.EditorTools.SpheresProjectSetup.BuildCI).
        /// Reads the arguments GameCI passes: -customBuildTarget, -customBuildPath, -customBuildName, -buildVersion.
        /// Unlike GameCI's default build method this runs project setup first, so the scene does not have to be committed.
        /// </summary>
        public static void BuildCI()
        {
            string targetName = CommandLineValue("-customBuildTarget") ?? CommandLineValue("-buildTarget");
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (!string.IsNullOrEmpty(targetName) && !System.Enum.TryParse(targetName, true, out target))
            {
                Debug.LogError("[Spheres] Unknown build target '" + targetName + "'");
                EditorApplication.Exit(1);
                return;
            }

            string name = CommandLineValue("-customBuildName") ?? "Spheres";
            string ext = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows ? ".exe"
                       : target == BuildTarget.StandaloneLinux64 ? ".x86_64"
                       : target == BuildTarget.StandaloneOSX ? ".app" : "";
            string path = CommandLineValue("-customBuildPath");
            if (string.IsNullOrEmpty(path)) path = Path.Combine("Builds", target.ToString(), name + ext);
            else if ((Directory.Exists(path) && !path.EndsWith(".app")) || path.EndsWith("/") || path.EndsWith("\\")) path = Path.Combine(path, name + ext);
            else if (ext.Length > 0 && !path.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)) path += ext;

            string version = CommandLineValue("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;

            Debug.Log("[Spheres] CI build: " + target + " -> " + path + " (version " + PlayerSettings.bundleVersion + ")");
            Build(target, path, false);
        }

        static string CommandLineValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, System.StringComparison.OrdinalIgnoreCase) && !args[i + 1].StartsWith("-"))
                    return args[i + 1];
            return null;
        }

        /// <summary>
        /// Bump the version, build Windows, zip it and open a new GitHub release page. Players' games see the
        /// release and offer to update themselves (Spheres.AutoUpdater).
        /// </summary>
        [MenuItem("Spheres/Publish Update (Windows)", priority = 25)]
        public static void PublishUpdate()
        {
            string current = string.IsNullOrEmpty(PlayerSettings.bundleVersion) ? "0.1.0" : PlayerSettings.bundleVersion;
            string next = NextVersion(current);
            if (!EditorUtility.DisplayDialog("Publish a Spheres update",
                    "This builds Spheres " + next + " for Windows (current: " + current + "), zips it, and opens GitHub's \"new release\" page.\n\n" +
                    "On that page: drag the zip into the box at the bottom, then click 'Publish release'. Everyone's game will offer the update next time it starts.",
                    "Build " + next, "Cancel"))
                return;
            PlayerSettings.bundleVersion = next;
            AssetDatabase.SaveAssets();
            const string dir = "Builds/Windows";
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (System.Exception e) { Debug.LogWarning("[Spheres] Couldn't clear old build: " + e.Message); }
            if (!Build(BuildTarget.StandaloneWindows64, dir + "/Spheres.exe", false)) return;

            string zip = "Builds/Spheres-Windows-v" + next + ".zip";
            try
            {
                if (File.Exists(zip)) File.Delete(zip);
                using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
                {
                    string root = Path.GetFullPath(dir);
                    foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    {
                        string rel = file.Substring(root.Length + 1).Replace('\\', '/');
                        if (rel.Contains("DoNotShip")) continue; // Burst/IL2CPP debug folders
                        System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archive, file, rel);
                    }
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Spheres", "Zipping the build failed: " + e.Message, "OK");
                return;
            }
            Debug.Log("[Spheres] Packaged " + zip);
            EditorUtility.RevealInFinder(Path.GetFullPath(zip));
            Application.OpenURL("https://github.com/" + AutoUpdater.Repo + "/releases/new?tag=v" + next + "&title=Spheres%20v" + next);
        }

        static string NextVersion(string v)
        {
            var parts = v.Split('.').Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 0).ToList();
            while (parts.Count < 3) parts.Add(0);
            parts[parts.Count - 1]++;
            return string.Join(".", parts);
        }

        static bool Build(BuildTarget target, string path, bool reveal = true)
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
                Debug.LogError("[Spheres] Build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return false;
            }
            Debug.Log("[Spheres] Built " + path + " (" + report.summary.totalSize / (1024 * 1024) + " MB). " +
                      "Install BepInEx 5 (x64) next to the executable to enable code mods.");
            if (reveal && !Application.isBatchMode) EditorUtility.RevealInFinder(path);
            return true;
        }

        [MenuItem("Spheres/Open User Data Folder", priority = 40)]
        public static void OpenUserData() => EditorUtility.RevealInFinder(Application.persistentDataPath + "/");
    }
}
