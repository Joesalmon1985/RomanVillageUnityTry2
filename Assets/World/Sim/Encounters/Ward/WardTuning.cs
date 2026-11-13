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
    }
}
