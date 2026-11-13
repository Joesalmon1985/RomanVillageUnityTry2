using System;
using System.Collections.Generic;

namespace World.Sim.Encounters.Rhythm
{
    public enum Route { Bar, Balcony }

    public enum RouteOutcome { None, Bar, Balcony }

    public enum RouteWindowState { NotYet, Open, Locked }

    public abstract class PhraseEvent
    {
        public double SongTime;
    }

    public sealed class JudgementEvent : PhraseEvent { public Judgement Judgement; }

    public sealed class BlockDecided : PhraseEvent
    {
        public int Block;
        public double Credit;
        public bool Passed;
        public int Stage;
        /// <summary>Stage of the next block (equals Stage for the last block).</summary>
        public int NextStage;
    }

    public sealed class BassRevealed : PhraseEvent { public int Beat; }

    public sealed class RouteWindowOpened : PhraseEvent { }

    public sealed class RouteLocked : PhraseEvent { public Route Route; }

    public sealed class PhraseEnded : PhraseEvent { public PhraseResult Result; }

    public sealed class BlockResult
    {
        public int Block;
        public double Credit;
        public bool Passed;
        public int Stage;
        public string Family;
    }

    public sealed class PhraseResult
    {
        public int PassedBlocks;
        public bool FinalBlockPassed;
        /// <summary>Three or more passing blocks and a passing final block.</summary>
        public bool Success;
        public RouteOutcome RouteOpened;
        public int Perfect, Great, Ok, Miss, Stray, HoldComplete, HoldPartial;
        /// <summary>True when the final block failed: the player stays on the floor and may retry.</summary>
        public bool CanRetry { get { return !FinalBlockPassed; } }
    }

    /// <summary>
    /// One 64-beat phrase of four 16-beat blocks. Song time 0 is beat 0 (first sample of the stems); the session may be
    /// advanced from -LeadInSeconds. All rules live here so a UI only draws.
    /// </summary>
    public sealed class PhraseSession
    {
        private readonly RhythmTuning tuning;
        private readonly Chart chart;
        private readonly TierCues tier;
        private readonly PadJudge judge;
        private readonly double bpm;
        private readonly int blocks;
        private readonly int[] stageOfBlock;
        private readonly BlockResult[] results;
        private readonly List<PhraseEvent> events = new List<PhraseEvent>();
        private readonly List<Milestone> milestones = new List<Milestone>();
        private int nextMilestone;
        private int bassRevealBeat = -1;
        private Route selection = Route.Bar;
        private double now = double.NegativeInfinity;
        private int cursorBlock;

        private enum MKind { Decide, OpenRoute, LockRoute, End }

        private sealed class Milestone { public double Beat; public MKind Kind; public int Block; }

        public PhraseSession(RhythmTuning tuning, Chart chart)
        {
            this.tuning = tuning;
            this.chart = chart;
            bpm = chart.Bpm > 0 ? chart.Bpm : tuning.Bpm;
            blocks = tuning.BlockCount;
            if (!chart.Tiers.TryGetValue(tuning.Tier, out tier)) throw new ArgumentException("chart has no tier " + tuning.Tier);
            judge = new PadJudge(tuning);
            stageOfBlock = new int[blocks];
            results = new BlockResult[blocks];
            for (int i = 0; i < blocks; i++) stageOfBlock[i] = -1;
            stageOfBlock[0] = 0;
            for (int b = 0; b < blocks; b++)
                milestones.Add(new Milestone { Beat = b * tuning.BlockBeats + tuning.DecisionBeat, Kind = MKind.Decide, Block = b });
            milestones.Add(new Milestone { Beat = tuning.RouteOpenBeat, Kind = MKind.OpenRoute });
            milestones.Add(new Milestone { Beat = tuning.RouteLockBeat, Kind = MKind.LockRoute });
            milestones.Add(new Milestone { Beat = tuning.PhraseBeats, Kind = MKind.End });
            // stable order by beat; at equal beats the decision (added first) comes before the lock
            for (int i = 1; i < milestones.Count; i++)
            {
                var m = milestones[i];
                int j = i - 1;
                while (j >= 0 && milestones[j].Beat > m.Beat) { milestones[j + 1] = milestones[j]; j--; }
                milestones[j + 1] = m;
            }
            ChartBlock(0);
        }

