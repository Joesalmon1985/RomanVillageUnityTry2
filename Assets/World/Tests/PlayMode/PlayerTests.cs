using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;
using World.Sim.Save;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game.Tests
{
    /// <summary>
    /// Package P (REFACTOR_SPEC 8.2): lattice geometry, stepping and turning, blocked cells, touch strips, auto-walk,
    /// follow, the pointer, speech bubbles, casting against the opening proposal (plot 631), the map and HUD toggles,
    /// encounter pose restore, the F1 developer tools, walk_tour and the no-WorldState-writes grep.
    /// </summary>
    public class PlayerTests
    {
        private const string TestPause = "PlayerTest";
        private WorldBootstrap Boot;
        private PlayerWalker W => Boot.Avatar;
        private WorldState S => Boot.Sim.State;

        private IEnumerator Load(bool pause = true)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.IsNotNull(Boot, "WorldBootstrap.Instance");
            Assert.IsNotNull(PlayerWalker.Instance, "PlayerWalker created");
            if (pause) Boot.Clock.Pause(TestPause);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
            if (MapScreen.IsOpen) MapScreen.Close();
            Hud.Visible = false;
            WalkGrid.RemoveStaticBlockers("test");
        }

        private IEnumerator Idle(float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while ((W.IsGliding || W.IsAutoWalking) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(W.IsGliding, "walker still gliding");
        }

        private IEnumerator Do(Action move)
        {
            yield return Idle();
            move();
            yield return null;
            yield return Idle();
        }

        private static void AssertAt(PlayerWalker w, Cell c, string what)
        {
            Assert.AreEqual(c, w.Cell, what + ": cell");
            Assert.Less(BoardSpace.Flat(w.Position - WalkLattice.ToWorld(c)), 0.001f, what + ": position on the cell");
        }

        private Cell CornerCell(int corner) => WalkLattice.ToCell(BoardSpace.Vertex(S.Board.Topology, corner));

        /// <summary>A corner with no settlement, its first neighbouring corner and the heading of the edge between.</summary>
        private void FreeEdge(out int a, out int b, out int heading)
        {
            var t = S.Board.Topology;
            for (a = 0; a < t.CornerCount; a++)
            {
                if (!PlotLayout.IsSettlementCorner(t, a) || EconomyApi.SettlementAt(S, a) != null) continue;
                foreach (int n in t.CornerNeighbours(a))
                {
                    if (EconomyApi.SettlementAt(S, n) != null) continue;
                    b = n;
                    heading = WalkLattice.HeadingOf(BoardSpace.YawOf(BoardSpace.Vertex(t, n) - BoardSpace.Vertex(t, a)));
                    return;
                }
            }
            throw new Exception("no free hex edge");
        }

        // ---------------- geometry ----------------

        [UnityTest]
        public IEnumerator Lattice_CornersHexCentresAndPlots_AreExactCells()
        {
            yield return Load();
            var t = S.Board.Topology;
            for (int c = 0; c < t.CornerCount; c++)
            {
                var p = BoardSpace.Vertex(t, c);
                Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(WalkLattice.ToCell(p)) - p), 0.001f, "corner " + c);
            }
            for (int h = 0; h < t.HexCount; h++)
            {
                var p = BoardSpace.Hex(t, h);
                Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(WalkLattice.ToCell(p)) - p), 0.001f, "hex centre " + h);
            }
            int plots = 0;
            for (int c = 0; c < t.CornerCount; c++)
            {
                if (!PlotLayout.IsSettlementCorner(t, c)) continue;
                for (int sec = 0; sec < 3; sec++)
                    for (int slot = 0; slot < 3; slot++)
                    {
                        int plot = PlotLayout.PlotId(c, sec, slot);
                        var p = new Vector3((float)PlotLayout.PlotX(t, plot), 0, (float)PlotLayout.PlotZ(t, plot));
                        Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(WalkLattice.ToCell(p)) - p), 0.001f, "plot " + plot);
                        plots++;
                    }
            }
            Assert.Greater(plots, 500, "plot centres checked");
            var camp = new CampLayout(S.Board);
            AssertAt(W, camp.Spawn, "spawn");
            Assert.AreEqual(camp.SpawnHeading, W.Heading, "spawn heading");
            Assert.AreEqual(PlayerWalker.FieldOfView, Boot.MainCamera.fieldOfView, 0.01f, "field of view 70");
            Assert.AreEqual(PlayerWalker.FixedPitch, W.Pitch, 0.01f, "fixed pitch");
            Assert.AreEqual(WorldBootstrap.EyeHeight, W.EyePosition.y - W.Position.y, 0.001f, "eye height");
        }

        // ---------------- stepping ----------------

        [UnityTest]
        public IEnumerator Steps_AHexEdgeIsTwelveForwardSteps_SixTurnsReturnTheHeading()
        {
            yield return Load();
            FreeEdge(out int a, out int b, out int h);
            W.Teleport(CornerCell(a), h);
            yield return null;
            int steps = 0;
            W.Stepped += () => steps++;
            float t0 = Time.time;
            for (int i = 0; i < 12; i++)
            {
                yield return Do(W.Forward);
                Assert.AreEqual(h, W.Heading, "no turning on the edge");
                AssertAt(W, CornerCell(a) + WalkLattice.Dir(h) * (i + 1), "step " + (i + 1));
            }
            Assert.AreEqual(12, steps, "Stepped raised per step");
            AssertAt(W, CornerCell(b), "the far corner after 12 steps");
            Debug.Log("[PlayerTests] 12 keyed steps took " + (Time.time - t0).ToString("0.00") + " s");
            yield return Do(W.Back);
            AssertAt(W, CornerCell(b) - WalkLattice.Dir(h), "a step back");
            Assert.AreEqual(h, W.Heading, "step back keeps the heading");

            for (int k = 1; k <= 6; k++)
            {
                yield return Do(W.TurnRight);
                Assert.AreEqual((h + k) % 6, W.Heading, "right turn " + k);
                Assert.AreEqual(WalkLattice.Yaw(h + k), Mathf.Repeat(W.Yaw, 360f), 0.01f, "yaw after right turn " + k);
            }
            Assert.AreEqual(h, W.Heading, "six right turns return the heading");
            for (int k = 1; k <= 6; k++) yield return Do(W.TurnLeft);
            Assert.AreEqual(h, W.Heading, "six left turns return the heading");
            AssertAt(W, CornerCell(b) - WalkLattice.Dir(h), "turning does not move");
        }

        [UnityTest]
        public IEnumerator Steps_GlideTakesStepSeconds_AndOneInputIsBuffered()
        {
            yield return Load();
            FreeEdge(out int a, out _, out int h);
            W.Teleport(CornerCell(a), h);
            yield return null;
            float t0 = Time.time;
            W.Forward();
            W.Forward(); // buffered
            W.Forward(); // replaces the buffered one (only one is kept)
            Assert.IsTrue(W.IsGliding);
            yield return null;
            yield return Idle();
            float took = Time.time - t0;
            AssertAt(W, CornerCell(a) + WalkLattice.Dir(h) * 2, "two steps: one plus one buffered");
            Assert.AreEqual(2 * PlayerWalker.StepSeconds, took, 0.15f, "each step glides 0.4 s");
        }

        [UnityTest]
        public IEnumerator BlockedCells_Bump_AndStaticBlockersAndBuildingsBlock()
        {
            yield return Load();
            var camp = new CampLayout(S.Board);
            var corner = WalkLattice.ToCell(camp.Hearth);
            Assert.IsTrue(WalkGrid.IsBlocked(corner), "the home storehouse corner is blocked");
            Assert.AreEqual("camp", WalkGrid.WhyBlocked(corner));
            // The log pile on plot 630 is active: its cell is blocked; the empty plot 631 is walkable.
            var t = S.Board.Topology;
            Assert.IsTrue(WalkGrid.IsBlocked(WalkLattice.ToCell(DevTools.PlotPos(S, 630))), "an active building's plot blocks");
            Assert.IsFalse(WalkGrid.IsBlocked(WalkLattice.ToCell(DevTools.PlotPos(S, 631))), "an empty plot stays walkable");
            // Beyond the board.
            Assert.IsTrue(WalkGrid.IsBlocked(WalkLattice.ToCell(new Vector3(BoardSpace.BoardRadius + 30f, 0, 0))), "beyond the board");

            // Walk into the storehouse: bump, stay.
            int h = 0;
            var beside = corner - WalkLattice.Dir(h);
            W.Teleport(beside, h);
            yield return null;
            int bumps = W.BumpCount;
            Cell? bumpedInto = null;
            W.Bumped += c => bumpedInto = c;
            yield return Do(W.Forward);
            AssertAt(W, beside, "after the bump");
            Assert.AreEqual(bumps + 1, W.BumpCount, "one bump");
            Assert.AreEqual(corner, bumpedInto, "bumped into the corner cell");

            // A registered static blocker (V's peaks, D's fire) blocks its cells.
            var ahead = beside - WalkLattice.Dir(h);
            WalkGrid.AddStaticBlocker(WalkLattice.ToWorld(ahead), 2f, "test");
            W.Teleport(beside, (h + 3) % 6);
            yield return Do(W.Forward);
            AssertAt(W, beside, "static blocker bumps");
            WalkGrid.RemoveStaticBlockers("test");
            yield return Do(W.Forward);
            AssertAt(W, ahead, "free again after RemoveStaticBlockers");
        }

        [UnityTest]
        public IEnumerator TouchStrips_MapToTheFourMoves()
        {
            yield return Load();
            Assert.AreEqual(1, TouchStrips.StripAt(new Vector2(1, 1)));
            Assert.AreEqual(4, TouchStrips.StripAt(new Vector2(Screen.width - 1, 1)));
            Assert.AreEqual(0, TouchStrips.StripAt(new Vector2(10, Screen.height * 0.5f)), "above the band: pointer taps");
            FreeEdge(out int a, out _, out int h);
            W.Teleport(CornerCell(a), h);
            yield return null;
            TouchStrips.SimulatePress(4); yield return null; yield return Idle();
            Assert.AreEqual((h + 1) % 6, W.Heading, "strip 4 turns right");
            TouchStrips.SimulatePress(1); yield return null; yield return Idle();
            Assert.AreEqual(h, W.Heading, "strip 1 turns left");
            TouchStrips.SimulatePress(2); yield return null; yield return Idle();
            AssertAt(W, CornerCell(a) + WalkLattice.Dir(h), "strip 2 forward");
            TouchStrips.SimulatePress(3); yield return null; yield return Idle();
            AssertAt(W, CornerCell(a), "strip 3 back");
        }

        // ---------------- auto-walk and follow ----------------

        [UnityTest]
        public IEnumerator AutoWalk_GoesRoundBlockedCells_ToTheGoal()
        {
            yield return Load();
            var camp = new CampLayout(S.Board);
            var corner = WalkLattice.ToCell(camp.Hearth);
            int h = 0;
            var from = corner - WalkLattice.Dir(h) * 2;
            var to = corner + WalkLattice.Dir(h) * 2; // straight line crosses the storehouse
            W.Teleport(from, h);
            yield return null;
            var path = WalkGrid.FindPath(from, to);
            Assert.IsNotNull(path, "a path exists");
            Assert.IsFalse(path.Contains(corner), "the path avoids the storehouse cell");
            foreach (var c in path) Assert.IsFalse(WalkGrid.IsBlocked(c), "path cell " + c + " is free");
            bool? arrived = null;
            W.AutoWalkEnded += ok => arrived = ok;
            var visited = new System.Collections.Generic.List<Cell>();
            W.Stepped += () => visited.Add(W.Cell);
            Assert.IsTrue(W.WalkTo(to));
            yield return Idle(20f);
            AssertAt(W, to, "auto-walk end");
            Assert.AreEqual(true, arrived, "AutoWalkEnded(true)");
            Assert.AreEqual(path.Count, visited.Count, "one step per path cell");
            Assert.IsFalse(visited.Contains(corner), "never stepped on the storehouse");
            Assert.IsNull(WalkGrid.FindPath(from, corner), "no path into a blocked cell");
        }

        [UnityTest]
        public IEnumerator Follow_EndsTwoCellsBehindAWalkingVillager_AndTheTapOffersFollow()
        {
            yield return Load(pause: false);
            FreeEdge(out int a, out _, out int h);
            var start = CornerCell(a) + WalkLattice.Dir(h);
            var npc = Npc.Spawn("test_walker", "Test walker", "Walks for the test.", "", Color.cyan, WalkLattice.ToWorld(start), WalkLattice.Yaw(h));
            W.Teleport(start - WalkLattice.Dir(h) * 3 + WalkLattice.Dir(h + 1), h);
            yield return null;
            var goal = start + WalkLattice.Dir(h) * 8;
            npc.WalkTo(WalkLattice.ToWorld(goal));
            yield return null;
            Assert.IsTrue(npc.IsWalking);
            // Tap the walking villager: its bubble offers "Follow" as a reply.
            Assert.AreEqual(TapOutcome.Interact, PointerInput.TapObject(npc));
            Assert.IsTrue(DialoguePanel.IsOpen, "the villager's bubble opened");
            var replies = SpeechBubbles.RepliesOf(DialoguePanel.Handle);
            int follow = -1;
            for (int i = 0; i < replies.Count; i++) if (replies[i] == DialoguePanel.FollowLabel) follow = i;
            Assert.GreaterOrEqual(follow, 0, "Follow offered for a walking villager");
            Assert.IsTrue(SpeechBubbles.Choose(DialoguePanel.Handle, follow));
            Assert.AreSame(npc, W.Following as Npc, "following the villager");
            float end = Time.realtimeSinceStartup + 25f;
            while ((npc.IsWalking || W.IsGliding || W.Cell != goal - WalkLattice.Dir(h) * PlayerWalker.FollowCells) && Time.realtimeSinceStartup < end) yield return null;
            AssertAt(W, goal - WalkLattice.Dir(h) * PlayerWalker.FollowCells, "two cells behind the villager");
            Assert.IsTrue(W.IsAutoWalking, "still following (until a move key or right click)");
            W.StopAuto();
            Assert.IsNull(W.Following, "StopAuto ends the follow");
            UnityEngine.Object.Destroy(npc.gameObject);
        }

        // ---------------- pointer and bubbles ----------------

        [UnityTest]
        public IEnumerator Pointer_HitsAnInspectable_AndATapShowsWhatItIs()
        {
            yield return Load();
            var mira = Npc.Find(NpcIds.Mira);
            Assert.IsNotNull(mira);
            yield return null;
            var cam = Boot.Views.ActiveCamera;
            var sp = cam.WorldToScreenPoint(mira.transform.position + Vector3.up * 1.1f);
            Assert.Greater(sp.z, 0, "Mira in front of the spawn view");
            var hit = PointerInput.Raycast(sp);
            Assert.AreSame(mira, hit.Target, "the pointer hits Mira");
            // A tap on the ground (a hex tile) inspects it with a small world-anchored label.
            var ground = W.Position + W.transform.forward * 6f + W.transform.right * 2.5f;
            var gsp = cam.WorldToScreenPoint(ground);
            var g = PointerInput.Raycast(gsp);
            Assert.IsTrue(g.HasGround, "ground under the pointer");
            Assert.IsInstanceOf<HexView>(g.Target, "the ground is a hex tile");
            Assert.AreEqual(TapOutcome.Inspect, PointerInput.TapAt(gsp));
            Assert.AreNotEqual(0, SpeechBubbles.InspectHandle, "inspect label open");
            StringAssert.Contains("hex", SpeechBubbles.TitleOf(SpeechBubbles.InspectHandle));
            // Tapping Mira talks: her bubble with the duel reply.
            Assert.AreEqual(TapOutcome.Interact, PointerInput.TapAt(sp));
            Assert.IsTrue(DialoguePanel.IsOpen);
            Assert.AreEqual("Practice duel", SpeechBubbles.RepliesOf(DialoguePanel.Handle)[0]);
            DialoguePanel.Close();
        }

        [UnityTest]
        public IEnumerator Bubbles_ReplyByTap_ByKeyPath_Queue_AndTimeout()
        {
            yield return Load();
            var mira = Npc.Find(NpcIds.Mira);
            int got = -2;
            int say = SpeechBubbles.Say(mira, "First this.", 30f);
            int ask = SpeechBubbles.Ask(mira, "Which one?", new[] { "Left", "Right", "Neither" }, i => got = i, 0f);
            Assert.IsTrue(SpeechBubbles.IsShowing(say));
            Assert.IsFalse(SpeechBubbles.IsShowing(ask), "the Ask waits in Mira's queue");
            Assert.IsFalse(SpeechBubbles.Choose(ask, 0), "a queued Ask cannot be answered");
            SpeechBubbles.Close(say);
            Assert.IsTrue(SpeechBubbles.IsShowing(ask), "the Ask shows when the Say closes");
            SpeechBubbles.Expand(ask);
            yield return null; yield return null;
            var r = SpeechBubbles.ReplyRectOf(ask, 1);
            Assert.Greater(r.width, 0, "reply 2 drawn under the bubble");
            Assert.AreEqual(TapOutcome.Bubble, PointerInput.TapAt(new Vector2(r.center.x, Screen.height - r.center.y)));
            Assert.AreEqual(1, got, "tapping reply 2 answers 1");
            Assert.IsFalse(SpeechBubbles.IsOpen(ask), "answered bubbles close");
            // Keys 1-3 answer the nearest open Ask (same path as the key handler).
            got = -2;
            int ask2 = SpeechBubbles.Ask(mira, "Again?", new[] { "Yes", "No" }, i => got = i, 0f);
            Assert.IsTrue(SpeechBubbles.AnswerNearest(0));
            Assert.AreEqual(0, got);
            // Timeout answers -1.
            got = -2;
            SpeechBubbles.Ask(mira, "Quick!", new[] { "Now" }, i => got = i, 0.3f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(-1, got, "timeout replies -1");
            Assert.IsFalse(SpeechBubbles.IsOpen(ask2));
        }

        // ---------------- casting ----------------

        private IEnumerator OpeningProposal(Action<Proposal> got)
        {
            Proposal p = null;
            for (int i = 0; i < 200 && p == null; i++)
            {
                p = FactionApi.Top(S, PlayerIds.Human);
                if (p == null) Boot.Clock.StepOnce();
            }
            Assert.IsNotNull(p, "the opening proposal appeared");
            Assert.AreEqual(ProposalKind.Building, p.Kind);
            Assert.AreEqual((int)BuildingType.WoodcutterHut, p.BuildingType);
            Assert.AreEqual(631, p.Plot, "the opening proposal is plot 631");
            got(p);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cast_OutsideRangeRefusesWithASentence_InsideRangeCastsPlot631()
        {
            yield return Load();
            Proposal p = null;
            yield return OpeningProposal(x => p = x);
            Assert.IsTrue(FactionApi.ProposalTarget(S, p.Id, out double tx, out double tz, out _));
            var target = new Vector3((float)tx, 0, (float)tz);
            double range = FactionApi.CastRangeMetres(S);

            // Out of range: 4 steps beyond the cast range on a free cell.
            var dir = (target - W.Position); dir.y = 0; dir.Normalize();
            var far = WalkGrid.NearestFree(WalkLattice.ToCell(target - dir * ((float)range + 10f)), 3).Value;
            W.Teleport(far, W.Heading);
            yield return null;
            Assert.Greater(SpellCaster.DistanceTo(p.Id), range, "standing out of range");
            int pending = Boot.Sim.PendingCommands.Count;
            Assert.AreEqual(TapOutcome.CastRefused, PointerInput.TapGround(target));
            Assert.AreEqual(SpellCaster.TooFar, SpellCaster.LastResult.Reason);
            Assert.AreEqual("Walk closer to cast.", SpellCaster.LastResult.Message);
            Assert.AreEqual(pending, Boot.Sim.PendingCommands.Count, "no command when out of range");
            bool said = false;
            foreach (int hnd in SpeechBubbles.OpenHandles()) if (SpeechBubbles.TextOf(hnd) == "Walk closer to cast.") said = true;
            Assert.IsTrue(said, "the refusal is said in a bubble");

            // In range: the spawn is about 13 m from plot 631.
            var camp = new CampLayout(S.Board);
            W.Teleport(camp.Spawn, camp.SpawnHeading);
            yield return null;
            Assert.LessOrEqual(SpellCaster.DistanceTo(p.Id), range, "spawn within cast range of plot 631");
            int played = SpellEffect.Played;
            int castEvents = 0;
            Boot.Clock.Ticked += evs => { foreach (var e in evs) if (e is SpellCast sc && sc.ProposalId == p.Id) castEvents++; };
            Assert.AreEqual(TapOutcome.Cast, PointerInput.TapGround(target));
            Assert.IsTrue(SpellCaster.LastResult.Ok, SpellCaster.LastResult.Message);
            Assert.AreEqual(played + 1, SpellEffect.Played, "the spell effect plays");
            Assert.IsInstanceOf<CastSpell>(Boot.Sim.PendingCommands[Boot.Sim.PendingCommands.Count - 1], "CastSpell submitted");
            Boot.Clock.StepOnce();
            Assert.AreEqual(1, castEvents, "SpellCast event");
            var plot = EconomyApi.Plot(S, 631);
            Assert.GreaterOrEqual(plot.BuildingId, 0, "a site on plot 631");
            Assert.AreEqual(BuildingType.WoodcutterHut, S.Economy.Buildings[plot.BuildingId].Type);
            Assert.IsNull(FactionApi.GetProposal(S, p.Id), "the proposal is gone");
            yield return null;
            Assert.Greater(SpellEffect.Active, 0, "the effect is visible");
            // Pressing ground with no proposal does nothing.
            Assert.AreNotEqual(TapOutcome.Cast, PointerInput.TapGround(W.Position + Vector3.right * 2f));
        }

        // ---------------- map, HUD, F1 ----------------

        [UnityTest]
        public IEnumerator MapAndHud_OpenAndClose_WithNoStateChangeAndNoCommands_MapTargetWalksOnClose()
        {
            yield return Load();
            for (int i = 0; i < 120; i++) Boot.Clock.StepOnce();
            string before = SaveCodec.StateJson(S);
            int pending = Boot.Sim.PendingCommands.Count;
            Assert.IsFalse(Hud.Visible, "HUD off at the start");
            Hud.Visible = true;
            yield return null; yield return null;
            Assert.Greater(Hud.StripLines(S).Count, 1, "HUD strip has content");
            StringAssert.Contains("Strength", string.Join("\n", Hud.StripLines(S)));
            Hud.Visible = false;
            MapScreen.Open();
            Assert.IsTrue(MapScreen.IsOpen);
            yield return null; yield return null;
            var view = MapScreenView.Instance;
            Assert.Greater(view.MapRect.width, 100f, "the map is drawn");
            // Tap the map two hex edges ahead of the player: a walk target, no teleport.
            var camp = new CampLayout(S.Board);
            var goal = camp.Spawn + WalkLattice.Dir(camp.SpawnHeading) * 3;
            var gui = view.WorldToGui(WalkLattice.ToWorld(goal));
            var pos = W.Position;
            Assert.IsTrue(view.TapScreen(new Vector2(gui.x, Screen.height - gui.y)), "tap on the map");
            Assert.IsTrue(MapScreen.Target.HasValue);
            Assert.LessOrEqual(WalkGrid.Distance(MapScreen.Target.Value, goal), 1, "target where tapped");
            yield return null;
            Assert.Less((W.Position - pos).magnitude, 1e-4f, "no teleport and no walking while the map is open");
            Assert.AreEqual(before, SaveCodec.StateJson(S), "map and HUD change no state");
            Assert.AreEqual(pending, Boot.Sim.PendingCommands.Count, "map and HUD submit no command");
            var target = MapScreen.Target.Value;
            MapScreen.Close();
            Assert.IsFalse(MapScreen.IsOpen);
            Assert.IsTrue(W.IsAutoWalking, "closing the map starts the auto-walk");
            yield return Idle(20f);
            AssertAt(W, target, "walked to the map target");
        }

        [UnityTest]
        public IEnumerator F1_ShowsTheDeveloperTools_AndADevBuildPlacesASite()
        {
            yield return Load();
            Assert.AreEqual(ViewMode.FirstPerson, Boot.Views.CurrentView);
            Boot.Views.Toggle(); // F1
            yield return null;
            Assert.AreEqual(ViewMode.God, Boot.Views.CurrentView);
            Assert.IsTrue(W.HasLock(ViewSwitcher.GodLock), "the walker holds still in the God View");
            Assert.IsNotNull(GodToolbar.Instance);
            foreach (var id in new[] { "select", "build", "road", "camp", "city", "banish", "cast", "problems", "wares", "time" })
                Assert.IsNotNull(GodToolbar.Find(id), "tool " + id);
            GodToolbar.Instance.Activate("build");
            yield return null; yield return null;
            Assert.Greater(GodOverlay.VisibleTotal, 0, "build overlay shows plots");
            // A direct build command: a Sawmill on a free plot of the home camp.
            var home = EconomyApi.SettlementAt(S, S.Board.StartCorners[0]);
            int free = -1;
            foreach (int pl in EconomyApi.PlotsOf(S, home.Id))
                if (EconomyApi.CheckBuilding(S, PlayerIds.Human, BuildingType.Sawmill, pl).Ok) { free = pl; break; }
            Assert.GreaterOrEqual(free, 0, "a free plot for a Sawmill");
            Assert.IsTrue(DevTools.PlaceBuilding(BuildingType.Sawmill, free).Ok, DevTools.LastResult.Message);
            Boot.Clock.StepOnce();
            var pv = EconomyApi.Plot(S, free);
            Assert.AreEqual(PlotUse.Site, pv.Use, "the site is placed");
            // Roll now steps to the next roll.
            int rolls = S.Economy.Dice.RollCount;
            int sum = DevTools.ForceRoll();
            Assert.AreEqual(rolls + 1, S.Economy.Dice.RollCount, "one more roll");
            Assert.IsTrue(sum >= 2 && sum <= 12);
            // Cast from the tools (no range check): the first open proposal of either people.
            for (int i = 0; i < 200 && FactionApi.Top(S, PlayerIds.Human) == null && FactionApi.Top(S, PlayerIds.Rival) == null; i++) Boot.Clock.StepOnce();
            int faction = FactionApi.Top(S, PlayerIds.Human) != null ? PlayerIds.Human : PlayerIds.Rival;
            Assert.IsNotNull(FactionApi.Top(S, faction), "an open proposal");
            Assert.IsTrue(DevTools.CastQueued(faction, 0).Ok, DevTools.LastResult.Message);
            DevTools.SetTimeScale(2f);
            Assert.AreEqual(2f, Time.timeScale);
            DevTools.SetTimeScale(1f);
            Boot.Views.Toggle();
            yield return null;
            Assert.AreEqual(ViewMode.FirstPerson, Boot.Views.CurrentView);
            Assert.IsFalse(W.IsLocked, "the walker is free again");
        }

        // ---------------- encounters ----------------

        [UnityTest]
        public IEnumerator Encounter_EnterAndLeave_RestoresTheLatticePose()
        {
            yield return Load();
            var camp = new CampLayout(S.Board);
            var cell = camp.Spawn + WalkLattice.Dir(camp.SpawnHeading);
            W.Teleport(cell, (camp.SpawnHeading + 2) % 6);
            yield return null;
            var enc = new DebugEncounter();
            Assert.IsTrue(EncounterHost.Instance.Start(enc));
            Assert.IsTrue(W.IsLocked, "locked during the encounter");
            EncounterHost.Instance.Context.PlaceAvatar(W.Position + new Vector3(3.3f, 1f, -2.2f), 17f, -4f);
            Assert.IsTrue(W.IsOffLattice);
            Assert.AreEqual(17f, W.Yaw, 0.01f);
            W.Forward(); // ignored while locked
            yield return null;
            Assert.IsFalse(W.IsGliding, "no moves during the encounter");
            enc.Press(); enc.Press(); enc.Press();
            yield return null; yield return null;
            Assert.IsFalse(EncounterHost.Instance.IsRunning);
            AssertAt(W, cell, "back on the cell");
            Assert.AreEqual((camp.SpawnHeading + 2) % 6, W.Heading, "heading restored");
            Assert.AreEqual(WalkLattice.Yaw(W.Heading), Mathf.Repeat(W.Yaw, 360f), 0.01f, "yaw restored");
            Assert.AreEqual(PlayerWalker.FixedPitch, W.Pitch, 0.01f, "pitch restored");
            Assert.IsFalse(W.IsOffLattice);
            Assert.IsFalse(W.IsLocked, "free to move");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MirasDuel_StartToFinish_WithThePointer()
        {
            yield return Load(pause: false);
            var mira = Npc.Find(NpcIds.Mira);
            WardDuelEncounter.TuningOverride = new World.Sim.Encounters.Ward.WardTuning
            {
                PlayerRecoveryMs = 150, MiraFirstCastMinMs = 6000, MiraFirstCastMaxMs = 6000, MiraThinkMinMs = 6000, MiraThinkMaxMs = 6000,
            };
            try
            {
                // Walk up to her on the lattice, tap her, tap "Practice duel" in her bubble.
                bool walked = false;
                var s = new AutoPlayScript("test_to_mira").WalkToObject(() => mira).Call(() => walked = true);
                AutoPlay.Instance.Run(s);
                float until = Time.realtimeSinceStartup + 20f;
                while (!walked && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(walked, "walked up to Mira: " + AutoPlay.Instance.FailMessage);
                var cell = W.Cell; int heading = W.Heading;
                var cam = Boot.Views.ActiveCamera;
                var sp = cam.WorldToScreenPoint(mira.transform.position + Vector3.up * 1.0f);
                Assert.AreEqual(TapOutcome.Interact, PointerInput.TapAt(sp), "tap on Mira");
                yield return null; yield return null;
                var r = SpeechBubbles.ReplyRectOf(DialoguePanel.Handle, 0);
                Assert.Greater(r.width, 0, "the duel reply is drawn");
                Assert.AreEqual(TapOutcome.Bubble, PointerInput.TapAt(new Vector2(r.center.x, Screen.height - r.center.y)));
                var e = WardDuelEncounter.Current;
                Assert.IsNotNull(e, "the duel started from the bubble");
                Assert.IsTrue(W.IsOffLattice, "the duel placed the viewpoint");
                // Play it out (the key path), then return.
                foreach (var x in new[] { World.Sim.Encounters.Ward.Essence.Flame, World.Sim.Encounters.Ward.Essence.Flame, World.Sim.Encounters.Ward.Essence.Vine }) e.AddEssence(x);
                Assert.IsTrue(e.LockWard());
                var solver = new World.Sim.Encounters.Ward.WardRival(new World.Sim.Encounters.Ward.WardTuning { Loci = e.Tuning.Loci }, e.Pool, new Rng(5));
                int observed = 0;
                until = Time.realtimeSinceStartup + 60f;
                while (e.Phase == WardDuelEncounter.Screen.Active && Time.realtimeSinceStartup < until)
                {
                    var hist = e.Session.PlayerHistory;
                    while (observed < hist.Count) { var x = hist[observed++]; solver.Observe(x.Weave, x.Fractures, x.Echoes); }
                    if (e.Session.MsUntilPlayerMayCast == 0 && e.Session.PlayerCastsLeft > 0)
                    {
                        foreach (var w in solver.NextCast()) e.AddEssence((World.Sim.Encounters.Ward.Essence)w);
                        Assert.IsTrue(e.Cast().Ok);
                    }
                    yield return null;
                }
                Assert.AreEqual(WardDuelEncounter.Screen.Result, e.Phase, "the duel finished");
                Assert.IsTrue(e.Return());
                yield return null; yield return null;
                Assert.IsFalse(EncounterHost.Instance.IsRunning);
                AssertAt(W, cell, "back on the lattice cell after the duel");
                Assert.AreEqual(heading, W.Heading);
                Assert.IsFalse(W.IsLocked);
                Assert.AreEqual(1, S.Ward.CompletedDuelsVsMira, "the duel is recorded");
            }
            finally { WardDuelEncounter.TuningOverride = null; }
        }

        // ---------------- scripts and rules ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator WalkTour_AutoPlay_Passes()
        {
            yield return Load(pause: false);
            Assert.IsTrue(AutoPlayScripts.TryCreate("walk_tour", out var script));
            bool? ok = null;
            AutoPlay.Instance.Finished += (s, pass) => ok = pass;
            AutoPlay.Instance.Run(script);
            float until = Time.realtimeSinceStartup + 100f;
            while (ok == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(ok == true, "walk_tour: " + (AutoPlay.Instance.FailMessage ?? "timed out"));
            Debug.Log("[PlayerTests] camp to the neighbouring corner: " + AutoPlayScripts.LastCornerWalkSeconds.ToString("0.00") + " s");
            Assert.Less(AutoPlayScripts.LastCornerWalkSeconds, 6f, "the neighbouring corner within 6 s");
        }

        [Test]
        public void NoWorldStateWrites_InPackagePFiles()
        {
            string root = Path.Combine(Application.dataPath, "World", "Game");
            var files = new System.Collections.Generic.List<string>();
            files.AddRange(Directory.GetFiles(Path.Combine(root, "Player"), "*.cs", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(root, "UI"), "*.cs", SearchOption.AllDirectories));
            files.Add(Path.Combine(root, "Boot", "AutoPlay.cs"));
            files.Add(Path.Combine(root, "Boot", "GameClock.cs"));
            files.Add(Path.Combine(root, "Boot", "WorldBootstrap.cs"));
            // An assignment or increment on anything reached through the sim state.
            var write = new Regex(@"\b(State|s|S|state)\.(Economy|Factions|Demons|Board|Players|Ward|Tick|Rng|Seed)\b[\w\.\[\]\(\)]*\s*(\+\+|--|(?<![=!<>])[+\-*/]?=(?!=))");
            foreach (var f in files)
            {
                int n = 0;
                foreach (var line in File.ReadAllLines(f))
                {
                    n++;
                    var code = line.Trim();
                    if (code.StartsWith("//") || code.StartsWith("///")) continue;
                    Assert.IsFalse(write.IsMatch(code), Path.GetFileName(f) + ":" + n + " writes WorldState: " + code);
                }
            }
        }
    }
}
