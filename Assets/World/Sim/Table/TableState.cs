using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The god board's turn engine (PRD_GODBOARD 4.2-4.6, Appendix A). Null when the module is not in the game.</summary>
        public Table.TableState Table;
    }
}

namespace World.Sim.Table
{
    /// <summary>Where the table is in its life.</summary>
    public enum TablePhase
    {
        /// <summary>Before the first Sacrifice (GB-6): no rolls, no turns.</summary>
        NotOpened = 0,
        /// <summary>The setup round (GB-5): each people places a second settlement and a road, in seat order.</summary>
        Setup = 1,
        /// <summary>A roll of 7 was made and someone still owes cards (TU-10). The turn waits.</summary>
        Discard = 2,
        /// <summary>The current people trades and builds, then ends the turn.</summary>
        Main = 3,
        /// <summary>A people reached the winning points (TU-7). The table is closed for good.</summary>
        Ended = 4,
    }

    public enum OfferStatus
    {
        Open = 0,
        Accepted = 1,
        Declined = 2,
        Expired = 3,
    }

    /// <summary>
    /// WorldState.Table. Plain serialisable data: arrays and lists of plain classes, no dictionaries, no references to
    /// systems. Arrays indexed by corner or hex edge use the ids of <see cref="BoardTopology"/>.
    /// </summary>
    public sealed class TableState
    {
        public TableTuning Tuning = new TableTuning();

        /// <summary>Number of peoples at the table (N). People ids are 0..N-1; seat order is id order.</summary>
        public int PeopleCount;

        /// <summary>The seat the player controls, or -1 for an AI-only table (tests, proofs). Only this seat has the hourglass.</summary>
        public int HumanSeat;

        public TablePhase Phase;

        /// <summary>True while the player has left the board (GB-4, HG-6): nothing on the table moves.</summary>
        public bool Paused;

        /// <summary>Ticks the table has run unpaused since it opened (dev and proofs).</summary>
        public long ActiveTicks;

        public List<TablePeople> Peoples = new List<TablePeople>();

        /// <summary>Owner people per corner, -1 when empty.</summary>
        public int[] CornerOwner;

        /// <summary>0 empty, 1 settlement (the first camp counts as one), 2 city, per corner.</summary>
        public int[] CornerLevel;

        /// <summary>Owner people per hex edge, -1 when no road.</summary>
        public int[] RoadOwner;

        /// <summary>Ports (BD-5). Copied at InitState; empty when the board has none yet.</summary>
        public List<TablePort> Ports = new List<TablePort>();

        // ---- Setup round ----

        /// <summary>The people placing in the setup round (seat order).</summary>
        public int SetupSeat;

        /// <summary>The settlement just placed in setup (-1 while the seat still has to place it; then its road follows).</summary>
        public int SetupCorner = -1;

        // ---- Turns ----

        /// <summary>Fixed turn order after setup, shuffled by seed (TU-1).</summary>
        public int[] TurnOrder = new int[0];

        /// <summary>Index into <see cref="TurnOrder"/> of the current turn.</summary>
        public int TurnIndex;

        /// <summary>1-based round number after setup; 0 during setup.</summary>
        public int Round;

        /// <summary>The people whose turn (or setup placement) it is.</summary>
        public int Current = -1;

        /// <summary>Ticks spent in the current turn or setup placement (unpaused only).</summary>
        public int TurnTicks;

        /// <summary>The last hidden roll (2-12). Never in a player-facing event; F1 and dev tools only (AB-1).</summary>
        public int LastRoll;

        /// <summary>The table's own dice stream (TU-1).</summary>
        public Rng RollRng = new Rng();

        // ---- Hourglass (human seat only, HG-1..HG-6) ----

        public int Sand;

        /// <summary>Ticks since the player's last accepted action in this turn (HG-2).</summary>
        public int IdleTicks;

        // ---- AI turn pacing (HG-5) ----

