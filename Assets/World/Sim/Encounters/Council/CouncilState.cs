using System.Collections.Generic;
using World.Sim.Eras;

namespace World.Sim.Encounters.Council
{
    public enum CouncilStatus { None = 0, Offered = 1, InProgress = 2, Decided = 3 }

    /// <summary>A candidate bound to a real resident (names and occupations are snapshotted when the election is offered).</summary>
    public sealed class CandidateInfo
    {
        public int Seat;
        public string Name;
        public string Occupation;
        public string OccupationId;
        public string ResidentKey;
        public string Emblem;
    }

    /// <summary>The lead NPC of one dispute track: a real resident when an occupation matches, otherwise a local delegate.</summary>
    public sealed class DisputeBinding
    {
        public int Track;
        public string DisputeId;
        public string LeadName;
        /// <summary>Null for a local delegate.</summary>
        public string LeadResidentKey;
        public bool IsDelegate;
        public string LeadTitle;
        public string Prop;
    }

    /// <summary>The office holder of a settlement. Separate from victory points.</summary>
    public sealed class OfficeHolder
    {
        public int Settlement;
        public string Name;
        public string ResidentKey;
        public string Occupation;
        public string Office;
        public int ElectionNo;
        public long Tick;
        public int Seat;
    }

    /// <summary>One settlement's council record.</summary>
    public sealed class SettlementCouncil
    {
        public int SettlementId;
        public CouncilStatus Status;
        public int ElectionNo;
        public ulong Seed;
        /// <summary>The candidate who approaches the player and asks for help.</summary>
        public int AskerSeat;
        public Era Era;
        public string Office;
        /// <summary>False after the player leaves, until resumed.</summary>
        public bool Present = true;
        public bool PlayerHelps;
        public List<CandidateInfo> Candidates = new List<CandidateInfo>();
        public List<DisputeBinding> Disputes = new List<DisputeBinding>();
        public ElectionState Election;
        public int WinnerSeat = -1;
        /// <summary>True once the winner has been recorded as office holder (once).</summary>
        public bool Applied;
        public long DecidedTick;
    }

    /// <summary>Saved with the world (WorldState.Council). Older saves lack it and keep this empty initialiser.</summary>
    public sealed class CouncilState
    {
        /// <summary>Sorted by settlement id.</summary>
        public List<SettlementCouncil> Settlements = new List<SettlementCouncil>();
        public List<OfficeHolder> Offices = new List<OfficeHolder>();
        public int ElectionsOffered;
    }
}
