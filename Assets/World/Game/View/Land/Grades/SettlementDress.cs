using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Dresses one <see cref="SettlementView"/> from state. Town: a taller totem with a crown of banners, a stone
    /// footing and rail under the palisade, stone gate posts at its six gaps, a ring of meeting seats round the hearth
    /// and extra torches at the gates. Open outer plots (ids 1350 and up, shown as faint rings by the base view) get a
    /// dotted way from the town edge; locked outer plots show nothing. A captured camp (the owner changed) gets the
    /// new owner's totem, ring stones and torches without rebuilding the view.
    /// </summary>
    public sealed class SettlementDress : MonoBehaviour
    {
        public bool TownShown { get; private set; }
        public int OwnerShown { get; private set; } = -1;
        public int OuterOpenShown { get; private set; }
        public int OuterWaysShown { get; private set; }
        public int TownTorches => _torches.Count;
        public bool OwnTotemShown => _totem != null && _totem.activeSelf;

        private SettlementView _view;
        private GameObject _totem, _extras, _ways;
        private readonly List<FireLight> _torches = new List<FireLight>();
        private string _waySig;
        private bool _pickMade;
        private Vector3[] _spokes = new Vector3[0];

        public void Apply(WorldState s, SettlementView v, Settlement st)
        {
            _view = v;
            if (_spokes.Length == 0 && st.State == SettlementState.Active) ComputeSpokes(s, st);
            if (!_pickMade)
            {
                var old = GetComponent<BoxCollider>();
                var pick = new GameObject("GradePick");
                pick.transform.SetParent(transform, false);
                if (old != null) { old.enabled = false; LandLook.PickBox(pick, old.center, old.size); }
                else LandLook.PickBox(pick, Vector3.up * 1.5f, new Vector3(8f, 3f, 8f));
                pick.AddComponent<SettlementInspect>().View = v;
                _pickMade = true;
            }
            if (st.State != SettlementState.Active) { Hide(); return; }
            int owner = st.Faction;
            bool town = st.IsTown;

            // Owner colours: the base view keeps the colours it was created with.
            var ring = transform.Find("CampRing");
            if (ring != null && owner != OwnerShown)
                ring.GetComponent<MeshFilter>().sharedMesh = LandLook.Cached("camp/ring/" + owner, mb => CampRing(mb, owner));

            // Totem: the base one while it is right; ours for a town or a new owner (never while the totem falls).
            var baseTotem = _view.Totem;
            bool needOwn = !_view.ShownOverrun && (town || owner != _view.Faction);
            if (needOwn)
            {
                if (_totem == null || OwnerShown != owner || TownShown != town)
                {
                    if (_totem != null) Destroy(_totem);
                    _totem = LandLook.Part(transform, "GradeTotem", LandLook.Cached("gtotem/" + owner + "/" + (town ? "town" : "city"), mb => TownTotem(mb, owner, town)));
                    if (baseTotem != null) _totem.transform.localPosition = baseTotem.localPosition;
                }
                _totem.SetActive(true);
                if (baseTotem != null) baseTotem.gameObject.SetActive(false);
            }
            else
            {
                if (_totem != null) _totem.SetActive(false);
                if (baseTotem != null) baseTotem.gameObject.SetActive(true);
            }

            if (town != TownShown || owner != OwnerShown || _extras == null && town)
            {
                if (_extras != null) { Destroy(_extras); _extras = null; }
                foreach (var t in _torches) if (t != null) Destroy(t.transform.parent.gameObject);
                _torches.Clear();
                if (town)
                {
                    _extras = LandLook.Part(transform, "TownDress", LandLook.Cached("gtown/" + owner + "/" + SpokeSig(), mb => TownMesh(mb, owner)));
                    foreach (var sp in _spokes)
                    {
                        var side = new Vector3(sp.z, 0, -sp.x);
                        foreach (float sgn in new[] { -1f, 1f })
                        {
                            var pos = transform.position + sp * 7.1f + side * (2.6f * sgn);
                            pos.y = BoardSpace.GroundY;
                            _torches.Add(WorldLights.Torch(transform, pos, owner == PlayerIds.Rival, false, "Town torch"));
                        }
                    }
                }
            }
            TownShown = town; OwnerShown = owner;
            SyncOuter(s, st, town);
        }

        private void Hide()
        {
            if (_totem != null) _totem.SetActive(false);
            if (_extras != null) _extras.SetActive(false);
            if (_ways != null) _ways.SetActive(false);
            OuterOpenShown = 0;
        }

        private void ComputeSpokes(WorldState s, Settlement st)
        {
            var t = s.Board.Topology;
            var sp = new List<Vector3>();
            for (int k = 0; k < 3; k++)
            {
                int h = PlotLayout.SectorHex(t, st.Corner, k);
                if (h < 0) continue;
                var d = BoardSpace.Hex(t, h) - BoardSpace.Vertex(t, st.Corner); d.y = 0;
                sp.Add(d.normalized);
            }
            _spokes = sp.ToArray();
        }

        private string SpokeSig() { var sb = new System.Text.StringBuilder(); foreach (var v in _spokes) sb.Append(Mathf.RoundToInt(BoardSpace.YawOf(v))).Append(','); return sb.ToString(); }

        // ---- outer plots ----

        private void SyncOuter(WorldState s, Settlement st, bool town)
        {
            var open = new List<PlotView>();
            if (town)
                foreach (int p in EconomyApi.PlotsOf(s, st.Id))
                    if (PlotLayoutOuter.IsOuter(p)) { var pv = EconomyApi.Plot(s, p); if (pv.Use == PlotUse.Free) open.Add(pv); }
            OuterOpenShown = open.Count;
            var sb = new System.Text.StringBuilder();
            foreach (var pv in open) sb.Append(pv.Plot).Append(',');
            string sig = sb.ToString();
            if (sig == _waySig) return;
            _waySig = sig;
            OuterWaysShown = open.Count;
            var mb = new MeshBuilder();
            var t = s.Board.Topology;
            var origin = transform.position;
            foreach (var pv in open)
            {
                var pts = PlotLayout.PathPoints(t, pv.Plot);
                for (int k = 0; k + 3 < pts.Length; k += 2)
                {
                    var a = LandLook.Ground(pts[k], pts[k + 1]) - origin; var b = LandLook.Ground(pts[k + 2], pts[k + 3]) - origin;
                    float len = (b - a).magnitude; int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.6f));
                    for (int i = 0; i < n; i++)
                    {
                        var p = Vector3.Lerp(a, b, (i + 0.5f) / n);
                        mb.Box(p + Vector3.up * 0.04f, new Vector3(0.45f, 0.08f, 0.3f), new Color(0.58f, 0.52f, 0.42f), BoardSpace.YawOf(b - a) + 15f * (i % 3));
                    }
                }
                // A short stake with a cord marker at the plot's edge facing the town.
                var c = LandLook.Ground(pv.X, pv.Z) - origin;
                var toward = -c; toward.y = 0; toward.Normalize();
                var q = c + toward * 4.3f;
                LandShapes.Beam(mb, q, q + Vector3.up * 0.9f, 0.045f, 4, LandLook.DarkWood);
                mb.Box(q + Vector3.up * 0.85f, new Vector3(0.3f, 0.2f, 0.05f), LandLook.PeopleTrim(st.Faction) * 0.85f, BoardSpace.YawOf(toward));
            }
            LandLook.SetMesh(ref _ways, transform, "OuterWays", mb, null, false);
        }

        // ---- shapes ----

        public static void CampRing(MeshBuilder mb, int faction)
        {
            var pc = LandLook.People(faction);
            for (int i = 0; i < 24; i++)
            {
                var p = Quaternion.Euler(0, i * 15f, 0) * Vector3.forward * 3.3f;
                mb.Box(p + Vector3.up * 0.09f, new Vector3(0.45f, 0.18f, 0.35f), i % 2 == 0 ? pc : LandLook.Stone, i * 15f);
            }
        }

        static void TownTotem(MeshBuilder mb, int faction, bool town)
        {
            SettlementView.TotemShape(mb, faction, true);
            if (!town) return;
            var pc = LandLook.People(faction); var tc = LandLook.PeopleTrim(faction);
            // A stone base, a crossbar with two hanging banners and a gold crown disc: taller and wider than a city's.
            mb.Cylinder(Vector3.zero, 0.75f, 0.55f, 0.5f, 8, LandLook.Stone);
            LandShapes.Beam(mb, Vector3.up * 9.2f, Vector3.up * 11.0f, 0.12f, 6, LandLook.Wood);
            mb.Box(new Vector3(0, 9.2f, 0), new Vector3(3.2f, 0.16f, 0.16f), LandLook.DarkWood);
            foreach (float sx in new[] { -1.3f, 1.3f })
            {
                mb.Quad(new Vector3(sx - 0.45f, 9.15f, 0), new Vector3(sx + 0.45f, 9.15f, 0), new Vector3(sx + 0.45f, 7.7f, 0), new Vector3(sx - 0.45f, 7.7f, 0), pc);
                mb.Quad(new Vector3(sx - 0.45f, 7.7f, 0), new Vector3(sx + 0.45f, 7.7f, 0), new Vector3(sx + 0.45f, 9.15f, 0), new Vector3(sx - 0.45f, 9.15f, 0), pc * 0.85f);
                mb.Box(new Vector3(sx, 8.6f, 0.02f), new Vector3(0.4f, 0.4f, 0.04f), tc);
            }
            mb.Cylinder(new Vector3(0, 10.5f, -0.05f), 0.55f, 0.55f, 0.08f, 12, tc);
            mb.Cone(new Vector3(0, 11.0f, 0), 0.22f, 0.5f, 6, tc);
        }

        /// <summary>Stone footing and rail under the palisade, gate posts at its six gaps, meeting seats round the hearth.</summary>
        void TownMesh(MeshBuilder mb, int faction)
        {
            var tc = LandLook.PeopleTrim(faction);
            int total = 48;
            Vector3 prev = default; bool havePrev = false;
            for (int i = 0; i <= total; i++)
            {
                float a = (i % total) * 360f / total;
                bool gap = false;
                foreach (var sp in _spokes)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(a, BoardSpace.YawOf(sp)));
                    if (Mathf.Abs(Mathf.DeltaAngle(d, Mathf.Round(d / 60f) * 60f)) < 9f) gap = true;
                }
                if (gap) { havePrev = false; continue; }
                var p = BoardSpace.DirOf(a) * 8f;
                mb.Box(p + Vector3.up * 0.25f, new Vector3(0.62f, 0.5f, 0.62f), LandLook.Stone * (0.88f + 0.06f * (i % 3)), a);
                if (havePrev) LandShapes.Beam(mb, prev + Vector3.up * 1.2f, p + Vector3.up * 1.2f, 0.06f, 4, LandLook.DarkWood, false);
                prev = p; havePrev = true;
            }
            // Gate posts: a stone pillar either side of each gap and a lintel with a people-colour pennon.
            foreach (var sp in _spokes)
                for (int k = 0; k < 6; k++)
                {
                    float centre = BoardSpace.YawOf(sp) + k * 60f;
                    var l = BoardSpace.DirOf(centre - 9.5f) * 8f; var r = BoardSpace.DirOf(centre + 9.5f) * 8f;
                    mb.Cylinder(l, 0.4f, 0.32f, 2.9f, 6, LandLook.Stone); mb.Cylinder(r, 0.4f, 0.32f, 2.9f, 6, LandLook.Stone);
                    LandShapes.Beam(mb, l + Vector3.up * 2.8f, r + Vector3.up * 2.8f, 0.09f, 5, LandLook.DarkWood);
                    var m = (l + r) / 2f + Vector3.up * 2.8f;
                    mb.Tri(m, m + Vector3.up * -0.5f + (r - l).normalized * 0.3f, m + Vector3.up * -0.25f - (r - l).normalized * 0.3f, tc);
                }
            // Meeting seats: a low ring of stone benches between the hearth and the storehouse piles.
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + 15f;
                var p = BoardSpace.DirOf(a) * 1.42f;
                mb.Box(p + Vector3.up * 0.14f, new Vector3(0.5f, 0.28f, 0.28f), LandLook.Stone * 1.05f, a + 90f);
                if (i % 3 == 0) mb.Box(p + Vector3.up * 0.3f, new Vector3(0.52f, 0.05f, 0.3f), tc, a + 90f);
            }
        }

        public string Describe() => "gradecamp " + _view.SettlementId + " owner " + OwnerShown + " town " + TownShown + " outer " + OuterOpenShown + " torches " + _torches.Count + " totem " + OwnTotemShown;
    }
}
