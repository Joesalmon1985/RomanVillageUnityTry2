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

        /// <summary>
        /// Named places kept through every era (PRD_AGES 4.1, A.2), sorted by id (ordinal). Read with
        /// <see cref="Eras.LandmarkApi"/>. Seeded at a new game (hearths, dance circle, harbour); towers are placed by
        /// the crystal campaign, the relay by M1, the district by F1.
        /// </summary>
        public System.Collections.Generic.List<Eras.Landmark> Landmarks = new System.Collections.Generic.List<Eras.Landmark>();

        /// <summary>Ports on the Meadow rim (BD-5), in the fixed spot order of <see cref="BoardGenerator.PortHexes"/>. Empty on the legacy board.</summary>
        public System.Collections.Generic.List<Port> Ports = new System.Collections.Generic.List<Port>();

        /// <summary>The god board deal (BD-4): deals tried (0 = not a dealt board) and whether the kept deal met BD-4.</summary>
        public int DealTriesUsed;
        public bool DealFair;

        [JsonIgnore]
        public BoardTopology Topology => BoardTopology.Get(Radius);

        public Terrain TerrainAt(int hex) => HexTerrain[hex];
        public int NumberAt(int hex) => HexNumber[hex];
    }

    /// <summary>
    /// A port on a Meadow rim hex (BD-5): a settlement on one of <see cref="Corners"/> trades at <see cref="Ratio"/>:1,
    /// any good when <see cref="Good"/> is null (3:1), else only that raw good (2:1).
    /// </summary>
    public sealed class Port
    {
        /// <summary>The Meadow rim hex the port sits on.</summary>
        public int Hex;
        /// <summary>The hex's settlement corners (where three hexes meet), ascending.</summary>
        public int[] Corners = new int[0];
        public int Ratio;
        public Ware? Good;
    }
}
