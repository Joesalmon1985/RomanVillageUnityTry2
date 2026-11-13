using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Factions;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// AutoPlay "cast_all" (integration): with no prince to lead the way, the player walks on the lattice to the top
    /// proposal of its people, turns to it and presses its ground through the real pointer path (a screen point,
    /// <see cref="PointerInput.TapAt"/>: the same raycast, tap order and range check as a mouse click), again and again
    /// until the game ends. Driven by state only (no timings): it re-plans whenever the top proposal changes.
    /// Logs "[AutoPlay] cast_all result ..., game minute ..., casts ..., metres walked ..." then "[AutoPlay] cast_all OK".
    /// Packaged: <c>-autoplay cast_all</c>, optionally <c>-speed 4</c> (time scale; walking and the world speed up together).
    /// </summary>
    public static class CastAll
    {
        public const string Name = "cast_all";

        /// <summary>Time scale while the script runs (tests and captures set it; the command line's -speed overrides).</summary>
        public static float Speed = 1f;
        /// <summary>Real seconds before the script gives up.</summary>
        public static float RealTimeout = 2400f;
        /// <summary>Called after every successful cast (captures).</summary>
        public static Action<int, Proposal> OnCast;

        // Results of the last run (tests, the progress log).
        public static bool Finished { get; private set; }
        public static GameOutcome Result { get; private set; }
        public static string ResultSentence { get; private set; }
        public static double GameMinute { get; private set; }
        public static int Casts { get; private set; }
        /// <summary>Casts that went through a real screen-point press (the rest fell back to a ground-point press).</summary>
        public static int ScreenCasts { get; private set; }
        public static int Refused { get; private set; }
        public static float MetresWalked { get; private set; }
        public static readonly List<string> CastLog = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => AutoPlayScripts.Register(Name, Create);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Speed = 1f; RealTimeout = 2400f; OnCast = null; Finished = false; Casts = 0; ScreenCasts = 0; Refused = 0; MetresWalked = 0; CastLog.Clear(); }

        public static AutoPlayScript Create()
        {
            var s = new AutoPlayScript(Name);
            s.Wait(0.5f).Step(Run).Log("[AutoPlay] " + Name + " OK").Quit(0);
            return s;
        }

        private static float CommandLineSpeed()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-speed" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            return -1f;
        }

        /// <summary>A free cell to cast from: within range of the target, far enough that its ground is in view, nearest to the player.</summary>
        private static bool StandCells(Vector3 target, float range, PlayerWalker w, List<Cell> into)
        {
            into.Clear();
            var c = WalkLattice.ToCell(target);
            for (int di = -3; di <= 3; di++)
                for (int dj = -3; dj <= 3; dj++)
                {
                    var n = new Cell { I = c.I + di, J = c.J + dj };
                    float d = BoardSpace.Flat(WalkLattice.ToWorld(n) - target);
                    if (d < 4.5f || d > range - 1f || WalkGrid.IsBlocked(n)) continue;
                    into.Add(n);
                }
            var me = w.Position;
            into.Sort((a, b) => BoardSpace.Flat(WalkLattice.ToWorld(a) - me).CompareTo(BoardSpace.Flat(WalkLattice.ToWorld(b) - me)));
            return into.Count > 0;
        }

        private static IEnumerator Run(AutoPlay a)
        {
            var boot = WorldBootstrap.Instance;
            var w = boot.Avatar;
            var s = boot.Sim.State;
            Finished = false; Casts = 0; ScreenCasts = 0; Refused = 0; CastLog.Clear();
            int steps0 = w.StepCount;
            float speed = CommandLineSpeed(); if (speed <= 0) speed = Speed;
            float oldScale = Time.timeScale;
            Time.timeScale = Mathf.Clamp(speed, 0.1f, 16f);
            float end = Time.realtimeSinceStartup + RealTimeout;
            var cells = new List<Cell>();
            int stuck = 0;
            try
            {
                while (!FactionApi.Outcome(s).GameOver)
                {
                    if (Time.realtimeSinceStartup > end) { a.Fail("cast_all: no ending after " + RealTimeout + " s (tick " + s.Tick + ", casts " + Casts + ")"); yield break; }
                    var top = FactionApi.Top(s, PlayerIds.Human);
                    if (top == null) { yield return null; continue; }
                    int id = top.Id;
                    if (!FactionApi.ProposalTarget(s, id, out double tx, out double tz, out _)) { yield return null; continue; }
                    var target = new Vector3((float)tx, PlayerWalker.GroundHeight(new Vector3((float)tx, 0, (float)tz)), (float)tz);
                    float range = (float)FactionApi.CastRangeMetres(s);

                    // Walk until we stand on a cell the proposal can be cast from (re-plan if it stops being the top one).
                    bool inPlace = SpellCaster.InRange(id) && BoardSpace.Flat(w.Position - target) >= 4.4f && !w.IsGliding;
                    if (!inPlace)
                    {
                        if (!StandCells(target, range, w, cells)) { a.Fail("cast_all: nowhere to stand for " + FactionApi.ProposalTitle(s, id)); yield break; }
                        bool going = false;
                        foreach (var c in cells) { if (c == w.Cell || w.WalkTo(c)) { going = true; break; } }
                        if (!going) { if (++stuck > 600) { a.Fail("cast_all: no path to " + FactionApi.ProposalTitle(s, id)); yield break; } yield return null; continue; }
                        while (w.IsAutoWalking || w.IsGliding)
                        {
                            var now = FactionApi.Top(s, PlayerIds.Human);
                            if (now == null || now.Id != id || FactionApi.Outcome(s).GameOver) { w.StopAuto(); break; }
                            if (Time.realtimeSinceStartup > end) break;
                            yield return null;
                        }
                        while (w.IsGliding) yield return null;
                        continue; // look again: the top proposal may have changed on the way
                    }
                    stuck = 0;
                    yield return a.DoFace(() => target);
                    yield return null;
                    var again = FactionApi.Top(s, PlayerIds.Human);
                    if (again == null || again.Id != id) continue;

                    // Press the ground through the real pointer: a few screen points inside the press area, nearest first.
                    var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
                    var outcome = TapOutcome.None;
                    bool byScreen = false;
                    var toMe = w.Position - target; toMe.y = 0; toMe = toMe.sqrMagnitude > 0.01f ? toMe.normalized : Vector3.forward;
                    var side = new Vector3(toMe.z, 0, -toMe.x);
                    var tries = new[] { target, target + toMe * 1.5f, target + side * 1.5f, target - side * 1.5f, target + toMe * 3f };
                    foreach (var p in tries)
                    {
                        if (cam == null) break;
                        var sp = cam.WorldToScreenPoint(p);
                        if (sp.z <= 0.3f || sp.x < 2 || sp.y < 2 || sp.x > Screen.width - 2 || sp.y > Screen.height - 2) continue;
                        var hit = PointerInput.Raycast(sp);
                        if (hit.ProposalId != id || (hit.Target != null && (hit.Target is IFollowable || hit.Target is IInteractable))) continue;
                        outcome = PointerInput.TapAt(sp);
                        byScreen = true;
                        break;
                    }
                    if (!byScreen) outcome = PointerInput.TapGround(target); // nothing of the press area on screen: press the ground point
                    if (outcome == TapOutcome.Cast)
                    {
                        Casts++; if (byScreen) ScreenCasts++;
                        long secs = s.Tick / WorldSim.TicksPerSecond;
                        string line = "cast " + Casts + " at " + (secs / 60) + ":" + (secs % 60).ToString("00") + " " + top.Kind + (top.BuildingType >= 0 ? " " + (World.Sim.Economy.BuildingType)top.BuildingType : "") + (byScreen ? "" : " (ground press)");
                        CastLog.Add(line);
                        Debug.Log("[AutoPlay] cast_all " + line);
                        try { OnCast?.Invoke(Casts, top); } catch (Exception e) { Debug.LogWarning("[AutoPlay] cast_all OnCast: " + e.Message); }
                        // The command is applied on the next tick: wait until the sim has taken the proposal off the queue.
                        long tick0 = s.Tick;
                        while (FactionApi.GetProposal(s, id) != null && s.Tick < tick0 + 20 && Time.realtimeSinceStartup < end) yield return null;
                    }
                    else
                    {
                        Refused++;
                        if (Refused > 400) { a.Fail("cast_all: presses keep failing (" + outcome + ": " + SpellCaster.LastResult.Message + ")"); yield break; }
                        float until = Time.time + 0.5f;
                        while (Time.time < until) yield return null;
                    }
                    yield return null;
                }
            }
            finally { Time.timeScale = oldScale; }

            var o = FactionApi.Outcome(s);
            Result = s.Factions.Outcome; ResultSentence = o.Sentence;
            GameMinute = s.Factions.EndedTick / 600.0;
            MetresWalked = (w.StepCount - steps0) * WalkLattice.Step;
            Finished = true;
            Debug.Log("[AutoPlay] cast_all result " + Result + " (" + s.Factions.OutcomeReason + "), game minute " + GameMinute.ToString("0.0")
                + ", casts " + Casts + " (" + ScreenCasts + " by screen press, " + Refused + " refused presses), metres walked " + MetresWalked.ToString("0")
                + ", strength " + FactionApi.Strength(s, PlayerIds.Human).Total + " v " + FactionApi.Strength(s, PlayerIds.Rival).Total);
        }
    }
}
