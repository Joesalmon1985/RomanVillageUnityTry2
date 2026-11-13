using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Sim.Tests
{
    /// <summary>
    /// The economy proof (REFACTOR_SPEC 8.1): plays scripted games, writes Docs/ECONOMY_PROOF.md and checks every pass
    /// criterion. The report is built twice and must be byte-identical. Games run in parallel; each is deterministic
    /// and results are kept in input order, so the text does not depend on scheduling.
    /// </summary>
    public class ProofReport
    {
        public static readonly ulong[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        public static readonly ulong[] SensitivitySeeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        public static readonly double[] SensitivityDelays = { 10, 20, 30, 45, 60, 90 };
        public const ulong PairSeed = 3;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string M(long ticks) => ticks < 0 ? "-" : (ticks / 600.0).ToString("0.0", Inv);
        private static string F1(double v) => v.ToString("0.0", Inv);
        private static string Pct(int a, int b) => b <= 0 ? "0%" : (100.0 * a / b).ToString("0.0", Inv) + "%";

        [Test]
        public void Proof_WritesEconomyProof_AndMeetsTheCriteria()
        {
            var failures = new List<string>();
            string a = Build(failures);
            string b = Build(new List<string>());
            Assert.AreEqual(a, b, "the proof is byte-identical across two runs of the same seeds");
            string root = RepoRoot();
            if (root != null) File.WriteAllText(Path.Combine(root, "Docs", "ECONOMY_PROOF.md"), a.Replace("\r\n", "\n"), new UTF8Encoding(false));
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static string RepoRoot()
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

        private struct Spec
        {
            public ulong Seed;
            public PlayerStyle Style;
            public double Delay;
            public Action<WorldSim> Setup;
            public bool Check;
        }

        private static GameResult[] RunAll(IList<Spec> specs)
        {
            var results = new GameResult[specs.Count];
            Parallel.For(0, specs.Count, i =>
            {
                var sp = specs[i];
                results[i] = GameRunner.Play(sp.Seed, sp.Style, sp.Delay, setup: sp.Setup, checkInvariants: sp.Check);
            });
            return results;
        }

        public static string Build(List<string> failures)
        {
            var sb = new StringBuilder();
            var styles = new[] { PlayerStyle.Prompt, PlayerStyle.Slow, PlayerStyle.Decliner, PlayerStyle.Ignore };
            var specs = new List<Spec>();
            foreach (var st in styles) foreach (var seed in Seeds) specs.Add(new Spec { Seed = seed, Style = st, Check = true });
            foreach (var d in SensitivityDelays) foreach (var seed in SensitivitySeeds) specs.Add(new Spec { Seed = seed, Style = PlayerStyle.FixedDelay, Delay = d });
            foreach (var seed in SensitivitySeeds) specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Prompt });
            foreach (var seed in SensitivitySeeds) specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Ignore });
            foreach (var seed in Seeds) specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Random, Check = true });
            var all = RunAll(specs);
            int k = 0;
            var byStyle = new Dictionary<PlayerStyle, GameResult[]>();
            foreach (var st in styles) { byStyle[st] = all.Skip(k).Take(Seeds.Length).ToArray(); k += Seeds.Length; }
            var sens = new List<(string name, GameResult[] games)>();
            foreach (var d in SensitivityDelays)
            {
                sens.Add((ScriptedPlayer.Name(PlayerStyle.FixedDelay, d), all.Skip(k).Take(SensitivitySeeds.Length).ToArray()));
                k += SensitivitySeeds.Length;
            }
            sens.Insert(0, ("prompt (modelled)", all.Skip(k).Take(SensitivitySeeds.Length).ToArray()));
            k += SensitivitySeeds.Length;
            sens.Add(("ignore", all.Skip(k).Take(SensitivitySeeds.Length).ToArray()));
            k += SensitivitySeeds.Length;
            var random = all.Skip(k).Take(Seeds.Length).ToArray();
            var prompt = byStyle[PlayerStyle.Prompt];

            var s0 = WorldSimFactory.CreateNew(Seeds[0]).State;
            sb.Append("# Economy proof (generated)\n\n");
            sb.Append("Generated by `ProofReport` (Assets/World/Tests/EditMode/ProofReport.cs) under `dotnet test DotNet`; do not edit by hand. ");
            sb.Append("The same seeds always give this file byte for byte. Rules: REFACTOR_SPEC sections 2 to 6 with the tuned values below ");
            sb.Append("(tuning log in PROGRESS.md). Times are game minutes (10 ticks = 1 s). Scripted players: **prompt** casts the top proposal ");
            sb.Append("after 5 s talk + the walk at 12.5 m/s from its last cast target + 5 s; **slow** casts 90 s after a proposal becomes the pitch; ");
            sb.Append("**decliner** declines each new pitch once (after 5 s) and casts the next the prompt way; **ignore** never answers; ");
            sb.Append("**delay N s** casts N seconds after the pitch; **random** casts a random open proposal every 5 to 45 s.\n\n");
            Tuning(sb, s0);

            SectionA(sb, prompt, failures);
            SectionB(sb, failures);
            SectionC(sb, failures);
            SectionD(sb, random, failures);
            SectionE(sb, byStyle, sens, failures);
            SectionE2(sb, sens, failures);
            SectionE3(sb, sens, failures);
            SectionF(sb, prompt, byStyle, failures);
            SectionG(sb, byStyle, sens);
            sb.Append("## Verdict\n\n");
            sb.Append(failures.Count == 0 ? "All pass criteria hold.\n" : "Failing criteria:\n\n" + string.Join("\n", failures.Select(f => "- " + f)) + "\n");
            return sb.ToString();
        }

        // ---- tuning ----

        private static void Tuning(StringBuilder sb, WorldState s)
        {
            var e = s.Economy.Tuning;
            var f = s.Factions.Tuning;
            var d = s.Demons.Tuning;
            var spec = new EconomyTuning();
            var fspec = new FactionTuning();
            var dspec = new DemonTuning();
            sb.Append("## Tuned values (differences from the spec's starting values)\n\n| Value | Spec | Used |\n|---|---|---|\n");
            void Row(string n, object a, object b) { if (!Equals(a, b)) sb.Append("| ").Append(n).Append(" | ").Append(a).Append(" | ").Append(b).Append(" |\n"); }
            Row("CarrierTicksPerStep", spec.CarrierTicksPerStep, e.CarrierTicksPerStep);
            Row("StartCarriers", spec.StartCarriers, e.StartCarriers);
            Row("NewSettlementCarriers", spec.NewSettlementCarriers, e.NewSettlementCarriers);
            Row("Starting Sheep", spec.StartStock[(int)Ware.Sheep], e.StartStock[(int)Ware.Sheep]);
            Row("HexCap", spec.HexCap, e.HexCap);
            Row("JobAgeTicks (new)", 0, e.JobAgeTicks);
            Row("RivalCastDelayTicks", fspec.RivalCastDelayTicks, f.RivalCastDelayTicks);
            Row("CityScore", fspec.CityScore, f.CityScore);
            Row("LodgeScore", fspec.LodgeScore, f.LodgeScore);
            Row("BanishEdgeScore", fspec.BanishEdgeScore, f.BanishEdgeScore);
            Row("WorkshopSurplusPerUnit (new)", fspec.WorkshopSurplusPerUnit, f.WorkshopSurplusPerUnit);
            Row("WorkshopSurplusCap (new)", fspec.WorkshopSurplusCap, f.WorkshopSurplusCap);
            Row("RivalPacePerCast (new)", fspec.RivalPacePerCast, f.RivalPacePerCast);
            Row("RivalSettleTicks (new)", "-", f.RivalSettleTicks);
            Row("RivalBoldTicksPerLead (new)", fspec.RivalBoldTicksPerLead, f.RivalBoldTicksPerLead);
            Row("RivalMinDelayTicks (new)", fspec.RivalMinDelayTicks, f.RivalMinDelayTicks);
            Row("WarningGraceTicks (new)", fspec.WarningGraceTicks, f.WarningGraceTicks);
            Row("DemonHeldCampsCount (new)", fspec.DemonHeldCampsCount, f.DemonHeldCampsCount);
            Row("WorkshopStageBonus (new, from 2 camps)", fspec.WorkshopStageBonus, f.WorkshopStageBonus);
            Row("CollectorStageBonus (new, from 4 camps)", fspec.CollectorStageBonus, f.CollectorStageBonus);
            Row("LodgeWithoutCity (new)", fspec.LodgeWithoutCity, f.LodgeWithoutCity);
            Row("ClusterBonus (new, within ClusterMetres)", fspec.ClusterBonus + " / " + fspec.ClusterMetres + " m", f.ClusterBonus + " / " + f.ClusterMetres + " m");
            Row("PressAdvantageBonus (new)", fspec.PressAdvantageBonus, f.PressAdvantageBonus);
            Row("Demon GraceTicks", dspec.GraceTicks, d.GraceTicks);
            Row("Demon SevensPerDraw (new)", dspec.SevensPerDraw, d.SevensPerDraw);
            Row("Demon DrawPerPeople (new)", dspec.DrawPerPeople, d.DrawPerPeople);
            sb.Append("\n");
        }

        // ---- A ----

        private static void SectionA(StringBuilder sb, GameResult[] prompt, List<string> failures)
        {
            sb.Append("## A. Ware audit\n\n| Ware | Made by | Used by | Collected + produced (prompt games) | Consumed (prompt games) |\n|---|---|---|---|---|\n");
            var tu = new EconomyTuning();
            foreach (var w in Wares.All)
            {
                var makers = new List<string>();
                var users = new List<string>();
                foreach (var def in BuildingDefs.All)
                {
                    if (def.Outputs[(int)w] > 0) makers.Add(def.Name);
                    if (def.Inputs[(int)w] > 0) users.Add(def.Name + " (input)");
                    if (def.Cost[(int)w] > 0) users.Add(def.Name);
                }
                if (tu.RoadCost[(int)w] > 0) users.Add("main road");
                if (tu.SettlementCost[(int)w] > 0) users.Add("new camp");
                if (tu.CityCost[(int)w] > 0) users.Add("city");
                if (w == Ware.Bread || w == Ware.Meat) users.Add("warrior meals");
                int made = prompt.Sum(r => r.Collected[(int)w] + r.Produced[(int)w]);
                int used = prompt.Sum(r => r.Consumed[(int)w]);
                sb.Append("| ").Append(Wares.Title(w)).Append(" | ").Append(string.Join(", ", makers)).Append(" | ")
                    .Append(string.Join(", ", users)).Append(" | ").Append(made).Append(" | ").Append(used).Append(" |\n");
                if (makers.Count == 0) failures.Add("A: " + w + " has no producer");
                if (users.Count == 0) failures.Add("A: " + w + " has no consumer");
            }
            sb.Append("\n| Building | Kind | Purpose | Prompt games that built it |\n|---|---|---|---|\n");
            foreach (var def in BuildingDefs.All)
            {
                int n = prompt.Count(r => r.Built[0].Contains(def.Type));
                sb.Append("| ").Append(def.Name).Append(" | ").Append(def.Class).Append(" | ").Append(def.Purpose).Append(" | ")
                    .Append(def.Placeable ? n + " of " + prompt.Length : "comes with a camp").Append(" |\n");
                if (def.Placeable && n == 0) failures.Add("A: " + def.Name + " is built in no prompt game");
            }
            sb.Append("\nPass: every ware has a producer and a consumer; every placeable type is built in at least one prompt game.\n\n");
        }

        // ---- B ----

        private static readonly (Ware ware, Terrain terrain)[] RawChains =
        {
            (Ware.Wood, Terrain.Forest), (Ware.Clay, Terrain.Hills), (Ware.Grain, Terrain.Fields), (Ware.Sheep, Terrain.Pasture), (Ware.Stone, Terrain.Mountain),
        };

        private static void SectionB(StringBuilder sb, List<string> failures)
        {
            sb.Append("## B. Traced chains (one per raw ware, roll to final use)\n\n");
            sb.Append("Each chain follows one unit of stock of the player's people through the events of a prompt game: the roll, the batch, ");
            sb.Append("maturing, the collector's cycle, the carrier to the storehouse, the trip (or shipment) to a consumer and the consumption. ");
            sb.Append("Units are interchangeable, so each step takes the first matching event after the previous one.\n\n");
            var found = new Dictionary<Ware, List<string>>();
            foreach (var seed in Seeds)
            {
                if (found.Count == RawChains.Length) break;
                var events = new List<SimEvent>();
                WorldState state = null;
                GameRunner.Play(seed, PlayerStyle.Prompt, onEvents: (sim, ev) => { state = sim.State; events.AddRange(ev); });
                foreach (var (ware, terrain) in RawChains)
                {
                    if (found.ContainsKey(ware)) continue;
                    var chain = Trace(state, events, ware, terrain);
                    if (chain != null) { chain.Insert(0, "Seed " + seed + "."); found[ware] = chain; }
                }
            }
            foreach (var (ware, _) in RawChains)
            {
                sb.Append("**").Append(Wares.Title(ware)).Append("**\n\n");
                if (!found.TryGetValue(ware, out var chain))
                {
                    sb.Append("No complete chain found.\n\n");
                    failures.Add("B: no complete chain for " + ware);
                    continue;
                }
                foreach (var line in chain) sb.Append("- ").Append(line).Append("\n");
                sb.Append("\n");
            }
            sb.Append("Pass: five complete chains, each ending in a consumption.\n\n");
        }

        private static List<string> Trace(WorldState s, List<SimEvent> ev, Ware ware, Terrain terrain)
        {
            var b = s.Board;
            var e = s.Economy;
            for (int i = 0; i < ev.Count; i++)
            {
                if (!(ev[i] is StockAdded add) || add.Ware != ware) continue;
                // A collector of the player's people on that hex.
                var roll = ev.Take(i + 1).OfType<DiceRolled>().LastOrDefault();
                int matured = Find(ev, i, x => x is StockMatured m && m.Hex == add.Hex && m.Tick >= add.MatureTick);
                if (matured < 0) continue;
                int start = Find(ev, matured, x => x is CollectStarted c && c.Hex == add.Hex && e.Buildings[c.BuildingId].Faction == 0);
                if (start < 0) continue;
                var cs = (CollectStarted)ev[start];
                int done = Find(ev, start, x => x is Collected c && c.BuildingId == cs.BuildingId);
                if (done < 0) continue;
                var col = (Collected)ev[done];
                int outJob = Find(ev, done, x => x is JobCreated j && j.FromKind == PlaceKind.Building && j.FromId == cs.BuildingId);
                if (outJob < 0) continue;
                var oj = (JobCreated)ev[outJob];
                int home = Find(ev, outJob, x => x is WareDelivered d && d.JobId == oj.JobId);
                if (home < 0) continue;
                var hd = (WareDelivered)ev[home];
                int settlement = hd.ToId;
                // From that storehouse (possibly via a shipment) to a consumer.
                int use = Find(ev, home, x => x is JobCreated j && j.Ware == ware && j.FromKind == PlaceKind.Storehouse && j.FromId == settlement
                    && !(j.ToKind == PlaceKind.Storehouse));
                var lines = new List<string>
                {
                    T(roll) + "roll " + roll.D1 + "+" + roll.D2 + " = " + roll.Sum + ".",
                    T(add) + TerrainInfo.DisplayName(terrain) + " " + b.HexNumber[add.Hex] + " (hex " + add.Hex + ") +" + add.Count + " young " + Wares.DisplayName(ware) + ", mature at " + M(add.MatureTick) + ".",
                    T(ev[matured]) + "the batch matures.",
                    T(cs) + BuildingDefs.Get(e.Buildings[cs.BuildingId].Type).Name + " (building " + cs.BuildingId + ") takes one mature unit.",
                    T(col) + "it yields " + col.Amount + " " + Wares.DisplayName(ware) + ".",
                    T(oj) + "job " + oj.JobId + ": a carrier fetches it.",
                    T(hd) + "delivered to the storehouse of " + EconomyApi.SettlementName(s, settlement) + ".",
                };
                if (use < 0)
                {
                    int ship = Find(ev, home, x => x is JobCreated j && j.Ware == ware && j.IsShipment && j.FromId == settlement);
                    if (ship < 0) continue;
                    var sj = (JobCreated)ev[ship];
                    int arrived = Find(ev, ship, x => x is WareDelivered d && d.JobId == sj.JobId);
                    if (arrived < 0) continue;
                    lines.Add(T(sj) + "shipment job " + sj.JobId + ": " + sj.Count + " " + Wares.DisplayName(ware) + " to " + EconomyApi.SettlementName(s, sj.ToId) + ".");
                    lines.Add(T(ev[arrived]) + "the shipment arrives.");
                    settlement = sj.ToId;
                    use = Find(ev, arrived, x => x is JobCreated j && j.Ware == ware && j.FromKind == PlaceKind.Storehouse && j.FromId == settlement && j.ToKind != PlaceKind.Storehouse);
                    if (use < 0) continue;
                }
                var uj = (JobCreated)ev[use];
                int got = Find(ev, use, x => x is WareDelivered d && d.JobId == uj.JobId);
                if (got < 0) continue;
                int consumed = Consumption(ev, got, uj);
                if (consumed < 0) continue;
                lines.Add(T(uj) + "job " + uj.JobId + ": " + Wares.DisplayName(ware) + " for " + Place(s, uj.ToKind, uj.ToId) + ".");
                lines.Add(T(ev[got]) + "delivered there.");
                lines.Add(T(ev[consumed]) + Consumed(s, ev[consumed]) + ".");
                return lines;
            }
            return null;
        }

        private static string T(SimEvent e) => "t " + e.Tick + " (" + M(e.Tick) + " min): ";

        private static int Find(List<SimEvent> ev, int after, Func<SimEvent, bool> p)
        {
            for (int i = after + 1; i < ev.Count; i++) if (p(ev[i])) return i;
            return -1;
        }

        private static int Consumption(List<SimEvent> ev, int after, JobCreated j)
        {
            switch (j.ToKind)
            {
                case PlaceKind.Building:
                    return Find(ev, after, x => (x is SiteCompleted c && c.Kind == SiteKind.Building && c.Id == j.ToId) || (x is ProductionStarted p && p.BuildingId == j.ToId));
                case PlaceKind.RoadSite:
                    return Find(ev, after, x => x is SiteCompleted c && c.Kind == SiteKind.Road && c.Id == j.ToId);
                case PlaceKind.SettlementSite:
                    return Find(ev, after, x => x is SiteCompleted c && c.Kind == SiteKind.Settlement && c.Id == j.ToId);
            }
            return -1;
        }

        private static string Place(WorldState s, PlaceKind kind, int id)
        {
            switch (kind)
            {
                case PlaceKind.Building:
                {
                    var b = s.Economy.Buildings[id];
                    return "the " + BuildingDefs.Get(b.Type).Name.ToLowerInvariant() + (b.State == BuildingState.Site ? " site" : "") + " (building " + id + ")";
                }
                case PlaceKind.RoadSite: return "the road site on hex edge " + s.Economy.Roads[id].HexEdge;
                case PlaceKind.SettlementSite: return "the camp site at corner " + s.Economy.Settlements[id].Corner;
            }
            return "the storehouse";
        }

        private static string Consumed(WorldState s, SimEvent e)
        {
            switch (e)
            {
                case ProductionStarted p: return "consumed: the " + BuildingDefs.Get(s.Economy.Buildings[p.BuildingId].Type).Name.ToLowerInvariant() + " starts a cycle with it";
                case SiteCompleted c when c.Kind == SiteKind.Building: return "consumed: the " + BuildingDefs.Get(s.Economy.Buildings[c.Id].Type).Name.ToLowerInvariant() + " is finished";
                case SiteCompleted c when c.Kind == SiteKind.Road: return "consumed: the main road is built";
                case SiteCompleted c when c.Kind == SiteKind.Settlement: return "consumed: the new camp is founded";
            }
            return "consumed";
        }

        // ---- C ----

        private static void SectionC(StringBuilder sb, List<string> failures)
        {
            sb.Append("## C. Cross-system links (paired runs on seed " + PairSeed + ", each changing one thing)\n\n| Change | Control | Variant | Shows |\n|---|---|---|---|\n");
            var tasks = new List<Func<(string, string, string, string, bool)>>
            {
                DemonOnCamp, DemonOnRoad, DeclineTop, UnfedWarriors, UncollectedHex, UnbanishedDemons,
            };
            var rows = new (string, string, string, string, bool)[tasks.Count + 1];
            Parallel.For(0, tasks.Count, i => rows[i] = tasks[i]());
            rows[tasks.Count] = CastDelays();
            foreach (var (change, control, variant, shows, ok) in rows)
            {
                sb.Append("| ").Append(change).Append(" | ").Append(control).Append(" | ").Append(variant).Append(" | ").Append(shows).Append(" |\n");
                if (!ok) failures.Add("C: " + change + " shows no difference");
            }
            sb.Append("\nPass: each pair shows the stated difference.\n\n");
        }

        private static (string, string, string, string, bool) DemonOnCamp()
        {
            const long at = 3600, window = 100;
            int Count(bool demon)
            {
                int n = 0;
                GameRunner.Play(PairSeed, PlayerStyle.Prompt, hook: sim =>
                {
                    if (demon && sim.State.Tick == at) sim.State.Demons.Corner[sim.State.Board.StartCorners[0]] = 1;
                }, onEvents: (sim, ev) =>
                {
                    foreach (var e in ev)
                        if (e is CollectStarted c && e.Tick > at && e.Tick <= at + window && sim.State.Economy.Buildings[c.BuildingId].Settlement == 0) n++;
                }, maxTicks: (int)(at + window));
                return n;
            }
            int a = Count(false), b = Count(true);
            return ("A demon on the home camp at " + M(at) + " min", a + " collect cycles start there in the next 10 s", b + " start", "its collectors stop", a > 0 && b == 0);
        }

        private static (string, string, string, string, bool) DemonOnRoad()
        {
            // Control: find the first loaded carrier of the player on a main-road leg after 4 minutes.
            long at = -1;
            int job = -1, edge = -1;
            long controlArrive = -1;
            GameRunner.Play(PairSeed, PlayerStyle.Prompt, hook: sim =>
            {
                var s = sim.State;
                if (at >= 0 || s.Tick < 2400) return;
                foreach (var c in s.Economy.Carriers)
                {
                    if (c.Faction != 0 || !c.HasLoad || c.LegCorners.Length < 2) continue;
                    int he = Logistics.CurrentEdge(s, c);
                    if (he < 0) continue;
                    at = s.Tick;
                    job = c.JobId;
                    edge = he;
                    break;
                }
            }, onEvents: (sim, ev) =>
            {
                foreach (var e in ev) if (e is WareDelivered d && d.JobId == job && job >= 0) controlArrive = e.Tick;
            });
            if (at < 0) return ("A demon on a road under a loaded carrier", "no loaded carrier on a road", "-", "-", false);
            long variantArrive = -1;
            GameRunner.Play(PairSeed, PlayerStyle.Prompt, hook: sim =>
            {
                if (sim.State.Tick == at) sim.State.Demons.Edge[edge] = 1;
            }, onEvents: (sim, ev) =>
            {
                foreach (var e in ev) if (e is WareDelivered d && d.JobId == job) variantArrive = e.Tick;
            });
            string v = variantArrive < 0 ? "never arrives (the game ends first)" : "arrives at " + M(variantArrive) + " min";
            return ("A demon on hex edge " + edge + " under a loaded carrier (job " + job + ") at " + M(at) + " min",
                "arrives at " + M(controlArrive) + " min", v, "the shipment waits for the banish", controlArrive >= 0 && (variantArrive < 0 || variantArrive > controlArrive));
        }

        private static (string, string, string, string, bool) DeclineTop()
        {
            string First(PlayerStyle style)
            {
                string first = null;
                GameRunner.Play(PairSeed, style, onEvents: (sim, ev) =>
                {
                    foreach (var e in ev)
                        if (first == null && e is SpellCast c && c.Faction == 0) first = Describe(c) + " at " + M(e.Tick) + " min";
                }, maxTicks: 3000);
                return first ?? "nothing";
            }
            string a = First(PlayerStyle.Prompt), b = First(PlayerStyle.Decliner);
            return ("Declining the top proposal (decliner vs prompt)", "first build: " + a, "first build: " + b, "a different build happens", a.Split(' ')[0] != b.Split(' ')[0] || a != b);
        }

        private static string Describe(SpellCast c) => c.Kind == ProposalKind.Building ? BuildingDefs.Get((BuildingType)c.BuildingType).Name : c.Kind.ToString();

        private static (string, string, string, string, bool) UnfedWarriors()
        {
            // Both peoples start with three warriors (strength 4 each). Control: both have bread for them. Variant: the
            // player's warriors get none, so its strength falls to 1 at their first meal and the rival's warning starts.
            GameResult Run(bool fed) => GameRunner.Play(PairSeed, PlayerStyle.Prompt, setup: sim =>
            {
                var s = sim.State;
                for (int f = 0; f < PlayerIds.Count; f++)
                    for (int i = 0; i < 3; i++)
                        s.Economy.Warriors.Add(new Warrior { Id = s.Economy.Warriors.Count, Faction = f, Lodge = -1, Settlement = f, Fed = true, NextMealTick = 600 });
                SimTestKit.Give(s, 1, Ware.Meat, 60);
                if (fed) SimTestKit.Give(s, 0, Ware.Meat, 60);
            });
            var a = Run(true);
            var b = Run(false);
            string Desc(GameResult r) => "strength " + (r.StrengthPerMinute.Count > 1 ? r.StrengthPerMinute[1][0] + "-" + r.StrengthPerMinute[1][1] : "?") + " at 2 min; first warning "
                + M(r.Warning) + " min; " + r.Outcome + " (" + r.Reason + ") at " + M(r.EndTick) + " min";
            bool ok = b.StrengthPerMinute.Count > 1 && a.StrengthPerMinute.Count > 1 && b.StrengthPerMinute[1][0] < a.StrengthPerMinute[1][0]
                && b.Warning >= 0 && (a.Warning < 0 || b.Warning < a.Warning);
            return ("Three warriors per people from the start; the player's get no food (meat for the rival's)", Desc(a), Desc(b), "strength drops and a warning starts", ok);
        }

        private static (string, string, string, string, bool) UncollectedHex()
        {
            int forest = -1;
            int Lost(bool retire)
            {
                var r = 0;
                GameRunner.Play(PairSeed, PlayerStyle.Ignore, setup: sim =>
                {
                    var s = sim.State;
                    forest = s.Board.Topology.VertexHexes(s.Board.StartCorners[0]).First(h => s.Board.HexTerrain[h] == Terrain.Forest);
                    if (retire)
                        foreach (var b in s.Economy.Buildings)
                            if (b.Settlement == 0 && b.Hex == forest) b.State = BuildingState.Retired;
                }, onEvents: (sim, ev) =>
                {
                    foreach (var e in ev) if (e is StockLost l && l.Hex == forest) r += l.Count;
                }, maxTicks: 6000);
                return r;
            }
            int a = Lost(false), b = Lost(true);
            return ("The home forest's log pile removed at the start (10 minutes, player ignores the prince)", a + " wood lost to the cap there", b + " lost", "uncollected stock is lost to the cap", b > a);
        }

        private static (string, string, string, string, bool) UnbanishedDemons()
        {
            // The same 45 s player; in the variant it never casts a banish (it waits for the next pitch instead).
            var rs = new GameResult[2];
            Parallel.For(0, 2, i => rs[i] = GameRunner.Play(PairSeed, PlayerStyle.FixedDelay, 45, skipBanish: i == 1));
            string D(GameResult r) => r.Outcome + (r.Reason == OutcomeReasons.Time ? " (time)" : "") + " at " + M(r.EndTick) + " min, " + r.PlayerStrength + "-" + r.RivalStrength
                + ", " + r.CollectedByPlayer + " raw goods collected, " + r.PlayerBanishes + " banishes";
            bool ok = rs[1].CollectedByPlayer < rs[0].CollectedByPlayer || rs[1].Outcome != rs[0].Outcome || rs[1].EndTick > rs[0].EndTick;
            return ("Demons never banished by the player (45 s player)", D(rs[0]), D(rs[1]), "an unbanished demon costs goods, strength or the game", ok);
        }

        private static (string, string, string, string, bool) CastDelays()
        {
            var specs = new[] { (PlayerStyle.FixedDelay, 0.0), (PlayerStyle.FixedDelay, 60.0), (PlayerStyle.Ignore, 0.0) };
            var rs = new GameResult[specs.Length];
            Parallel.For(0, specs.Length, i => rs[i] = GameRunner.Play(PairSeed, specs[i].Item1, specs[i].Item2));
            string D(GameResult r) => r.Outcome + " (" + r.Reason + ") at " + M(r.EndTick) + " min, " + r.PlayerStrength + "-" + r.RivalStrength;
            bool ok = rs.Select(r => r.Outcome).Distinct().Count() > 1;
            return ("Cast delay 0 s / 60 s / never", "0 s: " + D(rs[0]), "60 s: " + D(rs[1]) + "; never: " + D(rs[2]), "the outcome changes", ok);
        }

        // ---- D ----

        private static void SectionD(StringBuilder sb, GameResult[] random, List<string> failures)
        {
            sb.Append("## D. Conservation (random legal casts, 20 minutes, invariants of 4.4 every 100 ticks)\n\n");
            sb.Append("| Seed | Casts | Ending | Invariants | Longest job wait | Longest missed import |\n|---|---|---|---|---|---|\n");
            foreach (var r in random)
            {
                sb.Append("| ").Append(r.Seed).Append(" | ").Append(r.PlayerCasts).Append(" | ").Append(r.Outcome).Append(" ").Append(M(r.EndTick))
                    .Append(" | ").Append(r.InvariantFailure ?? "zero drift").Append(" | ").Append(r.LongestJobWait).Append(" ticks | ")
                    .Append(r.LongestMissedImport).Append(" ticks |\n");
                if (r.InvariantFailure != null) failures.Add("D: seed " + r.Seed + " " + r.InvariantFailure);
                if (r.LongestMissedImport > 100) failures.Add("D: seed " + r.Seed + " a camp waited " + r.LongestMissedImport + " ticks for an available import");
                if (r.LongestJobWait > 1800) failures.Add("D: seed " + r.Seed + " a job waited " + r.LongestJobWait + " ticks");
            }
            sb.Append("\nInvariants: for each ware, starting + collected + produced = consumed + in the world; for each raw ware, stock added = lost + taken + young + mature; collected = units taken x yield (less running cycles); ");
            sb.Append("reservations and incoming counts equal the open jobs; no idle carrier holds goods. Deadlock check: a camp that needs a ware a connected camp has spare ");
            sb.Append("always has a shipment on the way within 100 ticks. Starvation check: no job waits more than 3 minutes for a carrier.\n\n");
            sb.Append("Pass: zero drift; no missed import; no starved job.\n\n");
        }

        // ---- E ----

        private static void SectionE(StringBuilder sb, Dictionary<PlayerStyle, GameResult[]> byStyle, List<(string name, GameResult[] games)> sens, List<string> failures)
        {
            sb.Append("## E. Endings (10 seeds per scripted player)\n\n| Player | Victory by overrun | Defeat by overrun | Time limit (V/D/draw) | Median length | Per seed (outcome, minutes, strength) |\n|---|---|---|---|---|---|\n");
            foreach (var kv in byStyle)
            {
                var g = kv.Value;
                sb.Append("| ").Append(kv.Key.ToString().ToLowerInvariant()).Append(" | ").Append(g.Count(r => r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Overrun))
                    .Append(" | ").Append(g.Count(r => r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Overrun)).Append(" | ")
                    .Append(g.Count(r => r.Reason == OutcomeReasons.Time && r.Outcome == GameOutcome.Victory)).Append("/")
                    .Append(g.Count(r => r.Reason == OutcomeReasons.Time && r.Outcome == GameOutcome.Defeat)).Append("/")
                    .Append(g.Count(r => r.Reason == OutcomeReasons.Time && r.Outcome == GameOutcome.Draw)).Append(" | ")
                    .Append(F1(GameRunner.Median(g.Select(r => r.Minutes)))).Append(" | ")
                    .Append(string.Join(", ", g.Select(r => r.Seed + ":" + r.Outcome.ToString()[0] + (r.Reason == OutcomeReasons.Time ? "t" : "") + " " + F1(r.Minutes) + " " + r.PlayerStrength + "-" + r.RivalStrength)))
                    .Append(" |\n");
            }
            var prompt = byStyle[PlayerStyle.Prompt];
            int wins = prompt.Count(r => r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Overrun);
            double med = GameRunner.Median(prompt.Select(r => r.Minutes));
            if (wins < 8) failures.Add("E: prompt wins by overrun in " + wins + " of 10 (need 8)");
            if (med < 10 || med > 15) failures.Add("E: prompt median length " + F1(med) + " min (need 10-15)");
            var ign = byStyle[PlayerStyle.Ignore];
            int over = ign.Count(r => r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Overrun);
            if (over < ign.Length) failures.Add("E: ignored prince is overrun in " + over + " of 10 (need 10)");

            // Forced tuning: nobody may plan, so both stay at one camp: the time limit gives a draw.
            var draw = GameRunner.Play(Seeds[0], PlayerStyle.Prompt, setup: sim => { sim.State.Factions.Tuning.MaxOpenSites = 0; sim.State.Demons.Tuning.GraceTicks = int.MaxValue; });
            sb.Append("\nForced tuning (MaxOpenSites 0 and no demons, so neither people builds): ").Append(draw.Outcome).Append(" by ").Append(draw.Reason)
                .Append(" at ").Append(M(draw.EndTick)).Append(" min, strength ").Append(draw.PlayerStrength).Append("-").Append(draw.RivalStrength).Append(".\n\n");
            if (!(draw.Outcome == GameOutcome.Draw && draw.Reason == OutcomeReasons.Time)) failures.Add("E: the forced-tuning run does not end in a time-limit draw");

            sb.Append("Pass: prompt wins by overrun in at least 8 of 10 with a median length of 10-15 minutes and no game under 9 minutes; the ignored prince is overrun in 10 of 10 ");
            sb.Append("before the time limit; slow and decliner are reported; the forced-tuning run reaches the time-limit draw.\n\n");
            foreach (var r in prompt) if (r.Minutes < 9) failures.Add("E: prompt seed " + r.Seed + " ends at " + F1(r.Minutes) + " min (need 9+)");
        }

        // ---- E2, E3 ----

        private static int Wins(GameResult[] g) => g.Count(r => r.Outcome == GameOutcome.Victory);
        private static int OverrunLosses(GameResult[] g) => g.Count(r => r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Overrun);
        private static bool AllWorkedGoods(GameResult r)
        {
            for (int w = Wares.RawCount; w < Wares.Count; w++) if (r.PlayerMade[w] <= 0 || r.PlayerUsed[w] <= 0) return false;
            return true;
        }

        private static void SectionE2(StringBuilder sb, List<(string name, GameResult[] games)> sens, List<string> failures)
        {
            var ft = FactionTunings.Tuned();
            sb.Append("### E2. Sensitivity of the outcome to the cast delay (11 seeds each)\n\n");
            sb.Append("The player's whole contribution is how soon each proposal is cast after the prince pitches it (a changed pitch restarts the wait). ");
            sb.Append("The rival casts at most every ").Append(F1(ft.RivalCastDelayTicks / 10.0)).Append(" s, ").Append(F1(ft.RivalBoldTicksPerLead / 10.0))
                .Append(" s sooner per point of strength it leads by (never under ").Append(F1(ft.RivalMinDelayTicks / 10.0)).Append(" s). ");
            sb.Append("Walking: the mean straight-line distance between consecutive casts of the player (the first from the home camp); at 12.5 m/s on the lattice.\n\n");
            sb.Append("| Cast delay | Wins (overrun) | Wins (time) | Draws | Losses (time) | Losses (overrun) | Median length | Shortest | Mean final strength (you - rival) | Mean casts | Mean walk between casts |\n|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var (name, g) in sens)
            {
                sb.Append("| ").Append(name).Append(" | ")
                    .Append(g.Count(r => r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Overrun)).Append(" | ")
                    .Append(g.Count(r => r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Time)).Append(" | ")
                    .Append(g.Count(r => r.Outcome == GameOutcome.Draw)).Append(" | ")
                    .Append(g.Count(r => r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Time)).Append(" | ")
                    .Append(OverrunLosses(g)).Append(" | ")
                    .Append(F1(GameRunner.Median(g.Select(r => r.Minutes)))).Append(" | ")
                    .Append(F1(g.Min(r => r.Minutes))).Append(" | ")
                    .Append(F1(g.Average(r => r.PlayerStrength))).Append(" - ").Append(F1(g.Average(r => r.RivalStrength))).Append(" | ")
                    .Append(F1(g.Average(r => r.PlayerCasts))).Append(" | ")
                    .Append(g.Any(r => r.PlayerCasts > 0) ? F1(g.Where(r => r.PlayerCasts > 0).Average(r => r.MeanCastDistance)) + " m (" + F1(g.Where(r => r.PlayerCasts > 0).Average(r => r.MeanCastDistance) / 12.5) + " s)" : "-")
                    .Append(" |\n");
            }
            GameResult[] Row(string n) => sens.First(x => x.name == n).games;
            var d45 = Row("delay 45 s");
            var d60 = Row("delay 60 s");
            var d90 = Row("delay 90 s");
            var ign = Row("ignore");
            double med45 = GameRunner.Median(d45.Select(r => r.Minutes));
            if (Wins(d45) < 9) failures.Add("E2: the 45 s player wins " + Wins(d45) + " of 11 (need 9)");
            if (med45 < 12 || med45 > 18) failures.Add("E2: the 45 s player's median game is " + F1(med45) + " min (need 12-18)");
            if (Wins(d60) < 7) failures.Add("E2: the 60 s player wins " + Wins(d60) + " of 11 (need 7)");
            if (OverrunLosses(d60) > 0) failures.Add("E2: the 60 s player is overrun in " + OverrunLosses(d60) + " of 11 (need 0)");
            int even90 = d90.Count(r => r.Reason == OutcomeReasons.Time || r.Outcome == GameOutcome.Victory);
            if (even90 < 6) failures.Add("E2: the 90 s player ends at the time limit or wins in only " + even90 + " of 11 (need 6)");
            if (OverrunLosses(ign) < ign.Length) failures.Add("E2: the ignored prince is overrun in " + OverrunLosses(ign) + " of 11 (need 11)");
            if (ign.Any(r => r.Minutes < 8)) failures.Add("E2: an ignored prince is overrun before 8 minutes");
            double medIgn = GameRunner.Median(ign.Select(r => r.Minutes));
            if (medIgn < 12 || medIgn > 18) failures.Add("E2: the ignored prince's median overrun is at " + F1(medIgn) + " min (need 12-18)");
            sb.Append("\nPass: at 45 s the player wins at least 9 of 11 with a median game of 12-18 minutes; at 60 s it wins at least 7 of 11 and is never overrun; ");
            sb.Append("at 90 s at least 6 of 11 end at the time limit or in a win; the ignored prince is overrun in 11 of 11, never before 8 minutes, median 12-18 minutes.\n\n");
        }

        private static void SectionE3(StringBuilder sb, List<(string name, GameResult[] games)> sens, List<string> failures)
        {
            var d45 = sens.First(x => x.name == "delay 45 s").games;
            var wins = d45.Where(r => r.Outcome == GameOutcome.Victory).ToArray();
            sb.Append("### E3. The middle and late game in the 45 s player's wins\n\n");
            sb.Append("| Seed | Outcome | Minutes | Strength | City at | First warrior at | Most fed warriors | Warriors tipped it | Worked goods made and used | Banishes |\n|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var r in d45)
            {
                int margin = r.PlayerStrength - r.RivalStrength;
                bool tipped = r.Outcome == GameOutcome.Victory && r.MaxFedWarriors > 0 && (r.Reason == OutcomeReasons.Overrun ? margin - r.MaxFedWarriors < 3 : margin - r.MaxFedWarriors <= 0);
                var goods = Enumerable.Range(Wares.RawCount, Wares.Count - Wares.RawCount).Where(w => r.PlayerMade[w] > 0 && r.PlayerUsed[w] > 0).Select(w => Wares.DisplayName((Ware)w));
                sb.Append("| ").Append(r.Seed).Append(" | ").Append(r.Outcome).Append(r.Reason == OutcomeReasons.Time ? " (time)" : "").Append(" | ").Append(F1(r.Minutes))
                    .Append(" | ").Append(r.PlayerStrength).Append("-").Append(r.RivalStrength).Append(" | ").Append(M(r.City)).Append(" | ").Append(M(r.FirstWarrior))
                    .Append(" | ").Append(r.MaxFedWarriors).Append(" | ").Append(tipped ? "yes" : "no").Append(" | ").Append(string.Join(", ", goods)).Append(" | ").Append(r.PlayerBanishes).Append(" |\n");
            }
            int warriors = wins.Count(r => r.MaxFedWarriors > 0), city = wins.Count(r => r.City >= 0), goodsAll = wins.Count(AllWorkedGoods);
            int tippedN = wins.Count(r => r.MaxFedWarriors > 0 && (r.Reason == OutcomeReasons.Overrun ? (r.PlayerStrength - r.RivalStrength) - r.MaxFedWarriors < 3 : (r.PlayerStrength - r.RivalStrength) - r.MaxFedWarriors <= 0));
            double ban = d45.Average(r => r.PlayerBanishes);
            sb.Append("\nIn the ").Append(wins.Length).Append(" wins: fed warriors in ").Append(warriors).Append(", a city in ").Append(city)
                .Append(", all five worked goods made and used by the player in ").Append(goodsAll).Append("; without its fed warriors the player would not have had the margin in ")
                .Append(tippedN).Append(". Mean banishes per game: ").Append(F1(ban)).Append(".\n\n");
            if (warriors * 2 <= wins.Length) failures.Add("E3: fed warriors in only " + warriors + " of " + wins.Length + " wins");
            if (city * 2 <= wins.Length) failures.Add("E3: a city in only " + city + " of " + wins.Length + " wins");
            if (goodsAll * 2 <= wins.Length) failures.Add("E3: every worked good made and used in only " + goodsAll + " of " + wins.Length + " wins");
            if (ban < 2 || ban > 5) failures.Add("E3: " + F1(ban) + " banishes per game (need 2-5)");
            sb.Append("Pass: in most wins (more than half) the player trains and feeds warriors, raises a city and makes and uses every worked good; the player banishes 2-5 times per game on average.\n\n");
        }

        // ---- F ----

        private static void SectionF(StringBuilder sb, GameResult[] prompt, Dictionary<PlayerStyle, GameResult[]> byStyle, List<string> failures)
        {
            sb.Append("## F. Balance (prompt games)\n\n| Seed | Hut | 2nd camp | 1st workshop | City | 1st warrior | 1st warning (either side) | End | Rival 2nd camp | Rival city |\n|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var r in prompt)
                sb.Append("| ").Append(r.Seed).Append(" | ").Append(M(r.Hut)).Append(" | ").Append(M(r.SecondCamp)).Append(" | ").Append(M(r.FirstWorkshop))
                    .Append(" | ").Append(M(r.City)).Append(" | ").Append(M(r.FirstWarrior)).Append(" | ").Append(M(r.Warning)).Append(" | ").Append(M(r.EndTick))
                    .Append(" | ").Append(M(r.RivalSecondCamp)).Append(" | ").Append(M(r.RivalCity)).Append(" |\n");

            sb.Append("\n| Ware | Collected + produced | Consumed | Hex stock added | Lost to the cap | Worst seed | Construction waited (ticks) |\n|---|---|---|---|---|---|---|\n");
            foreach (var w in Wares.All)
            {
                int i = (int)w;
                int added = prompt.Sum(r => r.StockAdded[i]), lost = prompt.Sum(r => r.StockLost[i]);
                var worst = prompt.OrderByDescending(r => r.StockAdded[i] == 0 ? 0 : (double)r.StockLost[i] / r.StockAdded[i]).ThenBy(r => r.Seed).First();
                sb.Append("| ").Append(Wares.Title(w)).Append(" | ").Append(prompt.Sum(r => r.Collected[i] + r.Produced[i])).Append(" | ")
                    .Append(prompt.Sum(r => r.Consumed[i])).Append(" | ").Append(Wares.IsRaw(w) ? added.ToString(Inv) : "-").Append(" | ")
                    .Append(Wares.IsRaw(w) ? lost + " (" + Pct(lost, added) + ")" : "-").Append(" | ")
                    .Append(Wares.IsRaw(w) ? "seed " + worst.Seed + ": " + Pct(worst.StockLost[i], worst.StockAdded[i]) : "-").Append(" | ")
                    .Append(prompt.Sum(r => r.SiteWaitTicks[i])).Append(" |\n");
                if (Wares.IsRaw(w))
                    foreach (var r in prompt)
                        if (r.StockAdded[i] > 0 && r.StockLost[i] * 100 > 15 * r.StockAdded[i])
                            failures.Add("F: seed " + r.Seed + " loses " + Pct(r.StockLost[i], r.StockAdded[i]) + " of its " + w + " to the cap");
            }
            var waited = Wares.All.OrderByDescending(w => prompt.Sum(r => r.SiteWaitTicks[(int)w])).First();
            sb.Append("\nThe ware construction waited for most: ").Append(Wares.DisplayName(waited)).Append(".\n\n");

            sb.Append("Dice (all prompt games): | Sum | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n| Rolled |");
            int rolls = prompt.Sum(r => r.DiceSums.Sum());
            for (int n = 2; n <= 12; n++) sb.Append(" ").Append(prompt.Sum(r => r.DiceSums[n])).Append(" |");
            sb.Append("\n| Expected |");
            for (int n = 2; n <= 12; n++) sb.Append(" ").Append(F1(rolls * (6 - Math.Abs(7 - n)) / 36.0)).Append(" |");
            sb.Append("\n\n| Seed | Demons placed | Banished | Outbreaks | Strength per minute (you/rival) |\n|---|---|---|---|---|\n");
            foreach (var r in prompt)
                sb.Append("| ").Append(r.Seed).Append(" | ").Append(r.DemonsPlaced).Append(" | ").Append(r.DemonsBanished).Append(" | ").Append(r.Outbreaks)
                    .Append(" | ").Append(string.Join(" ", r.StrengthPerMinute.Select(x => x[0] + "/" + x[1]))).Append(" |\n");

            long longest = byStyle.Values.SelectMany(g => g).Max(r => r.RivalLongestWithoutProposal);
            sb.Append("\nLongest time the rival had no open proposal (all players, all seeds): ").Append(F1(longest / 10.0)).Append(" s.\n\n");
            if (longest >= 1800) failures.Add("F: the rival went " + F1(longest / 10.0) + " s without a proposal");
            sb.Append("Pass: the rival never goes 3 minutes without a proposal; no ware is lost to the cap above 15% in any prompt game.\n\n");
        }

        // ---- G ----

        private static void SectionG(StringBuilder sb, Dictionary<PlayerStyle, GameResult[]> byStyle, List<(string name, GameResult[] games)> sens)
        {
            sb.Append("## G. Known weaknesses\n\n");
            var slow = byStyle[PlayerStyle.Slow];
            var dec = byStyle[PlayerStyle.Decliner];
            GameResult[] Row(string n) => sens.First(x => x.name == n).games;
            var d90 = Row("delay 90 s");
            var ign = Row("ignore");
            sb.Append("- The cast delay still decides the game (table E2). The rival is slow (one spell per 100 s) only while it is not ahead; every point it leads by ");
            sb.Append("makes it cast 60 s sooner, which is what overruns an idle player. The same rule overruns a slow player: at 90 s per cast, ")
                .Append(OverrunLosses(d90)).Append(" of 11 are overrun. Overrunning every idle player and keeping the 90 s player even are in conflict: ");
            sb.Append("an idle player and a 90 s player differ by about one point of strength.\n");
            sb.Append("- The modelled player is crude: a fixed delay (or straight-line walking at 12.5 m/s and fixed talk) per cast; a changed pitch restarts its wait. ");
            sb.Append("Pitches near the last cast get a small bonus (+8 within 60 m), which shortens the walks only a little (table E2).\n");
            sb.Append("- Slow player (90 s after each pitch): ").Append(string.Join(", ", slow.Select(r => r.Outcome.ToString()[0] + (r.Reason == OutcomeReasons.Time ? "t" : "")))).Append(". ");
            sb.Append("Decliner: ").Append(string.Join(", ", dec.Select(r => r.Outcome.ToString()[0] + (r.Reason == OutcomeReasons.Time ? "t" : "")))).Append(".\n");
            sb.Append("- A camp held by demons counts nothing until banished (spec: it counts). This makes demons matter and makes an idle player (who never banishes) weaker; ");
            sb.Append("a demon on a camp at the wrong moment can start or cancel a warning.\n");
            sb.Append("- The lodge opens with a Bakery and a Smokehouse even without a city (spec: city only), and workshops get +30 once a people has two camps; without these the 45 s player never reached warriors before the game ended.\n");
            sb.Append("- Prompt games still end after about 11 minutes, several before the first warrior; the systems show fully in the 30-45 s games.\n");
            sb.Append("- The brain pursues one expansion target at a time and plans nothing else while three sites are open.\n");
            sb.Append("- Logistics is hub-and-spoke with one unit per local trip; carriers are the bottleneck of a busy camp (tuned with faster carriers and more of them). Job ageing (after 30 s) overrides the serving order to stop starvation.\n");
            sb.Append("- Shared dice give both start camps identical home numbers (6, 8, 5), so the first minutes are mirror images; differences come from the board beyond and from the cast delays.\n\n");
        }
    }
}
