using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>The handover milestone (PRD_AGES 4.2; package H1-1).</summary>
    public class HandoverTests
    {
        private static WorldSim Quiet(ulong seed = K.DefaultSeed)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            sim.State.Demons.Tuning.GraceTicks = int.MaxValue;
            return sim;
        }

        internal static int FreePlot(WorldState s, int faction, int settlement, BuildingType type) =>
            EconomyApi.PlotsOf(s, settlement).First(p => EconomyApi.CheckBuilding(s, faction, type, p).Ok);

        /// <summary>Gives the cost and places the building directly (the F1 tool), returning that run's events up to its completion.</summary>
        private static System.Collections.Generic.List<SimEvent> Build(WorldSim sim, int faction, int settlement, BuildingType type)
        {
            var s = sim.State;
            var def = BuildingDefs.Get(type);
            for (int w = 0; w < Wares.Count; w++) if (def.Cost[w] > 0) K.Give(s, settlement, (Ware)w, def.Cost[w]);
            int id = s.Economy.Buildings.Count;
            var all = K.Do(sim, new PlaceBuilding { Type = type, Plot = FreePlot(s, faction, settlement, type) }, faction);
            for (int i = 0; i < 3000 && s.Economy.Buildings[id].State != BuildingState.Active; i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
            }
            Assert.AreEqual(BuildingState.Active, s.Economy.Buildings[id].State, type + " built");
            return all;
        }

        [Test]
        public void FirstWorkedWareAtASite_HandsOver_Once_AndInvitesTheHistoricStep()
        {
            var sim = Quiet();
            var s = sim.State;
            Assert.IsTrue(BuildingDefs.Get(BuildingType.Bakery).Cost.Where((n, w) => n > 0 && w >= (int)Ware.Planks).Any(), "a bakery takes worked goods");
            var ev = Build(sim, 0, 0, BuildingType.Bakery);
            var h = ev.OfType<Eras.PrinceHandover>().Single();
            Assert.AreEqual(0, h.Faction);
            var fa = s.Factions.Factions[0];
            Assert.AreEqual(h.Tick, fa.HandoverTick);
            Assert.AreEqual(h.Tick, AgesApi.HandoverTick(s, 0));
            Assert.IsTrue(fa.Auto, "the prince runs the camp now");
            Assert.IsTrue(FactionApi.IsAutonomous(s, 0));
            var ready = ev.OfType<ChapterReady>().Single();
            Assert.AreEqual((ChapterIds.Historic, h.Tick), (ready.Chapter, ready.Tick));
            Assert.IsTrue(AgesApi.IsReady(s, ChapterIds.Historic));
            Assert.AreEqual(Era.Prehistoric, AgesApi.Current(s), "nothing steps until the player says so");
            Assert.IsFalse(AgesApi.CampaignMayStart(s));

            ev = Build(sim, 0, 0, BuildingType.Smokehouse);
            Assert.IsEmpty(ev.OfType<Eras.PrinceHandover>(), "once per faction");
            Assert.IsEmpty(ev.OfType<ChapterReady>());
            Assert.AreEqual(h.Tick, fa.HandoverTick);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void RawWaresAtASite_DoNotHandOver()
        {
            var sim = Quiet();
            var s = sim.State;
            var ev = Build(sim, 0, 0, BuildingType.WoodcutterHut);
            Assert.IsEmpty(ev.OfType<Eras.PrinceHandover>());
            Assert.AreEqual(-1, AgesApi.HandoverTick(s, 0));
            Assert.IsFalse(s.Factions.Factions[0].Auto);
            Assert.IsEmpty(AgesApi.ReadyChapters(s));
        }

        [Test]
        public void TheRivalsHandover_IsARecordOnly()
        {
            var sim = Quiet();
            var s = sim.State;
            int rivalHome = EconomyApi.SettlementAt(s, s.Board.StartCorners[1]).Id;
            var ev = Build(sim, 1, rivalHome, BuildingType.Bakery);
            Assert.AreEqual(1, ev.OfType<Eras.PrinceHandover>().Single().Faction);
            Assert.GreaterOrEqual(AgesApi.HandoverTick(s, 1), 0);
            Assert.IsEmpty(ev.OfType<ChapterReady>(), "only the player's people open the Historic step");
            Assert.AreEqual(-1, AgesApi.HandoverTick(s, 0));
            Assert.IsFalse(s.Factions.Factions[0].Auto);
            Assert.IsTrue(s.Factions.Factions[1].Auto, "the rival casts on its own as before");
            Assert.AreEqual(-1, s.Factions.Factions[1].AnnouncedProposalId, "and keeps its paced cast, without announcements");
        }

        [Test]
        public void ACityUpgradeTakingWorkedGoods_HandsOver()
        {
            var sim = Quiet();
            var s = sim.State;
            var tu = s.Economy.Tuning;
            for (int w = 0; w < Wares.Count; w++) if (tu.CityCost[w] > 0) K.Give(s, 0, (Ware)w, tu.CityCost[w]);
            var ev = K.Do(sim, new UpgradeCity { Corner = s.Board.StartCorners[0] });
            ev.AddRange(K.Step(sim, 5));
            Assert.AreEqual(0, ev.OfType<Eras.PrinceHandover>().Single().Faction, "the city takes planks from its own storehouse");
            Assert.IsTrue(s.Factions.Factions[0].Auto);
        }

        [Test]
        public void ScriptedGames_NoSiteBeforeTheHandoverWithoutACast_AndIgnoredNeverHandsOver()
        {
            foreach (var style in new[] { PlayerStyle.Prompt, PlayerStyle.Ignore })
            {
                var r = HandoverProof.Play(new HandoverProof.Spec { Seed = 3, Style = style });
                Assert.AreEqual(r.CastsBefore, r.SitesBefore, style + ": every site before the handover came from a cast");
                Assert.IsNull(r.InvariantFailure);
                if (style == PlayerStyle.Ignore) Assert.AreEqual(-1, r.Handover, "an ignored prince never hands over");
                else
                {
                    Assert.That(r.Handover, Is.InRange(3000, 5400), "a prompt player hands over at 5-9 minutes");
                    Assert.AreEqual(r.Handover, r.ChapterReady);
                }
            }
        }
    }
}
