using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Factions;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>Save version 4: round trip of the whole state, determinism after load, older saves refused, hooks re-registered.</summary>
    public class SaveTests
    {
        private static WorldSim MidGame()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var top = FactionApi.Top(sim.State, PlayerIds.Human);
            Assert.IsTrue(sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = top.Id }).Ok);
            sim.Step(1234);
            sim.DrainEvents();
            return sim;
        }

        [Test]
        public void RoundTrip_EqualState_Then1000TicksEqual()
        {
            var a = MidGame();
            var s = a.State;
            var pose = new PlayerPose { X = 12.5, Y = 1.75, Z = -40.25, Yaw = 135, Pitch = -10 };
            var extras = new Dictionary<string, string> { { "walk.i", "3" }, { "hud", "0" } };
            string json = SaveCodec.ToJson(s, pose, extras);
            var model = SaveCodec.FromJson(json);
            Assert.AreEqual(4, SaveCodec.CurrentVersion);
            Assert.AreEqual(4, SaveCodec.OldestVersion);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.Version);
            Assert.AreEqual(s.Tick, model.Tick);
            Assert.AreEqual(135, model.Player.Yaw);
            Assert.AreEqual("3", model.Extras["walk.i"]);
            var b = SaveCodec.CreateSim(model.State);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State), "loaded state equals the saved one");

            var ea = new List<string>();
            var eb = new List<string>();
            for (int t = 0; t < 1000; t++)
            {
                a.Step();
                b.Step();
                ea.AddRange(a.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick));
                eb.AddRange(b.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick));
            }
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State), "both run 1,000 ticks to equal states");
            CollectionAssert.AreEqual(ea, eb);
            Assert.IsTrue(ea.Any(x => x.StartsWith(nameof(DiceRolled))), "the dice kept rolling");
        }

        [Test]
        public void RoundTrip_DemonHooksAreReRegistered()
        {
            var a = WorldSimFactory.CreateNew(3);
            int home = a.State.Board.StartCorners[0];
            a.State.Demons.Corner[home] = 1;
            var b = SaveCodec.Load(SaveCodec.ToJson(a.State), out _);
            Assert.IsTrue(EconomyApi.Hooks(b).SettlementHalted(b.State, 0), "demon-held home camp after load");
            Assert.IsTrue(EconomyApi.Hooks(b).CornerBlocked(b.State, home));
            b.Step(300);
            foreach (var bl in b.State.Economy.Buildings.Where(x => x.Settlement == 0))
                Assert.AreEqual(0, bl.WorkProgress, "collectors of a demon-held camp start no cycle");
        }

        [Test]
        public void FromJson_RejectsEmptyBrokenOlderAndNewerSaves()
        {
            Assert.Throws<FormatException>(() => SaveCodec.FromJson(""));
            Assert.Throws<FormatException>(() => SaveCodec.FromJson("{ not json"));
            Assert.Throws<FormatException>(() => SaveCodec.FromJson("{\"Version\":1}"));
            var sim = WorldSimFactory.CreateNew(1);
            string json = SaveCodec.ToJson(sim.State);
            Assert.Throws<FormatException>(() => SaveCodec.FromJson(json.Replace("\"Version\":4", "\"Version\":99")));
            var e = Assert.Throws<FormatException>(() => SaveCodec.FromJson(json.Replace("\"Version\":4", "\"Version\":3")));
            Assert.AreEqual("The save is from an older version (3) with a different world.", e.Message);
        }
    }
}
