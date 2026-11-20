using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Plague;

namespace World.Game.Tests
{
    /// <summary>G2-2 and WU-3: the plague overlay, the demons on the ground, the banish on foot and the epidemic storm (PL-6, PL-11, PL-12).</summary>
    public class PlagueViewTests
    {
        private const string TestPause = "PlagueViewTest";
        private WorldBootstrap _boot;
        private PlagueViews _v;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; PointerInput.InputEnabled = true; WorldActions.Cancel(); }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; }

        private IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
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
            _v = PlagueViews.Instance;
            Assert.IsNotNull(_v, "plague views installed");
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private void Infect(int rounds)
        {
            var ev = new EventBuffer();
            for (int i = 0; i < rounds; i++) PlagueRules.InfectRound(S, ev);
        }

        private int NodeWithDemons()
        {
            for (int n = 1; n <= PlagueGraphData.NodeCount; n++) if (PlagueApi.DemonsAt(S, n) > 0) return n;
            return 0;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Overlay_Toggles_AndShows48Nodes()
        {
            yield return Start();
            Assert.AreEqual(48, _v.OverlayNodeCount);
            Assert.AreEqual(48, _v.Nodes.Count);
            Assert.AreEqual(73, _v.OverlayTrailCount);
            Assert.Greater(_v.OverlayHexCount, 30, "regions are tinted");
            Assert.IsFalse(_v.Overlay);
            yield return null;
            Assert.IsFalse(_v.OverlayRoot.activeSelf);
            _v.ToggleOverlay();
            yield return null;
            Assert.IsTrue(_v.Overlay);
            Assert.IsTrue(_v.BeaconRoot.activeSelf, "on foot the beacons show");
            Assert.IsFalse(_v.OverlayRoot.activeSelf, "the elevated layer is for the board");
            // On the board the elevated layer shows and its 48 node markers are children.
            var d = GodBoardDirector.Instance;
            Assert.IsTrue(d.TrySacrifice(true));
            Step(2);
            float t0 = Time.realtimeSinceStartup;
            while (d.View != BoardView.Board) { Assert.Less(Time.realtimeSinceStartup - t0, 10f); yield return null; }
            yield return null;
            Assert.IsTrue(_v.OverlayRoot.activeSelf);
            int markers = 0;
            foreach (Transform c in _v.OverlayRoot.transform) if (c.name.StartsWith("OverlayNode")) markers++;
            Assert.AreEqual(48, markers);
            _v.ToggleOverlay();
            yield return null;
            Assert.IsFalse(_v.OverlayRoot.activeSelf);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Demons_Appear_AfterInfection()
        {
            yield return Start();
            Assert.AreEqual(0, NodeWithDemons());
            for (int n = 1; n <= 48; n++) Assert.AreEqual(0, _v.FiguresAt(n));
            Infect(3);
            yield return null;
            int node = NodeWithDemons();
            Assert.Greater(node, 0, "the draw placed a demon");
            Assert.AreEqual(PlagueApi.DemonsAt(S, node), _v.FiguresAt(node));
            Assert.Greater(_v.FiguresAt(node), 0);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Banish_NearANode_SendsTheCommand_AndTheCountDrops()
        {
            yield return Start();
            Infect(3);
            yield return null;
            int node = NodeWithDemons();
            Assert.Greater(node, 0);
            int before = PlagueApi.DemonsAt(S, node);
            var w = PlayerWalker.Instance;
            var at = _v.NodePosition(node);
            // Far away: refused by the view, nothing sent.
            w.Teleport(at + new Vector3(120f, 0, 0), 0f, 8f);
            yield return null;
            var far = _v.TryBanishNear();
            Assert.IsFalse(far.Ok);
            Assert.AreEqual(0, _v.BanishesSent);
            // Beside it: the command goes out.
            w.Teleport(at + new Vector3(8f, 0, 0), 270f, 8f);
            yield return null;
            Assert.AreEqual(node, _v.NodeNearPlayer);
            var r = _v.TryBanishNear();
            Assert.IsTrue(r.Ok, r.Message);
            Step(2);
            yield return null;
            Assert.AreEqual(1, _v.BanishesSent);
            Assert.AreEqual(before - 1, PlagueApi.DemonsAt(S, node));
            Assert.AreEqual(before - 1, _v.FiguresAt(node));
            Assert.Greater(_v.CooldownTicksLeft, 0, "the cooldown shows");
            Assert.GreaterOrEqual(_v.PurgesPlayed, 1, "the purge plays");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Epidemic_DarkensTheSky()
        {
            yield return Start();
            Assert.AreEqual(0f, _v.StormLevel);
            _v.BeginStorm();
            yield return null;
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.Greater(_v.StormLevel, 0.9f);
            Assert.AreEqual(0, _v.EpidemicsSeen);
        }

        // ------------------------------------------------------------------ WU-3: the plague at the healer's lodge

        private LodgeTable Lodge => HealersLodge.Instance.Plague;

        private void AssertTableMatches()
        {
            var t = Lodge;
            for (int n = 1; n <= 48; n++)
            {
                Assert.AreEqual(_v.FiguresAt(n), t.TableFigures(n), "table figures = overlay figures at node " + n);
                for (int k = 0; k < 4; k++)
                {
                    Assert.AreEqual(PlagueApi.DemonsOfKindAt(S, n, k), t.TableDemons(n, k), "table demons node " + n + " kind " + k);
                    Assert.AreEqual(_v.NodeOf(n).Kinds[k], t.TableDemons(n, k), "overlay kinds node " + n + " kind " + k);
                }
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Lodge_Table_ShowsTheSameDemons_AfterInfection_AndAfterBanish()
        {
            yield return Start();
            Assert.IsNotNull(HealersLodge.Instance, "the lodge is built"); Assert.IsNotNull(Lodge, "its plague props exist");
            Assert.AreEqual(48, Lodge.NodeTaps.Count);
            yield return null;
            AssertTableMatches();
            Infect(4);
            yield return null; yield return null;
            Assert.Greater(PlagueApi.TotalDemons(S), 0);
            AssertTableMatches();
            int node = NodeWithDemons(); int before = PlagueApi.DemonsAt(S, node);
            var w = PlayerWalker.Instance;
            w.Teleport(_v.NodePosition(node) + new Vector3(8f, 0, 0), 270f, 8f);
            yield return null;
            Assert.IsTrue(_v.TryBanishNear().Ok);
            Step(2); yield return null; yield return null;
            Assert.AreEqual(before - 1, PlagueApi.DemonsAt(S, node));
            AssertTableMatches();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Lodge_Lanterns_Peg_AndVessels_FollowTheState()
        {
            yield return Start();
            yield return null;
            var t = Lodge;
            Assert.AreEqual(0, t.LanternsLit); Assert.AreEqual(1, t.PegStep);
            var ev = new EventBuffer();
            int guard = 0;
            while (S.Plague.Outbreaks < 3 && guard++ < 400) { PlagueRules.InfectRound(S, ev); PlagueRules.Epidemic(S, ev); }
            Assert.GreaterOrEqual(S.Plague.Outbreaks, 3, "the test could not cause outbreaks");
            Assert.Greater(S.Plague.RateIndex, 0, "epidemics moved the rate");
            yield return null; yield return null;
            Assert.AreEqual(S.Plague.Outbreaks, t.LanternsLit, "lanterns = outbreaks");
            Assert.AreEqual(S.Plague.RateIndex + 1, t.PegStep, "peg = rate step");
            // Vessels: empty, cured, eradicated, strengthened (Ash altar).
            for (int e = 0; e < 4; e++) Assert.AreEqual(VesselState.Empty, t.VesselShown(e));
            S.Plague.Cured[0] = true; S.Plague.Eradicated[1] = true; S.Plague.Cured[1] = true; S.Plague.Strengthened[2] = true;
            yield return null; yield return null;
            Assert.AreEqual(VesselState.Cured, t.VesselShown(0));
            Assert.AreEqual(VesselState.Eradicated, t.VesselShown(1));
            Assert.AreEqual(VesselState.Strengthened, t.VesselShown(2));
            Assert.AreEqual(VesselState.Empty, t.VesselShown(3));
            S.Plague.Strengthened[2] = false; S.Plague.Cured[0] = false;
            yield return null; yield return null;
            Assert.AreEqual(VesselState.Empty, t.VesselShown(0)); Assert.AreEqual(VesselState.Empty, t.VesselShown(2));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Lodge_Says_NothingOfEpidemicsOrCardsToCome_AndTagsItsZones()
        {
            yield return Start();
            Infect(5);
            S.Plague.Cured[2] = true;
            yield return null;
            var bad = new[] { "remain", "cards", "deck", "epidemics", " left in", "to come", "next epidemic" };
            int n = 0;
            foreach (var text in Lodge.AllTexts())
            {
                n++;
                Assert.IsNull(LedgerText.Violation(text), "AB-1 word in: " + text);
                foreach (var b in bad) Assert.IsFalse(text.ToLowerInvariant().Contains(b), b + " in: " + text);
            }
            Assert.Greater(n, 60);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var z in WorldZone.All) ids.Add(z.ZoneId);
            foreach (var id in new[] { WorldZones.PlgMap, WorldZones.PlgDanger, WorldZones.PlgCures, WorldZones.PlgDeck, WorldZones.PlgActions, WorldZones.PlgPhase, WorldZones.PlgCity })
                Assert.IsTrue(ids.Contains(id), id + " is tagged");
            // A tap on a prop opens its ledger (inspection only).
            Lodge.Post.Interact();
            Assert.AreEqual(WorldActionState.Inspect, WorldActions.State);
            Assert.IsTrue(LedgerCard.IsOpen);
            Assert.AreEqual("Warning post", LedgerCard.Content.Title);
            WorldActions.Cancel();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Shrine_Tap_Previews_ThenBanishesOnce_AndTheCharmCools()
        {
            yield return Start();
            Infect(3);
            yield return null;
            int node = NodeWithDemons(); Assert.Greater(node, 0);
            int before = PlagueApi.DemonsAt(S, node);
            var nv = _v.NodeOf(node);
            var w = PlayerWalker.Instance; var at = _v.NodePosition(node);
            // Far: refused with a reason, nothing sent.
            w.Teleport(at + new Vector3(120f, 0, 0), 0f, 8f);
            yield return null; yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(WorldTapResult.Refused, WorldActions.Tap(nv));
            Assert.AreEqual(WorldActionState.Refused, WorldActions.State);
            StringAssert.Contains("closer", WorldActions.Machine.Reason);
            Assert.AreEqual(0, _v.BanishesSent);
            WorldActions.Cancel();
            // Near: the first tap previews (nothing sent), the second confirms once.
            w.Teleport(at + new Vector3(8f, 0, 0), 270f, 8f);
            yield return null; yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(WorldTapResult.Previewed, WorldActions.Tap(nv));
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
            Assert.IsTrue(nv.Previewing); Assert.IsTrue(LedgerCard.IsOpen);
            Assert.AreEqual(0, _v.BanishesSent);
            Assert.AreEqual(before, PlagueApi.DemonsAt(S, node));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(WorldTapResult.Confirmed, WorldActions.Tap(nv));
            Assert.AreEqual(WorldTapResult.Ignored, WorldActions.Tap(nv), "the guard swallows a burst");
            Step(2); yield return null;
            Assert.AreEqual(1, _v.BanishesSent);
            Assert.AreEqual(before - 1, PlagueApi.DemonsAt(S, node));
            Assert.Greater(_v.CooldownTicksLeft, 0);
            Assert.Less(BanishCharm.Instance.Ready01, 1f, "the charm cools");
            yield return new WaitForSecondsRealtime(0.8f);
            if (PlagueApi.DemonsAt(S, node) > 0)
            {
                Assert.AreEqual(WorldTapResult.Refused, WorldActions.Tap(nv));
                StringAssert.Contains("smouldering", WorldActions.Machine.Reason);
            }
            WorldActions.Cancel();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NoPlagueOrBanishButtons_ByDefault_AndTheTotemToggleWorks()
        {
            yield return Start();
            PlagueHud.ClearView = false;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(PlagueHud.ClearView);
            Assert.AreEqual(0, _v.Hud.Texts.Count, "no plague controls are drawn");
            Assert.IsNull(GodBoardHud.PlagueToggle, "the board's round Plague icon is not offered");
            Assert.IsFalse(PlagueViews.OverlayOn);
            Assert.IsTrue(PlagueViews.ToggleOverlayFromTotem());
            Assert.IsTrue(_v.Overlay);
            Assert.IsFalse(PlagueViews.ToggleOverlayFromTotem());
        }

#if UNITY_EDITOR
        /// <summary>P2b: a tap on the table leans the camera over it; at 844 x 390 every pebble is at least the hit-target size and a tap on its centre picks it; the island and the docked card do not overlap.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator P2b_LodgeTable_LeanView_PebblesAreHitTargets_At844x390_AndFigurinesDoNotOverlap()
        {
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint w0, out uint h0);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(844, 390, "P2b lodge 844x390");
            try
            {
                for (int i = 0; i < 10 && Screen.height != 390; i++) yield return null;
                ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(Screen.width, Screen.height);
                yield return Start();
                Infect(5);
                // three demons of mixed kinds on one shrine
                for (int k = 0; k < 3; k++) S.Plague.KindCount[(5 - 1) * 4 + k] = 1;
                yield return null; yield return null;
                var t = Lodge;
                Assert.IsTrue(t.PropsShown, "the table is up while the plague runs");
                var w = PlayerWalker.Instance;
                w.Teleport(t.TableCentre + new Vector3(0, 0, -3.5f), 0f, 8f);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.IsFalse(t.Leaning);
                var inspected = WorldActions.Inspect(t.Table);
                for (int i = 0; i < 5 && !t.Leaning; i++) yield return null;
                Assert.IsTrue(t.Leaning, "a tap on the table leans the camera over it (inspect " + inspected + ", state " + WorldActions.State + ", locked " + w.IsLocked + ", gliding " + w.IsGliding + ", auto " + w.IsAutoWalking + ")");
                for (int i = 0; i < 3; i++) yield return null;
                var cam = w.Camera;
                float H = Screen.height, W = Screen.width;
                var safe = ScreenSafeArea.Current();
                // every pebble: grown hit rect >= the minimum; a tap at its centre picks that very pebble
                float min = WorldHitTarget.MinSide(H);
                for (int n = 1; n <= 48; n++)
                {
                    var li = t.NodeTap(n);
                    Assert.IsTrue(WorldHitTarget.ScreenBounds(li, cam, out var r), "pebble " + n + " is in front of the camera");
                    var big = WorldHitTarget.Enlarge(r, H);
                    Assert.GreaterOrEqual(big.width, min - 0.5f, "pebble " + n + " hit width"); Assert.GreaterOrEqual(big.height, min - 0.5f, "pebble " + n + " hit height");
                    var picked = WorldActions.PickEnlarged(new Vector2(r.center.x, H - r.center.y), cam);
                    Assert.AreSame(li, picked, "a tap at the centre of pebble " + n + " picks it");
                }
                // the island is inside the safe area and left of the card
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                foreach (var dx in new[] { -1f, 1f }) foreach (var dz in new[] { -1f, 1f })
                {
                    var sp = cam.WorldToScreenPoint(t.TableTop + new Vector3(dx * t.IslandHalfW, 0, dz * t.IslandHalfD));
                    float gy = H - sp.y;
                    x0 = Mathf.Min(x0, sp.x); x1 = Mathf.Max(x1, sp.x); y0 = Mathf.Min(y0, gy); y1 = Mathf.Max(y1, gy);
                }
                var island = Rect.MinMaxRect(x0, y0, x1, y1);
                Assert.IsTrue(island.xMin >= safe.xMin - 1 && island.yMin >= safe.yMin - 1 && island.yMax <= safe.yMax + 1 && island.xMax <= safe.xMax, "island " + island + " in safe " + safe);
                Assert.Greater(island.height, H * 0.7f, "the island fills the picture's height: " + island);
                yield return new WaitForSecondsRealtime(0.8f);
                var node = t.NodeTap(5);
                WorldActions.Cancel();
                Assert.AreEqual(WorldTapResult.Inspected, WorldActions.Inspect(node));
                yield return null;
                var card = LedgerCard.CurrentLayout(cam).Card;
                Assert.IsFalse(card.Overlaps(island), "the card " + card + " does not cover the island " + island);
                WorldActions.Cancel();
                t.EndLean(true);
                Assert.IsFalse(LedgerCard.DockRight);

                // figurines: three of mixed kinds stand clear of one another
                var fig = new System.Collections.Generic.List<Vector3>();
                foreach (var g in t.NodeTap(5).GetComponentsInChildren<Transform>()) if (g.name.StartsWith("Demon ")) fig.Add(g.position);
                Assert.AreEqual(3, fig.Count);
                for (int i = 0; i < 3; i++) for (int j = i + 1; j < 3; j++)
                    Assert.Greater(Vector3.Distance(fig[i], fig[j]), 0.07f, "figurines " + i + " and " + j + " overlap");
            }
            finally { UnityEditor.PlayModeWindow.SetCustomRenderingResolution(w0, h0, "restore"); ScreenSafeArea.Override = null; }
        }
#endif
    }
}
