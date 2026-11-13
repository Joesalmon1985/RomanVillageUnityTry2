using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>Shared economy rules: lookups, placement checks, site placement, settlement creation. Internal to World.Sim.</summary>
    internal static class EconomyLogic
    {
        // ---- lookups ----

        public static bool IsGameOver(WorldState s) => s.Factions != null && s.Factions.GameOver;

        public static bool ValidFaction(int f) => f >= 0 && f < PlayerIds.Count;

        /// <summary>The settlement (site or active, not cancelled) on a corner, or null.</summary>
        public static Settlement SettlementAt(WorldState s, int corner)
        {
            if (s.Economy == null) return null;
            foreach (var st in s.Economy.Settlements)
                if (st.Corner == corner && st.State != SettlementState.Cancelled) return st;
            return null;
        }

        public static Settlement GetSettlement(WorldState s, int id) =>
            s.Economy != null && id >= 0 && id < s.Economy.Settlements.Count ? s.Economy.Settlements[id] : null;

        public static Building GetBuilding(WorldState s, int id) =>
            s.Economy != null && id >= 0 && id < s.Economy.Buildings.Count ? s.Economy.Buildings[id] : null;

        public static Road GetRoad(WorldState s, int id) =>
            s.Economy != null && id >= 0 && id < s.Economy.Roads.Count ? s.Economy.Roads[id] : null;

        public static bool IsGone(Building b) => b.State == BuildingState.Retired || b.State == BuildingState.Cancelled;

        /// <summary>The building standing on a plot (site, constructing, active or retiring), or null.</summary>
        public static Building BuildingOnPlot(WorldState s, int plot)
        {
            if (s.Economy == null) return null;
            foreach (var b in s.Economy.Buildings)
                if (b.Plot == plot && !IsGone(b)) return b;
            return null;
        }

        /// <summary>Demons on a corner (0 without the Demons module).</summary>
        public static int CornerDemons(WorldState s, int corner) =>
            s.Demons != null && s.Demons.Corner != null && corner >= 0 && corner < s.Demons.Corner.Length ? s.Demons.Corner[corner] : 0;

        public static int EdgeDemons(WorldState s, int hexEdge) =>
            s.Demons != null && s.Demons.Edge != null && hexEdge >= 0 && hexEdge < s.Demons.Edge.Length ? s.Demons.Edge[hexEdge] : 0;

        /// <summary>A settlement is halted while demons sit on its corner.</summary>
        public static bool SettlementHalted(WorldState s, int settlementId)
        {
            var st = GetSettlement(s, settlementId);
            return st != null && CornerDemons(s, st.Corner) > 0;
        }

        /// <summary>True when every adjacent finished settlement of the hex is demon-held and there is at least one.</summary>
        public static bool HexStopped(WorldState s, int hex)
        {
            if (s.Economy == null) return false;
            var t = s.Board.Topology;
            bool any = false;
            foreach (int c in t.HexCorners(hex))
            {
                var st = SettlementAt(s, c);
                if (st == null || st.State != SettlementState.Active) continue;
                any = true;
                if (CornerDemons(s, c) == 0) return false;
            }
            return any;
        }

        /// <summary>A built road of the faction ends at the corner.</summary>
        public static bool OwnRoadEndsAt(WorldState s, int faction, int corner)
        {
            var t = s.Board.Topology;
            foreach (int e in t.CornerHexEdges(corner))
            {
                var r = RoadOnEdge(s, e);
                if (r != null && r.Faction == faction && r.State == RoadState.Built) return true;
            }
            return false;
        }

        public static Road RoadOnEdge(WorldState s, int hexEdge)
        {
            var e = s.Economy;
            if (e == null || e.RoadOnEdge == null || hexEdge < 0 || hexEdge >= e.RoadOnEdge.Length) return null;
            int id = e.RoadOnEdge[hexEdge];
            return id >= 0 && id < e.Roads.Count ? e.Roads[id] : null;
        }

        /// <summary>
        /// Nearest finished settlement of the faction reachable from the start corners over its built roads (by road
        /// edges, ties by lower corner id), never passing through another people's settlement corner. -1 when none.
        /// </summary>
        public static int NearestConnectedSettlement(WorldState s, int faction, IEnumerable<int> startCorners)
        {
            var t = s.Board.Topology;
            var dist = new int[t.CornerCount];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var frontier = new List<int>();
            foreach (int c in startCorners)
                if (t.IsCorner(c) && dist[c] < 0) { dist[c] = 0; frontier.Add(c); }
            while (frontier.Count > 0)
            {
                frontier.Sort();
                int best = -1;
                foreach (int c in frontier)
                {
                    var st = SettlementAt(s, c);
                    if (st != null && st.Faction == faction && st.State == SettlementState.Active) { best = st.Id; break; }
                }
                if (best >= 0) return best;
                var next = new List<int>();
                foreach (int c in frontier)
                {
                    var here = SettlementAt(s, c);
                    if (here != null && here.Faction != faction) continue; // never through another people's camp
                    foreach (int e in t.CornerHexEdges(c))
                    {
                        var r = RoadOnEdge(s, e);
                        if (r == null || r.Faction != faction || r.State != RoadState.Built) continue;
                        int o = t.HexEdgeCornerA(e) == c ? t.HexEdgeCornerB(e) : t.HexEdgeCornerA(e);
                        if (dist[o] >= 0) continue;
                        dist[o] = dist[c] + 1;
                        next.Add(o);
                    }
                }
                frontier = next;
            }
            return -1;
        }

        /// <summary>Settlement ids of the faction connected to the given settlement by its built roads (including itself).</summary>
        public static List<int> ConnectedSettlements(WorldState s, int settlementId)
        {
            var result = new List<int>();
            var home = GetSettlement(s, settlementId);
            if (home == null) return result;
            var t = s.Board.Topology;
            var seen = new bool[t.CornerCount];
            var stack = new Stack<int>();
            seen[home.Corner] = true;
            stack.Push(home.Corner);
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                var st = SettlementAt(s, c);
                if (st != null && st.Faction != home.Faction) continue;
                if (st != null && st.State == SettlementState.Active) result.Add(st.Id);
                foreach (int e in t.CornerHexEdges(c))
                {
                    var r = RoadOnEdge(s, e);
                    if (r == null || r.Faction != home.Faction || r.State != RoadState.Built) continue;
                    int o = t.HexEdgeCornerA(e) == c ? t.HexEdgeCornerB(e) : t.HexEdgeCornerA(e);
                    if (seen[o]) continue;
                    seen[o] = true;
                    stack.Push(o);
                }
            }
            result.Sort();
            return result;
        }

        // ---- checks ----

        private static CommandResult R(string code, params string[] args) => EconomyRefusals.Reject(code, args);

        public static CommandResult CheckBuilding(WorldState s, int faction, BuildingType type, int plot)
        {
            if (s == null || s.Economy == null || s.Board == null) return R(EconomyRefusals.UnknownPlot);
            if (IsGameOver(s)) return R(EconomyRefusals.GameOver);
            if (!ValidFaction(faction)) return R(EconomyRefusals.NotOwner);
            var def = BuildingDefs.Get(type);
            if (def == null) return R(EconomyRefusals.UnknownType);
            if (!def.Placeable) return R(EconomyRefusals.NotPlaceable, def.Name);
            var t = s.Board.Topology;
            if (!PlotLayout.IsValidPlot(t, plot)) return R(EconomyRefusals.UnknownPlot);
            var st = SettlementAt(s, PlotLayout.CornerOf(plot));
            if (st == null) return R(EconomyRefusals.NoSettlement);
            if (st.Faction != faction) return R(EconomyRefusals.NotOwner);
            if (st.State != SettlementState.Active) return R(EconomyRefusals.NoSettlement);
            if (System.Array.IndexOf(s.Board.ReservedPlots ?? new int[0], plot) >= 0) return R(EconomyRefusals.PlotReserved);
            if (PlotLayout.SlotOf(plot) == PlotLayout.CitySlot && !st.IsCity) return R(EconomyRefusals.PlotLocked);
            if (BuildingOnPlot(s, plot) != null) return R(EconomyRefusals.PlotTaken);
            int hex = PlotLayout.PlotHex(t, plot);
            if (def.IsCollector && (hex < 0 || s.Board.HexTerrain[hex] != def.Terrain))
                return R(EconomyRefusals.WrongTerrain, def.Name.ToLowerInvariant(), TerrainInfo.DisplayName(def.Terrain));
            foreach (var b in s.Economy.Buildings)
                if (b.Settlement == st.Id && b.Type == type && !IsGone(b)) return R(EconomyRefusals.AlreadyBuilt, def.Name.ToLowerInvariant());
            return CommandResult.Accept();
        }

        public static CommandResult CheckRoad(WorldState s, int faction, int hexEdge)
        {
            if (s == null || s.Economy == null || s.Board == null) return R(EconomyRefusals.NotConnected);
            if (IsGameOver(s)) return R(EconomyRefusals.GameOver);
            if (!ValidFaction(faction)) return R(EconomyRefusals.NotOwner);
            var t = s.Board.Topology;
            if (hexEdge < 0 || hexEdge >= t.HexEdgeCount) return R(EconomyRefusals.NotConnected);
            if (RoadOnEdge(s, hexEdge) != null) return R(EconomyRefusals.RoadExists);
            int a = t.HexEdgeCornerA(hexEdge), b = t.HexEdgeCornerB(hexEdge);
            var sa = SettlementAt(s, a);
            var sb = SettlementAt(s, b);
            if ((sa != null && sa.Faction != faction) || (sb != null && sb.Faction != faction)) return R(EconomyRefusals.BlockedByRival);
            bool joinsA = (sa != null && sa.State == SettlementState.Active) || OwnRoadEndsAt(s, faction, a);
            bool joinsB = (sb != null && sb.State == SettlementState.Active) || OwnRoadEndsAt(s, faction, b);
            if (!joinsA && !joinsB) return R(EconomyRefusals.NotConnected);
            return CommandResult.Accept();
        }

        public static CommandResult CheckSettlement(WorldState s, int faction, int corner)
        {
            if (s == null || s.Economy == null || s.Board == null) return R(EconomyRefusals.BadCorner);
            if (IsGameOver(s)) return R(EconomyRefusals.GameOver);
            if (!ValidFaction(faction)) return R(EconomyRefusals.NotOwner);
            var t = s.Board.Topology;
            if (!PlotLayout.IsSettlementCorner(t, corner)) return R(EconomyRefusals.BadCorner);
            if (SettlementAt(s, corner) != null) return R(EconomyRefusals.Occupied);
            foreach (int n in t.CornerNeighbours(corner))
                if (SettlementAt(s, n) != null) return R(EconomyRefusals.TooClose);
            if (!OwnRoadEndsAt(s, faction, corner) || NearestConnectedSettlement(s, faction, new[] { corner }) < 0)
                return R(EconomyRefusals.NotConnected);
            return CommandResult.Accept();
        }

        public static CommandResult CheckCity(WorldState s, int faction, int corner)
        {
            if (s == null || s.Economy == null) return R(EconomyRefusals.NotASettlement);
            if (IsGameOver(s)) return R(EconomyRefusals.GameOver);
            if (!ValidFaction(faction)) return R(EconomyRefusals.NotOwner);
            var st = SettlementAt(s, corner);
            if (st == null) return R(EconomyRefusals.NotASettlement);
            if (st.Faction != faction) return R(EconomyRefusals.NotOwner);
            if (st.State != SettlementState.Active) return R(EconomyRefusals.NotASettlement);
            if (st.IsCity) return R(EconomyRefusals.AlreadyCity);
            if (st.Upgrade != UpgradeState.None) return R(EconomyRefusals.UpgradeInProgress);
            return CommandResult.Accept();
        }

        public static CommandResult CheckCancel(WorldState s, int faction, SiteKind kind, int id)
        {
            if (s == null || s.Economy == null) return R(EconomyRefusals.NotASite);
            if (IsGameOver(s)) return R(EconomyRefusals.GameOver);
            if (!ValidFaction(faction)) return R(EconomyRefusals.NotOwner);
            int owner;
            switch (kind)
            {
                case SiteKind.Building:
                {
                    var b = GetBuilding(s, id);
                    if (b == null || (b.State != BuildingState.Site && b.State != BuildingState.Constructing)) return R(EconomyRefusals.NotASite);
                    owner = b.Faction;
                    break;
                }
                case SiteKind.Road:
                {
                    var r = GetRoad(s, id);
                    if (r == null || (r.State != RoadState.Site && r.State != RoadState.Constructing)) return R(EconomyRefusals.NotASite);
                    owner = r.Faction;
                    break;
                }
                case SiteKind.Settlement:
                {
                    var st = GetSettlement(s, id);
                    if (st == null || st.State != SettlementState.Site) return R(EconomyRefusals.NotASite);
                    owner = st.Faction;
                    break;
                }
                case SiteKind.City:
                {
                    var st = GetSettlement(s, id);
                    if (st == null || st.State != SettlementState.Active || st.Upgrade == UpgradeState.None) return R(EconomyRefusals.NotASite);
                    owner = st.Faction;
                    break;
                }
                default:
                    return R(EconomyRefusals.NotASite);
            }
            return owner == faction ? CommandResult.Accept() : R(EconomyRefusals.NotOwner);
        }

        // ---- placement (callers have validated) ----

        public static int PlaceBuildingSite(WorldState s, int faction, BuildingType type, int plot, EventBuffer ev)
        {
            var e = s.Economy;
            var st = SettlementAt(s, PlotLayout.CornerOf(plot));
            var b = new Building
            {
                Id = e.Buildings.Count, Type = type, Faction = faction, Settlement = st.Id, Plot = plot,
                Hex = PlotLayout.PlotHex(s.Board.Topology, plot), State = BuildingState.Site,
            };
            e.Buildings.Add(b);
            ev?.Add(new SitePlaced { Kind = SiteKind.Building, Id = b.Id, Faction = faction });
            return b.Id;
        }

        public static int PlaceRoadSite(WorldState s, int faction, int hexEdge, EventBuffer ev)
        {
            var e = s.Economy;
            var t = s.Board.Topology;
            var r = new Road
            {
                Id = e.Roads.Count, Faction = faction, HexEdge = hexEdge, State = RoadState.Site,
                SourceSettlement = NearestConnectedSettlement(s, faction, new[] { t.HexEdgeCornerA(hexEdge), t.HexEdgeCornerB(hexEdge) }),
            };
            e.Roads.Add(r);
            e.RoadOnEdge[hexEdge] = r.Id;
            ev?.Add(new SitePlaced { Kind = SiteKind.Road, Id = r.Id, Faction = faction });
            return r.Id;
        }

        public static int PlaceSettlementSite(WorldState s, int faction, int corner, EventBuffer ev)
        {
            var st = AddSettlement(s, faction, corner, SettlementState.Site);
            st.SourceSettlement = NearestConnectedSettlement(s, faction, new[] { corner });
            ev?.Add(new SitePlaced { Kind = SiteKind.Settlement, Id = st.Id, Faction = faction });
            return st.Id;
        }

        public static int PlaceCitySite(WorldState s, int faction, int corner, EventBuffer ev)
        {
            var st = SettlementAt(s, corner);
            st.Upgrade = UpgradeState.Site;
            st.UpgradeProgress = 0;
            ev?.Add(new SitePlaced { Kind = SiteKind.City, Id = st.Id, Faction = faction });
            return st.Id;
        }

        /// <summary>Cancels a site (validated); delivered goods go back to the source storehouse stock.</summary>
        public static void Cancel(WorldState s, SiteKind kind, int id, EventBuffer ev)
        {
            int faction = -1;
            switch (kind)
            {
                case SiteKind.Building: Logistics.OnSiteCancelled(s, PlaceKind.Building, id); break;
                case SiteKind.Road: Logistics.OnSiteCancelled(s, PlaceKind.RoadSite, id); break;
                case SiteKind.Settlement: Logistics.OnSiteCancelled(s, PlaceKind.SettlementSite, id); break;
            }
            switch (kind)
            {
                case SiteKind.Building:
                {
                    var b = GetBuilding(s, id);
                    ReturnGoods(GetSettlement(s, b.Settlement), b.Delivered);
                    b.State = BuildingState.Cancelled;
                    b.BuildProgress = 0;
                    faction = b.Faction;
                    ev?.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State });
                    break;
                }
                case SiteKind.Road:
                {
                    var r = GetRoad(s, id);
                    ReturnGoods(GetSettlement(s, r.SourceSettlement), r.Delivered);
                    r.State = RoadState.Cancelled;
                    r.BuildProgress = 0;
                    if (s.Economy.RoadOnEdge[r.HexEdge] == r.Id) s.Economy.RoadOnEdge[r.HexEdge] = -1;
                    faction = r.Faction;
                    break;
                }
                case SiteKind.Settlement:
                {
                    var st = GetSettlement(s, id);
                    ReturnGoods(GetSettlement(s, st.SourceSettlement), st.Delivered);
                    st.State = SettlementState.Cancelled;
                    st.BuildProgress = 0;
                    faction = st.Faction;
                    break;
                }
                case SiteKind.City:
                {
                    var st = GetSettlement(s, id);
                    ReturnGoods(st, st.UpgradeDelivered);
                    st.Upgrade = UpgradeState.None;
                    st.UpgradeProgress = 0;
                    faction = st.Faction;
                    break;
                }
            }
            ev?.Add(new SiteCancelled { Kind = kind, Id = id, Faction = faction });
        }

        private static void ReturnGoods(Settlement to, int[] delivered)
        {
            for (int w = 0; w < delivered.Length; w++)
            {
                if (to != null) to.Stock[w] += delivered[w];
                delivered[w] = 0;
            }
        }

        // ---- settlements ----

        public static Settlement AddSettlement(WorldState s, int faction, int corner, SettlementState state)
        {
            var e = s.Economy;
            var st = new Settlement { Id = e.Settlements.Count, Faction = faction, Corner = corner, State = state, FoundedTick = s.Tick };
            e.Settlements.Add(st);
            return st;
        }

        /// <summary>A finished settlement: one primitive collector on slot 0 of each resource sector, and its carriers.</summary>
        public static void FurnishSettlement(WorldState s, Settlement st, int carriers)
        {
            var e = s.Economy;
            var t = s.Board.Topology;
            for (int sec = 0; sec < 3; sec++)
            {
                int hex = PlotLayout.SectorHex(t, st.Corner, sec);
                if (hex < 0) continue;
                var type = BuildingDefs.PrimitiveFor(s.Board.HexTerrain[hex]);
                if (!BuildingDefs.IsValid(type)) continue;
                int plot = PlotLayout.PlotId(st.Corner, sec, 0);
                if (BuildingOnPlot(s, plot) != null) continue;
                e.Buildings.Add(new Building
                {
                    Id = e.Buildings.Count, Type = type, Faction = st.Faction, Settlement = st.Id, Plot = plot, Hex = hex,
                    State = BuildingState.Active,
                });
            }
            AddCarriers(s, st, carriers);
        }

        public static void AddCarriers(WorldState s, Settlement st, int n)
        {
            var e = s.Economy;
            for (int i = 0; i < n; i++)
            {
                e.Carriers.Add(new Carrier
                {
                    Id = e.Carriers.Count, Faction = st.Faction, Home = st.Id, State = CarrierState.Idle,
                    LegFromKind = PlaceKind.Storehouse, LegFromId = st.Id, LegToKind = PlaceKind.Storehouse, LegToId = st.Id,
                });
            }
        }

        // ---- accounting ----

        /// <summary>Wares in the world per ware: settlement stock, building stock and inputs, site and city deliveries, carrier loads.</summary>
        public static int[] WaresInWorld(WorldState s)
        {
            var w = new int[Wares.Count];
            var e = s.Economy;
            if (e == null) return w;
            foreach (var st in e.Settlements)
                for (int i = 0; i < Wares.Count; i++) w[i] += st.Stock[i] + st.Delivered[i] + st.UpgradeDelivered[i];
            foreach (var b in e.Buildings)
                for (int i = 0; i < Wares.Count; i++) w[i] += b.Stock[i] + b.Delivered[i];
            foreach (var r in e.Roads)
                for (int i = 0; i < Wares.Count; i++) w[i] += r.Delivered[i];
            foreach (var c in e.Carriers)
                if (c.HasLoad) w[(int)c.Load] += c.LoadCount;
            return w;
        }
    }
}
