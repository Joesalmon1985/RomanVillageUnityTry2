using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>Strategy tuning with defaults in code (serialised in <c>StrategyState.Tuning</c>).</summary>
    public sealed class StrategyTuning
    {
        // ---- Rounds and ages ----
        public int Ages = 3;
        public int RoundsPerAge = 2;
        /// <summary>
        /// When true the ages begin at the first round start after the quartermaster commission is complete
        /// (the rounds before are the prologue: no draft, no demons, no rival). False: Age I is round 1.
        /// </summary>
        public bool StartAfterCommission = true;
        /// <summary>Age I begins at the latest at the start of round MaxPrologueRounds + 1.</summary>
        public int MaxPrologueRounds = 8;

        /// <summary>Round-draw batches at your cities grow by this much per age (Age I x1, II x2, III x3): the land yields more as the ages turn.</summary>
        public int AgeBatchBonus = 2;

        // ---- Score and ending ----
        public int VictoryPoints = 8;
        public int VpPerCity = 2;
        /// <summary>VP for each wonder stage (index = stage - 1).</summary>
        public int[] WonderStageVp = { 1, 2 };
        public int OutpostVp = 2;
        public int BreachLimit = 4;

        // ---- Cities ----
        public int[] CityCost = EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 3, Ware.Grain, 1);
        public int MaxCardsPerCity = 3;
        /// <summary>Carriers that join the village (at the storehouse) when you found a city.</summary>
        public int CarriersPerCity = 2;

        // ---- Market ----
        /// <summary>Wares given for one ware received.</summary>
        public int TradeRate = 4;
        /// <summary>Best possible rate after the Market building and cards.</summary>
        public int MinTradeRate = 2;
        /// <summary>Wares sold for one coin.</summary>
        public int SellWaresPerCoin = 2;
        /// <summary>Coin to buy one ware.</summary>
        public int BuyPrice = 3;

        // ---- Demons and hero ----
        /// <summary>Infection draws per round end in Age I; +InfectionPerAge each later age.</summary>
        public int InfectionBase = 2;
        public int InfectionPerAge = 1;
        /// <summary>
        /// The infection deck holds the flags within this many road segments of the home city (4 = the home territory).
        /// A drawn corner always takes a demon; a drawn intermediate flag only while it holds a building, road end or city.
        /// </summary>
        public int InfectionRadius = 4;
        /// <summary>A drawn flag already holding this many demons adds a breach instead.</summary>
        public int BreachThreshold = 3;
        /// <summary>Hero moves per round; one move = one hex edge (corner to neighbouring corner), clearing the flags it passes and arrives at.</summary>
        public int HeroMovesPerRound = 4;
        /// <summary>
        /// Intensify: from Age II, before each infection draw the discard pile is shuffled back on top of the deck,
        /// so flags that already hold demons are drawn again (an ignored flag breaches).
        /// </summary>
        public bool Intensify = true;

        // ---- Draft ----
        public int DraftHandSize = 3;
        public int DraftPicks = 2;
        public int DiscardCoin = 3;

        // ---- Wonder ----
        /// <summary>Wares per stage (index = stage - 1), requested to and delivered at the wonder site.</summary>
        public int[][] WonderStages =
        {
            EconomyTuning.W(Ware.Planks, 4, Ware.Stone, 6),
            EconomyTuning.W(Ware.Rations, 2, Ware.Ingots, 2),
        };

        // ---- Rival ----
        /// <summary>Rounds of an age-run (1-based, counted from Age I) at whose end the rival gains 1 VP.</summary>
        public int[] RivalPlanRounds = { 2, 4, 6 };
        /// <summary>The rival builds at most this many of its drafted cards.</summary>
        public int RivalMaxBuilt = 3;
    }
}
