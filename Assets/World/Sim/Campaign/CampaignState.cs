using System.Collections.Generic;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The crystal campaign's run state (PRD_CRYSTALS A.2). Null = no campaign: then nothing of it has any effect.</summary>
        public Campaign.CampaignState Campaign;
    }
}

namespace World.Sim.Campaign
{
    // PRD_CRYSTALS appendix A.2: the run state, separate from the definition it carries. Binding from tag cry-I0-sim.
    // Every class is partial so the runtime package (I1, Sim/Campaign/Runtime) can add fields from its own folder.
    // A shape change inside this state bumps CampaignState.FormatVersion, never the world save version (N-5).

    public enum CampaignPhase { Running = 0, Won = 1, Lost = 2 }

    public enum PartyMode { Idle = 0, Travelling = 1, Acting = 2, AskingChoice = 3, AtThreshold = 4, InEncounter = 5, WaitingForPlayer = 6 }

    public enum RivalPhase { Marching = 0, Carrying = 1, Searching = 2, Paused = 3, PlanDone = 4, AtLastTower = 5 }

    /// <summary>A campaign duel's result as the campaign sees it (Surrender = the confirmed "Leave encounter", counted as Defeat).</summary>
    public enum DuelResult { Victory = 0, Defeat = 1, Clash = 2, Stalemate = 3, Surrender = 4 }

    /// <summary>How a companion fell (CampaignRecord).</summary>
    public enum FallCause { Defeat = 0, Surrender = 1 }

    /// <summary>What a rival report says (RivalReported.Kind).</summary>
    public enum RivalReportKind { Marching = 0, Seized = 1, OnAltar = 2, Searching = 3, AtLastTower = 4 }

    public sealed partial class RivalState
    {
        public RivalPhase Phase;
        /// <summary>Index into the plan's steps.</summary>
        public int Step;
        /// <summary>Rival progress spent on the current step's phase.</summary>
        public long StepProgress;
        /// <summary>While Paused: the RivalProgress value at which the expedition moves again.</summary>
        public long PausedUntilProgress;
        public MarkerRef At;
    }

    /// <summary>The truth about one crystal.</summary>
    public sealed partial class CrystalState
    {
        public Custody Custody;
        public MarkerRef At;
        public long SinceTick;
    }

    /// <summary>What the party believes about one crystal.</summary>
    public sealed partial class Belief
    {
        public Custody Custody;
        public MarkerRef At;
        public long AsOfTick;
        public SourceKind Source;
    }

    public sealed partial class LegState
    {
        public MarkerRef From, To;
        public int Ticks;
        public int Progress;
        /// <summary>Interior path as i,j pairs (empty on the surface).</summary>
        public int[] Cells = new int[0];
    }

    public sealed partial class PartyState
    {
        public PartyMode Mode;
        public MarkerRef At;
        public bool PlayerPresent = true;
        /// <summary>Null when not travelling.</summary>
        public LegState Leg;
        /// <summary>Lead queue id being followed, -1 = none.</summary>
        public int LeadId = -1;
        public string ChoiceId = "";
        public string EncounterId = "";
        public long ModeSinceTick;
    }

    public sealed partial class CompanionState
    {
        public string Id = "";
        public bool Alive = true;
        /// <summary>Encounter id where they fell ("" while alive).</summary>
        public string FellAt = "";
        public long FellTick = -1;
        public FallCause Cause;
    }

    public sealed partial class LeadState
    {
        /// <summary>Stable queue id (kept while the lead stays in the queue).</summary>
        public int Id;
        public string DefId = "";
        public int Score;
        public int Rank;
        public long TopSinceTick = -1;
    }

    public sealed partial class DeclinedLead
    {
        public string DefId = "";
        public long UntilTick;
    }

