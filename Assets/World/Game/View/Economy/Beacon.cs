using UnityEngine;

namespace World.Game
{
    /// <summary>A temporary pointer in the world: a light column with a bobbing arrow and a caption, gone after a while.</summary>
    public sealed class Beacon : MonoBehaviour
    {
        public string Caption;
        public float Seconds = 30f;
        public Vector3 LabelAnchor => transform.position + Vector3.up * 13f;
        private Transform _arrow;
        private float _t;

        public static Beacon Show(Vector3 ground, string caption, float seconds = 30f, Color? color = null)
        {
            var c = color ?? new Color(1f, 0.85f, 0.25f);
            var go = new GameObject("Beacon " + caption);
            if (WorldBootstrap.Instance != null) go.transform.SetParent(WorldBootstrap.Instance.transform, false);
            go.transform.position = new Vector3(ground.x, BoardSpace.GroundY, ground.z);
            var b = go.AddComponent<Beacon>();
            b.Caption = caption; b.Seconds = seconds;
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Column", new Vector3(0, 12f, 0), new Vector3(1.2f, 12f, 1.2f), MaterialFactory.CreateGlow(new Color(c.r, c.g, c.b, 0.45f)));
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Ring", new Vector3(0, 0.08f, 0), new Vector3(5f, 0.04f, 5f), MaterialFactory.CreateGlow(new Color(c.r, c.g, c.b, 0.6f)));
            b._arrow = new GameObject("Arrow").transform;
            b._arrow.SetParent(go.transform, false);
            var mb = new MeshBuilder();
            mb.Cone(new Vector3(0, 0, 0), 1.3f, 2.2f, 8, Color.white);
            mb.Cylinder(new Vector3(0, -2.4f, 0), 0.5f, 0.5f, 2.4f, 8, Color.white);
            var arrow = mb.ToObject("Shape", b._arrow, MaterialFactory.CreateUnlit(c));
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.Euler(180f, 0, 0); // tip down
            BeaconRegistry.Add(b);
            return b;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_arrow != null) { _arrow.localPosition = new Vector3(0, 4.5f + Mathf.Sin(_t * 3f) * 0.8f, 0); _arrow.Rotate(0, 90f * Time.unscaledDeltaTime, 0); }
            if (_t > Seconds) Destroy(gameObject);
        }

        private void OnDestroy() => BeaconRegistry.Remove(this);
    }

    /// <summary>Live beacons (for labels and tests).</summary>
    public static class BeaconRegistry
    {
        public static readonly System.Collections.Generic.List<Beacon> All = new System.Collections.Generic.List<Beacon>();
        public static void Add(Beacon b) { if (!All.Contains(b)) All.Add(b); }
        public static void Remove(Beacon b) => All.Remove(b);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => All.Clear();
    }
}
