using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// View of one economy building or construction site: its model beside the flag (BuildingPlots), scaffolding with
    /// the delivered materials and a progress bar while a site, a status badge (working / idle / waiting / blocked /
    /// off, including a demon on its flag and a robbed hex), a glow or turning part while it works, and a sign at the
    /// flag. Driven only by <see cref="Sync"/> from the sim state; inspectable with the same lines in both views.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour, IEconomyBuilding
    {
        public int BuildingId { get; private set; }
        public BuildingType Type { get; private set; }
        public int Vertex { get; private set; }
        public Plot Plot { get; private set; }
        public BuildingState ShownState { get; private set; } = (BuildingState)(-1);
        public EconomyBadge Badge { get; private set; }
        public string ShortStatus { get; private set; } = "";
        /// <summary>"Planks 1/2  Bricks 0/1" while a site, else null.</summary>
        public string MaterialsText { get; private set; }
        /// <summary>0..1 site delivery or construction progress; -1 when active.</summary>
        public float Progress { get; private set; } = -1f;
        public bool IsSite => ShownState == BuildingState.Site || ShownState == BuildingState.Constructing;
        public bool IsWorking => _working;
        public string DisplayName { get; private set; }
        public float ModelHeight => _model != null ? _model.Height : 4.5f;
        public Vector3 LabelAnchor => transform.position + Vector3.up * (ModelHeight + 1.6f);
        public Vector3 FlagPosition => BoardSpace.Vertex(Vertex);
        public BuildingSign Sign { get; private set; }
        public Transform BadgeTransform => _badge != null ? _badge.transform : null;
        public BuildingModel Model => _model;
        public int ShownMaterialCount { get; private set; }

        private BuildingModel _model;
        private GameObject _scaffold, _materials, _bar, _badge;
        private Transform _barFill;
        private Renderer _badgeRenderer, _badgeLow, _barFillRenderer;
        private readonly int[] _shownStock = new int[Wares.Count];
        private bool _working, _badgeSet;
        private float _badgeScale = 1f;

        public string Title
        {
            get
            {
                var s = Sim;
                return s == null ? DisplayName : EconomyApi.Describe(s, BuildingId).Title;
            }
        }

        public IEnumerable<string> Lines => Sim == null ? new string[0] : EconomyText.BuildingLines(Sim, BuildingId);

        private static WorldState Sim => WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;

        public static BuildingView Create(Transform parent, WorldState s, Building b, VillageLayout layout)
        {
            var plot = BuildingPlots.For(s, layout, b.Vertex, b.Type);
            var go = new GameObject("Building " + b.Id + " " + b.Type);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(plot.Centre, Quaternion.Euler(0, plot.Yaw, 0));
            var v = go.AddComponent<BuildingView>();
            v.BuildingId = b.Id; v.Type = b.Type; v.Vertex = b.Vertex; v.Plot = plot;
            v.DisplayName = EconomyApi.Def(s, b.Type).Name;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 2.5f, 0);
            col.size = new Vector3(plot.Half * 2f, 5f, plot.Half * 2f);
            v.Sign = BuildingSign.Create(v);
            v.BuildBar();
            v.BuildBadge();
            v.Sync(s, b);
            return v;
        }

        // ---------------- sync from state ----------------

        public void Sync(WorldState s, Building b)
        {
            var def = EconomyApi.Def(s, b.Type);
            if (b.State != ShownState)
            {
                ShownState = b.State;
                bool site = IsSite;
                if (_model == null && (b.State == BuildingState.Active || b.State == BuildingState.Constructing))
                    _model = BuildingModels.Build(b.Type, transform);
                if (_model != null) _model.Root.SetActive(b.State != BuildingState.Site);
                if (site && _scaffold == null) BuildScaffold();
                if (_scaffold != null) _scaffold.SetActive(site);
                if (_materials != null) _materials.SetActive(site);
                if (_bar != null) _bar.SetActive(site);
                if (b.State == BuildingState.Active && _model != null) _model.Root.transform.localScale = Vector3.one;
                if (_badge != null) _badge.transform.localPosition = new Vector3(0, ModelHeight + 1.0f, 0);
            }

            if (IsSite)
            {
                int need = 0, have = 0;
                var parts = new List<string>();
                for (int w = 0; w < Wares.Count; w++)
                {
                    if (def.Cost[w] <= 0) continue;
                    need += def.Cost[w]; have += Mathf.Min(b.Stock[w], def.Cost[w]);
                    parts.Add(EconomyApi.WareTitle((Ware)w) + " " + b.Stock[w] + "/" + def.Cost[w]);
                }
                MaterialsText = string.Join("  ", parts);
                float p = b.State == BuildingState.Site ? (need == 0 ? 1f : (float)have / need) : Mathf.Clamp01((float)b.BuildProgress / Mathf.Max(1, def.BuildTicks));
                if (!Mathf.Approximately(p, Progress) && _barFill != null)
                {
                    _barFill.localScale = new Vector3(Mathf.Max(0.001f, p) * 3.6f, 0.26f, 0.12f);
                    _barFill.localPosition = new Vector3(-1.8f + p * 1.8f, 0, -0.02f);
                    _barFillRenderer.sharedMaterial = MaterialFactory.SharedUnlit(b.State == BuildingState.Site ? new Color(1f, 0.75f, 0.2f) : new Color(0.35f, 0.9f, 0.35f));
                }
                Progress = p;
                if (b.State == BuildingState.Constructing && _model != null)
                    _model.Root.transform.localScale = new Vector3(1f, Mathf.Max(0.05f, p), 1f);
                SyncMaterials(b);
            }
            else { MaterialsText = null; Progress = -1f; }

            var badge = EconomyLook.BadgeOf(s, b);
            if (badge != Badge || !_badgeSet)
            {
                Badge = badge; _badgeSet = true;
                _badgeRenderer.sharedMaterial = _badgeLow.sharedMaterial = MaterialFactory.SharedUnlit(EconomyLook.BadgeColor(Badge));
            }
            ShortStatus = EconomyLook.ShortStatus(s, b);

            bool working = b.State == BuildingState.Active && b.Enabled && !b.Blocked
                           && (b.WorkProgress > 0 || (def.Kind == BuildingKind.PowerPlant && b.FuelLeft > 0));
            if (working != _working || _model != null && _model.Glow != null && _model.Glow.enabled != working)
            {
                _working = working;
                if (_model != null && _model.Glow != null) _model.Glow.enabled = working;
            }
        }

        // ---------------- pieces ----------------

        private void BuildScaffold()
        {
            float hs = Plot.Half - 0.4f;
            _scaffold = new GameObject("Scaffold");
            _scaffold.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            var pole = new Color(0.62f, 0.47f, 0.28f);
            mb.Box(new Vector3(0, 0.08f, 0), new Vector3(hs * 2f, 0.16f, hs * 2f), new Color(0.55f, 0.50f, 0.44f));
            // Corner stakes with string: the marked-out footprint.
            foreach (float x in new[] { -hs, hs }) foreach (float z in new[] { -hs, hs })
                mb.Box(new Vector3(x, 0.5f, z), new Vector3(0.12f, 1.0f, 0.12f), new Color(0.85f, 0.80f, 0.65f));
            foreach (float z in new[] { -hs, hs }) mb.Box(new Vector3(0, 0.8f, z), new Vector3(hs * 2f, 0.03f, 0.03f), Color.white);
            foreach (float x in new[] { -hs, hs }) mb.Box(new Vector3(x, 0.8f, 0), new Vector3(0.03f, 0.03f, hs * 2f), Color.white);
            // Scaffold frame at the back half.
            float h = 3.6f;
            foreach (float x in new[] { -hs + 0.4f, 0f, hs - 0.4f }) foreach (float z in new[] { -hs + 0.4f, -0.4f })
                mb.Box(new Vector3(x, h / 2, z), new Vector3(0.15f, h, 0.15f), pole);
            foreach (float y in new[] { 1.3f, 2.6f, 3.5f })
                foreach (float z in new[] { -hs + 0.4f, -0.4f })
                    mb.Box(new Vector3(0, y, z), new Vector3(hs * 2f - 0.6f, 0.1f, 0.1f), pole * 0.9f);
            mb.Box(new Vector3(0, 2.68f, -hs * 0.5f), new Vector3(hs * 2f - 0.8f, 0.06f, hs - 0.6f), new Color(0.80f, 0.66f, 0.42f));
            mb.ToObject("Frame", _scaffold.transform, BuildingModels.VertexMat);
            _materials = new GameObject("Materials");
            _materials.transform.SetParent(transform, false);
        }

        private void SyncMaterials(Building b)
        {
            bool same = true;
            for (int w = 0; w < Wares.Count; w++) if (_shownStock[w] != b.Stock[w]) same = false;
            if (same || _materials == null) return;
            for (int i = _materials.transform.childCount - 1; i >= 0; i--) Destroy(_materials.transform.GetChild(i).gameObject);
            int slot = 0;
            float hs = Plot.Half - 0.9f;
            for (int w = 0; w < Wares.Count; w++)
            {
                _shownStock[w] = b.Stock[w];
                for (int k = 0; k < b.Stock[w]; k++, slot++)
                {
                    var p = new Vector3(-hs + (slot % 4) * 1.1f, 0.3f + (slot / 4) * 0.42f, hs);
                    var size = (Ware)w == Ware.Planks ? new Vector3(0.95f, 0.2f, 0.55f) : new Vector3(0.6f, 0.4f, 0.6f);
                    Signs.Prim(PrimitiveType.Cube, _materials.transform, EconomyApi.WareName((Ware)w), p, size, MaterialFactory.Shared(EconomyLook.WareColor((Ware)w)));
                }
            }
            ShownMaterialCount = slot;
        }

        private void BuildBar()
        {
            _bar = new GameObject("ProgressBar");
            _bar.transform.SetParent(transform, false);
            _bar.transform.localPosition = new Vector3(0, 4.8f, 0);
            Signs.Prim(PrimitiveType.Cube, _bar.transform, "Back", Vector3.zero, new Vector3(3.8f, 0.4f, 0.1f), MaterialFactory.SharedUnlit(new Color(0.12f, 0.1f, 0.08f)));
            var fill = Signs.Prim(PrimitiveType.Cube, _bar.transform, "Fill", new Vector3(-1.8f, 0, -0.02f), new Vector3(0.001f, 0.26f, 0.12f), MaterialFactory.SharedUnlit(new Color(1f, 0.75f, 0.2f)));
            _barFill = fill.transform;
            _barFillRenderer = fill.GetComponent<Renderer>();
            _bar.SetActive(false);
        }

        private static Mesh _diamond;

        private void BuildBadge()
        {
            _badge = new GameObject("Badge");
            _badge.transform.SetParent(transform, false);
            _badge.transform.localPosition = new Vector3(0, 6.0f, 0);
            if (_diamond == null)
            {
                var mb = new MeshBuilder();
                mb.Cone(Vector3.zero, 0.55f, 0.75f, 4, Color.white);
                _diamond = mb.ToMesh("Badge");
            }
            var go = new GameObject("Diamond");
            go.transform.SetParent(_badge.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _diamond;
            _badgeRenderer = go.AddComponent<MeshRenderer>();
            _badgeRenderer.sharedMaterial = MaterialFactory.SharedUnlit(Color.white);
            _badgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var low = new GameObject("DiamondLow");
            low.transform.SetParent(go.transform, false);
            low.transform.localRotation = Quaternion.Euler(180f, 0, 0);
            low.AddComponent<MeshFilter>().sharedMesh = _diamond;
            _badgeLow = low.AddComponent<MeshRenderer>();
            _badgeLow.sharedMaterial = _badgeRenderer.sharedMaterial;
            _badgeLow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Badge scale (EconomyViews grows it with God View zoom so it stays readable).</summary>
        public void SetBadgeScale(float s)
        {
            if (Mathf.Approximately(s, _badgeScale) || _badge == null) return;
            _badgeScale = s;
            _badge.transform.localScale = Vector3.one * s;
            _badge.transform.localPosition = new Vector3(0, ModelHeight + 1.0f + 0.6f * (s - 1f), 0);
        }

        private void OnDestroy()
        {
            if (Sign != null) Destroy(Sign.gameObject);
        }

        // ---------------- animation ----------------

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_model != null && _model.Spinner != null && _working)
                _model.Spinner.Rotate(_model.SpinAxis, _model.SpinDegPerSec * dt, Space.Self);
            if (_badge != null) _badge.transform.Rotate(0, 90f * dt, 0, Space.Self);
            if (_bar != null && _bar.activeSelf)
            {
                var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : null;
                if (cam != null)
                {
                    var f = cam.transform.forward; f.y = 0;
                    if (f.sqrMagnitude > 1e-4f) _bar.transform.rotation = Quaternion.LookRotation(f);
                }
            }
        }
    }
}
