using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// A genuine planar mirror: a secondary camera renders the room, as seen from the viewer's eye position mirrored
    /// through the mirror plane, into a RenderTexture shown on the mirror surface.
    ///
    /// The mirror frame is this transform: position = centre of the glass, forward = surface normal pointing INTO the
    /// room (towards the viewer), up = world up. The reflection camera sits at the mirrored eye position behind the
    /// glass, looks into the room, and uses an off-axis frustum that passes exactly through the glass rectangle (near
    /// plane == glass plane, so nothing behind the glass is rendered). The texture is mapped with plain UVs.
    ///
    /// Culling layers: the player's own Sim lives on <see cref="PlayerLayer"/>. The viewer (eye) camera never renders
    /// that layer; only the reflection camera does, so the player's body exists visually only inside the mirror.
    /// </summary>
    public class MirrorReflectionController : MonoBehaviour
    {
        public Camera ViewerCamera;
        public int PlayerLayer = 8;
        [Tooltip("Layer of the glass quad itself; excluded from the reflection camera so it never renders its own texture.")]
        public int GlassLayer = 9;
        public Vector2 GlassSize = new Vector2(0.8f, 1.9f);
        public int TextureHeight = 1024;
        public Shader MirrorShader;
        public float FarClip = 40f;

        public Camera ReflectionCamera { get; private set; }
        public RenderTexture Texture { get; private set; }
        public MeshRenderer Surface { get; private set; }
        public int RenderCount { get; private set; }

        Material _material;

        /// <summary>Configure and build the mirror. Call once after adding the component.</summary>
        public void Initialize(Camera viewer, Vector2 glassSize, int playerLayer, int textureHeight = 1024)
        {
            ViewerCamera = viewer;
            GlassSize = glassSize;
            PlayerLayer = playerLayer;
            TextureHeight = textureHeight;
            Build();
        }

        void Build()
        {
            if (Surface != null) return;
            if (MirrorShader == null) MirrorShader = Shader.Find("Bedsit/Mirror");

            // surface quad. Local +x equals the reflection image's +u direction, so plain UVs are correct.
            var go = new GameObject("MirrorGlass") { layer = GlassLayer };
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            Surface = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = "MirrorGlassQuad" };
            float hw = GlassSize.x * 0.5f, hh = GlassSize.y * 0.5f;
            mesh.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            _material = new Material(MirrorShader) { name = "MirrorMaterial" };
            Surface.sharedMaterial = _material;
            Surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Surface.receiveShadows = false;

            int th = Mathf.Max(64, TextureHeight);
            int tw = Mathf.Max(64, Mathf.RoundToInt(th * GlassSize.x / GlassSize.y));
            Texture = new RenderTexture(tw, th, 24, RenderTextureFormat.ARGB32) { name = "MirrorRT", antiAliasing = 1 };
            Texture.Create();
            _material.mainTexture = Texture;

            var camGo = new GameObject("MirrorCamera");
            camGo.transform.SetParent(transform, false);
            ReflectionCamera = camGo.AddComponent<Camera>();
            ReflectionCamera.enabled = false;                    // rendered manually
            ReflectionCamera.targetTexture = Texture;
            ReflectionCamera.useOcclusionCulling = false;
            ReflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            ReflectionCamera.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
        }

        /// <summary>Configure culling so the player's layer is rendered by the mirror only.</summary>
        void ApplyCulling()
        {
            int mask = 1 << PlayerLayer;
            if (ViewerCamera != null) ViewerCamera.cullingMask &= ~mask;
            ReflectionCamera.cullingMask = ((ViewerCamera != null ? ViewerCamera.cullingMask : ~0) | mask) & ~(1 << GlassLayer);
        }

        /// <summary>A mirror never reflects itself: put the frame (and anything attached to it) on the glass layer so the
        /// reflection camera skips it, while the eye camera still sees it.</summary>
        public void ExcludeFromReflection(GameObject go)
        {
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GlassLayer;
        }

        void LateUpdate() => Render();

        /// <summary>Render the reflection for the viewer's current eye position (also callable from tests).</summary>
        public void Render()
        {
            Build();
            if (ViewerCamera == null) return;
            ApplyCulling();
            Surface.enabled = true;

            Vector3 c = transform.position;
            Vector3 n = transform.forward;
            Vector3 u = transform.up;
            Vector3 eye = ViewerCamera.transform.position;

            float s = Vector3.Dot(eye - c, n);                  // distance of the eye in front of the glass
            if (s < 0.05f) return;                              // eye behind/at the mirror: nothing sensible to draw

            Vector3 mirroredEye = eye - 2f * s * n;
            var t = ReflectionCamera.transform;
            t.position = mirroredEye;
            t.rotation = Quaternion.LookRotation(n, u);

            Vector3 camRight = t.right;                         // = -(viewer's right)
            float hw = GlassSize.x * 0.5f, hh = GlassSize.y * 0.5f;
            Vector3 right = transform.right;                    // local +x of the mirror frame (== camRight)
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = c + right * ((i & 1) == 0 ? -hw : hw) + u * ((i & 2) == 0 ? -hh : hh);
                Vector3 v = corner - mirroredEye;
                float x = Vector3.Dot(v, camRight), y = Vector3.Dot(v, u);
                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
                yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
            }
            float near = s + 0.01f;                             // near plane just in front of the glass plane (skips the glass and coplanar frame parts)
            ReflectionCamera.nearClipPlane = near;
            ReflectionCamera.farClipPlane = near + FarClip;
            ReflectionCamera.projectionMatrix = Matrix4x4.Frustum(xMin, xMax, yMin, yMax, near, near + FarClip);
            ReflectionCamera.Render();
            RenderCount++;
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            if (_material != null) Destroy(_material);
        }
    }
}