        // ---- timing helpers ----
        public double BeatToSeconds(double beat) { return beat * 60.0 / bpm; }
        public double TickToSeconds(long tick) { return tick * 60.0 / (bpm * tuning.TicksPerBeat); }
        public double SecondsToBeat(double s) { return s * bpm / 60.0; }

        // ---- state ----
        public RhythmTuning Tuning { get { return tuning; } }
        public int CurrentBlock { get { return cursorBlock; } }
        public bool IsEnded { get; private set; }
        public PhraseResult Result { get; private set; }
        public int BassRevealBeat { get { return bassRevealBeat; } }
        public bool HasBassReveal { get { return bassRevealBeat >= 0; } }
        public RouteWindowState RouteWindow { get; private set; }
        public Route CurrentRoute { get { return selection; } }
        public bool RouteChosen { get; private set; }
        public IReadOnlyList<RhythmCue> AllCues { get { return judge.Cues; } }

        /// <summary>Stage of a block, or -1 while undecided (a block's stage is known at the previous block's decision).</summary>
        public int StageOfBlock(int block) { return block >= 0 && block < blocks ? stageOfBlock[block] : -1; }

        /// <summary>Cue family ("drums"/"bass") of a block, or null while its stage is undecided.</summary>
        public string FamilyOfBlock(int block)
        {
            int s = StageOfBlock(block);
            return s < 0 ? null : tuning.Stages[s].Cue;
        }

        /// <summary>Result of a decided block, or null.</summary>
        public BlockResult BlockResultOf(int block) { return block >= 0 && block < blocks ? results[block] : null; }

        /// <summary>Stem gain targets for the block containing the current song time.</summary>
        public StemGains CurrentGains { get { return tuning.Stages[Math.Max(0, stageOfBlock[cursorBlock])].Gains; } }

        public int CurrentStage { get { return Math.Max(0, stageOfBlock[cursorBlock]); } }

        /// <summary>Cues of the current and next block (as far as charted) with absolute song times; check State/Credit for hit effects.</summary>
        public List<RhythmCue> UpcomingCues()
        {
            var o = new List<RhythmCue>();
            if (IsEnded) return o;
            foreach (var c in judge.Cues)
                if (c.Block == cursorBlock || c.Block == cursorBlock + 1) o.Add(c);
            return o;
        }

        public List<PhraseEvent> DrainEvents()
        {
            var o = new List<PhraseEvent>(events);
            events.Clear();
            return o;
        }

        // ---- input ----
        public Judgement Press(int pad, double songTime)
        {
            if (IsEnded) return null;
            Advance(songTime);
            if (IsEnded) return null;
            var j = judge.Press(pad, songTime - tuning.CalibrationMs / 1000.0);
            Pump();
            return j;
        }

        public Judgement Release(int pad, double songTime)
        {
            if (IsEnded) return null;
            Advance(songTime);
            if (IsEnded) return null;
            var j = judge.Release(pad, songTime - tuning.CalibrationMs / 1000.0);
            Pump();
            return j;
        }

        /// <summary>Accepted only while the route window is open (beat 48 up to, not including, beat 60.5).</summary>
        public bool ChooseRoute(Route route, double songTime)
        {
            Advance(songTime);
            if (RouteWindow != RouteWindowState.Open) return false;
            selection = route;
            RouteChosen = true;
            return true;
        }

        public void Advance(double songTime)
        {
            if (songTime > now) now = songTime;
            while (!IsEnded && nextMilestone < milestones.Count)
            {
                var m = milestones[nextMilestone];
                double t = BeatToSeconds(m.Beat);
                if (t > now) break;
                nextMilestone++;
                Process(m, t);
            }
            if (!IsEnded)
            {
                judge.Advance(now - tuning.CalibrationMs / 1000.0);
                Pump();
                int b = (int)Math.Floor(SecondsToBeat(now) / tuning.BlockBeats + 1e-9);
                cursorBlock = Math.Max(0, Math.Min(blocks - 1, b));
            }
        }

        private void Process(Milestone m, double t)
        {
            switch (m.Kind)
            {
                case MKind.Decide: Decide(m.Block, t); break;
                case MKind.OpenRoute:
                    RouteWindow = RouteWindowState.Open;
                    events.Add(new RouteWindowOpened { SongTime = t });
                    break;
                case MKind.LockRoute:
                    RouteWindow = RouteWindowState.Locked;
                    events.Add(new RouteLocked { SongTime = t, Route = selection });
                    break;
                case MKind.End: End(t); break;
            }
        }

