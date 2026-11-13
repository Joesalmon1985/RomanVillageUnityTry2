using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Ground highlights for God View tools: a glowing disc per flag or a glowing strip per road segment, coloured
    /// per item (green valid, red invalid, yellow hovered, orange existing). Sparse: callers pass only the ids to show
    /// (e.g. the flags in territory), pieces are created lazily per id and pooled; everything else is hidden. Callers
    /// should rebuild their id lists only when the state changes (see BuildTool / RoadTool), not every frame.
    /// </summary>
    public static class GodOverlay
    {
        public static readonly Color Valid = new Color(0.20f, 0.85f, 0.25f, 1f);
        public static readonly Color Invalid = new Color(0.90f, 0.16f, 0.12f, 1f);
        public static readonly Color Hover = new Color(1f, 0.85f, 0.15f, 1f);
        public static readonly Color Existing = new Color(1f, 0.50f, 0.10f, 1f);

        public struct Item
        {
            public int Id; public Color Color; public float Size;
            public Item(int id, Color c, float size) { Id = id; Color = c; Size = size; }
        }

        private static Transform _root;
        private static readonly Dictionary<int, GameObject> _vertex = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, GameObject> _edge = new Dictionary<int, GameObject>();
        private static readonly List<GameObject> _shown = new List<GameObject>();
        private static readonly Dictionary<Color32, Material> _mats = new Dictionary<Color32, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _root = null; _vertex.Clear(); _edge.Clear(); _shown.Clear(); _mats.Clear(); _spots.Clear(); _bars.Clear(); }

        private static Transform Root
        {
            get
            {
                if (_root != null) return _root;
                _vertex.Clear(); _edge.Clear(); _shown.Clear(); _spots.Clear(); _bars.Clear();
                _root = new GameObject("GodToolOverlay").transform;
                if (WorldBootstrap.Instance != null) _root.SetParent(WorldBootstrap.Instance.transform, false);
                return _root;
            }
        }

        private static Material Mat(Color c)
        {
            Color32 k = c;
            if (!_mats.TryGetValue(k, out var m) || m == null) { m = MaterialFactory.CreateUnlit(c, "Overlay"); _mats[k] = m; }
            return m;
        }

        private static void HideShown()
        {
            foreach (var g in _shown) if (g != null) g.SetActive(false);
            _shown.Clear();
        }

        /// <summary>Shows a disc at each listed flag (Size = radius in metres); hides every other overlay piece.</summary>
        public static void ShowVertices(IList<Item> items)
        {
            var root = Root;
            root.gameObject.SetActive(true);
            HideShown();
            foreach (var it in items)
            {
                if (!_vertex.TryGetValue(it.Id, out var g) || g == null)
                {
                    g = Signs.Prim(PrimitiveType.Cylinder, root, "V" + it.Id, BoardSpace.Vertex(it.Id) + Vector3.up * 0.2f, Vector3.one, Mat(Valid));
                    _vertex[it.Id] = g;
                }
                g.SetActive(true);
                g.transform.localScale = new Vector3(it.Size * 2f, 0.04f, it.Size * 2f);
                g.GetComponent<Renderer>().sharedMaterial = Mat(it.Color);
                _shown.Add(g);
            }
        }

        /// <summary>Shows a strip along each listed road segment (Size = width in metres); hides every other piece.</summary>
        public static void ShowEdges(IList<Item> items)
        {
            var root = Root;
            root.gameObject.SetActive(true);
            HideShown();
            foreach (var it in items)
            {
                if (!_edge.TryGetValue(it.Id, out var g) || g == null)
                {
                    BoardSpace.EdgeEnds(it.Id, out var a, out var b);
                    var d = b - a; d.y = 0;
                    g = Signs.Prim(PrimitiveType.Cube, root, "E" + it.Id, (a + b) * 0.5f + Vector3.up * 0.22f, Vector3.one, Mat(Valid), new Vector3(0, BoardSpace.YawOf(d), 0));
                    g.transform.localScale = new Vector3(1f, 0.04f, d.magnitude - 3f);
                    _edge[it.Id] = g;
                }
                g.SetActive(true);
                var s = g.transform.localScale;
                g.transform.localScale = new Vector3(it.Size, s.y, s.z);
                g.GetComponent<Renderer>().sharedMaterial = Mat(it.Color);
                _shown.Add(g);
            }
        }

        /// <summary>A disc at a world point (plots, corners, demon sites); Key identifies the pooled piece.</summary>
        public struct Spot
        {
            public int Key; public Vector3 Pos; public Color Color; public float Size;
            public Spot(int key, Vector3 pos, Color c, float size) { Key = key; Pos = pos; Color = c; Size = size; }
        }

        /// <summary>A flat bar between two world points (hex edges); Key identifies the pooled piece.</summary>
        public struct Bar
        {
            public int Key; public Vector3 A, B; public Color Color; public float Width;
            public Bar(int key, Vector3 a, Vector3 b, Color c, float width) { Key = key; A = a; B = b; Color = c; Width = width; }
        }

        private static readonly Dictionary<int, GameObject> _spots = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, GameObject> _bars = new Dictionary<int, GameObject>();

        /// <summary>Shows discs and bars (either list may be null); hides every other overlay piece.</summary>
        public static void Show(IList<Spot> spots, IList<Bar> bars)
        {
            var root = Root;
            if (_spots.Count > 0 && FirstOrNull(_spots) == null) { _spots.Clear(); _bars.Clear(); }
            root.gameObject.SetActive(true);
            HideShown();
            if (spots != null)
                foreach (var it in spots)
                {
                    if (!_spots.TryGetValue(it.Key, out var g) || g == null)
                    {
                        g = Signs.Prim(PrimitiveType.Cylinder, root, "S" + it.Key, it.Pos, Vector3.one, Mat(Valid));
                        Object.Destroy(g.GetComponent<Collider>());
                        _spots[it.Key] = g;
                    }
                    g.SetActive(true);
                    g.transform.position = new Vector3(it.Pos.x, PlayerWalker.GroundHeight(it.Pos) + 0.35f, it.Pos.z);
                    g.transform.localScale = new Vector3(it.Size * 2f, 0.05f, it.Size * 2f);
                    g.GetComponent<Renderer>().sharedMaterial = Mat(it.Color);
                    _shown.Add(g);
                }
            if (bars != null)
                foreach (var it in bars)
                {
                    if (!_bars.TryGetValue(it.Key, out var g) || g == null)
                    {
                        g = Signs.Prim(PrimitiveType.Cube, root, "B" + it.Key, Vector3.zero, Vector3.one, Mat(Valid));
                        Object.Destroy(g.GetComponent<Collider>());
                        _bars[it.Key] = g;
                    }
                    var d = it.B - it.A; d.y = 0;
                    g.SetActive(true);
                    g.transform.SetPositionAndRotation((it.A + it.B) * 0.5f + Vector3.up * 0.27f, Quaternion.Euler(0, BoardSpace.YawOf(d), 0));
                    g.transform.localScale = new Vector3(it.Width, 0.05f, Mathf.Max(0.5f, d.magnitude - 6f));
                    g.GetComponent<Renderer>().sharedMaterial = Mat(it.Color);
                    _shown.Add(g);
                }
        }

        private static GameObject FirstOrNull(Dictionary<int, GameObject> d) { foreach (var kv in d) return kv.Value; return null; }

        public static void Hide()
        {
            HideShown();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>Number of overlay pieces showing in a colour (tests).</summary>
        public static int VisibleCount(Color c)
        {
            if (_root == null || !_root.gameObject.activeSelf) return 0;
            int n = 0; Color32 k = c;
            foreach (var g in _shown) if (g != null && g.activeSelf && ((Color32)g.GetComponent<Renderer>().sharedMaterial.color).Equals(k)) n++;
            return n;
        }

        /// <summary>Total overlay pieces showing (tests: the overlay stays sparse).</summary>
        public static int VisibleTotal => _root == null || !_root.gameObject.activeSelf ? 0 : _shown.Count;
    }
}
