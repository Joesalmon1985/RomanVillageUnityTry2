using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    // The race tuning pass (sim-H1-cry-S1-tuned; PRD_CRYSTALS A.6). Additive fields through the partial classes; every
    // default keeps the earlier rule, so a definition or save that does not set them plays exactly as before.

    public sealed partial class CampaignTuning
    {
        /// <summary>
        /// Ticks per lattice step (5 m) when Mira runs: surface legs toward a crystal the party still believes on its
        /// pedestal while the newest word about it says Ash runners are heading there. 0 = she never runs (every leg at
        /// <see cref="PartyTicksPerStep"/>, the earlier rule).
        /// </summary>
        public int PartyRunTicksPerStep;
        /// <summary>Ticks to cross a stair, an exit or a door (descend, climb, open). 0 = one lattice step at the leg's pace (the earlier rule).</summary>
        public int StairTicks;
        /// <summary>
        /// How long Mira waits at a lethal threshold while the player is with her before she withdraws. -1 = the same as
        /// <see cref="ThresholdWait"/> (the earlier rule); 0 = no limit (she waits for "Fight" or "Not yet"). With the
        /// player away <see cref="ThresholdWait"/> applies, counted from the moment the player left or she arrived.
        /// </summary>
        public int ThresholdWaitPresent = -1;
        /// <summary>
        /// Delay of the report that the expedition has set out on its first step: -1 = <see cref="ReportDelay"/> (the earlier
        /// rule); 0 = Mira knows at the opening where the Ash runners are heading.
        /// </summary>
        public int OpeningReportDelay = -1;
        /// <summary>
        /// 1 = an overrun turns the party home whether a camp of yours was lost or your home camp looted (the earlier rule);
        /// 0 = only when a camp is lost (a looting still charges <see cref="CampOverrunCharge"/>).
        /// </summary>
        public int RetreatOnLooting = 1;
        /// <summary>
        /// After an overrun turned the party home, later overruns within this many ticks only charge the clock (the party
        /// keeps on). 0 = every overrun turns it home (the earlier rule).
        /// </summary>
        public int OverrunRetreatCooldown;
        /// <summary>Ticks per lattice step inside an interior (underground, by lamp light). 0 = <see cref="PartyTicksPerStep"/> (the earlier rule).</summary>
        public int InteriorTicksPerStep;
    }

    public sealed partial class RivalPlanDef
    {
        /// <summary>
        /// Crystal ids the expedition may open with; at the start of a run one is drawn from the campaign's seed and its
        /// step is moved to the front (the march costs and spreads stay with the plan's positions, so the opening march is
        /// always the first step's). Empty = the plan's own order.
        /// </summary>
        public List<string> OpenWith = new List<string>();
    }

    public sealed partial class RivalStepDef
    {
        /// <summary>The march cost of this position is drawn per run, uniformly in MarchCost - MarchSpread to MarchCost + MarchSpread. 0 = exactly MarchCost.</summary>
        public int MarchSpread;
    }

    public sealed partial class CampaignState
    {
        /// <summary>
        /// The expedition's plan for this run, drawn at the start from <see cref="RivalPlanDef.OpenWith"/> and
        /// <see cref="RivalStepDef.MarchSpread"/>; null = the definition's plan as written (no draw, or a save from before).
        /// </summary>
        public List<RivalStepDef> RivalPlan;
        /// <summary>Tick an overrun last turned the party home (-1 = never); see <see cref="CampaignTuning.OverrunRetreatCooldown"/>.</summary>
        public long LastOverrunRetreatTick = -1;
    }

    public sealed partial class PartyState
    {
        /// <summary>Mira broke into a run on the current route (said once per route in the journal).</summary>
        public bool Running;
    }

    internal static partial class CampaignRuntime
    {
        /// <summary>The run's plan: the drawn one, else the definition's.</summary>
        internal static List<RivalStepDef> RunPlan(CampaignState c) => c.RivalPlan ?? c.Definition.Rival?.Steps ?? new List<RivalStepDef>();

        /// <summary>The per-run draw of the expedition's opening and march costs (null when the plan has nothing to draw).</summary>
        internal static List<RivalStepDef> DrawRivalPlan(WorldState s, CampaignDefinition d)
        {
            var steps = d.Rival?.Steps ?? new List<RivalStepDef>();
            var open = d.Rival?.OpenWith ?? new List<string>();
            bool any = open.Count > 0;
            foreach (var x in steps) if (x != null && x.MarchSpread > 0) any = true;
            if (!any) return null;
            var rng = (s.Rng ?? new Rng(s.Seed)).Fork("campaign-rival:" + (d.Id ?? ""));
            var plan = new List<RivalStepDef>();
            foreach (var x in steps)
                if (x != null) plan.Add(new RivalStepDef { Crystal = x.Crystal, MarchCost = x.MarchCost, CarryCost = x.CarryCost, Altar = x.Altar, MarchSpread = x.MarchSpread });
            if (open.Count > 0)
            {
                string first = open[rng.NextInt(open.Count)];
                int k = plan.FindIndex(x => x.Crystal == first);
                if (k > 0)
                {
                    // Move the crystal (with its carry and altar) to the front; the march costs stay with the positions.
                    var march = new List<(int cost, int spread)>();
                    foreach (var x in plan) march.Add((x.MarchCost, x.MarchSpread));
                    var moved = plan[k];
                    plan.RemoveAt(k);
                    plan.Insert(0, moved);
                    for (int i = 0; i < plan.Count; i++) { plan[i].MarchCost = march[i].cost; plan[i].MarchSpread = march[i].spread; }
                }
            }
            foreach (var x in plan)
                if (x.MarchSpread > 0) x.MarchCost = Math.Max(1, x.MarchCost - x.MarchSpread + rng.NextInt(2 * x.MarchSpread + 1));
            return plan;
        }

        /// <summary>A surface leg shorter than this many steps is walked even when she would run.</summary>
        internal const int RunMinSteps = 4;

        /// <summary>Ticks per step for the next leg: running on the surface toward a crystal the Ash are reported heading for.</summary>
        private static int LegTicksPerStep(WorldState s, CampaignState c, MarkerRef from, MarkerRef to, out bool run)
        {
            var t = c.Definition.Tuning;
            run = false;
            if (from.IsInterior && to.IsInterior && t.InteriorTicksPerStep > 0) return t.InteriorTicksPerStep;
            if (t.PartyRunTicksPerStep <= 0 || from.IsInterior || to.IsInterior) return t.PartyTicksPerStep;
            var p = c.Party;
            if (p.Purpose != RoutePurpose.Lead) return t.PartyTicksPerStep;
            var cd = RaceCrystal(c);
            if (cd == null) return t.PartyTicksPerStep;
            var st = c.Crystal(cd.Element);
            var b = c.BeliefOf(cd.Element);
            if (st == null || b == null || st.Custody == Custody.Carried || st.Custody == Custody.Restored || b.Custody != Custody.AtSite) return t.PartyTicksPerStep;
            if (!ReportedMarching(s, c, cd.Element)) return t.PartyTicksPerStep;
            run = true;
            return t.PartyRunTicksPerStep;
        }

        /// <summary>The crystal the followed lead serves (pack lead or the runtime's own "@take:"), null when none.</summary>
        private static CrystalDef RaceCrystal(CampaignState c)
        {
            var p = c.Party;
            var d = c.Definition;
            string id = p.LeadDefId ?? "";
            if (id.StartsWith("@take:", StringComparison.Ordinal)) return d.Crystal(id.Substring(6));
            var lead = id.Length > 0 && id[0] != '@' ? d.Lead(id) : null;
            return lead == null || string.IsNullOrEmpty(lead.Crystal) ? null : d.Crystal(lead.Crystal);
        }
    }
}
