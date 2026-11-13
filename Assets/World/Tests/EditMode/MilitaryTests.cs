using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;
using K = World.Sim.Tests.StrategyTestKit;

namespace World.Sim.Tests
{
    /// <summary>G4 military rows: power, barracks supply, squads, combat, outpost and raids.</summary>
    public class MilitaryTests
    {
        private static void StepUntil(WorldSim sim, Func<bool> done, int max, List<SimEvent> sink = null)
        {
            for (int i = 0; i < max && !done(); i++)
            {
                sim.Step();
                var e = sim.DrainEvents();
                sink?.AddRange(e);
            }
            Assert.IsTrue(done(), "condition not reached in " + max + " ticks");
        }

        /// <summary>Age I game with a fuelled power plant and an active barracks; storehouse emptied of rations and ingots.</summary>
        private static WorldSim Camp(out int plant, out int barracks, ulong seed = EconomyTestKit.DefaultSeed)
        {
            var sim = K.NewSim(seed);
            var s = sim.State;
            s.Economy.Tuning.Buildings[(int)BuildingType.PowerPlant].FuelTicks = 100;
            K.Step(sim);
            K.FinishDraft(sim);
            K.Give(s, Ware.Planks, 30, Ware.Stone, 20, Ware.Timber, 4);
            plant = K.Build(sim, BuildingType.PowerPlant);
            barracks = K.Build(sim, BuildingType.Barracks);
            int p = plant;
            StepUntil(sim, () => EconomyApi.IsFuelled(s, p), 500);
            return sim;
        }

        private static QueuedUnit Head(WorldState s, int barracks) => s.Military.Queues.Single(q => q.BuildingId == barracks).Orders[0];

