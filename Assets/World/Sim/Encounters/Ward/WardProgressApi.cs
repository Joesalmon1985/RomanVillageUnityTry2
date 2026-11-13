namespace World.Sim.Encounters.Ward
{
    /// <summary>
    /// The one sanctioned write from the game layer into WorldState (M2): recording a finished practice duel.
    /// Why not a command: WorldSimFactory does not register encounter handlers, and queued commands only apply on the
    /// next sim tick, while the village clock stays paused for the whole encounter (a rematch must already see the new
    /// count, e.g. Light on duel 4). This touches nothing but <see cref="WorldState.Ward"/>, is deterministic, and the
    /// result is saved with WorldState. Forfeits and unfinished duels are not recorded.
    /// </summary>
    public static class WardProgressApi
    {
        /// <summary>Records the outcome once. Returns true if it counted as a completed duel.</summary>
        public static bool RecordDuel(WorldState state, WardOutcome outcome)
        {
            if (state == null) return false;
            if (state.Ward == null) state.Ward = new WardProgress();
            int before = state.Ward.CompletedDuelsVsMira;
            state.Ward.Record(outcome);
            return state.Ward.CompletedDuelsVsMira > before;
        }

        /// <summary>Essence pool for the next duel from the saved count.</summary>
        public static Essence[] NextPool(WorldState state, WardTuning tuning)
        {
            int n = state != null && state.Ward != null ? state.Ward.CompletedDuelsVsMira : 0;
            return WardFormat.For(n, tuning);
        }
    }
}
