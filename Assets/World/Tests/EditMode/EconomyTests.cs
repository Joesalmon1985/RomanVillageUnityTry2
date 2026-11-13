using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;

namespace World.Sim.Tests
{
    public class EconomyTests
    {
        private static WorldSim NewSim(ulong seed = EconomyTestKit.DefaultSeed, int ticksPerEdge = 5)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            sim.State.Economy.Tuning.TicksPerEdge = ticksPerEdge;
            return sim;
        }

        private static void StepUntil(WorldSim sim, System.Func<bool> done, int max, List<SimEvent> sink = null)
        {
            for (int i = 0; i < max && !done(); i++)
            {
                sim.Step();
                var evs = sim.DrainEvents();
                sink?.AddRange(evs);
            }
            Assert.IsTrue(done(), "condition not reached within " + max + " ticks");
        }

        // ---------------- Starter ----------------

        [Test]
        public void Starter_HasStorehouseDemoRoadSiteAndCarrierWalking()
        {
            var sim = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var s = sim.State;
            var e = s.Economy;
            Assert.AreEqual(1, e.Round);
            Assert.AreEqual(s.Board.StorehouseVertex, e.Buildings[e.StorehouseId].Vertex);
            Assert.AreEqual(2, e.Roads.Count(r => r), "demonstration road of 2 edges");
            var site = e.Buildings[1];
            Assert.AreEqual(BuildingType.Forester, site.Type);
            Assert.AreEqual(BuildingState.Site, site.State);
            Assert.AreEqual(e.DemoVertexB, site.Vertex);
            var c = e.Carriers[0];
            Assert.IsTrue(c.HasLoad);
            Assert.AreEqual(Ware.Planks, c.Load);
            Assert.GreaterOrEqual(c.CurrentEdge, 0, "already walking at tick 0");
            Assert.AreEqual(4, e.Carriers.Count);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
            sim.Step(10);
            Assert.AreEqual(10, s.Economy.Carriers[0].Progress, "visibly walking in the first second");
        }

        [Test]
        public void Starter_StockCoversTheFourBuildingsAndTheirRoads()
        {
            var t = new EconomyTuning();
            int planks = t.StarterStock[(int)Ware.Planks], stone = t.StarterStock[(int)Ware.Stone];
            foreach (var bt in new[] { BuildingType.Forester, BuildingType.Woodcutter, BuildingType.Sawmill, BuildingType.Quarry })
            {
                planks -= t.Def(bt).Cost[(int)Ware.Planks];
                stone -= t.Def(bt).Cost[(int)Ware.Stone];
            }
            planks -= 3 * t.RoadCost[(int)Ware.Planks];
            Assert.GreaterOrEqual(planks, 0);
            Assert.GreaterOrEqual(stone, 0);
            Assert.Less(planks, 10, "the commission needs real production");
        }

        // ---------------- Batches ----------------

        [Test]
        public void Batch_AuthorisedOncePerHexPerRound()
        {
            var sim = NewSim();
            var s = sim.State;
            sim.Step();
            var evs = sim.DrainEvents();
            var started = evs.OfType<RoundStarted>().Single();
            Assert.AreEqual(1, started.Round);
            Assert.AreEqual(2, started.Numbers.Length);
            int forest = EconomyTestKit.HomeHex(s, Terrain.Forest), hills = EconomyTestKit.HomeHex(s, Terrain.Hills);
            CollectionAssert.AreEquivalent(new[] { s.Board.HexNumber[forest], s.Board.HexNumber[hills] }, started.Numbers, "starter round 1");
            var batches = evs.OfType<BatchAuthorised>().ToList();
            Assert.AreEqual(batches.Count, batches.Select(b => b.Hex).Distinct().Count(), "one batch per hex");
            Assert.IsTrue(batches.Any(b => b.Hex == forest && b.Amount == 3), "forest halved without an active forester");
            Assert.IsTrue(batches.Any(b => b.Hex == hills && b.Amount == 6));

            int before = s.Economy.HexBatchRemaining[hills];
            var ev = new EventBuffer();
            Assert.IsFalse(EconomyApi.AuthoriseBatch(s, hills, 6, ev), "second authorisation in the same round");
            Assert.AreEqual(before, s.Economy.HexBatchRemaining[hills]);
            Assert.AreEqual(0, ev.Count);

            EconomyTestKit.Submit(sim, new EndRound());
            sim.Step();
            Assert.AreEqual(2, s.Economy.Round);
            Assert.AreEqual(1, sim.DrainEvents().OfType<RoundStarted>().Count());
            bool drawnAgain = s.Economy.HexBatchRound[hills] == 2;
            Assert.AreEqual(!drawnAgain, EconomyApi.AuthoriseBatch(s, hills, 1, ev), "a new round allows one more");
            Assert.IsFalse(EconomyApi.AuthoriseBatch(s, hills, 1, ev));
        }

