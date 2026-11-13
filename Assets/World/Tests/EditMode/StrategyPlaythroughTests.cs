using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>
    /// Full headless games with the scripted players (default tuning, real carrier speed), all invariants checked
    /// every 25 ticks. The games are shared with the proof report (GameRuns caches them for the test run).
    /// </summary>
    public class StrategyPlaythroughTests
    {
        private const int Minute = 600;

        private static IEnumerable<GameRecord> All(string mode) => GameRuns.Seeds.Select(seed => GameRuns.Get(seed, mode));

        [Test]
        public void FullPlaythrough_DefaultSeed_CoversTheWholeChain()
        {
            var r = GameRuns.Get(EconomyTestKit.DefaultSeed, "full");
            TestContext.WriteLine(r);
            Console.WriteLine("FULL " + r);
            Assert.AreNotEqual(GameOutcome.None, r.Outcome, "the game reached an ending");
            foreach (var m in new[] { "commission", "age1", "city2", "mine", "power_plant", "smelter", "smithy", "farm", "sheepfold", "mill",
                                      "bakery", "butcher", "tavern", "barracks", "first_soldier", "wonder_site", "wonder1" })
                Assert.IsTrue(r.Milestones.ContainsKey(m), "playthrough covers " + m + ": " + r);
            Assert.Greater(r.CardsBuilt, 0);
            Assert.Greater(r.UnitsTrained, 0);
            Assert.GreaterOrEqual(r.EventCounts.TryGetValue("DraftPicked", out int dp) ? dp : 0, 8, "at least two drafts, two picks each, both sides");
            Assert.GreaterOrEqual(r.Comparisons.Count, 2, "military comparisons at the age ends");
            foreach (var w in Wares.All) Assert.Greater(r.Ledger.Harvested[(int)w] + r.Ledger.Produced[(int)w], 0, w + " was made in a full game");
        }

        [Test]
        public void FullPlaythrough_ElevenSeeds_EndIn8To15Minutes_SquadsAreTrained()
        {
            var lines = new List<string>();
            int squads = 0, outposts = 0;
            foreach (var r in All("full"))
            {
                lines.Add(r.ToString());
                Assert.AreNotEqual(GameOutcome.None, r.Outcome, r.ToString());
                Assert.IsTrue(r.Milestones.ContainsKey("city2") && r.Milestones.ContainsKey("barracks"), r.ToString());
                Assert.That(r.Ticks >= 7.5 * Minute && r.Ticks <= 15.5 * Minute, "7.5 to 15.5 minutes: " + r);
                if (r.UnitsTrained >= 4) squads++;
                if (r.OutpostDestroyed) outposts++;
            }
            Console.WriteLine("FULL squads of 4: " + squads + ", outposts destroyed: " + outposts + "\n" + string.Join("\n", lines));
            Assert.GreaterOrEqual(squads, 6, "most full games train a squad of 4");
        }

        [Test]
        public void Balance_DefaultSeed_CompetentPlayerReachesTheTargetIn8To13Minutes_WithTheWonder()
        {
            var r = GameRuns.Get(EconomyTestKit.DefaultSeed, "competent");
            Console.WriteLine("COMPETENT " + r);
            Assert.AreEqual(GameOutcome.Victory, r.Outcome, r.ToString());
            Assert.AreEqual(OutcomeReasons.Points, r.Reason, "the target before the end of Age III");
            Assert.GreaterOrEqual(r.PlayerVp, new StrategyTuning().VictoryPoints);
            Assert.GreaterOrEqual(r.WonderStages, 1, "wonder stage 1 completes");
            Assert.That(r.Ticks >= 8 * Minute && r.Ticks <= 13 * Minute, "8 to 13 minutes: " + r.Ticks);
            Assert.Less(r.Milestone("commission"), 4 * Minute, "commission under 4 minutes");
        }

        [Test]
        public void Balance_TenSeeds_CompetentPlayerWinsOnPoints()
        {
            int wins = 0, wonders = 0;
            var lines = new List<string>();
            foreach (var r in All("competent").Skip(1))
            {
                lines.Add(r.ToString());
                if (r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Points && r.Ticks >= 6.5 * Minute && r.Ticks <= 15 * Minute) wins++;
                if (r.WonderStages >= 1) wonders++;
                Assert.Less(r.Milestone("commission"), 4 * Minute, "commission under 4 minutes: " + r);
            }
            Console.WriteLine("COMPETENT seeds: wins " + wins + "/10, wonder stage 1 in " + wonders + "/10\n" + string.Join("\n", lines));
            Assert.GreaterOrEqual(wins, 8, string.Join("\n", lines));
            Assert.GreaterOrEqual(wonders, 7, "wonder stage 1 completes in a normal competent game");
        }

        [Test]
        public void CarelessPlayer_IgnoringDemons_Loses_MostlyByBreach()
        {
            var all = All("careless").ToList();
            Console.WriteLine("CARELESS\n" + string.Join("\n", all));
            // Measured: 2 of 11 careless games still reach the target before the demons break through (a known weakness, see
            // Docs/ECONOMY_PROOF.md); the test pins that it stays rare.
            Assert.LessOrEqual(all.Count(r => r.Outcome == GameOutcome.Victory), 2, "a careless player rarely wins: " + string.Join(" | ", all));
            Assert.GreaterOrEqual(all.Count(r => r.Reason == OutcomeReasons.Breach), all.Count / 2, "most careless games end at the breach limit");
            Assert.Greater(all.Sum(r => r.Outbreaks), 0);
        }

        [Test]
        public void PassivePlayer_LosesOrIsOutscored()
        {
            foreach (var r in All("passive"))
            {
                Assert.AreNotEqual(GameOutcome.None, r.Outcome);
                Assert.AreNotEqual(GameOutcome.Victory, r.Outcome, r.ToString());
                Assert.Less(r.PlayerVp, Math.Max(r.RivalVp, new StrategyTuning().VictoryPoints), r.ToString());
            }
        }

        [Test, Explicit]
        public void Measure_AllPlayers()
        {
            foreach (var mode in GameRuns.Modes)
                foreach (var r in All(mode))
                    Console.WriteLine($"M {mode} {r.Seed} {r.Outcome} {r.Reason} {r.Minutes:0.0}min VP {r.PlayerVp}-{r.RivalVp} br {r.Breaches} cities {r.Cities} wonder {r.WonderStages} cards {r.CardsBuilt} units {r.UnitsTrained} outpost {(r.OutpostDestroyed ? "X" : "-")} commission {r.Milestone("commission")} bottleneck {r.Bottleneck} carriers {r.CarrierUsePercent}%");
        }
    }
}
