using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Encounters.Ward;

namespace World.Sim.Tests
{
    public class WardScorerTests
    {
        const int A = 0, B = 1, C = 2;

        static void Check(int[] ward, int[] weave, int f, int e, int d)
        {
            var s = WardScorer.Score(ward, weave);
            Assert.AreEqual(f, s.fractures, "fractures");
            Assert.AreEqual(e, s.echoes, "echoes");
            Assert.AreEqual(d, s.fades, "fades");
        }

        [Test]
        public void RepeatVectors()
        {
            var ward = new[] { B, B, A };
            Check(ward, new[] { B, A, A }, 2, 0, 1);
            Check(ward, new[] { A, A, A }, 1, 0, 2);
            Check(ward, new[] { C, B, B }, 1, 1, 1);
            Check(ward, new[] { A, C, C }, 0, 1, 2);
        }

        [Test]
        public void BrokenWhenAllFractured()
        {
            Assert.IsTrue(WardScorer.Score(new[] { A, B, C }, new[] { A, B, C }).broken);
            Assert.IsFalse(WardScorer.Score(new[] { A, B, C }, new[] { A, B, B }).broken);
        }

        // secret, guess, exact (fractures), colour_only (echoes): shared_fixtures/feedback_cases.json
        // and duplicate_edge_cases.json embedded.
        static readonly int[][][] Cases =
        {
            new[] { new[] {0,1,2,3}, new[] {0,1,2,3}, new[] {4,0} },
            new[] { new[] {0,0,1,2}, new[] {0,1,0,3}, new[] {1,2} },
            new[] { new[] {0,0,1,2}, new[] {0,0,0,0}, new[] {2,0} },
            new[] { new[] {1,1,1,1}, new[] {1,2,3,4}, new[] {1,0} },
            new[] { new[] {0,1,2,3}, new[] {3,2,1,0}, new[] {0,4} },
            new[] { new[] {0,0,1,2}, new[] {3,4,5,6}, new[] {0,0} },
            new[] { new[] {0,0,0,1}, new[] {0,0,1,0}, new[] {2,2} },
            new[] { new[] {2,2,2,2}, new[] {2,2,3,3}, new[] {2,0} },
        };

        [Test]
        public void SharedFixtureCases()
        {
            foreach (var c in Cases)
            {
                var s = WardScorer.Score(c[0], c[1]);
                Assert.AreEqual(c[2][0], s.fractures, string.Join(",", c[0]) + " vs " + string.Join(",", c[1]));
                Assert.AreEqual(c[2][1], s.echoes, string.Join(",", c[0]) + " vs " + string.Join(",", c[1]));
            }
        }

        [Test]
        public void LengthMismatchThrows()
        {
            Assert.Throws<ArgumentException>(() => WardScorer.Score(new[] { 0, 1 }, new[] { 0 }));
        }
    }

    public class WardSessionTests
    {
        static readonly int[] MyWard = { 0, 1, 2 };

        static WardDuelSession Start(ulong seed, Essence[] pool = null, WardTuning t = null)
        {
            t = t ?? new WardTuning();
            var s = new WardDuelSession(t, pool ?? WardFormat.For(0, t), seed);
            Assert.IsTrue(s.SetPlayerWard(MyWard));
            return s;
        }

        // The rival Ward for a seed: forfeit a twin session and read the reveal.
        static int[] RivalWardOf(ulong seed, Essence[] pool = null)
        {
            var s = Start(seed, pool);
            s.Forfeit();
            return s.RevealedRivalWard;
        }

        static int[] Miss(int[] ward)
        {
            return ward.Select(x => (x + 1) % 3).ToArray();
        }

        // Player casts 'Miss' weaves as fast as allowed until out of casts; Mira runs on her own.
        static void PlayerExhausts(WardDuelSession s, int[] weave)
        {
            for (int i = 0; i < 10 && s.Phase == WardPhase.Active; i++)
            {
                var r = s.PlayerCast(weave);
                Assert.IsTrue(r.Ok, r.Message);
                s.Advance(2000);
            }
        }

        static void RunOut(WardDuelSession s)
        {
            for (int i = 0; i < 2000 && s.Phase == WardPhase.Active; i++) s.Advance(100);
        }

