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
            GodBoardSettings.Override = null;
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
            // On the walking lattice: the free cell nearest the asked spot, turned towards the villager; then a pointer
            // tap on them and their bubble's first reply (the player's real path since package P).
            var npc = Npc.Find(npcId);
            var cell = WalkGrid.NearestFree(WalkLattice.ToCell(standAt), 4);
            Assert.IsTrue(cell.HasValue, "a free cell near " + npcId);
            var from = WalkLattice.ToWorld(cell.Value);
            Boot.Avatar.StopAuto();
            Boot.Avatar.Teleport(cell.Value, WalkLattice.HeadingOf(BoardSpace.YawOf(npc.transform.position - from)));
            yield return null; yield return null;
            Assert.IsFalse(Boot.Avatar.IsOffLattice, "standing on the lattice");
            RememberAvatar();
            Assert.AreEqual(TapOutcome.Interact, PointerInput.TapObject(npc), npcId + " tapped");
            Assert.IsTrue(DialoguePanel.IsOpen && SpeechBubbles.IsOpen(DialoguePanel.Handle), npcId + " bubble open");
            Assert.IsTrue(SpeechBubbles.AnswerNearest(0), npcId + " encounter reply accepted");
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

        // ------------------------------------------------------------------ WU-7: the duel as world objects

        static WardTuning SlowRecovery() => new WardTuning
        {
            PlayerRecoveryMs = 2500, MiraFirstCastMinMs = 60000, MiraFirstCastMaxMs = 60000, MiraThinkMinMs = 60000, MiraThinkMaxMs = 60000,
        };

        [UnityTest, Timeout(120000)]
        public IEnumerator Stones_BuildTakeBackAndCast_RecoveryBlocksASecondCast_BeadsAndTallyMatchTheSession()
        {
            yield return Load();
            GodBoardSettings.Override = false;
            WardDuelEncounter.TuningOverride = SlowRecovery();
            yield return StartDuel();
            var e = WardDuelEncounter.Current;
            yield return null; yield return null;
            Assert.IsTrue(e.WorldFirst); Assert.IsNotNull(e.Props); Assert.IsTrue(e.Props.RigVisible, "the stones are in the view");
            Assert.AreEqual(3, e.Props.StoneCount, "three essences in the first duel: three stones");

            // Setup: tap three stones, take the middle orb back, add one: the Ward in the hand is Flame, Vine, Frost.
            yield return DuelWorldTaps.Build(e, Essence.Flame, Essence.Frost, Essence.Vine);
            DuelWorldTaps.AssertHand(e, Essence.Flame, Essence.Frost, Essence.Vine);
            yield return DuelWorldTaps.Tap(e, DuelTarget.Socket, 1);
            DuelWorldTaps.AssertHand(e, Essence.Flame, Essence.Vine);
            yield return DuelWorldTaps.Build(e, Essence.Frost);
            DuelWorldTaps.AssertHand(e, Essence.Flame, Essence.Vine, Essence.Frost);
            yield return DuelWorldTaps.Tap(e, DuelTarget.Stone, 0);
            Assert.AreEqual(3, e.Building.Count, "a full hand takes no fourth orb");
            // Locking the Ward is a preview first, a confirm second.
            yield return DuelWorldTaps.Tap(e, DuelTarget.Hand);
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase, "the first tap on the hand only previews");
            Assert.AreEqual("Lock this Ward?", LedgerCard.Content.Question);
            yield return new WaitForSecondsRealtime(0.32f);
            yield return DuelWorldTaps.Tap(e, DuelTarget.Hand);
            Assert.AreEqual(WardDuelEncounter.Screen.Active, e.Phase);
            CollectionAssert.AreEqual(new[] { (int)Essence.Flame, (int)Essence.Vine, (int)Essence.Frost }, e.PlayerWard);
            yield return new WaitForSecondsRealtime(0.65f);

            // Active: two orbs are not a weave (refused, nothing cast); three are, by preview then confirm.
            yield return DuelWorldTaps.Build(e, Essence.Vine, Essence.Frost);
            yield return DuelWorldTaps.Tap(e, DuelTarget.Hand);
            Assert.AreEqual(WorldActionState.Refused, e.Props.Machine.State);
            Assert.AreEqual(0, e.Session.PlayerHistory.Count);
            yield return DuelWorldTaps.Build(e, Essence.Flame);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return DuelWorldTaps.PreviewConfirm(e, DuelTarget.Hand);
            Assert.AreEqual(1, e.Session.PlayerHistory.Count, "one cast");
            CollectionAssert.AreEqual(new[] { (int)Essence.Vine, (int)Essence.Frost, (int)Essence.Flame }, e.Session.PlayerHistory[0].Weave);
            Assert.AreEqual(0, e.Building.Count, "the orbs flew");
            yield return null;
            Assert.AreEqual(e.Session.PlayerCastsLeft, e.Props.BeadsLit(true), "beads equal the casts left (caster)");
            Assert.AreEqual(e.Session.RivalCastsLeft, e.Props.BeadsLit(false), "beads equal the casts left (opponent)");

            // Recovery blocks a second cast: the tap is refused in words and nothing is cast.
            Assert.Greater(e.Session.MsUntilPlayerMayCast, 0, "the hand is recovering");
            yield return DuelWorldTaps.Build(e, Essence.Flame, Essence.Flame, Essence.Vine);
            yield return DuelWorldTaps.Tap(e, DuelTarget.Hand);
            Assert.AreEqual(WorldActionState.Refused, e.Props.Machine.State);
            StringAssert.Contains("recovering", LedgerCard.Content.Refusal);
            Assert.AreEqual(1, e.Session.PlayerHistory.Count, "no second cast while recovering");
            yield return Until(() => e.Session.MsUntilPlayerMayCast == 0, 4f, "recovery");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return DuelWorldTaps.PreviewConfirm(e, DuelTarget.Hand);
            Assert.AreEqual(2, e.Session.PlayerHistory.Count, "the second cast once the hand has recovered");
            yield return null;
            Assert.AreEqual(e.Session.PlayerCastsLeft, e.Props.BeadsLit(true));

            // The tally stick's pegs equal the history's totals, and each row is sorted (so no position can be read from it).
            int f = 0, ec = 0, fd = 0;
            foreach (var h in e.Session.PlayerHistory) { f += h.Fractures; ec += h.Echoes; fd += h.Fades; }
            CollectionAssert.AreEqual(new[] { f, ec, fd }, e.Props.TallyTotals(true), "tally totals equal PlayerHistory");
            for (int r = 0; r < 2; r++)
            {
                var row = e.Props.TallyRow(true, r);
                for (int k = 1; k < 3; k++) Assert.GreaterOrEqual(row[k], row[k - 1], "tally row " + r + " is sorted");
            }
            // The ledger of the stick says the same in words.
            yield return DuelWorldTaps.Tap(e, DuelTarget.TallyCaster);
            Assert.AreEqual(WorldActionState.Inspect, e.Props.Machine.State);
            StringAssert.Contains("In all: " + DuelWorldProps.Plural(f, "fracture", "fractures"), string.Join(" | ", LedgerCard.Content.Lines));
            Assert.IsNull(LedgerText.Violation(LedgerCard.Content));
            // The Ward of the opponent is never in the air before the end.
            Assert.IsNull(e.Props.RevealedInAir());
            Assert.IsNull(e.Session.RevealedRivalWard);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Duel_NoLocusInTheWorldBeforeTheEnd_ResultRevealsBothWardsInTheAir_RematchAndGateReturn()
        {
            yield return Load();
            GodBoardSettings.Override = false;
            WardDuelEncounter.TuningOverride = SlowMira();
            yield return StartDuel();
            var e = WardDuelEncounter.Current;
            yield return null; yield return null;
            Lock(e, PlayerWard);
            yield return PlayToEnd(e, 2);
            yield return null;
            // Before the end every word the world gives is a count or an essence name of a cast weave; none names a place.
            foreach (var t in new[] { DuelTarget.Referee, DuelTarget.TallyCaster, DuelTarget.TallyOpponent, DuelTarget.BeadsCaster, DuelTarget.BeadsOpponent, DuelTarget.Hand })
            {
                var words = string.Join(" | ", e.Props.LedgerWords(t)).ToLowerInvariant();
                Assert.IsNull(LedgerText.Violation(words), t + " speaks of dice or odds");
                foreach (var bad in new[] { "locus", "position", "first place", "second place", "third place", "left place", "middle place" })
                    StringAssert.DoesNotContain(bad, words, t + " names a place");
            }
            Assert.IsNull(e.Props.RevealedInAir(), "no Ward in the air before the end");
            // Finish the duel by casting to the end (the keys path), then the referee steps in.
            yield return PlayToEnd(e);
            Assert.AreEqual(WardDuelEncounter.Screen.Result, e.Phase);
            yield return null; yield return null;
            var air = e.Props.RevealedInAir();
            Assert.IsNotNull(air, "both Wards are revealed in the air at the end");
            var want = new System.Collections.Generic.List<int>(e.Session.RevealedPlayerWard); want.AddRange(e.Session.RevealedRivalWard);
            CollectionAssert.AreEqual(want, air);
            yield return Until(() => LedgerCard.IsOpen && LedgerCard.Content.Title == "The referee", 3f, "the referee speaks at the end");
            StringAssert.Contains("rematch", string.Join(" ", LedgerCard.Content.Lines).ToLowerInvariant());
            // Tap the caster: a rematch (preview, confirm); the gate then leaves (preview, confirm) and the player is back where he stood.
            yield return new WaitForSecondsRealtime(0.7f);
            var card = LedgerCard.LastRect; // a tap on the tablet closes it
            e.TapAt(new Vector2(card.center.x, Screen.height - card.center.y));
            Assert.IsFalse(LedgerCard.IsOpen, "a tap on the tablet closes it");
            yield return DuelWorldTaps.PreviewConfirm(e, DuelTarget.Caster);
            Assert.AreEqual(WardDuelEncounter.Screen.Setup, e.Phase, "tapping the caster rematches");
            yield return null;
            Assert.IsNull(e.Props.RevealedInAir(), "the Wards are hidden again");
            yield return DuelWorldTaps.Tap(e, DuelTarget.Gate);
            Assert.AreEqual("Leave the duel?", LedgerCard.Content.Question);
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "a preview leaves nothing");
            yield return new WaitForSecondsRealtime(0.32f);
            yield return DuelWorldTaps.Tap(e, DuelTarget.Gate);
            yield return Until(() => !EncounterHost.Instance.IsRunning, 2f, "the gate leaves the duel");
            AssertAvatarRestored("left by the gate");
            Assert.IsFalse(LedgerCard.IsOpen, "the ledger closes with the duel");
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

    /// <summary>
    /// The dance at the fire (REFACTOR_SPEC 2.E, package D): DSP sync, perfect autoplayer (Fire leap, then Spirit call on
    /// retry), held stage, failed final block, abort, synth fallback; the viewpoint is restored and the village clock
    /// stands still throughout.
    /// </summary>
    public class RhythmPartyTests : EncounterTestBase
    {
        CampLayout Camp => new CampLayout(Boot.Sim.State.Board);

        IEnumerator StartParty()
        {
            var sol = Npc.Find(NpcIds.Host);
            Assert.IsNotNull(sol, "Sol stands at the dance circle");
            Assert.IsNotNull(DanceCircle.Current, "the dance circle is built");
            var front = Camp.DanceCircleCentre + Camp.Forward * 3.2f + Camp.Right * 1.2f;
            yield return Accept(NpcIds.Host, front, BoardSpace.YawOf(sol.transform.position - front));
            Assert.IsNotNull(RhythmPartyEncounter.Current, "the dance is the running encounter");
            Assert.AreSame(DanceCircle.Current, RhythmPartyEncounter.Current.Circle, "danced on the camp's circle");
        }

        static double Grid(RhythmCue c) => c.Tick * 60.0 / (174.0 * 960.0);

        [UnityTest, Timeout(150000)]
        public IEnumerator Phrase_DspSync_PerfectAutoplay_FireLeap_ThenSpiritCallOnRetry()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            Assert.IsFalse(Boot.GodCam.KeyboardPan, "God View keyboard pan off during the phrase");
            Assert.IsFalse(r.Stems.IsSynth, "owner's stems load from Resources");
            Assert.AreEqual(529655, r.Stems.Drums.samples);
            Assert.AreEqual(r.Player.StartDsp, r.Player.DrumsStartDsp, "drums start on song time 0");
            Assert.Less(r.SongTime, -1.9, "count-in: song time starts about 2 s negative");
            if (AudioGuard.CanWriteControls) Assert.AreEqual(4, r.Player.ClicksScheduled, "four count-in clicks");
            Assert.Less(BoardSpace.Flat(Boot.Avatar.Position - Camp.DancerSpot), 0.05f, "viewpoint at the dancer's spot");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(Boot.Avatar.Yaw, Camp.DancerYaw)), 0.5f, "facing the fire");
            r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Bar;

            bool synced = false;
            while (r.Phase != RhythmPartyEncounter.Screen.Result)
            {
                double t = r.SongTime;
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
            Assert.IsTrue(r.BassJoined, "the bass joined after a good block");
            Assert.AreEqual(r.Player.BassStartDsp, r.Player.OtherStartDsp, "bass and other share one scheduled start");
            if (AudioGuard.CanWriteControls)
                Assert.Less(Math.Abs(r.Player.BassStartDsp - r.Player.StartDsp - r.Session.BeatToSeconds(r.Session.BassRevealBeat)) * 1000, 1.0, "bass starts on the reveal downbeat");
            Assert.AreEqual(0, res.Great + res.Ok + res.Miss, "autoplayer at exact cue times: all perfect");
            Assert.AreEqual(0, res.HoldPartial);
            Assert.AreEqual(4, res.PassedBlocks);
            Assert.IsTrue(res.Success);
            Assert.AreEqual(RouteOutcome.Bar, res.RouteOpened);
            Assert.AreEqual(DanceCircle.Effect.FireLeap, r.Circle.LastEffect, "Fire leap: the dancers leap the fire");
            Assert.Greater(r.BlocksCheered, 0, "cheer on passed blocks");
            AssertClockHeld("after the phrase");

            // Retry at once, choose the spirit call.
            r.AutoRoute = SimRoute.Balcony;
            Assert.IsTrue(r.Retry());
            Assert.AreEqual(2, r.Attempts);
            Assert.AreEqual(DanceCircle.Effect.None, r.Circle.LastEffect, "a retry clears the ending");
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "second phrase did not end");
            Assert.AreEqual(RouteOutcome.Balcony, r.LastResult.RouteOpened);
            Assert.AreEqual(0, r.LastResult.Miss);
            Assert.AreEqual(DanceCircle.Effect.SpiritCall, r.Circle.LastEffect, "Spirit call: blue smoke rises");
            AssertClockHeld("after the retry");

            Assert.IsTrue(r.Return());
            yield return Until(() => !EncounterHost.Instance.IsRunning, 2f, "party did not end");
            AssertAvatarRestored("party");
            Assert.IsTrue(Boot.GodCam.KeyboardPan, "keyboard pan restored");
            Assert.AreEqual(0, DanceCircle.Current.ActiveDancers, "dancers back at their seats");
            for (int p = 1; p <= 4; p++) Assert.IsFalse(DanceCircle.Current.IsLit(p), "drum " + p + " dark after the dance");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phrase_MissFirstBlock_HoldsStage_StillSucceeds()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            r.AutoPlayEnabled = true; r.AutoSkipBlock = b => b == 0;
            yield return Until(() => r.Session.StageOfBlock(1) >= 0, 15f, "block 0 not decided");
            Assert.IsFalse(r.Session.BlockResultOf(0).Passed, "block 0 missed");
            Assert.AreEqual(0, r.Session.StageOfBlock(1), "a miss holds the mix stage");
            Assert.Greater(r.LiveMiss, 0, "misses counted");
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "phrase did not end");
            var res = r.LastResult;
            Assert.AreEqual(3, res.PassedBlocks);
            Assert.IsTrue(res.Success, "three passes: party success");
            Assert.AreEqual(32, r.Session.BassRevealBeat, "bass revealed at beat 32 after an early miss");
            Assert.AreEqual(RouteOutcome.Bar, res.RouteOpened, "final block passed: the default ending (Fire leap)");
            Assert.AreEqual(DanceCircle.Effect.FireLeap, r.Circle.LastEffect);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Phrase_FailFinalBlock_NoEnding_RetryWorks_AbortStopsAudio()
        {
            yield return Load();
            RhythmPartyEncounter.TuningOverride = new RhythmTuning();
            yield return StartParty();
            var r = RhythmPartyEncounter.Current;
            r.AutoPlayEnabled = true; r.AutoSkipBlock = b => b == 3; r.AutoRoute = SimRoute.Balcony;
            yield return Until(() => r.Phase == RhythmPartyEncounter.Screen.Result, 40f, "phrase did not end");
            var res = r.LastResult;
            Assert.IsFalse(res.FinalBlockPassed);
            Assert.AreEqual(3, res.PassedBlocks);
            Assert.IsTrue(res.Success, "three passes still count as a party success (R9)");
            Assert.AreEqual(RouteOutcome.None, res.RouteOpened, "no ending without the final block");
            Assert.AreEqual(DanceCircle.Effect.None, r.Circle.LastEffect, "no leap and no spirits");
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
            Assert.AreEqual(0, DanceCircle.Current.ActiveDancers, "dancers back at their seats after the abort");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SynthFallback_WhenStemsMissing()
        {
            yield return Load();
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
