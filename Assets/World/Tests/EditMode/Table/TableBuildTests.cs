using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>TU-3..TU-7: costs, placement, piece limits, longest road, points, the end of the age.</summary>
    public class TableBuildTests
    {
        private static int Home(WorldSim sim) => StartCorners[0];

        [Test]
        public void Costs_RoadSettlementCity_PaidExactly()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            var (target, e1, e2) = TwoAway(s, 0, Home(sim));

            SetHand(t, 0, 0, 1, 0, 0, 0);
            Refused(sim, new PlaceTableRoad { HexEdge = e1 }, TableRefusals.CannotAfford);
            SetHand(t, 0, 2, 2, 0, 0, 0);
            Do(sim, new PlaceTableRoad { HexEdge = e1 });
            CollectionAssert.AreEqual(Cards(1, 1), t.Peoples[0].Hand);
            Do(sim, new PlaceTableRoad { HexEdge = e2 });
            CollectionAssert.AreEqual(Cards(), t.Peoples[0].Hand);

            SetHand(t, 1, 1, 1, 0, 0, 0);
            Refused(sim, new PlaceTableSettlement { Corner = target }, TableRefusals.CannotAfford);
            SetHand(t, 0, 1, 1, 1, 1, 0);
            var evs = Do(sim, new PlaceTableSettlement { Corner = target });
            CollectionAssert.AreEqual(Cards(), t.Peoples[0].Hand);
            Assert.AreEqual("settlement", Single<Built>(evs).Kind);
            Assert.AreEqual(1, Single<PointsChanged>(evs).Delta);

            SetHand(t, 0, 0, 0, 2, 0, 2);
            Refused(sim, new UpgradeTableCity { Corner = target }, TableRefusals.CannotAfford);
            SetHand(t, 0, 0, 0, 2, 0, 3);
            Do(sim, new UpgradeTableCity { Corner = target });
            CollectionAssert.AreEqual(Cards(), t.Peoples[0].Hand);
            Assert.AreEqual(2, t.CornerLevel[target]);
        }

        [Test]
        public void Placement_DistanceRule_ConnectionByOwnRoad()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            var topo = s.Board.Topology;
            SetHand(t, 0, 9, 9, 9, 9, 9);
            Refused(sim, new PlaceTableSettlement { Corner = topo.CornerNeighbours(Home(sim)).First(c => t.CornerOwner[c] < 0) }, TableRefusals.TooClose);
            var (target, e1, e2) = TwoAway(s, 0, Home(sim));
            Refused(sim, new PlaceTableSettlement { Corner = target }, TableRefusals.NotConnected);
            Refused(sim, new PlaceTableRoad { HexEdge = e2 }, TableRefusals.NotConnected); // not touching our road or settlement
            Refused(sim, new PlaceTableRoad { HexEdge = topo.CornerHexEdges(StartCorners[1])[0] }, TableRefusals.NotConnected);
            Do(sim, new PlaceTableRoad { HexEdge = e1 });
            Refused(sim, new PlaceTableRoad { HexEdge = e1 }, TableRefusals.InvalidSite);
            Do(sim, new PlaceTableRoad { HexEdge = e2 });
            Do(sim, new PlaceTableSettlement { Corner = target });
            Refused(sim, new UpgradeTableCity { Corner = StartCorners[1] }, TableRefusals.InvalidSite); // not ours
        }

        [Test]
        public void Road_DoesNotRunThroughAnotherPeoplesSettlement()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            var topo = s.Board.Topology;
            var (target, e1, e2) = TwoAway(s, 0, Home(sim));
            int mid = topo.HexEdgeCornerA(e1) == Home(sim) ? topo.HexEdgeCornerB(e1) : topo.HexEdgeCornerA(e1);
            t.RoadOwner[e1] = 0;
            // People 1 holds the middle corner: our road may not continue through it.
            t.CornerOwner[mid] = 1;
            t.CornerLevel[mid] = 1;
            SetHand(t, 0, 5, 5, 0, 0, 0);
            Refused(sim, new PlaceTableRoad { HexEdge = e2 }, TableRefusals.NotConnected);
        }

        [Test]
        public void PieceLimits_NoPieces_CityReturnsSettlementPiece()
        {
            var sim = ToHumanTurn(tune: tu => { tu.MaxRoads = 2; tu.MaxSettlements = 3; tu.MaxCities = 1; });
            var s = sim.State;
            var t = T(sim);
            var topo = s.Board.Topology;
            SetHand(t, 0, 9, 9, 9, 9, 9);
            var (target, e1, e2) = TwoAway(s, 0, Home(sim));
            Do(sim, new PlaceTableRoad { HexEdge = e1 }); // 2 of 2 roads (one from setup)
            Assert.AreEqual(0, TableRules.RoadsLeft(t, 0));
            Refused(sim, new PlaceTableRoad { HexEdge = e2 }, TableRefusals.NoPieces);

            t.Tuning.MaxRoads = 15;
            Do(sim, new PlaceTableRoad { HexEdge = e2 });
            Do(sim, new PlaceTableSettlement { Corner = target }); // 3 of 3
            Assert.AreEqual(0, TableRules.SettlementsLeft(t, 0));
            var (t2, f1, f2) = TwoAway(s, 0, target);
            t.RoadOwner[f1] = 0;
            t.RoadOwner[f2] = 0;
            Refused(sim, new PlaceTableSettlement { Corner = t2 }, TableRefusals.NoPieces);

            Do(sim, new UpgradeTableCity { Corner = Home(sim) });
            Assert.AreEqual(1, TableRules.SettlementsLeft(t, 0), "the city gave its settlement piece back");
            Refused(sim, new UpgradeTableCity { Corner = target }, TableRefusals.NoPieces);
            Do(sim, new PlaceTableSettlement { Corner = t2 });
        }

        [Test]
        public void Points_FromTheBoard_SettlementCityRoad()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            Assert.AreEqual(2, TableRules.Points(s, 0));
            SetHand(t, 0, 0, 0, 2, 0, 3);
            var evs = Do(sim, new UpgradeTableCity { Corner = Home(sim) });
            Assert.AreEqual(3, TableRules.Points(s, 0));
            var pc = Single<PointsChanged>(evs);
            Assert.AreEqual((0, 3, 1), (pc.People, pc.Points, pc.Delta));
            // Points are never banked: taking the city off the board takes its points.
            t.CornerOwner[Home(sim)] = -1;
            t.CornerLevel[Home(sim)] = 0;
            Assert.AreEqual(1, TableRules.Points(s, 0));
        }

        /// <summary>A chain of n free hex edges starting at corner 'from' for people p (no corner owned by others).</summary>
        private static List<int> Chain(WorldState s, int from, int n)
        {
            var topo = s.Board.Topology;
            var t = s.Table;
            var path = new List<int>();
            var seen = new HashSet<int> { from };
            if (Extend(s, from, n, path, seen)) return path;
            Assert.Fail("no chain");
            return null;
        }

        private static bool Extend(WorldState s, int at, int n, List<int> path, HashSet<int> seen)
        {
            if (path.Count == n) return true;
            var topo = s.Board.Topology;
            foreach (int e in topo.CornerHexEdges(at))
            {
                if (s.Table.RoadOwner[e] >= 0) continue;
                int next = topo.HexEdgeCornerA(e) == at ? topo.HexEdgeCornerB(e) : topo.HexEdgeCornerA(e);
                if (seen.Contains(next) || s.Table.CornerOwner[next] >= 0) continue;
                seen.Add(next);
                path.Add(e);
                if (Extend(s, next, n, path, seen)) return true;
                path.RemoveAt(path.Count - 1);
                seen.Remove(next);
            }
            return false;
        }

        [Test]
        public void LongestRoad_FiveJoined_TwoPoints_TiesKeepHolder_LongerTakes()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            // Clear setup roads so only the chains below count.
            for (int e = 0; e < t.RoadOwner.Length; e++) t.RoadOwner[e] = -1;
            var mine = Chain(s, Home(sim), 6);
            for (int i = 0; i < 4; i++) t.RoadOwner[mine[i]] = 0;
            SetHand(t, 0, 9, 9, 0, 0, 0);
            var evs = Do(sim, new PlaceTableRoad { HexEdge = mine[4] });
            Assert.AreEqual(0, t.LongestRoadHolder);
            Assert.AreEqual(5, t.Peoples[0].RoadLength);
            Assert.AreEqual(0, Single<LongestRoadChanged>(evs).Holder);
            Assert.AreEqual(2 + 2, TableRules.Points(s, 0));

            // People 2 draws level at 5: the holder keeps it.
            var theirs = Chain(s, StartCorners[2], 6);
            for (int i = 0; i < 5; i++) t.RoadOwner[theirs[i]] = 2;
            t.RoadOwner[theirs[0]] = 2; // (already set; their chain is 5 long)
            SetHand(t, 0, 9, 9, 0, 0, 0);
            Do(sim, new PlaceTableRoad { HexEdge = mine[5] }); // we go to 6
            Assert.AreEqual(0, t.LongestRoadHolder);
            t.RoadOwner[theirs[5]] = 2;                       // they reach 6: a tie, we keep it
            SetHand(t, 0, 9, 9, 0, 0, 0);
            var extra = TableRules.LegalRoadEdges(s, 0).First(e => !mine.Contains(e));
            Do(sim, new PlaceTableRoad { HexEdge = extra });   // a branch: still 6
            Assert.AreEqual(6, t.Peoples[2].RoadLength);
            Assert.AreEqual(0, t.LongestRoadHolder, "ties keep the holder");
            Assert.AreEqual(2, TableRules.Points(s, 2));
        }

        [Test]
        public void LongestRoad_StrictlyLongerTakesIt()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            for (int e = 0; e < t.RoadOwner.Length; e++) t.RoadOwner[e] = -1;
            var theirs = Chain(s, StartCorners[2], 5);
            foreach (int e in theirs) t.RoadOwner[e] = 2;
            var mine = Chain(s, Home(sim), 6);
            for (int i = 0; i < 5; i++) t.RoadOwner[mine[i]] = 0;
            SetHand(t, 0, 9, 9, 0, 0, 0);
            // A tie at 5 with nobody holding it yet: nobody gets it.
            Do(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay });
            Assert.AreEqual(-1, t.LongestRoadHolder);
            Do(sim, new PlaceTableRoad { HexEdge = mine[5] });
            Assert.AreEqual(0, t.LongestRoadHolder);
        }

        [Test]
        public void TenPoints_AgeEnded_TableClosed()
        {
            var sim = ToHumanTurn(tune: tu => tu.WinPoints = 4);
            var s = sim.State;
            var t = T(sim);
            SetHand(t, 0, 0, 0, 4, 0, 6);
            Do(sim, new UpgradeTableCity { Corner = Home(sim) }); // 3 points
            Assert.AreEqual(TablePhase.Main, t.Phase);
            int second = Enumerable.Range(0, t.CornerOwner.Length).First(c => t.CornerOwner[c] == 0 && t.CornerLevel[c] == 1);
            var evs = Do(sim, new UpgradeTableCity { Corner = second }); // 4 points
            var ae = Single<AgeEnded>(evs);
            Assert.AreEqual((0, 4), (ae.Winner, ae.Points));
            Assert.AreEqual(TablePhase.Ended, t.Phase);
            Assert.AreEqual(0, t.Winner);
            Refused(sim, new EndTurn(), TableRefusals.TableClosed);
            Refused(sim, new Sacrifice(), TableRefusals.TableClosed);
            Assert.AreEqual(0, Step(sim, 500).Count, "nothing moves after the age ends");
        }

        [Test]
        public void DefaultWinPointsIsTen()
        {
            Assert.AreEqual(10, new TableTuning().WinPoints);
        }
    }
}
