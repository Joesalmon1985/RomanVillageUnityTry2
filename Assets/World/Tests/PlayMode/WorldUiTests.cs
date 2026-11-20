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
    /// <summary>WU-0 in play: the altar's two taps, the ledger at 844 x 390, the zone outline off by default and
    /// editor only, and the guard while the camera moves.</summary>
    public class WorldUiTests
    {
        private const string TestPause = "WorldUiTest";
        private WorldBootstrap _boot;
        private GodBoardDirector _d;
        private WorldState S => _boot.Sim.State;

        [SetUp]
        public void SetUp()
        {
            GodBoardDirector.RiseSeconds = 0.05f;
            PointerInput.InputEnabled = true; // an earlier test's board rise leaves the pointer off across scene loads
        }
        [TearDown]
        public void TearDown()
        {
            GodBoardDirector.RiseSeconds = 2.4f;
            ScreenSafeArea.Override = null;
            WorldActions.Cancel();
#if UNITY_EDITOR
            ZoneOutlineTool.Enabled = false;
#endif
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
            Assert.IsNotNull(_d, "god board installed");
        }

        private TempleAltar Altar => _d.OwnTemple.GetComponentInChildren<TempleAltar>();

        /// <summary>Stand a few metres in front of the altar, facing it, and let the camera settle.</summary>
        private IEnumerator StandAtAltar(float metres = 9f)
        {
            var a = _d.OwnTemple.AltarSpot.position;
            var w = PlayerWalker.Instance;
            var dir = w.Position - a; dir.y = 0;
            if (dir.sqrMagnitude < 1f) dir = Vector3.forward;
            var feet = a + dir.normalized * metres; feet.y = PlayerWalker.GroundHeight(feet);
            w.Teleport(feet, BoardSpace.YawOf(a - feet), 8f);
            yield return null;
            yield return new WaitForSecondsRealtime(0.35f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Altar_FirstTapPreviews_SecondConfirmsOnce()
        {
            yield return Start();
            yield return StandAtAltar();
            var altar = Altar;
            Assert.IsNotNull(altar);
            Assert.IsTrue(WorldActions.Registered.Contains(altar), "the altar is a registered world action");
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapObject(altar));
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
            Assert.IsTrue(altar.Highlighted, "the altar glows in preview");
            Assert.IsTrue(LedgerCard.IsOpen);
            Assert.AreEqual(TempleAltar.Question, LedgerCard.Content.Question);
            Assert.AreEqual(0, _d.SacrificeCount, "nothing sent on preview");
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase);
            // A double tap's second half is dropped.
            Assert.AreEqual(TapOutcome.Ignored, PointerInput.TapObject(altar));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(TapOutcome.Confirm, PointerInput.TapObject(altar));
            Assert.AreEqual(1, _d.SacrificeCount, "one Sacrifice per confirm");
            Assert.IsFalse(altar.Highlighted);
            // Rapid taps after the command send nothing more.
            PointerInput.TapObject(altar); PointerInput.TapObject(altar);
            Assert.AreEqual(1, _d.SacrificeCount);
            _boot.Clock.StepOnce(); _boot.Clock.StepOnce();
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase, "the table opened");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Altar_TapElsewhereCancels_AndFarIsRefused()
        {
            yield return Start();
            yield return StandAtAltar();
            var altar = Altar;
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapObject(altar));
            var ground = altar.transform.position + Vector3.right * 30f;
            Assert.AreEqual(TapOutcome.Cancelled, PointerInput.TapGround(ground));
            Assert.AreEqual(WorldActionState.Idle, WorldActions.State);
            Assert.IsFalse(altar.Highlighted);
            Assert.IsFalse(LedgerCard.IsOpen);
            Assert.AreEqual(0, _d.SacrificeCount);
            // Far away: refused, said on the ledger, nothing previewed.
            yield return StandAtAltar(GodBoardDirector.AltarRange + 12f);
            Assert.AreEqual(TapOutcome.Refused, PointerInput.TapObject(altar));
            Assert.AreEqual(WorldActionState.Refused, WorldActions.State);
            Assert.IsTrue(LedgerCard.IsOpen);
            StringAssert.Contains("closer", LedgerCard.Content.Refusal);
            Assert.IsFalse(altar.Highlighted);
            yield return new WaitForSecondsRealtime(0.4f);
            PointerInput.TapObject(altar);
            Assert.AreEqual(0, _d.SacrificeCount);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Altar_KeyboardAlternative_TwoPresses()
        {
            yield return Start();
            yield return StandAtAltar();
            Assert.AreEqual(WorldTapResult.Previewed, WorldActionController.KeyPress());
            Assert.AreEqual(0, _d.SacrificeCount);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State, "still previewing");
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActionController.KeyPress());
            Assert.AreEqual(1, _d.SacrificeCount);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TapWhileCameraMoves_IsIgnored()
        {
            yield return Start();
            yield return StandAtAltar();
            var altar = Altar;
            WorldActions.NoteCameraMove(0.5f);
            Assert.AreEqual(TapOutcome.Ignored, PointerInput.TapObject(altar));
            Assert.AreEqual(WorldActionState.Idle, WorldActions.State);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapObject(altar));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ZoneOutline_OffByDefault_AndEditorOnly()
        {
            yield return Start();
#if UNITY_EDITOR
            Assert.IsFalse(ZoneOutlineTool.Enabled, "off by default");
            Assert.IsTrue(GodToolbar.Tools.Any(t => t.Id == ZoneOutlineTool.ToolId), "toggled from the F1 tools");
            Assert.IsNotNull(_boot.GetComponent<ZoneOutlineOverlay>());
            yield return null; yield return null;
            Assert.AreEqual(0, ZoneOutlineOverlay.ZonesDrawn, "nothing drawn while off");
            Assert.AreEqual(0, ZoneOutlineOverlay.ObjectsDrawn);
#else
            // A development player: the overlay type must not exist.
            Assert.IsNull(typeof(WorldZone).Assembly.GetType("World.Game.ZoneOutlineTool"), "the overlay is compiled out of builds");
            Assert.IsNull(typeof(WorldZone).Assembly.GetType("World.Game.ZoneOutlineOverlay"));
            Assert.IsFalse(GodToolbar.Tools.Any(t => t.Id == "zones"));
#endif
            var tagged = WorldZone.All.FirstOrDefault(z => z.ZoneId == WorldZones.HubTemple);
            Assert.IsNotNull(tagged, "the player's temple is tagged HUB-TEMPLE");
            Assert.AreSame(_d.OwnTemple.gameObject, tagged.gameObject);
        }

