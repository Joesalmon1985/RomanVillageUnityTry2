using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>What a click or tap above the touch band hit.</summary>
    public struct PointerHit
    {
        /// <summary>Ground point under the pointer (valid when <see cref="HasGround"/>).</summary>
        public Vector3 Ground;
        public bool HasGround;
        /// <summary>The thing under the pointer (an IInspectable, IFollowable or IInteractable component), or null.</summary>
        public Component Target;
        /// <summary>Open proposal of the player's people whose press area holds <see cref="Ground"/>, or -1.</summary>
        public int ProposalId;
    }

    /// <summary>What a tap did (<see cref="PointerInput.LastTap"/>).</summary>
    public enum TapOutcome { None, Bubble, Interact, Person, Cast, CastRefused, Inspect }

    /// <summary>
    /// The free mouse pointer (REFACTOR_SPEC 2.D "Pointer", appendix B); replaces the old Picker. Owner: package P.
    /// The cursor is always free. A click, or a tap above the touch band, goes in this order:
    /// <list type="number">
    /// <item>a speech bubble or reply (<see cref="SpeechBubbles.HandleTap"/>);</item>
    /// <item>a person: one with a dialogue (<see cref="IInteractable"/>, e.g. Mira) talks; anyone else that can be
    /// followed (<see cref="IFollowable"/>, e.g. a carrier) shows what they are with a "Follow" reply;</item>
    /// <item>the ground of an open proposal of the player's people: <see cref="SpellCaster.TryCastAt"/>;</item>
    /// <item>anything inspectable (<see cref="IInspectable"/>, including hex tiles): a small label says what it is.</item>
    /// </list>
    /// A right click stops an auto-walk or follow. Hovering highlights what a tap would act on. In the F1 God View the
    /// developer tools take clicks (a click there selects for the inspect panel, see <see cref="Selected"/>).
    /// <see cref="Tapped"/> is raised for every world tap after the default handling, so other packages can react.
    /// </summary>
    public static class PointerInput
    {
        public static event Action<PointerHit> Tapped;
        /// <summary>What the pointer is over this frame (Target may be null).</summary>
        public static PointerHit Hovered { get; private set; }
        /// <summary>False while something else owns the pointer (encounters, menus).</summary>
        public static bool InputEnabled { get; set; } = true;
        /// <summary>The thing selected in the F1 God View (shown by the inspect panel); null if none.</summary>
        public static IInspectable Selected { get; set; }
        /// <summary>What the last tap did, and the hit.</summary>
        public static TapOutcome LastTap { get; private set; }
        public static PointerHit LastHit { get; private set; }

        /// <summary>Max ray length (m) for things under the pointer.</summary>
        public const float RayLength = 1200f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Tapped = null; Hovered = default; InputEnabled = true; Selected = null; LastTap = TapOutcome.None; LastHit = default; }

        /// <summary>P: raise a tap (the pointer component calls this).</summary>
        internal static void RaiseTapped(PointerHit hit) { Tapped?.Invoke(hit); }

        internal static void SetHovered(PointerHit hit) => Hovered = hit;

        private static Camera ActiveCamera => ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;

        /// <summary>What is under a screen point (pixels, origin bottom-left) in the active view.</summary>
        public static PointerHit Raycast(Vector2 screenPoint)
        {
            var hit = new PointerHit { ProposalId = -1 };
            var cam = ActiveCamera;
            if (cam == null) return hit;
            var ray = cam.ScreenPointToRay(screenPoint);
            var hits = Physics.RaycastAll(ray, RayLength, ~(1 << 2), QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var c = TargetOf(h.collider);
                if (c == null) continue;
                if (c is HexView)
                {
                    if (!hit.HasGround) { hit.Ground = h.point; hit.HasGround = true; }
                    if (hit.Target == null) hit.Target = c;
                    break; // the ground: nothing beyond it counts
                }
                if (hit.Target == null) hit.Target = c;
            }
            if (!hit.HasGround)
            {
                var plane = new Plane(Vector3.up, new Vector3(0, BoardSpace.GroundY, 0));
                if (plane.Raycast(ray, out float t) && t < RayLength) { hit.Ground = ray.GetPoint(t); hit.HasGround = true; }
            }
            if (hit.HasGround) hit.ProposalId = ProposalAt(hit.Ground);
            return hit;
        }

        /// <summary>The component a tap on this collider acts on: a person first, then anything inspectable.</summary>
        public static Component TargetOf(Collider col)
        {
            if (col == null) return null;
            var f = col.GetComponentInParent<IFollowable>() as Component;
            if (f != null) return f;
            var i = col.GetComponentInParent<IInteractable>() as Component;
            if (i != null) return i;
            return col.GetComponentInParent<IInspectable>() as Component;
        }

        /// <summary>Open proposal of the player's people at a ground point, or -1.</summary>
        public static int ProposalAt(Vector3 ground)
        {
            var b = WorldBootstrap.Instance;
            if (b == null || b.Sim == null) return -1;
            return World.Sim.Factions.FactionApi.ProposalAt(b.Sim.State, PlayerIds.Human, ground.x, ground.z);
        }

        /// <summary>A tap at a screen point (pixels, origin bottom-left), handled exactly as a real click.</summary>
        public static TapOutcome TapAt(Vector2 screenPoint)
        {
            var gui = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            if (SpeechBubbles.HandleTap(gui)) return Done(TapOutcome.Bubble, default);
            return Tap(Raycast(screenPoint));
        }

        /// <summary>A tap on an object (AutoPlay, tests): as if it were tapped where it stands.</summary>
        public static TapOutcome TapObject(Component target)
        {
            if (target == null) return Done(TapOutcome.None, default);
            var p = target.transform.position;
            var hit = new PointerHit { Target = target, Ground = new Vector3(p.x, BoardSpace.GroundY, p.z), HasGround = true, ProposalId = -1 };
            if (!(target is HexView)) return Tap(hit);
            hit.ProposalId = ProposalAt(hit.Ground);
            return Tap(hit);
        }

        /// <summary>A press on a ground point (AutoPlay, tests): casts if an open proposal is there, else inspects the hex.</summary>
        public static TapOutcome TapGround(Vector3 ground)
        {
            var hit = new PointerHit { Ground = ground, HasGround = true, ProposalId = ProposalAt(ground) };
            int hex = BoardSpace.HexAt(ground);
            if (hex >= 0) foreach (var hv in UnityEngine.Object.FindObjectsByType<HexView>(FindObjectsSortMode.None)) if (hv.HexId == hex) { hit.Target = hv; break; }
            return Tap(hit);
        }

        /// <summary>Default handling of a world tap (after bubbles).</summary>
        public static TapOutcome Tap(PointerHit hit)
        {
            var outcome = TapOutcome.None;
            var t = hit.Target;
            if (t != null && !(t is HexView) && (t is IInteractable || t is IFollowable))
            {
                if (t is IInteractable ia) { ia.Interact(); outcome = TapOutcome.Interact; }
                else { OfferFollow((IFollowable)t, t); outcome = TapOutcome.Person; }
            }
            else if (hit.HasGround && hit.ProposalId >= 0)
            {
                var r = SpellCaster.TryCastAt(hit.Ground);
                outcome = r.Ok ? TapOutcome.Cast : TapOutcome.CastRefused;
            }
            else if (t is IInspectable insp)
            {
                ShowInspect(insp, t, hit);
                outcome = TapOutcome.Inspect;
            }
            Done(outcome, hit);
            RaiseTapped(hit);
            return outcome;
        }

        private static TapOutcome Done(TapOutcome o, PointerHit hit) { LastTap = o; LastHit = hit; return o; }

        /// <summary>Shows what a person is with a "Follow" reply.</summary>
        public static void OfferFollow(IFollowable who, Component c)
        {
            var insp = c as IInspectable;
            string title = insp != null ? insp.Title : who.FollowName;
            var lines = insp != null ? insp.Lines : null;
            SpeechBubbles.Inspect(who.FollowTransform, Vector3.zero, title, lines, 6f, new[] { DialoguePanel.FollowLabel }, i =>
            {
                if (i == 0 && PlayerWalker.Instance != null) PlayerWalker.Instance.Follow(who);
            });
        }

        private static void ShowInspect(IInspectable insp, Component c, PointerHit hit)
        {
            if (c is HexView) SpeechBubbles.Inspect(null, hit.Ground + Vector3.up * 1.5f, insp.Title, insp.Lines, 5f);
            else SpeechBubbles.Inspect(c.transform, Vector3.zero, insp.Title, insp.Lines, 6f);
        }
    }

    /// <summary>Reads the mouse and touches for <see cref="PointerInput"/>, and draws the hover highlight. Added by <see cref="PlayerInstaller"/>.</summary>
    public sealed class PointerController : MonoBehaviour
    {
        private GameObject _ring;
        private Material _ringMat;
        private string _hoverText;
        private Color _hoverColour;
        private static readonly Color PersonColour = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color ThingColour = new Color(0.85f, 0.95f, 1f, 1f);
        private static readonly Color CastColour = new Color(0.45f, 1f, 0.55f, 1f);
        private static readonly Color FarColour = new Color(1f, 0.6f, 0.25f, 1f);

        private static bool Blocked =>
            !PointerInput.InputEnabled || Hud.MenuOpen || MapScreen.IsOpen
            || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning);

        private static bool GodView => ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God;

        private void Start()
        {
            _ring = BuildRing(transform, out _ringMat);
            _ring.SetActive(false);
        }

        private static GameObject BuildRing(Transform parent, out Material mat)
        {
            var mb = new MeshBuilder();
            const int N = 40;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                mb.Quad(o0 * 0.86f, o0, o1, o1 * 0.86f, Color.white);
                mb.Quad(o0 * 0.86f, o1 * 0.86f, o1, o0, Color.white); // both faces
            }
            mat = MaterialFactory.CreateGlow(new Color(1f, 1f, 1f, 0.8f), "HoverRing");
            var go = mb.ToObject("PointerHoverRing", parent, mat);
            go.layer = 2;
            return go;
        }

        private void Update()
        {
            _hoverText = null;
            if (Blocked || GodView || !Application.isFocused && !Application.isBatchMode)
            {
                if (_ring != null) _ring.SetActive(false);
                if (GodView) GodClick();
                return;
            }
            Vector2 mouse = Input.mousePosition;
            var gui = new Vector2(mouse.x, Screen.height - mouse.y);
            bool overBubble = SpeechBubbles.Contains(gui);
            var hit = overBubble ? new PointerHit { ProposalId = -1 } : PointerInput.Raycast(mouse);
            PointerInput.SetHovered(hit);
            UpdateHighlight(hit);

            if (Input.GetMouseButtonDown(1) && PlayerWalker.Instance != null) PlayerWalker.Instance.StopAuto();
            if (Input.GetMouseButtonDown(0)) PointerInput.TapAt(mouse);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && TouchStrips.StripAt(t.position) == 0) PointerInput.TapAt(t.position);
            }
        }

        /// <summary>F1 God View: a click with the Select tool selects for the inspect panel.</summary>
        private void GodClick()
        {
            if (!Input.GetMouseButtonDown(0) || Hud.MenuOpen) return;
            var bar = GodToolbar.Instance;
            if (bar != null && (GodToolbar.MouseOverUi() || (bar.Active != null && bar.Active.CapturesClicks))) return;
            var hit = PointerInput.Raycast(Input.mousePosition);
            PointerInput.Selected = hit.Target as IInspectable;
        }

        private void UpdateHighlight(PointerHit hit)
        {
            if (_ring == null) return;
            var s = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State : null;
            var t = hit.Target;
            Vector3 centre; float radius; Color c;
            if (t != null && !(t is HexView))
            {
                Bounds(t.transform, out centre, out radius);
                bool person = t is IFollowable || t is IInteractable;
                c = person ? PersonColour : ThingColour;
                _hoverText = t is IInspectable i ? i.Title : t is IFollowable f ? f.FollowName : null;
            }
            else if (hit.ProposalId >= 0 && s != null && World.Sim.Factions.FactionApi.ProposalTarget(s, hit.ProposalId, out double px, out double pz, out double pr))
            {
                centre = new Vector3((float)px, BoardSpace.GroundY, (float)pz);
                radius = Mathf.Min((float)pr, 6f);
                bool near = SpellCaster.InRange(hit.ProposalId);
                c = near ? CastColour : FarColour;
                _hoverText = World.Sim.Factions.FactionApi.ProposalTitle(s, hit.ProposalId) + (near ? ": press to cast" : ": walk closer to cast");
            }
            else { _ring.SetActive(false); return; }
            _ring.SetActive(true);
            _ring.transform.position = new Vector3(centre.x, PlayerWalker.GroundHeight(centre) + 0.08f, centre.z);
            _ring.transform.localScale = new Vector3(radius, 1f, radius);
            float pulse = 0.65f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            _ringMat.color = new Color(c.r, c.g, c.b, pulse);
            _hoverColour = c;
        }

        private static void Bounds(Transform t, out Vector3 centre, out float radius)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { centre = t.position; radius = 1f; return; }
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            centre = new Vector3(b.center.x, b.min.y, b.center.z);
            radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 1.15f, 0.7f, 9f);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_hoverText) || Blocked || GodView) return;
            GUI.depth = -6;
            var m = Event.current.mousePosition;
            var st = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(19), wordWrap = false };
            st.normal.textColor = _hoverColour;
            var size = st.CalcSize(new GUIContent(_hoverText));
            UiStyle.ShadowLabel(new Rect(Mathf.Min(m.x + UiStyle.Px(20), Screen.width - size.x - 6), m.y + UiStyle.Px(14), size.x + 4, size.y + 2), _hoverText, st);
        }
    }
}
