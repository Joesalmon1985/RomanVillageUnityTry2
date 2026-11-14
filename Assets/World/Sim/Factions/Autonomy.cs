using System;
using System.Collections.Generic;
using World.Sim.Economy;
using World.Sim.Eras;

namespace World.Sim.Factions
{
    /// <summary>Kinds of advice the player gives a leader (PRD_AGES 8.4, A.5).</summary>
    public enum AdviceKind { Priority = 0, Favour = 1, Veto = 2, FocusWare = 3, AttackTarget = 4, HoldTarget = 5, ScoutTarget = 6 }

    /// <summary>Classes of candidates a priority advice favours.</summary>
    public enum PriorityClass { Economy = 0, Expansion = 1, Military = 2, Defence = 3, Trade = 4 }

    /// <summary>
    /// Advice to one's own leader (8.4): always from speech replies or a map tap, never a button. Faction must be the
    /// commanding people (PlayerId). Fields that do not apply are -1. TargetKind: 0 settlement, 1 building (H2); Cell: a
    /// 20 m sight cell (H2).
    /// </summary>
    public sealed class Advise : Command
    {
        public int Faction;
        public AdviceKind Kind;
        public PriorityClass Priority;
        public int ProposalId = -1;
        public int Ware = -1;
        public int TargetKind = -1;
        public int TargetId = -1;
        public int Cell = -1;
    }

    /// <summary>Advice in force on a faction (Priority, FocusWare, HoldTarget) until UntilTick.</summary>
    public sealed class AdviceRecord
    {
        public AdviceKind Kind;
        public PriorityClass Priority;
        public int Ware = -1, TargetKind = -1, TargetId = -1, Cell = -1;
        public long UntilTick;
    }

    /// <summary>"What are you doing?" and "Why?" (8.4): the top action, its reason, what blocks the people, the top three ids.</summary>
    public struct Explanation
    {
        public string Doing;
        public string Why;
        /// <summary>The blocking reason, or null ("Waiting for iron: the smelter has no coal.").</summary>
        public string Blocked;
        public int[] TopProposalIds;
    }

    // Events (PRD_AGES A.5).

    public sealed class AdviceTaken : SimEvent { public int Faction; public AdviceKind Kind; }

    /// <summary>The leader would not follow the advice; Sentence says why (e.g. an attack that fails the threshold, H2).</summary>
    public sealed class AdviceDeclined : SimEvent { public int Faction; public AdviceKind Kind; public string Sentence; }

    /// <summary>An autonomous leader announced its next action: it executes at ExecuteTick unless the player answers (H1-1).</summary>
    public sealed class ActionAnnounced : SimEvent { public int Faction; public int ProposalId; public long ExecuteTick; }

