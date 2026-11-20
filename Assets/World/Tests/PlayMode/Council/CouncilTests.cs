using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Encounters.Council;
using L = World.Game.CouncilLayout;

namespace World.Game.Tests
{
    /// <summary>
    /// The council's Unity half: hit targets inside the screen and distinct at two sizes, the exact crowd counts per place,
    /// normal-size people, nothing floating, one move per accepted tap, doubles, listeners versus settled matching the engine, bust returning only the unsettled, the petition, listening with timing,
    /// unchanged draws after visiting the bench, leave and rejoin, save and load mid-push, no election below four
    /// residents, camera and clock restored, the approach, and the AutoPlay scripts.
    /// </summary>
    public class CouncilTests : EncounterTestBase
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            PrinceInstaller.Disabled = true; WorldActions.Cancel(); LedgerCard.Hide(); CouncilScene.ForceCloser = false;
            CouncilEncounter.AiDelay = 0.05f; CouncilEncounter.IntroSeconds = 0.4f;
            _savePath = Path.Combine(Application.temporaryCachePath, "worldmvp_council_test_save.json");
            if (File.Exists(_savePath)) File.Delete(_savePath);
            SaveSystem.PathOverride = _savePath;
            SaveSystem.TestPauseOnLoad = "TestHold";
        }

        [TearDown]
        public void TearDownCouncil()
        {
            WorldActions.Cancel(); LedgerCard.Hide(); CouncilScene.ForceCloser = null;
            PrinceInstaller.Disabled = false; CouncilDirector.Disabled = false; CouncilDirector.ApproachDelay = 12f;
            CouncilEncounter.AiDelay = 0.75f; CouncilEncounter.IntroSeconds = 2.4f;
            SaveSystem.PathOverride = null; SaveSystem.TestPauseOnLoad = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        int Home => CouncilDirector.Instance.HomeSettlement;
        SettlementCouncil Sc => CouncilApi.Get(Boot.Sim.State, Home);
        ElectionState E => Sc.Election;
        CouncilEncounter Enc => CouncilEncounter.Current;

        IEnumerator Stage(bool open = true, int seat = 0, ulong? seed = null)
        {
            yield return Load();
            CouncilDirector.Disabled = true;
            Boot.Clock.Pause("TestHold");
            if (seed.HasValue) Boot.Sim.State.Seed = seed.Value;
            CouncilFixture.StageExample(Boot.Sim.State, Home, seat);
            if (open)
            {
                Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home), "the council opened");
                yield return WaitReady();
            }
        }

        IEnumerator WaitReady()
        {
            float end = Time.realtimeSinceStartup + 20f;
            yield return new WaitForSecondsRealtime(0.6f);
            while (Time.realtimeSinceStartup < end && (Enc == null || !Enc.Scene.Settled || Enc.InTransition)) yield return null;
            Assert.IsNotNull(Enc, "the council is open");
        }

        // ---------------------------------------------------------------- layout

        [Test]
        public void Layout_LeftViewTargets_InsideSafeArea_AndNotOverlapping_At1080p_And_Phone()
        {
            foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(844, 390) })
            {
                var safe = L.SafeRect(size.x, size.y);
                var px = new List<KeyValuePair<string, Rect>>();
                foreach (var kv in L.LeftTargets()) px.Add(new KeyValuePair<string, Rect>(kv.Key, L.ToPixels(kv.Value, size.x, size.y)));
                px.Add(new KeyValuePair<string, Rect>("rightlantern", L.ToPixels(L.RightLantern, size.x, size.y)));
                foreach (var kv in px)
                {
                    Assert.GreaterOrEqual(kv.Value.xMin, safe.xMin - 0.01f); Assert.LessOrEqual(kv.Value.xMax, safe.xMax + 0.01f);
                    Assert.GreaterOrEqual(kv.Value.yMin, safe.yMin - 0.01f); Assert.LessOrEqual(kv.Value.yMax, safe.yMax + 0.01f);
                }
                for (int i = 0; i < px.Count; i++) for (int j = i + 1; j < px.Count; j++) Assert.IsFalse(px[i].Value.Overlaps(px[j].Value), px[i].Key + " overlaps " + px[j].Key);
            }
        }

        /// <summary>The scene's targets as a camera of the given pixel size sees them (a render-texture copy of the encounter camera).</summary>
        List<TapTarget> TargetsAt(int w, int h, out Camera cam)
        {
            var go = new GameObject("TestCam");
            cam = go.AddComponent<Camera>();
            cam.CopyFrom(Camera.main != null ? Camera.main : Enc.Scene.ViewCamera);
            cam.transform.SetPositionAndRotation(Enc.Scene.ViewCamera.transform.position, Enc.Scene.ViewCamera.transform.rotation);
            cam.targetTexture = new RenderTexture(w, h, 16);
            cam.fieldOfView = VerticalFovFor(w, h);
            return Enc.Scene.Targets(cam);
        }

        float VerticalFovFor(int w, int h)
        {
            // the same horizontal coverage the game camera has on its own screen
            float aspectGame = Mathf.Max(0.5f, (float)Screen.width / Screen.height);
            float hfov = 2f * Mathf.Atan(Mathf.Tan(Enc.CamFov * 0.5f * Mathf.Deg2Rad) * aspectGame);
            return 2f * Mathf.Atan(Mathf.Tan(hfov * 0.5f) / ((float)w / h)) * Mathf.Rad2Deg;
        }

        void CheckTargets(string phase)
        {
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(844, 390) })
            {
                var l = TargetsAt(size.x, size.y, out var cam);
                Assert.Greater(l.Count, 0, phase + ": there are targets at " + size);
                float minGap = 0.04f * size.y;
                for (int i = 0; i < l.Count; i++)
                {
                    Assert.IsTrue(l[i].Px.x >= 0 && l[i].Px.x <= size.x && l[i].Px.y >= 0 && l[i].Px.y <= size.y, phase + ": " + l[i].Name + " on screen at " + size + " " + l[i].Px);
                    for (int j = i + 1; j < l.Count; j++)
                        Assert.GreaterOrEqual((l[i].Px - l[j].Px).magnitude, l[i].Name.StartsWith("place") && l[j].Name.StartsWith("place") ? 0.02f * size.y : minGap, phase + ": " + l[i].Name + " and " + l[j].Name + " apart at " + size);
                }
                UnityEngine.Object.Destroy(cam.targetTexture); UnityEngine.Object.Destroy(cam.gameObject);
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Layout_HitTargets_OnScreenAndDistinct_At1080p_And_Phone()
        {
            yield return Stage();
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "three notes held out");
            CheckTargets("choosing");
            Assert.IsTrue(Enc.TapTargetNow("note 0")); yield return WaitReady();
            yield return Until(() => Enc.Scene.Targets(Enc.Scene.ViewCamera).Exists(t => t.Name == "seal"), 10f, "ready targets");
            CheckTargets("ready");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Crowd_ExactCountsPerPlace_LeadsIncluded_AtNormalSize()
        {
            yield return Stage();
            var want = new[] { 3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 };
            int total = 0;
            for (int t = 2; t <= 12; t++)
            {
                var ppl = Enc.Scene.People(t);
                Assert.AreEqual(want[t - 2], ppl.Count, "people at place " + t);
                int leads = 0; foreach (var p in ppl) { if (p.IsLead) leads++; Assert.That(p.A.transform.localScale.y, Is.InRange(0.9f, 1.1f), "normal world size"); }
                Assert.AreEqual(1, leads, "one lead at place " + t);
                Assert.IsTrue(ppl[ppl.Count - 1].IsLead, "the lead is the last to be won");
                total += ppl.Count;
            }
            Assert.AreEqual(83, total);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Scene_HasNothingFloating_OrScreenSpace()
        {
            yield return Stage();
            var root = Enc.Scene.Root;
            Assert.AreEqual(0, root.GetComponentsInChildren<Canvas>(true).Length, "no canvases in the village");
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<PlaceholderActor>() != null) continue;
                string n = r.transform.root == r.transform ? r.name : r.transform.name;
                if (r.GetComponentInParent<CouncilBench>() != null) continue;
                var hold = r.transform; bool held = false;
                while (hold != null && hold != root) { if (hold.name.StartsWith("Note") || hold.name == "Belt seal" || hold.name == "Petition" || hold.name == "Bench lantern") held = true; hold = hold.parent; }
                if (held) continue;
                Assert.IsFalse(r.name.Contains("Marker") || r.name.Contains("Banner") || r.name.Contains("Emblem"), "no floating marks: " + r.name);
                Assert.Less(r.bounds.min.y - Enc.Scene.GroundY, 9.0f, r.name + " stands on the ground, not in the air");
            }
        }

        // ---------------------------------------------------------------- the example trays and moves

        [UnityTest, Timeout(120000)]
        public IEnumerator Example_ShowsTheThreeTrays_4and9_5and8_6and7()
        {
            yield return Stage();
            var sums = new List<string>();
            foreach (var t in E.Pending) { var a = t.Sockets[0].Track; var b = t.Sockets[1].Track; sums.Add(Math.Min(a, b) + "+" + Math.Max(a, b)); }
            sums.Sort();
            CollectionAssert.AreEqual(new[] { "4+9", "5+8", "6+7" }, sums);
            foreach (var t in E.Pending) Assert.AreEqual(TrayMode.Both, t.Mode, "tapping a note commits both advances");
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "three notes");
            foreach (var p in Enc.Scene.Proposals) Assert.AreEqual(2, p.Steps.Count, "each note names two places");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TrayTap_CommitsExactlyOneMove_UnderRepeatedTaps()
        {
            yield return Stage();
            int adv0 = E.Advances;
            for (int i = 0; i < 6; i++) Enc.TapTargetNow("note 1"); // six taps in one frame, then more over time
            for (int i = 0; i < 20; i++) { yield return null; Enc.TapTargetNow("note " + (i % 3)); Enc.TapTargetNow("hand"); }
            yield return WaitReady();
            Assert.AreEqual(1, E.Advances - adv0, "exactly one move committed");
            int moved = 0; foreach (int p in E.Temp) if (p > 0) moved++;
            Assert.AreEqual(2, moved, "both places of the note moved a catspaw");
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "the view equals the state");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DoubleSum_AdvancesOneCatspawTwoPlaces()
        {
            yield return Stage();
            CouncilFixture.StageValues(E, 2, 2, 3, 3); // pairings give 4+6 and 5+5 (a double)
            yield return null;
            int tray = -1;
            for (int i = 0; i < E.Pending.Count; i++) if (E.Pending[i].Sockets[0].Track == 5 && E.Pending[i].Sockets[1].Track == 5) tray = i;
            Assert.GreaterOrEqual(tray, 0, "a tray with two copies of dispute 5");
            yield return WaitReady();
            yield return Until(() => Enc.Scene.Proposals.Exists(pp => pp.Tray == tray), 10f, "the double is held out as a note");
            Assert.IsTrue(Enc.TapTargetNow("note " + Enc.Scene.Proposals.FindIndex(pp => pp.Tray == tray)));
            yield return WaitReady();
            Assert.AreEqual(2, E.Temp[5 - 2], "the catspaw on dispute 5 moved two places");
            Assert.AreEqual(1, E.CatspawsInPlay, "one catspaw");
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "the view shows the catspaw at place 2");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ListenersVersusSettled_MatchTheEngine_AfterEachEvent()
        {
            yield return Stage();
            Enc.TapTargetNow("note 0");
            yield return WaitReady();
            Assert.AreEqual(2, Enc.Scene.AgentsShown, "two catspaws at the places being worked");
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "after the advance");
            int listeners = 0, settled = 0;
            for (int t = 2; t <= 12; t++) { Enc.Scene.Counts(t, out int s, out int l, out _); listeners += l; settled += s; }
            Assert.AreEqual(2, listeners, "one person won at each of the two places, listening, not settled");
            Assert.AreEqual(0, settled, "nothing banked yet");
            CouncilEncounter.AiDelay = 600f;
            Assert.IsTrue(Enc.TapTargetNow("seal"));
            yield return WaitReady();
            yield return Until(() => Enc.Scene.AgentsShown == 0, 10f, "catspaws gone after the bank");
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "after the bank");
            listeners = 0; settled = 0;
            for (int t = 2; t <= 12; t++) { Enc.Scene.Counts(t, out int s, out int l, out _); listeners += l; settled += s; }
            // the turn has passed to a rival: the village shows the rival's standing, which has no banked support yet
            Assert.AreEqual(0, listeners);
            Assert.AreNotEqual(E.PlayerSeat, E.Active);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BankedSupport_ShowsAgain_WhenTheTurnComesBackToThePlayer()
        {
            yield return Stage(true, 0, 5150);
            Enc.TapTargetNow("note 0"); yield return WaitReady(); Enc.TapTargetNow("seal"); yield return WaitReady();
            yield return Until(() => E.Active == E.PlayerSeat && E.Phase != ElectionPhase.Complete, 60f, "the player's turn again");
            CouncilEncounter.AiDelay = 600f;
            yield return WaitReady();
            // after a rival's bank the village holds that rival's standing for a few seconds (HoldShown) before it turns back
            yield return Until(() => Enc.Scene.ActiveSeatShown == E.PlayerSeat && Enc.Scene.ViewMismatch(Sc) == null, 15f, "the village re-formed for the player");
            // a rival may have secured a dispute (banked a whole track) between the player's turns; a secured place shows
            // everyone settled whoever holds it (CouncilScene.Expected), so only the unclaimed places measure banked support
            int settled = 0, banked = 0;
            for (int t = 2; t <= 12; t++)
            {
                if (E.Claimed[t - 2] >= 0) continue;
                Enc.Scene.Counts(t, out int s, out _, out _); settled += s;
                banked += E.BankedAt(E.PlayerSeat, t);
            }
            Assert.AreEqual(banked, settled, "as many people stand with the candidate as the banked places (unclaimed)");
            Assert.Greater(settled, 0);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator States_ReadFromPoseAndZone_BankHandsOverANote_ListenersEndAtZero()
        {
            yield return Stage();
            Enc.TapTargetNow("note 0"); yield return WaitReady();
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "after the advance");
            int listeners = 0, undecided = 0;
            for (int t = 2; t <= 12; t++)
            {
                var pl = Enc.Scene.PlaceOf(t);
                foreach (var p in Enc.Scene.People(t))
                {
                    if (p.S == PState.Listening) { listeners++; Assert.AreEqual(ActorPose.Listen, p.A.Pose, "listener pose at " + t); Assert.AreEqual(1, pl.ZoneOf(p.A.transform.position), "listener in the catspaw's patch at " + t); }
                    else if (p.S == PState.Undecided && !p.IsLead) { undecided++; Assert.AreEqual(ActorPose.Doubt, p.A.Pose, "undecided pose at " + t); Assert.AreEqual(2, pl.ZoneOf(p.A.transform.position), "undecided behind the prop at " + t); }
                }
                var paw = Enc.Scene.Catspaw(t);
                if (paw != null) { Assert.AreEqual(ActorPose.Address, paw.Pose, "the catspaw addresses its listeners"); Assert.AreEqual(1, pl.ZoneOf(paw.transform.position)); }
            }
            Assert.AreEqual(2, listeners); Assert.Greater(undecided, 40);
            int handed0 = Enc.Scene.NotesHanded;
            CouncilEncounter.AiDelay = 600f;
            Assert.IsTrue(Enc.TapTargetNow("seal"));
            yield return Until(() => Enc.Scene.NotesHanded >= handed0 + 2, 8f, "each lead handed a note to a banked listener");
            yield return Until(() => Enc.Scene.Settled, 10f, "settled");
            int notes = 0, settled = 0;
            for (int t = 2; t <= 12; t++)
            {
                Enc.Scene.Counts(t, out int s, out int l, out _);
                Assert.AreEqual(0, l, "a bank ends with zero listeners at " + t);
                notes += Enc.Scene.NotesHeld(t); settled += s;
                var pl = Enc.Scene.PlaceOf(t);
                foreach (var p in Enc.Scene.People(t))
                    if (p.S == PState.Settled) { Assert.AreEqual(ActorPose.Cheer, p.A.Pose, "supporters cheer"); Assert.AreEqual(0, pl.ZoneOf(p.A.transform.position), "supporters in the block at " + t); }
            }
            Assert.AreEqual(2, settled); Assert.AreEqual(2, notes, "the note was handed over at both places");
        }

        // ---------------------------------------------------------------- bust, petition

        IEnumerator PressOnUntil(Func<bool> done, int max = 400)
        {
            for (int i = 0; i < max && !done(); i++)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                var e = E;
                if (Enc == null || !Enc.Scene.Settled || Enc.InTransition) continue;
                if (e.Active != e.PlayerSeat) continue;
                if (e.Phase == ElectionPhase.Ready) Enc.TapTargetNow("hand");
                else if (e.Phase == ElectionPhase.Choosing)
                {
                    var props = Enc.Scene.Proposals; if (props.Count == 0) continue;
                    int best = 0; for (int k = 0; k < props.Count; k++) if (props[k].Steps.Count > props[best].Steps.Count) best = k;
                    Enc.TapTargetNow("note " + best);
                }
                else if (e.Phase == ElectionPhase.BustPending) break;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Bust_ReturnsOnlyTheUnsettled_ViewEqualsState_BankedSupportStays()
        {
            yield return Stage(true, 0, 12345);
            Enc.TapTargetNow("note 0"); yield return WaitReady(); Enc.TapTargetNow("seal"); yield return WaitReady();
            int bankedBefore = 0; for (int t = 2; t <= 12; t++) if (E.BankedAt(0, t) > 0) bankedBefore++;
            Assert.Greater(bankedBefore, 0);
            yield return Until(() => E.Active == E.PlayerSeat && E.Phase != ElectionPhase.Complete, 60f, "the player's turn again");
            CouncilEncounter.AiDelay = 600f;
            yield return WaitReady();
            int busts0 = E.Busts;
            yield return PressOnUntil(() => E.Busts > busts0, 600);
            Assert.Greater(E.Busts, busts0, "a bust happened");
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 15f, "the view equals the state after the bust");
            Assert.AreEqual(0, E.CatspawsInPlay, "unbanked progress is gone");
            int bankedAfter = 0; for (int t = 2; t <= 12; t++) if (E.BankedAt(0, t) > 0) bankedAfter++;
            Assert.AreEqual(bankedBefore, bankedAfter, "banked support stays");
            int settledNow = 0, listening = 0; for (int t = 2; t <= 12; t++) { Enc.Scene.Counts(t, out int s, out int l, out _); settledNow += s; listening += l; }
            Assert.AreEqual(0, listening, "the unsettled listeners are gone");
            int expectSettled = 0; for (int t = 2; t <= 12; t++) { Enc.Scene.Expected(E, t, out int es, out _); expectSettled += es; }
            Assert.AreEqual(expectSettled, settledNow, "a bust returns only the unsettled: everyone settled for the candidate shown stays settled");
            for (int t = 2; t <= 12; t++) foreach (var p in Enc.Scene.People(t)) if (p.S == PState.Settled) Assert.That(p.A.Pose, Is.EqualTo(ActorPose.Cheer).Or.EqualTo(ActorPose.Joining), "the settled block keeps cheering after a bust");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Petition_BustWaits_ThenTapPetitionSpends()
        {
            yield return Stage(true, 0, 777);
            E.PetitionEarned = true; // staged, as if a burst had succeeded
            CouncilEncounter.AiDelay = 600f;
            yield return PressOnUntil(() => E.Phase == ElectionPhase.BustPending, 800);
            Assert.AreEqual(ElectionPhase.BustPending, E.Phase, "with a petition held the bust waits");
            yield return WaitReady();
            Assert.IsTrue(Enc.Scene.PetitionShown, "the petition roll hangs at the candidate's hip");
            int pushes = E.Pushes;
            Assert.IsTrue(Enc.TapTargetNow("petition"));
            yield return WaitReady();
            Assert.IsTrue(E.PetitionSpent, "the petition was spent");
            Assert.AreEqual(pushes + 1, E.Pushes, "one fresh push");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Petition_TapSealAcceptsTheBust_AndKeepsThePetition()
        {
            yield return Stage(true, 0, 778);
            E.PetitionEarned = true;
            CouncilEncounter.AiDelay = 600f;
            yield return PressOnUntil(() => E.Phase == ElectionPhase.BustPending, 800);
            Assert.AreEqual(ElectionPhase.BustPending, E.Phase);
            yield return WaitReady();
            int busts = E.Busts;
            Enc.TapTargetNow("seal");
            yield return WaitReady();
            Assert.AreEqual(busts + 1, E.Busts, "the setback was taken");
            Assert.IsFalse(E.PetitionSpent, "the petition is kept");
        }

        // ---------------------------------------------------------------- listening

        IEnumerator StageRivalTurn(ulong seed)
        {
            yield return Stage(true, 0, seed);
            CouncilEncounter.AiDelay = 600f; // the rival does not move while we listen
            Enc.TapTargetNow("note 0"); yield return WaitReady(); Enc.TapTargetNow("seal"); yield return WaitReady();
            Assert.AreNotEqual(E.PlayerSeat, E.Active, "a rival's turn");
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Listening_SameAndDifferent_TimingPassedThrough_PetitionEarned_AndCarried()
        {
            yield return StageRivalTurn(31415);
            Assert.IsTrue(Enc.TurnLeft(), "turn left on a rival's turn");
            yield return Until(() => !Enc.InTransition, 5f, "camera turn");
            Assert.AreEqual(CouncilEncounter.ViewKind.Left, Enc.View);
            Assert.AreEqual(ListeningPhase.InProgress, E.Listening.Phase);
            for (int i = 0; i < 5; i++)
            {
                yield return Until(() => Enc.ReadyForAnswer, 15f, "line " + i + " readable");
                var ls = E.Listening; bool same = ListeningSession.IsSame(ls, ls.Index);
                int ms = 800 + 111 * i; int idx = ls.Index;
                Assert.IsTrue(Enc.AnswerListening(same ? ListeningChoice.Same : ListeningChoice.Different, ms).Ok);
                Assert.AreEqual(ms, E.Listening.ResponseMs[idx], "response time passed through in milliseconds");
                yield return Until(() => Enc.Listen != CouncilEncounter.ListenPhase.Feedback, 5f, "feedback");
            }
            Assert.AreEqual(ListeningPhase.Complete, E.Listening.Phase);
            Assert.AreEqual(5, E.Listening.Result.Correct, "all correct");
            Assert.IsTrue(E.PetitionEarned, "petition earned");
            Assert.GreaterOrEqual(Enc.CuesPlayed, 5, "a sound cue per answer");
            yield return Until(() => Enc.View == CouncilEncounter.ViewKind.Forward && !Enc.InTransition, 15f, "return to the table");
            yield return Until(() => Enc.Scene.Settled && !Enc.Scene.HoldPetition, 15f, "the aide carried the petition over; it is shown on the player's turn");
            Assert.IsTrue(E.PetitionAvailable);
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Listening_WrongAnswers_AreMarked_AndNoPetition()
        {
            yield return StageRivalTurn(27182);
            Enc.TurnLeft(); yield return Until(() => !Enc.InTransition, 5f, "turn");
            for (int i = 0; i < 5; i++)
            {
                yield return Until(() => Enc.ReadyForAnswer, 15f, "readable");
                var ls = E.Listening; bool same = ListeningSession.IsSame(ls, ls.Index);
                Enc.AnswerListening(same ? ListeningChoice.Different : ListeningChoice.Same, 5000);
                yield return Until(() => Enc.Listen != CouncilEncounter.ListenPhase.Feedback, 5f, "feedback");
            }
            Assert.AreEqual(0, E.Listening.Result.Correct);
            Assert.IsFalse(E.PetitionEarned);
            Assert.AreEqual("bad", Enc.LastCue);
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Listening_TurningBack_KeepsTheOpenBurst_OnARivalsTurn()
        {
            yield return StageRivalTurn(5555);
            Enc.TurnLeft(); yield return Until(() => !Enc.InTransition, 5f, "turn");
            int answers0 = E.Listening.Index;
            Enc.TapTarget("same"); // before the line is readable: ignored
            Assert.AreEqual(answers0, E.Listening.Index, "taps before a line is readable do nothing");
            yield return Until(() => Enc.ReadyForAnswer, 15f, "readable");
            Assert.IsTrue(Enc.TapTarget("rightlantern"));
            yield return Until(() => Enc.View == CouncilEncounter.ViewKind.Forward && !Enc.InTransition, 5f, "back");
            Assert.AreEqual(ListeningPhase.InProgress, E.Listening.Phase, "the open burst is kept");
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator Draws_AreUnchanged_AfterVisitingTheBench()
        {
            string a = null, b = null;
            for (int pass = 0; pass < 2; pass++)
            {
                yield return Load();
                CouncilDirector.Disabled = true; Boot.Clock.Pause("TestHold");
                Boot.Sim.State.Seed = 424242;
                var sc = CouncilFixture.StageElection(Boot.Sim.State, Home, 0, true);
                sc.Election.Active = 1; // a rival starts
                CouncilEncounter.AiDelay = 0.02f;
                Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home));
                yield return WaitReady();
                bool visited = false;
                float end = Time.realtimeSinceStartup + 150f;
                while (Time.realtimeSinceStartup < end && !(Sc.Election.Pushes >= 25 && Sc.Election.Active == Sc.Election.PlayerSeat && Sc.Election.Phase == ElectionPhase.Ready))
                {
                    var el = Sc.Election;
                    if (el.Active == el.PlayerSeat)
                    {
                        yield return new WaitForSecondsRealtime(0.1f);
                        if (!Enc.Scene.Settled) continue;
                        if (el.Phase == ElectionPhase.Ready) { if (el.Pushes >= 25) break; Enc.TapTargetNow(el.HasAdvanced ? "seal" : "hand"); }
                        else if (el.Phase == ElectionPhase.Choosing) Enc.TapTargetNow("note 0");
                        else if (el.Phase == ElectionPhase.BustPending) Enc.TapTargetNow("seal");
                    }
                    else if (pass == 1 && !visited && Enc.View == CouncilEncounter.ViewKind.Forward && !Enc.InTransition && Enc.TurnLeft())
                    {
                        visited = true;
                        yield return Until(() => !Enc.InTransition, 5f, "turn");
                        for (int i = 0; i < 3; i++)
                        {
                            yield return Until(() => Enc.ReadyForAnswer || Enc.Listen == CouncilEncounter.ListenPhase.Done || Sc.Election.Active == Sc.Election.PlayerSeat, 20f, "readable");
                            if (!Enc.ReadyForAnswer) break;
                            Enc.AnswerListening(i % 2 == 0 ? ListeningChoice.Same : ListeningChoice.Different, 1200);
                            yield return Until(() => Enc.Listen != CouncilEncounter.ListenPhase.Feedback, 5f, "feedback");
                        }
                        Enc.TurnRight(); yield return Until(() => !Enc.InTransition, 5f, "back");
                    }
                    else yield return null;
                }
                var f = Sc.Election;
                string hash = f.Pushes + "|" + string.Join(",", f.Banked) + "|" + string.Join(",", f.Claimed) + "|" + f.Busts;
                if (pass == 0) a = hash; else { b = hash; Assert.IsTrue(visited, "the bench was visited in the second pass"); }
                if (EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
            }
            Assert.AreEqual(a, b, "draws (pushes, positions, claims, busts) unchanged by visiting the listening bench");
        }

        // ---------------------------------------------------------------- leave, save, exit

        static string SigOf(ElectionState e)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(e.Active).Append('|').Append(e.PushId).Append('|').Append(e.Phase).Append('|').Append(string.Join(",", e.Banked)).Append('|').Append(string.Join(",", e.Temp)).Append('|').Append(e.PetitionEarned).Append(e.PetitionSpent);
            foreach (var t in e.Pending) sb.Append('|').Append(t.Mode).Append(t.Sockets[0].Track).Append('x').Append(t.Sockets[0].Steps).Append('/').Append(t.Sockets[1].Track).Append('x').Append(t.Sockets[1].Steps);
            return sb.ToString();
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator LeaveAndRejoin_ResumesTheSameState_IncludingThePendingPush()
        {
            yield return Stage();
            string before = SigOf(E);
            int pushId = E.PushId;
            Enc.Return();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "left");
            Boot.Clock.Resume("TestHold"); for (int i = 0; i < 3; i++) Boot.Clock.StepOnce(); Boot.Clock.Pause("TestHold");
            Assert.IsFalse(Sc.Present, "the council shows the player has left");
            Assert.AreEqual(ElectionPhase.Choosing, E.Phase, "the pending push stays pending");
            Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home), "sit down again");
            yield return WaitReady();
            Assert.IsTrue(Sc.Present, "present again");
            Assert.AreEqual(before, SigOf(E), "same candidate, positions, trays and petition state");
            Assert.AreEqual(pushId, E.PushId);
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "the same three notes are held out");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator SaveAndLoad_MidPush_ResumesExactly()
        {
            yield return Stage();
            Enc.Return(); yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "left");
            Boot.Clock.Resume("TestHold"); for (int i = 0; i < 3; i++) Boot.Clock.StepOnce(); Boot.Clock.Pause("TestHold");
            string before = SigOf(E);
            string stateJson = World.Sim.Save.SaveCodec.StateJson(Boot.Sim.State);
            Assert.IsTrue(Boot.Saves.Save(), SaveSystem.LastMessage);
            var old = Boot;
            Assert.IsTrue(Boot.Saves.Load(), SaveSystem.LastMessage);
            for (int i = 0; i < 300 && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old); i++) yield return null;
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            CouncilDirector.Disabled = true;
            Assert.AreNotSame(old, Boot);
            Assert.AreEqual(before, SigOf(Sc.Election), "the loaded election equals the saved one (pending push and trays included)");
            Assert.AreEqual(stateJson, World.Sim.Save.SaveCodec.StateJson(Boot.Sim.State), "whole state equal");
            Boot.Clock.Pause("TestHold");
            Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home));
            yield return WaitReady();
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "the notes are back in hand");
            int adv = E.Advances;
            Assert.IsTrue(Enc.TapTargetNow("note 1"));
            yield return WaitReady();
            Assert.AreEqual(adv + 1, E.Advances, "one move from the loaded push");
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Exit_RestoresCameraAndClock_AndAvatar()
        {
            yield return Load();
            CouncilDirector.Disabled = true;
            var cam = Boot.MainCamera; var parent = cam.transform.parent; var lp = cam.transform.localPosition; float fov = cam.fieldOfView;
            RememberAvatar(); SavedTick = Boot.Sim.State.Tick;
            bool wasPaused = Boot.Clock.IsPaused;
            CouncilFixture.StageExample(Boot.Sim.State, Home, 0);
            Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home));
            yield return WaitReady();
            Assert.IsTrue(Boot.Clock.IsPausedFor(PauseReasons.Encounter), "clock paused during the council");
            Assert.AreNotSame(parent, cam.transform.parent, "the council camera is its own");
            Enc.Return();
            yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "closed");
            Assert.AreSame(parent, cam.transform.parent, "camera back on the walker");
            Assert.Less((cam.transform.localPosition - lp).magnitude, 1e-4f);
            Assert.AreEqual(fov, cam.fieldOfView, 0.01f);
            Assert.IsFalse(Boot.Clock.IsPausedFor(PauseReasons.Encounter), "clock running again");
            Assert.AreEqual(wasPaused, Boot.Clock.IsPaused);
            AssertAvatarRestored("council");
            Assert.IsFalse(SpeechBubbles.ShowInEncounter);
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator OtherBubbles_AreNotShown_WhileTheCouncilIsOpen()
        {
            yield return Stage();
            int h = SpeechBubbles.Say(null, "The prince calls you", 30f);
            yield return null; yield return null;
            Assert.IsFalse(SpeechBubbles.IsOpen(h), "another system's bubble is swept while the council is open");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TurnLeft_OnlyOnRivalTurns_AndTurnRightReturns()
        {
            yield return Stage();
            Assert.IsFalse(Enc.TurnLeft(), "no listening on your own turn");
            Enc.TapTargetNow("note 0"); yield return WaitReady();
            CouncilEncounter.AiDelay = 600f;
            Enc.TapTargetNow("seal"); yield return WaitReady();
            Assert.IsTrue(Enc.TapTarget("lantern"), "the bench lantern turns the view");
            yield return Until(() => !Enc.InTransition, 5f, "turn");
            Assert.AreEqual(CouncilEncounter.ViewKind.Left, Enc.View);
            Assert.IsTrue(Enc.TurnRight());
            yield return Until(() => !Enc.InTransition, 5f, "turn back");
            Assert.AreEqual(CouncilEncounter.ViewKind.Forward, Enc.View);
        }

        // ---------------------------------------------------------------- eligibility and the approach

        [UnityTest, Timeout(200000)]
        public IEnumerator NoElectionOffered_WithFewerThanFourEligibleResidents()
        {
            yield return Load();
            CouncilDirector.Disabled = false; CouncilDirector.ApproachDelay = 0f;
            var s = Boot.Sim.State;
            s.Economy.Carriers.Clear(); // a village of just its storekeeper
            var res = CouncilRoster.WouldStand(CouncilRoster.Build(s, Home, CouncilContent.BuiltIn));
            Assert.Less(res.Count, 4, "a village with only its storekeeper has fewer than four people who would stand");
            Assert.IsFalse(CouncilApi.CanOffer(s, Home));
            var r = Boot.Sim.Check(new CouncilOffer { Settlement = Home, PlayerId = PlayerIds.Human });
            Assert.IsFalse(r.Ok); Assert.AreEqual(CouncilReasons.NotEligible, r.Reason);
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsNull(Sc, "the director never offered an election");
            Assert.IsNull(CouncilDirector.Instance.Asker, "no candidate came");
        }

        IEnumerator MakeEligibleAndWaitForAsk()
        {
            yield return Load();
            CouncilDirector.Disabled = false; CouncilDirector.ApproachDelay = 0.3f;
            CouncilFixture.MakeEligible(Boot.Sim.State, Home);
            Assert.IsTrue(CouncilApi.CanOffer(Boot.Sim.State, Home));
            yield return Until(() => CouncilDirector.Instance.Asker != null && HasReplies(), 60f, "a candidate came and asked");
        }

        static bool HasReplies() { foreach (int h in SpeechBubbles.OpenHandles()) { var r = SpeechBubbles.RepliesOf(h); if (r != null && r.Count > 0) return true; } return false; }

        [UnityTest, Timeout(300000)]
        public IEnumerator Approach_Accept_WalksToTheGathering_AndOpensTheCouncil()
        {
            yield return MakeEligibleAndWaitForAsk();
            Assert.AreEqual(CouncilStatus.Offered, Sc.Status);
            var ask = new List<int>(SpeechBubbles.OpenHandles())[0];
            Assert.GreaterOrEqual(SpeechBubbles.RepliesOf(ask).Count, 3, "accept, decline, ask what it is");
            SpeechBubbles.Choose(ask, 0);
            yield return Until(() => Enc != null, 150f, "the walk to the gathering opened the council");
            Assert.AreEqual(CouncilStatus.InProgress, Sc.Status);
            Assert.IsTrue(Sc.PlayerHelps);
            Assert.AreEqual(Sc.AskerSeat, E.PlayerSeat);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Approach_Decline_LetsTheFourPlayOnTheirOwn()
        {
            yield return MakeEligibleAndWaitForAsk();
            var ask = new List<int>(SpeechBubbles.OpenHandles())[0];
            SpeechBubbles.Choose(ask, 1);
            yield return Until(() => Sc.Status == CouncilStatus.InProgress, 10f, "declined");
            Assert.IsFalse(Sc.PlayerHelps); Assert.AreEqual(-1, E.PlayerSeat);
            Boot.Clock.MaxTicksPerFrame = 50;
            float end = Time.realtimeSinceStartup + 240f;
            while (Sc.Status != CouncilStatus.Decided && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(CouncilStatus.Decided, Sc.Status, "resolved in the background");
            Assert.IsNotNull(CouncilApi.OfficeHolderOf(Boot.Sim.State, Home));
            Assert.IsNull(Enc, "no encounter was opened");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Approach_WaitsWhile_AnotherBubbleIsOpen()
        {
            yield return Load();
            CouncilDirector.Disabled = false; CouncilDirector.ApproachDelay = 0.2f;
            int h = SpeechBubbles.Ask(null, "Pitch from someone else", new[] { "Show me", "Not this" }, null, 0f);
            CouncilFixture.MakeEligible(Boot.Sim.State, Home);
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsNull(Sc, "no offer while a pitch bubble is open");
            SpeechBubbles.Close(h);
            yield return Until(() => Sc != null, 30f, "the offer comes once the bubble is gone");
        }

        // ---------------------------------------------------------------- the older encounters still start

        [UnityTest, Timeout(200000)]
        public IEnumerator DanceAndWardDuel_StillStart_AfterTheCouncilWasOpened()
        {
            yield return Stage();
            Enc.Return(); yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "closed");
            Assert.IsTrue(EncounterDirectory.TryStart(EncounterDirectory.WardMira), "the Ward duel starts");
            Assert.IsNotNull(WardDuelEncounter.Current);
            EncounterHost.Instance.Abort();
            Assert.IsTrue(EncounterDirectory.TryStart(EncounterDirectory.RhythmParty), "the dance starts");
            Assert.IsNotNull(RhythmPartyEncounter.Current);
            EncounterHost.Instance.Abort();
        }

        // ---------------------------------------------------------------- AutoPlay

        IEnumerator RunScript(string name, float timeout)
        {
            var ap = AutoPlay.Instance;
            Assert.IsTrue(AutoPlayScripts.TryCreate(name, out var script), name + " registered");
            bool? ok = null;
            ap.QuitInEditor = false;
            ap.Finished += (s, pass) => ok = pass;
            ap.Run(script);
            float until = Time.realtimeSinceStartup + timeout;
            while (ok == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(ok == true, name + ": " + (ap.FailMessage ?? "timed out"));
        }

        // ================================================================ WU-4: preview, confirm, recorder, ladder, rings

        const float Handshake = 0.35f; // longer than the machine's double-tap guard

        IEnumerator TapPreview(string name)
        {
            if (name.StartsWith("note ")) yield return Until(() => Enc.Scene.Targets(Enc.Scene.ViewCamera).Exists(t => t.Name == name), 10f, name + " on screen");
            var have = new List<string>(); foreach (var t in Enc.Scene.Targets(Enc.Scene.ViewCamera)) have.Add(t.Name);
            Assert.IsTrue(Enc.TapTarget(name), name + " was tapped; targets " + string.Join(",", have) + " proposals " + Enc.Scene.Proposals.Count + " phase " + E.Phase);
            for (int i = 0; i < 6; i++) yield return null;
        }

        IEnumerator TapConfirm(string name)
        {
            yield return new WaitForSecondsRealtime(Handshake);
            Assert.IsTrue(Enc.TapTarget(name), name + " was tapped again");
            yield return null;
        }

        int NoteFor(int a, int b) { var ps = Enc.Scene.Proposals; for (int i = 0; i < ps.Count; i++) if (ps[i].Touches(a) && ps[i].Touches(b)) return i; return -1; }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wu4_EachExampleProposal_PreviewsBothPlaces_AndCommitsOnceOnTheSecondTap()
        {
            foreach (var pair in new[] { new[] { 4, 9 }, new[] { 5, 8 }, new[] { 6, 7 } })
            {
                yield return Stage();
                yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "three notes");
                int k = NoteFor(pair[0], pair[1]); Assert.GreaterOrEqual(k, 0);
                int accepted0 = Enc.Accepted;
                yield return TapPreview("note " + k);
                Assert.AreEqual(WorldActionState.Preview, WorldActions.State, "first tap previews");
                Assert.AreEqual(accepted0, Enc.Accepted, "the first tap sent nothing");
                Assert.AreEqual(ElectionPhase.Choosing, E.Phase);
                Assert.AreEqual(2, Enc.Scene.GhostCount, "a ghost catspaw at each place");
                foreach (int t in pair)
                {
                    CollectionAssert.Contains(Enc.Scene.PreviewTracks, t, "place " + t + " framed");
                    Assert.AreEqual(0, E.Temp[t - 2], "no catspaw has moved");
                    int tt = t;
                    yield return Until(() => Enc.Scene.PreviewTurned(tt) >= 1, 5f, "listeners turn at " + t);
                }
                Assert.IsTrue(LedgerCard.IsOpen, "the ledger card shows the proposal");
                Assert.IsNull(LedgerText.Violation(LedgerCard.Content));
                Enc.TapScreen(new Vector2(2, 2)); // a tap on nothing cancels the preview
                yield return null;
                Assert.AreEqual(WorldActionState.Idle, WorldActions.State, "tapping elsewhere cancels");
                Assert.AreEqual(0, Enc.Scene.GhostCount, "ghosts gone");
                Assert.AreEqual(accepted0, Enc.Accepted);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return TapPreview("note " + k);
                yield return TapConfirm("note " + k);
                yield return WaitReady();
                Assert.AreEqual(accepted0 + 1, Enc.Accepted, "committed once");
                foreach (int t in pair) Assert.Greater(E.Temp[t - 2], 0, "both places moved together");
                Assert.AreEqual(1, E.Advances);
                Enc.TapTarget("note " + k); Enc.TapTarget("note " + k); yield return null;
                Assert.AreEqual(1, E.Advances, "more taps do not replay it");
                yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "the view equals the state");
                Enc.Return(); yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "left");
            }
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Wu4_AnotherNote_MovesThePreview_WithoutACommand()
        {
            yield return Stage();
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "three notes");
            int a = NoteFor(4, 9), b = NoteFor(6, 7);
            yield return TapPreview("note " + a);
            var first = WorldActions.Machine.Current;
            CollectionAssert.AreEquivalent(new[] { 4, 9 }, Enc.Scene.PreviewTracks);
            yield return new WaitForSecondsRealtime(Handshake);
            yield return TapPreview("note " + b);
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
            Assert.AreNotSame(first, WorldActions.Machine.Current, "the preview moved to the other note");
            CollectionAssert.AreEquivalent(new[] { 6, 7 }, Enc.Scene.PreviewTracks);
            Assert.AreEqual(2, Enc.Scene.GhostCount);
            Assert.AreEqual(0, E.Advances, "nothing committed by moving the preview");
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Wu4_ADouble_ShowsOneCatspawTwoSteps()
        {
            yield return Stage();
            CouncilFixture.StageValues(E, 2, 2, 3, 3);
            int tray = -1; for (int i = 0; i < E.Pending.Count; i++) if (E.Pending[i].Sockets[0].Track == 5 && E.Pending[i].Sockets[1].Track == 5) tray = i;
            Assert.GreaterOrEqual(tray, 0);
            yield return WaitReady();
            yield return Until(() => Enc.Scene.Proposals.Exists(pp => pp.Tray == tray), 10f, "the double is a note");
            string name = "note " + Enc.Scene.Proposals.FindIndex(pp => pp.Tray == tray);
            yield return TapPreview(name);
            Assert.AreEqual(1, Enc.Scene.GhostCount, "one ghost catspaw");
            Assert.AreEqual(2, Enc.Scene.GhostSteps(5), "two steps ahead");
            yield return Until(() => Enc.Scene.PreviewTurned(5) >= 2, 5f, "two listeners turn");
            Assert.AreEqual(0, E.Temp[5 - 2]);
            yield return TapConfirm(name); yield return WaitReady();
            Assert.AreEqual(2, E.Temp[5 - 2]); Assert.AreEqual(1, E.CatspawsInPlay);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wu4_SocketNotes_NeverCommitHalfATray()
        {
            yield return Stage();
            // with one runner left both carvings of a tray are new places: a choice of one (a tray of sockets)
            E.Temp[2 - 2] = 1; E.Temp[3 - 2] = 1;
            CouncilFixture.StageValues(E, 1, 3, 4, 5);
            Assert.AreEqual(TrayMode.Either, E.Pending[0].Mode);
            yield return WaitReady();
            yield return Until(() => Enc.Scene.Proposals.Count > 0, 10f, "notes");
            int sock = Enc.Scene.Proposals.FindIndex(pp => pp.Socket >= 0); Assert.GreaterOrEqual(sock, 0, "a socket note exists");
            Assert.AreEqual(1, Enc.Scene.Proposals[sock].Steps.Count, "a socket note names one place");
            int adv = E.Advances;
            int before = 0; foreach (int tp0 in E.Temp) if (tp0 > 0) before++;
            yield return TapPreview("note " + sock);
            Assert.AreEqual(1, Enc.Scene.GhostCount, "only that socket's place is previewed");
            Assert.AreEqual(adv, E.Advances);
            yield return TapConfirm("note " + sock); yield return WaitReady();
            Assert.AreEqual(adv + 1, E.Advances);
            int moved = 0; foreach (int tp in E.Temp) if (tp > 0) moved++;
            Assert.AreEqual(1, moved - before, "exactly one half of the tray moved: never both silently");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wu4_Bank_NeedsAPreview_ThenConfirm_AndTabletCountsEqualTheEngine()
        {
            yield return Stage();
            AssertTablet();
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "notes");
            Enc.TapTargetNow("note " + NoteFor(5, 8)); yield return WaitReady();
            AssertTablet(); // after the advance: temporary progress
            Assert.IsTrue(string.Join(" ", CouncilTablet.Standings(Sc)).Contains("Listening now"));
            int banks = E.Banks; int pushes = E.Pushes;
            CouncilEncounter.AiDelay = 600f;
            yield return TapPreview("recorder");
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State, "the first tap on the recorder previews");
            Assert.AreEqual(banks, E.Banks, "no bank yet");
            yield return Until(() => { int n = 0; for (int t = 2; t <= 12; t++) n += Enc.Scene.PreviewStepped(t); return n >= 2; }, 5f, "the listeners step forward");
            Assert.IsTrue(Enc.Scene.PreviewBank);
            Assert.IsNull(LedgerText.Violation(LedgerCard.Content));
            Enc.TapTarget("hand"); yield return null; // elsewhere: cancels only
            Assert.AreEqual(pushes, E.Pushes, "the cancelling tap pressed nothing"); Assert.AreEqual(banks, E.Banks);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return TapPreview("seal"); // the belt seal is the shortcut with the same two taps
            Assert.AreEqual(banks, E.Banks, "the shortcut previews first too");
            yield return TapConfirm("seal"); yield return WaitReady();
            Assert.AreEqual(banks + 1, E.Banks, "banked once");
            AssertTablet(); // after the bank
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator Wu4_TabletCountsEqualTheEngine_AfterABust_AndClaimedPlacesAreShown()
        {
            yield return Stage(true, 0, 12345);
            Enc.TapTargetNow("note 0"); yield return WaitReady(); Enc.TapTargetNow("seal"); yield return WaitReady();
            yield return Until(() => E.Active == E.PlayerSeat && E.Phase != ElectionPhase.Complete, 60f, "the player's turn again");
            CouncilEncounter.AiDelay = 600f; yield return WaitReady();
            int busts0 = E.Busts;
            yield return PressOnUntil(() => E.Busts > busts0, 600);
            Assert.Greater(E.Busts, busts0);
            AssertTablet();
            // a claimed place is listed under its claimant, by name, with the claims of three
            E.Claimed[6 - 2] = 2; yield return null;
            var lines = CouncilTablet.Standings(Sc);
            Assert.IsTrue(lines[2].Contains("claims " + E.ClaimsOf(2) + " of 3") && lines[2].Contains("herbs"), lines[2]);
            AssertTablet();
        }

        void AssertTablet()
        {
            var sc = Sc; var e = E; var lines = CouncilTablet.Standings(sc);
            for (int s = 0; s < 4; s++)
            {
                var line = lines[s];
                StringAssert.StartsWith(sc.Candidates[s].Name, line);
                StringAssert.Contains("claims " + e.ClaimsOf(s) + " of 3", line);
                for (int t = 2; t <= 12; t++)
                {
                    string nm = CouncilNames.Place(t);
                    if (e.Claimed[t - 2] == s) StringAssert.Contains(nm, line.Substring(0, line.IndexOf('.') + 1), "claimed place named");
                    int b = e.BankedAt(s, t);
                    if (b > 0 && e.Claimed[t - 2] < 0) StringAssert.Contains(nm + " " + b + " of " + CouncilRules.Length(t), line);
                    int w = e.Temp[t - 2] - b;
                    if (s == e.Active && e.Phase != ElectionPhase.Complete && e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0 && w > 0) StringAssert.Contains(nm + " " + w + " more", line);
                }
            }
            Assert.IsNull(LedgerText.Violation(string.Join(" ", lines)));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wu4_ThreeRunnerCap_AndNoNumberOrSumInAnyCouncilText()
        {
            yield return Stage();
            CouncilEncounter.AiDelay = 600f;
            var texts = new List<string>();
            int steps = 0;
            for (int i = 0; i < 120 && E.Phase != ElectionPhase.Complete && E.Active == E.PlayerSeat && steps < 12; i++)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                if (!Enc.Scene.Settled) continue;
                var e = E;
                if (e.Phase == ElectionPhase.Ready) Enc.TapTargetNow("hand");
                else if (e.Phase == ElectionPhase.Choosing && Enc.Scene.Proposals.Count > 0)
                {
                    foreach (var p in Enc.Scene.Proposals) { int n = 0; foreach (var st in p.Steps) if (e.Temp[st.Key - 2] == 0) n++; Assert.LessOrEqual(n, e.CatspawsLeft, "a proposal never needs a fourth runner"); }
                    int k = Mathf.Min(1, Enc.Scene.Proposals.Count - 1);
                    var act = new CouncilAction(Enc, CouncilAction.Kind.Proposal, Enc.Scene.Proposals[k], e.PushId);
                    texts.Add(act.Title); texts.AddRange(act.Lines); texts.Add(act.PreviewLine);
                    Enc.TapTargetNow("note " + k); steps++;
                }
                Assert.LessOrEqual(E.CatspawsInPlay, 3); Assert.LessOrEqual(Enc.Scene.AgentsShown, 3);
            }
            var bank = new CouncilAction(Enc, CouncilAction.Kind.Bank); texts.Add(bank.Title); texts.AddRange(bank.Lines); texts.Add(bank.PreviewLine);
            var leave = new CouncilAction(Enc, CouncilAction.Kind.Leave); texts.AddRange(leave.Lines); texts.Add(leave.Title); texts.Add(leave.PreviewLine);
            if (Enc.Scene.LastLeadLine != null) texts.Add(Enc.Scene.LastLeadLine);
            if (Enc.Scene.LastCandidateLine != null) texts.Add(Enc.Scene.LastCandidateLine);
            Assert.Greater(texts.Count, 6);
            foreach (var t in texts)
            {
                Assert.IsFalse(Regex.IsMatch(t, "[0-9]"), "no digits in: " + t);
                Assert.IsFalse(t.Contains("+") || t.ToLowerInvariant().Contains(" sum"), "no sum in: " + t);
                Assert.IsNull(LedgerText.Violation(t), t);
            }
            foreach (var l in CouncilTablet.Standings(Sc))
            {
                Assert.IsFalse(l.Contains("+") || l.ToLowerInvariant().Contains("sum"), l);
                Assert.IsNull(LedgerText.Violation(l), l);
                var stripped = Regex.Replace(l, @"\d+ of \d+|\d+ more", "");
                Assert.IsFalse(Regex.IsMatch(stripped, "[0-9]"), "only counts of supporters: " + l);
            }
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator Wu4_RecorderTablet_OpensAsAnInspectCard()
        {
            yield return Stage();
            yield return TapPreview("tablet");
            Assert.AreEqual(WorldActionState.Inspect, WorldActions.State);
            Assert.IsTrue(LedgerCard.IsOpen);
            StringAssert.Contains("tablet", LedgerCard.Content.Title);
            Assert.IsTrue(new List<string>(LedgerCard.Content.AllText()).Exists(x => x.Contains("claims")));
            Enc.TapScreen(new Vector2(2, 2)); yield return null; // empty ground
            Assert.AreEqual(WorldActionState.Idle, WorldActions.State, "tapping elsewhere closes the tablet");
            var names = new List<string>(); foreach (var t in Enc.Scene.Targets(Enc.Scene.ViewCamera)) names.Add(t.Name);
            CollectionAssert.IsSubsetOf(new[] { "tablet", "ladder" }, names);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wu4_Ladder_LeavesWithAConfirm_AndResumeRestores()
        {
            yield return Stage();
            string before = SigOf(E);
            yield return TapPreview("ladder");
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State); Assert.IsTrue(Enc.Scene.PreviewLeave);
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "the first tap does not leave");
            Assert.IsTrue(Sc.Present);
            WorldActions.Cancel(); yield return null;
            Assert.IsFalse(Enc.Scene.PreviewLeave);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return TapPreview("ladder");
            yield return TapConfirm("ladder");
            yield return Until(() => !EncounterHost.Instance.IsRunning, 5f, "left");
            Boot.Clock.Resume("TestHold"); for (int i = 0; i < 3; i++) Boot.Clock.StepOnce(); Boot.Clock.Pause("TestHold");
            Assert.IsFalse(Sc.Present, "the council shows the player has left");
            Assert.IsTrue(EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home), "sit down again");
            yield return WaitReady();
            Assert.IsTrue(Sc.Present); Assert.AreEqual(before, SigOf(E), "the standing is restored");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wu4_GroundRings_MatchPeople_AndTheCloserCameraWorksPhoneSize()
        {
            yield return Stage();
            yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "notes");
            Enc.TapTargetNow("note " + NoteFor(5, 8)); yield return WaitReady();
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "view equals state");
            int rings = 0, people = 0;
            for (int t = 2; t <= 12; t++) foreach (var p in Enc.Scene.People(t)) { if (p.S == PState.Settled || p.S == PState.Listening) { people++; Assert.IsTrue(p.Ring != null && p.Ring.activeSelf, "a ring under every settled or listening person"); } if (p.Ring != null && p.Ring.activeSelf) rings++; }
            Assert.Greater(people, 0); Assert.AreEqual(people, rings, "no ring under anyone else");
            CouncilScene.ForceCloser = false; yield return new WaitForSecondsRealtime(1.5f);
            Assert.Less(Enc.Scene.CamPush, 0.2f, "not pushed on a big screen");
            CouncilScene.ForceCloser = true; yield return new WaitForSecondsRealtime(2f);
            // P2b: the camera moves closer only while both worked places still fit the picture (far-apart ones pull it back to the tower)
            var camV = Enc.Scene.ViewCamera;
            for (int t = 2; t <= 12; t++)
                if (E.Temp[t - 2] > 0 && E.Claimed[t - 2] < 0)
                {
                    var spw = camV.WorldToScreenPoint(Enc.Scene.PlaceCentre(t));
                    Assert.IsTrue(spw.z > 0f && spw.x > 0f && spw.x < Screen.width, "worked place " + t + " is in the picture at " + spw + " (push " + Enc.Scene.CamPush + ")");
                }
            Assert.IsTrue(Enc.Scene.Targets(Enc.Scene.ViewCamera).Exists(t => t.Name == "hand"), "the candidate's hand is still on screen");
            Assert.IsTrue(Enc.Scene.Targets(Enc.Scene.ViewCamera).Exists(t => t.Name == "recorder"), "the recorder is still on screen");
        }

#if UNITY_EDITOR
        /// <summary>P2b: at 844 x 390 the framing holds both previewed places of every proposal (even the far-apart pair), a pillar marks each, and the notes are as tall as a hit target.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator P2b_PreviewFraming_HoldsBothPlaces_At844x390_AndNotesAreBig()
        {
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint w0, out uint h0);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(844, 390, "P2b council 844x390");
            try
            {
                for (int i = 0; i < 10 && Screen.height != 390; i++) yield return null;
                ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(Screen.width, Screen.height);
                yield return Stage();
                CouncilScene.ForceCloser = true;
                yield return Until(() => Enc.Scene.Proposals.Count == 3, 10f, "three notes");
                float H = Screen.height, W = Screen.width;
                // the notes at the foot of the picture: each one at least the hit target tall, on screen
                yield return new WaitForSecondsRealtime(1.5f);
                var cam = Enc.Scene.ViewCamera;
                for (int k = 0; k < 3; k++)
                {
                    Assert.IsTrue(WorldHitTarget.ScreenBounds(Enc.Scene.NoteObject(k).transform, cam, out var nr), "note " + k + " on screen");
                    Assert.GreaterOrEqual(nr.height, WorldHitTarget.MinSide(H) * 0.8f, "note " + k + " is big enough: " + nr);
                    Assert.Greater(nr.yMin, H * 0.5f, "the notes are at the foot of the picture");
                }
                float farthest = 0f;
                for (int k = 0; k < 3; k++)
                {
                    WorldActions.Cancel();
                    yield return new WaitForSecondsRealtime(2.5f);
                    yield return TapPreview("note " + k);
                    Assert.AreEqual(WorldActionState.Preview, WorldActions.State, "first tap previews note " + k);
                    for (int w = 0; w < 16; w++)
                    {
                        yield return new WaitForSecondsRealtime(0.2f); // the camera glides (0.55 s smoothing)
                        if (WorldActions.State != WorldActionState.Preview) Assert.Fail("preview dropped after " + (w + 1) * 0.2f + " s: state " + WorldActions.State + " phase " + E.Phase + " active " + E.Active + " reason " + WorldActions.Machine.Reason);
                    }
                    Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
                    Assert.AreEqual(2, Enc.Scene.BeaconCount, "a pillar over each previewed place");
                    var tr = Enc.Scene.PreviewTracks; Assert.AreEqual(2, tr.Count);
                    farthest = Mathf.Max(farthest, Vector3.Distance(Enc.Scene.PlaceCentre(tr[0]), Enc.Scene.PlaceCentre(tr[1])));
                    var card = LedgerCard.CurrentLayout(cam).Card;
                    foreach (int t in tr)
                    {
                        var sp = cam.WorldToScreenPoint(Enc.Scene.PlaceCentre(t));
                        Assert.Greater(sp.z, 0f, "place " + t + " is in front of the camera");
                        Assert.IsTrue(sp.x > W * 0.03f && sp.x < W * 0.97f, "place " + t + " inside the width at " + sp + " (note " + k + ")");
                        Assert.IsTrue(sp.y > 0f && sp.y < H, "place " + t + " inside the height at " + sp);
                        Assert.IsFalse(card.Contains(new Vector2(sp.x, H - sp.y)), "the docked card does not cover place " + t);
                    }
                }
                Assert.Greater(farthest, 30f, "one of the example proposals has its two places far apart");
            }
            finally { CouncilScene.ForceCloser = null; UnityEditor.PlayModeWindow.SetCustomRenderingResolution(w0, h0, "restore"); ScreenSafeArea.Override = null; }
        }
#endif

        [UnityTest, Timeout(200000)]
        public IEnumerator AutoPlay_CouncilFixture_Passes()
        {
            yield return Load();
            yield return RunScript(CouncilScripts.Fixture, 120f);
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator AutoPlay_CouncilDecline_Passes()
        {
            yield return Load();
            Boot.Clock.MaxTicksPerFrame = 50;
            yield return RunScript(CouncilScripts.Decline, 300f);
        }

        [UnityTest, Timeout(1000000)]
        public IEnumerator AutoPlay_CouncilFull_Passes()
        {
            yield return Load();
            yield return RunScript(CouncilScripts.Full, 900f);
        }
    }
}
