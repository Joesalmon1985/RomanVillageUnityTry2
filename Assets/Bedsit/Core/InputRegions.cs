using UnityEngine;

namespace Bedsit
{
    public enum PlayerAction { None, TurnLeft, TurnRight, Interact }

    /// <summary>
    /// Pure screen-region logic for the game's exactly-three controls (no engine state, unit-testable).
    /// Regions are fractions of the screen (origin bottom-left, as Unity reports pointer positions):
    ///   Left  : x 0..0.22, any height  -> rotate 90 degrees anticlockwise
    ///   Right : x 0.78..1, any height  -> rotate 90 degrees clockwise
    ///   Top   : x 0.25..0.75, y 0.58..1 -> interact / move forward through a doorway
    /// The middle and bottom of the screen are deliberately inert.
    /// </summary>
    public static class InputRegions
    {
        public const float SideWidth = 0.22f;
        public const float TopMinX = 0.25f, TopMaxX = 0.75f, TopMinY = 0.58f;

        public static PlayerAction Classify(Vector2 normalized)
        {
            if (normalized.x < 0f || normalized.x > 1f || normalized.y < 0f || normalized.y > 1f) return PlayerAction.None;
            if (normalized.x <= SideWidth) return PlayerAction.TurnLeft;
            if (normalized.x >= 1f - SideWidth) return PlayerAction.TurnRight;
            if (normalized.x >= TopMinX && normalized.x <= TopMaxX && normalized.y >= TopMinY) return PlayerAction.Interact;
            return PlayerAction.None;
        }

        /// <summary>Bottom pad strip: four equal pads between the two turn zones, in the bottom 20 % of the screen.
        /// Returns 0..3 or -1. (Used only while the rhythm pads are active; left/right zones still turn.)</summary>
        public const float PadMaxY = 0.20f;
        public static int ClassifyPad(Vector2 normalized)
        {
            if (normalized.y < 0f || normalized.y > PadMaxY) return -1;
            if (normalized.x <= SideWidth || normalized.x >= 1f - SideWidth) return -1;
            float w = (1f - 2f * SideWidth) / 4f;
            return Mathf.Clamp((int)((normalized.x - SideWidth) / w), 0, 3);
        }

        public static int ClassifyPadScreen(Vector2 px, int width, int height)
        {
            if (width <= 0 || height <= 0) return -1;
            return ClassifyPad(new Vector2(px.x / width, px.y / height));
        }

        public static PlayerAction ClassifyScreen(Vector2 screenPixels, int width, int height)
        {
            if (width <= 0 || height <= 0) return PlayerAction.None;
            return Classify(new Vector2(screenPixels.x / width, screenPixels.y / height));
        }
    }
}
