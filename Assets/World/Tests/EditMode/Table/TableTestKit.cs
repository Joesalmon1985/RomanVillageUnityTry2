using System;
using System.Collections.Generic;
using NUnit.Framework;
using World.Sim.Table;

namespace World.Sim.Tests
{
    /// <summary>Helpers for the Table (G1-1) tests: a bare sim with only the board and the table module.</summary>
    public static class TableTestKit
    {
        public const ulong Seed = 20261119;

        /// <summary>The four fixed first camps (PE-2): home, Ash Clan, Elves, Orcs.</summary>
        public static readonly int[] StartCorners = { 70, 97, 57, 82 };

        public static WorldSim Create(int people = 4, int humanSeat = 0, ulong seed = Seed, TablePort[] ports = null,
            Action<TableSeams> wire = null, Action<TableTuning> tune = null)
        {
            var s = new WorldState { Seed = seed, Rng = new Rng(seed), Board = BoardGenerator.CreateMvpBoard(seed) };
            var corners = new int[people];
            Array.Copy(StartCorners, corners, Math.Min(people, StartCorners.Length));
            TableModule.InitState(s, people, corners, humanSeat, ports);
            tune?.Invoke(s.Table.Tuning);
            var sim = new WorldSim(s);
            var seams = TableModule.Register(sim);
            wire?.Invoke(seams);
            return sim;
        }

        /// <summary>AI seats that never act (their turns just run out), so tests control every card.</summary>
        public static void IdleAi(TableSeams seams) => seams.AiTurn = (s, p, a) => { };

        public static TableState T(WorldSim sim) => sim.State.Table;

        public static CommandResult Submit(WorldSim sim, Command c, int people = 0)
        {
            c.PlayerId = people;
            return sim.Submit(c);
        }

        /// <summary>Submit (must be accepted), step one tick, return that tick's events.</summary>
        public static List<SimEvent> Do(WorldSim sim, Command c, int people = 0)
        {
            var r = Submit(sim, c, people);
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            var evs = new List<SimEvent>(sim.DrainEvents());
            foreach (var e in evs) if (e is CommandRejected rej) Assert.Fail("rejected at apply: " + rej.Result);
            return evs;
        }

        public static void Refused(WorldSim sim, Command c, string code, int people = 0)
        {
            var r = Submit(sim, c, people);
            Assert.IsFalse(r.Ok, "expected " + code);
            Assert.AreEqual(code, r.Reason, r.ToString());
            Assert.IsFalse(string.IsNullOrEmpty(r.Message));
        }

        public static List<SimEvent> Step(WorldSim sim, int ticks)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
            }
            return all;
        }

        public static bool StepUntil(WorldSim sim, Func<bool> done, int max, List<SimEvent> log = null)
        {
            for (int i = 0; i < max; i++)
            {
                if (done()) return true;
                sim.Step();
                var evs = sim.DrainEvents();
                log?.AddRange(evs);
            }
            return done();
        }

        /// <summary>Makes the next hidden roll come out as total (searches a dice stream that starts with it).</summary>
        public static void ForceNextRoll(TableState t, int total)
        {
            for (ulong k = 1; k < 100000; k++)
            {
                var r = new Rng(k);
                if (r.NextRange(1, 7) + r.NextRange(1, 7) == total)
                {
                    t.RollRng = new Rng(k);
                    return;
                }
            }
            Assert.Fail("no stream for " + total);
        }

        /// <summary>The player places its setup settlement and road with the default chooser.</summary>
        public static void HumanSetup(WorldSim sim)
        {
            var (corner, edge) = TableDefaults.SetupPlacement(sim.State, 0);
            Do(sim, new PlaceTableSettlement { Corner = corner });
            Do(sim, new PlaceTableRoad { HexEdge = edge });
        }

        /// <summary>Opens the board, runs the setup round, and steps to the start of the player's first main phase.</summary>
        public static WorldSim ToHumanTurn(int people = 4, Action<TableSeams> wire = null, Action<TableTuning> tune = null, TablePort[] ports = null)
        {
            var sim = Create(people, 0, Seed, ports, wire ?? IdleAi, tune);
            Do(sim, new Sacrifice());
            HumanSetup(sim);
            var t = T(sim);
            Assert.IsTrue(StepUntil(sim, () => t.Phase == TablePhase.Main && t.Current == 0, 20000), "reach the player's turn");
            return sim;
        }

        public static void ClearHands(TableState t)
        {
            foreach (var p in t.Peoples) Array.Clear(p.Hand, 0, p.Hand.Length);
        }

        public static void SetHand(TableState t, int p, int wood, int clay, int grain, int sheep, int stone) =>
            t.Peoples[p].Hand = new[] { wood, clay, grain, sheep, stone };

        public static int[] Cards(int wood = 0, int clay = 0, int grain = 0, int sheep = 0, int stone = 0) => new[] { wood, clay, grain, sheep, stone };

        public static T Single<T>(List<SimEvent> evs) where T : SimEvent
        {
            T found = null;
            foreach (var e in evs)
                if (e is T x)
                {
                    Assert.IsNull(found, "more than one " + typeof(T).Name);
                    found = x;
                }
            Assert.IsNotNull(found, "no " + typeof(T).Name);
            return found;
        }

        public static int Count<T>(List<SimEvent> evs) where T : SimEvent
        {
            int n = 0;
            foreach (var e in evs) if (e is T) n++;
            return n;
        }

        /// <summary>A free corner two hex edges from 'from' with a free middle corner, legal for a settlement, and the two edges to it.</summary>
        public static (int target, int e1, int e2) TwoAway(WorldState s, int p, int from)
        {
            var topo = s.Board.Topology;
            var t = s.Table;
            foreach (int mid in topo.CornerNeighbours(from))
            {
                if (t.CornerOwner[mid] >= 0) continue;
                int e1 = topo.HexEdgeBetween(from, mid);
                if (t.RoadOwner[e1] >= 0) continue;
                foreach (int c in topo.CornerNeighbours(mid))
                {
                    if (c == from) continue;
                    int e2 = topo.HexEdgeBetween(mid, c);
                    if (t.RoadOwner[e2] >= 0) continue;
                    if (TableRules.SettlementRefusal(s, p, c, false) == null) return (c, e1, e2);
                }
            }
            Assert.Fail("no corner two away from " + from);
            return (-1, -1, -1);
        }
    }
}
