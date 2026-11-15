using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Encounters.Ward;
using World.Sim.Eras;

namespace World.Game.Tests
{
    /// <summary>
    /// The campaign duel (PRD_CRYSTALS I7): lethal, friendly and practice modes against the sim state; one resolve per
    /// attempt under double end events, exits and reloads; the restart after a mid-duel reload; no locus leak in the
    /// history, effects or audio; the phone layout inside the safe area with finger-sized targets. The lethal cases run on
    /// dev.historic with the authored pack, the Deepstone taken to Dolm's altar and Mira's leads followed to the Tally
    /// Vault threshold (the same helpers as the AutoPlay scripts).
    /// </summary>
    public class CampaignDuelTests : EncounterTestBase
    {
        const string Keeper = CampaignDuelScripts.KeeperEncounter;

        WorldState S => Boot.Sim.State;

        [SetUp]
        public void ResetDuel() => CampaignDuel.ResetSession();

        [TearDown]
        public void ResetDuelAfter()
        {
            CampaignDuel.ResetSession();
            DuelLayout.SafeAreaOverride = null;
            if (SaveSystem.PathOverride != null) { try { File.Delete(SaveSystem.PathOverride); } catch { } SaveSystem.PathOverride = null; }
        }

        // ------------------------------------------------------------------ helpers

        static WardTuning SlowOpponent() => new WardTuning
        {
            PlayerRecoveryMs = 150, MiraFirstCastMinMs = 60000, MiraFirstCastMaxMs = 60000, MiraThinkMinMs = 60000, MiraThinkMaxMs = 60000,
        };

        static WardTuning FastOpponent() => new WardTuning
        {
            PlayerRecoveryMs = 150, MiraFirstCastMinMs = 200, MiraFirstCastMaxMs = 300, MiraThinkMinMs = 250, MiraThinkMaxMs = 350,
        };

        static readonly int[] Ward = { (int)Essence.Flame, (int)Essence.Flame, (int)Essence.Vine };

        static Essence[] PoolOf(string profile) => CampaignDuel.Opponents().Spec(profile, 1).Pool;

        /// <summary>A seed for which the opponent breaks the given Ward while the player never casts.</summary>
        static ulong OpponentWinSeed(WardTuning t, Essence[] pool, int[] ward)
        {
            for (ulong seed = 1; seed < 400; seed++)
            {
                var s = new WardDuelSession(t, pool, seed);
                s.SetPlayerWard(ward);
                s.Advance(20000);
                if (s.Outcome == WardOutcome.Defeat) return seed;
            }
            throw new Exception("no seed found");
        }

        /// <summary>dev.historic, the authored campaign, the Deepstone on Dolm's altar, the party at the Tally Vault threshold.</summary>
        IEnumerator ToKeeperThreshold()
        {
            yield return Load();
            var old = Boot;
            Assert.IsTrue(DevStart.Begin(ScenarioStarts.DevHistoric), DevStart.LastError);
            yield return Until(() => WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == ScenarioStarts.DevHistoric, 90f, "dev.historic did not start");
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.IsNull(CampaignDuelScripts.StartAuthoredCampaign(), "StartCampaign");
            Assert.IsTrue(CampaignDuelScripts.StealToAltar(Element.Earth), "the Deepstone on Dolm's altar");
            string fail = null;
            yield return CampaignDuelScripts.DriveToThreshold(Keeper, 40000, m => fail = m);
            Assert.IsNull(fail, fail);
            Assert.AreEqual(PartyMode.AtThreshold, S.Campaign.Party.Mode);
        }

        WardDuelEncounter CommitAndStage()
        {
            Assert.IsNull(CampaignDuelScripts.Commit(Keeper), "CommitEncounter");
            var e = WardDuelEncounter.Current;
            Assert.IsNotNull(e, "the commit event staged the duel");
            Assert.IsTrue(e.IsLethal, "lethal mode");
            Assert.AreSame(e, EncounterHost.Instance.Current, "through the EncounterHost");
            return e;
        }

