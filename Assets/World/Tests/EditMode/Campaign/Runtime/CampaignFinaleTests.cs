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
    /// PRD_CRYSTALS 5.7 and 5.8: restoration and the island's change, the finale at the tower of the fourth crystal for
    /// every one of the 24 restoration orders, a survivable loss there, and the restored ending with its record.
    /// </summary>
    public class CampaignFinaleTests
    {
        private static IEnumerable<Element[]> Orders()
        {
            var all = new[] { Element.Earth, Element.Fire, Element.Water, Element.Air };
            foreach (var a in all)
                foreach (var b in all.Where(x => x != a))
                    foreach (var c in all.Where(x => x != a && x != b))
                        foreach (var d in all.Where(x => x != a && x != b && x != c))
                            yield return new[] { a, b, c, d };
        }

        private static string Id(Element e) => e.ToString().ToLowerInvariant();

        /// <summary>All four carried; the first three set in this order; the party walks to the fourth's tower.</summary>
        private static WorldSim ToTheLastTower(Element[] order, out List<SimEvent> all)
        {
            var sim = CampaignSamples.Started(7, Patient());
            Quiet(sim);
            all = new List<SimEvent>();
            foreach (Element e in System.Enum.GetValues(typeof(Element))) Do(sim, new DevSetCustody { Crystal = e, To = Custody.Carried });
            for (int i = 0; i < 3; i++)
            {
                all.AddRange(Follow(sim, "@restore:" + Id(order[i])));
                Assert.AreEqual(PartyMode.WaitingForPlayer, Mode(sim), order[i] + " at its tower");
                all.AddRange(Do(sim, new ConfirmRestore { Crystal = order[i] }));
                all.AddRange(Until(sim, () => Held(sim, order[i]) == Custody.Restored, 300, "restored"));
            }
            all.AddRange(Follow(sim, "@restore:" + Id(order[3])));
            return sim;
        }

        [Test]
        public void AllTwentyFourOrders_ReachTheFinale_AtTheFourthTower_ThenTheRestoredEnding()
        {
            int n = 0;
            foreach (var order in Orders())
            {
                n++;
                var sim = ToTheLastTower(order, out var all);
                var s = sim.State;
                var c = s.Campaign;
                string tower = "tower_" + Id(order[3]);
                Assert.AreEqual(PartyMode.AtThreshold, Mode(sim), string.Join(",", order));
                Assert.AreEqual("finale", CampaignApi.Party(s).EncounterId);
                Assert.AreEqual(tower, CampaignApi.Party(s).At.Location, "the finale is bound to the fourth crystal's tower");
                Assert.AreEqual("finale", all.OfType<EncounterThreshold>().Last().EncounterId);
                Assert.AreEqual("not_at_threshold", sim.Submit(new ConfirmRestore { Crystal = order[3] }).Reason, "the fourth is set by winning");

                var committed = Do(sim, new CommitEncounter { EncounterId = "finale" }).OfType<EncounterCommitted>().Single();
                Assert.AreEqual("finale_unweaver", committed.Profile);
                var ev = Do(sim, new ResolveEncounter { AttemptId = committed.AttemptId, Result = DuelResult.Victory });
                var ended = ev.OfType<CampaignEnded>().Single();
                Assert.IsTrue(ended.Won);
                Assert.AreEqual("restored", ended.EndingId);
                CollectionAssert.AreEqual(order, c.RestoredOrder, "restoration order");
                CollectionAssert.AreEqual(order, ended.Record.RestoredOrder);
                Assert.AreEqual(6, ended.Record.Survivors.Count);
                Assert.AreEqual(4, ev.Concat(all).OfType<CrystalRestored>().Count());
                Assert.AreEqual(4, LedgerApi.Points(s, PlayerIds.Human));
                Assert.AreEqual(CampaignPhase.Won, c.Phase);
                Assert.AreEqual(int.MaxValue, s.Demons.Tuning.SevensPerDraw, "four restored: no demon is drawn again");
            }
            Assert.AreEqual(24, n);
        }

        [Test]
        public void Finale_SurvivableLoss_RetreatsToItsTower_AndStaysOpen()
        {
            var sim = ToTheLastTower(new[] { Element.Water, Element.Air, Element.Earth, Element.Fire }, out _);
            var s = sim.State;
            var c = s.Campaign;
            int id = Do(sim, new CommitEncounter { EncounterId = "finale" }).OfType<EncounterCommitted>().Single().AttemptId;
            Assert.AreEqual("garrick", CampaignApi.Attempt(s).Volunteer);
            var ev = Do(sim, new ResolveEncounter { AttemptId = id, Result = DuelResult.Defeat });
            Assert.AreEqual("garrick", ev.OfType<CompanionFell>().Single().CompanionId);
            Assert.AreEqual(MarkerRef.Surface("tower_fire", "safe"), CampaignApi.Party(s).At, "the tower's safe point");
            Assert.AreEqual(CampaignPhase.Running, c.Phase);
            Assert.AreEqual(Custody.Carried, Held(sim, Element.Fire), "a carried crystal is never lost");
            Assert.AreEqual("the Beacon Tower", CampaignApi.Record(s).Fallen.Single().Place, "where he fell: the last tower");

            Follow(sim, "@restore:fire");
            Assert.AreEqual("finale", CampaignApi.Party(s).EncounterId, "the finale stays open");
            id = Do(sim, new CommitEncounter { EncounterId = "finale" }).OfType<EncounterCommitted>().Single().AttemptId;
            ev = Do(sim, new ResolveEncounter { AttemptId = id, Result = DuelResult.Victory });
            Assert.IsTrue(ev.OfType<CampaignEnded>().Single().Won);
            var rec = CampaignApi.Record(s);
            CollectionAssert.AreEqual(new[] { Element.Water, Element.Air, Element.Earth, Element.Fire }, rec.RestoredOrder);
            Assert.AreEqual("finale", rec.Fallen.Single().EncounterId);
            Assert.AreEqual(5, rec.Survivors.Count);
            Assert.AreEqual("campaign_over", sim.Submit(new SetCompany { Present = true }).Reason);
        }

        [Test]
        public void Restoration_DemonsAndBoons_AlwaysFromTheSavedBase()
        {
            var sim = CampaignSamples.Started(7, Patient());
            var s = sim.State;
            var c = s.Campaign;
            var econ = s.Economy.Tuning;
            int b = c.BaseSevensPerDraw;
            int[] grow = (int[])c.BaseGrowTicks.Clone();
            int carrier = c.BaseCarrierTicksPerStep;
            Assert.AreEqual(b, s.Demons.Tuning.SevensPerDraw);

            Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.AtRivalAltar });
            Assert.AreEqual(System.Math.Max(1, b - 1), s.Demons.Tuning.SevensPerDraw, "a crystal on an altar: demons more often");
            Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Restored });
            Assert.AreEqual(System.Math.Max(1, b + 1 - 1), s.Demons.Tuning.SevensPerDraw);
            Assert.AreEqual(grow[(int)Terrain.Hills] * 100 / 120, econ.GrowTicks[(int)Terrain.Hills]);
            Assert.AreEqual(grow[(int)Terrain.Mountain] * 100 / 120, econ.GrowTicks[(int)Terrain.Mountain]);
            Assert.AreEqual(grow[(int)Terrain.Fields], econ.GrowTicks[(int)Terrain.Fields]);
            Do(sim, new DevSetCustody { Crystal = Element.Water, To = Custody.Restored });
            Assert.AreEqual(grow[(int)Terrain.Fields] * 100 / 120, econ.GrowTicks[(int)Terrain.Fields]);
            Assert.AreEqual(grow[(int)Terrain.Pasture] * 100 / 120, econ.GrowTicks[(int)Terrain.Pasture]);
            Assert.AreEqual(grow[(int)Terrain.Forest], econ.GrowTicks[(int)Terrain.Forest]);
            Do(sim, new DevSetCustody { Crystal = Element.Air, To = Custody.Restored });
            Assert.AreEqual(carrier - 1, econ.CarrierTicksPerStep, "the Skyshard: one tick less per carrier step");

            // Recomputed from the base, never stacked: undo and redo give the same numbers.
            Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Carried });
            Assert.AreEqual(grow[(int)Terrain.Hills], econ.GrowTicks[(int)Terrain.Hills]);
            Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Restored });
            Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Restored });
            Assert.AreEqual(grow[(int)Terrain.Hills] * 100 / 120, econ.GrowTicks[(int)Terrain.Hills]);
            Assert.AreEqual(carrier - 1, econ.CarrierTicksPerStep);

            // All four: no demon is ever drawn again and those on the board are removed.
            int corner = s.Board.StartCorners[PlayerIds.Human];
            s.Demons.Corner[corner] = 2;
            Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Carried });
            var ev = Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Restored });
            Assert.AreEqual(int.MaxValue, s.Demons.Tuning.SevensPerDraw);
            Assert.AreEqual(0, s.Demons.Corner[corner]);
            Assert.AreEqual(2, ev.OfType<Demons.DemonsBanished>().Single().Count);
        }
    }
}
