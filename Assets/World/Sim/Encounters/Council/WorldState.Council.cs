namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>Village councils: elections, listening and office holders (separate from victory points).</summary>
        public World.Sim.Encounters.Council.CouncilState Council = new World.Sim.Encounters.Council.CouncilState();
    }
}
