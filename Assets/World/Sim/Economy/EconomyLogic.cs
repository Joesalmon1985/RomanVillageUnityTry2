using System;
using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>Shared economy rules used by systems, commands and the API. Deterministic; id-ordered.</summary>
    internal static class EconomyLogic
    {
        // ---------------- Lookup ----------------

        internal static BuildingDef Def(WorldState s, Building b) => s.Economy.Tuning.Def(b.Type);

        internal static Building Storehouse(WorldState s) => s.Economy.Buildings[s.Economy.StorehouseId];

        internal static Building Get(WorldState s, int id) =>
            id >= 0 && id < s.Economy.Buildings.Count ? s.Economy.Buildings[id] : null;

        internal static DeliveryJob FindJob(EconomyState e, int id)
        {
            foreach (var j in e.Jobs) if (j.Id == id) return j;
            return null;
        }

        internal static bool IsLive(Building b) => b != null && b.State != BuildingState.Removed;

        /// <summary>Building (any live state) on a vertex, or null.</summary>
        internal static Building BuildingAt(WorldState s, int vertex)
        {
            foreach (var b in s.Economy.Buildings) if (IsLive(b) && b.Vertex == vertex) return b;
            return null;
        }

        internal static City CityAt(WorldState s, int vertex)
        {
            foreach (var c in s.Economy.Cities) if (c.Vertex == vertex) return c;
            return null;
        }

        internal static string Name(WorldState s, Building b) => Def(s, b).Name;

        internal static string Subject(WorldState s, Building b) =>
            b.State == BuildingState.Site || b.State == BuildingState.Constructing ? Name(s, b) + " site" : Name(s, b);

        internal static string WareName(Ware w) => Wares.DisplayName(w);

        // ---------------- Territory and placement ----------------

        internal static bool IsInTerritory(WorldState s, int player, int vertex)
        {
            var topo = s.Board.Topology;
            if (vertex < 0 || vertex >= topo.VertexCount) return false;
            foreach (var c in s.Economy.Cities)
            {
                if (c.Owner != player) continue;
                int d = topo.Distance(c.Vertex, vertex);
                if (d >= 0 && d <= s.Economy.Tuning.TerritorySegments) return true;
            }
            return false;
        }

        internal static bool TouchesTerrain(WorldState s, int vertex, Terrain t)
        {
            foreach (int h in s.Board.Topology.VertexHexes(vertex)) if (s.Board.HexTerrain[h] == t) return true;
            return false;
        }

        internal static CommandResult CheckPlacement(WorldState s, int player, BuildingType type, int vertex)
        {
            var topo = s.Board.Topology;
            if ((int)type < 0 || (int)type >= s.Economy.Tuning.Buildings.Length)
                return CommandResult.Reject(EconomyRefusals.UnknownType, "That kind of building does not exist.");
            var def = s.Economy.Tuning.Def(type);
            if (player != PlayerIds.Human)
                return CommandResult.Reject(EconomyRefusals.NotOwner, "Only the player's village builds here.");
            if (!def.Placeable)
                return CommandResult.Reject(Reasons.InvalidSite, "There is only one " + def.Name.ToLowerInvariant() + ".");
            if (vertex < 0 || vertex >= topo.VertexCount)
                return CommandResult.Reject(EconomyRefusals.BadPlace, "That is not a flag on the map.");
            var occ = BuildingAt(s, vertex);
            if (occ != null)
                return CommandResult.Reject(Reasons.InvalidSite, "That flag is already taken by the " + Name(s, occ) + ".");
            if (CityAt(s, vertex) != null)
                return CommandResult.Reject(Reasons.InvalidSite, "That flag is taken by a city.");
            if (vertex == s.Board.WarehouseVertex)
                return CommandResult.Reject(Reasons.InvalidSite, "That flag is kept for the warehouse.");
            if (!IsInTerritory(s, player, vertex))
                return CommandResult.Reject(Reasons.InvalidSite, "That flag is outside your territory (more than "
                    + s.Economy.Tuning.TerritorySegments + " road segments, "
                    + (int)(s.Economy.Tuning.TerritorySegments * BoardTopology.EdgeLength) + " m, from your cities).");
            if (def.NeedsTerrain && !TouchesTerrain(s, vertex, def.Terrain))
                return CommandResult.Reject(Reasons.InvalidSite,
                    "A " + def.Name.ToLowerInvariant() + " must stand next to " + TerrainPhrase(def.Terrain) + ".");
            return CommandResult.Accept();
        }

        private static string TerrainPhrase(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return "a forest";
                case Terrain.Hills: return "hills";
                case Terrain.Fields: return "fields";
                case Terrain.Mountain: return "a mountain";
                case Terrain.Pasture: return "a pasture";
                default: return "meadow";
            }
        }

        internal static Building AddBuilding(WorldState s, BuildingType type, int owner, int vertex, BuildingState state)
        {
            var b = new Building
            {
                Id = s.Economy.Buildings.Count,
                Type = type,
                Owner = owner,
                Vertex = vertex,
                State = state,
            };
            s.Economy.Buildings.Add(b);
            return b;
        }

        // ---------------- Roads ----------------

        /// <summary>True when the vertex is part of the player's network: a city, a building/site, or a road end.</summary>
        internal static bool OnNetwork(WorldState s, int player, int vertex)
        {
            foreach (var c in s.Economy.Cities) if (c.Owner == player && c.Vertex == vertex) return true;
            var b = BuildingAt(s, vertex);
            if (b != null && b.Owner == player) return true;
            foreach (int e in s.Board.Topology.VertexEdges(vertex)) if (s.Economy.Roads[e]) return true;
            return false;
        }

        internal static bool CanPay(Building store, int[] cost)
        {
            for (int w = 0; w < Wares.Count; w++) if (store.Stock[w] - store.Reserved[w] < cost[w]) return false;
            return true;
        }

        /// <summary>Take wares from a store and book them as consumed (and into the given sink row of the ledger).</summary>
        internal static void Pay(WorldState s, Building store, int[] cost, int[] sinkRow)
        {
            for (int w = 0; w < Wares.Count; w++)
            {
                store.Stock[w] -= cost[w];
                s.Economy.Ledger.Consumed[w] += cost[w];
                if (sinkRow != null) sinkRow[w] += cost[w];
            }
        }

        /// <summary>Road segments one road cost pays for (tuning plus card bonus).</summary>
        internal static int RoadSegmentsPerCost(WorldState s) =>
            Math.Max(1, s.Economy.Tuning.RoadSegmentsPerCost + (s.Economy.Bonus != null ? s.Economy.Bonus.RoadSegments : 0));

        /// <summary>Wares to take from the storehouse for the next n road segments, given the current road credit.</summary>
        internal static int[] RoadCostFor(WorldState s, int segments)
        {
            var t = s.Economy.Tuning;
            int per = RoadSegmentsPerCost(s);
            int unpaid = Math.Max(0, segments - s.Economy.RoadCredit);
            int payments = (unpaid + per - 1) / per;
            var c = new int[Wares.Count];
            for (int w = 0; w < Wares.Count; w++) c[w] = t.RoadCost[w] * payments;
            return c;
        }

        /// <summary>Pays for one road segment: uses the credit, else takes RoadCost and grants credit for the following segments.</summary>
        internal static void PayRoadSegment(WorldState s)
        {
            var e = s.Economy;
            if (e.RoadCredit > 0)
            {
                e.RoadCredit--;
                return;
            }
            Pay(s, Storehouse(s), e.Tuning.RoadCost, e.Ledger.Roads);
            e.RoadCredit = RoadSegmentsPerCost(s) - 1;
        }

        internal static string CostText(int[] cost)
        {
            var parts = new List<string>();
            for (int w = 0; w < Wares.Count; w++) if (cost[w] > 0) parts.Add(cost[w] + " " + WareName((Ware)w));
            return parts.Count == 0 ? "nothing" : string.Join(" and ", parts);
        }

        // ---------------- Routes ----------------

        /// <summary>Vertex path over player roads avoiding blocked flags (the start flag is never treated as blocked).</summary>
        internal static int[] Route(WorldState s, bool[] blocked, int from, int to)
        {
            var roads = s.Economy.Roads;
            var topo = s.Board.Topology;
            if (blocked == null) return topo.FindPath(from, to, e => roads[e]);
            return topo.FindPath(from, to, e =>
            {
                if (!roads[e]) return false;
                int a = topo.EdgeVertexA(e), b = topo.EdgeVertexB(e);
                if (a != from && blocked[a]) return false;
                if (b != from && blocked[b]) return false;
                return true;
            });
        }

        /// <summary>Null when a route exists, else demon_on_route (a road route exists but a flag blocks it) or no_road.</summary>
        internal static string RouteReason(WorldState s, bool[] blocked, int from, int to)
        {
            if (Route(s, blocked, from, to) != null) return null;
            if (blocked != null && Route(s, null, from, to) != null) return Reasons.DemonOnRoute;
            return Reasons.NoRoad;
        }

        // ---------------- Demand ----------------

        /// <summary>Recipe or construction need for a ware (without external requests); never negative.</summary>
        internal static int RecipeNeed(WorldState s, Building b, int w)
        {
            var def = Def(s, b);
            int n = 0;
            if (b.State == BuildingState.Site)
            {
                n = def.Cost[w] - b.Stock[w] - b.Incoming[w];
            }
            else if (b.State == BuildingState.Active && b.Enabled)
            {
                if (def.Kind == BuildingKind.Producer && def.Inputs[w] > 0)
                    n = s.Economy.Tuning.InputBuffer * def.Inputs[w] - b.Stock[w] - b.Incoming[w];
                else if (def.Kind == BuildingKind.PowerPlant && def.Inputs[w] > 0)
                    n = s.Economy.Tuning.FuelBuffer * def.Inputs[w] - b.Stock[w] - b.Incoming[w];
                else if (def.Kind == BuildingKind.Harvester && def.Inputs[w] > 0)
                    n = s.Economy.Tuning.UpkeepBuffer * def.Inputs[w] - b.Stock[w] - b.Incoming[w];
            }
            return n > 0 ? n : 0;
        }

        internal static int Need(WorldState s, Building b, int w)
        {
            if (b.State == BuildingState.Removed || Def(s, b).Kind == BuildingKind.Store) return 0;
            int req = b.State == BuildingState.Active ? b.Requested[w] : 0;
            return RecipeNeed(s, b, w) + req;
        }

        /// <summary>True when another active, enabled building uses ware w as a recipe input, upkeep or fuel.</summary>
        internal static bool HasRecipeConsumer(WorldState s, Building p, int w)
        {
            foreach (var b in s.Economy.Buildings)
            {
                if (b == p || b.State != BuildingState.Active || !b.Enabled) continue;
                var d = Def(s, b);
                if (d.Kind != BuildingKind.Store && d.Kind != BuildingKind.Plain && d.Inputs[w] > 0) return true;
            }
            return false;
        }

        internal static DeliveryJob CreateJob(WorldState s, EventBuffer ev, Ware w, Building src, Building dst)
        {
            var e = s.Economy;
            var j = new DeliveryJob
            {
                Id = e.NextJobId++,
                Ware = w,
                SourceId = src.Id,
                DestId = dst.Id,
                State = JobState.Waiting,
                CreatedTick = s.Tick,
            };
            src.Reserved[(int)w]++;
            dst.Incoming[(int)w]++;
            if (dst.State == BuildingState.Active && dst.Requested[(int)w] > 0 && RecipeNeed(s, dst, (int)w) == 0)
            {
                dst.Requested[(int)w]--;
                j.FromRequest = true;
            }
            e.Jobs.Add(j);
            ev?.Add(new JobCreated { JobId = j.Id, Ware = w, SourceId = src.Id, DestId = dst.Id });
            return j;
        }

        /// <summary>Consumers in service order: priority high first, then id.</summary>
        internal static List<Building> ServiceOrder(WorldState s)
        {
            var list = new List<Building>();
            foreach (var b in s.Economy.Buildings) if (IsLive(b)) list.Add(b);
            list.Sort((a, b) => a.Priority != b.Priority ? ((int)b.Priority).CompareTo((int)a.Priority) : a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>Create delivery jobs: producers push outputs (to a requesting consumer, else the storehouse), then consumers pull from the storehouse.</summary>
        internal static void CreateJobs(WorldState s, bool[] blocked, EventBuffer ev)
        {
            var e = s.Economy;
            var store = Storehouse(s);
            var order = ServiceOrder(s);

            // Push finished outputs.
            foreach (var p in order)
            {
                var pd = Def(s, p);
                if (p.State != BuildingState.Active) continue;
                if (pd.Kind != BuildingKind.Harvester && pd.Kind != BuildingKind.Producer) continue;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (pd.Outputs[w] <= 0) continue;
                    while (p.Stock[w] - p.Reserved[w] > 0)
                    {
                        // Fair share: highest priority first, then the consumer with the least on hand (in cycles of
                        // its recipe), then the nearest. So two workshops sharing one input both keep working.
                        Building best = null;
                        int bestFill = int.MaxValue, bestLen = int.MaxValue;
                        foreach (var c in order)
                        {
                            if (c == p || Need(s, c, w) <= 0) continue;
                            if (best != null && c.Priority < best.Priority) break; // order is priority-desc
                            int fill = Fill(s, c, w);
                            if (best != null && fill > bestFill) continue;
                            var path = Route(s, blocked, p.Vertex, c.Vertex);
                            if (path == null) continue;
                            if (best == null || fill < bestFill || (fill == bestFill && path.Length < bestLen))
                            {
                                best = c;
                                bestFill = fill;
                                bestLen = path.Length;
                            }
                        }
                        if (best == null)
                        {
                            // Keep a few units for a local recipe consumer instead of hauling them to the storehouse and back.
                            int hold = HasRecipeConsumer(s, p, w) ? e.Tuning.HoldForConsumer : 0;
                            if (p.Stock[w] - p.Reserved[w] <= hold) break;
                            if (Route(s, blocked, p.Vertex, store.Vertex) == null) break;
                            best = store;
                        }
                        CreateJob(s, ev, (Ware)w, p, best);
                    }
                }
            }

            // Pull from the storehouse, one unit at a time to the consumer with the least on hand (priority first).
            var routeOk = new int[e.Buildings.Count]; // 0 unknown, 1 ok, 2 none
            for (int w = 0; w < Wares.Count; w++)
            {
                while (store.Stock[w] - store.Reserved[w] > 0)
                {
                    Building best = null;
                    int bestFill = int.MaxValue;
                    foreach (var c in order)
                    {
                        if (c == store || Need(s, c, w) <= 0) continue;
                        if (best != null && c.Priority < best.Priority) break;
                        int fill = Fill(s, c, w);
                        if (best != null && fill >= bestFill) continue;
                        if (routeOk[c.Id] == 0) routeOk[c.Id] = Route(s, blocked, store.Vertex, c.Vertex) != null ? 1 : 2;
                        if (routeOk[c.Id] != 1) continue;
                        best = c;
                        bestFill = fill;
                    }
                    if (best == null) break;
                    CreateJob(s, ev, (Ware)w, store, best);
                }
            }
        }

        /// <summary>
        /// How well a consumer is supplied with ware w, in hundredths of what one cycle (recipe, fuel, upkeep) or the
        /// whole construction cost needs; external requests count as empty (0). Lower = served first.
        /// </summary>
        internal static int Fill(WorldState s, Building c, int w)
        {
            var d = Def(s, c);
            int have = c.Stock[w] + c.Incoming[w];
            int per;
            if (c.State == BuildingState.Site) per = d.Cost[w];
            else if (c.Requested[w] > 0 && RecipeNeed(s, c, w) == 0) return 0;
            else per = d.Inputs[w];
            return per <= 0 ? 0 : have * 100 / per;
        }

        internal static void SetJobReason(DeliveryJob j, string reason, EventBuffer ev, long tick = -1)
        {
            bool route = reason == Reasons.NoRoad || reason == Reasons.DemonOnRoute;
            if (!route) j.RouteStalledSince = -1;
            else if (j.RouteStalledSince < 0) j.RouteStalledSince = tick;
            if (j.Reason == reason) return;
            bool wasStalled = j.Reason != null;
            j.Reason = reason;
            if (reason != null) ev?.Add(new JobStalled { JobId = j.Id, Reason = reason });
            else if (wasStalled) ev?.Add(new JobResumed { JobId = j.Id });
        }

        /// <summary>Drop a job that has not been picked up: free the reservation and the incoming count.</summary>
        internal static void ReleaseJob(WorldState s, DeliveryJob j, EventBuffer ev)
        {
            var e = s.Economy;
            int w = (int)j.Ware;
            e.Buildings[j.SourceId].Reserved[w]--;
            var dst = e.Buildings[j.DestId];
            dst.Incoming[w]--;
            if (j.FromRequest && dst.State == BuildingState.Active) dst.Requested[w]++;
            if (j.CarrierId >= 0)
            {
                var c = e.Carriers[j.CarrierId];
                c.JobId = -1;
                c.State = CarrierState.Idle;
            }
            e.Jobs.Remove(j);
            ev?.Add(new JobReleased { JobId = j.Id, Reason = j.Reason });
        }

        // ---------------- Site cancel ----------------

        internal static void CancelSite(WorldState s, Building site, EventBuffer ev)
        {
            var e = s.Economy;
            var store = Storehouse(s);
            for (int i = e.Jobs.Count - 1; i >= 0; i--)
            {
                var j = e.Jobs[i];
                if (j.DestId != site.Id) continue;
                int w = (int)j.Ware;
                site.Incoming[w]--;
                if (j.State == JobState.Carrying)
                {
                    j.DestId = store.Id;
                    store.Incoming[w]++;
                }
                else
                {
                    var src = e.Buildings[j.SourceId];
                    src.Reserved[w]--;
                    if (j.CarrierId >= 0)
                    {
                        var c = e.Carriers[j.CarrierId];
                        c.JobId = -1;
                        c.State = CarrierState.Idle;
                    }
                    e.Jobs.RemoveAt(i);
                }
            }
            for (int w = 0; w < Wares.Count; w++)
            {
                store.Stock[w] += site.Stock[w];
                site.Stock[w] = 0;
                site.Requested[w] = 0;
            }
            site.State = BuildingState.Removed;
            site.Reason = null;
            site.ReasonWare = -1;
            ev?.Add(new SiteCancelled { BuildingId = site.Id, Type = site.Type, Vertex = site.Vertex });
            ev?.Add(new BuildingStateChanged { BuildingId = site.Id, State = site.State, Enabled = site.Enabled });
        }

        // ---------------- Rounds and batches ----------------

        /// <summary>Hexes touching one of the player's cities, ascending, no duplicates.</summary>
        internal static List<int> CityHexes(WorldState s, int player)
        {
            var topo = s.Board.Topology;
            var seen = new bool[topo.HexCount];
            foreach (var c in s.Economy.Cities)
                if (c.Owner == player)
                    foreach (int h in topo.VertexHexes(c.Vertex)) seen[h] = true;
            var list = new List<int>();
            for (int h = 0; h < seen.Length; h++) if (seen[h]) list.Add(h);
            return list;
        }

        internal static bool HasActiveForester(WorldState s, int player, int hex)
        {
            var topo = s.Board.Topology;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State != BuildingState.Active || b.Owner != player || Def(s, b).Kind != BuildingKind.Forester) continue;
                foreach (int h in topo.VertexHexes(b.Vertex)) if (h == hex) return true;
            }
            return false;
        }

        internal static bool AuthoriseBatch(WorldState s, int hex, int amount, EventBuffer ev)
        {
            var e = s.Economy;
            if (hex < 0 || hex >= e.HexBatchRound.Length || amount <= 0) return false;
            if (e.HexBatchRound[hex] == e.Round) return false;
            var terrain = s.Board.HexTerrain[hex];
            var raw = TerrainInfo.RawWare(terrain);
            if (raw == null) return false;
            if (terrain == Terrain.Forest && !HasActiveForester(s, PlayerIds.Human, hex)) amount = (amount + 1) / 2;
            e.HexBatchRound[hex] = e.Round;
            int before = e.HexBatchRemaining[hex];
            e.HexBatchRemaining[hex] = Math.Min(e.Tuning.BatchCap, before + amount);
            e.Ledger.BatchAuthorised[(int)raw.Value] += amount;
            e.Ledger.BatchLost[(int)raw.Value] += before + amount - e.HexBatchRemaining[hex];
            ev?.Add(new BatchAuthorised { Hex = hex, Ware = raw.Value, Amount = amount });
            return true;
        }

        /// <summary>
        /// Production numbers for a round. Prologue starter sequence: while the commission is not complete (and for at
        /// most Tuning.StarterRounds rounds) the draw is two numbers: the home forest's, plus in turn the home hills'
        /// (odd rounds) and the home fields' (even rounds). Afterwards NumbersDrawn distinct random numbers from 1..4.
        /// </summary>
        internal static int[] DrawNumbers(WorldState s)
        {
            var e = s.Economy;
            var rng = e.RoundRng;
            int forestN = 0, hillsN = 0, fieldsN = 0;
            foreach (int h in s.Board.Topology.VertexHexes(s.Board.HomeCityVertex))
            {
                switch (s.Board.HexTerrain[h])
                {
                    case Terrain.Forest: forestN = s.Board.HexNumber[h]; break;
                    case Terrain.Hills: hillsN = s.Board.HexNumber[h]; break;
                    case Terrain.Fields: fieldsN = s.Board.HexNumber[h]; break;
                }
            }
            int[] nums = null;
            bool starter = forestN > 0 && hillsN > 0 && fieldsN > 0 && e.Tuning.NumbersDrawn >= 2
                           && !e.Commission.Complete && e.Round <= e.Tuning.StarterRounds;
            if (starter) nums = new[] { forestN, e.Round % 2 == 1 ? hillsN : fieldsN };
            if (nums == null)
            {
                var pool = new List<int> { 1, 2, 3, 4 };
                rng.Shuffle(pool);
                int k = Math.Max(1, Math.Min(4, e.Tuning.NumbersDrawn));
                nums = pool.GetRange(0, k).ToArray();
            }
            Array.Sort(nums);
            return nums;
        }

        /// <summary>Ticks until the bell may ring (0 = now): Tuning.MinRoundTicks after the round started.</summary>
        internal static long BellTicksLeft(WorldState s)
        {
            var e = s.Economy;
            if (e.RoundStartPending) return 0;
            return System.Math.Max(0L, e.RoundStartedTick + e.Tuning.MinRoundTicks - s.Tick);
        }

        internal static void StartRound(WorldState s, EconomyHooks hooks, EventBuffer ev)
        {
            var e = s.Economy;
            e.RoundStartPending = false;
            e.RoundStartedTick = s.Tick;
            var nums = DrawNumbers(s);
            e.LastDraw = nums;
            ev?.Add(new RoundStarted { Round = e.Round, Numbers = (int[])nums.Clone() });
            foreach (int h in CityHexes(s, PlayerIds.Human))
            {
                if (Array.IndexOf(nums, s.Board.HexNumber[h]) < 0) continue;
                if (hooks != null && hooks.HexStopped(s, h)) continue; // the robber: a demon on every city of this hex
                var raw = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                if (raw == null) continue;
                int amount = e.Tuning.BatchSizeFor(s.Board.HexTerrain[h]);
                if (hooks != null) foreach (var m in hooks.BatchModifiers) amount = m(s, h, raw.Value, amount);
                AuthoriseBatch(s, h, amount, ev);
            }
            if (hooks != null) foreach (var a in hooks.OnRoundStarted) a(s, ev);
        }

        /// <summary>True when a touching hex of the harvester's terrain has batch units left and is not stopped by the robber.</summary>
        internal static bool HasHarvestableHex(WorldState s, EconomyHooks hooks, Building b, BuildingDef def)
        {
            foreach (int h in s.Board.Topology.VertexHexes(b.Vertex))
            {
                if (s.Board.HexTerrain[h] != def.Terrain || s.Economy.HexBatchRemaining[h] <= 0) continue;
                if (hooks != null && hooks.HexStopped(s, h)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Set Building.Blocked for every live building from the blocked-flag hooks (demons, raids), and EconomyState.HexStopped (robber).</summary>
        internal static void RefreshBlocked(WorldState s, EconomyHooks hooks)
        {
            foreach (var b in s.Economy.Buildings)
                b.Blocked = b.State != BuildingState.Removed && hooks != null && hooks.Blocks(s, b.Vertex);
            var e = s.Economy;
            if (e.HexStopped == null || e.HexStopped.Length != e.HexBatchRemaining.Length) e.HexStopped = new bool[e.HexBatchRemaining.Length];
            for (int h = 0; h < e.HexStopped.Length; h++) e.HexStopped[h] = hooks != null && hooks.HexStopped(s, h);
        }

        // ---------------- Diagnostics ----------------

        internal static void ComputeReason(WorldState s, EconomyHooks hooks, bool[] blocked, Building b, out string reason, out int ware)
        {
            reason = null;
            ware = -1;
            var def = Def(s, b);
            var store = Storehouse(s);
            if (b.State == BuildingState.Removed || def.Kind == BuildingKind.Store) return;

            if (b.State == BuildingState.Site)
            {
                reason = RouteReason(s, blocked, store.Vertex, b.Vertex);
                if (reason != null) return;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (b.Stock[w] < def.Cost[w])
                    {
                        reason = Reasons.WaitingWare;
                        ware = w;
                        return;
                    }
                }
                return;
            }
            if (b.State == BuildingState.Constructing)
            {
                if (b.Blocked) reason = Reasons.DemonOnRoute;
                return;
            }

            if (!b.Enabled)
            {
                reason = Reasons.Disabled;
                return;
            }
            if (b.Blocked)
            {
                // A demon (or a raid) on the building's own flag stops it.
                reason = Reasons.DemonOnRoute;
                return;
            }
            bool powered = hooks == null || hooks.Powered(s, b);
            if (b.WorkProgress > 0)
            {
                if (!powered) reason = Reasons.NoPower;
                return;
            }
            if (def.Kind == BuildingKind.PowerPlant && b.FuelLeft > 0) return;

            reason = RouteReason(s, blocked, store.Vertex, b.Vertex);
            if (reason != null) return;

            if (def.Kind == BuildingKind.Forester) return;
            if (def.NeedsPower && !powered)
            {
                reason = Reasons.NoPower;
                return;
            }
            if (def.Kind == BuildingKind.Plain)
            {
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (b.Requested[w] > 0 || b.Incoming[w] > 0)
                    {
                        reason = Reasons.WaitingWare;
                        ware = w;
                        return;
                    }
                }
                return;
            }
            for (int w = 0; w < Wares.Count; w++)
            {
                if (def.Outputs[w] > 0 && b.Stock[w] >= def.Capacity)
                {
                    reason = Reasons.OutputFull;
                    ware = w;
                    return;
                }
                if (def.Kind == BuildingKind.Producer && def.Outputs[w] > 0 && s.Economy.Tuning.AtTarget(s.Economy, w))
                {
                    // Economy target reached: the storehouse holds enough of this output.
                    reason = Reasons.OutputFull;
                    ware = w;
                    return;
                }
            }
            if (def.Kind == BuildingKind.Harvester)
            {
                if (!HasHarvestableHex(s, hooks, b, def))
                {
                    reason = Reasons.NoBatch;
                    return;
                }
                if (b.InputCredit > 0) return;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (def.Inputs[w] > 0 && b.Stock[w] - b.Reserved[w] < def.Inputs[w])
                    {
                        reason = b.Incoming[w] > 0 ? Reasons.WaitingWare : Reasons.InputMissing;
                        ware = w;
                        return;
                    }
                }
                return;
            }
            for (int w = 0; w < Wares.Count; w++)
            {
                if (def.Inputs[w] > 0 && b.Stock[w] < def.Inputs[w])
                {
                    reason = b.Incoming[w] > 0 ? Reasons.WaitingWare : Reasons.InputMissing;
                    ware = w;
                    return;
                }
            }
        }

        internal static void UpdateDiagnostics(WorldState s, EconomyHooks hooks, bool[] blocked, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                if (b.State == BuildingState.Removed) continue;
                ComputeReason(s, hooks, blocked, b, out var reason, out var ware);
                if (reason == b.Reason && ware == b.ReasonWare) continue;
                b.Reason = reason;
                b.ReasonWare = ware;
                ev?.Add(new BuildingStateChanged
                {
                    BuildingId = b.Id, State = b.State, Enabled = b.Enabled, Reason = reason, ReasonWare = ware,
                });
            }

            if (!e.Commission.Complete && CommissionMet(s))
            {
                e.Commission.Complete = true;
                e.Commission.CompletedTick = s.Tick;
                ev?.Add(new CommissionCompleted());
            }
        }

        /// <summary>Buildings the quartermaster's commission needs active and road-connected.</summary>
        internal static readonly BuildingType[] CommissionTypes =
        {
            BuildingType.Forester, BuildingType.Woodcutter, BuildingType.Sawmill,
            BuildingType.ClayPit, BuildingType.CharcoalKiln, BuildingType.BrickKiln,
        };

        internal static bool CommissionBuildingsReady(WorldState s, BuildingType t)
        {
            var store = Storehouse(s);
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Type != t || b.State != BuildingState.Active || b.Owner != PlayerIds.Human) continue;
                if (Route(s, null, store.Vertex, b.Vertex) != null) return true;
            }
            return false;
        }

        internal static bool CommissionMet(WorldState s)
        {
            var store = Storehouse(s);
            var t = s.Economy.Tuning;
            if (store.Stock[(int)Ware.Planks] < t.CommissionPlanks || store.Stock[(int)Ware.Bricks] < t.CommissionBricks) return false;
            foreach (var bt in CommissionTypes) if (!CommissionBuildingsReady(s, bt)) return false;
            return true;
        }
    }

    /// <summary>Command refusal codes of the economy (stall reasons stay in <see cref="Reasons"/>).</summary>
    public static class EconomyRefusals
    {
        public const string NotOwner = "not_owner";
        public const string BadPlace = "bad_place";
        public const string UnknownType = "unknown_type";
        public const string NotConnected = "not_connected";
        public const string RoadExists = "road_exists";
        public const string NoRoadHere = "no_road_here";
        public const string NotEnoughWares = "not_enough_wares";
        public const string UnknownBuilding = "unknown_building";
        public const string NotASite = "not_a_site";
        public const string NotToggleable = "not_toggleable";
        public const string RoundStarting = "round_starting";
        /// <summary>EndRound before the round's harvest season (Tuning.MinRoundTicks) is over.</summary>
        public const string RoundTooSoon = "round_too_soon";
    }
}
