using System;
using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>Victory points, credits and the roster unlock, one ledger per faction (PRD_AGES 6, A.3).</summary>
        public Ledger.LedgerState Ledger;
    }
}

namespace World.Sim.Ledger
{
    /// <summary>WorldState.Ledger. Index = faction (<see cref="PlayerIds"/>).</summary>
    public sealed class LedgerState
    {
        public List<FactionLedger> Factions = new List<FactionLedger>();
    }

    /// <summary>One faction's points and credits (AG-20). Points never go down; the unlock never goes back.</summary>
    public sealed class FactionLedger
    {
        public int Faction;
        public int Points;
        /// <summary>Milestone keys already paid (6.1), sorted ordinal. Written once: saves, loads and repeats never pay twice.</summary>
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Reuse)] // keeps the ordinal comparer on load
        public SortedSet<string> Paid = new SortedSet<string>(StringComparer.Ordinal);
        public long Credits;
        public long ReservedCredits;
        /// <summary>Income stops at this (6.2: 2000 + 1000 per Refinery and Depot; M1 keeps it up to date).</summary>
        public long CreditCap = LedgerApi.BaseCreditCap;
        public bool RosterUnlocked;
        public long RosterUnlockedTick = -1;
        /// <summary>For the credits balance invariant: earned = spent + held + reserved.</summary>
        public long CreditsEarnedTrade, CreditsEarnedRefinery, CreditsSpent;
    }

    /// <summary>Refusal codes of the ledger checks (6.3, 6.4) and their exact sentences.</summary>
    public static class LedgerRefusals
    {
        public const string RosterLocked = "roster_locked";
        public const string NoCredits = "no_credits";

        /// <summary>roster_locked: n = the faction's points; no_credits: n = credits still missing.</summary>
        public static string Sentence(string code, long n = 0)
        {
            switch (code)
            {
                case RosterLocked: return "Our people need " + LedgerApi.RosterThreshold + " victory points to build that (" + n + " of " + LedgerApi.RosterThreshold + ").";
                case NoCredits: return "We need " + n + " more credits.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code, long n = 0) => CommandResult.Reject(code, Sentence(code, n));
    }

    // Events (PRD_AGES A.3).

    public sealed class PointsAwarded : SimEvent { public int Faction; public string Key; public int Points; public int Total; }

    public sealed class RosterUnlocked : SimEvent { public int Faction; }

    /// <summary>Reason: "trade", "refinery", "spent", "reserved", "released", "refund" (M1 writes these).</summary>
    public sealed class CreditsChanged : SimEvent { public int Faction; public long Delta; public string Reason; }
}
