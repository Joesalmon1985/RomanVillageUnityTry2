using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Encounters.Rhythm;

namespace World.Sim.Tests
{
    public class RhythmTests
    {
        // ---------- helpers ----------

        private static Chart OneCueChart(string family, int tick, int dur, int pad)
        {
            var c = new Chart { Id = "t", Bpm = 174, PhraseBeats = 32 };
            var t = new TierCues();
            var list = family == "drums" ? t.Drums : t.Bass;
            list.Add(new ChartCue { Tick = tick, DurTicks = dur, Pad = pad, Family = family });
            c.Tiers["1"] = t;
            return c;
        }

        private struct Act { public double T; public bool Press; public int Pad; }

        /// <summary>Presses and releases exactly on every charted cue as it becomes known; skip lets a block be left alone.</summary>
        private static PhraseResult Autoplay(PhraseSession s, Func<int, bool> skipBlock = null, Route? choose = null)
        {
            pending.Clear();
            var done = new HashSet<int>();
            double t = -s.Tuning.LeadInSeconds;
            for (int guard = 0; guard < 100000 && !s.IsEnded; guard++)
            {
                s.Advance(t);
                if (choose.HasValue && t >= s.BeatToSeconds(50)) s.ChooseRoute(choose.Value, t);
                var acts = new List<Act>();
                foreach (var c in s.AllCues)
                {
                    if (done.Contains(c.Index) || (skipBlock != null && skipBlock(c.Block))) continue;
                    acts.Add(new Act { T = c.Time, Press = true, Pad = c.Pad });
                    if (c.IsHold) acts.Add(new Act { T = c.EndTime, Press = false, Pad = c.Pad });
                    done.Add(c.Index);
                }
                // play every charted action up to the next block boundary, in time order (release before press on ties)
                pending.AddRange(acts);
                pending.Sort((a, b) => a.T != b.T ? a.T.CompareTo(b.T) : a.Press.CompareTo(b.Press));
                double horizon = t + 0.5;
                while (pending.Count > 0 && pending[0].T <= horizon)
                {
                    var a = pending[0]; pending.RemoveAt(0);
                    if (a.Press) s.Press(a.Pad, a.T); else s.Release(a.Pad, a.T);
                }
                t = horizon;
            }
            pending.Clear();
            return s.Result;
        }

        [ThreadStatic] private static List<Act> pendingStore;
        private static List<Act> pending { get { return pendingStore ?? (pendingStore = new List<Act>()); } }

        // ---------- PadJudge boundaries ----------

        [TestCase(44, JudgementKind.Perfect)]
        [TestCase(-44, JudgementKind.Perfect)]
        [TestCase(46, JudgementKind.Great)]
        [TestCase(-89, JudgementKind.Great)]
        [TestCase(91, JudgementKind.Ok)]
        [TestCase(-139, JudgementKind.Ok)]
        [TestCase(0, JudgementKind.Perfect)]
        [TestCase(-141, JudgementKind.Stray)]
        public void Judge_Boundaries(int ms, JudgementKind expected)
        {
            var j = new PadJudge(new RhythmTuning());
            j.AddCue(new RhythmCue { Pad = 1, Time = 2.0, EndTime = 2.0, Block = 0 });
            var r = j.Press(1, 2.0 + ms / 1000.0);
            Assert.AreEqual(expected, r.Kind);
        }

        [Test]
        public void Judge_StrayCostsNothing_AndMissResolvesAtWindowEnd()
        {
            var j = new PadJudge(new RhythmTuning());
            j.AddCue(new RhythmCue { Pad = 1, Time = 2.0, EndTime = 2.0 });
            j.Press(1, 1.0);
            Assert.AreEqual(1, j.Strays);
            j.Advance(2.2);
            Assert.AreEqual(1, j.Misses);
            Assert.AreEqual(2.14, j.Cues[0].ResolvedAt, 1e-9);
            Assert.AreEqual(0.0, j.Cues[0].Credit);
        }

