using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Ledger;
using World.Sim.Save;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// The runtime on the shipped packs (I2), headless, through commands only and with the story contract of
    /// CAMPAIGN_FORMAT section 7 (Mira goes only where a pack lead sends her; the runtime makes no leads of its own).
    /// The authored Earth route both ways, and all 24 restoration orders on the skeleton pack.
    /// </summary>
    public class CampaignPackRunTests
    {
        private static WorldSim StartPack(string pack, System.Action<CampaignDefinition> edit = null)
        {
            var d = PackFiles.Load(pack);
            d.Tuning.LeadPatience = int.MaxValue;   // the test answers for the player
            edit?.Invoke(d);
            var sim = CampaignSamples.Started(7, d);
            Quiet(sim);
            Assert.IsFalse(sim.State.Campaign.MakeOwnLeads);
            Assert.IsTrue(CampaignApi.Leads(sim.State).All(l => !l.DefId.StartsWith("@")), "only the pack's leads");
            return sim;
        }

        [Test]
        public void AuthoredEarth_FirstArrival()
        {
            var sim = StartPack(PackFiles.Authored);
            var s = sim.State;
            var c = s.Campaign;
            var all = new List<SimEvent>();
            Assert.IsTrue(c.Knows("earth.glade_known"), "Odo reads the marks on sight");
            all.AddRange(Follow(sim, "earth_stone"));
            Assert.IsTrue(c.Knows("earth.stair_open"));
            all.AddRange(Follow(sim, "earth_marks"));
            Assert.IsTrue(all.OfType<QuestEventFired>().Any(e => e.EventId == "earth_underground"), "an Arrive event fires on the way through");
            Assert.IsTrue(c.Knows("earth.root_way_known"));
            all.AddRange(Follow(sim, "earth_root"));
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth));
            Assert.IsTrue(c.Knows("earth.in_hand") && c.Knows("tower.earth_known") && c.Knows("earth.site_seen"));
            all.AddRange(Follow(sim, "earth_tower"));
            Assert.AreEqual(PartyMode.WaitingForPlayer, Mode(sim));
            Assert.AreEqual(MarkerRef.Surface("tower_earth", "door"), CampaignApi.Party(s).At);
            all.AddRange(Do(sim, new ConfirmRestore { Crystal = Element.Earth }));
            all.AddRange(Until(sim, () => c.Knows("earth.restored"), 300, "restored"));
            CollectionAssert.AreEqual(new[] { Custody.AtSite, Custody.Carried, Custody.Restored }, Chain(all, Element.Earth));
            Assert.IsTrue(all.OfType<QuestEventFired>().Any(e => e.EventId == "earth_restore"));
            Assert.AreEqual(0, all.OfType<EncounterCommitted>().Count());
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Human, "crystal:earth"));
            Assert.IsNull(Lead(sim, "earth_tower"), "done");
            Assert.IsTrue(CampaignApi.Journal(s).Count > 5);
            Assert.AreEqual(CampaignPhase.Running, c.Phase, "a Partial pack: the other routes are not written yet");
        }

        /// <summary>The expedition reaches the quarry first; the party follows the tally to Dolm.</summary>
        private static WorldSim ToDolm(out List<SimEvent> all)
        {
            var sim = StartPack(PackFiles.Authored);
            var c = sim.State.Campaign;
            all = new List<SimEvent>();
            all.AddRange(Do(sim, new DevSetRivalProgress { Value = 4800 }));
            all.AddRange(Follow(sim, "earth_stone"));
            all.AddRange(Follow(sim, "earth_marks"));
            all.AddRange(Until(sim, () => Held(sim, Element.Earth) == Custody.AtRivalAltar, 2000, "the altar"));
            all.AddRange(Follow(sim, "earth_root"));
            Assert.IsTrue(c.Knows("earth.stolen_seen") && c.Knows("earth.tally_read"), "the stolen twin at the empty pedestal");
            Assert.IsTrue(all.OfType<QuestEventFired>().Any(e => e.EventId == "earth_empty"));
            all.AddRange(Follow(sim, "earth_tally"));
            Assert.IsTrue(c.Knows("earth.vault_known"));
            all.AddRange(Follow(sim, "earth_vault"));
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim));
            Assert.AreEqual("keeper_earth", CampaignApi.Party(sim.State).EncounterId);
            Assert.AreEqual(new MarkerRef("ash_cut", "tally_vault", "threshold"), CampaignApi.Party(sim.State).At);
            return sim;
        }

        private static List<SimEvent> WinAndRestore(WorldSim sim)
        {
            var s = sim.State;
            var c = s.Campaign;
            var all = new List<SimEvent>();
            var committed = Do(sim, new CommitEncounter { EncounterId = "keeper_earth" }).OfType<EncounterCommitted>().Single();
            Assert.AreEqual("keeper_earth", committed.Profile);
            all.AddRange(Do(sim, new ResolveEncounter { AttemptId = committed.AttemptId, Result = DuelResult.Victory }));
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Earth));
            Assert.IsTrue(c.Knows("earth.recovered") && c.Knows("earth.in_hand") && c.Knows("earth.ash_cut_open"));
            Assert.IsTrue(CampaignApi.IsOpen(s, "ash_cut_way"), "the barred way out is open");
            all.AddRange(Follow(sim, "earth_tower"));
            Assert.IsTrue(all.OfType<PartyArrived>().Any(a => a.At == MarkerRef.Surface("ash_cut", "cut")), "out by the Ash cut");
            all.AddRange(Do(sim, new ConfirmRestore { Crystal = Element.Earth }));
            all.AddRange(Until(sim, () => c.Knows("earth.restored"), 300, "restored"));
            return all;
        }

        [Test]
        public void AuthoredEarth_StolenThenRecovered_Win()
        {
            var sim = ToDolm(out var all);
            all.AddRange(WinAndRestore(sim));
            CollectionAssert.AreEqual(new[] { Custody.AtSite, Custody.RivalTransit, Custody.AtRivalAltar, Custody.Carried, Custody.Restored }, Chain(all, Element.Earth));
            Assert.AreEqual(6, CampaignApi.Companions(sim.State).Count(k => k.Alive));
        }

        [Test]
        public void AuthoredEarth_StolenThenRecovered_LossKillsGarrick_ThenWin()
        {
            var sim = ToDolm(out var all);
            var s = sim.State;
            var c = s.Campaign;
            var committed = Do(sim, new CommitEncounter { EncounterId = "keeper_earth" }).OfType<EncounterCommitted>().Single();
            Assert.AreEqual("garrick", committed.Volunteer);
            long p = c.RivalProgress;
            var lost = Do(sim, new ResolveEncounter { AttemptId = committed.AttemptId, Result = DuelResult.Defeat });
            all.AddRange(lost);
            Assert.AreEqual("garrick", lost.OfType<CompanionFell>().Single().CompanionId);
            Assert.AreEqual(900, lost.OfType<RivalAdvanced>().Single().Amount);
            Assert.AreEqual(p + 901, c.RivalProgress);
            Assert.AreEqual(new MarkerRef("glade", "quarry_upper", "safe"), CampaignApi.Party(s).At, "back in the survey gallery");
            Assert.AreEqual(Custody.AtRivalAltar, Held(sim, Element.Earth));

            // Saved and loaded right after the loss: the loss is not applied twice.
            var loaded = SaveLoad(sim);
            Assert.AreEqual("already_resolved", loaded.Submit(new ResolveEncounter { AttemptId = committed.AttemptId, Result = DuelResult.Defeat }).Reason);

            all.AddRange(Follow(loaded, "earth_vault"));
            Assert.AreEqual("ivo", all.OfType<EncounterThreshold>().Last().Volunteer);
            all.AddRange(WinAndRestore(loaded));
            var rec = CampaignApi.Record(loaded.State);
            Assert.AreEqual("garrick", rec.Fallen.Single().Id);
            Assert.AreEqual("the Tally Vault", rec.Fallen.Single().Place);
            Assert.AreEqual(5, rec.Survivors.Count);
        }

        /// <summary>Check 1.7: with the player away Mira takes the Deepstone if it is there, and at the Tally Vault waits, withdraws, holds.</summary>
        [Test]
        public void AuthoredEarth_Away_TakesItAlone_OrWaitsAtDolm_NeverFightsOrRestores()
        {
            var first = StartPack(PackFiles.Authored);
            Do(first, new SetCompany { Present = false });
            var ev = SimTestKit.Step(first, 4000);
            Assert.AreEqual(Custody.Carried, Held(first, Element.Earth), "she takes it by herself");
            Assert.AreEqual(PartyMode.WaitingForPlayer, Mode(first), "and waits at the tower");
            Assert.AreEqual("tower_earth", CampaignApi.Party(first.State).At.Location);
            Assert.AreEqual(0, ev.OfType<CrystalRestored>().Count());
            StringAssert.Contains("waiting for you", CampaignApi.WhereIsMira(first.State));

            var stolen = StartPack(PackFiles.Authored);
            Do(stolen, new SetCompany { Present = false });
            Do(stolen, new DevRivalNextStep());
            ev = SimTestKit.Step(stolen, 8000);
            var th = ev.OfType<EncounterThreshold>().First();
            var back = ev.OfType<PartyRetreated>().First(r => r.Why == "threshold_wait");
            Assert.AreEqual(900, back.Tick - th.Tick, "90 seconds, then she withdraws");
            Assert.AreEqual(new MarkerRef("glade", "quarry_upper", "safe"), back.To);
            Assert.AreEqual(0, ev.OfType<EncounterCommitted>().Count());
            Assert.AreEqual(6, CampaignApi.Companions(stolen.State).Count(k => k.Alive));
            Assert.AreEqual(PartyMode.AtThreshold, Mode(stolen), "nothing else is open: she holds at the vault");
            Assert.IsTrue(stolen.State.Campaign.Party.HoldAtThreshold);
            Do(stolen, new SetCompany { Present = true });
            Until(stolen, () => !stolen.State.Campaign.Scout.Out, 3000, "Tansy back");
            Assert.IsTrue(stolen.Check(new CommitEncounter { EncounterId = "keeper_earth" }).Ok, "I'm with you: now they can fight");
        }

        private static string Id(Element e) => e.ToString().ToLowerInvariant();

        [Test]
        public void Skeleton_AllTwentyFourOrders_ReachTheFinale_ThenTheRestoredEnding()
        {
            // Collect all four by the pack's own routes once (the Skyshard needs the wind riddle), then save.
            var sim = StartPack(PackFiles.Skeleton);
            var c = sim.State.Campaign;
            Follow(sim, "earth_site");
            Assert.IsTrue(c.Knows("fire.known"), "Petra knows the way (the companion shortcut)");
            Follow(sim, "fire_site");
            Follow(sim, "water_site");
            Follow(sim, "air_cairn");
            Assert.AreEqual(PartyMode.AskingChoice, Mode(sim));
            Do(sim, new AdviseMira { ChoiceId = "air_wind", Answer = 1 });
            Follow(sim, "air_site");
            foreach (Element e in System.Enum.GetValues(typeof(Element))) Assert.AreEqual(Custody.Carried, Held(sim, e), e.ToString());
            string carried = SaveCodec.ToJson(sim.State);

            int n = 0;
            foreach (var order in Orders())
            {
                n++;
                var run = SaveCodec.Load(carried, out _);
                var s = run.State;
                var all = new List<SimEvent>();
                for (int i = 0; i < 3; i++)
                {
                    all.AddRange(Follow(run, Id(order[i]) + "_tower"));
                    Assert.AreEqual(PartyMode.WaitingForPlayer, Mode(run));
                    all.AddRange(Do(run, new ConfirmRestore { Crystal = order[i] }));
                    all.AddRange(Until(run, () => Held(run, order[i]) == Custody.Restored, 300, "restored"));
                    Assert.IsTrue(s.Campaign.Knows(Id(order[i]) + ".restored"));
                }
                all.AddRange(Follow(run, Id(order[3]) + "_tower"));
                string label = string.Join(",", order);
                Assert.AreEqual(PartyMode.AtThreshold, Mode(run), label);
                Assert.AreEqual("finale", CampaignApi.Party(s).EncounterId, label);
                Assert.AreEqual("tower_" + Id(order[3]), CampaignApi.Party(s).At.Location, label);
                Assert.AreEqual("not_at_threshold", run.Submit(new ConfirmRestore { Crystal = order[3] }).Reason);
                var committed = Do(run, new CommitEncounter { EncounterId = "finale" }).OfType<EncounterCommitted>().Single();
                Assert.AreEqual("finale_unweaver", committed.Profile);
                var ended = Do(run, new ResolveEncounter { AttemptId = committed.AttemptId, Result = DuelResult.Victory }).OfType<CampaignEnded>().Single();
                Assert.IsTrue(ended.Won, label);
                Assert.AreEqual("restored", ended.EndingId);
                CollectionAssert.AreEqual(order, ended.Record.RestoredOrder, label);
                Assert.AreEqual(4, LedgerApi.Points(s, PlayerIds.Human));
            }
            Assert.AreEqual(24, n);
        }

        private static IEnumerable<Element[]> Orders()
        {
            var all = new[] { Element.Earth, Element.Fire, Element.Water, Element.Air };
            foreach (var a in all)
                foreach (var b in all.Where(x => x != a))
                    foreach (var c in all.Where(x => x != a && x != b))
                        foreach (var d in all.Where(x => x != a && x != b && x != c))
                            yield return new[] { a, b, c, d };
        }
    }
}
