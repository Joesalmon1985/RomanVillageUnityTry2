using System.Collections.Generic;
using Bedsit;
using UnityEngine;
using World.Sim.Encounters.Ward;

namespace World.Game
{
    /// <summary>What a tap in the duel hit (WU-7). Stone, Socket and the tallies carry an index.</summary>
    public enum DuelTarget { None, Stone, Socket, Hand, Caster, Referee, TallyCaster, TallyOpponent, BeadsCaster, BeadsOpponent, Gate }

    /// <summary>
    /// The duel's world objects (WORLD_UI_PLAN 2.7, WU-7), built by <see cref="WardDuelEncounter"/> for the duel and removed
    /// with it. Essence stones on a cord, a gate and the hand (three sockets for the orbs and a palm to cast) hang in the
    /// camera's bottom row inside the safe area (fixed to the view, sized from the screen height so each is at least
    /// <see cref="WorldHitTarget.MinSide"/>); a post beside each wizard carries the beads (casts left) and a tally stick
    /// (history totals only: fractures, echoes, fades, never a locus); a referee stands between them; at the end both
    /// Wards are shown in the air. Reads the encounter and the session only; every command goes through the encounter's
    /// public methods (AddEssence, RemoveAt, LockWard, Cast, Rematch, Return, Surrender). Taps arrive as screen points
    /// (<see cref="TapAt"/>); casting, locking the Ward, leaving and the rematch use the WU-0 preview / confirm machine
    /// (a private instance: the shared one cancels while an encounter runs, see WU-5).
    /// </summary>
    public sealed class DuelWorldProps : MonoBehaviour
    {
        public static readonly Color FractureColour = new Color(1f, 0.55f, 0.35f), EchoColour = new Color(1f, 0.85f, 0.4f), FadeColour = new Color(0.62f, 0.62f, 0.68f);
        private static readonly Color Wood = new Color(0.45f, 0.32f, 0.2f), StoneCol = new Color(0.34f, 0.32f, 0.34f), Dark = new Color(0.12f, 0.1f, 0.12f);

        public WorldActionMachine Machine { get; } = new WorldActionMachine();
        public Transform Rig => _rig;
        public bool RigVisible { get; private set; }
        public bool HandPreviewed { get; private set; }
        public int StoneCount => _stones.Count;

        private WardDuelEncounter _e;
        private WardDuelStage _stage;
        private Camera _cam;
        private Transform _rig;
        private Material _unlit, _glow;
        private Material _beadLitC, _beadLitO, _beadDark, _pegDark, _pegF, _pegE, _pegD, _ringDim, _ringLit;

        private sealed class Stone { public Transform Root; public Transform Gem; public int Pool; }
        private readonly List<Stone> _stones = new List<Stone>();
        private Transform _gate, _palm, _palmFingers; private Material _palmGlowMat; private Transform _palmGlow;
        private readonly Transform[] _sockets = new Transform[3];
        private readonly Transform[] _socketOrbs = new Transform[3];
        private readonly Renderer[] _socketOrbR = new Renderer[3];
        private readonly Renderer[] _socketRingR = new Renderer[3];

        private sealed class Post
        {
            public Transform Root, Rail, Plank; public Renderer[] Beads; public Renderer[][] Pegs; public Color Accent;
        }
        private Post _casterPost, _oppPost;
        private ICharacterActor _referee;
        private Vector3 _refereeRest, _refereeStage;
        private readonly Transform[] _reveal = new Transform[6];
        private Transform _revealRoot;

        private HandAction _hand; private GateAction _gateAction; private RematchAction _rematch;
        private DuelInspect _refereeThing, _tallyC, _tallyO, _beadsC, _beadsO;
        private bool _resultShown, _setupShown, _wasVisible = true;
        private float _t;

        // ------------------------------------------------------------------ build

        public static DuelWorldProps Build(WardDuelEncounter e, Transform parent, Camera cam)
        {
            var go = new GameObject("DuelWorldProps");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<DuelWorldProps>();
            p._e = e; p._stage = e.Stage; p._cam = cam;
            p.BuildAll();
            return p;
        }

        private void BuildAll()
        {
            _unlit = MaterialFactory.CreateUnlit(Color.white, "Duel unlit");
            _glow = MaterialFactory.CreateGlow(Color.white, "Duel glow");
            _beadLitC = MaterialFactory.CreateUnlit(WardDuelStage.PlayerWardColor, "Bead lit caster");
            _beadLitO = MaterialFactory.CreateUnlit(_e.IsCampaignMode ? WardDuelStage.OpponentWardColor : WardDuelStage.MiraWardColor, "Bead lit opponent");
            _beadDark = MaterialFactory.CreateUnlit(new Color(0.16f, 0.13f, 0.14f), "Bead spent");
            _pegDark = MaterialFactory.CreateUnlit(new Color(0.2f, 0.16f, 0.14f), "Peg empty");
            _pegF = MaterialFactory.CreateUnlit(FractureColour, "Peg fracture");
            _pegE = MaterialFactory.CreateUnlit(EchoColour, "Peg echo");
            _pegD = MaterialFactory.CreateUnlit(FadeColour, "Peg fade");
            _ringDim = MaterialFactory.CreateUnlit(new Color(0.45f, 0.4f, 0.36f), "Socket ring");
            _ringLit = MaterialFactory.CreateUnlit(new Color(1f, 0.85f, 0.5f), "Socket ring lit");

            _rig = new GameObject("Duel rig").transform;
            _rig.SetParent(transform, false);
            BuildStones();
            BuildGate();
            BuildHand();
            BuildPosts();
            BuildReferee();
            BuildReveal();
            _hand = new HandAction(this); _gateAction = new GateAction(this); _rematch = new RematchAction(this);
            _refereeThing = new DuelInspect("The referee", RefereeLines);
            _tallyC = new DuelInspect(_e.CasterName + "'s tally stick", () => TallyLines(true));
            _tallyO = new DuelInspect(_e.OpponentName + "'s tally stick", () => TallyLines(false));
            _beadsC = new DuelInspect(_e.CasterName + "'s beads", () => BeadLines(true));
            _beadsO = new DuelInspect(_e.OpponentName + "'s beads", () => BeadLines(false));
            Machine.Changed += OnMachineChanged;
        }

