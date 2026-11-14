namespace World.Sim.Campaign
{
    // PRD_CRYSTALS appendix A.3: commands (PlayerId = 0). Every one is validated at once and applied at the next tick by
    // the handlers CampaignModule registers. Refusal codes and sentences are in CampaignRefusals.

    /// <summary>Starts a campaign from a resolved definition (validated; refused while one is running). Switches the island to campaign mode.</summary>
    public sealed class StartCampaign : Command { public CampaignDefinition Definition; }

    /// <summary>The player is with Mira (true: "I'm with you") or away (false: "Go on without me").</summary>
    public sealed class SetCompany : Command { public bool Present; }

    /// <summary>"Lead the way": follow this lead (queue id from LeadAdded / CampaignApi.Leads).</summary>
    public sealed class AcceptLead : Command { public int LeadId; }

    /// <summary>"Not that way": the next lead; this one waits DeclineCooldown.</summary>
    public sealed class DeclineLead : Command { public int LeadId; }

    /// <summary>
    /// Answers the open choice (index into its answers). The player's advice and Mira's own answer go through the same
    /// Sim action (CR-21). Named so it cannot clash with PRD_AGES <c>World.Sim.Factions.Advise</c>.
    /// </summary>
    public sealed class AdviseMira : Command { public string ChoiceId; public int Answer; }

    /// <summary>Send the scout to a location (one scout at a time, Tansy only).</summary>
    public sealed class SuggestScout : Command { public string LocationId; }

    /// <summary>"Fight": commits the lethal encounter at the party's threshold and creates the attempt.</summary>
    public sealed class CommitEncounter : Command { public string EncounterId; }

    /// <summary>"Not yet": steps back from the threshold before committing. Safe, no cost.</summary>
    public sealed class WithdrawFromEncounter : Command { public string EncounterId; }

    /// <summary>The duel ended: applied once, for the pending attempt with this id only (CR-29).</summary>
    public sealed class ResolveEncounter : Command { public int AttemptId; public DuelResult Result; }

    /// <summary>On Continue with a pending attempt: stage the duel again with a new seed, no penalty (Restarts + 1).</summary>
    public sealed class RestartAttempt : Command { public int AttemptId; }

    /// <summary>"Set it": restore the carried crystal at its tower.</summary>
    public sealed class ConfirmRestore : Command { public Element Crystal; }

    /// <summary>F1 tools and tests only.</summary>
    public sealed class DevSetRivalProgress : Command { public long Value; }

    /// <summary>F1 tools and tests only: the rival completes its current step now.</summary>
    public sealed class DevRivalNextStep : Command { }

    /// <summary>F1 tools and tests only.</summary>
    public sealed class DevSetCustody : Command { public Element Crystal; public Custody To; }

    /// <summary>F1 tools and tests only (revive or kill a companion).</summary>
    public sealed class DevSetCompanion : Command { public string Id; public bool Alive; }

    /// <summary>Refusal codes of the campaign and their exact sentences (A.3).</summary>
    public static class CampaignRefusals
    {
        public const string NoCampaign = "no_campaign";
        public const string CampaignRunning = "campaign_running";
        public const string CampaignOver = "campaign_over";
        public const string InvalidDefinition = "invalid_definition";
        public const string UnknownLead = "unknown_lead";
        public const string NotAtChoice = "not_at_choice";
        public const string NotAtThreshold = "not_at_threshold";
        public const string ScoutOut = "scout_out";
        public const string NoScout = "no_scout";
        public const string UnknownAttempt = "unknown_attempt";
        public const string AlreadyResolved = "already_resolved";
        public const string NotCarried = "not_carried";
        public const string WrongTower = "wrong_tower";
        public const string PlayerNeeded = "player_needed";
        /// <summary>I0 addition: SuggestScout to a location the definition does not have.</summary>
        public const string UnknownPlace = "unknown_place";

        public static readonly string[] All =
        {
            NoCampaign, CampaignRunning, CampaignOver, InvalidDefinition, UnknownLead, NotAtChoice, NotAtThreshold, ScoutOut,
            NoScout, UnknownAttempt, AlreadyResolved, NotCarried, WrongTower, PlayerNeeded, UnknownPlace,
        };

        /// <summary>The sentence; arg fills {first problem} (invalid_definition) or {name} (scout_out: also refused to CommitEncounter while the scout is out).</summary>
        public static string Sentence(string code, string arg = null)
        {
            switch (code)
            {
                case NoCampaign: return "No expedition is under way.";
                case CampaignRunning: return "An expedition is already under way.";
                case CampaignOver: return "The expedition is over.";
                case InvalidDefinition: return "That campaign cannot be played: " + (string.IsNullOrEmpty(arg) ? "it is not valid." : arg);
                case UnknownLead: return "Mira has moved on from that.";
                case NotAtChoice: return "Nobody is asking that now.";
                case NotAtThreshold: return "There is no fight to start here.";
                case ScoutOut: return "Wait for " + (string.IsNullOrEmpty(arg) ? "the scout" : arg) + " to come back first.";
                case NoScout: return "There is nobody left to send.";
                case UnknownAttempt: return "That duel is not the one under way.";
                case AlreadyResolved: return "That duel is already settled.";
                case NotCarried: return "Mira is not carrying that crystal.";
                case WrongTower: return "This is not its tower.";
                case PlayerNeeded: return "Mira waits for you before she does that.";
                case UnknownPlace: return "Mira does not know that place.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code, string arg = null) => CommandResult.Reject(code, Sentence(code, arg));
    }

    /// <summary>Maps a finished Ward duel to the campaign's result (Forfeited = the confirmed surrender).</summary>
    public static class DuelResults
    {
        /// <summary>False for WardOutcome.None (the duel has not ended).</summary>
        public static bool TryFrom(World.Sim.Encounters.Ward.WardOutcome o, out DuelResult r)
        {
            switch (o)
            {
                case World.Sim.Encounters.Ward.WardOutcome.Victory: r = DuelResult.Victory; return true;
                case World.Sim.Encounters.Ward.WardOutcome.Defeat: r = DuelResult.Defeat; return true;
                case World.Sim.Encounters.Ward.WardOutcome.Clash: r = DuelResult.Clash; return true;
                case World.Sim.Encounters.Ward.WardOutcome.Stalemate: r = DuelResult.Stalemate; return true;
                case World.Sim.Encounters.Ward.WardOutcome.Forfeited: r = DuelResult.Surrender; return true;
                default: r = DuelResult.Defeat; return false;
            }
        }
    }
}
