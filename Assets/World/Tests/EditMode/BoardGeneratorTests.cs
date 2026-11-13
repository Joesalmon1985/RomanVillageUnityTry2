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
        public void HomeCity_CornerNearCentre_TouchingForestHillsFields([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            var t = b.Topology;
            Assert.IsTrue(t.IsCorner(b.HomeCityVertex));
            var hexes = t.VertexHexes(b.HomeCityVertex);
            Assert.AreEqual(3, hexes.Count, "interior corner");
            CollectionAssert.AreEquivalent(new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields }, hexes.Select(h => b.HexTerrain[h]).ToArray());
            Assert.AreEqual(3, hexes.Select(h => b.HexNumber[h]).Distinct().Count(), "home hexes have distinct numbers");
            double r = System.Math.Sqrt(t.VertexX(b.HomeCityVertex) * t.VertexX(b.HomeCityVertex) + t.VertexZ(b.HomeCityVertex) * t.VertexZ(b.HomeCityVertex));
            Assert.LessOrEqual(r, BoardTopology.HexEdgeLength + 1e-9, "home corner is a corner of the centre hex");
        }

        [Test]
        public void WithinTwoHexEdgesOfHome_MoreForestHillsFieldsAndPasture()
        {
            var b = BoardGenerator.CreateMvpBoard(7);
            var home = b.Topology.VertexHexes(b.HomeCityVertex);
            var near = HexesWithin(b, b.HomeCityVertex, 2).Where(h => !home.Contains(h)).Select(h => b.HexTerrain[h]).ToList();
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
                if (t.Distance(b.HomeCityVertex, v) <= 4)
                    Assert.IsFalse(Terrains(b, v).Contains(Terrain.Mountain), "home territory flag " + v + " touches a mountain");
            // At least two good city corners 2-3 hex edges from home touch the range.
            var sites = t.Corners.Where(v =>
            {
                int d = t.CornerDistance(b.HomeCityVertex, v);
                return d >= 2 && d <= 3 && t.VertexHexes(v).Count == 3 && Terrains(b, v).Contains(Terrain.Mountain)
                    && Terrains(b, v).Where(x => x != Terrain.Meadow).Distinct().Count() >= 2;
            }).ToList();
            Assert.GreaterOrEqual(sites.Count, 2, "mountain city corners 2-3 hex edges from home");
            Assert.IsTrue(sites.Any(v => b.SuggestedCitySites.Contains(v)), "one of them is suggested");
        }

        [Test]
        public void RivalOutpost_FourToFiveHexEdgesAway_WithItsOwnForestHillsFields()
        {
            var b = BoardGenerator.CreateMvpBoard(5);
            var t = b.Topology;
            int d = t.CornerDistance(b.HomeCityVertex, b.RivalOutpostVertex);
            Assert.That(d >= 4 && d <= 5, "distance " + d);
            CollectionAssert.AreEquivalent(new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields }, Terrains(b, b.RivalOutpostVertex).ToArray());
            Assert.IsEmpty(t.VertexHexes(b.RivalOutpostVertex).Intersect(t.VertexHexes(b.HomeCityVertex)), "its own hexes");
        }

        [Test]
        public void SuggestedCitySites_EightOrMore_LegalSeparatedMixed()
        {
            var b = BoardGenerator.CreateMvpBoard(9);
            var t = b.Topology;
            var s = b.SuggestedCitySites;
            Assert.GreaterOrEqual(s.Length, 8);
            Assert.AreEqual(s.Length, s.Distinct().Count());
            foreach (int v in s)
            {
                Assert.IsTrue(t.IsCorner(v));
                Assert.AreEqual(3, t.VertexHexes(v).Count, "interior");
                Assert.GreaterOrEqual(t.CornerDistance(v, b.HomeCityVertex), 2, "not next to home");
                Assert.GreaterOrEqual(t.CornerDistance(v, b.RivalOutpostVertex), BoardGenerator.SuggestedSiteSpacing);
                Assert.GreaterOrEqual(Terrains(b, v).Where(x => x != Terrain.Meadow).Distinct().Count(), 2, "mixed economy at " + v);
                foreach (int w in s) if (w != v) Assert.GreaterOrEqual(t.CornerDistance(v, w), BoardGenerator.SuggestedSiteSpacing);
            }
            for (int i = 1; i < s.Length; i++)
                Assert.LessOrEqual(t.CornerDistance(b.HomeCityVertex, s[i - 1]), t.CornerDistance(b.HomeCityVertex, s[i]), "nearest first");
            Assert.IsTrue(s.Any(v => t.CornerDistance(b.HomeCityVertex, v) <= 3), "a second city site close to home");
        }

        [Test]
        public void WarehouseFlag_BesideTheSquare()
        {
            var b = BoardGenerator.CreateMvpBoard(5);
            var t = b.Topology;
            Assert.AreEqual(b.HomeCityVertex, b.StorehouseVertex);
            Assert.AreEqual(b.HomeCityVertex, b.MarketVertex);
            Assert.IsFalse(t.IsCorner(b.WarehouseVertex), "an intermediate flag");
            int d = t.Distance(b.HomeCityVertex, b.WarehouseVertex);
            Assert.That(d >= 2 && d <= 3, "2-3 segments from home, was " + d);
            Assert.IsFalse(Terrains(b, b.WarehouseVertex).Contains(Terrain.Forest), "off the demonstration road's forest edge");
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
        public void Numbers_OneToFour_EvenlyPerTerrain([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            foreach (var terr in new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields, Terrain.Mountain, Terrain.Pasture })
            {
                var nums = Enumerable.Range(0, 61).Where(h => b.HexTerrain[h] == terr).Select(h => b.HexNumber[h]).ToList();
                Assert.GreaterOrEqual(nums.Count, 4, terr.ToString());
                var counts = Enumerable.Range(1, 4).Select(n => nums.Count(x => x == n)).ToList();
                Assert.LessOrEqual(counts.Max() - counts.Min(), 1, terr + " numbers spread evenly");
            }
            for (int h = 0; h < 61; h++)
            {
                if (b.HexTerrain[h] == Terrain.Meadow) Assert.AreEqual(0, b.HexNumber[h]);
                else Assert.That(b.HexNumber[h] >= 1 && b.HexNumber[h] <= 4);
            }
        }

        [Test]
        public void Pasture_YieldsNoWareYet()
        {
            Assert.IsNull(TerrainInfo.RawWare(Terrain.Pasture));
            Assert.IsTrue(TerrainInfo.IsResource(Terrain.Pasture));
            Assert.AreEqual(Ware.Timber, TerrainInfo.RawWare(Terrain.Forest));
        }

        [Test]
        public void SameSeed_SameBoard_TerrainFixedAcrossSeeds()
        {
            var a = BoardGenerator.CreateMvpBoard(77);
            var c = BoardGenerator.CreateMvpBoard(77);
            CollectionAssert.AreEqual(a.HexNumber, c.HexNumber);
            CollectionAssert.AreEqual(a.HexTerrain, BoardGenerator.CreateMvpBoard(78).HexTerrain);
            Assert.AreEqual(a.HomeCityVertex, BoardGenerator.CreateMvpBoard(78).HomeCityVertex);
            CollectionAssert.AreEqual(a.SuggestedCitySites, BoardGenerator.CreateMvpBoard(78).SuggestedCitySites);
            // Pinned layout ids (the Unity layer and docs refer to them).
            Assert.AreEqual(70, a.HomeCityVertex);
        }
    }
}
