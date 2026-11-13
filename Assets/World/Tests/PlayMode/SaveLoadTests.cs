using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Encounters.Ward;
using World.Sim.Save;

namespace World.Game.Tests
{
    /// <summary>
    /// The save slot in the scene (G5): a save with a carrier mid-road loads into an equal state, the shipment arrives
    /// exactly once (and the game then matches a headless run of the same save), the player's pose, view and Ward progress
    /// come back; corrupt, older and missing saves are refused with a sentence and the game goes on; saving is refused
    /// during an encounter. The slot is a temporary file (never the player's save).
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

        private static Carrier InFlight(WorldState s) => s.Economy.Carriers.FirstOrDefault(c => c.HasLoad && c.CurrentEdge >= 0 && c.JobId >= 0);

        [UnityTest]
        public IEnumerator Save_CarrierMidRoad_LoadsEqual_DeliversOnce_RestoresPlayer()
        {
            yield return Load();
            int n = 0;
            while (InFlight(S) == null && n++ < 3000) _boot.Clock.StepOnce();
            var carrier = InFlight(S);
            Assert.IsNotNull(carrier, "a carrier walks a road with a load");
            int job = carrier.JobId;
            WardProgressApi.RecordDuel(S, WardOutcome.Victory); // Ward progress lives in WorldState and must come back
            int ward = S.Ward.CompletedDuelsVsMira;
            _boot.Warehouse.SetRouteOpen(Route.Balcony, true);

            // Stand somewhere else, look somewhere else, save from God View.
            var pos = _boot.Layout.Spawn + new Vector3(7.3f, 0.05f, -3.1f);
            _boot.Avatar.Teleport(pos, 141.5f, -12f);
            _boot.Views.SetView(ViewMode.God);
            _boot.GodCam.SetFocus(pos + new Vector3(30, 0, 10), 80f);
            yield return null;
            pos = _boot.Avatar.Position;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            StringAssert.Contains("saved", SaveSystem.LastMessage);
            string saved = SaveCodec.StateJson(S);
            long tick = S.Tick;

            // Reference: the same save run headless.
            var reference = SaveCodec.Load(File.ReadAllText(_path), out var model);
            Assert.AreEqual(saved, SaveCodec.StateJson(reference.State), "the file holds the state");
            Assert.IsNotNull(model.Player);

            // Change everything, then load.
            for (int i = 0; i < 50; i++) _boot.Clock.StepOnce();
            _boot.Avatar.Teleport(_boot.Layout.Spawn, 0f);
            _boot.Views.SetView(ViewMode.FirstPerson);
            var old = _boot;
            Assert.IsTrue(old.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);

            Assert.IsTrue(_boot.Clock.IsPausedFor(TestPause));
            Assert.AreEqual(tick, S.Tick, "loaded tick");
            Assert.AreEqual(saved, SaveCodec.StateJson(S), "loaded state equals the saved one");
            StringAssert.Contains("loaded", SaveSystem.LastMessage);
            Assert.Less((_boot.Avatar.Position - pos).magnitude, 0.001f, "position restored");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(_boot.Avatar.Yaw, 141.5f)), 0.1f, "yaw restored");
            Assert.Less(Mathf.Abs(_boot.Avatar.Pitch + 12f), 0.1f, "pitch restored");
            Assert.AreEqual(ViewMode.God, _boot.Views.CurrentView, "view restored");
            Assert.Less((_boot.GodCam.Focus - new Vector3(pos.x + 30, 0, pos.z + 10)).magnitude, 0.01f, "God View framing restored");
            Assert.AreEqual(ward, S.Ward.CompletedDuelsVsMira, "Ward progress restored");
            Assert.IsTrue(_boot.Warehouse.IsRouteOpen(Route.Balcony) && !_boot.Warehouse.IsRouteOpen(Route.Bar), "opened route restored");
            Assert.AreEqual(S.Economy.Carriers.Count, EconomyViews.Instance.CarrierViewCount, "carrier views rebuilt from the loaded state");

            // The shipment in flight arrives exactly once, and the game matches the headless run tick for tick.
            int delivered = 0;
            System.Action<IReadOnlyList<SimEvent>> count = evs => { foreach (var e in evs) if (e is WareDelivered d && d.JobId == job) delivered++; };
            _boot.Clock.Ticked += count;
            int refDelivered = 0;
            for (int i = 0; i < 400; i++)
            {
                _boot.Clock.StepOnce();
                reference.Step();
                foreach (var e in reference.DrainEvents()) if (e is WareDelivered d && d.JobId == job) refDelivered++;
                if (i % 50 == 0) yield return null;
            }
            _boot.Clock.Ticked -= count;
            Assert.AreEqual(1, delivered, "the in-flight shipment arrives exactly once after the load");
            Assert.AreEqual(1, refDelivered, "and once in the headless run");
            Assert.AreEqual(SaveCodec.StateJson(reference.State), SaveCodec.StateJson(S), "the loaded game runs on exactly like the headless load");
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
            File.WriteAllText(_path, json.Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":2"));
            Assert.IsFalse(boot.Saves.Load(), "older save");
            StringAssert.Contains("older", SaveSystem.LastMessage);

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

        [UnityTest]
        public IEnumerator PauseMenu_DuringTheRhythmPhrase_DoesNotPause_SecondEscLeaves()
        {
            yield return Load();
            Assert.IsTrue(EncounterDirectory.TryStart(EncounterDirectory.RhythmParty, Npc.Find(NpcIds.Host)));
            yield return null;
            Assert.IsTrue(Hud.InRhythmPhrase(), "the phrase is playing");
            Hud.Instance.OpenMenu();
            Assert.IsFalse(Hud.MenuOpen, "no pause menu during the phrase");
            StringAssert.Contains("Esc again", Hud.NoticeText);
            Hud.Instance.OpenMenu(); // the second Esc
            yield return null;
            Assert.IsFalse(EncounterHost.Instance.IsRunning, "the second Esc leaves the party");
            Assert.IsFalse(Hud.MenuOpen);
            Hud.Instance.OpenMenu();
            Assert.IsTrue(Hud.MenuOpen, "outside the phrase the menu opens");
            Hud.Instance.CloseMenu();
        }

        [UnityTest]
        public IEnumerator GoHere_PutsTheAvatarOnOpenGround_RefusesBuildingsAndTheSea()
        {
            yield return Load();
            _boot.Views.SetView(ViewMode.God);
            var far = _boot.Outpost.transform.position + new Vector3(0, 0, -40f);
            // Find open ground near the rival outpost (about 300 m from the village).
            Vector3 spot = default; bool found = false;
            for (int i = 0; i < 40 && !found; i++)
            {
                var p = far + new Vector3((i % 7) * 6f - 18f, 0, (i / 7) * 6f);
                if (TravelTool.TryFindSpot(p, out spot, out _)) found = true;
            }
            Assert.IsTrue(found, "open ground near the outpost");
            Assert.IsTrue(TravelTool.TravelTo(spot));
            Assert.AreEqual(ViewMode.FirstPerson, _boot.Views.CurrentView, "back in first person");
            Assert.Less(new Vector2(_boot.Avatar.Position.x - spot.x, _boot.Avatar.Position.z - spot.z).magnitude, 0.01f);
            Assert.IsFalse(TravelTool.TryFindSpot(new Vector3(BoardSpace.BoardRadius + 30f, 0, 0), out _, out var why), "off the board");
            Assert.IsNotNull(why);
            Assert.IsFalse(TravelTool.TryFindSpot(VillageBuilder.Storehouse.transform.position, out _, out why), "on the storehouse");
            Assert.IsNotNull(why);
        }
    }
}