        [Test]
        public void SetupValidation()
        {
            var t = new WardTuning();
            var s = new WardDuelSession(t, WardFormat.For(0, t), 1);
            Assert.AreEqual(WardPhase.Setup, s.Phase);
            Assert.IsFalse(s.SetPlayerWard(new[] { 0, 1 }));
            Assert.IsFalse(s.SetPlayerWard(new[] { 0, 1, 3 })); // Light not in pool yet
            Assert.AreEqual(WardPhase.Setup, s.Phase);
            Assert.IsTrue(s.SetPlayerWard(MyWard));
            Assert.AreEqual(WardPhase.Active, s.Phase);
            Assert.AreEqual(10, s.PlayerCastsLeft);
            Assert.AreEqual(10, s.RivalCastsLeft);
        }

        [Test]
        public void Outcome_Victory()
        {
            var s = Start(7);
            var w = RivalWardOf(7);
            var r = s.PlayerCast(w);
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.Broken);
            Assert.AreEqual(WardPhase.Complete, s.Phase);
            Assert.AreEqual(WardOutcome.Victory, s.Outcome);
            CollectionAssert.AreEqual(MyWard, s.RevealedPlayerWard);
            CollectionAssert.AreEqual(w, s.RevealedRivalWard);
            Assert.IsInstanceOf<DuelEndedEvent>(s.DrainEvents().Last());
        }

