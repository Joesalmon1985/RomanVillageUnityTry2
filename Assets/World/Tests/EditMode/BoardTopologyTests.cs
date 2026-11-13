using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace World.Sim.Tests
{
    public class BoardTopologyTests
    {
        private static BoardTopology Topo => BoardTopology.Get(4);

        [Test]
        public void Radius4_Counts_BothLevels()
        {
            Assert.AreEqual(61, Topo.HexCount);
            Assert.AreEqual(150, Topo.CornerCount);
            Assert.AreEqual(210, Topo.HexEdgeCount);
            Assert.AreEqual(570, Topo.VertexCount, "flags = corners + 2 per hex edge");
            Assert.AreEqual(630, Topo.EdgeCount, "segments = 3 per hex edge");
            Assert.AreEqual(150, Topo.Corners.Count);
        }

        [Test]
        public void Radius0And2_Counts()
        {
            var t0 = new BoardTopology(0);
            Assert.AreEqual((1, 6, 6, 18, 18), (t0.HexCount, t0.CornerCount, t0.HexEdgeCount, t0.VertexCount, t0.EdgeCount));
            var t2 = new BoardTopology(2);
            Assert.AreEqual((19, 54, 72), (t2.HexCount, t2.CornerCount, t2.HexEdgeCount));
            Assert.AreEqual(54 + 2 * 72, t2.VertexCount);
            Assert.AreEqual(3 * 72, t2.EdgeCount);
        }

        [Test]
        public void Corners_Degree2Or3_IntermediateFlags_Degree2()
        {
            int deg2 = 0, deg3 = 0;
            foreach (int v in Topo.Corners)
            {
                Assert.IsTrue(Topo.IsCorner(v));
                int d = Topo.Degree(v);
                Assert.That(d == 2 || d == 3, "corner " + v + " degree " + d);
                Assert.AreEqual(d, Topo.CornerNeighbours(v).Count);
                Assert.AreEqual(d, Topo.CornerHexEdges(v).Count);
                Assert.AreEqual(d, Topo.VertexNeighbours(v).Count);
                if (d == 2) deg2++; else deg3++;
            }
            Assert.AreEqual(2 * 210, deg2 * 2 + deg3 * 3, "handshake lemma on hex edges");
            for (int v = Topo.CornerCount; v < Topo.VertexCount; v++)
            {
                Assert.IsFalse(Topo.IsCorner(v));
                Assert.AreEqual(2, Topo.Degree(v), "intermediate flag " + v);
                Assert.AreEqual(0, Topo.CornerNeighbours(v).Count);
            }
            Assert.AreEqual(2 * 630, Enumerable.Range(0, Topo.VertexCount).Sum(v => Topo.Degree(v)), "handshake lemma on segments");
        }

        [Test]
        public void CornerNeighbours_AreExactlyThreeSegmentsAway_AlongTheirHexEdge()
        {
            foreach (int v in Topo.Corners)
                foreach (int n in Topo.CornerNeighbours(v))
                {
                    Assert.AreEqual(3, Topo.Distance(v, n), "corner " + v + " to " + n);
                    Assert.AreEqual(1, Topo.CornerDistance(v, n));
                    int he = Topo.HexEdgeBetween(v, n);
                    Assert.GreaterOrEqual(he, 0);
                    var flags = Topo.HexEdgeFlags(he);
                    Assert.AreEqual(Math.Min(v, n), flags[0]);
                    Assert.AreEqual(Math.Max(v, n), flags[3]);
                    var segs = Topo.HexEdgeSegments(he);
                    for (int k = 0; k < 3; k++)
                    {
                        Assert.AreEqual(segs[k], Topo.EdgeBetween(flags[k], flags[k + 1]));
                        Assert.AreEqual(he, Topo.HexEdgeOfSegment(segs[k]));
                    }
                    Assert.AreEqual(he, Topo.HexEdgeOfFlag(flags[1]));
                    Assert.AreEqual(he, Topo.HexEdgeOfFlag(flags[2]));
                }
            Assert.AreEqual(-1, Topo.HexEdgeOfFlag(0));
        }

        [Test]
        public void VertexHexes_CornersOneToThree_IntermediatesShareTheirHexEdge()
        {
            int c = Topo.HexIndex(new HexCoord(0, 0));
            foreach (int v in Topo.HexCorners(c)) Assert.AreEqual(3, Topo.VertexHexes(v).Count, "interior corner");
            foreach (int v in Topo.Corners) Assert.That(Topo.VertexHexes(v).Count >= 1 && Topo.VertexHexes(v).Count <= 3);
            for (int he = 0; he < Topo.HexEdgeCount; he++)
            {
                var f = Topo.HexEdgeFlags(he);
                var shared = Topo.VertexHexes(f[0]).Intersect(Topo.VertexHexes(f[3])).OrderBy(x => x).ToList();
                Assert.That(shared.Count == 1 || shared.Count == 2);
                CollectionAssert.AreEqual(shared, Topo.VertexHexes(f[1]).ToList());
                CollectionAssert.AreEqual(shared, Topo.VertexHexes(f[2]).ToList());
            }
            // Interior hex edges touch two hexes, rim hex edges one: 6*61 hex sides in total.
            int sides = Enumerable.Range(0, Topo.HexEdgeCount).Sum(he => Topo.VertexHexes(Topo.HexEdgeFlags(he)[1]).Count);
            Assert.AreEqual(6 * 61, sides);
        }

        [Test]
        public void HexLists_InCornerOrder()
        {
            for (int h = 0; h < Topo.HexCount; h++)
            {
                var corners = Topo.HexCorners(h);
                var sides = Topo.HexSides(h);
                var ring = Topo.HexFlags(h);
                Assert.AreEqual(6, corners.Distinct().Count());
                Assert.AreEqual(6, sides.Distinct().Count());
                Assert.AreEqual(18, ring.Distinct().Count());
                for (int k = 0; k < 6; k++)
                {
                    Assert.AreEqual(sides[k], Topo.HexEdgeBetween(corners[k], corners[(k + 1) % 6]));
                    Assert.AreEqual(corners[k], ring[3 * k]);
                    // Consecutive ring flags are one segment apart, all the way round.
                    for (int j = 0; j < 3; j++)
                        Assert.GreaterOrEqual(Topo.EdgeBetween(ring[3 * k + j], ring[(3 * k + j + 1) % 18]), 0);
                    Assert.IsTrue(Topo.VertexHexes(ring[3 * k + 1]).Contains(h));
                }
            }
        }

        [Test]
        public void EdgeBetween_IsSymmetric_AndMatchesEndpoints()
        {
            for (int e = 0; e < Topo.EdgeCount; e++)
            {
                int a = Topo.EdgeVertexA(e), b = Topo.EdgeVertexB(e);
                Assert.Less(a, b);
                Assert.AreEqual(e, Topo.EdgeBetween(a, b));
                Assert.AreEqual(e, Topo.EdgeBetween(b, a));
                Assert.AreEqual(b, Topo.OtherEnd(e, a));
                CollectionAssert.Contains(Topo.VertexEdges(a).ToList(), e);
                CollectionAssert.Contains(Topo.VertexEdges(b).ToList(), e);
            }
            Assert.AreEqual(-1, Topo.EdgeBetween(0, 0));
            int c = Topo.HexIndex(new HexCoord(0, 0));
            var cv = Topo.HexCorners(c);
            Assert.AreEqual(-1, Topo.EdgeBetween(cv[0], cv[1]), "neighbouring corners are a hex edge (3 segments) apart, not one segment");
            Assert.AreEqual(-1, Topo.HexEdgeBetween(cv[0], cv[3]), "opposite corners are not adjacent");
        }

        [Test]
        public void Ordering_IsStable_AndDocumented()
        {
            var a = new BoardTopology(4);
            var b = new BoardTopology(4);
            for (int v = 0; v < a.VertexCount; v++)
            {
                Assert.AreEqual(a.VertexX(v), b.VertexX(v));
                Assert.AreEqual(a.VertexZ(v), b.VertexZ(v));
                CollectionAssert.AreEqual(a.VertexHexes(v).ToList(), b.VertexHexes(v).ToList());
            }
            for (int e = 0; e < a.EdgeCount; e++)
                Assert.AreEqual((a.EdgeVertexA(e), a.EdgeVertexB(e)), (b.EdgeVertexA(e), b.EdgeVertexB(e)));
            // Corners first, south to north then west to east.
            for (int v = 1; v < a.CornerCount; v++)
                Assert.That(a.VertexZ(v - 1) < a.VertexZ(v) || (a.VertexZ(v - 1) == a.VertexZ(v) && a.VertexX(v - 1) < a.VertexX(v)));
            // Hex edges by (lower corner, higher corner).
            for (int he = 1; he < a.HexEdgeCount; he++)
                Assert.That((a.HexEdgeCornerA(he - 1), a.HexEdgeCornerB(he - 1)).CompareTo((a.HexEdgeCornerA(he), a.HexEdgeCornerB(he))) < 0);
            // Intermediate flag id = CornerCount + 2*hexEdge + j; segment id = 3*hexEdge + k.
            for (int he = 0; he < a.HexEdgeCount; he++)
            {
                var f = a.HexEdgeFlags(he);
                Assert.AreEqual(a.CornerCount + 2 * he, f[1]);
                Assert.AreEqual(a.CornerCount + 2 * he + 1, f[2]);
                CollectionAssert.AreEqual(new[] { 3 * he, 3 * he + 1, 3 * he + 2 }, a.HexEdgeSegments(he).ToArray());
            }
            // A few pinned ids (changing them breaks saves and Unity views).
            int centre = a.HexIndex(new HexCoord(0, 0));
            Assert.AreEqual(30, centre);
            CollectionAssert.AreEqual(new[] { 70, 80, 89, 79, 69, 60 }, a.HexCorners(centre).ToArray());
        }

        [Test]
        public void Geometry_HexEdge60_Segment20()
        {
            Assert.AreEqual(20.0, BoardTopology.EdgeLength);
            Assert.AreEqual(60.0, BoardTopology.HexEdgeLength);
            Assert.AreEqual(3, BoardTopology.SegmentsPerHexEdge);
            for (int e = 0; e < Topo.EdgeCount; e++)
            {
                int a = Topo.EdgeVertexA(e), b = Topo.EdgeVertexB(e);
                double dx = Topo.VertexX(a) - Topo.VertexX(b), dz = Topo.VertexZ(a) - Topo.VertexZ(b);
                Assert.AreEqual(20.0, Math.Sqrt(dx * dx + dz * dz), 1e-9, "segment " + e);
            }
            for (int h = 0; h < Topo.HexCount; h++)
                foreach (int v in Topo.HexCorners(h))
                {
                    double dx = Topo.VertexX(v) - Topo.HexX(h), dz = Topo.VertexZ(v) - Topo.HexZ(h);
                    Assert.AreEqual(60.0, Math.Sqrt(dx * dx + dz * dz), 1e-9);
                }
            // Intermediate flags are exactly on the straight hex edge at 1/3 and 2/3.
            for (int he = 0; he < Topo.HexEdgeCount; he++)
            {
                var f = Topo.HexEdgeFlags(he);
                for (int j = 1; j <= 2; j++)
                {
                    Assert.AreEqual(Topo.VertexX(f[0]) + j * (Topo.VertexX(f[3]) - Topo.VertexX(f[0])) / 3.0, Topo.VertexX(f[j]), 1e-9);
                    Assert.AreEqual(Topo.VertexZ(f[0]) + j * (Topo.VertexZ(f[3]) - Topo.VertexZ(f[0])) / 3.0, Topo.VertexZ(f[j]), 1e-9);
                }
            }
            // Pointy-top: corner 2 is straight north of the centre.
            int c = Topo.HexIndex(new HexCoord(0, 0));
            Assert.AreEqual(0.0, Topo.HexX(c), 1e-12);
            Assert.AreEqual(0.0, Topo.VertexX(Topo.HexCorners(c)[2]), 1e-12);
            Assert.AreEqual(60.0, Topo.VertexZ(Topo.HexCorners(c)[2]), 1e-12);
        }

        [Test]
        public void HexAt_And_NearestVertex_RoundTrip()
        {
            for (int h = 0; h < Topo.HexCount; h++)
                Assert.AreEqual(h, Topo.HexAt(Topo.HexX(h) + 9.0, Topo.HexZ(h) - 6.0));
            Assert.AreEqual(-1, Topo.HexAt(2000, 2000));
            for (int v = 0; v < Topo.VertexCount; v++)
                Assert.AreEqual(v, Topo.NearestVertex(Topo.VertexX(v) + 1.0, Topo.VertexZ(v)));
            foreach (int v in Topo.Corners)
                Assert.AreEqual(v, Topo.NearestCorner(Topo.VertexX(v) + 5.0, Topo.VertexZ(v) - 3.0));
        }

        [Test]
        public void FindPath_ShortestAndAvoidsBlockedSegment()
        {
            int c = Topo.HexIndex(new HexCoord(0, 0));
            var cv = Topo.HexCorners(c);
            int from = cv[0], to = cv[3];

            var path = Topo.FindPath(from, to);
            Assert.IsNotNull(path);
            Assert.AreEqual(10, path.Length, "opposite corners are 3 hex edges = 9 segments apart");
            Assert.AreEqual(from, path[0]);
            Assert.AreEqual(to, path[path.Length - 1]);
            for (int i = 1; i < path.Length; i++) Assert.GreaterOrEqual(Topo.EdgeBetween(path[i - 1], path[i]), 0);
            Assert.AreEqual(3, Topo.CornerDistance(from, to));
            var cp = Topo.FindCornerPath(from, to);
            Assert.AreEqual(4, cp.Length);
            for (int i = 1; i < cp.Length; i++) Assert.GreaterOrEqual(Topo.HexEdgeBetween(cp[i - 1], cp[i]), 0);
            Assert.IsNull(Topo.FindCornerPath(from, Topo.CornerCount), "intermediate flags are not on the corner graph");

            int blocked = Topo.EdgeBetween(path[0], path[1]);
            var detour = Topo.FindPath(from, to, e => e != blocked);
            Assert.IsNotNull(detour);
            CollectionAssert.DoesNotContain(Topo.PathEdges(detour).ToList(), blocked);
            Assert.AreEqual(10, detour.Length, "around the other side of the hex is also 9 segments");

            var cut = new HashSet<int>(Topo.VertexEdges(from));
            Assert.IsNull(Topo.FindPath(from, to, e => !cut.Contains(e)));
            Assert.AreEqual(-1, Topo.Distance(from, to, e => !cut.Contains(e)));
            Assert.AreEqual(9, Topo.Distance(from, to));

            Assert.AreEqual(new[] { from }, Topo.FindPath(from, from));
            Assert.AreEqual(Topo.FindPath(from, to), Topo.FindPath(from, to), "deterministic");
        }

        [Test]
        public void AllFlagsConnected_DistanceTableMatchesBfs()
        {
            for (int v = 1; v < Topo.VertexCount; v++) Assert.IsNotNull(Topo.FindPath(0, v));
            for (int v = 0; v < Topo.VertexCount; v += 37)
                Assert.AreEqual(Topo.FindPath(5, v).Length - 1, Topo.Distance(5, v));
        }
    }
}
