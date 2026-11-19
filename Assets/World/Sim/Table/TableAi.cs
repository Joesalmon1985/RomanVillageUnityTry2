using System;
using System.Collections.Generic;

namespace World.Sim.Table
{
    public enum TableTargetKind
    {
        None = 0,
        Road = 1,
        Settlement = 2,
        City = 3,
    }

    /// <summary>
    /// The build an AI seat (or the player's adviser, GB-7) works towards. Spot is what to build next: the corner of a
    /// settlement or city, or the hex edge of the next road. Site is the settlement corner a road heads for (-1 when the
    /// road is for the longest road only). Path lists the hex edges still to build from the people's network to Site,
    /// nearest first. Cost is the cards for the build at Spot.
    /// </summary>
    public sealed class TableTarget
    {
        public TableTargetKind Kind;
        public int Spot = -1;
        public int Site = -1;
        public int[] Path = new int[0];
        public int[] Cost = new int[Wares.RawCount];

        public static readonly TableTarget Nothing = new TableTarget();
    }

    /// <summary>
    /// AI fields of the god board brain (G1-2). Starting values, tuned from the proof (G1-2-proof.md). These change only how
    /// the AI plays, never the table's rules.
    /// </summary>
    public sealed class TableAiTuning
    {
        /// <summary>Site score: weight of summed pips, of different goods, of goods the people does not yet touch, and per road still to build.</summary>
        public int PipWeight = 3, KindWeight = 2, NewGoodWeight = 3, RoadPenalty = 4;
        /// <summary>Site score bonus for a 3:1 port, and for a 2:1 port of a good the people touches.</summary>
        public int GenericPortBonus = 2, GoodPortBonus = 3;
        /// <summary>Farthest site (in roads still to build) the brain plans for.</summary>
        public int MaxRoadsToSite = 3;
        /// <summary>A settlement with at least this many pips is upgraded before a new settlement is planned.</summary>
        public int CityFirstPips = 10;
        /// <summary>Offers per AI turn at most (TR-3).</summary>
        public int MaxOffersPerTurn = 3;
        /// <summary>Hand size above which surplus is shed (TR-5 (3); the discard limit of TU-10).</summary>
        public int ShedAbove = 7;
        /// <summary>Most cards an AI pays for the last card of its target build (TR-6: up to 2-for-1).</summary>
        public int LastCardPrice = 2;
    }

    /// <summary>
    /// The AI peoples' brain for the god board (G1-2, PRD_GODBOARD 4.4-4.6): a target build chosen by scoring corners,
    /// trading by TR-4 to TR-7, discards that keep the target's cards, and setup placement. Pure and deterministic: every
    /// choice reads only the state and breaks ties by the lowest id. Plugged into <see cref="TableSeams"/> by default.
    /// </summary>
    public static class TableAi
    {
        public static readonly TableAiTuning Tuning = new TableAiTuning();

        public const int MaxActionsPerCall = 30;

        // =====================================================================================================
        // Target build
        // =====================================================================================================

        /// <summary>The build people p works towards now (see <see cref="TableTarget"/>). Also the adviser's highlight (GB-7).</summary>
        public static TableTarget TargetFor(WorldState s, int p)
        {
            var t = s.Table;
            if (t == null || p < 0 || p >= t.PeopleCount) return TableTarget.Nothing;
            var tu = Tuning;
            int settlementsLeft = TableRules.SettlementsLeft(t, p);
            int citiesLeft = TableRules.CitiesLeft(t, p);
            int roadsLeft = TableRules.RoadsLeft(t, p);

            int city = BestCity(s, p);
            int cityPips = city >= 0 ? TableRules.CornerPips(s.Board, city) : -1;
            bool canCity = city >= 0 && citiesLeft > 0;

            int site = -1;
            int[] path = null;
            if (settlementsLeft > 0) (site, path) = BestSite(s, p, Math.Min(tu.MaxRoadsToSite, roadsLeft));

            // Cities first on rich settlements, or when no site is left; else the best site, by road if it is not reached yet.
            if (canCity && (site < 0 || cityPips >= tu.CityFirstPips)) return Make(TableTargetKind.City, city, -1, null);
            if (site >= 0)
            {
                if (path.Length == 0) return Make(TableTargetKind.Settlement, site, site, path);
                return Make(TableTargetKind.Road, path[0], site, path);
            }
            if (canCity) return Make(TableTargetKind.City, city, -1, null);
            if (roadsLeft > 0)
            {
                int e = LongestRoadEdge(s, p);
                if (e >= 0) return Make(TableTargetKind.Road, e, -1, null);
            }
            return TableTarget.Nothing;
        }

