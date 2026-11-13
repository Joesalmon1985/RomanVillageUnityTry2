using System.Collections.Generic;
using Newtonsoft.Json;
using World.Sim.Demons;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The two peoples: strength, proposals (the prince's brain), the overrun warning and the ending.</summary>
        public Factions.FactionsState Factions;
    }
}

namespace World.Sim.Factions
{
    public enum ProposalKind { Building = 0, Road = 1, Settlement = 2, City = 3, Banish = 4 }

    /// <summary>The ending from the player's side.</summary>
    public enum GameOutcome { None = 0, Victory = 1, Defeat = 2, Draw = 3 }

    public static class OutcomeReasons
    {
        public const string Overrun = "overrun";
        public const string Time = "time";
    }

    /// <summary>Villager ids who pitch proposals (REFACTOR_SPEC 6.3).</summary>
    public static class Speakers
    {
        public const string Prince = "prince";
        public const string Sawyer = "sawyer";
        public const string Merchant = "merchant";
    }

    public sealed class FactionTuning
    {
        public int StrengthPerSettlement = 1;
        public int StrengthPerCity = 2;
        public int StrengthPerFedWarrior = 1;
        public int OverrunMargin = 3;
        public int WarningTicks = 900;
        public int TimeLimitTicks = 12000;
        public int PlanIntervalTicks = 20;
        public int QueueLength = 3;
        public int DeclineCooldownTicks = 600;
        public int MaxOpenSites = 3;
        public int RivalCastDelayTicks = 450;
        public int ExpansionReachEdges = 3;
        /// <summary>The game layer allows a cast only within this distance of the target point.</summary>
        public double CastRangeMetres = 15;

        // Brain scores that tuning may change (6.2; added by S).
        /// <summary>Score of a city upgrade candidate (spec start value 55).</summary>
        public int CityScore = 55;
        /// <summary>Added to road and settlement candidates (spec start value 0).</summary>
        public int ExpansionBonus = 0;
        /// <summary>Added to road, settlement, city and lodge candidates while the people is the stronger (spec start value 0).</summary>
        public int PressAdvantageBonus = 0;
        /// <summary>The lead in strength from which PressAdvantageBonus applies.</summary>
        public int PressAdvantageLead = 1;
        /// <summary>PressAdvantageBonus applies only while the other people's strength has not changed for this long.</summary>
        public int PressIdleTicks = 0;
        /// <summary>Score of a warrior lodge candidate once the people has a city (spec start value 38).</summary>
        public int LodgeScore = 38;
        /// <summary>Banish candidates score this + 10 per demon (spec start value 100 for both).</summary>
        public int BanishCornerScore = 100;
        public int BanishEdgeScore = 100;
        /// <summary>Workshop bonus per unit of its first input the people holds above WorkshopSurplusFloor, up to WorkshopSurplusCap.</summary>
        public int WorkshopSurplusPerUnit = 0;
        public int WorkshopSurplusFloor = 4;
        public int WorkshopSurplusCap = 15;
    }

    public static class FactionTunings
    {
        /// <summary>
        /// The values a new game uses: the spec's starting values (FactionTuning's initialisers) with S's tuning (PROGRESS.md
        /// tuning log): rival cast delay 40 s, city 90, lodge 60, banish on a road edge 40 + 10 per demon, workshop surplus
        /// bonus 1 per unit above 4 (up to 15), and +30 to strength builds while ahead of a people whose strength has
        /// not changed for 5 minutes.
        /// </summary>
        public static FactionTuning Tuned() => new FactionTuning
        {
            RivalCastDelayTicks = 400, CityScore = 90, LodgeScore = 60, BanishEdgeScore = 40,
            WorkshopSurplusPerUnit = 1, WorkshopSurplusCap = 15, PressAdvantageBonus = 30, PressIdleTicks = 3000,
        };
    }

    /// <summary>All faction state (WorldState.Factions).</summary>
    public sealed class FactionsState
    {
        public FactionTuning Tuning = new FactionTuning();
        /// <summary>Index = people (<see cref="PlayerIds"/>).</summary>
        public List<Faction> Factions = new List<Faction>();
        /// <summary>Open proposals only, sorted by faction then rank.</summary>
        public List<Proposal> Proposals = new List<Proposal>();
        public int NextProposalId;
        public List<DeclinedProposal> Declined = new List<DeclinedProposal>();
        public OverrunWarning Warning = new OverrunWarning();
        public bool GameOver;
        public GameOutcome Outcome;
        /// <summary>"overrun" or "time"; null while the game runs.</summary>
        public string OutcomeReason;
        public long EndedTick = -1;
        /// <summary>Spells cast per people.</summary>
        public int[] Casts = new int[PlayerIds.Count];
        /// <summary>Per people: the opening proposal (woodcutter's hut) has been cast.</summary>
        public bool[] OpeningDone = new bool[PlayerIds.Count];
    }

