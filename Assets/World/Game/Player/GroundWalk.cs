using UnityEngine;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// WU-1b (WORLD_UI_PLAN P-3, HUB-START): tap visible walkable ground and the viewpoint auto-walks there along
    /// <see cref="WalkGrid.FindPath"/> (the same walk the Tab map's target uses), with a small footprint marker on the target
    /// cell until it arrives or the walk stops. Taps on people and props never get here (<see cref="PointerInput.Tap"/> sends
    /// only taps that hit ground or nothing). Keys, strips and a right click stop the walk as before. Reads only.
    /// </summary>
    public static class GroundWalk
    {
        /// <summary>Farthest ground tap (metres) that tries to walk; farther is the horizon, not a place.</summary>
        public const float MaxRange = 160f;
        /// <summary>The cell the last accepted tap walks to, and how many taps started a walk.</summary>
        public static Cell? Goal { get; private set; }
        public static int Walks { get; private set; }
        public static GameObject Marker => _marker != null ? _marker.gameObject : null;

        private static WalkMarker _marker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Goal = null; Walks = 0; _marker = null; }

        /// <summary>True when a ground tap may walk here now (not in God View, an interior, the map or a locked walker).</summary>
        public static bool Available
        {
            get
            {
                var w = PlayerWalker.Instance;
                if (w == null || w.IsLocked) return false;
                if (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God) return false;
                if (Interiors.Current != null) return false; // underground keeps to the footprint stones and keys
                return true;
            }
        }

        /// <summary>Starts the walk to a ground point. False (and a short line for a blocked place) when it did not start.</summary>
        public static bool TryWalk(Vector3 ground)
        {
            if (!Available) return false;
            var w = PlayerWalker.Instance;
            if (BoardSpace.Flat(ground - w.Position) > MaxRange) return false;
            var want = WalkLattice.ToCell(ground);
            var free = WalkGrid.NearestFree(want, 2);
            if (!free.HasValue) { SpeechBubbles.Say(null, "There is no way there.", 3f); return false; }
            var goal = free.Value;
            if (goal == w.Cell && !w.IsAutoWalking) return false; // already there
            if (!w.WalkTo(goal)) { SpeechBubbles.Say(null, "There is no way there.", 3f); return false; }
            Goal = goal; Walks++;
            ShowMarker(goal);
            return true;
        }

        private static void ShowMarker(Cell goal)
        {
            if (_marker == null) _marker = WalkMarker.Create();
            _marker.Place(PlayerWalker.CellPosition(goal));
        }

        /// <summary>Hides the marker (arrival, a stop, a new view).</summary>
        public static void ClearMarker() { if (_marker != null) _marker.Hide(); }
    }

    /// <summary>The small footprint ring on a walk target: two footprints and a ring, warm glow, fades out on arrival.</summary>
    public sealed class WalkMarker : MonoBehaviour
    {
        private Material _mat;
        private float _hideAt = -1f;
        private float _fade = 1f;
        private PlayerWalker _hooked;

        public bool Shown => gameObject.activeSelf;

        public static WalkMarker Create()
        {
            var go = new GameObject("Walk target marker");
            go.layer = 2;
            var m = go.AddComponent<WalkMarker>();
            m.Build();
            go.SetActive(false);
            return m;
        }

        private void Build()
        {
            var mb = new MeshBuilder();
            const int N = 24;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                mb.Quad(o0 * 0.55f, o0 * 0.72f, o1 * 0.72f, o1 * 0.55f, Color.white);
                mb.Quad(o0 * 0.55f, o1 * 0.55f, o1 * 0.72f, o0 * 0.72f, Color.white);
            }
            // Two footprints (a sole and a heel each), left and right, toes forward.
            foreach (float x in new[] { -0.16f, 0.16f })
            {
                float z = x < 0 ? -0.05f : 0.12f;
                mb.Box(new Vector3(x, 0.002f, z + 0.1f), new Vector3(0.16f, 0.004f, 0.24f), Color.white);
                mb.Box(new Vector3(x, 0.002f, z - 0.12f), new Vector3(0.11f, 0.004f, 0.11f), Color.white);
            }
            _mat = MaterialFactory.CreateGlow(new Color(1f, 0.85f, 0.45f, 0.85f), "WalkMarker");
            var body = mb.ToObject("Footprints", transform, _mat);
            body.layer = 2;
        }

        public void Place(Vector3 feet)
        {
            transform.position = feet + Vector3.up * 0.06f;
            _fade = 1f; _hideAt = -1f;
            gameObject.SetActive(true);
            Hook();
        }

        public void Hide() { _hideAt = -1f; gameObject.SetActive(false); }

        private void Hook()
        {
            var w = PlayerWalker.Instance;
            if (w == _hooked) return;
            Unhook();
            _hooked = w;
            if (w != null) w.AutoWalkEnded += OnEnded;
        }

        private void Unhook() { if (_hooked != null) _hooked.AutoWalkEnded -= OnEnded; _hooked = null; }
        private void OnDestroy() => Unhook();

        private void OnEnded(bool arrived)
        {
            if (!arrived) { Hide(); return; } // a stop: gone at once
            _hideAt = Time.unscaledTime + 0.5f; // arrival: fades out
        }

        private void Update()
        {
            float pulse = 0.65f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
            if (_hideAt >= 0f)
            {
                _fade = Mathf.Clamp01((_hideAt - Time.unscaledTime) / 0.5f);
                if (Time.unscaledTime >= _hideAt) { Hide(); return; }
            }
            _mat.color = new Color(1f, 0.85f, 0.45f, pulse * _fade);
        }
    }

    /// <summary>
    /// WU-1b (P-3): drag sideways on empty scene to look round: one sixth of a turn (the walker's 60 degree step) per drag step.
    /// The press must start on empty ground or sky, not on a person, prop, bubble, ledger card or the touch band. A finger dragged
    /// to the right turns the view left (the world follows the finger, as in a street view); keys (A / D, arrows) and the strips stay
    /// the alternative. A press that moved more than <see cref="Slop"/> is a drag and never a tap.
    /// </summary>
    public static class DragLook
    {
        public const float Slop = 12f;

        /// <summary>Pixels of sideways drag for one sixth of a turn (at least 40, 12 % of the screen height).</summary>
        public static float StepPixels => Mathf.Max(40f, Screen.height * 0.12f);

        public static bool Active { get; private set; }
        public static bool Dragging { get; private set; }
        /// <summary>Turn steps made in the current or last drag (negative = left).</summary>
        public static int Turned { get; private set; }

        private static Vector2 _start;
        private static float _lastX;

        /// <summary>What the current drag does: turn the walker in sixths, turn only the head (free look while locked or led), or swing the vista.</summary>
        public enum Kind { Steps, FreeLook, Vista }
        public static Kind Mode { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = Dragging = false; Turned = 0; _pending = 0; Mode = Kind.Steps; }

        public static void Begin(Vector2 screenPoint, bool eligible) { Active = eligible; Dragging = false; Turned = 0; _pending = 0; _start = screenPoint; _lastX = screenPoint.x; Mode = Kind.Steps; }

        /// <summary>Degrees of free look per pixel of drag (one sixth of a turn per <see cref="StepPixels"/>, as the steps).</summary>
        public static float DegreesPerPixel => 60f / StepPixels;

        /// <summary>Feeds the pointer position while held; turns the walker by whole steps. True while this press is a drag.</summary>
        public static bool Move(Vector2 screenPoint)
        {
            if (!Active) return false;
            float dx = screenPoint.x - _start.x;
            if (!Dragging && (screenPoint - _start).magnitude < Slop) return false;
            if (!Dragging)
            {
                Dragging = true;
                var w0 = PlayerWalker.Instance;
                // Opening vista: the drag swings the view round the camp. Free look: a locked or led walker keeps its course while the
                // head turns (the owner could not look round while the prince led). Otherwise the walker turns in sixths as before.
                if (OpeningVista.Active) Mode = Kind.Vista;
                else if (w0 != null && (w0.IsLocked || w0.Following != null)) Mode = Kind.FreeLook;
                else { Mode = Kind.Steps; if (w0 != null && w0.IsAutoWalking) w0.StopAuto(); }
            }
            float ddx = screenPoint.x - _lastX; _lastX = screenPoint.x;
            var w = PlayerWalker.Instance;
            if (Mode == Kind.Vista) { if (OpeningVista.Instance != null) OpeningVista.Instance.Swing(ddx); return true; }
            if (Mode == Kind.FreeLook) { if (w != null) w.Look(-ddx * DegreesPerPixel); return true; } // finger right = the view turns left
            int steps = (int)(dx / StepPixels); // truncates towards zero
            if (w == null) return true;
            // dx > 0 (finger right) = the view turns left: negative turns.
            int want = -steps;
            _pending += want - Turned; Turned = want;
            Pump();
            return true;
        }

        /// <summary>Turn steps still to make (the walker takes one at a time).</summary>
        public static int Pending => _pending;
        private static int _pending;

        /// <summary>Hands the walker the next queued turn when it is free; call every frame.</summary>
        public static void Pump()
        {
            var w = PlayerWalker.Instance;
            if (w == null || _pending == 0 || w.IsGliding || w.IsLocked) return;
            if (_pending > 0) { w.TurnRight(); _pending--; } else { w.TurnLeft(); _pending++; }
        }

        /// <summary>Ends the press. True when it was a drag (the caller must not treat it as a tap).</summary>
        public static bool End()
        {
            bool was = Dragging;
            if (was && Mode == Kind.FreeLook && PlayerWalker.Instance != null) PlayerWalker.Instance.ReleaseLook();
            Active = false; Dragging = false;
            return was;
        }
    }
}
