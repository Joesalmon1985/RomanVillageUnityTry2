using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Eras;

namespace World.Game
{
    /// <summary>
    /// Package I6's AutoPlay scripts (PRD_CRYSTALS 7 Slice 1, 9.2). Each starts from the developer start
    /// <c>dev.historic</c> plus <c>StartCampaign</c> on the authored pack and drives the Earth slice the way a player
    /// does: replies are tapped on the screen where the bubble draws them (the key path only when a reply is not on
    /// screen), the walker follows Mira, the map is tapped. Editor or packaged with <c>-autoplay &lt;name&gt;</c>
    /// (<c>-timescale &lt;x&gt;</c>; bubbles keep game time, so the game-minute timings do not change).
    /// <list type="bullet">
    /// <item><c>earth_first</c>: the opening, "Lead the way", the glade, the stair, the marks, the Deepstone on its
    /// pedestal, out, the Rooted Tower, "Set it": arriving first.</item>
    /// <item><c>earth_stolen</c>: the dev command sends the Ash expedition ahead; the bare pedestal, the tally, the
    /// Tally Vault, "What happens if we lose?", "Fight", a loss (Garrick stays), again, a win, out, restored.</item>
    /// <item><c>earth_stolen_win</c>: the same with a win at the first attempt (nobody dies).</item>
    /// <item><c>mira_alone</c>: "Go on ahead, I'll find you"; Mira does the route alone (the Ash expedition held back,
    /// as the proof's away run does); the cues and the map say where she is; the map's marker walks the player to the
    /// tower; "I'm with you"; "Set it".</item>
    /// <item><c>expedition_captures</c>: <c>earth_stolen</c> with night captures of every staged moment.</item>
    /// </list>
    /// The duel itself is package I7's: while it is not in the build the director's stand-in asks how it ends, and
    /// the script answers it (the result goes through <c>ResolveEncounter</c> like any duel's).
    /// </summary>
    public static class ExpeditionScripts
    {
        /// <summary>Settings for editor runs (the command line overrides them).</summary>
        public static float TimeScale = 1f;
        /// <summary>Captures of the staged moments (Evidence/screenshots, i6_*.png), taken at night.</summary>
        public static bool Captures;
        /// <summary>Seed of the developer start (null = the scene's).</summary>
        public static ulong? Seed;

        /// <summary>The last run's report: timings in game minutes since the campaign began, taps, cues.</summary>
        public static string LastReport { get; private set; }
        public static IReadOnlyList<KeyValuePair<string, long>> LastTimings => s_timings;
        /// <summary>Replies answered by a tap on the screen, and by the key path because the reply was not on screen.</summary>
        public static int PointerReplies { get; private set; }
        public static int KeyReplies { get; private set; }

        private static readonly List<KeyValuePair<string, long>> s_timings = new List<KeyValuePair<string, long>>();
        private static bool s_registered;

