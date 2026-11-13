using Newtonsoft.Json;

namespace World.Sim
{
    /// <summary>
    /// Serialisable board data (lives in WorldState.Board). Arrays are indexed by hex id of
    /// <see cref="Topology"/>. Vertex ids are FLAG ids of the same topology (fine level; corners are the
    /// flags below <see cref="BoardTopology.CornerCount"/>). Occupancy of flags and segments (cities,
    /// buildings, roads, demons) belongs to the modules, not here. Topology is derived, never stored.
    /// </summary>
    public sealed class Board
    {
        public int Radius;

        /// <summary>Terrain per hex id.</summary>
        public Terrain[] HexTerrain;

        /// <summary>Production number 1..4 per hex id; 0 for Meadow.</summary>
        public int[] HexNumber;

        /// <summary>Corner of the player's first city (interior; touches Forest, Hills and Fields).</summary>
        public int HomeCityVertex = -1;

        /// <summary>Corner of the rival outpost: 4-5 hex edges from home, touching its own Forest, Hills and Fields.</summary>
        public int RivalOutpostVertex = -1;

        /// <summary>Village square flags. Storehouse and market are at the home city corner.</summary>
        public int StorehouseVertex = -1;
        public int MarketVertex = -1;

        /// <summary>
        /// Flag kept for the warehouse (rhythm party venue): an intermediate flag 2 segments from the home
        /// corner, beside the square (not on it). No building may use it.
        /// </summary>
        public int WarehouseVertex = -1;

        /// <summary>
        /// Good city corners for bots and UI hints, nearest to home first: interior corners with a mixed
        /// economy, legal on the starting board (not next to home or the outpost) and at least 3 hex edges
        /// from each other. Excludes home and the outpost.
        /// </summary>
        public int[] SuggestedCitySites = new int[0];

        [JsonIgnore]
        public BoardTopology Topology => BoardTopology.Get(Radius);

        public Terrain TerrainAt(int hex) => HexTerrain[hex];
        public int NumberAt(int hex) => HexNumber[hex];
    }
}
