using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// God board display settings (WU-2). <see cref="ClearView"/> keeps yesterday's G1-6b HUD (people panels, hand fan, costs card,
    /// hourglass button, trade and discard windows) for players who want plain on-screen controls. It is off by default
    /// (world first: the altar ledge), toggled in the Esc menu, and saved for the player on this device (PlayerPrefs).
    /// Both layers read the same <see cref="GodBoardViewModel"/>.
    /// </summary>
    public static class GodBoardSettings
    {
        public const string ClearViewPref = "WorldMVP.GodBoard.ClearView";
        private static int _cached = -1;

        /// <summary>Tests and captures can set this without touching the saved choice.</summary>
        public static bool? Override;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _cached = -1; Override = null; }

        public static bool ClearView
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                if (_cached < 0)
                {
                    try { _cached = PlayerPrefs.GetInt(ClearViewPref, 0) != 0 ? 1 : 0; } catch { _cached = 0; }
                }
                return _cached == 1;
            }
            set
            {
                _cached = value ? 1 : 0;
                try { PlayerPrefs.SetInt(ClearViewPref, _cached); PlayerPrefs.Save(); } catch { }
            }
        }

        /// <summary>Forgets the cached value so the next read comes from PlayerPrefs (tests).</summary>
        public static void ReloadFromPrefs() { _cached = -1; }
    }
}
