namespace World.Sim.Encounters.Ward
{
    /// <summary>Saved Ward record (PRD W13). Plain data.</summary>
    public sealed class WardProgress
    {
        public int CompletedDuelsVsMira;
        public int Wins;
        public int Losses;
        public int Draws;

        /// <summary>Counts a finished duel. A forfeit is not a completed duel.</summary>
        public void Record(WardOutcome outcome)
        {
            switch (outcome)
            {
                case WardOutcome.Victory: Wins++; break;
                case WardOutcome.Defeat: Losses++; break;
                case WardOutcome.Stalemate:
                case WardOutcome.Clash: Draws++; break;
                default: return;
            }
            CompletedDuelsVsMira++;
        }
    }
}
