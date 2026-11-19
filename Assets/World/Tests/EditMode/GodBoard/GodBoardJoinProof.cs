using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using World.Sim.Plague;
using World.Sim.Table;
using World.Sim.War;

namespace World.Sim.Tests
{
    /// <summary>
    /// G0-3 board proof (start of G1-3): all-AI god board games on the dealt board with the plague and the war active,
    /// over many seeds. Writes Docs/progress/G0-3-proof.md (not the shared Docs/*_PROOF.md). Measures what PRD 12.2 asks
    /// for; asserts only the hard rules (TR-4, determinism of bookkeeping), never the tuning targets.
    /// </summary>
    public class GodBoardJoinProof
    {
        public const int Seeds = 40;
        public const ulong FirstSeed = 3000;
        public const int MaxTicks = 120000;

        public sealed class GameResult
        {
            public ulong Seed;
            public string End = "none"; // "won", "island", "cap"
            public int Winner = -1, Rounds, RoundsAtWar, Ticks, TicksAtWar;
            public int[] Points;
            public int PointsBurned, Burned, BurnedCities, Looted, BigFalls;
            public int Trained, Purged, Died, Duels, Outbreaks, Epidemics, BlockedProductions;
            public int PeopleTrades, BankTrades, Tr4Checked, Tr4Broken, Offers, Refusals;
            public int Settlements, Cities, Roads;
            // G1-3: a virtual player on the human seat (Seat -1 = the all-AI table).
            public int Seat = -1;
            public int OffersToPlayer, OffersToPlayerNonLeading, NonLeadingRounds, Banishes;
            public bool PlayerWon;
        }

        /// <summary>One all-AI game. tweak (optional) changes the fresh state before the first tick (proof variants only).</summary>
        public static GameResult Play(ulong seed, Action<WorldState> tweak = null, Func<WorldSim, Action> makeDriver = null, int humanSeat = -1)
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = humanSeat;
            var sim = WorldSimFactory.CreateNew(seed, setup);
            var drive = makeDriver?.Invoke(sim);
            var s = sim.State;
            tweak?.Invoke(s);
            var t = s.Table;
            int n = t.PeopleCount;
            var r = new GameResult { Seed = seed, Seat = humanSeat };
            var pts = new int[n];
            var peak = new int[n];
            for (int p = 0; p < n; p++) pts[p] = peak[p] = TableRules.Points(s, p);
            bool Leading(int q) => pts.All(x => x <= pts[q]);
            int lastRound = -1;
            bool roundWar = false;
            int i = 0;
            for (; i < MaxTicks && t.Phase != TablePhase.Ended; i++)
            {
                drive?.Invoke();
                sim.Step();
                bool war = s.War.AtWar.Any(x => x);
                if (t.Round >= 1)
                {
                    if (t.Round != lastRound)
                    {
                        if (lastRound >= 1 && roundWar) r.RoundsAtWar++;
                        if (lastRound >= 1 && humanSeat >= 0 && !Leading(humanSeat)) r.NonLeadingRounds++;
                        lastRound = t.Round;
                        roundWar = false;
                    }
                    roundWar |= war;
                    r.Ticks++;
                    if (war) r.TicksAtWar++;
                }
                foreach (var e in sim.DrainEvents())
                {
                    switch (e)
                    {
                        case PointsChanged pc:
                            pts[pc.People] = pc.Points;
                            peak[pc.People] = Math.Max(peak[pc.People], pc.Points);
                            if (peak[pc.People] >= 9 && pc.Points < 6) { r.BigFalls++; peak[pc.People] = pc.Points; }
                            break;
                        case SettlementLost sl:
                            r.PointsBurned += sl.WasCity ? 2 : 1;
                            break;
                        case SettlementBurned sb: r.Burned++; if (sb.WasCity) r.BurnedCities++; break;
                        case CampLooted _: r.Looted++; break;
                        case SoldierTrained _: r.Trained++; break;
                        case DemonPurged _: r.Purged++; break;
                        case SoldierDied _: r.Died++; break;
                        case DuelStarted _: r.Duels++; break;
                        case OfferMade om:
                            r.Offers++;
                            if (humanSeat >= 0 && om.From != humanSeat && (om.To == humanSeat || om.To < 0))
                            {
                                r.OffersToPlayer++;
                                if (!Leading(humanSeat)) r.OffersToPlayerNonLeading++;
                            }
                            break;
                        case Banished _: r.Banishes++; break;
                        case TradeRefused _: r.Refusals++; break;
                        case Produced pr: r.BlockedProductions += pr.Blocked.Length; break;
                        case Built b when !b.Setup:
                            if (b.Kind == "city") r.Cities++;
                            else if (b.Kind == "settlement") r.Settlements++;
                            else r.Roads++;
                            break;
                        case Traded tr:
                            if (tr.With < 0) { r.BankTrades++; break; }
                            r.PeopleTrades++;
                            r.Tr4Checked++;
                            if (Leading(tr.With) || Leading(tr.People)) r.Tr4Broken++;
                            break;
                    }
                }
            }
            if (lastRound >= 1 && roundWar) r.RoundsAtWar++;
            r.End = t.IslandFell ? "island" : t.Phase == TablePhase.Ended ? "won" : "cap";
            r.Winner = t.Winner;
            r.PlayerWon = humanSeat >= 0 && t.Winner == humanSeat;
            r.Rounds = t.Round;
            r.Banishes = Math.Max(0, r.Banishes - r.Purged); // the plague raises Banished for soldiers' purges too
            r.Points = Enumerable.Range(0, n).Select(p => TableRules.Points(s, p)).ToArray();
            r.Outbreaks = s.Plague.Outbreaks;
            r.Epidemics = s.Plague.Epidemics;
            return r;
        }

