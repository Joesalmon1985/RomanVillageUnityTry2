namespace World.Sim.Encounters.Ward
{
    /// <summary>
    /// Everything a duel session needs (PRD_CRYSTALS 5.12, A.5): <see cref="WardDuelSession.Create"/> builds the same
    /// session as <c>new WardDuelSession(Tuning, Pool, Seed)</c>. Shell landed by PRD_CRYSTALS I0; package I3 owns this
    /// file from tag cry-I0-sim.
    /// </summary>
    public sealed class WardDuelSpec
    {
        public WardTuning Tuning;
        public Essence[] Pool;
        public ulong Seed;
    }
}
