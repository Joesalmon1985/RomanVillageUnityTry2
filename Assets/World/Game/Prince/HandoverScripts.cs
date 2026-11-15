using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using World.Sim;
using World.Sim.Eras;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// AutoPlay <c>handover</c> (H1-4): from a normal night start the prompt player plays to the handover, hears the
    /// prince out, declines the invitation once, watches one build he announces and does by himself (no cast), gives
    /// one piece of advice ("Do this first") and sees it taken, accepts "Show me" when the invitation comes back, sees
    /// the beat at the fire and Mira's approach begin. Logs the game minute of each. Option: <c>-timescale</c> (3).
    /// </summary>
    public static partial class PrinceScripts
    {
        public static AutoPlayScript Handover()
        {
            var s = new AutoPlayScript("handover");
            var t = new List<string>();
            float tHand = 0, tHeard = 0;
            int annBefore = 0;
            string M(float sec) => (sec / 60f).ToString("0.0", CultureInfo.InvariantCulture);
            void Mark(string what) { t.Add(what + " " + M(Now)); Debug.Log("[AutoPlay] handover: " + what + " at game minute " + M(Now)); }

            s.Step(a => Do(() => ApplyTimeScale(6f)))
             .Step(a => FollowRoutine(a, "handover", true, () => D.AfterHandover))
             .Step(a => Do(() => { tHand = Now; Mark("handover (the prince is autonomous)"); if (DayNight.Instance != null) DayNight.Instance.SetHour(22.5); }))
             .Step(a => WaitFor(a, () => D.HandoverStage >= 2, 60f, "the prince did not come to say it"))
             .Step(a => a.DoFace(() => D.Prince.transform.position))
             .Wait(0.8f).Capture("h14_handover")
             .Step(a => WaitFor(a, () => D.HandoverSceneDone, 40f, "the handover lines did not finish"))
             .Step(a => Do(() =>
             {
                 tHeard = Now; Mark("heard him out");
                 foreach (var l in PrinceLines.HandoverLines) Expect(a, D.Said.Any(x => x.Text == l), "missing line: " + l);
             }))
             // The invitation comes first: "Not yet" once.
             .Step(a => WaitFor(a, () => D.InviteOpen && SpeechBubbles.IsShowing(D.AskHandle), 40f, "no invitation"))
             .Step(a => a.DoFace(() => D.Prince.transform.position))
             .Wait(0.5f).Capture("h14_invitation")
             .Step(a => TapReply(a, () => D.AskHandle, PrinceLines.NotYet))
             .Step(a => Do(() => { annBefore = D.Announcements.Count; Mark("declined the invitation (Not yet)"); }))
             // One autonomous build with no cast.
             .Step(a => WaitFor(a, () => D.Announcements.Count > annBefore && D.WalkingToAnnounced, 120f, "no announcement"))
             .Step(a => a.DoFace(() => D.Prince.transform.position))
             .Wait(0.6f).Capture("h14_announce")
             .Step(a => Do(() =>
             {
                 var an = D.Announcements[D.Announcements.Count - 1];
                 Mark("announced: " + an.Text);
                 Expect(a, an.Text.Length > 0 && an.Text.Length < 90, "announcement not one short line");
             }))
             .Step(a => WaitFor(a, () => D.Acts.Any(x => x.Gesture && x.ProposalId == D.Announcements[D.Announcements.Count - 1].ProposalId), 60f, "he did not act by himself"))
             .Step(a => Do(() =>
             {
                 var act = D.Acts.First(x => x.Gesture && x.ProposalId == D.Announcements[D.Announcements.Count - 1].ProposalId);
                 Mark("he built by himself (proposal " + act.ProposalId + ", no cast by the player)");
                 Expect(a, !D.Acts.Any(x => x.ByPlayer), "the player cast something");
             }))
             // Advice: wait for the next announcement with time left, tap him, ask what he is doing, then "Do this first".
             .Step(a => WaitFor(a, () => D.Announcements.Count > annBefore + 1 && (FactionApi.Announced(S, PlayerIds.Human) != null && S.Factions.Factions[0].AnnounceExecuteTick - S.Tick > 80), 120f, "no second announcement"))
             .Step(a => Do(() => ApplyTimeScale(2f)))
             .Step(a => TapPrince(a))
             .Wait(0.3f).Capture("h14_advice_menu")
             .Step(a => TapReply(a, () => D.AskHandle, PrinceLines.WhatDoing))
             .Step(a => WaitFor(a, () => !string.IsNullOrEmpty(D.LastExplanation), 5f, "no explanation"))
             .Step(a => Do(() =>
             {
                 Mark("asked what he is doing: " + D.LastExplanation);
                 Expect(a, !string.IsNullOrEmpty(D.LastExplanation), "no explanation");
             }))
             .Wait(0.5f).Capture("h14_explain")
             .Step(a => TapPrince(a))
             .Step(a => TapReply(a, () => D.AskHandle, PrinceLines.DoFirst))
             .Step(a => Do(() =>
             {
                 if (D.AdviceSent.Count == 0)
                 {
                     var said = string.Join(" / ", D.Said.Skip(Math.Max(0, D.Said.Count - 4)).Select(x => x.Text));
                     a.Fail("no advice sent: menu " + D.MenuOpen + " ask " + D.AskHandle + " top " + (FactionApi.Top(S, 0) != null) + " said " + said);
                 }
             }))
             .Step(a => Do(() => Mark("advice: " + D.AdviceSent[0].Kind + " on proposal " + D.AdviceSent[0].ProposalId)))
             .Step(a => WaitFor(a, () => D.Acts.Count(x => x.Gesture) >= 2 || D.Said.Any(x => x.Text == "Then that goes first."), 20f, "the advice had no visible effect"))
             .Step(a => Do(() =>
             {
                 var fav = D.AdviceSent[0].ProposalId;
                 var acts = D.Acts.Where(x => x.ProposalId == fav).ToList();
                 Mark("advice took effect (" + (acts.Count > 0 ? "he did it at once, tick " + acts[0].Tick : "he answered and re-planned") + ")");
             }))
             // Accept the invitation when it comes back.
             .Step(a => Do(() => ApplyTimeScale(6f)))
             .Step(a => WaitFor(a, () => D.InviteOpen && SpeechBubbles.IsShowing(D.AskHandle), 330f, "the invitation did not come back"))
             .Step(a => Do(() => Mark("invitation again (declined " + D.InvitationsDeclined + " times)")))
             .Step(a => TapReply(a, () => D.AskHandle, PrinceLines.ShowMe))
             .Step(a => WaitFor(a, () => AgesApi.Current(S) == Era.Historic, 10f, "the chapter did not advance"))
             .Step(a => Do(() =>
             {
                 Mark("Show me: the Historic age begins");
                 Expect(a, AgeWords.Setback(S), "wording is still the Prehistoric one");
                 Expect(a, !AgeWords.Headline(S).Contains("they fall"), "the map still says they fall");
             }))
             .Wait(1.5f).Capture("h14_step")
             .Step(a => WaitFor(a, () => S.Campaign != null, 30f, "Mira's campaign did not start"))
             .Step(a => WaitFor(a, () => ExpeditionDirector.Instance != null && ExpeditionDirector.Instance.SceneStep >= ExpeditionDirector.SceneOpening, 30f, "Mira did not begin her approach"))
             .Step(a => Do(() => Mark("Mira's opening begins")))
             .Wait(2f).Capture("h14_mira")
             .Step(a => Do(() =>
             {
                 Debug.Log("[AutoPlay] handover timings (game minutes): " + string.Join("; ", t));
                 Time.timeScale = 1f;
             }))
             .Log("[AutoPlay] handover OK")
             .Quit(0);
            return s;
        }

        private static IEnumerator TapPrince(AutoPlay a)
        {
            if (D.AskHandle != 0) { SpeechBubbles.Close(D.AskHandle); yield return null; }
            // The player walks up to him (he may be out at an announced spot) and taps him.
            if (BoardSpace.Flat(D.Prince.transform.position - W.Position) > 9f)
            {
                W.StopAuto();
                yield return a.DoWalkTo(() => D.Prince.transform.position, 6f, 40f);
                if (a.Failed) yield break;
            }
            yield return a.DoFace(() => D.Prince.transform.position);
            PointerInput.TapObject(D.Prince);
            yield return WaitFor(a, () => D.MenuOpen && SpeechBubbles.IsShowing(D.AskHandle), 6f, "the prince did not offer replies");
        }
    }
}
