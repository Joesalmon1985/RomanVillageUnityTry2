using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim.Encounters.Ward;

namespace World.Game
{
    /// <summary>
    /// The Ward duel's first-time tutorial (id <c>duel</c>), told by the referee: build with the essence stones, take one orb back,
    /// lock the secret Ward and cast a weave with the palm (preview, then confirm), read the beads and the tally stick, and leave by
    /// the gate. It runs in the world-first duel (practice and friendly; a lethal duel keeps its own stakes card and gets none).
    /// Nothing here reveals where the hidden Ward is: it only points at the same objects the player already sees.
    /// </summary>
    public static class DuelTutorial
    {
        public const string Id = "duel";
        private static WardDuelEncounter E => WardDuelEncounter.Current;

        private static bool InDuel()
        {
            var e = E;
            return e != null && !e.IsFinished && e.Props != null && e.WorldFirst && !e.IsLethal && e.Phase != WardDuelEncounter.Screen.Stakes;
        }

        private static Rect? Hit(DuelTarget t, int i = 0) { var e = E; return e != null && e.Props != null ? e.Props.HitRectOf(t, i) : null; }

        private static Rect? Stones()
        {
            var e = E; if (e == null || e.Props == null) return null;
            Rect? u = null;
            for (int i = 0; i < e.Props.StoneCount; i++) u = TutorialTargets.Union(u, Hit(DuelTarget.Stone, i));
            return u;
        }

        private static Rect? RefereeRect()
        {
            var e = E; if (e == null || e.Props == null) return null;
            var p = e.Props.ScreenPointOf(DuelTarget.Referee);
            return p.HasValue ? TutorialTargets.OfScreenPx(p.Value, Screen.height * 0.1f) : (Rect?)null;
        }

        public static Tutorial Create()
        {
            var t = new Tutorial { Id = Id, Title = "The Ward duel" };
            t.GameActive = () => InDuel() || (t.Flag("exited") && t.Steps.Count > 0 && t.Index >= t.Steps.Count - 1);
            t.CanStart = InDuel;
            t.OnBegin = tt => tt.Counters["b"] = E != null ? E.Building.Count : 0;
            t.OnUpdate = tt =>
            {
                var e = E;
                if (e != null && !e.IsFinished) tt.Set("in-duel");
                else if (tt.Flag("in-duel")) tt.Set("exited");
                if (e == null) return;
                int n = e.Building.Count;
                if (n < tt.Count("b")) tt.Set("took-back");
                tt.Counters["b"] = n;
            };
            t.BuildSteps = BuildSteps;
            t.Outro = "That is the duel: build, take back, lock your secret Ward, cast and read what comes back. The ring is yours whenever you want it.";
            t.OutroTutorName = "The referee";
            t.OutroTutorRect = RefereeRect;
            return t;
        }

        private static TutorialStep Step(string id, string line, Func<Rect?> target, Func<bool> done, float min = 1.2f) =>
            new TutorialStep { Id = id, TutorName = "The referee", TutorRect = RefereeRect, Line = line, Target = target, Done = done, MinSeconds = min };

        private static List<TutorialStep> BuildSteps(Tutorial t)
        {
            var stones = Step("stones", "Each stone is an essence. Tap stones to fill your hand: three make a secret Ward, and later a weave to cast.",
                Stones, () => { var e = E; return e != null && (e.Building.Count >= e.Tuning.Loci || e.Phase != WardDuelEncounter.Screen.Setup); });
            var back = Step("take-back", "Changed your mind? Tap an orb in your hand to take it back.",
                () => Hit(DuelTarget.Socket, 0), () => t.Flag("took-back") || (E != null && E.Phase != WardDuelEncounter.Screen.Setup));
            var lockStep = Step("lock", "Fill the hand again, then tap the palm. A card asks to lock this secret Ward; tap the palm again to lock it.",
                () => Hit(DuelTarget.Hand), () => E != null && E.Phase != WardDuelEncounter.Screen.Setup);
            var cast = Step("cast", "Now the duel. Build a weave of three stones the same way, tap the palm to preview the cast, and tap again to cast it.",
                () => Hit(DuelTarget.Hand), () => { var e = E; return e != null && e.Session != null && (e.Session.PlayerHistory.Count >= 1 || e.Phase == WardDuelEncounter.Screen.Result); });
            var beads = Step("beads", "The beads on the post are the casts you have left. A lit bead is still yours. Tap your beads to count them.",
                () => Hit(DuelTarget.BeadsCaster), () => t.SawInspect("beads"));
            var tally = Step("tally", "The tally stick keeps what each cast showed: pegs for fracture, echo and fade, always sorted. Tap it to read them.",
                () => Hit(DuelTarget.TallyCaster), () => t.SawInspect("tally stick"));
            var gate = Step("gate", "When you have seen enough, tap the gate once to preview and again to leave.",
                () => Hit(DuelTarget.Gate), () => t.Flag("exited"));
            return new List<TutorialStep> { stones, back, lockStep, cast, beads, tally, gate };
        }
    }
}
