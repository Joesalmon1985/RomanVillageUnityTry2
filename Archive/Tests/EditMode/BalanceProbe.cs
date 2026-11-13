using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>
    /// Tuning probe (explicit, not part of the normal run): plays the scripted games and prints the numbers the
    /// balance goals are judged on (cards by family, round and age lengths, raw waste, bottlenecks, game length).
    /// Run: dotnet test DotNet --filter BalanceProbe (output also written to %TEMP%/balance_probe.txt).
    /// </summary>
    public class BalanceProbe
    {
        private const string NL = "\n";

        /// <summary>Minutes from the first round of an age to the first round of the next (or the game end); "-" if not reached.</summary>
        internal static string AgeLen(GameRecord g, int age)
        {
            var first = g.Timeline.FirstOrDefault(x => x.Age == age && x.RoundInAge == 1);
            if (first == null) return "-";
            var next = g.Timeline.FirstOrDefault(x => x.Age == age + 1 && x.RoundInAge == 1);
            long end = next != null ? next.Tick : g.Ticks;
            return ((end - first.Tick) / 600.0).ToString("0.0");
        }

        [Test, Explicit]
        public void Probe()
        {
            var sb = new StringBuilder();
            foreach (var mode in new[] { "competent", "full", "careless", "passive" })
            {
                foreach (var seed in GameRuns.Seeds)
                {
                    var r = GameRuns.Get(seed, mode);
                    var fam = r.PlayerCards.Select(id => CardTable.Get(id).Family).Distinct().Count();
                    var starts = r.Timeline.Select(x => x.Tick).ToList();
                    var rl = starts.Zip(starts.Skip(1).Concat(new[] { r.Ticks }), (a, b) => (b - a) / 10).ToList();
                    sb.Append($"{mode,-9} {seed,8} {r.Outcome,-7} {r.Reason,-7} {r.Minutes,5:0.0}m VP {r.PlayerVp,2}-{r.RivalVp,2} " +
                              $"[c{r.Player.Cities} w{r.Player.Wonder} k{r.Player.Cards} m{r.Player.Military} o{r.Player.Outpost} | rp{r.Rival.Plan} rk{r.Rival.Cards} rm{r.Rival.Military}] " +
                              $"br{r.Breaches} units{r.UnitsTrained} comm {r.Milestone("commission") / 600.0:0.0} w2 {(r.Milestone("wonder2") >= 0 ? "Y" : "-")} " +
                              $"cards {r.PlayerCards.Count}/{fam}fam [{string.Join(",", r.PlayerCards)}] rival [{string.Join(",", r.RivalCards)}] picks [{string.Join(",", r.PlayerPicks)}] " +
                              $"bott {Wares.DisplayName(r.Bottleneck)} carriers {r.Carriers} rounds(s) {string.Join(" ", rl)}" + NL);
                    if (mode == "competent")
                    {
                        sb.Append("    raw waste%: ");
                        foreach (var w in Wares.Raw)
                        {
                            int a = r.Ledger.BatchAuthorised[(int)w], l = r.Ledger.BatchLost[(int)w];
                            sb.Append($"{Wares.DisplayName(w)} {l}/{a}={(a == 0 ? 0 : 100 * l / a)}% h{r.Ledger.Harvested[(int)w]} u{r.Ledger.Consumed[(int)w]}; ");
                        }
                        sb.Append(" stalls: ");
                        foreach (int w in Enumerable.Range(0, Wares.Count).OrderByDescending(w => r.StallTicksByWare[w]).Take(4))
                            sb.Append($"{Wares.DisplayName((Ware)w)} {r.StallTicksByWare[w] / 600} ");
                        sb.Append(NL);
                        if (r.Outcome != GameOutcome.Victory) sb.Append("    LOST: " + r + NL);
                    }
                }
            }
            var comp = GameRuns.Seeds.Select(x => GameRuns.Get(x, "competent")).ToList();
            var full = GameRuns.Seeds.Select(x => GameRuns.Get(x, "full")).ToList();
            var care = GameRuns.Seeds.Select(x => GameRuns.Get(x, "careless")).ToList();
            var pas = GameRuns.Seeds.Select(x => GameRuns.Get(x, "passive")).ToList();
            sb.Append("SUMMARY" + NL);
            sb.Append($"1. comp wins {comp.Count(g => g.Outcome == GameOutcome.Victory)}/11; >=3 families {comp.Count(g => g.PlayerCards.Select(id => CardTable.Get(id).Family).Distinct().Count() >= 3)}; civic pts>0 {comp.Count(g => g.Player.Cards > 0)}; wonder2 {comp.Count(g => g.Milestone("wonder2") >= 0)}; rival cards avg {comp.Average(g => g.RivalCards.Count):0.0} rival card pts {string.Join(",", comp.Select(g => g.Rival.Cards))}" + NL);
            sb.Append("2. age lengths (comp, min): " + string.Join(" | ", comp.Select(g => string.Join("/", Enumerable.Range(1, 3).Select(a => AgeLen(g, a))))) + NL);
            var bott = comp.GroupBy(g => g.Bottleneck).Select(x => Wares.DisplayName(x.Key) + " " + x.Count());
            sb.Append($"3. all five used {comp.Count(g => Wares.Raw.All(w => g.Ledger.Harvested[(int)w] > 0 && g.Ledger.Consumed[(int)w] > 0))}/11; bottlenecks {string.Join(", ", bott)}; waste% " +
                      string.Join(", ", Wares.Raw.Select(w => Wares.DisplayName(w) + " " + (100.0 * comp.Sum(g => g.Ledger.BatchLost[(int)w]) / Math.Max(1, comp.Sum(g => g.Ledger.BatchAuthorised[(int)w]))).ToString("0.0") + " (max " + comp.Max(g => 100 * g.Ledger.BatchLost[(int)w] / Math.Max(1, g.Ledger.BatchAuthorised[(int)w])) + ")")) + NL);
            sb.Append($"4. comp in 10-15: {comp.Count(g => g.Ticks >= 6000 && g.Ticks <= 9000)}, full in 10-15: {full.Count(g => g.Ticks >= 6000 && g.Ticks <= 9000)}; comp {comp.Min(g => g.Minutes):0.0}-{comp.Max(g => g.Minutes):0.0}; full {full.Min(g => g.Minutes):0.0}-{full.Max(g => g.Minutes):0.0}; weapons made comp {comp.Count(g => g.Ledger.Produced[(int)Ware.Weapons] > 0)} full {full.Count(g => g.Ledger.Produced[(int)Ware.Weapons] > 0)}; soldiers comp {comp.Count(g => g.UnitsTrained > 0)}" + NL);
            sb.Append($"5. commission {comp.Min(g => g.Milestone("commission")) / 600.0:0.0}-{comp.Max(g => g.Milestone("commission")) / 600.0:0.0}" + NL);
            sb.Append($"6. careless losses {care.Count(g => g.Outcome != GameOutcome.Victory)} (breach {care.Count(g => g.Reason == OutcomeReasons.Breach)}); passive losses {pas.Count(g => g.Outcome != GameOutcome.Victory)}; carriers {comp.Min(g => g.Carriers)}-{comp.Max(g => g.Carriers)} busy {comp.Min(g => g.CarrierUsePercent)}-{comp.Max(g => g.CarrierUsePercent)}%" + NL);
            Console.WriteLine(sb.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "balance_probe.txt"), sb.ToString());
        }
    }
}
