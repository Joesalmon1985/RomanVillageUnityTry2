using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Plague;
using World.Sim.Table;
using World.Sim.War;

namespace World.Game
{
    /// <summary>
    /// The war in the world and on the god board (G3-3; PRD_GODBOARD section 6, WA-1..WA-18). From <see cref="WorldState.War"/> and
    /// its events only:
    /// <list type="bullet">
    /// <item>a <see cref="SoldierView"/> per soldier, at its corner or plague node, walking the trails between them, in its people's colour;</item>
    /// <item>duels (two soldiers face off, swing alternately, hit/block/dodge reactions, wear shown by stagger, limp, dropped shield,
    /// slow swing, death), purging at demon nodes, a cheer when a soldier is trained;</item>
    /// <item>a <see cref="SiteView"/> per war settlement: processing huts (one model, a prop per good), goods piles, the lodge at each home camp,
    /// fire, collapse, ruin and smoke when it burns (until the corner reopens), looted-camp dressing;</item>
    /// <item>on the god board and the God camera, soldiers also show as flat coloured markers.</item>
    /// </list>
    /// It never writes state. Installed by <see cref="GodBoardInstaller"/> on a GodBoard game.
    /// </summary>
    public sealed class WarViews : MonoBehaviour
    {
        public static WarViews Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        public WorldBootstrap Boot { get; private set; }
        public WorldState S => Boot.Sim.State;
        public WarState W => S.War;
        public int SoldierCount => _views.Count;
        public int DyingCount => _dying.Count;
        public int SiteCount => _sites.Count;
        public int DuelsStarted, SwingsPlayed, DeathsPlayed, TrainedMoments, PurgeActions, BurnsPlayed, LootsPlayed, HutsPopped;
        public bool Elevated { get; private set; }
        public IEnumerable<SoldierView> Soldiers => _views.Values;

        private readonly Dictionary<int, SoldierView> _views = new Dictionary<int, SoldierView>();
        private readonly List<SoldierView> _dying = new List<SoldierView>();
        private readonly Dictionary<int, SiteView> _sites = new Dictionary<int, SiteView>();
        private readonly Dictionary<int, Vector3> _stepFrom = new Dictionary<int, Vector3>();
        private readonly HashSet<int> _fresh = new HashSet<int>();
        private readonly List<Vector3> _taken = new List<Vector3>();
        private Transform _soldiers, _siteRoot;
        private readonly List<Vector3> _templeAt = new List<Vector3>();

        public SoldierView ViewOf(int id) => _views.TryGetValue(id, out var v) ? v : null;
        public string SiteCorners() { var l = new List<string>(); foreach (var kv in _sites) l.Add(kv.Key + ":" + kv.Value.State + "/huts" + kv.Value.HutCount); return string.Join(" ", l); }
        public SiteView SiteOf(int corner) => _sites.TryGetValue(corner, out var v) ? v : null;

        // ---------------------------------------------------------------- install

        public static WarViews Install(WorldBootstrap boot)
        {
            if (boot?.Sim?.State?.War == null) return null;
            var go = new GameObject("WarViews");
            go.transform.SetParent(boot.transform, false);
            var v = go.AddComponent<WarViews>();
            v.Boot = boot;
            Instance = v;
            v._soldiers = new GameObject("Soldiers").transform; v._soldiers.SetParent(go.transform, false);
            v._siteRoot = new GameObject("Sites").transform; v._siteRoot.SetParent(go.transform, false);
            var d = GodBoardDirector.Instance;
            if (d != null) foreach (var t in d.Temples) v._templeAt.Add(t.transform.position);
            boot.Clock.Ticked += v.OnTicked;
            return v;
        }

