using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;
using K = World.Sim.Tests.StrategyTestKit;

namespace World.Sim.Tests
{
    /// <summary>G4 strategy rows: rounds and ages, cities, market, demons and hero, draft and cards, wonder, endings.</summary>
    public class StrategyTests
    {
        // ---------------- Rounds and ages ----------------

        [Test]
        public void Prologue_AgesStartAfterTheCommission()
        {
            var sim = K.NewSim(ageAtOnce: false);
            var s = sim.State;
            K.Step(sim, 2);
            Assert.IsFalse(s.Strategy.Started);
            Assert.IsFalse(StrategyApi.DraftPending(s));
            StringAssert.StartsWith("Prologue", StrategyApi.Headline(s));
            K.Do(sim, new EndRound());
            Assert.IsFalse(s.Strategy.Started, "commission not done yet");
            Assert.AreEqual(StrategyRefusals.Prologue, sim.Check(new FoundCity { Vertex = 0 }).Reason, "cities wait for Age I");
            s.Economy.Commission.Complete = true;
            var evs = K.Do(sim, new EndRound());
            Assert.IsTrue(s.Strategy.Started);
            Assert.AreEqual(1, s.Strategy.Age);
            Assert.AreEqual(3, s.Strategy.FirstRound);
            Assert.AreEqual(1, evs.OfType<AgeStarted>().Count());
            Assert.IsTrue(StrategyApi.DraftPending(s));
        }

        [Test]
        public void Rounds_ThreeAgesOfTwo_ThenTheGameEndsByPoints()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.Intensify = false; // no hero play here: keep demons from breaching (tested separately)
            K.Step(sim);
            var ages = new List<int>();
            for (int r = 0; r < 6; r++)
            {
                ages.Add(s.Strategy.Age * 10 + s.Strategy.RoundInAge);
                K.EndRound(sim);
            }
            CollectionAssert.AreEqual(new[] { 11, 12, 21, 22, 31, 32 }, ages);
            Assert.IsTrue(s.Strategy.GameOver);
            Assert.AreEqual(OutcomeReasons.AgeEnd, s.Strategy.OutcomeReason);
            var r2 = sim.Submit(new EndRound());
            Assert.AreEqual(StrategyRefusals.GameOver, r2.Reason);
        }

        // ---------------- Cities ----------------

        [Test]
        public void City_LegalAccepted_TooCloseAndNoRoadRefusedWithTheRule()
        {
            var sim = K.NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            int home = s.Board.HomeCityVertex;
            K.Give(s, Ware.Planks, 20, Ware.Stone, 10, Ware.Grain, 3);
            K.Step(sim);

            // Cities stand on corners only: the demo road's intermediate flags are refused.
            var notCorner = sim.Check(new FoundCity { Vertex = s.Economy.DemoVertexA });
            Assert.AreEqual(StrategyRefusals.BadVertex, notCorner.Reason);
            Assert.AreEqual("A city must stand on a hex corner.", notCorner.Message);

            // Too close: the corner one hex edge from home, reached by extending the demo road.
            int corner1 = K.DemoCorner(s);
            K.RoadTo(sim, corner1);
            var tooClose = sim.Check(new FoundCity { Vertex = corner1 });
            Assert.AreEqual(StrategyRefusals.TooClose, tooClose.Reason);
            StringAssert.Contains("next to another city", tooClose.Message);

            // No road: a free corner two hex edges from every city with no road touching it.
            int noRoad = topo.Corners.First(v =>
                !EconomyApi.IsVertexOccupied(s, v) &&
                s.Economy.Cities.All(c => topo.CornerDistance(c.Vertex, v) >= 2) &&
                topo.VertexEdges(v).All(e => !s.Economy.Roads[e]));
            var nr = sim.Check(new FoundCity { Vertex = noRoad });
            Assert.AreEqual(StrategyRefusals.NoRoad, nr.Reason);
            StringAssert.Contains("road", nr.Message);

            // Occupied: the rival outpost's corner.
            Assert.AreEqual(StrategyRefusals.Occupied, sim.Check(new FoundCity { Vertex = s.Board.RivalOutpostVertex }).Reason);

            // Legal: one more hex edge of road beyond corner1 reaches a corner two hex edges from home.
            int site = K.RoadToSecondCitySite(sim);
            Assert.AreEqual(2, topo.CornerDistance(s.Board.HomeCityVertex, site));
            CollectionAssert.Contains(StrategyApi.LegalCitySites(s), site);
            int planks = EconomyApi.StorehouseCount(s, Ware.Planks);
            var evs = K.Do(sim, new FoundCity { Vertex = site });
            Assert.AreEqual(1, evs.OfType<CityFounded>().Count());
            Assert.AreEqual(planks - s.Strategy.Tuning.CityCost[(int)Ware.Planks], EconomyApi.StorehouseCount(s, Ware.Planks));
            Assert.AreEqual(4, StrategyApi.Score(s, PlayerIds.Human).Cities);
            Assert.IsTrue(EconomyApi.IsInTerritory(s, PlayerIds.Human, site));
            // Its territory reaches 4 segments around its own corner.
            int far = Enumerable.Range(0, topo.VertexCount).First(v => topo.Distance(site, v) == 4 && topo.Distance(s.Board.HomeCityVertex, v) > 4);
            Assert.IsTrue(EconomyApi.IsInTerritory(s, PlayerIds.Human, far));

            // The new city is now "another city" for its neighbouring corners.
            int next = topo.CornerNeighbours(site).First(v => v != corner1);
            K.RoadTo(sim, next);
            Assert.AreEqual(StrategyRefusals.TooClose, sim.Check(new FoundCity { Vertex = next }).Reason);
        }

