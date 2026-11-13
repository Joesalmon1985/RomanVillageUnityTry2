using System;
using System.Collections.Generic;
using World.Sim.Economy;

namespace World.Sim.Tests
{
    /// <summary>Helpers for economy tests: placement search, road connection, invariant checks.</summary>
    internal static class EconomyTestKit
    {
        public const ulong DefaultSeed = 20261113UL;

        /// <summary>Vertices currently on the carrier network: the storehouse and every road end.</summary>
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

        /// <summary>Shortest all-segment path from v to the nearest network flag (lowest id on ties); [v] if v is on it.</summary>
        public static int[] PathToNetwork(WorldState s, int v)
        {
            var topo = s.Board.Topology;
            var dist = topo.DistancesFrom(v);
            int best = -1;
            foreach (int n in NetworkVertices(s))
                if (dist[n] >= 0 && (best < 0 || dist[n] < dist[best])) best = n;
            return best < 0 ? null : topo.FindPath(v, best);
        }

        /// <summary>Planks for a building's road connection of n new segments (road credit counted).</summary>
        public static int RoadPlanks(WorldState s, int segments) => EconomyApi.RoadCostFor(s, segments)[(int)Ware.Planks];

        /// <summary>Best free vertex for a building: valid placement, filter ok, fewest new roads, then lowest id.</summary>
        public static int ChooseVertex(WorldState s, BuildingType type, Func<int, bool> filter = null)
        {
            int best = -1, bestCost = int.MaxValue;
            for (int v = 0; v < s.Board.Topology.VertexCount; v++)
            {
                if (!EconomyApi.CheckPlacement(s, PlayerIds.Human, type, v).Ok) continue;
                if (filter != null && !filter(v)) continue;
                var p = PathToNetwork(s, v);
                if (p == null) continue;
                int cost = 0;
                var edges = s.Board.Topology.PathEdges(p);
                foreach (int e in edges) if (!s.Economy.Roads[e]) cost++;
                // Fewest new roads first, then closest (by edges) to the storehouse.
                cost = cost * 100 + s.Board.Topology.Distance(v, s.Board.StorehouseVertex);
                if (cost < bestCost)
                {
                    best = v;
                    bestCost = cost;
                }
            }
            return best;
        }

        /// <summary>Road edges (not yet built) from v to the network, in an order each one is placeable after the previous.</summary>
        public static List<int> RoadsToConnect(WorldState s, int v)
        {
            var list = new List<int>();
            var p = PathToNetwork(s, v);
            if (p == null) return list;
            foreach (int e in s.Board.Topology.PathEdges(p)) if (!s.Economy.Roads[e]) list.Add(e);
            return list;
        }

        public static void Submit(WorldSim sim, Command c)
        {
            var r = sim.Submit(c);
            if (!r.Ok) throw new InvalidOperationException("Command refused: " + c.GetType().Name + " " + r);
        }

        /// <summary>Place a building, connect it by road (one command per tick), and step until Active.</summary>
        public static int Build(WorldSim sim, BuildingType type, Func<int, bool> filter = null, int maxTicks = 5000)
        {
            var s = sim.State;
            int v = ChooseVertex(s, type, filter);
            if (v < 0) throw new InvalidOperationException("No vertex for " + type);
            Submit(sim, new PlaceBuilding { Type = type, Vertex = v });
            sim.Step();
            foreach (int e in RoadsToConnect(s, v))
            {
                Submit(sim, new PlaceRoad { Edge = e });
                sim.Step();
            }
            int id = EconomyApi.BuildingAt(s, v);
            for (int i = 0; i < maxTicks && s.Economy.Buildings[id].State != BuildingState.Active; i++) sim.Step();
            if (s.Economy.Buildings[id].State != BuildingState.Active) throw new InvalidOperationException(type + " never finished");
            return id;
        }

        /// <summary>Hex touching the home city with the given terrain.</summary>
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