        private static TableTarget Make(TableTargetKind kind, int spot, int site, int[] path)
        {
            var cost = kind == TableTargetKind.City ? TableRules.CityCost
                : kind == TableTargetKind.Settlement ? TableRules.SettlementCost
                : TableRules.RoadCost;
            return new TableTarget { Kind = kind, Spot = spot, Site = site, Path = path ?? new int[0], Cost = (int[])cost.Clone() };
        }

        /// <summary>p's settlement with the most pips (ties: lowest corner), or -1.</summary>
        public static int BestCity(WorldState s, int p)
        {
            var t = s.Table;
            int best = -1, bestPips = -1;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                if (t.CornerOwner[c] != p || t.CornerLevel[c] != 1) continue;
                int pips = TableRules.CornerPips(s.Board, c);
                if (pips > bestPips) { best = c; bestPips = pips; }
            }
            return best;
        }

        /// <summary>
        /// Score of a settlement site for p, roads not counted: pips, different goods, goods p does not touch yet, ports
        /// (the reuse of BoardGenerator.SiteScore's idea: variety counts more than count).
        /// </summary>
        public static int SiteScore(WorldState s, int p, int corner)
        {
            var tu = Tuning;
            var b = s.Board;
            int touched = TouchedGoods(s, p);
            int mask = GoodsMask(b, corner);
            int score = tu.PipWeight * TableRules.CornerPips(b, corner) + tu.KindWeight * Bits(mask) + tu.NewGoodWeight * Bits(mask & ~touched);
            foreach (var port in s.Table.Ports)
            {
                if (port.CornerA != corner && port.CornerB != corner) continue;
                if (port.Good < 0) score += tu.GenericPortBonus;
                else if (((touched | mask) & (1 << port.Good)) != 0) score += tu.GoodPortBonus;
            }
            return score;
        }

        /// <summary>
        /// The best settlement site within maxRoads new roads of p's network, and the free hex edges to build to reach it
        /// (nearest first). Score = SiteScore - RoadPenalty per road. (-1, null) when none.
        /// </summary>
        public static (int site, int[] path) BestSite(WorldState s, int p, int maxRoads)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            int n = topo.CornerCount;
            var dist = new int[n];
            var via = new int[n]; // hex edge we arrived by, -1 at the network
            for (int c = 0; c < n; c++) { dist[c] = int.MaxValue; via[c] = -1; }

            // 0-1 breadth-first search from p's network: own roads cost 0, free edges 1; other peoples' corners and roads block.
            var deque = new LinkedList<int>();
            for (int c = 0; c < n; c++)
            {
                if (t.CornerOwner[c] == p || (t.CornerOwner[c] < 0 && TouchesOwnRoad(t, topo, p, c)))
                {
                    dist[c] = 0;
                    deque.AddLast(c);
                }
            }
            while (deque.Count > 0)
            {
                int c = deque.First.Value;
                deque.RemoveFirst();
                foreach (int e in topo.CornerHexEdges(c))
                {
                    int owner = t.RoadOwner[e];
                    if (owner >= 0 && owner != p) continue;
                    int w = owner == p ? 0 : 1;
                    int nb = topo.HexEdgeCornerA(e) == c ? topo.HexEdgeCornerB(e) : topo.HexEdgeCornerA(e);
                    if (t.CornerOwner[nb] >= 0 && t.CornerOwner[nb] != p) continue;
                    int d = dist[c] + w;
                    if (d > maxRoads || d >= dist[nb]) continue;
                    dist[nb] = d;
                    via[nb] = e;
                    if (w == 0) deque.AddFirst(nb);
                    else deque.AddLast(nb);
                }
            }

