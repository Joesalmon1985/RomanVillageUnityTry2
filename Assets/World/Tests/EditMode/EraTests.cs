using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using World.Sim.Save;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>The Prehistoric to Historic step and dev.historic (PRD_AGES 4.2, 4.6, 4.7, 5.5; package H1-1).</summary>
    public class EraTests
    {
        [Test]
        public void HistoricStep_SmithsChest_IsDataUntilItsWaresExist()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            Assert.IsTrue(s.Eras.Tuning.SmithsChestWares.All(w => w >= Wares.Count), "tools and iron arrive with H2");
            var before = K.WaresInWorld(s);
            ErasLogic.MarkReady(s, ChapterIds.Historic, null);
            K.Do(sim, new AdvanceChapter { To = ChapterIds.Historic });
            Assert.AreEqual(Era.Historic, AgesApi.Current(s));
            Assert.AreEqual(0, ErasLogic.DeliverSmithsChest(s), "nothing to deliver in H1");
            Assert.IsNull(K.CheckInvariants(s));

            // With wares that exist (a test chest), each home storehouse receives it and the balance holds.
            var sim2 = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s2 = sim2.State;
            s2.Eras.Tuning.SmithsChestWares = new[] { (int)Ware.Planks, 40 };
            s2.Eras.Tuning.SmithsChestCounts = new[] { 3, 4 };
            int p0 = s2.Economy.Settlements[CampaignRules.HomeSettlement(s2, 0)].Stock[(int)Ware.Planks];
            int p1 = s2.Economy.Settlements[CampaignRules.HomeSettlement(s2, 1)].Stock[(int)Ware.Planks];
            ErasLogic.MarkReady(s2, ChapterIds.Historic, null);
            K.Do(sim2, new AdvanceChapter { To = ChapterIds.Historic });
            Assert.AreEqual(p0 + 3, s2.Economy.Settlements[CampaignRules.HomeSettlement(s2, 0)].Stock[(int)Ware.Planks]);
            Assert.AreEqual(p1 + 3, s2.Economy.Settlements[CampaignRules.HomeSettlement(s2, 1)].Stock[(int)Ware.Planks]);
            Assert.IsNull(K.CheckInvariants(s2));
        }

        [Test]
        public void HistoricStep_UnlocksGrades_AndEndsTheSkirmishClock()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var mine = s.Economy.Buildings.First(b => b.Faction == 0 && b.State == BuildingState.Active);
            Assert.AreEqual("wrong_era", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = mine.Id }).Reason);
            ErasLogic.MarkReady(s, ChapterIds.Historic, null);
            K.Do(sim, new AdvanceChapter { To = ChapterIds.Historic });
            Assert.AreNotEqual("wrong_era", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = mine.Id }).Reason);
            Assert.IsFalse(StrengthSystem.TimeLimited(s));
        }

        [Test]
        public void TheStepByInvitation_AfterARealHandover_SurvivesSaveAndLoad()
        {
            var sim = WorldSimFactory.CreateNew(4);
            var s = sim.State;
            var player = new ScriptedPlayer(sim, PlayerStyle.Prompt, 0, 4);
            bool ready = false;
            while (!ready && s.Tick < 12000)
            {
                player.Act();
                sim.Step();
                ready = sim.DrainEvents().OfType<ChapterReady>().Any();
            }
            Assert.IsTrue(ready);
            Assert.AreEqual("chapter_not_ready", sim.Check(new AdvanceChapter { PlayerId = 0, To = ChapterIds.ModernHarbour }).Reason);
            var b = SaveCodec.Load(SaveCodec.ToJson(s), out _);
            Assert.IsTrue(AgesApi.IsReady(b.State, ChapterIds.Historic), "readiness is saved");
            Assert.IsTrue(b.State.Factions.Factions[0].Auto, "so is the prince's autonomy");
            var ea = K.Do(sim, new AdvanceChapter { To = ChapterIds.Historic });
            var eb = K.Do(b, new AdvanceChapter { To = ChapterIds.Historic });
            Assert.AreEqual(1, ea.OfType<ChapterAdvanced>().Count());
            Assert.AreEqual(ea.Select(e => e.GetType().Name + e.Tick), eb.Select(e => e.GetType().Name + e.Tick));
            K.Step(sim, 600);
            K.Step(b, 600);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State));
            Assert.IsTrue(AgesApi.CampaignMayStart(s));
        }

        [Test]
        public void DevHistoric_HasTheAuthoredContents_ForBothPeoples_AndIsDeterministic()
        {
            var sim = ScenarioStarts.Create(ScenarioStarts.DevHistoric, 2);
            var s = sim.State;
            Assert.AreEqual((Era.Historic, PlayMode.DevStart, ScenarioStarts.DevHistoric), (AgesApi.Current(s), AgesApi.Mode(s), s.Eras.StartId));
            for (int f = 0; f < PlayerIds.Count; f++)
            {
                Assert.IsTrue(ScenarioStarts.HistoricStartReached(s, f), "people " + f);
                Assert.That(AgesApi.HandoverTick(s, f), Is.InRange(0, s.Eras.ChapterStartTick));
                Assert.IsTrue(s.Factions.Factions[f].Auto);
            }
            Assert.AreEqual(ChapterIds.Historic, s.History.Chapters.Single().To);
            Assert.AreEqual(s.Factions.Tuning.OverrunMargin, new FactionTuning { OverrunMargin = FactionTunings.Tuned().OverrunMargin }.OverrunMargin, "tuning restored");
            Assert.IsNull(K.CheckInvariants(s));
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(ScenarioStarts.Create(ScenarioStarts.DevHistoric, 2).State));
            K.Step(sim, 1200);
            Assert.IsFalse(s.Factions.GameOver);
        }
    }
}
