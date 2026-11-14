using System;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;

namespace World.Sim.Tests
{
    /// <summary>Plot ids and geometry (REFACTOR_SPEC 4.1, 4.2) and the walking lattice they sit on (2.D).</summary>
    public class PlotLayoutTests
    {
        private static Board B => BoardGenerator.CreateMvpBoard(SimTestKit.DefaultSeed);

        private static bool OnLattice(double x, double z)
        {
            double i = x / (5 * Math.Sqrt(3) / 2);
            double j = z / 5 - i / 2;
            return Math.Abs(i - Math.Round(i)) < 1e-6 && Math.Abs(j - Math.Round(j)) < 1e-6;
        }

        private static double SegDist(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            double u = Math.Max(0, Math.Min(1, ((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz)));
            return Math.Sqrt(Math.Pow(ax + u * dx - px, 2) + Math.Pow(az + u * dz - pz, 2));
        }

        [Test]
        public void Ids_RoundTrip()
        {
            for (int c = 0; c < 150; c++)
                for (int sec = 0; sec < 3; sec++)
                    for (int slot = 0; slot < 3; slot++)
                    {
                        int p = PlotLayout.PlotId(c, sec, slot);
                        Assert.AreEqual(c, PlotLayout.CornerOf(p));
                        Assert.AreEqual(sec, PlotLayout.SectorOf(p));
                        Assert.AreEqual(slot, PlotLayout.SlotOf(p));
                    }
            Assert.AreEqual(1349, PlotLayout.PlotId(149, 2, 2));
            Assert.AreEqual(9, PlotLayout.PlotsPerCorner);
        }

        [Test]
        public void DefaultBoard_HomeCornerSectors_AndNamedPlots()
        {
            var b = B;
            var t = b.Topology;
            Assert.IsTrue(PlotLayout.IsSettlementCorner(t, 70));
            Assert.AreEqual(22, PlotLayout.SectorHex(t, 70, 0));
            Assert.AreEqual(30, PlotLayout.SectorHex(t, 70, 1));
            Assert.AreEqual(31, PlotLayout.SectorHex(t, 70, 2));
            Assert.AreEqual(Terrain.Forest, b.HexTerrain[22]);
            Assert.AreEqual(Terrain.Fields, b.HexTerrain[30]);
            Assert.AreEqual(Terrain.Hills, b.HexTerrain[31]);
            Assert.AreEqual(1, PlotLayout.SectorOfHex(t, 70, 30));
            Assert.AreEqual(-1, PlotLayout.SectorOfHex(t, 70, 0));
            Assert.AreEqual(22, PlotLayout.PlotHex(t, 631));
            Assert.AreEqual(30, PlotLayout.PlotHex(t, 635));
            Assert.AreEqual(-1, PlotLayout.SectorHex(t, 70, 3));
        }

        [Test]
        public void EveryPlotOfEverySettlementCorner_Spacing_Clearances_Lattice()
        {
            var t = B.Topology;
            int corners = 0;
            foreach (int c in t.Corners)
            {
                if (!PlotLayout.IsSettlementCorner(t, c)) continue;
                corners++;
                Assert.IsTrue(OnLattice(t.VertexX(c), t.VertexZ(c)), "corner " + c);
                var plots = Enumerable.Range(0, 9).Select(k => c * 9 + k).ToArray();
                foreach (int p in plots)
                {
                    double x = PlotLayout.PlotX(t, p), z = PlotLayout.PlotZ(t, p);
                    Assert.IsTrue(OnLattice(x, z), "plot " + p + " on the lattice");
                    int hex = PlotLayout.PlotHex(t, p);
                    Assert.AreEqual(hex, t.HexAt(x, z), "plot " + p + " inside its sector's hex");
                    double dc = Math.Sqrt(Math.Pow(x - t.HexX(hex), 2) + Math.Pow(z - t.HexZ(hex), 2));
                    Assert.GreaterOrEqual(dc + 1e-9, 15, "plot " + p + " clear of the stock patch");
                    for (int e = 0; e < t.HexEdgeCount; e++)
                    {
                        int a = t.HexEdgeCornerA(e), bb = t.HexEdgeCornerB(e);
                        Assert.GreaterOrEqual(SegDist(x, z, t.VertexX(a), t.VertexZ(a), t.VertexX(bb), t.VertexZ(bb)) + 1e-9, 16, "plot " + p + " to hex edge " + e);
                    }
                    foreach (int q in plots)
                        if (q != p)
                            Assert.GreaterOrEqual(Math.Sqrt(Math.Pow(x - PlotLayout.PlotX(t, q), 2) + Math.Pow(z - PlotLayout.PlotZ(t, q), 2)) + 1e-9, 17,
                                "plots " + p + " and " + q);
                }
            }
            Assert.Greater(corners, 50);
        }

        [Test]
        public void SlotPositions_PathsAndFacing()
        {
            var t = B.Topology;
            int c = 70;
            double cx = t.VertexX(c), cz = t.VertexZ(c);
            int hex = PlotLayout.SectorHex(t, c, 0);
            for (int slot = 0; slot < 3; slot++)
            {
                int p = PlotLayout.PlotId(c, 0, slot);
                double x = PlotLayout.PlotX(t, p), z = PlotLayout.PlotZ(t, p);
                double d = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(z - cz, 2));
                Assert.AreEqual(slot == 2 ? 45 : Math.Sqrt(30 * 30 + 75), d, 1e-9);
                var path = PlotLayout.PathPoints(t, p);
                Assert.AreEqual(slot == 2 ? 4 : 6, path.Length);
                Assert.AreEqual(cx, path[0], 1e-9);
                Assert.AreEqual(cz, path[1], 1e-9);
                Assert.AreEqual(x, path[path.Length - 2], 1e-9);
                Assert.AreEqual(z, path[path.Length - 1], 1e-9);
                Assert.AreEqual(slot == 2 ? 9 : 8, PlotLayout.LocalSteps(p));
                double yaw = PlotLayout.PlotYaw(t, p);
                Assert.That(yaw >= 0 && yaw < 360);
                if (slot == 2) Assert.AreEqual(PlotLayout.Yaw(cx - x, cz - z), yaw, 1e-9, "slot 2 faces the corner");
            }
            // Slot 0 is to the left of the spoke (counter-clockwise seen from above).
            double bx = t.HexX(hex) - cx, bz = t.HexZ(hex) - cz;
            int p0 = PlotLayout.PlotId(c, 0, 0);
            double cross = bx * (PlotLayout.PlotZ(t, p0) - cz) - bz * (PlotLayout.PlotX(t, p0) - cx);
            Assert.Greater(cross, 0);
        }

