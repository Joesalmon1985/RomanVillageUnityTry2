using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Demons;

namespace World.Game
{
    /// <summary>
    /// Demons on a camp corner or a main-road edge (REFACTOR_SPEC 2.C.4, 8.4): one horned figure per demon (up to the
    /// cap of 3) squatting round the site, a scorched ring, a dark red smoke spire seen from afar and the count
    /// floating over them. Appears and disappears with the demons in state.
    /// </summary>
    public sealed class DemonView : MonoBehaviour, IInspectable
    {
        public DemonSiteKind Kind { get; private set; }
        public int SiteId { get; private set; }
        public int CountShown { get; private set; }

        private readonly List<LandFigure> _figures = new List<LandFigure>();
        private TextMesh _count;
        private GameObject _spire;
        private Vector3 _along = Vector3.forward;

        public string Title => "Demons";
        public IEnumerable<string> Lines => LandText.State != null ? LandText.DemonLines(LandText.State, Kind, SiteId) : new string[0];

        public static string Key(DemonSiteKind kind, int id) => (kind == DemonSiteKind.Corner ? "c" : "e") + id;

        public static DemonView Create(Transform parent, WorldState s, DemonSiteKind kind, int id)
        {
            var t = s.Board.Topology;
            var go = new GameObject("Demons " + Key(kind, id));
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<DemonView>();
            v.Kind = kind; v.SiteId = id;
            if (kind == DemonSiteKind.Corner) go.transform.position = BoardSpace.Vertex(t, id);
            else
            {
                BoardSpace.HexEdgeEnds(t, id, out var a, out var b);
                go.transform.position = (a + b) * 0.5f;
                v._along = (b - a).normalized;
            }
            LandLook.Part(go.transform, "Scorch", LandLook.Cached("demon/scorch/" + (int)kind, mb =>
            {
                float r = kind == DemonSiteKind.Corner ? 5.2f : 4.2f;
                LandShapes.Ring(mb, new Vector3(0, 0.09f, 0), r, 0.9f, 28, new Color(0.30f, 0.04f, 0.06f));
                for (int i = 0; i < 6; i++)
                {
                    var p = Quaternion.Euler(0, i * 60f + 15f, 0) * Vector3.forward * r;
                    mb.Cone(p, 0.35f, 0.9f, 4, new Color(0.15f, 0.03f, 0.05f));
                }
            }), LandLook.Unlit, false);
            v._spire = LandLook.Part(go.transform, "Spire", LandLook.Cached("demon/spire", mb =>
            {
                LandShapes.FadeBeam(mb, Vector3.up * 2.5f, 1.0f, 0.3f, 22f, 8, new Color(0.55f, 0.05f, 0.10f));
                LandShapes.FadeBeam(mb, Vector3.up * 2.5f, 0.45f, 0.1f, 30f, 6, new Color(0.85f, 0.15f, 0.12f));
            }), LandLook.Additive, false);
            v._count = Signs.Text(go.transform, "1", new Vector3(0, 4.6f, 0), 1.3f, new Color(1f, 0.35f, 0.25f), 0f);
            v._count.name = "Count";
            LandLook.PickBox(go, Vector3.up * 1.2f, new Vector3(9f, 2.6f, 9f));
            return v;
        }

        public void Sync(int count)
        {
            if (count == CountShown) return;
            CountShown = count;
            _count.text = count.ToString();
            int n = Mathf.Clamp(count, 0, 6);
            while (_figures.Count < n)
            {
                int i = _figures.Count;
                var f = LandFigure.Create(transform, "Demon " + i, -1, LandFigure.Kind.Demon, 1.15f);
                Vector3 p;
                if (Kind == DemonSiteKind.Corner) p = Quaternion.Euler(0, 40f + i * 120f, 0) * Vector3.forward * 3.9f;
                else p = _along * ((i - 1) * 2.6f) + Vector3.Cross(Vector3.up, _along) * (i % 2 == 0 ? 2.3f : -2.3f);
                f.transform.localPosition = p;
                f.transform.localRotation = Quaternion.LookRotation(-p.normalized + new Vector3(0.001f, 0, 0), Vector3.up);
                _figures.Add(f);
            }
            while (_figures.Count > n) { Destroy(_figures[_figures.Count - 1].gameObject); _figures.RemoveAt(_figures.Count - 1); }
        }

        public void Animate(Vector3 eye, float now)
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                var f = _figures[i];
                f.Body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(now * 3f + i * 2f)) * 0.25f, 0);
                f.Body.localRotation = Quaternion.Euler(10f, Mathf.Sin(now * 1.7f + i) * 25f, 0);
            }
            var d = _count.transform.position - eye; d.y = 0;
            if (d.sqrMagnitude > 0.01f) _count.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            _spire.transform.localScale = new Vector3(1f + 0.15f * Mathf.Sin(now * 2.3f), 1f, 1f + 0.15f * Mathf.Sin(now * 2.3f));
        }
    }
}
