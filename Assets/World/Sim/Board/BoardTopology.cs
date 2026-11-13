using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// Immutable topology and geometry of a pointy-top hex board of a given radius.
    /// Ids are plain ints: hex 0..HexCount-1, vertex 0..VertexCount-1, edge 0..EdgeCount-1; -1 = none.
    /// Ordering is stable and deterministic:
    ///   hexes by (R, Q); vertices by (Z, X) (south to north, then west to east);
    ///   edges by (lower vertex id, higher vertex id). All adjacency lists are sorted ascending,
    ///   except <see cref="HexVertices"/>, which is in corner order.
    /// Geometry: world X = east, world Z = north (Unity XZ ground plane), origin at the centre hex,
    /// edge length <see cref="EdgeLength"/> metres. Corner k of a hex is at angle (60k - 30) degrees
    /// from +X towards +Z (k=0 south-east, counter-clockwise). Positions are exact, not averaged.
    /// Derived data only: never stored in WorldState, rebuild with <see cref="Get"/>.
    /// </summary>
    public sealed class BoardTopology
    {
        /// <summary>World metres per hex edge (also hex circumradius). The single source of world scale.</summary>
        public const double EdgeLength = 20.0;

        public static readonly double Sqrt3 = Math.Sqrt(3.0);

        // Vertex lattice: X unit = EdgeLength*sqrt(3)/2, Z unit = EdgeLength/2.
        // Hex centre (q,r) -> (2q + r, 3r). Corner offsets in corner order:
        private static readonly int[] CornerDx = { 1, 1, 0, -1, -1, 0 };
        private static readonly int[] CornerDz = { -1, 1, 2, 1, -1, -2 };

        public int Radius { get; }
        public int HexCount => _hexes.Length;
        public int VertexCount => _vx.Length;
        public int EdgeCount => _edgeA.Length;

        private readonly HexCoord[] _hexes;
        private readonly Dictionary<HexCoord, int> _hexIndex = new Dictionary<HexCoord, int>();
        private readonly int[][] _hexVertices;
        private readonly int[][] _hexEdges;
        private readonly int[] _vx, _vz;
        private readonly int[][] _vertexHexes;
        private readonly int[][] _vertexEdges;
        private readonly int[][] _vertexNeighbours;
        private readonly int[] _edgeA, _edgeB;
        private readonly Dictionary<long, int> _edgeLookup = new Dictionary<long, int>();

        private static readonly Dictionary<int, BoardTopology> Cache = new Dictionary<int, BoardTopology>();

        /// <summary>Shared cached topology for a radius (thread-safe).</summary>
        public static BoardTopology Get(int radius)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(radius, out var t))
                {
                    t = new BoardTopology(radius);
                    Cache.Add(radius, t);
                }
                return t;
            }
        }

        public BoardTopology(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            Radius = radius;

            var hexes = new List<HexCoord>();
            for (int r = -radius; r <= radius; r++)
                for (int q = -radius; q <= radius; q++)
                    if (Math.Abs(q + r) <= radius) hexes.Add(new HexCoord(q, r));
            _hexes = hexes.ToArray();
            for (int i = 0; i < _hexes.Length; i++) _hexIndex.Add(_hexes[i], i);

            // Collect unique vertex lattice points.
            var keys = new List<(int z, int x)>();
            var seen = new HashSet<(int, int)>();
            foreach (var h in _hexes)
            {
                int cx = 2 * h.Q + h.R, cz = 3 * h.R;
                for (int k = 0; k < 6; k++)
                {
                    var key = (cz + CornerDz[k], cx + CornerDx[k]);
                    if (seen.Add(key)) keys.Add(key);
                }
            }
            keys.Sort((a, b) => a.z != b.z ? a.z.CompareTo(b.z) : a.x.CompareTo(b.x));
            _vx = new int[keys.Count];
            _vz = new int[keys.Count];
            var vIndex = new Dictionary<(int, int), int>();
            for (int i = 0; i < keys.Count; i++)
            {
                _vz[i] = keys[i].z;
                _vx[i] = keys[i].x;
                vIndex.Add(keys[i], i);
            }

            // Hex corners and edges.
            _hexVertices = new int[_hexes.Length][];
            var vHexes = new List<int>[keys.Count];
            for (int i = 0; i < keys.Count; i++) vHexes[i] = new List<int>();
            var edgePairs = new SortedSet<(int a, int b)>();
            for (int hi = 0; hi < _hexes.Length; hi++)
            {
                var h = _hexes[hi];
                int cx = 2 * h.Q + h.R, cz = 3 * h.R;
                var corners = new int[6];
                for (int k = 0; k < 6; k++)
                {
                    corners[k] = vIndex[(cz + CornerDz[k], cx + CornerDx[k])];
                    vHexes[corners[k]].Add(hi);
                }
                _hexVertices[hi] = corners;
                for (int k = 0; k < 6; k++)
                {
                    int a = corners[k], b = corners[(k + 1) % 6];
                    edgePairs.Add(a < b ? (a, b) : (b, a));
                }
            }

            _edgeA = new int[edgePairs.Count];
            _edgeB = new int[edgePairs.Count];
            var vEdges = new List<int>[keys.Count];
            var vNeigh = new List<int>[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                vEdges[i] = new List<int>();
                vNeigh[i] = new List<int>();
            }
            int e = 0;
            foreach (var p in edgePairs)
            {
                _edgeA[e] = p.a;
                _edgeB[e] = p.b;
                _edgeLookup.Add(PairKey(p.a, p.b), e);
                vEdges[p.a].Add(e);
                vEdges[p.b].Add(e);
                vNeigh[p.a].Add(p.b);
                vNeigh[p.b].Add(p.a);
                e++;
            }

            _vertexHexes = new int[keys.Count][];
            _vertexEdges = new int[keys.Count][];
            _vertexNeighbours = new int[keys.Count][];
            for (int i = 0; i < keys.Count; i++)
            {
                vHexes[i].Sort();
                vEdges[i].Sort();
                vNeigh[i].Sort();
                _vertexHexes[i] = vHexes[i].ToArray();
                _vertexEdges[i] = vEdges[i].ToArray();
                _vertexNeighbours[i] = vNeigh[i].ToArray();
            }

            _hexEdges = new int[_hexes.Length][];
            for (int hi = 0; hi < _hexes.Length; hi++)
            {
                var c = _hexVertices[hi];
                var list = new int[6];
                for (int k = 0; k < 6; k++) list[k] = EdgeBetween(c[k], c[(k + 1) % 6]);
                _hexEdges[hi] = list;
            }
        }

        private static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        // ---- Hexes ----

        public HexCoord Hex(int hex) => _hexes[hex];

        /// <summary>Hex id for a coordinate, or -1 if off the board.</summary>
        public int HexIndex(HexCoord c) => _hexIndex.TryGetValue(c, out var i) ? i : -1;

        public bool Contains(HexCoord c) => _hexIndex.ContainsKey(c);

        /// <summary>The 6 corner vertices of a hex, in corner order (k = 0..5, see class doc).</summary>
        public IReadOnlyList<int> HexVertices(int hex) => _hexVertices[hex];

        /// <summary>The 6 edges of a hex; edge k joins corner k and corner k+1.</summary>
        public IReadOnlyList<int> HexEdges(int hex) => _hexEdges[hex];

        // ---- Vertices ----

        /// <summary>On-board hexes touching a vertex (1 to 3), ascending.</summary>
        public IReadOnlyList<int> VertexHexes(int v) => _vertexHexes[v];

        /// <summary>Edges at a vertex (degree 2 or 3), ascending.</summary>
        public IReadOnlyList<int> VertexEdges(int v) => _vertexEdges[v];

        /// <summary>Vertices one edge away, ascending.</summary>
        public IReadOnlyList<int> VertexNeighbours(int v) => _vertexNeighbours[v];

        public int Degree(int v) => _vertexEdges[v].Length;

        // ---- Edges ----

        public int EdgeVertexA(int edge) => _edgeA[edge];
        public int EdgeVertexB(int edge) => _edgeB[edge];

        /// <summary>The vertex at the other end of an edge from v.</summary>
        public int OtherEnd(int edge, int v) => _edgeA[edge] == v ? _edgeB[edge] : _edgeA[edge];

        /// <summary>Edge joining two vertices, or -1 if they are not adjacent. Symmetric.</summary>
        public int EdgeBetween(int v1, int v2) => _edgeLookup.TryGetValue(PairKey(v1, v2), out var e) ? e : -1;

        // ---- Geometry (derived doubles, world metres) ----

        public double HexX(int hex) => (2 * _hexes[hex].Q + _hexes[hex].R) * Sqrt3 * 0.5 * EdgeLength;
        public double HexZ(int hex) => 3 * _hexes[hex].R * 0.5 * EdgeLength;
        public double VertexX(int v) => _vx[v] * Sqrt3 * 0.5 * EdgeLength;
        public double VertexZ(int v) => _vz[v] * 0.5 * EdgeLength;
        public double EdgeMidX(int e) => (VertexX(_edgeA[e]) + VertexX(_edgeB[e])) * 0.5;
        public double EdgeMidZ(int e) => (VertexZ(_edgeA[e]) + VertexZ(_edgeB[e])) * 0.5;

        /// <summary>Nearest vertex to a world point (ties: lowest id).</summary>
        public int NearestVertex(double x, double z)
        {
            int best = -1;
            double bestD = double.MaxValue;
            for (int v = 0; v < _vx.Length; v++)
            {
                double dx = VertexX(v) - x, dz = VertexZ(v) - z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        /// <summary>Hex containing a world point, or -1 if off the board.</summary>
        public int HexAt(double x, double z)
        {
            double qf = (Sqrt3 / 3.0 * x - 1.0 / 3.0 * z) / EdgeLength;
            double rf = (2.0 / 3.0 * z) / EdgeLength;
            double sf = -qf - rf;
            double rq = Math.Round(qf), rr = Math.Round(rf), rs = Math.Round(sf);
            double dq = Math.Abs(rq - qf), dr = Math.Abs(rr - rf), ds = Math.Abs(rs - sf);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return HexIndex(new HexCoord((int)rq, (int)rr));
        }

        // ---- Paths ----

        /// <summary>
        /// Shortest path by edge count (BFS, ties broken by ascending vertex id) from one vertex to
        /// another, using only edges for which edgeAllowed(edgeId) is true (null = all edges).
        /// Returns the vertex sequence including both ends ([from] when from == to), or null if unreachable.
        /// To block a vertex (e.g. a demon flag), reject every edge touching it in the filter.
        /// </summary>
        public int[] FindPath(int from, int to, Func<int, bool> edgeAllowed = null)
        {
            if (from == to) return new[] { from };
            var prev = new int[VertexCount];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            prev[from] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                foreach (int n in _vertexNeighbours[v])
                {
                    if (prev[n] != -2) continue;
                    if (edgeAllowed != null && !edgeAllowed(EdgeBetween(v, n))) continue;
                    prev[n] = v;
                    if (n == to)
                    {
                        var path = new List<int>();
                        for (int c = to; c != -1; c = prev[c]) path.Add(c);
                        path.Reverse();
                        return path.ToArray();
                    }
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        /// <summary>Edges along a vertex path from <see cref="FindPath"/>.</summary>
        public int[] PathEdges(int[] vertexPath)
        {
            if (vertexPath == null || vertexPath.Length < 2) return Array.Empty<int>();
            var edges = new int[vertexPath.Length - 1];
            for (int i = 0; i < edges.Length; i++) edges[i] = EdgeBetween(vertexPath[i], vertexPath[i + 1]);
            return edges;
        }

        /// <summary>Edge-count distance, or -1 if unreachable.</summary>
        public int Distance(int from, int to, Func<int, bool> edgeAllowed = null)
        {
            var p = FindPath(from, to, edgeAllowed);
            return p == null ? -1 : p.Length - 1;
        }
    }
}
