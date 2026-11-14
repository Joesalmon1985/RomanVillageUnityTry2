using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace World.Sim.Encounters.Ward
{
    /// <summary>
    /// An opponent's timing, mistakes and essence pool as data (PRD_CRYSTALS 5.6, 5.12, A.5). The duel rules do not
    /// change: <see cref="WardTuning.For"/> copies these numbers into the existing Mira* fields. Shell landed by
    /// PRD_CRYSTALS I0; package I3 owns this file (the profile file is StreamingAssets/world/ward_opponents.json,
    /// read through <see cref="WardOpponents"/>).
    /// </summary>
    public sealed class WardOpponentProfile
    {
        /// <summary>Profile id: practice_mira, keeper, keeper_earth|fire|water|air, rival_hesk, friendly_orrin, finale_unweaver.</summary>
        public string Id;
        public string DisplayName;
        /// <summary>Delay before the first cast, in ms (uniform in the range).</summary>
        public int FirstCastMinMs, FirstCastMaxMs;
        /// <summary>Delay between casts, in ms (uniform in the range). Longer means a slower, easier opponent.</summary>
        public int ThinkMinMs, ThinkMaxMs;
        /// <summary>Percent of casts that ignore the feedback and are random (the opponent's guessing mistakes).</summary>
        public int MistakeRatePercent;
        /// <summary>Casts the opponent may make; 0 keeps the base tuning's MaxCasts.</summary>
        public int MaxCasts;
        /// <summary>The essence pool of both sides in this duel; null or empty = the caller decides (e.g. WardFormat.For).</summary>
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public Essence[] Pool;

        // Presentation hints. The simulation never reads these; the animation layer may.
        /// <summary>"slow", "measured", "brisk" or "hasty": how the opponent carries itself while casting.</summary>
        public string CastTempo = "measured";
        /// <summary>Suggested length of the opponent's cast animation, in ms (shorter than ThinkMinMs).</summary>
        public int CastAnimMs = 1200;
        /// <summary>Free text for the stager, e.g. "stone-heavy, unhurried".</summary>
        public string StyleNote = "";
    }
}
