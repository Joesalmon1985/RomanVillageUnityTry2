using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// The Ash expedition (5.1, 5.3). One counter, RivalProgress: +1 per world tick while the campaign runs, plus the
    /// charges of the 5.1 table. The expedition spends it on its authored plan, step by step: march (MarchCost), then at
    /// the site seize the crystal if it is still AtSite (RivalTransit) and carry it (CarryCost) to the step's altar
    /// (AtRivalAltar), or, if the crystal is gone, search (SearchCost); then the next step. Steps whose crystal is already
    /// on an altar or restored are skipped. A pause (an Ash camp fell) uses up progress with no effect. When the plan is
    /// done and three crystals are restored, Hesk Varr goes to the last tower. Every move is a report that reaches the
    /// party ReportDelay later. The party can never lose a carried or restored crystal to it: recovery is only at an altar.
    /// </summary>
    internal static partial class CampaignRuntime
    {
        /// <summary>A charge on the expedition clock (5.1), with its event.</summary>
        internal static void Charge(WorldState s, CampaignState c, long amount, string why, EventBuffer ev)
        {
            if (amount <= 0) return;
            c.RivalProgress += amount;
            ev?.Add(new RivalAdvanced { Amount = amount, Why = why });
        }

        /// <summary>An Ash camp fell to the player's people: the expedition stops for AshCampLostPause of progress.</summary>
        internal static void PauseRival(WorldState s, CampaignState c, EventBuffer ev)
        {
            var r = c.Rival;
            long until = Math.Max(r.PausedUntilProgress, c.RivalSpent + Math.Max(0, c.Definition.Tuning.AshCampLostPause));
            if (until <= c.RivalSpent) return;
            r.PausedUntilProgress = until;
            if (r.Phase != RivalPhase.Paused) { c.RivalResumePhase = r.Phase; r.Phase = RivalPhase.Paused; }
            ev?.Add(new RivalPaused { Until = until });
        }

        internal static RivalStepDef RivalStepFor(CampaignState c, Element e)
        {
            var cd = c.Definition.Crystal(e);
            return cd == null ? null : RunPlan(c).Find(x => x != null && x.Crystal == cd.Id);
        }

        private static RivalStepDef CurrentStep(CampaignState c)
        {
            var plan = RunPlan(c);
            return c.Rival.Step >= 0 && c.Rival.Step < plan.Count ? plan[c.Rival.Step] : null;
        }

        /// <summary>Starts plan step k (or the first later one whose crystal is not on an altar or restored).</summary>
        private static void StartRivalStep(WorldState s, CampaignState c, int k, EventBuffer ev)
        {
            var d = c.Definition;
            var plan = RunPlan(c);
            var r = c.Rival;
            CrystalDef cd = null;
            while (k < plan.Count)
            {
                cd = d.Crystal(plan[k].Crystal);
                var st = cd != null ? c.Crystal(cd.Element) : null;
                if (st != null && st.Custody != Custody.AtRivalAltar && st.Custody != Custody.Restored) break;
                k++;
            }
            r.Step = k;
            r.StepProgress = 0;
            var phase = k >= plan.Count ? RivalPhase.PlanDone : RivalPhase.Marching;
            if (r.Phase == RivalPhase.Paused) c.RivalResumePhase = phase; else r.Phase = phase;
            if (k >= plan.Count) { CheckLastTower(s, c, ev); return; }
            r.At = cd.Site;
            ev?.Add(new RivalStepStarted { Step = k, Crystal = cd.Element });
            Report(s, c, cd.Element, RivalReportKind.Marching, cd.Site);
        }

        /// <summary>With the plan done and three crystals restored, Hesk Varr goes to the last tower (5.3).</summary>
        internal static void CheckLastTower(WorldState s, CampaignState c, EventBuffer ev)
        {
            var r = c.Rival;
            var phase = r.Phase == RivalPhase.Paused ? c.RivalResumePhase : r.Phase;
            if (phase != RivalPhase.PlanDone || CountCustody(c, Custody.Restored) != 3) return;
            var last = RemainingCrystal(c);
            if (!last.HasValue) return;
            if (r.Phase == RivalPhase.Paused) c.RivalResumePhase = RivalPhase.AtLastTower; else r.Phase = RivalPhase.AtLastTower;
            r.At = TowerRef(c, last.Value);
            Report(s, c, last.Value, RivalReportKind.AtLastTower, r.At);
        }

        private static void Report(WorldState s, CampaignState c, Element e, RivalReportKind kind, MarkerRef at)
        {
            c.Reports.Add(new RivalReport { Crystal = e, Kind = kind, At = at, Tick = s.Tick, DueTick = s.Tick + Math.Max(0, c.Definition.Tuning.ReportDelay) });
        }

        /// <summary>The party stands at the crystal's pedestal: it is theirs (the party wins a tie, 5.2).</summary>
        private static bool PartyClaims(CampaignState c, CrystalDef cd)
        {
            var p = c.Party;
            return cd != null && p.Mode != PartyMode.Travelling && Same(p.At, cd.Site);
        }

        /// <summary>Spends the progress not yet used, through as many phases as it covers.</summary>
        private static void TickRival(WorldState s, CampaignState c, EventBuffer ev)
        {
            var r = c.Rival;
            var t = c.Definition.Tuning;
            long budget = c.RivalProgress - c.RivalSpent;
            int guard = 0;
            while (budget > 0 && guard++ < 64)
            {
                if (c.RivalSpent < r.PausedUntilProgress)
                {
                    if (r.Phase != RivalPhase.Paused) { c.RivalResumePhase = r.Phase; r.Phase = RivalPhase.Paused; }
                    long idle = Math.Min(budget, r.PausedUntilProgress - c.RivalSpent);
                    c.RivalSpent += idle;
                    budget -= idle;
                    continue;
                }
                if (r.Phase == RivalPhase.Paused) r.Phase = c.RivalResumePhase;
                var step = CurrentStep(c);
                long need;
                switch (r.Phase)
                {
                    case RivalPhase.Marching: need = Math.Max(0, (step?.MarchCost ?? 0) - r.StepProgress); break;
                    case RivalPhase.Carrying: need = Math.Max(0, (step?.CarryCost ?? 0) - r.StepProgress); break;
                    case RivalPhase.Searching: need = Math.Max(0, t.SearchCost - r.StepProgress); break;
                    default:
                        c.RivalSpent += budget;
                        budget = 0;
                        continue;
                }
                if (budget < need)
                {
                    r.StepProgress += budget;
                    c.RivalSpent += budget;
                    budget = 0;
                    break;
                }
                r.StepProgress += need;
                c.RivalSpent += need;
                budget -= need;
                FinishRivalPhase(s, c, step, ev);
            }
            if (r.Phase == RivalPhase.Paused && c.RivalSpent >= r.PausedUntilProgress) r.Phase = c.RivalResumePhase;
        }

        private static void FinishRivalPhase(WorldState s, CampaignState c, RivalStepDef step, EventBuffer ev)
        {
            var r = c.Rival;
            var cd = step != null ? c.Definition.Crystal(step.Crystal) : null;
            switch (r.Phase)
            {
                case RivalPhase.Marching:
                {
                    var st = cd != null ? c.Crystal(cd.Element) : null;
                    r.StepProgress = 0;
                    r.At = cd?.Site ?? default;
                    if (st != null && st.Custody == Custody.AtSite && !PartyClaims(c, cd))
                    {
                        SetCustody(s, cd.Element, Custody.RivalTransit, cd.Site, ev);
                        r.Phase = RivalPhase.Carrying;
                        Report(s, c, cd.Element, RivalReportKind.Seized, cd.Site);
                    }
                    else
                    {
                        r.Phase = RivalPhase.Searching;
                        if (cd != null) Report(s, c, cd.Element, RivalReportKind.Searching, cd.Site);
                    }
                    return;
                }
                case RivalPhase.Carrying:
                {
                    if (cd != null && c.Crystal(cd.Element)?.Custody == Custody.RivalTransit)
                    {
                        SetCustody(s, cd.Element, Custody.AtRivalAltar, step.Altar, ev);
                        r.At = step.Altar;
                        Report(s, c, cd.Element, RivalReportKind.OnAltar, step.Altar);
                    }
                    StartRivalStep(s, c, r.Step + 1, ev);
                    return;
                }
                case RivalPhase.Searching:
                    StartRivalStep(s, c, r.Step + 1, ev);
                    return;
            }
        }

        /// <summary>DevRivalNextStep: the expedition finishes its current step now (the progress it needs is charged as "dev").</summary>
        private static void RivalNextStepNow(WorldState s, CampaignState c, EventBuffer ev)
        {
            TickRival(s, c, ev);   // spend what is already there first
            var r = c.Rival;
            int start = r.Step;
            int guard = 0;
            while (r.Step == start && guard++ < 8)
            {
                var phase = r.Phase == RivalPhase.Paused ? c.RivalResumePhase : r.Phase;
                if (phase != RivalPhase.Marching && phase != RivalPhase.Carrying && phase != RivalPhase.Searching) return;
                var step = CurrentStep(c);
                var t = c.Definition.Tuning;
                long need = phase == RivalPhase.Marching ? (step?.MarchCost ?? 0) : phase == RivalPhase.Carrying ? (step?.CarryCost ?? 0) : t.SearchCost;
                need = Math.Max(0, need - r.StepProgress);
                if (c.RivalSpent < r.PausedUntilProgress) need += r.PausedUntilProgress - c.RivalSpent;
                Charge(s, c, Math.Max(1, need), RivalCharges.Dev, ev);
                TickRival(s, c, ev);
            }
        }

        /// <summary>Reports whose time has come reach the party: RivalReported, the belief, report clues, the journal.</summary>
        private static void DeliverReports(WorldState s, CampaignState c, EventBuffer ev)
        {
            var d = c.Definition;
            while (c.ReportsRaised < c.Reports.Count && c.Reports[c.ReportsRaised].DueTick <= s.Tick)
            {
                var r = c.Reports[c.ReportsRaised++];
                ev?.Add(new RivalReported { Crystal = r.Crystal, Kind = r.Kind, At = r.At });
                var b = c.BeliefOf(r.Crystal);
                // Reports arrive in order; one is news unless the party has seen for itself since it happened.
                bool fresh = b != null && (b.Source == SourceKind.Report || b.AsOfTick <= r.Tick)
                    && b.Custody != Custody.Carried && b.Custody != Custody.Restored;
                if (fresh && r.Kind == RivalReportKind.Seized) SetBelief(s, c, r.Crystal, Custody.RivalTransit, r.At, SourceKind.Report, ev);
                if (fresh && r.Kind == RivalReportKind.OnAltar) SetBelief(s, c, r.Crystal, Custody.AtRivalAltar, r.At, SourceKind.Report, ev);
                var cd = d.Crystal(r.Crystal);
                string name = cd?.Name ?? r.Crystal.ToString();
                string place = CampaignTokens.PlaceName(d, r.At);
                if (place.Length == 0) place = "the island";
                switch (r.Kind)
                {
                    case RivalReportKind.Marching: Journal(s, c, "Word came: Ash runners were seen heading for " + place + ".", ev); break;
                    case RivalReportKind.Seized: Journal(s, c, "Word came: smoke at " + place + ". The Ash Clan has taken " + name + ".", ev); break;
                    case RivalReportKind.OnAltar: Journal(s, c, "Word came: a light was carried into " + place + ". " + name + " is on an Ash altar.", ev); break;
                    case RivalReportKind.Searching: Journal(s, c, "Word came: Ash runners are searching " + place + ".", ev); break;
                    case RivalReportKind.AtLastTower: Journal(s, c, "Word came: Hesk Varr stands alone at " + place + ".", ev); break;
                }
                if (r.Kind == RivalReportKind.Seized || r.Kind == RivalReportKind.OnAltar)
                    foreach (var clue in d.Clues ?? new List<ClueDef>())
                        if (clue.Source?.Kind == SourceKind.Report && (string.IsNullOrEmpty(clue.Source.Ref) || clue.Source.Ref == cd?.Id))
                            GiveClue(c, clue, ev);
                c.ReplanNow = true;
            }
        }
    }
}