        private GameObject Obj(string name, Transform parent, MeshBuilder mb, Material m) => mb.ToObject(name, parent, m);

        private static void Gem(MeshBuilder mb, Vector3 c, Vector3 r, Color col)
        {
            Vector3 px = c + Vector3.right * r.x, nx = c - Vector3.right * r.x, py = c + Vector3.up * r.y, ny = c - Vector3.up * r.y, pz = c + Vector3.forward * r.z, nz = c - Vector3.forward * r.z;
            mb.Tri(py, pz, px, col); mb.Tri(py, px, nz, col * 0.9f); mb.Tri(py, nz, nx, col * 0.8f); mb.Tri(py, nx, pz, col * 0.9f);
            mb.Tri(ny, px, pz, col * 0.7f); mb.Tri(ny, nz, px, col * 0.6f); mb.Tri(ny, nx, nz, col * 0.5f); mb.Tri(ny, pz, nx, col * 0.6f);
        }

        /// <summary>A flat disc in the XY plane (both faces).</summary>
        private static void Disc(MeshBuilder mb, Vector3 c, float r, int n, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * r; var p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * r;
                mb.Tri(c, p0, p1, col); mb.Tri(c, p1, p0, col);
            }
        }

        private static void RingXY(MeshBuilder mb, float r0, float r1, int n, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0);
                mb.Quad(d0 * r0, d0 * r1, d1 * r1, d1 * r0, col);
                mb.Quad(d0 * r0, d1 * r0, d1 * r1, d0 * r1, col);
            }
        }

        private static Vector3 GemShape(int essence)
        {
            switch ((Essence)essence)
            {
                case Essence.Flame: return new Vector3(0.28f, 0.5f, 0.28f);   // tall, pointed
                case Essence.Frost: return new Vector3(0.36f, 0.36f, 0.36f);  // regular crystal
                case Essence.Vine: return new Vector3(0.46f, 0.26f, 0.46f);   // wide and low
                default: return new Vector3(0.4f, 0.4f, 0.12f);               // light: a flat star
            }
        }

        private Transform MakeGem(Transform parent, int essence, float scale)
        {
            var c = EncounterUi.EssenceColor(essence);
            var root = new GameObject("Gem " + EssenceInfo.DisplayName((Essence)essence)).transform;
            root.SetParent(parent, false);
            var mb = new MeshBuilder();
            var shape = GemShape(essence);
            Gem(mb, Vector3.zero, shape, c);
            if ((Essence)essence == Essence.Vine) { Gem(mb, new Vector3(0.18f, 0.1f, 0), shape * 0.55f, c * 0.9f); Gem(mb, new Vector3(-0.18f, 0.1f, 0), shape * 0.55f, c * 0.9f); }
            if ((Essence)essence == Essence.Light) Gem(mb, Vector3.zero, new Vector3(0.12f, 0.4f, 0.4f), c);
            Obj("Body", root, mb, _unlit);
            var halo = new MeshBuilder();
            Gem(halo, Vector3.zero, shape * 1.55f, new Color(c.r, c.g, c.b, 0.22f));
            Obj("Halo", root, halo, _glow);
            root.localScale = Vector3.one * scale;
            return root;
        }

        private void BuildStones()
        {
            var cord = new MeshBuilder();
            cord.Box(Vector3.zero, Vector3.one, new Color(0.6f, 0.45f, 0.28f));
            var cordObj = Obj("Cord", _rig, cord, _unlit);
            cordObj.name = "Stone cord";
            for (int i = 0; i < 4 && i < _e.Pool.Length; i++)
            {
                var s = new Stone { Pool = (int)_e.Pool[i] };
                s.Root = new GameObject("DUEL-WEAVE stone " + EssenceInfo.DisplayName(_e.Pool[i])).transform;
                s.Root.SetParent(_rig, false);
                var back = new MeshBuilder();
                Disc(back, Vector3.zero, 0.5f, 8, StoneCol);
                Disc(back, new Vector3(0, 0, 0.02f), 0.4f, 8, new Color(0.2f, 0.19f, 0.22f));
                Obj("Setting", s.Root, back, _unlit);
                s.Gem = MakeGem(s.Root, s.Pool, 0.9f);
                _stones.Add(s);
            }
            _cord = cordObj.transform;
        }
        private Transform _cord;

        private void BuildGate()
        {
            _gate = new GameObject("DUEL-END gate").transform;
            _gate.SetParent(_rig, false);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(-0.42f, 0, 0), new Vector3(0.12f, 1f, 0.1f), Wood);
            mb.Box(new Vector3(0.42f, 0, 0), new Vector3(0.12f, 1f, 0.1f), Wood);
            mb.Box(new Vector3(0, 0.38f, 0), new Vector3(0.96f, 0.1f, 0.08f), Wood * 1.1f);
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.96f, 0.1f, 0.08f), Wood * 1.1f);
            mb.Box(new Vector3(0, -0.28f, 0), new Vector3(0.96f, 0.1f, 0.08f), Wood * 1.1f);
            Obj("Gate", _gate, mb, _unlit);
        }

        private void BuildHand()
        {
            _palm = new GameObject("DUEL-WEAVE hand").transform;
            _palm.SetParent(_rig, false);
            var mb = new MeshBuilder();
            Disc(mb, Vector3.zero, 0.5f, 14, new Color(0.62f, 0.48f, 0.4f));
            Disc(mb, new Vector3(0, 0, 0.01f), 0.4f, 14, new Color(0.46f, 0.34f, 0.3f));
            for (int f = 0; f < 5; f++)
            {
                float a = (35f + f * 27.5f) * Mathf.Deg2Rad;
                var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.62f;
                Disc(mb, c, 0.1f, 8, new Color(0.62f, 0.48f, 0.4f));
            }
            Obj("Palm", _palm, mb, _unlit);
            var gl = new MeshBuilder();
            Disc(gl, new Vector3(0, 0, -0.01f), 0.62f, 14, Color.white);
            _palmGlowMat = MaterialFactory.CreateGlow(new Color(1f, 0.85f, 0.4f, 0.0f), "Palm glow");
            _palmGlow = Obj("Glow", _palm, gl, _palmGlowMat).transform;
            for (int j = 0; j < 3; j++)
            {
                var s = new GameObject("DUEL-WEAVE socket " + j).transform;
                s.SetParent(_rig, false);
                var rm = new MeshBuilder();
                RingXY(rm, 0.38f, 0.5f, 12, Color.white);
                Disc(rm, new Vector3(0, 0, 0.02f), 0.38f, 12, new Color(0.08f, 0.07f, 0.1f));
                var ro = Obj("Ring", s, rm, _ringDim);
                _socketRingR[j] = ro.GetComponent<Renderer>();
                _sockets[j] = s;
                var orb = new GameObject("Orb").transform;
                orb.SetParent(s, false);
                _socketOrbs[j] = orb;
            }
        }

        private Post BuildPost(string name, Color accent, int beads)
        {
            var p = new Post { Accent = accent };
            p.Root = new GameObject(name).transform;
            p.Root.SetParent(transform, false);
            var pole = new MeshBuilder();
            pole.Box(new Vector3(0, 1.15f, 0.08f), new Vector3(0.1f, 2.3f, 0.1f), Wood);
            Obj("Pole", p.Root, pole, _unlit);
            var rail = new MeshBuilder();
            float span = beads * 0.17f + 0.2f;
            rail.Box(new Vector3(0, 2.32f, 0), new Vector3(span, 0.07f, 0.07f), Wood * 1.15f);
            p.Rail = Obj("Beads", p.Root, rail, _unlit).transform;
            var bm = new MeshBuilder();
            Gem(bm, Vector3.zero, new Vector3(0.075f, 0.085f, 0.075f), Color.white);
            var beadMesh = bm.ToMesh("Bead");
            p.Beads = new Renderer[beads];
            for (int i = 0; i < beads; i++)
            {
                var b = new GameObject("Bead " + i);
                b.transform.SetParent(p.Rail, false);
                b.transform.localPosition = new Vector3((i - (beads - 1) / 2f) * 0.17f, -0.12f, -0.02f);
                b.AddComponent<MeshFilter>().sharedMesh = beadMesh;
                p.Beads[i] = b.AddComponent<MeshRenderer>();
                p.Beads[i].sharedMaterial = _beadDark;
            }
            var plank = new MeshBuilder();
            plank.Box(new Vector3(0, 1.28f, 0.03f), new Vector3(0.62f, 1.72f, 0.05f), Wood * 1.25f);
            p.Plank = Obj("Tally", p.Root, plank, _unlit).transform;
            var pm = new MeshBuilder();
            pm.Box(Vector3.zero, new Vector3(0.14f, 0.09f, 0.05f), Color.white);
            var pegMesh = pm.ToMesh("Peg");
            int rows = Mathf.Max(1, _e.Tuning.MaxCasts);
            p.Pegs = new Renderer[rows][];
            for (int r = 0; r < rows; r++)
            {
                p.Pegs[r] = new Renderer[3];
                for (int k = 0; k < 3; k++)
                {
                    var g = new GameObject("Peg " + r + "." + k);
                    g.transform.SetParent(p.Plank, false);
                    g.transform.localPosition = new Vector3((k - 1) * 0.18f, 2.04f - r * 0.145f, -0.03f);
                    g.AddComponent<MeshFilter>().sharedMesh = pegMesh;
                    p.Pegs[r][k] = g.AddComponent<MeshRenderer>();
                    p.Pegs[r][k].sharedMaterial = _pegDark;
                }
            }
            return p;
        }

        private void BuildPosts()
        {
            int n = Mathf.Max(1, _e.Tuning.MaxCasts);
            _casterPost = BuildPost("DUEL-STATE caster post", WardDuelStage.PlayerWardColor, n);
            _oppPost = BuildPost("DUEL-STATE opponent post", _e.IsCampaignMode ? WardDuelStage.OpponentWardColor : WardDuelStage.MiraWardColor, n);
            _casterPost.Root.name = "DUEL-STATE caster post";
            WorldZone.Tag(_casterPost.Root.gameObject, WorldZones.DuelState);
            WorldZone.Tag(_oppPost.Root.gameObject, WorldZones.DuelState);
            PlacePosts();
        }

        private void FrameAxes(out Vector3 dir, out Vector3 right)
        {
            dir = _stage.MiraPos - _stage.PlayerPos; dir.y = 0;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            right = Vector3.Cross(Vector3.up, dir).normalized;
        }

        private void PlacePosts()
        {
            FrameAxes(out var dir, out var right);
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            _casterPost.Root.SetPositionAndRotation(_stage.PlayerPos - right * PostSide + dir * 0.2f, rot);
            _oppPost.Root.SetPositionAndRotation(_stage.MiraPos + right * PostSide - dir * 0.2f, rot);
        }

        /// <summary>Metres from each wizard to its post, along the screen's horizontal.</summary>
        public static float PostSide = 1.9f;

        private void BuildReferee()
        {
            FrameAxes(out var dir, out var right);
            var mid = (_stage.PlayerPos + _stage.MiraPos) * 0.5f;
            _refereeRest = mid + right * 2.6f; _refereeRest.y = _stage.PlayerPos.y;
            _refereeStage = mid + right * 0.5f; _refereeStage.y = _stage.PlayerPos.y;
            _referee = ActorFactory.Create("duel_referee", new Color(0.85f, 0.78f, 0.42f), _refereeRest, BoardSpace.YawOf(mid - _refereeRest), transform);
            _referee.Root.name = "DUEL-END referee";
            WorldZone.Tag(_referee.Root.gameObject, WorldZones.DuelEnd);
        }

        private void BuildReveal()
        {
            _revealRoot = new GameObject("Revealed Wards").transform;
            _revealRoot.SetParent(transform, false);
            for (int i = 0; i < 6; i++)
            {
                _reveal[i] = MakeGem(_revealRoot, (int)Essence.Frost, 0.5f);
                _reveal[i].name = "Revealed " + i;
            }
            _revealRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float dt) => Machine.Tick(Time.unscaledTime);

        private bool ViewOk()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return false;
            var vs = ViewSwitcher.Instance;
            return vs == null || vs.CurrentView != ViewMode.God;
        }

        /// <summary>Puts the rig on the camera as it is now (tests and the first frame call this before reading screen points).</summary>
        public void SyncToCamera()
        {
            if (_cam == null) _cam = _e != null && _e.Stage != null ? Camera.main : null;
            if (_cam == null || _rig == null) return;
            _rig.SetPositionAndRotation(_cam.transform.position, _cam.transform.rotation);
            _rig.localScale = Vector3.one;
            LayoutRig();
        }

        private void LateUpdate()
        {
            if (_e == null || _stage == null) return;
            _t += Time.unscaledDeltaTime;
            bool world = !GodBoardSettings.ClearView && ViewOk();
            if (world != _wasVisible)
            {
                _wasVisible = world;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch != _revealRoot) ch.gameObject.SetActive(world);
                }
                if (!world) { Machine.Cancel(); _revealRoot.gameObject.SetActive(false); }
            }
            RigVisible = world && _e.Phase != WardDuelEncounter.Screen.Stakes;
            if (!world) return;
            bool stakes = _e.Phase == WardDuelEncounter.Screen.Stakes;
            if (_rig != null) _rig.gameObject.SetActive(!stakes);
            SyncToCamera();
            PlacePosts();
            Refresh();
            AutoLedger();
        }

        private void AutoLedger()
        {
            var ph = _e.Phase;
            if (ph == WardDuelEncounter.Screen.Setup && _e.HelpVisible && !_setupShown)
            {
                if (Machine.Inspect(_refereeThing, Time.unscaledTime) == WorldTapResult.Inspected) _setupShown = true;
            }
            if (ph == WardDuelEncounter.Screen.Result && (!_e.IsCampaignMode || _e.HoldOver) && !_resultShown)
            {
                if (Machine.Inspect(_refereeThing, Time.unscaledTime) == WorldTapResult.Inspected) _resultShown = true;
            }
            if (ph != WardDuelEncounter.Screen.Result) _resultShown = false;
            if (ph == WardDuelEncounter.Screen.Stakes) _setupShown = false;
        }

        // ---- rig layout (camera space)

        private float HalfH => Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        private Vector3 RigPoint(float gx, float gy, float depth)
        {
            float W = Mathf.Max(1, _cam.pixelWidth), H = Mathf.Max(1, _cam.pixelHeight);
            float nx = gx / W * 2f - 1f, ny = 1f - gy / H * 2f;
            float hh = HalfH * depth, hw = hh * (W / H);
            return new Vector3(nx * hw, ny * hh, depth);
        }
        private float PxToWorld(float px, float depth) => px / Mathf.Max(1, _cam.pixelHeight) * 2f * HalfH * depth;

        public const float RigDepth = 1.3f;

        /// <summary>The bottom row's GUI-space centres (for tests and the layout): unit m = <see cref="WorldHitTarget.MinSide"/>.</summary>
        public void RowLayout(out float m, out float yC, out Rect safe)
        {
            float H = Mathf.Max(1, _cam != null ? _cam.pixelHeight : Screen.height);
            m = WorldHitTarget.MinSide(H);
            safe = ScreenSafeArea.Current();
            yC = safe.yMax - m * 0.62f - 6f;
        }

        private void Place(Transform t, float gx, float gy, float diameterPx)
        {
            t.localPosition = RigPoint(gx, gy, RigDepth);
            float s = PxToWorld(diameterPx, RigDepth);
            t.localScale = new Vector3(s, s, s);
            t.localRotation = Quaternion.identity;
        }

        private void LayoutRig()
        {
            if (_cam == null || _rig == null) return;
            RowLayout(out float m, out float yC, out Rect S);
            Place(_gate, S.x + 0.85f * m, yC - 0.05f * m, 1.1f * m);
            for (int i = 0; i < _stones.Count; i++)
                Place(_stones[i].Root, S.x + (2.35f + i * 1.3f) * m, yC, 1.1f * m);
            if (_stones.Count > 0)
            {
                float x0 = S.x + 2.35f * m, x1 = S.x + (2.35f + (_stones.Count - 1) * 1.3f) * m;
                _cord.localPosition = RigPoint((x0 + x1) / 2f, yC, RigDepth + 0.02f);
                float len = PxToWorld(x1 - x0 + 1.6f * m, RigDepth);
                _cord.localScale = new Vector3(len, PxToWorld(5f, RigDepth), PxToWorld(5f, RigDepth));
                _cord.localRotation = Quaternion.identity;
            }
            float palmX = S.xMax - 1.0f * m;
            Place(_palm, palmX, yC, 1.5f * m);
            for (int j = 0; j < 3; j++) Place(_sockets[j], palmX - 1.85f * m - (2 - j) * 1.2f * m, yC, 1.05f * m);
        }

        // ---- state

        private int[] _shownRevealPlayer, _shownRevealRival;

        private void Refresh()
        {
            var s = _e.Session;
            if (s == null) return;
            bool act = _e.Phase == WardDuelEncounter.Screen.Setup || _e.Phase == WardDuelEncounter.Screen.Active;
            bool res = _e.Phase == WardDuelEncounter.Screen.Result;
            _gate.gameObject.SetActive(true);
            for (int i = 0; i < _stones.Count; i++)
            {
                _stones[i].Root.gameObject.SetActive(act);
                _stones[i].Gem.localRotation = Quaternion.Euler(0, _t * 40f + i * 50f, 0);
            }
            _cord.gameObject.SetActive(act);
            _palm.gameObject.SetActive(act);
            // hand: sockets and orbs
            var b = _e.Building;
            for (int j = 0; j < 3; j++)
            {
                _sockets[j].gameObject.SetActive(act && j < _e.Tuning.Loci);
                bool filled = j < b.Count;
                _socketRingR[j].sharedMaterial = filled || HandPreviewed ? _ringLit : _ringDim;
                var orb = _socketOrbs[j];
                int want = filled ? b[j] : -1;
                if (orb.childCount == 0 && want < 0) continue;
                var cur = orb.childCount > 0 ? orb.GetChild(0) : null;
                if (want < 0) { if (cur != null) Destroy(cur.gameObject); }
                else if (cur == null || cur.name != "Gem " + EssenceInfo.DisplayName((Essence)want))
                {
                    if (cur != null) Destroy(cur.gameObject);
                    var g = MakeGem(orb, want, 0.62f);
                    g.localPosition = new Vector3(0, 0, -0.03f);
                }
                if (orb.childCount > 0) orb.GetChild(0).localRotation = Quaternion.Euler(0, _t * 70f + j * 40f, 0);
            }
            // palm glow = readiness; recovery shows as the glow returning
            float glow = 0.05f;
            if (_e.Phase == WardDuelEncounter.Screen.Setup) glow = b.Count == _e.Tuning.Loci ? 0.85f : 0.15f;
            else if (_e.Phase == WardDuelEncounter.Screen.Active)
            {
                if (s.PlayerCastsLeft <= 0) glow = 0.04f;
                else
                {
                    int ms = s.MsUntilPlayerMayCast;
                    float u = ms <= 0 ? 1f : 1f - ms / (float)Mathf.Max(1, _e.Tuning.PlayerRecoveryMs);
                    glow = Mathf.Lerp(0.05f, 0.9f, u) * (b.Count == _e.Tuning.Loci || ms > 0 ? 1f : 0.45f);
                }
                int idle = _e.MsUntilAutoCast;
                if (idle >= 0 && idle <= WardDuelEncounter.IdleWarnMs) glow = 0.5f + 0.5f * Mathf.Sin(_t * 8f); // the hand pulses before the idle cast
            }
            if (HandPreviewed) glow = 1f;
            _palmGlowMat.color = new Color(1f, 0.85f, 0.4f, glow);
            // beads and tally
            RefreshPost(_casterPost, s.PlayerCastsLeft, s.PlayerHistory, _beadLitC);
            RefreshPost(_oppPost, s.RivalCastsLeft, s.RivalHistory, _beadLitO);
            // referee
            RefreshReferee(res);
            // wards revealed in the air
            RefreshReveal(res, s);
        }

        private void RefreshPost(Post p, int left, IReadOnlyList<WardCastEntry> hist, Material lit)
        {
            for (int i = 0; i < p.Beads.Length; i++) p.Beads[i].sharedMaterial = i < left ? lit : _beadDark;
            for (int r = 0; r < p.Pegs.Length; r++)
            {
                if (r >= hist.Count) { for (int k = 0; k < 3; k++) p.Pegs[r][k].sharedMaterial = _pegDark; continue; }
                var h = hist[r];
                int f = h.Fractures, ec = h.Echoes;
                for (int k = 0; k < 3; k++) p.Pegs[r][k].sharedMaterial = k < f ? _pegF : k < f + ec ? _pegE : _pegD;
            }
        }

        private void RefreshReferee(bool result)
        {
            if (_referee == null) return;
            var target = result ? _refereeStage : _refereeRest;
            var r = _referee.Root;
            r.position = Vector3.MoveTowards(r.position, target, Time.unscaledDeltaTime * 3f);
            var look = (_stage.PlayerPos + _stage.MiraPos) * 0.5f;
            _referee.FaceTowards(new Vector3(look.x, r.position.y, look.z));
        }

        private int _revealKey = -1;
        private void RefreshReveal(bool res, WardDuelSession s)
        {
            _revealRoot.gameObject.SetActive(res && !GodBoardSettings.ClearView);
            if (!res) { _revealKey = -1; return; }
            var pw = s.RevealedPlayerWard; var rw = s.RevealedRivalWard;
            if (pw == null || rw == null) return;
            int key = 0; foreach (var x in pw) key = key * 5 + x + 1; foreach (var x in rw) key = key * 5 + x + 1;
            FrameAxes(out _, out var right);
            for (int side = 0; side < 2; side++)
            {
                var ward = side == 0 ? _stage.CasterWard : _stage.OpponentWard;
                var w = side == 0 ? pw : rw;
                for (int j = 0; j < 3 && j < w.Length; j++)
                {
                    var g = _reveal[side * 3 + j];
                    g.position = ward.transform.position + Vector3.up * (1.45f + 0.06f * Mathf.Sin(_t * 2f + j)) + right * (j - 1) * 0.5f;
                    g.rotation = Quaternion.Euler(0, _t * 60f, 0);
                }
            }
            if (key == _revealKey) return;
            _revealKey = key;
            for (int side = 0; side < 2; side++)
            {
                var w = side == 0 ? pw : rw;
                for (int j = 0; j < 3 && j < w.Length; j++)
                {
                    var old = _reveal[side * 3 + j];
                    var g = MakeGem(_revealRoot, w[j], 0.5f);
                    g.name = "Revealed " + side + "." + j;
                    Destroy(old.gameObject);
                    _reveal[side * 3 + j] = g;
                }
            }
        }

        /// <summary>Beads still lit on a side's post (tests: equals the casts left).</summary>
        public int BeadsLit(bool caster)
        {
            var p = caster ? _casterPost : _oppPost; int n = 0;
            foreach (var b in p.Beads) if (b.sharedMaterial != _beadDark) n++;
            return n;
        }

        /// <summary>Pegs lit on a side's tally stick by kind: fractures, echoes, fades (tests: equal the history's totals).</summary>
        public int[] TallyTotals(bool caster)
        {
            var p = caster ? _casterPost : _oppPost; var t = new int[3];
            foreach (var row in p.Pegs) foreach (var g in row)
            {
                if (g.sharedMaterial == _pegF) t[0]++; else if (g.sharedMaterial == _pegE) t[1]++; else if (g.sharedMaterial == _pegD) t[2]++;
            }
            return t;
        }

        /// <summary>One tally row as peg kinds left to right (0 fracture, 1 echo, 2 fade, -1 empty): always sorted, so no position can be read.</summary>
        public int[] TallyRow(bool caster, int row)
        {
            var p = caster ? _casterPost : _oppPost; var r = new int[3];
            for (int k = 0; k < 3; k++) { var m = p.Pegs[row][k].sharedMaterial; r[k] = m == _pegF ? 0 : m == _pegE ? 1 : m == _pegD ? 2 : -1; }
            return r;
        }

        /// <summary>The words each ledger would show for a thing (tests: counts and names only).</summary>
        public List<string> LedgerWords(DuelTarget t)
        {
            var l = new List<string>();
            IEnumerable<string> src = null;
            switch (t)
            {
                case DuelTarget.Referee: src = RefereeLines(); break;
                case DuelTarget.TallyCaster: src = TallyLines(true); break;
                case DuelTarget.TallyOpponent: src = TallyLines(false); break;
                case DuelTarget.BeadsCaster: src = BeadLines(true); break;
                case DuelTarget.BeadsOpponent: src = BeadLines(false); break;
                case DuelTarget.Hand: src = _hand.Lines; break;
            }
            if (src != null) l.AddRange(src);
            return l;
        }

        /// <summary>The revealed Ward gems in the air as essence ids (caster's three, then the opponent's three); null until the end.</summary>
        public int[] RevealedInAir()
        {
            if (_revealRoot == null || !_revealRoot.gameObject.activeSelf || _revealKey < 0) return null;
            var s = _e.Session;
            var r = new List<int>();
            if (s.RevealedPlayerWard != null) r.AddRange(s.RevealedPlayerWard);
            if (s.RevealedRivalWard != null) r.AddRange(s.RevealedRivalWard);
            return r.ToArray();
        }

        // ------------------------------------------------------------------ targets

        private struct Cand { public DuelTarget T; public int I; public Rect Gui; }

        private bool RectOf(Component c, out Rect r) => WorldHitTarget.ScreenBounds(c, _cam, out r);

        private void Candidates(List<Cand> list)
        {
            list.Clear();
            if (_cam == null) return;
            var ph = _e.Phase;
            bool act = ph == WardDuelEncounter.Screen.Setup || ph == WardDuelEncounter.Screen.Active;
            Rect r;
            if (ph != WardDuelEncounter.Screen.Stakes)
            {
                if (RectOf(_gate, out r)) list.Add(new Cand { T = DuelTarget.Gate, Gui = r });
                if (_referee != null && RectOf(_referee.Root, out r)) list.Add(new Cand { T = DuelTarget.Referee, Gui = r });
                if (RectOf(_casterPost.Plank, out r)) list.Add(new Cand { T = DuelTarget.TallyCaster, Gui = r });
                if (RectOf(_oppPost.Plank, out r)) list.Add(new Cand { T = DuelTarget.TallyOpponent, Gui = r });
                if (RectOf(_casterPost.Rail, out r)) list.Add(new Cand { T = DuelTarget.BeadsCaster, Gui = r });
                if (RectOf(_oppPost.Rail, out r)) list.Add(new Cand { T = DuelTarget.BeadsOpponent, Gui = r });
            }
            if (ph == WardDuelEncounter.Screen.Result && _stage.CasterActor != null && RectOf(_stage.CasterActor.Root, out r)) list.Add(new Cand { T = DuelTarget.Caster, Gui = r });
            if (act)
            {
                for (int i = 0; i < _stones.Count; i++) if (RectOf(_stones[i].Root, out r)) list.Add(new Cand { T = DuelTarget.Stone, I = i, Gui = r });
                for (int j = 0; j < _e.Building.Count && j < 3; j++) if (RectOf(_sockets[j], out r)) list.Add(new Cand { T = DuelTarget.Socket, I = j, Gui = r });
                if (RectOf(_palm, out r)) list.Add(new Cand { T = DuelTarget.Hand, Gui = r });
            }
        }

        /// <summary>The object under a screen point (pixels, origin bottom-left): each object's screen bounds grown to the minimum target; the nearest centre wins.</summary>
        public DuelTarget TargetAt(Vector2 screen, out int index)
        {
            index = 0;
            if (_cam == null) return DuelTarget.None;
            var gui = new Vector2(screen.x, Screen.height - screen.y);
            var list = new List<Cand>();
            Candidates(list);
            float best = float.MaxValue; var bt = DuelTarget.None;
            foreach (var c in list)
            {
                var big = WorldHitTarget.Enlarge(c.Gui, Screen.height);
                if (!big.Contains(gui)) continue;
                float d = (big.center - gui).sqrMagnitude / Mathf.Max(1f, big.width * big.height);
                if (d < best) { best = d; bt = c.T; index = c.I; }
            }
            return bt;
        }

        /// <summary>The screen point (pixels, origin bottom-left) where a target's centre is, or null if it is not offered now.</summary>
        public Vector2? ScreenPointOf(DuelTarget t, int index = 0)
        {
            SyncToCamera();
            var list = new List<Cand>();
            Candidates(list);
            foreach (var c in list)
                if (c.T == t && c.I == index) return new Vector2(c.Gui.center.x, Screen.height - c.Gui.center.y);
            return null;
        }

        /// <summary>The enlarged hit rect (GUI coordinates) of a target, or null.</summary>
        public Rect? HitRectOf(DuelTarget t, int index = 0)
        {
            SyncToCamera();
            var list = new List<Cand>();
            Candidates(list);
            foreach (var c in list) if (c.T == t && c.I == index) return WorldHitTarget.Enlarge(c.Gui, Screen.height);
            return null;
        }

        // ------------------------------------------------------------------ taps

        /// <summary>A tap at a screen point (pixels, origin bottom-left), as the pointer delivers it. Returns what it hit.</summary>
        public DuelTarget TapAt(Vector2 screen)
        {
            SyncToCamera();
            float now = Time.unscaledTime;
            var gui = new Vector2(screen.x, Screen.height - screen.y);
            // The open ledger is part of the thing it unfolded from; in a preview a tap on it confirms.
            if (LedgerCard.Contains(gui))
            {
                if (Machine.State == WorldActionState.Preview && Machine.Current is IWorldAction open) Machine.Tap(open, now);
                else Machine.Cancel(); // a tap on a tablet that only tells closes it
                return DuelTarget.None;
            }
            var t = TargetAt(screen, out int i);
            switch (t)
            {
                case DuelTarget.Stone:
                    Machine.TapElsewhere(now);
                    if (!_e.AddEssence((Essence)_stones[i].Pool)) _e.Say("The hand is full: tap an orb to take it back.");
                    break;
                case DuelTarget.Socket:
                    Machine.TapElsewhere(now);
                    _e.RemoveAt(i);
                    break;
                case DuelTarget.Hand: Machine.Tap(_hand, now); break;
                case DuelTarget.Gate: Machine.Tap(_gateAction, now); break;
                case DuelTarget.Caster: Machine.Tap(_rematch, now); break;
                case DuelTarget.Referee: Machine.Inspect(_refereeThing, now); break;
                case DuelTarget.TallyCaster: Machine.Inspect(_tallyC, now); break;
                case DuelTarget.TallyOpponent: Machine.Inspect(_tallyO, now); break;
                case DuelTarget.BeadsCaster: Machine.Inspect(_beadsC, now); break;
                case DuelTarget.BeadsOpponent: Machine.Inspect(_beadsO, now); break;
                default: Machine.TapElsewhere(now); break;
            }
            return t;
        }

        // ------------------------------------------------------------------ ledger

        private Transform AnchorOf(IInspectable thing)
        {
            if (ReferenceEquals(thing, _hand)) return _palm;
            if (ReferenceEquals(thing, _gateAction)) return _gate;
            if (ReferenceEquals(thing, _rematch)) return _stage.CasterActor != null ? _stage.CasterActor.Root : transform;
            if (ReferenceEquals(thing, _tallyC)) return _casterPost.Plank;
            if (ReferenceEquals(thing, _tallyO)) return _oppPost.Plank;
            if (ReferenceEquals(thing, _beadsC)) return _casterPost.Rail;
            if (ReferenceEquals(thing, _beadsO)) return _oppPost.Rail;
            return _referee != null ? _referee.Root : transform;
        }

        private bool _ledgerOurs;
        private void OnMachineChanged(WorldActionMachine m)
        {
            switch (m.State)
            {
                case WorldActionState.Preview:
                    var a = AnchorOf(m.Current);
                    LedgerCard.Show(LedgerContent.From(m.Current, ((IWorldAction)m.Current).PreviewLine, null), a, LedgerMode.Preview, a);
                    _ledgerOurs = true; break;
                case WorldActionState.Inspect:
                    a = AnchorOf(m.Current);
                    LedgerCard.Show(LedgerContent.From(m.Current, null, null), a, LedgerMode.Inspect, a);
                    _ledgerOurs = true; break;
                case WorldActionState.Refused:
                case WorldActionState.Disabled:
                    a = AnchorOf(m.Current);
                    LedgerCard.Show(LedgerContent.From(m.Current, null, m.Reason), a, LedgerMode.Refused, a);
                    _ledgerOurs = true; break;
                default:
                    if (_ledgerOurs) { LedgerCard.Hide(); _ledgerOurs = false; }
                    break;
            }
            HandPreviewed = m.State == WorldActionState.Preview && ReferenceEquals(m.Current, _hand);
        }

        private void OnDestroy()
        {
            Machine.Changed -= OnMachineChanged;
            if (_ledgerOurs) LedgerCard.Hide();
        }

        // ------------------------------------------------------------------ words (no digits are positions; counts only)

        public static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);

        public static string WeaveWords(int[] weave)
        {
            var parts = new string[weave.Length];
            for (int i = 0; i < weave.Length; i++) parts[i] = EssenceInfo.DisplayName((Essence)weave[i]);
            return string.Join(", ", parts);
        }

        /// <summary>One cast of a history: the weave and the three totals. Never a position.</summary>
        public static string CastWords(int index, WardCastEntry e) =>
            "Cast " + (index + 1) + ": " + WeaveWords(e.Weave) + ". " + Plural(e.Fractures, "fracture", "fractures") + ", " + Plural(e.Echoes, "echo", "echoes") + ", " + Plural(e.Fades, "fade", "fades") + ".";

        private IEnumerable<string> TallyLines(bool caster)
        {
            var s = _e.Session;
            var hist = caster ? s.PlayerHistory : s.RivalHistory;
            if (hist.Count == 0) { yield return "No casts yet."; yield break; }
            int f = 0, ec = 0, fd = 0;
            for (int i = 0; i < hist.Count; i++) { yield return CastWords(i, hist[i]); f += hist[i].Fractures; ec += hist[i].Echoes; fd += hist[i].Fades; }
            yield return "In all: " + Plural(f, "fracture", "fractures") + ", " + Plural(ec, "echo", "echoes") + ", " + Plural(fd, "fade", "fades") + ".";
            yield return "A fracture is the right essence in the right place; an echo, the right essence in the wrong place; a fade, an essence that is not in the Ward.";
        }

        private IEnumerable<string> BeadLines(bool caster)
        {
            var s = _e.Session;
            int left = caster ? s.PlayerCastsLeft : s.RivalCastsLeft;
            yield return Plural(left, "cast", "casts") + " left of " + _e.Tuning.MaxCasts + ".";
            if (caster) yield return StatusWords();
        }

        /// <summary>The hand's state in words: ready, recovering, out of casts (never a countdown number).</summary>
        public string StatusWords()
        {
            var s = _e.Session;
            if (_e.Phase == WardDuelEncounter.Screen.Setup) return "Build a secret Ward of " + _e.Tuning.Loci + " essences, then tap the hand to lock it.";
            if (_e.Phase != WardDuelEncounter.Screen.Active) return "The duel is over.";
            if (s.PlayerCastsLeft <= 0) return "Out of casts.";
            return s.MsUntilPlayerMayCast <= 0 ? "The hand is ready." : "The hand is still recovering.";
        }

        private IEnumerable<string> RefereeLines()
        {
            var s = _e.Session;
            switch (_e.Phase)
            {
                case WardDuelEncounter.Screen.Stakes: yield return "The duel has not begun."; yield break;
                case WardDuelEncounter.Screen.Setup:
                    yield return _e.IsCampaignMode ? "Choose Mira's secret Ward: three essences, repeats allowed." : "Choose your secret Ward: three essences, repeats allowed.";
                    yield return "Tap a stone to add its essence to the hand, an orb to take it back, and the hand to lock the Ward.";
                    if (_e.HelpVisible)
                    {
                        yield return "Fracture: the right essence in the right place.";
                        yield return "Echo: the right essence in the wrong place.";
                        yield return "Fade: that essence is not in the Ward.";
                    }
                    yield break;
                case WardDuelEncounter.Screen.Active:
                    yield return Plural(s.PlayerCastsLeft, "cast", "casts") + " left for " + _e.CasterName + ", " + Plural(s.RivalCastsLeft, "cast", "casts") + " for " + _e.OpponentName + ".";
                    yield return StatusWords();
                    yield return "Break " + WardDuelEncounter.Possessive(_e.OpponentName) + " Ward before " + (_e.IsCampaignMode ? "Mira's" : "yours") + " breaks.";
                    if (_e.HelpVisible)
                    {
                        yield return "Fracture: the right essence in the right place.";
                        yield return "Echo: the right essence in the wrong place.";
                        yield return "Fade: that essence is not in the Ward.";
                    }
                    yield break;
                default:
                    yield return _e.ResultHead();
                    yield return _e.ResultLine();
                    var foot = _e.ResultNote();
                    if (foot != null) yield return foot;
                    yield return _e.IsLethal ? "Tap the gate to go back." : "Tap " + _e.CasterName + " for a rematch; tap the gate to go back to the ring.";
                    yield break;
            }
        }

        // ------------------------------------------------------------------ the world actions

        private sealed class DuelInspect : IInspectable
        {
            private readonly System.Func<IEnumerable<string>> _lines;
            public DuelInspect(string title, System.Func<IEnumerable<string>> lines) { Title = title; _lines = lines; }
            public string Title { get; }
            public IEnumerable<string> Lines => _lines();
        }

        private sealed class HandAction : IWorldAction
        {
            private readonly DuelWorldProps _p;
            public HandAction(DuelWorldProps p) { _p = p; }
            private WardDuelEncounter E => _p._e;
            public string Title => E.Phase == WardDuelEncounter.Screen.Setup ? (E.IsCampaignMode ? "Mira's secret Ward" : "Your secret Ward") : (E.IsCampaignMode ? "Mira's weave" : "Your weave");
            public IEnumerable<string> Lines
            {
                get
                {
                    var b = E.Building;
                    yield return b.Count == 0 ? "The hand is empty." : "In the hand: " + WeaveWords(ToArray(b)) + ".";
                    yield return _p.StatusWords();
                }
            }
            private static int[] ToArray(IReadOnlyList<int> l) { var a = new int[l.Count]; for (int i = 0; i < a.Length; i++) a[i] = l[i]; return a; }
            public string PreviewLine => E.Phase == WardDuelEncounter.Screen.Setup ? "Lock this Ward?" : "Cast this weave?";
            public WorldActionCheck Check()
            {
                var ph = E.Phase;
                if (ph != WardDuelEncounter.Screen.Setup && ph != WardDuelEncounter.Screen.Active) return WorldActionCheck.Disable("The duel is over.");
                if (E.Building.Count != E.Tuning.Loci) return WorldActionCheck.Refuse("Fill the hand with " + E.Tuning.Loci + " essences first.");
                if (ph == WardDuelEncounter.Screen.Active)
                {
                    if (E.Session.PlayerCastsLeft <= 0) return WorldActionCheck.Refuse("Out of casts.");
                    if (E.Session.MsUntilPlayerMayCast > 0) return WorldActionCheck.Refuse("The hand is still recovering.");
                }
                return WorldActionCheck.Allowed;
            }
            public void ShowPreview(bool on) { }
            public bool Commit()
            {
                if (E.Phase == WardDuelEncounter.Screen.Setup) return E.LockWard();
                var r = E.Cast();
                return r != null && r.Ok;
            }
        }

        private sealed class GateAction : IWorldAction
        {
            private readonly DuelWorldProps _p;
            public GateAction(DuelWorldProps p) { _p = p; }
            private WardDuelEncounter E => _p._e;
            public string Title => "The gate";
            public IEnumerable<string> Lines
            {
                get
                {
                    if (E.Phase == WardDuelEncounter.Screen.Result) yield return "The way back to the ring.";
                    else if (E.IsLethal) yield return "Leaving the fight counts as a defeat: Mira falls back alone.";
                    else yield return "Leave the duel. Nothing is recorded.";
                }
            }
            public string PreviewLine => E.Phase == WardDuelEncounter.Screen.Result ? "Go back to the ring?" : E.IsLethal ? CampaignDuel.SurrenderLine(E.Request) : "Leave the duel?";
            public WorldActionCheck Check()
            {
                if (E.IsFinished) return WorldActionCheck.Disable("The duel is over.");
                if (E.Phase == WardDuelEncounter.Screen.Result && E.IsCampaignMode && !E.HoldOver) return WorldActionCheck.Refuse("Wait: the duel is still being called.");
                return WorldActionCheck.Allowed;
            }
            public void ShowPreview(bool on) { }
            public bool Commit() => E.Phase == WardDuelEncounter.Screen.Result ? E.Return() : E.Surrender();
        }

        private sealed class RematchAction : IWorldAction
        {
            private readonly DuelWorldProps _p;
            public RematchAction(DuelWorldProps p) { _p = p; }
            private WardDuelEncounter E => _p._e;
            public string Title => E.CasterName;
            public IEnumerable<string> Lines { get { yield return "Ready for another duel against " + E.OpponentName + "."; } }
            public string PreviewLine => "Duel again?";
            public WorldActionCheck Check()
            {
                if (E.Phase != WardDuelEncounter.Screen.Result) return WorldActionCheck.Disable("The duel is not over.");
                if (E.IsLethal) return WorldActionCheck.Refuse("There is one attempt: no rematch.");
                return WorldActionCheck.Allowed;
            }
            public void ShowPreview(bool on) { }
            public bool Commit() => E.Rematch();
        }
    }
}