        [Test]
        public void Power_ShortageStallsTheQueueWithNoPower_RestoringFuelResumesFromTheSameProgress()
        {
            var sim = Camp(out int plant, out int barracks);
            var s = sim.State;
            K.Give(s, Ware.Rations, 2, Ware.Ingots, 1);
            K.Do(sim, new QueueUnit { BarracksId = barracks });
            StepUntil(sim, () => Head(s, barracks).Progress >= 20, 400);
            var power = MilitaryApi.Power(s);
            Assert.AreEqual(4, power.Supply);
            Assert.AreEqual(2, power.Demand);

            // Burn out the fuel: take the plant's timber and keep the storehouse dry.
            var evs = new List<SimEvent>();
            EconomyApi.TrySpendFromStorehouse(s, Ware.Timber, EconomyApi.StorehouseCount(s, Ware.Timber));
            StepUntil(sim, () => s.Economy.Buildings[plant].Incoming[(int)Ware.Timber] == 0, 300, evs);
            EconomyApi.TryConsume(s, plant, Ware.Timber, EconomyApi.Available(s, plant, Ware.Timber));
            StepUntil(sim, () => !EconomyApi.IsFuelled(s, plant) && Head(s, barracks).Progress > 0, 300, evs);
            K.Step(sim, 2);
            int frozen = Head(s, barracks).Progress;
            Assert.Greater(frozen, 0);
            Assert.Less(frozen, MilitaryApi.TrainingTicks(s), "stalled mid-training");
            for (int i = 0; i < 60; i++)
            {
                int pr = Head(s, barracks).Progress;
                evs.AddRange(K.Step(sim));
                if (Head(s, barracks).Progress != pr)
                    Assert.Fail("progressed without power at tick " + s.Tick);
            }
            Assert.AreEqual(frozen, Head(s, barracks).Progress, "no progress without power");
            var info = MilitaryApi.DescribeBarracks(s, barracks);
            Assert.AreEqual(Reasons.NoPower, info.Reason);
            Assert.AreEqual("The barracks has no power.", info.Status);
            Assert.AreEqual(Reasons.NoPower, s.Economy.Buildings[barracks].Reason);
            Assert.IsTrue(evs.OfType<PowerChanged>().Any(p => p.BuildingId == barracks && !p.Powered));
            Assert.IsTrue(evs.OfType<QueueStateChanged>().Any(q => q.Reason == Reasons.NoPower));
            power = MilitaryApi.Power(s);
            Assert.AreEqual(0, power.Supply);
            StringAssert.Contains("Barracks has no power", power.Sentence);

            // Restore fuel: timber is carried to the plant, it burns, the queue continues from the same progress.
            K.Give(s, Ware.Timber, 2);
            StepUntil(sim, () => EconomyApi.IsFuelled(s, plant), 300);
            // The power system runs before the barracks queue, so the tick the plant relights already counts.
            Assert.AreEqual(frozen + 1, Head(s, barracks).Progress, "resumes from the same progress");
            var after = new List<SimEvent>();
            StepUntil(sim, () => s.Military.UnitsTrained == 1, 400, after);
            Assert.AreEqual(1, after.OfType<UnitTrained>().Count());
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Power_LowestPriorityConsumerLosesPowerFirst()
        {
            var sim = Camp(out int plant, out int b1);
            var s = sim.State;
            K.Give(s, Ware.Planks, 20, Ware.Stone, 20, Ware.Rations, 20, Ware.Ingots, 10);
            int b2 = K.Build(sim, BuildingType.Barracks);
            int refinery = K.Build(sim, BuildingType.Refinery);
            K.Do(sim, new QueueUnit { BarracksId = b1, Count = 2 });
            K.Do(sim, new QueueUnit { BarracksId = b2, Count = 2 });
            StepUntil(sim, () => EconomyApi.IsFuelled(s, plant), 300);
            var p = MilitaryApi.Power(s);
            Assert.AreEqual(4, p.Supply);
            Assert.AreEqual(6, p.Demand);
            CollectionAssert.AreEqual(new[] { b1, b2, refinery }, p.Consumers.Select(c => c.BuildingId).ToArray());
            CollectionAssert.AreEqual(new[] { true, true, false }, p.Consumers.Select(c => c.Powered).ToArray());
            K.Do(sim, new SetPriority { BuildingId = refinery, Priority = Priority.High });
            p = MilitaryApi.Power(s);
            CollectionAssert.AreEqual(new[] { refinery, b1, b2 }, p.Consumers.Select(c => c.BuildingId).ToArray());
            Assert.IsFalse(MilitaryApi.IsPowered(s, b2), "now the second barracks is last and loses power");
            Assert.IsTrue(MilitaryApi.IsPowered(s, refinery));
        }

        [Test]
        public void Barracks_SuppliesMustArriveBeforeTraining_MissingWareNamed()
        {
            var sim = Camp(out _, out int barracks);
            var s = sim.State;
            Assert.AreEqual(MilitaryRefusals.NoBarracks, sim.Check(new QueueUnit { BarracksId = s.Economy.StorehouseId }).Reason);
            K.Do(sim, new QueueUnit { BarracksId = barracks });
            K.Step(sim, 100);
            var info = MilitaryApi.DescribeBarracks(s, barracks);
            Assert.AreEqual(Reasons.WaitingWare, info.Reason);
            CollectionAssert.AreEqual(new[] { "2 rations", "1 ingots" }, info.WaitingFor);
            Assert.AreEqual("The barracks is waiting for 2 rations and 1 ingots.", info.Status);
            Assert.AreEqual(0, Head(s, barracks).Progress);

            // Rations in the storehouse are not enough: they must be carried to the barracks flag.
            K.Give(s, Ware.Rations, 2);
            var evs = new List<SimEvent>();
            StepUntil(sim, () => s.Economy.Buildings[barracks].Stock[(int)Ware.Rations] == 2, 300, evs);
            Assert.IsTrue(evs.OfType<WareDelivered>().Count(d => d.BuildingId == barracks && d.Ware == Ware.Rations) == 2);
            K.Step(sim, 5);
            info = MilitaryApi.DescribeBarracks(s, barracks);
            CollectionAssert.AreEqual(new[] { "1 ingots" }, info.WaitingFor);
            Assert.AreEqual((int)Ware.Ingots, s.Military.Queues[0].ReasonWare);
            Assert.IsFalse(Head(s, barracks).Supplied);

            K.Give(s, Ware.Ingots, 1);
            evs.Clear();
            StepUntil(sim, () => s.Military.UnitsTrained == 1, 500, evs);
            long delivered = evs.OfType<WareDelivered>().Single(d => d.BuildingId == barracks && d.Ware == Ware.Ingots).Tick;
            long started = evs.OfType<TrainingStarted>().Single().Tick;
            long trained = evs.OfType<UnitTrained>().Single().Tick;
            Assert.GreaterOrEqual(started, delivered, "training starts only after the ingots arrive");
            Assert.AreEqual(MilitaryApi.TrainingTicks(s) - 1, trained - started);
            Assert.AreEqual(2, s.Military.ConsumedForUnits[(int)Ware.Rations]);
            Assert.AreEqual(0, s.Economy.Buildings[barracks].Stock[(int)Ware.Rations], "consumed from the barracks store");
            Assert.AreEqual(1, MilitaryApi.Squads(s, PlayerIds.Human).Single().Units);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Cards_DrillYardAndMessHallChangeTraining()
        {
            var sim = Camp(out _, out int barracks);
            var s = sim.State;
            Assert.AreEqual(150, MilitaryApi.TrainingTicks(s));
            K.ForceIntoHand(s, "drill_yard");
            K.ForceIntoHand(s, "mess_hall");
            K.Give(s, Ware.Planks, 4, Ware.Stone, 3);
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "drill_yard"), CityVertex = s.Board.HomeCityVertex });
            K.Do(sim, new BuildCard { CardId = K.CardOf(s, "mess_hall"), CityVertex = s.Board.HomeCityVertex });
            Assert.AreEqual(90, MilitaryApi.TrainingTicks(s));
            K.Do(sim, new QueueUnit { BarracksId = barracks });
            Assert.AreEqual(1, Head(s, barracks).Rations);
        }

