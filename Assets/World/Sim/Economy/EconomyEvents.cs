namespace World.Sim.Economy
{
    // Economy events (REFACTOR_SPEC 5.3). Plain public fields; Tick is stamped by EventBuffer.

    public sealed class DiceRolled : SimEvent { public int D1; public int D2; public int Sum; public int RollIndex; }

    public sealed class StockAdded : SimEvent { public int Hex; public Ware Ware; public int Count; public long MatureTick; }

    /// <summary>Units of a batch that did not fit under the hex cap.</summary>
    public sealed class StockLost : SimEvent { public int Hex; public Ware Ware; public int Count; }

    public sealed class StockMatured : SimEvent { public int Hex; public Ware Ware; public int Count; }

    public sealed class CollectStarted : SimEvent { public int BuildingId; public int Hex; }

    public sealed class Collected : SimEvent { public int BuildingId; public int Hex; public Ware Ware; public int Amount; }

    public sealed class ProductionStarted : SimEvent { public int BuildingId; }

    public sealed class ProductionCycle : SimEvent { public int BuildingId; public Ware Ware; public int Amount; }

    public sealed class SitePlaced : SimEvent { public SiteKind Kind; public int Id; public int Faction; }

    public sealed class SiteCompleted : SimEvent { public SiteKind Kind; public int Id; public int Faction; }

    public sealed class SiteCancelled : SimEvent { public SiteKind Kind; public int Id; public int Faction; }

    public sealed class BuildingStateChanged : SimEvent
    {
        public int BuildingId;
        public BuildingState State;
        /// <summary>Stall reason code or null.</summary>
        public string Reason;
        public int ReasonWare = -1;
    }

    public sealed class BuildingRetired : SimEvent { public int BuildingId; }

    public sealed class SettlementFounded : SimEvent { public int SettlementId; public int Corner; public int Faction; }

    public sealed class CityUpgraded : SimEvent { public int SettlementId; public int Corner; public int Faction; }

    public sealed class RoadBuilt : SimEvent { public int RoadId; public int HexEdge; public int Faction; }

    public sealed class JobCreated : SimEvent
    {
        public int JobId;
        public Ware Ware;
        public int Count;
        public PlaceKind FromKind;
        public int FromId;
        public PlaceKind ToKind;
        public int ToId;
        public bool IsShipment;
    }

    public sealed class CarrierLegStarted : SimEvent
    {
        public int CarrierId;
        public PlaceKind FromKind;
        public int FromId;
        public PlaceKind ToKind;
        public int ToId;
        public int LegTicks;
    }

    public sealed class WarePickedUp : SimEvent { public int JobId; public int CarrierId; public Ware Ware; public int Count; }

    public sealed class WareDelivered : SimEvent
    {
        public int JobId;
        public int CarrierId;
        public Ware Ware;
        public int Count;
        public PlaceKind ToKind;
        public int ToId;
    }

    public sealed class JobStalled : SimEvent { public int JobId; public string Reason; }

    public sealed class WarriorTrained : SimEvent { public int WarriorId; public int Lodge; public int Faction; }

    public sealed class WarriorFed : SimEvent { public int WarriorId; public Ware Ware; }

    public sealed class WarriorHungry : SimEvent { public int WarriorId; }
}
