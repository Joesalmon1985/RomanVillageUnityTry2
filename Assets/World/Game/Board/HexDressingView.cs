using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>
    /// A standalone dressed land built from any <see cref="Board"/> (god board plan AB-2..AB-5): flat tiles tinted by
    /// terrain, the pip-level dressing of <see cref="HexDressing"/>, an island ground, the sea and the ports of
    /// <see cref="PortMarkers"/>. It never touches the live terrain, walk grid or state; the F1 "Land" tool and the
    /// capture script use it to check any seed. Also owns <see cref="Pulse"/> (AB-3).
    /// </summary>
    public sealed class HexDressingView : MonoBehaviour
    {
        public static HexDressingView Instance { get; private set; }

        public Board Board { get; private set; }
        public int[] ItemCounts { get; private set; }
        private readonly Dictionary<int, GameObject> _props = new Dictionary<int, GameObject>();

        /// <summary>Builds the land under a new root object. Destroys a previous one.</summary>
        public static HexDressingView Create(Board board, Transform parent)
        {
            if (Instance != null) DestroyObj(Instance.gameObject);
            var go = new GameObject("HexDressingPreview");
            if (parent != null) go.transform.SetParent(parent, false);
            var v = go.AddComponent<HexDressingView>();
            v.Build(board);
            Instance = v;
            return v;
        }

        private static void DestroyObj(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build(Board board)
        {
            Board = board;
            var t = board.Topology;
            var mat = MaterialFactory.CreateVertexColored(Color.white, "Dressing");
            ItemCounts = new int[t.HexCount];
            float R = BoardSpace.HexSize;

            // Island ground and sea.
            var ground = new MeshBuilder();
            float ir = BoardSpace.IslandRadius;
            var seaCol = new Color(0.20f, 0.42f, 0.62f);
            TerrainBuilder.Disc(ground, new Vector3(0, -2.5f, 0), ir * 2.2f, 48, seaCol);
            TerrainBuilder.Disc(ground, new Vector3(0, -0.3f, 0), ir, 64, new Color(0.82f, 0.76f, 0.56f));
            TerrainBuilder.Disc(ground, new Vector3(0, -0.2f, 0), ir - 16f, 64, new Color(0.36f, 0.52f, 0.26f));
            ground.ToObject("Ground", transform, mat);

            var centres = new List<Vector3>();
            for (int h = 0; h < t.HexCount; h++) centres.Add(BoardSpace.Hex(t, h));
            var corners = new List<Vector3>();
            foreach (int v in t.Corners) corners.Add(BoardSpace.Vertex(t, v));

            for (int h = 0; h < t.HexCount; h++)
            {
                var terrain = board.HexTerrain[h];
                var centre = centres[h];
                var tile = new MeshBuilder();
                var col = TerrainBuilder.TerrainColor(terrain);
                var ring = new List<Vector3>();
                foreach (int v in t.HexFlags(h)) ring.Add(BoardSpace.Vertex(t, v) + Vector3.up * 0.02f);
                ring.Sort((a, b) => Mathf.Atan2(a.z - centre.z, a.x - centre.x).CompareTo(Mathf.Atan2(b.z - centre.z, b.x - centre.x)));
                tile.FanUp(centre + Vector3.up * 0.02f, ring, col);
                for (int he = 0; he < t.HexSides(h).Count; he++)
                {
                    BoardSpace.HexEdgeEnds(t, t.HexSides(h)[he], out var a, out var b);
                    tile.Strip(a + Vector3.up * 0.06f, b + Vector3.up * 0.06f, 0.6f, new Color(0.80f, 0.75f, 0.58f));
                }
                var tgo = tile.ToObject("Hex " + h + " " + terrain, transform, mat);

                var props = new MeshBuilder();
                int hh = h;
                HexDressing.PlaceOk ok = (p, edge, clear) =>
                {
                    if (TerrainBuilder.EdgeMargin(p - centre, R) < edge) return false;
                    var d = p - centre; d.y = 0;
                    if (d.magnitude < HexDressing.CentreClear + clear) return false;
                    foreach (var c in corners)
                    {
                        float dx = p.x - c.x, dz = p.z - c.z, rr = 8f + clear;
                        if (dx * dx + dz * dz < rr * rr) return false;
                    }
                    return true;
                };
                ItemCounts[h] = HexDressing.Fill(props, board, h, ok, true);
                if (props.VertexCount > 0) _props[h] = props.ToObject("Dressing " + h, tgo.transform, mat);
            }
            if (board.Ports != null && board.Ports.Count > 0) PortMarkers.Build(board, transform, mat);
        }

        // ---------------- AB-3: pulse ----------------

        private sealed class Pulsing { public float T; public GameObject Ring; public Transform Lift; }
        private static readonly Dictionary<int, Pulsing> Active = new Dictionary<int, Pulsing>();
        private static GameObject _pulseRoot;
        private static Mesh _ringMesh;
        private static Material _ringMat;
        public const float PulseSeconds = 1.6f;

        /// <summary>
        /// Briefly highlights producing hexes: a gold ring swells over each hex and its dressing hops. Works on the
        /// preview land when one exists and otherwise on the live terrain's props. The turn engine calls this with the
        /// hexes that produced on a roll. Safe to call with hex ids out of range (ignored).
        /// </summary>
        public static void Pulse(IEnumerable<int> hexes)
        {
            if (hexes == null) return;
            EnsurePulseRoot();
            foreach (int h in hexes)
            {
                var topo = BoardSpace.Topo;
                if (h < 0 || h >= topo.HexCount) continue;
                if (!Active.TryGetValue(h, out var p))
                {
                    p = new Pulsing();
                    p.Ring = new GameObject("PulseRing " + h);
                    p.Ring.transform.SetParent(_pulseRoot.transform, false);
                    p.Ring.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
                    p.Ring.AddComponent<MeshRenderer>().sharedMaterial = _ringMat;
                    p.Ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    p.Ring.transform.position = BoardSpace.Hex(topo, h) + Vector3.up * 0.4f;
                    Active[h] = p;
                }
                p.T = PulseSeconds;
                p.Ring.SetActive(true);
                p.Lift = FindProps(h);
            }
        }

        /// <summary>Hex ids that are pulsing now (for tests and the demo).</summary>
        public static IReadOnlyCollection<int> PulsingNow
        {
            get { var l = new List<int>(); foreach (var kv in Active) if (kv.Value.T > 0f) l.Add(kv.Key); return l; }
        }

        private static Transform FindProps(int h)
        {
            if (Instance != null && Instance._props.TryGetValue(h, out var go) && go != null) return go.transform;
            var live = TerrainBuilder.HexProps(h);
            return live != null ? live.transform : null;
        }

        private static void EnsurePulseRoot()
        {
            if (_pulseRoot != null) return;
            _pulseRoot = new GameObject("HexPulse");
            _pulseRoot.AddComponent<PulseDriver>();
            var mb = new MeshBuilder();
            float R = BoardSpace.HexSize * 0.8f;
            for (int k = 0; k < 6; k++)
            {
                float a0 = k * 60f * Mathf.Deg2Rad, a1 = (k + 1) * 60f * Mathf.Deg2Rad;
                var o0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var o1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                mb.Quad(o0 * (R - 5f), o1 * (R - 5f), o1 * R, o0 * R, Color.white);
                mb.Quad(o0 * 8.5f, o1 * 8.5f, o1 * (R - 5f), o0 * (R - 5f), new Color(1f, 1f, 1f) * 0.25f);
            }
            _ringMesh = mb.ToMesh("PulseRing");
            _ringMat = MaterialFactory.CreateGlow(Color.white, "HexPulse");
        }

        private sealed class PulseDriver : MonoBehaviour
        {
            private MaterialPropertyBlock _mpb;
            private void Update()
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                foreach (var kv in Active)
                {
                    var p = kv.Value;
                    if (p.Ring == null || !p.Ring.activeSelf) continue;
                    p.T -= Time.unscaledDeltaTime;
                    float k = 1f - Mathf.Clamp01(p.T / PulseSeconds);
                    if (p.T <= 0f)
                    {
                        p.Ring.SetActive(false);
                        if (p.Lift != null) p.Lift.localPosition = Vector3.zero;
                        continue;
                    }
                    float s = Mathf.Lerp(0.7f, 1.08f, Mathf.Sqrt(k));
                    p.Ring.transform.localScale = new Vector3(s, 1f, s);
                    float fade = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                    _mpb.SetColor("_Color", new Color(1.0f, 0.82f, 0.30f) * (0.9f * fade));
                    p.Ring.GetComponent<MeshRenderer>().SetPropertyBlock(_mpb);
                    if (p.Lift != null) p.Lift.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * 1.6f * (1f - k), 0);
                }
            }
        }
    }
}
