using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Save;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// Grade 1 upgrades, City to Town, outer plots in placement and the campaign capture seam (PRD_AGES 5.6, 5.3, 4.3; package H1-2).
    /// These set the era to Historic directly (the handover and the chapter step belong to H1-1) and keep random demons away.
    /// </summary>
    public class UpgradeTests
    {
        internal static WorldSim Historic(ulong seed = K.DefaultSeed)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            sim.State.Eras.Current = Era.Historic;
            sim.State.Demons.Tuning.GraceTicks = int.MaxValue;
            // H1-1: worked goods moved into an upgrade are a handover (PRD_AGES 4.2), after which the prince builds on his own.
            // These tests want the player's people quiet: mark the milestone as passed without autonomy.
            sim.State.Factions.Factions[0].HandoverTick = 0;
            return sim;
        }

        /// <summary>Gives the goods, places the building, waits for it to be active.</summary>
        internal static Building BuildActive(WorldSim sim, BuildingType type, int plot, int settlement = 0)
        {
            var s = sim.State;
            var def = BuildingDefs.Get(type);
            for (int w = 0; w < Wares.Count; w++) if (def.Cost[w] > 0) K.Give(s, settlement, (Ware)w, def.Cost[w]);
            int before = s.Economy.Buildings.Count;
            K.Do(sim, new PlaceBuilding { Type = type, Plot = plot });
            var b = s.Economy.Buildings[before];
            Assert.IsTrue(K.StepUntil(sim, () => b.State == BuildingState.Active, 3000), type + " built");
            return b;
        }

        internal static void GiveGrade(WorldState s, int settlement, BuildingType type)
        {
            var g = BuildingDefs.Get(type).Grades[1];
            for (int w = 0; w < Wares.Count; w++) if (g.Goods[w] > 0) K.Give(s, settlement, (Ware)w, g.Goods[w]);
        }

        private static List<SimEvent> RunUntil(WorldSim sim, System.Func<bool> done, int max)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < max && !done(); i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
            }
            return all;
        }

        // ---- grade 1 ----

        [Test]
        public void UpgradeBuilding_HappyPath_GoodsComeByCarriers_ThenGradeRises()
        {
            var sim = Historic();
            var s = sim.State;
            var b = BuildActive(sim, BuildingType.Sawmill, 631);
            int id = b.Id;
            Assert.AreEqual(Grade.Prehistoric, b.Grade);
            Assert.AreEqual(100, GradeApi.CycleTicks(s, id));
            GiveGrade(s, 0, BuildingType.Sawmill); // 2 planks, 2 bricks
            var led = s.Economy.Ledger;
            int constructionPlanks = led.Construction[(int)Ware.Planks], constructionBricks = led.Construction[(int)Ware.Bricks];

            var ev = K.Do(sim, new UpgradeBuilding { BuildingId = id });
            var started = ev.OfType<UpgradeStarted>().Single();
            Assert.AreEqual((id, Grade.Historic), (started.BuildingId, started.Grade));
            Assert.IsTrue(b.Upgrading);
            Assert.AreEqual(Grade.Prehistoric, b.Grade, "still the old grade until it is finished");

            var after = RunUntil(sim, () => !b.Upgrading, 4000);
            Assert.IsFalse(b.Upgrading);
            Assert.AreEqual(Grade.Historic, b.Grade);
            var done = after.OfType<BuildingUpgraded>().Single();
            Assert.AreEqual((id, Grade.Historic), (done.BuildingId, done.Grade));
            var delivered = after.OfType<WareDelivered>().Where(d => d.ToKind == PlaceKind.BuildingUpgrade && d.ToId == id).ToList();
            Assert.AreEqual(4, delivered.Sum(d => d.Count), "2 planks and 2 bricks, each unit carried by a carrier");
            Assert.AreEqual(2, delivered.Where(d => d.Ware == Ware.Planks).Sum(d => d.Count));
            Assert.AreEqual(2, delivered.Where(d => d.Ware == Ware.Bricks).Sum(d => d.Count));
            Assert.IsTrue(delivered.All(d => d.CarrierId >= 0));
            Assert.AreEqual(constructionPlanks + 2, led.Construction[(int)Ware.Planks]);
            Assert.AreEqual(constructionBricks + 2, led.Construction[(int)Ware.Bricks]);
            CollectionAssert.AreEqual(new int[Wares.Count], b.UpgradeDelivered, "all of it consumed");
            CollectionAssert.AreEqual(new int[Wares.Count], b.UpgradeIncoming);
            Assert.AreEqual(65, GradeApi.CycleTicks(s, id));
            Assert.AreEqual(3, GradeApi.InputBufferCycles(s, id));
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void UpgradeBuilding_KeepsIdentityAndInventory_AndStartsNoNewCycle()
        {
            var sim = Historic();
            var s = sim.State;
            var b = BuildActive(sim, BuildingType.Sawmill, 631);
            K.Give(s, 0, Ware.Wood, 6);
            Assert.IsTrue(K.StepUntil(sim, () => b.Delivered[(int)Ware.Wood] >= 2, 1500), "inputs on hand");
            GiveGrade(s, 0, BuildingType.Sawmill);
            var identity = (b.Id, b.Type, b.Faction, b.Settlement, b.Plot, b.Hex, b.State);
            int inputsBefore = b.Delivered[(int)Ware.Wood];
            K.Do(sim, new UpgradeBuilding { BuildingId = b.Id });
            int minInputs = inputsBefore;
            var all = new List<SimEvent>();
            for (int i = 0; i < 4000 && b.Upgrading; i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
                if (b.Upgrading) minInputs = System.Math.Min(minInputs, b.Delivered[(int)Ware.Wood]);
            }
            Assert.AreEqual(Grade.Historic, b.Grade);
            Assert.AreEqual(identity, (b.Id, b.Type, b.Faction, b.Settlement, b.Plot, b.Hex, b.State), "same building, same plot");
            Assert.AreSame(b, s.Economy.Buildings[identity.Id]);
            Assert.GreaterOrEqual(minInputs, inputsBefore, "the inputs it holds stay put while it is improved");
            Assert.AreEqual(0, all.OfType<ProductionStarted>().Count(e => e.BuildingId == b.Id), "no cycle starts during the upgrade");
            Assert.IsNull(K.CheckInvariants(s));
            // And it works again afterwards, faster.
            var later = K.Step(sim, 400);
            Assert.Greater(later.OfType<ProductionStarted>().Count(e => e.BuildingId == b.Id), 0);
        }

        [Test]
        public void CancelUpgrade_ReturnsDeliveredGoods_AndOpenJobsEnd()
        {
            var sim = Historic();
            var s = sim.State;
            var b = BuildActive(sim, BuildingType.Sawmill, 631);
            GiveGrade(s, 0, BuildingType.Sawmill);
            int bricksBefore = s.Economy.Settlements[0].Stock[(int)Ware.Bricks];
            K.Do(sim, new UpgradeBuilding { BuildingId = b.Id });
            Assert.IsTrue(K.StepUntil(sim, () => b.UpgradeDelivered.Sum() >= 1, 600), "some goods have arrived");
            Assert.IsTrue(s.Economy.Jobs.Any(j => j.ToKind == PlaceKind.BuildingUpgrade), "and more are on the way");
            var ev = K.Do(sim, new CancelUpgrade { BuildingId = b.Id });
            Assert.AreEqual(b.Id, ev.OfType<UpgradeCancelled>().Single().BuildingId);
            Assert.IsFalse(b.Upgrading);
            Assert.AreEqual(Grade.Prehistoric, b.Grade);
            CollectionAssert.AreEqual(new int[Wares.Count], b.UpgradeDelivered);
            K.Step(sim, 1500);
            CollectionAssert.AreEqual(new int[Wares.Count], b.UpgradeIncoming, "nothing still heads for the cancelled upgrade");
            Assert.IsFalse(s.Economy.Jobs.Any(j => j.ToKind == PlaceKind.BuildingUpgrade));
            Assert.AreEqual(bricksBefore, s.Economy.Settlements[0].Stock[(int)Ware.Bricks], "the bricks are all back in the storehouse");
            Assert.AreEqual(0, s.Economy.Carriers.Count(c => c.HasLoad && c.Load == Ware.Bricks));
            Assert.IsNull(K.CheckInvariants(s));
            // The building works as before and may start the upgrade again.
            Assert.IsTrue(sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = b.Id }).Ok);
        }

        [Test]
        public void UpgradeBuilding_Refusals_WrongEra_MaxGrade_NotActive_InProgress()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed); // Prehistoric
            var s = sim.State;
            s.Demons.Tuning.GraceTicks = int.MaxValue;
            K.Give(s, 0, Ware.Wood, 2);
            K.Give(s, 0, Ware.Clay, 1);
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Plot = 631 });
            var site = s.Economy.Buildings.Last();
            var home = s.Economy.Buildings.First(x => x.Faction == 0 && x.State == BuildingState.Active);
            var r = sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = home.Id });
            Assert.AreEqual("wrong_era", r.Reason);
            Assert.AreEqual("That needs the Historic age.", r.Message);

            s.Eras.Current = Era.Historic;
            Assert.AreEqual("not_active", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = site.Id }).Reason);
            Assert.AreEqual("It must be finished first.", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = site.Id }).Message);
            Assert.IsTrue(sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = home.Id }).Ok);

            K.Do(sim, new UpgradeBuilding { BuildingId = home.Id });
            r = sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = home.Id });
            Assert.AreEqual("upgrade_in_progress", r.Reason);
            Assert.AreEqual("That building is already being improved.", r.Message);
            Assert.IsTrue(sim.Check(new CancelUpgrade { PlayerId = 0, BuildingId = home.Id }).Ok);
            Assert.AreEqual("not_owner", sim.Check(new CancelUpgrade { PlayerId = 1, BuildingId = home.Id }).Reason);
            K.Do(sim, new CancelUpgrade { BuildingId = home.Id });
            Assert.AreEqual("not_upgrading", sim.Check(new CancelUpgrade { PlayerId = 0, BuildingId = home.Id }).Reason);

            // A grade 2 building cannot be improved again; grade 1 to 2 is credits from Modern (M1-2), still unavailable here.
            home.Grade = Grade.Modern;
            Assert.AreEqual("max_grade", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = home.Id }).Reason);
            home.Grade = Grade.Historic;
            Assert.AreEqual("wrong_era", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = home.Id }).Reason, "grade 2 needs the Modern age");
            s.Eras.Current = Era.Modern;
            Assert.AreEqual(Reasons.Disabled, sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = home.Id }).Reason, "credits are M1-2");
        }

        [Test]
        public void SaveLoad_MidUpgrade_DeliversOnce_AndBothRunsAgree()
        {
            var sim = Historic();
            var s = sim.State;
            var b = BuildActive(sim, BuildingType.Sawmill, 631);
            GiveGrade(s, 0, BuildingType.Sawmill);
            int constructionPlanks = s.Economy.Ledger.Construction[(int)Ware.Planks];
            K.Do(sim, new UpgradeBuilding { BuildingId = b.Id });
            Assert.IsTrue(K.StepUntil(sim, () => b.UpgradeDelivered.Sum() >= 2 && b.UpgradeDelivered.Sum() < 4, 800), "caught half way");
            string json = SaveCodec.ToJson(s);
            var loaded = SaveCodec.Load(json, out _);
            var lb = loaded.State.Economy.Buildings[b.Id];
            Assert.IsTrue(lb.Upgrading);
            CollectionAssert.AreEqual(b.UpgradeDelivered, lb.UpgradeDelivered);
            CollectionAssert.AreEqual(b.UpgradeIncoming, lb.UpgradeIncoming);
            Assert.AreEqual(b.UpgradeProgress, lb.UpgradeProgress);
            K.Step(sim, 2500);
            K.Step(loaded, 2500);
            Assert.AreEqual(Grade.Historic, b.Grade);
            Assert.AreEqual(Grade.Historic, lb.Grade);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(loaded.State), "the loaded game goes on exactly like the original");
            Assert.AreEqual(constructionPlanks + 2, loaded.State.Economy.Ledger.Construction[(int)Ware.Planks], "the planks were spent once");
            Assert.IsNull(K.CheckInvariants(loaded.State));
        }

        [Test]
        public void CycleBonusPermille_DefaultsToZero_ShortensCycles_AndIsSaved()
        {
            var sim = Historic();
            var s = sim.State;
            var tu = s.Economy.Tuning;
            Assert.AreEqual(BuildingDefs.Count, tu.CycleBonusPermille.Length);
            Assert.IsTrue(tu.CycleBonusPermille.All(x => x == 0), "off by default");
            var logPile = s.Economy.Buildings.First(x => x.Faction == 0 && x.Type == BuildingType.LogPile);
            Assert.AreEqual(100, GradeApi.CycleTicks(s, logPile.Id));
            tu.CycleBonusPermille[(int)BuildingType.LogPile] = 250;
            Assert.AreEqual(75, GradeApi.CycleTicks(s, logPile.Id));
            logPile.Grade = Grade.Historic;
            Assert.AreEqual(52, GradeApi.CycleTicks(s, logPile.Id), "70 ticks less a quarter");
            tu.CycleBonusPermille[(int)BuildingType.LogPile] = 5000;
            Assert.AreEqual(7, GradeApi.CycleTicks(s, logPile.Id), "clamped at 900 permille");
            tu.CycleBonusPermille[(int)BuildingType.LogPile] = 250;
            var loaded = SaveCodec.Load(SaveCodec.ToJson(s), out _);
            Assert.AreEqual(250, loaded.State.Economy.Tuning.CycleBonusPermille[(int)BuildingType.LogPile]);
            Assert.AreEqual(52, GradeApi.CycleTicks(loaded.State, logPile.Id));
        }

        // ---- City to Town ----

        private static Settlement HomeAsCity(WorldSim sim)
        {
            var s = sim.State;
            var home = s.Economy.Settlements[0];
            home.IsCity = true; // the city step itself is SettlementTests' business
            return home;
        }

        [Test]
        public void UpgradeSettlement_CityToTown_FullFlow_UnlocksSixOuterPlots()
        {
            var sim = Historic();
            var s = sim.State;
            var home = s.Economy.Settlements[0];
            int outerPlot = PlotLayoutOuter.OuterPlotId(home.Corner, 0, 3);
            Assert.AreEqual("not_a_city", sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = home.Corner }).Reason);
            home.IsCity = true;
            var locked = EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, outerPlot);
            Assert.AreEqual("plot_locked", locked.Reason);
            Assert.AreEqual("That plot opens when the city becomes a town.", locked.Message);
            Assert.AreEqual(PlotUse.Locked, EconomyApi.Plot(s, outerPlot).Use);
            int plotsAsCity = EconomyApi.PlotsOf(s, 0).Length;

            var tu = s.Economy.Tuning;
            for (int w = 0; w < Wares.Count; w++) if (tu.TownCost[w] > 0) K.Give(s, 0, (Ware)w, tu.TownCost[w]);
            int carriersBefore = s.Economy.Carriers.Count(c => c.Home == 0);
            var ev = K.Do(sim, new UpgradeSettlement { Corner = home.Corner });
            var site = ev.OfType<SitePlaced>().Single();
            Assert.AreEqual((SiteKind.City, 0), (site.Kind, site.Id));
            Assert.AreNotEqual(UpgradeState.None, home.Upgrade, "goods move in at once, then it builds");
            var again = sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = home.Corner });
            Assert.AreEqual("upgrade_in_progress", again.Reason);
            Assert.AreEqual("This city is already being raised to a town.", again.Message);

            var all = RunUntil(sim, () => home.IsTown, 3000);
            Assert.IsTrue(home.IsTown);
            var up = all.OfType<TownUpgraded>().Single();
            Assert.AreEqual((0, home.Corner, 0), (up.SettlementId, up.Corner, up.Faction));
            Assert.AreEqual(UpgradeState.None, home.Upgrade);
            Assert.AreEqual(home.Id, all.OfType<SiteCompleted>().Single(e => e.Kind == SiteKind.City).Id);
            Assert.AreEqual(carriersBefore + 2, s.Economy.Carriers.Count(c => c.Home == 0), "two more carriers");
            Assert.GreaterOrEqual(home.TownTick, 600, "it took the town's 600 build ticks");
            Assert.AreEqual(UpgradeState.None, home.Upgrade);
            Assert.IsNull(K.CheckInvariants(s));

            // Six outer plots are open.
            var plots = EconomyApi.PlotsOf(s, 0);
            Assert.AreEqual(plotsAsCity + 6, plots.Length);
            Assert.AreEqual(6, plots.Count(PlotLayoutOuter.IsOuter));
            var view = EconomyApi.Plot(s, outerPlot);
            Assert.AreEqual((PlotUse.Free, home.Corner, 0, 3), (view.Use, view.Corner, view.Sector, view.Slot));
            Assert.AreEqual(PlotLayout.PlotX(s.Board.Topology, outerPlot), view.X, 1e-9);
            Assert.AreEqual(PlotLayout.PlotHex(s.Board.Topology, outerPlot), view.Hex);
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, outerPlot).Ok);

            // A building on an outer plot is placed, built (carriers walk the longer path) and counted like any other.
            var b = BuildActive(sim, BuildingType.Sawmill, outerPlot);
            Assert.AreEqual(outerPlot, b.Plot);
            Assert.AreEqual(PlotLayout.PlotHex(s.Board.Topology, outerPlot), b.Hex);
            Assert.AreEqual(PlotUse.Building, EconomyApi.Plot(s, outerPlot).Use);
            Assert.AreEqual(PlotTakenSentence, EconomyApi.CheckBuilding(s, 0, BuildingType.Kiln, outerPlot).Message);
            Assert.AreEqual(13, PlotLayout.LocalSteps(b.Plot));
            Assert.IsNull(K.CheckInvariants(s));

            // Already a town: Town to District is M1-2.
            Assert.AreEqual("wrong_era", sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = home.Corner }).Reason, "Town to District needs the Modern age");
            s.Eras.Current = Era.Modern;
            Assert.AreEqual(Reasons.Disabled, sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = home.Corner }).Reason, "and its credits are M1-2");
        }

        private const string PlotTakenSentence = "Something already stands on that plot.";

        [Test]
        public void UpgradeSettlement_Refusals_WrongEra_AndCancelReturnsGoods()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var home = HomeAsCity(sim);
            var r = sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = home.Corner });
            Assert.AreEqual("wrong_era", r.Reason);
            Assert.AreEqual("That needs the Historic age.", r.Message);
            s.Eras.Current = Era.Historic;
            Assert.IsTrue(sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = home.Corner }).Ok);
            Assert.AreEqual("not_a_settlement", sim.Check(new UpgradeSettlement { PlayerId = 1, Corner = home.Corner }).Reason, "not their camp");
            K.Give(s, 0, Ware.Planks, 4);
            K.Do(sim, new UpgradeSettlement { Corner = home.Corner });
            K.Step(sim, 5);
            Assert.AreEqual(4, home.UpgradeDelivered[(int)Ware.Planks], "planks move from the storehouse into the upgrade");
            Assert.AreEqual(0, home.Stock[(int)Ware.Planks]);
            K.Do(sim, new CancelSite { Kind = SiteKind.City, Id = 0 });
            Assert.AreEqual(UpgradeState.None, home.Upgrade);
            Assert.IsFalse(home.IsTown);
            Assert.AreEqual(4, home.Stock[(int)Ware.Planks], "returned");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void OuterPlots_NeighbouringTownsKeepTheGroundFirstComeFirstServed()
        {
            var sim = Historic();
            var s = sim.State;
            var t = s.Board.Topology;
            // Two camps two edges apart whose facing outer plots clash.
            int a = -1, b = -1, pa = -1, pb = -1;
            foreach (int c in t.Corners.Where(k => PlotLayout.IsSettlementCorner(t, k) && EconomyApi.SettlementAt(s, k) == null))
            {
                for (int sec = 0; sec < 3 && a < 0; sec++)
                    for (int slot = 3; slot <= 4 && a < 0; slot++)
                    {
                        int p = PlotLayoutOuter.OuterPlotId(c, sec, slot);
                        foreach (int q in PlotLayoutOuter.Rivals(t, p))
                        {
                            int o = PlotLayoutOuter.CornerOf(q);
                            if (t.CornerDistance(c, o) != 2 || EconomyApi.SettlementAt(s, o) != null) continue;
                            if (t.CornerNeighbours(o).Any(n => EconomyApi.SettlementAt(s, n) != null)) continue;
                            a = c; b = o; pa = p; pb = q;
                            break;
                        }
                    }
                if (a >= 0) break;
            }
            Assert.GreaterOrEqual(a, 0, "found a clashing pair");
            var sa = EconomyLogic.AddSettlement(s, 0, a, SettlementState.Active);
            var sb = EconomyLogic.AddSettlement(s, 1, b, SettlementState.Active);
            sa.IsCity = sb.IsCity = true;

            // Neither is a town yet: both plots are locked for the plain reason.
            Assert.AreEqual("plot_locked", EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, pa).Reason);
            sa.IsTown = true; sa.TownTick = 100;
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, pa).Ok, "the first town may use its ground");
            sb.IsTown = true; sb.TownTick = 200;
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, pa).Ok, "and keeps it");
            var r = EconomyApi.CheckBuilding(s, 1, BuildingType.Sawmill, pb);
            Assert.AreEqual("plot_blocked_by_neighbour", r.Reason);
            Assert.AreEqual("A neighbouring town already uses that ground.", r.Message);
            Assert.AreEqual(PlotUse.Locked, EconomyApi.Plot(s, pb).Use);
            CollectionAssert.DoesNotContain(EconomyApi.PlotsOf(s, sb.Id), pb);
            CollectionAssert.Contains(EconomyApi.PlotsOf(s, sa.Id), pa);
            Assert.Less(EconomyApi.PlotsOf(s, sb.Id).Count(PlotLayoutOuter.IsOuter), 6);
            // Plots of the second town that do not clash stay open.
            Assert.IsTrue(EconomyApi.PlotsOf(s, sb.Id).Any(PlotLayoutOuter.IsOuter));

            // If the order were the other way round, the other town would be the one blocked.
            sa.TownTick = 300;
            Assert.AreEqual("plot_blocked_by_neighbour", EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, pa).Reason);
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 1, BuildingType.Sawmill, pb).Ok);
            // Same tick: the lower settlement id wins.
            sa.TownTick = sb.TownTick = 400;
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, pa).Ok);
            Assert.AreEqual("plot_blocked_by_neighbour", EconomyApi.CheckBuilding(s, 1, BuildingType.Sawmill, pb).Reason);
        }

        // ---- campaign capture seam ----

        [Test]
        public void TransferSettlement_KeepsIdentityAndInventory_ChangesSideOfEverythingInIt()
        {
            var sim = Historic();
            var s = sim.State;
            int camp = K.SecondCamp(sim, out _);
            var st = s.Economy.Settlements[camp];
            var t = s.Board.Topology;
            int plot = PlotLayout.PlotId(st.Corner, 0, 1);
            var saw = BuildActive(sim, BuildingType.Sawmill, plot, camp);
            K.Give(s, camp, Ware.Wood, 6); // inputs flow from the camp's own storehouse: a valid delivery in flight
            K.Give(s, camp, Ware.Stone, 3);
            K.Step(sim, 15);
            Assert.IsTrue(s.Economy.Jobs.Any(j => j.Settlement == camp && j.ToKind == PlaceKind.Building && j.ToId == saw.Id), "inputs on the way");
            var idsBefore = s.Economy.Buildings.Where(b => b.Settlement == camp).Select(b => (b.Id, b.Plot, b.Type, b.Grade)).ToList();
            var stockBefore = (int[])st.Stock.Clone();
            int carriersOfCamp = s.Economy.Carriers.Count(c => c.Home == camp);
            var worldBefore = K.WaresInWorld(s);
            int buildingsBefore = s.Economy.Buildings.Count, settlementsBefore = s.Economy.Settlements.Count;

            var buf = new EventBuffer();
            Assert.IsTrue(EconomySeams.TransferSettlement(s, camp, 1, buf));
            var moved = buf.Items.OfType<SettlementTransferred>().Single();
            Assert.AreEqual((camp, st.Corner, 0, 1), (moved.SettlementId, moved.Corner, moved.FromFaction, moved.ToFaction));
            Assert.AreEqual(1, st.Faction);
            Assert.AreEqual(SettlementState.Active, st.State);
            Assert.IsTrue(s.Economy.Buildings.Where(b => b.Settlement == camp).All(b => b.Faction == 1));
            Assert.IsTrue(s.Economy.Carriers.Where(c => c.Home == camp).All(c => c.Faction == 1));
            Assert.AreEqual(carriersOfCamp, s.Economy.Carriers.Count(c => c.Home == camp));
            Assert.AreEqual(0, s.Economy.Settlements[0].Faction, "the other camp stays ours");
            CollectionAssert.AreEqual(idsBefore, s.Economy.Buildings.Where(b => b.Settlement == camp).Select(b => (b.Id, b.Plot, b.Type, b.Grade)).ToList(), "identity");
            CollectionAssert.AreEqual(stockBefore, st.Stock, "inventory");
            CollectionAssert.AreEqual(worldBefore, K.WaresInWorld(s), "no ware appears or disappears");
            Assert.AreEqual((buildingsBefore, settlementsBefore), (s.Economy.Buildings.Count, s.Economy.Settlements.Count));
            Assert.IsTrue(s.Economy.Jobs.Where(j => j.Settlement == camp && j.ToKind == PlaceKind.Building).All(j => j.Faction == 1));
            Assert.IsNull(K.CheckInvariants(s));

            Assert.IsFalse(EconomySeams.TransferSettlement(s, camp, 1, buf), "already theirs");
            Assert.IsFalse(EconomySeams.TransferSettlement(s, 99, 1, buf), "unknown");
            Assert.IsFalse(EconomySeams.TransferSettlement(s, camp, 7, buf), "not a people");
            Assert.IsFalse(EconomySeams.TransferSettlement(null, camp, 1, buf));

            // The valid delivery arrives; the new owner's economy goes on without drift.
            Assert.IsTrue(K.StepUntil(sim, () => saw.Delivered[(int)Ware.Wood] >= 2, 600), "the inputs in flight were delivered to the sawmill");
            K.Step(sim, 1500);
            Assert.IsNull(K.CheckInvariants(s));
            Assert.AreEqual(1, saw.Faction);
        }

        [Test]
        public void TransferSettlement_EndsDeliveriesThatCrossTheNewBorder_GoodsStayOrComeHome()
        {
            var sim = Historic();
            var s = sim.State;
            int camp = K.SecondCamp(sim, out _);
            var st = s.Economy.Settlements[camp];
            // The camp builds something and needs wood: the home camp ships it over the main road.
            K.Give(s, 0, Ware.Wood, 12);
            s.Economy.Settlements[camp].Stock[(int)Ware.Wood] = 0;
            int plot = PlotLayout.PlotId(st.Corner, 0, 1);
            K.Give(s, camp, Ware.Clay, 1);
            K.Do(sim, new PlaceBuilding { PlayerId = 0, Type = BuildingType.Sawmill, Plot = plot }, 0);
            Assert.IsTrue(K.StepUntil(sim, () => s.Economy.Jobs.Any(j => j.IsShipment && j.ToId == camp && j.State == JobState.Carrying), 800), "a shipment is on the road");
            Assert.IsTrue(EconomySeams.TransferSettlement(s, camp, 1, null));
            Assert.IsNull(K.CheckInvariants(s), "right after the transfer");
            var ship = s.Economy.Jobs.FirstOrDefault(j => j.IsShipment && j.Settlement == 0);
            if (ship != null) Assert.AreEqual(PlaceKind.Storehouse, ship.ToKind);
            Assert.IsFalse(s.Economy.Jobs.Any(j => j.ToKind == PlaceKind.Storehouse && j.ToId == camp && j.Faction == 0),
                "nobody of ours still carries goods to a camp that is no longer ours");
            K.Step(sim, 2000);
            Assert.IsNull(K.CheckInvariants(s));
            Assert.AreEqual(0, s.Economy.Carriers.Count(c => c.Faction == 0 && c.Home == camp));
            Assert.AreEqual(0, s.Economy.Jobs.Count(j => j.Faction == 0 && j.ToKind == PlaceKind.Storehouse && j.ToId == camp));
        }

        [Test]
        public void TransferSettlement_ThenSaveAndLoad_AgreesWithTheOriginal()
        {
            var sim = Historic();
            var s = sim.State;
            int camp = K.SecondCamp(sim, out _);
            EconomySeams.TransferSettlement(s, camp, 1, null);
            var loaded = SaveCodec.Load(SaveCodec.ToJson(s), out _);
            K.Step(sim, 800);
            K.Step(loaded, 800);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(loaded.State));
        }

        // ---- the existing economy is untouched while the new features are unused ----

        [Test]
        public void UnusedFeatures_ChangeNothing_ScriptedGameIsIdenticalWithAndWithoutGradeTuning()
        {
            string Run(bool touchTuning)
            {
                var sim = WorldSimFactory.CreateNew(77);
                var tu = sim.State.Economy.Tuning;
                if (touchTuning) { tu.CycleBonusPermille = new int[0]; } // a short table reads as "no bonus"
                sim.State.Demons.Tuning.GraceTicks = int.MaxValue;
                K.Give(sim.State, 0, Ware.Wood, 6);
                K.Give(sim.State, 0, Ware.Clay, 3);
                K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Plot = 631 });
                K.Step(sim, 3000);
                return SaveCodec.StateJson(sim.State);
            }
            string a = Run(false), b = Run(true);
            Assert.AreNotEqual(a, b, "the tuning array itself differs");
            // Everything but that array equals.
            Assert.AreEqual(a.Replace("\"CycleBonusPermille\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]", "\"CycleBonusPermille\":[]"), b);
        }
    }
}
