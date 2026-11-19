using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>GB-5, GB-6, PE-2, TU-1: before the first opening, the setup round, the turn order.</summary>
    public class TableSetupTests
    {
        [Test]
        public void InitState_FirstCampsOnStartCorners_OnePointEach()
        {
            var sim = Create();
            var t = T(sim);
            Assert.AreEqual(4, t.PeopleCount);
            for (int p = 0; p < 4; p++)
            {
                Assert.AreEqual(p, t.CornerOwner[StartCorners[p]]);
                Assert.AreEqual(1, t.CornerLevel[StartCorners[p]]);
                Assert.AreEqual(1, TableRules.Points(sim.State, p));
                Assert.AreEqual(0, TableRules.HandSize(t.Peoples[p].Hand));
            }
            Assert.AreEqual(TablePhase.NotOpened, t.Phase);
        }

        [Test]
        public void BeforeSacrifice_NoRollsNoTurns_CommandsRefused()
        {
            var sim = Create();
            var t = T(sim);
            var evs = Step(sim, 2000);
            Assert.AreEqual(0, evs.Count);
            Assert.AreEqual(TablePhase.NotOpened, t.Phase);
            Assert.AreEqual(0, t.LastRoll);
            Refused(sim, new PlaceTableSettlement { Corner = 10 }, TableRefusals.TableClosed);
            Refused(sim, new EndTurn(), TableRefusals.TableClosed);
            Refused(sim, new LeaveTable(), TableRefusals.TableClosed);
            Refused(sim, new Sacrifice(), TableRefusals.NotYourTurn, 1); // only the player's own altar opens the board
        }

        [Test]
        public void Sacrifice_OpensSetup_PlayerPlacesFirst()
        {
            var sim = Create();
            var evs = Do(sim, new Sacrifice());
            var t = T(sim);
            Assert.AreEqual(1, Count<TableOpened>(evs));
            Assert.AreEqual(TablePhase.Setup, t.Phase);
            Assert.AreEqual(0, t.Current);
            var ts = Single<TurnStarted>(evs);
            Assert.IsTrue(ts.Setup);
            // The player is not timed in setup: nothing happens while they think.
            Step(sim, 3000);
            Assert.AreEqual(TablePhase.Setup, t.Phase);
            Assert.AreEqual(0, t.Current);
        }

        [Test]
        public void Setup_DistanceRule_RoadMustTouchNewSettlement_StartingCards()
        {
            var sim = Create();
            Do(sim, new Sacrifice());
            var s = sim.State;
            var topo = s.Board.Topology;
            int home = StartCorners[0];
            Refused(sim, new PlaceTableSettlement { Corner = topo.CornerNeighbours(home)[0] }, TableRefusals.TooClose);
            Refused(sim, new PlaceTableSettlement { Corner = home }, TableRefusals.InvalidSite);
            Refused(sim, new PlaceTableRoad { HexEdge = topo.CornerHexEdges(home)[0] }, TableRefusals.InvalidSite); // settlement first
            Refused(sim, new PlaceTableSettlement { Corner = 5 }, TableRefusals.NotYourTurn, 1);

            // Any legal corner, even one far from the first camp (no road needed in setup).
            int far = TableRules.LegalSettlementCorners(s, 0, false).OrderByDescending(c => topo.CornerDistance(home, c)).First();
            Assert.Greater(topo.CornerDistance(home, far), 4);
            Do(sim, new PlaceTableSettlement { Corner = far });
            Assert.AreEqual(far, T(sim).SetupCorner);
            Refused(sim, new PlaceTableRoad { HexEdge = topo.CornerHexEdges(home)[0] }, TableRefusals.NotConnected);
            Refused(sim, new PlaceTableSettlement { Corner = 3 }, TableRefusals.InvalidSite); // one settlement per setup turn

            var expected = new int[5];
            foreach (int h in topo.VertexHexes(far))
            {
                var w = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                if (w.HasValue) expected[(int)w.Value]++;
            }
            var evs = Do(sim, new PlaceTableRoad { HexEdge = topo.CornerHexEdges(far)[0] });
            CollectionAssert.AreEqual(expected, T(sim).Peoples[0].Hand);
            CollectionAssert.AreEqual(expected, Single<StartingCards>(evs).Cards);
            Assert.AreEqual(1, T(sim).Current, "the next seat places");
            Assert.AreEqual(2, TableRules.Points(s, 0));
        }

        [Test]
        public void Setup_AiSeatsPlaceInSeatOrder_ThenTurnOrderFixedBySeed()
        {
            var sim = Create(wire: IdleAi);
            Do(sim, new Sacrifice());
            HumanSetup(sim);
            var t = T(sim);
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => t.Phase != TablePhase.Setup, 1000, log));
            var placers = log.OfType<Built>().Where(b => b.Setup && b.Kind == "settlement").Select(b => b.People).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, placers);
            for (int p = 0; p < 4; p++)
            {
                Assert.AreEqual(2, TableRules.CountSettlements(t, p));
                Assert.AreEqual(1, TableRules.CountRoads(t, p));
                Assert.Greater(TableRules.HandSize(t.Peoples[p].Hand), 0);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, t.TurnOrder);
            Assert.AreEqual(1, t.Round);
            Assert.AreEqual(t.TurnOrder[0], t.Current);
            Assert.AreEqual(1, log.OfType<TurnStarted>().Count(e => !e.Setup));

            // Same seed, same order; the order is not simply seat order for every seed.
            var again = Create(wire: IdleAi);
            Do(again, new Sacrifice());
            HumanSetup(again);
            StepUntil(again, () => T(again).Phase != TablePhase.Setup, 1000);
            CollectionAssert.AreEqual(t.TurnOrder, T(again).TurnOrder);
            bool someDiffer = false;
            for (ulong seed = 1; seed <= 6 && !someDiffer; seed++)
            {
                var other = Create(humanSeat: -1, seed: seed, wire: IdleAi);
                StepUntil(other, () => T(other).Phase != TablePhase.Setup && T(other).Phase != TablePhase.NotOpened, 1000);
                if (!T(other).TurnOrder.SequenceEqual(new[] { 0, 1, 2, 3 })) someDiffer = true;
            }
            Assert.IsTrue(someDiffer);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Setup_WorksForAnyNumberOfPeoples(int n)
        {
            var sim = Create(people: n, humanSeat: -1, wire: IdleAi);
            var t = T(sim);
            Assert.IsTrue(StepUntil(sim, () => t.Phase == TablePhase.Main || t.Phase == TablePhase.Discard, 2000));
            Assert.AreEqual(n, t.TurnOrder.Length);
            for (int p = 0; p < n; p++) Assert.AreEqual(2, TableRules.CountSettlements(t, p));
        }

        [Test]
        public void Setup_SixPeoples()
        {
            var s = new WorldState { Seed = Seed, Rng = new Rng(Seed), Board = BoardGenerator.CreateMvpBoard(Seed) };
            // Two more first camps on legal corners far from the four fixed ones.
            var corners = new List<int>(StartCorners);
            TableModule.InitState(s, 4, StartCorners, -1);
            for (int k = 0; k < 2; k++)
            {
                int c = TableRules.LegalSettlementCorners(s, 0, false).First(x => s.Board.Topology.VertexHexes(x).Count == 3);
                corners.Add(c);
                s.Table.CornerOwner[c] = 0; // reserve while picking
            }
            TableModule.InitState(s, 6, corners.ToArray(), -1);
            var sim = new WorldSim(s);
            TableModule.Register(sim).AiTurn = (st, p, a) => { };
            Assert.IsTrue(StepUntil(sim, () => s.Table.Phase == TablePhase.Main || s.Table.Phase == TablePhase.Discard, 3000));
            Assert.AreEqual(6, s.Table.TurnOrder.Length);
            for (int p = 0; p < 6; p++) Assert.AreEqual(2, TableRules.CountSettlements(s.Table, p));
        }
    }
}
