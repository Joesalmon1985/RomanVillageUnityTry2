using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// The council's first-time tutorial (id <c>council</c>), told by the recorder at the stone table: press on (the candidate's
    /// hand), preview a proposal and see both places light, commit it, read the tablet, push once more, bank with the seal, and what a
    /// bust is. It runs inside the council encounter, in the forward view; the listening game (the left view) pauses it.
    /// Conditions come from the election state and the council's events; nothing here sends a command.
    /// </summary>
    public static class CouncilTutorial
    {
        public const string Id = "council";
        private static CouncilEncounter Enc => CouncilEncounter.Current;

        private static ElectionState E => Enc != null ? Enc.E : null;
        private static bool Mine { get { var e = E; return e != null && e.PlayerSeat >= 0 && e.Active == e.PlayerSeat && e.Phase != ElectionPhase.Complete; } }

        /// <summary>The screen rect (GUI coordinates) of a named target of the council's forward view, or null.</summary>
        public static Rect? Target(string name)
        {
            var enc = Enc; var sc = enc != null ? enc.Scene : null; var cam = sc != null ? sc.ViewCamera : null;
            if (cam == null || enc.View != CouncilEncounter.ViewKind.Forward) return null;
            foreach (var t in sc.Targets(cam))
                if (t.Name == name)
                {
                    float k = Screen.height / (float)Mathf.Max(1, cam.pixelHeight);
                    return new Rect((t.Px.x - t.RadiusPx) * k, Screen.height - (t.Px.y + t.RadiusPx) * k, t.RadiusPx * 2 * k, t.RadiusPx * 2 * k);
                }
            return null;
        }

        public static Tutorial Create()
        {
            var t = new Tutorial { Id = Id, Title = "The council" };
            t.GameActive = () => { var enc = Enc; return enc != null && !enc.IsFinished && enc.E != null && !enc.Handover && enc.View == CouncilEncounter.ViewKind.Forward; };
            t.OnSim = OnSim;
            t.BuildSteps = BuildSteps;
            t.BuildTips = BuildTips;
            t.Outro = "That is a turn: press on, choose, read the tablet, and bank before the luck runs out. A bust loses only what you had not kept.";
            t.OutroTutorName = "The recorder";
            t.OutroTutorRect = () => Target("tablet");
            return t;
        }

        private static void OnSim(Tutorial t, SimEvent ev)
        {
            var e = E; if (e == null || !(ev is CouncilEvent ce)) return;
            bool mine = e.PlayerSeat >= 0 && (ce.Seat == e.PlayerSeat || (ce.Seat < 0 && e.Active == e.PlayerSeat));
            if (!mine) return;
            switch (ce)
            {
                case CouncilPushMade _: t.Bump("push"); break;
                case CouncilAdvanceMade _: t.Set("chose"); break;
                case CouncilBanked _: t.Set("banked"); break;
                case CouncilBust _: t.Set("bust"); break;
                case CouncilBustPending _: t.Set("bust-pending"); break;
            }
        }

        private static TutorialStep Step(string id, string line, Func<Rect?> target, Func<bool> done, float min = 1.2f) =>
            new TutorialStep { Id = id, TutorName = "The recorder", TutorRect = () => Target("tablet"), Line = line, Target = target, Done = done, MinSeconds = min };

        private static List<TutorialStep> BuildSteps(Tutorial t)
        {
            var press = Step("press", null, () => Target("hand"),
                () => { var e = E; return e != null && (e.Phase != ElectionPhase.Ready || e.HasAdvanced || t.Flag("chose") || t.Flag("banked") || t.Flag("bust")); });
            press.LineFn = () => Mine
                ? "Your candidate is the one at the front. Tap the candidate's hand to press on: the village puts some proposals on the table."
                : "The other candidates speak first. When it is your turn, tap your candidate's hand to press on.";

            var preview = Step("preview", "Each note is a proposal. Tap one once: ghosts show the places it would move, and the people there turn to listen.",
                () => Target("note 0") ?? Target("note 1"),
                () => t.DidPreview("proposal") || t.Flag("chose") || (E != null && E.HasAdvanced));
            var commit = Step("commit", "Tap the same note again to put it to the village.",
                () => Target("note 0") ?? Target("note 1"),
                () => t.Flag("chose") || (E != null && E.HasAdvanced));
            var tablet = Step("tablet", "The recorder's tablet keeps the exact standing. Tap it to read where each candidate stands.",
                () => Target("tablet"), () => t.SawInspect("recorder's tablet"));
            var again = Step("again", "You can press on once more for more ground, but a push that leaves nothing that fits is a bust. Tap the hand to try.",
                () => Target("hand"), () => t.Count("push") > t.Count("p0") || t.Flag("banked") || t.Flag("bust") || t.Flag("bust-pending"));
            again.OnEnter = () => t.Counters["p0"] = t.Count("push");
            var bank = Step("bank", null,
                () => (E != null && E.Phase == ElectionPhase.Choosing) ? Target("note 0") : (Target("recorder") ?? Target("seal")), () => t.Flag("banked") || t.Flag("bust"));
            bank.LineFn = () => E != null && E.Phase == ElectionPhase.Choosing
                ? "Choose a note again. Then you can settle what you have: tap the recorder's seal once to preview, again to keep it for good."
                : "Settle what you have: tap the recorder's seal once to preview, again to keep it for good. That ends your turn.";
            return new List<TutorialStep> { press, preview, commit, tablet, again, bank };
        }

        private static List<TutorialTip> BuildTips(Tutorial t)
        {
            return new List<TutorialTip>
            {
                new TutorialTip
                {
                    Id = "bust-pending", TutorName = "The recorder", TutorRect = () => Target("tablet"),
                    Line = "A bust: nothing fits. You would lose the progress you had not kept, never what you banked. Tap the seal to accept it, or the petition to try again.",
                    Target = () => Target("seal") ?? Target("petition"),
                    Trigger = () => t.Flag("bust-pending") && E != null && E.Phase == ElectionPhase.BustPending,
                    Resolved = () => E == null || E.Phase != ElectionPhase.BustPending, Seconds = 30f,
                },
                new TutorialTip
                {
                    Id = "bust", TutorName = "The recorder", TutorRect = () => Target("tablet"),
                    Line = "That was a bust: nothing fit, so the progress you had not kept is gone. What you banked in earlier turns stays yours.",
                    Target = () => Target("tablet"),
                    Trigger = () => t.Flag("bust"), Resolved = () => false, Seconds = 8f,
                },
            };
        }
    }
}
