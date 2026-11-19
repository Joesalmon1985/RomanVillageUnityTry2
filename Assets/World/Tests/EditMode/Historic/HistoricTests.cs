using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using World.Sim.Eras;
using World.Sim.Historic;
using World.Sim.Plague;
using World.Sim.Save;
using World.Sim.Table;

namespace World.Sim.Tests
{
    /// <summary>G1-8 (sim part), G4-1, G4-2: the switch into Historic, castles, finite resources and the Historic planner.</summary>
    public class HistoricTests
    {
        // ---------------------------------------------------------------- helpers

        /// <summary>An AI-only god board game played until the age ends with the island standing (first such seed from <paramref name="seed"/>).</summary>
        internal static (WorldSim sim, List<SimEvent> log, ulong seed) PlayToHistoric(ulong seed, int maxSeeds = 60)
        {
            for (ulong k = seed; k < seed + (ulong)maxSeeds; k++)
            {
                var setup = GameSetup.GodBoard;
                setup.TableHumanSeat = -1;
                var sim = WorldSimFactory.CreateNew(k, setup);
                var log = new List<SimEvent>();
                for (int i = 0; i < 60000 && sim.State.Table.Phase != TablePhase.Ended; i++)
                {
                    sim.Step();
                    log.AddRange(sim.DrainEvents());
                }
                if (sim.State.Historic != null) return (sim, log, k);
            }
            Assert.Fail("no seed from " + seed + " ended its age with the island standing");
            return default;
        }

        /// <summary>
        /// A god board game switched into Historic at tick 0 (table ended by hand, people 0 the winner); with
        /// <paramref name="planner"/> false the brains are off, so tests place buildings themselves.
        /// </summary>
        private static WorldSim Fresh(ulong seed = 7, bool planner = false)
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = -1;
            var sim = WorldSimFactory.CreateNew(seed, setup);
            var s = sim.State;
            s.Table.Phase = TablePhase.Ended;
            s.Table.Winner = 0;
            PlagueRules.End(s, null);
            HistoricModule.Switch(s, 0, new EventBuffer());
            Assert.IsNotNull(s.Historic);
            if (!planner) foreach (var hp in s.Historic.Peoples) hp.NextPlanTick = long.MaxValue;
            return sim;
        }

        private static List<SimEvent> Steps(WorldSim sim, int n)
        {
            var log = new List<SimEvent>();
            for (int i = 0; i < n; i++)
            {
                sim.Step();
                log.AddRange(sim.DrainEvents());
            }
            return log;
        }

        private static int HexOf(WorldState s, Terrain t, int skip = 0)
        {
            for (int h = 0; h < s.Board.HexTerrain.Length; h++)
                if (s.Board.HexTerrain[h] == t && skip-- == 0) return h;
            Assert.Fail("no " + t + " hex");
            return -1;
        }

        private static HistoricBuilding Active(WorldState s, int p, HistoricKind kind, int hex)
        {
            var h = s.Historic;
            var b = new HistoricBuilding { Id = h.Buildings.Count, People = p, Kind = kind, Hex = hex, State = HistoricBuildingState.Active };
            h.Buildings.Add(b);
            return b;
        }

        private static void ClearTrees(WorldState s)
        {
            foreach (var r in s.Historic.Hexes)
                for (int k = 0; k < r.Trees.Length; k++) { r.Trees[k] = 0; r.TreeNext[k] = 0; }
        }

        private static void AssertBalanced(WorldState s) =>
            CollectionAssert.AreEqual(new int[Wares.Count], HistoricApi.Imbalance(s), "every Historic good is accounted for");

        // ---------------------------------------------------------------- the switch (HI-1, HI-2, HI-7, HI-8)

