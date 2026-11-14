using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Encounters.Ward;

namespace World.Sim.Tests
{
    /// <summary>Ward opponent profiles (PRD_CRYSTALS I3): data, determinism, and balance against scripted players.</summary>
    public class WardOpponentTests
    {
        public static readonly WardBalanceTests.PlayerModel FirstTimer = WardBalanceTests.FirstTimer;
        public static readonly WardBalanceTests.PlayerModel Competent = new WardBalanceTests.PlayerModel { MinMs = 3500, MaxMs = 5000, MistakePercent = 5 };
        public static readonly WardBalanceTests.PlayerModel Expert = new WardBalanceTests.PlayerModel { MinMs = 2200, MaxMs = 3200, MistakePercent = 0 };
        const int Seeds = 300;

        static string FindFile()
        {
            // dotnet test runs from DotNet/.../bin; Unity's runner has the project folder as its working directory.
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var d = new DirectoryInfo(start);
                while (d != null)
                {
                    var f = Path.Combine(d.FullName, "Assets", "StreamingAssets", "world", "ward_opponents.json");
                    if (File.Exists(f)) return f;
                    d = d.Parent;
                }
            }
            Assert.Fail("ward_opponents.json not found");
            return null;
        }

        public static WardOutcome Simulate(WardDuelSpec spec, ulong seed, WardBalanceTests.PlayerModel m)
        {
            var t = spec.Tuning;
            var s = WardDuelSession.Create(spec);
            var r = new Rng(seed ^ 0xA5A5F00DUL);
            var me = new WardRival(new WardTuning { Loci = t.Loci, MiraMistakeRatePercent = m.MistakePercent }, spec.Pool, r.Fork("player"));
            s.SetPlayerWard(me.ChooseWard());
            int next = r.NextRange(m.MinMs, m.MaxMs + 1);
            while (s.Phase == WardPhase.Active && s.ElapsedMs < 900000)
            {
                s.Advance(20);
                foreach (var e in s.DrainEvents())
                    if (e is PlayerCastEvent pe && pe.Auto) me.Observe(pe.Entry.Weave, pe.Entry.Fractures, pe.Entry.Echoes);
                if (s.Phase != WardPhase.Active) break;
                if (s.ElapsedMs >= next && s.MsUntilPlayerMayCast == 0 && s.PlayerCastsLeft > 0)
                {
                    var w = me.NextCast();
                    var c = s.PlayerCast(w);
                    if (c.Ok) me.Observe(w, c.Fractures, c.Echoes);
                    next = s.ElapsedMs + r.NextRange(m.MinMs, m.MaxMs + 1);
                }
            }
            return s.Outcome;
        }

        static void Rates(WardOpponents o, string id, WardBalanceTests.PlayerModel m, out double win, out double lose, out double other)
        {
            int w = 0, l = 0;
            for (int i = 0; i < Seeds; i++)
            {
                var oc = Simulate(o.Spec(id, (ulong)(7000 + i)), (ulong)(7000 + i), m);
                if (oc == WardOutcome.Victory) w++; else if (oc == WardOutcome.Defeat) l++;
            }
            win = (double)w / Seeds; lose = (double)l / Seeds; other = 1.0 - win - lose;
        }

        static readonly string[] MeasuredIds =
        {
            "practice_mira", "keeper", "keeper_earth", "keeper_fire", "keeper_water", "keeper_air",
            "rival_hesk", "friendly_orrin", "finale_unweaver"
        };

