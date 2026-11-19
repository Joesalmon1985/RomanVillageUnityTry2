using System;
using System.Collections.Generic;

namespace World.Sim.Encounters.Council
{
    /// <summary>The authored intention of a line (brief section 5).</summary>
    public enum Intention { Complain = 0, Encourage = 1, Warn = 2, Reassure = 3, Question = 4 }

    public enum ListeningPhase { InProgress = 0, Complete = 1, Abandoned = 2 }

    /// <summary>Same = 0, Different = 1.</summary>
    public enum ListeningChoice { Same = 0, Different = 1 }

    public sealed class ListeningResult
    {
        public int Responses;
        public int Correct;
        /// <summary>Correct answers alone, percent.</summary>
        public int AccuracyPercent;
        /// <summary>Accuracy points earned by fast correct answers.</summary>
        public int SpeedBonusPercent;
        /// <summary>Accuracy plus speed bonus (capped at 100): compared with the threshold.</summary>
        public int EffectivePercent;
        public int ThresholdPercent;
        public bool Earned;
    }

    /// <summary>
    /// One burst: a reference line then <see cref="ListeningTuning.Responses"/> lines to judge, each Same or Different
    /// against the line before it. Plain data (saved with the election). Lines are stored by id; the text is content.
    /// </summary>
    public sealed class ListeningState
    {
        public int Burst;
        public ListeningPhase Phase;
        /// <summary>Lines[0] is the reference; Lines[i + 1] is judged by answer i.</summary>
        public string[] LineIds = new string[0];
        public int[] Tags = new int[0];
        /// <summary>-1 = unanswered, else a <see cref="ListeningChoice"/>.</summary>
        public int[] Answers = new int[0];
        public bool[] Correct = new bool[0];
        public int[] ResponseMs = new int[0];
        /// <summary>Answers given so far.</summary>
        public int Index;
        public ListeningResult Result;
    }

    /// <summary>Starts, answers and scores bursts. Uses only its own random stream (never the election's rolls).</summary>
    public static class ListeningSession
    {
        public const int MaxResponseMs = 60000;

        /// <summary>The expected answer to compare line <paramref name="index"/> + 1 with the line before it.</summary>
        public static bool IsSame(ListeningState s, int index) => s.Tags[index + 1] == s.Tags[index];

