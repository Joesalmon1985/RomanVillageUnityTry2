using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;

namespace World.Game.Tests
{
    /// <summary>F-0: the World scene boots on the prehistoric simulation (REFACTOR_SPEC 8.0, 8.6).</summary>
    public class WorldBootTests
    {
        private static IEnumerator LoadWorld()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldScene_Boots_AndSimTicks()
        {
            yield return LoadWorld();

            var boots = Object.FindObjectsByType<WorldBootstrap>(FindObjectsSortMode.None);
            Assert.AreEqual(1, boots.Length, "exactly one WorldBootstrap");
            var boot = boots[0];
            Assert.IsNotNull(boot.Sim, "sim created");
            Assert.IsNotNull(boot.Clock);
            Assert.IsNotNull(boot.MainCamera);
            Assert.AreEqual(61, boot.Sim.State.Board.Topology.HexCount, "radius-4 board");

            long t0 = boot.Sim.State.Tick;
            float until = Time.realtimeSinceStartup + 3f;
            while (boot.Sim.State.Tick < t0 + 3 && Time.realtimeSinceStartup < until) yield return null;
            Assert.GreaterOrEqual(boot.Sim.State.Tick, t0 + 3, "tick advances at 10 Hz");

            boot.Clock.Pause(PauseReasons.Menu);
            boot.Clock.Pause(PauseReasons.Encounter);
            long paused = boot.Sim.State.Tick;
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(paused, boot.Sim.State.Tick, "paused clock does not tick");
            boot.Clock.Resume(PauseReasons.Menu);
            Assert.IsTrue(boot.Clock.IsPaused, "still paused for the encounter");
            boot.Clock.Resume(PauseReasons.Encounter);
            Assert.IsFalse(boot.Clock.IsPaused);
        }

        [UnityTest]
        public IEnumerator NewBoard_StartCorners_Numbers_AndHomeCampSpawn()
        {
            yield return LoadWorld();
            var boot = WorldBootstrap.Instance;
            var b = boot.Sim.State.Board;

            CollectionAssert.AreEqual(new[] { 70, 97 }, b.StartCorners, "start corners (spec 4.3)");
            Assert.AreEqual(1, b.ReservedPlots.Length, "one reserved plot (the dance circle)");
            Assert.Greater(b.SuggestedSettlementCorners.Length, 0, "suggested settlement corners");
            for (int h = 0; h < b.HexNumber.Length; h++)
            {
                int n = b.HexNumber[h];
                if (b.HexTerrain[h] == World.Sim.Terrain.Meadow) Assert.AreEqual(0, n, "meadow hex " + h + " has no number");
                else Assert.IsTrue(n >= 2 && n <= 12 && n != 7, "hex " + h + " number " + n);
            }

            // The terrain view is built for every hex, with hex views carrying the new numbers.
            var hexes = Object.FindObjectsByType<HexView>(FindObjectsSortMode.None);
            Assert.AreEqual(61, hexes.Length, "61 hex views");
            foreach (var hv in hexes) Assert.AreEqual(b.HexNumber[hv.HexId], hv.Number, "hex view " + hv.HexId + " number");

            // Interim viewpoint at the camp spawn; Mira stands at the home camp.
            var camp = new CampLayout(b);
            Assert.AreEqual(70, camp.HomeCorner);
            Assert.Less(BoardSpace.Flat(boot.Avatar.Position - WalkLattice.ToWorld(camp.Spawn)), 0.5f, "avatar at the camp spawn");
            var mira = Npc.Find(NpcIds.Mira);
            Assert.IsNotNull(mira, "Mira spawned");
            Assert.Less(BoardSpace.Flat(mira.transform.position - camp.MiraSpot), 0.01f, "Mira at CampLayout.MiraSpot");
            Assert.Less(BoardSpace.Flat(mira.transform.position - camp.Hearth), 30f, "Mira by the home camp");

            // The village of the MVP is gone.
            Assert.IsNull(WarehouseSet.Current, "no warehouse in normal play");
            foreach (var name in new[] { "Warehouse", "Storehouse", "Market", "Bell", "Outpost" })
                Assert.IsNull(GameObject.Find(name), "no " + name + " object");
            Assert.AreEqual(1, Npc.All.Count, "only Mira until packages D and N add villagers");
        }

        [UnityTest]
        public IEnumerator WalkLattice_CornersAndHexCentres_AreCells()
        {
            yield return LoadWorld();
            var t = WorldBootstrap.Instance.Sim.State.Board.Topology;
            for (int c = 0; c < t.CornerCount; c++)
            {
                var p = BoardSpace.Vertex(t, c);
                Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(WalkLattice.ToCell(p)) - p), 0.001f, "corner " + c);
            }
            for (int h = 0; h < t.HexCount; h++)
            {
                var p = BoardSpace.Hex(t, h);
                Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(WalkLattice.ToCell(p)) - p), 0.001f, "hex " + h);
            }
            for (int k = 0; k < 6; k++)
            {
                Assert.AreEqual(k, WalkLattice.HeadingOf(WalkLattice.Yaw(k)));
                var d = WalkLattice.ToWorld(WalkLattice.Dir(k)) - WalkLattice.ToWorld(default);
                Assert.AreEqual(WalkLattice.Step, BoardSpace.Flat(d), 0.001f, "step length, heading " + k);
                Assert.AreEqual(WalkLattice.Yaw(k), Mathf.Repeat(BoardSpace.YawOf(d), 360f), 0.01f, "step direction, heading " + k);
            }
        }

        [UnityTest]
        public IEnumerator BootTour_AutoPlay_Passes()
        {
            yield return LoadWorld();
            var ap = AutoPlay.Instance;
            Assert.IsNotNull(ap);
            Assert.IsTrue(AutoPlayScripts.TryCreate("boot_tour", out var script));
            bool? ok = null;
            ap.Finished += (s, pass) => ok = pass;
            ap.Run(script);
            float until = Time.realtimeSinceStartup + 60f;
            while (ok == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(ok == true, "boot_tour: " + (ap.FailMessage ?? "timed out"));
        }
    }
}
