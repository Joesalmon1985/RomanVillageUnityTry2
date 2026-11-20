using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Table;

namespace World.Game.Tests
{
    /// <summary>WU-2: the altar ledge and the ghost-piece preview in play (WORLD_UI_PLAN 2.2).</summary>
    public class GodBoardLedgeTests
    {
        private const string TestPause = "GodBoardLedgeTest";
        private WorldBootstrap _boot;
        private GodBoardDirector _d;
        private GodBoardHud _hud;
        private WorldState S => _boot.Sim.State;
        private TableState T => S.Table;

        [SetUp]
        public void SetUp()
        {
            GodBoardDirector.RiseSeconds = 0.05f;
            PointerInput.InputEnabled = true;
            GodBoardSettings.Override = null;
            WorldActions.Machine.MinConfirmSeconds = 0f; WorldActions.Machine.GuardSeconds = 0f; // tests do not wait for the real-time guards (WU-0 tests those)
            WorldActions.Cancel();
        }

        [TearDown]
        public void TearDown()
        {
            GodBoardDirector.RiseSeconds = 2.4f;
            GodBoardSettings.Override = null;
            ScreenSafeArea.Override = null;
            WorldActions.Machine.MinConfirmSeconds = WorldActionMachine.DefaultMinConfirmSeconds;
            WorldActions.Machine.GuardSeconds = WorldActionMachine.DefaultGuardSeconds;
            WorldActions.Cancel();
        }

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
            _hud = _d.GetComponent<GodBoardHud>();
            Assert.IsNotNull(_d, "god board installed");
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private IEnumerator WaitView(BoardView v)
        {
            float t0 = Time.realtimeSinceStartup;
            while (_d.View != v) { Assert.Less(Time.realtimeSinceStartup - t0, 10f, "view " + _d.View + " never became " + v); yield return null; }
        }

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
            while (!(T.Phase == TablePhase.Main && T.Current == 0 && !T.Paused) && guard++ < 20000) Step(5);
            Assert.AreEqual(TablePhase.Main, T.Phase, "reached the player's turn");
            yield return null; yield return null;
        }

        private static void Fill(int[] hand, int n) { for (int g = 0; g < hand.Length; g++) hand[g] = n; }

        // ------------------------------------------------------------ ghost preview on the board