        [Test]
        public void Judge_NearestPendingCueWins_TieGoesToEarlier()
        {
            var j = new PadJudge(new RhythmTuning());
            j.AddCue(new RhythmCue { Pad = 1, Time = 1.0, EndTime = 1.0 });
            j.AddCue(new RhythmCue { Pad = 1, Time = 1.1, EndTime = 1.1 });
            j.Press(1, 1.07);
            Assert.AreEqual(CueState.Pending, j.Cues[0].State);
            Assert.AreEqual(CueState.Done, j.Cues[1].State);
            var k = new PadJudge(new RhythmTuning());
            k.AddCue(new RhythmCue { Pad = 1, Time = 1.0, EndTime = 1.0 });
            k.AddCue(new RhythmCue { Pad = 1, Time = 1.1, EndTime = 1.1 });
            k.Press(1, 1.05);
            Assert.AreEqual(CueState.Done, k.Cues[0].State);
        }

        // ---------- holds ----------

        [TestCase(1.86, 1.0, JudgementKind.HoldComplete)]
        [TestCase(1.84, 0.84, JudgementKind.HoldPartial)]
        [TestCase(1.5, 0.5, JudgementKind.HoldPartial)]
        [TestCase(1.1, 0.1, JudgementKind.HoldPartial)]
        public void Hold_Credit(double release, double credit, JudgementKind kind)
        {
            var j = new PadJudge(new RhythmTuning());
            j.AddCue(new RhythmCue { Pad = 2, Time = 1.0, EndTime = 2.0, IsHold = true });
            j.Press(2, 1.0);
            Assert.AreEqual(0.0, j.Cues[0].Credit, "no credit at press");
            var r = j.Release(2, release);
            Assert.AreEqual(kind, r.Kind);
            Assert.AreEqual(credit, j.Cues[0].Credit, 1e-9);
            Assert.AreEqual(credit, r.Credit, 1e-9);
        }

        [Test]
        public void Hold_PressedLate_UsesCueOnsetForCoverage()
        {
            var j = new PadJudge(new RhythmTuning());
            j.AddCue(new RhythmCue { Pad = 2, Time = 1.0, EndTime = 2.0, IsHold = true });
            Assert.AreEqual(JudgementKind.Ok, j.Press(2, 1.1).Kind);
            j.Release(2, 1.5);
            Assert.AreEqual(0.5, j.Cues[0].Credit, 1e-9);
        }

        [Test]
        public void Hold_StillHeldAtTail_Completes()
        {
            var j = new PadJudge(new RhythmTuning());
            j.AddCue(new RhythmCue { Pad = 2, Time = 1.0, EndTime = 2.0, IsHold = true });
            j.Press(2, 1.0);
            j.Advance(2.5);
            Assert.AreEqual(1.0, j.Cues[0].Credit);
            Assert.AreEqual(1, j.HoldsComplete);
        }

        // ---------- chart / parser ----------

        [Test]
        public void Pack_ParsesRealFile()
        {
            var c = PackJson.Parse(RhythmPackData.Lv1Warehouse);
            Assert.AreEqual("lv1_warehouse", c.Id);
            Assert.AreEqual(174, c.Bpm);
            Assert.AreEqual(32, c.PhraseBeats);
            Assert.AreEqual(529655, c.LoopFrames);
            Assert.AreEqual(3, c.Stems.Count);
            CollectionAssert.AreEquivalent(new[] { "1", "2", "3", "4" }, c.Tiers.Keys);
            Assert.AreEqual(24, c.Tiers["1"].Drums.Count);
            Assert.AreEqual(4, c.Tiers["1"].Bass.Count);
            Assert.AreEqual(1440, c.Tiers["1"].Drums[1].Tick);
            Assert.AreEqual("snare", c.Tiers["1"].Drums[1].Family);
            Assert.AreEqual(7680, c.Tiers["1"].Bass[0].DurTicks);
        }