        [Test]
        public void Outcome_Defeat_WhenMiraBreaksFirst()
        {
            bool found = false;
            for (ulong seed = 1; seed < 200 && !found; seed++)
            {
                var s = Start(seed);
                RunOut(s);
                if (s.Outcome != WardOutcome.Defeat) continue;
                found = true;
                Assert.AreEqual(WardPhase.Complete, s.Phase);
                Assert.IsNotNull(s.RevealedRivalWard);
                Assert.IsNotNull(s.RevealedPlayerWard);
                CollectionAssert.AreEqual(MyWard, s.RivalHistory.Last().Weave);
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void Outcome_PlayerExhaustedFirst_MiraKeepsCasting_ThenDefeat()
        {
            bool sawDefeat = false;
            // A quick player and a slow Mira, so the player runs out first.
            var t = new WardTuning { PlayerRecoveryMs = 100, MiraFirstCastMinMs = 5000, MiraFirstCastMaxMs = 5000, MiraThinkMinMs = 5000, MiraThinkMaxMs = 6000 };
            for (ulong seed = 1; seed < 200 && !sawDefeat; seed++)
            {
                var s = Start(seed, null, t);
                for (int i = 0; i < 10; i++)
                {
                    Assert.IsTrue(s.PlayerCast(Miss(RivalWardOf(seed))).Ok);
                    s.Advance(100);
                }
                if (s.Phase != WardPhase.Active) continue;
                Assert.AreEqual(0, s.PlayerCastsLeft);
                Assert.Greater(s.RivalCastsLeft, 0, "Mira still has casts");
                int before = s.RivalHistory.Count;
                RunOut(s);
                Assert.Greater(s.RivalHistory.Count, before, "Mira kept casting");
                if (s.Outcome == WardOutcome.Defeat) sawDefeat = true;
            }
            Assert.IsTrue(sawDefeat);
        }

        [Test]
        public void Outcome_Stalemate_BothExhausted()
        {
            // Default Mira almost always breaks in time, so give her a 100% mistake rate to reach exhaustion.
            var t = new WardTuning { MiraMistakeRatePercent = 100 };
            bool found = false;
            for (ulong seed = 1; seed < 200 && !found; seed++)
            {
                var s = Start(seed, null, t);
                PlayerExhausts(s, Miss(RivalWardOf(seed)));
                RunOut(s);
                if (s.Outcome != WardOutcome.Stalemate) continue;
                found = true;
                Assert.AreEqual(0, s.PlayerCastsLeft);
                Assert.AreEqual(0, s.RivalCastsLeft);
                Assert.IsNotNull(s.RevealedRivalWard);
                Assert.IsNotNull(s.RevealedPlayerWard);
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void Outcome_Clash_BothBreakInOneStep()
        {
            bool found = false;
            for (ulong seed = 1; seed < 3000 && !found; seed++)
            {
                var s = Start(seed);
                s.Advance(60000); // Mira casts through her casts; the idle auto-cast fires at 60 s
                if (s.Outcome != WardOutcome.Clash) continue;
                found = true;
                Assert.AreEqual(WardPhase.Complete, s.Phase);
                Assert.IsNotNull(s.RevealedRivalWard);
                Assert.IsNotNull(s.RevealedPlayerWard);
            }
            Assert.IsTrue(found, "no clash seed found");
        }

        [Test]
        public void Forfeit_RevealsAndIsNotCounted()
        {
            var s = Start(3);
            s.Forfeit();
            Assert.AreEqual(WardOutcome.Forfeited, s.Outcome);
            Assert.IsNotNull(s.RevealedRivalWard);
            var p = new WardProgress();
            p.Record(s.Outcome);
            Assert.AreEqual(0, p.CompletedDuelsVsMira);
        }

        [Test]
        public void Recovery_RefusesThenAllows()
        {
            var s = Start(11);
            var w = Miss(RivalWardOf(11));
            Assert.IsTrue(s.PlayerCast(w).Ok);
            Assert.AreEqual(2000, s.MsUntilPlayerMayCast);
            var r = s.PlayerCast(w);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("recovering", r.Reason);
            Assert.IsNotEmpty(r.Message);
            Assert.AreEqual(2000, r.MsUntilNextCast);
            Assert.AreEqual(9, s.PlayerCastsLeft);
            s.Advance(1999);
            Assert.IsFalse(s.PlayerCast(w).Ok);
            s.Advance(1);
            if (s.Phase == WardPhase.Active) Assert.IsTrue(s.PlayerCast(w).Ok);
        }

        [Test]
        public void Refusals_BadWeaveNoCastsNotActive()
        {
            var t = new WardTuning();
            var s = new WardDuelSession(t, WardFormat.For(0, t), 5);
            Assert.AreEqual("not_active", s.PlayerCast(MyWard).Reason);
            s.SetPlayerWard(MyWard);
            Assert.AreEqual("bad_weave", s.PlayerCast(new[] { 0, 1 }).Reason);
            Assert.AreEqual("bad_weave", s.PlayerCast(new[] { 0, 1, 3 }).Reason);
            Assert.AreEqual(10, s.PlayerCastsLeft);

            var t2 = new WardTuning { MaxCasts = 1 };
            var s2 = new WardDuelSession(t2, WardFormat.For(0, t2), 5);
            s2.SetPlayerWard(MyWard);
            var miss = Miss(RivalWardOf(5));
            // RivalWardOf uses default tuning; same seed and tuning loci so the Ward matches.
            Assert.IsTrue(s2.PlayerCast(miss).Ok);
            s2.Advance(2000);
            if (s2.Phase == WardPhase.Active) Assert.AreEqual("no_casts", s2.PlayerCast(miss).Reason);
        }

        [Test]
        public void IdleAutoCast_After60Seconds()
        {
            var t = new WardTuning { MiraFirstCastMinMs = 500000, MiraFirstCastMaxMs = 500000 };
            var s = Start(21, null, t);
            s.Advance(59999);
            Assert.AreEqual(10, s.PlayerCastsLeft);
            s.Advance(1);
            Assert.AreEqual(9, s.PlayerCastsLeft);
            var ev = s.DrainEvents().OfType<PlayerCastEvent>().ToList();
            Assert.AreEqual(1, ev.Count);
            Assert.IsTrue(ev[0].Auto);
            Assert.AreEqual(60000, ev[0].AtMs);
            Assert.AreEqual(1, s.PlayerHistory.Count);
        }

        [Test]
        public void Determinism_SameSeedSameInputs()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var a = Start(seed);
                var b = Start(seed);
                for (int i = 0; i < 400; i++)
                {
                    if (i % 25 == 0) { var w = new[] { i % 3, (i / 25) % 3, 2 }; Assert.AreEqual(a.PlayerCast(w).Ok, b.PlayerCast(w).Ok); }
                    a.Advance(137); b.Advance(137);
                }
                Assert.AreEqual(Dump(a), Dump(b));
            }
        }

        static string Dump(WardDuelSession s)
        {
            Func<IEnumerable<WardCastEntry>, string> h = l => string.Join(";", l.Select(e => string.Join("", e.Weave) + ":" + e.Fractures + e.Echoes + e.Fades));
            return s.Outcome + "|" + h(s.PlayerHistory) + "|" + h(s.RivalHistory) + "|" + s.ElapsedMs;
        }

        [Test]
        public void MiraTiming_WithinTunedBounds()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var s = Start(seed);
                s.Advance(4000);
                var first = s.DrainEvents().OfType<RivalCastEvent>().FirstOrDefault();
                Assert.IsNotNull(first);
                Assert.That(first.AtMs, Is.InRange(1500, 4000));
            }
        }

