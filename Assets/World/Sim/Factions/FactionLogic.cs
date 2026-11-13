using System.Collections.Generic;
using World.Sim.Demons;
using World.Sim.Economy;

namespace World.Sim.Factions
{
    /// <summary>Spells, declines, strength, the overrun warning and the endings (REFACTOR_SPEC 2.C).</summary>
    internal static class FactionLogic
    {
        public static Proposal Find(WorldState s, int proposalId)
        {
            if (s?.Factions == null) return null;
            foreach (var p in s.Factions.Proposals) if (p.Id == proposalId) return p;
            return null;
        }

        /// <summary>The underlying placement check of a proposal (what CastSpell re-checks).</summary>
        public static CommandResult CheckAction(WorldState s, Proposal p)
        {
            switch (p.Kind)
            {
                case ProposalKind.Building: return EconomyLogic.CheckBuilding(s, p.Faction, (BuildingType)p.BuildingType, p.Plot);
                case ProposalKind.Road: return EconomyLogic.CheckRoad(s, p.Faction, p.HexEdge);
                case ProposalKind.Settlement: return EconomyLogic.CheckSettlement(s, p.Faction, p.Corner);
                case ProposalKind.City: return EconomyLogic.CheckCity(s, p.Faction, p.Corner);
                case ProposalKind.Banish: return DemonLogic.CheckBanish(s, p.Faction, p.DemonSiteKind, p.DemonSiteId);
            }
            return FactionRefusals.Reject(FactionRefusals.UnknownProposal);
        }

        public static CommandResult CheckCast(WorldState s, int faction, int proposalId)
        {
            if (s?.Factions == null) return FactionRefusals.Reject(FactionRefusals.UnknownProposal);
            if (s.Factions.GameOver) return FactionRefusals.Reject(FactionRefusals.GameOver);
            var p = Find(s, proposalId);
            if (p == null || p.Faction != faction) return FactionRefusals.Reject(FactionRefusals.UnknownProposal);
            var r = CheckAction(s, p);
            return r.Ok ? r : FactionRefusals.Reject(FactionRefusals.NoLongerLegal, r.Message);
        }

        public static CommandResult CheckDecline(WorldState s, int faction, int proposalId)
        {
            if (s?.Factions == null) return FactionRefusals.Reject(FactionRefusals.UnknownProposal);
            if (s.Factions.GameOver) return FactionRefusals.Reject(FactionRefusals.GameOver);
            var p = Find(s, proposalId);
            if (p == null || p.Faction != faction) return FactionRefusals.Reject(FactionRefusals.UnknownProposal);
            return CommandResult.Accept();
        }

        /// <summary>Applies a validated proposal exactly as CastSpell does (the rival's brain calls it too).</summary>
        public static void Cast(WorldState s, Proposal p, EventBuffer ev)
        {
            var f = s.Factions;
            switch (p.Kind)
            {
                case ProposalKind.Building: EconomyLogic.PlaceBuildingSite(s, p.Faction, (BuildingType)p.BuildingType, p.Plot, ev); break;
                case ProposalKind.Road: EconomyLogic.PlaceRoadSite(s, p.Faction, p.HexEdge, ev); break;
                case ProposalKind.Settlement: EconomyLogic.PlaceSettlementSite(s, p.Faction, p.Corner, ev); break;
                case ProposalKind.City: EconomyLogic.PlaceCitySite(s, p.Faction, p.Corner, ev); break;
                case ProposalKind.Banish: DemonLogic.Banish(s, p.Faction, p.DemonSiteKind, p.DemonSiteId, ev); break;
            }
            if (p.Key == Brain.OpeningKey(s, p.Faction)) f.OpeningDone[p.Faction] = true;
            f.Casts[p.Faction]++;
            ev?.Add(new SpellCast
            {
                ProposalId = p.Id, Faction = p.Faction, Kind = p.Kind, BuildingType = p.BuildingType, Plot = p.Plot,
                HexEdge = p.HexEdge, Corner = p.Corner, DemonSiteKind = p.DemonSiteKind, DemonSiteId = p.DemonSiteId,
            });
            Remove(s, p, "cast", ev);
        }