        /// <summary>Train a full squad, order it to attack the outpost, run to the outcome. Returns the event log.</summary>
        private static List<SimEvent> AttackOutpost(ulong seed, out WorldSim sim)
        {
            sim = Camp(out _, out int barracks, seed);
            var s = sim.State;
            K.Give(s, Ware.Rations, 8, Ware.Ingots, 4, Ware.Timber, 12);
            K.Do(sim, new QueueUnit { BarracksId = barracks, Count = 4 });
            var w = sim;
            StepUntil(sim, () => w.State.Military.UnitsTrained == 4, 2000);
            var squad = MilitaryApi.Squads(s, PlayerIds.Human).Single();
            Assert.AreEqual(4, squad.Units, "one squad per barracks, up to 4 units");

            int outpost = s.Board.RivalOutpostVertex;
            var path = s.Board.Topology.FindPath(squad.At, outpost);
            s.Strategy.Demons[path[2]] = 1; // a demon on the way is cleared by the squad
            var log = new List<SimEvent>();
            log.AddRange(K.Do(sim, new OrderSquad { SquadId = squad.Id, Order = OrderKind.Attack, Target = outpost }));
            for (int i = 0; i < 3000 && !(w.State.Military.Outpost.Destroyed || !w.State.Military.Squads[squad.Id].Alive); i++)
            {
                sim.Step();
                log.AddRange(sim.DrainEvents());
            }
            var sv = MilitaryApi.DescribeSquad(s, squad.Id);
            Assert.IsTrue(s.Military.Outpost.Destroyed || !s.Military.Squads[squad.Id].Alive,
                $"squad at {sv.At} moving {sv.Moving} engaged {sv.Engaged} order {sv.Order} target {sv.Target} units {sv.Units} outpost {s.Military.Outpost.Hp} path {string.Join(",", path)}");
            return log;
        }

        private static string Signature(IEnumerable<SimEvent> evs)
        {
            var sb = new StringBuilder();
            foreach (var e in evs)
            {
                switch (e)
                {
                    case UnitHit h: sb.Append(h.Tick).Append(" hit ").Append(h.AttackerUnitId).Append('>').Append(h.TargetUnitId).Append(' ').Append(h.TargetHpLeft).Append('\n'); break;
                    case UnitDied d: sb.Append(d.Tick).Append(" died ").Append(d.UnitId).Append('\n'); break;
                    case SquadMoved m: sb.Append(m.Tick).Append(" move ").Append(m.From).Append('>').Append(m.To).Append('\n'); break;
                    case OutpostDestroyed o: sb.Append(o.Tick).Append(" outpost down\n"); break;
                }
            }
            return sb.ToString();
        }

        [Test]
        public void Squad_AttacksTheOutpost_OnFixedTicks_Deterministically_AndScoresTwo()
        {
            var log = AttackOutpost(EconomyTestKit.DefaultSeed, out var sim);
            var s = sim.State;
            var log2 = AttackOutpost(EconomyTestKit.DefaultSeed, out _);
            Assert.AreEqual(Signature(log), Signature(log2), "same seed, same fight");

            Assert.Greater(log.OfType<SquadMoved>().Count(), 3, "the squad walks vertex to vertex");
            Assert.AreEqual(1, log.OfType<DemonsCleared>().Count(d => d.By == "squad"));
            Assert.IsTrue(log.OfType<SquadEngaged>().Any());
            var hits = log.OfType<UnitHit>().ToList();
            Assert.IsNotEmpty(hits);
            foreach (var g in hits.GroupBy(h => h.AttackerUnitId))
            {
                var ticks = g.Select(h => h.Tick).Distinct().OrderBy(t => t).ToList();
                for (int i = 1; i < ticks.Count; i++)
                    Assert.AreEqual(0, (ticks[i] - ticks[0]) % s.Military.Tuning.AttackCooldown, "attacks on fixed ticks");
            }
            Assert.IsTrue(s.Military.Outpost.Destroyed, "4 Wardens beat 2 guards and the outpost");
            Assert.AreEqual(1, log.OfType<OutpostDestroyed>().Count());
            Assert.AreEqual(2, StrategyApi.Score(s, 0).Outpost);
            Assert.AreEqual(4, StrategyApi.Score(s, 0).Total);
            Assert.IsTrue(MilitaryApi.Outpost(s).Destroyed);
            Assert.IsNull(K.CheckInvariants(s));
            TestContext.WriteLine("attack: " + hits.Count + " hits, survivors " + MilitaryApi.Squads(s, 0).Sum(q => q.Units) + ", ended tick " + s.Tick);
        }

