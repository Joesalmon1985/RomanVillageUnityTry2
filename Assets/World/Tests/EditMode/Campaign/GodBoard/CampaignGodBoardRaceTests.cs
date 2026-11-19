using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using World.Sim.Campaign;

namespace World.Sim.Tests
{
    /// <summary>
    /// G2-3, CC-2 and PRD_GODBOARD 12.2 "Campaign": the race on the god board. The campaign starts in the first minute of a
    /// god board game (four peoples, dealt board, table and plague) and the Ash clock runs on world time. A direct player
    /// (decides at once) must reach at least 2 crystals first; a reluctant one (says "Not that way" to every lead of a
    /// crystal still believed on its pedestal) none, as today. Whole campaign: the race-sized skeleton of the campaign proof
    /// (<see cref="ProofStarts.RaceSkeleton"/>, the authored pack's tuning and march times); Earth slice: the authored pack.
    /// The measured table is written to the test output (not to Docs/).
    /// </summary>
    public class CampaignGodBoardRaceTests
    {
        private static readonly ulong[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private static readonly Element[] Four = { Element.Earth, Element.Fire, Element.Water, Element.Air };
        private const long WholeLimit = 150 * 600, EarthLimit = 40 * 600;

        private static ProofPolicy Direct() => new ProofPolicy { Name = "direct" };
        private static ProofPolicy Reluctant() => new ProofPolicy { Name = "reluctant", Reluctant = true };
        /// <summary>
        /// Plays the board in between: away from Mira (at the god board) from the start, back a minute after she starts to
        /// wait for them (a tower, a held threshold), prompt while there, and back to the board as soon as she sets out.
        /// </summary>
        private static ProofPolicy Board() => new ProofPolicy { Name = "board", Wander = true, Delay = 600 };

        private static string Outcome(ProofRun r, Element e)
        {
            var ch = r.Chain(e);
            return ch.Contains(Custody.RivalTransit) || ch.Contains(Custody.AtRivalAltar) ? "stolen" : ch.Contains(Custody.Carried) ? "first" : "-";
        }

        private sealed class Row { public string Player; public ulong Seed; public int First, Stolen; public string Ending, Opens; public long Ticks; public List<string> Failures; }

        private static Row Whole(ProofPolicy pol, ulong seed)
        {
            var r = ProofStarts.StartGodBoard(PackFiles.Skeleton, seed, pol, d => ProofStarts.RaceSkeleton(d, CampaignProofReport.SkeletonPad));
            long start = r.S.Tick;
            r.Run(x => x.C.Phase != CampaignPhase.Running, WholeLimit);
            return new Row
            {
                Player = pol.Name, Seed = seed, Ticks = r.S.Tick - start,
                First = Four.Count(e => Outcome(r, e) == "first"), Stolen = Four.Count(e => Outcome(r, e) == "stolen"),
                Ending = r.C.Phase.ToString(), Opens = r.C.RivalPlan?[0]?.Crystal ?? "", Failures = r.Failures,
            };
        }

        private static Row Earth(ProofPolicy pol, ulong seed)
        {
            var r = ProofStarts.StartGodBoard(PackFiles.Authored, seed, pol);
            long start = r.S.Tick;
            r.Run(x => x.C.Crystal(Element.Earth).Custody == Custody.Restored || x.C.Phase != CampaignPhase.Running, EarthLimit);
            string o = Outcome(r, Element.Earth);
            return new Row { Player = pol.Name, Seed = seed, Ticks = r.S.Tick - start, First = o == "first" ? 1 : 0, Stolen = o == "stolen" ? 1 : 0, Ending = o, Failures = r.Failures };
        }

        private static List<Row> RunAll(Func<ProofPolicy, ulong, Row> run)
        {
            var cases = new List<(Func<ProofPolicy> p, ulong s)>();
            foreach (var p in new Func<ProofPolicy>[] { Direct, Reluctant, Board }) foreach (var s in Seeds) cases.Add((p, s));
            var rows = new Row[cases.Count];
            Parallel.For(0, cases.Count, i => rows[i] = run(cases[i].p(), cases[i].s));
            return rows.ToList();
        }

        private static void Print(string title, List<Row> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine("| Player | Seed | Opens | First | Stolen | Ending | Minutes |");
            foreach (var w in rows)
                sb.AppendLine("| " + w.Player + " | " + w.Seed + " | " + w.Opens + " | " + w.First + " | " + w.Stolen + " | " + w.Ending + " | " + (w.Ticks / 600.0).ToString("0.0") + " |");
            TestContext.Out.WriteLine(sb.ToString());
        }

        [Test]
        public void WholeCampaign_DirectReachesTwoFirst_ReluctantNone()
        {
            var rows = RunAll(Whole);
            Print("God board, whole campaign (race-sized skeleton)", rows);
            foreach (var w in rows) Assert.IsEmpty(w.Failures, w.Player + " seed " + w.Seed + ": " + string.Join("; ", w.Failures));
            foreach (var w in rows.Where(x => x.Player == "direct"))
                Assert.GreaterOrEqual(w.First, 2, "direct seed " + w.Seed + " reaches at least 2 crystals first");
            foreach (var w in rows.Where(x => x.Player == "reluctant"))
                Assert.AreEqual(0, w.First, "reluctant seed " + w.Seed + " reaches none first");
        }

        [Test]
        public void EarthSlice_DirectFirst_ReluctantStolen()
        {
            var rows = RunAll(Earth);
            Print("God board, Earth slice (authored pack)", rows);
            foreach (var w in rows) Assert.IsEmpty(w.Failures, w.Player + " seed " + w.Seed + ": " + string.Join("; ", w.Failures));
            Assert.GreaterOrEqual(rows.Count(x => x.Player == "direct" && x.First == 1), 9, "the direct party reaches the Deepstone first on most seeds");
            Assert.AreEqual(0, rows.Count(x => x.Player == "reluctant" && x.First == 1), "the reluctant party never does");
        }
    }
}
