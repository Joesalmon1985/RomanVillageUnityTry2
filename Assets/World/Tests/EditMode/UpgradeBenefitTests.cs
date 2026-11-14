using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// The benefit of each grade 1 upgrade, measured in the building's own role (PRD_AGES 5.6: "each benefit has a measurement
    /// test in its role"), for the 15 types that have one and the town. Each building runs in a rig that removes everything
    /// else that could limit it (plenty of hex stock or inputs, a big store, 35 carriers, no stock targets) for the same
    /// number of ticks before and after, so the numbers are the building's. One minute is 600 ticks.
    /// </summary>
    public class UpgradeBenefitTests
    {
        private const int Window = 6000; // ten minutes

        private sealed class Rig
        {
            public WorldSim Sim;
            public WorldState S;
            public Settlement Camp;
            public Building B;
        }

        private static Rig Make(BuildingType type, Grade grade)
        {
            var sim = UpgradeTests.Historic();
            var s = sim.State;
            var t = s.Board.Topology;
            var def = BuildingDefs.Get(type);
            var tu = s.Economy.Tuning;
            tu.CollectorStore = 100000;
            tu.StockTarget = new int[Wares.Count];
            tu.WarriorsPerLodge = 3;
            // A free camp corner by the building's terrain (any corner for workshops).
            int corner = -1, sector = 0;
            foreach (int c in t.Corners)
            {
                if (!PlotLayout.IsSettlementCorner(t, c) || EconomyApi.SettlementAt(s, c) != null) continue;
                if (t.CornerNeighbours(c).Any(n => EconomyApi.SettlementAt(s, n) != null)) continue;
                if (!def.IsCollector) { corner = c; break; }
                for (int sec = 0; sec < 3; sec++)
                    if (s.Board.HexTerrain[PlotLayout.SectorHex(t, c, sec)] == def.Terrain) { corner = c; sector = sec; break; }
                if (corner >= 0) break;
            }
            Assert.GreaterOrEqual(corner, 0, "a corner for " + type);
            var st = EconomyLogic.AddSettlement(s, 0, corner, SettlementState.Active);
            EconomyLogic.FurnishSettlement(s, st, 35);
            Building b;
            if (def.Class == BuildingClass.PrimitiveCollector)
                b = s.Economy.Buildings.Single(x => x.Settlement == st.Id && x.Type == type);
            else
            {
                int plot = PlotLayout.PlotId(corner, sector, 1);
                b = new Building { Id = s.Economy.Buildings.Count, Type = type, Faction = 0, Settlement = st.Id, Plot = plot, Hex = PlotLayout.PlotHex(t, plot), State = BuildingState.Active };
                s.Economy.Buildings.Add(b);
            }
            b.Grade = grade;
            if (def.IsCollector)
            {
                var ware = TerrainInfo.RawWare(def.Terrain).Value;
                s.Economy.Hexes[b.Hex].Mature += 5000;
                s.Economy.Ledger.StockAdded[(int)ware] += 5000;
            }
            else
            {
                for (int w = 0; w < Wares.Count; w++) if (def.Inputs[w] > 0) K.Give(s, st.Id, (Ware)w, 4000);
                if (type == BuildingType.WarriorLodge) { K.Give(s, st.Id, Ware.Bread, 4000); K.Give(s, st.Id, Ware.Meat, 4000); }
            }
            return new Rig { Sim = sim, S = s, Camp = st, B = b };
        }

        /// <summary>What the building made in a window: collectors count wares and units taken, workshops wares, lodges recruits.</summary>
        private static (int output, int units) Measure(BuildingType type, Grade grade)
        {
            var r = Make(type, grade);
            int output = 0, units = 0;
            var def = BuildingDefs.Get(type);
            for (int i = 0; i < Window; i++)
            {
                r.Sim.Step();
                foreach (var e in r.Sim.DrainEvents())
                {
                    switch (e)
                    {
                        case Collected c when c.BuildingId == r.B.Id: output += c.Amount; units++; break;
                        case ProductionCycle p when p.BuildingId == r.B.Id: output += p.Amount; units++; break;
                        case WarriorTrained w when w.Lodge == r.B.Id: output++; units++; break;
                    }
                }
            }
            Assert.IsNull(K.CheckInvariants(r.S), type + " grade " + grade + " keeps the books");
            return (output, units);
        }

        private static string PerMinute(int n) => (n * 600.0 / Window).ToString("0.0");

        // ---- one test per role ----

        [TestCase(BuildingType.LogPile)]
        [TestCase(BuildingType.ClayScrape)]
        [TestCase(BuildingType.GrainBasket)]
        [TestCase(BuildingType.ThornFold)]
        [TestCase(BuildingType.RockHeap)]
        public void PrimitiveCollector_Grade1_CycleHundredToSeventy_UnitsOutPerMinuteOnAFullHex(BuildingType type)
        {
            var g0 = Measure(type, Grade.Prehistoric);
            var g1 = Measure(type, Grade.Historic);
            TestContext.WriteLine(type + ": " + PerMinute(g0.output) + " -> " + PerMinute(g1.output) + " units per minute (" + g0.output + " -> " + g1.output + " in " + Window + " ticks)");
            Assert.AreEqual(Window / 100, g0.output, 1);
            Assert.AreEqual(Window / 70, g1.output, 1);
            Assert.AreEqual(1.43, (double)g1.output / g0.output, 0.03);
            Assert.AreEqual(g0.units, g0.output, "one ware per unit taken");
            Assert.AreEqual(g1.units, g1.output);
        }

        [TestCase(BuildingType.WoodcutterHut)]
        [TestCase(BuildingType.ClayPit)]
        [TestCase(BuildingType.Farm)]
        [TestCase(BuildingType.ShepherdHut)]
        [TestCase(BuildingType.Quarry)]
        public void ImprovedCollector_Grade1_ThreeFromEachUnitInsteadOfTwo_WaresPerMinute(BuildingType type)
        {
            var g0 = Measure(type, Grade.Prehistoric);
            var g1 = Measure(type, Grade.Historic);
            TestContext.WriteLine(type + ": " + PerMinute(g0.output) + " -> " + PerMinute(g1.output) + " wares per minute; "
                + (double)g0.output / g0.units + " -> " + (double)g1.output / g1.units + " wares per stock unit");
            Assert.AreEqual(2.0, (double)g0.output / g0.units, 1e-9, "two from each unit");
            Assert.AreEqual(3.0, (double)g1.output / g1.units, 1e-9, "three from each unit");
            Assert.AreEqual(g0.units, g1.units, 1, "the cycle is unchanged (60 ticks)");
            Assert.AreEqual(Window / 60, g0.units, 1);
            Assert.AreEqual(1.5, (double)g1.output / g0.output, 0.02);
        }

        [TestCase(BuildingType.Sawmill, 100, 65)]
        [TestCase(BuildingType.Kiln, 150, 100)]
        [TestCase(BuildingType.Bakery, 100, 65)]
        [TestCase(BuildingType.Smokehouse, 150, 100)]
        [TestCase(BuildingType.Stonecutter, 150, 100)]
        public void Workshop_Grade1_FasterCycle_OutputPerMinuteWithInputsStocked(BuildingType type, int cycle0, int cycle1)
        {
            var def = BuildingDefs.Get(type);
            int perCycle = def.Outputs.Sum();
            var g0 = Measure(type, Grade.Prehistoric);
            var g1 = Measure(type, Grade.Historic);
            TestContext.WriteLine(type + ": " + PerMinute(g0.output) + " -> " + PerMinute(g1.output) + " " + string.Join("+", Enumerable.Range(0, Wares.Count).Where(w => def.Outputs[w] > 0).Select(w => Wares.DisplayName((Ware)w)))
                + " per minute (cycle " + cycle0 + " -> " + cycle1 + ")");
            Assert.AreEqual(Window / cycle0 * perCycle, g0.output, perCycle);
            Assert.AreEqual(Window / cycle1 * perCycle, g1.output, perCycle);
            Assert.AreEqual((double)cycle0 / cycle1, (double)g1.output / g0.output, 0.06);
        }

        [Test]
        public void Sawmill_Grade1_KeepsThreeCyclesOfWoodOnHand()
        {
            int Peak(Grade g)
            {
                var r = Make(BuildingType.Sawmill, g);
                int peak = 0;
                for (int i = 0; i < 1200; i++)
                {
                    r.Sim.Step();
                    r.Sim.DrainEvents();
                    peak = System.Math.Max(peak, r.B.Delivered[(int)Ware.Wood]);
                }
                return peak;
            }
            Assert.AreEqual(2, Peak(Grade.Prehistoric), "two cycles of wood on hand");
            Assert.AreEqual(3, Peak(Grade.Historic), "three cycles of wood on hand");
        }

        [Test]
        public void WarriorLodge_Grade1_BecomesBarracks_NoCapOfThree_RecruitEveryTwoHundredTicks()
        {
            var g0 = Measure(BuildingType.WarriorLodge, Grade.Prehistoric);
            var g1 = Measure(BuildingType.WarriorLodge, Grade.Historic);
            TestContext.WriteLine("Warrior lodge: " + g0.output + " -> " + g1.output + " soldiers per 10 minutes (cap of 3 gone, 300 -> 200 ticks)");
            Assert.AreEqual(3, g0.output, "a lodge stops at its three warriors");
            Assert.AreEqual(Window / 200, g1.output, 1, "a barracks keeps recruiting every 200 ticks");
        }

        [Test]
        public void EveryGradeRow_HasABenefitSentence_AndANumberTheSystemsRead()
        {
            foreach (var def in BuildingDefs.All)
            {
                var g1 = def.Grades[1];
                Assert.IsFalse(string.IsNullOrEmpty(g1.Benefit), def.Name);
                Assert.AreEqual(Grade.Historic, g1.To);
                Assert.GreaterOrEqual(g1.Ticks, 100);
                Assert.IsTrue(g1.CycleTicks > 0 || g1.YieldPerUnit > 0 || g1.InputBufferCycles > 0, def.Name + " has a measurable effect");
            }
        }

        // ---- all 16 types through the real flow ----

        [Test]
        public void AllSixteenTypes_RealUpgradeFlow_AppliesTheGradeTable()
        {
            for (int ti = 0; ti < BuildingDefs.Count; ti++)
            {
                var type = (BuildingType)ti;
                var def = BuildingDefs.Get(type);
                var r = Make(type, Grade.Prehistoric);
                UpgradeTests.GiveGrade(r.S, r.Camp.Id, type);
                if (def.Class == BuildingClass.Lodge) { r.S.Economy.Tuning.WarriorsPerLodge = 3; }
                var ev = K.Do(r.Sim, new UpgradeBuilding { BuildingId = r.B.Id });
                Assert.AreEqual(Grade.Historic, ev.OfType<UpgradeStarted>().Single().Grade, type.ToString());
                Assert.IsTrue(K.StepUntil(r.Sim, () => !r.B.Upgrading, 6000), type + " finished");
                Assert.AreEqual(Grade.Historic, r.B.Grade, type.ToString());
                var g = def.Grades[1];
                if (g.CycleTicks > 0) Assert.AreEqual(g.CycleTicks, GradeApi.CycleTicks(r.S, r.B.Id), type + " cycle");
                if (g.YieldPerUnit > 0) Assert.AreEqual(g.YieldPerUnit, GradeApi.YieldPerUnit(r.S, r.B.Id), type + " yield");
                for (int w = 0; w < Wares.Count; w++) Assert.AreEqual(0, r.B.UpgradeDelivered[w], type + " consumed " + (Ware)w);
                // It keeps working afterwards.
                K.Step(r.Sim, 600);
                Assert.IsNull(K.CheckInvariants(r.S), type.ToString());
                Assert.AreEqual(r.B.Plot, r.S.Economy.Buildings[r.B.Id].Plot);
            }
        }

        // ---- the town ----

        [Test]
        public void Town_Benefits_TwoMoreCarriers_ShipmentsOfSix_SixOuterPlots()
        {
            int ShipmentOf(bool town)
            {
                var sim = UpgradeTests.Historic();
                var s = sim.State;
                int camp = K.SecondCamp(sim, out _);
                var home = s.Economy.Settlements[0];
                home.IsCity = true;
                home.IsTown = town;
                home.Stock[(int)Ware.Wood] = 60;
                s.Economy.Ledger.Starting[(int)Ware.Wood] += 60;
                var c = s.Economy.Settlements[camp];
                c.Stock[(int)Ware.Wood] = 0;
                // Four building sites at the camp ask for eight wood in all.
                for (int i = 0; i < 4; i++)
                    s.Economy.Buildings.Add(new Building { Id = s.Economy.Buildings.Count, Type = BuildingType.Sawmill, Faction = 0, Settlement = camp, Plot = PlotLayout.PlotId(c.Corner, 0, 1), Hex = PlotLayout.PlotHex(s.Board.Topology, PlotLayout.PlotId(c.Corner, 0, 1)), State = BuildingState.Site });
                foreach (var j in new List<Job>(s.Economy.Jobs)) Logistics.Release(s, j);
                Logistics.CreateDemand(s, null, null);
                return s.Economy.Jobs.Where(j => j.IsShipment && j.ToKind == PlaceKind.Storehouse && j.ToId == camp && j.Ware == Ware.Wood).Max(j => j.Count);
            }
            int city = ShipmentOf(false), town = ShipmentOf(true);
            TestContext.WriteLine("Town: shipment of the biggest job " + city + " -> " + town + " units; carriers +2; outer plots +6");
            Assert.AreEqual(4, city);
            Assert.AreEqual(6, town);

            var sim2 = UpgradeTests.Historic();
            var s2 = sim2.State;
            var h = s2.Economy.Settlements[0];
            h.IsCity = true;
            int plotsBefore = EconomyApi.PlotsOf(s2, 0).Length;
            int carriersBefore = s2.Economy.Carriers.Count(x => x.Home == 0);
            var tu = s2.Economy.Tuning;
            for (int w = 0; w < Wares.Count; w++) if (tu.TownCost[w] > 0) K.Give(s2, 0, (Ware)w, tu.TownCost[w]);
            long started = s2.Tick; K.Do(sim2, new UpgradeSettlement { Corner = h.Corner });
            Assert.IsTrue(K.StepUntil(sim2, () => h.IsTown, 3000));
            Assert.AreEqual(carriersBefore + tu.CarriersPerTown, s2.Economy.Carriers.Count(x => x.Home == 0));
            Assert.AreEqual(plotsBefore + 6, EconomyApi.PlotsOf(s2, 0).Length);
            Assert.AreEqual(tu.TownShipmentSize, GradeLogic.ShipmentSize(tu, h));
            Assert.AreEqual(tu.ShipmentSize, GradeLogic.ShipmentSize(tu, new Settlement()));
            Assert.GreaterOrEqual(h.TownTick - started, tu.TownBuildTicks, "built for the town's 600 ticks");
        }
    }
}
