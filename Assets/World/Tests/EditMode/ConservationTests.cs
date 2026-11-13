using System.Linq;
using NUnit.Framework;
using World.Sim.Factions;
using World.Sim.Save;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Whole games: the invariants of 4.4 every 100 ticks, no exceptions, determinism, a game always ends.</summary>
    public class ConservationTests
    {
        /// <summary>A prompt-ish scripted player: casts its top proposal every 30 s.</summary>
        private static WorldSim Play(ulong seed, int ticks, bool checkInvariants)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            for (int t = 1; t <= ticks; t++)
            {
                if (t % 300 == 0 && !s.Factions.GameOver)
                {
                    var top = FactionApi.Top(s, PlayerIds.Human);
                    if (top != null) sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = top.Id });
                }
                sim.Step();
                sim.DrainEvents();
                if (checkInvariants && t % 100 == 0) Assert.IsNull(K.CheckInvariants(s), "seed " + seed + " tick " + t);
            }
            return sim;
        }

        [Test]
        public void TwentyMinutes_ThreeSeeds_InvariantsHold_GameEnds()
        {
            foreach (ulong seed in new ulong[] { 1, 7, K.DefaultSeed })
            {
                var sim = Play(seed, 12000, true);
                var s = sim.State;
                Assert.IsTrue(s.Factions.GameOver, "seed " + seed + ": the game ends by 20 minutes");
                Assert.Greater(s.Factions.Casts[PlayerIds.Human], 0);
                Assert.Greater(s.Factions.Casts[PlayerIds.Rival], 0);
                Assert.AreEqual(120, s.Economy.Dice.RollCount);
            }
        }

        [Test]
        public void SameSeed_IdenticalStateJson_AtTick12000()
        {
            var a = Play(K.DefaultSeed, 12000, false);
            var b = Play(K.DefaultSeed, 12000, false);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
        }
    }
}