        [Test]
        public void InvalidPlots_AreSafe()
        {
            var t = B.Topology;
            Assert.AreEqual(-1, PlotLayout.PlotHex(t, -5));
            Assert.AreEqual(0, PlotLayout.PathPoints(t, 99999).Length);
            Assert.AreEqual(0, PlotLayout.PlotX(t, -1));
            Assert.IsFalse(PlotLayout.IsValidPlot(t, -1));
            var edge = t.Corners.First(c => !PlotLayout.IsSettlementCorner(t, c));
            Assert.IsFalse(PlotLayout.IsValidPlot(t, edge * 9));
        }

        [Test]
        public void PlotView_UsesOfTheHomeCamp()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var s = sim.State;
            Assert.AreEqual(PlotUse.Building, EconomyApi.Plot(s, 630).Use, "log pile");
            Assert.AreEqual(PlotUse.Free, EconomyApi.Plot(s, 631).Use);
            Assert.AreEqual(PlotUse.Locked, EconomyApi.Plot(s, 632).Use);
            Assert.AreEqual(PlotUse.Reserved, EconomyApi.Plot(s, 635).Use);
            Assert.AreEqual(PlotUse.NoSettlement, EconomyApi.Plot(s, 0).Use);
            Assert.AreEqual(0, EconomyApi.Plot(s, 631).SettlementId);
            Assert.AreEqual(6, EconomyApi.PlotsOf(s, 0).Length, "a settlement has 6 plots");
            s.Economy.Settlements[0].IsCity = true;
            Assert.AreEqual(8, EconomyApi.PlotsOf(s, 0).Length, "the home city has 8 (dance circle reserved)");
            Assert.AreEqual(9, EconomyApi.PlotsOf(s, 1).Length + 3, "the rival camp has 6");
        }

