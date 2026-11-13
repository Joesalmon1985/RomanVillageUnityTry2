using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Board picking for God View tools: which vertex, edge or hex is under the mouse (or a screen point).
    /// Rays go through the active view camera and hit the flat ground plane. Returns -1 for none.
    /// </summary>
    public static class BoardPicker
    {
        /// <summary>Max ground distance (m) from the mouse point to a vertex / edge for it to count.</summary>
        public const float VertexRadius = 5f, EdgeRadius = 3f;

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

        public static int VertexAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.NearestVertex(p, VertexRadius) : -1;
        public static int EdgeAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.NearestEdge(p, EdgeRadius) : -1;
        public static int HexAt(Vector2 screenPoint) => GroundPointAt(screenPoint, out var p) ? BoardSpace.HexAt(p) : -1;

        public static int VertexUnderMouse() => VertexAt(Input.mousePosition);
        public static int EdgeUnderMouse() => EdgeAt(Input.mousePosition);
        public static int HexUnderMouse() => HexAt(Input.mousePosition);
    }
}
