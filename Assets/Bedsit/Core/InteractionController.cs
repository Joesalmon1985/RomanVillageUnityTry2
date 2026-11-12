using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// Decides which InteractionTarget the top control currently addresses: the highest-priority interactable target
    /// registered for the active zone and the direction the player is facing. Raises CurrentChanged so visual cues can
    /// follow, and routes the Interact action to the target.
    /// </summary>
    public class InteractionController : MonoBehaviour
    {
        public FirstPersonViewController View;
        public string ActiveZone = "bedsit";

        readonly List<InteractionTarget> _targets = new List<InteractionTarget>();

        public InteractionTarget Current { get; private set; }
        public event Action<InteractionTarget> CurrentChanged;
        public event Action<InteractionTarget> TargetInteracted;

        public IReadOnlyList<InteractionTarget> Targets => _targets;

        public void Register(InteractionTarget t)
        {
            if (t != null && !_targets.Contains(t)) _targets.Add(t);
            Refresh();
        }

        public void Unregister(InteractionTarget t)
        {
            _targets.Remove(t);
            Refresh();
        }

        public void SetZone(string zoneId)
        {
            ActiveZone = zoneId;
            Refresh();
        }

        void OnEnable()
        {
            if (View != null) { View.TurnStarted += OnTurnStarted; View.TurnCompleted += OnTurnCompleted; }
        }

        void OnDisable()
        {
            if (View != null) { View.TurnStarted -= OnTurnStarted; View.TurnCompleted -= OnTurnCompleted; }
        }

        void OnTurnStarted(CompassDirection d) => SetCurrent(null);
        void OnTurnCompleted(CompassDirection d) => Refresh();

        /// <summary>Targets for a zone + direction (highest priority first).</summary>
        public InteractionTarget Find(string zone, CompassDirection dir)
        {
            InteractionTarget best = null;
            foreach (var t in _targets)
            {
                if (t == null || !t.Interactable || t.ZoneId != zone || t.Direction != dir) continue;
                if (best == null || t.Priority > best.Priority) best = t;
            }
            return best;
        }

        public void Refresh()
        {
            if (View == null) return;
            SetCurrent(View.IsTurning ? null : Find(ActiveZone, View.Facing));
        }

        void SetCurrent(InteractionTarget t)
        {
            if (t == Current) return;
            Current = t;
            CurrentChanged?.Invoke(t);
        }

        /// <summary>Activate the current target. Returns false if nothing is addressable (or the view is mid-turn).</summary>
        public bool TryInteract()
        {
            if (View != null && View.IsTurning) return false;
            Refresh();
            if (Current == null) return false;
            var t = Current;
            t.Interact();
            TargetInteracted?.Invoke(t);
            Refresh();
            return true;
        }
    }
}
