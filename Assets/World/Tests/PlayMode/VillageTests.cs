using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace World.Game.Tests
{
    /// <summary>Gate G1 (walkable village) PlayMode checks.</summary>
    public class VillageTests
    {
        private WorldBootstrap _boot;

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot, "WorldBootstrap.Instance");
        }

        [UnityTest]
        public IEnumerator Scene_Boots_WithVillage()
        {
            yield return Load();
            Assert.AreEqual(1, Object.FindObjectsByType<WorldBootstrap>(FindObjectsSortMode.None).Length, "one sim/bootstrap");
            Assert.AreEqual(1, Object.FindObjectsByType<FirstPersonAvatar>(FindObjectsSortMode.None).Length, "one avatar");
            Assert.IsNotNull(_boot.Sim);
            foreach (var id in new[] { NpcIds.Mira, NpcIds.Host, NpcIds.Quartermaster, NpcIds.MarketKeeper, NpcIds.Captain, NpcIds.Scribe, NpcIds.Doorman })
                Assert.IsNotNull(Npc.Find(id), "NPC " + id);
            Assert.AreEqual(7, Npc.All.Count, "six named roles plus the doorman");
            Assert.IsNotNull(VillageBuilder.Storehouse, "storehouse");
            Assert.IsNotNull(VillageBuilder.Market, "market");
            Assert.IsNotNull(VillageBuilder.Bell, "bell");
            Assert.IsNotNull(_boot.Warehouse, "warehouse");
            Assert.AreEqual(ViewMode.FirstPerson, _boot.Views.CurrentView);

            var L = _boot.Layout;
            var spawn = _boot.Avatar.Position;
            Assert.Less(Flat(Npc.Find(NpcIds.Mira).transform.position - spawn), 25f, "Mira within 25 m of spawn");
            Assert.Less(Flat(Npc.Find(NpcIds.Doorman).transform.position - spawn), 25f, "door host within 25 m of spawn");
            Assert.Less(Flat(_boot.Warehouse.DoorPosition - L.Home), 40f, "warehouse door under 40 m from the square");
            // Both hosts and the door are in front of the spawn camera.
            var cam = _boot.MainCamera;
            foreach (var p in new[] { Npc.Find(NpcIds.Mira).transform.position, Npc.Find(NpcIds.Doorman).transform.position, _boot.Warehouse.DoorPosition })
            {
                var vp = cam.WorldToViewportPoint(p + Vector3.up * 1f);
                Assert.IsTrue(vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1, "visible from spawn: " + p);
            }
        }

        [UnityTest]
        public IEnumerator Walk_FromSpawn_ReachesMiraAndDoorHost_GreetingShows()
        {
            yield return Load();
            foreach (var id in new[] { NpcIds.Mira, NpcIds.Doorman })
            {
                var L = _boot.Layout;
                _boot.Avatar.Teleport(L.Spawn + Vector3.up * 0.05f, L.SpawnYaw);
                yield return null;
                var npc = Npc.Find(id);
                Assert.IsFalse(npc.IsGreetingShown, id + " greeting hidden at spawn");
                var script = new AutoPlayScript("walk_" + id).WalkTo(() => npc.transform.position, 3.0f, false, 15f);
                float t0 = Time.time;
                yield return _boot.AutoPlay.Run(script);
                float took = Time.time - t0;
                Assert.IsFalse(_boot.AutoPlay.Failed, _boot.AutoPlay.FailMessage);
                Assert.Less(took, 15f, "reached " + id + " in " + took + " s");
                yield return null; yield return null;
                Assert.IsTrue(npc.IsGreetingShown, id + " greeting shows within 4 m");
                Debug.Log("[VillageTests] walked to " + id + " in " + took.ToString("0.00") + " s");
            }
        }

        [UnityTest]
        public IEnumerator Tab_WhileWalkingAndInspecting_KeepsAvatarAndSelection()
        {
            yield return Load();
            var av = _boot.Avatar;
            var sim = _boot.Sim;
            _boot.Picker.Select(VillageBuilder.Storehouse);
            av.SetLook(av.Yaw + 17.3f, -8.4f);
            for (int i = 0; i < 30; i++) { av.SetScriptedMove(av.transform.forward, false); yield return null; }
            Assert.Greater(av.Velocity.magnitude, 1f, "avatar was walking");

            for (int round = 0; round < 2; round++)
            {
                Vector3 p0 = av.Position; float y0 = av.Yaw, pi0 = av.Pitch;
                _boot.Views.Toggle();
                Assert.AreEqual(ViewMode.God, _boot.Views.CurrentView);
                for (int i = 0; i < 20; i++) { av.SetScriptedMove(av.transform.forward, true); yield return null; }
                Assert.Less((av.Position - p0).magnitude, 0.001f, "no movement in God View");
                Assert.IsTrue(av.Marker.activeSelf, "marker shown in God View");
                _boot.Views.Toggle();
                Assert.AreEqual(ViewMode.FirstPerson, _boot.Views.CurrentView);
                Assert.Less((av.Position - p0).magnitude, 0.001f, "position restored to 1 mm");
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(av.Yaw, y0)), 0.1f, "yaw restored");
                Assert.Less(Mathf.Abs(av.Pitch - pi0), 0.1f, "pitch restored");
                Assert.AreSame(sim, _boot.Sim, "same sim instance");
                Assert.AreSame(VillageBuilder.Storehouse, _boot.Picker.Selected, "selection retained");
                Assert.IsFalse(av.Marker.activeSelf);
                for (int i = 0; i < 10; i++) { av.SetScriptedMove(av.transform.forward, false); yield return null; }
            }
        }

        [UnityTest]
        public IEnumerator EncounterHost_DebugEncounter_PausesSurvivesTabAndRestores()
        {
            yield return Load();
            var av = _boot.Avatar;
            av.SetLook(33.3f, 12.5f);
            yield return null;
            Vector3 p0 = av.Position; float y0 = av.Yaw, pi0 = av.Pitch;
            var enc = new DebugEncounter();
            var ctx = EncounterContext.Create(Npc.Find(NpcIds.Mira));
            Assert.IsTrue(_boot.Encounters.Start(enc, ctx));
            Assert.IsTrue(enc.Began);
            Assert.IsTrue(_boot.Clock.IsPausedFor(PauseReasons.Encounter));
            Assert.IsFalse(av.CanMove, "avatar movement locked");
            Assert.AreSame(Npc.Find(NpcIds.Mira).Actor, ctx.NpcActor);
            Assert.IsNotNull(ctx.Warehouse);
            ctx.MoveAvatarToStage(5f);
            long tick = _boot.Sim.State.Tick;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(tick, _boot.Sim.State.Tick, "clock paused during encounter");

            _boot.Views.Toggle();
            yield return null;
            Assert.IsTrue(_boot.Encounters.IsRunning, "survives Tab to God View");
            enc.Press();
            _boot.Views.Toggle();
            yield return null;
            Assert.IsTrue(_boot.Encounters.IsRunning, "survives Tab back");
            Assert.IsFalse(av.CanMove, "still locked after Tab back");
            Assert.Greater(enc.Ticks, 0, "encounter ticked");
            enc.Press(); enc.Press();
            yield return null; yield return null;
            Assert.IsFalse(_boot.Encounters.IsRunning, "finished after three presses");
            Assert.IsTrue(enc.Ended, "End called");
            Assert.Less((av.Position - p0).magnitude, 0.001f, "position restored");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(av.Yaw, y0)), 0.1f, "yaw restored");
            Assert.Less(Mathf.Abs(av.Pitch - pi0), 0.1f, "pitch restored");
            Assert.IsTrue(av.CanMove, "control restored");
            Assert.IsFalse(_boot.Clock.IsPaused, "clock resumed");
            long t1 = _boot.Sim.State.Tick;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Greater(_boot.Sim.State.Tick, t1, "clock ticks again");

            // Abort path.
            var enc2 = new DebugEncounter();
            Assert.IsTrue(_boot.Encounters.Start(enc2));
            _boot.Encounters.Abort();
            Assert.IsTrue(enc2.Ended);
            Assert.IsFalse(_boot.Clock.IsPaused);
        }

        [UnityTest]
        public IEnumerator EncounterDirectory_And_DialogueOptions()
        {
            yield return Load();
            var mira = Npc.Find(NpcIds.Mira);
            bool hadWard = EncounterDirectory.IsRegistered(EncounterDirectory.WardMira);
            mira.Interact();
            Assert.IsTrue(DialoguePanel.IsOpen);
            Assert.IsFalse(_boot.Avatar.CanMove, "dialogue holds the avatar");
            DialoguePanel.Close();
            Assert.IsTrue(_boot.Avatar.CanMove);

            const string id = "test_debug_encounter";
            DebugEncounter made = null;
            EncounterDirectory.Register(id, () => made = new DebugEncounter());
            try
            {
                DialoguePanel.Open("Test", new[] { "line" }, new[] { DialogueOption.Encounter("Go", id), DialogueOption.Encounter("Missing", "no_such_encounter") }, mira);
                Assert.IsFalse(DialoguePanel.Choose(1), "unregistered option is disabled");
                Assert.IsTrue(DialoguePanel.Choose(0));
                Assert.IsFalse(DialoguePanel.IsOpen);
                Assert.IsNotNull(made);
                Assert.IsTrue(_boot.Encounters.IsRunning);
                Assert.AreSame(mira, _boot.Encounters.Context.Npc);
                _boot.Encounters.Abort();
            }
            finally { EncounterDirectory.Unregister(id); }
            Assert.AreEqual(hadWard, EncounterDirectory.IsRegistered(EncounterDirectory.WardMira));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Warehouse_SetRouteOpen_TogglesBarrierAndLights()
        {
            yield return Load();
            var w = _boot.Warehouse;
            Assert.IsTrue(w.IsDoorMusicOn, "door music plays");
            Assert.AreEqual(1f, w.DoorMusic.spatialBlend, "3D music");
            Assert.IsNotNull(w.PartyLightsRoot);
            foreach (Route r in new[] { Route.Bar, Route.Balcony })
            {
                Assert.IsTrue(w.Barrier(r).activeSelf, r + " barrier at start");
                Assert.Greater(w.RouteLights(r).Count, 0);
                foreach (var l in w.RouteLights(r)) Assert.IsFalse(l.enabled, r + " light off at start");
                w.SetRouteOpen(r, true);
                Assert.IsFalse(w.Barrier(r).activeSelf, r + " barrier removed");
                foreach (var l in w.RouteLights(r)) Assert.IsTrue(l.enabled, r + " light on");
                w.SetRouteOpen(r, false);
                Assert.IsTrue(w.Barrier(r).activeSelf);
            }
            w.SetDoorMusic(false);
            Assert.IsFalse(w.IsDoorMusicOn);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Warehouse_Routes_BlockedWhenClosed_WalkableWhenOpen()
        {
            yield return Load();
            var w = _boot.Warehouse;
            var av = _boot.Avatar;
            Vector3 L(float x, float z) => w.transform.TransformPoint(new Vector3(x, 0, z));
            float yaw = w.transform.eulerAngles.y + 180f;
            _boot.AutoPlay.LogFailuresAsErrors = false; // closed routes are expected to time out

            // Bar: closed blocks, open lets the avatar in.
            foreach (bool open in new[] { false, true })
            {
                w.SetRouteOpen(Route.Bar, open);
                av.Teleport(L(-8f, -15f) + Vector3.up * 0.05f, yaw);
                yield return null;
                var s = new AutoPlayScript("bar").WalkTo(w.RouteInsidePosition(Route.Bar), 0.8f, false, open ? 8f : 3f);
                yield return _boot.AutoPlay.Run(s);
                Assert.AreEqual(!open, _boot.AutoPlay.Failed, "bar " + (open ? "open" : "closed") + ": " + _boot.AutoPlay.FailMessage);
            }

            // Balcony: walk up the ramp to the deck.
            foreach (bool open in new[] { false, true })
            {
                w.SetRouteOpen(Route.Balcony, open);
                av.Teleport(L(10f, -0.8f) + Vector3.up * 0.05f, yaw);
                yield return null;
                var s = new AutoPlayScript("balcony").WalkTo(L(10f, -9.5f), 0.6f, false, open ? 8f : 3f);
                if (open) s.WalkTo(w.RouteInsidePosition(Route.Balcony), 0.8f, false, 8f);
                yield return _boot.AutoPlay.Run(s);
                Assert.AreEqual(!open, _boot.AutoPlay.Failed, "balcony " + (open ? "open" : "closed") + ": " + _boot.AutoPlay.FailMessage);
                if (open) Assert.Greater(av.Position.y, 3.3f, "on the balcony deck");
            }

            // Decks: from the door to the front of the decks.
            av.Teleport(w.OutsideDoorPosition + Vector3.up * 0.05f, yaw);
            yield return null;
            yield return _boot.AutoPlay.Run(new AutoPlayScript("decks").WalkTo(w.DecksFrontPosition, 0.3f, false, 10f));
            Assert.IsFalse(_boot.AutoPlay.Failed, _boot.AutoPlay.FailMessage);
            Assert.IsTrue(Npc.Find(NpcIds.Host).IsGreetingShown, "DJ greets at the decks");
        }

        [UnityTest]
        public IEnumerator BoardSpace_And_BoardPicker_RoundTrip()
        {
            yield return Load();
            var topo = _boot.Sim.State.Board.Topology;
            int home = _boot.Sim.State.Board.HomeCityVertex;
            Assert.AreEqual(home, BoardSpace.NearestVertex(BoardSpace.Vertex(home) + new Vector3(1f, 0, -1f), 5f));
            for (int e = 0; e < topo.EdgeCount; e += 7) Assert.AreEqual(e, BoardSpace.NearestEdge(BoardSpace.EdgeMid(e), 1f));
            for (int h = 0; h < topo.HexCount; h++) Assert.AreEqual(h, BoardSpace.HexAt(BoardSpace.Hex(h)));
            _boot.Views.SetView(ViewMode.God);
            yield return null;
            var cam = _boot.GodCam.Camera;
            var sp = cam.WorldToScreenPoint(BoardSpace.Vertex(home));
            Assert.AreEqual(home, BoardPicker.VertexAt(sp));
            Assert.AreEqual(topo.HexAt(BoardSpace.Hex(4).x, BoardSpace.Hex(4).z), BoardPicker.HexAt(cam.WorldToScreenPoint(BoardSpace.Hex(4))));
            int edge = topo.VertexEdges(home)[0];
            Assert.AreEqual(edge, BoardPicker.EdgeAt(cam.WorldToScreenPoint(BoardSpace.EdgeMid(edge))));
            _boot.Views.SetView(ViewMode.FirstPerson);
        }

        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
    }
}
