using System.Collections.Generic;

namespace World.Sim.Encounters.Rhythm
{
    /// <summary>
    /// Built-in chart so the game works with no files: tier 1 of the owner's own Lv1 Warehouse pack (Beat Shaper loop, 174 BPM,
    /// 32-beat loop). Generated from Resources/Rhythm/lv1_warehouse/pack.json at commit 8bf2c02a of the MusicGame repo.
    /// </summary>
    public static class FallbackChart
    {
        public static Chart Create()
        {
            var chart = new Chart
            {
                Id = "lv1_warehouse_fallback",
                Label = "Lv1 Warehouse (built in, tier 1)",
                Bpm = 174,
                PhraseBeats = 32,
                LoopFrames = 529655,
            };
            var tier = new TierCues();
            tier.Drums.AddRange(new[]
            {
                C(0, 0, 1, "kick"),
                C(1440, 0, 4, "snare"),
                C(2400, 0, 1, "kick"),
                C(5760, 0, 4, "snare"),
                C(6720, 0, 4, "snare"),
                C(8160, 0, 1, "kick"),
                C(9600, 0, 4, "snare"),
                C(10560, 0, 1, "kick"),
                C(12480, 0, 1, "kick"),
                C(13440, 0, 4, "snare"),
                C(14400, 0, 1, "kick"),
                C(14400, 0, 4, "snare"),
                C(15360, 0, 1, "kick"),
                C(16800, 0, 4, "snare"),
                C(18240, 0, 1, "kick"),
                C(19680, 0, 1, "kick"),
                C(21120, 0, 4, "snare"),
                C(23040, 0, 1, "kick"),
                C(24480, 0, 4, "snare"),
                C(25920, 0, 1, "kick"),
                C(26880, 0, 4, "snare"),
                C(27840, 0, 4, "snare"),
                C(28800, 0, 4, "snare"),
                C(29760, 0, 4, "snare"),
            });
            tier.Bass.AddRange(new[]
            {
                C(0, 7680, 2, "bass"),
                C(8160, 6960, 1, "bass"),
                C(15360, 7680, 2, "bass"),
                C(24960, 4800, 3, "bass"),
            });
            chart.Tiers["1"] = tier;
            return chart;
        }

        private static ChartCue C(int tick, int dur, int pad, string family)
        {
            return new ChartCue { Tick = tick, DurTicks = dur, Pad = pad, Family = family };
        }
    }
}