        [Test]
        public void Fallback_MatchesPackTier1()
        {
            var p = PackJson.Parse(RhythmPackData.Lv1Warehouse).Tiers["1"];
            var f = FallbackChart.Create().Tiers["1"];
            Assert.AreEqual(p.Drums.Count, f.Drums.Count);
            Assert.AreEqual(p.Bass.Count, f.Bass.Count);
            for (int i = 0; i < p.Drums.Count; i++)
            {
                Assert.AreEqual(p.Drums[i].Tick, f.Drums[i].Tick);
                Assert.AreEqual(p.Drums[i].Pad, f.Drums[i].Pad);
            }
            for (int i = 0; i < p.Bass.Count; i++)
            {
                Assert.AreEqual(p.Bass[i].Tick, f.Bass[i].Tick);
                Assert.AreEqual(p.Bass[i].DurTicks, f.Bass[i].DurTicks);
            }
        }

        [Test]
        public void Tuning_Defaults()
        {
            var t = new RhythmTuning();
            Assert.AreEqual(174, t.Bpm);
            Assert.AreEqual(0.6, t.PassCredit);
            Assert.AreEqual(0, t.CalibrationMs);
            Assert.AreEqual(4, t.Stages.Count);
            Assert.AreEqual(0.85, t.Stages[2].Gains.Bass);
            Assert.AreEqual("bass", t.Stages[3].Cue);
        }

        // ---------- session: cues ----------

        [Test]
        public void Session_CueTimesAndClipping()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            var first = s.AllCues[0];
            Assert.AreEqual(0.0, first.Time);
            var snare = s.AllCues.First(c => c.Tick == 1440);
            Assert.AreEqual(1440 * 60.0 / (174 * 960), snare.Time, 1e-12);
            foreach (var c in s.AllCues) Assert.LessOrEqual(c.Tick, 12 * 960);
            // beats 12-16 carry no cues: the cue at tick 12480 (beat 13) is not charted in block 0
            Assert.IsFalse(s.AllCues.Any(c => c.Tick == 12480));
        }

        [Test]
        public void Session_HoldClippedToBeat12_AndShortHoldBecomesTap()
        {
            var tuning = new RhythmTuning();
            // bass-stage block at pos 0: hold at beat 8.5 of 7000 ticks clips to 12 beats
            var s = new PhraseSession(tuning, FallbackChart.Create());
            var ok = Autoplay(s);
            Assert.IsTrue(ok.Success);
            var bassHold = s.AllCues.First(c => c.Block == 1 && c.Tick == 16 * 960 + 8160);
            Assert.AreEqual(3360, bassHold.DurTicks);
            // a hold starting at beat 11.75 with 960 ticks left of span clips to 240 -> tap
            var s2 = new PhraseSession(tuning, OneCueChart("drums", 11 * 960 + 720, 5000, 1));
            Assert.IsFalse(s2.AllCues[0].IsHold);
            Assert.AreEqual(0, s2.AllCues[0].DurTicks);
        }

        // ---------- session: blocks ----------

