using System.Collections.Generic;
using Newtonsoft.Json;

namespace World.Sim.Campaign
{
    // Run-state fields added by package I1 (Sim/Campaign/Runtime) through the partial classes of A.2. Every field has a
    // safe default, so a state written by the I0 shape loads and plays on (CampaignState.FormatVersion stays 1: the
    // shape only grew). All of it is plain saved data: the runtime keeps no other memory, so a save at any tick
    // continues identically.

    /// <summary>Why the party is on its current route (what it does when it gets there).</summary>
    public enum RoutePurpose
    {
        /// <summary>No route, or a route with no purpose (it just stops there).</summary>
        None = 0,
        /// <summary>Following a lead: act at the goal.</summary>
        Lead = 1,
        /// <summary>Withdrawing or retreating on foot (threshold wait, "Not yet", a camp overrun).</summary>
        Retreat = 2,
        /// <summary>Going to study the marker that reveals the open choice's answer (Mira alone).</summary>
        Study = 3,
        /// <summary>Going back to the open choice to answer it.</summary>
        ChoiceReturn = 4,
        /// <summary>Going back to a threshold she withdrew from, to hold there until the player comes.</summary>
        Hold = 5,
    }

    public sealed partial class CampaignState
    {
        /// <summary>Rival progress the expedition has already used (marching, carrying, searching, paused, idle).</summary>
        public long RivalSpent;
        /// <summary>The phase the rival goes back to when a pause ends.</summary>
        public RivalPhase RivalResumePhase;
        /// <summary>How many of <see cref="Reports"/> have reached the party (RivalReported raised).</summary>
        public int ReportsRaised;
        /// <summary>Leads acted on without result: not offered again until something changes (see <see cref="SpentLead"/>).</summary>
        public List<SpentLead> Spent = new List<SpentLead>();
        /// <summary>Choices offered and not yet answered correctly, with where they were asked.</summary>
        public List<OpenChoice> OpenChoices = new List<OpenChoice>();
        /// <summary>Lethal encounters won (sorted ordinal).</summary>
        public List<string> CompletedEncounters = new List<string>();
        /// <summary>Mira re-plans her lead queue at the end of this tick, not only every PlanInterval.</summary>
        public bool ReplanNow;
        /// <summary>EconomyTuning.GrowTicks when the campaign started (boons are applied from it); null = not captured.</summary>
        public int[] BaseGrowTicks;
        /// <summary>EconomyTuning.CarrierTicksPerStep when the campaign started; -1 = not captured.</summary>
        public int BaseCarrierTicksPerStep = -1;
    }

    public sealed partial class PartyState
    {
        /// <summary>Waypoints still to reach after the current leg (the last one is the goal).</summary>
        public List<MarkerRef> Route = new List<MarkerRef>();
        public RoutePurpose Purpose;
        /// <summary>Definition id of the lead being followed ("" = none; implicit leads start with '@').</summary>
        public string LeadDefId = "";
        /// <summary>What to do at the lead's goal.</summary>
        public QuestAction LeadAction;
        /// <summary>Actions still to do at this place, in order.</summary>
        public List<QuestAction> Pending = new List<QuestAction>();
        /// <summary>While Acting: what, for how long, how far.</summary>
        public QuestAction ActingAction;
        public int ActingTicks, ActingProgress;
        /// <summary>While Acting: the acting is Mira's study of the open choice's marker.</summary>
        public bool Studying;
        /// <summary>While Acting a restoration: the crystal id.</summary>
        public string ActingCrystal = "";
        /// <summary>Start of the current wait (a threshold, a choice).</summary>
        public long WaitSinceTick;
        /// <summary>At a threshold she came back to while the player is away: she holds there with no time limit.</summary>
        public bool HoldAtThreshold;
        /// <summary>Encounter id she withdrew from while the player was away ("" = none). Cleared when the player rejoins.</summary>
        public string WithdrawnFrom = "";
        /// <summary>Where the open choice was asked.</summary>
        public MarkerRef ChoiceAt;
        /// <summary>Tick the current leg or action began: it advances from the next tick, so a leg of N ticks takes N ticks.</summary>
        public long BeganTick = -1;
        /// <summary>Progress signature when the party arrived at the lead's goal (to tell a fruitless visit).</summary>
        public long ArriveSignature;
    }

    public sealed partial class CompanionState
    {
        /// <summary>Display name of the place where they fell ("" while alive; the finale has no fixed place, so it is kept here).</summary>
        public string FellPlace = "";
    }

    public sealed partial class ScoutState
    {
        /// <summary>0 at home, 1 running out, 2 searching, 3 running back.</summary>
        public int Phase;
        /// <summary>Search ticks left (phase 2).</summary>
        public int Timer;
        /// <summary>Crystals whose truth the scout saw (reported on her return).</summary>
        public List<Element> Found = new List<Element>();
    }

    /// <summary>A lead that was acted on without result: closed while the progress signature stays the same.</summary>
    public sealed class SpentLead
    {
        public string DefId = "";
        public long Signature;
    }

    /// <summary>A choice that was offered and not yet answered correctly.</summary>
    public sealed class OpenChoice
    {
        public string ChoiceId = "";
        public MarkerRef At;
    }

    public sealed partial class LeadDef
    {
        /// <summary>
        /// Reason text for leads the runtime makes itself (ids starting with '@'), used when <see cref="ReasonLine"/> names
        /// no line. Never part of a pack (not serialized, so it never changes a definition's hash).
        /// </summary>
        [JsonIgnore]
        public string ReasonText;
    }
}
