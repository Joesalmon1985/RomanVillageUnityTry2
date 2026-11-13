using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Converts board ids to Unity world positions. Board metres map 1:1 to Unity (X east, Z north). The walkable
    /// ground is flat at <see cref="GroundY"/>.
    /// Two levels (see Docs/CONTRACTS.md, Board): FLAGS ("vertex", 570, 20 m apart; ids below CornerCount are the
    /// 150 hex corners) and road SEGMENTS ("edge", 630, 20 m); coarse HEX EDGES (210, 60 m, 3 segments each) join corners.
    /// </summary>
    public static class BoardSpace
    {
        /// <summary>Height of the walkable terrain top.</summary>
        public const float GroundY = 0f;

        public static BoardTopology Topo
        {
            get
            {
                var b = WorldBootstrap.Instance;
                return b != null && b.Sim != null ? b.Sim.State.Board.Topology : BoardTopology.Get(BoardGenerator.MvpRadius);
            }
        }

        /// <summary>Hex edge length = hex circumradius (60 m).</summary>
        public static float HexSize => (float)BoardTopology.HexEdgeLength;
        /// <summary>Road segment length = flag spacing (20 m).</summary>
        public static float SegmentLength => (float)BoardTopology.EdgeLength;

        /// <summary>World position of a flag (any vertex id, corner or intermediate).</summary>
        public static Vector3 Vertex(int v) => Vertex(Topo, v);
        public static Vector3 Vertex(BoardTopology t, int v) => new Vector3((float)t.VertexX(v), GroundY, (float)t.VertexZ(v));

        public static Vector3 Hex(int h) => Hex(Topo, h);
        public static Vector3 Hex(BoardTopology t, int h) => new Vector3((float)t.HexX(h), GroundY, (float)t.HexZ(h));

        /// <summary>Midpoint of a road segment (fine edge).</summary>
        public static Vector3 EdgeMid(int e) => EdgeMid(Topo, e);
        public static Vector3 EdgeMid(BoardTopology t, int e) => new Vector3((float)t.EdgeMidX(e), GroundY, (float)t.EdgeMidZ(e));

        /// <summary>Midpoint of a coarse hex edge (60 m, between two corners).</summary>
        public static Vector3 HexEdgeMid(BoardTopology t, int hexEdge) => new Vector3((float)t.HexEdgeMidX(hexEdge), GroundY, (float)t.HexEdgeMidZ(hexEdge));

        public static void EdgeEnds(int e, out Vector3 a, out Vector3 b) => EdgeEnds(Topo, e, out a, out b);
        public static void EdgeEnds(BoardTopology t, int e, out Vector3 a, out Vector3 b)
        {
            a = Vertex(t, t.EdgeVertexA(e));
            b = Vertex(t, t.EdgeVertexB(e));
        }

        public static void HexEdgeEnds(BoardTopology t, int hexEdge, out Vector3 a, out Vector3 b)
        {
            a = Vertex(t, t.HexEdgeCornerA(hexEdge));
            b = Vertex(t, t.HexEdgeCornerB(hexEdge));
        }

        /// <summary>Yaw in degrees (Unity, 0 = +Z north, clockwise) of the segment from A to B.</summary>
        public static float EdgeYaw(int e)
        {
            EdgeEnds(e, out var a, out var b);
            return YawOf(b - a);
        }

        /// <summary>
        /// Corner k (0..5) of hex h in world space, exact from <see cref="BoardTopology.HexCorners"/>; scale &lt; 1
        /// moves the point towards the hex centre (0 = centre, 1 = corner).
        /// </summary>
        public static Vector3 HexCorner(BoardTopology t, int h, int k, float scale = 1f)
        {
            var c = Hex(t, h);
            var p = Vertex(t, t.HexCorners(h)[k]);
            return Vector3.LerpUnclamped(c, p, scale);
        }

        /// <summary>
        /// Below this search radius the picking helpers look only at the hex under the point (its 18 flags, 6 corners or
        /// 6 sides): any flag or segment that close to a point inside a hex lies on that hex's boundary. Used by the God
        /// View tools every frame, so they never scan all 570 flags or 630 segments.
        /// </summary>
        public const float LocalSearchRadius = 15f;

        /// <summary>Nearest flag (any vertex) to a world point, or -1 if farther than maxDistance.</summary>
        public static int NearestVertex(Vector3 p, float maxDistance = float.MaxValue)
        {
            var t = Topo;
            if (maxDistance <= LocalSearchRadius)
            {
                int h = t.HexAt(p.x, p.z);
                if (h < 0) return Flat(p) > BoardRadius + maxDistance ? -1 : NearestOnRim(t, p, maxDistance, false);
                return NearestOf(t, t.HexFlags(h), p, maxDistance);
            }
            int v = t.NearestVertex(p.x, p.z);
            if (v < 0) return -1;
            return Flat(Vertex(t, v) - p) <= maxDistance ? v : -1;
        }

        /// <summary>Nearest hex corner to a world point, or -1 if farther than maxDistance.</summary>
        public static int NearestCorner(Vector3 p, float maxDistance = float.MaxValue)
        {
            var t = Topo;
            if (maxDistance <= LocalSearchRadius)
            {
                int h = t.HexAt(p.x, p.z);
                if (h < 0) return Flat(p) > BoardRadius + maxDistance ? -1 : NearestOnRim(t, p, maxDistance, true);
                return NearestOf(t, t.HexCorners(h), p, maxDistance);
            }
            int v = t.NearestCorner(p.x, p.z);
            if (v < 0) return -1;
            return Flat(Vertex(t, v) - p) <= maxDistance ? v : -1;
        }

        private static int NearestOf(BoardTopology t, System.Collections.Generic.IReadOnlyList<int> ids, Vector3 p, float maxDistance)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < ids.Count; i++)
            {
                int v = ids[i];
                float d = Flat(Vertex(t, v) - p);
                if (d < bestD || (d == bestD && v < best)) { bestD = d; best = v; }
            }
            return bestD <= maxDistance ? best : -1;
        }

        /// <summary>Just outside the board: the hex is found a little towards the centre.</summary>
        private static int NearestOnRim(BoardTopology t, Vector3 p, float maxDistance, bool corners)
        {
            var q = Flat(p) > 0 ? p - new Vector3(p.x, 0, p.z).normalized * maxDistance : p;
            int h = t.HexAt(q.x, q.z);
            if (h < 0) return -1;
            return corners ? NearestOf(t, t.HexCorners(h), p, maxDistance) : NearestOf(t, t.HexFlags(h), p, maxDistance);
        }

        /// <summary>Coarse hex edge nearest to a world point (distance to the 60 m line), or -1 if farther than maxDistance.</summary>
        public static int NearestHexEdge(Vector3 p, float maxDistance = float.MaxValue) => NearestHexEdge(Topo, p, maxDistance, out _);

        public static int NearestHexEdge(BoardTopology t, Vector3 p, float maxDistance, out float distance)
        {
            int best = -1; float bestD = maxDistance;
            if (maxDistance <= LocalSearchRadius)
            {
                int h = t.HexAt(p.x, p.z);
                if (h < 0 && Flat(p) <= BoardRadius + maxDistance)
                {
                    var q = p - new Vector3(p.x, 0, p.z).normalized * maxDistance;
                    h = t.HexAt(q.x, q.z);
                }
                distance = maxDistance;
                if (h < 0) return -1;
                foreach (int he in t.HexSides(h))
                {
                    HexEdgeEnds(t, he, out var a, out var b);
                    float d = DistanceToSegment(p, a, b);
                    if (d < bestD || (d == bestD && he < best)) { bestD = d; best = he; }
                }
                distance = bestD;
                return best;
            }
            for (int he = 0; he < t.HexEdgeCount; he++)
            {
                HexEdgeEnds(t, he, out var a, out var b);
                float d = DistanceToSegment(p, a, b);
                if (d <= bestD) { bestD = d; best = he; }
            }
            distance = bestD;
            return best;
        }

        /// <summary>
        /// Road segment whose line is nearest to a world point, or -1 if farther than maxDistance. Segments lie on
        /// the hex edges, so this finds the nearest hex edge (210) and then the nearest of its 3 segments.
        /// </summary>
        public static int NearestEdge(Vector3 p, float maxDistance = float.MaxValue)
        {
            var t = Topo;
            int he = NearestHexEdge(t, p, maxDistance, out _);
            if (he < 0) return -1;
            int best = -1; float bestD = float.MaxValue;
            foreach (int e in t.HexEdgeSegments(he))
            {
                EdgeEnds(t, e, out var a, out var b);
                float d = DistanceToSegment(p, a, b);
                if (d < bestD) { bestD = d; best = e; }
            }
            return bestD <= maxDistance ? best : -1;
        }

        /// <summary>Ground distance from p to the segment a-b.</summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var q = new Vector2(p.x, p.z); var a2 = new Vector2(a.x, a.z); var ab = new Vector2(b.x, b.z) - a2;
            float s = Mathf.Clamp01(Vector2.Dot(q - a2, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a2 + ab * s - q).magnitude;
        }

        /// <summary>Hex containing a world point, or -1 off the board.</summary>
        public static int HexAt(Vector3 p) => Topo.HexAt(p.x, p.z);

        /// <summary>Yaw in degrees of a horizontal direction (0 = +Z, 90 = +X).</summary>
        public static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        /// <summary>Unit horizontal direction for a yaw in degrees.</summary>
        public static Vector3 DirOf(float yawDeg) => Quaternion.Euler(0, yawDeg, 0) * Vector3.forward;

        public static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        private static float _radius = -1f;
        private static BoardTopology _radiusTopo;

        /// <summary>Radius (m) from the board centre that contains the whole board (outermost corner; about 476 m).</summary>
        public static float BoardRadius
        {
            get
            {
                var t = Topo;
                if (_radiusTopo == t && _radius > 0) return _radius;
                float r = 0;
                foreach (int v in t.Corners) r = Mathf.Max(r, new Vector2((float)t.VertexX(v), (float)t.VertexZ(v)).magnitude);
                _radius = r; _radiusTopo = t;
                return r;
            }
        }

        /// <summary>Radius of the island (board plus a wooded rim and beach); the sea starts beyond it.</summary>
        public static float IslandRadius => BoardRadius + 70f;
    }
}
