using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>
        /// The Historic age of a god board game (PRD_GODBOARD section 9, G1-8 / G4-1 / G4-2): castles, finite resources and
        /// the peoples' Historic economies. Null until the age has turned (and always in a Legacy game).
        /// </summary>
        public Historic.HistoricState Historic;
    }
}

namespace World.Sim.Historic
{
    /// <summary>Growth of one tree slot on a hex (HI-5). Stored as an int per slot.</summary>
    public enum TreeStage { Empty = 0, Sapling = 1, Young = 2, Mature = 3, Stump = 4 }

    /// <summary>One field slot on a Fields hex (HI-5).</summary>
    public enum FieldStage { Empty = 0, Sown = 1, Ripe = 2 }

    /// <summary>The Historic building kinds (data in <see cref="HistoricDefs"/>). Ids are saved; never reorder.</summary>
    public enum HistoricKind { Woodcutter = 0, Forester = 1, Quarry = 2, ClayPit = 3, Farm = 4, SheepFarm = 5, Kiln = 6 }

    public enum HistoricBuildingState
    {
        /// <summary>Waiting for its construction goods, then building for BuildTicks.</summary>
        Site = 0,
        Active = 1,
        /// <summary>A quarry whose rock is used up: it stops for good (the planner builds a new one elsewhere).</summary>
        Exhausted = 2,
    }

    /// <summary>Numbers of the Historic age (saved with the state; tune here). 10 ticks = 1 second.</summary>
    public sealed class HistoricTuning
    {
        // Trees (HI-5).
        public int ForestSlots = 12;
        public int MeadowSlots = 6;
        /// <summary>Mature trees on a Forest hex at the switch = TreesBase + TreesPerPip x pips (capped by the slots).</summary>
        public int TreesBase = 2;
        public int TreesPerPip = 2;
        /// <summary>Ticks per growth stage (sapling to young, young to mature).</summary>
        public int TreeGrowTicks = 600;
        /// <summary>A stump rots away (the slot becomes empty) after this long.</summary>
        public int StumpRotTicks = 900;
        public int WoodPerTree = 2;

        // Rock and clay.
        public int MountainRockBase = 10;
        public int MountainRockPerPip = 6;
        public int HillsRockBase = 4;
        public int HillsRockPerPip = 2;
        public int ClayBase = 6;
        public int ClayPerPip = 2;
        /// <summary>A Hills hex's clay seeps back one unit per this many ticks, up to its starting amount.</summary>
        public int ClayRefillTicks = 300;

        // Fields.
        public int FieldsSlotsBase = 2;
        public int MeadowFieldSlots = 0;
        public int FieldGrowTicks = 900;
        public int GrainPerHarvest = 2;

        // Carrying.
        public int CarryBaseTicks = 30;
        public int CarryPerHexTicks = 40;
        public int CarriersBase = 3;
        /// <summary>Most goods waiting in a building's output store before it stops.</summary>
        public int OutputCap = 4;

        // Stock at the switch, per people (its Historic store at the castle).
        public int[] StartStock = { 16, 4, 4, 0, 4, 0, 0, 0, 0, 0 };

        // Castle (HI-3): costs per stage (index 0 wood, 1 brick, 2 stone), ware ids and counts, and build ticks.
        public int[][] CastleCost =
        {
            new[] { 30, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 12, 0, 0, 0, 0, 0, 24, 0, 0, 0 },
            new[] { 12, 0, 8, 6, 30, 0, 0, 0, 0, 0 },
        };
        public int CastleStageTicks = 600;

        // The planner (HI-6).
        public int PlanIntervalTicks = 50;
        public int MaxOpenSites = 2;
        public int MaxWoodcutters = 4;
        public int MaxQuarries = 2;
        public int MaxClayPits = 3;
        /// <summary>Wood kept back in the store for building sites while the castle is fed.</summary>
        public int SiteReserveWood = 6;
        /// <summary>Most buildings (of all peoples) on one hex.</summary>
        public int MaxBuildingsPerHex = 3;
    }

    /// <summary>Resources of one hex (HI-5). Arrays are small and per hex: one int per tree slot or field slot.</summary>
    public sealed class HexResources
    {
        public int Hex;
        public Terrain Terrain;
        public int Pips;
        /// <summary>Per tree slot: <see cref="TreeStage"/> as int. Empty array on hexes where no tree grows.</summary>
        public int[] Trees = new int[0];
        /// <summary>Per tree slot: the tick of its next step (growth or rot); 0 when nothing is pending.</summary>
        public long[] TreeNext = new long[0];
        public int Rock;
        public int Clay;
        public int ClayCap;
        public long NextClayTick;
        /// <summary>Per field slot: <see cref="FieldStage"/> as int.</summary>
        public int[] Fields = new int[0];
        public long[] FieldNext = new long[0];
    }

