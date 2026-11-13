using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Every view of the land reconciled from state after each <see cref="GameClock.Ticked"/> (REFACTOR_SPEC 8.4,
    /// appendix B): settlements, plots, buildings, roads, carriers, stock patches, demons, proposal markers.
    /// Owner: package V. F-0 stub: nothing creates it yet; the lookups return null.
    /// </summary>
    public sealed class LandViews : MonoBehaviour
    {
        public static LandViews Instance { get; private set; }

        /// <summary>Raised after the views have caught up with a tick, with that tick's events.</summary>
        public event Action<IReadOnlyList<SimEvent>> Synced;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public Transform CarrierView(int carrierId) => null;
        public Transform BuildingView(int buildingId) => null;
        public Transform ProposalMarker(int proposalId) => null;

        /// <summary>Rebuilds every view from the current state (after a load, and in tests).</summary>
        public void RebuildFromState() { }

        // Keeps the event "used" until V raises it (avoids a compiler warning in the stub).
        private void RaiseSynced(IReadOnlyList<SimEvent> events) => Synced?.Invoke(events);
    }
}
