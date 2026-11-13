using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Converts board ids (vertex, edge, hex) to Unity world positions. Board metres map 1:1 to Unity
    /// (X east, Z north). The walkable ground is flat at <see cref="GroundY"/>.
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

        public static Vector3 Vertex(int v) => Vertex(Topo, v);
        public static Vector3 Vertex(BoardTopology t, int v) => new Vector3((float)t.VertexX(v), GroundY, (float)t.VertexZ(v));

        public static Vector3 Hex(int h) => Hex(Topo, h);
        public static Vector3 Hex(BoardTopology t, int h) => new Vector3((float)t.HexX(h), GroundY, (float)t.HexZ(h));

        public static Vector3 EdgeMid(int e) => EdgeMid(Topo, e);
        public static Vector3 EdgeMid(BoardTopology t, int e) => new Vector3((float)t.EdgeMidX(e), GroundY, (float)t.EdgeMidZ(e));

        public static void EdgeEnds(int e, out Vector3 a, out Vector3 b)
        {
            var t = Topo;
            a = Vertex(t, t.EdgeVertexA(e));
            b = Vertex(t, t.EdgeVertexB(e));
        }

        /// <summary>Yaw in degrees (Unity, 0 = +Z north, clockwise) of the edge from A to B.</summary>
        public static float EdgeYaw(int e)
        {
            EdgeEnds(e, out var a, out var b);
            return YawOf(b - a);
        }

        /// <summary>Hex corner k (0..5) in world space; corner k at angle 60k-30 degrees (pointy-top).</summary>
        public static Vector3 HexCorner(BoardTopology t, int h, int k, float scale = 1f)
        {
            float ang = (60f * k - 30f) * Mathf.Deg2Rad;
            float r = (float)BoardTopology.EdgeLength * scale;
            return Hex(t, h) + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
        }

        /// <summary>Nearest vertex to a world point, or -1 if farther than maxDistance.</summary>
        public static int NearestVertex(Vector3 p, float maxDistance = float.MaxValue)
        {
            var t = Topo;
            int v = t.NearestVertex(p.x, p.z);
            if (v < 0) return -1;
            var d = Vertex(t, v) - p; d.y = 0;
            return d.magnitude <= maxDistance ? v : -1;
        }

        /// <summary>Edge whose segment is nearest to a world point, or -1 if farther than maxDistance.</summary>
        public static int NearestEdge(Vector3 p, float maxDistance = float.MaxValue)
        {
            var t = Topo;
            int best = -1; float bestD = maxDistance;
            var q = new Vector2(p.x, p.z);
            for (int e = 0; e < t.EdgeCount; e++)
            {
                var a = Vertex(t, t.EdgeVertexA(e)); var b = Vertex(t, t.EdgeVertexB(e));
                var a2 = new Vector2(a.x, a.z); var b2 = new Vector2(b.x, b.z);
                var ab = b2 - a2;
                float s = Mathf.Clamp01(Vector2.Dot(q - a2, ab) / ab.sqrMagnitude);
                float d = (a2 + ab * s - q).magnitude;
                if (d <= bestD) { bestD = d; best = e; }
            }
            return best;
        }

        /// <summary>Hex containing a world point, or -1 off the board.</summary>
        public static int HexAt(Vector3 p) => Topo.HexAt(p.x, p.z);

        /// <summary>Yaw in degrees of a horizontal direction (0 = +Z, 90 = +X).</summary>
        public static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        /// <summary>Unit horizontal direction for a yaw in degrees.</summary>
        public static Vector3 DirOf(float yawDeg) => Quaternion.Euler(0, yawDeg, 0) * Vector3.forward;

        /// <summary>Radius (m) from the board centre that contains the whole board.</summary>
        public static float BoardRadius
        {
            get
            {
                var t = Topo; float r = 0;
                for (int v = 0; v < t.VertexCount; v++) r = Mathf.Max(r, new Vector2((float)t.VertexX(v), (float)t.VertexZ(v)).magnitude);
                return r;
            }
        }
    }
}
