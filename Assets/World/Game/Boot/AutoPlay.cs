using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// A scripted sequence of steps for tests, captures and the packaged smoke run. Drives the avatar
    /// through its public API (never fakes Input). Build with the fluent methods, run with AutoPlay.Run.
    /// </summary>
    public sealed class AutoPlayScript
    {
        public readonly string Name;
        internal readonly List<Func<AutoPlay, IEnumerator>> Steps = new List<Func<AutoPlay, IEnumerator>>();
        public AutoPlayScript(string name) { Name = name; }

        public AutoPlayScript Step(Func<AutoPlay, IEnumerator> step) { Steps.Add(step); return this; }
        public AutoPlayScript Teleport(Vector3 pos, float yaw, float pitch = 0f) => Step(a => a.DoTeleport(pos, yaw, pitch));
        public AutoPlayScript WalkTo(Vector3 target, float stopDistance = 0.5f, bool run = false, float timeout = 30f) => Step(a => a.DoWalkTo(() => target, stopDistance, run, timeout));
        public AutoPlayScript WalkTo(Func<Vector3> target, float stopDistance = 0.5f, bool run = false, float timeout = 30f) => Step(a => a.DoWalkTo(target, stopDistance, run, timeout));
        public AutoPlayScript LookAt(Vector3 point, float seconds = 0.5f) => Step(a => a.DoLookAt(() => point, seconds));
        public AutoPlayScript LookAt(Func<Vector3> point, float seconds = 0.5f) => Step(a => a.DoLookAt(point, seconds));
        public AutoPlayScript Turn(float deltaYaw, float seconds) => Step(a => a.DoTurn(deltaYaw, seconds));
        public AutoPlayScript Interact() => Step(a => a.DoCall(() => { if (!Picker.Instance.InteractWithTarget()) a.Fail("nothing to interact with"); }));
        public AutoPlayScript ToggleView() => Step(a => a.DoCall(() => ViewSwitcher.Instance.Toggle()));
        public AutoPlayScript Wait(float seconds) => Step(a => a.DoWait(seconds));
        public AutoPlayScript Call(Action action) => Step(a => a.DoCall(action));
        /// <summary>Fails the script with the message if the check returns false.</summary>
        public AutoPlayScript Check(Func<bool> check, string message) => Step(a => a.DoCall(() => { if (!check()) a.Fail(message); }));
        public AutoPlayScript Capture(string name) => Step(a => a.DoCapture(name));
        public AutoPlayScript Log(string line) => Step(a => a.DoCall(() => Debug.Log(line)));
        /// <summary>Logs "[AutoPlay] fps label: avg N, worst frame M ms (WxH)" measured over the given real time.</summary>
        public AutoPlayScript MeasureFps(string label, float seconds = 3f) => Step(a => a.DoMeasureFps(label, seconds));
        /// <summary>Quits the player with the exit code (in the editor: stops Play Mode only if QuitInEditor).</summary>
        public AutoPlayScript Quit(int exitCode = 0) => Step(a => a.DoQuit(exitCode));
    }

    /// <summary>Named scripts for -autoplay &lt;name&gt; and tests.</summary>
    public static class AutoPlayScripts
    {
        private static readonly Dictionary<string, Func<AutoPlayScript>> Registry = new Dictionary<string, Func<AutoPlayScript>>();
        static AutoPlayScripts() { Register("boot_tour", BootTour); }

        public static void Register(string name, Func<AutoPlayScript> factory) => Registry[name] = factory;
        public static bool TryCreate(string name, out AutoPlayScript script)
        {
            script = Registry.TryGetValue(name, out var f) ? f() : null;
            return script != null;
        }
        public static IEnumerable<string> Names => Registry.Keys;

        /// <summary>Spawn, look around, walk to Mira, walk to the warehouse door, God View and back, quit 0.</summary>
        public static AutoPlayScript BootTour()
        {
            var s = new AutoPlayScript("boot_tour");
            s.Wait(1f)
             .MeasureFps("spawn first person")
             .Turn(-50f, 1.2f).Turn(100f, 2f).Turn(-50f, 1.2f)
             .WalkTo(() => Npc.Find(NpcIds.Mira).transform.position, 2.5f)
             .Wait(0.4f)
             .Check(() => Npc.Find(NpcIds.Mira).IsGreetingShown, "Mira greeting not shown")
             .WalkTo(() => WorldBootstrap.Instance.Warehouse.OutsideDoorPosition, 1.0f)
             .Check(() => Npc.Find(NpcIds.Doorman).IsGreetingShown, "doorman greeting not shown")
             .Call(() => s_tab = WorldBootstrap.Instance.Avatar.Position)
             .ToggleView().Wait(0.5f)
             .MeasureFps("god view default", 2f)
             .Call(() => WorldBootstrap.Instance.GodCam.FrameIsland())
             .MeasureFps("god view whole island", 2f)
             .Call(() => WorldBootstrap.Instance.GodCam.FrameHome())
             .ToggleView().Wait(0.3f)
             .Check(() => (WorldBootstrap.Instance.Avatar.Position - s_tab).magnitude < 0.001f, "avatar moved across Tab")
             .Log("[AutoPlay] boot_tour OK")
             .Quit(0);
            return s;
        }
        private static Vector3 s_tab;
    }

    /// <summary>Runs AutoPlayScripts. Also honours "-autoplay &lt;name&gt;" on the command line in player builds.</summary>
    public sealed class AutoPlay : MonoBehaviour
    {
        public static AutoPlay Instance { get; private set; }
        public bool IsRunning { get; private set; }
        public bool Failed { get; private set; }
        public string FailMessage { get; private set; }
        /// <summary>Quit Play Mode in the editor when a script calls Quit (off by default so tests can continue).</summary>
        public bool QuitInEditor;
        /// <summary>Log a failed script as an error (default). Tests that expect a failure turn this off (logged as a warning).</summary>
        public bool LogFailuresAsErrors = true;
        public event Action<AutoPlayScript, bool> Finished;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            // "-novsync": uncapped frame rate for performance measurements (MeasureFps).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-novsync") >= 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            string name = CommandLineScript();
            if (name == null || s_commandLineRan) return;
            s_commandLineRan = true; // a new game reloads the scene; the command-line script runs once
            if (AutoPlayScripts.TryCreate(name, out var script)) Run(script);
            else
            {
                Debug.LogError("[AutoPlay] unknown script " + name);
                if (!Application.isEditor) Application.Quit(2);
            }
        }

        private static bool s_commandLineRan;

        public static string CommandLineScript()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-autoplay") return args[i + 1];
            return null;
        }

        public Coroutine Run(AutoPlayScript script)
        {
            IsRunning = true; Failed = false; FailMessage = null;
            return StartCoroutine(RunRoutine(script));
        }

        public void Fail(string message)
        {
            if (Failed) return;
            Failed = true; FailMessage = message;
        }

        private IEnumerator RunRoutine(AutoPlayScript script)
        {
            Debug.Log("[AutoPlay] start " + script.Name);
            foreach (var step in script.Steps)
            {
                yield return step(this);
                if (Failed) break;
            }
            IsRunning = false;
            if (Failed)
            {
                string msg = "[AutoPlay] " + script.Name + " FAILED: " + FailMessage;
                if (LogFailuresAsErrors) Debug.LogError(msg); else Debug.LogWarning(msg);
                if (!Application.isEditor) Application.Quit(1);
            }
            Finished?.Invoke(script, !Failed);
        }

        private static FirstPersonAvatar Avatar => WorldBootstrap.Instance.Avatar;

        internal IEnumerator DoTeleport(Vector3 pos, float yaw, float pitch)
        {
            Avatar.Teleport(pos, yaw, pitch);
            yield return null;
        }

        internal IEnumerator DoWalkTo(Func<Vector3> target, float stop, bool run, float timeout)
        {
            float t0 = Time.time;
            while (true)
            {
                var d = target() - Avatar.Position; d.y = 0;
                if (d.magnitude <= stop) break;
                if (Time.time - t0 > timeout) { Fail("walk timed out " + d.magnitude.ToString("0.0") + " m short"); break; }
                float yaw = Mathf.MoveTowardsAngle(Avatar.Yaw, BoardSpace.YawOf(d), 360f * Time.deltaTime);
                Avatar.SetLook(yaw, Mathf.MoveTowards(Avatar.Pitch, 5f, 60f * Time.deltaTime));
                Avatar.SetScriptedMove(d.normalized * Mathf.Clamp01(d.magnitude / 0.6f + 0.3f), run);
                yield return null;
            }
        }

        internal IEnumerator DoLookAt(Func<Vector3> point, float seconds)
        {
            float y0 = Avatar.Yaw, p0 = Avatar.Pitch, t = 0;
            var eye = Avatar.EyePosition; var d = point() - eye;
            float y1 = BoardSpace.YawOf(d), p1 = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / Mathf.Max(0.001f, seconds)));
                Avatar.SetLook(Mathf.LerpAngle(y0, y1, u), Mathf.Lerp(p0, p1, u));
                yield return null;
            }
            Avatar.SetLook(y1, p1);
        }

        internal IEnumerator DoTurn(float delta, float seconds)
        {
            float y0 = Avatar.Yaw, t = 0;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Avatar.SetLook(y0 + delta * Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / seconds)), Avatar.Pitch);
                yield return null;
            }
        }

        internal IEnumerator DoWait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Last measured average frame rate (for tests and logs).</summary>
        public float LastFps { get; private set; }

        internal IEnumerator DoMeasureFps(string label, float seconds)
        {
            yield return null;
            float t0 = Time.realtimeSinceStartup, last = t0, worst = 0f;
            int frames = 0;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                worst = Mathf.Max(worst, now - last);
                last = now; frames++;
            }
            LastFps = frames / Mathf.Max(0.001f, last - t0);
            Debug.Log("[AutoPlay] fps " + label + ": avg " + LastFps.ToString("0") + ", worst frame " + (worst * 1000f).ToString("0.0") + " ms (" + Screen.width + "x" + Screen.height + ", vSync " + QualitySettings.vSyncCount + ")");
        }

        internal IEnumerator DoCall(Action a)
        {
            try { a(); }
            catch (Exception e) { Fail(e.GetType().Name + ": " + e.Message); }
            yield return null;
        }

        /// <summary>Folder for captures: &lt;project or build root&gt;/Evidence/screenshots.</summary>
        public static string ScreenshotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence", "screenshots"));

        internal IEnumerator DoCapture(string name)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(ScreenshotDir);
            string path = Path.Combine(ScreenshotDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null; yield return null;
            Debug.Log("[AutoPlay] captured " + path);
        }

        internal IEnumerator DoQuit(int code)
        {
            yield return null;
            if (Application.isEditor) { if (QuitInEditor) Hud.Quit(code); }
            else Application.Quit(code);
        }
    }
}
