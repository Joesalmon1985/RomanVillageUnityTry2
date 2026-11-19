namespace World.Sim.War
{
    public sealed class HutBuilt : SimEvent { public int Corner; public int Good; }
    /// <summary>A hut made one processed good.</summary>
    public sealed class GoodProcessed : SimEvent { public int Corner; public int Good; }
    /// <summary>A processed good reached the lodge queue (the carrying stand-in, PR-3).</summary>
    public sealed class GoodLodged : SimEvent { public int People; public int Corner; public int Good; }
    public sealed class TrainingStarted : SimEvent { public int People; }
    public sealed class SoldierTrained : SimEvent { public int Soldier; public int People; public int Corner; public SoldierLook Look; }
    public sealed class SoldierStepped : SimEvent { public int Soldier; public int From; public int To; public int Ticks; }
    public sealed class DuelStarted : SimEvent { public int Duel; public int A; public int B; public int Node; public int Corner; }
    public sealed class DuelSwing : SimEvent { public int Duel; public int Attacker; public int Target; public bool Hit; public int Damage; public int TargetHp; public Wear TargetWear; }
    /// <summary>A duel ended without a death (peace came).</summary>
    public sealed class DuelEnded : SimEvent { public int Duel; public int A; public int B; public string Reason; }
    public sealed class SoldierDied : SimEvent { public int Soldier; public int People; public int Killer; public int KillerPeople; public int Node; public int Corner; }
    /// <summary>A soldier removed one demon (WA-10).</summary>
    public sealed class DemonPurged : SimEvent { public int Soldier; public int People; public int Node; }
    /// <summary>WA-14, WA-16. PointsLost is what the owner loses at once (1 settlement, 2 city).</summary>
    public sealed class SettlementBurned : SimEvent { public int Corner; public int People; public int AttackerPeople; public int Soldier; public bool WasCity; public int PointsLost; public int GoodsLost; }
    /// <summary>WA-15, WA-16.</summary>
    public sealed class CampLooted : SimEvent { public int Corner; public int People; public int AttackerPeople; public int Soldier; public int GoodsLost; }
    public sealed class RuinCleared : SimEvent { public int Corner; }
    /// <summary>Pairs lists the peoples at war, flat: a0, b0, a1, b1 with a lower than b. Leader is -1 without a unique leader.</summary>
    public sealed class WarStateChanged : SimEvent { public int[] Pairs; public int Leader; }
}
