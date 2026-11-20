using UnityEngine;

namespace World.Game
{
    /// <summary>A place's local frame: origin, yaw, +Z forward ("the front"), +X to the right of it.</summary>
    public struct HubFrame
    {
        public Vector3 O; public float Yaw;
        public HubFrame(Vector3 o, float yaw) { O = o; Yaw = yaw; }
        public Vector3 W(float x, float z)
        {
            var f = BoardSpace.DirOf(Yaw); var r = new Vector3(f.z, 0, -f.x);
            return O + f * z + r * x;
        }
        public Vector3 Forward => BoardSpace.DirOf(Yaw);
    }

    /// <summary>Small shared pieces for the hub's places (original primitive art, vertex coloured, local frame, base at y 0).</summary>
    public static class HubArt
    {
        public static readonly Color Straw = new Color(0.74f, 0.62f, 0.32f), StrawDark = new Color(0.55f, 0.44f, 0.22f),
            Herb = new Color(0.30f, 0.52f, 0.24f), HerbDry = new Color(0.58f, 0.55f, 0.30f), Grass = new Color(0.20f, 0.42f, 0.17f),
            Sand = new Color(0.62f, 0.53f, 0.38f), Slab = new Color(0.58f, 0.55f, 0.50f), Wool = new Color(0.93f, 0.91f, 0.86f),
            Grain = new Color(0.84f, 0.70f, 0.30f), Brick = new Color(0.66f, 0.38f, 0.26f), Water = new Color(0.30f, 0.45f, 0.58f);

        /// <summary>A game object with one vertex-coloured mesh at a frame.</summary>
        public static GameObject Make(Transform parent, string name, HubFrame f, MeshBuilder mb)
        {
            var go = mb.ToObject(name, parent, MaterialFactory.CreateVertexColored(Color.white, name));
            go.transform.SetPositionAndRotation(f.O, Quaternion.Euler(0, f.Yaw, 0));
            return go;
        }

        /// <summary>A trigger box the pointer can hit, carrying <typeparamref name="T"/> (local to the frame).</summary>
        public static T Pick<T>(Transform parent, string name, HubFrame f, Vector3 centre, Vector3 size) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(f.O, Quaternion.Euler(0, f.Yaw, 0));
            LandLook.PickBox(go, centre, size);
            return go.AddComponent<T>();
        }

        /// <summary>A warm point light (a hearth, a torch) so a place reads at night.</summary>
        public static Light Glow(Transform parent, Vector3 world, float range, float intensity)
        {
            var g = new GameObject("Glow");
            g.transform.SetParent(parent, false);
            g.transform.position = world;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1f, 0.74f, 0.42f);
            return l;
        }

        public static void Basket(MeshBuilder mb, Vector3 p, Color contents, int kind)
        {
            mb.Cylinder(p, 0.55f, 0.45f, 0.5f, 8, CampBuilder.WoodLight);
            mb.Cylinder(p + Vector3.up * 0.46f, 0.5f, 0.5f, 0.06f, 8, contents);
            if (kind == 0) for (int i = 0; i < 3; i++) mb.Cone(p + new Vector3(-0.2f + i * 0.2f, 0.5f, 0), 0.12f, 0.35f, 5, contents); // sheaves
            else if (kind == 1) mb.Box(p + new Vector3(0, 0.62f, 0), new Vector3(0.5f, 0.2f, 0.3f), contents); // bricks
            else if (kind == 2) for (int i = 0; i < 3; i++) CampBuilder.Beam(mb, p + new Vector3(-0.2f + i * 0.2f, 0.45f, -0.3f), p + new Vector3(-0.2f + i * 0.2f, 0.9f, 0.3f), 0.12f, CampBuilder.Wood); // logs
            else mb.Cylinder(p + Vector3.up * 0.5f, 0.42f, 0.2f, 0.3f, 7, Wool); // fleece
        }

        /// <summary>A fence of posts and two rails along a polyline (a gap is left where the gate is).</summary>
        public static void FenceRing(MeshBuilder mb, Vector3 centre, float radius, int posts, float gateYaw, float gateWidthDeg, float height, Color post, Color rail)
        {
            Vector3 P(int i, float y) { float a = i * Mathf.PI * 2f / posts; return centre + new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius); }
            for (int i = 0; i < posts; i++)
            {
                float deg = i * 360f / posts, mid = (i + 0.5f) * 360f / posts;
                bool gate = Mathf.Abs(Mathf.DeltaAngle(mid, gateYaw)) < gateWidthDeg * 0.5f;
                CampBuilder.Beam(mb, P(i, 0), P(i, height), 0.14f, post);
                if (gate) continue;
                int n = (i + 1) % posts;
                CampBuilder.Beam(mb, P(i, height * 0.9f), P(n, height * 0.9f), 0.07f, rail);
                CampBuilder.Beam(mb, P(i, height * 0.5f), P(n, height * 0.5f), 0.07f, rail);
            }
        }

        /// <summary>A white lamb (body, head, four legs), facing local +Z.</summary>
        public static void Lamb(MeshBuilder mb, Vector3 p, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 L(float x, float y, float z) => p + q * new Vector3(x, y, z);
            mb.Box(L(0, 0.42f, 0), new Vector3(0.38f, 0.34f, 0.6f), Wool, yaw);
            mb.Box(L(0, 0.55f, 0.38f), new Vector3(0.2f, 0.2f, 0.22f), CampBuilder.Fur, yaw);
            for (int i = 0; i < 4; i++) mb.Box(L((i % 2 - 0.5f) * 0.22f, 0.12f, (i / 2 - 0.5f) * 0.36f), new Vector3(0.07f, 0.26f, 0.07f), CampBuilder.Fur, yaw);
        }
    }
}
