using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// The fixed island (radius 4, 61 hexes, 60 m hex edges). Terrain layout and both start corners are fixed;
    /// production numbers 2..12 are dealt with the seed (REFACTOR_SPEC 2.A.1).
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
    /// Player start (corner 70): the corner shared by (0,0) Fields, (1,-1) Forest and (1,0) Hills. The Mountain
    /// range lies 2-3 hex edges away. The rival start (corner 97) is the Forest/Hills/Fields corner 4-5 hex
    /// edges from home. The outer ring is mostly Meadow.
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

        /// <summary>Minimum distance in hex edges between two suggested settlement corners.</summary>
        public const int SuggestedSiteSpacing = 3;

        /// <summary>Fixed numbers of each starting camp's hexes by terrain (2.A.1 step 1).</summary>
        public const int StartForestNumber = 6, StartHillsNumber = 8, StartFieldsNumber = 5;

        /// <summary>Numbers of the four Mountain hexes, in a seeded order (2.A.1 step 2).</summary>
        public static readonly int[] MountainNumbers = { 5, 6, 9, 10 };

        /// <summary>Pool shuffled over the other 28 resource hexes (2.A.1 step 3).</summary>
        public static readonly int[] NumberPool =
        {
            2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 8, 8, 8, 9, 9, 9, 10, 10, 10, 10, 11, 11, 12,
        };

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

            // Start corners: the player's is shared by the three home hexes; the rival's is the Forest/Hills/Fields
            // corner 4-5 hex edges away. Both depend on the fixed terrain only.
            int home = SharedCorner(topo, homeIds);
            int rival = RivalCorner(b, topo, home);
            b.StartCorners = new[] { home, rival };
            DealNumbers(b, topo, b.StartCorners, new Rng(seed).Fork("board.numbers"));
            b.SuggestedSettlementCorners = SuggestSites(b, topo, home, rival);

            // The dance circle: slot 2 of the home camp's Fields sector.
            int fieldsSector = -1;
            for (int sec = 0; sec < 3; sec++)
                if (PlotLayout.SectorHex(topo, home, sec) >= 0 && b.HexTerrain[PlotLayout.SectorHex(topo, home, sec)] == Terrain.Fields) fieldsSector = sec;
            b.ReservedPlots = fieldsSector >= 0 ? new[] { PlotLayout.PlotId(home, fieldsSector, PlotLayout.CitySlot) } : new int[0];
            return b;
        }

        /// <summary>
        /// 2.A.1: starting camps' hexes get Forest 6, Hills 8, Fields 5; Mountains a seeded order of {5, 6, 9, 10};
        /// the other resource hexes a seeded shuffle of <see cref="NumberPool"/> (cycled if the layout ever has more).
        /// </summary>
        private static void DealNumbers(Board b, BoardTopology topo, int[] startCorners, Rng rng)
        {
            var fixedHex = new bool[topo.HexCount];
            foreach (int c in startCorners)
            {
                if (c < 0) continue;
                foreach (int h in topo.VertexHexes(c))
                {
                    int n = b.HexTerrain[h] == F ? StartForestNumber : b.HexTerrain[h] == H ? StartHillsNumber
                        : b.HexTerrain[h] == Fi ? StartFieldsNumber : 0;
                    if (n == 0) continue;
                    b.HexNumber[h] = n;
                    fixedHex[h] = true;
                }
            }

            var mountains = new List<int>();
            var others = new List<int>();
            for (int h = 0; h < topo.HexCount; h++)
            {
                if (fixedHex[h] || !TerrainInfo.IsResource(b.HexTerrain[h])) continue;
                if (b.HexTerrain[h] == Mt) mountains.Add(h);
                else others.Add(h);
            }

            var mNums = new List<int>(MountainNumbers);
            rng.Shuffle(mNums);
            for (int i = 0; i < mountains.Count; i++) b.HexNumber[mountains[i]] = mNums[i % mNums.Count];

            var pool = new List<int>();
            while (pool.Count < others.Count) pool.AddRange(NumberPool);
            if (pool.Count > others.Count && others.Count >= NumberPool.Length) pool.RemoveRange(others.Count, pool.Count - others.Count);
            rng.Shuffle(pool);
            for (int i = 0; i < others.Count; i++) b.HexNumber[others[i]] = pool[i];
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
        /// Rival start: a corner 4-5 hex edges from home whose three hexes are exactly Forest, Hills and
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

        /// <summary>Settlement value of a corner: 10 per distinct non-Meadow terrain, +1 per non-Meadow hex, +5 with a Mountain.</summary>
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