        [Test]
        public void RivalWard_NotExposedBeforeEnd()
        {
            var s = Start(9);
            Assert.IsNull(s.RevealedRivalWard);
            Assert.IsNull(s.RevealedPlayerWard);
            s.Advance(10000);
            if (s.Phase == WardPhase.Active) Assert.IsNull(s.RevealedRivalWard);
            // No public member other than the Revealed* properties hands out an int[].
            foreach (var m in typeof(WardDuelSession).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Type t = m is PropertyInfo p ? p.PropertyType : m is MethodInfo mi ? mi.ReturnType : m is FieldInfo f ? f.FieldType : null;
                if (t == typeof(int[]))
                    Assert.IsTrue(m.Name.StartsWith("Revealed") || m.Name.StartsWith("get_Revealed"), m.Name);
                Assert.IsFalse(m is FieldInfo, "no public fields: " + m.Name);
            }
            // The cast event and history types carry only the weave and totals of casts.
            Assert.IsNull(typeof(WardCastEntry).GetField("Ward"));
        }
    }

    public class WardRivalTests
    {
        static int[] RandomWard(Rng r, int loci, int poolSize)
        {
            var w = new int[loci];
            for (int i = 0; i < loci; i++) w[i] = r.NextInt(poolSize);
            return w;
        }

        // Returns casts needed to break (0 if not within maxCasts).
        static int Duel(ulong seed, Essence[] pool, bool checkConsistency)
        {
            var t = new WardTuning();
            var rng = new Rng(seed);
            var rival = new WardRival(t, pool, rng.Fork("r"));
            var ward = RandomWard(rng.Fork("w"), t.Loci, pool.Length);
            var past = new List<(int[] weave, int f, int e)>();
            for (int i = 1; i <= t.MaxCasts; i++)
            {
                var cast = rival.NextCast();
                if (checkConsistency && i > 1)
                    foreach (var p in past)
                    {
                        var s = WardScorer.Score(cast, p.weave);
                        Assert.AreEqual(p.f, s.fractures, "seed " + seed + " cast " + i);
                        Assert.AreEqual(p.e, s.echoes, "seed " + seed + " cast " + i);
                    }
                var sc = WardScorer.Score(ward, cast);
                if (sc.broken) return i;
                rival.Observe(cast, sc.fractures, sc.echoes);
                past.Add((cast, sc.fractures, sc.echoes));
            }
            return 0;
        }

        [Test]
        public void NeverCastsInconsistentWeave()
        {
            for (ulong seed = 1; seed <= 200; seed++) { Duel(seed, WardFormat.For(0, null), true); Duel(seed, WardFormat.For(3, null), true); }
        }

        [Test]
        public void OpeningSplitsTwoEssences()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var t = new WardTuning();
                var r = new WardRival(t, WardFormat.For(0, t), new Rng(seed));
                var w = r.NextCast();
                Assert.AreEqual(2, w.Distinct().Count(), string.Join(",", w));
            }
        }

        [Test]
        public void ResetsWhenNoCandidatesRemain()
        {
            var t = new WardTuning();
            var r = new WardRival(t, WardFormat.For(0, t), new Rng(1));
            r.NextCast();
            r.Observe(new[] { 0, 0, 1 }, 2, 1); // impossible feedback for a length-3 weave
            Assert.AreEqual(0, r.CandidateCount);
            var w = r.NextCast();
            Assert.AreEqual(3, w.Length);
            Assert.AreEqual(27, new WardRival(t, WardFormat.For(0, t), new Rng(1)).CandidateCount);
        }

        [Test]
        public void MistakeRate100_CastsRandomLegalWeaves()
        {
            var t = new WardTuning { MiraMistakeRatePercent = 100 };
            var r = new WardRival(t, WardFormat.For(0, t), new Rng(4));
            for (int i = 0; i < 20; i++)
            {
                var w = r.NextCast();
                Assert.AreEqual(3, w.Length);
                Assert.IsTrue(w.All(x => x >= 0 && x <= 2));
            }
        }

        [Test]
        public void RivalDeterministic()
        {
            var t = new WardTuning();
            var a = new WardRival(t, WardFormat.For(0, t), new Rng(77));
            var b = new WardRival(t, WardFormat.For(0, t), new Rng(77));
            for (int i = 0; i < 10; i++) CollectionAssert.AreEqual(a.NextCast(), b.NextCast());
        }

        [Test]
        public void BreaksWardWithinTenCasts_AtLeast95Percent()
        {
            foreach (var pool in new[] { WardFormat.For(0, null), WardFormat.For(3, null) })
            {
                int broken = 0, total = 0;
                for (ulong seed = 1; seed <= 200; seed++)
                {
                    int n = Duel(seed, pool, false);
                    if (n > 0) { broken++; total += n; }
                }
                double rate = broken / 200.0, avg = broken == 0 ? 0 : (double)total / broken;
                string msg = "Mira pool " + pool.Length + ": broke " + broken + "/200 (" + (rate * 100).ToString("F1") + "%), avg casts " + avg.ToString("F2");
                Console.WriteLine(msg);
                TestContext.Out.WriteLine(msg);
                Assert.GreaterOrEqual(rate, 0.95, msg);
            }
        }
    }

    public class WardFormatAndProgressTests
    {
        [Test]
        public void LightUnlocksOnFourthDuel()
        {
            var t = new WardTuning();
            for (int done = 0; done < 3; done++)
            {
                var p = WardFormat.For(done, t);
                CollectionAssert.AreEqual(new[] { Essence.Flame, Essence.Frost, Essence.Vine }, p);
            }
            CollectionAssert.AreEqual(new[] { Essence.Flame, Essence.Frost, Essence.Vine, Essence.Light }, WardFormat.For(3, t));
            Assert.AreEqual(4, WardFormat.For(10, t).Length);
        }

        [Test]
        public void EssenceNamesAndSymbols()
        {
            Assert.AreEqual("Flame", EssenceInfo.DisplayName(Essence.Flame));
            Assert.AreEqual(4, new HashSet<char>(Enum.GetValues(typeof(Essence)).Cast<Essence>().Select(EssenceInfo.Symbol)).Count);
        }

        [Test]
        public void TuningDefaults()
        {
            var t = new WardTuning();
            Assert.AreEqual(3, t.Loci);
            Assert.AreEqual(10, t.MaxCasts);
            Assert.AreEqual(2000, t.PlayerRecoveryMs);
            Assert.AreEqual(60000, t.IdleAutoCastMs);
            Assert.AreEqual(0, t.MiraMistakeRatePercent);
            Assert.AreEqual(3, t.LightUnlockAfterDuels);
        }

        [Test]
        public void Record_CountsOutcomes()
        {
            var p = new WardProgress();
            p.Record(WardOutcome.Victory);
            p.Record(WardOutcome.Defeat);
            p.Record(WardOutcome.Stalemate);
            p.Record(WardOutcome.Clash);
            p.Record(WardOutcome.Forfeited);
            Assert.AreEqual(4, p.CompletedDuelsVsMira);
            Assert.AreEqual(1, p.Wins);
            Assert.AreEqual(1, p.Losses);
            Assert.AreEqual(2, p.Draws);
        }

        [Test]
        public void Progress_RoundTripsInsideWorldState()
        {
            var sim = WorldSimFactory.CreateNew(9);
            sim.State.Ward.Record(WardOutcome.Victory);
            sim.State.Ward.Record(WardOutcome.Clash);
            var json = JsonConvert.SerializeObject(sim.State);
            var copy = JsonConvert.DeserializeObject<WorldState>(json);
            Assert.AreEqual(2, copy.Ward.CompletedDuelsVsMira);
            Assert.AreEqual(1, copy.Ward.Wins);
            Assert.AreEqual(1, copy.Ward.Draws);
            Assert.AreEqual(json, JsonConvert.SerializeObject(copy));
        }
    }
}
