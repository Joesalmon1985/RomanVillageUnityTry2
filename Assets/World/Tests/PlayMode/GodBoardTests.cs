using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Table;

namespace World.Game.Tests
{
    /// <summary>G1-5: the dev start, the temples, opening the god board, a placement click and leaving (GB-1, GB-2, GB-4).</summary>
    public class GodBoardTests
    {
        private const string TestPause = "GodBoardTest";
        private WorldBootstrap _boot;
        private GodBoardDirector _d;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; }

        private IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsNotNull(old, "World scene");
            Assert.IsTrue(DevStart.Begin(GodBoardStart.Id, 7UL), DevStart.LastError);
            float t0 = Time.realtimeSinceStartup;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == GodBoardStart.Id))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 60f, "dev.godboard did not start");
                yield return null;
            }
            yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
            _d = GodBoardDirector.Instance;
            Assert.IsNotNull(_d, "god board installed");
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private IEnumerator WaitView(BoardView v)
        {
            float t0 = Time.realtimeSinceStartup;
            while (_d.View != v) { Assert.Less(Time.realtimeSinceStartup - t0, 10f, "view " + _d.View + " never became " + v); yield return null; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DevStart_CreatesGodBoardGame_WithFourTemples()
        {
            yield return Start();
            Assert.AreEqual(Ruleset.GodBoard, S.Ruleset);
            Assert.AreEqual(4, S.Peoples.Count);
            Assert.IsNotNull(S.Table);
            Assert.AreEqual(0, S.Table.HumanSeat);
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase);
            Assert.AreEqual(4, _d.Temples.Count);
            for (int p = 0; p < 4; p++) Assert.IsNotNull(_d.TempleOf(p), "temple " + p);
            Assert.IsNotNull(_d.OwnTemple.GetComponentInChildren<TempleAltar>(), "the player's altar works");
            Assert.AreEqual(0, _d.TempleOf(1).GetComponentsInChildren<TempleAltar>().Length, "rival temples are scenery");
            // Rival temple smokes only while that people takes its turn.
            Assert.IsFalse(_d.TempleOf(1).Smoking);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LegacyStart_HasNoGodBoard()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(WorldBootstrap.Instance);
            Assert.AreEqual(Ruleset.Legacy, WorldBootstrap.Instance.Sim.State.Ruleset);
            Assert.IsNull(GodBoardDirector.Instance);
            Assert.IsNull(WorldBootstrap.Instance.Sim.State.Table);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Sacrifice_OpensTheTable_AndTheCameraRises()
        {
            yield return Start();
            Step(5);
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase, "quiet before the sacrifice (GB-6)");
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase);
            Assert.IsFalse(S.Table.Paused);
            yield return WaitView(BoardView.Board);
            Assert.IsTrue(_d.BoardCam.enabled);
            Assert.IsFalse(_boot.Avatar.Camera.enabled);
            Assert.IsTrue(_boot.Avatar.HasLock(GodBoardDirector.Lock));
            Assert.Greater(_d.BoardCam.transform.position.y, 500f, "high above the island");
            Assert.IsTrue(_d.MyTurn);
            Assert.Greater(_d.CornerSpots.Count, 20, "legal corners are shown in setup");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PlacementClick_SendsCommand_AndPieceAppears()
        {
            yield return Start();
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            yield return WaitView(BoardView.Board);
            yield return null;
            int c = _d.AdviseCorner;
            Assert.GreaterOrEqual(c, 0, "the adviser marks a corner");
            Assert.IsTrue(_d.CornerSpots.Contains(c));
            string tapped = _d.TapAt(_d.ScreenOfCorner(c));
            Assert.AreEqual("corner " + c, tapped);
            Step(2);
            yield return null;
            Assert.AreEqual(0, S.Table.CornerOwner[c]);
            Assert.IsNotNull(_d.Pieces.CornerPiece(c), "the settlement piece appears");
            // The road comes next, beside it.
            Assert.AreEqual(c, S.Table.SetupCorner);
            yield return null;
            Assert.Greater(_d.EdgeSpots.Count, 0);
            int e = _d.EdgeSpots[0];
            Assert.IsNotNull(_d.TapAt(_d.ScreenOfEdge(e)));
            Step(2);
            yield return null;
            Assert.AreEqual(0, S.Table.RoadOwner[e]);
            Assert.IsNotNull(_d.Pieces.RoadPiece(e));
            // A tap that is not on a legal spot sends nothing.
            Assert.IsNull(_d.TapAt(new Vector2(2, 2)));
            // The three AIs place, then the game is in its first round.
            Step(600);
            yield return null;
            Assert.AreEqual(TablePhase.Main, S.Table.Phase);
            Assert.GreaterOrEqual(_d.Pieces.SettlementCount, 8);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Leave_ReturnsToTheAltar_AndTheTablePauses()
        {
            yield return Start();
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            yield return WaitView(BoardView.Board);
            _d.Leave();
            Step(2);
            Assert.IsTrue(S.Table.Paused, "turns pause when the player leaves (GB-4)");
            yield return WaitView(BoardView.Altar);
            Assert.IsFalse(_d.BoardCam.enabled);
            Assert.IsTrue(_boot.Avatar.Camera.enabled);
            Assert.IsFalse(_boot.Avatar.HasLock(GodBoardDirector.Lock));
            long ticks = S.Table.ActiveTicks;
            int turn = S.Table.Current;
            Step(100);
            Assert.AreEqual(ticks, S.Table.ActiveTicks, "the table is frozen");
            Assert.AreEqual(turn, S.Table.Current);
            // Coming back resumes it where it stood (GB-5).
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            Assert.IsFalse(S.Table.Paused);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase);
        }
    }
}
