namespace World.Sim.Military
{
    // Events for views. Plain public fields; Tick is stamped by EventBuffer.

    public sealed class UnitQueued : SimEvent { public int BarracksId; public int QueueLength; }

    /// <summary>The head order took its supplies from the barracks store; training starts.</summary>
    public sealed class TrainingStarted : SimEvent { public int BarracksId; }

    public sealed class UnitTrained : SimEvent { public int BarracksId; public int SquadId; public int UnitId; }

    /// <summary>The barracks queue stalled or resumed (Reason null = progressing).</summary>
    public sealed class QueueStateChanged : SimEvent { public int BarracksId; public string Reason; public int ReasonWare = -1; }

    /// <summary>A consumer gained or lost power.</summary>
    public sealed class PowerChanged : SimEvent { public int BuildingId; public bool Powered; }

    public sealed class SquadOrdered : SimEvent { public int SquadId; public OrderKind Order; public int Target; }

    /// <summary>A squad stepped onto an edge (views animate From -> To over TicksPerEdge).</summary>
    public sealed class SquadMoved : SimEvent { public int SquadId; public int From; public int To; }

    public sealed class SquadArrived : SimEvent { public int SquadId; public int Vertex; }

    public sealed class SquadEngaged : SimEvent { public int Vertex; public int[] SquadIds; }

    /// <summary>One attack. TargetUnitId -1 = the outpost.</summary>
    public sealed class UnitHit : SimEvent { public int Vertex; public int AttackerUnitId; public int TargetUnitId; public int Damage; public int TargetHpLeft; }

    public sealed class UnitDied : SimEvent { public int SquadId; public int UnitId; public int Vertex; }

    public sealed class SquadDestroyed : SimEvent { public int SquadId; public int Owner; public int Vertex; }

    public sealed class OutpostDamaged : SimEvent { public int Hp; public int MaxHp; }

    public sealed class OutpostDestroyed : SimEvent { public int Vertex; }

    public sealed class GarrisonReinforced : SimEvent { public int Guards; }

    public sealed class RaidLaunched : SimEvent { public int SquadId; public int Target; public int Units; }

    /// <summary>A raid reached its target flag unopposed and blocks it.</summary>
    public sealed class RaidOccupied : SimEvent { public int SquadId; public int Vertex; }

    /// <summary>A raid left (going home), merged into the garrison, or was disbanded when the outpost fell.</summary>
    public sealed class RaidEnded : SimEvent { public int SquadId; public string How; }
}
