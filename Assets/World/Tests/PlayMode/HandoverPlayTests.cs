using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Eras;
using World.Sim.Factions;

namespace World.Game.Tests
{
    /// <summary>
    /// Package H1-4: the prince after the handover (lines once, no pestering, announce then act, a cast skips the wait,
    /// advice by conversation, the invitation and its repeat, "Show me" across a save and load, wording by age, Mira's
    /// story trigger, Hush). The handover is put into the state directly (the sim's own milestone is tested in
    /// HandoverTests); the director reads the state, so everything after it is the real path.
    /// </summary>
    public class HandoverPlayTests
    {
        private WorldBootstrap Boot;
        private WorldState S => Boot.Sim.State;
        private PrinceDirector D => PrinceDirector.Instance;
        private float Now => S.Tick / (float)WorldSim.TicksPerSecond;
        private Func<bool> _hushWas;
        private string _savePath;

        private IEnumerator Load(float timeScale = 6f, bool campaign = false)
        {
            Time.timeScale = 1f;
            _hushWas = PrinceDirector.Hush;
            PrinceDirector.StartsCampaign = campaign;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.IsNotNull(Boot);
            Assert.IsNotNull(D, "PrinceDirector");
            Speed(timeScale);
        }

        private void Speed(float ts)
        {
            Time.timeScale = ts;
            Boot.Clock.MaxTicksPerFrame = Mathf.Max(5, Mathf.CeilToInt(ts * 10f / 25f) + 2);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            PrinceDirector.Hush = _hushWas;
            PrinceDirector.StartsCampaign = true;
            if (Boot != null && Boot.Clock != null) Boot.Clock.MaxTicksPerFrame = 5;
            SaveSystem.PathOverride = null; SaveSystem.TestPauseOnLoad = null;
            if (_savePath != null && File.Exists(_savePath)) File.Delete(_savePath);
        }

        private IEnumerator Until(Func<bool> cond, float gameSeconds, string what)
        {
            float end = Now + gameSeconds;
            float real = Time.realtimeSinceStartup + 25f + gameSeconds * 2f / Mathf.Max(1f, Time.timeScale);
            while (!cond())
            {
                if (Now > end || Time.realtimeSinceStartup > real) Assert.Fail(what + " (game " + Now.ToString("0.0") + " s)");
                yield return null;
            }
        }

        /// <summary>The player's people are handed over: autonomous, the chapter ready, the opening over.</summary>
        private void HandOver(bool ready = true)
        {
            var fa = S.Factions.Factions[PlayerIds.Human];
            fa.HandoverTick = S.Tick; fa.Auto = true;
            S.Factions.OpeningDone[PlayerIds.Human] = true;
            D.RestoreFirstScene(PrinceDirector.SceneDone);
            if (ready && !S.Eras.ReadyChapters.Contains(ChapterIds.Historic)) S.Eras.ReadyChapters.Add(ChapterIds.Historic);
        }

        private int Said(string text) => D.Said.Count(l => l.Text == text);
        private bool Shown => D.AskHandle != 0 && SpeechBubbles.IsShowing(D.AskHandle);

        private IEnumerator Tap(string label)
        {
            // Pages: "More" until the label is offered.
            for (int guard = 0; guard < 8; guard++)
            {
                yield return Until(() => Shown, 8f, "no replies shown for " + label);
                var r = SpeechBubbles.RepliesOf(D.AskHandle);
                Assert.IsTrue(r.Count <= 3, "more than three replies: " + string.Join(" | ", r));
                int i = r.ToList().IndexOf(label);
                if (i < 0) i = r.ToList().IndexOf(PrinceLines.More);
                Assert.GreaterOrEqual(i, 0, "neither " + label + " nor More in " + string.Join(" | ", r));
                bool last = r[i] == label;
                SpeechBubbles.Choose(D.AskHandle, i);
                yield return null;
                if (last) yield break;
            }
            Assert.Fail("label never offered: " + label);
        }

