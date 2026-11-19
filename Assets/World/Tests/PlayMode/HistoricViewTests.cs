using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Historic;
using World.Sim.Campaign;
using World.Sim.Table;

namespace World.Game.Tests
{
    /// <summary>G1-8 view part, G4-3, G4-4: the switch, temples falling, castles, trees and stumps, the player in Historic.</summary>
    public class HistoricViewTests
    {
        private const string TestPause = "HistoricViewTest";
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; HistoricDirector.EveHoldSeconds = 1e6f; }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; HistoricDirector.EveHoldSeconds = 3f; HistoricDirector.TurnMomentSeconds = 4.5f; }

        private IEnumerator Start(string id)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsNotNull(old, "World scene");
            Assert.IsTrue(DevStart.Begin(id, 7UL), DevStart.LastError);
            float t0 = Time.realtimeSinceStartup;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == id))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 120f, id + " did not start");
                yield return null;
            }
            yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        [UnityTest, Timeout(240000)]
        public IEnumerator Switch_TearsDownTemples_AndStartsCastles()
        {
            yield return Start(GodBoardStart.EveId);
            var d = GodBoardDirector.Instance; var hd = HistoricDirector.Instance;
            Assert.IsNotNull(hd);
            Assert.IsNull(S.Historic, "one tick before the age turns");
            Assert.AreEqual(4, d.Temples.Count);
            var temples = new System.Collections.Generic.List<TempleView>(d.Temples);
            Step(1);
            Assert.IsNotNull(S.Historic, "the switch happened");
            Assert.AreEqual(World.Sim.Eras.Era.Historic, World.Sim.Eras.AgesApi.Current(S));
            yield return null; yield return null;
            Assert.IsTrue(hd.CollapseWaiting || hd.TemplesDown, "temples are told to fall");
            float t0 = Time.realtimeSinceStartup;
            while (temples.Exists(t => t != null))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 20f, "temples never cleared");
                yield return null;
            }
            Assert.IsTrue(hd.TemplesDown);
            Step(2);
            yield return null; yield return null;
            hd.SyncNow();
            for (int p = 0; p < 4; p++)
            {
                Assert.AreEqual(0, S.Historic.Peoples[p].Castle.StagesDone);
                Assert.IsTrue(hd.ScaffoldShown(p), "castle " + p + " is a construction site with scaffolding");
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ForestTrees_RenderFromTheSim_AndAFelledTreeBecomesAStump()
        {
            yield return Start(GodBoardStart.EveId);
            var hd = HistoricDirector.Instance;
            Step(1);
            yield return null;
            hd.SyncNow();
            int hex = -1;
            for (int h = 0; h < S.Historic.Hexes.Length && hex < 0; h++)
                if (S.Historic.Hexes[h] != null && S.Historic.Hexes[h].Terrain == World.Sim.Terrain.Forest) hex = h;
            Assert.GreaterOrEqual(hex, 0);
            Assert.IsTrue(hd.HexHasTreeMesh(hex), "the forest hex shows the sim's trees");
            Assert.IsTrue(hd.ForestPropsHidden(hex), "the prehistoric dressing is gone");
            var hr = S.Historic.Hexes[hex];
            int slot = System.Array.IndexOf(hr.Trees, (int)TreeStage.Mature);
            Assert.GreaterOrEqual(slot, 0, "grown trees at the switch");
            int before = hd.TreeMeshVertices(hex);
            hr.Trees[slot] = (int)TreeStage.Stump; hr.TreeNext[slot] = S.Tick + S.Historic.Tuning.StumpRotTicks; // what a woodcutter's felling leaves
            hd.SyncNow();
            int stumped = hd.TreeMeshVertices(hex);
            Assert.AreNotEqual(before, stumped, "the mature tree was replaced by a stump");
            hr.Trees[slot] = (int)TreeStage.Empty; // rotted
            hd.SyncNow();
            Assert.Less(hd.TreeMeshVertices(hex), stumped, "a rotted stump is gone");
            hr.Trees[slot] = (int)TreeStage.Sapling; // a forester planted
            hd.SyncNow();
            Assert.Greater(hd.TreeMeshVertices(hex), 0);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Castle_ShowsItsStage()
        {
            yield return Start(GodBoardStart.EveId);
            var hd = HistoricDirector.Instance;
            Step(1); yield return null;
            hd.SyncNow();
            Assert.AreEqual(0, hd.CastleStagesShown(0), "stage 0 rises first");
            Assert.IsTrue(hd.ScaffoldShown(0));
            var cs = S.Historic.Peoples[0].Castle;
            cs.StagesDone = 1; hd.SyncNow();
            Assert.AreEqual(0, hd.CastleStagesShown(0), "the wooden castle stands while the brick stage is waiting for goods");
            cs.StagesDone = 2; hd.SyncNow();
            Assert.AreEqual(1, hd.CastleStagesShown(0));
            cs.StagesDone = 3; hd.SyncNow();
            Assert.AreEqual(2, hd.CastleStagesShown(0), "the stone castle");
            Assert.IsFalse(hd.ScaffoldShown(0), "scaffolding comes down when the castle is finished");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Historic_PlayerWalks_BoardGone_CouncilReachable()
        {
            yield return Start(GodBoardStart.HistoricId);
            Assert.IsNotNull(S.Historic);
            var d = GodBoardDirector.Instance;
            Assert.AreEqual(0, d.Temples.Count, "a Historic game has no temples");
            Assert.AreEqual(BoardView.Altar, d.View);
            Assert.IsFalse(d.BoardCam.enabled, "the god board is gone");
            Assert.IsTrue(d.GameOver);
            Assert.IsNotNull(PlayerWalker.Instance);
            Assert.IsTrue(PlayerWalker.Instance.CanMove, "the player just walks");
            Assert.IsNotNull(CouncilDirector.Instance, "the council is installed in Historic");
            Assert.IsNotNull(CouncilDirector.Instance.EnsureScene(CouncilDirector.Instance.HomeSettlement), "the council scene can be built");
            yield return null;
            Step(30);
            yield return null;
            HistoricDirector.Instance.SyncNow();
            Assert.Greater(HistoricDirector.Instance.BuildingViews, 0);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Historic_SpireStillStartsTheRhythmEncounter()
        {
            yield return Start(GodBoardStart.HistoricId);
            Assert.IsNotNull(S.Historic);
            var sd = SpireDirector.Instance;
            Assert.IsNotNull(sd, "spires installed in a Historic game");
            sd.OnTicked(new System.Collections.Generic.List<SimEvent> { new SpireRaised { Element = Element.Earth, People = 1, Corner = S.Board.StartCorners[1], Kind = "city" } });
            var sp = sd.Stage(0);
            _boot.Avatar.PlaceExact(sp.Layout.DancerSpot, sp.Layout.DancerYaw, 0f);
            sp.Interact();
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "the rhythm encounter starts at a spire after the switch");
            EncounterHost.Instance.Abort();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator BoardOpen_ClosesWithTheTurnMoment()
        {
            yield return Start(GodBoardStart.Id);
            var d = GodBoardDirector.Instance;
            Assert.IsTrue(d.TrySacrifice(true));
            Step(2);
            float t0 = Time.realtimeSinceStartup;
            while (d.View != BoardView.Board) { Assert.Less(Time.realtimeSinceStartup - t0, 10f); yield return null; }
            HistoricDirector.TurnMomentSeconds = 0.4f;
            // The sim is not at its end here; the view hears the switch event and closes the board after the moment.
            HistoricDirector.Instance.BeganForTest(0);
            t0 = Time.realtimeSinceStartup;
            while (d.View != BoardView.Altar) { Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the board never closed"); yield return null; }
            Assert.IsTrue(_boot.Avatar.Camera.enabled);
        }
    }
}