    public sealed class Faction
    {
        public int Id;
        /// <summary>"Your people", "the Ash Clan".</summary>
        public string Name;
        /// <summary>True when the brain casts its own proposals (the rival).</summary>
        public bool Auto;
        public int Strength;
        public int Settlements;
        public int Cities;
        public int FedWarriors;
        public int HungryWarriors;
        public long NextPlanTick;
        /// <summary>Tick the strength last changed (0 at the start).</summary>
        public long StrengthChangedTick;
    }

    /// <summary>An open proposal of a faction's brain: what, where, why, and who pitches it.</summary>
    public sealed class Proposal
    {
        public int Id;
        public int Faction;
        public ProposalKind Kind;
        /// <summary>World.Sim.Economy.BuildingType as int, or -1.</summary>
        public int BuildingType = -1;
        /// <summary>Target settlement (building, city), or -1.</summary>
        public int Settlement = -1;
        public int Plot = -1;
        public int HexEdge = -1;
        public int Corner = -1;
        public DemonSiteKind DemonSiteKind;
        public int DemonSiteId = -1;
        /// <summary>One sentence: why.</summary>
        public string Reason;
        /// <summary>Villager id (<see cref="Speakers"/>).</summary>
        public string Speaker;
        public int Score;
        /// <summary>0 = top.</summary>
        public int Rank;
        public long CreatedTick;
        public long TopSinceTick = -1;

        /// <summary>Identity of the action: Kind|BuildingType|Plot|HexEdge|Corner|DemonSiteKind|DemonSiteId (ints).</summary>
        [JsonIgnore]
        public string Key => MakeKey(Kind, BuildingType, Plot, HexEdge, Corner, DemonSiteKind, DemonSiteId);

        public static string MakeKey(ProposalKind kind, int buildingType, int plot, int hexEdge, int corner, DemonSiteKind siteKind, int siteId) =>
            (int)kind + "|" + buildingType + "|" + plot + "|" + hexEdge + "|" + corner + "|" + (int)siteKind + "|" + siteId;
    }

    public sealed class DeclinedProposal
    {
        public int Faction;
        public string Key;
        public long UntilTick;
    }

    public sealed class OverrunWarning
    {
        public bool Active;
        public int Attacker = -1;
        public int Defender = -1;
        public long StartTick;
        public long EndTick;
    }

    // Commands (normal play).

    /// <summary>Casts an open proposal of your people: re-checks it and starts the site (or banishes).</summary>
    public sealed class CastSpell : Command { public int ProposalId; }

    /// <summary>Declines an open proposal of your people; the same action is not proposed again for DeclineCooldownTicks.</summary>
    public sealed class DeclineProposal : Command { public int ProposalId; }

    public static class FactionRefusals
    {
        public const string GameOver = "game_over";
        public const string UnknownProposal = "unknown_proposal";
        public const string NoLongerLegal = "no_longer_legal";

        public static string Sentence(string code, string underlying = null)
        {
            switch (code)
            {
                case GameOver: return "The game is over.";
                case UnknownProposal: return "That proposal is gone.";
                case NoLongerLegal: return "That can no longer be done: " + (underlying ?? "");
                default: return code;
            }
        }

        public static CommandResult Reject(string code, string underlying = null) => CommandResult.Reject(code, Sentence(code, underlying));
    }

    // Events (REFACTOR_SPEC 5.3).

    public sealed class ProposalAdded : SimEvent { public int ProposalId; public int Faction; public int Rank; }

    public sealed class ProposalsReordered : SimEvent { public int Faction; public int TopProposalId; }

    /// <summary>Why: "cast", "declined" or "expired".</summary>
    public sealed class ProposalRemoved : SimEvent { public int ProposalId; public int Faction; public string Why; }

    public sealed class SpellCast : SimEvent
    {
        public int ProposalId;
        public int Faction;
        public ProposalKind Kind;
        public int BuildingType = -1;
        public int Plot = -1;
        public int HexEdge = -1;
        public int Corner = -1;
        public DemonSiteKind DemonSiteKind;
        public int DemonSiteId = -1;
    }

    public sealed class StrengthChanged : SimEvent { public int Faction; public int Strength; }

    public sealed class OverrunWarningStarted : SimEvent { public int Attacker; public int Defender; public long EndTick; }

    public sealed class OverrunWarningCancelled : SimEvent { public int Attacker; public int Defender; }

    public sealed class FactionOverrun : SimEvent { public int Attacker; public int Defender; }

    public sealed class GameEnded : SimEvent { public GameOutcome Outcome; public string Reason; public int PlayerStrength; public int RivalStrength; }
}
