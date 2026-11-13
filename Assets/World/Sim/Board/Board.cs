using Newtonsoft.Json;

namespace World.Sim
{
    /// <summary>
    /// Serialisable board data (lives in WorldState.Board). Arrays are indexed by hex id of <see cref="Topology"/>.
    /// Corner ids (0..149) and hex edge ids (0..209) are those of the same topology. Occupancy (settlements,
    /// buildings, roads, demons) belongs to the modules, not here. Topology is derived, never stored.
    /// </summary>
    public sealed class Board
    {
        public int Radius;

        /// <summary>Terrain per hex id.</summary>
        public Terrain[] HexTerrain;

        /// <summary>Production number 2..12 (never 7) per hex id; 0 for Meadow.</summary>
        public int[] HexNumber;

        /// <summary>Starting settlement corners, index = people: [0] the player's (70), [1] the rival's (97).</summary>
        public int[] StartCorners = new int[0];

        /// <summary>
        /// Good settlement corners for hints and bots, nearest to the player's start first: interior corners with a
        /// mixed economy, not next to a start corner and at least <see cref="BoardGenerator.SuggestedSiteSpacing"/>
        /// hex edges from each other and from the rival's start.
        /// </summary>
        public int[] SuggestedSettlementCorners = new int[0];

        /// <summary>Plot ids never buildable: the dance circle (slot 2 of the home camp's Fields sector).</summary>
        public int[] ReservedPlots = new int[0];

        [JsonIgnore]
        public BoardTopology Topology => BoardTopology.Get(Radius);

        public Terrain TerrainAt(int hex) => HexTerrain[hex];
        public int NumberAt(int hex) => HexNumber[hex];
    }
}
