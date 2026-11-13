namespace World.Sim.Economy
{
    /// <summary>
    /// Entry point of the Economy module (REFACTOR_SPEC A.4). InitState builds the starting economy; Register adds
    /// systems and command handlers (also on a sim built around a loaded state).
    /// </summary>
    public static class EconomyModule
    {
        public static void Register(WorldSim sim)
        {
            var hooks = EconomyApi.Hooks(sim);
            sim.AddSystem(SystemOrder.Dice, new DiceSystem(hooks));
            sim.AddSystem(SystemOrder.Growth, new GrowthSystem());
            sim.AddSystem(SystemOrder.Collect, new CollectSystem(hooks));
            sim.AddSystem(SystemOrder.Production, new ProductionSystem());
            sim.AddSystem(SystemOrder.Construction, new ConstructionSystem(hooks));
            sim.AddSystem(SystemOrder.Jobs, new JobSystem(hooks));
            sim.AddSystem(SystemOrder.Carriers, new CarrierSystem(hooks));
            sim.RegisterHandler(new PlaceBuildingHandler());
            sim.RegisterHandler(new PlaceRoadHandler());
            sim.RegisterHandler(new FoundSettlementHandler());
            sim.RegisterHandler(new UpgradeCityHandler());
            sim.RegisterHandler(new CancelSiteHandler());
        }

        /// <summary>
        /// Starting economy: dice deck and first roll tick, hex stock (StartMature mature units on every hex next to a
        /// starting settlement), one active settlement per people on Board.StartCorners with StartStock, StartCarriers
        /// carriers and a primitive collector on slot 0 of each resource sector.
        /// </summary>
        public static void InitState(WorldState s)
        {
            var topo = s.Board.Topology;
            var e = new EconomyState();
            s.Economy = e;
            var tu = e.Tuning;

            e.Dice.Rng = s.Rng.Fork("economy.dice");
            e.Dice.Deck = DiceSystem.NewDeck(e.Dice.Rng);
            e.Dice.NextRollTick = tu.FirstRollTick;

            e.Hexes = new HexStock[topo.HexCount];
            for (int h = 0; h < topo.HexCount; h++) e.Hexes[h] = new HexStock { Hex = h };
            e.RoadOnEdge = new int[topo.HexEdgeCount];
            for (int i = 0; i < e.RoadOnEdge.Length; i++) e.RoadOnEdge[i] = -1;

            var starts = s.Board.StartCorners ?? new int[0];
            var stocked = new bool[topo.HexCount];
            for (int f = 0; f < starts.Length && f < PlayerIds.Count; f++)
            {
                int corner = starts[f];
                if (!PlotLayout.IsSettlementCorner(topo, corner)) continue;
                var st = EconomyLogic.AddSettlement(s, f, corner, SettlementState.Active);
                for (int w = 0; w < Wares.Count; w++) st.Stock[w] = tu.StartStock[w];
                EconomyLogic.FurnishSettlement(s, st, tu.StartCarriers);
                foreach (int h in topo.VertexHexes(corner))
                {
                    var ware = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                    if (ware == null || stocked[h]) continue;
                    stocked[h] = true;
                    e.Hexes[h].Mature = tu.StartMature;
                    e.Ledger.StockAdded[(int)ware.Value] += tu.StartMature;
                }
            }

            e.Ledger.Starting = EconomyLogic.WaresInWorld(s);
        }
    }
}
