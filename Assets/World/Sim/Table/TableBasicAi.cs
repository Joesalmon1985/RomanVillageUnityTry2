using System.Collections.Generic;

namespace World.Sim.Table
{
    /// <summary>
    /// A stand-in AI seat (G1-1): build whenever affordable (city, then settlement, then a road when no settlement spot is
    /// reachable), and trade surplus with the bank for the next build. No offers to other peoples. The real build and
    /// trade brain is G1-2. Deterministic: every choice breaks ties by the lowest id.
    /// </summary>
    public static class TableBasicAi
    {
        public const int MaxActionsPerTurn = 30;

        public static void PlayTurn(WorldState s, int p, TableActor act)
        {
            for (int i = 0; i < MaxActionsPerTurn; i++)
            {
                if (s.Table.Phase != TablePhase.Main) return;
                if (TryCity(s, p, act) || TrySettlement(s, p, act) || TryRoad(s, p, act) || TryBankTrade(s, p, act)) continue;
                return;
            }
        }

        private static int[] Hand(WorldState s, int p) => s.Table.Peoples[p].Hand;

        private static bool TryCity(WorldState s, int p, TableActor act)
        {
            var t = s.Table;
            if (!TableRules.CanPay(Hand(s, p), TableRules.CityCost) || TableRules.CitiesLeft(t, p) <= 0) return false;
            int best = -1, bestPips = -1;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                if (t.CornerOwner[c] != p || t.CornerLevel[c] != 1) continue;
                int pips = TableRules.CornerPips(s.Board, c);
                if (pips > bestPips) { best = c; bestPips = pips; }
            }
            return best >= 0 && act.UpgradeCity(best).Ok;
        }

        private static bool TrySettlement(WorldState s, int p, TableActor act)
        {
            if (!TableRules.CanPay(Hand(s, p), TableRules.SettlementCost) || TableRules.SettlementsLeft(s.Table, p) <= 0) return false;
            int best = BestCorner(s, TableRules.LegalSettlementCorners(s, p, true));
            return best >= 0 && act.PlaceSettlement(best).Ok;
        }

        private static int BestCorner(WorldState s, List<int> corners)
        {
            int best = -1, bestPips = -1;
            foreach (int c in corners)
            {
                int pips = TableRules.CornerPips(s.Board, c);
                if (pips > bestPips) { best = c; bestPips = pips; }
            }
            return best;
        }

        /// <summary>A road when it is the build we are working towards (see <see cref="Target"/>).</summary>
        private static bool TryRoad(WorldState s, int p, TableActor act)
        {
            if (Target(s, p) != TableRules.RoadCost) return false;
            if (!TableRules.CanPay(Hand(s, p), TableRules.RoadCost) || TableRules.RoadsLeft(s.Table, p) <= 0) return false;
            int edge = BestRoad(s, p);
            return edge >= 0 && act.PlaceRoad(edge).Ok;
        }

        /// <summary>The legal road whose far corner is the best free settlement spot (by pips), else the lowest legal road.</summary>
        private static int BestRoad(WorldState s, int p)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            int best = -1, bestScore = -1;
            foreach (int e in TableRules.LegalRoadEdges(s, p))
            {
                int score = 0;
                foreach (int c in new[] { topo.HexEdgeCornerA(e), topo.HexEdgeCornerB(e) })
                {
                    if (t.CornerOwner[c] >= 0) continue;
                    int pips = TableRules.CornerPips(s.Board, c);
                    if (TableRules.SettlementRefusal(s, p, c, false) == null) pips += 20; // a spot we could settle
                    if (pips > score) score = pips;
                }
                if (score > bestScore) { best = e; bestScore = score; }
            }
            return best;
        }

        /// <summary>
        /// The build we save for: a city while we have a settlement and city pieces; else a settlement when a spot is
        /// reachable; else a road (towards a spot, or for the longest road); null when nothing is left to build.
        /// </summary>
        private static int[] Target(WorldState s, int p)
        {
            var t = s.Table;
            bool canSettle = TableRules.SettlementsLeft(t, p) > 0;
            bool canCity = TableRules.CountSettlements(t, p) > 0 && TableRules.CitiesLeft(t, p) > 0;
            if (canSettle && TableRules.LegalSettlementCorners(s, p, true).Count > 0) return TableRules.SettlementCost;
            if (canSettle && TableRules.RoadsLeft(t, p) > 0 && TableRules.LegalRoadEdges(s, p).Count > 0) return TableRules.RoadCost;
            if (canCity) return TableRules.CityCost;
            if (TableRules.RoadsLeft(t, p) > 0 && TableRules.LegalRoadEdges(s, p).Count > 0) return TableRules.RoadCost;
            return null;
        }

        /// <summary>Trades the good we hold most of beyond the target's needs for the first good the target still lacks.</summary>
        private static bool TryBankTrade(WorldState s, int p, TableActor act)
        {
            var target = Target(s, p);
            if (target == null) return false;
            var hand = Hand(s, p);
            int want = -1;
            for (int g = 0; g < Wares.RawCount; g++) if (hand[g] < target[g]) { want = g; break; }
            if (want < 0) return false;
            int give = -1, spare = 0;
            for (int g = 0; g < Wares.RawCount; g++)
            {
                int extra = hand[g] - target[g] - TableRules.TradeRate(s, p, (Ware)g);
                if (extra >= 0 && hand[g] - target[g] > spare) { give = g; spare = hand[g] - target[g]; }
            }
            return give >= 0 && act.BankTrade((Ware)give, (Ware)want).Ok;
        }
    }
}
