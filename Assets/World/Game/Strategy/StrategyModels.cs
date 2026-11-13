using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Original primitive-built models for the strategy layer (vertex-coloured meshes from <see cref="MeshBuilder"/>):
    /// a walled city, a demon, a soldier, a guard, the hero's staff, wonder stages, a power bolt, a robber marker.
    /// </summary>
    public static class StrategyModels
    {
        private static Material _vc;
        private static readonly Dictionary<Color32, Material> Glows = new Dictionary<Color32, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _vc = null; Glows.Clear(); }

        public static Material VertexMat => _vc != null ? _vc : (_vc = MaterialFactory.CreateVertexColored(Color.white, "StrategyModels"));

        public static Material Glow(Color c)
        {
            Color32 k = c;
            if (!Glows.TryGetValue(k, out var m) || m == null) { m = MaterialFactory.CreateGlow(c, "StrategyGlow"); Glows[k] = m; }
            return m;
        }

        public static readonly Color PlayerBlue = new Color(0.25f, 0.50f, 0.95f);
        public static readonly Color RivalRed = new Color(0.80f, 0.18f, 0.15f);
        private static readonly Color Stone = new Color(0.66f, 0.62f, 0.55f), DarkStone = new Color(0.45f, 0.42f, 0.38f);
        private static readonly Color Timber = new Color(0.46f, 0.32f, 0.20f), Roof = new Color(0.30f, 0.38f, 0.62f);

        private static GameObject Obj(MeshBuilder mb, string name, Transform parent) => mb.ToObject(name, parent, VertexMat);

        // ---------------- city ----------------

        /// <summary>A small walled town (about 15 m across, 12 m tall), blue roofs and banner: distinct from the home square.</summary>
        public static GameObject City(Transform parent, int seed)
        {
            var mb = new MeshBuilder();
            var rnd = new PropRandom(seed);
            const int sides = 8;
            const float R = 7f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * R, 0, Mathf.Sin(a0) * R);
                var p1 = new Vector3(Mathf.Cos(a1) * R, 0, Mathf.Sin(a1) * R);
                var mid = (p0 + p1) / 2; float len = (p1 - p0).magnitude;
                float yaw = Mathf.Atan2(p1.x - p0.x, p1.z - p0.z) * Mathf.Rad2Deg;
                if (i == 0) // gate: two short wall pieces and an arch
                {
                    mb.Box(Vector3.Lerp(p0, p1, 0.18f) + Vector3.up * 1.6f, new Vector3(0.9f, 3.2f, len * 0.36f), Stone, yaw);
                    mb.Box(Vector3.Lerp(p0, p1, 0.82f) + Vector3.up * 1.6f, new Vector3(0.9f, 3.2f, len * 0.36f), Stone, yaw);
                    mb.Box(mid + Vector3.up * 3.6f, new Vector3(1.0f, 0.8f, len * 0.5f), DarkStone, yaw);
                }
                else mb.Box(mid + Vector3.up * 1.6f, new Vector3(0.9f, 3.2f, len + 0.4f), i % 2 == 0 ? Stone : Stone * 0.95f, yaw);
                mb.Cylinder(p0, 0.9f, 0.85f, 4.4f, 8, DarkStone);
                mb.Cone(p0 + Vector3.up * 4.4f, 1.15f, 1.6f, 8, Roof);
            }
            // Keep in the middle.
            mb.Box(new Vector3(0, 3.5f, 0), new Vector3(3.6f, 7f, 3.6f), Stone);
            mb.Box(new Vector3(0, 7.3f, 0), new Vector3(4.2f, 0.6f, 4.2f), DarkStone);
            mb.Cone(new Vector3(0, 7.6f, 0), 2.6f, 3.2f, 4, Roof);
            mb.Box(new Vector3(0, 12.4f, 0), new Vector3(0.12f, 3.6f, 0.12f), Timber);
            mb.Box(new Vector3(0.75f, 13.5f, 0), new Vector3(1.4f, 0.9f, 0.06f), PlayerBlue);
            // Houses inside the walls.
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f + rnd.Range(-12f, 12f)) * Mathf.Deg2Rad;
                var c = new Vector3(Mathf.Cos(a) * 4.3f, 0, Mathf.Sin(a) * 4.3f);
                float yaw = -a * Mathf.Rad2Deg;
                mb.Box(c + Vector3.up * 1.1f, new Vector3(2.2f, 2.2f, 1.8f), new Color(0.86f, 0.80f, 0.68f), yaw);
                mb.Box(c + Vector3.up * 2.5f, new Vector3(2.5f, 0.6f, 2.0f), Roof * 0.9f, yaw);
            }
            mb.FanUp(new Vector3(0, 0.05f, 0), Ring(R + 0.5f, 16, 0.05f), new Color(0.55f, 0.50f, 0.42f));
            return Obj(mb, "City", parent);
        }

        private static List<Vector3> Ring(float r, int n, float y)
        {
            var l = new List<Vector3>();
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; l.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); }
            return l;
        }

        // ---------------- demon ----------------

        public sealed class DemonParts { public GameObject Root; public Transform Body; public Renderer[] Eyes; }

        /// <summary>A hunched, horned shadow creature about 2.2 m tall with glowing eyes and a spiked back.</summary>
        public static DemonParts Demon(Transform parent, int seed)
        {
            var rnd = new PropRandom(seed);
            var root = new GameObject("Demon");
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var mb = new MeshBuilder();
            var skin = Color.Lerp(new Color(0.10f, 0.05f, 0.12f), new Color(0.22f, 0.06f, 0.10f), rnd.Next());
            var hide = skin * 1.6f; hide.a = 1;
            // Legs (bent back).
            mb.Box(new Vector3(-0.28f, 0.45f, 0.1f), new Vector3(0.22f, 0.9f, 0.26f), skin, 0);
            mb.Box(new Vector3(0.28f, 0.45f, 0.1f), new Vector3(0.22f, 0.9f, 0.26f), skin, 0);
            // Hunched torso: three boxes stepping forward.
            mb.Box(new Vector3(0, 1.1f, 0.05f), new Vector3(0.8f, 0.5f, 0.55f), hide);
            mb.Box(new Vector3(0, 1.5f, 0.25f), new Vector3(0.95f, 0.45f, 0.6f), skin);
            mb.Box(new Vector3(0, 1.82f, 0.48f), new Vector3(0.8f, 0.35f, 0.55f), hide);
            // Spikes down the back.
            for (int i = 0; i < 4; i++) mb.Cone(new Vector3(0, 1.2f + i * 0.2f, -0.18f + i * 0.17f), 0.12f, 0.45f, 4, new Color(0.05f, 0.02f, 0.04f));
            // Long arms reaching to the ground.
            mb.Box(new Vector3(-0.62f, 1.15f, 0.55f), new Vector3(0.16f, 1.3f, 0.18f), skin);
            mb.Box(new Vector3(0.62f, 1.15f, 0.55f), new Vector3(0.16f, 1.3f, 0.18f), skin);
            for (int k = -1; k <= 1; k++)
            {
                mb.Cone(new Vector3(-0.62f + k * 0.07f, 0.48f, 0.62f), 0.04f, -0.25f, 3, Color.black);
                mb.Cone(new Vector3(0.62f + k * 0.07f, 0.48f, 0.62f), 0.04f, -0.25f, 3, Color.black);
            }
            // Head with horns and a jaw.
            mb.Box(new Vector3(0, 2.0f, 0.82f), new Vector3(0.5f, 0.42f, 0.5f), skin);
            mb.Box(new Vector3(0, 1.82f, 0.98f), new Vector3(0.42f, 0.12f, 0.3f), new Color(0.3f, 0.02f, 0.04f));
            var hornC = new Color(0.85f, 0.80f, 0.70f);
            mb.Cylinder(new Vector3(-0.2f, 2.2f, 0.78f), 0.08f, 0.01f, 0.7f, 5, hornC);
            mb.Cylinder(new Vector3(0.2f, 2.2f, 0.78f), 0.08f, 0.01f, 0.7f, 5, hornC);
            Obj(mb, "Flesh", body);
            var eyes = new List<Renderer>();
            foreach (float x in new[] { -0.12f, 0.12f })
            {
                var e = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(e.GetComponent<Collider>());
                e.name = "Eye";
                e.transform.SetParent(body, false);
                e.transform.localPosition = new Vector3(x, 2.06f, 1.08f);
                e.transform.localScale = new Vector3(0.11f, 0.07f, 0.04f);
                var r = e.GetComponent<Renderer>();
                r.sharedMaterial = Glow(new Color(1f, 0.15f, 0.05f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eyes.Add(r);
            }
            return new DemonParts { Root = root, Body = body, Eyes = eyes.ToArray() };
        }

        // ---------------- soldiers ----------------

        public sealed class SoldierParts { public GameObject Root; public Transform Spear; public Transform Body; }

        /// <summary>A soldier (1.9 m): tunic in the side's colour, helmet, round shield, spear. Guards get a square shield.</summary>
        public static SoldierParts Soldier(Transform parent, Color team, bool guard)
        {
            var root = new GameObject(guard ? "Guard" : "Soldier");
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var mb = new MeshBuilder();
            var dark = new Color(0.2f, 0.18f, 0.16f);
            var metal = new Color(0.62f, 0.64f, 0.68f);
            mb.Box(new Vector3(-0.14f, 0.45f, 0), new Vector3(0.18f, 0.9f, 0.2f), dark);
            mb.Box(new Vector3(0.14f, 0.45f, 0), new Vector3(0.18f, 0.9f, 0.2f), dark);
            mb.Box(new Vector3(0, 1.2f, 0), new Vector3(0.55f, 0.65f, 0.32f), team);
            mb.Box(new Vector3(0, 1.0f, 0), new Vector3(0.6f, 0.12f, 0.36f), dark);
            mb.Box(new Vector3(0, 1.65f, 0), new Vector3(0.28f, 0.28f, 0.28f), new Color(0.85f, 0.7f, 0.55f));
            mb.Cylinder(new Vector3(0, 1.74f, 0), 0.2f, 0.17f, 0.16f, 8, metal);
            mb.Cone(new Vector3(0, 1.9f, 0), 0.17f, 0.18f, 8, metal);
            if (guard) mb.Box(new Vector3(-0.38f, 1.15f, 0.12f), new Vector3(0.08f, 0.8f, 0.55f), team * 0.7f);
            else
            {
                mb.Box(new Vector3(-0.38f, 1.1f, 0.12f), new Vector3(0.08f, 0.62f, 0.62f), team * 0.8f);
                mb.Box(new Vector3(-0.43f, 1.1f, 0.12f), new Vector3(0.04f, 0.2f, 0.2f), metal);
            }
            Obj(mb, "Figure", body);
            var spear = new GameObject("Spear").transform;
            spear.SetParent(body, false);
            spear.localPosition = new Vector3(0.36f, 1.15f, 0f);
            var smb = new MeshBuilder();
            smb.Box(new Vector3(0, 0.3f, 0.2f), new Vector3(0.05f, 0.05f, 2.2f), new Color(0.45f, 0.3f, 0.18f));
            smb.Cone(new Vector3(0, 0.3f, 1.3f), 0.06f, 0.3f, 4, metal);
            var so = Obj(smb, "Spear", spear);
            so.transform.localRotation = Quaternion.identity;
            return new SoldierParts { Root = root, Spear = spear, Body = body };
        }

        // ---------------- hero ----------------

        /// <summary>The hero's staff with a glowing orb and a gold banner (added to the hero's placeholder figure).</summary>
        public static GameObject HeroStaff(Transform parent)
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0.45f, 1.3f, 0.1f), new Vector3(0.07f, 2.6f, 0.07f), new Color(0.40f, 0.28f, 0.16f));
            mb.Box(new Vector3(0.75f, 2.2f, 0.1f), new Vector3(0.6f, 0.7f, 0.04f), new Color(0.95f, 0.78f, 0.25f));
            mb.Box(new Vector3(0, 1.1f, -0.22f), new Vector3(0.55f, 1.3f, 0.06f), new Color(0.75f, 0.15f, 0.15f));
            var go = Obj(mb, "HeroGear", parent);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(orb.GetComponent<Collider>());
            orb.name = "Orb";
            orb.transform.SetParent(go.transform, false);
            orb.transform.localPosition = new Vector3(0.45f, 2.75f, 0.1f);
            orb.transform.localScale = Vector3.one * 0.3f;
            orb.GetComponent<Renderer>().sharedMaterial = Glow(new Color(1f, 0.9f, 0.45f));
            return go;
        }

        // ---------------- wonder ----------------

        /// <summary>
        /// Wonder stage pieces, in the wonder-site model's own space (its top platform is at y 2.1, centred at z -0.8):
        /// stage 1 raises the four columns and an architrave, stage 2 an obelisk with a gilded tip (about 18 m).
        /// </summary>
        public static GameObject WonderStage(Transform parent, int stage)
        {
            var mb = new MeshBuilder();
            var marble = new Color(0.95f, 0.93f, 0.88f);
            if (stage == 1)
            {
                foreach (float x in new[] { -1.6f, 1.6f }) foreach (float z in new[] { -2.4f, 0.8f })
                    mb.Cylinder(new Vector3(x, 2.1f, z), 0.4f, 0.34f, 4.5f, 12, marble);
                mb.Box(new Vector3(0, 6.85f, -0.8f), new Vector3(4.6f, 0.5f, 4.6f), marble * 0.92f);
                mb.Box(new Vector3(0, 7.25f, -0.8f), new Vector3(3.8f, 0.3f, 3.8f), marble);
            }
            else
            {
                mb.Box(new Vector3(0, 7.9f, -0.8f), new Vector3(2.0f, 1.0f, 2.0f), marble * 0.95f);
                mb.Cylinder(new Vector3(0, 8.4f, -0.8f), 0.95f, 0.5f, 8.5f, 4, marble);
                mb.Cone(new Vector3(0, 16.9f, -0.8f), 0.7f, 1.5f, 4, new Color(1f, 0.82f, 0.3f));
            }
            return Obj(mb, "WonderStage" + stage, parent);
        }

        // ---------------- power bolt ----------------

        /// <summary>A lightning-bolt sign (about 1.6 m) for powered buildings; recoloured on/off by its material.</summary>
        public static GameObject Bolt(Transform parent)
        {
            var go = new GameObject("PowerBolt");
            go.transform.SetParent(parent, false);
            var pts = new[] { new Vector3(0.15f, 0.8f, 0), new Vector3(-0.15f, 0.1f, 0), new Vector3(0.12f, 0.1f, 0), new Vector3(-0.15f, -0.8f, 0) };
            for (int i = 0; i < 3; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(seg.GetComponent<Collider>());
                seg.transform.SetParent(go.transform, false);
                var a = pts[i]; var b = pts[i + 1];
                seg.transform.localPosition = (a + b) / 2;
                seg.transform.localScale = new Vector3(0.22f, (b - a).magnitude + 0.1f, 0.12f);
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
                seg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        public static void SetMaterial(GameObject go, Material m)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
        }

        // ---------------- flat overlays ----------------

        /// <summary>A ring on the ground (unlit), for territories and outbreak bursts.</summary>
        public static GameObject GroundRing(Transform parent, float radius, float width, Color c, float y = 0.12f, int seg = 96)
        {
            var mb = new MeshBuilder();
            TerrainBuilder.Ring(mb, new Vector3(0, y, 0), radius, width, seg, c);
            var go = mb.ToObject("Ring", parent, MaterialFactory.SharedUnlit(Color.white));
            return go;
        }
    }
}
