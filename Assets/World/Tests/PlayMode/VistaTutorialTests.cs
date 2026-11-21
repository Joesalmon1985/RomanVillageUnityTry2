using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game.Tests
{
    /// <summary>
    /// The opening vista and the tutorials together (21 Nov 2026 merge). A plain launch with the tutorials ON: while the player looks out over
    /// the camp (and while the prince welcomes) no tutorial bubble, ring or start; a first tap previews, a second tap glides to the place and
    /// that place's tutorial begins with its own tutor. Both SetUpFixtures (TestDefaults: legacy start, vista off; and
    /// TutorialTestSetup: tutorials off) run first for the whole namespace; this fixture turns the vista and the tutorials back on.
    /// </summary>
    public class VistaTutorialTests
    {
        [SetUp]
        public void SetUp()
        {
            WorldBootstrap.DefaultStart = GodBoardStart.Id;
            OpeningVista.Mode = VistaMode.On;
            TutorialStore.UseMemory = true; TutorialStore.ClearMemory();
            TutorialDirector.Enabled = true;
            TutorialDirector.TimeScale = 0.02f;
            if (TutorialDirector.Instance != null) TutorialDirector.Instance.ResetProgress();
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
            TutorialDirector.Enabled = false; TutorialDirector.TimeScale = 1f;
            TutorialStore.ClearMemory();
            GodBoardDirector.RiseSeconds = 2.4f;
            CouncilDirector.Disabled = false;
            WorldActions.Cancel(); LedgerCard.Hide();
        }

        private static TutorialDirector Dir => TutorialDirector.Instance;

        private IEnumerator Launch()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            float t0 = Time.realtimeSinceStartup;
            while (!(OpeningVista.Instance != null && OpeningVista.Up)) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the vista did not open"); yield return null; }
            Assert.IsNotNull(Dir, "the tutorial director exists");
        }

        private static IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond()) { Assert.Less(Time.realtimeSinceStartup - t0, seconds, what); yield return null; }
        }

        private static void AssertTutorialsQuiet(string when)
        {
            Assert.IsNull(Dir.Active, when + ": no tutorial runs while the player looks out over the camp (" + (Dir.Active != null ? Dir.Active.Id : "") + ")");
            Assert.IsFalse(Dir.BubbleShown, when + ": no tutorial bubble");
            Assert.IsFalse(Dir.RingShown, when + ": no tutorial ring");
            Assert.IsNull(Dir.ShownLine, when + ": no tutorial line");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Vista_NoTutorialSpeaks_WhileTheWelcomeAndThePreviewHaveTheScreen()
        {
            yield return Launch();
            var v = OpeningVista.Instance;
            // the whole welcome: the prince's bubbles show, the tutorials stay silent (and never start on the hub before a place is picked)
            float t0 = Time.realtimeSinceStartup; int frames = 0;
            while (v.Pointed.Count < 3 && Time.realtimeSinceStartup - t0 < 40f)
            {
                AssertTutorialsQuiet("welcome frame " + frames++);
                yield return null;
            }
            Assert.GreaterOrEqual(v.Pointed.Count, 3, "the welcome pointed at places");
            // a preview (one tap) is not a pick either
            yield return new WaitForSecondsRealtime(0.7f);
            var p = v.Find(WorldZones.HubTemple);
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapAt(v.ScreenCentre(p)), "the first tap previews");
            for (int i = 0; i < 20; i++) { AssertTutorialsQuiet("preview"); yield return null; }
            Assert.IsTrue(OpeningVista.Up, "still looking out after one tap");
        }

        /// <summary>The second tap on a place; returns when the camera has landed. Asserts the tutorials were silent until then.</summary>
        private IEnumerator Pick(string zone)
        {
            var v = OpeningVista.Instance;
            yield return Until(() => OpeningVista.Up, 20f, "the vista is up before " + zone);
            yield return new WaitForSecondsRealtime(0.7f);
            AssertTutorialsQuiet(zone + " before the tap");
            var p = v.Find(zone);
            var at = v.ScreenCentre(p);
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapAt(at), zone + ": first tap");
            yield return new WaitForSecondsRealtime(0.4f);
            AssertTutorialsQuiet(zone + " previewed");
            Assert.AreEqual(TapOutcome.Confirm, PointerInput.TapAt(at), zone + ": second tap");
            // gliding down: still nothing from the tutorials
            while (v.State != VistaState.Off) { AssertTutorialsQuiet(zone + " gliding"); yield return null; }
            Assert.AreSame(p, v.LastArrived, zone + ": arrived");
        }

        private IEnumerator Begins(string game, params string[] tutors)
        {
            yield return Until(() => Dir.Active != null && Dir.Active.Id == game && Dir.BubbleShown, 25f,
                game + " tutorial did not begin with a bubble (active " + (Dir.Active != null ? Dir.Active.Id : "none") + ")");
            Assert.AreEqual(VistaState.Off, OpeningVista.Instance.State, game + ": the vista is down when the tutorial speaks");
            Assert.IsTrue(tutors.Contains(Dir.ShownTutor), game + ": the tutor is " + string.Join(" / ", tutors) + " (was " + Dir.ShownTutor + ")");
            Assert.IsFalse(string.IsNullOrEmpty(Dir.ShownLine), game + ": a line");
        }

        private IEnumerator BackToTheVista(string zone)
        {
            foreach (int h in SpeechBubbles.OpenHandles().ToArray()) SpeechBubbles.Close(h);
            if (DialoguePanel.IsOpen) DialoguePanel.Close();
            WorldActions.Cancel();
            yield return null;
            yield return Until(() => OpeningVista.TryReturn(), 10f, zone + ": back to the vista");
            yield return Until(() => OpeningVista.Up, 10f, zone + ": the camera rises");
            yield return null; yield return null;
            AssertTutorialsQuiet(zone + " back on the vista"); // a parked tutorial does not speak over the vista
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EachPlace_TwoTaps_ThenThatGamesTutorialBeginsWithItsTutor()
        {
            yield return Launch();
            var boot = WorldBootstrap.Instance;
            yield return new WaitForSecondsRealtime(1f);
            AssertTutorialsQuiet("first second");

            // the healer's lodge: the healer teaches the plague
            yield return Pick(WorldZones.HubHealer);
            yield return Begins("plague", "The healer");
            yield return BackToTheVista(WorldZones.HubHealer);

            // Sol's dance circle: Sol teaches the rhythm game (the plague waits where it was)
            yield return Pick(WorldZones.HubRhythm);
            yield return Begins("rhythm", "Sol");
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the dance ends");
            yield return BackToTheVista(WorldZones.HubRhythm);

            // the tower: with a dispute called the council opens and the recorder teaches
            int home = CouncilDirector.Instance.HomeSettlement;
            CouncilFixture.StageExample(boot.Sim.State, home, 0);
            yield return Pick(WorldZones.HubCouncil);
            yield return Begins("council", "The recorder");
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the council ends");
            yield return BackToTheVista(WorldZones.HubCouncil);

            // the temple: the offering opens the board, the keeper (or the prince on the ledge) teaches
            yield return Pick(WorldZones.HubTemple);
            yield return Begins("board", "The temple keeper", "The prince");
            GodBoardDirector.Instance.Leave();
            yield return Until(() => GodBoardDirector.Instance.View == BoardView.Altar, 30f, "back from the board");
            yield return BackToTheVista(WorldZones.HubTemple);

            // the practice ring: Mira walks over, the duel starts, the referee teaches
            yield return Pick(WorldZones.HubDuel);
            yield return Until(() => EncounterHost.Instance.IsRunning, 60f, "the duel starts when Mira arrives");
            yield return Begins("duel", "The referee");
            EncounterHost.Instance.Abort();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 15f, "the duel ends");
            Assert.IsFalse(TutorialStore.IsDone("plague"), "nothing was marked done by merely visiting");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheCave_TwoTaps_ArrivesAtMira_AndTheDungeonTutorialWaitsForTheWayDown()
        {
            yield return Launch();
            yield return Pick(WorldZones.HubCave);
            yield return null; yield return null;
            Assert.IsNull(Dir.Active, "no dungeon tutorial on the surface: it starts when the way down is taken");
            Assert.Greater(SpeechBubbles.OpenHandles().Count(), 0, "Mira speaks to the player at the cave mouth");
        }
    }
}
