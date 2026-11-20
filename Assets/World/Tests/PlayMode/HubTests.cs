using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Encounters.Council;
using World.Sim.Table;

namespace World.Game.Tests
{
    /// <summary>WU-1a: the hub's places at night: each destination exists, is tagged and its entry starts its game or says its honest placeholder.</summary>
    public class HubTests
    {
        private const string TestPause = "HubTest";
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; CouncilDirector.Disabled = true; PointerInput.InputEnabled = true; WorldActions.Cancel(); }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; CouncilDirector.Disabled = false; }

        private static readonly string[] HubZones = { WorldZones.HubTemple, WorldZones.HubCouncil, WorldZones.HubRhythm, WorldZones.HubHealer, WorldZones.HubCave, WorldZones.HubDuel };

        private IEnumerator Start(string id, ulong seed = 7UL)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsNotNull(old, "World scene");
            Assert.IsTrue(DevStart.Begin(id, seed), DevStart.LastError);
            float t0 = Time.realtimeSinceStartup;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == id))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 120f, id + " did not start");
                yield return null;
            }
            yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
            if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false; // the prince's guide would walk the player away
            PlayerWalker.Instance.StopAuto();
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private static IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond()) { Assert.Less(Time.realtimeSinceStartup - t0, seconds, what); yield return null; }
        }

        private static float Bearing(Vector3 from, float yaw, Vector3 to) { var d = to - from; d.y = 0; return Mathf.DeltaAngle(yaw, BoardSpace.YawOf(d)); }

        private static Npc Need(string id) { var n = Npc.Find(id); Assert.IsNotNull(n, "villager " + id); return n; }

        [UnityTest, Timeout(180000)]
        public IEnumerator Hub_AtNight_EveryDestinationExists_IsTagged_AndFramedFromTheSpawn()
        {
            yield return Start(GodBoardStart.Id);
            Assert.Greater(DayNight.Instance.Night, 0.5f, "the hub opens at night");
            var layout = new CampLayout(S.Board);
            Assert.IsTrue(layout.Hub);
            foreach (var z in HubZones) Assert.IsNotNull(HubInstaller.Zone(z), "zone " + z);
            int hubZones = 0; foreach (var wz in HubInstaller.Root.GetComponentsInChildren<WorldZone>()) if (wz.ZoneId.StartsWith("HUB-")) hubZones++; // the lodge's PLG-* props (WU-3) are tagged below the hub too
            Assert.AreEqual(6, hubZones);
            // The people of each place.
            Need("temple_keeper"); Need("temple_builder_0"); Need("temple_builder_1");
            Need("council_steward");
            Need("healer"); Need("healer_assistant"); Need("healer_runner");
            Need("ring_referee"); Need("ring_sparrer_0"); Need("ring_sparrer_1");
            Need(NpcIds.Mira); Need("camp_companion_0"); Need("camp_companion_1"); Need(NpcIds.Host);
            Assert.IsNotNull(HealersLodge.Instance); Assert.IsNotNull(DuelRing.Instance); Assert.IsNotNull(ExpeditionCamp.Instance); Assert.IsNotNull(TempleForecourt.Instance);
            Assert.IsNotNull(GodBoardDirector.Instance.OwnTemple.GetComponentInChildren<TempleAltar>(), "the altar's own path stays");

            // The first view (half of the 70 degree vertical view on a 2:1 phone is about 56 degrees each way): temple, tower, circle, Mira.
            var spawn = WalkLattice.ToWorld(layout.Spawn); float yaw = WalkLattice.Yaw(layout.SpawnHeading);
            Assert.AreEqual(layout.SpawnHeading, PlayerWalker.Instance.Heading, "the walker starts on the layout's heading");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, HubInstaller.Zone(WorldZones.HubTemple).transform.position)), 56f, "temple in the first view");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, CouncilDirector.Instance.Site.Position)), 56f, "tower in the first view");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, layout.DanceCircleCentre)), 56f, "circle in the first view");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, layout.MiraSpot)), 56f, "Mira in the first view");
            // The ring and the lodge are one turn away (60 to 150 degrees off the spawn heading).
            foreach (var z in new[] { WorldZones.HubHealer, WorldZones.HubDuel })
            {
                float b = Mathf.Abs(Bearing(spawn, yaw, HubInstaller.Zone(z).transform.position));
                Assert.That(b, Is.InRange(56f, 150f), z + " is one turn away");
            }
            // The lodge keeps away from the council's herbs place.
            Assert.Greater(BoardSpace.Flat(HubInstaller.Zone(WorldZones.HubHealer).transform.position - CouncilDirector.Instance.Site.Loc[4]), 20f);
            // The legacy look is unchanged: no hub on a board that was not dealt.
            Assert.IsNotNull(GameObject.Find("Hub ground"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Temple_KeeperWalksToTheAltar_AndTheOfferingOpensTheTable()
        {
            yield return Start(GodBoardStart.Id);
            var f = TempleForecourt.Instance;
            var keeper = f.Keeper;
            keeper.DialogueBuilder(keeper);
            Assert.AreEqual("Make the offering", keeper.Options[0].Label);
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase);
            // The player walks up: the keeper greets and walks to the altar.
            PlayerWalker.Instance.Teleport(f.Frame.W(0, 10f), f.Frame.Yaw + 180f, 6f);
            yield return Until(() => f.Greeted && BoardSpace.Flat(keeper.transform.position - f.AltarApproach) < 1.6f, 20f, "keeper greets and walks to the altar");
            // The offering: the keeper takes the same path as the altar (Sacrifice).
            keeper.Options[0].OnChoose();
            yield return Until(() => !f.Offering, 20f, "the offering finishes");
            Step(2);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase, "Sacrifice opened the table");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Temple_DoorwayTap_IsTheSamePathAsTheAltar()
        {
            yield return Start(GodBoardStart.Id);
            var f = TempleForecourt.Instance;
            var door = f.GetComponentInChildren<TempleDoorway>();
            Assert.IsNotNull(door, "the doorway is a hit target");
            PlayerWalker.Instance.Teleport(f.Frame.W(0, 12f), f.Frame.Yaw + 180f, 6f);
            door.Interact();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase, "the first tap only previews");
            door.Interact();
            Step(2);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Council_StewardSaysNoDisputeYet_AndOpensWhenOneIsCalled()
        {
            yield return Start(GodBoardStart.Id);
            var st = CouncilSteward.Instance; Assert.IsNotNull(st);
            var dir = CouncilDirector.Instance;
            var tower = dir.Site.Position;
            Assert.Less(BoardSpace.Flat(st.Steward.transform.position - dir.StewardSpot), 0.5f, "the steward stands at the tower foot");
            Assert.Less(BoardSpace.Flat(st.Steward.transform.position - tower), 8f);
            if (!CouncilSteward.CanHold(S, dir.HomeSettlement))
            {
                st.Steward.DialogueBuilder(st.Steward);
                Assert.AreEqual(CouncilSteward.NotYet, st.Steward.Lines[0], "the honest placeholder");
                Assert.AreEqual("No dispute is called yet.", dir.StewardOpen(st.Steward));
            }
            // A council in progress with the player helping: the steward opens it through the existing accept path.
            CouncilFixture.StageExample(S, dir.HomeSettlement, 0);
            Step(2);
            Assert.IsTrue(CouncilSteward.CanHold(S, dir.HomeSettlement), "a council is called");
            st.Steward.DialogueBuilder(st.Steward);
            Assert.AreEqual("Hear the dispute", st.Steward.Options[0].Label);
            Assert.IsNull(dir.StewardOpen(st.Steward));
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "the council encounter started");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Rhythm_DrumStartsTheDance_ThenSolLeadsToTheSpires()
        {
            yield return Start(GodBoardStart.Id);
            var circle = DanceCircle.Current;
            var starter = circle.GetComponentsInChildren<DanceStarter>(true);
            Assert.GreaterOrEqual(starter.Length, 5, "the fire ring and four drums start the dance");
            var sol = Need(NpcIds.Host);
            sol.DialogueBuilder(sol);
            Assert.AreEqual("Dance at the fire", sol.Options[0].Label);
            starter[1].Interact();
            Assert.IsNotNull(RhythmPartyEncounter.Current, "a drum started the dance");
            EncounterHost.Instance.Abort();
            yield return null;

            // After spire 1 the home circle is cold and Sol offers to lead.
            var sd = SpireDirector.Instance;
            sd.Raise(Element.Earth, 0, S.Board.StartCorners[0], false);
            Assert.IsTrue(circle.Retired);
            sol.DialogueBuilder(sol);
            Assert.AreEqual("Lead me to the spires", sol.Options[0].Label);
            Assert.IsTrue(SolGuide.Instance.LeadToSpire());
            Assert.IsNotNull(PlayerWalker.Instance.Following, "the player follows Sol");
            Assert.Less(BoardSpace.Flat(sol.transform.position - HubInstaller.Zone(WorldZones.HubRhythm).transform.position), 20f, "Sol stays at the cold circle until he leads");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Healer_AndRunner_SayTheTruth_AndTheLodgeIsOrdinaryInHistoric()
        {
            yield return Start(GodBoardStart.Id);
            var lodge = HealersLodge.Instance;
            Assert.IsFalse(string.IsNullOrEmpty(lodge.HealerSays()));
            Assert.IsFalse(lodge.HealerSays().Contains("gone"));
            lodge.Healer.DialogueBuilder(lodge.Healer);
            Assert.AreEqual(lodge.HealerSays(), lodge.Healer.Lines[0]);
            lodge.SendRunner("Sickness has broken out on the island!");
            yield return Until(() => BoardSpace.Flat(lodge.Runner.transform.position - lodge.Door) < 3f, 20f, "the runner reaches the lodge");
            Assert.AreEqual(1, lodge.RunnerTrips);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Historic_ShowsTheTempleRuin_AndTheLodgeWithoutThePlague()
        {
            yield return Start(GodBoardStart.HistoricId);
            Assert.IsNotNull(S.Historic);
            Assert.IsNull(GodBoardDirector.Instance.OwnTemple, "no temple stands in Historic");
            var f = TempleForecourt.Instance;
            yield return Until(() => f.RuinShown, 5f, "the ruin shows");
            Assert.IsNotNull(f.Ruin.GetComponent<CampPropInfo>(), "the ruin is tappable");
            f.Keeper.DialogueBuilder(f.Keeper);
            StringAssert.Contains("age has turned", f.Keeper.Lines[0]);
            Assert.AreEqual(1, f.Keeper.Options.Count, "no offering in Historic");
            var lodge = HealersLodge.Instance;
            Assert.IsFalse(lodge.PlagueRuns);
            StringAssert.Contains("sickness is gone", lodge.HealerSays());
            Assert.IsNotNull(HubInstaller.Zone(WorldZones.HubHealer));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Cave_MiraCampHasCompanions_AndHerOfferStands()
        {
            yield return Start(GodBoardStart.Id);
            var mira = Need(NpcIds.Mira);
            mira.DialogueBuilder(mira);
            Assert.IsTrue(mira.Options.Any(o => o.Label == MiraOffer.Label), "her conversation is the entry");
            Assert.IsTrue(mira.Options.Any(o => o.Label == "Practice duel"));
            Assert.IsNotNull(ExpeditionCamp.Instance.GetComponentInChildren<MapTap>(), "the map opens her conversation");
            foreach (var c in ExpeditionCamp.Instance.Companions) Assert.Less(BoardSpace.Flat(c.transform.position - mira.transform.position), 6f, "companions wait by her");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Duel_RefereeOffersPracticeDuel_MiraWalksOver_AndTheEncounterStarts()
        {
            yield return Start(GodBoardStart.Id);
            var ring = DuelRing.Instance;
            Assert.IsNotNull(ring.GetComponentInChildren<RingTap>());
            ring.Referee.DialogueBuilder(ring.Referee);
            Assert.AreEqual("Practice duel", ring.Referee.Options[0].Label);
            var mira = Need(NpcIds.Mira);
            var home = mira.transform.position;
            Assert.IsTrue(ring.StartDuel());
            yield return Until(() => ring.State == DuelRing.Errand.Dueling, 40f, "Mira walks over");
            Assert.IsNotNull(WardDuelEncounter.Current, "the existing Ward duel started");
            Assert.Less(BoardSpace.Flat(mira.HomePosition - ring.MiraStand), 1.6f, "Mira stands at the ring's gate");
            EncounterHost.Instance.Abort();
            yield return Until(() => ring.State == DuelRing.Errand.None, 60f, "Mira walks back");
            Assert.Less(BoardSpace.Flat(mira.transform.position - home), 2f);
        }

        // ------------------------------------------------------------------------------------------------------------
        // WU-1b: tap the ground to walk, drag to turn, two-tap entries, the hub view comes back, framing on four seeds.

        private static Vector2 Scr(Vector3 world)
        {
            var p = PlayerWalker.Instance.Camera.WorldToScreenPoint(world);
            Assert.Greater(p.z, 0f, "in front of the camera");
            return new Vector2(p.x, p.y);
        }

        private static bool OnScreen(Vector2 p) => p.x > 4 && p.y > 4 && p.x < Screen.width - 4 && p.y < Screen.height - 4;

        private static IEnumerator Settled()
        {
            yield return Until(() => !PlayerWalker.Instance.IsGliding, 8f, "walker settles");
        }

        /// <summary>Puts the player on the hub's spawn cell and heading and waits a frame so the camera follows.</summary>
        private IEnumerator AtSpawn()
        {
            var layout = new CampLayout(S.Board);
            PlayerWalker.Instance.StopAuto();
            PlayerWalker.Instance.Teleport(layout.Spawn, layout.SpawnHeading);
            yield return null; yield return null;
        }

        /// <summary>A screen point whose ray lands on plain ground (no person, prop or action) near a world point, or false.</summary>
        private static bool FreeGroundPoint(Vector3 world, out Vector2 scr, out Vector3 ground)
        {
            scr = default; ground = default;
            foreach (var off in new[] { Vector3.zero, new Vector3(2, 0, 0), new Vector3(-2, 0, 0), new Vector3(0, 0, 2), new Vector3(0, 0, -2), new Vector3(4, 0, 2), new Vector3(-4, 0, 2) })
            {
                var w = world + off; w.y = BoardSpace.GroundY;
                var p = PlayerWalker.Instance.Camera.WorldToScreenPoint(w);
                if (p.z <= 0f) continue;
                var s = new Vector2(p.x, p.y);
                if (!OnScreen(s) || TouchStrips.StripAt(s) != 0) continue;
                var hit = PointerInput.Raycast(s);
                if (!hit.HasGround || (hit.Target != null && !(hit.Target is HexView)) || !PointerInput.EmptyScene(s)) continue;
                scr = s; ground = hit.Ground; return true;
            }
            return false;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator GroundTap_WalksThere_WithAFootprintMarker_AndArrives()
        {
            yield return Start(GodBoardStart.Id);
            yield return AtSpawn();
            var w = PlayerWalker.Instance;
            var spawn = w.Position; var fwd = BoardSpace.DirOf(w.Yaw);
            Vector2 scr = default; Vector3 ground = default; bool found = false;
            foreach (float d in new[] { 9f, 12f, 7f, 15f }) if (FreeGroundPoint(spawn + fwd * d, out scr, out ground)) { found = true; break; }
            Assert.IsTrue(found, "plain visible ground ahead of the spawn");
            int before = GroundWalk.Walks;
            Assert.AreEqual(TapOutcome.Walk, PointerInput.TapAt(scr), "a ground tap walks");
            Assert.AreEqual(before + 1, GroundWalk.Walks);
            Assert.IsTrue(w.IsAutoWalking, "the auto-walk started");
            Assert.IsNotNull(GroundWalk.Marker); Assert.IsTrue(GroundWalk.Marker.activeSelf, "a footprint marker on the target");
            var goal = GroundWalk.Goal.Value;
            Assert.Less(BoardSpace.Flat(GroundWalk.Marker.transform.position - WalkLattice.ToWorld(goal)), 0.5f, "the marker lies on the target cell");
            Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(goal) - ground), 2.5f * WalkLattice.Step, "the target is where it was tapped (at most a cell or two off)");
            yield return Until(() => !w.IsAutoWalking, 60f, "the walk ends");
            yield return Settled();
            Assert.AreEqual(goal, w.Cell, "it arrived at the target cell");
            yield return Until(() => !GroundWalk.Marker.activeSelf, 5f, "the marker goes on arrival");
            // A right click (StopAuto) also takes the marker away.
            Assert.IsTrue(FreeGroundPoint(w.Position - BoardSpace.DirOf(w.Yaw) * 0f + BoardSpace.DirOf(w.Yaw) * 8f, out scr, out ground));
            Assert.AreEqual(TapOutcome.Walk, PointerInput.TapAt(scr));
            w.StopAuto();
            Assert.IsFalse(GroundWalk.Marker.activeSelf, "a stopped walk clears the marker");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator GroundTap_OnTheForecourt_IsNotSwallowed_AndTheDoorwayIsSmall()
        {
            yield return Start(GodBoardStart.Id);
            var f = TempleForecourt.Instance;
            const float R = TempleView.Radius;
            var door = f.GetComponentInChildren<TempleDoorway>();
            var box = door.GetComponent<BoxCollider>();
            Assert.Less(box.size.x, 5f, "the doorway hit target is the step, not the whole forecourt");
            Assert.Less(box.size.z, 3f);
            var yaw = f.Frame.Yaw + 180f;
            PlayerWalker.Instance.StopAuto();
            PlayerWalker.Instance.Teleport(WalkLattice.ToCell(f.Frame.W(-2f, R + 15f)), WalkLattice.HeadingOf(yaw));
            yield return null; yield return null;
            Vector2 scr = default; Vector3 ground = default; bool found = false;
            foreach (var p in new[] { f.Frame.W(-3.4f, R + 6.4f), f.Frame.W(3.2f, R + 8f), f.Frame.W(-1f, R + 9f), f.Frame.W(0f, R + 7f) })
                if (FreeGroundPoint(p, out scr, out ground)) { found = true; break; }
            Assert.IsTrue(found, "bare paving on the forecourt is visible");
            Assert.AreEqual(TapOutcome.Walk, PointerInput.TapAt(scr), "a tap on the forecourt paving walks there");
            Assert.AreNotEqual(WorldActionState.Preview, WorldActions.State, "no altar preview from a paving tap");
            PlayerWalker.Instance.StopAuto();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Tap_OnAPerson_DoesNotWalk()
        {
            yield return Start(GodBoardStart.Id);
            yield return AtSpawn();
            var w = PlayerWalker.Instance;
            var mira = Need(NpcIds.Mira);
            var p = Scr(mira.transform.position + Vector3.up * 1.0f);
            Assert.IsTrue(OnScreen(p), "Mira is in the first view");
            Assert.IsFalse(PointerInput.EmptyScene(p), "a person is not empty scene");
            int walks = GroundWalk.Walks;
            var o = PointerInput.TapAt(p);
            Assert.That(o, Is.EqualTo(TapOutcome.Interact).Or.EqualTo(TapOutcome.Person), "a tap on a person talks");
            Assert.AreEqual(walks, GroundWalk.Walks, "no walk from a tap on a person");
            Assert.IsFalse(w.IsAutoWalking);
            // A tap on a prop with a world action previews it and does not walk either.
            var ring = DuelRing.Instance.GetComponentInChildren<RingTap>();
            WorldActions.Cancel();
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(ring));
            Assert.AreEqual(walks, GroundWalk.Walks);
            WorldActions.Cancel();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Drag_OnEmptyScene_TurnsOneSixthPerStep_AndKeysStayTheAlternative()
        {
            yield return Start(GodBoardStart.Id);
            yield return AtSpawn();
            var w = PlayerWalker.Instance;
            int h0 = w.Heading;
            Vector2 scr = default; Vector3 ground = default; bool found = false;
            foreach (float d in new[] { 9f, 12f, 7f }) if (FreeGroundPoint(w.Position + BoardSpace.DirOf(w.Yaw) * d, out scr, out ground)) { found = true; break; }
            Assert.IsTrue(found);
            Assert.IsTrue(PointerInput.EmptyScene(scr));
            float step = DragLook.StepPixels;
            // Less than one step: no turn. A press that barely moved is a tap, not a drag.
            DragLook.Begin(scr, true);
            Assert.IsFalse(DragLook.Move(scr + new Vector2(4, 0)), "a few pixels is still a tap");
            Assert.IsFalse(DragLook.End());
            DragLook.Begin(scr, true);
            Assert.IsTrue(DragLook.Move(scr + new Vector2(step * 0.6f, 0)), "a real drag");
            yield return null;
            Assert.AreEqual(h0, w.Heading, "less than one step turns nothing");
            Assert.IsTrue(DragLook.End(), "it was a drag, so no tap");
            // Two and a bit steps to the right: two sixths to the left.
            DragLook.Begin(scr, true);
            DragLook.Move(scr + new Vector2(step * 2.3f, 0));
            for (int i = 0; i < 400 && (DragLook.Pending != 0 || w.IsGliding); i++) { DragLook.Pump(); yield return null; }
            Assert.AreEqual(((h0 - 2) % 6 + 6) % 6, w.Heading, "two steps right = two sixths left");
            DragLook.End();
            // Back the other way, one step to the left: one sixth to the right.
            int h1 = w.Heading;
            DragLook.Begin(scr, true);
            DragLook.Move(scr - new Vector2(step * 1.2f, 0));
            for (int i = 0; i < 400 && (DragLook.Pending != 0 || w.IsGliding); i++) { DragLook.Pump(); yield return null; }
            Assert.AreEqual((h1 + 1) % 6, w.Heading);
            DragLook.End();
            // A press that starts on a person never turns.
            var mira = Need(NpcIds.Mira);
            PlayerWalker.Instance.Teleport(WalkLattice.ToCell(w.Position), h0); yield return null; yield return null;
            DragLook.Begin(Scr(mira.transform.position + Vector3.up), PointerInput.EmptyScene(Scr(mira.transform.position + Vector3.up)));
            Assert.IsFalse(DragLook.Active, "a press on a person is not a look-round");
            Assert.IsFalse(DragLook.Move(scr + new Vector2(step * 3f, 0)));
            // Keys and strips are the alternative: the walker's own turn.
            int h2 = w.Heading;
            w.TurnRight(); yield return Settled();
            Assert.AreEqual((h2 + 1) % 6, w.Heading);
            w.PressStrip(1); yield return Settled();
            Assert.AreEqual(h2, w.Heading);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TwoTap_Duel_PreviewsFirst_ThenMiraComes()
        {
            yield return Start(GodBoardStart.Id);
            var ring = DuelRing.Instance; var tap = ring.GetComponentInChildren<RingTap>();
            Assert.IsNotNull(tap);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(tap));
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
            Assert.AreEqual("Fight a practice duel with Mira?", LedgerCard.Content.Question);
            Assert.IsTrue(ring.PreviewShown, "a ring of light round the fence");
            Assert.AreEqual(DuelRing.Errand.None, ring.State, "the preview sends nothing");
            Assert.AreEqual(0, ring.DuelsStarted);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(tap));
            Assert.AreEqual(DuelRing.Errand.MiraComing, ring.State, "the second tap starts the errand once");
            Assert.AreEqual(1, tap.Commits);
            yield return Until(() => ring.State == DuelRing.Errand.Dueling, 40f, "Mira walks over");
            Assert.IsNotNull(WardDuelEncounter.Current);
            EncounterHost.Instance.Abort();
            yield return Until(() => ring.State == DuelRing.Errand.None, 60f, "Mira walks back");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TwoTap_Offering_Council_Rhythm_AllPreviewBeforeTheyCommit()
        {
            yield return Start(GodBoardStart.Id);
            // The lamb pen: the keeper's offering.
            var f = TempleForecourt.Instance;
            PlayerWalker.Instance.Teleport(f.Frame.W(0, 10f), f.Frame.Yaw + 180f, 6f);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(f.Offer));
            Assert.AreEqual("Take a lamb to the altar?", LedgerCard.Content.Question);
            Assert.IsFalse(f.Offering, "the preview sends nothing");
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(f.Offer));
            Assert.IsTrue(f.Offering, "the keeper is on the way");
            yield return Until(() => !f.Offering, 30f, "the offering finishes");
            Step(2);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase, "the same Sacrifice as the conversation");
            WorldActions.Cancel();

            // The steward's bench.
            var dir = CouncilDirector.Instance; var st = CouncilSteward.Instance;
            CouncilFixture.StageExample(S, dir.HomeSettlement, 0);
            Step(2);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(st.Entry));
            Assert.AreEqual("Hear the dispute?", LedgerCard.Content.Question);
            Assert.IsFalse(EncounterHost.Instance.IsRunning, "the preview opens nothing");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(st.Entry));
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "the council started through the steward's path");
            Assert.AreEqual(1, st.Entry.Commits);
            EncounterHost.Instance.Abort();
            yield return null; yield return null;
            WorldActions.Cancel();

            // Sol's seat: the dance while the circle is warm, the walk to the spires once it is cold.
            var sol = SolGuide.Instance;
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(sol.Entry));
            Assert.AreEqual("Dance at the fire?", LedgerCard.Content.Question);
            Assert.IsNull(RhythmPartyEncounter.Current, "the preview starts nothing");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(sol.Entry));
            Assert.IsNotNull(RhythmPartyEncounter.Current, "the dance started");
            EncounterHost.Instance.Abort();
            yield return null; yield return null;
            WorldActions.Cancel();
            SpireDirector.Instance.Raise(Element.Earth, 0, S.Board.StartCorners[0], false);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(sol.Entry));
            Assert.AreEqual("Follow Sol to the spires?", LedgerCard.Content.Question);
            Assert.IsNull(PlayerWalker.Instance.Following);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(sol.Entry));
            Assert.IsNotNull(PlayerWalker.Instance.Following, "the player follows Sol");
            PlayerWalker.Instance.StopAuto();
        }

        private struct Pose { public WalkLattice.Cell Cell; public int Heading; public bool Off; }
        private static Pose Here() { var w = PlayerWalker.Instance; return new Pose { Cell = w.Cell, Heading = w.Heading, Off = w.IsOffLattice }; }
        private static void AssertBack(Pose before, string where)
        {
            var w = PlayerWalker.Instance;
            Assert.AreEqual(before.Cell, w.Cell, where + ": the same cell");
            Assert.AreEqual(before.Heading, w.Heading, where + ": the same heading");
            Assert.IsFalse(w.IsOffLattice, where + ": on the lattice");
            Assert.AreEqual(WalkLattice.Yaw(before.Heading), w.Yaw, 0.01f, where + ": the same yaw");
            Assert.IsFalse(w.IsLocked, where + ": free to walk");
            Assert.AreEqual(ViewMode.FirstPerson, ViewSwitcher.Instance.CurrentView, where + ": the walking view");
            Assert.IsTrue(PointerInput.InputEnabled, where + ": the pointer is back");
        }

        [UnityTest, Timeout(480000)]
        public IEnumerator EveryDestination_ReturnsToTheSameHubPositionAndHeading_WithStateKept()
        {
            yield return Start(GodBoardStart.Id);
            yield return AtSpawn();
            var w = PlayerWalker.Instance;
            var home = Here();
            // God board: sacrifice, rise, play, leave.
            var d = GodBoardDirector.Instance;
            Assert.IsTrue(d.TrySacrifice(true));
            Step(2);
            float t0 = Time.realtimeSinceStartup;
            while (d.View != BoardView.Board) { Assert.Less(Time.realtimeSinceStartup - t0, 30f); yield return null; }
            d.Leave(); Step(2);
            t0 = Time.realtimeSinceStartup;
            while (d.View != BoardView.Altar) { Assert.Less(Time.realtimeSinceStartup - t0, 30f); yield return null; }
            yield return null;
            AssertBack(home, "god board");
            Assert.IsTrue(S.Table.Paused, "state kept: the table waits where it was");
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase);
            // Council.
            var dir = CouncilDirector.Instance;
            CouncilFixture.StageExample(S, dir.HomeSettlement, 0);
            Step(2);
            Assert.IsNull(dir.StewardOpen(CouncilSteward.Instance.Steward));
            Assert.IsTrue(EncounterHost.Instance.IsRunning);
            yield return null;
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 10f, "council ends");
            yield return null;
            AssertBack(home, "council");
            // Dance at the home circle.
            var starter = DanceCircle.Current.GetComponentsInChildren<DanceStarter>(true)[1];
            starter.Interact();
            Assert.IsNotNull(RhythmPartyEncounter.Current);
            yield return null; yield return null;
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 10f, "dance ends");
            yield return null;
            AssertBack(home, "dance circle");
            // Practice duel.
            var ring = DuelRing.Instance;
            Assert.IsTrue(ring.StartDuel());
            yield return Until(() => ring.State == DuelRing.Errand.Dueling, 40f, "Mira walks over");
            yield return null; yield return null;
            EncounterHost.Instance.Abort();
            yield return Until(() => ring.State == DuelRing.Errand.None, 60f, "Mira walks back");
            AssertBack(home, "duel");
            // The lodge: inspection leaves the player where he stands.
            HealersLodge.Instance.Tap();
            yield return null;
            foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h);
            AssertBack(home, "healer's lodge");
            // A ground tap away and the walk back by another ground tap, then a destination from the new place.
            var fwd = BoardSpace.DirOf(w.Yaw);
            Vector2 scr = default; Vector3 ground = default; bool found = false;
            foreach (float dd in new[] { 9f, 12f, 7f }) if (FreeGroundPoint(w.Position + fwd * dd, out scr, out ground)) { found = true; break; }
            Assert.IsTrue(found);
            Assert.AreEqual(TapOutcome.Walk, PointerInput.TapAt(scr));
            yield return Until(() => !w.IsAutoWalking, 60f, "walks away");
            yield return Settled();
            var away = Here();
            Assert.AreNotEqual(home.Cell, away.Cell);
            Assert.IsTrue(ring.StartDuel());
            yield return Until(() => ring.State == DuelRing.Errand.Dueling, 40f, "Mira walks over again");
            yield return null; yield return null;
            EncounterHost.Instance.Abort();
            yield return Until(() => ring.State == DuelRing.Errand.None, 60f, "Mira walks back again");
            AssertBack(away, "duel from a walked-to place");
            // Sol leads to the spires: the player ends at the spire's stage and the dance there brings him back to it.
            var sd = SpireDirector.Instance;
            sd.Raise(Element.Earth, 0, S.Board.StartCorners[0], false);
            Assert.IsTrue(SolGuide.Instance.LeadToSpire());
            yield return Until(() => SolGuide.Instance.Led == 1, 120f, "Sol arrives at the spire");
            w.StopAuto();
            yield return Until(() => !w.IsGliding, 8f, "walker settles");
            var atSpire = Here();
            EncounterDirectory.TryStart(EncounterDirectory.RhythmParty, Need(NpcIds.Host)); // the circle is cold: it may refuse; then there is nothing to come back from
            yield return null; yield return null;
            if (EncounterHost.Instance.IsRunning) { EncounterHost.Instance.Abort(); yield return Until(() => !EncounterHost.Instance.IsRunning, 10f, "spire dance ends"); yield return null; AssertBack(atSpire, "spire dance"); }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Framing_OnFourSeeds_TempleTowerCircleAndMiraAreInViewOrOneTurnAway()
        {
            foreach (ulong seed in new ulong[] { 7UL, 3000UL, 3001UL, 42UL })
            {
                yield return Start(GodBoardStart.Id, seed);
                var layout = new CampLayout(S.Board);
                Assert.IsTrue(layout.Hub, "seed " + seed);
                var spawn = WalkLattice.ToWorld(layout.Spawn); float yaw = WalkLattice.Yaw(layout.SpawnHeading);
                var targets = new[]
                {
                    ("temple", HubInstaller.Zone(WorldZones.HubTemple).transform.position),
                    ("tower", CouncilDirector.Instance.Site.Position),
                    ("circle", layout.DanceCircleCentre),
                    ("mira", layout.MiraSpot),
                };
                foreach (var (name, pos) in targets)
                {
                    float b = Mathf.Abs(Bearing(spawn, yaw, pos));
                    Assert.Less(b, 56f + 60f, "seed " + seed + ": " + name + " is in the first view or one turn away (" + b.ToString("0") + " degrees off)");
                }
                // Mira and the circle are in the first view (the spawn-heading rule), the temple within one turn.
                Assert.Less(Mathf.Abs(Bearing(spawn, yaw, layout.DanceCircleCentre)), 56f, "seed " + seed + ": circle in the first view");
                Assert.Less(Mathf.Abs(Bearing(spawn, yaw, layout.MiraSpot)), 56f, "seed " + seed + ": Mira in the first view");
                // One turn left and right still holds the lodge and the ring in view of one of them.
                foreach (var z in new[] { WorldZones.HubHealer, WorldZones.HubDuel })
                {
                    float best = 180f;
                    foreach (int k in new[] { -1, 0, 1 }) best = Mathf.Min(best, Mathf.Abs(Bearing(spawn, yaw + 60f * k, HubInstaller.Zone(z).transform.position)));
                    Assert.Less(best, 56f, "seed " + seed + ": " + z + " is in view after at most one turn");
                }
            }
        }
    }
}
