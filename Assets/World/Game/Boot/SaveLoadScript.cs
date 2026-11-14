using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Save;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// AutoPlay "save_load" (package F-1; packaged smoke test of the save slot; uses the AutoPlay slot file, never the
    /// player's): waits for a carrier of your people walking with a load, steps a few cells from the spawn, saves (the
    /// F5 path), walks on; checks that a corrupt file and an older-version file are refused without leaving the game;
    /// loads (the F9 path: the scene reloads); then checks the tick, the walker's cell and heading, the time of day,
    /// the first-scene step and Ward progress, and that the load carried at the save is delivered exactly once.
    /// Logs "[AutoPlay] save_load OK" and quits 0.
    /// </summary>
    public static class SaveLoadScript
    {
        public const string Name = "save_load";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => AutoPlayScripts.Register(Name, Create);

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static Cell _cell;
        private static int _heading, _jobId, _ward, _scene, _delivered;
        private static long _tick;
        private static double _hour;

        public static AutoPlayScript Create()
        {
            var s = new AutoPlayScript(Name);
            s.Wait(1f)
             .Step(a => WaitFor(a, () => InFlight() != null, 120f, "no carrier of your people walked with a load"))
             .Step(LatticeMove.TurnRight).Step(LatticeMove.Forward, 2).Step(LatticeMove.TurnLeft, 2)
             .WaitIdle()
             .Step(a => WaitFor(a, () => InFlight() != null && B.Sim.PendingCommands.Count == 0, 120f, "no carrier walked with a load at the save"))
             .Call(() =>
             {
                 var c = InFlight();
                 _jobId = c.JobId;
                 _cell = B.Avatar.Cell; _heading = B.Avatar.Heading;
                 _ward = B.Sim.State.Ward.CompletedDuelsVsMira;
                 _scene = PrinceDirector.Instance != null ? PrinceDirector.Instance.FirstSceneStep : -1;
                 if (!B.Saves.Save()) throw new Exception("save refused: " + SaveSystem.LastMessage);
                 _tick = B.Sim.State.Tick;
                 _hour = DayNight.HourAtTick(_tick);
                 Debug.Log("[AutoPlay] save_load saved at tick " + _tick + " with carrier " + c.Id + " carrying " + c.LoadCount + " " + c.Load + " for job " + _jobId + ": " + SaveSystem.LastMessage);
             })
             .Step(LatticeMove.Forward, 2)
             .Wait(1.5f)
             // Refusals: a corrupt file and an older save leave the game running.
             .Call(() => CheckRefused("{ this is not a save", null))
             .Call(() => CheckRefused(File.ReadAllText(SaveSystem.SlotPath).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":3"), "older"))
             .Call(() =>
             {
                 AutoPlay.ContinueAfterLoad = AfterLoad();
                 if (!B.Saves.Load()) { AutoPlay.ContinueAfterLoad = null; throw new Exception("load refused: " + SaveSystem.LastMessage); }
             })
             .Wait(30f); // the scene reloads under this script; AfterLoad goes on in the new scene
            return s;
        }

        private static void CheckRefused(string content, string mustContain)
        {
            string real = SaveSystem.SlotPath;
            string bad = Path.Combine(Path.GetDirectoryName(real), "worldmvp_autoplay_bad.json");
            File.WriteAllText(bad, content);
            var before = B;
            string old = SaveSystem.PathOverride;
            SaveSystem.PathOverride = bad;
            try
            {
                if (B.Saves.Load()) throw new Exception("a bad save was loaded");
            }
            finally { SaveSystem.PathOverride = old; File.Delete(bad); }
            if (!SaveSystem.LastWasError) throw new Exception("no refusal message");
            if (mustContain != null && !SaveSystem.LastMessage.Contains(mustContain)) throw new Exception("refusal does not say '" + mustContain + "': " + SaveSystem.LastMessage);
            if (WorldBootstrap.Instance != before) throw new Exception("the scene changed after a refused load");
            Debug.Log("[AutoPlay] save_load refused as expected: " + SaveSystem.LastMessage);
        }

        private static AutoPlayScript AfterLoad()
        {
            var s = new AutoPlayScript("save_load (after load)");
            // AutoPlay.Start may run before SaveSystem.Start has applied the saved pose: wait for it.
            s.Step(a => WaitFor(a, () => SaveSystem.LoadCount >= 1, 10f, "no load recorded"))
             .Call(() =>
             {
                 var st = B.Sim.State;
                 if (B.Avatar.Cell != _cell || B.Avatar.Heading != _heading) throw new Exception("walker not restored: " + B.Avatar.Cell + " h" + B.Avatar.Heading + " vs " + _cell + " h" + _heading);
                 if (B.Avatar.IsOffLattice) throw new Exception("the walker came back off the lattice");
                 if (st.Ward.CompletedDuelsVsMira != _ward) throw new Exception("Ward progress not restored");
                 if (st.Tick < _tick || st.Tick > _tick + 20) throw new Exception("loaded tick " + st.Tick + " vs saved " + _tick);
                 if (Math.Abs(DayNight.HourAtTick(st.Tick) - _hour) > 0.05) throw new Exception("time of day not restored");
                 int scene = PrinceDirector.Instance != null ? PrinceDirector.Instance.FirstSceneStep : -1;
                 if (scene < _scene) throw new Exception("first scene went back from step " + _scene + " to " + scene);
                 _delivered = 0;
                 B.Clock.Ticked += Count;
                 Debug.Log("[AutoPlay] save_load loaded at tick " + st.Tick + "; walker, time of day, first scene step " + scene + " and Ward progress restored: " + SaveSystem.LastMessage);
             })
             .Step(a => WaitFor(a, () => _delivered > 0 || InFlightJob(_jobId) == null, 90f, "the load carried at the save never arrived after the load"))
             .Wait(3f)
             .Call(() =>
             {
                 B.Clock.Ticked -= Count;
                 if (_delivered != 1) throw new Exception("the load carried at the save arrived " + _delivered + " times after the load");
                 Debug.Log("[AutoPlay] save_load: the load of job " + _jobId + " arrived exactly once after the load");
             })
             .Log("[AutoPlay] save_load OK")
             .Quit(0);
            return s;
        }

        private static void Count(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events) if (e is WareDelivered d && d.JobId == _jobId) _delivered++;
        }

        /// <summary>A carrier of your people carrying a load with at least 2 seconds of its leg left.</summary>
        private static Carrier InFlight()
        {
            foreach (var c in B.Sim.State.Economy.Carriers)
                if (c.Faction == PlayerIds.Human && c.HasLoad && c.JobId >= 0 && c.State == CarrierState.Carrying && c.LegTicks - c.Progress >= 20) return c;
            return null;
        }

        private static Carrier InFlightJob(int jobId)
        {
            foreach (var c in B.Sim.State.Economy.Carriers) if (c.HasLoad && c.JobId == jobId) return c;
            return null;
        }

        private static IEnumerator WaitFor(AutoPlay a, Func<bool> cond, float timeout, string message)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) { a.Fail(message); yield break; }
                yield return null;
            }
        }
    }
}
