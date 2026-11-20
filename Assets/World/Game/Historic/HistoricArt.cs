using UnityEngine;
using World.Sim;
using World.Sim.Historic;

namespace World.Game
{
    /// <summary>
    /// Original primitive art of the Historic age on the god board (G4-3): the seven Historic buildings, the three castle
    /// stages and their scaffolding, tree stages, field plots, small walkers. Every function adds flat-shaded shapes to a
    /// <see cref="MeshBuilder"/> (local coordinates, ground at y = 0, front is +Z); the views merge them into a few meshes.
    /// Pure view code, no state.
    /// </summary>
    public static class HistoricArt
    {
        public static readonly Color Plank = new Color(0.62f, 0.46f, 0.30f), Timber = new Color(0.36f, 0.25f, 0.15f);
        public static readonly Color Thatch = new Color(0.72f, 0.62f, 0.34f), Brick = new Color(0.66f, 0.30f, 0.20f);
        public static readonly Color Stone = new Color(0.62f, 0.62f, 0.64f), StoneDark = new Color(0.44f, 0.44f, 0.47f);
        public static readonly Color Soil = new Color(0.40f, 0.28f, 0.17f), Dark = new Color(0.14f, 0.12f, 0.11f);

        /// <summary>A quad visible from both sides (roofs and gables).</summary>
        public static void Both(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            mb.Quad(a, b, c, d, col);
            mb.Quad(a, d, c, b, col * 0.85f);
        }

        public static void BothTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            mb.Tri(a, b, c, col);
            mb.Tri(a, c, b, col * 0.85f);
        }

