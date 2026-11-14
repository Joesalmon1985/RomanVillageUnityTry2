using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Eras;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// PRD_CRYSTALS 5.1 and 5.3: the expedition clock (+1 per tick and the charges of the table), the authored plan
    /// (march, seize, carry to the altar, search when the crystal is gone), reports that reach the party ReportDelay
    /// later, and the faction game's two links (5.9): an overrun of the player's camp, and an Ash camp falling.
    /// </summary>
    public class CampaignRivalTests
    {
        /// <summary>Raises a CampOverrun just before the campaign slot on one tick, as Strength does.</summary>
        private sealed class OverrunAt : ISimSystem
        {
            public long At;
            public int Attacker, Defender;
            public CampOverrunKind Kind;

            public void Tick(WorldState s, EventBuffer ev)
            {
                if (s.Tick == At) ev.Add(new CampOverrun { Attacker = Attacker, Defender = Defender, SettlementId = 0, Kind = Kind });
            }
        }

        [Test]
        public void Plan_MarchSeizeCarry_OnTheAuthoredClock_ReportsLater()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            Quiet(sim);   // no overrun charges in the way of the plain clock
            var c = s.Campaign;
            var ev = sim.DrainEvents().ToList();
            Assert.AreEqual(Element.Earth, ev.OfType<RivalStepStarted>().Single().Crystal, "the first step starts with the campaign");
            Assert.AreEqual(1, c.RivalProgress, "+1 on the start tick");

            ev.AddRange(Until(sim, () => Held(sim, Element.Earth) == Custody.RivalTransit, 6000, "the seizure"));
            Assert.AreEqual(4800, c.RivalProgress, "Earth's march is 4800 (8 minutes)");
            long seized = s.Tick;
            Assert.AreEqual(RivalPhase.Carrying, c.Rival.Phase);
            Assert.AreEqual(new MarkerRef("glade", "quarry", "pedestal"), c.Crystal(Element.Earth).At);
            Assert.AreEqual(0, CampaignApi.RivalView(s).Reports.Count(r => r.Kind == RivalReportKind.Seized), "not yet known");

            ev.AddRange(Until(sim, () => Held(sim, Element.Earth) == Custody.AtRivalAltar, 2000, "the altar"));
            Assert.AreEqual(4800 + 900, c.RivalProgress, "carry 900");
            Assert.AreEqual(new MarkerRef("ash_cut", "quarry", "altar"), c.Crystal(Element.Earth).At);
            Assert.AreEqual(1, c.Rival.Step);
            Assert.AreEqual(RivalPhase.Marching, c.Rival.Phase);
            Assert.AreEqual(Element.Fire, ev.OfType<RivalStepStarted>().Last().Crystal);

            ev.AddRange(Until(sim, () => ev.OfType<RivalReported>().Any(r => r.Kind == RivalReportKind.Seized), 400, "the report"));
            Assert.AreEqual(seized + 300, ev.OfType<RivalReported>().First(r => r.Kind == RivalReportKind.Seized).Tick, "ReportDelay 300");
            Assert.AreEqual(Custody.RivalTransit, CampaignApi.Belief(s, Element.Earth).Custody);
            Assert.AreEqual(SourceKind.Report, CampaignApi.Belief(s, Element.Earth).Source);
            Assert.IsTrue(CampaignApi.Journal(s).Any(j => j.Text.Contains("smoke at the Old Quarry")));

            ev.AddRange(Until(sim, () => Held(sim, Element.Fire) == Custody.RivalTransit, 4000, "the Kilnheart"));
            Assert.AreEqual(4800 + 900 + 3600, c.RivalProgress);
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count(), "nothing but time so far");
        }

        [Test]
        public void Charges_WrongAnswer_AndTheRivalSpendsThemAtOnce()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            Follow(sim, "air_watch");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim));
            Assert.AreEqual("air_test", CampaignApi.Party(s).ChoiceId);
            long before = c.Rival.StepProgress;
            var ev = Do(sim, new AdviseMira { ChoiceId = "air_test", Answer = 0 });
            var wrong = ev.OfType<ChoiceAnswered>().Single();
            Assert.IsFalse(wrong.Correct);
            Assert.IsTrue(wrong.ByPlayer);
            Assert.AreEqual(300, ev.OfType<RivalAdvanced>().Single().Amount);
            Assert.AreEqual(RivalCharges.WrongAnswer, ev.OfType<RivalAdvanced>().Single().Why);
            Assert.AreEqual(before + 301, c.Rival.StepProgress, "the expedition used the time at once");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim), "look round and try again");
            ev = Do(sim, new AdviseMira { ChoiceId = "air_test", Answer = 1 });
            Assert.IsTrue(ev.OfType<ChoiceAnswered>().Single().Correct);
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count());
            Assert.IsTrue(c.Knows("air.markers_known"));
            Assert.IsNull(Lead(sim, "air_watch"), "done");
            Assert.AreEqual(PartyMode.Idle, Mode(sim));
        }

        [Test]
        public void AshCampFalls_ExpeditionPauses900()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            sim.Step(100);
            var inject = new OverrunAt { At = s.Tick + 1, Attacker = PlayerIds.Human, Defender = PlayerIds.Rival, Kind = CampOverrunKind.CampLost };
            sim.AddSystem(SystemOrder.Campaign - 1, inject);
            sim.DrainEvents();
            sim.Step();
            var ev = sim.DrainEvents();
            var paused = ev.OfType<RivalPaused>().Single();
            Assert.AreEqual(RivalPhase.Paused, c.Rival.Phase);
            long step0 = c.Rival.StepProgress;
            sim.Step(898);
            Assert.AreEqual(step0, c.Rival.StepProgress, "stopped");
            Assert.AreEqual(RivalPhase.Paused, c.Rival.Phase);
            sim.Step(10);
            Assert.AreEqual(RivalPhase.Marching, c.Rival.Phase);
            Assert.Greater(c.Rival.StepProgress, step0);
            Assert.AreEqual(paused.Until, c.Rival.PausedUntilProgress);
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count(), "a pause is not a charge");
            // The seizure comes 900 later than it would have.
            Until(sim, () => Held(sim, Element.Earth) == Custody.RivalTransit, 6000, "the seizure");
            Assert.AreEqual(4800 + 900, c.RivalProgress);
        }

        [Test]
        public void YourCampOverrun_Charges1800_PartyRetreatsHome_ThresholdAbandoned_NobodyDies()
        {
            var sim = CampaignOutcomeTests.KeeperHasEarth();
            var s = sim.State;
            var c = s.Campaign;
            Follow(sim, "@recover:earth");
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim));
            sim.AddSystem(SystemOrder.Campaign - 1, new OverrunAt { At = s.Tick + 1, Attacker = PlayerIds.Rival, Defender = PlayerIds.Human, Kind = CampOverrunKind.Looted });
            long p = c.RivalProgress;
            sim.Step();
            var ev = sim.DrainEvents();
            Assert.AreEqual(1800, ev.OfType<RivalAdvanced>().Single().Amount, "a looted home camp costs the same as a lost camp");
            Assert.AreEqual(RivalCharges.CampOverrun, ev.OfType<RivalAdvanced>().Single().Why);
            Assert.AreEqual(p + 1800 + 1, c.RivalProgress);
            var back = ev.OfType<PartyRetreated>().Single();
            Assert.AreEqual("camp_overrun", back.Why);
            Assert.AreEqual(MarkerRef.Surface("home", "fire"), back.To);
            Assert.AreEqual(PartyMode.Travelling, Mode(sim), "on foot");
            Assert.AreEqual("", CampaignApi.Party(s).EncounterId, "the uncommitted threshold is abandoned");
            Until(sim, () => Settled(sim), 3000, "home");
            Assert.AreEqual(MarkerRef.Surface("home", "fire"), CampaignApi.Party(s).At);
            Assert.AreEqual(6, CampaignApi.Companions(s).Count(k => k.Alive));
            Assert.AreEqual(CampaignPhase.Running, c.Phase);

            // During a committed duel the world clock is paused in the game; if one still came, only the charge applies.
            Follow(sim, "@recover:earth");
            Do(sim, new CommitEncounter { EncounterId = "keeper_earth" });
            sim.AddSystem(SystemOrder.Campaign - 1, new OverrunAt { At = s.Tick + 1, Attacker = PlayerIds.Rival, Defender = PlayerIds.Human });
            sim.Step();
            ev = sim.DrainEvents();
            Assert.AreEqual(1800, ev.OfType<RivalAdvanced>().Single().Amount);
            Assert.AreEqual(PartyMode.InEncounter, Mode(sim));
            Assert.IsNotNull(CampaignApi.Attempt(s));
        }

        [Test]
        public void DevRivalNextStep_FinishesTheCurrentStep()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var c = sim.State.Campaign;
            var ev = Do(sim, new DevRivalNextStep());
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Earth));
            Assert.AreEqual(1, c.Rival.Step);
            Assert.IsTrue(ev.OfType<RivalAdvanced>().All(x => x.Why == RivalCharges.Dev));
            for (int i = 0; i < 3; i++) Do(sim, new DevRivalNextStep());
            Assert.AreEqual(RivalPhase.PlanDone, c.Rival.Phase);
            foreach (Element e in System.Enum.GetValues(typeof(Element))) Assert.AreEqual(Custody.AtRivalAltar, Held(sim, e));
            CollectionAssert.AreEqual(new[] { Element.Earth, Element.Fire, Element.Water, Element.Air }, c.EverOnAltar);
            Assert.AreEqual(4, Ledger.LedgerApi.Points(sim.State, PlayerIds.Rival));
            Assert.AreEqual(1, sim.State.Demons.Tuning.SevensPerDraw, "four on altars: demons as often as they can come");
            long spent = c.RivalSpent;
            sim.Step(100);
            Assert.AreEqual(c.RivalProgress, c.RivalSpent, "a finished plan waits and keeps nothing back");
        }
    }
}