    /// <summary>A committed lethal duel not yet resolved (5.6). Resolution is accepted once, for this Id only.</summary>
    public sealed partial class AttemptState
    {
        public int Id;
        public string EncounterId = "";
        /// <summary>Companion id who covers the retreat, "" = none left.</summary>
        public string Volunteer = "";
        public ulong Seed;
        public int Restarts;
        public long CommittedTick;
    }

    public sealed partial class AttemptRecord
    {
        public int Id;
        public string EncounterId = "";
        public DuelResult Result;
        public string Volunteer = "";
        public long Tick;
    }

    public sealed partial class ScoutState
    {
        public bool Out;
        public string CompanionId = "";
        /// <summary>Location id.</summary>
        public string Target = "";
        public LegState Leg;
    }

    public sealed partial class JournalEntry
    {
        public long Tick;
        public string Text = "";
        /// <summary>Location id ("" = none).</summary>
        public string Place = "";
    }

    /// <summary>A rival report (I0 addition to A.2). Known to the party from DueTick on (ReportDelay after the move).</summary>
    public sealed partial class RivalReport
    {
        public Element Crystal;
        public RivalReportKind Kind;
        public MarkerRef At;
        public long Tick;
        public long DueTick;
    }

    /// <summary>WorldState.Campaign (A.2). Null = no campaign.</summary>
    public sealed partial class CampaignState
    {
        /// <summary>Version of this state's own shape (1 = the shape I0 landed).</summary>
        public int FormatVersion = 1;

        public CampaignDefinition Definition;
        public string DefinitionHash = "";
        public CampaignPhase Phase;
        public long StartTick;
        public string EndingId = "";

        public long RivalProgress;
        public RivalState Rival = new RivalState();
        /// <summary>By Element (length 4).</summary>
        public CrystalState[] Crystals = new CrystalState[0];
        /// <summary>By Element (length 4).</summary>
        public Belief[] Beliefs = new Belief[0];
        public PartyState Party = new PartyState();
        /// <summary>In the definition's companion order.</summary>
        public List<CompanionState> Companions = new List<CompanionState>();
        /// <summary>Sorted ordinal; only ever added to.</summary>
        public List<string> Facts = new List<string>();
        public List<LeadState> Leads = new List<LeadState>();
        public int NextLeadId = 1;
        public List<DeclinedLead> Declined = new List<DeclinedLead>();
        /// <summary>The pending attempt, null when none.</summary>
        public AttemptState Attempt;
        public int NextAttemptId = 1;
        public List<AttemptRecord> Resolved = new List<AttemptRecord>();
        public ScoutState Scout = new ScoutState();
        public List<JournalEntry> Journal = new List<JournalEntry>();
        public List<Element> RestoredOrder = new List<Element>();
        /// <summary>Quest events with Once that have fired, sorted ordinal.</summary>
        public List<string> FiredEvents = new List<string>();
        /// <summary>DemonTuning.SevensPerDraw when the campaign started (boons and altars are applied from it).</summary>
        public int BaseSevensPerDraw;
        public Rng Rng = new Rng();

        // I0 additions to A.2 (logged in Docs/CAMPAIGN_API.md).
        /// <summary>Rival reports in the order raised; known from their DueTick.</summary>
        public List<RivalReport> Reports = new List<RivalReport>();
        /// <summary>Elements that were ever on an Ash altar (CampaignRecord.EverOnAltar), in the order they first got there.</summary>
        public List<Element> EverOnAltar = new List<Element>();

        public CrystalState Crystal(Element e) => Crystals != null && (int)e < Crystals.Length ? Crystals[(int)e] : null;
        public Belief BeliefOf(Element e) => Beliefs != null && (int)e < Beliefs.Length ? Beliefs[(int)e] : null;
        public CompanionState Companion(string id) => Companions?.Find(c => c.Id == id);
        public bool Knows(string fact) => !string.IsNullOrEmpty(fact) && Facts != null && Facts.BinarySearch(fact, System.StringComparer.Ordinal) >= 0;
    }
}
