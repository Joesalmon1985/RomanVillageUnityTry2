using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim;
using World.Sim.Table;

namespace World.Game.EditTests
{
    /// <summary>WU-2: the god board view model matches the sim, and every ledge tablet text is clean (AB-1).</summary>
    public class GodBoardViewModelTests
    {
        private static WorldSim Sim()
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = PlayerIds.Human;
            var sim = WorldSimFactory.CreateNew(20261120UL, setup);
            Assert.IsTrue(sim.Submit(new Sacrifice { PlayerId = PlayerIds.Human }).Ok);
            sim.Step();
            return sim;
        }

        private static GodBoardViewModel MainTurn(WorldSim sim, int[] hand)
        {
            var t = sim.State.Table;
            t.Phase = TablePhase.Main; t.Paused = false; t.Current = PlayerIds.Human; t.TurnOrder = new[] { 0, 1, 2, 3 };
            System.Array.Copy(hand, t.Peoples[PlayerIds.Human].Hand, hand.Length);
            return new GodBoardViewModel(sim.State, PlayerIds.Human);
        }

        [Test]
        public void Hand_Costs_Affordability_MatchTheSim()
        {
            var sim = Sim();
            var vm = MainTurn(sim, new[] { 1, 1, 0, 0, 0 });
            var t = sim.State.Table;
            Assert.IsTrue(vm.Valid);
            CollectionAssert.AreEqual(t.Peoples[0].Hand, vm.Hand);
            Assert.AreEqual(TableRules.HandSize(t.Peoples[0].Hand), vm.HandSize);
            Assert.IsTrue(vm.Kind[0].Affordable, "road: 1 wood 1 clay");
            Assert.IsFalse(vm.Kind[1].Affordable, "camp needs grain and sheep");
            Assert.IsFalse(vm.Kind[2].Affordable);
            Assert.AreEqual(TableRules.RoadsLeft(t, 0), vm.Kind[0].Left);
            Assert.AreEqual(TableRules.SettlementsLeft(t, 0), vm.Kind[1].Left);
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 0 }, vm.Kind[1].Missing);
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 0, 3 }, vm.Kind[2].Missing);
            // not your turn: nothing is affordable even with goods
            t.Current = 1;
            vm = new GodBoardViewModel(sim.State, 0);
            Assert.IsFalse(vm.CanAct);
            Assert.IsFalse(vm.Kind[0].Affordable);
            Assert.IsTrue(vm.Kind[0].CanPay);
        }

        [Test]
        public void LegalKinds_MatchTheLegalSpotLists()
        {
            var sim = Sim();
            var vm = MainTurn(sim, new[] { 5, 5, 5, 5, 5 });
            Assert.AreEqual(TableRules.LegalRoadEdges(sim.State, 0).Count, vm.LegalSpotCount(BuildKind.Road));
            Assert.AreEqual(TableRules.LegalSettlementCorners(sim.State, 0, true).Count, vm.LegalSpotCount(BuildKind.Settlement));
            int cities = 0; var t = sim.State.Table;
            for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == 0 && t.CornerLevel[c] == 1) cities++;
            Assert.AreEqual(cities, vm.LegalSpotCount(BuildKind.City));
            vm = MainTurn(sim, new[] { 0, 0, 0, 0, 0 });
            Assert.AreEqual(0, vm.LegalSpotCount(BuildKind.Road));
        }

        [Test]
        public void Points_Rates_Discard_Offers_Sand_MatchTheSim()
        {
            var sim = Sim();
            var vm = MainTurn(sim, new[] { 2, 0, 0, 0, 0 });
            var t = sim.State.Table;
            for (int p = 0; p < 4; p++)
            {
                Assert.AreEqual(TableRules.Points(sim.State, p), vm.Points[p]);
                Assert.AreEqual(TableRules.HandSize(t.Peoples[p].Hand), vm.Cards[p]);
                Assert.AreEqual(t.Peoples[p].RoadLength, vm.RoadLength[p]);
            }
            for (int g = 0; g < Wares.RawCount; g++) Assert.AreEqual(TableRules.TradeRate(sim.State, 0, (Ware)g), vm.BankRate[g]);
            Assert.AreEqual(t.Tuning.BankRate, vm.BankRateBase);
            // an open offer to the human shows; a declined one does not
            var o = new TradeOffer { Id = t.NextOfferId++, From = 1, To = 0, TicksLeft = 100, Status = OfferStatus.Open };
            o.Give[1] = 1; o.Get[0] = 1;
            t.Offers.Add(o);
            Assert.AreEqual(1, new GodBoardViewModel(sim.State, 0).Incoming.Count);
            o.DeclinedBy.Add(0);
            Assert.AreEqual(0, new GodBoardViewModel(sim.State, 0).Incoming.Count);
            // discard need and the sand
            t.Phase = TablePhase.Discard; t.DiscardOwed = new int[t.PeopleCount]; t.DiscardOwed[0] = 3; t.DiscardTicksLeft = 77;
            vm = new GodBoardViewModel(sim.State, 0);
            Assert.IsTrue(vm.MustDiscard);
            Assert.AreEqual(3, vm.DiscardOwed);
            Assert.AreEqual(77, vm.DiscardTicksLeft);
            t.Phase = TablePhase.Main; t.Sand = t.Tuning.TurnCapTicks / 2;
            vm = new GodBoardViewModel(sim.State, 0);
            Assert.AreEqual(0.5f, vm.SandFraction, 0.001f);
            Assert.IsFalse(vm.MustDiscard);
        }

        [Test]
        public void LedgerTexts_PassTheGuard_AndSayNothingOfDiceOrHexNumbers()
        {
            var sim = Sim();
            var vm = MainTurn(sim, new[] { 1, 3, 0, 2, 1 });
            var t = sim.State.Table;
            var o = new TradeOffer { Id = t.NextOfferId++, From = 2, To = 0, TicksLeft = 100, Status = OfferStatus.Open };
            o.Give[3] = 2; o.Get[1] = 1; t.Offers.Add(o);
            vm = new GodBoardViewModel(sim.State, 0);
            var feed = new List<string> { "Bram built a road.", "You traded at the bank." };
            Assert.IsNull(GodBoardLedger.FirstViolation(vm, feed));
            var all = new List<LedgerContent> { GodBoardLedger.Baskets(vm), GodBoardLedger.Bench(vm), GodBoardLedger.Standings(vm), GodBoardLedger.Trader(vm),
                GodBoardLedger.Sack(vm, 0), GodBoardLedger.Hourglass(vm), GodBoardLedger.Chronicler(feed), GodBoardLedger.Prince(true, "Try the coast."), GodBoardLedger.Totem(false), GodBoardLedger.Envoy(vm, o) };
            foreach (var c in all)
                foreach (var line in c.AllText())
                {
                    var l = line.ToLowerInvariant();
                    foreach (var bad in new[] { "hex ", "roll", "token", "pip", "odds", "chance", "%" }) StringAssert.DoesNotContain(bad, l, line);
                }
            // exact counts appear where the plan lists them
            StringAssert.Contains("Logs: 1", string.Join("\n", GodBoardLedger.Baskets(vm).AllText()));
            StringAssert.Contains("Clay bricks: 3", string.Join("\n", GodBoardLedger.Baskets(vm).AllText()));
            StringAssert.Contains("2 fleece", string.Join("\n", GodBoardLedger.Envoy(vm, o).AllText()));
            StringAssert.Contains("first to 10 points", string.Join("\n", GodBoardLedger.Standings(vm).AllText()));
        }

        [Test]
        public void Refusals_SayWhy()
        {
            var sim = Sim();
            var vm = MainTurn(sim, new[] { 0, 0, 0, 0, 0 });
            StringAssert.Contains("lack", GodBoardLedger.BuildRefusal(vm, BuildKind.Road));
            sim.State.Table.Current = 2;
            vm = new GodBoardViewModel(sim.State, 0);
            StringAssert.Contains("turn", GodBoardLedger.BuildRefusal(vm, BuildKind.Road));
        }
    }
}
