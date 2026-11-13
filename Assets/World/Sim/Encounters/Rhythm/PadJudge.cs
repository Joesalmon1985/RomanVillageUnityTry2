using System;
using System.Collections.Generic;

namespace World.Sim.Encounters.Rhythm
{
    public enum JudgementKind { Perfect, Great, Ok, Miss, Stray, HoldComplete, HoldPartial }

    public enum CueState { Pending, Holding, Done }

    /// <summary>A cue placed on the song timeline (seconds from song time 0 = beat 0).</summary>
    public sealed class RhythmCue
    {
        public int Index;
        public int Block;
        public int Pad;
        public string Family;
        /// <summary>Absolute phrase tick (960 per beat).</summary>
        public int Tick;
        public int DurTicks;
        public double Time;
        public double EndTime;
        public bool IsHold;
        public CueState State;
        /// <summary>Final once State is Done (0..1).</summary>
        public double Credit;
        public double ResolvedAt = -1;
    }

    public sealed class Judgement
    {
        public JudgementKind Kind;
        public int Pad;
        public double Time;
        /// <summary>Index of the cue, -1 for a stray.</summary>
        public int CueIndex = -1;
        /// <summary>Hold coverage for HoldComplete/HoldPartial, 1 for a hit, 0 for a miss.</summary>
        public double Credit;
        public double DeltaMs;
    }

    /// <summary>Pure state machine over the cues, driven by song time (port of MusicGame pad_judge.gd).</summary>
    public sealed class PadJudge
    {
        private const double Eps = 1e-9;
        private readonly double acceptS, perfectMs, greatMs, acceptMs, completeS;
        private readonly List<RhythmCue> cues = new List<RhythmCue>();
        private readonly List<int>[] padCues = new List<int>[5];
        private readonly int[] cursor = new int[5];
        private readonly int[] active = new int[5];
        private readonly List<Judgement> pending = new List<Judgement>();
        private double now = double.NegativeInfinity;

        public int Perfect, Great, Ok, Misses, Strays, HoldsComplete, HoldsPartial;

        public PadJudge(RhythmTuning t)
        {
            perfectMs = t.PerfectMs;
            greatMs = t.GreatMs;
            acceptMs = t.AcceptMs;
            acceptS = t.AcceptMs / 1000.0;
            completeS = t.HoldCompleteSeconds;
            for (int i = 0; i < 5; i++) { padCues[i] = new List<int>(); active[i] = -1; }
        }

        public IReadOnlyList<RhythmCue> Cues { get { return cues; } }

        /// <summary>Appends a cue (cues of later blocks come later in time). Sets its Index.</summary>
        public void AddCue(RhythmCue c)
        {
            c.Index = cues.Count;
            c.State = CueState.Pending;
            c.Credit = 0;
            cues.Add(c);
            if (c.Pad >= 1 && c.Pad <= 4) padCues[c.Pad].Add(c.Index);
        }

        /// <summary>Takes the judgements produced since the last call.</summary>
        public List<Judgement> Drain()
        {
            var o = new List<Judgement>(pending);
            pending.Clear();
            return o;
        }

        public void Advance(double t)
        {
            if (t > now) now = t;
            for (int pad = 1; pad <= 4; pad++)
            {
                var list = padCues[pad];
                int c = cursor[pad];
                while (c < list.Count)
                {
                    var cue = cues[list[c]];
                    if (cue.State == CueState.Pending)
                    {
                        if (now - cue.Time > acceptS + Eps) Miss(cue);
                        else break;
                    }
                    else if (cue.State == CueState.Holding)
                    {
                        if (now >= cue.EndTime) FinishHold(pad, cue, cue.EndTime, true);
                        else break;
                    }
                    c++;
                }
                cursor[pad] = c;
            }
        }

