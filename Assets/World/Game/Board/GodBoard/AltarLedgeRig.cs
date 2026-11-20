using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The altar ledge as real 3D objects (WU-2b, WORLD_UI_PLAN 2.2): a carved stone slab seen by its own camera, layered in
    /// front of the island at the bottom of the frame, lit by two torches. On it, modelled from primitives: five offering
    /// baskets whose goods follow the hand, the builder's bench with three sample pieces and the spare stacks, four banner
    /// stakes with point stones and the longest-road cord, the trader and his barter mat (it unrolls; goods move onto it),
    /// envoys who walk on holding bundles, the discard sack, the turn hourglass with flowing sand, the chronicler, the prince,
    /// the demon-ward totem and the lamb stone. Every object stands inside its layout rect from
    /// <see cref="GodBoardHud.LayoutLedge"/>; a camera-facing box collider covers that rect, and taps are 3D raycasts
    /// (<see cref="Pick"/>) into <see cref="GodBoardHud.TapLedge"/>. No digits are drawn: counts are read on the ledger tablet.
    /// The rig only reads the view model; it never touches the simulation.
    /// </summary>
    public sealed class AltarLedgeRig : MonoBehaviour
    {
        public const int Layer = 30;
        public const float Fov = 30f, Pitch = 30f, SlabY = -4f;
        public const int BasketCap = 9;
        /// <summary>Objects stand a little taller than their rect: the camera looks down on them, so they read foreshortened.</summary>
        public const float ModelBoost = 1.28f;
        public static readonly Vector3 Origin = new Vector3(0f, -20000f, 0f);

        public Camera Cam { get; private set; }
        public bool Shown { get; private set; }
        private readonly Dictionary<string, LedgeItem> _items = new Dictionary<string, LedgeItem>();
        public IReadOnlyDictionary<string, LedgeItem> Items => _items;
        private readonly int[] _shown = new int[Wares.RawCount];
        /// <summary>How many goods pieces basket <paramref name="g"/> shows now (the hand, capped at <see cref="BasketCap"/>).</summary>
        public int VisibleGoods(int g) => _shown[g];

        private GodBoardHud _hud;
        private GodBoardDirector _d;
        private Transform _root, _slab, _torches;
        private Light _torchL, _torchR, _fill;
        private Transform _flameL, _flameR;
        private float _matOpenedAt = -1f;

        private static readonly Color Stone = new Color(0.42f, 0.38f, 0.33f), StoneDark = new Color(0.26f, 0.23f, 0.20f), StoneLight = new Color(0.58f, 0.53f, 0.45f);
        private static readonly Color Wicker = new Color(0.62f, 0.44f, 0.22f), WickerDark = new Color(0.36f, 0.24f, 0.11f);
        private static readonly Color Wood = new Color(0.47f, 0.32f, 0.18f), WoodDark = new Color(0.30f, 0.20f, 0.11f);
        private static readonly Color Skin = new Color(0.78f, 0.58f, 0.42f), Linen = new Color(0.86f, 0.80f, 0.66f), Gold = new Color(1f, 0.82f, 0.32f);
        private static readonly Color[] GoodCol =
        {
            new Color(0.50f, 0.32f, 0.16f), new Color(0.74f, 0.34f, 0.19f), new Color(0.93f, 0.78f, 0.30f), new Color(0.95f, 0.94f, 0.89f), new Color(0.60f, 0.62f, 0.66f),
        };

        public static AltarLedgeRig Create(GodBoardHud hud, GodBoardDirector d)
        {
            var go = new GameObject("AltarLedge3D");
            go.transform.SetParent(hud.transform, false);
            go.transform.position = Origin;
            var r = go.AddComponent<AltarLedgeRig>();
            r._hud = hud; r._d = d;
            r.Build();
            return r;
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            _root = transform;
            var camGo = new GameObject("Ledge Camera");
            camGo.transform.SetParent(_root, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.Depth;
            Cam.cullingMask = 1 << Layer;
            Cam.fieldOfView = Fov;
            Cam.nearClipPlane = 0.3f; Cam.farClipPlane = 80f;
            Cam.depth = 40f;
            Cam.allowHDR = false; Cam.allowMSAA = true;
            Cam.enabled = false;
            foreach (var c in Camera.allCameras) if (c != Cam) c.cullingMask &= ~(1 << Layer);
            if (_d.BoardCam != null) _d.BoardCam.cullingMask &= ~(1 << Layer);

            _torchL = MakeLight("Torch L", LightType.Point, new Color(1f, 0.62f, 0.30f), 2.4f, 11f);
            _torchR = MakeLight("Torch R", LightType.Point, new Color(1f, 0.62f, 0.30f), 2.4f, 11f);
            _fill = MakeLight("Ledge fill", LightType.Directional, new Color(1f, 0.82f, 0.62f), 0.55f, 0f);
            _fill.transform.localRotation = Quaternion.Euler(38f, 18f, 0f);

            _slab = new GameObject("Slab").transform; _slab.SetParent(_root, false);
            // carved blocks with seams and a little colour play; the slab is scaled in depth each frame to meet the band's top edge
            float x = -26f; int i = 0;
            while (x < 26f)
            {
                float w = 1.6f + 0.5f * Mathf.PerlinNoise(i * 0.7f, 0.3f);
                var tint = Color.Lerp(Stone, StoneDark, 0.35f * Mathf.PerlinNoise(i * 1.3f, 2.1f));
                Prim(PrimitiveType.Cube, _slab, new Vector3(x + w / 2f, -1f, 0.5f), new Vector3(w - 0.04f, 2f, 1f), tint);
                x += w; i++;
            }
            // the carved rim along the back edge and its lit lip
            var rim = Prim(PrimitiveType.Cube, _slab, new Vector3(0f, 0.09f, 0.985f), new Vector3(60f, 0.18f, 0.03f), StoneLight);
            rim.name = "Rim";
            var groove = Prim(PrimitiveType.Cube, _slab, new Vector3(0f, 0.004f, 0.94f), new Vector3(60f, 0.01f, 0.02f), StoneDark);
            groove.name = "Groove";

            _torches = new GameObject("Torches").transform; _torches.SetParent(_root, false);
            _flameL = Torch("L"); _flameR = Torch("R");
        }

        private Transform Torch(string side)
        {
            var t = new GameObject("Torch " + side).transform; t.SetParent(_torches, false);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.35f, 0f), new Vector3(0.09f, 0.35f, 0.09f), WoodDark);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.72f, 0f), new Vector3(0.16f, 0.05f, 0.16f), new Color(0.2f, 0.17f, 0.14f));
            var f = Glow(t, new Vector3(0f, 0.86f, 0f), new Vector3(0.2f, 0.32f, 0.2f), new Color(1f, 0.6f, 0.2f, 0.9f));
            Glow(t, new Vector3(0f, 0.83f, 0f), new Vector3(0.1f, 0.18f, 0.1f), new Color(1f, 0.92f, 0.6f, 1f));
            return f.transform.parent;
        }

        private Light MakeLight(string name, LightType type, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; if (range > 0f) l.range = range;
            l.cullingMask = 1 << Layer; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        private static GameObject Prim(PrimitiveType t, Transform parent, Vector3 lp, Vector3 ls, Color c, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(t);
            var col = go.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp; go.transform.localScale = ls;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFactory.Shared(c);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        private static GameObject Glow(Transform parent, Vector3 lp, Vector3 ls, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(go.GetComponent<Collider>());
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp; go.transform.localScale = ls;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFactory.CreateGlow(c);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        private static void Paint(GameObject go, Color c) { var r = go.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = MaterialFactory.Shared(c); }
        private static void SetGlow(GameObject go, Color c) { var r = go.GetComponent<Renderer>(); if (r != null) r.sharedMaterial.color = c; }

        private static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i).gameObject; c.SetActive(false); Destroy(c); } }

        /// <summary>A person, one unit tall, facing the camera: robe, head, arms, optional hat.</summary>
        private static Transform Person(Transform parent, Vector3 at, Color robe, Color hat, bool hatOn, float h = 1f)
        {
            var p = new GameObject("Person").transform; p.SetParent(parent, false); p.localPosition = at; p.localScale = Vector3.one * h;
            Prim(PrimitiveType.Capsule, p, new Vector3(0f, 0.32f, 0f), new Vector3(0.30f, 0.31f, 0.24f), robe);
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.08f, 0.28f), Color.Lerp(robe, Color.black, 0.25f));
            Prim(PrimitiveType.Sphere, p, new Vector3(0f, 0.72f, 0f), new Vector3(0.21f, 0.22f, 0.21f), Skin);
            Prim(PrimitiveType.Capsule, p, new Vector3(-0.17f, 0.42f, -0.02f), new Vector3(0.08f, 0.17f, 0.08f), robe, new Vector3(0f, 0f, -18f));
            Prim(PrimitiveType.Capsule, p, new Vector3(0.17f, 0.42f, -0.02f), new Vector3(0.08f, 0.17f, 0.08f), robe, new Vector3(0f, 0f, 18f));
            if (hatOn) Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.84f, 0f), new Vector3(0.24f, 0.04f, 0.24f), hat);
            return p;
        }

        /// <summary>One good as a small 3D piece about 0.15 units across: a log, a clay brick, a sheaf, a fleece or a stone.</summary>
        private static GameObject Good(int g, Transform parent, Vector3 at, float s = 1f, float yaw = 0f)
        {
            var c = GoodCol[g];
            switch (g)
            {
                case 0: { var go = Prim(PrimitiveType.Cylinder, parent, at, new Vector3(0.075f, 0.11f, 0.075f) * s, c, new Vector3(0f, yaw, 90f)); return go; }
                case 1: return Prim(PrimitiveType.Cube, parent, at, new Vector3(0.16f, 0.075f, 0.09f) * s, c, new Vector3(0f, yaw, 0f));
                case 2: return Prim(PrimitiveType.Capsule, parent, at + new Vector3(0f, 0.03f * s, 0f), new Vector3(0.085f, 0.1f, 0.085f) * s, c, new Vector3(0f, yaw, 8f));
                case 3: return Prim(PrimitiveType.Sphere, parent, at, new Vector3(0.16f, 0.11f, 0.14f) * s, c);
                default: return Prim(PrimitiveType.Sphere, parent, at, new Vector3(0.13f, 0.1f, 0.12f) * s, c, new Vector3(20f, yaw, 10f));
            }
        }

        private static readonly Vector3[] PileSpots =
        {
            new Vector3(-0.13f, 0f, -0.06f), new Vector3(0.13f, 0f, -0.06f), new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, -0.14f),
            new Vector3(-0.07f, 0.08f, 0f), new Vector3(0.08f, 0.08f, 0.02f), new Vector3(0f, 0.08f, -0.1f),
            new Vector3(-0.02f, 0.15f, -0.03f), new Vector3(0.06f, 0.16f, -0.06f),
        };

        private LedgeItem NewItem(string id)
        {
            var go = new GameObject("Ledge3D " + id);
            go.layer = Layer;
            go.transform.SetParent(_root, false);
            var it = go.AddComponent<LedgeItem>();
            it.Id = id; it.Born = Time.unscaledTime;
            var hit = new GameObject("Hit"); hit.layer = Layer; hit.transform.SetParent(go.transform, false);
            it.Hit = hit.AddComponent<BoxCollider>();
            it.Model = new GameObject("Model").transform; it.Model.SetParent(go.transform, false);
            it.Dyn = new GameObject("Dyn").transform; it.Dyn.SetParent(it.Model, false);
            BuildModel(it);
            _items[id] = it;
            return it;
        }

        private void BuildModel(LedgeItem it)
        {
            var m = it.Model; string id = it.Id;
            if (id.StartsWith("basket:"))
            {
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.2f, 0f), new Vector3(0.64f, 0.2f, 0.64f), Wicker);
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.11f, 0f), new Vector3(0.655f, 0.015f, 0.655f), WickerDark);
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.27f, 0f), new Vector3(0.655f, 0.015f, 0.655f), WickerDark);
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.405f, 0f), new Vector3(0.70f, 0.03f, 0.70f), WickerDark).name = "Rim";
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.41f, 0f), new Vector3(0.58f, 0.01f, 0.58f), new Color(0.16f, 0.11f, 0.06f));
                var ring = Glow(m, new Vector3(0f, 0.01f, 0f), new Vector3(0.95f, 0.01f, 0.95f), new Color(0, 0, 0, 0)); ring.name = "Ring";
                it.Dyn.localPosition = new Vector3(0f, 0.43f, 0f);
            }
            else if (id.StartsWith("bench:"))
            {
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.3f, 0f), new Vector3(0.98f, 0.07f, 0.55f), Wood);
                Prim(PrimitiveType.Cube, m, new Vector3(-0.38f, 0.14f, 0f), new Vector3(0.08f, 0.28f, 0.42f), WoodDark);
                Prim(PrimitiveType.Cube, m, new Vector3(0.38f, 0.14f, 0f), new Vector3(0.08f, 0.28f, 0.42f), WoodDark);
                var ring = Glow(m, new Vector3(0f, 0.01f, 0f), new Vector3(1.05f, 0.01f, 0.7f), new Color(0, 0, 0, 0)); ring.name = "Ring";
                it.Dyn.localPosition = new Vector3(0f, 0.335f, 0f);
            }
            else if (id.StartsWith("stake:"))
            {
                Prim(PrimitiveType.Cylinder, m, new Vector3(-0.12f, 0.02f, 0f), new Vector3(0.26f, 0.02f, 0.26f), StoneDark);
                var pole = new GameObject("Pole").transform; pole.SetParent(m, false);
                Prim(PrimitiveType.Cylinder, pole, new Vector3(-0.12f, 0.46f, 0f), new Vector3(0.045f, 0.46f, 0.045f), WoodDark);
                Prim(PrimitiveType.Cube, pole, new Vector3(0.05f, 0.80f, 0f), new Vector3(0.32f, 0.22f, 0.02f), Color.white).name = "Banner";
                Prim(PrimitiveType.Cube, pole, new Vector3(0.05f, 0.675f, 0f), new Vector3(0.32f, 0.035f, 0.025f), Color.gray).name = "Trim";
                Prim(PrimitiveType.Cylinder, pole, new Vector3(-0.12f, 0.6f, 0f), new Vector3(0.075f, 0.02f, 0.075f), Gold).name = "Cord";
                Prim(PrimitiveType.Cube, pole, new Vector3(-0.08f, 0.52f, -0.03f), new Vector3(0.015f, 0.15f, 0.015f), Gold, new Vector3(0f, 0f, 12f)).name = "CordEnd";
                Prim(PrimitiveType.Sphere, pole, new Vector3(-0.12f, 0.95f, 0f), new Vector3(0.17f, 0.12f, 0.17f), new Color(0.75f, 0.25f, 0.15f)).name = "Helmet";
                var ring = Glow(m, new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.01f, 0.7f), new Color(0, 0, 0, 0)); ring.name = "Ring";
                it.Dyn.localPosition = Vector3.zero;
            }
            else if (id == "trader")
            {
                Person(m, new Vector3(-0.1f, 0f, 0.05f), new Color(0.42f, 0.24f, 0.30f), new Color(0.62f, 0.18f, 0.12f), true);
                var roll = new GameObject("Roll").transform; roll.SetParent(m, false);
                Prim(PrimitiveType.Cylinder, roll, new Vector3(0.2f, 0.08f, -0.14f), new Vector3(0.13f, 0.26f, 0.13f), new Color(0.55f, 0.18f, 0.12f), new Vector3(0f, 20f, 90f));
                Prim(PrimitiveType.Cylinder, roll, new Vector3(0.2f, 0.08f, -0.14f), new Vector3(0.135f, 0.05f, 0.135f), new Color(0.85f, 0.62f, 0.28f), new Vector3(0f, 20f, 90f));
                var ring = Glow(m, new Vector3(0f, 0.01f, 0f), new Vector3(0.95f, 0.01f, 0.7f), new Color(0, 0, 0, 0)); ring.name = "Ring";
            }
            else if (id == "mat")
            {
                var cloth = Prim(PrimitiveType.Cube, m, new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.01f), new Color(0.30f, 0.12f, 0.08f)); cloth.name = "Cloth";
                for (int k = 0; k < 5; k++) Prim(PrimitiveType.Cube, cloth.transform, new Vector3(0f, -0.4f + k * 0.2f, -0.6f), new Vector3(1f, 0.02f, 0.5f), new Color(0.56f, 0.38f, 0.18f));
                Prim(PrimitiveType.Cube, cloth.transform, new Vector3(0f, 0.05f, -0.6f), new Vector3(1f, 0.012f, 0.5f), new Color(0.25f, 0.08f, 0.05f));
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.5f, 0f), new Vector3(0.04f, 0.53f, 0.04f), WoodDark, new Vector3(0f, 0f, 90f)).name = "RodTop";
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, -0.5f, 0f), new Vector3(0.05f, 0.53f, 0.05f), new Color(0.55f, 0.18f, 0.12f), new Vector3(0f, 0f, 90f)).name = "RodBottom";
            }
            else if (id == "mat-give" || id.StartsWith("mat-get:"))
            {
                var plate = Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.8f, 0.01f, 0.8f), new Color(0.62f, 0.44f, 0.26f), new Vector3(-90f, 0f, 0f)); plate.name = "Plate";
                if (id == "mat-give") plate.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
                var ring = Glow(m, new Vector3(0f, 0.5f, 0.04f), new Vector3(0.95f, 0.95f, 0.01f), new Color(0, 0, 0, 0)); ring.name = "Ring";
                it.Dyn.localPosition = new Vector3(0f, 0.5f, -0.06f);
            }
            else if (id == "mat-deal")
            {
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.5f, 0f), new Vector3(0.78f, 0.06f, 0.78f), StoneLight, new Vector3(-90f, 0f, 0f));
                // two clasped hands carved on the deal stone
                Prim(PrimitiveType.Capsule, m, new Vector3(-0.12f, 0.5f, -0.06f), new Vector3(0.14f, 0.2f, 0.1f), Skin, new Vector3(0f, 0f, 60f));
                Prim(PrimitiveType.Capsule, m, new Vector3(0.12f, 0.5f, -0.07f), new Vector3(0.14f, 0.2f, 0.1f), new Color(0.66f, 0.47f, 0.33f), new Vector3(0f, 0f, -60f));
                var seal = Glow(m, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.95f, 0.95f, 0.01f), new Color(0, 0, 0, 0)); seal.name = "Ring";
            }
            else if (id == "mat-peoples")
            {
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.5f, 0.06f), Wood);
                for (int p = 0; p < 4; p++)
                    Prim(PrimitiveType.Cylinder, m, new Vector3(-0.27f + p * 0.18f, 0.5f, -0.08f), new Vector3(0.12f, 0.06f, 0.12f), Color.white, new Vector3(-90f, 0f, 0f)).name = "Peg" + p;
            }
            else if (id.StartsWith("envoy:"))
            {
                var walker = new GameObject("Walker").transform; walker.SetParent(m, false);
                Person(walker, new Vector3(-0.45f, 0f, 0f), Color.white, Color.white, true).name = "Envoy";
                var bundle = new GameObject("Bundle").transform; bundle.SetParent(walker, false); bundle.localPosition = new Vector3(-0.2f, 0.44f, -0.14f);
                Prim(PrimitiveType.Cube, bundle, Vector3.zero, new Vector3(0.3f, 0.2f, 0.2f), Linen, new Vector3(0f, -10f, 5f));
                Prim(PrimitiveType.Cube, bundle, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.22f, 0.21f), new Color(0.55f, 0.35f, 0.18f), new Vector3(0f, -10f, 5f));
                Prim(PrimitiveType.Cylinder, m, new Vector3(0.42f, 0.06f, -0.05f), new Vector3(0.48f, 0.06f, 0.48f), Wood).name = "Bowl";
                it.Dyn.localPosition = Vector3.zero;
            }
            else if (id.StartsWith("wave:"))
            {
                Prim(PrimitiveType.Cylinder, m, new Vector3(-0.27f, 0.28f, 0f), new Vector3(0.06f, 0.28f, 0.06f), WoodDark);
                Prim(PrimitiveType.Cylinder, m, new Vector3(0.27f, 0.28f, 0f), new Vector3(0.06f, 0.28f, 0.06f), WoodDark);
                Prim(PrimitiveType.Cube, m, new Vector3(-0.13f, 0.45f, 0f), new Vector3(0.3f, 0.035f, 0.035f), new Color(0.75f, 0.2f, 0.14f), new Vector3(0f, 0f, -14f));
                Prim(PrimitiveType.Cube, m, new Vector3(0.13f, 0.45f, 0f), new Vector3(0.3f, 0.035f, 0.035f), new Color(0.75f, 0.2f, 0.14f), new Vector3(0f, 0f, 14f));
                // a raised palm: the gesture of sending the envoy away
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.7f, 0f), new Vector3(0.2f, 0.24f, 0.05f), Skin);
                for (int f = 0; f < 4; f++) Prim(PrimitiveType.Capsule, m, new Vector3(-0.075f + f * 0.05f, 0.88f, 0f), new Vector3(0.04f, 0.07f, 0.04f), Skin);
            }
            else if (id == "sack")
            {
                var body = new GameObject("Body").transform; body.SetParent(m, false);
                Prim(PrimitiveType.Sphere, body, new Vector3(-0.08f, 0.27f, 0f), new Vector3(0.52f, 0.5f, 0.46f), new Color(0.66f, 0.52f, 0.32f));
                Prim(PrimitiveType.Cylinder, body, new Vector3(-0.08f, 0.56f, 0f), new Vector3(0.2f, 0.07f, 0.2f), new Color(0.60f, 0.46f, 0.27f));
                Prim(PrimitiveType.Cylinder, body, new Vector3(-0.08f, 0.6f, 0f), new Vector3(0.22f, 0.02f, 0.22f), WickerDark);
                var mini = Hourglass(m, new Vector3(0.36f, 0f, -0.02f), 0.42f); mini.name = "Mini";
                it.Dyn.localPosition = Vector3.zero;
            }
            else if (id == "sack-clear")
            {
                Prim(PrimitiveType.Sphere, m, new Vector3(0f, 0.02f, 0f), new Vector3(0.62f, 0.4f, 0.62f), Wood);
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.22f, 0f), new Vector3(0.12f, 0.02f, 0.12f), WoodDark);
                Good(1, m, new Vector3(0.33f, 0.04f, -0.12f), 0.9f, 30f);
                Good(0, m, new Vector3(-0.32f, 0.04f, -0.14f), 0.9f, -20f);
            }
            else if (id == "hourglass")
            {
                Hourglass(m, Vector3.zero, 1f).name = "Glass";
                var bell = new GameObject("Bell").transform; bell.SetParent(m, false); bell.localPosition = new Vector3(0.36f, 0f, -0.05f);
                Prim(PrimitiveType.Cylinder, bell, new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.08f, 0.14f), new Color(0.72f, 0.52f, 0.22f));
                Prim(PrimitiveType.Sphere, bell, new Vector3(0f, 0.16f, 0f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.72f, 0.52f, 0.22f));
                Prim(PrimitiveType.Cylinder, bell, new Vector3(0f, 0.23f, 0f), new Vector3(0.03f, 0.03f, 0.03f), WoodDark);
            }
            else if (id == "chronicler")
            {
                Person(m, new Vector3(-0.08f, -0.08f, 0.08f), new Color(0.34f, 0.36f, 0.30f), new Color(0.3f, 0.25f, 0.2f), false, 0.95f);
                Prim(PrimitiveType.Cube, m, new Vector3(0.05f, 0.012f, -0.2f), new Vector3(0.42f, 0.012f, 0.22f), new Color(0.93f, 0.88f, 0.74f), new Vector3(0f, -12f, 0f));
                Prim(PrimitiveType.Cylinder, m, new Vector3(0.05f, 0.42f, -0.17f), new Vector3(0.07f, 0.17f, 0.07f), new Color(0.93f, 0.88f, 0.74f), new Vector3(0f, 0f, 90f));
                Prim(PrimitiveType.Cylinder, m, new Vector3(0.32f, 0.07f, -0.1f), new Vector3(0.05f, 0.07f, 0.05f), new Color(0.9f, 0.86f, 0.7f)).name = "Candle";
                Glow(m, new Vector3(0.32f, 0.18f, -0.1f), new Vector3(0.06f, 0.1f, 0.06f), new Color(1f, 0.7f, 0.3f, 1f)).name = "Flame";
            }
            else if (id == "prince")
            {
                Person(m, Vector3.zero, new Color(0.20f, 0.26f, 0.52f), Gold, false);
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.86f, 0f), new Vector3(0.2f, 0.045f, 0.2f), Gold).name = "Crown";
                for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cube, m, new Vector3(Mathf.Cos(k * 1.57f) * 0.08f, 0.92f, Mathf.Sin(k * 1.57f) * 0.08f), new Vector3(0.035f, 0.06f, 0.035f), Gold).name = "Crown";
            }
            else if (id == "totem")
            {
                Prim(PrimitiveType.Cylinder, m, new Vector3(0f, 0.05f, 0f), new Vector3(0.3f, 0.05f, 0.3f), StoneDark);
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.42f, 0f), new Vector3(0.2f, 0.7f, 0.18f), Wood).name = "Pole";
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.62f, -0.1f), new Vector3(0.3f, 0.24f, 0.04f), new Color(0.88f, 0.84f, 0.72f)).name = "Mask";
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.28f, -0.1f), new Vector3(0.26f, 0.05f, 0.04f), new Color(0.6f, 0.2f, 0.14f));
                Glow(m, new Vector3(-0.065f, 0.65f, -0.13f), new Vector3(0.06f, 0.06f, 0.02f), new Color(1f, 0.3f, 0.2f)).name = "Eye";
                Glow(m, new Vector3(0.065f, 0.65f, -0.13f), new Vector3(0.06f, 0.06f, 0.02f), new Color(1f, 0.3f, 0.2f)).name = "Eye";
                Prim(PrimitiveType.Cube, m, new Vector3(-0.08f, 0.85f, 0f), new Vector3(0.04f, 0.2f, 0.02f), new Color(0.8f, 0.3f, 0.2f), new Vector3(0f, 0f, 20f));
                Prim(PrimitiveType.Cube, m, new Vector3(0.08f, 0.85f, 0f), new Vector3(0.04f, 0.2f, 0.02f), new Color(0.9f, 0.75f, 0.3f), new Vector3(0f, 0f, -20f));
            }
            else if (id == "lamb")
            {
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.13f, 0f), new Vector3(0.78f, 0.26f, 0.5f), StoneLight);
                Prim(PrimitiveType.Cube, m, new Vector3(0f, 0.27f, 0f), new Vector3(0.84f, 0.03f, 0.56f), Stone);
                var lamb = new GameObject("Lamb").transform; lamb.SetParent(m, false); lamb.localPosition = new Vector3(0f, 0.285f, 0f);
                var wool = new Color(0.96f, 0.95f, 0.9f);
                Prim(PrimitiveType.Sphere, lamb, new Vector3(0.02f, 0.17f, 0f), new Vector3(0.36f, 0.22f, 0.22f), wool);
                Prim(PrimitiveType.Sphere, lamb, new Vector3(-0.2f, 0.25f, -0.02f), new Vector3(0.13f, 0.13f, 0.12f), new Color(0.3f, 0.26f, 0.22f));
                Prim(PrimitiveType.Sphere, lamb, new Vector3(-0.17f, 0.3f, -0.01f), new Vector3(0.12f, 0.08f, 0.12f), wool);
                foreach (var lx in new[] { -0.1f, 0.12f }) foreach (var lz in new[] { -0.06f, 0.06f })
                        Prim(PrimitiveType.Cube, lamb, new Vector3(lx, 0.04f, lz), new Vector3(0.04f, 0.09f, 0.04f), new Color(0.3f, 0.26f, 0.22f));
            }
        }

        /// <summary>A real hourglass, one unit tall at scale 1: wooden caps and posts, two glass bulbs, sand that drains.</summary>
        private static Transform Hourglass(Transform parent, Vector3 at, float s)
        {
            var root = new GameObject("Hourglass").transform; root.SetParent(parent, false); root.localPosition = at; root.localScale = Vector3.one * s;
            var pivot = new GameObject("Pivot").transform; pivot.SetParent(root, false); pivot.localPosition = new Vector3(0f, 0.5f, 0f);
            Prim(PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.47f, 0f), new Vector3(0.56f, 0.03f, 0.56f), Wood);
            Prim(PrimitiveType.Cylinder, pivot, new Vector3(0f, 0.47f, 0f), new Vector3(0.56f, 0.03f, 0.56f), Wood);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.094f + 0.5f;
                Prim(PrimitiveType.Cylinder, pivot, new Vector3(Mathf.Cos(a) * 0.23f, 0f, Mathf.Sin(a) * 0.23f), new Vector3(0.04f, 0.46f, 0.04f), WoodDark);
            }
            Glow(pivot, new Vector3(0f, 0.22f, 0f), new Vector3(0.36f, 0.42f, 0.36f), new Color(0.55f, 0.7f, 0.85f, 0.22f)).name = "BulbTop";
            Glow(pivot, new Vector3(0f, -0.22f, 0f), new Vector3(0.36f, 0.42f, 0.36f), new Color(0.55f, 0.7f, 0.85f, 0.22f)).name = "BulbBottom";
            Prim(PrimitiveType.Sphere, pivot, new Vector3(0f, 0.14f, 0f), new Vector3(0.28f, 0.2f, 0.28f), new Color(0.98f, 0.82f, 0.38f)).name = "SandTop";
            Prim(PrimitiveType.Sphere, pivot, new Vector3(0f, -0.36f, 0f), new Vector3(0.3f, 0.12f, 0.3f), new Color(0.98f, 0.82f, 0.38f)).name = "SandBottom";
            Prim(PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.12f, 0f), new Vector3(0.02f, 0.12f, 0.02f), new Color(1f, 0.88f, 0.45f)).name = "Stream";
            return root;
        }

        /// <summary>Sets an hourglass's sand: <paramref name="top"/> is the share still above (1 full, 0 run out).</summary>
        private static void SetSand(Transform glass, float top, bool running, Color sand)
        {
            var pivot = glass.Find("Pivot");
            var st = pivot.Find("SandTop"); var sb = pivot.Find("SandBottom"); var stream = pivot.Find("Stream");
            top = Mathf.Clamp01(top);
            st.gameObject.SetActive(top > 0.02f);
            st.localScale = new Vector3(0.28f * Mathf.Sqrt(Mathf.Max(0.15f, top)), 0.3f * top, 0.28f * Mathf.Sqrt(Mathf.Max(0.15f, top)));
            st.localPosition = new Vector3(0f, 0.05f + 0.15f * top * 0.5f + 0.02f, 0f);
            float b = 1f - top;
            sb.gameObject.SetActive(b > 0.02f);
            sb.localScale = new Vector3(0.32f, 0.04f + 0.26f * b, 0.32f);
            sb.localPosition = new Vector3(0f, -0.43f + (0.04f + 0.26f * b) * 0.5f, 0f);
            stream.gameObject.SetActive(running && top > 0.02f);
            Paint(st.gameObject, sand); Paint(sb.gameObject, sand);
        }

        // ---------------------------------------------------------------- placing

        private float GuiToScreenY(float gy) => Screen.height - gy;

        private Vector3 OnSlab(float gx, float gy)
        {
            var ray = Cam.ScreenPointToRay(new Vector3(gx, GuiToScreenY(gy), 0f));
            float py = Origin.y + SlabY;
            float dy = Mathf.Min(ray.direction.y, -0.02f);
            float t = (py - ray.origin.y) / dy;
            return ray.origin + ray.direction * t;
        }

        private float DepthOf(Vector3 w) => Cam.transform.InverseTransformPoint(w).z;
        private Vector3 At(float gx, float gy, float depth) => Cam.ScreenToWorldPoint(new Vector3(gx, GuiToScreenY(gy), depth));

        /// <summary>Stands the item inside its rect: the hit box covers the rect exactly (camera facing), the model stands on the slab.</summary>
        private void Place(LedgeItem it, Rect r, float modelScale = 0.92f, bool onSlab = true, float depthOverride = -1f)
        {
            Vector3 foot = onSlab ? OnSlab(r.center.x, r.yMax - r.height * 0.04f) : Vector3.zero;
            float z = depthOverride > 0f ? depthOverride : DepthOf(foot);
            var c = At(r.center.x, r.center.y, z);
            float w = (At(r.xMax, r.center.y, z) - At(r.xMin, r.center.y, z)).magnitude;
            float h = (At(r.center.x, r.yMin, z) - At(r.center.x, r.yMax, z)).magnitude;
            it.transform.position = onSlab ? foot : At(r.center.x, r.yMax, z);
            it.transform.rotation = Quaternion.identity;
            it.Hit.transform.position = c;
            it.Hit.transform.rotation = Cam.transform.rotation;
            it.Hit.size = new Vector3(w, h, 0.05f);
            it.Hit.center = Vector3.zero;
            it.Model.localScale = Vector3.one * h * modelScale * (onSlab ? ModelBoost : 1f);
            if (!onSlab) it.Model.rotation = Cam.transform.rotation;
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>Shows or hides the ledge and makes every object match the layout and the view model.</summary>
        public void Sync(bool show)
        {
            Shown = show;
            Cam.enabled = show;
            _slab.gameObject.SetActive(show); _torches.gameObject.SetActive(show);
            _torchL.enabled = _torchR.enabled = _fill.enabled = show;
            if (!show) { foreach (var it in _items.Values) if (it.gameObject.activeSelf) it.gameObject.SetActive(false); return; }
            Cam.fieldOfView = Fov;
            var band = _hud.LedgeBand;
            var vm = _d.Model;
            float now = Time.unscaledTime;

            // the slab: from below the screen's bottom edge to the band's top edge
            var far = OnSlab(Screen.width * 0.5f, band.y);
            var near = OnSlab(Screen.width * 0.5f, Screen.height + 4f);
            float zf = far.z, zn = near.z - 1.5f;
            _slab.position = new Vector3(Origin.x, Origin.y + SlabY, zn);
            _slab.localScale = new Vector3(1f, 1f, Mathf.Max(0.5f, zf - zn));
            // the torches stand on the rim at both screen edges, their light falls across the ledge
            var tl = OnSlab(Screen.width * 0.035f, band.y); var tr = OnSlab(Screen.width * 0.965f, band.y);
            float tscale = (At(0f, band.y - band.height * 0.75f, DepthOf(tl)) - At(0f, band.y, DepthOf(tl))).magnitude;
            _flameL.position = tl + Vector3.forward * 0.05f; _flameR.position = tr + Vector3.forward * 0.05f;
            _flameL.localScale = _flameR.localScale = Vector3.one * tscale;
            float fl = 0.85f + 0.3f * Mathf.PerlinNoise(now * 3.1f, 0.2f), fr = 0.85f + 0.3f * Mathf.PerlinNoise(now * 2.7f, 5.3f);
            _torchL.transform.position = tl + new Vector3(0.6f, tscale * 1.2f, -1.2f); _torchR.transform.position = tr + new Vector3(-0.6f, tscale * 1.2f, -1.2f);
            float span = Mathf.Abs(tr.x - tl.x);
            _torchL.range = _torchR.range = Mathf.Max(6f, span * 0.85f);
            _torchL.intensity = 2.2f * fl; _torchR.intensity = 2.2f * fr;

            var seen = new HashSet<string>();
            foreach (var kv in _hud.LedgeRects)
            {
                string id = kv.Key;
                if (!_items.TryGetValue(id, out var it) || it == null) it = NewItem(id);
                if (!it.gameObject.activeSelf) { it.gameObject.SetActive(true); it.Born = now; }
                seen.Add(id);
                Refresh(it, kv.Value, vm, now);
            }
            // the mat's cloth (not a tap target itself)
            if (_hud.MatOpen)
            {
                if (_matOpenedAt < 0f) _matOpenedAt = now;
                if (!_items.TryGetValue("mat", out var mat)) mat = NewItem("mat");
                mat.Hit.enabled = false;
                if (!mat.gameObject.activeSelf) mat.gameObject.SetActive(true);
                seen.Add("mat");
                var mr = _hud.MatRect;
                float z = DepthOf(far) - 0.3f;
                Place(mat, mr, 1f, false, z);
                float hpx = (At(mr.center.x, mr.yMin, z) - At(mr.center.x, mr.yMax, z)).magnitude;
                float wpx = (At(mr.xMax, mr.center.y, z) - At(mr.xMin, mr.center.y, z)).magnitude;
                float u = Mathf.Clamp01((now - _matOpenedAt) / 0.35f);
                u = u * u * (3f - 2f * u);
                mat.transform.position = At(mr.center.x, mr.y, z);
                mat.Model.localScale = Vector3.one;
                var cloth = mat.Model.Find("Cloth");
                cloth.localScale = new Vector3(wpx, hpx * Mathf.Max(0.02f, u), 0.01f);
                cloth.localPosition = new Vector3(0f, -hpx * u * 0.5f, 0.05f);
                var top = mat.Model.Find("RodTop"); top.localScale = new Vector3(0.05f * hpx, wpx * 0.53f, 0.05f * hpx); top.localPosition = Vector3.zero;
                var bot = mat.Model.Find("RodBottom"); bot.localScale = new Vector3(0.09f * hpx, wpx * 0.52f, 0.09f * hpx); bot.localPosition = new Vector3(0f, -hpx * u, 0f);
            }
            else _matOpenedAt = -1f;

            var drop = new List<string>();
            foreach (var kv in _items)
            {
                if (seen.Contains(kv.Key)) continue;
                if (kv.Value.gameObject.activeSelf) kv.Value.gameObject.SetActive(false);
                if (kv.Key.StartsWith("envoy:") || kv.Key.StartsWith("wave:")) drop.Add(kv.Key);
            }
            foreach (var k in drop) { Destroy(_items[k].gameObject); _items.Remove(k); }
            for (int g = 0; g < Wares.RawCount; g++) if (!_hud.LedgeRects.ContainsKey("basket:" + g)) _shown[g] = 0;
        }

        private void Refresh(LedgeItem it, Rect r, GodBoardViewModel vm, float now)
        {
            string id = it.Id;
            if (id.StartsWith("basket:"))
            {
                Place(it, r, 0.95f);
                int g = int.Parse(id.Substring(7));
                int n = vm.Hand[g];
                if (_hud.MatOpen && _hud.BankGive == g) n -= Mathf.Min(n, vm.BankRate[g]);
                n -= Mathf.Min(n, _hud.DiscardPick[g]);
                int show = Mathf.Clamp(n, 0, BasketCap);
                if (it.Sig != show.ToString())
                {
                    it.Sig = show.ToString();
                    Clear(it.Dyn);
                    for (int i = 0; i < show; i++) Good(g, it.Dyn, PileSpots[i] * 1.2f, 1.6f, i * 47f);
                }
                _shown[g] = show;
                bool lacks = _hud.Lacks(g);
                bool picked = (_hud.MatOpen && _hud.BankGive == g) || _hud.DiscardPick[g] > 0;
                float pulse = 0.6f + 0.4f * Mathf.Sin(now * 6f);
                SetGlow(it.Model.Find("Ring").gameObject, lacks ? new Color(1f, 0.25f, 0.15f, 0.8f * pulse) : picked ? new Color(1f, 0.8f, 0.3f, 0.7f) : new Color(0, 0, 0, 0));
                return;
            }
            if (id.StartsWith("bench:"))
            {
                Place(it, r, 0.95f);
                int k = int.Parse(id.Substring(6));
                var kv = vm.Kind[k];
                var col = _hud.PeopleColour(vm.Human);
                bool ok = kv.Affordable;
                string sig = kv.Left + "/" + ok;
                if (it.Sig != sig)
                {
                    it.Sig = sig;
                    Clear(it.Dyn);
                    var pc = ok ? col : Color.Lerp(col, new Color(0.15f, 0.13f, 0.12f), 0.6f);
                    var roof = ok ? new Color(0.72f, 0.58f, 0.30f) : new Color(0.32f, 0.28f, 0.22f);
                    if (k == 0) Prim(PrimitiveType.Cube, it.Dyn, new Vector3(-0.12f, 0.035f, -0.06f), new Vector3(0.52f, 0.06f, 0.13f), pc, new Vector3(0f, 12f, 0f));
                    else if (k == 1) Hut(it.Dyn, new Vector3(-0.12f, 0f, -0.04f), 1f, pc, roof);
                    else { Hut(it.Dyn, new Vector3(-0.16f, 0f, -0.02f), 1.15f, pc, roof); Prim(PrimitiveType.Cube, it.Dyn, new Vector3(0.04f, 0.2f, 0.06f), new Vector3(0.12f, 0.4f, 0.12f), pc); Prim(PrimitiveType.Cube, it.Dyn, new Vector3(-0.14f, 0.04f, -0.2f), new Vector3(0.5f, 0.08f, 0.05f), new Color(0.5f, 0.46f, 0.4f)); }
                    // the spare pieces stacked at the bench's end
                    int left = Mathf.Min(kv.Left, kv.Max);
                    for (int i = 0; i < left; i++)
                    {
                        if (k == 0) Prim(PrimitiveType.Cube, it.Dyn, new Vector3(0.33f, 0.016f + i * 0.026f, 0.05f), new Vector3(0.2f, 0.022f, 0.07f), Color.Lerp(col, Color.black, 0.2f));
                        else if (k == 1) Prim(PrimitiveType.Cube, it.Dyn, new Vector3(0.3f + (i % 2) * 0.1f, 0.04f + (i / 2) * 0.085f, 0.08f), new Vector3(0.08f, 0.08f, 0.08f), Color.Lerp(col, Color.black, 0.2f));
                        else Prim(PrimitiveType.Cube, it.Dyn, new Vector3(0.28f + (i % 2) * 0.11f, 0.06f + (i / 2) * 0.125f, 0.08f), new Vector3(0.09f, 0.12f, 0.09f), Color.Lerp(col, Color.black, 0.2f));
                    }
                }
                float shake = _hud.ShakeOffset(kv.Kind);
                it.Model.localPosition = new Vector3(shake * it.Model.localScale.x, 0f, 0f);
                bool active = _d.BuildMode == kv.Kind;
                SetGlow(it.Model.Find("Ring").gameObject, active ? new Color(1f, 0.8f, 0.3f, 0.75f) : ok ? new Color(1f, 0.75f, 0.35f, 0.18f) : new Color(0, 0, 0, 0));
                return;
            }
            if (id.StartsWith("stake:"))
            {
                Place(it, r, 0.95f);
                int p = int.Parse(id.Substring(6));
                bool current = vm.Current == p && !vm.Paused && vm.Phase != TablePhase.NotOpened && vm.Phase != TablePhase.Ended;
                var pole = it.Model.Find("Pole");
                float lift = current ? 0.1f + 0.015f * Mathf.Sin(now * 3f) : 0f;
                pole.localPosition = Vector3.Lerp(pole.localPosition, new Vector3(0f, lift, 0f), 0.2f);
                var col = _hud.PeopleColour(p);
                var banner = pole.Find("Banner"); Paint(banner.gameObject, col);
                banner.localRotation = Quaternion.Euler(0f, current ? 8f * Mathf.Sin(now * 2.3f) : 0f, 0f);
                Paint(pole.Find("Trim").gameObject, Color.Lerp(col, Color.black, 0.4f));
                bool longest = vm.LongestHolder == p;
                pole.Find("Cord").gameObject.SetActive(longest); pole.Find("CordEnd").gameObject.SetActive(longest);
                bool war = false; foreach (var pr in vm.WarPairs) if (pr[0] == p || pr[1] == p) war = true;
                pole.Find("Helmet").gameObject.SetActive(war);
                int pts = Mathf.Clamp(p < vm.Points.Length ? vm.Points[p] : 0, 0, 12);
                if (it.Sig != pts.ToString())
                {
                    it.Sig = pts.ToString(); Clear(it.Dyn);
                    for (int i = 0; i < pts; i++)
                        Prim(PrimitiveType.Sphere, it.Dyn, new Vector3(0.16f + (i % 2) * 0.1f + ((i / 2) % 2) * 0.02f, 0.045f + (i / 2) * 0.075f, -0.05f), new Vector3(0.095f, 0.08f, 0.09f), new Color(0.90f, 0.87f, 0.78f));
                }
                SetGlow(it.Model.Find("Ring").gameObject, current ? new Color(1f, 0.82f, 0.35f, 0.55f) : new Color(0, 0, 0, 0));
                return;
            }
            switch (id)
            {
                case "trader":
                    Place(it, r, 0.92f);
                    it.Model.Find("Roll").gameObject.SetActive(!_hud.MatOpen);
                    SetGlow(it.Model.Find("Ring").gameObject, _hud.MatOpen ? new Color(1f, 0.8f, 0.3f, 0.6f) : new Color(0, 0, 0, 0));
                    return;
                case "hourglass":
                {
                    Place(it, r, 0.9f);
                    bool main = vm.CanAct;
                    var glass = it.Model.Find("Glass");
                    var pivot = glass.Find("Pivot");
                    float target = _hud.HourglassTipped ? 90f : 0f;
                    float cur = pivot.localEulerAngles.z; if (cur > 180f) cur -= 360f;
                    pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(cur, target, 0.2f));
                    SetSand(glass, main ? vm.SandFraction : 0f, main && !_hud.HourglassTipped, main ? (vm.SandFraction > 0.3f ? new Color(0.98f, 0.82f, 0.38f) : new Color(0.95f, 0.45f, 0.25f)) : new Color(0.55f, 0.5f, 0.4f));
                    var bell = it.Model.Find("Bell");
                    bell.localPosition = Vector3.Lerp(bell.localPosition, new Vector3(0.36f, _hud.HourglassTipped ? 0.32f : 0f, -0.05f), 0.2f);
                    return;
                }
                case "sack":
                {
                    Place(it, r, 0.95f);
                    int owed = Mathf.Min(vm.DiscardOwed, 12), chosen = Mathf.Min(_hud.DiscardChosen, owed);
                    float f = owed > 0 ? chosen / (float)owed : 0f;
                    it.Model.Find("Body").localScale = Vector3.one * (0.8f + 0.25f * f);
                    string sig = owed + "/" + chosen + "/" + string.Join(",", _hud.DiscardPick);
                    if (it.Sig != sig)
                    {
                        it.Sig = sig; Clear(it.Dyn);
                        // the knots on the sack's cord: one per good owed; tied (gold) as goods go in
                        float span = Mathf.Min(0.07f, 0.6f / Mathf.Max(1, owed));
                        for (int i = 0; i < owed; i++)
                            Prim(PrimitiveType.Sphere, it.Dyn, new Vector3(-0.08f - (owed - 1) * span * 0.5f + i * span, 0.5f, -0.2f), Vector3.one * 0.05f, i < chosen ? Gold : new Color(0.2f, 0.13f, 0.07f));
                        Prim(PrimitiveType.Cube, it.Dyn, new Vector3(-0.08f, 0.5f, -0.19f), new Vector3(Mathf.Max(0.1f, owed * span + 0.04f), 0.012f, 0.012f), WickerDark);
                        // the goods already in the sack peek from its mouth
                        int shown = 0;
                        for (int g = 0; g < Wares.RawCount && shown < 5; g++)
                            for (int n = 0; n < _hud.DiscardPick[g] && shown < 5; n++, shown++)
                                Good(g, it.Dyn, new Vector3(-0.16f + shown * 0.045f, 0.66f + (shown % 2) * 0.03f, 0f), 0.85f, shown * 40f);
                    }
                    float frac = Mathf.Clamp01(vm.DiscardTicksLeft / (float)Mathf.Max(1, vm.DiscardTicksMax));
                    SetSand(it.Model.Find("Mini"), frac, true, frac > 0.3f ? new Color(0.98f, 0.82f, 0.38f) : new Color(0.95f, 0.4f, 0.25f));
                    return;
                }
                case "sack-clear": Place(it, r, 0.85f); return;
                case "chronicler":
                    Place(it, r, 0.95f);
                    it.Model.Find("Flame").gameObject.SetActive(_hud.UnreadFeed > 0);
                    return;
                case "prince":
                {
                    Place(it, r, 0.92f);
                    var c = GodBoardDirector.AdviserOn ? Gold : new Color(0.42f, 0.38f, 0.30f);
                    foreach (Transform t in it.Model) if (t.name == "Crown") Paint(t.gameObject, c);
                    return;
                }
                case "totem":
                {
                    Place(it, r, 0.95f);
                    bool on = GodBoardHud.PlagueIsOn != null && GodBoardHud.PlagueIsOn();
                    bool can = GodBoardHud.PlagueToggle != null;
                    foreach (Transform t in it.Model) if (t.name == "Eye") SetGlow(t.gameObject, on ? new Color(1f, 0.3f, 0.2f, 1f) : new Color(0.25f, 0.08f, 0.05f, 0.4f));
                    Paint(it.Model.Find("Pole").gameObject, can ? Wood : new Color(0.25f, 0.23f, 0.21f));
                    return;
                }
                case "lamb": Place(it, r, 0.92f); return;
                case "mat-give":
                case "mat-deal":
                case "mat-peoples":
                    RefreshMatPart(it, r, vm, now); return;
            }
            if (id.StartsWith("mat-get:")) { RefreshMatPart(it, r, vm, now); return; }
            if (id.StartsWith("envoy:"))
            {
                Place(it, r, 0.95f);
                int oid = int.Parse(id.Substring(6));
                TradeOffer o = null; foreach (var x in vm.Incoming) if (x.Id == oid) o = x;
                if (o == null) return;
                var walker = it.Model.Find("Walker");
                // walks in from the screen's side, then waits; turns to go as patience runs out
                float w = Mathf.Clamp01((now - it.Born) / 0.9f);
                float frac = Mathf.Clamp01(o.TicksLeft / (float)Mathf.Max(1, vm.OfferTicks));
                walker.localPosition = new Vector3((1f - w) * 3.5f, w < 1f ? Mathf.Abs(Mathf.Sin(now * 12f)) * 0.04f : 0f, 0f);
                walker.localRotation = Quaternion.Euler(0f, w < 1f ? -70f * (1f - w) : (1f - frac) * 110f, 0f);
                string sig = o.From + "|" + string.Join(",", o.Give) + "|" + string.Join(",", o.Get);
                if (it.Sig != sig)
                {
                    it.Sig = sig; Clear(it.Dyn);
                    var col = _hud.PeopleColour(o.From);
                    var env = walker.Find("Envoy");
                    foreach (Transform t in env) if (t.name.StartsWith("Capsule") || t.name.StartsWith("Cylinder")) Paint(t.gameObject, t.name.StartsWith("Cylinder") && t.localPosition.y > 0.5f ? Color.Lerp(col, Color.white, 0.3f) : col);
                    // the bundle held out shows what the envoy gives; the bowl at the feet shows what it asks for
                    var bundle = walker.Find("Bundle");
                    int s = 0;
                    for (int g = 0; g < Wares.RawCount && s < 3; g++) if (o.Give[g] > 0) { Good(g, bundle, new Vector3(-0.08f + s * 0.08f, 0.13f, 0f), 0.85f, s * 30f); s++; }
                    s = 0;
                    for (int g = 0; g < Wares.RawCount && s < 3; g++) if (o.Get[g] > 0) { Good(g, it.Dyn, new Vector3(0.36f + s * 0.07f, 0.15f, -0.05f), 0.9f, s * 50f); s++; }
                }
                Paint(it.Model.Find("Bowl").gameObject, TableRules.CanPay(vm.Hand, o.Get) ? Wood : new Color(0.2f, 0.17f, 0.14f));
                return;
            }
            if (id.StartsWith("wave:")) { Place(it, r, 0.9f); return; }
            Place(it, r, 0.9f);
        }

        private static void Hut(Transform parent, Vector3 at, float s, Color wall, Color roof)
        {
            Prim(PrimitiveType.Cube, parent, at + new Vector3(0f, 0.11f * s, 0f), new Vector3(0.3f, 0.22f, 0.26f) * s, wall);
            Prim(PrimitiveType.Cube, parent, at + new Vector3(0f, 0.24f * s, 0f), new Vector3(0.24f, 0.24f, 0.3f) * s, roof, new Vector3(0f, 0f, 45f));
            Prim(PrimitiveType.Cube, parent, at + new Vector3(0f, 0.07f * s, -0.131f * s), new Vector3(0.07f, 0.13f, 0.01f) * s, new Color(0.12f, 0.09f, 0.07f));
        }

        private void RefreshMatPart(LedgeItem it, Rect r, GodBoardViewModel vm, float now)
        {
            var mr = _hud.MatRect;
            var far = OnSlab(Screen.width * 0.5f, _hud.LedgeBand.y);
            float z = DepthOf(far) - 0.45f;
            Place(it, r, 0.9f, false, z);
            it.transform.position = At(r.center.x, r.yMax, z);
            it.Model.rotation = Cam.transform.rotation;
            float u = _matOpenedAt < 0f ? 0f : Mathf.Clamp01((now - _matOpenedAt - 0.2f) / 0.25f);
            it.Model.gameObject.SetActive(u > 0f);
            string id = it.Id;
            if (id == "mat-give")
            {
                float ar = r.width / Mathf.Max(1f, r.height);
                it.Model.Find("Plate").localScale = new Vector3(0.85f * ar, 0.01f, 0.85f);
                it.Model.Find("Ring").localScale = new Vector3(0.98f * ar, 0.95f, 0.01f);
                int g = _hud.BankGive;
                int n = g >= 0 ? Mathf.Min(vm.BankRate[g], 6) : 0;
                string sig = g + "/" + n;
                if (it.Sig != sig)
                {
                    it.Sig = sig; Clear(it.Dyn);
                    for (int i = 0; i < n; i++) Good(g, it.Dyn, new Vector3((i - (n - 1) * 0.5f) * 0.42f, 0f, 0f), 2.6f, i * 35f);
                }
                SetGlow(it.Model.Find("Ring").gameObject, g >= 0 ? new Color(1f, 0.8f, 0.3f, 0.35f) : new Color(0, 0, 0, 0));
            }
            else if (id.StartsWith("mat-get:"))
            {
                int g = int.Parse(id.Substring(8));
                if (it.Sig != "x") { it.Sig = "x"; Clear(it.Dyn); Good(g, it.Dyn, Vector3.zero, 3.4f, 25f); }
                bool sel = _hud.BankGet == g;
                it.Dyn.localPosition = new Vector3(0f, 0.5f + (sel ? 0.08f : 0f), -0.06f);
                SetGlow(it.Model.Find("Ring").gameObject, sel ? new Color(1f, 0.8f, 0.3f, 0.75f) : new Color(0, 0, 0, 0));
            }
            else if (id == "mat-deal")
            {
                bool can = vm.CanAct && _hud.BankGive >= 0 && _hud.BankGet >= 0 && _hud.BankGive != _hud.BankGet && vm.Hand[_hud.BankGive] >= vm.BankRate[_hud.BankGive];
                SetGlow(it.Model.Find("Ring").gameObject, can ? new Color(0.6f, 1f, 0.5f, 0.35f + 0.25f * Mathf.Sin(now * 5f)) : new Color(0, 0, 0, 0));
            }
            else if (id == "mat-peoples")
            {
                for (int p = 0; p < 4; p++) { var peg = it.Model.Find("Peg" + p); if (peg != null) Paint(peg.gameObject, p < vm.PeopleCount ? _hud.PeopleColour(p) : Color.gray); }
            }
        }

        // ---------------------------------------------------------------- picking

        /// <summary>The ledge object under a screen point (pixels, bottom-left origin), by a 3D raycast; null if none.</summary>
        public string Pick(Vector2 screen)
        {
            if (!Shown || Cam == null) return null;
            Physics.SyncTransforms();
            var ray = Cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (!Physics.Raycast(ray, out var hit, 200f, 1 << Layer, QueryTriggerInteraction.Collide)) return null;
            var it = hit.collider.GetComponentInParent<LedgeItem>();
            return it != null && it.Hit != null && it.Hit.enabled ? it.Id : null;
        }

        /// <summary>Screen rect (GUI coordinates) of an item's hit box as the ledge camera sees it.</summary>
        public bool HitRect(string id, out Rect gui)
        {
            gui = default;
            if (!_items.TryGetValue(id, out var it) || it == null || !it.gameObject.activeInHierarchy) return false;
            var b = it.Hit; var t = b.transform;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var lp = b.center + Vector3.Scale(b.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var s = Cam.WorldToScreenPoint(t.TransformPoint(lp));
                minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x); minY = Mathf.Min(minY, s.y); maxY = Mathf.Max(maxY, s.y);
            }
            gui = new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
            return true;
        }

        private void OnDestroy()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("World glow")) Destroy(r.sharedMaterial);
        }
    }
}
