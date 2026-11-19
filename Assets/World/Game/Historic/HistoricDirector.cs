using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Historic;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>
    /// The Historic age in the world (G1-8 view part, G4-3, G4-4). Listens to the sim's Historic events and snapshot and
    /// shows: the switch moment and the temples falling, a castle per people in three stages with scaffolding, the
    /// individual trees (growth stages, stumps), fields, quarry rock and clay heaps, the seven Historic buildings, and
    /// carriers walking each trip. It never writes sim state. Installed with the god board on a GodBoard game.
    /// Cost: one merged mesh per forest or field hex (rebuilt only when a slot changes), one small mesh per building,
    /// shared walker meshes per people and load; the snapshot is read four times a second.
    /// </summary>
    public sealed class HistoricDirector : MonoBehaviour
    {
        public static HistoricDirector Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        /// <summary>Seconds the board stays up with "The age has turned" before it closes by itself.</summary>
        public static float TurnMomentSeconds = 4.5f;
        public const float SyncInterval = 0.25f;
        /// <summary>Dev start "eve": seconds the clock stays paused after the scene loads, so the switch plays in front of the camera (captures set it very large).</summary>
        public static float EveHoldSeconds = 3f;
        private const string EvePause = "HistoricEve";
        private float _eveUntil = -1f;

        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;
        private HistoricState H => S.Historic;
        private HistoricLayout _layout;
        private Material _mat;
        private Transform _root;
        private float _syncAt, _leaveAt = -1f, _clock;
        private bool _built, _collapsePending, _collapsed;
        private bool _hidDressing;

        private sealed class HexView { public GameObject Trees, Fields, Rock, Clay; public int TreeSig = -1, FieldSig = -1, RockSig = -1, ClaySig = -1; }
        private readonly Dictionary<int, HexView> _hexes = new Dictionary<int, HexView>();

        private sealed class BuildingView { public GameObject Go; public Vector3 Pos; public float Yaw; public int Sig = -1; }
        private readonly Dictionary<int, BuildingView> _buildings = new Dictionary<int, BuildingView>();
        private readonly Dictionary<int, int> _perHex = new Dictionary<int, int>();

        private sealed class CastleViewData
        {
            public GameObject Root, Stage, Scaffold; public Vector3 Pos; public float Yaw; public Color Colour;
            public Mesh[] StageMesh = new Mesh[3], ScaffoldMesh = new Mesh[3];
            public int Sig = -1; public float Flash; public int ShownStage = -1;
        }
        private readonly Dictionary<int, CastleViewData> _castles = new Dictionary<int, CastleViewData>();

        public int CastleStagesShown(int people) => _castles.TryGetValue(people, out var c) ? c.ShownStage : -1;
        public bool ScaffoldShown(int people) => _castles.TryGetValue(people, out var c) && c.Scaffold != null && c.Scaffold.activeSelf;
        public int TreeMeshHexes { get { int n = 0; foreach (var h in _hexes.Values) if (h.Trees != null) n++; return n; } }
        public int BuildingViews => _buildings.Count;
        public int WalkersShown { get { int n = 0; foreach (var w in _walkers.Values) if (w.Go.activeSelf) n++; return n; } }
        public int DustPuffs => _dust.Active;
        public bool TemplesDown => _collapsed;
        public bool CollapseWaiting => _collapsePending;

        // ---------------------------------------------------------------- install

        public static HistoricDirector Install(WorldBootstrap boot)
        {
            var go = new GameObject("Historic");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<HistoricDirector>();
            d.Init(boot);
            return d;
        }

        private void Init(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            _layout = new HistoricLayout(boot.Sim.State.Board);
            _mat = MaterialFactory.CreateVertexColored(Color.white, "Historic");
            _root = new GameObject("HistoricWorld").transform;
            _root.SetParent(transform, false);
            _dust = new HistoricDust(_root);
            boot.Clock.Ticked += OnTicked;
            if (DevStart.Current == GodBoardStart.EveId) { boot.Clock.Pause(EvePause); _eveUntil = Time.unscaledTime + EveHoldSeconds; }
            _collapsed = boot.Sim.State.Historic != null; // a loaded Historic game has no temples (the god board skips them)
        }

        private void OnDestroy()
        {
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            if (Instance == this) Instance = null;
            if (_mat != null) Destroy(_mat);
        }

        // ---------------------------------------------------------------- events

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case HistoricBegan hb: OnBegan(hb); break;
                    case TemplesTornDown _: _collapsePending = true; break;
                    case CastleStageRaised cr: OnStageRaised(cr); break;
                    case TreeFelled tf: OnFelled(tf); break;
                    case TreePlanted tp: OnPlanted(tp); break;
                    case QuarryExhausted _: _syncAt = 0f; break;
                    case HistoricBuildingFinished _: _syncAt = 0f; break;
                    case HistoricSitePlaced _: _syncAt = 0f; break;
                }
            }
        }

        private string WinnerName(int p)
        {
            foreach (var pe in S.Peoples)
                if (pe.Id == p) return p == PlayerIds.Human ? "Your people" : char.ToUpper(pe.Name.StartsWith("the ") ? pe.Name[4] : pe.Name[0]) + (pe.Name.StartsWith("the ") ? pe.Name.Substring(5) : pe.Name.Substring(1));
            return "A people";
        }

        private void OnBegan(HistoricBegan hb)
        {
            var gb = GodBoardDirector.Instance;
            string who = WinnerName(hb.Winner);
            if (gb != null && gb.IsOpen) _leaveAt = Time.unscaledTime + TurnMomentSeconds; // the G1-6 panel is the moment, then the world
            else Hud.Notice("The age has turned. " + who + " reached ten points. Temples fall; castles rise.", false, 6f);
            _syncAt = 0f;
        }

        private void OnStageRaised(CastleStageRaised cr)
        {
            if (_castles.TryGetValue(cr.People, out var c))
            {
                _dust.Burst(c.Pos + Vector3.up * 1f, 14, 6.5f, 1.4f);
                c.Flash = 1.5f;
            }
            _syncAt = 0f;
        }

        // ---------------------------------------------------------------- helpers

        private Color PeopleColour(int p)
        {
            foreach (var pe in S.Peoples) if (pe.Id == p) return TempleView.PeopleColour(pe);
            return Color.white;
        }

        private HexView HexOf(int hex)
        {
            if (!_hexes.TryGetValue(hex, out var v)) { v = new HexView(); _hexes[hex] = v; }
            return v;
        }

        private GameObject Obj(string name, Transform parent, Mesh m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = _mat;
            go.layer = 2;
            return go;
        }

        private static void SetMesh(ref GameObject go, string name, Transform parent, MeshBuilder mb, System.Func<string, Transform, Mesh, GameObject> make)
        {
            if (go != null) { var old = go.GetComponent<MeshFilter>().sharedMesh; go.GetComponent<MeshFilter>().sharedMesh = mb.ToMesh(name); if (old != null) Destroy(old); }
            else go = make(name, parent, mb.ToMesh(name));
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>Ends the eve hold: the clock runs and the age turns.</summary>
        public void ReleaseEve() { _eveUntil = -1f; _boot.Clock.Resume(EvePause); }

        // Frame-time probe (G4-3 budget): the last 600 frame times, read by tests and the capture driver.
        private readonly float[] _ft = new float[600];
        private int _ftN;
        public void ResetFrameStats() { _ftN = 0; }
        public string FrameStats()
        {
            int n = Mathf.Min(_ftN, _ft.Length);
            if (n == 0) return "no frames";
            var a = new float[n]; System.Array.Copy(_ft, a, n); System.Array.Sort(a);
            float sum = 0f; foreach (float f in a) sum += f;
            return "frames=" + n + " res=" + Screen.width + "x" + Screen.height + " avg=" + (1000f * sum / n).ToString("0.00") + "ms p95=" + (1000f * a[(int)(n * 0.95f)]).ToString("0.00") + "ms max=" + (1000f * a[n - 1]).ToString("0.0") + "ms";
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            _ft[_ftN % _ft.Length] = Time.unscaledDeltaTime; _ftN++;
            if (_eveUntil > 0f && Time.unscaledTime >= _eveUntil) ReleaseEve();
            if (S.Historic == null) return;
            if (_leaveAt > 0f && Time.unscaledTime >= _leaveAt)
            {
                _leaveAt = -1f;
                var gb = GodBoardDirector.Instance;
                if (gb != null && gb.IsOpen) gb.Leave();
            }
            if (_collapsePending)
            {
                var gb = GodBoardDirector.Instance;
                if (gb == null || gb.View == BoardView.Altar) CollapseTemples();
            }
            if (!_built) { HideDressing(); _built = true; _syncAt = 0f; }
            if (Time.unscaledTime >= _syncAt) { _syncAt = Time.unscaledTime + SyncInterval; SyncAll(); }
            UpdateCastleFlash();
            UpdateCarriers();
            UpdateActors();
            _dust.Update(Time.deltaTime);
        }

        private void CollapseTemples()
        {
            _collapsePending = false; _collapsed = true;
            var gb = GodBoardDirector.Instance;
            if (gb == null) return;
            foreach (var t in gb.Temples) t.Collapse();
        }

        private void HideDressing()
        {
            if (_hidDressing) return;
            _hidDressing = true;
            for (int h = 0; h < H.Hexes.Length; h++)
            {
                var hr = H.Hexes[h];
                if (hr == null) continue;
                if (hr.Terrain == Terrain.Forest || hr.Terrain == Terrain.Fields)
                {
                    var go = TerrainBuilder.HexProps(h);
                    if (go != null) go.SetActive(false);
                }
            }
        }

        /// <summary>Hex props of the Forest and Fields are replaced; restoring them is only needed when this object goes (scene reload rebuilds).</summary>
        public void SyncNow() { if (S.Historic != null) { HideDressing(); SyncAll(); } }

        private void SyncAll()
        {
            SyncGround();
            SyncBuildings();
            SyncCastles();
        }

        // ---------------------------------------------------------------- ground: trees, fields, rock, clay

        private void SyncGround()
        {
            var h = H;
            for (int i = 0; i < h.Hexes.Length; i++)
            {
                var hr = h.Hexes[i];
                if (hr == null) continue;
                var view = HexOf(i);
                if (hr.Trees.Length > 0) SyncTrees(i, hr, view);
                if (hr.Fields.Length > 0) SyncFields(i, hr, view);
                if (hr.Terrain == Terrain.Mountain || hr.Terrain == Terrain.Hills) SyncHeaps(i, hr, view);
            }
        }

        private void SyncTrees(int hex, HexResources hr, HexView view)
        {
            int sig = 17;
            long tick = S.Tick; int rot = H.Tuning.StumpRotTicks;
            for (int s = 0; s < hr.Trees.Length; s++)
            {
                int st = hr.Trees[s];
                int q = 0;
                if (st == (int)TreeStage.Stump) q = 1 + Mathf.Clamp((int)(3L * (hr.TreeNext[s] - tick) / System.Math.Max(1, rot)), 0, 2);
                sig = sig * 31 + st * 4 + q;
            }
            if (sig == view.TreeSig) return;
            view.TreeSig = sig;
            var sp = _layout.Of(hex);
            var mb = new MeshBuilder();
            for (int s = 0; s < hr.Trees.Length && s < sp.Trees.Length; s++)
            {
                var st = (TreeStage)hr.Trees[s];
                if (st == TreeStage.Empty) continue;
                float life = st == TreeStage.Stump ? Mathf.Clamp01((float)(hr.TreeNext[s] - tick) / Mathf.Max(1, rot)) : 1f;
                HistoricArt.TreeAt(mb, sp.Trees[s], st, life, new PropRandom(hex * 977 + s));
            }
            SetMesh(ref view.Trees, "Trees " + hex, _root, mb, Obj);
        }

        private void SyncFields(int hex, HexResources hr, HexView view)
        {
            int sig = 5;
            for (int s = 0; s < hr.Fields.Length; s++) sig = sig * 7 + hr.Fields[s] + 1;
            if (sig == view.FieldSig) return;
            view.FieldSig = sig;
            var sp = _layout.Of(hex);
            var mb = new MeshBuilder();
            for (int s = 0; s < hr.Fields.Length && s < sp.Fields.Length; s++) HistoricArt.FieldPlot(mb, sp.Fields[s], (FieldStage)hr.Fields[s], (hex * 37 + s * 23) % 4 * 15f);
            SetMesh(ref view.Fields, "Fields " + hex, _root, mb, Obj);
        }

        private int RockMax(HexResources hr)
        {
            var t = H.Tuning;
            return hr.Terrain == Terrain.Mountain ? t.MountainRockBase + t.MountainRockPerPip * hr.Pips : t.HillsRockBase + t.HillsRockPerPip * hr.Pips;
        }

        private void SyncHeaps(int hex, HexResources hr, HexView view)
        {
            int max = Mathf.Max(1, RockMax(hr));
            int boulders = hr.Rock <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(14f * hr.Rock / max), 1, 14);
            int clayMax = Mathf.Max(1, hr.ClayCap);
            int lumps = hr.Terrain != Terrain.Hills || hr.Clay <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(8f * hr.Clay / clayMax), 1, 8);
            if (boulders * 100 + lumps != view.RockSig)
            {
                view.RockSig = boulders * 100 + lumps;
                var sp = _layout.Of(hex);
                var mb = new MeshBuilder();
                var rnd = new PropRandom(hex * 311 + 9);
                var col = hr.Terrain == Terrain.Mountain ? new Color(0.50f, 0.49f, 0.50f) : new Color(0.58f, 0.54f, 0.48f);
                for (int i = 0; i < boulders; i++)
                {
                    float a = i * 137.5f, r = 0.8f + (i % 5) * 0.9f + rnd.Range(0f, 0.6f);
                    float size = 1.5f + 1.7f * (1f - i / 14f);
                    mb.Rock(sp.Rock + BoardSpace.DirOf(a) * r + Vector3.up * (i > 8 ? 0.9f : 0f), size, col * rnd.Range(0.9f, 1.1f), rnd.Next);
                }
                for (int i = 0; i < lumps; i++)
                {
                    float a = i * 137.5f, r = 0.5f + (i % 4) * 0.8f;
                    mb.Cylinder(sp.Clay + BoardSpace.DirOf(a) * r, 1.2f, 0.5f, 0.9f, 7, new Color(0.70f, 0.40f, 0.24f) * rnd.Range(0.92f, 1.05f));
                }
                SetMesh(ref view.Rock, "Heaps " + hex, _root, mb, Obj);
            }
        }

        // ---------------------------------------------------------------- buildings

        private void SyncBuildings()
        {
            var h = H;
            for (int i = 0; i < h.Buildings.Count; i++)
            {
                var b = h.Buildings[i];
                if (!_buildings.TryGetValue(b.Id, out var v))
                {
                    _perHex.TryGetValue(b.Hex, out int k);
                    _perHex[b.Hex] = k + 1;
                    var sp = _layout.Of(b.Hex);
                    v = new BuildingView { Pos = sp.Build[k % 3], Yaw = sp.BuildYaw[k % 3] };
                    _buildings[b.Id] = v;
                    v.Go = new GameObject("Building " + b.Id + " " + b.Kind);
                    v.Go.transform.SetParent(_root, false);
                    v.Go.transform.SetPositionAndRotation(v.Pos, Quaternion.Euler(0, v.Yaw, 0));
                }
                var def = HistoricDefs.Get(b.Kind);
                float prog = 0f; int sig;
                if (b.State == HistoricBuildingState.Site)
                {
                    int need = 0, have = 0;
                    for (int w = 0; w < Wares.Count; w++) { need += def.Cost[w]; have += Mathf.Min(b.Delivered[w], def.Cost[w]); }
                    prog = 0.3f * (need > 0 ? (float)have / need : 1f) + 0.7f * Mathf.Clamp01((float)b.BuildProgress / Mathf.Max(1, def.BuildTicks));
                    sig = 100 + Mathf.RoundToInt(prog * 8f);
                }
                else sig = b.State == HistoricBuildingState.Exhausted ? 2 : 1;
                if (sig == v.Sig) continue;
                v.Sig = sig;
                var mb = new MeshBuilder();
                var pc = PeopleColour(b.People);
                if (b.State == HistoricBuildingState.Site) HistoricArt.Site(mb, b.Kind, pc, prog);
                else HistoricArt.Building(mb, b.Kind, pc, b.State == HistoricBuildingState.Exhausted);
                var mf = v.Go.GetComponent<MeshFilter>();
                if (mf == null) { v.Go.AddComponent<MeshFilter>(); v.Go.AddComponent<MeshRenderer>().sharedMaterial = _mat; v.Go.layer = 2; mf = v.Go.GetComponent<MeshFilter>(); }
                var old = mf.sharedMesh; mf.sharedMesh = mb.ToMesh("B" + b.Id); if (old != null) Destroy(old);
            }
        }

        /// <summary>World position of a Historic building's spot, or the hex centre when the view is not built yet.</summary>
        public Vector3 BuildingPos(int id) => _buildings.TryGetValue(id, out var v) ? v.Pos : Vector3.zero;

        // ---------------------------------------------------------------- castles

        private void EnsureCastle(HistoricPeople hp)
        {
            if (_castles.ContainsKey(hp.Id)) return;
            var gb = GodBoardDirector.Instance;
            var board = S.Board;
            Vector3 at; float yaw;
            var temple = gb != null ? gb.TempleOf(hp.Id) : null;
            if (temple != null && hp.Castle.Corner == board.StartCorners[hp.Id < board.StartCorners.Length ? hp.Id : 0]) { at = temple.transform.position; yaw = temple.transform.eulerAngles.y; }
            else if (!TempleSites.Find(board, hp.Castle.Corner, null, out at, out yaw)) { at = BoardSpace.Vertex(board.Topology, hp.Castle.Corner); yaw = 0f; }
            var c = new CastleViewData { Pos = at, Yaw = yaw, Colour = PeopleColour(hp.Id) };
            c.Root = new GameObject("Castle " + hp.Id);
            c.Root.transform.SetParent(_root, false);
            c.Root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            LandViews.AddClearing(at.x, at.z, HistoricArt.CastleRadius + 2f);
            WalkGrid.AddSolid(at, HistoricArt.CastleRadius * 0.8f, "castles");
            _castles[hp.Id] = c;
        }

        private Mesh StageMesh(CastleViewData c, int stage)
        {
            if (c.StageMesh[stage] == null) { var mb = new MeshBuilder(); HistoricArt.Castle(mb, stage, c.Colour); c.StageMesh[stage] = mb.ToMesh("Castle" + stage); }
            return c.StageMesh[stage];
        }

        private Mesh ScaffoldMesh(CastleViewData c, int stage)
        {
            if (c.ScaffoldMesh[stage] == null) { var mb = new MeshBuilder(); HistoricArt.Scaffold(mb, HistoricArt.StageHeight[stage]); c.ScaffoldMesh[stage] = mb.ToMesh("Scaffold" + stage); }
            return c.ScaffoldMesh[stage];
        }

        private void SyncCastles()
        {
            foreach (var hp in H.Peoples)
            {
                EnsureCastle(hp);
                var c = _castles[hp.Id];
                var cs = hp.Castle;
                int done = Mathf.Clamp(cs.StagesDone, 0, 3);
                int show = -1; float scale = 1f; bool scaffold = false; int scaffoldStage = 0;
                if (done >= 3) show = 2;
                else
                {
                    var cost = H.Tuning.CastleCost[done];
                    int need = 0, have = 0;
                    for (int w = 0; w < Wares.Count; w++) { need += cost[w]; have += Mathf.Min(cs.Delivered[w], cost[w]); }
                    float p = 0.35f * (need > 0 ? (float)have / need : 1f) + 0.65f * Mathf.Clamp01((float)cs.BuildProgress / Mathf.Max(1, H.Tuning.CastleStageTicks));
                    scaffold = true; scaffoldStage = done;
                    if (done > 0 && p < 0.35f) show = done - 1;
                    else { show = done; scale = Mathf.Lerp(0.12f, 1f, done > 0 ? (p - 0.35f) / 0.65f : p); }
                    scale = Mathf.Round(scale * 16f) / 16f;
                }
                int sig = (show + 1) * 1000 + Mathf.RoundToInt(scale * 16f) * 10 + (scaffold ? 1 + scaffoldStage : 0) * 100000;
                if (sig == c.Sig) continue;
                c.Sig = sig;
                c.ShownStage = show;
                if (show >= 0)
                {
                    if (c.Stage == null) c.Stage = Obj("Stage", c.Root.transform, StageMesh(c, show));
                    else c.Stage.GetComponent<MeshFilter>().sharedMesh = StageMesh(c, show);
                    c.Stage.SetActive(true);
                    c.Stage.transform.localScale = new Vector3(1f, scale, 1f);
                }
                else if (c.Stage != null) c.Stage.SetActive(false);
                if (scaffold)
                {
                    if (c.Scaffold == null) c.Scaffold = Obj("Scaffold", c.Root.transform, ScaffoldMesh(c, scaffoldStage));
                    else c.Scaffold.GetComponent<MeshFilter>().sharedMesh = ScaffoldMesh(c, scaffoldStage);
                    c.Scaffold.SetActive(true);
                }
                else if (c.Scaffold != null) c.Scaffold.SetActive(false);
            }
        }

        private void UpdateCastleFlash()
        {
            foreach (var c in _castles.Values)
            {
                if (c.Flash <= 0f || c.Stage == null) continue;
                c.Flash -= Time.deltaTime;
                float k = Mathf.Max(0f, c.Flash) / 1.5f;
                c.Stage.transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * 0.5f * k, 0);
                if (c.Flash <= 0f) c.Stage.transform.localPosition = Vector3.zero;
            }
        }

        // ---------------------------------------------------------------- tests and tools

        public Vector3 CastlePos(int people) => _castles.TryGetValue(people, out var c) ? c.Pos : Vector3.zero;
        public bool HexHasTreeMesh(int hex) => _hexes.TryGetValue(hex, out var v) && v.Trees != null && v.Trees.activeSelf;
        public bool ForestPropsHidden(int hex) { var go = TerrainBuilder.HexProps(hex); return go == null || !go.activeSelf; }

        private HistoricDust _dust;
        private readonly Dictionary<int, Walker> _walkers = new Dictionary<int, Walker>();
        private readonly Stack<Walker> _pool = new Stack<Walker>();
        private readonly Dictionary<int, Mesh> _walkerMeshes = new Dictionary<int, Mesh>();
        private readonly List<Actor> _actors = new List<Actor>();
        private Mesh _fallMesh;

        private sealed class Walker { public GameObject Go; public Transform T; public bool Seen; public int Key = -1; public float Id; }
        private sealed class Actor { public Walker W; public Vector3 A, B; public float T; public bool Fell; }

        private Mesh WalkerMesh(int people, int ware)
        {
            int key = people * 32 + (ware + 1);
            if (!_walkerMeshes.TryGetValue(key, out var m))
            {
                var mb = new MeshBuilder();
                var load = ware >= 0 ? HistoricArt.WareColour((Ware)ware) : new Color(0, 0, 0, 0);
                HistoricArt.Walker(mb, PeopleColour(people), load);
                m = mb.ToMesh("Walker" + key);
                _walkerMeshes[key] = m;
            }
            return m;
        }

        private Walker GetWalker(int people, int ware)
        {
            Walker w = _pool.Count > 0 ? _pool.Pop() : null;
            if (w == null)
            {
                w = new Walker();
                w.Go = Obj("Walker", _root, WalkerMesh(people, ware));
                w.T = w.Go.transform;
            }
            else w.Go.GetComponent<MeshFilter>().sharedMesh = WalkerMesh(people, ware);
            w.Go.SetActive(true);
            return w;
        }

        private void Release(Walker w) { w.Go.SetActive(false); _pool.Push(w); }

        private Vector3 EndPos(CarryEnd end, int id)
        {
            if (end == CarryEnd.Building) return BuildingPos(id);
            return _castles.TryGetValue(id, out var c) ? c.Pos + c.Root.transform.rotation * new Vector3(0, 0, HistoricArt.CastleRadius * 0.9f) : Vector3.zero;
        }

        private readonly List<int> _gone = new List<int>();

        private void UpdateCarriers()
        {
            foreach (var w in _walkers.Values) w.Seen = false;
            var jobs = H.Jobs;
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                var a = EndPos(j.From, j.FromId); var b = EndPos(j.To, j.ToId);
                if (a == Vector3.zero || b == Vector3.zero) continue;
                float u = Mathf.Clamp01((j.Progress + 0.5f) / Mathf.Max(1, j.Ticks));
                int key = j.Id;
                if (!_walkers.TryGetValue(key, out var w))
                {
                    w = GetWalker(j.People, (int)j.Ware); w.Id = j.Id; _walkers[key] = w;
                    w.T.position = Vector3.Lerp(a, b, u);
                }
                w.Seen = true;
                // The job's own lane: a small sideways offset so a stream of carriers is not a single line.
                var dir = b - a; dir.y = 0;
                var side = dir.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, dir.normalized) : Vector3.right;
                var target = Vector3.Lerp(a, b, u) + side * (((j.Id % 5) - 2) * 0.6f);
                var cur = w.T.position;
                cur = Vector3.Lerp(cur, target, 1f - Mathf.Exp(-9f * Time.deltaTime));
                cur.y = Mathf.Abs(Mathf.Sin(_clock * 9f + w.Id)) * 0.12f;
                if (dir.sqrMagnitude > 0.01f) w.T.rotation = Quaternion.Slerp(w.T.rotation, Quaternion.LookRotation(dir, Vector3.up), 0.2f);
                w.T.position = cur;
            }
            _gone.Clear();
            foreach (var kv in _walkers) if (!kv.Value.Seen) _gone.Add(kv.Key);
            foreach (int k in _gone) { Release(_walkers[k]); _walkers.Remove(k); }
        }

        // ---------------------------------------------------------------- felling and planting actors

        private void OnFelled(TreeFelled tf) { StartActor(tf.People, tf.BuildingId, tf.Hex, tf.Slot, true); }
        private void OnPlanted(TreePlanted tp) { StartActor(tp.People, tp.BuildingId, tp.Hex, tp.Slot, false); }

        private void StartActor(int people, int building, int hex, int slot, bool fell)
        {
            if (_actors.Count >= 10 || _layout == null) return;
            var sp = _layout.Of(hex);
            if (slot < 0 || slot >= sp.Trees.Length) return;
            var from = _buildings.TryGetValue(building, out var bv) ? bv.Pos : sp.Centre;
            var w = GetWalker(people, fell ? (int)Ware.Wood : -1);
            w.T.position = from;
            _actors.Add(new Actor { W = w, A = from, B = sp.Trees[slot], Fell = fell });
            if (fell) SpawnFaller(sp.Trees[slot]);
        }

        private readonly List<KeyValuePair<Transform, float>> _fallers = new List<KeyValuePair<Transform, float>>();

        private void SpawnFaller(Vector3 at)
        {
            if (_fallMesh == null) { var mb = new MeshBuilder(); TerrainBuilder.Tree(mb, Vector3.zero, new PropRandom(5), 1.25f); _fallMesh = mb.ToMesh("FallingTree"); }
            if (_fallers.Count > 8) return;
            var go = Obj("Falling tree", _root, _fallMesh);
            go.transform.position = at;
            _fallers.Add(new KeyValuePair<Transform, float>(go.transform, 0f));
        }

        private void UpdateActors()
        {
            for (int i = _actors.Count - 1; i >= 0; i--)
            {
                var a = _actors[i];
                a.T += Time.deltaTime;
                float t = a.T;
                Vector3 p;
                if (t < 1.2f) p = Vector3.Lerp(a.A, a.B, t / 1.2f);
                else if (t < 1.8f) p = a.B + Vector3.down * 0.15f * Mathf.Sin((t - 1.2f) / 0.6f * Mathf.PI);
                else p = Vector3.Lerp(a.B, a.A, Mathf.Clamp01((t - 1.8f) / 1.2f));
                p.y += Mathf.Abs(Mathf.Sin(_clock * 9f + i)) * 0.1f;
                a.W.T.position = p;
                var d = (t < 1.8f ? a.B - a.A : a.A - a.B); d.y = 0;
                if (d.sqrMagnitude > 0.01f) a.W.T.rotation = Quaternion.LookRotation(d, Vector3.up);
                if (t >= 3f) { Release(a.W); _actors.RemoveAt(i); }
            }
            for (int i = _fallers.Count - 1; i >= 0; i--)
            {
                var kv = _fallers[i];
                float t = kv.Value + Time.deltaTime;
                if (kv.Key == null) { _fallers.RemoveAt(i); continue; }
                float k = Mathf.Clamp01(t / 1.3f);
                kv.Key.rotation = Quaternion.Euler(0, 0, -88f * k * k);
                if (t >= 1.5f) { Destroy(kv.Key.gameObject); _fallers.RemoveAt(i); continue; }
                _fallers[i] = new KeyValuePair<Transform, float>(kv.Key, t);
            }
        }
    }
}
