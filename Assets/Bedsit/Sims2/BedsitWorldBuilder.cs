using System;
using System.Collections.Generic;
using System.Linq;
using OpenTS2.Components;
using UnityEngine;

namespace Bedsit.Sims2
{
    /// <summary>
    /// Builds the bedsit, corridor and garden as ONE coherent 3D environment from original Sims 2 resources:
    /// genuine wall/floor materials, genuine furniture, door, window and plant models, a genuine lawn texture.
    /// Only the thin wall/floor/ceiling slabs and the sky are generated geometry (Sims 2 walls are not objects).
    /// Metres; +Z is north, +X is east.
    /// </summary>
    public static class BedsitWorldBuilder
    {
        // ---- room dimensions -------------------------------------------------------------------------------------------
        public const float HalfW = 2.10f;        // interior half-width  (x)
        public const float HalfD = 2.34f;        // interior half-depth  (z)
        public const float WallT = 0.12f;
        public const float Height = 2.80f;
        public const float EyeHeight = 1.62f;
        public const float CorridorInnerZ0 = HalfD + WallT;            // corridor interior face next to the bedsit
        public const float CorridorWidth = 2.1f;
        public const float CorridorHalfLen = 4.2f;

        // original resources (see README provenance table)
        const string WallBedsit = "wall_paintneutatlanticsand_k_wh_txmt";
        const string WallCorridor = "wall_victoriangreenstripewainscoting_kcr_md_txmt";
        const string FloorBedsit = "floor_loftmahogonyherringbone_txmt";
        const string FloorCorridor = "floor_clubredwood_txmt";
        const string CeilingMat = "wall_paintneutsnowflake_base_txmt";
        const string LawnTexture = "terrain_lawn_fulltile_txtr";
        const string DoorModel = "doorQuaint_cres";
        const string WindowModel = "windowColonialDoubleHung_cres";
        const string MirrorShape = "mirrorFloorModerate_mirrorfloormoderate_shpe";

        sealed class Ctx
        {
            public Transform Root;
            public BedsitWorld World;
        }

        public static BedsitWorld Build()
        {
            if (!Sims2Runtime.Initialise()) return null;
            var rootGo = new GameObject("BedsitWorld");
            var world = rootGo.AddComponent<BedsitWorld>();
            var c = new Ctx { Root = rootGo.transform, World = world };

            BuildShell(c);
            BuildFurniture(c);
            BuildDoorAndWindows(c);
            BuildMirror(c);
            BuildGarden(c);
            BuildLights(c);

            world.BedsitStandpoint = new Vector3(0f, EyeHeight, 0f);
            world.CorridorStandpoint = new Vector3(0f, EyeHeight, CorridorInnerZ0 + 0.68f);
            world.DoorwayPoint = new Vector3(0f, 0f, HalfD + WallT);
            world.BedPillowEye = new Vector3(HalfW - 0.7f, 0.86f, -1.25f);
            world.CorridorNpcSpot = new Vector3(-0.25f, 0f, CorridorInnerZ0 + CorridorWidth - 0.42f);
            world.CorridorNpcLookAt = new Vector3(0f, 0f, 0f);
            world.EastNpcSpot = new Vector3(HalfW + WallT + 1.55f, 0f, 0.5f);
            world.EastNpcLookAt = new Vector3(0f, 0f, 0.5f);
            world.WestNpcSpot = new Vector3(-HalfW - WallT - 1.55f, 0f, -0.25f);
            world.WestNpcLookAt = new Vector3(0f, 0f, -0.25f);
            return world;
        }

        // ================================================================================================================ shell

