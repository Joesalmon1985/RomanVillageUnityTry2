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
    }
}
