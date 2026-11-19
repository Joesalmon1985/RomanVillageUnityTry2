using System.Linq;
using NUnit.Framework;
using UnityEngine;
using World.Game;
using World.Sim;
using Terrain = World.Sim.Terrain;

namespace World.Game.Tests
{
    /// <summary>G1-4: abundance dressing by pip level (AB-2, AB-5), pulse (AB-3), ports (AB-4). Pure view; no scene needed.</summary>
    public class HexDressingTests
    {
        private static Board Row(Terrain terrain)
        {
            var src = BoardGenerator.CreateDealtBoard(1UL);
            var t = src.Topology;
            var b = new Board { Radius = src.Radius, HexTerrain = new Terrain[t.HexCount], HexNumber = new int[t.HexCount] };
            int[] nums = { 2, 3, 4, 5, 6 };
            for (int q = -2; q <= 2; q++) { int h = t.HexIndex(new HexCoord(q, 0)); b.HexTerrain[h] = terrain; b.HexNumber[h] = nums[q + 2]; }
            return b;
        }

        private static int[] Counts(Terrain terrain)
        {
            var b = Row(terrain);
            var t = b.Topology;
            var res = new int[5];
            for (int q = -2; q <= 2; q++)
            {
                int h = t.HexIndex(new HexCoord(q, 0));
                var c = BoardSpace.Hex(t, h);
                var mb = new MeshBuilder();
                res[q + 2] = HexDressing.Fill(mb, b, h, (p, e, r) => TerrainBuilder.EdgeMargin(p - c, BoardSpace.HexSize) >= e && (p - c).magnitude > HexDressing.CentreClear + r, true);
            }
            return res;
        }

        [TestCase(Terrain.Forest)]
        [TestCase(Terrain.Fields)]
        [TestCase(Terrain.Pasture)]
        public void Items_GrowWithPips(Terrain terrain)
        {
            var c = Counts(terrain);
            for (int i = 1; i < 5; i++) Assert.Greater(c[i], c[i - 1], terrain + " level " + i + " counts " + string.Join(",", c));
        }

        [Test]
        public void Hills_And_Mountain_LevelsGrow()
        {
            for (int i = 1; i < 5; i++)
            {
                Assert.Greater(HexDressing.CliffRadius[i], HexDressing.CliffRadius[i - 1]);
                Assert.Greater(HexDressing.CliffHeight[i], HexDressing.CliffHeight[i - 1]);
                Assert.Greater(HexDressing.PeakHeights[i], HexDressing.PeakHeights[i - 1]);
            }
            Assert.GreaterOrEqual(Counts(Terrain.Hills).Min(), 1);
        }

        [Test]
        public void Meadow_And_NumberlessHexes_AddNothing()
        {
            var b = BoardGenerator.CreateDealtBoard(3UL);
            int h = Enumerable.Range(0, b.HexTerrain.Length).First(i => b.HexTerrain[i] == Terrain.Meadow);
            Assert.AreEqual(0, HexDressing.Fill(new MeshBuilder(), b, h, (p, e, r) => true, true));
        }

        [Test]
        public void Preview_BuildsFromDealtBoard_WithPortsAndPulse()
        {
            var b = BoardGenerator.CreateDealtBoard(7UL);
            var v = HexDressingView.Create(b, null);
            try
            {
                Assert.IsNotNull(v.transform.Find("Ports"));
                int h = Enumerable.Range(0, b.HexTerrain.Length).First(i => b.HexTerrain[i] != Terrain.Meadow);
                HexDressingView.Pulse(new[] { h, -5, 9999 });
                CollectionAssert.AreEqual(new[] { h }, HexDressingView.PulsingNow.ToArray());
            }
            finally { Object.DestroyImmediate(v.gameObject); }
        }
    }
}
