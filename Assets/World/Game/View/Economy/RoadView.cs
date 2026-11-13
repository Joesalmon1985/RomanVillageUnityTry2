using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>A paved strip along one 20 m road segment (flag to flag). Inspectable; created and removed from sim state.</summary>
    public sealed class RoadView : MonoBehaviour, IInspectable
    {
        public const float Width = 2.6f;
        public int Edge { get; private set; }

        public string Title => "Road";
        public IEnumerable<string> Lines
        {
            get
            {
                var b = WorldBootstrap.Instance;
                return b == null ? new string[0] : EconomyText.RoadLines(b.Sim.State, Edge);
            }
        }

        private static Material _mat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _mat = null; }

        public static RoadView Create(Transform parent, int edge)
        {
            BoardSpace.EdgeEnds(edge, out var a, out var b);
            var go = new GameObject("Road " + edge);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<RoadView>();
            v.Edge = edge;

            var mid = (a + b) * 0.5f;
            var dir = b - a; dir.y = 0; float len = dir.magnitude; dir /= len;
            float yaw = BoardSpace.YawOf(dir);
            go.transform.SetPositionAndRotation(mid, Quaternion.Euler(0, yaw, 0));
            // Local frame: +Z along the road, X across. Ends overlap the flags slightly so joints at corners close.
            var mb = new MeshBuilder();
            float y = 0.07f, half = len / 2 + 0.6f;
            mb.Strip(new Vector3(0, y, -half), new Vector3(0, y, half), Width, new Color(0.46f, 0.40f, 0.33f));
            var rnd = new PropRandom(edge * 31 + 7);
            float step = 0.9f;
            int rows = Mathf.FloorToInt(2 * half / step);
            for (int i = 0; i < rows; i++)
            {
                float t0 = -half + (i + 0.06f) * step, t1 = -half + (i + 0.94f) * step;
                for (int k = 0; k < 2; k++)
                {
                    float w0 = -Width * 0.42f + k * Width * 0.42f + (i % 2 == 0 ? 0 : 0.2f);
                    float w1 = w0 + Width * 0.38f;
                    w0 = Mathf.Max(w0, -Width * 0.44f); w1 = Mathf.Min(w1, Width * 0.44f);
                    var c = new Color(0.72f, 0.67f, 0.58f) * rnd.Range(0.9f, 1.05f); c.a = 1;
                    float yy = y + 0.012f;
                    mb.Quad(new Vector3(w0, yy, t0), new Vector3(w0, yy, t1), new Vector3(w1, yy, t1), new Vector3(w1, yy, t0), c);
                }
            }
            var kerb = new Color(0.38f, 0.33f, 0.27f);
            foreach (float s in new[] { -1f, 1f })
                mb.Box(new Vector3(s * Width * 0.5f, 0.1f, 0), new Vector3(0.16f, 0.14f, len - 3.0f), kerb);
            var paving = mb.ToObject("Paving", go.transform, _mat != null ? _mat : (_mat = MaterialFactory.CreateVertexColored(Color.white, "Road")));
            paving.transform.localPosition = Vector3.zero;
            paving.transform.localRotation = Quaternion.identity;
            var r = paving.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Pick volume (thin trigger along the segment, kept away from the flags).
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 0.3f, 0);
            col.size = new Vector3(Width, 0.6f, Mathf.Max(1f, len - 5f));
            return v;
        }
    }
}
