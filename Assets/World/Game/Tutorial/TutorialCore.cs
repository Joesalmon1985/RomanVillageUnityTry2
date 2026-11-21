using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Where "this game's tutorial is done" is remembered: PlayerPrefs on the player's device (key per game). Automated runs
    /// (tests, -autoplay smoke scripts) use a memory store instead so they never write the registry or leak between runs.
    /// </summary>
    public static class TutorialStore
    {
        public const string Prefix = "WorldMVP.Tutorial.Done.";
        /// <summary>True: keep the flags in memory only (tests, autoplay).</summary>
        public static bool UseMemory = HasArg("-autoplay") || HasArg("-runTests");
        private static readonly HashSet<string> Memory = new HashSet<string>();

        private static bool HasArg(string a) { foreach (var s in Environment.GetCommandLineArgs()) if (s == a) return true; return false; }

        public static bool IsDone(string game)
        {
            if (UseMemory) return Memory.Contains(game);
            try { return PlayerPrefs.GetInt(Prefix + game, 0) != 0; } catch { return Memory.Contains(game); }
        }

        public static void SetDone(string game, bool done)
        {
            if (done) Memory.Add(game); else Memory.Remove(game);
            if (UseMemory) return;
            try { PlayerPrefs.SetInt(Prefix + game, done ? 1 : 0); PlayerPrefs.Save(); } catch { }
        }

        public static void ClearMemory() => Memory.Clear();
    }

    /// <summary>One thing the tutor says and the thing it points at, finished by a condition read from the game.</summary>
    public sealed class TutorialStep
    {
        /// <summary>Short id, for tests and logs.</summary>
        public string Id;
        /// <summary>The tutor's name on the bubble ("The prince").</summary>
        public string TutorName;
        /// <summary>The tutor as a person in the world (the bubble hangs over the head); null = <see cref="TutorRect"/> or the top of the screen.</summary>
        public Func<ISpeaker> Speaker;
        /// <summary>The tutor when it is a thing on the screen (the prince on the altar ledge): its rect in GUI coordinates.</summary>
        public Func<Rect?> TutorRect;
        /// <summary>The line, in our words.</summary>
        public string Line;
        /// <summary>The line when it depends on the game now (overrides <see cref="Line"/>).</summary>
        public Func<string> LineFn;
        /// <summary>Runs once when the step is first shown (forget readings that belong to an earlier step).</summary>
        public Action OnEnter;
        /// <summary>The ring: the screen rect (GUI coordinates) of what to tap now; null = no ring.</summary>
        public Func<Rect?> Target;
        /// <summary>True when the player did it (read from the game). Also true when they had already done it before the step came: the step is then skipped.</summary>
        public Func<bool> Done;
        /// <summary>The step stays at least this long (seconds) once it has been shown, so a line is never gone before it can be read.</summary>
        public float MinSeconds = 1.2f;
        /// <summary>For steps that only tell: they finish after this many seconds instead of a condition (0 = use <see cref="Done"/>).</summary>
        public float AutoSeconds;
    }

    /// <summary>A hint that is said when something happens, outside the main order (a 7 and the discard sack, an envoy): it never blocks the steps.</summary>
    public sealed class TutorialTip
    {
        public string Id, TutorName, Line;
        public Func<ISpeaker> Speaker;
        public Func<Rect?> TutorRect;
        public Func<Rect?> Target;
        /// <summary>Says the tip when this becomes true.</summary>
        public Func<bool> Trigger;
        /// <summary>The tip goes away when this becomes true (or after <see cref="Seconds"/>).</summary>
        public Func<bool> Resolved;
        public float Seconds = 12f;
        public bool Shown;
    }

    /// <summary>One game's tutorial: its steps, when its place is open, and the latches its conditions read.</summary>
    public sealed class Tutorial
    {
        public string Id, Title;
        /// <summary>True while the player is in this game's place (the board is open, the lodge is near, the council is called...). The tutorial pauses when false.</summary>
        public Func<bool> GameActive;
        /// <summary>True when the first step may begin (default: <see cref="GameActive"/>).</summary>
        public Func<bool> CanStart;
        /// <summary>Builds the steps fresh when the tutorial starts or is replayed, so counters taken at the start are the baseline.</summary>
        public Func<Tutorial, List<TutorialStep>> BuildSteps;
        public Func<Tutorial, List<TutorialTip>> BuildTips;
        /// <summary>Called once when the tutorial starts or restarts (take baselines here).</summary>
        public Action<Tutorial> OnBegin;
        /// <summary>Called for each sim event while this tutorial runs (set flags, read by the step conditions).</summary>
        public Action<Tutorial, World.Sim.SimEvent> OnSim;
        /// <summary>Called when a world action previews, confirms or inspects while this tutorial runs.</summary>
        public Action<Tutorial, WorldActionMachine> OnAction;
        /// <summary>True: the bubble sits at the top of the safe area (the board, where the tutor is on the ledge at the bottom).</summary>
        public bool BubbleAtTop;
        /// <summary>Called every frame while this is the running tutorial, even when its place is not open (read the game, set flags).</summary>
        public Action<Tutorial> OnUpdate;
        public string Outro;
        public string OutroTutorName;
        public Func<ISpeaker> OutroSpeaker;
        public Func<Rect?> OutroTutorRect;

        public List<TutorialStep> Steps = new List<TutorialStep>();
        public List<TutorialTip> Tips = new List<TutorialTip>();
        public int Index;
        public bool Running, Finished;
        /// <summary>Started from "Show me how again": every step is shown, even one whose condition already holds (it is then read for a few seconds and goes on).</summary>
        public bool Replay;
        /// <summary>Things that happened since the start (set by the game's hooks, read by the conditions).</summary>
        public readonly HashSet<string> Flags = new HashSet<string>();
        public readonly Dictionary<string, int> Counters = new Dictionary<string, int>();
        /// <summary>Titles of ledger tablets the player opened since the start.</summary>
        public readonly List<string> Inspected = new List<string>();
        /// <summary>Titles of actions the player confirmed since the start.</summary>
        public readonly List<string> Committed = new List<string>();
        /// <summary>Titles of actions the player previewed since the start.</summary>
        public readonly List<string> Previewed = new List<string>();

        public TutorialStep Current => Running && Index >= 0 && Index < Steps.Count ? Steps[Index] : null;
        public bool Flag(string f) => Flags.Contains(f);
        public void Set(string f) => Flags.Add(f);
        public int Count(string c) => Counters.TryGetValue(c, out var n) ? n : 0;
        public void Bump(string c) => Counters[c] = Count(c) + 1;
        public bool SawInspect(string part) { foreach (var t in Inspected) if (t != null && t.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
        public bool DidCommit(string part) { foreach (var t in Committed) if (t != null && t.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
        public bool DidPreview(string part) { foreach (var t in Previewed) if (t != null && t.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

        internal void Reset()
        {
            Flags.Clear(); Counters.Clear(); Inspected.Clear(); Committed.Clear(); Previewed.Clear();
            Index = 0; Finished = false; Running = false;
            Steps = BuildSteps != null ? BuildSteps(this) : new List<TutorialStep>();
            Tips = BuildTips != null ? BuildTips(this) : new List<TutorialTip>();
        }
    }

    /// <summary>Helpers that turn things in the world or on the ledge into the screen rects the ring and the bubble use.</summary>
    public static class TutorialTargets
    {
        /// <summary>The camera that draws the scene the player sees now.</summary>
        public static Camera SceneCamera()
        {
            var v = ViewSwitcher.Instance;
            var c = v != null ? v.ActiveCamera : null;
            if (c != null && c.enabled && c.gameObject.activeInHierarchy) return c;
            Camera best = null;
            foreach (var cam in Camera.allCameras)
            {
                if (!cam.enabled || cam.targetTexture != null || cam.cullingMask == (1 << AltarLedgeRig.Layer)) continue;
                if (best == null || cam.depth > best.depth) best = cam;
            }
            return best != null ? best : Camera.main;
        }

        /// <summary>Screen rect of a scene object's renderers and colliders.</summary>
        public static Rect? Of(Component c)
        {
            if (c == null || !c.gameObject.activeInHierarchy) return null;
            var cam = SceneCamera();
            return WorldHitTarget.ScreenBounds(c, cam, out var r) ? r : (Rect?)null;
        }

        public static Rect? Of(Transform t) => t == null ? null : Of((Component)t);

        /// <summary>Screen rect of a scene object seen by a given camera (an encounter's own camera).</summary>
        public static Rect? Of(Component c, Camera cam)
        {
            if (c == null || !c.gameObject.activeInHierarchy || cam == null) return null;
            return WorldHitTarget.ScreenBounds(c, cam, out var r) ? r : (Rect?)null;
        }

        /// <summary>Screen rect of a world point (a small box round it).</summary>
        public static Rect? OfPoint(Vector3 world, float sidePx = 0f)
        {
            var cam = SceneCamera();
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(world);
            if (s.z <= 0) return null;
            float side = sidePx > 0 ? sidePx : Screen.height * 0.09f;
            return new Rect(s.x - side / 2, Screen.height - s.y - side / 2, side, side);
        }

        /// <summary>Screen rect of a world point seen by a given camera.</summary>
        public static Rect? OfPoint(Vector3 world, Camera cam, float sidePx = 0f)
        {
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(world);
            if (s.z <= 0) return null;
            float side = sidePx > 0 ? sidePx : Screen.height * 0.09f;
            return new Rect(s.x - side / 2, Screen.height - s.y - side / 2, side, side);
        }

        /// <summary>Screen rect round a screen point given in pixels (origin bottom-left, as the board's spot helpers return).</summary>
        public static Rect? OfScreenPx(Vector2 px, float sidePx = 0f)
        {
            float side = sidePx > 0 ? sidePx : Screen.height * 0.1f;
            return new Rect(px.x - side / 2, Screen.height - px.y - side / 2, side, side);
        }

        /// <summary>Screen rect of a thing on the altar ledge (by its layout id: "hourglass", "basket:0", "bench:road", ...).</summary>
        public static Rect? Ledge(string id)
        {
            var d = GodBoardDirector.Instance;
            if (d == null) return null;
            var hud = d.GetComponent<GodBoardHud>();
            var rig = hud != null ? hud.Rig : null;
            return rig != null && rig.Shown && rig.HitRect(id, out var r) ? r : (Rect?)null;
        }

        /// <summary>Union of several rects (null ones ignored); null if none.</summary>
        public static Rect? Union(params Rect?[] rs)
        {
            Rect? u = null;
            foreach (var r in rs)
            {
                if (!r.HasValue) continue;
                if (!u.HasValue) u = r;
                else { var a = u.Value; var b = r.Value; u = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)); }
            }
            return u;
        }

        /// <summary>The tutor as an NPC by id (the prince, Mira, Sol...), or null.</summary>
        public static Func<ISpeaker> Npc(string id) => () =>
        {
            foreach (var n in World.Game.Npc.All) if (n != null && n.Id == id) return n;
            return null;
        };
    }
}
