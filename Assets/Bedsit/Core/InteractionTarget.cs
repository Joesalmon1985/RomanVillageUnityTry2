using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bedsit
{
    /// <summary>
    /// A world interaction definition: "in zone Z, when the player faces direction D, the top control does this".
    /// Pure data + events; it never touches rendering, so the same component works for any scene or renderer.
    /// </summary>
    public class InteractionTarget : MonoBehaviour
    {
        public string Id = "target";
        [Tooltip("Zone this target belongs to (e.g. \"bedsit\", \"corridor\").")]
        public string ZoneId = "bedsit";
        public CompassDirection Direction = CompassDirection.North;
        [Tooltip("Higher wins when several targets share a zone + direction.")]
        public int Priority = 0;
        public bool Interactable = true;

        [Tooltip("World point the visual cue floats at (defaults to this transform).")]
        public Transform CueAnchor;

        public event Action<InteractionTarget> Interacted;
        public UnityEvent OnInteract = new UnityEvent();

        public int InteractCount { get; private set; }

        public Vector3 CuePosition => (CueAnchor != null ? CueAnchor : transform).position;

        public void Interact()
        {
            if (!Interactable) return;
            InteractCount++;
            Interacted?.Invoke(this);
            OnInteract.Invoke();
        }
    }
}
