using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Save;
using World.Sim.Strategy;
using K = World.Sim.Tests.StrategyTestKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// Rules of the unified economy (ECONOMY_DESIGN sections 3-8): the ware chain, mines eating rations, the smelter
    /// needing coal and power, demons on flags and cities (the robber), outbreaks and epidemics, the age-end military
    /// comparison, the five card families, the new commission, save of the new state and the UI helpers.
    /// </summary>
    public class UnifiedEconomyTests
    {
        private static void StepUntil(WorldSim sim, Func<bool> done, int max, List<SimEvent> sink = null)
        {
            for (int i = 0; i < max && !done(); i++)
            {
                sim.Step();
                var e = sim.DrainEvents();
                sink?.AddRange(e);
            }
            Assert.IsTrue(done(), "condition not reached in " + max + " ticks");
        }

        /// <summary>Found a city on the nearest legal corner touching the terrain (roads built, wares given).</summary>
        internal static int CityOn(WorldSim sim, Terrain terrain)
        {
            var s = sim.State;
            var topo = s.Board.Topology;
            K.Give(s, Ware.Planks, 30, Ware.Bricks, 30, Ware.Rations, 4);
            int site = topo.Corners
                .Where(v => topo.VertexHexes(v).Any(h => s.Board.HexTerrain[h] == terrain))
                .Where(v => StrategyApi.CheckCity(s, v).Reason == StrategyRefusals.NoRoad || StrategyApi.CheckCity(s, v).Ok)
                .OrderBy(v => EconomyTestKit.PathToNetwork(s, v).Length).ThenBy(v => v).First();
            K.RoadTo(sim, site);
            K.Do(sim, new FoundCity { Vertex = site });
            Assert.IsTrue(StrategyApi.PlayerCities(s).Contains(site));
            return site;
        }

        internal static int HexOf(WorldState s, int vertex, Terrain t) =>
            s.Board.Topology.VertexHexes(vertex).First(h => s.Board.HexTerrain[h] == t);

        // ---------------- Wares and buildings ----------------

        [Test]
        public void Wares_FiveRawAndNineProcessed_EveryWareHasAProducerAndAConsumerOrSink()
        {
            var s = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed).State;
            CollectionAssert.AreEqual(new[] { Ware.Logs, Ware.Clay, Ware.Wheat, Ware.Sheep, Ware.Ore }, Wares.Raw);
            Assert.AreEqual(14, Wares.Count);
            foreach (var w in Wares.All)
            {
                Assert.IsNotEmpty(EconomyApi.ProducersOf(s, w), "producer of " + w);
                bool consumed = EconomyApi.ConsumersOf(s, w).Count > 0;
                bool spent = w == Ware.Planks || w == Ware.Bricks || w == Ware.Rations || w == Ware.Iron || w == Ware.Weapons;
                Assert.IsTrue(consumed || spent, "consumer or sink of " + w);
                StringAssert.StartsWith("#", EconomyApi.WareColour(w));
            }
            Assert.AreEqual(Wares.Count, Wares.All.Select(EconomyApi.WareColour).Distinct().Count(), "distinct colours");
            // Each resource terrain has a harvester.
            foreach (var w in Wares.Raw)
                Assert.IsTrue(s.Economy.Tuning.Buildings.Any(d => d.Kind == BuildingKind.Harvester && d.Terrain == TerrainInfo.TerrainOf(w)), "harvester for " + w);
            // Every building has a construction cost in planks (and bricks above small).
            foreach (var d in s.Economy.Tuning.Buildings.Where(d => d.Placeable))
            {
                Assert.Greater(d.Cost[(int)Ware.Planks], 0, d.Name);
                Assert.AreEqual(d.Size == BuildingSize.Small ? 0 : d.Cost[(int)Ware.Bricks], d.Cost[(int)Ware.Bricks]);
                Assert.IsFalse(string.IsNullOrEmpty(d.Purpose), d.Name);
            }
        }

        [Test]
        public void Recipes_MatchTheDesign()
        {
            var t = new EconomyTuning();
            void Check(BuildingType b, int[] inputs, Ware output)
            {
                CollectionAssert.AreEqual(inputs, t.Def(b).Inputs, b + " inputs");
                Assert.Greater(t.Def(b).Outputs[(int)output], 0, b + " output");
            }
            Check(BuildingType.Sawmill, EconomyTuning.W(Ware.Logs, 1), Ware.Planks);
            Check(BuildingType.CharcoalKiln, EconomyTuning.W(Ware.Logs, 1), Ware.Coal);
            Assert.AreEqual(1, t.Def(BuildingType.CharcoalKiln).Outputs[(int)Ware.Coal], "1 logs -> 1 coal (tuned from 2 logs)");
            Check(BuildingType.BrickKiln, EconomyTuning.W(Ware.Clay, 2, Ware.Coal, 1), Ware.Bricks);
            Assert.AreEqual(2, t.Def(BuildingType.BrickKiln).Outputs[(int)Ware.Bricks], "2 clay + 1 coal -> 2 bricks (tuned from 1 clay)");
            Check(BuildingType.Mill, EconomyTuning.W(Ware.Wheat, 1), Ware.Flour);
            Check(BuildingType.Bakery, EconomyTuning.W(Ware.Flour, 1), Ware.Bread);
            Check(BuildingType.Butcher, EconomyTuning.W(Ware.Sheep, 1), Ware.Meat);
            Check(BuildingType.Tavern, EconomyTuning.W(Ware.Bread, 1, Ware.Meat, 1), Ware.Rations);
            Check(BuildingType.Smelter, EconomyTuning.W(Ware.Ore, 2, Ware.Coal, 1), Ware.Iron);
            Assert.AreEqual(1, t.Def(BuildingType.Bakery).Outputs[(int)Ware.Bread], "1 flour -> 1 bread (tuned from 2)");
            Assert.AreEqual(1, t.Def(BuildingType.Butcher).Outputs[(int)Ware.Meat], "1 sheep -> 1 meat (tuned from 2)");
            Assert.AreEqual(2, t.Def(BuildingType.Tavern).Outputs[(int)Ware.Rations], "1 bread + 1 meat -> 2 rations");
            Assert.IsTrue(t.Def(BuildingType.Smelter).NeedsPower);
            Check(BuildingType.Smithy, EconomyTuning.W(Ware.Iron, 1, Ware.Planks, 1), Ware.Weapons);
            CollectionAssert.AreEqual(EconomyTuning.W(Ware.Coal, 1), t.Def(BuildingType.PowerPlant).Inputs, "power plant burns coal");
            Assert.IsTrue(t.Def(BuildingType.Barracks).NeedsPower);
            CollectionAssert.AreEqual(EconomyTuning.W(Ware.Rations, 1), t.Def(BuildingType.Mine).Inputs);
            Assert.AreEqual(3, t.Def(BuildingType.Mine).CyclesPerInput, "1 rations per 3 ore (tuned from 2)");
        }

        // ---------------- Mine, smelter, power ----------------

        [Test]
        public void Mine_StallsWithoutRations_NamingThem_ThenEatsOneRationPerThreeOre()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            int city = CityOn(sim, Terrain.Mountain);
            int mountain = HexOf(s, city, Terrain.Mountain);
            EconomyApi.TrySpendFromStorehouse(s, Ware.Rations, EconomyApi.StorehouseCount(s, Ware.Rations));
            int mine = K.Build(sim, BuildingType.Mine, EconomyTestKit.Touches(s, mountain));
            s.Economy.HexBatchRound[mountain] = 0;
            Assert.IsTrue(EconomyApi.AuthoriseBatch(s, mountain, 6, new EventBuffer()));
            K.Step(sim, 5);
            var b = s.Economy.Buildings[mine];
            Assert.AreEqual(Reasons.InputMissing, b.Reason, "a batch waits but the miners have no rations");
            Assert.AreEqual((int)Ware.Rations, b.ReasonWare);
            Assert.AreEqual("Mine needs rations to work.", EconomyApi.Describe(s, mine).Status);
            Assert.AreEqual(0, s.Economy.Ledger.Harvested[(int)Ware.Ore]);

            K.Give(s, Ware.Rations, 3);
            var evs = new List<SimEvent>();
            StepUntil(sim, () => s.Economy.Ledger.Harvested[(int)Ware.Ore] >= 6, 3000, evs);
            Assert.AreEqual(2, evs.OfType<UpkeepPaid>().Count(u => u.BuildingId == mine), "6 ore used 2 rations");
            Assert.AreEqual(2, s.Economy.Ledger.RecipeIn[(int)BuildingType.Mine][(int)Ware.Rations]);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Smelter_StallsWithoutPower_ThenWithoutCoal_ThenMakesIron()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.Give(s, Ware.Planks, 20, Ware.Bricks, 20, Ware.Ore, 2);
            EconomyApi.TrySpendFromStorehouse(s, Ware.Coal, EconomyApi.StorehouseCount(s, Ware.Coal));
            int smelter = K.Build(sim, BuildingType.Smelter);
            K.Step(sim, 30);
            var b = s.Economy.Buildings[smelter];
            Assert.AreEqual(Reasons.InputMissing, b.Reason, "ore delivered, coal missing");
            Assert.AreEqual((int)Ware.Coal, b.ReasonWare);
            Assert.AreEqual("Smelter needs coal to work.", EconomyApi.Describe(s, smelter).Status);

            K.Give(s, Ware.Coal, 1);
            K.Step(sim, 60);
            Assert.AreEqual(Reasons.NoPower, b.Reason, "inputs ready, no power plant");
            Assert.AreEqual(0, b.WorkProgress);

            int plant = K.Build(sim, BuildingType.PowerPlant);
            K.Give(s, Ware.Coal, 2);
            var evs = new List<SimEvent>();
            StepUntil(sim, () => s.Economy.Ledger.Produced[(int)Ware.Iron] >= 1, 1000, evs);
            Assert.IsTrue(evs.OfType<FuelBurned>().Any(f => f.BuildingId == plant && f.Ware == Ware.Coal), "the plant burned coal");
            Assert.AreEqual(1, s.Economy.Ledger.RecipeIn[(int)BuildingType.Smelter][(int)Ware.Coal]);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void PowerCut_StopsSmelterAndBarracks_TheyResumeWhenCoalReturns()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Economy.Tuning.Def(BuildingType.PowerPlant).FuelTicks = 60;
            K.Step(sim);
            K.FinishDraft(sim);
            K.Give(s, Ware.Planks, 30, Ware.Bricks, 30, Ware.Ore, 6, Ware.Coal, 8, Ware.Weapons, 1);
            int plant = K.Build(sim, BuildingType.PowerPlant);
            int smelter = K.Build(sim, BuildingType.Smelter);
            int barracks = K.Build(sim, BuildingType.Barracks);
            K.Do(sim, new QueueUnit { BarracksId = barracks });
            for (int i = 0; i < 600 && s.Economy.Buildings[smelter].WorkProgress == 0; i++) sim.Step();
            var sb = s.Economy.Buildings[smelter];
            Assert.Greater(sb.WorkProgress, 0, "smelter " + sb.Reason + " " + sb.ReasonWare + " stock " + string.Join(",", sb.Stock) + " power " + MilitaryApi.Power(s).Sentence + " plant " + EconomyApi.Describe(s, plant).Status);
            // Cut the fuel: take every coal and wait for the plant to go dark.
            EconomyApi.TrySpendFromStorehouse(s, Ware.Coal, EconomyApi.StorehouseCount(s, Ware.Coal));
            StepUntil(sim, () => s.Economy.Buildings[plant].Incoming[(int)Ware.Coal] == 0, 400);
            EconomyApi.TryConsume(s, plant, Ware.Coal, EconomyApi.Available(s, plant, Ware.Coal));
            EconomyApi.TryConsume(s, smelter, Ware.Coal, EconomyApi.Available(s, smelter, Ware.Coal));
            StepUntil(sim, () => !EconomyApi.IsFuelled(s, plant), 400);
            K.Step(sim, 3);
            Assert.AreEqual(0, MilitaryApi.Power(s).Supply);
            Assert.AreEqual(Reasons.NoPower, s.Military.Queues.Single().Reason, "barracks stalls");
            int frozen = s.Economy.Buildings[smelter].WorkProgress;
            K.Step(sim, 30);
            if (frozen > 0) Assert.AreEqual(frozen, s.Economy.Buildings[smelter].WorkProgress, "smelter cycle frozen without power");
            Assert.AreEqual(Reasons.NoPower, s.Economy.Buildings[barracks].Reason);

            K.Give(s, Ware.Coal, 14); // enough for the plant (1 per 60 ticks here) and the smelter
            StepUntil(sim, () => EconomyApi.IsFuelled(s, plant), 400);
            for (int i = 0; i < 600 && s.Military.UnitsTrained == 0; i++) sim.Step();
            Assert.AreEqual(1, s.Military.UnitsTrained, MilitaryApi.DescribeBarracks(s, barracks).Status + " | " + MilitaryApi.Power(s).Sentence + " | " + EconomyApi.Describe(s, barracks).Status);
            Assert.IsNull(K.CheckInvariants(s));
        }

        // ---------------- Demons on flags and cities ----------------

        [Test]
        public void DemonOnABuildingFlag_StopsTheBuilding_UntilCleared()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            int saw = K.Build(sim, BuildingType.Sawmill);
            var b = s.Economy.Buildings[saw];
            K.Give(s, Ware.Logs, 6);
            StepUntil(sim, () => b.WorkProgress > 0, 400);
            s.Strategy.Demons[b.Vertex] = 1;
            K.Step(sim, 2);
            Assert.IsTrue(b.Blocked);
            Assert.AreEqual(Reasons.DemonOnRoute, b.Reason);
            StringAssert.StartsWith("A demon on its flag stops the Sawmill", EconomyApi.Describe(s, saw).Status);
            int frozen = b.WorkProgress;
            K.Step(sim, 40);
            Assert.AreEqual(frozen, b.WorkProgress, "no work under a demon");
            StrategyApi.ClearDemonsBySquad(s, b.Vertex, new EventBuffer());
            K.Step(sim, 2);
            Assert.IsFalse(b.Blocked);
            Assert.Greater(b.WorkProgress + s.Economy.Ledger.Cycles[(int)BuildingType.Sawmill], frozen > 0 ? 0 : -1);
            StepUntil(sim, () => s.Economy.Ledger.Cycles[(int)BuildingType.Sawmill] > 0, 200);
        }

        [Test]
        public void Robber_ADemonOnTheCityStopsItsHexes_BatchesAndHarvest_ResumeWhenCleared()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            int forest = EconomyTestKit.HomeHex(s, Terrain.Forest);
            int wc = K.Build(sim, BuildingType.Woodcutter, EconomyTestKit.Touches(s, forest));
            int home = s.Board.HomeCityVertex;
            s.Strategy.Tuning.InfectionBase = 0; // no infection draws in this test: only the rigged demon
            s.Strategy.Tuning.InfectionPerAge = 0;
            K.Do(sim, new MoveHero { Vertex = K.DemoCorner(s) }); // hero away from home
            s.Strategy.Demons[home] = 1;
            s.Economy.HexBatchRemaining[forest] = 5;
            K.Step(sim, 2);
            Assert.IsTrue(s.Economy.HexStopped[forest], "every city of the home forest holds a demon");
            Assert.IsTrue(StrategyApi.IsHexRobbed(s, forest));
            int harvested = s.Economy.Ledger.Harvested[(int)Ware.Logs];
            K.Step(sim, s.Economy.Tuning.HarvestTicks * 2 + 5);
            Assert.LessOrEqual(s.Economy.Ledger.Harvested[(int)Ware.Logs], harvested + 2, "at most the cycle already running finishes");
            Assert.AreEqual(Reasons.NoBatch, s.Economy.Buildings[wc].Reason);
            StringAssert.Contains("A demon on your city stops the forest", EconomyApi.Describe(s, wc).Status);
            // The next round draws no batch for the home hexes.
            var evs = K.EndRound(sim);
            foreach (int h in s.Board.Topology.VertexHexes(home))
                Assert.IsFalse(evs.OfType<BatchAuthorised>().Any(x => x.Hex == h), "no batch on robbed hex " + h);
            // Cleared: harvest resumes from the carried-over batch.
            s.Strategy.Demons[home] = 0;
            K.Step(sim, 2);
            Assert.IsFalse(s.Economy.HexStopped[forest]);
            StepUntil(sim, () => s.Economy.Ledger.Harvested[(int)Ware.Logs] > harvested + 2, 400);
        }

        // ---------------- Outbreaks and epidemics ----------------

        private static void Rig(WorldState s, params int[] topFirst)
        {
            s.Strategy.InfectionDeck.Clear();
            s.Strategy.InfectionDiscard.Clear();
            for (int i = topFirst.Length - 1; i >= 0; i--) s.Strategy.InfectionDeck.Add(topFirst[i]);
            s.Strategy.Tuning.InfectionBase = topFirst.Length;
        }

        [Test]
        public void OutbreakNeighbours_AreTheNearestDemonSitesAlongEachLine()
        {
            var sim = K.NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            // An empty corner far from the village: its neighbours are its three corner neighbours.
            int lonely = topo.Corners.First(v => topo.VertexHexes(v).Count == 3 && topo.CornerDistance(v, s.Board.HomeCityVertex) >= 3
                && topo.CornerDistance(v, s.Board.RivalOutpostVertex) >= 2);
            CollectionAssert.AreEquivalent(topo.CornerNeighbours(lonely), StrategyApi.OutbreakNeighbours(s, lonely));
            // The demo corner: along its hex edge to home the nearest site is the Forester's flag (occupied), not home.
            int corner = K.DemoCorner(s);
            var nb = StrategyApi.OutbreakNeighbours(s, corner);
            CollectionAssert.Contains(nb, s.Economy.DemoVertexB);
            CollectionAssert.DoesNotContain(nb, s.Board.HomeCityVertex);
            Assert.LessOrEqual(nb.Count, 3);
            // An intermediate flag has two: one in each direction.
            Assert.AreEqual(2, StrategyApi.OutbreakNeighbours(s, s.Economy.DemoVertexA).Count);
        }

        [Test]
        public void Outbreak_Chains_EachSiteAtMostOncePerDraw_AdvancingTheBreachTrackEachTime()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.Do(sim, new MoveHero { Vertex = s.Board.Topology.CornerNeighbours(s.Board.HomeCityVertex).First(v => v != K.DemoCorner(s)) });
            int a = s.Economy.DemoVertexA, b = s.Economy.DemoVertexB;
            int cap = StrategyApi.DemonCap(s);
            Assert.AreEqual(3, cap);
            // A and B are neighbours on the demo road; both full.
            CollectionAssert.Contains(StrategyApi.OutbreakNeighbours(s, a), b);
            CollectionAssert.Contains(StrategyApi.OutbreakNeighbours(s, b), a);
            s.Strategy.Demons[a] = cap;
            s.Strategy.Demons[b] = cap;
            Rig(s, a);
            var evs = K.EndRound(sim);
            var obs = evs.OfType<OutbreakOccurred>().ToList();
            Assert.AreEqual(2, obs.Count, "A outbreaks, B (full) outbreaks in the chain, A does not outbreak again");
            Assert.AreEqual(a, obs[0].Vertex);
            Assert.AreEqual(b, obs[1].Vertex);
            Assert.IsTrue(obs[1].Chained);
            Assert.AreEqual(2, s.Strategy.Breaches, "one step per outbreak");
            Assert.AreEqual(cap, s.Strategy.Demons[a]);
            Assert.AreEqual(cap, s.Strategy.Demons[b]);
            foreach (int n in StrategyApi.OutbreakNeighbours(s, a).Concat(StrategyApi.OutbreakNeighbours(s, b)).Distinct())
                if (n != a && n != b && n != s.Strategy.HeroVertex) Assert.GreaterOrEqual(s.Strategy.Demons[n], 1, "neighbour " + n + " gained a demon");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Epidemic_AtEachAgeChange_FillsTheBottomSiteToThree_AndPutsTheDiscardOnTop()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99;
            K.Step(sim);
            Assert.AreEqual(2, StrategyApi.InfectionCount(s), "Age I draws 2");
            for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge - 1; r++) K.EndRound(sim);
            // Rig: the bottom card is a fresh corner; the discard holds two known flags.
            var st = s.Strategy;
            int bottom = K.DemoCorner(s);
            int home = s.Board.HomeCityVertex;
            // Deck (top last): the round-end draw takes the demo road flag; the epidemic then finds the corner at the bottom.
            st.InfectionDeck.Clear();
            st.InfectionDiscard.Clear();
            st.InfectionDeck.AddRange(new[] { bottom, s.Economy.DemoVertexB, s.Economy.DemoVertexA });
            st.InfectionDiscard.Add(home); // home joins in Age II: keep it off the bottom
            st.Demons[bottom] = 0;
            st.Tuning.InfectionBase = 0; // no ordinary draw this round end, only the epidemic at the age change
            st.Tuning.InfectionPerAge = 0;
            var evs = K.EndRound(sim);
            Assert.AreEqual(2, st.Age);
            Assert.IsNotEmpty(evs.OfType<EpidemicStruck>(), string.Join(",", evs.Select(e => e.GetType().Name)) + " deck " + string.Join(",", st.InfectionDeck) + " bottom " + bottom);
            var ep = evs.OfType<EpidemicStruck>().Single();
            Assert.AreEqual(bottom, ep.Vertex);
            Assert.AreEqual(3, st.Demons[bottom], "filled to three");
            Assert.IsFalse(ep.Outbreak, "it held none before");
            Assert.AreEqual(1, st.Epidemics);
            Assert.AreEqual(0, st.InfectionDiscard.Count, "discard shuffled back");
            // Deck now: [B] at the bottom, then the shuffled discard (home, A drawn at round end, the epidemic corner) on top.
            Assert.AreEqual(s.Economy.DemoVertexB, st.InfectionDeck[0], "the undrawn card stays at the bottom");
            CollectionAssert.AreEquivalent(new[] { home, s.Economy.DemoVertexA, bottom }, st.InfectionDeck.Skip(1).ToList(), "the discard went back on top");
            st.Tuning.InfectionBase = 2;
            st.Tuning.InfectionPerAge = 1;
            Assert.AreEqual(3, StrategyApi.InfectionCount(s), "the rate rises with the age");
        }

        [Test]
        public void Epidemic_OnASiteThatHeldDemons_Outbreaks()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99;
            K.Step(sim);
            for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge - 1; r++) K.EndRound(sim);
            var st = s.Strategy;
            int bottom = K.DemoCorner(s);
            st.InfectionDeck.Clear();
            st.InfectionDiscard.Clear();
            st.InfectionDeck.AddRange(new[] { bottom, s.Economy.DemoVertexB, s.Economy.DemoVertexA });
            st.InfectionDiscard.Add(s.Board.HomeCityVertex);
            st.Demons[bottom] = 1;
            st.Tuning.InfectionBase = 0;
            st.Tuning.InfectionPerAge = 0;
            int breaches = st.Breaches;
            var evs = K.EndRound(sim);
            Assert.IsTrue(evs.OfType<EpidemicStruck>().Single().Outbreak);
            Assert.AreEqual(breaches + 1, st.Breaches);
            Assert.AreEqual(bottom, evs.OfType<OutbreakOccurred>().First().Vertex);
        }

        // ---------------- Military comparison ----------------

        /// <summary>Give the player n soldiers in a field squad (test setup).</summary>
        private static void Soldiers(WorldState s, int n)
        {
            var sq = MilitaryLogicAccess.NewPlayerSquad(s, s.Board.HomeCityVertex);
            for (int i = 0; i < n; i++) sq.Units.Add(new Unit { Id = s.Military.NextUnitId++, Hp = s.Military.Tuning.UnitHp });
        }

        [Test]
        public void MilitaryComparison_EachAgeEnd_WinLoseTie_TokensByAge_MirroredForTheRival()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99;
            s.Strategy.Tuning.VictoryPoints = 99;
            K.Step(sim);
            var t = s.Strategy.Tuning;
            // Age I: no soldiers, rival has its garrison -> loss.
            int rivalI = MilitaryApi.Shields(s, PlayerIds.Rival);
            Assert.Greater(rivalI, 0);
            List<SimEvent> evs = null;
            for (int r = 0; r < t.RoundsPerAge; r++) evs = K.EndRound(sim);
            var c1 = evs.OfType<MilitaryCompared>().Single();
            Assert.AreEqual(1, c1.Age);
            Assert.AreEqual(ComparisonResult.Loss, c1.Result);
            Assert.AreEqual(t.MilitaryLossVp, c1.PlayerVp);
            Assert.AreEqual(t.MilitaryWinVp[0], c1.RivalVp);
            // Age II: exactly as many shields as the rival -> tie.
            for (int r = 0; r < t.RoundsPerAge - 1; r++) K.EndRound(sim);
            Soldiers(s, MilitaryApi.Shields(s, PlayerIds.Rival));
            evs = K.EndRound(sim);
            var c2 = evs.OfType<MilitaryCompared>().Single();
            Assert.AreEqual(2, c2.Age);
            Assert.AreEqual(ComparisonResult.Tie, c2.Result);
            Assert.AreEqual(0, c2.PlayerVp);
            Assert.AreEqual(0, c2.RivalVp);
            // Age III: one more shield -> win +5, rival -1.
            for (int r = 0; r < t.RoundsPerAge - 1; r++) K.EndRound(sim);
            Soldiers(s, MilitaryApi.Shields(s, PlayerIds.Rival) - MilitaryApi.Shields(s, PlayerIds.Human) + 1);
            var summary = MilitaryApi.ShieldSummary(s);
            StringAssert.Contains("you would win 5 points", summary.Sentence);
            evs = K.EndRound(sim);
            var c3 = evs.OfType<MilitaryCompared>().Single();
            Assert.AreEqual(ComparisonResult.Win, c3.Result);
            Assert.AreEqual(t.MilitaryWinVp[2], c3.PlayerVp);
            Assert.AreEqual(t.MilitaryLossVp, c3.RivalVp);
            Assert.AreEqual(3, s.Strategy.Comparisons.Count);
            Assert.AreEqual(t.MilitaryLossVp + 0 + t.MilitaryWinVp[2], StrategyApi.Score(s, 0).Military);
            Assert.AreEqual(t.MilitaryWinVp[0] + 0 + t.MilitaryLossVp, StrategyApi.Score(s, 1).Military);
            Assert.IsTrue(s.Strategy.GameOver, "Age III ends the game");
        }

        [Test]
        public void MilitaryCards_AddShields_AndTheComparisonFlips()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99;
            K.Step(sim);
            K.FinishDraft(sim);
            Soldiers(s, MilitaryApi.Shields(s, PlayerIds.Rival));
            Assert.AreEqual(MilitaryApi.Shields(s, PlayerIds.Rival), MilitaryApi.Shields(s, PlayerIds.Human));
            K.ForceIntoHand(s, "palisade");
            s.Player(0).Coin = 2;
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "palisade"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(MilitaryApi.Shields(s, PlayerIds.Rival) + 1, MilitaryApi.Shields(s, PlayerIds.Human));
            List<SimEvent> evs = null;
            for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge; r++) evs = K.EndRound(sim);
            Assert.AreEqual(ComparisonResult.Win, evs.OfType<MilitaryCompared>().Single().Result);
        }

        // ---------------- Cards: five families with real hooks ----------------

        [Test]
        public void Cards_FiveFamilies_ThreePlusPerAge_EachWithAHook_CostsByAge()
        {
            Assert.GreaterOrEqual(CardTable.All.Length, 15);
            foreach (CardFamily f in Enum.GetValues(typeof(CardFamily)))
                Assert.IsNotEmpty(CardTable.ForFamily(f), "family " + f);
            for (int age = 1; age <= 3; age++)
            {
                var cards = CardTable.ForAge(age);
                Assert.GreaterOrEqual(cards.Count, 3, "age " + age);
                foreach (var c in cards)
                {
                    int hooks = c.ForestBatch + c.HillsBatch + c.FieldsBatch + c.PastureBatch + c.MountainBatch + c.AllBatch + c.ExtraOutput
                                + c.TradeRateBonus + c.CoinPerRound + c.RoadSegments + c.Vp + c.VpPerCity + c.Shields + Math.Abs(c.TrainingPercent)
                                + c.DamageBonus + c.HeroMoves + c.InfectionMinus + c.DemonCapBonus;
                    Assert.Greater(hooks, 0, c.Id + " has an effect");
                    bool fits = age == 1 ? (c.Cost[(int)Ware.Rations] > 0 || c.CoinCost > 0)
                        : age == 2 ? c.Cost[(int)Ware.Rations] > 0 && c.Cost[(int)Ware.Bricks] > 0
                        : c.Cost[(int)Ware.Rations] > 0 && (c.Cost[(int)Ware.Iron] > 0);
                    Assert.IsTrue(fits, c.Id + " costs follow the age");
                }
            }
            foreach (var c in CardTable.All)
            {
                bool ok = c.Family switch
                {
                    CardFamily.RawAndCrafted => c.ForestBatch + c.HillsBatch + c.FieldsBatch + c.PastureBatch + c.MountainBatch + c.AllBatch + c.ExtraOutput > 0,
                    CardFamily.Commerce => c.TradeRateBonus + c.CoinPerRound + c.RoadSegments > 0,
                    CardFamily.Civic => c.Vp + c.VpPerCity > 0,
                    CardFamily.Military => c.Shields > 0,
                    _ => c.HeroMoves + c.InfectionMinus + c.DemonCapBonus > 0,
                };
                Assert.IsTrue(ok, c.Id + " has its family's hook");
            }
        }

        [Test]
        public void CardHook_Crafted_CharcoalGuildMakesOneMoreCoal()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            int kiln = K.Build(sim, BuildingType.CharcoalKiln);
            K.ForceIntoHand(s, "charcoal_guild");
            K.Give(s, Ware.Rations, 1, Ware.Bricks, 2);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "charcoal_guild"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(1, s.Economy.Bonus.ExtraOutput[(int)BuildingType.CharcoalKiln]);
            StringAssert.Contains("1 logs -> 2 coal", EconomyApi.RecipeText(s, BuildingType.CharcoalKiln));
            K.Give(s, Ware.Logs, 2);
            var evs = new List<SimEvent>();
            StepUntil(sim, () => evs.OfType<ProductionCycle>().Any(p => p.BuildingId == kiln), 600, evs);
            Assert.AreEqual(2, evs.OfType<ProductionCycle>().First(p => p.BuildingId == kiln).Amount, "1 logs -> 2 coal");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void CardHook_Commerce_TollGateCoinAndRoadGuildCheaperRoads()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.ForceIntoHand(s, "toll_gate");
            K.ForceIntoHand(s, "road_guild");
            s.Player(0).Coin = 2;
            K.Give(s, Ware.Rations, 1, Ware.Bricks, 1);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "toll_gate"), CityVertex = s.Board.HomeCityVertex });
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "road_guild"), CityVertex = s.Board.HomeCityVertex });
            int coin = s.Player(0).Coin;
            K.EndRound(sim);
            Assert.AreEqual(coin + 2, s.Player(0).Coin, "Toll Gate: 2 coin at the round start");
            Assert.AreEqual(EconomyTuning.W(Ware.Planks, 1, Ware.Bricks, 1), EconomyApi.RoadCostFor(s, 6), "one payment now covers 6 segments");
        }

        [Test]
        public void CardHook_Civic_CensusHallCountsCities()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.ForceIntoHand(s, "census_hall");
            K.Give(s, Ware.Rations, 1, Ware.Iron, 1);
            int before = StrategyApi.Score(s, 0).Cards;
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "census_hall"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(before + 1, StrategyApi.Score(s, 0).Cards, "1 city");
            CityOn(sim, Terrain.Pasture);
            Assert.AreEqual(before + 2, StrategyApi.Score(s, 0).Cards, "2 cities");
        }

        [Test]
        public void CardHook_Lore_BeaconTowerOneFewerDraw_RangerPostOneMoreMove()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            Assert.AreEqual(2, StrategyApi.InfectionCount(s));
            K.ForceIntoHand(s, "beacon_tower");
            K.ForceIntoHand(s, "ranger_post");
            K.Give(s, Ware.Rations, 1, Ware.Iron, 1);
            s.Player(0).Coin = 2;
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "beacon_tower"), CityVertex = s.Board.HomeCityVertex });
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "ranger_post"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(1, StrategyApi.InfectionCount(s));
            K.EndRound(sim);
            Assert.AreEqual(s.Strategy.Tuning.HeroMovesPerRound + 1, s.Strategy.HeroMovesLeft);
        }

        // ---------------- Commission ----------------

        [Test]
        public void Commission_NeedsTheSixChainBuildings_And10Planks10Bricks()
        {
            var sim = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var s = sim.State;
            s.Economy.Tuning.TicksPerEdge = 5;
            int forest = EconomyTestKit.HomeHex(s, Terrain.Forest), hills = EconomyTestKit.HomeHex(s, Terrain.Hills);
            EconomyTestKit.Build(sim, BuildingType.Woodcutter, EconomyTestKit.Touches(s, forest));
            EconomyTestKit.Build(sim, BuildingType.ClayPit, EconomyTestKit.Touches(s, hills));
            EconomyTestKit.Build(sim, BuildingType.Sawmill);
            EconomyTestKit.Build(sim, BuildingType.CharcoalKiln);
            StepUntil(sim, () => s.Economy.Buildings[1].State == BuildingState.Active, 1000);
            EconomyApi.AddToStorehouse(s, Ware.Planks, 20);
            EconomyApi.AddToStorehouse(s, Ware.Bricks, 20);
            sim.Step(5);
            Assert.IsFalse(s.Economy.Commission.Complete, "no brick kiln yet");
            var evs = new List<SimEvent>();
            int v = EconomyTestKit.ChooseVertex(s, BuildingType.BrickKiln);
            EconomyTestKit.Submit(sim, new PlaceBuilding { Type = BuildingType.BrickKiln, Vertex = v });
            foreach (int e in EconomyTestKit.RoadsToConnect(s, v)) { sim.Step(); evs.AddRange(sim.DrainEvents()); EconomyTestKit.Submit(sim, new PlaceRoad { Edge = e }); }
            StepUntil(sim, () => s.Economy.Commission.Complete, 2000, evs);
            Assert.AreEqual(1, evs.OfType<CommissionCompleted>().Count());
            Assert.AreEqual(evs.OfType<SiteCompleted>().Single(x => x.Type == BuildingType.BrickKiln).Tick, s.Economy.Commission.CompletedTick, "complete the tick the last building opens");
        }

        // ---------------- Save of the new state ----------------

        [Test]
        public void Save_RoundTripsComparisonsEpidemicsBonusesAndRefusesOldVersions()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99;
            K.Step(sim);
            K.FinishDraft(sim);
            K.ForceIntoHand(s, "charcoal_guild");
            K.Give(s, Ware.Rations, 1, Ware.Bricks, 2);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "charcoal_guild"), CityVertex = s.Board.HomeCityVertex });
            for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge; r++) K.EndRound(sim);
            K.FinishDraft(sim);
            Assert.AreEqual(1, s.Strategy.Comparisons.Count);
            Assert.AreEqual(1, s.Strategy.Epidemics);
            string json = SaveCodec.ToJson(s);
            var b = SaveCodec.Load(json, out _);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State));
            Assert.AreEqual(1, b.State.Strategy.Comparisons.Count);
            Assert.AreEqual(1, b.State.Economy.Bonus.ExtraOutput[(int)BuildingType.CharcoalKiln]);
            Assert.Throws<FormatException>(() => SaveCodec.FromJson(json.Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":1")));
        }

        // ---------------- UI helpers ----------------

        [Test]
        public void UiHelpers_BuildMenuRecipesLegalFlagsWaitingForForecastShieldsScores()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            var menu = EconomyApi.BuildMenu(s);
            Assert.AreEqual(BuildingDefs.Count - 1, menu.Count, "every placeable type");
            var kiln = EconomyApi.DescribeType(s, BuildingType.BrickKiln);
            Assert.AreEqual("Brick kiln", kiln.Name);
            Assert.AreEqual("2 clay and 1 coal -> 2 bricks (4 s)", kiln.RecipeText);
            StringAssert.StartsWith("Needs: 2 clay and 1 coal per cycle", kiln.NeedsText);
            Assert.AreEqual("Makes: 2 bricks per cycle.", kiln.MakesText);
            var mine = EconomyApi.DescribeType(s, BuildingType.Mine);
            StringAssert.Contains("1 rations per 3 ore", mine.NeedsText);
            Assert.AreEqual("mountain", mine.TerrainText);
            StringAssert.Contains("2 logs", EconomyApi.DescribeType(s, BuildingType.Woodcutter).RecipeText);
            foreach (int v in EconomyApi.LegalBuildFlags(s, BuildingType.ClayPit))
                Assert.IsTrue(EconomyApi.CheckPlacement(s, 0, BuildingType.ClayPit, v).Ok);
            Assert.IsNotEmpty(EconomyApi.LegalBuildFlags(s, BuildingType.ClayPit));
            Assert.IsEmpty(EconomyApi.LegalBuildFlags(s, BuildingType.Mine), "no mountain in the home territory");
            var w = EconomyApi.WaitingFor(s, 1);
            Assert.IsFalse(string.IsNullOrEmpty(w.Sentence));
            var f = StrategyApi.Forecast(s);
            Assert.AreEqual(2, f.NextDraws);
            Assert.AreEqual(s.Strategy.InfectionDeck.Count, f.DeckSize);
            StringAssert.Contains("breaches 0 of " + s.Strategy.Tuning.BreachLimit, f.Sentence);
            var sh = MilitaryApi.ShieldSummary(s);
            Assert.AreEqual(MilitaryApi.Shields(s, PlayerIds.Rival), sh.RivalShields);
            StringAssert.StartsWith("Shields 0", sh.Sentence);
            var scores = StrategyApi.Scores(s);
            Assert.AreEqual(2, scores[0].Total);
            StringAssert.Contains("military 0", StrategyApi.ScoreText(s, 1));
            foreach (CardFamily fam in Enum.GetValues(typeof(CardFamily)))
            {
                var cards = StrategyApi.CardsByFamily(s, fam);
                Assert.IsNotEmpty(cards);
                Assert.IsTrue(cards.All(c => c.Family == fam && c.Colour == CardTable.FamilyColour(fam) && !string.IsNullOrEmpty(c.EffectText)));
            }
        }
    }

    /// <summary>Test access to a field squad (the military module only creates them at a barracks).</summary>
    internal static class MilitaryLogicAccess
    {
        internal static Squad NewPlayerSquad(WorldState s, int vertex)
        {
            var sq = new Squad { Id = s.Military.Squads.Count, Owner = PlayerIds.Human, Kind = SquadKind.Field, At = vertex };
            s.Military.Squads.Add(sq);
            return sq;
        }
    }
}
