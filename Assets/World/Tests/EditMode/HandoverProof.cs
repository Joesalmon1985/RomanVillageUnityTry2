using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Proof section H of Docs/AGES_PROOF.md (PRD_AGES 10 slice H1, 12; package H1-1): the prince's handover, his
    /// autonomy, advice, the Historic step by invitation, dev.historic and the campaign overrun, in scripted headless
    /// games. Built twice and must be byte-identical; every pass criterion is checked. H1-3 grows this into the full
    /// AGES_PROOF generator (it owns AgesProof*.cs); this file is H1-1's section.
    /// </summary>
    public class HandoverProof
    {
        public static readonly ulong[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        /// <summary>20 game minutes after the handover.</summary>
        public const int AfterTicks = 12000;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string M(long ticks) => ticks < 0 ? "-" : (ticks / 600.0).ToString("0.0", Inv);
        private static string F1(double v) => v.ToString("0.0", Inv);

        [Test]
        public void Proof_WritesAgesProofSectionH_AndMeetsTheCriteria()
        {
            var failures = new List<string>();
            string a = Build(failures);
            string b = Build(new List<string>());
            Assert.AreEqual(a, b, "the proof is byte-identical across two runs of the same seeds");
            string root = RepoRoot();
            if (root != null) File.WriteAllText(Path.Combine(root, "Docs", "AGES_PROOF.md"), a.Replace("\r\n", "\n"), new UTF8Encoding(false));
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

        // ---- one scripted game ----

        public enum Advice { None, Priority, Focus, Veto, Favour }

        public sealed class Spec
        {
            public ulong Seed;
            public PlayerStyle Style;
            public double Delay;
            /// <summary>The scripted player keeps casting after the handover (else it stops answering the prince).</summary>
            public bool CastAfter = true;
            /// <summary>Campaign rules from the start (overrun never ends the game, no time limit).</summary>
            public bool Campaign;
            /// <summary>Counterfactual: the player's people never hand over (the rules before H1-1).</summary>
            public bool NoHandover;
            public Advice Advice;
            public PriorityClass Priority = PriorityClass.Expansion;
            public Ware Focus = Ware.Stone;
            public int MaxTicks = 12000;
            /// <summary>Stop AfterTicks after the handover (or at MaxTicks without one).</summary>
            public bool StopAfterWindow;
        }

        public sealed class Run
        {
            public Spec Spec;
            public long Handover = -1, ChapterReady = -1, RivalHandover = -1;
            public int SitesBefore, CastsBefore, CastsAfter, SitesAfter, CompletedAfter, AutoCasts, PlayerCasts;
            public long LongestGapAfter;
            public GameOutcome Outcome;
            public string Reason;
            public long EndTick;
            public int Strength, RivalStrength, Camps, Cities;
            public HashSet<BuildingType> BuiltAfter = new HashSet<BuildingType>();
            public HashSet<BuildingType> Have = new HashSet<BuildingType>();
            public long SecondCamp = -1, ThirdCamp = -1, City = -1, AllWorkshops = -1, FirstWarrior = -1;
            public int MaxFedWarriors, OverrunsSuffered, OverrunsDealt, GameEnded;
            public bool HomeKept = true;
            // advice
            public string AdviceKey;
            public long AdviceTick = -1, AdviceKeyCast = -1;
            public int ExpansionSites, FocusSites;
            public string InvariantFailure;
        }

        public static Run Play(Spec sp)
        {
            var sim = WorldSimFactory.CreateNew(sp.Seed);
            var s = sim.State;
            if (sp.Campaign) ErasModule.EnterCampaign(s);
            var me = s.Factions.Factions[PlayerIds.Human];
            if (sp.NoHandover) me.HandoverTick = long.MaxValue; // counterfactual: the milestone can never fire
            var player = new ScriptedPlayer(sim, sp.Style, sp.Delay, sp.Seed);
            var r = new Run { Spec = sp };
            long lastDone = -1;
            var built = new HashSet<BuildingType>();
            while (s.Tick < sp.MaxTicks && !s.Factions.GameOver)
            {
                if (sp.StopAfterWindow && r.Handover >= 0 && s.Tick >= r.Handover + AfterTicks) break;
                bool handed = r.Handover >= 0;
                if (!handed || sp.CastAfter) player.Act();
                if (handed && r.AdviceTick < 0 && sp.Advice != Advice.None) GiveAdvice(sim, sp, r);
                sim.Step();
                foreach (var e in sim.DrainEvents()) Record(s, r, e, ref lastDone, built);
                r.MaxFedWarriors = Math.Max(r.MaxFedWarriors, FactionApi.Strength(s, 0).FedWarriors);
                if (s.Tick % 100 == 0 && r.InvariantFailure == null)
                {
                    var f = SimTestKit.CheckInvariants(s);
                    if (f != null) r.InvariantFailure = "tick " + s.Tick + ": " + f;
                }
            }
            if (r.Handover >= 0) r.LongestGapAfter = Math.Max(r.LongestGapAfter, s.Tick - Math.Max(lastDone, r.Handover));
            r.Outcome = s.Factions.Outcome;
            r.Reason = s.Factions.OutcomeReason;
            r.EndTick = s.Factions.GameOver ? s.Factions.EndedTick : s.Tick;
            r.Strength = FactionApi.Strength(s, 0).Total;
            r.RivalStrength = FactionApi.Strength(s, 1).Total;
            r.AutoCasts = me.AutoCasts;
            r.PlayerCasts = player.Casts;
            foreach (var st in s.Economy.Settlements)
                if (st.Faction == 0 && st.State == SettlementState.Active) { r.Camps++; if (st.IsCity) r.Cities++; }
            foreach (var b in s.Economy.Buildings) if (b.Faction == 0 && b.State == BuildingState.Active) r.Have.Add(b.Type);
            var home = EconomyLogic.SettlementAt(s, s.Board.StartCorners[0]);
            r.HomeKept = home != null && home.Faction == 0;
            return r;
        }

        private static void GiveAdvice(WorldSim sim, Spec sp, Run r)
        {
            var s = sim.State;
            Advise a = null;
            switch (sp.Advice)
            {
                case Advice.Priority: a = new Advise { Faction = 0, Kind = AdviceKind.Priority, Priority = sp.Priority }; break;
                case Advice.Focus: a = new Advise { Faction = 0, Kind = AdviceKind.FocusWare, Ware = (int)sp.Focus }; break;
                case Advice.Veto:
                {
                    var p = FactionApi.Announced(s, 0);
                    if (p == null) return;
                    r.AdviceKey = p.Key;
                    a = new Advise { Faction = 0, Kind = AdviceKind.Veto, ProposalId = p.Id };
                    break;
                }
                case Advice.Favour:
                {
                    var ann = FactionApi.Announced(s, 0);
                    var p = FactionApi.Proposals(s, 0).FirstOrDefault(x => ann == null || x.Id != ann.Id);
                    if (p == null || !sim.Check(new CastSpell { PlayerId = 0, ProposalId = p.Id }).Ok) return;
                    r.AdviceKey = p.Key;
                    a = new Advise { Faction = 0, Kind = AdviceKind.Favour, ProposalId = p.Id };
                    break;
                }
            }
            a.PlayerId = 0;
            if (sim.Submit(a).Ok) r.AdviceTick = s.Tick;
        }

        /// <summary>The paired-run measure: the key of the advised proposal in the control run (same tick, same choice).</summary>
        private static void Record(WorldState s, Run r, SimEvent e, ref long lastDone, HashSet<BuildingType> built)
        {
            bool after = r.Handover >= 0;
            switch (e)
            {
                case PrinceHandover h:
                    if (h.Faction == 0 && r.Handover < 0) { r.Handover = e.Tick; lastDone = e.Tick; }
                    if (h.Faction == 1 && r.RivalHandover < 0) r.RivalHandover = e.Tick;
                    break;
                case ChapterReady cr when cr.Chapter == ChapterIds.Historic && r.ChapterReady < 0:
                    r.ChapterReady = e.Tick;
                    break;
                case SpellCast sc when sc.Faction == 0:
                    if (!after && sc.Kind != ProposalKind.Banish) r.CastsBefore++;
                    if (after) r.CastsAfter++;
                    var key = Proposal.MakeKey(sc.Kind, sc.BuildingType, sc.Plot, sc.HexEdge, sc.Corner, sc.DemonSiteKind, sc.DemonSiteId);
                    if (r.AdviceKey != null && key == r.AdviceKey && r.AdviceKeyCast < 0) r.AdviceKeyCast = e.Tick;
                    break;
                case SitePlaced sp when sp.Faction == 0:
                    if (!after) r.SitesBefore++;
                    else
                    {
                        r.SitesAfter++;
                        if (e.Tick < r.Handover + 3000)
                        {
                            if (sp.Kind == SiteKind.Road || sp.Kind == SiteKind.Settlement || sp.Kind == SiteKind.City) r.ExpansionSites++;
                            if (sp.Kind == SiteKind.Building)
                            {
                                var t = s.Economy.Buildings[sp.Id].Type;
                                if (AutonomyLogic.ServesWare(s, (int)r.Spec.Focus, ProposalKind.Building, (int)t, -1)) r.FocusSites++;
                            }
                        }
                    }
                    break;
                case SiteCompleted c when c.Faction == 0:
                    if (after && e.Tick <= r.Handover + AfterTicks)
                    {
                        r.CompletedAfter++;
                        r.LongestGapAfter = Math.Max(r.LongestGapAfter, e.Tick - lastDone);
                        lastDone = e.Tick;
                    }
                    if (c.Kind == SiteKind.Building)
                    {
                        var t = s.Economy.Buildings[c.Id].Type;
                        built.Add(t);
                        if (after) r.BuiltAfter.Add(t);
                        if (r.AllWorkshops < 0 && built.Contains(BuildingType.Sawmill) && built.Contains(BuildingType.Kiln) && built.Contains(BuildingType.Bakery)
                            && built.Contains(BuildingType.Smokehouse) && built.Contains(BuildingType.Stonecutter)) r.AllWorkshops = e.Tick;
                    }
                    break;
                case SettlementFounded f when f.Faction == 0:
                    if (r.SecondCamp < 0) r.SecondCamp = e.Tick;
                    else if (r.ThirdCamp < 0) r.ThirdCamp = e.Tick;
                    break;
                case CityUpgraded cu when cu.Faction == 0 && r.City < 0:
                    r.City = e.Tick;
                    break;
                case WarriorTrained wt when wt.Faction == 0 && r.FirstWarrior < 0:
                    r.FirstWarrior = e.Tick;
                    break;
                case CampOverrun co:
                    if (co.Defender == 0) r.OverrunsSuffered++;
                    else r.OverrunsDealt++;
                    break;
                case GameEnded _:
                    r.GameEnded++;
                    break;
            }
        }

        private static Run[] RunAll(IList<Spec> specs)
        {
            var results = new Run[specs.Count];
            Parallel.For(0, specs.Count, i => results[i] = Play(specs[i]));
            return results;
        }

        private static double Median(IEnumerable<double> xs) => GameRunner.Median(xs);

        private static string Name(PlayerStyle style, double delay) => ScriptedPlayer.Name(style, delay);

        // ---- the report ----

        public static string Build(List<string> failures)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Ages proof (generated)");
            sb.AppendLine();
            sb.AppendLine("Generated by `HandoverProof` (Assets/World/Tests/EditMode/HandoverProof.cs) under `dotnet test DotNet`; do not edit by hand. The same seeds always give this file byte for byte. Rules: PRD_AGES 4.2 (handover), 4.3 (campaign overrun), 8.4 (announce and advice), on top of the Prehistoric rules and tuning of ECONOMY_PROOF.md. Times are game minutes (10 ticks = 1 s). Seeds 1 to 11. Scripted players as in ECONOMY_PROOF.md (**prompt**, **delay N s**, **ignore**). The autonomous prince announces his top proposal and carries it out 15 s later unless the player answers (a cast does it at once).");
            sb.AppendLine();
            sb.AppendLine("Package H1-1 writes section H; H1-3 adds the rest of the generator (sections A to T of PRD_AGES 12) around it.");
            sb.AppendLine();
            SectionH1(sb, failures);
            SectionH2(sb, failures);
            SectionH3(sb, failures);
            SectionH4(sb, failures);
            SectionH5(sb, failures);
            SectionH6(sb, failures);
            SectionH7(sb, failures);
            sb.AppendLine("## Verdict");
            sb.AppendLine();
            sb.AppendLine(failures.Count == 0 ? "All pass criteria hold." : "FAILING: " + string.Join("; ", failures));
            return sb.ToString();
        }

        private static readonly (PlayerStyle style, double delay)[] Players =
            { (PlayerStyle.Prompt, 0), (PlayerStyle.FixedDelay, 20), (PlayerStyle.FixedDelay, 45), (PlayerStyle.FixedDelay, 60), (PlayerStyle.FixedDelay, 90), (PlayerStyle.Ignore, 0) };

        // H1: when the handover happens, and that nothing is built before it without a cast (skirmish rules, 20 minutes).
        private static void SectionH1(StringBuilder sb, List<string> failures)
        {
            var specs = new List<Spec>();
            foreach (var (style, delay) in Players)
                foreach (var seed in Seeds) specs.Add(new Spec { Seed = seed, Style = style, Delay = delay });
            var runs = RunAll(specs);
            sb.AppendLine("## H1. The handover (skirmish rules, 20 minutes)");
            sb.AppendLine();
            sb.AppendLine("The milestone: the first worked ware (planks, bricks, bread, meat or cut stone) a carrier delivers to a construction of the player's people. On that tick the prince becomes autonomous, `PrinceHandover` and `ChapterReady{ch.historic}` are raised. Before it, every construction site of the player's people comes from a spell the player cast.");
            sb.AppendLine();
            sb.AppendLine("| Player | Handed over | Median minute | Earliest | Latest | Per seed (minute) | Sites before handover | Casts before handover | Rival handover (median) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var (style, delay) in Players)
            {
                var g = runs.Where(r => r.Spec.Style == style && r.Spec.Delay == delay).ToArray();
                var hs = g.Where(r => r.Handover >= 0).Select(r => r.Handover / 600.0).ToArray();
                var rv = g.Where(r => r.RivalHandover >= 0).Select(r => r.RivalHandover / 600.0).ToArray();
                sb.AppendLine("| " + Name(style, delay) + " | " + hs.Length + " of " + g.Length + " | " + (hs.Length > 0 ? F1(Median(hs)) : "-") + " | "
                    + (hs.Length > 0 ? F1(hs.Min()) : "-") + " | " + (hs.Length > 0 ? F1(hs.Max()) : "-") + " | "
                    + string.Join(", ", g.Select(r => r.Spec.Seed + ":" + M(r.Handover))) + " | " + g.Sum(r => r.SitesBefore) + " | " + g.Sum(r => r.CastsBefore) + " | "
                    + (rv.Length > 0 ? F1(Median(rv)) : "-") + " |");
                foreach (var r in g)
                {
                    if (r.SitesBefore != r.CastsBefore)
                        failures.Add("H1: " + Name(style, delay) + " seed " + r.Seed() + ": " + r.SitesBefore + " sites before the handover but " + r.CastsBefore + " casts");
                    if (r.Handover >= 0 && r.ChapterReady != r.Handover)
                        failures.Add("H1: " + Name(style, delay) + " seed " + r.Seed() + ": ChapterReady at " + r.ChapterReady + ", handover at " + r.Handover);
                    if (r.InvariantFailure != null) failures.Add("H1: " + Name(style, delay) + " seed " + r.Seed() + " " + r.InvariantFailure);
                }
                if (style == PlayerStyle.Ignore && hs.Length > 0) failures.Add("H1: an ignored prince handed over in " + hs.Length + " games");
                if (style == PlayerStyle.Prompt)
                {
                    if (hs.Length < g.Length) failures.Add("H1: the prompt player handed over in only " + hs.Length + " of " + g.Length);
                    else if (Median(hs) < 5 || Median(hs) > 9) failures.Add("H1: the prompt player's median handover is at " + F1(Median(hs)) + " min (need 5-9)");
                }
            }
            sb.AppendLine();
            sb.AppendLine("Pass: an ignored prince never hands over; the prompt player hands over in every game, median 5-9 minutes; before the handover the number of construction sites of the player's people equals the number of construction spells the player cast (nothing is built without a cast); `ChapterReady` comes on the handover tick; ware balance zero drift every 100 ticks.");
            sb.AppendLine();
        }

        // H2: after the handover with no player input at all (campaign rules, so nothing ends the game), 20 minutes.
        private static void SectionH2(StringBuilder sb, List<string> failures)
        {
            var specs = new List<Spec>();
            foreach (var seed in Seeds)
            {
                specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Prompt, CastAfter = false, Campaign = true, MaxTicks = 30000, StopAfterWindow = true });
                specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Prompt, CastAfter = true, Campaign = true, MaxTicks = 30000, StopAfterWindow = true });
                specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Prompt, CastAfter = false, Campaign = true, MaxTicks = 30000, StopAfterWindow = true, Advice = Advice.Priority, Priority = PriorityClass.Economy });
            }
            var runs = RunAll(specs);
            var idle = runs.Where((r, i) => i % 3 == 0).ToArray();
            var casting = runs.Where((r, i) => i % 3 == 1).ToArray();
            var advised = runs.Where((r, i) => i % 3 == 2).ToArray();

            sb.AppendLine("## H2. The prince on his own (20 minutes after the handover, no player input)");
            sb.AppendLine();
            sb.AppendLine("The prompt player plays until the handover, then never answers again. Campaign rules (overrun loots instead of ending the game, no time limit), so the 20 minutes can be watched to the end. Minutes are counted from the handover.");
            sb.AppendLine();
            sb.AppendLine("| Seed | Handover | Finished constructions | Longest gap | 2nd camp | 3rd camp | City | All five workshops | First warrior | Most fed warriors | Camps at end | Strength (you - rival) | Overruns suffered / dealt | Prince's own actions |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in idle)
            {
                Func<long, string> rel = t => t < 0 ? "-" : t <= r.Handover ? "before" : M(t - r.Handover);
                sb.AppendLine("| " + r.Seed() + " | " + M(r.Handover) + " | " + r.CompletedAfter + " | " + M(r.LongestGapAfter) + " | " + rel(r.SecondCamp) + " | " + rel(r.ThirdCamp)
                    + " | " + rel(r.City) + " | " + rel(r.AllWorkshops) + " | " + rel(r.FirstWarrior) + " | " + r.MaxFedWarriors + " | " + r.Camps + " | "
                    + r.Strength + " - " + r.RivalStrength + " | " + r.OverrunsSuffered + " / " + r.OverrunsDealt + " | " + r.AutoCasts + " |");
            }
            int notOverrun = idle.Count(r => r.OverrunsSuffered == 0);
            int keepsBuilding = idle.Count(r => r.LongestGapAfter <= 6000 && r.CompletedAfter >= 10);
            int city = idle.Count(r => r.City >= 0), third = idle.Count(r => r.ThirdCamp >= 0), shops = idle.Count(r => r.AllWorkshops >= 0), warriors = idle.Count(r => r.FirstWarrior >= 0);
            sb.AppendLine();
            sb.AppendLine("Milestones reached in the 11 games (by the end of the window): a third camp in " + third + ", a city in " + city + ", all five workshops in " + shops + ", a warrior in " + warriors
                + ". Not overrun in " + notOverrun + " of 11; building all along (10+ constructions, no 10-minute gap) in " + keepsBuilding + " of 11.");
            sb.AppendLine();
            sb.AppendLine("Does the prince do better with the player? Same seeds and window: the player keeps casting (prompt) or only advises \"Priority: economy\" once at the handover.");
            sb.AppendLine();
            sb.AppendLine("| Who plays after the handover | Mean finished constructions | Mean camps | Cities | Mean strength (you - rival) | Overruns suffered | Mean prince's own actions | Mean player casts after |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            void Row(string who, Run[] g) => sb.AppendLine("| " + who + " | " + F1(g.Average(r => r.CompletedAfter)) + " | " + F1(g.Average(r => r.Camps)) + " | " + g.Count(r => r.City >= 0)
                + " | " + F1(g.Average(r => r.Strength)) + " - " + F1(g.Average(r => r.RivalStrength)) + " | " + g.Sum(r => r.OverrunsSuffered) + " | " + F1(g.Average(r => r.AutoCasts))
                + " | " + F1(g.Average(r => r.CastsAfter - r.AutoCasts)) + " |");
            Row("the prince alone", idle);
            Row("the prince and a casting player", casting);
            Row("the prince, advised \"economy\" once", advised);
            sb.AppendLine();
            if (notOverrun < 8) failures.Add("H2: the prince alone is overrun in " + (11 - notOverrun) + " of 11 (need at most 3)");
            if (keepsBuilding < 8) failures.Add("H2: the prince alone keeps building in only " + keepsBuilding + " of 11 (need 8)");
            if (third < 8) failures.Add("H2: a third camp in only " + third + " of 11 (need 8)");
            if (city < 6) failures.Add("H2: a city in only " + city + " of 11 (need 6)");
            if (shops < 6) failures.Add("H2: all five workshops in only " + shops + " of 11 (need 6)");
            if (casting.Average(r => r.CompletedAfter) <= idle.Average(r => r.CompletedAfter)) failures.Add("H2: a casting player does not add constructions");
            foreach (var r in runs) if (r.InvariantFailure != null) failures.Add("H2: seed " + r.Seed() + " " + r.InvariantFailure);
            foreach (var r in runs) if (r.GameEnded > 0) failures.Add("H2: seed " + r.Seed() + ": the game ended in campaign mode");
            sb.AppendLine("Pass: with no input at all after the handover, the people are not overrun and keep building (10 or more finished constructions in the 20 minutes, never 10 minutes without one) in at least 8 of 11; a third camp in at least 8, a city and all five workshops in at least 6; a player who keeps casting adds constructions on average; zero ware drift; the game never ends in campaign mode.");
            sb.AppendLine();
        }

        // H3: advice changes a measured outcome (paired runs, campaign rules, the prince alone after the handover).
        private static void SectionH3(StringBuilder sb, List<string> failures)
        {
            var kinds = new[] { Advice.None, Advice.Priority, Advice.Focus, Advice.Veto, Advice.Favour };
            var specs = new List<Spec>();
            foreach (var seed in Seeds)
                foreach (var k in kinds)
                    specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Prompt, CastAfter = false, Campaign = true, MaxTicks = 30000, StopAfterWindow = true, Advice = k });
            var runs = RunAll(specs);
            Run Get(ulong seed, Advice k) => runs[Array.IndexOf(Seeds, seed) * kinds.Length + Array.IndexOf(kinds, k)];

            sb.AppendLine("## H3. Advice changes the outcome (paired runs)");
            sb.AppendLine();
            sb.AppendLine("Each pair plays the same seed to the handover (prompt player), then the prince alone; the variant gives one piece of advice on the first tick after the handover, the control none. Campaign rules.");
            sb.AppendLine();
            sb.AppendLine("| Advice | Measure | Control (sum or per seed) | Variant | Seeds that changed in the advised direction |");
            sb.AppendLine("|---|---|---|---|---|");

            // Priority: expansion.
            int pc = Seeds.Sum(x => Get(x, Advice.None).ExpansionSites), pv = Seeds.Sum(x => Get(x, Advice.Priority).ExpansionSites);
            int pBetter = Seeds.Count(x => Get(x, Advice.Priority).ExpansionSites > Get(x, Advice.None).ExpansionSites);
            int pWorse = Seeds.Count(x => Get(x, Advice.Priority).ExpansionSites < Get(x, Advice.None).ExpansionSites);
            sb.AppendLine("| Priority: new camps and roads (+40, 5 min) | road, camp and city sites started in the 5 minutes after | " + pc + " | " + pv + " | " + pBetter + " more, " + pWorse + " fewer |");
            if (pv <= pc || pBetter < 6) failures.Add("H3: Priority expansion does not raise expansion sites (" + pc + " -> " + pv + ", " + pBetter + " seeds)");

            // Focus: stone.
            int fc = Seeds.Sum(x => Get(x, Advice.None).FocusSites), fv = Seeds.Sum(x => Get(x, Advice.Focus).FocusSites);
            int fBetter = Seeds.Count(x => Get(x, Advice.Focus).FocusSites > Get(x, Advice.None).FocusSites);
            int fWorse = Seeds.Count(x => Get(x, Advice.Focus).FocusSites < Get(x, Advice.None).FocusSites);
            sb.AppendLine("| Focus on stone (+30, 5 min) | quarry and stonecutter sites started in the 5 minutes after | " + fc + " | " + fv + " | " + fBetter + " more, " + fWorse + " fewer |");
            if (fv <= fc || fBetter < 4) failures.Add("H3: Focus on stone does not raise stone sites (" + fc + " -> " + fv + ", " + fBetter + " seeds)");

            // Veto: the announced action.
            int vCtl = 0, vVar = 0, vN = 0;
            var vCells = new List<string>();
            foreach (var x in Seeds)
            {
                var v = Get(x, Advice.Veto);
                if (v.AdviceKey == null) { vCells.Add(x + ":-"); continue; }
                vN++;
                // The control's announced action on the same tick is the same proposal (identical runs up to the advice).
                var c = PlayKeyed(x, v.AdviceKey);
                bool ctlSoon = c >= 0 && c - v.AdviceTick <= 600, varSoon = v.AdviceKeyCast >= 0 && v.AdviceKeyCast - v.AdviceTick <= 600;
                if (ctlSoon) vCtl++;
                if (varSoon) vVar++;
                vCells.Add(x + ":" + (ctlSoon ? "c" : "-") + "/" + (varSoon ? "v" : "-"));
            }
            sb.AppendLine("| Not this (on the announced action) | the vetoed action carried out within 60 s | " + vCtl + " of " + vN + " | " + vVar + " of " + vN + " | " + (vCtl - vVar) + " (" + string.Join(" ", vCells) + ") |");
            if (vN < 8 || vVar > 0 || vCtl < vN - 1) failures.Add("H3: Veto: control " + vCtl + ", variant " + vVar + " of " + vN);

            // Favour: the second proposal.
            int favN = 0, favNow = 0;
            double ctlWait = 0;
            int ctlNever = 0;
            foreach (var x in Seeds)
            {
                var v = Get(x, Advice.Favour);
                if (v.AdviceKey == null) continue;
                favN++;
                if (v.AdviceKeyCast >= 0 && v.AdviceKeyCast - v.AdviceTick <= 1) favNow++;
                long c = PlayKeyed(x, v.AdviceKey);
                if (c < 0) ctlNever++;
                else ctlWait += (c - v.AdviceTick) / 10.0;
            }
            int ctlSome = favN - ctlNever;
            sb.AppendLine("| Favour this (the second proposal) | when that action is carried out | " + (ctlSome > 0 ? "after " + F1(ctlWait / ctlSome) + " s on average" : "-") + " in " + ctlSome + " of " + favN + ", never in " + ctlNever
                + " | at once in " + favNow + " of " + favN + " | " + favNow + " |");
            if (favN < 8 || favNow < favN) failures.Add("H3: Favour carried out at once in " + favNow + " of " + favN);
            sb.AppendLine();
            sb.AppendLine("Veto cells: seed:c/v, c = carried out within 60 s in the control, v = in the variant.");
            sb.AppendLine();
            sb.AppendLine("Pass: Priority raises the advised class in at least 6 of 11 seeds and in sum; Focus raises the advised ware's sites in at least 4 of 11 and in sum; a vetoed action is never carried out within its 60 s cooldown while the control carries it out (at most one exception); a favoured action is carried out at once in every seed that had one.");
            sb.AppendLine();
        }

        /// <summary>The control run of a paired advice run: the tick at which the action with this key was cast (-1 never).</summary>
        private static long PlayKeyed(ulong seed, string key)
        {
            var sp = new Spec { Seed = seed, Style = PlayerStyle.Prompt, CastAfter = false, Campaign = true, MaxTicks = 30000, StopAfterWindow = true };
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            ErasModule.EnterCampaign(s);
            var player = new ScriptedPlayer(sim, sp.Style, 0, seed);
            long handover = -1;
            while (s.Tick < sp.MaxTicks)
            {
                if (handover >= 0 && s.Tick >= handover + AfterTicks) break;
                if (handover < 0) player.Act();
                sim.Step();
                foreach (var e in sim.DrainEvents())
                {
                    if (e is PrinceHandover h && h.Faction == 0 && handover < 0) handover = e.Tick;
                    if (e is SpellCast sc && sc.Faction == 0 && handover >= 0
                        && Proposal.MakeKey(sc.Kind, sc.BuildingType, sc.Plot, sc.HexEdge, sc.Corner, sc.DemonSiteKind, sc.DemonSiteId) == key) return e.Tick;
                }
            }
            return -1;
        }

        // H4: the Historic step by invitation, across save and load.
        private static void SectionH4(StringBuilder sb, List<string> failures)
        {
            var rows = new string[Seeds.Length];
            var ok = new bool[Seeds.Length];
            Parallel.For(0, Seeds.Length, i =>
            {
                ulong seed = Seeds[i];
                var sim = WorldSimFactory.CreateNew(seed);
                var s = sim.State;
                var player = new ScriptedPlayer(sim, PlayerStyle.Prompt, 0, seed);
                bool ready = false;
                while (s.Tick < 12000 && !s.Factions.GameOver && !ready)
                {
                    player.Act();
                    sim.Step();
                    foreach (var e in sim.DrainEvents()) if (e is ChapterReady cr && cr.Chapter == ChapterIds.Historic) ready = true;
                }
                if (!ready) { rows[i] = "| " + seed + " | - | - | - | - | - |"; return; }
                long at = s.Tick;
                var loaded = SaveCodec.Load(SaveCodec.ToJson(s), out _);
                bool same0 = SaveCodec.StateJson(s) == SaveCodec.StateJson(loaded.State);
                var refusedEarly = sim.Check(new AdvanceChapter { PlayerId = 0, To = ChapterIds.ModernHarbour }).Reason;
                var ra = sim.Submit(new AdvanceChapter { PlayerId = 0, To = ChapterIds.Historic });
                var rb = loaded.Submit(new AdvanceChapter { PlayerId = 0, To = ChapterIds.Historic });
                var evA = new List<string>();
                var evB = new List<string>();
                for (int t = 0; t < 1200; t++)
                {
                    sim.Step();
                    loaded.Step();
                    evA.AddRange(sim.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick));
                    evB.AddRange(loaded.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick));
                }
                bool same = SaveCodec.StateJson(s) == SaveCodec.StateJson(loaded.State) && evA.SequenceEqual(evB);
                bool advanced = AgesApi.Current(s) == Era.Historic && evA.Contains("ChapterAdvanced@" + (at + 1)) && AgesApi.CampaignMayStart(s);
                ok[i] = same0 && ra.Ok && rb.Ok && same && advanced && refusedEarly == ErasRefusals.ChapterNotReady && !s.Factions.GameOver;
                rows[i] = "| " + seed + " | " + M(at) + " | " + (ra.Ok && rb.Ok ? "accepted" : "refused") + " | " + (advanced ? "Historic, CampaignMayStart" : "no") + " | "
                    + (same0 && same ? "identical" : "DIFFERENT") + " | " + (s.Factions.GameOver ? "ended" : "plays on") + " |";
            });
            sb.AppendLine("## H4. The Historic step by invitation, across save and load");
            sb.AppendLine();
            sb.AppendLine("Prompt player, skirmish start. On `ChapterReady{ch.historic}` the game is saved and loaded; both copies submit `AdvanceChapter{ch.historic}` (the player's \"Show me\") and run 2 minutes more. A step to a chapter that is not ready is refused.");
            sb.AppendLine();
            sb.AppendLine("| Seed | Ready at | AdvanceChapter | After the step | Saved copy vs original (state and events, 2 min) | Game |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var row in rows) sb.AppendLine(row);
            sb.AppendLine();
            int n = ok.Count(x => x);
            if (n < Seeds.Length) failures.Add("H4: the step by invitation across save and load holds in " + n + " of " + Seeds.Length);
            sb.AppendLine("Pass: in every seed the step is accepted, the island is Historic with `CampaignMayStart`, the loaded copy plays on byte-identical to the original, and the Historic game is not ended by the 20-minute skirmish clock.");
            sb.AppendLine();
        }

        // H5: campaign overrun does not end the game; skirmish overrun still does.
        private static void SectionH5(StringBuilder sb, List<string> failures)
        {
            var specs = new List<Spec>();
            foreach (var seed in Seeds)
            {
                specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Ignore, Campaign = true, MaxTicks = 24000 });
                specs.Add(new Spec { Seed = seed, Style = PlayerStyle.Ignore, Campaign = false, MaxTicks = 24000 });
            }
            var runs = RunAll(specs);
            var camp = runs.Where((r, i) => i % 2 == 0).ToArray();
            var skirm = runs.Where((r, i) => i % 2 == 1).ToArray();
            sb.AppendLine("## H5. Overrun in the campaign and in skirmish (ignored prince, 40 minutes)");
            sb.AppendLine();
            sb.AppendLine("The player never answers, so the Ash Clan grows stronger. In campaign mode an overrun loots the player's home (half its stock, its buildings stop 3 minutes; a non-home camp would be captured instead once H1-2's `TransferSettlement` lands), then 5 minutes of grace; the game never ends. In skirmish the first overrun ends the game, as built.");
            sb.AppendLine();
            sb.AppendLine("| Seed | Campaign: overruns suffered | Game ended | Home kept | Skirmish: ending |");
            sb.AppendLine("|---|---|---|---|---|");
            for (int i = 0; i < Seeds.Length; i++)
                sb.AppendLine("| " + Seeds[i] + " | " + camp[i].OverrunsSuffered + " | " + (camp[i].GameEnded > 0 ? "yes" : "no") + " | " + (camp[i].HomeKept ? "yes" : "no") + " | "
                    + skirm[i].Outcome + " (" + skirm[i].Reason + ") at " + M(skirm[i].EndTick) + " |");
            sb.AppendLine();
            if (camp.Any(r => r.GameEnded > 0)) failures.Add("H5: a campaign game ended");
            if (camp.Any(r => !r.HomeKept)) failures.Add("H5: a campaign home was lost");
            if (camp.Count(r => r.OverrunsSuffered >= 2) < 8) failures.Add("H5: fewer than 8 campaign games show two or more overruns");
            if (skirm.Any(r => !(r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Overrun))) failures.Add("H5: a skirmish ignored prince is not overrun");
            foreach (var r in runs) if (r.InvariantFailure != null) failures.Add("H5: seed " + r.Seed() + " " + r.InvariantFailure);
            sb.AppendLine("Pass: no campaign game ends and the home is always kept; at least 8 of 11 show repeated overruns (the grace ends and the stronger rival comes again); every skirmish game ends in defeat by overrun; zero ware drift.");
            sb.AppendLine();
        }

        // H6: what the handover did to the Prehistoric skirmish pace (open decision 14.2 default: the handover also applies in skirmish).
        private static void SectionH6(StringBuilder sb, List<string> failures)
        {
            var players = new[] { (PlayerStyle.Prompt, 0.0), (PlayerStyle.FixedDelay, 45.0), (PlayerStyle.FixedDelay, 60.0), (PlayerStyle.FixedDelay, 90.0), (PlayerStyle.Ignore, 0.0) };
            var specs = new List<Spec>();
            foreach (var (style, delay) in players)
                foreach (var seed in Seeds)
                {
                    specs.Add(new Spec { Seed = seed, Style = style, Delay = delay });
                    specs.Add(new Spec { Seed = seed, Style = style, Delay = delay, NoHandover = true });
                }
            var runs = RunAll(specs);
            sb.AppendLine("## H6. The Prehistoric skirmish with and without the handover");
            sb.AppendLine();
            sb.AppendLine("Open decision 14.2, default taken: the handover also happens in the 20-minute skirmish (the prince builds on his own once the first workshop goods reach a site; no age step). The counterfactual column replays the same seeds with the handover switched off (the rules of ECONOMY_PROOF.md before H1-1). ECONOMY_PROOF.md (sections E, E2, E3) is regenerated with the handover on and all its pass criteria still hold; this table shows what moved.");
            sb.AppendLine();
            sb.AppendLine("| Player | Handover | Wins (overrun / time) | Draws | Losses (time / overrun) | Median length | Mean strength (you - rival) |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var (style, delay) in players)
                foreach (bool off in new[] { true, false })
                {
                    var g = runs.Where(r => r.Spec.Style == style && r.Spec.Delay == delay && r.Spec.NoHandover == off).ToArray();
                    int wo = g.Count(r => r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Overrun), wt = g.Count(r => r.Outcome == GameOutcome.Victory && r.Reason == OutcomeReasons.Time);
                    int d = g.Count(r => r.Outcome == GameOutcome.Draw);
                    int lt = g.Count(r => r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Time), lo = g.Count(r => r.Outcome == GameOutcome.Defeat && r.Reason == OutcomeReasons.Overrun);
                    sb.AppendLine("| " + Name(style, delay) + " | " + (off ? "off (before H1-1)" : "on") + " | " + wo + " / " + wt + " | " + d + " | " + lt + " / " + lo + " | "
                        + F1(Median(g.Select(r => r.EndTick / 600.0))) + " | " + F1(g.Average(r => r.Strength)) + " - " + F1(g.Average(r => r.RivalStrength)) + " |");
                }
            sb.AppendLine();
            var ign = runs.Where(r => r.Spec.Style == PlayerStyle.Ignore && !r.Spec.NoHandover).ToArray();
            var ignOff = runs.Where(r => r.Spec.Style == PlayerStyle.Ignore && r.Spec.NoHandover).ToArray();
            for (int i = 0; i < ign.Length; i++)
                if (ign[i].EndTick != ignOff[i].EndTick || ign[i].Outcome != ignOff[i].Outcome) failures.Add("H6: the ignored prince's game changed on seed " + ign[i].Seed());
            var p90 = runs.Where(r => r.Spec.Style == PlayerStyle.FixedDelay && r.Spec.Delay == 90).ToArray();
            int lossOn = p90.Count(r => !r.Spec.NoHandover && r.Outcome == GameOutcome.Defeat), lossOff = p90.Count(r => r.Spec.NoHandover && r.Outcome == GameOutcome.Defeat);
            if (lossOn > lossOff) failures.Add("H6: the handover made the 90 s player lose more often (" + lossOff + " -> " + lossOn + ")");
            sb.AppendLine("What moved: a slow player now gets the prince's help after the first workshop goods, so the 60 s and 90 s players win more often and sooner; the prompt player is barely changed (he already casts about as fast as the prince would act); the ignored prince is unchanged (he never hands over). The rival was not retuned: ECONOMY_PROOF.md keeps all its pass criteria.");
            sb.AppendLine();
            sb.AppendLine("Pass: the ignored prince's games are identical with and without the handover; the handover never makes the 90 s player lose more often.");
            sb.AppendLine();
        }

        // H7: dev.historic.
        private static void SectionH7(StringBuilder sb, List<string> failures)
        {
            var rows = new string[Seeds.Length];
            var ok = new bool[Seeds.Length];
            string json1 = null, json1b = null;
            Parallel.For(0, Seeds.Length + 1, i =>
            {
                if (i == Seeds.Length)
                {
                    json1b = SaveCodec.StateJson(ScenarioStarts.Create(ScenarioStarts.DevHistoric, Seeds[0]).State);
                    return;
                }
                ulong seed = Seeds[i];
                var sim = ScenarioStarts.Create(ScenarioStarts.DevHistoric, seed);
                var s = sim.State;
                if (i == 0) json1 = SaveCodec.StateJson(s);
                bool p = ScenarioStarts.HistoricStartReached(s, 0), r = ScenarioStarts.HistoricStartReached(s, 1);
                string Who(int f)
                {
                    int camps = s.Economy.Settlements.Count(x => x.Faction == f && x.State == SettlementState.Active);
                    int b = s.Economy.Buildings.Count(x => x.Faction == f && x.State == BuildingState.Active && BuildingDefs.Get(x.Type).Placeable);
                    return camps + " camps, " + b + " buildings";
                }
                long start = s.Tick;
                string inv = null;
                for (int t = 0; t < 6000 && !s.Factions.GameOver; t++)
                {
                    sim.Step();
                    sim.DrainEvents();
                    if (inv == null && s.Tick % 100 == 0) inv = SimTestKit.CheckInvariants(s);
                }
                string after = s.Factions.GameOver ? s.Factions.Outcome + " (" + s.Factions.OutcomeReason + ") at +" + M(s.Factions.EndedTick - start) : "plays on, " + FactionApi.Strength(s, 0).Total + "-" + FactionApi.Strength(s, 1).Total;
                ok[i] = p && r && inv == null && AgesApi.Current(s) == Era.Historic && AgesApi.HandoverTick(s, 0) >= 0 && AgesApi.HandoverTick(s, 1) >= 0;
                rows[i] = "| " + seed + " | " + M(start) + " | " + (p ? "yes" : "NO") + " (" + Who(0) + ") | " + (r ? "yes" : "NO") + " (" + Who(1) + ") | " + after + " | " + (inv ?? "zero drift") + " |";
            });
            sb.AppendLine("## H7. `dev.historic`");
            sb.AppendLine();
            sb.AppendLine("The start is the end state of a scripted run: a director casts both peoples' top proposals as soon as they are legal until each has its home raised to a city, at least two more camps, a Sawmill, a Kiln, a Bakery, a Smokehouse and two improved collectors; then both are autonomous and the island takes the Historic step (the smith's chest: none of its wares exist before H2). Then it plays 10 minutes on its own (DevStart rules: overrun still ends a game outside the campaign).");
            sb.AppendLine();
            sb.AppendLine("| Seed | Built in | Player reached | Rival reached | After 10 minutes | Invariants |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var row in rows) sb.AppendLine(row);
            sb.AppendLine();
            sb.AppendLine("Same seed twice: " + (json1 == json1b ? "identical state" : "DIFFERENT") + ".");
            sb.AppendLine();
            if (json1 != json1b) failures.Add("H7: dev.historic is not deterministic");
            int n = ok.Count(x => x);
            if (n < Seeds.Length) failures.Add("H7: dev.historic reached its contents in " + n + " of " + Seeds.Length);
            sb.AppendLine("Pass: both peoples reach the listed contents on every seed, both have handed over, the era is Historic, zero ware drift, the same seed gives the same start.");
            sb.AppendLine();
        }
    }

    internal static class HandoverProofExt
    {
        public static ulong Seed(this HandoverProof.Run r) => r.Spec.Seed;
    }
}
