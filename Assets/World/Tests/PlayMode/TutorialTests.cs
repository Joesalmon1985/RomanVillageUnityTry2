using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Table;

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
    }
}