        [Test]
        public void Orders_Refusals_DefendAndCancel()
        {
            var sim = Camp(out _, out int barracks);
            var s = sim.State;
            Assert.AreEqual(MilitaryRefusals.UnknownSquad,
                sim.Check(new OrderSquad { SquadId = s.Military.Outpost.GarrisonSquadId, Order = OrderKind.Attack, Target = 0 }).Reason);
            K.Give(s, Ware.Rations, 2, Ware.Ingots, 1);
            K.Do(sim, new QueueUnit { BarracksId = barracks });
            StepUntil(sim, () => s.Military.UnitsTrained == 1, 1000);
            int id = MilitaryApi.Squads(s, 0).Single().Id;
            Assert.AreEqual(MilitaryRefusals.BadOrder, sim.Check(new OrderSquad { SquadId = id, Order = OrderKind.Return, Target = 0 }).Reason);
            Assert.AreEqual(MilitaryRefusals.NoOrder, sim.Check(new CancelOrder { SquadId = id }).Reason);
            int home = s.Board.HomeCityVertex;
            K.Do(sim, new OrderSquad { SquadId = id, Order = OrderKind.Defend, Target = home });
            StepUntil(sim, () => MilitaryApi.DescribeSquad(s, id).At == home && !MilitaryApi.DescribeSquad(s, id).Moving, 500);
            Assert.AreEqual("Defending its flag.", MilitaryApi.DescribeSquad(s, id).OrderText);
            K.Do(sim, new OrderSquad { SquadId = id, Order = OrderKind.Attack, Target = s.Board.RivalOutpostVertex });
            K.Step(sim, 3);
            Assert.IsTrue(MilitaryApi.DescribeSquad(s, id).Moving);
            K.Do(sim, new CancelOrder { SquadId = id });
            K.Step(sim, s.Military.Tuning.TicksPerEdge + 2);
            var v = MilitaryApi.DescribeSquad(s, id);
            Assert.IsFalse(v.Moving, "stops at the next flag");
            Assert.AreEqual(1, s.Board.Topology.Distance(home, v.At));
        }

        [Test]
        public void Rival_RaidsFromAgeTwo_OccupyAFlag_AndLeaveAfterARound()
        {
            var sim = K.NewSim();
            var s = sim.State;
            K.Step(sim);
            K.EndRound(sim);
            Assert.IsFalse(s.Military.Squads.Any(q => q.Kind == SquadKind.Raid), "no raids in Age I");
            K.EndRound(sim);
            Assert.AreEqual(2, s.Strategy.Age);
            Assert.AreEqual(2, MilitaryApi.Outpost(s).Guards);
            var evs = K.EndRound(sim);
            Assert.AreEqual(3, MilitaryApi.Outpost(s).Guards, "garrison +1 per age");
            var launched = evs.OfType<RaidLaunched>().Single();
            Assert.AreEqual(MilitaryLogicTarget(s), launched.Target, "nearest flag on the player's roads");
            var occ = new List<SimEvent>();
            StepUntil(sim, () => MilitaryApi.RaidedFlags(s).Count > 0, 2000, occ);
            Assert.AreEqual(launched.Target, MilitaryApi.RaidedFlags(s).Single());
            Assert.IsTrue(EconomyApi.Hooks(sim).IsVertexBlocked.Any(f => f(s, launched.Target)), "the raid blocks the flag like a demon");

            var end = K.EndRound(sim);
            Assert.IsTrue(end.OfType<RaidEnded>().Any(r => r.SquadId == launched.SquadId && r.How == "leaving"));
            Assert.IsFalse(MilitaryApi.RaidedFlags(s).Contains(launched.Target) && s.Military.Squads[launched.SquadId].Occupying);
            var home = new List<SimEvent>();
            StepUntil(sim, () => !s.Military.Squads[launched.SquadId].Alive || s.Military.Squads[launched.SquadId].Kind == SquadKind.Garrison, 2000, home);
            Assert.IsTrue(home.OfType<RaidEnded>().Any(r => r.SquadId == launched.SquadId && r.How == "merged"));
            Assert.IsNull(K.CheckInvariants(s));
        }

        private static int MilitaryLogicTarget(WorldState s)
        {
            var topo = s.Board.Topology;
            return Enumerable.Range(0, topo.VertexCount)
                .Where(v => v != s.Board.StorehouseVertex && topo.VertexEdges(v).Any(e => s.Economy.Roads[e]))
                .OrderBy(v => topo.Distance(s.Board.RivalOutpostVertex, v)).ThenBy(v => v).First();
        }
    }
}