        static void BuildShell(Ctx c)
        {
            var wallB = Sims2Runtime.GetMaterial(WallBedsit);
            var wallC = Sims2Runtime.GetMaterial(WallCorridor);
            var floorB = Sims2Runtime.GetMaterial(FloorBedsit);
            var floorC = Sims2Runtime.GetMaterial(FloorCorridor);
            var ceil = Sims2Runtime.GetMaterial(CeilingMat);
            c.World.Report.Add($"walls: {WallBedsit}, {WallCorridor}; floors: {FloorBedsit}, {FloorCorridor}; ceiling: {CeilingMat}");

            float xo = HalfW + WallT, zo = HalfD + WallT;
            // bedsit floor + ceiling
            Box(c, "FloorBedsit", new Vector3(-xo, -0.12f, -zo), new Vector3(xo, 0f, zo), floorB, 0.8f);
            Box(c, "CeilingBedsit", new Vector3(-xo, Height, -zo), new Vector3(xo, Height + 0.12f, zo), ceil, 0.5f);
            // south wall (mirror wall) and the two side walls with window openings are built below via Wall(...)
            Wall(c, "WallSouth", new Vector3(-xo, 0, -zo), new Vector3(xo, Height, -zo + WallT), wallB, null);
            Wall(c, "WallNorth", new Vector3(-xo, 0, HalfD), new Vector3(xo, Height, HalfD + WallT), wallB,
                new Rect(-0.5f, 0f, 1.0f, 2.40f));                                           // doorway (x centred at 0)
            Wall(c, "WallEast", new Vector3(HalfW, 0, -zo), new Vector3(xo, Height, zo), wallB,
                new Rect(0.5f - 0.52f, 1.02f, 1.04f, 1.62f));                                // window centred z=0.5 (rect.x = z-min)
            Wall(c, "WallWest", new Vector3(-xo, 0, -zo), new Vector3(-HalfW, Height, zo), wallB,
                new Rect(-0.25f - 0.52f, 1.02f, 1.04f, 1.62f));                              // window centred z=-0.25

            // corridor (runs east-west outside the northern door)
            float z0 = CorridorInnerZ0, z1 = CorridorInnerZ0 + CorridorWidth, hl = CorridorHalfLen;
            Box(c, "FloorCorridor", new Vector3(-hl, -0.12f, z0), new Vector3(hl, 0f, z1 + WallT), floorC, 0.8f);
            Box(c, "CeilingCorridor", new Vector3(-hl, Height, z0), new Vector3(hl, Height + 0.12f, z1 + WallT), ceil, 0.5f);
            Box(c, "CorridorWallNorth", new Vector3(-hl, 0, z1), new Vector3(hl, Height, z1 + WallT), wallC, 0.8f);
            Box(c, "CorridorWallWestEnd", new Vector3(-hl - WallT, 0, z0), new Vector3(-hl, Height, z1 + WallT), wallC, 0.8f);
            Box(c, "CorridorWallEastEnd", new Vector3(hl, 0, z0), new Vector3(hl + WallT, Height, z1 + WallT), wallC, 0.8f);
        }

        /// <summary>A thick axis-aligned wall slab with at most one rectangular opening.
        /// hole.x = offset along the wall's long axis (world x for N/S walls, world z for E/W walls) of the opening's min edge,
        /// hole.y = bottom, hole.width/height = size.</summary>
        static void Wall(Ctx c, string name, Vector3 min, Vector3 max, Material mat, Rect? hole)
        {
            if (!hole.HasValue) { Box(c, name, min, max, mat, 0.8f); return; }
            var h = hole.Value;
            bool alongX = (max.x - min.x) > (max.z - min.z);            // N/S walls run along x, E/W along z
            float a0 = alongX ? min.x : min.z, a1 = alongX ? max.x : max.z;
            float h0 = h.x, h1 = h.x + h.width, y0 = h.y, y1 = h.y + h.height;
            Vector3 P(float a, float y) => alongX ? new Vector3(a, y, 0) : new Vector3(0, y, a);
            Vector3 Mn(float a, float y) { var p = P(a, y); return new Vector3(alongX ? p.x : min.x, p.y, alongX ? min.z : p.z); }
            Vector3 Mx(float a, float y) { var p = P(a, y); return new Vector3(alongX ? p.x : max.x, p.y, alongX ? max.z : p.z); }
            if (h0 > a0) Box(c, name + "_A", Mn(a0, min.y), Mx(h0, max.y), mat, 0.8f);                  // left of opening
            if (a1 > h1) Box(c, name + "_B", Mn(h1, min.y), Mx(a1, max.y), mat, 0.8f);                  // right of opening
            if (y0 > min.y) Box(c, name + "_Low", Mn(h0, min.y), Mx(h1, y0), mat, 0.8f);                // below
            if (max.y > y1) Box(c, name + "_High", Mn(h0, y1), Mx(h1, max.y), mat, 0.8f);               // above
        }