        [Test]
        public void Batch_RoundDrawsAreSeededAndDistinct()
        {
            var a = NewSim(7);
            var b = NewSim(7);
            for (int r = 0; r < 12; r++)
            {
                a.Step();
                b.Step();
                CollectionAssert.AreEqual(a.State.Economy.LastDraw, b.State.Economy.LastDraw);
                Assert.AreEqual(2, a.State.Economy.LastDraw.Distinct().Count());
                Assert.IsTrue(a.State.Economy.LastDraw.All(n => n >= 1 && n <= 4));
                a.Submit(new EndRound());
                b.Submit(new EndRound());
            }
        }

        [Test]
        public void Batch_ForesterKeepsForestBatchWhole_AndModifierHookApplies()
        {
            var sim = NewSim();
            var s = sim.State;
            int forest = EconomyTestKit.HomeHex(s, Terrain.Forest);
            StepUntil(sim, () => s.Economy.Buildings[1].State == BuildingState.Active, 2000);
            EconomyApi.Hooks(sim).BatchModifiers.Add((st, hex, ware, amount) => ware == Ware.Timber ? amount + 2 : amount);
            // Round 2 or 3 of the starter sequence draws the home forest.
            var evs = new List<SimEvent>();
            for (int i = 0; i < 2; i++)
            {
                EconomyTestKit.Submit(sim, new EndRound());
                sim.Step();
                evs.AddRange(sim.DrainEvents());
            }
            var fb = evs.OfType<BatchAuthorised>().Single(b => b.Hex == forest);
            Assert.AreEqual(8, fb.Amount, "6 + 2 from the modifier, not halved with the forester active");
        }

        // ---------------- Recipe ----------------

