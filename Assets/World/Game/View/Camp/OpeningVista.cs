using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>Whether a god board hub opens on the vista (On) or in the walking view as before (Off: older tests, AutoPlay scripts).</summary>
    public enum VistaMode { On, Off }

    /// <summary>Where the vista camera is: not in use (the walking view), up over the camp, gliding down to a place, or rising back.</summary>
    public enum VistaState { Off, Vista, Descending, Rising }

    /// <summary>
    /// The opening vista (owner feedback, 21 Nov 2026; layout approved by the owner the same day, Docs/design/OPENING_TARGET.md): the
    /// game opens on a composed, elevated night view of the hub in which all six destinations stand in their own blocks of the screen:
    /// far away along the top the temple (left), the council longhouse (centre) and Sol's drums (right); near along the bottom the duel
    /// ring (left), the healer's lodge (centre) and Mira's camp by the cave (right); the start fire in the near foreground band. The
    /// pose comes from <see cref="CampLayout.Vista"/> (the hub's places were placed for it); every screen shows the whole 16:9 frame
    /// centred in its safe area (<see cref="VistaFrame"/>), and the blocks divide the safe area itself, so a tap picks the place whose
    /// block it falls in (<see cref="RegionAt"/>) at any aspect ratio. Tap a block once: that place lights up and the shared
    /// <see cref="LedgerCard"/> names the activity (a <see cref="VistaPlace"/> on the WU-0 two-tap machine). Tap it again (or the card,
    /// or E): the camera glides down to the player standing at the place and the place's existing entry runs
    /// (<see cref="VistaPlace.Enter"/>). Back to the vista: Esc (or a phone's back key) in the walking view, or a tap on the sky. W or the
    /// up arrow in the vista steps down into the walking view where the player stands; A / D light the places in reading order.
    /// Reads state only; the entries are the same calls the hub's people and props make. Owner: opening vista.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class OpeningVista : MonoBehaviour
    {
        public const string Lock = "Vista";
        public const float DescendSeconds = 1.4f, RiseSeconds = 1.0f, FieldOfView = VistaFrame.Fov;
        /// <summary>Downward pitch of the vista camera (from <see cref="CampLayout.Vista"/>).</summary>
        public float Pitch => _pose.Pitch;

        /// <summary>Set by tests and AutoPlay. Not reset per session (the PlayMode test assembly turns it off).</summary>
        public static VistaMode Mode = VistaMode.On;
        public static OpeningVista Instance { get; private set; }

        /// <summary>Tests: divide a screen of this size (pixels) instead of the real one; null = the real screen. The safe area is then the whole of it unless <see cref="ScreenSafeArea.Override"/> says otherwise.</summary>
        public static Vector2Int? ScreenOverride;
        private static int SW => ScreenOverride.HasValue ? ScreenOverride.Value.x : Screen.width;
        private static int SH => ScreenOverride.HasValue ? ScreenOverride.Value.y : Screen.height;

        /// <summary>True while the vista owns the camera (up, or gliding).</summary>
        public static bool Active => Instance != null && Instance.State != VistaState.Off;
        /// <summary>True while the player looks out over the camp (taps pick regions).</summary>
        public static bool Up => Instance != null && Instance.State == VistaState.Vista;
        /// <summary>True while the vista owns the screen or is about to open (before <see cref="Start"/> opens it): the tutorials wait for the player's pick.</summary>
        public static bool Opening => Active || (Instance != null && !Instance._started && WillOpen);
        private bool _started;

        /// <summary>Whether a god board hub opens on the vista this run: Mode is On and no packaged AutoPlay script runs without <c>-vista</c>.</summary>
        public static bool WillOpen
        {
            get
            {
                if (Mode != VistaMode.On) return false;
                if (!Application.isEditor && AutoPlay.CommandLineScript() != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-vista") < 0) return false;
                return true;
            }
        }

        public VistaState State { get; private set; } = VistaState.Off;
        public IReadOnlyList<VistaPlace> Places => _places;
        /// <summary>The place the last travel went to (tests), and how many travels and returns.</summary>
        public VistaPlace LastArrived { get; private set; }
        public int Travels { get; private set; }
        public int Returns { get; private set; }
        /// <summary>Region taps in the vista (the welcome stops at the first).</summary>
        public int Taps { get; private set; }
        /// <summary>Raised when a travel has landed and the place's entry ran.</summary>
        public event Action<VistaPlace> Arrived;

        /// <summary>The ground point at the middle of the view.</summary>
        public Vector3 Focus { get; private set; }
        public Vector3 CameraPosition => _pose.Position;
        public Quaternion CameraRotation => _pose.Rotation;
        public VistaPose Pose => _pose;

        private readonly List<VistaPlace> _places = new List<VistaPlace>();
        private WorldBootstrap _boot;
        private VistaPose _pose;
        private float _t; private Vector3 _fromPos; private Quaternion _fromRot; private float _fromFov;
        private VistaPlace _going;
        private bool _fogWas; private bool _fogSaved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; ScreenOverride = null; }

        public static OpeningVista Create(WorldBootstrap boot, Transform root, CampLayout layout)
        {
            var go = new GameObject("Opening vista");
            go.transform.SetParent(root, false);
            var v = go.AddComponent<OpeningVista>();
            v._boot = boot;
            Instance = v;
            VistaPlaces.Build(v, go.transform, layout, v._places);
            v._pose = layout.Vista.Valid ? layout.Vista : Fallback(v._places);
            var f = VistaFrame.Ground(v._pose, 0.5f, 0.5f);
            v.Focus = f.HasValue ? f.Value : v._pose.Position;
            VistaPlaces.BuildScenery(go.transform, layout, v._places, v._pose);
            VistaTerrain.BuildBackdrop(go.transform, v._places, v.Focus, v._pose.Yaw);
            if (!layout.Vista.Valid) Debug.LogWarning("[Vista] no pose puts the circle and the cave camp in their blocks on this board; looking from the south instead");
            // the very first frame is the vista: the camera takes the pose now, before anything renders (Start then locks the walker)
            if (WillOpen && v._places.Count > 0) v.Apply(v._pose.Position, v._pose.Rotation, 1f);
            return v;
        }

        /// <summary>A board where no composed pose fits: look at the places' middle from the south, high enough to hold them.</summary>
        private static VistaPose Fallback(List<VistaPlace> places)
        {
            var c = Vector3.zero; foreach (var p in places) c += p.Centre; c /= Mathf.Max(1, places.Count); c.y = 0;
            var rot = Quaternion.Euler(40f, 0f, 0f);
            return new VistaPose { Position = c - rot * Vector3.forward * 150f, Yaw = 0f, Pitch = 40f, Valid = false };
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private IEnumerator Start()
        {
            // every installer has run (in the boot's Awake): open at once, so the first frame and the first tap are the vista's
            if (WillOpen && _places.Count > 0 && PlayerWalker.Instance != null) Open(true);
            else
            {
                yield return null;
                if (WillOpen && _places.Count > 0) Open(true);
            }
            _started = true;
        }

        // ------------------------------------------------------------------ pose

        private Camera Cam => _boot != null && _boot.Avatar != null ? _boot.Avatar.Camera : Camera.main;

        /// <summary>The lookout's bearing (degrees).</summary>
        public float Bearing => _pose.Yaw;

        // ------------------------------------------------------------------ regions

        /// <summary>The safe area the blocks divide (GUI coordinates, top-left origin).</summary>
        public static Rect SafeGui()
        {
            if (ScreenSafeArea.Override.HasValue) return ScreenSafeArea.Override.Value;
            if (ScreenOverride.HasValue) return new Rect(0, 0, SW, SH);
            return ScreenSafeArea.Current();
        }

        /// <summary>Where the 16:9 reference frame lies on the screen now (pixels, bottom-left origin).</summary>
        public static Rect RefRect() => VistaFrame.RefRect(Mathf.Max(1, SW), Mathf.Max(1, SH), SafeGui());

        /// <summary>A place's block on the screen now (pixels, bottom-left origin).</summary>
        public static Rect BlockRect(string zone)
        {
            var s = SafeGui(); var b = VistaFrame.Block(zone);
            float x0 = s.xMin + b.xMin * s.width, x1 = s.xMin + b.xMax * s.width;
            float yTop0 = s.yMin + b.yMin * s.height, yTop1 = s.yMin + b.yMax * s.height;
            return Rect.MinMaxRect(x0, SH - yTop1, x1, SH - yTop0);
        }

        /// <summary>A place's screen box (pixels, origin bottom-left) as the vista camera sees it.</summary>
        public Rect ScreenRect(VistaPlace p)
        {
            var rr = RefRect();
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var w in p.Samples())
            {
                var v = VistaFrame.Screen(_pose, w, rr);
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>A place's centre on screen (pixels, origin bottom-left).</summary>
        public Vector2 ScreenCentre(VistaPlace p)
        {
            var v = VistaFrame.Screen(_pose, p.Centre + Vector3.up * p.Height * 0.35f, RefRect());
            return new Vector2(v.x, v.y);
        }

        /// <summary>
        /// The place whose block holds a screen point (pixels, origin bottom-left): the safe area is divided into the owner's six
        /// blocks (top row temple, council, rhythm; bottom row duel, healer, cave); points outside it count as its nearest edge.
        /// </summary>
        public VistaPlace RegionAt(Vector2 screen)
        {
            var s = SafeGui();
            float u = (screen.x - s.xMin) / Mathf.Max(1f, s.width), vTop = ((SH - screen.y) - s.yMin) / Mathf.Max(1f, s.height);
            var p = Find(VistaFrame.ZoneAt(u, vTop));
            if (p != null) return p;
            // a place missing on this board: the nearest place's centre
            VistaPlace near = null; float best = float.MaxValue;
            foreach (var q in _places) { float d = (ScreenCentre(q) - screen).sqrMagnitude; if (d < best) { best = d; near = q; } }
            return near;
        }

        public VistaPlace Find(string zone) { foreach (var p in _places) if (p.Zone == zone) return p; return null; }

        // ------------------------------------------------------------------ taps and keys

        /// <summary>A tap in the vista (the pointer sends every tap here while it is up).</summary>
        public TapOutcome TapScreen(Vector2 screen)
        {
            if (State != VistaState.Vista) return TapOutcome.Ignored;
            var p = RegionAt(screen);
            if (p == null) return TapOutcome.Ignored;
            Taps++;
            return Result(WorldActions.Tap(p));
        }

        /// <summary>The same as tapping the place's region (tests, AutoPlay, keys).</summary>
        public TapOutcome TapPlace(VistaPlace p)
        {
            if (State != VistaState.Vista || p == null) return TapOutcome.Ignored;
            Taps++;
            return Result(WorldActions.Tap(p));
        }

        private static TapOutcome Result(WorldTapResult r)
        {
            switch (r)
            {
                case WorldTapResult.Previewed: return TapOutcome.Preview;
                case WorldTapResult.Confirmed: return TapOutcome.Confirm;
                case WorldTapResult.Refused: case WorldTapResult.Disabled: return TapOutcome.Refused;
                case WorldTapResult.Cancelled: return TapOutcome.Cancelled;
                default: return TapOutcome.Ignored;
            }
        }

        /// <summary>
        /// A drag in the vista (pixels since the last call). The view stays put: the screen's blocks are the places, so a swing would
        /// move the places out of the blocks a tap picks. Kept so the pointer's drag path has somewhere to go.
        /// </summary>
        public void Swing(float dxPixels) { }

        private void Update()
        {
            UpdateHint();
            if (State != VistaState.Vista || Hud.MenuOpen || MapScreen.IsOpen) return;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad8)) { StepDown(); return; }
            int dir = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
            if (dir != 0) CycleKey(dir);
        }

        /// <summary>A / D (arrows) in the vista preview the places in reading order (top row left to right, then the bottom row); E or Enter confirms (the shared machine's key).</summary>
        private void CycleKey(int dir)
        {
            var order = new List<VistaPlace>();
            foreach (var z in VistaFrame.Zones) { var p = Find(z); if (p != null) order.Add(p); }
            if (order.Count == 0) return;
            int i = WorldActions.Machine.Current is VistaPlace cur ? order.IndexOf(cur) : (dir > 0 ? -1 : order.Count);
            i = (i + dir + order.Count) % order.Count;
            WorldActions.Cancel();
            TapPlace(order[i]);
        }

        // ------------------------------------------------------------------ transitions

        /// <summary>Up over the camp (snap at the start of a game, else a glide up from the eye).</summary>
        public void Open(bool snap)
        {
            var w = PlayerWalker.Instance;
            if (w == null || State == VistaState.Vista) return;
            WorldActions.Cancel();
            w.StopAuto();
            w.AddLock(Lock);
            GroundWalk.ClearMarker();
            var cam = Cam;
            Debug.Log("[Vista] open over the camp: " + _pose + ", " + _places.Count + " places, screen " + SW + "x" + SH + (snap ? " (start)" : ""));
            if (snap || cam == null) { State = VistaState.Vista; Apply(CameraPosition, CameraRotation, 1f); return; }
            _fromPos = cam.transform.position; _fromRot = cam.transform.rotation; _fromFov = cam.fieldOfView;
            _t = 0f; State = VistaState.Rising; Returns++;
        }

        /// <summary>Back to the vista from the walking view if nothing else holds the player (Esc, a sky tap). True if it rose.</summary>
        public static bool TryReturn()
        {
            var v = Instance;
            if (v == null || Mode != VistaMode.On || v.State != VistaState.Off) return false;
            var w = PlayerWalker.Instance;
            if (w == null || w.IsLocked || w.IsOffLattice) return false;
            if (Interiors.Current != null || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning)) return false;
            if (GodBoardDirector.Instance != null && GodBoardDirector.Instance.View != BoardView.Altar) return false;
            if (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God) return false;
            if (WorldActions.Open || DialoguePanel.IsOpen) return false;
            v.Open(false);
            return true;
        }

        /// <summary>Glides down into the walking view where the player stands (W in the vista).</summary>
        public void StepDown() { if (State == VistaState.Vista) BeginDescent(null); }

        /// <summary>Leaves the vista at once into the walking view (tests, the tour, AutoPlay).</summary>
        public void Leave()
        {
            if (State == VistaState.Off) return;
            WorldActions.Cancel();
            Finish(null);
        }

        /// <summary>The second tap on a place: the camera glides down to the player at the place, then its entry runs.</summary>
        internal bool TravelTo(VistaPlace p)
        {
            if (State != VistaState.Vista || p == null) return false;
            var w = PlayerWalker.Instance;
            if (w == null) return false;
            var stand = p.StandPoint();
            var want = WalkLattice.ToCell(stand);
            var free = WalkGrid.IsBlocked(want) ? WalkGrid.NearestFree(want, 5) : want;
            if (!free.HasValue) return false;
            var cell = free.Value;
            var face = p.FacePoint() - PlayerWalker.CellPosition(cell);
            int heading = WalkLattice.HeadingOf(BoardSpace.YawOf(face));
            w.StopAuto();
            w.Teleport(cell, heading);
            Travels++;
            p.Pulse(DescendSeconds + 0.4f); // the place stays lit while the camera glides to it
            BeginDescent(p);
            return true;
        }

        private void BeginDescent(VistaPlace to)
        {
            var cam = Cam;
            _going = to;
            _fromPos = CameraPosition; _fromRot = CameraRotation;
            if (cam != null) { _fromPos = cam.transform.position; _fromRot = cam.transform.rotation; }
            _t = 0f; State = VistaState.Descending;
        }

        private void Finish(VistaPlace arrived)
        {
            State = VistaState.Off;
            var cam = Cam;
            if (cam != null)
            {
                cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;
                cam.ResetProjectionMatrix();
                cam.fieldOfView = PlayerWalker.FieldOfView;
            }
            RestoreFog();
            var w = PlayerWalker.Instance;
            if (w != null) w.RemoveLock(Lock);
            _going = null;
            if (arrived != null)
            {
                LastArrived = arrived;
                arrived.Enter?.Invoke();
                Arrived?.Invoke(arrived);
                HintOnce();
            }
        }

        private static bool s_hinted;
        private bool _hintPending;
        /// <summary>The first time the player is back in the walking view after a trip (not over a game), say how to look out again.</summary>
        private void HintOnce() { if (!s_hinted) _hintPending = true; }

        private void UpdateHint()
        {
            if (!_hintPending || State != VistaState.Off) return;
            var w = PlayerWalker.Instance;
            if (w == null || w.IsLocked || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning)) return;
            if (GodBoardDirector.Instance != null && GodBoardDirector.Instance.View != BoardView.Altar) return;
            _hintPending = false; s_hinted = true;
            Hud.Notice("Tap the sky, or press Esc, to look out over the camp again.", false);
        }

        private void LateUpdate()
        {
            var cam = Cam;
            if (State == VistaState.Off || cam == null) return;
            // an encounter or the god board took the camera (a duel whose opponent arrived late, say): give it up at once
            if ((EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) || (GodBoardDirector.Instance != null && GodBoardDirector.Instance.View != BoardView.Altar)
                || (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God))
            {
                if (State == VistaState.Descending) { Finish(null); return; }
                Leave(); return;
            }
            WorldActions.NoteCameraMove(0f);
            switch (State)
            {
                case VistaState.Vista:
                    Apply(CameraPosition, CameraRotation, 1f);
                    break;
                case VistaState.Rising:
                {
                    _t += Time.unscaledDeltaTime / RiseSeconds;
                    float e = Smooth(_t);
                    Apply(Arc(_fromPos, CameraPosition, e), Quaternion.Slerp(_fromRot, CameraRotation, e), e, _fromFov);
                    WorldActions.NoteCameraMove(0.2f);
                    if (_t >= 1f) State = VistaState.Vista;
                    break;
                }
                case VistaState.Descending:
                {
                    var w = PlayerWalker.Instance;
                    var eyePos = w != null ? w.Head.position : _fromPos;
                    var eyeRot = w != null ? w.Head.rotation : _fromRot;
                    _t += Time.unscaledDeltaTime / DescendSeconds;
                    float e = Smooth(_t);
                    Apply(Arc(_fromPos, eyePos, e), Quaternion.Slerp(_fromRot, eyeRot, e), 1f - e, PlayerWalker.FieldOfView);
                    WorldActions.NoteCameraMove(0.2f);
                    if (_t >= 1f) Finish(_going);
                    break;
                }
            }
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static Vector3 Arc(Vector3 a, Vector3 b, float e) => Vector3.Lerp(a, b, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * Vector3.Distance(a, b) * 0.08f;

        /// <summary>
        /// Puts the camera at a pose. <paramref name="vista"/> blends the projection: 1 = the vista's (the 16:9 reference frame centred
        /// in the safe area, <see cref="VistaFrame.Projection"/>), 0 = a plain one of field of view <paramref name="plainFov"/>.
        /// </summary>
        private void Apply(Vector3 pos, Quaternion rot, float vista, float plainFov = 70f)
        {
            var cam = Cam;
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(pos, rot);
            int w = Mathf.Max(1, cam.pixelWidth), h = Mathf.Max(1, cam.pixelHeight);
            var safe = ScreenSafeArea.Override ?? ScreenSafeArea.Current();
            var a = VistaFrame.Plain(w, h, plainFov, cam.nearClipPlane, cam.farClipPlane);
            var b = VistaFrame.Projection(w, h, VistaFrame.RefRect(w, h, safe), cam.nearClipPlane, cam.farClipPlane);
            var m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i] = Mathf.Lerp(a[i], b[i], Mathf.Clamp01(vista));
            cam.projectionMatrix = m;
            cam.fieldOfView = Mathf.Lerp(plainFov, FieldOfView, vista);
            if (!_fogSaved) { _fogWas = RenderSettings.fog; _fogSaved = true; }
            RenderSettings.fog = false; // the far side of the camp must read at night
        }

        private void RestoreFog() { if (_fogSaved) { RenderSettings.fog = _fogWas; _fogSaved = false; } }

        // ------------------------------------------------------------------ the welcome

        /// <summary>
        /// The first moments: a short welcome from the prince, then each place lights for a beat with a line naming what is played there.
        /// Stops at the player's first tap in the vista or when they leave it. Speech only; nothing is sent.
        /// </summary>
        public void PlayWelcome(ISpeaker prince, string greeting, IList<KeyValuePair<string, string>> lines)
        {
            StartCoroutine(Welcome(prince, greeting, lines));
        }

        /// <summary>True while the welcome is pointing out the places.</summary>
        public bool Welcoming { get; private set; }
        /// <summary>The places the welcome has pointed at so far (tests).</summary>
        public readonly List<string> Pointed = new List<string>();

        private IEnumerator Welcome(ISpeaker prince, string greeting, IList<KeyValuePair<string, string>> lines)
        {
            Welcoming = true;
            int taps = Taps;
            while (State == VistaState.Rising) yield return null;
            if (State != VistaState.Vista) { Welcoming = false; yield break; }
            var g0 = VistaFrame.Ground(_pose, 0.5f, 0.6f); // below the row split, so the greeting does not hide the far row
            var focus = (g0.HasValue ? g0.Value : Focus) + Vector3.up * 4f;
            int h = prince != null ? SpeechBubbles.SayAt(focus, greeting, 5.5f) : 0;
            float until = Time.unscaledTime + 4.5f;
            while (Time.unscaledTime < until) { if (Taps != taps || State != VistaState.Vista) { SpeechBubbles.Close(h); Welcoming = false; yield break; } yield return null; }
            foreach (var kv in lines)
            {
                var p = Find(kv.Key);
                if (p == null) continue;
                Pointed.Add(kv.Key);
                p.Pulse(2.4f);
                int b = SpeechBubbles.SayAt(p.Anchor.position, kv.Value, 2.4f);
                until = Time.unscaledTime + 2.5f;
                while (Time.unscaledTime < until) { if (Taps != taps || State != VistaState.Vista) { SpeechBubbles.Close(b); p.Pulse(0f); Welcoming = false; yield break; } yield return null; }
            }
            Welcoming = false;
        }
    }
}