        [Test]
        public void AiOnlyGodBoard_PlagueAndWar_ManySeeds_WriteProof()
        {
            var results = new List<GameResult>();
            for (int k = 0; k < Seeds; k++) results.Add(Play(FirstSeed + (ulong)k));
            var variants = new List<(string name, List<GameResult> rs)>();
            foreach (var (name, tweak) in Variants())
            {
                var rs = new List<GameResult>();
                for (int k = 0; k < Seeds; k++) rs.Add(Play(FirstSeed + (ulong)k, tweak));
                variants.Add((name, rs));
            }
            string report = Report(results) + VariantReport(results, variants);
            TestContext.Out.WriteLine(report);
            string root = RepoRoot();
            if (root != null)
                File.WriteAllText(Path.Combine(root, "Docs", "progress", "G0-3-proof.md"), report.Replace("\r\n", "\n"), new UTF8Encoding(false));

            Assert.AreEqual(0, results.Sum(g => g.Tr4Broken), "TR-4 in 100% of trades");
            foreach (var (name, rs) in variants) Assert.AreEqual(0, rs.Sum(g => g.Tr4Broken), "TR-4 in " + name);
            Assert.IsTrue(results.All(g => g.End != "cap"), "every game ends (a win or the island's fall)");
        }

        /// <summary>
        /// Proof variants (never game rules): what the figures look like without the war, with WA-11 read literally, and
        /// with longer AI turns so a round lasts about as many ticks as in a game with a player (3 AI turns plus one
        /// 45 s hourglass turn, about 600 ticks; the war and the lodges run on ticks, the plague on rounds).
        /// </summary>
        private static IEnumerable<(string, Action<WorldState>)> Variants()
        {
            yield return ("No war (war stopped from the start; plague only, as in G0-2)", s => s.War.Stopped = true);
            yield return ("WA-11 literal tie rule (everyone tied = at war)", s => { s.War.TiedLeadersFight = TieRule.Literal; Recompute(s); });
            yield return ("Player-paced rounds (AiTurnTicks 150: about 600 ticks a round)", s => s.Table.Tuning.AiTurnTicks = 150);
        }

        private static void Recompute(WorldState s)
        {
            var pts = s.Table.Peoples.Select(p => p.Points).ToArray();
            WarRules.SetPoints(s, pts, null);
        }