        [Test]
        public void Switch_AtTheAgesEnd_StepsIntoHistoric_ClosesTheTable_TearsDownTemples_StartsCastles()
        {
            var (sim, log, _) = PlayToHistoric(21);
            var s = sim.State;
            var ended = log.OfType<AgeEnded>().Single();
            Assert.AreEqual(Era.Historic, s.Eras.Current, "HI-1: the era moved");
            Assert.AreEqual(ChapterIds.Historic, s.Eras.Chapter);
            Assert.IsTrue(log.OfType<ChapterReady>().Any(e => e.Chapter == ChapterIds.Historic));
            Assert.AreEqual(1, log.OfType<ChapterAdvanced>().Count(e => e.To == ChapterIds.Historic));
            Assert.AreEqual(ChapterIds.Historic, s.History.Chapters.Last().To);
            var began = log.OfType<HistoricBegan>().Single();
            Assert.AreEqual(ended.Winner, began.Winner);
            Assert.AreEqual(ended.Tick, began.Tick, "the step is in the same tick as the age's end");
            Assert.IsTrue(s.Historic.TableClosed);
            // HI-8: the plague had already ended in that tick.
            Assert.IsTrue(s.Plague.Ended);
            Assert.AreEqual(0, PlagueApi.TotalDemons(s));
            // HI-2: the temples, one per people on its start corner.
            var torn = log.OfType<TemplesTornDown>().Single();
            CollectionAssert.AreEqual(s.Table.Peoples.Select(p => p.StartCorner).ToArray(), torn.Corners);
            // HI-3: a castle site for every people.
            var started = log.OfType<CastleStageStarted>().ToList();
            Assert.AreEqual(s.PeopleCount, started.Count);
            foreach (var e in started) Assert.AreEqual(0, e.Stage);
            // HI-1: the board is closed for good.
            Assert.IsFalse(sim.Check(new Sacrifice { PlayerId = 0 }).Ok);
            var after = Steps(sim, 600);
            Assert.IsFalse(after.OfType<TurnStarted>().Any());
            Assert.AreEqual(TablePhase.Ended, s.Table.Phase);
            // HI-7: the war keeps its rules (it is not stopped by the switch).
            Assert.IsNotNull(s.War);
            Assert.IsFalse(s.War.Stopped);
            Assert.AreEqual(0, after.OfType<HistoricBegan>().Count(), "once only");
        }

