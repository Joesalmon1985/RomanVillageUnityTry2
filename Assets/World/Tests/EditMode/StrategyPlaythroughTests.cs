using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>Full headless games with the scripted players (default tuning, real carrier speed).</summary>
    public class StrategyPlaythroughTests
    {
        public const int TickBudget = 20000; // 33 minutes of sim time

        internal sealed class GameReport
        {
            public ulong Seed;
            public long Ticks;
            public GameOutcome Outcome;
            public string Reason;
            public int PlayerVp, RivalVp, Breaches, Cities, CardsBuilt, WonderStages, UnitsTrained, Rounds;
            public bool OutpostDestroyed;
            public string Notes;
            public int Guards, SquadUnits;
            public SortedDictionary<string, long> Milestones;
            public Dictionary<string, int> EventCounts = new Dictionary<string, int>();

            public override string ToString() =>
                $"seed {Seed}: {Outcome} ({Reason}) at tick {Ticks} = {Ticks / 600.0:0.0} min, VP {PlayerVp}-{RivalVp}, rounds {Rounds}, cities {Cities}, cards {CardsBuilt}, " +
                $"wonder {WonderStages}, units {UnitsTrained}, outpost {(OutpostDestroyed ? "destroyed" : "standing")}, breaches {Breaches}; " +
                string.Join(", ", Milestones.Select(kv => kv.Key + "@" + kv.Value)) + " || " + Notes;
        }

        internal static GameReport Play(ulong seed, bool passive, bool checkEveryTick = true, bool completionist = false, bool careless = false)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            var player = new StrategyPlayer(sim, passive, completionist) { IgnoreDemons = careless };
            var rep = new GameReport { Seed = seed };
            for (int t = 0; t < TickBudget && !s.Strategy.GameOver; t++)
            {
                player.Act();
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    string k = e.GetType().Name;
                    rep.EventCounts[k] = rep.EventCounts.TryGetValue(k, out int n) ? n + 1 : 1;
                    if (e is CommandRejected cr && !(cr.Command is EndRound))
                        rep.EventCounts["rejected:" + cr.Command.GetType().Name + ":" + cr.Result.Reason] = 1;
                }
                if (checkEveryTick || t % 50 == 0)
                {
                    var err = StrategyTestKit.CheckInvariants(s);
                    if (err != null) Assert.Fail("seed " + seed + " tick " + s.Tick + ": " + err);
                }
            }
            rep.Ticks = s.Tick;
            rep.Outcome = s.Strategy.Outcome;
            rep.Reason = s.Strategy.OutcomeReason;
            rep.PlayerVp = StrategyApi.Score(s, 0).Total;
            rep.RivalVp = StrategyApi.Score(s, 1).Total;
            rep.Breaches = s.Strategy.Breaches;
            rep.Cities = StrategyApi.PlayerCities(s).Count;
            rep.CardsBuilt = s.Strategy.Sides[0].Built.Count;
            rep.WonderStages = s.Strategy.Wonder.StagesDone;
            rep.UnitsTrained = s.Military.UnitsTrained;
            rep.OutpostDestroyed = s.Military.Outpost.Destroyed;
            rep.Rounds = s.Economy.Round;
            rep.Milestones = player.Milestones;
            rep.Guards = MilitaryApi.Outpost(s).Guards;
            rep.SquadUnits = MilitaryApi.Squads(s, 0).Sum(q => q.Units);
            var wi = StrategyApi.Wonder(s);
            rep.Notes = wi.Status + " | " + (wi.BuildingId >= 0 ? EconomyApi.Describe(s, wi.BuildingId).Status + " " + string.Join(";", EconomyApi.Describe(s, wi.BuildingId).StockLines) : "no site");
            return rep;
        }

        private static readonly string[] FullMilestones =
            { "commission", "age1", "city2", "farm", "wonder_site", "wonder1_ordered", "power_plant", "barracks", "units", "attack" };

        [Test]
        public void FullPlaythrough_DefaultSeed_CoversTheWholeGame()
        {
            var r = Play(EconomyTestKit.DefaultSeed, false, completionist: true);
            TestContext.WriteLine(r);
            Console.WriteLine("FULL " + r);
            Assert.AreNotEqual(GameOutcome.None, r.Outcome, "the game reached an ending");
            foreach (var m in FullMilestones) Assert.IsTrue(r.Milestones.ContainsKey(m), "playthrough covers " + m + ": " + r);
            Assert.Greater(r.CardsBuilt, 0);
            Assert.Greater(r.UnitsTrained, 0);
            Assert.GreaterOrEqual(r.EventCounts.TryGetValue("DraftPicked", out int dp) ? dp : 0, 12, "three drafts, two picks each, both sides");
        }

        private static ulong OtherSeed(ulong i) => i * 104729UL + 11UL;

        private const int Minute = 600;

        [Test]
        public void FullPlaythrough_TenOtherSeeds_EndIn10To15Minutes_FullSquadsTakeTheOutpost()
        {
            var lines = new List<string>();
            int fullSquads = 0, taken = 0;
            foreach (var seed in new[] { EconomyTestKit.DefaultSeed }.Concat(Enumerable.Range(1, 10).Select(i => OtherSeed((ulong)i))))
            {
                var r = Play(seed, false, completionist: true);
                lines.Add(r.ToString());
                Assert.AreNotEqual(GameOutcome.None, r.Outcome, r.ToString());
                Assert.IsTrue(r.Milestones.ContainsKey("city2") && r.Milestones.ContainsKey("barracks"), r.ToString());
                Assert.That(r.Ticks >= 10 * Minute && r.Ticks <= 15.5 * Minute, "10 to 15 minutes: " + r);
                if (r.UnitsTrained >= 4)
                {
                    fullSquads++;
                    if (r.OutpostDestroyed) taken++;
                }
            }
            Console.WriteLine("FULL squads of 4: " + fullSquads + ", outposts destroyed by them: " + taken + "\n" + string.Join("\n", lines));
            Assert.GreaterOrEqual(fullSquads, 5, "most full games train a squad of 4");
            Assert.GreaterOrEqual(2 * taken, fullSquads, "a full squad destroys the outpost at least half the time");
        }

        [Test]
        public void Balance_DefaultSeed_CompetentPlayerReachesEightPointsIn8To13Minutes_WithTheWonder()
        {
            var r = Play(EconomyTestKit.DefaultSeed, false);
            Console.WriteLine("COMPETENT " + r);
            Assert.AreEqual(GameOutcome.Victory, r.Outcome, r.ToString());
            Assert.AreEqual(OutcomeReasons.Points, r.Reason, "8 points before the end of Age III");
            Assert.GreaterOrEqual(r.PlayerVp, 8);
            Assert.GreaterOrEqual(r.WonderStages, 1, "wonder stage 1 completes");
            Assert.That(r.Ticks >= 8 * Minute && r.Ticks <= 13 * Minute, "8 to 13 minutes: " + r.Ticks);
            Assert.Less(r.Milestones["commission"], 4 * Minute, "commission under 4 minutes");
        }

        [Test]
        public void Balance_TenSeeds_CompetentPlayerReachesEightPoints()
        {
            int wins = 0, wonders = 0;
            var lines = new List<string>();
            for (ulong i = 1; i <= 10; i++)
            {
                var r = Play(OtherSeed(i), false, checkEveryTick: false);
                lines.Add(r.ToString());
                if (r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Points && r.Ticks >= 6.5 * Minute && r.Ticks <= 13.5 * Minute) wins++;
                if (r.WonderStages >= 1) wonders++;
                Assert.Less(r.Milestones["commission"], 4 * Minute, "commission under 4 minutes: " + r);
            }
            Console.WriteLine("COMPETENT seeds: 8-point wins " + wins + "/10, wonder stage 1 in " + wonders + "/10\n" + string.Join("\n", lines));
            Assert.GreaterOrEqual(wins, 8, string.Join("\n", lines));
            Assert.GreaterOrEqual(wonders, 7, "wonder stage 1 completes in a normal competent game");
        }

        [Test]
        public void CarelessPlayer_IgnoringDemons_LosesByBreach()
        {
            var r = Play(EconomyTestKit.DefaultSeed, false, careless: true);
            Console.WriteLine("CARELESS " + r);
            Assert.AreEqual(GameOutcome.Defeat, r.Outcome, r.ToString());
            Assert.AreEqual(OutcomeReasons.Breach, r.Reason);
            Assert.AreEqual(4, r.Breaches);
        }

        [Test, Explicit]
        public void Measure_AllPlayers()
        {
            foreach (var mode in new[] { "competent", "full", "careless", "passive" })
                for (ulong i = 0; i <= 10; i++)
                {
                    ulong seed = i == 0 ? EconomyTestKit.DefaultSeed : i * 104729UL + 11UL;
                    var r = Play(seed, mode == "passive", false, mode == "full", mode == "careless");
                    Console.WriteLine($"M {mode} {seed} {r.Outcome} {r.Reason} {r.Ticks / 600.0:0.0}min VP {r.PlayerVp}-{r.RivalVp} br {r.Breaches} cities {r.Cities} wonder {r.WonderStages} cards {r.CardsBuilt} units {r.UnitsTrained} outpost {(r.OutpostDestroyed ? "X" : "-")} commission {(r.Milestones.TryGetValue("commission", out long c) ? c : -1)} attack {(r.Milestones.TryGetValue("attack", out long a) ? a : -1)} guards {r.Guards} squad {r.SquadUnits}");
                }
        }

        [Test]
        public void PassivePlayer_LosesOrIsOutscored()
        {
            var r = Play(EconomyTestKit.DefaultSeed, true);
            TestContext.WriteLine(r);
            Console.WriteLine("PASSIVE " + r);
            Assert.AreNotEqual(GameOutcome.None, r.Outcome);
            Assert.AreNotEqual(GameOutcome.Victory, r.Outcome);
            Assert.Less(r.PlayerVp, r.RivalVp);
        }
    }
}
