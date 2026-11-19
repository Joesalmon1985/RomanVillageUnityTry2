using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>
    /// Abundance dressing of the resource hexes (god board plan AB-2, AB-5): every hex shows its pip value (2/12 = 1 ...
    /// 6/8 = 5, <see cref="BoardGenerator.Pips"/>) as five clearly different levels of the same terrain: Forest tree
    /// count and height, Hills clay cliff size, Fields extent, Pasture flock size, Mountain height. It is built at load
    /// from the board that is dealt, never baked into a scene, and shows no number, die or odds (AB-1). Original
    /// primitive shapes only, merged into one mesh per hex through <see cref="MeshBuilder"/> (vertex colours, one shared
    /// material). Pure view code: it reads a <see cref="Board"/> and never writes state.
    /// </summary>
    public static class HexDressing
    {
        /// <summary>Number of abundance levels (one per pip value 1..5).</summary>
        public const int Levels = 5;
        /// <summary>Radius kept free at the hex centre for the stock patch and its token (as <see cref="LandViewsInstaller.PatchClearing"/>).</summary>
        public const float CentreClear = 8.5f;
        /// <summary>Props keep this far from the hex edges (main roads), like <see cref="TerrainBuilder.RoadMargin"/>.</summary>
        public const float EdgeClear = 4f;

        /// <summary>Placement test: (world point, margin to the hex edge, clear radius) to true when a prop may stand there.</summary>
        public delegate bool PlaceOk(Vector3 p, float edgeMargin, float clearRadius);

        /// <summary>Pips of a hex (1..5), 0 for Meadow or a hex without a number.</summary>
        public static int PipsOf(Board board, int hex) => BoardGenerator.Pips(board.HexNumber[hex]);

        /// <summary>Level index 0..4 for a pip value (clamped), or -1 when there are no pips.</summary>
        public static int Level(int pips) => pips <= 0 ? -1 : Mathf.Clamp(pips, 1, Levels) - 1;

        // ---- Per-level tables (index = level 0..4). Chosen so that neighbouring levels differ at a glance from the god camera. ----
        public static readonly int[] TreeCount = { 9, 20, 36, 58, 88 };
        public static readonly float[] TreeScale = { 0.8f, 1.0f, 1.25f, 1.55f, 1.9f };
        public static readonly float[] CliffRadius = { 5f, 7.5f, 10f, 13f, 17f };
        public static readonly float[] CliffHeight = { 3f, 5.5f, 8f, 11.5f, 16f };
        public static readonly int[] CliffMounds = { 0, 1, 2, 4, 6 };
        public static readonly float[] FieldHalfExtent = { 12f, 19f, 27f, 35f, 44f };
        public static readonly int[] FlockSize = { 3, 6, 10, 15, 22 };
        public static readonly float[] PeakHeights = { 13f, 21f, 30f, 41f, 55f };
        public static readonly float[] SnowFraction = { 0f, 0.14f, 0.24f, 0.34f, 0.44f };

        /// <summary>Height (m) of one Mountain crag at a level; <paramref name="variation"/> 0..1 gives the crags a slight spread.</summary>
        public static float PeakHeight(int level, float variation) => PeakHeights[level] * Mathf.Lerp(0.85f, 1.05f, variation);

        /// <summary>Draws one crag (rock body and snow cap) at the given level; used by the preview and the terrain builder.</summary>
        public static void Peak(MeshBuilder mb, Vector3 p, float radius, float height, int level, PropRandom rnd)
        {
            float snow = SnowFraction[level];
            float body = height * (1f - snow);
            var rock = new Color(0.47f, 0.45f, 0.43f) * rnd.Range(0.9f, 1.1f);
            mb.Cylinder(p, radius, radius * 0.24f, body, 9, rock, false);
            if (snow > 0f) mb.Cone(p + Vector3.up * body, radius * 0.24f + (1f - snow) * 0.5f, height * snow, 9, new Color(0.95f, 0.95f, 0.97f));
            else mb.Cone(p + Vector3.up * body, radius * 0.24f, 0.01f, 9, rock);
        }

        /// <summary>
        /// Adds the dressing of resource hex <paramref name="h"/> to <paramref name="mb"/> (world coordinates). Meadow and
        /// numberless hexes add nothing. Mountain crags are added only when <paramref name="withPeaks"/> (the terrain
        /// builder keeps its own crag colliders and passes false). Returns the number of main items placed (trees, cliff
        /// mounds, crop rows, sheep, crags) for tests.
        /// </summary>
        public static int Fill(MeshBuilder mb, Board board, int h, PlaceOk ok, bool withPeaks)
        {
            var terrain = board.HexTerrain[h];
            int level = Level(PipsOf(board, h));
            if (terrain == Terrain.Meadow || level < 0) return 0;
            var t = board.Topology;
            var centre = BoardSpace.Hex(t, h);
            float R = BoardSpace.HexSize;
            var rnd = new PropRandom(9000 + h * 7919 + level * 131);
            Vector3 RandomInHex()
            {
                for (int i = 0; i < 24; i++)
                {
                    var p = centre + new Vector3(rnd.Range(-R, R), 0, rnd.Range(-R, R));
                    if (TerrainBuilder.EdgeMargin(p - centre, R) >= EdgeClear) return p;
                }
                return centre;
            }
            int placed = 0;
            switch (terrain)
            {
                case Terrain.Forest:
                {
                    float sc = TreeScale[level];
                    int want = TreeCount[level];
                    for (int i = 0; i < want * 6 && placed < want; i++)
                    {
                        var p = RandomInHex();
                        if (!ok(p, EdgeClear, 2f * sc)) continue;
                        TerrainBuilder.Tree(mb, p, rnd, sc * rnd.Range(0.9f, 1.15f));
                        placed++;
                    }
                    for (int i = 0; i < 14; i++)
                    {
                        var p = RandomInHex();
                        if (!ok(p, EdgeClear, 1f)) continue;
                        mb.Rock(p, rnd.Range(0.7f, 1.4f), new Color(0.22f, 0.38f, 0.18f), rnd.Next);
                    }
                    break;
                }
                case Terrain.Hills:
                {
                    float r = CliffRadius[level], height = CliffHeight[level];
                    // One terraced clay cliff, the whole hex's size cue; try a ring of directions until it clears.
                    float a0 = rnd.Range(0f, 360f);
                    for (int k = 0; k < 24; k++)
                    {
                        var dir = BoardSpace.DirOf(a0 + k * 15f);
                        var p = centre + dir * (CentreClear + r + 6f + rnd.Range(0f, 8f));
                        if (!ok(p, r + EdgeClear, r)) continue;
                        Cliff(mb, p, r, height, rnd);
                        placed++;
                        break;
                    }
                    for (int m = 0; m < CliffMounds[level]; m++)
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            var p = RandomInHex();
                            float s = rnd.Range(3f, 5.5f);
                            if (!ok(p, s * 1.1f + 2f, s)) continue;
                            mb.Cylinder(p, s * 1.1f, s * 0.45f, s * 0.5f, 10, new Color(0.62f, 0.36f, 0.22f) * rnd.Range(0.95f, 1.08f), true);
                            placed++;
                            break;
                        }
                    }
                    for (int i = 0; i < 22; i++)
                    {
                        var p = RandomInHex();
                        if (!ok(p, EdgeClear, 1f)) continue;
                        mb.Rock(p, rnd.Range(0.4f, 1.3f), new Color(0.58f, 0.52f, 0.44f), rnd.Next);
                    }
                    break;
                }
                case Terrain.Fields:
                {
                    float e = FieldHalfExtent[level];
                    float ang = rnd.Range(0f, 180f);
                    var dir = BoardSpace.DirOf(ang); var side = new Vector3(dir.z, 0, -dir.x);
                    var cropA = new Color(0.90f, 0.74f, 0.26f); var cropB = new Color(0.66f, 0.70f, 0.24f);
                    const float rowStep = 3.4f, piece = 8f;
                    int rows = Mathf.FloorToInt(e / rowStep), cols = Mathf.FloorToInt(e / piece);
                    for (int row = -rows; row <= rows; row++)
                        for (int s = -cols; s <= cols; s++)
                        {
                            var p = centre + side * (row * rowStep) + dir * (s * piece);
                            var a = p - dir * (piece * 0.45f); var b = p + dir * (piece * 0.45f);
                            if (!ok(a, EdgeClear, 0.5f) || !ok(b, EdgeClear, 0.5f) || !ok(p, EdgeClear, 0.5f)) continue;
                            mb.Box(p + Vector3.up * 0.3f, new Vector3(1.1f, 0.6f + rnd.Range(-0.05f, 0.1f), piece * 0.9f), (row & 1) == 0 ? cropA : cropB, ang);
                            placed++;
                        }
                    // A tilled-earth ring marks the field's edge so the extent reads from above.
                    for (int i = 0; i < level + 1; i++)
                    {
                        var p = centre + side * rnd.Range(-e, e) + dir * rnd.Range(-e, e);
                        if (!ok(p, EdgeClear + 2f, 1.5f)) continue;
                        mb.Cylinder(p, 1.1f, 0.8f, 1.5f, 10, new Color(0.90f, 0.80f, 0.42f));
                    }
                    break;
                }
                case Terrain.Mountain:
                {
                    var sides = t.HexSides(h);
                    for (int i = 0; i < sides.Count; i++)
                    {
                        var mid = BoardSpace.HexEdgeMid(t, sides[i]);
                        var dir = mid - centre; dir.y = 0; dir.Normalize();
                        var p = centre + dir * TerrainBuilder.PeakDistance;
                        float height = PeakHeight(level, rnd.Next()) * (i % 2 == 0 ? 1f : 0.85f);
                        if (withPeaks) Peak(mb, p, TerrainBuilder.PeakRadius * rnd.Range(0.9f, 1f), height, level, rnd);
                        placed++;
                    }
                    for (int i = 0; i < 40; i++)
                    {
                        var p = RandomInHex();
                        if (!ok(p, EdgeClear, 1f)) continue;
                        mb.Rock(p, rnd.Range(0.6f, 2.0f), new Color(0.52f, 0.50f, 0.48f), rnd.Next);
                    }
                    break;
                }
                case Terrain.Pasture:
                {
                    int want = FlockSize[level];
                    int clusters = 1 + level / 2 + (level >= 3 ? 1 : 0);
                    var centres = new List<Vector3>();
                    for (int c = 0; c < clusters; c++)
                        for (int i = 0; i < 20; i++)
                        {
                            var p = RandomInHex();
                            if (!ok(p, EdgeClear + 8f, 6f)) continue;
                            centres.Add(p); break;
                        }
                    if (centres.Count == 0) centres.Add(centre + BoardSpace.DirOf(rnd.Range(0f, 360f)) * 28f);
                    for (int i = 0; i < want * 8 && placed < want; i++)
                    {
                        var c = centres[placed % centres.Count];
                        var p = c + new Vector3(rnd.Range(-7f, 7f), 0, rnd.Range(-7f, 7f));
                        if (!ok(p, EdgeClear, 2.4f)) continue;
                        Sheep(mb, p, rnd.Range(0f, 360f), 3.2f, rnd);
                        placed++;
                    }
                    for (int i = 0; i < 70; i++)
                    {
                        var p = RandomInHex();
                        if (!ok(p, EdgeClear - 1f, 0f)) continue;
                        mb.Cone(p, rnd.Range(0.3f, 0.5f), rnd.Range(0.25f, 0.45f), 5, new Color(0.30f, 0.56f, 0.22f) * rnd.Range(0.95f, 1.08f));
                    }
                    break;
                }
            }
            return placed;
        }

        /// <summary>A terraced clay cliff: stacked frustums that step in, red-brown bands, and a bare scrape at its foot.</summary>
        private static void Cliff(MeshBuilder mb, Vector3 p, float r, float height, PropRandom rnd)
        {
            var clay = new Color(0.66f, 0.38f, 0.24f);
            TerrainBuilder.Disc(mb, p + Vector3.up * 0.05f, r * 1.25f, 14, clay * 0.8f);
            int steps = 3;
            float y = 0f, rr = r;
            for (int i = 0; i < steps; i++)
            {
                float h = height / steps;
                var col = Color.Lerp(clay * 0.85f, new Color(0.78f, 0.52f, 0.34f), i / (float)(steps - 1)) * rnd.Range(0.95f, 1.05f);
                mb.Cylinder(p + Vector3.up * y, rr, rr * 0.72f, h, 9, col, true);
                y += h; rr *= 0.72f;
            }
        }

        /// <summary>A sheep of body length 1.25 * scale (stock patch sheep are scale 1).</summary>
        public static void Sheep(MeshBuilder mb, Vector3 p, float yaw, float scale, PropRandom rnd)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var wool = new Color(1f, 0.99f, 0.94f) * rnd.Range(0.95f, 1.02f); wool.a = 1;
            var dark = new Color(0.16f, 0.14f, 0.13f);
            Vector3 P(float x, float y, float z) => p + q * (new Vector3(x, y, z) * scale);
            mb.Box(P(0, 0.75f, 0), new Vector3(0.8f, 0.65f, 1.25f) * scale, wool, yaw);
            mb.Box(P(0, 0.95f, 0.72f), new Vector3(0.36f, 0.4f, 0.42f) * scale, dark, yaw);
            foreach (float x in new[] { -0.25f, 0.25f }) foreach (float z in new[] { -0.4f, 0.4f })
                mb.Box(P(x, 0.22f, z), new Vector3(0.12f, 0.44f, 0.12f) * scale, dark, yaw);
        }
    }
}
