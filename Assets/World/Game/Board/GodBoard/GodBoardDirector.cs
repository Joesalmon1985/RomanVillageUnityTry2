using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    public enum BoardView { Altar, Rising, Board, Falling }

    /// <summary>
    /// The god board in the world (G1-5; PRD_GODBOARD GB-1, GB-2, GB-4, GB-7). Builds the four temples, owns the board camera
    /// (the rise from the altar and the way back), shows the pieces and the legal spots, turns a press on a spot into a
    /// table command, and listens to the table's events (feed, production pulse). It never writes state: it reads
    /// <see cref="WorldState.Table"/> and submits commands. Installed by <see cref="GodBoardInstaller"/> on a GodBoard game only.
    /// The HUD is <see cref="GodBoardHud"/>; the trade panel and offer cards are G1-6 (see <see cref="GodBoardHud.TradePanel"/>).
    /// </summary>
    public sealed class GodBoardDirector : MonoBehaviour
    {
        public const string Lock = "GodBoard";
        public const string SolidOwner = "temples";
        public const float AltarRange = 16f, BoardFov = 35f, TapRadiusPx = 24f;
        /// <summary>Seconds the camera takes to rise or fall (tests set it near zero).</summary>
        public static float RiseSeconds = 2.4f;
        /// <summary>The prince's hint (GB-7): the player can turn it off.</summary>
        public static bool AdviserOn = true;

        public static GodBoardDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; RiseSeconds = 2.4f; AdviserOn = true; }

        public BoardView View { get; private set; } = BoardView.Altar;
        public bool IsOpen => View == BoardView.Rising || View == BoardView.Board;
        public Camera BoardCam { get; private set; }
        public GodBoardPieces Pieces { get; private set; }
        public IReadOnlyList<TempleView> Temples => _temples;
        public WorldBootstrap Boot => _boot;
        public WorldState S => _boot.Sim.State;
        public TableState T => S.Table;
        public int Human => T != null && T.HumanSeat >= 0 ? T.HumanSeat : PlayerIds.Human;

        /// <summary>Legal spots for the player now (settlement or setup corners, road edges, own settlements that may become cities).</summary>
        public IReadOnlyList<int> CornerSpots => _corners;
        public IReadOnlyList<int> EdgeSpots => _edges;
        public IReadOnlyList<int> UpgradeSpots => _upgrades;
        /// <summary>The adviser's marked corner and edge, or -1.</summary>
        public int AdviseCorner { get; private set; } = -1;
        public int AdviseEdge { get; private set; } = -1;
        public int[] Points { get; private set; } = new int[0];
        public string Message { get; private set; } = "";
        public CommandResult LastResult { get; private set; }
        public int SacrificeCount { get; private set; }
        /// <summary>Unscaled time of the last epidemic (a resolved 7), for the banner; far in the past at first.</summary>
        public float EpidemicAt { get; private set; } = -100f;
        /// <summary>The table has ended: an age turned or the island fell. No further table input.</summary>
        public bool GameOver => T != null && T.Phase == TablePhase.Ended;

        public struct FeedLine { public string Text; public float Time; }
        private readonly List<FeedLine> _feed = new List<FeedLine>();
        public IReadOnlyList<FeedLine> Feed => _feed;

        private WorldBootstrap _boot;
        private readonly List<TempleView> _temples = new List<TempleView>();
        private readonly List<int> _corners = new List<int>(), _edges = new List<int>(), _upgrades = new List<int>();
        private float _t, _messageUntil;
        private Vector3 _fromPos, _toPos; private Quaternion _fromRot, _toRot; private float _fromFov;
        private Vector3 _focus; private float _height, _fitHeight, _centreX, _centreZ, _halfX, _halfZ;
        private long _marksTick = -1;
        private bool _savedHudVisible, _savedPointer = true;
        private Vector2 _downPos, _lastPos; private bool _down, _dragged; private float _pinchLast;

        // ---------------------------------------------------------------- install

        public static GodBoardDirector Install(WorldBootstrap boot)
        {
            var go = new GameObject("GodBoard");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<GodBoardDirector>();
            d.Init(boot);
            return d;
        }

        private void Init(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            var s = boot.Sim.State;
            if (s.Historic == null) BuildTemples(s); // a game that is already Historic has no temples (HI-2)
            Pieces = GodBoardPieces.Create(transform, s);
            BuildCamera(s);
            gameObject.AddComponent<GodBoardHud>();
            boot.Clock.Ticked += OnTicked;
            // A loaded game whose table was left running: nobody is at the board, so it waits (GB-4).
            var t = s.Table;
            if (t != null && (t.Phase == TablePhase.Setup || t.Phase == TablePhase.Main || t.Phase == TablePhase.Discard) && !t.Paused)
                boot.Sim.Submit(new LeaveTable { PlayerId = Human });
            RefreshPoints();
        }

        private void OnDestroy()
        {
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            if (Instance == this) Instance = null;
            WalkGrid.RemoveStaticBlockers(SolidOwner);
        }

        private void BuildTemples(WorldState s)
        {
            var board = s.Board;
            var layout = new CampLayout(board);
            WalkGrid.RemoveStaticBlockers(SolidOwner);
            int n = Mathf.Min(s.Table != null ? s.Table.PeopleCount : 0, board.StartCorners.Length);
            for (int p = 0; p < n; p++)
            {
                People people = null;
                foreach (var pe in s.Peoples) if (pe.Id == p) people = pe;
                if (people == null) continue;
                if (!TempleSites.Find(board, board.StartCorners[p], p == PlayerIds.Human ? layout.Dressing : null, out var at, out float yaw))
                { Debug.LogWarning("[GodBoard] no clear site for the temple of people " + p); continue; }
                var tv = TempleView.Create(transform, people, at, yaw, p == Human);
                _temples.Add(tv);
                LandViews.AddClearing(at.x, at.z, TempleSites.Footprint + 2f);
                WalkGrid.AddSolid(at, TempleView.Radius * 0.9f, SolidOwner);
            }
        }

        public TempleView TempleOf(int people) { foreach (var t in _temples) if (t.People == people) return t; return null; }
        public TempleView OwnTemple => TempleOf(Human);

        private void BuildCamera(WorldState s)
        {
            var go = new GameObject("God Board Camera");
            go.transform.SetParent(transform, false);
            BoardCam = go.AddComponent<Camera>();
            BoardCam.fieldOfView = BoardFov;
            BoardCam.nearClipPlane = 1f;
            BoardCam.farClipPlane = 6000f;
            BoardCam.clearFlags = CameraClearFlags.SolidColor;
            BoardCam.backgroundColor = new Color(0.20f, 0.40f, 0.52f);
            BoardCam.enabled = false;
            var topo = s.Board.Topology;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int c = 0; c < topo.CornerCount; c++)
            {
                var v = BoardSpace.Corner(topo, c);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
            }
            _centreX = (minX + maxX) * 0.5f; _centreZ = (minZ + maxZ) * 0.5f;
            _halfX = (maxX - minX) * 0.5f + 25f; _halfZ = (maxZ - minZ) * 0.5f + 25f;
            RefitCamera();
        }

        private void RefitCamera()
        {
            float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float tan = Mathf.Tan(BoardFov * 0.5f * Mathf.Deg2Rad);
            _fitHeight = Mathf.Max(_halfZ / tan, _halfX / (tan * aspect)) * 1.02f;
            if (_height <= 0f) { _height = _fitHeight; _focus = new Vector3(_centreX, 0, _centreZ); }
        }

        /// <summary>The camera pose of the board view (top down, north up).</summary>
        private void BoardPose(out Vector3 pos, out Quaternion rot)
        {
            pos = new Vector3(_focus.x, _height, _focus.z);
            rot = Quaternion.Euler(90f, 0f, 0f);
        }

        // ---------------------------------------------------------------- opening and leaving

        /// <summary>Casting at the altar: sends <c>Sacrifice</c> and starts the camera rise (GB-1).</summary>
        public bool TrySacrifice(bool ignoreRange = false)
        {
            if (T == null) { Hud.Notice("This game has no god board.", true); return false; }
            if (IsOpen || View == BoardView.Falling) return false;
            var w = PlayerWalker.Instance;
            var altar = OwnTemple;
            if (altar == null) return false;
            if (!ignoreRange && w != null && BoardSpace.Flat(altar.transform.position - w.Position) > AltarRange)
            {
                SpeechBubbles.SayAt(altar.transform.position + Vector3.up * 6f, "Walk closer to the altar.", 3f);
                return false;
            }
            LastResult = _boot.Sim.Submit(new Sacrifice { PlayerId = Human });
            if (!LastResult.Ok) { SpeechBubbles.SayAt(altar.transform.position + Vector3.up * 6f, LastResult.Message, 3.5f); return false; }
            SacrificeCount++;
            SpellEffect.Play(altar.AltarSpot.position, true);
            BeginRise();
            return true;
        }

        private void BeginRise()
        {
            var avatar = _boot.Avatar;
            var cam = avatar != null ? avatar.Camera : _boot.MainCamera;
            _fromPos = cam.transform.position; _fromRot = cam.transform.rotation; _fromFov = cam.fieldOfView;
            if (avatar != null) avatar.AddLock(Lock);
            cam.enabled = false;
            if (_boot.Views != null) _boot.Views.DevKeyEnabled = false;
            _savedPointer = PointerInput.InputEnabled; PointerInput.InputEnabled = false;
            _savedHudVisible = Hud.Visible; Hud.Visible = false;
            RenderSettingsFogOff();
            if (_height <= 0f || Mathf.Abs(_height - _fitHeight) < 1e-3f) { RefitCamera(); _height = _fitHeight; _focus = new Vector3(_centreX, 0, _centreZ); }
            BoardCam.enabled = true;
            Pieces.gameObject.SetActive(true);
            View = BoardView.Rising; _t = 0f;
            ApplyCamera(0f, true);
        }

        private bool _fogWas;
        private void RenderSettingsFogOff() { _fogWas = RenderSettings.fog; RenderSettings.fog = false; }

        /// <summary>The "Return to the world" control: pauses the table and brings the camera back to the altar (GB-4).</summary>
        public void Leave()
        {
            if (!IsOpen) return;
            var r = _boot.Sim.Submit(new LeaveTable { PlayerId = Human });
            if (r.Ok) LastResult = r;
            var avatar = _boot.Avatar;
            var cam = avatar != null ? avatar.Camera : _boot.MainCamera;
            _fromPos = BoardCam.transform.position; _fromRot = BoardCam.transform.rotation; _fromFov = BoardCam.fieldOfView;
            _toPos = cam.transform.position; _toRot = cam.transform.rotation;
            View = BoardView.Falling; _t = 0f;
        }

        private void EndFall()
        {
            View = BoardView.Altar;
            BoardCam.enabled = false;
            Pieces.gameObject.SetActive(false);
            var avatar = _boot.Avatar;
            if (avatar != null) { avatar.Camera.enabled = true; avatar.RemoveLock(Lock); }
            else _boot.MainCamera.enabled = true;
            if (_boot.Views != null) _boot.Views.DevKeyEnabled = true;
            PointerInput.InputEnabled = _savedPointer;
            Hud.Visible = _savedHudVisible;
            RenderSettings.fog = _fogWas;
        }

        // ---------------------------------------------------------------- commands (the view only submits)

        private CommandResult Submit(Command c)
        {
            c.PlayerId = Human;
            var r = _boot.Sim.Submit(c);
            LastResult = r;
            if (!r.Ok) Say(string.IsNullOrEmpty(r.Message) ? r.Reason : r.Message);
            else { _marksTick = -1; }
            return r;
        }

        /// <summary>Sends a table command as the player (G1-6 panels). Refusals show as a plain sentence in the status line.</summary>
        public CommandResult Send(Command c) => Submit(c);

        public CommandResult PlaceSettlement(int corner) => Submit(new PlaceTableSettlement { Corner = corner });
        public CommandResult PlaceRoad(int edge) => Submit(new PlaceTableRoad { HexEdge = edge });
        public CommandResult UpgradeCity(int corner) => Submit(new UpgradeTableCity { Corner = corner });
        public CommandResult EndTurn() => Submit(new EndTurn());

        public void Say(string text) { Message = text ?? ""; _messageUntil = Time.unscaledTime + 3.5f; }
        public bool HasMessage => Time.unscaledTime < _messageUntil && Message.Length > 0;

        /// <summary>A press at a screen point (pixels, origin bottom-left): the nearest legal spot gets its command. Returns what was sent, or null.</summary>
        public string TapAt(Vector2 screen)
        {
            if (View != BoardView.Board || GameOver) return null;
            RefreshMarks(true);
            float scale = Screen.height / 390f;
            float best = TapRadiusPx * Mathf.Max(1f, scale);
            string what = null; int spot = -1;
            var topo = S.Board.Topology;
            foreach (int c in _corners)
            {
                float d = Vector2.Distance(screen, ScreenOfCorner(c));
                if (d < best) { best = d; what = "corner"; spot = c; }
            }
            foreach (int c in _upgrades)
            {
                float d = Vector2.Distance(screen, ScreenOfCorner(c));
                if (d < best) { best = d; what = "city"; spot = c; }
            }
            foreach (int e in _edges)
            {
                float d = Vector2.Distance(screen, ScreenOfEdge(e)) + 3f;
                if (d < best) { best = d; what = "road"; spot = e; }
            }
            if (what == null) return null;
            if (what == "corner") PlaceSettlement(spot);
            else if (what == "city") UpgradeCity(spot);
            else PlaceRoad(spot);
            return what + " " + spot;
        }

        public Vector2 ScreenOfCorner(int c)
        {
            var p = BoardCam.WorldToScreenPoint(BoardSpace.Corner(S.Board.Topology, c) + Vector3.up * 10f);
            return new Vector2(p.x, p.y);
        }

        public Vector2 ScreenOfEdge(int e)
        {
            var p = BoardCam.WorldToScreenPoint(BoardSpace.HexEdgeMid(S.Board.Topology, e) + Vector3.up * 10f);
            return new Vector2(p.x, p.y);
        }

        // ---------------------------------------------------------------- reading the table

        public bool MyTurn => T != null && !T.Paused && T.Current == Human && (T.Phase == TablePhase.Setup || T.Phase == TablePhase.Main);

        private void RefreshPoints()
        {
            var t = T;
            if (t == null) return;
            if (Points.Length != t.PeopleCount) Points = new int[t.PeopleCount];
            for (int p = 0; p < t.PeopleCount; p++) Points[p] = TableRules.Points(S, p);
        }

        private void RefreshMarks(bool force)
        {
            var t = T;
            if (t == null) return;
            if (!force && _marksTick == S.Tick) return;
            _marksTick = S.Tick;
            _corners.Clear(); _edges.Clear(); _upgrades.Clear();
            AdviseCorner = -1; AdviseEdge = -1;
            if (MyTurn)
            {
                int h = Human;
                var hand = t.Peoples[h].Hand;
                if (t.Phase == TablePhase.Setup)
                {
                    if (t.SetupCorner < 0) _corners.AddRange(TableRules.LegalSettlementCorners(S, h, false));
                    else _edges.AddRange(TableRules.LegalRoadEdges(S, h, t.SetupCorner));
                }
                else
                {
                    if (TableRules.SettlementsLeft(t, h) > 0 && TableRules.CanPay(hand, TableRules.SettlementCost)) _corners.AddRange(TableRules.LegalSettlementCorners(S, h, true));
                    if (TableRules.RoadsLeft(t, h) > 0 && TableRules.CanPay(hand, TableRules.RoadCost)) _edges.AddRange(TableRules.LegalRoadEdges(S, h));
                    if (TableRules.CitiesLeft(t, h) > 0 && TableRules.CanPay(hand, TableRules.CityCost))
                        for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == h && t.CornerLevel[c] == 1) _upgrades.Add(c);
                }
                if (AdviserOn) Advise(h);
            }
            var ac = new List<int>(); var ae = new List<int>();
            if (AdviseCorner >= 0) ac.Add(AdviseCorner);
            if (AdviseEdge >= 0) ae.Add(AdviseEdge);
            var shown = new List<int>(_corners); shown.AddRange(_upgrades);
            Pieces.ShowMarks(shown, _edges, ac, ae);
        }

        private void Advise(int h)
        {
            var t = T;
            if (t.Phase == TablePhase.Setup)
            {
                if (t.SetupCorner < 0)
                {
                    int best = -1, bs = int.MinValue;
                    foreach (int c in _corners) { int sc = TableAi.SiteScore(S, h, c); if (sc > bs) { bs = sc; best = c; } }
                    AdviseCorner = best;
                }
                else
                {
                    // The road whose far end is the best next site.
                    var topo = S.Board.Topology;
                    int best = -1, bs = int.MinValue;
                    foreach (int e in _edges)
                    {
                        int far = topo.HexEdgeCornerA(e) == t.SetupCorner ? topo.HexEdgeCornerB(e) : topo.HexEdgeCornerA(e);
                        int sc = int.MinValue;
                        foreach (int e2 in topo.CornerHexEdges(far))
                        {
                            if (e2 == e) continue;
                            int c2 = topo.HexEdgeCornerA(e2) == far ? topo.HexEdgeCornerB(e2) : topo.HexEdgeCornerA(e2);
                            if (TableRules.SettlementRefusal(S, h, c2, false) == null) sc = Mathf.Max(sc, TableAi.SiteScore(S, h, c2));
                        }
                        if (sc > bs) { bs = sc; best = e; }
                    }
                    AdviseEdge = best;
                }
                return;
            }
            var target = TableAi.TargetFor(S, h);
            if (target.Kind == TableTargetKind.Road) AdviseEdge = target.Spot;
            else if (target.Kind == TableTargetKind.Settlement || target.Kind == TableTargetKind.City) AdviseCorner = target.Spot;
        }

        // ---------------------------------------------------------------- events

        private string Name(int p)
        {
            foreach (var pe in S.Peoples) if (pe.Id == p) return p == Human ? "You" : char.ToUpper(pe.Name.StartsWith("the ") ? pe.Name[4] : pe.Name[0]) + (pe.Name.StartsWith("the ") ? pe.Name.Substring(5) : pe.Name.Substring(1));
            return "People " + p;
        }

        public string PeopleShort(int p) => Name(p) == "You" ? "You" : Name(p);

        private void Feed1(string text)
        {
            _feed.Add(new FeedLine { Text = text, Time = Time.unscaledTime });
            while (_feed.Count > 6) _feed.RemoveAt(0);
        }

        private int FindOfferFrom(int id)
        {
            foreach (var o in T.Offers) if (o.Id == id) return o.From;
            return -1;
        }

        /// <summary>Esc or the pause key: closes the trade panel if open, else leaves the board like "Return to the world". True when it did something.</summary>
        public bool PressEscape()
        {
            if (!IsOpen) return false;
            var hud = GetComponent<GodBoardHud>();
            if (hud != null && hud.TradeOpen) { hud.CloseTrade(); return true; }
            Leave();
            return true;
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            RefreshPoints();
            foreach (var e in events)
            {
                switch (e)
                {
                    case Produced pr: HexDressingView.Pulse(pr.Hexes); break;
                    case Built b: Feed1(Name(b.People) + (b.People == Human ? " built a " : " built a ") + b.Kind + "."); break;
                    case Traded tr: Feed1(tr.With < 0 ? Name(tr.People) + " traded at the bank." : Name(tr.People) + " traded with " + Name(tr.With) + "."); break;
                    case TradeRefused rf:
                        Feed1(rf.Code == TableRefusals.LeaderRefuses ? Name(rf.People) + " will not trade with the leader." : Name(rf.People) + " will not trade: " + rf.Sentence);
                        break;
                    case Epidemic _:
                        Feed1("The gods are angry.");
                        EpidemicAt = Time.unscaledTime;
                        if (View != BoardView.Board) Hud.Notice("The gods are angry.", true);
                        break;
                    case OfferAnswered oa when _boot.Sim.State.Table != null && FindOfferFrom(oa.OfferId) == Human:
                        Feed1(oa.Status == OfferStatus.Accepted ? Name(oa.By) + " accepted your offer." : oa.Status == OfferStatus.Declined ? "Your offer was declined." : "Your offer ran out of time.");
                        break;
                    case GameLost _: Feed1("The island falls."); break;
                    case Discarded d: Feed1(Name(d.People) + " discarded cards."); break;
                    case OfferMade om when om.To == Human: Feed1(Name(om.From) + " offers you a trade."); break;
                    case AgeEnded ae: Feed1(Name(ae.Winner) + " reached " + ae.Points + " points. The age has turned."); break;
                    case TableResumed _: Feed1("The god board opens."); break;
                }
            }
            _marksTick = -1;
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            var t = T;
            foreach (var tv in _temples)
                tv.Smoking = t != null && !t.Paused && tv.People == t.Current && (t.Phase == TablePhase.Setup || t.Phase == TablePhase.Main || t.Phase == TablePhase.Discard);

            if (Input.GetKeyDown(KeyCode.Escape)) PressEscape();

            if (View == BoardView.Rising || View == BoardView.Falling)
            {
                _t += Time.unscaledDeltaTime / Mathf.Max(0.01f, RiseSeconds);
                float u = Mathf.Clamp01(_t);
                ApplyCamera(u, View == BoardView.Rising);
                if (u >= 1f)
                {
                    if (View == BoardView.Rising) View = BoardView.Board;
                    else EndFall();
                }
            }
            if (View == BoardView.Board)
            {
                Pointer();
                ClampFocus();
                ApplyBoardCamera();
            }
            if (IsOpen) { Pieces.Sync(); RefreshMarks(false); }
        }

        private void ApplyCamera(float u, bool rising)
        {
            float k = u * u * (3f - 2f * u);
            if (rising)
            {
                BoardPose(out var p, out var r);
                BoardCam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPos, p, k), Quaternion.Slerp(_fromRot, r, k));
                BoardCam.fieldOfView = Mathf.Lerp(_fromFov, BoardFov, k);
            }
            else
            {
                var cam = _boot.Avatar != null ? _boot.Avatar.Camera : _boot.MainCamera;
                BoardCam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPos, cam.transform.position, k), Quaternion.Slerp(_fromRot, cam.transform.rotation, k));
                BoardCam.fieldOfView = Mathf.Lerp(_fromFov, cam.fieldOfView, k);
            }
        }

        private void ApplyBoardCamera()
        {
            BoardPose(out var p, out var r);
            BoardCam.transform.SetPositionAndRotation(p, r);
            BoardCam.fieldOfView = BoardFov;
        }

        private void ClampFocus()
        {
            float tan = Mathf.Tan(BoardFov * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float hz = _height * tan, hx = hz * aspect;
            float mx = Mathf.Max(0f, _halfX - hx * 0.5f), mz = Mathf.Max(0f, _halfZ - hz * 0.5f);
            _focus.x = Mathf.Clamp(_focus.x, _centreX - mx, _centreX + mx);
            _focus.z = Mathf.Clamp(_focus.z, _centreZ - mz, _centreZ + mz);
        }

        /// <summary>Zoom factor above 1 zooms in. Clamped between the whole island and a close view.</summary>
        public void Zoom(float factor)
        {
            _height = Mathf.Clamp(_height / Mathf.Max(0.1f, factor), _fitHeight * 0.22f, _fitHeight);
        }

        public void Pan(Vector2 screenDelta)
        {
            float mpp = 2f * _height * Mathf.Tan(BoardFov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            _focus.x -= screenDelta.x * mpp; _focus.z -= screenDelta.y * mpp;
        }

        public void ResetView() { _height = _fitHeight; _focus = new Vector3(_centreX, 0, _centreZ); }
        public float ZoomLevel => _fitHeight / Mathf.Max(1f, _height);

        private void Pointer()
        {
            var hud = GetComponent<GodBoardHud>();
            float dragPx = 10f * Mathf.Max(1f, Screen.height / 390f);
            if (Input.touchCount >= 2)
            {
                float d = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
                if (_pinchLast > 0f && d > 1f) Zoom(d / _pinchLast);
                _pinchLast = d; _down = false;
                return;
            }
            _pinchLast = 0f;
            bool touch = Input.touchCount == 1;
            Vector2 pos; bool pressed, released, held;
            if (touch)
            {
                var tc = Input.GetTouch(0); pos = tc.position;
                pressed = tc.phase == TouchPhase.Began; released = tc.phase == TouchPhase.Ended; held = true;
            }
            else
            {
                pos = Input.mousePosition;
                pressed = Input.GetMouseButtonDown(0); released = Input.GetMouseButtonUp(0); held = Input.GetMouseButton(0);
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f && (hud == null || !hud.Over(pos))) Zoom(Mathf.Pow(1.2f, wheel));
            }
            if (pressed)
            {
                if (hud != null && hud.Over(pos)) { _down = false; return; }
                _down = true; _dragged = false; _downPos = pos; _lastPos = pos;
            }
            else if (_down && held)
            {
                if (!_dragged && Vector2.Distance(pos, _downPos) > dragPx) _dragged = true;
                if (_dragged) { Pan(pos - _lastPos); _lastPos = pos; }
            }
            if (_down && released)
            {
                _down = false;
                if (!_dragged) TapAt(pos);
            }
        }
    }

    /// <summary>The call from <see cref="WorldBootstrap"/> (after the land views, before the F1 tools): only a GodBoard game gets a god board.</summary>
    public static class GodBoardInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            var s = boot.Sim.State;
            if (s.Ruleset != Ruleset.GodBoard || s.Table == null) return;
            GodBoardDirector.Install(boot);
            PlagueViews.Install(boot);
            HistoricDirector.Install(boot); // G4-3: the switch, castles, trees, Historic buildings
            SpireDirector.Install(boot); // G2-4: crystal spires and their rhythm stages
        }
    }
}
