using System.Collections.Generic;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>One stalled thing for the Problems panel.</summary>
    public struct EconomyProblem
    {
        /// <summary>True for a delivery job, false for a building or site.</summary>
        public bool IsJob;
        /// <summary>Building id or job id.</summary>
        public int Id;
        public string Title;
        /// <summary>The reason sentence from EconomyApi.WaitingFor / DescribeJob.</summary>
        public string Sentence;
        public string Reason;
    }

    /// <summary>
    /// Inspect-panel text for buildings, sites, carriers, roads, jobs and hexes, built only from the sim's helpers
    /// (DescribeType, RecipeText, Describe, WaitingFor, DescribeJob, CostText) and plain state, so God View and first
    /// person show the same sentences.
    /// </summary>
    public static class EconomyText
    {
        public const string Warn = "#FFB84D";

        public static IEnumerable<string> BuildingLines(WorldState s, int id)
        {
            var b = EconomyApi.GetBuilding(s, id);
            if (b == null) { yield return "Gone."; yield break; }
            var def = EconomyApi.Def(s, b.Type);
            var type = EconomyApi.DescribeType(s, b.Type);
            var info = EconomyApi.Describe(s, id);
            var wait = EconomyApi.WaitingFor(s, id);
            bool stalled = wait.Reason != null || b.Blocked || (b.State == BuildingState.Active && EconomyLook.RobbedHarvester(s, b));
            string status = wait.Sentence;
            if (b.Blocked && wait.Reason != Reasons.DemonOnRoute) status = "A demon on its flag stops the " + info.Title + ". " + status;
            yield return stalled ? "<color=" + Warn + ">" + status + "</color>" : status;
            if (!string.IsNullOrEmpty(type.Purpose)) yield return "<i>" + type.Purpose + "</i>";
            if (!string.IsNullOrEmpty(type.RecipeText)) yield return "Recipe: " + type.RecipeText;
            if (!string.IsNullOrEmpty(type.NeedsText) && def.Kind != BuildingKind.Store) yield return type.NeedsText;
            if (EconomyLook.IsSite(b))
            {
                yield return "<b>Materials</b> (cost " + type.CostText + ")";
                foreach (var l in info.StockLines) yield return "  " + l;
                if (b.State == BuildingState.Constructing)
                    yield return "Construction " + EconomyLook.Percent(b.BuildProgress, def.BuildTicks);
            }
            else
            {
                if (info.StockLines.Length > 0)
                {
                    yield return "<b>Stock</b>";
                    foreach (var l in info.StockLines) yield return "  " + l;
                }
                else if (def.Kind != BuildingKind.Store) yield return "Stock: empty";
                if (def.Kind != BuildingKind.Store)
                    yield return "Priority " + b.Priority.ToString().ToLowerInvariant() + (b.Enabled ? ", switched on" : ", <color=" + Warn + ">switched off</color>");
                if (b.State == BuildingState.Active && (def.Kind == BuildingKind.Harvester || def.Kind == BuildingKind.Producer) && b.WorkProgress > 0)
                {
                    int ticks = def.Kind == BuildingKind.Harvester ? s.Economy.Tuning.HarvestTicks : def.WorkTicks;
                    yield return "Work cycle " + EconomyLook.Percent(b.WorkProgress, ticks);
                }
                if (def.Kind == BuildingKind.PowerPlant && b.FuelLeft > 0) yield return "Fuel left " + (b.FuelLeft / WorldSim.TicksPerSecond) + " s";
            }
            foreach (var l in JobLinesFor(s, id)) yield return l;
        }

        /// <summary>Lines for open jobs that start or end at a building (at most <paramref name="max"/>, then "and N more").</summary>
        public static IEnumerable<string> JobLinesFor(WorldState s, int buildingId, int max = 5)
        {
            int shown = 0, more = 0;
            foreach (var j in s.Economy.Jobs)
            {
                if (j.SourceId != buildingId && j.DestId != buildingId) continue;
                if (shown >= max) { more++; continue; }
                if (shown == 0) yield return "<b>Deliveries</b>";
                shown++;
                var info = EconomyApi.DescribeJob(s, j.Id);
                string status = j.Reason != null ? "<color=" + Warn + ">" + info.Status + "</color>" : info.Status;
                yield return "  " + info.Title + ": " + status;
            }
            if (more > 0) yield return "  and " + more + " more.";
        }

        public static string CarrierTitle(WorldState s, int carrierId)
        {
            var e = s.Economy;
            if (carrierId < 0 || carrierId >= e.Carriers.Count) return "Carrier";
            var c = e.Carriers[carrierId];
            return "Carrier " + (carrierId + 1) + (c.HasLoad ? " with 1 " + EconomyApi.WareName(c.Load) : "");
        }

        public static IEnumerable<string> CarrierLines(WorldState s, int carrierId)
        {
            var e = s.Economy;
            if (carrierId < 0 || carrierId >= e.Carriers.Count) yield break;
            var c = e.Carriers[carrierId];
            var store = e.Buildings[e.StorehouseId];
            switch (c.State)
            {
                case CarrierState.Idle:
                    if (c.AtVertex == store.Vertex) yield return "Idle at the storehouse, waiting for a job.";
                    else yield return "Waiting at this flag for a job; walks home after "
                        + (e.Tuning.CarrierWaitTicks / WorldSim.TicksPerSecond) + " s (" + (c.IdleTicks / WorldSim.TicksPerSecond) + " s so far).";
                    break;
                case CarrierState.Returning: yield return "Walking back to the storehouse (takes jobs at flags on the way)."; break;
                case CarrierState.ToPickup: yield return "Walking to a pickup."; break;
                default: yield return "Carrying a load."; break;
            }
            if (c.HasLoad) yield return "Load: <b>" + EconomyLook.WareTag(c.Load) + "</b>";
            if (c.JobId >= 0)
            {
                var j = EconomyLook.Job(s, c.JobId);
                if (j != null)
                {
                    var info = EconomyApi.DescribeJob(s, j.Id);
                    yield return "Job: " + info.Title;
                    yield return j.Reason != null ? "<color=" + Warn + ">" + info.Status + "</color>" : info.Status;
                }
            }
            if (c.CurrentEdge >= 0)
                yield return "On the road, " + EconomyLook.Percent(c.Progress, e.Tuning.TicksPerEdge) + " of a 20 m segment ("
                    + (e.Tuning.TicksPerEdge / (float)WorldSim.TicksPerSecond).ToString("0.#") + " s per segment).";
        }

        public static IEnumerable<string> RoadLines(WorldState s, int edge)
        {
            yield return "A 20 m road segment. Carriers walk roads from flag to flag; no road, no delivery.";
            yield return RoadCostRule(s);
            int on = 0;
            foreach (var c in s.Economy.Carriers)
            {
                if (c.CurrentEdge != edge) continue;
                on++;
                yield return "  Carrier " + (c.Id + 1) + (c.HasLoad ? " with 1 " + EconomyApi.WareName(c.Load) : " (empty)");
            }
            if (on == 0) yield return "No carrier on it right now.";
            yield return "Remove it with the Road tool in God View (right side of its panel).";
        }

        /// <summary>"Roads: 1 planks and 1 bricks pays for 3 segments (2 already paid)."</summary>
        public static string RoadCostRule(WorldState s)
        {
            var t = s.Economy.Tuning;
            int per = t.RoadSegmentsPerCost + (s.Economy.Bonus != null ? s.Economy.Bonus.RoadSegments : 0);
            string credit = s.Economy.RoadCredit > 0 ? " (" + s.Economy.RoadCredit + " already paid for)" : "";
            return "Roads: " + EconomyApi.CostText(t.RoadCost) + " from the storehouse pays for " + per + " segments" + credit + ".";
        }

        public static IEnumerable<string> JobLines(WorldState s, int jobId)
        {
            var info = EconomyApi.DescribeJob(s, jobId);
            var j = EconomyLook.Job(s, jobId);
            yield return j != null && j.Reason != null ? "<color=" + Warn + ">" + info.Status + "</color>" : info.Status;
            if (j == null) yield break;
            yield return j.CarrierId >= 0 ? "Carrier " + (j.CarrierId + 1) + (j.State == JobState.Carrying ? " has the load." : " is on the way to it.") : "No carrier assigned yet.";
        }

        /// <summary>Every stalled site, building and job with its sentence (buildings first, then jobs).</summary>
        public static List<EconomyProblem> Problems(WorldState s)
        {
            var list = new List<EconomyProblem>();
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State == BuildingState.Removed || b.Type == BuildingType.Storehouse) continue;
                bool robbed = b.State == BuildingState.Active && EconomyLook.RobbedHarvester(s, b);
                if (b.Reason == null && !b.Blocked && !robbed) continue;
                var w = EconomyApi.WaitingFor(s, b.Id);
                list.Add(new EconomyProblem { IsJob = false, Id = b.Id, Title = EconomyApi.Describe(s, b.Id).Title, Sentence = w.Sentence, Reason = b.Reason ?? Reasons.DemonOnRoute });
            }
            foreach (var j in s.Economy.Jobs)
            {
                if (j.Reason == null) continue;
                var info = EconomyApi.DescribeJob(s, j.Id);
                list.Add(new EconomyProblem { IsJob = true, Id = j.Id, Title = info.Title, Sentence = info.Status, Reason = j.Reason });
            }
            return list;
        }

        /// <summary>Lines for a hex: terrain, number, batch left, robber, harvesters touching it.</summary>
        public static IEnumerable<string> HexLines(WorldState s, int hex)
        {
            var terrain = s.Board.HexTerrain[hex];
            var raw = TerrainInfo.RawWare(terrain);
            if (raw.HasValue) yield return "Gives " + EconomyApi.WareName(raw.Value) + " when its number " + s.Board.HexNumber[hex] + " is drawn next to one of your cities.";
            else yield return "Open meadow: gives nothing, good for workshops.";
            var e = s.Economy;
            if (raw.HasValue)
            {
                int left = hex < e.HexBatchRemaining.Length ? e.HexBatchRemaining[hex] : 0;
                if (left > 0)
                {
                    yield return "<b>Batch: " + left + " " + EconomyApi.WareName(raw.Value) + " units authorised</b>, not yet harvested (carry-over cap " + e.Tuning.BatchCap + ").";
                    if (terrain == Terrain.Forest) yield return "A woodcutter turns each unit into 2 logs.";
                }
                else yield return "No batch waiting on this hex.";
                if (e.HexStopped != null && hex < e.HexStopped.Length && e.HexStopped[hex])
                    yield return "<color=" + Warn + ">Demons on every city touching it stop this hex producing.</color>";
                var names = new List<string>();
                foreach (var b in e.Buildings)
                {
                    if (b.State != BuildingState.Active) continue;
                    var d = EconomyApi.Def(s, b.Type);
                    if (d.Kind != BuildingKind.Harvester || d.Terrain != terrain) continue;
                    foreach (int h in s.Board.Topology.VertexHexes(b.Vertex)) if (h == hex) { names.Add(d.Name); break; }
                }
                yield return names.Count == 0 ? "No harvester touches it yet." : "Harvested by: " + string.Join(", ", names) + ".";
            }
        }

        /// <summary>Commission progress: buildings active and road-connected (of 6), storehouse planks and bricks.</summary>
        public static void Commission(WorldState s, out int built, out int planks, out int bricks, out bool complete)
        {
            built = 0;
            foreach (var t in EconomyLook.CommissionTypes) if (CommissionBuildingReady(s, t)) built++;
            var store = s.Economy.Buildings[s.Economy.StorehouseId];
            planks = store.Stock[(int)Ware.Planks];
            bricks = store.Stock[(int)Ware.Bricks];
            complete = s.Economy.Commission.Complete;
        }

        public static bool CommissionBuildingReady(WorldState s, BuildingType t)
        {
            foreach (var b in s.Economy.Buildings)
                if (b.Type == t && b.State == BuildingState.Active && b.Owner == PlayerIds.Human && EconomyApi.IsConnected(s, b.Id)) return true;
            return false;
        }

        /// <summary>"buildings 3/6, planks 7/10, bricks 4/10".</summary>
        public static string CommissionProgress(WorldState s)
        {
            Commission(s, out int built, out int planks, out int bricks, out _);
            var t = s.Economy.Tuning;
            return "buildings " + built + "/" + EconomyLook.CommissionTypes.Length + ", planks " + planks + "/" + t.CommissionPlanks + ", bricks " + bricks + "/" + t.CommissionBricks;
        }

        public static string JoinAnd(IList<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            var head = new List<string>();
            for (int i = 0; i < items.Count - 1; i++) head.Add(items[i]);
            return string.Join(", ", head) + " and " + items[items.Count - 1];
        }
    }
}
