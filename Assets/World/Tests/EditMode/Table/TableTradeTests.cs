using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>TR-1..TR-3: bank and port trades, offers between peoples, answers and expiry.</summary>
    public class TableTradeTests
    {
        [Test]
        public void Bank_FourForOne()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            SetHand(t, 0, 3, 0, 0, 0, 0);
            Refused(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay }, TableRefusals.CannotAfford);
            Refused(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Wood }, TableRefusals.BadTrade);
            Refused(sim, new BankTrade { Give = Ware.Planks, Get = Ware.Wood }, TableRefusals.BadTrade);
            SetHand(t, 0, 5, 0, 0, 0, 0);
            var evs = Do(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay });
            CollectionAssert.AreEqual(Cards(1, 1), t.Peoples[0].Hand);
            var tr = Single<Traded>(evs);
            Assert.AreEqual((0, -1, 4), (tr.People, tr.With, tr.Rate));
            Refused(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay }, TableRefusals.NotYourTurn, 1);
        }

        private static TablePort[] PortsAtHome(int good)
        {
            var topo = BoardTopology.Get(BoardGenerator.MvpRadius);
            int home = StartCorners[0];
            return new[] { new TablePort { CornerA = home, CornerB = topo.CornerNeighbours(home)[0], Good = good } };
        }

        [Test]
        public void Port_ThreeForOne_WhenOwningACorner()
        {
            var sim = ToHumanTurn(ports: PortsAtHome(-1));
            var s = sim.State;
            Assert.AreEqual(3, TableRules.TradeRate(s, 0, Ware.Sheep));
            Assert.AreEqual(4, TableRules.TradeRate(s, 1, Ware.Sheep), "not their port");
            SetHand(T(sim), 0, 0, 0, 0, 3, 0);
            Do(sim, new BankTrade { Give = Ware.Sheep, Get = Ware.Stone });
            CollectionAssert.AreEqual(Cards(stone: 1), T(sim).Peoples[0].Hand);
        }

        [Test]
        public void Port_TwoForOne_OnlyForItsGood()
        {
            var sim = ToHumanTurn(ports: PortsAtHome((int)Ware.Grain));
            var s = sim.State;
            Assert.AreEqual(2, TableRules.TradeRate(s, 0, Ware.Grain));
            Assert.AreEqual(4, TableRules.TradeRate(s, 0, Ware.Wood));
            SetHand(T(sim), 0, 0, 0, 2, 0, 0);
            var tr = Single<Traded>(Do(sim, new BankTrade { Give = Ware.Grain, Get = Ware.Wood }));
            Assert.AreEqual(2, tr.Rate);
        }

        [Test]
        public void NoPortsOnTheBoard_BankOnly()
        {
            var sim = ToHumanTurn();
            foreach (Ware w in Wares.Raw) Assert.AreEqual(4, TableRules.TradeRate(sim.State, 0, w));
        }

        [Test]
        public void Offer_AiDeclinesByDefault_AfterAnswerTicks()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            SetHand(t, 0, 2, 0, 0, 0, 0);
            SetHand(t, 1, 0, 2, 0, 0, 0);
            var evs = Do(sim, new OfferTrade { To = 1, Give = Cards(1), Get = Cards(clay: 1) });
            var om = Single<OfferMade>(evs);
            Assert.AreEqual((0, 1), (om.From, om.To));
            Assert.AreEqual(1, t.Offers.Count);
            evs = Step(sim, t.Tuning.AiAnswerTicks);
            var ans = Single<OfferAnswered>(evs);
            Assert.AreEqual((1, OfferStatus.Declined), (ans.By, ans.Status));
            Assert.AreEqual(0, t.Offers.Count);
            CollectionAssert.AreEqual(Cards(2), t.Peoples[0].Hand);
            Refused(sim, new AnswerOffer { OfferId = om.OfferId, Accept = true }, TableRefusals.OfferClosed, 1);
        }

        [Test]
        public void Offer_AiAccepts_CardsChangeHands()
        {
            var sim = ToHumanTurn(wire: seams => { IdleAi(seams); seams.AiAnswer = (s, p, o) => true; });
            var t = T(sim);
            SetHand(t, 0, 2, 0, 0, 0, 0);
            SetHand(t, 1, 0, 2, 0, 0, 0);
            Do(sim, new OfferTrade { To = 1, Give = Cards(2), Get = Cards(clay: 1) });
            var evs = Step(sim, t.Tuning.AiAnswerTicks);
            Assert.AreEqual(OfferStatus.Accepted, Single<OfferAnswered>(evs).Status);
            var tr = Single<Traded>(evs);
            Assert.AreEqual((0, 1), (tr.People, tr.With));
            CollectionAssert.AreEqual(Cards(clay: 1), t.Peoples[0].Hand);
            CollectionAssert.AreEqual(Cards(2, 1), t.Peoples[1].Hand);
        }

        [Test]
        public void Offer_ToEveryone_FirstAcceptingPeopleTakesIt_AllDeclineCloses()
        {
            var sim = ToHumanTurn(wire: seams => { IdleAi(seams); seams.AiAnswer = (s, p, o) => p == 2; });
            var t = T(sim);
            SetHand(t, 0, 1, 0, 0, 0, 0);
            SetHand(t, 1, 0, 0, 1, 0, 0);
            SetHand(t, 2, 0, 0, 1, 0, 0);
            Do(sim, new OfferTrade { To = -1, Give = Cards(1), Get = Cards(grain: 1) });
            var evs = Step(sim, t.Tuning.AiAnswerTicks);
            Assert.AreEqual(2, Single<Traded>(evs).With);
            CollectionAssert.AreEqual(Cards(grain: 1), t.Peoples[0].Hand);

            // Nobody wants wood for sheep: everyone declines and the offer closes.
            SetHand(t, 0, 1, 0, 0, 0, 0);
            Do(sim, new OfferTrade { To = -1, Give = Cards(1), Get = Cards(sheep: 1) });
            evs = Step(sim, t.Tuning.AiAnswerTicks);
            var ans = Single<OfferAnswered>(evs);
            Assert.AreEqual((-1, OfferStatus.Declined), (ans.By, ans.Status));
        }

        [Test]
        public void Offer_ChecksAndRefusals()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            SetHand(t, 0, 1, 0, 0, 0, 0);
            Refused(sim, new OfferTrade { To = 1, Give = Cards(2), Get = Cards(clay: 1) }, TableRefusals.CannotAfford);
            Refused(sim, new OfferTrade { To = 0, Give = Cards(1), Get = Cards(clay: 1) }, TableRefusals.BadTrade);
            Refused(sim, new OfferTrade { To = 1, Give = Cards(), Get = Cards(clay: 1) }, TableRefusals.BadTrade);
            Refused(sim, new OfferTrade { To = 9, Give = Cards(1), Get = Cards(clay: 1) }, TableRefusals.BadTrade);
            Refused(sim, new OfferTrade { To = 0, Give = Cards(1), Get = Cards(clay: 1) }, TableRefusals.NotYourTurn, 1);
            Refused(sim, new AnswerOffer { OfferId = 99, Accept = true }, TableRefusals.OfferClosed, 1);
        }

        [Test]
        public void Offer_ExpiresAfterOfferTicks()
        {
            var sim = ToHumanTurn(tune: tu => tu.AiAnswerTicks = 10000);
            var t = T(sim);
            SetHand(t, 0, 1, 0, 0, 0, 0);
            Do(sim, new OfferTrade { To = 1, Give = Cards(1), Get = Cards(clay: 1) });
            var evs = Step(sim, t.Tuning.OfferTicks - 2);
            Assert.AreEqual(0, Count<OfferAnswered>(evs));
            evs = Step(sim, 1);
            Assert.AreEqual(OfferStatus.Expired, Single<OfferAnswered>(evs).Status);
            Assert.AreEqual(0, t.Offers.Count);
        }

        [Test]
        public void AiOffersToPlayer_PlayerAccepts_OrItExpiresAtTurnEnd()
        {
            int offered = 0;
            var sim = ToHumanTurn(wire: seams => seams.AiTurn = (s, p, a) =>
            {
                if (s.Table.AiCalls > 1) return; // asked again after the answer (G1-2): offer only once a turn
                s.Table.Peoples[p].Hand = Cards(sheep: 2);
                Assert.IsTrue(a.OfferTrade(0, Cards(sheep: 1), Cards(1)).Ok);
                offered++;
            });
            var t = T(sim);
            Do(sim, new EndTurn());
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => t.Offers.Count > 0, 50, log));
            var o = t.Offers[0];
            int ai = o.From;
            SetHand(t, 0, 3, 0, 0, 0, 0); // production at the AI's turn start may have changed our hand
            Refused(sim, new AnswerOffer { OfferId = o.Id, Accept = true }, TableRefusals.OfferClosed, Enumerable.Range(1, 3).First(x => x != ai));
            var evs = Do(sim, new AnswerOffer { OfferId = o.Id, Accept = true });
            Assert.AreEqual((ai, 0), (Single<Traded>(evs).People, Single<Traded>(evs).With));
            CollectionAssert.AreEqual(Cards(2, sheep: 1), t.Peoples[0].Hand);

            // The next AI's offer is left unanswered: the AI's turn waits for it until it expires (OfferTicks), then ends (TR-3).
            log.Clear();
            Assert.IsTrue(StepUntil(sim, () => t.Offers.Count > 0 && t.Offers[0].From != ai, 200, log));
            var second = t.Offers[0];
            log.Clear();
            Assert.IsTrue(StepUntil(sim, () => t.Current != second.From, 200, log));
            Assert.AreEqual(OfferStatus.Expired, log.OfType<OfferAnswered>().Single(e => e.OfferId == second.Id).Status);
            Refused(sim, new AnswerOffer { OfferId = second.Id, Accept = true }, TableRefusals.OfferClosed);
        }

        [Test]
        public void AnswerOffer_CannotAfford()
        {
            int offerId = -1;
            var sim = ToHumanTurn(wire: seams => seams.AiTurn = (s, p, a) =>
            {
                s.Table.Peoples[p].Hand = Cards(sheep: 2);
                a.OfferTrade(0, Cards(sheep: 1), Cards(stone: 3));
            });
            var t = T(sim);
            Do(sim, new EndTurn());
            Assert.IsTrue(StepUntil(sim, () => t.Offers.Count > 0, 50));
            ClearHands(t);
            offerId = t.Offers[0].Id;
            Refused(sim, new AnswerOffer { OfferId = offerId, Accept = true }, TableRefusals.CannotAfford);
            var evs = Do(sim, new AnswerOffer { OfferId = offerId, Accept = false });
            Assert.AreEqual(OfferStatus.Declined, Single<OfferAnswered>(evs).Status);
        }
    }
}
