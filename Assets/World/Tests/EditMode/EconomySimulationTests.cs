using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Economy;

namespace World.Sim.Tests
{
    /// <summary>Long-running economy checks: conservation, headless commission, save round-trip.</summary>
    public class EconomySimulationTests
    {
        // ---------------- Conservation ----------------

        [Test]
        public void Conservation_20000Ticks_RandomValidAndInvalidCommands()
        {
            var sim = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var s = sim.State;
            var topo = s.Board.Topology;
            var rng = new Rng(4242);
            var blocked = new bool[topo.VertexCount];
            bool power = true;
            var hooks = EconomyApi.Hooks(sim);
            hooks.IsVertexBlocked.Add((st, v) => blocked[v]);
            hooks.IsPowered = (st, id) => power;
            EconomyApi.AddToStorehouse(s, Ware.Planks, 30);
            EconomyApi.AddToStorehouse(s, Ware.Stone, 30);
            var script = new CommissionScript(sim);

            int accepted = 0, refused = 0, delivered = 0, harvested = 0, produced = 0;
            for (int t = 0; t < 20000; t++)
            {
                script.Act();
                if (t % 25 == 0) RepairOneRoad(sim);
                if (t % 100 == 0 && EconomyApi.StorehouseCount(s, Ware.Planks) < 6) EconomyApi.AddToStorehouse(s, Ware.Planks, 6);
                if (t % 300 == 0) Array.Clear(blocked, 0, blocked.Length);
                if (rng.Chance(1, 6))
                {
                    var c = RandomCommand(s, rng);
                    if (c != null)
                    {
                        if (sim.Submit(c).Ok) accepted++;
                        else refused++;
                    }
                }
                switch (rng.NextInt(40))
                {
                    case 0: EconomyApi.AddToStorehouse(s, (Ware)rng.NextInt(Wares.Count), 1 + rng.NextInt(4)); break;
                    case 1: EconomyApi.TrySpendFromStorehouse(s, (Ware)rng.NextInt(Wares.Count), 1 + rng.NextInt(3)); break;
                    case 2:
                        if (s.Economy.Buildings.Count > 0)
                            EconomyApi.RequestWares(s, rng.NextInt(s.Economy.Buildings.Count), (Ware)rng.NextInt(Wares.Count), 1 + rng.NextInt(3));
                        break;
                    case 3:
                        if (s.Economy.Buildings.Count > 0)
                            EconomyApi.TryConsume(s, rng.NextInt(s.Economy.Buildings.Count), (Ware)rng.NextInt(Wares.Count), 1 + rng.NextInt(2));
                        break;
                    case 4: if (rng.Chance(1, 4)) blocked[rng.NextInt(topo.VertexCount)] = true; break;
                    case 5: power = !power; break;
                    case 6:
                        var ev = new EventBuffer();
                        EconomyApi.AuthoriseBatch(s, rng.NextInt(topo.HexCount), 1 + rng.NextInt(6), ev);
                        break;
                }
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    if (e is WareDelivered) delivered++;
                    else if (e is HarvestCycle) harvested++;
                    else if (e is ProductionCycle) produced++;
                }
                var err = EconomyTestKit.CheckInvariants(s);
                if (err != null) Assert.Fail("tick " + s.Tick + ": " + err);
            }
            TestContext.WriteLine($"accepted {accepted}, refused {refused}, delivered {delivered}, harvest cycles {harvested}, production cycles {produced}");
            Assert.Greater(accepted, 100);
            Assert.Greater(refused, 100);
            Assert.Greater(delivered, 100);
            Assert.Greater(harvested, 10);
            Assert.Greater(produced, 5);
        }

