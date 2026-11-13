namespace World.Sim.Strategy
{
    // Events for views. Plain public fields; Tick is stamped by EventBuffer.

    /// <summary>A new age began (draft follows in the same tick).</summary>
    public sealed class AgeStarted : SimEvent { public int Age; public int Round; }

    /// <summary>A round of an age began (after the economy's RoundStarted).</summary>
    public sealed class StrategyRoundStarted : SimEvent { public int Age; public int RoundInAge; public int HeroMoves; }

    public sealed class CityFounded : SimEvent { public int PlayerId; public int Vertex; public int CityId; }

    public sealed class Traded : SimEvent { public Ware Give; public int GiveCount; public Ware Get; public int GetCount; }

    public sealed class Sold : SimEvent { public Ware Ware; public int Count; public int Coin; }

    public sealed class Bought : SimEvent { public Ware Ware; public int Count; public int Coin; }

    public sealed class DemonSpawned : SimEvent { public int Vertex; public int Count; }

    /// <summary>A demon was drawn onto the hero's flag and cleared at once.</summary>
    public sealed class DemonRepelled : SimEvent { public int Vertex; }

    /// <summary>The breach track advanced (one step per outbreak). Vertex = the site that outbroke.</summary>
    public sealed class BreachAdvanced : SimEvent { public int Vertex; public int Breaches; public int Limit; }

    /// <summary>A site that would have gained one demon too many outbroke: each of its Neighbours gains one (chains possible).</summary>
    public sealed class OutbreakOccurred : SimEvent { public int Vertex; public int[] Neighbours; public int Breaches; public int Limit; public bool Chained; }

    /// <summary>Epidemic at an age change: the deck's bottom site was filled to Demons, then the discard went back on top.</summary>
    public sealed class EpidemicStruck : SimEvent { public int Vertex; public int Age; public int Demons; public bool Outbreak; }

    /// <summary>End-of-age military comparison (shields on both sides and the tokens awarded).</summary>
    public sealed class MilitaryCompared : SimEvent
    {
        public int Age;
        public int PlayerShields;
        public int RivalShields;
        public ComparisonResult Result;
        public int PlayerVp;
        public int RivalVp;
    }

    /// <summary>Demons on a flag were cleared. By: "hero" or "squad".</summary>
    public sealed class DemonsCleared : SimEvent { public int Vertex; public int Count; public string By; }

    public sealed class InfectionDrawn : SimEvent { public int[] Vertices; }

    public sealed class HeroMoved : SimEvent { public int From; public int To; public int MovesLeft; }

    /// <summary>Both sides were dealt cards for an age.</summary>
    public sealed class DraftDealt : SimEvent { public int Age; public int[] PlayerCards; }

    /// <summary>A side took a card in the draft (the rival's pick is public once made).</summary>
    public sealed class DraftPicked : SimEvent { public int PlayerId; public int CardId; public string DefId; public int Step; }

    /// <summary>The remaining draft hands were swapped.</summary>
    public sealed class DraftPassed : SimEvent { public int[] PlayerCards; }

    public sealed class DraftFinished : SimEvent { public int Age; public int[] DiscardedCards; }

    public sealed class CardBuilt : SimEvent { public int PlayerId; public int CardId; public string DefId; public int CityVertex; }

    public sealed class CardDiscarded : SimEvent { public int PlayerId; public int CardId; public string DefId; public int Coin; }

    public sealed class WonderStageOrdered : SimEvent { public int Stage; public int BuildingId; }

    public sealed class WonderStageCompleted : SimEvent { public int Stage; public int Vp; }

    /// <summary>The rival gained points from its plan.</summary>
    public sealed class RivalScored : SimEvent { public int Vp; public int Total; }

    public sealed class GameEnded : SimEvent
    {
        public GameOutcome Outcome;
        public string Reason;
        public int PlayerVp;
        public int RivalVp;
    }
}
