using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// Scene/environment management: a set of named zones (e.g. "bedsit", "corridor"), each with a fixed player
    /// standpoint. It places the first-person rig, tells the InteractionController which zone is active and exposes the
    /// doorway waypoints used for first-person transitions. It knows nothing about how zones are rendered.
    /// </summary>
    public class WorldSceneController : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public string Id;
            public Vector3 Standpoint;               // x,z = position; y = eye height
            public CompassDirection DefaultFacing = CompassDirection.North;
        }

        public Transform Rig;
        public FirstPersonViewController View;
        public InteractionController Interactions;

        readonly Dictionary<string, Zone> _zones = new Dictionary<string, Zone>();

        public string CurrentZoneId { get; private set; }
        public event Action<string, string> ZoneChanged;   // (from, to)

        public Zone GetZone(string id) => _zones.TryGetValue(id, out var z) ? z : null;

        public void DefineZone(string id, Vector3 standpoint, CompassDirection facing = CompassDirection.North)
        {
            _zones[id] = new Zone { Id = id, Standpoint = standpoint, DefaultFacing = facing };
        }

        /// <summary>Instantly enter a zone (used at start-up). Keeps the current facing unless one is given.</summary>
        public void EnterZoneImmediate(string id, CompassDirection? facing = null)
        {
            var z = GetZone(id);
            if (z == null) { Debug.LogError("Unknown zone " + id); return; }
            Rig.position = z.Standpoint;
            View.SnapTo(facing ?? z.DefaultFacing);
            SetCurrent(id);
        }

        /// <summary>Mark a zone as the current one after a walk completed (rig is already there).</summary>
        public void ArriveInZone(string id)
        {
            var z = GetZone(id);
            if (z != null) Rig.position = new Vector3(z.Standpoint.x, Rig.position.y, z.Standpoint.z);
            SetCurrent(id);
        }

        void SetCurrent(string id)
        {
            var from = CurrentZoneId;
            CurrentZoneId = id;
            if (Interactions != null) Interactions.SetZone(id);
            ZoneChanged?.Invoke(from, id);
        }
    }
}