        private IEnumerator OpenMenu()
        {
            PointerInput.TapObject(D.Prince);
            yield return Until(() => D.MenuOpen && Shown, 6f, "tapping the prince did not offer replies");
        }

        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Handover_LinesSaidOnce_ThenNoPestering()
        {
            yield return Load();
            HandOver(ready: false);
            yield return Until(() => D.HandoverSceneDone, 90f, "the handover scene did not finish");
            foreach (var l in PrinceLines.HandoverLines) Assert.AreEqual(1, Said(l), "said once: " + l);
            Assert.IsTrue(D.Said.Any(l => l.Speaker == NpcIds.Prince));
            int pitches = D.PitchCount;
            float until = Now + 90f;
            yield return Until(() => Now >= until, 100f, "waiting");
            Assert.AreEqual(pitches, D.PitchCount, "he pitched after the handover");
            Assert.AreEqual(PrinceDirector.Phase.Waiting, D.CurrentPhase);
            foreach (var l in PrinceLines.HandoverLines) Assert.AreEqual(1, Said(l), "still once: " + l);
            Assert.AreEqual(0, D.Said.Count(l => l.Ask && l.Tick > S.Factions.Factions[0].HandoverTick), "no questions without the invitation or a tap");
        }

        [UnityTest]
        public IEnumerator Announce_ThenActsWithoutCast()
        {
            yield return Load();
            HandOver(ready: false);
            yield return Until(() => D.HandoverSceneDone, 90f, "scene");
            int seen = D.Announcements.Count;
            yield return Until(() => D.Announcements.Count > seen, 120f, "no announcement");
            var an = D.Announcements[D.Announcements.Count - 1];
            Assert.IsNotNull(FactionApi.GetProposal(S, an.ProposalId), "announced proposal is open");
            yield return null; yield return null;
            Assert.IsTrue(PrinceDirector.ProposalMark(an.ProposalId) != null, "the ground mark still shows");
            yield return Until(() => D.WalkingToAnnounced || D.Acts.Count > 0, 40f, "he did not walk out");
            Assert.Less(an.Text.Length, 100, an.Text);
            Assert.IsFalse(an.Text.Contains("{"), an.Text);
            yield return Until(() => D.Acts.Any(a => a.Gesture && a.ProposalId == an.ProposalId), 90f, "he did not act by himself");
            var act = D.Acts.First(a => a.Gesture && a.ProposalId == an.ProposalId);
            Assert.GreaterOrEqual(act.Tick, an.ExecuteTick, "after the announce window");
            Assert.IsFalse(D.Acts.Any(a => a.ByPlayer), "nothing was cast by the player");
            Assert.Greater(SpellEffect.Played, 0, "a visible gesture at the spot");
        }

        [UnityTest]
        public IEnumerator Cast_SkipsTheWait()
        {
            yield return Load();
            HandOver(ready: false);
            yield return Until(() => D.Announcements.Count > 0, 120f, "no announcement");
            var an = D.Announcements[D.Announcements.Count - 1];
            Assert.IsTrue(SpellCaster.CastAnywhere(an.ProposalId).Ok, "the player's press");
            yield return Until(() => D.Acts.Any(a => a.ProposalId == an.ProposalId), 10f, "the cast was not seen");
            var act = D.Acts.First(a => a.ProposalId == an.ProposalId);
            Assert.IsTrue(act.ByPlayer);
            Assert.Less(act.Tick, an.ExecuteTick, "before the announce window ended");
        }

