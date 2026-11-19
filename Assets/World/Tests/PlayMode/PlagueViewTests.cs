using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Plague;

namespace World.Game.Tests
{
    /// <summary>G2-2: the plague overlay, the demons on the ground, the banish on foot and the epidemic storm (PL-6, PL-11, PL-12).</summary>
    public class PlagueViewTests
    {
        private const string TestPause = "PlagueViewTest";
        private WorldBootstrap _boot;
        private PlagueViews _v;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; }
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
    }
}
