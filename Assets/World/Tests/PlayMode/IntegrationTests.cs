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
            // Too far: a press on the mark is refused with "walk closer".
            Assert.Greater(SpellCaster.DistanceTo(top.Id), (float)FactionApi.CastRangeMetres(S));
            Assert.AreEqual(TapOutcome.CastRefused, PointerInput.TapGround(target));
            Assert.AreEqual(SpellCaster.TooFar, SpellCaster.LastResult.Reason);
            // Stand two steps from the plot, turned to it, and click the mark on screen (through its trigger collider).
            var cell = WalkLattice.ToCell(target);
            Cell? stand = null; int heading = 0;
            for (int h = 0; h < 6 && !stand.HasValue; h++)
            {
                var c = cell + WalkLattice.Dir(h) * 2;
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
