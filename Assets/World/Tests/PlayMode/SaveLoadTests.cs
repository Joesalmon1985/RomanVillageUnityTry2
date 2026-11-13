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
