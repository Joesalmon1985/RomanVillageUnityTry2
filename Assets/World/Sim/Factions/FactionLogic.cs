using System;
using System.Collections.Generic;
using World.Sim.Demons;
using World.Sim.Economy;

namespace World.Sim.Factions
{
    /// <summary>Spells, declines, strength, the overrun warning and the endings (REFACTOR_SPEC 2.C).</summary>
    internal static class FactionLogic
    {
        /// <summary>The Ash Clan's cast delay now (<see cref="RivalDelay(WorldState, int)"/> for <see cref="PlayerIds.Rival"/>).</summary>
        public static int RivalDelay(WorldState s) => RivalDelay(s, PlayerIds.Rival);

        /// <summary>
        /// The cast delay of an AI people now: flat, or ramping from RivalCastDelayTicks to RivalCastDelayEndTicks; bolder
        /// by its lead over the strongest other people.
        /// </summary>
        public static int RivalDelay(WorldState s, int faction)
        {
            var tu = s.Factions.Tuning;
            int d = tu.RivalCastDelayTicks;
            if (tu.RivalCastDelayEndTicks >= 0 && tu.RivalRampTicks > 0)
            {
                long t = Math.Min(s.Tick, tu.RivalRampTicks);
                d = (int)(tu.RivalCastDelayTicks + (tu.RivalCastDelayEndTicks - tu.RivalCastDelayTicks) * t / tu.RivalRampTicks);
            }
            // Bolder when ahead: each point of strength the rival leads by takes RivalBoldTicksPerLead off its delay.
            var f = s.Factions;
            int strongestOther = StrongestOther(f, faction);
            if (tu.RivalBoldTicksPerLead > 0 && faction >= 0 && faction < f.Factions.Count && strongestOther >= 0)
            {
                int lead = f.Factions[faction].Strength - f.Factions[strongestOther].Strength;
                int over = lead - tu.RivalBoldFromLead + 1;
                if (over > 0) d = Math.Max(tu.RivalMinDelayTicks, d - tu.RivalBoldTicksPerLead * over);
            }
            return d;
        }

