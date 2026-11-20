using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Marks a hub destination with its zone id (WORLD_UI_PLAN 2.1): <c>HUB-TEMPLE</c>, <c>HUB-COUNCIL</c>, <c>HUB-RHYTHM</c>,
    /// <c>HUB-HEALER</c>, <c>HUB-CAVE</c>, <c>HUB-DUEL</c>. A placeholder for WU-0's <c>WorldZone</c>: when that component lands, the
    /// merge replaces this class (one place: <see cref="HubInstaller"/>). Holds no behaviour.
    /// </summary>
    public sealed class HubZoneTag : MonoBehaviour
    {
        public const string Temple = "HUB-TEMPLE", Council = "HUB-COUNCIL", Rhythm = "HUB-RHYTHM", Healer = "HUB-HEALER", Cave = "HUB-CAVE", Duel = "HUB-DUEL";
        public static readonly string[] AllZones = { Temple, Council, Rhythm, Healer, Cave, Duel };

        public string Zone;
        /// <summary>The destination's footprint radius in metres (a hit target around it).</summary>
        public float Radius;

        private static readonly List<HubZoneTag> _all = new List<HubZoneTag>();
        public static IReadOnlyList<HubZoneTag> All => _all;

        public static HubZoneTag Find(string zone)
        {
            foreach (var t in _all) if (t != null && t.Zone == zone) return t;
            return null;
        }

        public static HubZoneTag Add(Transform parent, string zone, Vector3 at, float radius)
        {
            var go = new GameObject("Zone " + zone);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var t = go.AddComponent<HubZoneTag>();
            t.Zone = zone; t.Radius = radius;
            return t;
        }

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();
    }
}