        private static string VariantReport(List<GameResult> main, List<(string name, List<GameResult> rs)> variants)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Variants (same seeds; proof settings only, no rule changed)");
            sb.AppendLine();
            sb.AppendLine("| Variant | Won / island fell / cap | Median rounds to 10 (won) | Island falls | Rounds at war | Points burned per game: median / max | Trained / purged per game | TR-4 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            void Row(string name, List<GameResult> rs)
            {
                var won = rs.Where(g => g.End == "won").Select(g => g.Rounds).ToList();
                int fell = rs.Count(g => g.End == "island");
                sb.AppendLine("| " + name + " | " + rs.Count(g => g.End == "won") + " / " + fell + " / " + rs.Count(g => g.End == "cap")
                              + " | " + (won.Count == 0 ? "n/a" : Median(won).ToString()) + " | " + Pct(fell, rs.Count)
                              + " | " + Pct(rs.Sum(g => g.RoundsAtWar), rs.Sum(g => g.Rounds))
                              + " | " + Median(rs.Select(g => g.PointsBurned).ToList()) + " / " + rs.Max(g => g.PointsBurned)
                              + " | " + rs.Average(g => g.Trained).ToString("0.0") + " / " + rs.Average(g => g.Purged).ToString("0.0")
                              + " | " + (rs.Sum(g => g.Tr4Checked) - rs.Sum(g => g.Tr4Broken)) + " of " + rs.Sum(g => g.Tr4Checked) + " |");
            }
            Row("Default (above)", main);
            foreach (var (name, rs) in variants) Row(name, rs);
            return sb.ToString();
        }

        private static double Median(List<int> xs)
        {
            if (xs.Count == 0) return double.NaN;
            xs = xs.OrderBy(x => x).ToList();
            return xs.Count % 2 == 1 ? xs[xs.Count / 2] : (xs[xs.Count / 2 - 1] + xs[xs.Count / 2]) / 2.0;
        }

        private static string Pct(int a, int b) => b == 0 ? "n/a" : (100.0 * a / b).ToString("0.0") + "%";

