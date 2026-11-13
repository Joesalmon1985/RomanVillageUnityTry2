using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// World views of the road economy: roads (one per 20 m segment), flags with ware stacks, buildings and sites,
    /// carriers and batch markers. Everything is reconciled from <c>WorldState.Economy</c> after every GameClock tick,
    /// so <see cref="RebuildFromState"/> reproduces the scene from state alone (e.g. after loading a save). Per tick it
    /// walks the buildings, carriers and 61 hexes; the 630 road segments are rescanned only after a road event or a
    /// rebuild, never every frame. Also draws the floating labels (names, statuses, loads, batches, beacons).
    /// </summary>
    public sealed class EconomyViews : MonoBehaviour
    {
        public static EconomyViews Instance { get; private set; }

        /// <summary>Building labels show within this distance in first person.</summary>
        public const float LabelRangeFirstPerson = 30f;
        public const float LoadLabelRangeFirstPerson = 30f;
        /// <summary>God View zoom (ortho half-height) up to which building names / statuses / carrier loads are labelled.</summary>
        /// <summary>Above this God View size only selected, hovered or stalled buildings show their name tag.</summary>
        public const float GodDeclutterSize = 75f;
        public const float GodNameMaxSize = 170f, GodStatusMaxSize = 95f, GodLoadMaxSize = 45f;

        private readonly Dictionary<int, RoadView> _roads = new Dictionary<int, RoadView>();
        private readonly Dictionary<int, BuildingView> _buildings = new Dictionary<int, BuildingView>();
        private readonly Dictionary<int, CarrierView> _carriers = new Dictionary<int, CarrierView>();
        private readonly Dictionary<int, FlagView> _flags = new Dictionary<int, FlagView>();
        private readonly Dictionary<int, BatchMarker> _batches = new Dictionary<int, BatchMarker>();
        private readonly HashSet<int> _clearedHexes = new HashSet<int>();

        private WorldBootstrap _boot;
        private Transform _root;
        private float _lastTickTime;
        private bool _roadsDirty = true, _structureDirty = true;

        /// <summary>Raised after the views were reconciled with a tick's state (events of that tick).</summary>
        public event Action<IReadOnlyList<SimEvent>> Synced;

        public int RoadViewCount => _roads.Count;
        public int CarrierViewCount => _carriers.Count;
        public int BuildingViewCount => _buildings.Count;
        public int FlagViewCount => _flags.Count;
        public int BatchMarkerCount => _batches.Count;
        public IEnumerable<BuildingView> Buildings => _buildings.Values;
        public IEnumerable<CarrierView> Carriers => _carriers.Values;
        public IEnumerable<RoadView> Roads => _roads.Values;
        public IEnumerable<BatchMarker> Batches => _batches.Values;
        public IEnumerable<FlagView> Flags => _flags.Values;
        /// <summary>Number of full road rescans so far (tests: roads are not rescanned every tick).</summary>
        public int RoadScans { get; private set; }

        public RoadView Road(int edge) => _roads.TryGetValue(edge, out var v) ? v : null;
        public BuildingView Building(int id) => _buildings.TryGetValue(id, out var v) ? v : null;
        public CarrierView Carrier(int id) => _carriers.TryGetValue(id, out var v) ? v : null;
        public BatchMarker Batch(int hex) => _batches.TryGetValue(hex, out var v) ? v : null;
        public FlagView Flag(int vertex) => _flags.TryGetValue(vertex, out var v) ? v : null;
        public bool HasFlag(int vertex) => _flags.ContainsKey(vertex);

        /// <summary>Fraction (0..1) of the next tick that has elapsed, for interpolation.</summary>
        public float TickFraction => Mathf.Clamp01((Time.time - _lastTickTime) * WorldSim.TicksPerSecond);

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            _root = new GameObject("EconomyViews").transform;
            _root.SetParent(boot.transform, false);
            boot.Clock.Ticked += OnTicked;
            if (boot.Picker != null) boot.Picker.SelectionChanged += OnSelectionChanged;
            ProblemsTool.BuildingResolver = id => Building(id);
            ProblemsTool.CarrierResolver = id => Carrier(id);
            RebuildFromState();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            if (_boot != null && _boot.Picker != null) _boot.Picker.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(IInspectable sel)
        {
            if (sel is InspectForward f && f != null && f.Target != null) Picker.Instance.Select(f.Target);
        }

        // ---------------- rebuild / reconcile ----------------

        /// <summary>Destroys every economy view (tests and loads use this before RebuildFromState).</summary>
        public void DestroyAllViews()
        {
            foreach (var v in _roads.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _buildings.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _carriers.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _flags.Values) if (v != null) Destroy(v.gameObject);
            foreach (var v in _batches.Values) if (v != null) Destroy(v.gameObject);
            _roads.Clear(); _buildings.Clear(); _carriers.Clear(); _flags.Clear(); _batches.Clear();
        }

        /// <summary>Recreates all views from the current sim state (nothing is kept from events).</summary>
        public void RebuildFromState()
        {
            DestroyAllViews();
            _roadsDirty = true; _structureDirty = true;
            SyncAll(null);
            foreach (var c in _carriers.Values) c.Snap();
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            _lastTickTime = Time.time;
            SyncAll(events);
            Synced?.Invoke(events);
        }

        /// <summary>Brings every view in line with the state (create, update, remove).</summary>
        public void SyncAll(IReadOnlyList<SimEvent> events)
        {
            var s = _boot.Sim.State;
            var e = s.Economy;
            var topo = s.Board.Topology;
            if (events != null)
                foreach (var ev in events)
                    if (ev is RoadPlaced || ev is RoadRemoved) { _roadsDirty = true; break; }

            // Roads: rescan only after a road event or a rebuild.
            if (_roadsDirty)
            {
                _roadsDirty = false; _structureDirty = true; RoadScans++;
                for (int edge = 0; edge < e.Roads.Length; edge++)
                {
                    bool want = e.Roads[edge];
                    bool have = _roads.TryGetValue(edge, out var rv) && rv != null;
                    if (want && !have) _roads[edge] = RoadView.Create(_root, edge);
                    else if (!want && _roads.ContainsKey(edge)) { if (rv != null) Destroy(rv.gameObject); _roads.Remove(edge); }
                }
            }

            // Buildings and sites (the storehouse already stands in the village).
            int live = 0;
            foreach (var b in e.Buildings)
            {
                bool want = b.State != BuildingState.Removed && b.Type != BuildingType.Storehouse;
                bool have = _buildings.TryGetValue(b.Id, out var bv) && bv != null;
                if (want)
                {
                    live++;
                    if (!have) { _buildings[b.Id] = BuildingView.Create(_root, s, b, _boot.Layout); _structureDirty = true; }
                    else bv.Sync(s, b);
                }
                else if (_buildings.ContainsKey(b.Id))
                {
                    if (have) Destroy(bv.gameObject);
                    _buildings.Remove(b.Id); _structureDirty = true;
                }
            }
            if (live != _buildings.Count) _structureDirty = true;

            if (_structureDirty)
            {
                _structureDirty = false;
                SyncFlags(s);
                SyncClearings(s);
            }

            // Ware stacks at building flags.
            foreach (var b in e.Buildings)
            {
                if (b.State == BuildingState.Removed || b.Type == BuildingType.Storehouse) continue;
                if (!_flags.TryGetValue(b.Vertex, out var fv) || fv == null) continue;
                fv.SetStack(EconomyLook.IsSite(b) ? null : b.Stock);
            }

            // Carriers.
            foreach (var c in e.Carriers)
            {
                if (!_carriers.TryGetValue(c.Id, out var cv) || cv == null)
                {
                    cv = CarrierView.Create(_root, c.Id);
                    _carriers[c.Id] = cv;
                    cv.Sync(s, c, c.Id);
                    cv.Snap();
                    cv.Animate(0f, 0f);
                }
                else cv.Sync(s, c, c.Id);
            }

            // Batch markers on every hex with units left.
            for (int h = 0; h < e.HexBatchRemaining.Length; h++)
            {
                int n = e.HexBatchRemaining[h];
                bool have = _batches.TryGetValue(h, out var m) && m != null;
                if (n <= 0)
                {
                    if (_batches.ContainsKey(h)) { if (have) Destroy(m.gameObject); _batches.Remove(h); }
                    continue;
                }
                if (!have)
                {
                    var raw = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                    if (!raw.HasValue) continue;
                    m = BatchMarker.Create(_root, h, raw.Value);
                    _batches[h] = m;
                }
                m.SetRemaining(n);
            }
        }

        private void SyncFlags(WorldState s)
        {
            var e = s.Economy;
            var topo = s.Board.Topology;
            var flags = new HashSet<int>();
            foreach (var b in e.Buildings) if (b.State != BuildingState.Removed) flags.Add(b.Vertex);
            foreach (var kv in _roads) { flags.Add(topo.EdgeVertexA(kv.Key)); flags.Add(topo.EdgeVertexB(kv.Key)); }
            foreach (int v in flags) if (!_flags.ContainsKey(v)) _flags[v] = FlagView.Create(_root, v);
            List<int> gone = null;
            foreach (var kv in _flags) if (!flags.Contains(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (int k in gone) { if (_flags[k] != null) Destroy(_flags[k].gameObject); _flags.Remove(k); }
            foreach (var bv in _buildings.Values)
                if (bv != null && _flags.TryGetValue(bv.Vertex, out var fv)) fv.SetStackDirection(bv.Plot.Into);
        }

        /// <summary>Keeps trees, rocks and crops off every building plot (rebuilds only the hexes whose plots changed).</summary>
        private void SyncClearings(WorldState s)
        {
            var byHex = new Dictionary<int, List<Vector3>>();
            foreach (var bv in _buildings.Values)
            {
                if (bv == null || bv.Plot.Hex < 0) continue;
                if (!byHex.TryGetValue(bv.Plot.Hex, out var l)) byHex[bv.Plot.Hex] = l = new List<Vector3>();
                l.Add(bv.Plot.Clearing);
            }
            foreach (var kv in byHex)
            {
                kv.Value.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                TerrainBuilder.SetClearings(kv.Key, kv.Value);
            }
            foreach (int h in _clearedHexes) if (!byHex.ContainsKey(h)) TerrainBuilder.SetClearings(h, null);
            _clearedHexes.Clear();
            foreach (int h in byHex.Keys) _clearedHexes.Add(h);
        }

        private void Update()
        {
            float f = TickFraction, dt = Time.deltaTime;
            foreach (var c in _carriers.Values) if (c != null) c.Animate(f, dt);
        }

        private void LateUpdate()
        {
            var views = _boot != null ? _boot.Views : null;
            bool god = views != null && views.CurrentView == ViewMode.God;
            float size = god ? _boot.GodCam.Size : 0f;
            float badge = god ? Mathf.Clamp(size / 30f, 1f, 5f) : 1f;
            float load = god ? Mathf.Clamp(size / 28f, 1.4f, 5f) : 1f;
            foreach (var b in _buildings.Values) if (b != null) b.SetBadgeScale(badge);
            foreach (var c in _carriers.Values) if (c != null) c.SetGodScale(load, god);
        }

        // ---------------- labels ----------------

        private GUIStyle _name, _status, _load;
        private int _styleFor = -1;

        private void OnGUI()
        {
            if (_boot == null || _boot.Views == null || Hud.MenuOpen) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            GUI.depth = 10; // under the toolbar, panels and toasts
            if (Event.current.type != EventType.Repaint) return;
            var views = _boot.Views;
            var cam = views.ActiveCamera;
            if (cam == null) return;
            bool god = views.CurrentView == ViewMode.God;
            float size = god ? _boot.GodCam.Size : 0f;
            var eye = cam.transform.position;
            _placed.Clear();
            int key = (god ? 1 : 0) + Screen.height * 2;
            if (_styleFor != key)
            {
                _styleFor = key;
                _name = new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = UiStyle.Px(god ? 19 : 23), wordWrap = false };
                _status = new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter, fontSize = UiStyle.Px(god ? 16 : 19), wordWrap = false };
                _load = new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = UiStyle.Px(god ? 15 : 19), wordWrap = false };
            }

            foreach (var b in BeaconRegistry.All)
            {
                if (b == null || !ScreenPoint(cam, b.LabelAnchor, out var p)) continue;
                DrawTag(p, b.Caption, null, new Color(1f, 0.85f, 0.25f));
            }

            var selected = Picker.Instance != null ? Picker.Instance.Selected : null;
            var hovered = Picker.Instance != null ? Picker.Instance.Hovered : null;
            if (!god || size <= GodNameMaxSize)
                foreach (var bv in _buildings.Values)
                {
                    if (bv == null) continue;
                    if (!god && (bv.transform.position - eye).magnitude > LabelRangeFirstPerson) continue;
                    // Declutter: zoomed out, only selected, hovered or stalled buildings keep their name tag.
                    if (god && size > GodDeclutterSize && !ReferenceEquals(selected, bv) && !ReferenceEquals(hovered, bv)
                        && bv.Badge != EconomyBadge.Blocked && bv.Badge != EconomyBadge.Waiting) continue;
                    if (!ScreenPoint(cam, bv.LabelAnchor + (god ? Vector3.up * 2f * (Mathf.Clamp(size / 30f, 1f, 5f) - 1f) : Vector3.zero), out var p)) continue;
                    string name = bv.IsSite ? bv.DisplayName + " site" : bv.DisplayName;
                    bool withStatus = !god || size <= GodStatusMaxSize || ReferenceEquals(selected, bv);
                    string status = withStatus ? (bv.MaterialsText ?? bv.ShortStatus) : null;
                    DrawTag(p, name, status, EconomyLook.BadgeColor(bv.Badge));
                }

            foreach (var cv in _carriers.Values)
            {
                if (cv == null || !cv.HasLoad) continue;
                bool sel = ReferenceEquals(selected, cv);
                if (god && size > GodLoadMaxSize && !sel) continue;
                if (!god && (cv.transform.position - eye).magnitude > LoadLabelRangeFirstPerson) continue;
                if (!ScreenPoint(cam, cv.CratePosition + Vector3.up * (god ? 1.2f : 0.6f), out var p)) continue;
                var col = EconomyLook.WareColor(cv.Load);
                string text = EconomyApi.WareTitle(cv.Load);
                var sz = _load.CalcSize(new GUIContent(text));
                var r = Place(new Rect(p.x - sz.x / 2 - UiStyle.Px(7), p.y - sz.y - UiStyle.Px(2), sz.x + UiStyle.Px(14), sz.y + UiStyle.Px(2)));
                GUI.DrawTexture(r, UiStyle.SolidTexture(new Color(0.06f, 0.05f, 0.04f, 0.8f)));
                GUI.DrawTexture(new Rect(r.x, r.yMax - UiStyle.Px(4), r.width, UiStyle.Px(4)), UiStyle.SolidTexture(col));
                var old = _load.normal.textColor; _load.normal.textColor = EconomyLook.WareTextColor(cv.Load);
                GUI.Label(r, text, _load);
                _load.normal.textColor = old;
            }

            foreach (var m in _batches.Values)
            {
                if (m == null) continue;
                if (!god && (m.transform.position - eye).magnitude > 70f) continue;
                if (god && size > 300f) continue;
                if (!ScreenPoint(cam, m.LabelAnchor, out var p)) continue;
                DrawTag(p, EconomyApi.WareTitle(m.Ware) + " batch " + m.Remaining, null, EconomyLook.WareColor(m.Ware));
            }
        }

        private readonly List<Rect> _placed = new List<Rect>();

        /// <summary>Moves a label up until it does not overlap labels already drawn this pass.</summary>
        private Rect Place(Rect r)
        {
            for (int guard = 0; guard < 10; guard++)
            {
                bool hit = false;
                foreach (var o in _placed)
                    if (o.Overlaps(r)) { r.y = o.y - r.height - 2; hit = true; }
                if (!hit) break;
            }
            _placed.Add(r);
            return r;
        }

        private static bool ScreenPoint(Camera cam, Vector3 world, out Vector2 p)
        {
            var sp = cam.WorldToScreenPoint(world);
            p = new Vector2(sp.x, Screen.height - sp.y);
            return sp.z > 0 && sp.x > -200 && sp.x < Screen.width + 200 && sp.y > -100 && sp.y < Screen.height + 100;
        }

        private void DrawTag(Vector2 p, string name, string status, Color badge)
        {
            var nc = new GUIContent(name);
            var ns = _name.CalcSize(nc);
            float w = ns.x, h = ns.y;
            Vector2 ss = Vector2.zero;
            if (!string.IsNullOrEmpty(status)) { ss = _status.CalcSize(new GUIContent(status)); w = Mathf.Max(w + UiStyle.Px(20), ss.x); h += ss.y; }
            float dot = UiStyle.Px(12), pad = UiStyle.Px(7);
            w += dot + pad * 3;
            var r = Place(new Rect(p.x - w / 2, p.y - h - pad, w, h + pad));
            GUI.DrawTexture(r, UiStyle.SolidTexture(new Color(0.06f, 0.05f, 0.04f, 0.78f)));
            GUI.DrawTexture(new Rect(r.x + pad, r.y + (ns.y - dot) / 2 + pad / 2, dot, dot), UiStyle.SolidTexture(badge));
            GUI.Label(new Rect(r.x + pad * 2 + dot, r.y + pad / 2, w - dot - pad * 3, ns.y), nc, _name);
            if (!string.IsNullOrEmpty(status))
            {
                var old = _status.normal.textColor; _status.normal.textColor = Color.Lerp(badge, Color.white, 0.45f);
                GUI.Label(new Rect(r.x + pad, r.y + pad / 2 + ns.y, w - pad * 2, ss.y), status, _status);
                _status.normal.textColor = old;
            }
        }
    }
}
