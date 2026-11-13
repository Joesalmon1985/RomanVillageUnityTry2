using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Encounters.Rhythm;
using World.Sim.Encounters.Ward;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game.Tests
{
    /// <summary>Shared scene helpers for the Milestone 2 encounter tests.</summary>
    public abstract class EncounterTestBase
    {
        protected WorldBootstrap Boot;
        protected Vector3 SavedPos; protected float SavedYaw, SavedPitch; protected long SavedTick;

        protected IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.IsNotNull(Boot, "WorldBootstrap.Instance");
            Assert.IsTrue(EncounterDirectory.IsRegistered(EncounterDirectory.WardMira), "ward_mira registered");
            Assert.IsTrue(EncounterDirectory.IsRegistered(EncounterDirectory.RhythmParty), "rhythm_party registered");
        }

        [TearDown]
        public void TearDownEncounter()
        {
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
            WardDuelEncounter.TuningOverride = null; WardDuelEncounter.SeedOverride = null;
            RhythmPartyEncounter.TuningOverride = null; RhythmPartyEncounter.StemDirOverride = null;
        }

        protected static IEnumerator Until(Func<bool> cond, float timeout, string message)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(message + " (timed out after " + timeout + " s)");
                yield return null;
            }
        }

        protected void RememberAvatar()
        {
            SavedPos = Boot.Avatar.Position; SavedYaw = Boot.Avatar.Yaw; SavedPitch = Boot.Avatar.Pitch;
        }

        protected void AssertAvatarRestored(string what)
        {
            Assert.Less((Boot.Avatar.Position - SavedPos).magnitude, 1e-4f, what + ": position restored");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(Boot.Avatar.Yaw, SavedYaw)), 0.01f, what + ": yaw restored");
            Assert.Less(Mathf.Abs(Boot.Avatar.Pitch - SavedPitch), 0.01f, what + ": pitch restored");
            Assert.IsTrue(Boot.Avatar.CanMove, what + ": avatar free to move");
        }

        protected void AssertClockHeld(string what)
        {
            Assert.IsTrue(Boot.Clock.IsPausedFor(PauseReasons.Encounter), what + ": village clock paused");
            Assert.AreEqual(SavedTick, Boot.Sim.State.Tick, what + ": sim tick unchanged");
        }

        /// <summary>Walks up to an NPC (teleport), opens its dialogue and accepts option 0, the way the player does.</summary>
        protected IEnumerator Accept(string npcId, Vector3 standAt, float yaw)
        {
            Boot.Avatar.Teleport(standAt + Vector3.up * 0.05f, yaw, 3f);
            yield return null; yield return null;
            RememberAvatar();
            var npc = Npc.Find(npcId);
            npc.Interact();
            Assert.IsTrue(DialoguePanel.IsOpen, npcId + " dialogue open");
            Assert.IsTrue(DialoguePanel.Choose(0), npcId + " encounter option accepted");
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "encounter running");
            SavedTick = Boot.Sim.State.Tick;
            yield return null;
        }
    }

    /// <summary>G2 Ward duel in the scene: win, lose, rematch, return, Light on duel 4, abort, Tab.</summary>
    public class WardDuelTests : EncounterTestBase
    {
        static readonly int[] PlayerWard = { (int)Essence.Flame, (int)Essence.Flame, (int)Essence.Vine };

        static WardTuning SlowMira() => new WardTuning
        {
            PlayerRecoveryMs = 150, MiraFirstCastMinMs = 6000, MiraFirstCastMaxMs = 6000, MiraThinkMinMs = 6000, MiraThinkMaxMs = 6000,
        };

        static WardTuning FastMira() => new WardTuning
        {
            PlayerRecoveryMs = 150, MiraFirstCastMinMs = 200, MiraFirstCastMaxMs = 300, MiraThinkMinMs = 250, MiraThinkMaxMs = 350,
        };

        /// <summary>A seed for which Mira breaks the given Ward within her ten casts while the player never casts.</summary>
        static ulong MiraWinSeed(WardTuning t, int[] ward)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                var s = new WardDuelSession(t, WardFormat.For(0, t), seed);
                s.SetPlayerWard(ward);
                s.Advance(20000);
                if (s.Outcome == WardOutcome.Defeat) return seed;
            }
            throw new Exception("no seed found");
        }

        IEnumerator StartDuel()
        {
            var mira = Npc.Find(NpcIds.Mira);
            var d = WalkLattice.ToWorld(new CampLayout(Boot.Sim.State.Board).Spawn) - mira.transform.position; d.y = 0;
            yield return Accept(NpcIds.Mira, mira.transform.position + d.normalized * 2.6f, BoardSpace.YawOf(-d));
            Assert.IsNotNull(WardDuelEncounter.Current, "Ward duel is the running encounter");
        }

        static void Lock(WardDuelEncounter e, int[] ward)
        {
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase);
            foreach (var x in ward) Assert.IsTrue(e.AddEssence((Essence)x));
            Assert.IsFalse(e.AddEssence(Essence.Frost), "a Ward has three loci");
            Assert.IsTrue(e.RemoveLast()); Assert.IsTrue(e.AddEssence((Essence)ward[2]));
            Assert.IsTrue(e.LockWard());
            Assert.AreEqual(WardDuelEncounter.Screen.Active, e.Phase);
        }

        /// <summary>Candidate-filter player: casts whenever allowed, through AddEssence/Cast (the key path). Stops after maxCasts casts or at the end.</summary>
        static IEnumerator PlayToEnd(WardDuelEncounter e, int maxCasts = 10)
        {
            var solver = new WardRival(new WardTuning { Loci = e.Tuning.Loci }, e.Pool, new Rng(5));
            int observed = 0, casts = 0;
            while (e.Phase == WardDuelEncounter.Screen.Active)
            {
                var h = e.Session.PlayerHistory;
                while (observed < h.Count) { var x = h[observed++]; solver.Observe(x.Weave, x.Fractures, x.Echoes); }
                if (casts < maxCasts && e.Session.MsUntilPlayerMayCast == 0 && e.Session.PlayerCastsLeft > 0)
                {
                    foreach (var w in solver.NextCast()) e.AddEssence((Essence)w);
                    var r = e.Cast();
                    Assert.IsTrue(r.Ok, "cast refused: " + r.Message);
                    casts++;
                    if (casts >= maxCasts) yield break;
                }
                yield return null;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Duel_Win_Rematch_Lose_Return()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowMira();
            yield return StartDuel();
            var e = WardDuelEncounter.Current;
            Assert.AreEqual(3, e.Pool.Length, "first duel: three essences");
            Assert.IsTrue(e.HelpVisible, "first-time help shows during setup");
            Assert.IsNull(e.Session.RevealedRivalWard, "Mira's Ward hidden");
            Lock(e, PlayerWard);
            Assert.IsTrue(e.HelpVisible, "help shows during the first duel");
            yield return PlayToEnd(e);
            Assert.AreEqual(WardDuelEncounter.Screen.Result, e.Phase);
            Assert.AreEqual(WardOutcome.Victory, e.LastOutcome, "scripted player beats a slow Mira");
            Assert.IsTrue(e.Recorded);
            Assert.AreEqual(1, Boot.Sim.State.Ward.CompletedDuelsVsMira);
            Assert.AreEqual(1, Boot.Sim.State.Ward.Wins);
            Assert.IsNotNull(e.Session.RevealedRivalWard); Assert.IsNotNull(e.Session.RevealedPlayerWard);
            Assert.IsFalse(e.HelpVisible, "help gone after the first completed duel");
            AssertClockHeld("after the win");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(e.Stage.MiraWard.IsBroken, "Mira's Ward shatters when the breaking bolt lands");
            Assert.IsFalse(e.Stage.PlayerWard.IsBroken);
            AssertClockHeld("after the win animation");

            // Rematch: Mira fast, seed where she breaks the Ward; the player does not cast.
            var fast = FastMira();
            WardDuelEncounter.TuningOverride = fast;
            WardDuelEncounter.SeedOverride = MiraWinSeed(fast, PlayerWard);
            Assert.IsTrue(e.Rematch());
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase);
            Assert.IsFalse(e.Recorded);
            Lock(e, PlayerWard);
            yield return Until(() => e.Phase == WardDuelEncounter.Screen.Result, 15f, "Mira did not finish the duel");
            Assert.AreEqual(WardOutcome.Defeat, e.LastOutcome);
            Assert.AreEqual(2, Boot.Sim.State.Ward.CompletedDuelsVsMira);
            Assert.AreEqual(1, Boot.Sim.State.Ward.Losses);
            Assert.Greater(e.Session.RivalHistory.Count, 0);
            Assert.AreEqual(0, e.Session.PlayerHistory.Count);
            CollectionAssert.AreEqual(PlayerWard, e.Session.RevealedPlayerWard);
            AssertClockHeld("after the loss");

            Assert.IsTrue(e.Return());
            yield return Until(() => !EncounterHost.Instance.IsRunning, 2f, "duel did not end");
            AssertAvatarRestored("duel");
            Assert.IsFalse(Boot.Clock.IsPausedFor(PauseReasons.Encounter), "clock resumes");
            Assert.IsNull(WardDuelEncounter.Current);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Duel_LightOnFourth_AbortRecordsNothing()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowMira();
            for (int i = 0; i < 3; i++) WardProgressApi.RecordDuel(Boot.Sim.State, WardOutcome.Victory);
            yield return StartDuel();
            var e = WardDuelEncounter.Current;
            Assert.AreEqual(4, e.Pool.Length, "duel 4 offers Light");
            Lock(e, new[] { (int)Essence.Light, (int)Essence.Frost, (int)Essence.Vine });
            yield return PlayToEnd(e, 1); // one cast, then the duel stays open
            Assert.AreEqual(WardDuelEncounter.Screen.Active, e.Phase);
            Assert.AreEqual(1, e.Session.PlayerHistory.Count);
            // Esc -> pause menu -> "Leave encounter" (the menu button calls Abort then CloseMenu).
            Boot.Hud.OpenMenu();
            yield return null;
            int elapsed = e.Session.ElapsedMs;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(elapsed, e.Session.ElapsedMs, "duel time stands still while the pause menu is open");
            var session = e.Session;
            EncounterHost.Instance.Abort(); Boot.Hud.CloseMenu();
            yield return null;
            Assert.IsFalse(EncounterHost.Instance.IsRunning);
            Assert.AreEqual(WardOutcome.Forfeited, session.Outcome);
            Assert.AreEqual(3, Boot.Sim.State.Ward.CompletedDuelsVsMira, "abort records nothing");
            AssertAvatarRestored("aborted duel");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Duel_TabMidDuel_KeepsProgress()
        {
            yield return Load();
            WardDuelEncounter.TuningOverride = SlowMira();
            yield return StartDuel();
            var e = WardDuelEncounter.Current;
            Lock(e, PlayerWard);
            yield return PlayToEnd(e, 1);
            int left = e.Session.PlayerCastsLeft, mira = e.Session.RivalCastsLeft, t0 = e.Session.ElapsedMs;
            Boot.Views.Toggle();
            Assert.AreEqual(ViewMode.God, Boot.Views.CurrentView);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreSame(e, WardDuelEncounter.Current, "duel still running in God View");
            Assert.Greater(e.Session.ElapsedMs, t0, "duel clock keeps running in God View");
            // the duel still takes input in God View
            foreach (var w in new[] { Essence.Vine, Essence.Vine, Essence.Vine }) e.AddEssence(w);
            Assert.IsTrue(e.Cast().Ok, "cast in God View");
            Boot.Views.Toggle();
            yield return null;
            Assert.AreEqual(ViewMode.FirstPerson, Boot.Views.CurrentView);
            Assert.AreEqual(left - 1, e.Session.PlayerCastsLeft, "casts used unaffected by Tab");
            Assert.AreEqual(mira, e.Session.RivalCastsLeft);
            AssertClockHeld("Tab mid-duel");
        }
    }

    /// <summary>G2 rhythm party in the scene: DSP sync, perfect autoplayer (bar, then balcony on retry), held stage, failed final block, abort, Tab, synth fallback.</summary>
    public class RhythmPartyTests : EncounterTestBase
    {
        // F-0 interim: normal play has no warehouse; the tests stand one up (package D replaces the party with the dance circle).
        WarehouseSet Warehouse => WarehouseSet.Current;

        IEnumerator LoadWithWarehouse()
        {
            yield return Load();
            WarehouseSet.BuildStandalone(Boot);
            yield return null; yield return null;
        }

        IEnumerator StartParty()
        {
            var w = WarehouseSet.BuildStandalone(Boot);
            yield return null;
            yield return Accept(NpcIds.Host, w.DecksFrontPosition, w.DecksYaw + 180f);
            Assert.IsNotNull(RhythmPartyEncounter.Current, "rhythm party is the running encounter");
        }

        static double Grid(RhythmCue c) => c.Tick * 60.0 / (174.0 * 960.0);

        [UnityTest, Timeout(150000)]
        public IEnumerator Phrase_DspSync_PerfectAutoplay_Bar_ThenBalconyOnRetry()
        {
            yield return LoadWithWarehouse();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            bool doorBefore = Warehouse.IsDoorMusicOn;
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            Assert.IsFalse(Boot.GodCam.KeyboardPan, "God View keyboard pan off during the phrase");
            Assert.IsFalse(r.Stems.IsSynth, "owner's stems load from Resources");
            Assert.AreEqual(529655, r.Stems.Drums.samples);
            Assert.AreEqual(r.Player.StartDsp, r.Player.DrumsStartDsp, "drums start on song time 0");
            Assert.Less(r.SongTime, -1.9, "count-in: song time starts about 2 s negative");
            if (AudioGuard.CanWriteControls) Assert.AreEqual(4, r.Player.ClicksScheduled, "four count-in clicks");
            if (AudioGuard.CanWriteControls) Assert.IsFalse(Warehouse.IsDoorMusicOn, "door music off while the stems play");
            r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;

            bool tabbed = false, synced = false;
            double tabSong = 0; int tabJudged = 0; float tabReal = 0;
            while (r.Phase != RhythmPartyEncounter.Screen.Result)
            {
                double t = r.SongTime;
                if (!tabbed && t > 7.0)
                {
                    tabbed = true; tabSong = t; tabJudged = r.CuesJudged; tabReal = Time.realtimeSinceStartup;
                    Boot.Views.Toggle();
                    yield return new WaitForSecondsRealtime(0.5f);
                    Assert.AreEqual(ViewMode.God, Boot.Views.CurrentView);
                    Assert.GreaterOrEqual(r.CuesJudged, tabJudged, "cues judged kept across Tab");
                    Assert.AreEqual(tabSong + (Time.realtimeSinceStartup - tabReal), r.SongTime, 0.12, "song time keeps running across Tab");
                    Boot.Views.Toggle();
                }
                if (!synced && t > 9.0 && !r.Player.UsingRealtimeFallback && AudioGuard.CanWriteControls && r.Player.Drums.isPlaying)
                {
                    synced = true;
                    var d = r.Player.Drums;
                    double len = d.clip.samples / (double)d.clip.frequency;
                    double expected = ((AudioSettings.dspTime - r.Player.DrumsStartDsp) % len + len) % len;
                    double actual = d.timeSamples / (double)d.clip.frequency;
                    double diff = Math.Abs(expected - actual); diff = Math.Min(diff, len - diff);
                    Debug.Log("[RhythmPartyTests] drums playhead vs DSP schedule: " + (diff * 1000).ToString("0.0") + " ms");
                    Assert.Less(diff, 0.06, "drums play where the DSP schedule says (within one DSP buffer)");
                    if (r.Player.Bass.isPlaying && r.Player.Other.isPlaying)
                        Assert.LessOrEqual(Math.Abs(r.Player.Bass.timeSamples - r.Player.Other.timeSamples), 1024, "bass and other sample-aligned");
                }
                yield return null;
            }
            var res = r.LastResult;
            // DSP grid: every cue time sits on the 174 BPM grid; stems on the same grid.
            foreach (var c in r.Session.AllCues) Assert.Less(Math.Abs(c.Time - Grid(c)) * 1000, 1.0, "cue " + c.Index + " on the DSP grid");
            Assert.IsTrue(r.Session.HasBassReveal);
            Assert.AreEqual(r.Player.BassStartDsp, r.Player.OtherStartDsp, "bass and other share one scheduled start");
            if (AudioGuard.CanWriteControls)
                Assert.Less(Math.Abs(r.Player.BassStartDsp - r.Player.StartDsp - r.Session.BeatToSeconds(r.Session.BassRevealBeat)) * 1000, 1.0, "bass starts on the reveal downbeat");
            Assert.AreEqual(0, res.Great + res.Ok + res.Miss, "autoplayer at exact cue times: all perfect");
            Assert.AreEqual(0, res.HoldPartial);
            Assert.AreEqual(4, res.PassedBlocks);
            Assert.IsTrue(res.Success);
            Assert.AreEqual(RouteOutcome.Bar, res.RouteOpened);
            Assert.IsTrue(Warehouse.IsRouteOpen(World.Game.Route.Bar), "bar opened");
            Assert.IsFalse(Warehouse.IsRouteOpen(World.Game.Route.Balcony));
            Assert.Greater(r.BlocksCheered, 0, "cheer on passed blocks");
            AssertClockHeld("after the phrase");

            // Retry at once, choose the balcony.
            r.AutoRoute = SimRoute.Balcony;
            Assert.IsTrue(r.Retry());
            Assert.AreEqual(2, r.Attempts);
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "second phrase did not end");
            Assert.AreEqual(RouteOutcome.Balcony, r.LastResult.RouteOpened);
            Assert.AreEqual(0, r.LastResult.Miss);
            Assert.IsTrue(Warehouse.IsRouteOpen(World.Game.Route.Balcony), "balcony opened");
            Assert.IsTrue(Warehouse.IsRouteOpen(World.Game.Route.Bar), "bar stays open");
            AssertClockHeld("after the retry");

            Assert.IsTrue(r.Return());
            yield return Until(() => !EncounterHost.Instance.IsRunning, 2f, "party did not end");
            AssertAvatarRestored("party");
            Assert.IsTrue(Boot.GodCam.KeyboardPan, "keyboard pan restored");
            if (AudioGuard.CanWriteControls) Assert.AreEqual(doorBefore, Warehouse.IsDoorMusicOn, "door music restored");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phrase_MissFirstBlock_HoldsStage_StillSucceeds()
        {
            yield return LoadWithWarehouse();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            r.AutoPlayEnabled = true; r.AutoSkipBlock = b => b == 0;
            yield return Until(() => r.Session.StageOfBlock(1) >= 0, 15f, "block 0 not decided");
            Assert.IsFalse(r.Session.BlockResultOf(0).Passed, "block 0 missed");
            Assert.AreEqual(0, r.Session.StageOfBlock(1), "a miss holds the mix stage");
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "phrase did not end");
            var res = r.LastResult;
            Assert.AreEqual(3, res.PassedBlocks);
            Assert.IsTrue(res.Success, "three passes: party success");
            Assert.AreEqual(32, r.Session.BassRevealBeat, "bass revealed at beat 32 after an early miss");
            Assert.AreEqual(RouteOutcome.Bar, res.RouteOpened, "final block passed: default route opens");
            Assert.IsTrue(Warehouse.IsRouteOpen(World.Game.Route.Bar));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phrase_FailFinalBlock_NoRoute_RetryWorks_AbortStopsAudio()
        {
            yield return LoadWithWarehouse();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            r.AutoPlayEnabled = true; r.AutoSkipBlock = b => b == 3; r.AutoRoute = SimRoute.Balcony;
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "phrase did not end");
            var res = r.LastResult;
            Assert.IsFalse(res.FinalBlockPassed);
            Assert.AreEqual(3, res.PassedBlocks);
            Assert.IsTrue(res.Success, "three passes still count as a party success (R9)");
            Assert.AreEqual(RouteOutcome.None, res.RouteOpened, "no route without the final block");
            Assert.IsFalse(Warehouse.IsRouteOpen(World.Game.Route.Balcony));
            Assert.IsFalse(Warehouse.IsRouteOpen(World.Game.Route.Bar));
            Assert.IsTrue(res.CanRetry);

            Assert.IsTrue(r.Retry());
            Assert.AreEqual(RhythmPartyEncounter.Screen.CountIn, r.Phase);
            Assert.IsFalse(r.Session.IsEnded);
            r.AutoSkipBlock = null;
            yield return Until(() => r.SongTime > 1.0, 6f, "retry did not start playing");
            var drums = r.Player.Drums;
            if (AudioGuard.CanWriteControls && !r.Player.UsingRealtimeFallback) Assert.IsTrue(r.Player.AnyPlaying, "stems playing mid-phrase");
            long tick = Boot.Sim.State.Tick;
            EncounterHost.Instance.Abort();
            yield return null;
            Assert.IsTrue(drums == null, "stem sources destroyed on abort (audio stopped)");
            Assert.IsNull(RhythmPartyEncounter.Current);
            AssertAvatarRestored("aborted party");
            Assert.AreEqual(tick, SavedTick, "clock paused until the abort");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SynthFallback_WhenStemsMissing()
        {
            yield return LoadWithWarehouse();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            RhythmPartyEncounter.StemDirOverride = "Rhythm/no_such_pack";
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            Assert.IsTrue(r.Stems.IsSynth, "missing clips fall back to SynthAudio");
            Assert.AreEqual("lv1_warehouse_fallback", r.Chart.Id, "missing pack falls back to the built-in chart");
            foreach (var clip in new[] { r.Stems.Drums, r.Stems.Bass, r.Stems.Other })
            {
                Assert.AreEqual(SynthAudio.LoopFrames(), clip.samples, clip.name + " is 32 beats at 174 BPM");
                Assert.AreEqual(SynthAudio.Rate, clip.frequency);
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                double sum = 0; foreach (var x in data) sum += x * x;
                Assert.Greater(Math.Sqrt(sum / data.Length), 0.01, clip.name + " not silent");
            }
            // the synth kick lands on the chart's first kick (tick 0): loud attack, quieter just before the next beat
            var dd = new float[r.Stems.Drums.samples]; r.Stems.Drums.GetData(dd, 0);
            float Peak(int from, int n) { float m = 0; for (int i = from; i < from + n; i++) m = Mathf.Max(m, Mathf.Abs(dd[i])); return m; }
            int beat = (int)Math.Round(60.0 / 174 * SynthAudio.Rate);
            Assert.Greater(Peak(0, 2000), 2f * Peak(beat - 3000, 2000), "kick on beat 0");
            r.AutoPlayEnabled = true;
            yield return Until(() => r.SongTime > 1.5, 6f, "fallback phrase did not start");
            Assert.AreEqual(0, r.LiveMiss);
        }
    }
}