        /// <summary>Actions the current AI took this turn; its turn lasts AiTurnTicks + AiActionTicks per action.</summary>
        public int AiActions;

        /// <summary>True once the current AI seat has been asked to play its turn.</summary>
        public bool AiPlayed;

        // ---- Discards on a 7 (TU-10) ----

        /// <summary>Cards each people still owes after a 7, index = people.</summary>
        public int[] DiscardOwed = new int[0];

        /// <summary>Ticks left for the player to choose their discard.</summary>
        public int DiscardTicksLeft;

        // ---- Trade offers (TR-2, TR-3) ----

        public List<TradeOffer> Offers = new List<TradeOffer>();
        public int NextOfferId = 1;

        // ---- Points (TU-6, TU-7) ----

        /// <summary>People holding the longest road, or -1.</summary>
        public int LongestRoadHolder = -1;

        /// <summary>The winner once Phase is Ended, else -1.</summary>
        public int Winner = -1;
    }

    /// <summary>One people's seat at the table.</summary>
    public sealed class TablePeople
    {
        public int Id;

        /// <summary>The first camp's corner (PE-2).</summary>
        public int StartCorner;

        /// <summary>Cards per raw good, index = (int)Ware for Wood..Stone (TU-9). Separate from the physical goods.</summary>
        public int[] Hand = new int[Wares.RawCount];

        /// <summary>Points as last computed from the board (cache for PointsChanged; the truth is TableRules.Points).</summary>
        public int Points;

        /// <summary>Longest joined road as last computed.</summary>
        public int RoadLength;
    }

    /// <summary>A port on two corners of the rim (BD-5). Good = -1 for a 3:1 port, else the raw good of a 2:1 port.</summary>
    public sealed class TablePort
    {
        public int CornerA;
        public int CornerB;
        public int Good = -1;
        /// <summary>Every corner that may use the port (G0-2: the port hex's settlement corners); null = only A and B.</summary>
        public int[] Corners;
        /// <summary>The board hex the port sits on, or -1.</summary>
        public int Hex = -1;
    }

    /// <summary>A trade offered by the current people (TR-2). Give = what From gives, Get = what From receives.</summary>
    public sealed class TradeOffer
    {
        public int Id;
        public int From;
        /// <summary>The people it is addressed to, or -1 for everyone.</summary>
        public int To = -1;
        public int[] Give = new int[Wares.RawCount];
        public int[] Get = new int[Wares.RawCount];
        public int TicksLeft;
        public int Age;
        public OfferStatus Status;
        /// <summary>Who answered (accepted or declined last), -1 if nobody.</summary>
        public int AnsweredBy = -1;
        /// <summary>Peoples that declined an offer to everyone.</summary>
        public List<int> DeclinedBy = new List<int>();
    }

    /// <summary>Every number of the table (PRD 4.4-4.6). Starting values; tuned from play (HG-0). Saved with the state.</summary>
    public sealed class TableTuning
    {
        public int WinPoints = 10;
        public int MaxRoads = 15, MaxSettlements = 5, MaxCities = 4;
        public int SettlementPoints = 1, CityPoints = 2, LongestRoadPoints = 2, LongestRoadMin = 5;
        /// <summary>A people holding more than this many cards discards half (rounded down) on a 7.</summary>
        public int DiscardOver = 7;
        public int DiscardTicks = 150;
        public int BankRate = 4, GenericPortRate = 3, GoodPortRate = 2;
        public int OfferTicks = 150;
        public int AiAnswerTicks = 20;
        // Hourglass.
        public int TurnBaseTicks = 450, TurnCapTicks = 900, IdleAfterTicks = 40, IdleRate = 3, ActionRefillTicks = 50;
        // AI pacing.
        public int AiTurnTicks = 30, AiActionTicks = 10;
        /// <summary>Ticks an AI seat waits before its setup placement (so the player can follow it).</summary>
        public int AiSetupTicks = 30;
    }
}
