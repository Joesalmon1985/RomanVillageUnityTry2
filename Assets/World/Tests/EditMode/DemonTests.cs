using System.Linq;
using NUnit.Framework;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Demons (REFACTOR_SPEC 2.C.4): the draw on a 7 after the grace period, the cap and outbreaks, banishing.</summary>
    public class DemonTests
    {
        [Test]
        public void SevenBeforeGrace_Nothing_AfterGrace_OneDemonOnASite()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            K.RigNextRoll(s, 3, 4);
            Assert.IsEmpty(K.Step(sim, 1).OfType<DemonPlaced>());

            s.Tick = 1799;
            K.RigNextRoll(s, 2, 5);
            var placed = K.Step(sim, 1).OfType<DemonPlaced>().Single();
            Assert.AreEqual(DemonSiteKind.Corner, placed.SiteKind, "only the two start camps are sites");
            CollectionAssert.Contains(s.Board.StartCorners, placed.SiteId);
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(1, DemonApi.DemonsAt(s, DemonSiteKind.Corner, placed.SiteId));
            var site = DemonApi.DemonSites(s).Single();
            Assert.AreEqual(placed.SiteId, site.Id);
            Assert.AreEqual(s.Economy.Settlements.Single(x => x.Corner == placed.SiteId).Faction, site.Faction);
            Assert.AreEqual(1, s.Demons.Placed);
        }

        [Test]
        public void FourthDemon_Outbreaks_ToBuiltRoads()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int home = s.Board.StartCorners[0];
            int edge = s.Board.Topology.CornerHexEdges(home).Min();
            K.Do(sim, new PlaceRoad { HexEdge = edge });
            K.Step(sim, 200);
            Assert.AreEqual(RoadState.Built, EconomyApi.RoadOn(s, edge).State);
            s.Demons.Corner[home] = 3;
            s.Demons.Corner[s.Board.StartCorners[1]] = 3;
            s.Demons.Edge[edge] = 3;
            s.Tick = 1900;
            K.RigNextRoll(s, 1, 6);
            var ev = K.Step(sim, 1);
            var obs = ev.OfType<DemonOutbreak>().ToList();
            Assert.IsNotEmpty(obs, "every site is at the cap");
            Assert.AreEqual(obs.Count, obs.Select(o => (o.SiteKind, o.SiteId)).Distinct().Count(), "each site outbreaks at most once per draw");
            Assert.AreEqual(obs.Count, s.Demons.Outbreaks);
            Assert.AreEqual(0, s.Demons.Placed);
            var first = obs[0];
            if (first.SiteKind == DemonSiteKind.Corner && first.SiteId == home)
            {
                CollectionAssert.AreEqual(new[] { edge }, first.SpreadIds, "a corner spreads to its built roads");
                Assert.AreEqual(2, obs.Count, "the road outbreaks in turn");
            }
            if (first.SiteKind == DemonSiteKind.HexEdge)
                CollectionAssert.AreEqual(new[] { home }, first.SpreadIds, "a road spreads to its camp ends");
            Assert.AreEqual(3, s.Demons.Corner[home]);
            Assert.AreEqual(3, s.Demons.Edge[edge]);
        }

        [Test]
        public void Banish_RemovesEveryDemon_OwnSitesOnly_AndTheBrainProposesIt()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int home = s.Board.StartCorners[0];
            s.Demons.Corner[home] = 2;
            var r = sim.Check(new Banish { PlayerId = PlayerIds.Rival, SiteKind = DemonSiteKind.Corner, SiteId = home });
            Assert.AreEqual(DemonRefusals.NotOwner, r.Reason);
            r = sim.Check(new Banish { PlayerId = PlayerIds.Human, SiteKind = DemonSiteKind.Corner, SiteId = 5 });
            Assert.AreEqual("There are no demons there.", r.Message);

            K.Step(sim, 20);
            var top = FactionApi.Top(s, PlayerIds.Human);
            Assert.AreEqual(ProposalKind.Banish, top.Kind);
            Assert.AreEqual(120, top.Score);
            Assert.AreEqual("2 demons sit on the home camp: its collectors stop and it trades with no one.", top.Reason);
            Assert.AreEqual(Speakers.Prince, top.Speaker);
            var ev = K.Do(sim, new CastSpell { ProposalId = top.Id });
            var b = ev.OfType<DemonsBanished>().Single();
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(0, DemonApi.DemonsAt(s, DemonSiteKind.Corner, home));
            Assert.AreEqual(2, s.Demons.Banished);
        }

        [Test]
        public void SiteNames()
        {
            var s = WorldSimFactory.CreateNew(K.DefaultSeed).State;
            Assert.AreEqual("the home camp", DemonApi.SiteName(s, DemonSiteKind.Corner, s.Board.StartCorners[0]));
            Assert.AreEqual("the Ash Clan's home camp", DemonApi.SiteName(s, DemonSiteKind.Corner, s.Board.StartCorners[1]));
            StringAssert.StartsWith("the road ", DemonApi.SiteName(s, DemonSiteKind.HexEdge, 100));
        }
    }
}
