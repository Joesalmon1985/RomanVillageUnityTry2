using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// The fixed MVP map (radius 2). Terrain layout is fixed; production numbers are dealt from a
    /// fixed multiset with the seed (the three home hexes always get three different numbers).
    /// Layout (q,r), +Z north:
    ///   r=-2: (0,-2) Forest  (1,-2) Meadow  (2,-2) Forest
    ///   r=-1: (-1,-1) Hills  (0,-1) Meadow  (1,-1) Forest  (2,-1) Fields
    ///   r= 0: (-2,0) Mountain (-1,0) Meadow (0,0) Fields   (1,0) Hills   (2,0) Meadow
    ///   r= 1: (-2,1) Mountain (-1,1) Forest (0,1) Meadow   (1,1) Hills
    ///   r= 2: (-2,2) Fields  (-1,2) Mountain (0,2) Meadow
    /// Home city: the vertex shared by (0,0) Fields, (1,-1) Forest and (1,0) Hills.
    /// </summary>
    public static class BoardGenerator
    {
        public const int MvpRadius = 2;

        public static readonly HexCoord[] HomeHexes = { new HexCoord(0, 0), new HexCoord(1, -1), new HexCoord(1, 0) };

        private static readonly (int q, int r, Terrain t)[] Layout =
        {
            (0, -2, Terrain.Forest), (1, -2, Terrain.Meadow), (2, -2, Terrain.Forest),
            (-1, -1, Terrain.Hills), (0, -1, Terrain.Meadow), (1, -1, Terrain.Forest), (2, -1, Terrain.Fields),
            (-2, 0, Terrain.Mountain), (-1, 0, Terrain.Meadow), (0, 0, Terrain.Fields), (1, 0, Terrain.Hills), (2, 0, Terrain.Meadow),
            (-2, 1, Terrain.Mountain), (-1, 1, Terrain.Forest), (0, 1, Terrain.Meadow), (1, 1, Terrain.Hills),
            (-2, 2, Terrain.Fields), (-1, 2, Terrain.Mountain), (0, 2, Terrain.Meadow),
        };

        /// <summary>Production numbers dealt over the 13 resource hexes.</summary>
        private static readonly int[] NumberPool = { 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 4, 4, 4 };

        public static Board CreateMvpBoard(ulong seed)
        {
            var topo = BoardTopology.Get(MvpRadius);
            var b = new Board
            {
                Radius = MvpRadius,
                HexTerrain = new Terrain[topo.HexCount],
                HexNumber = new int[topo.HexCount],
            };
            foreach (var (q, r, t) in Layout) b.HexTerrain[topo.HexIndex(new HexCoord(q, r))] = t;

            // Production numbers.
            var rng = new Rng(seed).Fork("board.numbers");
            var pool = new List<int>(NumberPool);
            var homeIds = new int[HomeHexes.Length];
            for (int i = 0; i < HomeHexes.Length; i++) homeIds[i] = topo.HexIndex(HomeHexes[i]);
            var distinct = new List<int> { 1, 2, 3, 4 };
            rng.Shuffle(distinct);
            for (int i = 0; i < homeIds.Length; i++)
            {
                b.HexNumber[homeIds[i]] = distinct[i];
                pool.Remove(distinct[i]);
            }
            rng.Shuffle(pool);
            int p = 0;
            for (int h = 0; h < topo.HexCount; h++)
            {
                if (!TerrainInfo.IsResource(b.HexTerrain[h]) || Array.IndexOf(homeIds, h) >= 0) continue;
                b.HexNumber[h] = pool[p++];
            }
            if (p != pool.Count) throw new InvalidOperationException("Number pool does not match resource hexes");

            // Home city vertex: the one shared by all three home hexes.
            b.HomeCityVertex = SharedVertex(topo, homeIds);
            b.StorehouseVertex = b.HomeCityVertex;
            b.MarketVertex = b.HomeCityVertex;
            b.WarehouseVertex = topo.VertexNeighbours(b.HomeCityVertex)[0];

            // Rival outpost: farthest (by edges) from home, not touching a Mountain hex; ties lowest id.
            int best = -1, bestD = -1;
            for (int v = 0; v < topo.VertexCount; v++)
            {
                bool mountain = false;
                foreach (int h in topo.VertexHexes(v)) if (b.HexTerrain[h] == Terrain.Mountain) mountain = true;
                if (mountain) continue;
                int d = topo.Distance(b.HomeCityVertex, v);
                if (d > bestD) { bestD = d; best = v; }
            }
            b.RivalOutpostVertex = best;
            return b;
        }

        private static int SharedVertex(BoardTopology topo, int[] hexes)
        {
            foreach (int v in topo.HexVertices(hexes[0]))
            {
                bool all = true;
                for (int i = 1; i < hexes.Length; i++)
                {
                    bool found = false;
                    foreach (int w in topo.HexVertices(hexes[i])) if (w == v) found = true;
                    if (!found) all = false;
                }
                if (all) return v;
            }
            throw new InvalidOperationException("Home hexes do not share a vertex");
        }
    }
}