        // ================================================================================================================ furniture

        static void BuildFurniture(Ctx c)
        {
            float wall = HalfW + WallT * 0.5f;                  // wall plane centres
            float zwall = HalfD + WallT * 0.5f;
            const float inset = 0.5f + WallT * 0.5f;            // Sims objects have their back at local z = -0.5

            // bed: head to the east wall, south-east corner
            Place(c, "bedSingleCheap_cres", new Vector3(wall + 0.0f, 0, -1.25f), Vector3.left, inset - 0.5f + 0.5f);
            // dresser on the west wall
            Place(c, "dresserColonial_cres", new Vector3(-wall, 0, -1.45f), Vector3.right, inset);
            // bookcase on the north wall, east of the door
            Place(c, "bookcaseValue_cres", new Vector3(1.45f, 0, zwall), Vector3.back, inset);
            // plant in the north-west corner
            PlaceFree(c, "plantFicus_cres", new Vector3(-1.62f, 0, 1.78f), 20f);
            // painting above the bed, poster beside the window
            Place(c, "paintingHorizontalCity_cres", new Vector3(wall, 0, -1.3f), Vector3.left, inset, -0.35f);
            Place(c, "posterTeenAngst_cres", new Vector3(-wall, 0, 1.45f), Vector3.right, inset, 0.15f);
            // lamp next to the bed (on the floor so it needs no table), a chair by the dresser
            PlaceFree(c, "lightingTableValue1_cres", new Vector3(HalfW - 0.38f, 0, -2.05f), 0f);
            PlaceFree(c, "chairLivingValuePlain_cres", new Vector3(-1.5f, 0, 0.95f), 105f);
            c.World.Report.Add("furniture: bedSingleCheap, dresserColonial, bookcaseValue, plantFicus, paintingHorizontalCity, posterTeenAngst, lightingTableValue1, chairLivingValuePlain");
        }

        // ================================================================================================================ door + windows

        static void BuildDoorAndWindows(Ctx c)
        {
            float zwall = HalfD + WallT * 0.5f;
            // northern door: wall plane at z = zwall, interior (bedsit) side is south => the object's front faces south
            var door = Place(c, DoorModel, new Vector3(0f, 0, zwall), Vector3.back, 0.5f);
            var simDoor = door.AddComponent<SimDoor>();
            simDoor.AjarNormalizedTime = 0.8f;
            if (!simDoor.Setup(door, "o-door-open-CW_anim")) Debug.LogWarning("[BedsitWorldBuilder] door animation not available");
            c.World.Door = simDoor;
            c.World.Report.Add($"door: {DoorModel} animated with o-door-open-CW_anim from {Sims2Runtime.PackageOfAnimation("o-door-open-CW_anim")}");

            // windows (front faces into the room)
            float wall = HalfW + WallT * 0.5f;
            var w1 = Place(c, WindowModel, new Vector3(wall, 0, 0.5f), Vector3.left, 0.5f);
            var w2 = Place(c, WindowModel, new Vector3(-wall, 0, -0.25f), Vector3.right, 0.5f);
            ThinGlass(w1); ThinGlass(w2);
            c.World.Report.Add($"windows: {WindowModel} x2");

            // a second door on the corridor's far wall + a plant for scenery
            float zc = CorridorInnerZ0 + CorridorWidth;
            Place(c, "doorWalnut_cres", new Vector3(-2.4f, 0, zc + WallT * 0.5f), Vector3.back, 0.5f);
            Place(c, "doorWalnut_cres", new Vector3(2.7f, 0, zc + WallT * 0.5f), Vector3.back, 0.5f);
            PlaceFree(c, "plantFicus_cres", new Vector3(-3.5f, 0, zc - 0.5f), 0f);
            Place(c, "paintingHorizontalCity_cres", new Vector3(-1.1f, 0, zc + WallT * 0.5f), Vector3.back, 0.5f, 0.0f);
        }

        /// <summary>The game's window glass is fairly milky; thin its alpha a little so the NPCs outside read clearly.
        /// (Uses private material copies, so the shared game material is untouched.)</summary>
        static void ThinGlass(GameObject window)
        {
            if (window == null) return;
            foreach (var r in window.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.ToLowerInvariant().Contains("glass")) continue;
                var m = new Material(r.sharedMaterial);
                if (m.HasProperty("_AlphaMultiplier")) m.SetFloat("_AlphaMultiplier", m.GetFloat("_AlphaMultiplier") * 0.35f);
                r.sharedMaterial = m;
            }
        }