        public static void Register()
        {
            if (s_registered) return;
            s_registered = true;
            AutoPlayScripts.Register("earth_first", () => Start("earth_first", EarthFirst));
            AutoPlayScripts.Register("earth_stolen", () => Start("earth_stolen", a => EarthStolen(a, "earth_stolen", true)));
            AutoPlayScripts.Register("earth_stolen_win", () => Start("earth_stolen_win", a => EarthStolen(a, "earth_stolen_win", false)));
            AutoPlayScripts.Register("mira_alone", () => Start("mira_alone", MiraAlone, d => d.Rival.Steps[0].MarchCost = 60000));
            AutoPlayScripts.Register("expedition_captures", () => { Captures = true; return Start("expedition_captures", a => EarthStolen(a, "expedition_captures", true)); });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_registered = false; TimeScale = 1f; Captures = false; Seed = null; LastReport = null; s_timings.Clear(); PointerReplies = 0; KeyReplies = 0; }

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static PlayerWalker W => WorldBootstrap.Instance.Avatar;
        private static ExpeditionDirector Dir => ExpeditionDirector.Instance;
        private static WorldState S => WorldBootstrap.Instance.Sim.State;
        private static CampaignState C => S.Campaign;
        private static float Now => S.Tick / (float)WorldSim.TicksPerSecond;

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static void ApplyTimeScale()
        {
            float ts = float.TryParse(Arg("-timescale"), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : TimeScale;
            Time.timeScale = Mathf.Clamp(ts, 0.1f, 16f);
            B.Clock.MaxTicksPerFrame = Mathf.Max(5, Mathf.CeilToInt(Time.timeScale * WorldSim.TicksPerSecond / 30f) + 2);
        }

        // =====================================================================================================
        // Start: dev.historic, then StartCampaign
        // =====================================================================================================

        /// <summary>
        /// The script's two halves: on a world that is not the developer start it rebuilds the scene on dev.historic
        /// and goes on there (the reload ends the running script); then it sends StartCampaign and plays.
        /// </summary>
        private static AutoPlayScript Start(string name, Func<AutoPlay, IEnumerator> play, Action<CampaignDefinition> edit = null)
        {
            var s = new AutoPlayScript(name);
            s.Step(a => Boot(a, name, play, edit));
            return s;
        }

        private static IEnumerator Boot(AutoPlay a, string name, Func<AutoPlay, IEnumerator> play, Action<CampaignDefinition> edit)
        {
            if (DevStart.Current != ScenarioStarts.DevHistoric && S.Campaign == null)
            {
                var second = new AutoPlayScript(name);
                second.Step(b => Begin(b, name, play, edit));
                AutoPlay.ContinueAfterLoad = second;
                if (!DevStart.Begin(ScenarioStarts.DevHistoric, Seed)) { AutoPlay.ContinueAfterLoad = null; a.Fail("dev.historic could not be built: " + DevStart.LastError); }
                // The scene reloads under this script; its second half runs in the new scene.
                yield return new WaitForSecondsRealtime(600f);
                yield break;
            }
            yield return Begin(a, name, play, edit);
        }

        private static IEnumerator Begin(AutoPlay a, string name, Func<AutoPlay, IEnumerator> play, Action<CampaignDefinition> edit)
        {
            s_timings.Clear(); PointerReplies = 0; KeyReplies = 0; LastReport = null;
            yield return null; yield return null;
            ApplyTimeScale();
            if (S.Campaign == null)
            {
                CampaignDefinition d = null;
                try { d = ExpeditionDev.LoadPack(ExpeditionDev.AuthoredPack); edit?.Invoke(d); }
                catch (Exception e) { a.Fail("the authored pack did not load: " + e.Message); }
                if (a.Failed) yield break;
                var r = ExpeditionDev.StartCampaign(d);
                if (!r.Ok) { a.Fail("StartCampaign refused: " + r.Message); yield break; }
            }
            yield return WaitFor(a, () => Dir != null && S.Campaign != null, 10f, "the expedition director");
            if (a.Failed) yield break;
            var plan = C.RivalPlan ?? C.Definition.Rival.Steps;
            Debug.Log("[AutoPlay] " + name + ": campaign " + C.Definition.Id + " on " + (DevStart.Current ?? "this world") + ", seed " + S.Seed + ", tick " + S.Tick
                      + ", the Ash march on the first crystal takes " + ExpeditionJournal.Clock(plan[0].MarchCost) + ", interiors " + (Dir.StandInInteriors ? "stand-in (no package I5)" : "package I5")
                      + ", time x" + Time.timeScale.ToString("0.#", CultureInfo.InvariantCulture) + ", " + Screen.width + "x" + Screen.height);
            yield return play(a);
            Time.timeScale = 1f;
            if (a.Failed) yield break;
            Report(name);
            Debug.Log("[AutoPlay] " + name + " OK");
            if (!Application.isEditor) Application.Quit(0);
        }

        private static void Report(string name)
        {
            var sb = new StringBuilder();
            sb.Append("[AutoPlay] ").Append(name).Append(" timings (game m:ss since the campaign began):");
            foreach (var kv in s_timings) sb.Append(' ').Append(kv.Key).Append(' ').Append(ExpeditionJournal.Clock(kv.Value)).Append(';');
            sb.Append(" replies by pointer ").Append(PointerReplies).Append(", by key ").Append(KeyReplies);
            sb.Append("; legs ").Append(Dir.LegCount).Append(", calls ").Append(Dir.Calls).Append(", cues ").Append(Dir.Cues.Count).Append(", lines said ").Append(Dir.Said.Count);
            sb.Append("; pack lines missing (stand-ins used): ").Append(string.Join(", ", new List<string>(ExpeditionLines.UsedFallbacks)));
            LastReport = sb.ToString();
            Debug.Log(LastReport);
        }

        // =====================================================================================================
        // Shared steps
        // =====================================================================================================

        private static IEnumerator WaitFor(AutoPlay a, Func<bool> cond, float gameSeconds, string what)
        {
            float end = Now + gameSeconds;
            float realEnd = Time.realtimeSinceStartup + gameSeconds * 2f + 60f;
            while (!cond())
            {
                if (Now > end || Time.realtimeSinceStartup > realEnd) { a.Fail("timed out waiting for " + what + " (" + Status() + ")"); yield break; }
                yield return null;
            }
        }

        private static void Stamp(string what)
        {
            foreach (var kv in s_timings) if (kv.Key == what) return;
            s_timings.Add(new KeyValuePair<string, long>(what, S.Tick - C.StartTick));
            Debug.Log("[AutoPlay] " + ExpeditionJournal.Clock(S.Tick - C.StartTick) + " " + what + " (" + Status() + ")");
        }

        /// <summary>One line of where everything stands (failure messages, logs).</summary>
        public static string Status()
        {
            if (B == null || S.Campaign == null) return "no campaign";
            var p = C.Party; var d = Dir;
            return "tick " + S.Tick + ", party " + p.Mode + " at " + p.At + (p.Mode == PartyMode.Travelling && p.Leg != null ? " to " + p.Leg.To : "")
                   + ", player " + (p.PlayerPresent ? "present" : "away") + (d != null ? ", ask " + (d.AskKind ?? "-") + ", space " + (d.MiraSpace.Length > 0 ? d.MiraSpace : "surface") + (d.Railed ? " (led)" : "") : "")
                   + ", earth " + C.Crystal(Element.Earth).Custody + ", rival " + C.Rival.Phase + " step " + C.Rival.Step + ", alive " + CountAlive();
        }

        private static int CountAlive() { int n = 0; foreach (var k in C.Companions) if (k.Alive) n++; return n; }
        private static bool Knows(string fact) => C.Knows(fact);
        /// <summary>The text of a reply or line id as it is shown now.</summary>
        public static string Text(string id) => ExpeditionLines.Get(S, id, id);

        /// <summary>
        /// For tests: plays as a willing player until the condition holds ("Lead the way", "Coming", "Fight", "Set it",
        /// the right answer), settling each duel of the stand-in with the next of the given results.
        /// </summary>
        public static IEnumerator PlayUntil(AutoPlay a, Func<bool> until, float gameSeconds, string what, params DuelResult[] duels)
        {
            var plan = new Plan();
            foreach (var r in duels) plan.Duels.Enqueue(r);
            yield return Drive(a, plan, until, gameSeconds, what);
        }

        /// <summary>
        /// Answers the open bubble with the reply of that text by a tap on the screen where the reply is drawn. A
        /// folded question is unfolded by a tap on its body first. Only when the bubble is not on screen is the reply
        /// given by the key path (keys 1 to 3), and counted as such.
        /// </summary>
        public static IEnumerator TapReply(AutoPlay a, int handle, string replyText)
        {
            var replies = SpeechBubbles.RepliesOf(handle);
            int index = -1;
            if (replies != null) for (int i = 0; i < replies.Count; i++) if (replies[i] == replyText) index = i;
            if (index < 0) { a.Fail("no reply \"" + replyText + "\" on the bubble \"" + SpeechBubbles.TextOf(handle) + "\" (" + (replies != null ? string.Join(" / ", replies) : "none") + ")"); yield break; }
            for (int frame = 0; frame < 45 && SpeechBubbles.IsOpen(handle); frame++)
            {
                var r = SpeechBubbles.ReplyRectOf(handle, index);
                if (r.width > 1f && r.yMax > 0 && r.y < Screen.height && r.xMax > 0 && r.x < Screen.width)
                {
                    PointerInput.TapAt(new Vector2(r.center.x, Screen.height - r.center.y));
                    if (!SpeechBubbles.IsOpen(handle)) { PointerReplies++; yield return null; yield break; }
                }
                else
                {
                    var body = SpeechBubbles.ScreenRectOf(handle);
                    if (body.width > 1f) PointerInput.TapAt(new Vector2(body.center.x, Screen.height - body.center.y)); // unfold
                }
                yield return null;
            }
            if (SpeechBubbles.IsOpen(handle))
            {
                if (!SpeechBubbles.Choose(handle, index)) { a.Fail("the reply \"" + replyText + "\" could not be chosen"); yield break; }
                KeyReplies++;
            }
            yield return null;
        }

        private sealed class Plan
        {
            public string Opening = ExpeditionLines.ReplyLead;
            public bool TellMore, WhyOnce, IfLose, NotYetOnce;
            public readonly Queue<DuelResult> Duels = new Queue<DuelResult>();
            public readonly List<Moment> Moments = new List<Moment>();
            public Moment On(string name, Func<bool> when, string capture = null, float delay = 0f)
            {
                var m = new Moment { Name = name, When = when, Capture = capture, Delay = delay };
                Moments.Add(m);
                return m;
            }
        }

        private sealed class Moment
        {
            public string Name, Capture;
            public Func<bool> When;
            public float Delay;
            public bool Done;
        }

        private static IEnumerator Capture(AutoPlay a, string name)
        {
            if (!Captures) yield break;
            // At night, from the player's eye (the scrub is an offset on the game time and is not saved).
            if (DayNight.Instance != null) { DayNight.Instance.SetHour(23.5); yield return null; yield return null; }
            yield return a.DoCapture("i6_" + name);
        }

        /// <summary>
        /// Plays until the condition holds: answers Mira's questions as the plan says (taps), answers her call, and
        /// stamps the plan's moments as they come (with a capture when asked).
        /// </summary>
        private static IEnumerator Drive(AutoPlay a, Plan plan, Func<bool> until, float gameSeconds, string what)
        {
            float end = Now + gameSeconds;
            float realEnd = Time.realtimeSinceStartup + gameSeconds * 2f + 120f;
            while (true)
            {
                foreach (var m in plan.Moments)
                {
                    if (m.Done || !m.When()) continue;
                    m.Done = true;
                    Stamp(m.Name);
                    if (m.Capture != null && Captures)
                    {
                        if (m.Delay > 0f) yield return a.DoWait(m.Delay / Mathf.Max(1f, Time.timeScale));
                        yield return Capture(a, m.Capture);
                    }
                }
                if (until()) yield break;
                if (Now > end || Time.realtimeSinceStartup > realEnd) { a.Fail("timed out waiting for " + what + " (" + Status() + ")"); yield break; }
                var d = Dir;
                int h = d != null ? d.AskHandle : 0;
                if (h != 0 && SpeechBubbles.IsShowing(h))
                {
                    string reply = null;
                    switch (d.AskKind)
                    {
                        case ExpeditionDirector.AskOpening:
                            reply = plan.TellMore ? Text(ExpeditionLines.ReplyMore) : Text(plan.Opening);
                            if (plan.TellMore) { plan.TellMore = false; Stamp("asked \"Tell me more\""); }
                            else { Stamp("opening answered"); yield return Capture(a, "opening"); }
                            break;
                        case ExpeditionDirector.AskLead:
                            if (plan.WhyOnce) { plan.WhyOnce = false; reply = Text(ExpeditionLines.ReplyWhy); Stamp("asked \"Why there?\""); }
                            else reply = Text(ExpeditionLines.ReplyLead);
                            break;
                        case ExpeditionDirector.AskCall: reply = Text(ExpeditionLines.ReplyComing); break;
                        case ExpeditionDirector.AskTower:
                            Stamp("tower: \"Shall I set it?\"");
                            yield return Capture(a, "tower_before");
                            reply = Text(ExpeditionLines.ReplySet);
                            break;
                        case ExpeditionDirector.AskThreshold:
                            if (plan.IfLose) { plan.IfLose = false; reply = Text(ExpeditionLines.ReplyIfLose); Stamp("threshold: asked what happens if we lose"); yield return Capture(a, "threshold"); }
                            else if (plan.NotYetOnce) { plan.NotYetOnce = false; reply = Text(ExpeditionLines.ReplyNotYet); Stamp("threshold: \"Not yet\""); }
                            else { reply = Text(ExpeditionLines.ReplyFight); Stamp("threshold: \"Fight\" (" + (d.ThresholdVolunteer.Length > 0 ? d.ThresholdVolunteer : "nobody") + " will hold the way)"); }
                            break;
                        case ExpeditionDirector.AskDuel:
                        {
                            if (plan.Duels.Count == 0) { a.Fail("a duel the script did not expect (" + Status() + ")"); yield break; }
                            var result = plan.Duels.Dequeue();
                            reply = Text(result == DuelResult.Victory ? ExpeditionLines.DuelWin : result == DuelResult.Defeat ? ExpeditionLines.DuelLose : ExpeditionLines.DuelClash);
                            Stamp("duel " + (d.DuelsStarted) + ": " + result + " (stand-in for package I7's duel)");
                            break;
                        }
                        case ExpeditionDirector.AskChoice:
                        {
                            var ch = C.Definition.Choice(C.Party.ChoiceId);
                            if (ch != null) foreach (var an in ch.Answers) if (an.Correct) reply = Text(an.Line);
                            break;
                        }
                    }
                    // A capture takes frames: the question may have been closed and asked again meanwhile.
                    if (reply != null && d.AskHandle == h && SpeechBubbles.IsShowing(h))
                    {
                        yield return TapReply(a, h, reply);
                        if (a.Failed) yield break;
                    }
                }
                yield return null;
            }
        }

        private static IEnumerator TapMira(AutoPlay a)
        {
            var mira = Dir.Mira;
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            var sp = cam.WorldToScreenPoint(mira.transform.position + Vector3.up * 1.1f);
            bool tapped = false;
            if (sp.z > 0.5f && sp.x > 2 && sp.x < Screen.width - 2 && sp.y > 2 && sp.y < Screen.height - 2)
            {
                var hit = PointerInput.Raycast(sp);
                if (ReferenceEquals(hit.Target, mira) && !SpeechBubbles.Contains(new Vector2(sp.x, Screen.height - sp.y))) { PointerInput.TapAt(sp); tapped = true; }
            }
            if (!tapped) PointerInput.TapObject(mira); // someone stands in front of her: as a tap on her
            yield return null;
        }

        // =====================================================================================================
        // earth_first
        // =====================================================================================================

        private static void EarthMoments(Plan plan)
        {
            plan.On("Mira's question", () => Dir.AskKind == ExpeditionDirector.AskOpening);
            plan.On("set out", () => Dir.LegCount > 0);
            plan.On("on the road", () => C.Party.Mode == PartyMode.Travelling && !C.Party.At.IsInterior && BoardSpace.Flat(Dir.Mira.transform.position - ExpeditionSpace.World(S, C.Party.At)) > 45f, "road");
            plan.On("at the boundary stone", () => C.Party.At.Location == "glade" && !C.Party.At.IsInterior && C.Party.Mode == PartyMode.Acting, "glade", 6f);
            plan.On("stair open", () => Knows("earth.stair_open"), "stair_open", 3f);
            plan.On("underground", () => Dir.MiraSpace == "quarry_upper");
            plan.On("in the quarry", () => Dir.MiraSpace == "quarry_upper" && C.Party.Mode == PartyMode.Travelling && C.Party.Leg != null && !Dir.LastLeg.Link, "quarry", 5f);
            plan.On("marks read", () => Knows("earth.root_way_known"));
        }

        private static IEnumerator EarthFirst(AutoPlay a)
        {
            var plan = new Plan { TellMore = true, WhyOnce = true };
            EarthMoments(plan);
            plan.On("at the pedestal", () => C.Party.At.Marker == "pedestal" && C.Party.Mode == PartyMode.Acting && C.Party.ActingAction == QuestAction.Take, "pedestal", 5f);
            plan.On("Deepstone taken", () => C.Crystal(Element.Earth).Custody == Custody.Carried, "taken", 2f);
            plan.On("back on the surface", () => C.Crystal(Element.Earth).Custody == Custody.Carried && Dir.MiraSpace.Length == 0);
            plan.On("at the Rooted Tower", () => C.Party.At.Location == "tower_earth" && C.Party.Mode == PartyMode.WaitingForPlayer);
            plan.On("setting it", () => C.Party.Mode == PartyMode.Acting && C.Party.ActingAction == QuestAction.Restore, "setting", 14f);
            yield return Drive(a, plan, () => C.Crystal(Element.Earth).Custody == Custody.Restored, 900f, "the Deepstone restored");
            if (a.Failed) yield break;
            Stamp("restored");
            yield return a.DoWait(6f / Mathf.Max(1f, Time.timeScale));
            yield return Capture(a, "tower_after");
            Check(a, C.Resolved.Count == 0, "arriving first means no lethal duel, but " + C.Resolved.Count + " were fought");
            Check(a, CountAlive() == 6, "all six alive, but " + CountAlive());
            Check(a, C.Party.PlayerPresent, "the player ended away from Mira");
            Check(a, Dir.Sites.Tower(Element.Earth).Restored, "the Rooted Tower's view is not lit");
            Check(a, Dir.Calls == 0 || PointerReplies > 0, "no reply was tapped");
        }

        private static void Check(AutoPlay a, bool ok, string message) { if (!ok) a.Fail(message + " (" + Status() + ")"); }

        // =====================================================================================================
        // earth_stolen
        // =====================================================================================================

        private static IEnumerator EarthStolen(AutoPlay a, string name, bool loseFirst)
        {
            // The dev command sends the Ash expedition ahead: it seizes the Deepstone and sets it on its altar.
            var r1 = ExpeditionDev.AshNextStep();
            if (!r1.Ok) { a.Fail("DevRivalNextStep refused: " + r1.Message); yield break; }
            yield return WaitFor(a, () => C.Crystal(Element.Earth).Custody != Custody.AtSite, 5f, "the Ash expedition to seize the Deepstone");
            if (a.Failed) yield break;
            if (C.Crystal(Element.Earth).Custody == Custody.RivalTransit) ExpeditionDev.AshNextStep();
            yield return WaitFor(a, () => C.Crystal(Element.Earth).Custody == Custody.AtRivalAltar, 5f, "the Deepstone on the Ash altar");
            if (a.Failed) yield break;
            Stamp("the Ash expedition sent ahead (dev)");
            var plan = new Plan { IfLose = true };
            if (loseFirst) plan.Duels.Enqueue(DuelResult.Defeat);
            plan.Duels.Enqueue(DuelResult.Victory);
            EarthMoments(plan);
            plan.On("the bare pedestal", () => Knows("earth.stolen_seen"), "bare", 1.5f);
            plan.On("tally followed", () => Knows("earth.vault_known"));
            plan.On("in the Tally Vault", () => Dir.MiraSpace == "tally_vault", "vault", 8f);
            plan.On("at Dolm's threshold", () => C.Party.Mode == PartyMode.AtThreshold);
            plan.On("the retreat", () => Dir.RetreatPlaying, "retreat", 1.6f);
            plan.On("back in the survey gallery", () => Dir.RetreatsPlayed > 0 && !Dir.RetreatPlaying, "after_loss", 3f);
            plan.On("Dolm beaten, the Deepstone recovered", () => Knows("earth.recovered"), "recovered", 3f);
            plan.On("out by the Ash cut", () => Knows("earth.recovered") && Dir.MiraSpace.Length == 0, "ash_cut", 2f);
            plan.On("at the Rooted Tower", () => C.Party.At.Location == "tower_earth" && C.Party.Mode == PartyMode.WaitingForPlayer);
            plan.On("setting it", () => C.Party.Mode == PartyMode.Acting && C.Party.ActingAction == QuestAction.Restore, "setting", 14f);
            yield return Drive(a, plan, () => C.Crystal(Element.Earth).Custody == Custody.Restored, 2400f, "the Deepstone recovered and restored");
            if (a.Failed) yield break;
            Stamp("restored");
            yield return a.DoWait(6f / Mathf.Max(1f, Time.timeScale));
            yield return Capture(a, "tower_after");
            Check(a, Knows("earth.ash_cut_open"), "the Ash cut did not open");
            if (loseFirst)
            {
                Check(a, CountAlive() == 5 && !C.Companion("garrick").Alive, "one loss should cost Garrick alone, but " + CountAlive() + " are alive");
                Check(a, Dir.RetreatsPlayed == 1 && Dir.LastVolunteer == "garrick", "the retreat with Garrick staying was not played (" + Dir.RetreatsPlayed + ", " + Dir.LastVolunteer + ")");
                Check(a, C.Resolved.Count == 2, "two attempts, but " + C.Resolved.Count);
                var mate = Dir.MateOf("garrick");
                Check(a, mate != null && !mate.Alive && mate.FellSpace == "tally_vault", "Garrick's body does not lie in the Tally Vault");
                foreach (var l in Dir.Said) Check(a, l.Speaker != "garrick" || l.Tick <= C.Companion("garrick").FellTick, "Garrick spoke after he fell: " + l.Text);
            }
            else
            {
                Check(a, CountAlive() == 6, "a win at the first attempt costs nobody, but " + CountAlive() + " are alive");
                Check(a, C.Resolved.Count == 1 && Dir.RetreatsPlayed == 0, "one attempt and no retreat expected");
            }
            Check(a, Dir.Sites.Tower(Element.Earth).Restored, "the Rooted Tower's view is not lit");
            yield return JournalAndMap(a);
        }

        /// <summary>The journal and the map layer: opened, checked, captured.</summary>
        private static IEnumerator JournalAndMap(AutoPlay a)
        {
            MapScreen.Open();
            yield return null; yield return null; yield return null;
            Check(a, Dir.MapLayer.Marks.Count > 0, "the expedition's map layer drew nothing");
            Check(a, Dir.Journal.PanelRect.width > 0, "the journal is not on the map");
            yield return Capture(a, "map");
            Dir.Journal.Expanded = true;
            yield return null; yield return null;
            yield return Capture(a, "journal");
            Dir.Journal.Expanded = false;
            MapScreen.ClearTarget();
            MapScreen.Close();
            yield return null;
        }

        // =====================================================================================================
        // mira_alone
        // =====================================================================================================

        private static IEnumerator MiraAlone(AutoPlay a)
        {
            // "Go on ahead, I'll find you."
            var plan = new Plan { Opening = ExpeditionLines.ReplyAhead };
            plan.On("Mira's question", () => Dir.AskKind == ExpeditionDirector.AskOpening);
            yield return Drive(a, plan, () => !C.Party.PlayerPresent, 150f, "\"Go on ahead, I'll find you\"");
            if (a.Failed) yield break;
            Stamp("left Mira at the camp");
            // Stay at the camp (a walk round the hearth) while she works alone; the cues say what she does.
            var away = new Plan();
            int cuesAtStart = Dir.Cues.Count;
            away.On("she set out alone", () => Dir.LegCount > 0);
            away.On("first cue", () => Dir.Cues.Count > cuesAtStart, "cue", 0.6f);
            away.On("stair open", () => Knows("earth.stair_open"));
            away.On("marks read", () => Knows("earth.root_way_known"));
            away.On("Deepstone taken by Mira alone", () => C.Crystal(Element.Earth).Custody == Custody.Carried);
            away.On("Mira waits at the Rooted Tower", () => C.Party.Mode == PartyMode.WaitingForPlayer && C.Party.At.Location == "tower_earth");
            bool mapShown = false;
            yield return Drive(a, away, () =>
            {
                if (!mapShown && Knows("earth.stair_open")) mapShown = true;
                return mapShown;
            }, 1500f, "Mira to open the stair alone");
            if (a.Failed) yield break;
            // The map while she is away: her marker and errand, the journal beside it.
            MapScreen.Open();
            yield return null; yield return null; yield return null;
            Check(a, Dir.MapLayer.MiraLabel().Length > 0, "the map does not say what Mira is doing");
            Debug.Log("[AutoPlay] the map says: " + Dir.MapLayer.MiraLabel() + " | journal: " + ExpeditionJournal.MiraSentence(S));
            yield return Capture(a, "map_away");
            MapScreen.Close();
            yield return null;
            // The HUD line while away.
            Hud.Visible = true;
            yield return null; yield return null;
            Check(a, !string.IsNullOrEmpty(Dir.Journal.HudLineShown), "no HUD line with the HUD on");
            yield return Capture(a, "hud_away");
            Hud.Visible = false;
            yield return Drive(a, away, () => C.Party.Mode == PartyMode.WaitingForPlayer && C.Party.At.Location == "tower_earth", 2400f, "Mira to wait at the Rooted Tower");
            if (a.Failed) yield break;
            Check(a, C.Crystal(Element.Earth).Custody == Custody.Carried, "she did not take the Deepstone alone");
            Check(a, Dir.Cues.Count - cuesAtStart >= 3, "fewer than three cues while away (" + (Dir.Cues.Count - cuesAtStart) + ")");
            Check(a, C.Resolved.Count == 0 && C.Attempt == null, "she fought alone");
            // Find her by the map: a tap on "Mira is waiting for you here" sets the walk.
            MapScreen.Open();
            yield return null; yield return null; yield return null;
            Check(a, Dir.MapLayer.MiraLabel().Contains("waiting for you"), "the map does not say she is waiting: " + Dir.MapLayer.MiraLabel());
            var gui = MapScreenView.Instance.WorldToGui(Dir.MapLayer.MiraPoint());
            bool took = MapScreenView.Instance.TapScreen(new Vector2(gui.x, Screen.height - gui.y));
            Check(a, took && Dir.MapLayer.TapsTaken > 0 && MapScreen.Target.HasValue, "the tap on Mira's marker did not set the walk");
            yield return Capture(a, "map_waiting");
            MapScreen.Close();
            Stamp("map tapped: walking to Mira");
            yield return WaitFor(a, () => !W.IsAutoWalking && !W.IsGliding, 400f, "the walk to the tower");
            if (a.Failed) yield break;
            Check(a, Dir.PlayerNear(40f), "the walk did not end near Mira (" + BoardSpace.Flat(W.Position - Dir.Mira.transform.position).ToString("0") + " m)");
            Stamp("found Mira");
            yield return a.DoWalkTo(() => Dir.Mira.transform.position, 6f, 60f);
            // Tap her: "I'm with you".
            yield return TapMira(a);
            yield return WaitFor(a, () => Dir.AskKind == ExpeditionDirector.AskMenu, 10f, "Mira's reply to the tap");
            if (a.Failed) yield break;
            yield return Capture(a, "rejoin");
            yield return TapReply(a, Dir.AskHandle, Text(ExpeditionLines.ReplyWith));
            yield return WaitFor(a, () => C.Party.PlayerPresent, 10f, "the player to count as present");
            if (a.Failed) yield break;
            Stamp("rejoined: \"I'm with you\"");
            var end = new Plan();
            end.On("setting it", () => C.Party.Mode == PartyMode.Acting && C.Party.ActingAction == QuestAction.Restore, "setting", 14f);
            yield return Drive(a, end, () => C.Crystal(Element.Earth).Custody == Custody.Restored, 300f, "the restoration");
            if (a.Failed) yield break;
            Stamp("restored");
            yield return a.DoWait(6f / Mathf.Max(1f, Time.timeScale));
            yield return Capture(a, "tower_after");
            Check(a, Dir.Sites.Tower(Element.Earth).Restored, "the Rooted Tower's view is not lit");
            Check(a, CountAlive() == 6, "all six alive, but " + CountAlive());
        }
    }
}
