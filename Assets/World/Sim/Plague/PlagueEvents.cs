namespace World.Sim.Plague
{
    /// <summary>A demon was placed. Reason: "draw", "epidemic" or "spread".</summary>
    public sealed class Infected : SimEvent { public int Node; public int Kind; public int Count; public string Reason; }

    /// <summary>An epidemic (PL-6): the rate moved, the bottom card got 3 demons, the discards went on top.</summary>
    public sealed class Epidemic : SimEvent { public int Node; public int RateIndex; public int Rate; }

    /// <summary>A node overflowed. Chain = 0 for the first outbreak of a chain, 1.. for the ones it set off. Total = the counter now.</summary>
    public sealed class Outbreak : SimEvent { public int Node; public int Kind; public int Chain; public int Total; }

    /// <summary>The island falls (PL-10): shared doom.</summary>
    public sealed class IslandFell : SimEvent { public int Outbreaks; }

    public sealed class Banished : SimEvent { public int Node; public int Kind; public int Left; }

    public sealed class Cured : SimEvent { public int Element; }

    public sealed class Eradicated : SimEvent { public int Element; }

    /// <summary>A people's crystal strengthened a kind (CC-3): its draws place 2 demons.</summary>
    public sealed class Strengthened : SimEvent { public int Element; }

    /// <summary>The plague ended (HI-8). Cleared = demons removed.</summary>
    public sealed class PlagueEnded : SimEvent { public int Cleared; }
}
