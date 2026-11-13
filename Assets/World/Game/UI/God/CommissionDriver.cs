using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>
    /// Plays the quartermaster's commission through the God View tools (the same calls a click makes), following the
    /// headless EditMode CommissionScript: plans a woodcutter by the home forest, a clay pit by the home hills, then a
    /// sawmill, a charcoal kiln and a brick kiln (the forester site is pre-placed), connects each by road, and ends the
    /// round with the End Round button whenever the home forest and hills batches are used up. One command per tick,
    /// only when nothing is pending. Used by the "commission" AutoPlay script, the PlayMode test and captures.
    /// Planning walks the flags only when it plans a building (a handful of times), never per frame.
    /// </summary>
    public sealed class CommissionDriver
    {
        private readonly WorldBootstrap _boot;
        private readonly Queue<Func<CommandResult>> _queue = new Queue<Func<CommandResult>>();
        private readonly int _forestHex, _hillsHex;
        public string Failure { get; private set; }
        public int Actions { get; private set; }
        public int RoundsEnded { get; private set; }

        public CommissionDriver(WorldBootstrap boot)
        {
            _boot = boot;
            _forestHex = HomeHex(boot.Sim.State, Terrain.Forest);
            _hillsHex = HomeHex(boot.Sim.State, Terrain.Hills);
        }

        private WorldState S => _boot.Sim.State;

        /// <summary>Submits at most one command through a tool (only when nothing is pending). Call once per tick.</summary>
        public void Act()
        {
            if (Failure != null || _boot.Sim.PendingCommands.Count > 0 || S.Economy.Commission.Complete) return;
            if (_queue.Count == 0) Plan();
            if (_queue.Count == 0) return;
            var r = _queue.Dequeue()();
            Actions++;
            if (!r.Ok) Failure = "command refused: " + r.Reason + " - " + r.Message;
        }

        private void Plan()
        {
            var s = S;
            if (TryPlan(BuildingType.Woodcutter, Touches(s, _forestHex))) return;
            if (TryPlan(BuildingType.ClayPit, Touches(s, _hillsHex))) return;
            if (TryPlan(BuildingType.Sawmill, null)) return;
            if (TryPlan(BuildingType.CharcoalKiln, null)) return;
            if (TryPlan(BuildingType.BrickKiln, null)) return;
            if (s.Economy.RoundStartPending) return;
            if (s.Economy.HexBatchRemaining[_forestHex] == 0 && s.Economy.HexBatchRemaining[_hillsHex] == 0)
                _queue.Enqueue(() =>
                {
                    GodToolbar.Instance.Activate(EconomyGodTools.EndRoundId);
                    RoundsEnded++;
                    return GodCommands.LastCommand is EndRound ? GodCommands.LastResult : CommandResult.Reject("no_end_round", "End Round did not run.");
                });
        }

        private bool TryPlan(BuildingType t, Func<int, bool> filter)
        {
            var s = S;
            if (EconomyApi.FindBuilding(s, t) >= 0) return false;
            int v = ChooseVertex(s, t, filter);
            if (v < 0) return false;
            var roads = RoadsToConnect(s, v);
            _queue.Enqueue(() =>
            {
                var tool = GodToolbar.Instance.Activate(BuildTool.ToolId) as BuildTool;
                tool.SelectType(t);
                return tool.TryPlace(v);
            });
            foreach (int e in roads)
                _queue.Enqueue(() => ((RoadTool)GodToolbar.Instance.Activate(RoadTool.ToolId)).Place(e));
            return true;
        }

        // ---------------- planning helpers (same rules as the headless script) ----------------

        public static int HomeHex(WorldState s, Terrain t)
        {
            foreach (int h in s.Board.Topology.VertexHexes(s.Board.HomeCityVertex)) if (s.Board.HexTerrain[h] == t) return h;
            return -1;
        }

        public static Func<int, bool> Touches(WorldState s, int hex) => v =>
        {
            foreach (int h in s.Board.Topology.VertexHexes(v)) if (h == hex) return true;
            return false;
        };

        public static List<int> NetworkVertices(WorldState s)
        {
            var topo = s.Board.Topology;
            var on = new bool[topo.VertexCount];
            on[s.Economy.Buildings[s.Economy.StorehouseId].Vertex] = true;
            for (int e = 0; e < topo.EdgeCount; e++)
            {
                if (!s.Economy.Roads[e]) continue;
                on[topo.EdgeVertexA(e)] = true;
                on[topo.EdgeVertexB(e)] = true;
            }
            var list = new List<int>();
            for (int v = 0; v < on.Length; v++) if (on[v]) list.Add(v);
            return list;
        }

        /// <summary>Shortest all-segment path from v to the nearest network flag (lowest id on ties); [v] if on it.</summary>
        public static int[] PathToNetwork(WorldState s, int v)
        {
            var topo = s.Board.Topology;
            var dist = topo.DistancesFrom(v);
            int best = -1;
            foreach (int n in NetworkVertices(s))
                if (dist[n] >= 0 && (best < 0 || dist[n] < dist[best])) best = n;
            return best < 0 ? null : topo.FindPath(v, best);
        }

        /// <summary>Legal flag for the type: fewest new road segments, then closest to the storehouse.</summary>
        public static int ChooseVertex(WorldState s, BuildingType type, Func<int, bool> filter)
        {
            int best = -1, bestCost = int.MaxValue;
            foreach (int v in EconomyApi.LegalBuildFlags(s, type))
            {
                if (filter != null && !filter(v)) continue;
                var p = PathToNetwork(s, v);
                if (p == null) continue;
                int cost = 0;
                foreach (int e in s.Board.Topology.PathEdges(p)) if (!s.Economy.Roads[e]) cost++;
                cost = cost * 100 + s.Board.Topology.Distance(v, s.Board.StorehouseVertex);
                if (cost < bestCost) { best = v; bestCost = cost; }
            }
            return best;
        }

        public static List<int> RoadsToConnect(WorldState s, int v)
        {
            var list = new List<int>();
            var p = PathToNetwork(s, v);
            if (p == null) return list;
            foreach (int e in s.Board.Topology.PathEdges(p)) if (!s.Economy.Roads[e]) list.Add(e);
            return list;
        }

        // ---------------- AutoPlay ----------------

        /// <summary>
        /// Coroutine step: acts and advances the clock by <paramref name="ticksPerFrame"/> ticks per frame
        /// (GameClock.StepOnce, so the views get every tick) until the commission completes. The normal clock is
        /// paused meanwhile so ticks are not doubled.
        /// </summary>
        public static IEnumerator Drive(AutoPlay a, int ticksPerFrame, int tickBudget)
        {
            var boot = WorldBootstrap.Instance;
            var d = new CommissionDriver(boot);
            if (boot.Views.CurrentView != ViewMode.God) boot.Views.SetView(ViewMode.God);
            boot.GodCam.SetFocus(boot.Layout.VillageCentre + (boot.Layout.Home - boot.Layout.VillageCentre) * 0.3f, 95f);
            const string pause = "AutoPlay";
            boot.Clock.Pause(pause);
            long start = boot.Sim.State.Tick;
            try
            {
                while (!boot.Sim.State.Economy.Commission.Complete)
                {
                    for (int k = 0; k < ticksPerFrame && !boot.Sim.State.Economy.Commission.Complete; k++)
                    {
                        d.Act();
                        if (d.Failure != null) { a.Fail(d.Failure); yield break; }
                        boot.Clock.StepOnce();
                    }
                    if (boot.Sim.State.Tick - start > tickBudget) { a.Fail("commission not complete after " + tickBudget + " ticks"); yield break; }
                    yield return null;
                }
            }
            finally { boot.Clock.Resume(pause); }
            if (GodToolbar.Instance != null) GodToolbar.Instance.Activate(GodToolbar.SelectId);
            Debug.Log("[AutoPlay] commission complete at tick " + boot.Sim.State.Tick + " (round " + boot.Sim.State.Economy.Round + ", "
                + d.Actions + " tool actions, " + d.RoundsEnded + " rounds ended)");
        }

        /// <summary>The registered "commission" script: God View tools at speed, then Bram acknowledges in first person.</summary>
        public static AutoPlayScript Script(int ticksPerFrame = 6)
        {
            var s = new AutoPlayScript("commission");
            s.Wait(0.5f)
             .Check(() => !WorldBootstrap.Instance.Sim.State.Economy.Commission.Complete, "commission already complete at start")
             .Step(a => Drive(a, ticksPerFrame, 6000))
             .Wait(0.5f)
             .Check(() => EconomyFirstPerson.Instance != null && EconomyFirstPerson.Instance.CompletionSeen, "the completion was not shown")
             .Check(() => EconomyViews.Instance.BuildingViewCount >= EconomyLook.CommissionTypes.Length, "commission buildings have no views")
             .Call(() =>
             {
                 var boot = WorldBootstrap.Instance;
                 boot.Views.SetView(ViewMode.FirstPerson);
                 var qm = Npc.Find(NpcIds.Quartermaster);
                 boot.Avatar.Teleport(qm.transform.position + qm.transform.forward * 2.5f, BoardSpace.YawOf(-qm.transform.forward), 5f);
             })
             .Wait(0.3f)
             .Call(() => Npc.Find(NpcIds.Quartermaster).Interact())
             .Check(() => DialoguePanel.IsOpen && string.Join(" ", DialoguePanel.Instance.CurrentLines).Contains("commission is complete"), "Bram does not acknowledge the finished commission")
             .Check(() => EconomyFirstPerson.Instance.QuartermasterAcknowledged, "acknowledgement not recorded")
             .Wait(0.3f)
             .Call(DialoguePanel.Close)
             .Log("[AutoPlay] commission OK")
             .Quit(0);
            return s;
        }
    }
}