        [Test]
        public void Session_AllPass_StagesAndBassReveal()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            var r = Autoplay(s);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4).Select(s.StageOfBlock).ToArray());
            Assert.AreEqual(16, s.BassRevealBeat);
            Assert.AreEqual(4, r.PassedBlocks);
        }

        [Test]
        public void Session_MissThenThreePasses_StagesFamiliesAndReveal()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            var r = Autoplay(s, b => b == 0);
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 2 }, Enumerable.Range(0, 4).Select(s.StageOfBlock).ToArray());
            Assert.AreEqual(32, s.BassRevealBeat);
            CollectionAssert.AreEqual(new[] { "drums", "drums", "bass", "drums" }, Enumerable.Range(0, 4).Select(s.FamilyOfBlock).ToArray());
            Assert.AreEqual(3, r.PassedBlocks);
            Assert.IsTrue(r.FinalBlockPassed);
            Assert.IsTrue(r.Success);
            Assert.IsFalse(s.BlockResultOf(0).Passed);
        }

        [Test]
        public void Session_BassBlock_UsesRevealOriginForCues()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Autoplay(s, b => b == 0);
            // block 2 is bass revealed at beat 32: loop position 0, so the first bass cue (tick 0) lands at beat 32
            var first = s.AllCues.First(c => c.Block == 2);
            Assert.AreEqual(32 * 960, first.Tick);
            Assert.AreEqual(2, first.Pad);
            // block 3 is drums at beat 48: position 16
            var d = s.AllCues.First(c => c.Block == 3);
            Assert.AreEqual(48 * 960, d.Tick);
        }

        [Test]
        public void Session_NextBlockCuesAppearAtDecision()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Assert.IsFalse(s.AllCues.Any(c => c.Block == 1));
            s.Advance(s.BeatToSeconds(12.4));
            Assert.IsFalse(s.AllCues.Any(c => c.Block == 1));
            s.Advance(s.BeatToSeconds(12.5));
            Assert.IsTrue(s.AllCues.Any(c => c.Block == 1));
            Assert.IsTrue(s.UpcomingCues().Any(c => c.Block == 1));
        }

        [Test]
        public void Session_CreditExactlySixtyPercentPasses_JustUnderFails()
        {
            // one tap (hit, 1.0) and one hold: mean 0.6 needs hold credit 0.2
            foreach (var tc in new[] { new { hold = 0.2, pass = true }, new { hold = 0.18, pass = false } })
            {
                var chart = new Chart { Id = "t", Bpm = 174, PhraseBeats = 32 };
                var t = new TierCues();
                t.Drums.Add(new ChartCue { Tick = 960, DurTicks = 0, Pad = 1, Family = "kick" });
                t.Drums.Add(new ChartCue { Tick = 2 * 960, DurTicks = 4 * 960, Pad = 4, Family = "snare" });
                chart.Tiers["1"] = t;
                var s = new PhraseSession(new RhythmTuning(), chart);
                s.Press(1, s.BeatToSeconds(1));
                double start = s.BeatToSeconds(2);
                s.Press(4, start);
                s.Release(4, start + tc.hold * s.BeatToSeconds(4));
                s.Advance(s.BeatToSeconds(13));
                Assert.AreEqual(tc.pass, s.BlockResultOf(0).Passed, "hold credit " + tc.hold);
                Assert.AreEqual(0.5 + tc.hold / 2, s.BlockResultOf(0).Credit, 1e-9);
            }
        }

        [Test]
        public void Session_BlockWithoutCues_CreditOne()
        {
            var chart = new Chart { Id = "t", Bpm = 174, PhraseBeats = 32 };
            chart.Tiers["1"] = new TierCues();
            var s = new PhraseSession(new RhythmTuning(), chart);
            s.Advance(s.BeatToSeconds(12.5));
            Assert.AreEqual(1.0, s.BlockResultOf(0).Credit);
            Assert.IsTrue(s.BlockResultOf(0).Passed);
        }

        [Test]
        public void Session_GainsFollowStage()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Assert.AreEqual(0.0, s.CurrentGains.Bass);
            Autoplay(s);
            // ended during block 3: bass strongest
            Assert.AreEqual(1.0, s.CurrentGains.Bass);
            Assert.AreEqual(0.4, s.CurrentGains.Drums);
            Assert.AreEqual(0.5, s.CurrentGains.Other);
        }

        // ---------- session: route ----------

        [Test]
        public void Route_WindowOpensAt48_LocksAt60_5_AndChoiceCommits()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Assert.IsFalse(s.ChooseRoute(Route.Balcony, s.BeatToSeconds(47.9)));
            Assert.AreEqual(RouteWindowState.NotYet, s.RouteWindow);
            Assert.IsTrue(s.ChooseRoute(Route.Balcony, s.BeatToSeconds(48.0)));
            Assert.AreEqual(RouteWindowState.Open, s.RouteWindow);
            Assert.IsTrue(s.ChooseRoute(Route.Bar, s.BeatToSeconds(55)));
            Assert.AreEqual(Route.Bar, s.CurrentRoute);
            Assert.IsTrue(s.ChooseRoute(Route.Balcony, s.BeatToSeconds(60.4)));
            Assert.IsFalse(s.ChooseRoute(Route.Bar, s.BeatToSeconds(60.5)));
            Assert.AreEqual(RouteWindowState.Locked, s.RouteWindow);
            Assert.AreEqual(Route.Balcony, s.CurrentRoute);
            var ev = s.DrainEvents();
            Assert.AreEqual(1, ev.OfType<RouteWindowOpened>().Count());
            var lk = ev.OfType<RouteLocked>().Single();
            Assert.AreEqual(Route.Balcony, lk.Route);
            Assert.AreEqual(s.BeatToSeconds(60.5), lk.SongTime, 1e-12);
        }

        [Test]
        public void Route_NoChoice_IsBar_AndBalconyOpens()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Assert.AreEqual(RouteOutcome.Bar, Autoplay(s).RouteOpened);

            var s2 = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Assert.AreEqual(RouteOutcome.Balcony, Autoplay(s2, null, Route.Balcony).RouteOpened);
        }

        [Test]
        public void Route_FailedFinalBlock_NoRouteAndRetry()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            var r = Autoplay(s, b => b == 3, Route.Balcony);
            Assert.AreEqual(3, r.PassedBlocks);
            Assert.IsFalse(r.FinalBlockPassed);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(RouteOutcome.None, r.RouteOpened);
            Assert.IsTrue(r.CanRetry);
        }

        [Test]
        public void Route_TwoPasses_NotSuccess()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            var r = Autoplay(s, b => b == 0 || b == 1);
            Assert.AreEqual(2, r.PassedBlocks);
            Assert.IsTrue(r.FinalBlockPassed);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(RouteOutcome.None, r.RouteOpened);
        }

        [Test]
        public void Route_ThreePassesWithPassingFinal_Success()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            var r = Autoplay(s, b => b == 1);
            Assert.AreEqual(3, r.PassedBlocks);
            Assert.IsTrue(r.Success);
        }

        // ---------- autoplayers ----------

        private static void AssertPerfectFullSuccess(Chart chart)
        {
            var s = new PhraseSession(new RhythmTuning(), chart);
            var r = Autoplay(s);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(4, r.PassedBlocks);
            Assert.AreEqual(RouteOutcome.Bar, r.RouteOpened);
            Assert.AreEqual(0, r.Great);
            Assert.AreEqual(0, r.Ok);
            Assert.AreEqual(0, r.Miss);
            Assert.AreEqual(0, r.Stray);
            Assert.AreEqual(0, r.HoldPartial);
            Assert.AreEqual(s.AllCues.Count(c => c.IsHold), r.HoldComplete);
            Assert.AreEqual(s.AllCues.Count, r.Perfect);
            var ev = s.DrainEvents();
            Assert.AreEqual(1, ev.OfType<PhraseEnded>().Count());
            Assert.AreEqual(4, ev.OfType<BlockDecided>().Count());
            Assert.AreEqual(16, ev.OfType<BassRevealed>().Single().Beat);
        }

        [Test] public void Autoplayer_Fallback_FullSuccess() { AssertPerfectFullSuccess(FallbackChart.Create()); }

        [Test] public void Autoplayer_ParsedPack_FullSuccess() { AssertPerfectFullSuccess(PackJson.Parse(RhythmPackData.Lv1Warehouse)); }

        [Test]
        public void Autoplayer_AllTiers_Succeed()
        {
            var chart = PackJson.Parse(RhythmPackData.Lv1Warehouse);
            foreach (var key in chart.Tiers.Keys)
            {
                var tn = new RhythmTuning { Tier = key };
                Assert.IsTrue(Autoplay(new PhraseSession(tn, chart)).Success, "tier " + key);
            }
        }

        [Test]
        public void Calibration_ShiftsJudgedPress()
        {
            var tn = new RhythmTuning { CalibrationMs = 100 };
            var s = new PhraseSession(tn, OneCueChart("drums", 960, 0, 1));
            var j = s.Press(1, s.BeatToSeconds(1) + 0.1); // late by exactly the calibration: perfect
            Assert.AreEqual(JudgementKind.Perfect, j.Kind);
        }

        [Test]
        public void Session_EmitsEventsInOrder()
        {
            var s = new PhraseSession(new RhythmTuning(), FallbackChart.Create());
            Autoplay(s);
            var kinds = s.DrainEvents().Where(e => !(e is JudgementEvent)).Select(e => e.GetType().Name).ToList();
            CollectionAssert.AreEqual(new[]
            {
                "BlockDecided", "BassRevealed", "BlockDecided", "BlockDecided", "RouteWindowOpened", "BlockDecided", "RouteLocked", "PhraseEnded",
            }, kinds);
        }
    }
}
