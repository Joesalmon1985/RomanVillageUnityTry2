using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>The shared states of a world action (WORLD_UI_PLAN P-3, WU-0).</summary>
    public enum WorldActionState
    {
        /// <summary>Nothing is shown.</summary>
        Idle,
        /// <summary>A thing that only tells (an <see cref="IInspectable"/>) has its ledger open.</summary>
        Inspect,
        /// <summary>The first tap: the action is shown as it would happen; nothing is sent yet.</summary>
        Preview,
        /// <summary>The second tap sent the command (once); the tap guard runs.</summary>
        Confirm,
        /// <summary>The action is offered but cannot be done now; the reason is said.</summary>
        Refused,
        /// <summary>The thing is not usable at all now (closed, ruined, someone else's).</summary>
        Disabled,
    }

    /// <summary>What a tap on a world action did.</summary>
    public enum WorldTapResult { Ignored, Inspected, Previewed, Confirmed, Refused, Disabled, Cancelled }

    /// <summary>Whether a world action can be done now (read from state, no side effects).</summary>
    public struct WorldActionCheck
    {
        /// <summary>False: the thing is not an action now (<see cref="WorldActionState.Disabled"/>).</summary>
        public bool Enabled;
        /// <summary>True: the action can be previewed and confirmed.</summary>
        public bool Ok;
        /// <summary>Why not, in our words (one line); null when ok.</summary>
        public string Reason;

        public static WorldActionCheck Allowed => new WorldActionCheck { Enabled = true, Ok = true };
        public static WorldActionCheck Refuse(string reason) => new WorldActionCheck { Enabled = true, Ok = false, Reason = reason };
        public static WorldActionCheck Disable(string reason) => new WorldActionCheck { Enabled = false, Ok = false, Reason = reason };
    }

    /// <summary>
    /// A thing in the world that does one command: tap once to preview, tap again to confirm (WU-0). Implemented by
    /// view components on top of <see cref="IInspectable"/> (its title and lines fill the <see cref="LedgerCard"/>).
    /// The implementation only reads state in <see cref="Check"/> and <see cref="ShowPreview"/>; only
    /// <see cref="Commit"/> sends a command.
    /// </summary>
    public interface IWorldAction : IInspectable
    {
        /// <summary>The question shown while previewing, e.g. "Sacrifice a lamb?".</summary>
        string PreviewLine { get; }
        /// <summary>Can it be done now? No side effects.</summary>
        WorldActionCheck Check();
        /// <summary>Show or hide the preview (highlight, ghost, a person turning). No command.</summary>
        void ShowPreview(bool on);
        /// <summary>Send the command, exactly once per call. True when the sim accepted it.</summary>
        bool Commit();
    }

    /// <summary>An action or inspectable that lives on the screen rather than in the scene (the altar ledge, WU-2): its rect in GUI coordinates (top-left origin), for the ledger card to unfold from.</summary>
    public interface IScreenAnchored
    {
        bool TryGetScreenRect(out Rect gui);
    }

    /// <summary>A world action that is a scene object: where the ledger unfolds from and how far a key press reaches.</summary>
    public interface IWorldActionAnchor
    {
        Transform Anchor { get; }
        /// <summary>Metres from the player within which the keyboard alternative (E) picks this action; 0 = never.</summary>
        float KeyReach { get; }
    }

    /// <summary>
    /// The preview / confirm / cancel state machine of WU-0, as plain logic (no Unity objects; times are passed in so
    /// EditMode tests drive it). One machine is shared by the whole world (<see cref="WorldActions.Machine"/>).
    /// Rules: the first tap on an action previews it; a second tap on the same action confirms it and sends its command
    /// once; a tap on anything else (or <see cref="Cancel"/>) cancels; a refused or disabled action never previews.
    /// The tap guard ignores taps for <see cref="GuardSeconds"/> after each command and while the camera moves; a
    /// second tap sooner than <see cref="MinConfirmSeconds"/> after the preview (a bounced or double tap) is ignored,
    /// so a fast double tap previews but never confirms. A preview left for <see cref="PreviewTimeoutSeconds"/>
    /// lapses: a late tap previews again instead of confirming.
    /// </summary>
    public sealed class WorldActionMachine
    {
        public const float DefaultGuardSeconds = 0.6f;
        public const float DefaultMinConfirmSeconds = 0.25f;
        public const float DefaultPreviewTimeoutSeconds = 12f;

        public float GuardSeconds = DefaultGuardSeconds;
        public float MinConfirmSeconds = DefaultMinConfirmSeconds;
        public float PreviewTimeoutSeconds = DefaultPreviewTimeoutSeconds;

        public WorldActionState State { get; private set; } = WorldActionState.Idle;
        /// <summary>The action previewed, confirmed, refused or disabled last; the thing inspected; or null.</summary>
        public IInspectable Current { get; private set; }
        /// <summary>Reason of the last refusal or disabled state.</summary>
        public string Reason { get; private set; }
        /// <summary>Commands sent through this machine (one per confirm).</summary>
        public int CommandsSent { get; private set; }
        /// <summary>Whether the last confirm's command was accepted.</summary>
        public bool LastCommitOk { get; private set; }

        private float _previewAt = float.NegativeInfinity, _guardUntil = float.NegativeInfinity;

        /// <summary>Raised whenever the state or the current thing changes.</summary>
        public event Action<WorldActionMachine> Changed;

        /// <summary>True while taps are ignored (after a command, or the camera is moving).</summary>
        public bool Guarded(float now, bool cameraMoving) => cameraMoving || now < _guardUntil;

        /// <summary>A tap on a world action.</summary>
        public WorldTapResult Tap(IWorldAction action, float now, bool cameraMoving = false)
        {
            if (action == null) return TapElsewhere(now, cameraMoving);
            if (Guarded(now, cameraMoving)) return WorldTapResult.Ignored;
            Lapse(now);
            if (State == WorldActionState.Preview && ReferenceEquals(Current, action))
            {
                if (now - _previewAt < MinConfirmSeconds) return WorldTapResult.Ignored; // a double tap is one tap
                var again = action.Check();
                if (!again.Ok) { EndPreview(); return Refuse(action, again, now); }
                EndPreview();
                Current = action;
                CommandsSent++;
                LastCommitOk = action.Commit();
                State = WorldActionState.Confirm;
                _guardUntil = now + GuardSeconds;
                Raise();
                return WorldTapResult.Confirmed;
            }
            if (State == WorldActionState.Preview) EndPreview();
            var c = action.Check();
            if (!c.Ok) return Refuse(action, c, now);
            Current = action; Reason = null;
            State = WorldActionState.Preview;
            _previewAt = now;
            action.ShowPreview(true);
            Raise();
            return WorldTapResult.Previewed;
        }

        /// <summary>A tap on a thing that only tells (opens its ledger). Cancels a preview first.</summary>
        public WorldTapResult Inspect(IInspectable thing, float now, bool cameraMoving = false)
        {
            if (thing is IWorldAction wa) return Tap(wa, now, cameraMoving);
            if (thing == null) return TapElsewhere(now, cameraMoving);
            if (Guarded(now, cameraMoving)) return WorldTapResult.Ignored;
            if (State == WorldActionState.Preview) EndPreview();
            Current = thing; Reason = null; State = WorldActionState.Inspect; _previewAt = now;
            Raise();
            return WorldTapResult.Inspected;
        }

        /// <summary>A tap on nothing in particular (the ground, empty sky, another thing): cancels what is open.</summary>
        public WorldTapResult TapElsewhere(float now, bool cameraMoving = false)
        {
            if (State == WorldActionState.Idle) return WorldTapResult.Ignored;
            Cancel();
            return WorldTapResult.Cancelled;
        }

        /// <summary>Close whatever is open; never sends anything.</summary>
        public void Cancel()
        {
            if (State == WorldActionState.Idle) return;
            if (State == WorldActionState.Preview) EndPreview();
            State = WorldActionState.Idle; Current = null; Reason = null;
            Raise();
        }

        /// <summary>Per frame: a confirm returns to idle once its guard has run; a preview lapses after its timeout.</summary>
        public void Tick(float now)
        {
            if (State == WorldActionState.Confirm && now >= _guardUntil) { State = WorldActionState.Idle; Current = null; Raise(); }
            Lapse(now);
        }

        private void Lapse(float now)
        {
            bool open = State == WorldActionState.Preview || State == WorldActionState.Refused || State == WorldActionState.Disabled || State == WorldActionState.Inspect;
            if (open && now - _previewAt > PreviewTimeoutSeconds) Cancel();
        }

        private WorldTapResult Refuse(IWorldAction action, WorldActionCheck c, float now)
        {
            Current = action; Reason = c.Reason; _previewAt = now;
            State = c.Enabled ? WorldActionState.Refused : WorldActionState.Disabled;
            Raise();
            return c.Enabled ? WorldTapResult.Refused : WorldTapResult.Disabled;
        }

        private void EndPreview()
        {
            if (Current is IWorldAction a) a.ShowPreview(false);
        }

        private void Raise() => Changed?.Invoke(this);
    }

    /// <summary>Hit-target sizing from the screen height (WU-0, P-6): every tap target at least 7 % of the screen height.</summary>
    public static class WorldHitTarget
    {
        public const float MinFraction = 0.07f;

        /// <summary>Smallest target side in pixels for a screen this tall (28 px at 390 is below the 44 px rule, so the larger wins).</summary>
        public static float MinSide(float screenHeight) => Mathf.Max(Mathf.Ceil(screenHeight * MinFraction), DuelLayout.MinTouch * Mathf.Min(1f, screenHeight / 390f));

        /// <summary>The rect grown about its centre to at least <see cref="MinSide"/> on both sides.</summary>
        public static Rect Enlarge(Rect r, float screenHeight)
        {
            float m = MinSide(screenHeight);
            float w = Mathf.Max(r.width, m), h = Mathf.Max(r.height, m);
            return new Rect(r.center.x - w / 2, r.center.y - h / 2, w, h);
        }

        public static bool Meets(Rect r, float screenHeight) => r.width >= MinSide(screenHeight) - 0.5f && r.height >= MinSide(screenHeight) - 0.5f;

        /// <summary>Screen bounds (GUI coordinates, top-left origin) of an object's renderers and colliders; false if behind the camera.</summary>
        public static bool ScreenBounds(Component c, Camera cam, out Rect rect)
        {
            rect = default;
            if (c == null) return false;
            var lp = c.GetComponent<LedgeProxy>(); // the altar ledge lives on the screen, not in the scene
            if (lp != null) return lp.TryGetScreenRect(out rect);
            if (cam == null) return false;
            bool any = false; var b = new Bounds();
            foreach (var r in c.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            foreach (var col in c.GetComponentsInChildren<Collider>()) { if (!any) { b = col.bounds; any = true; } else b.Encapsulate(col.bounds); }
            if (!any) b = new Bounds(c.transform.position, Vector3.one);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            int front = 0;
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var s = cam.WorldToScreenPoint(p);
                if (s.z <= 0) continue;
                front++;
                minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x); minY = Mathf.Min(minY, s.y); maxY = Mathf.Max(maxY, s.y);
            }
            if (front == 0) return false;
            rect = Rect.MinMaxRect(minX, Screen.height - maxY, maxX, Screen.height - minY);
            return true;
        }
    }

    /// <summary>
    /// The world's one <see cref="WorldActionMachine"/> and its hookup to the pointer, keys, camera and
    /// <see cref="LedgerCard"/> (WU-0). <see cref="PointerInput"/> routes taps here; the
    /// <see cref="WorldActionController"/> ticks it and offers the keyboard alternative.
    /// </summary>
    public static class WorldActions
    {
        public static readonly WorldActionMachine Machine = new WorldActionMachine();
        private static readonly List<Component> _registered = new List<Component>();
        private static float _cameraMovingUntil = float.NegativeInfinity;

        /// <summary>Scene world actions (for generous hit targets and the E key).</summary>
        public static IReadOnlyList<Component> Registered => _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Machine.Cancel();
            _registered.Clear();
            _cameraMovingUntil = float.NegativeInfinity;
            Hooked = false;
        }

        private static bool Hooked;

        private static void Hook()
        {
            if (Hooked) return;
            Hooked = true;
            Machine.Changed -= OnChanged;
            Machine.Changed += OnChanged;
        }

        public static void Register(Component c) { Hook(); if (c != null && !_registered.Contains(c)) _registered.Add(c); }
        public static void Unregister(Component c)
        {
            _registered.Remove(c);
            if (c is IInspectable i && ReferenceEquals(Machine.Current, i)) Machine.Cancel();
        }

        private static float Now => Time.unscaledTime;

        /// <summary>Camera code (or the controller, from the camera's motion) says the view is moving for a moment.</summary>
        public static void NoteCameraMove(float seconds = 0.15f) => _cameraMovingUntil = Mathf.Max(_cameraMovingUntil, Now + seconds);
        public static bool CameraMoving => Now < _cameraMovingUntil;

        public static WorldTapResult Tap(IWorldAction a) { Hook(); return Machine.Tap(a, Now, CameraMoving); }
        public static WorldTapResult Inspect(IInspectable i) { Hook(); return Machine.Inspect(i, Now, CameraMoving); }
        public static WorldTapResult TapElsewhere() { Hook(); return Machine.TapElsewhere(Now, CameraMoving); }
        public static void Cancel() => Machine.Cancel();
        public static WorldActionState State => Machine.State;
        public static bool Open => Machine.State == WorldActionState.Preview || Machine.State == WorldActionState.Inspect || Machine.State == WorldActionState.Refused || Machine.State == WorldActionState.Disabled;

        /// <summary>The nearest registered action within its key reach of a point (for E), or null.</summary>
        public static IWorldAction NearestInReach(Vector3 from)
        {
            IWorldAction best = null; float bestD = float.MaxValue;
            foreach (var c in _registered)
            {
                if (c == null || !c.gameObject.activeInHierarchy || !(c is IWorldAction a)) continue;
                float reach = c is IWorldActionAnchor an ? an.KeyReach : 0f;
                var p = c is IWorldActionAnchor an2 && an2.Anchor != null ? an2.Anchor.position : c.transform.position;
                float d = BoardSpace.Flat(p - from);
                if (reach > 0 && d <= reach && d < bestD) { best = a; bestD = d; }
            }
            return best;
        }

        /// <summary>A registered action whose screen bounds, enlarged to the minimum target, hold a screen point (pixels, bottom-left origin).</summary>
        public static Component PickEnlarged(Vector2 screenPoint, Camera cam)
        {
            if (cam == null) return null;
            var gui = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            Component best = null; float bestArea = float.MaxValue;
            foreach (var c in _registered)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                if (!WorldHitTarget.ScreenBounds(c, cam, out var r)) continue;
                var big = WorldHitTarget.Enlarge(r, Screen.height);
                if (!big.Contains(gui)) continue;
                float area = big.width * big.height;
                if (area < bestArea) { best = c; bestArea = area; }
            }
            return best;
        }

        private static void OnChanged(WorldActionMachine m)
        {
            switch (m.State)
            {
                case WorldActionState.Preview:
                    LedgerCard.Show(LedgerContent.From(m.Current, ((IWorldAction)m.Current).PreviewLine, null), AnchorOf(m.Current), LedgerMode.Preview, m.Current as Component, m.Current as IScreenAnchored);
                    break;
                case WorldActionState.Inspect:
                    LedgerCard.Show(LedgerContent.From(m.Current, null, null), AnchorOf(m.Current), LedgerMode.Inspect, m.Current as Component, m.Current as IScreenAnchored);
                    break;
                case WorldActionState.Refused:
                case WorldActionState.Disabled:
                    LedgerCard.Show(LedgerContent.From(m.Current, null, m.Reason), AnchorOf(m.Current), LedgerMode.Refused, m.Current as Component, m.Current as IScreenAnchored);
                    break;
                default:
                    LedgerCard.Hide();
                    break;
            }
        }

        private static Transform AnchorOf(IInspectable i)
        {
            if (i is IWorldActionAnchor a && a.Anchor != null) return a.Anchor;
            return i is Component c ? c.transform : null;
        }
    }

    /// <summary>Ticks <see cref="WorldActions"/>, watches the camera for moves and offers the keyboard alternative:
    /// E (or Enter) previews the nearest action in reach and confirms it on the next press; Backspace cancels.
    /// Added by <see cref="PlayerInstaller"/>.</summary>
    public sealed class WorldActionController : MonoBehaviour
    {
        /// <summary>Camera motion per frame above which taps are guarded (metres and degrees).</summary>
        public const float MoveMetres = 0.35f, TurnDegrees = 2.5f;
        private Vector3 _lastPos; private Quaternion _lastRot; private Camera _lastCam;

        /// <summary>The god board owns the pointer (WU-2): its ledge and ghost previews use the same machine.</summary>
        private static bool OnGodBoard => GodBoardDirector.Instance != null && GodBoardDirector.Instance.View == BoardView.Board;

        private static bool Blocked =>
            (!PointerInput.InputEnabled && !OnGodBoard) || Hud.MenuOpen || MapScreen.IsOpen
            || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning);

        private void Update()
        {
            WorldActions.Machine.Tick(Time.unscaledTime);
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (OnGodBoard) cam = GodBoardDirector.Instance.BoardCam; // the board camera is the one the player sees (panning is a drag, not a tap)
            if (cam != null)
            {
                var t = cam.transform;
                if (cam == _lastCam && (Vector3.Distance(t.position, _lastPos) > MoveMetres || Quaternion.Angle(t.rotation, _lastRot) > TurnDegrees))
                    WorldActions.NoteCameraMove();
                _lastCam = cam; _lastPos = t.position; _lastRot = t.rotation;
            }
            // the council (WU-4) takes its own taps but previews on this machine: leave its previews alone while its encounter runs
            bool council = EncounterHost.Instance != null && EncounterHost.Instance.Current is CouncilEncounter && !Hud.MenuOpen && !MapScreen.IsOpen;
            if (Blocked) { if (WorldActions.Open && !council) WorldActions.Cancel(); return; }
            if (Input.GetKeyDown(KeyCode.Backspace)) WorldActions.Cancel();
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) KeyPress();
        }

        /// <summary>The non-gesture alternative: confirm the open preview, or preview the nearest action in reach.</summary>
        public static WorldTapResult KeyPress()
        {
            var m = WorldActions.Machine;
            if (m.State == WorldActionState.Preview && m.Current is IWorldAction open) return WorldActions.Tap(open);
            var w = PlayerWalker.Instance;
            var a = w != null ? WorldActions.NearestInReach(w.Position) : null;
            return a != null ? WorldActions.Tap(a) : WorldTapResult.Ignored;
        }
    }
}
