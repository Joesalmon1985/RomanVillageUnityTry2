using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// The fixed MVP map (radius 4, 61 hexes, 60 m hex edges). Terrain layout is fixed; production numbers
    /// are dealt with the seed, evenly per terrain (within one terrain each number 1..4 appears equally
    /// often, +-1), and the three home hexes always get three different numbers.
    ///
    /// Layout (q,r), +Z north; rows listed north to south, q ascending (M Meadow, F Forest, H Hills,
    /// Fi Fields, P Pasture, Mt Mountain; * = home hex):
    ///   r= 4  q-4..0 : M  M  F  M  M
    ///   r= 3  q-4..1 : M  Fi P  H  F  M
    ///   r= 2  q-4..2 : M  F  H  Fi Mt Mt M
    ///   r= 1  q-4..3 : M  H  Fi F  P  Mt Mt M
    ///   r= 0  q-4..4 : M  F  Fi H  Fi* H* Fi F  M
    ///   r=-1  q-3..4 : P  Fi F  M  F* P  H  M
    ///   r=-2  q-2..4 : M  H  P  Fi F  Fi M
    ///   r=-3  q-1..4 : M  F  H  M  P  M
    ///   r=-4  q 0..4 : M  M  P  M  M
    /// Home city: the corner shared by (0,0) Fields, (1,-1) Forest and (1,0) Hills (just south-east of the
    /// centre). The Mountain range (0,2) (1,2) (1,1) (2,1) lies north-east, out of the home territory but
    /// touching city corners 2-3 hex edges from home. The rival outpost is the Forest/Hills/Fields corner
    /// 4-5 hex edges from home (north-west). The outer ring is mostly Meadow.
    /// </summary>
    public static class BoardGenerator
    {
        public const int MvpRadius = 4;

        public static readonly HexCoord[] HomeHexes = { new HexCoord(0, 0), new HexCoord(1, -1), new HexCoord(1, 0) };

        private const Terrain M = Terrain.Meadow, F = Terrain.Forest, H = Terrain.Hills, Fi = Terrain.Fields,
            P = Terrain.Pasture, Mt = Terrain.Mountain;

        /// <summary>Rows from r = -4 (south) to r = 4 (north), q ascending.</summary>
        private static readonly Terrain[][] Rows =
        {
            new[] { M, M, P, M, M },
            new[] { M, F, H, M, P, M },
            new[] { M, H, P, Fi, F, Fi, M },
            new[] { P, Fi, F, M, F, P, H, M },
            new[] { M, F, Fi, H, Fi, H, Fi, F, M },
            new[] { M, H, Fi, F, P, Mt, Mt, M },
            new[] { M, F, H, Fi, Mt, Mt, M },
            new[] { M, Fi, P, H, F, M },
            new[] { M, M, F, M, M },
        };

        /// <summary>Terrains that get production numbers, in dealing order.</summary>
        private static readonly Terrain[] NumberedTerrains = { F, H, Fi, Mt, P };

        /// <summary>Minimum distance in hex edges between two suggested city sites.</summary>
        public const int SuggestedSiteSpacing = 3;

        public static Board CreateMvpBoard(ulong seed)
        {
            var topo = BoardTopology.Get(MvpRadius);
            var b = new Board
            {
                Radius = MvpRadius,
                HexTerrain = new Terrain[topo.HexCount],
                HexNumber = new int[topo.HexCount],
            };
            for (int i = 0; i < Rows.Length; i++)
            {
                int r = i - MvpRadius;
                int q0 = Math.Max(-MvpRadius, -MvpRadius - r);
                for (int j = 0; j < Rows[i].Length; j++) b.HexTerrain[topo.HexIndex(new HexCoord(q0 + j, r))] = Rows[i][j];
            }

            var homeIds = new int[HomeHexes.Length];
            for (int i = 0; i < HomeHexes.Length; i++) homeIds[i] = topo.HexIndex(HomeHexes[i]);
            DealNumbers(b, topo, homeIds, new Rng(seed).Fork("board.numbers"));

            // Home city corner: the one shared by all three home hexes.
            int home = SharedCorner(topo, homeIds);
            b.HomeCityVertex = home;
            b.StorehouseVertex = home;
            b.MarketVertex = home;
            b.WarehouseVertex = WarehouseFlag(b, topo, home);
            b.RivalOutpostVertex = RivalCorner(b, topo, home);
            b.SuggestedCitySites = SuggestSites(b, topo, home, b.RivalOutpostVertex);
            return b;
        }

        private static void DealNumbers(Board b, BoardTopology topo, int[] homeIds, Rng rng)
        {
            var distinct = new List<int> { 1, 2, 3, 4 };
            rng.Shuffle(distinct);
            for (int i = 0; i < homeIds.Length; i++) b.HexNumber[homeIds[i]] = distinct[i];

            foreach (var t in NumberedTerrains)
            {
                var hexes = new List<int>();
                int homeNumber = 0;
                for (int h = 0; h < topo.HexCount; h++)
                {
                    if (b.HexTerrain[h] != t) continue;
                    if (Array.IndexOf(homeIds, h) >= 0) homeNumber = b.HexNumber[h];
                    else hexes.Add(h);
                }
                int n = hexes.Count + (homeNumber > 0 ? 1 : 0);
                if (n == 0) continue;
                var counts = new int[5];
                for (int k = 1; k <= 4; k++) counts[k] = n / 4;
                var extras = new List<int> { 1, 2, 3, 4 };
                rng.Shuffle(extras);
                if (homeNumber > 0 && n / 4 == 0)
                {
                    extras.Remove(homeNumber);
                    extras.Insert(0, homeNumber);
                }
                for (int k = 0; k < n % 4; k++) counts[extras[k]]++;
                var pool = new List<int>();
                for (int k = 1; k <= 4; k++)
                    for (int c = 0; c < counts[k]; c++) pool.Add(k);
                if (homeNumber > 0) pool.Remove(homeNumber);
                rng.Shuffle(pool);
                for (int i = 0; i < hexes.Count; i++) b.HexNumber[hexes[i]] = pool[i];
            }
        }

        private static int SharedCorner(BoardTopology topo, int[] hexes)
        {
            foreach (int v in topo.HexCorners(hexes[0]))
            {
                bool all = true;
                for (int i = 1; i < hexes.Length; i++)
                {
                    bool found = false;
                    foreach (int w in topo.HexCorners(hexes[i])) if (w == v) found = true;
                    if (!found) all = false;
                }
                if (all) return v;
            }
            throw new InvalidOperationException("Home hexes do not share a corner");
        }

        private static bool Touches(Board b, BoardTopology topo, int v, Terrain t)
        {
            foreach (int h in topo.VertexHexes(v)) if (b.HexTerrain[h] == t) return true;
            return false;
        }

        /// <summary>
        /// The intermediate flag two segments from home along the home hex edge that does not touch the
        /// home Forest (the Fields/Hills edge), so the demonstration road along the forest stays clear.
        /// </summary>
        private static int WarehouseFlag(Board b, BoardTopology topo, int home)
        {
            foreach (int e in topo.CornerHexEdges(home))
            {
                var flags = topo.HexEdgeFlags(e);
                if (Touches(b, topo, flags[1], Terrain.Forest)) continue;
                return flags[0] == home ? flags[2] : flags[1];
            }
            var first = topo.HexEdgeFlags(topo.CornerHexEdges(home)[0]);
            return first[0] == home ? first[2] : first[1];
        }

        /// <summary>
        /// Rival outpost: a corner 4-5 hex edges from home whose three hexes are exactly Forest, Hills and
        /// Fields (farthest first, then lowest id). Fallback: the farthest corner not touching a Mountain.
        /// </summary>
        private static int RivalCorner(Board b, BoardTopology topo, int home)
        {
            int best = -1, bestD = -1;
            foreach (int v in topo.Corners)
            {
                var hs = topo.VertexHexes(v);
                if (hs.Count != 3) continue;
                int d = topo.CornerDistance(home, v);
                if (d < 4 || d > 5) continue;
                if (!Touches(b, topo, v, Terrain.Forest) || !Touches(b, topo, v, Terrain.Hills) || !Touches(b, topo, v, Terrain.Fields)) continue;
                if (d > bestD) { best = v; bestD = d; }
            }
            if (best >= 0) return best;
            foreach (int v in topo.Corners)
            {
                if (Touches(b, topo, v, Terrain.Mountain)) continue;
                int d = topo.CornerDistance(home, v);
                if (d > bestD) { best = v; bestD = d; }
            }
            return best;
        }

        /// <summary>City value of a corner: 10 per distinct non-Meadow terrain, +1 per non-Meadow hex, +5 with a Mountain.</summary>
        public static int SiteScore(Board b, BoardTopology topo, int corner)
        {
            var kinds = new List<Terrain>();
            int count = 0;
            bool mountain = false;
            foreach (int h in topo.VertexHexes(corner))
            {
                var t = b.HexTerrain[h];
                if (t == Terrain.Meadow) continue;
                count++;
                if (t == Terrain.Mountain) mountain = true;
                if (!kinds.Contains(t)) kinds.Add(t);
            }
            return kinds.Count * 10 + count + (mountain ? 5 : 0);
        }

        private static int[] SuggestSites(Board b, BoardTopology topo, int home, int rival)
        {
            var cands = new List<int>();
            foreach (int v in topo.Corners)
            {
                if (topo.VertexHexes(v).Count != 3 || v == home || v == rival) continue;
                if (topo.CornerDistance(v, home) < 2 || (rival >= 0 && topo.CornerDistance(v, rival) < 2)) continue;
                if (SiteScore(b, topo, v) < 22) continue; // at least two different useful terrains
                cands.Add(v);
            }
            // Second-city band (2-3 hex edges from home) first, then by value, distance and id.
            cands.Sort((x, y) =>
            {
                int dx = topo.CornerDistance(home, x), dy = topo.CornerDistance(home, y);
                int bx = dx >= 2 && dx <= 3 ? 0 : 1, by = dy >= 2 && dy <= 3 ? 0 : 1;
                if (bx != by) return bx.CompareTo(by);
                int sx = SiteScore(b, topo, x), sy = SiteScore(b, topo, y);
                if (sx != sy) return sy.CompareTo(sx);
                if (dx != dy) return dx.CompareTo(dy);
                return x.CompareTo(y);
            });
            var sel = new List<int>();
            foreach (int v in cands)
            {
                bool ok = rival < 0 || topo.CornerDistance(v, rival) >= SuggestedSiteSpacing;
                foreach (int s in sel) if (topo.CornerDistance(v, s) < SuggestedSiteSpacing) ok = false;
                if (ok) sel.Add(v);
            }
            sel.Sort((x, y) =>
            {
                int dx = topo.CornerDistance(home, x), dy = topo.CornerDistance(home, y);
                return dx != dy ? dx.CompareTo(dy) : x.CompareTo(y);
            });
            return sel.ToArray();
        }
    }
}
