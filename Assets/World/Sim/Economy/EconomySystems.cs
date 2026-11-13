using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>
    /// Dice (2.A): one roll every RollIntervalTicks from FirstRollTick, drawn from a shuffled deck of the 36 ordered
    /// outcomes. A roll of N (not 7) adds a batch of young stock to every resource hex numbered N that touches a
    /// finished settlement which is not demon-held, up to the hex cap (excess lost). Then EconomyHooks.OnRolled runs.
    /// </summary>
    internal sealed class DiceSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public DiceSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            var d = e.Dice;
            if (s.Tick < d.NextRollTick) return;
            int interval = e.Tuning.RollIntervalTicks > 0 ? e.Tuning.RollIntervalTicks : 1;
            d.NextRollTick = s.Tick + interval;
            Roll(s, ev, _hooks);
        }

        public static void Roll(WorldState s, EventBuffer ev, EconomyHooks hooks)
        {
            var e = s.Economy;
            var d = e.Dice;
            if (d.Deck == null || d.Deck.Length == 0) d.Deck = NewDeck(d.Rng);
            int card = d.Deck[d.Deck.Length - 1];
            var rest = new int[d.Deck.Length - 1];
            System.Array.Copy(d.Deck, rest, rest.Length);
            d.Deck = rest;
            d.LastD1 = card / 10;
            d.LastD2 = card % 10;
            d.LastRollTick = s.Tick;
            int sum = d.LastD1 + d.LastD2;
            ev.Add(new DiceRolled { D1 = d.LastD1, D2 = d.LastD2, Sum = sum, RollIndex = d.RollCount });
            d.RollCount++;
            if (sum != 7) AddStock(s, ev, sum, hooks);
            hooks?.Rolled(s, ev, d.LastD1, d.LastD2);
        }

        public static int[] NewDeck(Rng rng)
        {
            var list = new List<int>(36);
            for (int a = 1; a <= 6; a++)
                for (int b = 1; b <= 6; b++) list.Add(a * 10 + b);
            rng.Shuffle(list);
            return list.ToArray();
        }

        private static void AddStock(WorldState s, EventBuffer ev, int number, EconomyHooks hooks)
        {
            var e = s.Economy;
            var t = s.Board.Topology;
            for (int h = 0; h < t.HexCount; h++)
            {
                if (s.Board.HexNumber[h] != number) continue;
                var terrain = s.Board.HexTerrain[h];
                var ware = TerrainInfo.RawWare(terrain);
                if (ware == null) continue;
                if (!Feeds(s, h, hooks)) continue;
                var hs = e.Hexes[h];
                int total = hs.Mature;
                foreach (var c in hs.Young) total += c.Count;
                int add = System.Math.Max(0, System.Math.Min(e.Tuning.BatchSize, e.Tuning.HexCap - total));
                int lost = e.Tuning.BatchSize - add;
                e.Ledger.StockAdded[(int)ware.Value] += e.Tuning.BatchSize;
                if (add > 0)
                {
                    long mature = s.Tick + e.Tuning.GrowFor(terrain);
                    hs.Young.Add(new StockCohort { Ware = ware.Value, Count = add, AddedTick = s.Tick, MatureTick = mature });
                    ev.Add(new StockAdded { Hex = h, Ware = ware.Value, Count = add, MatureTick = mature });
                }
                if (lost > 0)
                {
                    e.Ledger.StockLost[(int)ware.Value] += lost;
                    ev.Add(new StockLost { Hex = h, Ware = ware.Value, Count = lost });
                }
            }
        }

        /// <summary>A hex receives stock when at least one adjacent finished settlement (any people) is not demon-held.</summary>
        private static bool Feeds(WorldState s, int hex, EconomyHooks hooks)
        {
            foreach (int c in s.Board.Topology.HexCorners(hex))
            {
                var st = EconomyLogic.SettlementAt(s, c);
                if (st == null || st.State != SettlementState.Active) continue;
                if (hooks != null ? !hooks.SettlementHalted(s, st.Id) : !EconomyLogic.SettlementHalted(s, st.Id)) return true;
            }
            return false;
        }
    }

    /// <summary>Growth (2.A): young cohorts become mature at their MatureTick.</summary>
    internal sealed class GrowthSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            foreach (var hs in s.Economy.Hexes)
            {
                if (hs.Young.Count == 0) continue;
                for (int i = 0; i < hs.Young.Count; i++)
                {
                    var c = hs.Young[i];
                    if (c.MatureTick > s.Tick) continue;
                    hs.Mature += c.Count;
                    ev.Add(new StockMatured { Hex = hs.Hex, Ware = c.Ware, Count = c.Count });
                    hs.Young.RemoveAt(i);
                    i--;
                }
            }
        }
    }

    /// <summary>
    /// Collect (2.A): each collector cycle takes 1 mature unit of its hex at the start (ascending building id: first
    /// come first served) and puts Yield wares in its store at the end. A full store waits (output_full); a demon-held
    /// settlement's collectors start no cycle; a retiring collector finishes its cycle, then retires.
    /// </summary>
    internal sealed class CollectSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public CollectSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            for (int i = 0; i < e.Buildings.Count; i++)
            {
                var b = e.Buildings[i];
                if (b.State != BuildingState.Active && b.State != BuildingState.Retiring) continue;
                var def = BuildingDefs.Get(b.Type);
                if (def == null || !def.IsCollector) continue;
                var ware = TerrainInfo.RawWare(def.Terrain).Value;

                if (b.WorkProgress > 0)
                {
                    b.WorkProgress++;
                    if (b.WorkProgress < def.WorkTicks) continue;
                    b.WorkProgress = 0;
                    b.Stock[(int)ware] += def.Yield;
                    e.Ledger.Collected[(int)ware] += def.Yield;
                    ev.Add(new Collected { BuildingId = b.Id, Hex = b.WorkHex, Ware = ware, Amount = def.Yield });
                    b.WorkHex = -1;
                }

                if (b.State == BuildingState.Retiring)
                {
                    if (TotalStock(b) > 0) continue; // its store is carried away first (Jobs)
                    b.State = BuildingState.Retired;
                    ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State });
                    ev.Add(new BuildingRetired { BuildingId = b.Id });
                    continue;
                }

                string reason = null;
                int rw = -1;
                var hs = b.Hex >= 0 && b.Hex < e.Hexes.Length ? e.Hexes[b.Hex] : null;
                if (_hooks.SettlementHalted(s, b.Settlement)) reason = Reasons.DemonOnRoute;
                else if (TotalStock(b) + def.Yield > e.Tuning.CollectorStore) { reason = Reasons.OutputFull; rw = (int)ware; }
                else if (hs == null || hs.Mature <= 0) { reason = Reasons.NoBatch; rw = (int)ware; }
                else
                {
                    hs.Mature--;
                    e.Ledger.StockTaken[(int)ware]++;
                    b.WorkProgress = 1;
                    b.WorkHex = b.Hex;
                    ev.Add(new CollectStarted { BuildingId = b.Id, Hex = b.Hex });
                    if (def.WorkTicks <= 1)
                    {
                        b.WorkProgress = 0;
                        b.Stock[(int)ware] += def.Yield;
                        e.Ledger.Collected[(int)ware] += def.Yield;
                        ev.Add(new Collected { BuildingId = b.Id, Hex = b.Hex, Ware = ware, Amount = def.Yield });
                        b.WorkHex = -1;
                    }
                }
                SetReason(b, reason, rw, ev);
            }
        }

        internal static int TotalStock(Building b)
        {
            int n = 0;
            foreach (int x in b.Stock) n += x;
            return n;
        }

        internal static void SetReason(Building b, string reason, int ware, EventBuffer ev)
        {
            if (b.Reason == reason && b.ReasonWare == ware) return;
            b.Reason = reason;
            b.ReasonWare = ware;
            ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State, Reason = reason, ReasonWare = ware });
        }
    }

    /// <summary>
    /// Production (2.B, 3.2): workshops, warrior lodges and warrior meals.
    /// S-0 FIRST VERSION: inputs are taken straight from the settlement's storehouse at the cycle start (no carrier
    /// trips, no InputBuffer); outputs go to the building's store and the interim Jobs system moves them home.
    /// </summary>
    internal sealed class ProductionSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            for (int i = 0; i < e.Buildings.Count; i++)
            {
                var b = e.Buildings[i];
                if (b.State != BuildingState.Active) continue;
                var def = BuildingDefs.Get(b.Type);
                if (def == null || (def.Class != BuildingClass.Workshop && def.Class != BuildingClass.Lodge)) continue;
                var st = EconomyLogic.GetSettlement(s, b.Settlement);
                if (st == null) continue;

                if (b.WorkProgress > 0)
                {
                    b.WorkProgress++;
                    if (b.WorkProgress < def.WorkTicks) continue;
                    b.WorkProgress = 0;
                    if (def.Class == BuildingClass.Lodge) TrainWarrior(s, b, ev);
                    else
                    {
                        for (int w = 0; w < Wares.Count; w++)
                        {
                            if (def.Outputs[w] <= 0) continue;
                            b.Stock[w] += def.Outputs[w];
                            e.Ledger.Produced[w] += def.Outputs[w];
                            e.Ledger.RecipeOut[(int)b.Type][w] += def.Outputs[w];
                            ev.Add(new ProductionCycle { BuildingId = b.Id, Ware = (Ware)w, Amount = def.Outputs[w] });
                        }
                    }
                }

                string reason = null;
                int rw = -1;
                if (def.Class == BuildingClass.Lodge && WarriorsAt(s, b.Id) >= e.Tuning.WarriorsPerLodge) reason = Reasons.OutputFull;
                else if (def.Class == BuildingClass.Workshop && OutputFull(e, st, b, def, out rw)) reason = Reasons.OutputFull;
                else if (!HasInputs(st, def, out rw)) reason = Reasons.InputMissing;
                else
                {
                    rw = -1;
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        int n = def.Inputs[w];
                        if (n <= 0) continue;
                        st.Stock[w] -= n;
                        e.Ledger.Consumed[w] += n;
                        if (def.Class == BuildingClass.Lodge) e.Ledger.Training[w] += n;
                        else e.Ledger.RecipeIn[(int)b.Type][w] += n;
                    }
                    b.WorkProgress = 1;
                    ev.Add(new ProductionStarted { BuildingId = b.Id });
                }
                CollectSystem.SetReason(b, reason, rw, ev);
            }
            Meals(s, ev);
        }

        private static bool OutputFull(EconomyState e, Settlement st, Building b, BuildingDef def, out int ware)
        {
            ware = -1;
            for (int w = 0; w < Wares.Count; w++)
            {
                if (def.Outputs[w] <= 0) continue;
                int target = e.Tuning.StockTarget != null && w < e.Tuning.StockTarget.Length ? e.Tuning.StockTarget[w] : 0;
                if (target > 0 && st.Stock[w] + b.Stock[w] >= target) { ware = w; return true; }
            }
            return false;
        }

        private static bool HasInputs(Settlement st, BuildingDef def, out int missing)
        {
            missing = -1;
            for (int w = 0; w < Wares.Count; w++)
                if (def.Inputs[w] > 0 && st.Stock[w] - st.Reserved[w] < def.Inputs[w]) { missing = w; return false; }
            return true;
        }

        private static int WarriorsAt(WorldState s, int lodge)
        {
            int n = 0;
            foreach (var w in s.Economy.Warriors) if (w.Lodge == lodge) n++;
            return n;
        }

        private static void TrainWarrior(WorldState s, Building lodge, EventBuffer ev)
        {
            var e = s.Economy;
            var w = new Warrior
            {
                Id = e.Warriors.Count, Faction = lodge.Faction, Lodge = lodge.Id, Settlement = lodge.Settlement,
                Fed = true, NextMealTick = s.Tick + e.Tuning.MealTicks,
            };
            e.Warriors.Add(w);
            ev.Add(new WarriorTrained { WarriorId = w.Id, Lodge = lodge.Id, Faction = lodge.Faction });
        }

        /// <summary>Each warrior eats 1 Bread (else 1 Meat) every MealTicks from its settlement's storehouse; a miss makes it hungry.</summary>
        private static void Meals(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var w in e.Warriors)
            {
                if (s.Tick < w.NextMealTick) continue;
                var st = EconomyLogic.GetSettlement(s, w.Settlement);
                int food = -1;
                if (st != null)
                {
                    if (st.Stock[(int)Ware.Bread] - st.Reserved[(int)Ware.Bread] > 0) food = (int)Ware.Bread;
                    else if (st.Stock[(int)Ware.Meat] - st.Reserved[(int)Ware.Meat] > 0) food = (int)Ware.Meat;
                }
                if (food >= 0)
                {
                    st.Stock[food]--;
                    e.Ledger.Consumed[food]++;
                    e.Ledger.Meals[food]++;
                    w.Fed = true;
                    w.NextMealTick = s.Tick + e.Tuning.MealTicks;
                    ev.Add(new WarriorFed { WarriorId = w.Id, Ware = (Ware)food });
                }
                else
                {
                    if (w.Fed) ev.Add(new WarriorHungry { WarriorId = w.Id });
                    w.Fed = false;
                    w.NextMealTick = s.Tick + e.Tuning.MealRetryTicks;
                }
            }
        }
    }

    /// <summary>
    /// Construction (3.3): a site whose goods have all arrived builds for its build ticks, then completes. A city
    /// upgrade moves what it can from its own Stock to UpgradeDelivered each tick (no carriers, as specified).
    /// </summary>
    internal sealed class ConstructionSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public ConstructionSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            var tu = e.Tuning;

            for (int i = 0; i < e.Buildings.Count; i++)
            {
                var b = e.Buildings[i];
                var def = BuildingDefs.Get(b.Type);
                if (def == null) continue;
                if (b.State == BuildingState.Site && Covered(b.Delivered, def.Cost))
                {
                    b.State = BuildingState.Constructing;
                    b.BuildProgress = 0;
                    b.Reason = null;
                    b.ReasonWare = -1;
                    ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State });
                }
                if (b.State != BuildingState.Constructing) continue;
                b.BuildProgress++;
                if (b.BuildProgress < def.BuildTicks) continue;
                CompleteBuilding(s, b, def, ev);
            }

            for (int i = 0; i < e.Roads.Count; i++)
            {
                var r = e.Roads[i];
                if (r.State == RoadState.Site && Covered(r.Delivered, tu.RoadCost) && !_hooks.EdgeBlocked(s, r.HexEdge))
                {
                    r.State = RoadState.Constructing;
                    r.BuildProgress = 0;
                    r.Reason = null;
                    r.ReasonWare = -1;
                }
                if (r.State != RoadState.Constructing) continue;
                if (_hooks.EdgeBlocked(s, r.HexEdge)) { r.Reason = Reasons.DemonOnRoute; continue; }
                r.Reason = null;
                r.BuildProgress++;
                if (r.BuildProgress < tu.RoadBuildTicks) continue;
                r.State = RoadState.Built;
                Consume(e, r.Delivered);
                ev.Add(new RoadBuilt { RoadId = r.Id, HexEdge = r.HexEdge, Faction = r.Faction });
                ev.Add(new SiteCompleted { Kind = SiteKind.Road, Id = r.Id, Faction = r.Faction });
                _hooks.SiteCompleted(s, ev, SiteKind.Road, r.Id);
            }

            for (int i = 0; i < e.Settlements.Count; i++)
            {
                var st = e.Settlements[i];
                if (st.State == SettlementState.Site && Covered(st.Delivered, tu.SettlementCost))
                {
                    st.BuildProgress++;
                    if (st.BuildProgress >= tu.SettlementBuildTicks)
                    {
                        st.State = SettlementState.Active;
                        st.FoundedTick = s.Tick;
                        Consume(e, st.Delivered);
                        EconomyLogic.FurnishSettlement(s, st, tu.NewSettlementCarriers);
                        ev.Add(new SettlementFounded { SettlementId = st.Id, Corner = st.Corner, Faction = st.Faction });
                        ev.Add(new SiteCompleted { Kind = SiteKind.Settlement, Id = st.Id, Faction = st.Faction });
                        _hooks.SiteCompleted(s, ev, SiteKind.Settlement, st.Id);
                    }
                }

                if (st.State != SettlementState.Active || st.Upgrade == UpgradeState.None) continue;
                if (st.Upgrade == UpgradeState.Site)
                {
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        int need = tu.CityCost[w] - st.UpgradeDelivered[w];
                        int take = System.Math.Min(need, st.Stock[w] - st.Reserved[w]);
                        if (take <= 0) continue;
                        st.Stock[w] -= take;
                        st.UpgradeDelivered[w] += take;
                    }
                    if (Covered(st.UpgradeDelivered, tu.CityCost)) { st.Upgrade = UpgradeState.Constructing; st.UpgradeProgress = 0; }
                }
                if (st.Upgrade != UpgradeState.Constructing) continue;
                st.UpgradeProgress++;
                if (st.UpgradeProgress < tu.CityBuildTicks) continue;
                st.Upgrade = UpgradeState.None;
                st.IsCity = true;
                Consume(e, st.UpgradeDelivered);
                EconomyLogic.AddCarriers(s, st, tu.CarriersPerCity);
                ev.Add(new CityUpgraded { SettlementId = st.Id, Corner = st.Corner, Faction = st.Faction });
                ev.Add(new SiteCompleted { Kind = SiteKind.City, Id = st.Id, Faction = st.Faction });
                _hooks.SiteCompleted(s, ev, SiteKind.City, st.Id);
            }
        }

        private void CompleteBuilding(WorldState s, Building b, BuildingDef def, EventBuffer ev)
        {
            var e = s.Economy;
            b.State = BuildingState.Active;
            b.BuildProgress = def.BuildTicks;
            Consume(e, b.Delivered);
            ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State });
            var st = EconomyLogic.GetSettlement(s, b.Settlement);
            if (st != null) EconomyLogic.AddCarriers(s, st, e.Tuning.CarriersPerBuilding);
            if (def.Class == BuildingClass.ImprovedCollector)
            {
                // The primitive collector of that hex in that settlement retires.
                foreach (var o in e.Buildings)
                {
                    if (o.Settlement != b.Settlement || o.Hex != b.Hex || o.State != BuildingState.Active) continue;
                    var od = BuildingDefs.Get(o.Type);
                    if (od == null || od.Class != BuildingClass.PrimitiveCollector) continue;
                    o.State = BuildingState.Retiring;
                    ev.Add(new BuildingStateChanged { BuildingId = o.Id, State = o.State });
                }
            }
            ev.Add(new SiteCompleted { Kind = SiteKind.Building, Id = b.Id, Faction = b.Faction });
            _hooks.SiteCompleted(s, ev, SiteKind.Building, b.Id);
        }

        internal static bool Covered(int[] have, int[] cost)
        {
            for (int w = 0; w < cost.Length && w < have.Length; w++) if (have[w] < cost[w]) return false;
            return true;
        }

        private static void Consume(EconomyState e, int[] delivered)
        {
            for (int w = 0; w < delivered.Length; w++)
            {
                if (delivered[w] == 0) continue;
                e.Ledger.Consumed[w] += delivered[w];
                e.Ledger.Construction[w] += delivered[w];
                delivered[w] = 0;
            }
        }
    }

    /// <summary>
    /// Jobs and logistics. S-0 FIRST VERSION (no carrier trips): every tick, building sites take their missing goods
    /// straight from their settlement's storehouse, road and settlement sites from their source settlement's
    /// storehouse, and collector and workshop stores are emptied into their storehouse. The full hub-and-spoke
    /// version with jobs, carriers and shipments (2.B.1) replaces this.
    /// </summary>
    internal sealed class JobSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public JobSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                var st = EconomyLogic.GetSettlement(s, b.Settlement);
                if (st == null) continue;
                var def = BuildingDefs.Get(b.Type);
                if (b.State == BuildingState.Site && def != null) Pull(st, b.Delivered, def.Cost, b);
                if (b.State == BuildingState.Active || b.State == BuildingState.Retiring || b.State == BuildingState.Constructing)
                {
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        if (b.Stock[w] <= 0) continue;
                        st.Stock[w] += b.Stock[w];
                        b.Stock[w] = 0;
                    }
                }
            }
            foreach (var r in e.Roads)
            {
                if (r.State != RoadState.Site) continue;
                var src = EconomyLogic.GetSettlement(s, r.SourceSettlement);
                if (src == null || _hooks.SettlementHalted(s, src.Id)) { r.Reason = Reasons.NoRoad; continue; }
                PullRoad(src, r, e.Tuning.RoadCost);
            }
            foreach (var st in e.Settlements)
            {
                if (st.State != SettlementState.Site) continue;
                var src = EconomyLogic.GetSettlement(s, st.SourceSettlement);
                if (src == null || _hooks.SettlementHalted(s, src.Id)) { st.Reason = Reasons.NoRoad; continue; }
                for (int w = 0; w < Wares.Count; w++)
                {
                    int take = System.Math.Min(e.Tuning.SettlementCost[w] - st.Delivered[w], src.Stock[w] - src.Reserved[w]);
                    if (take <= 0) continue;
                    src.Stock[w] -= take;
                    st.Delivered[w] += take;
                }
                st.Reason = ConstructionSystem.Covered(st.Delivered, e.Tuning.SettlementCost) ? null : Reasons.WaitingWare;
            }
        }

        private static void Pull(Settlement from, int[] delivered, int[] cost, Building b)
        {
            int missing = -1;
            for (int w = 0; w < Wares.Count; w++)
            {
                int take = System.Math.Min(cost[w] - delivered[w], from.Stock[w] - from.Reserved[w]);
                if (take > 0)
                {
                    from.Stock[w] -= take;
                    delivered[w] += take;
                }
                if (delivered[w] < cost[w] && missing < 0) missing = w;
            }
            b.Reason = missing >= 0 ? Reasons.WaitingWare : null;
            b.ReasonWare = missing;
        }

        private static void PullRoad(Settlement from, Road r, int[] cost)
        {
            int missing = -1;
            for (int w = 0; w < Wares.Count; w++)
            {
                int take = System.Math.Min(cost[w] - r.Delivered[w], from.Stock[w] - from.Reserved[w]);
                if (take > 0)
                {
                    from.Stock[w] -= take;
                    r.Delivered[w] += take;
                }
                if (r.Delivered[w] < cost[w] && missing < 0) missing = w;
            }
            r.Reason = missing >= 0 ? Reasons.WaitingWare : null;
            r.ReasonWare = missing;
        }
    }

    /// <summary>Carriers. S-0 STUB: carriers stay idle at their storehouse (the interim Jobs system moves goods directly).</summary>
    internal sealed class CarrierSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) { }
    }
}
