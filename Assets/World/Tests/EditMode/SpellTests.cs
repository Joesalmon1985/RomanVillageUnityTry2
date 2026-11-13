using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Proposals and spells (REFACTOR_SPEC 2.C.2, 6.1, 6.4): the opening, CastSpell, DeclineProposal, the rival's auto-cast.</summary>
    public class SpellTests
    {
        [Test]
        public void Opening_IsTheTopProposalAtTickZero()
        {
            var s = WorldSimFactory.CreateNew(K.DefaultSeed).State;
            var top = FactionApi.Top(s, PlayerIds.Human);
            Assert.IsNotNull(top);
            Assert.AreEqual(ProposalKind.Building, top.Kind);
            Assert.AreEqual((int)BuildingType.WoodcutterHut, top.BuildingType);
            Assert.AreEqual(631, top.Plot);
            Assert.AreEqual(0, top.Settlement);
            Assert.AreEqual(Speakers.Prince, top.Speaker);
            Assert.AreEqual("A woodcutter's hut brings in twice the wood of the log pile, and wood builds everything.", top.Reason);
            Assert.AreEqual(0, top.Rank);
            Assert.AreEqual("0|5|631|-1|-1|0|-1", top.Key);
            Assert.AreEqual("Woodcutter's hut", FactionApi.ProposalTitle(s, top.Id));
            Assert.IsTrue(FactionApi.ProposalTarget(s, top.Id, out double x, out double z, out double r));
            Assert.AreEqual(PlotLayout.PlotX(s.Board.Topology, 631), x, 1e-9);
            Assert.AreEqual(PlotLayout.PlotZ(s.Board.Topology, 631), z, 1e-9);
            Assert.AreEqual(5, r);
            Assert.AreEqual(top.Id, FactionApi.ProposalAt(s, PlayerIds.Human, x + 3, z));
            Assert.AreEqual(-1, FactionApi.ProposalAt(s, PlayerIds.Human, x + 6, z));
            Assert.AreEqual(15, FactionApi.CastRangeMetres(s));
            Assert.LessOrEqual(FactionApi.Proposals(s, PlayerIds.Human).Count, 3);
        }

        [Test]
        public void Cast_StartsTheSite_RemovesTheProposal_HutCompletes_LogPileRetires()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var top = FactionApi.Top(s, PlayerIds.Human);
            var ev = K.Do(sim, new CastSpell { ProposalId = top.Id });
            var placed = ev.OfType<SitePlaced>().Single(p => p.Faction == PlayerIds.Human);
            Assert.AreEqual(SiteKind.Building, placed.Kind);
            var cast = ev.OfType<SpellCast>().Single();
            Assert.AreEqual(631, cast.Plot);
            Assert.AreEqual(top.Id, cast.ProposalId);
            Assert.AreEqual("cast", ev.OfType<ProposalRemoved>().Single(r => r.ProposalId == top.Id).Why);
            Assert.IsNull(FactionApi.GetProposal(s, top.Id));
            Assert.IsTrue(s.Factions.OpeningDone[PlayerIds.Human]);
            Assert.AreEqual(1, s.Factions.Casts[PlayerIds.Human]);
            Assert.AreEqual(PlotUse.Site, EconomyApi.Plot(s, 631).Use);

            Assert.AreEqual(FactionRefusals.UnknownProposal, sim.Submit(new CastSpell { PlayerId = 0, ProposalId = top.Id }).Reason);

            ev = K.Step(sim, 300);
            var hut = s.Economy.Buildings[placed.Id];
            Assert.AreEqual(BuildingState.Active, hut.State, "S-0 interim logistics: goods come straight from the storehouse");
            Assert.IsTrue(ev.OfType<SiteCompleted>().Any(c => c.Kind == SiteKind.Building && c.Id == hut.Id));
            var logPile = s.Economy.Buildings.Single(b => b.Plot == 630);
            Assert.AreEqual(BuildingState.Retired, logPile.State);
            Assert.IsTrue(ev.OfType<BuildingRetired>().Any(r => r.BuildingId == logPile.Id));
            Assert.AreEqual(PlotUse.Free, EconomyApi.Plot(s, 630).Use);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Decline_RemovesAndBlocksTheSameKey()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var top = FactionApi.Top(s, PlayerIds.Human);
            string key = top.Key;
            var ev = K.Do(sim, new DeclineProposal { ProposalId = top.Id });
            Assert.AreEqual("declined", ev.OfType<ProposalRemoved>().Single(r => r.ProposalId == top.Id).Why);
            K.Step(sim, 590);
            Assert.IsFalse(FactionApi.Proposals(s, PlayerIds.Human).Any(p => p.Key == key), "not proposed again within the cooldown");
            K.Step(sim, 40);
            Assert.IsTrue(FactionApi.Proposals(s, PlayerIds.Human).Any(p => p.Key == key), "back after the cooldown");
        }

        [Test]
        public void Commands_RefusedForAnotherPeoplesProposal()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            sim.Step(10);
            var rivalTop = FactionApi.Top(sim.State, PlayerIds.Rival);
            Assert.IsNotNull(rivalTop, "the rival plans at tick 10");
            var r = sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = rivalTop.Id });
            Assert.AreEqual(FactionRefusals.UnknownProposal, r.Reason);
            Assert.AreEqual("That proposal is gone.", r.Message);
        }

        [Test]
        public void NoLongerLegal_CarriesTheUnderlyingSentence()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var top = FactionApi.Top(sim.State, PlayerIds.Human);
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Plot = 631 });
            var r = sim.Check(new CastSpell { PlayerId = 0, ProposalId = top.Id });
            Assert.AreEqual(FactionRefusals.NoLongerLegal, r.Reason);
            Assert.AreEqual("That can no longer be done: Something already stands on that plot.", r.Message);
        }

        [Test]
        public void Rival_CastsItsTopProposal_45SecondsAfterItBecameTop()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var ev = K.Step(sim, 459);
            Assert.IsFalse(ev.OfType<SpellCast>().Any());
            ev = K.Step(sim, 1);
            var cast = ev.OfType<SpellCast>().Single();
            Assert.AreEqual(PlayerIds.Rival, cast.Faction);
            Assert.AreEqual(460, cast.Tick);
            Assert.AreEqual((int)BuildingType.WoodcutterHut, cast.BuildingType);
            Assert.IsTrue(sim.State.Factions.OpeningDone[PlayerIds.Rival]);
        }
    }
}
