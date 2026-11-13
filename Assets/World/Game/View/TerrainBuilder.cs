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

        public string Title => LandText.State != null ? LandText.HexTitle(LandText.State, HexId) : Terrain + " hex";
        public IEnumerable<string> Lines
        {
            get
            {
                var st = LandText.State;
                if (st != null && st.Economy != null)
                {
                    foreach (var line in LandText.HexLines(st, HexId)) yield return line;
                    yield break;
                }
                var ware = TerrainInfo.RawWare(Terrain);
                if (ware.HasValue) yield return "Gives " + Wares.DisplayName(ware.Value) + ".";
                else yield return "Gives nothing (open meadow).";
                if (Number > 0) yield return "Production number " + Number + ".";
                yield return "Hex id " + HexId + ".";
            }
        }
    }

    /// <summary>
    /// Builds the board terrain for the radius-4 map (61 hexes, 60 m hex edges): one flat walkable tile per hex tinted
    /// by terrain, props per terrain combined into one mesh per hex (trees, mounds and rocks, crop rows, mountain crags,
    /// grass), hex-edge lines, a low stone at every corner, the island rim and beach, invisible bounds, sky dome, fog and
    /// ambient light. God-View-only overlay: hex labels and faint rings at the suggested camp corners. Owner: package V.
    /// Props keep 4 m off the hex edges (main roads), out of the stock patch at every hex centre (the clearings passed
    /// in) and, through <see cref="SetClearings"/>, off the corners, plots and local paths of every camp. Mountain crags
    /// stand only where no plot can ever be (41 m out towards each hex-edge midpoint) and are registered with
    /// <see cref="WalkGrid"/>.
    /// </summary>
    public static class TerrainBuilder
    {
        /// <summary>Props stay this far (m) from the hex-edge lines, where flags and road segments are.</summary>
        public const float RoadMargin = 4f;
        /// <summary>Mountain crags: distance from the hex centre towards each hex-edge midpoint, and base radius.</summary>
        public const float PeakDistance = 41f, PeakRadius = 6.5f;
        /// <summary>God View hex labels sit this far south of the hex centre so the stock patch and its token stay visible.</summary>
        public const float LabelOffset = 22f;
        public const string PeakBlockerOwner = "land.peaks";

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

        /// <summary>Circles kept free of trees, rocks and crops: (x, z, radius) per entry.</summary>
        private sealed class Clearings
        {
            private readonly List<Vector3> _clear;
            public Clearings(IReadOnlyList<Vector3> clear) { _clear = clear != null ? new List<Vector3>(clear) : new List<Vector3>(); }
            public bool IsClear(Vector3 p, float margin = 0f)
            {
                foreach (var c in _clear)
                {
                    float dx = p.x - c.x, dz = p.z - c.y;
                    if (dx * dx + dz * dz < (c.z + margin) * (c.z + margin)) return false;
                }
                return true;
            }
        }

        /// <param name="clear">Circles (x, z, radius) kept free of props.</param>
        public static GameObject Build(Transform parent, Board board, IReadOnlyList<Vector3> clear, out GameObject godViewOnly)
        {
            var layout = new Clearings(clear);
            PropCounts.Clear();
            WalkGrid.RemoveStaticBlockers(PeakBlockerOwner);
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

            // Corner markers (150): a low stone disc where three lines meet.
            var posts = new MeshBuilder();
            foreach (int v in t.Corners)
            {
                var p = BoardSpace.Vertex(t, v);
                Disc(posts, p + Vector3.up * 0.04f, 1.2f, 12, new Color(0.74f, 0.70f, 0.60f));
            }
            posts.ToObject("CornerStones", root, vcMat);

            BuildIsland(root, layout, vcMat);
            BuildBounds(root);

            // God-view-only overlay.
            godViewOnly = new GameObject("GodViewOverlay");
            godViewOnly.transform.SetParent(parent, false);
            var overlayMat = MaterialFactory.CreateUnlit(Color.white, "GodOverlay");
            var corners = new MeshBuilder();
            foreach (int v in t.Corners) Disc(corners, BoardSpace.Vertex(t, v) + Vector3.up * 0.1f, 2.2f, 12, new Color(1f, 0.97f, 0.85f));
            corners.ToObject("CornerMarkers", godViewOnly.transform, overlayMat);
            var sites = new MeshBuilder();
            foreach (int v in board.SuggestedSettlementCorners) Ring(sites, BoardSpace.Vertex(t, v) + Vector3.up * 0.12f, 6.5f, 0.7f, 24, new Color(0.62f, 0.82f, 0.95f));
            sites.ToObject("SuggestedSettlementCorners", godViewOnly.transform, overlayMat);

            for (int h = 0; h < t.HexCount; h++)
            {
                var terrain = board.HexTerrain[h];
                string label = board.HexNumber[h] > 0 ? terrain + "\n" + board.HexNumber[h] : terrain.ToString();
                var tm = Signs.FlatText(godViewOnly.transform, label, LabelPoint(BoardSpace.Hex(t, h) + new Vector3(0, 0, -LabelOffset)), 6.5f, new Color(1f, 1f, 0.92f));
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

        private static void BuildHex(Transform root, Board board, BoardTopology t, int h, Clearings layout, Material mat)
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

            _ctx.Board = board; _ctx.Layout = layout; _ctx.Mat = mat;
            if (_tiles.Length != t.HexCount) { _tiles = new Transform[t.HexCount]; _props = new GameObject[t.HexCount]; _clearings = new List<Vector3>[t.HexCount]; _peaks = new List<Vector3>[t.HexCount]; }
            _tiles[h] = tile.transform; _clearings[h] = null; _peaks[h] = new List<Vector3>();
            _props[h] = BuildProps(board, t, h, layout, mat, tile.transform, null, true);
        }

        // ---------------- props, rebuildable per hex with clearings ----------------

        private struct Ctx { public Board Board; public Clearings Layout; public Material Mat; }
        private static Ctx _ctx;
        private static Transform[] _tiles = new Transform[0];
        private static GameObject[] _props = new GameObject[0];
        private static List<Vector3>[] _clearings = new List<Vector3>[0];
        private static List<Vector3>[] _peaks = new List<Vector3>[0];

        /// <summary>Mountain peak footprints (x, z, base radius) on a hex; empty for other terrain.</summary>
        public static IReadOnlyList<Vector3> PeakCircles(int hex) =>
            hex >= 0 && hex < _peaks.Length && _peaks[hex] != null ? _peaks[hex] : (IReadOnlyList<Vector3>)new Vector3[0];

        /// <summary>True when a ground point lies within margin of a mountain peak's base.</summary>
        public static bool InPeak(Vector3 p, float margin = 0f)
        {
            int h = BoardSpace.HexAt(p);
            foreach (var c in PeakCircles(h))
            {
                float dx = p.x - c.x, dz = p.z - c.y;
                if (dx * dx + dz * dz < (c.z + margin) * (c.z + margin)) return true;
            }
            return false;
        }

        /// <summary>The prop mesh object of a hex (null if it has none).</summary>
        public static GameObject HexProps(int hex) => hex >= 0 && hex < _props.Length ? _props[hex] : null;

        /// <summary>
        /// Keeps trees, rocks and crops out of these circles (x, z, radius) on a hex, e.g. building plots. Rebuilds that
        /// hex's prop mesh only when the set changes; the rest of the hex looks exactly the same (props are seeded per hex).
        /// </summary>
        public static void SetClearings(int hex, List<Vector3> circles)
        {
            if (hex < 0 || hex >= _tiles.Length || _tiles[hex] == null || _ctx.Board == null) return;
            var old = _clearings[hex];
            bool same = (old == null || old.Count == 0) ? (circles == null || circles.Count == 0)
                       : circles != null && circles.Count == old.Count;
            if (same && old != null && circles != null)
                for (int i = 0; i < old.Count && same; i++) same = (old[i] - circles[i]).sqrMagnitude < 1e-4f;
            if (same) return;
            _clearings[hex] = circles == null ? null : new List<Vector3>(circles);
            if (_props[hex] != null) Object.Destroy(_props[hex]);
            _props[hex] = BuildProps(_ctx.Board, _ctx.Board.Topology, hex, _ctx.Layout, _ctx.Mat, _tiles[hex], _clearings[hex], false);
        }

        private static GameObject BuildProps(Board board, BoardTopology t, int h, Clearings layout, Material mat, Transform tile, List<Vector3> clearings, bool first)
        {
            var terrain = board.HexTerrain[h];
            var rnd = new PropRandom(5000 + h * 7919);
            var centre = BoardSpace.Hex(t, h);
            float R = BoardSpace.HexSize;
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
            bool Ok(Vector3 p, float margin, float clear)
            {
                if (EdgeMargin(p - centre, R) < margin || !layout.IsClear(p, clear)) return false;
                if (clearings != null)
                    foreach (var c in clearings)
                    {
                        float dx = p.x - c.x, dz = p.z - c.y, rr = c.z + clear;
                        if (dx * dx + dz * dz < rr * rr) return false;
                    }
                return true;
            }
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
                    if (first) Count("tree", placed);
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
                    if (first) Count("hill prop", placed);
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
                    if (first) Count("crop row", placed);
                    break;
                }
                case Terrain.Mountain:
                {
                    // Six crags towards the hex-edge midpoints, 41 m out: at least 8 m from any possible plot (the side
                    // plots of the neighbouring corners are 13 m away) and 7 m from the hex-edge lines (main roads).
                    var sides = t.HexSides(h);
                    for (int i = 0; i < sides.Count; i++)
                    {
                        var mid = BoardSpace.HexEdgeMid(t, sides[i]);
                        var dir = mid - centre; dir.y = 0; dir.Normalize();
                        var p = centre + dir * PeakDistance;
                        float r = PeakRadius * rnd.Range(0.9f, 1.0f), height = rnd.Range(20f, 34f) * (i % 2 == 0 ? 1f : 0.8f);
                        if (!first) continue;
                        _peaks[h].Add(new Vector3(p.x, p.z, r));
                        WalkGrid.AddStaticBlocker(p, r, PeakBlockerOwner);
                        var peak = new MeshBuilder();
                        peak.Cylinder(p, r, r * 0.24f, height * 0.75f, 9, new Color(0.47f, 0.45f, 0.43f) * rnd.Range(0.9f, 1.1f), false);
                        peak.Cone(p + Vector3.up * height * 0.75f, r * 0.24f, height * 0.25f, 9, new Color(0.95f, 0.95f, 0.97f));
                        var go = peak.ToObject("Peak", tile, mat);
                        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; mc.convex = true;
                    }
                    for (int i = 0; i < 70; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 1f)) continue;
                        props.Rock(p, rnd.Range(0.6f, 2.2f), new Color(0.52f, 0.50f, 0.48f), rnd.Next);
                        placed++;
                    }
                    if (first) Count("mountain rock", placed);
                    break;
                }
                case Terrain.Pasture: // grass tufts and a few bushes; the sheep belong to the stock patch
                    for (int i = 0; i < 160; i++)
                    {
                        var p = RandomInHex(RoadMargin - 1f);
                        if (!Ok(p, RoadMargin - 1f, 0f)) continue;
                        props.Cone(p, rnd.Range(0.3f, 0.5f), rnd.Range(0.25f, 0.45f), 5, new Color(0.30f, 0.56f, 0.22f) * rnd.Range(0.95f, 1.08f));
                        placed++;
                    }
                    for (int i = 0; i < 18; i++)
                    {
                        var p = RandomInHex(RoadMargin);
                        if (!Ok(p, RoadMargin, 1f)) continue;
                        props.Rock(p, rnd.Range(0.8f, 1.4f), new Color(0.26f, 0.46f, 0.20f), rnd.Next);
                    }
                    if (first) Count("tuft", placed);
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
                    if (first) Count("flower", placed);
                    break;
            }
            return props.VertexCount > 0 ? props.ToObject("Props", tile, mat) : null;
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
        private static void BuildIsland(Transform root, Clearings layout, Material mat)
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
