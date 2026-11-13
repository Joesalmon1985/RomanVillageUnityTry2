using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using Terrain = World.Sim.Terrain;

namespace World.Game.Tests
{
    /// <summary>Map rework (radius-4 board): terrain, compact village, rival outpost, God camera framing and board picking.</summary>
    public class MapLayoutTests
    {
        private WorldBootstrap _boot;

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot, "WorldBootstrap.Instance");
        }

        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        [UnityTest]
        public IEnumerator Terrain_Has61HexTiles_IncludingPasture()
        {
            yield return Load();
            var tiles = Object.FindObjectsByType<HexView>(FindObjectsSortMode.None);
            Assert.AreEqual(61, tiles.Length, "one tile per hex");
            Assert.AreEqual(61, tiles.Select(t => t.HexId).Distinct().Count());
            Assert.IsTrue(tiles.Any(t => t.Terrain == Terrain.Pasture), "a Pasture tile");
            foreach (var t in tiles) Assert.IsNotNull(t.GetComponent<MeshCollider>(), "walkable tile " + t.HexId);
            // Tiles are 60 m hexes: the tile mesh reaches the hex corners.
            var topo = _boot.Sim.State.Board.Topology;
            var b = tiles[0].GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(2f * 60f * 0.8660254f, b.size.x, 0.6f, "tile width across flats");
            Assert.AreEqual(120f, b.size.z, 0.6f, "tile height corner to corner");
            Assert.Greater(TerrainBuilder.PropCounts["sheep"], 0, "sheep on the pastures");
            Assert.AreEqual(topo.HexCount, 61);
        }

        [UnityTest]
        public IEnumerator Village_FitsWithin70m_AndStaysOffPlots()
        {
            yield return Load();
            var L = _boot.Layout;
            var board = _boot.Sim.State.Board;
            var topo = board.Topology;
            var pts = new List<Vector3>();
            for (int k = 0; k < 16; k++) pts.Add(L.SquareCentre + BoardSpace.DirOf(k * 22.5f) * VillageLayout.SquareRadius);
            pts.AddRange(_boot.Warehouse.FootprintCorners());
            var buildings = new List<BuildingInfo> { VillageBuilder.Storehouse, VillageBuilder.Market };
            buildings.AddRange(VillageBuilder.Cottages);
            Assert.GreaterOrEqual(VillageBuilder.Cottages.Count, 4, "a few houses make it read as a village");
            foreach (var bi in buildings)
            {
                var bc = bi.GetComponent<BoxCollider>();
                Assert.IsNotNull(bc, bi.name + " pick volume");
                Assert.LessOrEqual(bc.size.x, 10f, bi.name + " footprint under 10 m");
                Assert.LessOrEqual(bc.size.z, 10f, bi.name + " footprint under 10 m");
                foreach (var sx in new[] { -0.5f, 0.5f }) foreach (var sz in new[] { -0.5f, 0.5f })
                    pts.Add(bi.transform.TransformPoint(bc.center + new Vector3(bc.size.x * sx, 0, bc.size.z * sz)));
                // Off the road plots: no flag closer than 7 m to the building centre.
                int v = BoardSpace.NearestVertex(bi.transform.position);
                Assert.Greater(Flat(BoardSpace.Vertex(v) - bi.transform.position), 7f, bi.name + " clear of flag " + v);
            }
            pts.Add(VillageBuilder.Bell.transform.position);
            foreach (var p in pts) Assert.Less(Flat(p - L.Home), 70f, "village point " + p + " within 70 m of the home corner");

            // The warehouse footprint covers no flag and stays beside its flag.
            var w = _boot.Warehouse.transform;
            for (int f = 0; f < topo.VertexCount; f++)
            {
                var l = w.InverseTransformPoint(BoardSpace.Vertex(topo, f));
                bool inside = l.x > -WarehouseSet.HallHalfWidth - WarehouseSet.BarAnnexWidth - 1f && l.x < WarehouseSet.HallHalfWidth + 1f && l.z < 1f && l.z > -WarehouseSet.HallDepth - 1f;
                Assert.IsFalse(inside, "warehouse covers flag " + f);
            }
            Assert.Less(Flat(_boot.Warehouse.DoorPosition - BoardSpace.Vertex(board.WarehouseVertex)), 16f, "warehouse at its flag");
            Assert.Less(Flat(BoardSpace.Vertex(board.WarehouseVertex) - L.Home), 41f);
        }

        [UnityTest]
        public IEnumerator RivalOutpost_StandsAtItsCorner_AndIsInspectable()
        {
            yield return Load();
            var board = _boot.Sim.State.Board;
            Assert.IsNotNull(_boot.Outpost, "outpost built");
            Assert.AreEqual("Rival outpost", _boot.Outpost.Title);
            Assert.AreEqual(board.RivalOutpostVertex, _boot.Outpost.Vertex);
            Assert.IsTrue(board.Topology.IsCorner(board.RivalOutpostVertex));
            Assert.Less(Flat(_boot.Outpost.transform.position - BoardSpace.Vertex(board.RivalOutpostVertex)), 0.5f);
            var col = _boot.Outpost.GetComponent<Collider>();
            Assert.IsNotNull(col); Assert.IsTrue(col.isTrigger, "pick volume");
            // A ray from above hits the outpost's pick volume.
            var p = BoardSpace.Vertex(board.RivalOutpostVertex);
            Assert.IsTrue(Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Collide));
            Assert.AreSame(_boot.Outpost.gameObject, hit.collider.gameObject);
        }

        [UnityTest]
        public IEnumerator GodCamera_DefaultFramesVillage_MaxFramesIsland()
        {
            yield return Load();
            var board = _boot.Sim.State.Board; var topo = board.Topology;
            _boot.Views.SetView(ViewMode.God);
            yield return null;
            var g = _boot.GodCam; var cam = g.Camera;
            Assert.AreEqual(GodCamera.DefaultSize, g.Size, 0.01f, "default zoom");
            bool InView(Vector3 p) { var v = cam.WorldToViewportPoint(p); return v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1; }
            Assert.IsTrue(InView(_boot.Layout.Home), "home corner");
            Assert.IsTrue(InView(_boot.Warehouse.DoorPosition), "warehouse");
            foreach (int h in topo.VertexHexes(board.HomeCityVertex)) Assert.IsTrue(InView(BoardSpace.Hex(h)), "neighbouring hex " + h);
            Assert.IsFalse(InView(BoardSpace.Hex(0)) && InView(BoardSpace.Hex(topo.HexCount - 1)), "default framing is not the whole map");

            g.Size = 1e6f;
            Assert.AreEqual(g.MaxSize, g.Size, 0.01f, "zoom clamps");
            g.FrameIsland();
            yield return null;
            for (int h = 0; h < topo.HexCount; h++) Assert.IsTrue(InView(BoardSpace.Hex(h)), "hex " + h + " at max zoom");
            foreach (int c in topo.Corners) Assert.IsTrue(InView(BoardSpace.Vertex(c)), "corner " + c + " at max zoom");
            Assert.IsTrue(InView(BoardSpace.Vertex(board.RivalOutpostVertex)), "outpost");
            Assert.Greater(cam.farClipPlane, GodCamera.Distance + BoardSpace.IslandRadius, "far plane reaches the far shore");

            g.FrameHome();
            Assert.AreEqual(GodCamera.DefaultSize, g.Size, 0.01f);
            _boot.Views.SetView(ViewMode.FirstPerson);
        }

        [UnityTest]
        public IEnumerator BoardPicker_ReturnsFlagSegmentCornerIds_AtSampledPoints()
        {
            yield return Load();
            var board = _boot.Sim.State.Board; var topo = board.Topology;
            _boot.Views.SetView(ViewMode.God);
            yield return null;
            var cam = _boot.GodCam.Camera;
            var focus = _boot.Layout.VillageCentre;
            Vector2 S(Vector3 p) => cam.WorldToScreenPoint(p);
            int flags = 0, segs = 0, corners = 0;
            for (int v = 0; v < topo.VertexCount; v++)
            {
                var p = BoardSpace.Vertex(topo, v);
                if (Flat(p - focus) > 100f) continue;
                Assert.AreEqual(v, BoardPicker.VertexAt(S(p + new Vector3(1.5f, 0, -1f))), "flag " + v);
                if (topo.IsCorner(v))
                {
                    Assert.AreEqual(v, BoardPicker.CornerAt(S(p + new Vector3(-4f, 0, 3f))), "corner " + v);
                    corners++;
                }
                else Assert.AreEqual(-1, BoardPicker.CornerAt(S(p)), "intermediate flag " + v + " is no corner");
                flags++;
            }
            for (int e = 0; e < topo.EdgeCount; e++)
            {
                var m = BoardSpace.EdgeMid(topo, e);
                if (Flat(m - focus) > 100f) continue;
                Assert.AreEqual(e, BoardPicker.EdgeAt(S(m)), "segment " + e);
                Assert.AreEqual(topo.HexEdgeOfSegment(e), BoardPicker.HexEdgeAt(S(m)), "hex edge of segment " + e);
                segs++;
            }
            for (int h = 0; h < topo.HexCount; h++)
            {
                var c = BoardSpace.Hex(topo, h);
                if (Flat(c - focus) > 100f) continue;
                Assert.AreEqual(h, BoardPicker.HexAt(S(c)), "hex " + h);
                Assert.AreEqual(-1, BoardPicker.VertexAt(S(c)), "hex centre is no flag");
            }
            Assert.Greater(flags, 20); Assert.Greater(segs, 20); Assert.Greater(corners, 5);
            Debug.Log("[MapLayoutTests] picked " + flags + " flags, " + segs + " segments, " + corners + " corners");
            _boot.Views.SetView(ViewMode.FirstPerson);
        }
    }
}
