using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;

namespace World.Game.Tests
{
    /// <summary>
    /// Package L (Docs/LIGHTING.md): the game starts at night with its lights on; the time of day follows the game
    /// clock and holds while it is paused; lights fade at dawn; the per-pixel light budget holds in a late game;
    /// a loaded game shows the lighting of its saved game time.
    /// </summary>
    public class LightingTests
    {
        private const string TestPause = "LightTest";
        private WorldBootstrap _boot;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            DayNight.DevOffsetHours = 0; DayNight.DevTimeLapse = 0f;
            _path = Path.Combine(Application.temporaryCachePath, "worldmvp_light_test_save.json");
            if (File.Exists(_path)) File.Delete(_path);
            SaveSystem.PathOverride = _path;
            SaveSystem.TestPauseOnLoad = TestPause;
        }

        [TearDown]
        public void TearDown()
        {
            DayNight.DevOffsetHours = 0; DayNight.DevTimeLapse = 0f;
            SaveSystem.PathOverride = null;
            SaveSystem.TestPauseOnLoad = null;
            if (File.Exists(_path)) File.Delete(_path);
        }

        private IEnumerator Load(bool pause = true)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot);
            if (pause) _boot.Clock.Pause(TestPause);
            Assert.IsNotNull(DayNight.Instance, "DayNight installed");
            Assert.IsNotNull(WorldLights.Instance, "WorldLights installed");
            Assert.IsNotNull(LightDecorator.Instance, "LightDecorator installed");
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        private void StepTo(double hour)
        {
            // Steps the game clock until the time of day reaches the hour (at most one day).
            int ticksPerDay = Mathf.RoundToInt(DayNight.DayLengthSeconds * WorldSim.TicksPerSecond);
            for (int i = 0; i < ticksPerDay; i++)
            {
                double h = DayNight.HourAtTick(_boot.Sim.State.Tick);
                double ahead = (hour - h + 24.0) % 24.0;
                if (ahead < 24.0 / ticksPerDay * 1.5) return;
                _boot.Clock.StepOnce();
            }
            Assert.Fail("hour " + hour + " not reached");
        }

        private static int PerPixelPointLights()
        {
            int n = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled && l.gameObject.activeInHierarchy && l.type == LightType.Point && l.intensity > 0f && l.renderMode != LightRenderMode.ForceVertex) n++;
            return n;
        }

        private static int Directionals()
        {
            int n = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled && l.gameObject.activeInHierarchy && l.type == LightType.Directional && l.intensity > 0f) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator NewGame_StartsAtNight_WithLightsOn()
        {
            yield return Load();
            yield return Frames(20);
            var dn = DayNight.Instance;
            Assert.IsTrue(dn.State.SunBelowHorizon, "sun below the horizon at the start, elevation " + dn.State.SunElevation);
            Assert.Less(dn.State.SunElevation, -30f, "deep night");
            Assert.AreEqual(1f, dn.Night, 0.001f, "night level");
            Assert.IsFalse(dn.Sun.enabled, "sun light off");
            Assert.IsTrue(dn.Moon.enabled && dn.Moon.intensity > 0.3f, "moonlight on");
            Assert.AreSame(dn.Moon, RenderSettings.sun);
            Assert.Greater(dn.State.Stars, 0.95f, "stars out");
            Assert.AreEqual(1, Directionals(), "one directional light at a time");

            // Never black: the ambient fill is blue and present.
            Assert.Greater(RenderSettings.ambientSkyColor.b, 0.2f);
            Assert.Greater(RenderSettings.ambientGroundColor.b, 0.05f);
            Assert.Greater(RenderSettings.ambientSkyColor.b, RenderSettings.ambientSkyColor.r, "blue fill");

            // Fires and torches are lit: the home camp's hearth, its torches, Mira's brazier, the dance fire, the lantern.
            var home = LandViews.Instance.Settlement(0);
            Assert.IsNotNull(home);
            var lit = home.GetComponent<LitBy>();
            Assert.IsNotNull(lit, "the home camp has its lights");
            Assert.Greater(lit.Main.Level, 0.95f, "hearth light on");
            Assert.GreaterOrEqual(lit.Torches.Count, 3, "entrance torches");
            foreach (var t in lit.Torches) { Assert.Greater(t.Level, 0.95f); Assert.IsTrue(t.Flame.gameObject.activeSelf, "torch flame shown"); }
            Assert.IsNotNull(LightDecorator.Instance.MiraBrazier);
            Assert.Greater(LightDecorator.Instance.MiraBrazier.Level, 0.95f);
            Assert.IsNotNull(LightDecorator.Instance.DanceFire, "the dance fire is adopted");
            Assert.IsTrue(WorldLights.Instance.Lantern.enabled && WorldLights.Instance.Lantern.intensity > 0.3f, "the player's lantern is lit");
            Assert.Greater(PerPixelPointLights(), 1, "real point lights near the spawn");
            Assert.AreEqual(2, LightDecorator.Instance.CampsLit, "both peoples' camps are lit");
            var rival = LandViews.Instance.Settlement(1).GetComponent<LitBy>();
            Assert.Greater(rival.Main.Color.g, rival.Main.Color.r, "the Ash Clan's fire is green, not warm");
            Assert.Greater(lit.Main.Color.r, lit.Main.Color.g, "your fire is warm");

            // The first minutes are properly night: three real minutes of game clock later the sun is still well down.
            var later = DayNight.Evaluate(DayNight.HourAtTick(180 * WorldSim.TicksPerSecond));
            Assert.AreEqual(1f, later.Night, 0.001f, "still night after three minutes");
            Assert.Less(later.SunElevation, -16f);
            Assert.AreEqual(0f, later.SunIntensity);
        }

        [UnityTest]
        public IEnumerator TimeOfDay_FollowsTheGameClock_AndHoldsWhilePaused()
        {
            yield return Load();
            yield return null;
            var dn = DayNight.Instance;
            long t0 = _boot.Sim.State.Tick;
            Assert.AreEqual(DayNight.HourAtTick(t0), dn.Hour, 1e-6, "paused: the hour is exactly the tick's");

            for (int i = 0; i < 300; i++) _boot.Clock.StepOnce();
            yield return null;
            double perTick = 24.0 / (DayNight.DayLengthSeconds * WorldSim.TicksPerSecond);
            Assert.AreEqual(DayNight.HourAtTick(t0) + 300 * perTick, dn.Hour, 1e-6, "300 ticks later");
            Assert.AreEqual(720f, DayNight.DayLengthSeconds, "default day length: 12 real minutes");

            // Paused (an encounter, the menu): nothing moves.
            double held = dn.Hour; var sun = dn.State.SunDir; var moon = dn.Moon.transform.rotation;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(held, dn.Hour, 0.0, "the hour holds while the clock is paused");
            Assert.AreEqual(sun, dn.State.SunDir);
            Assert.AreEqual(moon, dn.Moon.transform.rotation);

            // Running: it advances with the ticks, at the clock's rate.
            _boot.Clock.Resume(TestPause);
            long t1 = _boot.Sim.State.Tick;
            float until = Time.realtimeSinceStartup + 5f;
            while (_boot.Sim.State.Tick < t1 + 8 && Time.realtimeSinceStartup < until) yield return null;
            _boot.Clock.Pause(TestPause);
            yield return null;
            long t2 = _boot.Sim.State.Tick;
            Assert.GreaterOrEqual(t2, t1 + 8);
            Assert.AreEqual(DayNight.HourAtTick(t2), dn.Hour, 1e-6);
            Assert.Greater(dn.Hour, held);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Lights_FadeAtDawn_AndComeBackAtDusk()
        {
            yield return Load();
            yield return Frames(10);
            var dn = DayNight.Instance;
            var lit = LandViews.Instance.Settlement(0).GetComponent<LitBy>();
            var torch = lit.Torches[0];
            Assert.Greater(torch.Level, 0.95f);

            StepTo(5.9); // first light: the sun is just under the horizon, the lights are part way down
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.Greater(dn.Night, 0.02f); Assert.Less(dn.Night, 0.98f, "dawn: between night and day");
            Assert.Greater(dn.State.HorizonGlow.r, 0.2f, "horizon glow at dawn");
            Assert.Less(torch.Level, 0.98f); Assert.Greater(torch.Level, 0.01f);

            StepTo(8.0);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(0f, dn.Night, 0.001f, "day");
            Assert.IsTrue(dn.Sun.enabled && dn.Sun.intensity > 0.3f, "sun up");
            Assert.IsFalse(dn.Moon.enabled, "moon light off");
            Assert.AreSame(dn.Sun, RenderSettings.sun);
            Assert.AreEqual(0f, torch.Level, 0.001f, "torch out by day");
            Assert.IsFalse(torch.Light.enabled, "torch light off");
            Assert.IsFalse(torch.Flame.gameObject.activeSelf, "torch flame hidden");
            Assert.IsFalse(WorldLights.Instance.Lantern.enabled, "lantern out");
            Assert.AreEqual(lit.Main.DayLevel, lit.Main.Level, 0.01f, "the hearth keeps a low day glow");
            Assert.AreEqual(0f, dn.State.Stars, 0.001f);
            Assert.AreEqual(1, Directionals());

            StepTo(19.5);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(1f, dn.Night, 0.001f, "night again");
            Assert.Greater(torch.Level, 0.95f, "torches relit at dusk");
            Assert.IsTrue(torch.Flame.gameObject.activeSelf);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LateGame_LightBudget_IsNotExceeded()
        {
            yield return Load();
            LandLateGame.Grow(_boot.Sim, 2300, new[] { 4, 4 }, true, () => _boot.Clock.StepOnce());
            yield return null;
            DayNight.Instance.SetHour(23.0); // the developer scrub: late game, at night
            LightDecorator.Instance.ScanNow();
            yield return Frames(5);
            Assert.AreEqual(1f, DayNight.Instance.Night, 0.001f);
            Assert.Greater(WorldLights.Registered, 40, "a late game has many fires: " + WorldLights.Registered);
            Assert.Greater(LightDecorator.Instance.BuildingsLit, 10, "door torches");
            Assert.GreaterOrEqual(LightDecorator.Instance.CampsLit, 4);

            var e = _boot.Sim.State.Economy;
            var topo = _boot.Sim.State.Board.Topology;
            int checkedSpots = 0, worst = 0;
            foreach (var st in e.Settlements)
            {
                if (st.State != World.Sim.Economy.SettlementState.Active) continue;
                var c = BoardSpace.Vertex(topo, st.Corner);
                _boot.Avatar.Teleport(c + new Vector3(9f, 0.05f, 4f), -115f, 6f);
                yield return new WaitForSecondsRealtime(0.7f);
                int px = PerPixelPointLights();
                worst = Mathf.Max(worst, px);
                Assert.LessOrEqual(px, WorldLights.PixelBudget, "per-pixel point lights at camp " + st.Id);
                Assert.LessOrEqual(WorldLights.Instance.PixelLightsNow, WorldLights.PixelBudget);
                Assert.AreEqual(0, WorldLights.Instance.VertexLightsNow, "no per-vertex lights (they flood the large ground triangles)");
                Assert.AreEqual(1, Directionals(), "one directional light");
                Assert.Greater(px, 1, "the camp is really lit");
                checkedSpots++;
            }
            Assert.GreaterOrEqual(checkedSpots, 4);
            Assert.LessOrEqual(QualitySettings.pixelLightCount, WorldLights.PixelLightCount);
            Debug.Log("[LightingTests] late game: " + WorldLights.Registered + " fire lights registered, at most " + worst + " per-pixel point lights at once over " + checkedSpots + " camps");
        }

        [UnityTest]
        public IEnumerator Restore_FromSavedGameTime_GivesTheSameLighting()
        {
            // The pure function: the same game time always gives the same lighting.
            var a = DayNight.Evaluate(DayNight.HourAtTick(1234));
            var b = DayNight.Evaluate(DayNight.HourAtTick(1234));
            Assert.AreEqual(a.SunDir, b.SunDir); Assert.AreEqual(a.Night, b.Night); Assert.AreEqual(a.AmbientSky, b.AmbientSky);
            Assert.AreNotEqual(a.SunDir, DayNight.Evaluate(DayNight.HourAtTick(1934)).SunDir);

            yield return Load();
            for (int i = 0; i < 2250; i++) _boot.Clock.StepOnce(); // into the dawn, where everything is changing fastest
            yield return Frames(3);
            var dn = DayNight.Instance;
            var before = dn.State; double hour = dn.Hour;
            Assert.Greater(before.Night, 0.01f); Assert.Less(before.Night, 0.99f, "saved during the dawn");
            Color ambient = RenderSettings.ambientSkyColor, fog = RenderSettings.fogColor;
            var moonRot = dn.Moon.transform.rotation; float moonInt = dn.Moon.intensity; bool sunOn = dn.Sun.enabled;
            int fires = WorldLights.Registered;

            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            var old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            for (int i = 0; i < 200 && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old); i++) yield return null;
            yield return Frames(3);
            _boot = WorldBootstrap.Instance;
            Assert.AreNotSame(old, _boot, "the scene reloaded");
            var dn2 = DayNight.Instance;
            Assert.AreNotSame(dn, dn2);
            Assert.AreEqual(hour, dn2.Hour, 1e-9, "the hour of the saved game time");
            var after = dn2.State;
            Assert.AreEqual(before.SunDir, after.SunDir); Assert.AreEqual(before.MoonDir, after.MoonDir);
            Assert.AreEqual(before.Night, after.Night); Assert.AreEqual(before.Stars, after.Stars);
            Assert.AreEqual(before.SunIntensity, after.SunIntensity); Assert.AreEqual(before.MoonIntensity, after.MoonIntensity);
            Assert.AreEqual(before.Zenith, after.Zenith); Assert.AreEqual(before.Horizon, after.Horizon);
            Assert.AreEqual(ambient, RenderSettings.ambientSkyColor); Assert.AreEqual(fog, RenderSettings.fogColor);
            Assert.AreEqual(sunOn, dn2.Sun.enabled);
            Assert.AreEqual(moonInt, dn2.Moon.intensity, 1e-6f);
            Assert.Less(Quaternion.Angle(moonRot, dn2.Moon.transform.rotation), 0.001f);
            Assert.AreEqual(fires, WorldLights.Registered, "the same fires are registered");
            // Lights come up at their level at once (no fade-in after a load).
            var lit = LandViews.Instance.Settlement(0).GetComponent<LitBy>();
            Assert.AreEqual(Mathf.Lerp(lit.Main.DayLevel, 1f, after.Night), lit.Main.Level, 0.02f);
        }
    }
}
