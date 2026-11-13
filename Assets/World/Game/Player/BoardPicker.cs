using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Board picking for God View tools: which flag, road segment, hex corner, hex edge or hex is under the mouse
    /// (or a screen point). Rays go through the active view camera and hit the flat ground plane. Returns -1 for none.
    /// Flags ("vertex") and segments ("edge") are the fine level (20 m apart); corners and hex edges the coarse one
    /// (60 m): city and hero tools pick corners only.
    /// </summary>
    public static class BoardPicker
    {
        /// <summary>Max ground distance (m) from the mouse point to a flag / segment / corner / hex edge for it to count.</summary>
        public const float VertexRadius = 5f, EdgeRadius = 3f, CornerRadius = 10f, HexEdgeRadius = 6f;

        public static Camera ActiveCamera => ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;

        public static bool GroundPointAt(Vector2 screenPoint, out Vector3 point)
        {
            point = default;
            var cam = ActiveCamera;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.up, new Vector3(0, BoardSpace.GroundY, 0));
            if (!plane.Raycast(ray, out float t)) return false;
            point = ray.GetPoint(t);
            return true;
        }

        public static bool GroundPointUnderMouse(out Vector3 point) => GroundPointAt(Input.mousePosition, out point);

        /// <summary>Flag (fine vertex id, 0..569) within <see cref="VertexRadius"/>.</summary>
        public static int VertexAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.NearestVertex(p, VertexRadius) : -1;
        /// <summary>Road segment (fine edge id, 0..629) within <see cref="EdgeRadius"/>.</summary>
        public static int EdgeAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.NearestEdge(p, EdgeRadius) : -1;
        /// <summary>Hex corner (vertex id below CornerCount) within <see cref="CornerRadius"/>.</summary>
        public static int CornerAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.NearestCorner(p, CornerRadius) : -1;
        /// <summary>Coarse hex edge (0..209) within <see cref="HexEdgeRadius"/>.</summary>
        public static int HexEdgeAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.NearestHexEdge(p, HexEdgeRadius) : -1;
        public static int HexAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.HexAt(p) : -1;

        /// <summary>Same as <see cref="VertexAt"/> / <see cref="EdgeAt"/>, named for the fine level.</summary>
        public static int FlagAt(Vector2 screenPoint) => VertexAt(screenPoint);
        public static int SegmentAt(Vector2 screenPoint) => EdgeAt(screenPoint);

        public static int VertexUnderMouse() => VertexAt(Input.mousePosition);
        public static int EdgeUnderMouse() => EdgeAt(Input.mousePosition);
        public static int CornerUnderMouse() => CornerAt(Input.mousePosition);
        public static int HexEdgeUnderMouse() => HexEdgeAt(Input.mousePosition);
        public static int HexUnderMouse() => HexAt(Input.mousePosition);
    }
}
