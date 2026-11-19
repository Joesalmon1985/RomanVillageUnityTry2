using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Encounters.Council;
using static World.Sim.Tests.CouncilTestKit;

namespace World.Sim.Tests
{
    /// <summary>Active listening (brief section 5) and its separation from the election's draws.</summary>
    public class CouncilListeningTests
    {
        static readonly CouncilContent C = CouncilContent.BuiltIn;

        static ListeningState Burst(ulong seed, int burst = 1) => ListeningSession.Start(new ListeningTuning(), C.ListeningLines, seed, burst);

        static void AnswerAll(ListeningState s, ListeningTuning t, bool[] correct, int ms)
        {
            for (int i = 0; i < correct.Length; i++)
            {
                bool same = ListeningSession.IsSame(s, i);
                int choice = (correct[i] ? same : !same) ? (int)ListeningChoice.Same : (int)ListeningChoice.Different;
                Assert.IsTrue(ListeningSession.Answer(s, t, choice, ms, null).Ok);
            }
        }

        [Test]
        public void Burst_HasAReferenceAndFiveBalancedResponses()
        {
            int same2 = 0, same3 = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var s = Burst(seed);
                Assert.AreEqual(6, s.LineIds.Length);
                Assert.AreEqual(5, s.Answers.Length);
                int same = Enumerable.Range(0, 5).Count(i => ListeningSession.IsSame(s, i));
                Assert.That(same, Is.InRange(2, 3));
                if (same == 2) same2++; else same3++;
                Assert.AreEqual(6, s.LineIds.Distinct().Count(), "no line repeats in a burst");
                for (int i = 0; i < 6; i++) Assert.AreEqual(s.Tags[i], (int)C.ListeningLine(s.LineIds[i]).Tag, "the stored tag is the line's authored intention");
                int run = 1;
                for (int i = 1; i < 5; i++)
                {
                    run = ListeningSession.IsSame(s, i) == ListeningSession.IsSame(s, i - 1) ? run + 1 : 1;
                    Assert.LessOrEqual(run, 3);
                }
            }
            Assert.Greater(same2, 60); Assert.Greater(same3, 60);
        }

