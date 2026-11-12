using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// The four fixed compass views. Yaw is measured clockwise from above (Unity +Y rotation):
    /// North = +Z (0), East = +X (90), South = -Z (180), West = -X (270).
    /// </summary>
    public enum CompassDirection { North = 0, East = 1, South = 2, West = 3 }

    public static class CompassExtensions
    {
        public static float YawDegrees(this CompassDirection d) => 90f * (int)d;

        public static CompassDirection TurnedRight(this CompassDirection d) => (CompassDirection)(((int)d + 1) & 3);

        public static CompassDirection TurnedLeft(this CompassDirection d) => (CompassDirection)(((int)d + 3) & 3);

        public static CompassDirection Opposite(this CompassDirection d) => (CompassDirection)(((int)d + 2) & 3);

        public static Vector3 Forward(this CompassDirection d)
        {
            switch (d)
            {
                case CompassDirection.North: return Vector3.forward;
                case CompassDirection.East: return Vector3.right;
                case CompassDirection.South: return Vector3.back;
                default: return Vector3.left;
            }
        }
    }
}
