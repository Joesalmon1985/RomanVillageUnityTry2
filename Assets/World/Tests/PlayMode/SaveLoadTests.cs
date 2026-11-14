using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Save;

namespace World.Game.Tests
{
    /// <summary>
    /// The save slot in the scene, F-0 subset: refusals (missing, corrupt, older, empty) and saving during an encounter,
    /// plus a plain round trip of the tick and the interim avatar pose. The full G5 tests of the MVP (carrier mid-road,
    /// exactly-once delivery, rhythm Esc, Go here) are in Archive/Tests/PlayMode/SaveLoadTests.cs; package F rewrites the
    /// shipment and first-scene round trip in F-1 (REFACTOR_SPEC 8.6). The slot is a temporary file.
    /// </summary>
    public class SaveLoadTests
    {
        private const string TestPause = "Test";
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "worldmvp_test_save.json");
            if (File.Exists(_path)) File.Delete(_path);
            SaveSystem.PathOverride = _path;
            SaveSystem.TestPauseOnLoad = TestPause;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.PathOverride = null;
            SaveSystem.TestPauseOnLoad = null;
            if (File.Exists(_path)) File.Delete(_path);
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot);
            Assert.IsNotNull(_boot.Saves, "SaveSystem installed");
            _boot.Clock.Pause(TestPause);
        }

        private IEnumerator WaitForReload(WorldBootstrap old)
        {
            for (int i = 0; i < 200 && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old); i++) yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.AreNotSame(old, _boot, "the scene reloaded");
        }

        [UnityTest]
        public IEnumerator Save_Load_RoundTrip_StateAndPose()
        {
            yield return Load();
            for (int i = 0; i < 250; i++) _boot.Clock.StepOnce(); // past a few rolls
            var pos = WalkLattice.ToWorld(new CampLayout(S.Board).Spawn) + new Vector3(7.3f, 0.05f, -3.1f);
            _boot.Avatar.Teleport(pos, 123f, -7f);
            yield return null;
            string before = SaveCodec.StateJson(S);
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            var old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            Assert.AreEqual(before, SaveCodec.StateJson(S), "loaded state equals the saved state");
            Assert.Less(BoardSpace.Flat(_boot.Avatar.Position - pos), 0.01f, "pose restored");
            Assert.AreEqual(123f, _boot.Avatar.Yaw, 0.01f);
        }

        [UnityTest]
        public IEnumerator Save_Load_WalkerOnTheLattice_FirstScene_Hud_AndTimeOfDay()
        {
            yield return Load();
            // Run the opening on: the prince asks (first scene step 1 or later) and some game time passes.
            _boot.Clock.Resume(TestPause);
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until && (PrinceDirector.Instance.FirstSceneStep < PrinceDirector.SceneAsked || S.Tick < 60)) yield return null;
            _boot.Clock.Pause(TestPause);
            Assert.GreaterOrEqual(PrinceDirector.Instance.FirstSceneStep, PrinceDirector.SceneAsked, "the prince asked");
            int step = PrinceDirector.Instance.FirstSceneStep;
            var cell = new CampLayout(S.Board).Spawn + WalkLattice.Dir(2) * 2;
            _boot.Avatar.StopAuto();
            _boot.Avatar.Teleport(cell, 4);
            Hud.Visible = true;
            yield return null;
            long tick = S.Tick;
            double hour = DayNight.HourAtTick(tick);
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            StringAssert.Contains("\"Version\":" + SaveCodec.CurrentVersion, File.ReadAllText(_path), "the current save version");
            Hud.Visible = false;
            var old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            yield return null;
            Assert.AreEqual(tick, S.Tick, "tick restored (the loaded clock is held)");
            Assert.AreEqual(cell, _boot.Avatar.Cell, "walker cell restored");
            Assert.AreEqual(4, _boot.Avatar.Heading, "walker heading restored");
            Assert.IsFalse(_boot.Avatar.IsOffLattice, "walker on the lattice");
            Assert.IsTrue(Hud.Visible, "HUD state restored");
            Hud.Visible = false;
            Assert.AreEqual(step, PrinceDirector.Instance.FirstSceneStep, "first scene step restored");
            Assert.AreEqual(hour, DayNight.Instance.Hour, 0.02, "the time of day follows the loaded tick");
            Assert.Greater(DayNight.Instance.Night, 0.9f, "still night");
            // The prince picks the scene up again: he asks once more.
            _boot.Clock.Resume(TestPause);
            int said = PrinceDirector.Instance.Said.Count;
            until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until && PrinceDirector.Instance.Said.Count == said) yield return null;
            Assert.Greater(PrinceDirector.Instance.Said.Count, said, "the prince speaks again after the load");
        }

        [UnityTest]
        public IEnumerator Save_WithACarrierMidPath_ItsLoadArrivesExactlyOnceAfterTheLoad()
        {
            yield return Load();
            World.Sim.Economy.Carrier carrier = null;
            for (int i = 0; i < 1500 && carrier == null; i++)
            {
                _boot.Clock.StepOnce();
                foreach (var c in S.Economy.Carriers)
                    if (c.Faction == PlayerIds.Human && c.HasLoad && c.JobId >= 0 && c.State == World.Sim.Economy.CarrierState.Carrying && c.Progress >= 10 && c.LegTicks - c.Progress >= 20) { carrier = c; break; }
            }
            Assert.IsNotNull(carrier, "a carrier of your people mid-path with a load");
            int job = carrier.JobId, id = carrier.Id, progress = carrier.Progress;
            yield return null;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            var old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            var after = S.Economy.Carriers[id];
            Assert.IsTrue(after.HasLoad && after.JobId == job && after.Progress == progress, "the carrier is mid-path with its load after the load");
            yield return null; yield return null;
            Assert.IsNotNull(LandViews.Instance.Carrier(id), "its view was rebuilt from the loaded state");
            Assert.IsTrue(LandViews.Instance.Carrier(id).Figure.HasLoad, "the view shows the load");
            int delivered = 0;
            void Count(System.Collections.Generic.IReadOnlyList<SimEvent> evs) { foreach (var e in evs) if (e is World.Sim.Economy.WareDelivered d && d.JobId == job) delivered++; }
            _boot.Clock.Ticked += Count;
            for (int i = 0; i < 600; i++) _boot.Clock.StepOnce();
            _boot.Clock.Ticked -= Count;
            Assert.AreEqual(1, delivered, "the load carried at the save is delivered exactly once");
        }

        [UnityTest]
        public IEnumerator Load_CorruptOldOrMissingSave_IsRefused_AndTheGameGoesOn()
        {
            yield return Load();
            var boot = _boot;

            Assert.IsFalse(boot.Saves.Load(), "no save yet");
            StringAssert.Contains("no save", SaveSystem.LastMessage);

            File.WriteAllText(_path, "{ this is not a save");
            Assert.IsFalse(boot.Saves.Load(), "corrupt file");
            Assert.IsTrue(SaveSystem.LastWasError);
            StringAssert.StartsWith("Not loaded:", SaveSystem.LastMessage);

            Assert.IsTrue(boot.Saves.Save(), SaveSystem.LastMessage);
            string json = File.ReadAllText(_path);
            File.WriteAllText(_path, json.Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":3"));
            Assert.IsFalse(boot.Saves.Load(), "older save (version 3, the MVP)");
            StringAssert.Contains("older version (3)", SaveSystem.LastMessage);

            File.WriteAllText(_path, "");
            Assert.IsFalse(boot.Saves.Load(), "empty file");

            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreSame(boot, WorldBootstrap.Instance, "no reload after a refused load");
            long t = S.Tick;
            boot.Clock.StepOnce();
            Assert.AreEqual(t + 1, S.Tick, "the game goes on");
        }

        [UnityTest]
        public IEnumerator Save_DuringEncounter_IsRefusedWithASentence()
        {
            yield return Load();
            Assert.IsTrue(EncounterHost.Instance.Start(new DebugEncounter()));
            yield return null;
            Assert.IsFalse(_boot.Saves.Save());
            StringAssert.Contains("can't save during", SaveSystem.LastMessage);
            Assert.IsFalse(File.Exists(_path), "nothing written");
            Assert.IsFalse(_boot.Saves.Load());
            EncounterHost.Instance.Abort();
            yield return null;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
        }
    }
}