        /// <summary>A new burst from a seed: balanced Same/Different, varied speakers, no repeated line.</summary>
        public static ListeningState Start(ListeningTuning tuning, IList<ListeningLine> lines, ulong seed, int burst)
        {
            var rng = new Rng(seed);
            int n = Math.Max(1, tuning.Responses);
            // Balanced answer pattern: as even a split as possible, shuffled, never more than three alike in a row.
            var same = new bool[n];
            int sameCount = n / 2 + (n % 2 == 1 && rng.NextInt(2) == 0 ? 1 : 0);
            for (int i = 0; i < sameCount; i++) same[i] = true;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                rng.Shuffle(same);
                if (MaxRun(same) <= 3) break;
            }
            var tags = new int[n + 1];
            tags[0] = rng.NextInt(5);
            for (int i = 0; i < n; i++)
            {
                if (same[i]) tags[i + 1] = tags[i];
                else
                {
                    int t = rng.NextInt(4);
                    tags[i + 1] = t >= tags[i] ? t + 1 : t;
                }
            }
            var ids = new string[n + 1];
            string lastSpeaker = null;
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i <= n; i++)
            {
                var pool = new List<ListeningLine>();
                foreach (var l in lines) if ((int)l.Tag == tags[i] && !used.Contains(l.Id) && l.Speaker != lastSpeaker) pool.Add(l);
                if (pool.Count == 0) foreach (var l in lines) if ((int)l.Tag == tags[i] && !used.Contains(l.Id)) pool.Add(l);
                if (pool.Count == 0) foreach (var l in lines) if ((int)l.Tag == tags[i]) pool.Add(l);
                var pick = pool[rng.NextInt(pool.Count)];
                ids[i] = pick.Id;
                used.Add(pick.Id);
                lastSpeaker = pick.Speaker;
            }
            var answers = new int[n];
            for (int i = 0; i < n; i++) answers[i] = -1;
            return new ListeningState
            {
                Burst = burst,
                Phase = ListeningPhase.InProgress,
                LineIds = ids,
                Tags = tags,
                Answers = answers,
                Correct = new bool[n],
                ResponseMs = new int[n],
            };
        }

        static int MaxRun(bool[] a)
        {
            int best = 1, run = 1;
            for (int i = 1; i < a.Length; i++) { run = a[i] == a[i - 1] ? run + 1 : 1; if (run > best) best = run; }
            return best;
        }

        public static ActionResult ValidateAnswer(ListeningState s, int choice, int responseMs)
        {
            if (s == null || s.Phase != ListeningPhase.InProgress) return ActionResult.Reject(CouncilReasons.ListeningNone, "There is no burst to answer.");
            if (choice != 0 && choice != 1) return ActionResult.Reject(CouncilReasons.BadAnswer, "Answer Same or Different.");
            if (responseMs < 0) return ActionResult.Reject(CouncilReasons.BadAnswer, "A response time cannot be negative.");
            return ActionResult.Accept();
        }

        /// <summary>Records one answer (response time in milliseconds from the caller). Scores the burst after the last.</summary>
        public static ActionResult Answer(ListeningState s, ListeningTuning tuning, int choice, int responseMs, List<CouncilEvent> events)
        {
            var r = ValidateAnswer(s, choice, responseMs);
            if (!r.Ok) return r;
            int i = s.Index;
            int ms = Math.Min(responseMs, MaxResponseMs);
            bool correct = (choice == (int)ListeningChoice.Same) == IsSame(s, i);
            s.Answers[i] = choice;
            s.Correct[i] = correct;
            s.ResponseMs[i] = ms;
            s.Index++;
            events?.Add(new CouncilListeningAnswered { Index = i, Correct = correct, ResponseMs = ms, ReferenceTag = s.Tags[i], LineTag = s.Tags[i + 1] });
            if (s.Index >= s.Answers.Length)
            {
                s.Phase = ListeningPhase.Complete;
                s.Result = Score(s, tuning);
                events?.Add(new CouncilListeningFinished { Result = s.Result });
            }
            return r;
        }

        /// <summary>Accuracy plus a speed bonus for correct answers; earned when the total reaches the threshold.</summary>
        public static ListeningResult Score(ListeningState s, ListeningTuning t)
        {
            int n = s.Answers.Length, correct = 0, bonusPermille = 0;
            for (int i = 0; i < n; i++)
            {
                if (!s.Correct[i]) continue;
                correct++;
                int ms = s.ResponseMs[i];
                int factor = ms <= t.FastMs ? 1000 : ms >= t.SlowMs ? 0 : (t.SlowMs - ms) * 1000 / Math.Max(1, t.SlowMs - t.FastMs);
                bonusPermille += factor;
            }
            int accuracy = n == 0 ? 0 : correct * 100 / n;
            int bonus = bonusPermille * t.SpeedBonusPercent / 1000;
            int effective = Math.Min(100, accuracy + bonus);
            return new ListeningResult
            {
                Responses = n, Correct = correct, AccuracyPercent = accuracy, SpeedBonusPercent = bonus,
                EffectivePercent = effective, ThresholdPercent = t.AccuracyThresholdPercent,
                Earned = effective >= t.AccuracyThresholdPercent && correct > 0,
            };
        }
    }

    /// <summary>
    /// Listening on top of an election: starting, answering, skipping and abandoning a burst, and the one petition
    /// per election. Touches only listening fields: never <see cref="ElectionState.Rolls"/>.
    /// </summary>
    public static class ElectionListening
    {
        public static ActionResult ValidateStart(ElectionState e)
        {
            if (e == null || e.Phase == ElectionPhase.Complete) return ActionResult.Reject(CouncilReasons.ElectionOver, "The election is over.");
            if (e.PlayerSeat < 0) return ActionResult.Reject(CouncilReasons.NoPlayer, "Nobody here has asked for your help.");
            if (e.PetitionEarned) return ActionResult.Reject(CouncilReasons.PetitionAlreadyEarned, "A petition has already been earned.");
            if (e.Listening != null && e.Listening.Phase == ListeningPhase.InProgress) return ActionResult.Reject(CouncilReasons.ListeningActive, "A burst is already under way.");
            if (e.ListenBursts >= e.ListeningRules.MaxBursts) return ActionResult.Reject(CouncilReasons.ListeningLimit, "No more bursts this election.");
            return ActionResult.Accept();
        }

        public static ActionResult Start(ElectionState e, IList<ListeningLine> lines, List<CouncilEvent> events)
        {
            var r = ValidateStart(e);
            if (!r.Ok) return r;
            e.ListenBursts++;
            e.Listening = ListeningSession.Start(e.ListeningRules, lines, e.ListenRng.NextULong(), e.ListenBursts);
            events?.Add(new CouncilListeningStarted { Seat = e.PlayerSeat, Burst = e.ListenBursts });
            return r;
        }

        public static ActionResult Answer(ElectionState e, int choice, int responseMs, List<CouncilEvent> events)
        {
            var r = ListeningSession.Answer(e.Listening, e.ListeningRules, choice, responseMs, events);
            if (!r.Ok) return r;
            if (e.Listening.Phase == ListeningPhase.Complete && e.Listening.Result.Earned && !e.PetitionEarned)
            {
                e.PetitionEarned = true;
                events?.Add(new CouncilPetitionEarned { Seat = e.PlayerSeat });
            }
            return r;
        }

        /// <summary>Leaves a burst unfinished (it is dropped). No effect on the election's draws.</summary>
        public static ActionResult Abandon(ElectionState e, List<CouncilEvent> events)
        {
            if (e == null || e.Listening == null || e.Listening.Phase != ListeningPhase.InProgress)
                return ActionResult.Reject(CouncilReasons.ListeningNone, "There is no burst to leave.");
            e.Listening.Phase = ListeningPhase.Abandoned;
            e.ListenAbandons++;
            events?.Add(new CouncilListeningLeft { Seat = e.PlayerSeat, Abandoned = true });
            return ActionResult.Accept();
        }

        /// <summary>Records that the player chose not to listen this time.</summary>
        public static ActionResult Skip(ElectionState e, List<CouncilEvent> events)
        {
            if (e == null || e.Phase == ElectionPhase.Complete) return ActionResult.Reject(CouncilReasons.ElectionOver, "The election is over.");
            e.ListenSkips++;
            events?.Add(new CouncilListeningLeft { Seat = e.PlayerSeat, Abandoned = false });
            return ActionResult.Accept();
        }
    }
}
