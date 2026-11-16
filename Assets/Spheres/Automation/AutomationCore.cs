using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Spheres.Automation
{
    /// <summary>
    /// Command-line options for the automation runners. In a player these come from the player's command
    /// line; in the editor they come from the editor's command line (e.g. after -executeMethod).
    /// </summary>
    public sealed class AutomationArgs
    {
        readonly string[] args;

        public AutomationArgs(string[] args) { this.args = args ?? new string[0]; }

        /// <summary>SessionState key with extra space-separated options set by the editor menu items.</summary>
        public const string EditorExtraArgsKey = "spheres.automation.args";

        public static AutomationArgs FromCommandLine()
        {
            var list = new List<string>();
#if UNITY_EDITOR
            // Editor menu items pass their options here; they win over the editor's own command line.
            string extra = UnityEditor.SessionState.GetString(EditorExtraArgsKey, "");
            if (!string.IsNullOrEmpty(extra)) list.AddRange(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
#endif
            try { list.AddRange(Environment.GetCommandLineArgs()); }
            catch (Exception) { }
            return new AutomationArgs(list.ToArray());
        }

        int IndexOf(string name)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public bool Has(string name) => IndexOf(name) >= 0;

        public string Get(string name, string fallback = null)
        {
            int i = IndexOf(name);
            if (i < 0 || i + 1 >= args.Length) return fallback;
            string v = args[i + 1];
            return v.StartsWith("-") && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? fallback : v;
        }

        public int GetInt(string name, int fallback) =>
            int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        public float GetFloat(string name, float fallback) =>
            float.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        public List<string> GetList(string name)
        {
            var list = new List<string>();
            string v = Get(name);
            if (string.IsNullOrEmpty(v)) return list;
            foreach (var part in v.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (part.Trim().Length > 0) list.Add(part.Trim());
            return list;
        }
    }

    /// <summary>
    /// Collects every error, exception, assert and warning Unity logs while an automation run is active,
    /// grouped by the section of the run that was executing (a map, a menu, ...).
    /// </summary>
    public sealed class AutomationLog
    {
        public sealed class Entry
        {
            public string Context;
            public LogType Type;
            public string Message;
            public string Stack;
            public int Count;
            public string Key => Context + "\n" + Type + "\n" + Message + "\n" + Stack;
        }

        public static AutomationLog Active { get; private set; }

        readonly object gate = new object();
        readonly List<Entry> errors = new List<Entry>();
        readonly List<Entry> warnings = new List<Entry>();
        readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>();
        readonly Dictionary<string, int> errorsPerContext = new Dictionary<string, int>();
        Regex ignore;
        volatile string context = "boot";

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        /// <summary>Section the next messages are attributed to.</summary>
        public string Context
        {
            get => context;
            set => context = string.IsNullOrEmpty(value) ? "unknown" : value;
        }

        /// <summary>Starts capturing (idempotent). Optional regex: matching messages are ignored.</summary>
        public static AutomationLog Begin(string ignorePattern)
        {
            if (Active != null) return Active;
            var log = new AutomationLog();
            if (!string.IsNullOrEmpty(ignorePattern))
            {
                try { log.ignore = new Regex(ignorePattern, RegexOptions.IgnoreCase); }
                catch (Exception e) { Debug.LogWarning("[SpheresAutomation] Invalid ignore pattern: " + e.Message); }
            }
            Application.logMessageReceivedThreaded += log.OnLog;
            Active = log;
            return log;
        }

        public void End()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            if (Active == this) Active = null;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            if (message != null && message.StartsWith("[SpheresAutomation]")) return;
            if (ignore != null && message != null && ignore.IsMatch(message)) return;
            Add(context, type, message, stack);
        }

        /// <summary>Record a failure detected by the harness itself (not logged by the game).</summary>
        public void AddFailure(string ctx, string message, string stack = "")
        {
            Add(ctx ?? context, LogType.Error, "[harness] " + message, stack ?? "");
        }

        public void AddException(string ctx, Exception e)
        {
            Add(ctx ?? context, LogType.Exception, e.GetType().Name + ": " + e.Message, e.StackTrace ?? "");
        }

        void Add(string ctx, LogType type, string message, string stack)
        {
            bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            lock (gate)
            {
                var e = new Entry { Context = ctx, Type = type, Message = message ?? "", Stack = stack ?? "" };
                if (byKey.TryGetValue(e.Key, out var existing)) existing.Count++;
                else
                {
                    e.Count = 1;
                    byKey[e.Key] = e;
                    if (isError) errors.Add(e);
                    else if (warnings.Count < 500) warnings.Add(e);
                }
                if (isError)
                {
                    ErrorCount++;
                    errorsPerContext.TryGetValue(ctx, out int n);
                    errorsPerContext[ctx] = n + 1;
                }
                else WarningCount++;
            }
        }

        public int ErrorsIn(string ctx)
        {
            lock (gate) return errorsPerContext.TryGetValue(ctx, out int n) ? n : 0;
        }

        public List<Entry> Errors() { lock (gate) return new List<Entry>(errors); }
        public List<Entry> Warnings() { lock (gate) return new List<Entry>(warnings); }

        /// <summary>Appends the errors (all) and warnings (first few unique ones) to a report.</summary>
        public void AppendDetails(StringBuilder sb, int maxWarnings)
        {
            var errs = Errors();
            sb.AppendLine();
            sb.AppendLine("== Errors (" + ErrorCount + " total, " + errs.Count + " unique) ==");
            if (errs.Count == 0) sb.AppendLine("none");
            foreach (var e in errs)
            {
                sb.AppendLine();
                sb.AppendLine("--- [" + e.Context + "] " + e.Type + (e.Count > 1 ? " x" + e.Count : ""));
                sb.AppendLine(e.Message.TrimEnd());
                if (!string.IsNullOrEmpty(e.Stack)) sb.AppendLine(Indent(e.Stack.TrimEnd()));
            }
            var warns = Warnings();
            sb.AppendLine();
            sb.AppendLine("== Warnings (" + WarningCount + " total, " + warns.Count + " unique; informational) ==");
            if (warns.Count == 0) sb.AppendLine("none");
            for (int i = 0; i < warns.Count && i < maxWarnings; i++)
            {
                var w = warns[i];
                sb.AppendLine("[" + w.Context + "] " + (w.Count > 1 ? "x" + w.Count + " " : "") + FirstLine(w.Message));
            }
            if (warns.Count > maxWarnings) sb.AppendLine("... " + (warns.Count - maxWarnings) + " more unique warnings");
        }

        static string Indent(string s) => "    " + s.Replace("\n", "\n    ");
        static string FirstLine(string s) { int i = s.IndexOf('\n'); return i < 0 ? s : s.Substring(0, i); }
    }

    /// <summary>
    /// Base class for the smoke test and balance simulator. Runs a script (an IEnumerator, one step per
    /// frame) while forcing the game to run as fast as the machine allows: every frame advances a fixed
    /// amount of game time (Time.captureDeltaTime) regardless of wall-clock time, so the three-second
    /// round intros and card pick delays cost a few frames, not seconds.
    /// An exception thrown by a step aborts only the sub-routine that threw; the run continues with the
    /// next section.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public abstract class AutomationRunner : MonoBehaviour
    {
        /// <summary>-1 while running, otherwise the process exit code the run asks for.</summary>
        public static int ExitCode { get; private set; } = -1;
        public static bool Finished => ExitCode >= 0;
        public static AutomationRunner Current { get; private set; }

        /// <summary>Forget the result of a previous run (needed when play mode is entered without a domain reload).</summary>
        public static void ResetStatus() => ExitCode = -1;

        public AutomationArgs Args { get; private set; }
        protected AutomationLog Log { get; private set; }
        protected GameManager Gm => GameManager.Instance;

        /// <summary>Simulated frames per game second (game time per frame = 1 / SimFps). 0 = real time.</summary>
        protected float SimFps = 50f;
        protected float TimeScale = 2f;
        /// <summary>Wall-clock limit for the whole run in seconds.</summary>
        protected float WallLimit = 3600f;

        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        float wallStart;
        bool done;

        protected abstract string Name { get; }
        protected abstract IEnumerator Run();
        /// <summary>Write reports. Called exactly once at the end, also after timeouts.</summary>
        protected abstract void WriteReport(string status);

        public static T Launch<T>(AutomationArgs args) where T : AutomationRunner
        {
            var go = new GameObject("Spheres Automation");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<T>();
            r.Args = args;
            return r;
        }

        protected virtual void Awake()
        {
            Current = this;
            ExitCode = -1;
            wallStart = Time.realtimeSinceStartup;
        }

        protected virtual void Start()
        {
            if (Args == null) Args = AutomationArgs.FromCommandLine();
            Log = AutomationLog.Begin(Args.Get("-automationIgnore"));
            SimFps = Mathf.Max(0f, Args.GetFloat("-simFps", SimFps));
            TimeScale = Mathf.Clamp(Args.GetFloat("-simTimeScale", TimeScale), 0.1f, 100f);
            WallLimit = Mathf.Max(30f, Args.GetFloat("-automationTimeout", WallLimit / 60f) * 60f);
            Say(Name + " started (sim fps " + SimFps + ", time scale " + TimeScale + ", wall limit " + Mathf.RoundToInt(WallLimit / 60f) + " min)");
            stack.Push(Run());
        }

        protected virtual void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        protected static void Say(string message) => Debug.Log("[SpheresAutomation] " + message);

        void ApplySpeed()
        {
            if (done) return;
            if (SimFps > 0f)
            {
                Time.captureDeltaTime = 1f / SimFps;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
            var gm = Gm;
            if (gm == null || !gm.Paused) Time.timeScale = TimeScale;
        }

        void Update()
        {
            if (done) return;
            ApplySpeed();
            if (Time.realtimeSinceStartup - wallStart > WallLimit)
            {
                Log.AddFailure(Log.Context, "Run exceeded the wall-clock limit of " + Mathf.RoundToInt(WallLimit / 60f) + " minutes (-automationTimeout)");
                Complete("TIMEOUT", 2);
                return;
            }
            Step();
        }

        // Game code changes Time.timeScale in Update (slow motion at round end); put ours back afterwards.
        void LateUpdate() => ApplySpeed();

        void Step()
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e)
                {
                    if (done) return;
                    Log.AddException(Log.Context, e);
                    stack.Pop();
                    continue; // resume the parent routine in the same frame
                }
                if (done) return; // the script called Complete()
                if (!more)
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                return; // yield null (or anything else) = wait one frame
            }
            if (!done) Complete(null, -1);
        }

        /// <summary>Finish the run. code &lt; 0 = derive from the error count.</summary>
        protected void Complete(string status, int code)
        {
            if (done) return;
            done = true;
            stack.Clear();
            if (code < 0) code = Log.ErrorCount > 0 ? 1 : 0;
            if (status == null) status = code == 0 ? "PASS" : "FAIL";
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            try { WriteReport(status); }
            catch (Exception e)
            {
                Debug.LogError("[SpheresAutomation] Could not write the report: " + e);
                if (code == 0) code = 1;
            }
            Say(Name + " finished: " + status + " (exit code " + code + ", " + Log.ErrorCount + " errors, " +
                Mathf.RoundToInt(Time.realtimeSinceStartup - wallStart) + " s wall time)");
            Log.End();
            ExitCode = code;
#if !UNITY_EDITOR
            Application.Quit(code);
#endif
        }

        // ------------------------------------------------------------------ helpers for scripts

        protected float WallTime => Time.realtimeSinceStartup - wallStart;

        protected static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Wait for an amount of scaled game time, or until a condition is true.</summary>
        protected static IEnumerator WaitGame(float seconds, Func<bool> until = null, float wallLimit = 120f)
        {
            float t = 0f, start = Time.realtimeSinceStartup;
            while (t < seconds && Time.realtimeSinceStartup - start < wallLimit)
            {
                if (until != null && until()) yield break;
                yield return null;
                t += Time.deltaTime;
            }
        }

        /// <summary>Resolve a report path: absolute paths are kept, relative ones are relative to the working directory.</summary>
        protected static string ReportPath(string path)
        {
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return full;
        }

        /// <summary>Snapshot of match settings and the persisted PlayerPrefs copy, restored after the run.</summary>
        protected sealed class SettingsBackup
        {
            const string PrefsKey = "spheres.settings.v1"; // MatchSettings.PrefsKey
            readonly string json;
            readonly string prefs;

            public SettingsBackup(MatchSettings s)
            {
                json = JsonUtility.ToJson(s);
                prefs = PlayerPrefs.HasKey(PrefsKey) ? PlayerPrefs.GetString(PrefsKey) : null;
            }

            public void Restore(MatchSettings s)
            {
                if (s != null) JsonUtility.FromJsonOverwrite(json, s);
                if (prefs != null) PlayerPrefs.SetString(PrefsKey, prefs);
                else PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Overwrite match rules in place with a bots-only setup (the GameManager owns the settings object).</summary>
        protected static void ConfigureBots(MatchSettings s, int count, int teams, BotDifficulty[] difficulties, string namePrefix)
        {
            s.slots.Clear();
            for (int i = 0; i < count; i++)
            {
                s.slots.Add(new PlayerSlot
                {
                    name = namePrefix + " " + (i + 1),
                    control = ControlType.Bot,
                    team = i % Mathf.Max(2, teams),
                    difficulty = difficulties[i % difficulties.Length],
                });
            }
            s.mapRotation = MapRotation.Fixed;
            s.disabledCards.Clear();
            s.friendlyFire = false;
        }

        protected static bool HasBadPosition(GameManager gm)
        {
            foreach (var p in gm.Players)
            {
                if (p == null) continue;
                var v = p.transform.position;
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                    float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Starts a runner when the game is launched for automation:
    /// player: <c>Spheres -batchmode -nographics -spheresSmokeTest</c> (or <c>-spheresBalanceSim</c>);
    /// editor: the Spheres.EditorTools.SmokeTest / BalanceSim entry points set a session flag and enter play mode.
    /// Does nothing in a normal game launch.
    /// </summary>
    public static class AutomationEntry
    {
        /// <summary>SessionState key the editor tools set to the run kind before entering play mode.</summary>
        public const string EditorSessionKey = "spheres.automation.kind";
        public const string KindSmoke = "smoke";
        public const string KindBalance = "balance";

        static string pending;

        static string RequestedKind()
        {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetString(EditorSessionKey, "");
#else
            var args = AutomationArgs.FromCommandLine();
            if (args.Has("-spheresSmokeTest")) return KindSmoke;
            if (args.Has("-spheresBalanceSim")) return KindBalance;
            return "";
#endif
        }

        // Start capturing before the game boots so errors from GameManager.Awake are attributed to "boot".
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeScene()
        {
            pending = RequestedKind();
            if (string.IsNullOrEmpty(pending)) return;
            AutomationRunner.ResetStatus();
            AutomationLog.Begin(AutomationArgs.FromCommandLine().Get("-automationIgnore"));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterScene()
        {
            if (string.IsNullOrEmpty(pending) || AutomationRunner.Current != null) return;
            var args = AutomationArgs.FromCommandLine();
            if (pending == KindSmoke) AutomationRunner.Launch<SmokeTestRunner>(args);
            else if (pending == KindBalance) AutomationRunner.Launch<BalanceSimRunner>(args);
            else Debug.LogError("[SpheresAutomation] Unknown automation kind '" + pending + "'");
            pending = null;
        }
    }
}