        [Test]
        public void City_CannotAfford_NamesTheShortfall_AndOutpostCountsForDistance()
        {
            var sim = K.NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            K.Step(sim);
            int site = K.RoadToSecondCitySite(sim);
            var r = sim.Check(new FoundCity { Vertex = site });
            Assert.AreEqual(StrategyRefusals.CannotAfford, r.Reason);
            StringAssert.Contains("grain", r.Message);
            Assert.IsTrue(s.Economy.Cities.Any(c => c.Owner == PlayerIds.Rival && c.Vertex == s.Board.RivalOutpostVertex));
            int nearOutpost = topo.CornerNeighbours(s.Board.RivalOutpostVertex)[0];
            Assert.AreEqual(StrategyRefusals.TooClose, StrategyApi.CheckCity(s, nearOutpost).Reason);
        }

        // ---------------- Market ----------------

        [Test]
        public void Trade_FourForOne_Exact_AndRefusals()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            Assert.AreEqual(4, StrategyApi.TradeRate(s));
            int p = EconomyApi.StorehouseCount(s, Ware.Planks), st = EconomyApi.StorehouseCount(s, Ware.Stone);
            K.Do(sim, new Trade { Give = Ware.Planks, Get = Ware.Stone, Count = 2 });
            Assert.AreEqual(p - 8, EconomyApi.StorehouseCount(s, Ware.Planks));
            Assert.AreEqual(st + 2, EconomyApi.StorehouseCount(s, Ware.Stone));
            var r = sim.Check(new Trade { Give = Ware.Ingots, Get = Ware.Stone, Count = 1 });
            Assert.AreEqual(StrategyRefusals.CannotAfford, r.Reason);
            StringAssert.Contains("4 ingots", r.Message);
            Assert.AreEqual(StrategyRefusals.SameWare, sim.Check(new Trade { Give = Ware.Stone, Get = Ware.Stone }).Reason);
            Assert.AreEqual(StrategyRefusals.BadCount, sim.Check(new Trade { Give = Ware.Planks, Get = Ware.Stone, Count = 0 }).Reason);

