using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Original primitive models of the prehistoric camp (package V), drawn into a <see cref="MeshBuilder"/> in local
    /// space: origin on the ground at the plot centre, +Z = the building's front. Every model fits a 4.5 m radius.
    /// </summary>
    public static class LandShapes
    {
        // ---------------- primitives ----------------

        /// <summary>A prism of <paramref name="sides"/> sides from a to b (logs, poles, beams).</summary>
        public static void Beam(MeshBuilder mb, Vector3 a, Vector3 b, float r, int sides, Color col, bool caps = true)
        {
            var axis = b - a;
            if (axis.sqrMagnitude < 1e-8f) return;
            var dir = axis.normalized;
            var u = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized * r;
            var v = Vector3.Cross(dir, u).normalized * r;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var o0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0);
                var o1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                mb.Quad(a + o0, a + o1, b + o1, b + o0, col * (0.9f + 0.1f * Mathf.Cos(a0)));
                if (caps)
                {
                    mb.Tri(b, b + o0, b + o1, col * 1.05f);
                    mb.Tri(a, a + o1, a + o0, col * 0.85f);
                }
            }
        }

        /// <summary>A vertical additive beam whose colour fades to black (invisible) at the top. Open both ends.</summary>
        public static void FadeBeam(MeshBuilder mb, Vector3 b, float r0, float r1, float h, int sides, Color bottom)
        {
            var top = b + Vector3.up * h;
            int rings = 8;
            for (int k = 0; k < rings; k++)
            {
                float u0 = k / (float)rings, u1 = (k + 1) / (float)rings;
                Color c0 = bottom * (1f - u0) * (1f - u0), c1 = bottom * (1f - u1) * (1f - u1);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    float ra = Mathf.Lerp(r0, r1, u0), rb = Mathf.Lerp(r0, r1, u1);
                    var p00 = Vector3.Lerp(b, top, u0) + d0 * ra; var p01 = Vector3.Lerp(b, top, u0) + d1 * ra;
                    var p10 = Vector3.Lerp(b, top, u1) + d0 * rb; var p11 = Vector3.Lerp(b, top, u1) + d1 * rb;
                    mb.Quad(p00, p10, p11, p01, (c0 + c1) * 0.5f);
                }
            }
        }

        public static void Disc(MeshBuilder mb, Vector3 c, float r, int seg, Color col) => TerrainBuilder.Disc(mb, c, r, seg, col);
        public static void Ring(MeshBuilder mb, Vector3 c, float r, float w, int seg, Color col) => TerrainBuilder.Ring(mb, c, r, w, seg, col);

        /// <summary>Round hut: wattle wall and a thatch cone with a door at +Z.</summary>
        public static void Hut(MeshBuilder mb, Vector3 c, float r, float wall, float roof, Color wallCol, Color roofCol)
        {
            mb.Cylinder(c, r, r * 0.96f, wall, 10, wallCol, false);
            mb.Cone(c + Vector3.up * (wall - 0.15f), r * 1.25f, roof, 10, roofCol);
            mb.Box(c + new Vector3(0, wall * 0.42f, r * 0.97f), new Vector3(0.8f, wall * 0.84f, 0.1f), new Color(0.12f, 0.09f, 0.07f));
            // A few poles poking out of the roof top.
            var top = c + Vector3.up * (wall + roof - 0.25f);
            for (int i = 0; i < 3; i++)
            {
                var d = Quaternion.Euler(0, i * 120f + 20f, 0) * Vector3.forward;
                Beam(mb, top - d * 0.1f, top + d * 0.35f + Vector3.up * 0.6f, 0.04f, 4, LandLook.DarkWood);
            }
        }

        /// <summary>Hide tent: a cone with poles crossing at the top.</summary>
        public static void Tent(MeshBuilder mb, Vector3 c, float r, float h, Color hide)
        {
            mb.Cone(c, r, h, 9, hide);
            mb.Box(c + new Vector3(0, 0.5f, r * 0.8f), new Vector3(0.6f, 1.0f, 0.4f), new Color(0.15f, 0.11f, 0.08f));
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0, i * 90f + 45f, 0) * Vector3.forward;
                Beam(mb, c + d * r * 0.95f, c + Vector3.up * (h + 0.6f) - d * 0.25f, 0.045f, 4, LandLook.DarkWood);
            }
        }

        /// <summary>Logs lying side by side along X, stacked in a pyramid of <paramref name="rows"/>.</summary>
        public static void LogStack(MeshBuilder mb, Vector3 c, float yaw, int rows, float len, float r, Color col)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            for (int row = 0; row < rows; row++)
            {
                int n = rows - row;
                for (int i = 0; i < n; i++)
                {
                    float z = (i - (n - 1) * 0.5f) * r * 2.05f;
                    var p = c + q * new Vector3(0, r + row * r * 1.75f, z);
                    Beam(mb, p - q * Vector3.right * (len * 0.5f), p + q * Vector3.right * (len * 0.5f), r, 6, col * (0.92f + 0.08f * ((i + row) % 2)));
                }
            }
        }

        public static void Basket(MeshBuilder mb, Vector3 c, float r, float h, Color col)
        {
            mb.Cylinder(c, r * 0.8f, r, h, 10, col, false);
            Disc(mb, c + Vector3.up * (h * 0.7f), r * 0.95f, 10, col * 0.6f);
            for (int i = 1; i <= 3; i++) mb.Cylinder(c + Vector3.up * (h * i / 4f - 0.03f), r * (0.8f + 0.2f * i / 4f) + 0.02f, r * (0.8f + 0.2f * i / 4f) + 0.02f, 0.06f, 10, col * 0.75f, false);
        }

        public static void Sheaf(MeshBuilder mb, Vector3 c, float h, Color col)
        {
            mb.Cylinder(c, 0.22f, 0.12f, h * 0.55f, 6, col * 0.9f, false);
            mb.Cylinder(c + Vector3.up * (h * 0.55f), 0.12f, 0.3f, h * 0.45f, 6, col);
            mb.Cylinder(c + Vector3.up * (h * 0.38f), 0.16f, 0.16f, 0.08f, 6, LandLook.DarkWood, false);
        }

        /// <summary>Pennant post in a people's colours (who owns the building).</summary>
        public static void Pennant(MeshBuilder mb, Vector3 p, float h, int faction)
        {
            Beam(mb, p, p + Vector3.up * h, 0.06f, 5, LandLook.DarkWood);
            var col = LandLook.People(faction);
            mb.Tri(p + Vector3.up * h, p + Vector3.up * (h - 0.55f), p + new Vector3(0.9f, h - 0.27f, 0), col);
            mb.Tri(p + Vector3.up * h, p + new Vector3(0.9f, h - 0.27f, 0), p + Vector3.up * (h - 0.55f), col * 0.9f);
        }

        public static void Rack(MeshBuilder mb, Vector3 c, float yaw, float w, float h, Color hang)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var l = c + q * new Vector3(-w / 2, 0, 0); var r = c + q * new Vector3(w / 2, 0, 0);
            Beam(mb, l, l + Vector3.up * h, 0.05f, 4, LandLook.DarkWood);
            Beam(mb, r, r + Vector3.up * h, 0.05f, 4, LandLook.DarkWood);
            Beam(mb, l + Vector3.up * h, r + Vector3.up * h, 0.04f, 4, LandLook.DarkWood);
            int n = Mathf.Max(2, Mathf.RoundToInt(w / 0.35f));
            for (int i = 1; i < n; i++)
            {
                var top = Vector3.Lerp(l, r, i / (float)n) + Vector3.up * (h - 0.05f);
                mb.Box(top - Vector3.up * 0.3f, new Vector3(0.12f, 0.5f, 0.05f), hang, yaw);
            }
        }

        // ---------------- buildings ----------------

        /// <summary>The finished model of a building type (front +Z).</summary>
        public static void Building(MeshBuilder mb, BuildingType type, int faction)
        {
            var wood = LandLook.Wood; var dark = LandLook.DarkWood;
            var rnd = new PropRandom(31 + (int)type * 101);
            switch (type)
            {
                case BuildingType.LogPile:
                    LogStack(mb, new Vector3(-0.4f, 0, -0.6f), 90f, 3, 3.0f, 0.22f, wood);
                    mb.Cylinder(new Vector3(1.9f, 0, 1.2f), 0.4f, 0.38f, 0.55f, 8, wood * 0.9f);
                    Beam(mb, new Vector3(1.75f, 0.55f, 1.2f), new Vector3(2.4f, 0.95f, 1.5f), 0.04f, 4, dark);
                    mb.Box(new Vector3(1.75f, 0.6f, 1.15f), new Vector3(0.12f, 0.22f, 0.3f), LandLook.Stone);
                    for (int i = 0; i < 10; i++) mb.Box(new Vector3(rnd.Range(0.8f, 2.8f), 0.03f, rnd.Range(0.2f, 2.2f)), new Vector3(0.18f, 0.05f, 0.1f), LandLook.Thatch, rnd.Range(0, 180));
                    Pennant(mb, new Vector3(-2.2f, 0, 1.6f), 2.2f, faction);
                    break;
                case BuildingType.ClayScrape:
                    Disc(mb, new Vector3(0, 0.03f, -0.3f), 2.3f, 14, new Color(0.45f, 0.24f, 0.15f));
                    Ring(mb, new Vector3(0, 0.05f, -0.3f), 2.4f, 0.5f, 14, LandLook.ClayRed * 1.05f);
                    for (int i = 0; i < 6; i++) mb.Rock(new Vector3(rnd.Range(-1.6f, 1.6f), 0, rnd.Range(-1.8f, 1.0f)), rnd.Range(0.25f, 0.45f), LandLook.ClayRed, rnd.Next);
                    Beam(mb, new Vector3(2.4f, 0, 1.2f), new Vector3(1.9f, 1.6f, 0.8f), 0.04f, 4, dark);
                    mb.Cylinder(new Vector3(-2.3f, 0, 1.4f), 0.35f, 0.25f, 0.6f, 7, LandLook.Hide);
                    Pennant(mb, new Vector3(2.6f, 0, -2.0f), 2.2f, faction);
                    break;
                case BuildingType.GrainBasket:
                    Basket(mb, new Vector3(0, 0, -0.3f), 0.95f, 1.15f, new Color(0.62f, 0.48f, 0.26f));
                    for (int i = 0; i < 5; i++) Sheaf(mb, new Vector3(-1.9f + i * 0.45f, 0, 1.3f + (i % 2) * 0.3f), 1.1f, new Color(0.88f, 0.74f, 0.32f));
                    mb.Box(new Vector3(1.8f, 0.2f, -0.6f), new Vector3(0.9f, 0.4f, 0.7f), LandLook.Stone);
                    Beam(mb, new Vector3(1.6f, 0.42f, -0.6f), new Vector3(2.1f, 0.45f, -0.5f), 0.04f, 4, dark);
                    Pennant(mb, new Vector3(-2.2f, 0, -1.6f), 2.2f, faction);
                    break;
                case BuildingType.ThornFold:
                    for (int i = 0; i < 13; i++)
                    {
                        float a = (i / 13f) * 300f + 30f; // gap at the front
                        var p = Quaternion.Euler(0, a + 180f, 0) * Vector3.forward * 3.0f;
                        mb.Cone(p, rnd.Range(0.55f, 0.75f), rnd.Range(0.9f, 1.3f), 6, new Color(0.24f, 0.30f, 0.14f) * rnd.Range(0.85f, 1.1f));
                        mb.Rock(p, 0.45f, new Color(0.30f, 0.26f, 0.14f), rnd.Next);
                    }
                    mb.Cylinder(new Vector3(0, 0, -0.8f), 0.9f, 0.4f, 0.6f, 8, LandLook.Thatch * 0.9f);
                    Pennant(mb, new Vector3(1.4f, 0, 3.4f), 2.2f, faction);
                    break;
                case BuildingType.RockHeap:
                    for (int i = 0; i < 14; i++)
                    {
                        float a = rnd.Range(0, 360f), d = rnd.Range(0f, 1.6f);
                        var p = Quaternion.Euler(0, a, 0) * Vector3.forward * d + new Vector3(0, i > 8 ? 0.45f : 0f, -0.4f);
                        mb.Rock(p, rnd.Range(0.35f, 0.65f), LandLook.Stone * rnd.Range(0.85f, 1.1f), rnd.Next);
                    }
                    mb.Rock(new Vector3(2.0f, 0, 1.3f), 0.3f, LandLook.Stone * 0.8f, rnd.Next);
                    Pennant(mb, new Vector3(-2.2f, 0, 1.6f), 2.2f, faction);
                    break;
                case BuildingType.WoodcutterHut:
                    Hut(mb, new Vector3(0, 0, -1.6f), 1.9f, 1.7f, 2.3f, new Color(0.55f, 0.42f, 0.28f), LandLook.Thatch);
                    LogStack(mb, new Vector3(2.4f, 0, 0.6f), 0f, 3, 2.4f, 0.2f, wood);
                    mb.Cylinder(new Vector3(-1.9f, 0, 1.6f), 0.45f, 0.42f, 0.6f, 8, wood * 0.95f);
                    Beam(mb, new Vector3(-1.9f, 0.6f, 1.6f), new Vector3(-1.4f, 1.3f, 2.0f), 0.045f, 4, dark);
                    mb.Box(new Vector3(-1.9f, 0.66f, 1.62f), new Vector3(0.1f, 0.25f, 0.4f), LandLook.Stone * 0.8f);
                    Pennant(mb, new Vector3(1.6f, 0, -3.0f), 3.6f, faction);
                    break;
                case BuildingType.ClayPit:
                    mb.Box(new Vector3(0.6f, 0.02f, 0.4f), new Vector3(3.6f, 0.04f, 2.6f), new Color(0.32f, 0.17f, 0.10f));
                    for (int i = 0; i < 8; i++) mb.Rock(new Vector3(-1.4f + i * 0.55f, 0, -1.2f), 0.5f, LandLook.ClayRed * rnd.Range(0.9f, 1.1f), rnd.Next);
                    Beam(mb, new Vector3(2.5f, 0.0f, 1.6f), new Vector3(1.4f, 0.0f, 0.9f), 0.25f, 4, wood);
                    // Lean-to with drying clay blocks.
                    Beam(mb, new Vector3(-2.6f, 0, 1.0f), new Vector3(-2.6f, 1.9f, 1.0f), 0.06f, 4, dark);
                    Beam(mb, new Vector3(-2.6f, 0, 2.6f), new Vector3(-2.6f, 1.9f, 2.6f), 0.06f, 4, dark);
                    mb.Quad(new Vector3(-2.6f, 1.9f, 0.9f), new Vector3(-2.6f, 1.9f, 2.7f), new Vector3(-1.2f, 1.0f, 2.7f), new Vector3(-1.2f, 1.0f, 0.9f), LandLook.Thatch);
                    mb.Quad(new Vector3(-1.2f, 1.0f, 0.9f), new Vector3(-1.2f, 1.0f, 2.7f), new Vector3(-2.6f, 1.9f, 2.7f), new Vector3(-2.6f, 1.9f, 0.9f), LandLook.Thatch * 0.8f);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-2.0f, 0.15f, 1.1f + i * 0.4f), new Vector3(0.5f, 0.3f, 0.3f), LandLook.ClayRed * 1.1f);
                    Pennant(mb, new Vector3(2.8f, 0, -1.8f), 3.2f, faction);
                    break;
                case BuildingType.Farm:
                    Hut(mb, new Vector3(-1.4f, 0, -1.6f), 1.6f, 1.6f, 2.1f, new Color(0.55f, 0.42f, 0.28f), LandLook.Thatch);
                    // Granary on stilts.
                    for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(2.0f + (i % 2) * 1.2f, 0, -2.0f + (i / 2) * 1.2f);
                        Beam(mb, p, p + Vector3.up * 1.0f, 0.06f, 4, dark);
                    }
                    mb.Cylinder(new Vector3(2.6f, 1.0f, -1.4f), 0.85f, 0.85f, 1.0f, 8, new Color(0.62f, 0.48f, 0.26f));
                    mb.Cone(new Vector3(2.6f, 1.95f, -1.4f), 1.05f, 0.9f, 8, LandLook.Thatch);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.2f + i * 0.9f, 0.08f, 2.0f), new Vector3(0.35f, 0.16f, 2.2f), LandLook.Earth * 1.2f);
                    for (int i = 0; i < 4; i++) for (int k = 0; k < 4; k++) mb.Cone(new Vector3(-0.2f + i * 0.9f, 0.1f, 1.2f + k * 0.55f), 0.12f, 0.4f, 4, new Color(0.45f, 0.62f, 0.25f));
                    Pennant(mb, new Vector3(-3.2f, 0, 0.6f), 3.2f, faction);
                    break;
                case BuildingType.ShepherdHut:
                    Hut(mb, new Vector3(-1.6f, 0, -1.4f), 1.6f, 1.6f, 2.1f, new Color(0.55f, 0.42f, 0.28f), LandLook.Hide * 0.95f);
                    {
                        var c = new Vector3(1.8f, 0, 0.6f); float hw = 1.6f, hd = 2.0f;
                        Vector3[] pts = { c + new Vector3(-hw, 0, -hd), c + new Vector3(hw, 0, -hd), c + new Vector3(hw, 0, hd), c + new Vector3(-hw, 0, hd) };
                        for (int k = 0; k < 4; k++)
                        {
                            var a = pts[k]; var b = pts[(k + 1) % 4];
                            for (int s = 0; s <= 4; s++) { var p = Vector3.Lerp(a, b, s / 4f); Beam(mb, p, p + Vector3.up * 1.0f, 0.05f, 4, dark); }
                            if (k == 3) continue; // gate side
                            Beam(mb, a + Vector3.up * 0.45f, b + Vector3.up * 0.45f, 0.04f, 4, wood);
                            Beam(mb, a + Vector3.up * 0.85f, b + Vector3.up * 0.85f, 0.04f, 4, wood);
                        }
                    }
                    Beam(mb, new Vector3(-0.2f, 0, 1.8f), new Vector3(0.1f, 1.7f, 2.0f), 0.04f, 4, dark); // crook
                    Pennant(mb, new Vector3(-3.0f, 0, 1.0f), 3.2f, faction);
                    break;
                case BuildingType.Quarry:
                    for (int tier = 0; tier < 3; tier++)
                        mb.Box(new Vector3(0, 0.5f + tier * 0.9f, -1.6f - tier * 0.5f), new Vector3(4.2f - tier * 0.8f, 1.0f + tier * 0.0f, 1.4f), LandLook.Stone * (0.85f + tier * 0.07f));
                    for (int i = 0; i < 5; i++) mb.Box(new Vector3(-1.6f + i * 0.8f, 0.25f, 0.6f + (i % 2) * 0.5f), new Vector3(0.6f, 0.5f, 0.5f), LandLook.Stone * 1.08f, i * 13f);
                    // Tripod.
                    var apex = new Vector3(1.8f, 3.0f, 1.2f);
                    for (int i = 0; i < 3; i++) Beam(mb, apex + Quaternion.Euler(0, i * 120f, 0) * Vector3.forward * 1.2f - Vector3.up * 3.0f, apex, 0.06f, 4, dark);
                    Beam(mb, apex, apex - Vector3.up * 1.6f, 0.015f, 3, LandLook.Thatch);
                    mb.Box(apex - Vector3.up * 1.8f, new Vector3(0.4f, 0.35f, 0.4f), LandLook.Stone);
                    Pennant(mb, new Vector3(-2.6f, 0, 1.4f), 3.2f, faction);
                    break;
                case BuildingType.Sawmill:
                {
                    // Saw pit: two trestles with a log across, a long hide shelter over it.
                    foreach (float x in new[] { -1.4f, 1.4f })
                    {
                        Beam(mb, new Vector3(x - 0.5f, 0, -0.4f), new Vector3(x, 1.2f, -0.4f), 0.05f, 4, dark);
                        Beam(mb, new Vector3(x + 0.5f, 0, -0.4f), new Vector3(x, 1.2f, -0.4f), 0.05f, 4, dark);
                    }
                    Beam(mb, new Vector3(-2.4f, 1.3f, -0.4f), new Vector3(2.4f, 1.3f, -0.4f), 0.25f, 7, wood);
                    mb.Box(new Vector3(0.3f, 1.65f, -0.4f), new Vector3(0.04f, 0.9f, 0.5f), LandLook.Stone * 0.7f);
                    foreach (float x in new[] { -2.6f, 2.6f }) foreach (float z in new[] { -1.8f, 1.0f }) Beam(mb, new Vector3(x, 0, z), new Vector3(x, 2.4f, z), 0.06f, 4, dark);
                    mb.Quad(new Vector3(-2.8f, 2.4f, -2.0f), new Vector3(-2.8f, 2.9f, -0.4f), new Vector3(2.8f, 2.9f, -0.4f), new Vector3(2.8f, 2.4f, -2.0f), LandLook.Hide);
                    mb.Quad(new Vector3(-2.8f, 2.9f, -0.4f), new Vector3(-2.8f, 2.4f, 1.2f), new Vector3(2.8f, 2.4f, 1.2f), new Vector3(2.8f, 2.9f, -0.4f), LandLook.Hide * 0.92f);
                    mb.Quad(new Vector3(2.8f, 2.4f, -2.0f), new Vector3(2.8f, 2.9f, -0.4f), new Vector3(-2.8f, 2.9f, -0.4f), new Vector3(-2.8f, 2.4f, -2.0f), LandLook.Hide * 0.7f);
                    mb.Quad(new Vector3(2.8f, 2.9f, -0.4f), new Vector3(2.8f, 2.4f, 1.2f), new Vector3(-2.8f, 2.4f, 1.2f), new Vector3(-2.8f, 2.9f, -0.4f), LandLook.Hide * 0.7f);
                    for (int i = 0; i < 5; i++) mb.Box(new Vector3(0, 0.08f + i * 0.13f, 2.2f), new Vector3(2.6f, 0.11f, 0.7f), WareColorRaw(Ware.Planks) * (0.95f + 0.05f * (i % 2)));
                    Pennant(mb, new Vector3(-3.2f, 0, 2.2f), 3.6f, faction);
                    break;
                }
                case BuildingType.Kiln:
                    mb.Cylinder(new Vector3(0, 0, -0.8f), 1.9f, 1.5f, 1.3f, 12, LandLook.ClayRed);
                    mb.Cylinder(new Vector3(0, 1.3f, -0.8f), 1.5f, 0.6f, 0.9f, 12, LandLook.ClayRed * 0.95f);
                    mb.Cylinder(new Vector3(0, 2.2f, -0.8f), 0.45f, 0.35f, 1.0f, 8, LandLook.ClayRed * 0.85f);
                    mb.Box(new Vector3(0, 0.45f, 1.05f), new Vector3(0.9f, 0.9f, 0.2f), new Color(0.12f, 0.06f, 0.04f));
                    for (int i = 0; i < 3; i++) for (int k = 0; k < 3 - i; k++) mb.Box(new Vector3(2.4f, 0.13f + i * 0.26f, -1.0f + k * 0.55f + i * 0.27f), new Vector3(0.5f, 0.25f, 0.5f), WareColorRaw(Ware.Bricks));
                    LogStack(mb, new Vector3(-2.5f, 0, 1.6f), 0, 2, 1.8f, 0.15f, wood);
                    Pennant(mb, new Vector3(-2.8f, 0, -2.2f), 3.6f, faction);
                    break;
                case BuildingType.Bakery:
                    mb.Cylinder(new Vector3(-0.6f, 0, -1.0f), 1.4f, 1.2f, 0.8f, 12, LandLook.ClayRed * 1.05f);
                    mb.Cone(new Vector3(-0.6f, 0.8f, -1.0f), 1.25f, 1.0f, 12, LandLook.ClayRed);
                    mb.Box(new Vector3(-0.6f, 0.4f, 0.3f), new Vector3(0.6f, 0.5f, 0.2f), new Color(0.12f, 0.06f, 0.04f));
                    foreach (float x in new[] { -2.6f, 1.6f }) foreach (float z in new[] { -2.6f, 0.8f }) Beam(mb, new Vector3(x, 0, z), new Vector3(x, 2.4f, z), 0.06f, 4, dark);
                    mb.Quad(new Vector3(-2.8f, 2.5f, -2.8f), new Vector3(-2.8f, 2.2f, 1.0f), new Vector3(1.8f, 2.2f, 1.0f), new Vector3(1.8f, 2.5f, -2.8f), LandLook.Thatch);
                    mb.Quad(new Vector3(1.8f, 2.5f, -2.8f), new Vector3(1.8f, 2.2f, 1.0f), new Vector3(-2.8f, 2.2f, 1.0f), new Vector3(-2.8f, 2.5f, -2.8f), LandLook.Thatch * 0.7f);
                    mb.Cylinder(new Vector3(2.5f, 0, 1.6f), 0.55f, 0.55f, 0.25f, 10, LandLook.Stone);
                    mb.Cylinder(new Vector3(2.5f, 0.25f, 1.6f), 0.45f, 0.45f, 0.15f, 10, LandLook.Stone * 0.85f);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(-2.0f + i * 0.5f, 0.12f, 2.0f), new Vector3(0.4f, 0.22f, 0.3f), WareColorRaw(Ware.Bread));
                    Pennant(mb, new Vector3(3.0f, 0, -1.6f), 3.6f, faction);
                    break;
                case BuildingType.Smokehouse:
                    Tent(mb, new Vector3(-0.6f, 0, -1.0f), 1.9f, 3.6f, LandLook.Hide * 0.85f);
                    Rack(mb, new Vector3(2.0f, 0, 0.0f), 90f, 2.6f, 1.7f, WareColorRaw(Ware.Meat));
                    Rack(mb, new Vector3(-1.2f, 0, 2.2f), 0f, 2.4f, 1.6f, WareColorRaw(Ware.Meat));
                    Pennant(mb, new Vector3(2.8f, 0, -2.4f), 3.6f, faction);
                    break;
                case BuildingType.Stonecutter:
                    mb.Box(new Vector3(0, 0.45f, -0.6f), new Vector3(2.6f, 0.15f, 1.0f), wood);
                    foreach (float x in new[] { -1.1f, 1.1f }) mb.Box(new Vector3(x, 0.2f, -0.6f), new Vector3(0.25f, 0.4f, 0.9f), dark);
                    mb.Box(new Vector3(-0.3f, 0.75f, -0.6f), new Vector3(0.7f, 0.45f, 0.6f), WareColorRaw(Ware.CutStone));
                    for (int i = 0; i < 2; i++) for (int k = 0; k < 3; k++) mb.Box(new Vector3(2.4f, 0.25f + i * 0.5f, -1.4f + k * 0.65f), new Vector3(0.6f, 0.48f, 0.6f), WareColorRaw(Ware.CutStone) * (0.95f + 0.05f * k));
                    for (int i = 0; i < 5; i++) mb.Rock(new Vector3(-2.4f + rnd.Range(-0.6f, 0.6f), 0, 1.4f + rnd.Range(-0.6f, 0.6f)), rnd.Range(0.35f, 0.6f), LandLook.Stone, rnd.Next);
                    Hut(mb, new Vector3(-1.2f, 0, -3.0f), 1.0f, 1.3f, 1.5f, new Color(0.55f, 0.42f, 0.28f), LandLook.Thatch);
                    Pennant(mb, new Vector3(2.9f, 0, 1.8f), 3.6f, faction);
                    break;
                case BuildingType.WarriorLodge:
                {
                    // Longhouse: low walls, steep A roof, crossed gable ends.
                    float hw = 1.7f, hl = 3.2f;
                    var wallCol = new Color(0.50f, 0.38f, 0.25f);
                    mb.Box(new Vector3(0, 0.75f, -1.2f), new Vector3(hw * 2, 1.5f, hl * 2 * 0.55f), wallCol, 90f);
                    var a0 = new Vector3(-hl * 0.6f, 1.4f, -1.2f - hw - 0.3f); var a1 = new Vector3(hl * 0.6f, 1.4f, -1.2f - hw - 0.3f);
                    var r0 = new Vector3(-hl * 0.6f, 3.6f, -1.2f); var r1 = new Vector3(hl * 0.6f, 3.6f, -1.2f);
                    var b0 = new Vector3(-hl * 0.6f, 1.4f, -1.2f + hw + 0.3f); var b1 = new Vector3(hl * 0.6f, 1.4f, -1.2f + hw + 0.3f);
                    mb.Quad(a0, r0, r1, a1, LandLook.Thatch * 0.85f);
                    mb.Quad(b1, r1, r0, b0, LandLook.Thatch);
                    mb.Tri(a0, b0, r0, wallCol * 0.9f); mb.Tri(a1, r1, b1, wallCol * 0.9f);
                    Beam(mb, r0 + new Vector3(-0.2f, -0.2f, -0.5f), r0 + new Vector3(-0.2f, 0.7f, 0.5f), 0.05f, 4, dark);
                    Beam(mb, r0 + new Vector3(-0.2f, -0.2f, 0.5f), r0 + new Vector3(-0.2f, 0.7f, -0.5f), 0.05f, 4, dark);
                    mb.Box(new Vector3(0, 0.6f, -1.2f + hw + 0.01f), new Vector3(0.8f, 1.2f, 0.06f), new Color(0.12f, 0.09f, 0.07f));
                    // Spear rack and shields in the people's colour.
                    Beam(mb, new Vector3(2.6f, 1.2f, 0.4f), new Vector3(2.6f, 1.2f, 2.4f), 0.04f, 4, dark);
                    for (int i = 0; i < 5; i++)
                    {
                        var p = new Vector3(2.55f, 0, 0.6f + i * 0.4f);
                        Beam(mb, p, p + new Vector3(0.15f, 2.3f, 0), 0.03f, 4, wood);
                        mb.Cone(p + new Vector3(0.15f, 2.3f, 0), 0.06f, 0.3f, 4, LandLook.Stone);
                    }
                    var pc = LandLook.People(faction); var tc = LandLook.PeopleTrim(faction);
                    for (int i = 0; i < 3; i++)
                    {
                        var c = new Vector3(-1.2f + i * 1.2f, 0.9f, -1.2f + hw + 0.12f);
                        mb.Cylinder(c - Vector3.forward * 0.04f, 0.42f, 0.42f, 0.0f, 10, pc);
                        mb.Box(c, new Vector3(0.6f, 0.6f, 0.06f), pc);
                        mb.Box(c + Vector3.forward * 0.04f, new Vector3(0.18f, 0.18f, 0.04f), tc);
                    }
                    Pennant(mb, new Vector3(-2.8f, 0, 0.8f), 4.2f, faction);
                    break;
                }
            }
        }

        /// <summary>Raw ware colour (for meshes; same as <see cref="LandLook.WareColor"/>).</summary>
        public static Color WareColorRaw(Ware w) => LandLook.WareColor(w);

        /// <summary>Approximate height of a finished model (for labels, scaffolds and pick boxes).</summary>
        public static float Height(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.LogPile: case BuildingType.ClayScrape: case BuildingType.GrainBasket:
                case BuildingType.ThornFold: case BuildingType.RockHeap: return 2.3f;
                case BuildingType.WarriorLodge: case BuildingType.Smokehouse: return 4.2f;
                default: return 3.8f;
            }
        }

        // ---------------- people ----------------

        /// <summary>A standing person (1.75 m) in a tunic of the given colour, facing +Z. Arms hang down.</summary>
        public static void Person(MeshBuilder mb, Color tunic, Color trim, bool warrior = false, bool demon = false)
        {
            var skin = demon ? new Color(0.38f, 0.07f, 0.09f) : LandLook.Skin;
            var leg = demon ? new Color(0.10f, 0.04f, 0.05f) : new Color(0.38f, 0.27f, 0.18f);
            if (demon)
            {
                mb.Box(new Vector3(-0.12f, 0.42f, 0), new Vector3(0.15f, 0.84f, 0.18f), leg);
                mb.Box(new Vector3(0.12f, 0.42f, 0), new Vector3(0.15f, 0.84f, 0.18f), leg);
                mb.Box(new Vector3(0, 1.08f, 0), new Vector3(0.48f, 0.56f, 0.28f), tunic);
                mb.Box(new Vector3(0, 0.82f, 0), new Vector3(0.52f, 0.08f, 0.3f), trim);
            }
            else
            {
                // Prehistoric dress: bare legs in fur boots, a hide wrap dyed in the people's colour, a fur kilt and
                // mantle, a sash in the people's trim.
                var hide = Color.Lerp(new Color(0.52f, 0.38f, 0.24f), tunic, 0.5f);
                var fur = new Color(0.34f, 0.25f, 0.16f);
                mb.Box(new Vector3(-0.12f, 0.42f, 0), new Vector3(0.14f, 0.84f, 0.16f), skin);
                mb.Box(new Vector3(0.12f, 0.42f, 0), new Vector3(0.14f, 0.84f, 0.16f), skin);
                mb.Box(new Vector3(-0.12f, 0.13f, 0.02f), new Vector3(0.18f, 0.26f, 0.24f), fur);
                mb.Box(new Vector3(0.12f, 0.13f, 0.02f), new Vector3(0.18f, 0.26f, 0.24f), fur);
                mb.Box(new Vector3(0, 1.10f, 0), new Vector3(0.46f, 0.52f, 0.27f), hide);
                mb.Box(new Vector3(0, 0.72f, 0), new Vector3(0.54f, 0.30f, 0.32f), fur);
                mb.Box(new Vector3(0, 0.88f, 0), new Vector3(0.52f, 0.07f, 0.3f), trim);
                mb.Box(new Vector3(-0.12f, 1.33f, 0), new Vector3(0.36f, 0.14f, 0.34f), fur);
            }
            mb.Box(new Vector3(-0.31f, 1.05f, 0), new Vector3(0.12f, 0.55f, 0.14f), skin);
            mb.Box(new Vector3(0.31f, 1.05f, 0), new Vector3(0.12f, 0.55f, 0.14f), skin);
            mb.Box(new Vector3(0, 1.5f, 0), new Vector3(0.26f, 0.3f, 0.26f), skin);
            if (demon)
            {
                mb.Cone(new Vector3(-0.1f, 1.62f, 0), 0.06f, 0.35f, 4, new Color(0.85f, 0.80f, 0.70f));
                mb.Cone(new Vector3(0.1f, 1.62f, 0), 0.06f, 0.35f, 4, new Color(0.85f, 0.80f, 0.70f));
            }
            else mb.Box(new Vector3(0, 1.68f, -0.03f), new Vector3(0.28f, 0.1f, 0.3f), LandLook.Hair);
            if (warrior)
            {
                mb.Box(new Vector3(0, 1.72f, 0), new Vector3(0.3f, 0.08f, 0.3f), trim);
                mb.Cone(new Vector3(0, 1.76f, 0), 0.1f, 0.35f, 4, tunic);
            }
        }

        /// <summary>Glowing eyes of a demon (unlit mesh).</summary>
        public static void DemonEyes(MeshBuilder mb)
        {
            mb.Box(new Vector3(-0.06f, 1.53f, 0.14f), new Vector3(0.06f, 0.05f, 0.02f), new Color(1f, 0.25f, 0.1f));
            mb.Box(new Vector3(0.06f, 1.53f, 0.14f), new Vector3(0.06f, 0.05f, 0.02f), new Color(1f, 0.25f, 0.1f));
        }

        /// <summary>Flames (unlit): three nested cones.</summary>
        public static void Flames(MeshBuilder mb, Vector3 c, float r, float h)
        {
            mb.Cone(c, r, h, 7, new Color(0.95f, 0.35f, 0.08f));
            mb.Cone(c + Vector3.up * 0.02f, r * 0.65f, h * 1.15f, 6, new Color(1f, 0.62f, 0.12f));
            mb.Cone(c + Vector3.up * 0.04f, r * 0.3f, h * 1.25f, 5, new Color(1f, 0.92f, 0.45f));
        }
    }
}
