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
            Assert.AreEqual(n, res.Perfect, "every cue (tap or hold head) judged perfect");
            int holds = 0; foreach (var c in r.Session.AllCues) if (c.IsHold) holds++;
            Assert.AreEqual(holds, res.HoldComplete, "every hold held to its end");
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

        // ------------------------------------------------------------------ WU-5: rhythm, world first

        static Vector2 Scr(Vector3 world) { var s = Camera.main.WorldToScreenPoint(world); return new Vector2(s.x, s.y); }

        static Chart ChartOf(params (int tick, int dur, int pad)[] cues)
        {
            var c = new Chart { Id = "wu5", Bpm = 174, PhraseBeats = 32 };
            var t = new TierCues();
            foreach (var q in cues) t.Drums.Add(new ChartCue { Tick = q.tick, DurTicks = q.dur, Pad = q.pad, Family = q.pad == 1 ? "kick" : "snare" });
            c.Tiers["1"] = t;
            return c;
        }

        [Test]
        public void PressWindows_EdgesJudgedByTheTuning_EarlyAndLate()
        {
            // Press at cue time +- a millisecond inside / outside each window edge (45, 90, 140 ms).
            var cases = new (double ms, JudgementKind kind)[]
            {
                (0, JudgementKind.Perfect), (44, JudgementKind.Perfect), (-44, JudgementKind.Perfect),
                (46, JudgementKind.Great), (-46, JudgementKind.Great), (89, JudgementKind.Great), (-89, JudgementKind.Great),
                (91, JudgementKind.Ok), (-91, JudgementKind.Ok), (139, JudgementKind.Ok), (-139, JudgementKind.Ok),
                (141, JudgementKind.Stray), (-141, JudgementKind.Stray),
            };
            foreach (var cs in cases)
            {
                var s = new PhraseSession(new RhythmTuning(), ChartOf((1920, 0, 1)));
                var cue = s.AllCues[0];
                var j = s.Press(1, cue.Time + cs.ms / 1000.0);
                Assert.IsNotNull(j, "press at " + cs.ms + " ms judged");
                Assert.AreEqual(cs.kind, j.Kind, "press " + cs.ms + " ms from the cue");
            }
        }

        [Test]
        public void TwoPadsAtOnce_BothJudged_AndAHoldSurvivesATapOnAnotherPad()
        {
            var s = new PhraseSession(new RhythmTuning(), ChartOf((1920, 0, 1), (1920, 0, 4)));
            double t = s.AllCues[0].Time;
            Assert.AreEqual(t, s.AllCues[1].Time, 1e-9, "two cues on one tick");
            var a = s.Press(1, t); var b = s.Press(4, t);
            Assert.AreEqual(JudgementKind.Perfect, a.Kind); Assert.AreEqual(JudgementKind.Perfect, b.Kind);
            Assert.AreNotEqual(a.CueIndex, b.CueIndex, "each pad judged its own cue");

            // A hold on pad 2 and a tap on pad 1 while it is held.
            var h = new PhraseSession(new RhythmTuning(), ChartOf((1920, 3840, 2), (3840, 0, 1)));
            var hold = h.AllCues[0]; var tap = h.AllCues[1];
            Assert.IsTrue(hold.IsHold);
            Assert.AreEqual(JudgementKind.Perfect, h.Press(2, hold.Time).Kind);
            Assert.AreEqual(JudgementKind.Perfect, h.Press(1, tap.Time).Kind, "a tap while another pad is held");
            h.Release(2, hold.EndTime); h.Advance(hold.EndTime + 0.3);
            Assert.IsTrue(HasJudgement(h, JudgementKind.HoldComplete), "released at the end: the hold completes");

            var early = new PhraseSession(new RhythmTuning(), ChartOf((1920, 3840, 2)));
            var c2 = early.AllCues[0];
            early.Press(2, c2.Time);
            early.Release(2, c2.Time + 0.5); early.Advance(c2.EndTime + 0.3);
            Assert.IsTrue(HasJudgement(early, JudgementKind.HoldPartial), "let go early: a partial hold");
        }

        static bool HasJudgement(PhraseSession s, JudgementKind k)
        {
            foreach (var e in s.DrainEvents()) if (e is JudgementEvent je && je.Judgement.Kind == k) return true;
            return false;
        }

        [Test]
        public void Calibration_ShiftsTheJudgement_AsCalibrationMsSays()
        {
            var plain = new PhraseSession(new RhythmTuning(), ChartOf((1920, 0, 1)));
            var late = new PhraseSession(new RhythmTuning { CalibrationMs = 60 }, ChartOf((1920, 0, 1)));
            var early = new PhraseSession(new RhythmTuning { CalibrationMs = -40 }, ChartOf((1920, 0, 1)));
            double t = plain.AllCues[0].Time;
            Assert.AreEqual(JudgementKind.Great, plain.Press(1, t + 0.060).Kind, "60 ms late is Great without calibration");
            Assert.AreEqual(JudgementKind.Perfect, late.Press(1, t + 0.060).Kind, "CalibrationMs = +60 makes a 60 ms late press Perfect");
            Assert.AreEqual(JudgementKind.Perfect, early.Press(1, t - 0.040).Kind, "CalibrationMs = -40 makes a 40 ms early press Perfect");
            Assert.AreEqual(30.0, TapAlongCalibration_Offset(new[] { 0.03, 0.03, 0.03, 0.03, 0.03, 0.03, 0.03, 0.03 }), 1e-9, "tap-along: eight taps 30 ms late = +30 ms");
        }

        static double TapAlongCalibration_Offset(double[] offsets)
        {
            var c = new TapAlongCalibration(10.0);
            for (int i = 0; i < offsets.Length; i++) c.Tap(c.ClickTimes[i] + offsets[i]);
            Assert.IsTrue(c.TryResult(out var ms));
            return ms;
        }

        [Test]
        public void TapAlong_MedianOfOffsets_NeedsFourTaps_ClampedAndSigned()
        {
            var c = new TapAlongCalibration(5.0);
            c.Tap(c.ClickTimes[0] + 0.02); c.Tap(c.ClickTimes[1] + 0.02); c.Tap(c.ClickTimes[2] - 0.01);
            Assert.IsFalse(c.TryResult(out _), "three taps are not enough");
            Assert.IsFalse(c.Tap(c.ClickTimes[7] + 0.4), "a tap far from every click is ignored");
            c.Tap(c.ClickTimes[3] + 0.05);
            Assert.IsTrue(c.TryResult(out var ms));
            Assert.AreEqual(20.0, ms, 1e-9, "median of +20, +20, -10, +50 = +20 ms");
            Assert.AreEqual("+20 ms", RhythmPartyEncounter.SignedMs(ms)); Assert.AreEqual("-35 ms", RhythmPartyEncounter.SignedMs(-35));
            var big = new TapAlongCalibration(1.0);
            for (int i = 0; i < 6; i++) big.Tap(big.ClickTimes[i] + 0.24);
            Assert.IsTrue(big.TryResult(out var m2)); Assert.LessOrEqual(m2, 250.0);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator RealInputs_StripTap_DrumShapeTap_AndKeyPath_AllJudgeTheSame_AndTwoPadsAtOnce()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartDance();
            var r = RhythmPartyEncounter.Current;
            Assert.IsNotNull(TouchStrips.ShapeResolver, "the dance registers the drums' shapes");
            yield return null;
            // Every drum's projected shape answers with its own pad, anywhere on screen, and never smaller than the strip rule.
            for (int p = 1; p <= 4; p++)
                Assert.AreEqual(p, TouchStrips.PadAt(Scr(r.Circle.DrumTop(p))), "a tap on drum " + p + "'s shape presses pad " + p);
            Assert.GreaterOrEqual(DanceStageProps.MinHitRadius * 2f, Screen.height * 0.07f, "hit targets at least 7 % of the screen height");
            var high = new Vector2(Screen.width * 0.9f, Screen.height * 0.9f);
            Assert.AreEqual(0, TouchStrips.PadAt(high), "empty air presses nothing");
            Assert.AreEqual(4, TouchStrips.PadAt(new Vector2(Screen.width * 0.9f, 10f)), "a tap on pad 4's strip still counts");

            var c0 = r.Session.AllCues[0]; var c1 = r.Session.AllCues[1]; var c2 = r.Session.AllCues[2];
            Assert.AreEqual(1, c0.Pad); Assert.AreEqual(4, c1.Pad); Assert.AreEqual(1, c2.Pad);
            // cue 0 by its strip
            yield return Until(() => r.SongTime >= c0.Time - 0.016, 10f, "cue 0 not reached");
            TouchStrips.SimulatePress(1);
            yield return Until(() => c0.State == CueState.Done, 2f, "cue 0 not judged by the strip");
            Assert.Greater(c0.Credit, 0.5, "strip press judged a hit");
            // cue 1 by the drum's shape (above the strip band)
            var shape4 = Scr(r.Circle.DrumTop(4));
            Assert.AreEqual(0, TouchStrips.StripAt(shape4), "drum 4's top is above the touch band");
            yield return Until(() => r.SongTime >= c1.Time - 0.016, 10f, "cue 1 not reached");
            TouchStrips.SimulateTouchBegin(7, shape4);
            yield return Until(() => c1.State == CueState.Done, 2f, "cue 1 not judged by the drum shape");
            Assert.Greater(c1.Credit, 0.5, "shape tap judged a hit");
            yield return null;
            Assert.IsTrue(TouchStrips.Held(4), "a finger resting on the drum's shape holds the pad");
            TouchStrips.SimulateTouchEnd(7);
            yield return null; yield return null;
            Assert.IsFalse(TouchStrips.Held(4), "lifting the finger releases the pad");
            // cue 2 by the key path (HandlePress is what D F J K call)
            yield return Until(() => r.SongTime >= c2.Time - 0.016, 10f, "cue 2 not reached");
            var j = r.HandlePress(1, r.SongTime);
            Assert.IsNotNull(j); Assert.AreEqual(c2.Index, j.CueIndex);
            Assert.AreEqual(3, r.PressesHandled, "three presses handled, one per path");
            Assert.AreEqual(0, r.TallyMiss, "nothing missed so far");
            Assert.GreaterOrEqual(r.TallyPerfect + r.TallyGreat, 3, "all three hit");

            // two fingers at once on two empty-air strips: two presses in one frame (strays: no cue is due)
            yield return Until(() => r.SongTime >= c2.Time + 0.5 && r.SongTime < r.Session.AllCues[3].Time - 0.35, 10f, "quiet gap");
            int before = r.PressesHandled;
            TouchStrips.SimulateTouchBegin(11, new Vector2(Screen.width * 0.1f, 10f));
            TouchStrips.SimulateTouchBegin(12, new Vector2(Screen.width * 0.6f, 10f));
            yield return null; yield return null;
            Assert.AreEqual(before + 2, r.PressesHandled, "two pads pressed at once, both handled");
            Assert.AreEqual(2, r.TallyStray, "both were strays (no cue due), counted");
            TouchStrips.SimulateTouchEnd(11); TouchStrips.SimulateTouchEnd(12);
            yield return null;
            Assert.AreEqual(0, r.TallyMiss, "strays cost nothing");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Autoplay_PerformersFollowTheCueFamily_TorchesLight_TallyEqualsTheSession()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartDance();
            var r = RhythmPartyEncounter.Current;
            var props = r.Props;
            Assert.IsNotNull(props);
            r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;
            r.AutoSkipBlock = b => b == 1; // leave block 1 alone: misses, a missed torch
            double fall = RhythmPartyEncounter.FallSeconds;
            var seen = new HashSet<DanceStageProps.Pose>();
            bool sawCurrent1 = false, sawKept0Before1 = false;
            while (r.Phase != RhythmPartyEncounter.Screen.Result)
            {
                yield return null;
                if (r.Phase == RhythmPartyEncounter.Screen.Result || r.Session == null) break;
                double t = r.SongTime;
                for (int p = 1; p <= 4; p++)
                {
                    string fam = null; double best = double.MaxValue;
                    foreach (var c in r.Session.UpcomingCues())
                    {
                        if (c.Pad != p || c.State == CueState.Done) continue;
                        if (c.State != CueState.Holding && t < c.Time - fall) continue;
                        if (c.Time < best) { best = c.Time; fam = c.Family; }
                    }
                    var want = DanceStageProps.PoseForFamily(fam);
                    Assert.AreEqual(want, props.PoseOf(p), "performer " + p + " pose at song time " + t.ToString("0.000"));
                    seen.Add(props.PoseOf(p));
                }
                double beat = r.Session.SecondsToBeat(t);
                if (beat > 13 && beat < 16) { Assert.AreEqual(DanceStageProps.TorchState.Kept, props.TorchStateOf(0), "block 1 kept: torch 1 gold"); Assert.AreEqual(DanceStageProps.TorchState.Unlit, props.TorchStateOf(1)); sawKept0Before1 = true; }
                if (beat > 17 && beat < 24) { Assert.AreEqual(DanceStageProps.TorchState.Current, props.TorchStateOf(1), "block 2 is playing: torch 2 flickers"); sawCurrent1 = true; }
                if (beat < 0) for (int i = 0; i < 4; i++) Assert.AreEqual(DanceStageProps.TorchState.Unlit, props.TorchStateOf(i), "count-in: all torches dark");
            }
            Assert.IsTrue(sawKept0Before1 && sawCurrent1, "torches checked mid-phrase");
            Assert.IsTrue(seen.Contains(DanceStageProps.Pose.Kick) && seen.Contains(DanceStageProps.Pose.Snare) && seen.Contains(DanceStageProps.Pose.Idle), "kick, snare and rest poses all shown");
            var res = r.LastResult;
            yield return null;
            Assert.AreEqual(DanceStageProps.TorchState.Kept, props.TorchStateOf(0));
            Assert.AreEqual(DanceStageProps.TorchState.Missed, props.TorchStateOf(1), "the skipped block's torch is a dull ember");
            for (int i = 2; i < 4; i++) Assert.AreEqual(res.PassedBlocks >= 3 ? DanceStageProps.TorchState.Kept : props.TorchStateOf(i), props.TorchStateOf(i));
            Assert.Greater(props.Strikes, 0, "performers struck their drums");
            // The tally is the session's own count.
            Assert.AreEqual(res.Perfect, r.TallyPerfect); Assert.AreEqual(res.Great, r.TallyGreat); Assert.AreEqual(res.Ok, r.TallyOk);
            Assert.AreEqual(res.Miss, r.TallyMiss); Assert.AreEqual(res.Stray, r.TallyStray);
            Assert.AreEqual(res.HoldComplete, r.TallyHoldComplete); Assert.AreEqual(res.HoldPartial, r.TallyHoldPartial);
            Assert.Greater(res.Miss, 0, "block 1 left alone: misses");

            // Sol's tally on request: tap Sol, the card shows the exact counts.
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Sol, r.TapAt(Scr(props.SolPoint)), "a tap on Sol");
            Assert.AreEqual(RhythmPartyEncounter.Card.Tally, r.OpenCard);
            string card = r.CardText;
            StringAssert.Contains("Perfect " + res.Perfect, card); StringAssert.Contains("Great " + res.Great, card);
            StringAssert.Contains("OK " + res.Ok, card); StringAssert.Contains("Miss " + res.Miss, card);
            StringAssert.Contains("Holds kept " + res.HoldComplete + " of " + (res.HoldComplete + res.HoldPartial), card);
            StringAssert.Contains("Blocks kept " + res.PassedBlocks + " of 4", card);
            yield return null;
            Assert.IsTrue(LedgerCard.IsOpen, "Sol's tally is on the shared ledger card");
            string all = string.Join(" / ", LedgerCard.Content.AllText());
            StringAssert.Contains("Perfect " + res.Perfect, all); StringAssert.Contains("Miss " + res.Miss, all);
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Nothing, r.TapAt(high), "tapping the ground closes it");
            yield return null;
            Assert.IsFalse(LedgerCard.IsOpen, "closed");
            Assert.AreEqual(RhythmPartyEncounter.Card.None, r.OpenCard);
            // The torches answer with the track, tempo, block and stage.
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Torch, r.TapAt(Scr(props.TorchPoint(2))));
            StringAssert.Contains("174 BPM", r.CardText);
            StringAssert.Contains(r.PackId, r.CardText);
            r.TapAt(high);
        }

        static IEnumerator WaitSeconds(float sec) { float end = Time.realtimeSinceStartup + sec; while (Time.realtimeSinceStartup < end) yield return null; }

        Vector2 high => new Vector2(Screen.width * 0.9f, Screen.height * 0.9f);

        [UnityTest, Timeout(120000)]
        public IEnumerator MidSongPause_FreezesTime_ResumesAfterFourCount_ScoringExact()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartDance();
            var r = RhythmPartyEncounter.Current;
            r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;
            yield return Until(() => r.SongTime > r.Session.BeatToSeconds(20), 30f, "phrase did not reach beat 20");
            Assert.IsTrue(r.Pause(), "pause accepted mid-phrase");
            double P = r.SongTime;
            int judged = r.CuesJudged, presses = r.PressesHandled, misses = r.TallyMiss;
            float end = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(RhythmPartyEncounter.PauseStage.Paused, r.PauseState);
            Assert.AreEqual(P, r.SongTime, 1e-9, "song time frozen while paused");
            Assert.AreEqual(P, r.Player.SongTime, 1e-9, "the audio clock's song time frozen too");
            Assert.AreEqual(judged, r.CuesJudged, "nothing judged while paused"); Assert.AreEqual(misses, r.TallyMiss, "no misses while paused");
            if (AudioGuard.CanWriteControls) Assert.IsFalse(r.Player.AnyPlaying, "the music stopped");
            // A press on a strip while paused is ignored; mid-phrase the bell refuses to tune.
            TouchStrips.SimulatePress(2); yield return null; yield return null;
            Assert.AreEqual(presses, r.PressesHandled, "pads ignored while paused");
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Bell, r.TapAt(Scr(r.Props.BellPoint)));
            Assert.IsFalse(r.TuneActive, "calibration only between phrases");
            Assert.AreEqual(RhythmPartyEncounter.PauseStage.Paused, r.PauseState);
            // Strike a drum's shape: resume with a count of four.
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Drum, r.TapAt(Scr(r.Circle.DrumTop(2))));
            Assert.AreEqual(RhythmPartyEncounter.PauseStage.Counting, r.PauseState);
            var counts = new List<int>();
            float limit = Time.realtimeSinceStartup + 4f;
            while (r.PauseState == RhythmPartyEncounter.PauseStage.Counting && Time.realtimeSinceStartup < limit)
            {
                if (counts.Count == 0 || counts[counts.Count - 1] != r.ResumeCount) counts.Add(r.ResumeCount);
                Assert.AreEqual(P, r.SongTime, 1e-9, "song time holds during the four-count");
                yield return null;
            }
            CollectionAssert.AreEqual(new[] { 4, 3, 2, 1 }, counts, "a count of four");
            Assert.AreEqual(RhythmPartyEncounter.PauseStage.None, r.PauseState);
            yield return null;
            Assert.GreaterOrEqual(r.SongTime, P); Assert.Less(r.SongTime - P, 0.2, "the song goes on from where it stopped");
            if (AudioGuard.CanWriteControls && !r.Player.UsingRealtimeFallback)
            {
                yield return null; yield return null;
                float len = r.Stems.Drums.length;
                double want = ((r.SongTime % len) + len) % len;
                double got = r.Player.Drums.time;
                double diff = Math.Abs(want - got); diff = Math.Min(diff, len - diff);
                Assert.Less(diff, 0.08, "the drums play where song time says after the resume (diff " + diff.ToString("0.000") + " s)");
            }
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "phrase did not end after the pause");
            var res = r.LastResult;
            string odd = "";
            foreach (var c in r.Session.AllCues) if (c.Credit < 0.999) odd += " [cue " + c.Index + " pad" + c.Pad + " t=" + c.Time.ToString("0.000") + " end=" + c.EndTime.ToString("0.000") + " hold=" + c.IsHold + " credit=" + c.Credit.ToString("0.00") + "]";
            Assert.AreEqual(0, res.Great + res.Ok + res.Miss + res.HoldPartial, odd + " judgement exact: the pause cost nothing (P" + res.Perfect + " G" + res.Great + " O" + res.Ok + " M" + res.Miss + " HP" + res.HoldPartial + " stray" + res.Stray + ", paused at beat " + r.Session.SecondsToBeat(P).ToString("0.00") + ")");
            Assert.AreEqual(4, res.PassedBlocks); Assert.AreEqual(1, r.Pauses);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LogSeat_PausesThenLeavesWithConfirm_AndStopsTheAudio()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartDance();
            var r = RhythmPartyEncounter.Current;
            var player = r.Player;
            yield return Until(() => r.SongTime > 1.0, 10f, "song did not start");
            if (AudioGuard.CanWriteControls && !player.UsingRealtimeFallback) Assert.IsTrue(player.AnyPlaying, "music playing");
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Seat, r.TapAt(Scr(r.Props.SeatPoint)), "tap on the log seat");
            Assert.IsTrue(r.IsPaused, "the first tap during a phrase pauses");
            Assert.IsFalse(r.IsFinished);
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(RhythmPartyEncounter.TapKind.Seat, r.TapAt(Scr(r.Props.SeatPoint)));
            Assert.IsTrue(r.LeaveArmed, "the next tap asks to leave (preview)"); Assert.IsFalse(r.IsFinished, "not yet");
            yield return WaitSeconds(0.3f);
            Assert.IsTrue(LedgerCard.IsOpen && LedgerCard.Mode == LedgerMode.Preview, "the shared ledger asks: Leave the dance?");
            StringAssert.Contains("Leave the dance?", LedgerCard.Content.Question);
            // Anything else cancels the preview.
            r.TapAt(high);
            Assert.IsFalse(r.LeaveArmed, "a tap elsewhere cancels");
            yield return WaitSeconds(0.7f);
            r.TapAt(Scr(r.Props.SeatPoint)); Assert.IsTrue(r.LeaveArmed);
            yield return WaitSeconds(0.3f);
            r.TapAt(Scr(r.Props.SeatPoint));
            Assert.IsTrue(r.IsFinished && r.Left, "second tap confirms the exit");
            Assert.IsFalse(player.AnyPlaying, "audio stopped at once");
            yield return Until(() => !EncounterHost.Instance.IsRunning, 2f, "dance did not end");
            yield return null;
            Assert.IsFalse(LedgerCard.IsOpen, "the ledger is closed with the dance");
            Assert.IsTrue(player == null, "the audio objects are gone");
            Assert.IsNull(TouchStrips.ShapeResolver, "the drums' shapes are unregistered after the dance");
            Assert.AreEqual(0, DanceCircle.Current.ActiveDancers);
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator TuningBell_TapAlong_SavesTheOffset_SurvivesAReload_AndChangesJudgement()
        {
            bool had = PlayerPrefs.HasKey(RhythmPartyEncounter.CalibrationPref);
            float old = PlayerPrefs.GetFloat(RhythmPartyEncounter.CalibrationPref, 0f);
            PlayerPrefs.SetFloat(RhythmPartyEncounter.CalibrationPref, 0f);
            try
            {
                yield return Load();
                yield return StartDance();
                var r = RhythmPartyEncounter.Current;
                Assert.AreEqual(0.0, r.Tuning.CalibrationMs, 1e-9, "no offset saved yet");
                r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;
                yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 60f, "phrase did not end");
                // The bell is for between phrases: tap it, eight clicks follow, tap along 35 ms late.
                Assert.AreEqual(RhythmPartyEncounter.TapKind.Bell, r.TapAt(Scr(r.Props.BellPoint)));
                Assert.IsTrue(r.TuneActive);
                var clicks = r.TuneClickTimes;
                Assert.AreEqual(8, clicks.Length);
                for (int i = 0; i < clicks.Length; i++) Assert.IsTrue(r.TuneTap(clicks[i] + 0.035), "tap " + i + " matched its click");
                Assert.AreEqual(8, r.TuneMatched);
                yield return Until(() => !r.TuneActive, 12f, "tap-along did not finish");
                Assert.AreEqual(35.0, r.Tuning.CalibrationMs, 1e-9, "median offset in ms");
                Assert.AreEqual(35.0, r.LastTuneMs, 1e-9);
                Assert.AreEqual(35f, PlayerPrefs.GetFloat(RhythmPartyEncounter.CalibrationPref, -1f), 1e-4f, "saved");
                Assert.IsTrue(r.Return());
                yield return Until(() => !EncounterHost.Instance.IsRunning, 2f, "dance did not end");
                yield return null;

                // A reload: a new dance reads the saved offset, and judges by it (CalibrationMs = +35: a press 35 ms late is Perfect).
                yield return StartDance();
                var r2 = RhythmPartyEncounter.Current;
                Assert.AreNotSame(r, r2);
                Assert.AreEqual(35.0, r2.Tuning.CalibrationMs, 1e-9, "the offset survived");
                var c0 = r2.Session.AllCues[0];
                var j = r2.HandlePress(c0.Pad, c0.Time + 0.035);
                Assert.AreEqual(JudgementKind.Perfect, j.Kind, "35 ms late judged Perfect with the saved offset");
                Assert.AreEqual(0.0, j.DeltaMs, 1.0);
            }
            finally
            {
                if (had) PlayerPrefs.SetFloat(RhythmPartyEncounter.CalibrationPref, old); else PlayerPrefs.DeleteKey(RhythmPartyEncounter.CalibrationPref);
            }
        }
    }
}
