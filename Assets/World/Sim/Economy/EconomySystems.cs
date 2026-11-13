namespace World.Sim.Economy
{
    /// <summary>
    /// Rounds slot (first economy system): refreshes which buildings stand on a blocked flag (demons, raids),
    /// then performs the pending round start (draw, batches, OnRoundStarted hooks).
    /// </summary>
    internal sealed class RoundSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public RoundSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            EconomyLogic.RefreshBlocked(s, _hooks);
            if (s.Economy.RoundStartPending) EconomyLogic.StartRound(s, _hooks, ev);
        }
    }

    /// <summary>
    /// Harvest slot: harvesters work one cycle per batch unit on a touching hex of their terrain. A harvester
    /// with an upkeep (the Mine) pays Inputs once every CyclesPerInput cycles before it may start a cycle.
    /// Blocked buildings and hexes stopped by the robber do not work.
    /// </summary>
    internal sealed class HarvestSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public HarvestSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            var topo = s.Board.Topology;
            foreach (var b in e.Buildings)
            {
                if (b.State != BuildingState.Active || !b.Enabled || b.Blocked) continue;
                var def = EconomyLogic.Def(s, b);
                if (def.Kind != BuildingKind.Harvester) continue;
                var raw = TerrainInfo.RawWare(def.Terrain);
                if (raw == null) continue;
                int w = (int)raw.Value;
                if (b.WorkProgress > 0)
                {
                    b.WorkProgress++;
                    if (b.WorkProgress >= e.Tuning.HarvestTicks)
                    {
                        int n = System.Math.Max(1, def.Outputs[w]); // raw units per batch unit (Woodcutter 2)
                        b.Stock[w] += n;
                        e.Ledger.Harvested[w] += n;
                        e.Ledger.RecipeOut[(int)b.Type][w] += n;
                        e.Ledger.Cycles[(int)b.Type]++;
                        ev.Add(new HarvestCycle { BuildingId = b.Id, Hex = b.WorkHex, Ware = raw.Value, Amount = n });
                        b.WorkProgress = 0;
                        b.WorkHex = -1;
                    }
                    continue;
                }
                if (b.Stock[w] >= def.Capacity) continue;
                int hex = -1;
                foreach (int h in topo.VertexHexes(b.Vertex))
                {
                    if (s.Board.HexTerrain[h] != def.Terrain || e.HexBatchRemaining[h] <= 0) continue;
                    if (_hooks != null && _hooks.HexStopped(s, h)) continue;
                    hex = h;
                    break;
                }
                if (hex < 0) continue;
                if (!PayUpkeep(s, b, def, ev)) continue;
                e.HexBatchRemaining[hex]--;
                b.WorkHex = hex;
                b.WorkProgress = 1;
            }
        }

        /// <summary>Uses one paid cycle, paying Inputs first when none is left. False when the inputs are missing.</summary>
        private static bool PayUpkeep(WorldState s, Building b, BuildingDef def, EventBuffer ev)
        {
            bool any = false;
            for (int w = 0; w < Wares.Count; w++) if (def.Inputs[w] > 0) any = true;
            if (!any) return true;
            if (b.InputCredit <= 0)
            {
                for (int w = 0; w < Wares.Count; w++)
                    if (def.Inputs[w] > 0 && b.Stock[w] - b.Reserved[w] < def.Inputs[w]) return false;
                var l = s.Economy.Ledger;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (def.Inputs[w] <= 0) continue;
                    b.Stock[w] -= def.Inputs[w];
                    l.Consumed[w] += def.Inputs[w];
                    l.RecipeIn[(int)b.Type][w] += def.Inputs[w];
                    ev?.Add(new UpkeepPaid { BuildingId = b.Id, Ware = (Ware)w, Amount = def.Inputs[w], Cycles = System.Math.Max(1, def.CyclesPerInput) });
                }
                b.InputCredit = System.Math.Max(1, def.CyclesPerInput);
            }
            b.InputCredit--;
            return true;
        }
    }

    /// <summary>
    /// Production slot: recipe buildings consume inputs once at cycle start, yield outputs at the end (plus any
    /// card bonus on the main output). No progress without power or while the building's flag is blocked.
    /// </summary>
    internal sealed class ProductionSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public ProductionSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                if (b.State != BuildingState.Active || !b.Enabled || b.Blocked) continue;
                var def = EconomyLogic.Def(s, b);
                if (def.Kind != BuildingKind.Producer) continue;
                bool powered = _hooks.Powered(s, b);
                if (b.WorkProgress > 0)
                {
                    if (!powered) continue;
                    b.WorkProgress++;
                    if (b.WorkProgress >= def.WorkTicks)
                    {
                        int bonus = e.Bonus != null && e.Bonus.ExtraOutput != null && (int)b.Type < e.Bonus.ExtraOutput.Length ? e.Bonus.ExtraOutput[(int)b.Type] : 0;
                        bool first = true;
                        for (int w = 0; w < Wares.Count; w++)
                        {
                            if (def.Outputs[w] <= 0) continue;
                            int n = def.Outputs[w] + (first ? bonus : 0);
                            first = false;
                            b.Stock[w] += n;
                            e.Ledger.Produced[w] += n;
                            e.Ledger.RecipeOut[(int)b.Type][w] += n;
                            ev.Add(new ProductionCycle { BuildingId = b.Id, Output = (Ware)w, Amount = n });
                        }
                        e.Ledger.Cycles[(int)b.Type]++;
                        b.WorkProgress = 0;
                    }
                    continue;
                }
                if (!powered) continue;
                bool ok = true;
                for (int w = 0; w < Wares.Count && ok; w++)
                {
                    if (def.Inputs[w] > 0 && b.Stock[w] - b.Reserved[w] < def.Inputs[w]) ok = false;
                    if (def.Outputs[w] > 0 && b.Stock[w] >= def.Capacity) ok = false;
                }
                if (!ok) continue;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (def.Inputs[w] <= 0) continue;
                    b.Stock[w] -= def.Inputs[w];
                    e.Ledger.Consumed[w] += def.Inputs[w];
                    e.Ledger.RecipeIn[(int)b.Type][w] += def.Inputs[w];
                }
                b.WorkProgress = 1;
                ev.Add(new ProductionStarted { BuildingId = b.Id });
            }
        }
    }

    /// <summary>Construction slot: complete sites start building; finished builds become Active. Blocked sites wait.</summary>
    internal sealed class ConstructionSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                var def = EconomyLogic.Def(s, b);
                if (b.State == BuildingState.Site)
                {
                    bool all = true;
                    for (int w = 0; w < Wares.Count; w++) if (b.Stock[w] < def.Cost[w]) all = false;
                    if (!all) continue;
                    b.State = BuildingState.Constructing;
                    b.BuildProgress = 0;
                    b.Reason = null;
                    b.ReasonWare = -1;
                    ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State, Enabled = b.Enabled });
                }
                else if (b.State == BuildingState.Constructing)
                {
                    if (b.Blocked) continue;
                    b.BuildProgress++;
                    if (b.BuildProgress < def.BuildTicks) continue;
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        b.Stock[w] -= def.Cost[w];
                        e.Ledger.Consumed[w] += def.Cost[w];
                        e.Ledger.Construction[w] += def.Cost[w];
                    }
                    b.State = BuildingState.Active;
                    for (int i = 0; i < e.Tuning.CarriersPerBuilding; i++) EconomyApi.AddCarrier(s);
                    ev.Add(new SiteCompleted { BuildingId = b.Id, Type = b.Type, Vertex = b.Vertex });
                    ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State, Enabled = b.Enabled });
                }
            }
        }
    }

    /// <summary>Jobs slot: creates delivery jobs (reserving wares at the source).</summary>
    internal sealed class JobSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public JobSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev) =>
            EconomyLogic.CreateJobs(s, _hooks.ComputeBlocked(s), ev);
    }

    /// <summary>
    /// Power slot: power plants burn one fuel unit whenever their fuel time runs out and some building needs power
    /// (EconomyHooks.PowerDemanded); not while blocked.
    /// </summary>
    internal sealed class PowerSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public PowerSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            bool demanded = _hooks == null || _hooks.PowerDemanded == null || _hooks.PowerDemanded(s);
            foreach (var b in e.Buildings)
            {
                if (b.State != BuildingState.Active || b.Blocked) continue;
                var def = EconomyLogic.Def(s, b);
                if (def.Kind != BuildingKind.PowerPlant) continue;
                if (b.FuelLeft > 0) b.FuelLeft--;
                if (b.FuelLeft > 0 || !b.Enabled || !demanded) continue;
                bool ok = true;
                for (int w = 0; w < Wares.Count; w++) if (def.Inputs[w] > 0 && b.Stock[w] - b.Reserved[w] < def.Inputs[w]) ok = false;
                if (!ok) continue;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (def.Inputs[w] <= 0) continue;
                    b.Stock[w] -= def.Inputs[w];
                    e.Ledger.Consumed[w] += def.Inputs[w];
                    e.Ledger.RecipeIn[(int)b.Type][w] += def.Inputs[w];
                    ev.Add(new FuelBurned { BuildingId = b.Id, Ware = (Ware)w, Amount = def.Inputs[w] });
                }
                e.Ledger.Cycles[(int)b.Type]++;
                b.FuelLeft = def.FuelTicks;
            }
        }
    }

    /// <summary>Diagnostics slot: one reason per building, BuildingStateChanged on change, commission check.</summary>
    internal sealed class DiagnosticsSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public DiagnosticsSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev) =>
            EconomyLogic.UpdateDiagnostics(s, _hooks, _hooks.ComputeBlocked(s), ev);
    }
}
