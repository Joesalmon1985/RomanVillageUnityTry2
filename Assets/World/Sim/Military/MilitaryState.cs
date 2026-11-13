using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>Military: power allocation, barracks queues, squads, combat, rival outpost and raids.</summary>
        public Military.MilitaryState Military;
    }
}

namespace World.Sim.Military
{
    public enum SquadKind
    {
        /// <summary>Player squad formed at a barracks.</summary>
        Field = 0,
        /// <summary>Rival guards at the outpost.</summary>
        Garrison = 1,
        /// <summary>Rival raid party.</summary>
        Raid = 2,
    }

    public enum OrderKind
    {
        None = 0,
        /// <summary>Walk to the target flag and hold it.</summary>
        Defend = 1,
        /// <summary>Walk to the target flag and fight whatever is there (the outpost, a raid).</summary>
        Attack = 2,
        /// <summary>Rival raid going home.</summary>
        Return = 3,
    }

    /// <summary>All military state (in <c>WorldState.Military</c>). Plain data; squads indexed by id.</summary>
    public sealed class MilitaryState
    {
        public MilitaryTuning Tuning = new MilitaryTuning();
        /// <summary>Index = squad id. Destroyed or merged squads stay with Alive = false.</summary>
        public List<Squad> Squads = new List<Squad>();
        public int NextUnitId;
        /// <summary>One queue per barracks that ever had an order (ascending building id).</summary>
        public List<BarracksQueue> Queues = new List<BarracksQueue>();
        public OutpostState Outpost = new OutpostState();
        /// <summary>Consumers without power after the last Power tick (ascending building id).</summary>
        public List<int> Unpowered = new List<int>();
        public int UnitsTrained;
        /// <summary>Wares consumed at barracks for units, per ware.</summary>
        public int[] ConsumedForUnits = new int[Wares.Count];
    }

    public sealed class Unit
    {
        public int Id;
        public int Hp;
        /// <summary>Ticks until the unit can attack again.</summary>
        public int Cooldown;
    }

    /// <summary>
    /// A group of units walking vertex to vertex over any edge. At a flag: CurrentEdge = -1 and At is the
    /// flag. On an edge: FromVertex -> ToVertex, Progress of TicksPerEdge done (interpolate for views).
    /// </summary>
    public sealed class Squad
    {
        public int Id;
        public int Owner;
        public SquadKind Kind;
        public bool Alive = true;
        public List<Unit> Units = new List<Unit>();
        public int At;
        public int CurrentEdge = -1;
        public int FromVertex = -1;
        public int ToVertex = -1;
        public int Progress;
        public OrderKind Order;
        public int Target = -1;
        /// <summary>True while fighting at its flag.</summary>
        public bool Engaged;
        /// <summary>Field squads: barracks building that trained it.</summary>
        public int BarracksId = -1;
        /// <summary>Raid: holds its target flag (blocks it like a demon).</summary>
        public bool Occupying;
        /// <summary>Raid: the age-run round at whose end it goes home.</summary>
        public int LeaveAfterRound;
    }

    public sealed class QueuedUnit
    {
        /// <summary>Supplies taken from the barracks store; training may run.</summary>
        public bool Supplied;
        public int Progress;
        public int Rations;
        public int Weapons;
    }

    public sealed class BarracksQueue
    {
        public int BuildingId;
        public List<QueuedUnit> Orders = new List<QueuedUnit>();
        /// <summary>Squad that new units join (while it is at the barracks, idle and not full).</summary>
        public int HomeSquadId = -1;
        /// <summary>Stall reason of the head order (<see cref="Reasons"/>) or null.</summary>
        public string Reason;
        public int ReasonWare = -1;
    }

    public sealed class OutpostState
    {
        public int Vertex = -1;
        public int Hp;
        public int MaxHp;
        public bool Destroyed;
        public long DestroyedTick = -1;
        public int GarrisonSquadId = -1;
    }
}
