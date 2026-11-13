using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>
    /// Building types (ECONOMY_DESIGN section 3). Ids 0-13 keep their old numbers (Quarry is now the Clay pit,
    /// Grain mill the Mill, Ore pit the Mine, Refinery the Smelter); 14-19 are new.
    /// </summary>
    public enum BuildingType
    {
        Storehouse = 0,
        Forester = 1,
        Woodcutter = 2,
        Sawmill = 3,
        ClayPit = 4,
        Farm = 5,
        Mill = 6,
        Bakery = 7,
        Mine = 8,
        Smelter = 9,
        PowerPlant = 10,
        Barracks = 11,
        Market = 12,
        WonderSite = 13,
        Sheepfold = 14,
        Butcher = 15,
        Tavern = 16,
        CharcoalKiln = 17,
        BrickKiln = 18,
        Smithy = 19,
    }

    /// <summary>Construction size class; the cost in Planks and Bricks follows from it (EconomyTuning.SizeCost).</summary>
    public enum BuildingSize
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        Huge = 3,
    }

    /// <summary>How a building behaves in the economy.</summary>
    public enum BuildingKind
    {
        /// <summary>The storehouse: default source and sink.</summary>
        Store,
        /// <summary>Passive: keeps forest batches whole for the forest hexes it touches.</summary>
        Forester,
        /// <summary>Works one cycle per unit of an authorised batch on a touching hex (the Mine also uses up Rations).</summary>
        Harvester,
        /// <summary>Recipe: inputs to outputs per cycle.</summary>
        Producer,
        /// <summary>Burns coal; <see cref="EconomyApi.IsFuelled"/> while fuel time is left.</summary>
        PowerPlant,
        /// <summary>Construction and a store that receives requested wares; other modules add behaviour.</summary>
        Plain,
    }

    public enum BuildingState
    {
        /// <summary>Construction site waiting for its cost wares.</summary>
        Site = 0,
        /// <summary>All wares arrived; building for BuildTicks.</summary>
        Constructing = 1,
        Active = 2,
        /// <summary>Cancelled site. Kept in the list so ids stay stable.</summary>
        Removed = 3,
    }

    public enum Priority
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }

    public enum JobState
    {
        /// <summary>Created, ware reserved at the source, no carrier yet.</summary>
        Waiting = 0,
        /// <summary>Carrier walking to the source.</summary>
        ToPickup = 1,
        /// <summary>Ware is on the carrier.</summary>
        Carrying = 2,
    }

    public enum CarrierState
    {
        Idle = 0,
        ToPickup = 1,
        Carrying = 2,
        /// <summary>Walking back to the storehouse; can take a job at the next flag.</summary>
        Returning = 3,
    }

    /// <summary>All economy state (in <c>WorldState.Economy</c>). Plain data; ids are list indices.</summary>
    public sealed class EconomyState
    {
        public EconomyTuning Tuning = new EconomyTuning();

        /// <summary>Current round, starts at 1.</summary>
        public int Round = 1;
        /// <summary>True between EndRound (or game start) and the round-start draw at the next Rounds tick.</summary>
        public bool RoundStartPending = true;
        /// <summary>Production numbers drawn at the start of the current round (ascending).</summary>
        public int[] LastDraw = new int[0];
        public Rng RoundRng = new Rng();

        /// <summary>Per hex id: round in which a batch was last authorised (0 = never).</summary>
        public int[] HexBatchRound = new int[0];
        /// <summary>Per hex id: raw units still to harvest.</summary>
        public int[] HexBatchRemaining = new int[0];
        /// <summary>
        /// Per hex id: true while the robber stops the hex (a demon on every player city touching it). Refreshed at
        /// the start of every economy tick from EconomyHooks.IsHexStopped; read by views and diagnostics.
        /// </summary>
        public bool[] HexStopped = new bool[0];

        /// <summary>Per road segment id (BoardTopology fine edge): true when the player has a road there.</summary>
        public bool[] Roads = new bool[0];
        /// <summary>Road segments already paid for (see EconomyTuning.RoadSegmentsPerCost); used before paying again.</summary>
        public int RoadCredit;

        public List<City> Cities = new List<City>();
        /// <summary>Index = building id. Cancelled sites stay with State = Removed.</summary>
        public List<Building> Buildings = new List<Building>();
        /// <summary>Open delivery jobs (finished jobs are removed), ascending id.</summary>
        public List<DeliveryJob> Jobs = new List<DeliveryJob>();
        public int NextJobId;
        /// <summary>Index = carrier id.</summary>
        public List<Carrier> Carriers = new List<Carrier>();

        public int StorehouseId;
        /// <summary>Demonstration road flags (2 segments along the home forest hex edge): storehouse -> DemoVertexA -> DemoVertexB (pre-placed Forester site).</summary>
        public int DemoVertexA = -1;
        public int DemoVertexB = -1;

        public WareLedger Ledger = new WareLedger();
        public CommissionState Commission = new CommissionState();

        /// <summary>Effects of built cards on the economy, written by the strategy module (EconomyApi.SetBonuses).</summary>
        public EconomyBonuses Bonus = new EconomyBonuses();
    }

    /// <summary>Card effects the economy applies (kept in state so static API calls see them).</summary>
    public sealed class EconomyBonuses
    {
        /// <summary>Per building type: extra units of its main output per cycle.</summary>
        public int[] ExtraOutput = new int[BuildingDefs.Count];
        /// <summary>Extra road segments paid for by one road cost.</summary>
        public int RoadSegments;
    }

    /// <summary>A city: occupies a hex corner flag, extends territory and brings its touching hexes into production. No store.</summary>
    public sealed class City
    {
        public int Id;
        public int Owner;
        public int Vertex;
    }

    /// <summary>A building or construction site on a vertex flag, with its own store.</summary>
    public sealed class Building
    {
        public int Id;
        public BuildingType Type;
        public int Owner;
        public int Vertex;
        public BuildingState State;
        public bool Enabled = true;
        public Priority Priority = Priority.Normal;

        /// <summary>Construction ticks done (Constructing).</summary>
        public int BuildProgress;
        /// <summary>Ticks into the current work cycle; 0 = idle.</summary>
        public int WorkProgress;
        /// <summary>Hex being harvested in the current cycle, or -1.</summary>
        public int WorkHex = -1;
        /// <summary>Power plant: ticks of fuel left.</summary>
        public int FuelLeft;
        /// <summary>Harvester with an upkeep (the Mine): cycles already paid for by consumed inputs.</summary>
        public int InputCredit;
        /// <summary>
        /// True while a demon or a raid holds this building's flag: it does no work (harvest, recipe, construction,
        /// fuel, training, wonder stage). Refreshed at the start of every economy tick from EconomyHooks.IsVertexBlocked.
        /// </summary>
        public bool Blocked;

        /// <summary>Per ware (index = (int)Ware): wares physically here (sites: delivered materials).</summary>
        public int[] Stock = new int[Wares.Count];
        /// <summary>Per ware: part of Stock promised to a delivery job (not spendable).</summary>
        public int[] Reserved = new int[Wares.Count];
        /// <summary>Per ware: open jobs delivering here.</summary>
        public int[] Incoming = new int[Wares.Count];
        /// <summary>Per ware: externally requested units not yet assigned to a job (<see cref="EconomyApi.RequestWares"/>).</summary>
        public int[] Requested = new int[Wares.Count];

        /// <summary>Current stall reason (<see cref="Reasons"/>) or null when progressing/fine. Set by diagnostics.</summary>
        public string Reason;
        /// <summary>Ware named by the reason, or -1.</summary>
        public int ReasonWare = -1;

        public int Available(Ware w) => Stock[(int)w] - Reserved[(int)w];
    }

    /// <summary>One ware moving from a source building to a destination building or site.</summary>
    public sealed class DeliveryJob
    {
        public int Id;
        public Ware Ware;
        public int SourceId;
        public int DestId;
        public JobState State;
        public int CarrierId = -1;
        /// <summary>Stall reason (no_carrier, no_road, demon_on_route) or null while progressing.</summary>
        public string Reason;
        public long CreatedTick;
        /// <summary>Tick since which a not-yet-picked-up job has had no route (-1 = routed).</summary>
        public long RouteStalledSince = -1;
        /// <summary>Created for an external request (<see cref="EconomyApi.RequestWares"/>); restored if released.</summary>
        public bool FromRequest;
    }

    /// <summary>
    /// A carrier walking vertex to vertex along roads. At a flag: CurrentEdge = -1 and AtVertex is the flag.
    /// On an edge: CurrentEdge, FromVertex -> ToVertex, Progress ticks of TicksPerEdge done (interpolate).
    /// </summary>
    public sealed class Carrier
    {
        public int Id;
        public CarrierState State;
        public int AtVertex;
        public int CurrentEdge = -1;
        public int FromVertex = -1;
        public int ToVertex = -1;
        public int Progress;
        public bool HasLoad;
        public Ware Load;
        public int JobId = -1;
        /// <summary>Ticks spent idle away from the storehouse (it walks home after Tuning.CarrierWaitTicks).</summary>
        public int IdleTicks;
    }

    /// <summary>
    /// Ware accounting per ware (index = (int)Ware). In world = Starting + Harvested + Produced + Added - Consumed.
    /// Consumed is also split by sink: Consumed = sum(RecipeIn) + Construction + Roads + External, and
    /// Produced = sum(RecipeOut). RecipeIn/RecipeOut/Cycles are indexed by (int)BuildingType (recipe inputs,
    /// mine upkeep and power plant fuel count as RecipeIn of that building type).
    /// </summary>
    public sealed class WareLedger
    {
        public int[] Starting = new int[Wares.Count];
        public int[] Harvested = new int[Wares.Count];
        public int[] Produced = new int[Wares.Count];
        /// <summary>Added from outside the economy (<see cref="EconomyApi.AddToStorehouse"/>).</summary>
        public int[] Added = new int[Wares.Count];
        /// <summary>Recipe inputs, mine upkeep, fuel, construction, roads and external spending.</summary>
        public int[] Consumed = new int[Wares.Count];

        /// <summary>Per building type, per ware: inputs used up (recipes, mine rations, power plant coal).</summary>
        public int[][] RecipeIn = NewTable();
        /// <summary>Per building type, per ware: outputs made (recipes) or harvested (harvesters).</summary>
        public int[][] RecipeOut = NewTable();
        /// <summary>Per building type: finished work cycles (recipe cycles, harvest cycles, fuel units burned).</summary>
        public int[] Cycles = new int[BuildingDefs.Count];
        /// <summary>Per ware: used up by finished construction sites.</summary>
        public int[] Construction = new int[Wares.Count];
        /// <summary>Per ware: paid for road segments.</summary>
        public int[] Roads = new int[Wares.Count];
        /// <summary>Per raw ware: batch units authorised by round draws (after modifiers), and the part lost to the carry-over cap.</summary>
        public int[] BatchAuthorised = new int[Wares.Count];
        public int[] BatchLost = new int[Wares.Count];
        /// <summary>Per ware: spent through EconomyApi.TryConsume / TrySpendFromStorehouse by other modules (cities, cards, wonder, market, soldiers).</summary>
        public int[] External = new int[Wares.Count];

        public static int[][] NewTable()
        {
            var t = new int[BuildingDefs.Count][];
            for (int i = 0; i < t.Length; i++) t[i] = new int[Wares.Count];
            return t;
        }
    }

    public sealed class CommissionState
    {
        public bool Complete;
        public long CompletedTick = -1;
    }
}
