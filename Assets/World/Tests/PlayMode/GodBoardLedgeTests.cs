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
            yield return WaitMat();
            Assert.IsTrue(_hud.LedgeRects.ContainsKey("mat-get:4") && _hud.Rig.Items.ContainsKey("mat-get:4"), "the mat is unrolled with the goods to get");
            _hud.TapLedge("basket:0");
            Assert.AreEqual(0, _hud.BankGive);
            yield return null;
            Assert.AreEqual(Mathf.Clamp(12 - TableRules.TradeRate(S, 0, Ware.Wood), 0, AltarLedgeRig.BasketCap), _hud.Rig.VisibleGoods(0), "the goods to give moved from the basket onto the mat");
            _hud.TapLedge("mat-get:4");
            Assert.AreEqual(4, _hud.BankGet, "a good on the mat is the good to get");
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
            Assert.IsNotNull(_hud.Rig, "world first: the 3D ledge is up");
            Assert.IsTrue(_hud.Rig.Shown && _hud.Rig.Items.ContainsKey("bench:0"), "world first: the builder's bench stands on the ledge");
            Assert.IsFalse(world.Contains("[Road]"));
            Assert.IsFalse(world.Any(x => x.StartsWith("[bench") || x.StartsWith("[basket")), "no IMGUI ledge in the world-first view");
            var vm = _hud.Model;
            var tablet = string.Join("\n", GodBoardLedger.Baskets(vm).AllText());
            StringAssert.Contains("Logs: 3", tablet); StringAssert.Contains("Clay bricks: 2", tablet); StringAssert.Contains("Sheaves: 4", tablet);
            GodBoardSettings.Override = true;
            yield return null; yield return null;
            var clear = new List<string>(_hud.Texts);
            Assert.IsTrue(clear.Contains("[Road]") && clear.Contains("[Camp]"), "Clear view: the costs card is back: " + string.Join("|", clear));
            Assert.IsFalse(clear.Contains("[bench:0]"));
            Assert.IsFalse(_hud.Rig.Shown, "Clear view hides the 3D ledge");
            Assert.IsFalse(_hud.Rig.Cam.enabled);
            for (int g = 0; g < 5; g++) Assert.IsTrue(clear.Contains(hand[g].ToString()), "the hand fan shows " + hand[g]);
            for (int p = 0; p < 4; p++) Assert.IsTrue(clear.Contains(_hud.Model.Points[p].ToString()), "the points badge shows " + _hud.Model.Points[p]);
            Assert.AreSame(_d.Model, _hud.Model, "both layers read one view model");
            Assert.AreEqual(vm.Hand.Sum(), _hud.Model.HandSize);
        }

        // ------------------------------------------------------------ WU-2b: the ledge is real 3D objects

        [UnityTest, Timeout(240000)]
        public IEnumerator WorldFirst_DrawsNoImguiLedge_OnlyThe3DLedge()
        {
            yield return ToMyTurn();
            GodBoardSettings.Override = false;
            yield return null; yield return null;
            Assert.AreEqual(0, _hud.LedgeGuiDraws, "the old IMGUI ledge is never drawn");
            Assert.IsFalse(_hud.Texts.Any(x => x.StartsWith("[basket") || x.StartsWith("[stake") || x.StartsWith("[hourglass") || x.StartsWith("[trader")), "no ledge buttons in the HUD layer: " + string.Join("|", _hud.Texts));
            var rig = _hud.Rig;
            Assert.IsNotNull(rig); Assert.IsTrue(rig.Shown); Assert.IsTrue(rig.Cam.enabled, "the ledge camera renders");
            Assert.AreEqual(1 << AltarLedgeRig.Layer, rig.Cam.cullingMask, "the ledge camera sees only the ledge");
            Assert.AreEqual(0, _d.BoardCam.cullingMask & (1 << AltarLedgeRig.Layer), "the board camera never sees the ledge");
            Assert.Greater(rig.Cam.depth, _d.BoardCam.depth, "the ledge is layered in front of the island");
            foreach (var id in _hud.LedgeRects.Keys)
            {
                Assert.IsTrue(rig.Items.ContainsKey(id), id + " is an object on the ledge");
                var it = rig.Items[id];
                Assert.Greater(it.GetComponentsInChildren<MeshRenderer>().Length, 1, id + " is modelled (more than one mesh)");
                Assert.IsNotNull(it.Hit, id + " has a hit collider");
                Assert.AreEqual(AltarLedgeRig.Layer, it.Hit.gameObject.layer);
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LedgeColliders_AreAtLeastTheHitTarget_At844x390_AndRaycastsPickThem()
        {
            yield return ToMyTurn();
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint ow, out uint oh);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(844, 390, "WU-2b test 844x390");
            ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(844, 390);
            for (int i = 0; i < 10 && !(Screen.width == 844 && Screen.height == 390); i++) yield return null;
            yield return null; yield return null;
            try
            {
                if (Screen.width != 844 || Screen.height != 390) Assert.Inconclusive("the game view did not take 844x390 (" + Screen.width + "x" + Screen.height + ")");
                CheckColliders();
            }
            finally
            {
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution(ow, oh, "restore");
            }
#else
            CheckColliders();
#endif
        }

        private void CheckColliders()
        {
            float min = WorldHitTarget.MinSide(Screen.height);
            var rig = _hud.Rig;
            Assert.IsTrue(rig.Shown);
            int checkedN = 0;
            foreach (var kv in _hud.LedgeRects)
            {
                Assert.IsTrue(rig.HitRect(kv.Key, out var hr), kv.Key + " has a hit box on screen");
                Assert.GreaterOrEqual(hr.width, min - 0.5f, kv.Key + " collider wide enough at " + Screen.width + "x" + Screen.height + ": " + hr);
                Assert.GreaterOrEqual(hr.height, min - 0.5f, kv.Key + " collider tall enough: " + hr);
                var c = kv.Value.center;
                Assert.AreEqual(kv.Key, rig.Pick(new Vector2(c.x, Screen.height - c.y)), "a 3D raycast at the centre of " + kv.Key + " picks it");
                checkedN++;
            }
            Assert.GreaterOrEqual(checkedN, 18);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Baskets_VisibleContents_FollowTheHand()
        {
            yield return ToMyTurn();
            var hand = T.Peoples[0].Hand;
            hand[0] = 0; hand[1] = 3; hand[2] = 9; hand[3] = 14; hand[4] = 1;
            yield return null; yield return null;
            var rig = _hud.Rig;
            for (int g = 0; g < 5; g++) Assert.AreEqual(Mathf.Min(hand[g], AltarLedgeRig.BasketCap), rig.VisibleGoods(g), "basket " + g + " shows the hand");
            Assert.AreEqual(0, rig.Items["basket:0"].Dyn.childCount, "an empty basket holds no goods");
            hand[0] = 4; hand[1] = 0;
            yield return null; yield return null;
            Assert.AreEqual(4, rig.VisibleGoods(0), "the basket fills as the hand grows");
            Assert.AreEqual(0, rig.VisibleGoods(1), "and empties as it shrinks");
            int active = 0; foreach (Transform t in rig.Items["basket:0"].Dyn) if (t.gameObject.activeSelf) active++;
            Assert.AreEqual(4, active, "four logs in the wood basket");
            // the exact count is read on the tablet, never on the basket
            Assert.AreEqual(WorldTapResult.Inspected, _hud.TapLedge("basket:0"));
            StringAssert.Contains("Logs: 4", string.Join("|", LedgerCard.Content.AllText()));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ClearView_OneSetting_ShowsBoardHudAndPlagueButtonsTogether()
        {
            yield return ToMyTurn();
            GodBoardSettings.Override = null;
            bool saved = GodBoardSettings.ClearView;
            try
            {
                GodBoardSettings.ClearView = false;
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsFalse(PlagueHud.ClearView, "the plague reads the same setting");
                Assert.IsFalse(_hud.Texts.Contains("[Road]"));
                Assert.IsFalse(_hud.Texts.Any(x => x.StartsWith("[Plague")), "world first: no Plague button");
                Assert.IsNull(GodBoardHud.PlagueToggle);
                Assert.IsTrue(GodBoardHud.TotemAvailable, "the totem still works the overlay");
                bool was = GodBoardHud.TotemOn;
                _hud.TapLedge("totem");
                Assert.AreNotEqual(was, GodBoardHud.TotemOn, "the totem toggles the overlay");
                _hud.TapLedge("totem"); WorldActions.Cancel();
                // one toggle: both layers come back
                PlagueHud.ClearView = true;
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsTrue(GodBoardSettings.ClearView, "one setting is the source of truth");
                Assert.IsTrue(_hud.Texts.Contains("[Road]"), "the board HUD is back: " + string.Join("|", _hud.Texts));
                Assert.IsTrue(_hud.Texts.Any(x => x.StartsWith("[Plague")), "the Plague button is back: " + string.Join("|", _hud.Texts));
                Assert.IsFalse(_hud.Rig.Shown);
                GodBoardSettings.ClearView = false;
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsFalse(_hud.Texts.Any(x => x.StartsWith("[Plague")));
                Assert.IsTrue(_hud.Rig.Shown);
            }
            finally { GodBoardSettings.ClearView = saved; }
        }

        private IEnumerator WaitMat()
        {
            float t0 = Time.realtimeSinceStartup;
            while (_hud.MatRise < 0.999f || !_hud.LedgeRects.ContainsKey("mat-get:4")) { Assert.Less(Time.realtimeSinceStartup - t0, 5f, "the ledge never slid up for the mat"); yield return null; }
            for (int i = 0; i < 4; i++) yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Mat_LiesOnTheLedge_TheLedgeSlidesUp_AndTheIslandIsNotCovered()
        {
            yield return ToMyTurn();
            ScreenSafeArea.Override = null;
            float bandBefore = _hud.LedgeBand.y;
            _hud.TapLedge("trader");
            yield return WaitMat();
            var band = _hud.LedgeBand;
            Assert.Less(band.y, bandBefore, "the ledge slid up");
            Assert.GreaterOrEqual(_hud.MatRect.y, band.y - 0.5f, "the mat strip lies inside the ledge band");
            foreach (var id in new[] { "mat-give", "mat-get:0", "mat-get:4", "mat-deal", "mat-people:1", "mat-people:all" })
            {
                Assert.IsTrue(_hud.LedgeRects.TryGetValue(id, out var r), id + " is laid out");
                Assert.GreaterOrEqual(r.y, band.y - 0.5f, id + " is on the stone");
                Assert.LessOrEqual(r.yMax, band.yMax + 0.5f, id + " is on the stone");
                Assert.GreaterOrEqual(r.x, band.x - 0.5f); Assert.LessOrEqual(r.xMax, band.xMax + 0.5f, id + " stays in the ledge's width");
            }
            // the island is fitted above the band: the board camera ends where the ledge begins
            Assert.AreEqual(Screen.height - band.y, _d.BoardCam.pixelRect.y, 3f, "the island camera is shorter by the mat strip, never covered by it");
            // and it all folds back
            _hud.TapLedge("trader");
            float t0 = Time.realtimeSinceStartup;
            while (_hud.MatRise > 0.001f) { Assert.Less(Time.realtimeSinceStartup - t0, 5f); yield return null; }
            Assert.AreEqual(bandBefore, _hud.LedgeBand.y, 0.5f, "the ledge slid back down");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MatOffersToPeoples_PreviewThenSendOneOfferTrade()
        {
            yield return ToMyTurn();
            var hand = T.Peoples[0].Hand;
            Fill(hand, 0); hand[0] = 6; hand[4] = 0;
            yield return null; yield return null;
            _hud.TapLedge("trader");
            yield return WaitMat();
            // nothing chosen: the emblem refuses and says what to do
            _hud.TapLedge("mat-people:1");
            Assert.AreEqual(WorldActionState.Refused, WorldActions.State);
            WorldActions.Cancel();
            _hud.TapLedge("basket:0"); _hud.TapLedge("mat-get:4");
            int offers = T.Offers.Count;
            Assert.AreEqual(WorldTapResult.Previewed, _hud.TapLedge("mat-people:1"), "first tap previews");
            Assert.IsTrue(LedgerCard.IsOpen);
            StringAssert.Contains("1 log", string.Join(" ", LedgerCard.Content.AllText()));
            Step(2);
            Assert.AreEqual(offers, T.Offers.Count, "the preview sent nothing");
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("mat-people:1"), "second tap sends");
            Step(2);
            // the sim may turn the offer down (the peoples will not trade with the leader): then nothing was added and it says so
            if (T.Offers.Count == offers) StringAssert.Contains("trade", _hud.TradeMessage);
            else
            {
                var o = T.Offers[T.Offers.Count - 1];
                Assert.AreEqual(0, o.From); Assert.AreEqual(1, o.To);
                Assert.AreEqual(1, o.Give[0]); Assert.AreEqual(1, o.Get[4]);
            }
            // to every people: the same two taps send one OfferTrade for all (To = -1)
            WorldActions.Cancel(); LedgerCard.Hide();
            foreach (var oo in T.Offers) if (oo.Status == OfferStatus.Open) oo.Status = OfferStatus.Expired;
            offers = T.Offers.Count;
            Assert.AreEqual(WorldTapResult.Previewed, _hud.TapLedge("mat-people:all"));
            Step(2);
            Assert.AreEqual(offers, T.Offers.Count, "the preview sent nothing");
            Assert.AreEqual(WorldTapResult.Confirmed, _hud.TapLedge("mat-people:all"));
            Step(2);
            Assert.AreEqual(offers + 1, T.Offers.Count, "exactly one offer: " + _hud.TradeMessage);
            var oa = T.Offers[T.Offers.Count - 1];
            Assert.AreEqual(0, oa.From); Assert.AreEqual(-1, oa.To);
            Assert.AreEqual(1, oa.Give[0]); Assert.AreEqual(1, oa.Get[4]);
            Assert.AreEqual(0, _hud.OfferGive.Sum() + _hud.OfferGet.Sum(), "the old window was not used");
            Assert.IsFalse(_hud.TradeOpen && _hud.TradeTab == 1, "no old trade window");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Jetty_Tap_ShowsItsRateAsGoodsPictures_AndSendsNothing()
        {
            yield return ToMyTurn();
            Assert.Greater(S.Board.Ports.Count, 0);
            int offersBefore = T.Offers.Count; var handBefore = (int[])T.Peoples[0].Hand.Clone();
            int found = -1;
            for (int i = 0; i < S.Board.Ports.Count && found < 0; i++)
            {
                var sp = _d.ScreenOfPort(i);
                if (sp.x < 4 || sp.y < 4 || sp.x > Screen.width - 4 || sp.y > Screen.height - 4) continue;
                if (sp.y < Screen.height - _hud.LedgeBand.y) continue;   // behind the ledge
                if (_d.PortAt(sp) == i) found = i;
            }
            Assert.GreaterOrEqual(found, 0, "a jetty can be tapped on the board");
            var res = _d.TapAt(_d.ScreenOfPort(found));
            if (res != null && res.StartsWith("preview")) res = _d.TapAt(_d.ScreenOfPort(found));
            Assert.AreEqual("jetty " + found, res);
            Assert.IsTrue(LedgerCard.IsOpen);
            _d.PortInfo(found, out int good, out int rate, out bool held);
            Assert.AreEqual(rate, LedgerCard.Content.PicCount, "the rate is a picture row of goods");
            Assert.AreEqual(good, LedgerCard.Content.PicGood);
            StringAssert.Contains("jetty", LedgerCard.Content.Title.ToLowerInvariant());
            Assert.IsNull(LedgerText.Violation(LedgerCard.Content));
            Assert.AreEqual(WorldActionState.Inspect, WorldActions.State, "it only tells");
            Step(2);
            Assert.AreEqual(offersBefore, T.Offers.Count); CollectionAssert.AreEqual(handBefore, T.Peoples[0].Hand, "no command was sent");
            WorldActions.Cancel();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LedgerTablet_OfLedgeObjects_OpensAboveTheLedge_NeverOverIt()
        {
            yield return ToMyTurn();
            foreach (var id in new[] { "basket:2", "bench:1", "hourglass", "stake:1", "chronicler", "lamb" })
            {
                _hud.TapLedge(id);
                var cam = _d.BoardCam;
                var layout = LedgerCard.CurrentLayout(cam);
                Assert.IsTrue(LedgerCard.IsOpen, id);
                Assert.LessOrEqual(layout.Card.yMax, _hud.LedgeBand.y + 0.5f, id + ": the tablet does not unfold over the ledge");
                WorldActions.Cancel();
                LedgerCard.Hide();
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator FootprintStones_AreHidden_WhileTheCameraRisesToTheBoard()
        {
            var savedMode = FootprintStones.Mode;
            FootprintStones.Mode = FootprintStones.StoneMode.Always;
            try
            {
                yield return Start();
                var fs = FootprintStones.Instance;
                Assert.IsNotNull(fs, "the stones exist");
                yield return null; yield return null;
                Assert.IsTrue(fs.ShouldShow(), "on foot at the altar the stones show");
                GodBoardDirector.RiseSeconds = 3f;
                Assert.IsTrue(_d.TrySacrifice(true));
                Step(2);
                float t0 = Time.realtimeSinceStartup;
                while (_d.View != BoardView.Rising) { Assert.Less(Time.realtimeSinceStartup - t0, 5f); yield return null; }
                yield return null; yield return null;
                Assert.IsFalse(fs.ShouldShow(), "hidden while the camera rises");
                Assert.IsFalse(fs.Visible);
                yield return WaitView(BoardView.Board);
                yield return null;
                Assert.IsFalse(fs.ShouldShow(), "hidden on the board");
                _d.Leave();
                t0 = Time.realtimeSinceStartup;
                while (_d.View != BoardView.Altar) { Assert.Less(Time.realtimeSinceStartup - t0, 15f); yield return null; }
                yield return null;
                Assert.IsTrue(fs.ShouldShow(), "back at the altar the stones return");
            }
            finally { FootprintStones.Mode = savedMode; }
        }
    }
}