        public static void Decline(WorldState s, Proposal p, EventBuffer ev)
        {
            var f = s.Factions;
            f.Declined.Add(new DeclinedProposal { Faction = p.Faction, Key = p.Key, UntilTick = s.Tick + f.Tuning.DeclineCooldownTicks });
            Remove(s, p, "declined", ev);
        }

        /// <summary>Removes an open proposal and re-ranks the faction's queue (a new top gets TopSinceTick = now).</summary>
        public static void Remove(WorldState s, Proposal p, string why, EventBuffer ev)
        {
            var f = s.Factions;
            var before = Top(f, p.Faction);
            f.Proposals.Remove(p);
            ev?.Add(new ProposalRemoved { ProposalId = p.Id, Faction = p.Faction, Why = why });
            Rerank(f, p.Faction);
            var after = Top(f, p.Faction);
            if (after != null && after != before)
            {
                after.TopSinceTick = s.Tick;
                ev?.Add(new ProposalsReordered { Faction = p.Faction, TopProposalId = after.Id });
            }
        }

        public static Proposal Top(FactionsState f, int faction)
        {
            foreach (var p in f.Proposals) if (p.Faction == faction && p.Rank == 0) return p;
            return null;
        }

        public static void Rerank(FactionsState f, int faction)
        {
            int r = 0;
            foreach (var p in f.Proposals) if (p.Faction == faction) p.Rank = r++;
        }

        // ---- strength, warning, endings ----

        public static void ComputeStrength(WorldState s, Faction fa)
        {
            var tu = s.Factions.Tuning;
            fa.Settlements = fa.Cities = fa.FedWarriors = fa.HungryWarriors = 0;
            if (s.Economy != null)
            {
                foreach (var st in s.Economy.Settlements)
                {
                    if (st.Faction != fa.Id || st.State != SettlementState.Active) continue;
                    if (st.IsCity) fa.Cities++;
                    else fa.Settlements++;
                }
                foreach (var w in s.Economy.Warriors)
                {
                    if (w.Faction != fa.Id) continue;
                    if (w.Fed) fa.FedWarriors++;
                    else fa.HungryWarriors++;
                }
            }
            fa.Strength = fa.Settlements * tu.StrengthPerSettlement + fa.Cities * tu.StrengthPerCity + fa.FedWarriors * tu.StrengthPerFedWarrior;
        }

        public static GameOutcome OutcomeFor(int winner) =>
            winner == PlayerIds.Human ? GameOutcome.Victory : winner == PlayerIds.Rival ? GameOutcome.Defeat : GameOutcome.Draw;
    }

    /// <summary>Strength (every tick), the overrun warning and the endings (REFACTOR_SPEC 2.C.3).</summary>
    internal sealed class StrengthSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var f = s.Factions;
            if (f == null || f.GameOver || f.Factions.Count < PlayerIds.Count) return;
            foreach (var fa in f.Factions)
            {
                int before = fa.Strength;
                FactionLogic.ComputeStrength(s, fa);
                if (fa.Strength != before) ev.Add(new StrengthChanged { Faction = fa.Id, Strength = fa.Strength });
            }
            int ps = f.Factions[PlayerIds.Human].Strength, rs = f.Factions[PlayerIds.Rival].Strength;
            var tu = f.Tuning;
            var w = f.Warning;

