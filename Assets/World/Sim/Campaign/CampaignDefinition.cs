using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace World.Sim.Campaign
{
    // PRD_CRYSTALS appendix A.1: the campaign definition (a resolved data pack). Plain data, public fields, lists in a
    // fixed order so the state JSON (which holds the whole definition) is deterministic. Binding from tag cry-I0-sim;
    // changes are additive and logged in Docs/CAMPAIGN_API.md. Every class is partial so the loader and validator
    // package (I2) can add fields from Sim/Campaign/Definition without editing this file.

    public enum Element { Earth = 0, Fire = 1, Water = 2, Air = 3 }

    public enum Custody { AtSite = 0, RivalTransit = 1, AtRivalAltar = 2, Carried = 3, Restored = 4 }

    public enum PackStatus { Partial = 0, Complete = 1 }

    public enum LocationKind { Camp = 0, Site = 1, Tower = 2, Watchtower = 3, Shore = 4 }

    public enum AnchorKind { HomeCamp = 0, MeadowHexCentre = 1, RimCorner = 2 }

    public enum MarkerKind
    {
        Exit = 0, Stairs = 1, Door = 2, Pedestal = 3, Altar = 4, Threshold = 5, SafePoint = 6, Inscription = 7, Witness = 8,
        Lever = 9, DuelCaster = 10, DuelOpponent = 11, Plinth = 12,
    }

    public enum SourceKind { Witness = 0, Marker = 1, Companion = 2, Scout = 3, Report = 4, Wizard = 5, Opening = 6 }

    public enum QuestAction { Arrive = 0, Talk = 1, Examine = 2, Search = 3, Open = 4, Light = 5, Take = 6, Answer = 7, Restore = 8 }

    public enum EncounterKind { Lethal = 0, Friendly = 1, Practice = 2 }

    /// <summary>
    /// What a quest event or a won encounter does besides setting facts (I0 addition to A.1; facts themselves are the
    /// <c>Sets</c> lists, or <see cref="SetFact"/> where only effects exist, as in <see cref="EncounterDef.OnVictory"/>).
    /// </summary>
    public enum EffectKind
    {
        /// <summary>Ref = fact.</summary>
        SetFact = 0,
        /// <summary>Crystal = crystal id: AtSite or AtRivalAltar becomes Carried.</summary>
        TakeCrystal = 1,
        /// <summary>Ref = choice id: the party mode becomes AskingChoice.</summary>
        OfferChoice = 2,
        /// <summary>Ref = line id, written to the journal.</summary>
        Journal = 3,
        /// <summary>At = where the party is put.</summary>
        PlaceParty = 4,
        /// <summary>Amount = rival progress added (a wrong step, a smoked-out kiln).</summary>
        RivalCharge = 5,
        /// <summary>Crystal = crystal id: the party's belief about it is set to the truth (Orrin, a scout, a pedestal).</summary>
        SetBelief = 6,
    }

    /// <summary>How many companions are alive at an ending (selects an ending variant).</summary>
    public enum SurvivorBand { All = 0, Some = 1, None = 2 }

    /// <summary>
    /// A marker anywhere in the campaign. Interior "" (or null) = on the surface round the location's anchor. Text form
    /// (pack JSON fields such as <see cref="MarkerDef.To"/>): "location/marker" on the surface, "location/interior/marker"
    /// inside an interior entered from that location.
    /// </summary>
    public partial struct MarkerRef : IEquatable<MarkerRef>
    {
        public string Location, Interior, Marker;

        public MarkerRef(string location, string interior, string marker)
        {
            Location = location;
            Interior = interior;
            Marker = marker;
        }

        public static MarkerRef Surface(string location, string marker) => new MarkerRef(location, "", marker);

        /// <summary>True when no location and no marker is named.</summary>
        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrEmpty(Location) && string.IsNullOrEmpty(Marker);

        [JsonIgnore]
        public bool IsInterior => !string.IsNullOrEmpty(Interior);

        /// <summary>Parses the text form; false (and an empty ref) for anything else.</summary>
        public static bool TryParse(string text, out MarkerRef r)
        {
            r = default;
            if (string.IsNullOrEmpty(text)) return false;
            var p = text.Split('/');
            if (p.Length == 2 && p[0].Length > 0 && p[1].Length > 0) { r = new MarkerRef(p[0], "", p[1]); return true; }
            if (p.Length == 3 && p[0].Length > 0 && p[1].Length > 0 && p[2].Length > 0) { r = new MarkerRef(p[0], p[1], p[2]); return true; }
            return false;
        }

        public bool Equals(MarkerRef o) =>
            string.Equals(Location ?? "", o.Location ?? "", StringComparison.Ordinal)
            && string.Equals(Interior ?? "", o.Interior ?? "", StringComparison.Ordinal)
            && string.Equals(Marker ?? "", o.Marker ?? "", StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MarkerRef m && Equals(m);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                foreach (var part in new[] { Location ?? "", Interior ?? "", Marker ?? "" })
                    foreach (char c in part) h = h * 31 + c;
                return h;
            }
        }

        public static bool operator ==(MarkerRef a, MarkerRef b) => a.Equals(b);
        public static bool operator !=(MarkerRef a, MarkerRef b) => !a.Equals(b);

        /// <summary>The text form ("" when empty).</summary>
        public override string ToString() =>
            IsEmpty ? "" : IsInterior ? Location + "/" + Interior + "/" + Marker : Location + "/" + Marker;
    }

    /// <summary>
    /// A marker. On the surface I, J are lattice offsets from the location's anchor cell; in an interior they are the
    /// interior's own cells (column i, row j of <see cref="InteriorDef.Rows"/>; for a legend entry the loader fills them
    /// from the rows). To: the text form of a <see cref="MarkerRef"/> (Exit, Stairs). OpenWhen: a fact (Door).
    /// Crystal: a crystal id (Pedestal, Altar).
    /// </summary>
    public sealed partial class MarkerDef
    {
        public string Id = "";
        public MarkerKind Kind;
        public int I, J;
        public string To = "", OpenWhen = "", Crystal = "";
        public string Name = "";
    }

    /// <summary>A fixed role and the actor bound to it (mira, prince, rival_leader, final_enemy, wizard).</summary>
    public sealed partial class RoleDef
    {
        public string Id = "", ActorId = "", Name = "";
    }

    /// <summary>Service ids of <see cref="CompanionDef.Service"/> (section 4.2).</summary>
    public static class CompanionServices
    {
        public const string Shield = "shield";   // Garrick: a retreat after a clash or stalemate costs half
        public const string Lamp = "lamp";       // Ivo: interior maps show undiscovered markers as "?"
        public const string Fire = "fire";       // Petra: the kiln order (Fire shortcut)
        public const string Water = "water";     // Nell: the flood marker, the cistern sluice
        public const string Earth = "earth";     // Odo: the forest marks (Earth shortcut)
        public const string Scout = "scout";     // Tansy: scouting
    }

    public sealed partial class CompanionDef
    {
        public string Id = "", Name = "", Role = "", Line = "";
        /// <summary>1 to 6: on a lethal defeat the first living companion in this order covers the retreat.</summary>
        public int VolunteerOrder;
        /// <summary>One of <see cref="CompanionServices"/>, or "".</summary>
        public string Service = "";
        public string ActorId = "";
    }

    public sealed partial class AnchorDef
    {
        public AnchorKind Kind;
        /// <summary>Hex (q, r) of a MeadowHexCentre anchor (BoardGenerator coordinates).</summary>
        public int Q, R;
        /// <summary>Corner id of a RimCorner anchor; -1 = by the bearing rule of appendix C for the tower's element.</summary>
        public int Corner = -1;
    }

    public sealed partial class LocationDef
    {
        public string Id = "", Name = "";
        public LocationKind Kind;
        public AnchorDef Anchor = new AnchorDef();
        public List<MarkerDef> Markers = new List<MarkerDef>();
        /// <summary>Marker id of the safe point on the surface here ("" = the anchor).</summary>
        public string SafePoint = "";
    }

    /// <summary>
    /// An interior (5.10): rows of text, one character per lattice cell ('#' rock, '.' floor, any legend character a
    /// floor cell with that marker). Size limit 16 by 16, at most 80 floor cells.
    /// </summary>
    public sealed partial class InteriorDef
    {
        public string Id = "", Name = "";
        public List<string> Rows = new List<string>();
        public SortedDictionary<char, MarkerDef> Legend = new SortedDictionary<char, MarkerDef>();
    }

    /// <summary>A link between two markers (a stair, a door, a causeway), open when the fact is known ("" = always).</summary>
    public sealed partial class ConnectionDef
    {
        public string Id = "";
        public MarkerRef A, B;
        public string OpenWhen = "";
    }

    public sealed partial class CrystalDef
    {
        public string Id = "", Name = "";
        public Element Element;
        /// <summary>The pedestal marker at the original site.</summary>
        public MarkerRef Site;
        /// <summary>Tower id.</summary>
        public string Tower = "";
        public bool Underground;
    }

    public sealed partial class TowerDef
    {
        public string Id = "", Location = "";
        public Element Element;
        /// <summary>Line id of the door sign.</summary>
        public string Sign = "";
        /// <summary>"#RRGGBB".</summary>
        public string BeamColour = "";
    }

    public sealed partial class RivalStepDef
    {
        /// <summary>Crystal id.</summary>
        public string Crystal = "";
        public int MarchCost, CarryCost;
        public MarkerRef Altar;
    }

    public sealed partial class RivalPlanDef
    {
        public string LeaderRole = "rival_leader";
        public List<RivalStepDef> Steps = new List<RivalStepDef>();
        /// <summary>Crystal ids already on their altar when the campaign starts.</summary>
        public List<string> StolenAtStart = new List<string>();
    }

    public sealed partial class ClueSourceDef
    {
        public SourceKind Kind;
        /// <summary>The witness role or location, the marker (text form), the companion id, ...</summary>
        public string Ref = "";
    }

    public sealed partial class ClueDef
    {
        public string Id = "";
        public ClueSourceDef Source = new ClueSourceDef();
        public List<string> Requires = new List<string>();
        public List<string> Gives = new List<string>();
        public bool Essential;
        /// <summary>Clue id of the fallback ("" = none). An essential clue's fallback never has a Companion source.</summary>
        public string Fallback = "";
    }

    public sealed partial class GoalDef
    {
        public MarkerRef At;
        public QuestAction Action;
    }

    public sealed partial class LeadDef
    {
        public string Id = "", Title = "";
        public GoalDef Goal = new GoalDef();
        public List<string> Requires = new List<string>();
        /// <summary>The fact that marks the lead done.</summary>
        public string DoneWhen = "";
        /// <summary>Line id of the one-sentence reason.</summary>
        public string ReasonLine = "";
        public int Priority;
        /// <summary>Crystal id the lead serves ("" = none).</summary>
        public string Crystal = "";
    }

    public sealed partial class AnswerDef
    {
        /// <summary>Line id of the answer as spoken.</summary>
        public string Line = "";
        public bool Correct;
    }

    public sealed partial class ChoiceDef
    {
        public string Id = "";
        public string QuestionLine = "";
        /// <summary>At most three.</summary>
        public List<AnswerDef> Answers = new List<AnswerDef>();
        /// <summary>The marker whose reading reveals the answer (Mira studies it when the player is away).</summary>
        public MarkerRef RevealedBy;
        public string WrongLine = "";
        /// <summary>Facts set by a correct answer (I0 addition).</summary>
        public List<string> Sets = new List<string>();
    }

    public sealed partial class EffectDef
    {
        public EffectKind Kind;
        public string Crystal = "";
        public string Ref = "";
        public MarkerRef At;
        public int Amount;
    }

    /// <summary>Where the duel is staged (5.12): Mira on Caster, the opponent on Opponent, the volunteer at the way out.</summary>
    public sealed partial class DuelStageDef
    {
        public MarkerRef Caster, Opponent, Volunteer;
    }

    public static class EncounterBindings
    {
        /// <summary>The finale: bound to the tower of the fourth crystal, not to a place (5.8).</summary>
        public const string LastTower = "last_tower";
    }

    public sealed partial class EncounterDef
    {
        public string Id = "", Name = "";
        public EncounterKind Kind;
        /// <summary>WardOpponentProfile id.</summary>
        public string Profile = "";
        public string OpponentActorId = "", OpponentName = "";
        public MarkerRef Threshold, SafePoint;
        public DuelStageDef Stage = new DuelStageDef();
        public List<EffectDef> OnVictory = new List<EffectDef>();
        /// <summary>Quest event id of the alternate recovery without a duel ("" = none).</summary>
        public string Alternate = "";
        /// <summary>"" or <see cref="EncounterBindings.LastTower"/>.</summary>
        public string BoundTo = "";
    }

    public sealed partial class QuestEventDef
    {
        public string Id = "";
        public MarkerRef At;
        public QuestAction Action;
        public List<string> Requires = new List<string>();
        public List<string> Sets = new List<string>();
        public List<EffectDef> Effects = new List<EffectDef>();
        /// <summary>Line ids spoken when it fires.</summary>
        public List<string> Lines = new List<string>();
        public bool Once = true;
        /// <summary>Extra conditions in the guard grammar of <see cref="CampaignGuards"/> (I0 addition, e.g. "custody: earth = AtSite").</summary>
        public List<string> Guards = new List<string>();
    }

    public sealed partial class EndingVariantDef
    {
        public SurvivorBand Survivors;
        public List<string> Lines = new List<string>();
    }

    public sealed partial class EndingDef
    {
        public string Id = "";
        /// <summary>Won (the restored ending) or Lost (terminal defeat).</summary>
        public CampaignPhase Outcome;
        public List<string> Lines = new List<string>();
        public List<EndingVariantDef> Variants = new List<EndingVariantDef>();
    }

    /// <summary>A spoken line. Text may hold tokens (<see cref="CampaignTokens"/>); Guards use <see cref="CampaignGuards"/>.</summary>
    public sealed partial class LineDef
    {
        public string Id = "", Speaker = "", Text = "";
        public List<string> Guards = new List<string>();
        /// <summary>Topic for <c>CampaignApi.LinesFor</c> ("" = none).</summary>
        public string Topic = "";
    }

    /// <summary>All numbers of section 5 (starting values). Saved with the definition.</summary>
    public sealed partial class CampaignTuning
    {
        public int PartyTicksPerStep = 7, ScoutTicksPerStep = 4, ReadTicks = 50, SearchTicks = 150, TakeTicks = 50, RestoreTicks = 100;
        public int LeadPatience = 450, SoloDecide = 100, ChoicePatience = 300, SoloStudyTicks = 200, ThresholdWait = 900;
        public int DeclineCooldown = 600, PlanInterval = 20, QueueLength = 3, StaleTicks = 3000, ReportDelay = 300, SearchCost = 300;
        public int DefeatCharge = 900, DrawCharge = 600, DrawChargeWithShield = 300, WrongAnswerCharge = 300;
        public int CampOverrunCharge = 1800, AshCampLostPause = 900;
        public int TideCycle = 2400, TideDry = 900, BoonPercent = 20;
    }

    /// <summary>One versioned campaign: the authored pack or a generated one, merged from its files (6.1).</summary>
    public sealed partial class CampaignDefinition
    {
        /// <summary>The newest format this build reads.</summary>
        public const int CurrentFormatVersion = 1;

        public int FormatVersion = 1;
        public string Id = "", Title = "";
        public PackStatus Status;
        public string Template = "";
        public ulong Seed;
        public string GeneratorVersion = "";
        public List<RoleDef> Roles = new List<RoleDef>();
        public List<CompanionDef> Companions = new List<CompanionDef>();
        public List<LocationDef> Locations = new List<LocationDef>();
        public List<InteriorDef> Interiors = new List<InteriorDef>();
        public List<ConnectionDef> Connections = new List<ConnectionDef>();
        public List<CrystalDef> Crystals = new List<CrystalDef>();
        public List<TowerDef> Towers = new List<TowerDef>();
        public RivalPlanDef Rival = new RivalPlanDef();
        public List<ClueDef> Clues = new List<ClueDef>();
        public List<LeadDef> Leads = new List<LeadDef>();
        public List<ChoiceDef> Choices = new List<ChoiceDef>();
        public List<EncounterDef> Encounters = new List<EncounterDef>();
        public List<QuestEventDef> Events = new List<QuestEventDef>();
        public List<EndingDef> Endings = new List<EndingDef>();
        public List<LineDef> Lines = new List<LineDef>();
        public CampaignTuning Tuning = new CampaignTuning();

        // Lookups by id (null when missing). Linear: definitions are small and this keeps the class plain data.
        public RoleDef Role(string id) => Roles?.Find(x => x.Id == id);
        public CompanionDef Companion(string id) => Companions?.Find(x => x.Id == id);
        public LocationDef Location(string id) => Locations?.Find(x => x.Id == id);
        public InteriorDef Interior(string id) => Interiors?.Find(x => x.Id == id);
        public ConnectionDef Connection(string id) => Connections?.Find(x => x.Id == id);
        public CrystalDef Crystal(string id) => Crystals?.Find(x => x.Id == id);
        public CrystalDef Crystal(Element e) => Crystals?.Find(x => x.Element == e);
        public TowerDef Tower(string id) => Towers?.Find(x => x.Id == id);
        public ClueDef Clue(string id) => Clues?.Find(x => x.Id == id);
        public LeadDef Lead(string id) => Leads?.Find(x => x.Id == id);
        public ChoiceDef Choice(string id) => Choices?.Find(x => x.Id == id);
        public EncounterDef Encounter(string id) => Encounters?.Find(x => x.Id == id);
        public QuestEventDef Event(string id) => Events?.Find(x => x.Id == id);
        public EndingDef Ending(string id) => Endings?.Find(x => x.Id == id);
        public LineDef Line(string id) => Lines?.Find(x => x.Id == id);
    }
}
