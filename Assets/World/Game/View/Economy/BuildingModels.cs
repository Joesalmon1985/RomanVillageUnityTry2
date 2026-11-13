using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>A built model: its root, an optional part that turns while the building works, glow parts, and its height.</summary>
    public sealed class BuildingModel
    {
        public GameObject Root;
        /// <summary>Turns while working (saw blade, mill sails, hoist wheel, grindstone); may be null.</summary>
        public Transform Spinner;
        public Vector3 SpinAxis = Vector3.forward;
        public float SpinDegPerSec = 180f;
        /// <summary>Lit while working (furnace mouth, ember, power coil); may be null.</summary>
        public Renderer Glow;
        public float Height = 5f;
    }

    /// <summary>
    /// Original low-poly building models from primitives (MeshBuilder + MaterialFactory), one distinct look per
    /// <see cref="BuildingType"/>. Local frame: origin at the footprint centre on the ground, +Z faces the building's
    /// flag (door and front). Every model stays inside the square footprint of its size class
    /// (<see cref="BuildingPlots.HalfFor"/>: Small 3.2, Medium 3.8, Large 4.4, Huge 5.0 m half side).
    /// </summary>
    public static class BuildingModels
    {
        public static readonly Color Plaster = new Color(0.88f, 0.82f, 0.70f);
        public static readonly Color Wood = new Color(0.47f, 0.32f, 0.19f);
        public static readonly Color DarkWood = new Color(0.32f, 0.21f, 0.12f);
        public static readonly Color StoneC = new Color(0.62f, 0.60f, 0.56f);
        public static readonly Color RoofRed = new Color(0.66f, 0.27f, 0.18f);
        public static readonly Color RoofBrown = new Color(0.42f, 0.27f, 0.16f);
        public static readonly Color RoofSlate = new Color(0.30f, 0.33f, 0.38f);
        public static readonly Color Leaf = new Color(0.20f, 0.48f, 0.20f);
        public static readonly Color BrickRed = new Color(0.62f, 0.27f, 0.20f);
        public static readonly Color Iron = new Color(0.30f, 0.31f, 0.34f);
        public static readonly Color Window = new Color(0.95f, 0.85f, 0.55f);

        private static Material _vc;
        public static Material VertexMat => _vc != null ? _vc : (_vc = MaterialFactory.CreateVertexColored(Color.white, "Economy buildings"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _vc = null; }

        private static Color W(Ware w) => EconomyLook.WareColor(w);

        public static BuildingModel Build(BuildingType type, Transform parent)
        {
            var m = new BuildingModel();
            var root = new GameObject("Model " + type);
            root.transform.SetParent(parent, false);
            m.Root = root;
            var mb = new MeshBuilder();
            var t = root.transform;
            switch (type)
            {
                case BuildingType.Storehouse: Storehouse(mb); m.Height = 6.5f; break;
                case BuildingType.Forester: Forester(mb); m.Height = 4.2f; break;
                case BuildingType.Woodcutter: Woodcutter(mb, t); m.Height = 4.4f; break;
                case BuildingType.Sawmill: Sawmill(mb, t, m); break;
                case BuildingType.ClayPit: ClayPit(mb); m.Height = 3.6f; break;
                case BuildingType.Farm: Farm(mb); m.Height = 4.6f; break;
                case BuildingType.Mill: Mill(mb, t, m); break;
                case BuildingType.Bakery: Bakery(mb, t, m); break;
                case BuildingType.Mine: Mine(mb, t, m); break;
                case BuildingType.Smelter: Smelter(mb, t, m); break;
                case BuildingType.PowerPlant: PowerPlant(mb, t, m); break;
                case BuildingType.Barracks: Barracks(mb); m.Height = 6.4f; break;
                case BuildingType.Market: Market(mb); m.Height = 4.2f; break;
                case BuildingType.WonderSite: Wonder(mb); m.Height = 9.5f; break;
                case BuildingType.Sheepfold: Sheepfold(mb); m.Height = 3.0f; break;
                case BuildingType.Butcher: Butcher(mb); m.Height = 4.6f; break;
                case BuildingType.Tavern: Tavern(mb); m.Height = 6.6f; break;
                case BuildingType.CharcoalKiln: CharcoalKiln(mb, t, m); break;
                case BuildingType.BrickKiln: BrickKiln(mb, t, m); break;
                case BuildingType.Smithy: Smithy(mb, t, m); break;
                default: House(mb, 4f, 4f, 3f, Plaster, RoofRed); m.Height = 5f; break;
            }
            mb.ToObject("Mesh", t, VertexMat);
            return m;
        }

        // ---------------- helpers ----------------

        /// <summary>Gable roof over a w x d footprint at wall height h, ridge along X (overhang 0.3 m).</summary>
        public static void GableRoof(MeshBuilder mb, Vector3 c, float w, float d, float h, float rise, Color roof, Color gable)
        {
            float ov = 0.3f;
            Vector3 f0 = c + new Vector3(-w / 2 - ov, h, d / 2 + ov), f1 = c + new Vector3(w / 2 + ov, h, d / 2 + ov);
            Vector3 b0 = c + new Vector3(-w / 2 - ov, h, -d / 2 - ov), b1 = c + new Vector3(w / 2 + ov, h, -d / 2 - ov);
            Vector3 r0 = c + new Vector3(-w / 2 - ov, h + rise, 0), r1 = c + new Vector3(w / 2 + ov, h + rise, 0);
            mb.Quad(f1, r1, r0, f0, roof);
            mb.Quad(b0, r0, r1, b1, roof * 0.88f);
            mb.Quad(f0, r0, r1, f1, roof * 0.5f);
            mb.Quad(b1, r1, r0, b0, roof * 0.5f);
            mb.Tri(c + new Vector3(-w / 2, h, d / 2), c + new Vector3(-w / 2, h + rise - 0.2f, 0), c + new Vector3(-w / 2, h, -d / 2), gable);
            mb.Tri(c + new Vector3(w / 2, h, -d / 2), c + new Vector3(w / 2, h + rise - 0.2f, 0), c + new Vector3(w / 2, h, d / 2), gable);
        }

        /// <summary>Single-slope roof (high at the back) over a w x d footprint at front wall height h.</summary>
        public static void ShedRoof(MeshBuilder mb, Vector3 c, float w, float d, float h, float rise, Color roof)
        {
            float ov = 0.25f;
            Vector3 f0 = c + new Vector3(-w / 2 - ov, h, d / 2 + ov), f1 = c + new Vector3(w / 2 + ov, h, d / 2 + ov);
            Vector3 b0 = c + new Vector3(-w / 2 - ov, h + rise, -d / 2 - ov), b1 = c + new Vector3(w / 2 + ov, h + rise, -d / 2 - ov);
            mb.Quad(f1, b1, b0, f0, roof);
            mb.Quad(f0, b0, b1, f1, roof * 0.5f);
        }

        /// <summary>Walls with a door and window on +Z, corner beams and a gable roof.</summary>
        public static void House(MeshBuilder mb, float w, float d, float h, Color wall, Color roof, Vector3 c = default, float rise = 1.6f, float doorX = 0f)
        {
            mb.Box(c + new Vector3(0, h / 2, 0), new Vector3(w, h, d), wall);
            foreach (float x in new[] { -w / 2, w / 2 }) foreach (float z in new[] { -d / 2, d / 2 })
                mb.Box(c + new Vector3(x, h / 2, z), new Vector3(0.26f, h + 0.05f, 0.26f), DarkWood);
            mb.Box(c + new Vector3(doorX, 1.0f, d / 2 + 0.03f), new Vector3(0.95f, 2.0f, 0.1f), DarkWood);
            float wx = doorX + (doorX <= 0 ? w * 0.28f : -w * 0.28f);
            if (w > 2.6f) mb.Box(c + new Vector3(wx, h * 0.6f, d / 2 + 0.03f), new Vector3(0.65f, 0.55f, 0.08f), Window);
            GableRoof(mb, c, w, d, h, rise, roof, wall);
        }

        private static void Sapling(MeshBuilder mb, Vector3 p, float s)
        {
            mb.Cylinder(p, 0.07f * s, 0.05f * s, 0.8f * s, 5, Wood);
            mb.Cone(p + Vector3.up * 0.5f * s, 0.55f * s, 1.3f * s, 6, Leaf * 1.15f);
        }

        /// <summary>A horizontal log (box with end caps) along X or Z.</summary>
        private static void Log(MeshBuilder mb, Vector3 c, float len, float r, bool alongX)
        {
            var bark = new Color(0.45f, 0.30f, 0.17f);
            var cut = new Color(0.78f, 0.62f, 0.40f);
            var size = alongX ? new Vector3(len, r * 2, r * 2) : new Vector3(r * 2, r * 2, len);
            mb.Box(c, size, bark, 0f);
            var endSize = alongX ? new Vector3(0.04f, r * 1.7f, r * 1.7f) : new Vector3(r * 1.7f, r * 1.7f, 0.04f);
            var off = alongX ? new Vector3(len / 2 + 0.02f, 0, 0) : new Vector3(0, 0, len / 2 + 0.02f);
            mb.Box(c + off, endSize, cut); mb.Box(c - off, endSize, cut);
        }

        private static void Crate(MeshBuilder mb, Vector3 c, float s, Color col)
        {
            mb.Box(c + Vector3.up * s / 2, Vector3.one * s, col);
            mb.Box(c + Vector3.up * s / 2, new Vector3(s + 0.04f, s * 0.18f, s + 0.04f), col * 0.6f);
        }

        private static void Barrel(MeshBuilder mb, Vector3 p)
        {
            mb.Cylinder(p, 0.38f, 0.42f, 0.5f, 10, new Color(0.50f, 0.33f, 0.18f));
            mb.Cylinder(p + Vector3.up * 0.5f, 0.42f, 0.38f, 0.5f, 10, new Color(0.50f, 0.33f, 0.18f));
            mb.Cylinder(p + Vector3.up * 0.46f, 0.43f, 0.43f, 0.08f, 10, Iron, false);
        }

        private static Renderer GlowBox(Transform root, string name, Vector3 pos, Vector3 size, Color c)
        {
            var g = Signs.Prim(PrimitiveType.Cube, root, name, pos, size, MaterialFactory.SharedUnlit(c));
            return g.GetComponent<Renderer>();
        }

        private static Transform Pivot(Transform root, string name, Vector3 pos)
        {
            var p = new GameObject(name).transform;
            p.SetParent(root, false);
            p.localPosition = pos;
            return p;
        }

        private static void Fence(MeshBuilder mb, Vector3 a, Vector3 b, float h = 0.9f)
        {
            var d = b - a; float len = d.magnitude; var dir = d / len; float yaw = BoardSpace.YawOf(dir);
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
            for (int i = 0; i <= posts; i++) mb.Box(a + dir * (i * len / posts) + Vector3.up * h / 2, new Vector3(0.1f, h, 0.1f), DarkWood);
            mb.Box(a + dir * (len / 2) + Vector3.up * h * 0.75f, new Vector3(0.06f, 0.07f, len), Wood, yaw);
            mb.Box(a + dir * (len / 2) + Vector3.up * h * 0.35f, new Vector3(0.06f, 0.07f, len), Wood, yaw);
        }

        // ---------------- models: small (half 3.2) ----------------

        private static void Forester(MeshBuilder mb)
        {
            House(mb, 3.0f, 2.6f, 2.4f, Plaster, new Color(0.30f, 0.45f, 0.24f), new Vector3(-1.3f, 0, -1.2f), 1.3f);
            var c = new Vector3(1.7f, 0, 1.0f);
            mb.Box(c + new Vector3(0, 0.03f, 0), new Vector3(2.4f, 0.06f, 2.6f), new Color(0.36f, 0.26f, 0.16f));
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                Sapling(mb, c + new Vector3(-0.8f + i * 0.8f, 0, -0.9f + j * 0.9f), 0.7f + 0.15f * ((i + j) % 2));
            Fence(mb, c + new Vector3(-1.3f, 0, -1.4f), c + new Vector3(1.3f, 0, -1.4f), 0.6f);
            Fence(mb, c + new Vector3(1.3f, 0, -1.4f), c + new Vector3(1.3f, 0, 1.4f), 0.6f);
            Fence(mb, c + new Vector3(1.3f, 0, 1.4f), c + new Vector3(-1.3f, 0, 1.4f), 0.6f);
            // Water butt and a spade.
            mb.Cylinder(new Vector3(-2.6f, 0, 0.6f), 0.4f, 0.4f, 0.9f, 8, new Color(0.40f, 0.28f, 0.16f));
            mb.Box(new Vector3(-1.9f, 0.7f, 0.7f), new Vector3(0.06f, 1.4f, 0.06f), DarkWood, 0f);
        }

        private static void Woodcutter(MeshBuilder mb, Transform root)
        {
            var logWall = new Color(0.52f, 0.35f, 0.20f);
            var c = new Vector3(-1.1f, 0, -1.2f);
            float w = 3.2f, d = 2.6f, h = 2.5f;
            // Log cabin: stacked horizontal logs.
            for (int i = 0; i < 6; i++)
            {
                float y = 0.21f + i * 0.4f;
                Log(mb, c + new Vector3(0, y, d / 2), w + 0.3f, 0.2f, true);
                Log(mb, c + new Vector3(0, y, -d / 2), w + 0.3f, 0.2f, true);
                Log(mb, c + new Vector3(-w / 2, y + 0.2f, 0), d + 0.3f, 0.2f, false);
                Log(mb, c + new Vector3(w / 2, y + 0.2f, 0), d + 0.3f, 0.2f, false);
            }
            mb.Box(c + new Vector3(0, h / 2, 0), new Vector3(w - 0.2f, h, d - 0.2f), logWall * 0.8f);
            mb.Box(c + new Vector3(0.4f, 1.0f, d / 2 + 0.22f), new Vector3(0.9f, 2.0f, 0.08f), DarkWood);
            GableRoof(mb, c, w, d, h, 1.4f, RoofBrown, logWall);
            // Log pile on the right, three rows along Z.
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                    Log(mb, new Vector3(2.0f + (i - (3 - row) * 0.5f) * 0.42f, 0.22f + row * 0.38f, -1.0f), 2.2f, 0.2f, false);
            // Chopping block with an axe, and a felled trunk.
            mb.Cylinder(new Vector3(1.4f, 0, 1.7f), 0.35f, 0.33f, 0.55f, 8, new Color(0.60f, 0.42f, 0.26f));
            mb.Box(new Vector3(1.4f, 0.85f, 1.7f), new Vector3(0.06f, 0.65f, 0.06f), DarkWood);
            mb.Box(new Vector3(1.52f, 1.12f, 1.7f), new Vector3(0.25f, 0.14f, 0.04f), new Color(0.75f, 0.77f, 0.8f));
            Log(mb, new Vector3(-1.0f, 0.25f, 1.9f), 3.0f, 0.25f, true);
        }

        private static void ClayPit(MeshBuilder mb)
        {
            var clay = W(Ware.Clay);
            var earth = new Color(0.38f, 0.26f, 0.16f);
            // The pit: dark floor with clay walls stepped down.
            mb.Box(new Vector3(-0.6f, 0.02f, -0.8f), new Vector3(4.0f, 0.04f, 3.2f), earth * 0.6f);
            mb.Box(new Vector3(-0.6f, 0.05f, -0.8f), new Vector3(3.2f, 0.04f, 2.4f), clay * 0.7f);
            mb.Box(new Vector3(-0.6f, 0.07f, -0.8f), new Vector3(2.2f, 0.04f, 1.5f), new Color(0.22f, 0.30f, 0.38f)); // puddle
            // Spoil heaps of clay around it.
            mb.Cone(new Vector3(-2.4f, 0, -2.4f), 0.8f, 0.9f, 8, clay);
            mb.Cone(new Vector3(1.9f, 0, -2.3f), 1.0f, 1.1f, 8, clay * 0.95f);
            mb.Cone(new Vector3(2.2f, 0, -0.6f), 0.7f, 0.7f, 8, clay * 1.05f);
            // Lean-to shelter on posts over the back edge.
            foreach (float x in new[] { -2.5f, 1.3f }) foreach (float z in new[] { -2.6f, -1.2f })
                mb.Box(new Vector3(x, 1.2f, z), new Vector3(0.16f, 2.4f, 0.16f), DarkWood);
            ShedRoof(mb, new Vector3(-0.6f, 0, -1.9f), 4.0f, 1.6f, 2.2f, 0.6f, RoofBrown);
            // Ladder down, spade, and a barrow of clay at the front.
            mb.Box(new Vector3(-1.6f, 0.6f, 0.8f), new Vector3(0.06f, 1.2f, 0.06f), Wood, 0f);
            mb.Box(new Vector3(-1.1f, 0.6f, 0.8f), new Vector3(0.06f, 1.2f, 0.06f), Wood, 0f);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-1.35f, 0.1f + i * 0.28f, 0.8f), new Vector3(0.5f, 0.05f, 0.05f), Wood);
            mb.Box(new Vector3(1.4f, 0.45f, 2.0f), new Vector3(0.9f, 0.4f, 1.2f), Wood);
            mb.Box(new Vector3(1.4f, 0.72f, 2.0f), new Vector3(0.8f, 0.18f, 1.0f), clay);
            mb.Cylinder(new Vector3(1.4f, 0, 2.7f), 0.22f, 0.22f, 0.1f, 8, DarkWood);
        }

        private static void Farm(MeshBuilder mb)
        {
            var red = new Color(0.66f, 0.20f, 0.16f);
            var c = new Vector3(-0.9f, 0, -1.2f);
            mb.Box(c + new Vector3(0, 1.3f, 0), new Vector3(3.6f, 2.6f, 2.8f), red);
            mb.Box(c + new Vector3(0, 1.0f, 1.42f), new Vector3(1.4f, 2.0f, 0.06f), new Color(0.92f, 0.90f, 0.85f));
            mb.Box(c + new Vector3(0, 1.0f, 1.46f), new Vector3(1.3f, 0.1f, 0.05f), red * 0.7f, 0f);
            GableRoof(mb, c, 3.6f, 2.8f, 2.6f, 1.7f, new Color(0.32f, 0.30f, 0.28f), red);
            var hay = W(Ware.Wheat);
            mb.Cylinder(new Vector3(2.2f, 0, -1.6f), 0.55f, 0.55f, 0.8f, 10, hay * 0.9f);
            mb.Cylinder(new Vector3(2.3f, 0, -0.3f), 0.55f, 0.55f, 0.8f, 10, hay * 0.85f);
            // Sheaves of wheat in a row by the flag side.
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3(-2.4f + i * 1.15f, 0, 2.2f);
                mb.Cylinder(p, 0.22f, 0.3f, 0.9f, 6, hay);
                mb.Cone(p + Vector3.up * 0.9f, 0.32f, 0.35f, 6, hay * 1.05f);
            }
            // Plough.
            mb.Box(new Vector3(2.1f, 0.3f, 1.0f), new Vector3(0.12f, 0.12f, 1.6f), Wood, 20f);
            mb.Box(new Vector3(2.35f, 0.25f, 0.4f), new Vector3(0.4f, 0.35f, 0.08f), Iron, 20f);
        }

        private static void Sheepfold(MeshBuilder mb)
        {
            // Low shepherd's shed at the back, a round pen with sheep at the front.
            var c = new Vector3(-1.2f, 0, -1.9f);
            mb.Box(c + new Vector3(0, 0.9f, 0), new Vector3(3.2f, 1.8f, 2.2f), new Color(0.80f, 0.74f, 0.62f));
            mb.Box(c + new Vector3(0.6f, 0.75f, 1.12f), new Vector3(0.9f, 1.5f, 0.06f), DarkWood);
            ShedRoof(mb, c, 3.2f, 2.2f, 1.8f, 0.5f, new Color(0.48f, 0.40f, 0.26f));
            mb.Box(new Vector3(1.8f, 0.4f, -2.2f), new Vector3(1.4f, 0.8f, 1.2f), W(Ware.Wheat) * 0.8f); // hay rack
            var pc = new Vector3(0.6f, 0, 1.0f);
            int n = 10; float r = 2.1f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                if (i == 2) continue; // gate
                Fence(mb, pc + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), pc + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r), 0.8f);
            }
            var rnd = new PropRandom(41);
            TerrainBuilder.Sheep(mb, pc + new Vector3(-0.7f, 0, -0.3f), 40f, rnd);
            TerrainBuilder.Sheep(mb, pc + new Vector3(0.8f, 0, 0.5f), 200f, rnd);
            TerrainBuilder.Sheep(mb, pc + new Vector3(0.1f, 0, -1.0f), 110f, rnd);
            mb.Box(pc + new Vector3(-0.9f, 0.25f, 1.0f), new Vector3(1.0f, 0.5f, 0.4f), Wood); // trough
        }

        // ---------------- medium (half 3.8) ----------------

        private static void Sawmill(MeshBuilder mb, Transform root, BuildingModel m)
        {
            float w = 4.8f, d = 3.4f, h = 2.8f;
            var c = new Vector3(-0.5f, 0, -0.3f);
            foreach (float x in new[] { -w / 2, 0, w / 2 }) foreach (float z in new[] { -d / 2, d / 2 })
                mb.Box(c + new Vector3(x, h / 2, z), new Vector3(0.28f, h, 0.28f), DarkWood);
            mb.Box(c + new Vector3(0, h / 2, -d / 2), new Vector3(w, h, 0.2f), new Color(0.55f, 0.40f, 0.25f));
            GableRoof(mb, c, w, d, h, 1.3f, RoofSlate, new Color(0.55f, 0.40f, 0.25f));
            mb.Box(c + new Vector3(0, 0.8f, 0.2f), new Vector3(3.6f, 0.18f, 0.9f), Wood);
            foreach (float x in new[] { -1.5f, 1.5f }) mb.Box(c + new Vector3(x, 0.4f, 0.2f), new Vector3(0.2f, 0.8f, 0.8f), DarkWood);
            Log(mb, c + new Vector3(-1.0f, 1.06f, 0.2f), 1.6f, 0.17f, true);
            // Plank stack front-right and a log heap back-right.
            for (int i = 0; i < 6; i++) mb.Box(new Vector3(2.9f, 0.08f + i * 0.14f, 1.6f), new Vector3(1.6f, 0.11f, 0.6f), W(Ware.Planks) * (0.95f + 0.02f * i));
            for (int i = 0; i < 3; i++) Log(mb, new Vector3(2.9f, 0.2f, -2.4f + i * 0.42f), 1.6f, 0.2f, true);
            Log(mb, new Vector3(2.9f, 0.55f, -2.2f), 1.6f, 0.2f, true);
            var pivot = Pivot(root, "Blade", c + new Vector3(0.5f, 1.1f, 0.2f));
            Signs.Prim(PrimitiveType.Cylinder, pivot, "Disc", Vector3.zero, new Vector3(1.1f, 0.02f, 1.1f), MaterialFactory.Shared(new Color(0.80f, 0.82f, 0.86f)), new Vector3(0, 0, 90f));
            Signs.Prim(PrimitiveType.Cube, pivot, "Tooth", new Vector3(0, 0.53f, 0), new Vector3(0.05f, 0.12f, 0.12f), MaterialFactory.Shared(Iron));
            Signs.Prim(PrimitiveType.Cube, pivot, "Tooth", new Vector3(0, -0.53f, 0), new Vector3(0.05f, 0.12f, 0.12f), MaterialFactory.Shared(Iron));
            m.Spinner = pivot; m.SpinAxis = Vector3.right; m.SpinDegPerSec = 540f;
            m.Height = h + 1.4f;
        }

        private static void Mill(MeshBuilder mb, Transform root, BuildingModel m)
        {
            var c = new Vector3(0, 0, -0.9f);
            mb.Cylinder(c, 2.2f, 1.5f, 5.6f, 12, Plaster);
            mb.Cylinder(c, 2.3f, 2.3f, 0.5f, 12, StoneC);
            mb.Cone(c + new Vector3(0, 5.6f, 0), 1.75f, 1.8f, 12, RoofRed);
            mb.Box(c + new Vector3(0, 1.0f, 2.0f), new Vector3(0.9f, 2.0f, 0.4f), DarkWood);
            mb.Box(c + new Vector3(0, 3.4f, 1.75f), new Vector3(0.5f, 0.5f, 0.2f), Window);
            // Flour sacks by the door.
            for (int i = 0; i < 3; i++)
                mb.Box(new Vector3(1.5f + (i % 2) * 0.6f, 0.3f + (i / 2) * 0.5f, 1.8f), new Vector3(0.55f, 0.6f, 0.45f), W(Ware.Flour) * 0.95f);
            var hub = Pivot(root, "Sails", c + new Vector3(0, 5.0f, 1.75f));
            var sailMat = MaterialFactory.Shared(new Color(0.94f, 0.92f, 0.86f));
            var armMat = MaterialFactory.Shared(DarkWood);
            for (int i = 0; i < 4; i++)
            {
                var arm = new GameObject("Arm").transform;
                arm.SetParent(hub, false);
                arm.localRotation = Quaternion.Euler(0, 0, i * 90f + 20f);
                Signs.Prim(PrimitiveType.Cube, arm, "Spar", new Vector3(0, 1.75f, 0), new Vector3(0.14f, 3.5f, 0.1f), armMat);
                Signs.Prim(PrimitiveType.Cube, arm, "Sail", new Vector3(0.42f, 2.1f, 0.03f), new Vector3(0.75f, 2.6f, 0.04f), sailMat);
            }
            Signs.Prim(PrimitiveType.Cylinder, hub, "Hub", Vector3.zero, new Vector3(0.4f, 0.2f, 0.4f), armMat, new Vector3(90, 0, 0));
            m.Spinner = hub; m.SpinAxis = Vector3.forward; m.SpinDegPerSec = 60f;
            m.Height = 8.8f;
        }

        private static void Bakery(MeshBuilder mb, Transform root, BuildingModel m)
        {
            House(mb, 4.0f, 3.0f, 2.8f, new Color(0.93f, 0.88f, 0.76f), RoofRed, new Vector3(-0.9f, 0, -1.2f), 1.6f, -0.4f);
            mb.Box(new Vector3(0.3f, 4.2f, -1.6f), new Vector3(0.6f, 2.4f, 0.6f), BrickRed);
            mb.Box(new Vector3(0.3f, 5.45f, -1.6f), new Vector3(0.75f, 0.12f, 0.75f), BrickRed * 0.8f);
            // Bread oven dome by the door with a glowing mouth.
            var oc = new Vector3(2.3f, 0, 1.4f);
            mb.Cylinder(oc, 1.1f, 1.0f, 0.5f, 12, StoneC);
            mb.Cylinder(oc + Vector3.up * 0.5f, 1.0f, 0.35f, 1.0f, 12, BrickRed * 1.1f);
            mb.Box(oc + new Vector3(0, 0.75f, 0.95f), new Vector3(0.6f, 0.5f, 0.12f), new Color(0.12f, 0.06f, 0.04f));
            m.Glow = GlowBox(root, "OvenGlow", oc + new Vector3(0, 0.72f, 1.0f), new Vector3(0.45f, 0.35f, 0.05f), new Color(1f, 0.55f, 0.15f));
            // Hanging loaf sign and a bread table.
            mb.Box(new Vector3(-0.9f, 2.4f, 0.45f), new Vector3(1.2f, 0.45f, 0.08f), DarkWood);
            mb.Box(new Vector3(-0.9f, 2.4f, 0.5f), new Vector3(0.8f, 0.28f, 0.06f), W(Ware.Bread));
            mb.Box(new Vector3(-2.3f, 0.45f, 1.5f), new Vector3(1.6f, 0.9f, 0.7f), Wood);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-2.85f + i * 0.38f, 0.98f, 1.5f), new Vector3(0.3f, 0.16f, 0.22f), W(Ware.Bread));
            m.Height = 6.0f;
        }

        private static void Mine(MeshBuilder mb, Transform root, BuildingModel m)
        {
            var rock = new Color(0.42f, 0.40f, 0.40f);
            mb.Cylinder(new Vector3(0, 0, -1.2f), 2.6f, 1.2f, 2.8f, 9, rock);
            mb.Cylinder(new Vector3(-1.6f, 0, -2.4f), 1.2f, 0.4f, 1.6f, 7, rock * 0.9f);
            // Timbered adit.
            mb.Box(new Vector3(0, 1.05f, 1.15f), new Vector3(1.8f, 2.1f, 0.9f), new Color(0.05f, 0.04f, 0.04f));
            mb.Box(new Vector3(-1.0f, 1.15f, 1.5f), new Vector3(0.24f, 2.3f, 0.24f), Wood);
            mb.Box(new Vector3(1.0f, 1.15f, 1.5f), new Vector3(0.24f, 2.3f, 0.24f), Wood);
            mb.Box(new Vector3(0, 2.35f, 1.5f), new Vector3(2.5f, 0.28f, 0.3f), Wood);
            // Rails and an ore cart.
            mb.Box(new Vector3(-0.35f, 0.03f, 2.5f), new Vector3(0.08f, 0.06f, 2.4f), Iron);
            mb.Box(new Vector3(0.35f, 0.03f, 2.5f), new Vector3(0.08f, 0.06f, 2.4f), Iron);
            mb.Box(new Vector3(0, 0.55f, 2.9f), new Vector3(1.0f, 0.6f, 1.1f), new Color(0.35f, 0.25f, 0.18f));
            mb.Box(new Vector3(0, 0.9f, 2.9f), new Vector3(0.8f, 0.2f, 0.9f), W(Ware.Ore));
            // Ore heap and a ration crate (the miners eat).
            mb.Cone(new Vector3(2.6f, 0, 1.2f), 0.9f, 0.8f, 7, W(Ware.Ore));
            Crate(mb, new Vector3(-2.5f, 0, 1.6f), 0.7f, W(Ware.Rations));
            // Headframe with a hoist wheel on top of the rock.
            mb.Box(new Vector3(1.9f, 2.2f, -1.8f), new Vector3(0.18f, 4.4f, 0.18f), Wood);
            mb.Box(new Vector3(1.9f, 2.2f, -0.6f), new Vector3(0.18f, 4.4f, 0.18f), Wood);
            mb.Box(new Vector3(1.9f, 4.35f, -1.2f), new Vector3(0.2f, 0.2f, 1.4f), Wood);
            var wheel = Pivot(root, "HoistWheel", new Vector3(1.9f, 4.6f, -1.2f));
            Signs.Prim(PrimitiveType.Cylinder, wheel, "Wheel", Vector3.zero, new Vector3(1.2f, 0.05f, 1.2f), MaterialFactory.Shared(Iron), new Vector3(0, 0, 90f));
            Signs.Prim(PrimitiveType.Cube, wheel, "Spoke", Vector3.zero, new Vector3(0.08f, 1.1f, 0.08f), MaterialFactory.Shared(DarkWood));
            m.Spinner = wheel; m.SpinAxis = Vector3.right; m.SpinDegPerSec = 120f;
            m.Height = 5.4f;
        }

        private static void Butcher(MeshBuilder mb)
        {
            var c = new Vector3(0, 0, -1.4f);
            House(mb, 4.6f, 2.8f, 2.8f, new Color(0.95f, 0.93f, 0.88f), new Color(0.40f, 0.30f, 0.25f), c, 1.4f, -1.3f);
            // Striped awning over the counter.
            for (int i = 0; i < 6; i++)
            {
                var col = i % 2 == 0 ? new Color(0.75f, 0.15f, 0.15f) : new Color(0.95f, 0.93f, 0.90f);
                mb.Box(new Vector3(0.1f + i * 0.5f - 0.0f, 2.5f, 0.6f), new Vector3(0.5f, 0.08f, 1.4f), col, 0f);
            }
            foreach (float x in new[] { -0.15f, 2.85f }) mb.Box(new Vector3(x, 1.25f, 1.25f), new Vector3(0.1f, 2.5f, 0.1f), DarkWood);
            mb.Box(new Vector3(1.35f, 0.5f, 0.6f), new Vector3(2.8f, 1.0f, 0.8f), Wood);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(0.3f + i * 0.7f, 1.08f, 0.6f), new Vector3(0.5f, 0.16f, 0.35f), W(Ware.Meat));
            // Hanging rack with joints.
            mb.Box(new Vector3(-2.6f, 1.6f, 1.2f), new Vector3(0.1f, 3.2f, 0.1f), DarkWood);
            mb.Box(new Vector3(-1.6f, 1.6f, 1.2f), new Vector3(0.1f, 3.2f, 0.1f), DarkWood);
            mb.Box(new Vector3(-2.1f, 3.1f, 1.2f), new Vector3(1.2f, 0.1f, 0.1f), DarkWood);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-2.45f + i * 0.35f, 2.55f, 1.2f), new Vector3(0.22f, 0.7f, 0.22f), W(Ware.Meat) * 0.9f);
            // Pen with one sheep waiting.
            var rnd = new PropRandom(77);
            TerrainBuilder.Sheep(mb, new Vector3(-2.6f, 0, 2.6f), 90f, rnd);
            Fence(mb, new Vector3(-3.6f, 0, 1.9f), new Vector3(-1.4f, 0, 1.9f), 0.8f);
            Fence(mb, new Vector3(-1.4f, 0, 1.9f), new Vector3(-1.4f, 0, 3.4f), 0.8f);
        }

        private static void Tavern(MeshBuilder mb)
        {
            var c = new Vector3(-0.3f, 0, -1.0f);
            float w = 4.6f, d = 3.2f, h = 4.4f;
            mb.Box(c + new Vector3(0, 1.1f, 0), new Vector3(w, 2.2f, d), StoneC);
            mb.Box(c + new Vector3(0, 3.3f, 0), new Vector3(w + 0.2f, 2.2f, d + 0.2f), Plaster);
            foreach (float x in new[] { -w / 2 - 0.1f, -0.8f, 0.8f, w / 2 + 0.1f })
                mb.Box(c + new Vector3(x, 3.3f, d / 2 + 0.12f), new Vector3(0.2f, 2.2f, 0.06f), DarkWood);
            mb.Box(c + new Vector3(0, 2.25f, d / 2 + 0.12f), new Vector3(w + 0.2f, 0.18f, 0.08f), DarkWood);
            mb.Box(c + new Vector3(0, 1.0f, d / 2 + 0.03f), new Vector3(1.0f, 2.0f, 0.1f), DarkWood);
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                mb.Box(c + new Vector3(x, 1.2f, d / 2 + 0.03f), new Vector3(0.7f, 0.6f, 0.08f), Window);
                mb.Box(c + new Vector3(x * 0.75f, 3.4f, d / 2 + 0.16f), new Vector3(0.6f, 0.6f, 0.06f), Window);
            }
            GableRoof(mb, c, w + 0.2f, d + 0.2f, h, 1.8f, RoofBrown, Plaster);
            mb.Box(c + new Vector3(1.3f, h + 1.3f, -0.6f), new Vector3(0.5f, 1.6f, 0.5f), StoneC * 0.85f);
            // Hanging sign (green for rations) on a bracket post.
            mb.Box(new Vector3(2.9f, 1.5f, 1.6f), new Vector3(0.14f, 3.0f, 0.14f), DarkWood);
            mb.Box(new Vector3(2.5f, 2.95f, 1.6f), new Vector3(0.9f, 0.1f, 0.1f), DarkWood);
            mb.Box(new Vector3(2.4f, 2.45f, 1.6f), new Vector3(0.08f, 0.7f, 0.9f), W(Ware.Rations));
            // Barrels and a bench.
            Barrel(mb, new Vector3(-2.9f, 0, 1.5f));
            Barrel(mb, new Vector3(-2.2f, 0, 2.3f));
            mb.Box(new Vector3(0.2f, 0.42f, 1.6f), new Vector3(2.0f, 0.1f, 0.45f), Wood);
            foreach (float x in new[] { -0.6f, 1.0f }) mb.Box(new Vector3(x, 0.2f, 1.6f), new Vector3(0.1f, 0.4f, 0.4f), DarkWood);
        }

        private static void CharcoalKiln(MeshBuilder mb, Transform root, BuildingModel m)
        {
            var earth = new Color(0.26f, 0.22f, 0.18f);
            var kc = new Vector3(-0.6f, 0, -0.9f);
            mb.Cylinder(kc, 2.6f, 1.6f, 1.2f, 14, earth);
            mb.Cylinder(kc + Vector3.up * 1.2f, 1.6f, 0.4f, 1.2f, 14, earth * 1.15f);
            mb.Cylinder(kc + Vector3.up * 2.4f, 0.35f, 0.25f, 0.5f, 8, new Color(0.15f, 0.13f, 0.12f));
            // Turf patches and vents.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                mb.Box(kc + new Vector3(Mathf.Cos(a) * 2.0f, 0.55f, Mathf.Sin(a) * 2.0f), new Vector3(0.4f, 0.4f, 0.4f), new Color(0.30f, 0.42f, 0.22f), i * 60f);
            }
            m.Glow = GlowBox(root, "Ember", kc + new Vector3(0, 0.35f, 2.55f), new Vector3(0.6f, 0.4f, 0.1f), new Color(1f, 0.45f, 0.10f));
            // Smoke puffs (static, pale).
            mb.Cone(kc + new Vector3(0.1f, 2.9f, 0), 0.5f, 0.9f, 7, new Color(0.70f, 0.70f, 0.72f));
            mb.Cone(kc + new Vector3(0.3f, 3.6f, -0.2f), 0.35f, 0.7f, 7, new Color(0.80f, 0.80f, 0.82f));
            // Log stack (input) and coal heap (output).
            for (int row = 0; row < 3; row++) for (int i = 0; i < 3 - row; i++)
                Log(mb, new Vector3(2.5f + (i - (2 - row) * 0.5f) * 0.42f, 0.2f + row * 0.36f, -1.6f), 2.2f, 0.19f, false);
            mb.Cone(new Vector3(2.4f, 0, 1.8f), 1.0f, 0.8f, 8, W(Ware.Coal));
            mb.Box(new Vector3(1.2f, 0.25f, 2.6f), new Vector3(0.9f, 0.5f, 0.6f), Wood);
            mb.Box(new Vector3(1.2f, 0.52f, 2.6f), new Vector3(0.8f, 0.1f, 0.5f), W(Ware.Coal));
            m.Height = 4.2f;
        }

        private static void BrickKiln(MeshBuilder mb, Transform root, BuildingModel m)
        {
            var kc = new Vector3(-0.8f, 0, -1.0f);
            // Bottle kiln: a brick hovel tapering to a chimney.
            mb.Cylinder(kc, 2.2f, 1.9f, 1.8f, 14, BrickRed);
            mb.Cylinder(kc + Vector3.up * 1.8f, 1.9f, 0.7f, 2.4f, 14, BrickRed * 0.95f);
            mb.Cylinder(kc + Vector3.up * 4.2f, 0.7f, 0.6f, 1.6f, 10, BrickRed * 0.85f);
            mb.Cylinder(kc + Vector3.up * 5.75f, 0.68f, 0.68f, 0.15f, 10, StoneC);
            for (int i = 1; i < 4; i++) mb.Cylinder(kc + Vector3.up * (i * 1.1f), 2.25f - i * 0.25f, 2.25f - i * 0.25f, 0.1f, 14, StoneC * 0.9f, false);
            mb.Box(kc + new Vector3(0, 0.8f, 2.05f), new Vector3(1.0f, 1.6f, 0.3f), new Color(0.10f, 0.06f, 0.05f));
            m.Glow = GlowBox(root, "Firebox", kc + new Vector3(0, 0.55f, 2.18f), new Vector3(0.7f, 0.6f, 0.05f), new Color(1f, 0.50f, 0.12f));
            // Stacks of finished bricks (output) and the clay heap and coal (inputs).
            var brick = W(Ware.Bricks);
            for (int s = 0; s < 2; s++)
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 3; x++)
                        mb.Box(new Vector3(2.0f + x * 0.45f, 0.11f + y * 0.21f, 0.6f + s * 1.2f), new Vector3(0.42f, 0.2f, 0.9f), brick * (0.92f + 0.03f * ((x + y) % 3)), (y % 2) * 0f);
            mb.Cone(new Vector3(2.5f, 0, -2.3f), 1.0f, 0.9f, 8, W(Ware.Clay));
            mb.Cone(new Vector3(-3.0f, 0, 2.3f), 0.7f, 0.5f, 8, W(Ware.Coal));
            m.Height = 6.2f;
        }

        // ---------------- large (half 4.4) ----------------

        private static void Smelter(MeshBuilder mb, Transform root, BuildingModel m)
        {
            var c = new Vector3(-0.8f, 0, -1.2f);
            float w = 5.0f, d = 3.6f, h = 3.4f;
            mb.Box(c + new Vector3(0, h / 2, 0), new Vector3(w, h, d), StoneC * 0.85f);
            GableRoof(mb, c, w, d, h, 1.0f, new Color(0.35f, 0.38f, 0.42f), StoneC * 0.85f);
            // Tall furnace stack.
            mb.Cylinder(new Vector3(2.6f, 0, -2.6f), 1.1f, 0.7f, 8.0f, 10, new Color(0.34f, 0.30f, 0.28f));
            mb.Cylinder(new Vector3(2.6f, 8.0f, -2.6f), 0.75f, 0.75f, 0.3f, 10, Iron);
            // Furnace mouth.
            mb.Box(c + new Vector3(0, 1.0f, d / 2 + 0.06f), new Vector3(1.8f, 1.6f, 0.15f), new Color(0.12f, 0.10f, 0.09f));
            m.Glow = GlowBox(root, "FurnaceGlow", c + new Vector3(0, 0.9f, d / 2 + 0.15f), new Vector3(1.3f, 1.0f, 0.06f), new Color(1f, 0.55f, 0.15f));
            // Ore and coal bins, iron bars stacked.
            mb.Box(new Vector3(-3.3f, 0.4f, 2.4f), new Vector3(1.4f, 0.8f, 1.2f), Wood);
            mb.Box(new Vector3(-3.3f, 0.82f, 2.4f), new Vector3(1.2f, 0.12f, 1.0f), W(Ware.Ore));
            mb.Box(new Vector3(-1.7f, 0.4f, 2.4f), new Vector3(1.4f, 0.8f, 1.2f), Wood);
            mb.Box(new Vector3(-1.7f, 0.82f, 2.4f), new Vector3(1.2f, 0.12f, 1.0f), W(Ware.Coal));
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(1.6f, 0.1f + i * 0.16f, 2.6f), new Vector3(1.6f, 0.14f, 0.5f), W(Ware.Iron), (i % 2) * 6f);
            // Power line pole with blue insulators: this building needs power.
            mb.Box(new Vector3(3.6f, 2.5f, 1.4f), new Vector3(0.18f, 5.0f, 0.18f), DarkWood);
            mb.Box(new Vector3(3.6f, 4.8f, 1.4f), new Vector3(1.2f, 0.12f, 0.12f), DarkWood);
            foreach (float x in new[] { 3.15f, 4.05f }) mb.Cylinder(new Vector3(x, 4.86f, 1.4f), 0.1f, 0.06f, 0.3f, 6, new Color(0.40f, 0.70f, 0.95f));
            m.Height = 8.4f;
        }

        private static void PowerPlant(MeshBuilder mb, Transform root, BuildingModel m)
        {
            var c = new Vector3(-0.9f, 0, -1.0f);
            float w = 5.4f, d = 3.8f, h = 3.2f;
            mb.Box(c + new Vector3(0, h / 2, 0), new Vector3(w, h, d), BrickRed * 0.95f);
            for (int i = 0; i < 4; i++) mb.Box(c + new Vector3(-1.9f + i * 1.25f, 1.9f, d / 2 + 0.04f), new Vector3(0.6f, 1.2f, 0.06f), Window * 0.9f);
            mb.Box(c + new Vector3(0, 0.9f, d / 2 + 0.04f), new Vector3(1.2f, 1.8f, 0.08f), DarkWood);
            GableRoof(mb, c, w, d, h, 0.9f, RoofSlate, BrickRed * 0.95f);
            var sc = new Vector3(3.2f, 0, -2.8f);
            mb.Cylinder(sc, 1.0f, 0.65f, 9.5f, 12, BrickRed * 0.85f);
            mb.Cylinder(sc + Vector3.up * 9.5f, 0.75f, 0.75f, 0.4f, 12, BrickRed * 0.6f);
            for (int i = 1; i < 4; i++) mb.Cylinder(sc + Vector3.up * (i * 2.3f), 1.02f - i * 0.09f, 1.02f - i * 0.09f, 0.2f, 12, new Color(0.88f, 0.88f, 0.85f), false);
            // Coil on a pylon beside the hall: glows while supplying power.
            mb.Box(new Vector3(3.0f, 2.0f, 1.6f), new Vector3(0.3f, 4.0f, 0.3f), Iron);
            mb.Box(new Vector3(3.0f, 0.15f, 1.6f), new Vector3(1.2f, 0.3f, 1.2f), StoneC);
            for (int i = 0; i < 4; i++) mb.Cylinder(new Vector3(3.0f, 2.8f + i * 0.28f, 1.6f), 0.45f, 0.45f, 0.12f, 10, new Color(0.72f, 0.45f, 0.20f));
            var coil = Signs.Prim(PrimitiveType.Sphere, root, "Coil", new Vector3(3.0f, 4.4f, 1.6f), Vector3.one * 0.8f, MaterialFactory.SharedUnlit(new Color(0.55f, 0.85f, 1f)));
            m.Glow = coil.GetComponent<Renderer>();
            // Coal heap.
            mb.Cone(new Vector3(-3.3f, 0, 2.6f), 1.0f, 0.9f, 8, W(Ware.Coal));
            mb.Cone(new Vector3(-2.3f, 0, 2.9f), 0.7f, 0.6f, 8, W(Ware.Coal) * 1.2f);
            m.Height = 10.2f;
        }

        private static void Market(MeshBuilder mb)
        {
            // Two stalls under yellow-and-green awnings, crates of every colour.
            for (int s = 0; s < 2; s++)
            {
                var c = new Vector3(-2.0f + s * 4.0f, 0, -0.4f - s * 1.2f);
                mb.Box(c + new Vector3(0, 0.5f, 0.6f), new Vector3(3.2f, 1.0f, 0.8f), Wood);
                foreach (float x in new[] { -1.6f, 1.6f }) foreach (float z in new[] { -1.2f, 1.0f })
                    mb.Box(c + new Vector3(x, 1.4f, z), new Vector3(0.14f, 2.8f, 0.14f), DarkWood);
                for (int i = 0; i < 6; i++)
                {
                    var col = i % 2 == 0 ? new Color(0.90f, 0.75f, 0.20f) : new Color(0.25f, 0.55f, 0.30f);
                    mb.Box(c + new Vector3(-1.65f + 3.3f * (i + 0.5f) / 6f, 2.85f + (i % 2) * 0.02f, -0.1f), new Vector3(3.3f / 6f + 0.01f, 0.1f, 2.6f), col);
                }
                for (int i = 0; i < 4; i++)
                    mb.Box(c + new Vector3(-1.1f + i * 0.72f, 1.1f, 0.6f), new Vector3(0.55f, 0.2f, 0.45f), W((Ware)((i + s * 4) % Wares.Count)));
            }
            for (int i = 0; i < 5; i++) Crate(mb, new Vector3(-3.6f + i * 0.9f, 0, 3.0f), 0.7f, W((Ware)((i * 3 + 1) % Wares.Count)));
            // Scales post with a coin-coloured sign.
            mb.Box(new Vector3(3.6f, 1.5f, 2.2f), new Vector3(0.14f, 3.0f, 0.14f), DarkWood);
            mb.Box(new Vector3(3.6f, 2.8f, 2.4f), new Vector3(1.0f, 0.6f, 0.08f), new Color(0.95f, 0.80f, 0.25f));
        }

        private static void Smithy(MeshBuilder mb, Transform root, BuildingModel m)
        {
            // Open forge: roof on posts, stone hearth and chimney at the back.
            float w = 6.0f, d = 4.0f, h = 2.9f;
            var c = new Vector3(-0.6f, 0, -1.2f);
            foreach (float x in new[] { -w / 2, w / 2 }) foreach (float z in new[] { -d / 2, d / 2 })
                mb.Box(c + new Vector3(x, h / 2, z), new Vector3(0.3f, h, 0.3f), DarkWood);
            GableRoof(mb, c, w, d, h, 1.2f, RoofSlate, Wood);
            mb.Box(c + new Vector3(-1.2f, 0.6f, -1.3f), new Vector3(2.0f, 1.2f, 1.2f), StoneC);
            mb.Box(c + new Vector3(-1.2f, 3.2f, -1.6f), new Vector3(0.9f, 4.4f, 0.8f), StoneC * 0.9f);
            m.Glow = GlowBox(root, "ForgeGlow", c + new Vector3(-1.2f, 1.22f, -1.3f), new Vector3(1.4f, 0.06f, 0.8f), new Color(1f, 0.50f, 0.12f));
            // Anvil.
            mb.Box(c + new Vector3(0.9f, 0.35f, 0.2f), new Vector3(0.4f, 0.7f, 0.4f), Iron * 0.8f);
            mb.Box(c + new Vector3(0.9f, 0.8f, 0.2f), new Vector3(0.9f, 0.25f, 0.35f), Iron);
            // Weapon rack (output) and iron bars (input).
            mb.Box(new Vector3(3.3f, 1.0f, 2.2f), new Vector3(0.12f, 2.0f, 0.12f), DarkWood);
            mb.Box(new Vector3(1.9f, 1.0f, 2.2f), new Vector3(0.12f, 2.0f, 0.12f), DarkWood);
            mb.Box(new Vector3(2.6f, 1.8f, 2.2f), new Vector3(1.6f, 0.1f, 0.1f), DarkWood);
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(2.05f + i * 0.28f, 1.05f, 2.25f), new Vector3(0.06f, 1.5f, 0.08f), W(Ware.Weapons) * 1.3f);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-3.2f, 0.08f + i * 0.15f, 2.4f), new Vector3(1.4f, 0.13f, 0.45f), W(Ware.Iron));
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-1.6f, 0.08f + i * 0.13f, 2.6f), new Vector3(1.2f, 0.11f, 0.5f), W(Ware.Planks));
            // Grindstone (turns while working).
            mb.Box(new Vector3(3.5f, 0.35f, -0.4f), new Vector3(0.7f, 0.7f, 0.15f), Wood);
            var grind = Pivot(root, "Grindstone", new Vector3(3.5f, 0.85f, -0.4f));
            Signs.Prim(PrimitiveType.Cylinder, grind, "Stone", Vector3.zero, new Vector3(0.9f, 0.08f, 0.9f), MaterialFactory.Shared(new Color(0.70f, 0.66f, 0.58f)), new Vector3(90f, 0, 0));
            Signs.Prim(PrimitiveType.Cube, grind, "Mark", new Vector3(0, 0.38f, 0), new Vector3(0.12f, 0.12f, 0.2f), MaterialFactory.Shared(DarkWood));
            m.Spinner = grind; m.SpinAxis = Vector3.forward; m.SpinDegPerSec = 200f;
            m.Height = 5.8f;
        }

        // ---------------- huge (half 5.0) ----------------

        private static void Barracks(MeshBuilder mb)
        {
            var c = new Vector3(0, 0, -2.2f);
            House(mb, 8.0f, 3.6f, 3.0f, new Color(0.70f, 0.64f, 0.55f), new Color(0.50f, 0.20f, 0.18f), c, 1.6f, -2.0f);
            // Palisade along the front corners, banner pole, weapon rack, training dummy.
            for (int i = 0; i < 7; i++)
            {
                mb.Box(new Vector3(-4.6f + i * 0.32f, 0.8f, 3.6f), new Vector3(0.26f, 1.6f, 0.26f), Wood);
                mb.Box(new Vector3(2.7f + i * 0.32f, 0.8f, 3.6f), new Vector3(0.26f, 1.6f, 0.26f), Wood);
            }
            mb.Box(new Vector3(4.3f, 3.0f, 1.0f), new Vector3(0.12f, 6.0f, 0.12f), DarkWood);
            mb.Box(new Vector3(4.3f, 5.3f, 1.5f), new Vector3(0.05f, 1.2f, 1.0f), new Color(0.75f, 0.12f, 0.12f));
            mb.Box(new Vector3(-1.0f, 0.9f, 0.6f), new Vector3(1.8f, 0.1f, 0.1f), DarkWood);
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(-1.7f + i * 0.35f, 1.0f, 0.65f), new Vector3(0.05f, 1.9f, 0.05f), W(Ware.Weapons) * 1.3f);
            var dc = new Vector3(1.8f, 0, 1.6f);
            mb.Box(dc + new Vector3(0, 0.9f, 0), new Vector3(0.14f, 1.8f, 0.14f), Wood);
            mb.Box(dc + new Vector3(0, 1.4f, 0), new Vector3(1.0f, 0.12f, 0.12f), Wood);
            mb.Cylinder(dc + new Vector3(0, 1.0f, 0), 0.32f, 0.3f, 0.8f, 8, W(Ware.Wheat) * 0.85f);
            mb.Cylinder(dc + new Vector3(0, 1.85f, 0), 0.2f, 0.2f, 0.35f, 8, W(Ware.Wheat) * 0.85f);
            // Packed-earth drill yard.
            mb.Box(new Vector3(0, 0.02f, 1.8f), new Vector3(9.4f, 0.04f, 3.4f), new Color(0.58f, 0.50f, 0.38f));
        }

        private static void Wonder(MeshBuilder mb)
        {
            var marble = new Color(0.93f, 0.91f, 0.86f);
            mb.Box(new Vector3(0, 0.35f, 0), new Vector3(8.6f, 0.7f, 8.6f), marble * 0.9f);
            mb.Box(new Vector3(0, 1.05f, -0.4f), new Vector3(6.6f, 0.7f, 6.6f), marble * 0.95f);
            mb.Box(new Vector3(0, 1.75f, -0.8f), new Vector3(4.6f, 0.7f, 4.6f), marble);
            // Steps at the front.
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0, 0.12f + i * 0.24f, 4.1f - i * 0.35f), new Vector3(2.2f, 0.24f, 0.4f), marble * 0.85f);
            // Column stumps on the top platform: the wonder is begun.
            foreach (float x in new[] { -1.6f, 1.6f }) foreach (float z in new[] { -2.4f, 0.8f })
                mb.Cylinder(new Vector3(x, 2.1f, z), 0.4f, 0.36f, z < 0 ? 2.6f : 1.2f, 12, marble);
            // A wooden crane at the back corner.
            mb.Box(new Vector3(-4.0f, 4.6f, -4.0f), new Vector3(0.3f, 9.2f, 0.3f), Wood);
            mb.Box(new Vector3(-2.2f, 9.0f, -4.0f), new Vector3(3.8f, 0.25f, 0.25f), Wood);
            mb.Box(new Vector3(-0.6f, 7.7f, -4.0f), new Vector3(0.05f, 2.6f, 0.05f), new Color(0.2f, 0.2f, 0.2f));
            mb.Box(new Vector3(-0.6f, 6.2f, -4.0f), new Vector3(0.7f, 0.5f, 0.7f), marble);
        }

        private static void Storehouse(MeshBuilder mb)
        {
            var c = new Vector3(0, 0, -1.0f);
            House(mb, 8.0f, 5.0f, 3.6f, new Color(0.70f, 0.58f, 0.42f), RoofRed, c, 2.0f);
            mb.Box(c + new Vector3(0, 1.4f, 2.53f), new Vector3(2.6f, 2.8f, 0.08f), DarkWood);
            for (int i = 0; i < 6; i++) Crate(mb, new Vector3(-4.0f + i * 1.4f, 0, 3.4f), 0.8f, W((Ware)(i * 2 % Wares.Count)));
        }
    }
}
