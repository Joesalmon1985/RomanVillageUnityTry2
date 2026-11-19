using System;
using System.Collections;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// AutoPlay scripts of the council, driven through real pointer taps on the props (the encounter's own tap path):
    /// <c>council_fixture</c> stages the brief's example trays and commits one tray; <c>council_full</c> accepts the
    /// candidate's request in the normal game and plays a complete election against three AI candidates (a bank, a
    /// bust, a successful listening burst, spending the petition, a winner taking office); <c>council_decline</c>
    /// declines and lets the four play on their own. Each logs "[AutoPlay] NAME OK".
    /// </summary>
    public static class CouncilScripts
    {
        public const string Fixture = "council_fixture", Full = "council_full", Decline = "council_decline";
        static WorldBootstrap B => WorldBootstrap.Instance;
        static CouncilEncounter Enc => CouncilEncounter.Current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            AutoPlayScripts.Register(Fixture, CreateFixture);
            AutoPlayScripts.Register(Full, CreateFull);
            AutoPlayScripts.Register(Decline, CreateDecline);
        }

        static int Home => CouncilDirector.Instance != null ? CouncilDirector.Instance.HomeSettlement : 0;
        static SettlementCouncil Sc => CouncilApi.Get(B.Sim.State, Home);

        // ------------------------------------------------------------------ council_fixture

        public static AutoPlayScript CreateFixture()
        {
            var s = new AutoPlayScript(Fixture);
            s.Wait(1f)
             .Call(() => { CouncilDirector.Disabled = true; CouncilFixture.StageExample(B.Sim.State, Home, 0); })
             .Call(() => EncounterDirectory.TryStart(CouncilEncounter.Id, null, Home))
             .Wait(1.6f)
             .Check(() => Enc != null && Enc.View == CouncilEncounter.ViewKind.Forward, "the council did not open")
             .Check(() => Sc.Election.Pending.Count == 3, "three example trays were expected")
             .Check(() => CouncilScene.Current.Proposals.Count == 3, "the three example proposals are not held out as notes")
             .Capture("council_fixture_forward")
             .Call(() => Enc.TapTarget("note " + TrayWith(5, 8)))
             .Wait(2.2f)
             .Check(() => Sc.Election.Temp[5 - 2] > 0 && Sc.Election.Temp[8 - 2] > 0, "tapping the 5+8 tray did not move both catspaws")
             .Check(() => CouncilScene.Current.ViewMismatch(Sc) == null, "the view does not match the state")
             .Capture("council_fixture_moved")
             .Call(() => Enc.Return())
             .Wait(0.5f)
             .Check(() => !EncounterHost.Instance.IsRunning, "the council is still open")
             .Log("[AutoPlay] " + Fixture + " OK")
             .Quit(0);
            return s;
        }

        static int TrayWith(int a, int b)
        {
            var p = CouncilScene.Current.Proposals;
            for (int i = 0; i < p.Count; i++) if (p[i].Touches(a) && p[i].Touches(b)) return i;
            return 0;
        }

        // ------------------------------------------------------------------ council_decline

        public static AutoPlayScript CreateDecline()
        {
            var s = new AutoPlayScript(Decline);
            s.Wait(1f)
             .Call(PrepareVillage)
             .Step(a => WaitFor(a, () => CouncilDirector.Instance.Asker != null && Asking(), 60f, "the candidate did not come and ask"))
             .Capture("council_approach")
             .Wait(0.5f)
             .PickReply(1)
             .Step(a => WaitFor(a, () => Sc != null && Sc.Status == CouncilStatus.InProgress, 10f, "declining did not start the election"))
             .Check(() => !Sc.PlayerHelps && Sc.Election.PlayerSeat < 0, "declined election should have no player seat")
             .Step(a => WaitFor(a, () => Sc.Status == CouncilStatus.Decided, 240f, "the four did not finish on their own"))
             .Check(() => CouncilApi.OfficeHolderOf(B.Sim.State, Home) != null, "no office holder recorded")
             .Call(() => Debug.Log("[AutoPlay] council_decline winner " + CouncilApi.OfficeHolderOf(B.Sim.State, Home).Name + ", background steps " + CouncilDirector.Instance.BackgroundSteps))
             .Log("[AutoPlay] " + Decline + " OK")
             .Quit(0);
            return s;
        }

        static void PrepareVillage()
        {
            CouncilDirector.Disabled = false; CouncilDirector.ApproachDelay = 0.5f; CouncilEncounter.AiDelay = 0.12f;
            CouncilFixture.MakeEligible(B.Sim.State, Home);
            // the prince keeps to himself while the council is played (his pitch bubbles would hold the candidate back)
            CouncilDirector.IgnorePrince = true;
            if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
            foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h);
        }

        static bool Asking()
        {
            foreach (int h in SpeechBubbles.OpenHandles()) if (SpeechBubbles.RepliesOf(h) != null && SpeechBubbles.RepliesOf(h).Count > 0) return true;
            return false;
        }

        // ------------------------------------------------------------------ council_full

        public static AutoPlayScript CreateFull()
        {
            var s = new AutoPlayScript(Full);
            s.Wait(1f)
             .Call(PrepareVillage)
             .Step(a => WaitFor(a, () => CouncilDirector.Instance.Asker != null && Asking(), 60f, "the candidate did not come and ask"))
             .Wait(0.4f)
             .PickReply(2) // "What is it?"
             .Wait(0.6f)
             .Step(a => WaitFor(a, Asking, 10f, "the explanation did not come with replies"))
             .PickReply(0) // "Gladly"
             .Step(a => WaitFor(a, () => Enc != null && Enc.View == CouncilEncounter.ViewKind.Forward && !Enc.InTransition, 90f, "the walk to the gathering did not open the council"))
             .Wait(1.2f)
             .Capture("council_full_start")
             .Step(PlayElection)
             .Wait(1f)
             .Check(() => Sc.Status == CouncilStatus.Decided, "the election did not finish")
             .Check(() => CouncilApi.OfficeHolderOf(B.Sim.State, Home) != null, "no winner took office")
             .Log("[AutoPlay] " + Full + " OK")
             .Quit(0);
            return s;
        }

        sealed class Demo { public bool Banked, Bust, Listened, Spent, Handover; public int Sittings; }

        static IEnumerator PlayElection(AutoPlay a)
        {
            var d = new Demo();
            float end = Time.realtimeSinceStartup + 600f;
            bool listenedOnce = false;
            int advancesThisSitting = 0, lastSitting = -1;
            bool pressOnForBust = false;
            while (Time.realtimeSinceStartup < end)
            {
                var enc = Enc;
                if (enc == null) break;
                var sc = Sc; var e = sc.Election;
                if (e.Phase == ElectionPhase.Complete) { d.Handover = true; break; }
                if (e.Sittings != lastSitting) { lastSitting = e.Sittings; advancesThisSitting = 0; }
                d.Banked |= e.Banks > 0; d.Bust |= e.Busts > 0; d.Spent |= e.PetitionSpent; d.Listened |= e.PetitionEarned;
                bool mine = e.Active == e.PlayerSeat;
                if (!mine)
                {
                    // a rival's turn: the first time, turn left and listen (the same lantern tap a player makes)
                    if (!listenedOnce && !e.PetitionEarned && e.Phase != ElectionPhase.Complete && enc.View == CouncilEncounter.ViewKind.Forward && !enc.InTransition && Time.realtimeSinceStartup > 0f)
                    {
                        listenedOnce = true;
                        yield return Wait(0.8f);
                        enc.TapTarget("lantern");
                        yield return Wait(0.8f);
                        if (enc.View != CouncilEncounter.ViewKind.Left) { a.Fail("the lantern did not turn the view left"); yield break; }
                        Shot("council_full_bench");
                        yield return ListenBurst(a, enc);
                        if (Failed(a)) yield break;
                    }
                    yield return null; continue;
                }
                if (enc.View != CouncilEncounter.ViewKind.Forward || enc.InTransition) { yield return null; continue; }
                if (!CouncilScene.Current.Settled) { yield return null; continue; }
                yield return Wait(0.35f);
                e = Sc.Election;
                switch (e.Phase)
                {
                    case ElectionPhase.Ready:
                        if (e.HasAdvanced && ShouldBank(e, advancesThisSitting, pressOnForBust, d)) { BankTap(enc); }
                        else PressOn(enc);
                        break;
                    case ElectionPhase.Choosing:
                        {
                            // prefer the proposal that moves the most
                            var props = CouncilScene.Current.Proposals;
                            if (props.Count == 0) break;
                            int best = 0, bestScore = -1;
                            for (int i = 0; i < props.Count; i++)
                            {
                                int score = 0; foreach (var st in props[i].Steps) score += st.Value;
                                if (score > bestScore) { bestScore = score; best = i; }
                            }
                            enc.TapTarget("note " + best);
                            advancesThisSitting++;
                            break;
                        }
                    case ElectionPhase.BustPending:
                        if (e.PetitionAvailable) { enc.TapTarget("petition"); d.Spent = true; pressOnForBust = false; }
                        else enc.TapTarget("seal");
                        break;
                }
                // after the first bank and the burst, press on until a bust so that one is shown and the petition is spent
                if (e.Banks > 0 && e.PetitionEarned && !e.PetitionSpent) pressOnForBust = true;
                yield return Wait(0.3f);
            }
            var fin = Sc.Election;
            Debug.Log("[AutoPlay] council_full report: banks " + fin.Banks + ", busts " + fin.Busts + ", listening bursts " + fin.ListenBursts + " (petition earned " + fin.PetitionEarned + ", spent " + fin.PetitionSpent + "), pushes " + fin.Pushes + ", sittings " + fin.Sittings + ", winner seat " + fin.Winner);
            if (fin.Banks == 0) a.Fail("no bank was demonstrated");
            else if (fin.Busts == 0) a.Fail("no bust was demonstrated");
            else if (!fin.PetitionEarned) a.Fail("no petition was earned by listening");
            else if (!fin.PetitionSpent) a.Fail("the petition was not spent");
            else if (fin.Winner < 0) a.Fail("no winner");
            // let the handover play
            float t1 = Time.realtimeSinceStartup + 9f;
            while (Enc != null && Time.realtimeSinceStartup < t1) yield return null;
        }

        static int _alt;
        static int FirstPaw()
        {
            var e = Sc.Election;
            for (int t = 2; t <= 12; t++) if (e.Temp[t - 2] > 0 && CouncilScene.Current.Catspaw(t) != null) return t;
            return 0;
        }

        /// <summary>Press on by tapping a catspaw (every other time) or the candidate's beckoning hand.</summary>
        static void PressOn(CouncilEncounter enc)
        {
            int t = FirstPaw();
            if (t > 0 && (_alt++ % 2) == 1 && enc.TapTarget("paw " + t)) return;
            enc.TapTarget("hand");
        }

        /// <summary>Bank by tapping a lead who has listeners (every other time) or the candidate's seal.</summary>
        static void BankTap(CouncilEncounter enc)
        {
            int t = FirstPaw();
            if (t > 0 && (_alt++ % 2) == 1 && enc.TapTarget("lead " + t)) return;
            enc.TapTarget("seal");
        }

        static bool ShouldBank(ElectionState e, int advances, bool pressOnForBust, Demo d)
        {
            if (!d.Banked) return advances >= 1;       // the first sitting: bank at once, to show it
            if (pressOnForBust) return false;           // press on until the bust (spends the petition)
            return advances >= 2 || e.CatspawsLeft == 0;
        }

        /// <summary>A screenshot beside the other captures (Evidence/screenshots, git-ignored).</summary>
        public static void Shot(string name)
        {
            try
            {
                System.IO.Directory.CreateDirectory(AutoPlay.ScreenshotDir);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(AutoPlay.ScreenshotDir, name + ".png"));
            }
            catch (Exception e) { Debug.LogWarning("[Council] capture failed: " + e.Message); }
        }

        static bool Failed(AutoPlay a) => a.Failed;

        static IEnumerator Wait(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        static IEnumerator ListenBurst(AutoPlay a, CouncilEncounter enc)
        {
            float end = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < end)
            {
                var e = Sc.Election;
                if (enc.View != CouncilEncounter.ViewKind.Left) break;
                var ls = e.Listening;
                if (ls == null || ls.Phase != ListeningPhase.InProgress)
                {
                    if (enc.Listen == CouncilEncounter.ListenPhase.Done && ls != null && ls.Phase == ListeningPhase.Complete) { yield return Wait(0.5f); break; }
                    yield return null; continue;
                }
                if (!enc.ReadyForAnswer) { yield return null; continue; }
                yield return Wait(0.7f); // a quick, correct player
                var ls2 = Sc.Election.Listening;
                if (ls2 == null || ls2.Phase != ListeningPhase.InProgress || !enc.ReadyForAnswer) continue;
                bool same = ListeningSession.IsSame(ls2, ls2.Index);
                int idx = ls2.Index;
                enc.TapTarget(same ? "same" : "different");
                float w = Time.realtimeSinceStartup + 3f;
                while (Sc.Election.Listening != null && Sc.Election.Listening.Index == idx && Sc.Election.Listening.Phase == ListeningPhase.InProgress && Time.realtimeSinceStartup < w) yield return null;
            }
            var res = Sc.Election.Listening != null ? Sc.Election.Listening.Result : null;
            Debug.Log("[AutoPlay] council_full listening burst: " + (res != null ? res.Correct + " correct, effective " + res.EffectivePercent + "%, earned " + res.Earned : "unfinished"));
            // go back to the table; the aide carries the petition
            float t2 = Time.realtimeSinceStartup + 6f;
            while (enc.View == CouncilEncounter.ViewKind.Left && Time.realtimeSinceStartup < t2) yield return null;
            if (enc.View == CouncilEncounter.ViewKind.Left) { enc.TapTarget("rightlantern"); yield return Wait(0.8f); }
        }

        static IEnumerator WaitFor(AutoPlay a, Func<bool> cond, float timeout, string message)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) { a.Fail(message); yield break; }
                yield return null;
            }
        }
    }
}
