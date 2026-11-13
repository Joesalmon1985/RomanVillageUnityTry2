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
            public SortedDictionary<string, long> Milestones;
            public Dictionary<string, int> EventCounts = new Dictionary<string, int>();

            public override string ToString() =>
                $"seed {Seed}: {Outcome} ({Reason}) at tick {Ticks} = {Ticks / 600.0:0.0} min, VP {PlayerVp}-{RivalVp}, rounds {Rounds}, cities {Cities}, cards {CardsBuilt}, " +
                $"wonder {WonderStages}, units {UnitsTrained}, outpost {(OutpostDestroyed ? "destroyed" : "standing")}, breaches {Breaches}; " +
                string.Join(", ", Milestones.Select(kv => kv.Key + "@" + kv.Value)) + " || " + Notes;
        }

        internal static GameReport Play(ulong seed, bool passive, bool checkEveryTick = true, bool completionist = false)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            var player = new StrategyPlayer(sim, passive, completionist);
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

        [Test]
        public void FullPlaythrough_TenOtherSeeds_ReachAnEndingWithInvariants()
        {
            var lines = new List<string>();
            for (ulong i = 1; i <= 10; i++)
            {
                var r = Play(i * 104729UL + 11UL, false, completionist: true);
                lines.Add(r.ToString());
                Assert.AreNotEqual(GameOutcome.None, r.Outcome, r.ToString());
                Assert.IsTrue(r.Milestones.ContainsKey("city2") && r.Milestones.ContainsKey("barracks"), r.ToString());
            }
            foreach (var l in lines) TestContext.WriteLine(l);
            Console.WriteLine("FULL seeds\n" + string.Join("\n", lines));
        }

        [Test]
        public void Balance_DefaultSeed_CompetentPlayerWinsByPointsInSixToNineMinutes()
        {
            var r = Play(EconomyTestKit.DefaultSeed, false);
            TestContext.WriteLine(r);
            Console.WriteLine("COMPETENT " + r);
            Assert.AreEqual(GameOutcome.Victory, r.Outcome, r.ToString());
            Assert.That(r.Reason == OutcomeReasons.Points || r.Reason == OutcomeReasons.AgeEnd, "won on points");
            Assert.That(r.Ticks >= 6 * 600 && r.Ticks <= 9 * 600, "6 to 9 minutes of sim time: " + r.Ticks);
        }

        [Test]
        public void Balance_TenSeeds_CompetentPlayerMostlyWins()
        {
            int wins = 0;
            var lines = new List<string>();
            for (ulong i = 1; i <= 10; i++)
            {
                var r = Play(i * 104729UL + 11UL, false, checkEveryTick: false);
                lines.Add(r.ToString());
                if (r.Outcome == GameOutcome.Victory) wins++;
            }
            Console.WriteLine("COMPETENT seeds wins " + wins + "/10\n" + string.Join("\n", lines));
            Assert.GreaterOrEqual(wins, 6, string.Join("\n", lines));
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
