using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Economy;
using World.Sim.Ledger;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// PRD_CRYSTALS 5.2 (custody) and the two Earth routes of Slice 1 on the sample skeleton: first arrival
    /// (AtSite, Carried, Restored, no lethal duel) and stolen then recovered (AtSite, RivalTransit, AtRivalAltar, Carried,
    /// Restored, one loss costing Garrick and +900, one win). Every custody change is one event.
    /// </summary>
    public class CampaignCustodyTests
    {
        [Test]
        public void Earth_FirstArrival_TakenCarriedRestored_NoDuel()
        {
            var sim = Sample(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            var all = new List<SimEvent>();
            Assert.IsTrue(c.Knows("earth.glade_known"), "Odo reads the forest marks on sight");
            Assert.AreEqual("earth_stone", CampaignApi.TopLead(s).DefId, "the best lead first");
            StringAssert.Contains("foresters", CampaignApi.TopLead(s).Reason);

            all.AddRange(Follow(sim, "earth_stone"));
            Assert.IsTrue(c.Knows("earth.stair_open"), "Mira read the boundary stone");
            Assert.IsTrue(all.OfType<QuestEventFired>().Any(x => x.EventId == "earth_read_stone"));
            Assert.AreEqual(MarkerRef.Surface("glade", "stone"), CampaignApi.Party(s).At);

            // No lead of the pack goes to the quarry pedestal, so the runtime makes one; it goes down the stair.
            var take = Lead(sim, "@take:earth") ?? CampaignApi.Leads(s).First();
            all.AddRange(Follow(sim, "@take:earth"));
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth));
            Assert.AreEqual(new MarkerRef("glade", "quarry", "pedestal"), CampaignApi.Party(s).At);
            Assert.IsTrue(c.Knows("tower.earth_known"), "the pedestal names the tower (earth_take)");
            Assert.IsTrue(all.OfType<PartyArrived>().Any(x => x.At.IsInterior), "the route went underground");
            Assert.AreEqual(Custody.Carried, CampaignApi.Belief(s, Element.Earth).Custody);

            all.AddRange(Follow(sim, "@restore:earth"));
            Assert.AreEqual(PartyMode.WaitingForPlayer, Mode(sim), "at the tower with its crystal she waits for the player");
            Assert.AreEqual("tower_earth", CampaignApi.Party(s).At.Location);
            int hills0 = s.Economy.Tuning.GrowTicks[(int)Terrain.Hills];
            all.AddRange(Do(sim, new ConfirmRestore { Crystal = Element.Earth }));
            all.AddRange(Until(sim, () => Held(sim, Element.Earth) == Campaign.Custody.Restored, 200, "restoration"));
            Assert.AreEqual(c.Definition.Tuning.RestoreTicks, all.OfType<CrystalRestored>().Single().Tick - all.OfType<CommandApplied>().Last(x => x.Command is ConfirmRestore).Tick);

            CollectionAssert.AreEqual(new[] { Campaign.Custody.AtSite, Campaign.Custody.Carried, Campaign.Custody.Restored }, Chain(all, Element.Earth));
            Assert.AreEqual(0, all.OfType<EncounterCommitted>().Count(), "first arrival: no lethal duel");
            CollectionAssert.AreEqual(new[] { Element.Earth }, c.RestoredOrder);
            Assert.AreEqual(1, all.OfType<CrystalRestored>().Single().Count);
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Human, "crystal:earth"));
            Assert.AreEqual(c.BaseSevensPerDraw + 1, s.Demons.Tuning.SevensPerDraw, "demons rarer with each restoration");
            Assert.AreEqual(c.BaseGrowTicks[(int)Terrain.Hills] * 100 / 120, s.Economy.Tuning.GrowTicks[(int)Terrain.Hills], "the Deepstone's boon");
            Assert.AreNotEqual(hills0, s.Economy.Tuning.GrowTicks[(int)Terrain.Hills]);
            Assert.AreEqual(c.BaseGrowTicks[(int)Terrain.Fields], s.Economy.Tuning.GrowTicks[(int)Terrain.Fields]);
            Assert.AreEqual("One tower lit.", CampaignApi.Line(s, "mira_one_lit"), "lines follow the count");
            Assert.IsTrue(CampaignApi.Journal(s).Any(j => j.Text.Contains("is lit")));
            Assert.AreEqual(Custody.Restored, CampaignApi.Belief(s, Element.Earth).Custody);
            Assert.Less(c.RivalProgress, 4800, "the direct player beat the expedition");
            Assert.AreEqual(CampaignPhase.Running, c.Phase);
        }

        [Test]
        public void Earth_StolenThenRecovered_OneLossOneWin()
        {
            // News travels slowly here, so the party finds the empty pedestal itself before any report arrives.
            var d = Patient();
            d.Tuning.ReportDelay = 20000;
            var sim = Sample(7, d);
            var s = sim.State;
            var c = s.Campaign;
            var all = new List<SimEvent>();
            // The Ash expedition is one step ahead: it reaches the quarry at once.
            all.AddRange(Do(sim, new DevSetRivalProgress { Value = 4800 }));
            Assert.AreEqual(Custody.RivalTransit, Held(sim, Element.Earth));
            Assert.AreEqual(Custody.AtSite, CampaignApi.Belief(s, Element.Earth).Custody, "the party has not heard yet");
            Assert.AreEqual(RivalPhase.Carrying, c.Rival.Phase);

            all.AddRange(Follow(sim, "earth_stone"));
            all.AddRange(Until(sim, () => Held(sim, Element.Earth) == Campaign.Custody.AtRivalAltar, 1000, "the altar"));
            Assert.AreEqual(new MarkerRef("ash_cut", "quarry", "altar"), c.Crystal(Element.Earth).At);
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Rival, "crystal:earth"), "the Ash Clan's point, once");

            // The party still believes it is on its pedestal and goes there: empty, the tally, the truth.
            all.AddRange(Follow(sim, "@take:earth"));
            Assert.IsTrue(c.Knows("earth.stolen_seen") && c.Knows("earth.tally_read"), "earth_empty fired at the empty pedestal");
            Assert.AreEqual(Custody.AtRivalAltar, CampaignApi.Belief(s, Element.Earth).Custody);

            all.AddRange(Follow(sim, "@recover:earth"));
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim));
            Assert.AreEqual("keeper_earth", CampaignApi.Party(s).EncounterId);
            var th = all.OfType<EncounterThreshold>().Last();
            Assert.AreEqual("garrick", th.Volunteer, "the volunteer is named before the fight");

            // One loss: Garrick holds the way out and dies; the party is at the safe point; the rival gains 900.
            var committed = Do(sim, new CommitEncounter { EncounterId = "keeper_earth" }).OfType<EncounterCommitted>().Single();
            long before = c.RivalProgress;
            var lost = Do(sim, new ResolveEncounter { AttemptId = committed.AttemptId, Result = DuelResult.Defeat });
            all.AddRange(lost);
            Assert.IsFalse(c.Companion("garrick").Alive);
            Assert.AreEqual("keeper_earth", c.Companion("garrick").FellAt);
            Assert.AreEqual(new MarkerRef("glade", "quarry", "safe"), CampaignApi.Party(s).At);
            Assert.AreEqual(PartyMode.Idle, Mode(sim));
            Assert.AreEqual(900, lost.OfType<RivalAdvanced>().Single().Amount);
            Assert.AreEqual(RivalCharges.Defeat, lost.OfType<RivalAdvanced>().Single().Why);
            Assert.AreEqual(before + 900 + 1, c.RivalProgress);
            Assert.AreEqual("defeat", lost.OfType<PartyRetreated>().Single().Why);
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Earth), "the enemy stays");
            Assert.IsTrue(c.Knows("earth.stolen_seen"), "what was found is kept");

            // Go again and win: the Deepstone comes off the altar and the Ash cut opens.
            all.AddRange(Follow(sim, "@recover:earth"));
            Assert.AreEqual("ivo", all.OfType<EncounterThreshold>().Last().Volunteer);
            var again = Do(sim, new CommitEncounter { EncounterId = "keeper_earth" }).OfType<EncounterCommitted>().Single();
            Assert.AreEqual(committed.AttemptId + 1, again.AttemptId);
            all.AddRange(Do(sim, new ResolveEncounter { AttemptId = again.AttemptId, Result = DuelResult.Victory }));
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth));
            Assert.IsTrue(c.Knows("earth.ash_cut_open") && c.Knows("tower.earth_known"));
            Assert.IsTrue(CampaignApi.IsOpen(s, "ash_cut"));
            CollectionAssert.Contains(c.CompletedEncounters, "keeper_earth");
            Assert.AreEqual(5, CampaignApi.Companions(s).Count(k => k.Alive));

            all.AddRange(Follow(sim, "@restore:earth"));
            all.AddRange(Do(sim, new ConfirmRestore { Crystal = Element.Earth }));
            all.AddRange(Until(sim, () => Held(sim, Element.Earth) == Campaign.Custody.Restored, 200, "restoration"));
            CollectionAssert.AreEqual(new[]
            {
                Campaign.Custody.AtSite, Campaign.Custody.RivalTransit, Campaign.Custody.AtRivalAltar, Campaign.Custody.Carried, Campaign.Custody.Restored,
            }, Chain(all, Element.Earth));
            Assert.AreEqual(1, LedgerApi.Points(s, PlayerIds.Rival));
            Assert.AreEqual(1, LedgerApi.Points(s, PlayerIds.Human));
            var rec = CampaignApi.Record(s);
            CollectionAssert.AreEqual(new[] { Element.Earth }, rec.EverOnAltar);
            Assert.AreEqual("garrick", rec.Fallen.Single().Id);
            Assert.AreEqual("the Old Quarry", rec.Fallen.Single().Place);
        }

        [Test]
        public void Custody_EveryTransition_OneEventEach_AndGuarded()
        {
            var sim = Sample(7, Patient());
            var s = sim.State;
            var c = s.Campaign;

            // AtSite -> RivalTransit -> AtRivalAltar by the plan (rival seizes and carries).
            var ev = Do(sim, new DevRivalNextStep());
            Assert.AreEqual(2, CustodyOf(ev, Element.Earth).Count());
            CollectionAssert.AreEqual(new[] { Campaign.Custody.AtSite, Campaign.Custody.RivalTransit, Campaign.Custody.AtRivalAltar }, Chain(ev, Element.Earth));
            Assert.AreEqual(1, c.Rival.Step, "the expedition moved on to its next step");

            // AtRivalAltar -> Carried only by winning (or an alternate route); Carried -> Restored by setting it.
            Assert.IsFalse(CampaignRuntime.SetCustody(s, Element.Earth, Campaign.Custody.AtRivalAltar, default, null), "guarded by the state it starts from");
            Assert.IsTrue(CampaignRuntime.TakeCrystal(s, c, Element.Earth, null));
            Assert.IsFalse(CampaignRuntime.TakeCrystal(s, c, Element.Earth, null), "a carried crystal cannot be taken again");
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth));
            c.Party.At = MarkerRef.Surface("tower_earth", "door");
            ev = Do(sim, new ConfirmRestore { Crystal = Element.Earth });
            ev.AddRange(Until(sim, () => Held(sim, Element.Earth) == Campaign.Custody.Restored, 200, "restored"));
            Assert.AreEqual(1, CustodyOf(ev, Element.Earth).Count());
            CampaignRuntime.Restore(s, c, Element.Earth, null);
            Assert.AreEqual(1, c.RestoredOrder.Count, "Restored is final; a second restore does nothing");

            // The rival never touches a carried crystal: it arrives, finds nothing, searches, goes on.
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Campaign.Custody.Carried });
            ev = Do(sim, new DevRivalNextStep());
            Assert.AreEqual(0, CustodyOf(ev, Element.Fire).Count());
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Fire));
            Assert.IsTrue(c.Reports.Any(r => r.Crystal == Element.Fire && r.Kind == RivalReportKind.Searching));
            Assert.AreEqual(2, c.Rival.Step);

            // A restored crystal's step is skipped outright.
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Air, To = Campaign.Custody.Restored });
            Do(sim, new DevRivalNextStep());   // water: seized and carried
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Water));
            Assert.AreEqual(RivalPhase.PlanDone, c.Rival.Phase, "air was restored, so its step was skipped and the plan is done");
        }

        [Test]
        public void Custody_PartyAtThePedestal_WinsTheTie()
        {
            var sim = Sample(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            // The party is taking the Skyshard at its pedestal when the expedition arrives.
            var take = Lead(sim, "@take:air");
            Assert.IsNotNull(take, "the Skyshard's pedestal has no pack lead, so the runtime offers one");
            Do(sim, new AcceptLead { LeadId = take.Id });
            Until(sim, () => Mode(sim) == PartyMode.Acting, 2000, "taking");
            Assert.AreEqual(QuestAction.Take, c.Party.ActingAction);
            // Make the air step current and spend the march right now.
            c.Rival.Step = 3;
            c.Rival.Phase = RivalPhase.Marching;
            c.Rival.StepProgress = 0;
            var ev = Do(sim, new DevSetRivalProgress { Value = c.RivalSpent + 3600 });
            Assert.AreEqual(Custody.AtSite, Held(sim, Element.Air), "the expedition found the party at the pedestal");
            Assert.AreEqual(RivalPhase.Searching, c.Rival.Phase);
            Until(sim, () => Held(sim, Element.Air) == Campaign.Custody.Carried, 100, "taken");
        }
    }
}
