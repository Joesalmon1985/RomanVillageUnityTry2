using System;
using NUnit.Framework;
using World.Sim.Encounters.Ward;

namespace World.Sim.Tests
{
    /// <summary>
    /// Mira's balance against a scripted first-time player (M2): a candidate-filter player (the same deduction Mira uses)
    /// with a small mistake rate, casting every 5 to 7 s. Target: the player wins roughly 40-60% of duels.
    /// </summary>
    public class WardBalanceTests
    {
        public struct PlayerModel { public int MinMs, MaxMs, MistakePercent; }

        public static readonly PlayerModel FirstTimer = new PlayerModel { MinMs = 5000, MaxMs = 7000, MistakePercent = 15 };

        /// <summary>Plays one duel to the end with 20 ms steps. Returns the outcome.</summary>
        public static WardOutcome Simulate(WardTuning t, ulong seed, PlayerModel m, int completedDuels = 0)
        {
            var pool = WardFormat.For(completedDuels, t);
            var s = new WardDuelSession(t, pool, seed);
            var r = new Rng(seed ^ 0xA5A5F00DUL);
            var me = new WardRival(new WardTuning { Loci = t.Loci, MiraMistakeRatePercent = m.MistakePercent }, pool, r.Fork("player"));
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

        public static double WinRate(WardTuning t, PlayerModel m, int seeds, int completedDuels, out double lose, out double draw)
        {
            int w = 0, l = 0, d = 0;
            for (int i = 0; i < seeds; i++)
            {
                var o = Simulate(t, (ulong)(7000 + i), m, completedDuels);
                if (o == WardOutcome.Victory) w++; else if (o == WardOutcome.Defeat) l++; else d++;
            }
            lose = (double)l / seeds; draw = (double)d / seeds;
            return (double)w / seeds;
        }

        [Test]
        public void Practice_FirstTimePlayer_WinsAbout40To60Percent()
        {
            var t = WardTuning.Practice();
            double w3 = WinRate(t, FirstTimer, 300, 0, out var l3, out _);
            double w4 = WinRate(t, FirstTimer, 300, 3, out var l4, out _);
            Console.WriteLine("practice tuning, first-time player: pool 3 win " + w3.ToString("P0") + " lose " + l3.ToString("P0") +
                              "; pool 4 win " + w4.ToString("P0") + " lose " + l4.ToString("P0"));
            Assert.That(w3, Is.InRange(0.40, 0.60), "three-essence win rate");
            Assert.That(w4, Is.InRange(0.40, 0.65), "four-essence win rate");
        }

        [Test]
        public void PrdCadence_IsTooFastForAFirstTimer()
        {
            // documents why the practice cadence exists: with the PRD defaults (2.5-6 s) a first-timer rarely wins
            double w = WinRate(new WardTuning(), FirstTimer, 200, 0, out _, out _);
            Assert.Less(w, 0.25);
        }

        [Test]
        public void Practice_ScriptedPlayer_IsDeterministic()
        {
            var t = WardTuning.Practice();
            for (ulong seed = 1; seed < 20; seed++)
                Assert.AreEqual(Simulate(t, seed, FirstTimer), Simulate(t, seed, FirstTimer));
        }

        [Test]
        public void TuningFromJson_OverridesOnlyGivenFields()
        {
            var t = WardTuning.FromJson("{ \"MiraThinkMinMs\": 7000, \"MiraMistakeRatePercent\": 10 }");
            Assert.AreEqual(7000, t.MiraThinkMinMs);
            Assert.AreEqual(10, t.MiraMistakeRatePercent);
            Assert.AreEqual(WardTuning.Practice().MiraThinkMaxMs, t.MiraThinkMaxMs);
            Assert.AreEqual(2000, t.PlayerRecoveryMs);
            Assert.AreEqual(WardTuning.Practice().MiraThinkMinMs, WardTuning.FromJson(null).MiraThinkMinMs);
        }

        [Test]
        public void ProgressApi_RecordsCompletedDuels_IgnoresForfeits_AndUnlocksLight()
        {
            var sim = WorldSimFactory.CreateNew(5);
            var s = sim.State;
            var t = WardTuning.Practice();
            Assert.AreEqual(3, WardProgressApi.NextPool(s, t).Length);
            Assert.IsFalse(WardProgressApi.RecordDuel(s, WardOutcome.Forfeited));
            Assert.IsFalse(WardProgressApi.RecordDuel(s, WardOutcome.None));
            Assert.IsTrue(WardProgressApi.RecordDuel(s, WardOutcome.Victory));
            Assert.IsTrue(WardProgressApi.RecordDuel(s, WardOutcome.Defeat));
            Assert.AreEqual(3, WardProgressApi.NextPool(s, t).Length, "duel 3 still three essences");
            Assert.IsTrue(WardProgressApi.RecordDuel(s, WardOutcome.Clash));
            Assert.AreEqual(4, WardProgressApi.NextPool(s, t).Length, "duel 4 offers Light");
            Assert.AreEqual(3, s.Ward.CompletedDuelsVsMira);
            Assert.AreEqual(1, s.Ward.Wins); Assert.AreEqual(1, s.Ward.Losses); Assert.AreEqual(1, s.Ward.Draws);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(Newtonsoft.Json.JsonConvert.SerializeObject(s));
            Assert.AreEqual(3, back.Ward.CompletedDuelsVsMira);
        }
    }
}
