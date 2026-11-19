using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// G1-2 proof (the start of G1-3): AI-only games over many seeds with the AI brain on every seat, 4 peoples on the
    /// fixed start corners, BoardGenerator.CreateMvpBoard(seed) until the dealt board lands. Writes Docs/progress/G1-2-proof.md.
    /// </summary>
    public class TableAiProof
    {
        public const int Seeds = 60;
        public const ulong FirstSeed = 1000;
        private const int MaxTicks = 400000;

        public sealed class GameResult
        {
            public ulong Seed;
            public bool Ended;
            public int Winner = -1;
            public int Rounds;
            public long Ticks;
            public int[] Points;
            public int PeopleTrades, BankTrades, Offers, Refusals, Tr4Checked, Tr4Broken, Sevens, Cities, Settlements, Roads;
        }

        /// <summary>One AI-only game; checks TR-4 on every completed trade between peoples (points tracked in event order).</summary>
        public static GameResult Play(ulong seed, int people = 4, Action<TableSeams> wire = null)
        {
            var sim = Create(people: people, humanSeat: -1, seed: seed, wire: wire);
            var t = T(sim);
            var r = new GameResult { Seed = seed };
            var pts = new int[people];
            for (int p = 0; p < people; p++) pts[p] = TableRules.Points(sim.State, p);
            bool Leading(int q) => pts.All(x => x <= pts[q]);
            for (int i = 0; i < MaxTicks && t.Phase != TablePhase.Ended; i++)
            {
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    switch (e)
                    {
                        case PointsChanged pc: pts[pc.People] = pc.Points; break;
                        case OfferMade _: r.Offers++; break;
                        case TradeRefused _: r.Refusals++; break;
                        case Epidemic _: r.Sevens++; break;
                        case Built b when !b.Setup:
                            if (b.Kind == "city") r.Cities++;
                            else if (b.Kind == "settlement") r.Settlements++;
                            else r.Roads++;
                            break;
                        case Traded tr:
                            if (tr.With < 0) { r.BankTrades++; break; }
                            r.PeopleTrades++;
                            // Both sides are AI seats here: neither may trade with a leading people (TR-4).
                            r.Tr4Checked++;
                            if (Leading(tr.With) || Leading(tr.People)) r.Tr4Broken++;
                            break;
                    }
                }
            }
            r.Ended = t.Phase == TablePhase.Ended;
            r.Winner = t.Winner;
            r.Rounds = t.Round;
            r.Ticks = sim.State.Tick;
            r.Points = Enumerable.Range(0, people).Select(p => TableRules.Points(sim.State, p)).ToArray();
            return r;
        }

        [Test]
        public void AiOnlyGames_ManySeeds_WriteProof_AndObeyTr4()
        {
            var results = new List<GameResult>();
            for (int i = 0; i < Seeds; i++) results.Add(Play(FirstSeed + (ulong)i));
            string report = Report(results);
            TestContext.Out.WriteLine(report);
            string root = RepoRoot();
            if (root != null)
                File.WriteAllText(Path.Combine(root, "Docs", "progress", "G1-2-proof.md"), report.Replace("\r\n", "\n"), new UTF8Encoding(false));

            Assert.IsTrue(results.All(g => g.Ended), "every game reaches 10 points");
            Assert.AreEqual(0, results.Sum(g => g.Tr4Broken), "TR-4 in 100% of trades");
            Assert.Greater(results.Sum(g => g.PeopleTrades), 0, "AIs trade with each other");
        }

        private static string Report(List<GameResult> rs)
        {
            var sb = new StringBuilder();
            var rounds = rs.Where(g => g.Ended).Select(g => g.Rounds).OrderBy(x => x).ToList();
            double Median(List<int> xs) => xs.Count == 0 ? 0 : xs.Count % 2 == 1 ? xs[xs.Count / 2] : (xs[xs.Count / 2 - 1] + xs[xs.Count / 2]) / 2.0;
            int inBand = rounds.Count(x => x >= 25 && x <= 40);
            sb.AppendLine("# G1-2 proof: AI-only god board games");
            sb.AppendLine();
            sb.AppendLine("Written by `TableAiProof.AiOnlyGames_ManySeeds_WriteProof_AndObeyTr4` (EditMode, `dotnet test DotNet --filter \"FullyQualifiedName~TableAiProof\"`).");
            sb.AppendLine("Setup: " + rs.Count + " seeds (" + FirstSeed + " to " + (FirstSeed + (ulong)rs.Count - 1) + "), 4 AI seats on `TableAi`, start corners 70, 97, 57, 82, `BoardGenerator.CreateMvpBoard(seed)` (the dealt board has not landed), no ports, no plague.");
            sb.AppendLine();
            sb.AppendLine("| Measure | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Games that reached 10 points | " + rs.Count(g => g.Ended) + " of " + rs.Count + " |");
            if (rounds.Count > 0)
            {
                sb.AppendLine("| Rounds to 10 points: min / median / mean / max | " + rounds.First() + " / " + Median(rounds) + " / " + rounds.Average().ToString("0.0") + " / " + rounds.Last() + " |");
                sb.AppendLine("| Games in the PRD band (25-40 rounds) | " + inBand + " of " + rounds.Count + " |");
                sb.AppendLine("| Ticks per game: median | " + Median(rs.Where(g => g.Ended).Select(g => (int)g.Ticks).OrderBy(x => x).ToList()) + " |");
            }
            for (int p = 0; p < 4; p++) sb.AppendLine("| Wins, people " + p + " (start corner " + StartCorners[p] + ") | " + rs.Count(g => g.Winner == p) + " |");
            sb.AppendLine("| Trades between peoples (total / per game) | " + rs.Sum(g => g.PeopleTrades) + " / " + (rs.Sum(g => g.PeopleTrades) / (double)rs.Count).ToString("0.0") + " |");
            sb.AppendLine("| Bank and port trades (total / per game) | " + rs.Sum(g => g.BankTrades) + " / " + (rs.Sum(g => g.BankTrades) / (double)rs.Count).ToString("0.0") + " |");
            sb.AppendLine("| Offers made (total) | " + rs.Sum(g => g.Offers) + " |");
            sb.AppendLine("| Leader refusals shown (TradeRefused) | " + rs.Sum(g => g.Refusals) + " |");
            sb.AppendLine("| Trades checked for TR-4 / obeying | " + rs.Sum(g => g.Tr4Checked) + " / " + (rs.Sum(g => g.Tr4Checked) - rs.Sum(g => g.Tr4Broken)) + " (" + (rs.Sum(g => g.Tr4Checked) == 0 ? "n/a" : (100.0 * (rs.Sum(g => g.Tr4Checked) - rs.Sum(g => g.Tr4Broken)) / rs.Sum(g => g.Tr4Checked)).ToString("0.0") + "%") + ") |");
            sb.AppendLine("| Builds per game: roads / settlements / cities | " + (rs.Sum(g => g.Roads) / (double)rs.Count).ToString("0.0") + " / " + (rs.Sum(g => g.Settlements) / (double)rs.Count).ToString("0.0") + " / " + (rs.Sum(g => g.Cities) / (double)rs.Count).ToString("0.0") + " |");
            sb.AppendLine("| Sevens per game | " + (rs.Sum(g => g.Sevens) / (double)rs.Count).ToString("0.0") + " |");
            sb.AppendLine();
            sb.AppendLine("## Per seed");
            sb.AppendLine();
            sb.AppendLine("| Seed | Rounds | Winner | Points | Trades (peoples / bank) |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var g in rs)
                sb.AppendLine("| " + g.Seed + " | " + (g.Ended ? g.Rounds.ToString() : "not ended (" + g.Rounds + ")") + " | " + g.Winner + " | " + string.Join(", ", g.Points) + " | " + g.PeopleTrades + " / " + g.BankTrades + " |");
            return sb.ToString();
        }

        private static string RepoRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                    if (Directory.Exists(Path.Combine(d.FullName, "Docs", "progress")) && Directory.Exists(Path.Combine(d.FullName, "Assets"))) return d.FullName;
            }
            return null;
        }
    }
}
