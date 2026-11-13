using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The four invisible touch strips in the bottom 30% of the screen (REFACTOR_SPEC 2.D): left to right 1-4, the four
    /// moves on foot and the four drums in the dance. Owner: package P.
    /// F-0: <see cref="StripAt"/> is the pure geometry from the spec; <see cref="Held"/> and <see cref="Pressed"/> are
    /// stubs (false) until P reads touches and the mouse.
    /// </summary>
    public static class TouchStrips
    {
        /// <summary>Height of the band as a fraction of the screen height.</summary>
        public const float BandFraction = 0.3f;

        /// <summary>Strip 1-4 under a screen point (pixels, origin bottom-left as Input.mousePosition), 0 outside the band.</summary>
        public static int StripAt(Vector2 screenPoint)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return 0;
            if (screenPoint.y < 0 || screenPoint.y >= Screen.height * BandFraction) return 0;
            if (screenPoint.x < 0 || screenPoint.x >= Screen.width) return 0;
            return Mathf.Clamp(1 + (int)(screenPoint.x * 4f / Screen.width), 1, 4);
        }

        /// <summary>True while strip 1-4 is held. F-0 stub: false.</summary>
        public static bool Held(int strip) => false;

        /// <summary>True on the frame strip 1-4 was pressed. F-0 stub: false.</summary>
        public static bool Pressed(int strip) => false;
    }
}
