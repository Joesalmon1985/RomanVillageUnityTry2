using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace World.Sim.Economy
{
    /// <summary>The last roll. RollIndex -1 and Tick -1 before the first roll.</summary>
    public struct DiceView
    {
        public int D1, D2, Sum, RollIndex;
        public long Tick;
    }

    /// <summary>Stock on one hex for views and the HUD. Stage0..2 are young units in the first, second and last third of their growth.</summary>
    public struct HexStockView
    {
        public int Hex;
        public Terrain Terrain;
        public Ware? Ware;
        public int Number, Pips, Mature, Young, Stage0, Stage1, Stage2, Cap;
        /// <summary>Tick the next young cohort matures, or -1.</summary>
        public long NextMatureTick;
        /// <summary>False for Hills and Mountain (mined: nothing young is drawn).</summary>
        public bool YoungVisible;
        /// <summary>Every adjacent finished settlement is demon-held: rolls add nothing.</summary>
        public bool Stopped;
    }

    public enum PlotUse { NoSettlement, Locked, Reserved, Free, Site, Building }

    public struct PlotView
    {
        public int Plot, Corner, Sector, Slot, Hex;
        public Terrain Terrain;
        public double X, Z, Yaw;
        /// <summary>The finished settlement owning the plot, or -1.</summary>
        public int SettlementId;
        public PlotUse Use;
        /// <summary>The building or site on the plot, or -1.</summary>
        public int BuildingId;
    }

    public struct BuildingInfo
    {
        public string Title, Status, RecipeText, CostText;
        public string[] StockLines;
    }

    /// <summary>Read helpers for the game layer (REFACTOR_SPEC 5.4, A.3). Static, never mutate, null-safe.</summary>
    public static class EconomyApi
    {
        private static readonly ConditionalWeakTable<WorldSim, EconomyHooks> HookTable = new ConditionalWeakTable<WorldSim, EconomyHooks>();

        /// <summary>The cross-module hook lists of a sim (subscribe in a module's Register).</summary>
        public static EconomyHooks Hooks(WorldSim sim) => HookTable.GetValue(sim, _ => new EconomyHooks());

        // ---- dice ----

        public static DiceView LastRoll(WorldState s)
        {
            var d = s?.Economy?.Dice;
            if (d == null || d.RollCount == 0) return new DiceView { RollIndex = -1, Tick = -1 };
            return new DiceView { D1 = d.LastD1, D2 = d.LastD2, Sum = d.LastD1 + d.LastD2, RollIndex = d.RollCount - 1, Tick = d.LastRollTick };
        }

        public static long TicksToNextRoll(WorldState s)
        {
            var d = s?.Economy?.Dice;
            return d == null ? 0 : System.Math.Max(0, d.NextRollTick - s.Tick);
        }

        /// <summary>Chances out of 36 of a number on two dice (0 for 7 and anything outside 2..12).</summary>
        public static int Pips(int number) => number < 2 || number > 12 || number == 7 ? 0 : 6 - System.Math.Abs(7 - number);

        // ---- hex stock ----

        public static HexStockView HexStock(WorldState s, int hex)
        {
            var v = new HexStockView { Hex = hex, NextMatureTick = -1 };
            if (s?.Board == null || hex < 0 || hex >= s.Board.HexTerrain.Length) return v;
            v.Terrain = s.Board.HexTerrain[hex];
            v.Ware = TerrainInfo.RawWare(v.Terrain);
            v.Number = s.Board.HexNumber[hex];
            v.Pips = Pips(v.Number);
            v.YoungVisible = !TerrainInfo.IsMined(v.Terrain) && v.Ware != null;
            var e = s.Economy;
            if (e == null || hex >= e.Hexes.Length) return v;
            v.Cap = e.Tuning.HexCap;
            var hs = e.Hexes[hex];
            v.Mature = hs.Mature;
            foreach (var c in hs.Young)
            {
                v.Young += c.Count;
                long grow = System.Math.Max(1, c.MatureTick - c.AddedTick);
                long stage = System.Math.Max(0, s.Tick - c.AddedTick) * 3 / grow;
                if (stage <= 0) v.Stage0 += c.Count;
                else if (stage == 1) v.Stage1 += c.Count;
                else v.Stage2 += c.Count;
                if (v.NextMatureTick < 0 || c.MatureTick < v.NextMatureTick) v.NextMatureTick = c.MatureTick;
            }
            v.Stopped = EconomyLogic.HexStopped(s, hex);
            return v;
        }

        // ---- settlements ----

        /// <summary>Every settlement by id, including sites (State Site) and cancelled sites (State Cancelled).</summary>
        public static IReadOnlyList<Settlement> Settlements(WorldState s) =>
            s?.Economy != null ? (IReadOnlyList<Settlement>)s.Economy.Settlements : new Settlement[0];

        /// <summary>The settlement or settlement site on a corner, or null.</summary>
        public static Settlement SettlementAt(WorldState s, int corner) => s == null ? null : EconomyLogic.SettlementAt(s, corner);

        /// <summary>"the home camp", "the Ash Clan's home camp", "the camp by the pasture and hills".</summary>
        public static string SettlementName(WorldState s, int settlementId)
        {
            var st = s == null ? null : EconomyLogic.GetSettlement(s, settlementId);
            if (st == null) return "a camp";
            var starts = s.Board.StartCorners ?? new int[0];
            bool home = st.Faction < starts.Length && starts[st.Faction] == st.Corner;
            string owner = st.Faction == PlayerIds.Rival ? "the Ash Clan's " : "the ";
            if (home) return owner + "home camp";
            return owner + "camp by the " + CornerTerrains(s, st.Corner);
        }

        /// <summary>Unreserved storehouse stock of a settlement.</summary>
        public static int StockOf(WorldState s, int settlementId, Ware w)
        {
            var st = s == null ? null : EconomyLogic.GetSettlement(s, settlementId);
            if (st == null || (int)w < 0 || (int)w >= Wares.Count) return 0;
            return st.Stock[(int)w] - st.Reserved[(int)w];
        }

        // ---- plots ----

        public static PlotView Plot(WorldState s, int plot)
        {
            var v = new PlotView { Plot = plot, Corner = -1, Sector = -1, Slot = -1, Hex = -1, SettlementId = -1, BuildingId = -1, Use = PlotUse.NoSettlement };
            if (s?.Board == null) return v;
            var t = s.Board.Topology;
            if (!PlotLayout.IsValidPlot(t, plot)) return v;
            v.Corner = PlotLayout.CornerOf(plot);
            v.Sector = PlotLayout.SectorOf(plot);
            v.Slot = PlotLayout.SlotOf(plot);
            v.Hex = PlotLayout.PlotHex(t, plot);
            v.Terrain = v.Hex >= 0 ? s.Board.HexTerrain[v.Hex] : Terrain.Meadow;
            v.X = PlotLayout.PlotX(t, plot);
            v.Z = PlotLayout.PlotZ(t, plot);
            v.Yaw = PlotLayout.PlotYaw(t, plot);
            var st = EconomyLogic.SettlementAt(s, v.Corner);
            if (st == null || st.State != SettlementState.Active) return v;
            v.SettlementId = st.Id;
            var b = EconomyLogic.BuildingOnPlot(s, plot);
            if (System.Array.IndexOf(s.Board.ReservedPlots ?? new int[0], plot) >= 0) v.Use = PlotUse.Reserved;
            else if (b != null)
            {
                v.BuildingId = b.Id;
                v.Use = b.State == BuildingState.Site || b.State == BuildingState.Constructing ? PlotUse.Site : PlotUse.Building;
            }
            else if (v.Slot == PlotLayout.CitySlot && !st.IsCity) v.Use = PlotUse.Locked;
            else v.Use = PlotUse.Free;
            return v;
        }

        /// <summary>The usable plots of a finished settlement (6, a city 9, minus reserved plots), ascending.</summary>
        public static int[] PlotsOf(WorldState s, int settlementId)
        {
            var st = s == null ? null : EconomyLogic.GetSettlement(s, settlementId);
            if (st == null || st.State != SettlementState.Active) return new int[0];
            var t = s.Board.Topology;
            var list = new List<int>();
            for (int sec = 0; sec < 3; sec++)
            {
                if (PlotLayout.SectorHex(t, st.Corner, sec) < 0) continue;
                for (int slot = 0; slot < PlotLayout.SlotsPerSector; slot++)
                {
                    if (slot == PlotLayout.CitySlot && !st.IsCity) continue;
                    int p = PlotLayout.PlotId(st.Corner, sec, slot);
                    if (System.Array.IndexOf(s.Board.ReservedPlots ?? new int[0], p) >= 0) continue;
                    list.Add(p);
                }
            }
            return list.ToArray();
        }

        // ---- buildings ----

        public static BuildingDef Def(BuildingType t) => BuildingDefs.Get(t);

        public static BuildingInfo DescribeBuilding(WorldState s, int buildingId)
        {
            var b = s == null ? null : EconomyLogic.GetBuilding(s, buildingId);
            if (b == null) return new BuildingInfo { Title = "", Status = "", RecipeText = "", CostText = "", StockLines = new string[0] };
            var def = BuildingDefs.Get(b.Type);
            var info = new BuildingInfo
            {
                Title = def.Name,
                RecipeText = RecipeText(def),
                CostText = CostText(def.Cost),
            };
            switch (b.State)
            {
                case BuildingState.Site: info.Status = WaitingSentence(s, SiteKind.Building, b.Id) ?? "A building site."; break;
                case BuildingState.Constructing: info.Status = "Being built (" + (100 * b.BuildProgress / System.Math.Max(1, def.BuildTicks)) + "%)."; break;
                case BuildingState.Retiring: info.Status = "Being given up for the better collector."; break;
                case BuildingState.Retired: info.Status = "Given up."; break;
                case BuildingState.Cancelled: info.Status = "Cancelled."; break;
                default:
                    if (b.Reason != null)
                    {
                        string detail = b.ReasonWare >= 0
                            ? (b.Reason == Reasons.NoBatch ? TerrainInfo.DisplayName(def.Terrain) : Wares.DisplayName((Ware)b.ReasonWare))
                            : null;
                        info.Status = detail != null ? Reasons.Sentence(b.Reason, def.Name, detail) : Reasons.Sentence(b.Reason, def.Name);
                    }
                    else info.Status = b.WorkProgress > 0 ? "Working." : "Ready.";
                    break;
            }
            var lines = new List<string>();
            for (int w = 0; w < Wares.Count; w++)
                if (b.Stock[w] > 0) lines.Add(Wares.Title((Ware)w) + ": " + b.Stock[w]);
            info.StockLines = lines.ToArray();
            return info;
        }

        /// <summary>Why a site is not progressing ("Sawmill is waiting for clay."), or null when it is progressing (or is no site).</summary>
        public static string WaitingSentence(WorldState s, SiteKind kind, int id)
        {
            if (s?.Economy == null) return null;
            var tu = s.Economy.Tuning;
            switch (kind)
            {
                case SiteKind.Building:
                {
                    var b = EconomyLogic.GetBuilding(s, id);
                    if (b == null || b.State != BuildingState.Site) return null;
                    var def = BuildingDefs.Get(b.Type);
                    return Waiting(def.Name, b.Delivered, def.Cost);
                }
                case SiteKind.Road:
                {
                    var r = EconomyLogic.GetRoad(s, id);
                    if (r == null) return null;
                    if ((r.State == RoadState.Site || r.State == RoadState.Constructing) && EconomyLogic.EdgeDemons(s, r.HexEdge) > 0)
                        return Reasons.Sentence(Reasons.DemonOnRoute, "the road");
                    if (r.State != RoadState.Site) return null;
                    if (r.SourceSettlement < 0) return Reasons.Sentence(Reasons.NoRoad, "The road site");
                    return Waiting("The road site", r.Delivered, tu.RoadCost);
                }
                case SiteKind.Settlement:
                {
                    var st = EconomyLogic.GetSettlement(s, id);
                    if (st == null || st.State != SettlementState.Site) return null;
                    if (st.SourceSettlement < 0) return Reasons.Sentence(Reasons.NoRoad, "The camp site");
                    return Waiting("The camp site", st.Delivered, tu.SettlementCost);
                }
                case SiteKind.City:
                {
                    var st = EconomyLogic.GetSettlement(s, id);
                    if (st == null || st.Upgrade != UpgradeState.Site) return null;
                    return Waiting("The city", st.UpgradeDelivered, tu.CityCost);
                }
            }
            return null;
        }

        private static string Waiting(string subject, int[] have, int[] cost)
        {
            for (int w = 0; w < cost.Length; w++)
                if (have[w] < cost[w]) return Reasons.Sentence(Reasons.WaitingWare, subject, Wares.DisplayName((Ware)w));
            return null;
        }

        // ---- roads ----

        /// <summary>The road or road site on a hex edge, or null.</summary>
        public static Road RoadOn(WorldState s, int hexEdge) => s == null ? null : EconomyLogic.RoadOnEdge(s, hexEdge);

        // ---- carriers ----

        /// <summary>
        /// Interpolated carrier position (metres) and facing (degrees, 0 = north, clockwise) at Tick + tickFraction
        /// (0..1) along its current leg: local legs follow PlotLayout.PathPoints, main-road legs the corner path
        /// (and the hex-edge midpoint of a road site). An idle carrier stands at its storehouse corner. False for an
        /// unknown carrier.
        /// </summary>
        public static bool CarrierPosition(WorldState s, int carrierId, double tickFraction, out double x, out double z, out double yaw)
        {
            x = z = yaw = 0;
            var e = s?.Economy;
            if (e == null || carrierId < 0 || carrierId >= e.Carriers.Count) return false;
            var c = e.Carriers[carrierId];
            var pts = Logistics.LegPoints(s, c);
            if (pts.Length < 2) return false;
            double f = c.LegTicks > 0 ? (c.Progress + Math.Max(0, Math.Min(1, tickFraction))) / c.LegTicks : 0;
            Logistics.PointAlong(pts, f, out x, out z, out yaw);
            return true;
        }

        public static string DescribeCarrier(WorldState s, int carrierId)
        {
            var e = s?.Economy;
            if (e == null || carrierId < 0 || carrierId >= e.Carriers.Count) return "";
            var c = e.Carriers[carrierId];
            string who = "A carrier of " + SettlementName(s, c.Home);
            if (c.JobId >= 0)
            {
                string job = DescribeJob(s, c.JobId);
                if (job.Length > 0) return who + ": " + char.ToLowerInvariant(job[0]) + job.Substring(1);
            }
            if (c.State == CarrierState.Returning) return who + ", walking home.";
            return who + ", waiting at the storehouse.";
        }

        public static string DescribeJob(WorldState s, int jobId)
        {
            var e = s?.Economy;
            if (e == null) return "";
            foreach (var j in e.Jobs)
            {
                if (j.Id != jobId) continue;
                return (j.IsShipment ? "Shipping " : "Carrying ") + j.Count + " " + Wares.DisplayName(j.Ware) + " from "
                    + PlaceName(s, j.FromKind, j.FromId) + " to " + PlaceName(s, j.ToKind, j.ToId) + ".";
            }
            return "";
        }

        private static string PlaceName(WorldState s, PlaceKind kind, int id)
        {
            switch (kind)
            {
                case PlaceKind.Storehouse: return "the storehouse of " + SettlementName(s, id);
                case PlaceKind.Building:
                {
                    var b = EconomyLogic.GetBuilding(s, id);
                    return b == null ? "a building" : "the " + BuildingDefs.Get(b.Type).Name.ToLowerInvariant();
                }
                case PlaceKind.RoadSite: return "a road site";
                case PlaceKind.SettlementSite: return "a camp site";
            }
            return "somewhere";
        }

        // ---- checks (never mutate) ----

        public static CommandResult CheckBuilding(WorldState s, int faction, BuildingType t, int plot) => EconomyLogic.CheckBuilding(s, faction, t, plot);
        public static CommandResult CheckRoad(WorldState s, int faction, int hexEdge) => EconomyLogic.CheckRoad(s, faction, hexEdge);
        public static CommandResult CheckSettlement(WorldState s, int faction, int corner) => EconomyLogic.CheckSettlement(s, faction, corner);
        public static CommandResult CheckCity(WorldState s, int faction, int corner) => EconomyLogic.CheckCity(s, faction, corner);

        // ---- wares ----

        public static string WareName(Ware w) => Wares.DisplayName(w);
        public static string WareTitle(Ware w) => Wares.Title(w);

        /// <summary>Display colour "#RRGGBB" of a ware (loads, icons).</summary>
        public static string WareColour(Ware w)
        {
            switch (w)
            {
                case Ware.Wood: return "#8B5A2B";
                case Ware.Clay: return "#B5653A";
                case Ware.Grain: return "#E3C25A";
                case Ware.Sheep: return "#EDE8DC";
                case Ware.Stone: return "#8E9196";
                case Ware.Planks: return "#C99A5B";
                case Ware.Bricks: return "#A33B2A";
                case Ware.Bread: return "#D9A050";
                case Ware.Meat: return "#9E3B3B";
                case Ware.CutStone: return "#B8BCC2";
                default: return "#FFFFFF";
            }
        }

        /// <summary>"2 wood + 1 clay"; "nothing" for an empty or null cost.</summary>
        public static string CostText(int[] cost)
        {
            if (cost == null) return "nothing";
            var sb = new StringBuilder();
            for (int w = 0; w < cost.Length && w < Wares.Count; w++)
            {
                if (cost[w] <= 0) continue;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(cost[w]).Append(' ').Append(Wares.DisplayName((Ware)w));
            }
            return sb.Length == 0 ? "nothing" : sb.ToString();
        }

        // ---- helpers shared with the other modules ----

        /// <summary>"pasture and hills", "forest, hills and fields" (distinct non-Meadow terrains of a corner, by hex id); "meadow" if none.</summary>
        public static string CornerTerrains(WorldState s, int corner)
        {
            var t = s.Board.Topology;
            var names = new List<string>();
            if (t.IsCorner(corner))
            {
                var hexes = new List<int>(t.VertexHexes(corner));
                hexes.Sort();
                foreach (int h in hexes)
                {
                    var terr = s.Board.HexTerrain[h];
                    if (terr == Terrain.Meadow) continue;
                    string n = TerrainInfo.DisplayName(terr);
                    if (!names.Contains(n)) names.Add(n);
                }
            }
            if (names.Count == 0) return "meadow";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        private static string RecipeText(BuildingDef def)
        {
            switch (def.Class)
            {
                case BuildingClass.PrimitiveCollector:
                case BuildingClass.ImprovedCollector:
                    return "1 " + TerrainInfo.DisplayName(def.Terrain) + " unit -> " + def.Yield + " " + Wares.DisplayName(TerrainInfo.RawWare(def.Terrain).Value);
                case BuildingClass.Lodge:
                    return CostText(def.Inputs) + " -> 1 warrior";
                default:
                    return CostText(def.Inputs) + " -> " + CostText(def.Outputs);
            }
        }
    }
}
