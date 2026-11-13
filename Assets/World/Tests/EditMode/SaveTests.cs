using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Save;
using World.Sim.Strategy;
using K = World.Sim.Tests.StrategyTestKit;

namespace World.Sim.Tests
{
    /// <summary>G5 EditMode rows: save round trip, shipment in flight, drafts, squads and the infection deck survive.</summary>
    public class SaveTests
    {
        /// <summary>The same simple commands for both sims: first draft card, end the round at fixed ticks.</summary>
        private static void Drive(WorldSim sim, int t)
        {
            var s = sim.State;
            if (StrategyApi.DraftPending(s)) sim.Submit(new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[0] });
            else if (t % 400 == 399) sim.Submit(new EndRound());
            else if (t % 97 == 0 && StrategyApi.HeroMoveTargets(s).Count > 0) sim.Submit(new MoveHero { Vertex = StrategyApi.HeroMoveTargets(s)[0] });
        }

        /// <summary>Play the completionist script until a squad exists and a loaded carrier is mid-road.</summary>
        private static WorldSim MidGame()
        {
            var sim = WorldSimFactory.CreateNew(EconomyTestKit.DefaultSeed);
            var s = sim.State;
            var player = new StrategyPlayer(sim, completionist: true);
            for (int t = 0; t < 20000; t++)
            {
                player.Act();
                sim.Step();
                sim.DrainEvents();
                bool squad = MilitaryApi.Squads(s, PlayerIds.Human).Count > 0;
                bool inFlight = s.Economy.Carriers.Any(c => c.HasLoad && c.CurrentEdge >= 0 && c.Progress > 0);
                if (squad && inFlight && sim.PendingCommands.Count == 0 && !s.Strategy.GameOver) return sim;
            }
            Assert.Fail("no mid-game moment with a squad and a shipment in flight");
            return null;
        }

