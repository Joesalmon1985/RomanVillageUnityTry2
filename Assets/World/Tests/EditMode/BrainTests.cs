using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>The prince's brain (REFACTOR_SPEC 6): queue mechanics, expansion, speakers, the rival on the same rules.</summary>
    public class BrainTests
    {
        private static void CheckQueue(WorldState s)
        {
            var f = s.Factions;
            for (int fa = 0; fa < PlayerIds.Count; fa++)
            {
                var q = f.Proposals.Where(p => p.Faction == fa).ToList();
                Assert.LessOrEqual(q.Count, f.Tuning.QueueLength);
                for (int i = 0; i < q.Count; i++)
                {
                    Assert.AreEqual(i, q[i].Rank);
                    if (i > 0)
                        Assert.IsTrue(q[i - 1].Score > q[i].Score || (q[i - 1].Score == q[i].Score && string.CompareOrdinal(q[i - 1].Key, q[i].Key) < 0),
                            "sorted by score then key");
                    Assert.IsFalse(string.IsNullOrEmpty(q[i].Reason));
                    StringAssert.EndsWith(".", q[i].Reason);
                }
                Assert.AreEqual(q.Count, q.Select(p => p.Key).Distinct().Count());
            }
            for (int i = 1; i < f.Proposals.Count; i++) Assert.LessOrEqual(f.Proposals[i - 1].Faction, f.Proposals[i].Faction);
        }

        [Test]
        public void PromptGame_Expands_WithTheRightSpeakers_AndAValidQueue()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var player = new ScriptedPlayer(sim, PlayerStyle.Prompt);
            var seen = new Dictionary<string, HashSet<string>>(); // speaker -> titles
            var kinds = new HashSet<ProposalKind>();
            for (int t = 0; t < 7200 && !s.Factions.GameOver; t++)
            {
                player.Act();
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    if (e is ProposalAdded pa && pa.Faction == 0)
                    {
                        var p = FactionApi.GetProposal(s, pa.ProposalId);
                        if (!seen.TryGetValue(p.Speaker, out var set)) seen[p.Speaker] = set = new HashSet<string>();
                        set.Add(FactionApi.ProposalTitle(s, p.Id));
                        if (p.Kind == ProposalKind.Road)
                        {
                            Assert.AreEqual(Speakers.Merchant, p.Speaker);
                            StringAssert.StartsWith("A road towards the ", p.Reason);
                            StringAssert.StartsWith("Road to the ", FactionApi.ProposalTitle(s, p.Id));
                        }
                        if (p.Kind == ProposalKind.Settlement) StringAssert.StartsWith("A camp at the ", p.Reason);
                        if (p.Kind == ProposalKind.Building && p.BuildingType == (int)BuildingType.Sawmill) Assert.AreEqual(Speakers.Sawyer, p.Speaker);
                    }
                    if (e is SpellCast sc && sc.Faction == 0) kinds.Add(sc.Kind);
                }
                if (t % 100 == 0) CheckQueue(s);
            }
            CollectionAssert.IsSupersetOf(seen.Keys, new[] { Speakers.Prince, Speakers.Merchant });
            Assert.Contains(ProposalKind.Road, kinds.ToList());
            Assert.Contains(ProposalKind.Settlement, kinds.ToList());
            Assert.Greater(s.Economy.Settlements.Count(x => x.Faction == 0 && x.State == SettlementState.Active), 1, "a second camp within 12 minutes");
        }

        [Test]
        public void Rival_RunsTheSameBrain_AndExpandsAlone()
        {
            var r = GameRunner.Play(K.DefaultSeed, PlayerStyle.Ignore, maxTicks: 9000);
            Assert.GreaterOrEqual(r.RivalSecondCamp, 0, "the rival founds a second camp by itself");
            Assert.Greater(r.RivalCasts, 5);
            Assert.LessOrEqual(r.RivalLongestWithoutProposal, 1800, "the rival never goes 3 minutes without a proposal");
        }

        [Test]
        public void NoPlanning_WhileThreeSitesAreOpen_ExceptBanish()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            K.Give(s, 0, Ware.Wood, 20);
            K.Give(s, 0, Ware.Clay, 20);
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Plot = 634 });
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Kiln, Plot = 637 });
            var t = s.Board.Topology;
            K.Do(sim, new PlaceRoad { HexEdge = t.CornerHexEdges(s.Board.StartCorners[0]).Min() });
            K.Step(sim, 20);
            Assert.IsEmpty(FactionApi.Proposals(s, 0).Where(p => p.Kind != ProposalKind.Banish), "three open sites: no planning");
            s.Demons.Corner[s.Board.StartCorners[0]] = 1;
            K.Step(sim, 20);
            Assert.AreEqual(ProposalKind.Banish, FactionApi.Top(s, 0).Kind);
        }

        [Test]
        public void DefenderUnderWarning_RanksStrengthFirst()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Factions.Tuning.OverrunMargin = 0; // the player warns the rival at once: the rival defends
            K.Step(sim, 30);
            Assert.IsTrue(s.Factions.Warning.Active);
            Assert.AreEqual(PlayerIds.Rival, s.Factions.Warning.Defender);
            K.Give(s, 1, Ware.Planks, 10);
            K.Give(s, 1, Ware.Bricks, 10);
            K.Give(s, 1, Ware.CutStone, 10);
            K.Give(s, 1, Ware.Bread, 10);
            K.Step(sim, 20);
            var top = FactionApi.Top(s, 1);
            Assert.AreEqual(ProposalKind.City, top.Kind);
            StringAssert.EndsWith("gathering: this keeps us standing.", top.Reason);
        }
    }
}
