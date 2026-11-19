using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// The god board deal (PRD_GODBOARD 4.0, BD-1 to BD-5). The island outline and its 23 Meadow hexes are those of the
    /// legacy table; seven hexes are pinned; the other 31 resource hexes get a seeded terrain deal, all 38 a seeded number
    /// deal with no 6 or 8 next to another 6 or 8. The deal is repeated with the next sub-seed until the four fixed start
    /// corners are fair (BD-4), at most <see cref="DealTries"/> times, else the fairest deal tried is kept. Nine fixed
    /// port spots on the Meadow rim get a seeded shuffle of four 3:1 and five 2:1 ports (BD-5). Pure and deterministic
    /// from the seed. The legacy board (<see cref="CreateMvpBoard"/>) is unchanged (BD-6).
    /// </summary>
    public static partial class BoardGenerator
    {
        /// <summary>Most deals tried for fair starts (BD-4).</summary>
        public const int DealTries = 500;

        /// <summary>Most number reshuffles per deal before giving up on the 6/8 rule (never reached in practice).</summary>
        public const int NumberShuffleLimit = 100000;

        /// <summary>The four fixed start corners (PE-2), index = people: home, Ash Clan, Elves, Orcs.</summary>
        public static readonly int[] DealtStartCorners = { 70, 97, 57, 82 };

        /// <summary>The seven pinned hexes (BD-2, owner Q-1) and their terrain.</summary>
        public static readonly HexCoord[] PinnedHexes =
        {
            new HexCoord(0, 0), new HexCoord(1, -1), new HexCoord(1, 0),   // the home camp: Fields, Forest, Hills
            new HexCoord(-1, -1),                                          // the glade's other wood
            new HexCoord(3, -1),                                           // the Cold Kiln's clay hills
            new HexCoord(2, 1), new HexCoord(1, 2),                        // the Air route's mountains
        };

        public static readonly Terrain[] PinnedTerrains =
        {
            Terrain.Fields, Terrain.Forest, Terrain.Hills,
            Terrain.Forest,
            Terrain.Hills,
            Terrain.Mountain, Terrain.Mountain,
        };

        /// <summary>Terrain dealt over the 31 unpinned resource hexes (BD-2): 6 Forest, 8 Pasture, 7 Fields, 5 Hills, 5 Mountain.</summary>
        public static readonly Terrain[] DealtTerrainPool = MakeTerrainPool();

        /// <summary>Numbers dealt over all 38 resource hexes (BD-3): two 2s and 12s, four each of 3, 5, 6, 8, 9, 11, five each of 4 and 10.</summary>
        public static readonly int[] DealtNumberPool =
        {
            2, 2, 12, 12,
            3, 3, 3, 3, 5, 5, 5, 5, 6, 6, 6, 6, 8, 8, 8, 8, 9, 9, 9, 9, 11, 11, 11, 11,
            4, 4, 4, 4, 4, 10, 10, 10, 10, 10,
        };

        /// <summary>
        /// The nine port spots (BD-5): Meadow hexes on the rim (ring 4), spread round the shore, none a campaign place
        /// (<see cref="CampaignMeadowHexes"/>) and none with a tower corner (<see cref="TowerCorners"/>) among its corners.
        /// </summary>
        public static readonly HexCoord[] PortHexes =
        {
            // Anticlockwise from the north shore. The east and south-west shores hold tower hexes ((4, 0), (-1, -3)) and
            // campaign places, so these nine are the most even spread of the eleven allowed rim hexes.
            new HexCoord(0, 4), new HexCoord(-1, 4), new HexCoord(-3, 4), new HexCoord(-4, 4), new HexCoord(-4, 2),
            new HexCoord(-4, 0), new HexCoord(3, -4), new HexCoord(4, -4), new HexCoord(4, -2),
        };

        /// <summary>Corners the crystal campaign's towers stand on (no port there).</summary>
        public static readonly int[] TowerCorners = { 17, 74, 2, 147 };

        /// <summary>Meadow hexes the campaign uses for its places (no port there).</summary>
        public static readonly HexCoord[] CampaignMeadowHexes =
        {
            new HexCoord(0, -1), new HexCoord(-2, -2), new HexCoord(4, -1), new HexCoord(4, -3), new HexCoord(1, -4),
            new HexCoord(2, -3), new HexCoord(2, 2), new HexCoord(3, 1), new HexCoord(1, 3), new HexCoord(0, -4),
        };

        /// <summary>Port kinds shuffled over <see cref="PortHexes"/>: four 3:1 (null good) and one 2:1 per raw good.</summary>
        public static readonly Ware?[] PortGoods = { null, null, null, null, Ware.Wood, Ware.Clay, Ware.Grain, Ware.Sheep, Ware.Stone };

        private static Terrain[] MakeTerrainPool()
        {
            var list = new List<Terrain>();
            void Add(Terrain t, int n) { for (int i = 0; i < n; i++) list.Add(t); }
            Add(Terrain.Forest, 6);
            Add(Terrain.Pasture, 8);
            Add(Terrain.Fields, 7);
            Add(Terrain.Hills, 5);
            Add(Terrain.Mountain, 5);
            return list.ToArray();
        }

        /// <summary>Pips of a number (2/12 = 1 ... 6/8 = 5; 0 for anything else).</summary>
        public static int Pips(int number) => number < 2 || number > 12 || number == 7 ? 0 : 6 - Math.Abs(7 - number);

        /// <summary>True for a 6 or an 8.</summary>
        public static bool IsRed(int number) => number == 6 || number == 8;

        /// <summary>
        /// The god board (BD-1 to BD-5) for a seed. Each try t uses the sub-seed stream "deal.t". A try deals the terrain
        /// of the 31 unpinned hexes unless an earlier try already found a terrain that gives every start three different
        /// raw terrains (that terrain is kept), then deals the numbers on all 38 (reshuffled until the 6/8 rule holds).
        /// The first try that meets BD-4 wins; after <see cref="DealTries"/> the fairest try is kept (fewest starts without
        /// three terrains, then smallest pip spread, then earliest).
        /// </summary>
        public static Board CreateDealtBoard(ulong seed)
        {
            var topo = BoardTopology.Get(MvpRadius);
            var root = new Rng(seed).Fork("board.deal");
            Board best = null, goodTerrain = null;
            int bestBad = int.MaxValue, bestSpread = int.MaxValue, tries = 0;
            for (int t = 0; t < DealTries; t++)
            {
                tries = t + 1;
                var rng = root.Fork("deal." + t);
                var b = goodTerrain != null ? CopyTerrain(goodTerrain) : DealTerrain(topo, rng);
                DealNumbers(b, topo, rng);
                StartFairness(b, topo, DealtStartCorners, out int bad, out int spread);
                if (bad == 0 && goodTerrain == null) goodTerrain = b;
                if (bad < bestBad || (bad == bestBad && spread < bestSpread))
                {
                    best = b;
                    bestBad = bad;
                    bestSpread = spread;
                }
                if (bad == 0 && spread <= 1) break;
            }

            best.StartCorners = (int[])DealtStartCorners.Clone();
            best.DealTriesUsed = tries;
            best.DealFair = bestBad == 0 && bestSpread <= 1;
            best.SuggestedSettlementCorners = SuggestSites(best, topo, best.StartCorners);
            best.ReservedPlots = DanceCirclePlots(best, topo, best.StartCorners[0]);
            best.Ports = DealPorts(topo, new Rng(seed).Fork("board.ports"));
            return best;
        }

        private static Board CopyTerrain(Board src) => new Board
        {
            Radius = src.Radius,
            HexTerrain = (Terrain[])src.HexTerrain.Clone(),
            HexNumber = new int[src.HexNumber.Length],
        };

        /// <summary>The Meadow layout, the seven pins and a shuffle of <see cref="DealtTerrainPool"/> over the other hexes.</summary>
        private static Board DealTerrain(BoardTopology topo, Rng rng)
        {
            var b = new Board { Radius = MvpRadius, HexTerrain = new Terrain[topo.HexCount], HexNumber = new int[topo.HexCount] };
            WriteLegacyTerrain(b, topo);

            var pinned = new bool[topo.HexCount];
            for (int i = 0; i < PinnedHexes.Length; i++)
            {
                int h = topo.HexIndex(PinnedHexes[i]);
                b.HexTerrain[h] = PinnedTerrains[i];
                pinned[h] = true;
            }

            var open = new List<int>();
            for (int h = 0; h < topo.HexCount; h++)
                if (b.HexTerrain[h] != Terrain.Meadow && !pinned[h]) open.Add(h);
            if (open.Count != DealtTerrainPool.Length)
                throw new InvalidOperationException("The Meadow layout does not leave " + DealtTerrainPool.Length + " unpinned resource hexes.");

            var terrains = new List<Terrain>(DealtTerrainPool);
            rng.Shuffle(terrains);
            for (int i = 0; i < open.Count; i++) b.HexTerrain[open[i]] = terrains[i];
            return b;
        }

        /// <summary>A shuffle of <see cref="DealtNumberPool"/> over all resource hexes, reshuffled until no 6/8 touches another.</summary>
        private static void DealNumbers(Board b, BoardTopology topo, Rng rng)
        {
            var resource = new List<int>();
            for (int h = 0; h < topo.HexCount; h++) if (b.HexTerrain[h] != Terrain.Meadow) resource.Add(h);
            if (resource.Count != DealtNumberPool.Length)
                throw new InvalidOperationException("The Meadow layout does not leave " + DealtNumberPool.Length + " resource hexes.");
            var numbers = new List<int>(DealtNumberPool);
            for (int n = 0; n < NumberShuffleLimit; n++)
            {
                rng.Shuffle(numbers);
                for (int i = 0; i < resource.Count; i++) b.HexNumber[resource[i]] = numbers[i];
                if (RedsApart(b, topo)) return;
            }
            throw new InvalidOperationException("No number deal keeps the 6s and 8s apart.");
        }

        /// <summary>BD-3: no 6 or 8 next to another 6 or 8.</summary>
        public static bool RedsApart(Board b, BoardTopology topo)
        {
            for (int h = 0; h < topo.HexCount; h++)
            {
                if (!IsRed(b.HexNumber[h])) continue;
                var c = topo.Hex(h);
                for (int d = 0; d < 6; d++)
                {
                    int n = topo.HexIndex(c.Neighbour(d));
                    if (n >= 0 && IsRed(b.HexNumber[n])) return false;
                }
            }
            return true;
        }

        /// <summary>Summed pips of the hexes around a corner.</summary>
        public static int CornerPips(Board b, BoardTopology topo, int corner)
        {
            int sum = 0;
            foreach (int h in topo.VertexHexes(corner)) sum += Pips(b.HexNumber[h]);
            return sum;
        }

        /// <summary>True when a corner touches three different raw terrains (BD-4).</summary>
        public static bool ThreeRawTerrains(Board b, BoardTopology topo, int corner)
        {
            var hs = topo.VertexHexes(corner);
            if (hs.Count != 3) return false;
            var seen = new List<Terrain>(3);
            foreach (int h in hs)
            {
                var t = b.HexTerrain[h];
                if (!TerrainInfo.IsResource(t) || seen.Contains(t)) return false;
                seen.Add(t);
            }
            return true;
        }

        /// <summary>BD-4 measure: starts without three different raw terrains, and max minus min of the starts' summed pips.</summary>
        public static void StartFairness(Board b, BoardTopology topo, int[] starts, out int badStarts, out int pipSpread)
        {
            badStarts = 0;
            int lo = int.MaxValue, hi = int.MinValue;
            foreach (int c in starts)
            {
                if (!ThreeRawTerrains(b, topo, c)) badStarts++;
                int p = CornerPips(b, topo, c);
                lo = Math.Min(lo, p);
                hi = Math.Max(hi, p);
            }
            pipSpread = starts.Length == 0 ? 0 : hi - lo;
        }

        /// <summary>True when a board meets BD-4 on its start corners.</summary>
        public static bool StartsFair(Board b)
        {
            StartFairness(b, b.Topology, b.StartCorners, out int bad, out int spread);
            return bad == 0 && spread <= 1;
        }

        /// <summary>The nine ports on <see cref="PortHexes"/>, kinds shuffled (BD-5).</summary>
        private static List<Port> DealPorts(BoardTopology topo, Rng rng)
        {
            var goods = new List<Ware?>(PortGoods);
            rng.Shuffle(goods);
            var ports = new List<Port>(PortHexes.Length);
            for (int i = 0; i < PortHexes.Length; i++)
            {
                int h = topo.HexIndex(PortHexes[i]);
                ports.Add(new Port { Hex = h, Corners = PortCorners(topo, h), Ratio = goods[i] == null ? 3 : 2, Good = goods[i] });
            }
            return ports;
        }

        /// <summary>A hex's settlement corners (where three hexes meet), ascending.</summary>
        public static int[] PortCorners(BoardTopology topo, int hex)
        {
            var list = new List<int>();
            foreach (int v in topo.HexCorners(hex)) if (topo.VertexHexes(v).Count == 3) list.Add(v);
            list.Sort();
            return list.ToArray();
        }
    }
}