        [Test]
        public void Recipe_SawmillConsumesOneTimberYieldsOnePlanks()
        {
            var sim = NewSim();
            var s = sim.State;
            int saw = EconomyTestKit.Build(sim, BuildingType.Sawmill);
            sim.DrainEvents();
            var l = s.Economy.Ledger;
            int consumed0 = l.Consumed[(int)Ware.Timber], produced0 = l.Produced[(int)Ware.Planks];
            int timberBefore = EconomyApi.WaresInWorld(s)[(int)Ware.Timber];
            Assert.AreEqual(0, timberBefore);
            EconomyApi.AddToStorehouse(s, Ware.Timber, 1);
            var evs = new List<SimEvent>();
            StepUntil(sim, () => evs.OfType<ProductionCycle>().Any(), 1000, evs);
            sim.Step(200);
            evs.AddRange(sim.DrainEvents());
            var cycles = evs.OfType<ProductionCycle>().Where(p => p.BuildingId == saw).ToList();
            Assert.AreEqual(1, cycles.Count);
            Assert.AreEqual(Ware.Planks, cycles[0].Output);
            Assert.AreEqual(1, cycles[0].Amount);
            Assert.AreEqual(consumed0 + 1, l.Consumed[(int)Ware.Timber]);
            Assert.AreEqual(produced0 + 1, l.Produced[(int)Ware.Planks]);
            Assert.AreEqual(0, EconomyApi.WaresInWorld(s)[(int)Ware.Timber]);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        // ---------------- Construction ----------------

        [Test]
        public void Construction_CompletesWhenAllWaresArrive()
        {
            var sim = NewSim();
            var s = sim.State;
            int planks0 = s.Economy.Ledger.Consumed[(int)Ware.Planks];
            int saw = EconomyTestKit.Build(sim, BuildingType.Sawmill);
            Assert.AreEqual(BuildingState.Active, s.Economy.Buildings[saw].State);
            Assert.IsTrue(s.Economy.Buildings[saw].Stock.All(n => n == 0), "construction wares consumed");
            Assert.GreaterOrEqual(s.Economy.Ledger.Consumed[(int)Ware.Planks] - planks0, 2);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        [Test]
        public void Construction_MissingWareReportsWaitingWareNamingIt()
        {
            var sim = NewSim();
            var s = sim.State;
            Assert.IsTrue(EconomyApi.TrySpendFromStorehouse(s, Ware.Stone, EconomyApi.StorehouseCount(s, Ware.Stone)));
            int v = EconomyTestKit.ChooseVertex(s, BuildingType.Sawmill);
            EconomyTestKit.Submit(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = v });
            sim.Step();
            foreach (int e in EconomyTestKit.RoadsToConnect(s, v))
            {
                EconomyTestKit.Submit(sim, new PlaceRoad { Edge = e });
                sim.Step();
            }
            int id = EconomyApi.BuildingAt(s, v);
            var b = s.Economy.Buildings[id];
            StepUntil(sim, () => b.Stock[(int)Ware.Planks] == 2, 500);
            sim.Step(100);
            Assert.AreEqual(BuildingState.Site, b.State);
            Assert.AreEqual(Reasons.WaitingWare, b.Reason);
            Assert.AreEqual((int)Ware.Stone, b.ReasonWare);
            var info = EconomyApi.Describe(s, id);
            Assert.AreEqual("Sawmill site is waiting for stone.", info.Status);
            Assert.IsTrue(info.StockLines.Any(x => x.StartsWith("stone 0/1")));

            EconomyApi.AddToStorehouse(s, Ware.Stone, 1);
            StepUntil(sim, () => b.State == BuildingState.Active, 500);
            sim.Step();
            Assert.AreEqual(Reasons.InputMissing, b.Reason, "built; now an idle sawmill without timber");
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        [Test]
        public void CancelSite_ReturnsWaresAndConserves()
        {
            var sim = NewSim(ticksPerEdge: 60);
            var s = sim.State;
            sim.Step(70); // first planks has arrived at the forester site
            var site = s.Economy.Buildings[1];
            Assert.GreaterOrEqual(site.Stock[(int)Ware.Planks] + site.Incoming[(int)Ware.Planks], 1);
            int before = EconomyApi.WaresInWorld(s)[(int)Ware.Planks];
            EconomyTestKit.Submit(sim, new CancelSite { BuildingId = 1 });
            sim.Step();
            Assert.AreEqual(BuildingState.Removed, site.State);
            Assert.AreEqual(before, EconomyApi.WaresInWorld(s)[(int)Ware.Planks]);
            sim.Step(400);
            Assert.AreEqual(0, s.Economy.Jobs.Count, "redirected shipments land in the storehouse");
            Assert.AreEqual(14, s.Economy.Buildings[0].Stock[(int)Ware.Planks]);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        // ---------------- Stall reasons ----------------

        [Test]
        public void Stall_NoRoad_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            int v = -1;
            for (int x = 0; x < s.Board.Topology.VertexCount && v < 0; x++)
                if (EconomyApi.CheckPlacement(s, 0, BuildingType.Sawmill, x).Ok && EconomyTestKit.RoadsToConnect(s, x).Count >= 2) v = x;
            EconomyTestKit.Submit(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = v });
            sim.Step(2);
            var b = s.Economy.Buildings[EconomyApi.BuildingAt(s, v)];
            Assert.AreEqual(Reasons.NoRoad, b.Reason);
            StringAssert.StartsWith("No road connects the Sawmill site to the storehouse", EconomyApi.Describe(s, b.Id).Status);
            foreach (int e in EconomyTestKit.RoadsToConnect(s, v))
            {
                EconomyTestKit.Submit(sim, new PlaceRoad { Edge = e });
                sim.Step();
            }
            sim.Step();
            Assert.AreNotEqual(Reasons.NoRoad, b.Reason);
            StepUntil(sim, () => b.State == BuildingState.Active, 1000);
        }

        [Test]
        public void Stall_NoCarrier_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            EconomyApi.AddToStorehouse(s, Ware.Planks, 20);
            EconomyApi.AddToStorehouse(s, Ware.Stone, 20);
            var evs = new List<SimEvent>();
            var types = new[] { BuildingType.Market, BuildingType.GrainMill, BuildingType.Bakery };
            foreach (var t in types)
            {
                int v = EconomyTestKit.ChooseVertex(s, t);
                EconomyTestKit.Submit(sim, new PlaceBuilding { Type = t, Vertex = v });
                sim.Step();
                foreach (int e in EconomyTestKit.RoadsToConnect(s, v))
                {
                    EconomyTestKit.Submit(sim, new PlaceRoad { Edge = e });
                    sim.Step();
                }
                evs.AddRange(sim.DrainEvents());
            }
            StepUntil(sim, () => s.Economy.Jobs.Count == 0 && s.Economy.Buildings.Count(b => b.State == BuildingState.Active) == 5, 3000, evs);
            Assert.IsTrue(evs.OfType<JobStalled>().Any(j => j.Reason == Reasons.NoCarrier), "more jobs than carriers");
            var stalled = evs.OfType<JobStalled>().Where(j => j.Reason == Reasons.NoCarrier).Select(j => j.JobId).Distinct().ToList();
            foreach (int id in stalled)
                Assert.IsTrue(evs.OfType<WareDelivered>().Count(d => d.JobId == id) == 1, "stalled job " + id + " delivered once");
        }

