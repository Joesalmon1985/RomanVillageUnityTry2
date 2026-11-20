using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Eras;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game.Tests
{
    /// <summary>
    /// Package I6 (PRD_CRYSTALS 8.1): the expedition director and its views on the developer start dev.historic plus
    /// StartCampaign with the shipped packs. The loop (opening, leads, calls, choices, thresholds, the tower), lines
    /// and guards, the pace from the sim's events, leaving and rejoining, the cues, the retreat with a companion
    /// staying behind, the restoration view, the journal and the map layer, save and load mid-leg and at a
    /// threshold, the prince and Mira never speaking at once. Replies are tapped on the screen where the bubble draws
    /// them. The world clock runs faster than real time (bubbles keep game time), so the game-minute timings are
    /// those of normal play.
    /// </summary>
    public class ExpeditionTests
    {
        private const float Fast = 8f;
        private WorldBootstrap _boot;
        private string _path;

        private WorldState S => _boot.Sim.State;
        private CampaignState C => S.Campaign;
        private static ExpeditionDirector Dir => ExpeditionDirector.Instance;
        private PlayerWalker W => _boot.Avatar;
        private float Now => S.Tick / (float)WorldSim.TicksPerSecond;
        private static List<ExpeditionDirector.Line> Lines => new List<ExpeditionDirector.Line>(Dir.Said);

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "worldmvp_test_expedition.json");
            if (File.Exists(_path)) File.Delete(_path);
            SaveSystem.PathOverride = _path;
            SaveSystem.TestPauseOnLoad = null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            PrinceInstaller.Disabled = false;
            SaveSystem.PathOverride = null;
            SaveSystem.TestPauseOnLoad = null;
            if (MapScreen.IsOpen) { MapScreen.ClearTarget(); MapScreen.Close(); }
            Hud.Visible = false;
            Interiors.FactsOverride = null;
            WorldActions.Cancel();
            if (File.Exists(_path)) File.Delete(_path);
        }

        // ---------------- helpers ----------------

        private void Speed(float scale)
        {
            Time.timeScale = scale;
            _boot.Clock.MaxTicksPerFrame = Mathf.Max(5, Mathf.CeilToInt(scale * WorldSim.TicksPerSecond / 30f) + 2);
        }

        private IEnumerator WaitForReload(WorldBootstrap old, float seconds = 240f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && (WorldBootstrap.Instance == null || WorldBootstrap.Instance == old)) yield return null;
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.AreNotSame(old, _boot, "the scene reloaded");
        }

        /// <summary>The Slice 1 test start: dev.historic, then StartCampaign on a shipped pack (optionally edited in memory).</summary>
        private IEnumerator StartCampaign(string pack = ExpeditionDev.AuthoredPack, Action<CampaignDefinition> edit = null, float scale = Fast)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsNotNull(old);
            Assert.IsTrue(DevStart.Begin(ScenarioStarts.DevHistoric), DevStart.LastError);
            yield return WaitForReload(old);
            Assert.IsNull(Dir, "no director before a campaign exists");
            var d = ExpeditionDev.LoadPack(pack);
            edit?.Invoke(d);
            var r = ExpeditionDev.StartCampaign(d);
            Assert.IsTrue(r.Ok, r.Message);
            Speed(scale);
            yield return Until(() => Dir != null, 5f, "the director");
        }

        private IEnumerator Until(Func<bool> cond, float gameSeconds, string what)
        {
            float end = Now + gameSeconds;
            float realEnd = Time.realtimeSinceStartup + gameSeconds + 60f;
            while (!cond())
            {
                if (Now > end || Time.realtimeSinceStartup > realEnd) Assert.Fail("timed out waiting for " + what + " (" + ExpeditionScripts.Status() + ")");
                yield return null;
            }
        }

        private IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        private static string T(string id) => ExpeditionScripts.Text(id);

        private IEnumerator Tap(string replyId)
        {
            int h = Dir.AskHandle;
            Assert.AreNotEqual(0, h, "a question of Mira's is open (" + ExpeditionScripts.Status() + ")");
            yield return Until(() => !SpeechBubbles.IsOpen(h) || SpeechBubbles.IsShowing(h), 30f, "the question on screen");
            yield return ExpeditionScripts.TapReply(_boot.AutoPlay, h, T(replyId));
            Assert.IsFalse(_boot.AutoPlay.Failed, _boot.AutoPlay.FailMessage);
        }

        private IEnumerator Play(Func<bool> until, float gameSeconds, string what, params DuelResult[] duels)
        {
            yield return ExpeditionScripts.PlayUntil(_boot.AutoPlay, until, gameSeconds, what, duels);
            Assert.IsFalse(_boot.AutoPlay.Failed, _boot.AutoPlay.FailMessage);
        }

        private T Last<T>() where T : Command
        {
            for (int i = Dir.Commands.Count - 1; i >= 0; i--) if (Dir.Commands[i] is T t) return t;
            return null;
        }

        /// <summary>The surface way's state as package I5 keeps it (I5 draws both ends of a way since the integration).</summary>
        private static bool StairOpen(string marker) => MarkerRef.TryParse(marker, out var r) && Interiors.LinkAt(r) != null && Interiors.LinkAt(r).IsOpen;

        private int Count<T>() where T : Command { int n = 0; foreach (var c in Dir.Commands) if (c is T) n++; return n; }

        private bool SaidLine(string lineId, string speaker)
        {
            string text = ExpeditionLines.Sentence(CampaignTokens.Fill(C, C.Definition.Line(lineId).Text));
            foreach (var l in Dir.Said) if (l.Text == text && l.Speaker == speaker) return true;
            return false;
        }

        private int SaidIndex(string lineId)
        {
            string text = ExpeditionLines.Sentence(CampaignTokens.Fill(C, C.Definition.Line(lineId).Text));
            for (int i = 0; i < Dir.Said.Count; i++) if (Dir.Said[i].Text == text) return i;
            return -1;
        }

        private IEnumerator RunScript(string name, float scale = Fast)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var old = WorldBootstrap.Instance;
            ExpeditionScripts.TimeScale = scale;
            Assert.IsTrue(AutoPlayScripts.TryCreate(name, out var script), "script " + name);
            old.AutoPlay.Run(script);
            yield return WaitForReload(old);
            float end = Time.realtimeSinceStartup + 900f;
            while (ExpeditionScripts.LastReport == null && !(AutoPlay.Instance != null && AutoPlay.Instance.Failed) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(AutoPlay.Instance.Failed, AutoPlay.Instance.FailMessage);
            Assert.IsNotNull(ExpeditionScripts.LastReport, name + " finished");
            _boot = WorldBootstrap.Instance;
            Time.timeScale = 1f;
        }

        // ---------------- the loop, the lines, the pace ----------------

        [UnityTest, Timeout(420000)]
        public IEnumerator Opening_ThenLeadTheWay_LinesFromThePack_PaceFromTheEvent_ViewsFollow()
        {
            yield return StartCampaign();
            // Mira is the camp's Mira, taken over; six named companions in their own colours.
            Assert.AreSame(Npc.Find(NpcIds.Mira), Dir.Mira);
            Assert.IsTrue(Dir.Mira.Directed);
            Assert.AreEqual(6, Dir.Companions.Count);
            var colours = new HashSet<Color>();
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(C.Definition.Companions[i].Name, Dir.Companions[i].DisplayName);
                Assert.IsTrue(colours.Add(Dir.Mates[i].Colour), "a colour of their own: " + Dir.Mates[i].Id);
                Assert.Less(BoardSpace.Flat(Dir.Companions[i].transform.position - Dir.Mira.transform.position), 25f, "gathered at the camp");
            }
            Assert.AreEqual(ExpeditionDirector.SceneNotStarted, Dir.SceneStep);
            // The opening: the pack's story in order, then "Come with me?" with the pack's three replies.
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskOpening, 120f, "Mira's question");
            string[] story = { "mira_open_1", "mira_open_2", "mira_open_3", "mira_open_4", "mira_open_earth", "mira_open_six", "mira_open_cost", "mira_open_practice" };
            int last = -1;
            foreach (var id in story)
            {
                Assert.IsTrue(SaidLine(id, "mira"), "said " + id);
                int at = SaidIndex(id);
                Assert.Greater(at, last, id + " in order");
                last = at;
            }
            foreach (var l in Dir.Said) Assert.IsFalse(l.Text.Contains("{"), "no unfilled token: " + l.Text);
            var replies = SpeechBubbles.RepliesOf(Dir.AskHandle);
            CollectionAssert.AreEqual(new[] { T("reply_lead"), T("reply_more"), T("reply_ahead") }, replies);
            Assert.AreEqual("Come with me?", SpeechBubbles.TextOf(Dir.AskHandle));
            Assert.AreEqual(0, Dir.Commands.Count, "nothing sent before the player answers");
            Assert.AreEqual(ExpeditionDirector.SceneOpening, Dir.SceneStep);
            // "Tell me more": two more lines, the question again without that reply.
            yield return Tap("reply_more");
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskOpening && SaidIndex("mira_open_more_2") >= 0, 60f, "the question again");
            Assert.AreEqual(2, SpeechBubbles.RepliesOf(Dir.AskHandle).Count);
            // "Lead the way": AcceptLead, the player follows, the leg is walked at the event's pace.
            var top = CampaignApi.TopLead(S);
            Assert.IsNotNull(top);
            yield return Tap("reply_lead");
            Assert.AreEqual(1, Dir.Commands.Count);
            Assert.AreEqual(top.Id, Last<AcceptLead>().LeadId);
            Assert.AreEqual(ExpeditionDirector.SceneUnderWay, Dir.SceneStep);
            yield return Until(() => Dir.LegCount >= 1 && C.Party.Mode == PartyMode.Travelling, 10f, "the party to set out");
            Assert.AreSame(Dir.Mira, W.Following as Npc, "the player follows Mira");
            var leg = Dir.LastLeg;
            var tune = C.Definition.Tuning;
            Assert.AreEqual(C.Party.Leg.Ticks, leg.Ticks, "the leg's ticks are the sim's");
            Assert.AreEqual(C.Party.Running, leg.Running);
            float perStep = leg.Running ? tune.PartyRunTicksPerStep : tune.PartyTicksPerStep;
            float pace = WalkLattice.Step / (perStep / WorldSim.TicksPerSecond);
            Assert.AreEqual(pace, leg.Speed, pace * 0.2f, "walked at the sim's pace (" + (leg.Running ? "running" : "walking") + ")");
            // Measured on her body: metres per game second over three seconds of the leg.
            yield return Until(() => C.Party.Leg != null && C.Party.Leg.Progress >= 20, 30f, "the leg under way");
            var p0 = Dir.Mira.transform.position; long t0 = S.Tick;
            yield return Until(() => S.Tick >= t0 + 30, 10f, "three game seconds");
            float measured = BoardSpace.Flat(Dir.Mira.transform.position - p0) / ((S.Tick - t0) / (float)WorldSim.TicksPerSecond);
            Assert.AreEqual(leg.Speed, measured, leg.Speed * 0.3f, "Mira's body moves at the leg's pace");
            foreach (var m in Dir.Mates) Assert.Less(BoardSpace.Flat(m.Npc.transform.position - Dir.Mira.transform.position), 12f, m.Id + " walks in the file");
            // At the stone: she stands by it, reads it (the early line, then the rest when the slab moves), the stair view opens.
            yield return Until(() => C.Party.Mode == PartyMode.Acting && C.Party.At.Marker == "stone", 120f, "Mira at the boundary stone");
            yield return Until(() => Dir.MiraSettled, 10f, "Mira standing");
            var stone = ExpeditionSpace.World(S, C.Party.At);
            Assert.Less(BoardSpace.Flat(Dir.Mira.transform.position - stone), 3f, "she stands at the stone");
            Assert.Greater(BoardSpace.Flat(Dir.Mira.transform.position - stone), 1f, "in front of it, not inside it");
            Assert.IsTrue(Dir.Sites.MarkerProps.ContainsKey("glade/stone"));
            Assert.IsFalse(StairOpen("glade/stair"));
            yield return Until(() => SaidIndex("mira_stone_1") >= 0, 10f, "her first line as she reads");
            Assert.IsFalse(C.Knows("earth.stair_open"), "said while reading, before the event");
            yield return Until(() => C.Knows("earth.stair_open"), 60f, "the stair");
            yield return Until(() => SaidIndex("odo_stone") >= 0, 30f, "the event's lines");
            Assert.IsTrue(SaidLine("mira_stone_2", "mira"));
            Assert.IsTrue(SaidLine("odo_stone", "odo"));
            Assert.AreEqual(1, Lines.FindAll(l => l.Text == ExpeditionLines.Sentence(C.Definition.Line("mira_stone_1").Text)).Count, "the early line is not said twice");
            Assert.IsTrue(StairOpen("glade/stair"), "the slab slides back");
            // The next lead is offered with its reason and the three replies; "Why there?" then two replies.
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskLead, 60f, "the next lead");
            var lead = CampaignApi.TopLead(S);
            Assert.AreEqual(ExpeditionLines.Sentence(lead.Reason), SpeechBubbles.TextOf(Dir.AskHandle));
            CollectionAssert.AreEqual(new[] { T("reply_lead"), T("reply_why"), T("reply_not") }, SpeechBubbles.RepliesOf(Dir.AskHandle));
            yield return Tap("reply_why");
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskLead && Dir.ScriptPending == 0, 30f, "the lead again");
            CollectionAssert.AreEqual(new[] { T("reply_lead"), T("reply_not") }, SpeechBubbles.RepliesOf(Dir.AskHandle));
            // "Not that way": DeclineLead.
            yield return Tap("reply_not");
            Assert.AreEqual(lead.Id, Last<DeclineLead>().LeadId);
            foreach (var c in Dir.Commands) Assert.IsTrue(c is AcceptLead || c is DeclineLead, "only lead commands so far: " + c.GetType().Name);
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator DeadCompanion_NeverSpeaks_TheSawyerReadsTheNotches()
        {
            // Without Odo's shortcut the way leads past the sawyer; Odo is dead before the party reaches the stone.
            yield return StartCampaign(ExpeditionDev.AuthoredPack, d => d.Clues.RemoveAll(c => c.Id == "earth_odo"));
            Assert.IsTrue(_boot.Sim.Submit(new DevSetCompanion { Id = "odo", Alive = false }).Ok);
            yield return Until(() => !Dir.MateOf("odo").Alive, 5f, "the roster view to follow the state");
            yield return Play(() => C.Knows("earth.glade_known"), 400f, "the sawyer's word");
            yield return Until(() => C.FiredEvents.Contains("earth_sawyer_talk"), 60f, "the talk with the sawyer");
            yield return Until(() => SaidIndex("sawyer_notches_3") >= 0, 60f, "the sawyer's three lines");
            Assert.IsTrue(SaidLine("sawyer_notches_1", "sawyer") && SaidLine("sawyer_notches_2", "sawyer"));
            yield return Play(() => C.Knows("earth.stair_open"), 400f, "the stone read");
            yield return Until(() => Dir.ScriptPending == 0, 30f, "the lines to end");
            Assert.IsTrue(SaidLine("mira_stone_2", "mira"));
            foreach (var l in Dir.Said) Assert.AreNotEqual("odo", l.Speaker, "a dead companion spoke: " + l.Text);
            Assert.Less(SaidIndex("odo_stone"), 0, "his guarded line is not said by anyone");
            // He lies where the roster says he fell; five walk on.
            int walking = 0;
            foreach (var m in Dir.Mates) if (m.Alive) walking++;
            Assert.AreEqual(5, walking);
            StringAssert.Contains("Odo Birch fell", string.Join(" ", ExpeditionJournal.RosterLines(S)));
        }

        // ---------------- leaving, the cues, rejoining ----------------

        [UnityTest, Timeout(420000)]
        public IEnumerator GoOnAhead_CuesMapAndHud_ThenImWithYou_AndFallingBehind()
        {
            // H1-4: in a developer Historic start the prince now speaks after the handover and his lines hold Mira's cues until she has arrived; this test is about Mira.
            PrinceInstaller.Disabled = true;
            yield return StartCampaign(ExpeditionDev.AuthoredPack, d => d.Rival.Steps[0].MarchCost = 60000);
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskOpening, 120f, "Mira's question");
            yield return Tap("reply_ahead");
            Assert.IsFalse(Last<SetCompany>().Present, "\"Go on ahead\" sends SetCompany(false)");
            yield return Until(() => !C.Party.PlayerPresent, 3f, "the sim to count the player away");
            int cues0 = Dir.Cues.Count;
            // Alone she waits, then goes: the journal's sentence is the cue, and how to rejoin follows.
            yield return Until(() => Dir.LegCount >= 1, 150f, "Mira to set out alone");
            yield return Until(() => Dir.Cues.Exists(c => c.StartsWith("Mira went on by herself")), 120f, "the cue that says where she went (" + string.Join(" | ", Dir.Cues) + ")");
            Assert.Greater(Dir.Cues.Count, cues0);
            Assert.IsTrue(Dir.Cues.Contains(T(ExpeditionLines.CueHowToRejoin)), "and how to rejoin: " + string.Join(" | ", Dir.Cues));
            foreach (var cue in Dir.Cues) Assert.IsFalse(cue.StartsWith("Three cuts"), "what was said at a place is not a cue");
            Assert.AreEqual(0, Dir.AskHandle, "she does not pester a player who is away");
            // HUD line and map.
            Hud.Visible = true;
            yield return Frames(3);
            StringAssert.StartsWith("Mira is", Dir.Journal.HudLineShown);
            Hud.Visible = false;
            MapScreen.Open();
            yield return Frames(3);
            var marks = new List<ExpeditionMapLayer.Mark>(Dir.MapLayer.Marks);
            Assert.AreEqual(4, marks.FindAll(m => m.Kind == "tower").Count, "four towers");
            Assert.AreEqual(4, marks.FindAll(m => m.Kind == "belief").Count, "four believed crystals");
            Assert.AreEqual(6, marks.FindAll(m => m.Kind == "companion").Count);
            Assert.IsTrue(marks.Exists(m => m.Kind == "lead"));
            var mira = marks.Find(m => m.Kind == "mira");
            StringAssert.StartsWith("Mira is on her way to", mira.Label);
            Assert.Greater(Dir.Journal.PanelRect.width, 0f, "the journal is the map's side text");
            StringAssert.Contains("Find her and tap her to rejoin", ExpeditionJournal.MiraSentence(S));
            MapScreen.ClearTarget();
            MapScreen.Close();
            // Find her, tap her: "I'm with you".
            yield return Until(() => C.Party.Mode != PartyMode.Travelling, 200f, "Mira to stop somewhere");
            var beside = WalkGrid.NearestFree(WalkLattice.ToCell(Dir.Mira.transform.position) + new Cell(1, 1), 4);
            Assert.IsTrue(beside.HasValue);
            W.Teleport(beside.Value, 0);
            yield return Frames(3);
            PointerInput.TapObject(Dir.Mira);
            yield return Frames(2);
            Assert.AreEqual(ExpeditionDirector.AskMenu, Dir.AskKind);
            CollectionAssert.AreEqual(new[] { T("reply_with"), T("reply_know"), "Show me your pouch.", "Show me your journal." }, SpeechBubbles.RepliesOf(Dir.AskHandle));
            yield return Tap("reply_with");
            Assert.IsTrue(Last<SetCompany>().Present, "\"I'm with you\" sends SetCompany(true)");
            yield return Until(() => C.Party.PlayerPresent, 3f, "the sim to count the player present");
            // Present: tapping her offers to leave.
            yield return Until(() => Dir.ScriptPending == 0, 20f, "her greeting");
            if (Dir.AskHandle != 0 && Dir.AskKind != ExpeditionDirector.AskMenu) { /* a lead offered meanwhile: the tap unfolds it */ }
            // Falling behind: beyond 60 m for 20 s she counts the player away and a cue says so.
            int sent = Count<SetCompany>();
            // The cue is "You fell behind" (60 m for 20 s), or the journal's "You left Mira" when the prince was leading the
            // player off at that moment (PRD 5.9: that is "away" at once).
            System.Func<int> leftCues = () => Dir.Cues.FindAll(c => c == T(ExpeditionLines.CueFellBehind) || c.StartsWith("You left Mira")).Count;
            int left0 = leftCues();
            var far = WalkGrid.NearestFree(WalkLattice.ToCell(Dir.Mira.transform.position + new Vector3(110f, 0, 0)), 6);
            Assert.IsTrue(far.HasValue);
            W.StopAuto();
            W.Teleport(far.Value, 0);
            yield return Until(() => Count<SetCompany>() > sent, ExpeditionDirector.AwaySeconds + 15f, "SetCompany(false) for a player who fell behind");
            Assert.IsFalse(Last<SetCompany>().Present);
            yield return Until(() => leftCues() > left0, 90f, "the cue (" + string.Join(" | ", Dir.Cues) + ")");
            foreach (var c in Dir.Commands) Assert.IsTrue(c is SetCompany, "only SetCompany was sent: " + c.GetType().Name);
        }

        // ---------------- the Earth slice, arriving first; the restoration; a load ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator EarthFirst_Script_RestoresTheDeepstone_TheTowerChanges_AndALoadShowsTheSame()
        {
            yield return RunScript("earth_first");
            StringAssert.Contains("restored", ExpeditionScripts.LastReport);
            Assert.AreEqual(Custody.Restored, C.Crystal(Element.Earth).Custody);
            Assert.Greater(ExpeditionScripts.PointerReplies, 3, "replies were tapped on the screen");
            var tower = Dir.Sites.Tower(Element.Earth);
            Assert.IsTrue(tower.Restored && tower.Lit.activeSelf, "the Rooted Tower is lit");
            Assert.AreEqual(1, Dir.Sites.RestorationsPlayed);
            Assert.AreEqual("tower", Dir.Sites.CrystalPlace(Element.Earth), "the Deepstone sits in the tower's crown");
            Assert.AreEqual(0, Dir.CarriedShown);
            foreach (var other in new[] { Element.Fire, Element.Water, Element.Air }) Assert.IsFalse(Dir.Sites.Tower(other).Restored);
            Assert.AreEqual(C.BaseSevensPerDraw + 1, S.Demons.Tuning.SevensPerDraw, "demons come less often");
            StringAssert.Contains("1 of four towers lit", ExpeditionJournal.DemonSentence(S));
            Assert.IsTrue(SaidLine("mira_set_1", "mira") && SaidLine("mira_set_2", "mira"));
            StringAssert.Contains("set in the Rooted Tower", ExpeditionJournal.BeliefSentence(S, Element.Earth));
            // The other routes' beliefs have gone stale by now: the journal says so and where to look.
            if (CampaignApi.Belief(S, Element.Fire).Stale)
            {
                string fire = ExpeditionJournal.BeliefSentence(S, Element.Fire);
                StringAssert.Contains("Stale.", fire);
                StringAssert.Contains(CampaignApi.Belief(S, Element.Fire).WhereToLook, fire);
            }
            // Save, load: the same world from state alone, no opening again, nothing replayed.
            _boot.Clock.Pause("Test");
            yield return Frames(2);
            long tick = S.Tick;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            var old = _boot;
            SaveSystem.TestPauseOnLoad = "Test";
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            Assert.IsNotNull(Dir, "the director after the load");
            yield return Frames(5);
            Assert.AreEqual(tick, S.Tick);
            Assert.AreEqual(ExpeditionDirector.SceneUnderWay, Dir.SceneStep, "no second opening");
            tower = Dir.Sites.Tower(Element.Earth);
            Assert.IsTrue(tower.Restored && tower.Lit.activeSelf, "lit after the load");
            Assert.AreEqual(0, Dir.Sites.RestorationsPlayed, "the effect is not replayed");
            Assert.AreEqual("tower", Dir.Sites.CrystalPlace(Element.Earth));
            Assert.IsTrue(StairOpen("glade/stair"), "the opened stair stays open");
            Assert.AreEqual(6, Dir.Mates.Count);
            Assert.Less(BoardSpace.Flat(Dir.Mira.transform.position - ExpeditionSpace.World(S, C.Party.At)), 4f, "Mira is where the state says");
            _boot.Clock.Resume("Test");
            yield return Frames(30);
            Assert.AreEqual(0, Dir.Commands.Count, "a load sends nothing");
            Assert.IsFalse(Dir.Asked.ContainsKey(ExpeditionDirector.AskOpening));
        }

        // ---------------- stolen: the threshold, a death, the win ----------------

        [UnityTest, Timeout(1200000)]
        public IEnumerator EarthStolen_Script_ALossKillsGarrick_StagedAndRemembered_ThenAWin()
        {
            yield return RunScript("earth_stolen");
            Assert.AreEqual(Custody.Restored, C.Crystal(Element.Earth).Custody);
            Assert.IsFalse(C.Companion("garrick").Alive);
            Assert.AreEqual(1, Dir.RetreatsPlayed);
            Assert.AreEqual("garrick", Dir.LastVolunteer);
            var garrick = Dir.MateOf("garrick");
            Assert.IsFalse(garrick.Alive);
            Assert.AreEqual("tally_vault", garrick.FellSpace, "he lies where he held the way");
            Assert.IsNotNull(garrick.FallenMark);
            // The threshold named him and made the stakes clear; he said his line as he stayed; Mira remembered him.
            string threshold = ExpeditionLines.Sentence(C.Definition.Line("mira_threshold_earth").Text).Replace("{role.keeper_earth.name}", "Dolm the Tally-Keeper");
            Assert.IsTrue(Lines.Exists(l => l.Text == threshold.Replace("{volunteer.name}", "Garrick Holt")), "the threshold names Garrick");
            Assert.IsTrue(Lines.Exists(l => l.Text == threshold.Replace("{volunteer.name}", "Ivo Quill")), "the second time it names Ivo");
            Assert.IsTrue(Lines.Exists(l => l.Speaker == "mira" && l.Text.Contains("Garrick Holt holds the way out and doesn't come back")), "what happens if we lose");
            Assert.IsTrue(Lines.Exists(l => l.Speaker == "garrick" && l.Text == "Go. I've had the practice."));
            Assert.IsTrue(SaidLine("mira_after_garrick", "mira"));
            Assert.IsTrue(SaidLine("mira_for_garrick_tower", "mira"), "remembered at the lit tower");
            Assert.IsTrue(SaidLine("dolm_greet", "keeper_earth") && SaidLine("dolm_beaten", "keeper_earth") && SaidLine("mira_recovered", "mira"));
            long fell = C.Companion("garrick").FellTick;
            foreach (var l in Dir.Said) Assert.IsFalse(l.Speaker == "garrick" && l.Tick > fell, "Garrick spoke after he fell: " + l.Text);
            Assert.IsFalse(Lines.Exists(l => l.Text.Contains("Fresh boot marks") && l.Tick > fell));
            StringAssert.Contains("Garrick Holt fell at the Tally Vault", string.Join(" ", ExpeditionJournal.RosterLines(S)));
            // Commands: exactly one commit and one resolution per attempt, and nothing but commands.
            Assert.AreEqual(2, Count<CommitEncounter>());
            Assert.AreEqual(2, C.Resolved.Count, "two attempts resolved (by the campaign duel, I7)");
            Assert.AreEqual(1, Count<ConfirmRestore>());
        }

        // ---------------- save and load: mid-leg, and at a threshold ----------------

        [UnityTest, Timeout(1200000)]
        public IEnumerator SaveLoad_MidLeg_AndAtTheThreshold_ContinueFromState()
        {
            yield return StartCampaign();
            Assert.IsTrue(ExpeditionDev.AshNextStep().Ok);
            yield return Play(() => C.Party.Mode == PartyMode.Travelling && C.Party.Leg != null && !C.Party.At.IsInterior && C.Party.Leg.Progress >= C.Party.Leg.Ticks / 3, 300f, "the first leg under way");
            // Mid-leg.
            _boot.Clock.Pause("Test");
            yield return Frames(3);
            var pos = Dir.Mira.transform.position;
            long tick = S.Tick; int progress = C.Party.Leg.Progress; var to = C.Party.Leg.To;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            var old = _boot;
            SaveSystem.TestPauseOnLoad = "Test";
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            Assert.IsNotNull(Dir, "the director after the load");
            yield return Frames(4);
            Assert.AreEqual(tick, S.Tick);
            Assert.AreEqual(PartyMode.Travelling, Dir.Phase);
            Assert.AreEqual(progress, C.Party.Leg.Progress);
            Assert.AreEqual(to, Dir.LastLeg.To, "the leg is rebuilt from state");
            // The sim knows a leg by its two markers: after a load she is on the path from the leg's first marker (the camp's
            // fire), not from where she happened to stand when she set out (beside the player), so she may be some metres off.
            Assert.Less(BoardSpace.Flat(Dir.Mira.transform.position - pos), 25f, "Mira is on the leg near where she was");
            Assert.Greater(BoardSpace.Flat(Dir.Mira.transform.position - ExpeditionSpace.World(S, C.Party.Leg.From)), 10f, "under way, not back at the start");
            foreach (var m in Dir.Mates) Assert.Less(BoardSpace.Flat(m.Npc.transform.position - Dir.Mira.transform.position), 12f, m.Id + " with her");
            Assert.AreEqual(ExpeditionDirector.SceneUnderWay, Dir.SceneStep);
            _boot.Clock.Resume("Test");
            Speed(Fast);
            // On to Dolm's threshold (the pedestal is bare; the tally; the vault).
            yield return Play(() => C.Party.Mode == PartyMode.AtThreshold && Dir.AskKind == ExpeditionDirector.AskThreshold, 1500f, "Dolm's threshold");
            Assert.IsTrue(C.Knows("earth.stolen_seen") && C.Knows("earth.vault_known"));
            Assert.AreEqual("garrick", Dir.ThresholdVolunteer);
            Assert.AreEqual("tally_vault", Dir.MiraSpace);
            Assert.IsTrue(Dir.Sites.Person("keeper_earth").gameObject.activeSelf, "Dolm stands at his altar");
            Assert.AreEqual("altar", Dir.Sites.CrystalPlace(Element.Earth));
            Assert.AreEqual(0, Count<CommitEncounter>());
            // At the threshold.
            _boot.Clock.Pause("Test");
            yield return Frames(2);
            tick = S.Tick;
            Assert.IsTrue(_boot.Saves.Save(), SaveSystem.LastMessage);
            old = _boot;
            Assert.IsTrue(_boot.Saves.Load(), SaveSystem.LastMessage);
            yield return WaitForReload(old);
            Assert.IsNotNull(Dir, "the director after the load");
            Assert.AreEqual(tick, S.Tick);
            _boot.Clock.Resume("Test");
            Speed(Fast);
            Assert.AreEqual(PartyMode.AtThreshold, C.Party.Mode);
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskThreshold, 30f, "the threshold question again");
            Assert.AreEqual("tally_vault", Dir.MiraSpace);
            Assert.AreEqual("tally_vault", Dir.PlayerSpace, "the player is with her below");
            CollectionAssert.AreEqual(new[] { T("reply_fight"), T("reply_not_yet"), T("reply_if_lose") }, SpeechBubbles.RepliesOf(Dir.AskHandle));
            StringAssert.Contains("Garrick Holt holds the way out", SpeechBubbles.TextOf(Dir.AskHandle));
            // "Not yet" is safe: WithdrawFromEncounter, she steps back, nobody dies, the rival is not charged.
            long rival = C.RivalProgress; long at = S.Tick;
            yield return Tap("reply_not_yet");
            Assert.IsNotNull(Last<WithdrawFromEncounter>());
            yield return Until(() => C.Party.Mode == PartyMode.Idle && C.Party.At.Marker == "safe", 400f, "the party back at the safe point");
            Assert.AreEqual(rival + (S.Tick - at), C.RivalProgress, "withdrawing costs only the time it takes");
            Assert.AreEqual(6, CampaignApi.Companions(S).Count);
            // Back, "Fight", a win: the crystal comes off the altar into her hands.
            yield return Play(() => C.Crystal(Element.Earth).Custody == Custody.Carried, 900f, "the win", DuelResult.Victory);
            Assert.AreEqual(1, Count<CommitEncounter>());
            Assert.AreEqual(1, C.Resolved.Count, "one attempt resolved (by the campaign duel, I7)");
            yield return Frames(5);
            Assert.AreEqual(1, Dir.CarriedShown, "the Deepstone in Mira's hands");
            Assert.AreEqual("", Dir.Sites.CrystalPlace(Element.Earth));
            Assert.AreEqual(0, Dir.RetreatsPlayed);
        }

        // ---------------- the prince and Mira ----------------

        [UnityTest, Timeout(420000)]
        public IEnumerator PrinceAndMira_NeverSpeakAtOnce_HeHoldsBack_SheYields()
        {
            yield return StartCampaign(ExpeditionDev.AuthoredPack, null, 4f);
            var pd = PrinceDirector.Instance;
            Assert.IsNotNull(pd);
            // Through the whole opening: never both questions, never both speaking.
            float end = Now + 150f;
            while (Dir.AskKind != ExpeditionDirector.AskOpening && Now < end)
            {
                Assert.IsFalse(pd.AskHandle != 0 && (Dir.AskHandle != 0 || Dir.ScriptPending > 0) && SpeechBubbles.IsShowing(pd.AskHandle), "the prince and Mira at once");
                yield return null;
            }
            Assert.AreEqual(ExpeditionDirector.AskOpening, Dir.AskKind, ExpeditionScripts.Status());
            Assert.IsTrue(Dir.MiraHasTheFloor);
            Assert.IsTrue(pd.Hushed || pd.Mood == PitchMood.Urgent, "the prince holds back while Mira asks");
            // The prince speaks up (an urgent word of his): Mira's question closes until he is done, then comes back.
            int h = SpeechBubbles.Say(pd.Prince, "Test: the camp needs a word.", 1.5f);
            pd.Prince.transform.position = W.Position + new Vector3(4f, 0, 4f);
            yield return Frames(3);
            Assert.IsTrue(Dir.PrinceBusy());
            Assert.AreEqual(0, Dir.AskHandle, "Mira yields while the prince speaks");
            yield return Until(() => !SpeechBubbles.IsOpen(h), 30f, "the prince to finish");
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskOpening, 30f, "Mira's question again");
            // On the road with Mira he neither pitches nor tags along.
            yield return Tap("reply_lead");
            yield return Until(() => C.Party.Mode == PartyMode.Travelling && C.Party.Leg != null && C.Party.Leg.Progress > 60, 120f, "the road");
            Assert.IsTrue(Dir.MiraHasTheFloor);
            if (pd.Mood != PitchMood.Urgent)
            {
                Assert.IsTrue(pd.Hushed);
                Assert.AreEqual(0, pd.AskHandle, "no pitch on Mira's road");
                Assert.Greater(BoardSpace.Flat(pd.Prince.transform.position - W.Position), 20f, "the prince stays with his people");
            }
        }

        // ---------------- the Ash expedition in the world ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator AshRunners_Script_SeenCarryingTheCrystal_AndWordComes()
        {
            yield return RunScript("ash_runners");
            Assert.AreNotEqual(Custody.AtSite, C.Crystal(Element.Earth).Custody);
            Assert.Greater(Dir.Ash.ReportsSeen, 0);
            Assert.IsNotNull(Dir.Ash.LastReport);
            Assert.IsTrue(Dir.Cues.Exists(c => c.StartsWith("Word came")), "reports are cued");
            MapScreen.Open();
            yield return Frames(3);
            Assert.IsTrue(new List<ExpeditionMapLayer.Mark>(Dir.MapLayer.Marks).Exists(m => m.Kind == "ash" && m.Label.Contains("ago")), "the last report is on the map with its age");
            MapScreen.ClearTarget();
            MapScreen.Close();
        }

        // ---------------- a choice ----------------

        /// <summary>The authored Earth pack has no riddle yet: the test adds one at the boundary stone (in memory, test text).</summary>
        private static void AddRiddleAtTheStone(CampaignDefinition d)
        {
            d.Lines.Add(new LineDef { Id = "t_ring_question", Speaker = "mira", Topic = "earth", Text = "The ring is cut on one slab of three. Which do we push?" });
            d.Lines.Add(new LineDef { Id = "t_ring_wrong", Speaker = "mira", Topic = "earth", Text = "It won't move. Not that one." });
            d.Lines.Add(new LineDef { Id = "t_ring_north", Speaker = "", Topic = "reply", Text = "The north slab" });
            d.Lines.Add(new LineDef { Id = "t_ring_ringed", Speaker = "", Topic = "reply", Text = "The ringed slab" });
            d.Lines.Add(new LineDef { Id = "t_ring_south", Speaker = "", Topic = "reply", Text = "The south slab" });
            var ch = new ChoiceDef { Id = "t_ring", QuestionLine = "t_ring_question", WrongLine = "t_ring_wrong", RevealedBy = MarkerRef.Surface("glade", "stone") };
            ch.Sets.Add("t.ring_known");
            ch.Answers.Add(new AnswerDef { Line = "t_ring_north" });
            ch.Answers.Add(new AnswerDef { Line = "t_ring_ringed", Correct = true });
            ch.Answers.Add(new AnswerDef { Line = "t_ring_south" });
            d.Choices.Add(ch);
            d.Event("earth_read_stone").Effects.Add(new EffectDef { Kind = EffectKind.OfferChoice, Ref = "t_ring" });
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Choice_AdviceGoesThroughAdviseMira_AWrongAnswerIsAskedAgain()
        {
            yield return StartCampaign(ExpeditionDev.AuthoredPack, AddRiddleAtTheStone);
            yield return Play(() => C.Party.Mode == PartyMode.AskingChoice && Dir.AskKind == ExpeditionDirector.AskChoice, 400f, "the riddle at the stone");
            var ch = C.Definition.Choice(C.Party.ChoiceId);
            Assert.AreEqual("t_ring", ch.Id);
            yield return Until(() => SpeechBubbles.IsShowing(Dir.AskHandle), 30f, "the question on screen");
            CollectionAssert.AreEqual(new[] { "The north slab", "The ringed slab", "The south slab" }, SpeechBubbles.RepliesOf(Dir.AskHandle));
            Assert.AreEqual(ExpeditionLines.Get(S, ch.QuestionLine), SpeechBubbles.TextOf(Dir.AskHandle));
            Assert.AreEqual(0, Count<AdviseMira>());
            // A wrong answer first: the same command, the rival is charged, the wrong line, the question again.
            long rival = C.RivalProgress; long at = S.Tick;
            yield return ExpeditionScripts.TapReply(_boot.AutoPlay, Dir.AskHandle, "The north slab");
            Assert.AreEqual(0, Last<AdviseMira>().Answer);
            Assert.AreEqual("t_ring", Last<AdviseMira>().ChoiceId);
            yield return Until(() => SaidIndex("t_ring_wrong") >= 0, 30f, "the wrong line");
            yield return Until(() => Dir.AskKind == ExpeditionDirector.AskChoice && SpeechBubbles.IsShowing(Dir.AskHandle), 60f, "the question again");
            Assert.GreaterOrEqual(C.RivalProgress - rival - (S.Tick - at), C.Definition.Tuning.WrongAnswerCharge);
            Assert.AreEqual(PartyMode.AskingChoice, C.Party.Mode);
            yield return ExpeditionScripts.TapReply(_boot.AutoPlay, Dir.AskHandle, "The ringed slab");
            Assert.AreEqual(1, Last<AdviseMira>().Answer);
            yield return Until(() => C.Knows("t.ring_known"), 10f, "the right answer");
            Assert.AreEqual(2, Count<AdviseMira>());
            Assert.AreNotEqual(PartyMode.AskingChoice, C.Party.Mode);
        }

        // ---------------- WU-6: the expedition as things in the world ----------------

        private IEnumerator Settle()
        {
            WorldActions.Cancel(); LedgerCard.Hide();
            float end = Time.realtimeSinceStartup + 8f;
            yield return new WaitForSecondsRealtime(0.5f);
            while (WorldActions.CameraMoving && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(WorldActions.CameraMoving, "the camera is still");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Pedestal_OffersMirasAct_AndTheSecondTapSendsOneCommand()
        {
            // The Root Chamber lead is made the top one at once (a test edit of the definition); nothing else changes.
            yield return StartCampaign(ExpeditionDev.AuthoredPack, d =>
            {
                var root = d.Leads.Find(x => x.Id == "earth_root");
                root.Requires.Clear(); root.Priority = 1000;
                // The way down is open from the start, so the party can reach the pedestal (nothing else changes).
                foreach (var loc in d.Locations) foreach (var mk in loc.Markers) if (mk.Id == "stair") mk.OpenWhen = "";
                foreach (var cn in d.Connections) if (cn.OpenWhen == "earth.stair_open") cn.OpenWhen = "";
            });
            yield return Until(() => CampaignApi.TopLead(S) != null && CampaignApi.TopLead(S).DefId == "earth_root", 20f, "the Root Chamber lead");
            int leadId = CampaignApi.TopLead(S).Id;
            var pedestal = InteriorScripts.Thing("glade/quarry_upper/pedestal").GetComponent<MiraActThing>();
            Assert.IsNotNull(pedestal, "the pedestal carries Mira's act");
            Assert.AreEqual(WorldZones.CryInteract, pedestal.GetComponent<WorldZone>().ZoneId);
            yield return Settle();
            int before = Count<AcceptLead>();
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapObject(pedestal), "the first tap offers her act");
            Assert.AreEqual(WorldActionState.Preview, WorldActions.State);
            Assert.IsTrue(LedgerCard.IsOpen);
            StringAssert.Contains("Mira", LedgerCard.Content.Question);
            Assert.AreEqual(before, Count<AcceptLead>(), "a preview sends nothing");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(TapOutcome.Confirm, PointerInput.TapObject(pedestal), "the second tap commits it");
            Assert.AreEqual(before + 1, Count<AcceptLead>(), "exactly one command");
            Assert.AreEqual(leadId, Last<AcceptLead>().LeadId);
            Assert.AreEqual("AcceptLead", Dir.LastCommand);
            // A burst of taps right after sends nothing more (the tap guard).
            PointerInput.TapObject(pedestal); PointerInput.TapObject(pedestal);
            Assert.AreEqual(before + 1, Count<AcceptLead>());
            // A thing where she has no errand only reads itself.
            yield return Settle();
            var marks = InteriorScripts.Thing("glade/quarry_upper/marks").GetComponent<MiraActThing>();
            Assert.AreEqual(TapOutcome.Refused, PointerInput.TapObject(marks));
            Assert.AreEqual(before + 1, Count<AcceptLead>());
            Assert.IsTrue(LedgerCard.IsOpen);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompanionCard_NamesTheLiving_AndTheFallenAreRemembered()
        {
            yield return StartCampaign();
            var garrick = Dir.MateOf("garrick");
            Assert.IsNotNull(garrick);
            var card = garrick.Npc.GetComponent<CompanionCard>();
            Assert.IsNotNull(card);
            Assert.AreEqual(WorldZones.CryParty, garrick.Npc.GetComponent<WorldZone>().ZoneId);
            yield return Settle();
            PointerInput.TapObject(card);
            Assert.IsTrue(LedgerCard.IsOpen);
            Assert.AreEqual(garrick.Def.Name, LedgerCard.Content.Title);
            var text = string.Join(" | ", LedgerCard.Content.AllText());
            StringAssert.Contains("Alive", text);
            StringAssert.Contains(garrick.Def.Role.TrimEnd('.'), text);
            Assert.IsNull(LedgerCard.Content.Question, "on the surface there is only the card");
            Assert.AreEqual(0, Count<AcceptLead>() + Count<SetCompany>());
            // Another companion is named the same way.
            var ivo = Dir.MateOf("ivo");
            yield return Settle();
            PointerInput.TapObject(ivo.Npc.GetComponent<CompanionCard>());
            Assert.AreEqual(ivo.Def.Name, LedgerCard.Content.Title);
            // Garrick falls (a developer command): the card remembers him.
            Assert.IsTrue(_boot.Sim.Submit(new DevSetCompanion { Id = "garrick", Alive = false }).Ok);
            yield return Until(() => !garrick.Alive, 10f, "Garrick fallen");
            yield return Settle();
            PointerInput.TapObject(card);
            Assert.AreEqual(garrick.Def.Name, LedgerCard.Content.Title);
            text = string.Join(" | ", LedgerCard.Content.AllText());
            StringAssert.Contains("Fallen", text);
            StringAssert.DoesNotContain("Alive", text);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator LeadMeOut_FromACompanionCard_ReturnsToTheSurfaceAndTheHubView()
        {
            yield return StartCampaign();
            Interiors.FactsOverride = f => true; // the story has not opened the ways yet: they are open for this walk
            Assert.IsTrue(Dir.PlayerPresent);
            W.Teleport(InteriorScripts.CellOf("glade/quarry_upper/safe"), 0);
            yield return Frames(3);
            Assert.AreEqual("quarry_upper", Interiors.Current);
            var card = Dir.MateOf("ivo").Npc.GetComponent<CompanionCard>();
            yield return Settle();
            Assert.AreEqual(TapOutcome.Preview, PointerInput.TapObject(card));
            StringAssert.Contains("lead you out", LedgerCard.Content.Question);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(TapOutcome.Confirm, PointerInput.TapObject(card));
            float end = Time.realtimeSinceStartup + 120f;
            while ((Interiors.Current != null || Interiors.IsCrossing || Interiors.IsLeadingOut) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsNull(Interiors.Current, "on the surface");
            MarkerRef.TryParse("glade/stair", out var stair);
            Assert.AreEqual(Interiors.LinkAt(stair).Cell, W.Cell, "at the glade's stair");
            Assert.AreEqual(ViewMode.FirstPerson, ViewSwitcher.Instance.CurrentView, "the walking view");
            yield return Frames(3);
            Assert.IsFalse(InteriorLighting.Instance.Underground, "the sky's light again");
            Assert.AreEqual("", Dir.PlayerSpace);
        }

        /// <summary>Decision 11: going down the glade's stair from the hub and asking Mira to lead out puts the player back on the hub cell and heading.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator LeadMeOut_AfterSettingOutFromTheHub_ReturnsToTheHubCellAndHeading()
        {
            yield return StartCampaign();
            Interiors.FactsOverride = f => true;
            W.StopAuto();
            var home = W.Cell; int heading = (W.Heading + 3) % 6;
            InteriorDirector.Instance.SetHubPose(home, heading);
            MarkerRef.TryParse("glade/stair", out var stairRef);
            var stair = Interiors.LinkAt(stairRef);
            W.Teleport(stair.Cell + WalkLattice.Dir(stair.OutHeading), stair.OutHeading + 3);
            yield return Frames(3);
            W.Forward();
            float end = Time.realtimeSinceStartup + 40f;
            while ((Interiors.Current == null || Interiors.IsCrossing || W.IsGliding) && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual("quarry_upper", Interiors.Current, "down the stair");
            yield return Frames(3);
            Assert.IsTrue(Dir.LeadMeOut());
            end = Time.realtimeSinceStartup + 120f;
            while ((Interiors.Current != null || Interiors.IsCrossing || Interiors.IsLeadingOut) && Time.realtimeSinceStartup < end) yield return null;
            yield return Frames(3);
            Assert.IsNull(Interiors.Current, "on the surface");
            Assert.AreEqual(home, W.Cell, "on the hub cell");
            Assert.AreEqual(heading, W.Heading, "with the heading it set out with");
            Assert.AreEqual(ViewMode.FirstPerson, ViewSwitcher.Instance.CurrentView);
            Assert.IsFalse(W.IsLocked);
            Assert.IsFalse(InteriorLighting.Instance.Underground, "the sky's light again");
            Assert.AreEqual(CampaignPhase.Running, S.Campaign.Phase, "the campaign is kept");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Pouch_NamesTheCarriedCrystals_AndTheJournalBookOpensTheJournal()
        {
            yield return StartCampaign();
            Assert.IsNotNull(Dir.Pouch); Assert.IsNotNull(Dir.Book);
            Assert.AreEqual(WorldZones.CryPack, Dir.Pouch.GetComponent<WorldZone>().ZoneId);
            Assert.AreEqual(WorldZones.CryCamp, Dir.Book.GetComponent<WorldZone>().ZoneId);
            yield return Settle();
            PointerInput.TapObject(Dir.Pouch);
            Assert.AreEqual("Mira's pouch", LedgerCard.Content.Title);
            StringAssert.Contains("Nothing", string.Join(" ", LedgerCard.Content.Lines));
            WorldActions.Cancel();
            Assert.IsTrue(_boot.Sim.Submit(new DevSetCustody { Crystal = Element.Earth, To = Custody.Carried }).Ok);
            yield return Until(() => Dir.CarriedShown == 1, 10f, "the crystal in her hands");
            yield return Settle();
            PointerInput.TapObject(Dir.Pouch);
            StringAssert.Contains("one crystal", string.Join(" ", LedgerCard.Content.Lines));
            Assert.AreEqual(1, Dir.CarriedNames().Count);
            WorldActions.Cancel(); LedgerCard.Hide();
            Assert.IsFalse(MapScreen.IsOpen);
            PointerInput.TapObject(Dir.Book);
            Assert.IsTrue(MapScreen.IsOpen, "the journal book opens the journal");
            Assert.IsTrue(Dir.Journal.Expanded);
            // P2a: a book view, not the side column, and big enough to tap
            yield return Frames(3);
            var book = Dir.Journal.BookRect;
            Assert.Greater(book.width, Screen.width * 0.6f, "the journal is an open book over the map");
            var safe = ScreenSafeArea.Current();
            Assert.GreaterOrEqual(book.x, safe.x - 0.5f); Assert.LessOrEqual(book.xMax, safe.xMax + 0.5f);
            Assert.GreaterOrEqual(ExpeditionJournal.BookFont(390f), 15, "the page text is readable at 844 x 390");
            var pouchBox = Dir.Pouch.GetComponent<BoxCollider>(); var bookBox = Dir.Book.GetComponent<BoxCollider>();
            Assert.GreaterOrEqual(Mathf.Min(pouchBox.size.x, pouchBox.size.y), 1.2f, "the pouch has a larger tap volume");
            Assert.GreaterOrEqual(Mathf.Min(bookBox.size.x, bookBox.size.y), 1.2f, "so has the book");
            MapScreen.Close();
        }
    }
}