        [Test]
        public void Measure_PrintsWinRateTable()
        {
            var o = WardOpponents.BuiltIn();
            var sb = new StringBuilder();
            sb.AppendLine("| Profile | First-timer win | lose | draw | Competent win | Expert win |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var id in MeasuredIds)
            {
                Rates(o, id, FirstTimer, out var w, out var l, out var d);
                Rates(o, id, Competent, out var cw, out _, out _);
                Rates(o, id, Expert, out var ew, out _, out _);
                sb.AppendLine("| `" + id + "` | " + Math.Max(0, w).ToString("P0") + " | " + l.ToString("P0") + " | " + Math.Max(0, d).ToString("P0") + " | " + cw.ToString("P0") + " | " + ew.ToString("P0") + " |");
            }
            Console.WriteLine(sb.ToString());
            var outPath = Environment.GetEnvironmentVariable("WARD_TABLE_OUT");
            if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, sb.ToString());
            // regenerate the data file from the built-in table: WARD_JSON_OUT=<path> dotnet test DotNet --filter Measure_
            var jsonOut = Environment.GetEnvironmentVariable("WARD_JSON_OUT");
            if (!string.IsNullOrEmpty(jsonOut))
                File.WriteAllText(jsonOut, JsonConvert.SerializeObject(new WardOpponents.FileShape { Profiles = o.All }, Formatting.Indented,
                    new Newtonsoft.Json.Converters.StringEnumConverter()) + Environment.NewLine);
        }

        [Test]
        public void Keepers_FirstTimerWins65To75Percent()
        {
            var o = WardOpponents.BuiltIn();
            foreach (var id in new[] { "keeper", "keeper_earth", "keeper_fire", "keeper_water", "keeper_air" })
            {
                Rates(o, id, FirstTimer, out var w, out _, out _);
                Assert.That(w, Is.InRange(0.65, 0.75), id);
            }
        }

        [Test]
        public void Finale_FirstTimerWinsAboutAHalfPlus()
        {
            Rates(WardOpponents.BuiltIn(), "finale_unweaver", FirstTimer, out var w, out _, out _);
            Assert.That(w, Is.InRange(0.50, 0.60));
        }

        [Test]
        public void SkillOrdering_ExpertBeatsCompetentBeatsFirstTimer_OnEveryLethalProfile()
        {
            var o = WardOpponents.BuiltIn();
            foreach (var id in new[] { "keeper", "rival_hesk", "finale_unweaver" })
            {
                Rates(o, id, FirstTimer, out var a, out _, out _);
                Rates(o, id, Competent, out var b, out _, out _);
                Rates(o, id, Expert, out var c, out _, out _);
                Assert.Greater(b, a, id + " competent > first-timer");
                Assert.GreaterOrEqual(c, b, id + " expert >= competent");
            }
        }

        [Test]
        public void RivalHesk_IsHarderThanKeepersAndEasierThanFinale()
        {
            var o = WardOpponents.BuiltIn();
            Rates(o, "keeper", FirstTimer, out var k, out _, out _);
            Rates(o, "rival_hesk", FirstTimer, out var h, out _, out _);
            Rates(o, "finale_unweaver", FirstTimer, out var f, out _, out _);
            Assert.Less(h, k);
            Assert.Greater(h, f);
        }