        [Test]
        public void RoundTrip_EqualState_Then1000TicksEqual_ShipmentArrivesOnce_SquadsSurvive()
        {
            var a = MidGame();
            var s = a.State;
            var pose = new PlayerPose { X = 12.5, Y = 1.75, Z = -40.25, Yaw = 135, Pitch = -10 };
            var extras = new Dictionary<string, string> { { "view", "god" }, { "camera.zoom", "0.6" } };
            string json = SaveCodec.ToJson(s, pose, extras);
            var model = SaveCodec.FromJson(json);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.Version);
            Assert.AreEqual(s.Tick, model.Tick);
            Assert.AreEqual(135, model.Player.Yaw);
            Assert.AreEqual(-40.25, model.Player.Z);
            Assert.AreEqual("god", model.Extras["view"]);
            var b = SaveCodec.CreateSim(model.State);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State), "loaded state equals the saved one");

            var carrier = s.Economy.Carriers.First(c => c.HasLoad && c.CurrentEdge >= 0);
            int jobInFlight = carrier.JobId;
            int squads = MilitaryApi.Squads(s, PlayerIds.Human).Count;
            Assert.Greater(squads, 0);
            Assert.AreEqual(squads, MilitaryApi.Squads(b.State, PlayerIds.Human).Count);

            var da = new List<int>();
            var db = new List<int>();
            for (int t = 0; t < 1000; t++)
            {
                Drive(a, t);
                Drive(b, t);
                a.Step();
                b.Step();
                da.AddRange(a.DrainEvents().OfType<WareDelivered>().Select(d => d.JobId));
                db.AddRange(b.DrainEvents().OfType<WareDelivered>().Select(d => d.JobId));
                Assert.IsNull(K.CheckInvariants(b.State), "tick " + b.State.Tick);
            }
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State), "both run 1,000 ticks to equal states");
            CollectionAssert.AreEqual(da, db);
            Assert.AreEqual(1, db.Count(j => j == jobInFlight), "the shipment in flight at save arrives exactly once after load");
        }

        [Test]
        public void RoundTrip_PerRoundDraftAndHarvestSeason_Survive()
        {
            var a = K.NewSim(ticksPerEdge: 60, minRoundTicks: 600);
            var s = a.State;
            K.Step(a);
            K.Do(a, new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[0] }); // pick 1 made; pick 2 waits for the next round
            K.Step(a, 100);
            Assert.AreEqual(0, s.Strategy.DraftStep);
            Assert.AreEqual(2, s.Strategy.DraftNextStep);
            long left = EconomyApi.BellTicksLeft(s);
            Assert.Greater(left, 0);
            var b = SaveCodec.Load(SaveCodec.ToJson(s), out var model);
            var t = b.State;
            Assert.AreEqual(3, model.Version);
            Assert.AreEqual(2, t.Strategy.DraftNextStep, "the passed hand waits for the next round after load");
            CollectionAssert.AreEqual(s.Strategy.Sides[0].DraftHand, t.Strategy.Sides[0].DraftHand);
            Assert.AreEqual(s.Economy.RoundStartedTick, t.Economy.RoundStartedTick);
            Assert.AreEqual(left, EconomyApi.BellTicksLeft(t), "the harvest season continues where it was");
            Assert.AreEqual(EconomyRefusals.RoundTooSoon, b.Check(new EndRound { PlayerId = PlayerIds.Human }).Reason);
            K.Step(a, (int)left);
            K.Step(b, (int)left);
            var ea = K.Do(a, new EndRound());
            var eb = K.Do(b, new EndRound());
            Assert.AreEqual(2, ea.OfType<DraftResumed>().Single().Step);
            Assert.AreEqual(2, eb.OfType<DraftResumed>().Single().Step);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
        }

        [Test]
        public void RoundTrip_DraftInProgress_InfectionDeckAndHooksSurvive()
        {
            var a = K.NewSim(ticksPerEdge: 60);
            var s = a.State;
            s.Strategy.Tuning.DraftPickPerRound = false; // both picks at the age start: the second is pending at save (per-round: RoundTrip_PerRoundDraft...)
            K.Step(a);
            K.Do(a, new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[0] }); // first pick made, second pending
            s.Strategy.Demons[K.DemoCorner(s)] = 2;
            Assert.AreEqual(2, s.Strategy.DraftStep);
            var b = SaveCodec.Load(SaveCodec.ToJson(s), out var model);
            var t = b.State;
            Assert.IsNull(model.Player);
            Assert.AreEqual(2, t.Strategy.DraftStep, "draft in progress survives");
            CollectionAssert.AreEqual(s.Strategy.Sides[0].DraftHand, t.Strategy.Sides[0].DraftHand);
            CollectionAssert.AreEqual(s.Strategy.InfectionDeck, t.Strategy.InfectionDeck, "infection deck order survives");
            Assert.AreEqual(s.Strategy.InfectionRng.State, t.Strategy.InfectionRng.State);

            // Hooks are re-registered: EndRound is refused during the draft, demons block flags.
            Assert.AreEqual(StrategyRefusals.DraftPending, b.Submit(new EndRound()).Reason);
            Assert.IsTrue(EconomyApi.Hooks(b).IsVertexBlocked.Any(f => f(t, K.DemoCorner(s))));
            Assert.AreEqual(StrategyRefusals.TooClose, b.Check(new FoundCity { Vertex = K.DemoCorner(s) }).Reason);

            // Both finish the draft and end three rounds identically (infection draws from the restored deck).
            var ea = new List<SimEvent>();
            var eb = new List<SimEvent>();
            for (int i = 0; i < 3; i++)
            {
                ea.AddRange(K.EndRound(a));
                eb.AddRange(K.EndRound(b));
            }
            CollectionAssert.AreEqual(ea.OfType<DemonSpawned>().Select(d => d.Vertex), eb.OfType<DemonSpawned>().Select(d => d.Vertex));
            CollectionAssert.AreEqual(ea.OfType<DraftPicked>().Select(d => d.CardId), eb.OfType<DraftPicked>().Select(d => d.CardId));
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
        }

        [Test]
        public void RoundTrip_PowerPredicateIsReRegistered()
        {
            var a = K.NewSim();
            var s = a.State;
            K.Step(a);
            K.FinishDraft(a);
            K.Give(s, Ware.Planks, 20, Ware.Bricks, 10, Ware.Rations, 1, Ware.Weapons, 1);
            int barracks = K.Build(a, BuildingType.Barracks);
            K.Do(a, new QueueUnit { BarracksId = barracks });
            K.Step(a, 200);
            Assert.AreEqual(Reasons.NoPower, s.Military.Queues[0].Reason, "no power plant");
            var b = SaveCodec.Load(SaveCodec.ToJson(s), out _);
            K.Step(b, 50);
            Assert.AreEqual(Reasons.NoPower, b.State.Military.Queues[0].Reason);
            Assert.AreEqual(0, b.State.Military.Queues[0].Orders[0].Progress);
            Assert.IsFalse(EconomyApi.Hooks(b).IsPowered(b.State, barracks));
        }

        [Test]
        public void FromJson_RejectsEmptyBrokenAndNewerSaves()
        {
            Assert.Throws<FormatException>(() => SaveCodec.FromJson(""));
            Assert.Throws<FormatException>(() => SaveCodec.FromJson("{ not json"));
            Assert.Throws<FormatException>(() => SaveCodec.FromJson("{\"Version\":1}"));
            var sim = K.NewSim();
            string json = SaveCodec.ToJson(sim.State).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":99");
            Assert.Throws<FormatException>(() => SaveCodec.FromJson(json));
        }
    }
}
