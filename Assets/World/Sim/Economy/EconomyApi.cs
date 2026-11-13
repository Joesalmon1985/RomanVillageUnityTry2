using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace World.Sim.Economy
{
    /// <summary>Player-facing description of a building, site or job (for inspect panels).</summary>
    public struct EconomyInfo
    {
        public string Title;
        /// <summary>One sentence: the stall reason sentence, or what it is doing.</summary>
        public string Status;
        /// <summary>Reason code (<see cref="Reasons"/>) or null when progressing.</summary>
        public string Reason;
        public string[] StockLines;
    }

    /// <summary>
    /// Public surface of the economy for other modules and views. State reads are also fine directly
    /// from <c>WorldState.Economy</c>; writes from other modules go through these methods only.
    /// The economy belongs to <see cref="PlayerIds.Human"/>.
    /// </summary>
    public static class EconomyApi
    {
        private static readonly ConditionalWeakTable<WorldSim, EconomyHooks> HookTable = new ConditionalWeakTable<WorldSim, EconomyHooks>();

        /// <summary>The hook lists of this sim (created by <see cref="EconomyModule.Register"/>). Subscribe in your Register.</summary>
        public static EconomyHooks Hooks(WorldSim sim) => HookTable.GetValue(sim, _ => new EconomyHooks());

        // ---- Rounds and batches ----

        /// <summary>
        /// Add a batch of raw units on a hex, at most once per hex per round (returns false if already
        /// authorised this round, the hex is Meadow, or amount &lt;= 0). Forest batches are halved
        /// (rounded up) unless an active Forester touches the hex. Emits BatchAuthorised.
        /// </summary>
        public static bool AuthoriseBatch(WorldState s, int hex, int amount, EventBuffer ev) =>
            EconomyLogic.AuthoriseBatch(s, hex, amount, ev);

        /// <summary>Raw units still to harvest on a hex.</summary>
        public static int BatchRemaining(WorldState s, int hex) => s.Economy.HexBatchRemaining[hex];

        // ---- Cities and territory ----

        /// <summary>Add a city on a free flag (strategy validates its own rules, e.g. corners only). Returns the city id or -1 if the flag is taken.</summary>
        public static int AddCity(WorldState s, int player, int vertex)
        {
            if (vertex < 0 || vertex >= s.Board.Topology.VertexCount) return -1;
            if (EconomyLogic.BuildingAt(s, vertex) != null || EconomyLogic.CityAt(s, vertex) != null) return -1;
            var c = new City { Id = s.Economy.Cities.Count, Owner = player, Vertex = vertex };
            s.Economy.Cities.Add(c);
            return c.Id;
        }

        /// <summary>True when the flag is within Tuning.TerritorySegments segments (fine graph, roads not needed) of one of the player's cities.</summary>
        public static bool IsInTerritory(WorldState s, int player, int vertex) => EconomyLogic.IsInTerritory(s, player, vertex);

        /// <summary>
        /// Storehouse wares needed to build the next <paramref name="segments"/> road segments, counting the road
        /// credit already paid (1 RoadCost covers Tuning.RoadSegmentsPerCost segments).
        /// </summary>
        public static int[] RoadCostFor(WorldState s, int segments) => EconomyLogic.RoadCostFor(s, segments);

        /// <summary>True when a city, building or site occupies the vertex.</summary>
        public static bool IsVertexOccupied(WorldState s, int vertex) =>
            EconomyLogic.BuildingAt(s, vertex) != null || EconomyLogic.CityAt(s, vertex) != null;

        /// <summary>Same check as the PlaceBuilding command (for previews without a sim).</summary>
        public static CommandResult CheckPlacement(WorldState s, int player, BuildingType type, int vertex) =>
            EconomyLogic.CheckPlacement(s, player, type, vertex);

        // ---- Buildings ----

        /// <summary>Lowest-id live building of a type (any state; pass activeOnly for Active ones), or -1.</summary>
        public static int FindBuilding(WorldState s, BuildingType type, bool activeOnly = false)
        {
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Type != type || !EconomyLogic.IsLive(b)) continue;
                if (activeOnly && b.State != BuildingState.Active) continue;
                return b.Id;
            }
            return -1;
        }

        /// <summary>Live building id on a vertex, or -1.</summary>
        public static int BuildingAt(WorldState s, int vertex)
        {
            var b = EconomyLogic.BuildingAt(s, vertex);
            return b == null ? -1 : b.Id;
        }

        public static Building GetBuilding(WorldState s, int id) => EconomyLogic.Get(s, id);

        public static BuildingDef Def(WorldState s, BuildingType type) => s.Economy.Tuning.Def(type);

        /// <summary>True when an active building has a road route to the storehouse (ignoring demons).</summary>
        public static bool IsConnected(WorldState s, int buildingId)
        {
            var b = EconomyLogic.Get(s, buildingId);
            return EconomyLogic.IsLive(b) && EconomyLogic.Route(s, null, EconomyLogic.Storehouse(s).Vertex, b.Vertex) != null;
        }

        /// <summary>Power plant has fuel time left.</summary>
        public static bool IsFuelled(WorldState s, int buildingId)
        {
            var b = EconomyLogic.Get(s, buildingId);
            return b != null && b.State == BuildingState.Active && b.Enabled && b.FuelLeft > 0;
        }

        public static bool HasRoad(WorldState s, int edge) => edge >= 0 && edge < s.Economy.Roads.Length && s.Economy.Roads[edge];

        // ---- Wares for other modules ----

        /// <summary>Ask carriers to bring count units of a ware from the storehouse to an active building's store.</summary>
        public static bool RequestWares(WorldState s, int buildingId, Ware ware, int count)
        {
            var b = EconomyLogic.Get(s, buildingId);
            if (b == null || b.State != BuildingState.Active || count <= 0) return false;
            if (EconomyLogic.Def(s, b).Kind == BuildingKind.Store) return false;
            b.Requested[(int)ware] += count;
            return true;
        }

        /// <summary>Unreserved count of a ware in a building's store.</summary>
        public static int Available(WorldState s, int buildingId, Ware ware)
        {
            var b = EconomyLogic.Get(s, buildingId);
            return b == null ? 0 : b.Available(ware);
        }

        /// <summary>Spend unreserved wares from a building's store (all or nothing).</summary>
        public static bool TryConsume(WorldState s, int buildingId, Ware ware, int count)
        {
            var b = EconomyLogic.Get(s, buildingId);
            if (!EconomyLogic.IsLive(b) || count < 0 || b.Available(ware) < count) return false;
            b.Stock[(int)ware] -= count;
            s.Economy.Ledger.Consumed[(int)ware] += count;
            return true;
        }

        /// <summary>Unreserved (spendable) count of a ware in the storehouse.</summary>
        public static int StorehouseCount(WorldState s, Ware ware) => EconomyLogic.Storehouse(s).Available(ware);

        public static bool TrySpendFromStorehouse(WorldState s, Ware ware, int count) =>
            TryConsume(s, s.Economy.StorehouseId, ware, count);

        /// <summary>Add an idle carrier at the storehouse (strategy: each new city brings carriers). Returns its id.</summary>
        public static int AddCarrier(WorldState s)
        {
            var c = new Carrier { Id = s.Economy.Carriers.Count, State = CarrierState.Idle, AtVertex = EconomyLogic.Storehouse(s).Vertex };
            s.Economy.Carriers.Add(c);
            return c.Id;
        }

        /// <summary>Add wares to the storehouse from outside the economy (trade, card rewards).</summary>
        public static void AddToStorehouse(WorldState s, Ware ware, int count)
        {
            if (count <= 0) return;
            EconomyLogic.Storehouse(s).Stock[(int)ware] += count;
            s.Economy.Ledger.Added[(int)ware] += count;
        }

        /// <summary>Wares now in the world (all stores plus carrier loads), per ware.</summary>
        public static int[] WaresInWorld(WorldState s)
        {
            var t = new int[Wares.Count];
            foreach (var b in s.Economy.Buildings) for (int w = 0; w < Wares.Count; w++) t[w] += b.Stock[w];
            foreach (var c in s.Economy.Carriers) if (c.HasLoad) t[(int)c.Load]++;
            return t;
        }

        // ---- Diagnostics ----

        /// <summary>Title, one status sentence and stock lines for a building or site.</summary>
        public static EconomyInfo Describe(WorldState s, int buildingId)
        {
            var b = EconomyLogic.Get(s, buildingId);
            if (b == null) return new EconomyInfo { Title = "Nothing", Status = "That building does not exist.", StockLines = new string[0] };
            var def = EconomyLogic.Def(s, b);
            string subject = EconomyLogic.Subject(s, b);
            var info = new EconomyInfo { Title = subject, Reason = b.Reason };
            info.Status = b.Reason != null ? ReasonSentence(s, b) : IdleSentence(s, b, def);
            var lines = new List<string>();
            for (int w = 0; w < Wares.Count; w++)
            {
                string name = EconomyLogic.WareName((Ware)w);
                bool site = b.State == BuildingState.Site || b.State == BuildingState.Constructing;
                if (site && def.Cost[w] > 0)
                {
                    string l = name + " " + b.Stock[w] + "/" + def.Cost[w];
                    if (b.Incoming[w] > 0) l += " (" + b.Incoming[w] + " on the way)";
                    lines.Add(l);
                }
                else if (!site && (b.Stock[w] > 0 || b.Incoming[w] > 0 || b.Requested[w] > 0))
                {
                    string l = name + " " + b.Stock[w];
                    var extra = new List<string>();
                    if (b.Reserved[w] > 0) extra.Add(b.Reserved[w] + " waiting for pickup");
                    if (b.Incoming[w] > 0) extra.Add(b.Incoming[w] + " on the way");
                    if (b.Requested[w] > 0) extra.Add(b.Requested[w] + " requested");
                    if (extra.Count > 0) l += " (" + string.Join(", ", extra) + ")";
                    lines.Add(l);
                }
            }
            info.StockLines = lines.ToArray();
            return info;
        }

        /// <summary>Title and status sentence of an open delivery job (finished jobs: "Delivered.").</summary>
        public static EconomyInfo DescribeJob(WorldState s, int jobId)
        {
            var j = EconomyLogic.FindJob(s.Economy, jobId);
            if (j == null) return new EconomyInfo { Title = "Delivery", Status = "Delivered.", StockLines = new string[0] };
            var src = s.Economy.Buildings[j.SourceId];
            var dst = s.Economy.Buildings[j.DestId];
            string ware = EconomyLogic.WareName(j.Ware);
            string to = "the " + EconomyLogic.Subject(s, dst);
            var info = new EconomyInfo
            {
                Title = "1 " + ware + " from the " + EconomyLogic.Subject(s, src) + " to " + to,
                Reason = j.Reason,
                StockLines = new string[0],
            };
            switch (j.Reason)
            {
                case Reasons.NoCarrier: info.Status = Reasons.Sentence(Reasons.NoCarrier, "The " + ware + " for " + to); break;
                case Reasons.NoRoad: info.Status = Reasons.Sentence(Reasons.NoRoad, "the " + EconomyLogic.Subject(s, src), to); break;
                case Reasons.DemonOnRoute: info.Status = Reasons.Sentence(Reasons.DemonOnRoute, to); break;
                default:
                    info.Status = j.State == JobState.Carrying ? "A carrier is taking the " + ware + " to " + to + "."
                        : j.State == JobState.ToPickup ? "A carrier is on the way to pick up the " + ware + "."
                        : "Waiting for a carrier.";
                    break;
            }
            return info;
        }

        private static string ReasonSentence(WorldState s, Building b)
        {
            string subject = EconomyLogic.Subject(s, b);
            string ware = b.ReasonWare >= 0 ? EconomyLogic.WareName((Ware)b.ReasonWare) : null;
            switch (b.Reason)
            {
                case Reasons.NoRoad: return Reasons.Sentence(Reasons.NoRoad, "the " + subject, "the storehouse");
                case Reasons.DemonOnRoute: return Reasons.Sentence(Reasons.DemonOnRoute, "the " + subject);
                case Reasons.WaitingWare:
                case Reasons.InputMissing:
                    return ware != null ? Reasons.Sentence(b.Reason, subject, ware) : Reasons.Sentence(b.Reason, subject);
                default: return Reasons.Sentence(b.Reason, subject);
            }
        }

        private static string IdleSentence(WorldState s, Building b, BuildingDef def)
        {
            switch (b.State)
            {
                case BuildingState.Site: return def.Name + " site has all its materials.";
                case BuildingState.Constructing:
                    return def.Name + " is being built (" + (100 * b.BuildProgress / System.Math.Max(1, def.BuildTicks)) + "%).";
                case BuildingState.Removed: return "This site was cancelled.";
            }
            if (def.Kind == BuildingKind.Store) return "The storehouse holds the village's goods.";
            if (def.Kind == BuildingKind.PowerPlant && b.FuelLeft > 0) return def.Name + " is supplying power.";
            if (b.WorkProgress > 0) return def.Name + " is working.";
            return def.Name + " is ready.";
        }
    }
}
