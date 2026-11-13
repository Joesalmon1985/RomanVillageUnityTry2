using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Orthographic overhead camera for God View. WASD / arrows or middle-drag to pan, wheel to zoom.
    /// The focus point (ground point at the screen centre) is clamped to the board.
    /// </summary>
    public sealed class GodCamera : MonoBehaviour
    {
        public const float Pitch = 62f, Distance = 160f, MinSize = 12f, MaxSize = 110f;

        public Camera Camera { get; private set; }
        public Vector3 Focus { get; private set; }
        public float Size { get => Camera.orthographicSize; set => Camera.orthographicSize = Mathf.Clamp(value, MinSize, MaxSize); }
        public float ClampRadius = 100f;
        /// <summary>When false (e.g. a text field has focus) keyboard panning is ignored.</summary>
        public bool KeyboardPan = true;
        private Vector3 _lastMouse;

        public static GodCamera Create(Transform parent)
        {
            var go = new GameObject("God Camera");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GodCamera>();
            g.Camera = go.AddComponent<Camera>();
            g.Camera.orthographic = true;
            g.Camera.orthographicSize = 70f;
            g.Camera.nearClipPlane = 1f;
            g.Camera.farClipPlane = 600f;
            g.Camera.clearFlags = CameraClearFlags.SolidColor;
            g.Camera.backgroundColor = new Color(0.30f, 0.42f, 0.30f);
            g.Camera.enabled = false;
            return g;
        }

        public void SetFocus(Vector3 focus, float? size = null)
        {
            focus.y = BoardSpace.GroundY;
            var flat = new Vector3(focus.x, 0, focus.z);
            if (flat.magnitude > ClampRadius) flat = flat.normalized * ClampRadius;
            Focus = new Vector3(flat.x, BoardSpace.GroundY, flat.z);
            if (size.HasValue) Size = size.Value;
            Apply();
        }

        private void Apply()
        {
            var rot = Quaternion.Euler(Pitch, 0, 0);
            transform.SetPositionAndRotation(Focus - rot * Vector3.forward * Distance, rot);
        }

        private void Update()
        {
            if (Camera == null || !Camera.enabled) return;
            float dt = Time.unscaledDeltaTime;
            var pan = Vector3.zero;
            if (KeyboardPan)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) pan.z += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) pan.z -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) pan.x += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1;
            }
            var f = Focus + pan.normalized * (Size * 1.2f * dt);
            var mouse = Input.mousePosition;
            if (Input.GetMouseButton(2) && !Input.GetMouseButtonDown(2))
            {
                float k = 2f * Size / Mathf.Max(1, Screen.height);
                var d = mouse - _lastMouse;
                f -= new Vector3(d.x * k, 0, d.y * k / Mathf.Sin(Pitch * Mathf.Deg2Rad));
            }
            _lastMouse = mouse;
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0) Size *= Mathf.Pow(0.88f, wheel);
            SetFocus(f);
        }
    }
}
