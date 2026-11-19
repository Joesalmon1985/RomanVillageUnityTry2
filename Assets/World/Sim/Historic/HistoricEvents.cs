namespace World.Sim.Historic
{
    /// <summary>HI-1: the age has turned; the god board is closed for good and the era is Historic. Winner = the people that ended it.</summary>
    public sealed class HistoricBegan : SimEvent { public int Winner; }

    /// <summary>HI-2: every temple falls (views animate dust, collapse and cleared ground). Corners = each people's temple corner, index = people.</summary>
    public sealed class TemplesTornDown : SimEvent { public int[] Corners; }

    /// <summary>HI-3: a castle stage became a construction site (Stage 0 wood, 1 brick, 2 stone).</summary>
    public sealed class CastleStageStarted : SimEvent { public int People; public int Corner; public int Stage; }

    /// <summary>HI-3: a castle stage is raised.</summary>
    public sealed class CastleStageRaised : SimEvent { public int People; public int Corner; public int Stage; }

    public sealed class HistoricSitePlaced : SimEvent { public int People; public int BuildingId; public HistoricKind Kind; public int Hex; }

    public sealed class HistoricBuildingFinished : SimEvent { public int People; public int BuildingId; public HistoricKind Kind; public int Hex; }

    /// <summary>HI-5: a woodcutter felled a tree; a stump is left in the slot.</summary>
    public sealed class TreeFelled : SimEvent { public int People; public int BuildingId; public int Hex; public int Slot; }

    /// <summary>HI-5: a forester planted a sapling.</summary>
    public sealed class TreePlanted : SimEvent { public int People; public int BuildingId; public int Hex; public int Slot; }

    /// <summary>HI-5: a quarry's rock is used up; the quarry stops for good.</summary>
    public sealed class QuarryExhausted : SimEvent { public int People; public int BuildingId; public int Hex; }
}
