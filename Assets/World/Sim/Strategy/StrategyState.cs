using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>Strategy layer: ages, cities, market, demons, hero, draft and cards, wonder, score and ending.</summary>
        public Strategy.StrategyState Strategy;
    }
}

namespace World.Sim.Strategy
{
    public enum GameOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
        Draw = 3,
    }

    /// <summary>Why the game ended (<see cref="StrategyState.OutcomeReason"/>).</summary>
    public static class OutcomeReasons
    {
        /// <summary>A side reached the target victory points.</summary>
        public const string Points = "points";
        /// <summary>The breach counter reached its limit.</summary>
        public const string Breach = "breach";
        /// <summary>The last round of the last age ended; points were compared.</summary>
        public const string AgeEnd = "age_end";
        /// <summary>The player crossed the target on the tick the rival outpost fell.</summary>
        public const string Outpost = "outpost";
    }

    /// <summary>All strategy state (in <c>WorldState.Strategy</c>). Plain data; lists indexed by id.</summary>
    public sealed class StrategyState
    {
        public StrategyTuning Tuning = new StrategyTuning();

        /// <summary>False during the prologue (before the commission); ages, draft, infection and the rival start with Age I.</summary>
        public bool Started;
        /// <summary>Economy round in which Age I began (0 = not started).</summary>
        public int FirstRound;
        /// <summary>Current age 1..Ages (0 in the prologue).</summary>
        public int Age;
        /// <summary>Round inside the current age, 1..RoundsPerAge (0 in the prologue).</summary>
        public int RoundInAge;

        public bool GameOver;
        public GameOutcome Outcome;
        /// <summary>One of <see cref="OutcomeReasons"/>, or null while playing.</summary>
        public string OutcomeReason;
        public long EndedTick = -1;

        // ---- Demons and hero ----
        /// <summary>Demons per vertex id.</summary>
        public int[] Demons = new int[0];
        /// <summary>The breach track: one step per outbreak; the game is lost at Tuning.BreachLimit.</summary>
        public int Breaches;
        /// <summary>Epidemics so far (one at each age change).</summary>
        public int Epidemics;
        /// <summary>Sites that outbroke at the last infection or epidemic (for views), in order.</summary>
        public List<int> LastOutbreaks = new List<int>();
        /// <summary>Draw pile of vertex ids (top = last element).</summary>
        public List<int> InfectionDeck = new List<int>();
        public List<int> InfectionDiscard = new List<int>();
        public Rng InfectionRng = new Rng();
        /// <summary>Vertices drawn at the last infection (for views).</summary>
        public List<int> LastInfection = new List<int>();
        public int HeroVertex = -1;
        public int HeroMovesLeft;

        // ---- Draft and cards ----
        /// <summary>Every card instance, index = card id.</summary>
        public List<CardInstance> Cards = new List<CardInstance>();
        /// <summary>Per age (index age-1): card ids of that age's deck, in deal order.</summary>
        public List<AgeDeck> Decks = new List<AgeDeck>();
        /// <summary>0 = no draft; 1 = first pick pending; 2 = second pick pending.</summary>
        public int DraftStep;
        public int DraftAge;
        /// <summary>Per player (index = player id).</summary>
        public List<SideState> Sides = new List<SideState>();

        // ---- Wonder ----
        public WonderState Wonder = new WonderState();

        // ---- Military comparison ----
        /// <summary>One entry per finished age: shields on both sides and the tokens awarded.</summary>
        public List<MilitaryComparison> Comparisons = new List<MilitaryComparison>();

        public StrategyLedger Ledger = new StrategyLedger();
    }

    /// <summary>Result of an age-end military comparison, from the player's side.</summary>
    public enum ComparisonResult
    {
        Tie = 0,
        Win = 1,
        Loss = 2,
    }

    /// <summary>A military comparison at the end of an age (7 Wonders' conflict tokens).</summary>
    public sealed class MilitaryComparison
    {
        public int Age;
        public long Tick;
        public int PlayerShields;
        public int RivalShields;
        public ComparisonResult Result;
        /// <summary>Token points for the player (+1/+3/+5 on a win, -1 on a loss, 0 on a tie).</summary>
        public int PlayerVp;
        /// <summary>Token points for the rival (mirror of the player's result).</summary>
        public int RivalVp;
    }

    public sealed class AgeDeck
    {
        public int Age;
        public List<int> CardIds = new List<int>();
    }

    /// <summary>One physical card. Where it is follows from the lists that hold its id.</summary>
    public sealed class CardInstance
    {
        public int Id;
        /// <summary>Key of the card's row in <see cref="CardTable"/>.</summary>
        public string DefId;
        public int Age;
    }

    public sealed class BuiltCard
    {
        public int CardId;
        public int CityVertex;
        public long Tick;
    }

    /// <summary>Strategy data of one side (player or rival).</summary>
    public sealed class SideState
    {
        public int PlayerId;
        /// <summary>Drafted cards not yet built or discarded.</summary>
        public List<int> Hand = new List<int>();
        /// <summary>Cards on offer in the running draft.</summary>
        public List<int> DraftHand = new List<int>();
        public List<BuiltCard> Built = new List<BuiltCard>();
        public List<int> Discarded = new List<int>();
        /// <summary>Rival: points from its seeded plan.</summary>
        public int PlanVp;
    }

    public sealed class WonderState
    {
        /// <summary>Wonder site building used for stages (-1 until the first stage is ordered).</summary>
        public int BuildingId = -1;
        /// <summary>Stages finished (0..Tuning.WonderStages.Length).</summary>
        public int StagesDone;
        /// <summary>Stage being supplied (1-based) or 0.</summary>
        public int InProgressStage;
        public long StageOrderedTick = -1;
    }

    /// <summary>Wares that left the economy through strategy actions, per ware (index = (int)Ware).</summary>
    public sealed class StrategyLedger
    {
        public int[] Cities = new int[Wares.Count];
        public int[] Cards = new int[Wares.Count];
        public int[] Wonder = new int[Wares.Count];
        public int[] TradeGiven = new int[Wares.Count];
        public int[] TradeReceived = new int[Wares.Count];
        public int[] Sold = new int[Wares.Count];
        public int CoinFromSales;
        public int CoinFromDiscards;
        public int CoinSpentOnCards;
        public int CoinSpentBuying;
        public int CoinFromCards;
    }
}
