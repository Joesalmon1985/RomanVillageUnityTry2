using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Encounters.Council;
using L = World.Game.CouncilLayout;

namespace World.Game.Tests
{
    /// <summary>
    /// The council's Unity half: layout of every hit target, visibility of every part from the forward camera, one move
    /// per accepted tap, doubles, temporary versus banked marks, bust rollback, the petition, listening with timing,
    /// unchanged draws after visiting the bench, leave and rejoin, save and load mid-push, no election below four
    /// residents, camera and clock restored, the approach, and the AutoPlay scripts.
    /// </summary>
    public class CouncilTests : EncounterTestBase
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            PrinceInstaller.Disabled = true;
            CouncilEncounter.AiDelay = 0.05f; CouncilEncounter.IntroSeconds = 0.4f;
            _savePath = Path.Combine(Application.temporaryCachePath, "worldmvp_council_test_save.json");
            if (File.Exists(_savePath)) File.Delete(_savePath);
            SaveSystem.PathOverride = _savePath;
            SaveSystem.TestPauseOnLoad = "TestHold";
        }

        [TearDown]
        public void TearDownCouncil()
        {
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
        public void Layout_HitTargets_InsideSafeArea_AndNotOverlapping_At1080p_And_Phone()
        {
            foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(844, 390) })
            {
                var safe = L.SafeRect(size.x, size.y);
                foreach (var set in new[] { L.ForwardTargets(), L.LeftTargets() })
                {
                    var px = new List<KeyValuePair<string, Rect>>();
                    foreach (var kv in set) px.Add(new KeyValuePair<string, Rect>(kv.Key, L.ToPixels(kv.Value, size.x, size.y)));
                    foreach (var kv in px)
                    {
                        Assert.GreaterOrEqual(kv.Value.xMin, safe.xMin - 0.01f, kv.Key + " inside the safe area (left) at " + size);
                        Assert.LessOrEqual(kv.Value.xMax, safe.xMax + 0.01f, kv.Key + " inside (right) at " + size);
                        Assert.GreaterOrEqual(kv.Value.yMin, safe.yMin - 0.01f, kv.Key + " inside (top) at " + size);
                        Assert.LessOrEqual(kv.Value.yMax, safe.yMax + 0.01f, kv.Key + " inside (bottom) at " + size);
                        Assert.GreaterOrEqual(Mathf.Min(kv.Value.width, kv.Value.height), size.y > 500 ? 40f : 12f, kv.Key + " big enough to tap at " + size);
                    }
                    for (int i = 0; i < px.Count; i++)
                        for (int j = i + 1; j < px.Count; j++)
                            Assert.IsFalse(px[i].Value.Overlaps(px[j].Value), px[i].Key + " overlaps " + px[j].Key + " at " + size);
                }
                Assert.IsTrue(Mathf.Approximately(safe.width / safe.height, 16f / 9f), "safe area is 16:9");
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Layout_EveryLeadAndPart_IsVisibleFromTheForwardCamera()
        {
            yield return Stage();
            var scene = Enc.Scene;
            var screenRect = new Rect(-0.012f, -0.012f, 1.07f, 1.07f);
            void Check(string what, GameObject go, Rect? within = null)
            {
                Assert.IsTrue(CouncilScene.ProjectBounds(go, out var r), what + " has a renderer");
                Assert.IsTrue(screenRect.Contains(r.min) && screenRect.Contains(r.max), what + " inside the safe area: " + r);
                if (within.HasValue) Assert.IsTrue(within.Value.Contains(r.center), what + " in its band " + within.Value + ": " + r);
            }
            for (int t = 2; t <= 12; t++)
            {
                var bay = L.LeadBay(t);
                Check("lead " + t, scene.Lead(t).gameObject, new Rect(bay.x - 0.01f, 0.08f, bay.width + 0.02f, 0.2f));
                Check("prop " + t, scene.LeadProp(t), new Rect(bay.x - 0.01f, 0.08f, bay.width + 0.045f, 0.2f));
            }
            for (int s = 0; s < 4; s++) Check("emblem " + s, scene.BayEmblem(s), L.Bay(s));
            Check("hand", scene.HandObject); Check("seal", scene.SealObject);
            Check("candidate", scene.Candidate(E.Active).gameObject);
            for (int i = 0; i < 3; i++) Check("tray " + i, scene.TrayObject(i));
            Assert.AreEqual(3, scene.TraysShown); Assert.AreEqual(6, scene.CarvingsShown, "six carvings: two per tray");
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
            foreach (var t in E.Pending) Assert.AreEqual(TrayMode.Both, t.Mode, "tapping a tray commits both advances");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TrayTap_CommitsExactlyOneMove_UnderRepeatedTaps()
        {
            yield return Stage();
            int adv0 = E.Advances;
            for (int i = 0; i < 6; i++) Enc.TapTarget("tray 1"); // six taps in one frame, then more over time
            for (int i = 0; i < 20; i++) { yield return null; Enc.TapTarget("tray " + (i % 3)); }
            yield return WaitReady();
            Assert.AreEqual(1, E.Advances - adv0, "exactly one move committed");
            int moved = 0; foreach (int p in E.Temp) if (p > 0) moved++;
            Assert.AreEqual(2, moved, "both carvings of the tray moved a catspaw");
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
            Assert.IsTrue(Enc.TapTarget("tray " + tray));
            yield return WaitReady();
            Assert.AreEqual(2, E.Temp[5 - 2], "the catspaw on dispute 5 moved two places");
            Assert.AreEqual(1, E.CatspawsInPlay, "one catspaw");
            yield return Until(() => Enc.Scene.ViewMismatch(Sc) == null, 10f, "the view shows the catspaw at place 2");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Marks_TemporaryThenBanked_AreDistinctInTheView()
        {
            yield return Stage();
            Enc.TapTarget("tray 0");
            yield return WaitReady();
            Assert.AreEqual(2, Enc.Scene.CatspawsShown, "two temporary catspaws"); Assert.AreEqual(0, Enc.Scene.PostsShown, "no banked emblem yet");
            CouncilEncounter.AiDelay = 600f;
            Assert.IsTrue(Enc.TapTarget("seal"));
            yield return WaitReady();
            yield return Until(() => Enc.Scene.CatspawsShown == 0, 10f, "catspaws gone after the bank");
            int banked = 0; for (int t = 2; t <= 12; t++) if (E.BankedAt(0, t) > 0) banked++;
            Assert.AreEqual(2, banked);
            Assert.AreEqual(2, Enc.Scene.PostsShown, "two emblems fixed at their places");
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
                if (e.Phase == ElectionPhase.Ready) Enc.TapTarget("hand");
                else if (e.Phase == ElectionPhase.Choosing)
                {
                    int best = 0;
                    for (int k = 0; k < e.Pending.Count; k++) if (e.Pending[k].Mode == TrayMode.Both) best = k;
                    var t = e.Pending[best];
                    Enc.TapTarget(t.Mode == TrayMode.Both ? "tray " + best : "socket " + best + (t.Sockets[0].IsEmpty ? "b" : "a"));
                }
                else if (e.Phase == ElectionPhase.BustPending) break;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Bust_WalksCatspawsBack_ViewEqualsState_BankedEmblemsStay()
        {
            yield return Stage(true, 0, 12345);
            Enc.TapTarget("tray 0"); yield return WaitReady(); Enc.TapTarget("seal"); yield return WaitReady();
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
            Assert.AreEqual(bankedBefore, bankedAfter, "banked emblems stay");
            Assert.Greater(Enc.Scene.WitnessesAway, 0, "a witness walked away");
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
            Assert.IsTrue(Enc.Scene.PetitionShown, "the petition is shown beside the seal");
            int pushes = E.Pushes;
            Assert.IsTrue(Enc.TapTarget("petition"));
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
            Enc.TapTarget("seal");
            yield return WaitReady();
            Assert.AreEqual(busts + 1, E.Busts, "the setback was taken");
            Assert.IsFalse(E.PetitionSpent, "the petition is kept");
        }

        // ---------------------------------------------------------------- listening

        IEnumerator StageRivalTurn(ulong seed)
        {
            yield return Stage(true, 0, seed);
            CouncilEncounter.AiDelay = 600f; // the rival does not move while we listen
            Enc.TapTarget("tray 0"); yield return WaitReady(); Enc.TapTarget("seal"); yield return WaitReady();
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
            yield return Until(() => Enc.Scene.PetitionShown, 10f, "the aide carried the petition to the table");
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
                        if (el.Phase == ElectionPhase.Ready) { if (el.Pushes >= 25) break; Enc.TapTarget(el.HasAdvanced ? "seal" : "hand"); }
                        else if (el.Phase == ElectionPhase.Choosing) Enc.TapTarget(el.Pending[0].Mode == TrayMode.Both ? "tray 0" : "socket 0" + (el.Pending[0].Sockets[0].IsEmpty ? "b" : "a"));
                        else if (el.Phase == ElectionPhase.BustPending) Enc.TapTarget("seal");
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
            Assert.AreEqual(3, Enc.Scene.TraysShown, "the same three trays are on the table");
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
            Assert.AreEqual(3, Enc.Scene.TraysShown, "the trays are back on the table");
            int adv = E.Advances;
            Assert.IsTrue(Enc.TapTarget("tray 1"));
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
            Enc.TapTarget("tray 0"); yield return WaitReady();
            CouncilEncounter.AiDelay = 600f;
            Enc.TapTarget("seal"); yield return WaitReady();
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
