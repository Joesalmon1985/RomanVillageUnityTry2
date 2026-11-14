using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Save;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// The campaign is saveable at any tick (CR-37, I1 acceptance): a run saved and loaded in the middle of a leg, an
    /// action, a choice, a threshold, a pending attempt, a rival carry, a rival pause, a scout's run, a wait at a tower or
    /// a restoration continues exactly as the unbroken run (same state JSON at the end). The player is a scripted one
    /// whose every decision reads only the state, so the loaded run gets the same answers at the same ticks.
    /// </summary>
    public class CampaignSaveTests
    {
        private const int End = 9000;

        private static void Try(WorldSim sim, Command c)
        {
            if (sim.Check(c).Ok) sim.Submit(c);
        }

        /// <summary>A direct player that steps away for a while: accepts the top lead, answers right, fights (loses the first duel), sets crystals.</summary>
        private static void Script(WorldSim sim)
        {
            var s = sim.State;
            var c = s.Campaign;
            if (c == null || c.Phase != CampaignPhase.Running) return;
            long t = s.Tick - c.StartTick;
            var p = c.Party;
            if (t == 1) Try(sim, new DevSetRivalProgress { Value = 4700 });           // the expedition is nearly at the quarry
            if (t == 300) Try(sim, new SuggestScout { LocationId = "forge" });
            if (t == 1500) CampaignRuntime.PauseRival(s, c, null);                     // as if an Ash camp fell
            if (t == 2500) Try(sim, new SetCompany { Present = false });
            if (t == 5000) Try(sim, new SetCompany { Present = true });
            if (!p.PlayerPresent) return;
            switch (p.Mode)
            {
                case PartyMode.AtThreshold:
                    if (s.Tick - p.WaitSinceTick >= 20) Try(sim, new CommitEncounter { EncounterId = p.EncounterId });
                    return;
                case PartyMode.InEncounter:
                    if (c.Attempt != null && s.Tick - c.Attempt.CommittedTick >= 30)
                        Try(sim, new ResolveEncounter { AttemptId = c.Attempt.Id, Result = c.Resolved.Count == 0 ? DuelResult.Defeat : DuelResult.Victory });
                    return;
                case PartyMode.AskingChoice:
                {
                    var ch = c.Definition.Choice(p.ChoiceId);
                    if (ch != null && s.Tick - p.WaitSinceTick >= 20) Try(sim, new AdviseMira { ChoiceId = ch.Id, Answer = ch.Answers.FindIndex(a => a.Correct) });
                    return;
                }
                case PartyMode.WaitingForPlayer:
                case PartyMode.Idle:
                {
                    foreach (Element e in Enum.GetValues(typeof(Element)))
                    {
                        var r = new ConfirmRestore { Crystal = e };
                        if (sim.Check(r).Ok) { sim.Submit(r); return; }
                    }
                    var top = c.Leads.OrderBy(l => l.Rank).FirstOrDefault();
                    if (top != null && s.Tick - top.TopSinceTick >= 30) Try(sim, new AcceptLead { LeadId = top.Id });
                    return;
                }
            }
        }

        private static WorldSim NewRun()
        {
            var d = CampaignSamples.Skeleton();
            d.Lead("air_watch").Crystal = "";   // so Orrin's riddle comes up, early
            d.Lead("air_watch").Priority = 70;
            return Sample(11, d);
        }

        private static void RunTo(WorldSim sim, long tick, Action<WorldSim> each = null)
        {
            while (sim.State.Tick < tick)
            {
                Script(sim);
                sim.Step();
                sim.DrainEvents();
                each?.Invoke(sim);
            }
        }

        [Test]
        public void SaveAndLoad_AtEveryKindOfInFlightState_ContinuesIdentically()
        {
            var kinds = new (string name, Func<CampaignState, bool> when)[]
            {
                ("surface leg", c => c.Party.Mode == PartyMode.Travelling && c.Party.Leg != null && c.Party.Leg.Progress > 0 && c.Party.Leg.Cells.Length == 0),
                ("interior leg", c => c.Party.Mode == PartyMode.Travelling && c.Party.Leg != null && c.Party.Leg.Progress > 0 && c.Party.Leg.Cells.Length > 0),
                ("acting", c => c.Party.Mode == PartyMode.Acting && c.Party.ActingProgress > 0 && c.Party.ActingAction != QuestAction.Restore),
                ("choice", c => c.Party.Mode == PartyMode.AskingChoice),
                ("threshold", c => c.Party.Mode == PartyMode.AtThreshold),
                ("pending attempt", c => c.Attempt != null),
                ("rival carry", c => c.Crystals.Any(x => x.Custody == Custody.RivalTransit)),
                ("rival paused", c => c.Rival.Phase == RivalPhase.Paused),
                ("scout out", c => c.Scout.Out && c.Scout.Phase == 1),
                ("waiting at a tower", c => c.Party.Mode == PartyMode.WaitingForPlayer),
                ("restoring", c => c.Party.Mode == PartyMode.Acting && c.Party.ActingAction == QuestAction.Restore && c.Party.ActingProgress > 0),
                ("player away", c => !c.Party.PlayerPresent),
            };
            var saves = new Dictionary<string, string>();
            var unbroken = NewRun();
            RunTo(unbroken, End, sim =>
            {
                foreach (var (name, when) in kinds)
                    if (!saves.ContainsKey(name) && sim.State.Tick < End - 500 && when(sim.State.Campaign)) saves[name] = SaveCodec.ToJson(sim.State);
            });
            string final = Json(unbroken);
            var missing = kinds.Select(k => k.name).Where(n => !saves.ContainsKey(n)).ToList();
            Assert.AreEqual(0, missing.Count, "states never reached: " + string.Join(", ", missing));
            Assert.Greater(unbroken.State.Campaign.Resolved.Count, 0, "the run fought");
            Assert.Greater(unbroken.State.Campaign.RestoredOrder.Count, 0, "the run restored");

            foreach (var kv in saves)
            {
                var loaded = SaveCodec.Load(kv.Value, out _);
                CampaignModule.Register(loaded);
                RunTo(loaded, End);
                Assert.AreEqual(final, Json(loaded), "loaded at: " + kv.Key);
            }

            // Determinism: a second unbroken run is the same run.
            var again = NewRun();
            RunTo(again, End);
            Assert.AreEqual(final, Json(again));
        }

        [Test]
        public void PendingAttempt_RestartOnContinue_NewSeedNoPenalty_SameInBothCopies()
        {
            var sim = CampaignOutcomeTests.KeeperHasEarth();
            int id = CampaignOutcomeTests.ToTheFight(sim);
            var s = sim.State;
            ulong seed = CampaignApi.Attempt(s).Seed;
            var a = SaveLoad(sim);
            var b = SaveLoad(sim);
            foreach (var x in new[] { a, b })
            {
                var ev = Do(x, new RestartAttempt { AttemptId = id });
                var again = ev.OfType<EncounterCommitted>().Single();
                Assert.AreEqual(id, again.AttemptId);
                Assert.AreNotEqual(seed, again.Seed);
                Assert.AreEqual(1, CampaignApi.Attempt(x.State).Restarts);
                Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count(), "no penalty");
                Assert.AreEqual(6, CampaignApi.Companions(x.State).Count(k => k.Alive));
                Do(x, new ResolveEncounter { AttemptId = id, Result = DuelResult.Defeat });
                x.Step(200);
            }
            Assert.AreEqual(Json(a), Json(b));
            Assert.AreEqual(5, CampaignApi.Companions(a.State).Count(k => k.Alive), "one death, once");
        }
    }
}
