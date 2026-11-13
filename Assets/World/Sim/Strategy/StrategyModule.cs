namespace World.Sim.Strategy
{
    /// <summary>
    /// Entry point of the Strategy module. Owned by the Strategy agent; Core never changes for it.
    /// InitState adds this module's starting data to WorldState (fields declared in a
    /// partial WorldState file in this folder). Register adds systems (with SystemOrder slots)
    /// and command handlers to the sim. Called by WorldSimFactory.CreateNew.
    /// </summary>
    public static class StrategyModule
    {
        public static void Register(WorldSim sim)
        {
        }

        public static void InitState(WorldState s)
        {
        }
    }
}
