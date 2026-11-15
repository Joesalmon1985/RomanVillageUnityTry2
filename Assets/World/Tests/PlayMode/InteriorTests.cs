using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Eras;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game.Tests
{
    /// <summary>
    /// Package I5 (PRD_CRYSTALS 5.10, 8.1): interiors built from any pack's definition (the authored Earth pack and the
    /// skeleton), walls that block, stairs and exits that link both ways to the same cell and heading, doors that obey
    /// their facts, the interior map that shows only what was seen, "Lead me out" to the surface, a save made
    /// underground that loads in place, no sky light underground, frame rate and lights in budget, nothing built with
    /// no campaign, and no WorldState writes in the package's files.
    /// </summary>
    public class InteriorTests
    {
        private const string TestPause = "InteriorTest";
        private WorldBootstrap _boot;
        private PlayerWalker W => _boot.Avatar;
        private WorldState S => _boot.Sim.State;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "worldmvp_test_interiors.json");
            if (File.Exists(_path)) File.Delete(_path);
            SaveSystem.PathOverride = _path;
            SaveSystem.TestPauseOnLoad = TestPause;
        }

        [TearDown]
        public void TearDown()
        {
            Interiors.FactsOverride = null;
            SaveSystem.PathOverride = null;
            SaveSystem.TestPauseOnLoad = null;
            if (MapScreen.IsOpen) MapScreen.Close();
            if (File.Exists(_path)) File.Delete(_path);
        }

        private IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot);
            _boot.Clock.Pause(TestPause);
        }

        /// <summary>A new game made a developer start (tests only) with a shipped pack's campaign running.</summary>
        private IEnumerator LoadWithCampaign(string pack)
        {
            yield return Load();
            S.Eras.Mode = World.Sim.Eras.PlayMode.DevStart; // test set-up: allows StartCampaign on a new game
            Assert.IsNull(InteriorScripts.StartCampaign(pack));
            yield return null;
            Assert.Greater(Interiors.All.Count, 0, "interiors built from the running campaign");
        }

        /// <summary>A new game with a pack's interiors built by hand (no campaign) and the given facts known.</summary>
        private IEnumerator LoadBuilt(string pack, Func<string, bool> facts)
        {
            yield return Load();
            Interiors.FactsOverride = facts;
            Interiors.Build(InteriorScripts.LoadPack(pack));
            yield return null;
        }

        private IEnumerator Idle(float timeout = 15f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while ((W.IsGliding || W.IsAutoWalking || Interiors.IsCrossing) && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            Assert.IsFalse(Interiors.IsCrossing, "still crossing");
            Assert.IsFalse(W.IsGliding, "still gliding");
        }

        private IEnumerator Do(Action move) { yield return Idle(); move(); yield return null; yield return Idle(); }

        private static Cell C(string marker) => InteriorScripts.CellOf(marker);
        private static InteriorLink L(string marker) { MarkerRef.TryParse(marker, out var r); return Interiors.LinkAt(r); }

        // ---------------- building from the definition ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator RegionBuild_EqualsTheDefinition_AuthoredAndSkeleton()
        {
            foreach (var pack in new[] { InteriorScripts.AuthoredPack, InteriorScripts.SkeletonPack })
            {
                yield return LoadBuilt(pack, f => false);
                var def = InteriorScripts.LoadPack(pack);
                Assert.AreEqual(def.Interiors.Count, Interiors.All.Count, pack + ": one interior per definition");
                int floor = 0;
                foreach (var idef in def.Interiors)
                {
                    var m = Interiors.Get(idef.Id);
                    Assert.IsNotNull(m, idef.Id);
                    Assert.AreEqual(InteriorSpace.Origin(def.Interiors.IndexOf(idef)), m.Origin);
                    Assert.AreEqual(idef.Location ?? "", m.Location ?? "", idef.Id + " location");
                    Assert.AreEqual(InteriorMaps.Markers(idef).Count, m.Markers.Count, idef.Id + " markers");
                    for (int j = -1; j <= idef.Rows.Count; j++)
                        for (int i = -1; i <= 16; i++)
                        {
                            var c = m.ToCell(i, j);
                            bool isFloor = InteriorMaps.IsFloor(idef, i, j);
                            var md = InteriorMaps.MarkerAt(idef, i, j);
                            bool door = isFloor && md != null && md.Kind == MarkerKind.Door && !string.IsNullOrEmpty(md.OpenWhen);
                            Assert.AreEqual(isFloor && !door, !WalkGrid.IsBlocked(c), pack + " " + idef.Id + " (" + i + "," + j + ") walkable");
                            Assert.AreEqual(isFloor, m.Floor.Contains(c), idef.Id + " floor (" + i + "," + j + ")");
                            Assert.AreEqual(isFloor, WalkGrid.RegionOwner(c) == InteriorDirector.RegionOwner, idef.Id + " region (" + i + "," + j + ")");
                            if (isFloor) { floor++; Assert.IsTrue(InteriorSpace.TryLocal(c, out var id, out int li, out int lj) && id == idef.Id && li == i && lj == j); }
                        }
                    Assert.IsNotNull(m.Root, idef.Id + " has a view");
                    Assert.Greater(m.Root.GetComponentsInChildren<MeshRenderer>(true).Length, 1, idef.Id + " has rock and markers");
                }
                Assert.AreEqual(floor, WalkGrid.RegionCellCount, pack + ": every floor cell and no other is open region");
                // Every way leads to its other end and back.
                Assert.Greater(Interiors.Links.Count, 0);
                foreach (var l in Interiors.Links)
                {
                    var other = Interiors.LinkAt(l.To);
                    Assert.IsNotNull(other, pack + ": the other end of " + l);
                    Assert.AreEqual(l.End, other.To, pack + ": " + l + " leads back");
                    Assert.IsTrue(WalkGrid.IsAvoided(l.Cell), "paths avoid " + l);
                    if (l.End.IsInterior) Assert.IsTrue(Interiors.Get(l.End.Interior).Floor.Contains(l.Cell + WalkLattice.Dir(l.OutHeading)), l + " faces open floor on arrival");
                }
                // Cells beyond the board that no interior lists stay blocked.
                Assert.AreEqual("edge", WalkGrid.WhyBlocked(InteriorSpace.Origin(0) + new Cell(-3, 5)));
                Assert.AreEqual("edge", WalkGrid.WhyBlocked(InteriorSpace.Origin(def.Interiors.Count) + new Cell(2, 2)));
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NoCampaign_NothingIsBuilt_AndTheMapIsTheIsland()
        {
            yield return Load();
            Assert.IsNull(S.Campaign);
            Assert.AreEqual(0, Interiors.All.Count);
            Assert.AreEqual(0, Interiors.Links.Count);
            Assert.AreEqual(0, WalkGrid.RegionCellCount);
            Assert.IsNull(Interiors.Current);
            Assert.IsFalse(InteriorLighting.Instance.Underground);
            Assert.IsTrue(WalkGrid.IsBlocked(InteriorSpace.Origin(0) + new Cell(5, 5)), "beyond the board is blocked");
            MapScreen.Open();
            yield return null; yield return null;
            Assert.AreEqual(MapMode.Island, MapScreen.Mode);
            Assert.AreEqual(Rect.zero, MapScreenView.Instance.SwitchRect, "no interior switch on the surface");
            MapScreen.Close();
            Assert.IsFalse(Interiors.LeadOut(), "Lead me out does nothing on the surface");
        }

        // ---------------- walking ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Walls_Block_AndPathsFollowTheFloor()
        {
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => false);
            var m = Interiors.Get("quarry_upper");
            var start = C("glade/quarry_upper/safe");
            W.Teleport(start, 0);
            yield return null;
            Assert.AreEqual("quarry_upper", Interiors.Current);
            for (int h = 0; h < 6; h++)
            {
                var n = start + WalkLattice.Dir(h);
                if (m.Floor.Contains(n)) continue;
                W.Teleport(start, h);
                yield return null;
                int bumps = W.BumpCount;
                yield return Do(W.Forward);
                Assert.AreEqual(start, W.Cell, "rock at heading " + h + " blocks");
                Assert.AreEqual(bumps + 1, W.BumpCount, "a bump at heading " + h);
            }
            // The walker's path over the floor is as long as the definition's own path.
            var p = InteriorMaps.Path(m.Def, 8, 6, 10, 2, f => false);
            var wp = WalkGrid.FindPath(start, C("glade/quarry_upper/pedestal"));
            Assert.IsNotNull(wp);
            Assert.AreEqual(p.Length / 2 - 1, wp.Count, "same number of steps as InteriorMaps.Path");
            foreach (var c in wp) Assert.IsTrue(m.Floor.Contains(c), "path stays on the floor");
            Assert.AreEqual(InteriorSpace.FloorY, W.Position.y, 0.001f, "the walker stands on the interior floor");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Stairs_LinkBothWays_ToTheSameCellAndHeading_AndShutWaysHold()
        {
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => f != "earth.ash_cut_open");
            var glade = L("glade/stair"); var exit = L("glade/quarry_upper/exit");
            Assert.IsTrue(glade.IsOpen && exit.IsOpen);
            Assert.IsFalse(glade.Up); Assert.IsTrue(exit.Up);
            // Down: step onto the glade's stair from its open side.
            W.Teleport(glade.Cell + WalkLattice.Dir(glade.OutHeading), glade.OutHeading + 3);
            yield return null;
            yield return Do(W.Forward);
            Assert.AreEqual("quarry_upper", Interiors.Current);
            Assert.AreEqual(exit.Cell, W.Cell, "on the quarry's exit");
            Assert.AreEqual(exit.OutHeading, W.Heading, "facing into the quarry");
            int crossings = InteriorDirector.Instance.CrossingCount;
            // Up and down again: off the stair and back onto it.
            yield return Do(W.Forward); yield return Do(W.Back);
            Assert.IsNull(Interiors.Current, "back on the surface");
            Assert.AreEqual(glade.Cell, W.Cell); Assert.AreEqual(glade.OutHeading, W.Heading);
            yield return Do(W.Forward); yield return Do(W.Back);
            Assert.AreEqual("quarry_upper", Interiors.Current);
            Assert.AreEqual(exit.Cell, W.Cell, "the same cell"); Assert.AreEqual(exit.OutHeading, W.Heading, "and heading");
            Assert.AreEqual(crossings + 2, InteriorDirector.Instance.CrossingCount);
            // The stair between the two levels, both ways.
            var down = L("glade/quarry_upper/stair_down"); var up = L("ash_cut/tally_vault/stair_up");
            Assert.IsFalse(down.Up); Assert.IsTrue(up.Up);
            Assert.IsTrue(W.WalkTo(down.Cell));
            yield return Idle(30f);
            Assert.AreEqual("tally_vault", Interiors.Current);
            Assert.AreEqual(up.Cell, W.Cell); Assert.AreEqual(up.OutHeading, W.Heading);
            yield return Do(W.Forward); yield return Do(W.Back);
            Assert.AreEqual("quarry_upper", Interiors.Current);
            Assert.AreEqual(down.Cell, W.Cell); Assert.AreEqual(down.OutHeading, W.Heading);
            // A shut way: the Ash cut's exit is behind the barred door, and its surface end does not open.
            var cut = L("ash_cut/cut");
            Assert.IsFalse(cut.IsOpen);
            W.Teleport(cut.Cell + WalkLattice.Dir(cut.OutHeading), cut.OutHeading + 3);
            yield return null;
            int before = InteriorDirector.Instance.CrossingCount;
            yield return Do(W.Forward);
            Assert.AreEqual(cut.Cell, W.Cell, "stands on the shut way");
            Assert.IsNull(Interiors.Current, "and stays on the surface");
            Assert.AreEqual(before, InteriorDirector.Instance.CrossingCount);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Doors_ObeyTheirFacts()
        {
            bool open = false;
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => f == "earth.ash_cut_open" ? open : true);
            var bar = C("ash_cut/tally_vault/bar");
            var door = InteriorScripts.Thing("ash_cut/tally_vault/bar").GetComponent<DoorView>();
            Assert.IsTrue(WalkGrid.IsBlocked(bar), "barred while the fact is unknown");
            Assert.IsFalse(door.Open);
            Assert.IsFalse(Interiors.IsDoorOpen("tally_vault", "bar"));
            W.Teleport(C("ash_cut/tally_vault/keeper"), 1);
            yield return null;
            Assert.IsNull(WalkGrid.FindPath(W.Cell, C("ash_cut/tally_vault/cut_exit")), "no way past the bar");
            Assert.AreEqual(0, Interiors.PathWorld("tally_vault", 4, 10, 7, 10).Count);
            open = true;
            yield return null; yield return null;
            Assert.IsFalse(WalkGrid.IsBlocked(bar), "open once the fact is known");
            Assert.IsTrue(door.Open);
            Assert.IsNotNull(WalkGrid.FindPath(W.Cell, C("ash_cut/tally_vault/cut_exit")));
            Assert.AreEqual(4, Interiors.PathWorld("tally_vault", 4, 10, 7, 10).Count);
        }

        // ---------------- the map and the way out ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Map_ShowsOnlyExploredCells_AndAlwaysTheWayOut()
        {
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => true);
            var m = Interiors.Get("quarry_upper");
            Assert.IsTrue(Interiors.Enter(new MarkerRef("glade", "quarry_upper", "exit")));
            yield return Idle();
            Assert.AreEqual("quarry_upper", Interiors.Current);
            var seen = new HashSet<Cell>(Interiors.Explored("quarry_upper"));
            Assert.Greater(seen.Count, 1);
            var exitLocal = C("glade/quarry_upper/exit") - m.Origin;
            foreach (var c in seen) Assert.LessOrEqual(WalkGrid.Distance(c, exitLocal), InteriorDirector.RevealSteps, "only cells near the player");
            var pedestal = m.Marker("pedestal");
            Assert.IsFalse(InteriorMapLayer.Discovered(m, pedestal), "the pedestal is not on the map yet");
            Assert.IsTrue(InteriorMapLayer.Discovered(m, m.Marker("exit")), "the way out is always on the map");
            Assert.IsTrue(InteriorMapLayer.Discovered(m, m.Marker("stair_down")) == seen.Contains(new Cell(m.Marker("stair_down").I, m.Marker("stair_down").J)), "a stair down shows once seen");
            MapScreen.Open();
            yield return null; yield return null;
            Assert.AreEqual(MapMode.Interior, MapScreen.Mode);
            Assert.AreEqual("quarry_upper", MapScreen.InteriorShown);
            Assert.Greater(InteriorMapLayer.Instance.LeadOutRect.width, 0, "Lead me out is offered");
            // The switch line shows the island; again, the interior.
            var sw = MapScreenView.Instance.SwitchRect;
            Assert.Greater(sw.width, 0);
            Assert.IsTrue(MapScreenView.Instance.TapScreen(new Vector2(sw.center.x, Screen.height - sw.center.y)));
            Assert.AreEqual(MapMode.Island, MapScreen.Mode);
            yield return null;
            Assert.IsTrue(MapScreenView.Instance.TapScreen(new Vector2(sw.center.x, Screen.height - sw.center.y)));
            Assert.AreEqual(MapMode.Interior, MapScreen.Mode);
            // A tap on the pedestal's cell walks there when the map closes.
            yield return null;
            var g = MapScreenView.Instance.WorldToGui(m.World(pedestal.I, pedestal.J + 1));
            Assert.IsTrue(MapScreenView.Instance.TapScreen(new Vector2(g.x, Screen.height - g.y)));
            Assert.IsTrue(MapScreen.Target.HasValue && m.Floor.Contains(MapScreen.Target.Value), "the target is an interior cell");
            MapScreen.Close();
            yield return Idle(30f);
            Assert.IsTrue(InteriorMapLayer.Discovered(m, pedestal), "seen now");
            var after = Interiors.Explored("quarry_upper");
            Assert.Greater(after.Count, seen.Count);
            Assert.Less(after.Count, m.Floor.Count, "not the whole quarry");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LeadMeOut_ReachesTheSurface_ByTheNearestOpenWay()
        {
            bool cutOpen = false;
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => f == "earth.ash_cut_open" ? cutOpen : true);
            // From the vault with the bar shut: up the stair, through the quarry and out to the glade.
            W.Teleport(C("ash_cut/tally_vault/caster"), 0);
            yield return null;
            Assert.AreEqual("tally_vault", Interiors.Current);
            Assert.IsTrue(InteriorDirector.Instance.FindWayOut(W.Cell, out var first, out var surface, out int steps));
            Assert.AreEqual("ash_cut/tally_vault/stair_up", first.End.ToString());
            Assert.AreEqual("glade/quarry_upper/exit", surface.End.ToString());
            Assert.IsTrue(Interiors.LeadOut());
            float end = Time.realtimeSinceStartup + 90f;
            while ((Interiors.IsLeadingOut || Interiors.IsCrossing || W.IsAutoWalking) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsNull(Interiors.Current, "on the surface");
            Assert.AreEqual(L("glade/stair").Cell, W.Cell, "at the glade's stair");
            // With the bar open, the Ash cut is nearer from the altar.
            cutOpen = true;
            W.Teleport(C("ash_cut/tally_vault/caster"), 0);
            yield return null; yield return null;
            Assert.IsTrue(InteriorDirector.Instance.FindWayOut(W.Cell, out first, out surface, out _));
            Assert.AreEqual("ash_cut/tally_vault/cut_exit", first.End.ToString());
            Assert.IsTrue(Interiors.Exit(), "Exit takes the player up at once");
            yield return Idle();
            Assert.IsNull(Interiors.Current);
            Assert.AreEqual(L("ash_cut/cut").Cell, W.Cell);
        }

        // ---------------- save, light, budget ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator SaveAndLoad_Underground_RestoresThePlayerInPlace()
        {
            yield return LoadWithCampaign(InteriorScripts.AuthoredPack);
            // The stair opens only through the story; here it is reached directly (Enter) as the expedition would.
            Assert.IsTrue(Interiors.Enter(new MarkerRef("ash_cut", "tally_vault", "threshold")));
            yield return Idle();
            Assert.AreEqual("tally_vault", Interiors.Current);
            yield return Do(W.TurnLeft);
            var cell = W.Cell; int heading = W.Heading;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            var old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            float end = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < end && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old)) yield return null;
            yield return null; yield return null; yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.AreNotSame(old, _boot);
            Assert.IsNotNull(S.Campaign, "the campaign loaded");
            Assert.AreEqual(2, Interiors.All.Count, "the interiors rebuilt from the loaded state");
            Assert.AreEqual(cell, W.Cell, "the same cell");
            Assert.AreEqual(heading, W.Heading, "the same heading");
            Assert.AreEqual("tally_vault", Interiors.Current, "still in the Tally Vault");
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(InteriorLighting.Instance.Underground && InteriorLighting.Instance.SkyDark, "lit as underground");
            yield return Do(W.TurnRight);
            Assert.IsTrue(WalkGrid.IsBlocked(C("ash_cut/tally_vault/bar")), "the bar still holds");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NoSkyLight_Underground_ByNightOrDay()
        {
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => true);
            foreach (double hour in new[] { 23.0, 12.0 })
            {
                DayNight.Instance.SetHour(hour);
                W.Teleport(L("glade/stair").Cell + WalkLattice.Dir(L("glade/stair").OutHeading), 0);
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.IsFalse(InteriorLighting.Instance.Underground);
                Assert.IsTrue(DayNight.Instance.Sun.enabled || DayNight.Instance.Moon.enabled, "the sky lights the surface at " + hour);
                Assert.IsTrue(Interiors.Enter(new MarkerRef("glade", "quarry_upper", "pedestal")));
                yield return Idle();
                yield return new WaitForEndOfFrame();
                Assert.IsTrue(InteriorLighting.Instance.Underground);
                Assert.IsFalse(DayNight.Instance.Sun.enabled, "no sun underground at " + hour);
                Assert.IsFalse(DayNight.Instance.Moon.enabled, "no moon underground at " + hour);
                Assert.IsTrue(InteriorLighting.Instance.SkyDark, "no sky underground at " + hour);
                Assert.Less(RenderSettings.ambientSkyColor.maxColorComponent, InteriorLighting.MaxAmbient, "dark ambient at " + hour);
                Assert.IsTrue(WorldLights.Instance.Lantern.enabled && WorldLights.Instance.Lantern.intensity > 0.5f, "the lantern is lit at " + hour);
                var dome = _boot.transform.Find("SkyDome");
                if (dome != null) Assert.IsFalse(dome.GetComponent<Renderer>().enabled, "the sky dome is hidden");
                Assert.IsTrue(Interiors.Exit());
                yield return Idle();
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.IsFalse(InteriorLighting.Instance.Underground);
                if (dome != null) Assert.IsTrue(dome.GetComponent<Renderer>().enabled, "the sky dome is back");
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FrameRate_AndLightCount_InBudget_Underground()
        {
            yield return LoadBuilt(InteriorScripts.AuthoredPack, f => true);
            foreach (var at in new[] { "ash_cut/tally_vault/caster", "glade/quarry_upper/pedestal", "glade/quarry_upper/exit" })
            {
                MarkerRef.TryParse(at, out var r);
                Assert.IsTrue(Interiors.Enter(r));
                yield return Idle();
                float t0 = Time.realtimeSinceStartup; int frames = 0;
                while (Time.realtimeSinceStartup - t0 < 1.5f) { frames++; yield return null; }
                float fps = frames / (Time.realtimeSinceStartup - t0);
                int pointLights = 0;
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.enabled && l.isActiveAndEnabled && l.type == LightType.Point && l.intensity > 0f) pointLights++;
                Debug.Log("[InteriorTests] " + at + ": " + fps.ToString("0") + " fps, " + pointLights + " point lights, pixel " + WorldLights.Instance.PixelLightsNow);
                Assert.LessOrEqual(WorldLights.Instance.PixelLightsNow, WorldLights.PixelBudget, "pixel lights at " + at);
                Assert.LessOrEqual(pointLights, WorldLights.PixelBudget, "point lights on at " + at);
                Assert.Greater(fps, 20f, "frame rate at " + at);
            }
        }

        // ---------------- reading markers ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator Markers_FollowTheSim_PedestalTallyAndAltar()
        {
            yield return LoadWithCampaign(InteriorScripts.AuthoredPack);
            yield return null;
            Assert.IsTrue(InteriorScripts.StandHasCrystal("glade/quarry_upper/pedestal"), "the Deepstone on its pedestal");
            Assert.IsFalse(InteriorScripts.StandHasCrystal("ash_cut/tally_vault/altar"));
            Assert.IsFalse(InteriorScripts.TallyShown("glade/quarry_upper/tally"));
            Assert.IsTrue(_boot.Sim.Submit(new DevSetCustody { Crystal = Element.Earth, To = Custody.AtRivalAltar }).Ok);
            _boot.Clock.StepOnce();
            yield return null;
            Assert.IsFalse(InteriorScripts.StandHasCrystal("glade/quarry_upper/pedestal"), "the pedestal is bare");
            Assert.IsTrue(InteriorScripts.StandHasCrystal("ash_cut/tally_vault/altar"), "the Deepstone on the Ash altar");
            Assert.IsTrue(InteriorScripts.TallyShown("glade/quarry_upper/tally"), "an Ash tally stick by the bare pedestal");
            // Survey marks read with the story's own lines.
            var marks = (InteriorThing)InteriorScripts.Thing("glade/quarry_upper/marks");
            var lines = new List<string>(marks.Lines);
            Assert.Greater(lines.Count, 0);
            StringAssert.Contains("Double cuts", lines[0]);
            // The surface stair stays shut until Mira reads the stone.
            Assert.IsFalse(Interiors.LinkAt(MarkerRef.Surface("glade", "stair")).IsOpen);
            Assert.IsTrue(Interiors.LinkAt(new MarkerRef("glade", "quarry_upper", "stair_down")).IsOpen);
        }

        // ---------------- no state writes ----------------

        [Test]
        public void NoWorldStateWrites_InInteriorFiles()
        {
            var files = new List<string>(Directory.GetFiles(Path.Combine(Application.dataPath, "World", "Game", "Interiors"), "*.cs", SearchOption.AllDirectories));
            files.Add(Path.Combine(Application.dataPath, "World", "Game", "UI", "MapScreen.cs"));
            files.Add(Path.Combine(Application.dataPath, "World", "Game", "UI", "MapLayers.cs"));
            var write = new Regex(@"\b(State|s|S|state|c|Campaign)\.(Economy|Factions|Demons|Board|Players|Ward|Tick|Rng|Seed|Campaign|Facts|Party|Crystals|Eras)\b[\w\.\[\]\(\)]*\s*(\+\+|--|(?<![=!<>])[+\-*/]?=(?!=))");
            var calls = new Regex(@"\.(Facts|Crystals|Companions|Journal)\.(Add|Remove|Insert|Clear|Sort)\(");
            foreach (var f in files)
            {
                int n = 0;
                foreach (var line in File.ReadAllLines(f))
                {
                    n++;
                    var code = line.Trim();
                    if (code.StartsWith("//")) continue;
                    Assert.IsFalse(write.IsMatch(code), Path.GetFileName(f) + ":" + n + " writes WorldState: " + code);
                    Assert.IsFalse(calls.IsMatch(code), Path.GetFileName(f) + ":" + n + " changes WorldState: " + code);
                }
            }
        }
    }
}
