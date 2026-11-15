using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// The Historic-grade models of the 16 building types (PRD_AGES H1-5), built from the same flat-shaded primitives as
    /// <see cref="LandShapes.Building"/>. Every model keeps its type's identity and stays inside the 4.5 m plot radius
    /// (<see cref="Radius"/>); the front is +Z, the door torch stands near (2.9, 0, 3.5) and the lodge's warriors stand
    /// at z 3.3 and beyond, so the front stays open. Roofs are pitched (tile, shingle or aged thatch), walls timber or
    /// plaster on a stone footing, yards are fenced.
    /// </summary>
    public static class HistoricShapes
    {
        /// <summary>Metres from the plot centre inside which every model stays (the door torch stands just outside the walls).</summary>
        public const float Radius = 4.6f;

        static readonly Color Plank = new Color(0.62f, 0.46f, 0.30f), Plaster = new Color(0.84f, 0.78f, 0.64f), Foot = new Color(0.58f, 0.57f, 0.55f);
        static readonly Color Tile = new Color(0.62f, 0.30f, 0.20f), Shingle = new Color(0.42f, 0.32f, 0.22f), OldThatch = new Color(0.70f, 0.60f, 0.34f);
        static readonly Color DarkDoor = new Color(0.20f, 0.13f, 0.08f), Timber = new Color(0.34f, 0.23f, 0.14f);

        public static void Build(MeshBuilder mb, BuildingType type, int faction)
        {
            var wood = LandLook.Wood; var rnd = new PropRandom(77 + (int)type * 13);
            switch (type)
            {
                case BuildingType.LogPile:
                    Hall(mb, new Vector3(-0.5f, 0, -1.4f), 0, 1.8f, 1.3f, 1.5f, 1.1f, Plank, Shingle, true);
                    Chimney(mb, new Vector3(-1.5f, 1.6f, -1.8f), 1.7f);
                    Lean(mb, new Vector3(2.4f, 0, -1.2f), 90f, 1.9f, 1.4f, 1.9f, 1.3f, Shingle);
                    LandShapes.LogStack(mb, new Vector3(2.4f, 0, -1.2f), 90f, 3, 1.6f, 0.2f, wood);
                    Stump(mb, new Vector3(-2.2f, 0, 1.4f));
                    Sawhorse(mb, new Vector3(0.3f, 0, 1.2f), 10f);
                    Chips(mb, rnd, 0.0f, 1.6f, 1.5f, 1.2f);
                    LandShapes.Pennant(mb, new Vector3(-3.2f, 0, -0.2f), 3.0f, faction);
                    break;
                case BuildingType.ClayScrape:
                    LandShapes.Disc(mb, new Vector3(0.3f, 0.03f, 0.2f), 1.9f, 14, new Color(0.45f, 0.24f, 0.15f));
                    for (int i = 0; i < 14; i++) { float a = i * 360f / 14f; mb.Box(new Vector3(0.3f, 0.12f, 0.2f) + Quaternion.Euler(0, a, 0) * Vector3.forward * 2.05f, new Vector3(0.55f, 0.24f, 0.4f), Foot * (0.9f + 0.1f * (i % 2)), a); }
                    for (int i = 0; i < 5; i++) mb.Box(new Vector3(-1.0f + i * 0.5f, 0.07f, 0.2f), new Vector3(0.42f, 0.05f, 2.6f), wood * 0.9f);
                    Hall(mb, new Vector3(0f, 0, -2.8f), 0, 1.5f, 0.8f, 1.4f, 0.9f, Plank, Shingle, true);
                    Bricks(mb, new Vector3(3.0f, 0, -0.9f), 3, 4, 0.34f, WareC(Ware.Bricks));
                    Barrow(mb, new Vector3(-2.9f, 0, 1.7f), 30f);
                    LandShapes.Pennant(mb, new Vector3(2.5f, 0, -2.2f), 3.0f, faction);
                    break;
                case BuildingType.GrainBasket:
                    FenceRect(mb, -3.0f, 3.0f, -3.2f, 2.5f, 1.0f, 0.8f, 0f);
                    Hall(mb, new Vector3(0, 0, -2.0f), 0, 1.9f, 0.95f, 1.5f, 1.0f, Plank, OldThatch, true);
                    LandShapes.Basket(mb, new Vector3(0.0f, 0, 0.0f), 0.95f, 1.15f, new Color(0.62f, 0.48f, 0.26f));
                    for (int i = 0; i < 6; i++) LandShapes.Sheaf(mb, new Vector3(-2.3f + i * 0.45f, 0, 0.9f + (i % 2) * 0.35f), 1.1f, new Color(0.88f, 0.74f, 0.32f));
                    mb.Box(new Vector3(1.9f, 0.2f, -0.1f), new Vector3(1.0f, 0.4f, 0.8f), Foot);
                    LandShapes.Pennant(mb, new Vector3(-3.6f, 0, -0.6f), 3.0f, faction);
                    break;
                case BuildingType.ThornFold:
                    FenceRing(mb, Vector3.zero, 3.2f, 16, 180f, 42f, 1.1f);
                    Hall(mb, new Vector3(0, 0, -1.6f), 0, 1.4f, 0.8f, 1.3f, 0.8f, Plank, OldThatch, false);
                    mb.Cone(new Vector3(1.4f, 0, 0.1f), 0.9f, 1.2f, 8, LandLook.Thatch);
                    mb.Box(new Vector3(-1.5f, 0.3f, 0.6f), new Vector3(1.4f, 0.25f, 0.45f), wood); mb.Box(new Vector3(-1.5f, 0.05f, 0.6f), new Vector3(1.2f, 0.1f, 0.3f), LandLook.Stone);
                    LandShapes.Pennant(mb, new Vector3(1.6f, 0, 3.6f), 3.0f, faction);
                    break;
                case BuildingType.RockHeap:
                    Wall(mb, new Vector3(-2.8f, 0, -2.8f), new Vector3(2.8f, 0, -2.8f), 0.7f);
                    Wall(mb, new Vector3(-2.8f, 0, -2.8f), new Vector3(-2.8f, 0, 1.2f), 0.7f);
                    Wall(mb, new Vector3(2.8f, 0, -2.8f), new Vector3(2.8f, 0, 0.0f), 0.7f);
                    for (int i = 0; i < 14; i++) { float a = rnd.Range(0, 360f), d = rnd.Range(0f, 1.4f); mb.Rock(Quaternion.Euler(0, a, 0) * Vector3.forward * d + new Vector3(0, i > 8 ? 0.5f : 0f, -1.3f), rnd.Range(0.35f, 0.6f), LandLook.Stone * rnd.Range(0.85f, 1.1f), rnd.Next); }
                    Bricks(mb, new Vector3(-2.2f, 0, -0.5f), 3, 3, 0.45f, LandLook.Stone * 1.1f);
                    Crane(mb, new Vector3(1.9f, 0, -0.9f), 3.0f, -20f);
                    LandShapes.Pennant(mb, new Vector3(-3.4f, 0, 1.8f), 3.0f, faction);
                    break;
                case BuildingType.WoodcutterHut:
                    Hall(mb, new Vector3(-0.6f, 0, -1.5f), 0, 1.9f, 1.4f, 1.7f, 1.2f, Plank, Shingle, true);
                    Chimney(mb, new Vector3(-1.7f, 1.8f, -1.9f), 1.9f);
                    Lean(mb, new Vector3(2.5f, 0, -1.0f), 90f, 2.0f, 1.4f, 1.9f, 1.3f, Shingle);
                    LandShapes.LogStack(mb, new Vector3(2.5f, 0, -1.0f), 90f, 3, 1.8f, 0.2f, wood);
                    FenceLine(mb, new Vector3(-3.4f, 0, 0.3f), new Vector3(-0.8f, 0, 2.2f), 0.9f);
                    Stump(mb, new Vector3(-2.3f, 0, 2.0f));
                    LandShapes.LogStack(mb, new Vector3(-0.2f, 0, 1.6f), 0f, 2, 1.6f, 0.17f, wood);
                    LandShapes.Pennant(mb, new Vector3(0.9f, 0, -3.2f), 3.6f, faction);
                    break;
                case BuildingType.ClayPit:
                    mb.Box(new Vector3(0.3f, 0.02f, 0.3f), new Vector3(3.2f, 0.04f, 2.4f), new Color(0.32f, 0.17f, 0.10f));
                    for (int i = 0; i < 9; i++) { mb.Box(new Vector3(-1.5f + i * 0.4f, 0.15f, -0.95f), new Vector3(0.38f, 0.3f, 0.4f), Foot * (0.92f + 0.08f * (i % 2))); mb.Box(new Vector3(-1.5f + i * 0.4f, 0.15f, 1.55f), new Vector3(0.38f, 0.3f, 0.4f), Foot * (0.92f + 0.08f * (i % 2))); }
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-1.2f + i * 0.9f, 0.07f, 0.3f), new Vector3(0.1f, 0.05f, 2.2f), wood);
                    Hall(mb, new Vector3(-0.4f, 0, -2.7f), 0, 1.7f, 0.8f, 1.5f, 0.9f, Plank, Shingle, true);
                    Barrow(mb, new Vector3(2.7f, 0, 0.0f), -60f);
                    Bricks(mb, new Vector3(-3.1f, 0, 0.5f), 3, 2, 0.4f, LandLook.ClayRed);
                    LandShapes.Pennant(mb, new Vector3(2.8f, 0, -2.0f), 3.2f, faction);
                    break;
                case BuildingType.Farm:
                    Hall(mb, new Vector3(-1.4f, 0, -1.9f), 0, 1.4f, 1.1f, 1.5f, 1.1f, Plaster, Tile, true);
                    Chimney(mb, new Vector3(-2.1f, 1.7f, -2.1f), 1.8f);
                    Hall(mb, new Vector3(1.6f, 0, -1.8f), 0, 1.1f, 1.1f, 1.8f, 1.2f, Plank, Shingle, true);
                    FenceRect(mb, -3.2f, 2.4f, -0.2f, 2.5f, 0.9f, 0.9f, 0f);
                    for (int i = 0; i < 6; i++) { mb.Box(new Vector3(-2.4f + i * 0.9f, 0.08f, 1.3f), new Vector3(0.35f, 0.16f, 2.0f), LandLook.Earth * 1.2f); for (int k = 0; k < 4; k++) mb.Cone(new Vector3(-2.4f + i * 0.9f, 0.1f, 0.5f + k * 0.55f), 0.12f, 0.4f, 4, new Color(0.45f, 0.62f, 0.25f)); }
                    LandShapes.Pennant(mb, new Vector3(-3.6f, 0, -0.3f), 3.4f, faction);
                    break;
                case BuildingType.ShepherdHut:
                    Hall(mb, new Vector3(-1.7f, 0, -1.6f), 0, 1.3f, 1.1f, 1.4f, 1.0f, Plank, OldThatch, true);
                    Chimney(mb, new Vector3(-2.3f, 1.6f, -1.9f), 1.7f);
                    FenceRect(mb, 0.5f, 3.4f, -2.2f, 2.3f, 1.0f, 0.8f, 0f);
                    mb.Box(new Vector3(2.0f, 0.4f, -1.5f), new Vector3(1.6f, 0.3f, 0.5f), wood);
                    mb.Cone(new Vector3(2.6f, 0, -0.2f), 0.7f, 0.9f, 8, LandLook.Thatch);
                    LandShapes.Beam(mb, new Vector3(-0.4f, 0, 1.3f), new Vector3(-0.1f, 1.8f, 1.5f), 0.04f, 4, Timber);
                    LandShapes.Pennant(mb, new Vector3(-3.4f, 0, 0.8f), 3.2f, faction);
                    break;
                case BuildingType.Quarry:
                    for (int tier = 0; tier < 4; tier++) mb.Box(new Vector3(0.4f, 0.4f + tier * 0.75f, -2.4f - tier * 0.3f), new Vector3(4.4f - tier * 0.7f, 0.85f, 1.3f), LandLook.Stone * (0.82f + tier * 0.06f));
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(-0.4f + i * 0.55f, 0.1f + i * 0.1f, -0.9f), new Vector3(0.5f, 0.18f + i * 0.08f, 0.8f), Foot * 1.05f);
                    Hall(mb, new Vector3(-2.9f, 0, 0.0f), 90f, 1.0f, 0.8f, 1.4f, 0.8f, Plank, Shingle, false);
                    Crane(mb, new Vector3(1.9f, 0, 0.0f), 3.4f, 25f);
                    Bricks(mb, new Vector3(1.5f, 0, -0.9f), 2, 3, 0.5f, LandLook.Stone * 1.08f);
                    Barrow(mb, new Vector3(-0.9f, 0, 1.4f), 15f);
                    LandShapes.Pennant(mb, new Vector3(-2.2f, 0, 2.3f), 3.4f, faction);
                    break;
                case BuildingType.Sawmill:
                    foreach (float x in new[] { -2.5f, 2.5f }) foreach (float z in new[] { -2.2f, 0.9f }) LandShapes.Beam(mb, new Vector3(x, 0, z), new Vector3(x, 2.3f, z), 0.08f, 5, Timber);
                    Roof(mb, new Vector3(0, 0, -0.65f), 0, 2.5f, 1.55f, 2.3f, 1.0f, Shingle, 0.35f, Plank);
                    foreach (float x in new[] { -1.3f, 1.3f }) { LandShapes.Beam(mb, new Vector3(x - 0.5f, 0, -0.5f), new Vector3(x, 1.2f, -0.5f), 0.05f, 4, Timber); LandShapes.Beam(mb, new Vector3(x + 0.5f, 0, -0.5f), new Vector3(x, 1.2f, -0.5f), 0.05f, 4, Timber); }
                    LandShapes.Beam(mb, new Vector3(-2.2f, 1.3f, -0.5f), new Vector3(2.2f, 1.3f, -0.5f), 0.25f, 7, wood);
                    mb.Box(new Vector3(0.3f, 1.8f, -0.5f), new Vector3(0.05f, 1.0f, 0.6f), LandLook.Stone * 0.7f); mb.Box(new Vector3(0.3f, 2.2f, -0.5f), new Vector3(0.12f, 0.1f, 0.7f), Timber);
                    for (int i = 0; i < 6; i++) mb.Box(new Vector3(0, 0.08f + i * 0.13f, 2.3f), new Vector3(2.6f, 0.11f, 0.7f), WareC(Ware.Planks) * (0.95f + 0.05f * (i % 2)));
                    LandShapes.LogStack(mb, new Vector3(-0.2f, 0, -3.5f), 0f, 3, 3.0f, 0.2f, wood);
                    LandShapes.Pennant(mb, new Vector3(-3.4f, 0, 2.2f), 3.6f, faction);
                    break;
                case BuildingType.Kiln:
                    mb.Cylinder(new Vector3(0, 0, -1.0f), 2.15f, 2.0f, 0.7f, 14, Foot);
                    mb.Cylinder(new Vector3(0, 0.7f, -1.0f), 1.8f, 1.35f, 1.0f, 14, LandLook.ClayRed);
                    mb.Cylinder(new Vector3(0, 1.7f, -1.0f), 1.35f, 0.7f, 0.8f, 14, LandLook.ClayRed * 0.95f);
                    mb.Cylinder(new Vector3(0.9f, 0.7f, -2.0f), 0.55f, 0.45f, 3.0f, 8, Foot * 0.95f); mb.Cylinder(new Vector3(0.9f, 3.7f, -2.0f), 0.62f, 0.62f, 0.18f, 8, Foot * 0.8f);
                    mb.Box(new Vector3(0, 0.65f, 0.95f), new Vector3(1.0f, 0.95f, 0.3f), Foot * 1.05f); mb.Box(new Vector3(0, 0.5f, 1.1f), new Vector3(0.7f, 0.65f, 0.2f), new Color(0.10f, 0.05f, 0.03f));
                    Bricks(mb, new Vector3(2.7f, 0, -1.0f), 3, 3, 0.36f, WareC(Ware.Bricks));
                    Lean(mb, new Vector3(-2.7f, 0, -0.4f), 90f, 1.8f, 1.2f, 1.8f, 1.2f, Shingle);
                    LandShapes.LogStack(mb, new Vector3(-2.7f, 0, -0.4f), 90f, 2, 1.5f, 0.17f, wood);
                    LandShapes.Pennant(mb, new Vector3(-2.9f, 0, -3.0f), 3.6f, faction);
                    break;
                case BuildingType.Bakery:
                    foreach (float x in new[] { -2.7f, 1.5f }) foreach (float z in new[] { -2.7f, 0.7f }) LandShapes.Beam(mb, new Vector3(x, 0, z), new Vector3(x, 2.3f, z), 0.07f, 5, Timber);
                    Roof(mb, new Vector3(-0.6f, 0, -1.0f), 0, 2.3f, 2.0f, 2.3f, 1.1f, Tile, 0.3f, Plaster);
                    mb.Cylinder(new Vector3(-0.6f, 0, -1.6f), 1.5f, 1.4f, 0.6f, 12, Foot);
                    mb.Cylinder(new Vector3(-0.6f, 0.6f, -1.6f), 1.4f, 0.9f, 0.7f, 12, Plaster * 0.97f);
                    mb.Cylinder(new Vector3(-0.6f, 1.3f, -1.6f), 0.9f, 0.3f, 0.5f, 12, Plaster * 0.9f);
                    mb.Box(new Vector3(-0.6f, 0.7f, -0.2f), new Vector3(0.8f, 0.6f, 0.25f), new Color(0.10f, 0.05f, 0.03f));
                    mb.Cylinder(new Vector3(-1.4f, 1.6f, -2.5f), 0.32f, 0.28f, 1.8f, 6, Foot * 0.9f);
                    mb.Box(new Vector3(0.7f, 0.6f, -0.4f), new Vector3(1.0f, 0.1f, 0.6f), wood); mb.Box(new Vector3(0.45f, 0.3f, -0.4f), new Vector3(0.1f, 0.6f, 0.5f), Timber); mb.Box(new Vector3(0.95f, 0.3f, -0.4f), new Vector3(0.1f, 0.6f, 0.5f), Timber);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(0.5f + i * 0.2f, 0.7f, -0.4f), new Vector3(0.17f, 0.12f, 0.2f), WareC(Ware.Bread));
                    mb.Cylinder(new Vector3(2.5f, 0, 0.4f), 0.55f, 0.55f, 0.25f, 10, LandLook.Stone); mb.Cylinder(new Vector3(2.5f, 0.25f, 0.4f), 0.45f, 0.45f, 0.15f, 10, LandLook.Stone * 0.85f);
                    Barrel(mb, new Vector3(-3.1f, 0, 0.9f)); Barrel(mb, new Vector3(-3.1f, 0, 1.8f));
                    LandShapes.Pennant(mb, new Vector3(3.3f, 0, -1.7f), 3.6f, faction);
                    break;
                case BuildingType.Smokehouse:
                    Hall(mb, new Vector3(-0.7f, 0, -1.6f), 0, 1.5f, 1.4f, 2.1f, 1.4f, Plank * 0.9f, Shingle, true);
                    mb.Box(new Vector3(-0.7f, 4.0f, -1.6f), new Vector3(1.6f, 0.35f, 0.5f), Timber); mb.Box(new Vector3(-0.7f, 4.25f, -1.6f), new Vector3(1.9f, 0.1f, 0.8f), Shingle * 0.8f);
                    Chimney(mb, new Vector3(0.6f, 2.4f, -2.4f), 2.6f);
                    LandShapes.Rack(mb, new Vector3(2.4f, 0, -0.6f), 90f, 2.4f, 1.7f, WareC(Ware.Meat));
                    LandShapes.Rack(mb, new Vector3(-1.2f, 0, 1.6f), 0f, 2.2f, 1.6f, WareC(Ware.Meat));
                    LandShapes.LogStack(mb, new Vector3(-3.4f, 0, -1.6f), 90f, 2, 1.4f, 0.16f, wood);
                    LandShapes.Pennant(mb, new Vector3(2.4f, 0, -2.4f), 3.8f, faction);
                    break;
                case BuildingType.Stonecutter:
                    Hall(mb, new Vector3(-1.0f, 0, -2.2f), 0, 1.5f, 1.0f, 1.7f, 1.0f, Foot * 1.05f, Shingle, true);
                    mb.Box(new Vector3(0.7f, 0.5f, 0.0f), new Vector3(2.6f, 0.15f, 1.0f), wood);
                    foreach (float x in new[] { -0.4f, 1.8f }) mb.Box(new Vector3(x, 0.22f, 0.0f), new Vector3(0.25f, 0.44f, 0.9f), Timber);
                    mb.Box(new Vector3(0.4f, 0.8f, 0.0f), new Vector3(0.7f, 0.45f, 0.6f), WareC(Ware.CutStone));
                    Bricks(mb, new Vector3(2.9f, 0, -1.7f), 3, 2, 0.55f, WareC(Ware.CutStone));
                    for (int i = 0; i < 4; i++) mb.Rock(new Vector3(-2.9f + rnd.Range(-0.5f, 0.5f), 0, 0.8f + rnd.Range(-0.5f, 0.5f)), rnd.Range(0.35f, 0.55f), LandLook.Stone, rnd.Next);
                    Crane(mb, new Vector3(-0.3f, 0, 1.6f), 2.6f, 200f);
                    LandShapes.Pennant(mb, new Vector3(3.4f, 0, 0.5f), 3.6f, faction);
                    break;
                case BuildingType.WarriorLodge:
                {
                    Hall(mb, new Vector3(0, 0, -1.5f), 0, 2.5f, 1.5f, 1.7f, 1.4f, Plank * 0.95f, Tile, true);
                    mb.Box(new Vector3(0, 0.3f, -1.5f), new Vector3(5.3f, 0.5f, 3.3f), Foot * 0.95f);
                    var pc = LandLook.People(faction); var tc = LandLook.PeopleTrim(faction);
                    for (int i = 0; i < 3; i++) { var c = new Vector3(-1.4f + i * 1.4f, 1.15f, 0.04f); mb.Cylinder(c, 0.44f, 0.44f, 0.0f, 10, pc); mb.Box(c + new Vector3(0, 0, 0.02f), new Vector3(0.62f, 0.62f, 0.05f), pc); mb.Box(c + new Vector3(0, 0, 0.06f), new Vector3(0.2f, 0.2f, 0.04f), tc); }
                    LandShapes.Beam(mb, new Vector3(3.0f, 1.3f, -2.3f), new Vector3(3.0f, 1.3f, 0.6f), 0.04f, 4, Timber);
                    for (int i = 0; i < 5; i++) { var p = new Vector3(3.0f, 0, -2.1f + i * 0.5f); LandShapes.Beam(mb, p, p + new Vector3(0.1f, 2.4f, 0), 0.03f, 4, wood); mb.Cone(p + new Vector3(0.1f, 2.4f, 0), 0.07f, 0.3f, 4, LandLook.Stone); }
                    LandShapes.Beam(mb, new Vector3(-3.2f, 0, 1.3f), new Vector3(-3.2f, 4.2f, 1.3f), 0.07f, 5, Timber);
                    mb.Quad(new Vector3(-3.2f, 4.2f, 1.3f), new Vector3(-3.2f, 3.3f, 1.3f), new Vector3(-3.2f, 3.3f, 2.4f), new Vector3(-3.2f, 4.2f, 2.2f), pc);
                    mb.Quad(new Vector3(-3.2f, 3.3f, 2.4f), new Vector3(-3.2f, 3.3f, 1.3f), new Vector3(-3.2f, 4.2f, 1.3f), new Vector3(-3.2f, 4.2f, 2.2f), pc * 0.85f);
                    break;
                }
            }
        }

        // ---------------- parts ----------------

        static Color WareC(Ware w) => LandLook.WareColor(w);
        static Vector3 R(Vector3 c, float yaw, Vector3 l) => c + Quaternion.Euler(0, yaw, 0) * l;

        static void Face(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            mb.Quad(a, b, c, d, col); mb.Quad(d, c, b, a, col * 0.78f);
        }

        /// <summary>Pitched roof (ridge along local X) with tile rows and a ridge beam; gable ends closed in <paramref name="gable"/>.</summary>
        static void Roof(MeshBuilder mb, Vector3 c, float yaw, float hx, float hz, float eave, float ridge, Color col, float ov, Color gable)
        {
            float ex = hx + ov, ez = hz + ov;
            Vector3 P(float x, float y, float z) => R(c, yaw, new Vector3(x, y, z));
            Face(mb, P(-ex, eave, ez), P(ex, eave, ez), P(ex, eave + ridge, 0), P(-ex, eave + ridge, 0), col);
            Face(mb, P(ex, eave, -ez), P(-ex, eave, -ez), P(-ex, eave + ridge, 0), P(ex, eave + ridge, 0), col * 0.9f);
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Tri(P(s * hx, eave, -hz), P(s * hx, eave, hz), P(s * hx, eave + ridge * (1f - hz / ez * 0f), 0), gable * 0.9f);
                mb.Tri(P(s * hx, eave + ridge, 0), P(s * hx, eave, hz), P(s * hx, eave, -hz), gable * 0.7f);
            }
            for (int k = 1; k <= 3; k++)
            {
                float t = k / 4f;
                foreach (float sz in new[] { -1f, 1f })
                    LandShapes.Beam(mb, P(-ex, eave + ridge * t + 0.04f, sz * ez * (1 - t)), P(ex, eave + ridge * t + 0.04f, sz * ez * (1 - t)), 0.03f, 3, col * 0.75f, false);
            }
            LandShapes.Beam(mb, P(-ex - 0.05f, eave + ridge + 0.04f, 0), P(ex + 0.05f, eave + ridge + 0.04f, 0), 0.08f, 4, Timber);
        }

        /// <summary>Walls (stone footing, corner posts, door and shutters on +Z) under a pitched roof.</summary>
        static void Hall(MeshBuilder mb, Vector3 c, float yaw, float hx, float hz, float wallH, float ridge, Color wall, Color roof, bool door)
        {
            mb.Box(R(c, yaw, new Vector3(0, wallH / 2, 0)), new Vector3(hx * 2, wallH, hz * 2), wall, yaw);
            mb.Box(R(c, yaw, new Vector3(0, 0.18f, 0)), new Vector3(hx * 2 + 0.16f, 0.36f, hz * 2 + 0.16f), Foot, yaw);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                mb.Box(R(c, yaw, new Vector3(sx * hx, wallH / 2, sz * hz)), new Vector3(0.18f, wallH, 0.18f), Timber, yaw);
            mb.Box(R(c, yaw, new Vector3(0, wallH - 0.1f, hz)), new Vector3(hx * 2 + 0.1f, 0.14f, 0.2f), Timber, yaw);
            if (door)
            {
                mb.Box(R(c, yaw, new Vector3(0, 0.6f, hz + 0.03f)), new Vector3(0.8f, 1.2f, 0.1f), DarkDoor, yaw);
                if (hx > 1.2f) foreach (float sx in new[] { -1f, 1f })
                {
                    mb.Box(R(c, yaw, new Vector3(sx * hx * 0.62f, wallH * 0.62f, hz + 0.03f)), new Vector3(0.5f, 0.45f, 0.08f), new Color(0.12f, 0.1f, 0.08f), yaw);
                    mb.Box(R(c, yaw, new Vector3(sx * hx * 0.62f + sx * 0.33f, wallH * 0.62f, hz + 0.05f)), new Vector3(0.16f, 0.5f, 0.06f), Timber, yaw);
                }
            }
            Roof(mb, c, yaw, hx, hz, wallH, ridge, roof, 0.3f, wall);
        }

        /// <summary>Open lean-to on four posts, roof falling to the back (local -Z when yaw 0).</summary>
        static void Lean(MeshBuilder mb, Vector3 c, float yaw, float w, float d, float hi, float lo, Color col)
        {
            foreach (float sx in new[] { -1f, 1f }) { LandShapes.Beam(mb, R(c, yaw, new Vector3(sx * w / 2, 0, d / 2)), R(c, yaw, new Vector3(sx * w / 2, hi, d / 2)), 0.07f, 4, Timber); LandShapes.Beam(mb, R(c, yaw, new Vector3(sx * w / 2, 0, -d / 2)), R(c, yaw, new Vector3(sx * w / 2, lo, -d / 2)), 0.07f, 4, Timber); }
            Face(mb, R(c, yaw, new Vector3(-w / 2 - 0.2f, hi + 0.05f, d / 2 + 0.2f)), R(c, yaw, new Vector3(w / 2 + 0.2f, hi + 0.05f, d / 2 + 0.2f)), R(c, yaw, new Vector3(w / 2 + 0.2f, lo + 0.05f, -d / 2 - 0.2f)), R(c, yaw, new Vector3(-w / 2 - 0.2f, lo + 0.05f, -d / 2 - 0.2f)), col);
        }

        static void Chimney(MeshBuilder mb, Vector3 top, float h)
        {
            mb.Box(new Vector3(top.x, top.y + h * 0.5f, top.z), new Vector3(0.45f, h, 0.45f), Foot * 0.92f);
            mb.Box(new Vector3(top.x, top.y + h + 0.05f, top.z), new Vector3(0.6f, 0.12f, 0.6f), Foot * 0.75f);
        }

        static void Stump(MeshBuilder mb, Vector3 p)
        {
            mb.Cylinder(p, 0.45f, 0.42f, 0.55f, 8, LandLook.Wood * 0.95f);
            LandShapes.Beam(mb, p + Vector3.up * 0.55f, p + new Vector3(0.5f, 1.25f, 0.3f), 0.045f, 4, Timber);
            mb.Box(p + new Vector3(0.42f, 1.2f, 0.26f), new Vector3(0.12f, 0.24f, 0.4f), LandLook.Stone * 0.8f);
        }

        static void Sawhorse(MeshBuilder mb, Vector3 p, float yaw)
        {
            foreach (float s in new[] { -0.6f, 0.6f })
            {
                LandShapes.Beam(mb, R(p, yaw, new Vector3(s - 0.25f, 0, -0.3f)), R(p, yaw, new Vector3(s, 0.8f, 0)), 0.05f, 4, Timber);
                LandShapes.Beam(mb, R(p, yaw, new Vector3(s + 0.25f, 0, 0.3f)), R(p, yaw, new Vector3(s, 0.8f, 0)), 0.05f, 4, Timber);
            }
            LandShapes.Beam(mb, R(p, yaw, new Vector3(-0.9f, 0.9f, 0)), R(p, yaw, new Vector3(0.9f, 0.9f, 0)), 0.13f, 6, LandLook.Wood);
        }

        static void Chips(MeshBuilder mb, PropRandom r, float x, float z, float rx, float rz)
        {
            for (int i = 0; i < 12; i++) mb.Box(new Vector3(x + r.Range(-rx, rx), 0.03f, z + r.Range(-rz, rz)), new Vector3(0.18f, 0.05f, 0.1f), LandLook.Thatch, r.Range(0, 180));
        }

        static void Bricks(MeshBuilder mb, Vector3 c, int tiers, int cols, float s, Color col)
        {
            for (int i = 0; i < tiers; i++) for (int k = 0; k < tiers - i + (cols - tiers > 0 ? cols - tiers : 0); k++)
                mb.Box(c + new Vector3(0, s * 0.5f + i * s, (k - 1) * (s * 1.1f) + i * s * 0.55f), new Vector3(s * 1.2f, s * 0.98f, s), col * (0.94f + 0.06f * (k % 2)));
        }

        static void Barrel(MeshBuilder mb, Vector3 p)
        {
            mb.Cylinder(p, 0.4f, 0.45f, 0.45f, 8, LandLook.Wood); mb.Cylinder(p + Vector3.up * 0.45f, 0.45f, 0.38f, 0.4f, 8, LandLook.Wood * 1.05f);
            mb.Cylinder(p + Vector3.up * 0.3f, 0.47f, 0.47f, 0.05f, 8, Timber, false);
        }

        static void Barrow(MeshBuilder mb, Vector3 p, float yaw)
        {
            mb.Box(R(p, yaw, new Vector3(0, 0.5f, 0)), new Vector3(0.7f, 0.3f, 1.1f), LandLook.Wood, yaw);
            LandShapes.Beam(mb, R(p, yaw, new Vector3(-0.3f, 0.5f, 0.5f)), R(p, yaw, new Vector3(-0.3f, 0.45f, 1.5f)), 0.04f, 4, Timber);
            LandShapes.Beam(mb, R(p, yaw, new Vector3(0.3f, 0.5f, 0.5f)), R(p, yaw, new Vector3(0.3f, 0.45f, 1.5f)), 0.04f, 4, Timber);
            LandShapes.Beam(mb, R(p, yaw, new Vector3(-0.06f, 0.3f, -0.6f)), R(p, yaw, new Vector3(0.06f, 0.3f, -0.6f)), 0.3f, 8, Timber);
        }

        /// <summary>Timber crane: mast, jib, rope and a hanging block.</summary>
        static void Crane(MeshBuilder mb, Vector3 p, float h, float yaw)
        {
            LandShapes.Beam(mb, p, p + Vector3.up * h, 0.1f, 5, Timber);
            LandShapes.Beam(mb, R(p, yaw, new Vector3(0, h, 0)), R(p, yaw, new Vector3(1.6f, h - 0.3f, 0)), 0.07f, 4, Timber);
            LandShapes.Beam(mb, R(p, yaw, new Vector3(0, h - 0.9f, 0)), R(p, yaw, new Vector3(1.2f, h - 0.4f, 0)), 0.04f, 4, Timber);
            LandShapes.Beam(mb, R(p, yaw, new Vector3(1.55f, h - 0.3f, 0)), R(p, yaw, new Vector3(1.55f, h - 1.9f, 0)), 0.015f, 3, LandLook.Thatch);
            mb.Box(R(p, yaw, new Vector3(1.55f, h - 2.1f, 0)), new Vector3(0.5f, 0.4f, 0.5f), LandLook.Stone);
            mb.Box(p + Vector3.up * 0.15f, new Vector3(0.7f, 0.3f, 0.7f), Foot);
        }

        static void Wall(MeshBuilder mb, Vector3 a, Vector3 b, float h)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt((b - a).magnitude / 0.6f));
            float yaw = BoardSpace.YawOf(b - a);
            for (int i = 0; i <= n; i++)
                mb.Box(Vector3.Lerp(a, b, i / (float)n) + Vector3.up * (h * 0.5f + 0.04f * (i % 2)), new Vector3(0.62f, h, 0.55f), Foot * (0.88f + 0.08f * (i % 3)), yaw + (i % 2) * 6f);
        }

        static void FenceLine(MeshBuilder mb, Vector3 a, Vector3 b, float h)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 0.75f));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                LandShapes.Beam(mb, p, p + Vector3.up * h, 0.055f, 4, Timber);
            }
            LandShapes.Beam(mb, a + Vector3.up * h * 0.45f, b + Vector3.up * h * 0.45f, 0.035f, 4, LandLook.Wood, false);
            LandShapes.Beam(mb, a + Vector3.up * h * 0.9f, b + Vector3.up * h * 0.9f, 0.035f, 4, LandLook.Wood, false);
        }

        /// <summary>Rectangular fence with a gate gap of half-width <paramref name="gate"/> on the side facing <paramref name="gateYaw"/> (0 = front +Z, 180 = back; any other = none).</summary>
        static void FenceRect(MeshBuilder mb, float x0, float x1, float z0, float z1, float h, float gate, float gateYaw)
        {
            float gx = (x0 + x1) / 2;
            if (gateYaw == 0f)
            {
                FenceLine(mb, new Vector3(x0, 0, z1), new Vector3(gx - gate, 0, z1), h); FenceLine(mb, new Vector3(gx + gate, 0, z1), new Vector3(x1, 0, z1), h);
                LandShapes.Beam(mb, new Vector3(gx - gate, 0, z1), new Vector3(gx - gate, h + 0.4f, z1), 0.08f, 5, Timber);
                LandShapes.Beam(mb, new Vector3(gx + gate, 0, z1), new Vector3(gx + gate, h + 0.4f, z1), 0.08f, 5, Timber);
            }
            else FenceLine(mb, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), h);
            FenceLine(mb, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), h);
            FenceLine(mb, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), h);
            FenceLine(mb, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), h);
        }

        static void FenceRing(MeshBuilder mb, Vector3 c, float r, int n, float gapCentreYaw, float gapDeg, float h)
        {
            // gapCentreYaw is the yaw (degrees from +Z) of the opening; 180 means the gap faces the viewer side +Z, so the ring is drawn away from it.
            for (int i = 0; i < n; i++)
            {
                float a0 = i * 360f / n, a1 = (i + 1) * 360f / n;
                float mid = (a0 + a1) / 2f;
                if (Mathf.Abs(Mathf.DeltaAngle(mid, gapCentreYaw - 180f)) < gapDeg / 2f) continue;
                FenceLine(mb, c + Quaternion.Euler(0, a0, 0) * Vector3.forward * r, c + Quaternion.Euler(0, a1, 0) * Vector3.forward * r, h);
            }
        }
    }
}
