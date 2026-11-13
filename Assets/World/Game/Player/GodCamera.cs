using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Orthographic overhead camera for God View. WASD / arrows or middle-drag to pan, wheel to zoom,
    /// H frames the home village, M (or zooming fully out) shows the whole island.
    /// The focus point (ground point at the screen centre) is clamped to the board.
    /// Sizes are orthographic half-heights in metres: <see cref="DefaultSize"/> frames the village and its
    /// neighbouring hexes; <see cref="MaxSize"/> (set from the island radius at boot) frames the whole island.
    /// </summary>
    public sealed class GodCamera : MonoBehaviour
    {
        public const float Pitch = 62f, Distance = 900f, MinSize = 12f, DefaultSize = 115f;
        /// <summary>NPC name plates show in God View only up to this zoom-out.</summary>
        public const float NameplateMaxSize = 60f;
        /// <summary>God View ground labels grow with zoom-out up to this factor so they stay readable on the whole island.</summary>
        public const float MaxLabelScale = 3.2f;

        public Camera Camera { get; private set; }
        public Vector3 Focus { get; private set; }
        /// <summary>Largest zoom-out (ortho half-height, m); <see cref="SizeToFrame"/> of the island radius.</summary>
        public float MaxSize = 520f;
        public float Size { get => Camera.orthographicSize; set => Camera.orthographicSize = Mathf.Clamp(value, MinSize, MaxSize); }
        public float ClampRadius = 100f;
        /// <summary>Where H returns to (the village centre).</summary>
        public Vector3 HomeFocus;
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
            g.Camera.orthographicSize = DefaultSize;
            g.Camera.nearClipPlane = 1f;
            g.Camera.farClipPlane = Distance + 1600f;
            g.Camera.clearFlags = CameraClearFlags.SolidColor;
            g.Camera.backgroundColor = new Color(0.20f, 0.40f, 0.52f);
            g.Camera.enabled = false;
            return g;
        }

        /// <summary>
        /// Ortho half-height that shows a ground disc of the given radius around the focus at any aspect of 4:3 or wider
        /// (the ground is foreshortened by sin(Pitch) vertically).
        /// </summary>
        public static float SizeToFrame(float radius, float aspect = 4f / 3f)
            => Mathf.Max(radius * Mathf.Sin(Pitch * Mathf.Deg2Rad), radius / Mathf.Max(0.5f, aspect)) * 1.04f;

        public void SetFocus(Vector3 focus, float? size = null)
        {
            focus.y = BoardSpace.GroundY;
            var flat = new Vector3(focus.x, 0, focus.z);
            if (flat.magnitude > ClampRadius) flat = flat.normalized * ClampRadius;
            Focus = new Vector3(flat.x, BoardSpace.GroundY, flat.z);
            if (size.HasValue) Size = size.Value;
            Apply();
        }

        /// <summary>Default framing: the home village and its neighbouring hexes.</summary>
        public void FrameHome() => SetFocus(HomeFocus, DefaultSize);
        /// <summary>The whole island.</summary>
        public void FrameIsland() => SetFocus(Vector3.zero, MaxSize);

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
                if (Input.GetKeyDown(KeyCode.H)) { FrameHome(); return; }
                if (Input.GetKeyDown(KeyCode.M)) { FrameIsland(); return; }
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
            if (wheel != 0)
            {
                // Zoom about the point under the mouse so the overview is comfortable on the large map.
                bool hadPoint = BoardPicker.GroundPointAt(mouse, out var before);
                Size *= Mathf.Pow(0.85f, wheel);
                Apply();
                if (hadPoint && BoardPicker.GroundPointAt(mouse, out var after)) f += before - after;
            }
            SetFocus(f);
        }

        private void LateUpdate()
        {
            if (Camera == null || !Camera.enabled) return;
            float s = Mathf.Clamp(Size / DefaultSize, 1f, MaxLabelScale);
            if (Mathf.Abs(s - _labelScale) < 0.01f) return;
            _labelScale = s;
            foreach (var l in ScaledLabels) if (l != null) l.localScale = Vector3.one * s;
        }

        private float _labelScale = 1f;
        /// <summary>Ground labels (God View overlay) scaled with zoom-out.</summary>
        public static readonly System.Collections.Generic.List<Transform> ScaledLabels = new System.Collections.Generic.List<Transform>();
    }
}