            int best = -1, bestScore = int.MinValue;
            for (int c = 0; c < n; c++)
            {
                if (dist[c] == int.MaxValue) continue;
                if (TableRules.SettlementRefusal(s, p, c, false) != null) continue;
                int score = SiteScore(s, p, c) - Tuning.RoadPenalty * dist[c];
                if (score > bestScore) { best = c; bestScore = score; }
            }
            if (best < 0) return (-1, null);

            var path = new List<int>();
            for (int c = best; via[c] >= 0;)
            {
                int e = via[c];
                if (t.RoadOwner[e] < 0) path.Add(e);
                c = topo.HexEdgeCornerA(e) == c ? topo.HexEdgeCornerB(e) : topo.HexEdgeCornerA(e);
            }
            path.Reverse();
            return (best, path.ToArray());
        }

        private static bool TouchesOwnRoad(TableState t, BoardTopology topo, int p, int c)
        {
            foreach (int e in topo.CornerHexEdges(c)) if (t.RoadOwner[e] == p) return true;
            return false;
        }

        /// <summary>The legal road that makes p's longest road longest (ties: lowest edge), or -1.</summary>
        private static int LongestRoadEdge(WorldState s, int p)
        {
            var t = s.Table;
            int best = -1, bestLen = -1;
            foreach (int e in TableRules.LegalRoadEdges(s, p))
            {
                t.RoadOwner[e] = p;
                int len = TableRules.LongestRoad(s, p);
                t.RoadOwner[e] = -1;
                if (len > bestLen) { best = e; bestLen = len; }
            }
            return best;
        }

        // ---- Goods ----

        private static int GoodsMask(Board b, int corner)
        {
            int mask = 0;
            foreach (int h in b.Topology.VertexHexes(corner))
            {
                var w = TerrainInfo.RawWare(b.HexTerrain[h]);
                if (w.HasValue && TableRules.Pips(b.HexNumber[h]) > 0) mask |= 1 << (int)w.Value;
            }
            return mask;
        }

        /// <summary>Goods (bit per raw good) of the resource hexes beside p's settlements and cities.</summary>
        public static int TouchedGoods(WorldState s, int p)
        {
            var t = s.Table;
            int mask = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == p) mask |= GoodsMask(s.Board, c);
            return mask;
        }

        private static int Bits(int mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }

        /// <summary>Summed pips of p's production (a city counts twice): the TR-4 tie-break.</summary>
        public static int ProductionPips(WorldState s, int p)
        {
            var t = s.Table;
            int sum = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++)
                if (t.CornerOwner[c] == p) sum += t.CornerLevel[c] * TableRules.CornerPips(s.Board, c);
            return sum;
        }

        /// <summary>Cards still missing for a cost.</summary>
        public static int Missing(int[] hand, int[] cost)
        {
            int n = 0;
            for (int g = 0; g < Wares.RawCount; g++) n += Math.Max(0, cost[g] - hand[g]);
            return n;
        }

        // =====================================================================================================
        // Who trades with whom (TR-4)
        // =====================================================================================================

        /// <summary>True when q holds the highest points at the table (ties included).</summary>
        public static bool IsLeading(WorldState s, int q)
        {
            var t = s.Table;
            int mine = TableRules.Points(s, q);
            for (int o = 0; o < t.PeopleCount; o++) if (TableRules.Points(s, o) > mine) return false;
            return true;
        }

        /// <summary>
        /// Why AI seat p will not trade with people q at all, or null (TR-4): leader_refuses when q is leading. A leading AI
        /// still trades with peoples that are not leading.
        /// </summary>
        public static string Refusal(WorldState s, int p, int q) =>
            q != p && IsLeading(s, q) ? TableRefusals.LeaderRefuses : null;

        /// <summary>Peoples p may trade with, preferred first: lowest points, then lowest summed pips, then lowest id (TR-4).</summary>
        public static List<int> Partners(WorldState s, int p)
        {
            var t = s.Table;
            var list = new List<int>();
            for (int q = 0; q < t.PeopleCount; q++) if (q != p && Refusal(s, p, q) == null) list.Add(q);
            list.Sort((a, b) =>
            {
                int pa = TableRules.Points(s, a), pb = TableRules.Points(s, b);
                if (pa != pb) return pa.CompareTo(pb);
                int sa = ProductionPips(s, a), sb = ProductionPips(s, b);
                if (sa != sb) return sa.CompareTo(sb);
                return a.CompareTo(b);
            });
            return list;
        }

        // =====================================================================================================
        // Answering offers (TR-6)
        // =====================================================================================================

        /// <summary>
        /// Whether AI seat p accepts offer o (it gives o.Get and receives o.Give). TR-4 first; then it must be able to pay;
        /// then the trade must not lower progress to its target build, and give at least as many cards as it takes, except
        /// that it pays up to LastCardPrice for 1 when that card completes its target build.
        /// </summary>
        public static bool Accepts(WorldState s, int p, TradeOffer o)
        {
            if (Refusal(s, p, o.From) != null) return false;
            var hand = s.Table.Peoples[p].Hand;
            if (!TableRules.CanPay(hand, o.Get)) return false;
            return Fair(s, p, o.Get, o.Give);
        }

        /// <summary>TR-6 for p giving 'give' and receiving 'get'.</summary>
        public static bool Fair(WorldState s, int p, int[] give, int[] get)
        {
            var hand = s.Table.Peoples[p].Hand;
            var cost = TargetFor(s, p).Cost;
            var after = new int[Wares.RawCount];
            for (int g = 0; g < Wares.RawCount; g++) after[g] = hand[g] - give[g] + get[g];
            int before = Missing(hand, cost), now = Missing(after, cost);
            if (now > before) return false;
            int gives = TableRules.HandSize(give), gets = TableRules.HandSize(get);
            if (gets >= gives) return true;
            return before == 1 && now == 0 && gets == 1 && gives <= Tuning.LastCardPrice;
        }

        // =====================================================================================================
        // The turn
        // =====================================================================================================

        /// <summary>
        /// An AI seat's turn (TableSeams.AiTurn): build the target when affordable (and a city whenever affordable), else
        /// offer a trade to the best partner and wait for the answer (the engine asks again), else trade with the bank or a
        /// port. Called again after each of its offers is answered.
        /// </summary>
        public static void PlayTurn(WorldState s, int p, TableActor act)
        {
            for (int i = 0; i < MaxActionsPerCall; i++)
            {
                if (s.Table.Phase != TablePhase.Main || s.Table.Current != p) return;
                var target = TargetFor(s, p);
                if (TryBuild(s, p, target, act)) continue;
                if (TryCity(s, p, act)) continue;
                if (TryOffer(s, p, target, act)) return; // wait for the answer
                if (TryBank(s, p, target, act)) continue;
                return;
            }
        }

        private static bool TryBuild(WorldState s, int p, TableTarget target, TableActor act)
        {
            if (target.Kind == TableTargetKind.None || !TableRules.CanPay(s.Table.Peoples[p].Hand, target.Cost)) return false;
            switch (target.Kind)
            {
                case TableTargetKind.City: return act.UpgradeCity(target.Spot).Ok;
                case TableTargetKind.Settlement: return act.PlaceSettlement(target.Spot).Ok;
                default: return act.PlaceRoad(target.Spot).Ok;
            }
        }

        /// <summary>A city is always worth its cards: upgrade the best settlement when affordable.</summary>
        private static bool TryCity(WorldState s, int p, TableActor act)
        {
            var t = s.Table;
            if (!TableRules.CanPay(t.Peoples[p].Hand, TableRules.CityCost) || TableRules.CitiesLeft(t, p) <= 0) return false;
            int c = BestCity(s, p);
            return c >= 0 && act.UpgradeCity(c).Ok;
        }

        /// <summary>
        /// The goods p wants, in TR-5 order: (1) the cards missing for its target build, (2) goods it has no settlement next
        /// to. Each good once.
        /// </summary>
        public static List<int> Wants(WorldState s, int p, TableTarget target)
        {
            var hand = s.Table.Peoples[p].Hand;
            var list = new List<int>();
            for (int g = 0; g < Wares.RawCount; g++) if (hand[g] < target.Cost[g]) list.Add(g);
            int touched = TouchedGoods(s, p);
            for (int g = 0; g < Wares.RawCount; g++) if ((touched & (1 << g)) == 0 && !list.Contains(g) && hand[g] == 0) list.Add(g);
            return list;
        }

        /// <summary>Cards p can spare per good: beyond the target build's needs (TR-5 (3)).</summary>
        public static int[] Spare(WorldState s, int p, TableTarget target)
        {
            var hand = s.Table.Peoples[p].Hand;
            var spare = new int[Wares.RawCount];
            for (int g = 0; g < Wares.RawCount; g++) spare[g] = Math.Max(0, hand[g] - target.Cost[g]);
            return spare;
        }

        private static int Largest(int[] counts, int except)
        {
            int best = -1;
            for (int g = 0; g < Wares.RawCount; g++)
                if (g != except && counts[g] > 0 && (best < 0 || counts[g] > counts[best])) best = g;
            return best;
        }

        /// <summary>
        /// One offer to the most preferred partner not yet asked this turn: a spare card for a wanted card (1-for-1; up to
        /// LastCardPrice-for-1 for the last card of the target). AI partners are only asked when they would accept; the
        /// player is asked once a turn. Wants (2) are only traded for when the hand is above ShedAbove.
        /// </summary>
        private static bool TryOffer(WorldState s, int p, TableTarget target, TableActor act)
        {
            var t = s.Table;
            var tu = Tuning;
            if (target.Kind == TableTargetKind.None || t.AiAsked.Count >= tu.MaxOffersPerTurn) return false;
            var hand = t.Peoples[p].Hand;
            int missing = Missing(hand, target.Cost);
            var spare = Spare(s, p, target);
            int spareTotal = TableRules.HandSize(spare);
            if (spareTotal == 0) return false;
            bool shed = TableRules.HandSize(hand) > tu.ShedAbove;

            var wants = Wants(s, p, target);
            var partners = Partners(s, p);
            foreach (int want in wants)
            {
                bool forTarget = hand[want] < target.Cost[want];
                if (!forTarget && !shed) continue;
                int give = Largest(spare, want);
                if (give < 0) continue;
                foreach (int q in partners)
                {
                    if (t.AiAsked.Contains(q)) continue;
                    var offers = new List<int[]> { One(give) };
                    if (forTarget && missing == 1 && tu.LastCardPrice >= 2) offers.Add(TwoFrom(spare, give, want));
                    foreach (var g in offers)
                    {
                        if (g == null) continue;
                        var get = One(want);
                        if (q != t.HumanSeat && !WouldAccept(s, q, p, g, get)) continue;
                        if (!act.OfferTrade(q, g, get).Ok) continue;
                        t.AiAsked.Add(q);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool WouldAccept(WorldState s, int q, int from, int[] give, int[] get)
        {
            // q receives 'give' and pays 'get'.
            if (Refusal(s, q, from) != null) return false;
            if (!TableRules.CanPay(s.Table.Peoples[q].Hand, get)) return false;
            return Fair(s, q, get, give);
        }

        private static int[] One(int g)
        {
            var a = new int[Wares.RawCount];
            a[g] = 1;
            return a;
        }

        /// <summary>Two spare cards (the largest pile first, then the next) never of the wanted good, or null.</summary>
        private static int[] TwoFrom(int[] spare, int first, int want)
        {
            var left = (int[])spare.Clone();
            var a = new int[Wares.RawCount];
            for (int k = 0; k < 2; k++)
            {
                int g = Largest(left, want);
                if (g < 0) return null;
                left[g]--;
                a[g]++;
            }
            return a;
        }

        /// <summary>
        /// Bank or port (TR-1) when no partner helps: the spare good with the most cards beyond its rate, for the first
        /// good the target lacks. Above ShedAbove cards it also sheds surplus for a good it has no settlement next to.
        /// </summary>
        private static bool TryBank(WorldState s, int p, TableTarget target, TableActor act)
        {
            if (target.Kind == TableTargetKind.None) return false;
            var hand = s.Table.Peoples[p].Hand;
            var spare = Spare(s, p, target);
            bool shed = TableRules.HandSize(hand) > Tuning.ShedAbove;
            foreach (int want in Wants(s, p, target))
            {
                if (hand[want] >= target.Cost[want] && !shed) continue;
                int give = -1, extra = -1;
                for (int g = 0; g < Wares.RawCount; g++)
                {
                    if (g == want) continue;
                    int rate = TableRules.TradeRate(s, p, (Ware)g);
                    if (spare[g] < rate) continue;
                    if (spare[g] - rate > extra) { give = g; extra = spare[g] - rate; }
                }
                if (give >= 0) return act.BankTrade((Ware)give, (Ware)want).Ok;
            }
            return false;
        }

        // =====================================================================================================
        // Discards (TU-10) and setup (GB-5)
        // =====================================================================================================

        /// <summary>Discards one card at a time from the good with the most cards beyond the target's needs; only then from the target's cards.</summary>
        public static int[] ChooseDiscard(WorldState s, int p, int count)
        {
            var hand = (int[])s.Table.Peoples[p].Hand.Clone();
            var keep = TargetFor(s, p).Cost;
            var cards = new int[Wares.RawCount];
            for (int i = 0; i < count; i++)
            {
                int best = -1, bestExtra = int.MinValue;
                for (int g = 0; g < Wares.RawCount; g++)
                {
                    if (hand[g] <= 0) continue;
                    int extra = hand[g] - keep[g];
                    if (extra > bestExtra) { best = g; bestExtra = extra; }
                }
                if (best < 0) break;
                hand[best]--;
                cards[best]++;
            }
            return cards;
        }

        /// <summary>Setup: the legal corner with the best SiteScore (ties: lowest id), and the road towards the best site beyond it.</summary>
        public static (int corner, int edge) SetupPlacement(WorldState s, int p)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            int best = -1, bestScore = int.MinValue;
            foreach (int c in TableRules.LegalSettlementCorners(s, p, false))
            {
                int score = SiteScore(s, p, c);
                if (score > bestScore) { best = c; bestScore = score; }
            }
            if (best < 0) return TableDefaults.SetupPlacement(s, p);

            // The road: the free edge whose far corner leads to the best next site (pretend the settlement stands).
            int edge = -1, edgeScore = int.MinValue;
            t.CornerOwner[best] = p;
            t.CornerLevel[best] = 1;
            try
            {
                foreach (int e in topo.CornerHexEdges(best))
                {
                    if (t.RoadOwner[e] >= 0) continue;
                    int far = topo.HexEdgeCornerA(e) == best ? topo.HexEdgeCornerB(e) : topo.HexEdgeCornerA(e);
                    int score = int.MinValue;
                    foreach (int e2 in topo.CornerHexEdges(far))
                    {
                        if (e2 == e) continue;
                        int c2 = topo.HexEdgeCornerA(e2) == far ? topo.HexEdgeCornerB(e2) : topo.HexEdgeCornerA(e2);
                        if (TableRules.SettlementRefusal(s, p, c2, false) == null) score = Math.Max(score, SiteScore(s, p, c2));
                    }
                    if (score > edgeScore) { edge = e; edgeScore = score; }
                }
            }
            finally
            {
                t.CornerOwner[best] = -1;
                t.CornerLevel[best] = 0;
            }
            return (best, edge);
        }
    }
}