        [UnityTest, Timeout(240000)]
        public IEnumerator Build_PreviewThenConfirm_SendsOnce()
        {
            yield return ToMyTurn();
            Fill(T.Peoples[0].Hand, 5);
            _d.SetBuildMode(BuildKind.Road);
            yield return null; yield return null;
            Assert.Greater(_d.EdgeSpots.Count, 0);
            int e = _d.EdgeSpots[0];
            int roads = _d.Pieces.RoadCount; int wood = T.Peoples[0].Hand[0], clay = T.Peoples[0].Hand[1];
            string first = _d.TapAt(_d.ScreenOfEdge(e));
            Assert.AreEqual("preview road " + e, first);
            Assert.IsTrue(_d.Pieces.HasGhost, "a ghost piece stands on the spot");
            Assert.AreEqual(BuildKind.Road, _d.Pieces.GhostKind);
            Assert.AreEqual(e, _d.Pieces.GhostSpot);
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
            StringAssert.Contains("road", string.Join(" ", LedgerCard.Content.AllText()).ToLowerInvariant());
            Step(2);
            Assert.AreEqual(-1, T.RoadOwner[e], "the preview built nothing");
            Assert.AreEqual(wood, T.Peoples[0].Hand[0]);
            string second = _d.TapAt(_d.ScreenOfEdge(e));
            Assert.AreEqual("road " + e, second);
            Step(2); yield return null;
            Assert.AreEqual(0, T.RoadOwner[e]);
            Assert.AreEqual(wood - 1, T.Peoples[0].Hand[0]);
            Assert.AreEqual(clay - 1, T.Peoples[0].Hand[1]);
            Assert.IsFalse(_d.Pieces.HasGhost, "the ghost is gone");
            // A third tap on the same spot (it is built now) builds no second road there.
            string third = _d.TapAt(_d.ScreenOfEdge(e));
            Assert.IsTrue(third == null || !third.StartsWith("road"), "third tap: " + third);
            WorldActions.Cancel(); _d.CancelSpotPreview();
            Step(2); yield return null;
            Assert.AreEqual(wood - 1, T.Peoples[0].Hand[0], "paid once");
            Assert.AreEqual(roads + 1, _d.Pieces.RoadCount, "exactly one road more");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Build_Cancel_LeavesNothing()
        {
            yield return ToMyTurn();
            Fill(T.Peoples[0].Hand, 5);
            _d.SetBuildMode(BuildKind.Road);
            yield return null; yield return null;
            int e = _d.EdgeSpots[0];
            int owners = T.RoadOwner.Count(x => x >= 0);
            var hand = (int[])T.Peoples[0].Hand.Clone();
            Assert.IsTrue(_d.TapAt(_d.ScreenOfEdge(e)).StartsWith("preview"));
            Assert.IsTrue(_d.Pieces.HasGhost);
            // elsewhere cancels
            Assert.IsNull(_d.TapAt(new Vector2(2, 2)));
            yield return null;
            Assert.AreEqual(WorldActionState.Idle, WorldActions.State);
            Assert.IsFalse(_d.Pieces.HasGhost);
            Step(3);
            Assert.AreEqual(owners, T.RoadOwner.Count(x => x >= 0), "no road was built");
            CollectionAssert.AreEqual(hand, T.Peoples[0].Hand, "nothing was paid");
            // Esc also cancels a preview before it leaves the board
            Assert.IsTrue(_d.TapAt(_d.ScreenOfEdge(e)).StartsWith("preview"));
            Assert.IsTrue(_d.PressEscape());
            Assert.AreEqual(BoardView.Board, _d.View, "the first Esc only cancelled the preview");
            Assert.IsFalse(_d.Pieces.HasGhost);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Build_IllegalSpot_IsNeverOffered()
        {
            yield return ToMyTurn();
            Fill(T.Peoples[0].Hand, 5);
            yield return null; yield return null;
            var legal = _d.EdgeSpots.ToList();
            int bad = -1;
            for (int e = 0; e < T.RoadOwner.Length; e++) if (T.RoadOwner[e] < 0 && !legal.Contains(e)) { bad = e; break; }
            Assert.GreaterOrEqual(bad, 0);
            Assert.IsFalse(_d.IsSpotOffered(BuildKind.Road, bad));
            Assert.IsFalse(_d.SpotAction(BuildKind.Road, bad).Check().Ok, "the action refuses an illegal spot");
            Assert.IsNull(_d.TapAt(new Vector2(Screen.width * 0.5f, Screen.height - 2f)), "a tap far from every legal spot offers nothing");
            // Too poor: nothing is lit and nothing previews
            Fill(T.Peoples[0].Hand, 0);
            Assert.IsFalse(_d.IsSpotOffered(BuildKind.Road, legal[0]), "too poor: not offered"); // also refreshes the marks
            Assert.AreEqual(0, _d.EdgeSpots.Count);
            Assert.IsNull(_d.TapAt(_d.ScreenOfEdge(legal[0])));
            Assert.AreEqual(WorldActionState.Idle, WorldActions.State);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Builder_BenchChoosesTheMode_AndConfirmsAnOpenGhost()
        {
            yield return ToMyTurn();
            Fill(T.Peoples[0].Hand, 5);
            yield return null; yield return null;
            Assert.AreEqual(WorldTapResult.Inspected, _hud.TapLedge("bench:0"));
            Assert.AreEqual(BuildKind.Road, _d.BuildMode);
            yield return null; yield return null;
            int e = _d.EdgeSpots[0];
            Assert.IsTrue(_d.TapAt(_d.ScreenOfEdge(e)).StartsWith("preview"));
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("bench:1"), "tapping the builder confirms the ghost");
            Step(2);
            Assert.AreEqual(0, T.RoadOwner[e]);
            // A refusal is spoken
            Fill(T.Peoples[0].Hand, 0);
            yield return null; yield return null;
            Assert.AreEqual(WorldTapResult.Refused, _hud.TapLedge("bench:2"));
            StringAssert.Contains("lack", string.Join(" ", LedgerCard.Content.AllText()));
        }

        // ------------------------------------------------------------ the ledge objects

        [UnityTest, Timeout(240000)]
        public IEnumerator Ledge_ObjectsFitTheSafeArea_AreBigEnough_AndCarryTheirZones()
        {
            yield return ToMyTurn();
            foreach (var sz in new[] { new Vector2(Screen.width, Screen.height) })
            {
                ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(Screen.width, Screen.height);
                _hud.LayoutLedge();
                var safe = ScreenSafeArea.Current();
                float min = WorldHitTarget.MinSide(Screen.height);
                Assert.IsTrue(_hud.LedgeRects.Count >= 18, "baskets, bench, stakes, trader, hourglass and the rest");
                foreach (var kv in _hud.LedgeRects)
                {
                    var r = kv.Value;
                    Assert.GreaterOrEqual(r.xMin, safe.xMin - 0.5f, kv.Key + " inside the safe area");
                    Assert.LessOrEqual(r.xMax, safe.xMax + 0.5f, kv.Key + " inside the safe area");
                    Assert.LessOrEqual(r.yMax, safe.yMax + 0.5f, kv.Key + " inside the safe area");
                    Assert.GreaterOrEqual(r.height, min - 0.5f, kv.Key + " tall enough");
                    if (!kv.Key.StartsWith("envoy")) Assert.GreaterOrEqual(r.width, min - 0.5f, kv.Key + " wide enough");
                }
                // no two objects overlap
                var list = _hud.LedgeRects.ToList();
                for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++)
                        Assert.IsFalse(list[i].Value.Overlaps(list[j].Value), list[i].Key + " overlaps " + list[j].Key);
            }
            var zones = WorldZone.All.Select(z => z.ZoneId).ToList();
            foreach (var id in new[] { WorldZones.HexTurn, WorldZones.HexStore, WorldZones.HexDev, WorldZones.HexAwards, WorldZones.HexBuild, WorldZones.HexTrade, WorldZones.HexRaider, WorldZones.HexEnd, WorldZones.HexFeed, WorldZones.HexLeave, WorldZones.HexPlague })
                CollectionAssert.Contains(zones, id);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator BankAndPortTrade_ViaTheBarterMat()
        {
            yield return ToMyTurn();
            var hand = T.Peoples[0].Hand;
            Fill(hand, 0); hand[0] = 12; hand[2] = 12;
            yield return null; yield return null;
            // bank 4:1: the trader unrolls the mat, a basket gives, the wanted good is set opposite
            Assert.AreEqual(WorldTapResult.Inspected, _hud.TapLedge("trader"));
            Assert.IsTrue(_hud.MatOpen);
            _hud.TapLedge("basket:0");
            Assert.AreEqual(0, _hud.BankGive);
            _hud.SelectBankGet(4);
            int rate = TableRules.TradeRate(S, 0, Ware.Wood);
            Assert.AreEqual(rate, _hud.BankRate(0));
            var r1 = _hud.DoBankTrade();
            Assert.IsTrue(r1.Ok, r1.Message);
            Step(2);
            Assert.AreEqual(12 - rate, T.Peoples[0].Hand[0]);
            Assert.AreEqual(1, T.Peoples[0].Hand[4]);
            // the same trade sent again at once is stopped by the guard
            int blocked = _d.DuplicatesBlocked;
            var r2 = _hud.DoBankTrade();
            Assert.IsFalse(r2.Ok);
            Assert.AreEqual(blocked + 1, _d.DuplicatesBlocked);
            Step(2);
            Assert.AreEqual(12 - rate, T.Peoples[0].Hand[0], "traded once");
            // a jetty you own is a port: its rate is used
            TablePort port = null;
            foreach (var p in T.Ports) if (p.Good >= 0 || p.Good < 0) { port = p; break; }
            Assert.IsNotNull(port);
            T.CornerOwner[port.CornerA] = 0; T.CornerLevel[port.CornerA] = 1;
            yield return null;
            int g = port.Good >= 0 ? port.Good : 2;
            int pr = TableRules.TradeRate(S, 0, (Ware)g);
            Assert.Less(pr, 4, "a port gives a better rate");
            Assert.AreEqual(pr, _hud.BankRate(g), "the mat shows the port rate");
            hand[g] = 12;
            int get = g == 4 ? 3 : 4;
            yield return new WaitForSecondsRealtime(DuplicateWait);
            _hud.SelectBankGive(g); _hud.SelectBankGet(get);
            int before = T.Peoples[0].Hand[g], gotBefore = T.Peoples[0].Hand[get];
            var r3 = _hud.DoBankTrade();
            Assert.IsTrue(r3.Ok, r3.Message);
            Step(2);
            Assert.AreEqual(before - pr, T.Peoples[0].Hand[g]);
            Assert.AreEqual(gotBefore + 1, T.Peoples[0].Hand[get]);
            StringAssert.Contains("jett", string.Join(" ", GodBoardLedger.Trader(_d.FreshModel()).AllText()));
        }

        private const float DuplicateWait = GodBoardDirector.DuplicateWindow + 0.1f;

        [UnityTest, Timeout(240000)]
        public IEnumerator Envoy_AcceptAndDecline()
        {
            yield return ToMyTurn();
            var t = T;
            for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == 1 && t.CornerLevel[c] == 1) { t.CornerLevel[c] = 2; break; }
            t.Peoples[1].Hand[1] = 3; t.Peoples[2].Hand[2] = 3;
            t.Peoples[0].Hand[3] = 3; t.Peoples[0].Hand[4] = 3;
            int clay0 = t.Peoples[0].Hand[1], sheep0 = t.Peoples[0].Hand[3], grain0 = t.Peoples[0].Hand[2];
            var o1 = new TradeOffer { Id = t.NextOfferId++, From = 1, To = 0, TicksLeft = 150, Status = OfferStatus.Open }; o1.Give[1] = 1; o1.Get[3] = 1;
            var o2 = new TradeOffer { Id = t.NextOfferId++, From = 2, To = 0, TicksLeft = 150, Status = OfferStatus.Open }; o2.Give[2] = 1; o2.Get[4] = 1;
            t.Offers.Add(o1); t.Offers.Add(o2);
            yield return null; yield return null;
            Assert.IsTrue(_hud.LedgeRects.ContainsKey("envoy:" + o1.Id) && _hud.LedgeRects.ContainsKey("envoy:" + o2.Id), "two envoys on the ledge");
            // tap once: the bundle is shown, nothing is traded
            Assert.AreEqual(WorldTapResult.Previewed, _hud.TapLedge("envoy:" + o1.Id));
            StringAssert.Contains("clay brick", string.Join(" ", LedgerCard.Content.AllText()));
            Step(2);
            Assert.AreEqual(clay0, t.Peoples[0].Hand[1]);
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("envoy:" + o1.Id));
            Step(2);
            Assert.AreEqual(clay0 + 1, t.Peoples[0].Hand[1]);
            Assert.AreEqual(sheep0 - 1, t.Peoples[0].Hand[3]);
            // wave the other off
            yield return null;
            _hud.TapLedge("wave:" + o2.Id);
            Step(2);
            Assert.AreEqual(grain0, t.Peoples[0].Hand[2], "declined: nothing changed hands");
            Assert.AreEqual(0, _hud.IncomingOffers().Count, "no envoy waits");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator DiscardSack_BeforeTheTimer_AndAfter()
        {
            yield return ToMyTurn();
            var t = T; var hand = t.Peoples[0].Hand;
            Fill(hand, 0); hand[0] = 4; hand[1] = 4;
            t.Phase = TablePhase.Discard; t.DiscardOwed = new int[t.PeopleCount]; t.DiscardOwed[0] = 4; t.DiscardTicksLeft = t.Tuning.DiscardTicks;
            yield return null; yield return null;
            Assert.IsTrue(_hud.DiscardVisible);
            Assert.IsTrue(_hud.LedgeRects.ContainsKey("sack"), "the sack is on the ledge");
            StringAssert.Contains("give up 4", string.Join(" ", LedgerOf("sack")));
            // an incomplete sack is refused with the number still owed
            _hud.TapLedge("basket:0");
            Assert.AreEqual(WorldTapResult.Refused, _hud.TapLedge("sack"));
            StringAssert.Contains("3 more", string.Join(" ", LedgerCard.Content.AllText()));
            _hud.TapLedge("basket:0"); _hud.TapLedge("basket:1"); _hud.TapLedge("basket:1");
            Assert.AreEqual(4, _hud.DiscardChosen);
            _hud.TapLedge("basket:1");
            Assert.AreEqual(4, _hud.DiscardChosen, "no more than owed goes in");
            Assert.AreEqual(WorldTapResult.Previewed, _hud.TapLedge("sack"));
            Assert.AreEqual(8, TableRules.HandSize(hand), "previewing gives nothing up");
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("sack"));
            Step(2); yield return null;
            Assert.AreEqual(2, hand[0]); Assert.AreEqual(2, hand[1]);
            Assert.IsFalse(_hud.DiscardVisible);
            // After the timer: the default chooser gives up the cards
            t.Phase = TablePhase.Discard; t.DiscardOwed = new int[t.PeopleCount]; t.DiscardOwed[0] = 2; t.DiscardTicksLeft = 3;
            yield return null; yield return null;
            Assert.IsTrue(_hud.DiscardVisible);
            _hud.TapLedge("basket:0");
            Step(6); yield return null;
            Assert.AreNotEqual(TablePhase.Discard, t.Phase, "the discard is over");
            Assert.AreEqual(2, TableRules.HandSize(hand), "the adviser's choice took two cards");
            Assert.IsFalse(_hud.DiscardVisible);
            Assert.AreEqual(0, _hud.DiscardChosen, "the half-filled sack was emptied");
        }

