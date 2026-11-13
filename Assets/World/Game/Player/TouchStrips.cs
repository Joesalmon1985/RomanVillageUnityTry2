using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The four invisible touch strips in the bottom 30% of the screen (REFACTOR_SPEC 2.D): left to right 1-4, the four
    /// moves on foot (1 turn left, 2 forward, 3 step back, 4 turn right) and the four drums in the dance. Owner: package P.
    /// Real touches only: a mouse click anywhere is a pointer tap (with the fixed -8 degree pitch the near ground, where
    /// the player presses to cast, is in the lower part of the screen). Tests and AutoPlay press a strip with
    /// <see cref="SimulatePress"/> / <see cref="SimulateHold"/>, which go through the same <see cref="Held"/> / <see cref="Pressed"/> path.
    /// </summary>
    public static class TouchStrips
    {
        /// <summary>Height of the band as a fraction of the screen height.</summary>
        public const float BandFraction = 0.3f;

        private static readonly bool[] _held = new bool[5], _pressed = new bool[5], _simHeld = new bool[5], _simPress = new bool[5];
        private static int _frame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < 5; i++) { _held[i] = _pressed[i] = _simHeld[i] = _simPress[i] = false; }
            _frame = -1;
        }

        /// <summary>Strip 1-4 under a screen point (pixels, origin bottom-left as Input.mousePosition), 0 outside the band.</summary>
        public static int StripAt(Vector2 screenPoint)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return 0;
            if (screenPoint.y < 0 || screenPoint.y >= Screen.height * BandFraction) return 0;
            if (screenPoint.x < 0 || screenPoint.x >= Screen.width) return 0;
            return Mathf.Clamp(1 + (int)(screenPoint.x * 4f / Screen.width), 1, 4);
        }

        /// <summary>True while strip 1-4 is held (a touch rests in it).</summary>
        public static bool Held(int strip) { Sample(); return strip >= 1 && strip <= 4 && _held[strip]; }

        /// <summary>True on the frame strip 1-4 was pressed (a touch began in it).</summary>
        public static bool Pressed(int strip) { Sample(); return strip >= 1 && strip <= 4 && _pressed[strip]; }

        /// <summary>True if any touch is in the band this frame (pointer code ignores those touches).</summary>
        public static bool AnyTouchInBand()
        {
            for (int i = 0; i < Input.touchCount; i++) if (StripAt(Input.GetTouch(i).position) > 0) return true;
            return false;
        }

        /// <summary>Presses a strip as if a touch began there on the next frame (tests, AutoPlay).</summary>
        public static void SimulatePress(int strip) { if (strip >= 1 && strip <= 4) _simPress[strip] = true; }

        /// <summary>Holds or releases a strip (tests, AutoPlay).</summary>
        public static void SimulateHold(int strip, bool held) { if (strip >= 1 && strip <= 4) _simHeld[strip] = held; }

        private static void Sample()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            for (int i = 1; i <= 4; i++) { _held[i] = _simHeld[i]; _pressed[i] = _simPress[i]; _simPress[i] = false; }
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                int s = StripAt(t.position);
                if (s == 0) continue;
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) _held[s] = true;
                if (t.phase == TouchPhase.Began) _pressed[s] = true;
            }
        }
    }
}
