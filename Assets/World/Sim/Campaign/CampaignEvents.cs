namespace World.Sim.Campaign
{
    // PRD_CRYSTALS appendix A.4: events (Tick is stamped by the EventBuffer). The game animates these; it never writes state.

    public sealed class CampaignStarted : SimEvent { public string DefinitionId; }

    public sealed class LeadAdded : SimEvent { public int LeadId; public string DefId; public int Rank; }

    public sealed class LeadsReordered : SimEvent { public int TopLeadId; }

    /// <summary>Why: "done", "declined", "closed" (its requirements no longer hold), "dropped" (pushed out of the queue).</summary>
    public sealed class LeadRemoved : SimEvent { public int LeadId; public string Why; }

    /// <summary>One leg begins. Running (tuning pass): Mira runs this leg (PartyRunTicksPerStep); otherwise she walks (PartyTicksPerStep).</summary>
    public sealed class PartySetOut : SimEvent { public int LeadId; public MarkerRef From; public MarkerRef To; public int LegTicks; public bool Running; }

    public sealed class PartyArrived : SimEvent { public MarkerRef At; }

    public sealed class PartyModeChanged : SimEvent { public PartyMode Mode; }

    public sealed class QuestEventFired : SimEvent { public string EventId; }

    public sealed class FactLearned : SimEvent { public string Fact; public SourceKind Source; }

    public sealed class ChoiceOffered : SimEvent { public string ChoiceId; }

    public sealed class ChoiceAnswered : SimEvent { public string ChoiceId; public int Answer; public bool Correct; public bool ByPlayer; }

    /// <summary>Every custody change is one step with one of these (5.2).</summary>
    public sealed class CrystalCustodyChanged : SimEvent { public Element Crystal; public Custody From; public Custody To; public MarkerRef At; }

    public sealed class BeliefChanged : SimEvent { public Element Crystal; }

    public sealed class RivalStepStarted : SimEvent { public int Step; public Element Crystal; }

    /// <summary>Raised when the report reaches the party (its DueTick), not when the rival moved.</summary>
    public sealed class RivalReported : SimEvent { public Element Crystal; public RivalReportKind Kind; public MarkerRef At; }

    /// <summary>A charge on the expedition clock (not the +1 per tick). Why: one of <see cref="RivalCharges"/>.</summary>
    public sealed class RivalAdvanced : SimEvent { public long Amount; public string Why; }

    /// <summary>The expedition stops until RivalProgress reaches Until (an Ash camp fell).</summary>
    public sealed class RivalPaused : SimEvent { public long Until; }

    /// <summary>The party stands before a lethal encounter; Volunteer = companion id who will cover the retreat, "" = none.</summary>
    public sealed class EncounterThreshold : SimEvent { public string EncounterId; public string Volunteer; }

    /// <summary>The game writes the checkpoint and stages the duel from this (Profile = WardOpponentProfile id).</summary>
    public sealed class EncounterCommitted : SimEvent
    {
        public int AttemptId;
        public string EncounterId;
        public string Volunteer;
        public ulong Seed;
        public string Profile;
    }

    public sealed class EncounterResolved : SimEvent { public int AttemptId; public string EncounterId; public DuelResult Result; }

    public sealed class CompanionFell : SimEvent { public string CompanionId; public string EncounterId; }

    /// <summary>Why: "defeat", "draw", "withdraw", "camp_overrun", "threshold_wait".</summary>
    public sealed class PartyRetreated : SimEvent { public MarkerRef To; public string Why; }

    public sealed class CrystalRestored : SimEvent { public Element Crystal; public string Tower; public int Count; }

    public sealed class ScoutSent : SimEvent { public string CompanionId; public string Target; }

    public sealed class ScoutReported : SimEvent { public Element Crystal; }

    public sealed class ScoutReturned : SimEvent { }

    public sealed class JournalAdded : SimEvent { public string Text; }

    /// <summary>The run ended (5.8). Record is the CampaignRecord handed to the later ages (N-6).</summary>
    public sealed class CampaignEnded : SimEvent { public bool Won; public string EndingId; public CampaignRecord Record; }

    /// <summary>Why codes of RivalAdvanced (the rows of the 5.1 table that are charges).</summary>
    public static class RivalCharges
    {
        public const string Defeat = "defeat";
        public const string Draw = "draw";
        public const string WrongAnswer = "wrong_answer";
        public const string CampOverrun = "camp_overrun";
        public const string Dev = "dev";
    }
}
