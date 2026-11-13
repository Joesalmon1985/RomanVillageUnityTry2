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
            sim.State.Economy.Tuning.MinRoundTicks = 0; // the bell rings at once here (the harvest season has its own test)
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
            Assert.AreEqual(2, e.Roads.Count(r => r), "demonstration road of 2 segments");
            var topo = s.Board.Topology;
            Assert.AreEqual(1, topo.Distance(s.Board.StorehouseVertex, e.DemoVertexA));
            Assert.AreEqual(2, topo.Distance(s.Board.StorehouseVertex, e.DemoVertexB));
            Assert.IsTrue(topo.VertexHexes(e.DemoVertexB).Any(h => s.Board.HexTerrain[h] == Terrain.Forest), "the forester site touches the forest");
            Assert.AreNotEqual(s.Board.WarehouseVertex, e.DemoVertexA);
            var site = e.Buildings[1];
            Assert.AreEqual(BuildingType.Forester, site.Type);
            Assert.AreEqual(BuildingState.Site, site.State);
            Assert.AreEqual(e.DemoVertexB, site.Vertex);
            var c = e.Carriers[0];
            Assert.IsTrue(c.HasLoad);
            Assert.AreEqual(Ware.Planks, c.Load);
            Assert.GreaterOrEqual(c.CurrentEdge, 0, "already walking at tick 0");
            Assert.AreEqual(e.Tuning.Carriers, e.Carriers.Count);
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
            sim.Step(10);
            Assert.AreEqual(10, s.Economy.Carriers[0].Progress, "visibly walking in the first second");
        }

        [Test]
        public void Starter_StockCoversTheSixCommissionBuildingsAndTheirRoads()
        {
            var t = new EconomyTuning();
            int planks = t.StarterStock[(int)Ware.Planks], bricks = t.StarterStock[(int)Ware.Bricks];
            foreach (var bt in new[] { BuildingType.Forester, BuildingType.Woodcutter, BuildingType.Sawmill, BuildingType.ClayPit, BuildingType.CharcoalKiln, BuildingType.BrickKiln })
            {
                planks -= t.Def(bt).Cost[(int)Ware.Planks];
                bricks -= t.Def(bt).Cost[(int)Ware.Bricks];
            }
            // About 5 road segments to connect them; one RoadCost pays for RoadSegmentsPerCost segments.
            int payments = (5 + t.RoadSegmentsPerCost - 1) / t.RoadSegmentsPerCost;
            planks -= payments * t.RoadCost[(int)Ware.Planks];
            bricks -= payments * t.RoadCost[(int)Ware.Bricks];
            Assert.GreaterOrEqual(planks, 0, "the first buildings can be placed before any brick is made");
            Assert.GreaterOrEqual(bricks, 0);
            Assert.Less(planks, t.CommissionPlanks, "the commission needs real production");
            Assert.Less(bricks, t.CommissionBricks);
            Assert.Greater(t.StarterStock[(int)Ware.Coal], 0, "a little coal lets the brick kiln start first");
            Assert.GreaterOrEqual(t.StarterStock[(int)Ware.Rations], new Strategy.StrategyTuning().CityCost[(int)Ware.Rations], "rations for the first city");
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
            int forestSize = s.Economy.Tuning.BatchSizeFor(Terrain.Forest), size = s.Economy.Tuning.BatchSizeFor(Terrain.Hills);
            Assert.IsTrue(batches.Any(b => b.Hex == forest && b.Amount == (forestSize + 1) / 2), "forest halved without an active forester");
            Assert.IsTrue(batches.Any(b => b.Hex == hills && b.Amount == size));

            int before = s.Economy.HexBatchRemaining[hills];
            var ev = new EventBuffer();
            Assert.IsFalse(EconomyApi.AuthoriseBatch(s, hills, size, ev), "second authorisation in the same round");
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
                var draw = a.State.Economy.LastDraw;
                Assert.AreEqual(draw.Length, draw.Distinct().Count(), "distinct numbers");
                // Prologue starter rounds draw 2 numbers (home forest + hills or fields); afterwards NumbersDrawn.
                Assert.AreEqual(a.State.Economy.Round <= a.State.Economy.Tuning.StarterRounds ? 2 : a.State.Economy.Tuning.NumbersDrawn, draw.Length);
                Assert.IsTrue(draw.All(n => n >= 1 && n <= 4));
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
            EconomyApi.Hooks(sim).BatchModifiers.Add((st, hex, ware, amount) => ware == Ware.Logs ? amount + 2 : amount);
            // Round 2 or 3 of the starter sequence draws the home forest.
            var evs = new List<SimEvent>();
            for (int i = 0; i < 2; i++)
            {
                EconomyTestKit.Submit(sim, new EndRound());
                sim.Step();
                evs.AddRange(sim.DrainEvents());
            }
            var fb = evs.OfType<BatchAuthorised>().First(b => b.Hex == forest);
            Assert.AreEqual(s.Economy.Tuning.BatchSizeFor(Terrain.Forest) + 2, fb.Amount, "batch + 2 from the modifier, not halved with the forester active");
        }

        // ---------------- Recipe ----------------

        [Test]
        public void Recipe_SawmillConsumesOneLogsYieldsOnePlanks()
        {
            var sim = NewSim();
            var s = sim.State;
            int saw = EconomyTestKit.Build(sim, BuildingType.Sawmill);
            sim.DrainEvents();
            var l = s.Economy.Ledger;
            int consumed0 = l.Consumed[(int)Ware.Logs], produced0 = l.Produced[(int)Ware.Planks];
            int logsBefore = EconomyApi.WaresInWorld(s)[(int)Ware.Logs];
            Assert.AreEqual(0, logsBefore);
            EconomyApi.AddToStorehouse(s, Ware.Logs, 1);
            var evs = new List<SimEvent>();
            StepUntil(sim, () => evs.OfType<ProductionCycle>().Any(), 1000, evs);
            sim.Step(200);
            evs.AddRange(sim.DrainEvents());
            var cycles = evs.OfType<ProductionCycle>().Where(p => p.BuildingId == saw).ToList();
            Assert.AreEqual(1, cycles.Count);
            Assert.AreEqual(Ware.Planks, cycles[0].Output);
            Assert.AreEqual(1, cycles[0].Amount);
            Assert.AreEqual(consumed0 + 1, l.Consumed[(int)Ware.Logs]);
            Assert.AreEqual(produced0 + 1, l.Produced[(int)Ware.Planks]);
            Assert.AreEqual(0, EconomyApi.WaresInWorld(s)[(int)Ware.Logs]);
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
            Assert.GreaterOrEqual(s.Economy.Ledger.Consumed[(int)Ware.Planks] - planks0, EconomyApi.Def(s, BuildingType.Sawmill).Cost[(int)Ware.Planks]);
            Assert.AreEqual(EconomyApi.Def(s, BuildingType.Sawmill).Cost[(int)Ware.Bricks], s.Economy.Ledger.Construction[(int)Ware.Bricks], "construction booked as its own sink");
            Assert.IsNull(EconomyTestKit.CheckInvariants(s));
        }

        [Test]
        public void Construction_MissingWareReportsWaitingWareNamingIt()
        {
            var sim = NewSim();
            var s = sim.State;
            Assert.IsTrue(EconomyApi.TrySpendFromStorehouse(s, Ware.Bricks, EconomyApi.StorehouseCount(s, Ware.Bricks)));
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
            StepUntil(sim, () => b.Stock[(int)Ware.Planks] == EconomyApi.Def(s, BuildingType.Sawmill).Cost[(int)Ware.Planks], 500);
            sim.Step(100);
            Assert.AreEqual(BuildingState.Site, b.State);
            Assert.AreEqual(Reasons.WaitingWare, b.Reason);
            Assert.AreEqual((int)Ware.Bricks, b.ReasonWare);
            var info = EconomyApi.Describe(s, id);
            Assert.AreEqual("Sawmill site is waiting for bricks.", info.Status);
            Assert.IsTrue(info.StockLines.Any(x => x.StartsWith("bricks 0/1")));

            EconomyApi.AddToStorehouse(s, Ware.Bricks, 1);
            StepUntil(sim, () => b.State == BuildingState.Active, 500);
            sim.Step();
            Assert.AreEqual(Reasons.InputMissing, b.Reason, "built; now an idle sawmill without logs");
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
            Assert.AreEqual(s.Economy.Tuning.StarterStock[(int)Ware.Planks], s.Economy.Buildings[0].Stock[(int)Ware.Planks]);
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
            // Only the four first carriers, and none joining with new buildings: more jobs than carriers.
            s.Economy.Carriers.RemoveRange(4, s.Economy.Carriers.Count - 4);
            s.Economy.Tuning.CarriersPerBuilding = 0;
            EconomyApi.AddToStorehouse(s, Ware.Planks, 20);
            EconomyApi.AddToStorehouse(s, Ware.Bricks, 20);
            var evs = new List<SimEvent>();
            var types = new[] { BuildingType.Market, BuildingType.Mill, BuildingType.Bakery };
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
            Assert.AreEqual("A demon on its flag stops the Forester site.", EconomyApi.Describe(s, 1).Status);
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
            Assert.AreEqual("Sawmill needs logs to work.", EconomyApi.Describe(s, saw).Status);
            EconomyApi.AddToStorehouse(s, Ware.Logs, 1);
            sim.Step(2);
            Assert.AreEqual(Reasons.WaitingWare, b.Reason);
            Assert.AreEqual("Sawmill is waiting for logs.", EconomyApi.Describe(s, saw).Status);
            StepUntil(sim, () => b.WorkProgress > 0, 500);
            sim.Step();
            Assert.IsNull(b.Reason);
        }

        [Test]
        public void Stall_NoPower_ProducedAndCleared()
        {
            var sim = NewSim();
            var s = sim.State;
            EconomyApi.AddToStorehouse(s, Ware.Bricks, 5);
            bool power = false;
            EconomyApi.Hooks(sim).IsPowered = (st, id) => power;
            int r = EconomyTestKit.Build(sim, BuildingType.Smelter);
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
            int q = EconomyTestKit.Build(sim, BuildingType.ClayPit, EconomyTestKit.Touches(s, hills));
            var b = s.Economy.Buildings[q];
            StepUntil(sim, () => b.Reason == Reasons.NoBatch, 3000);
            Assert.AreEqual("Clay pit has no authorised batch this round.", EconomyApi.Describe(s, q).Status);
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
            Assert.AreEqual("That flag is outside your territory (more than 4 road segments, 80 m, from your cities).", r4.Message);

            var r5 = sim.Submit(new PlaceRoad { Edge = s.Economy.Roads.ToList().IndexOf(true) });
            Assert.AreEqual(EconomyRefusals.RoadExists, r5.Reason);
            var r6 = sim.Submit(new CancelSite { BuildingId = 0 });
            Assert.AreEqual(EconomyRefusals.NotASite, r6.Reason);
            Assert.AreEqual(0, sim.PendingCommands.Count);
        }

        [Test]
        public void RoadsCostPlanksAndBricks_OnePaymentPerThreeSegments_AndRefuseWithoutStock()
        {
            var sim = NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            Assert.AreEqual(3, s.Economy.Tuning.RoadSegmentsPerCost);
            int store = s.Board.StorehouseVertex;
            // Walk a fresh hex edge out of the home corner, one segment per tick.
            int he = topo.CornerHexEdges(store).First(x => topo.HexEdgeSegments(x).All(seg => !s.Economy.Roads[seg]));
            var flags = topo.HexEdgeFlags(he);
            if (flags[0] != store) flags = flags.Reverse().ToArray();
            int consumed0 = s.Economy.Ledger.Consumed[(int)Ware.Planks], bricks0 = s.Economy.Ledger.Consumed[(int)Ware.Bricks];
            CollectionAssert.AreEqual(EconomyTuning.W(Ware.Planks, 1, Ware.Bricks, 1), EconomyApi.RoadCostFor(s, 1));
            CollectionAssert.AreEqual(EconomyTuning.W(Ware.Planks, 2, Ware.Bricks, 2), EconomyApi.RoadCostFor(s, 4));
            for (int k = 0; k < 3; k++)
            {
                EconomyTestKit.Submit(sim, new PlaceRoad { Edge = topo.EdgeBetween(flags[k], flags[k + 1]) });
                sim.Step();
                Assert.IsTrue(s.Economy.Roads[topo.EdgeBetween(flags[k], flags[k + 1])]);
            }
            Assert.AreEqual(consumed0 + 1, s.Economy.Ledger.Consumed[(int)Ware.Planks], "one hex edge (3 segments) cost 1 planks");
            Assert.AreEqual(bricks0 + 1, s.Economy.Ledger.Consumed[(int)Ware.Bricks], "and 1 bricks");
            Assert.AreEqual(1, s.Economy.Ledger.Roads[(int)Ware.Bricks], "booked as a road sink");
            Assert.AreEqual(0, s.Economy.RoadCredit);

            // Next segment pays again; with no planks it is refused, but a paid credit still builds.
            EconomyApi.TrySpendFromStorehouse(s, Ware.Planks, EconomyApi.StorehouseCount(s, Ware.Planks));
            int next = topo.VertexEdges(flags[3]).First(x => !s.Economy.Roads[x]);
            var r = sim.Submit(new PlaceRoad { Edge = next });
            Assert.AreEqual(EconomyRefusals.NotEnoughWares, r.Reason);
            Assert.AreEqual("A road needs 1 planks and 1 bricks in the storehouse (it pays for 3 segments).", r.Message);
            s.Economy.RoadCredit = 1;
            Assert.IsTrue(sim.Submit(new PlaceRoad { Edge = next }).Ok, "paid-for segment needs no stock");
            sim.Step();
            Assert.AreEqual(0, s.Economy.RoadCredit);
        }

        [Test]
        public void Territory_FourSegmentsBuildable_FiveRefusedWithASentence()
        {
            var sim = NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            int home = s.Board.HomeCityVertex;
            Assert.AreEqual(4, s.Economy.Tuning.TerritorySegments);
            int at4 = -1, at5 = -1;
            for (int v = 0; v < topo.VertexCount; v++)
            {
                int d = topo.Distance(home, v);
                if (d == 4 && at4 < 0 && !EconomyApi.IsVertexOccupied(s, v)) at4 = v;
                if (d == 5 && at5 < 0) at5 = v;
            }
            Assert.IsTrue(EconomyApi.IsInTerritory(s, 0, at4));
            Assert.IsFalse(EconomyApi.IsInTerritory(s, 0, at5));
            Assert.IsTrue(sim.Check(new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = at4 }).Ok, "80 m from the city corner is buildable");
            var r = sim.Check(new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = at5 });
            Assert.AreEqual(Reasons.InvalidSite, r.Reason);
            Assert.AreEqual("That flag is outside your territory (more than 4 road segments, 80 m, from your cities).", r.Message);

            // The home territory is a compact cluster: 16 flags, all within 80 m walking.
            Assert.AreEqual(16, Enumerable.Range(0, topo.VertexCount).Count(v => EconomyApi.IsInTerritory(s, 0, v)));
            // Intermediate flags can hold buildings; terrain comes from the hexes of their hex edge.
            int mid = Enumerable.Range(topo.CornerCount, topo.VertexCount - topo.CornerCount)
                .First(v => EconomyApi.IsInTerritory(s, 0, v) && !EconomyApi.IsVertexOccupied(s, v) && v != s.Board.WarehouseVertex
                    && topo.VertexHexes(v).Any(h => s.Board.HexTerrain[h] == Terrain.Hills));
            Assert.IsTrue(sim.Check(new PlaceBuilding { Type = BuildingType.ClayPit, Vertex = mid }).Ok);
            // A flag holding a city cannot hold a building (the home corner also holds the storehouse).
            Assert.AreEqual(Reasons.InvalidSite, sim.Check(new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = home }).Reason);
            int corner1 = topo.CornerNeighbours(home)[0];
            EconomyApi.AddCity(s, 0, corner1);
            Assert.AreEqual("That flag is taken by a city.", sim.Check(new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = corner1 }).Message);
        }

        // ---------------- External requests ----------------

        [Test]
        public void RequestWares_DeliversToBuilding_AndTryConsumeSpendsOnlyUnreserved()
        {
            var sim = NewSim();
            var s = sim.State;
            EconomyApi.AddToStorehouse(s, Ware.Planks, 10);
            EconomyApi.AddToStorehouse(s, Ware.Bricks, 10);
            EconomyApi.AddToStorehouse(s, Ware.Weapons, 3);
            int m = EconomyTestKit.Build(sim, BuildingType.Market);
            Assert.IsTrue(EconomyApi.RequestWares(s, m, Ware.Weapons, 3));
            sim.Step();
            Assert.AreEqual(Reasons.WaitingWare, s.Economy.Buildings[m].Reason);
            Assert.AreEqual(0, EconomyApi.StorehouseCount(s, Ware.Weapons), "reserved weapons are not spendable");
            Assert.IsFalse(EconomyApi.TrySpendFromStorehouse(s, Ware.Weapons, 1));
            StepUntil(sim, () => s.Economy.Buildings[m].Stock[(int)Ware.Weapons] == 3, 1000);
            sim.Step();
            Assert.IsNull(s.Economy.Buildings[m].Reason);
            Assert.IsTrue(EconomyApi.TryConsume(s, m, Ware.Weapons, 2));
            Assert.IsFalse(EconomyApi.TryConsume(s, m, Ware.Weapons, 2));
            Assert.AreEqual(2, s.Economy.Ledger.External[(int)Ware.Weapons], "booked as an external sink");
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

        [Test]
        public void Round_HarvestSeason_BellRefusedUntilMinRoundTicks()
        {
            var sim = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var s = sim.State;
            int min = s.Economy.Tuning.MinRoundTicks;
            Assert.Greater(min, 0, "a round lasts at least a harvest season");
            sim.Step();
            Assert.AreEqual(s.Tick, s.Economy.RoundStartedTick, "round 1 started at its draw");
            Assert.AreEqual(min, EconomyApi.BellTicksLeft(s));
            var r = sim.Submit(new EndRound { PlayerId = PlayerIds.Human });
            Assert.AreEqual(EconomyRefusals.RoundTooSoon, r.Reason);
            StringAssert.Contains("the bell can ring in 60 s", r.Message);
            for (int i = 0; i < min - 1; i++) sim.Step();
            Assert.AreEqual(1, EconomyApi.BellTicksLeft(s));
            Assert.AreEqual(EconomyRefusals.RoundTooSoon, sim.Check(new EndRound { PlayerId = PlayerIds.Human }).Reason);
            sim.Step();
            Assert.AreEqual(0, EconomyApi.BellTicksLeft(s));
            Assert.AreEqual(min, EconomyApi.RoundElapsedTicks(s));
            Assert.IsTrue(sim.Submit(new EndRound { PlayerId = PlayerIds.Human }).Ok);
            sim.Step();
            Assert.AreEqual(2, s.Economy.Round);
            Assert.AreEqual(min, EconomyApi.BellTicksLeft(s), "a new season starts with the new round");
        }
    }
}