        // ================================================================================================================ mirror

        static void BuildMirror(Ctx c)
        {
            // Freestanding original mirror (frame model) against the south wall, centred on x = 0.
            float zface = -HalfD;                                        // interior face of the south wall
            var frame = Sims2Runtime.CreateShapeObject(MirrorShape);
            if (frame == null) { Debug.LogWarning("[BedsitWorldBuilder] mirror frame missing"); }
            else
            {
                var holder = Hold(c, frame, "MirrorFrame_" + MirrorShape);
                holder.transform.position = new Vector3(0f, 0f, zface + 0.5f + 0.02f);
                holder.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // front faces into the room (north)
                c.World.MirrorFrameObject = holder;
            }
            c.World.Report.Add($"mirror frame: {MirrorShape} (glass = live planar reflection)");

            // Glass frame: centre, normal pointing INTO the room (north), world up.
            var glass = new GameObject("MirrorGlassFrame").transform;
            glass.SetParent(c.Root, false);
            glass.position = new Vector3(0f, 1.15f, zface + 0.5f - 0.02f);
            glass.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            c.World.MirrorFrame = glass;
            c.World.MirrorGlassSize = new Vector2(0.62f, 1.62f);
        }

        // ================================================================================================================ garden

        static void BuildGarden(Ctx c)
        {
            var lawn = Sims2Runtime.GetTextureMaterial(LawnTexture);
            c.World.Report.Add($"garden: lawn texture {LawnTexture}; trees/shrubs/fence from treeBirch01, shrub4, flowerDaisy, fenceBalustradeWhiteRail");
            Box(c, "Lawn", new Vector3(-40f, -0.6f, -40f), new Vector3(40f, -0.02f, 40f), lawn, 0.35f, false);

            var rng = new System.Random(7);
            // shrubs and flowers close to the house on both sides
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side * (HalfW + WallT + 0.9f);
                for (int i = 0; i < 6; i++)
                {
                    float z = -2.0f + i * 0.85f + (float)rng.NextDouble() * 0.3f;
                    if (z > -1.0f && z < 1.6f) continue;                 // keep the window sightlines clear
                    PlaceFree(c, "shrub4_cres", new Vector3(x0 + side * (float)rng.NextDouble() * 0.2f, -0.02f, z), (float)rng.NextDouble() * 360f);
                }
                for (int i = 0; i < 5; i++)
                    PlaceFree(c, "flowerDaisy_cres", new Vector3(side * (HalfW + WallT + 1.6f + (float)rng.NextDouble()), -0.02f, -2.5f + i * 1.3f), (float)rng.NextDouble() * 360f);
                // fence line
                for (int i = -9; i <= 9; i++)
                    PlaceFree(c, "fenceBalustradeWhiteRail_cres", new Vector3(side * 7.4f, -0.02f, i * 1.0f), 90f);
                // big trees (the game's own birches are ~10 m tall) set back beyond the fence
                PlaceFree(c, "treeBirch01_cres", new Vector3(side * 11.5f, -0.02f, 2.5f), 0f, 0.8f);
                PlaceFree(c, "treeBirch01_cres", new Vector3(side * 12.5f, -0.02f, -5.5f), 40f, 0.9f);
            }
            // trees beyond the corridor / behind the house
            PlaceFree(c, "treeBirch01_cres", new Vector3(-6f, -0.02f, 14f), 10f, 0.9f);
            PlaceFree(c, "treeBirch01_cres", new Vector3(7f, -0.02f, 16f), 70f, 1.0f);
        }

        // ================================================================================================================ lights

        static void BuildLights(Ctx c)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(c.Root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, 78f, 0f);     // from the west, low: reaches the west window and the floor
            c.World.Sun = sun;

            var room = new GameObject("RoomLight").AddComponent<Light>();
            room.transform.SetParent(c.Root, false);
            room.type = LightType.Point;
            room.color = new Color(1f, 0.86f, 0.68f);
            room.range = 7f;
            room.intensity = 1.05f;
            room.shadows = LightShadows.None;
            room.transform.position = new Vector3(0f, Height - 0.7f, 0f);
            c.World.RoomLight = room;

            var corr = new GameObject("CorridorLight").AddComponent<Light>();
            corr.transform.SetParent(c.Root, false);
            corr.type = LightType.Point;
            corr.color = new Color(1f, 0.9f, 0.75f);
            corr.range = 7f;
            corr.intensity = 1.0f;
            corr.shadows = LightShadows.None;
            corr.transform.position = new Vector3(0f, Height - 0.7f, CorridorInnerZ0 + CorridorWidth * 0.5f);
            c.World.CorridorLight = corr;

            ApplyEnvironment();
        }

        /// <summary>Scene-wide lighting environment (also applied when the baked world is loaded).</summary>
        public static void ApplyEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.46f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.26f, 0.22f);
        }

        // ================================================================================================================ helpers

        /// <summary>Place an original object whose back lies on a wall: origin = wall plane + inward * distance,
        /// local +Z facing into the room. extraUp lifts it (wall hangings).</summary>
        /// <summary>Creates a holder under the world root; the Sims-to-Unity wrapper (which carries its own -90 degree
        /// rotation) is parented beneath it, so placement never overwrites that conversion.</summary>
        static GameObject Hold(Ctx c, GameObject wrapper, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(c.Root, false);
            wrapper.transform.SetParent(holder.transform, false);
            return holder;
        }

        static GameObject Place(Ctx c, string cresName, Vector3 wallPoint, Vector3 inward, float distance, float extraUp = 0f)
        {
            var asset = Sims2Runtime.GetCres(cresName);
            if (asset == null) { Debug.LogWarning("[BedsitWorldBuilder] missing " + cresName); return null; }
            var go = Hold(c, asset.CreateRootGameObject(), cresName);
            go.transform.position = wallPoint + inward * distance + Vector3.up * extraUp;
            go.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
            return go;
        }

        static GameObject PlaceFree(Ctx c, string cresName, Vector3 pos, float yawDegrees, float scale = 1f)
        {
            var asset = Sims2Runtime.GetCres(cresName);
            if (asset == null) { Debug.LogWarning("[BedsitWorldBuilder] missing " + cresName); return null; }
            var go = Hold(c, asset.CreateRootGameObject(), cresName);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static GameObject Box(Ctx c, string name, Vector3 min, Vector3 max, Material mat, float uvPerMetre, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(min, max, uvPerMetre);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Standard"));
            if (!castShadows) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static Mesh BoxMesh(Vector3 min, Vector3 max, float uv)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); var norms = new List<Vector3>();
            void Face(Vector3 o, Vector3 u, Vector3 v, Vector3 n, Func<Vector3, Vector2> uvf)
            {
                int i0 = verts.Count;
                var p = new[] { o, o + u, o + u + v, o + v };
                foreach (var q in p) { verts.Add(q); uvs.Add(uvf(q) * uv); norms.Add(n); }
                if (Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), n) > 0f) tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                else tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var s = max - min;
            Func<Vector3, Vector2> zy = q => new Vector2(q.z, q.y);
            Func<Vector3, Vector2> xz = q => new Vector2(q.x, q.z);
            Func<Vector3, Vector2> xy = q => new Vector2(q.x, q.y);
            Face(new Vector3(max.x, min.y, min.z), new Vector3(0, 0, s.z), new Vector3(0, s.y, 0), Vector3.right, zy);
            Face(new Vector3(min.x, min.y, min.z), new Vector3(0, 0, s.z), new Vector3(0, s.y, 0), Vector3.left, zy);
            Face(new Vector3(min.x, max.y, min.z), new Vector3(s.x, 0, 0), new Vector3(0, 0, s.z), Vector3.up, xz);
            Face(new Vector3(min.x, min.y, min.z), new Vector3(s.x, 0, 0), new Vector3(0, 0, s.z), Vector3.down, xz);
            Face(new Vector3(min.x, min.y, max.z), new Vector3(s.x, 0, 0), new Vector3(0, s.y, 0), Vector3.forward, xy);
            Face(new Vector3(min.x, min.y, min.z), new Vector3(s.x, 0, 0), new Vector3(0, s.y, 0), Vector3.back, xy);
            var m = new Mesh { name = "Box" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
