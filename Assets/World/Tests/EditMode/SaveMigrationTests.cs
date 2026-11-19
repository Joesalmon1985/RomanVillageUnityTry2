using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using World.Sim.Eras;
using World.Sim.Factions;
using World.Sim.Ledger;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Save v5 (PRD_AGES AG-52, H1-0): a v4 save loads by migration as a Prehistoric skirmish and plays on identically;
    /// v3 and newer-than-current saves are refused; v5 round trips keep the ordinal ledger and history order; the
    /// campaign slot is null by default and a filled one loads.
    /// </summary>
    public class SaveMigrationTests
    {
        private static WorldSim MidGame()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var top = FactionApi.Top(sim.State, PlayerIds.Human);
            Assert.IsTrue(sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = top.Id }).Ok);
            sim.Step(2400);
            sim.DrainEvents();
            return sim;
        }

        /// <summary>A v5 save turned into the shape version 4 wrote: no H1 modules, landmarks or H1 fields.</summary>
        private static string AsV4(string v5Json)
        {
            var root = JObject.Parse(v5Json);
            root["Version"] = 4;
            var st = (JObject)root["State"];
            foreach (var n in new[] { "Eras", "History", "Ledger", "Campaign" }) st.Remove(n);
            ((JObject)st["Board"]).Remove("Landmarks");
            var eco = (JObject)st["Economy"];
            foreach (JObject b in (JArray)eco["Buildings"])
                foreach (var n in new[] { "Grade", "Upgrading", "UpgradeDelivered", "UpgradeIncoming", "UpgradeProgress", "UpgradeCredits" }) b.Remove(n);
            foreach (JObject s in (JArray)eco["Settlements"]) { s.Remove("IsTown"); s.Remove("IsDistrict"); }
            ((JObject)eco["Ledger"]).Remove("Looted");
            var et = (JObject)eco["Tuning"];
            foreach (var n in new[] { "TownCost", "TownToolWare", "TownBuildTicks", "CarriersPerTown", "TownShipmentSize", "DistrictCredits",
                         "DistrictBuildTicks", "CarriersPerDistrict", "DistrictShipmentSize" }) et.Remove(n);
            var fs = (JObject)st["Factions"];
            fs.Remove("Halts");
            foreach (JObject f in (JArray)fs["Factions"])
                foreach (var n in new[] { "HandoverTick", "AnnounceTicks", "Personality", "Advice", "SetbackUntilTick" }) f.Remove(n);
            var ft = (JObject)fs["Tuning"];
            foreach (var n in new[] { "AnnounceTicks", "SetbackGraceTicks", "LootHaltTicks", "AdvicePriorityBonus", "AdvicePriorityTicks",
                         "AdviceFocusBonus", "AdviceFocusTicks", "AdviceVetoTicks", "AdviceHoldTicks", "StrengthPerTown" }) ft.Remove(n);
            return root.ToString(Newtonsoft.Json.Formatting.None);
        }

        [Test]
        public void V4Save_LoadsAsPrehistoricSkirmish_AndPlaysOnIdentically()
        {
            var a = MidGame();
            string v4 = AsV4(SaveCodec.ToJson(a.State));
            StringAssert.DoesNotContain("\"Eras\"", v4);
            StringAssert.DoesNotContain("HandoverTick", v4);

            var b = SaveCodec.Load(v4, out var model);
            Assert.AreEqual(4, model.LoadedVersion);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.Version);
            var s = b.State;
            Assert.AreEqual(Era.Prehistoric, AgesApi.Current(s));
            Assert.AreEqual(PlayMode.Skirmish, AgesApi.Mode(s));
            Assert.AreEqual(ChapterIds.Prehistoric, AgesApi.Chapter(s));
            Assert.IsFalse(AgesApi.CampaignMayStart(s));
            Assert.AreEqual(-1, AgesApi.HandoverTick(s, PlayerIds.Human));
            Assert.AreEqual(PlayerIds.LegacyCount, s.Ledger.Factions.Count);
            Assert.IsNull(s.Campaign);
            Assert.IsNotNull(LandmarkApi.Get(s, LandmarkIds.HomeHearth));
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(s), "the migrated state equals the state a v5 build had");

            var ea = new List<string>();
            var eb = new List<string>();
            for (int t = 0; t < 1500; t++)
            {
                a.Step();
                b.Step();
                ea.AddRange(a.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick));
                eb.AddRange(b.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick));
            }
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State), "plays on identically for 1,500 ticks");
            CollectionAssert.AreEqual(ea, eb);
        }

        [Test]
        public void Versions_ThreeAndNewerAreRefused_EveryStepExists()
        {
            string v5 = SaveCodec.ToJson(WorldSimFactory.CreateNew(1).State);
            StringAssert.Contains("\"Version\":5", v5);
            var e = Assert.Throws<FormatException>(() => SaveCodec.FromJson(v5.Replace("\"Version\":5", "\"Version\":3")));
            Assert.AreEqual("The save is from an older version (3) with a different world.", e.Message);
            Assert.Throws<FormatException>(() => SaveCodec.FromJson(v5.Replace("\"Version\":5", "\"Version\":6")));
            for (int v = SaveCodec.OldestVersion; v < SaveCodec.CurrentVersion; v++)
                CollectionAssert.Contains(SaveMigrations.StepVersions, v, "a migration step from version " + v);
            var m = SaveCodec.FromJson(v5);
            Assert.AreEqual(5, m.LoadedVersion);
        }

        [Test]
        public void V5RoundTrip_KeepsOrdinalLedgerAndHistory()
        {
            var sim = WorldSimFactory.CreateNew(2);
            var s = sim.State;
            Assert.IsTrue(LedgerLogic.Award(s, 0, "mission:B:x", 1, null));
            Assert.IsTrue(LedgerLogic.Award(s, 0, "mission:a:x", 1, null));
            s.History.Outcomes["b.x"] = "1";
            s.History.Outcomes["B.x"] = "2";
            s.History.Casualties.Add(new CasualtyRecord { Id = "odo", Name = "Odo", Chapter = ChapterIds.Historic, Cause = "duel", Place = "the quarry", Tick = 5 });
            string json = SaveCodec.ToJson(s);
            var b = SaveCodec.Load(json, out _);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State));
            CollectionAssert.AreEqual(new[] { "mission:B:x", "mission:a:x" }, b.State.Ledger.Factions[0].Paid.ToArray());
            Assert.IsTrue(LedgerLogic.Award(b.State, 0, "mission:A:x", 1, null));
            CollectionAssert.AreEqual(new[] { "mission:A:x", "mission:B:x", "mission:a:x" }, b.State.Ledger.Factions[0].Paid.ToArray(), "ordinal after load");
            CollectionAssert.AreEqual(new[] { "B.x", "b.x" }, b.State.History.Outcomes.Keys.ToArray());
            Assert.AreEqual("the quarry", b.State.History.Casualties[0].Place);
        }

        [Test]
        public void CampaignSlot_NullByDefault_FilledShellLoads()
        {
            var sim = WorldSimFactory.CreateNew(3);
            string json = SaveCodec.ToJson(sim.State);
            StringAssert.Contains("\"Campaign\":null", json);
            var filled = json.Replace("\"Campaign\":null", "\"Campaign\":{\"FormatVersion\":1,\"Unknown\":7}");
            var b = SaveCodec.Load(filled, out _);
            Assert.IsNotNull(b.State.Campaign);
            Assert.AreEqual(1, b.State.Campaign.FormatVersion);
            b.Step(10);
        }
    }
}
