using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;

namespace World.Game.Tests
{
    /// <summary>
    /// Opening vista (owner feedback, 21 Nov 2026; target layout approved the same day, Docs/design/OPENING_TARGET.md): a start with no
    /// arguments is the god board hub opening on the elevated vista; the screen's safe area is divided into the owner's six blocks at the
    /// phone, tablet and desktop ratios, each block's centre picks its place and each place stands inside its own block; a first tap on a
    /// block previews that place and a second tap takes the player there and starts its entry; taps work from the first frame, through
    /// the welcome; no step pads in the hub; a drag turns the view while the prince leads.
    /// </summary>
    public class OpeningVistaTests
    {
        private WorldBootstrap _boot;
        private static readonly string[] Zones = { WorldZones.HubTemple, WorldZones.HubCouncil, WorldZones.HubRhythm, WorldZones.HubHealer, WorldZones.HubCave, WorldZones.HubDuel };
        private static readonly Vector2Int[] Sizes = { new Vector2Int(844, 390), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) };

        [SetUp]
        public void SetUp()
        {
            WorldBootstrap.DefaultStart = GodBoardStart.Id;
            OpeningVista.Mode = VistaMode.On;
            GodBoardDirector.RiseSeconds = 0.05f;
            CouncilDirector.Disabled = true;
            PointerInput.InputEnabled = true;
            WorldActions.Cancel();
        }

        [TearDown]
        public void TearDown()
        {
            WorldBootstrap.DefaultStart = DevStart.LegacyId; // the rest of the suite (TestDefaults)
            OpeningVista.Mode = VistaMode.Off;
            OpeningVista.ScreenOverride = null;
            ScreenSafeArea.Override = null;
            GodBoardDirector.RiseSeconds = 2.4f;
            CouncilDirector.Disabled = false;
        }