        private void OnDestroy()
        {
            if (Boot != null && Boot.Clock != null) Boot.Clock.Ticked -= OnTicked;
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- positions

        public static Vector3 NodePos(int node) => new Vector3(PlagueGraphData.X[node - 1], BoardSpace.GroundY, PlagueGraphData.Z[node - 1]);
        public Vector3 CornerPos(int corner) => BoardSpace.Vertex(S.Board.Topology, corner);
        private Vector3 StepPos(int step) => step > 0 ? NodePos(step) : CornerPos(-step - 1);

        private Color PeopleColour(int p)
        {
            foreach (var pe in S.Peoples) if (pe.Id == p) return TempleView.PeopleColour(pe);
            return Color.white;
        }

        /// <summary>Finds clear ground near a site for a hut (footprint ~3 m) or the lodge (~6.5 m).</summary>
        public bool PlaceNear(SiteView site, float footprint, int slot, out Vector3 at, out float yaw, float minRadius = 10.5f)
        {
            var board = S.Board; var c = site.transform.position;
            float start = (site.Corner * 37 + slot * 97) % 360;
            for (float r = minRadius; r <= 52f; r += 3f)
                for (int k = 0; k < 24; k++)
                {
                    var p = c + BoardSpace.DirOf(start + k * 15f) * r;
                    if (board.Topology.HexAt(p.x, p.z) < 0) continue;
                    bool ok = true;
                    for (int q = 0; q < 6 && ok; q++) { var e = p + BoardSpace.DirOf(q * 60f) * footprint; if (board.Topology.HexAt(e.x, e.z) < 0) ok = false; }
                    if (!ok || CampLayout.Obstruction(board, p, footprint) != null) continue;
                    foreach (var t in _taken) if (BoardSpace.Flat(t - p) < footprint + t.y + 1f) { ok = false; break; }
                    if (!ok) continue;
                    foreach (var t in _templeAt) if (BoardSpace.Flat(t - p) < TempleView.Radius + footprint + 1.5f) { ok = false; break; }
                    if (!ok) continue;
                    for (int n = 1; n <= PlagueGraphData.NodeCount && ok; n++) if (BoardSpace.Flat(NodePos(n) - p) < footprint + 6f) ok = false;
                    if (!ok) continue;
                    _taken.Add(new Vector3(p.x, footprint, p.z));
                    at = p; yaw = BoardSpace.YawOf(c - p); return true;
                }
            at = c; yaw = 0; return false;
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (W == null) return;
            var d = GodBoardDirector.Instance;
            Elevated = (d != null && d.View != BoardView.Altar) || (Boot.Views != null && Boot.Views.CurrentView == ViewMode.God);
            SyncSoldiers();
            SyncSites();
            for (int i = _dying.Count - 1; i >= 0; i--)
                if (_dying[i] == null || _dying[i].Finished) { if (_dying[i] != null) Destroy(_dying[i].gameObject); _dying.RemoveAt(i); }
        }

        private readonly Dictionary<int, int> _slotCount = new Dictionary<int, int>();

        private void SyncSoldiers()
        {
            var w = W;
            // drop views whose soldier left the state without a death (withdrawn from a lost settlement)
            List<int> gone = null;
            foreach (var kv in _views) if (WarApi.SoldierById(S, kv.Key) == null) (gone ?? (gone = new List<int>())).Add(kv.Key);
            if (gone != null) foreach (int id in gone) { var v = _views[id]; _views.Remove(id); if (v != null) { v.Die(); _dying.Add(v); } }

            _slotCount.Clear();
            var order = new List<Soldier>(w.Soldiers);
            order.Sort((a, b) => a.Id.CompareTo(b.Id));
            // how many stand at each place (for the ring)
            var total = new Dictionary<int, int>();
            foreach (var x in order) { int key = KeyOf(x); if (key != 0) total[key] = total.TryGetValue(key, out int n) ? n + 1 : 1; }
            foreach (var x in order)
            {
                if (!_views.TryGetValue(x.Id, out var v))
                {
                    v = SoldierView.Create(_soldiers, x, PeopleColour(x.People), WarApi.LookOf(x));
                    _views[x.Id] = v;
                    v.SnapTo(PositionOf(x, v, 0, 1, out _));
                    if (_fresh.Remove(x.Id)) v.Play(SoldierAction.Cheer);
                }
                int key = KeyOf(x);
                int slot = 0, cnt = 1;
                if (key != 0) { slot = _slotCount.TryGetValue(key, out int sc) ? sc : 0; _slotCount[key] = slot + 1; cnt = total[key]; }
                bool moving;
                var pos = PositionOf(x, v, slot, cnt, out moving);
                v.SimMoving = moving;
                v.SetWear(WarApi.WearOf(x));
                v.ShowMarker = Elevated;
                v.PurgeLoop = x.PurgeLeft > 0 && x.DuelId == 0 && !x.Moving;
                v.FaceYaw = float.NaN;
                if (x.DuelId != 0)
                {
                    var duel = w.Duels.Find(q => q.Id == x.DuelId);
                    if (duel != null)
                    {
                        int other = duel.A == x.Id ? duel.B : duel.A;
                        var ov = ViewOf(other);
                        var axis = BoardSpace.DirOf(duel.Id * 47f);
                        var centre = CentreOf(x);
                        pos = centre + axis * (x.Id < other ? -1.3f : 1.3f);
                        v.FaceYaw = BoardSpace.YawOf(axis * (x.Id < other ? 1f : -1f));
                    }
                }
                else if (v.PurgeLoop && x.Node > 0) v.FaceYaw = BoardSpace.YawOf(NodePos(x.Node) - pos);
                else if (!moving && x.Corner >= 0) { var c = CornerPos(x.Corner); var o = pos - c; o.y = 0; if (o.sqrMagnitude > 0.01f) v.FaceYaw = BoardSpace.YawOf(o); }
                v.Target = pos;
            }
        }

        private static int KeyOf(Soldier x) => x.Moving ? 0 : x.Node > 0 ? x.Node : x.Corner >= 0 ? -(x.Corner + 1) : 0;

        private Vector3 CentreOf(Soldier x) => x.Node > 0 ? NodePos(x.Node) : x.Corner >= 0 ? CornerPos(x.Corner) : Vector3.zero;

        private Vector3 PositionOf(Soldier x, SoldierView v, int slot, int count, out bool moving)
        {
            moving = false;
            if (x.Moving && x.Path.Count > 0)
            {
                moving = true;
                var to = StepPos(x.Path[0]);
                var from = _stepFrom.TryGetValue(x.Id, out var f) ? f : (v != null ? v.transform.position : to);
                float u = 1f - Mathf.Clamp01(x.MoveLeft / (float)WarData.MoveTicks);
                return Vector3.Lerp(from, to, u);
            }
            var c = CentreOf(x);
            if (x.Node <= 0 && x.Corner < 0) return v != null ? v.transform.position : c;
            float r = x.Node > 0 ? 3.2f : 6.5f;
            float a = (slot + (x.Node > 0 ? 0.5f : 0f)) * Mathf.PI * 2f / Mathf.Max(3, count) + (x.Node > 0 ? 0f : 0.6f);
            return c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r;
        }

        // ---------------------------------------------------------------- sites

        private bool EconomyDraws(int corner)
        {
            var e = S.Economy;
            if (e == null) return false;
            foreach (var st in e.Settlements) if (st.Corner == corner) return true;
            return false;
        }

        private SiteView EnsureSite(int corner, int people, bool home, bool city)
        {
            if (_sites.TryGetValue(corner, out var s)) return s;
            s = SiteView.Create(this, _siteRoot, CornerPos(corner), corner, people, PeopleColour(people), home, city, !EconomyDraws(corner));
            _sites[corner] = s;
            return s;
        }

        private void SyncSites()
        {
            var w = W;
            foreach (var st in w.Settlements)
            {
                var site = EnsureSite(st.Corner, st.People, st.IsHome, st.IsCity);
                if (site.State == SiteState.Standing) site.SyncStanding(S, st, WarApi.LodgeOf(S, st.People) is Lodge lg && lg.Corner == st.Corner ? lg : null, _siteRoot);
            }
            foreach (var r in w.Ruins)
            {
                var site = EnsureSite(r.Corner, r.People, false, false);
                if (site.State == SiteState.Standing && WarApi.SettlementAt(S, r.Corner) == null) { site.BeginBurn(true); BurnsPlayed++; }
            }
            List<int> drop = null;
            foreach (var kv in _sites)
            {
                bool has = WarApi.SettlementAt(S, kv.Key) != null, ruin = WarApi.IsRuin(S, kv.Key);
                if (kv.Value.State == SiteState.Standing ? !has && !ruin : !ruin) (drop ?? (drop = new List<int>())).Add(kv.Key);
            }
            if (drop != null) foreach (int c in drop) { if (_sites[c] != null) { ReleaseSpots(_sites[c]); Destroy(_sites[c].gameObject); } _sites.Remove(c); }
        }

        private void ReleaseSpots(SiteView s)
        {
            var c = s.transform.position;
            _taken.RemoveAll(t => BoardSpace.Flat(t - c) < 55f);
            // other sites keep theirs: put them back
            foreach (var o in _sites.Values)
            {
                if (o == s) continue;
                foreach (var h in o.Huts.Values) if (h != null) _taken.Add(new Vector3(h.transform.position.x, 2.9f, h.transform.position.z));
                if (o.Lodge != null) _taken.Add(new Vector3(o.Lodge.transform.position.x, 6.5f, o.Lodge.transform.position.z));
            }
        }

        // ---------------------------------------------------------------- events

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            if (W == null) return;
            foreach (var e in events)
            {
                switch (e)
                {
                    case SoldierTrained tr:
                        TrainedMoments++; _fresh.Add(tr.Soldier);
                        { var site = SiteOf(tr.Corner); if (site != null && site.Lodge != null) site.Lodge.Celebrate(PeopleColour(tr.People)); }
                        break;
                    case SoldierStepped st:
                        _stepFrom[st.Soldier] = StepPos(st.From);
                        break;
                    case DuelStarted ds:
                        DuelsStarted++;
                        break;
                    case DuelSwing sw:
                    {
                        var a = ViewOf(sw.Attacker); var t = ViewOf(sw.Target);
                        if (a != null) { a.Play(SoldierAction.Swing); SwingsPlayed++; }
                        if (t != null)
                        {
                            t.SetWear(sw.TargetWear);
                            bool shielded = t.HasShield && !t.ShieldDropped && t.Levels[Stat.Defence] >= 1 && t.Wear == Wear.Fresh;
                            // The blow lands about half-way through the swing.
                            float delay = 0.3f * (a != null ? a.SlowFactor : 1f);
                            t.Play(!sw.Hit ? SoldierAction.Dodge : shielded ? SoldierAction.Block : SoldierAction.Hit, delay);
                        }
                        break;
                    }
                    case SoldierDied dd:
                    {
                        if (_views.TryGetValue(dd.Soldier, out var v))
                        {
                            _views.Remove(dd.Soldier);
                            v.Die(); _dying.Add(v); DeathsPlayed++;
                        }
                        var k = ViewOf(dd.Killer);
                        if (k != null) k.Play(SoldierAction.Cheer, 0.6f);
                        _stepFrom.Remove(dd.Soldier);
                        break;
                    }
                    case DemonPurged dp:
                    {
                        var v = ViewOf(dp.Soldier);
                        if (v != null) { v.Play(SoldierAction.Purge); PurgeActions++; }
                        break;
                    }
                    case GoodProcessed gp:
                    {
                        var site = SiteOf(gp.Corner);
                        if (site != null && site.Huts.TryGetValue(gp.Good, out var h)) { h.Pop(); HutsPopped++; }
                        break;
                    }
                    case SettlementBurned sb:
                    {
                        var site = EnsureSite(sb.Corner, sb.People, false, sb.WasCity);
                        if (site.State == SiteState.Standing) { site.BeginBurn(true); BurnsPlayed++; }
                        break;
                    }
                    case CampLooted cl:
                    {
                        var site = SiteOf(cl.Corner);
                        if (site != null) { site.Loot(); LootsPlayed++; }
                        break;
                    }
                }
            }
        }
    }
}