        private void Decide(int b, double t)
        {
            double cal = tuning.CalibrationMs / 1000.0;
            judge.Advance(t - cal);
            judge.FinalizeBlock(b, t - cal);
            Pump();
            double sum = 0; int n = 0;
            foreach (var c in judge.Cues)
                if (c.Block == b) { sum += c.Credit; n++; }
            double credit = n == 0 ? 1.0 : sum / n;
            bool passed = credit >= tuning.PassCredit - 1e-9;
            int stage = stageOfBlock[b];
            int next = stage;
            if (b < blocks - 1 && passed) next = Math.Min(stage + 1, tuning.Stages.Count - 1);
            results[b] = new BlockResult { Block = b, Credit = credit, Passed = passed, Stage = stage, Family = tuning.Stages[stage].Cue };
            events.Add(new BlockDecided { SongTime = t, Block = b, Credit = credit, Passed = passed, Stage = stage, NextStage = next });
            if (b < blocks - 1)
            {
                stageOfBlock[b + 1] = next;
                int boundary = (b + 1) * tuning.BlockBeats;
                if (next >= 1 && bassRevealBeat < 0)
                {
                    bassRevealBeat = boundary;
                    events.Add(new BassRevealed { SongTime = BeatToSeconds(boundary), Beat = boundary });
                }
                ChartBlock(b + 1);
            }
        }

        private void End(double t)
        {
            judge.Advance(t - tuning.CalibrationMs / 1000.0);
            for (int b = 0; b < blocks; b++) judge.FinalizeBlock(b, t);
            Pump();
            var r = new PhraseResult
            {
                Perfect = judge.Perfect, Great = judge.Great, Ok = judge.Ok, Miss = judge.Misses, Stray = judge.Strays,
                HoldComplete = judge.HoldsComplete, HoldPartial = judge.HoldsPartial,
            };
            for (int b = 0; b < blocks; b++) if (results[b] != null && results[b].Passed) r.PassedBlocks++;
            var last = results[blocks - 1];
            r.FinalBlockPassed = last != null && last.Passed;
            r.Success = r.PassedBlocks >= 3 && r.FinalBlockPassed;
            r.RouteOpened = r.Success ? (selection == Route.Bar ? RouteOutcome.Bar : RouteOutcome.Balcony) : RouteOutcome.None;
            Result = r;
            IsEnded = true;
            events.Add(new PhraseEnded { SongTime = t, Result = r });
        }

        private void Pump()
        {
            foreach (var j in judge.Drain()) events.Add(new JudgementEvent { SongTime = j.Time, Judgement = j });
        }

        /// <summary>Adds the cues of block b for its stage: pack position = (blockStart - origin) mod loop length, origin 0 for drums, the bass reveal beat for bass.</summary>
        private void ChartBlock(int b)
        {
            int stage = stageOfBlock[b];
            string fam = tuning.Stages[stage].Cue;
            var src = fam == "drums" ? tier.Drums : tier.Bass;
            int tpb = tuning.TicksPerBeat;
            int start = b * tuning.BlockBeats;
            int origin = fam == "drums" ? 0 : Math.Max(bassRevealBeat, 0);
            int loop = chart.PhraseBeats;
            int pos = (((start - origin) % loop) + loop) % loop;
            var added = new List<ChartCue>();
            foreach (var c in src)
            {
                int off = c.Tick - pos * tpb;
                if (off < 0 || off >= tuning.BlockBeats * tpb) continue;
                if ((double)off / tpb > tuning.CueSpanBeats + 1e-6) continue;
                added.Add(c);
            }
            added.Sort((x, y) => x.Tick != y.Tick ? x.Tick.CompareTo(y.Tick) : x.Pad.CompareTo(y.Pad));
            foreach (var c in added)
            {
                int off = c.Tick - pos * tpb;
                int dur = c.DurTicks;
                if (dur > 0)
                {
                    dur = Math.Min(dur, (int)Math.Round((tuning.CueSpanBeats - (double)off / tpb) * tpb));
                    if (dur < tuning.MinHoldTicks) dur = 0;
                }
                int tick = start * tpb + off;
                double time = TickToSeconds(tick);
                judge.AddCue(new RhythmCue
                {
                    Block = b, Pad = c.Pad, Family = c.Family, Tick = tick, DurTicks = dur,
                    Time = time, EndTime = TickToSeconds(tick + dur), IsHold = dur > 0,
                });
            }
        }
    }
}