        [Test]
        public void PracticeMira_MatchesTheShippedSession_CastForCast()
        {
            var o = WardOpponents.BuiltIn();
            foreach (int completed in new[] { 0, 3 })
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var tuning = WardTuning.Practice();
                    var pool = WardFormat.For(completed, tuning);
                    var old = new WardDuelSession(tuning, pool, seed);
                    var now = WardDuelSession.Create(o.Spec("practice_mira", seed, completed));
                    var ward = new[] { 0, 1, 2 };
                    Assert.IsTrue(old.SetPlayerWard(ward)); Assert.IsTrue(now.SetPlayerWard(ward));
                    var a = new List<string>(); var b = new List<string>();
                    for (int ms = 0; ms < 120000 && (old.Phase == WardPhase.Active || now.Phase == WardPhase.Active); ms += 50)
                    {
                        old.Advance(50); now.Advance(50);
                        Dump(old, a); Dump(now, b);
                    }
                    Assert.AreEqual(string.Join("|", a), string.Join("|", b), "seed " + seed + " pool " + pool.Length);
                    Assert.IsNotEmpty(a);
                }
        }

        static void Dump(WardDuelSession s, List<string> into)
        {
            foreach (var e in s.DrainEvents())
            {
                if (e is RivalCastEvent r) into.Add(e.AtMs + ":" + string.Join("", r.Entry.Weave) + "/" + r.Entry.Fractures + r.Entry.Echoes);
                else if (e is PlayerCastEvent p) into.Add(e.AtMs + ":P" + string.Join("", p.Entry.Weave));
                else if (e is DuelEndedEvent d) into.Add(e.AtMs + ":" + d.Outcome);
            }
        }

        [Test]
        public void SameSeedAndProfile_GiveTheSameDuel_AndSeedsDiffer()
        {
            var o = WardOpponents.BuiltIn();
            foreach (var id in MeasuredIds)
            {
                var first = new List<string>();
                for (ulong seed = 1; seed <= 15; seed++)
                {
                    Assert.AreEqual(Simulate(o.Spec(id, seed), seed, FirstTimer), Simulate(o.Spec(id, seed), seed, FirstTimer), id);
                    first.Add(Simulate(o.Spec(id, seed), seed, FirstTimer).ToString());
                }
                Assert.Greater(new HashSet<string>(first).Count, 1, id + " outcomes vary with seed");
            }
        }

        [Test]
        public void For_WritesProfileIntoMiraFields_AndLeavesTheRules()
        {
            var o = WardOpponents.BuiltIn();
            var p = o.GetOrDefault("keeper_fire");
            var t = WardTuning.For(p);
            Assert.AreEqual(p.FirstCastMinMs, t.MiraFirstCastMinMs);
            Assert.AreEqual(p.ThinkMaxMs, t.MiraThinkMaxMs);
            Assert.AreEqual(p.MistakeRatePercent, t.MiraMistakeRatePercent);
            Assert.AreEqual(3, t.Loci);
            Assert.AreEqual(10, t.MaxCasts);
            Assert.AreEqual(2000, t.PlayerRecoveryMs);
            var capped = WardTuning.For(new WardOpponentProfile { Id = "x", FirstCastMinMs = 1, FirstCastMaxMs = 2, ThinkMinMs = 3, ThinkMaxMs = 4, MaxCasts = 6 });
            Assert.AreEqual(6, capped.MaxCasts);
        }

        [Test]
        public void Table_HasEveryNeededId_Pools_AndSaneHints()
        {
            var o = WardOpponents.BuiltIn();
            foreach (var id in MeasuredIds) Assert.IsTrue(o.TryGet(id, out _), id);
            Assert.AreEqual(3, o.GetOrDefault("keeper").Pool.Length);
            Assert.AreEqual(4, o.GetOrDefault("finale_unweaver").Pool.Length);
            Assert.Contains(Essence.Light, o.GetOrDefault("finale_unweaver").Pool);
            Assert.IsNull(o.GetOrDefault("practice_mira").Pool, "practice keeps the 3-then-4 format");
            Assert.AreEqual("keeper", o.GetOrDefault("no_such_profile").Id, "unknown id falls back to the keeper");
            foreach (var p in o.All)
            {
                Assert.LessOrEqual(p.FirstCastMinMs, p.FirstCastMaxMs, p.Id);
                Assert.LessOrEqual(p.ThinkMinMs, p.ThinkMaxMs, p.Id);
                Assert.Less(p.CastAnimMs, p.ThinkMinMs, p.Id);
                Assert.IsNotEmpty(p.CastTempo, p.Id);
            }
        }

        [Test]
        public void ProfileFile_EqualsBuiltIn_AndIsWhatTheGameLoads()
        {
            var fromFile = WardOpponents.Parse(File.ReadAllText(FindFile()));
            var builtIn = WardOpponents.BuiltIn();
            Assert.AreEqual(builtIn.All.Count, fromFile.All.Count);
            for (int i = 0; i < builtIn.All.Count; i++)
                Assert.AreEqual(JsonConvert.SerializeObject(builtIn.All[i]), JsonConvert.SerializeObject(fromFile.All[i]), builtIn.All[i].Id);
        }

        [Test]
        public void Create_NullSpec_FallsBackToPractice()
        {
            var s = WardDuelSession.Create(null);
            Assert.IsTrue(s.SetPlayerWard(new[] { 0, 1, 2 }));
        }
    }
}