        [UnityTest]
        public IEnumerator Advice_EachKind_SendsTheRightCommand_AndShowsTheAnswer()
        {
            yield return Load();
            HandOver(ready: false);
            yield return Until(() => D.HandoverSceneDone, 90f, "scene");
            // What are you doing?
            yield return OpenMenu();
            var first = SpeechBubbles.RepliesOf(D.AskHandle);
            CollectionAssert.AreEqual(new[] { PrinceLines.WhatDoing, PrinceLines.DoFirst, PrinceLines.More }, first.ToArray());
            yield return Tap(PrinceLines.WhatDoing);
            Assert.IsNotNull(D.LastExplanation);
            yield return Until(() => D.Said.Any(l => l.Text == D.LastExplanation), 5f, "the answer was not said");
            // Do this first (needs something to favour).
            yield return Until(() => FactionApi.Top(S, 0) != null, 60f, "no proposal");
            int n = D.AdviceSent.Count;
            yield return OpenMenu();
            yield return Tap(PrinceLines.DoFirst);
            Assert.AreEqual(n + 1, D.AdviceSent.Count);
            Assert.AreEqual(AdviceKind.Favour, D.AdviceSent[n].Kind);
            Assert.AreEqual(0, D.AdviceSent[n].Faction);
            long tickFav = S.Tick;
            yield return Until(() => Said("Then that goes first.") > 0 || D.Said.Any(l => l.Tick >= tickFav && l.Speaker == NpcIds.Prince), 5f, "no answer to the advice");
            // Not that.
            yield return Until(() => FactionApi.Top(S, 0) != null, 60f, "no proposal");
            n = D.AdviceSent.Count;
            yield return OpenMenu();
            yield return Tap(PrinceLines.NotThat);
            Assert.AreEqual(AdviceKind.Veto, D.AdviceSent[n].Kind);
            // What should come first? Economy, then a ware, then Trade (declined with the sim's sentence).
            yield return OpenMenu();
            yield return Tap(PrinceLines.WhatFirst);
            yield return Tap("Economy");
            Assert.AreEqual(AdviceKind.Priority, D.AdviceSent[D.AdviceSent.Count - 1].Kind);
            Assert.AreEqual(PriorityClass.Economy, D.AdviceSent[D.AdviceSent.Count - 1].Priority);
            yield return OpenMenu();
            yield return Tap(PrinceLines.WhatFirst);
            yield return Tap("A ware");
            yield return Tap("Stone");
            var fw = D.AdviceSent[D.AdviceSent.Count - 1];
            Assert.AreEqual(AdviceKind.FocusWare, fw.Kind);
            Assert.AreEqual((int)Ware.Stone, fw.Ware);
            yield return OpenMenu();
            yield return Tap(PrinceLines.WhatFirst);
            yield return Tap("Trade");
            yield return Until(() => Said("We have nothing to trade with yet.") > 0, 6f, "the sim's sentence for declined advice");
        }

        [UnityTest]
        public IEnumerator Invitation_RepeatsWhileDeclined_AndWhatChangesAnswers()
        {
            yield return Load();
            S.Eras.Tuning.InviteRepeatTicks = 400; // 40 s instead of 3 minutes
            HandOver();
            yield return Until(() => D.InviteOpen && Shown, 120f, "no invitation");
            CollectionAssert.AreEqual(new[] { PrinceLines.ShowMe, PrinceLines.NotYet, PrinceLines.WhatChanges }, SpeechBubbles.RepliesOf(D.AskHandle).ToArray());
            yield return Tap(PrinceLines.WhatChanges);
            yield return Until(() => Shown && SpeechBubbles.TextOf(D.AskHandle) == PrinceLines.InviteWhat, 5f, "no answer to What changes?");
            yield return Tap(PrinceLines.NotYet);
            Assert.IsFalse(D.InviteOpen);
            float declined = Now;
            yield return Until(() => D.InviteOpen && Shown, 120f, "the invitation did not come back");
            Assert.GreaterOrEqual(Now - declined, 39f, "not before the sim's repeat interval");
            Assert.AreEqual(2, D.Invitations);
            Assert.AreEqual(Era.Prehistoric, AgesApi.Current(S));
        }