    public static partial class FactionApi
    {
        /// <summary>
        /// The leader's answer to "What are you doing?" and "Why?" (8.4), in plain English. Doing: the announced action and
        /// when it happens (autonomous), or the pitch waiting for a cast (before the handover). Why: its reason, plus the
        /// advice in force. Blocked: what holds the people up (open-site limit, a site waiting for goods), or null.
        /// TopProposalIds: the open proposals by rank.
        /// </summary>
        public static Explanation Explain(WorldState s, int faction)
        {
            var x = new Explanation { TopProposalIds = new int[0] };
            var fs = s?.Factions;
            if (fs == null || faction < 0 || faction >= fs.Factions.Count) return x;
            var fa = fs.Factions[faction];
            var list = Proposals(s, faction);
            var ids = new int[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = list[i].Id;
            x.TopProposalIds = ids;

            if (fs.GameOver)
            {
                x.Doing = "Nothing: the game is over.";
                return x;
            }
            var announced = fa.AnnouncedProposalId >= 0 ? FactionLogic.Find(s, fa.AnnouncedProposalId) : null;
            var top = Top(s, faction);
            if (announced != null)
            {
                long left = Math.Max(0, fa.AnnounceExecuteTick - s.Tick);
                long secs = (left + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond;
                x.Doing = "I am about to " + AutonomyLogic.ActionPhrase(s, announced)
                    + (secs > 0 ? " in " + secs + " s, unless you say otherwise." : " now.");
                x.Why = announced.Reason;
            }
            else if (top != null)
            {
                bool led = AutonomyLogic.IsLed(fa);
                x.Doing = led
                    ? "Next I will " + AutonomyLogic.ActionPhrase(s, top) + ", unless you cast it first."
                    : "I propose to " + AutonomyLogic.ActionPhrase(s, top) + ": cast it when you are ready.";
                x.Why = top.Reason;
            }
            else
            {
                x.Doing = "Nothing new for now: I am waiting for the sites we have to finish.";
            }
            string advice = AutonomyLogic.AdviceSentence(s, faction);
            if (advice != null) x.Why = x.Why == null ? advice : x.Why + " " + advice;
            x.Blocked = AutonomyLogic.BlockedSentence(s, faction);
            return x;
        }

        /// <summary>Advice in force on a faction now (expired records excluded), oldest first.</summary>
        public static IReadOnlyList<AdviceRecord> AdviceInForce(WorldState s, int faction)
        {
            var r = new List<AdviceRecord>();
            var fs = s?.Factions?.Factions;
            if (fs == null || faction < 0 || faction >= fs.Count) return r;
            foreach (var a in fs[faction].Advice) if (a.UntilTick > s.Tick) r.Add(a);
            return r;
        }

        /// <summary>The proposal the autonomous leader has announced (it executes at AnnounceExecuteTick), or null. H1-1.</summary>
        public static Proposal Announced(WorldState s, int faction)
        {
            var fs = s?.Factions?.Factions;
            if (fs == null || faction < 0 || faction >= fs.Count || fs[faction].AnnouncedProposalId < 0) return null;
            return FactionLogic.Find(s, fs[faction].AnnouncedProposalId);
        }

        /// <summary>True once the faction's leader builds on its own (after the handover, PRD_AGES 4.2). H1-1.</summary>
        public static bool IsAutonomous(WorldState s, int faction)
        {
            var fs = s?.Factions?.Factions;
            return fs != null && faction >= 0 && faction < fs.Count && fs[faction].Auto;
        }
    }

    /// <summary>
    /// The handover (PRD_AGES 4.2) and the autonomous leader's announce window (8.4). H1-1.
    /// Handover: the first worked ware (ids 5-9) delivered to a construction of a faction (EconomyHooks.OnConstructionDelivery)
    /// sets Faction.HandoverTick and raises PrinceHandover (once per faction). For the player's people it also makes the
    /// prince autonomous (Auto) and marks ch.historic ready (ChapterReady). The rival already casts on its own; for it the
    /// handover is a record only.
    /// Announce: a led faction (the player's people after the handover) announces its top proposal (ActionAnnounced) and
    /// executes it AnnounceTicks later unless the player answers first: a cast or a Favour does it at once, a Veto or a
    /// decline drops it. It announces again no sooner than AutoPaceTicks after its own last action, so a player who
    /// also casts gets more done than the prince alone. The rival keeps its own paced auto-cast (REFACTOR_SPEC 6.4).
    /// </summary>
    internal static class AutonomyLogic
    {
        /// <summary>Worked wares (4.2): Planks, Bricks, Bread, Meat, Cut stone.</summary>
        public static bool IsWorked(Ware w) => (int)w >= (int)Ware.Planks && (int)w <= (int)Ware.CutStone;

        /// <summary>EconomyHooks.OnConstructionDelivery listener.</summary>
        public static void OnConstructionDelivery(WorldState s, EventBuffer ev, int faction, SiteKind kind, int id, Ware ware, int count)
        {
            if (count <= 0 || !IsWorked(ware)) return;
            Handover(s, faction, ev);
        }

        /// <summary>Applies the handover milestone to a faction once. Returns false if it already happened (or no such faction).</summary>
        public static bool Handover(WorldState s, int faction, EventBuffer ev)
        {
            var fs = s?.Factions;
            if (fs == null || faction < 0 || faction >= fs.Factions.Count) return false;
            var fa = fs.Factions[faction];
            if (fa.HandoverTick >= 0) return false;
            fa.HandoverTick = s.Tick;
            ev?.Add(new PrinceHandover { Faction = faction });
            if (faction == PlayerIds.Human)
            {
                fa.Auto = true;
                fa.AnnounceTicks = fs.Tuning.AnnounceTicks;
                fa.AnnouncedProposalId = -1;
                fa.AnnounceExecuteTick = -1;
                ErasLogic.MarkReady(s, ChapterIds.Historic, ev);
            }
            return true;
        }

        /// <summary>A leader that announces and executes (the player's people once autonomous).</summary>
        public static bool IsLed(Faction fa) => fa != null && fa.Auto && fa.Id == PlayerIds.Human;

        /// <summary>One brain tick of a led faction: execute a due announcement, then announce the next action.</summary>
        public static void Tick(WorldState s, Faction fa, EventBuffer ev)
        {
            var f = s.Factions;
            if (fa.AnnouncedProposalId >= 0)
            {
                var p = FactionLogic.Find(s, fa.AnnouncedProposalId);
                if (p == null)
                {
                    // Answered by the player (cast, favour, veto, decline) or dropped by the brain: announce afresh.
                    fa.AnnouncedProposalId = -1;
                    fa.AnnounceExecuteTick = -1;
                }
                else if (s.Tick >= fa.AnnounceExecuteTick)
                {
                    fa.AnnouncedProposalId = -1;
                    fa.AnnounceExecuteTick = -1;
                    if (FactionLogic.CheckAction(s, p).Ok)
                    {
                        FactionLogic.Cast(s, p, ev);
                        fa.AutoCasts++;
                        fa.LastAutoCastTick = s.Tick;
                    }
                    else FactionLogic.Remove(s, p, "expired", ev);
                }
            }
            if (fa.AnnouncedProposalId >= 0 || f.GameOver) return;
            if (fa.LastAutoCastTick >= 0 && s.Tick - fa.LastAutoCastTick < f.Tuning.AutoPaceTicks) return;
            var top = FactionLogic.Top(f, fa.Id);
            if (top == null || !FactionLogic.CheckAction(s, top).Ok) return;
            fa.AnnouncedProposalId = top.Id;
            fa.AnnounceExecuteTick = s.Tick + Math.Max(0, fa.AnnounceTicks);
            ev?.Add(new ActionAnnounced { Faction = fa.Id, ProposalId = top.Id, ExecuteTick = fa.AnnounceExecuteTick });
        }

        // ---- advice in the brain's scores (8.4) ----

        /// <summary>True when a candidate belongs to a priority class (H1 meaning of the five classes).</summary>
        public static bool InClass(PriorityClass c, ProposalKind kind, int buildingType)
        {
            BuildingClass? cls = null;
            if (kind == ProposalKind.Building && BuildingDefs.IsValid((BuildingType)buildingType)) cls = BuildingDefs.Get((BuildingType)buildingType).Class;
            switch (c)
            {
                case PriorityClass.Economy:
                    return kind == ProposalKind.Upgrade || (cls != null && cls != BuildingClass.Lodge);
                case PriorityClass.Expansion:
                    return kind == ProposalKind.Road || kind == ProposalKind.Settlement || kind == ProposalKind.City || kind == ProposalKind.Town;
                case PriorityClass.Military:
                    return cls == BuildingClass.Lodge;
                case PriorityClass.Defence:
                    return cls == BuildingClass.Lodge || kind == ProposalKind.Banish || kind == ProposalKind.City || kind == ProposalKind.Town;
                default:
                    return false; // Trade: nothing to trade with before H2 (the Trading post).
            }
        }

        /// <summary>
        /// True when a candidate serves a ware: a building that makes it (collector of its terrain, workshop with it as
        /// output) or makes one of the inputs of its workshop, its upgrade, or a camp on a corner that touches its terrain.
        /// </summary>
        public static bool ServesWare(WorldState s, int ware, ProposalKind kind, int buildingType, int corner)
        {
            if (ware < 0 || ware >= Wares.Count) return false;
            if ((kind == ProposalKind.Building || kind == ProposalKind.Upgrade) && BuildingDefs.IsValid((BuildingType)buildingType))
                return Makes((BuildingType)buildingType, ware) || MakesInputOf((BuildingType)buildingType, ware);
            if (kind == ProposalKind.Settlement && corner >= 0 && Wares.IsRaw((Ware)ware))
            {
                var terr = TerrainInfo.TerrainOf((Ware)ware);
                foreach (int h in s.Board.Topology.VertexHexes(corner)) if (s.Board.HexTerrain[h] == terr) return true;
            }
            return false;
        }

        private static bool Makes(BuildingType type, int ware)
        {
            var def = BuildingDefs.Get(type);
            if (def.IsCollector) return Wares.IsRaw((Ware)ware) && TerrainInfo.TerrainOf((Ware)ware) == def.Terrain;
            return def.Outputs != null && ware < def.Outputs.Length && def.Outputs[ware] > 0;
        }

        /// <summary>The type makes an input of a workshop whose output is the ware.</summary>
        private static bool MakesInputOf(BuildingType type, int ware)
        {
            for (int i = 0; i < BuildingDefs.Count; i++)
            {
                var maker = BuildingDefs.Get((BuildingType)i);
                if (maker.IsCollector || maker.Outputs == null || ware >= maker.Outputs.Length || maker.Outputs[ware] <= 0) continue;
                for (int w = 0; w < maker.Inputs.Length && w < Wares.Count; w++)
                    if (maker.Inputs[w] > 0 && Makes(type, w)) return true;
            }
            return false;
        }

        /// <summary>The bonus advice in force gives a candidate (Priority +AdvicePriorityBonus, FocusWare +AdviceFocusBonus).</summary>
        public static int AdviceBonus(WorldState s, int faction, ProposalKind kind, int buildingType, int corner)
        {
            var fs = s.Factions;
            if (faction < 0 || faction >= fs.Factions.Count) return 0;
            int bonus = 0;
            foreach (var a in fs.Factions[faction].Advice)
            {
                if (a.UntilTick <= s.Tick) continue;
                if (a.Kind == AdviceKind.Priority && InClass(a.Priority, kind, buildingType)) bonus += fs.Tuning.AdvicePriorityBonus;
                if (a.Kind == AdviceKind.FocusWare && ServesWare(s, a.Ware, kind, buildingType, corner)) bonus += fs.Tuning.AdviceFocusBonus;
            }
            return bonus;
        }

        // ---- sentences ----

        /// <summary>"build a Sawmill at the home camp", "lay a road to the forest corner", "found a camp by the hills and pasture"...</summary>
        public static string ActionPhrase(WorldState s, Proposal p)
        {
            string title = FactionApi.ProposalTitle(s, p.Id);
            switch (p.Kind)
            {
                case ProposalKind.Building:
                    return "build " + Article(title) + " at " + EconomyApi.SettlementName(s, p.Settlement);
                case ProposalKind.Road: return "lay a road to " + Tail(title, "Road to ");
                case ProposalKind.Settlement: return "found a camp by " + Tail(title, "Camp by ");
                case ProposalKind.City:
                case ProposalKind.Town:
                case ProposalKind.Banish:
                case ProposalKind.Upgrade:
                    return title.Length == 0 ? "act" : char.ToLowerInvariant(title[0]) + title.Substring(1);
            }
            return "act";
        }

        private static string Tail(string title, string prefix) =>
            title.StartsWith(prefix, StringComparison.Ordinal) ? title.Substring(prefix.Length) : title;

        private static string Article(string name)
        {
            if (string.IsNullOrEmpty(name)) return "something";
            return ("AEIOU".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? "an " : "a ") + name;
        }

        /// <summary>One sentence for the advice in force ("You asked me to put the economy first."), or null.</summary>
        public static string AdviceSentence(WorldState s, int faction)
        {
            var parts = new List<string>();
            foreach (var a in FactionApi.AdviceInForce(s, faction))
            {
                if (a.Kind == AdviceKind.Priority) parts.Add("put " + PriorityName(a.Priority) + " first");
                else if (a.Kind == AdviceKind.FocusWare && a.Ware >= 0 && a.Ware < Wares.Count) parts.Add("focus on " + Wares.DisplayName((Ware)a.Ware));
            }
            if (parts.Count == 0) return null;
            return "You asked me to " + string.Join(" and to ", parts) + ".";
        }

        public static string PriorityName(PriorityClass c)
        {
            switch (c)
            {
                case PriorityClass.Economy: return "the economy";
                case PriorityClass.Expansion: return "new camps and roads";
                case PriorityClass.Military: return "warriors";
                case PriorityClass.Defence: return "our defence";
                default: return "trade";
            }
        }

        /// <summary>What holds the people up: the open-site limit, or the oldest site waiting for goods (its sentence); null when nothing.</summary>
        public static string BlockedSentence(WorldState s, int faction)
        {
            if (s.Economy == null) return null;
            int open = Brain.OpenSites(s, faction);
            string waiting = null;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Faction != faction || b.State != BuildingState.Site) continue;
                waiting = EconomyApi.WaitingSentence(s, SiteKind.Building, b.Id);
                if (waiting != null) break;
            }
            if (waiting == null)
                foreach (var r in s.Economy.Roads)
                {
                    if (r.Faction != faction || (r.State != RoadState.Site && r.State != RoadState.Constructing)) continue;
                    waiting = EconomyApi.WaitingSentence(s, SiteKind.Road, r.Id);
                    if (waiting != null) break;
                }
            if (waiting == null)
                foreach (var st in s.Economy.Settlements)
                {
                    if (st.Faction != faction || st.State != SettlementState.Site) continue;
                    waiting = EconomyApi.WaitingSentence(s, SiteKind.Settlement, st.Id);
                    if (waiting != null) break;
                }
            if (open >= s.Factions.Tuning.MaxOpenSites)
                return "We have " + open + " sites open; I start nothing new until one is finished." + (waiting != null ? " " + waiting : "");
            return waiting;
        }
    }

