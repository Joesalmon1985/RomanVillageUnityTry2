using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace World.Game.Tests
{
    public class WorldBootTests
    {
        [UnityTest]
        public IEnumerator WorldScene_Boots_AndSimTicks()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;

            var boots = Object.FindObjectsByType<WorldBootstrap>(FindObjectsSortMode.None);
            Assert.AreEqual(1, boots.Length, "exactly one WorldBootstrap");
            var boot = boots[0];
            Assert.IsNotNull(boot.Sim, "sim created");
            Assert.IsNotNull(boot.Clock);
            Assert.IsNotNull(boot.MainCamera);
            Assert.AreEqual(61, boot.Sim.State.Board.Topology.HexCount, "radius-4 board");

            long t0 = boot.Sim.State.Tick;
            float until = Time.realtimeSinceStartup + 3f;
            while (boot.Sim.State.Tick < t0 + 3 && Time.realtimeSinceStartup < until) yield return null;
            Assert.GreaterOrEqual(boot.Sim.State.Tick, t0 + 3, "tick advances at 10 Hz");

            boot.Clock.Pause(PauseReasons.Menu);
            boot.Clock.Pause(PauseReasons.Draft);
            long paused = boot.Sim.State.Tick;
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(paused, boot.Sim.State.Tick, "paused clock does not tick");
            boot.Clock.Resume(PauseReasons.Menu);
            Assert.IsTrue(boot.Clock.IsPaused, "still paused for Draft");
            boot.Clock.Resume(PauseReasons.Draft);
            Assert.IsFalse(boot.Clock.IsPaused);
        }
    }
}
