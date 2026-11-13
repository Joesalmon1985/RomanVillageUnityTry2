using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Strength, the overrun warning and the endings (REFACTOR_SPEC 2.C.3).</summary>
    public class OverrunTests
    {
        [Test]
        public void StartingStrength_OneEach_NoWarning()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            sim.Step(5);
            var s = sim.State;
            Assert.AreEqual(1, FactionApi.Strength(s, PlayerIds.Human).Total);
            Assert.AreEqual(1, FactionApi.Strength(s, PlayerIds.Rival).Settlements);
            Assert.IsFalse(FactionApi.Warning(s).Active);
            Assert.IsFalse(FactionApi.Outcome(s).GameOver);
            Assert.AreEqual("Your people 1, the Ash Clan 1.", FactionApi.Headline(s));
            Assert.AreEqual("Your people", s.Factions.Factions[0].Name);
            Assert.AreEqual("the Ash Clan", s.Factions.Factions[1].Name);
            Assert.IsFalse(s.Factions.Factions[0].Auto);
            Assert.IsTrue(s.Factions.Factions[1].Auto);
        }

        [Test]
        public void Warning_ThenOverrun_EndsTheGame()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Factions.Tuning.OverrunMargin = 0; // equal strength is enough: the player (checked first) attacks
            var ev = K.Step(sim, 1);
            var w = ev.OfType<OverrunWarningStarted>().Single();
            Assert.AreEqual(PlayerIds.Human, w.Attacker);
            Assert.AreEqual(s.Tick + 900, w.EndTick);
            Assert.IsTrue(FactionApi.Warning(s).Active);
            Assert.AreEqual(900, FactionApi.Warning(s).TicksLeft);
            ev = K.Step(sim, 900);
            Assert.AreEqual(PlayerIds.Rival, ev.OfType<FactionOverrun>().Single().Defender);
            var end = ev.OfType<GameEnded>().Single();
            Assert.AreEqual(GameOutcome.Victory, end.Outcome);
            Assert.AreEqual(OutcomeReasons.Overrun, end.Reason);
            var o = FactionApi.Outcome(s);
            Assert.IsTrue(o.GameOver);
            Assert.AreEqual("Your people overran the Ash Clan.", o.Sentence);
            var r = sim.Submit(new PlaceRoad { PlayerId = 0, HexEdge = 0 });
            Assert.AreEqual("game_over", r.Reason);
            Assert.AreEqual("The game is over.", r.Message);
            Assert.IsEmpty(K.Step(sim, 100).OfType<ProposalAdded>(), "no planning after the end");
        }

        [Test]
        public void Warning_Cancelled_WhenTheMarginNoLongerHolds()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Factions.Tuning.OverrunMargin = 0;
            K.Step(sim, 1);
            s.Factions.Tuning.OverrunMargin = 1;
            var ev = K.Step(sim, 1);
            Assert.AreEqual(1, ev.OfType<OverrunWarningCancelled>().Count());
            Assert.IsFalse(FactionApi.Warning(s).Active);
        }

        [Test]
        public void TimeLimit_EqualStrength_IsADraw()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Factions.Tuning.TimeLimitTicks = 50;
            var ev = K.Step(sim, 50);
            var end = ev.OfType<GameEnded>().Single();
            Assert.AreEqual(GameOutcome.Draw, end.Outcome);
            Assert.AreEqual(OutcomeReasons.Time, end.Reason);
            Assert.AreEqual(50, s.Factions.EndedTick);
            Assert.AreEqual("Time is up: both peoples are equally strong.", FactionApi.Outcome(s).Sentence);
        }

        [Test]
        public void FedWarriorsCount_HungryOnesDoNot()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Economy.Warriors.Add(new Warrior { Id = 0, Faction = 0, Lodge = -1, Settlement = 0, Fed = true, NextMealTick = 1 });
            var ev = K.Step(sim, 1);
            Assert.IsTrue(ev.OfType<WarriorHungry>().Any(), "no bread or meat at the start");
            Assert.AreEqual(1, FactionApi.Strength(s, 0).Total);
            Assert.AreEqual(1, FactionApi.Strength(s, 0).HungryWarriors);
            s.Economy.Settlements[0].Stock[(int)Ware.Bread] = 1;
            s.Economy.Ledger.Starting[(int)Ware.Bread] += 1;
            ev = K.Step(sim, 100);
            Assert.AreEqual(Ware.Bread, ev.OfType<WarriorFed>().Single().Ware);
            Assert.AreEqual(2, FactionApi.Strength(s, 0).Total);
            Assert.IsNull(K.CheckInvariants(s));
        }
    }
}