    /// <summary>
    /// Advice (8.4). Favour casts at once and Veto declines for AdviceVetoTicks (as CastSpell and DeclineProposal);
    /// Priority and FocusWare add their bonus to the brain's scores while in force (H1-1); Priority Trade is declined
    /// until there is something to trade with (H2); AttackTarget, HoldTarget and ScoutTarget are refused advice_not_yet
    /// until H2.
    /// </summary>
    internal static class AdviceLogic
    {
        public const string NothingToTrade = "We have nothing to trade with yet.";

        public static CommandResult Check(WorldState s, Advise c)
        {
            var f = s?.Factions;
            if (f == null) return FactionRefusals.Reject(FactionRefusals.NotOwner);
            if (f.GameOver) return FactionRefusals.Reject(FactionRefusals.GameOver);
            if (c.Faction != c.PlayerId || c.Faction < 0 || c.Faction >= f.Factions.Count) return FactionRefusals.Reject(FactionRefusals.NotOwner);
            switch (c.Kind)
            {
                case AdviceKind.Priority:
                    if (!Enum.IsDefined(typeof(PriorityClass), c.Priority)) return FactionRefusals.Reject(FactionRefusals.UnknownAdvice);
                    return CommandResult.Accept();
                case AdviceKind.Favour: return FactionLogic.CheckCast(s, c.Faction, c.ProposalId);
                case AdviceKind.Veto: return FactionLogic.CheckDecline(s, c.Faction, c.ProposalId);
                case AdviceKind.FocusWare:
                    if (c.Ware < 0 || c.Ware >= Wares.Count) return FactionRefusals.Reject(FactionRefusals.UnknownWare);
                    return CommandResult.Accept();
                case AdviceKind.AttackTarget:
                case AdviceKind.HoldTarget:
                case AdviceKind.ScoutTarget:
                    return FactionRefusals.Reject(FactionRefusals.AdviceNotYet); // H2 (military, fog, scouts).
                default: return FactionRefusals.Reject(FactionRefusals.UnknownAdvice);
            }
        }

