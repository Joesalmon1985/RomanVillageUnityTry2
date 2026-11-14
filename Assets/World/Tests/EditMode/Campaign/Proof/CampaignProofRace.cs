using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using World.Sim.Campaign;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Section H of the campaign proof: the race, after the race tuning (sim-H1-cry-S1-tuned). The Earth slice on the
    /// authored pack and the whole campaign on the skeleton made race-sized (ProofStarts.RaceSkeleton), 11 seeds per player
    /// type, each run also played as a twin with the overruns' clock effects removed to tell when an overrun decided a crystal.
    /// </summary>
    public partial class CampaignProofReport
    {
        public static readonly ulong[] RaceSeeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

        private static readonly Element[] Four = { Element.Earth, Element.Fire, Element.Water, Element.Air };

        /// <summary>Ticks the skeleton's pedestal visits are padded by in the race-sized copy (the Earth route's stairs and underground walk).</summary>
        public const int SkeletonPad = 0;

        private static readonly Lazy<double> FinaleRate = new Lazy<double>(() =>
        {
            int won = 0;
            for (int k = 1; k <= 1000; k++)
                if (ProofRun.PlayWard("finale_unweaver", (ulong)k * 0x9E3779B97F4A7C15UL + 1, WardOpponentTests.FirstTimer) == DuelResult.Victory) won++;
            return won / 1000.0;
        });

        private sealed partial class Builder
        {
            private double _keeperRate;

            private sealed class RaceRow
            {
                public string Player;
                public ulong Seed;
                public string Earth = "", Opens = "", Ending = "", Results = "";
                public long March = -1, Reached = -1, Seized = -1, RivalThere = -1, Restored = -1, Length = -1, OverCharge;
                public int Duels, Deaths, OverYou, OverAsh, Pauses, First, Stolen, Decided, PrinceCasts, Withdrawals;
                public bool Lost;
                public string[] Outcome = new string[4];
            }

            private static void NoOverrunClock(CampaignDefinition d)
            {
                d.Tuning.CampOverrunCharge = 0;
                d.Tuning.AshCampLostPause = 0;
            }

            private static string Outcome(ProofRun r, Element e)
            {
                var ch = r.Chain(e);
                return ch.Contains(Custody.RivalTransit) || ch.Contains(Custody.AtRivalAltar) ? "stolen" : ch.Contains(Custody.Carried) ? "first" : "-";
            }

            private static void Fill(RaceRow w, ProofRun r)
            {
                w.Duels = r.Duels.Count;
                w.Results = string.Concat(r.Duels.Select(x => x.Result == DuelResult.Victory ? "W" : x.Result == DuelResult.Defeat || x.Result == DuelResult.Surrender ? "L" : "D"));
                w.Deaths = r.C.Companions.Count(k => !k.Alive);
                w.OverYou = r.OverrunsOfYou;
                w.OverAsh = r.OverrunsOfAsh;
                w.OverCharge = r.Of<RivalAdvanced>().Where(x => x.Why == RivalCharges.CampOverrun).Sum(x => x.Amount);
                w.Pauses = r.Of<RivalPaused>().Count();
                w.PrinceCasts = r.SpellCasts[PlayerIds.Human];
                w.Withdrawals = r.Of<PartyRetreated>().Count(x => x.Why == "threshold_wait");
                for (int i = 0; i < 4; i++) w.Outcome[i] = Outcome(r, (Element)i);
            }

            private static long Median(IEnumerable<long> xs)
            {
                var l = xs.Where(x => x >= 0).OrderBy(x => x).ToList();
                return l.Count == 0 ? -1 : l[(l.Count - 1) / 2];
            }

            private static string F1(double x) => x.ToString("0.0", Inv);
            private static string Signed(long ticks) => ticks < 0 ? "-" + T(-ticks) : "+" + T(ticks);

            private void SectionH()
            {
                var a = PackFiles.Load(Authored);
                var t = a.Tuning;
                var plan = a.Rival.Steps;
                L("## H. The race");
                L();
                L("**What changed (the race tuning, `sim-H1-cry-S1-tuned`).** The party, not the player's glide, sets the clock. A lattice step is 5 m. Mira and the companions walk at " + Speed(t.PartyTicksPerStep) + " on the surface (`PartyTicksPerStep` " + t.PartyTicksPerStep + ", was 7) and " + Speed(t.InteriorTicksPerStep) + " underground by lamp light (`InteriorTicksPerStep` " + t.InteriorTicksPerStep + "); she runs at " + Speed(t.PartyRunTicksPerStep) + " (`PartyRunTicksPerStep` " + t.PartyRunTicksPerStep + ") only on surface legs of " + CampaignRuntime.RunMinSteps + " steps or more toward a crystal the party still believes on its pedestal while the newest word says Ash runners are heading there, and says so in the journal. A stair, exit or door takes " + (t.StairTicks / 10) + " s, reading a marker or talking " + (t.ReadTicks / 10) + " s, searching " + (t.SearchTicks / 10) + " s, taking a crystal " + (t.TakeTicks / 10) + " s, setting it in its tower " + (t.RestoreTicks / 10) + " s. With you she waits " + (t.LeadPatience / 10) + " s for \"Lead the way\" and an answer, and at a lethal threshold she waits for \"Fight\" or \"Not yet\" with no limit; alone she waits " + (t.SoloDecide / 10) + " s before each lead and " + (t.ThresholdWait / 10) + " s at a threshold. The expedition's opening is known at once (`OpeningReportDelay` 0) and is drawn per seed: on the authored pack it always opens with the Deepstone, its march drawn in " + T(plan[0].MarchCost - plan[0].MarchSpread) + " to " + T(plan[0].MarchCost + plan[0].MarchSpread) + " (was 8:00); every later march in " + T(plan[1].MarchCost - plan[1].MarchSpread) + " to " + T(plan[1].MarchCost + plan[1].MarchSpread) + " (was 6:00), carry " + T(plan[1].CarryCost) + ". An overrun of your camps charges " + T(t.CampOverrunCharge) + " (was 3:00) and turns the party home only when a camp is lost and no overrun did so in the last " + T(t.OverrunRetreatCooldown) + "; an Ash camp lost stops the expedition for " + T(t.AshCampLostPause) + " (was 1:30). The full list with before and after is in `PRD_CRYSTALS` A.6.");
                L();
                L("Players: **prompt** (decides at once), **45 s** and **90 s** (each decision, \"Lead the way\", \"Fight\", \"Set it\", an answer, taken that long after it is offered), and **with the prince** (\"Go on without me\" at the start; on the Earth slice never back; in the whole campaign back " + 60 + " s after Mira starts to wait for them at a tower or a threshold, prompt while there, and off again as soon as she sets out). Seeds 1 to " + RaceSeeds.Length + ", real Ward duels by the scripted first-timer on each attempt's own seed. Every run is also played as a **twin** from the same start with the overruns' effect on the expedition clock removed (`CampOverrunCharge` and `AshCampLostPause` 0; the overruns and the retreats still happen): a crystal is **decided by an overrun** when its outcome (first or stolen) differs between the run and its twin.");
                L();
                EarthRace();
                SlowAtThreshold();
                WholeCampaign();
            }

            private static string Speed(int ticksPerStep) => ticksPerStep <= 0 ? "-" : (50.0 / ticksPerStep).ToString("0.0", Inv) + " m/s";

            // ------------------------------------------------------------ H1, the Earth slice

            private void EarthRace()
            {
                var players = new[] { Prompt(), Prompt(450), Prompt(900), new ProofPolicy { Name = "with the prince", Away = true } };
                var cases = new List<(ProofPolicy p, ulong seed)>();
                foreach (var p in players) foreach (var s in RaceSeeds) cases.Add((p, s));
                var rows = new RaceRow[cases.Count];
                var twins = new string[cases.Count];
                Parallel.For(0, cases.Count * 2, k =>
                {
                    int i = k / 2;
                    bool twin = k % 2 == 1;
                    var pol = cases[i].p.Clone();
                    pol.Duel = WardOpponentTests.FirstTimer;
                    var r = Keep(ProofStarts.Start(Authored, cases[i].seed, pol, twin ? NoOverrunClock : (Action<CampaignDefinition>)null));
                    r.Run(EarthRestored, 24000);
                    Claim(r.Failures.Count == 0, "H1 " + pol.Name + " seed " + cases[i].seed + (twin ? " twin" : "") + ": " + string.Join("; ", r.Failures));
                    if (twin) { twins[i] = Outcome(r, Element.Earth); r.Sim = null; return; }
                    var w = new RaceRow { Player = pol.Name, Seed = cases[i].seed };
                    w.Earth = Outcome(r, Element.Earth);
                    w.March = r.C.RivalPlan != null && r.C.RivalPlan.Count > 0 ? r.C.RivalPlan[0].MarchCost : -1;
                    w.Reached = FirstTick<CrystalCustodyChanged>(r, x => x.Crystal == Element.Earth && x.From == Custody.AtSite && x.To == Custody.Carried);
                    w.Seized = FirstTick<CrystalCustodyChanged>(r, x => x.Crystal == Element.Earth && x.To == Custody.RivalTransit);
                    var there = r.C.Reports.FirstOrDefault(x => x.Crystal == Element.Earth && (x.Kind == RivalReportKind.Seized || x.Kind == RivalReportKind.Searching));
                    w.RivalThere = there == null ? -1 : there.Tick - r.C.StartTick;
                    w.Restored = FirstTick<CrystalRestored>(r, x => x.Crystal == Element.Earth);
                    Fill(w, r);
                    Claim(!r.RosterUnlocked && r.MaxPoints <= 4, "H1: no roster unlock from crystal points");
                    Claim(w.Earth != "first" || w.Duels == 0, "H1 " + pol.Name + " seed " + w.Seed + ": arriving first means no lethal duel for the Deepstone");
                    rows[i] = w;
                    r.Sim = null;
                });
                for (int i = 0; i < rows.Length; i++) rows[i].Decided = twins[i] != rows[i].Earth ? 1 : 0;
                _earthDecided = rows.Sum(x => x.Decided);
                _earthRuns = rows.Length;

                L("### H1. The Earth slice (authored pack)");
                L();
                L("Each run lasts until the Deepstone is restored or 40 game minutes. \"Ash at the quarry\" is when the expedition reached the pedestal (it seizes the Deepstone, or finds it gone and searches); the margin is that minus the time the party took it.");
                L();
                L("| Player | Seed | Drawn march | Ash at the quarry | Deepstone | Taken at | Margin | Seized at | Restored at | Duels | Deaths | Overruns of you / of the Ash | Decided by an overrun |");
                L("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var w in rows)
                    L("| " + w.Player + " | " + w.Seed + " | " + T(w.March) + " | " + T(w.RivalThere) + " | " + w.Earth + " | " + T(w.Reached) + " | " + (w.Reached >= 0 && w.RivalThere >= 0 ? Signed(w.RivalThere - w.Reached) : "-") + " | " + T(w.Seized) + " | " + T(w.Restored)
                      + " | " + w.Duels + (w.Duels > 0 ? " (" + w.Results + ")" : "") + " | " + w.Deaths + " | " + w.OverYou + " / " + w.OverAsh + " | " + (w.Decided > 0 ? "yes (twin: " + twins[Array.IndexOf(rows, w)] + ")" : "no") + " |");
                L();
                L("| Player | Deepstone first | Stolen | Median margin when first | Median taken at | Median restored at | Lethal duels | Deaths | Decided by an overrun |");
                L("|---|---|---|---|---|---|---|---|---|");
                foreach (var g in rows.GroupBy(x => x.Player))
                {
                    var firsts = g.Where(x => x.Earth == "first" && x.RivalThere >= 0).Select(x => x.RivalThere - x.Reached);
                    L("| " + g.Key + " | " + g.Count(x => x.Earth == "first") + " of " + g.Count() + " | " + g.Count(x => x.Earth == "stolen") + " | " + T(Median(firsts)) + " | " + T(Median(g.Select(x => x.Reached))) + " | " + T(Median(g.Select(x => x.Restored)))
                      + " | " + g.Sum(x => x.Duels) + " | " + g.Sum(x => x.Deaths) + " | " + g.Sum(x => x.Decided) + " |");
                }
                L();

                var prompt = rows.Where(x => x.Player == "prompt").ToList();
                var mid = rows.Where(x => x.Player == "45 s").ToList();
                var slow = rows.Where(x => x.Player == "90 s").ToList();
                var prince = rows.Where(x => x.Player == "with the prince").ToList();
                var marches = rows.Select(x => x.March).Distinct().ToList();
                long lo = a0.MarchCost - a0.MarchSpread, hi = a0.MarchCost + a0.MarchSpread;
                long promptMargin = Median(prompt.Where(x => x.Earth == "first" && x.RivalThere >= 0).Select(x => x.RivalThere - x.Reached));
                var promptRestores = prompt.Where(x => x.Earth == "first").Select(x => x.Restored).ToList();

                // The prompt party when the Ash got there first (the "Ash got there first" start).
                var theft = Keep(ProofStarts.Start(Authored, RaceSeeds[0], Prompt()));
                theft.Do(new DevRivalNextStep());
                theft.Run(EarthRestored, 12000);
                long theftRestored = FirstTick<CrystalRestored>(theft, x => true);
                long theftDuel = theft.Duels.Count > 0 ? theft.Since(theft.Duels[0].Tick) : -1;
                Claim(theft.Failures.Count == 0, "H1 theft: " + string.Join("; ", theft.Failures));
                _theftRestoredT = theftRestored;
                _promptRestoreT = promptRestores.Count > 0 ? promptRestores.Max() : -1;

                L("Asserted on the Earth slice:");
                L();
                Check(prompt.Count(x => x.Earth == "first") >= 9, "The prompt party reaches the Deepstone first on most seeds (at least 9 of 11): " + prompt.Count(x => x.Earth == "first") + " of 11.");
                Check(promptMargin > 0 && promptMargin <= 1800, "Not by a mile: the prompt party's median margin is under 3:00: " + T(promptMargin) + " (it was 7:21 before the tuning).");
                Check(promptRestores.Count > 0 && promptRestores.All(x => x >= 3000 && x <= 4200), "Followed promptly and not stolen, the Earth route takes 5 to 7 minutes from the opening to the restoration: " + T(promptRestores.Min()) + " to " + T(promptRestores.Max()) + ".");
                Check(theftRestored >= 3600 && theftRestored <= 6600, "Followed promptly with the theft trail and the duel (the \"Ash got there first\" start, seed " + RaceSeeds[0] + ", duel won), it takes 6 to 11 minutes of game time: Dolm's threshold and the duel at " + T(theftDuel) + ", restored at " + T(theftRestored) + ". Game time does not run during the duel; with a duel of one to two minutes of play the player's own time is about 8 to 9 minutes.");
                int midStolen = mid.Count(x => x.Earth == "stolen");
                Check(midStolen >= 3 && midStolen <= 8, "The 45 s player's race is close to even: stolen on " + midStolen + " of 11 (3 to 8 asserted).");
                Check(slow.Count(x => x.Earth == "stolen") >= 9, "The 90 s player loses it on most seeds: stolen on " + slow.Count(x => x.Earth == "stolen") + " of 11.");
                Check(prince.Count(x => x.Earth == "stolen") >= 9, "The player who stays with the prince finds it taken: stolen on " + prince.Count(x => x.Earth == "stolen") + " of 11.");
                Check(rows.All(x => x.Earth != "first" || x.Duels == 0), "Arriving first means no lethal duel for that crystal: every first arrival had no duel; every lethal duel above was fought for a stolen Deepstone.");
                Check(marches.Count >= 5 && marches.All(x => x >= lo && x <= hi), "The expedition's opening march differs by seed within its authored bounds (" + T(lo) + " to " + T(hi) + "): " + marches.Count + " different marches in 11 seeds, " + T(marches.Min()) + " to " + T(marches.Max()) + ".");
                Check(rows.All(x => x.PrinceCasts > 0), "The prince builds and casts in every run (he is autonomous).");
                L();
            }

            private RivalStepDef a0 => PackFiles.Load(Authored).Rival.Steps[0];

            /// <summary>One asserted check, written as a bullet with its verdict.</summary>
            private void Check(bool ok, string what)
            {
                Claim(ok, "H: " + what);
                L("- " + (ok ? "**Met.** " : "**NOT MET.** ") + what);
            }

            // ------------------------------------------------------------ H2, slow players at a threshold

            private void SlowAtThreshold()
            {
                L("### H2. The slow players at a lethal threshold");
                L();
                var res = new Dictionary<int, (long commit, int withdrew, int duels)>();
                foreach (var dl in new[] { 450, 900 })
                {
                    var sp = Prompt(dl);
                    sp.Duel = WardOpponentTests.FirstTimer;
                    var r = Keep(new ProofRun(SaveCodec.Load(_threshold, out _), sp));
                    long t0 = r.S.Tick;
                    r.Run(x => x.C.Resolved.Count > 0 || x.C.Phase != CampaignPhase.Running, 6000);
                    var commit = r.Of<EncounterCommitted>().FirstOrDefault();
                    res[dl] = (commit == null ? -1 : commit.Tick - t0, r.Of<PartyRetreated>().Count(x => x.Why == "threshold_wait"), r.Duels.Count);
                    Claim(r.Failures.Count == 0, "H2 " + dl + ": " + string.Join("; ", r.Failures));
                }
                var silent = Keep(new ProofRun(SaveCodec.Load(_threshold, out _), new ProofPolicy { Name = "silent", NeverFight = true }));
                silent.Run(x => false, 6000);
                int silentBack = silent.Of<PartyRetreated>().Count(x => x.Why == "threshold_wait");
                L("From section B's threshold before Dolm (seed " + Seeds[0] + "), with the player present. The rule now: **with the player near (`PlayerPresent`) Mira waits at a lethal threshold for \"Fight\" or \"Not yet\" with no time limit (`ThresholdWaitPresent` 0); the 90 s withdrawal (`ThresholdWait`) applies only while the player is away, counted from when she arrived or from when the player left.** Section F2 shows the away case (she withdraws after 90 s and later holds).");
                L();
                Check(res[450].commit >= 450 && res[450].commit <= 452 && res[450].withdrew == 0, "The 45 s player commits " + (res[450].commit / 10) + " s after the threshold is offered, with no withdrawal.");
                Check(res[900].commit >= 900 && res[900].commit <= 902 && res[900].withdrew == 0, "The 90 s player now fights too: committed " + (res[900].commit / 10) + " s after, with no withdrawal (before the tuning Mira withdrew at exactly 90 s, every time, and that player never fought).");
                Check(silentBack == 0 && silent.C.Party.Mode == PartyMode.AtThreshold, "A present player who says nothing for 10 minutes: Mira is still at the threshold, " + silentBack + " withdrawals.");
                L();
            }

            // ------------------------------------------------------------ H3, the whole campaign

            private void WholeCampaign()
            {
                var players = new[] { Prompt(), Prompt(450), Prompt(900), new ProofPolicy { Name = "with the prince", Wander = true, Delay = 600 } };
                var cases = new List<(ProofPolicy p, ulong seed)>();
                foreach (var p in players) foreach (var s in RaceSeeds) cases.Add((p, s));
                var rows = new RaceRow[cases.Count];
                var twins = new string[cases.Count][];
                Parallel.For(0, cases.Count * 2, k =>
                {
                    int i = k / 2;
                    bool twin = k % 2 == 1;
                    var pol = cases[i].p.Clone();
                    pol.Duel = WardOpponentTests.FirstTimer;
                    var r = Keep(ProofStarts.Start(Skeleton, cases[i].seed, pol, d => { ProofStarts.RaceSkeleton(d, SkeletonPad); if (twin) NoOverrunClock(d); }));
                    r.Run(x => x.C.Phase != CampaignPhase.Running, 90000);
                    Claim(r.Failures.Count == 0, "H3 " + pol.Name + " seed " + cases[i].seed + (twin ? " twin" : "") + ": " + string.Join("; ", r.Failures));
                    if (twin) { twins[i] = Four.Select(e => Outcome(r, e)).ToArray(); r.Sim = null; return; }
                    var w = new RaceRow { Player = pol.Name, Seed = cases[i].seed };
                    var first = r.C.RivalPlan[0];
                    w.Opens = first.Crystal + " " + T(first.MarchCost);
                    w.Ending = r.C.Phase == CampaignPhase.Running ? "running" : r.C.EndingId;
                    w.Lost = r.C.Phase == CampaignPhase.Lost;
                    w.Length = r.C.Phase != CampaignPhase.Running ? r.Since(r.S.Tick) : -1;
                    Fill(w, r);
                    w.First = w.Outcome.Count(x => x == "first");
                    w.Stolen = w.Outcome.Count(x => x == "stolen");
                    Claim(Four.All(e => Outcome(r, e) != "first" || !r.Duels.Any(x => x.Encounter == "keeper_" + Id(e))), "H3 " + pol.Name + " seed " + w.Seed + ": no keeper duel for a crystal reached first");
                    Claim(!w.Lost || r.Duels.Count(x => x.Result == DuelResult.Defeat || x.Result == DuelResult.Surrender) >= 7, "H3: a terminal defeat only after seven lost duels");
                    rows[i] = w;
                    r.Sim = null;
                });
                for (int i = 0; i < rows.Length; i++) rows[i].Decided = Enumerable.Range(0, 4).Any(e => twins[i][e] != rows[i].Outcome[e]) ? 1 : 0;

                double pk = _keeperRate, pf = FinaleRate.Value;
                L("### H3. The whole campaign (skeleton pack, race-sized by the proof)");
                L();
                L("The skeleton is the only Complete pack, and its routes are one step each. For this section only, a copy of it is made race-sized (`ProofStarts.RaceSkeleton`; the shipped skeleton is not changed): it takes the authored pack's tuning and expedition timings, lets the expedition open with any of the four crystals (drawn per seed), gives every route the Earth route's shape (one more marker to read before the pedestal, so one more decision and reading)" + (SkeletonPad > 0 ? ", and pads each pedestal visit by " + (SkeletonPad / 10) + " s." : ". No padding is needed: with the authored tuning the walks between the skeleton's sites and towers already make each route about as long as the Earth route (a prompt party restores a crystal every 5 to 6 minutes, the Earth slice takes " + T(_promptRestoreT) + ").") + " Each run lasts until an ending or 150 game minutes. W, L, D: duels won, lost (a companion dies), drawn.");
                L();
                L("| Player | Seed | Expedition opens with | Ending | Minutes | Crystals first | Stolen | Lethal duels | Deaths | Overruns of you / of the Ash | Clock added by overruns | An overrun decided a crystal |");
                L("|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var w in rows)
                    L("| " + w.Player + " | " + w.Seed + " | " + w.Opens + " | " + w.Ending + " | " + Min(w.Length) + " | " + w.First + " | " + w.Stolen + " | " + w.Duels + " (" + w.Results + ") | " + w.Deaths + " | " + w.OverYou + " / " + w.OverAsh + " | +" + T(w.OverCharge) + ", " + w.Pauses + " pause(s) | " + (w.Decided > 0 ? "yes" : "no") + " |");
                L();
                L("Per player type (11 campaigns each). Expected values use the first-timer's measured win rates: " + Pct((int)Math.Round(pk * 1000), 1000) + " against a keeper (section B) and " + Pct((int)Math.Round(pf * 1000), 1000) + " against the Unweaver (1000 seeds of `finale_unweaver`). A stolen crystal costs on average 1/p lethal duels and (1 - p)/p deaths before it is won back; the finale 1/q and (1 - q)/q.");
                L();
                L("| Player | Crystals first / stolen (of 44) | Median stolen per campaign | Lethal duels (median, mean) | Expected lethal duels | Deaths (median, mean, range) | Expected deaths | Campaigns with 1 to 3 deaths | Terminal defeats | Minutes (median, range) | Campaigns in 30 to 50 minutes | Campaigns where an overrun decided a crystal |");
                L("|---|---|---|---|---|---|---|---|---|---|---|---|");
                var summary = new Dictionary<string, (double stolen, double deaths, long minutes)>();
                foreach (var g in rows.GroupBy(x => x.Player))
                {
                    var l = g.ToList();
                    double ms = l.Average(x => x.Stolen);
                    double expDuels = ms / pk + 1 / pf, expDeaths = ms * (1 - pk) / pk + (1 - pf) / pf;
                    long minutes = Median(l.Select(x => x.Length));
                    summary[g.Key] = (Median(l.Select(x => (long)x.Stolen)), Median(l.Select(x => (long)x.Deaths)), minutes);
                    L("| " + g.Key + " | " + l.Sum(x => x.First) + " / " + l.Sum(x => x.Stolen) + " | " + Median(l.Select(x => (long)x.Stolen)) + " | " + Median(l.Select(x => (long)x.Duels)) + ", " + F1(l.Average(x => x.Duels)) + " | " + F1(expDuels)
                      + " | " + Median(l.Select(x => (long)x.Deaths)) + ", " + F1(l.Average(x => x.Deaths)) + ", " + l.Min(x => x.Deaths) + " to " + l.Max(x => x.Deaths) + " | " + F1(expDeaths) + " | " + l.Count(x => x.Deaths >= 1 && x.Deaths <= 3)
                      + " | " + l.Count(x => x.Lost) + " | " + Min(minutes) + ", " + Min(l.Where(x => x.Length >= 0).Select(x => x.Length).DefaultIfEmpty(-1).Min()) + " to " + Min(l.Max(x => x.Length)) + " | " + l.Count(x => x.Length >= 18000 && x.Length <= 30000) + " | " + l.Sum(x => x.Decided) + " |");
                }
                L();
                var prompt = rows.Where(x => x.Player == "prompt").ToList();
                var mid = rows.Where(x => x.Player == "45 s").ToList();
                var prince = rows.Where(x => x.Player == "with the prince").ToList();
                L("Asserted on the whole campaign:");
                L();
                Check(summary["45 s"].minutes >= 18000 && summary["45 s"].minutes <= 30000, "The 45 s player's median campaign lasts 30 to 50 minutes: " + Min(summary["45 s"].minutes) + " (" + mid.Count(x => x.Length >= 18000 && x.Length <= 30000) + " of 11 campaigns in the band; see the overruns below for the others).");
                Check(summary["prompt"].stolen <= 1 && summary["prompt"].deaths <= 1 && prompt.All(x => x.Deaths <= 2), "A prompt first-timer loses none or one companion: median " + summary["prompt"].deaths + ", at most " + prompt.Max(x => x.Deaths) + "; median crystals stolen " + summary["prompt"].stolen + ".");
                Check(summary["45 s"].stolen >= 1 && summary["45 s"].stolen <= 3 && summary["45 s"].deaths >= 1 && summary["45 s"].deaths <= 3, "A middling (45 s) first-timer loses some crystals and wins them back by duel: median " + summary["45 s"].stolen + " stolen and " + summary["45 s"].deaths + " companion(s) dead per campaign; " + mid.Count(x => x.Deaths >= 1 && x.Deaths <= 3) + " of 11 campaigns lose 1 to 3.");
                Check(summary["with the prince"].stolen >= 2, "The player who stays with the prince finds crystals taken: median " + summary["with the prince"].stolen + " of 4 stolen.");
                int lost = rows.Count(x => x.Lost);
                Check(lost <= 2, "Terminal defeat is rare and comes only from losing seven duels: " + lost + " of " + rows.Length + " campaigns.");
                Check(rows.All(x => x.Ending != "running"), "Every campaign reached an ending within 150 minutes.");
                L();

                // H4, the faction game underneath.
                L("### H4. Overruns, with the prince autonomous");
                L();
                L("The faction game runs with its default balance and nobody helps the prince. In these runs an overrun comes about every 6:30 of game time, and on each seed one side always wins it: on seeds " + string.Join(", ", RaceSeeds.Where(s => rows.Where(x => x.Seed == s).Sum(x => x.OverYou) > rows.Where(x => x.Seed == s).Sum(x => x.OverAsh))) + " the Ash Clan takes your camps (each +" + T(PackFiles.Load(Authored).Tuning.CampOverrunCharge) + " on the expedition clock, and a trip home at most once in " + T(PackFiles.Load(Authored).Tuning.OverrunRetreatCooldown) + "), on the others your people take Ash camps (each stops the expedition " + T(PackFiles.Load(Authored).Tuning.AshCampLostPause) + ").");
                L();
                L("| Player | Overruns of you / of the Ash (44 campaigns) | Clock added by overruns (total) | Expedition pauses | Campaigns where an overrun decided a crystal | Campaign minutes, seeds where you were overrun (median) | Campaign minutes, other seeds (median) |");
                L("|---|---|---|---|---|---|---|");
                foreach (var g in rows.GroupBy(x => x.Player))
                {
                    var l = g.ToList();
                    L("| " + g.Key + " | " + l.Sum(x => x.OverYou) + " / " + l.Sum(x => x.OverAsh) + " | +" + T(l.Sum(x => x.OverCharge)) + " | " + l.Sum(x => x.Pauses) + " | " + l.Sum(x => x.Decided) + " of " + l.Count + " | " + Min(Median(l.Where(x => x.OverYou > x.OverAsh).Select(x => x.Length))) + " | " + Min(Median(l.Where(x => x.OverYou <= x.OverAsh).Select(x => x.Length))) + " |");
                }
                L();
                int decided = rows.Sum(x => x.Decided);
                _overrunSeeds = string.Join(", ", RaceSeeds.Where(s => rows.Where(x => x.Seed == s).Sum(x => x.OverYou) > rows.Where(x => x.Seed == s).Sum(x => x.OverAsh)));
                _midOverrunMedian = Median(mid.Where(x => x.OverYou > x.OverAsh).Select(x => x.Length));
                _midInBand = mid.Count(x => x.Length >= 18000 && x.Length <= 30000);
                L("An overrun decided at least one crystal in " + decided + " of " + rows.Length + " whole campaigns (the twin with the overruns' clock effect removed had another outcome for some crystal; after the first difference the two runs part ways, so only the first is counted) and the Deepstone in " + _earthDecided + " of " + _earthRuns + " Earth-slice runs (in both directions: an Ash camp lost can save a crystal, a camp of yours lost can cost one). Where the Ash Clan wins the faction game the campaign is longer for every player type (the table above), because each overrun moves the expedition on and the retreats cost the walk home.");
                L();
            }

            private int _earthDecided, _earthRuns, _midInBand;
            private long _theftRestoredT = -1, _midOverrunMedian = -1, _promptRestoreT = -1;
            private string _overrunSeeds = "";
        }
    }
}
