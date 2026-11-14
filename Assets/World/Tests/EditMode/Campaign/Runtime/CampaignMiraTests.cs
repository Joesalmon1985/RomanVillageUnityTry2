using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// PRD_CRYSTALS 5.4 and 5.5: Mira's lead queue, what she does with and without the player, choices answered by the
    /// same step whoever answers, fallbacks that keep every essential clue reachable with no companion, and the scout.
    /// </summary>
    public class CampaignMiraTests
    {
        [Test]
        public void LeadQueue_BestThree_StableIds_DeclineWaits600()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var q = CampaignApi.Leads(s);
            Assert.AreEqual(3, q.Count, "QueueLength");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, q.Select(l => l.Rank).ToArray());
            Assert.IsTrue(q[0].Score >= q[1].Score && q[1].Score >= q[2].Score);
            var ids = q.Select(l => l.Id).ToArray();
            sim.Step(200);
            CollectionAssert.AreEqual(ids, CampaignApi.Leads(s).Select(l => l.Id).ToArray(), "ids stay while the leads stay");
            Assert.AreEqual(PartyMode.Idle, Mode(sim), "with the player here and no answer, the patient test Mira waits");

            var top = q[0];
            var ev = Do(sim, new DeclineLead { LeadId = top.Id });
            Assert.AreEqual("declined", ev.OfType<LeadRemoved>().Single(x => x.LeadId == top.Id).Why);
            Assert.AreNotEqual(top.DefId, CampaignApi.TopLead(s).DefId, "the next lead is offered");
            sim.Step(590);
            Assert.IsNull(Lead(sim, top.DefId), "it waits DeclineCooldown");
            Until(sim, () => Lead(sim, top.DefId) != null, 40, "the declined lead back");
            Assert.AreNotEqual(top.Id, Lead(sim, top.DefId).Id, "a lead back in the queue has a new id");
            Assert.AreEqual("unknown_lead", sim.Submit(new AcceptLead { LeadId = top.Id }).Reason);
        }

        [Test]
        public void Present_NoAnswer_SheGoesAnywayAfterLeadPatience()
        {
            var sim = CampaignSamples.Started(7);
            var s = sim.State;
            var top = CampaignApi.TopLead(s);
            long t0 = s.Tick;
            var ev = Until(sim, () => Mode(sim) == PartyMode.Travelling, 1000, "Mira setting out");
            Assert.AreEqual(450, s.Tick - t0, "LeadPatience");
            Assert.AreEqual(top.Id, ev.OfType<PartySetOut>().First().LeadId);
            StringAssert.Contains("went on by herself", CampaignApi.Journal(s).Last().Text);
            Assert.AreEqual("Mira is on her way to the Stonemark Glade.", CampaignApi.WhereIsMira(s));
            Assert.IsTrue(CampaignApi.PartyPosition(s, 0.5, out _, out _, out double t) && t > 0 && t < 1);
            Assert.AreEqual("unknown_lead", sim.Submit(new AcceptLead { LeadId = 999 }).Reason);
        }

        [Test]
        public void Choice_PlayerAnswersRight_OrNoAnswer_SheFindsItInTheWorld()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            Follow(sim, "air_watch");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim));
            long asked = s.Tick;
            // No answer: after ChoicePatience she treats it as away, goes to the door, studies, comes back, answers right.
            var ev = Until(sim, () => c.Knows("air.markers_known"), 1000, "her own answer");
            var answered = ev.OfType<ChoiceAnswered>().Single();
            Assert.IsTrue(answered.Correct);
            Assert.IsFalse(answered.ByPlayer);
            Assert.AreEqual(1, answered.Answer);
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count(), "she never guesses, so it costs no charge");
            Assert.AreEqual(asked + 300, ev.OfType<PartySetOut>().First().Tick, "ChoicePatience");
            Assert.AreEqual(MarkerRef.Surface("watch", "door"), ev.OfType<PartySetOut>().First().To, "the marker that reveals the answer");
            Assert.IsTrue(ev.OfType<PartySetOut>().Count() >= 2, "there and back");
            Assert.IsTrue(CampaignApi.Journal(s).Any(j => j.Text.Contains("found the answer")));
            Assert.AreEqual(0, c.OpenChoices.Count);
            Assert.AreEqual(MarkerRef.Surface("watch", "orrin"), CampaignApi.Party(s).At);
        }

        [Test]
        public void Choice_LeftOpenByAnOverrun_ComesBackAsALead()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            Follow(sim, "air_watch");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim));
            CampaignRuntime.OnCampOverrun(s, c, PlayerIds.Rival, PlayerIds.Human, null);
            Assert.AreEqual(PartyMode.Travelling, Mode(sim));
            Assert.AreEqual("air_test", c.OpenChoices.Single().ChoiceId, "the question is still open");
            Until(sim, () => Settled(sim), 2000, "home");
            Follow(sim, "@choice:air_test");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim), "asked again where it was asked");
            Do(sim, new AdviseMira { ChoiceId = "air_test", Answer = 1 });
            Assert.IsTrue(c.Knows("air.markers_known"));
        }

        [Test]
        public void Fallback_ZeroCompanions_EveryEssentialClueStillReachable()
        {
            var sim = WorldSimFactory.CreateNew(7);
            var s = sim.State;
            CampaignRuntime.Start(s, Patient(), null);
            foreach (var k in s.Campaign.Companions) { k.Alive = false; k.FellAt = "dev"; k.FellTick = 0; }
            sim.Step();
            var c = s.Campaign;
            Assert.IsFalse(c.Knows("earth.glade_known"), "Odo's shortcut died with him");
            Assert.IsNotNull(Lead(sim, "@clue:earth_sawyer"), "the sawyer, who cannot die, is the fallback");
            StringAssert.Contains("the home camp", Lead(sim, "@clue:earth_sawyer").Reason);
            var ev = Follow(sim, "@clue:earth_sawyer");
            Assert.IsTrue(c.Knows("earth.glade_known"));
            Assert.AreEqual(SourceKind.Witness, ev.OfType<FactLearned>().Single(f => f.Fact == "earth.glade_known").Source);
            Follow(sim, "earth_stone");
            Follow(sim, "@take:earth");
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth), "the whole Earth route with nobody left");
            Assert.IsNull(CampaignApi.NextVolunteer(s));
        }

        [Test]
        public void Scout_SentByThePlayer_ReportsTheTruth_AndBlocksACommit()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            Do(sim, new DevRivalNextStep());
            Do(sim, new DevRivalNextStep());
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Fire));
            Assert.AreEqual(Custody.AtSite, CampaignApi.Belief(s, Element.Fire).Custody);
            var ev = Do(sim, new SuggestScout { LocationId = "forge" });
            Assert.AreEqual("tansy", ev.OfType<ScoutSent>().Single().CompanionId);
            Assert.IsTrue(c.Scout.Out);
            Assert.AreEqual("scout_out", sim.Submit(new SuggestScout { LocationId = "kiln" }).Reason);
            ev = Until(sim, () => !c.Scout.Out, 3000, "Tansy back");
            Assert.AreEqual(Element.Fire, ev.OfType<ScoutReported>().Single().Crystal);
            Assert.AreEqual(1, ev.OfType<ScoutReturned>().Count());
            var b = CampaignApi.Belief(s, Element.Fire);
            Assert.AreEqual(Custody.AtRivalAltar, b.Custody);
            Assert.AreEqual(SourceKind.Scout, b.Source);
            Assert.AreEqual(MarkerRef.Surface("forge", "altar"), b.At);
            sim.Step();
            Assert.IsNotNull(Lead(sim, "@recover:fire"), "now the party knows where to win it back");
        }

        /// <summary>
        /// The player says "Go on without me" at the start and never comes back. Mira does everything but the lethal
        /// duels and the restorations: she reads, travels, takes unguarded crystals, answers riddles after studying,
        /// sends the scout, waits ThresholdWait at a threshold and withdraws, waits at towers. Nobody dies.
        /// </summary>
        [Test]
        public void Away_SheNeverFightsOrRestores_ButDoesEverythingElse()
        {
            var d = CampaignSamples.Skeleton();
            d.Lead("air_watch").Crystal = "";   // Orrin stays worth a visit after the Skyshard is found, so she meets his riddle
            var sim = CampaignSamples.Started(7, d);
            var s = sim.State;
            var c = s.Campaign;
            Quiet(sim);
            Do(sim, new SetCompany { Present = false });
            Do(sim, new DevRivalNextStep());   // the Deepstone goes to Dolm's altar
            var ev = SimTestKit.Step(sim, 20000);
            Assert.AreEqual(0, ev.OfType<EncounterCommitted>().Count(), "never a lethal duel alone");
            Assert.AreEqual(0, ev.OfType<CrystalRestored>().Count(), "never a restoration alone");
            Assert.AreEqual(6, CampaignApi.Companions(s).Count(k => k.Alive));
            Assert.AreEqual(0, c.RestoredOrder.Count);
            Assert.IsTrue(ev.OfType<CrystalCustodyChanged>().Any(x => x.To == Custody.Carried), "she takes unguarded crystals");
            var asked = ev.OfType<ChoiceAnswered>().ToList();
            Assert.IsTrue(asked.Count > 0 && asked.All(a => a.Correct && !a.ByPlayer), "riddles answered right, by herself");
            Assert.IsTrue(ev.OfType<ScoutSent>().Any(), "she sends Tansy when a belief goes stale");
            var th = ev.OfType<EncounterThreshold>().FirstOrDefault(x => x.EncounterId == "keeper_earth");
            Assert.IsNotNull(th, "she reached Dolm's threshold");
            var back = ev.OfType<PartyRetreated>().First(x => x.Why == "threshold_wait");
            Assert.AreEqual(900, back.Tick - th.Tick, "ThresholdWait, then she withdraws");
            Assert.AreEqual(new MarkerRef("glade", "quarry", "safe"), back.To);
            Assert.IsTrue(CampaignApi.Journal(s).Any(j => j.Text.Contains("waits for you")));
            Assert.IsTrue(CampaignApi.Journal(s).Any(j => j.Text.Contains("went on by herself")));
            Assert.AreNotEqual("", CampaignApi.WhereIsMira(s));

            // "I'm with you": the player rejoins; at Dolm's threshold the fight can now be committed.
            Do(sim, new SetCompany { Present = true });
            Until(sim, () => Mode(sim) == PartyMode.AtThreshold && CampaignApi.Party(s).EncounterId == "keeper_earth"
                || Lead(sim, "@recover:earth") != null && Settled(sim), 3000, "Dolm's threshold on offer");
            if (Mode(sim) != PartyMode.AtThreshold) Follow(sim, "@recover:earth");
            Assert.IsTrue(sim.Check(new CommitEncounter { EncounterId = "keeper_earth" }).Ok);
        }

        [Test]
        public void Away_AtAThreshold_WithNothingElseOpen_SheHoldsThere()
        {
            var d = Patient();
            d.Leads.Clear();   // only the runtime's own leads
            var sim = CampaignSamples.Started(7, d);
            var s = sim.State;
            var c = s.Campaign;
            Quiet(sim);
            Do(sim, new DevRivalNextStep());
            CampaignRuntime.Learn(c, "earth.stair_open", SourceKind.Marker, null);
            CampaignRuntime.SetBeliefTruth(s, c, Element.Earth, SourceKind.Report, null);
            foreach (var e in new[] { Element.Fire, Element.Water, Element.Air })
                SimTestKit.Do(sim, new DevSetCustody { Crystal = e, To = Custody.Restored });
            Follow(sim, "@recover:earth");
            Do(sim, new SetCompany { Present = false });
            var ev = Until(sim, () => ev0(sim), 4000, "back at the threshold, holding");
            Assert.IsTrue(c.Party.HoldAtThreshold);
            Assert.AreEqual("keeper_earth", c.Party.WithdrawnFrom);
            long p = c.RivalProgress;
            sim.Step(3000);
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim), "no time limit while she holds");
            Assert.AreEqual("Mira stands ready at the Old Quarry.", CampaignApi.WhereIsMira(s));
            Do(sim, new SetCompany { Present = true });
            Assert.IsFalse(c.Party.HoldAtThreshold);
            if (c.Scout.Out) Assert.AreEqual("scout_out", sim.Check(new CommitEncounter { EncounterId = "keeper_earth" }).Reason, "wait for Tansy");
            Until(sim, () => !c.Scout.Out, 3000, "Tansy back");
            Assert.IsTrue(sim.Check(new CommitEncounter { EncounterId = "keeper_earth" }).Ok);

            bool ev0(WorldSim x) => Mode(x) == PartyMode.AtThreshold && x.State.Campaign.Party.HoldAtThreshold;
        }
    }
}
