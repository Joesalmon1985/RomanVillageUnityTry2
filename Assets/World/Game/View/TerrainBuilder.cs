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
                yield return ware.HasValue ? "Gives " + ware.Value.ToString().ToLowerInvariant() + "." : "Gives nothing (open meadow).";
                if (Number > 0) yield return "Production number " + Number + ".";
                yield return "Hex id " + HexId + ".";
            }
        }
    }

    /// <summary>
    /// Builds the board terrain: one flat walkable tile per hex tinted by terrain, props per terrain
    /// (trees, rocks, crop rows, peaks), faint edge lines, a post and disc at every vertex, scenery outside
    /// the board, invisible bounds, sky dome, fog and ambient light.
    /// </summary>
    public static class TerrainBuilder
    {
        public static Color TerrainColor(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return new Color(0.27f, 0.45f, 0.20f);
                case Terrain.Hills: return new Color(0.55f, 0.55f, 0.32f);
                case Terrain.Fields: return new Color(0.70f, 0.62f, 0.30f);
                case Terrain.Mountain: return new Color(0.48f, 0.46f, 0.44f);
                default: return new Color(0.45f, 0.62f, 0.28f);
            }
        }

        public static GameObject Build(Transform parent, Board board, VillageLayout layout, out GameObject godViewOnly)
        {
            var root = new GameObject("Terrain").transform;
            root.SetParent(parent, false);
            var t = board.Topology;
            var vcMat = MaterialFactory.CreateVertexColored(Color.white, "Terrain");

            for (int h = 0; h < t.HexCount; h++) BuildHex(root, board, t, h, layout, vcMat);

            // Edge lines.
            var lines = new MeshBuilder();
            var lineCol = new Color(0.86f, 0.80f, 0.62f);
            for (int e = 0; e < t.EdgeCount; e++)
            {
                var a = BoardSpace.Vertex(t, t.EdgeVertexA(e)) + Vector3.up * 0.03f;
                var b = BoardSpace.Vertex(t, t.EdgeVertexB(e)) + Vector3.up * 0.03f;
                lines.Strip(a, b, 0.45f, lineCol);
            }
            lines.ToObject("EdgeLines", root, vcMat);

            // Vertex posts (flag spots) with a disc so they read from above.
            var posts = new MeshBuilder();
            for (int v = 0; v < t.VertexCount; v++)
            {
                var p = BoardSpace.Vertex(t, v);
                var ring = new List<Vector3>();
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    ring.Add(p + new Vector3(Mathf.Cos(a) * 1.1f, 0.06f, Mathf.Sin(a) * 1.1f));
                }
                posts.FanUp(p + Vector3.up * 0.06f, ring, new Color(0.80f, 0.76f, 0.64f));
                posts.Cylinder(p, 0.09f, 0.08f, 1.5f, 6, new Color(0.40f, 0.28f, 0.18f));
                posts.Box(p + new Vector3(0.22f, 1.32f, 0), new Vector3(0.4f, 0.26f, 0.03f), new Color(0.78f, 0.22f, 0.18f));
            }
            posts.ToObject("VertexPosts", root, vcMat);

            BuildOutskirts(root, vcMat);
            BuildBounds(root);

            // God-view-only overlay: hex numbers drawn flat on the ground.
            godViewOnly = new GameObject("GodViewOverlay");
            godViewOnly.transform.SetParent(parent, false);
            for (int h = 0; h < t.HexCount; h++)
            {
                var c = BoardSpace.Hex(t, h);
                string label = board.HexTerrain[h] == Terrain.Meadow ? "Meadow" : board.HexTerrain[h] + "\n" + board.HexNumber[h];
                var tm = Signs.FlatText(godViewOnly.transform, label, c + new Vector3(0, 9f, -6f), 2.2f, new Color(1f, 1f, 0.92f));
                tm.name = "HexLabel " + h;
            }
            godViewOnly.SetActive(false);
            return root.gameObject;
        }

        private static List<Vector3> ReverseToCcw(List<Vector3> r) => r; // ring above is built clockwise in angle; see FanUp

        private static void BuildHex(Transform root, Board board, BoardTopology t, int h, VillageLayout layout, Material mat)
        {
            var terrain = board.HexTerrain[h];
            var baseCol = TerrainColor(terrain);
            var rnd = new PropRandom(1000 + h * 7919);
            var centre = BoardSpace.Hex(t, h);

            // Tile: centre, inner ring (0.5) and outer corners, flat at y = 0, with a skirt down to -0.4.
            var mb = new MeshBuilder();
            var inner = new Vector3[6]; var outer = new Vector3[6];
            for (int k = 0; k < 6; k++)
            {
                inner[k] = Vector3.Lerp(centre, BoardSpace.HexCorner(t, h, k), 0.5f) + new Vector3(rnd.Range(-1.5f, 1.5f), 0, rnd.Range(-1.5f, 1.5f));
                outer[k] = BoardSpace.HexCorner(t, h, k);
            }
            Color Jit() { float j = rnd.Range(-0.05f, 0.05f); return new Color(baseCol.r + j, baseCol.g + j * 1.2f, baseCol.b + j * 0.6f); }
            for (int k = 0; k < 6; k++)
            {
                int n = (k + 1) % 6;
                // corners go counter-clockwise in angle (seen from above), so (centre, next, this) is clockwise
                mb.Tri(centre, inner[n], inner[k], Jit());
                mb.Tri(inner[k], inner[n], outer[n], Jit());
                mb.Tri(inner[k], outer[n], outer[k], Jit());
                var down = Vector3.down * 0.4f;
                mb.Quad(outer[k], outer[n], outer[n] + down, outer[k] + down, baseCol * 0.7f);
            }
            var tile = new GameObject("Hex " + h + " " + terrain);
            tile.transform.SetParent(root, false);
            var mesh = mb.ToMesh(tile.name);
            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            tile.AddComponent<MeshRenderer>().sharedMaterial = mat;
            tile.AddComponent<MeshCollider>().sharedMesh = mesh;
            var hv = tile.AddComponent<HexView>();
            hv.HexId = h; hv.Terrain = terrain; hv.Number = board.HexNumber[h];

            // Props.
            var props = new MeshBuilder();
            float inR = (float)BoardTopology.EdgeLength * 0.82f;
            Vector3 RandomInHex()
            {
                for (int i = 0; i < 20; i++)
                {
                    var p = centre + new Vector3(rnd.Range(-inR, inR), 0, rnd.Range(-inR, inR));
                    if (InsideHex(p - centre, inR)) return p;
                }
                return centre;
            }
            switch (terrain)
            {
                case Terrain.Forest:
                    for (int i = 0; i < 26; i++)
                    {
                        var p = RandomInHex();
                        if (!layout.IsClear(p, 2f) || NearAnyVertex(t, p, 2.5f)) continue;
                        Tree(props, p, rnd);
                    }
                    break;
                case Terrain.Hills:
                    for (int i = 0; i < 9; i++)
                    {
                        var p = RandomInHex();
                        if (!layout.IsClear(p, 4f)) continue;
                        float s = rnd.Range(2.5f, 4.5f);
                        props.Cylinder(p, s * 1.1f, s * 0.45f, s * 0.15f, 9, new Color(0.50f, 0.52f, 0.30f) * rnd.Range(0.95f, 1.08f), true);
                    }
                    for (int i = 0; i < 18; i++)
                    {
                        var p = RandomInHex();
                        if (!layout.IsClear(p, 1f)) continue;
                        props.Rock(p, rnd.Range(0.4f, 1.3f), new Color(0.58f, 0.56f, 0.52f), rnd.Next);
                    }
                    break;
                case Terrain.Fields:
                {
                    float ang = rnd.Range(0f, 180f);
                    var dir = BoardSpace.DirOf(ang); var side = new Vector3(dir.z, 0, -dir.x);
                    var cropA = new Color(0.86f, 0.72f, 0.28f); var cropB = new Color(0.62f, 0.66f, 0.26f);
                    for (int row = -7; row <= 7; row++)
                    {
                        for (int s = -10; s <= 10; s++)
                        {
                            var p = centre + side * (row * 2.0f) + dir * (s * 1.4f);
                            if (!InsideHex(p - centre, inR) || !layout.IsClear(p, 0.5f) || NearAnyVertex(t, p, 1.6f)) continue;
                            props.Box(p + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f + rnd.Range(-0.1f, 0.15f), 1.3f), (row & 1) == 0 ? cropA : cropB, ang);
                        }
                    }
                    break;
                }
                case Terrain.Mountain:
                {
                    int peaks = 3;
                    for (int i = 0; i < peaks; i++)
                    {
                        var p = centre + BoardSpace.DirOf(i * 120f + rnd.Range(0, 60f)) * rnd.Range(3f, 7f);
                        float r = rnd.Range(7f, 9.5f), height = rnd.Range(16f, 26f);
                        var peak = new MeshBuilder();
                        peak.Cylinder(p, r, r * 0.22f, height * 0.78f, 8, new Color(0.47f, 0.45f, 0.43f) * rnd.Range(0.9f, 1.1f), false);
                        peak.Cone(p + Vector3.up * height * 0.78f, r * 0.22f, height * 0.22f, 8, new Color(0.95f, 0.95f, 0.97f));
                        var go = peak.ToObject("Peak", tile.transform, mat);
                        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; mc.convex = true;
                    }
                    for (int i = 0; i < 12; i++) props.Rock(RandomInHex(), rnd.Range(0.6f, 1.6f), new Color(0.52f, 0.50f, 0.48f), rnd.Next);
                    break;
                }
                default: // Meadow: flowers and the odd bush
                    for (int i = 0; i < 40; i++)
                    {
                        var p = RandomInHex();
                        if (!layout.IsClear(p, 0f) || NearAnyVertex(t, p, 1.5f)) continue;
                        var fc = i % 3 == 0 ? new Color(0.95f, 0.85f, 0.35f) : i % 3 == 1 ? new Color(0.92f, 0.92f, 0.95f) : new Color(0.75f, 0.45f, 0.75f);
                        props.Box(p + Vector3.up * 0.12f, new Vector3(0.25f, 0.24f, 0.25f), fc);
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        var p = RandomInHex();
                        if (!layout.IsClear(p, 1f) || NearAnyVertex(t, p, 2f)) continue;
                        props.Rock(p, rnd.Range(0.8f, 1.3f), new Color(0.30f, 0.48f, 0.22f), rnd.Next);
                    }
                    break;
            }
            if (props.VertexCount > 0) props.ToObject("Props", tile.transform, mat);
        }

        public static void Tree(MeshBuilder mb, Vector3 p, PropRandom rnd)
        {
            float s = rnd.Range(0.8f, 1.3f);
            mb.Cylinder(p, 0.25f * s, 0.18f * s, 2.2f * s, 6, new Color(0.38f, 0.26f, 0.16f));
            var g = new Color(0.16f, 0.36f, 0.16f) * rnd.Range(0.85f, 1.15f);
            mb.Cone(p + Vector3.up * 1.6f * s, 2.0f * s, 3.4f * s, 7, g);
            mb.Cone(p + Vector3.up * 3.3f * s, 1.5f * s, 3.0f * s, 7, g * 1.1f);
        }

        private static bool InsideHex(Vector3 d, float r)
        {
            // pointy-top hex with circumradius r: |x| <= r*sqrt3/2 and |x|*0.5 + |z|*sqrt3/2 <= r*sqrt3/2
            float ax = Mathf.Abs(d.x), az = Mathf.Abs(d.z), k = r * 0.8660254f;
            return ax <= k && ax * 0.5f + az * 0.8660254f <= k;
        }

        private static bool NearAnyVertex(BoardTopology t, Vector3 p, float r)
        {
            int v = t.NearestVertex(p.x, p.z);
            var d = BoardSpace.Vertex(t, v) - p; d.y = 0;
            if (d.magnitude < r) return true;
            int e = BoardSpace.NearestEdge(p, r * 0.6f);
            return e >= 0;
        }

        private static void BuildOutskirts(Transform root, Material mat)
        {
            var mb = new MeshBuilder();
            var rnd = new PropRandom(4242);
            float br = BoardSpace.BoardRadius;
            // Scattered trees just outside the board.
            for (int i = 0; i < 140; i++)
            {
                float a = rnd.Range(0, Mathf.PI * 2f), r = rnd.Range(br + 8f, 180f);
                Tree(mb, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), rnd);
            }
            // Distant soft hills.
            for (int i = 0; i < 26; i++)
            {
                float a = i * Mathf.PI * 2f / 26f + rnd.Range(-0.1f, 0.1f), r = rnd.Range(175f, 195f);
                var p = new Vector3(Mathf.Cos(a) * r, -1f, Mathf.Sin(a) * r);
                float rad = rnd.Range(30f, 55f), hgt = rnd.Range(14f, 34f);
                mb.Cylinder(p, rad, rad * 0.35f, hgt, 10, new Color(0.30f, 0.44f, 0.30f) * rnd.Range(0.85f, 1.1f), true);
            }
            mb.ToObject("Outskirts", root, mat);
        }

        private static void BuildBounds(Transform root)
        {
            var b = new GameObject("WorldBounds").transform;
            b.SetParent(root, false);
            float r = BoardSpace.BoardRadius + 6f;
            int n = 24;
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

        /// <summary>Sky dome, fog, ambient and sun tuning.</summary>
        public static void BuildSky(Transform parent, Light sun, Camera cam)
        {
            var horizon = new Color(0.78f, 0.84f, 0.88f);
            var zenith = new Color(0.33f, 0.55f, 0.85f);
            var mb = new MeshBuilder();
            int seg = 32, rings = 20; float R = 850f;
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
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 380f;
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

    /// <summary>Keeps the sky dome centred on whichever camera renders.</summary>
    public sealed class SkyFollow : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (cam == null) return;
            var p = cam.transform.position; p.y = 0;
            transform.position = p;
        }
    }
}
