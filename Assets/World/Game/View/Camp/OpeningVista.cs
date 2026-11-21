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
    /// The opening vista (owner feedback, 21 Nov 2026): the game opens on a composed, elevated night view of the hub in which all six
    /// destinations are visible at once (temple, council tower, dance circle, healer's lodge, Mira's expedition camp and cave, duel ring).
    /// The screen is divided into invisible regions, one per place, assigned from each place's projected bounds every frame
    /// (<see cref="RegionAt"/>): a tap inside one place's projected box picks it, overlapping boxes go to the nearest centre, and every
    /// other point goes to the nearest box, so the whole screen is covered at any aspect ratio. Tap a region once: that place lights up
    /// and the shared <see cref="LedgerCard"/> names the activity (a <see cref="VistaPlace"/> on the WU-0 two-tap machine). Tap it
    /// again (or the card, or E): the camera glides down to the player standing at the place and the place's existing entry runs
    /// (<see cref="VistaPlace.Enter"/>) with no further tap: the vista's preview already was the preview, so the second tap is the
    /// confirm. Back to the vista: Esc (or a phone's back key) in the walking view, or a tap on the sky. W or the up arrow in the
    /// vista steps down into the walking view where the player stands. Drag in the vista swings the view a little round the camp.
    /// Reads state only; the entries are the same calls the hub's people and props make. Owner: opening vista.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class OpeningVista : MonoBehaviour
    {
        public const string Lock = "Vista";
        public const float DescendSeconds = 1.4f, RiseSeconds = 1.0f, FieldOfView = 34f;
        /// <summary>Downward pitch of the vista camera (chosen with the bearing, see <see cref="ChooseYaw"/>).</summary>
        public float Pitch { get; private set; } = 40f;
        /// <summary>Widest swing (degrees) of a drag round the camp.</summary>
        public const float MaxOrbit = 35f;

        /// <summary>Set by tests and AutoPlay. Not reset per session (the PlayMode test assembly turns it off).</summary>
        public static VistaMode Mode = VistaMode.On;
        public static OpeningVista Instance { get; private set; }

        /// <summary>True while the vista owns the camera (up, or gliding).</summary>
        public static bool Active => Instance != null && Instance.State != VistaState.Off;
        /// <summary>True while the player looks out over the camp (taps pick regions).</summary>
        public static bool Up => Instance != null && Instance.State == VistaState.Vista;

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

        public Vector3 Focus { get; private set; }
        public Vector3 CameraPosition { get; private set; }
        public Quaternion CameraRotation { get; private set; }

        private readonly List<VistaPlace> _places = new List<VistaPlace>();
        private WorldBootstrap _boot;
        private float _baseYaw, _orbit, _distance;
        private int _fitW, _fitH; private float _fitOrbit = float.NaN;
        private float _t; private Vector3 _fromPos, _toPos; private Quaternion _fromRot, _toRot; private float _fromFov, _toFov;
        private VistaPlace _going;
        private bool _fogWas; private bool _fogSaved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static OpeningVista Create(WorldBootstrap boot, Transform root, CampLayout layout)
        {
            var go = new GameObject("Opening vista");
            go.transform.SetParent(root, false);
            var v = go.AddComponent<OpeningVista>();
            v._boot = boot;
            Instance = v;
            VistaPlaces.Build(v, go.transform, layout, v._places);
            v.ChooseYaw();
            VistaPlaces.BuildScenery(go.transform, layout, v._places, v._baseYaw);
            VistaTerrain.BuildBackdrop(go.transform, v._places, v.Focus, v._baseYaw);
            return v;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private IEnumerator Start()
        {
            yield return null; // every installer has run and the walker stands on its spawn
            if (WillOpen && _places.Count > 0) Open(true);
        }

        // ------------------------------------------------------------------ pose

        private Camera Cam => _boot != null && _boot.Avatar != null ? _boot.Avatar.Camera : Camera.main;

        /// <summary>
        /// Picks where to look from: every 15 degrees of bearing at three pitches, fitted so all places are in frame. A pose is
        /// rejected when the island's hills would hide a place or the camera would sit in a hillside; of the rest, the score favours
        /// places spread apart on screen and of similar size (near and far not too different), and a short distance.
        /// </summary>
        private void ChooseYaw()
        {
            Focus = Centroid();
            float aspect = 16f / 9f, bestScore = float.MinValue;
            _baseYaw = 0f; float bestPitch = 40f;
            foreach (float pitch in new[] { 36f, 44f, 52f })
            {
                Pitch = pitch;
                for (int i = 0; i < 24; i++)
                {
                    float yaw = i * 15f;
                    float d = FitDistance(yaw, aspect, Focus);
                    var pos = Pos(Focus, yaw, d);
                    if (!Clear(pos)) continue;
                    float near = float.MaxValue, far = 0f;
                    foreach (var p in _places) { float dd = Vector3.Distance(pos, p.Centre); near = Mathf.Min(near, dd); far = Mathf.Max(far, dd); }
                    float score = Spread(yaw, aspect, d, Focus) * 4f - (far / Mathf.Max(1f, near)) * 0.35f - d / 400f;
                    if (score > bestScore) { bestScore = score; _baseYaw = yaw; bestPitch = pitch; }
                }
            }
            Pitch = bestPitch;
        }

        /// <summary>True when the camera stands well above the ground under it and every place can be seen over the hills.</summary>
        private bool Clear(Vector3 cam)
        {
            if (cam.y < PlayerWalker.GroundHeight(cam) + 14f) return false;
            foreach (var p in _places)
            {
                var target = p.Centre + Vector3.up * Mathf.Min(2f, p.Height);
                for (int k = 1; k < 12; k++)
                {
                    var q = Vector3.Lerp(cam, target, k / 12f);
                    if (PlayerWalker.GroundHeight(q) > q.y - 1f) return false;
                }
            }
            return true;
        }

        private Vector3 Centroid()
        {
            var c = Vector3.zero;
            foreach (var p in _places) c += p.Centre;
            c /= Mathf.Max(1, _places.Count);
            c.y = 0f;
            return c;
        }

        private Quaternion Rot(float yaw) => Quaternion.Euler(Pitch, yaw, 0f);
        private Vector3 Pos(Vector3 focus, float yaw, float dist) => focus - Rot(yaw) * Vector3.forward * dist;

        /// <summary>The smallest distance at which every place's bounds project inside the frame's margins.</summary>
        private float FitDistance(float yaw, float aspect, Vector3 focus)
        {
            float lo = 20f, hi = 400f;
            for (int it = 0; it < 22; it++)
            {
                float mid = (lo + hi) * 0.5f;
                if (AllInside(yaw, aspect, mid, focus)) hi = mid; else lo = mid;
            }
            return hi;
        }

        private bool AllInside(float yaw, float aspect, float dist, Vector3 focus)
        {
            var pos = Pos(focus, yaw, dist); var rot = Rot(yaw);
            foreach (var p in _places)
                foreach (var w in p.Samples())
                {
                    var v = Project(pos, rot, aspect, w);
                    if (v.z <= 0.5f) return false;
                    // GUI-ish margins: a little room at the sides, more at the top for the cards and at the bottom for the notch band.
                    if (v.x < 0.06f || v.x > 0.94f || v.y < 0.07f || v.y > 0.86f) return false;
                }
            return true;
        }

        private float Spread(float yaw, float aspect, float dist, Vector3 focus)
        {
            var pos = Pos(focus, yaw, dist); var rot = Rot(yaw);
            float min = float.MaxValue;
            for (int i = 0; i < _places.Count; i++)
                for (int j = i + 1; j < _places.Count; j++)
                {
                    var a = Project(pos, rot, aspect, _places[i].Centre); var b = Project(pos, rot, aspect, _places[j].Centre);
                    min = Mathf.Min(min, new Vector2((a.x - b.x) * aspect, a.y - b.y).magnitude);
                }
            return min;
        }

        /// <summary>Viewport point (0..1, origin bottom-left; z = depth) of a world point for a pinhole camera at this pose.</summary>
        private static Vector3 Project(Vector3 camPos, Quaternion camRot, float aspect, Vector3 world)
        {
            var local = Quaternion.Inverse(camRot) * (world - camPos);
            if (local.z <= 0.01f) return new Vector3(0.5f, 0.5f, local.z);
            float t = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector3(0.5f + local.x / (local.z * t * aspect) * 0.5f, 0.5f + local.y / (local.z * t) * 0.5f, local.z);
        }

        /// <summary>Fits the pose to the screen now (aspect and drag swing), then centres the places' box on the screen.</summary>
        private void Refit()
        {
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (w == _fitW && h == _fitH && Mathf.Approximately(_fitOrbit, _orbit)) return;
            _fitW = w; _fitH = h; _fitOrbit = _orbit;
            float aspect = (float)w / h, yaw = _baseYaw + _orbit;
            var centroid = Centroid();
            var focus = centroid;
            for (int pass = 0; pass < 6; pass++)
            {
                _distance = FitDistance(yaw, aspect, focus);
                // centre the projected box of every place
                var pos = Pos(focus, yaw, _distance); var rot = Rot(yaw);
                float x0 = 1, x1 = 0, y0 = 1, y1 = 0;
                foreach (var p in _places) foreach (var s in p.Samples()) { var v = Project(pos, rot, aspect, s); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
                float dx = (x0 + x1) * 0.5f - 0.5f, dy = (y0 + y1) * 0.5f - 0.45f;
                var right = rot * Vector3.right; right.y = 0; right.Normalize();
                var fwd = rot * Vector3.forward; fwd.y = 0; fwd.Normalize();
                float span = _distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
                focus += (right * dx * span * aspect + fwd * dy * span / Mathf.Sin(Pitch * Mathf.Deg2Rad)) * 0.6f; // damped: places stand at different depths
                var off = focus - centroid; if (off.magnitude > 30f) focus = centroid + off.normalized * 30f;
            }
            Focus = focus;
            _distance = FitDistance(yaw, aspect, focus);
            CameraPosition = Pos(focus, yaw, _distance);
            CameraRotation = Rot(yaw);
        }

        /// <summary>The lookout's bearing (degrees) before any drag swing.</summary>
        public float Bearing => _baseYaw;

        /// <summary>Captures and tuning: look from this bearing and pitch.</summary>
        public void ForcePose(float yaw, float pitch) { _baseYaw = yaw; Pitch = pitch; _orbit = 0f; _fitW = 0; }

        // ------------------------------------------------------------------ regions

        /// <summary>A place's screen box (pixels, origin bottom-left) as the vista camera sees it now.</summary>
        public Rect ScreenRect(VistaPlace p)
        {
            Refit();
            float aspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var s in p.Samples())
            {
                var v = Project(CameraPosition, CameraRotation, aspect, s);
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
            }
            return Rect.MinMaxRect(x0 * Screen.width, y0 * Screen.height, x1 * Screen.width, y1 * Screen.height);
        }

        /// <summary>A place's centre on screen (pixels, origin bottom-left).</summary>
        public Vector2 ScreenCentre(VistaPlace p)
        {
            Refit();
            float aspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            var v = Project(CameraPosition, CameraRotation, aspect, p.Centre + Vector3.up * p.Height * 0.35f);
            return new Vector2(v.x * Screen.width, v.y * Screen.height);
        }

        /// <summary>
        /// The place whose region holds a screen point (pixels, origin bottom-left). Inside exactly one place's box: that place; inside
        /// several: the nearest centre among them; outside every box: the box nearest the point (so the whole screen is divided).
        /// </summary>
        public VistaPlace RegionAt(Vector2 screen)
        {
            VistaPlace inside = null; float insideD = float.MaxValue;
            VistaPlace near = null; float nearD = float.MaxValue;
            foreach (var p in _places)
            {
                var r = ScreenRect(p);
                if (r.Contains(screen))
                {
                    float d = (ScreenCentre(p) - screen).sqrMagnitude;
                    if (d < insideD) { insideD = d; inside = p; }
                    continue;
                }
                float dx = Mathf.Max(r.xMin - screen.x, 0f, screen.x - r.xMax), dy = Mathf.Max(r.yMin - screen.y, 0f, screen.y - r.yMax);
                float dd = dx * dx + dy * dy;
                if (dd < nearD) { nearD = dd; near = p; }
            }
            return inside ?? near;
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

        /// <summary>A drag in the vista swings the view round the camp (pixels since the last call).</summary>
        public void Swing(float dxPixels)
        {
            if (State != VistaState.Vista) return;
            float o = Mathf.Clamp(_orbit - dxPixels / Mathf.Max(1f, Screen.width) * 90f, -MaxOrbit, MaxOrbit);
            if (Clear(Pos(Focus, _baseYaw + o, _distance))) _orbit = o; // never swing into a hillside
        }

        private void Update()
        {
            if (State != VistaState.Vista || Hud.MenuOpen || MapScreen.IsOpen) return;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad8)) { StepDown(); return; }
            int dir = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
            if (dir != 0) CycleKey(dir);
        }

        /// <summary>A / D (arrows) in the vista preview the places left to right; E or Enter confirms (the shared machine's key).</summary>
        private void CycleKey(int dir)
        {
            var order = new List<VistaPlace>(_places);
            order.Sort((a, b) => ScreenCentre(a).x.CompareTo(ScreenCentre(b).x));
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
            _orbit = 0f; _fitW = 0;
            Refit();
            var cam = Cam;
            if (snap || cam == null) { State = VistaState.Vista; Apply(CameraPosition, CameraRotation, FieldOfView); return; }
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
            BeginDescent(p);
            return true;
        }

        private void BeginDescent(VistaPlace to)
        {
            var cam = Cam;
            _going = to;
            _fromPos = CameraPosition; _fromRot = CameraRotation; _fromFov = FieldOfView;
            if (cam != null) { _fromPos = cam.transform.position; _fromRot = cam.transform.rotation; _fromFov = cam.fieldOfView; }
            _t = 0f; State = VistaState.Descending;
        }

        private void Finish(VistaPlace arrived)
        {
            State = VistaState.Off;
            var cam = Cam;
            if (cam != null)
            {
                cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;
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
        private void HintOnce()
        {
            if (s_hinted) return;
            s_hinted = true;
            Hud.Notice("Tap the sky, or press Esc, to look out over the camp again.", false);
        }

        private void LateUpdate()
        {
            var cam = Cam;
            if (State == VistaState.Off || cam == null) return;
            WorldActions.NoteCameraMove(0f);
            switch (State)
            {
                case VistaState.Vista:
                    Refit();
                    Apply(CameraPosition, CameraRotation, FieldOfView);
                    break;
                case VistaState.Rising:
                {
                    Refit();
                    _t += Time.unscaledDeltaTime / RiseSeconds;
                    float e = Smooth(_t);
                    Apply(Arc(_fromPos, CameraPosition, e), Quaternion.Slerp(_fromRot, CameraRotation, e), Mathf.Lerp(_fromFov, FieldOfView, e));
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
                    Apply(Arc(_fromPos, eyePos, e), Quaternion.Slerp(_fromRot, eyeRot, e), Mathf.Lerp(_fromFov, PlayerWalker.FieldOfView, e));
                    WorldActions.NoteCameraMove(0.2f);
                    if (_t >= 1f) Finish(_going);
                    break;
                }
            }
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static Vector3 Arc(Vector3 a, Vector3 b, float e) => Vector3.Lerp(a, b, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * Vector3.Distance(a, b) * 0.08f;

        private void Apply(Vector3 pos, Quaternion rot, float fov)
        {
            var cam = Cam;
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
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
            var focus = Focus + Vector3.up * 6f;
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