        [UnityTest]
        public IEnumerator ShowMe_Advances_AndSurvivesSaveAndLoad()
        {
            yield return Load();
            _savePath = Path.Combine(Application.temporaryCachePath, "worldmvp_h14_save.json");
            SaveSystem.PathOverride = _savePath; SaveSystem.TestPauseOnLoad = "Test";
            HandOver();
            yield return Until(() => D.InviteOpen && Shown, 120f, "no invitation");
            yield return Tap(PrinceLines.ShowMe);
            yield return Until(() => AgesApi.Current(S) == Era.Historic, 5f, "Show me did not advance");
            yield return Until(() => D.StepBeatPlayed, 5f, "no beat");
            Assert.AreEqual(ChapterIds.Historic, AgesApi.Chapter(S));
            Boot.Clock.Pause("Test");
            Assert.IsTrue(Boot.Saves.Save(), SaveSystem.LastMessage);
            var old = Boot;
            Assert.IsTrue(Boot.Saves.Load(), SaveSystem.LastMessage);
            for (int i = 0; i < 200 && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old); i++) yield return null;
            yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.AreNotSame(old, Boot);
            Assert.AreEqual(Era.Historic, AgesApi.Current(S), "still Historic after the load");
            Assert.AreEqual(ChapterIds.Historic, AgesApi.Chapter(S));
            Boot.Clock.Resume("Test");
            Speed(6f);
            yield return Until(() => Now > 20f, 60f, "after load");
            Assert.AreEqual(0, D.Invitations, "no new invitation after the step");
            Assert.IsFalse(D.StepBeatPlayed, "the beat is not replayed after a load");
        }

        [Test]
        public void MapWording_DependsOnTheAge()
        {
            var go = new GameObject("tmp");
            try
            {
                var s = new WorldState(); // only Eras is read
                s.Eras = new ErasState();
                var w = new WarningView { Active = true, Attacker = PlayerIds.Human, Defender = PlayerIds.Rival, TicksLeft = 300, Sentence = "Your people are gathering against the Ash Clan: they fall in 30 s unless they grow stronger." };
                Assert.IsFalse(AgeWords.Setback(s));
                StringAssert.Contains("they fall in", AgeWords.Warning(s, w));
                s.Eras.Current = Era.Historic;
                Assert.IsTrue(AgeWords.Setback(s));
                var text = AgeWords.Warning(s, w);
                StringAssert.DoesNotContain("fall in", text);
                StringAssert.Contains("30 s", text);
                var w2 = new WarningView { Active = true, Attacker = PlayerIds.Rival, Defender = PlayerIds.Human, TicksLeft = 300, Sentence = "x" };
                StringAssert.Contains("lose a camp", AgeWords.Warning(s, w2));
                s.Eras.Current = Era.Prehistoric; s.Eras.Mode = World.Sim.Eras.PlayMode.Campaign;
                Assert.IsTrue(AgeWords.Setback(s), "a campaign is always a setback");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Mira_StartsOnlyAfterCampaignMayStart()
        {
            yield return Load(campaign: true);
            HandOver();
            Assert.IsFalse(AgesApi.CampaignMayStart(S));
            yield return Until(() => D.HandoverSceneDone, 90f, "scene");
            yield return Until(() => Now > S.Factions.Factions[0].HandoverTick / 10f + 30f, 60f, "wait");
            Assert.IsNull(S.Campaign, "no expedition before the step");
            Assert.IsFalse(D.CampaignTriggered);
            yield return Until(() => D.InviteOpen && Shown, 120f, "no invitation");
            yield return Tap(PrinceLines.ShowMe);
            yield return Until(() => AgesApi.Current(S) == Era.Historic, 5f, "step");
            Assert.IsTrue(AgesApi.CampaignMayStart(S));
            yield return Until(() => S.Campaign != null, 30f, "Mira's campaign did not start");
            Assert.IsTrue(D.CampaignTriggered);
            yield return Until(() => ExpeditionDirector.Instance != null, 5f, "no expedition director");
        }

        [UnityTest]
        public IEnumerator Hush_KeepsHimQuiet()
        {
            yield return Load();
            PrinceDirector.Hush = () => true;
            HandOver();
            float until = Now + 30f;
            yield return Until(() => Now >= until, 40f, "wait");
            Assert.AreEqual(1, D.HandoverStage, "he waits for Mira to finish");
            Assert.AreEqual(0, D.Said.Count(l => PrinceLines.HandoverLines.Contains(l.Text)));
            Assert.IsFalse(D.InviteOpen);
            Assert.AreEqual(0, D.Announcements.Count(a => a.Spoken), "no announcement spoken over her");
            PrinceDirector.Hush = () => false;
            yield return Until(() => D.HandoverSceneDone, 90f, "he speaks when she is done");
            Assert.AreEqual(1, Said(PrinceLines.HandoverLines[0]));
        }
    }
}