    /// <summary>A people's castle (HI-3): three stages, each a construction site fed by the people's carriers.</summary>
    public sealed class Castle
    {
        public int Corner;
        /// <summary>Stages raised (0 to 3); the site being fed is stage <see cref="StagesDone"/> while it is below 3.</summary>
        public int StagesDone;
        public int[] Delivered = new int[Wares.Count];
        public int[] Incoming = new int[Wares.Count];
        public int BuildProgress;
        /// <summary>Tick each stage was raised (-1 before).</summary>
        public long[] RaisedTick = { -1, -1, -1 };
    }

    /// <summary>One people in Historic: its store (at the castle), carriers, castle and the wood measure (HI-6).</summary>
    public sealed class HistoricPeople
    {
        public int Id;
        /// <summary>The corner of the castle and the people's Historic store.</summary>
        public int StoreCorner;
        public int[] Stock = new int[Wares.Count];
        public int Carriers;
        public Castle Castle = new Castle();
        public long NextPlanTick;
        /// <summary>The tick wood last reached the store (StartTick at the switch).</summary>
        public long LastWoodTick;
        /// <summary>Longest stretch (ticks) in which no wood reached the store (the HI-6 measure), closed stretches only.</summary>
        public long LongestWoodGap;
        /// <summary>Since when the store has held no wood (-1 while it holds some).</summary>
        public long WoodEmptySince = -1;
        public long LongestWoodEmpty;
        public int TreesFelled;
        public int TreesPlanted;
        public int WoodDelivered;
    }

    /// <summary>A Historic building of a people on a hex (HI-5).</summary>
    public sealed class HistoricBuilding
    {
        public int Id;
        public int People;
        public HistoricKind Kind;
        public int Hex;
        public HistoricBuildingState State;
        /// <summary>Site: construction goods delivered. Active workshop: inputs on hand.</summary>
        public int[] Delivered = new int[Wares.Count];
        public int[] Incoming = new int[Wares.Count];
        public int BuildProgress;
        /// <summary>Ticks into the current cycle; 0 = idle.</summary>
        public int WorkProgress;
        /// <summary>What the current cycle does: 0 none, 1 the main work, 2 a farm's sowing.</summary>
        public int WorkMode;
        /// <summary>Goods made and waiting to be carried (ware <see cref="HistoricDef.Output"/>).</summary>
        public int Output;
        public long PlacedTick;
    }

    public enum CarryEnd { Store = 0, Building = 1, Castle = 2 }

    /// <summary>A carrier underway with one unit (the people's carriers are a count; each job is one carrier busy).</summary>
    public sealed class HistoricJob
    {
        public int Id;
        public int People;
        public Ware Ware;
        public CarryEnd From;
        public int FromId;
        public CarryEnd To;
        public int ToId;
        public int Ticks;
        public int Progress;
    }

    /// <summary>Accounting of Historic goods (balance: Starting + Produced = Consumed + stores + outputs + inputs on hand + carried).</summary>
    public sealed class HistoricLedger
    {
        public int[] Starting = new int[Wares.Count];
        public int[] Produced = new int[Wares.Count];
        public int[] Consumed = new int[Wares.Count];
    }

    /// <summary>WorldState.Historic.</summary>
    public sealed class HistoricState
    {
        public HistoricTuning Tuning = new HistoricTuning();
        public long StartTick;
        /// <summary>The people that ended the prehistoric age.</summary>
        public int Winner = -1;
        /// <summary>HI-1: the god board is closed for good.</summary>
        public bool TableClosed;
        /// <summary>HI-2: the temples are down (one per people, on its start corner).</summary>
        public int[] TempleCorners = new int[0];
        public HexResources[] Hexes = new HexResources[0];
        public List<HistoricPeople> Peoples = new List<HistoricPeople>();
        public List<HistoricBuilding> Buildings = new List<HistoricBuilding>();
        public List<HistoricJob> Jobs = new List<HistoricJob>();
        public int NextJobId;
        public HistoricLedger Ledger = new HistoricLedger();
    }
}
