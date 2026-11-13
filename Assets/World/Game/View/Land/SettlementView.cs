using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// A camp at its corner (REFACTOR_SPEC 8.4): the hearth and the storehouse piles (one block per unit of each ware,
    /// up to 12 a pile) inside the 3 m storehouse radius, the people's totem, the faint stone rings of its empty plots,
    /// the local paths from the storehouse to every building, the city palisade and its upgrade site, a camp site with
    /// its goods, and the stopped look while demons hold it (fire out, dark smoke and ground). Reconciled from state.
    /// </summary>
    public sealed class SettlementView : MonoBehaviour, IInspectable
    {
        public const float StoreRadius = 2.2f;

        public int SettlementId { get; private set; }
        public int Faction { get; private set; }
        public int Corner { get; private set; }
        public SettlementState ShownState { get; private set; } = (SettlementState)(-1);
        public bool ShownCity { get; private set; }
        public bool ShownHeld { get; private set; }
        public bool ShownOverrun { get; private set; }
        public int FreePlotRings { get; private set; }
        public int PathCount { get; private set; }
        public int StockBlocks { get; private set; }
        public SiteParts Site { get; private set; }
        public SiteParts UpgradeSite { get; private set; }
        public Transform Totem => _totem != null ? _totem.transform : null;

        private GameObject _hearth, _fire, _smoke, _store, _totem, _palisade, _plots, _paths, _held, _siteRing, _burn;
        private string _stockSig, _plotSig;
        private int _palisadeSig = -1;
        private Vector3[] _spokes = new Vector3[0];

        public string Title => LandText.State != null ? LandText.StoreTitle(LandText.State, SettlementId) : "Camp";
        public IEnumerable<string> Lines => LandText.State != null ? LandText.StoreLines(LandText.State, SettlementId) : new string[0];

        public static SettlementView Create(Transform parent, WorldState s, Settlement st)
        {
            var t = s.Board.Topology;
            var go = new GameObject("Camp " + st.Id + " (" + (st.Faction == PlayerIds.Rival ? "Ash Clan" : "yours") + ")");
            go.transform.SetParent(parent, false);
            go.transform.position = BoardSpace.Vertex(t, st.Corner);
            var v = go.AddComponent<SettlementView>();
            v.SettlementId = st.Id;
            v.Faction = st.Faction;
            v.Corner = st.Corner;
            var sp = new List<Vector3>();
            for (int k = 0; k < 3; k++)
            {
                int h = PlotLayout.SectorHex(t, st.Corner, k);
                if (h < 0) continue;
                var d = BoardSpace.Hex(t, h) - go.transform.position; d.y = 0;
                sp.Add(d.normalized);
            }
            v._spokes = sp.ToArray();
            v.Site = new SiteParts(go.transform);
            v.UpgradeSite = new SiteParts(go.transform);
            LandLook.PickBox(go, Vector3.up * 1.5f, new Vector3(5.5f, 3f, 5.5f));
            return v;
        }

        public void Sync(WorldState s, Settlement st, bool overrun)
        {
            var tu = s.Economy.Tuning;
            bool site = st.State == SettlementState.Site;
            if (st.State != ShownState)
            {
                ShownState = st.State;
                if (!site) { Site.Clear(); if (_siteRing != null) { Destroy(_siteRing); _siteRing = null; } }
                if (site && _siteRing == null)
                {
                    var mb = new MeshBuilder();
                    for (int i = 0; i < 12; i++)
                    {
                        var p = Quaternion.Euler(0, i * 30f, 0) * Vector3.forward * 3.2f;
                        LandShapes.Beam(mb, p, p + Vector3.up * 0.9f, 0.06f, 4, LandLook.DarkWood);
                        var q = Quaternion.Euler(0, (i + 1) * 30f, 0) * Vector3.forward * 3.2f;
                        LandShapes.Beam(mb, p + Vector3.up * 0.75f, q + Vector3.up * 0.75f, 0.02f, 3, LandLook.Thatch, false);
                    }
                    LandShapes.Pennant(mb, new Vector3(0, 0, 0), 3.0f, Faction);
                    _siteRing = LandLook.Part(transform, "CampSiteRing", mb.ToMesh("camp site"));
                }
                if (!site) BuildCamp();
            }
            if (site)
            {
                var mid = SideDir();
                var along = Vector3.Cross(Vector3.up, mid);
                Site.Sync(tu.SettlementCost, st.Delivered, st.BuildProgress / (float)Mathf.Max(1, tu.SettlementBuildTicks), st.BuildProgress > 0,
                    mid * 1.0f - along * 0.8f, along, mid * 3.6f + along * 1.2f, Faction);
                return;
            }

            // Storehouse piles.
            var sb = new System.Text.StringBuilder();
            for (int w = 0; w < Wares.Count; w++) sb.Append(Mathf.Min(st.Stock[w], 13)).Append(',');
            string ssig = sb.ToString();
            if (ssig != _stockSig)
            {
                _stockSig = ssig;
                var mb = new MeshBuilder();
                StockBlocks = 0;
                for (int w = 0; w < Wares.Count; w++)
                {
                    int n = Mathf.Min(st.Stock[w], 12);
                    var dir = Quaternion.Euler(0, 18f + w * 36f, 0) * Vector3.forward;
                    var p = dir * StoreRadius;
                    var c = LandLook.WareColor((Ware)w);
                    LandShapes.Disc(mb, p + Vector3.up * 0.02f, 0.42f, 8, LandLook.DarkWood);
                    for (int i = 0; i < n; i++)
                        mb.Box(p + Vector3.up * (0.11f + i * 0.2f), new Vector3(0.5f, 0.18f, 0.5f), c * (i % 2 == 0 ? 1f : 0.9f), 18f + w * 36f);
                    if (st.Stock[w] > 12) mb.Cone(p + Vector3.up * (0.2f + 12 * 0.2f), 0.3f, 0.35f, 6, c);
                    StockBlocks += n;
                }
                LandLook.SetMesh(ref _store, transform, "StorePiles", mb);
            }

            // City and upgrade.
            bool city = st.IsCity;
            int pal = city ? 1000 : st.Upgrade != UpgradeState.None ? Mathf.RoundToInt(10f * st.UpgradeProgress / Mathf.Max(1, tu.CityBuildTicks)) : -1;
            if (city != ShownCity || _totem == null)
            {
                ShownCity = city;
                if (_totem != null) Destroy(_totem);
                _totem = LandLook.Part(transform, "Totem", LandLook.Cached("totem/" + Faction + "/" + (city ? "city" : "camp"), mb => TotemShape(mb, Faction, city)));
                _totem.transform.localPosition = TotemSpot();
            }
            if (pal != _palisadeSig)
            {
                _palisadeSig = pal;
                var mb = new MeshBuilder();
                if (pal >= 0) Palisade(mb, city ? 1f : pal / 10f);
                LandLook.SetMesh(ref _palisade, transform, "Palisade", mb);
            }
            if (st.Upgrade != UpgradeState.None && !city)
            {
                // Between the first spoke (a local path) and the hex edge next to it (a road), 30 degrees from both.
                var mid = SideDir();
                var along = Vector3.Cross(Vector3.up, mid);
                UpgradeSite.Sync(tu.CityCost, st.UpgradeDelivered, st.UpgradeProgress / (float)Mathf.Max(1, tu.CityBuildTicks),
                    st.Upgrade == UpgradeState.Constructing, mid * 4.6f - along * 1.4f, along, mid * 6.4f + along * 0.6f, Faction);
            }
            else UpgradeSite.Clear();

            // Plots and paths.
            SyncPlots(s, st);

            // Demons and the overrun.
            bool held = DemonApi.DemonsAt(s, DemonSiteKind.Corner, st.Corner) > 0;
            if (held != ShownHeld || overrun != ShownOverrun || _held == null && held)
            {
                ShownHeld = held; ShownOverrun = overrun;
                if (_fire != null) _fire.SetActive(!held && !overrun);
                if (_smoke != null) _smoke.SetActive(held || overrun);
                if (held && _held == null)
                    _held = LandLook.Part(transform, "HeldGround", LandLook.Cached("camp/held", mb =>
                    {
                        LandShapes.Disc(mb, new Vector3(0, 0.06f, 0), 3.4f, 20, new Color(0.10f, 0.04f, 0.10f));
                        LandShapes.Ring(mb, new Vector3(0, 0.08f, 0), 3.9f, 0.5f, 24, new Color(0.45f, 0.08f, 0.30f));
                    }), LandLook.Unlit, false);
                if (_held != null) _held.SetActive(held);
                if (overrun && _burn == null)
                    _burn = LandLook.Part(transform, "Burning", LandLook.Cached("camp/burn", mb =>
                    {
                        for (int i = 0; i < 5; i++) LandShapes.Flames(mb, Quaternion.Euler(0, i * 72f, 0) * Vector3.forward * 2.0f, 0.7f, 2.4f);
                    }), LandLook.Unlit, false);
                if (_burn != null) _burn.SetActive(overrun);
                if (_totem != null) _totem.transform.localRotation = overrun ? Quaternion.Euler(78f, 30f, 0) : Quaternion.identity;
            }
        }

        /// <summary>Unit direction 30 degrees between the first spoke and the hex edge beside it (clear of paths and roads).</summary>
        private Vector3 SideDir()
        {
            if (_spokes.Length < 2) return Vector3.forward;
            var edge = (_spokes[0] + _spokes[1]).normalized;
            return (edge + _spokes[0]).normalized;
        }

        private Vector3 TotemSpot()
        {
            // Between the first two spokes, just inside the storehouse ring (clear of the piles).
            var d = _spokes.Length > 1 ? (_spokes[0] + _spokes[1]).normalized : Vector3.forward;
            return -d * 1.15f;
        }

        private void BuildCamp()
        {
            if (_hearth != null) return;
            _hearth = LandLook.Part(transform, "Hearth", LandLook.Cached("camp/hearth", mb =>
            {
                for (int i = 0; i < 9; i++)
                {
                    var p = Quaternion.Euler(0, i * 40f, 0) * Vector3.forward * 0.85f;
                    mb.Box(p + Vector3.up * 0.12f, new Vector3(0.36f, 0.24f, 0.3f), LandLook.Stone * (0.9f + 0.03f * (i % 3)), i * 40f);
                }
                LandShapes.Disc(mb, new Vector3(0, 0.03f, 0), 0.75f, 10, new Color(0.12f, 0.10f, 0.09f));
                for (int i = 0; i < 4; i++)
                {
                    var d = Quaternion.Euler(0, i * 90f + 20f, 0) * Vector3.forward;
                    LandShapes.Beam(mb, d * 0.6f + Vector3.up * 0.05f, Vector3.up * 0.35f, 0.07f, 5, LandLook.DarkWood);
                }
            }));
            LandLook.Part(transform, "CampRing", LandLook.Cached("camp/ring/" + Faction, mb =>
            {
                var pc = LandLook.People(Faction);
                for (int i = 0; i < 24; i++)
                {
                    var p = Quaternion.Euler(0, i * 15f, 0) * Vector3.forward * 3.3f;
                    mb.Box(p + Vector3.up * 0.09f, new Vector3(0.45f, 0.18f, 0.35f), i % 2 == 0 ? pc : LandLook.Stone, i * 15f);
                }
            }));
            _fire = LandLook.Part(transform, "Fire", LandLook.Cached("camp/fire", mb => LandShapes.Flames(mb, Vector3.up * 0.1f, 0.5f, 1.1f)), LandLook.Unlit, false);
            _smoke = LandLook.Part(transform, "Smoke", LandLook.Cached("camp/smoke", mb =>
            {
                // A thin column of grey puffs drifting up from the cold hearth.
                for (int i = 0; i < 6; i++)
                {
                    float y = 0.4f + i * 0.75f, r = 0.14f + i * 0.06f;
                    mb.Box(new Vector3(0.25f * i * 0.25f, y, -0.1f * i), new Vector3(r * 2, r * 1.5f, r * 2), new Color(0.42f, 0.40f, 0.42f) * (1f + i * 0.05f), i * 25f);
                }
            }), LandLook.Unlit, false);
            _smoke.SetActive(false);
        }

        public static void TotemShape(MeshBuilder mb, int faction, bool city)
        {
            var pc = LandLook.People(faction); var tc = LandLook.PeopleTrim(faction);
            float h = city ? 8.5f : 6.2f;
            LandShapes.Beam(mb, Vector3.zero, Vector3.up * h, 0.22f, 8, LandLook.Wood);
            for (int i = 0; i < (city ? 4 : 3); i++)
                mb.Cylinder(Vector3.up * (1.2f + i * 1.5f), 0.26f, 0.26f, 0.45f, 8, i % 2 == 0 ? pc : tc);
            // Carved face.
            mb.Box(new Vector3(0, h - 1.9f, 0.2f), new Vector3(0.45f, 0.6f, 0.12f), tc * 0.9f);
            mb.Box(new Vector3(-0.1f, h - 1.8f, 0.27f), new Vector3(0.1f, 0.08f, 0.05f), new Color(0.1f, 0.08f, 0.06f));
            mb.Box(new Vector3(0.1f, h - 1.8f, 0.27f), new Vector3(0.1f, 0.08f, 0.05f), new Color(0.1f, 0.08f, 0.06f));
            // Wings (both sides) in the people's colour.
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Quad(new Vector3(0.2f * s, h - 1.2f, 0), new Vector3(1.6f * s, h - 0.7f, 0), new Vector3(1.4f * s, h - 1.5f, 0), new Vector3(0.2f * s, h - 1.6f, 0), pc);
                mb.Quad(new Vector3(0.2f * s, h - 1.6f, 0), new Vector3(1.4f * s, h - 1.5f, 0), new Vector3(1.6f * s, h - 0.7f, 0), new Vector3(0.2f * s, h - 1.2f, 0), pc * 0.9f);
            }
            var top = Vector3.up * h;
            if (faction == PlayerIds.Rival)
            {
                // Horned ash skull.
                mb.Box(top + Vector3.up * 0.3f, new Vector3(0.6f, 0.55f, 0.55f), new Color(0.82f, 0.80f, 0.74f));
                mb.Box(top + new Vector3(-0.13f, 0.35f, 0.28f), new Vector3(0.14f, 0.14f, 0.04f), new Color(0.08f, 0.06f, 0.05f));
                mb.Box(top + new Vector3(0.13f, 0.35f, 0.28f), new Vector3(0.14f, 0.14f, 0.04f), new Color(0.08f, 0.06f, 0.05f));
                foreach (float s in new[] { -1f, 1f })
                    LandShapes.Beam(mb, top + new Vector3(0.28f * s, 0.45f, 0), top + new Vector3(0.9f * s, 1.3f, 0.1f), 0.08f, 5, new Color(0.85f, 0.82f, 0.72f));
                if (city) LandShapes.Beam(mb, top + Vector3.up * 0.55f, top + Vector3.up * 1.6f, 0.05f, 4, pc);
            }
            else
            {
                // Sun disc with rays.
                var c = top + Vector3.up * 0.7f;
                for (int i = 0; i < 12; i++)
                {
                    float a0 = i * 30f * Mathf.Deg2Rad, a1 = (i + 1) * 30f * Mathf.Deg2Rad;
                    var p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.55f;
                    var p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 0.55f;
                    mb.Tri(c, p1, p0, tc); mb.Tri(c, p0, p1, tc);
                    var r = c + new Vector3(Mathf.Cos(a0 + 0.26f), Mathf.Sin(a0 + 0.26f), 0) * (city ? 1.15f : 0.9f);
                    mb.Tri(p0, p1, r, tc * 0.95f); mb.Tri(p1, p0, r, tc * 0.95f);
                }
            }
        }

        /// <summary>A ring of stakes at 8 m with gaps on the spokes and the hex edges (fraction = how much stands).</summary>
        private void Palisade(MeshBuilder mb, float fraction)
        {
            int total = 48;
            int up = Mathf.RoundToInt(total * Mathf.Clamp01(fraction));
            int n = 0;
            for (int i = 0; i < total && n < up; i++)
            {
                float a = i * 360f / total;
                // Gaps every 30 degrees around the lattice rays from the corner (spokes, edges and between).
                bool gap = false;
                foreach (var sp in _spokes)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(a, BoardSpace.YawOf(sp)));
                    if (Mathf.Abs(Mathf.DeltaAngle(d, Mathf.Round(d / 60f) * 60f)) < 9f) gap = true; // spokes and hex edges
                }
                if (gap) continue;
                n++;
                var p = BoardSpace.DirOf(a) * 8f;
                float h = 2.2f + 0.25f * ((i * 7) % 3);
                LandShapes.Beam(mb, p, p + Vector3.up * h, 0.14f, 5, LandLook.Wood * (0.9f + 0.05f * (i % 3)));
                mb.Cone(p + Vector3.up * h, 0.14f, 0.35f, 5, LandLook.Wood * 0.8f);
            }
        }

        private void SyncPlots(WorldState s, Settlement st)
        {
            var plots = EconomyApi.PlotsOf(s, st.Id);
            var sb = new System.Text.StringBuilder();
            var views = new PlotView[plots.Length];
            for (int i = 0; i < plots.Length; i++)
            {
                views[i] = EconomyApi.Plot(s, plots[i]);
                sb.Append(plots[i]).Append((int)views[i].Use).Append(',');
            }
            string sig = sb.ToString();
            if (sig == _plotSig) return;
            _plotSig = sig;
            var origin = transform.position;
            var rings = new MeshBuilder();
            var paths = new MeshBuilder();
            var t = s.Board.Topology;
            FreePlotRings = 0; PathCount = 0;
            foreach (var pv in views)
            {
                var c = LandLook.Ground(pv.X, pv.Z) - origin;
                if (pv.Use == PlotUse.Free)
                {
                    FreePlotRings++;
                    for (int k = 0; k < 10; k++)
                    {
                        var p = c + Quaternion.Euler(0, k * 36f, 0) * Vector3.forward * 4.3f;
                        rings.Box(p + Vector3.up * 0.06f, new Vector3(0.34f, 0.12f, 0.28f), new Color(0.66f, 0.64f, 0.58f), k * 36f + 20f);
                    }
                }
                else if (pv.Use == PlotUse.Site || pv.Use == PlotUse.Building)
                {
                    PathCount++;
                    var pts = PlotLayout.PathPoints(t, pv.Plot);
                    for (int k = 0; k + 3 < pts.Length; k += 2)
                    {
                        var a = LandLook.Ground(pts[k], pts[k + 1]) - origin;
                        var b = LandLook.Ground(pts[k + 2], pts[k + 3]) - origin;
                        if (k == 0) { var d = (b - a); a += d.normalized * 3.0f; }
                        paths.Strip(a + Vector3.up * 0.05f, b + Vector3.up * 0.05f, 1.5f, new Color(0.52f, 0.42f, 0.30f));
                        LandShapes.Disc(paths, b + Vector3.up * 0.05f, 0.75f, 8, new Color(0.52f, 0.42f, 0.30f));
                    }
                }
            }
            LandLook.SetMesh(ref _plots, transform, "PlotRings", rings);
            LandLook.SetMesh(ref _paths, transform, "LocalPaths", paths, null, false);
        }

        public void Animate(float now)
        {
            if (_fire != null && _fire.activeSelf)
            {
                float f = 1f + 0.12f * Mathf.Sin(now * 9f + Corner) + 0.07f * Mathf.Sin(now * 23f);
                _fire.transform.localScale = new Vector3(1f, f, 1f);
            }
            if (_smoke != null && _smoke.activeSelf) _smoke.transform.localRotation = Quaternion.Euler(0, now * 12f, Mathf.Sin(now * 0.7f) * 3f);
            if (_burn != null && _burn.activeSelf) _burn.transform.localScale = new Vector3(1, 1f + 0.2f * Mathf.Sin(now * 11f), 1);
            Site.Animate(now); UpgradeSite.Animate(now);
        }
    }
}
