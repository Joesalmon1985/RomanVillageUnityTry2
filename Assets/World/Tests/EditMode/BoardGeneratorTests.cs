using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace World.Sim.Tests
{
    public class BoardGeneratorTests
    {
        private static readonly ulong[] Seeds = { 0, 1, 2, 42, 20261113, ulong.MaxValue };

        private static IEnumerable<Terrain> Terrains(Board b, int v) => b.Topology.VertexHexes(v).Select(h => b.HexTerrain[h]);

        /// <summary>Hexes touching any corner within d hex edges of the given corner.</summary>
        private static HashSet<int> HexesWithin(Board b, int corner, int d)
        {
            var t = b.Topology;
            var set = new HashSet<int>();
            foreach (int c in t.Corners)
                if (t.CornerDistance(corner, c) <= d)
                    foreach (int h in t.VertexHexes(c)) set.Add(h);
            return set;
        }

        [Test]
        public void MvpBoard_Radius4_61Hexes()
        {
            var b = BoardGenerator.CreateMvpBoard(1);
            Assert.AreEqual(4, b.Radius);
            Assert.AreEqual(61, b.HexTerrain.Length);
            Assert.AreEqual(61, b.HexNumber.Length);
            Assert.AreEqual(60.0, BoardTopology.HexEdgeLength);
        }

        [Test]
        public void StartCorners_70And97_ForestHillsFields_FixedNumbers([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            var t = b.Topology;
            CollectionAssert.AreEqual(new[] { 70, 97 }, b.StartCorners);
            foreach (int c in b.StartCorners)
            {
                var hexes = t.VertexHexes(c);
                Assert.AreEqual(3, hexes.Count, "interior corner");
                CollectionAssert.AreEquivalent(new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields }, hexes.Select(h => b.HexTerrain[h]).ToArray());
                foreach (int h in hexes)
                {
                    int expected = b.HexTerrain[h] == Terrain.Forest ? 6 : b.HexTerrain[h] == Terrain.Hills ? 8 : 5;
                    Assert.AreEqual(expected, b.HexNumber[h], "start hex " + h + " " + b.HexTerrain[h]);
                }
            }
            int home = b.StartCorners[0];
            double r = System.Math.Sqrt(t.VertexX(home) * t.VertexX(home) + t.VertexZ(home) * t.VertexZ(home));
            Assert.LessOrEqual(r, BoardTopology.HexEdgeLength + 1e-9, "home corner is a corner of the centre hex");
        }

        [Test]
        public void WithinTwoHexEdgesOfHome_MoreForestHillsFieldsAndPasture()
        {
            var b = BoardGenerator.CreateMvpBoard(7);
            var home = b.Topology.VertexHexes(b.StartCorners[0]);
            var near = HexesWithin(b, b.StartCorners[0], 2).Where(h => !home.Contains(h)).Select(h => b.HexTerrain[h]).ToList();
            foreach (var terr in new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields, Terrain.Pasture })
                CollectionAssert.Contains(near, terr, terr + " within two hex edges of home");
        }

        [Test]
        public void Mountains_ARangeOutsideHomeTerritory_NextToSecondCitySites()
        {
            var b = BoardGenerator.CreateMvpBoard(3);
            var t = b.Topology;
            var mountains = Enumerable.Range(0, t.HexCount).Where(h => b.HexTerrain[h] == Terrain.Mountain).ToList();
            Assert.GreaterOrEqual(mountains.Count, 4);
            // One connected range of at least 4 hexes.
            var start = mountains[0];
            var seen = new HashSet<int> { start };
            var stack = new Stack<int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var c = t.Hex(stack.Pop());
                for (int d = 0; d < 6; d++)
                {
                    int n = t.HexIndex(c.Neighbour(d));
                    if (n >= 0 && b.HexTerrain[n] == Terrain.Mountain && seen.Add(n)) stack.Push(n);
                }
            }
            Assert.GreaterOrEqual(seen.Count, 4, "mountain range");
            // No flag of the home territory (4 segments) touches a Mountain.
            for (int v = 0; v < t.VertexCount; v++)
                if (t.Distance(b.StartCorners[0], v) <= 4)
                    Assert.IsFalse(Terrains(b, v).Contains(Terrain.Mountain), "home territory flag " + v + " touches a mountain");
            // At least two good city corners 2-3 hex edges from home touch the range.
            var sites = t.Corners.Where(v =>
            {
                int d = t.CornerDistance(b.StartCorners[0], v);
                return d >= 2 && d <= 3 && t.VertexHexes(v).Count == 3 && Terrains(b, v).Contains(Terrain.Mountain)
                    && Terrains(b, v).Where(x => x != Terrain.Meadow).Distinct().Count() >= 2;
            }).ToList();
            Assert.GreaterOrEqual(sites.Count, 2, "mountain city corners 2-3 hex edges from home");
            Assert.IsTrue(sites.Any(v => b.SuggestedSettlementCorners.Contains(v)), "one of them is suggested");
        }

        [Test]
        public void RivalStart_FourToFiveHexEdgesAway_WithItsOwnForestHillsFields()
        {
            var b = BoardGenerator.CreateMvpBoard(5);
            var t = b.Topology;
            int d = t.CornerDistance(b.StartCorners[0], b.StartCorners[1]);
            Assert.That(d >= 4 && d <= 5, "distance " + d);
            CollectionAssert.AreEquivalent(new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields }, Terrains(b, b.StartCorners[1]).ToArray());
            Assert.IsEmpty(t.VertexHexes(b.StartCorners[1]).Intersect(t.VertexHexes(b.StartCorners[0])), "its own hexes");
        }

        [Test]
        public void SuggestedSettlementCorners_EightOrMore_LegalSeparatedMixed()
        {
            var b = BoardGenerator.CreateMvpBoard(9);
            var t = b.Topology;
            var s = b.SuggestedSettlementCorners;
            Assert.GreaterOrEqual(s.Length, 8);
            Assert.AreEqual(s.Length, s.Distinct().Count());
            foreach (int v in s)
            {
                Assert.IsTrue(t.IsCorner(v));
                Assert.AreEqual(3, t.VertexHexes(v).Count, "interior");
                Assert.GreaterOrEqual(t.CornerDistance(v, b.StartCorners[0]), 2, "not next to home");
                Assert.GreaterOrEqual(t.CornerDistance(v, b.StartCorners[1]), BoardGenerator.SuggestedSiteSpacing);
                Assert.GreaterOrEqual(Terrains(b, v).Where(x => x != Terrain.Meadow).Distinct().Count(), 2, "mixed economy at " + v);
                foreach (int w in s) if (w != v) Assert.GreaterOrEqual(t.CornerDistance(v, w), BoardGenerator.SuggestedSiteSpacing);
            }
            for (int i = 1; i < s.Length; i++)
                Assert.LessOrEqual(t.CornerDistance(b.StartCorners[0], s[i - 1]), t.CornerDistance(b.StartCorners[0], s[i]), "nearest first");
            Assert.IsTrue(s.Any(v => t.CornerDistance(b.StartCorners[0], v) <= 3), "a second city site close to home");
        }

        [Test]
        public void ReservedPlot_IsTheDanceCircle_HomeFieldsSectorSlot2([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            var t = b.Topology;
            CollectionAssert.AreEqual(new[] { 635 }, b.ReservedPlots);
            int p = b.ReservedPlots[0];
            Assert.AreEqual(b.StartCorners[0], PlotLayout.CornerOf(p));
            Assert.AreEqual(PlotLayout.CitySlot, PlotLayout.SlotOf(p));
            Assert.AreEqual(Terrain.Fields, b.HexTerrain[PlotLayout.PlotHex(t, p)]);
        }

        [Test]
        public void OuterRing_MostlyMeadow_WithSomeVariety()
        {
            var b = BoardGenerator.CreateMvpBoard(1);
            var t = b.Topology;
            var ring = Enumerable.Range(0, t.HexCount).Where(h => t.Hex(h).Ring == 4).Select(h => b.HexTerrain[h]).ToList();
            Assert.AreEqual(24, ring.Count);
            Assert.GreaterOrEqual(ring.Count(x => x == Terrain.Meadow), 16);
            Assert.GreaterOrEqual(ring.Distinct().Count(), 2);
        }

        [Test]
        public void Numbers_TwoToTwelve_DealtFromThePools([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            var t = b.Topology;
            var startHexes = new HashSet<int>(b.StartCorners.SelectMany(c => t.VertexHexes(c)));
            var mountains = new List<int>();
            var others = new List<int>();
            for (int h = 0; h < 61; h++)
            {
                if (b.HexTerrain[h] == Terrain.Meadow) { Assert.AreEqual(0, b.HexNumber[h]); continue; }
                Assert.That(b.HexNumber[h] >= 2 && b.HexNumber[h] <= 12 && b.HexNumber[h] != 7, "hex " + h + " has " + b.HexNumber[h]);
                if (startHexes.Contains(h)) continue;
                if (b.HexTerrain[h] == Terrain.Mountain) mountains.Add(b.HexNumber[h]);
                else others.Add(b.HexNumber[h]);
            }
            Assert.AreEqual(6, startHexes.Count);
            CollectionAssert.AreEquivalent(new[] { 5, 6, 9, 10 }, mountains);
            Assert.AreEqual(28, others.Count);
            CollectionAssert.AreEquivalent(BoardGenerator.NumberPool, others);
        }

        [Test]
        public void Numbers_DependOnTheSeed()
        {
            var seen = new HashSet<string>();
            foreach (var seed in Seeds) seen.Add(string.Join(",", BoardGenerator.CreateMvpBoard(seed).HexNumber));
            Assert.Greater(seen.Count, 1);
        }

        [Test]
        public void EveryResourceTerrain_YieldsItsRawWare_AndBack()
        {
            Assert.AreEqual(Ware.Wood, TerrainInfo.RawWare(Terrain.Forest));
            Assert.AreEqual(Ware.Clay, TerrainInfo.RawWare(Terrain.Hills));
            Assert.AreEqual(Ware.Grain, TerrainInfo.RawWare(Terrain.Fields));
            Assert.AreEqual(Ware.Sheep, TerrainInfo.RawWare(Terrain.Pasture));
            Assert.AreEqual(Ware.Stone, TerrainInfo.RawWare(Terrain.Mountain));
            Assert.IsNull(TerrainInfo.RawWare(Terrain.Meadow));
            Assert.IsTrue(TerrainInfo.IsResource(Terrain.Pasture));
            foreach (var w in Wares.Raw) Assert.AreEqual(w, TerrainInfo.RawWare(TerrainInfo.TerrainOf(w)));
            Assert.AreEqual(Terrain.Meadow, TerrainInfo.TerrainOf(Ware.Planks));
            Assert.AreEqual(Wares.RawCount, Wares.Raw.Length);
            Assert.AreEqual(Wares.Count, Wares.All.Length);
            Assert.AreEqual(10, Wares.Count);
            Assert.AreEqual(5, Wares.RawCount);
            Assert.AreEqual("Cut stone", Wares.Title(Ware.CutStone));
            Assert.AreEqual("cut stone", Wares.DisplayName(Ware.CutStone));
            Assert.AreEqual(9, (int)Ware.CutStone);
        }

        [Test]
        public void SameSeed_SameBoard_TerrainFixedAcrossSeeds()
        {
            var a = BoardGenerator.CreateMvpBoard(77);
            var c = BoardGenerator.CreateMvpBoard(77);
            CollectionAssert.AreEqual(a.HexNumber, c.HexNumber);
            CollectionAssert.AreEqual(a.HexTerrain, BoardGenerator.CreateMvpBoard(78).HexTerrain);
            CollectionAssert.AreEqual(a.StartCorners, BoardGenerator.CreateMvpBoard(78).StartCorners);
            CollectionAssert.AreEqual(a.SuggestedSettlementCorners, BoardGenerator.CreateMvpBoard(78).SuggestedSettlementCorners);
            // Pinned layout ids (the Unity layer and docs refer to them).
            Assert.AreEqual(70, a.StartCorners[0]);
            Assert.AreEqual(97, a.StartCorners[1]);
        }
    }
}