        // ---- outer town plots (PRD_AGES 5.3, H1-2) ----

        private static double Dist(BoardTopology t, int p, int q) =>
            Math.Sqrt(Math.Pow(PlotLayout.PlotX(t, p) - PlotLayout.PlotX(t, q), 2) + Math.Pow(PlotLayout.PlotZ(t, p) - PlotLayout.PlotZ(t, q), 2));

        private static int[] OuterPlots(BoardTopology t, int corner)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int sec = 0; sec < 3; sec++)
                for (int slot = 3; slot <= 4; slot++) list.Add(PlotLayoutOuter.OuterPlotId(corner, sec, slot));
            return list.ToArray();
        }

        [Test]
        public void Outer_IdsAreFoldedIntoPlotLayout()
        {
            var t = B.Topology;
            Assert.AreEqual(1350, PlotLayoutOuter.OuterBase);
            Assert.AreEqual(1350 + 70 * 6 + 1 * 2 + 1, PlotLayoutOuter.OuterPlotId(70, 1, 4));
            foreach (int c in t.Corners)
            {
                bool settle = PlotLayout.IsSettlementCorner(t, c);
                foreach (int p in OuterPlots(t, c))
                {
                    Assert.AreEqual(settle, PlotLayout.IsValidPlot(t, p), "outer plot " + p + " of corner " + c);
                    if (!settle) continue;
                    Assert.AreEqual(c, PlotLayout.CornerOf(p));
                    Assert.AreEqual(PlotLayoutOuter.SectorOf(p), PlotLayout.SectorOf(p));
                    Assert.AreEqual(PlotLayoutOuter.SlotOf(p), PlotLayout.SlotOf(p));
                    Assert.That(PlotLayout.SlotOf(p) == 3 || PlotLayout.SlotOf(p) == 4);
                    Assert.AreEqual(PlotLayout.SectorHex(t, c, PlotLayout.SectorOf(p)), PlotLayout.PlotHex(t, p));
                    Assert.IsTrue(PlotLayoutOuter.Centre(t, p, out double x, out double z));
                    Assert.AreEqual(x, PlotLayout.PlotX(t, p), 1e-9);
                    Assert.AreEqual(z, PlotLayout.PlotZ(t, p), 1e-9);
                    Assert.AreEqual(PlotLayoutOuter.Yaw(t, p), PlotLayout.PlotYaw(t, p), 1e-9);
                    var path = PlotLayout.PathPoints(t, p);
                    Assert.AreEqual(6, path.Length);
                    Assert.AreEqual(t.VertexX(c), path[0], 1e-9);
                    Assert.AreEqual(x, path[4], 1e-9);
                    Assert.AreEqual(13, PlotLayout.LocalSteps(p));
                }
            }
            // The existing ids and counts did not move.
            Assert.AreEqual(1350, PlotLayout.PlotCount(t));
            Assert.IsFalse(PlotLayout.IsValidPlot(t, PlotLayoutOuter.OuterBase + 150 * 6));
            Assert.IsFalse(PlotLayout.IsValidPlot(t, 99999));
            Assert.AreEqual(0, PlotLayout.PathPoints(t, 99999).Length);
        }

        [Test]
        public void Outer_Geometry_45MetresAlongTheSpoke_17ToTheSide_Slot3Left_Slot4Right()
        {
            var t = B.Topology;
            int c = 70;
            double cx = t.VertexX(c), cz = t.VertexZ(c);
            for (int sec = 0; sec < 3; sec++)
            {
                int hex = PlotLayout.SectorHex(t, c, sec);
                double bx = (t.HexX(hex) - cx) / BoardTopology.HexEdgeLength, bz = (t.HexZ(hex) - cz) / BoardTopology.HexEdgeLength;
                foreach (int slot in new[] { 3, 4 })
                {
                    int p = PlotLayoutOuter.OuterPlotId(c, sec, slot);
                    double dx = PlotLayout.PlotX(t, p) - cx, dz = PlotLayout.PlotZ(t, p) - cz;
                    double along = dx * bx + dz * bz;
                    double side = bx * dz - bz * dx; // positive = left of the spoke
                    Assert.AreEqual(45, along, 1e-9);
                    Assert.AreEqual(slot == 3 ? 17 : -17, side, 1e-9);
                    Assert.AreEqual(hex, t.HexAt(PlotLayout.PlotX(t, p), PlotLayout.PlotZ(t, p)), "inside its sector's hex");
                    double yaw = PlotLayout.PlotYaw(t, p);
                    double want = PlotLayout.Yaw(cx + 45 * bx - PlotLayout.PlotX(t, p), cz + 45 * bz - PlotLayout.PlotZ(t, p));
                    Assert.AreEqual(want, yaw, 1e-9, "faces the spoke point");
                }
            }
        }

        [Test]
        public void Outer_Spacing_ForEveryCorner_OwnPlotsEdgesAndHexCentre()
        {
            var t = B.Topology;
            int corners = 0, plots = 0;
            double minOwnInner = 1e9, minOwnOuter = 1e9, minEdge = 1e9, minCentre = 1e9, minStore = 1e9;
            foreach (int c in t.Corners)
            {
                if (!PlotLayout.IsSettlementCorner(t, c)) continue;
                corners++;
                var outer = OuterPlots(t, c);
                Assert.AreEqual(6, outer.Length);
                foreach (int p in outer)
                {
                    plots++;
                    double x = PlotLayout.PlotX(t, p), z = PlotLayout.PlotZ(t, p);
                    int hex = PlotLayout.PlotHex(t, p);
                    minCentre = Math.Min(minCentre, Math.Sqrt(Math.Pow(x - t.HexX(hex), 2) + Math.Pow(z - t.HexZ(hex), 2)));
                    minStore = Math.Min(minStore, Math.Sqrt(Math.Pow(x - t.VertexX(c), 2) + Math.Pow(z - t.VertexZ(c), 2)));
                    for (int k = 0; k < 9; k++) minOwnInner = Math.Min(minOwnInner, Dist(t, p, c * 9 + k));
                    foreach (int q in outer) if (q != p) minOwnOuter = Math.Min(minOwnOuter, Dist(t, p, q));
                    for (int e = 0; e < t.HexEdgeCount; e++)
                    {
                        int a = t.HexEdgeCornerA(e), bb = t.HexEdgeCornerB(e);
                        minEdge = Math.Min(minEdge, SegDist(x, z, t.VertexX(a), t.VertexZ(a), t.VertexX(bb), t.VertexZ(bb)));
                    }
                }
            }
            Assert.Greater(corners, 50);
            Assert.AreEqual(corners * 6, plots);
            TestContext.WriteLine("outer plots: own inner " + minOwnInner + ", own outer " + minOwnOuter + ", hex edges (main roads) " + minEdge
                + ", hex centre " + minCentre + ", storehouse " + minStore);
            Assert.GreaterOrEqual(minOwnInner + 1e-9, 17, "17 m from the camp's own nine plots");
            Assert.GreaterOrEqual(minOwnOuter + 1e-9, 17, "17 m from its own other outer plots");
            Assert.GreaterOrEqual(minEdge + 1e-9, 16, "as clear of every hex edge (main road) as the inner plots");
            Assert.GreaterOrEqual(minCentre + 1e-9, 15, "clear of the stock patch");
            Assert.GreaterOrEqual(minStore + 1e-9, 17, "clear of the storehouse");
        }

        [Test]
        public void Outer_Spacing_AgainstEveryOtherSettlementCorner_AndTheRivalsRule()
        {
            var t = B.Topology;
            var corners = t.Corners.Where(c => PlotLayout.IsSettlementCorner(t, c)).ToArray();
            double minInner = 1e9, minStore = 1e9, minOuterNonRival = 1e9;
            int rivalPairs = 0;
            foreach (int a in corners)
                foreach (int b in corners)
                {
                    if (a == b || t.CornerDistance(a, b) < 2) continue; // camps are at least a road apart (too_close)
                    foreach (int p in OuterPlots(t, a))
                    {
                        var rivals = PlotLayoutOuter.Rivals(t, p);
                        double px = PlotLayout.PlotX(t, p), pz = PlotLayout.PlotZ(t, p);
                        minStore = Math.Min(minStore, Math.Sqrt(Math.Pow(px - t.VertexX(b), 2) + Math.Pow(pz - t.VertexZ(b), 2)));
                        for (int k = 0; k < 9; k++) minInner = Math.Min(minInner, Dist(t, p, b * 9 + k));
                        foreach (int q in OuterPlots(t, b))
                        {
                            double d = Dist(t, p, q);
                            if (d < PlotLayoutOuter.MinSpacing)
                            {
                                CollectionAssert.Contains(rivals, q, "a close pair is listed as rivals: " + p + " and " + q);
                                rivalPairs++;
                            }
                            else
                            {
                                CollectionAssert.DoesNotContain(rivals, q);
                                minOuterNonRival = Math.Min(minOuterNonRival, d);
                            }
                        }
                    }
                }
            TestContext.WriteLine("outer vs other camps: inner plots " + minInner + ", storehouse " + minStore + ", outer (not rivals) " + minOuterNonRival + ", rival pairs " + rivalPairs);
            Assert.GreaterOrEqual(minInner + 1e-9, 17, "an outer plot is 17 m from every plot of any other camp");
            Assert.GreaterOrEqual(minStore + 1e-9, 17, "and from any other camp's storehouse");
            Assert.GreaterOrEqual(minOuterNonRival + 1e-9, PlotLayoutOuter.MinSpacing);
            Assert.Greater(rivalPairs, 0, "the one known conflict exists: two camps two edges apart on one hex");
        }

        [Test]
        public void Outer_Rivals_AreSymmetric_AndOnlyBetweenCampsTwoEdgesApart()
        {
            int legal = 0;
            double closest = 1e9;
            var t = B.Topology;
            foreach (int c in t.Corners.Where(k => PlotLayout.IsSettlementCorner(t, k)))
                foreach (int p in OuterPlots(t, c))
                    foreach (int q in PlotLayoutOuter.Rivals(t, p))
                    {
                        CollectionAssert.Contains(PlotLayoutOuter.Rivals(t, q), p, "symmetric");
                        int other = PlotLayoutOuter.CornerOf(q);
                        Assert.LessOrEqual(t.CornerDistance(c, other), 2, "only camps one or two edges apart can clash (one apart cannot both be camps)");
                        if (t.CornerDistance(c, other) == 2) { legal++; closest = Math.Min(closest, Dist(t, p, q)); }
                    }
            TestContext.WriteLine("legal rival pairs (camps two edges apart): " + legal + ", closest " + closest + " m");
            Assert.Greater(legal, 0);
            Assert.Less(closest, 15);
            Assert.AreEqual(0, PlotLayoutOuter.Rivals(t, 5).Count);
        }
    }
}
