using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// PRD_CRYSTALS 5.6: attempts and the outcome table, each applied once with the attempt id. Six lethal losses cost
    /// the six companions in volunteer order and the run can still be won; a seventh ends it; clash and stalemate are a
    /// living retreat (cheaper with the shield-bearer); surrender after committing is a defeat; withdrawing before is free.
    /// </summary>
    public class CampaignOutcomeTests
    {
        /// <summary>The Deepstone is on Dolm's altar, the party knows it, and the stair is open.</summary>
        public static WorldSim KeeperHasEarth(ulong seed = 7)
        {
            var sim = CampaignSamples.Started(seed, Patient());
            var s = sim.State;
            Do(sim, new DevRivalNextStep());
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Earth));
            CampaignRuntime.Learn(s.Campaign, "earth.stair_open", SourceKind.Marker, null);
            CampaignRuntime.SetBeliefTruth(s, s.Campaign, Element.Earth, SourceKind.Report, null);
            return sim;
        }

        /// <summary>To Dolm's threshold and commit; returns the attempt id.</summary>
        public static int ToTheFight(WorldSim sim)
        {
            if (Mode(sim) != PartyMode.AtThreshold) Follow(sim, "@recover:earth");
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim));
            return Do(sim, new CommitEncounter { EncounterId = "keeper_earth" }).OfType<EncounterCommitted>().Single().AttemptId;
        }

        [Test]
        public void SixDeathsInVolunteerOrder_ThenAWin()
        {
            var sim = KeeperHasEarth();
            var s = sim.State;
            var c = s.Campaign;
            var all = new List<SimEvent>();
            for (int i = 0; i < 6; i++)
            {
                string expected = CampaignSamples.CompanionIds[i];
                var there = Follow(sim, "@recover:earth");
                Assert.AreEqual(expected, there.OfType<EncounterThreshold>().Last().Volunteer, "named before the fight");
                int id = ToTheFight(sim);
                Assert.AreEqual(expected, CampaignApi.Attempt(s).Volunteer);
                var ev = Do(sim, new ResolveEncounter { AttemptId = id, Result = DuelResult.Defeat });
                all.AddRange(ev);
                Assert.AreEqual(expected, ev.OfType<CompanionFell>().Single().CompanionId, "loss " + (i + 1));
                Assert.AreEqual(900, ev.OfType<RivalAdvanced>().Single().Amount);
                Assert.AreEqual(new MarkerRef("glade", "quarry", "safe"), CampaignApi.Party(s).At);
                Assert.AreEqual(CampaignPhase.Running, c.Phase);
            }
            Assert.IsNull(CampaignApi.NextVolunteer(s));
            Assert.AreEqual(0, CampaignApi.Companions(s).Count(k => k.Alive));

            // With nobody left the party can still commit and win (CR-30).
            int last = ToTheFight(sim);
            Assert.AreEqual("", CampaignApi.Attempt(s).Volunteer);
            Do(sim, new ResolveEncounter { AttemptId = last, Result = DuelResult.Victory });
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth));
            Assert.AreEqual(CampaignPhase.Running, c.Phase);

            var rec = CampaignApi.Record(s);
            CollectionAssert.AreEqual(CampaignSamples.CompanionIds, rec.Fallen.Select(f => f.Id).ToArray(), "the fallen in the order they fell");
            Assert.IsTrue(rec.Fallen.All(f => f.Place == "the Old Quarry" && f.EncounterId == "keeper_earth" && f.Cause == FallCause.Defeat));
            Assert.AreEqual(0, rec.Survivors.Count);
            Assert.AreEqual(7, c.Resolved.Count);
        }

        [Test]
        public void SeventhLethalLoss_EndsTheRun()
        {
            var sim = KeeperHasEarth();
            var s = sim.State;
            var c = s.Campaign;
            for (int i = 0; i < 6; i++) Do(sim, new ResolveEncounter { AttemptId = ToTheFight(sim), Result = DuelResult.Defeat });
            int id = ToTheFight(sim);
            var ev = Do(sim, new ResolveEncounter { AttemptId = id, Result = DuelResult.Defeat });
            var ended = ev.OfType<CampaignEnded>().Single();
            Assert.IsFalse(ended.Won);
            Assert.AreEqual("unwoven", ended.EndingId);
            Assert.AreEqual(CampaignPhase.Lost, c.Phase);
            Assert.AreEqual("unwoven", CampaignApi.Record(s).EndingId);
            Assert.AreEqual(6, ended.Record.Fallen.Count);
            Assert.AreEqual(0, ev.OfType<CompanionFell>().Count(), "nobody was left to fall");
            Assert.IsFalse(CampaignApi.IsRunning(s));
            Assert.AreEqual("campaign_over", sim.Submit(new SetCompany { Present = false }).Reason);
            Assert.AreEqual("already_resolved", sim.Submit(new ResolveEncounter { AttemptId = id, Result = DuelResult.Victory }).Reason);
            long p = c.RivalProgress;
            sim.Step(50);
            Assert.AreEqual(p, c.RivalProgress, "the clock stops with the run");
        }

        [Test]
        public void ClashAndStalemate_LivingRetreat_ShieldHalvesTheCharge()
        {
            var sim = KeeperHasEarth();
            var s = sim.State;
            var ev = Do(sim, new ResolveEncounter { AttemptId = ToTheFight(sim), Result = DuelResult.Clash });
            Assert.AreEqual(0, ev.OfType<CompanionFell>().Count());
            Assert.AreEqual(300, ev.OfType<RivalAdvanced>().Single().Amount, "Garrick covers the retreat: half");
            Assert.AreEqual(RivalCharges.Draw, ev.OfType<RivalAdvanced>().Single().Why);
            Assert.AreEqual("draw", ev.OfType<PartyRetreated>().Single().Why);
            Assert.AreEqual(new MarkerRef("glade", "quarry", "safe"), CampaignApi.Party(s).At);
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Earth), "the encounter stays unresolved");

            Do(sim, new DevSetCompanion { Id = "garrick", Alive = false });
            ev = Do(sim, new ResolveEncounter { AttemptId = ToTheFight(sim), Result = DuelResult.Stalemate });
            Assert.AreEqual(600, ev.OfType<RivalAdvanced>().Single().Amount);
            Assert.AreEqual(5, CampaignApi.Companions(s).Count(k => k.Alive));
        }

        [Test]
        public void SurrenderAfterCommit_IsADefeat_WithdrawBefore_IsFree()
        {
            var sim = KeeperHasEarth();
            var s = sim.State;
            var c = s.Campaign;
            var ev = Do(sim, new ResolveEncounter { AttemptId = ToTheFight(sim), Result = DuelResult.Surrender });
            Assert.AreEqual("garrick", ev.OfType<CompanionFell>().Single().CompanionId);
            Assert.AreEqual(FallCause.Surrender, c.Companion("garrick").Cause);
            Assert.AreEqual(900, ev.OfType<RivalAdvanced>().Single().Amount);

            Follow(sim, "@recover:earth");
            long p = c.RivalProgress;
            ev = Do(sim, new WithdrawFromEncounter { EncounterId = "keeper_earth" });
            Assert.AreEqual("withdraw", ev.OfType<PartyRetreated>().Single().Why);
            ev.AddRange(Until(sim, () => Settled(sim), 500, "back at the safe point"));
            Assert.AreEqual(new MarkerRef("glade", "quarry", "safe"), CampaignApi.Party(s).At);
            Assert.AreEqual(PartyMode.Idle, Mode(sim), "the player is here: she simply waits for the next word");
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count(), "no charge");
            Assert.AreEqual(p + (s.Tick - ev.First().Tick) + 1, c.RivalProgress, "only the clock");
            Assert.AreEqual(5, CampaignApi.Companions(s).Count(k => k.Alive));
            Assert.IsNull(CampaignApi.Attempt(s));
            Assert.AreEqual("not_at_threshold", sim.Submit(new CommitEncounter { EncounterId = "keeper_earth" }).Reason);
        }

        [Test]
        public void ResolveTwice_AppliedOnce_EvenAcrossALoad()
        {
            var sim = KeeperHasEarth();
            var s = sim.State;
            var c = s.Campaign;
            int id = ToTheFight(sim);
            Assert.IsTrue(sim.Submit(new ResolveEncounter { AttemptId = id, Result = DuelResult.Defeat }).Ok);
            Assert.IsTrue(sim.Submit(new ResolveEncounter { AttemptId = id, Result = DuelResult.Defeat }).Ok, "both callbacks arrive before the tick");
            sim.Step();
            var ev = sim.DrainEvents();
            Assert.AreEqual(1, ev.OfType<EncounterResolved>().Count());
            Assert.AreEqual(1, ev.OfType<CompanionFell>().Count());
            Assert.AreEqual(1, ev.OfType<RivalAdvanced>().Count());
            Assert.AreEqual("already_resolved", ev.OfType<CommandRejected>().Single().Result.Reason);
            Assert.AreEqual(5, CampaignApi.Companions(s).Count(k => k.Alive));

            var loaded = SaveLoad(sim);
            Assert.AreEqual("already_resolved", loaded.Submit(new ResolveEncounter { AttemptId = id, Result = DuelResult.Victory }).Reason, "a reload cannot apply it again");
            Assert.AreEqual("unknown_attempt", loaded.Submit(new ResolveEncounter { AttemptId = id + 1, Result = DuelResult.Victory }).Reason);
        }

        [Test]
        public void FriendlyAndPractice_NeverMakeAnAttempt()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            Follow(sim, "air_watch");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim), "Orrin asks; his place is not a threshold");
            Assert.AreEqual("not_at_threshold", sim.Submit(new CommitEncounter { EncounterId = "orrin_demo" }).Reason);
            Assert.AreEqual(0, s.Campaign.NextAttemptId - 1);
        }
    }
}
