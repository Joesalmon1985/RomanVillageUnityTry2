using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>The god board deal (PRD_GODBOARD 4.0, BD-1 to BD-6; G0-1).</summary>
    public class BoardDealTests
    {
        private static readonly ulong[] Seeds = { 0, 1, 2, 7, 42, 1234, 20261119, ulong.MaxValue };

        /// <summary>Seeds for the statistical checks (fairness rate, port shuffle).</summary>
        private const int ManySeeds = 1000;

        private static int Pips(int n) => n == 2 || n == 12 ? 1 : n == 3 || n == 11 ? 2 : n == 4 || n == 10 ? 3 : n == 5 || n == 9 ? 4 : n == 6 || n == 8 ? 5 : 0;

        private static bool IsRaw(Terrain t) => t != Terrain.Meadow;

        /// <summary>BD-4 checked from scratch: every start touches three different raw terrains; summed pips within ±1.</summary>
        private static bool Fair(Board b)
        {
            var t = b.Topology;
            var sums = new List<int>();
            foreach (int c in b.StartCorners)
            {
                var ts = t.VertexHexes(c).Select(h => b.HexTerrain[h]).ToList();
                if (ts.Count != 3 || !ts.All(IsRaw) || ts.Distinct().Count() != 3) return false;
                sums.Add(t.VertexHexes(c).Sum(h => Pips(b.HexNumber[h])));
            }
            return sums.Max() - sums.Min() <= 1;
        }

        [Test]
        public void MeadowLayout_IsTheLegacyOne()
        {
            var legacy = BoardGenerator.CreateMvpBoard(0);
            foreach (var seed in Seeds)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                Assert.AreEqual(BoardGenerator.MvpRadius, b.Radius);
                Assert.AreEqual(61, b.HexTerrain.Length);
                for (int h = 0; h < b.HexTerrain.Length; h++)
                    Assert.AreEqual(legacy.HexTerrain[h] == Terrain.Meadow, b.HexTerrain[h] == Terrain.Meadow, "seed " + seed + " hex " + h);
                Assert.AreEqual(23, b.HexTerrain.Count(x => x == Terrain.Meadow));
            }
        }

        [Test]
        public void SevenHexesArePinned()
        {
            var pins = new Dictionary<HexCoord, Terrain>
            {
                { new HexCoord(0, 0), Terrain.Fields }, { new HexCoord(1, -1), Terrain.Forest }, { new HexCoord(1, 0), Terrain.Hills },
                { new HexCoord(-1, -1), Terrain.Forest }, { new HexCoord(3, -1), Terrain.Hills },
                { new HexCoord(2, 1), Terrain.Mountain }, { new HexCoord(1, 2), Terrain.Mountain },
            };
            foreach (var seed in Seeds)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                foreach (var kv in pins) Assert.AreEqual(kv.Value, b.HexTerrain[b.Topology.HexIndex(kv.Key)], "seed " + seed + " " + kv.Key);
            }
        }

        [Test]
        public void TileMix_IsTheDoubledClassicSet()
        {
            foreach (var seed in Seeds)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                Assert.AreEqual(8, b.HexTerrain.Count(x => x == Terrain.Forest), "Forest");
                Assert.AreEqual(8, b.HexTerrain.Count(x => x == Terrain.Pasture), "Pasture");
                Assert.AreEqual(8, b.HexTerrain.Count(x => x == Terrain.Fields), "Fields");
                Assert.AreEqual(7, b.HexTerrain.Count(x => x == Terrain.Hills), "Hills");
                Assert.AreEqual(7, b.HexTerrain.Count(x => x == Terrain.Mountain), "Mountain");
            }
        }

        [Test]
        public void TerrainIsDealt_NotFixed()
        {
            var distinct = new HashSet<string>();
            for (ulong seed = 0; seed < 20; seed++) distinct.Add(string.Join(",", BoardGenerator.CreateDealtBoard(seed).HexTerrain));
            Assert.Greater(distinct.Count, 15, "different seeds deal different terrain");
        }

        [Test]
        public void Numbers_AreTheBd3Pool_OnEveryResourceHex()
        {
            var expected = new List<int>();
            void Add(int n, int k) { for (int i = 0; i < k; i++) expected.Add(n); }
            Add(2, 2); Add(12, 2);
            foreach (int n in new[] { 3, 5, 6, 8, 9, 11 }) Add(n, 4);
            Add(4, 5); Add(10, 5);
            expected.Sort();
            Assert.AreEqual(38, expected.Count);
            foreach (var seed in Seeds)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                var got = new List<int>();
                for (int h = 0; h < b.HexTerrain.Length; h++)
                {
                    if (b.HexTerrain[h] == Terrain.Meadow) Assert.AreEqual(0, b.HexNumber[h], "Meadow has no number");
                    else got.Add(b.HexNumber[h]);
                }
                got.Sort();
                CollectionAssert.AreEqual(expected, got, "seed " + seed);
            }
        }

        [Test]
        public void NoSixOrEightNextToAnother()
        {
            for (ulong seed = 0; seed < 200; seed++)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                var t = b.Topology;
                for (int h = 0; h < t.HexCount; h++)
                {
                    if (b.HexNumber[h] != 6 && b.HexNumber[h] != 8) continue;
                    for (int d = 0; d < 6; d++)
                    {
                        int n = t.HexIndex(t.Hex(h).Neighbour(d));
                        if (n < 0) continue;
                        Assert.IsFalse(b.HexNumber[n] == 6 || b.HexNumber[n] == 8, "seed " + seed + ": red " + t.Hex(h) + " next to red " + t.Hex(n));
                    }
                }
            }
        }

        [Test]
        public void FourFixedStartCorners()
        {
            foreach (var seed in Seeds)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                CollectionAssert.AreEqual(new[] { 70, 97, 57, 82 }, b.StartCorners);
                foreach (int c in b.StartCorners)
                {
                    Assert.IsTrue(PlotLayout.IsSettlementCorner(b.Topology, c), "start " + c + " is a settlement corner");
                    Assert.IsTrue(b.Topology.VertexHexes(c).All(h => b.HexTerrain[h] != Terrain.Meadow), "start " + c + " touches no Meadow");
                }
            }
            // The home start is the corner of the three pinned home hexes.
            var hb = BoardGenerator.CreateDealtBoard(0);
            var homeHexes = hb.Topology.VertexHexes(70).Select(h => hb.Topology.Hex(h)).ToList();
            CollectionAssert.AreEquivalent(BoardGenerator.HomeHexes, homeHexes);
        }

        [Test]
        public void FairStarts_HoldForAtLeast99PercentOfSeeds()
        {
            int fair = 0, maxTries = 0;
            for (ulong seed = 0; seed < ManySeeds; seed++)
            {
                var b = BoardGenerator.CreateDealtBoard(seed * 7919 + 3);
                bool f = Fair(b);
                Assert.AreEqual(f, b.DealFair, "DealFair matches the rule for seed " + seed);
                Assert.That(b.DealTriesUsed, Is.InRange(1, BoardGenerator.DealTries));
                if (f) fair++;
                maxTries = Math.Max(maxTries, b.DealTriesUsed);
            }
            TestContext.Progress.WriteLine("BD-4: " + fair + "/" + ManySeeds + " fair, most tries " + maxTries);
            Assert.GreaterOrEqual(fair, ManySeeds * 99 / 100, "at least 99% of seeds meet BD-4");
        }

        [Test]
        public void UnfairFallback_KeepsTheFairestTry()
        {
            // StartFairness is the measure the deal minimises; on a fair board both parts are zero/at most one.
            var b = BoardGenerator.CreateDealtBoard(5);
            BoardGenerator.StartFairness(b, b.Topology, b.StartCorners, out int bad, out int spread);
            Assert.AreEqual(Fair(b), bad == 0 && spread <= 1);
            Assert.LessOrEqual(BoardGenerator.DealTries, 500);
            Assert.AreEqual(500, BoardGenerator.DealTries);
        }

        [Test]
        public void Ports_NineFixedSpots_KindsShuffled()
        {
            var legacy = BoardGenerator.CreateMvpBoard(0);
            var t = legacy.Topology;
            var campaign = new HashSet<HexCoord>
            {
                new HexCoord(0, -1), new HexCoord(-2, -2), new HexCoord(4, -1), new HexCoord(4, -3), new HexCoord(1, -4),
                new HexCoord(2, -3), new HexCoord(2, 2), new HexCoord(3, 1), new HexCoord(1, 3), new HexCoord(0, -4),
            };
            var towers = new[] { 17, 74, 2, 147 };
            var layouts = new HashSet<string>();
            int[] spotHexes = null;
            for (ulong seed = 0; seed < 200; seed++)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                Assert.AreEqual(9, b.Ports.Count);
                var hexes = b.Ports.Select(p => p.Hex).ToArray();
                if (spotHexes == null) spotHexes = hexes;
                CollectionAssert.AreEqual(spotHexes, hexes, "the spots are fixed");
                Assert.AreEqual(4, b.Ports.Count(p => p.Good == null && p.Ratio == 3), "four 3:1");
                var goods = b.Ports.Where(p => p.Good != null).Select(p => p.Good.Value).OrderBy(g => g).ToArray();
                CollectionAssert.AreEqual(new[] { Ware.Wood, Ware.Clay, Ware.Grain, Ware.Sheep, Ware.Stone }, goods, "one 2:1 per raw good");
                Assert.IsTrue(b.Ports.Where(p => p.Good != null).All(p => p.Ratio == 2));
                layouts.Add(string.Join(",", b.Ports.Select(p => p.Good?.ToString() ?? "any")));
            }
            Assert.Greater(layouts.Count, 50, "kinds are shuffled per game");

            var angles = new List<double>();
            foreach (int h in spotHexes)
            {
                var c = t.Hex(h);
                Assert.AreEqual(Terrain.Meadow, legacy.HexTerrain[h], c + " is Meadow");
                Assert.AreEqual(BoardGenerator.MvpRadius, c.Ring, c + " is on the rim");
                Assert.IsFalse(campaign.Contains(c), c + " is not a campaign place");
                foreach (int v in t.HexCorners(h)) CollectionAssert.DoesNotContain(towers, v, c + " has no tower corner");
                var corners = BoardGenerator.PortCorners(t, h);
                Assert.GreaterOrEqual(corners.Length, 2, c + " has settlement corners");
                foreach (int v in corners) Assert.IsTrue(PlotLayout.IsSettlementCorner(t, v));
                angles.Add((Math.Atan2(t.HexZ(h), t.HexX(h)) * 180 / Math.PI + 360) % 360);
            }
            Assert.AreEqual(9, spotHexes.Distinct().Count());
            angles.Sort();
            double maxGap = 360 - angles[angles.Count - 1] + angles[0];
            for (int i = 1; i < angles.Count; i++) maxGap = Math.Max(maxGap, angles[i] - angles[i - 1]);
            Assert.Less(maxGap, 120, "ports spread round the shore");
        }

        [Test]
        public void Deterministic_FromTheSeed()
        {
            foreach (var seed in Seeds)
            {
                string a = Newtonsoft.Json.JsonConvert.SerializeObject(BoardGenerator.CreateDealtBoard(seed));
                string b = Newtonsoft.Json.JsonConvert.SerializeObject(BoardGenerator.CreateDealtBoard(seed));
                Assert.AreEqual(a, b, "seed " + seed);
            }
            Assert.AreNotEqual(
                Newtonsoft.Json.JsonConvert.SerializeObject(BoardGenerator.CreateDealtBoard(1)),
                Newtonsoft.Json.JsonConvert.SerializeObject(BoardGenerator.CreateDealtBoard(2)));
        }

        [Test]
        public void SuggestedSites_AvoidEveryStart_AndDanceCircleReserved()
        {
            foreach (var seed in Seeds)
            {
                var b = BoardGenerator.CreateDealtBoard(seed);
                var t = b.Topology;
                foreach (int v in b.SuggestedSettlementCorners)
                    foreach (int st in b.StartCorners) Assert.GreaterOrEqual(t.CornerDistance(v, st), 2, "seed " + seed + " site " + v + " start " + st);
                Assert.AreEqual(1, b.ReservedPlots.Length, "the dance circle");
            }
        }

        [Test]
        public void LegacyBoard_Unchanged_HasTwoStartsAndNoPorts()
        {
            var b = BoardGenerator.CreateMvpBoard(42);
            CollectionAssert.AreEqual(new[] { 70, 97 }, b.StartCorners);
            Assert.AreEqual(0, b.Ports.Count);
            Assert.AreEqual(0, b.DealTriesUsed);
            Assert.AreEqual(BoardGenerator.StartForestNumber, b.HexNumber[b.Topology.HexIndex(new HexCoord(1, -1))]);
        }

        [Test]
        public void DealtBoard_SurvivesSaveRoundTrip()
        {
            var sim = WorldSimFactory.CreateNew(77, GameSetup.GodBoard);
            string json = SaveCodec.ToJson(sim.State);
            var loaded = SaveCodec.FromJson(json).State;
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(sim.State.Board), Newtonsoft.Json.JsonConvert.SerializeObject(loaded.Board));
            Assert.AreEqual(9, loaded.Board.Ports.Count);
        }
    }
}
