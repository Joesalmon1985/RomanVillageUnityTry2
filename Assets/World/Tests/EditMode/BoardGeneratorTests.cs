using System.Linq;
using NUnit.Framework;

namespace World.Sim.Tests
{
    public class BoardGeneratorTests
    {
        private static readonly ulong[] Seeds = { 0, 1, 2, 42, 20261113, ulong.MaxValue };

        [Test]
        public void MvpBoard_Radius2()
        {
            var b = BoardGenerator.CreateMvpBoard(1);
            Assert.AreEqual(2, b.Radius);
            Assert.AreEqual(19, b.HexTerrain.Length);
            Assert.AreEqual(19, b.HexNumber.Length);
        }

        [Test]
        public void HomeCity_InteriorVertex_TouchingForestHillsFields([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            var t = b.Topology;
            var hexes = t.VertexHexes(b.HomeCityVertex);
            Assert.AreEqual(3, hexes.Count, "interior vertex");
            var terr = hexes.Select(h => b.HexTerrain[h]).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields }, terr);
            Assert.IsFalse(hexes.Any(h => b.HexTerrain[h] == Terrain.Mountain), "no mountain at home");
            Assert.AreEqual(3, hexes.Select(h => b.HexNumber[h]).Distinct().Count(), "home hexes have distinct numbers");
        }

        [Test]
        public void TerrainCounts_AndNumbers([ValueSource(nameof(Seeds))] ulong seed)
        {
            var b = BoardGenerator.CreateMvpBoard(seed);
            foreach (var t in new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields, Terrain.Mountain })
                Assert.GreaterOrEqual(b.HexTerrain.Count(x => x == t), 2, t.ToString());
            for (int h = 0; h < 19; h++)
            {
                if (b.HexTerrain[h] == Terrain.Meadow) Assert.AreEqual(0, b.HexNumber[h]);
                else Assert.That(b.HexNumber[h] >= 1 && b.HexNumber[h] <= 4);
            }
            for (int n = 1; n <= 4; n++)
                Assert.GreaterOrEqual(b.HexNumber.Count(x => x == n), 2, "number " + n + " appears on several hexes");
        }

        [Test]
        public void Mountain_ReachableOnlyFromASecondCity()
        {
            var b = BoardGenerator.CreateMvpBoard(3);
            var t = b.Topology;
            // Some vertex touches a mountain, and none of them is the home city.
            var mountainVerts = Enumerable.Range(0, t.VertexCount)
                .Where(v => t.VertexHexes(v).Any(h => b.HexTerrain[h] == Terrain.Mountain)).ToList();
            Assert.IsNotEmpty(mountainVerts);
            CollectionAssert.DoesNotContain(mountainVerts, b.HomeCityVertex);
        }

        [Test]
        public void Suggestions_Valid()
        {
            var b = BoardGenerator.CreateMvpBoard(5);
            var t = b.Topology;
            Assert.AreEqual(b.HomeCityVertex, b.StorehouseVertex);
            Assert.AreEqual(b.HomeCityVertex, b.MarketVertex);
            Assert.GreaterOrEqual(t.EdgeBetween(b.HomeCityVertex, b.WarehouseVertex), 0, "warehouse next to home");
            Assert.That(b.RivalOutpostVertex >= 0 && b.RivalOutpostVertex != b.HomeCityVertex);
            Assert.GreaterOrEqual(t.Distance(b.HomeCityVertex, b.RivalOutpostVertex), 6, "rival is far from home");
            Assert.IsFalse(t.VertexHexes(b.RivalOutpostVertex).Any(h => b.HexTerrain[h] == Terrain.Mountain));
        }

        [Test]
        public void SameSeed_SameBoard_TerrainFixedAcrossSeeds()
        {
            var a = BoardGenerator.CreateMvpBoard(77);
            var c = BoardGenerator.CreateMvpBoard(77);
            CollectionAssert.AreEqual(a.HexNumber, c.HexNumber);
            CollectionAssert.AreEqual(a.HexTerrain, BoardGenerator.CreateMvpBoard(78).HexTerrain);
            Assert.AreEqual(a.HomeCityVertex, BoardGenerator.CreateMvpBoard(78).HomeCityVertex);
        }
    }
}
