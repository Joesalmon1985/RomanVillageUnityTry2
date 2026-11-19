using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Factions;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>Peoples to N (PRD_GODBOARD PE-1 to PE-3; G0-1).</summary>
    public class PeoplesTests
    {
        private static WorldSim Four(ulong seed = 11) => WorldSimFactory.CreateNew(seed, GameSetup.GodBoard);

        [Test]
        public void PlayerIds_KeepHumanAndRival_AddElvesAndOrcs()
        {
            Assert.AreEqual(0, PlayerIds.Human);
            Assert.AreEqual(1, PlayerIds.Rival);
            Assert.AreEqual(PlayerIds.Rival, PlayerIds.AshClan);
            Assert.AreEqual(2, PlayerIds.Elves);
            Assert.AreEqual(3, PlayerIds.Orcs);
            Assert.AreEqual(4, PlayerIds.GodBoardCount);
            Assert.AreEqual(6, PlayerIds.MaxCount);
        }

        [Test]
        public void PeopleTable_HasIdNameColourBanner_AllDistinct()
        {
            var all = PeopleDefs.FirstN(PlayerIds.MaxCount);
            for (int i = 0; i < all.Count; i++)
            {
                Assert.AreEqual(i, all[i].Id);
                Assert.IsFalse(string.IsNullOrEmpty(all[i].Name));
                Assert.IsFalse(string.IsNullOrEmpty(all[i].BannerId));
                foreach (int c in new[] { all[i].ColorR, all[i].ColorG, all[i].ColorB }) Assert.That(c, Is.InRange(0, 255));
            }
            Assert.AreEqual(all.Count, all.Select(p => p.Name).Distinct().Count());
            Assert.AreEqual(all.Count, all.Select(p => p.BannerId).Distinct().Count());
            Assert.AreEqual(all.Count, all.Select(p => (p.ColorR, p.ColorG, p.ColorB)).Distinct().Count());
            Assert.AreEqual("Your people", all[PlayerIds.Human].Name);
            Assert.AreEqual("the Ash Clan", all[PlayerIds.Rival].Name);
            Assert.AreEqual("the Elves", all[PlayerIds.Elves].Name);
            Assert.AreEqual("the Orcs", all[PlayerIds.Orcs].Name);
            Assert.Throws<ArgumentOutOfRangeException>(() => PeopleDefs.Get(PlayerIds.MaxCount));
        }

        [Test]
        public void LegacyGame_HasTwoPeoples()
        {
            var s = WorldSimFactory.CreateNew(3).State;
            Assert.AreEqual(2, s.PeopleCount);
            Assert.AreEqual(2, s.Peoples.Count);
            Assert.AreEqual(2, s.Factions.Factions.Count);
            CollectionAssert.AreEqual(new[] { 70, 97 }, s.Board.StartCorners);
            Assert.AreEqual(0, s.Board.Ports.Count);
        }

        [Test]
        public void GodBoardGame_HasFourPeoples_InEverySystem()
        {
            var s = Four().State;
            Assert.AreEqual(4, s.PeopleCount);
            Assert.AreEqual(4, s.Players.Count);
            Assert.AreEqual(4, s.Peoples.Count);
            for (int id = 0; id < 4; id++)
            {
                Assert.AreEqual(id, s.Players[id].Id);
                Assert.AreEqual(id, s.Peoples[id].Id);
                Assert.AreEqual(s.Peoples[id].Name, s.Players[id].Name);
                Assert.AreEqual(s.Peoples[id].Name, s.Factions.Factions[id].Name);
                Assert.AreEqual(id != PlayerIds.Human, s.Factions.Factions[id].Auto);
            }
            Assert.AreEqual(4, s.Factions.Casts.Length);
            Assert.AreEqual(4, s.Factions.OpeningDone.Length);
            Assert.AreEqual(4, s.Ledger.Factions.Count);
            CollectionAssert.AreEqual(new[] { 70, 97, 57, 82 }, s.Board.StartCorners);
            // One active home camp per people on its start corner.
            for (int id = 0; id < 4; id++)
            {
                var st = EconomyApi.SettlementAt(s, s.Board.StartCorners[id]);
                Assert.IsNotNull(st, "people " + id + " has a home camp");
                Assert.AreEqual(id, st.Faction);
                Assert.AreEqual(SettlementState.Active, st.State);
            }
        }

        [Test]
        public void GodBoardGame_ValidatesEveryPeople_RefusesOthers()
        {
            var s = Four().State;
            Assert.IsTrue(EconomyLogic.ValidFaction(s, PlayerIds.Orcs));
            Assert.IsFalse(EconomyLogic.ValidFaction(s, 4));
            Assert.IsFalse(EconomyLogic.ValidFaction(s, -1));
            Assert.IsFalse(EconomyLogic.ValidFaction(WorldSimFactory.CreateNew(3).State, PlayerIds.Elves), "a legacy game has no Elves");
        }

        [Test]
        public void FourPeoplesOnLegacyRules_RunTenMinutes_AllPeoplesPlan()
        {
            // G0-2: the god board ruleset has no proposals or spells, so the N-people legacy rules are checked on a
            // four-people game that keeps the legacy ruleset.
            var setup = GameSetup.GodBoard;
            setup.Ruleset = Ruleset.Legacy;
            var sim = WorldSimFactory.CreateNew(5, setup);
            sim.Step(6000);
            sim.DrainEvents();
            var s = sim.State;
            for (int id = 1; id < 4; id++) Assert.Greater(s.Factions.Casts[id], 0, PeopleDefs.Name(s, id) + " cast spells");
            Assert.AreEqual(4, s.Factions.Factions.Count);
        }

        [Test]
        public void Names_ForEveryPeople()
        {
            var s = Four().State;
            StringAssert.StartsWith("the Elves' home camp", EconomyApi.SettlementName(s, EconomyApi.SettlementAt(s, 57).Id));
            StringAssert.StartsWith("the Orcs' home camp", EconomyApi.SettlementName(s, EconomyApi.SettlementAt(s, 82).Id));
            Assert.AreEqual("the Ash Clan's home camp", EconomyApi.SettlementName(s, EconomyApi.SettlementAt(s, 97).Id));
            Assert.AreEqual("the home camp", EconomyApi.SettlementName(s, EconomyApi.SettlementAt(s, 70).Id));
            string head = FactionApi.Headline(s);
            foreach (var p in s.Peoples) StringAssert.Contains(p.Name, head);
            // The legacy headline is unchanged.
            var l = WorldSimFactory.CreateNew(3).State;
            StringAssert.IsMatch(@"^Your people \d+, the Ash Clan \d+\.$", FactionApi.Headline(l));
        }

        [Test]
        public void StrongestOther_PicksAcrossAllPeoples()
        {
            var s = Four().State;
            var f = s.Factions;
            f.Factions[0].Strength = 3;
            f.Factions[1].Strength = 4;
            f.Factions[2].Strength = 9;
            f.Factions[3].Strength = 9;
            Assert.AreEqual(2, FactionLogic.StrongestOther(f, 0), "lowest id on a tie");
            Assert.AreEqual(3, FactionLogic.StrongestOther(f, 2));
            Assert.AreEqual(GameOutcome.Defeat, FactionLogic.OutcomeFor(PlayerIds.Orcs));
            Assert.AreEqual(GameOutcome.Victory, FactionLogic.OutcomeFor(PlayerIds.Human));
            Assert.AreEqual(GameOutcome.Draw, FactionLogic.OutcomeFor(-1));
        }

        [Test]
        public void Save_RoundTripsWithFourPeoples_ThenStaysInStep()
        {
            var a = Four(9);
            a.Step(1500);
            a.DrainEvents();
            string json = SaveCodec.ToJson(a.State);
            var b = SaveCodec.Load(json, out _);
            Assert.AreEqual(4, b.State.PeopleCount);
            Assert.AreEqual(4, b.State.Peoples.Count);
            Assert.AreEqual("the Orcs", b.State.Peoples[PlayerIds.Orcs].Name);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
            a.Step(1000);
            b.Step(1000);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State), "deterministic after load");
        }

        [Test]
        public void OldSave_WithoutPeoples_GetsThemFromItsPlayers()
        {
            var sim = WorldSimFactory.CreateNew(4);
            var o = JObject.Parse(SaveCodec.ToJson(sim.State));
            ((JObject)o["State"]).Remove("Peoples");
            var loaded = SaveCodec.Load(o.ToString(Newtonsoft.Json.Formatting.None), out _).State;
            Assert.AreEqual(2, loaded.Peoples.Count);
            Assert.AreEqual("Your people", loaded.Peoples[0].Name);
            Assert.AreEqual("the Ash Clan", loaded.Peoples[1].Name);
            Assert.AreEqual(SaveCodec.StateJson(sim.State), SaveCodec.StateJson(loaded));
        }

        [Test]
        public void Factory_RefusesBadPeopleCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldSimFactory.CreateNew(1, new GameSetup { Peoples = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldSimFactory.CreateNew(1, new GameSetup { Peoples = PlayerIds.MaxCount + 1 }));
        }

        [Test]
        public void SixPeoples_OnTheDealtBoard_TheExtraTwoHaveNoCamp()
        {
            var sim = WorldSimFactory.CreateNew(2, new GameSetup { Peoples = 6, Board = BoardKind.Dealt });
            var s = sim.State;
            Assert.AreEqual(6, s.PeopleCount);
            Assert.AreEqual(6, s.Factions.Factions.Count);
            Assert.AreEqual(6, s.Ledger.Factions.Count);
            Assert.IsFalse(s.Economy.Settlements.Any(st => st.Faction >= 4));
            sim.Step(600);
            sim.DrainEvents();
        }

        [Test]
        public void SimSource_NeverUsesTheOldFixedCount()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets", "World", "Sim"))) dir = Path.GetDirectoryName(dir);
            if (dir == null) Assert.Ignore("Sim source not found from " + TestContext.CurrentContext.TestDirectory);
            var bad = Directory.GetFiles(Path.Combine(dir, "Assets", "World", "Sim"), "*.cs", SearchOption.AllDirectories)
                .Where(p => File.ReadAllText(p).Contains("PlayerIds.Count"))
                .Select(Path.GetFileName).ToList();
            CollectionAssert.IsEmpty(bad, "Sim code uses WorldState.PeopleCount, not PlayerIds.Count");
        }
    }
}
