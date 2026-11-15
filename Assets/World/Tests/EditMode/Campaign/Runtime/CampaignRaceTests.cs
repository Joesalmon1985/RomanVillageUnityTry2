using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// The race tuning (sim-H1-cry-S1-tuned): the party's walking and running pace, stairs, the per-run draw of the
    /// expedition's opening, the threshold wait with the player near or away, the opening report and the overrun retreat.
    /// Every new field defaults to the earlier rule; the shipped authored pack sets them.
    /// </summary>
    public class CampaignRaceTests
    {
        private static WorldSim Authored(ulong seed = 7, System.Action<CampaignDefinition> edit = null)
        {
            var d = PackFiles.Load(PackFiles.Authored);
            d.Tuning.LeadPatience = int.MaxValue;   // the test answers for the player
            edit?.Invoke(d);
            var sim = CampaignSamples.Started(seed, d);
            Quiet(sim);
            return sim;
        }

        [Test]
        public void NewFields_DefaultToTheEarlierRules()
        {
            var t = new CampaignTuning();
            Assert.AreEqual(0, t.PartyRunTicksPerStep, "never runs");
            Assert.AreEqual(0, t.StairTicks, "a stair is one step at the leg's pace");
            Assert.AreEqual(0, t.InteriorTicksPerStep, "underground at PartyTicksPerStep");
            Assert.AreEqual(-1, t.ThresholdWaitPresent, "ThresholdWait with the player present too");
            Assert.AreEqual(-1, t.OpeningReportDelay, "the opening report after ReportDelay");
            Assert.AreEqual(1, t.RetreatOnLooting);
            Assert.AreEqual(0, t.OverrunRetreatCooldown);
            Assert.AreEqual(0, new RivalStepDef().MarchSpread);
            Assert.IsEmpty(new RivalPlanDef().OpenWith);
            var sim = CampaignSamples.Started(7);
            Assert.IsNull(sim.State.Campaign.RivalPlan, "nothing to draw: the definition's plan as written");
        }

        [Test]
        public void AuthoredPack_SetsTheRaceNumbers()
        {
            var t = PackFiles.Load(PackFiles.Authored).Tuning;
            Assert.AreEqual(20, t.PartyTicksPerStep, "a brisk walk, 2.5 m/s");
            Assert.AreEqual(7, t.PartyRunTicksPerStep, "a run, about 7 m/s");
            Assert.AreEqual(30, t.InteriorTicksPerStep);
            Assert.AreEqual(120, t.StairTicks);
            Assert.AreEqual(0, t.ThresholdWaitPresent);
            Assert.AreEqual(900, t.ThresholdWait);
            Assert.AreEqual(0, t.OpeningReportDelay);
        }

        [Test]
        public void RivalPlan_DrawnPerSeed_WithinItsBounds_AndKeptBySaves()
        {
            var marches = new HashSet<int>();
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var sim = Authored(seed);
                var plan = sim.State.Campaign.RivalPlan;
                Assert.IsNotNull(plan);
                Assert.AreEqual("earth", plan[0].Crystal, "the authored pack always opens with the Deepstone");
                Assert.That(plan[0].MarchCost, Is.InRange(2700 - 600, 2700 + 600)); // 2250 before I8's 45 s opening delay
                Assert.That(plan[1].MarchCost, Is.InRange(3000 - 600, 3000 + 600));
                marches.Add(plan[0].MarchCost);
                Assert.AreEqual(plan[0].MarchCost, Authored(seed).State.Campaign.RivalPlan[0].MarchCost, "the same seed draws the same plan");
                var loaded = SaveLoad(sim);
                Assert.AreEqual(plan[0].MarchCost, loaded.State.Campaign.RivalPlan[0].MarchCost);
            }
            Assert.GreaterOrEqual(marches.Count, 4, "the opening march differs by seed");

            // OpenWith over all four: the opening crystal varies, the march costs stay with the positions.
            var opens = new HashSet<string>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var sim = Authored(seed, d => d.Rival.OpenWith = new List<string> { "earth", "fire", "water", "air" });
                var plan = sim.State.Campaign.RivalPlan;
                opens.Add(plan[0].Crystal);
                Assert.That(plan[0].MarchCost, Is.InRange(2100, 3300), "the opening march is the first position's (2700 +- 600 since I8's 45 s opening delay)");
                Assert.AreEqual(4, plan.Select(x => x.Crystal).Distinct().Count());
                Assert.AreEqual(sim.State.Campaign.Definition.Rival.Steps.First(x => x.Crystal == plan[0].Crystal).Altar, plan[0].Altar, "a crystal keeps its altar");
            }
            Assert.GreaterOrEqual(opens.Count, 2);
        }

        [Test]
        public void Validator_ChecksTheDrawBounds()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            Assert.IsFalse(CampaignValidator.Validate(d).Any(p => p.IsError), "the shipped pack is valid");
            d.Rival.OpenWith.Add("moon");
            Assert.IsTrue(CampaignValidator.Validate(d).Any(p => p.IsError && p.Code == ProblemCodes.RivalPlan && p.Id == "moon"));
            d = PackFiles.Load(PackFiles.Authored);
            d.Rival.Steps[0].MarchSpread = d.Rival.Steps[0].MarchCost;
            Assert.IsTrue(CampaignValidator.Validate(d).Any(p => p.IsError && p.Code == ProblemCodes.RivalPlan && p.Id == "earth"));
        }

        [Test]
        public void TheOpeningReport_IsKnownAtOnce_AndMiraRunsToTheGlade_ThenWalksUnderground()
        {
            var sim = Authored();
            var s = sim.State;
            sim.Step();
            Assert.AreEqual(RivalReportKind.Marching, CampaignApi.RivalView(s).Reports.Single(x => x.Crystal == Element.Earth).Kind, "word of the opening at once");
            var ev = Follow(sim, "earth_stone");
            var first = ev.OfType<PartySetOut>().First();
            Assert.IsTrue(first.Running, "Ash runners are heading for the Deepstone: she runs");
            int steps = CampaignRuntime.RouteSteps(s, s.Campaign, first.From, first.To);
            Assert.AreEqual(steps * 7, first.LegTicks);
            Assert.IsTrue(CampaignApi.Journal(s).Any(j => j.Text.Contains("breaks into a run")));
            ev = Follow(sim, "earth_marks");
            var legs = ev.OfType<PartySetOut>().ToList();
            Assert.IsTrue(legs.All(x => !x.Running || (!x.From.IsInterior && !x.To.IsInterior)), "never runs underground");
            Assert.IsTrue(legs.Any(x => x.LegTicks == 120 && x.From.IsInterior != x.To.IsInterior), "the stair takes StairTicks");
            var under = legs.First(x => x.From.IsInterior && x.To.IsInterior);
            Assert.AreEqual(CampaignRuntime.RouteSteps(s, s.Campaign, under.From, under.To) * 30, under.LegTicks, "underground at InteriorTicksPerStep");
        }

        [Test]
        public void Walking_WhenNoWordOfTheAsh()
        {
            var sim = Authored(7, d => d.Tuning.OpeningReportDelay = 100000);
            var ev = Follow(sim, "earth_stone");
            var first = ev.OfType<PartySetOut>().First();
            Assert.IsFalse(first.Running);
            Assert.AreEqual(CampaignRuntime.RouteSteps(sim.State, sim.State.Campaign, first.From, first.To) * 20, first.LegTicks);
        }

        private static WorldSim AtDolm(out CampaignState c)
        {
            var sim = Authored();
            c = sim.State.Campaign;
            Do(sim, new DevRivalNextStep());
            Follow(sim, "earth_stone");
            Follow(sim, "earth_marks");
            Follow(sim, "earth_root");
            Follow(sim, "earth_tally");
            Follow(sim, "earth_vault");
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim));
            return sim;
        }

        [Test]
        public void Threshold_WithThePlayerNear_SheWaitsWithNoLimit_AwayThe90sCountFromLeaving()
        {
            var sim = AtDolm(out var c);
            var ev = SimTestKit.Step(sim, 3000);
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim), "five minutes with the player near: still there");
            Assert.AreEqual(0, ev.OfType<PartyRetreated>().Count());
            Do(sim, new SetCompany { Present = false });
            long left = c.Party.WaitSinceTick;
            Assert.AreEqual(sim.State.Tick, left, "the away wait starts when the player leaves");
            ev = Until(sim, () => Mode(sim) != PartyMode.AtThreshold, 2000, "the away withdrawal");
            var back = ev.OfType<PartyRetreated>().Single();
            Assert.AreEqual("threshold_wait", back.Why);
            Assert.AreEqual(900, back.Tick - left, "ThresholdWait, counted from when the player left");
        }

        [Test]
        public void Overrun_LootingOnlyCharges_AndRetreatsHaveACooldown()
        {
            var sim = Authored();
            var s = sim.State;
            var c = s.Campaign;
            Follow(sim, "earth_stone");
            long p = c.RivalProgress;
            CampaignRuntime.OnCampOverrun(s, c, PlayerIds.Rival, PlayerIds.Human, null, true);
            Assert.AreEqual(p + 600, c.RivalProgress, "a looting charges CampOverrunCharge");
            Assert.AreNotEqual(RoutePurpose.Retreat, c.Party.Purpose, "and the party presses on");
            CampaignRuntime.OnCampOverrun(s, c, PlayerIds.Rival, PlayerIds.Human, null, false);
            Assert.AreEqual(RoutePurpose.Retreat, c.Party.Purpose, "a camp lost turns the party home");
            Until(sim, () => Settled(sim), 3000, "home");
            Follow(sim, "earth_marks");
            long p2 = c.RivalProgress;
            CampaignRuntime.OnCampOverrun(s, c, PlayerIds.Rival, PlayerIds.Human, null, false);
            Assert.AreNotEqual(RoutePurpose.Retreat, c.Party.Purpose, "a second camp lost inside OverrunRetreatCooldown only charges");
            Assert.AreEqual(p2 + 600, c.RivalProgress);
        }
    }
}
