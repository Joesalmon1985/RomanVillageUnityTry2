using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Encounters.Council;
using World.Sim.Encounters.Rhythm;
using World.Sim.Plague;
using World.Sim.Table;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game.Tests
{
    /// <summary>
    /// Tutorials are off in every other fixture of this assembly (they only talk and highlight, but a bubble over the scene is
    /// not what those tests are about) and in memory-only storage so no test touches the player's saved flags.
    /// </summary>
    [SetUpFixture]
    public class TutorialTestSetup
    {
        [OneTimeSetUp] public void Off() { TutorialDirector.Enabled = false; TutorialStore.UseMemory = true; }
    }

    /// <summary>
    /// One test per game: plays the tutorial through the way a player would (the same taps, commands and clock steps the game's own
    /// fixtures use), checks that every step comes up in order and ends on its condition, that the ring has a target on screen,
    /// that Skip ends it for good, that it is remembered, and that "Show me how again" starts it over.
    /// </summary>
    public class TutorialTests : EncounterTestBase
    {
        private const string TestPause = "TutorialTest";

        [SetUp]
        public void SetUpTutorials()
        {
            TutorialStore.UseMemory = true; TutorialStore.ClearMemory();
            TutorialDirector.Enabled = true; TutorialDirector.TimeScale = 0.02f;
            GodBoardDirector.RiseSeconds = 0.05f;
            PointerInput.InputEnabled = true;
            GodBoardSettings.Override = null;
            WorldActions.Machine.MinConfirmSeconds = 0f; WorldActions.Machine.GuardSeconds = 0f;
            WorldActions.Cancel();
            PrinceInstaller.Disabled = true;
            if (TutorialDirector.Instance != null) TutorialDirector.Instance.ResetProgress();
        }

        [TearDown]
        public void TearDownTutorials()
        {
            TutorialDirector.Enabled = false; TutorialDirector.TimeScale = 1f;
            TutorialStore.ClearMemory();
            GodBoardDirector.RiseSeconds = 2.4f;
            ScreenSafeArea.Override = null;
            WorldActions.Machine.MinConfirmSeconds = WorldActionMachine.DefaultMinConfirmSeconds;
            WorldActions.Machine.GuardSeconds = WorldActionMachine.DefaultGuardSeconds;
            WorldActions.Cancel(); LedgerCard.Hide();
            PrinceInstaller.Disabled = false;
            CouncilDirector.Disabled = false;
        }

        private static TutorialDirector Dir => TutorialDirector.Instance;

        // ------------------------------------------------------------------ helpers

        /// <summary>Waits until the running tutorial of <paramref name="game"/> shows the step (or its outro).</summary>
        private static IEnumerator AtStep(string game, string step, float timeout = 12f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (true)
            {
                var a = Dir.Active;
                if (a != null && a.Id == game)
                {
                    if (step == "outro" ? Dir.ShownStepId == "outro" : a.Current != null && a.Current.Id == step) yield break;
                }
                if (Time.realtimeSinceStartup > end)
                    Assert.Fail(game + ": never reached step '" + step + "' (active " + (a != null ? a.Id : "none") + ", step " + (a?.Current?.Id ?? "-") + ", shown " + Dir.ShownStepId + ")");
                yield return null;
            }
        }

        /// <summary>A step on screen has a line, a named tutor and, when it points at something, a ring rect that lies on the screen.</summary>
        private static void AssertSaid(string game, bool expectRing = true)
        {
            var s = Dir.Active.Current;
            Assert.IsNotNull(s, game + " has a step");
            Assert.IsFalse(string.IsNullOrEmpty(Dir.ShownLine), game + "/" + s.Id + ": a line is said");
            Assert.IsFalse(string.IsNullOrEmpty(Dir.ShownTutor), game + "/" + s.Id + ": the tutor is named");
            Assert.IsNull(LedgerText.Violation(Dir.ShownLine ?? ""), game + "/" + s.Id + ": no dice, pips or odds in the line");
            if (!expectRing || s.Target == null) return;
            var r = s.Target();
            Assert.IsTrue(r.HasValue, game + "/" + s.Id + ": the ring has a target");
            var rr = r.Value;
            Assert.IsFalse(float.IsNaN(rr.x) || float.IsNaN(rr.y), game + "/" + s.Id + ": the ring rect is a number");
            Assert.IsTrue(rr.xMax > 0 && rr.yMax > 0 && rr.x < Screen.width && rr.y < Screen.height, game + "/" + s.Id + ": the ring is on the screen " + rr + " of " + Screen.width + "x" + Screen.height);
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Skip ends the tutorial for good and it is remembered; "Show me how again" starts it over from the first step.</summary>
        private static IEnumerator SkipThenReplay(string game, string firstStep)
        {
            Dir.Skip();
            yield return Frames(3);
            Assert.IsNull(Dir.Active, game + ": no tutorial after Skip");
            Assert.IsTrue(TutorialStore.IsDone(game), game + ": Skip is remembered");
            CollectionAssert.Contains(Dir.Ended, game + ":skipped");
            yield return Frames(10);
            Assert.IsNull(Dir.Active, game + ": a skipped tutorial does not come back by itself");
            Dir.Replay(game);
            Assert.IsFalse(TutorialStore.IsDone(game), game + ": replay forgets the done flag");
            yield return AtStep(game, firstStep);
        }

        private static void AssertFinishedAndRemembered(string game)
        {
            Assert.IsTrue(TutorialStore.IsDone(game), game + ": remembered as done");
            CollectionAssert.Contains(Dir.Ended, game + ":done");
            Assert.IsNull(Dir.Active, game + ": nothing runs after the end");
        }

        // ------------------------------------------------------------------ the hex board

        private WorldBootstrap StartGodBoard_Boot;

        private IEnumerator StartGodBoard()
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
            Boot = WorldBootstrap.Instance;
            Boot.Clock.Pause(TestPause);
            Assert.IsNotNull(GodBoardDirector.Instance, "god board installed");
            Assert.IsNotNull(TutorialDirector.Instance, "tutorial director exists");
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Board_FirstTurn_EveryStepAdvances_SkipAndReplayWork()
        {
            yield return StartGodBoard();
            var d = GodBoardDirector.Instance; var hud = d.GetComponent<GodBoardHud>();
            var T = Boot.Sim.State.Table;
            Action<int> step = n => { for (int i = 0; i < n; i++) Boot.Clock.StepOnce(); };

            // Far from the altar nothing starts; near it the prince tells the first step.
            var w = PlayerWalker.Instance;
            w.Teleport(d.OwnTemple.transform.position + new Vector3(90f, 0, 0), 0f, 8f);
            yield return Frames(12);
            Assert.IsNull(Dir.Active, "no tutorial far from the altar");
            w.Teleport(d.OwnTemple.transform.position + new Vector3(10f, 0, 0), 270f, 8f);
            yield return AtStep("board", "sacrifice");
            AssertSaid("board");
            Assert.AreEqual("The prince", Dir.ShownTutor);

            // Skip, remembered; replay starts it over.
            yield return SkipThenReplay("board", "sacrifice");

            // Sacrifice by the altar's own two taps: the board opens and the first setup step comes up.
            var altar = d.OwnTemple.GetComponentInChildren<TempleAltar>();
            Assert.IsNotNull(altar, "the player's altar");
            yield return new WaitForSecondsRealtime(0.5f); // the camera has stopped moving since the teleport (the tap guard)
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(altar));
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(altar));
            step(2);
            yield return AtStep("board", "settle-preview");
            Assert.AreEqual(BoardView.Board, d.View);
            yield return Frames(3);
            AssertSaid("board");

            // Ghost, then confirm, for the camp.
            Assert.IsTrue(d.TapAt(d.ScreenOfCorner(d.AdviseCorner)).StartsWith("preview"));
            yield return AtStep("board", "settle-confirm");
            AssertSaid("board");
            Assert.IsTrue(d.Pieces.HasGhost);
            Assert.IsFalse(d.TapAt(d.ScreenOfCorner(d.AdviseCorner)).StartsWith("preview"));
            step(2);
            // ... and for the road.
            yield return AtStep("board", "road-preview");
            yield return Until(() => d.EdgeSpots.Count > 0, 5f, "road spots");
            AssertSaid("board");
            Assert.IsTrue(d.TapAt(d.ScreenOfEdge(d.EdgeSpots[0])).StartsWith("preview"));
            yield return AtStep("board", "road-confirm");
            AssertSaid("board");
            Assert.IsFalse(d.TapAt(d.ScreenOfEdge(d.EdgeSpots[0])).StartsWith("preview"));
            step(2);

            // The baskets and the costs while the peoples place their camps.
            yield return AtStep("board", "baskets");
            AssertSaid("board");
            hud.TapLedge("basket:2");
            yield return AtStep("board", "costs");
            AssertSaid("board");
            WorldActions.Cancel(); yield return null;
            hud.TapLedge("bench:0");
            yield return AtStep("board", "wait-turn");
            AssertSaid("board");
            int guard = 0;
            while (!(T.Phase == TablePhase.Main && T.Current == 0 && !T.Paused) && guard++ < 20000) { step(5); if (guard % 40 == 0) yield return null; }
            Assert.AreEqual(TablePhase.Main, T.Phase, "the player's turn came");
            yield return AtStep("board", "act");
            AssertSaid("board");

            // An envoy while the tutorial runs: said as a tip, outside the order.
            var offer = new TradeOffer { Id = T.NextOfferId++, From = 1, To = 0, TicksLeft = 150, Status = OfferStatus.Open };
            offer.Give[0] = 1; offer.Get[1] = 1;
            T.Offers.Add(offer);
            yield return Until(() => Dir.TipShown, 5f, "the envoy tip");
            Assert.AreEqual("envoy", Dir.ShownStepId);
            T.Offers.Remove(offer);
            yield return Until(() => !Dir.TipShown, 5f, "the envoy tip goes when the offer is gone");

            // Build a road with the goods (the same two taps), which ends the "act" step.
            for (int g = 0; g < T.Peoples[0].Hand.Length; g++) T.Peoples[0].Hand[g] = 5;
            d.SetBuildMode(BuildKind.Road);
            yield return Frames(2);
            int e = d.EdgeSpots[0];
            Assert.IsTrue(d.TapAt(d.ScreenOfEdge(e)).StartsWith("preview"));
            Assert.IsFalse(d.TapAt(d.ScreenOfEdge(e)).StartsWith("preview"));
            step(2);
            yield return AtStep("board", "end-turn");
            AssertSaid("board");

            // A 7 for a full hand: the discard sack is explained when it happens (nothing forces it).
            T.Phase = TablePhase.Discard; T.DiscardOwed = new int[T.PeopleCount]; T.DiscardOwed[0] = 2; T.DiscardTicksLeft = 400;
            yield return Until(() => Dir.TipShown && Dir.ShownStepId == "discard", 5f, "the discard tip");
            StringAssert.Contains("sack", Dir.ShownLine);
            T.Phase = TablePhase.Main; T.DiscardOwed = new int[T.PeopleCount];
            yield return Until(() => !Dir.TipShown, 5f, "the tip goes once nothing is owed");

            // The hourglass, twice: the turn ends.
            Assert.AreEqual(WorldTapResult.Previewed, hud.TapLedge("hourglass"));
            Assert.AreEqual(WorldTapResult.Confirmed, hud.TapLedge("hourglass"));
            step(2);
            yield return AtStep("board", "leave");
            AssertSaid("board");

            // The lamb stone, twice: back at the altar; the tutorial says its last words and is done.
            Assert.AreEqual(WorldTapResult.Previewed, hud.TapLedge("lamb"));
            Assert.AreEqual(WorldTapResult.Confirmed, hud.TapLedge("lamb"));
            step(2);
            yield return AtStep("board", "outro", 15f);
            Assert.IsFalse(string.IsNullOrEmpty(Dir.ShownLine));
            yield return Until(() => Dir.Active == null, 10f, "the tutorial ends");
            AssertFinishedAndRemembered("board");

            // Done is remembered: coming back to the altar does not start it again.
            yield return Frames(15);
            Assert.IsNull(Dir.Active);
            // ... until "Show me how again" is chosen.
            Dir.Replay("board");
            yield return AtStep("board", "sacrifice");
        }

        // ------------------------------------------------------------------ the plague

        private static void Face(Vector3 at)
        {
            var w = PlayerWalker.Instance;
            w.Teleport(w.Position, BoardSpace.YawOf(at - w.Position), 8f);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Plague_HealerTeachesTheLanternsAndTheBanish_SkipAndReplayWork()
        {
            yield return StartGodBoard();
            var lodge = HealersLodge.Instance; var lt = LodgeTable.Instance; var pv = PlagueViews.Instance;
            var S = Boot.Sim.State;
            Assert.IsTrue(lodge.PlagueRuns, "the plague runs in this game");
            var w = PlayerWalker.Instance;
            var healer = lodge.Healer.transform.position;

            // Far from the lodge nothing starts; at her door she tells the first step.
            w.Teleport(healer + new Vector3(120f, 0, 0), 0f, 8f);
            yield return Frames(12);
            Assert.IsNull(Dir.Active, "no tutorial far from the lodge");
            w.Teleport(healer + new Vector3(0, 0, -6f), 0f, 8f);
            Face(healer);
            yield return AtStep("plague", "ask");
            yield return Frames(3);
            AssertSaid("plague");
            Assert.AreEqual("The healer", Dir.ShownTutor);
            yield return SkipThenReplay("plague", "ask");

            // "How bad is it?" through her own dialogue option.
            lodge.Healer.DialogueBuilder(lodge.Healer);
            var how = lodge.Healer.Options.Find(o => o.Label == "How bad is it?");
            Assert.IsNotNull(how, "the healer offers it");
            how.OnChoose();
            yield return AtStep("plague", "lanterns");
            Face(lt.Post.transform.position); yield return Frames(3);
            AssertSaid("plague");
            WorldActions.Cancel(); yield return null;

            // The post, the stick, a vessel: each is read by tapping it (the ledger tablet opens).
            yield return new WaitForSecondsRealtime(0.5f);
            WorldActions.Inspect(lt.Post);
            yield return AtStep("plague", "stick");
            Face(lt.Stick.transform.position); yield return Frames(3);
            AssertSaid("plague");
            yield return new WaitForSecondsRealtime(0.5f); // the camera is still after the turn (the tap guard)
            WorldActions.Inspect(lt.Stick);
            yield return AtStep("plague", "vessels");
            Face(lt.Vessels[1].transform.position); yield return Frames(3);
            AssertSaid("plague", false);
            yield return new WaitForSecondsRealtime(0.5f);
            WorldActions.Inspect(lt.Vessels[1]);

            // No demon stands anywhere yet: the banish step says so and waits.
            yield return AtStep("plague", "banish-preview");
            StringAssert.Contains("No demon", Dir.ShownLine);
            var ev = new EventBuffer();
            for (int i = 0; i < 3; i++) PlagueRules.InfectRound(S, ev);
            yield return Frames(3);
            StringAssert.Contains("Demons stand", Dir.ShownLine, "the line changes once a demon stands on a shrine");

            // Walk to the shrine with demons: tap once (preview), again (banish).
            int node = 0; for (int n = 1; n <= PlagueGraphData.NodeCount; n++) if (PlagueApi.DemonsAt(S, n) > 0) { node = n; break; }
            Assert.Greater(node, 0);
            var nv = pv.NodeOf(node); var at = pv.NodePosition(node);
            w.Teleport(at + new Vector3(8f, 0, 0), 270f, 8f);
            yield return Frames(4);
            AssertSaid("plague");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(nv));
            yield return AtStep("plague", "banish-confirm");
            AssertSaid("plague");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(nv));
            for (int i = 0; i < 2; i++) Boot.Clock.StepOnce();
            yield return AtStep("plague", "charm");
            Assert.Greater(pv.CooldownTicksLeft, 0, "the charm is cooling");

            // The charm step reads for a few seconds, then the healer says her last words and it is done.
            yield return AtStep("plague", "outro", 15f);
            yield return Until(() => Dir.Active == null, 10f, "the tutorial ends");
            AssertFinishedAndRemembered("plague");
            yield return Frames(15);
            Assert.IsNull(Dir.Active, "done is remembered");
            Dir.Replay("plague");
            w.Teleport(healer + new Vector3(0, 0, -6f), 0f, 8f);
            yield return AtStep("plague", "ask");
        }

        // ------------------------------------------------------------------ the council

        private CouncilEncounter Enc => CouncilEncounter.Current;

        [UnityTest, Timeout(600000)]
        public IEnumerator Council_RecorderTeachesPressPreviewCommitTabletPushBank_SkipAndReplayWork()
        {
            yield return Load();
            CouncilDirector.Disabled = true;
            CouncilEncounter.AiDelay = 0.05f; CouncilEncounter.IntroSeconds = 0.4f;
            Boot.Clock.Pause("TestHold");
            int home = CouncilDirector.Instance.HomeSettlement;
            CouncilFixture.StageExample(Boot.Sim.State, home, 0);
            Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, home), "the council opened");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Until(() => Enc != null && Enc.Scene.Settled && !Enc.InTransition, 20f, "the council is settled");
            var e = Enc.E;
            try
            {
                // The example is staged with a push already pending, so "press on" is already true and is passed over without a word.
                yield return AtStep("council", "preview");
                yield return Until(() => Dir.Active.Current.Target().HasValue, 10f, "a note is on screen");
                AssertSaid("council");
                Assert.AreEqual("The recorder", Dir.ShownTutor);

                // Skip is remembered; replay starts from the first step and shows even the one that already holds.
                yield return SkipThenReplay("council", "press");
                yield return AtStep("council", "preview");
                yield return Until(() => Dir.Active.Current.Target().HasValue, 10f, "a note is on screen");

                int k = Enc.Scene.Proposals.FindIndex(p => p.Touches(5) && p.Touches(8)); if (k < 0) k = 0;
                Assert.IsTrue(Enc.TapTarget("note " + k), "tapped a note");
                yield return AtStep("council", "commit");
                AssertSaid("council");
                Assert.AreEqual(ElectionPhase.Choosing, e.Phase, "the preview sent nothing");
                yield return new WaitForSecondsRealtime(0.35f);
                Assert.IsTrue(Enc.TapTarget("note " + k), "tapped it again");
                yield return AtStep("council", "tablet");
                yield return Until(() => Dir.Active.Current.Target().HasValue, 10f, "the tablet is on screen");
                AssertSaid("council");
                Assert.IsTrue(e.HasAdvanced);

                yield return new WaitForSecondsRealtime(0.5f); // the council swallows a tap right after a commit
                Assert.IsTrue(Enc.TapTarget("tablet"));
                yield return AtStep("council", "again");
                AssertSaid("council");
                WorldActions.Cancel(); yield return null;

                // Push once more: the village offers new notes (or it is a bust, which the recorder explains).
                CouncilEncounter.AiDelay = 600f;
                yield return Until(() => Enc.Scene.Settled && !Enc.InTransition && e.Phase == ElectionPhase.Ready, 20f, "settled and ready to press");
                int pushes = e.Pushes;
                Assert.IsTrue(Enc.TapTargetNow("hand"), "pressed on");
                yield return Until(() => e.Pushes > pushes, 5f, "the push was made");
                if (e.Phase == ElectionPhase.Choosing)
                {
                    yield return AtStep("council", "bank");
                    yield return Until(() => Enc.Scene.Settled && !Enc.InTransition && Enc.Scene.Targets(Enc.Scene.ViewCamera).Exists(t => t.Name == "note 0"), 20f, "a note is on screen");
                    yield return new WaitForSecondsRealtime(0.5f); // the council swallows a tap right after a push
                    int adv = e.Advances;
                    Assert.IsTrue(Enc.TapTargetNow("note 0"), "chose a note");
                    yield return Until(() => e.Advances > adv || e.Phase != ElectionPhase.Choosing, 10f, "the note was taken (last target " + Enc.LastTarget + ", taps " + Enc.TapsHandled + ", proposals " + Enc.Scene.Proposals.Count + ", phase " + e.Phase + ", refused " + Enc.Refused + " " + Enc.LastRefusal + ", transition " + Enc.InTransition + ", settled " + Enc.Scene.Settled + ")");
                    yield return Until(() => Enc.Scene.Settled && !Enc.InTransition && e.Phase == ElectionPhase.Ready, 20f, "ready again (phase " + e.Phase + ", active " + e.Active + " seat " + e.PlayerSeat + ")");
                    yield return Until(() => Dir.Active.Current.Target().HasValue, 10f, "the recorder's seal is on screen");
                    AssertSaid("council");
                    Assert.IsTrue(Enc.TapTarget("recorder"), "previewed the seal");
                    yield return new WaitForSecondsRealtime(0.35f);
                    Assert.IsTrue(Enc.TapTarget("recorder"), "confirmed the seal");
                }
                else
                {
                    // a bust: said in words, and the step after it is done
                    yield return Until(() => Dir.TipShown, 10f, "the bust is explained (phase " + e.Phase + ", busts " + e.Busts + ", step " + (Dir.Active?.Current?.Id ?? "-") + ", ended " + string.Join(",", Dir.Ended) + ")");
                    StringAssert.Contains("bust", Dir.ShownLine.ToLowerInvariant());
                }
                yield return AtStep("council", "outro", 15f);
                yield return Until(() => Dir.Active == null, 10f, "the tutorial ends");
                AssertFinishedAndRemembered("council");
            }
            finally
            {
                CouncilEncounter.AiDelay = 0.75f; CouncilEncounter.IntroSeconds = 2.4f;
                if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
            }
        }

        // ------------------------------------------------------------------ the rhythm dance

        private CampLayout Camp => new CampLayout(Boot.Sim.State.Board);
        private static Vector2 Scr(Vector3 world) { var p = Camera.main.WorldToScreenPoint(world); return new Vector2(p.x, p.y); }

        private IEnumerator StartDance()
        {
            var sol = Npc.Find(NpcIds.Host);
            var front = Camp.DanceCircleCentre + Camp.Forward * 3.2f + Camp.Right * 1.2f;
            yield return Accept(NpcIds.Host, front, BoardSpace.YawOf(sol.transform.position - front));
            Assert.IsNotNull(RhythmPartyEncounter.Current, "the dance started");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Rhythm_SolTeachesDrumsTorchesPauseTallyAndBell_SkipAndReplayWork()
        {
            bool had = PlayerPrefs.HasKey(RhythmPartyEncounter.CalibrationPref);
            float old = PlayerPrefs.GetFloat(RhythmPartyEncounter.CalibrationPref, 0f);
            try
            {
                yield return Load();
                RhythmPartyEncounter.TuningOverride = new RhythmTuning();
                yield return StartDance();
                var r = RhythmPartyEncounter.Current;

                yield return AtStep("rhythm", "hit");
                yield return Frames(3);
                AssertSaid("rhythm");
                Assert.AreEqual("Sol", Dir.ShownTutor);
                yield return SkipThenReplay("rhythm", "hit");

                // Strike the drums: the player's own judged hits end the first step (the autoplayer stands in for the fingers).
                r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;
                yield return AtStep("rhythm", "torch", 30f);
                yield return Frames(3);
                AssertSaid("rhythm");

                // A block's torch is kept (gold): the next step.
                yield return AtStep("rhythm", "pause", 40f);
                yield return Frames(3);
                AssertSaid("rhythm");
                Assert.AreEqual(RhythmPartyEncounter.TapKind.Seat, r.TapAt(Scr(r.Props.SeatPoint)), "tap on the log seat pauses");
                Assert.IsTrue(r.IsPaused);
                yield return Frames(3);
                StringAssert.Contains("rests", Dir.ShownLine, "the line changes while the music rests");
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(r.Resume(), "a drum strike resumes");
                yield return Until(() => !r.IsPaused, 10f, "the four-count ended");
                yield return AtStep("rhythm", "tally", 20f);
                AssertSaid("rhythm");

                // The phrase ends; tap Sol for the tally, then the bell for the tuning.
                yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 90f, "the phrase ended");
                Assert.AreEqual(RhythmPartyEncounter.TapKind.Sol, r.TapAt(Scr(r.Props.SolPoint)));
                yield return AtStep("rhythm", "bell", 10f);
                AssertSaid("rhythm");
                StringAssert.Contains("bell", Dir.ShownLine);
                r.TapAt(new Vector2(2, 2)); // closes Sol's card
                Assert.AreEqual(RhythmPartyEncounter.TapKind.Bell, r.TapAt(Scr(r.Props.BellPoint)));
                Assert.IsTrue(r.TuneActive);
                foreach (double c in r.TuneClickTimes) Assert.IsTrue(r.TuneTap(c + 0.02));
                yield return AtStep("rhythm", "outro", 20f);
                yield return Until(() => Dir.Active == null, 10f, "the tutorial ends");
                AssertFinishedAndRemembered("rhythm");
                Assert.IsTrue(r.Return());
                yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "dance ended");
            }
            finally
            {
                if (had) PlayerPrefs.SetFloat(RhythmPartyEncounter.CalibrationPref, old); else PlayerPrefs.DeleteKey(RhythmPartyEncounter.CalibrationPref);
            }
        }
    }
}
