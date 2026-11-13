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
        public void Rounds_ThreeAgesOfThree_ThenTheGameEndsByPoints()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99; // no hero play here: keep demons from ending the game (tested separately)
            s.Strategy.Tuning.VictoryPoints = 99; // the rival would reach the target on the last bell: test the age-end comparison
            K.Step(sim);
            var ages = new List<int>();
            for (int r = 0; r < 9; r++)
            {
                ages.Add(s.Strategy.Age * 10 + s.Strategy.RoundInAge);
                K.EndRound(sim);
            }
            CollectionAssert.AreEqual(new[] { 11, 12, 13, 21, 22, 23, 31, 32, 33 }, ages, "three ages of three rounds");
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
            K.Give(s, Ware.Planks, 20, Ware.Bricks, 10, Ware.Rations, 3);
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
            Assert.IsTrue(sim.Check(new FoundCity { Vertex = site }).Ok, "the starting stock pays for the first city");
            EconomyApi.TrySpendFromStorehouse(s, Ware.Rations, EconomyApi.StorehouseCount(s, Ware.Rations) - 1);
            var r = sim.Check(new FoundCity { Vertex = site });
            Assert.AreEqual(StrategyRefusals.CannotAfford, r.Reason);
            StringAssert.Contains("1 rations", r.Message);
            Assert.AreEqual("3 planks, 3 bricks, 2 rations", StrategyApi.CityCostText(s));
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
            s.Player(0).Coin = 0; // without the commission coin of Age I, to count the coin exactly
            Assert.AreEqual(4, StrategyApi.TradeRate(s));
            int p = EconomyApi.StorehouseCount(s, Ware.Planks), st = EconomyApi.StorehouseCount(s, Ware.Bricks);
            K.Do(sim, new Trade { Give = Ware.Planks, Get = Ware.Bricks, Count = 2 });
            Assert.AreEqual(p - 8, EconomyApi.StorehouseCount(s, Ware.Planks));
            Assert.AreEqual(st + 2, EconomyApi.StorehouseCount(s, Ware.Bricks));
            var r = sim.Check(new Trade { Give = Ware.Weapons, Get = Ware.Bricks, Count = 1 });
            Assert.AreEqual(StrategyRefusals.CannotAfford, r.Reason);
            StringAssert.Contains("4 weapons", r.Message);
            Assert.AreEqual(StrategyRefusals.SameWare, sim.Check(new Trade { Give = Ware.Bricks, Get = Ware.Bricks }).Reason);
            Assert.AreEqual(StrategyRefusals.BadCount, sim.Check(new Trade { Give = Ware.Planks, Get = Ware.Bricks, Count = 0 }).Reason);

            // Sell 2 for 1 coin; buy 1 for 3 coin.
            K.Do(sim, new Sell { Ware = Ware.Bricks, Count = 2 });
            Assert.AreEqual(1, s.Player(0).Coin);
            Assert.AreEqual(StrategyRefusals.BadCount, sim.Check(new Sell { Ware = Ware.Bricks, Count = 3 }).Reason);
            Assert.AreEqual(StrategyRefusals.CannotAfford, sim.Check(new Buy { Ware = Ware.Wheat, Count = 1 }).Reason);
            s.Player(0).Coin = 3;
            K.Do(sim, new Buy { Ware = Ware.Wheat, Count = 1 });
            Assert.AreEqual(1, EconomyApi.StorehouseCount(s, Ware.Wheat));
            Assert.AreEqual(0, s.Player(0).Coin);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Trade_MerchantGuildAndMarketImproveTheRate()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            K.ForceIntoHand(s, "merchant_guild");
            K.Give(s, Ware.Iron, 1);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "merchant_guild"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(3, StrategyApi.TradeRate(s));
            K.Give(s, Ware.Planks, 10, Ware.Bricks, 10);
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
            // No hero play here: keep the demons from ending the game before the third draft (tested separately).
            a.State.Strategy.Tuning.BreachLimit = 99;
            b.State.Strategy.Tuning.BreachLimit = 99;
            // The draft with every pick at the age start (DraftPickPerRound off, the original three cards and two picks);
            // the tuned one-pick-per-round draft is Draft_OnePickPerRound_FourEach_PassedBetweenRounds.
            foreach (var x in new[] { a, b })
            {
                x.State.Strategy.Tuning.DraftPickPerRound = false;
                x.State.Strategy.Tuning.DraftHandSize = 3;
                x.State.Strategy.Tuning.DraftPicks = 2;
            }
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
                for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge && !s.Strategy.GameOver; r++)
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
        public void Draft_OnePickPerRound_FourEach_PassedBetweenRounds()
        {
            var a = K.NewSim();
            var b = K.NewSim();
            var s = a.State;
            foreach (var x in new[] { a, b }) x.State.Strategy.Tuning.BreachLimit = 99;
            Assert.IsTrue(s.Strategy.Tuning.DraftPickPerRound);
            K.Step(a);
            K.Step(b);
            var everDealt = new HashSet<int>();
            var rivalA = new List<int>();
            var rivalB = new List<int>();
            for (int age = 1; age <= 3 && !s.Strategy.GameOver; age++)
            {
                Assert.AreEqual(age, s.Strategy.Age);
                Assert.AreEqual(4, s.Strategy.Sides[0].DraftHand.Count, "four cards each");
                Assert.AreEqual(4, s.Strategy.Sides[1].DraftHand.Count);
                foreach (int id in s.Strategy.Sides[0].DraftHand.Concat(s.Strategy.Sides[1].DraftHand)) Assert.IsTrue(everDealt.Add(id), "card dealt twice");
                for (int pick = 1; pick <= 3 && !s.Strategy.GameOver; pick++)
                {
                    Assert.AreEqual(pick, s.Strategy.RoundInAge, "one pick in each round of the age");
                    Assert.IsTrue(StrategyApi.DraftPending(s));
                    Assert.AreEqual(pick, s.Strategy.DraftStep);
                    Assert.AreEqual(5 - pick, s.Strategy.Sides[0].DraftHand.Count);
                    Assert.AreEqual(StrategyRefusals.DraftPending, a.Submit(new EndRound()).Reason, "the bell waits for the pick");
                    var other = s.Strategy.Sides[1].DraftHand.ToList();
                    var evs = K.Do(a, new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[0] });
                    var evb = K.Do(b, new DraftPick { CardId = b.State.Strategy.Sides[0].DraftHand[0] });
                    rivalA.AddRange(evs.OfType<DraftPicked>().Where(p => p.PlayerId == PlayerIds.Rival).Select(p => p.CardId));
                    rivalB.AddRange(evb.OfType<DraftPicked>().Where(p => p.PlayerId == PlayerIds.Rival).Select(p => p.CardId));
                    Assert.IsFalse(StrategyApi.DraftPending(s), "no second pick in the same round");
                    if (pick < 3)
                    {
                        Assert.AreEqual(1, evs.OfType<DraftPassed>().Count());
                        Assert.AreEqual(pick + 1, s.Strategy.DraftNextStep);
                        CollectionAssert.IsSubsetOf(s.Strategy.Sides[0].DraftHand, other, "you hold the hand the rival passed, for the next round");
                        var next = K.Do(a, new EndRound());
                        K.Do(b, new EndRound());
                        var resumed = next.OfType<DraftResumed>().Single();
                        Assert.AreEqual(pick + 1, resumed.Step);
                        Assert.AreEqual(age, resumed.Age);
                    }
                    else
                    {
                        Assert.AreEqual(2, evs.OfType<DraftFinished>().Single().DiscardedCards.Length, "the last card in each hand is discarded");
                        Assert.AreEqual(0, s.Strategy.DraftNextStep);
                        K.Do(a, new EndRound());
                        K.Do(b, new EndRound());
                    }
                    Assert.IsNull(K.CheckInvariants(s));
                }
            }
            Assert.AreEqual(9, s.Strategy.Sides[0].Hand.Count + s.Strategy.Sides[0].Built.Count, "three picks per age");
            Assert.AreEqual(9, rivalA.Count);
            CollectionAssert.AreEqual(rivalB, rivalA, "rival picks are deterministic for a seed");
        }

        [Test]
        public void Draft_RivalBuildsPointAndShieldCards_OnlyWhenTuned()
        {
            foreach (int maxBuilt in new[] { 0, 3 })
            {
                var sim = K.NewSim();
                var s = sim.State;
                s.Strategy.Tuning.RivalMaxBuilt = maxBuilt;
                s.Strategy.Tuning.BreachLimit = 99;
                K.Step(sim);
                // Put the Drill Yard (shields) and the Monument (points) in the rival's age II hand.
                for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge; r++) K.EndRound(sim);
                Assert.AreEqual(2, s.Strategy.Age);
                int drill = K.CardOf(s, "drill_yard"), mon = K.CardOf(s, "monument");
                var hp = s.Strategy.Sides[0].DraftHand;
                var hr = s.Strategy.Sides[1].DraftHand;
                foreach (int c in new[] { drill, mon })
                {
                    if (hr.Contains(c)) continue;
                    int other = hr.First(x => x != drill && x != mon);
                    if (hp.Contains(c)) hp[hp.IndexOf(c)] = other;
                    else { s.Strategy.Sides[0].Discarded.Remove(c); s.Strategy.Sides[1].Discarded.Remove(c); }
                    hr[hr.IndexOf(other)] = c;
                }
                int shields = MilitaryApi.Shields(s, PlayerIds.Rival);
                int points = StrategyApi.Score(s, PlayerIds.Rival).Cards;
                var evs = K.Do(sim, new DraftPick { CardId = hp[0] });
                // The rival takes the card worth most to it: the Monument (2 points) before the Drill Yard (1 shield).
                Assert.AreEqual(mon, evs.OfType<DraftPicked>().Single(p => p.PlayerId == 1).CardId, "points and shields first, the larger first");
                bool built = s.Strategy.Sides[1].Built.Any(x => x.CardId == mon);
                Assert.AreEqual(maxBuilt > 0, built, "built only when RivalMaxBuilt allows");
                Assert.AreEqual(points + (built ? 2 : 0), StrategyApi.Score(s, PlayerIds.Rival).Cards, "a built civic card scores for the rival");
                Assert.AreEqual(shields, MilitaryApi.Shields(s, PlayerIds.Rival));
                // Next round it picks again, from the hand you passed it; the Drill Yard stayed in its own hand, now yours,
                // so it can only take it if you pass it back: here you keep it, and its shields stay away from the rival.
                K.EndRound(sim);
                Assert.IsTrue(StrategyApi.DraftPending(s));
                if (s.Strategy.Sides[0].DraftHand.Contains(drill)) K.Do(sim, new DraftPick { CardId = drill });
                else K.FinishDraft(sim);
                Assert.IsFalse(s.Strategy.Sides[1].Built.Any(x => x.CardId == drill), "a card you take is denied to the rival");
                Assert.AreEqual(shields, MilitaryApi.Shields(s, PlayerIds.Rival));
            }
        }

        [Test]
        public void Rival_BuildsAtMostOneShieldCardAndTwoCardsPerAge()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99;
            K.Step(sim);
            var rival = s.Strategy.Sides[1];
            var human = s.Strategy.Sides[0];
            // The rival's age I hand: two shield cards and two point cards.
            var want = new[] { "palisade", "watch_post", "shrine", "village_green" }.Select(id => K.CardOf(s, id)).ToList();
            foreach (int id in want)
            {
                if (rival.DraftHand.Contains(id)) continue;
                int other = rival.DraftHand.First(x => !want.Contains(x));
                if (human.DraftHand.Contains(id)) human.DraftHand[human.DraftHand.IndexOf(id)] = other;
                rival.DraftHand[rival.DraftHand.IndexOf(other)] = id;
            }
            for (int r = 0; r < s.Strategy.Tuning.RoundsPerAge && !s.Strategy.GameOver; r++) K.EndRound(sim);
            var picked = rival.Built.Select(b => b.CardId).Concat(rival.Hand).Where(id => s.Strategy.Cards[id].Age == 1).ToList();
            var built = rival.Built.Where(b => s.Strategy.Cards[b.CardId].Age == 1).Select(b => StrategyApi.CardDefOf(s, b.CardId)).ToList();
            Assert.AreEqual(3, picked.Count, "three picks in the age");
            Assert.AreEqual(s.Strategy.Tuning.RivalBuildsPerAge, built.Count, "two of them built");
            Assert.AreEqual(1, built.Count(d => d.Shields > 0), "at most one shield card per age");
            Assert.AreEqual(1, built.Count(d => d.Vp > 0));
        }

        [Test]
        public void Cards_BuildRules_CostCityAndLimit()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.FinishDraft(sim);
            s.Player(0).Coin = 0; // spend the commission coin first: coin costs are checked
            int home = s.Board.HomeCityVertex;
            foreach (var id in new[] { "lumber_camp", "clay_diggers", "granary", "ranger_post" }) K.ForceIntoHand(s, id);
            Assert.AreEqual(StrategyRefusals.NoCity, sim.Check(new BuildCard { CardId = K.CardOf(s, "lumber_camp"), CityVertex = s.Economy.DemoVertexA }).Reason);
            Assert.AreEqual(StrategyRefusals.NotInHand, sim.Check(new BuildCard { CardId = K.CardOf(s, "great_hall"), CityVertex = home }).Reason);
            var cost = sim.Check(new BuildCard { CardId = K.CardOf(s, "clay_diggers"), CityVertex = home });
            Assert.AreEqual(StrategyRefusals.CannotAfford, cost.Reason, "Clay Diggers costs coin");
            StringAssert.Contains("2 coin", cost.Message);
            s.Player(0).Coin = 4;
            int moves = s.Strategy.HeroMovesLeft;
            foreach (var id in new[] { "lumber_camp", "clay_diggers", "ranger_post" })
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
            // The deck: every flag within InfectionRadius (2) segments of home except home: its 6 nearest flags.
            int radius = s.Strategy.Tuning.InfectionRadius;
            Assert.AreEqual(Enumerable.Range(0, topo.VertexCount).Count(v => v != home && topo.Distance(home, v) <= radius), s.Strategy.InfectionDeck.Count);
            Assert.AreEqual(6, s.Strategy.InfectionDeck.Count);
            Assert.IsTrue(s.Strategy.InfectionDeck.All(v => topo.Distance(home, v) <= radius && v != home));
            K.Step(sim);
            int empty = Enumerable.Range(0, topo.VertexCount).First(v => !topo.IsCorner(v) && !EconomyApi.IsVertexOccupied(s, v)
                && topo.VertexEdges(v).All(e => !s.Economy.Roads[e]) && topo.Distance(home, v) <= 4);
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
        public void Demons_InfectionAddsDemon_FourthOutbreaksAndAdvancesTheBreachTrack()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int v = K.DemoCorner(s);
            var neighbours = StrategyApi.OutbreakNeighbours(s, v);
            RigInfection(s, v, v, v, v);
            var evs = K.EndRound(sim);
            Assert.AreEqual(3, s.Strategy.Demons[v], "a site holds at most 3");
            Assert.AreEqual(1, s.Strategy.Breaches, "a fourth draw on a full site outbreaks: the breach track advances");
            var ob = evs.OfType<OutbreakOccurred>().Single();
            Assert.AreEqual(v, ob.Vertex);
            CollectionAssert.AreEqual(neighbours, ob.Neighbours);
            foreach (int n in neighbours)
                Assert.AreEqual(n == s.Strategy.HeroVertex ? 0 : 1, s.Strategy.Demons[n], "each connected neighbouring site gains one");
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
            K.Give(s, Ware.Bricks, 2, Ware.Rations, 1);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "ward_stones"), CityVertex = s.Board.HomeCityVertex });
            int v = K.DemoCorner(s);
            RigInfection(s, v, v, v, v);
            K.EndRound(sim);
            Assert.AreEqual(4, s.Strategy.Demons[v]);
            Assert.AreEqual(0, s.Strategy.Breaches);
        }

        [Test]
        public void Demons_BreachLimit_EndsTheGameAsALoss()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            int v = K.DemoCorner(s);
            int limit = s.Strategy.Tuning.BreachLimit;
            RigInfection(s, Enumerable.Repeat(v, 3 + limit).ToArray());
            var evs = K.EndRound(sim);
            Assert.AreEqual(limit, s.Strategy.Breaches, "the game stops at the limit");
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
            int perRound = s.Strategy.Tuning.HeroMovesPerRound;
            for (int i = 0; i < perRound; i++) K.Do(sim, new MoveHero { Vertex = i % 2 == 0 ? n : home });
            var r = sim.Check(new MoveHero { Vertex = n });
            Assert.AreEqual(Reasons.NoTurnAction, r.Reason);
            Assert.AreEqual(Reasons.Sentence(Reasons.NoTurnAction), r.Message);
            K.EndRound(sim);
            Assert.AreEqual(perRound, s.Strategy.HeroMovesLeft, "moves refresh each round");
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
            K.Give(s, Ware.Planks, 10, Ware.Bricks, 4);
            int site = K.Build(sim, BuildingType.WonderSite);
            EconomyApi.TrySpendFromStorehouse(s, Ware.Bricks, EconomyApi.StorehouseCount(s, Ware.Bricks));
            int bricksNow = 0;
            K.Do(sim, new BuildWonderStage());
            Assert.AreEqual(StrategyRefusals.StageInProgress, sim.Check(new BuildWonderStage()).Reason);
            K.Step(sim, 300);
            Assert.AreEqual(0, s.Strategy.Wonder.StagesDone);
            var info = StrategyApi.Wonder(s);
            StringAssert.Contains("bricks", info.Status);
            var stage = s.Strategy.Tuning.WonderStages[0];
            StringAssert.Contains((stage[(int)Ware.Bricks] - bricksNow) + " bricks", info.Status);
            Assert.AreEqual(stage[(int)Ware.Planks], s.Economy.Buildings[site].Stock[(int)Ware.Planks], "planks arrived at the wonder site");

            // Bricks in the storehouse are not enough: the stage completes only once they are delivered.
            K.Give(s, Ware.Bricks, stage[(int)Ware.Bricks]);
            var evs = K.Step(sim, 1);
            Assert.AreEqual(0, s.Strategy.Wonder.StagesDone, "not complete before delivery");
            evs.AddRange(K.Step(sim, 300));
            Assert.AreEqual(1, s.Strategy.Wonder.StagesDone);
            var done = evs.OfType<WonderStageCompleted>().Single();
            Assert.AreEqual(2, done.Vp);
            int lastDelivery = evs.OfType<WareDelivered>().Where(d => d.BuildingId == site).Max(d => (int)d.Tick);
            Assert.GreaterOrEqual(done.Tick, lastDelivery);
            Assert.AreEqual(2, StrategyApi.Score(s, 0).Wonder);
            Assert.AreEqual(stage[(int)Ware.Bricks], s.Strategy.Ledger.Wonder[(int)Ware.Bricks]);
            Assert.IsNull(K.CheckInvariants(s));
        }

        // ---------------- Victory paths ----------------

        [Test]
        public void Victory_ScriptedGameReachesTwelvePoints_AndWins()
        {
            var sim = K.NewSim();
            var s = sim.State;
            var topo = s.Board.Topology;
            s.Strategy.Tuning.VictoryPoints = 12; // the scoring sources and the tick-boundary check (the tuned target is played by the scripted games)
            K.Step(sim);
            K.FinishDraft(sim);
            K.Give(s, Ware.Planks, 60, Ware.Bricks, 40, Ware.Rations, 10, Ware.Iron, 4);
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
            Assert.AreEqual(8, StrategyApi.Score(s, 0).Total, "wonder stage 1 is worth 2");
            K.ForceIntoHand(s, "shrine");
            s.Player(0).Coin = 1;
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "shrine"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(9, StrategyApi.Score(s, 0).Total, "a civic card scores as printed");
            Assert.IsFalse(s.Strategy.GameOver);
            K.Do(sim, new BuildWonderStage());
            var evs = K.Step(sim, 400);
            var end = evs.OfType<GameEnded>().Single();
            Assert.AreEqual(GameOutcome.Victory, end.Outcome);
            Assert.AreEqual(OutcomeReasons.Points, end.Reason);
            Assert.AreEqual(12, end.PlayerVp, "wonder stage 2 is worth 3");
            Assert.AreEqual(end.Tick, evs.OfType<WonderStageCompleted>().Last().Tick, "checked at the tick boundary of the stage");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Ending_AfterAgeThree_ComparesPoints()
        {
            var sim = K.NewSim();
            var s = sim.State;
            s.Strategy.Tuning.BreachLimit = 99; // no hero play here: keep demons from ending the game (tested separately)
            s.Strategy.Tuning.VictoryPoints = 99; // the rival would reach the target on the last bell: test the age-end comparison
            K.Step(sim);
            GameEnded end = null;
            for (int r = 0; r < s.Strategy.Tuning.Ages * s.Strategy.Tuning.RoundsPerAge; r++) end = K.EndRound(sim).OfType<GameEnded>().FirstOrDefault() ?? end;
            Assert.IsNotNull(end);
            Assert.AreEqual(OutcomeReasons.AgeEnd, end.Reason);
            var p = StrategyApi.Score(s, 0).Total;
            var rv = StrategyApi.Score(s, 1).Total;
            Assert.AreEqual(2, StrategyApi.Score(s, 0).Cities, "a passive player keeps only the home city");
            Assert.AreEqual(-3, StrategyApi.Score(s, 0).Military, "and loses all three military comparisons");
            Assert.AreEqual(3, s.Strategy.Comparisons.Count);
            Assert.Greater(rv, p);
            Assert.AreEqual(GameOutcome.Defeat, end.Outcome);
            TestContext.WriteLine("passive: player " + p + " rival " + rv + " -> " + StrategyApi.Headline(s));
        }
    }
}
