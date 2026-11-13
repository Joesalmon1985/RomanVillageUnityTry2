using System.Collections.Generic;

namespace World.Sim.Economy
{
    public enum BuildingType
    {
        Storehouse = 0,
        Forester = 1,
        Woodcutter = 2,
        Sawmill = 3,
        Quarry = 4,
        Farm = 5,
        GrainMill = 6,
        Bakery = 7,
        OrePit = 8,
        Refinery = 9,
        PowerPlant = 10,
        Barracks = 11,
        Market = 12,
        WonderSite = 13,
    }

    /// <summary>How a building behaves in the economy.</summary>
    public enum BuildingKind
    {
        /// <summary>The storehouse: default source and sink.</summary>
        Store,
        /// <summary>Passive: keeps forest batches whole for the forest hexes it touches.</summary>
        Forester,
        /// <summary>Works one cycle per unit of an authorised batch on a touching hex.</summary>
        Harvester,
        /// <summary>Recipe: inputs to outputs per cycle.</summary>
        Producer,
        /// <summary>Burns timber; <see cref="EconomyApi.IsFuelled"/> while fuel time is left.</summary>
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

        /// <summary>Per edge id: true when the player has a road there.</summary>
        public bool[] Roads = new bool[0];

        public List<City> Cities = new List<City>();
        /// <summary>Index = building id. Cancelled sites stay with State = Removed.</summary>
        public List<Building> Buildings = new List<Building>();
        /// <summary>Open delivery jobs (finished jobs are removed), ascending id.</summary>
        public List<DeliveryJob> Jobs = new List<DeliveryJob>();
        public int NextJobId;
        /// <summary>Index = carrier id.</summary>
        public List<Carrier> Carriers = new List<Carrier>();

        public int StorehouseId;
        /// <summary>Demonstration road flags: storehouse -> DemoVertexA -> DemoVertexB (pre-placed Forester site).</summary>
        public int DemoVertexA = -1;
        public int DemoVertexB = -1;

        public WareLedger Ledger = new WareLedger();
        public CommissionState Commission = new CommissionState();
    }

    /// <summary>A city: occupies a vertex, extends territory and brings its touching hexes into production. No store.</summary>
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
    }

    /// <summary>Ware accounting per ware (index = (int)Ware). In world = Starting + Harvested + Produced + Added - Consumed.</summary>
    public sealed class WareLedger
    {
        public int[] Starting = new int[Wares.Count];
        public int[] Harvested = new int[Wares.Count];
        public int[] Produced = new int[Wares.Count];
        /// <summary>Added from outside the economy (<see cref="EconomyApi.AddToStorehouse"/>).</summary>
        public int[] Added = new int[Wares.Count];
        /// <summary>Recipe inputs, fuel, construction, roads and external spending.</summary>
        public int[] Consumed = new int[Wares.Count];
    }

    public sealed class CommissionState
    {
        public bool Complete;
        public long CompletedTick = -1;
    }
}
