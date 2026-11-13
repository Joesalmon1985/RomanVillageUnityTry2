using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Encounters.Rhythm;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game.Tests
{
    /// <summary>
    /// Package D (REFACTOR_SPEC 8.3): the drums light for the right pads at cue time minus the fall time under a perfect
    /// autoplayer; the ending choice works only inside its window; the camp dressing keeps off plots, stock patches,
    /// local paths, hex-edge lines and the corner; nothing of the old village remains; the fire and drums block the
    /// walking lattice; the "duel_dance" AutoPlay script passes (Mira's duel, then the dance).
    /// </summary>
    public class DanceTests : EncounterTestBase
    {
        CampLayout Camp => new CampLayout(Boot.Sim.State.Board);

        IEnumerator StartDance()
        {
            var sol = Npc.Find(NpcIds.Host);
            var front = Camp.DanceCircleCentre + Camp.Forward * 3.2f + Camp.Right * 1.2f;
            yield return Accept(NpcIds.Host, front, BoardSpace.YawOf(sol.transform.position - front));
            Assert.IsNotNull(RhythmPartyEncounter.Current, "the dance started");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Drums_LightForTheRightPads_AtCueMinusFall_AutoplayAllPerfect()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartDance();
            var r = RhythmPartyEncounter.Current;
            var circle = r.Circle;
            r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;
            double fall = RhythmPartyEncounter.FallSeconds;
            int framesChecked = 0, litFrames = 0, emberFrames = 0;
            var sawLit = new bool[5];
            while (r.Phase != RhythmPartyEncounter.Screen.Result)
            {
                yield return null;
                if (r.Phase == RhythmPartyEncounter.Screen.Result || r.Session == null) break;
                // Every frame: a drum is lit exactly when one of its own cues is inside [time - fall, judged) or held.
                double t = r.SongTime;
                var expect = new bool[5];
                foreach (var c in r.Session.UpcomingCues())
                {
                    if (c.State == CueState.Done) continue;
                    if (c.State == CueState.Holding || t >= c.Time - fall) expect[c.Pad] = true;
                }
                for (int p = 1; p <= 4; p++)
                {
                    Assert.AreEqual(expect[p], circle.IsLit(p), "drum " + p + " lit state at song time " + t.ToString("0.000"));
                    if (expect[p]) { litFrames++; sawLit[p] = true; }
                }
                if (circle.EmbersThisFrame > 0) emberFrames++;
                framesChecked++;
            }
            Assert.Greater(framesChecked, 300, "frames checked");
            Assert.Greater(litFrames, 0); Assert.Greater(emberFrames, 0, "embers fly to the drums");
            var used = new bool[5];
            foreach (var c in r.Session.AllCues) used[c.Pad] = true;
            for (int p = 1; p <= 4; p++) Assert.AreEqual(used[p], sawLit[p], "drum " + p + " lit at least once if and only if the chart uses it");

            // Each cue lit its own drum within one frame of cue time minus the fall time.
            int n = 0;
            foreach (var c in r.Session.AllCues)
            {
                Assert.IsTrue(r.LitAt.TryGetValue(c.Index, out var lit), "cue " + c.Index + " lit its drum");
                Assert.AreEqual(c.Pad, lit.Pad, "cue " + c.Index + " lit the right drum");
                double late = lit.SongTime - (c.Time - fall);
                Assert.GreaterOrEqual(late, -1e-9, "cue " + c.Index + " not lit early");
                Assert.Less(late, 0.1, "cue " + c.Index + " lit within a frame of cue time - fall time");
                n++;
            }
            Assert.GreaterOrEqual(n, 16, "cues in the phrase (four charted blocks)");
            var res = r.LastResult;
            Assert.AreEqual(0, res.Great + res.Ok + res.Miss + res.HoldPartial, "all perfect");
            Assert.AreEqual(4, res.PassedBlocks);
            Assert.AreEqual(n - res.HoldComplete, res.Perfect, "every cue judged perfect (holds also complete)");
        }

        [Test]
        public void Approach_IsZeroAtCueMinusFall_OneAtCue()
        {
            Assert.AreEqual(0.0, DanceCircle.Approach(10.0, 10.0 - 1.1, 1.1), 1e-9);
            Assert.AreEqual(1.0, DanceCircle.Approach(10.0, 10.0, 1.1), 1e-9);
            Assert.Less(DanceCircle.Approach(10.0, 8.8, 1.1), 0.0, "not yet visible before the lead");
            Assert.LessOrEqual(RhythmPartyEncounter.FallSeconds, 1.2f, "RHYTHM_API: fall time at most 1.2 s");
            Assert.AreEqual("Fire leap", RhythmPartyEncounter.ChoiceName(SimRoute.Bar));
            Assert.AreEqual("Spirit call", RhythmPartyEncounter.ChoiceName(SimRoute.Balcony));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator EndingChoice_OnlyInsideTheWindow()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartDance();
            var r = RhythmPartyEncounter.Current;
            r.AutoPlayEnabled = true;
            yield return Until(() => r.SongTime > r.Session.BeatToSeconds(20), 20f, "phrase did not reach beat 20");
            Assert.IsFalse(r.ChooseRoute(SimRoute.Balcony), "refused before beat 48");
            Assert.IsFalse(r.Circle.ChoiceVisible, "no choice marks before the window");
            yield return Until(() => r.Session.RouteWindow == RouteWindowState.Open, 15f, "window did not open");
            yield return null;
            Assert.IsTrue(r.Circle.ChoiceVisible, "choice marks by the fire");
            Assert.IsTrue(r.Circle.ChoiceFireLeap, "Fire leap is the default");
            Assert.IsTrue(r.ChooseRoute(SimRoute.Balcony), "Spirit call accepted in the window");
            yield return null;
            Assert.IsFalse(r.Circle.ChoiceFireLeap, "the spirit mark is picked");
            Assert.IsTrue(r.ChooseRoute(SimRoute.Bar), "can change the mind");
            Assert.IsTrue(r.ChooseRoute(SimRoute.Balcony));
            yield return Until(() => r.SongTime > r.Session.BeatToSeconds(60.6), 10f, "phrase did not pass beat 60.5");
            Assert.IsFalse(r.ChooseRoute(SimRoute.Bar), "refused after beat 60.5");
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 10f, "phrase did not end");
            Assert.AreEqual(RouteOutcome.Balcony, r.LastRouteOpened);
            Assert.AreEqual(DanceCircle.Effect.SpiritCall, r.Circle.LastEffect);
            Assert.IsFalse(r.ChooseRoute(SimRoute.Bar), "refused after the end");
        }

        [UnityTest]
        public IEnumerator CampLayout_DressingKeepsOffPlotsPathsRoadsAndPatches()
        {
            yield return Load();
            var board = Boot.Sim.State.Board;
            var camp = Camp;
            Assert.AreEqual(635, camp.ReservedPlot, "the circle is on plot 635 (default board)");
            Assert.Greater(camp.Dressing.Count, 8, "tents, lean-to, racks, totems, wood pile, figures");
            foreach (var p in camp.Dressing)
                Assert.IsNull(CampLayout.Obstruction(board, p.Position, p.Radius), p.Kind + " at " + p.Position + ": " + CampLayout.Obstruction(board, p.Position, p.Radius));

            // The built props, measured from their meshes, stay inside their footprint and off everything.
            var dressing = CampInstaller.Root.Find("Dressing");
            Assert.IsNotNull(dressing);
            int built = 0;
            foreach (Transform child in dressing)
            {
                var b = Bounds(child);
                var c = b.center; c.y = 0;
                float radius = Mathf.Max(b.extents.x, b.extents.z);
                Assert.IsNull(CampLayout.Obstruction(board, c, radius), child.name + ": " + CampLayout.Obstruction(board, c, radius));
                built++;
            }
            Assert.AreEqual(camp.Dressing.Count, built, "one object per prop");

            // The circle's parts stay inside the reserved plot's 7 m circle and off every other plot and path.
            var circle = DanceCircle.Current.transform;
            foreach (var r in circle.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Sparks") continue;
                var b = r.bounds; var c = b.center; c.y = 0;
                float radius = Mathf.Max(b.extents.x, b.extents.z);
                if (r.name == "Circle") radius = (float)PlotLayout.DanceCircleRadius - 0.01f; // the ground disc is the circle itself
                Assert.IsNull(CampLayout.Obstruction(board, c, Mathf.Min(radius, (float)PlotLayout.DanceCircleRadius), true), r.name + ": " + CampLayout.Obstruction(board, c, radius, true));
            }
            Assert.IsNull(CampLayout.Obstruction(board, camp.MiraSpot, 0.5f), "Mira's spot: " + CampLayout.Obstruction(board, camp.MiraSpot, 0.5f));
            Assert.Less(BoardSpace.Flat(Npc.Find(NpcIds.Mira).transform.position - camp.MiraSpot), 0.01f, "Mira at her spot");
            Assert.Less(BoardSpace.Flat(Npc.Find(NpcIds.Host).transform.position - camp.DrummerSpot), 0.01f, "Sol at the drummer's spot");

            // Spawn and the prince's start are lattice cells 20 m apart; the spawn is 4 steps along the Forest spoke.
            Assert.AreEqual(20f, BoardSpace.Flat(WalkLattice.ToWorld(camp.Spawn) - WalkLattice.ToWorld(camp.PrinceStart)), 0.01f);
            Assert.AreEqual(20f, BoardSpace.Flat(WalkLattice.ToWorld(camp.Spawn) - camp.Hearth), 0.01f);
            Assert.AreEqual(camp.SpawnHeading, WalkLattice.HeadingOf(BoardSpace.YawOf(WalkLattice.ToWorld(camp.PrinceStart) - WalkLattice.ToWorld(camp.Spawn))));
            Assert.AreEqual(6f, BoardSpace.Flat(camp.DancerSpot - camp.DanceCircleCentre), 0.01f, "dancer 6 m from the fire");
            for (int p = 1; p <= 4; p++) Assert.AreEqual(4f, BoardSpace.Flat(camp.DrumPosition(p) - camp.DanceCircleCentre), 0.01f, "drum " + p + " on the 4 m circle");
            // Drums 1-4 left to right as seen from the dancer.
            for (int p = 1; p < 4; p++)
                Assert.Less(Vector3.Dot(camp.DrumPosition(p) - camp.DancerSpot, camp.Right), Vector3.Dot(camp.DrumPosition(p + 1) - camp.DancerSpot, camp.Right), "drum order");
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [UnityTest]
        public IEnumerator NoVillageLeft_AndTheFireBlocksTheLattice()
        {
            yield return Load();
            string[] gone = { "warehouse", "cottage", "market", "bell", "square" };
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = t.name.ToLowerInvariant();
                foreach (var g in gone) Assert.IsFalse(n.Contains(g), "object '" + t.name + "' named like the old village");
            }
            var camp = Camp;
            Assert.IsTrue(WalkGrid.IsBlocked(WalkLattice.ToCell(camp.DanceCircleCentre)), "the fire blocks its cell");
            // The cell 5 m from the fire on the way in (between drums 2 and 3) stays free.
            var approach = WalkLattice.ToCell(camp.DanceCircleCentre - camp.Forward * 5f);
            Assert.Less(BoardSpace.Flat(WalkLattice.ToWorld(approach) - (camp.DanceCircleCentre - camp.Forward * 5f)), 0.01f, "the approach is a lattice cell");
            Assert.IsFalse(WalkGrid.IsBlocked(approach), "the way in stays walkable");
            // The drums block the fire's neighbour cells they stand next to (drums 1 and 4, 1.2 m from those cells).
            var fire = WalkLattice.ToCell(camp.DanceCircleCentre);
            int blocked = 0;
            for (int h = 0; h < 6; h++) if (WalkGrid.IsBlocked(fire + WalkLattice.Dir(h))) blocked++;
            Assert.AreEqual(2, blocked, "two of the fire's six neighbour cells are drum cells");
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator DuelDance_AutoPlay_Passes()
        {
            yield return Load();
            var ap = AutoPlay.Instance;
            Assert.IsTrue(AutoPlayScripts.TryCreate(DuelDance.Name, out var script), "duel_dance registered");
            Assert.IsFalse(AutoPlayScripts.TryCreate("first_five", out _), "first_five renamed");
            bool? ok = null;
            ap.Finished += (s, pass) => ok = pass;
            ap.Run(script);
            float until = Time.realtimeSinceStartup + 380f;
            while (ok == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(ok == true, "duel_dance: " + (ap.FailMessage ?? "timed out"));
            Assert.Greater(Boot.Sim.State.Ward.CompletedDuelsVsMira, 0, "Mira's duel completed");
        }
    }
}
