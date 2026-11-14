namespace World.Sim.Encounters.Ward
{
    /// <summary>
    /// An opponent's timing, mistakes and essence pool as data (PRD_CRYSTALS 5.6, 5.12, A.5). The duel rules do not
    /// change: <see cref="WardTuning.For"/> copies these numbers into the existing Mira* fields. Shell landed by
    /// PRD_CRYSTALS I0; package I3 owns this file from tag cry-I0-sim (the profile file, the measured win rates).
    /// </summary>
    public sealed class WardOpponentProfile
    {
        /// <summary>Profile id, e.g. "practice_mira", "keeper", "friendly_orrin", "finale_unweaver".</summary>
        public string Id;
        public string DisplayName;
        public int FirstCastMinMs, FirstCastMaxMs, ThinkMinMs, ThinkMaxMs, MistakeRatePercent;
        /// <summary>The essence pool of both sides in this duel; null or empty = the caller decides (e.g. WardFormat.For).</summary>
        public Essence[] Pool;
    }
}
