using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The pieces on the god board (GB-2): a settlement or city per owned corner and a road per owned hex edge, in each
    /// people's colour (from <see cref="WorldState.Peoples"/>), reconciled from <see cref="TableState"/>. Also the legal-spot
    /// highlights and the adviser marks. Pieces are drawn larger than life so they read from the whole-island view.
    /// </summary>
    public sealed class GodBoardPieces : MonoBehaviour
    {
        public const float HouseWidth = 22f, RoadWidth = 9f;

        private WorldState _s;
        private GameObject[] _corner, _road;
        private int[] _cornerSig, _roadSig;
        private Transform _pieces, _marks;
        private Material _pieceMat, _spotMat, _adviseMat, _baseMat;
        private string _markSig = "";
        private float _clock;

        public int SettlementCount { get; private set; }
        public int CityCount { get; private set; }
        public int RoadCount { get; private set; }
        public int MarkCount => _marks != null ? _marks.childCount : 0;

        public static GodBoardPieces Create(Transform parent, WorldState s)
        {
            var go = new GameObject("GodBoardPieces");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<GodBoardPieces>();
            p._s = s;
            var topo = s.Board.Topology;
            p._corner = new GameObject[topo.CornerCount];
            p._cornerSig = new int[topo.CornerCount];
            p._road = new GameObject[topo.HexEdgeCount];
            p._roadSig = new int[topo.HexEdgeCount];
            p._pieces = new GameObject("Pieces").transform; p._pieces.SetParent(go.transform, false);
            p._marks = new GameObject("Marks").transform; p._marks.SetParent(go.transform, false);
            p._pieceMat = MaterialFactory.CreateVertexColored(Color.white, "GodBoardPiece");
            p._baseMat = MaterialFactory.CreateUnlit(new Color(0.08f, 0.07f, 0.06f), "GodBoardPieceBase");
            p._spotMat = MaterialFactory.CreateUnlit(new Color(1f, 0.92f, 0.45f), "GodBoardSpot");
            p._adviseMat = MaterialFactory.CreateUnlit(new Color(0.45f, 1f, 0.85f), "GodBoardAdvise");
            go.SetActive(false);
            return p;
        }

        public GameObject CornerPiece(int corner) => corner >= 0 && corner < _corner.Length ? _corner[corner] : null;
        public GameObject RoadPiece(int edge) => edge >= 0 && edge < _road.Length ? _road[edge] : null;

        private Color PeopleColour(int p)
        {
            foreach (var pe in _s.Peoples) if (pe.Id == p) return TempleView.PeopleColour(pe);
            return Color.white;
        }

        /// <summary>Makes the pieces match the table now.</summary>
        public void Sync()
        {
            var t = _s.Table;
            if (t == null || t.CornerOwner == null) return;
            var topo = _s.Board.Topology;
            int sc = 0, cc = 0, rc = 0;
            for (int c = 0; c < _corner.Length; c++)
            {
                int sig = t.CornerOwner[c] < 0 ? 0 : (t.CornerOwner[c] + 1) * 10 + t.CornerLevel[c];
                if (t.CornerOwner[c] >= 0) { if (t.CornerLevel[c] >= 2) cc++; else sc++; }
                if (sig == _cornerSig[c]) continue;
                _cornerSig[c] = sig;
                if (_corner[c] != null) { Destroy(_corner[c]); _corner[c] = null; }
                if (sig != 0) _corner[c] = BuildHouse(BoardSpace.Corner(topo, c), PeopleColour(t.CornerOwner[c]), t.CornerLevel[c] >= 2, c);
            }
            for (int e = 0; e < _road.Length; e++)
            {
                int sig = t.RoadOwner[e] < 0 ? 0 : t.RoadOwner[e] + 1;
                if (t.RoadOwner[e] >= 0) rc++;
                if (sig == _roadSig[e]) continue;
                _roadSig[e] = sig;
                if (_road[e] != null) { Destroy(_road[e]); _road[e] = null; }
                if (sig != 0) _road[e] = BuildRoad(e, PeopleColour(t.RoadOwner[e]));
            }
            SettlementCount = sc; CityCount = cc; RoadCount = rc;
        }

        private GameObject BuildHouse(Vector3 at, Color col, bool city, int corner)
        {
            var mb = new MeshBuilder();
            float w = city ? HouseWidth * 1.35f : HouseWidth;
            mb.Cylinder(new Vector3(0, 0.2f, 0), w * 0.78f, w * 0.74f, 0.6f, 14, new Color(0.1f, 0.09f, 0.08f));
            mb.Box(new Vector3(0, 4f, 0), new Vector3(w * 0.75f, 7f, w * 0.6f), Color.Lerp(col, Color.white, 0.25f));
            mb.Cone(new Vector3(0, 7.5f, 0), w * 0.62f, city ? 7f : 6f, 4, col * 0.7f);
            if (city)
            {
                mb.Box(new Vector3(w * 0.32f, 8f, w * 0.1f), new Vector3(w * 0.28f, 16f, w * 0.28f), Color.Lerp(col, Color.white, 0.4f));
                mb.Cone(new Vector3(w * 0.32f, 16f, w * 0.1f), w * 0.26f, 6f, 4, col * 0.6f);
            }
            var go = mb.ToObject((city ? "City " : "Settlement ") + corner, _pieces, _pieceMat);
            go.transform.position = at + Vector3.up * 0.4f;
            go.layer = 2;
            return go;
        }

        private GameObject BuildRoad(int edge, Color col)
        {
            var topo = _s.Board.Topology;
            BoardSpace.HexEdgeEnds(topo, edge, out var a, out var b);
            var d = b - a; d.y = 0;
            float len = d.magnitude;
            var dir = d / len;
            var mb = new MeshBuilder();
            var perp = new Vector3(dir.z, 0, -dir.x);
            float h0 = len * 0.17f, h1 = len * 0.83f;
            Vector3 P(float along, float side, float y) => dir * along + perp * side + Vector3.up * y;
            mb.Quad(P(h0, -RoadWidth * 0.62f, 0.15f), P(h1, -RoadWidth * 0.62f, 0.15f), P(h1, RoadWidth * 0.62f, 0.15f), P(h0, RoadWidth * 0.62f, 0.15f), new Color(0.08f, 0.07f, 0.06f));
            mb.Quad(P(h0, -RoadWidth * 0.62f, 0.15f), P(h0, RoadWidth * 0.62f, 0.15f), P(h1, RoadWidth * 0.62f, 0.15f), P(h1, -RoadWidth * 0.62f, 0.15f), new Color(0.08f, 0.07f, 0.06f));
            mb.Box(P(len * 0.5f, 0, 1.2f), new Vector3(RoadWidth, 2.2f, h1 - h0), col, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            var go = mb.ToObject("Road " + edge, _pieces, _pieceMat);
            go.transform.position = a + Vector3.up * 0.3f;
            go.layer = 2;
            return go;
        }

        /// <summary>
        /// Shows the legal spots (gold) and the adviser's marks (green): corners as discs, edges as bars. Rebuilt only when
        /// the sets change.
        /// </summary>
        public void ShowMarks(IList<int> corners, IList<int> edges, IList<int> adviseCorners, IList<int> adviseEdges)
        {
            string sig = string.Join(",", corners) + "|" + string.Join(",", edges) + "|" + string.Join(",", adviseCorners) + "|" + string.Join(",", adviseEdges);
            if (sig == _markSig) return;
            _markSig = sig;
            for (int i = _marks.childCount - 1; i >= 0; i--) Destroy(_marks.GetChild(i).gameObject);
            var topo = _s.Board.Topology;
            foreach (int c in corners) Disc(BoardSpace.Corner(topo, c), 11f, _spotMat, "Spot " + c);
            foreach (int e in edges) Bar(e, 11f, _spotMat, "Spot edge " + e);
            foreach (int c in adviseCorners) Ring(BoardSpace.Corner(topo, c), 19f, _adviseMat, "Advise " + c);
            foreach (int e in adviseEdges) Bar(e, 15f, _adviseMat, "Advise edge " + e);
        }

        private void Disc(Vector3 at, float r, Material m, string name)
        {
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, r, r, 0.4f, 14, Color.white);
            var go = mb.ToObject(name, _marks, m);
            go.transform.position = at + Vector3.up * 14f; go.layer = 2;
        }

        private void Ring(Vector3 at, float r, Material m, string name)
        {
            var mb = new MeshBuilder();
            const int N = 20;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                mb.Quad(o0 * (r - 4f), o0 * r, o1 * r, o1 * (r - 4f), Color.white);
                mb.Quad(o0 * (r - 4f), o1 * (r - 4f), o1 * r, o0 * r, Color.white);
            }
            var go = mb.ToObject(name, _marks, m);
            go.transform.position = at + Vector3.up * 15f; go.layer = 2;
        }

        private void Bar(int edge, float width, Material m, string name)
        {
            BoardSpace.HexEdgeEnds(_s.Board.Topology, edge, out var a, out var b);
            var d = b - a; d.y = 0; float len = d.magnitude; var dir = d / len;
            var mb = new MeshBuilder();
            mb.Box(Vector3.zero, new Vector3(width, 0.6f, len * 0.62f), Color.white, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            var go = mb.ToObject(name, _marks, m);
            go.transform.position = (a + b) * 0.5f + Vector3.up * 14f; go.layer = 2;
        }

        private void Update()
        {
            _clock += Time.unscaledDeltaTime;
            float pulse = 0.7f + 0.3f * Mathf.Sin(_clock * 5f);
            if (_spotMat != null) _spotMat.color = new Color(1f * pulse, 0.92f * pulse, 0.45f * pulse);
        }

        private void OnDestroy()
        {
            foreach (var m in new[] { _pieceMat, _spotMat, _adviseMat, _baseMat }) if (m != null) Destroy(m);
        }
    }
}
