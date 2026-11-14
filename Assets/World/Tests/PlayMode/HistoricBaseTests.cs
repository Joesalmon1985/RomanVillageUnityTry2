using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Eras;
using World.Sim.Save;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game.Tests
{
    /// <summary>
    /// The historic base (14 Nov 2026, branch historic/main): the sim's <see cref="Lattice"/> and the game's
    /// <see cref="WalkLattice"/> are the same cells (PRD_CRYSTALS 8.1 note, I0); a version-4 save written by the
    /// refactor's Windows build loads as a Prehistoric skirmish and plays on; the encounter request reaches the
    /// encounter and "ward_campaign" is registered; the developer start dev.historic boots.
    /// </summary>
    public class HistoricBaseTests
    {
        private const string TestPause = "Test";
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;
        private string _path;

        private static string FixturePath => Path.Combine(Application.dataPath, "World/Tests/PlayMode/Fixtures/save_v4_refactor.json");

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "worldmvp_test_save_v4.json");
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

        // ---------------- the lattice ----------------

        [Test]
        public void SimLattice_IsTheWalkLattice()
        {
            Assert.AreEqual((double)WalkLattice.Step, Lattice.Step, 1e-9);
            for (int i = -60; i <= 60; i++)
                for (int j = -60; j <= 60; j++)
                {
                    var c = new Cell(i, j);
                    var w = WalkLattice.ToWorld(c);
                    Assert.AreEqual(w.x, Lattice.X(i, j), 1e-3, "X of " + c);
                    Assert.AreEqual(w.z, Lattice.Z(i, j), 1e-3, "Z of " + c);
                    Lattice.CellOf(w.x, w.z, out int si, out int sj);
                    Assert.AreEqual(c, new Cell(si, sj), "CellOf the centre of " + c);
                }
            var rng = new System.Random(20261114);
            for (int n = 0; n < 20000; n++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 1200 - 600), 0, (float)(rng.NextDouble() * 1200 - 600));
                var gc = WalkLattice.ToCell(p);
                Lattice.CellOf(p.x, p.z, out int si, out int sj);
                Assert.AreEqual(gc, new Cell(si, sj), "the cell of " + p);
                var other = WalkLattice.ToCell(p + new Vector3((float)(rng.NextDouble() * 200 - 100), 0, (float)(rng.NextDouble() * 200 - 100)));
                Assert.AreEqual(WalkGrid.Distance(gc, other), Lattice.Distance(gc.I, gc.J, other.I, other.J), "distance " + gc + " to " + other);
            }
            for (int h = 0; h < 6; h++)
            {
                var d = WalkLattice.Dir(h);
                Assert.AreEqual(1, Lattice.Distance(0, 0, d.I, d.J), "heading " + h + " is one step");
            }
        }

        // ---------------- scene helpers ----------------

        private IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot);
            _boot.Clock.Pause(TestPause);
        }

        private IEnumerator WaitForReload(WorldBootstrap old, float seconds = 60f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old)) yield return null;
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.AreNotSame(old, _boot, "the scene reloaded");
        }

        // ---------------- save version 4 ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator V4Save_FromTheRefactorBuild_LoadsAsPrehistoricSkirmish_AndPlaysOn()
        {
            Assert.IsTrue(File.Exists(FixturePath), "fixture " + FixturePath);
            StringAssert.StartsWith("{\"Version\":4,", File.ReadAllText(FixturePath));
            File.Copy(FixturePath, _path, true);
            yield return LoadScene();
            var old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            Assert.IsFalse(SaveSystem.LastWasError, SaveSystem.LastMessage);
            Assert.AreEqual(194, S.Tick, "the saved tick");
            Assert.AreEqual(Era.Prehistoric, AgesApi.Current(S));
            Assert.AreEqual(World.Sim.Eras.PlayMode.Skirmish, AgesApi.Mode(S));
            Assert.IsNull(S.Campaign, "no campaign");
            Assert.AreEqual(new Cell(14, -18), _boot.Avatar.Cell, "the walker's cell");
            Assert.IsNotNull(PrinceDirector.Instance, "the prince is there");
            Assert.AreEqual(1, PrinceDirector.Instance.FirstSceneStep, "the first scene's step");
            // Plays on: a minute of game time, the views follow.
            for (int i = 0; i < 600; i++) _boot.Clock.StepOnce();
            yield return null;
            yield return null;
            Assert.AreEqual(794, S.Tick);
            Assert.IsNotNull(LandViews.Instance);
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            StringAssert.Contains("\"Version\":" + SaveCodec.CurrentVersion, File.ReadAllText(_path), "saved again as the current version");
        }

        // ---------------- encounter requests ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator EncounterRequest_ReachesTheEncounter_AndWardCampaignIsRegistered()
        {
            yield return LoadScene();
            Assert.IsTrue(EncounterDirectory.IsRegistered(EncounterDirectory.WardCampaign), "ward_campaign registered");
            Assert.IsTrue(EncounterDirectory.IsRegistered(EncounterDirectory.WardMira));
            DebugEncounter enc = null;
            const string id = "test_request";
            EncounterDirectory.Register(id, () => enc = new DebugEncounter());
            try
            {
                var req = new WardDuelRequest
                {
                    Mode = World.Sim.Campaign.EncounterKind.Lethal, AttemptId = 3, EncounterId = "keeper.earth", ProfileId = "keeper_earth", Seed = 7,
                    Stage = new DuelStageSpec { CasterActorId = NpcIds.Mira, OpponentActorId = "keeper", VolunteerActorId = "companion", VolunteerPos = new Vector3(1, 0, 2) },
                };
                Assert.IsTrue(EncounterDirectory.TryStart(id, null, req));
                Assert.AreSame(req, EncounterHost.Instance.Context.Request);
                Assert.IsTrue(req.IsCampaign && req.Stage.HasVolunteer);
                for (int i = 0; i < 3; i++) enc.Press();
                yield return null;
                yield return null;
                Assert.IsFalse(EncounterHost.Instance.IsRunning);
                // Without a request: null, as before.
                Assert.IsTrue(EncounterDirectory.TryStart(id, null));
                Assert.IsNull(EncounterHost.Instance.Context.Request);
                EncounterHost.Instance.Abort();
                Assert.IsFalse(CampaignDuel.Start(null), "I0 shell: CampaignDuel starts nothing");
                Assert.IsNull(Interiors.Current, "I0 shell: always on the surface");
                Assert.IsNull(ExpeditionDirector.Instance, "I0 shell: no expedition director");
            }
            finally { EncounterDirectory.Unregister(id); }
        }

        // ---------------- developer start ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator DevHistoric_Boots_AndRuns()
        {
            yield return LoadScene();
            var old = _boot;
            Assert.IsTrue(DevStart.Begin(ScenarioStarts.DevHistoric), DevStart.LastError);
            yield return WaitForReload(old, 240f);
            Assert.AreEqual(ScenarioStarts.DevHistoric, DevStart.Current);
            Assert.AreEqual(Era.Historic, AgesApi.Current(S));
            Assert.AreEqual(World.Sim.Eras.PlayMode.DevStart, AgesApi.Mode(S));
            Assert.Greater(S.Tick, 0);
            Assert.IsNotNull(LandViews.Instance);
            Assert.IsNotNull(PrinceDirector.Instance);
            Assert.AreEqual(PrinceDirector.SceneDone, PrinceDirector.Instance.FirstSceneStep, "no first scene in a developer start");
            _boot.Clock.Resume(TestPause);
            long t0 = S.Tick;
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.Greater(S.Tick, t0, "the clock runs");
            // The map and the views with Historic buildings, towns and grades.
            MapScreen.Open();
            yield return null;
            yield return null;
            MapScreen.Close();
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
        }
    }
}