        [Test]
        public void Burst_VariesSpeakersBetweenNeighbours()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var s = Burst(seed);
                for (int i = 1; i < 6; i++)
                    Assert.AreNotEqual(C.ListeningLine(s.LineIds[i]).Speaker, C.ListeningLine(s.LineIds[i - 1]).Speaker);
            }
        }

        [Test]
        public void CorrectAndIncorrectComparisons_AreScored()
        {
            var t = new ListeningTuning();
            var s = Burst(5);
            var ev = new List<CouncilEvent>();
            bool same0 = ListeningSession.IsSame(s, 0);
            Assert.IsTrue(ListeningSession.Answer(s, t, same0 ? 0 : 1, 1000, ev).Ok);
            Assert.IsTrue(s.Correct[0]);
            bool same1 = ListeningSession.IsSame(s, 1);
            Assert.IsTrue(ListeningSession.Answer(s, t, same1 ? 1 : 0, 1000, ev).Ok);
            Assert.IsFalse(s.Correct[1]);
            var answered = ev.OfType<CouncilListeningAnswered>().ToList();
            Assert.IsTrue(answered[0].Correct); Assert.IsFalse(answered[1].Correct);
            Assert.AreEqual(s.Tags[0], answered[0].ReferenceTag);
            Assert.AreEqual(s.Tags[1], answered[0].LineTag);
        }

        [Test]
        public void Scoring_UsesTheThreshold_AndASpeedBonusForCorrectAnswers()
        {
            var t = new ListeningTuning { AccuracyThresholdPercent = 85, SpeedBonusPercent = 5, FastMs = 1500, SlowMs = 4000 };
            ListeningResult Run(bool[] ok, int ms) { var s = Burst(9); AnswerAll(s, t, ok, ms); return s.Result; }
            var five = Run(new[] { true, true, true, true, true }, 5000);
            Assert.IsTrue(five.Earned); Assert.AreEqual(100, five.AccuracyPercent);
            var fourSlow = Run(new[] { true, true, true, true, false }, 5000);
            Assert.AreEqual(80, fourSlow.EffectivePercent); Assert.IsFalse(fourSlow.Earned);
            var fourFast = Run(new[] { true, true, true, true, false }, 800);
            Assert.AreEqual(4 * 5, fourFast.SpeedBonusPercent); Assert.IsTrue(fourFast.Earned);
            var three = Run(new[] { true, true, true, false, false }, 500);
            Assert.IsFalse(three.Earned);
            var wrongFast = Run(new[] { false, false, false, false, false }, 100);
            Assert.AreEqual(0, wrongFast.SpeedBonusPercent, "speed earns nothing without correctness");
            // partial speed
            var tune = new ListeningTuning { SpeedBonusPercent = 10, FastMs = 1000, SlowMs = 3000 };
            var s2 = Burst(9); AnswerAll(s2, tune, new[] { true, true, true, true, true }, 2000);
            Assert.AreEqual(25, s2.Result.SpeedBonusPercent);
            // adjusting the threshold or switching the bonus off changes the outcome
            t.SpeedBonusPercent = 0;
            Assert.IsFalse(Run(new[] { true, true, true, true, false }, 800).Earned);
            t.AccuracyThresholdPercent = 60;
            Assert.IsTrue(Run(new[] { true, true, true, false, false }, 5000).Earned);
        }

        [Test]
        public void Answers_AreRefusedWhenInvalid_AndAfterTheBurstEnds()
        {
            var t = new ListeningTuning();
            var s = Burst(3);
            Assert.AreEqual(CouncilReasons.BadAnswer, ListeningSession.Answer(s, t, 2, 100, null).Reason);
            Assert.AreEqual(CouncilReasons.BadAnswer, ListeningSession.Answer(s, t, 0, -5, null).Reason);
            AnswerAll(s, t, new[] { true, true, true, true, true }, 1000);
            Assert.AreEqual(ListeningPhase.Complete, s.Phase);
            Assert.AreEqual(CouncilReasons.ListeningNone, ListeningSession.Answer(s, t, 0, 100, null).Reason);
        }

        [Test]
        public void OnePetitionPerElection_EarnedOnce_SpentOnce()
        {
            var e = NewElection(21, 0);
            var ev = new List<CouncilEvent>();
            Assert.IsTrue(ElectionListening.Start(e, C.ListeningLines, ev).Ok);
            for (int i = 0; i < 5; i++) ElectionListening.Answer(e, ListeningSession.IsSame(e.Listening, i) ? 0 : 1, 900, ev);
            Assert.IsTrue(e.PetitionEarned);
            Assert.AreEqual(1, ev.OfType<CouncilPetitionEarned>().Count());
            Assert.AreEqual(CouncilReasons.PetitionAlreadyEarned, ElectionListening.Start(e, C.ListeningLines, ev).Reason, "no second petition");
            // spend it after a bust
            e.Active = 0;
            e.Phase = ElectionPhase.BustPending;
            Assert.IsTrue(ElectionEngine.Apply(e, ElectionAction.SpendPetition(0), ev).Ok);
            Assert.IsTrue(e.PetitionSpent);
            Assert.AreEqual(1, ev.OfType<CouncilPetitionSpent>().Count());
            e.Phase = ElectionPhase.BustPending;
            Assert.AreEqual(CouncilReasons.NoPetition, ElectionEngine.Validate(e, ElectionAction.SpendPetition(0)).Reason, "spent only once");
            Assert.AreEqual(CouncilReasons.PetitionAlreadyEarned, ElectionListening.Start(e, C.ListeningLines, ev).Reason);
        }

        [Test]
        public void FailedBursts_AreLimited_AndAllAiElectionsOfferNone()
        {
            var e = NewElection(22, 1);
            var ev = new List<CouncilEvent>();
            for (int b = 0; b < e.ListeningRules.MaxBursts; b++)
            {
                Assert.IsTrue(ElectionListening.Start(e, C.ListeningLines, ev).Ok);
                for (int i = 0; i < 5; i++) ElectionListening.Answer(e, ListeningSession.IsSame(e.Listening, i) ? 1 : 0, 900, ev); // all wrong
                Assert.IsFalse(e.PetitionEarned);
            }
            Assert.AreEqual(CouncilReasons.ListeningLimit, ElectionListening.Start(e, C.ListeningLines, ev).Reason);
            var ai = NewElection(22, -1);
            Assert.AreEqual(CouncilReasons.NoPlayer, ElectionListening.Start(ai, C.ListeningLines, ev).Reason);
        }

        [Test]
        public void AStartedBurst_MustFinishOrBeLeftBeforeAnother()
        {
            var e = NewElection(23, 0);
            Assert.IsTrue(ElectionListening.Start(e, C.ListeningLines, null).Ok);
            Assert.AreEqual(CouncilReasons.ListeningActive, ElectionListening.Start(e, C.ListeningLines, null).Reason);
            Assert.IsTrue(ElectionListening.Abandon(e, null).Ok);
            Assert.AreEqual(CouncilReasons.ListeningNone, ElectionListening.Abandon(e, null).Reason);
            Assert.AreEqual(1, e.ListenAbandons);
        }

        /// <summary>One scripted election; the listening behaviour differs, the election decisions do not.</summary>
        static ElectionState PlayWith(ulong seed, string listening, out List<string> trays)
        {
            var e = NewElection(seed, 0);
            trays = new List<string>();
            var ev = new List<CouncilEvent>();
            int step = 0;
            while (e.Phase != ElectionPhase.Complete && step++ < 5000)
            {
                if (e.Active != 0 && step % 3 == 0)
                {
                    switch (listening)
                    {
                        case "skip": ElectionListening.Skip(e, ev); break;
                        case "abandon":
                            if (e.Listening == null || e.Listening.Phase != ListeningPhase.InProgress) ElectionListening.Start(e, C.ListeningLines, ev);
                            else { ElectionListening.Answer(e, 0, 500, ev); if (e.Listening.Index == 3) ElectionListening.Abandon(e, ev); }
                            break;
                        case "visit":
                            // finishes bursts with wrong answers: visited but no petition is ever earned
                            if (e.Listening == null || e.Listening.Phase != ListeningPhase.InProgress) ElectionListening.Start(e, C.ListeningLines, ev);
                            else ElectionListening.Answer(e, ListeningSession.IsSame(e.Listening, e.Listening.Index) ? 1 : 0, 700, ev);
                            break;
                    }
                }
                ev.Clear();
                // the player's seat (0) acts with a fixed rule that ignores listening; AI seats use their own streams
                Step(e, ev);
                foreach (var o in ev.OfType<CouncilOptionsOffered>())
                    trays.Add(o.Seat + ":" + string.Join("/", o.Trays.Select(t => t.SumA + "+" + t.SumB)));
            }
            return e;
        }

        [Test]
        public void VisitingSkippingOrAbandoningListening_NeverChangesTheDraws()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var baseline = PlayWith(seed, "none", out var baseTrays);
                foreach (var mode in new[] { "skip", "abandon", "visit" })
                {
                    var e = PlayWith(seed, mode, out var trays);
                    CollectionAssert.AreEqual(baseTrays, trays, mode + " seed " + seed);
                    Assert.AreEqual(baseline.Rolls.State, e.Rolls.State, mode);
                    Assert.AreEqual(baseline.AiRng.State, e.AiRng.State, mode + ": AI stream untouched too");
                    Assert.AreEqual(baseline.Winner, e.Winner);
                    Assert.AreEqual(baseline.Pushes, e.Pushes);
                }
            }
        }

        [Test]
        public void ListeningStateSurvivesSaveAndLoad_AtEveryAnswer()
        {
            var e = NewElection(31, 0);
            ElectionListening.Start(e, C.ListeningLines, null);
            for (int i = 0; i < 5; i++)
            {
                var copy = RoundTrip(e);
                Assert.AreEqual(Json(e), Json(copy));
                int choice = i % 2;
                ElectionListening.Answer(e, choice, 1200 + i, null);
                ElectionListening.Answer(copy, choice, 1200 + i, null);
                Assert.AreEqual(Json(e), Json(copy));
            }
            Assert.IsNotNull(e.Listening.Result);
        }

        [Test]
        public void ListeningDoesNotDrawFromTheElectionStream_WhenBurstsAreStarted()
        {
            var e = NewElection(40, 0);
            ulong rolls = e.Rolls.State, ai = e.AiRng.State;
            ElectionListening.Start(e, C.ListeningLines, null);
            Assert.AreEqual(rolls, e.Rolls.State);
            Assert.AreEqual(ai, e.AiRng.State);
        }
    }
}