#if UNITY_EDITOR
        [UnityTest, Timeout(120000)]
        public IEnumerator LedgerCard_ReadableAt844x390()
        {
            yield return Start();
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint w0, out uint h0);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(844, 390, "WU-0 844x390");
            try
            {
                for (int i = 0; i < 10 && Screen.height != 390; i++) yield return null;
                ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(Screen.width, Screen.height);
                yield return StandAtAltar();
                Assert.AreEqual(TapOutcome.Preview, PointerInput.TapObject(Altar));
                yield return new WaitForSecondsRealtime(LedgerCardView.UnfoldSeconds + 0.1f);
                var cam = ViewSwitcher.Instance.ActiveCamera;
                var L = LedgerCard.CurrentLayout(cam);
                Assert.GreaterOrEqual(L.FontSize, LedgerLayout.MinFont, "text size at " + Screen.width + "x" + Screen.height);
                var safe = ScreenSafeArea.Current();
                Assert.IsTrue(L.Card.xMin >= safe.xMin && L.Card.xMax <= safe.xMax && L.Card.yMin >= safe.yMin && L.Card.yMax <= safe.yMax, "card " + L.Card + " in safe " + safe);
                Assert.GreaterOrEqual(L.Card.height, WorldHitTarget.MinSide(Screen.height));
                if (Screen.height == 390)
                {
                    if (LedgerCard.LastRect.width > 0) Assert.AreEqual(L.Card, LedgerCard.LastRect, "the card drawn is the card laid out");
                    // Tapping the card confirms, like the altar itself.
                    yield return new WaitForSecondsRealtime(0.3f);
                    var c = L.Card.center;
                    Assert.AreEqual(TapOutcome.Confirm, PointerInput.TapAt(new Vector2(c.x, Screen.height - c.y)));
                    Assert.AreEqual(1, _d.SacrificeCount);
                }
                else Assert.Inconclusive("the game view did not take 844 x 390 (" + Screen.width + "x" + Screen.height + "); layout checked at the real size");
            }
            finally { UnityEditor.PlayModeWindow.SetCustomRenderingResolution(w0, h0, "restore"); }
        }
#endif
    }
}
