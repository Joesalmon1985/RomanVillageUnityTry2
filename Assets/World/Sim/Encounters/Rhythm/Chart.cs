using System.Collections.Generic;

namespace World.Sim.Encounters.Rhythm
{
    /// <summary>A cue as stored in a pack: [tick, dur_ticks, pad, family]. Ticks are 960 per beat from the loop start.</summary>
    public sealed class ChartCue
    {
        public int Tick;
        public int DurTicks;
        public int Pad;
        public string Family;
    }

    public sealed class TierCues
    {
        public List<ChartCue> Drums = new List<ChartCue>();
        public List<ChartCue> Bass = new List<ChartCue>();
    }

    /// <summary>Chart model of a pack.json: one loop of PhraseBeats beats (32 in lv1_warehouse), cues per tier and family.</summary>
    public sealed class Chart
    {
        public string Id;
        public string Label;
        public double Bpm = 174;
        /// <summary>Beats in the pack's loop (32). The game phrase is 64 beats, so the loop plays twice.</summary>
        public int PhraseBeats = 32;
        public int LoopFrames;
        /// <summary>Stem name to the pack's relative path (informational).</summary>
        public Dictionary<string, string> Stems = new Dictionary<string, string>();
        public Dictionary<string, TierCues> Tiers = new Dictionary<string, TierCues>();
    }
}
