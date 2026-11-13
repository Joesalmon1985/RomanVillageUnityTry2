using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>
    /// Strategy tuning with defaults in code (serialised in <c>StrategyState.Tuning</c>). Numbers marked "tuned"
    /// were set by the headless balance runs; see Docs/ECONOMY_DESIGN.md (tuned numbers) and Docs/ECONOMY_PROOF.md.
    /// </summary>
    public sealed class StrategyTuning
    {
        // ---- Rounds and ages ----
        public int Ages = 3;
        /// <summary>Rounds in each age (tuned). The age ends with the military comparison at the end of its last round.</summary>
        public int RoundsPerAge = 3;
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
        /// <summary>First side to this many points wins (tuned from the design's starting value 12).</summary>
        public int VictoryPoints = 12;
        public int VpPerCity = 2;
        /// <summary>VP for each wonder stage (index = stage - 1).</summary>
        public int[] WonderStageVp = { 2, 3 };
        public int OutpostVp = 2;
        /// <summary>The breach track: each outbreak advances it by one; reaching this loses the game (tuned from 6).</summary>
        public int BreachLimit = 5;

        // ---- Military comparison (end of each age) ----
        /// <summary>Points for winning the comparison, by age (index = age - 1).</summary>
        public int[] MilitaryWinVp = { 1, 3, 5 };
        /// <summary>Points for losing a comparison (negative).</summary>
        public int MilitaryLossVp = -1;

        // ---- Cities ----
        /// <summary>A new city: 3 Planks + 3 Bricks + 2 Rations (design section 4).</summary>
        public int[] CityCost = EconomyTuning.W(Ware.Planks, 3, Ware.Bricks, 3, Ware.Rations, 2);
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
        /// <summary>Infection draws per round end in Age I; +InfectionPerAge each later age (2, 3, 4).</summary>
        public int InfectionBase = 2;
        public int InfectionPerAge = 1;
        /// <summary>
        /// The infection deck holds the flags within this many road segments of the home city (tuned to 2: the village
        /// core around the storehouse, 40 m); the home corner joins in Age II and every founded city adds its corner.
        /// A drawn corner always takes a demon; a drawn intermediate flag only while it holds a building, a road end, a
        /// city or demons (otherwise the card is discarded and the next one drawn).
        /// </summary>
        public int InfectionRadius = 2;
        /// <summary>A site holds at most this many demons; one more makes it outbreak (Ward Stones: +1).</summary>
        public int DemonCap = 3;
        /// <summary>Epidemic: the bottom card's site is filled to this many demons (an outbreak if it already held some).</summary>
        public int EpidemicFill = 3;
        /// <summary>Epidemic at each age change (Age II and Age III start).</summary>
        public bool Epidemics = true;
        /// <summary>Hero moves per round; one move = one hex edge (corner to neighbouring corner), clearing the flags it passes and arrives at.</summary>
        public int HeroMovesPerRound = 4;

        // ---- Draft ----
        public int DraftHandSize = 3;
        public int DraftPicks = 2;
        public int DiscardCoin = 3;

        // ---- Wonder ----
        /// <summary>Wares per stage (index = stage - 1), requested to and delivered at the wonder site. Stage 1 Planks + Bricks, stage 2 Iron + Rations.</summary>
        public int[][] WonderStages =
        {
            EconomyTuning.W(Ware.Planks, 4, Ware.Bricks, 4),
            EconomyTuning.W(Ware.Iron, 2, Ware.Rations, 2),
        };

        // ---- Rival ----
        /// <summary>Rounds of an age-run (1-based, counted from Age I) at whose end the rival's plan gains RivalPlanVp.</summary>
        public int[] RivalPlanRounds = { 3, 6, 9 };
        public int RivalPlanVp = 2;
        /// <summary>
        /// The rival builds at most this many of its drafted military cards (one per age). Tuned to 0: the rival's
        /// shields are its garrison and raiders only; its drafted cards only deny them to you.
        /// </summary>
        public int RivalMaxBuilt = 0;
    }
}
