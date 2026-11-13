using System;
using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>
    /// Hub-and-spoke logistics (REFACTOR_SPEC 2.B.1). Every settlement's storehouse is the hub of its local paths:
    /// building sites and workshop inputs are carried from it, collector and workshop outputs are carried to it,
    /// one unit per local trip. Road and settlement sites are carried from their source settlement along the main
    /// roads, and imports travel between storehouses as shipments of up to ShipmentSize units of one ware. A carrier
    /// always returns to its own storehouse after a delivery (unless it can take an output back from the building it
    /// just served). All choices are deterministic: ties by ascending id.
    /// </summary>
    internal static class Logistics
    {
        public const int StepsPerHexEdge = 12;
        public const int StepsToRoadMidpoint = 6;

        // ---- routing ----

        private static bool EdgeBlocked(WorldState s, EconomyHooks h, int e) => h != null ? h.EdgeBlocked(s, e) : EconomyLogic.EdgeDemons(s, e) > 0;
        private static bool CornerBlocked(WorldState s, EconomyHooks h, int c) => h != null ? h.CornerBlocked(s, c) : EconomyLogic.CornerDemons(s, c) > 0;
        public static bool Halted(WorldState s, EconomyHooks h, int settlementId) =>
            h != null ? h.SettlementHalted(s, settlementId) : EconomyLogic.SettlementHalted(s, settlementId);

        /// <summary>
        /// Shortest corner path (by road edges, ties by lower corner ids) over the faction's built roads. With
        /// avoidDemons, demon-held edges are not used and the path never passes through a demon-held corner. It never
        /// passes through another people's settlement. Null when there is none.
        /// </summary>
        public static int[] Route(WorldState s, EconomyHooks h, int faction, int from, int to, bool avoidDemons = true)
        {
            var t = s.Board.Topology;
            if (!t.IsCorner(from) || !t.IsCorner(to)) return null;
            if (from == to) return new[] { from };
            var parent = new int[t.CornerCount];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            parent[from] = -1;
            var frontier = new List<int> { from };
            var nbrs = new List<int>(3);
            while (frontier.Count > 0)
            {
                var next = new List<int>();
                foreach (int c in frontier)
                {
                    nbrs.Clear();
                    foreach (int e in t.CornerHexEdges(c))
                    {
                        var r = EconomyLogic.RoadOnEdge(s, e);
                        if (r == null || r.Faction != faction || r.State != RoadState.Built) continue;
                        if (avoidDemons && EdgeBlocked(s, h, e)) continue;
                        nbrs.Add(t.HexEdgeCornerA(e) == c ? t.HexEdgeCornerB(e) : t.HexEdgeCornerA(e));
                    }
                    nbrs.Sort();
                    foreach (int o in nbrs)
                    {
                        if (parent[o] != -2) continue;
                        if (o != to)
                        {
                            var st = EconomyLogic.SettlementAt(s, o);
                            if (st != null && st.Faction != faction) continue;
                            if (avoidDemons && CornerBlocked(s, h, o)) continue;
                        }
                        parent[o] = c;
                        if (o == to) return Path(parent, to);
                        next.Add(o);
                    }
                }
                next.Sort();
                frontier = next;
            }
            return null;
        }

        private static int[] Path(int[] parent, int to)
        {
            var list = new List<int>();
            for (int c = to; c != -1; c = parent[c]) list.Add(c);
            list.Reverse();
            return list.ToArray();
        }

        /// <summary>Route from a settlement's corner to a road site (to its nearer end corner; ties lower corner). Null when none.</summary>
        public static int[] RouteToRoadSite(WorldState s, EconomyHooks h, int faction, int fromCorner, Road r, bool avoidDemons = true)
        {
            var t = s.Board.Topology;
            int a = t.HexEdgeCornerA(r.HexEdge), b = t.HexEdgeCornerB(r.HexEdge);
            if (a > b) { int x = a; a = b; b = x; }
            var pa = Route(s, h, faction, fromCorner, a, avoidDemons);
            var pb = Route(s, h, faction, fromCorner, b, avoidDemons);
            if (pa == null) return pb;
            if (pb == null) return pa;
            return pb.Length < pa.Length ? pb : pa;
        }

        // ---- places ----

        public static int[] StockArray(WorldState s, PlaceKind kind, int id, out int[] reserved)
        {
            reserved = null;
            switch (kind)
            {
                case PlaceKind.Storehouse: { var st = EconomyLogic.GetSettlement(s, id); reserved = st?.Reserved; return st?.Stock; }
                case PlaceKind.Building: { var b = EconomyLogic.GetBuilding(s, id); reserved = b?.Reserved; return b?.Stock; }
            }
            return null;
        }

        public static int[] IncomingArray(WorldState s, PlaceKind kind, int id)
        {
            switch (kind)
            {
                case PlaceKind.Storehouse:
                case PlaceKind.SettlementSite: return EconomyLogic.GetSettlement(s, id)?.Incoming;
                case PlaceKind.Building: return EconomyLogic.GetBuilding(s, id)?.Incoming;
                case PlaceKind.RoadSite: return EconomyLogic.GetRoad(s, id)?.Incoming;
            }
            return null;
        }

        private static int[] DeliveredArray(WorldState s, PlaceKind kind, int id)
        {
            switch (kind)
            {
                case PlaceKind.Storehouse: return EconomyLogic.GetSettlement(s, id)?.Stock;
                case PlaceKind.SettlementSite: return EconomyLogic.GetSettlement(s, id)?.Delivered;
                case PlaceKind.Building: return EconomyLogic.GetBuilding(s, id)?.Delivered;
                case PlaceKind.RoadSite: return EconomyLogic.GetRoad(s, id)?.Delivered;
            }
            return null;
        }

        // ---- jobs ----

        public static int Avail(Settlement st, int w) => st.Stock[w] - st.Reserved[w];

        public static Job CreateJob(WorldState s, EventBuffer ev, int faction, int settlement, Ware ware, int count,
            PlaceKind fromKind, int fromId, PlaceKind toKind, int toId, bool shipment)
        {
            var e = s.Economy;
            var j = new Job
            {
                Id = e.NextJobId++, Faction = faction, Settlement = settlement, Ware = ware, Count = count,
                FromKind = fromKind, FromId = fromId, ToKind = toKind, ToId = toId, IsShipment = shipment,
                State = JobState.Waiting, CreatedTick = s.Tick,
            };
            StockArray(s, fromKind, fromId, out var res);
            res[(int)ware] += count;
            IncomingArray(s, toKind, toId)[(int)ware] += count;
            e.Jobs.Add(j);
            ev?.Add(new JobCreated
            {
                JobId = j.Id, Ware = ware, Count = count, FromKind = fromKind, FromId = fromId, ToKind = toKind, ToId = toId, IsShipment = shipment,
            });
            return j;
        }

        /// <summary>Drops a job that has not been picked up: its source reservation and destination incoming are released.</summary>
        public static void Release(WorldState s, Job j)
        {
            if (j.State == JobState.Waiting || j.State == JobState.ToPickup)
            {
                StockArray(s, j.FromKind, j.FromId, out var res);
                if (res != null) res[(int)j.Ware] -= j.Count;
            }
            var inc = IncomingArray(s, j.ToKind, j.ToId);
            if (inc != null) inc[(int)j.Ware] -= j.Count;
            s.Economy.Jobs.Remove(j);
        }

        public static Job FindJob(WorldState s, int id)
        {
            foreach (var j in s.Economy.Jobs) if (j.Id == id) return j;
            return null;
        }

        /// <summary>Serving order (2.B.1): construction sites 0, workshop inputs 1, outputs to the storehouse 2, shipments 3.</summary>
        public static int Priority(WorldState s, Job j)
        {
            if (j.IsShipment) return 3;
            if (j.FromKind == PlaceKind.Building) return 2;
            if (j.ToKind == PlaceKind.RoadSite || j.ToKind == PlaceKind.SettlementSite) return 0;
            if (j.ToKind == PlaceKind.Building)
            {
                var b = EconomyLogic.GetBuilding(s, j.ToId);
                return b != null && (b.State == BuildingState.Site || b.State == BuildingState.Constructing) ? 0 : 1;
            }
            return 2;
        }

        // ---- demand ----

        /// <summary>Goods still needed by everything this settlement's storehouse serves, not yet on a job (sites, inputs, sourced sites, city).</summary>
        public static int Unjobbed(WorldState s, Settlement st, int w)
        {
            var e = s.Economy;
            int n = 0;
            foreach (var b in e.Buildings)
            {
                if (b.Settlement != st.Id) continue;
                var def = BuildingDefs.Get(b.Type);
                if (b.State == BuildingState.Site) n += Math.Max(0, def.Cost[w] - b.Delivered[w] - b.Incoming[w]);
                else if (b.State == BuildingState.Active && def.Inputs[w] > 0 && InputsWanted(s, b, def))
                    n += Math.Max(0, e.Tuning.InputBuffer * def.Inputs[w] - b.Delivered[w] - b.Incoming[w]);
            }
            foreach (var r in e.Roads)
                if (r.State == RoadState.Site && r.SourceSettlement == st.Id)
                    n += Math.Max(0, e.Tuning.RoadCost[w] - r.Delivered[w] - r.Incoming[w]);
            foreach (var o in e.Settlements)
                if (o.State == SettlementState.Site && o.SourceSettlement == st.Id)
                    n += Math.Max(0, e.Tuning.SettlementCost[w] - o.Delivered[w] - o.Incoming[w]);
            if (st.Upgrade == UpgradeState.Site) n += Math.Max(0, e.Tuning.CityCost[w] - st.UpgradeDelivered[w]);
            return n;
        }

        private static bool InputsWanted(WorldState s, Building b, BuildingDef def) =>
            def.Class != BuildingClass.Lodge || ProductionSystem.WarriorsAt(s, b.Id) < s.Economy.Tuning.WarriorsPerLodge;

        public static void CreateDemand(WorldState s, EventBuffer ev, EconomyHooks h)
        {
            var e = s.Economy;
            var tu = e.Tuning;
            foreach (var st in e.Settlements)
            {
                if (st.State != SettlementState.Active) continue;

                // a. construction: building sites (local), road and settlement sites sourced here (main roads).
                foreach (var b in e.Buildings)
                {
                    if (b.Settlement != st.Id || b.State != BuildingState.Site) continue;
                    var def = BuildingDefs.Get(b.Type);
                    int missing = -1;
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        int need = def.Cost[w] - b.Delivered[w] - b.Incoming[w];
                        while (need > 0 && Avail(st, w) > 0)
                        {
                            CreateJob(s, ev, st.Faction, st.Id, (Ware)w, 1, PlaceKind.Storehouse, st.Id, PlaceKind.Building, b.Id, false);
                            need--;
                        }
                        if (need > 0 && missing < 0) missing = w;
                    }
                    b.Reason = missing >= 0 ? Reasons.WaitingWare : null;
                    b.ReasonWare = missing;
                }
                bool halted = Halted(s, h, st.Id);
                foreach (var r in e.Roads)
                {
                    if (r.State != RoadState.Site || r.SourceSettlement != st.Id) continue;
                    if (!ConstructionSystem.Covered(Sum(r.Delivered, r.Incoming), tu.RoadCost))
                    {
                        if (halted) { r.Reason = Reasons.DemonOnRoute; continue; }
                        if (RouteToRoadSite(s, h, st.Faction, st.Corner, r) == null)
                        {
                            r.Reason = RouteToRoadSite(s, h, st.Faction, st.Corner, r, false) != null ? Reasons.DemonOnRoute : Reasons.NoRoad;
                            continue;
                        }
                    }
                    r.ReasonWare = SiteJobs(s, ev, st, tu.RoadCost, r.Delivered, r.Incoming, PlaceKind.RoadSite, r.Id);
                    r.Reason = r.ReasonWare >= 0 ? Reasons.WaitingWare : null;
                }
                foreach (var o in e.Settlements)
                {
                    if (o.State != SettlementState.Site || o.SourceSettlement != st.Id) continue;
                    if (!ConstructionSystem.Covered(Sum(o.Delivered, o.Incoming), tu.SettlementCost))
                    {
                        if (halted) { o.Reason = Reasons.DemonOnRoute; continue; }
                        if (Route(s, h, st.Faction, st.Corner, o.Corner) == null)
                        {
                            o.Reason = Route(s, h, st.Faction, st.Corner, o.Corner, false) != null ? Reasons.DemonOnRoute : Reasons.NoRoad;
                            continue;
                        }
                    }
                    o.ReasonWare = SiteJobs(s, ev, st, tu.SettlementCost, o.Delivered, o.Incoming, PlaceKind.SettlementSite, o.Id);
                    o.Reason = o.ReasonWare >= 0 ? Reasons.WaitingWare : null;
                }

                // b. workshop and lodge inputs (InputBuffer cycles on hand or on the way).
                foreach (var b in e.Buildings)
                {
                    if (b.Settlement != st.Id || b.State != BuildingState.Active) continue;
                    var def = BuildingDefs.Get(b.Type);
                    if ((def.Class != BuildingClass.Workshop && def.Class != BuildingClass.Lodge) || !InputsWanted(s, b, def)) continue;
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        if (def.Inputs[w] <= 0) continue;
                        int need = tu.InputBuffer * def.Inputs[w] - b.Delivered[w] - b.Incoming[w];
                        while (need > 0 && Avail(st, w) > 0)
                        {
                            CreateJob(s, ev, st.Faction, st.Id, (Ware)w, 1, PlaceKind.Storehouse, st.Id, PlaceKind.Building, b.Id, false);
                            need--;
                        }
                    }
                }

                // c. outputs to the storehouse.
                foreach (var b in e.Buildings)
                {
                    if (b.Settlement != st.Id || EconomyLogic.IsGone(b)) continue;
                    for (int w = 0; w < Wares.Count; w++)
                        while (b.Stock[w] - b.Reserved[w] > 0)
                            CreateJob(s, ev, st.Faction, st.Id, (Ware)w, 1, PlaceKind.Building, b.Id, PlaceKind.Storehouse, st.Id, false);
                }
            }

            // d. imports: shipments between storehouses of one people.
            foreach (var dst in e.Settlements)
            {
                if (dst.State != SettlementState.Active || Halted(s, h, dst.Id)) continue;
                for (int w = 0; w < Wares.Count; w++)
                {
                    int need = Unjobbed(s, dst, w) - Avail(dst, w);
                    if (need <= 0 || ShipmentHeading(s, dst.Id, w)) continue;
                    Settlement best = null;
                    int bestLen = int.MaxValue, bestSpare = 0;
                    foreach (var src in e.Settlements)
                    {
                        if (src == dst || src.Faction != dst.Faction || src.State != SettlementState.Active || Halted(s, h, src.Id)) continue;
                        int spare = Avail(src, w) - Unjobbed(s, src, w);
                        if (spare <= 0) continue;
                        var route = Route(s, h, src.Faction, src.Corner, dst.Corner);
                        if (route == null) continue;
                        if (route.Length < bestLen || (route.Length == bestLen && src.Corner < best.Corner))
                        {
                            best = src;
                            bestLen = route.Length;
                            bestSpare = spare;
                        }
                    }
                    if (best == null) continue;
                    int n = Math.Min(tu.ShipmentSize, Math.Min(need, bestSpare));
                    CreateJob(s, ev, best.Faction, best.Id, (Ware)w, n, PlaceKind.Storehouse, best.Id, PlaceKind.Storehouse, dst.Id, true);
                }
            }
        }

        private static int[] Sum(int[] a, int[] b)
        {
            var r = new int[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = a[i] + b[i];
            return r;
        }

        /// <summary>Jobs (up to ShipmentSize units of one ware each) from a source storehouse to a road or settlement site. Returns the first ware still short, or -1.</summary>
        private static int SiteJobs(WorldState s, EventBuffer ev, Settlement src, int[] cost, int[] delivered, int[] incoming, PlaceKind kind, int id)
        {
            int missing = -1;
            for (int w = 0; w < Wares.Count; w++)
            {
                int need = cost[w] - delivered[w] - incoming[w];
                while (need > 0 && Avail(src, w) > 0)
                {
                    int n = Math.Min(need, Math.Min(Avail(src, w), s.Economy.Tuning.ShipmentSize));
                    CreateJob(s, ev, src.Faction, src.Id, (Ware)w, n, PlaceKind.Storehouse, src.Id, kind, id, false);
                    need -= n;
                }
                if (need > 0 && missing < 0) missing = w;
            }
            return missing;
        }

        public static bool ShipmentHeading(WorldState s, int settlementId, int w)
        {
            foreach (var j in s.Economy.Jobs)
                if (j.IsShipment && j.ToKind == PlaceKind.Storehouse && j.ToId == settlementId && (int)j.Ware == w) return true;
            return false;
        }

        // ---- carriers ----

        public static void Assign(WorldState s, EventBuffer ev, EconomyHooks h)
        {
            var e = s.Economy;
            if (e.Jobs.Count == 0) return;
            var waiting = new List<Job>();
            foreach (var j in e.Jobs) if (j.State == JobState.Waiting) waiting.Add(j);
            if (waiting.Count == 0) return;
            var prio = new Dictionary<int, int>();
            int age = e.Tuning.JobAgeTicks;
            foreach (var j in waiting) prio[j.Id] = age > 0 && s.Tick - j.CreatedTick >= age ? -1 : Priority(s, j); // no job starves
            waiting.Sort((a, b) => prio[a.Id] != prio[b.Id] ? prio[a.Id].CompareTo(prio[b.Id]) : a.Id.CompareTo(b.Id));

            var idle = new Dictionary<int, Queue<Carrier>>();
            foreach (var c in e.Carriers)
            {
                if (c.State != CarrierState.Idle) continue;
                if (!idle.TryGetValue(c.Home, out var q)) idle[c.Home] = q = new Queue<Carrier>();
                q.Enqueue(c);
            }
            foreach (var j in waiting)
            {
                bool main = j.IsShipment || j.ToKind == PlaceKind.RoadSite || j.ToKind == PlaceKind.SettlementSite;
                int[] route = null;
                if (main)
                {
                    var home = e.Settlements[j.Settlement];
                    route = MainRoute(s, h, j, home.Corner, true);
                    if (route == null || Halted(s, h, j.Settlement) || (j.IsShipment && Halted(s, h, j.ToId)))
                    {
                        string why = route == null && MainRoute(s, h, j, home.Corner, false) == null ? Reasons.NoRoad : Reasons.DemonOnRoute;
                        ev?.Add(new JobStalled { JobId = j.Id, Reason = why });
                        Release(s, j); // demand re-creates it when a route exists (or another source)
                        continue;
                    }
                }
                if (!idle.TryGetValue(j.Settlement, out var q) || q.Count == 0)
                {
                    Stall(j, Reasons.NoCarrier, ev);
                    continue;
                }
                var carrier = q.Dequeue();
                Start(s, ev, carrier, j, route);
            }
        }

        private static int[] MainRoute(WorldState s, EconomyHooks h, Job j, int fromCorner, bool avoid)
        {
            switch (j.ToKind)
            {
                case PlaceKind.RoadSite: return RouteToRoadSite(s, h, j.Faction, fromCorner, s.Economy.Roads[j.ToId], avoid);
                case PlaceKind.SettlementSite: return Route(s, h, j.Faction, fromCorner, s.Economy.Settlements[j.ToId].Corner, avoid);
                default: return Route(s, h, j.Faction, fromCorner, s.Economy.Settlements[j.ToId].Corner, avoid);
            }
        }

        private static void Stall(Job j, string reason, EventBuffer ev)
        {
            if (j.Reason == reason) return;
            j.Reason = reason;
            ev?.Add(new JobStalled { JobId = j.Id, Reason = reason });
        }

        private static void Start(WorldState s, EventBuffer ev, Carrier c, Job j, int[] route)
        {
            j.CarrierId = c.Id;
            j.Reason = null;
            c.JobId = j.Id;
            if (j.FromKind == PlaceKind.Storehouse)
            {
                Pickup(s, ev, c, j);
                if (route != null) BeginMain(s, ev, c, PlaceKind.Storehouse, j.FromId, j.ToKind, j.ToId, route, CarrierState.Carrying);
                else BeginLocal(s, ev, c, PlaceKind.Storehouse, c.Home, j.ToKind, j.ToId, CarrierState.Carrying);
            }
            else
            {
                j.State = JobState.ToPickup;
                BeginLocal(s, ev, c, PlaceKind.Storehouse, c.Home, j.FromKind, j.FromId, CarrierState.ToPickup);
            }
        }

        private static void Pickup(WorldState s, EventBuffer ev, Carrier c, Job j)
        {
            var stock = StockArray(s, j.FromKind, j.FromId, out var res);
            stock[(int)j.Ware] -= j.Count;
            res[(int)j.Ware] -= j.Count;
            c.HasLoad = true;
            c.Load = j.Ware;
            c.LoadCount = j.Count;
            j.State = JobState.Carrying;
            j.CarrierId = c.Id;
            c.JobId = j.Id;
            ev?.Add(new WarePickedUp { JobId = j.Id, CarrierId = c.Id, Ware = j.Ware, Count = j.Count });
        }

        private static void BeginLocal(WorldState s, EventBuffer ev, Carrier c, PlaceKind fk, int fid, PlaceKind tk, int tid, CarrierState state)
        {
            int buildingId = fk == PlaceKind.Building ? fid : tid;
            var b = EconomyLogic.GetBuilding(s, buildingId);
            int steps = b != null ? PlotLayout.LocalSteps(b.Plot) : 8;
            Begin(ev, c, fk, fid, tk, tid, new int[0], Math.Max(1, steps * s.Economy.Tuning.CarrierTicksPerStep), state);
        }

        private static void BeginMain(WorldState s, EventBuffer ev, Carrier c, PlaceKind fk, int fid, PlaceKind tk, int tid, int[] corners, CarrierState state)
        {
            int steps = (corners.Length - 1) * StepsPerHexEdge;
            if (fk == PlaceKind.RoadSite) steps += StepsToRoadMidpoint;
            if (tk == PlaceKind.RoadSite) steps += StepsToRoadMidpoint;
            Begin(ev, c, fk, fid, tk, tid, corners, Math.Max(1, steps * s.Economy.Tuning.CarrierTicksPerStep), state);
        }

        private static void Begin(EventBuffer ev, Carrier c, PlaceKind fk, int fid, PlaceKind tk, int tid, int[] corners, int ticks, CarrierState state)
        {
            c.State = state;
            c.LegFromKind = fk;
            c.LegFromId = fid;
            c.LegToKind = tk;
            c.LegToId = tid;
            c.LegCorners = corners;
            c.LegTicks = ticks;
            c.Progress = 0;
            ev?.Add(new CarrierLegStarted { CarrierId = c.Id, FromKind = fk, FromId = fid, ToKind = tk, ToId = tid, LegTicks = ticks });
        }

        /// <summary>The hex edge a main-road carrier is on (or about to enter), or -1.</summary>
        public static int CurrentEdge(WorldState s, Carrier c)
        {
            if (c.LegCorners == null || c.LegCorners.Length < 2) return -1;
            int per = StepsPerHexEdge * s.Economy.Tuning.CarrierTicksPerStep;
            int offset = c.LegFromKind == PlaceKind.RoadSite ? StepsToRoadMidpoint * s.Economy.Tuning.CarrierTicksPerStep : 0;
            int p = c.Progress - offset;
            if (p < 0) return -1;
            int idx = p / Math.Max(1, per);
            if (idx >= c.LegCorners.Length - 1) return -1;
            return s.Board.Topology.HexEdgeBetween(c.LegCorners[idx], c.LegCorners[idx + 1]);
        }

        public static void Move(WorldState s, EventBuffer ev, EconomyHooks h)
        {
            var e = s.Economy;
            for (int i = 0; i < e.Carriers.Count; i++)
            {
                var c = e.Carriers[i];
                if (c.State == CarrierState.Idle) continue;
                int edge = CurrentEdge(s, c);
                if (edge >= 0 && EdgeBlocked(s, h, edge))
                {
                    var j = c.JobId >= 0 ? FindJob(s, c.JobId) : null;
                    if (j != null) Stall(j, Reasons.DemonOnRoute, ev);
                    continue; // waits on the road until the demons are banished
                }
                c.Progress++;
                if (c.Progress >= c.LegTicks) Arrive(s, ev, c);
            }
        }

        private static void Arrive(WorldState s, EventBuffer ev, Carrier c)
        {
            var e = s.Economy;
            var j = c.JobId >= 0 ? FindJob(s, c.JobId) : null;
            switch (c.State)
            {
                case CarrierState.ToPickup:
                    if (j == null) { GoHome(s, ev, c); return; }
                    Pickup(s, ev, c, j);
                    BeginLocal(s, ev, c, c.LegToKind, c.LegToId, PlaceKind.Storehouse, c.Home, CarrierState.Carrying);
                    return;
                case CarrierState.Carrying:
                    if (j == null) { GoHome(s, ev, c); return; }
                    if (c.LegToKind != j.ToKind || c.LegToId != j.ToId)
                    {
                        // The destination was cancelled while the goods were on the way: bring them home.
                        if (c.LegCorners.Length > 0) BeginMain(s, ev, c, c.LegToKind, c.LegToId, PlaceKind.Storehouse, c.Home, Reverse(c.LegCorners), CarrierState.Carrying);
                        else BeginLocal(s, ev, c, c.LegToKind, c.LegToId, PlaceKind.Storehouse, c.Home, CarrierState.Carrying);
                        return;
                    }
                    Deliver(s, ev, c, j);
                    if (j.ToKind == PlaceKind.Storehouse && j.ToId == c.Home) { Idle(c); return; }
                    if (j.ToKind == PlaceKind.Building)
                    {
                        // Take an output of that building back, if one is waiting for this settlement's carriers.
                        Job back = null;
                        foreach (var o in e.Jobs)
                            if (o.State == JobState.Waiting && o.FromKind == PlaceKind.Building && o.FromId == j.ToId && o.Settlement == c.Home)
                            { back = o; break; }
                        if (back != null)
                        {
                            Pickup(s, ev, c, back);
                            BeginLocal(s, ev, c, PlaceKind.Building, j.ToId, PlaceKind.Storehouse, c.Home, CarrierState.Carrying);
                            return;
                        }
                    }
                    GoHome(s, ev, c);
                    return;
                case CarrierState.Returning:
                    Idle(c);
                    return;
            }
        }

        private static void GoHome(WorldState s, EventBuffer ev, Carrier c)
        {
            c.JobId = -1;
            if (c.LegToKind == PlaceKind.Storehouse && c.LegToId == c.Home) { Idle(c); return; }
            if (c.LegCorners.Length > 0) BeginMain(s, ev, c, c.LegToKind, c.LegToId, PlaceKind.Storehouse, c.Home, Reverse(c.LegCorners), CarrierState.Returning);
            else BeginLocal(s, ev, c, c.LegToKind, c.LegToId, PlaceKind.Storehouse, c.Home, CarrierState.Returning);
        }

        private static void Idle(Carrier c)
        {
            c.State = CarrierState.Idle;
            c.JobId = -1;
            c.HasLoad = false;
            c.LoadCount = 0;
            c.LegFromKind = PlaceKind.Storehouse;
            c.LegFromId = c.Home;
            c.LegToKind = PlaceKind.Storehouse;
            c.LegToId = c.Home;
            c.LegCorners = new int[0];
            c.LegTicks = 0;
            c.Progress = 0;
        }

        private static void Deliver(WorldState s, EventBuffer ev, Carrier c, Job j)
        {
            var into = DeliveredArray(s, j.ToKind, j.ToId);
            into[(int)j.Ware] += j.Count;
            IncomingArray(s, j.ToKind, j.ToId)[(int)j.Ware] -= j.Count;
            c.HasLoad = false;
            c.LoadCount = 0;
            c.JobId = -1;
            s.Economy.Jobs.Remove(j);
            ev?.Add(new WareDelivered { JobId = j.Id, CarrierId = c.Id, Ware = j.Ware, Count = j.Count, ToKind = j.ToKind, ToId = j.ToId });
        }

        private static int[] Reverse(int[] a)
        {
            var r = (int[])a.Clone();
            Array.Reverse(r);
            return r;
        }

        /// <summary>
        /// A site was cancelled: waiting jobs to it are released; goods already on a carrier are redirected to the
        /// carrier's own storehouse (it brings them home after reaching the old destination).
        /// </summary>
        public static void OnSiteCancelled(WorldState s, PlaceKind kind, int id)
        {
            var e = s.Economy;
            foreach (var j in new List<Job>(e.Jobs))
            {
                if (j.ToKind != kind || j.ToId != id) continue;
                if (j.State != JobState.Carrying) { Release(s, j); continue; }
                var inc = IncomingArray(s, kind, id);
                inc[(int)j.Ware] -= j.Count;
                var c = e.Carriers[j.CarrierId];
                j.ToKind = PlaceKind.Storehouse;
                j.ToId = c.Home;
                e.Settlements[c.Home].Incoming[(int)j.Ware] += j.Count;
            }
        }

        // ---- positions ----

        /// <summary>Polyline (x0,z0,x1,z1,...) of the carrier's current leg; a single point at the storehouse when idle.</summary>
        public static double[] LegPoints(WorldState s, Carrier c)
        {
            var t = s.Board.Topology;
            var e = s.Economy;
            var pts = new List<double>();
            if (c.State == CarrierState.Idle || c.LegTicks <= 0)
            {
                var home = EconomyLogic.GetSettlement(s, c.Home);
                if (home != null) { pts.Add(t.VertexX(home.Corner)); pts.Add(t.VertexZ(home.Corner)); }
                return pts.ToArray();
            }
            if (c.LegCorners != null && c.LegCorners.Length > 0)
            {
                if (c.LegFromKind == PlaceKind.RoadSite) AddMid(s, pts, c.LegFromId);
                foreach (int k in c.LegCorners) { pts.Add(t.VertexX(k)); pts.Add(t.VertexZ(k)); }
                if (c.LegToKind == PlaceKind.RoadSite) AddMid(s, pts, c.LegToId);
                return pts.ToArray();
            }
            bool outward = c.LegToKind == PlaceKind.Building;
            var b = EconomyLogic.GetBuilding(s, outward ? c.LegToId : c.LegFromId);
            if (b == null) return pts.ToArray();
            var path = PlotLayout.PathPoints(t, b.Plot);
            if (outward) return path;
            var rev = new double[path.Length];
            for (int i = 0; i < path.Length; i += 2)
            {
                rev[path.Length - 2 - i] = path[i];
                rev[path.Length - 1 - i] = path[i + 1];
            }
            return rev;
        }

        private static void AddMid(WorldState s, List<double> pts, int roadId)
        {
            var r = EconomyLogic.GetRoad(s, roadId);
            if (r == null) return;
            pts.Add(s.Board.Topology.HexEdgeMidX(r.HexEdge));
            pts.Add(s.Board.Topology.HexEdgeMidZ(r.HexEdge));
        }

        public static void PointAlong(double[] pts, double f, out double x, out double z, out double yaw)
        {
            x = z = yaw = 0;
            if (pts.Length < 2) return;
            x = pts[0];
            z = pts[1];
            if (pts.Length < 4) return;
            double total = 0;
            for (int i = 2; i < pts.Length; i += 2) total += Math.Sqrt(Sq(pts[i] - pts[i - 2]) + Sq(pts[i + 1] - pts[i - 1]));
            double want = Math.Max(0, Math.Min(1, f)) * total;
            for (int i = 2; i < pts.Length; i += 2)
            {
                double dx = pts[i] - pts[i - 2], dz = pts[i + 1] - pts[i - 1];
                double len = Math.Sqrt(dx * dx + dz * dz);
                if (len <= 0) continue;
                yaw = PlotLayout.Yaw(dx, dz);
                if (want <= len || i == pts.Length - 2)
                {
                    double u = Math.Min(1, want / len);
                    x = pts[i - 2] + u * dx;
                    z = pts[i - 1] + u * dz;
                    return;
                }
                want -= len;
            }
        }

        private static double Sq(double v) => v * v;
    }

    /// <summary>Jobs (500): creates demand jobs in serving order and assigns idle carriers.</summary>
    internal sealed class JobSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public JobSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            Logistics.CreateDemand(s, ev, _hooks);
            Logistics.Assign(s, ev, _hooks);
        }
    }

    /// <summary>Carriers (600): walk their legs (waiting on demon-held road edges), pick up, deliver and return.</summary>
    internal sealed class CarrierSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public CarrierSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev) => Logistics.Move(s, ev, _hooks);
    }
}
