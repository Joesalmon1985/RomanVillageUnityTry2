using Newtonsoft.Json;

namespace World.Sim.Encounters.Ward
{
    /// <summary>Tunable Ward numbers (PRD 4.2). All times are integer milliseconds.</summary>
    public sealed class WardTuning
    {
        public int Loci = 3;
        public int MaxCasts = 10;
        public int PlayerRecoveryMs = 2000;
        public int IdleAutoCastMs = 60000;
        public int MiraFirstCastMinMs = 1500;
        public int MiraFirstCastMaxMs = 4000;
        public int MiraThinkMinMs = 2500;
        public int MiraThinkMaxMs = 6000;
        public int MiraMistakeRatePercent = 0;
        public int LightUnlockAfterDuels = 3;

        /// <summary>
        /// The practice cadence the game ships with (M2 balance): Mira thinks 6 to 10 s (first cast 3 to 6 s), no mistakes.
        /// Measured against a scripted first-time player (candidate filter with 15% random casts, a cast every 5 to 7 s):
        /// about 54% wins with three essences (see WardBalanceTests). The PRD defaults above stay the rule defaults.
        /// </summary>
        public static WardTuning Practice()
        {
            return new WardTuning
            {
                MiraFirstCastMinMs = 3000, MiraFirstCastMaxMs = 6000,
                MiraThinkMinMs = 6000, MiraThinkMaxMs = 10000,
                MiraMistakeRatePercent = 0,
            };
        }

        /// <summary>Overrides fields present in a JSON object (e.g. StreamingAssets/world/ward.json) on top of a base tuning.</summary>
        public static WardTuning FromJson(string json, WardTuning baseTuning = null)
        {
            var t = baseTuning ?? Practice();
            if (!string.IsNullOrEmpty(json)) JsonConvert.PopulateObject(json, t);
            return t;
        }
    }
}
