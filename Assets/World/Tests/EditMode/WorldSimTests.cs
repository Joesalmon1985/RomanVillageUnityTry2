using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace World.Sim.Tests
{
    public class WorldSimTests
    {
        private sealed class AddCoin : Command
        {
            public int Amount;
        }

        private sealed class CoinAdded : SimEvent
        {
            public int Amount;
        }

        private sealed class TickSeen : SimEvent
        {
            public string Name;
            public int CoinAtTick;
        }

        private sealed class AddCoinHandler : CommandHandler<AddCoin>
        {
            protected override CommandResult Validate(WorldState s, AddCoin c)
            {
                if (c.Amount <= 0) return CommandResult.Reject(Reasons.InvalidSite, "Amount must be positive.");
                if (s.Player(c.PlayerId).Coin + c.Amount > 10) return CommandResult.RejectDefault(Reasons.OutputFull, "The purse");
                return CommandResult.Accept();
            }

            protected override CommandResult Apply(WorldState s, AddCoin c, EventBuffer ev)
            {
                s.Player(c.PlayerId).Coin += c.Amount;
                ev.Add(new CoinAdded { Amount = c.Amount });
                return CommandResult.Accept();
            }
        }

        private sealed class Recorder : ISimSystem
        {
            public string Name;
            public void Tick(WorldState s, EventBuffer ev) =>
                ev.Add(new TickSeen { Name = Name, CoinAtTick = s.Player(PlayerIds.Human).Coin });
        }

        private static WorldSim NewSim()
        {
            var sim = WorldSimFactory.CreateNew(1234);
            sim.RegisterHandler(new AddCoinHandler());
            return sim;
        }

        [Test]
        public void Factory_BuildsBoardPlayersAndSeed()
        {
            var sim = WorldSimFactory.CreateNew(99);
            Assert.AreEqual(99UL, sim.State.Seed);
            Assert.AreEqual(0, sim.State.Tick);
            Assert.IsNotNull(sim.State.Board);
            Assert.AreEqual(570, sim.State.Board.Topology.VertexCount);
            Assert.AreEqual(PlayerIds.Count, sim.State.Players.Count);
            Assert.AreEqual(10, WorldSim.TicksPerSecond);
        }

        [Test]
        public void Submit_ValidatesNow_AppliesAtNextTick()
        {
            var sim = NewSim();
            var r = sim.Submit(new AddCoin { PlayerId = PlayerIds.Human, Amount = 3 });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, sim.State.Player(0).Coin, "not applied before the tick boundary");
            Assert.AreEqual(1, sim.PendingCommands.Count);

            sim.Step();
            Assert.AreEqual(1, sim.State.Tick);
            Assert.AreEqual(3, sim.State.Player(0).Coin);
            Assert.AreEqual(0, sim.PendingCommands.Count);
            var evs = sim.DrainEvents();
            Assert.IsTrue(evs.OfType<CoinAdded>().Any(e => e.Tick == 1));
            Assert.AreEqual(1, evs.OfType<CommandApplied>().Count());
            Assert.AreEqual(0, sim.DrainEvents().Count, "drain clears");
        }

        [Test]
        public void Submit_RejectsWithReasonAndSentence()
        {
            var sim = NewSim();
            var r = sim.Submit(new AddCoin { Amount = 0 });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(Reasons.InvalidSite, r.Reason);
            Assert.AreEqual("Amount must be positive.", r.Message);
            Assert.AreEqual(0, sim.PendingCommands.Count);

            var r2 = sim.Submit(new AddCoin { Amount = 11 });
            Assert.AreEqual(Reasons.OutputFull, r2.Reason);
            Assert.AreEqual("The purse has no room for its output.", r2.Message);
        }

        [Test]
        public void UnknownCommand_IsRefused()
        {
            var sim = WorldSimFactory.CreateNew(1);
            var r = sim.Submit(new AddCoin { Amount = 1 });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(Reasons.Disabled, r.Reason);
        }

        [Test]
        public void QueuedCommands_ApplyInOrder_AndAreRecheckedAtBoundary()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.Submit(new AddCoin { Amount = 6 }).Ok);
            Assert.IsTrue(sim.Submit(new AddCoin { Amount = 6 }).Ok, "valid against current state");
            sim.Step();
            Assert.AreEqual(6, sim.State.Player(0).Coin, "second one fails the re-check");
            var evs = sim.DrainEvents();
            var rejected = evs.OfType<CommandRejected>().Single();
            Assert.AreEqual(Reasons.OutputFull, rejected.Result.Reason);
        }

        [Test]
        public void Systems_RunInOrderSlots_AfterCommands()
        {
            var sim = NewSim();
            sim.AddSystem(SystemOrder.Diagnostics, new Recorder { Name = "diag" });
            sim.AddSystem(SystemOrder.Rounds, new Recorder { Name = "rounds" });
            sim.AddSystem(SystemOrder.Carriers, new Recorder { Name = "carriers1" });
            sim.AddSystem(SystemOrder.Carriers, new Recorder { Name = "carriers2" });
            sim.Submit(new AddCoin { Amount = 2 });
            sim.Step();
            var seen = sim.DrainEvents().OfType<TickSeen>().ToList();
            CollectionAssert.AreEqual(new[] { "rounds", "carriers1", "carriers2", "diag" }, seen.Select(s => s.Name).ToArray());
            Assert.IsTrue(seen.All(s => s.CoinAtTick == 2), "commands apply before systems");
            sim.Step(9);
            Assert.AreEqual(10, sim.State.Tick);
        }

        [Test]
        public void DuplicateHandler_Throws()
        {
            var sim = NewSim();
            Assert.Throws<System.InvalidOperationException>(() => sim.RegisterHandler(new AddCoinHandler()));
        }

        [Test]
        public void WorldState_RoundTripsThroughJson()
        {
            var sim = WorldSimFactory.CreateNew(555);
            sim.Step(5);
            sim.State.Rng.NextULong();
            var json = JsonConvert.SerializeObject(sim.State);
            var copy = JsonConvert.DeserializeObject<WorldState>(json);
            Assert.AreEqual(sim.State.Tick, copy.Tick);
            Assert.AreEqual(sim.State.Seed, copy.Seed);
            Assert.AreEqual(sim.State.Rng.State, copy.Rng.State);
            CollectionAssert.AreEqual(sim.State.Board.HexNumber, copy.Board.HexNumber);
            CollectionAssert.AreEqual(sim.State.Board.StartCorners, copy.Board.StartCorners);
            CollectionAssert.AreEqual(sim.State.Board.ReservedPlots, copy.Board.ReservedPlots);
            Assert.AreEqual(630, copy.Board.Topology.EdgeCount);
            Assert.AreEqual(json, JsonConvert.SerializeObject(copy));
        }

        [Test]
        public void ReasonCodes_ClosedListHasSentences()
        {
            Assert.AreEqual(11, Reasons.All.Length);
            foreach (var code in Reasons.All)
            {
                Assert.IsTrue(Reasons.IsKnown(code));
                StringAssert.EndsWith(".", Reasons.Sentence(code));
                StringAssert.EndsWith(".", Reasons.Sentence(code, "Sawmill", "timber"));
            }
            Assert.AreEqual("Sawmill is waiting for timber.", Reasons.Sentence(Reasons.WaitingWare, "Sawmill", "timber"));
            Assert.AreEqual("Log pile is waiting for forest stock to grow.", Reasons.Sentence(Reasons.NoBatch, "Log pile", "forest"));
            Assert.IsFalse(Reasons.IsKnown("nope"));
        }
    }
}