        /// <summary>Null when all logistics invariants hold, else a description of the first failure.</summary>
        public static string CheckInvariants(WorldState s)
        {
            var e = s.Economy;
            var inWorld = EconomyApi.WaresInWorld(s);
            var l = e.Ledger;
            for (int w = 0; w < Wares.Count; w++)
            {
                int expected = l.Starting[w] + l.Harvested[w] + l.Produced[w] + l.Added[w] - l.Consumed[w];
                if (inWorld[w] != expected) return $"ledger {(Ware)w}: in world {inWorld[w]} expected {expected}";
            }
            var ids = new HashSet<int>();
            foreach (var j in e.Jobs)
            {
                if (!ids.Add(j.Id)) return "duplicate job id " + j.Id;
                var src = e.Buildings[j.SourceId];
                var dst = e.Buildings[j.DestId];
                if (dst.State == BuildingState.Removed) return "job " + j.Id + " to removed building";
                if (j.State == JobState.Waiting && j.CarrierId != -1) return "waiting job with carrier";
                if (j.State != JobState.Waiting)
                {
                    var c = e.Carriers[j.CarrierId];
                    if (c.JobId != j.Id) return "carrier/job mismatch " + j.Id;
                    if ((j.State == JobState.Carrying) != c.HasLoad) return "load mismatch on job " + j.Id;
                    if (c.HasLoad && c.Load != j.Ware) return "wrong load on job " + j.Id;
                }
                if (src.State == BuildingState.Removed && j.State != JobState.Carrying) return "reservation at removed building";
            }
            foreach (var c in e.Carriers)
            {
                if (c.HasLoad && (c.JobId < 0 || EconomyLogicView.Job(s, c.JobId) == null)) return "carrier " + c.Id + " holds a load without a job";
                if (c.JobId >= 0 && EconomyLogicView.Job(s, c.JobId) == null) return "carrier " + c.Id + " has a stale job";
            }
            foreach (var b in e.Buildings)
            {
                for (int w = 0; w < Wares.Count; w++)
                {
                    int reserved = 0, incoming = 0;
                    foreach (var j in e.Jobs)
                    {
                        if ((int)j.Ware != w) continue;
                        if (j.SourceId == b.Id && j.State != JobState.Carrying) reserved++;
                        if (j.DestId == b.Id) incoming++;
                    }
                    if (b.Reserved[w] != reserved) return $"building {b.Id} reserved {(Ware)w} {b.Reserved[w]} != jobs {reserved}";
                    if (b.Incoming[w] != incoming) return $"building {b.Id} incoming {(Ware)w} {b.Incoming[w]} != jobs {incoming}";
                    if (b.Stock[w] < b.Reserved[w]) return $"building {b.Id} reserved more {(Ware)w} than in stock";
                    if (b.Stock[w] < 0 || b.Requested[w] < 0) return $"building {b.Id} negative {(Ware)w}";
                    if (b.State == BuildingState.Removed && b.Stock[w] != 0) return "removed site still holds wares";
                }
                if (b.Reason != null && !Reasons.IsKnown(b.Reason)) return "unknown reason " + b.Reason;
            }
            foreach (var j in e.Jobs) if (j.Reason != null && !Reasons.IsKnown(j.Reason)) return "unknown job reason " + j.Reason;
            return null;
        }

        private static class EconomyLogicView
        {
            public static DeliveryJob Job(WorldState s, int id)
            {
                foreach (var j in s.Economy.Jobs) if (j.Id == id) return j;
                return null;
            }
        }
    }

    /// <summary>
    /// Headless commission player: places a woodcutter on the home forest, a quarry on the home hills
    /// and a sawmill (the forester site is pre-placed), connects each by road, and ends the round
    /// whenever the home forest and hills batches are used up. No trading.
    /// </summary>
    internal sealed class CommissionScript
    {
        private readonly WorldSim _sim;
        private readonly Queue<Command> _queue = new Queue<Command>();
        private readonly int _forestHex, _hillsHex;

        public CommissionScript(WorldSim sim)
        {
            _sim = sim;
            _forestHex = EconomyTestKit.HomeHex(sim.State, Terrain.Forest);
            _hillsHex = EconomyTestKit.HomeHex(sim.State, Terrain.Hills);
        }

        /// <summary>Submit at most one command (call once before each Step).</summary>
        public void Act()
        {
            var s = _sim.State;
            if (_queue.Count == 0) Plan(s);
            if (_queue.Count == 0) return;
            var c = _queue.Dequeue();
            _sim.Submit(c);
        }

        private void Plan(WorldState s)
        {
            if (TryPlan(s, BuildingType.Woodcutter, EconomyTestKit.Touches(s, _forestHex))) return;
            if (TryPlan(s, BuildingType.Quarry, EconomyTestKit.Touches(s, _hillsHex))) return;
            if (TryPlan(s, BuildingType.Sawmill, null)) return;
            if (s.Economy.RoundStartPending || _sim.PendingCommands.Count > 0) return;
            if (s.Economy.HexBatchRemaining[_forestHex] == 0 && s.Economy.HexBatchRemaining[_hillsHex] == 0)
                _queue.Enqueue(new EndRound { PlayerId = PlayerIds.Human });
        }

        private bool TryPlan(WorldState s, BuildingType t, Func<int, bool> filter)
        {
            if (EconomyApi.FindBuilding(s, t) >= 0) return false;
            int v = EconomyTestKit.ChooseVertex(s, t, filter);
            if (v < 0) return false;
            _queue.Enqueue(new PlaceBuilding { PlayerId = PlayerIds.Human, Type = t, Vertex = v });
            // Roads are computed now (before the site exists); each one touches the site or the previous road.
            foreach (int e in EconomyTestKit.RoadsToConnect(s, v)) _queue.Enqueue(new PlaceRoad { PlayerId = PlayerIds.Human, Edge = e });
            return true;
        }
    }
}
