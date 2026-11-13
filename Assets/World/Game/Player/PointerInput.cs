using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>What a click or tap above the touch band hit.</summary>
    public struct PointerHit
    {
        /// <summary>Ground point under the pointer (valid when <see cref="HasGround"/>).</summary>
        public Vector3 Ground;
        public bool HasGround;
        /// <summary>The thing under the pointer (an IInspectable, IFollowable or speech reply component), or null.</summary>
        public Component Target;
    }

    /// <summary>
    /// The free mouse pointer (REFACTOR_SPEC 2.D "Pointer", appendix B). Replaces <see cref="Picker"/>. Owner: package P.
    /// F-0 stub: the static surface exists; nothing raises <see cref="Tapped"/> yet.
    /// </summary>
    public static class PointerInput
    {
        public static event Action<PointerHit> Tapped;
        /// <summary>What the pointer is over this frame (Target may be null).</summary>
        public static PointerHit Hovered { get; private set; }
        /// <summary>False while something else owns the pointer (encounters, menus).</summary>
        public static bool InputEnabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Tapped = null; Hovered = default; InputEnabled = true; }

        /// <summary>P: raise a tap (the pointer component calls this).</summary>
        internal static void RaiseTapped(PointerHit hit) { Hovered = hit; Tapped?.Invoke(hit); }
    }
}