        /// <summary>Loads the World scene as a plain launch does (no save, no developer start).</summary>
        private IEnumerator Launch()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            float t0 = Time.realtimeSinceStartup;
            while (!(OpeningVista.Instance != null && OpeningVista.Up)) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the vista did not open"); yield return null; }
            _boot = WorldBootstrap.Instance;
        }

        private static IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond()) { Assert.Less(Time.realtimeSinceStartup - t0, seconds, what); yield return null; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NoArgumentStart_IsTheGodBoardHub_OpeningOnTheVista_WithAWelcome()
        {
            yield return Launch();
            Assert.IsNull(DevStart.Current, "a plain launch, not a developer start");
            Assert.AreEqual(Ruleset.GodBoard, _boot.Sim.State.Ruleset, "a new game is the god board game");
            Assert.IsNotNull(GodBoardDirector.Instance, "the god board is there");
            Assert.IsTrue(HubInstaller.Layout != null && HubInstaller.Layout.Hub, "the hub is built");
            var v = OpeningVista.Instance;
            Assert.AreEqual(Zones.Length, v.Places.Count, "six places in the vista");
            foreach (var z in Zones) Assert.IsNotNull(v.Find(z), "place " + z);
            var w = PlayerWalker.Instance;
            Assert.IsTrue(w.HasLock(OpeningVista.Lock), "the walker waits while the player looks out over the camp");
            Assert.Greater(_boot.MainCamera.transform.position.y - w.Position.y, 15f, "the camera is up over the camp");
            // the prince welcomes and names the places one by one; no build pitch in a god board game
            var p = PrinceDirector.Instance;
            Assert.IsTrue(p == null || p.IsGodBoardGame, "the prince guides, he does not pitch builds");
            yield return Until(() => v.Pointed.Count >= 2, 40f, "the welcome points out the places");
            Assert.AreEqual(WorldZones.HubTemple, v.Pointed[0], "the welcome starts at the temple");
            Assert.IsTrue(p == null || p.FirstSceneStep >= PrinceDirector.SceneDone, "no woodcutter's hut scene");
            Assert.IsTrue(p == null || p.CurrentPhase == PrinceDirector.Phase.Waiting, "no build pitch (" + (p != null ? p.CurrentPhase.ToString() : "") + ")");
        }

        /// <summary>Simulates a screen: its size, and a notch safe area on the phone shape (null = the whole screen is safe).</summary>
        private static void SimulateScreen(Vector2Int size, bool notch)
        {
            OpeningVista.ScreenOverride = size;
            ScreenSafeArea.Override = notch ? ScreenSafeArea.SimulatedNotch(size.x, size.y) : (Rect?)null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EachBlock_PicksItsPlace_AndEachPlaceStandsInItsBlock_AtThreeRatios()
        {
            yield return Launch();
            var v = OpeningVista.Instance;
            Assert.IsTrue(HubInstaller.Layout.Vista.Valid, "a composed pose fits this board (" + HubInstaller.Layout.Vista + ")");
            var screens = new[] { (new Vector2Int(1920, 1080), false), (new Vector2Int(844, 390), true), (new Vector2Int(1024, 768), false) };
            foreach (var (size, notch) in screens)
            {
                SimulateScreen(size, notch);
                var safe = OpeningVista.SafeGui();
                foreach (var z in Zones)
                {
                    var p = v.Find(z);
                    Assert.IsNotNull(p, z);
                    // the block's centre picks the place, and so do points near its corners
                    var block = OpeningVista.BlockRect(z);
                    Assert.AreSame(p, v.RegionAt(block.center), z + ": its block's centre picks it at " + size);
                    foreach (var k in new[] { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f), new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.9f) })
                        Assert.AreSame(p, v.RegionAt(new Vector2(block.xMin + k.x * block.width, block.yMin + k.y * block.height)), z + ": inside its block at " + size);
                    // the place's centre stands inside its own block, inside the safe area
                    var c = v.ScreenCentre(p);
                    Assert.IsTrue(block.Contains(c), z + " centre " + c + " is inside its block " + block + " at " + size);
                    Assert.IsTrue(safe.Contains(new Vector2(c.x, size.y - c.y)), z + " centre inside the safe area at " + size);
                    Assert.AreSame(p, v.RegionAt(c), z + ": its own centre picks it at " + size);
                }
                // the blocks divide the whole screen: every point picks a place, and each place gets its block's share
                var counts = v.Places.ToDictionary(p => p, p => 0);
                for (int y = 0; y < 20; y++)
                    for (int x = 0; x < 40; x++)
                    {
                        var at = v.RegionAt(new Vector2((x + 0.5f) / 40f * size.x, (y + 0.5f) / 20f * size.y));
                        Assert.IsNotNull(at, "every point of the screen is a region");
                        counts[at]++;
                    }
                foreach (var kv in counts) Assert.Greater(kv.Value, 800 / 10, kv.Key.Zone + " has a block of its own at " + size + " (" + kv.Value + " of 800 samples)");
            }
            // the far row is far, the near row near: the council's longhouse is the biggest of the far row
            SimulateScreen(new Vector2Int(1920, 1080), false);
            float Height(string z) => v.ScreenRect(v.Find(z)).height;
            Assert.Greater(Height(WorldZones.HubCouncil), Height(WorldZones.HubTemple), "the council stands bigger than the temple");
            Assert.Greater(Height(WorldZones.HubCouncil), Height(WorldZones.HubRhythm), "the council stands bigger than the drums");
            Assert.IsTrue(CouncilDirector.Instance.Site.HasHall, "the council has its longhouse");
            OpeningVista.ScreenOverride = null; ScreenSafeArea.Override = null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FirstFrame_IsTheVista_AndATapDuringTheWelcomePreviews()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            float t0 = Time.realtimeSinceStartup;
            while (OpeningVista.Instance == null) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "no vista"); yield return null; }
            var v = OpeningVista.Instance;
            var cam = WorldBootstrap.Instance.Avatar.Camera;
            Assert.Less(Vector3.Distance(cam.transform.position, v.CameraPosition), 0.01f, "the camera takes the vista's pose as soon as the hub is built");
            yield return new WaitForEndOfFrame(); // the first frame that renders
            Assert.IsTrue(OpeningVista.Up, "the vista is up by the end of the first frame");
            Assert.Less(Vector3.Distance(cam.transform.position, v.CameraPosition), 0.01f, "the first rendered frame is the vista");
            // the welcome is speaking; a tap now previews at once and ends the welcome
            yield return Until(() => v.Welcoming, 10f, "the welcome starts");
            yield return null;
            var z = WorldZones.HubHealer;
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapAt(OpeningVista.BlockRect(z).center), "a tap during the welcome previews");
            Assert.AreSame(v.Find(z), WorldActions.Machine.Current, "the previewed place");
            yield return Until(() => !v.Welcoming, 3f, "the tap ends the welcome");
            WorldActions.Cancel();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator EachRegion_FirstTapPreviews_SecondTapGoesThere_AndBackToTheVista()
        {
            yield return Launch();
            var v = OpeningVista.Instance;
            var w = PlayerWalker.Instance;
            // the duel last: Mira walks over to the ring and the duel starts later
            foreach (var zone in new[] { WorldZones.HubHealer, WorldZones.HubCave, WorldZones.HubRhythm, WorldZones.HubCouncil, WorldZones.HubTemple, WorldZones.HubDuel })
            {
                yield return Until(() => OpeningVista.Up, 20f, "the vista is up before " + zone);
                yield return new WaitForSecondsRealtime(0.7f); // the tap guard after the last command
                var p = v.Find(zone);
                var at = OpeningVista.BlockRect(zone).center; // anywhere in the place's block
                Assert.AreEqual(TapOutcome.Preview, PointerInput.TapAt(at), zone + ": the first tap previews");
                Assert.AreSame(p, WorldActions.Machine.Current, zone + ": the preview is that place");
                Assert.IsTrue(p.Lit, zone + ": the place lights up");
                Assert.AreEqual(WorldActionState.Preview, WorldActions.State, zone + ": the ledger card names it");
                Assert.IsTrue(OpeningVista.Up, zone + ": still looking out after one tap");
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(TapOutcome.Confirm, PointerInput.TapAt(at), zone + ": the second tap goes there");
                yield return Until(() => v.State == VistaState.Off, 10f, zone + ": the camera glides down");
                Assert.AreSame(p, v.LastArrived, zone + ": arrived at the place");
                Assert.Less(BoardSpace.Flat(w.Position - p.Centre), p.Radius + 9f, zone + ": the player stands at the place");
                Assert.IsFalse(w.HasLock(OpeningVista.Lock), zone + ": walking view");
                yield return null; yield return null;
                switch (zone)
                {
                    case WorldZones.HubTemple:
                        Assert.AreNotEqual(BoardView.Altar, GodBoardDirector.Instance.View, "the temple: the offering opens the god board");
                        GodBoardDirector.Instance.Leave();
                        yield return Until(() => GodBoardDirector.Instance.View == BoardView.Altar, 30f, "back from the board");
                        break;
                    case WorldZones.HubRhythm:
                        Assert.IsNotNull(RhythmPartyEncounter.Current, "the dance circle: the dance starts");
                        EncounterHost.Instance.Abort();
                        yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the dance ends");
                        break;
                    case WorldZones.HubCouncil:
                        // with no dispute called the steward says so honestly; with one the council opens
                        Assert.IsTrue(EncounterHost.Instance.IsRunning || WorldActions.State == WorldActionState.Disabled || WorldActions.State == WorldActionState.Refused,
                            "the tower: the council opens or the steward says why not");
                        if (EncounterHost.Instance.IsRunning) { EncounterHost.Instance.Abort(); yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the council ends"); }
                        WorldActions.Cancel();
                        break;
                    case WorldZones.HubDuel:
                        Assert.AreNotEqual(DuelRing.Errand.None, DuelRing.Instance.State, "the ring: Mira comes over for the duel");
                        break;
                    default: // the healer and Mira answer in their bubbles
                        Assert.Greater(SpeechBubbles.OpenHandles().Count(), 0, zone + ": someone speaks to the player");
                        break;
                }
                foreach (int h in SpeechBubbles.OpenHandles().ToArray()) SpeechBubbles.Close(h);
                if (DialoguePanel.IsOpen) DialoguePanel.Close();
                if (zone == WorldZones.HubDuel) break;
                yield return null;
                Assert.IsTrue(OpeningVista.TryReturn(), zone + ": back to the vista (Esc or a tap on the sky)");
                yield return Until(() => OpeningVista.Up, 10f, zone + ": the camera rises back");
            }
            // a late duel start takes the camera from the vista safely
            yield return Until(() => OpeningVista.TryReturn(), 5f, "back up after the ring");
            yield return Until(() => EncounterHost.Instance.IsRunning, 60f, "the duel starts when Mira arrives");
            yield return null; yield return null;
            Assert.AreEqual(VistaState.Off, v.State, "the duel took the camera from the vista");
            EncounterHost.Instance.Abort();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NoStepPads_InTheHub_TheBandTaps_AndStripsDoNotMove()
        {
            yield return Launch();
            var v = OpeningVista.Instance;
            var savedMode = FootprintStones.Mode;
            try
            {
                FootprintStones.Mode = FootprintStones.StoneMode.Always;
                yield return null;
                Assert.IsFalse(TouchStrips.StripsMove, "on the surface the strips are not moves");
                Assert.IsTrue(FootprintStones.Instance == null || !FootprintStones.Instance.ShouldShow(), "no step pads drawn over the vista");
                yield return new WaitForSecondsRealtime(0.5f); // the tap guard after the camera took up the vista
                // a tap in the old strip band picks a region (it is not swallowed as a move)
                var band = new Vector2(Screen.width * 0.5f, Screen.height * 0.1f);
                var o = PointerInput.TapAt(band);
                Assert.AreEqual(TapOutcome.Preview, o, "a tap at the bottom of the screen previews a place");
                WorldActions.Cancel();
                v.Leave();
                yield return null;
                Assert.IsTrue(FootprintStones.Instance == null || !FootprintStones.Instance.ShouldShow(), "no step pads drawn in the walking view");
                var w = PlayerWalker.Instance;
                var cell = w.Cell; int heading = w.Heading;
                TouchStrips.SimulatePress(2); yield return null; yield return null;
                TouchStrips.SimulatePress(4); yield return null; yield return null;
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.AreEqual(cell, w.Cell, "strip 2 does not step on the surface");
                Assert.AreEqual(heading, w.Heading, "strip 4 does not turn on the surface");
                int free = -1;
                for (int h = 0; h < 6 && free < 0; h++) if (!WalkGrid.IsStepBlocked(cell, cell + WalkLattice.Dir(h))) free = h;
                Assert.GreaterOrEqual(free, 0, "a free neighbour");
                w.Teleport(cell, free);
                w.Forward();
                yield return Until(() => !w.IsGliding, 5f, "a step");
                Assert.AreNotEqual(cell, w.Cell, "the walker still moves (keys, tap-to-walk)");
            }
            finally { FootprintStones.Mode = savedMode; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Drag_TurnsTheView_WhileThePrinceLeads_AndWhileLocked()
        {
            yield return Launch();
            OpeningVista.Instance.Leave();
            yield return null;
            var w = PlayerWalker.Instance;
            var prince = PrinceDirector.Instance != null ? PrinceDirector.Instance.Prince : null;
            Assert.IsNotNull(prince, "the prince");
            PrinceDirector.Instance.enabled = false;
            // the prince leads: he walks off and the player follows him
            var away = w.Position + w.transform.forward * 30f;
            prince.WalkTo(away);
            w.Follow(prince);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNotNull(w.Following, "the player follows the prince");
            float camYaw0 = w.Camera.transform.eulerAngles.y;
            var start = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            DragLook.Begin(start, true);
            DragLook.Move(start + new Vector2(DragLook.StepPixels * 0.3f, 0));
            DragLook.Move(start + new Vector2(DragLook.StepPixels * 1.5f, 0));
            Assert.AreEqual(DragLook.Kind.FreeLook, DragLook.Mode, "while led, a drag is free look");
            yield return null;
            Assert.Less(w.LookOffset, -50f, "a drag to the right turns the view left while the prince leads");
            Assert.IsNotNull(w.Following, "the lead goes on while the player looks round");
            // The body keeps turning towards the leader while led, so compare the camera with the body, not with its start.
            float camYaw1 = w.Camera.transform.eulerAngles.y;
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(w.Yaw, camYaw1)), 40f, "the camera looks away from the body's heading (camera " + camYaw0 + " -> " + camYaw1 + ", body " + w.Yaw + ")");
            DragLook.End();
            // locked (someone talking holds the player): the view still turns
            w.StopAuto(); prince.StopWalking();
            w.AddLock("VistaTest");
            float before = w.LookOffset;
            DragLook.Begin(start, true);
            DragLook.Move(start - new Vector2(DragLook.StepPixels * 1.2f, 0));
            Assert.AreEqual(DragLook.Kind.FreeLook, DragLook.Mode, "locked, a drag is free look");
            Assert.Greater(w.LookOffset, before + 40f, "a drag to the left turns the view right while locked");
            DragLook.End();
            w.RemoveLock("VistaTest");
            // free again: the look folds into the walker's heading on the lattice
            yield return Until(() => Mathf.Abs(w.LookOffset) < 0.01f && !w.IsGliding, 5f, "the look folds into the heading");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(w.Yaw, WalkLattice.Yaw(w.Heading))), 0.5f, "the walker stands on a lattice heading again");
        }
    }
}
