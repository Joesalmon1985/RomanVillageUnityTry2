using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace World.Sim.Tests
{
    public class BoardTopologyTests
    {
        private static BoardTopology Topo => BoardTopology.Get(2);

        [Test]
        public void Radius2_Counts()
        {
            Assert.AreEqual(19, Topo.HexCount);
            Assert.AreEqual(54, Topo.VertexCount);
            Assert.AreEqual(72, Topo.EdgeCount);
        }

        [Test]
        public void Radius0And1_Counts()
        {
            var t0 = new BoardTopology(0);
            Assert.AreEqual((1, 6, 6), (t0.HexCount, t0.VertexCount, t0.EdgeCount));
            var t1 = new BoardTopology(1);
            Assert.AreEqual((7, 24, 30), (t1.HexCount, t1.VertexCount, t1.EdgeCount));
        }

        [Test]
        public void CentreHex_HasSixDistinctVertices_AndSixEdges()
        {
            int c = Topo.HexIndex(new HexCoord(0, 0));
            Assert.GreaterOrEqual(c, 0);
            Assert.AreEqual(6, Topo.HexVertices(c).Distinct().Count());
            Assert.AreEqual(6, Topo.HexEdges(c).Distinct().Count());
            Assert.IsTrue(Topo.HexEdges(c).All(e => e >= 0));
        }

        [Test]
        public void EveryVertex_HasDegreeTwoOrThree_AndOneToThreeHexes()
        {
            int deg2 = 0, deg3 = 0;
            for (int v = 0; v < Topo.VertexCount; v++)
            {
                int d = Topo.Degree(v);
                Assert.That(d == 2 || d == 3, "vertex " + v + " degree " + d);
                if (d == 2) deg2++; else deg3++;
                Assert.That(Topo.VertexHexes(v).Count >= 1 && Topo.VertexHexes(v).Count <= 3);
                Assert.AreEqual(d, Topo.VertexNeighbours(v).Count);
            }
            Assert.AreEqual(18, deg2, "rim vertices touching one hex");
            Assert.AreEqual(36, deg3);
            Assert.AreEqual(2 * 72, deg2 * 2 + deg3 * 3, "handshake lemma");
        }

        [Test]
        public void InteriorVertex_TouchesThreeHexes()
        {
            // Every corner of the centre hex is interior.
            int c = Topo.HexIndex(new HexCoord(0, 0));
            foreach (int v in Topo.HexVertices(c))
            {
                Assert.AreEqual(3, Topo.VertexHexes(v).Count);
                Assert.AreEqual(3, Topo.Degree(v));
            }
            Assert.AreEqual(24, Enumerable.Range(0, Topo.VertexCount).Count(v => Topo.VertexHexes(v).Count == 3));
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
            var cv = Topo.HexVertices(c);
            Assert.AreEqual(-1, Topo.EdgeBetween(cv[0], cv[3]), "opposite corners are not adjacent");
        }

        [Test]
        public void Ordering_IsStable_AcrossInstances()
        {
            var a = new BoardTopology(2);
            var b = new BoardTopology(2);
            for (int v = 0; v < a.VertexCount; v++)
            {
                Assert.AreEqual(a.VertexX(v), b.VertexX(v));
                Assert.AreEqual(a.VertexZ(v), b.VertexZ(v));
            }
            for (int e = 0; e < a.EdgeCount; e++)
                Assert.AreEqual((a.EdgeVertexA(e), a.EdgeVertexB(e)), (b.EdgeVertexA(e), b.EdgeVertexB(e)));
            // Vertices sorted south to north, then west to east.
            for (int v = 1; v < a.VertexCount; v++)
                Assert.That(a.VertexZ(v - 1) < a.VertexZ(v) || (a.VertexZ(v - 1) == a.VertexZ(v) && a.VertexX(v - 1) < a.VertexX(v)));
        }

        [Test]
        public void Geometry_TrueCorners_EdgeLength20()
        {
            Assert.AreEqual(20.0, BoardTopology.EdgeLength);
            for (int e = 0; e < Topo.EdgeCount; e++)
            {
                int a = Topo.EdgeVertexA(e), b = Topo.EdgeVertexB(e);
                double dx = Topo.VertexX(a) - Topo.VertexX(b), dz = Topo.VertexZ(a) - Topo.VertexZ(b);
                Assert.AreEqual(20.0, Math.Sqrt(dx * dx + dz * dz), 1e-9, "edge " + e);
            }
            for (int h = 0; h < Topo.HexCount; h++)
                foreach (int v in Topo.HexVertices(h))
                {
                    double dx = Topo.VertexX(v) - Topo.HexX(h), dz = Topo.VertexZ(v) - Topo.HexZ(h);
                    Assert.AreEqual(20.0, Math.Sqrt(dx * dx + dz * dz), 1e-9);
                }
            // Pointy-top: corner 2 is straight north of the centre.
            int c = Topo.HexIndex(new HexCoord(0, 0));
            Assert.AreEqual(0.0, Topo.HexX(c), 1e-12);
            Assert.AreEqual(0.0, Topo.VertexX(Topo.HexVertices(c)[2]), 1e-12);
            Assert.AreEqual(20.0, Topo.VertexZ(Topo.HexVertices(c)[2]), 1e-12);
        }

        [Test]
        public void HexAt_And_NearestVertex_RoundTrip()
        {
            for (int h = 0; h < Topo.HexCount; h++)
                Assert.AreEqual(h, Topo.HexAt(Topo.HexX(h) + 3.0, Topo.HexZ(h) - 2.0));
            Assert.AreEqual(-1, Topo.HexAt(500, 500));
            for (int v = 0; v < Topo.VertexCount; v++)
                Assert.AreEqual(v, Topo.NearestVertex(Topo.VertexX(v) + 1.0, Topo.VertexZ(v)));
        }

        [Test]
        public void FindPath_ShortestAndAvoidsBlockedEdge()
        {
            int c = Topo.HexIndex(new HexCoord(0, 0));
            var cv = Topo.HexVertices(c);
            int from = cv[0], to = cv[3];

            var path = Topo.FindPath(from, to);
            Assert.IsNotNull(path);
            Assert.AreEqual(4, path.Length, "opposite corners are 3 edges apart");
            Assert.AreEqual(from, path[0]);
            Assert.AreEqual(to, path[path.Length - 1]);
            for (int i = 1; i < path.Length; i++) Assert.GreaterOrEqual(Topo.EdgeBetween(path[i - 1], path[i]), 0);

            // Block the first edge of the found path: a different path must be found and avoid it.
            int blocked = Topo.EdgeBetween(path[0], path[1]);
            var detour = Topo.FindPath(from, to, e => e != blocked);
            Assert.IsNotNull(detour);
            CollectionAssert.DoesNotContain(Topo.PathEdges(detour).ToList(), blocked);
            Assert.AreEqual(4, detour.Length, "around the other side of the hex is also 3 edges");

            // Block every edge of the start vertex: unreachable.
            var cut = new HashSet<int>(Topo.VertexEdges(from));
            Assert.IsNull(Topo.FindPath(from, to, e => !cut.Contains(e)));
            Assert.AreEqual(-1, Topo.Distance(from, to, e => !cut.Contains(e)));

            Assert.AreEqual(new[] { from }, Topo.FindPath(from, from));
            Assert.AreEqual(Topo.FindPath(from, to), Topo.FindPath(from, to), "deterministic");
        }

        [Test]
        public void AllVerticesConnected()
        {
            for (int v = 1; v < Topo.VertexCount; v++) Assert.IsNotNull(Topo.FindPath(0, v));
        }
    }
}
