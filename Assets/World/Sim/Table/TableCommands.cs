namespace World.Sim.Table
{
    // ---- Commands (PRD_GODBOARD Appendix A; PlayerId = the acting people) ----
    // Building commands carry a "Table" prefix because the Economy module already has PlaceRoad and UpgradeCity.

    /// <summary>The player sacrifices at their own altar: opens the board the first time, resumes it afterwards (GB-1, GB-5).</summary>
    public sealed class Sacrifice : Command { }

    /// <summary>The player returns to the world: the table pauses with its turn state frozen (GB-4, HG-6).</summary>
    public sealed class LeaveTable : Command { }

    public sealed class PlaceTableSettlement : Command { public int Corner; }

    public sealed class PlaceTableRoad : Command { public int HexEdge; }

    public sealed class UpgradeTableCity : Command { public int Corner; }

    /// <summary>Gives the bank or port rate of Give for one Get (TR-1).</summary>
    public sealed class BankTrade : Command { public Ware Give; public Ware Get; }

    /// <summary>Offers a trade to one people (To) or everyone (To = -1). Give/Get are counts per raw good (TR-2).</summary>
    public sealed class OfferTrade : Command { public int To = -1; public int[] Give; public int[] Get; }

    /// <summary>Accepts or declines an open offer (TR-3). Counters are a later step.</summary>
    public sealed class AnswerOffer : Command { public int OfferId; public bool Accept; }

    /// <summary>Ends the turn (tapping the hourglass, HG-4).</summary>
    public sealed class EndTurn : Command { }

    /// <summary>The cards a people gives up after a 7 (TU-10). Counts per raw good; must sum to what is owed.</summary>
    public sealed class DiscardCards : Command { public int[] Cards; }

    // ---- Events (Appendix A). Never carry the roll value (AB-1). ----

    public sealed class TableOpened : SimEvent { }

    public sealed class TablePaused : SimEvent { }

    public sealed class TableResumed : SimEvent { }

    /// <summary>A people's setup placement or turn began. Round is 0 during setup.</summary>
    public sealed class TurnStarted : SimEvent { public int People; public int Round; public bool Setup; }

    /// <summary>Reason: "ended" (EndTurn), "sand" (hourglass ran out), "ai" (an AI seat finished).</summary>
    public sealed class TurnEnded : SimEvent { public int People; public string Reason; }

    /// <summary>
    /// Hexes that produced on this roll (ascending hex ids) and the cards each people got: Gains[people * 5 + good].
    /// Blocked hexes that matched the roll are listed in Blocked. No roll value.
    /// </summary>
    public sealed class Produced : SimEvent { public int[] Hexes; public int[] Blocked; public int[] Gains; }

    /// <summary>A roll of 7 (after its discards): the plague consumes this (PL-6). No production on a 7.</summary>
    public sealed class Epidemic : SimEvent { public int People; public int Round; }

    /// <summary>Peoples above the limit must discard: Owed[people] cards (TU-10).</summary>
    public sealed class DiscardRequired : SimEvent { public int[] Owed; }

    /// <summary>Auto is true when the default chooser picked (AI seat, or the player's time ran out).</summary>
    public sealed class Discarded : SimEvent { public int People; public int[] Cards; public bool Auto; }

    /// <summary>Kind: "settlement", "road", "city". Spot = corner or hex edge. Setup is true for setup-round pieces.</summary>
    public sealed class Built : SimEvent { public int People; public string Kind; public int Spot; public bool Setup; }

    /// <summary>Cards a people got at the end of its setup placement (one per resource hex beside its second settlement).</summary>
    public sealed class StartingCards : SimEvent { public int People; public int[] Cards; }

    /// <summary>A completed trade. With = -1 for the bank or a port (Rate is then the rate used), else the partner people.</summary>
    public sealed class Traded : SimEvent { public int People; public int With; public int[] Give; public int[] Get; public int Rate; public int OfferId; }

    public sealed class OfferMade : SimEvent { public int OfferId; public int From; public int To; public int[] Give; public int[] Get; }

    /// <summary>Status: Accepted, Declined (by To, or by everyone for an open offer) or Expired.</summary>
    public sealed class OfferAnswered : SimEvent { public int OfferId; public int By; public OfferStatus Status; }

    public sealed class PointsChanged : SimEvent { public int People; public int Points; public int Delta; }

    public sealed class LongestRoadChanged : SimEvent { public int Holder; public int Previous; public int Length; }

    /// <summary>A people reached the winning points: the prehistoric age ends (TU-7, HI-1).</summary>
    public sealed class AgeEnded : SimEvent { public int Winner; public int Points; }

    /// <summary>Refusal codes of the table (Appendix A) and their sentences.</summary>
    public static class TableRefusals
    {
        public const string NotYourTurn = "not_your_turn";
        public const string CannotAfford = "cannot_afford";
        public const string TooClose = "too_close";
        public const string NotConnected = "not_connected";
        public const string NoPieces = "no_pieces";
        public const string OfferClosed = "offer_closed";
        public const string LeaderRefuses = "leader_refuses";
        public const string TableClosed = "table_closed";
        /// <summary>Not a free corner or edge, or not this people's settlement (shares the core code).</summary>
        public const string InvalidSite = Reasons.InvalidSite;
        /// <summary>A trade that makes no sense (nothing given, same good both ways, wrong discard count).</summary>
        public const string BadTrade = "bad_trade";

        public static string Sentence(string code)
        {
            switch (code)
            {
                case NotYourTurn: return "It is not our turn.";
                case CannotAfford: return "We do not have the cards for that.";
                case TooClose: return "That is too close to another settlement.";
                case NotConnected: return "Our roads do not reach there.";
                case NoPieces: return "We have no pieces of that kind left.";
                case OfferClosed: return "That offer is no longer open.";
                case LeaderRefuses: return "They will not trade with the leader.";
                case TableClosed: return "The god board is closed.";
                case InvalidSite: return "We cannot build there.";
                case BadTrade: return "That trade does not add up.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code) => CommandResult.Reject(code, Sentence(code));
    }
}