        public Judgement Press(int pad, double t)
        {
            Advance(t);
            if (pad < 1 || pad > 4) return null;
            if (active[pad] >= 0)
            {
                var held = cues[active[pad]];
                if (t < held.EndTime - completeS) return null; // a second finger on a held pad satisfies nothing
                FinishHold(pad, held, t, true);
            }
            RhythmCue best = null;
            double bestAbs = double.MaxValue;
            var list = padCues[pad];
            for (int k = cursor[pad]; k < list.Count; k++)
            {
                var cue = cues[list[k]];
                if (cue.State != CueState.Pending) continue;
                double dMs = (t - cue.Time) * 1000.0;
                if (dMs > acceptMs + 1e-6) continue;
                if (dMs < -(acceptMs + 1e-6)) break;
                if (Math.Abs(dMs) < bestAbs - 1e-9) { best = cue; bestAbs = Math.Abs(dMs); } // ties keep the earlier cue
            }
            if (best == null)
            {
                Strays++;
                return Emit(new Judgement { Kind = JudgementKind.Stray, Pad = pad, Time = t });
            }
            JudgementKind kind = bestAbs <= perfectMs + 1e-6 ? JudgementKind.Perfect : (bestAbs <= greatMs + 1e-6 ? JudgementKind.Great : JudgementKind.Ok);
            if (kind == JudgementKind.Perfect) Perfect++; else if (kind == JudgementKind.Great) Great++; else Ok++;
            var j = new Judgement { Kind = kind, Pad = pad, Time = t, CueIndex = best.Index, DeltaMs = (t - best.Time) * 1000.0, Credit = 1 };
            if (best.IsHold)
            {
                best.State = CueState.Holding;
                active[pad] = best.Index;
            }
            else
            {
                best.State = CueState.Done;
                best.Credit = 1;
                best.ResolvedAt = t;
            }
            return Emit(j);
        }

        public Judgement Release(int pad, double t)
        {
            Advance(t);
            if (pad < 1 || pad > 4 || active[pad] < 0) return null;
            var cue = cues[active[pad]];
            return FinishHold(pad, cue, t, t >= cue.EndTime - completeS - Eps);
        }

        /// <summary>Resolves every unresolved cue of a block (a pending cue misses, a hold ends where it stands). Used at the block decision.</summary>
        public void FinalizeBlock(int block, double t)
        {
            for (int pad = 1; pad <= 4; pad++)
                if (active[pad] >= 0 && cues[active[pad]].Block == block)
                {
                    var h = cues[active[pad]];
                    FinishHold(pad, h, Math.Min(t, h.EndTime), t >= h.EndTime - completeS);
                }
            foreach (var c in cues)
                if (c.Block == block && c.State == CueState.Pending) Miss(c);
        }

        private Judgement FinishHold(int pad, RhythmCue cue, double at, bool complete)
        {
            active[pad] = -1;
            double span = cue.EndTime - cue.Time;
            double cov = complete ? 1.0 : (span > 0 ? Math.Max(0, Math.Min(1, (at - cue.Time) / span)) : 1.0);
            cue.State = CueState.Done;
            cue.Credit = cov;
            cue.ResolvedAt = Math.Min(at, cue.EndTime);
            if (complete) HoldsComplete++; else HoldsPartial++;
            return Emit(new Judgement
            {
                Kind = complete ? JudgementKind.HoldComplete : JudgementKind.HoldPartial,
                Pad = pad, Time = cue.ResolvedAt, CueIndex = cue.Index, Credit = cov,
            });
        }

        private void Miss(RhythmCue cue)
        {
            cue.State = CueState.Done;
            cue.Credit = 0;
            cue.ResolvedAt = cue.Time + acceptS;
            Misses++;
            Emit(new Judgement { Kind = JudgementKind.Miss, Pad = cue.Pad, Time = cue.ResolvedAt, CueIndex = cue.Index, Credit = 0 });
        }

        private Judgement Emit(Judgement j)
        {
            pending.Add(j);
            return j;
        }
    }
}