        static void LockWard(WardDuelEncounter e, int[] ward)
        {
            if (e.Phase == WardDuelEncounter.Screen.Stakes) Assert.IsTrue(e.BeginWard());
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase);
            foreach (var x in ward) Assert.IsTrue(e.AddEssence((Essence)x));
            Assert.IsTrue(e.LockWard());
            Assert.AreEqual(WardDuelEncounter.Screen.Active, e.Phase);
        }

        static IEnumerator PlayToEnd(WardDuelEncounter e)
        {
            var solver = new WardRival(new WardTuning { Loci = e.Tuning.Loci }, e.Pool, new Rng(5));
            int observed = 0;
            float end = Time.realtimeSinceStartup + 60f;
            while (e.Phase == WardDuelEncounter.Screen.Active)
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("the duel did not end");
                var h = e.Session.PlayerHistory;
                while (observed < h.Count) { var x = h[observed++]; solver.Observe(x.Weave, x.Fractures, x.Echoes); }
                if (e.Session.MsUntilPlayerMayCast == 0 && e.Session.PlayerCastsLeft > 0)
                {
                    foreach (var w in solver.NextCast()) e.AddEssence((Essence)w);
                    Assert.IsTrue(e.Cast().Ok);
                }
                yield return null;
            }
        }

        int Alive() => CampaignApi.Companions(S).Count(c => c.Alive);
        bool IsAlive(string id) => CampaignApi.Companions(S).First(c => c.Id == id).Alive;

        // ------------------------------------------------------------------ lethal

        [UnityTest, Timeout(240000)]
        public IEnumerator Lethal_Victory_StakesFirst_NoRematch_ResolvedOnceAtTheEndEvent()
        {
            yield return ToKeeperThreshold();
            int alive = Alive();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            var e = CommitAndStage();
            int attempt = e.Request.AttemptId;
            Assert.AreEqual(CampaignApi.Attempt(S).Id, attempt, "the request carries the pending attempt");
            Assert.AreEqual(CampaignApi.Attempt(S).Seed, e.Request.Seed, "the request carries the attempt's seed");
            Assert.AreEqual("keeper_earth", e.Request.ProfileId);
            Assert.AreEqual("Dolm", e.OpponentName);
            Assert.AreEqual(WardDuelEncounter.Screen.Stakes, e.Phase, "a lethal duel opens on the stakes card");
            StringAssert.Contains("Garrick will hold the way out", e.StakesText);
            Assert.IsNotNull(e.Stage.VolunteerActor, "the volunteer stands by");
            Assert.IsTrue(e.Stage.IsCampaign);
            Assert.AreEqual(NpcIds.Mira, e.Stage.CasterActor.ActorId, "Mira casts");
            Assert.AreEqual("keeper_earth", e.Stage.OpponentActor.ActorId);
            Assert.Less((e.Stage.CasterActor.Root.position - e.Request.Stage.CasterPos).magnitude, 0.01f, "Mira on the caster mark");
            Assert.Less((e.Stage.OpponentActor.Root.position - e.Request.Stage.OpponentPos).magnitude, 0.01f, "Dolm on his mark");
            AssertInFrame(e, "lethal stage");
            Assert.IsFalse(e.AddEssence(e.Pool[0]), "no Ward before the stakes are read");
            long tickBefore = S.Tick;

            LockWard(e, Ward);
            yield return PlayToEnd(e);
            Assert.AreEqual(WardDuelEncounter.Screen.Result, e.Phase);
            Assert.AreEqual(WardOutcome.Victory, e.LastOutcome);
            // Sent at the end event, applied by one clock step, before the presentation hold.
            Assert.AreEqual(DuelResult.Victory, e.Resolution);
            Assert.IsTrue(e.ResolutionSent);
            Assert.AreEqual(1, CampaignDuel.ResolvesSent);
            Assert.IsNull(CampaignApi.Attempt(S), "no attempt pending after the end event");
            Assert.AreEqual(tickBefore + 1, S.Tick, "exactly one clock step");
            Assert.AreEqual(1, S.Campaign.Resolved.Count);
            Assert.AreEqual(Custody.Carried, CampaignApi.Crystal(S, Element.Earth).Custody, "Dolm beaten: the Deepstone is carried");
            Assert.AreEqual(alive, Alive(), "nobody died");
            Assert.IsFalse(e.HoldOver, "the presentation hold runs");
            Assert.IsFalse(e.Return(), "the result cannot be left during the hold");
            Assert.IsFalse(e.Rematch(), "no rematch in a lethal duel");

            // A duplicate end event and a second resolve change nothing.
            e.SimulateEndEvent(WardOutcome.Defeat);
            Assert.AreEqual(WardOutcome.Victory, e.LastOutcome);
            Assert.IsFalse(CampaignDuel.SendResolve(attempt, DuelResult.Defeat), "a second resolve is not sent");
            var raw = Boot.Sim.Submit(new ResolveEncounter { AttemptId = attempt, Result = DuelResult.Defeat });
            Assert.IsFalse(raw.Ok, "the sim refuses a duplicate");
            Assert.AreEqual(CampaignRefusals.AlreadyResolved, raw.Reason);
            Assert.AreEqual(1, CampaignDuel.ResolvesSent);
            Assert.AreEqual(alive, Alive());

            yield return Until(() => e.HoldOver, 5f, "hold");
            Assert.IsTrue(e.Return());
            yield return Until(() => !EncounterHost.Instance.IsRunning, 3f, "the duel did not end");
            Assert.IsFalse(Boot.Clock.IsPausedFor(PauseReasons.Encounter), "the clock resumes");
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsNull(WardDuelEncounter.Current, "nothing restaged");
            Assert.AreEqual(0, CampaignDuel.RestartsSent);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Lethal_Defeat_VolunteerDiesOnce_DoubleEndEventChangesNothing()
        {
            yield return ToKeeperThreshold();
            int alive = Alive();
            long rival = S.Campaign.RivalProgress;
            var fast = FastOpponent();
            WardDuelEncounter.TuningOverride = fast;
            WardDuelEncounter.SeedOverride = OpponentWinSeed(fast, PoolOf("keeper_earth"), Ward);
            var e = CommitAndStage();
            int attempt = e.Request.AttemptId;
            LockWard(e, Ward);
            yield return Until(() => e.Phase == WardDuelEncounter.Screen.Result, 20f, "Dolm did not finish the duel");
            Assert.AreEqual(WardOutcome.Defeat, e.LastOutcome);
            Assert.AreEqual(DuelResult.Defeat, e.Resolution);
            Assert.IsFalse(IsAlive("garrick"), "the first volunteer falls");
            Assert.AreEqual(alive - 1, Alive());
            Assert.GreaterOrEqual(S.Campaign.RivalProgress, rival + 900, "the expedition gains 900");
            Assert.AreEqual(1, CampaignApi.Record(S).Fallen.Count);
            Assert.IsTrue(e.Stage.VolunteerHolding, "Garrick steps into the gap");

            e.SimulateEndEvent(WardOutcome.Defeat);
            e.SimulateEndEvent(WardOutcome.Victory);
            Assert.IsFalse(CampaignDuel.SendResolve(attempt, DuelResult.Defeat));
            Assert.AreEqual(1, CampaignDuel.ResolvesSent);
            Assert.AreEqual(1, S.Campaign.Resolved.Count);
            Assert.AreEqual(alive - 1, Alive(), "still one death");
            Assert.AreEqual(1, CampaignApi.Record(S).Fallen.Count);
            yield return Until(() => e.HoldOver, 5f, "hold");
            Assert.IsTrue(e.Return());
            yield return Until(() => !EncounterHost.Instance.IsRunning, 3f, "end");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Lethal_ConfirmedLeave_IsASurrender_CountsAsDefeat()
        {
            yield return ToKeeperThreshold();
            int alive = Alive();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            var e = CommitAndStage();
            LockWard(e, Ward);
            StringAssert.Contains("Garrick will pay for this. Leave?", CampaignDuel.SurrenderLine(e.Request));
            Assert.IsFalse(CampaignDuel.SurrenderArmed);
            CampaignDuel.ArmSurrender(); // the first press of "Leave encounter" in the pause menu
            Assert.IsTrue(CampaignDuel.SurrenderArmed);
            Assert.IsTrue(e.Surrender()); // the confirming press
            Assert.IsTrue(e.Surrendered);
            Assert.AreEqual(WardDuelEncounter.Screen.Result, e.Phase);
            Assert.AreEqual(DuelResult.Surrender, e.Resolution);
            Assert.AreEqual(1, CampaignDuel.ResolvesSent);
            Assert.IsFalse(IsAlive("garrick"), "surrender counts as a defeat");
            Assert.AreEqual(alive - 1, Alive());
            Assert.IsNull(CampaignApi.Attempt(S));
            Assert.IsFalse(e.Surrender(), "nothing to leave twice");
            yield return Until(() => e.HoldOver, 5f, "hold");
            Assert.IsTrue(e.Return());
            yield return Until(() => !EncounterHost.Instance.IsRunning, 3f, "end");
            Assert.AreEqual(1, S.Campaign.Resolved.Count);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Lethal_ExitAndReloadMidDuel_RestartWithNewSeed_NoPenalty_OneResolve()
        {
            yield return ToKeeperThreshold();
            int alive = Alive();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            var e1 = CommitAndStage();
            int attempt = e1.Request.AttemptId;
            ulong seed1 = e1.Request.Seed;
            long rival = S.Campaign.RivalProgress;
            LockWard(e1, Ward);
            var stageObject = e1.Stage.gameObject;

            // Exit mid-duel without the confirmed surrender (a host abort): no result, the attempt waits.
            EncounterHost.Instance.Abort();
            yield return null;
            Assert.IsTrue(stageObject == null, "the stage is gone: no stale effects");
            Assert.AreEqual(0, CampaignDuel.ResolvesSent);
            Assert.IsNotNull(CampaignApi.Attempt(S), "the attempt is still pending");
            Assert.AreEqual(alive, Alive(), "no penalty");
            // ... and is continued with RestartAttempt: a new seed, the same attempt.
            yield return Until(() => WardDuelEncounter.Current != null, 5f, "the attempt was not restarted");
            var e2 = WardDuelEncounter.Current;
            Assert.AreEqual(1, CampaignDuel.RestartsSent);
            Assert.AreEqual(attempt, e2.Request.AttemptId);
            Assert.AreNotEqual(seed1, e2.Request.Seed, "a new seed");
            Assert.AreEqual(1, CampaignApi.Attempt(S).Restarts);
            LockWard(e2, Ward);

            // Close the game mid-duel: the save holds the pending attempt; on loading, the duel is staged again.
            CampaignDuel.AutoRestart = false;
            EncounterHost.Instance.Abort();
            yield return null;
            SaveSystem.PathOverride = Path.Combine(Application.temporaryCachePath, "worldmvp_i7_test_save.json");
            Assert.IsTrue(Boot.Saves.Save(), SaveSystem.LastMessage);
            CampaignDuel.AutoRestart = true;
            var old = Boot;
            Assert.IsTrue(Boot.Saves.Load(), SaveSystem.LastMessage);
            yield return Until(() => WorldBootstrap.Instance != null && WorldBootstrap.Instance != old, 30f, "the save did not load");
            Boot = WorldBootstrap.Instance;
            yield return Until(() => WardDuelEncounter.Current != null, 10f, "the pending duel was not continued after the load");
            var e3 = WardDuelEncounter.Current;
            Assert.AreEqual(attempt, e3.Request.AttemptId);
            Assert.AreEqual(2, CampaignApi.Attempt(S).Restarts);
            Assert.AreNotEqual(e2.Request.Seed, e3.Request.Seed);
            Assert.AreEqual(alive, Alive(), "no penalty after the reload");
            Assert.Less(S.Campaign.RivalProgress - rival, 50, "no charge for the restarts");
            Assert.AreEqual(0, CampaignDuel.ResolvesSent);

            LockWard(e3, Ward);
            yield return PlayToEnd(e3);
            Assert.AreEqual(WardOutcome.Victory, e3.LastOutcome);
            Assert.AreEqual(1, CampaignDuel.ResolvesSent);
            Assert.AreEqual(1, S.Campaign.Resolved.Count);
            yield return Until(() => e3.HoldOver, 5f, "hold");
            e3.Return();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 3f, "end");
        }

        // ------------------------------------------------------------------ friendly and practice

        [UnityTest, Timeout(120000)]
        public IEnumerator Friendly_NoStakes_NoAttempt_NothingRecorded_Rematch()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            int completed = S.Ward.CompletedDuelsVsMira;
            Assert.IsTrue(CampaignDuel.StartRequest(CampaignDuel.Friendly(WardOpponents.FriendlyOrrin, "Orrin", "orrin")));
            long tick = S.Tick;
            var e = WardDuelEncounter.Current;
            Assert.IsTrue(e.IsFriendly && e.IsCampaignMode && !e.IsLethal);
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase, "no stakes card");
            Assert.IsNull(e.StakesText);
            Assert.IsNull(e.Stage.VolunteerActor, "nobody volunteers");
            Assert.AreEqual(NpcIds.Mira, e.Stage.CasterActor.ActorId);
            LockWard(e, Ward);
            yield return PlayToEnd(e);
            Assert.AreEqual(WardOutcome.Victory, e.LastOutcome);
            Assert.IsNull(e.Resolution);
            Assert.AreEqual(0, CampaignDuel.ResolvesSent);
            Assert.AreEqual(completed, S.Ward.CompletedDuelsVsMira, "a friendly duel is not practice");
            Assert.IsNull(S.Campaign, "no campaign state touched");
            yield return Until(() => e.HoldOver, 5f, "hold");
            Assert.IsTrue(e.Rematch(), "friendly duels have rematches");
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase);
            Assert.AreEqual(0, e.Stage.BoltsInFlight + e.Stage.CastsWindingUp, "no stale bolts after a rematch");
            Assert.IsFalse(e.Stage.OpponentWard.IsBroken);
            // "Leave encounter" in a friendly duel is harmless.
            Assert.IsTrue(e.Surrender());
            yield return null;
            Assert.IsFalse(EncounterHost.Instance.IsRunning);
            Assert.AreEqual(tick, S.Tick, "the clock never stepped");
            Assert.AreEqual(completed, S.Ward.CompletedDuelsVsMira);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Practice_AsARequest_IsTheShippedPracticeDuel()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            Assert.IsTrue(CampaignDuel.StartRequest(CampaignDuel.Practice()));
            var e = WardDuelEncounter.Current;
            Assert.IsFalse(e.IsCampaignMode);
            Assert.AreEqual(EncounterKind.Practice, e.Mode);
            Assert.AreEqual("Ward duel with Mira", e.Title);
            Assert.IsFalse(e.Stage.IsCampaign, "the player's wizard faces Mira");
            Assert.AreEqual("ward_player", e.Stage.PlayerActor.ActorId);
            Assert.AreEqual(3, e.Pool.Length);
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase);
            LockWard(e, Ward);
            yield return PlayToEnd(e);
            Assert.AreEqual(WardOutcome.Victory, e.LastOutcome);
            Assert.IsTrue(e.Recorded, "practice is recorded in WardProgress as today");
            Assert.AreEqual(1, S.Ward.CompletedDuelsVsMira);
            Assert.IsTrue(e.Return(), "no presentation gate in practice");
            yield return null;
            Assert.AreEqual(0, CampaignDuel.ResolvesSent);
        }

        // ------------------------------------------------------------------ staging

        [UnityTest, Timeout(120000)]
        public IEnumerator Staging_TakesPositionsFromTheRequest_InteriorSpot()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            // An interior lies far east of the island on the same lattice (I5); the duel uses the request's points only.
            var c = new Vector3(1500f, 0f, 40f);
            var spec = new DuelStageSpec
            {
                CasterActorId = NpcIds.Mira, OpponentActorId = "keeper_earth", VolunteerActorId = "companion_garrick",
                CasterPos = c, OpponentPos = c + new Vector3(0, 0, 5f), VolunteerPos = c + new Vector3(-6f, 0, -3f),
            };
            var req = new WardDuelRequest { Mode = EncounterKind.Friendly, ProfileId = "keeper_earth", OpponentName = "Dolm the Tally-Keeper", Stage = spec };
            RememberAvatar();
            Assert.IsTrue(CampaignDuel.StartRequest(req));
            var e = WardDuelEncounter.Current;
            Assert.Less((e.Stage.CasterActor.Root.position - spec.CasterPos).magnitude, 0.01f);
            Assert.Less((e.Stage.OpponentActor.Root.position - spec.OpponentPos).magnitude, 0.01f);
            Assert.Less((e.Stage.VolunteerActor.Root.position - WardDuelStage.StandByPoint(spec.CasterPos, spec.OpponentPos, spec.VolunteerPos)).magnitude, 0.01f);
            Assert.Less(Vector3.Dot(e.Stage.VolunteerActor.Root.position - c, Vector3.right), 0f, "on the way-out side (left)");
            Assert.Less((Boot.Avatar.Position - c).magnitude, 10f, "the camera went to the stage");
            AssertInFrame(e, "interior stage");
            var mira = Npc.Find(NpcIds.Mira);
            if (mira != null && mira.Actor is PlaceholderActor pa) Assert.IsFalse(pa.GetComponentInChildren<Renderer>().enabled, "the camp Mira steps aside");
            EncounterHost.Instance.Abort();
            yield return null;
            if (mira != null && mira.Actor is PlaceholderActor pb) Assert.IsTrue(pb.GetComponentInChildren<Renderer>().enabled, "the camp Mira is back");
            AssertAvatarRestored("after the interior duel");
        }

        /// <summary>Mira, the opponent and the volunteer are all inside the camera's view.</summary>
        void AssertInFrame(WardDuelEncounter e, string what)
        {
            var cam = Boot.MainCamera;
            var pts = new List<KeyValuePair<string, Vector3>>
            {
                new KeyValuePair<string, Vector3>("Mira", e.Stage.CasterActor.Root.position + Vector3.up * 1.2f),
                new KeyValuePair<string, Vector3>("opponent", e.Stage.OpponentActor.Root.position + Vector3.up * 1.5f),
            };
            if (e.Stage.VolunteerActor != null) pts.Add(new KeyValuePair<string, Vector3>("volunteer", e.Stage.VolunteerActor.Root.position + Vector3.up * 1.2f));
            foreach (var p in pts)
            {
                var v = cam.WorldToViewportPoint(p.Value);
                Assert.Greater(v.z, 0f, what + ": " + p.Key + " in front of the camera");
                Assert.That(v.x, Is.InRange(0f, 1f), what + ": " + p.Key + " in frame (x " + v.x + ")");
                Assert.That(v.y, Is.InRange(0f, 1f), what + ": " + p.Key + " in frame (y " + v.y + ")");
            }
            var mx = cam.WorldToViewportPoint(e.Stage.CasterActor.Root.position + Vector3.up * 1.2f).x;
            var ox = cam.WorldToViewportPoint(e.Stage.OpponentActor.Root.position + Vector3.up * 1.5f).x;
            Assert.Less(mx, ox, what + ": Mira left of the opponent, so it reads whose spell is being shaped");
        }

        // ------------------------------------------------------------------ no leak

        [UnityTest, Timeout(120000)]
        public IEnumerator NoLeak_TwoWeavesMatchingDifferentLoci_LookAndSoundTheSame()
        {
            yield return Load();
            var tuning = SlowOpponent();
            WardDuelEncounter.TuningOverride = tuning;
            const ulong seed = 4242;
            WardDuelEncounter.SeedOverride = seed;
            var pool = PoolOf(WardOpponents.FriendlyOrrin);
            var probe = new WardDuelSession(tuning, pool, seed);
            probe.SetPlayerWard(Ward);
            probe.Forfeit();
            int[] rival = probe.RevealedRivalWard;
            // Two weaves with the same totals whose fractures sit on different loci.
            int[] a = null, b = null;
            var all = new List<int[]>();
            foreach (var x in pool) foreach (var y in pool) foreach (var z in pool) all.Add(new[] { (int)x, (int)y, (int)z });
            foreach (var p in all)
            {
                var sp = WardScorer.Score(rival, p);
                if (sp.fractures == 0 || sp.broken) continue;
                foreach (var q in all)
                {
                    var sq = WardScorer.Score(rival, q);
                    if (sq != sp) continue;
                    bool samePlaces = true;
                    for (int i = 0; i < 3; i++) if ((p[i] == rival[i]) != (q[i] == rival[i])) samePlaces = false;
                    if (!samePlaces) { a = p; b = q; break; }
                }
                if (a != null) break;
            }
            Assert.IsNotNull(a, "two weaves with equal totals on different loci");

            yield return CastAndRecord(a); var logA = _record;
            yield return CastAndRecord(b); var logB = _record;
            Assert.IsNotNull(logA); Assert.IsNotNull(logB);
            CollectionAssert.AreEqual(logA, logB, "effects, sounds and cues are the same for both weaves");
        }

        /// <summary>Starts a friendly duel on the fixed seed, casts one weave, waits for the impact, returns every cue (stage log, struck cue, crack level, history totals).</summary>
        IEnumerator CastAndRecord(int[] weave)
        {
            _record = null;
            Assert.IsTrue(CampaignDuel.StartRequest(CampaignDuel.Friendly(WardOpponents.FriendlyOrrin, "Orrin", "orrin")));
            var e = WardDuelEncounter.Current;
            LockWard(e, Ward);
            foreach (var x in weave) e.AddEssence((Essence)x);
            Assert.IsTrue(e.Cast().Ok);
            yield return Until(() => e.CueLog.Count > 0, 5f, "no struck cue");
            var rec = new List<string>(e.Stage.Log);
            rec.AddRange(e.CueLog);
            rec.Add("crack " + e.Stage.OpponentWard.CrackLevel + " broken " + e.Stage.OpponentWard.IsBroken);
            var h = e.Session.PlayerHistory[0];
            rec.Add("history " + h.Fractures + "/" + h.Echoes + "/" + h.Fades);
            foreach (var line in rec)
                foreach (var word in new[] { "locus", "loci", "position", "first", "second", "third" })
                    StringAssert.DoesNotContain(word, line);
            EncounterHost.Instance.Abort();
            yield return null;
            _record = rec;
        }

        List<string> _record;

        // ------------------------------------------------------------------ layout

        static readonly (string name, float w, float h, Rect safe)[] Screens =
        {
            ("844x390 full", 844, 390, new Rect(0, 0, 844, 390)),
            ("844x390 notch", 844, 390, new Rect(47, 0, 750, 369)),
            ("932x430 full", 932, 430, new Rect(0, 0, 932, 430)),
            ("932x430 notch", 932, 430, new Rect(59, 0, 814, 409)),
            ("1920x1080", 1920, 1080, new Rect(0, 0, 1920, 1080)),
        };

        [Test]
        public void Layout_EveryRectInsideTheSafeArea_TouchTargetsAtLeast44()
        {
            foreach (var sc in Screens)
                foreach (bool help in new[] { true, false })
                {
                    var L = DuelLayout.Compute(sc.w, sc.h, sc.safe, help);
                    Assert.AreEqual(sc.h < 700, L.Compact, sc.name + ": layout kind");
                    foreach (var kv in L.AllRects())
                    {
                        if (kv.Value.width <= 0) continue;
                        string what = sc.name + " " + kv.Key + " " + kv.Value;
                        Assert.GreaterOrEqual(kv.Value.xMin, sc.safe.xMin - 0.5f, what + " left");
                        Assert.GreaterOrEqual(kv.Value.yMin, sc.safe.yMin - 0.5f, what + " top");
                        Assert.LessOrEqual(kv.Value.xMax, sc.safe.xMax + 0.5f, what + " right");
                        Assert.LessOrEqual(kv.Value.yMax, sc.safe.yMax + 0.5f, what + " bottom");
                    }
                    foreach (var kv in L.TouchTargets())
                    {
                        Assert.GreaterOrEqual(kv.Value.height, DuelLayout.MinTouch - 0.01f, sc.name + " " + kv.Key + " height");
                        Assert.GreaterOrEqual(kv.Value.width, DuelLayout.MinTouch - 0.01f, sc.name + " " + kv.Key + " width");
                    }
                    if (!L.Compact) continue;
                    // The pieces on screen together never overlap.
                    var during = new List<KeyValuePair<string, Rect>>
                    {
                        Kv("caster card", L.CasterPanel), Kv("opponent card", L.OpponentPanel), Kv("title", L.Title), Kv("builder", L.Builder),
                        Kv("toast", L.Toast), Kv("idle", L.Idle), Kv("cue", L.Cue),
                    };
                    if (help) during.Add(Kv("help", L.Help));
                    for (int i = 0; i < during.Count; i++)
                        for (int j = i + 1; j < during.Count; j++)
                            Assert.IsFalse(during[i].Value.Overlaps(during[j].Value), sc.name + ": " + during[i].Key + " overlaps " + during[j].Key);
                    for (int i = 0; i < 4; i++)
                        for (int j = i + 1; j < 4; j++) Assert.IsFalse(L.Essences[i].Overlaps(L.Essences[j]), sc.name + " essences " + i + "," + j);
                    // The middle band stays open for the two wizards.
                    float band = L.Builder.yMin - L.CasterPanel.yMax;
                    Assert.Greater(band, 100f, sc.name + ": room for the stage between the cards and the builder");
                }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Layout_LiveDuel_UsesTheSafeArea()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowOpponent();
            DuelLayout.SafeAreaOverride = new Rect(40, 0, Screen.width - 80, Screen.height - 20);
            Assert.IsTrue(CampaignDuel.StartRequest(CampaignDuel.Friendly(WardOpponents.FriendlyOrrin)));
            var e = WardDuelEncounter.Current;
            yield return null; yield return null;
            var L = e.LastLayout;
            Assert.IsNotNull(L, "the duel drew its panel");
            foreach (var kv in L.AllRects())
            {
                if (kv.Value.width <= 0) continue;
                Assert.GreaterOrEqual(kv.Value.xMin, 39.5f, kv.Key);
                Assert.LessOrEqual(kv.Value.xMax, Screen.width - 39.5f, kv.Key);
            }
            // Expandable history (compact) folds back.
            e.ToggleHistory(); Assert.IsTrue(e.HistoryExpanded);
            e.ToggleHistory(); Assert.IsFalse(e.HistoryExpanded);
            EncounterHost.Instance.Abort();
        }

        static KeyValuePair<string, Rect> Kv(string n, Rect r) => new KeyValuePair<string, Rect>(n, r);
    }
}