        public static void Apply(WorldState s, Advise c, EventBuffer ev)
        {
            var f = s.Factions;
            var fa = f.Factions[c.Faction];
            fa.Advice.RemoveAll(a => a.UntilTick <= s.Tick);
            switch (c.Kind)
            {
                case AdviceKind.Favour:
                    FactionLogic.Cast(s, FactionLogic.Find(s, c.ProposalId), ev);
                    break;
                case AdviceKind.Veto:
                {
                    var p = FactionLogic.Find(s, c.ProposalId);
                    f.Declined.Add(new DeclinedProposal { Faction = p.Faction, Key = p.Key, UntilTick = s.Tick + f.Tuning.AdviceVetoTicks });
                    FactionLogic.Remove(s, p, "declined", ev);
                    break;
                }
                case AdviceKind.Priority:
                    if (c.Priority == PriorityClass.Trade)
                    {
                        ev?.Add(new AdviceDeclined { Faction = c.Faction, Kind = c.Kind, Sentence = NothingToTrade });
                        return;
                    }
                    fa.Advice.RemoveAll(a => a.Kind == AdviceKind.Priority);
                    fa.Advice.Add(new AdviceRecord { Kind = c.Kind, Priority = c.Priority, UntilTick = s.Tick + f.Tuning.AdvicePriorityTicks });
                    break;
                case AdviceKind.FocusWare:
                    fa.Advice.RemoveAll(a => a.Kind == AdviceKind.FocusWare);
                    fa.Advice.Add(new AdviceRecord { Kind = c.Kind, Ware = c.Ware, UntilTick = s.Tick + f.Tuning.AdviceFocusTicks });
                    break;
            }
            ev?.Add(new AdviceTaken { Faction = c.Faction, Kind = c.Kind });
        }
    }

    internal sealed class AdviseHandler : CommandHandler<Advise>
    {
        protected override CommandResult Validate(WorldState s, Advise c) => AdviceLogic.Check(s, c);

        protected override CommandResult Apply(WorldState s, Advise c, EventBuffer ev)
        {
            AdviceLogic.Apply(s, c, ev);
            return CommandResult.Accept();
        }
    }
}
