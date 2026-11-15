using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Builds the running campaign's interiors and keeps them in step with the sim (package I5, PRD_CRYSTALS 5.10):
    /// walk regions on the lattice far to the east, rock, stairs, doors, markers and lights from each
    /// <see cref="InteriorDef"/>; doors and stairs open when their facts are known; stepping onto an open stair or exit
    /// takes the player through it with a short walked fade; the player's interior (<see cref="Interiors.Current"/>)
    /// follows the walker's cell; "Lead me out" walks to the nearest way up. Reads state only.
    /// </summary>
    public sealed class InteriorDirector : MonoBehaviour
    {
        public const string RegionOwner = "interiors";
        private const string CrossLock = "interior.crossing";

        public static InteriorDirector Instance { get; private set; }

        /// <summary>Seconds of the walk into a stair (fading out), the dark, and the walk out (fading in).</summary>
        public static float WalkInSeconds = 0.45f, DarkSeconds = 0.15f, WalkOutSeconds = 0.45f;
        /// <summary>Cells revealed on the map round the player (steps over open floor).</summary>
        public const int RevealSteps = 2;

        private WorldBootstrap _boot;
        private readonly List<InteriorModel> _models = new List<InteriorModel>();
        private readonly List<InteriorLink> _links = new List<InteriorLink>();
        private readonly Dictionary<Cell, InteriorLink> _linkAt = new Dictionary<Cell, InteriorLink>();
        private readonly List<IInteriorView> _views = new List<IInteriorView>();
        private readonly Dictionary<string, HashSet<Cell>> _explored = new Dictionary<string, HashSet<Cell>>();
        private Transform _root, _surfaceRoot;
        private string _builtHash;
        private CampaignDefinition _def;
        private float _fade;
        private Cell _lastRevealCell = new Cell(int.MinValue, int.MinValue);
        private string _lastRevealIn;

        public IReadOnlyList<InteriorModel> Models => _models;
        public IReadOnlyList<InteriorLink> Links => _links;
        public CampaignDefinition Definition => _def;
        public bool IsCrossing { get; private set; }
        public bool LeadingOut { get; private set; }
        /// <summary>Crossings made since boot (tests).</summary>
        public int CrossingCount { get; private set; }
        /// <summary>How many times the interiors were built (tests).</summary>
        public int BuildCount { get; private set; }
        /// <summary>The fade's opacity now (0 clear, 1 black).</summary>
        public float Fade => _fade;
        public Transform SurfaceRoot => _surfaceRoot;

        public static InteriorDirector Create(WorldBootstrap boot)
        {
            var go = new GameObject("Interiors");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<InteriorDirector>();
            d._boot = boot;
            Instance = d;
            go.AddComponent<InteriorLighting>();
            d._root = go.transform;
            d._surfaceRoot = new GameObject("Surface ways").transform;
            d._surfaceRoot.SetParent(go.transform, false);
            MapScreen.AddLayer(InteriorMapLayer.Instance);
            // Leftovers of the previous scene (statics survive a reload).
            WalkGrid.RemoveOpenRegions(RegionOwner);
            Interiors.SetCurrent(null);
            var s = boot.Sim != null ? boot.Sim.State : null;
            if (s?.Campaign != null) d.BuildFor(s.Campaign.Definition);
            boot.Clock.Ticked += d.OnTicked;
            return d;
        }

        private void Awake() { Instance = this; }

        private void Start()
        {
            var w = PlayerWalker.Instance;
            if (w != null) { w.Stepped += OnStepped; w.AutoWalkEnded += OnAutoEnded; }
        }

        private void OnDestroy()
        {
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            var w = PlayerWalker.Instance;
            if (w != null) { w.Stepped -= OnStepped; w.AutoWalkEnded -= OnAutoEnded; w.RemoveLock(CrossLock); }
            MapScreen.RemoveLayer(InteriorMapLayer.Instance);
            WalkGrid.RemoveOpenRegions(RegionOwner);
            if (Instance == this) { Instance = null; Interiors.SetCurrent(null); }
        }

        private WorldState State => _boot != null && _boot.Sim != null ? _boot.Sim.State : null;

        private string StateHash()
        {
            var c = State?.Campaign;
            return c?.Definition == null ? null : c.DefinitionHash ?? c.Definition.Id;
        }

        // ---------------- building ----------------

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            if (StateHash() != _builtHash) BuildFor(State?.Campaign?.Definition);
            SyncViews();
        }

        /// <summary>Rebuilds from the running state and re-reads the player's place.</summary>
        public void RebuildFromState()
        {
            var s = State;
            _builtHash = "\u0000"; // force
            BuildFor(s?.Campaign?.Definition);
            UpdateCurrent(true);
        }

        public void BuildFor(CampaignDefinition def)
        {
            Clear();
            _def = def;
            // A build by hand (tests, tools) holds until the running campaign changes.
            _builtHash = StateHash();
            if (def != null && def.Interiors != null && def.Interiors.Count > 0)
            {
                for (int k = 0; k < def.Interiors.Count; k++)
                {
                    var idef = def.Interiors[k];
                    if (idef == null) continue;
                    var m = new InteriorModel
                    {
                        Id = idef.Id, Name = string.IsNullOrEmpty(idef.Name) ? idef.Id : idef.Name, Location = LocationOf(def, idef),
                        Index = k, Def = idef, Origin = InteriorSpace.Origin(k), Height = idef.Rows?.Count ?? 0,
                        Markers = InteriorMaps.Markers(idef),
                    };
                    if (idef.Rows != null) foreach (var r in idef.Rows) m.Width = Math.Max(m.Width, r?.Length ?? 0);
                    for (int j = 0; j < m.Height; j++)
                        for (int i = 0; i < m.Width; i++)
                            if (m.IsFloor(i, j)) m.Floor.Add(m.ToCell(i, j));
                    _models.Add(m);
                    WalkGrid.AddOpenRegion(RegionOwner, m.Floor, InteriorSpace.FloorY);
                }
                BuildLinks(def);
                ComputeDepths();
                foreach (var l in _links) l.Up = IsUp(l);
                foreach (var m in _models)
                {
                    m.Root = new GameObject("Interior " + m.Id).transform;
                    m.Root.SetParent(_root, false);
                    InteriorBuilder.Build(this, m, _views);
                    m.Root.gameObject.SetActive(false);
                }
                foreach (var l in _links)
                {
                    WalkGrid.SetCellAvoided(RegionOwner, l.Cell, true);
                    if (l.OnSurface) _views.Add(SurfaceWayView.Create(_surfaceRoot, l, this));
                }
            }
            BuildCount++;
            SyncViews();
            UpdateCurrent(true);
            Interiors.RaiseBuilt();
            if (_models.Count > 0) Debug.Log("[Interiors] built " + _models.Count + " interiors, " + _links.Count + " way ends, " + WalkGrid.RegionCellCount + " floor cells (" + def.Id + ")");
        }

        private void Clear()
        {
            foreach (var m in _models) if (m.Root != null) Destroy(m.Root.gameObject);
            for (int i = _surfaceRoot.childCount - 1; i >= 0; i--) Destroy(_surfaceRoot.GetChild(i).gameObject);
            _models.Clear(); _links.Clear(); _linkAt.Clear(); _views.Clear();
            WalkGrid.RemoveOpenRegions(RegionOwner);
            _def = null;
        }

        /// <summary>The location an interior is filed under (its own Location, else the one its links name).</summary>
        private static string LocationOf(CampaignDefinition def, InteriorDef idef)
        {
            if (!string.IsNullOrEmpty(idef.Location)) return idef.Location;
            if (idef.Legend != null)
                foreach (var md in idef.Legend.Values)
                    if (md != null && MarkerRef.TryParse(md.To, out var r) && !r.IsInterior) return r.Location;
            return "";
        }

        public InteriorModel Model(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in _models) if (m.Id == id) return m;
            return null;
        }

        private void BuildLinks(CampaignDefinition def)
        {
            foreach (var m in _models)
                foreach (var md in m.Markers)
                {
                    bool way = md.Kind == MarkerKind.Exit || md.Kind == MarkerKind.Stairs || (md.Kind == MarkerKind.Door && !string.IsNullOrEmpty(md.To));
                    if (!way || !MarkerRef.TryParse(md.To, out var to)) continue;
                    var l = new InteriorLink { End = m.Ref(md.Id), To = to, Kind = md.Kind, Name = Named(md), Cell = m.ToCell(md.I, md.J) };
                    AddNeed(l, md.OpenWhen);
                    _links.Add(l);
                }
            // Surface ends: every surface marker whose To leads into a built interior, and every surface marker an
            // interior link names (even when it does not name back: the validator reports that).
            var board = State?.Board;
            if (def.Locations != null && board != null)
                foreach (var loc in def.Locations)
                {
                    if (loc?.Markers == null) continue;
                    foreach (var md in loc.Markers)
                    {
                        if (md == null) continue;
                        var self = MarkerRef.Surface(loc.Id, md.Id);
                        bool named = _links.Exists(x => x.To == self);
                        bool leads = MarkerRef.TryParse(md.To, out var to) && to.IsInterior && Model(to.Interior) != null;
                        if (!named && !leads) continue;
                        if (!leads) to = _links.Find(x => x.To == self).End;
                        if (!CampaignPlaces.TryMarkerCell(def, self, board, out int ci, out int cj)) continue;
                        var l = new InteriorLink { End = self, To = to, Kind = md.Kind, Name = Named(md), Cell = new Cell(ci, cj) };
                        AddNeed(l, md.OpenWhen);
                        _links.Add(l);
                    }
                }
            // Each end also needs the other end's OpenWhen and every connection that joins the two.
            foreach (var l in _links)
            {
                var other = _links.Find(x => x.End == l.To);
                if (other != null && MarkerOf(other.End) is MarkerDef omd) AddNeed(l, omd.OpenWhen);
                if (def.Connections != null)
                    foreach (var c in def.Connections)
                        if (c != null && ((c.A == l.End && c.B == l.To) || (c.B == l.End && c.A == l.To))) AddNeed(l, c.OpenWhen);
            }
            foreach (var l in _links)
            {
                l.OutHeading = OutHeadingOf(l);
                _linkAt[l.Cell] = l;
            }
        }

        private static void AddNeed(InteriorLink l, string fact) { if (!string.IsNullOrEmpty(fact) && !l.Needs.Contains(fact)) l.Needs.Add(fact); }
        private static string Named(MarkerDef md) => string.IsNullOrEmpty(md.Name) ? md.Id : md.Name;

        /// <summary>The marker definition a reference names (surface or interior), or null.</summary>
        public MarkerDef MarkerOf(MarkerRef r)
        {
            if (r.IsInterior) return Model(r.Interior)?.Marker(r.Marker);
            var loc = _def?.Location(r.Location);
            return loc?.Markers?.Find(x => x != null && x.Id == r.Marker);
        }

        public bool TryMarkerCell(MarkerRef r, out Cell cell)
        {
            cell = default;
            if (r.IsInterior)
            {
                var m = Model(r.Interior);
                var md = m?.Marker(r.Marker);
                if (md == null) return false;
                cell = m.ToCell(md.I, md.J);
                return true;
            }
            var s = State;
            if (_def == null || s == null || !CampaignPlaces.TryMarkerCell(_def, r, s.Board, out int i, out int j)) return false;
            cell = new Cell(i, j);
            return true;
        }

        /// <summary>
        /// The heading the player faces on arriving at a way's end: inside, towards the most open neighbouring floor;
        /// on the surface, away from the way (towards the location's anchor, or its safe point when the way is on it).
        /// </summary>
        private int OutHeadingOf(InteriorLink l)
        {
            if (l.End.IsInterior)
            {
                var m = Model(l.End.Interior);
                int best = 0, bestScore = int.MinValue;
                for (int h = 0; h < 6; h++)
                {
                    int score = (Open(m, l.Cell, h) ? 4 : 0) + (Open(m, l.Cell, h - 1) ? 1 : 0) + (Open(m, l.Cell, h + 1) ? 1 : 0)
                                + (Open(m, l.Cell + WalkLattice.Dir(h), h) ? 1 : 0);
                    if (!Open(m, l.Cell, h)) score -= 10;
                    if (score > bestScore) { bestScore = score; best = h; }
                }
                return best;
            }
            var s = State;
            var loc = _def.Location(l.End.Location);
            if (loc != null && s != null && CampaignPlaces.TryAnchorCell(_def, loc, s.Board, out int ai, out int aj))
            {
                var anchor = new Cell(ai, aj);
                if (anchor != l.Cell) return PlayerWalker.HeadingTowards(l.Cell, Step1(l.Cell, anchor));
                if (!string.IsNullOrEmpty(loc.SafePoint) && TryMarkerCell(MarkerRef.Surface(loc.Id, loc.SafePoint), out var safe) && safe != l.Cell)
                    return PlayerWalker.HeadingTowards(l.Cell, Step1(l.Cell, safe));
            }
            return 3;
        }

        /// <summary>The neighbour of a cell on the way to another (nearest by heading).</summary>
        private static Cell Step1(Cell from, Cell to)
        {
            var d = WalkLattice.ToWorld(to) - WalkLattice.ToWorld(from);
            return from + WalkLattice.Dir(WalkLattice.HeadingOf(BoardSpace.YawOf(d)));
        }

        private static bool Open(InteriorModel m, Cell c, int h) => m.Floor.Contains(c + WalkLattice.Dir(h));

        /// <summary>Depth of each interior: 1 for those with a way to the surface, then one more per stair down.</summary>
        private void ComputeDepths()
        {
            foreach (var m in _models) m.Depth = 0;
            var queue = new Queue<InteriorModel>();
            foreach (var l in _links)
                if (l.End.IsInterior && !l.To.IsInterior)
                {
                    var m = Model(l.End.Interior);
                    if (m != null && m.Depth == 0) { m.Depth = 1; queue.Enqueue(m); }
                }
            while (queue.Count > 0)
            {
                var m = queue.Dequeue();
                foreach (var l in _links)
                {
                    if (l.End.Interior != m.Id || !l.To.IsInterior) continue;
                    var n = Model(l.To.Interior);
                    if (n != null && n.Depth == 0) { n.Depth = m.Depth + 1; queue.Enqueue(n); }
                }
            }
            foreach (var m in _models) if (m.Depth == 0) m.Depth = 1;
        }

        private bool IsUp(InteriorLink l)
        {
            if (!l.End.IsInterior) return false;           // from the surface every way leads down
            if (!l.To.IsInterior) return true;             // to the surface
            var a = Model(l.End.Interior); var b = Model(l.To.Interior);
            if (a == null || b == null) return false;
            return b.Depth != a.Depth ? b.Depth < a.Depth : b.Index < a.Index;
        }

        // ---------------- keeping in step ----------------

        private void SyncViews()
        {
            foreach (var m in _models)
                foreach (var md in m.Markers)
                    if (md.Kind == MarkerKind.Door)
                        WalkGrid.SetCellBlocked(RegionOwner, m.ToCell(md.I, md.J), !Interiors.Knows(md.OpenWhen));
            var s = State;
            foreach (var v in _views)
            {
                try { v.Sync(s); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void Update()
        {
            if (Interiors.FactsOverride != null) SyncDoorsOnly();
            if (!IsCrossing) UpdateCurrent(false);
            Reveal();
            if (LeadingOut && !IsCrossing) ContinueLeadOut();
        }

        private void SyncDoorsOnly()
        {
            // Tests drive facts directly (no tick): keep doors and views in step every frame.
            SyncViews();
        }

        private void UpdateCurrent(bool force)
        {
            var w = PlayerWalker.Instance;
            string now = null;
            if (w != null && !w.IsOffLattice && WalkGrid.RegionOwner(w.Cell) == RegionOwner && InteriorSpace.TryLocal(w.Cell, out var id, out _, out _)) now = id;
            if (now != Interiors.Current || force) SetActiveInterior(now);
        }

        private void SetActiveInterior(string id)
        {
            foreach (var m in _models)
                if (m.Root != null && m.Root.gameObject.activeSelf != (m.Id == id)) m.Root.gameObject.SetActive(m.Id == id);
            Interiors.SetCurrent(id);
        }

        private void Reveal()
        {
            var w = PlayerWalker.Instance;
            var cur = Interiors.Current;
            if (w == null || cur == null) return;
            if (w.Cell == _lastRevealCell && cur == _lastRevealIn) return;
            _lastRevealCell = w.Cell; _lastRevealIn = cur;
            RevealAround(cur, w.Cell);
        }

        /// <summary>Marks the floor cells within <see cref="RevealSteps"/> open steps of a world cell as seen.</summary>
        public void RevealAround(string interiorId, Cell world)
        {
            var m = Model(interiorId);
            if (m == null || !m.Floor.Contains(world)) return;
            if (!_explored.TryGetValue(m.Id, out var set)) _explored[m.Id] = set = new HashSet<Cell>();
            var dist = new Dictionary<Cell, int> { [world] = 0 };
            var q = new Queue<Cell>();
            q.Enqueue(world);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                set.Add(c - m.Origin);
                int d = dist[c];
                if (d >= RevealSteps) continue;
                // One sees past an open cell, not through a shut door.
                if (d > 0 && WalkGrid.IsBlocked(c)) continue;
                for (int h = 0; h < 6; h++)
                {
                    var n = c + WalkLattice.Dir(h);
                    if (!m.Floor.Contains(n) || dist.ContainsKey(n)) continue;
                    dist[n] = d + 1;
                    q.Enqueue(n);
                }
            }
        }

        public IReadOnlyCollection<Cell> ExploredOf(string id) =>
            id != null && _explored.TryGetValue(id, out var set) ? set : (IReadOnlyCollection<Cell>)Array.Empty<Cell>();

        public string WriteExplored()
        {
            var sb = new StringBuilder();
            foreach (var kv in _explored)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(kv.Key);
                foreach (var c in kv.Value) sb.Append(';').Append(c.I.ToString(CultureInfo.InvariantCulture)).Append(',').Append(c.J.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public void ReadExplored(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var part in text.Split('|'))
            {
                var items = part.Split(';');
                if (items.Length == 0 || items[0].Length == 0) continue;
                if (!_explored.TryGetValue(items[0], out var set)) _explored[items[0]] = set = new HashSet<Cell>();
                for (int k = 1; k < items.Length; k++)
                {
                    var p = items[k].Split(',');
                    if (p.Length == 2 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int j))
                        set.Add(new Cell(i, j));
                }
            }
        }

        // ---------------- going through stairs and exits ----------------

        /// <summary>The way end on a world cell, or null.</summary>
        public InteriorLink LinkOn(Cell c) => _linkAt.TryGetValue(c, out var l) ? l : null;

        private void OnStepped()
        {
            if (IsCrossing) return;
            var w = PlayerWalker.Instance;
            var l = w != null ? LinkOn(w.Cell) : null;
            if (l == null) return;
            if (!l.IsOpen)
            {
                LeadingOut = false;
                SpeechBubbles.Say(null, LandLook.Capitalise(l.Name) + ": the way is shut.", 3f);
                return;
            }
            var other = _links.Find(x => x.End == l.To);
            if (other == null) return;
            StartCoroutine(Cross(l, other.Cell, other.OutHeading, l.End, other.End));
        }

        private void OnAutoEnded(bool arrived)
        {
            if (IsCrossing) return;
            if (!arrived) LeadingOut = false;
        }

        /// <summary>Puts the player on a marker with the fade (Interiors.Enter).</summary>
        public bool EnterAt(MarkerRef to)
        {
            if (IsCrossing || !TryMarkerCell(to, out var cell)) return false;
            var w = PlayerWalker.Instance;
            if (w == null) return false;
            var link = _links.Find(x => x.End == to);
            int heading = link != null ? link.OutHeading : w.Heading;
            var here = LinkOn(w.Cell);
            StartCoroutine(Cross(here, cell, heading, here != null ? here.End : default, to));
            return true;
        }

        /// <summary>Up to the surface at once by the nearest open way out (Interiors.Exit).</summary>
        public bool ExitNow()
        {
            var w = PlayerWalker.Instance;
            if (w == null || IsCrossing || Interiors.Current == null) return false;
            if (!FindWayOut(w.Cell, out _, out var surface, out _)) return false;
            var top = _links.Find(x => x.End == surface.To);
            if (top == null) return false;
            StartCoroutine(Cross(LinkOn(w.Cell), top.Cell, top.OutHeading, surface.End, top.End));
            return true;
        }

        /// <summary>"Lead me out" (Interiors.LeadOut).</summary>
        public bool StartLeadOut()
        {
            var w = PlayerWalker.Instance;
            if (w == null || Interiors.Current == null) return false;
            if (!FindWayOut(w.Cell, out var first, out _, out _)) { SpeechBubbles.Say(null, "There is no open way out from here.", 3f); return false; }
            LeadingOut = true;
            if (IsCrossing) return true;
            if (w.Cell == first.Cell) { StartCoroutine(Cross(first, _links.Find(x => x.End == first.To).Cell, _links.Find(x => x.End == first.To).OutHeading, first.End, first.To)); return true; }
            if (!w.WalkTo(first.Cell)) { LeadingOut = false; return false; }
            return true;
        }

        private void ContinueLeadOut()
        {
            var w = PlayerWalker.Instance;
            if (w == null) { LeadingOut = false; return; }
            if (Interiors.Current == null) { LeadingOut = false; return; }
            if (w.IsAutoWalking || w.IsGliding || w.IsLocked) return;
            if (!FindWayOut(w.Cell, out var first, out _, out _) || !w.WalkTo(first.Cell)) LeadingOut = false;
        }

        /// <summary>
        /// The nearest open way up from a world cell inside an interior: the first way end to walk to here, the way end
        /// that finally reaches the surface, and the steps on the way (each crossing counted as two). False when none.
        /// </summary>
        public bool FindWayOut(Cell from, out InteriorLink first, out InteriorLink surface, out int steps)
        {
            first = surface = null; steps = 0;
            if (!InteriorSpace.TryLocal(from, out var startId, out _, out _)) return false;
            var best = new Dictionary<InteriorLink, int>();
            var firstOf = new Dictionary<InteriorLink, InteriorLink>();
            var open = new List<(int cost, InteriorLink end, InteriorLink first)>();
            foreach (var kv in Distances(startId, from))
            {
                var l = LinkOn(kv.Key);
                if (l == null || !l.IsOpen || !l.End.IsInterior) continue;
                open.Add((kv.Value, l, l));
            }
            while (open.Count > 0)
            {
                open.Sort((a, b) => a.cost != b.cost ? a.cost.CompareTo(b.cost) : string.CompareOrdinal(a.end.End.ToString(), b.end.End.ToString()));
                var cur = open[0]; open.RemoveAt(0);
                if (best.ContainsKey(cur.end)) continue;
                best[cur.end] = cur.cost; firstOf[cur.end] = cur.first;
                if (!cur.end.To.IsInterior) { first = cur.first; surface = cur.end; steps = cur.cost + 2; return true; }
                var other = _links.Find(x => x.End == cur.end.To);
                if (other == null) continue;
                foreach (var kv in Distances(other.End.Interior, other.Cell))
                {
                    var l = LinkOn(kv.Key);
                    if (l == null || l == other || !l.IsOpen || best.ContainsKey(l)) continue;
                    open.Add((cur.cost + 2 + kv.Value, l, cur.first));
                }
            }
            return false;
        }

        /// <summary>Steps over open floor from a cell to every reachable cell of an interior (way ends are reached, not crossed).</summary>
        private Dictionary<Cell, int> Distances(string interiorId, Cell from)
        {
            var dist = new Dictionary<Cell, int>();
            var m = Model(interiorId);
            if (m == null || !m.Floor.Contains(from)) return dist;
            dist[from] = 0;
            var q = new Queue<Cell>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c != from && LinkOn(c) != null) continue;
                for (int h = 0; h < 6; h++)
                {
                    var n = c + WalkLattice.Dir(h);
                    if (!m.Floor.Contains(n) || dist.ContainsKey(n) || WalkGrid.IsBlocked(n)) continue;
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>
        /// The walked fade through a way: a few metres on into the stair (down or up) while the view goes dark, the
        /// player placed at the other end, then a few metres out of it while the view clears. The walker is locked
        /// throughout (about a second).
        /// </summary>
        private IEnumerator Cross(InteriorLink fromEnd, Cell toCell, int toHeading, MarkerRef fromRef, MarkerRef toRef)
        {
            var w = PlayerWalker.Instance;
            IsCrossing = true;
            CrossingCount++;
            Interiors.RaiseCrossing(fromRef, toRef);
            w.AddLock(CrossLock);
            if (w.IsAutoWalking) w.StopAuto();
            bool down = fromEnd != null ? !fromEnd.Up : true;
            // Into the stair: on along the way's direction (away from its open side).
            var start = w.Position;
            float yaw0 = w.Yaw;
            var into = fromEnd != null ? Flat(WalkLattice.ToWorld(WalkLattice.Dir(fromEnd.OutHeading + 3)) - WalkLattice.ToWorld(new Cell(0, 0))).normalized : Flat(w.LookDirection).normalized;
            float intoYaw = fromEnd != null ? WalkLattice.Yaw(fromEnd.OutHeading + 3) : yaw0;
            float t = 0f;
            while (t < WalkInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / WalkInSeconds), e = u * u * (3f - 2f * u);
                _fade = e;
                w.PlaceExact(start + into * (2.4f * e) + Vector3.up * ((down ? -1.1f : 1.1f) * e), Mathf.LerpAngle(yaw0, intoYaw, Mathf.Clamp01(u * 2f)), PlayerWalker.FixedPitch + (down ? 10f : -6f) * e);
                yield return null;
            }
            _fade = 1f;
            // The other end: show its interior first, so the walker's ground and the light are right at once.
            InteriorSpace.TryLocal(toCell, out var toId, out _, out _);
            if (WalkGrid.RegionOwner(toCell) != RegionOwner) toId = null;
            SetActiveInterior(toId);
            w.Teleport(toCell, toHeading);
            RevealAround(toId, toCell);
            float hold = 0f;
            while (hold < DarkSeconds) { hold += Time.unscaledDeltaTime; yield return null; }
            var end = PlayerWalker.CellPosition(toCell);
            var outDir = Flat(WalkLattice.ToWorld(WalkLattice.Dir(toHeading)) - WalkLattice.ToWorld(new Cell(0, 0))).normalized;
            float yaw1 = WalkLattice.Yaw(toHeading);
            var from1 = end - outDir * 2.2f + Vector3.up * (down ? 1.0f : -1.0f);
            t = 0f;
            while (t < WalkOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / WalkOutSeconds), e = u * u * (3f - 2f * u);
                _fade = 1f - e;
                w.PlaceExact(Vector3.Lerp(from1, end, e), yaw1, PlayerWalker.FixedPitch + (down ? 8f : -5f) * (1f - e));
                yield return null;
            }
            _fade = 0f;
            w.Teleport(toCell, toHeading);
            w.RemoveLock(CrossLock);
            IsCrossing = false;
            UpdateCurrent(true);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        private void OnGUI()
        {
            if (_fade <= 0.001f) return;
            GUI.depth = -1000;
            GUI.color = new Color(0, 0, 0, _fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    /// <summary>A view of something in or at an interior that follows the sim (doors, stairs, pedestals, altars).</summary>
    public interface IInteriorView
    {
        void Sync(WorldState s);
    }
}
