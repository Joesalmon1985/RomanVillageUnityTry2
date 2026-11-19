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
            /// <summary>Quick-player games only: offers AIs made to the player, trades the player made, rounds it started not leading.</summary>
            public int OffersToPlayer, PlayerTrades, PlayerNotLeadingRounds;
        }

        /// <summary>
        /// One game with seat 0 played by a "Quick" virtual player (PRD 12.1): builds the adviser's target (TableAi.TargetFor)
        /// as soon as it can afford it, accepts every fair offer (at least as many cards in as out), declines the rest, ends
        /// its turn at once. AI seats on the brain. Checks TR-4 from the AI side of every trade.
        /// </summary>
        public static GameResult PlayQuick(ulong seed)
        {
            const int people = 4;
            var sim = Create(people: people, humanSeat: 0, seed: seed);
            var s = sim.State;
            var t = T(sim);
            var r = new GameResult { Seed = seed };
            var pts = new int[people];
            for (int p = 0; p < people; p++) pts[p] = TableRules.Points(s, p);
            bool Leading(int q) => pts.All(x => x <= pts[q]);
            int lastRound = 0;
            Submit(sim, new Sacrifice());
            for (int i = 0; i < MaxTicks && t.Phase != TablePhase.Ended; i++)
            {
                QuickAct(sim);
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    switch (e)
                    {
                        case PointsChanged pc: pts[pc.People] = pc.Points; break;
                        case TurnStarted ts when !ts.Setup && ts.Round != lastRound:
                            lastRound = ts.Round;
                            if (!Leading(0)) r.PlayerNotLeadingRounds++;
                            break;
                        case OfferMade om:
                            r.Offers++;
                            if (om.To == 0) r.OffersToPlayer++;
                            break;
                        case TradeRefused _: r.Refusals++; break;
                        case Epidemic _: r.Sevens++; break;
                        case Traded tr:
                            if (tr.With < 0) { r.BankTrades++; break; }
                            r.PeopleTrades++;
                            r.Tr4Checked++;
                            if (tr.People == 0 || tr.With == 0) r.PlayerTrades++;
                            // Each AI side must not trade with a leading people; the player is not bound by TR-4.
                            if (tr.People != 0 && Leading(tr.With)) r.Tr4Broken++;
                            if (tr.With != 0 && Leading(tr.People)) r.Tr4Broken++;
                            break;
                    }
                }
            }
            r.Ended = t.Phase == TablePhase.Ended;
            r.Winner = t.Winner;
            r.Rounds = t.Round;
            r.Ticks = s.Tick;
            r.Points = Enumerable.Range(0, people).Select(p => TableRules.Points(s, p)).ToArray();
            return r;
        }

        /// <summary>The Quick player's one command this tick, if any.</summary>
        private static void QuickAct(WorldSim sim)
        {
            var s = sim.State;
            var t = s.Table;
            foreach (var o in t.Offers)
            {
                if (o.Status != OfferStatus.Open || o.From == 0 || (o.To >= 0 && o.To != 0) || o.DeclinedBy.Contains(0)) continue;
                bool fair = TableRules.CanPay(t.Peoples[0].Hand, o.Get) && TableRules.HandSize(o.Give) >= TableRules.HandSize(o.Get);
                Submit(sim, new AnswerOffer { OfferId = o.Id, Accept = fair });
                return;
            }
            if (t.Phase == TablePhase.Discard && t.DiscardOwed[0] > 0)
            {
                Submit(sim, new DiscardCards { Cards = TableAi.ChooseDiscard(s, 0, t.DiscardOwed[0]) });
                return;
            }
            if (t.Current != 0) return;
            if (t.Phase == TablePhase.Setup)
            {
                if (t.SetupCorner < 0) Submit(sim, new PlaceTableSettlement { Corner = TableAi.SetupPlacement(s, 0).corner });
                else
                {
                    var legal = TableRules.LegalRoadEdges(s, 0, t.SetupCorner);
                    Submit(sim, new PlaceTableRoad { HexEdge = legal.Count > 0 ? legal[0] : -1 });
                }
                return;
            }
            if (t.Phase != TablePhase.Main) return;
            var target = TableAi.TargetFor(s, 0);
            if (target.Kind != TableTargetKind.None && TableRules.CanPay(t.Peoples[0].Hand, target.Cost))
            {
                Command c = target.Kind == TableTargetKind.City ? new UpgradeTableCity { Corner = target.Spot }
                    : target.Kind == TableTargetKind.Settlement ? new PlaceTableSettlement { Corner = target.Spot }
                    : (Command)new PlaceTableRoad { HexEdge = target.Spot };
                if (Submit(sim, c).Ok) return;
            }
            Submit(sim, new EndTurn());
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
            var quick = new List<GameResult>();
            for (int i = 0; i < Seeds; i++) quick.Add(PlayQuick(FirstSeed + (ulong)i));
            string report = Report(results) + QuickReport(quick);
            TestContext.Out.WriteLine(report);
            string root = RepoRoot();
            if (root != null)
                File.WriteAllText(Path.Combine(root, "Docs", "progress", "G1-2-proof.md"), report.Replace("\r\n", "\n"), new UTF8Encoding(false));

            Assert.IsTrue(results.All(g => g.Ended), "every game reaches 10 points");
            Assert.AreEqual(0, results.Sum(g => g.Tr4Broken), "TR-4 in 100% of trades");
            Assert.Greater(results.Sum(g => g.PeopleTrades), 0, "AIs trade with each other");
            Assert.IsTrue(quick.All(g => g.Ended), "every game with the Quick player ends");
            Assert.AreEqual(0, quick.Sum(g => g.Tr4Broken), "TR-4 with a player seat");
            Assert.Greater(quick.Sum(g => g.OffersToPlayer), 0, "AIs make offers to the player");
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

        private static string QuickReport(List<GameResult> rs)
        {
            var sb = new StringBuilder();
            var rounds = rs.Where(g => g.Ended).Select(g => g.Rounds).OrderBy(x => x).ToList();
            int notLeading = rs.Sum(g => g.PlayerNotLeadingRounds), toPlayer = rs.Sum(g => g.OffersToPlayer);
            int checkedTr = rs.Sum(g => g.Tr4Checked), broken = rs.Sum(g => g.Tr4Broken);
            sb.AppendLine();
            sb.AppendLine("## With a Quick player in seat 0");
            sb.AppendLine();
            sb.AppendLine("Same seeds; seat 0 is a virtual Quick player (PRD 12.1): builds the adviser's target (`TableAi.TargetFor`) when affordable, accepts every offer giving it at least as many cards as it pays, ends its turn at once, makes no offers and no bank trades. A first look for G1-3, not its full proof.");
            sb.AppendLine();
            sb.AppendLine("| Measure | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Games that reached 10 points | " + rs.Count(g => g.Ended) + " of " + rs.Count + " |");
            if (rounds.Count > 0)
                sb.AppendLine("| Rounds to 10 points: min / median / max | " + rounds.First() + " / " + rounds[rounds.Count / 2] + " / " + rounds.Last() + " |");
            sb.AppendLine("| Quick player wins (PRD target 30-50%) | " + rs.Count(g => g.Winner == 0) + " of " + rs.Count + " (" + (100.0 * rs.Count(g => g.Winner == 0) / rs.Count).ToString("0") + "%) |");
            sb.AppendLine("| AI offers to the player / rounds the player started not leading | " + toPlayer + " / " + notLeading + " (" + (notLeading == 0 ? "n/a" : (3.0 * toPlayer / notLeading).ToString("0.00")) + " per 3 rounds; PRD target at least 1) |");
            sb.AppendLine("| Trades the player made with AIs | " + rs.Sum(g => g.PlayerTrades) + " |");
            sb.AppendLine("| Trades between peoples, all (per game) | " + (rs.Sum(g => g.PeopleTrades) / (double)rs.Count).ToString("0.0") + " |");
            sb.AppendLine("| Trades checked for TR-4 (AI sides) / obeying | " + checkedTr + " / " + (checkedTr - broken) + " |");
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
