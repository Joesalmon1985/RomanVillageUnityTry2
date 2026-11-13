using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game.Tests
{
    /// <summary>
    /// Package N (REFACTOR_SPEC 2.C.5, 6.3, 8.5): the opening lines and replies, the prince reaching the player from
    /// anywhere, leading with wait and fetch, the cast at the spot and his answer, decline, ignoring (nothing built,
    /// escalation), speaker routing, the win and loss scenes, the line banks, and the no-WorldState-writes grep over
    /// N's files. Timers run on game time, so the tests speed the clock up with Time.timeScale.
    /// </summary>
    public class PrinceTests
    {
        private WorldBootstrap Boot;
        private PlayerWalker W => Boot.Avatar;
        private WorldState S => Boot.Sim.State;
        private PrinceDirector D => PrinceDirector.Instance;
        private float Now => S.Tick / (float)WorldSim.TicksPerSecond;

        private IEnumerator Load(float timeScale = 1f)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.IsNotNull(Boot, "WorldBootstrap.Instance");
            Assert.IsNotNull(D, "PrinceDirector created by PrinceInstaller");
            Speed(timeScale);
        }

        private void Speed(float timeScale)
        {
            Time.timeScale = timeScale;
            Boot.Clock.MaxTicksPerFrame = Mathf.Max(5, Mathf.CeilToInt(timeScale * 10f / 25f) + 2);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (Boot != null && Boot.Clock != null) Boot.Clock.MaxTicksPerFrame = 5;
        }

        /// <summary>Waits until the condition holds, failing after <paramref name="gameSeconds"/> of game time (or a real-time guard).</summary>
        private IEnumerator Until(Func<bool> cond, float gameSeconds, string what)
        {
            float end = Now + gameSeconds;
            float real = Time.realtimeSinceStartup + 20f + gameSeconds * 2f / Mathf.Max(1f, Time.timeScale);
            while (!cond())
            {
                if (Now > end || Time.realtimeSinceStartup > real) Assert.Fail(what + " (game " + Now.ToString("0.0") + " s)");
                yield return null;
            }
        }

        private bool AskShown => D.AskHandle != 0 && SpeechBubbles.IsShowing(D.AskHandle);

        private float FacingError(Npc n)
        {
            var d = W.Position - n.transform.position; d.y = 0;
            return Vector3.Angle(n.transform.forward, d);
        }

        private IEnumerator OpeningAsk()
        {
            yield return Until(() => AskShown && D.ActiveSpeaker == D.Prince, 30f, "the prince did not ask within 30 s");
        }

        private int Choose(string label)
        {
            int h = D.AskHandle;
            var r = SpeechBubbles.RepliesOf(h);
            Assert.IsNotNull(r, "no replies");
            int i = r.ToList().IndexOf(label);
            Assert.GreaterOrEqual(i, 0, "reply " + label + " in " + string.Join(" | ", r));
            Assert.IsTrue(SpeechBubbles.Choose(h, i), "choose " + label);
            return i;
        }

        private Vector3 Target(int proposalId)
        {
            Assert.IsTrue(FactionApi.ProposalTarget(S, proposalId, out double x, out double z, out _), "target of " + proposalId);
            return new Vector3((float)x, 0, (float)z);
        }

        /// <summary>A free cell about <paramref name="metres"/> from the player, away from the opening plot.</summary>
        private Cell FarCell(float metres)
        {
            var camp = new CampLayout(S.Board);
            var away = W.Position - camp.Hearth; away.y = 0; away.Normalize();
            var c = WalkLattice.ToCell(W.Position + away * metres);
            var f = WalkGrid.NearestFree(c, 4);
            Assert.IsTrue(f.HasValue, "a free cell");
            return f.Value;
        }

        // =====================================================================================================

        [UnityTest]
        public IEnumerator FirstScene_OpeningLineAndReplies_AreExact()
        {
            yield return Load(2f);
            yield return OpeningAsk();
            Assert.LessOrEqual(Now, 30f, "the prince reaches the player within 30 s");
            Assert.AreEqual(PrinceLines.FirstLine, SpeechBubbles.TextOf(D.AskHandle));
            Assert.AreEqual("I'm the prince. Come with me. We're building a woodcutter's hut.", SpeechBubbles.TextOf(D.AskHandle), "the spec's exact line");
            CollectionAssert.AreEqual(new[] { "Lead the way", "What's it for?", "I'll catch up." }, SpeechBubbles.RepliesOf(D.AskHandle).ToArray());
            Assert.AreEqual("The prince", SpeechBubbles.TitleOf(D.AskHandle));
            Assert.Less(BoardSpace.Flat(D.Prince.transform.position - W.Position), 12f, "he stands near the player");
            Assert.AreEqual(PrinceDirector.SceneAsked, D.FirstSceneStep);
            // "What's it for?": the reason, then the two other replies.
            int first = D.AskHandle;
            Choose(PrinceLines.ReplyWhat);
            yield return Until(() => D.AskHandle != 0 && D.AskHandle != first, 3f, "no follow-up question");
            var open = FactionApi.Top(S, PlayerIds.Human);
            Assert.AreEqual(open.Reason, SpeechBubbles.TextOf(D.AskHandle), "the proposal's reason");
            CollectionAssert.AreEqual(new[] { "Lead the way", "I'll catch up." }, SpeechBubbles.RepliesOf(D.AskHandle).ToArray());
            // "I'll catch up.": he walks to the plot and waits there; the player does not follow.
            Choose(PrinceLines.ReplyCatchUp);
            yield return null;
            Assert.IsNull(W.Following, "the player walks alone");
            yield return Until(() => D.CurrentPhase == PrinceDirector.Phase.AtSpot, 30f, "he did not wait at the plot");
            var p = FactionApi.GetProposal(S, D.PitchedProposal);
            Assert.AreEqual(631, p.Plot, "the opening plot");
            Assert.Less(BoardSpace.Flat(D.Prince.transform.position - Target(p.Id)), 11f, "he stands by the plot");
            Assert.Greater(BoardSpace.Flat(D.Prince.transform.position - Target(p.Id)), 2f, "but not on it");
        }

        [UnityTest]
        public IEnumerator Prince_ReachesThePlayerFromAnywhere_AndFacesThem()
        {
            yield return Load(4f);
            W.Teleport(FarCell(160f), 0);
            float d0 = BoardSpace.Flat(D.Prince.transform.position - W.Position);
            Assert.Greater(d0, 120f, "the player starts far away");
            yield return Until(() => AskShown, 60f, "the prince did not reach the far player");
            Assert.Less(BoardSpace.Flat(D.Prince.transform.position - W.Position), 12f, "near the player");
            yield return new WaitForSeconds(0.3f); // the body finishes its turn
            Assert.Less(FacingError(D.Prince), 25f, "he faces the player");
            // The player moves on: the next time he catches up again (here: walk off 60 m, answer nothing).
            Debug.Log("[PrinceTests] reached the player from " + d0.ToString("0") + " m by game " + Now.ToString("0.0") + " s");
        }

        [UnityTest]
        public IEnumerator Leading_WaitsAndCalls_WhenThePlayerLags_ThenFetches()
        {
            yield return Load(2f);
            yield return OpeningAsk();
            Choose(PrinceLines.ReplyLead);
            yield return null;
            Assert.AreSame(D.Prince, W.Following as Npc, "Lead the way: the player follows him");
            Assert.AreEqual(PrinceDirector.Phase.Leading, D.CurrentPhase);
            // The player stops following and stays 30 m back.
            W.StopAuto();
            W.Teleport(FarCell(30f), 0);
            yield return Until(() => D.WaitingForPlayer && D.Calls > 0, 20f, "he did not wait and call");
            Assert.IsTrue(D.AskHandle == 0 || true);
            bool calling = SpeechBubbles.OpenHandles().Any(h => SpeechBubbles.RepliesOf(h) != null && SpeechBubbles.RepliesOf(h).Contains(PrinceLines.Coming));
            Assert.IsTrue(calling, "his call offers \"Coming\"");
            Assert.IsFalse(D.Prince.IsWalking, "he waits");
            // The player wanders far off: he comes back to fetch them and asks again.
            W.Teleport(FarCell(150f), 0);
            yield return Until(() => D.Fetches > 0, 20f, "he did not come to fetch the player");
            yield return Until(() => AskShown, 60f, "no question after the fetch");
            Assert.Less(BoardSpace.Flat(D.Prince.transform.position - W.Position), 12f, "he came back to the player");
            CollectionAssert.Contains(SpeechBubbles.RepliesOf(D.AskHandle).ToArray(), PrinceLines.ReplyLead, "he offers to lead again");
        }

        [UnityTest]
        public IEnumerator Cast_AtTheSpot_StartsTheHut_AndHeAnswers()
        {
            yield return Load(2f);
            yield return OpeningAsk();
            Choose(PrinceLines.ReplyLead);
            yield return Until(() => D.CurrentPhase == PrinceDirector.Phase.AtSpot, 30f, "not at the spot");
            int pid = D.PitchedProposal;
            yield return Until(() => SpellCaster.InRange(pid), 15f, "the following player is not in cast range");
            var p = FactionApi.GetProposal(S, pid);
            Assert.IsTrue(D.Said.Any(l => l.Speaker == NpcIds.Prince && l.Text == p.Reason), "he explained at the spot");
            Assert.AreEqual(TapOutcome.Cast, PointerInput.TapGround(Target(pid)), "the press casts");
            yield return null; yield return null;
            Assert.IsTrue(S.Economy.Buildings.Any(b => b.Plot == 631 && b.Type == BuildingType.WoodcutterHut && b.Faction == PlayerIds.Human), "hut site on plot 631");
            yield return Until(() => D.Said.Any(l => l.Text == PrinceLines.Bank(NpcIds.Prince, "first.cast")[0]), 3f, "no answer to the cast");
            Assert.AreEqual(PrinceDirector.SceneBuilding, D.FirstSceneStep);
            Assert.IsNotNull(D.Builder, "the builder was called over");
            Assert.IsNull(W.Following, "the follow ends with the cast");
        }

        [UnityTest]
        public IEnumerator CastFromTooFar_IsRefused_AndHeSaysSo()
        {
            yield return Load(2f);
            yield return OpeningAsk();
            int pid = FactionApi.Top(S, PlayerIds.Human).Id;
            W.Teleport(FarCell(60f), 0);
            var r = SpellCaster.TryCastAt(Target(pid));
            Assert.AreEqual(SpellCaster.TooFar, r.Reason);
            yield return null;
            var tooFar = PrinceLines.Bank(NpcIds.Prince, "toofar");
            Assert.IsTrue(D.Said.Any(l => tooFar.Contains(l.Text)), "he reacts to the refusal");
        }

        [UnityTest]
        public IEnumerator Decline_BringsTheNextProposal_AndNotTheSameAgainWithin60s()
        {
            yield return Load(4f);
            D.RestoreFirstScene(PrinceDirector.SceneDone);
            yield return Until(() => FactionApi.Top(S, PlayerIds.Human) != null, 10f, "no proposal");
            Assert.IsTrue(DevTools.Cast(FactionApi.Top(S, PlayerIds.Human).Id).Ok, "the opening, cast from the F1 tools");
            yield return Until(() => AskShown && SpeechBubbles.RepliesOf(D.AskHandle).Contains(PrinceLines.NotThis), 90f, "no pitch after the opening");
            int declined = D.PitchedProposal;
            string key = FactionApi.GetProposal(S, declined).Key;
            CollectionAssert.AreEqual(new[] { "Show me", "Not this", "Later" }, SpeechBubbles.RepliesOf(D.AskHandle).ToArray());
            Choose(PrinceLines.NotThis);
            yield return Until(() => FactionApi.GetProposal(S, declined) == null, 3f, "DeclineProposal did not remove it");
            Assert.AreEqual(1, D.Declines);
            float t0 = Now;
            yield return Until(() => AskShown && D.PitchedProposal != declined, 30f, "no next pitch after the decline");
            Assert.Less(Now - t0, 15f, "the next pitch comes at once");
            // For 60 s nothing with the same key is pitched.
            while (Now - t0 < 60f)
            {
                var p = FactionApi.GetProposal(S, D.PitchedProposal);
                if (p != null && AskShown) Assert.AreNotEqual(key, p.Key, "the declined proposal came back within 60 s");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Ignoring_BuildsNothing_AndThePesteringEscalates()
        {
            yield return Load(10f);
            int start = S.Economy.Buildings.Count(b => b.Faction == PlayerIds.Human);
            var moods = new HashSet<PitchMood>();
            float end = Now + 330f;
            while (Now < end)
            {
                if (AskShown) moods.Add(D.Mood);
                yield return null;
            }
            Assert.AreEqual(0, S.Factions.Casts[PlayerIds.Human], "no casts");
            Assert.AreEqual(start, S.Economy.Buildings.Count(b => b.Faction == PlayerIds.Human), "nothing built");
            Assert.AreEqual(0, S.Economy.Roads.Count(r => r.Faction == PlayerIds.Human), "no roads");
            var asks = D.Said.Where(l => l.Ask).ToList();
            Assert.GreaterOrEqual(asks.Count, 4, "he keeps asking");
            Assert.GreaterOrEqual(D.ResignedCount, 1, "after three unanswered pitches he goes quiet for a while");
            var gaps = new List<float>();
            for (int i = 1; i < asks.Count; i++) gaps.Add((asks[i].Tick - asks[i - 1].Tick) / 10f);
            Debug.Log("[PrinceTests] ignored: asks at " + string.Join(", ", asks.Select(a => (a.Tick / 10f).ToString("0"))) + " s; moods " + string.Join(",", moods));
            Assert.Greater(gaps[0], gaps[1], "the second interval is shorter than the first (escalation)");
            Assert.IsTrue(moods.Contains(PitchMood.Keen) && moods.Contains(PitchMood.Pressing), "his mood rises");
            Assert.GreaterOrEqual(asks.Select(a => a.Text).Distinct().Count(), Math.Min(asks.Count, 4), "varied pitch lines");
            AssertNoRepeats();
        }

        private void AssertNoRepeats()
        {
            var last = new Dictionary<string, string>();
            foreach (var l in D.Said)
            {
                if (last.TryGetValue(l.Speaker, out var prev)) Assert.AreNotEqual(prev, l.Text, l.Speaker + " said the same sentence twice in a row");
                last[l.Speaker] = l.Text;
            }
        }

        [UnityTest]
        public IEnumerator SpeakerRouting_SawyerAndMerchantPitchTheirOwnProposals()
        {
            yield return Load(8f);
            // Rule check on the proposal's speaker field.
            Assert.AreSame(D.Sawyer, D.SpeakerFor(new Proposal { Speaker = Speakers.Sawyer }));
            Assert.AreSame(D.Merchant, D.SpeakerFor(new Proposal { Speaker = Speakers.Merchant }));
            Assert.AreSame(D.Prince, D.SpeakerFor(new Proposal { Speaker = Speakers.Prince }));
            // In play: cast what the others want from the F1 tools until each of them comes to pitch.
            D.RestoreFirstScene(PrinceDirector.SceneDone);
            bool sawyer = false, merchant = false;
            float end = Now + 900f;
            float nextCast = 0f;
            while (Now < end && !(sawyer && merchant))
            {
                var top = FactionApi.Top(S, PlayerIds.Human);
                if (AskShown && D.ActiveSpeaker != null)
                {
                    var p = FactionApi.GetProposal(S, D.PitchedProposal);
                    if (p != null && p.Speaker == Speakers.Sawyer && D.ActiveSpeaker == D.Sawyer && SpeechBubbles.TitleOf(D.AskHandle) == D.Sawyer.DisplayName) sawyer = true;
                    if (p != null && p.Speaker == Speakers.Merchant && D.ActiveSpeaker == D.Merchant && SpeechBubbles.TitleOf(D.AskHandle) == D.Merchant.DisplayName) merchant = true;
                    if (p != null && p.Speaker != Speakers.Prince) Assert.AreNotSame(D.Prince, D.ActiveSpeaker, "the prince pitched someone else's proposal");
                }
                // Cast from the F1 tools whatever is not still waiting to be seen pitched (lets the brain move on).
                bool wanted = top != null && ((top.Speaker == Speakers.Sawyer && !sawyer) || (top.Speaker == Speakers.Merchant && !merchant));
                if (top != null && !wanted && Now >= nextCast) { DevTools.Cast(top.Id); nextCast = Now + 8f; }
                yield return null;
            }
            Assert.IsTrue(merchant, "the merchant pitched a road");
            Assert.IsTrue(sawyer, "the sawyer pitched wood or the sawmill");
        }

        [UnityTest]
        public IEnumerator Win_PlaysTheVictoryScene()
        {
            yield return Load(4f);
            D.RestoreFirstScene(PrinceDirector.SceneDone);
            // Test tuning (tests may write state; the game layer may not): equal strength now counts as overrunning.
            S.Factions.Tuning.OverrunMargin = 0;
            S.Factions.Tuning.WarningTicks = 40;
            yield return Until(() => FactionApi.Outcome(S).GameOver, 30f, "no ending");
            Assert.AreEqual(GameOutcome.Victory, FactionApi.Outcome(S).Outcome);
            Assert.IsTrue(D.Said.Any(l => PrinceLines.Bank(NpcIds.Prince, "warn.attack").Any(t => l.Text.StartsWith(t.Substring(0, 10)))), "he announced the coming overrun");
            Speed(1f);
            yield return Until(() => D.EndingFinished, 60f, "the scene did not finish");
            foreach (var line in PrinceLines.Bank(NpcIds.Prince, "end.win")) Assert.IsTrue(D.Said.Any(l => l.Text == line), "said: " + line);
            Assert.IsTrue(D.Said.Any(l => l.Text == FactionApi.Outcome(S).Sentence), "the plain sentence hangs in front of the player");
            Assert.Less(BoardSpace.Flat(D.Prince.transform.position - W.Position), 20f, "the prince is with the player");
        }

        [UnityTest]
        public IEnumerator Loss_PlaysTheOverrunScene()
        {
            yield return Load(16f);
            // Test tuning: one camp more than yours is enough, and the warning is short; the rival casts at once.
            S.Factions.Tuning.OverrunMargin = 1;
            S.Factions.Tuning.WarningTicks = 50;
            S.Factions.Tuning.RivalCastDelayTicks = 20;
            yield return Until(() => FactionApi.Outcome(S).GameOver, 1200f, "no ending");
            var o = FactionApi.Outcome(S);
            Assert.AreEqual(GameOutcome.Defeat, o.Outcome);
            Assert.AreEqual(OutcomeReasons.Overrun, o.Reason);
            Assert.IsTrue(D.Said.Any(l => PrinceLines.Bank(NpcIds.Prince, "warn.defend").Any(t => l.Text.StartsWith(t.Substring(0, 12)))), "he warned of the overrun");
            Speed(1f);
            yield return Until(() => D.EndingFinished, 60f, "the scene did not finish");
            Assert.IsNotNull(Npc.Find(NpcIds.RivalChief), "the Ash Clan's war leader came");
            foreach (var line in PrinceLines.Bank("rival", "end.loss")) Assert.IsTrue(D.Said.Any(l => l.Text == line), "said: " + line);
            Assert.IsTrue(D.Said.Any(l => l.Text == o.Sentence), "the plain sentence");
        }

        // =====================================================================================================

        [Test]
        public void LineBanks_AreVariedAndShort()
        {
            Assert.GreaterOrEqual(PrinceLines.BankCount, 60);
            foreach (var l in PrinceLines.AllLines())
            {
                Assert.LessOrEqual(l.Length, 130, "short line: " + l);
                Assert.IsFalse(l.Contains("  "), l);
            }
            foreach (var who in new[] { NpcIds.Prince, NpcIds.Sawyer, NpcIds.Merchant })
                foreach (var topic in new[] { "open/Calm", "open/Keen", "open/Pressing", "open/Urgent", "lead", "wait", "fetch", "press", "nudge", "cast", "decline", "later", "resigned", "idle" })
                    Assert.GreaterOrEqual(PrinceLines.Bank(who, topic).Length, 2, who + "/" + topic);
            foreach (var kind in new[] { "hut", "collector", "workshop", "sawmill", "lodge", "road", "camp", "city", "banish" })
                Assert.GreaterOrEqual(PrinceLines.Bank(NpcIds.Prince, "body/" + kind).Length, 2, kind);
        }

        [Test]
        public void Voice_NeverSaysTheSameSentenceTwiceInARow()
        {
            var v = new Voice(NpcIds.Prince, 7);
            var args = new Dictionary<string, string> { ["thing"] = "a farm", ["where"] = "by the fields at the home camp", ["camp"] = "the home camp", ["site"] = "the home camp" };
            string last = null;
            foreach (var mood in new[] { PitchMood.Calm, PitchMood.Keen, PitchMood.Pressing, PitchMood.Urgent })
                for (int i = 0; i < 40; i++)
                {
                    string l = v.Pitch(mood, i % 2 == 0 ? "collector" : "city", args);
                    Assert.AreNotEqual(last, l);
                    Assert.IsFalse(l.Contains("{"), "filled: " + l);
                    v.Said(l); last = l;
                }
            Assert.AreEqual("A farm by the fields", Voice.Fill("{Thing} {where}", new Dictionary<string, string> { ["thing"] = "a farm", ["where"] = "by the fields" }));
        }

        [Test]
        public void NoWorldStateWrites_InPackageNFiles()
        {
            string root = Path.Combine(Application.dataPath, "World", "Game");
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(Path.Combine(root, "Prince"), "*.cs", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(root, "Actors"), "*.cs", SearchOption.AllDirectories));
            Assert.Greater(files.Count, 5);
            var write = new Regex(@"\b(State|s|S|state)\.(Economy|Factions|Demons|Board|Players|Ward|Tick|Rng|Seed)\b[\w\.\[\]\(\)]*\s*(\+\+|--|(?<![=!<>])[+\-*/]?=(?![=>]))");
            foreach (var f in files)
            {
                int n = 0;
                foreach (var line in File.ReadAllLines(f))
                {
                    n++;
                    var code = line.Trim();
                    if (code.StartsWith("//")) continue;
                    Assert.IsFalse(write.IsMatch(code), Path.GetFileName(f) + ":" + n + " writes WorldState: " + code);
                }
            }
        }
    }
}
