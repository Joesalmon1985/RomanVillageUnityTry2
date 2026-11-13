using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Factions;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>One lattice move for <see cref="AutoPlayScript.Step(LatticeMove, int)"/>.</summary>
    public enum LatticeMove { Forward, Back, TurnLeft, TurnRight }

    /// <summary>
    /// A scripted sequence of steps for tests, captures and the packaged smoke run. Drives the player through the
    /// same public paths a person uses (walker moves, pointer taps, bubble replies; never fakes Input). Build with the
    /// fluent methods, run with AutoPlay.Run.
    /// Lattice helpers (package P): <see cref="Step(LatticeMove,int)"/>, <see cref="Turn(int)"/>,
    /// <see cref="WalkTo(Cell,float)"/>, <see cref="WalkToObject"/>, <see cref="Follow"/>, <see cref="TapObject"/>,
    /// <see cref="TapGround"/>, <see cref="PickReply"/>, <see cref="CastAtProposal"/>, <see cref="OpenMap"/> /
    /// <see cref="CloseMap"/>, <see cref="ToggleHud"/>, <see cref="ToggleView"/> (F1 developer tools).
    /// </summary>
    public sealed class AutoPlayScript
    {
        public readonly string Name;
        internal readonly List<Func<AutoPlay, IEnumerator>> Steps = new List<Func<AutoPlay, IEnumerator>>();
        public AutoPlayScript(string name) { Name = name; }

        public AutoPlayScript Step(Func<AutoPlay, IEnumerator> step) { Steps.Add(step); return this; }

        // ---- lattice (package P) ----

        /// <summary>One or more lattice moves through the walker (each waits for its glide to finish).</summary>
        public AutoPlayScript Step(LatticeMove move, int times = 1) => Step(a => a.DoMoves(move, times));
        /// <summary>Turns by sixths of a circle: positive = right (clockwise), negative = left.</summary>
        public AutoPlayScript Turn(int sixths) => Step(a => a.DoMoves(sixths >= 0 ? LatticeMove.TurnRight : LatticeMove.TurnLeft, Math.Abs(sixths)));
        /// <summary>Puts the walker on a cell with a heading at once.</summary>
        public AutoPlayScript TeleportCell(Cell cell, int heading) => Step(a => a.DoTeleportCell(() => cell, heading));
        public AutoPlayScript TeleportCell(Func<Cell> cell, int heading) => Step(a => a.DoTeleportCell(cell, heading));
        /// <summary>Auto-walks along the lattice to a cell (path round blocked cells).</summary>
        public AutoPlayScript WalkTo(Cell cell, float timeout = 60f) => Step(a => a.DoWalkToCell(() => cell, timeout));
        public AutoPlayScript WalkTo(Func<Cell> cell, float timeout = 60f) => Step(a => a.DoWalkToCell(cell, timeout));
        /// <summary>Walks up to an object: to the free cell beside it nearest to the player, then faces it.</summary>
        public AutoPlayScript WalkToObject(Func<Component> target, float timeout = 60f) => Step(a => a.DoWalkTo(() => target().transform.position, 0.1f, timeout));
        /// <summary>Starts following a person (the step returns at once; the walker keeps following).</summary>
        public AutoPlayScript Follow(Func<IFollowable> who) => Step(a => a.DoCall(() => WorldBootstrap.Instance.Avatar.Follow(who())));
        /// <summary>Taps an object with the pointer (as a click on it).</summary>
        public AutoPlayScript TapObject(Func<Component> target) => Step(a => a.DoCall(() => PointerInput.TapObject(target())));
        /// <summary>Presses a ground point with the pointer (casts if an open proposal is there).</summary>
        public AutoPlayScript TapGround(Func<Vector3> ground) => Step(a => a.DoCall(() => PointerInput.TapGround(ground())));
        /// <summary>Answers the nearest open bubble's reply (0-based), as keys 1-3 do. Fails if there is none.</summary>
        public AutoPlayScript PickReply(int index) => Step(a => a.DoPickReply(index, 3f));
        /// <summary>Walks within cast range of your people's proposal at a rank (0 = top) and presses its ground. Fails if it is not cast.</summary>
        public AutoPlayScript CastAtProposal(int rank = 0, float timeout = 90f) => Step(a => a.DoCastAtProposal(rank, timeout));
        public AutoPlayScript OpenMap() => Step(a => a.DoCall(MapScreen.Open));
        public AutoPlayScript CloseMap() => Step(a => a.DoCall(MapScreen.Close));
        public AutoPlayScript ToggleHud() => Step(a => a.DoCall(() => Hud.Visible = !Hud.Visible));
        /// <summary>Waits until the walker stands still with no auto-walk (or the timeout).</summary>
        public AutoPlayScript WaitIdle(float timeout = 30f) => Step(a => a.DoWaitIdle(timeout));

        // ---- older helpers, now on the lattice ----

        /// <summary>Exact teleport (legacy; the walker takes the nearest cell and heading as its lattice pose).</summary>
        public AutoPlayScript Teleport(Vector3 pos, float yaw, float pitch = 0f) => Step(a => a.DoTeleport(pos, yaw, pitch));
        /// <summary>
        /// Walks along the lattice towards a point until within <paramref name="stopDistance"/> m. With a stop distance
        /// under one step it ends on the free cell beside the point nearest to the player, facing it. (run is ignored.)
        /// </summary>
        public AutoPlayScript WalkTo(Vector3 target, float stopDistance = 0.5f, bool run = false, float timeout = 30f) => Step(a => a.DoWalkTo(() => target, stopDistance, timeout));
        public AutoPlayScript WalkTo(Func<Vector3> target, float stopDistance = 0.5f, bool run = false, float timeout = 30f) => Step(a => a.DoWalkTo(target, stopDistance, timeout));
        /// <summary>Turns to the heading nearest the direction of the point (there is no free look).</summary>
        public AutoPlayScript LookAt(Vector3 point, float seconds = 0.5f) => Step(a => a.DoFace(() => point));
        public AutoPlayScript LookAt(Func<Vector3> point, float seconds = 0.5f) => Step(a => a.DoFace(point));
        /// <summary>Turns by a yaw in degrees, rounded to sixths of a circle.</summary>
        public AutoPlayScript Turn(float deltaYaw, float seconds) => Turn(Mathf.RoundToInt(deltaYaw / 60f));
        /// <summary>Taps the nearest villager with a dialogue within 15 m (as a click on them).</summary>
        public AutoPlayScript Interact() => Step(a => a.DoCall(() =>
        {
            var n = AutoPlay.NearestInteractable(15f);
            if (n == null) a.Fail("nothing to interact with");
            else PointerInput.TapObject(n);
        }));
        /// <summary>F1: the developer God View and back.</summary>
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
        static AutoPlayScripts()
        {
            Register("boot_tour", BootTour);
            Register("capture_tour", CaptureTour);
            Register("walk_tour", WalkTour);
        }

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static PlayerWalker W => WorldBootstrap.Instance.Avatar;
        private static CampLayout Camp => new CampLayout(WorldBootstrap.Instance.Sim.State.Board);

        /// <summary>
        /// Screenshots for checking a packaged build by eye (materials, fonts, UI scale): spawn, the pause menu, Mira,
        /// the map, the HUD and the F1 God View. Files go to &lt;exe folder&gt;/Evidence/screenshots (pkg_*.png).
        /// </summary>
        public static AutoPlayScript CaptureTour()
        {
            var s = new AutoPlayScript("capture_tour");
            s.Wait(1.5f).Capture("pkg_spawn")
             .Call(() => Hud.Instance.OpenMenu()).Wait(0.3f).Capture("pkg_menu")
             .Call(() => { Hud.Instance.MenuPage = Hud.Page.HowToPlay; }).Wait(0.3f).Capture("pkg_menu_howto")
             .Call(() => Hud.Instance.CloseMenu())
             .WalkToObject(() => Npc.Find(NpcIds.Mira)).Wait(0.3f).Capture("pkg_mira")
             .OpenMap().Wait(0.4f).Capture("pkg_map").CloseMap()
             .ToggleHud().Wait(0.3f).Capture("pkg_hud").ToggleHud()
             .ToggleView().Wait(0.8f).Capture("pkg_god_home")
             .Call(() => B.GodCam.FrameIsland()).Wait(0.8f).Capture("pkg_god_island")
             .ToggleView()
             .Log("[AutoPlay] capture_tour OK")
             .Quit(0);
            return s;
        }

        public static void Register(string name, Func<AutoPlayScript> factory) => Registry[name] = factory;
        public static bool TryCreate(string name, out AutoPlayScript script)
        {
            script = Registry.TryGetValue(name, out var f) ? f() : null;
            return script != null;
        }
        public static IEnumerable<string> Names => Registry.Keys;

        /// <summary>
        /// Spawn at the home camp on the lattice, check the new board (61 hexes, numbers 2..12 and never 7, start
        /// corners), turn round in place, walk up to Mira, the F1 God View and back (the player does not move), quit 0.
        /// </summary>
        public static AutoPlayScript BootTour()
        {
            var s = new AutoPlayScript("boot_tour");
            s.Wait(1f)
             .Check(BoardLooksNew, "board is not the prehistoric board (61 hexes, numbers 2..12 except 7, two start corners)")
             .Check(() => W.Cell == Camp.Spawn && W.Heading == Camp.SpawnHeading, "walker not on the camp spawn cell and heading")
             .MeasureFps("spawn first person")
             .Turn(-1).Turn(2).Turn(-1)
             .Check(() => W.Heading == Camp.SpawnHeading, "turns did not return the heading")
             .WalkToObject(() => Npc.Find(NpcIds.Mira))
             .Check(() => BoardSpace.Flat(W.Position - Npc.Find(NpcIds.Mira).transform.position) <= WalkLattice.Step + 0.1f, "not next to Mira")
             .Check(() => B.Sim.State.Tick > 0, "the clock does not run")
             .Call(() => s_tab = W.Position)
             .ToggleView().Wait(0.5f)
             .MeasureFps("god view default", 2f)
             .Call(() => B.GodCam.FrameIsland())
             .MeasureFps("god view whole island", 2f)
             .Call(() => B.GodCam.FrameHome())
             .ToggleView().Wait(0.3f)
             .Check(() => (W.Position - s_tab).magnitude < 0.001f, "the player moved across F1")
             .Log("[AutoPlay] boot_tour OK")
             .Quit(0);
            return s;
        }
        private static Vector3 s_tab;

        /// <summary>
        /// "walk_tour" (package P): steps round the start camp on the lattice by keys (forward, turns, a back step and a
        /// bump into the storehouse), opens and closes the map and the HUD, auto-walks from the camp to the neighbouring
        /// corner (logs the time), comes back, talks to Mira by tapping her and leaves her bubble, quits 0.
        /// </summary>
        public static AutoPlayScript WalkTour()
        {
            var s = new AutoPlayScript("walk_tour");
            float t0 = 0; int bumps0 = 0;
            s.Wait(1f)
             .Check(() => W.Cell == Camp.Spawn, "walker not on the spawn cell")
             // Round the camp by keys: towards the corner (blocked: bump), then sideways.
             .Call(() => bumps0 = W.BumpCount)
             .LookAt(() => Camp.Hearth)                            // face the corner (4 steps away)
             .Step(LatticeMove.Forward, 3)                         // 3 steps: next to the storehouse
             .Step(LatticeMove.Forward)                            // into the storehouse: bump
             .Check(() => W.BumpCount == bumps0 + 1, "no bump at the storehouse")
             .Check(() => WalkGrid.Distance(W.Cell, WalkLattice.ToCell(Camp.Hearth)) == 1, "not beside the storehouse")
             .Turn(-1).Step(LatticeMove.Forward, 2).Turn(2).Step(LatticeMove.Forward, 2).Step(LatticeMove.Back)
             .Capture("walk_tour_camp")
             .OpenMap().Wait(0.5f).Check(() => MapScreen.IsOpen, "map did not open").Capture("walk_tour_map").CloseMap()
             .Check(() => !MapScreen.IsOpen, "map did not close")
             .ToggleHud().Wait(0.4f).Check(() => Hud.Visible, "HUD did not show").Capture("walk_tour_hud").ToggleHud()
             .Check(() => !Hud.Visible, "HUD did not hide")
             // From the camp to the neighbouring corner.
             .Call(() => { var st = NeighbourStart(out _, out int h); W.Teleport(st, h); t0 = Time.time; })
             .WalkTo(() => { NeighbourStart(out var c, out _); return c; }, 40f)
             .Call(() => { s_cornerSeconds = Time.time - t0; Debug.Log("[AutoPlay] walk_tour: camp to the neighbouring corner in " + s_cornerSeconds.ToString("0.00") + " s (" + W.StepCount + " steps so far)"); })
             .WalkTo(() => Camp.Spawn, 40f)
             // Talk to Mira with the pointer, answer with a reply.
             .WalkToObject(() => Npc.Find(NpcIds.Mira))
             .TapObject(() => Npc.Find(NpcIds.Mira))
             .Check(() => DialoguePanel.IsOpen && SpeechBubbles.IsOpen(DialoguePanel.Handle), "Mira's bubble did not open")
             .Wait(0.5f).Capture("walk_tour_mira")
             .PickReply(1) // "Maybe later"
             .Check(() => !DialoguePanel.IsOpen, "Mira's bubble did not close on the reply")
             .Check(() => EncounterHost.Instance == null || !EncounterHost.Instance.IsRunning, "an encounter started")
             .Log("[AutoPlay] walk_tour OK")
             .Quit(0);
            return s;
        }

        /// <summary>Seconds the last walk_tour took from the camp to the neighbouring corner.</summary>
        public static float LastCornerWalkSeconds => s_cornerSeconds;
        private static float s_cornerSeconds;

        /// <summary>
        /// The cell beside the home corner on the hex edge towards its first neighbouring corner (start), and that
        /// neighbouring corner's cell (out). 11 steps apart on one heading.
        /// </summary>
        public static Cell NeighbourStart(out Cell neighbour, out int heading)
        {
            var t = B.Sim.State.Board.Topology;
            int home = Camp.HomeCorner;
            int other = t.CornerNeighbours(home)[0];
            var a = WalkLattice.ToCell(BoardSpace.Vertex(t, home));
            neighbour = WalkLattice.ToCell(BoardSpace.Vertex(t, other));
            heading = WalkLattice.HeadingOf(BoardSpace.YawOf(BoardSpace.Vertex(t, other) - BoardSpace.Vertex(t, home)));
            return a + WalkLattice.Dir(heading);
        }

        private static bool BoardLooksNew()
        {
            var b = WorldBootstrap.Instance.Sim.State.Board;
            if (b.Topology.HexCount != 61 || b.StartCorners == null || b.StartCorners.Length != 2) return false;
            for (int h = 0; h < b.HexNumber.Length; h++)
            {
                int n = b.HexNumber[h];
                bool meadow = b.HexTerrain[h] == World.Sim.Terrain.Meadow;
                if (meadow ? n != 0 : (n < 2 || n > 12 || n == 7)) return false;
            }
            return true;
        }
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
            if (ContinueAfterLoad != null)
            {
                // A script that loaded a save (the scene reloaded under it) goes on here.
                var next = ContinueAfterLoad; ContinueAfterLoad = null;
                Run(next);
                return;
            }
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

        /// <summary>
        /// A script to run in the next scene (set just before loading a save or starting a new game: the reload ends
        /// the running script, so its second half continues here).
        /// </summary>
        public static AutoPlayScript ContinueAfterLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ContinueAfterLoad = null; }

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

        private static PlayerWalker Walker => WorldBootstrap.Instance.Avatar;

        /// <summary>The nearest villager with a dialogue (an IInteractable person) within range, or null.</summary>
        public static Component NearestInteractable(float range)
        {
            Component best = null; float bd = range;
            var me = Walker.Position;
            foreach (var n in Npc.All)
            {
                if (n == null) continue;
                float d = BoardSpace.Flat(n.transform.position - me);
                if (d <= bd) { bd = d; best = n; }
            }
            return best;
        }

        internal IEnumerator DoTeleport(Vector3 pos, float yaw, float pitch)
        {
            Walker.Teleport(pos, yaw, pitch);
            yield return null;
        }

        internal IEnumerator DoTeleportCell(Func<Cell> cell, int heading)
        {
            Walker.StopAuto();
            Walker.Teleport(cell(), heading);
            yield return null;
        }

        internal IEnumerator DoMoves(LatticeMove m, int times)
        {
            for (int i = 0; i < times; i++)
            {
                float end = Time.time + 5f;
                while ((Walker.IsGliding || Walker.IsLocked) && Time.time < end) yield return null;
                switch (m)
                {
                    case LatticeMove.Forward: Walker.Forward(); break;
                    case LatticeMove.Back: Walker.Back(); break;
                    case LatticeMove.TurnLeft: Walker.TurnLeft(); break;
                    case LatticeMove.TurnRight: Walker.TurnRight(); break;
                }
                yield return null;
                while (Walker.IsGliding && Time.time < end) yield return null;
            }
        }

        internal IEnumerator DoWaitIdle(float timeout)
        {
            float end = Time.time + timeout;
            while ((Walker.IsGliding || Walker.IsAutoWalking) && Time.time < end) yield return null;
            if (Walker.IsGliding || Walker.IsAutoWalking) Fail("the walker did not come to rest");
        }

        internal IEnumerator DoWalkToCell(Func<Cell> target, float timeout)
        {
            float end = Time.time + timeout;
            while (Walker.IsGliding && Time.time < end) yield return null;
            var goal = target();
            if (Walker.Cell == goal) yield break;
            if (!Walker.WalkTo(goal)) { Fail("no path to " + goal); yield break; }
            while ((Walker.IsAutoWalking || Walker.IsGliding) && Time.time < end) yield return null;
            if (Walker.Cell != goal) Fail("walk to " + goal + " ended at " + Walker.Cell);
        }

        /// <summary>The cell to stand on to reach a point: within stop of it, or (stop under a step) the free neighbour nearest the player.</summary>
        public static Cell? ApproachCell(Vector3 point, float stop)
        {
            var c = WalkLattice.ToCell(point);
            if (stop >= WalkLattice.Step - 0.01f) return WalkGrid.NearestFree(c, 4);
            Cell? best = null; float bd = float.MaxValue;
            var me = Walker.Position;
            for (int h = 0; h < 6; h++)
            {
                var n = c + WalkLattice.Dir(h);
                if (WalkGrid.IsBlocked(n)) continue;
                float d = BoardSpace.Flat(WalkLattice.ToWorld(n) - me);
                if (d < bd) { bd = d; best = n; }
            }
            return best ?? WalkGrid.NearestFree(c, 4);
        }

        internal IEnumerator DoWalkTo(Func<Vector3> target, float stop, float timeout)
        {
            float end = Time.time + timeout;
            Cell? goal = null;
            while (Time.time < end)
            {
                var p = target();
                bool idle = !Walker.IsGliding;
                if (idle && BoardSpace.Flat(p - Walker.Position) <= stop) { Walker.StopAuto(); break; }
                var want = ApproachCell(p, stop);
                if (!want.HasValue) { Fail("nowhere to stand near " + p); yield break; }
                if (idle && Walker.Cell == want.Value && !Walker.IsAutoWalking) break;
                if (!goal.HasValue || goal.Value != want.Value)
                {
                    goal = want;
                    if (!Walker.WalkTo(want.Value)) { Fail("no path towards " + p); yield break; }
                }
                yield return null;
            }
            if (Time.time >= end) { Fail("walk timed out " + BoardSpace.Flat(target() - Walker.Position).ToString("0.0") + " m short"); yield break; }
            yield return DoFace(target);
        }

        internal IEnumerator DoFace(Func<Vector3> point)
        {
            float end = Time.time + 3f;
            while (Walker.IsGliding && Time.time < end) yield return null;
            var d = point() - Walker.Position; d.y = 0;
            if (d.magnitude < 0.5f) yield break;
            int want = WalkLattice.HeadingOf(BoardSpace.YawOf(d));
            int diff = ((want - Walker.Heading) % 6 + 6) % 6;
            if (diff == 0) yield break;
            yield return DoMoves(diff <= 3 ? LatticeMove.TurnRight : LatticeMove.TurnLeft, diff <= 3 ? diff : 6 - diff);
        }

        internal IEnumerator DoPickReply(int index, float timeout)
        {
            float end = Time.time + timeout;
            while (SpeechBubbles.NearestAsk(index + 1) == 0 && Time.time < end) yield return null;
            if (!SpeechBubbles.AnswerNearest(index)) Fail("no bubble with reply " + (index + 1));
            yield return null;
        }

        internal IEnumerator DoCastAtProposal(int rank, float timeout)
        {
            var s = WorldBootstrap.Instance.Sim.State;
            var list = FactionApi.Proposals(s, PlayerIds.Human);
            if (rank < 0 || rank >= list.Count) { Fail("no proposal at rank " + rank); yield break; }
            int id = list[rank].Id;
            if (!FactionApi.ProposalTarget(s, id, out double x, out double z, out _)) { Fail("proposal " + id + " has no target"); yield break; }
            var target = new Vector3((float)x, 0, (float)z);
            float range = (float)FactionApi.CastRangeMetres(s);
            if (!SpellCaster.InRange(id)) yield return DoWalkTo(() => target, range - 1f, timeout);
            if (Failed) yield break;
            yield return DoFace(() => target);
            var outcome = PointerInput.TapGround(target);
            if (outcome != TapOutcome.Cast) Fail("cast of proposal " + id + " refused: " + SpellCaster.LastResult.Message);
            yield return null;
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
