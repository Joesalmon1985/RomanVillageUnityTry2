namespace World.Sim.Economy
{
    // Events for views. Plain public fields; Tick is stamped by EventBuffer.

    public sealed class RoadPlaced : SimEvent { public int Edge; public int PlayerId; }

    public sealed class RoadRemoved : SimEvent { public int Edge; public int PlayerId; }

    public sealed class SitePlaced : SimEvent { public int BuildingId; public BuildingType Type; public int Vertex; }

    /// <summary>A site finished building and is now Active.</summary>
    public sealed class SiteCompleted : SimEvent { public int BuildingId; public BuildingType Type; public int Vertex; }

    /// <summary>A site was cancelled; its delivered wares went back to the storehouse.</summary>
    public sealed class SiteCancelled : SimEvent { public int BuildingId; public BuildingType Type; public int Vertex; }

    /// <summary>Lifecycle state, enabled flag or stall reason of a building changed.</summary>
    public sealed class BuildingStateChanged : SimEvent
    {
        public int BuildingId;
        public BuildingState State;
        public bool Enabled;
        /// <summary>Reason code or null.</summary>
        public string Reason;
        public int ReasonWare = -1;
    }

    public sealed class JobCreated : SimEvent { public int JobId; public Ware Ware; public int SourceId; public int DestId; }

    public sealed class WarePickedUp : SimEvent { public int JobId; public int CarrierId; public Ware Ware; public int BuildingId; }

    /// <summary>A shipment arrived and was credited to BuildingId (once per job).</summary>
    public sealed class WareDelivered : SimEvent { public int JobId; public int CarrierId; public Ware Ware; public int BuildingId; }

    public sealed class JobStalled : SimEvent { public int JobId; public string Reason; }

    public sealed class JobResumed : SimEvent { public int JobId; }

    /// <summary>A job stalled on its route before pickup was dropped; its wares are free again and demand re-creates it.</summary>
    public sealed class JobReleased : SimEvent { public int JobId; public string Reason; }

    /// <summary>A harvester finished one batch unit: Amount units of Ware (Woodcutter 2, others 1) appeared in its store.</summary>
    public sealed class HarvestCycle : SimEvent { public int BuildingId; public int Hex; public Ware Ware; public int Amount = 1; }

    /// <summary>A recipe finished: Amount units of Output appeared in the building's store.</summary>
    public sealed class ProductionCycle : SimEvent { public int BuildingId; public Ware Output; public int Amount; }

    /// <summary>A recipe cycle began: the building used up one cycle of its inputs (see its BuildingDef.Inputs).</summary>
    public sealed class ProductionStarted : SimEvent { public int BuildingId; }

    /// <summary>A harvester with an upkeep (the Mine) used up Amount of Ware, paying for the next Cycles harvest cycles.</summary>
    public sealed class UpkeepPaid : SimEvent { public int BuildingId; public Ware Ware; public int Amount; public int Cycles; }

    /// <summary>A power plant burned fuel (it now supplies power for FuelTicks).</summary>
    public sealed class FuelBurned : SimEvent { public int BuildingId; public Ware Ware; public int Amount; }

    public sealed class RoundStarted : SimEvent { public int Round; public int[] Numbers; }

    public sealed class BatchAuthorised : SimEvent { public int Hex; public Ware Ware; public int Amount; }

    public sealed class CommissionCompleted : SimEvent { }
}
