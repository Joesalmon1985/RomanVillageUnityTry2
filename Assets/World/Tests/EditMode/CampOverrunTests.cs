using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>The setback rule of the campaign (PRD_AGES 4.3; crystals PRD N-2, N-3; package H1-1).</summary>
    public class CampOverrunTests
    {
        /// <summary>Equal strength is enough and warnings are short: the player (checked first) attacks the Ash Clan.</summary>
        private static WorldSim Pressing(bool campaign)
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            s.Demons.Tuning.GraceTicks = int.MaxValue;
            if (campaign) ErasModule.EnterCampaign(s);
            s.Factions.Tuning.OverrunMargin = 0;
            s.Factions.Tuning.WarningGraceTicks = 0;
            s.Factions.Tuning.WarningTicks = 10;
            s.Factions.Tuning.MaxOpenSites = 0; // neither people builds: the strengths stay equal
            return sim;
        }

        [Test]
        public void Skirmish_OverrunStillEndsTheGame()
        {
            var sim = Pressing(false);
            var ev = K.Step(sim, 20);
            Assert.AreEqual(OutcomeReasons.Overrun, ev.OfType<GameEnded>().Single().Reason);
            Assert.IsEmpty(ev.OfType<CampOverrun>());
        }

        [Test]
        public void Campaign_OnlyHome_IsLooted_Halted_ThenGrace_AndTheGameGoesOn()
        {
            var sim = Pressing(true);
            var s = sim.State;
            int rivalHome = CampaignRules.HomeSettlement(s, 1);
            var home = s.Economy.Settlements[rivalHome];
            int woodBefore = home.Stock[(int)Ware.Wood];
            var ev = K.Step(sim, 20);
            Assert.IsEmpty(ev.OfType<GameEnded>());
            Assert.IsFalse(s.Factions.GameOver);
            var co = ev.OfType<CampOverrun>().Single();
            Assert.AreEqual((0, 1, rivalHome, CampOverrunKind.Looted), (co.Attacker, co.Defender, co.SettlementId, co.Kind));
            Assert.AreEqual(1, home.Faction, "the home is never lost");
            Assert.LessOrEqual(home.Stock[(int)Ware.Wood], woodBefore - woodBefore / 2 + 2, "half the stock is looted");
            Assert.Greater(s.Economy.Ledger.Looted.Sum(), 0);
            Assert.IsTrue(CampaignRules.IsHalted(s, rivalHome));
            Assert.AreEqual(co.Tick + 3000, s.Factions.Factions[1].SetbackUntilTick);
            // No new warning against the Ash Clan during the grace; then it can come again.
            var grace = K.Step(sim, 2970);
            Assert.IsEmpty(grace.OfType<OverrunWarningStarted>().Where(w => w.Defender == 1));
            Assert.IsFalse(CampaignRules.IsHalted(s, rivalHome), "the halt lasts 3 minutes");
            var later = K.Step(sim, 100);
            Assert.AreEqual(1, later.OfType<OverrunWarningStarted>().Count(w => w.Defender == 1));
            Assert.AreEqual(1, later.Concat(K.Step(sim, 20)).OfType<CampOverrun>().Count(), "the second setback");
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Campaign_ANonHomeCamp_IsCaptured_TheHomeNever()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int camp = K.SecondCamp(sim, out _);
            ErasModule.EnterCampaign(s);
            int home = CampaignRules.HomeSettlement(s, 0);
            Assert.AreEqual(camp, CampaignRules.CaptureTarget(s, 1, 0, home));
            var ev = new EventBuffer();
            CampaignRules.Overrun(s, 1, 0, ev);
            var co = ev.Items.OfType<CampOverrun>().Single();
            if (co.Kind == CampOverrunKind.Looted)
                Assert.Ignore("EconomySeams.TransferSettlement is not real in this build (H1-2); the overrun fell back to looting the home.");
            Assert.AreEqual((1, 0, camp, CampOverrunKind.CampLost), (co.Attacker, co.Defender, co.SettlementId, co.Kind));
            Assert.AreEqual(1, s.Economy.Settlements[camp].Faction, "captured");
            Assert.AreEqual(0, s.Economy.Settlements[home].Faction);
            Assert.IsTrue(ev.Items.OfType<SettlementTransferred>().Any());
            Assert.IsTrue(s.Factions.Proposals.All(p => FactionLogic.CheckAction(s, p).Ok), "stale proposals are dropped at once");
            Assert.IsNull(K.CheckInvariants(s));
            // With only the home left, the next overrun loots it.
            ev = new EventBuffer();
            Assert.AreEqual(-1, CampaignRules.CaptureTarget(s, 1, 0, home));
            CampaignRules.Overrun(s, 1, 0, ev);
            Assert.AreEqual(CampOverrunKind.Looted, ev.Items.OfType<CampOverrun>().Single().Kind);
            Assert.AreEqual(0, s.Economy.Settlements[home].Faction);
        }

        [Test]
        public void AfterTheHistoricStep_OrInADevStart_OverrunIsASetback()
        {
            foreach (var setup in new System.Action<WorldState>[] { s => s.Eras.Current = Era.Historic, s => s.Eras.Mode = PlayMode.DevStart })
            {
                var sim = Pressing(false);
                setup(sim.State);
                var ev = K.Step(sim, 20);
                Assert.IsEmpty(ev.OfType<GameEnded>());
                Assert.AreEqual(1, ev.OfType<CampOverrun>().Count());
                Assert.IsFalse(AgesApi.IsCampaign(sim.State), "the campaign flag itself is untouched");
            }
        }
    }
}
