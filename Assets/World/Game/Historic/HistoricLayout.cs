using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>Fixed view positions inside one hex for the Historic age (G4-3): building spots, tree slots, field plots, rock and clay heaps.</summary>
    public sealed class HexSpots
    {
        public Vector3 Centre;
        public Vector3[] Build = new Vector3[3];
        public float[] BuildYaw = new float[3];
        public Vector3[] Trees;
        public Vector3[] Fields;
        public Vector3 Rock, Clay;
    }

    /// <summary>Deterministic slot positions per hex (view only: the sim has slots, not places). Cached per director.</summary>
    public sealed class HistoricLayout
    {
        public const int MaxTrees = 12, MaxFields = 8;
        private readonly Board _board;
        private readonly Dictionary<int, HexSpots> _cache = new Dictionary<int, HexSpots>();

        public HistoricLayout(Board board) { _board = board; }

        public HexSpots Of(int hex)
        {
            if (_cache.TryGetValue(hex, out var s)) return s;
            s = Make(hex);
            _cache[hex] = s;
            return s;
        }

        private HexSpots Make(int hex)
        {
            var t = _board.Topology;
            var c = BoardSpace.Hex(t, hex);
            float R = BoardSpace.HexSize;
            var rnd = new PropRandom(51000 + hex * 7919);
            var corners = new List<Vector3>();
            foreach (int v in t.HexFlags(hex)) corners.Add(BoardSpace.Vertex(t, v));
            var sp = new HexSpots { Centre = c };
            float a0 = rnd.Range(0f, 360f);
            var placed = new List<Vector3>();
            for (int k = 0; k < 3; k++)
            {
                sp.Build[k] = c + BoardSpace.DirOf(a0 + k * 120f) * 27f;
                sp.BuildYaw[k] = BoardSpace.YawOf(c - sp.Build[k]);
                placed.Add(sp.Build[k]);
            }
            sp.Rock = c + BoardSpace.DirOf(a0 + 60f) * 30f; placed.Add(sp.Rock);
            sp.Clay = c + BoardSpace.DirOf(a0 + 180f) * 30f; placed.Add(sp.Clay);

            bool Ok(Vector3 p, float edge, float apart)
            {
                var d = p - c; d.y = 0;
                if (TerrainBuilder.EdgeMargin(d, R) < edge || d.magnitude < 11f) return false;
                foreach (var q in corners) { float dx = p.x - q.x, dz = p.z - q.z; if (dx * dx + dz * dz < 81f) return false; }
                foreach (var q in placed) { float dx = p.x - q.x, dz = p.z - q.z; if (dx * dx + dz * dz < apart * apart) return false; }
                return true;
            }
            Vector3[] Scatter(int n, float edge, float apart, float fromBuild)
            {
                var list = new List<Vector3>();
                for (int i = 0; i < n * 60 && list.Count < n; i++)
                {
                    var p = c + new Vector3(rnd.Range(-R, R), 0, rnd.Range(-R, R));
                    if (!Ok(p, edge, fromBuild)) continue;
                    bool far = true;
                    foreach (var q in list) { float dx = p.x - q.x, dz = p.z - q.z; if (dx * dx + dz * dz < apart * apart) { far = false; break; } }
                    if (far) list.Add(p);
                }
                while (list.Count < n) list.Add(c + BoardSpace.DirOf(rnd.Range(0f, 360f)) * rnd.Range(14f, 40f));
                return list.ToArray();
            }
            sp.Fields = Scatter(MaxFields, 6f, 12f, 10f);
            foreach (var f in sp.Fields) placed.Add(f);
            sp.Trees = Scatter(MaxTrees, 5f, 6.5f, 7f);
            return sp;
        }
    }
}
