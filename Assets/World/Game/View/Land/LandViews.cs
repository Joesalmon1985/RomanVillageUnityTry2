using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// Every view of the land reconciled from state after each <see cref="GameClock.Ticked"/> (REFACTOR_SPEC 8.4,
    /// appendix B): number tokens and stock patches, camps, plots and local paths, buildings and sites, main roads,
    /// carriers and porters, warriors, demons, proposal markers, the war camp and the overrun, and the roll effect.
    /// <see cref="RebuildFromState"/> rebuilds everything from state alone (load, new game, tests). Per tick it walks
    /// only what exists (settlements, buildings, roads, carriers, warriors, open proposals) and the hexes whose stock
    /// changed or is growing; it never scans the 1350 plot ids or the 210 hex edges. Owner: package V.
    /// </summary>
    public sealed class LandViews : MonoBehaviour
    {
        public static LandViews Instance { get; private set; }

        /// <summary>Raised after the views have caught up with a tick, with that tick's events.</summary>
        public event Action<IReadOnlyList<SimEvent>> Synced;

        /// <summary>
        /// Where carrier views read carriers from (appendix B change log, V). Default: the sim. Tests set a fake feed
        /// and call <see cref="RebuildFromState"/>; set it back to <see cref="SimCarrierFeed.Instance"/> afterwards.
        /// </summary>
        public static ICarrierFeed CarrierFeed = SimCarrierFeed.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { CarrierFeed = SimCarrierFeed.Instance; }

        private WorldBootstrap _boot;
        private WorldSim _sim;
        private Transform _root, _dynamic;
        private HexLandView[] _hexes = new HexLandView[0];
        private readonly Dictionary<int, List<int>> _hexesByNumber = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, SettlementView> _settlements = new Dictionary<int, SettlementView>();
        private readonly Dictionary<int, World.Game.BuildingView> _buildings = new Dictionary<int, World.Game.BuildingView>();
        private readonly Dictionary<int, RoadView> _roads = new Dictionary<int, RoadView>();
        private readonly Dictionary<int, World.Game.CarrierView> _carriers = new Dictionary<int, World.Game.CarrierView>();
        private readonly Dictionary<string, DemonView> _demons = new Dictionary<string, DemonView>();
        private readonly Dictionary<int, ProposalMarkerView> _markers = new Dictionary<int, ProposalMarkerView>();
        private readonly HashSet<int> _dirtyHexes = new HashSet<int>();
        private readonly HashSet<int> _growingHexes = new HashSet<int>();
        private readonly Dictionary<int, List<Warrior>> _warriorsByLodge = new Dictionary<int, List<Warrior>>();
        private readonly List<(GameObject go, float start)> _bursts = new List<(GameObject, float)>();
        private WarCampView _warCamp;
        private string _campSig;
        private float _lastTickTime;

        // ---------------- appendix B surface ----------------

        public Transform CarrierView(int carrierId) => _carriers.TryGetValue(carrierId, out var v) && v != null ? v.transform : null;
        public Transform BuildingView(int buildingId) => _buildings.TryGetValue(buildingId, out var v) && v != null ? v.transform : null;
        public Transform ProposalMarker(int proposalId) => _markers.TryGetValue(proposalId, out var v) && v != null ? v.transform : null;

        // ---------------- extra read access (tests, other packages) ----------------

        public HexLandView Hex(int hex) => hex >= 0 && hex < _hexes.Length ? _hexes[hex] : null;
        public SettlementView Settlement(int id) => _settlements.TryGetValue(id, out var v) ? v : null;
        public World.Game.BuildingView Building(int id) => _buildings.TryGetValue(id, out var v) ? v : null;
        public RoadView Road(int id) => _roads.TryGetValue(id, out var v) ? v : null;
        public World.Game.CarrierView Carrier(int id) => _carriers.TryGetValue(id, out var v) ? v : null;
        public DemonView Demons(DemonSiteKind kind, int id) => _demons.TryGetValue(DemonView.Key(kind, id), out var v) ? v : null;
        public ProposalMarkerView Marker(int id) => _markers.TryGetValue(id, out var v) ? v : null;
        public WarCampView WarCamp => _warCamp;
        public IEnumerable<SettlementView> Settlements => _settlements.Values;
        public IEnumerable<World.Game.BuildingView> Buildings => _buildings.Values;
        public IEnumerable<RoadView> Roads => _roads.Values;
        public IEnumerable<World.Game.CarrierView> Carriers => _carriers.Values;
        public IEnumerable<DemonView> DemonViews => _demons.Values;
        public IEnumerable<ProposalMarkerView> Markers => _markers.Values;
        public int RebuildCount { get; private set; }
        /// <summary>Hex stock views recomputed so far (tests: only changed or growing hexes are looked at).</summary>
        public int HexSyncs { get; private set; }

        /// <summary>The viewing camera's position this frame (boards and tokens turn to it).</summary>
        public static Vector3 Eye { get; private set; }

        /// <summary>Fraction (0..1) of the next tick that has elapsed, for interpolation.</summary>
        public float TickFraction => Mathf.Clamp01((Time.time - _lastTickTime) * WorldSim.TicksPerSecond);

        // ---------------- setup ----------------

        public static LandViews Create(WorldBootstrap boot)
        {
            var go = new GameObject("LandViews");
            go.transform.SetParent(boot.transform, false);
            var v = go.AddComponent<LandViews>();
            v.Bind(boot);
            return v;
        }

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
        }

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            _sim = boot.Sim;
            _root = transform;
            var s = _sim.State;
            var t = s.Board.Topology;
            var hexRoot = new GameObject("Hexes").transform;
            hexRoot.SetParent(_root, false);
            _hexes = new HexLandView[t.HexCount];
            for (int h = 0; h < t.HexCount; h++)
            {
                _hexes[h] = HexLandView.Create(hexRoot, s.Board, h);
                int n = s.Board.HexNumber[h];
                if (!_hexesByNumber.TryGetValue(n, out var l)) _hexesByNumber[n] = l = new List<int>();
                l.Add(h);
            }
            boot.Clock.Ticked += OnTicked;
            RebuildFromState();
        }

        /// <summary>Rebuilds every view from the current state (after a load, and in tests).</summary>
        public void RebuildFromState()
        {
            if (_sim == null) return;
            if (_dynamic != null) Destroy(_dynamic.gameObject);
            foreach (var b in _buildings.Values) if (b != null && b.Gatherer != null) Destroy(b.Gatherer.gameObject);
            _settlements.Clear(); _buildings.Clear(); _roads.Clear(); _carriers.Clear(); _demons.Clear(); _markers.Clear();
            foreach (var (go, _) in _bursts) if (go != null) Destroy(go);
            _bursts.Clear();
            _warCamp = null;
            _campSig = null;
            _dynamic = new GameObject("Dynamic").transform;
            _dynamic.SetParent(_root, false);
            RebuildCount++;
            for (int h = 0; h < _hexes.Length; h++) _dirtyHexes.Add(h);
            SyncAll(null);
        }

        /// <summary>
        /// Feeds events through the same path as a clock tick (reconcile, then effects, then <see cref="Synced"/>).
        /// Used by the test-only fake driver for carrier events before the real logistics land.
        /// </summary>
        public void Inject(IReadOnlyList<SimEvent> events) => OnTicked(events);

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            _lastTickTime = Time.time;
            SyncAll(events);
            Synced?.Invoke(events);
        }

        // ---------------- reconcile ----------------

        private void SyncAll(IReadOnlyList<SimEvent> events)
        {
            var s = _sim.State;
            var e = s.Economy;
            if (e == null) return;

            if (events != null) ReadEvents(s, events);

            var outcome = FactionApi.Outcome(s);
            int overrunLoser = outcome.GameOver && outcome.Reason == OutcomeReasons.Overrun
                ? (outcome.Outcome == GameOutcome.Victory ? PlayerIds.Rival : PlayerIds.Human) : -1;

            // Camps.
            List<int> gone = null;
            foreach (var st in e.Settlements)
            {
                bool want = st.State != SettlementState.Cancelled;
                bool have = _settlements.TryGetValue(st.Id, out var sv) && sv != null;
                if (want)
                {
                    if (!have) _settlements[st.Id] = sv = SettlementView.Create(_dynamic, s, st);
                    sv.Sync(s, st, st.Faction == overrunLoser && st.State == SettlementState.Active);
                }
                else if (have) { Destroy(sv.gameObject); (gone ??= new List<int>()).Add(st.Id); }
            }
            if (gone != null) foreach (int id in gone) _settlements.Remove(id);

            // Warriors by lodge (few).
            foreach (var l in _warriorsByLodge.Values) l.Clear();
            foreach (var w in e.Warriors)
            {
                if (!_warriorsByLodge.TryGetValue(w.Lodge, out var l)) _warriorsByLodge[w.Lodge] = l = new List<Warrior>();
                l.Add(w);
            }

            // Buildings and sites.
            gone = null;
            foreach (var b in e.Buildings)
            {
                bool want = b.State != BuildingState.Retired && b.State != BuildingState.Cancelled;
                bool have = _buildings.TryGetValue(b.Id, out var bv) && bv != null;
                if (want)
                {
                    if (!have) _buildings[b.Id] = bv = World.Game.BuildingView.Create(_dynamic, s, b);
                    _warriorsByLodge.TryGetValue(b.Id, out var lw);
                    bv.Sync(s, b, lw);
                }
                else if (_buildings.ContainsKey(b.Id)) { if (have) Destroy(bv.gameObject); (gone ??= new List<int>()).Add(b.Id); }
            }
            if (gone != null) foreach (int id in gone) _buildings.Remove(id);

            // Main roads.
            gone = null;
            foreach (var r in e.Roads)
            {
                bool want = r.State != RoadState.Cancelled;
                bool have = _roads.TryGetValue(r.Id, out var rv) && rv != null;
                if (want)
                {
                    if (!have) _roads[r.Id] = rv = RoadView.Create(_dynamic, s, r);
                    rv.Sync(s, r);
                }
                else if (_roads.ContainsKey(r.Id)) { if (have) Destroy(rv.gameObject); (gone ??= new List<int>()).Add(r.Id); }
            }
            if (gone != null) foreach (int id in gone) _roads.Remove(id);

            SyncCarriers(s);
            SyncDemons(s);
            SyncMarkers(s);
            SyncWar(s, outcome);
            SyncHexes(s);
            SyncClearings(s);
        }

        private void ReadEvents(WorldState s, IReadOnlyList<SimEvent> events)
        {
            foreach (var ev in events)
            {
                switch (ev)
                {
                    case DiceRolled d:
                        if (d.Sum != 7 && _hexesByNumber.TryGetValue(d.Sum, out var list))
                            foreach (int h in list) _hexes[h].Flare();
                        break;
                    case StockAdded a: _dirtyHexes.Add(a.Hex); if (a.Count > 0) _hexes[a.Hex].Paid(a.Count); break;
                    case StockLost l: _dirtyHexes.Add(l.Hex); break;
                    case StockMatured m: _dirtyHexes.Add(m.Hex); break;
                    case CollectStarted c: _dirtyHexes.Add(c.Hex); break;
                    case Collected c: _dirtyHexes.Add(c.Hex); break;
                    case DemonPlaced _:
                    case DemonOutbreak _:
                    case DemonsBanished _:
                    case SettlementFounded _:
                        for (int h = 0; h < _hexes.Length; h++) _dirtyHexes.Add(h); // the hexes' stopped flags
                        break;
                    case FactionOverrun o: Burst(s, o.Defender); break;
                }
            }
        }

        private void SyncHexes(WorldState s)
        {
            foreach (int h in _growingHexes) _dirtyHexes.Add(h);
            _growingHexes.Clear();
            foreach (int h in _dirtyHexes)
            {
                var v = EconomyApi.HexStock(s, h);
                HexSyncs++;
                _hexes[h].Sync(v);
                if (v.Young > 0) _growingHexes.Add(h);
            }
            _dirtyHexes.Clear();
        }

        private readonly Dictionary<int, int> _idleIndex = new Dictionary<int, int>();

        private void SyncCarriers(WorldState s)
        {
            var feed = CarrierFeed ?? SimCarrierFeed.Instance;
            int n = feed.Count(s);
            _idleIndex.Clear();
            double frac = TickFraction;
            for (int id = 0; id < n; id++)
            {
                if (!feed.TryGet(s, id, frac, out var pose))
                {
                    if (_carriers.TryGetValue(id, out var dead) && dead != null) { Destroy(dead.gameObject); _carriers.Remove(id); }
                    continue;
                }
                if (!_carriers.TryGetValue(id, out var cv) || cv == null) _carriers[id] = cv = World.Game.CarrierView.Create(_dynamic, id, pose);
                cv.Sync(pose);
                if (!pose.Moving)
                {
                    _idleIndex.TryGetValue(pose.Home, out int k);
                    _idleIndex[pose.Home] = k + 1;
                    var home = pose.Home >= 0 && pose.Home < s.Economy.Settlements.Count ? s.Economy.Settlements[pose.Home] : null;
                    var c = home != null ? BoardSpace.Vertex(s.Board.Topology, home.Corner) : LandLook.Ground(pose.X, pose.Z);
                    cv.IdleSpot = c + IdleOffset(s, home, k);
                    cv.IdleFace = c;
                }
            }
            if (_carriers.Count > n)
            {
                var extra = new List<int>();
                foreach (var kv in _carriers) if (kv.Key >= n) extra.Add(kv.Key);
                foreach (int id in extra) { if (_carriers[id] != null) Destroy(_carriers[id].gameObject); _carriers.Remove(id); }
            }
        }

        /// <summary>Idle carriers wait 4 m from the corner in the six gaps between the spokes and the hex edges, then a ring further out.</summary>
        private static Vector3 IdleOffset(WorldState s, Settlement home, int k)
        {
            float baseYaw = 0f;
            if (home != null)
            {
                int h = PlotLayout.SectorHex(s.Board.Topology, home.Corner, 0);
                if (h >= 0) baseYaw = BoardSpace.YawOf(BoardSpace.Hex(s.Board.Topology, h) - BoardSpace.Vertex(s.Board.Topology, home.Corner));
            }
            int ring = k / 6, slot = k % 6;
            float yaw = baseYaw + 30f + slot * 60f + ring * 12f;
            return BoardSpace.DirOf(yaw) * (4.0f + ring * 1.3f);
        }

        private readonly HashSet<string> _seenDemons = new HashSet<string>();

        private void SyncDemons(WorldState s)
        {
            _seenDemons.Clear();
            var e = s.Economy;
            foreach (var st in e.Settlements)
                if (st.State == SettlementState.Active) DemonAt(s, DemonSiteKind.Corner, st.Corner);
            foreach (var r in e.Roads)
                if (r.State == RoadState.Built || r.State == RoadState.Constructing || r.State == RoadState.Site) DemonAt(s, DemonSiteKind.HexEdge, r.HexEdge);
            if (_demons.Count > _seenDemons.Count)
            {
                var gone = new List<string>();
                foreach (var kv in _demons) if (!_seenDemons.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var k in gone) { if (_demons[k] != null) Destroy(_demons[k].gameObject); _demons.Remove(k); }
            }
        }

        private void DemonAt(WorldState s, DemonSiteKind kind, int id)
        {
            int n = DemonApi.DemonsAt(s, kind, id);
            if (n <= 0) return;
            string key = DemonView.Key(kind, id);
            if (!_seenDemons.Add(key)) return;
            if (!_demons.TryGetValue(key, out var v) || v == null) _demons[key] = v = DemonView.Create(_dynamic, s, kind, id);
            v.Sync(n);
        }

        private readonly HashSet<int> _seenMarkers = new HashSet<int>();

        private void SyncMarkers(WorldState s)
        {
            _seenMarkers.Clear();
            var list = FactionApi.Proposals(s, PlayerIds.Human);
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                _seenMarkers.Add(p.Id);
                if (!_markers.TryGetValue(p.Id, out var m) || m == null) _markers[p.Id] = m = ProposalMarkerView.Create(_dynamic, s, p);
                m.SetTop(i == 0);
            }
            if (_markers.Count > _seenMarkers.Count)
            {
                var gone = new List<int>();
                foreach (var kv in _markers) if (!_seenMarkers.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { if (_markers[id] != null) Destroy(_markers[id].gameObject); _markers.Remove(id); }
            }
        }

        private void SyncWar(WorldState s, OutcomeView outcome)
        {
            var w = FactionApi.Warning(s);
            bool overrun = outcome.GameOver && outcome.Reason == OutcomeReasons.Overrun;
            int attacker = w.Active ? w.Attacker : overrun ? (outcome.Outcome == GameOutcome.Victory ? PlayerIds.Human : PlayerIds.Rival) : -1;
            int defender = attacker < 0 ? -1 : 1 - attacker;
            if (_warCamp != null && (attacker < 0 || _warCamp.Attacker != attacker)) { Destroy(_warCamp.gameObject); _warCamp = null; }
            if (attacker >= 0 && _warCamp == null) _warCamp = WarCampView.Create(_dynamic, s, attacker, defender);
            if (_warCamp != null) _warCamp.Sync(s);
        }

        /// <summary>The overrun moment: a red flash dome rising over every camp of the defender.</summary>
        private void Burst(WorldState s, int defender)
        {
            foreach (var st in s.Economy.Settlements)
            {
                if (st.Faction != defender || st.State != SettlementState.Active) continue;
                var go = LandLook.Part(_dynamic, "Overrun burst", LandLook.Cached("war/burst", mb =>
                {
                    LandShapes.FadeBeam(mb, Vector3.zero, 14f, 3f, 30f, 18, new Color(0.9f, 0.18f, 0.08f));
                    LandShapes.Ring(mb, new Vector3(0, 0.2f, 0), 14f, 2f, 36, new Color(1f, 0.5f, 0.1f));
                }), LandLook.Additive, false);
                go.transform.position = BoardSpace.Vertex(s.Board.Topology, st.Corner);
                _bursts.Add((go, Time.time));
            }
        }

        /// <summary>Trees, rocks and crops are cleared round every camp: its corner, its plots and its local paths.</summary>
        private void SyncClearings(WorldState s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var st in s.Economy.Settlements) if (st.State != SettlementState.Cancelled) sb.Append(st.Corner).Append(',');
            if (_warCamp != null) sb.Append("w").Append(_warCamp.transform.position.x.ToString("0"));
            string sig = sb.ToString();
            if (sig == _campSig) return;
            _campSig = sig;
            var t = s.Board.Topology;
            var byHex = new Dictionary<int, List<Vector3>>();
            void Add(Vector3 p, float r)
            {
                int h = t.HexAt(p.x, p.z);
                foreach (int hh in Near(t, p, r, h))
                {
                    if (!byHex.TryGetValue(hh, out var l)) byHex[hh] = l = new List<Vector3>();
                    l.Add(new Vector3(p.x, p.z, r));
                }
            }
            var corners = new List<int>();
            foreach (var st in s.Economy.Settlements) if (st.State != SettlementState.Cancelled && !corners.Contains(st.Corner)) corners.Add(st.Corner);
            corners.Sort();
            foreach (int c in corners)
            {
                Add(BoardSpace.Vertex(t, c), 10f);
                for (int sec = 0; sec < 3; sec++)
                {
                    if (PlotLayout.SectorHex(t, c, sec) < 0) continue;
                    for (int slot = 0; slot < 3; slot++)
                    {
                        int plot = PlotLayout.PlotId(c, sec, slot);
                        Add(LandLook.Ground(PlotLayout.PlotX(t, plot), PlotLayout.PlotZ(t, plot)), 6.8f);
                        var pts = PlotLayout.PathPoints(t, plot);
                        for (int k = 0; k + 3 < pts.Length; k += 2)
                        {
                            var a = LandLook.Ground(pts[k], pts[k + 1]); var b = LandLook.Ground(pts[k + 2], pts[k + 3]);
                            float len = (b - a).magnitude;
                            for (float u = 4f; u < len; u += 4f) Add(Vector3.Lerp(a, b, u / len), 2.4f);
                        }
                    }
                }
            }
            if (_warCamp != null) Add(_warCamp.transform.position, 8.5f);
            // Package D's home camp: the dance circle, tents and props, Mira's duel ground, the spawn view (x, z, radius).
            foreach (var c in new CampLayout(s.Board).Clearings) Add(new Vector3(c.x, 0, c.y), c.z);
            for (int h = 0; h < _hexes.Length; h++)
            {
                byHex.TryGetValue(h, out var l);
                TerrainBuilder.SetClearings(h, l);
            }
        }

        /// <summary>The hex of a point and its neighbours that a circle may reach.</summary>
        private static IEnumerable<int> Near(BoardTopology t, Vector3 p, float r, int h)
        {
            if (h >= 0) yield return h;
            for (int k = 0; k < 6; k++)
            {
                var q = p + BoardSpace.DirOf(k * 60f) * r;
                int hh = t.HexAt(q.x, q.z);
                if (hh >= 0 && hh != h) yield return hh;
            }
        }

        // ---------------- per frame ----------------

        private void Update()
        {
            if (_sim == null) return;
            float now = Time.time;
            var cam = ViewSwitcher.Instance != null && ViewSwitcher.Instance.ActiveCamera != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            var eye = cam != null ? cam.transform.position : Vector3.zero;
            Eye = eye;
            var s = _sim.State;
            foreach (var h in _hexes) h.Animate(eye, now);
            foreach (var v in _settlements.Values) if (v != null) v.Animate(now);
            foreach (var v in _buildings.Values)
            {
                if (v == null) continue;
                bool halted = _settlements.TryGetValue(SettlementOf(s, v.BuildingId), out var sv) && sv != null && sv.ShownHeld;
                v.Animate(now, halted);
            }
            foreach (var v in _roads.Values) if (v != null) v.Animate(now);
            var feed = CarrierFeed ?? SimCarrierFeed.Instance;
            double frac = TickFraction;
            foreach (var v in _carriers.Values) if (v != null) v.Animate(feed, s, frac, now);
            foreach (var v in _demons.Values) if (v != null) v.Animate(eye, now);
            foreach (var v in _markers.Values) if (v != null) v.Animate(now);
            if (_warCamp != null) _warCamp.Animate(now);
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var (go, start) = _bursts[i];
                float k = (now - start) / 4f;
                if (go == null || k >= 1f) { if (go != null) Destroy(go); _bursts.RemoveAt(i); continue; }
                go.transform.localScale = new Vector3(0.3f + k * 1.4f, 0.2f + k * 1.2f, 0.3f + k * 1.4f);
            }
        }

        private static int SettlementOf(WorldState s, int buildingId) =>
            buildingId >= 0 && buildingId < s.Economy.Buildings.Count ? s.Economy.Buildings[buildingId].Settlement : -1;

        // ---------------- test support ----------------

        /// <summary>
        /// A sorted description of every persistent view (kind, id, state and position to 0.1 m), so a rebuilt scene can
        /// be compared with an incrementally updated one. Transient effects and animated figures are left out.
        /// </summary>
        public List<string> Snapshot()
        {
            var list = new List<string>();
            string P(Vector3 p) => p.x.ToString("0.0") + "," + p.z.ToString("0.0");
            foreach (var h in _hexes) list.Add("hex " + h.HexId + " m" + h.ShownMature + " s" + h.ShownStage2 + "/" + h.ShownStage1 + "/" + h.ShownStage0 + (h.ShownStopped ? " stopped" : "") + (h.ReadyMarkShown ? " ready" : ""));
            foreach (var v in _settlements.Values) list.Add("camp " + v.SettlementId + " " + v.ShownState + (v.ShownCity ? " city" : "") + (v.ShownHeld ? " held" : "") + (v.ShownOverrun ? " overrun" : "") + " plots " + v.FreePlotRings + " paths " + v.PathCount + " stock " + v.StockBlocks + " @" + P(v.transform.position));
            foreach (var v in _buildings.Values) list.Add("bld " + v.BuildingId + " " + v.Type + " " + v.ShownState + " warriors " + v.WarriorsShown + "/" + v.FedWarriorsShown + " site " + v.Site.DeliveredShown + "/" + v.Site.MissingShown + " @" + P(v.transform.position));
            foreach (var v in _roads.Values) list.Add("road " + v.RoadId + " " + v.ShownState + " laid " + v.ShownLaid.ToString("0.00") + " @" + P(v.transform.position));
            foreach (var v in _carriers.Values) list.Add("carrier " + v.CarrierId + " load " + (v.Pose.HasLoad ? v.Pose.Load + "x" + v.Pose.LoadCount : "-") + (v.Pose.Shipment ? " ship" : "") + (v.Pose.Moving ? " moving" : " idle"));
            foreach (var v in _demons.Values) list.Add("demons " + DemonView.Key(v.Kind, v.SiteId) + " x" + v.CountShown);
            foreach (var v in _markers.Values) list.Add("marker " + v.ProposalId + (v.IsTop ? " top" : "") + " @" + P(v.Target));
            if (_warCamp != null) list.Add("warcamp " + _warCamp.Attacker + " @" + P(_warCamp.transform.position));
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
