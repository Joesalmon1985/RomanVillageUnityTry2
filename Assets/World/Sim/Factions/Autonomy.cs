using System;
using System.Collections.Generic;
using World.Sim.Economy;

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
        /// The leader's answer to "What are you doing?" (8.4). H1-0 FIRST VERSION: Doing = the top proposal's title, Why =
        /// its reason, Blocked = the waiting sentence of the people's oldest unfinished building site (or null), the open
        /// proposal ids by rank. H1-1 refines the sentences (announce window, advice in force).
        /// </summary>
        public static Explanation Explain(WorldState s, int faction)
        {
            var x = new Explanation { TopProposalIds = new int[0] };
            if (s?.Factions == null) return x;
            var list = Proposals(s, faction);
            var ids = new int[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = list[i].Id;
            x.TopProposalIds = ids;
            var top = Top(s, faction);
            if (top != null)
            {
                x.Doing = ProposalTitle(s, top.Id);
                x.Why = top.Reason;
            }
            if (s.Economy != null)
            {
                foreach (var b in s.Economy.Buildings)
                {
                    if (b.Faction != faction || (b.State != BuildingState.Site && b.State != BuildingState.Constructing)) continue;
                    x.Blocked = EconomyApi.WaitingSentence(s, SiteKind.Building, b.Id);
                    if (x.Blocked != null) break;
                }
            }
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
    }

    /// <summary>
    /// Advice (8.4). H1-0 FIRST VERSION: validation is real; Favour casts at once and Veto declines (as CastSpell and
    /// DeclineProposal); Priority and FocusWare are recorded but the brain does not read them yet (H1-1 adds the +40 / +30
    /// scores); AttackTarget, HoldTarget and ScoutTarget are refused advice_not_yet until H2.
    /// </summary>
    internal static class AdviceLogic
    {
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
                    return FactionRefusals.Reject(FactionRefusals.AdviceNotYet); // H1-0 STUB: H2 (military, fog, scouts).
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
