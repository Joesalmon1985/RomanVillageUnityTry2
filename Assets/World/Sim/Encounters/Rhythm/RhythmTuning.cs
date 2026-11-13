using System.Collections.Generic;

namespace World.Sim.Encounters.Rhythm
{
    /// <summary>Stem volume targets for one mix stage (0..1).</summary>
    public sealed class StemGains
    {
        public double Drums;
        public double Bass;
        public double Other;

        public StemGains() { }

        public StemGains(double drums, double bass, double other)
        {
            Drums = drums;
            Bass = bass;
            Other = other;
        }
    }

    /// <summary>One mix stage: which cue family the block plays and the stem gains.</summary>
    public sealed class StageTuning
    {
        public string Id;
        /// <summary>"drums" or "bass".</summary>
        public string Cue;
        public StemGains Gains = new StemGains();
    }

    /// <summary>Tunables for the rhythm phrase. Defaults come from MusicGame content/run/run.json at 8bf2c02a and PRD 4.3.</summary>
    public sealed class RhythmTuning
    {
        public double Bpm = 174;
        public int TicksPerBeat = 960;
        public int PhraseBeats = 64;
        public int BlockBeats = 16;
        public int BlockCount = 4;
        public double CueSpanBeats = 12.0;
        public double DecisionBeat = 12.5;
        /// <summary>A hold shorter than this many ticks (after clipping to the cue span) becomes a tap.</summary>
        public int MinHoldTicks = 480;

        public double PerfectMs = 45;
        public double GreatMs = 90;
        public double AcceptMs = 140;
        public double HoldCompleteSeconds = 0.15;
        public double PassCredit = 0.6;

        /// <summary>Chart tier key used from the pack ("1".."4").</summary>
        public string Tier = "1";

        public double LeadInSeconds = 2.0;
        /// <summary>Input calibration: a press at song time t is judged as t - CalibrationMs/1000 (positive = player/hardware late).</summary>
        public double CalibrationMs = 0;

        public double RouteOpenBeat = 48.0;
        public double RouteLockBeat = 60.5;

        public List<StageTuning> Stages = DefaultStages();

        public static List<StageTuning> DefaultStages()
        {
            return new List<StageTuning>
            {
                new StageTuning { Id = "drums_alone", Cue = "drums", Gains = new StemGains(1.0, 0.0, 0.0) },
                new StageTuning { Id = "bass_enters", Cue = "bass", Gains = new StemGains(0.5, 1.0, 0.45) },
                new StageTuning { Id = "drums_return", Cue = "drums", Gains = new StemGains(1.0, 0.85, 0.45) },
                new StageTuning { Id = "bass_strongest", Cue = "bass", Gains = new StemGains(0.4, 1.0, 0.5) },
            };
        }
    }
}
