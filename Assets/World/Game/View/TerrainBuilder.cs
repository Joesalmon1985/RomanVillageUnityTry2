using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>Inspectable hex tile (one per board hex; has the walkable collider).</summary>
    public sealed class HexView : MonoBehaviour, IInspectable
    {
        public int HexId;
        public Terrain Terrain;
        public int Number;

        public string Title => Terrain + " hex";
        public IEnumerable<string> Lines
        {
            get
            {
                var ware = TerrainInfo.RawWare(Terrain);
                if (ware.HasValue) yield return "Gives " + ware.Value.ToString().ToLowerInvariant() + ".";
                else if (Terrain == Terrain.Pasture) yield return "Grazing land. Gives nothing yet.";
                else yield return "Gives nothing (open meadow).";
                if (Number > 0) yield return "Production number " + Number + ".";
                yield return "Hex id " + HexId + ".";
            }
        }
    }

    /// <summary>
    /// Builds the board terrain for the radius-4 map (61 hexes, 60 m hex edges): one flat walkable tile per hex tinted
    /// by terrain (outer ring through the 18 flags of the hex), props per terrain combined into one mesh per hex
    /// (trees, mounds and rocks, crop rows, peaks, fenced pastures with sheep), hex-edge lines, a post at every corner,
    /// the island rim and beach, invisible bounds, sky dome, fog and ambient light. God-View-only overlay: hex labels,
    /// a plot disc at every intermediate flag and faint rings at the suggested city sites.
    /// Props keep 4 m off the hex edges (roads and flags) and out of the village and outpost clearings.
    /// </summary>
    public static class TerrainBuilder
    {
        /// <summary>Props stay this far (m) from the hex-edge lines, where flags and road segments are.</summary>
        public const float RoadMargin = 4f;
        /// <summary>Height of the God View labels (raised above trees and peaks, shifted south so they read over their hex).</summary>
        public const float LabelHeight = 70f;

        public static Color TerrainColor(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return new Color(0.27f, 0.45f, 0.20f);
                case Terrain.Hills: return new Color(0.55f, 0.55f, 0.32f);
                case Terrain.Fields: return new Color(0.70f, 0.62f, 0.30f);
                case Terrain.Mountain: return new Color(0.48f, 0.46f, 0.44f);
                case Terrain.Pasture: return new Color(0.34f, 0.60f, 0.27f);
                default: return new Color(0.50f, 0.62f, 0.30f);
            }
        }

        /// <summary>Number of props placed per terrain (for tests and the performance budget).</summary>
        public static readonly Dictionary<string, int> PropCounts = new Dictionary<string, int>();

        public static GameObject Build(Transform parent, Board board, VillageLayout layout, out GameObject godViewOnly)
        {
            PropCounts.Clear();
            GodCamera.ScaledLabels.Clear();
            var root = new GameObject("Terrain").transform;
            root.SetParent(parent, false);
            var t = board.Topology;
            var vcMat = MaterialFactory.CreateVertexColored(Color.white, "Terrain");

            for (int h = 0; h < t.HexCount; h++) BuildHex(root, board, t, h, layout, vcMat);

            // Hex-edge lines (210 x 60 m; each is three 20 m road segments).
            var lines = new MeshBuilder();
            var lineCol = new Color(0.80f, 0.75f, 0.58f);
            for (int he = 0; he < t.HexEdgeCount; he++)
            {
                BoardSpace.HexEdgeEnds(t, he, out var a, out var b);
                lines.Strip(a + Vector3.up * 0.03f, b + Vector3.up * 0.03f, 0.5f, lineCol);
            }
            lines.ToObject("HexEdgeLines", root, vcMat);

            // Corner markers (150): low stone disc and a short post with a pennant. Not inside the village square.
            var posts = new MeshBuilder();
            foreach (int v in t.Corners)
            {
                var p = BoardSpace.Vertex(t, v);
                Disc(posts, p + Vector3.up * 0.05f, 1.5f, 12, new Color(0.74f, 0.70f, 0.60f));
                if (BoardSpace.Flat(p - layout.SquareCentre) < VillageLayout.SquareRadius + 1f) continue;
                posts.Cylinder(p, 0.1f, 0.08f, 2.0f, 6, new Color(0.40f, 0.28f, 0.18f));
                posts.Box(p + new Vector3(0.24f, 1.8f, 0), new Vector3(0.44f, 0.28f, 0.03f), new Color(0.78f, 0.22f, 0.18f));
            }
            posts.ToObject("CornerPosts", root, vcMat);

            BuildIsland(root, layout, vcMat);
            BuildBounds(root);

            // God-view-only overlay.
            godViewOnly = new GameObject("GodViewOverlay");
            godViewOnly.transform.SetParent(parent, false);
            var overlayMat = MaterialFactory.CreateUnlit(Color.white, "GodOverlay");
            var plots = new MeshBuilder();
            for (int v = 0; v < t.VertexCount; v++)
            {
                if (t.IsCorner(v)) continue;
                Disc(plots, BoardSpace.Vertex(t, v) + Vector3.up * 0.09f, 1.1f, 10, new Color(0.96f, 0.93f, 0.80f));
            }
            plots.ToObject("PlotMarkers", godViewOnly.transform, overlayMat);
            var corners = new MeshBuilder();
            foreach (int v in t.Corners) Disc(corners, BoardSpace.Vertex(t, v) + Vector3.up * 0.1f, 2.2f, 12, new Color(1f, 0.97f, 0.85f));
            corners.ToObject("CornerMarkers", godViewOnly.transform, overlayMat);
            var sites = new MeshBuilder();
            foreach (int v in board.SuggestedCitySites) Ring(sites, BoardSpace.Vertex(t, v) + Vector3.up * 0.12f, 6.5f, 0.7f, 24, new Color(0.62f, 0.82f, 0.95f));
            sites.ToObject("SuggestedCitySites", godViewOnly.transform, overlayMat);

            for (int h = 0; h < t.HexCount; h++)
            {
                var terrain = board.HexTerrain[h];
                string label = board.HexNumber[h] > 0 ? terrain + "\n" + board.HexNumber[h] : terrain.ToString();
                var tm = Signs.FlatText(godViewOnly.transform, label, LabelPoint(BoardSpace.Hex(t, h)), 6.5f, new Color(1f, 1f, 0.92f));
                tm.name = "HexLabel " + h;
                GodCamera.ScaledLabels.Add(tm.transform);
            }
            godViewOnly.SetActive(false);
            return root.gameObject;
        }

        /// <summary>World point for a God View label that reads over ground point g (raised, shifted against the camera pitch).</summary>
        public static Vector3 LabelPoint(Vector3 g, float height = LabelHeight)
            => g + new Vector3(0, height, -height / Mathf.Tan(GodCamera.Pitch * Mathf.Deg2Rad));

        public static void Disc(MeshBuilder mb, Vector3 c, float r, int seg, Color col)
        {
            var ring = new List<Vector3>(seg);
            for (int k = 0; k < seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                ring.Add(c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }
            mb.FanUp(c, ring, col);
        }

        public static void Ring(MeshBuilder mb, Vector3 c, float r, float w, int seg, Color col)
        {
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector3 D(float a) => new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                mb.Quad(c + D(a0) * (r - w / 2), c + D(a1) * (r - w / 2), c + D(a1) * (r + w / 2), c + D(a0) * (r + w / 2), col);
            }
        }

        private static void Count(string kind, int n) { PropCounts.TryGetValue(kind, out int c); PropCounts[kind] = c + n; }

        private static void BuildHex(Transform root, Board board, BoardTopology t, int h, VillageLayout layout, Material mat)
        {
            var terrain = board.HexTerrain[h];
            var baseCol = TerrainColor(terrain);
            var rnd = new PropRandom(1000 + h * 7919);
            var centre = BoardSpace.Hex(t, h);
            float R = BoardSpace.HexSize;

            // Tile: centre, a jittered inner ring and the 18 ring flags (sorted counter-clockwise), with a skirt.
            var outer = new List<Vector3>();
            foreach (int v in t.HexFlags(h)) outer.Add(BoardSpace.Vertex(t, v));
            outer.Sort((a, b) => Mathf.Atan2(a.z - centre.z, a.x - centre.x).CompareTo(Mathf.Atan2(b.z - centre.z, b.x - centre.x)));
            int n = outer.Count;
            var inner = new Vector3[n];
            for (int k = 0; k < n; k++)
                inner[k] = Vector3.Lerp(centre, outer[k], 0.5f) + new Vector3(rnd.Range(-4f, 4f), 0, rnd.Range(-4f, 4f));
            var mb = new MeshBuilder();
            Color Jit() { float j = rnd.Range(-0.04f, 0.04f); return new Color(baseCol.r + j, baseCol.g + j * 1.2f, baseCol.b + j * 0.6f); }
            var down = Vector3.down * 0.4f;
            for (int k = 0; k < n; k++)
            {
                int m = (k + 1) % n;
                mb.Tri(centre, inner[m], inner[k], Jit());
                mb.Tri(inner[k], inner[m], outer[m], Jit());
                mb.Tri(inner[k], outer[m], outer[k], Jit());
                mb.Quad(outer[k], outer[m], outer[m] + down, outer[k] + down, baseCol * 0.7f);
            }
            var tile = new GameObject("Hex " + h + " " + terrain);
            tile.transform.SetParent(root, false);
            var mesh = mb.ToMesh(tile.name);
            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            tile.AddComponent<MeshRenderer>().sharedMaterial = mat;
            tile.AddComponent<MeshCollider>().sharedMesh = mesh;
            var hv = tile.AddComponent<HexView>();
            hv.HexId = h; hv.Terrain = terrain; hv.Number = board.HexNumber[h];

            // Props (one combined mesh per hex, no colliders except mountain peaks).
            var props = new MeshBuilder();
            Vector3 RandomInHex(float margin)
            {
                for (int i = 0; i < 24; i++)
                {
                    var p = centre + new Vector3(rnd.Range(-R, R), 0, rnd.Range(-R, R));
                    if (EdgeMargin(p - centre, R) >= margin) return p;
                }
                return centre;
            }
            bool Ok(Vector3 p, float margin, float clear) => EdgeMargin(p - centre, R) >= margin && layout.IsClear(p, clear);
            int placed = 0;
            switch (terrain)
            {
                case Terrain.Forest:
                    for (int i = 0; i < 190; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 2f)) continue;
                        Tree(props, p, rnd); placed++;
                    }
                    for (int i = 0; i < 30; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 1f)) continue;
                        props.Rock(p, rnd.Range(0.7f, 1.4f), new Color(0.22f, 0.38f, 0.18f), rnd.Next);
                    }
                    Count("tree", placed);
                    break;
                case Terrain.Hills:
                    for (int i = 0; i < 26; i++)
                    {
                        var p = RandomInHex(RoadMargin + 6f);
                        float s = rnd.Range(4f, 9f);
                        if (!Ok(p, s * 1.1f + 2f, s)) continue;
                        props.Cylinder(p, s * 1.1f, s * 0.45f, s * 0.18f, 10, new Color(0.50f, 0.52f, 0.30f) * rnd.Range(0.95f, 1.08f), true);
                        placed++;
                    }
                    for (int i = 0; i < 70; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 1f)) continue;
                        props.Rock(p, rnd.Range(0.4f, 1.5f), new Color(0.58f, 0.56f, 0.52f), rnd.Next);
                        placed++;
                    }
                    Count("hill prop", placed);
                    break;
                case Terrain.Fields:
                {
                    float ang = rnd.Range(0f, 180f);
                    var dir = BoardSpace.DirOf(ang); var side = new Vector3(dir.z, 0, -dir.x);
                    var cropA = new Color(0.86f, 0.72f, 0.28f); var cropB = new Color(0.62f, 0.66f, 0.26f);
                    const float rowStep = 3.2f, piece = 8f;
                    for (int row = -18; row <= 18; row++)
                    {
                        for (int s = -7; s <= 7; s++)
                        {
                            var p = centre + side * (row * rowStep) + dir * (s * piece);
                            var a = p - dir * (piece * 0.45f); var b = p + dir * (piece * 0.45f);
                            if (!Ok(a, RoadMargin, 0.5f) || !Ok(b, RoadMargin, 0.5f)) continue;
                            props.Box(p + Vector3.up * 0.26f, new Vector3(0.95f, 0.52f + rnd.Range(-0.06f, 0.1f), piece * 0.9f), (row & 1) == 0 ? cropA : cropB, ang);
                            placed++;
                        }
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        var p = RandomInHex(RoadMargin + 2f);
                        if (!Ok(p, RoadMargin + 2f, 1f)) continue;
                        props.Cylinder(p + side * 1.6f, 0.75f, 0.75f, 1.1f, 10, new Color(0.88f, 0.78f, 0.40f));
                    }
                    Count("crop row", placed);
                    break;
                }
                case Terrain.Mountain:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var p = centre + BoardSpace.DirOf(i * 120f + rnd.Range(0, 60f)) * rnd.Range(6f, 16f);
                        float r = rnd.Range(18f, 25f), height = rnd.Range(38f, 58f);
                        var peak = new MeshBuilder();
                        peak.Cylinder(p, r, r * 0.22f, height * 0.78f, 9, new Color(0.47f, 0.45f, 0.43f) * rnd.Range(0.9f, 1.1f), false);
                        peak.Cone(p + Vector3.up * height * 0.78f, r * 0.22f, height * 0.22f, 9, new Color(0.95f, 0.95f, 0.97f));
                        var go = peak.ToObject("Peak", tile.transform, mat);
                        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; mc.convex = true;
                    }
                    for (int i = 0; i < 70; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 1f)) continue;
                        props.Rock(p, rnd.Range(0.6f, 2.2f), new Color(0.52f, 0.50f, 0.48f), rnd.Next);
                        placed++;
                    }
                    Count("mountain rock", placed);
                    break;
                }
                case Terrain.Pasture:
                    placed = BuildPasture(props, centre, R, layout, rnd);
                    Count("sheep", placed);
                    break;
                default: // Meadow: flowers, bushes and the odd lone tree
                    for (int i = 0; i < 300; i++)
                    {
                        var p = RandomInHex(RoadMargin - 1.5f);
                        if (!Ok(p, RoadMargin - 1.5f, 0f)) continue;
                        var fc = i % 3 == 0 ? new Color(0.95f, 0.85f, 0.35f) : i % 3 == 1 ? new Color(0.92f, 0.92f, 0.95f) : new Color(0.75f, 0.45f, 0.75f);
                        props.Box(p + Vector3.up * 0.12f, new Vector3(0.25f, 0.24f, 0.25f), fc);
                        placed++;
                    }
                    for (int i = 0; i < 28; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 1f)) continue;
                        props.Rock(p, rnd.Range(0.8f, 1.5f), new Color(0.30f, 0.48f, 0.22f), rnd.Next);
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        var p = RandomInHex(RoadMargin + 2f);
                        if (!Ok(p, RoadMargin + 2f, 3f)) continue;
                        Tree(props, p, rnd, 1.3f);
                    }
                    Count("flower", placed);
                    break;
            }
            if (props.VertexCount > 0) props.ToObject("Props", tile.transform, mat);
        }

        /// <summary>A hex-shaped fence inset in the hex with a gate, sheep, a trough and grass tufts. Returns the sheep count.</summary>
        private static int BuildPasture(MeshBuilder mb, Vector3 centre, float R, VillageLayout layout, PropRandom rnd)
        {
            var wood = new Color(0.48f, 0.34f, 0.20f);
            float fr = R * 0.62f; // fence circumradius: 37 m, about 19 m inside the roads
            var corners = new Vector3[6];
            for (int k = 0; k < 6; k++)
            {
                float a = (60f * k - 30f) * Mathf.Deg2Rad;
                corners[k] = centre + new Vector3(Mathf.Cos(a) * fr, 0, Mathf.Sin(a) * fr);
            }
            for (int k = 0; k < 6; k++)
            {
                var a = corners[k]; var b = corners[(k + 1) % 6];
                var d = b - a; float len = d.magnitude; var dir = d / len; float yaw = BoardSpace.YawOf(dir);
                int posts = Mathf.CeilToInt(len / 3.5f);
                for (int i = 0; i <= posts; i++)
                {
                    float s = i * len / posts;
                    bool gate = k == 4 && Mathf.Abs(s - len / 2) < 2.4f;
                    if (gate) continue;
                    mb.Box(a + dir * s + Vector3.up * 0.6f, new Vector3(0.16f, 1.2f, 0.16f), wood);
                }
                // Two rails, broken for the gate on side 4.
                foreach (float y in new[] { 0.5f, 1.0f })
                {
                    if (k == 4)
                    {
                        float g0 = len / 2 - 2.2f, g1 = len / 2 + 2.2f;
                        mb.Box(a + dir * (g0 / 2) + Vector3.up * y, new Vector3(0.08f, 0.1f, g0), wood * 1.1f, yaw);
                        mb.Box(a + dir * ((g1 + len) / 2) + Vector3.up * y, new Vector3(0.08f, 0.1f, len - g1), wood * 1.1f, yaw);
                    }
                    else mb.Box(a + dir * (len / 2) + Vector3.up * y, new Vector3(0.08f, 0.1f, len), wood * 1.1f, yaw);
                }
            }
            // Water trough near the centre.
            mb.Box(centre + new Vector3(4f, 0.35f, -3f), new Vector3(3f, 0.7f, 0.9f), wood);
            mb.Box(centre + new Vector3(4f, 0.66f, -3f), new Vector3(2.7f, 0.05f, 0.65f), new Color(0.35f, 0.55f, 0.75f));
            // Grass tufts.
            for (int i = 0; i < 70; i++)
            {
                var p = centre + new Vector3(rnd.Range(-fr, fr), 0, rnd.Range(-fr, fr));
                if (EdgeMargin(p - centre, fr) < 1f) continue;
                mb.Cone(p, rnd.Range(0.3f, 0.5f), rnd.Range(0.25f, 0.4f), 5, new Color(0.30f, 0.56f, 0.22f) * rnd.Range(0.95f, 1.08f));
            }
            // Sheep, grazing in two loose flocks.
            int sheep = 0;
            for (int i = 0; i < 28; i++)
            {
                var flock = centre + (i % 2 == 0 ? new Vector3(-9f, 0, -8f) : new Vector3(10f, 0, 6f));
                var p = flock + new Vector3(rnd.Range(-11f, 11f), 0, rnd.Range(-9f, 9f));
                if (EdgeMargin(p - centre, fr) < 3f || !layout.IsClear(p, 1f)) continue;
                Sheep(mb, p, rnd.Range(0f, 360f), rnd);
                sheep++;
            }
            return sheep;
        }

        public static void Sheep(MeshBuilder mb, Vector3 p, float yaw, PropRandom rnd)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var wool = new Color(1f, 0.99f, 0.94f) * rnd.Range(0.95f, 1.02f); wool.a = 1;
            var dark = new Color(0.16f, 0.14f, 0.13f);
            mb.Box(p + q * new Vector3(0, 0.75f, 0), new Vector3(0.8f, 0.65f, 1.25f), wool, yaw);
            mb.Box(p + q * new Vector3(0, 0.95f, 0.72f), new Vector3(0.36f, 0.4f, 0.42f), dark, yaw);
            foreach (float x in new[] { -0.25f, 0.25f }) foreach (float z in new[] { -0.4f, 0.4f })
                mb.Box(p + q * new Vector3(x, 0.22f, z), new Vector3(0.12f, 0.44f, 0.12f), dark, yaw);
        }

        public static void Tree(MeshBuilder mb, Vector3 p, PropRandom rnd, float scale = 1f)
        {
            float s = rnd.Range(1.0f, 1.6f) * scale;
            mb.Cylinder(p, 0.25f * s, 0.18f * s, 2.2f * s, 6, new Color(0.38f, 0.26f, 0.16f));
            var g = new Color(0.16f, 0.36f, 0.16f) * rnd.Range(0.85f, 1.15f);
            mb.Cone(p + Vector3.up * 1.6f * s, 2.0f * s, 3.4f * s, 7, g);
            mb.Cone(p + Vector3.up * 3.3f * s, 1.5f * s, 3.0f * s, 7, g * 1.1f);
        }

        /// <summary>Distance from a point (relative to the hex centre) to the border of a pointy-top hex of circumradius r (negative outside).</summary>
        public static float EdgeMargin(Vector3 d, float r)
        {
            float ax = Mathf.Abs(d.x), az = Mathf.Abs(d.z), k = r * 0.8660254f;
            return k - Mathf.Max(ax, ax * 0.5f + az * 0.8660254f);
        }

        /// <summary>Island ground beyond the board: a grass rim with trees and soft hills, a sandy beach and a ragged coast.</summary>
        private static void BuildIsland(Transform root, VillageLayout layout, Material mat)
        {
            float br = BoardSpace.BoardRadius, ir = BoardSpace.IslandRadius;
            var rnd = new PropRandom(4242);
            int seg = 120;
            var coast = new float[seg];
            for (int i = 0; i < seg; i++) coast[i] = ir * (1f + 0.035f * Mathf.Sin(i * 0.37f) + 0.02f * Mathf.Sin(i * 1.3f + 1f));
            var ground = new MeshBuilder();
            var grass = new Color(0.36f, 0.52f, 0.26f); var sand = new Color(0.82f, 0.76f, 0.56f); var wet = new Color(0.62f, 0.58f, 0.44f);
            Vector3 P(int i, float r, float y) { float a = i * Mathf.PI * 2f / seg; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                // Order (centre, j, i) faces up: angles increase counter-clockwise seen from above.
                ground.Tri(new Vector3(0, -0.05f, 0), P(j, coast[j] - 16f, -0.05f), P(i, coast[i] - 16f, -0.05f), grass);
                ground.Quad(P(i, coast[i] - 16f, -0.05f), P(j, coast[j] - 16f, -0.05f), P(j, coast[j], -0.15f), P(i, coast[i], -0.15f), sand);
                ground.Quad(P(i, coast[i], -0.15f), P(j, coast[j], -0.15f), P(j, coast[j] + 10f, -1.2f), P(i, coast[i] + 10f, -1.2f), wet);
            }
            var island = ground.ToObject("Island", root, mat);
            island.AddComponent<MeshCollider>().sharedMesh = island.GetComponent<MeshFilter>().sharedMesh;

            var mb = new MeshBuilder();
            int trees = 0;
            for (int i = 0; i < 700; i++)
            {
                float a = rnd.Range(0, Mathf.PI * 2f), r = rnd.Range(br + 10f, ir - 22f);
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (!layout.IsClear(p, 2f)) continue;
                Tree(mb, p, rnd); trees++;
            }
            for (int i = 0; i < 34; i++)
            {
                float a = i * Mathf.PI * 2f / 34f + rnd.Range(-0.05f, 0.05f), r = rnd.Range(br + 18f, ir - 30f);
                var p = new Vector3(Mathf.Cos(a) * r, -1f, Mathf.Sin(a) * r);
                float rad = rnd.Range(14f, 26f), hgt = rnd.Range(6f, 16f);
                mb.Cylinder(p, rad, rad * 0.35f, hgt, 10, new Color(0.30f, 0.44f, 0.30f) * rnd.Range(0.85f, 1.1f), true);
            }
            Count("rim tree", trees);
            mb.ToObject("Outskirts", root, mat);
        }

        private static void BuildBounds(Transform root)
        {
            var b = new GameObject("WorldBounds").transform;
            b.SetParent(root, false);
            float r = BoardSpace.BoardRadius + 6f;
            int n = 48;
            float seg = 2f * r * Mathf.Tan(Mathf.PI / n) + 1f;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) * 360f / n;
                var dir = BoardSpace.DirOf(a);
                var go = new GameObject("Bound " + i);
                go.transform.SetParent(b, false);
                go.transform.position = dir * (r + 1f) + Vector3.up * 10f;
                go.transform.rotation = Quaternion.Euler(0, a, 0);
                go.AddComponent<BoxCollider>().size = new Vector3(seg, 30f, 2f);
            }
        }

        /// <summary>First-person fog range (m): the village stays crisp, the far side of the 950 m island fades out.</summary>
        public const float FogStart = 140f, FogEnd = 700f;

        /// <summary>Sky dome, fog, ambient and sun tuning.</summary>
        public static void BuildSky(Transform parent, Light sun, Camera cam)
        {
            var horizon = new Color(0.78f, 0.84f, 0.88f);
            var zenith = new Color(0.33f, 0.55f, 0.85f);
            var mb = new MeshBuilder();
            int seg = 32, rings = 20; float R = 1100f;
            for (int i = 0; i < rings; i++)
            {
                float e0 = Mathf.Lerp(-10f, 90f, i / (float)rings) * Mathf.Deg2Rad;
                float e1 = Mathf.Lerp(-10f, 90f, (i + 1) / (float)rings) * Mathf.Deg2Rad;
                var c = Color.Lerp(horizon, zenith, Mathf.Pow(Mathf.Clamp01((i + 0.5f) / rings), 0.7f));
                for (int j = 0; j < seg; j++)
                {
                    float a0 = j * Mathf.PI * 2f / seg, a1 = (j + 1) * Mathf.PI * 2f / seg;
                    Vector3 P(float e, float a) => new Vector3(Mathf.Cos(e) * Mathf.Cos(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(a)) * R;
                    // inward facing
                    mb.Quad(P(e0, a0), P(e0, a1), P(e1, a1), P(e1, a0), c);
                }
            }
            var mat = MaterialFactory.CreateUnlit(Color.white, "Sky");
            mat.SetFloat("_NoFog", 1f);
            var dome = mb.ToObject("SkyDome", parent, mat);
            dome.AddComponent<SkyFollow>();

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = horizon;
            cam.farClipPlane = 1500f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.52f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.24f);

            sun.transform.rotation = Quaternion.Euler(38f, -150f, 0f);
            sun.color = new Color(1f, 0.93f, 0.80f);
            sun.intensity = 1.15f;
            sun.shadowStrength = 0.75f;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.pixelLightCount = 6;
        }
    }

    /// <summary>Keeps the sky dome centred on whichever camera renders; hidden for the orthographic God View camera.</summary>
    public sealed class SkyFollow : MonoBehaviour
    {
        private Renderer _r;
        private void LateUpdate()
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (cam == null) return;
            if (_r == null) _r = GetComponent<Renderer>();
            if (_r != null) _r.enabled = !cam.orthographic;
            var p = cam.transform.position; p.y = 0;
            transform.position = p;
        }
    }
}
