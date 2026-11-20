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
    /// <summary>The virtual players of PRD_GODBOARD 12.1 (G1-3).</summary>
    public enum VirtualKind
    {
        /// <summary>Acts at once, accepts every fair offer, ends the turn as soon as it has nothing to do, banishes demons on its own hexes.</summary>
        Quick = 0,
        /// <summary>Acts about every 3.5 s and ends its turn only when most of the hourglass is gone.</summary>
        Slow = 1,
        /// <summary>Opens the board and places its setup pieces, then never acts (the hourglass ends every turn).</summary>
        Absent = 2,
        /// <summary>The human seat played by the table AI (same brain as the AI seats, AI pacing, no banishing).</summary>
        AiSeat = 3,
        /// <summary>As AiSeat, and banishing like Quick (a proof extra, not one of the four of 12.1).</summary>
        AiSeatBanish = 4,
    }

    /// <summary>
    /// A virtual player: a driver called once before every tick. It sends commands through <c>WorldSim.Submit</c> like the real
    /// player would, one action per tick, so it never touches the engine behind the checks.
    /// </summary>
    public sealed class VirtualPlayer
    {
        private readonly VirtualKind _kind;
        private readonly WorldSim _sim;
        private readonly int _seat;
        private long _lastAct = -1000;
        private int _turnKey = -1, _turnActions;

        public VirtualPlayer(VirtualKind kind, WorldSim sim, int seat)
        {
            _kind = kind;
            _sim = sim;
            _seat = seat;
        }

        public static Func<WorldSim, Action> Factory(VirtualKind kind, int seat) => sim => new VirtualPlayer(kind, sim, seat).Tick;

        private bool Banishes => _kind == VirtualKind.Quick || _kind == VirtualKind.AiSeatBanish;

        private bool Send(Command c)
        {
            c.PlayerId = _seat;
            return _sim.Submit(c).Ok;
        }

        public void Tick()
        {
            var s = _sim.State;
            var t = s.Table;
            if (t == null || t.Phase == TablePhase.Ended || _sim.PendingCommands.Count > 0) return;

            if (t.Phase == TablePhase.NotOpened) { Send(new Sacrifice()); return; }
            if (Banishes && TryBanish(s)) return;

            if (t.Phase == TablePhase.Setup)
            {
                if (t.Current != _seat) return;
                if (t.SetupCorner < 0)
                {
                    var (corner, _) = TableAi.SetupPlacement(s, _seat);
                    if (TableRules.SettlementRefusal(s, _seat, corner, false) != null) (corner, _) = TableDefaults.SetupPlacement(s, _seat);
                    Send(new PlaceTableSettlement { Corner = corner });
                }
                else
                {
                    var (_, edge) = TableAi.SetupPlacement(s, _seat);
                    if (edge < 0 || TableRules.RoadRefusal(s, _seat, edge, t.SetupCorner) != null)
                    {
                        var legal = TableRules.LegalRoadEdges(s, _seat, t.SetupCorner);
                        edge = legal.Count > 0 ? legal[0] : -1;
                    }
                    Send(new PlaceTableRoad { HexEdge = edge });
                }
                return;
            }

            if (_kind == VirtualKind.Absent) return;

            if (t.Phase == TablePhase.Discard)
            {
                if (_kind != VirtualKind.Slow && t.DiscardOwed[_seat] > 0)
                    Send(new DiscardCards { Cards = TableAi.ChooseDiscard(s, _seat, t.DiscardOwed[_seat]) });
                return;
            }
            if (t.Phase != TablePhase.Main) return;

            if (AnswerOffers(s, t)) return;
            if (t.Current != _seat) return;

            int key = t.Round * 16 + t.TurnIndex;
            if (key != _turnKey) { _turnKey = key; _turnActions = 0; }
            foreach (var o in t.Offers) if (o.Status == OfferStatus.Open && o.From == _seat) return; // waits for the answer

            if (_kind == VirtualKind.Slow && s.Tick - _lastAct < 35) { MaybeEnd(t); return; }
            bool submitted = false;
            var actor = new TableActor(c =>
            {
                if (submitted) return CommandResult.Reject("budget", "one action a tick");
                c.PlayerId = _seat;
                var r = _sim.Submit(c);
                if (r.Ok) submitted = true;
                return r;
            }, _seat);
            TableAi.PlayTurn(s, _seat, actor);
            if (submitted)
            {
                _lastAct = s.Tick;
                _turnActions++;
                return;
            }
            MaybeEnd(t);
        }

        private void MaybeEnd(TableState t)
        {
            switch (_kind)
            {
                case VirtualKind.Quick:
                    Send(new EndTurn());
                    break;
                case VirtualKind.AiSeat:
                case VirtualKind.AiSeatBanish:
                    if (t.TurnTicks >= t.Tuning.AiTurnTicks + t.Tuning.AiActionTicks * _turnActions) Send(new EndTurn());
                    break;
                case VirtualKind.Slow:
                    if (t.Sand <= t.Tuning.TurnBaseTicks / 5) Send(new EndTurn()); // most of the hourglass is used
                    break;
            }
        }

        private bool AnswerOffers(WorldState s, TableState t)
        {
            foreach (var o in t.Offers)
            {
                if (o.Status != OfferStatus.Open || o.From == _seat || (o.To >= 0 && o.To != _seat) || o.DeclinedBy.Contains(_seat)) continue;
                if (_kind == VirtualKind.Slow && o.Age < 90) continue;
                bool yes;
                if (_kind == VirtualKind.AiSeat || _kind == VirtualKind.AiSeatBanish) yes = TableAi.Accepts(s, _seat, o);
                else yes = TableRules.CanPay(t.Peoples[_seat].Hand, o.Get) && TableAi.Fair(s, _seat, o.Get, o.Give); // every fair offer
                if (Send(new AnswerOffer { OfferId = o.Id, Accept = yes })) return true;
                if (yes && Send(new AnswerOffer { OfferId = o.Id, Accept = false })) return true;
            }
            return false;
        }

        /// <summary>Banishes a demon on a hex beside one of its own settlements (the node with the most demons; ties: lowest id).</summary>
        private bool TryBanish(WorldState s)
        {
            if (!PlagueApi.Active(s) || PlagueApi.BanishCooldownLeft(s) > 0 || PlagueApi.TotalDemons(s) == 0) return false;
            var t = s.Table;
            var topo = s.Board.Topology;
            int best = -1, bestDemons = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                if (t.CornerOwner[c] != _seat) continue;
                foreach (int h in topo.VertexHexes(c))
                    foreach (int node in PlagueApi.NodesOfHex(s, h))
                    {
                        int d = PlagueApi.DemonsAt(s, node);
                        if (d > bestDemons || (d == bestDemons && d > 0 && node < best)) { best = node; bestDemons = d; }
                    }
            }
            return best >= 0 && Send(new Banish { Node = best });
        }
    }

    /// <summary>Named tuning settings of the proof (never rules): applied for one game, always restored afterwards.</summary>
    public sealed class Knobs
    {
        public string Name = "Default (balance F1: infect every 3rd round, tied leaders never fight)";
        public int Grace = 6;
        public int[] Rates = { 1, 1, 2, 2, 2, 3, 3, 3, 4 };
        public int InfectEvery = PlagueRules.DefaultInfectEvery, RateEvery = 1, WinPoints = 10, WarLead = 2, Soldiers = 1, EpidemicDemons = 3, OutbreaksToFall = 8;
        public TieRule Tie = TieRule.NeverWhenTied;

        /// <summary>The rules as they stood before balance F1 (infect every round, tied leaders fight unless everyone is tied): the study's baseline.</summary>
        public static Knobs BeforeF1() => new Knobs { Name = "Before F1 (infect every round, ties fight unless everyone is tied)", InfectEvery = 1, Tie = TieRule.OnlyWhenNotEveryoneTied };

        public Knobs Named(string name) { Name = name; return this; }
        public Knobs With(Action<Knobs> f) { f(this); return this; }

        public Knobs Clone() => new Knobs
        {
            Name = Name, Grace = Grace, Rates = (int[])Rates.Clone(), InfectEvery = InfectEvery, RateEvery = RateEvery, WinPoints = WinPoints, WarLead = WarLead,
            Soldiers = Soldiers, EpidemicDemons = EpidemicDemons, OutbreaksToFall = OutbreaksToFall, Tie = Tie,
        };

        public GodBoardJoinProof.GameResult Play(ulong seed, VirtualKind? kind, int seat)
        {
            var oldGrace = PlagueRules.GraceRounds;
            var oldRates = PlagueRules.RateTrack;
            var oldRateEvery = PlagueRules.RateEvery;
            var oldLead = WarData.WarLead;
            var oldSoldiers = WarData.SettlementSoldiers;
            var oldEpi = PlagueRules.EpidemicDemons;
            var oldFall = PlagueRules.OutbreaksToFall;
            try
            {
                PlagueRules.GraceRounds = Grace;
                PlagueRules.RateTrack = Rates;
                PlagueRules.RateEvery = RateEvery;
                WarData.WarLead = WarLead;
                WarData.SettlementSoldiers = Soldiers;
                PlagueRules.EpidemicDemons = EpidemicDemons;
                PlagueRules.OutbreaksToFall = OutbreaksToFall;
                return GodBoardJoinProof.Play(seed, s =>
                {
                    s.Table.Tuning.WinPoints = WinPoints;
                    s.Plague.InfectEvery = InfectEvery;
                    if (s.War.TiedLeadersFight != Tie)
                    {
                        s.War.TiedLeadersFight = Tie;
                        WarRules.SetPoints(s, s.Table.Peoples.Select(p => p.Points).ToArray(), null);
                    }
                }, kind.HasValue ? VirtualPlayer.Factory(kind.Value, seat) : null, kind.HasValue ? seat : -1);
            }
            finally
            {
                PlagueRules.GraceRounds = oldGrace;
                PlagueRules.RateTrack = oldRates;
                PlagueRules.RateEvery = oldRateEvery;
                WarData.WarLead = oldLead;
                WarData.SettlementSoldiers = oldSoldiers;
                PlagueRules.EpidemicDemons = oldEpi;
                PlagueRules.OutbreaksToFall = oldFall;
            }
        }
    }

    /// <summary>
    /// G1-3 board proof with virtual players (PRD 12.1, 12.2) and the tuning options study. Writes Docs/progress/G1-3-proof.md
    /// and Docs/progress/G1-3-tuning.md. EditMode tests assert harness sanity only (determinism, every game ends, TR-4), never the targets.
    /// </summary>
    public class GodBoardVirtualProof
    {
        public const int Seeds = 48;
        public const ulong FirstSeed = 3000;

        /// <summary>The player's seat rotates with the seed so no start corner or turn position favours it.</summary>
        public static int SeatOf(ulong seed) => (int)(seed % 4);

        public static List<GodBoardJoinProof.GameResult> Run(Knobs knobs, VirtualKind? kind, int seeds = Seeds)
        {
            var list = new List<GodBoardJoinProof.GameResult>();
            for (int k = 0; k < seeds; k++)
            {
                ulong seed = FirstSeed + (ulong)k;
                list.Add(knobs.Play(seed, kind, SeatOf(seed)));
            }
            return list;
        }

        // =====================================================================================================
        // Tests
        // =====================================================================================================

        [Test]
        public void VirtualPlayers_SameSeedSameGame_AndEveryGameEnds()
        {
            var knobs = new Knobs();
            foreach (VirtualKind kind in Enum.GetValues(typeof(VirtualKind)))
            {
                var a = knobs.Play(3001, kind, 1);
                var b = knobs.Play(3001, kind, 1);
                Assert.AreEqual(Fingerprint(a), Fingerprint(b), kind + ": same seed, same game");
                Assert.AreNotEqual("cap", a.End, kind + ": the game ends (a win or the island's fall)");
            }
        }

        [Test]
        public void KnobsAreRestoredAfterEveryGame()
        {
            var k = new Knobs { Grace = 10, Rates = new[] { 1, 1, 1, 2 }, InfectEvery = 2, WarLead = 3, Soldiers = 2, EpidemicDemons = 2, OutbreaksToFall = 9, WinPoints = 12 };
            k.Play(3002, VirtualKind.Quick, 2);
            Assert.AreEqual(6, PlagueRules.GraceRounds);
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 2, 3, 3, 3, 4 }, PlagueRules.RateTrack);
                        Assert.AreEqual(1, PlagueRules.RateEvery);
            Assert.AreEqual(2, WarData.WarLead);
            Assert.AreEqual(1, WarData.SettlementSoldiers);
            Assert.AreEqual(3, PlagueRules.EpidemicDemons);
            Assert.AreEqual(8, PlagueRules.OutbreaksToFall);
        }

        [Test]
        public void VirtualPlayers_Behave_AsDescribed()
        {
            var k = new Knobs();
            var quick = k.Play(3001, VirtualKind.Quick, 1);
            var slow = k.Play(3001, VirtualKind.Slow, 1);
            var absent = k.Play(3001, VirtualKind.Absent, 1);
            Assert.IsTrue(quick.Points != null && slow.Points != null && absent.Points != null);
            Assert.Greater(quick.Banishes, 0, "Quick banishes");
            Assert.AreEqual(0, slow.Banishes, "Slow does not");
            Assert.AreEqual(0, absent.Banishes, "Absent does not");
        }

        [Test]
        public void VirtualPlayers_ManySeeds_WriteProof()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var runs = new List<(VirtualKind kind, List<GodBoardJoinProof.GameResult> rs)>();
            foreach (VirtualKind kind in Enum.GetValues(typeof(VirtualKind))) runs.Add((kind, Run(new Knobs(), kind)));
            var allAi = Run(new Knobs(), null);
            string report = ProofReport(runs, allAi, sw.Elapsed.TotalSeconds);
            TestContext.Out.WriteLine(report);
            Write("G1-3-proof.md", report);
            foreach (var (kind, rs) in runs)
            {
                Assert.AreEqual(0, rs.Sum(g => g.Tr4Broken), "TR-4 for " + kind);
                Assert.IsTrue(rs.All(g => g.End != "cap"), "every " + kind + " game ends");
            }
        }

        [Test]
        public void TuningStudy_WriteOptions()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rows = new List<(Knobs k, List<GodBoardJoinProof.GameResult> ai)>();
            foreach (var k in StudyVariants()) rows.Add((k, Run(k, VirtualKind.AiSeat)));
            var fin = new List<(Knobs k, List<(VirtualKind kind, List<GodBoardJoinProof.GameResult> rs)> runs)>();
            foreach (var k in Finalists())
                fin.Add((k, new[] { VirtualKind.Quick, VirtualKind.Slow, VirtualKind.Absent, VirtualKind.AiSeat }.Select(kind => (kind, Run(k, kind))).ToList()));
            string report = StudyReport(rows, sw.Elapsed.TotalSeconds) + FinalistReport(fin);
            TestContext.Out.WriteLine(report);
            // Balance F1: G1-3-tuning.md is the study as it was written before F1 (plus a hand-added "after F1" section); it is no longer regenerated.
            foreach (var (k, rs) in rows)
            {
                Assert.AreEqual(0, rs.Sum(g => g.Tr4Broken), "TR-4 in " + k.Name);
                Assert.IsTrue(rs.All(g => g.End != "cap"), "every game ends in " + k.Name);
            }
        }

        private static string Fingerprint(GodBoardJoinProof.GameResult g) =>
            g.End + "|" + g.Winner + "|" + g.Rounds + "|" + g.Ticks + "|" + string.Join(",", g.Points) + "|" + g.PeopleTrades + "|" + g.BankTrades
            + "|" + g.Outbreaks + "|" + g.Banishes + "|" + g.Trained + "|" + g.Burned + "|" + g.OffersToPlayer;

        // =====================================================================================================
        // Variants of the study
        // =====================================================================================================

        /// <summary>The option sets run against every virtual player in the study's second table.</summary>
        public static IEnumerable<Knobs> Finalists()
        {
            yield return Knobs.BeforeF1();
            yield return new Knobs().Named("F1 infect every 3rd round + tied leaders never fight").With(k => { k.InfectEvery = 3; k.Tie = TieRule.NeverWhenTied; });
            yield return Knobs.BeforeF1().Named("F3 F1 + win 13").With(k => { k.InfectEvery = 3; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; });
            yield return Knobs.BeforeF1().Named("D5 F3 + war lead 3").With(k => { k.InfectEvery = 3; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("D8 infect every 2nd + grace 10 + win 13 + ties never + lead 3").With(k => { k.Grace = 10; k.InfectEvery = 2; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
        }

        private static readonly int[] SlowRates = { 1, 1, 1, 2, 2, 2, 3, 3, 4 };

        public static IEnumerable<Knobs> StudyVariants()
        {
            yield return Knobs.BeforeF1();
            // Single changes.
            yield return Knobs.BeforeF1().Named("S1 plague grace 6 to 10 rounds").With(k => k.Grace = 10);
            yield return Knobs.BeforeF1().Named("S2 rate track 1,1,1,2,2,2,3,3,4").With(k => k.Rates = SlowRates);
            yield return Knobs.BeforeF1().Named("S3 infection every 2nd round").With(k => k.InfectEvery = 2);
            yield return Knobs.BeforeF1().Named("S4 win at 12 points").With(k => k.WinPoints = 12);
            yield return Knobs.BeforeF1().Named("S5 win at 13 points").With(k => k.WinPoints = 13);
            yield return Knobs.BeforeF1().Named("S6 war lead 3").With(k => k.WarLead = 3);
            yield return Knobs.BeforeF1().Named("S7 two soldiers per settlement").With(k => k.Soldiers = 2);
            yield return Knobs.BeforeF1().Named("S8 WA-11 literal tie rule").With(k => k.Tie = TieRule.Literal);
            yield return Knobs.BeforeF1().Named("S9 epidemic places 2 demons").With(k => k.EpidemicDemons = 2);
            yield return Knobs.BeforeF1().Named("S10 island falls at 10 outbreaks").With(k => k.OutbreaksToFall = 10);
            yield return Knobs.BeforeF1().Named("S11 rate track advances every 2nd epidemic").With(k => k.RateEvery = 2);
            yield return Knobs.BeforeF1().Named("S13 infection every 3rd round").With(k => k.InfectEvery = 3);
            yield return Knobs.BeforeF1().Named("S14 war lead 4").With(k => k.WarLead = 4);
            yield return Knobs.BeforeF1().Named("S15 tied leaders never fight (new tie value)").With(k => k.Tie = TieRule.NeverWhenTied);
            yield return Knobs.BeforeF1().Named("S16 win at 14 points").With(k => k.WinPoints = 14);
            // Combinations.
            yield return Knobs.BeforeF1().Named("D1 S13 + win 12").With(k => { k.InfectEvery = 3; k.WinPoints = 12; });
            yield return Knobs.BeforeF1().Named("D2 S13 + win 13").With(k => { k.InfectEvery = 3; k.WinPoints = 13; });
            yield return Knobs.BeforeF1().Named("D3 S13 + win 14").With(k => { k.InfectEvery = 3; k.WinPoints = 14; });
            yield return Knobs.BeforeF1().Named("D4 S13 + win 13 + S15 (ties never fight)").With(k => { k.InfectEvery = 3; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; });
            yield return Knobs.BeforeF1().Named("D5 S13 + win 13 + S15 + lead 3").With(k => { k.InfectEvery = 3; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("D6 S13 + win 14 + S15 + lead 3").With(k => { k.InfectEvery = 3; k.WinPoints = 14; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("D7 every 2nd round + win 13 + S15 + lead 3").With(k => { k.InfectEvery = 2; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("D8 D7 + grace 10").With(k => { k.Grace = 10; k.InfectEvery = 2; k.WinPoints = 13; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("D9 S15 + lead 3 only (war change alone)").With(k => { k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("F1 S13 + S15 (infect every 3rd round, tied leaders never fight)").With(k => { k.InfectEvery = 3; k.Tie = TieRule.NeverWhenTied; });
            yield return Knobs.BeforeF1().Named("F2 F1 + win 12").With(k => { k.InfectEvery = 3; k.WinPoints = 12; k.Tie = TieRule.NeverWhenTied; });
            yield return Knobs.BeforeF1().Named("E1 D6 + grace 10").With(k => { k.Grace = 10; k.InfectEvery = 3; k.WinPoints = 14; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("E2 S13 + win 14 + S15 (war lead stays 2)").With(k => { k.InfectEvery = 3; k.WinPoints = 14; k.Tie = TieRule.NeverWhenTied; });
            yield return Knobs.BeforeF1().Named("E3 every 2nd round + grace 10 + win 14 + S15 + lead 3").With(k => { k.Grace = 10; k.InfectEvery = 2; k.WinPoints = 14; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("E4 S13 + win 15 + S15 + lead 3").With(k => { k.InfectEvery = 3; k.WinPoints = 15; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
            yield return Knobs.BeforeF1().Named("E5 every 2nd round + rate every 2nd epidemic + grace 10 + win 14 + S15 + lead 3").With(k => { k.Grace = 10; k.InfectEvery = 2; k.RateEvery = 2; k.WinPoints = 14; k.Tie = TieRule.NeverWhenTied; k.WarLead = 3; });
        }

        // =====================================================================================================
        // Reports
        // =====================================================================================================

        private static double Median(List<int> xs)
        {
            if (xs.Count == 0) return double.NaN;
            xs = xs.OrderBy(x => x).ToList();
            return xs.Count % 2 == 1 ? xs[xs.Count / 2] : (xs[xs.Count / 2 - 1] + xs[xs.Count / 2]) / 2.0;
        }

        private static string Pct(int a, int b) => b == 0 ? "n/a" : (100.0 * a / b).ToString("0.0") + "%";
        private static string Pct(double a) => a.ToString("0.0") + "%";

        private static string RoundsWon(List<GodBoardJoinProof.GameResult> rs)
        {
            var won = rs.Where(g => g.End == "won").Select(g => g.Rounds).ToList();
            return won.Count == 0 ? "n/a (0 won)" : Median(won) + " (" + won.Min() + "-" + won.Max() + ", " + won.Count + " won)";
        }

        private static string Verdict(bool ok) => ok ? " **met**" : " missed";

        private static string ProofReport(List<(VirtualKind kind, List<GodBoardJoinProof.GameResult> rs)> runs, List<GodBoardJoinProof.GameResult> allAi, double seconds)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# G1-3 proof: the god board with virtual players");
            sb.AppendLine();
            sb.AppendLine("Written by `GodBoardVirtualProof.VirtualPlayers_ManySeeds_WriteProof` (EditMode, `dotnet test DotNet --filter \"FullyQualifiedName~GodBoardVirtualProof\"`).");
            sb.AppendLine("Setup: " + Seeds + " seeds (" + FirstSeed + " to " + (FirstSeed + Seeds - 1) + ") per player, `GameSetup.GodBoard` (dealt board, ports), plague and war live, all rule defaults unchanged (tie rule `OnlyWhenNotEveryoneTied`, grace 6, rate track 1,1,2,2,2,3,3,3,4, win at 10, war lead 2, one garrison soldier). The player's seat rotates with the seed (seed mod 4), the other three seats are `TableAi`. Cap 120000 ticks. Whole run: " + seconds.ToString("0") + " s.");
            sb.AppendLine();
            sb.AppendLine("Players (one command a tick through `WorldSim.Submit`, never past the engine's checks):");
            sb.AppendLine("- **Quick**: acts at once, accepts every fair offer (TR-6 fairness, TR-4 leader rule not applied), ends its turn as soon as nothing is left to do, discards at once, banishes the demon node (most demons first) on a hex beside its own settlements whenever the cooldown allows.");
            sb.AppendLine("- **Slow**: one action about every 3.5 s, answers offers after 9 s, ends its turn only when under a fifth of the base sand is left (so it uses most of every hourglass), no banishing.");
            sb.AppendLine("- **Absent**: sacrifices to open the board, places its setup pieces (the setup round has no hourglass; with the AI's choice), then never acts: every turn ends on the hourglass, offers to it expire, discards are automatic.");
            sb.AppendLine("- **AI seat**: the human seat played by `TableAi.PlayTurn` and `TableAi.Accepts`, with the AI's own turn length (30 ticks plus 10 an action), no banishing.");
            sb.AppendLine("- **AI seat + banish** (extra): the AI seat that also banishes like Quick.");
            sb.AppendLine("- **All-AI table** (reference, `TableHumanSeat = -1`): the G0-3 proof's setting on these seeds.");
            sb.AppendLine();

            var cols = runs.Select(r => r.kind).ToList();
            sb.AppendLine("## Against PRD 12.2");
            sb.AppendLine();
            sb.AppendLine("| Measure | Target | " + string.Join(" | ", cols.Select(c => c.ToString())) + " | All-AI table |");
            sb.AppendLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", cols.Count + 1)));
            void Row(string measure, string target, Func<List<GodBoardJoinProof.GameResult>, string> f)
                => sb.AppendLine("| " + measure + " | " + target + " | " + string.Join(" | ", runs.Select(r => f(r.rs))) + " | " + f(allAi) + " |");
            Row("Rounds to 10 points (won games): median (min-max)", "25-40 (AI seat)", RoundsWon);
            Row("Won games in the 25-40 band", "-", rs => rs.Count(g => g.End == "won" && g.Rounds >= 25 && g.Rounds <= 40) + " of " + rs.Count(g => g.End == "won"));
            Row("Games won by the player's seat", "Quick 30-50%; Absent under 5% (25% is even)", rs => rs.Any(g => g.Seat < 0) ? "-" : Pct(rs.Count(g => g.PlayerWon), rs.Count));
            Row("Island falls (8 outbreaks)", "5-20% with no banishing; under 5% with an army brain", rs => rs.Count(g => g.End == "island") + " of " + rs.Count + " (" + Pct(rs.Count(g => g.End == "island"), rs.Count) + ")");
            Row("Points lost to burning per game: median / max", "median under 2", rs => Median(rs.Select(g => g.PointsBurned).ToList()) + " / " + rs.Max(g => g.PointsBurned));
            Row("Games with a fall from 9+ to under 6 more than twice", "none", rs => rs.Count(g => g.BigFalls > 2) + " (falls in all games: " + rs.Sum(g => g.BigFalls) + ")");
            Row("Share of rounds with a war active", "under 30%", rs => Pct(rs.Sum(g => g.RoundsAtWar), rs.Sum(g => g.Rounds)));
            Row("Trades between AI seats obeying TR-4", "100%", rs => (rs.Sum(g => g.Tr4Checked) - rs.Sum(g => g.Tr4Broken)) + " of " + rs.Sum(g => g.Tr4Checked));
            Row("Trades with the player (of them with a leading people at the table)", "- (the player's own choice; the engine closes AI offers to a leading player and AI seats refuse a leading player)", rs => rs.Any(g => g.Seat < 0) ? "-" : rs.Sum(g => g.PlayerTrades) + " (" + rs.Sum(g => g.PlayerLeaderTrades) + ")");
            Row("AI-to-player offers per round while the player is not leading", "at least 1 in 3 (0.33)", rs =>
            {
                int rounds = rs.Sum(g => g.NonLeadingRounds);
                return rs.Any(g => g.Seat < 0) || rounds == 0 ? "-" : ((double)rs.Sum(g => g.OffersToPlayerNonLeading) / rounds).ToString("0.00") + " (" + rs.Sum(g => g.OffersToPlayerNonLeading) + " in " + rounds + " rounds)";
            });
            sb.AppendLine();
            sb.AppendLine("Targets not measured by this proof: **wares ledger balance and card accounting** (covered by the ledger and table EditMode tests, not re-run per game here); **determinism** (the harness test `VirtualPlayers_SameSeedSameGame_AndEveryGameEnds` runs each virtual player twice on one seed and compares the outcome; save and load at every step is in the G0-3 save tests); **campaign** (see below).");
            sb.AppendLine();
            sb.AppendLine("**Campaign-free note:** these games are run without the crystal campaign. No crystal was played, restored, or granted, so the 12.2 campaign target (a direct player reaches at least 2 crystals first, a reluctant one 0, with the board played in between) is not measured here; the plague had no cure (CC-3) and no free grants (G2-3, CC-4) changed the board.");
            sb.AppendLine();

            sb.AppendLine("## Other figures (per game, mean)");
            sb.AppendLine();
            sb.AppendLine("| Measure | " + string.Join(" | ", cols.Select(c => c.ToString())) + " | All-AI table |");
            sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", cols.Count + 1)));
            void Mean(string measure, Func<GodBoardJoinProof.GameResult, double> f, string fmt = "0.0")
                => sb.AppendLine("| " + measure + " | " + string.Join(" | ", runs.Select(r => r.rs.Average(f).ToString(fmt))) + " | " + allAi.Average(f).ToString(fmt) + " |");
            Mean("Rounds played (all games)", g => g.Rounds);
            Mean("Table ticks per round", g => g.Rounds == 0 ? 0 : (double)g.Ticks / g.Rounds, "0");
            Mean("Banishes by the player", g => g.Banishes);
            Mean("AI offers to the player", g => g.OffersToPlayer);
            Mean("Soldiers trained / demons purged (soldiers)", g => g.Trained);
            Mean("Demons purged by soldiers", g => g.Purged);
            Mean("Settlements burned", g => g.Burned, "0.00");
            Mean("Trades between peoples", g => g.PeopleTrades);
            Mean("Bank and port trades", g => g.BankTrades);
            Mean("Outbreaks at the end", g => g.Outbreaks);
            sb.AppendLine();
            sb.AppendLine("The row \"Soldiers trained / demons purged\" shows trained soldiers (the next row the purges).");
            sb.AppendLine();
            sb.AppendLine("## Games by outcome");
            sb.AppendLine();
            sb.AppendLine("| Player | Won (any seat) / island fell / cap | Player wins | Player's final points: median | Rounds when the island fell: median |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var (kind, rs) in runs)
            {
                var fell = rs.Where(g => g.End == "island").Select(g => g.Rounds).ToList();
                sb.AppendLine("| " + kind + " | " + rs.Count(g => g.End == "won") + " / " + rs.Count(g => g.End == "island") + " / " + rs.Count(g => g.End == "cap")
                              + " | " + rs.Count(g => g.PlayerWon) + " of " + rs.Count + " | " + Median(rs.Select(g => g.Points[g.Seat]).ToList())
                              + " | " + (fell.Count == 0 ? "n/a" : Median(fell).ToString()) + " |");
            }
            return sb.ToString();
        }

        private static string StudyReport(List<(Knobs k, List<GodBoardJoinProof.GameResult> ai)> rows, double seconds)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# G1-3 tuning options study (proof settings only; no rule default changed)");
            sb.AppendLine();
            sb.AppendLine("Written by `GodBoardVirtualProof.TuningStudy_WriteOptions`. Every row is the **AI seat** (the human seat played by the table AI, seat rotating with the seed) with three `TableAi` peoples, " + Seeds + " seeds (" + FirstSeed + " to " + (FirstSeed + Seeds - 1) + "), dealt board, plague and war live, no banishing. Variants are set only inside the proof through `Knobs` (static knobs in `PlagueRules`, `WarData`, and `TableTuning.WinPoints` on the fresh state), restored after every game. Run time " + seconds.ToString("0") + " s.");
            sb.AppendLine();
            sb.AppendLine("Targets: island falls under about 10%, rounds to win 25-40, war active under about 30% of rounds.");
            sb.AppendLine();
            sb.AppendLine("| Variant | Won / fell | Rounds to win: median (min-max) | Won in 25-40 | Island falls | Rounds at war | Burned points median / max | Trained / purged | Player wins |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var (k, rs) in rows)
            {
                int fell = rs.Count(g => g.End == "island");
                double war = rs.Sum(g => g.Rounds) == 0 ? 0 : 100.0 * rs.Sum(g => g.RoundsAtWar) / rs.Sum(g => g.Rounds);
                sb.AppendLine("| " + k.Name + " | " + rs.Count(g => g.End == "won") + " / " + fell + " | " + RoundsWon(rs)
                              + " | " + rs.Count(g => g.End == "won" && g.Rounds >= 25 && g.Rounds <= 40) + " of " + rs.Count(g => g.End == "won")
                              + " | " + Pct(fell, rs.Count) + (100.0 * fell / rs.Count <= 10 ? " ok" : "")
                              + " | " + Pct(war) + (war < 30 ? " ok" : "")
                              + " | " + Median(rs.Select(g => g.PointsBurned).ToList()) + " / " + rs.Max(g => g.PointsBurned)
                              + " | " + rs.Average(g => g.Trained).ToString("0.0") + " / " + rs.Average(g => g.Purged).ToString("0.0")
                              + " | " + Pct(rs.Count(g => g.PlayerWon), rs.Count) + " |");
            }
            return sb.ToString();
        }

        private static string FinalistReport(List<(Knobs k, List<(VirtualKind kind, List<GodBoardJoinProof.GameResult> rs)> runs)> fin)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Finalists against every virtual player (same seeds, " + Seeds + " games each)");
            sb.AppendLine();
            sb.AppendLine("Cells: rounds to win (median) / island falls / war share of rounds / player wins. Targets: rounds 25-40 (AI seat), falls under 10%, war under 30%, Quick wins 30-50%, Absent under 5%.");
            sb.AppendLine();
            sb.AppendLine("| Option set | Quick | Slow | Absent | AI seat |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var (k, runs) in fin)
            {
                sb.Append("| " + k.Name + " |");
                foreach (var (kind, rs) in runs)
                {
                    int fell = rs.Count(g => g.End == "island");
                    sb.Append(" " + Median(rs.Where(g => g.End == "won").Select(g => g.Rounds).ToList()) + " / " + Pct(fell, rs.Count) + " / "
                              + Pct(rs.Sum(g => g.RoundsAtWar), rs.Sum(g => g.Rounds)) + " / " + Pct(rs.Count(g => g.PlayerWon), rs.Count) + " |");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void Write(string file, string text)
        {
            string root = RepoRoot();
            if (root != null) File.WriteAllText(Path.Combine(root, "Docs", "progress", file), text.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        private static string RepoRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                    if (Directory.Exists(Path.Combine(d.FullName, "Docs", "progress")) && Directory.Exists(Path.Combine(d.FullName, "Assets"))) return d.FullName;
            return null;
        }
    }
}
