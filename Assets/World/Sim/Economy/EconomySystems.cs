namespace World.Sim.Economy
{
    /// <summary>Rounds slot: performs the pending round start (draw, batches, OnRoundStarted hooks).</summary>
    internal sealed class RoundSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public RoundSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            if (s.Economy.RoundStartPending) EconomyLogic.StartRound(s, _hooks, ev);
        }
    }

    /// <summary>Harvest slot: harvesters work one cycle per batch unit on a touching hex of their terrain.</summary>
    internal sealed class HarvestSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            var topo = s.Board.Topology;
            foreach (var b in e.Buildings)
            {
                if (b.State != BuildingState.Active || !b.Enabled) continue;
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
                        b.Stock[w]++;
                        e.Ledger.Harvested[w]++;
                        ev.Add(new HarvestCycle { BuildingId = b.Id, Hex = b.WorkHex, Ware = raw.Value });
                        b.WorkProgress = 0;
                        b.WorkHex = -1;
                    }
                    continue;
                }
                if (b.Stock[w] >= def.Capacity) continue;
                foreach (int h in topo.VertexHexes(b.Vertex))
                {
                    if (s.Board.HexTerrain[h] != def.Terrain || e.HexBatchRemaining[h] <= 0) continue;
                    e.HexBatchRemaining[h]--;
                    b.WorkHex = h;
                    b.WorkProgress = 1;
                    break;
                }
            }
        }
    }

    /// <summary>Production slot: recipe buildings consume inputs once at cycle start, yield outputs at the end.</summary>
    internal sealed class ProductionSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public ProductionSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                if (b.State != BuildingState.Active || !b.Enabled) continue;
                var def = EconomyLogic.Def(s, b);
                if (def.Kind != BuildingKind.Producer) continue;
                bool powered = _hooks.Powered(s, b);
                if (b.WorkProgress > 0)
                {
                    if (!powered) continue;
                    b.WorkProgress++;
                    if (b.WorkProgress >= def.WorkTicks)
                    {
                        for (int w = 0; w < Wares.Count; w++)
                        {
                            if (def.Outputs[w] <= 0) continue;
                            b.Stock[w] += def.Outputs[w];
                            e.Ledger.Produced[w] += def.Outputs[w];
                            ev.Add(new ProductionCycle { BuildingId = b.Id, Output = (Ware)w, Amount = def.Outputs[w] });
                        }
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
                }
                b.WorkProgress = 1;
            }
        }
    }

    /// <summary>Construction slot: complete sites start building; finished builds become Active.</summary>
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
                    b.BuildProgress++;
                    if (b.BuildProgress < def.BuildTicks) continue;
                    for (int w = 0; w < Wares.Count; w++)
                    {
                        b.Stock[w] -= def.Cost[w];
                        e.Ledger.Consumed[w] += def.Cost[w];
                    }
                    b.State = BuildingState.Active;
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

    /// <summary>Power slot: power plants burn one fuel unit whenever their fuel time runs out.</summary>
    internal sealed class PowerSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Economy;
            foreach (var b in e.Buildings)
            {
                if (b.State != BuildingState.Active) continue;
                var def = EconomyLogic.Def(s, b);
                if (def.Kind != BuildingKind.PowerPlant) continue;
                if (b.FuelLeft > 0) b.FuelLeft--;
                if (b.FuelLeft > 0 || !b.Enabled) continue;
                bool ok = true;
                for (int w = 0; w < Wares.Count; w++) if (def.Inputs[w] > 0 && b.Stock[w] - b.Reserved[w] < def.Inputs[w]) ok = false;
                if (!ok) continue;
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (def.Inputs[w] <= 0) continue;
                    b.Stock[w] -= def.Inputs[w];
                    e.Ledger.Consumed[w] += def.Inputs[w];
                }
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
