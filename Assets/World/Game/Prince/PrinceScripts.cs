using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// Package N's AutoPlay scripts. Every one drives the player through the real paths (walker moves and follows,
    /// pointer taps on the screen, bubble replies) and runs in the editor or packaged with <c>-autoplay &lt;name&gt;</c>.
    /// <list type="bullet">
    /// <item><c>first_scene</c>: the opening of REFACTOR_SPEC 2.C.5: the prince reaches you, his exact line and
    /// replies, "What's it for?", "Lead the way", the press at plot 631 by a tap on the screen, the builder, the hut
    /// finished, its first load of wood followed to the storehouse. Logs <c>[AutoPlay] first_scene OK</c>.</item>
    /// <item><c>follow_prince</c>: plays a whole game the way a willing player does: answers each pitch after a delay,
    /// follows the speaker (or walks straight to the spot with <c>-princeMode direct</c>) and presses the ground as soon
    /// as it is in range; reports the result, game minute, casts, declines, metres walked and pace. Options:
    /// <c>-princeDelay &lt;s&gt;</c> (game seconds before each answer, default 3), <c>-princeMode follow|direct</c>,
    /// <c>-timescale &lt;x&gt;</c> (default 1; every timer runs on game time, so the numbers do not change),
    /// <c>-princeMinutes &lt;m&gt;</c> (stop after m game minutes, default 21).</item>
    /// <item><c>prompt_game</c>: <c>follow_prince</c> with no delay; fails unless it ends in Victory.</item>
    /// <item><c>ignore_prince</c>: does nothing at all; checks nothing is built for your people, the pitches keep
    /// coming and escalate, and the game ends in Defeat by overrun with its scene.</item>
    /// <item><c>prince_captures</c>: screenshots for checking by eye (Evidence/screenshots, n_*.png).</item>
    /// </list>
    /// </summary>
    public static class PrinceScripts
    {
        /// <summary>Settings for editor runs (the command line overrides them).</summary>
        public static float Delay = 3f;
        public static bool Direct;
        public static float TimeScale = 1f;
        public static float MaxMinutes = 21f;

        /// <summary>The last follow_prince / ignore_prince report (tests).</summary>
        public static string LastReport { get; private set; }
        public static GameOutcome LastOutcome { get; private set; }

        private static bool s_registered;

        public static void Register()
        {
            if (s_registered) return;
            s_registered = true;
            AutoPlayScripts.Register("first_scene", FirstScene);
            AutoPlayScripts.Register("follow_prince", () => FollowPrince("follow_prince", false));
            AutoPlayScripts.Register("prompt_game", () => FollowPrince("prompt_game", true));
            AutoPlayScripts.Register("ignore_prince", IgnorePrince);
            AutoPlayScripts.Register("prince_captures", Captures);
        }

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static PlayerWalker W => WorldBootstrap.Instance.Avatar;
        private static PrinceDirector D => PrinceDirector.Instance;
        private static WorldState S => WorldBootstrap.Instance.Sim.State;
        private static float Now => S.Tick / (float)WorldSim.TicksPerSecond;

        // ---------------- options ----------------

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static float ArgF(string name, float fallback) =>
            float.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static void ApplyTimeScale()
        {
            float ts = ArgF("-timescale", TimeScale);
            Time.timeScale = Mathf.Clamp(ts, 0.1f, 32f);
            // Enough ticks per frame for the scale at 30 fps or more.
            B.Clock.MaxTicksPerFrame = Mathf.Max(5, Mathf.CeilToInt(Time.timeScale * WorldSim.TicksPerSecond / 30f) + 2);
        }

        // ---------------- shared steps ----------------

        private static IEnumerator WaitFor(AutoPlay a, Func<bool> cond, float gameSeconds, string message)
        {
            float end = Now + gameSeconds;
            float realEnd = Time.realtimeSinceStartup + gameSeconds * 4f + 30f;
            while (!cond())
            {
                if (Now > end || Time.realtimeSinceStartup > realEnd) { a.Fail(message); yield break; }
                yield return null;
            }
        }

        /// <summary>Presses the ground at a point with a tap on the screen (as a click there); falls back to a ground press when it is off screen or under a bubble.</summary>
        public static TapOutcome PressGround(Vector3 point)
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (cam != null)
            {
                var sp = cam.WorldToScreenPoint(point);
                bool onScreen = sp.z > 0.5f && sp.x > 2 && sp.x < Screen.width - 2 && sp.y > Screen.height * 0.02f && sp.y < Screen.height - 2;
                if (onScreen && !SpeechBubbles.Contains(new Vector2(sp.x, Screen.height - sp.y)))
                {
                    var o = PointerInput.TapAt(sp);
                    if (o == TapOutcome.Cast || o == TapOutcome.CastRefused) return o;
                }
            }
            s_groundFallbacks++;
            return PointerInput.TapGround(point);
        }
        private static int s_groundFallbacks;

        private static Vector3 TargetOf(int proposalId)
        {
            FactionApi.ProposalTarget(S, proposalId, out double x, out double z, out _);
            var p = new Vector3((float)x, 0, (float)z);
            p.y = PlayerWalker.GroundHeight(p);
            return p;
        }

        /// <summary>The reply index with one of the labels (first match in label order), or -1.</summary>
        private static int ReplyIndex(int handle, params string[] labels)
        {
            var r = SpeechBubbles.RepliesOf(handle);
            if (r == null) return -1;
            foreach (var l in labels) for (int i = 0; i < r.Count; i++) if (r[i] == l) return i;
            return -1;
        }

        // =====================================================================================================
        // first_scene
        // =====================================================================================================

        public static AutoPlayScript FirstScene()
        {
            var s = new AutoPlayScript("first_scene");
            int ask = 0;
            long t0 = 0;
            s.Step(a => Do(() => { ApplyTimeScale(); t0 = S.Tick; }))
             .Step(a => WaitFor(a, () => D.AskHandle != 0 && SpeechBubbles.IsShowing(D.AskHandle) && D.ActiveSpeaker == D.Prince, 30f, "the prince did not reach the player within 30 s"))
             .Step(a => Do(() =>
             {
                 ask = D.AskHandle;
                 float secs = (S.Tick - t0) / 10f;
                 Debug.Log("[AutoPlay] first_scene: the prince asked after " + secs.ToString("0.0") + " game s, " + BoardSpace.Flat(D.Prince.transform.position - W.Position).ToString("0.0") + " m from the player");
                 Expect(a, SpeechBubbles.TextOf(ask) == PrinceLines.FirstLine, "first line: " + SpeechBubbles.TextOf(ask));
                 var r = SpeechBubbles.RepliesOf(ask);
                 Expect(a, r != null && r.Count == 3 && r[0] == PrinceLines.ReplyLead && r[1] == PrinceLines.ReplyWhat && r[2] == PrinceLines.ReplyCatchUp, "replies: " + (r == null ? "none" : string.Join(" | ", r)));
                 Expect(a, FacingPlayer(D.Prince, 25f), "the prince does not face the player");
             }))
             .Wait(0.4f).Capture("n_opening")
             // "What's it for?" gives the reason, then the two other replies.
             .Step(a => TapReply(a, () => ask, PrinceLines.ReplyWhat))
             .Step(a => WaitFor(a, () => D.AskHandle != 0 && D.AskHandle != ask, 5f, "no second question after What's it for?"))
             .Step(a => Do(() =>
             {
                 ask = D.AskHandle;
                 var open = FactionApi.Top(S, PlayerIds.Human);
                 Expect(a, open != null && SpeechBubbles.TextOf(ask) == open.Reason, "the reason: " + SpeechBubbles.TextOf(ask));
                 var r = SpeechBubbles.RepliesOf(ask);
                 Expect(a, r != null && r.Count == 2 && r[0] == PrinceLines.ReplyLead && r[1] == PrinceLines.ReplyCatchUp, "two replies: " + (r == null ? "none" : string.Join(" | ", r)));
             }))
             .Wait(0.3f).Capture("n_whats_it_for")
             .Step(a => TapReply(a, () => ask, PrinceLines.ReplyLead))
             .Step(a => Do(() => Expect(a, ReferenceEquals(W.Following, D.Prince), "the player is not following the prince")))
             .Step(a => WaitFor(a, () => D.CurrentPhase == PrinceDirector.Phase.Leading && OnScreen(D.Prince.transform.position + Vector3.up) && BoardSpace.Flat(D.Prince.transform.position - W.Position) > 6f, 10f, "not leading in view"))
             .Wait(0.3f).Capture("n_leading")
             .Step(a => WaitFor(a, () => D.CurrentPhase == PrinceDirector.Phase.AtSpot, 40f, "the prince did not reach the plot"))
             .Step(a => WaitFor(a, () => !W.IsGliding && !W.IsAutoWalking || SpellCaster.InRange(D.PitchedProposal), 15f, "the player did not catch up"))
             .Wait(1.2f).Capture("n_at_plot")
             .Step(a => Do(() =>
             {
                 var p = FactionApi.GetProposal(S, D.PitchedProposal);
                 Expect(a, p != null && p.Plot == 631 && p.BuildingType == (int)BuildingType.WoodcutterHut, "the opening is not the woodcutter's hut on plot 631");
                 Expect(a, D.FirstSceneStep == PrinceDirector.SceneAtPlot, "scene step " + D.FirstSceneStep);
                 Expect(a, D.Said.Any(l => l.Speaker == NpcIds.Prince && l.Text == p.Reason), "he did not explain at the plot");
             }))
             .Step(a => CastPitched(a))
             .Wait(0.25f).Capture("n_cast")
             .Step(a => Do(() =>
             {
                 bool site = false;
                 foreach (var b in S.Economy.Buildings) if (b.Plot == 631 && b.Type == BuildingType.WoodcutterHut && b.Faction == PlayerIds.Human) site = true;
                 Expect(a, site, "no woodcutter's hut site on plot 631");
                 Expect(a, D.FirstSceneStep == PrinceDirector.SceneBuilding, "scene step after the cast " + D.FirstSceneStep);
             }))
             .Step(a => WaitFor(a, () => D.Builder != null && !D.Builder.IsWalking, 30f, "the builder did not come"))
             .Wait(0.5f).Capture("n_builder")
             .Step(a => WaitFor(a, () => D.FirstSceneStep >= PrinceDirector.SceneFirstLoad, 120f, "the hut was not finished"))
             .Wait(0.5f).Capture("n_hut_done")
             .Step(a => WaitFor(a, () => D.AskHandle != 0 && ReplyIndex(D.AskHandle, PrinceLines.Follow) >= 0, 230f, "the prince did not point out the first load"))
             .Wait(0.3f).Capture("n_first_load")
             .Step(a => TapReply(a, () => D.AskHandle, PrinceLines.Follow))
             .Step(a => Do(() => Expect(a, W.Following != null && !(W.Following is Npc), "the player is not following a carrier")))
             .Wait(1f).Capture("n_following_load")
             .Step(a => WaitFor(a, () => D.FirstSceneStep == PrinceDirector.SceneDone, 60f, "the load did not reach the storehouse"))
             .Step(a => Do(() =>
             {
                 var camp = new CampLayout(S.Board);
                 float d = BoardSpace.Flat(W.Position - camp.Hearth);
                 Debug.Log("[AutoPlay] first_scene: followed the load to the storehouse, ending " + d.ToString("0.0") + " m from it at game " + Now.ToString("0.0") + " s");
                 Expect(a, d < 16f, "the player is " + d.ToString("0") + " m from the storehouse");
                 Expect(a, D.Said.Any(l => l.Text == PrinceLines.Bank(NpcIds.Prince, "first.home")[0]), "no word when the load arrived");
             }))
             .Wait(0.5f).Capture("n_load_home")
             .Log("[AutoPlay] first_scene OK")
             .Quit(0);
            return s;
        }

        private static bool OnScreen(Vector3 p)
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            var sp = cam.WorldToScreenPoint(p);
            return sp.z > 0 && sp.x > Screen.width * 0.1f && sp.x < Screen.width * 0.9f && sp.y > 0 && sp.y < Screen.height;
        }

        private static bool FacingPlayer(Npc n, float degrees)
        {
            var d = W.Position - n.transform.position; d.y = 0;
            return Vector3.Angle(n.transform.forward, d) <= degrees;
        }

        private static void Expect(AutoPlay a, bool ok, string message) { if (!ok) a.Fail(message); }

        private static IEnumerator Do(Action act)
        {
            try { act(); } catch (Exception e) { AutoPlay.Instance.Fail(e.GetType().Name + ": " + e.Message); }
            yield return null;
        }

        /// <summary>Taps a reply in a bubble with the pointer (a click on its rect); keys 1-3 when it is not drawn.</summary>
        private static IEnumerator TapReply(AutoPlay a, Func<int> handle, string label)
        {
            int h = handle();
            float end = Time.realtimeSinceStartup + 5f;
            int i = ReplyIndex(h, label);
            if (i < 0) { a.Fail("no reply " + label); yield break; }
            SpeechBubbles.Expand(h);
            Rect r = Rect.zero;
            while (Time.realtimeSinceStartup < end && (r = SpeechBubbles.ReplyRectOf(h, i)).width <= 0) yield return null;
            if (r.width > 0) PointerInput.TapAt(new Vector2(r.center.x, Screen.height - r.center.y));
            if (SpeechBubbles.IsOpen(h)) SpeechBubbles.Choose(h, i);
            yield return null;
        }

        private static IEnumerator CastPitched(AutoPlay a)
        {
            int id = D.PitchedProposal;
            if (id < 0) { a.Fail("nothing pitched"); yield break; }
            var target = TargetOf(id);
            W.StopAuto();
            if (!SpellCaster.InRange(id))
            {
                float range = (float)FactionApi.CastRangeMetres(S);
                yield return a.DoWalkTo(() => target, range - 1f, 30f);
            }
            yield return a.DoFace(() => target);
            var o = PressGround(target);
            if (o != TapOutcome.Cast) a.Fail("the cast was refused: " + SpellCaster.LastResult.Message);
            yield return null;
        }

        // =====================================================================================================
        // follow_prince / prompt_game
        // =====================================================================================================

        public static AutoPlayScript FollowPrince(string name, bool prompt)
        {
            var s = new AutoPlayScript(name);
            s.Step(a => FollowRoutine(a, name, prompt)).Quit(0);
            return s;
        }

        private static IEnumerator FollowRoutine(AutoPlay a, string name, bool prompt)
        {
            ApplyTimeScale();
            float delay = prompt ? 0f : ArgF("-princeDelay", Delay);
            string modeArg = Arg("-princeMode");
            bool direct = modeArg != null ? modeArg.Equals("direct", StringComparison.OrdinalIgnoreCase) : Direct;
            float maxMinutes = ArgF("-princeMinutes", MaxMinutes);
            s_groundFallbacks = 0;
            Debug.Log("[AutoPlay] " + name + ": delay " + delay + " s, mode " + (direct ? "direct" : "follow") + ", time x" + Time.timeScale);

            var d = D;
            Vector3 last = W.Position;
            float walked = 0f;
            int seenAsk = 0; float seenAt = 0f;
            int castsBefore = 0;
            var walkPerCast = new List<float>();
            float walkedAtCast = 0f;
            int directFor = -1;
            while (!FactionApi.Outcome(S).GameOver && Now < maxMinutes * 60f)
            {
                walked += BoardSpace.Flat(W.Position - last); last = W.Position;
                if (d.Casts.Count > castsBefore)
                {
                    castsBefore = d.Casts.Count;
                    walkPerCast.Add(walked - walkedAtCast); walkedAtCast = walked;
                }
                // 1. Answer the open question after the delay.
                int h = SpeechBubbles.NearestAsk(1);
                if (h != 0 && h != seenAsk) { seenAsk = h; seenAt = Now; }
                if (h != 0 && Now - seenAt >= delay)
                {
                    int i = ReplyIndex(h, PrinceLines.ReplyLead, PrinceLines.ShowMe, PrinceLines.Coming, PrinceLines.NotNow, DialoguePanel.FollowLabel);
                    if (i >= 0) { SpeechBubbles.Choose(h, i); yield return null; continue; }
                }
                // 2. Walk: follow the speaker (the reply did it) or go straight to the spot.
                int pid = d.PitchedProposal;
                var ph = d.CurrentPhase;
                bool led = pid >= 0 && (ph == PrinceDirector.Phase.Leading || ph == PrinceDirector.Phase.AtSpot);
                if (led && direct && directFor != pid && !SpellCaster.InRange(pid))
                {
                    directFor = pid;
                    var target = TargetOf(pid);
                    W.StopAuto();
                    yield return a.DoWalkTo(() => target, (float)FactionApi.CastRangeMetres(S) - 1f, 120f);
                    if (a.Failed) yield break;
                    continue;
                }
                if (led && !direct && ph == PrinceDirector.Phase.AtSpot && !SpellCaster.InRange(pid) && !W.IsAutoWalking && !W.IsGliding)
                {
                    // Followed but stopped short (e.g. the follow ended): walk the last steps.
                    var target = TargetOf(pid);
                    yield return a.DoWalkTo(() => target, (float)FactionApi.CastRangeMetres(S) - 1f, 60f);
                    if (a.Failed) yield break;
                    continue;
                }
                // 3. Press the ground as soon as it is in range.
                if (led && SpellCaster.InRange(pid) && !W.IsGliding)
                {
                    var target = TargetOf(pid);
                    W.StopAuto();
                    yield return a.DoFace(() => target);
                    if (FactionApi.GetProposal(S, pid) != null) PressGround(target);
                    yield return null;
                    continue;
                }
                yield return null;
            }
            // Report.
            var o = FactionApi.Outcome(S);
            LastOutcome = o.Outcome;
            var casts = d.Casts;
            var pitched = casts.Where(c => c.PitchedTick >= 0).Select(c => (c.CastTick - c.PitchedTick) / 10f).ToList();
            var gaps = new List<float>();
            for (int i = 1; i < casts.Count; i++) gaps.Add((casts[i].CastTick - casts[i - 1].CastTick) / 10f);
            string Avg(List<float> l) => l.Count == 0 ? "-" : l.Average().ToString("0.0", CultureInfo.InvariantCulture);
            string Med(List<float> l) { if (l.Count == 0) return "-"; var x = l.OrderBy(v => v).ToList(); return x[x.Count / 2].ToString("0.0", CultureInfo.InvariantCulture); }
            string kinds = string.Join(",", casts.GroupBy(c => c.Kind).Select(g => g.Key + ":" + g.Count()));
            string report = name + " result=" + (o.GameOver ? o.Outcome + "/" + o.Reason : "running") + " minute=" + (Now / 60f).ToString("0.0", CultureInfo.InvariantCulture)
                + " casts=" + casts.Count + " declines=" + d.Declines + " walked=" + walked.ToString("0") + "m"
                + " pitchToCast avg=" + Avg(pitched) + "s median=" + Med(pitched) + "s"
                + " castInterval avg=" + Avg(gaps) + "s median=" + Med(gaps) + "s"
                + " walkPerCast avg=" + Avg(walkPerCast) + "m median=" + Med(walkPerCast) + "m"
                + " delay=" + delay + " mode=" + (direct ? "direct" : "follow") + " timescale=" + Time.timeScale
                + " pitches=" + d.PitchCount + " fetches=" + d.Fetches + " calls=" + d.Calls + " groundFallbacks=" + s_groundFallbacks
                + " strength=" + FactionApi.Strength(S, PlayerIds.Human).Total + "/" + FactionApi.Strength(S, PlayerIds.Rival).Total
                + " kinds=" + kinds;
            LastReport = report;
            Debug.Log("[AutoPlay] " + report);
            for (int i = 0; i < casts.Count; i++)
                Debug.Log("[AutoPlay] " + name + " cast " + (i + 1) + ": t=" + (casts[i].CastTick / 10f).ToString("0") + "s " + casts[i].Kind + " by " + casts[i].Speaker
                    + " pitch->cast " + (casts[i].PitchedTick >= 0 ? ((casts[i].CastTick - casts[i].PitchedTick) / 10f).ToString("0.0") + "s" : "-")
                    + (i < walkPerCast.Count ? " walked " + walkPerCast[i].ToString("0") + "m" : ""));
            if (prompt && o.Outcome != GameOutcome.Victory) a.Fail("prompt_game did not win: " + report);
            else Debug.Log("[AutoPlay] " + name + " OK");
            Time.timeScale = 1f;
        }

        // =====================================================================================================
        // ignore_prince
        // =====================================================================================================

        public static AutoPlayScript IgnorePrince()
        {
            var s = new AutoPlayScript("ignore_prince");
            s.Step(a => IgnoreRoutine(a)).Quit(0);
            return s;
        }

        private static IEnumerator IgnoreRoutine(AutoPlay a)
        {
            ApplyTimeScale();
            float maxMinutes = ArgF("-princeMinutes", MaxMinutes);
            var d = D;
            int startBuildings = S.Economy.Buildings.Count(b => b.Faction == PlayerIds.Human);
            bool capturedPester = false;
            var moods = new HashSet<PitchMood>();
            while (!FactionApi.Outcome(S).GameOver && Now < maxMinutes * 60f)
            {
                if (d.AskHandle != 0) moods.Add(d.Mood);
                if (!capturedPester && d.PitchCount >= 3 && d.AskHandle != 0 && SpeechBubbles.IsShowing(d.AskHandle))
                {
                    capturedPester = true;
                    float ts = Time.timeScale; Time.timeScale = 1f;
                    yield return a.DoWait(0.4f);
                    yield return a.DoCapture("n_pester");
                    Time.timeScale = ts;
                }
                yield return null;
            }
            var o = FactionApi.Outcome(S);
            LastOutcome = o.Outcome;
            int built = S.Economy.Buildings.Count(b => b.Faction == PlayerIds.Human) - startBuildings;
            int roads = S.Economy.Roads.Count(r => r.Faction == PlayerIds.Human);
            int camps = S.Economy.Settlements.Count(st => st.Faction == PlayerIds.Human);
            int pitchLines = d.Said.Count(l => l.Ask);
            int distinct = d.Said.Where(l => l.Ask).Select(l => l.Text).Distinct().Count();
            bool repeat = false;
            var lastBy = new Dictionary<string, string>();
            foreach (var l in d.Said)
            {
                if (lastBy.TryGetValue(l.Speaker, out var prev) && prev == l.Text) repeat = true;
                lastBy[l.Speaker] = l.Text;
            }
            string report = "ignore_prince result=" + (o.GameOver ? o.Outcome + "/" + o.Reason : "running") + " minute=" + (Now / 60f).ToString("0.0", CultureInfo.InvariantCulture)
                + " casts=" + S.Factions.Casts[PlayerIds.Human] + " newBuildings=" + built + " roads=" + roads + " camps=" + camps
                + " pitches=" + d.PitchCount + " distinctPitchLines=" + distinct + "/" + pitchLines + " resigned=" + d.ResignedCount
                + " moods=" + string.Join(",", moods) + " strength=" + FactionApi.Strength(S, PlayerIds.Human).Total + "/" + FactionApi.Strength(S, PlayerIds.Rival).Total;
            LastReport = report;
            Debug.Log("[AutoPlay] " + report);
            if (S.Factions.Casts[PlayerIds.Human] != 0 || built != 0 || roads != 0 || camps != 1) { a.Fail("something was built for an idle player: " + report); yield break; }
            if (d.PitchCount < 3 || d.ResignedCount < 1) { a.Fail("the pestering did not escalate: " + report); yield break; }
            if (repeat) { a.Fail("a villager said the same sentence twice in a row"); yield break; }
            if (o.Outcome != GameOutcome.Defeat || o.Reason != OutcomeReasons.Overrun) { a.Fail("no overrun loss: " + report); yield break; }
            Time.timeScale = 1f;
            yield return WaitFor(a, () => d.EndingFinished, 60f, "the loss scene did not play");
            yield return a.DoWait(1f);
            yield return a.DoCapture("n_loss");
            Debug.Log("[AutoPlay] ignore_prince OK");
        }

        // =====================================================================================================
        // prince_captures: the conversation at a glance
        // =====================================================================================================

        public static AutoPlayScript Captures()
        {
            var s = new AutoPlayScript("prince_captures");
            s.Step(a => WaitFor(a, () => D.AskHandle != 0 && SpeechBubbles.IsShowing(D.AskHandle), 30f, "no opening question"))
             .Wait(0.5f).Capture("n_opening")
             .Step(a => TapReply(a, () => D.AskHandle, PrinceLines.ReplyLead))
             .Wait(2f).Capture("n_leading")
             .Step(a => WaitFor(a, () => D.CurrentPhase == PrinceDirector.Phase.AtSpot, 40f, "not at the plot"))
             .Wait(1.5f).Capture("n_at_plot")
             .Step(a => CastPitched(a))
             .Wait(0.3f).Capture("n_cast")
             // Skip ahead with the F1 tools until the merchant pitches a road.
             .Step(a => MerchantRoad(a))
             .Wait(0.5f).Capture("n_merchant")
             .Log("[AutoPlay] prince_captures OK")
             .Quit(0);
            return s;
        }

        private static IEnumerator MerchantRoad(AutoPlay a)
        {
            float end = Time.realtimeSinceStartup + 240f;
            Time.timeScale = 4f; B.Clock.MaxTicksPerFrame = 20;
            while (Time.realtimeSinceStartup < end)
            {
                var top = FactionApi.Top(S, PlayerIds.Human);
                if (top != null && top.Speaker == Speakers.Merchant && D.ActiveSpeaker == D.Merchant && D.AskHandle != 0 && SpeechBubbles.IsShowing(D.AskHandle))
                { Time.timeScale = 1f; yield break; }
                // Cast what is not a road from the F1 tools so the brain gets to a road.
                if (top != null && top.Kind != ProposalKind.Road && D.FirstSceneStep >= PrinceDirector.SceneDone) DevTools.Cast(top.Id);
                yield return a.DoWait(0.5f);
            }
            Time.timeScale = 1f;
            a.Fail("the merchant never pitched a road");
        }
    }
}
