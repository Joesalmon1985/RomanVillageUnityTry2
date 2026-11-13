using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Finds what is under the crosshair (first person, within <see cref="ReachFirstPerson"/> m of the eye)
    /// or under the mouse (God View). E interacts in first person; left click selects in both views.
    /// The selection is kept across Tab and shown by the InspectPanel.
    /// </summary>
    public sealed class Picker : MonoBehaviour
    {
        public const float ReachFirstPerson = 4f;
        public static Picker Instance { get; private set; }

        /// <summary>Currently selected inspectable (null if none).</summary>
        public IInspectable Selected { get; private set; }
        /// <summary>Interactable under the crosshair / mouse this frame (null if none).</summary>
        public IInteractable Target { get; private set; }
        /// <summary>Inspectable under the crosshair / mouse this frame (null if none).</summary>
        public IInspectable Hovered { get; private set; }
        public RaycastHit LastHit { get; private set; }
        public bool HasHit { get; private set; }

        public event Action<IInspectable> SelectionChanged;

        /// <summary>When false, E and clicks are ignored (encounters, dialogue and menus disable it automatically).</summary>
        public bool InputEnabled = true;

        private ViewSwitcher _views;

        public void Bind(ViewSwitcher views) { Instance = this; _views = views; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Select(IInspectable item)
        {
            if (ReferenceEquals(item, Selected)) return;
            Selected = item;
            SelectionChanged?.Invoke(item);
        }

        public void ClearSelection() => Select(null);

        /// <summary>Interacts with the current target, if any. Returns true if something was used.</summary>
        public bool InteractWithTarget()
        {
            var t = Target;
            if (t == null || (t is UnityEngine.Object o && o == null)) return false;
            if (t is IInspectable insp) Select(insp);
            t.Interact();
            return true;
        }

        private bool Blocked => Hud.MenuOpen || DialoguePanel.IsOpen || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning);

        private void Update()
        {
            if (_views == null) return;
            UpdateHover();
            if (!InputEnabled || Blocked) return;

            if (_views.CurrentView == ViewMode.FirstPerson)
            {
                if (Input.GetKeyDown(KeyCode.E)) InteractWithTarget();
                if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked && Hovered != null) Select(Hovered);
            }
            else
            {
                if (Input.GetMouseButtonDown(0) && !InspectPanel.ContainsMouse())
                    Select(Hovered);
            }
        }

        private void UpdateHover()
        {
            Target = null; Hovered = null; HasHit = false;
            if (Blocked && _views.CurrentView == ViewMode.FirstPerson) return;
            Ray ray; float range;
            if (_views.CurrentView == ViewMode.FirstPerson)
            {
                var cam = _views.Avatar.Camera;
                ray = new Ray(cam.transform.position, cam.transform.forward);
                range = ReachFirstPerson;
            }
            else
            {
                ray = _views.God.Camera.ScreenPointToRay(Input.mousePosition);
                range = 1000f;
            }
            if (!Physics.Raycast(ray, out var hit, range, ~(1 << 2), QueryTriggerInteraction.Collide)) return;
            HasHit = true; LastHit = hit;
            Hovered = hit.collider.GetComponentInParent<IInspectable>();
            Target = hit.collider.GetComponentInParent<IInteractable>();
        }
    }
}