            // Sell 2 for 1 coin; buy 1 for 3 coin.
            K.Do(sim, new Sell { Ware = Ware.Stone, Count = 2 });
            Assert.AreEqual(1, s.Player(0).Coin);
            Assert.AreEqual(StrategyRefusals.BadCount, sim.Check(new Sell { Ware = Ware.Stone, Count = 3 }).Reason);
            Assert.AreEqual(StrategyRefusals.CannotAfford, sim.Check(new Buy { Ware = Ware.Grain, Count = 1 }).Reason);
            s.Player(0).Coin = 3;
            K.Do(sim, new Buy { Ware = Ware.Grain, Count = 1 });
            Assert.AreEqual(1, EconomyApi.StorehouseCount(s, Ware.Grain));
            Assert.AreEqual(0, s.Player(0).Coin);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Trade_CharterAndMarketImproveTheRate()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.ForceIntoHand(s, "trade_charter");
            s.Player(0).Coin = 2;
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "trade_charter"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(3, StrategyApi.TradeRate(s));
            K.Give(s, Ware.Planks, 10, Ware.Stone, 10);
            K.Build(sim, BuildingType.Market);
            Assert.AreEqual(2, StrategyApi.TradeRate(s));
        }

        // ---------------- Draft and cards ----------------

        [Test]
        public void Draft_ThreeEach_PickPassPick_NoDuplicates_DeterministicRival_DiscardPays()
        {
            var a = K.NewSim();
            var b = K.NewSim();
            var s = a.State;
            K.Step(a);
            K.Step(b);
            var rivalPicksA = new List<int>();
            var rivalPicksB = new List<int>();
            var everDealt = new HashSet<int>();
            for (int age = 1; age <= 3; age++)
            {
                Assert.IsTrue(StrategyApi.DraftPending(s), "draft at the start of age " + age);
                Assert.AreEqual(age, s.Strategy.DraftAge);
                Assert.AreEqual(3, s.Strategy.Sides[0].DraftHand.Count);
                Assert.AreEqual(3, s.Strategy.Sides[1].DraftHand.Count);
                var dealt = s.Strategy.Sides[0].DraftHand.Concat(s.Strategy.Sides[1].DraftHand).ToList();
                Assert.AreEqual(6, dealt.Distinct().Count(), "six different cards dealt");
                foreach (int id in dealt) Assert.IsTrue(everDealt.Add(id), "card dealt twice");
                Assert.IsTrue(dealt.All(id => s.Strategy.Cards[id].Age == age));

                var end = a.Submit(new EndRound());
                Assert.AreEqual(StrategyRefusals.DraftPending, end.Reason, "EndRound refused while a pick is pending");

                var otherHand = s.Strategy.Sides[1].DraftHand.ToList();
                int first = s.Strategy.Sides[0].DraftHand[0];
                var e1 = K.Do(a, new DraftPick { CardId = first });
                K.Do(b, new DraftPick { CardId = b.State.Strategy.Sides[0].DraftHand[0] });
                int rival1 = e1.OfType<DraftPicked>().Single(p => p.PlayerId == PlayerIds.Rival).CardId;
                CollectionAssert.Contains(otherHand, rival1);
                Assert.AreEqual(1, e1.OfType<DraftPassed>().Count());
                Assert.AreEqual(2, s.Strategy.Sides[0].DraftHand.Count, "received the rival's two remaining cards");
                CollectionAssert.IsSubsetOf(s.Strategy.Sides[0].DraftHand, otherHand);
                Assert.AreEqual(StrategyRefusals.NotInDraft, a.Check(new DraftPick { CardId = first }).Reason);

                var e2 = K.Do(a, new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[1] });
                K.Do(b, new DraftPick { CardId = b.State.Strategy.Sides[0].DraftHand[1] });
                Assert.AreEqual(2, e2.OfType<DraftFinished>().Single().DiscardedCards.Length, "one card left in each hand is discarded");
                Assert.IsFalse(StrategyApi.DraftPending(s));
                rivalPicksA.AddRange(e1.Concat(e2).OfType<DraftPicked>().Where(p => p.PlayerId == 1).Select(p => p.CardId));
                rivalPicksB.AddRange(b.DrainEvents().OfType<DraftPicked>().Select(p => p.CardId));
                Assert.IsNull(K.CheckInvariants(s));
                for (int r = 0; r < 2 && !s.Strategy.GameOver; r++)
                {
                    K.EndRound(a);
                    K.EndRound(b);
                }
            }
            Assert.AreEqual(6, s.Strategy.Sides[0].Hand.Count, "two picks per age");
            Assert.AreEqual(6, rivalPicksA.Count);
            CollectionAssert.AreEqual(b.State.Strategy.Sides[1].Hand.Concat(b.State.Strategy.Sides[1].Built.Select(x => x.CardId)).OrderBy(x => x),
                                      rivalPicksA.OrderBy(x => x), "rival picks are deterministic for a seed");

            // Discard pays coin (game over refuses commands, so use a fresh game).
            var c = K.NewSim();
            K.Step(c);
            int pick = c.State.Strategy.Sides[0].DraftHand[0];
            K.FinishDraft(c);
            int coin = c.State.Player(0).Coin;
            var ev = K.Do(c, new DiscardCard { CardId = pick });
            Assert.AreEqual(coin + 3, c.State.Player(0).Coin);
            Assert.AreEqual(3, ev.OfType<CardDiscarded>().Single().Coin);
            Assert.AreEqual(StrategyRefusals.NotInHand, c.Check(new DiscardCard { CardId = pick }).Reason);
        }

        [Test]
        public void Draft_RivalPrefersPointCards()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            // Put the Monument in the rival's age II hand by hand.
            K.EndRound(sim);
            K.EndRound(sim);
            Assert.AreEqual(2, s.Strategy.Age);
            int mon = K.CardOf(s, "monument");
            var hp = s.Strategy.Sides[0].DraftHand;
            var hr = s.Strategy.Sides[1].DraftHand;
            if (hp.Contains(mon))
            {
                int swap = hr[0];
                hp[hp.IndexOf(mon)] = swap;
                hr[0] = mon;
            }
            var evs = K.Do(sim, new DraftPick { CardId = hp[0] });
            Assert.AreEqual(mon, evs.OfType<DraftPicked>().Single(p => p.PlayerId == 1).CardId);
            Assert.IsTrue(s.Strategy.Sides[1].Built.Any(x => x.CardId == mon), "the rival builds its point card");
            Assert.AreEqual(1, StrategyApi.Score(s, PlayerIds.Rival).Cards);
        }

        [Test]
        public void Cards_BuildRules_CostCityAndLimit()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            int home = s.Board.HomeCityVertex;
            foreach (var id in new[] { "lumber_camp", "stone_cutters", "granary", "ranger_post" }) K.ForceIntoHand(s, id);
            Assert.AreEqual(StrategyRefusals.NoCity, sim.Check(new BuildCard { CardId = K.CardOf(s, "lumber_camp"), CityVertex = s.Economy.DemoVertexA }).Reason);
            Assert.AreEqual(StrategyRefusals.NotInHand, sim.Check(new BuildCard { CardId = K.CardOf(s, "great_hall"), CityVertex = home }).Reason);
            K.Give(s, Ware.Planks, 10, Ware.Stone, 5);
            int moves = s.Strategy.HeroMovesLeft;
            foreach (var id in new[] { "lumber_camp", "stone_cutters", "ranger_post" })
                Assert.AreEqual(1, K.Do(sim, new BuildCard { CardId = K.CardOf(s, id), CityVertex = home }).OfType<CardBuilt>().Count(), id);
            Assert.AreEqual(moves + 1, s.Strategy.HeroMovesLeft, "Ranger Post adds a move at once");
            Assert.AreEqual(StrategyRefusals.CityFull, sim.Check(new BuildCard { CardId = K.CardOf(s, "granary"), CityVertex = home }).Reason);
            Assert.AreEqual(3, StrategyApi.CardsAtCity(s, 0, home).Count);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void CardEffect_LumberCamp_NextForestBatchIsLarger_SameSeed()
        {
            var with = K.NewSim(ticksPerEdge: 60);
            var without = K.NewSim(ticksPerEdge: 60);
            K.Step(with);
            K.Step(without);
            K.FinishDraft(with);
            K.FinishDraft(without);
            K.ForceIntoHand(with.State, "lumber_camp");
            K.Do(with, new BuildCard { CardId = K.CardOf(with.State, "lumber_camp"), CityVertex = with.State.Board.HomeCityVertex });
            K.Step(without);
            int forest = EconomyTestKit.HomeHex(with.State, Terrain.Forest);
            BatchAuthorised a = null, b = null;
            for (int r = 0; r < 5 && (a == null || b == null); r++)
            {
                a = K.EndRound(with).OfType<BatchAuthorised>().FirstOrDefault(x => x.Hex == forest);
                b = K.EndRound(without).OfType<BatchAuthorised>().FirstOrDefault(x => x.Hex == forest);
            }
            Assert.IsNotNull(a, "forest drawn");
            Assert.IsNotNull(b);
            Assert.AreEqual(a.Tick, b.Tick, "same draw, same seed");
            Assert.Greater(a.Amount, b.Amount, "Lumber Camp enlarges the forest batch");
            Assert.AreEqual(b.Amount + 1, a.Amount, "+2 before the halving (no active forester)");
        }

        // ---------------- Demons and hero ----------------

        [Test]
        public void Infection_DeckIsTheHomeTerritory_EmptyIntermediateFlagsAreSkipped()
        {
            var sim = K.NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            int home = s.Board.HomeCityVertex;
            // The deck: every flag within 4 segments of home except home (16 territory flags - 1).
            Assert.AreEqual(15, s.Strategy.InfectionDeck.Count);
            Assert.IsTrue(s.Strategy.InfectionDeck.All(v => topo.Distance(home, v) <= 4 && v != home));
            K.Step(sim);
            int empty = s.Strategy.InfectionDeck.First(v => !topo.IsCorner(v) && !EconomyApi.IsVertexOccupied(s, v)
                && topo.VertexEdges(v).All(e => !s.Economy.Roads[e]));
            int corner = K.DemoCorner(s);
            RigInfection(s, empty, corner);
            s.Strategy.Tuning.InfectionBase = 1;
            K.EndRound(sim);
            Assert.AreEqual(0, s.Strategy.Demons[empty], "an empty intermediate flag takes no demon");
            Assert.AreEqual(1, s.Strategy.Demons[corner], "the next card is drawn instead");
            CollectionAssert.AreEqual(new[] { corner }, s.Strategy.LastInfection);
            // A road flag does take demons.
            RigInfection(s, s.Economy.DemoVertexA);
            K.EndRound(sim);
            Assert.AreEqual(1, s.Strategy.Demons[s.Economy.DemoVertexA]);
        }

        [Test]
        public void Hero_WalkingAHexEdge_ClearsTheFlagsItPasses()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int corner = K.DemoCorner(s);
            s.Strategy.Demons[s.Economy.DemoVertexA] = 1;
            s.Strategy.Demons[s.Economy.DemoVertexB] = 2;
            s.Strategy.Demons[corner] = 1;
            var evs = K.Do(sim, new MoveHero { Vertex = corner });
            Assert.AreEqual(3, evs.OfType<DemonsCleared>().Count());
            CollectionAssert.AreEqual(new[] { s.Economy.DemoVertexA, s.Economy.DemoVertexB, corner }, evs.OfType<DemonsCleared>().Select(d => d.Vertex).ToArray(), "in walking order");
            Assert.AreEqual(0, StrategyApi.Demons(s).Count);
        }

        private static void RigInfection(WorldState s, params int[] verticesTopFirst)
        {
            s.Strategy.InfectionDeck.Clear();
            for (int i = verticesTopFirst.Length - 1; i >= 0; i--) s.Strategy.InfectionDeck.Add(verticesTopFirst[i]);
            s.Strategy.Tuning.InfectionBase = verticesTopFirst.Length;
        }

        [Test]
        public void Demons_InfectionAddsDemon_FourthAddsBreach()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int v = K.DemoCorner(s);
            RigInfection(s, v, v, v, v);
            var evs = K.EndRound(sim);
            Assert.AreEqual(3, s.Strategy.Demons[v]);
            Assert.AreEqual(3, evs.OfType<DemonSpawned>().Count());
            Assert.AreEqual(1, s.Strategy.Breaches, "a fourth draw on a full flag adds a breach instead");
            Assert.AreEqual(1, evs.OfType<BreachAdvanced>().Count());
            Assert.IsFalse(s.Strategy.GameOver);
        }

        [Test]
        public void Demons_WardStonesRaiseTheBreachThreshold()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.ForceIntoHand(s, "ward_stones");
            K.Give(s, Ware.Stone, 3);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "ward_stones"), CityVertex = s.Board.HomeCityVertex });
            int v = K.DemoCorner(s);
            RigInfection(s, v, v, v, v);
            K.EndRound(sim);
            Assert.AreEqual(4, s.Strategy.Demons[v]);
            Assert.AreEqual(0, s.Strategy.Breaches);
        }

        [Test]
        public void Demons_FourBreaches_EndTheGameAsALoss()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int v = K.DemoCorner(s);
            RigInfection(s, v, v, v, v, v, v, v);
            var evs = K.EndRound(sim);
            Assert.AreEqual(4, s.Strategy.Breaches);
            Assert.IsTrue(s.Strategy.GameOver);
            Assert.AreEqual(GameOutcome.Defeat, s.Strategy.Outcome);
            Assert.AreEqual(OutcomeReasons.Breach, s.Strategy.OutcomeReason);
            Assert.AreEqual(1, evs.OfType<GameEnded>().Count());
            StringAssert.StartsWith("Defeat", StrategyApi.Headline(s));
        }

        [Test]
        public void Demons_BlockADelivery_HeroArrivalClearsIt_AndTheDeliveryResumes()
        {
            var sim = K.NewSim(ticksPerEdge: 60);
            var s = sim.State;
            var topo = s.Board.Topology;
            int b = s.Economy.DemoVertexB, corner = K.DemoCorner(s);
            K.Step(sim);
            K.FinishDraft(sim);
            // A sawmill site one segment beyond the demo corner: its deliveries walk storehouse -> A -> B -> corner -> site.
            K.Do(sim, new PlaceRoad { Edge = topo.EdgeBetween(b, corner) });
            int c = topo.VertexNeighbours(corner).First(v => v != b && EconomyApi.CheckPlacement(s, 0, BuildingType.Sawmill, v).Ok);
            K.Do(sim, new PlaceRoad { Edge = topo.EdgeBetween(corner, c) });
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Vertex = c });
            int siteId = EconomyApi.BuildingAt(s, c);
            K.Step(sim, 10);
            Assert.IsTrue(s.Economy.Jobs.Any(j => j.DestId == siteId && j.State != JobState.Waiting), "a carrier is on its way");

            // Infection puts a demon on the corner (demons sit on corners) while the carriers are still before it.
            RigInfection(s, corner);
            var evs = K.EndRound(sim);
            Assert.AreEqual(1, s.Strategy.Demons[corner]);
            evs.AddRange(K.Step(sim, 200));
            var stalls = evs.OfType<JobStalled>().Where(j => j.Reason == Reasons.DemonOnRoute).ToList();
            Assert.IsNotEmpty(stalls, "a delivery reports demon_on_route");
            // A loaded carrier waits on the demo road (A or B) before the corner, with its ware (jobs not yet picked up may be released and re-created).
            var carrying = s.Economy.Jobs.First(j => j.State == JobState.Carrying && j.DestId == siteId && stalls.Any(x => x.JobId == j.Id));
            int stalledJob = carrying.Id;
            CollectionAssert.Contains(new[] { s.Economy.DemoVertexA, b }, s.Economy.Carriers[carrying.CarrierId].AtVertex, "stopped on the demo road before the corner");
            Assert.AreEqual(Reasons.DemonOnRoute, EconomyApi.DescribeJob(s, stalledJob).Reason);
            Assert.IsFalse(evs.OfType<WareDelivered>().Any(d => d.BuildingId == siteId));

            // The hero moves one hex edge, home -> corner; arriving clears it.
            var hev = K.Do(sim, new MoveHero { Vertex = corner });
            Assert.AreEqual(1, hev.OfType<DemonsCleared>().Single().Count);
            Assert.AreEqual(0, s.Strategy.Demons[corner]);
            hev.AddRange(K.Step(sim, 400));
            Assert.IsTrue(hev.OfType<JobResumed>().Any(j => j.JobId == stalledJob), "the stalled delivery resumes");
            Assert.AreEqual(1, hev.OfType<WareDelivered>().Count(d => d.JobId == stalledJob), "and arrives once");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Hero_MovesOnlyToNeighbours_AndRunsOutOfMoves()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int home = s.Board.HomeCityVertex;
            int n = K.DemoCorner(s);
            var topo = s.Board.Topology;
            CollectionAssert.AreEquivalent(topo.CornerNeighbours(home).ToList(), StrategyApi.HeroMoveTargets(s));
            Assert.AreEqual(StrategyRefusals.NotAdjacent, sim.Check(new MoveHero { Vertex = s.Economy.DemoVertexA }).Reason, "not to an intermediate flag");
            int twoAway = topo.Corners.First(v => topo.CornerDistance(home, v) == 2);
            Assert.AreEqual(StrategyRefusals.NotAdjacent, sim.Check(new MoveHero { Vertex = twoAway }).Reason, "one hex edge per move");
            for (int i = 0; i < 4; i++) K.Do(sim, new MoveHero { Vertex = i % 2 == 0 ? n : home });
            var r = sim.Check(new MoveHero { Vertex = n });
            Assert.AreEqual(Reasons.NoTurnAction, r.Reason);
            Assert.AreEqual(Reasons.Sentence(Reasons.NoTurnAction), r.Message);
            K.EndRound(sim);
            Assert.AreEqual(4, s.Strategy.HeroMovesLeft, "moves refresh each round");
        }

        [Test]
        public void Demons_DrawnOntoTheHero_AreClearedAtOnce_AndADemonCityLosesItsBatches()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int home = s.Board.HomeCityVertex;
            RigInfection(s, home);
            var evs = K.EndRound(sim);
            Assert.AreEqual(1, evs.OfType<DemonRepelled>().Count());
            Assert.AreEqual(0, s.Strategy.Demons[home]);

            // Move the hero away and let a demon sit on the home city: no batches there next round.
            K.Do(sim, new MoveHero { Vertex = K.DemoCorner(s) });
            RigInfection(s, home);
            K.EndRound(sim);
            Assert.AreEqual(1, s.Strategy.Demons[home]);
            var next = K.EndRound(sim);
            Assert.AreEqual(0, next.OfType<BatchAuthorised>().Count(), "a demon on the only city flag stops its production");
        }

        // ---------------- Wonder ----------------

        [Test]
        public void Wonder_StagesNeedDeliveredWares_MissingNamed()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            Assert.AreEqual(StrategyRefusals.NoWonder, sim.Check(new BuildWonderStage()).Reason);
            K.Give(s, Ware.Planks, 10, Ware.Stone, 4);
            int site = K.Build(sim, BuildingType.WonderSite);
            EconomyApi.TrySpendFromStorehouse(s, Ware.Stone, EconomyApi.StorehouseCount(s, Ware.Stone));
            int stoneNow = 0;
            K.Do(sim, new BuildWonderStage());
            Assert.AreEqual(StrategyRefusals.StageInProgress, sim.Check(new BuildWonderStage()).Reason);
            K.Step(sim, 300);
            Assert.AreEqual(0, s.Strategy.Wonder.StagesDone);
            var info = StrategyApi.Wonder(s);
            StringAssert.Contains("stone", info.Status);
            var stage = s.Strategy.Tuning.WonderStages[0];
            StringAssert.Contains((stage[(int)Ware.Stone] - stoneNow) + " stone", info.Status);
            Assert.AreEqual(stage[(int)Ware.Planks], s.Economy.Buildings[site].Stock[(int)Ware.Planks], "planks arrived at the wonder site");

            // Stone in the storehouse is not enough: the stage completes only once it is delivered.
            K.Give(s, Ware.Stone, stage[(int)Ware.Stone]);
            var evs = K.Step(sim, 1);
            Assert.AreEqual(0, s.Strategy.Wonder.StagesDone, "not complete before delivery");
            evs.AddRange(K.Step(sim, 300));
            Assert.AreEqual(1, s.Strategy.Wonder.StagesDone);
            var done = evs.OfType<WonderStageCompleted>().Single();
            Assert.AreEqual(1, done.Vp);
            int lastDelivery = evs.OfType<WareDelivered>().Where(d => d.BuildingId == site).Max(d => (int)d.Tick);
            Assert.GreaterOrEqual(done.Tick, lastDelivery);
            Assert.AreEqual(1, StrategyApi.Score(s, 0).Wonder);
            Assert.AreEqual(stage[(int)Ware.Stone], s.Strategy.Ledger.Wonder[(int)Ware.Stone]);
            Assert.IsNull(K.CheckInvariants(s));
        }

        // ---------------- Victory paths ----------------

        [Test]
        public void Victory_ScriptedGameReachesEightPoints_AndWins()
        {
            var sim = K.NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            K.Step(sim);
            K.FinishDraft(sim);
            K.Give(s, Ware.Planks, 60, Ware.Stone, 40, Ware.Grain, 4, Ware.Rations, 4, Ware.Ingots, 4);
            // Two more cities along a road west from the forester site.
            for (int c = 0; c < 2; c++)
            {
                var legal = StrategyApi.LegalCitySites(s);
                int site;
                if (legal.Count > 0) site = legal[0];
                else
                {
                    // Extend roads toward the closest free flag that only lacks a road.
                    site = Enumerable.Range(0, topo.VertexCount).Where(v => StrategyApi.CheckCity(s, v).Reason == StrategyRefusals.NoRoad)
                        .OrderBy(v => EconomyTestKit.PathToNetwork(s, v).Length).ThenBy(v => v).First();
                    K.RoadTo(sim, site);
                }
                K.Do(sim, new FoundCity { Vertex = site });
            }
            Assert.AreEqual(6, StrategyApi.Score(s, 0).Total);
            K.Build(sim, BuildingType.WonderSite);
            K.Do(sim, new BuildWonderStage());
            K.Step(sim, 400);
            Assert.AreEqual(7, StrategyApi.Score(s, 0).Total);
            Assert.IsFalse(s.Strategy.GameOver);
            K.Do(sim, new BuildWonderStage());
            var evs = K.Step(sim, 400);
            var end = evs.OfType<GameEnded>().Single();
            Assert.AreEqual(GameOutcome.Victory, end.Outcome);
            Assert.AreEqual(OutcomeReasons.Points, end.Reason);
            Assert.AreEqual(9, end.PlayerVp);
            Assert.AreEqual(end.Tick, evs.OfType<WonderStageCompleted>().Last().Tick, "checked at the tick boundary of the stage");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Ending_AfterAgeThree_ComparesPoints()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.Intensify = false; // no hero play here: keep demons from breaching (tested separately)
            K.Step(sim);
            GameEnded end = null;
            for (int r = 0; r < 6; r++) end = K.EndRound(sim).OfType<GameEnded>().FirstOrDefault() ?? end;
            Assert.IsNotNull(end);
            Assert.AreEqual(OutcomeReasons.AgeEnd, end.Reason);
            var p = StrategyApi.Score(s, 0).Total;
            var rv = StrategyApi.Score(s, 1).Total;
            Assert.AreEqual(2, p, "a passive player keeps only the home city");
            Assert.Greater(rv, p);
            Assert.AreEqual(GameOutcome.Defeat, end.Outcome);
            TestContext.WriteLine("passive: player " + p + " rival " + rv + " -> " + StrategyApi.Headline(s));
        }
    }
}
