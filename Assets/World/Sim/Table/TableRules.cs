using System;
using System.Collections.Generic;

namespace World.Sim.Table
{
    /// <summary>Read-only rules of the table (TU-3..TU-7, TR-1). Static, never mutate. Every loop runs in ascending id order.</summary>
    public static class TableRules
    {
        // ---- Costs (TU-3), counts per raw good: Wood, Clay, Grain, Sheep, Stone ----
        public static readonly int[] RoadCost = { 1, 1, 0, 0, 0 };
        public static readonly int[] SettlementCost = { 1, 1, 1, 1, 0 };
        public static readonly int[] CityCost = { 0, 0, 2, 0, 3 };

        public static TablePeople People(WorldState s, int p)
        {
            var t = s?.Table;
            if (t == null || p < 0 || p >= t.Peoples.Count) return null;
            return t.Peoples[p];
        }

        public static bool CanPay(int[] hand, int[] cost)
        {
            for (int g = 0; g < Wares.RawCount; g++) if (hand[g] < cost[g]) return false;
            return true;
        }

        public static int HandSize(int[] hand)
        {
            int n = 0;
            for (int g = 0; g < Wares.RawCount; g++) n += hand[g];
            return n;
        }

        // ---- Pieces (TU-5) ----

        public static int CountSettlements(TableState t, int p) => CountLevel(t, p, 1);
        public static int CountCities(TableState t, int p) => CountLevel(t, p, 2);

        public static int CountRoads(TableState t, int p)
        {
            int n = 0;
            for (int e = 0; e < t.RoadOwner.Length; e++) if (t.RoadOwner[e] == p) n++;
            return n;
        }

        private static int CountLevel(TableState t, int p, int level)
        {
            int n = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == p && t.CornerLevel[c] == level) n++;
            return n;
        }

        public static int RoadsLeft(TableState t, int p) => t.Tuning.MaxRoads - CountRoads(t, p);
        public static int SettlementsLeft(TableState t, int p) => t.Tuning.MaxSettlements - CountSettlements(t, p);
        public static int CitiesLeft(TableState t, int p) => t.Tuning.MaxCities - CountCities(t, p);

        // ---- Placement (TU-4) ----