        [Test]
        public void Stall_DemonOnRoute_ProducedAndCleared()
        {
            var sim = NewSim(ticksPerEdge: 30);
            var s = sim.State;
            int flag = s.Economy.DemoVertexB; // the demo shipment's destination flag
            bool block = true;
            EconomyApi.Hooks(sim).IsVertexBlocked.Add((st, v) => block && v == flag);
            var evs = new List<SimEvent>();
            sim.Step(40);
            evs.AddRange(sim.DrainEvents());
            var site = s.Economy.Buildings[1];
            Assert.AreEqual(Reasons.DemonOnRoute, site.Reason);
            Assert.AreEqual("A demon blocks the route to the Forester site.", EconomyApi.Describe(s, 1).Status);
            Assert.IsTrue(evs.OfType<JobStalled>().Any(j => j.Reason == Reasons.DemonOnRoute));
            block = false;
            StepUntil(sim, () => site.State != BuildingState.Site, 500, evs);
            Assert.IsTrue(evs.OfType<JobResumed>().Any());
        }

        [Test]
        public void Stall_OutputFull_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            int forest = EconomyTestKit.HomeHex(s, Terrain.Forest);
            int wc = EconomyTestKit.Build(sim, BuildingType.Woodcutter, EconomyTestKit.Touches(s, forest));
            var b = s.Economy.Buildings[wc];
            s.Economy.Tuning.Def(BuildingType.Woodcutter).Capacity = 1;
            s.Economy.Tuning.TicksPerEdge = 400; // carriers far too slow
            s.Economy.HexBatchRemaining[forest] = 6;
            StepUntil(sim, () => b.Reason == Reasons.OutputFull, 2000);
            Assert.AreEqual("Woodcutter has no room for its output.", EconomyApi.Describe(s, wc).Status);
            s.Economy.Tuning.TicksPerEdge = 5;
            StepUntil(sim, () => b.Reason != Reasons.OutputFull, 2000);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        [Test]
        public void Stall_InputMissing_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            int saw = EconomyTestKit.Build(sim, BuildingType.Sawmill);
            sim.Step(5);
            var b = s.Economy.Buildings[saw];
            Assert.AreEqual(Reasons.InputMissing, b.Reason);
            Assert.AreEqual("Sawmill needs timber to work.", EconomyApi.Describe(s, saw).Status);
            EconomyApi.AddToStorehouse(s, Ware.Timber, 1);
            sim.Step(2);
            Assert.AreEqual(Reasons.WaitingWare, b.Reason);
            Assert.AreEqual("Sawmill is waiting for timber.", EconomyApi.Describe(s, saw).Status);
            StepUntil(sim, () => b.WorkProgress > 0, 500);
            sim.Step();
            Assert.IsNull(b.Reason);
        }

        [Test]
        public void Stall_NoPower_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            EconomyApi.AddToStorehouse(s, Ware.Stone, 5);
            bool power = false;
            EconomyApi.Hooks(sim).IsPowered = (st, id) => power;
            int r = EconomyTestKit.Build(sim, BuildingType.Refinery);
            sim.Step(2);
            var b = s.Economy.Buildings[r];
            Assert.AreEqual(Reasons.NoPower, b.Reason);
            power = true;
            sim.Step(2);
            Assert.AreEqual(Reasons.InputMissing, b.Reason, "no longer no_power; now needs ore");
            Assert.AreEqual((int)Ware.Ore, b.ReasonWare);
        }

        [Test]
        public void Stall_NoBatch_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            int hills = EconomyTestKit.HomeHex(s, Terrain.Hills);
            int q = EconomyTestKit.Build(sim, BuildingType.Quarry, EconomyTestKit.Touches(s, hills));
            var b = s.Economy.Buildings[q];
            StepUntil(sim, () => b.Reason == Reasons.NoBatch, 3000);
            Assert.AreEqual("Quarry has no authorised batch this round.", EconomyApi.Describe(s, q).Status);
            s.Economy.HexBatchRound[hills] = 0;
            Assert.IsTrue(EconomyApi.AuthoriseBatch(s, hills, 2, new EventBuffer()));
            sim.Step(2);
            Assert.IsNull(b.Reason);
        }

        [Test]
        public void Stall_Disabled_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            int saw = EconomyTestKit.Build(sim, BuildingType.Sawmill);
            EconomyTestKit.Submit(sim, new ToggleBuilding { BuildingId = saw, Enabled = false });
            sim.Step();
            Assert.AreEqual(Reasons.Disabled, s.Economy.Buildings[saw].Reason);
            Assert.AreEqual("Sawmill is switched off.", EconomyApi.Describe(s, saw).Status);
            EconomyTestKit.Submit(sim, new ToggleBuilding { BuildingId = saw, Enabled = true });
            sim.Step();
            Assert.AreNotEqual(Reasons.Disabled, s.Economy.Buildings[saw].Reason);
        }

        // ---------------- Interrupted route ----------------

        [Test]
        public void InterruptedRoute_SameShipmentArrivesOnce()
        {
            var sim = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var s = sim.State;
            var topo = s.Board.Topology;
            var carrier = s.Economy.Carriers[0];
            int jobId = carrier.JobId;
            var site = s.Economy.Buildings[1];
            int edgeAB = topo.EdgeBetween(s.Economy.DemoVertexA, s.Economy.DemoVertexB);
            int edgeHA = carrier.CurrentEdge;

            EconomyTestKit.Submit(sim, new RemoveRoad { Edge = edgeAB });
            var evs = new List<SimEvent>();
            StepUntil(sim, () => carrier.CurrentEdge < 0 && carrier.AtVertex == s.Economy.DemoVertexA, 200, evs);
            sim.Step(20);
            evs.AddRange(sim.DrainEvents());
            var job = s.Economy.Jobs.Single(j => j.Id == jobId);
            Assert.AreEqual(Reasons.NoRoad, job.Reason);
            Assert.IsTrue(evs.OfType<JobStalled>().Any(j => j.JobId == jobId && j.Reason == Reasons.NoRoad));
            Assert.IsTrue(carrier.HasLoad, "keeps the load at the flag");
            Assert.AreEqual(Reasons.NoRoad, EconomyApi.Describe(s, 1).Reason);
            Assert.AreEqual("No road connects the Storehouse to the Forester site.", EconomyApi.DescribeJob(s, jobId).Status);

            // Also remove the road behind it and put both back.
            EconomyTestKit.Submit(sim, new RemoveRoad { Edge = edgeHA });
            sim.Step(5);
            EconomyTestKit.Submit(sim, new PlaceRoad { Edge = edgeHA });
            sim.Step();
            int before = site.Stock[(int)Ware.Planks];
            EconomyTestKit.Submit(sim, new PlaceRoad { Edge = edgeAB });
            evs.Clear();
            int deliveredAt = -1;
            for (int i = 0; i < 400; i++)
            {
                sim.Step();
                var tickEvs = sim.DrainEvents();
                evs.AddRange(tickEvs);
                if (tickEvs.OfType<WareDelivered>().Any(d => d.JobId == jobId))
                {
                    deliveredAt = i;
                    int toSite = tickEvs.OfType<WareDelivered>().Count(d => d.BuildingId == 1);
                    Assert.AreEqual(before + toSite, site.Stock[(int)Ware.Planks] + (site.State == BuildingState.Site ? 0 : 0));
                    break;
                }
            }
            Assert.GreaterOrEqual(deliveredAt, 0, "shipment arrived");
            Assert.AreEqual(before + 1, site.Stock[(int)Ware.Planks], "destination rose by exactly one");
            sim.Step(1000);
            evs.AddRange(sim.DrainEvents());
            Assert.AreEqual(1, evs.OfType<WareDelivered>().Count(d => d.JobId == jobId), "arrives exactly once");
            Assert.IsTrue(evs.OfType<JobResumed>().Any(j => j.JobId == jobId));
            Assert.AreEqual(BuildingState.Active, site.State);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        // ---------------- Invalid placement ----------------

        [Test]
        public void InvalidPlacements_RefusedWithSentences()
        {
            var sim = NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;

            int noForest = -1;
            for (int v = 0; v < topo.VertexCount && noForest < 0; v++)
                if (EconomyApi.IsInTerritory(s, 0, v) && !EconomyApi.IsVertexOccupied(s, v) && v != s.Board.WarehouseVertex
                    && !topo.VertexHexes(v).Any(h => s.Board.HexTerrain[h] == Terrain.Forest)) noForest = v;
            var r1 = sim.Submit(new PlaceBuilding { Type = BuildingType.Woodcutter, Vertex = noForest });
            Assert.IsFalse(r1.Ok);
            Assert.AreEqual(Reasons.InvalidSite, r1.Reason);
            Assert.AreEqual("A woodcutter must stand next to a forest.", r1.Message);

            var net = EconomyTestKit.NetworkVertices(s);
            int far = -1;
            for (int e = 0; e < topo.EdgeCount && far < 0; e++)
                if (!net.Contains(topo.EdgeVertexA(e)) && !net.Contains(topo.EdgeVertexB(e)) && !s.Economy.Buildings.Any(b => b.Vertex == topo.EdgeVertexA(e) || b.Vertex == topo.EdgeVertexB(e))) far = e;
            var r2 = sim.Submit(new PlaceRoad { Edge = far });
            Assert.IsFalse(r2.Ok);
            Assert.AreEqual(EconomyRefusals.NotConnected, r2.Reason);
            StringAssert.EndsWith(".", r2.Message);

            var r3 = sim.Submit(new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = s.Economy.DemoVertexB });
            Assert.IsFalse(r3.Ok);
            Assert.AreEqual("That flag is already taken by the Forester.", r3.Message);

            int outside = -1;
            for (int v = 0; v < topo.VertexCount && outside < 0; v++) if (!EconomyApi.IsInTerritory(s, 0, v)) outside = v;
            var r4 = sim.Submit(new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = outside });
            Assert.AreEqual("That flag is outside your territory.", r4.Message);

            var r5 = sim.Submit(new PlaceRoad { Edge = s.Economy.Roads.ToList().IndexOf(true) });
            Assert.AreEqual(EconomyRefusals.RoadExists, r5.Reason);
            var r6 = sim.Submit(new CancelSite { BuildingId = 0 });
            Assert.AreEqual(EconomyRefusals.NotASite, r6.Reason);
            Assert.AreEqual(0, sim.PendingCommands.Count);
        }

        [Test]
        public void RoadsCostPlanks_AndRefuseWithoutStock()
        {
            var sim = NewSim();
            var s = sim.State;
            int store = s.Board.StorehouseVertex;
            int e = s.Board.Topology.VertexEdges(store).First(x => !s.Economy.Roads[x]);
            int planks = s.Economy.Buildings[0].Stock[(int)Ware.Planks];
            EconomyTestKit.Submit(sim, new PlaceRoad { Edge = e });
            sim.Step();
            Assert.IsTrue(s.Economy.Roads[e]);
            Assert.LessOrEqual(s.Economy.Buildings[0].Stock[(int)Ware.Planks], planks - 1);
            EconomyApi.TrySpendFromStorehouse(s, Ware.Planks, EconomyApi.StorehouseCount(s, Ware.Planks));
            int e2 = s.Board.Topology.VertexEdges(store).First(x => !s.Economy.Roads[x]);
            var r = sim.Submit(new PlaceRoad { Edge = e2 });
            Assert.AreEqual(EconomyRefusals.NotEnoughWares, r.Reason);
            Assert.AreEqual("A road needs 1 planks in the storehouse.", r.Message);
        }

        // ---------------- External requests ----------------

        [Test]
        public void RequestWares_DeliversToBuilding_AndTryConsumeSpendsOnlyUnreserved()
        {
            var sim = NewSim();
            var s = sim.State;
            EconomyApi.AddToStorehouse(s, Ware.Planks, 10);
            EconomyApi.AddToStorehouse(s, Ware.Stone, 10);
            EconomyApi.AddToStorehouse(s, Ware.Rations, 3);
            int m = EconomyTestKit.Build(sim, BuildingType.Market);
            Assert.IsTrue(EconomyApi.RequestWares(s, m, Ware.Rations, 3));
            sim.Step();
            Assert.AreEqual(Reasons.WaitingWare, s.Economy.Buildings[m].Reason);
            Assert.AreEqual(0, EconomyApi.StorehouseCount(s, Ware.Rations), "reserved rations are not spendable");
            Assert.IsFalse(EconomyApi.TrySpendFromStorehouse(s, Ware.Rations, 1));
            StepUntil(sim, () => s.Economy.Buildings[m].Stock[(int)Ware.Rations] == 3, 1000);
            sim.Step();
            Assert.IsNull(s.Economy.Buildings[m].Reason);
            Assert.IsTrue(EconomyApi.TryConsume(s, m, Ware.Rations, 2));
            Assert.IsFalse(EconomyApi.TryConsume(s, m, Ware.Rations, 2));
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        [Test]
        public void Hooks_RoundEndingAndStartedRunInOrder()
        {
            var sim = NewSim();
            var log = new List<string>();
            var h = EconomyApi.Hooks(sim);
            h.OnRoundEnding.Add((st, ev) => log.Add("end" + st.Economy.Round));
            h.OnRoundStarted.Add((st, ev) => log.Add("start" + st.Economy.Round));
            sim.Step();
            EconomyTestKit.Submit(sim, new EndRound());
            sim.Step();
            CollectionAssert.AreEqual(new[] { "start1", "end1", "start2" }, log);
        }
    }
}