        /// <summary>Walls plus a pitched roof (ridge along local z). <paramref name="yaw"/> turns the whole hut around its centre.</summary>
        public static void Hut(MeshBuilder mb, Vector3 c, float w, float d, float wallH, float roofH, Color wall, Color roof, float yaw = 0f)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => c + q * new Vector3(x, y, z);
            mb.Box(P(0, wallH * 0.5f, 0), new Vector3(w, wallH, d), wall, yaw);
            float ox = w * 0.5f + 0.3f, oz = d * 0.5f + 0.3f;
            var r0 = P(0, wallH + roofH, -oz); var r1 = P(0, wallH + roofH, oz);
            Both(mb, P(-ox, wallH, -oz), r0, r1, P(-ox, wallH, oz), roof);
            Both(mb, r0, P(ox, wallH, -oz), P(ox, wallH, oz), r1, roof * 1.05f);
            BothTri(mb, P(-w * 0.5f, wallH, oz - 0.3f), P(w * 0.5f, wallH, oz - 0.3f), P(0, wallH + roofH, oz - 0.3f), wall * 0.95f);
            BothTri(mb, P(w * 0.5f, wallH, -oz + 0.3f), P(-w * 0.5f, wallH, -oz + 0.3f), P(0, wallH + roofH, -oz + 0.3f), wall * 0.95f);
            mb.Box(P(0, wallH * 0.38f, d * 0.5f + 0.02f), new Vector3(w * 0.28f, wallH * 0.76f, 0.1f), Dark, yaw);
        }

        public static void Banner(MeshBuilder mb, Vector3 foot, float height, Color col, float yaw = 0f)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            mb.Cylinder(foot, 0.1f, 0.08f, height, 5, Timber);
            var a = foot + Vector3.up * height; var b = a + q * new Vector3(1.5f, 0, 0);
            Both(mb, a, b, b + Vector3.down * 1.1f, a + Vector3.down * 1.1f, col);
        }

        public static void Fence(MeshBuilder mb, Vector3 c, float r, int posts, float height, float gapStartDeg, float gapDeg, Color col)
        {
            for (int i = 0; i < posts; i++)
            {
                float a = i * 360f / posts;
                if (Mathf.DeltaAngle(a, gapStartDeg) > -gapDeg && Mathf.DeltaAngle(a, gapStartDeg) < 0f) continue;
                var p = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                mb.Box(p + Vector3.up * height * 0.5f, new Vector3(0.18f, height, 0.18f), col);
                var n = c + Quaternion.Euler(0, a + 360f / posts, 0) * Vector3.forward * r;
                mb.Strip(p + Vector3.up * height * 0.75f, n + Vector3.up * height * 0.75f, 0.12f, col * 0.9f);
            }
        }

        public static void Sheep(MeshBuilder mb, Vector3 p, float yaw, float scale = 1.5f)
            => HexDressing.Sheep(mb, p, yaw, scale, new PropRandom(Mathf.RoundToInt(p.x * 13f + p.z * 7f) & 0xffff));

        public static void LogStack(MeshBuilder mb, Vector3 c, int n, float yaw = 0f)
        {
            for (int i = 0; i < n; i++)
            {
                int row = i < 3 ? 0 : 1, col = i < 3 ? i : i - 3;
                var p = c + Quaternion.Euler(0, yaw, 0) * new Vector3((col - 1) * 0.5f + row * 0.25f, 0.25f + row * 0.4f, 0);
                mb.Box(p, new Vector3(0.42f, 0.42f, 1.8f), Plank * (0.9f + 0.05f * i), yaw);
            }
        }

        // ------------------------------------------------------------ the seven buildings

        /// <summary>A finished Historic building (<paramref name="exhausted"/>: a quarry whose rock is gone).</summary>
        public static void Building(MeshBuilder mb, HistoricKind kind, Color pc, bool exhausted)
        {
            switch (kind)
            {
                case HistoricKind.Woodcutter:
                    Hut(mb, new Vector3(0, 0, -0.8f), 3.2f, 2.8f, 2.0f, 1.2f, Plank, pc);
                    LogStack(mb, new Vector3(2.7f, 0, -0.2f), 5, 90f);
                    mb.Box(new Vector3(-2.4f, 0.55f, 1.2f), new Vector3(0.3f, 1.1f, 1.4f), Timber, 30f); // saw horse
                    mb.Cylinder(new Vector3(-0.5f, 0, 2.3f), 0.45f, 0.38f, 0.5f, 7, Plank * 0.8f); // chopping block
                    mb.Box(new Vector3(-0.5f, 1.1f, 2.3f), new Vector3(0.1f, 1.0f, 0.1f), Timber);
                    break;
                case HistoricKind.Forester:
                    Hut(mb, new Vector3(0, 0, -1.0f), 2.8f, 2.4f, 1.8f, 1.0f, Plank * 0.9f, Thatch);
                    Banner(mb, new Vector3(-2.2f, 0, -1.8f), 3.2f, pc);
                    for (int i = 0; i < 6; i++)
                    {
                        var p = new Vector3(-1.5f + (i % 3) * 1.4f, 0, 1.7f + (i / 3) * 1.2f);
                        mb.Box(p + Vector3.up * 0.04f, new Vector3(1.0f, 0.08f, 0.9f), Soil);
                        mb.Cylinder(p, 0.06f, 0.05f, 0.5f, 4, Timber);
                        mb.Cone(p + Vector3.up * 0.3f, 0.32f, 0.7f, 6, new Color(0.30f, 0.58f, 0.24f));
                    }
                    break;
                case HistoricKind.Quarry:
                    mb.Cylinder(new Vector3(0, -0.02f, 0), 4.2f, 3.8f, 0.04f, 12, exhausted ? new Color(0.50f, 0.47f, 0.42f) : new Color(0.58f, 0.55f, 0.50f));
                    mb.Cylinder(new Vector3(0, 0.02f, 0), 2.6f, 2.2f, 0.04f, 10, new Color(0.40f, 0.38f, 0.36f));
                    Hut(mb, new Vector3(-0.5f, 0, -3.2f), 2.6f, 1.8f, 1.7f, 0.9f, StoneDark * 1.2f, pc);
                    // a small wooden crane and cut blocks
                    mb.Box(new Vector3(2.8f, 1.6f, -0.5f), new Vector3(0.22f, 3.2f, 0.22f), Timber);
                    mb.Box(new Vector3(1.9f, 3.1f, -0.5f), new Vector3(2.0f, 0.2f, 0.2f), Timber, 0f);
                    mb.Box(new Vector3(1.2f, 2.3f, -0.5f), new Vector3(0.05f, 1.6f, 0.05f), Dark);
                    mb.Box(new Vector3(1.2f, 0.5f, -0.5f), new Vector3(0.9f, 0.7f, 0.7f), Stone);
                    mb.Box(new Vector3(3.3f, 0.35f, 1.4f), new Vector3(1.1f, 0.7f, 0.8f), Stone * 0.9f, 20f);
                    mb.Box(new Vector3(-3.0f, 0.3f, 0.8f), new Vector3(0.9f, 0.6f, 0.7f), Stone * 0.95f, -15f);
                    if (exhausted) { mb.Box(new Vector3(0, 0.6f, 2.6f), new Vector3(0.12f, 1.2f, 0.12f), Timber); mb.Box(new Vector3(0, 1.1f, 2.6f), new Vector3(1.0f, 0.5f, 0.08f), Dark); }
                    break;
                case HistoricKind.ClayPit:
                    mb.Cylinder(new Vector3(0.4f, -0.02f, 0.6f), 3.2f, 2.9f, 0.04f, 12, new Color(0.52f, 0.28f, 0.18f));
                    mb.Cylinder(new Vector3(0.4f, 0.02f, 0.6f), 1.9f, 1.6f, 0.04f, 10, new Color(0.40f, 0.20f, 0.13f));
                    Hut(mb, new Vector3(-0.4f, 0, -3.0f), 2.6f, 1.8f, 1.6f, 0.9f, Plank, pc);
                    for (int i = 0; i < 3; i++) mb.Cylinder(new Vector3(3.4f, 0, -1.6f + i * 1.1f), 0.7f, 0.25f, 0.8f, 7, new Color(0.70f, 0.40f, 0.24f));
                    mb.Box(new Vector3(-2.7f, 0.5f, 0.6f), new Vector3(0.8f, 0.5f, 1.2f), Timber, 20f); // a barrow
                    break;
                case HistoricKind.Farm:
                    Hut(mb, new Vector3(0, 0, -1.6f), 4.0f, 3.0f, 2.2f, 1.7f, Plank, Thatch);
                    mb.Box(new Vector3(0, 1.0f, -0.05f), new Vector3(1.3f, 2.0f, 0.12f), Dark);
                    Banner(mb, new Vector3(2.6f, 0, 0.4f), 3.3f, pc);
                    Fence(mb, new Vector3(0, 0, 1.6f), 3.0f, 14, 0.9f, 20f, 40f, Plank);
                    for (int i = 0; i < 4; i++) mb.Cylinder(new Vector3(-1.5f + i * 1.0f, 0, 1.9f), 0.28f, 0.2f, 0.7f, 6, new Color(0.88f, 0.74f, 0.32f));
                    break;
                case HistoricKind.SheepFarm:
                    Hut(mb, new Vector3(0, 0, -2.2f), 3.0f, 2.0f, 1.5f, 1.0f, Plank, Thatch);
                    Banner(mb, new Vector3(-2.4f, 0, -2.2f), 3.2f, pc);
                    Fence(mb, new Vector3(0, 0, 1.0f), 3.6f, 16, 1.0f, 10f, 40f, Plank);
                    for (int i = 0; i < 4; i++) Sheep(mb, new Vector3(-1.6f + (i % 2) * 2.4f, 0, 0.2f + (i / 2) * 1.8f), i * 80f);
                    mb.Box(new Vector3(2.6f, 0.25f, -1.4f), new Vector3(1.2f, 0.5f, 0.5f), Timber); // a hay trough
                    break;
                case HistoricKind.Kiln:
                    mb.Cylinder(Vector3.zero, 2.0f, 1.6f, 2.4f, 10, Brick);
                    mb.Cone(new Vector3(0, 2.4f, 0), 1.65f, 1.0f, 10, Brick * 0.85f);
                    mb.Cylinder(new Vector3(0.9f, 2.6f, -0.6f), 0.4f, 0.3f, 1.8f, 7, StoneDark);
                    mb.Box(new Vector3(0, 0.7f, 1.7f), new Vector3(1.0f, 1.1f, 0.5f), Dark);
                    mb.Box(new Vector3(0, 0.55f, 1.95f), new Vector3(0.6f, 0.5f, 0.1f), new Color(1f, 0.55f, 0.15f));
                    Hut(mb, new Vector3(-3.2f, 0, -0.6f), 1.6f, 1.6f, 1.2f, 0.8f, Plank, pc);
                    for (int i = 0; i < 6; i++) mb.Box(new Vector3(3.0f, 0.2f + (i / 3) * 0.4f, -1f + (i % 3) * 0.5f), new Vector3(0.5f, 0.38f, 0.26f), Brick);
                    break;
            }
        }

        /// <summary>A construction site: ground mark, four poles and planks, rising with <paramref name="progress"/> (0..1).</summary>
        public static void Site(MeshBuilder mb, HistoricKind kind, Color pc, float progress)
        {
            mb.Cylinder(new Vector3(0, -0.02f, 0), 3.3f, 3.1f, 0.05f, 10, Soil * 1.1f);
            float h = 2.6f * Mathf.Lerp(0.5f, 1f, progress);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 1.8f, z = (i < 2 ? -1 : 1) * 1.5f;
                mb.Box(new Vector3(x, h * 0.5f, z), new Vector3(0.18f, h, 0.18f), Timber);
            }
            mb.Box(new Vector3(0, h, -1.5f), new Vector3(3.8f, 0.14f, 0.14f), Timber);
            mb.Box(new Vector3(0, h, 1.5f), new Vector3(3.8f, 0.14f, 0.14f), Timber);
            mb.Box(new Vector3(0, h * 0.55f, -1.5f), new Vector3(3.8f, 0.14f, 0.14f), Timber);
            if (progress > 0.05f) mb.Box(new Vector3(0, 0.15f + progress * 0.9f, 0), new Vector3(3.0f, 0.3f + progress * 1.8f, 2.4f), Plank * 0.9f);
            mb.Box(new Vector3(2.7f, 0.2f, 1.0f), new Vector3(0.8f, 0.4f, 0.5f), Plank);
            Banner(mb, new Vector3(-2.6f, 0, 1.8f), 2.6f, pc);
        }

        // ------------------------------------------------------------ castle (HI-3)

        public const float CastleRadius = 7.2f;
        public static readonly float[] StageHeight = { 4.6f, 8.2f, 12.5f };

        /// <summary>Stage 0, 1 or 2: wood, brick, stone. Local origin = the castle centre, gate at +Z.</summary>
        public static void Castle(MeshBuilder mb, int stage, Color pc)
        {
            mb.Cylinder(new Vector3(0, -0.03f, 0), CastleRadius + 1.2f, CastleRadius + 0.8f, 0.06f, 20, Soil * 1.15f);
            switch (stage)
            {
                case 0: CastleWood(mb, pc); break;
                case 1: CastleBrick(mb, pc); break;
                default: CastleStone(mb, pc); break;
            }
        }

        private static void CastleWood(MeshBuilder mb, Color pc)
        {
            int logs = 26;
            for (int i = 0; i < logs; i++)
            {
                float a = i * 360f / logs;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 16f) continue; // gate at +Z (angle 0 = forward)
                var p = Quaternion.Euler(0, a, 0) * Vector3.forward * 6.4f;
                float h = 3.0f + 0.4f * ((i * 7) % 3);
                mb.Cylinder(p, 0.34f, 0.3f, h, 6, Plank * (0.9f + 0.05f * (i % 3)));
                mb.Cone(p + Vector3.up * h, 0.3f, 0.5f, 6, Plank * 0.8f);
            }
            mb.Box(new Vector3(-1.5f, 2.6f, 6.4f), new Vector3(0.3f, 5.2f, 0.3f), Timber); // gate posts
            mb.Box(new Vector3(1.5f, 2.6f, 6.4f), new Vector3(0.3f, 5.2f, 0.3f), Timber);
            mb.Box(new Vector3(0, 5.0f, 6.4f), new Vector3(3.3f, 0.35f, 0.3f), Timber);
            Hut(mb, new Vector3(0, 0, -1.0f), 5.4f, 3.8f, 2.4f, 1.8f, Plank, Thatch);
            Banner(mb, new Vector3(2.4f, 0, 1.2f), 5.2f, pc);
            // a watch post on four poles
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-4.0f + (i % 2) * 1.6f, 2.2f, 2.2f + (i / 2) * 1.6f), new Vector3(0.2f, 4.4f, 0.2f), Timber);
            mb.Box(new Vector3(-3.2f, 4.5f, 3.0f), new Vector3(2.0f, 0.2f, 2.0f), Plank);
            Hut(mb, new Vector3(-3.2f, 4.5f, 3.0f), 1.6f, 1.6f, 0.0f, 0.9f, Plank, pc);
        }

        private static void CastleBrick(MeshBuilder mb, Color pc)
        {
            WallRing(mb, 5.8f, 3.3f, 0.9f, Brick, true);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 5.8f, z = (i < 2 ? -1 : 1) * 5.8f;
                mb.Box(new Vector3(x, 2.6f, z), new Vector3(2.5f, 5.2f, 2.5f), Brick * 0.95f);
                Hut(mb, new Vector3(x, 5.2f, z), 2.0f, 2.0f, 0.0f, 1.5f, Brick, pc, 0f);
                mb.Box(new Vector3(x, 3.8f, z + (z > 0 ? 1.26f : -1.26f)), new Vector3(0.5f, 0.8f, 0.06f), Dark);
            }
            mb.Box(new Vector3(0, 3.1f, -1.0f), new Vector3(4.4f, 6.2f, 4.0f), Brick * 1.05f);
            Hut(mb, new Vector3(0, 6.2f, -1.0f), 4.4f, 4.0f, 0.0f, 2.2f, Brick, pc);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-1.4f + i * 1.4f, 4.2f, 1.02f), new Vector3(0.5f, 0.9f, 0.06f), Dark);
            Banner(mb, new Vector3(0, 8.4f, -1.0f), 3.2f, pc);
        }

        private static void CastleStone(MeshBuilder mb, Color pc)
        {
            WallRing(mb, 5.8f, 4.2f, 1.0f, Stone, true);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 5.8f, z = (i < 2 ? -1 : 1) * 5.8f;
                mb.Cylinder(new Vector3(x, 0, z), 1.75f, 1.55f, 6.6f, 10, Stone * 1.02f);
                mb.Cylinder(new Vector3(x, 6.6f, z), 2.0f, 2.0f, 0.5f, 10, StoneDark);
                mb.Cone(new Vector3(x, 7.1f, z), 1.9f, 2.6f, 10, pc);
            }
            mb.Box(new Vector3(0, 4.3f, -1.0f), new Vector3(5.0f, 8.6f, 4.6f), Stone * 1.05f);
            for (int i = 0; i < 8; i++)
            {
                float cx = -2.2f + (i % 4) * 1.47f, cz = i < 4 ? -3.2f : 1.2f;
                mb.Box(new Vector3(cx, 9.0f, cz), new Vector3(0.8f, 0.8f, 0.8f), StoneDark);
            }
            mb.Cylinder(new Vector3(0, 8.6f, -1.0f), 1.6f, 1.2f, 1.2f, 8, StoneDark);
            mb.Cone(new Vector3(0, 9.8f, -1.0f), 1.5f, 2.8f, 8, pc);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(-1.5f + i * 1.5f, 5.4f, 1.33f), new Vector3(0.5f, 1.1f, 0.06f), Dark);
            Banner(mb, new Vector3(0, 12.4f, -1.0f), 4.0f, pc);
            mb.Box(new Vector3(0, 1.4f, 5.9f), new Vector3(2.6f, 2.8f, 0.2f), Dark); // gate
        }

        /// <summary>Four curtain walls joining the corner towers, with a gap for the gate at +Z and merlons on top.</summary>
        private static void WallRing(MeshBuilder mb, float half, float height, float thick, Color col, bool merlons)
        {
            for (int side = 0; side < 4; side++)
            {
                var q = Quaternion.Euler(0, side * 90f, 0);
                for (int seg = -2; seg <= 2; seg++)
                {
                    float along = seg * (half * 2f / 5f);
                    if (side == 0 && Mathf.Abs(along) < 1.7f) continue; // the gate side (+Z)
                    var c = q * new Vector3(along, height * 0.5f, half);
                    mb.Box(c, new Vector3(half * 2f / 5f + 0.05f, height, thick), col * (0.95f + 0.03f * ((seg + 2) % 2)), side * 90f);
                    if (merlons) mb.Box(q * new Vector3(along, height + 0.3f, half), new Vector3(half * 2f / 5f * 0.5f, 0.6f, thick * 0.6f), col * 0.85f, side * 90f);
                }
            }
        }

        /// <summary>Scaffolding around a castle stage: poles, rails and a plank deck up to <paramref name="height"/>.</summary>
        public static void Scaffold(MeshBuilder mb, float height)
        {
            const float R = CastleRadius + 0.4f;
            int poles = 10;
            for (int i = 0; i < poles; i++)
            {
                float a = i * 360f / poles;
                var p = Quaternion.Euler(0, a, 0) * Vector3.forward * R;
                mb.Box(p + Vector3.up * height * 0.5f, new Vector3(0.22f, height, 0.22f), Timber);
                var n = Quaternion.Euler(0, a + 360f / poles, 0) * Vector3.forward * R;
                for (int k = 1; k <= 3; k++)
                {
                    float y = height * k / 3f;
                    var m = (p + n) * 0.5f + Vector3.up * y;
                    mb.Box(m, new Vector3(Vector3.Distance(p, n), 0.14f, 0.14f), Plank, Mathf.Atan2(n.x - p.x, n.z - p.z) * Mathf.Rad2Deg - 90f);
                }
            }
            mb.Box(new Vector3(0, height * 0.33f, R + 0.2f), new Vector3(3.2f, 0.18f, 1.6f), Plank * 0.9f);
            mb.Box(new Vector3(R * 0.5f, 0.5f, R * 0.4f), new Vector3(1.2f, 1.0f, 1.2f), Plank); // a crate of tools
        }

        // ------------------------------------------------------------ trees, fields, heaps

        public static void TreeAt(MeshBuilder mb, Vector3 p, TreeStage stage, float stumpLife, PropRandom rnd)
        {
            switch (stage)
            {
                case TreeStage.Sapling:
                    mb.Cylinder(p, 0.1f, 0.07f, 1.0f, 4, Timber);
                    mb.Cone(p + Vector3.up * 0.35f, 0.7f, 1.5f, 5, new Color(0.40f, 0.68f, 0.28f));
                    break;
                case TreeStage.Young:
                    TerrainBuilder.Tree(mb, p, rnd, 0.62f);
                    break;
                case TreeStage.Mature:
                    TerrainBuilder.Tree(mb, p, rnd, 1.25f);
                    break;
                case TreeStage.Stump:
                {
                    float f = Mathf.Clamp01(stumpLife);
                    var wood = Color.Lerp(new Color(0.30f, 0.22f, 0.15f), new Color(0.46f, 0.32f, 0.20f), f);
                    mb.Cylinder(p, 0.8f, 0.62f, 0.5f + 0.5f * f, 6, wood);
                    mb.Cylinder(p + Vector3.up * (0.5f + 0.5f * f), 0.64f, 0.64f, 0.04f, 6, Color.Lerp(wood, new Color(0.85f, 0.72f, 0.5f), f));
                    break;
                }
            }
        }

        public static void FieldPlot(MeshBuilder mb, Vector3 p, FieldStage stage, float yaw)
        {
            mb.Box(p + Vector3.up * 0.06f, new Vector3(9f, 0.12f, 9f), stage == FieldStage.Empty ? Soil : Soil * 0.85f, yaw);
            if (stage == FieldStage.Empty) { for (int i = -3; i <= 3; i += 2) mb.Box(p + Quaternion.Euler(0, yaw, 0) * new Vector3(i * 1.1f, 0.14f, 0), new Vector3(0.3f, 0.06f, 8f), Soil * 1.25f, yaw); return; }
            bool ripe = stage == FieldStage.Ripe;
            var green = new Color(0.40f, 0.62f, 0.22f); var gold = new Color(0.92f, 0.76f, 0.28f);
            for (int i = -3; i <= 3; i++)
                mb.Box(p + Quaternion.Euler(0, yaw, 0) * new Vector3(i * 1.15f, ripe ? 0.5f : 0.25f, 0), new Vector3(0.7f, ripe ? 0.8f : 0.3f, 8f), ripe ? (i % 2 == 0 ? gold : gold * 0.9f) : green, yaw);
        }

        public static void Boulder(MeshBuilder mb, Vector3 p, float size, Color col, PropRandom rnd) => mb.Rock(p, size, col, rnd.Next);

        // ------------------------------------------------------------ people

        public static readonly Color Skin = new Color(0.86f, 0.70f, 0.56f);

        /// <summary>A small walker 1.7 m tall in a people's colour, carrying a load of a colour (alpha 0 = none).</summary>
        public static void Walker(MeshBuilder mb, Color pc, Color load)
        {
            mb.Box(new Vector3(-0.15f, 0.4f, 0), new Vector3(0.22f, 0.8f, 0.26f), Dark * 1.6f);
            mb.Box(new Vector3(0.15f, 0.4f, 0), new Vector3(0.22f, 0.8f, 0.26f), Dark * 1.6f);
            mb.Box(new Vector3(0, 1.15f, 0), new Vector3(0.62f, 0.78f, 0.36f), pc);
            mb.Box(new Vector3(0, 1.72f, 0), new Vector3(0.36f, 0.36f, 0.36f), Skin);
            mb.Box(new Vector3(0, 1.95f, 0), new Vector3(0.42f, 0.12f, 0.42f), pc * 0.7f);
            if (load.a > 0.01f) { load.a = 1f; mb.Box(new Vector3(0, 1.55f, -0.38f), new Vector3(0.55f, 0.5f, 0.45f), load); }
        }

        /// <summary>P1: the walker as a jointed figure. Torso, head, hat and the load (held in front, alpha 0 = none); feet at y = 0, hips at 0.8.</summary>
        public static void WalkerTorso(MeshBuilder mb, Color pc, Color load)
        {
            mb.Box(new Vector3(0, 1.15f, 0), new Vector3(0.62f, 0.78f, 0.36f), pc);
            mb.Box(new Vector3(0, 0.82f, 0), new Vector3(0.56f, 0.14f, 0.34f), pc * 0.7f);
            mb.Box(new Vector3(0, 1.72f, 0), new Vector3(0.36f, 0.36f, 0.36f), Skin);
            mb.Box(new Vector3(0, 1.95f, 0), new Vector3(0.42f, 0.12f, 0.42f), pc * 0.7f);
            if (load.a > 0.01f) { load.a = 1f; mb.Box(new Vector3(0, 1.2f, 0.42f), new Vector3(0.5f, 0.38f, 0.36f), load); }
        }

        /// <summary>One leg hanging from its hip pivot (0.8 long).</summary>
        public static void WalkerLeg(MeshBuilder mb) { mb.Box(new Vector3(0, -0.4f, 0), new Vector3(0.2f, 0.8f, 0.24f), Dark * 1.6f); mb.Box(new Vector3(0, -0.76f, 0.05f), new Vector3(0.22f, 0.08f, 0.32f), Dark); }

        /// <summary>One arm hanging from its shoulder pivot (sleeve in the people's colour, hand).</summary>
        public static void WalkerArm(MeshBuilder mb, Color pc) { mb.Box(new Vector3(0, -0.28f, 0), new Vector3(0.15f, 0.56f, 0.17f), pc * 0.85f); mb.Box(new Vector3(0, -0.6f, 0), new Vector3(0.14f, 0.12f, 0.14f), Skin); }

        public static Color WareColour(Ware w)
        {
            switch (w)
            {
                case Ware.Wood: return new Color(0.50f, 0.34f, 0.18f);
                case Ware.Clay: return new Color(0.72f, 0.38f, 0.22f);
                case Ware.Grain: return new Color(0.92f, 0.76f, 0.26f);
                case Ware.Sheep: return new Color(0.96f, 0.95f, 0.9f);
                case Ware.Stone: return new Color(0.58f, 0.60f, 0.66f);
                case Ware.Bricks: return new Color(0.70f, 0.30f, 0.18f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }
    }
}
