using System;
using System.Linq;
using NUnit.Framework;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Jobs, carriers, shipments and positions (REFACTOR_SPEC 2.B.1).</summary>
    public class LogisticsTests
    {
        [Test]
        public void HutSite_TwoWood_CarriedOneUnitPerLocalTrip()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var ev = K.Do(sim, new PlaceBuilding { Type = BuildingType.WoodcutterHut, Plot = 631 });
            int hut = s.Economy.Buildings.Single(b => b.Plot == 631).Id;
            var jobs = ev.OfType<JobCreated>().Where(j => j.ToKind == PlaceKind.Building && j.ToId == hut).ToList();
            Assert.AreEqual(2, jobs.Count);
            Assert.IsTrue(jobs.All(j => j.Ware == Ware.Wood && j.Count == 1 && !j.IsShipment && j.FromKind == PlaceKind.Storehouse));
            Assert.AreEqual(0, s.Economy.Settlements[0].Reserved[(int)Ware.Wood], "picked up at once: the carriers were at the storehouse");
            Assert.AreEqual(2, s.Economy.Buildings[hut].Incoming[(int)Ware.Wood]);
            var legs = ev.OfType<CarrierLegStarted>().Where(l => l.ToKind == PlaceKind.Building && l.ToId == hut).ToList();
            Assert.AreEqual(2, legs.Count);
            Assert.AreEqual(8 * 15, legs[0].LegTicks, "slot 1: 8 steps of 15 ticks");
            Assert.AreEqual(2, ev.OfType<WarePickedUp>().Count(p => jobs.Any(j => j.JobId == p.JobId)));
            ev = K.Step(sim, 120);
            Assert.AreEqual(2, ev.OfType<WareDelivered>().Count(d => d.ToKind == PlaceKind.Building && d.ToId == hut));
            Assert.AreEqual(BuildingState.Constructing, s.Economy.Buildings[hut].State);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void CollectorOutput_FetchedToTheStorehouse()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var pile = s.Economy.Buildings.Single(b => b.Plot == 630);
            int wood = s.Economy.Settlements[0].Stock[(int)Ware.Wood];
            var ev = K.Step(sim, 100); // first cycle ends at tick 100
            var job = ev.OfType<JobCreated>().Single(j => j.FromKind == PlaceKind.Building && j.FromId == pile.Id);
            Assert.AreEqual(PlaceKind.Storehouse, job.ToKind);
            ev = K.Step(sim, 240);
            Assert.IsTrue(ev.OfType<WareDelivered>().Any(d => d.JobId == job.JobId && d.ToKind == PlaceKind.Storehouse));
            Assert.Greater(s.Economy.Settlements[0].Stock[(int)Ware.Wood], wood);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void CarrierPosition_FollowsTheLeg()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var t = s.Board.Topology;
            int home = s.Board.StartCorners[0];
            Assert.IsTrue(EconomyApi.CarrierPosition(s, 0, 0, out double x, out double z, out _));
            Assert.AreEqual(t.VertexX(home), x, 1e-9, "idle at the storehouse corner");
            Assert.AreEqual(t.VertexZ(home), z, 1e-9);
            Assert.IsFalse(EconomyApi.CarrierPosition(s, 999, 0, out _, out _, out _));

            K.Do(sim, new PlaceBuilding { Type = BuildingType.WoodcutterHut, Plot = 631 });
            sim.Step();
            sim.DrainEvents();
            var c = s.Economy.Carriers.First(k => k.State == CarrierState.Carrying && k.LegToKind == PlaceKind.Building);
            EconomyApi.CarrierPosition(s, c.Id, 0, out double x0, out double z0, out _);
            double prevD = -1;
            while (c.State == CarrierState.Carrying && c.Progress < c.LegTicks - 1)
            {
                EconomyApi.CarrierPosition(s, c.Id, 0.5, out double xi, out double zi, out double yaw);
                double d = Math.Sqrt(Math.Pow(xi - t.VertexX(home), 2) + Math.Pow(zi - t.VertexZ(home), 2));
                Assert.GreaterOrEqual(d, prevD - 1e-9, "walks away from the storehouse");
                prevD = d;
                sim.Step();
                sim.DrainEvents();
            }
            Assert.AreEqual(CarrierState.Carrying, c.State);
            EconomyApi.CarrierPosition(s, c.Id, 1, out double x1, out double z1, out _);
            Assert.AreEqual(PlotLayout.PlotX(t, 631), x1, 0.05);
            Assert.AreEqual(PlotLayout.PlotZ(t, 631), z1, 0.05);
        }

        [Test]
        public void Shipment_ImportsAWareTheCampLacks_FromAConnectedCamp()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int camp = K.SecondCamp(sim, out var path);
            var st = s.Economy.Settlements[camp];
            for (int w = 0; w < Wares.Count; w++) Assert.GreaterOrEqual(st.Stock[w], 0);
            // A workshop site at the new camp needs goods it does not hold: they come as shipments from home.
            K.Give(s, 0, Ware.Wood, 4);
            K.Give(s, 0, Ware.Clay, 2);
            int plot = EconomyApi.PlotsOf(s, camp).First(p => EconomyApi.CheckBuilding(s, 0, BuildingType.Kiln, p).Ok);
            var ev = K.Do(sim, new PlaceBuilding { Type = BuildingType.Kiln, Plot = plot });
            ev.AddRange(K.Step(sim, 5));
            var ship = ev.OfType<JobCreated>().Where(j => j.IsShipment && j.ToId == camp).ToList();
            Assert.IsNotEmpty(ship, "imports created");
            Assert.IsTrue(ship.All(j => j.FromKind == PlaceKind.Storehouse && j.FromId == 0 && j.Count <= 4));
            Assert.IsTrue(K.StepUntil(sim, () => s.Economy.Carriers.Any(c => c.Home != camp && c.LegToKind == PlaceKind.Storehouse && c.LegToId == camp && c.HasLoad), 3000));
            var leg = s.Economy.Carriers.First(c => c.Home != camp && c.LegToKind == PlaceKind.Storehouse && c.LegToId == camp && c.HasLoad);
            Assert.AreEqual((path.Length - 1) * 12 * 15, leg.LegTicks, "two hex edges at 180 ticks each");
            Assert.AreEqual(path, leg.LegCorners);
            int kiln = s.Economy.Buildings.Single(b => b.Plot == plot).Id;
            Assert.IsTrue(K.StepUntil(sim, () => s.Economy.Buildings[kiln].State != BuildingState.Site, 2000),
                "no camp waits forever for an import that exists on a connected road");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void DemonOnTheOnlyRoad_ShipmentWaits_ThenArrivesAfterBanish()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var t = s.Board.Topology;
            int camp = K.SecondCamp(sim, out var path);
            K.Give(s, 0, Ware.Bricks, 4);
            int plot = EconomyApi.PlotsOf(s, camp).First(p => EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, p).Ok);
            // Something at the camp that needs bricks: a lodge site (2 Planks + 2 Bricks).
            K.Give(s, 0, Ware.Planks, 2);
            K.Do(sim, new PlaceBuilding { Type = BuildingType.WarriorLodge, Plot = plot });
            Assert.IsTrue(K.StepUntil(sim, () => s.Economy.Carriers.Any(c => c.HasLoad && c.Load == Ware.Bricks && c.LegCorners.Length > 1), 4000));
            var carrier = s.Economy.Carriers.First(c => c.HasLoad && c.Load == Ware.Bricks && c.LegCorners.Length > 1);
            int jobId = carrier.JobId;
            int edge = t.HexEdgeBetween(path[1], path[2]);
            s.Demons.Edge[edge] = 1;
            K.Step(sim, 600);
            Assert.IsTrue(s.Economy.Jobs.Any(j => j.Id == jobId), "the shipment waits at the demon");
            Assert.AreEqual(Reasons.DemonOnRoute, s.Economy.Jobs.Single(j => j.Id == jobId).Reason);
            Assert.IsTrue(sim.Submit(new Banish { PlayerId = 0, SiteKind = DemonSiteKind.HexEdge, SiteId = edge }).Ok);
            var ev = K.Step(sim, 400);
            Assert.IsTrue(ev.OfType<WareDelivered>().Any(d => d.JobId == jobId), "it arrives after the banish");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void HaltedCamp_SendsNoShipments()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int camp = K.SecondCamp(sim, out _);
            s.Demons.Corner[s.Board.StartCorners[0]] = 1;
            K.Give(s, 0, Ware.Planks, 2);
            K.Give(s, 0, Ware.Bricks, 2);
            int plot = EconomyApi.PlotsOf(s, camp).First(p => EconomyApi.CheckBuilding(s, 0, BuildingType.WarriorLodge, p).Ok);
            var ev = K.Do(sim, new PlaceBuilding { Type = BuildingType.WarriorLodge, Plot = plot });
            ev.AddRange(K.Step(sim, 300));
            Assert.IsFalse(ev.OfType<JobCreated>().Any(j => j.IsShipment && j.FromId == 0), "demon-held home sends nothing");
            s.Demons.Corner[s.Board.StartCorners[0]] = 0;
            ev = K.Step(sim, 5);
            Assert.IsTrue(ev.OfType<JobCreated>().Any(j => j.IsShipment && j.FromId == 0 && j.ToId == camp));
        }

        [Test]
        public void Cancel_GoodsInFlightComeHome()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            K.Do(sim, new PlaceBuilding { Type = BuildingType.WoodcutterHut, Plot = 631 });
            int hut = s.Economy.Buildings.Single(b => b.Plot == 631).Id;
            K.Step(sim, 30);
            Assert.IsTrue(s.Economy.Carriers.Any(c => c.HasLoad && c.LegToKind == PlaceKind.Building && c.LegToId == hut));
            K.Do(sim, new CancelSite { Kind = SiteKind.Building, Id = hut });
            Assert.IsNull(K.CheckInvariants(s));
            int wood = s.Economy.Settlements[0].Stock[(int)Ware.Wood];
            K.Step(sim, 400);
            Assert.IsFalse(s.Economy.Carriers.Any(c => c.HasLoad && c.Load == Ware.Wood && c.LegToKind == PlaceKind.Building && c.LegToId == hut));
            Assert.AreEqual(BuildingState.Cancelled, s.Economy.Buildings[hut].State);
            Assert.IsNull(K.CheckInvariants(s));
            Assert.GreaterOrEqual(s.Economy.Settlements[0].Stock[(int)Ware.Wood], wood + 2, "both units are back");
        }

        [Test]
        public void Workshop_KeepsTwoCyclesOfInputs()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            K.Give(s, 0, Ware.Wood, 10);
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Plot = 631 });
            int mill = s.Economy.Buildings.Single(b => b.Plot == 631).Id;
            Assert.IsTrue(K.StepUntil(sim, () => s.Economy.Buildings[mill].State == BuildingState.Active, 1000));
            K.Step(sim, 5);
            var b = s.Economy.Buildings[mill];
            Assert.LessOrEqual(b.Delivered[(int)Ware.Wood] + b.Incoming[(int)Ware.Wood], 2 + 1);
            var ev = K.Step(sim, 500);
            Assert.IsTrue(ev.OfType<ProductionCycle>().Any(p => p.BuildingId == mill && p.Ware == Ware.Planks));
            Assert.LessOrEqual(b.Delivered[(int)Ware.Wood] + b.Incoming[(int)Ware.Wood], 2);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Carriers_PerSettlement_GrowWithBuildings()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            Assert.AreEqual(3, s.Economy.Carriers.Count(c => c.Home == 0));
            K.Do(sim, new PlaceBuilding { Type = BuildingType.WoodcutterHut, Plot = 631 });
            Assert.IsTrue(K.StepUntil(sim, () => s.Economy.Buildings.Single(b => b.Plot == 631).State == BuildingState.Active, 1000));
            Assert.AreEqual(4, s.Economy.Carriers.Count(c => c.Home == 0));
            Assert.IsTrue(s.Economy.Carriers.All(c => c.Faction == s.Economy.Settlements[c.Home].Faction));
            var idle = s.Economy.Carriers.FirstOrDefault(c => c.Home == 0 && c.State == CarrierState.Idle);
            if (idle != null) Assert.AreEqual("A carrier of the home camp, waiting at the storehouse.", EconomyApi.DescribeCarrier(s, idle.Id));
            var busy = s.Economy.Carriers.FirstOrDefault(c => c.JobId >= 0);
            if (busy != null) StringAssert.StartsWith("A carrier of ", EconomyApi.DescribeCarrier(s, busy.Id));
        }
    }
}
