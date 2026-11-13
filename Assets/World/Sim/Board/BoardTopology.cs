using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// Immutable topology and geometry of a pointy-top hex board of a given radius, on two levels.
    ///
    /// COARSE level (Catan): hexes, hex CORNERS and HEX EDGES. Cities, the rival outpost, the city
    /// distance rule and the hero live here. A hex edge is <see cref="HexEdgeLength"/> (60 m) long.
    ///
    /// FINE level (Widelands): every hex edge is split into <see cref="SegmentsPerHexEdge"/> (3) road
    /// SEGMENTS of <see cref="EdgeLength"/> (20 m) by two intermediate points. FLAGS are all corners plus
    /// all intermediate points. Buildings stand on flags, roads are placed per segment, carriers, squads
    /// and raids walk segment to segment. All <c>Vertex*</c>/<c>Edge*</c> members (VertexCount,
    /// VertexNeighbours, EdgeVertexA, EdgeBetween, FindPath, Distance, ...) are the FINE graph:
    /// "vertex" = flag, "edge" = segment. Coarse members say Corner/HexEdge in their name.
    ///
    /// Ids are plain ints; -1 = none. Ordering is stable and deterministic:
    ///   hexes by (R, Q);
    ///   flags: corners first, ids 0..CornerCount-1, sorted south to north then west to east (Z, X);
    ///          then intermediate points, id = CornerCount + 2*hexEdge + j (j = 0 nearer the hex edge's
    ///          lower corner, j = 1 nearer its higher corner). So <c>IsCorner(v) == v &lt; CornerCount</c>;
    ///   hex edges by (lower corner id, higher corner id);
    ///   segments: id = 3*hexEdge + k, k = 0, 1, 2 walking from the hex edge's lower corner;
    ///          <see cref="EdgeVertexA"/> is always the lower flag id of a segment.
    /// All adjacency lists are sorted ascending, except the per-hex lists (<see cref="HexCorners"/>,
    /// <see cref="HexSides"/>, <see cref="HexFlags"/>), which are in corner order.
    ///
    /// Geometry: world X = east, world Z = north (Unity XZ ground plane), origin at the centre hex.
    /// Hex circumradius = hex edge length = <see cref="HexEdgeLength"/>. Corner k of a hex is at angle
    /// (60k - 30) degrees from +X towards +Z (k=0 south-east, counter-clockwise). Intermediate flags sit
    /// exactly at 1/3 and 2/3 of their hex edge. Positions are exact, not averaged.
    /// Derived data only: never stored in WorldState, rebuild with <see cref="Get"/>.
    /// </summary>
    public sealed class BoardTopology
    {
        /// <summary>World metres per road SEGMENT (fine edge): the distance between neighbouring flags.</summary>
        public const double EdgeLength = 20.0;

        /// <summary>Road segments per hex edge.</summary>
        public const int SegmentsPerHexEdge = 3;

        /// <summary>World metres per HEX EDGE (also the hex circumradius). The single source of world scale.</summary>
        public const double HexEdgeLength = EdgeLength * SegmentsPerHexEdge;

        public static readonly double Sqrt3 = Math.Sqrt(3.0);

        // Corner lattice: X unit = HexEdgeLength*sqrt(3)/2, Z unit = HexEdgeLength/2.
        // Hex centre (q,r) -> (2q + r, 3r). Corner offsets in corner order:
        private static readonly int[] CornerDx = { 1, 1, 0, -1, -1, 0 };
        private static readonly int[] CornerDz = { -1, 1, 2, 1, -1, -2 };

        public int Radius { get; }
        public int HexCount => _hexes.Length;

        /// <summary>Number of flags (corners + intermediate points).</summary>
        public int VertexCount => _vx.Length;

        /// <summary>Number of road segments.</summary>
        public int EdgeCount => _edgeA.Length;

        /// <summary>Number of hex corners (flags 0..CornerCount-1).</summary>
        public int CornerCount { get; }

        /// <summary>Number of hex edges (coarse edges).</summary>
        public int HexEdgeCount => _hexEdgeA.Length;

        private readonly HexCoord[] _hexes;
        private readonly Dictionary<HexCoord, int> _hexIndex = new Dictionary<HexCoord, int>();
        private readonly int[][] _hexCorners;
        private readonly int[][] _hexSides;
        private readonly int[][] _hexFlags;
        // Flag positions in thirds of the corner lattice (exact integers).
        private readonly int[] _vx, _vz;
        private readonly int[][] _vertexHexes;
        private readonly int[][] _vertexEdges;
        private readonly int[][] _vertexNeighbours;
        private readonly int[][] _cornerNeighbours;
        private readonly int[][] _cornerHexEdges;
        private readonly int[] _corners;
        private readonly int[] _edgeA, _edgeB;
        private readonly int[] _hexEdgeA, _hexEdgeB;
        private readonly Dictionary<long, int> _edgeLookup = new Dictionary<long, int>();
        private readonly Dictionary<long, int> _hexEdgeLookup = new Dictionary<long, int>();
        private readonly int[][] _distCache;
        private readonly int[][] _cornerDistCache;

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

            // ---- Corners (coarse lattice points) ----
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
            CornerCount = keys.Count;
            var cIndex = new Dictionary<(int, int), int>();
            for (int i = 0; i < keys.Count; i++) cIndex.Add(keys[i], i);

            // Hex corners and hex edges.
            _hexCorners = new int[_hexes.Length][];
            var cHexes = new List<int>[CornerCount];
            for (int i = 0; i < CornerCount; i++) cHexes[i] = new List<int>();
            var hexEdgePairs = new SortedSet<(int a, int b)>();
            for (int hi = 0; hi < _hexes.Length; hi++)
            {
                var h = _hexes[hi];
                int cx = 2 * h.Q + h.R, cz = 3 * h.R;
                var corners = new int[6];
                for (int k = 0; k < 6; k++)
                {
                    corners[k] = cIndex[(cz + CornerDz[k], cx + CornerDx[k])];
                    cHexes[corners[k]].Add(hi);
                }
                _hexCorners[hi] = corners;
                for (int k = 0; k < 6; k++)
                {
                    int a = corners[k], b = corners[(k + 1) % 6];
                    hexEdgePairs.Add(a < b ? (a, b) : (b, a));
                }
            }

            int hexEdgeCount = hexEdgePairs.Count;
            _hexEdgeA = new int[hexEdgeCount];
            _hexEdgeB = new int[hexEdgeCount];
            var cNeigh = new List<int>[CornerCount];
            var cEdges = new List<int>[CornerCount];
            for (int i = 0; i < CornerCount; i++)
            {
                cNeigh[i] = new List<int>();
                cEdges[i] = new List<int>();
            }
            int he = 0;
            foreach (var p in hexEdgePairs)
            {
                _hexEdgeA[he] = p.a;
                _hexEdgeB[he] = p.b;
                _hexEdgeLookup.Add(PairKey(p.a, p.b), he);
                cNeigh[p.a].Add(p.b);
                cNeigh[p.b].Add(p.a);
                cEdges[p.a].Add(he);
                cEdges[p.b].Add(he);
                he++;
            }

            // ---- Flags: corners, then two intermediate points per hex edge ----
            int flagCount = CornerCount + 2 * hexEdgeCount;
            _vx = new int[flagCount];
            _vz = new int[flagCount];
            for (int i = 0; i < CornerCount; i++)
            {
                _vz[i] = 3 * keys[i].z;
                _vx[i] = 3 * keys[i].x;
            }
            _corners = new int[CornerCount];
            for (int i = 0; i < CornerCount; i++) _corners[i] = i;

            var vHexes = new List<int>[flagCount];
            for (int i = 0; i < flagCount; i++) vHexes[i] = new List<int>();
            for (int c = 0; c < CornerCount; c++) vHexes[c].AddRange(cHexes[c]);

            int segCount = SegmentsPerHexEdge * hexEdgeCount;
            _edgeA = new int[segCount];
            _edgeB = new int[segCount];
            for (int e = 0; e < hexEdgeCount; e++)
            {
                int a = _hexEdgeA[e], b = _hexEdgeB[e];
                int p0 = CornerCount + 2 * e, p1 = p0 + 1;
                _vx[p0] = _vx[a] + (_vx[b] - _vx[a]) / 3;
                _vz[p0] = _vz[a] + (_vz[b] - _vz[a]) / 3;
                _vx[p1] = _vx[a] + 2 * (_vx[b] - _vx[a]) / 3;
                _vz[p1] = _vz[a] + 2 * (_vz[b] - _vz[a]) / 3;
                // Hexes sharing this hex edge = hexes touching both corners.
                foreach (int h in cHexes[a])
                    if (cHexes[b].Contains(h))
                    {
                        vHexes[p0].Add(h);
                        vHexes[p1].Add(h);
                    }
                int[] chain = { a, p0, p1, b };
                for (int k = 0; k < SegmentsPerHexEdge; k++)
                {
                    int s = SegmentsPerHexEdge * e + k;
                    int u = chain[k], w = chain[k + 1];
                    _edgeA[s] = Math.Min(u, w);
                    _edgeB[s] = Math.Max(u, w);
                    _edgeLookup.Add(PairKey(u, w), s);
                }
            }

            var vEdges = new List<int>[flagCount];
            var vNeigh = new List<int>[flagCount];
            for (int i = 0; i < flagCount; i++)
            {
                vEdges[i] = new List<int>();
                vNeigh[i] = new List<int>();
            }
            for (int s = 0; s < segCount; s++)
            {
                vEdges[_edgeA[s]].Add(s);
                vEdges[_edgeB[s]].Add(s);
                vNeigh[_edgeA[s]].Add(_edgeB[s]);
                vNeigh[_edgeB[s]].Add(_edgeA[s]);
            }

            _vertexHexes = new int[flagCount][];
            _vertexEdges = new int[flagCount][];
            _vertexNeighbours = new int[flagCount][];
            for (int i = 0; i < flagCount; i++)
            {
                vHexes[i].Sort();
                vEdges[i].Sort();
                vNeigh[i].Sort();
                _vertexHexes[i] = vHexes[i].ToArray();
                _vertexEdges[i] = vEdges[i].ToArray();
                _vertexNeighbours[i] = vNeigh[i].ToArray();
            }
            _cornerNeighbours = new int[CornerCount][];
            _cornerHexEdges = new int[CornerCount][];
            for (int i = 0; i < CornerCount; i++)
            {
                cNeigh[i].Sort();
                cEdges[i].Sort();
                _cornerNeighbours[i] = cNeigh[i].ToArray();
                _cornerHexEdges[i] = cEdges[i].ToArray();
            }

            // Per-hex lists in corner order.
            _hexSides = new int[_hexes.Length][];
            _hexFlags = new int[_hexes.Length][];
            for (int hi = 0; hi < _hexes.Length; hi++)
            {
                var c = _hexCorners[hi];
                var sides = new int[6];
                var ring = new int[6 * SegmentsPerHexEdge];
                for (int k = 0; k < 6; k++)
                {
                    int a = c[k], b = c[(k + 1) % 6];
                    int e = _hexEdgeLookup[PairKey(a, b)];
                    sides[k] = e;
                    var mids = HexEdgeIntermediates(e);
                    ring[3 * k] = a;
                    // Walk from corner k towards corner k+1.
                    bool forward = _hexEdgeA[e] == a;
                    ring[3 * k + 1] = forward ? mids[0] : mids[1];
                    ring[3 * k + 2] = forward ? mids[1] : mids[0];
                }
                _hexSides[hi] = sides;
                _hexFlags[hi] = ring;
            }

            _distCache = new int[flagCount][];
            _cornerDistCache = new int[CornerCount][];
        }

        private static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }

        private int[] HexEdgeIntermediates(int hexEdge) => new[] { CornerCount + 2 * hexEdge, CornerCount + 2 * hexEdge + 1 };

        // ---- Hexes ----

        public HexCoord Hex(int hex) => _hexes[hex];

        /// <summary>Hex id for a coordinate, or -1 if off the board.</summary>
        public int HexIndex(HexCoord c) => _hexIndex.TryGetValue(c, out var i) ? i : -1;

        public bool Contains(HexCoord c) => _hexIndex.ContainsKey(c);

        /// <summary>The 6 corner flags of a hex, in corner order (k = 0..5, see class doc).</summary>
        public IReadOnlyList<int> HexCorners(int hex) => _hexCorners[hex];

        /// <summary>The 6 hex edges (coarse) of a hex; side k joins corner k and corner k+1.</summary>
        public IReadOnlyList<int> HexSides(int hex) => _hexSides[hex];

        /// <summary>
        /// The 18 flags on a hex's boundary in ring order: corner k at index 3k, then the two intermediate
        /// flags of side k walking towards corner k+1.
        /// </summary>
        public IReadOnlyList<int> HexFlags(int hex) => _hexFlags[hex];

        // ---- Flags (fine vertices) ----

        /// <summary>True when the flag is a hex corner (ids below <see cref="CornerCount"/>).</summary>
        public bool IsCorner(int v) => v >= 0 && v < CornerCount;

        /// <summary>Flag ids of all corners (0..CornerCount-1), ascending.</summary>
        public IReadOnlyList<int> Corners => _corners;

        /// <summary>
        /// On-board hexes touching a flag, ascending: a corner touches 1 to 3 hexes; an intermediate flag
        /// touches the 1 or 2 hexes sharing its hex edge.
        /// </summary>
        public IReadOnlyList<int> VertexHexes(int v) => _vertexHexes[v];

        /// <summary>Road segments at a flag (corners 2 or 3, intermediate flags 2), ascending.</summary>
        public IReadOnlyList<int> VertexEdges(int v) => _vertexEdges[v];

        /// <summary>Flags one segment away, ascending.</summary>
        public IReadOnlyList<int> VertexNeighbours(int v) => _vertexNeighbours[v];

        /// <summary>Number of segments at a flag.</summary>
        public int Degree(int v) => _vertexEdges[v].Length;

        // ---- Segments (fine edges) ----

        /// <summary>Lower flag id of a segment.</summary>
        public int EdgeVertexA(int edge) => _edgeA[edge];

        /// <summary>Higher flag id of a segment.</summary>
        public int EdgeVertexB(int edge) => _edgeB[edge];

        /// <summary>The flag at the other end of a segment from v.</summary>
        public int OtherEnd(int edge, int v) => _edgeA[edge] == v ? _edgeB[edge] : _edgeA[edge];

        /// <summary>Segment joining two flags, or -1 if they are not adjacent. Symmetric.</summary>
        public int EdgeBetween(int v1, int v2) => _edgeLookup.TryGetValue(PairKey(v1, v2), out var e) ? e : -1;

        // ---- Coarse level: corners and hex edges ----

        /// <summary>Corners one hex edge away from a corner (2 or 3), ascending. Empty for an intermediate flag.</summary>
        public IReadOnlyList<int> CornerNeighbours(int v) => IsCorner(v) ? _cornerNeighbours[v] : Array.Empty<int>();

        /// <summary>Hex edges at a corner (2 or 3), ascending. Empty for an intermediate flag.</summary>
        public IReadOnlyList<int> CornerHexEdges(int v) => IsCorner(v) ? _cornerHexEdges[v] : Array.Empty<int>();

        /// <summary>Lower corner id of a hex edge.</summary>
        public int HexEdgeCornerA(int hexEdge) => _hexEdgeA[hexEdge];

        /// <summary>Higher corner id of a hex edge.</summary>
        public int HexEdgeCornerB(int hexEdge) => _hexEdgeB[hexEdge];

        /// <summary>Hex edge joining two corners, or -1 if they are not one hex edge apart. Symmetric.</summary>
        public int HexEdgeBetween(int c1, int c2) => _hexEdgeLookup.TryGetValue(PairKey(c1, c2), out var e) ? e : -1;

        /// <summary>The 3 segments of a hex edge, walking from its lower corner (ids 3e, 3e+1, 3e+2).</summary>
        public IReadOnlyList<int> HexEdgeSegments(int hexEdge) =>
            new[] { SegmentsPerHexEdge * hexEdge, SegmentsPerHexEdge * hexEdge + 1, SegmentsPerHexEdge * hexEdge + 2 };

        /// <summary>The 4 flags along a hex edge, from its lower corner to its higher corner.</summary>
        public IReadOnlyList<int> HexEdgeFlags(int hexEdge) =>
            new[] { _hexEdgeA[hexEdge], CornerCount + 2 * hexEdge, CornerCount + 2 * hexEdge + 1, _hexEdgeB[hexEdge] };

        /// <summary>The hex edge a segment lies on.</summary>
        public int HexEdgeOfSegment(int segment) => segment / SegmentsPerHexEdge;

        /// <summary>The hex edge an intermediate flag lies on, or -1 for a corner.</summary>
        public int HexEdgeOfFlag(int v) => IsCorner(v) ? -1 : (v - CornerCount) / 2;

        /// <summary>Distance between two corners in hex edges (BFS over corners), or -1 if either is not a corner.</summary>
        public int CornerDistance(int a, int b)
        {
            if (!IsCorner(a) || !IsCorner(b)) return -1;
            return CornerDistances(a)[b];
        }

        /// <summary>
        /// Shortest corner path (hex edge count; ties by ascending corner id), both ends included, or null
        /// when either end is not a corner. Each step is one hex edge (e.g. one hero move).
        /// </summary>
        public int[] FindCornerPath(int from, int to)
        {
            if (!IsCorner(from) || !IsCorner(to)) return null;
            if (from == to) return new[] { from };
            var dist = CornerDistances(to);
            var path = new List<int> { from };
            int cur = from;
            while (cur != to)
            {
                int next = -1;
                foreach (int n in _cornerNeighbours[cur])
                    if (dist[n] == dist[cur] - 1) { next = n; break; }
                if (next < 0) return null;
                path.Add(next);
                cur = next;
            }
            return path.ToArray();
        }

        private int[] CornerDistances(int src)
        {
            lock (_cornerDistCache)
            {
                var d = _cornerDistCache[src];
                if (d != null) return d;
                d = new int[CornerCount];
                for (int i = 0; i < d.Length; i++) d[i] = -1;
                d[src] = 0;
                var q = new Queue<int>();
                q.Enqueue(src);
                while (q.Count > 0)
                {
                    int v = q.Dequeue();
                    foreach (int n in _cornerNeighbours[v])
                    {
                        if (d[n] >= 0) continue;
                        d[n] = d[v] + 1;
                        q.Enqueue(n);
                    }
                }
                _cornerDistCache[src] = d;
                return d;
            }
        }

        // ---- Geometry (derived doubles, world metres) ----

        private const double XUnit = 0.5 / 3.0 * HexEdgeLength; // times sqrt(3)
        private const double ZUnit = 0.5 / 3.0 * HexEdgeLength;

        public double HexX(int hex) => (2 * _hexes[hex].Q + _hexes[hex].R) * Sqrt3 * 0.5 * HexEdgeLength;
        public double HexZ(int hex) => 3 * _hexes[hex].R * 0.5 * HexEdgeLength;
        public double VertexX(int v) => _vx[v] * Sqrt3 * XUnit;
        public double VertexZ(int v) => _vz[v] * ZUnit;
        /// <summary>Midpoint of a segment.</summary>
        public double EdgeMidX(int e) => (VertexX(_edgeA[e]) + VertexX(_edgeB[e])) * 0.5;
        public double EdgeMidZ(int e) => (VertexZ(_edgeA[e]) + VertexZ(_edgeB[e])) * 0.5;
        /// <summary>Midpoint of a hex edge.</summary>
        public double HexEdgeMidX(int hexEdge) => (VertexX(_hexEdgeA[hexEdge]) + VertexX(_hexEdgeB[hexEdge])) * 0.5;
        public double HexEdgeMidZ(int hexEdge) => (VertexZ(_hexEdgeA[hexEdge]) + VertexZ(_hexEdgeB[hexEdge])) * 0.5;

        /// <summary>Nearest flag to a world point (ties: lowest id).</summary>
        public int NearestVertex(double x, double z) => Nearest(x, z, VertexCount);

        /// <summary>Nearest corner to a world point (ties: lowest id).</summary>
        public int NearestCorner(double x, double z) => Nearest(x, z, CornerCount);

        private int Nearest(double x, double z, int count)
        {
            int best = -1;
            double bestD = double.MaxValue;
            for (int v = 0; v < count; v++)
            {
                double dx = VertexX(v) - x, dz = VertexZ(v) - z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        /// <summary>Hex containing a world point, or -1 if off the board.</summary>
        public int HexAt(double x, double z)
        {
            double qf = (Sqrt3 / 3.0 * x - 1.0 / 3.0 * z) / HexEdgeLength;
            double rf = (2.0 / 3.0 * z) / HexEdgeLength;
            double sf = -qf - rf;
            double rq = Math.Round(qf), rr = Math.Round(rf), rs = Math.Round(sf);
            double dq = Math.Abs(rq - qf), dr = Math.Abs(rr - rf), ds = Math.Abs(rs - sf);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return HexIndex(new HexCoord((int)rq, (int)rr));
        }

        // ---- Paths (fine graph) ----

        /// <summary>
        /// Shortest path by segment count (BFS, ties broken by ascending flag id) from one flag to
        /// another, using only segments for which edgeAllowed(segmentId) is true (null = all segments).
        /// Returns the flag sequence including both ends ([from] when from == to), or null if unreachable.
        /// To block a flag (e.g. a demon flag), reject every segment touching it in the filter.
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

        /// <summary>Segments along a flag path from <see cref="FindPath"/>.</summary>
        public int[] PathEdges(int[] vertexPath)
        {
            if (vertexPath == null || vertexPath.Length < 2) return Array.Empty<int>();
            var edges = new int[vertexPath.Length - 1];
            for (int i = 0; i < edges.Length; i++) edges[i] = EdgeBetween(vertexPath[i], vertexPath[i + 1]);
            return edges;
        }

        /// <summary>
        /// Segment-count distance between flags, or -1 if unreachable. Without a filter the answer comes
        /// from a cached all-segments BFS table (cheap to call repeatedly).
        /// </summary>
        public int Distance(int from, int to, Func<int, bool> edgeAllowed = null)
        {
            if (edgeAllowed == null) return Distances(from)[to];
            var p = FindPath(from, to, edgeAllowed);
            return p == null ? -1 : p.Length - 1;
        }

        /// <summary>Unfiltered segment distances from one flag to every flag (cached; do not modify).</summary>
        public IReadOnlyList<int> DistancesFrom(int from) => Distances(from);

        private int[] Distances(int src)
        {
            lock (_distCache)
            {
                var d = _distCache[src];
                if (d != null) return d;
                d = new int[VertexCount];
                for (int i = 0; i < d.Length; i++) d[i] = -1;
                d[src] = 0;
                var q = new Queue<int>();
                q.Enqueue(src);
                while (q.Count > 0)
                {
                    int v = q.Dequeue();
                    foreach (int n in _vertexNeighbours[v])
                    {
                        if (d[n] >= 0) continue;
                        d[n] = d[v] + 1;
                        q.Enqueue(n);
                    }
                }
                _distCache[src] = d;
                return d;
            }
        }
    }
}