        /// <summary>Null when a settlement may stand at the corner for p (connected = must touch p's road), else the refusal code.</summary>
        public static string SettlementRefusal(WorldState s, int p, int corner, bool needRoad)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            if (!topo.IsCorner(corner) || t.CornerOwner[corner] >= 0) return TableRefusals.InvalidSite;
            // G0-3: only corners where three hexes meet, like the world's camps and the ports (PlotLayout.IsSettlementCorner).
            if (!PlotLayout.IsSettlementCorner(topo, corner)) return TableRefusals.InvalidSite;
            // G0-3, WA-14: a burned settlement's ruin keeps its corner closed until the ruin time is over.
            if (t.ClosedCorners != null && t.ClosedCorners.Contains(corner)) return TableRefusals.InvalidSite;
            foreach (int n in topo.CornerNeighbours(corner)) if (t.CornerOwner[n] >= 0) return TableRefusals.TooClose;
            if (needRoad)
            {
                bool touches = false;
                foreach (int e in topo.CornerHexEdges(corner)) if (t.RoadOwner[e] == p) { touches = true; break; }
                if (!touches) return TableRefusals.NotConnected;
            }
            return null;
        }

        /// <summary>
        /// Null when p may lay a road on the hex edge, else the refusal code. The road must touch p's settlement, or p's road
        /// at a corner not held by another people. In setup (setupCorner &gt;= 0) it must touch that settlement.
        /// </summary>
        public static string RoadRefusal(WorldState s, int p, int edge, int setupCorner = -1)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            if (edge < 0 || edge >= topo.HexEdgeCount || t.RoadOwner[edge] >= 0) return TableRefusals.InvalidSite;
            int a = topo.HexEdgeCornerA(edge), b = topo.HexEdgeCornerB(edge);
            if (setupCorner >= 0) return a == setupCorner || b == setupCorner ? null : TableRefusals.NotConnected;
            return Reaches(s, p, a, edge) || Reaches(s, p, b, edge) ? null : TableRefusals.NotConnected;
        }

        private static bool Reaches(WorldState s, int p, int corner, int exceptEdge)
        {
            var t = s.Table;
            if (t.CornerOwner[corner] == p) return true;
            if (t.CornerOwner[corner] >= 0) return false; // another people's settlement cuts the road
            foreach (int e in s.Board.Topology.CornerHexEdges(corner)) if (e != exceptEdge && t.RoadOwner[e] == p) return true;
            return false;
        }

        /// <summary>Legal settlement corners for p, ascending.</summary>
        public static List<int> LegalSettlementCorners(WorldState s, int p, bool needRoad)
        {
            var list = new List<int>();
            foreach (int c in s.Board.Topology.Corners) if (SettlementRefusal(s, p, c, needRoad) == null) list.Add(c);
            return list;
        }

        /// <summary>Legal road edges for p, ascending.</summary>
        public static List<int> LegalRoadEdges(WorldState s, int p, int setupCorner = -1)
        {
            var list = new List<int>();
            int n = s.Board.Topology.HexEdgeCount;
            for (int e = 0; e < n; e++) if (RoadRefusal(s, p, e, setupCorner) == null) list.Add(e);
            return list;
        }

        // ---- Trading (TR-1) ----

        /// <summary>Best rate p pays to the bank for one card when giving good g: 4, 3 at a 3:1 port, 2 at that good's port.</summary>
        public static int TradeRate(WorldState s, int p, Ware g)
        {
            var t = s.Table;
            int rate = t.Tuning.BankRate;
            foreach (var port in t.Ports)
            {
                if (!UsesPort(t, p, port)) continue;
                if (port.Good < 0) rate = Math.Min(rate, t.Tuning.GenericPortRate);
                else if (port.Good == (int)g) rate = Math.Min(rate, t.Tuning.GoodPortRate);
            }
            return rate;
        }

        /// <summary>True when p holds a settlement or city on a corner of the port.</summary>
        public static bool UsesPort(TableState t, int p, TablePort port)
        {
            if (OwnsCorner(t, p, port.CornerA) || OwnsCorner(t, p, port.CornerB)) return true;
            if (port.Corners != null) foreach (int c in port.Corners) if (OwnsCorner(t, p, c)) return true;
            return false;
        }

        private static bool OwnsCorner(TableState t, int p, int c) => c >= 0 && c < t.CornerOwner.Length && t.CornerOwner[c] == p;

        // ---- Points (TU-6) ----

        /// <summary>Points computed from the board: settlements, cities and the longest road. Never banked.</summary>
        public static int Points(WorldState s, int p)
        {
            var t = s.Table;
            int pts = CountSettlements(t, p) * t.Tuning.SettlementPoints + CountCities(t, p) * t.Tuning.CityPoints;
            if (t.LongestRoadHolder == p) pts += t.Tuning.LongestRoadPoints;
            return pts;
        }

        /// <summary>
        /// Longest trail of p's roads (each hex edge used once), not passing through a corner held by another people.
        /// Depth-first search from every road end; at most 15 roads, so this is cheap.
        /// </summary>
        public static int LongestRoad(WorldState s, int p)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            var used = new bool[t.RoadOwner.Length];
            int best = 0;
            for (int e = 0; e < t.RoadOwner.Length; e++)
            {
                if (t.RoadOwner[e] != p) continue;
                used[e] = true;
                best = Math.Max(best, 1 + Walk(s, topo, p, topo.HexEdgeCornerA(e), used));
                best = Math.Max(best, 1 + Walk(s, topo, p, topo.HexEdgeCornerB(e), used));
                used[e] = false;
            }
            return best;
        }

        private static int Walk(WorldState s, BoardTopology topo, int p, int corner, bool[] used)
        {
            var t = s.Table;
            if (t.CornerOwner[corner] >= 0 && t.CornerOwner[corner] != p) return 0;
            int best = 0;
            foreach (int e in topo.CornerHexEdges(corner))
            {
                if (used[e] || t.RoadOwner[e] != p) continue;
                used[e] = true;
                best = Math.Max(best, 1 + Walk(s, topo, p, topo.OtherCorner(e, corner), used));
                used[e] = false;
            }
            return best;
        }

        private static int OtherCorner(this BoardTopology topo, int edge, int corner) =>
            topo.HexEdgeCornerA(edge) == corner ? topo.HexEdgeCornerB(edge) : topo.HexEdgeCornerA(edge);

        /// <summary>Summed pips (2/12 = 1 ... 6/8 = 5) of the resource hexes at a corner.</summary>
        public static int CornerPips(Board b, int corner)
        {
            int sum = 0;
            foreach (int h in b.Topology.VertexHexes(corner)) sum += Pips(b.HexNumber[h]);
            return sum;
        }

        public static int Pips(int number) => number < 2 || number > 12 || number == 7 ? 0 : 6 - Math.Abs(7 - number);
    }
}
