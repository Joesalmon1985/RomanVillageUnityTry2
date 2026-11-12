using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// The visual (never textual) hint that the top control will do something: a small green diamond, in the spirit of a
    /// Sims plumbob, that floats above whatever the InteractionController currently addresses. It is a procedurally drawn
    /// UI marker, not a character or a game asset.
    /// </summary>
    public class InteractionCue : MonoBehaviour
    {
        public InteractionController Controller;
        public Camera ViewerCamera;
        public float Size = 0.16f;
        [Tooltip("Layer for the marker (the game puts it on the mirror-excluded layer so reflections never show UI cues).")]
        public int Layer = 0;

        public bool Visible { get; private set; }

        GameObject _marker;
        Material _material;
        Vector3 _anchor;
        float _appear;

        void Start()
        {
            _marker = new GameObject("InteractionCueMarker");
            _marker.layer = Layer;
            _marker.transform.SetParent(transform, false);
            _marker.AddComponent<MeshFilter>().sharedMesh = BuildDiamond();
            var mr = _marker.AddComponent<MeshRenderer>();
            _material = new Material(Shader.Find("Bedsit/Cue"));
            mr.sharedMaterial = _material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _marker.SetActive(false);
            if (Controller != null) Controller.CurrentChanged += OnCurrentChanged;
        }

        void OnDestroy()
        {
            if (Controller != null) Controller.CurrentChanged -= OnCurrentChanged;
            if (_material != null) Destroy(_material);
        }

        void OnCurrentChanged(InteractionTarget t)
        {
            Visible = t != null;
            if (t != null) { _anchor = t.CuePosition; _appear = 0f; }
            if (_marker != null) _marker.SetActive(Visible);
        }

        void LateUpdate()
        {
            if (!Visible || _marker == null) return;
            _appear = Mathf.MoveTowards(_appear, 1f, Time.deltaTime * 4f);
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.04f;
            _marker.transform.position = _anchor + Vector3.up * bob;
            _marker.transform.rotation = Quaternion.Euler(0f, Time.time * 70f, 0f);
            float dist = ViewerCamera != null ? Vector3.Distance(ViewerCamera.transform.position, _anchor) : 3f;
            _marker.transform.localScale = Vector3.one * (Size * Mathf.SmoothStep(0f, 1f, _appear) * Mathf.Clamp(dist / 3f, 0.6f, 1.6f));
            _material.SetFloat("_Pulse", 0.85f + 0.15f * Mathf.Sin(Time.time * 4f));
        }

        static Mesh BuildDiamond()
        {
            // elongated octahedron
            var v = new[]
            {
                new Vector3(0, 1.4f, 0), new Vector3(0, -1.4f, 0),
                new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1),
            };
            int[] tris = { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            // flat shading: duplicate vertices per face
            var verts = new Vector3[tris.Length];
            var idx = new int[tris.Length];
            for (int i = 0; i < tris.Length; i++) { verts[i] = v[tris[i]]; idx[i] = i; }
            var m = new Mesh { name = "CueDiamond", vertices = verts, triangles = idx };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