        /// <summary>Reconnect one disconnected building by one road (keeps the random economy alive).</summary>
        private static void RepairOneRoad(WorldSim sim)
        {
            var s = sim.State;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State == BuildingState.Removed || EconomyApi.IsConnected(s, b.Id)) continue;
                var roads = EconomyTestKit.RoadsToConnect(s, b.Vertex);
                foreach (int e in roads)
                    if (sim.Submit(new PlaceRoad { Edge = e }).Ok) return;
            }
        }

        private static Command RandomCommand(WorldState s, Rng rng)
        {
            var topo = s.Board.Topology;
            int n = s.Economy.Buildings.Count;
            switch (rng.NextInt(10))
            {
                case 0:
                case 1:
                case 2:
                    return new PlaceBuilding { Type = (BuildingType)rng.NextInt(14), Vertex = rng.NextInt(topo.VertexCount + 2) - 1 };
                case 3:
                case 4:
                    return new PlaceRoad { Edge = rng.NextInt(topo.EdgeCount + 2) - 1 };
                case 5:
                {
                    var roads = new List<int>();
                    for (int e = 0; e < topo.EdgeCount; e++) if (s.Economy.Roads[e]) roads.Add(e);
                    if (roads.Count == 0 || rng.Chance(1, 4)) return new RemoveRoad { Edge = rng.NextInt(topo.EdgeCount) };
                    return new RemoveRoad { Edge = roads[rng.NextInt(roads.Count)] };
                }
                case 6: return new CancelSite { BuildingId = rng.NextInt(n + 1) };
                case 7: return new ToggleBuilding { BuildingId = rng.NextInt(n + 1), Enabled = rng.Chance(1, 2) };
                case 8: return new SetPriority { BuildingId = rng.NextInt(n + 1), Priority = (Priority)rng.NextInt(4) };
                default: return new EndRound { PlayerId = rng.Chance(1, 8) ? PlayerIds.Rival : PlayerIds.Human };
            }
        }

        // ---------------- Headless commission ----------------

        private static long RunCommission(ulong seed, int budget)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var script = new CommissionScript(sim);
            int events = 0;
            for (int t = 0; t < budget && !sim.State.Economy.Commission.Complete; t++)
            {
                script.Act();
                sim.Step();
                events += sim.DrainEvents().OfType<CommissionCompleted>().Count();
            }
            Assert.IsNull(EconomyTestKit.CheckInvariants(sim.State), "seed " + seed);
            if (!sim.State.Economy.Commission.Complete) return -1;
            Assert.AreEqual(1, events, "CommissionCompleted once");
            sim.Step(50);
            Assert.AreEqual(0, sim.DrainEvents().OfType<CommissionCompleted>().Count());
            return sim.State.Economy.Commission.CompletedTick;
        }

        [Test]
        public void Commission_Headless_DefaultSeed_Within3600Ticks()
        {
            long t = RunCommission(EconomyTestKit.DefaultSeed, 3600);
            TestContext.WriteLine("default seed commission at tick " + t);
            Console.WriteLine("COMMISSION default " + t);
            Assert.Greater(t, 0, "commission not reached in 3600 ticks");
        }

        [Test]
        public void Commission_Headless_TwentyOtherSeeds_Within3600Ticks()
        {
            long worst = 0;
            ulong worstSeed = 0;
            var failed = new List<ulong>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                long t = RunCommission(seed * 7919UL + 3UL, 3600);
                if (t < 0) failed.Add(seed);
                if (t > worst)
                {
                    worst = t;
                    worstSeed = seed;
                }
            }
            TestContext.WriteLine("worst commission tick " + worst + " (seed index " + worstSeed + ")");
            Console.WriteLine("COMMISSION worst " + worst + " idx " + worstSeed);
            CollectionAssert.IsEmpty(failed, "seeds that missed the commission");
        }

        // ---------------- Save round-trip ----------------

        private static WorldSim Load(string json)
        {
            var copy = JsonConvert.DeserializeObject<WorldState>(json);
            var sim = new WorldSim(copy);
            EconomyModule.Register(sim);
            Strategy.StrategyModule.Register(sim);
            Military.MilitaryModule.Register(sim);
            return sim;
        }

        [Test]
        public void SaveRoundTrip_MidFlight_ContinuesIdentically()
        {
            var a = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var scriptA = new CommissionScript(a);
            for (int t = 0; t < 700; t++)
            {
                scriptA.Act();
                a.Step();
            }
            Assert.IsTrue(a.State.Economy.Carriers.Any(c => c.HasLoad && c.CurrentEdge >= 0), "a shipment is in flight");
            // Drain the one-tick command queue so nothing pending is lost at save.
            Assert.AreEqual(0, a.PendingCommands.Count);
            a.DrainEvents();
            string json = JsonConvert.SerializeObject(a.State);
            var b = Load(json);
            Assert.AreEqual(json, JsonConvert.SerializeObject(b.State), "round-trips exactly");

            var scriptB = new CommissionScript(b);
            var deliveredA = new List<int>();
            var deliveredB = new List<int>();
            for (int t = 0; t < 1000; t++)
            {
                scriptA.Act();
                scriptB.Act();
                a.Step();
                b.Step();
                deliveredA.AddRange(a.DrainEvents().OfType<WareDelivered>().Select(d => d.JobId));
                deliveredB.AddRange(b.DrainEvents().OfType<WareDelivered>().Select(d => d.JobId));
            }
            Assert.AreEqual(JsonConvert.SerializeObject(a.State), JsonConvert.SerializeObject(b.State));
            CollectionAssert.AreEqual(deliveredA, deliveredB);
            Assert.AreEqual(deliveredA.Count, deliveredA.Distinct().Count(), "each shipment arrives once");
        }
    }
}
