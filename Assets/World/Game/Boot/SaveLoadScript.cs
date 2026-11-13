using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Save;

namespace World.Game
{
    /// <summary>
    /// AutoPlay "save_load" (packaged smoke test of the save slot; uses the AutoPlay slot file, never the player's):
    /// waits for a carrier walking a road with a load, stands somewhere else in God View and saves (F5 path); moves on;
    /// checks that a corrupt file and an older-version file are refused without leaving the game; loads (F9 path, the
    /// scene reloads); then checks the pose, the view, Ward progress, and that the in-flight delivery arrives exactly once.
    /// Logs "[AutoPlay] save_load OK" and quits 0.
    /// </summary>
    public static class SaveLoadScript
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => AutoPlayScripts.Register("save_load", Create);

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static Vector3 _pos;
        private static float _yaw, _pitch;
        private static int _jobId, _ward;
        private static long _tick;
        private static int _delivered;

        public static AutoPlayScript Create()
        {
            var s = new AutoPlayScript("save_load");
            s.Wait(1f)
             .Step(a => WaitFor(a, () => InFlight() != null, 90f, "no carrier walked a road with a load"))
             .Call(() =>
             {
                 var c = InFlight();
                 _jobId = c.JobId;
                 Debug.Log("[AutoPlay] save_load carrier " + c.Id + " carries " + c.Load + " on segment " + c.CurrentEdge + " for job " + _jobId);
             })
             .Teleport(B.Layout.Spawn + new Vector3(6f, 0.05f, -4f), 123f, -7f)
             .Wait(0.3f)
             .ToggleView()
             .Call(() =>
             {
                 if (InFlightJob(_jobId) == null) throw new Exception("the shipment arrived before the save; rerun");
                 _pos = B.Avatar.Position; _yaw = B.Avatar.Yaw; _pitch = B.Avatar.Pitch;
                 _ward = B.Sim.State.Ward.CompletedDuelsVsMira;
                 if (!B.Saves.Save()) throw new Exception("save refused: " + SaveSystem.LastMessage);
                 _tick = B.Sim.State.Tick;
                 Debug.Log("[AutoPlay] save_load saved at tick " + _tick + ": " + SaveSystem.LastMessage);
             })
             .Teleport(B.Layout.Spawn, 0f)
             .ToggleView()
             .Wait(2f)
             // Refusals: a corrupt file and an older save leave the game running.
             .Call(() => CheckRefused("{ this is not a save", null))
             .Call(() => CheckRefused(File.ReadAllText(SaveSystem.SlotPath).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":2"), "older"))
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
            SaveSystem.PathOverride = bad;
            try
            {
                if (B.Saves.Load()) throw new Exception("a bad save was loaded");
            }
            finally { SaveSystem.PathOverride = null; File.Delete(bad); }
            if (!SaveSystem.LastWasError) throw new Exception("no refusal message");
            if (mustContain != null && !SaveSystem.LastMessage.Contains(mustContain)) throw new Exception("refusal does not say '" + mustContain + "': " + SaveSystem.LastMessage);
            if (WorldBootstrap.Instance != before) throw new Exception("the scene changed after a refused load");
            Debug.Log("[AutoPlay] save_load refused as expected: " + SaveSystem.LastMessage);
        }

        private static AutoPlayScript AfterLoad()
        {
            var s = new AutoPlayScript("save_load (after load)");
            // AutoPlay.Start may run before SaveSystem.Start has applied the saved pose and view: wait for it.
            s.Step(a => WaitFor(a, () => SaveSystem.LoadCount >= 1, 10f, "no load recorded"))
             .Call(() =>
             {
                 if (SaveSystem.LoadCount < 1) throw new Exception("no load recorded");
                 if ((B.Avatar.Position - _pos).magnitude > 0.001f) throw new Exception("position not restored: " + B.Avatar.Position + " vs " + _pos);
                 if (Mathf.Abs(Mathf.DeltaAngle(B.Avatar.Yaw, _yaw)) > 0.1f || Mathf.Abs(B.Avatar.Pitch - _pitch) > 0.1f) throw new Exception("facing not restored");
                 if (B.Views.CurrentView != ViewMode.God) throw new Exception("view not restored");
                 if (B.Sim.State.Ward.CompletedDuelsVsMira != _ward) throw new Exception("Ward progress not restored");
                 if (B.Sim.State.Tick < _tick || B.Sim.State.Tick > _tick + 20) throw new Exception("loaded tick " + B.Sim.State.Tick + " vs saved " + _tick);
                 _delivered = 0;
                 B.Clock.Ticked += Count;
                 Debug.Log("[AutoPlay] save_load loaded at tick " + B.Sim.State.Tick + "; pose, view and Ward progress restored: " + SaveSystem.LastMessage);
             })
             .Step(a => WaitFor(a, () => _delivered > 0 || InFlightJob(_jobId) == null, 60f, "the in-flight shipment never arrived after the load"))
             .Wait(3f)
             .Call(() =>
             {
                 B.Clock.Ticked -= Count;
                 if (_delivered != 1) throw new Exception("the in-flight shipment arrived " + _delivered + " times after the load");
                 Debug.Log("[AutoPlay] save_load shipment of job " + _jobId + " arrived exactly once after the load");
             })
             .Log("[AutoPlay] save_load OK")
             .Quit(0);
            return s;
        }

        private static void Count(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events) if (e is WareDelivered d && d.JobId == _jobId) _delivered++;
        }

        private static Carrier InFlight()
        {
            foreach (var c in B.Sim.State.Economy.Carriers) if (c.HasLoad && c.CurrentEdge >= 0 && c.JobId >= 0) return c;
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
