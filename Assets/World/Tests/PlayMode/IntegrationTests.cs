using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;
using Cell = World.Game.WalkLattice.Cell;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game.Tests
{
    /// <summary>
    /// Integration of packages S, P, V and D (tag pre-integrated-1): the land views against the real logistics, casting
    /// through the real pointer on the real views, the cast mark's beam, the dance with the HUD off, and the
    /// "cast_all" AutoPlay script played to an ending.
    /// </summary>
    public class IntegrationTests : EncounterTestBase
    {
        WorldState S => Boot.Sim.State;

        [UnityTest, Timeout(120000)]
        public IEnumerator CarrierViews_FollowTheRealSim_PositionsLoadsAndPorters()
        {
            yield return Load();
            Boot.Clock.Pause("test");
            int movingSeen = 0, loadedSeen = 0, portersSeen = 0;
            for (int round = 0; round < 40; round++)
            {
                // Grow the people with their own proposals (the F1 cast: no range check), so roads and shipments appear.
                for (int i = 0; i < 60; i++)
                {
                    if (i % 20 == 0 && FactionApi.Top(S, PlayerIds.Human) != null) DevTools.CastQueued(PlayerIds.Human, 0);
                    Boot.Clock.StepOnce();
                }
                // Let the views settle on the end of the tick (the tick fraction clamps at 1 while the clock is held).
                float until = Time.time + 0.25f;
                while (Time.time < until) yield return null;
                yield return new WaitForEndOfFrame();
                Assert.AreEqual(S.Economy.Carriers.Count, CountCarriers(), "one view per carrier");
                foreach (var c in S.Economy.Carriers)
                {
                    var v = LandViews.Instance.Carrier(c.Id);
                    Assert.IsNotNull(v, "view of carrier " + c.Id);
                    Assert.AreEqual(c.HasLoad, v.Figure.HasLoad, "carrier " + c.Id + " load shown");
                    bool onRoad = c.LegCorners != null && c.LegCorners.Length > 0;
                    if (onRoad) Assert.IsTrue(v.Shipment, "carrier " + c.Id + " on a main-road leg looks like a porter");
                    if (c.State == CarrierState.Idle) continue;
                    movingSeen++;
                    if (c.HasLoad) loadedSeen++;
                    if (onRoad && c.HasLoad) portersSeen++;
                    Assert.IsTrue(EconomyApi.CarrierPosition(S, c.Id, 1.0, out double x, out double z, out _));
                    var p = v.transform.position;
                    Assert.Less(Mathf.Abs(p.x - (float)x) + Mathf.Abs(p.z - (float)z), 0.05f, "carrier " + c.Id + " stands on the sim's position");
                }
                if (portersSeen > 3 && round > 12) break;
            }
            Assert.Greater(movingSeen, 20, "carriers were seen walking");
            Assert.Greater(loadedSeen, 5, "carriers were seen carrying");
            Assert.Greater(portersSeen, 0, "a loaded porter was seen on a main road");
        }

        int CountCarriers() { int n = 0; foreach (var _ in LandViews.Instance.Carriers) n++; return n; }

        [UnityTest, Timeout(120000)]
        public IEnumerator Cast_OnTheRealViews_ByScreenPress_SiteCarriersBuilding()
        {
            yield return Load();
            yield return Until(() => FactionApi.Top(S, PlayerIds.Human) != null, 10f, "the opening proposal");
            var top = FactionApi.Top(S, PlayerIds.Human);
            Assert.AreEqual(ProposalKind.Building, top.Kind);
            int plot = top.Plot;
            var marker = LandViews.Instance.Marker(top.Id);
            Assert.IsNotNull(marker, "cast mark of the top proposal");
            // From the spawn (about 30 m away) the beam shows; close by it is hidden so it cannot white out the view.
            yield return null; yield return null;
            Assert.IsTrue(marker.IsTop && marker.PillarShown, "the beam is a beacon from afar");
            var target = marker.Target;
            // Too far (from where the prince starts): a press on the mark is refused with "walk closer".
            var campL = new CampLayout(S.Board);
            Boot.Avatar.Teleport(campL.PrinceStart, campL.SpawnHeading);
            yield return null;
            Assert.IsTrue(marker.PillarShown, "the beam still shows from further off");
            Assert.Greater(SpellCaster.DistanceTo(top.Id), (float)FactionApi.CastRangeMetres(S));
            Assert.AreEqual(TapOutcome.CastRefused, PointerInput.TapGround(target));
            Assert.AreEqual(SpellCaster.TooFar, SpellCaster.LastResult.Reason);
            // Stand one step from the plot, turned to it, and click the mark on screen (through its trigger collider).
            var cell = WalkLattice.ToCell(target);
            Cell? stand = null; int heading = 0;
            for (int h = 0; h < 6 && !stand.HasValue; h++)
            {
                var c = cell + WalkLattice.Dir(h);
                if (!WalkGrid.IsBlocked(c)) { stand = c; heading = (h + 3) % 6; }
            }
            Assert.IsTrue(stand.HasValue, "a cell to cast from");
            Boot.Avatar.Teleport(stand.Value, heading);
            yield return null; yield return null;
            Assert.IsFalse(marker.PillarShown, "the beam is hidden close by");
            var cam = ViewSwitcher.Instance.ActiveCamera;
            var sp = cam.WorldToScreenPoint(target);
            Assert.Greater(sp.z, 0f, "the plot is in view");
            var hit = PointerInput.Raycast(sp);
            Assert.IsTrue(hit.HasGround, "ground under the press");
            Assert.Less(Mathf.Abs(hit.Ground.y - target.y), 0.3f, "the press lands on the ground, not on a view's collider");
            Assert.AreEqual(top.Id, hit.ProposalId, "the press is on the proposal's ground");
            Assert.AreEqual(TapOutcome.Cast, PointerInput.TapAt(sp), "cast by a screen press: " + SpellCaster.LastResult.Message);
            // A second press before the next tick is not a second spell.
            Assert.AreNotEqual(TapOutcome.Cast, PointerInput.TapAt(sp), "double press");
            // Site, deliveries, building.
            Boot.Clock.Pause("test");
            Building b = null;
            for (int i = 0; i < 20 && b == null; i++) { Boot.Clock.StepOnce(); foreach (var x in S.Economy.Buildings) if (x.Plot == plot && x.State != BuildingState.Cancelled && x.State != BuildingState.Retired) b = x; }
            Assert.IsNotNull(b, "a site on the plot");
            yield return null;
            Assert.IsNotNull(LandViews.Instance.BuildingView(b.Id), "the site has a view");
            bool delivering = false;
            for (int i = 0; i < 3000 && b.State != BuildingState.Active; i++)
            {
                Boot.Clock.StepOnce();
                foreach (var c in S.Economy.Carriers) if (c.Faction == PlayerIds.Human && c.HasLoad && c.LegToKind == PlaceKind.Building && c.LegToId == b.Id) delivering = true;
                if (i % 50 == 0) yield return null;
            }
            Assert.IsTrue(delivering, "a carrier brought goods to the site");
            Assert.AreEqual(BuildingState.Active, b.State, "the building was finished");
            yield return null;
            Assert.IsNotNull(LandViews.Instance.BuildingView(b.Id), "the finished building has a view");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Dance_WithTheHudOff_SolCallsIt_AndTouchStripsPlay()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new World.Sim.Encounters.Rhythm.RhythmTuning();
            var camp = new CampLayout(S.Board);
            var sol = Npc.Find(NpcIds.Host);
            var front = camp.DanceCircleCentre + camp.Forward * 3.2f + camp.Right * 1.2f;
            yield return Accept(NpcIds.Host, front, BoardSpace.YawOf(sol.transform.position - front));
            var r = RhythmPartyEncounter.Current;
            Assert.IsNotNull(r, "the dance started by a tap and a bubble reply");
            Assert.IsFalse(Hud.Visible, "HUD off by default");
            Assert.IsFalse(RhythmPartyEncounter.OverlayOn, "no dance overlay with the HUD off");
            Assert.IsTrue(SpeechBubbles.ShowInEncounter, "Sol's bubbles show during the dance");
            Assert.IsFalse(string.IsNullOrEmpty(r.SolSaid), "Sol says how to play");
            // Touch strips are the drums.
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Playing || r.SongTime > 0.2, 20f, "the phrase starts");
            int before = r.PressesHandled;
            TouchStrips.SimulatePress(2);
            TouchStrips.SimulateHold(2, true);
            yield return null; yield return null;
            TouchStrips.SimulateHold(2, false);
            yield return null;
            Assert.Greater(r.PressesHandled, before, "a touch strip strikes its drum");
            // The first block is missed (nobody plays): Sol says so and the fire sinks; then the autoplayer keeps one.
            int lines = r.SolLines, falters = r.Circle.Falters;
            yield return Until(() => r.Session.BlockResultOf(0) != null, 40f, "block 1 decided");
            yield return null;
            Assert.IsFalse(r.Session.BlockResultOf(0).Passed, "block 1 missed");
            Assert.Greater(r.SolLines, lines, "Sol calls the missed block");
            Assert.Greater(r.Circle.Falters, falters, "the fire sinks on a missed block");
            r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Balcony;
            lines = r.SolLines;
            yield return Until(() => r.Session.BlockResultOf(1) != null, 40f, "block 2 decided");
            yield return null;
            Assert.IsTrue(r.Session.BlockResultOf(1).Passed, "block 2 kept");
            Assert.Greater(r.SolLines, lines, "Sol calls the kept block");
            EncounterHost.Instance.Abort();
            yield return null;
            Assert.IsFalse(SpeechBubbles.ShowInEncounter, "bubbles hidden in encounters again");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Camp_NothingGrowsInClearings_GatherersGoRoundTheCircle_IdleCarriersKeepOff()
        {
            yield return Load();
            yield return null; yield return null;
            var camp = new CampLayout(S.Board);
            var t = S.Board.Topology;
            // Nothing grows inside a clearing of the home camp.
            int checkedPoints = 0;
            for (int h = 0; h < t.HexCount; h++)
            {
                if (BoardSpace.Flat(BoardSpace.Hex(t, h) - camp.Hearth) > 130f) continue;
                foreach (var pt in TerrainBuilder.PropPoints(h))
                {
                    checkedPoints++;
                    foreach (var c in camp.Clearings)
                    {
                        float dx = pt.x - c.x, dz = pt.z - c.y;
                        Assert.GreaterOrEqual(Mathf.Sqrt(dx * dx + dz * dz), c.z - 0.01f, "a prop of hex " + h + " at " + pt + " stands in a camp clearing at (" + c.x + ", " + c.y + ") r " + c.z);
                    }
                }
            }
            Assert.Greater(checkedPoints, 200, "prop points recorded round the home camp");

            // Every gatherer's way keeps out of the dance circle.
            float ring = (float)PlotLayout.DanceCircleRadius + 0.5f;
            int routes = 0, bent = 0;
            foreach (var bv in LandViews.Instance.Buildings)
            {
                if (!bv.IsCollector) continue;
                var r = bv.GathererRoute;
                routes++;
                if (r.Count > 2) bent++;
                for (int i = 1; i < r.Count; i++)
                {
                    var a = r[i - 1]; var ab = r[i] - a; ab.y = 0;
                    float u = Mathf.Clamp01(Vector3.Dot(camp.DanceCircleCentre - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    Assert.Greater(BoardSpace.Flat(a + ab * u - camp.DanceCircleCentre), ring, "a gatherer's way crosses the dance circle (building " + bv.BuildingId + ")");
                }
            }
            Assert.Greater(routes, 3, "collectors with gatherers");
            Assert.Greater(bent, 0, "the Fields gatherer of the home camp steps round the circle");

            // Idle carriers wait by the hearth, at least 3 m from every cell the player can stand on.
            int idle = 0;
            foreach (var c in S.Economy.Carriers)
            {
                if (c.State != CarrierState.Idle) continue;
                var v = LandViews.Instance.Carrier(c.Id);
                Assert.IsNotNull(v);
                idle++;
                var pos = v.transform.position;
                var cell = WalkLattice.ToCell(pos);
                for (int di = -2; di <= 2; di++)
                    for (int dj = -2; dj <= 2; dj++)
                    {
                        var n = new Cell(cell.I + di, cell.J + dj);
                        if (WalkGrid.IsBlocked(n)) continue;
                        Assert.GreaterOrEqual(BoardSpace.Flat(WalkLattice.ToWorld(n) - pos), 3f, "idle carrier " + c.Id + " stands near a walkable cell");
                    }
            }
            Assert.Greater(idle, 3, "idle carriers at the start");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Camp_DressingIsSolid_AndTrapsNobody()
        {
            yield return Load();
            var camp = new CampLayout(S.Board);
            Assert.Greater(WalkGrid.SolidCount, 5, "dressing registered as solids");
            // The spawn, the prince's line, Mira and Sol stay reachable.
            Assert.IsFalse(WalkGrid.IsBlocked(camp.Spawn), "spawn free");
            var c = camp.Spawn;
            for (int i = 0; i < 4; i++)
            {
                var n = c + WalkLattice.Dir(camp.SpawnHeading);
                Assert.IsFalse(WalkGrid.IsStepBlocked(c, n), "the line from the spawn to the prince's start is free (step " + i + ")");
                c = n;
            }
            foreach (string id in new[] { NpcIds.Mira, NpcIds.Host })
            {
                var npc = Npc.Find(id);
                var near = AutoPlay.ApproachCell(npc.transform.position, 0.1f);
                Assert.IsTrue(near.HasValue, "a free cell beside " + id);
                Assert.IsNotNull(WalkGrid.FindPath(camp.Spawn, near.Value), "a way from the spawn to " + id);
                Assert.LessOrEqual(BoardSpace.Flat(WalkLattice.ToWorld(near.Value) - npc.transform.position), 8f, id + " can be reached to a neighbouring cell");
            }
            // No step of any found path passes through a solid, and a step into a tent bumps.
            int solidSteps = 0;
            foreach (var prop in camp.Dressing)
            {
                if (prop.Kind == "figure") continue;
                var pc = WalkLattice.ToCell(prop.Position);
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                        for (int h = 0; h < 6; h++)
                        {
                            var a = new Cell(pc.I + di, pc.J + dj); var b = a + WalkLattice.Dir(h);
                            if (WalkGrid.SolidBetween(a, b) == null) continue;
                            solidSteps++;
                            if (WalkGrid.IsBlocked(a) || WalkGrid.IsBlocked(b)) continue;
                            var path = WalkGrid.FindPath(a, b);
                            Assert.IsNotNull(path, "the cells either side of a " + prop.Kind + " stay joined another way");
                            Assert.Greater(path.Count, 1, "the path goes round the " + prop.Kind);
                        }
            }
            Assert.Greater(solidSteps, 0, "some step is blocked by dressing");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Night_Start_LanternOnTheWalker_ScrubWorks()
        {
            yield return Load();
            yield return null; yield return null;
            Assert.IsNotNull(DayNight.Instance, "day-night cycle installed");
            Assert.AreEqual(22.5, DayNight.HourAtTick(0), 0.01, "a new game starts at 22:30");
            Assert.Greater(DayNight.Instance.Night, 0.95f, "full night at the start");
            var lantern = WorldLights.Instance.Lantern;
            Assert.IsTrue(lantern.enabled, "lantern lit");
            Assert.Less(BoardSpace.Flat(lantern.transform.position - Boot.Avatar.Position), 2.5f, "the lantern rides with the lattice walker");
            Boot.Avatar.Teleport(Boot.Avatar.Cell + WalkLattice.Dir(Boot.Avatar.Heading) * 3, Boot.Avatar.Heading);
            yield return null; yield return null;
            Assert.Less(BoardSpace.Flat(lantern.transform.position - Boot.Avatar.Position), 2.5f, "the lantern follows the walker");
            DevTools.TimeOfDay(3);
            yield return null;
            Assert.Less(DayNight.Instance.Night, 0.05f, "noon by the F1 scrub");
            DevTools.TimeOfDay(5);
            yield return null;
            Assert.Greater(DayNight.Instance.Night, 0.95f, "night again");
            DayNight.DevOffsetHours = 0;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator CastAll_AutoPlay_PlaysToAnEnding()
        {
            yield return Load();
            Assert.IsNull(PrinceDirector.Instance != null ? PrinceDirector.Instance.Prince : null, "no prince present");
            var ap = AutoPlay.Instance;
            Assert.IsTrue(AutoPlayScripts.TryCreate(CastAll.Name, out var script), "cast_all registered");
            CastAll.Speed = 8f;
            bool? ok = null;
            ap.Finished += (s, pass) => ok = pass;
            ap.Run(script);
            float until = Time.realtimeSinceStartup + 560f;
            while (ok == null && Time.realtimeSinceStartup < until) yield return null;
            Time.timeScale = 1f;
            Assert.IsTrue(ok == true, "cast_all: " + (ap.FailMessage ?? "timed out"));
            Assert.IsTrue(CastAll.Finished && S.Factions.GameOver, "the game ended");
            Assert.AreNotEqual(GameOutcome.None, CastAll.Result);
            Assert.Greater(CastAll.Casts, 10, "casts");
            Assert.AreEqual(CastAll.Casts, CastAll.ScreenCasts, "every cast went through a real screen press");
            Assert.Greater(CastAll.MetresWalked, 100f, "walked");
            Debug.Log("[Integration] cast_all: " + CastAll.Result + " at minute " + CastAll.GameMinute.ToString("0.0") + ", casts " + CastAll.Casts + ", walked " + CastAll.MetresWalked.ToString("0") + " m");
        }
    }
}
