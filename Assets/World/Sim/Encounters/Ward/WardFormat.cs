namespace World.Sim.Encounters.Ward
{
    public static class WardFormat
    {
        /// <summary>Essence pool for the next duel: Flame, Frost, Vine; Light once enough duels are completed.</summary>
        public static Essence[] For(int completedDuelsVsMira, WardTuning tuning)
        {
            if (tuning == null) tuning = new WardTuning();
            if (completedDuelsVsMira >= tuning.LightUnlockAfterDuels)
                return new[] { Essence.Flame, Essence.Frost, Essence.Vine, Essence.Light };
            return new[] { Essence.Flame, Essence.Frost, Essence.Vine };
        }
    }
}