        /// <summary>The strongest people other than <paramref name="faction"/> (lowest id on a tie), or -1 when there is none.</summary>
        public static int StrongestOther(FactionsState f, int faction)
        {
            int best = -1;
            if (f == null) return best;
            for (int i = 0; i < f.Factions.Count; i++)
            {
                if (i == faction) continue;
                if (best < 0 || f.Factions[i].Strength > f.Factions[best].Strength) best = i;
            }
            return best;
        }

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
                case ProposalKind.Upgrade:
                {
                    var b = Brain.BuildingOnPlot(s, p.Faction, p.Plot);
                    return b == null ? EconomyRefusals.Reject(EconomyRefusals.UnknownBuilding) : Brain.CheckUpgrade(s, p.Faction, b.Id);
                }
                case ProposalKind.Town: return Brain.CheckTown(s, p.Faction, p.Corner);
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
                case ProposalKind.Upgrade: Brain.StartUpgrade(s, p.Faction, Brain.BuildingOnPlot(s, p.Faction, p.Plot).Id, ev); break;
                case ProposalKind.Town: Brain.StartTown(s, p.Faction, p.Corner, ev); break;
            }
            if (p.Key == Brain.OpeningKey(s, p.Faction)) f.OpeningDone[p.Faction] = true;
            f.Casts[p.Faction]++;
            var fa = f.Factions.Count > p.Faction ? f.Factions[p.Faction] : null;
            if (fa != null) fa.LastCastTick = s.Tick;
            if (fa != null && FactionApi.ProposalTarget(s, p.Id, out double cx, out double cz, out _))
            {
                fa.HasLastCast = true;
                fa.LastCastX = (int)Math.Round(cx);
                fa.LastCastZ = (int)Math.Round(cz);
            }
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
            fa.Settlements = fa.Cities = fa.Towns = fa.FedWarriors = fa.HungryWarriors = fa.HeldCamps = 0;
            if (s.Economy != null)
            {
                foreach (var st in s.Economy.Settlements)
                {
                    if (st.Faction != fa.Id || st.State != SettlementState.Active) continue;
                    if (!tu.DemonHeldCampsCount && EconomyLogic.CornerDemons(s, st.Corner) > 0) { fa.HeldCamps++; continue; }
                    if (st.IsTown) fa.Towns++;
                    else if (st.IsCity) fa.Cities++;
                    else fa.Settlements++;
                }
                foreach (var w in s.Economy.Warriors)
                {
                    if (w.Faction != fa.Id) continue;
                    if (w.Fed) fa.FedWarriors++;
                    else fa.HungryWarriors++;
                }
            }
            fa.Strength = fa.Settlements * tu.StrengthPerSettlement + fa.Cities * tu.StrengthPerCity + fa.Towns * tu.StrengthPerTown
                + fa.FedWarriors * tu.StrengthPerFedWarrior;
        }

        public static GameOutcome OutcomeFor(int winner) =>
            winner == PlayerIds.Human ? GameOutcome.Victory : winner >= 0 ? GameOutcome.Defeat : GameOutcome.Draw;
    }

    /// <summary>Strength (every tick), the overrun warning and the endings (REFACTOR_SPEC 2.C.3).</summary>
    internal sealed class StrengthSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var f = s.Factions;
            // A contest needs at least two peoples.
            if (f == null || f.GameOver || f.Factions.Count < 2 || f.Factions.Count < s.PeopleCount) return;
            // Overrun ends only the 20-minute Prehistoric skirmish; everywhere else the setback rule of PRD_AGES 4.3 applies
            // (campaign, a game that took the Historic step, dev starts; H1-1).
            bool fatal = TimeLimited(s);
            if (f.Halts.Count > 0) f.Halts.RemoveAll(h => h.UntilTick <= s.Tick);
            foreach (var fa in f.Factions)
            {
                int before = fa.Strength;
                FactionLogic.ComputeStrength(s, fa);
                if (fa.Strength != before)
                {
                    fa.StrengthChangedTick = s.Tick;
                    ev.Add(new StrengthChanged { Faction = fa.Id, Strength = fa.Strength });
                }
            }
            // ps: the player's strength; rs: the strongest other people's (the Ash Clan's in a two-people game).
            int ps = f.Factions[PlayerIds.Human].Strength, rs = f.Factions[FactionLogic.StrongestOther(f, PlayerIds.Human)].Strength;
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
                    if (fatal)
                    {
                        End(s, FactionLogic.OutcomeFor(w.Attacker), OutcomeReasons.Overrun, ps, rs, ev);
                        return;
                    }
                    CampaignRules.Overrun(s, w.Attacker, w.Defender, ev);
                }
            }
            if (!w.Active)
            {
                int attacker = -1, defender = -1;
                if (s.Tick >= tu.WarningGraceTicks) PickOverrun(s, f, tu.OverrunMargin, out attacker, out defender);
                if (attacker >= 0)
                {
                    w.Active = true;
                    w.Attacker = attacker;
                    w.Defender = defender;
                    w.StartTick = s.Tick;
                    w.EndTick = s.Tick + tu.WarningTicks;
                    ev.Add(new OverrunWarningStarted { Attacker = w.Attacker, Defender = w.Defender, EndTick = w.EndTick });
                }
            }
            // The time limit belongs to the 20-minute Prehistoric skirmish only (PRD_AGES 4.3; H1-1): a game that stepped
            // to the Historic age, a dev start and the campaign play on.
            if (TimeLimited(s) && s.Tick >= tu.TimeLimitTicks)
            {
                if (w.Active)
                {
                    w.Active = false;
                    ev.Add(new OverrunWarningCancelled { Attacker = w.Attacker, Defender = w.Defender });
                }
                End(s, ps > rs ? GameOutcome.Victory : rs > ps ? GameOutcome.Defeat : GameOutcome.Draw, OutcomeReasons.Time, ps, rs, ev);
            }
        }

        /// <summary>
        /// Who may start an overrun warning: the strongest people (lowest id on a tie) against the weakest other people
        /// (lowest id on a tie) when it leads by the margin. Setback grace (4.3): no new warning against a people just
        /// overrun (-1, never set, in the Prehistoric skirmish). With two peoples this is exactly the old player-or-rival rule.
        /// </summary>
        private static void PickOverrun(WorldState s, FactionsState f, int margin, out int attacker, out int defender)
        {
            attacker = -1;
            defender = -1;
            int a = -1;
            for (int i = 0; i < f.Factions.Count; i++)
                if (a < 0 || f.Factions[i].Strength > f.Factions[a].Strength) a = i;
            if (a < 0) return;
            int d = -1;
            for (int i = 0; i < f.Factions.Count; i++)
            {
                if (i == a) continue;
                if (d < 0 || f.Factions[i].Strength < f.Factions[d].Strength) d = i;
            }
            if (d < 0 || f.Factions[a].Strength < f.Factions[d].Strength + margin) return;
            if (f.Factions[d].SetbackUntilTick > s.Tick) return;
            attacker = a;
            defender = d;
        }

        /// <summary>The skirmish time limit applies: Prehistoric era in skirmish mode.</summary>
        public static bool TimeLimited(WorldState s) =>
            Eras.AgesApi.Mode(s) == Eras.PlayMode.Skirmish && Eras.AgesApi.Current(s) == Eras.Era.Prehistoric;

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
            var f = new FactionsState { Tuning = FactionTunings.Tuned() };
            s.Factions = f;
            int n = s.PeopleCount;
            f.Casts = new int[n];
            f.OpeningDone = new bool[n];
            for (int id = 0; id < n; id++)
            {
                var fa = new Faction
                {
                    Id = id, Name = PeopleDefs.Name(s, id), Auto = id != PlayerIds.Human,
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
            // Later ages (PRD_AGES A.5, 4.3; H1-0).
            sim.RegisterHandler(new AdviseHandler());
            EconomyApi.Hooks(sim).IsSettlementHalted.Add(CampaignRules.IsHalted);
            // The handover (PRD_AGES 4.2; H1-1): the first worked ware delivered to a construction of a people.
            EconomyApi.Hooks(sim).OnConstructionDelivery.Add(AutonomyLogic.OnConstructionDelivery);
        }
    }
}