        private List<string> LedgerOf(string what)
        {
            var o = _hud.LedgeObjects.ContainsKey(what) ? _hud.LedgeObjects[what] : null;
            return GodBoardLedger.Sack(_d.FreshModel(), _hud.DiscardChosen).AllText().ToList();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator EndTurn_NeedsTwoTaps_AndGoesOncePerTurn()
        {
            yield return ToMyTurn();
            int turnIndex = T.TurnIndex, round = T.Round;
            Assert.AreEqual(WorldTapResult.Previewed, _hud.TapLedge("hourglass"));
            Assert.IsTrue(_hud.HourglassTipped, "the glass tips");
            Step(2);
            Assert.AreEqual(0, T.Current, "one tap ends nothing");
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("hourglass"));
            Assert.IsFalse(_hud.HourglassTipped);
            Step(2);
            Assert.IsTrue(T.Current != 0 || T.Round != round, "the turn ended");
            // the guard: a second end turn for the same turn is never sent
            int blocked = _d.DuplicatesBlocked;
            var again = _d.EndTurn();
            Assert.IsFalse(again.Ok);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator EndTurn_Command_IsStoppedTwiceInOneTurn()
        {
            yield return ToMyTurn();
            Assert.IsTrue(_d.EndTurn().Ok);
            int blocked = _d.DuplicatesBlocked;
            var second = _d.EndTurn();   // the sim has not stepped yet: the same turn
            Assert.IsFalse(second.Ok);
            Assert.AreEqual(blocked + 1, _d.DuplicatesBlocked, "the guard stopped it before the sim saw it");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LeavingFreezesTheSand()
        {
            yield return ToMyTurn();
            Step(40);
            long active = T.ActiveTicks; int sand = T.Sand;
            Assert.AreEqual(WorldTapResult.Previewed, _hud.TapLedge("lamb"));
            Assert.AreEqual(BoardView.Board, _d.View, "preview first");
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("lamb"));
            Step(1);
            yield return WaitView(BoardView.Altar);
            Assert.IsTrue(T.Paused);
            Step(200);
            Assert.AreEqual(sand, T.Sand, "the sand is frozen while you are away");
            Assert.AreEqual(active, T.ActiveTicks);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ClearView_ShowsTheSameNumbers_AsTheLedge()
        {
            yield return ToMyTurn();
            var hand = T.Peoples[0].Hand;
            hand[0] = 3; hand[1] = 2; hand[2] = 4; hand[3] = 1; hand[4] = 0;
            yield return null; yield return null;
            GodBoardSettings.Override = false;
            yield return null; yield return null;
            var world = new List<string>(_hud.Texts);
            Assert.IsTrue(world.Contains("[bench:0]"), "world first: the ledge is drawn: " + string.Join("|", world));
            Assert.IsFalse(world.Contains("[Road]"));
            var vm = _hud.Model;
            var tablet = string.Join("\n", GodBoardLedger.Baskets(vm).AllText());
            StringAssert.Contains("Logs: 3", tablet); StringAssert.Contains("Clay bricks: 2", tablet); StringAssert.Contains("Sheaves: 4", tablet);
            GodBoardSettings.Override = true;
            yield return null; yield return null;
            var clear = new List<string>(_hud.Texts);
            Assert.IsTrue(clear.Contains("[Road]") && clear.Contains("[Camp]"), "Clear view: the costs card is back: " + string.Join("|", clear));
            Assert.IsFalse(clear.Contains("[bench:0]"));
            for (int g = 0; g < 5; g++) Assert.IsTrue(clear.Contains(hand[g].ToString()), "the hand fan shows " + hand[g]);
            for (int p = 0; p < 4; p++) Assert.IsTrue(clear.Contains(_hud.Model.Points[p].ToString()), "the points badge shows " + _hud.Model.Points[p]);
            Assert.AreSame(_d.Model, _hud.Model, "both layers read one view model");
            Assert.AreEqual(vm.Hand.Sum(), _hud.Model.HandSize);
        }
    }
}