        [Test]
        public void Switch_IsRefused_ForAFallenIsland_OrARunningTable_AndIsIdempotent()
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = -1;
            var s = WorldSimFactory.CreateNew(5, setup).State;
            HistoricModule.Switch(s, 0, null);
            Assert.IsNull(s.Historic, "the table has not ended");
            s.Table.Phase = TablePhase.Ended;
            s.Table.IslandFell = true;
            s.Table.Winner = -1;
            HistoricModule.Switch(s, -1, null);
            Assert.IsNull(s.Historic, "a fallen island never reaches Historic");
            Assert.AreEqual(Era.Prehistoric, s.Eras.Current);
            s.Table.IslandFell = false;
            s.Table.Winner = 2;
            var ev = new EventBuffer();
            HistoricModule.Switch(s, 2, ev);
            var first = s.Historic;
            HistoricModule.Switch(s, 2, ev);
            Assert.AreSame(first, s.Historic);
            Assert.AreEqual(1, ev.Items.OfType<HistoricBegan>().Count());
            Assert.AreEqual(2, s.Historic.Winner);
        }

        [Test]
        public void SwitchedGame_IsDeterministic()
        {
            var (a, _, seed) = PlayToHistoric(21);
            var (b, _, seedB) = PlayToHistoric(seed, 1);
            Assert.AreEqual(seed, seedB);
            Steps(a, 6000);
            Steps(b, 6000);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
        }

        // ---------------------------------------------------------------- castles (HI-3)

        [Test]
        public void CastleCorner_IsThePeoplesSettlementWithTheMostPips_TiesToTheLowestCorner()
        {
            var sim = Fresh();
            var s = sim.State;
            var t = s.Table;
            var topo = s.Board.Topology;
            for (int c = 0; c < t.CornerOwner.Length; c++) t.CornerOwner[c] = -1;
            var corners = topo.Corners.Where(c => PlotLayout.IsSettlementCorner(topo, c))
                .OrderByDescending(c => BoardGenerator.CornerPips(s.Board, topo, c)).ThenBy(c => c).ToList();
            int top = BoardGenerator.CornerPips(s.Board, topo, corners[0]);
            var tied = corners.Where(c => BoardGenerator.CornerPips(s.Board, topo, c) == top).ToList();
            int low = corners.Last();
            t.CornerOwner[low] = 1;
            foreach (int c in tied) t.CornerOwner[c] = 1;
            Assert.AreEqual(tied.Min(), HistoricRules.CastleCorner(s, 1));
            // A people with no settlement left keeps its start corner.
            Assert.AreEqual(t.Peoples[3].StartCorner, HistoricRules.CastleCorner(s, 3));
        }

        [Test]
        public void CastleStages_RiseInOrder_WoodBrickStone_FedByCarriers()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            var hp = h.Peoples[0];
            for (int i = 1; i < h.Peoples.Count; i++) h.Peoples[i].Carriers = 0;
            var raised = new List<CastleStageRaised>();
            var startedNext = new List<CastleStageStarted>();
            for (int stage = 0; stage < 3; stage++)
            {
                var cost = h.Tuning.CastleCost[stage];
                // Put the stage's goods in the store, keeping the site reserve of wood, and book them as starting stock.
                for (int w = 0; w < Wares.Count; w++)
                {
                    int add = cost[w] + (w == (int)Ware.Wood ? h.Tuning.SiteReserveWood : 0);
                    hp.Stock[w] += add;
                    h.Ledger.Starting[w] += add;
                }
                var log = Steps(sim, 400);
                Assert.AreEqual(stage, hp.Castle.StagesDone, "the stage builds for CastleStageTicks once everything is there");
                for (int w = 0; w < Wares.Count; w++) Assert.AreEqual(cost[w], hp.Castle.Delivered[w], "carriers brought " + (Ware)w);
                log.AddRange(Steps(sim, h.Tuning.CastleStageTicks + 10));
                raised.AddRange(log.OfType<CastleStageRaised>());
                startedNext.AddRange(log.OfType<CastleStageStarted>());
                Assert.AreEqual(stage + 1, hp.Castle.StagesDone);
                AssertBalanced(s);
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, raised.Where(e => e.People == 0).Select(e => e.Stage).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, startedNext.Where(e => e.People == 0).Select(e => e.Stage).ToArray());
            Assert.AreEqual(3, HistoricApi.CastleStages(s, 0));
            Assert.IsTrue(hp.Castle.RaisedTick[0] < hp.Castle.RaisedTick[1] && hp.Castle.RaisedTick[1] < hp.Castle.RaisedTick[2]);
            Assert.AreEqual(0, HistoricRules.CastleNeed(h, hp, (int)Ware.Wood), "a finished castle wants nothing");
        }

        [Test]
        public void Castle_KeepsTheSiteReserveOfWood()
        {
            var sim = Fresh();
            var h = sim.State.Historic;
            var hp = h.Peoples[0];
            Steps(sim, 300);
            Assert.AreEqual(h.Tuning.SiteReserveWood, hp.Stock[(int)Ware.Wood], "wood above the reserve went to the castle");
            Assert.AreEqual(h.Tuning.StartStock[0] - h.Tuning.SiteReserveWood, hp.Castle.Delivered[(int)Ware.Wood]);
        }

        // ---------------------------------------------------------------- resources (HI-5)

        [Test]
        public void Hexes_AtTheSwitch_TreesFromPips_RockAndClay_FieldSlots()
        {
            var s = Fresh().State;
            var tu = s.Historic.Tuning;
            foreach (var r in s.Historic.Hexes)
            {
                int pips = BoardGenerator.Pips(s.Board.HexNumber[r.Hex]);
                int mature = r.Trees.Count(t => t == (int)TreeStage.Mature);
                switch (r.Terrain)
                {
                    case Terrain.Forest:
                        Assert.AreEqual(tu.ForestSlots, r.Trees.Length);
                        Assert.AreEqual(Math.Min(tu.ForestSlots, tu.TreesBase + tu.TreesPerPip * pips), mature);
                        break;
                    case Terrain.Meadow:
                        Assert.AreEqual(tu.MeadowSlots, r.Trees.Length, "a forester may plant on meadow");
                        Assert.AreEqual(0, mature);
                        break;
                    default:
                        Assert.AreEqual(0, r.Trees.Length);
                        break;
                }
                Assert.AreEqual(r.Terrain == Terrain.Mountain ? tu.MountainRockBase + tu.MountainRockPerPip * pips
                    : r.Terrain == Terrain.Hills ? tu.HillsRockBase + tu.HillsRockPerPip * pips : 0, r.Rock);
                Assert.AreEqual(r.Terrain == Terrain.Hills ? tu.ClayBase + tu.ClayPerPip * pips : 0, r.Clay);
                Assert.AreEqual(r.Terrain == Terrain.Fields ? tu.FieldsSlotsBase + pips : 0, r.Fields.Length);
            }
        }

        [Test]
        public void Woodcutter_FellsAGrownTree_LeavesAStump_ThatRots_AndTheWoodReachesTheStore()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            var tu = h.Tuning;
            ClearTrees(s);
            int hex = HexOf(s, Terrain.Forest);
            var r = h.Hexes[hex];
            r.Trees[3] = (int)TreeStage.Mature;
            r.Trees[5] = (int)TreeStage.Young;
            r.TreeNext[5] = long.MaxValue;
            var b = Active(s, 0, HistoricKind.Woodcutter, hex);
            int woodBefore = h.Peoples[0].Stock[(int)Ware.Wood] + h.Peoples[0].Castle.Delivered[(int)Ware.Wood] + h.Peoples[0].Castle.Incoming[(int)Ware.Wood];
            var log = Steps(sim, HistoricDefs.Get(HistoricKind.Woodcutter).WorkTicks + 5);
            var felled = log.OfType<TreeFelled>().Single();
            Assert.AreEqual(hex, felled.Hex);
            Assert.AreEqual(3, felled.Slot);
            Assert.AreEqual((int)TreeStage.Stump, r.Trees[3]);
            Assert.AreEqual((int)TreeStage.Young, r.Trees[5], "only grown trees are felled");
            Assert.AreEqual(1, h.Peoples[0].TreesFelled);
            Steps(sim, 400);
            Assert.AreEqual(0, b.WorkProgress, "no grown tree left: the woodcutter waits");
            int woodAfter = h.Peoples[0].Stock[(int)Ware.Wood] + h.Peoples[0].Castle.Delivered[(int)Ware.Wood] + h.Peoples[0].Castle.Incoming[(int)Ware.Wood];
            Assert.AreEqual(woodBefore + tu.WoodPerTree, woodAfter, "the logs were carried to the store");
            Assert.AreEqual(1, h.Peoples[0].WoodDelivered / tu.WoodPerTree);
            Steps(sim, tu.StumpRotTicks);
            Assert.AreEqual((int)TreeStage.Empty, r.Trees[3], "the stump rotted away");
            Steps(sim, 3 * tu.TreeGrowTicks);
            Assert.AreEqual((int)TreeStage.Empty, r.Trees[3], "nothing regrows without a forester");
            AssertBalanced(s);
        }

        [Test]
        public void Forester_PlantsOnEmptySlotsOnly_AndSaplingsGrowToMatureTrees()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            var tu = h.Tuning;
            ClearTrees(s);
            int hex = HexOf(s, Terrain.Forest);
            var r = h.Hexes[hex];
            for (int k = 0; k < r.Trees.Length; k++) { r.Trees[k] = (int)TreeStage.Stump; r.TreeNext[k] = long.MaxValue; }
            r.Trees[7] = (int)TreeStage.Empty;
            r.TreeNext[7] = 0;
            foreach (int n in HistoricRules.Neighbours(s.Board, hex))
                for (int k = 0; k < h.Hexes[n].Trees.Length; k++) { h.Hexes[n].Trees[k] = (int)TreeStage.Stump; h.Hexes[n].TreeNext[k] = long.MaxValue; }
            Active(s, 0, HistoricKind.Forester, hex);
            var log = Steps(sim, HistoricDefs.Get(HistoricKind.Forester).WorkTicks + 2);
            var planted = log.OfType<TreePlanted>().Single();
            Assert.AreEqual(hex, planted.Hex);
            Assert.AreEqual(7, planted.Slot);
            Assert.AreEqual((int)TreeStage.Sapling, r.Trees[7]);
            Assert.AreEqual(0, Steps(sim, 400).OfType<TreePlanted>().Count(), "never on a stump");
            Steps(sim, tu.TreeGrowTicks);
            Assert.AreEqual((int)TreeStage.Young, r.Trees[7]);
            Steps(sim, tu.TreeGrowTicks);
            Assert.AreEqual((int)TreeStage.Mature, r.Trees[7]);
        }

        [Test]
        public void Quarry_UsesUpTheRock_ThenIsExhausted_AndThePlannerReplacesIt()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            int hex = HexOf(s, Terrain.Mountain);
            h.Hexes[hex].Rock = 3;
            var q = Active(s, 0, HistoricKind.Quarry, hex);
            var log = Steps(sim, 3 * 150 + 50);
            Assert.AreEqual(0, h.Hexes[hex].Rock);
            Assert.AreEqual(3, h.Ledger.Produced[(int)Ware.Stone]);
            Assert.AreEqual(HistoricBuildingState.Exhausted, q.State);
            Assert.AreEqual(hex, log.OfType<QuarryExhausted>().Single().Hex);
            Assert.AreEqual(0, HistoricPlanner.CountOf(h, 0, HistoricKind.Quarry), "an exhausted quarry does not count");
            // The planner's need list, with wood covered: a new quarry on another rock hex.
            Active(s, 0, HistoricKind.Woodcutter, HexOf(s, Terrain.Forest));
            Active(s, 0, HistoricKind.Forester, HexOf(s, Terrain.Forest));
            Active(s, 0, HistoricKind.Woodcutter, HexOf(s, Terrain.Forest, 1));
            Active(s, 0, HistoricKind.Forester, HexOf(s, Terrain.Forest, 1));
            Active(s, 0, HistoricKind.Forester, HexOf(s, Terrain.Forest, 2));
            if (HistoricPlanner.BestHex(s, 0, HistoricKind.Quarry) < 0) Assert.Inconclusive("no other rock in reach on this seed");
            Assert.AreEqual(HistoricKind.Quarry, HistoricPlanner.NextNeed(s, 0));
            Assert.AreNotEqual(hex, HistoricPlanner.BestHex(s, 0, HistoricKind.Quarry), "not on the used-up hex");
            AssertBalanced(s);
        }

        [Test]
        public void ClayPit_Digs_AndTheClaySeepsBack()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            int hex = HexOf(s, Terrain.Hills);
            var r = h.Hexes[hex];
            int cap = r.ClayCap;
            Active(s, 0, HistoricKind.ClayPit, hex);
            Steps(sim, 2 * 150 + 5);
            Assert.AreEqual(2, h.Ledger.Produced[(int)Ware.Clay]);
            h.Buildings.Clear();
            Steps(sim, cap * h.Tuning.ClayRefillTicks + 10);
            Assert.AreEqual(cap, r.Clay, "refilled up to its starting amount, no more");
        }

        [Test]
        public void Farm_SowsThenHarvests_AfterTheFieldsGrow()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            var tu = h.Tuning;
            int hex = HexOf(s, Terrain.Fields);
            var r = h.Hexes[hex];
            Active(s, 0, HistoricKind.Farm, hex);
            Steps(sim, 105);
            Assert.AreEqual((int)FieldStage.Sown, r.Fields[0]);
            Assert.AreEqual(0, h.Ledger.Produced[(int)Ware.Grain], "nothing to harvest yet");
            Steps(sim, tu.FieldGrowTicks + 300);
            Assert.Greater(h.Ledger.Produced[(int)Ware.Grain], 0, "a ripe field was harvested");
            Assert.AreEqual(0, h.Ledger.Produced[(int)Ware.Grain] % tu.GrainPerHarvest);
            AssertBalanced(s);
        }

        [Test]
        public void SheepFarm_NeedsGrain_FromTheStore()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            var hp = h.Peoples[0];
            h.Ledger.Starting[(int)Ware.Grain] -= hp.Stock[(int)Ware.Grain];
            hp.Stock[(int)Ware.Grain] = 0;
            Active(s, 0, HistoricKind.SheepFarm, HexOf(s, Terrain.Pasture));
            Steps(sim, 800);
            Assert.AreEqual(0, h.Ledger.Produced[(int)Ware.Sheep], "no grain, no sheep");
            hp.Stock[(int)Ware.Grain] = 2;
            h.Ledger.Starting[(int)Ware.Grain] += 2;
            Steps(sim, 1000);
            Assert.AreEqual(2, h.Ledger.Produced[(int)Ware.Sheep]);
            Assert.AreEqual(2, h.Ledger.Consumed[(int)Ware.Grain]);
            AssertBalanced(s);
        }

        [Test]
        public void Kiln_FiresClayAndWood_IntoBricks()
        {
            var sim = Fresh();
            var s = sim.State;
            var h = s.Historic;
            var hp = h.Peoples[0];
            h.Ledger.Starting[(int)Ware.Clay] += 2 - hp.Stock[(int)Ware.Clay];
            hp.Stock[(int)Ware.Clay] = 2;
            Active(s, 0, HistoricKind.Kiln, HexOf(s, Terrain.Meadow));
            Steps(sim, 1000);
            Assert.AreEqual(2, h.Ledger.Produced[(int)Ware.Bricks]);
            Assert.AreEqual(2, h.Ledger.Consumed[(int)Ware.Clay]);
            AssertBalanced(s);
        }

        // ---------------------------------------------------------------- the planner (HI-4, HI-6)

        [Test]
        public void Planner_StartsWithAWoodcutter_KeepsForestersAhead_AndRespectsTheSiteRules()
        {
            var sim = Fresh(7, planner: true);
            var s = sim.State;
            var h = s.Historic;
            Assert.AreEqual(HistoricKind.Woodcutter, HistoricPlanner.NextNeed(s, 0));
            var log = Steps(sim, 9000);
            var placed = log.OfType<HistoricSitePlaced>().ToList();
            foreach (var hp in h.Peoples)
            {
                var mine = placed.Where(e => e.People == hp.Id).ToList();
                Assert.IsNotEmpty(mine, "every people builds, the player's too (HI-4)");
                Assert.AreEqual(HistoricKind.Woodcutter, mine[0].Kind, "wood first");
                int cutters = HistoricPlanner.CountOf(h, hp.Id, HistoricKind.Woodcutter);
                int foresters = HistoricPlanner.CountOf(h, hp.Id, HistoricKind.Forester);
                Assert.GreaterOrEqual(foresters, cutters, "foresters ahead of woodcutters");
                foreach (var e in mine)
                {
                    var d = HistoricDefs.Get(e.Kind);
                    Assert.IsTrue(HistoricDefs.Allows(d, s.Board.HexTerrain[e.Hex]), e.Kind + " on " + s.Board.HexTerrain[e.Hex]);
                    Assert.IsTrue(HistoricRules.Territory(s, hp.Id, HistoricRules.FarRings).Contains(e.Hex));
                }
            }
            for (int hex = 0; hex < h.Hexes.Length; hex++)
                Assert.LessOrEqual(h.Buildings.Count(b => b.Hex == hex && b.State != HistoricBuildingState.Exhausted), h.Tuning.MaxBuildingsPerHex);
            Assert.AreEqual("wrong_terrain", HistoricRules.SiteRefusal(s, 0, HistoricKind.Quarry, HexOf(s, Terrain.Meadow)));
            AssertBalanced(s);
        }

        [Test]
        public void SiteRefusals()
        {
            var s = Fresh().State;
            Assert.AreEqual("invalid_site", HistoricRules.SiteRefusal(s, 0, HistoricKind.Kiln, -1));
            Assert.AreEqual("wrong_terrain", HistoricRules.SiteRefusal(s, 0, HistoricKind.ClayPit, HexOf(s, Terrain.Forest)));
            var reach = HistoricRules.Territory(s, 0, HistoricRules.FarRings);
            int far = Enumerable.Range(0, s.Board.HexTerrain.Length).FirstOrDefault(x => !reach.Contains(x) && s.Board.HexTerrain[x] == Terrain.Meadow);
            if (!reach.Contains(far)) Assert.AreEqual("out_of_reach", HistoricRules.SiteRefusal(s, 0, HistoricKind.Kiln, far));
            int hex = reach.First(x => s.Board.HexTerrain[x] == Terrain.Meadow || s.Board.HexTerrain[x] == Terrain.Forest);
            for (int i = 0; i < s.Historic.Tuning.MaxBuildingsPerHex; i++) Assert.GreaterOrEqual(HistoricRules.PlaceSite(s, 0, HistoricKind.Kiln, hex, null), 0);
            Assert.AreEqual("hex_full", HistoricRules.SiteRefusal(s, 0, HistoricKind.Kiln, hex));
            var legacy = WorldSimFactory.CreateNew(1).State;
            Assert.AreEqual("not_historic", HistoricRules.SiteRefusal(legacy, 0, HistoricKind.Kiln, 0));
        }

        // ---------------------------------------------------------------- save (v8)

        [Test]
        public void Save_HistoricGameMidCastle_RoundTrips_AndPlaysOnIdentically()
        {
            var (a, _, _) = PlayToHistoric(21);
            // Run until some people's castle is between stages with goods on the site.
            for (int i = 0; i < 30000 && !a.State.Historic.Peoples.Any(p => p.Castle.StagesDone == 1 && p.Castle.Delivered.Sum() > 0); i++)
            {
                a.Step();
                a.DrainEvents();
            }
            var hs = a.State.Historic;
            Assert.IsTrue(hs.Peoples.Any(p => p.Castle.StagesDone == 1 && p.Castle.Delivered.Sum() > 0), "mid-castle");
            Assert.IsNotEmpty(hs.Jobs, "carriers underway at the save");
            string json = SaveCodec.ToJson(a.State);
            var b = SaveCodec.Load(json, out var model);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.LoadedVersion);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
            for (int i = 0; i < 3000; i++)
            {
                a.Step();
                b.Step();
                var ea = a.DrainEvents();
                var eb = b.DrainEvents();
                Assert.AreEqual(ea.Count, eb.Count, "same events at tick " + a.State.Tick);
            }
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
            AssertBalanced(b.State);
        }

        [Test]
        public void Save_V7GodBoardGameWaitingAtHistoricReady_StepsIntoHistoricOnLoad()
        {
            var (a, _, _) = PlayToHistoric(21);
            var root = JObject.Parse(SaveCodec.ToJson(a.State));
            root["Version"] = 7;
            var st = (JObject)root["State"];
            st.Remove("Historic");
            st["Eras"]["Current"] = (int)Era.Prehistoric;
            st["Eras"]["Chapter"] = ChapterIds.Prehistoric;
            st["Eras"]["ReadyChapters"] = new JArray(ChapterIds.Historic);
            st["History"]["Chapters"] = new JArray();
            var b = SaveCodec.Load(root.ToString(), out var model);
            Assert.AreEqual(7, model.LoadedVersion);
            Assert.IsNotNull(b.State.Historic, "v7 to v8: the waiting game steps into Historic");
            Assert.AreEqual(Era.Historic, b.State.Eras.Current);
            Assert.AreEqual(a.State.Table.Winner, b.State.Historic.Winner);
            Steps(b, 2000);
            Assert.Greater(b.State.Historic.Buildings.Count, 0, "the brains plan after the load");
        }

        [Test]
        public void Save_V7LegacyAndRunningGodBoardGames_HaveNoHistoric()
        {
            var legacy = WorldSimFactory.CreateNew(3);
            Steps(legacy, 50);
            var b = SaveCodec.Load(SaveCodec.ToJson(legacy.State).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":7"), out _);
            Assert.IsNull(b.State.Historic);
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = -1;
            var g = WorldSimFactory.CreateNew(3, setup);
            Steps(g, 500);
            var c = SaveCodec.Load(SaveCodec.ToJson(g.State).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":7"), out _);
            Assert.IsNull(c.State.Historic);
            Assert.AreEqual(Era.Prehistoric, c.State.Eras.Current);
        }
    }
}
