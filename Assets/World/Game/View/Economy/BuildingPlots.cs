using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>Where a building's model stands beside its flag.</summary>
    public struct Plot
    {
        /// <summary>Footprint centre on the ground.</summary>
        public Vector3 Centre;
        /// <summary>Unit direction from the flag into the plot (away from the road line).</summary>
        public Vector3 Into;
        /// <summary>Model yaw: the model's +Z (its front, door and sign) faces back towards the flag.</summary>
        public float Yaw;
        /// <summary>Half the side of the square footprint (m).</summary>
        public float Half;
        /// <summary>Hex the plot lies in.</summary>
        public int Hex;
        /// <summary>Circle (x, z, radius) kept clear of terrain props.</summary>
        public Vector3 Clearing => new Vector3(Centre.x, Centre.z, Half * 1.42f + 1.2f);
    }

    /// <summary>
    /// Building placement geometry on the 60 m hexes. A building stands beside its flag, inside one of the hexes the
    /// flag touches, fully clear of the road line (TerrainBuilder.RoadMargin plus a little) and of the village
    /// buildings, square and warehouse. The choice depends only on the flag, the type and the fixed village, so views
    /// rebuilt from state alone land in the same place. Footprints of neighbouring flags (20 m apart) never overlap.
    /// </summary>
    public static class BuildingPlots
    {
        /// <summary>Gap kept between the road line and a footprint (m).</summary>
        public static float RoadClear => TerrainBuilder.RoadMargin + 0.6f;

        /// <summary>Half footprint per size class: Small 3.2, Medium 3.8, Large 4.4, Huge 5.0 m.</summary>
        public static float HalfFor(BuildingSize size)
        {
            switch (size)
            {
                case BuildingSize.Small: return 3.2f;
                case BuildingSize.Medium: return 3.8f;
                case BuildingSize.Large: return 4.4f;
                default: return 5.0f;
            }
        }

        private struct Obstacle { public Vector3 C; public float HX, HZ, Yaw, R; public bool Circle; }
        private static List<Obstacle> _obstacles;
        private static VillageLayout _for;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _obstacles = null; _for = null; }

        private static List<Obstacle> Obstacles(VillageLayout L)
        {
            if (_obstacles != null && _for == L) return _obstacles;
            _for = L;
            _obstacles = new List<Obstacle>();
            if (L == null) return _obstacles;
            void Rect(Vector3 c, float w, float d, float yaw) => _obstacles.Add(new Obstacle { C = c, HX = w / 2, HZ = d / 2, Yaw = yaw });
            void Circ(Vector3 c, float r) => _obstacles.Add(new Obstacle { C = c, R = r, Circle = true });
            Circ(L.SquareCentre, VillageLayout.SquareRadius + 1f);
            Rect(L.Storehouse, VillageLayout.StorehouseWidth + 3f, VillageLayout.StorehouseDepth + 3f, L.StorehouseYaw);
            Rect(L.Market, 7f, 5f, L.MarketYaw);
            Circ(L.Bell, 2f);
            foreach (var (p, yaw) in L.Cottages) Rect(p, VillageLayout.CottageWidth + 2f, VillageLayout.CottageDepth + 2f, yaw);
            Circ(L.Outpost, 18f);
            var w = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Warehouse : null;
            if (w != null)
            {
                var c = w.FootprintCorners();
                var centre = (c[0] + c[1] + c[2] + c[3]) / 4f;
                float wx = (c[1] - c[0]).magnitude, wz = (c[2] - c[1]).magnitude;
                Rect(centre, wx + 3f, wz + 3f, BoardSpace.YawOf(c[2] - c[1]));
            }
            foreach (var n in new[] { L.MiraSpot, L.DoormanSpot, L.QuartermasterSpot, L.KeeperSpot, L.CaptainSpot, L.ScribeSpot }) Circ(n, 1.5f);
            return _obstacles;
        }

        /// <summary>How far a circle (centre p, radius r) reaches into the village obstacles (0 = clear).</summary>
        public static float VillageOverlap(VillageLayout L, Vector3 p, float r)
        {
            float worst = 0;
            foreach (var o in Obstacles(L))
            {
                float d;
                if (o.Circle) d = r + o.R - BoardSpace.Flat(p - o.C);
                else
                {
                    var local = Quaternion.Euler(0, -o.Yaw, 0) * (p - o.C);
                    float dx = Mathf.Max(Mathf.Abs(local.x) - o.HX, 0), dz = Mathf.Max(Mathf.Abs(local.z) - o.HZ, 0);
                    d = r - Mathf.Sqrt(dx * dx + dz * dz);
                }
                if (d > worst) worst = d;
            }
            return worst;
        }

        /// <summary>The plot for a building of this type on this flag.</summary>
        public static Plot For(WorldState s, VillageLayout L, int vertex, BuildingType type)
        {
            var topo = s.Board.Topology;
            var def = EconomyApi.Def(s, type);
            float half = HalfFor(def.Size);
            var flag = BoardSpace.Vertex(topo, vertex);
            bool corner = topo.IsCorner(vertex);
            Plot best = default; float bestScore = float.MinValue; bool found = false;
            foreach (int h in topo.VertexHexes(vertex))
            {
                var hc = BoardSpace.Hex(topo, h);
                var t = s.Board.HexTerrain[h];
                float terrainScore = 0;
                if (def.NeedsTerrain && t == def.Terrain) terrainScore += 3f;
                if (t == Terrain.Mountain) terrainScore -= 4f;
                if (t == Terrain.Meadow || t == Terrain.Fields) terrainScore += 1f;
                Vector3 baseInto, along = Vector3.zero;
                if (corner) { baseInto = hc - flag; baseInto.y = 0; baseInto.Normalize(); }
                else
                {
                    int he = topo.HexEdgeOfFlag(vertex);
                    BoardSpace.HexEdgeEnds(topo, he, out var a, out var b);
                    along = b - a; along.y = 0; along.Normalize();
                    baseInto = new Vector3(along.z, 0, -along.x);
                    if (Vector3.Dot(baseInto, hc - flag) < 0) baseInto = -baseInto;
                }
                // Candidates: straight in, then shifted along the road (at most 4 m, so neighbouring plots 20 m apart
                // never meet) or turned a little at corners, and pushed further from the road.
                foreach (float shift in corner ? new[] { 0f, 15f, -15f } : new[] { 0f, 4f, -4f })
                    foreach (float extra in new[] { 0f, 3f, 6f, 9f, 12f, 15f })
                    {
                        Vector3 into = baseInto, c;
                        if (corner)
                        {
                            into = Quaternion.Euler(0, shift, 0) * baseInto;
                            // Square footprint facing the flag: the nearer hex edge is (60 - |turn|) degrees off the axis.
                            float sin = Mathf.Sin((60f - Mathf.Abs(shift)) * Mathf.Deg2Rad);
                            float phi = (30f + Mathf.Abs(shift)) * Mathf.Deg2Rad; // edge normal vs the footprint's axis
                            float support = half * (Mathf.Cos(phi) + Mathf.Sin(phi));
                            c = flag + into * ((RoadClear + support) / sin + extra);
                        }
                        else c = flag + baseInto * (RoadClear + half + extra) + along * shift;
                        float score = terrainScore - Mathf.Abs(shift) * 0.08f - extra * 0.12f;
                        score -= VillageOverlap(L, c, half * 1.42f) * 20f;
                        if (TerrainBuilder.InPeak(c, half)) score -= 6f;
                        score -= h * 0.0001f; // deterministic tie break
                        if (score > bestScore)
                        {
                            bestScore = score; found = true;
                            best = new Plot { Centre = c, Into = into, Yaw = BoardSpace.YawOf(-into), Half = half, Hex = h };
                        }
                    }
            }
            if (!found)
            {
                var into = flag.sqrMagnitude > 1e-3f ? -flag.normalized : Vector3.forward;
                best = new Plot { Centre = flag + into * (RoadClear + half), Into = into, Yaw = BoardSpace.YawOf(-into), Half = half, Hex = BoardSpace.HexAt(flag) };
            }
            return best;
        }
    }
}
