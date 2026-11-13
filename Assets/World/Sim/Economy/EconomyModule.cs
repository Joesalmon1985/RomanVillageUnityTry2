namespace World.Sim.Economy
{
    /// <summary>
    /// Entry point of the Economy module. Owned by the Economy agent; Core never changes for it.
    /// InitState adds this module's starting data to WorldState (fields declared in a
    /// partial WorldState file in this folder). Register adds systems (with SystemOrder slots)
    /// and command handlers to the sim. Called by WorldSimFactory.CreateNew.
    /// </summary>
    public static class EconomyModule
    {
        /// <summary>Adds systems and handlers. Also call it on a sim built around a loaded WorldState.</summary>
        public static void Register(WorldSim sim)
        {
            var hooks = EconomyApi.Hooks(sim);
            sim.AddSystem(SystemOrder.Rounds, new RoundSystem(hooks));
            sim.AddSystem(SystemOrder.Harvest, new HarvestSystem(hooks));
            sim.AddSystem(SystemOrder.Production, new ProductionSystem(hooks));
            sim.AddSystem(SystemOrder.Construction, new ConstructionSystem());
            sim.AddSystem(SystemOrder.Jobs, new JobSystem(hooks));
            sim.AddSystem(SystemOrder.Carriers, new CarrierSystem(hooks));
            sim.AddSystem(SystemOrder.Power, new PowerSystem(hooks));
            sim.AddSystem(SystemOrder.Diagnostics, new DiagnosticsSystem(hooks));
            sim.RegisterHandler(new PlaceRoadHandler());
            sim.RegisterHandler(new RemoveRoadHandler());
            sim.RegisterHandler(new PlaceBuildingHandler());
            sim.RegisterHandler(new CancelSiteHandler());
            sim.RegisterHandler(new SetPriorityHandler());
            sim.RegisterHandler(new ToggleBuildingHandler());
            sim.RegisterHandler(new EndRoundHandler(hooks));
        }

        /// <summary>
        /// Starting economy: home city and storehouse (starter stock), carriers at the storehouse, a
        /// 2-segment demonstration road from the storehouse along a home hex edge bordering the forest, a Forester site at
        /// its end, and that site's first Planks already on a carrier walking the first edge at tick 0.
        /// </summary>
        public static void InitState(WorldState s)
        {
            var topo = s.Board.Topology;
            var e = new EconomyState();
            s.Economy = e;
            e.RoundRng = s.Rng.Fork("economy.rounds");
            e.HexBatchRound = new int[topo.HexCount];
            e.HexBatchRemaining = new int[topo.HexCount];
            e.HexStopped = new bool[topo.HexCount];
            e.Roads = new bool[topo.EdgeCount];

            int home = s.Board.HomeCityVertex;
            e.Cities.Add(new City { Id = 0, Owner = PlayerIds.Human, Vertex = home });

            var store = EconomyLogic.AddBuilding(s, BuildingType.Storehouse, PlayerIds.Human, s.Board.StorehouseVertex, BuildingState.Active);
            e.StorehouseId = store.Id;
            for (int w = 0; w < Wares.Count; w++) store.Stock[w] = e.Tuning.StarterStock[w];

            for (int i = 0; i < e.Tuning.Carriers; i++)
                e.Carriers.Add(new Carrier { Id = i, State = CarrierState.Idle, AtVertex = store.Vertex });

            // Demonstration road: storehouse -> A -> B, two segments along a home hex edge that borders the
            // home forest (lowest hex edge id first, avoiding the warehouse flag). B touches the forest.
            int a = -1, b = -1;
            if (topo.IsCorner(store.Vertex))
            {
                foreach (int he in topo.CornerHexEdges(store.Vertex))
                {
                    var flags = topo.HexEdgeFlags(he);
                    bool fromA = flags[0] == store.Vertex;
                    int fa = fromA ? flags[1] : flags[2], fb = fromA ? flags[2] : flags[1];
                    if (fa == s.Board.WarehouseVertex || fb == s.Board.WarehouseVertex) continue;
                    bool forest = false;
                    foreach (int h in topo.VertexHexes(fb)) if (s.Board.HexTerrain[h] == Terrain.Forest) forest = true;
                    if (!forest) continue;
                    a = fa;
                    b = fb;
                    break;
                }
            }
            if (a >= 0)
            {
                e.DemoVertexA = a;
                e.DemoVertexB = b;
                e.Roads[topo.EdgeBetween(store.Vertex, a)] = true;
                e.Roads[topo.EdgeBetween(a, b)] = true;

                var site = EconomyLogic.AddBuilding(s, BuildingType.Forester, PlayerIds.Human, b, BuildingState.Site);
                if (e.Tuning.Def(BuildingType.Forester).Cost[(int)Ware.Planks] > 0 && store.Stock[(int)Ware.Planks] > 0 && e.Carriers.Count > 0)
                {
                    var job = EconomyLogic.CreateJob(s, null, Ware.Planks, store, site);
                    var c = e.Carriers[0];
                    store.Stock[(int)Ware.Planks]--;
                    store.Reserved[(int)Ware.Planks]--;
                    job.State = JobState.Carrying;
                    job.CarrierId = c.Id;
                    c.JobId = job.Id;
                    c.State = CarrierState.Carrying;
                    c.HasLoad = true;
                    c.Load = Ware.Planks;
                    c.FromVertex = store.Vertex;
                    c.ToVertex = a;
                    c.CurrentEdge = topo.EdgeBetween(store.Vertex, a);
                    c.Progress = 0;
                }
            }

            e.Ledger.Starting = EconomyApi.WaresInWorld(s);
            EconomyLogic.UpdateDiagnostics(s, null, null, null);
        }
    }
}
