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

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; PointerInput.InputEnabled = true; GodBoardSettings.Override = null; }
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
            string tapped = _d.TapAtNow(_d.ScreenOfCorner(c)); // the one-tap path; the preview path is in GodBoardLedgeTests
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
            Assert.IsNotNull(_d.TapAtNow(_d.ScreenOfEdge(e)));
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

        // ---------------------------------------------------------------- G1-6

        /// <summary>Opens the board, places the player's setup, then steps until it is the player's main-phase turn.</summary>
        private IEnumerator ToMyTurn()
        {
            yield return Start();
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            yield return WaitView(BoardView.Board);
            yield return null;
            _d.TapAtNow(_d.ScreenOfCorner(_d.AdviseCorner)); Step(2); yield return null;
            _d.TapAtNow(_d.ScreenOfEdge(_d.EdgeSpots[0])); Step(2); yield return null;
            int guard = 0;
            while (!(S.Table.Phase == TablePhase.Main && S.Table.Current == 0 && !S.Table.Paused) && guard++ < 20000) Step(5);
            Assert.AreEqual(TablePhase.Main, S.Table.Phase, "reached the player's turn");
            Assert.AreEqual(0, S.Table.Current);
            yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator CostsCard_BuildMode_LightsOnlyThatKindOfSpot()
        {
            yield return ToMyTurn();
            var hand = S.Table.Peoples[0].Hand;
            for (int g = 0; g < hand.Length; g++) hand[g] = 5;
            _d.SetBuildMode(BuildKind.Road);
            yield return null; yield return null;
            Assert.AreEqual(BuildKind.Road, _d.BuildMode);
            Assert.Greater(_d.EdgeSpots.Count, 0, "road spots lit");
            Assert.AreEqual(0, _d.CornerSpots.Count, "no camp spots while in road mode");
            Assert.AreEqual(0, _d.UpgradeSpots.Count);
            _d.SetBuildMode(BuildKind.Settlement);
            yield return null; yield return null;
            Assert.AreEqual(0, _d.EdgeSpots.Count, "no road spots while in camp mode");
            _d.SetBuildMode(BuildKind.Settlement);
            Assert.AreEqual(BuildKind.None, _d.BuildMode, "tapping the same row again puts it out");
            _d.SetBuildMode(BuildKind.Road);
            Assert.IsTrue(_d.EndTurn().Ok);
            Step(2); yield return null;
            Assert.AreEqual(BuildKind.None, _d.BuildMode, "the mode ends with the turn");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TradePanel_BankTrade_ChangesTheHand()
        {
            yield return ToMyTurn();
            var hud = _d.GetComponent<GodBoardHud>();
            var hand = S.Table.Peoples[0].Hand;
            hand[0] = 8; hand[4] = 0;
            int rate = hud.BankRate(0);
            hud.OpenTrade();
            Assert.IsTrue(hud.TradeOpen);
            hud.SelectBankGive(0); hud.SelectBankGet(4);
            int wood = hand[0], stone = hand[4];
            var r = hud.DoBankTrade();
            Assert.IsTrue(r.Ok, r.Message);
            Step(2);
            Assert.AreEqual(wood - rate, S.Table.Peoples[0].Hand[0]);
            Assert.AreEqual(stone + 1, S.Table.Peoples[0].Hand[4]);
            // An offer to everyone from the panel is accepted by the table.
            hud.SetTab(1); hud.SetOfferTo(-1);
            hud.AdjustOffer(true, 0, 1); hud.AdjustOffer(false, 2, 1);
            Assert.IsTrue(hud.DoOffer().Ok);
            Step(1);
            Assert.IsNotNull(hud.OwnOpenOffer());
            // Esc closes the panel first, a second press leaves the board.
            Assert.IsTrue(_d.PressEscape());
            Assert.IsFalse(hud.TradeOpen);
            Assert.AreEqual(BoardView.Board, _d.View);
            Assert.IsTrue(_d.PressEscape());
            yield return WaitView(BoardView.Altar);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator OfferCard_AiOffer_CanBeAccepted()
        {
            yield return ToMyTurn();
            var hud = _d.GetComponent<GodBoardHud>();
            var t = S.Table;
            // Make the player not the leader (TR-4), give the AI seat 1 a city and the cards.
            for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == 1 && t.CornerLevel[c] == 1) { t.CornerLevel[c] = 2; break; }
            t.Peoples[1].Hand[1] = 3; t.Peoples[0].Hand[3] = 3; int clay0 = t.Peoples[0].Hand[1], sheep0 = t.Peoples[0].Hand[3];
            var o = new TradeOffer { Id = t.NextOfferId++, From = 1, To = 0, TicksLeft = 150, Status = OfferStatus.Open };
            o.Give[1] = 1; o.Get[3] = 1;
            t.Offers.Add(o);
            yield return null;
            var list = hud.IncomingOffers();
            Assert.AreEqual(1, list.Count, "the card is shown");
            Assert.AreEqual(o.Id, list[0].Id);
            var r = hud.AnswerOffer(o.Id, true);
            Assert.IsTrue(r.Ok, r.Message);
            Step(2);
            Assert.AreEqual(clay0 + 1, t.Peoples[0].Hand[1]);
            Assert.AreEqual(sheep0 - 1, t.Peoples[0].Hand[3]);
            Assert.AreEqual(0, hud.IncomingOffers().Count);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator DiscardPicker_SendsAValidDiscard()
        {
            yield return ToMyTurn();
            var hud = _d.GetComponent<GodBoardHud>();
            var t = S.Table;
            var hand = t.Peoples[0].Hand;
            hand[0] = 4; hand[1] = 4; hand[2] = 0; hand[3] = 0; hand[4] = 0;
            t.Phase = TablePhase.Discard;
            t.DiscardOwed = new int[t.PeopleCount]; t.DiscardOwed[0] = 4;
            t.DiscardTicksLeft = t.Tuning.DiscardTicks;
            yield return null;
            Assert.IsTrue(hud.DiscardVisible);
            Assert.AreEqual(4, hud.DiscardOwed);
            Assert.IsFalse(hud.DoDiscard().Ok, "too few cards chosen is refused");
            hud.AdjustDiscard(0, 1); hud.AdjustDiscard(0, 1); hud.AdjustDiscard(1, 1); hud.AdjustDiscard(1, 1); hud.AdjustDiscard(1, 1);
            Assert.AreEqual(4, hud.DiscardChosen, "cannot choose more than owed");
            var r = hud.DoDiscard();
            Assert.IsTrue(r.Ok, r.Message);
            Step(2);
            Assert.AreEqual(2, t.Peoples[0].Hand[0]);
            Assert.AreEqual(2, t.Peoples[0].Hand[1]);
            Assert.AreEqual(4, TableRules.HandSize(hand));
            Assert.IsFalse(hud.DiscardVisible);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator GameLost_ShowsTheEndPanel_AndTheBoardTakesNoInput()
        {
            yield return ToMyTurn();
            var hud = _d.GetComponent<GodBoardHud>();
            var t = S.Table;
            Assert.IsFalse(hud.GameOverShown);
            TableEngine eng = null;
            foreach (var sys in _boot.Sim.Systems) if (sys is TableEngine te) eng = te;
            Assert.IsNotNull(eng, "table engine");
            Assert.IsTrue(eng.IslandFalls(S, new EventBuffer()));
            yield return null; yield return null;
            Assert.IsTrue(t.IslandFell);
            Assert.IsTrue(hud.GameOverShown);
            Assert.IsTrue(_d.GameOver);
            Assert.IsNull(_d.TapAt(new Vector2(Screen.width / 2f, Screen.height / 2f)), "no table input");
            Assert.IsTrue(hud.Texts.Contains("The island falls."), "texts: " + string.Join("|", hud.Texts));
        }
    }
}