            if (w.Active)
            {
                int att = f.Factions[w.Attacker].Strength, def = f.Factions[w.Defender].Strength;
                if (att < def + tu.OverrunMargin)
                {
                    w.Active = false;
                    ev.Add(new OverrunWarningCancelled { Attacker = w.Attacker, Defender = w.Defender });
                }
                else if (s.Tick >= w.EndTick)
                {
                    w.Active = false;
                    ev.Add(new FactionOverrun { Attacker = w.Attacker, Defender = w.Defender });
                    End(s, FactionLogic.OutcomeFor(w.Attacker), OutcomeReasons.Overrun, ps, rs, ev);
                    return;
                }
            }
            if (!w.Active)
            {
                int attacker = ps >= rs + tu.OverrunMargin ? PlayerIds.Human : rs >= ps + tu.OverrunMargin ? PlayerIds.Rival : -1;
                if (attacker >= 0)
                {
                    w.Active = true;
                    w.Attacker = attacker;
                    w.Defender = 1 - attacker;
                    w.StartTick = s.Tick;
                    w.EndTick = s.Tick + tu.WarningTicks;
                    ev.Add(new OverrunWarningStarted { Attacker = w.Attacker, Defender = w.Defender, EndTick = w.EndTick });
                }
            }
            if (s.Tick >= tu.TimeLimitTicks)
            {
                if (w.Active)
                {
                    w.Active = false;
                    ev.Add(new OverrunWarningCancelled { Attacker = w.Attacker, Defender = w.Defender });
                }
                End(s, ps > rs ? GameOutcome.Victory : rs > ps ? GameOutcome.Defeat : GameOutcome.Draw, OutcomeReasons.Time, ps, rs, ev);
            }
        }

        private static void End(WorldState s, GameOutcome outcome, string reason, int ps, int rs, EventBuffer ev)
        {
            var f = s.Factions;
            f.GameOver = true;
            f.Outcome = outcome;
            f.OutcomeReason = reason;
            f.EndedTick = s.Tick;
            ev.Add(new GameEnded { Outcome = outcome, Reason = reason, PlayerStrength = ps, RivalStrength = rs });
        }
    }

    internal sealed class CastSpellHandler : CommandHandler<CastSpell>
    {
        protected override CommandResult Validate(WorldState s, CastSpell c) => FactionLogic.CheckCast(s, c.PlayerId, c.ProposalId);

        protected override CommandResult Apply(WorldState s, CastSpell c, EventBuffer ev)
        {
            FactionLogic.Cast(s, FactionLogic.Find(s, c.ProposalId), ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class DeclineProposalHandler : CommandHandler<DeclineProposal>
    {
        protected override CommandResult Validate(WorldState s, DeclineProposal c) => FactionLogic.CheckDecline(s, c.PlayerId, c.ProposalId);

        protected override CommandResult Apply(WorldState s, DeclineProposal c, EventBuffer ev)
        {
            FactionLogic.Decline(s, FactionLogic.Find(s, c.ProposalId), ev);
            return CommandResult.Accept();
        }
    }

    /// <summary>Entry point of the Factions module (REFACTOR_SPEC A.4).</summary>
    public static class FactionsModule
    {
        public static void InitState(WorldState s)
        {
            var f = new FactionsState();
            s.Factions = f;
            for (int id = 0; id < PlayerIds.Count; id++)
            {
                var fa = new Faction
                {
                    Id = id, Name = id == PlayerIds.Human ? "Your people" : "the Ash Clan", Auto = id != PlayerIds.Human,
                    NextPlanTick = id == PlayerIds.Human ? 0 : f.Tuning.PlanIntervalTicks / 2,
                };
                f.Factions.Add(fa);
                FactionLogic.ComputeStrength(s, fa);
            }
            // The player's first plan happens at tick 0 so the opening proposal exists from the start.
            Brain.Plan(s, PlayerIds.Human, null);
        }

        public static void Register(WorldSim sim)
        {
            sim.AddSystem(SystemOrder.Brain, new BrainSystem());
            sim.AddSystem(SystemOrder.Strength, new StrengthSystem());
            sim.RegisterHandler(new CastSpellHandler());
            sim.RegisterHandler(new DeclineProposalHandler());
        }
    }
}
