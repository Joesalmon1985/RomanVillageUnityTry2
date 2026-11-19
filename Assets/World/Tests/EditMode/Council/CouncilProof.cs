using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using World.Sim.Encounters.Council;

namespace World.Sim.Tests
{
    /// <summary>
    /// The council balance report: headless seeded elections and listening bursts, written to Docs/COUNCIL_PROOF.md.
    /// Everything here is an automated simulation with scripted players; none of it is a human observation.
    /// Deterministic: built twice and compared byte for byte.
    /// </summary>
    public class CouncilProof
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int AiElections = 2000;
        const int PlayerElections = 1500;
        const int BurstSamples = 4000;
        static readonly CouncilContent C = CouncilContent.BuiltIn;

        [Test, Timeout(1800000), Category("HeavyProof")]
        public void CouncilProof_WritesDocsCouncilProof_AndMeetsEveryCheck()
        {
            var failures = new List<string>();
            string a = Build(failures);
            string b = Build(new List<string>());
            Assert.AreEqual(a, b, "the proof is byte-identical across two runs");
            string root = RepoRoot();
            if (root != null) File.WriteAllText(Path.Combine(root, "Docs", "COUNCIL_PROOF.md"), a.Replace("\r\n", "\n"), new UTF8Encoding(false));
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        static string RepoRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var d = new DirectoryInfo(start);
                while (d != null)
                {
                    if (Directory.Exists(Path.Combine(d.FullName, "Docs")) && Directory.Exists(Path.Combine(d.FullName, "Assets"))) return d.FullName;
                    d = d.Parent;
                }
            }
            return null;
        }

        // ------------------------------------------------------------ simulation

        sealed class Result
        {
            public int Winner, Starter, PlayerSeat, Sittings, Pushes, Busts;
            public int[] SeatSittings = new int[4], SeatPushes = new int[4], SeatBusts = new int[4];
            public string[] Personality = new string[4];
            public bool Earned, Spent;
            public int Opportunities, Skipped, Started, Completed, Abandoned;
            public int PlayerSittingsWithPetition, PlayerPushesWithPetition, PlayerSittingsWithout, PlayerPushesWithout;
            public bool PetitionPresentAtEnd;
        }

        sealed class Listener
        {
            public bool Enabled;
            public double Accuracy;
            public int MedianMs;
            public double ListenChance = 0.7, AbandonChance = 0.1;
            public ListeningTuning Tune = new ListeningTuning();
        }

        static int[] _intentCorrect, _intentTotal;

        static string[] DealPersonalities(ulong seed)
        {
            var ids = new List<string>();
            for (int i = 0; i < 4; i++) ids.Add(C.Personalities[i].Id);
            new Rng(seed).Fork("personalities").Shuffle(ids);
            return ids.ToArray();
        }

        /// <summary>One election. playerPersonality null = all four AI. Otherwise seat 0 is the scripted player.</summary>
        static Result Run(ulong seed, CouncilPersonality player, Listener listener)
        {
            string[] ids;
            ListeningTuning tune = listener != null ? listener.Tune : C.Listening;
            if (player == null) ids = DealPersonalities(seed);
            else
            {
                var rest = new List<string> { "careful", "bold", "closer" };
                new Rng(seed).Fork("rivals").Shuffle(rest);
                ids = new[] { "player", rest[0], rest[1], rest[2] };
            }
            var e = ElectionEngine.Create(seed, player == null ? -1 : 0, ids, tune);
            var res = new Result { Starter = e.Starter, PlayerSeat = e.PlayerSeat };
            Array.Copy(ids, res.Personality, 4);
            var script = new Rng(seed ^ 0x5EED5EEDUL);
            var ev = new List<CouncilEvent>();
            bool inSitting = false, sittingHadPetition = false;
            int sittingPushes = 0;
            int guard = 0;
            while (e.Phase != ElectionPhase.Complete)
            {
                if (guard++ > 20000) throw new InvalidOperationException("election did not end: seed " + seed);
                if (listener != null && listener.Enabled && e.PlayerSeat >= 0 && e.Active != e.PlayerSeat
                    && e.Phase == ElectionPhase.Ready && e.SittingPushes == 0 && !e.PetitionEarned && e.ListenBursts < e.ListeningRules.MaxBursts)
                {
                    res.Opportunities++;
                    if (script.NextInt(1000) < listener.ListenChance * 1000) Listen(e, listener, script, res);
                    else { res.Skipped++; ElectionListening.Skip(e, null); }
                }
                if (e.Active == 0 && player != null && !inSitting) { inSitting = true; sittingHadPetition = e.PetitionAvailable; sittingPushes = 0; }
                var p = e.Active == 0 && player != null ? player : C.Personality(e.Personality[e.Active]);
                int active = e.Active;
                ev.Clear();
                var act = CouncilAi.Decide(e, p);
                var r = ElectionEngine.Apply(e, act, ev);
                if (!r.Ok) throw new InvalidOperationException("scripted action refused: " + r);
                foreach (var x in ev)
                {
                    if (x is CouncilPushMade) { res.SeatPushes[active]++; if (active == 0 && player != null) sittingPushes++; }
                    else if (x is CouncilBust) res.SeatBusts[active]++;
                    else if (x is CouncilTurnPassed || x is CouncilElectionWon)
                    {
                        res.SeatSittings[active]++;
                        if (active == 0 && player != null)
                        {
                            if (sittingHadPetition) { res.PlayerSittingsWithPetition++; res.PlayerPushesWithPetition += sittingPushes; }
                            else { res.PlayerSittingsWithout++; res.PlayerPushesWithout += sittingPushes; }
                            inSitting = false;
                        }
                    }
                }
            }
            res.Winner = e.Winner; res.Sittings = e.Sittings; res.Pushes = e.Pushes; res.Busts = e.Busts;
            res.Earned = e.PetitionEarned; res.Spent = e.PetitionSpent;
            return res;
        }

        static void Listen(ElectionState e, Listener l, Rng script, Result res)
        {
            ElectionListening.Start(e, C.ListeningLines, null);
            res.Started++;
            var s = e.Listening;
            int stopAfter = script.NextInt(1000) < l.AbandonChance * 1000 ? 2 + script.NextInt(3) : 99;
            var ev = new List<CouncilEvent>();
            for (int i = 0; i < s.Answers.Length; i++)
            {
                if (i >= stopAfter) { ElectionListening.Abandon(e, null); res.Abandoned++; return; }
                bool same = ListeningSession.IsSame(s, i);
                bool right = script.NextInt(1000) < l.Accuracy * 1000;
                int choice = (right ? same : !same) ? 0 : 1;
                int ms = (int)(l.MedianMs * (0.6 + 0.9 * script.NextInt(1000) / 1000.0));
                ev.Clear();
                ElectionListening.Answer(e, choice, ms, ev);
                if (_intentTotal != null)
                    foreach (var x in ev.OfType<CouncilListeningAnswered>()) { _intentTotal[x.LineTag]++; if (x.Correct) _intentCorrect[x.LineTag]++; }
            }
            res.Completed++;
        }

        // ------------------------------------------------------------ report helpers

        static string P(double n, double of, int d = 1) => of == 0 ? "-" : (100.0 * n / of).ToString("F" + d, Inv) + "%";
        static string F(double v, int d = 1) => v.ToString("F" + d, Inv);
        static double Mean(IEnumerable<double> v) { var l = v.ToList(); return l.Count == 0 ? 0 : l.Average(); }
        static double Pct(List<int> sorted, double q) => sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];

        static CouncilPersonality Scripted(double tolerance, double boost = 0)
        {
            var p = C.Personality("steady").Copy();
            p.Id = "player"; p.Name = "Scripted player";
            p.BustTolerance = tolerance; p.PetitionBoost = boost;
            p.MinAdvances = 1; p.MaxAdvances = 99; p.Impulse = 0; p.BankAtTopPercent = 100; p.TrailingBoost = 0; p.LeadingCaution = 0;
            return p;
        }

        static string Build(List<string> fail)
        {
            var sb = new StringBuilder();
            void L(string s = "") => sb.Append(s).Append('\n');
            void Check(bool ok, string what) { if (!ok) fail.Add(what); }
            var lt = C.Listening;

            L("# Council proof (generated)");
            L();
            L("Generated by `CouncilProof` (Assets/World/Tests/EditMode/Council/CouncilProof.cs) under `dotnet test DotNet`; do not edit by hand. The same seeds always give this file byte for byte.");
            L();
            L("**These are automated simulations with scripted players. Nothing in this file is a human observation.** Every number says how the rules and the scripted players behave, not how people will play or feel. The human playtest must be reported separately.");
            L();
            L("Setup: the election rules in `ElectionEngine` (four candidates, tracks 2 to 12, three catspaws, three claims to win), the four shipped personalities in `council_content.json` dealt to seats by a seeded shuffle, and the listening rules: " + lt.Responses + " responses, accuracy threshold " + lt.AccuracyThresholdPercent + "%, speed bonus up to " + lt.SpeedBonusPercent + " points per correct answer (full at or under " + lt.FastMs + " ms, none at or over " + lt.SlowMs + " ms), at most " + lt.MaxBursts + " bursts per election, one petition per election. A **sitting** is one candidate's turn; a **push** is one press-on; a **bust** is a push with no legal move.");
            L();

            // ---- A. all AI
            var ai = new List<Result>();
            for (ulong s = 1; s <= AiElections; s++) ai.Add(Run(s, null, null));
            Check(ai.Count == AiElections, "all AI elections ended");
            L("## A. Four AI candidates (" + AiElections + " elections, seeds 1 to " + AiElections + ")");
            L();
            var sit = ai.Select(r => r.Sittings).OrderBy(x => x).ToList();
            var push = ai.Select(r => r.Pushes).OrderBy(x => x).ToList();
            L("| Measure | Mean | Median | 10th pct | 90th pct | Shortest | Longest |");
            L("|---|---|---|---|---|---|---|");
            L("| Sittings per election (all four candidates) | " + F(sit.Average()) + " | " + F(Pct(sit, 0.5), 0) + " | " + F(Pct(sit, 0.1), 0) + " | " + F(Pct(sit, 0.9), 0) + " | " + sit[0] + " | " + sit[sit.Count - 1] + " |");
            L("| Pushes per election | " + F(push.Average()) + " | " + F(Pct(push, 0.5), 0) + " | " + F(Pct(push, 0.1), 0) + " | " + F(Pct(push, 0.9), 0) + " | " + push[0] + " | " + push[push.Count - 1] + " |");
            L();
            L("Pushes per sitting: " + F(ai.Sum(r => r.Pushes) / (double)ai.Sum(r => r.Sittings), 2) + ". Bust rate: " + P(ai.Sum(r => r.Busts), ai.Sum(r => r.Pushes)) + " of pushes, " + P(ai.Sum(r => r.Busts), ai.Sum(r => r.Sittings)) + " of sittings end in a bust.");
            L();
            L("### Win rate and behaviour by personality");
            L();
            L("| Personality | Wins | Win rate | Pushes per sitting | Bust rate per sitting | Bust rate per push |");
            L("|---|---|---|---|---|---|");
            foreach (var p in C.Personalities)
            {
                int wins = 0, sittings = 0, pushes = 0, busts = 0;
                foreach (var r in ai)
                    for (int s = 0; s < 4; s++)
                        if (r.Personality[s] == p.Id)
                        {
                            if (r.Winner == s) wins++;
                            sittings += r.SeatSittings[s]; pushes += r.SeatPushes[s]; busts += r.SeatBusts[s];
                        }
                L("| " + p.Name + " | " + wins + " | " + P(wins, ai.Count) + " | " + F(pushes / (double)sittings, 2) + " | " + P(busts, sittings) + " | " + P(busts, pushes) + " |");
                Check(wins > 0.15 * ai.Count && wins < 0.40 * ai.Count, "personality " + p.Id + " wins " + wins + " of " + ai.Count + ": outside 15% to 40%");
            }
            L();
            L("### Win rate by seat");
            L();
            L("| Seat | Emblem | Wins | Win rate |");
            L("|---|---|---|---|");
            for (int s = 0; s < 4; s++)
            {
                int wins = ai.Count(r => r.Winner == s);
                L("| " + (s + 1) + " | " + C.Emblems[s].Name + " | " + wins + " | " + P(wins, ai.Count) + " |");
                Check(wins > 0.20 * ai.Count && wins < 0.30 * ai.Count, "seat " + s + " wins " + wins + ": outside 20% to 30%");
            }
            L();
            double firstAdv = 100.0 * ai.Count(r => r.Winner == r.Starter) / ai.Count - 100.0 * ai.Count(r => (r.Winner - r.Starter + 4) % 4 == 3) / ai.Count;
            L("By turn position (0 = the candidate chosen to start, then clockwise):");
            L();
            L("| Position | Win rate |");
            L("|---|---|");
            for (int off = 0; off < 4; off++)
                L("| " + off + " | " + P(ai.Count(r => (r.Winner - r.Starter + 4) % 4 == off), ai.Count) + " |");
            L();

            // ---- B. listening bursts alone
            L("## B. Listening bursts on their own (" + BurstSamples + " scripted bursts per row)");
            L();
            L("A scripted listener answers each comparison correctly with a fixed chance and a response time spread from 60% to 150% of a median. \"Earned\" is the chance one burst reaches the threshold. Rows with the speed bonus off use the same answers.");
            L();
            L("| Accuracy per answer | Median ms | Earned with bonus | Earned without bonus | Mean score with bonus |");
            L("|---|---|---|---|---|");
            double lastEarn = -1;
            foreach (var (acc, ms) in new[] { (0.5, 3000), (0.7, 3000), (0.8, 1200), (0.8, 2500), (0.8, 4500), (0.9, 1200), (0.9, 2500), (0.9, 4500), (1.0, 2500), (1.0, 4500) })
            {
                int withB = 0, withoutB = 0; double score = 0;
                for (int i = 0; i < BurstSamples; i++)
                {
                    var rng = new Rng((ulong)(i + 1) * 7919UL + (ulong)(acc * 1000) + (ulong)ms);
                    var tuneOn = lt.Copy();
                    var tuneOff = lt.Copy(); tuneOff.SpeedBonusPercent = 0;
                    var st = ListeningSession.Start(tuneOn, C.ListeningLines, rng.NextULong(), 1);
                    for (int k = 0; k < 5; k++)
                    {
                        bool same = ListeningSession.IsSame(st, k);
                        bool right = rng.NextInt(1000) < acc * 1000;
                        int msv = (int)(ms * (0.6 + 0.9 * rng.NextInt(1000) / 1000.0));
                        ListeningSession.Answer(st, tuneOn, (right ? same : !same) ? 0 : 1, msv, null);
                    }
                    var on = st.Result; var off = ListeningSession.Score(st, tuneOff);
                    if (on.Earned) withB++;
                    if (off.Earned) withoutB++;
                    score += on.EffectivePercent;
                }
                L("| " + (int)(acc * 100) + "% | " + ms + " | " + P(withB, BurstSamples) + " | " + P(withoutB, BurstSamples) + " | " + F(score / BurstSamples) + " |");
                Check(withB >= withoutB, "the speed bonus never lowers the earn rate");
                if (ms == 2500) { Check(withB / (double)BurstSamples >= lastEarn - 0.001, "earn rate rises with accuracy"); lastEarn = withB / (double)BurstSamples; }
            }
            L();

            // ---- C. scripted player
            L("## C. A scripted player advising seat 1 (" + PlayerElections + " elections per row, same seeds in every row)");
            L();
            L("The player candidate stops by a fixed rule: press on while the chance that the next push is a bust is at most the **press-on limit**. The three rivals are the Careful, the Bold and the Closer. Listening rows: at the start of each rival sitting, while no petition is earned and bursts remain, the player listens with a 70% chance (otherwise skips) and abandons a started burst 10% of the time; a petition is spent at the first bust after it is earned. Same seeds mean the same pushes until a petition is spent, so the rows are paired.");
            L();
            var levels = new (string name, Listener l)[]
            {
                ("no listening", null),
                ("listening, sharp (95%, 1200 ms)", new Listener { Enabled = true, Accuracy = 0.95, MedianMs = 1200 }),
                ("listening, fair (80%, 2500 ms)", new Listener { Enabled = true, Accuracy = 0.80, MedianMs = 2500 }),
                ("listening, poor (60%, 4500 ms)", new Listener { Enabled = true, Accuracy = 0.60, MedianMs = 4500 }),
            };
            _intentCorrect = new int[5]; _intentTotal = new int[5];
            L("| Press-on limit | Player behaviour | Win rate | Petition earned | Of earned, spent | Sittings per election | Pushes per player sitting | Player bust rate per sitting | Burst completion | Skipped |");
            L("|---|---|---|---|---|---|---|---|---|---|");
            var baselineWins = new Dictionary<double, int>();
            var rowResults = new Dictionary<string, List<Result>>();
            foreach (double tol in new[] { 8.0, 20.0, 35.0 })
            {
                foreach (var (name, l) in levels)
                {
                    var rs = new List<Result>();
                    for (ulong s = 1; s <= PlayerElections; s++) rs.Add(Run(s, Scripted(tol), l));
                    rowResults[tol + name] = rs;
                    int wins = rs.Count(r => r.Winner == 0);
                    if (l == null) baselineWins[tol] = wins;
                    int earned = rs.Count(r => r.Earned), spent = rs.Count(r => r.Spent);
                    int started = rs.Sum(r => r.Started), completed = rs.Sum(r => r.Completed), opp = rs.Sum(r => r.Opportunities), skipped = rs.Sum(r => r.Skipped);
                    double pps = rs.Sum(r => r.SeatPushes[0]) / (double)rs.Sum(r => r.SeatSittings[0]);
                    L("| " + tol + "% | " + name + " | " + P(wins, rs.Count) + " | " + (l == null ? "-" : P(earned, rs.Count)) + " | " + (l == null || earned == 0 ? "-" : P(spent, earned)) + " | "
                        + F(rs.Average(r => r.Sittings)) + " | " + F(pps, 2) + " | " + P(rs.Sum(r => r.SeatBusts[0]), rs.Sum(r => r.SeatSittings[0])) + " | "
                        + (l == null ? "-" : P(completed, started)) + " | " + (l == null ? "-" : P(skipped, opp)) + " |");
                }
            }
            L();
            L("A fair coin among four candidates would win 25% of elections; the sample's noise is about plus or minus 2.2 points at 95% confidence, so differences smaller than that are not meaningful.");
            L();

            // ---- D. does the petition change win rate or press-on behaviour
            L("### Does having the petition change the win rate or press-on behaviour?");
            L();
            double effectMax = double.MinValue;
            L("| Press-on limit | Petition effect on win rate (sharp listener vs none) | Pushes per sitting while a petition is held | Pushes per sitting without one |");
            L("|---|---|---|---|");
            foreach (double tol in new[] { 8.0, 20.0, 35.0 })
            {
                var rs = rowResults[tol + levels[1].name];
                int wins = rs.Count(r => r.Winner == 0);
                double diff = 100.0 * (wins - baselineWins[tol]) / rs.Count;
                effectMax = Math.Max(effectMax, diff);
                int sw = rs.Sum(r => r.PlayerSittingsWithPetition), pw = rs.Sum(r => r.PlayerPushesWithPetition);
                int so = rs.Sum(r => r.PlayerSittingsWithout), po = rs.Sum(r => r.PlayerPushesWithout);
                L("| " + tol + "% | " + (diff >= 0 ? "+" : "") + F(diff) + " points | " + (sw == 0 ? "-" : F(pw / (double)sw, 2)) + " | " + (so == 0 ? "-" : F(po / (double)so, 2)) + " |");
            }
            L();
            L("Pushes per sitting while holding a petition is partly a matter of when in the election the petition is held (earlier sittings come first), so that column is not a clean measure of changed behaviour; the +10 rows below assume the change instead.");
            L();
            L("The scripted player does not change its rule when it holds a petition. To see what a player who presses on more would gain, the same player is repeated with a press-on limit raised by 10 points while holding one (sharp listener):");
            L();
            L("| Base limit | Win rate, rule unchanged | Win rate, +10 points while holding | Pushes per sitting with petition, unchanged | with +10 |");
            L("|---|---|---|---|---|");
            foreach (double tol in new[] { 8.0, 20.0 })
            {
                var plain = rowResults[tol + levels[1].name];
                var boosted = new List<Result>();
                for (ulong s = 1; s <= PlayerElections; s++) boosted.Add(Run(s, Scripted(tol, 10), levels[1].l));
                double a1 = plain.Sum(r => r.PlayerPushesWithPetition) / (double)Math.Max(1, plain.Sum(r => r.PlayerSittingsWithPetition));
                double a2 = boosted.Sum(r => r.PlayerPushesWithPetition) / (double)Math.Max(1, boosted.Sum(r => r.PlayerSittingsWithPetition));
                L("| " + tol + "% | " + P(plain.Count(r => r.Winner == 0), plain.Count) + " | " + P(boosted.Count(r => r.Winner == 0), boosted.Count) + " | " + F(a1, 2) + " | " + F(a2, 2) + " |");
            }
            L();

            L("### Accuracy by intention (scripted listeners, all rows above)");
            L();
            L("| Intention | Answers judged | Correct |");
            L("|---|---|---|");
            for (int i = 0; i < 5; i++) L("| " + (Intention)i + " | " + _intentTotal[i] + " | " + P(_intentCorrect[i], _intentTotal[i]) + " |");
            L();
            L("The scripted listener errs at the same rate on every intention, so this table only shows that every intention is asked about in similar numbers. Whether some pairs (for example Reassure and Encourage) are confused more often than others is a question for human players.");
            L();
            _intentCorrect = null; _intentTotal = null;

            L("## Known weaknesses");
            L();
            L("- **No humans.** Scripted listeners have a fixed chance of being right and a response time drawn at random; real response times, reading speed, fatigue and learning are not modelled, so the earn rates are a range, not a prediction. The threshold, the bonus and the burst length should be tuned from the human playtest.");
            L("- **Scripted decisions.** The player stops by a single exact bust-odds rule; people stop by feel, often too early or too late, and choose trays by what they can read on the table rather than by a score. Win rates of the scripted player say little about a person's.");
            L("- **First-mover advantage.** The candidate who starts wins " + F(firstAdv) + " points more often than the one who plays last. The start is a fair seeded choice and seats rotate, so no seat is favoured over many games, but a single election starts tilted. The brief fixes the rule; a possible later tweak is a small head start for the last candidate.");
            L("- **Four fixed temperaments.** The rivals use four hand-tuned personalities; their win rates are close, not equal, and were tuned only by these simulations.");
            L("- **The petition is worth most to players who press on.** In these runs it moves a risk-taking scripted player's win rate by up to " + F(effectMax) + " points and barely moves a cautious one's, because it cancels one bust. If that is too strong for a side game, make it harder to earn (higher threshold, fewer bursts) rather than weaker; the rule that the sitting's progress is kept is fixed by the brief. Whether people press on more because they hold one is only visible in the \"+10 points while holding\" rows, which assume the answer.");
            L("- **Listening opportunities.** The simulation lets the player listen at the start of every rival sitting. In the game the player must look left in time, rivals' turns are fast, and a burst may be cut short; the real number of opportunities will be lower.");
            L("- **Same seeds, paired runs.** Rows share seeds, which makes differences cleaner but also makes the rows correlated; they are not independent samples.");
            L("- **Election length is in sittings and pushes, not minutes.** Real duration depends on animation and reading time, which the simulation does not have.");
            L();
            return sb.ToString();
        }
    }
}
