using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>G1-1 "done when": a seeded AI-only game reaches 10 points deterministically; same seed and commands, same state.</summary>
    public class TableGameTests
    {
        private const int MaxTicks = 200000;

        private static (WorldSim sim, List<SimEvent> log) PlayOut(ulong seed, int people = 4)
        {
            var sim = Create(people: people, humanSeat: -1, seed: seed); // default seams: the basic builder on every seat
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => T(sim).Phase == TablePhase.Ended, MaxTicks, log), "seed " + seed + " never ended");
            return (sim, log);
        }

        [Test]
        public void AiOnlyGame_ReachesTenPoints_Deterministically()
        {
            var (a, logA) = PlayOut(Seed);
            var (b, logB) = PlayOut(Seed);
            var ta = T(a);
            Assert.GreaterOrEqual(TableRules.Points(a.State, ta.Winner), 10);
            var ended = logA.OfType<AgeEnded>().Single();
            Assert.AreEqual(ta.Winner, ended.Winner);
            Assert.AreEqual(a.State.Tick, b.State.Tick);
            Assert.AreEqual(JsonConvert.SerializeObject(a.State.Table), JsonConvert.SerializeObject(b.State.Table));
            Assert.AreEqual(logA.Count, logB.Count);
            TestContext.Out.WriteLine("winner " + ta.Winner + " in round " + ta.Round + " at tick " + a.State.Tick
                                      + "; points " + string.Join(",", Enumerable.Range(0, 4).Select(p => TableRules.Points(a.State, p))));
            // Sanity of the finished board.
            for (int p = 0; p < 4; p++)
            {
                Assert.LessOrEqual(TableRules.CountRoads(ta, p), ta.Tuning.MaxRoads);
                Assert.LessOrEqual(TableRules.CountSettlements(ta, p), ta.Tuning.MaxSettlements);
                Assert.LessOrEqual(TableRules.CountCities(ta, p), ta.Tuning.MaxCities);
                foreach (int n in ta.Peoples[p].Hand) Assert.GreaterOrEqual(n, 0);
            }
            Assert.Greater(logA.OfType<Epidemic>().Count(), 0, "sevens came up");
            Assert.Greater(logA.OfType<Built>().Count(e => e.Kind == "city"), 0);
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        [TestCase(77UL)]
        public void AiOnlyGames_OtherSeeds_End(ulong seed)
        {
            var (sim, _) = PlayOut(seed);
            Assert.GreaterOrEqual(TableRules.Points(sim.State, T(sim).Winner), 10);
        }

        [TestCase(2)]
        [TestCase(3)]
        public void AiOnlyGames_FewerPeoples_End(int people)
        {
            var (sim, _) = PlayOut(Seed, people);
            Assert.GreaterOrEqual(TableRules.Points(sim.State, T(sim).Winner), 10);
        }

        [Test]
        public void SaveMidGame_JsonRoundTrip_ContinuesIdentically()
        {
            var sim = Create(humanSeat: -1);
            Step(sim, 1000);
            Assert.AreNotEqual(TablePhase.Ended, T(sim).Phase);
            string json = JsonConvert.SerializeObject(sim.State);
            var copy = JsonConvert.DeserializeObject<WorldState>(json);
            Assert.AreEqual(JsonConvert.SerializeObject(sim.State.Table), JsonConvert.SerializeObject(copy.Table));
            var sim2 = new WorldSim(copy);
            TableModule.Register(sim2);
            Step(sim, 5000);
            Step(sim2, 5000);
            Assert.AreEqual(JsonConvert.SerializeObject(sim.State.Table), JsonConvert.SerializeObject(sim2.State.Table));
        }

        [Test]
        public void SameSeedSameCommands_SameState_WithAPlayer()
        {
            string Run()
            {
                var sim = ToHumanTurn(wire: seams => { });
                var t = T(sim);
                for (int turn = 0; turn < 6 && t.Phase != TablePhase.Ended; turn++)
                {
                    StepUntil(sim, () => t.Phase == TablePhase.Main && t.Current == 0 || t.Phase == TablePhase.Ended, 20000);
                    if (t.Phase == TablePhase.Ended) break;
                    if (t.Peoples[0].Hand[0] >= 4) Submit(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Stone });
                    sim.Step();
                    Submit(sim, new EndTurn());
                    sim.Step();
                }
                return JsonConvert.SerializeObject(sim.State.Table);
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