        private static string Report(List<GameResult> rs)
        {
            var sb = new StringBuilder();
            var won = rs.Where(g => g.End == "won").ToList();
            var fell = rs.Where(g => g.End == "island").ToList();
            var rounds = won.Select(g => g.Rounds).ToList();
            int totalRounds = rs.Sum(g => g.Rounds);
            sb.AppendLine("# G0-3 proof: all-AI god board games with the plague and the war");
            sb.AppendLine();
            sb.AppendLine("Written by `GodBoardJoinProof.AiOnlyGodBoard_PlagueAndWar_ManySeeds_WriteProof` (EditMode, `dotnet test DotNet --filter \"FullyQualifiedName~GodBoardJoinProof\"`).");
            sb.AppendLine("Setup: " + rs.Count + " seeds (" + FirstSeed + " to " + (FirstSeed + (ulong)rs.Count - 1) + "), `GameSetup.GodBoard` (dealt board, ports), AI-only table (`TableHumanSeat = -1`, 4 seats on `TableAi`), plague and war wired as in a real game, nobody banishing (no player). War tie rule: `OnlyWhenNotEveryoneTied` (default). Cap " + MaxTicks + " ticks.");
            sb.AppendLine();
            sb.AppendLine("## Against PRD 12.2");
            sb.AppendLine();
            sb.AppendLine("| Measure | Target | Value |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Rounds to 10 points (games won): min / median / max | 25-40 | " + (rounds.Count == 0 ? "n/a" : rounds.Min() + " / " + Median(rounds) + " / " + rounds.Max()) + " |");
            sb.AppendLine("| Won games in the 25-40 band | - | " + rounds.Count(x => x >= 25 && x <= 40) + " of " + rounds.Count + " |");
            sb.AppendLine("| Island falls (8 outbreaks) | under 5% with an army brain active | " + fell.Count + " of " + rs.Count + " (" + Pct(fell.Count, rs.Count) + ") |");
            sb.AppendLine("| Points lost to burning per game: median / max | median under 2 | " + Median(rs.Select(g => g.PointsBurned).ToList()) + " / " + rs.Max(g => g.PointsBurned) + " |");
            sb.AppendLine("| Games where a people fell from 9+ to under 6 more than twice | none | " + rs.Count(g => g.BigFalls > 2) + " (falls in all games: " + rs.Sum(g => g.BigFalls) + ") |");
            sb.AppendLine("| Share of rounds with any war active | under 30% | " + Pct(rs.Sum(g => g.RoundsAtWar), totalRounds) + " (" + rs.Sum(g => g.RoundsAtWar) + " of " + totalRounds + " rounds) |");
            sb.AppendLine("| Share of table ticks with any war active | - | " + Pct(rs.Sum(g => g.TicksAtWar), rs.Sum(g => g.Ticks)) + " |");
            sb.AppendLine("| Trades between peoples obeying TR-4 | 100% | " + (rs.Sum(g => g.Tr4Checked) - rs.Sum(g => g.Tr4Broken)) + " of " + rs.Sum(g => g.Tr4Checked) + " (" + Pct(rs.Sum(g => g.Tr4Checked) - rs.Sum(g => g.Tr4Broken), rs.Sum(g => g.Tr4Checked)) + ") |");
            sb.AppendLine();
            sb.AppendLine("## Other figures (per game, mean)");
            sb.AppendLine();
            double M(Func<GameResult, int> f) => rs.Count == 0 ? 0 : rs.Average(f);
            sb.AppendLine("| Measure | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Games won / island fell / hit the cap | " + won.Count + " / " + fell.Count + " / " + rs.Count(g => g.End == "cap") + " |");
            sb.AppendLine("| Rounds when the island fell: min / median / max | " + (fell.Count == 0 ? "n/a" : fell.Min(g => g.Rounds) + " / " + Median(fell.Select(g => g.Rounds).ToList()) + " / " + fell.Max(g => g.Rounds)) + " |");
            sb.AppendLine("| Wins by people 0 / 1 / 2 / 3 | " + string.Join(" / ", Enumerable.Range(0, 4).Select(p => won.Count(g => g.Winner == p))) + " |");
            sb.AppendLine("| Soldiers trained | " + M(g => g.Trained).ToString("0.0") + " |");
            sb.AppendLine("| Demons purged by soldiers | " + M(g => g.Purged).ToString("0.0") + " |");
            sb.AppendLine("| Duels / soldiers died | " + M(g => g.Duels).ToString("0.0") + " / " + M(g => g.Died).ToString("0.0") + " |");
            sb.AppendLine("| Settlements burned (of them cities) / home camps looted | " + M(g => g.Burned).ToString("0.00") + " (" + M(g => g.BurnedCities).ToString("0.00") + ") / " + M(g => g.Looted).ToString("0.00") + " |");
            sb.AppendLine("| Outbreaks / epidemics | " + M(g => g.Outbreaks).ToString("0.0") + " / " + M(g => g.Epidemics).ToString("0.0") + " |");
            sb.AppendLine("| Blocked hex productions | " + M(g => g.BlockedProductions).ToString("0.0") + " |");
            sb.AppendLine("| Trades between peoples / bank and port | " + M(g => g.PeopleTrades).ToString("0.0") + " / " + M(g => g.BankTrades).ToString("0.0") + " |");
            sb.AppendLine("| Builds: roads / settlements / cities | " + M(g => g.Roads).ToString("0.0") + " / " + M(g => g.Settlements).ToString("0.0") + " / " + M(g => g.Cities).ToString("0.0") + " |");
            sb.AppendLine();
            sb.AppendLine("## Per seed");
            sb.AppendLine();
            sb.AppendLine("| Seed | End | Rounds | Winner | Points | Outbreaks | Rounds at war | Burned (points) | Trained | Purged | Died |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var g in rs)
                sb.AppendLine("| " + g.Seed + " | " + g.End + " | " + g.Rounds + " | " + (g.Winner < 0 ? "-" : g.Winner.ToString()) + " | " + string.Join(", ", g.Points)
                              + " | " + g.Outbreaks + " | " + g.RoundsAtWar + " | " + g.Burned + " (" + g.PointsBurned + ") | " + g.Trained + " | " + g.Purged + " | " + g.Died + " |");
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
