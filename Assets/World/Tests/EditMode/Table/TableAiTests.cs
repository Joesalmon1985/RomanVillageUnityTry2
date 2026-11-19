using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>G1-2: the AI brain. Target build, TR-4 to TR-7, discards, setup, determinism.</summary>
    public class TableAiTests
    {
        /// <summary>AI seats with the brain's answers, refusals and discards, but no turns of their own.</summary>
        private static void BrainNoTurns(TableSeams seams) => seams.AiTurn = (s, p, a) => { };

        /// <summary>Gives p one more point: a settlement on a free legal corner (no road needed), set straight on the board.</summary>
        private static int AddSettlement(WorldState s, int p)
        {
            int c = TableRules.LegalSettlementCorners(s, p, false).Last();
            s.Table.CornerOwner[c] = p;
            s.Table.CornerLevel[c] = 1;
            return c;
        }

        private static TradeOffer Offer(int from, int to, int[] give, int[] get) =>
            new TradeOffer { From = from, To = to, Give = give, Get = get };

        // ---- Target build ----

        [Test]
        public void Target_AfterSetup_IsALegalBuildWithItsCost()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var s = sim.State;
            for (int p = 0; p < 4; p++)
            {
                var tg = TableAi.TargetFor(s, p);
                Assert.AreNotEqual(TableTargetKind.None, tg.Kind, "people " + p);
                switch (tg.Kind)
                {
                    case TableTargetKind.Settlement:
                        Assert.IsNull(TableRules.SettlementRefusal(s, p, tg.Spot, true));
                        CollectionAssert.AreEqual(TableRules.SettlementCost, tg.Cost);
                        break;
                    case TableTargetKind.Road:
                        Assert.IsNull(TableRules.RoadRefusal(s, p, tg.Spot));
                        Assert.AreEqual(tg.Path[0], tg.Spot);
                        Assert.IsNull(TableRules.SettlementRefusal(s, p, tg.Site, false));
                        CollectionAssert.AreEqual(TableRules.RoadCost, tg.Cost);
                        break;
                    case TableTargetKind.City:
                        Assert.AreEqual((p, 1), (s.Table.CornerOwner[tg.Spot], s.Table.CornerLevel[tg.Spot]));
                        CollectionAssert.AreEqual(TableRules.CityCost, tg.Cost);
                        break;
                }
            }
        }

        [Test]
        public void Target_RoadPath_BuiltInOrder_ReachesASettleableSite()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => { tu.TurnBaseTicks = tu.TurnCapTicks = 100000; tu.MaxCities = 0; });
            var s = sim.State;
            var t = T(sim);
            var tg = TableAi.TargetFor(s, 0);
            Assert.AreNotEqual(TableTargetKind.City, tg.Kind);
            int site = tg.Site;
            foreach (int e in tg.Path)
            {
                SetHand(t, 0, 1, 1, 0, 0, 0);
                Do(sim, new PlaceTableRoad { HexEdge = e });
            }
            SetHand(t, 0, 1, 1, 1, 1, 0);
            Do(sim, new PlaceTableSettlement { Corner = site });
            Assert.AreEqual(0, t.CornerOwner[site]);
        }

        [Test]
        public void Target_PrefersCity_WhenNoSettlementPieceLeft_AndOnTheRichestSettlement()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxSettlements = 2);
            var s = sim.State;
            var tg = TableAi.TargetFor(s, 0);
            Assert.AreEqual(TableTargetKind.City, tg.Kind);
            int best = -1;
            for (int c = 0; c < T(sim).CornerOwner.Length; c++)
                if (T(sim).CornerOwner[c] == 0 && (best < 0 || TableRules.CornerPips(s.Board, c) > TableRules.CornerPips(s.Board, best))) best = c;
            Assert.AreEqual(best, tg.Spot);
        }

        [Test]
        public void Target_PrefersCity_OnAHighPipSettlement()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var s = sim.State;
            int city = TableAi.BestCity(s, 0);
            int pips = TableRules.CornerPips(s.Board, city);
            var tu = TableAi.Tuning;
            int keep = tu.CityFirstPips;
            try
            {
                tu.CityFirstPips = pips;
                Assert.AreEqual((TableTargetKind.City, city), (TableAi.TargetFor(s, 0).Kind, TableAi.TargetFor(s, 0).Spot));
                tu.CityFirstPips = pips + 1;
                Assert.AreNotEqual(TableTargetKind.City, TableAi.TargetFor(s, 0).Kind);
            }
            finally { tu.CityFirstPips = keep; }
        }

        [Test]
        public void Target_RespectsPieceLimits()
        {
            // No roads left: the plan is a settlement already reached, or a city; never a road.
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxRoads = 1);
            var tg = TableAi.TargetFor(sim.State, 0);
            Assert.AreNotEqual(TableTargetKind.Road, tg.Kind);
            Assert.IsTrue(tg.Kind == TableTargetKind.City || tg.Path.Length == 0);

            // Nothing left at all: no target.
            var sim2 = ToHumanTurn(wire: BrainNoTurns, tune: tu => { tu.MaxRoads = 1; tu.MaxSettlements = 2; tu.MaxCities = 0; });
            Assert.AreEqual(TableTargetKind.None, TableAi.TargetFor(sim2.State, 0).Kind);
        }

        // ---- TR-4: who trades with whom ----

        [Test]
        public void Leading_IncludesTies()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var s = sim.State;
            for (int p = 0; p < 4; p++) Assert.IsTrue(TableAi.IsLeading(s, p), "all tied after setup");
            AddSettlement(s, 2);
            for (int p = 0; p < 4; p++) Assert.AreEqual(p == 2, TableAi.IsLeading(s, p));
            AddSettlement(s, 3);
            Assert.IsTrue(TableAi.IsLeading(s, 2) && TableAi.IsLeading(s, 3));
            Assert.AreEqual(TableRefusals.LeaderRefuses, TableAi.Refusal(s, 1, 2));
            Assert.IsNull(TableAi.Refusal(s, 1, 0));
        }

        [Test]
        public void LeadingAi_StillTradesWithNonLeaders_NeverWithLeaders()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var s = sim.State;
            AddSettlement(s, 2);
            AddSettlement(s, 3);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, TableAi.Partners(s, 2));
            CollectionAssert.AreEquivalent(new[] { 0 }, TableAi.Partners(s, 1));
        }

        [Test]
        public void Partners_LowestPointsFirst_ThenLowestPips()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var s = sim.State;
            AddSettlement(s, 3); // 3 leads
            AddSettlement(s, 3);
            AddSettlement(s, 1); // 1 has 3 points, 0 and 2 have 2
            var list = TableAi.Partners(s, 1);
            CollectionAssert.DoesNotContain(list, 3);
            Assert.AreEqual(2, list.Count, "0 and 2");
            int first = list[0], second = list[1];
            Assert.LessOrEqual(TableAi.ProductionPips(s, first), TableAi.ProductionPips(s, second));
            Assert.AreEqual(new[] { 0, 2 }.OrderBy(q => TableAi.ProductionPips(s, q)).ThenBy(q => q).ToArray(), list.ToArray());

            var all = TableAi.Partners(s, 0);
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(2, all[0], "2 points before 3 points");
            Assert.AreEqual(1, all[1]);
        }

        [Test]
        public void PlayerLeading_OfferToAnAi_RefusedWithLeaderRefuses()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var t = T(sim);
            AddSettlement(sim.State, 0);
            SetHand(t, 0, 2, 0, 0, 0, 0);
            Refused(sim, new OfferTrade { To = 1, Give = Cards(1), Get = Cards(clay: 1) }, TableRefusals.LeaderRefuses);
            Assert.AreEqual("They will not trade with the leader.", TableRefusals.Sentence(TableRefusals.LeaderRefuses));
        }

        [Test]
        public void PlayerLeading_OfferToEveryone_EachAiSaysWhy_AndTheOfferCloses()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns);
            var t = T(sim);
            AddSettlement(sim.State, 0);
            SetHand(t, 0, 2, 0, 0, 0, 0);
            for (int p = 1; p < 4; p++) SetHand(t, p, 0, 3, 0, 0, 0);
            Do(sim, new OfferTrade { To = -1, Give = Cards(2), Get = Cards(clay: 1) });
            var evs = Step(sim, t.Tuning.AiAnswerTicks);
            var refused = evs.OfType<TradeRefused>().ToList();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, refused.Select(e => e.People).ToArray());
            Assert.IsTrue(refused.All(e => e.With == 0 && e.Code == TableRefusals.LeaderRefuses));
            Assert.AreEqual("They will not trade with the leader.", refused[0].Sentence);
            Assert.AreEqual(0, Count<Traded>(evs));
            Assert.AreEqual(OfferStatus.Declined, Single<OfferAnswered>(evs).Status);
        }

        [Test]
        public void PlayerNotLeading_AiAnswersAfterAiAnswerTicks_AcceptsAGenerousTrade()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxCities = 0);
            var t = T(sim);
            AddSettlement(sim.State, 3);
            SetHand(t, 0, 2, 0, 0, 0, 0);
            ClearHands(t);
            SetHand(t, 0, 2, 0, 0, 0, 0);
            // AI 1 holds stone only (never part of a road or settlement); it gets 2 cards for 1.
            SetHand(t, 1, 0, 0, 0, 0, 2);
            var cost = TableAi.TargetFor(sim.State, 1).Cost;
            Assert.AreEqual(0, cost[4], "AI 1 plans a road or settlement");
            Do(sim, new OfferTrade { To = 1, Give = Cards(2), Get = Cards(stone: 1) });
            var evs = Step(sim, t.Tuning.AiAnswerTicks - 2);
            Assert.AreEqual(0, Count<OfferAnswered>(evs));
            evs = Step(sim, 1);
            Assert.AreEqual(OfferStatus.Accepted, Single<OfferAnswered>(evs).Status);
            CollectionAssert.AreEqual(Cards(stone: 1), t.Peoples[0].Hand);
        }

        // ---- TR-6: what an AI accepts ----

        [Test]
        public void Accepts_Tr6()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxSettlements = 2); // AI 1's target: a city (2 grain, 3 stone)
            var s = sim.State;
            var t = T(sim);
            AddSettlement(s, 3); // 0 is not leading; the tests below are about TR-6
            Assert.AreEqual(TableTargetKind.City, TableAi.TargetFor(s, 1).Kind);

            SetHand(t, 1, 2, 2, 1, 0, 2);
            // 1-for-1 that does not touch the city: accepted.
            Assert.IsTrue(TableAi.Accepts(s, 1, Offer(0, 1, Cards(sheep: 1), Cards(wood: 1))));
            // Lowers progress (gives stone the city needs): refused.
            Assert.IsFalse(TableAi.Accepts(s, 1, Offer(0, 1, Cards(sheep: 1), Cards(stone: 1))));
            // Gives more than it gets, not for the last card: refused.
            Assert.IsFalse(TableAi.Accepts(s, 1, Offer(0, 1, Cards(grain: 1), Cards(wood: 2))));
            // Cannot pay: refused.
            Assert.IsFalse(TableAi.Accepts(s, 1, Offer(0, 1, Cards(grain: 1), Cards(sheep: 1))));
            // More cards for fewer: accepted.
            Assert.IsTrue(TableAi.Accepts(s, 1, Offer(0, 1, Cards(sheep: 2), Cards(wood: 1))));

            // The last card of the target: pays up to 2-for-1, not 3-for-1.
            SetHand(t, 1, 2, 2, 2, 0, 2);
            Assert.AreEqual(1, TableAi.Missing(t.Peoples[1].Hand, TableRules.CityCost));
            Assert.IsTrue(TableAi.Accepts(s, 1, Offer(0, 1, Cards(stone: 1), Cards(wood: 2))));
            Assert.IsTrue(TableAi.Accepts(s, 1, Offer(0, 1, Cards(stone: 1), Cards(wood: 1, clay: 1))));
            Assert.IsFalse(TableAi.Accepts(s, 1, Offer(0, 1, Cards(stone: 1), Cards(wood: 2, clay: 1))));
            // 2-for-1 for a card that does not finish the target: refused.
            Assert.IsFalse(TableAi.Accepts(s, 1, Offer(0, 1, Cards(sheep: 1), Cards(wood: 2))));

            // Never with the leader, however good the deal.
            AddSettlement(s, 0);
            AddSettlement(s, 0);
            Assert.IsFalse(TableAi.Accepts(s, 1, Offer(0, 1, Cards(stone: 1, grain: 1), Cards(wood: 1))));
        }

        // ---- TR-5: what an AI wants ----

        [Test]
        public void Wants_TargetCardsFirst_ThenGoodsWithoutASettlement()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxSettlements = 2);
            var s = sim.State;
            var t = T(sim);
            var target = TableAi.TargetFor(s, 1);
            Assert.AreEqual(TableTargetKind.City, target.Kind);
            SetHand(t, 1, 0, 0, 1, 0, 1);
            var wants = TableAi.Wants(s, 1, target);
            Assert.AreEqual(new[] { 2, 4 }, wants.Take(2).ToArray(), "grain and stone for the city first");
            int touched = TableAi.TouchedGoods(s, 1);
            foreach (int g in wants.Skip(2)) Assert.AreEqual(0, touched & (1 << g), "then goods it has no settlement next to");
            CollectionAssert.AreEqual(Cards(), TableAi.Spare(s, 1, target));
            SetHand(t, 1, 3, 0, 3, 0, 1);
            CollectionAssert.AreEqual(Cards(3, 0, 1), TableAi.Spare(s, 1, target));
        }

        // ---- The turn: offers to AIs and to the player, the bank when nobody helps ----

        /// <summary>The brain on every seat; on its first call of each turn, 'setup' may change the hands first.</summary>
        private static void BrainWith(TableSeams seams, System.Action<WorldState, int> setup) => seams.AiTurn = (s, p, a) =>
        {
            if (s.Table.AiCalls == 1) setup(s, p);
            TableAi.PlayTurn(s, p, a);
        };

        [Test]
        public void AiTurn_TradesWithAnotherAi_ThenBuilds()
        {
            int mover = -1;
            bool armed = false;
            var sim = ToHumanTurn(wire: seams => BrainWith(seams, (s, p) =>
            {
                if (!armed || mover >= 0) return;
                mover = p;
                var t = s.Table;
                ClearHands(t);
                // The player leads: AIs never trade with it. The mover misses one card of its target.
                AddSettlement(s, 0);
                var cost = TableAi.TargetFor(s, p).Cost;
                var hand = (int[])cost.Clone();
                int lack = System.Array.FindIndex(cost, n => n > 0);
                hand[lack]--;
                int spareGood = cost[4] == 0 ? 4 : 3; // stone for a road or settlement, sheep for a city
                hand[spareGood] += 1;
                t.Peoples[p].Hand = hand;
                for (int q = 1; q < 4; q++)
                    if (q != p) { var h = new int[5]; h[lack] = 5; t.Peoples[q].Hand = h; }
            }));
            var t = T(sim);
            armed = true;
            var log = Do(sim, new EndTurn());
            Assert.IsTrue(StepUntil(sim, () => mover >= 0 && t.Current != mover, 500, log));
            var trade = log.OfType<Traded>().First(e => e.With >= 0);
            Assert.AreEqual(mover, trade.People);
            Assert.AreNotEqual(0, trade.With, "never the leading player");
            Assert.IsTrue(log.OfType<Built>().Any(b => b.People == mover && !b.Setup), "built after the trade");
            var made = log.OfType<OfferMade>().First();
            var answered = log.OfType<OfferAnswered>().First(e => e.OfferId == made.OfferId);
            Assert.AreEqual(t.Tuning.AiAnswerTicks, answered.Tick - made.Tick, "answered after AiAnswerTicks");
        }

        [Test]
        public void AiTurn_OffersToTheNonLeadingPlayer()
        {
            int mover = -1;
            bool armed = false;
            var sim = ToHumanTurn(wire: seams => BrainWith(seams, (s, p) =>
            {
                if (!armed || mover >= 0) return;
                mover = p;
                var t = s.Table;
                ClearHands(t);
                // Every AI leads but the player: the player is the only partner.
                for (int q = 1; q < 4; q++) AddSettlement(s, q);
                var cost = TableAi.TargetFor(s, p).Cost;
                var hand = (int[])cost.Clone();
                int lack = System.Array.FindIndex(cost, n => n > 0);
                hand[lack]--;
                hand[cost[4] == 0 ? 4 : 3] += 1;
                t.Peoples[p].Hand = hand;
            }));
            var t = T(sim);
            armed = true;
            var log = Do(sim, new EndTurn());
            Assert.IsTrue(StepUntil(sim, () => t.Offers.Count > 0, 100, log));
            var o = t.Offers[0];
            Assert.AreEqual((mover, 0), (o.From, o.To));
            Assert.AreEqual(1, TableRules.HandSize(o.Get));
            // The player accepts: the AI builds.
            var need = o.Get;
            t.Peoples[0].Hand = (int[])need.Clone();
            log.AddRange(Do(sim, new AnswerOffer { OfferId = o.Id, Accept = true }));
            Assert.IsTrue(StepUntil(sim, () => t.Current != mover, 300, log));
            Assert.IsTrue(log.OfType<Built>().Any(b => b.People == mover && !b.Setup));
        }

        [Test]
        public void AiTurn_NoPartner_UsesTheBank()
        {
            int mover = -1;
            bool armed = false;
            var sim = ToHumanTurn(wire: seams => BrainWith(seams, (s, p) =>
            {
                if (!armed || mover >= 0) return;
                mover = p;
                var t = s.Table;
                ClearHands(t);
                // All tied after setup: everyone leads, so nobody trades with anyone (TR-4).
                var cost = TableAi.TargetFor(s, p).Cost;
                var hand = (int[])cost.Clone();
                int lack = System.Array.FindIndex(cost, n => n > 0);
                hand[lack]--;
                hand[cost[4] == 0 ? 4 : 3] += 4;
                t.Peoples[p].Hand = hand;
            }));
            var t = T(sim);
            armed = true;
            var log = Do(sim, new EndTurn());
            Assert.IsTrue(StepUntil(sim, () => mover >= 0 && t.Current != mover, 300, log));
            Assert.AreEqual(0, log.OfType<OfferMade>().Count());
            Assert.AreEqual(1, log.OfType<Traded>().Count(e => e.People == mover && e.With == -1 && e.Rate == 4));
            Assert.IsTrue(log.OfType<Built>().Any(b => b.People == mover && !b.Setup));
        }

        // ---- Discards ----

        [Test]
        public void Discard_KeepsTheTargetBuildsCards()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxSettlements = 2);
            var s = sim.State;
            var t = T(sim);
            Assert.AreEqual(TableTargetKind.City, TableAi.TargetFor(s, 1).Kind);
            SetHand(t, 1, 3, 1, 2, 1, 3); // 10 cards, owes 5; the city's 2 grain + 3 stone stay
            var cards = TableAi.ChooseDiscard(s, 1, 5);
            CollectionAssert.AreEqual(Cards(3, 1, 0, 1, 0), cards);
            // Owing more than the spare cards: the target's cards go last.
            cards = TableAi.ChooseDiscard(s, 1, 6);
            Assert.AreEqual(6, TableRules.HandSize(cards));
            Assert.AreEqual(5, cards[0] + cards[1] + cards[3]);
        }

        [Test]
        public void Discard_OnASeven_AiSeatsUseTheBrain()
        {
            var sim = ToHumanTurn(wire: BrainNoTurns, tune: tu => tu.MaxSettlements = 2);
            var t = T(sim);
            SetHand(t, 1, 3, 1, 2, 1, 3);
            Do(sim, new EndTurn());
            // EndTurn rolls for the next people; force a 7 for the turn after instead.
            ForceNextRoll(t, 7);
            SetHand(t, 1, 3, 1, 2, 1, 3);
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => log.OfType<Discarded>().Any(), 500, log));
            var d = log.OfType<Discarded>().First(e => e.People == 1);
            CollectionAssert.AreEqual(Cards(3, 1, 0, 1, 0), d.Cards);
        }

        // ---- Setup ----

        [Test]
        public void Setup_AiPlacesOnTheBestScoredCorner_WithARoadTouchingIt()
        {
            var sim = Create(humanSeat: -1);
            var t = T(sim);
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => t.Phase == TablePhase.Main, 2000, log));
            var built = log.OfType<Built>().Where(b => b.Setup).ToList();
            Assert.AreEqual(8, built.Count);
            var topo = sim.State.Board.Topology;
            for (int i = 0; i < 8; i += 2)
            {
                Assert.AreEqual("settlement", built[i].Kind);
                Assert.AreEqual("road", built[i + 1].Kind);
                int e = built[i + 1].Spot;
                Assert.IsTrue(topo.HexEdgeCornerA(e) == built[i].Spot || topo.HexEdgeCornerB(e) == built[i].Spot);
            }
        }

        // ---- Whole games ----

        [Test]
        public void AiOnlyGame_TheBrain_ReachesTenPoints_Deterministically()
        {
            var a = TableAiProof.Play(Seed);
            var b = TableAiProof.Play(Seed);
            Assert.IsTrue(a.Ended);
            Assert.AreEqual(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
            Assert.AreEqual(0, a.Tr4Broken);
            TestContext.Out.WriteLine("winner " + a.Winner + " in round " + a.Rounds + " at tick " + a.Ticks + "; trades " + a.PeopleTrades + " / bank " + a.BankTrades);
        }

        [TestCase(2)]
        [TestCase(3)]
        public void AiOnlyGame_TheBrain_FewerPeoples_End(int people)
        {
            var g = TableAiProof.Play(Seed, people);
            Assert.IsTrue(g.Ended);
            Assert.AreEqual(0, g.Tr4Broken);
        }

        [Test]
        public void SaveMidGame_TheBrain_JsonRoundTrip_ContinuesIdentically()
        {
            var sim = Create(humanSeat: -1);
            var t = T(sim);
            // Stop while an AI's offer is open, so the replay state is in the save.
            Assert.IsTrue(StepUntil(sim, () => t.Offers.Count > 0, 100000), "an offer comes up");
            string json = JsonConvert.SerializeObject(sim.State);
            var sim2 = new WorldSim(JsonConvert.DeserializeObject<WorldState>(json));
            TableModule.Register(sim2);
            Step(sim, 3000);
            Step(sim2, 3000);
            Assert.AreEqual(JsonConvert.SerializeObject(sim.State.Table), JsonConvert.SerializeObject(sim2.State.Table));
        }
    }
}
