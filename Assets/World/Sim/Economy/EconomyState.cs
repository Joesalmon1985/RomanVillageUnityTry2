using System.Collections.Generic;

namespace World.Sim.Economy
{
    public enum BuildingState
    {
        /// <summary>Construction site waiting for its goods.</summary>
        Site = 0,
        /// <summary>All goods arrived; building for BuildTicks.</summary>
        Constructing = 1,
        Active = 2,
        /// <summary>A primitive collector replaced by an improved one: finishes its cycle, then its store is carried away.</summary>
        Retiring = 3,
        /// <summary>Gone; the plot is free. Kept in the list so ids stay stable.</summary>
        Retired = 4,
        /// <summary>A cancelled site (API change log, S-0). Kept in the list so ids stay stable; the plot is free.</summary>
        Cancelled = 5,
    }

    public enum SettlementState
    {
        Site = 0,
        Active = 1,
        /// <summary>A cancelled settlement site (API change log, S-0). Kept so ids stay stable.</summary>
        Cancelled = 2,
    }

    public enum UpgradeState { None = 0, Site = 1, Constructing = 2 }

    public enum RoadState
    {
        Site = 0,
        Constructing = 1,
        Built = 2,
        /// <summary>A cancelled road site (API change log, S-0). Kept so ids stay stable; RoadOnEdge no longer points to it.</summary>
        Cancelled = 3,
    }

    /// <summary>Kinds of construction site (CancelSite, SitePlaced...). City: Id is the settlement id.</summary>
    public enum SiteKind { Building = 0, Road = 1, Settlement = 2, City = 3 }

    /// <summary>Ends of a job or carrier leg. Storehouse: id = settlement; Building: building; RoadSite: road; SettlementSite: settlement.</summary>
    public enum PlaceKind { Storehouse = 0, Building = 1, RoadSite = 2, SettlementSite = 3 }

    public enum JobState
    {
        /// <summary>Created, no carrier yet.</summary>
        Waiting = 0,
        /// <summary>Carrier walking to the pickup.</summary>
        ToPickup = 1,
        /// <summary>Goods on the carrier.</summary>
        Carrying = 2,
    }

    public enum CarrierState { Idle = 0, ToPickup = 1, Carrying = 2, Returning = 3 }

    /// <summary>All economy state (WorldState.Economy). Plain data; ids are list indices.</summary>
    public sealed class EconomyState
    {
        public EconomyTuning Tuning = new EconomyTuning();
        public DiceState Dice = new DiceState();
        /// <summary>Per hex id (61).</summary>
        public HexStock[] Hexes = new HexStock[0];
        public List<Settlement> Settlements = new List<Settlement>();
        /// <summary>Retired and cancelled buildings stay in the list.</summary>
        public List<Building> Buildings = new List<Building>();
        public List<Road> Roads = new List<Road>();
        /// <summary>Per hex edge id (210): the road on it, or -1.</summary>
        public int[] RoadOnEdge = new int[0];
        /// <summary>Open jobs, ascending id.</summary>
        public List<Job> Jobs = new List<Job>();
        public int NextJobId;
        public List<Carrier> Carriers = new List<Carrier>();
        public List<Warrior> Warriors = new List<Warrior>();
        public WareLedger Ledger = new WareLedger();
    }

    /// <summary>The dice: a deck of the 36 ordered outcomes encoded d1 * 10 + d2, top = last.</summary>
    public sealed class DiceState
    {
        public int[] Deck = new int[0];
        public Rng Rng = new Rng();
        public long NextRollTick;
        public int RollCount;
        public int LastD1;
        public int LastD2;
        /// <summary>-1 before the first roll.</summary>
        public long LastRollTick = -1;
    }

    /// <summary>Stock on one hex: young cohorts (in adding order) and mature units.</summary>
    public sealed class HexStock
    {
        public int Hex;
        public int Mature;
        public List<StockCohort> Young = new List<StockCohort>();
    }

    public sealed class StockCohort
    {
        public Ware Ware;
        public int Count;
        public long AddedTick;
        public long MatureTick;
    }

