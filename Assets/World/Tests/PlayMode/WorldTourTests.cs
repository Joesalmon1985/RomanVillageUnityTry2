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
    /// <summary>WU-8: the whole-game pass. The tour is the same code the packaged AutoPlay script <c>world_ui_tour</c> runs.</summary>
    public class WorldTourTests
    {
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;

        [SetUp]
        public void SetUp()
        {
            GodBoardDirector.RiseSeconds = 0.05f; CouncilDirector.Disabled = true; PointerInput.InputEnabled = true;
            GodBoardSettings.Override = false; WorldActions.Cancel(); Interiors.FactsOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            GodBoardDirector.RiseSeconds = 2.4f; CouncilDirector.Disabled = false; PointerInput.InputEnabled = true;
            GodBoardSettings.Override = null; Interiors.FactsOverride = null; WorldActions.Cancel();
        }

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
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Tour_EntersAndLeavesAllSixDestinations_InARow_ToTheSameHubPose_WithStateKept()
        {
            yield return Start(GodBoardStart.Id);
            var tour = new WorldTour(m => Assert.Fail(m));
            yield return tour.Run();
            Assert.IsNull(tour.Failure);
            foreach (var where in new[] { "god board", "council", "rhythm", "healer's lodge", "duel ring", "crystal expedition", "Lead me out of the crystal expedition" })
                CollectionAssert.Contains(tour.Done, where);
            Assert.IsTrue(tour.Done.Contains("crystal expedition"));
            Assert.IsNotNull(S.Campaign, "the expedition started a campaign");
        }

        // (the tour itself checks the dungeon return: "Lead me out" ends on the hub cell, heading and view)

        /// <summary>The Esc menu's Clear view button flips the one setting; the board HUD, the plague buttons and the duel panel all follow it.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ClearView_EscMenu_OneSetting_ShowsBoardHud_PlagueButtons_AndDuelPanelTogether()
        {
            yield return Start(GodBoardStart.Id);
            GodBoardSettings.Override = null;
            bool saved = GodBoardSettings.ClearView;
            try
            {
                GodBoardSettings.ClearView = false;
                _boot.Clock.Pause("WorldTourTest");
                if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
                var d = GodBoardDirector.Instance;
                var hud = d.GetComponent<GodBoardHud>();
                Assert.IsTrue(d.TrySacrifice(true));
                _boot.Clock.StepOnce(); _boot.Clock.StepOnce();
                float t0 = Time.realtimeSinceStartup;
                while (d.View != BoardView.Board) { Assert.Less(Time.realtimeSinceStartup - t0, 30f); yield return null; }
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsFalse(hud.Texts.Any(x => x.StartsWith("[Plague")), "world first: no Plague button");
                Assert.IsTrue(hud.Rig.Shown, "the ledge is up");
                // The Esc menu: open it, press the Clear view button's handler, close it.
                Hud.Instance.OpenMenu();
                Assert.IsTrue(Hud.MenuOpen);
                Assert.IsTrue(Hud.ToggleClearView());
                Hud.Instance.CloseMenu();
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsTrue(GodBoardSettings.ClearView);
                Assert.IsTrue(PlagueHud.ClearView, "the plague buttons read the same setting");
                Assert.IsTrue(hud.Texts.Any(x => x.StartsWith("[Plague")), "the Plague button is back: " + string.Join("|", hud.Texts));
                Assert.IsTrue(hud.Texts.Any(x => x.StartsWith("[")), "the board HUD is drawn");
                Assert.IsFalse(hud.Rig.Shown, "the ledge is down");
                // The duel panel, in the same game.
                d.Leave(); _boot.Clock.StepOnce(); _boot.Clock.StepOnce();
                t0 = Time.realtimeSinceStartup;
                while (d.View != BoardView.Altar) { Assert.Less(Time.realtimeSinceStartup - t0, 30f); yield return null; }
                var ring = DuelRing.Instance;
                Assert.IsTrue(ring.StartDuel());
                t0 = Time.realtimeSinceStartup;
                while (ring.State != DuelRing.Errand.Dueling) { Assert.Less(Time.realtimeSinceStartup - t0, 60f); yield return null; }
                yield return null; yield return null; yield return null;
                var e = WardDuelEncounter.Current;
                Assert.IsNotNull(e);
                Assert.IsFalse(e.WorldFirst, "the duel reads the same setting");
                Assert.IsFalse(e.Props.RigVisible);
                Assert.IsNotNull(e.LastLayout, "DuelLayout draws the panel");
                // Switch it off from the menu again: the world objects return.
                Assert.IsFalse(Hud.ToggleClearView());
                yield return null; yield return null;
                Assert.IsTrue(e.WorldFirst); Assert.IsTrue(e.Props.RigVisible);
                EncounterHost.Instance.Abort();
                t0 = Time.realtimeSinceStartup;
                while (ring.State != DuelRing.Errand.None) { Assert.Less(Time.realtimeSinceStartup - t0, 90f); yield return null; }
            }
            finally { GodBoardSettings.ClearView = saved; }
        }
    }
}
