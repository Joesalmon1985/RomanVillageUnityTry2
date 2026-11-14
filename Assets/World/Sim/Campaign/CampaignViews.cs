using System.Collections.Generic;

namespace World.Sim.Eras
{
    public sealed partial class HistoryState
    {
        /// <summary>
        /// The crystal campaign's record (PRD_CRYSTALS N-6, A.5), copied whole by the ages' transition code when
        /// CampaignEnded { Won } is raised. Null when no campaign was won.
        /// </summary>
        public global::World.Sim.Campaign.CampaignRecord CampaignRecord;
    }
}

namespace World.Sim.Campaign
{
    // Read-only snapshots returned by CampaignApi (A.5). Built fresh on each call; changing one changes nothing.

    public sealed class CrystalView
    {
        public Element Element;
        public string Id, Name;
        public Custody Custody;
        public MarkerRef At;
        public long SinceTick;
        /// <summary>Tower id.</summary>
        public string Tower;
    }

    public sealed class BeliefView
    {
        public Element Element;
        public Custody Custody;
        public MarkerRef At;
        public long AsOfTick;
        public SourceKind Source;
        /// <summary>Older than CampaignTuning.StaleTicks.</summary>
        public bool Stale;
        /// <summary>The "where to look" sentence for a stale belief ("" when not stale).</summary>
        public string WhereToLook;
    }

    public sealed class LeadView
    {
        /// <summary>Queue id (for AcceptLead / DeclineLead).</summary>
        public int Id;
        public string DefId, Title;
        /// <summary>The reason line with tokens filled.</summary>
        public string Reason;
        public int Score, Rank;
        public string Crystal;
        public MarkerRef Goal;
        public QuestAction Action;
    }

    public sealed class PartyView
    {
        public PartyMode Mode;
        public MarkerRef At;
        public bool PlayerPresent;
        /// <summary>-1 = none.</summary>
        public int LeadId;
        public string ChoiceId, EncounterId;
        public long ModeSinceTick;
        /// <summary>Leg destination when travelling (empty otherwise).</summary>
        public MarkerRef To;
    }

    public sealed class CompanionView
    {
        public string Id, Name, Role, Line, Service, ActorId;
        public int VolunteerOrder;
        public bool Alive;
        public string FellAt;
        public long FellTick;
    }

    public sealed class AttemptView
    {
        public int Id;
        public string EncounterId;
        /// <summary>"" = no companion left.</summary>
        public string Volunteer;
        public ulong Seed;
        public int Restarts;
        public long CommittedTick;
        public string Profile;
        public EncounterKind Kind;
    }

    /// <summary>What the party knows of the rival: reports only, never the truth (5.3).</summary>
    public sealed class RivalKnown
    {
        /// <summary>Reports that have reached the party, oldest first.</summary>
        public List<RivalReport> Reports = new List<RivalReport>();
        /// <summary>Tick of the newest known report, -1 = none.</summary>
        public long LastReportTick = -1;
    }

    /// <summary>One fallen companion in the CampaignRecord.</summary>
    public sealed class FallenRecord
    {
        public string Id, Name, EncounterId;
        /// <summary>Display name of the place where they fell.</summary>
        public string Place;
        public long Tick;
        public FallCause Cause;
    }

    /// <summary>What the campaign hands to later ages (N-6; PRD_AGES 4.4).</summary>
    public sealed class CampaignRecord
    {
        public string EndingId = "";
        public string DefinitionId = "";
        public ulong Seed;
        public List<Element> RestoredOrder = new List<Element>();
        /// <summary>Elements that were ever on an Ash altar.</summary>
        public List<Element> EverOnAltar = new List<Element>();
        /// <summary>Companion ids alive at the end, in volunteer order.</summary>
        public List<string> Survivors = new List<string>();
        /// <summary>In the order they fell.</summary>
        public List<FallenRecord> Fallen = new List<FallenRecord>();
    }

    /// <summary>One problem found by the validator (6.5). Any error blocks the start.</summary>
    public sealed class Problem
    {
        public string Code;
        public string Sentence;
        /// <summary>The id the problem is about ("" = the whole pack).</summary>
        public string Id;
        public bool IsError = true;

        public Problem() { }

        public Problem(string code, string sentence, string id = "", bool isError = true)
        {
            Code = code;
            Sentence = sentence;
            Id = id ?? "";
            IsError = isError;
        }

        public override string ToString() => Code + ": " + Sentence;
    }

    /// <summary>The validator's problem codes (6.5: one per broken fixture).</summary>
    public static class ProblemCodes
    {
        public const string DuplicateId = "duplicate_id";
        public const string DanglingReference = "dangling_reference";
        public const string BadVersion = "bad_version";
        public const string CompanionCount = "companion_count";
        public const string VolunteerOrder = "volunteer_order";
        public const string MissingTower = "missing_tower";
        public const string SharedTower = "shared_tower";
        public const string BadAnchor = "bad_anchor";
        public const string InteriorDisconnected = "interior_disconnected";
        public const string NoWayOut = "no_way_out";
        public const string UnreachableClue = "unreachable_clue";
        public const string CompanionOnlyClue = "companion_only_clue";
        public const string NoRecovery = "no_recovery";
        public const string NoFinaleStage = "no_finale_stage";
        public const string DeadSpeaker = "dead_speaker";
        public const string FixedLocationText = "fixed_location_text";
        public const string RivalPlan = "rival_plan";
        /// <summary>I0 addition: the definition is missing (null) or a required part is null.</summary>
        public const string Missing = "missing";

        public static readonly string[] All =
        {
            DuplicateId, DanglingReference, BadVersion, CompanionCount, VolunteerOrder, MissingTower, SharedTower, BadAnchor,
            InteriorDisconnected, NoWayOut, UnreachableClue, CompanionOnlyClue, NoRecovery, NoFinaleStage, DeadSpeaker,
            FixedLocationText, RivalPlan, Missing,
        };
    }
}
