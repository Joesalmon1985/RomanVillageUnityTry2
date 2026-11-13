using System;
using System.Collections.Generic;
using UnityEngine;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The player's floating viewpoint on the walking lattice (REFACTOR_SPEC 2.D, appendix B), Dungeon Master style.
    /// Owner: package P.
    /// <list type="bullet">
    /// <item>Moves: forward one cell in the heading, step back one cell against it, turn left (h - 1) or right (h + 1).
    /// Steps glide over <see cref="StepSeconds"/> (ease in-out), turns over <see cref="TurnSeconds"/>; holding a key
    /// repeats; one further input is buffered. A move into a blocked cell (<see cref="WalkGrid"/>) bumps and stays.</item>
    /// <item>Keys: W / Up / Numpad 8 forward; S / Down / Numpad 2 / Numpad 5 back; A / Left / Numpad 4 / Numpad 7 turn
    /// left; D / Right / Numpad 6 / Numpad 9 turn right; touch strips 1-4 = left, forward, back, right.</item>
    /// <item>Eye <see cref="WorldBootstrap.EyeHeight"/> above the ground (terrain height followed), fixed pitch (8 degrees
    /// down; <see cref="Pitch"/> uses Unity's sign, positive = down), field of view 70, no mouse look.</item>
    /// <item>Auto-walk (<see cref="WalkTo"/>) and follow (<see cref="Follow"/>) step along <see cref="WalkGrid.FindPath"/>;
    /// any movement key, or a right click (<see cref="PointerInput"/>), stops them.</item>
    /// </list>
    /// Transform position = feet; yaw on this transform, pitch on <see cref="Head"/> (the main camera's parent).
    /// </summary>
    public sealed class PlayerWalker : MonoBehaviour
    {
        public const float StepSeconds = 0.4f, TurnSeconds = 0.25f, BumpSeconds = 0.2f, BumpDistance = 0.7f;
        /// <summary>Unity pitch (positive looks down) of the fixed view: the spec's -8 degrees of elevation.</summary>
        public const float FixedPitch = 8f;
        public const float FieldOfView = 70f;
        /// <summary>Follow stands this many cells behind the person.</summary>
        public const int FollowCells = 2;

        public static PlayerWalker Instance { get; private set; }

        /// <summary>The cell the viewpoint stands on (during a glide: the cell it is gliding to).</summary>
        public Cell Cell { get; private set; }
        /// <summary>0..5, see <see cref="WalkLattice.Yaw"/>.</summary>
        public int Heading { get; private set; }
        /// <summary>Feet position (on the ground).</summary>
        public Vector3 Position => transform.position;
        public Vector3 EyePosition => Head != null ? Head.position : transform.position + Vector3.up * WorldBootstrap.EyeHeight;
        public float Yaw => _yaw;
        /// <summary>Unity pitch of the view (positive = down): <see cref="FixedPitch"/> except while an encounter placed it.</summary>
        public float Pitch => _pitch;
        /// <summary>The view direction (the old avatar's <c>Forward</c>).</summary>
        public Vector3 LookDirection => Head != null ? Head.forward : transform.forward;
        /// <summary>True while a step, turn or bump is animating.</summary>
        public bool IsGliding => _action.Kind != ActionKind.None;
        public IFollowable Following { get; private set; }
        /// <summary>True while an auto-walk (map target, follow, AutoPlay) is under way.</summary>
        public bool IsAutoWalking => _autoGoal.HasValue || Following != null;
        /// <summary>Goal of the current auto-walk, if any.</summary>
        public Cell? AutoGoal => _autoGoal;
        /// <summary>True when the viewpoint stands off the lattice (an encounter placed it, or a legacy exact teleport).</summary>
        public bool IsOffLattice { get; private set; }

        public Camera Camera { get; private set; }
        public Transform Head { get; private set; }
        /// <summary>Arrow and beacon shown in the F1 God View where the player stands.</summary>
        public GameObject Marker { get; private set; }

        /// <summary>Raised after every completed step (not turns).</summary>
        public event Action Stepped;
        /// <summary>Raised when a move bumps into a blocked cell (the cell it tried).</summary>
        public event Action<Cell> Bumped;
        /// <summary>Raised when an auto-walk ends: true = arrived, false = stopped or no path.</summary>
        public event Action<bool> AutoWalkEnded;

        /// <summary>Steps taken and bumps since boot (tests, diagnostics).</summary>
        public int StepCount { get; private set; }
        public int BumpCount { get; private set; }

        private readonly HashSet<string> _locks = new HashSet<string>();
        public bool IsLocked => _locks.Count > 0;
        public bool CanMove => _locks.Count == 0;
        /// <summary>There is no mouse look; kept for callers of the old avatar (same as <see cref="CanMove"/>).</summary>
        public bool CanLook => _locks.Count == 0;

        private float _yaw, _pitch = FixedPitch;

        private enum ActionKind { None, Step, Turn, Bump }
        private enum Move { None, Forward, Back, Left, Right }
        private struct Action_
        {
            public ActionKind Kind; public float T, Duration;
            public Vector3 From, To; public float FromYaw, ToYaw;
        }
        private Action_ _action;
        private Move _buffered;

        private Cell? _autoGoal;
        private List<Cell> _path;
        private Cell? _followGoal;
        private Vector3 _followLastPos, _followDir;
        private float _followStill;

        // ---------------- creation ----------------

        public static PlayerWalker Create(Transform parent, Camera cam, Cell cell, int heading)
        {
            var go = new GameObject("Player");
            go.layer = 2; // Ignore Raycast: the pointer never hits the player
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<PlayerWalker>();
            w.Head = new GameObject("Head").transform;
            w.Head.SetParent(go.transform, false);
            w.Head.localPosition = new Vector3(0, WorldBootstrap.EyeHeight, 0);
            w.Camera = cam;
            if (cam != null)
            {
                cam.transform.SetParent(w.Head, false);
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                cam.fieldOfView = FieldOfView;
            }
            w.BuildMarker();
            w.Teleport(cell, heading);
            return w;
        }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void BuildMarker()
        {
            Marker = new GameObject("GodViewMarker");
            Marker.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            var col = new Color(1f, 0.85f, 0.2f);
            mb.Quad(new Vector3(-1.2f, 0.4f, -2f), new Vector3(-1.2f, 0.4f, 1f), new Vector3(1.2f, 0.4f, 1f), new Vector3(1.2f, 0.4f, -2f), col);
            mb.Tri(new Vector3(-2.8f, 0.4f, 1f), new Vector3(0, 0.4f, 4.2f), new Vector3(2.8f, 0.4f, 1f), col);
            mb.Cylinder(Vector3.zero, 0.5f, 0.5f, 6f, 8, new Color(1f, 0.55f, 0.1f));
            mb.Box(new Vector3(0, 6.5f, 0), new Vector3(2f, 2f, 2f), col);
            mb.ToObject("Arrow", Marker.transform, MaterialFactory.CreateUnlit(Color.white, "PlayerMarker"));
            Marker.SetActive(false);
        }

        // ---------------- locks ----------------

        public void AddLock(string reason) { if (_locks.Add(reason)) CancelHeldInput(); }
        public void RemoveLock(string reason) => _locks.Remove(reason);
        public bool HasLock(string reason) => _locks.Contains(reason);
        /// <summary>Old avatar names (movement and look are one lock now).</summary>
        public void AddMoveLock(string reason) => AddLock(reason);
        public void RemoveMoveLock(string reason) => RemoveLock(reason);
        public void AddLookLock(string reason) => AddLock(reason);
        public void RemoveLookLock(string reason) => RemoveLock(reason);
        private void CancelHeldInput() { _buffered = Move.None; }

        // ---------------- moves ----------------

        public void Forward() => Request(Move.Forward);
        public void Back() => Request(Move.Back);
        public void TurnLeft() => Request(Move.Left);
        public void TurnRight() => Request(Move.Right);

        /// <summary>Touch strip 1-4 to its move (1 turn left, 2 forward, 3 back, 4 turn right).</summary>
        public void PressStrip(int strip)
        {
            switch (strip)
            {
                case 1: TurnLeft(); break;
                case 2: Forward(); break;
                case 3: Back(); break;
                case 4: TurnRight(); break;
            }
        }

        private void Request(Move m)
        {
            if (m == Move.None || IsLocked) return;
            if (IsGliding) { _buffered = m; return; }
            Begin(m);
        }

        private void Begin(Move m)
        {
            switch (m)
            {
                case Move.Forward: BeginStep(Cell + WalkLattice.Dir(Heading)); break;
                case Move.Back: BeginStep(Cell - WalkLattice.Dir(Heading)); break;
                case Move.Left: BeginTurn(Heading - 1, TurnSeconds); break;
                case Move.Right: BeginTurn(Heading + 1, TurnSeconds); break;
            }
        }

        private void BeginStep(Cell to)
        {
            var from = transform.position;
            if (WalkGrid.IsBlocked(to))
            {
                var dir = WalkLattice.ToWorld(to) - WalkLattice.ToWorld(Cell); dir.y = 0;
                var home = IsOffLattice ? from : CellPosition(Cell);
                _action = new Action_ { Kind = ActionKind.Bump, Duration = BumpSeconds, From = home, To = home + dir.normalized * BumpDistance, FromYaw = _yaw, ToYaw = _yaw };
                BumpCount++;
                Bumped?.Invoke(to);
                return;
            }
            Cell = to;
            IsOffLattice = false;
            _action = new Action_ { Kind = ActionKind.Step, Duration = StepSeconds, From = from, To = CellPosition(to), FromYaw = _yaw, ToYaw = _yaw + Mathf.DeltaAngle(_yaw, WalkLattice.Yaw(Heading)) };
            _pitch = FixedPitch;
        }

        private void BeginTurn(int toHeading, float seconds)
        {
            int h = Mod6(toHeading);
            float target = WalkLattice.Yaw(h);
            Heading = h;
            _action = new Action_ { Kind = ActionKind.Turn, Duration = seconds, From = transform.position, To = transform.position, FromYaw = _yaw, ToYaw = _yaw + Mathf.DeltaAngle(_yaw, target) };
            _pitch = FixedPitch;
        }

        /// <summary>Puts the viewpoint on a cell with a heading at once (load, tests). Ends any glide; keeps auto-walks off.</summary>
        public void Teleport(Cell cell, int heading)
        {
            Cell = cell; Heading = Mod6(heading);
            _action = default; _buffered = Move.None;
            IsOffLattice = false;
            _yaw = WalkLattice.Yaw(Heading); _pitch = FixedPitch;
            ApplyPose(CellPosition(cell));
        }

        /// <summary>
        /// Encounters: an exact pose off the lattice (feet position, yaw, Unity pitch); <see cref="RestoreCell"/> snaps
        /// back to the cell and heading. Use <c>EncounterContext.PlaceAvatar</c> rather than calling this directly.
        /// </summary>
        public void PlaceExact(Vector3 position, float yaw, float pitch)
        {
            _action = default; _buffered = Move.None;
            IsOffLattice = true;
            _yaw = yaw; _pitch = pitch;
            ApplyPose(position);
        }

        /// <summary>Back onto the stored cell and heading (encounter end).</summary>
        public void RestoreCell() => Teleport(Cell, Heading);

        /// <summary>
        /// Legacy exact teleport (save files of the old pose, old scripts): stands exactly at <paramref name="feet"/>
        /// with that yaw and pitch, and takes the nearest cell and heading as its lattice pose (the next step glides
        /// onto the lattice). New code uses <see cref="Teleport(Cell,int)"/>.
        /// </summary>
        public void Teleport(Vector3 feet, float yaw, float pitch)
        {
            Cell = WalkLattice.ToCell(feet);
            Heading = WalkLattice.HeadingOf(yaw);
            var onCell = CellPosition(Cell);
            bool exact = BoardSpace.Flat(feet - onCell) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(yaw, WalkLattice.Yaw(Heading))) > 0.01f || Mathf.Abs(pitch - FixedPitch) > 0.01f;
            PlaceExact(feet, yaw, pitch);
            IsOffLattice = exact;
        }

        // ---------------- auto-walk and follow ----------------

        /// <summary>Walks along the lattice to a cell (path round blocked cells). Returns false if there is no path.</summary>
        public bool WalkTo(Cell target)
        {
            Following = null; _followGoal = null;
            return PlanTo(target);
        }

        private bool PlanTo(Cell target)
        {
            var path = WalkGrid.FindPath(Cell, target);
            if (path == null) { _autoGoal = null; _path = null; return false; }
            _autoGoal = target; _path = path;
            return true;
        }

        /// <summary>Follows a person: auto-walks to the cell <see cref="FollowCells"/> steps behind them, re-planning as they move.</summary>
        public void Follow(IFollowable who)
        {
            StopAuto(false);
            if (who == null || who.FollowTransform == null) return;
            Following = who;
            _followLastPos = who.FollowTransform.position;
            _followDir = Flat(who.FollowTransform.forward);
            if (_followDir.sqrMagnitude < 1e-4f) _followDir = Vector3.forward;
            _followStill = 0f;
            _followGoal = null;
        }

        /// <summary>Stops an auto-walk or follow (the current glide finishes).</summary>
        public void StopAuto() => StopAuto(true);

        private void StopAuto(bool raise)
        {
            bool was = IsAutoWalking;
            _autoGoal = null; _path = null; Following = null; _followGoal = null;
            if (was && raise) AutoWalkEnded?.Invoke(false);
        }

        // ---------------- update ----------------

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!IsLocked) ReadInput();
            Animate(dt);
            if (IsGliding || IsLocked) return;
            if (_buffered != Move.None) { var m = _buffered; _buffered = Move.None; Begin(m); return; }
            if (UpdateAuto()) return;
            if (!IsLocked) RepeatHeld();
        }

        private static bool AnyKey(KeyCode a, KeyCode b, KeyCode c, KeyCode d, Func<KeyCode, bool> f) =>
            f(a) || f(b) || f(c) || (d != KeyCode.None && f(d));

        private static Move KeyMove(Func<KeyCode, bool> f)
        {
            if (AnyKey(KeyCode.W, KeyCode.UpArrow, KeyCode.Keypad8, KeyCode.None, f)) return Move.Forward;
            if (AnyKey(KeyCode.S, KeyCode.DownArrow, KeyCode.Keypad2, KeyCode.Keypad5, f)) return Move.Back;
            if (AnyKey(KeyCode.A, KeyCode.LeftArrow, KeyCode.Keypad4, KeyCode.Keypad7, f)) return Move.Left;
            if (AnyKey(KeyCode.D, KeyCode.RightArrow, KeyCode.Keypad6, KeyCode.Keypad9, f)) return Move.Right;
            return Move.None;
        }

        private static Move StripMove(Func<int, bool> f)
        {
            if (f(2)) return Move.Forward;
            if (f(3)) return Move.Back;
            if (f(1)) return Move.Left;
            if (f(4)) return Move.Right;
            return Move.None;
        }

        /// <summary>True while keyboard movement is taken by something else (map, menu, God View, a text field).</summary>
        private static bool InputCaptured =>
            Hud.MenuOpen || MapScreen.IsOpen || (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God);

        private void ReadInput()
        {
            if (InputCaptured) return;
            var m = KeyMove(Input.GetKeyDown);
            if (m == Move.None) m = StripMove(TouchStrips.Pressed);
            if (m == Move.None) return;
            if (IsAutoWalking) StopAuto();
            Request(m);
        }

        private void RepeatHeld()
        {
            if (InputCaptured) return;
            var m = KeyMove(Input.GetKey);
            if (m == Move.None) m = StripMove(TouchStrips.Held);
            if (m != Move.None) Begin(m);
        }

        private void Animate(float dt)
        {
            if (_action.Kind == ActionKind.None) return;
            _action.T += dt;
            float u = Mathf.Clamp01(_action.T / Mathf.Max(0.0001f, _action.Duration));
            float e = u * u * (3f - 2f * u); // ease in-out
            switch (_action.Kind)
            {
                case ActionKind.Step:
                    _yaw = Mathf.Lerp(_action.FromYaw, _action.ToYaw, e);
                    ApplyPose(Vector3.Lerp(_action.From, _action.To, e), true);
                    break;
                case ActionKind.Turn:
                    _yaw = Mathf.Lerp(_action.FromYaw, _action.ToYaw, e);
                    ApplyPose(transform.position, false);
                    break;
                case ActionKind.Bump:
                    float b = Mathf.Sin(u * Mathf.PI); // out and back
                    ApplyPose(Vector3.Lerp(_action.From, _action.To, b * 0.999f), false);
                    break;
            }
            if (u >= 1f)
            {
                var kind = _action.Kind;
                var bumpHome = _action.From;
                _action = default;
                if (kind == ActionKind.Step)
                {
                    _yaw = WalkLattice.Yaw(Heading);
                    ApplyPose(CellPosition(Cell));
                    StepCount++;
                    Stepped?.Invoke();
                }
                else if (kind == ActionKind.Turn) { _yaw = WalkLattice.Yaw(Heading); ApplyPose(transform.position, false); }
                else if (kind == ActionKind.Bump) ApplyPose(bumpHome, false);
            }
        }

        /// <summary>Advances the auto-walk or follow by one action. True if it started one.</summary>
        private bool UpdateAuto()
        {
            if (Following != null) UpdateFollowGoal();
            if (!_autoGoal.HasValue || _path == null) return false;
            if (_path.Count == 0)
            {
                bool following = Following != null;
                if (!following) { _autoGoal = null; _path = null; AutoWalkEnded?.Invoke(true); }
                else FaceFollowed();
                return false;
            }
            var next = _path[0];
            if (WalkGrid.IsBlocked(next) || WalkGrid.Distance(Cell, next) != 1)
            {
                // Something changed (a site finished on the way): re-plan from here.
                if (!PlanTo(_autoGoal.Value)) { StopAuto(false); AutoWalkEnded?.Invoke(false); return false; }
                if (_path.Count == 0) return false;
                next = _path[0];
            }
            int h = HeadingTowards(Cell, next);
            if (h != Heading)
            {
                int turns = Mathf.Abs(Mathf.RoundToInt(Mathf.DeltaAngle(WalkLattice.Yaw(Heading), WalkLattice.Yaw(h)) / 60f));
                BeginTurn(h, TurnSeconds * (0.6f + 0.4f * turns));
                return true;
            }
            _path.RemoveAt(0);
            BeginStep(next);
            return true;
        }

        private void UpdateFollowGoal()
        {
            var t = Following.FollowTransform;
            if (t == null || (Following is UnityEngine.Object o && o == null)) { StopAuto(); return; }
            var p = t.position;
            var moved = Flat(p - _followLastPos);
            if (moved.magnitude > 0.05f) { _followDir = moved.normalized; _followStill = 0f; _followLastPos = p; }
            else _followStill += Time.deltaTime;
            var behind = WalkLattice.ToCell(p - _followDir * (FollowCells * WalkLattice.Step));
            var free = WalkGrid.NearestFree(behind, 3);
            if (!free.HasValue) return;
            var want = free.Value;
            bool replan = !_followGoal.HasValue
                || WalkGrid.Distance(want, _followGoal.Value) > 1
                || (_followStill > 0.3f && want != _followGoal.Value);
            if (!replan) return;
            _followGoal = want;
            var path = WalkGrid.FindPath(Cell, want);
            if (path == null) return;
            _autoGoal = want; _path = path;
        }

        private void FaceFollowed()
        {
            var t = Following.FollowTransform;
            if (t == null) return;
            var d = Flat(t.position - transform.position);
            if (d.magnitude < 1f) return;
            int h = WalkLattice.HeadingOf(BoardSpace.YawOf(d));
            if (h != Heading) BeginTurn(h, TurnSeconds);
        }

        /// <summary>Heading index of the step from a to its neighbour b (-1 if not neighbours).</summary>
        public static int HeadingTowards(Cell a, Cell b)
        {
            var d = b - a;
            for (int h = 0; h < 6; h++) if (WalkLattice.Dir(h) == d) return h;
            return WalkLattice.HeadingOf(BoardSpace.YawOf(WalkLattice.ToWorld(b) - WalkLattice.ToWorld(a)));
        }

        // ---------------- pose ----------------

        /// <summary>Ground position of a cell including the terrain height.</summary>
        public static Vector3 CellPosition(Cell c)
        {
            var p = WalkLattice.ToWorld(c);
            p.y = GroundHeight(p);
            return p;
        }

        private static readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Height of the walkable ground (hex tiles) under a point, else <see cref="BoardSpace.GroundY"/>.</summary>
        public static float GroundHeight(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(new Ray(new Vector3(p.x, 120f, p.z), Vector3.down), _hits, 200f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.normal.y < 0.6f) continue;
                if (h.collider.GetComponentInParent<HexView>() == null) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return float.IsNegativeInfinity(best) ? BoardSpace.GroundY : best;
        }

        private void ApplyPose(Vector3 feet, bool sampleGround = true)
        {
            if (sampleGround && !IsOffLattice) feet.y = GroundHeight(feet);
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0, _yaw, 0));
            if (Head != null) Head.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        private static int Mod6(int h) => ((h % 6) + 6) % 6;
    }
}