    /// <summary>A camp ("Settlement" in code). Ware arrays are indexed by ware id.</summary>
    public sealed class Settlement
    {
        public int Id;
        public int Faction;
        public int Corner;
        public SettlementState State;
        public bool IsCity;
        public UpgradeState Upgrade;
        /// <summary>Settlement site: the settlement its goods come from. -1 for a starting settlement.</summary>
        public int SourceSettlement = -1;
        public int BuildProgress;
        public int UpgradeProgress;
        /// <summary>Storehouse stock (includes reserved units).</summary>
        public int[] Stock = new int[Wares.Count];
        public int[] Reserved = new int[Wares.Count];
        /// <summary>Units on the way to the storehouse (open jobs heading there).</summary>
        public int[] Incoming = new int[Wares.Count];
        /// <summary>Settlement site: goods delivered so far.</summary>
        public int[] Delivered = new int[Wares.Count];
        /// <summary>City upgrade: goods moved from Stock so far.</summary>
        public int[] UpgradeDelivered = new int[Wares.Count];
        public long FoundedTick;
        /// <summary>Stall reason code (<see cref="Reasons"/>) or null.</summary>
        public string Reason;
        public int ReasonWare = -1;
    }

    public sealed class Building
    {
        public int Id;
        public BuildingType Type;
        public int Faction;
        public int Settlement;
        public int Plot;
        /// <summary>Hex of the plot's sector.</summary>
        public int Hex;
        public BuildingState State;
        /// <summary>Output store (collectors, workshops).</summary>
        public int[] Stock = new int[Wares.Count];
        public int[] Reserved = new int[Wares.Count];
        public int[] Incoming = new int[Wares.Count];
        /// <summary>Site: goods delivered; active workshop or lodge: inputs on hand.</summary>
        public int[] Delivered = new int[Wares.Count];
        public int BuildProgress;
        /// <summary>Ticks into the current cycle; 0 = no cycle running.</summary>
        public int WorkProgress;
        /// <summary>Hex the current collect cycle took its unit from, or -1.</summary>
        public int WorkHex = -1;
        public string Reason;
        public int ReasonWare = -1;
    }

    public sealed class Road
    {
        public int Id;
        public int Faction;
        public int HexEdge;
        public RoadState State;
        public int SourceSettlement = -1;
        public int[] Delivered = new int[Wares.Count];
        public int[] Incoming = new int[Wares.Count];
        public int BuildProgress;
        public string Reason;
        public int ReasonWare = -1;
    }

    public sealed class Job
    {
        public int Id;
        public int Faction;
        /// <summary>Settlement whose carrier does the job.</summary>
        public int Settlement;
        public Ware Ware;
        public int Count;
        public PlaceKind FromKind;
        public int FromId;
        public PlaceKind ToKind;
        public int ToId;
        public bool IsShipment;
        public JobState State;
        public int CarrierId = -1;
        public string Reason;
        public long CreatedTick;
    }

    public sealed class Carrier
    {
        public int Id;
        public int Faction;
        /// <summary>Home settlement id.</summary>
        public int Home;
        public CarrierState State;
        public int JobId = -1;
        public bool HasLoad;
        public Ware Load;
        public int LoadCount;
        public PlaceKind LegFromKind;
        public int LegFromId = -1;
        public PlaceKind LegToKind;
        public int LegToId = -1;
        /// <summary>Corner path of a main-road leg; empty for a local leg.</summary>
        public int[] LegCorners = new int[0];
        public int LegTicks;
        public int Progress;
    }

    public sealed class Warrior
    {
        public int Id;
        public int Faction;
        /// <summary>Lodge building id.</summary>
        public int Lodge;
        public int Settlement;
        public bool Fed;
        public long NextMealTick;
    }

    /// <summary>
    /// Accounting (REFACTOR_SPEC 4.4). Per-ware arrays have length <see cref="Wares.Count"/>; RecipeIn and RecipeOut
    /// are [building type][ware]; hex stock counters (StockAdded/Lost/Taken) are indexed by ware id too (only raw
    /// wares are ever non-zero).
    /// </summary>
    public sealed class WareLedger
    {
        public int[] Starting = new int[Wares.Count];
        public int[] Collected = new int[Wares.Count];
        public int[] Produced = new int[Wares.Count];
        public int[] Consumed = new int[Wares.Count];
        public int[][] RecipeIn = NewTable();
        public int[][] RecipeOut = NewTable();
        public int[] Construction = new int[Wares.Count];
        public int[] Training = new int[Wares.Count];
        public int[] Meals = new int[Wares.Count];
        public int[] StockAdded = new int[Wares.Count];
        public int[] StockLost = new int[Wares.Count];
        public int[] StockTaken = new int[Wares.Count];

        private static int[][] NewTable()
        {
            var t = new int[BuildingDefs.Count][];
            for (int i = 0; i < t.Length; i++) t[i] = new int[Wares.Count];
            return t;
        }
    }
}
