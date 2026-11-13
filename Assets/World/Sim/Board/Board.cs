using Newtonsoft.Json;

namespace World.Sim
{
    /// <summary>
    /// Serialisable board data (lives in WorldState.Board). Arrays are indexed by hex id of
    /// <see cref="Topology"/>. Vertex ids refer to the same topology. Occupancy of vertices and
    /// edges (cities, buildings, roads, demons) belongs to the modules, not here.
    /// </summary>
    public sealed class Board
    {
        public int Radius;

        /// <summary>Terrain per hex id.</summary>
        public Terrain[] HexTerrain;

        /// <summary>Production number 1..4 per hex id; 0 for Meadow.</summary>
        public int[] HexNumber;

        /// <summary>Interior vertex of the player's first city (touches Forest, Hills and Fields).</summary>
        public int HomeCityVertex = -1;

        /// <summary>Suggested vertex for the rival outpost: far from home, not touching a Mountain hex.</summary>
        public int RivalOutpostVertex = -1;

        /// <summary>Suggested village square flags. Storehouse and market are at the home city.</summary>
        public int StorehouseVertex = -1;
        public int MarketVertex = -1;
        /// <summary>Suggested flag for the warehouse (rhythm party venue): a neighbour of the home city.</summary>
        public int WarehouseVertex = -1;

        [JsonIgnore]
        public BoardTopology Topology => BoardTopology.Get(Radius);

        public Terrain TerrainAt(int hex) => HexTerrain[hex];
        public int NumberAt(int hex) => HexNumber[hex];
    }
}
