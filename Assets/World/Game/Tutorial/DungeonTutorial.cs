using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The crystal dungeon's first-time tutorial (id <c>dungeon</c>), told by Mira once the player is underground: step and turn with
    /// the footprint stones, tap a marker so Mira offers to act and confirm it, tap a companion, open the journal, and "Lead me out".
    /// The sim keeps its own rules (leads, acts, the party); the tutorial only reads the walker, the world actions and the
    /// interior state. If Mira has no errand at any marker nearby, the marker step says so and waits.
    /// </summary>
    public static class DungeonTutorial
    {
        public const string Id = "dungeon";
        private static ExpeditionDirector X => ExpeditionDirector.Instance;

        private static bool Underground => X != null && Interiors.Current != null && PlayerWalker.Instance != null
            && !(EncounterHost.Instance != null && EncounterHost.Instance.IsRunning);

        /// <summary>The nearest marker that has an errand for Mira (the player can ask her to act there), or null.</summary>
        public static MiraActThing NearestErrand()
        {
            var x = X; var w = PlayerWalker.Instance;
            if (x == null || w == null) return null;
            MiraActThing best = null; float bd = float.MaxValue;
            foreach (var m in UnityEngine.Object.FindObjectsByType<MiraActThing>(FindObjectsSortMode.None))
            {
                if (m == null || m.Thing == null || !m.gameObject.activeInHierarchy || x.ActFor(m.Thing.Ref) == null) continue;
                float d = BoardSpace.Flat(m.transform.position - w.Position);
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        /// <summary>The nearest living companion standing in the party, or null.</summary>
        public static Npc NearestCompanion()
        {
            var x = X; var w = PlayerWalker.Instance;
            if (x == null || w == null) return null;
            Npc best = null; float bd = float.MaxValue;
            foreach (var m in x.Mates)
            {
                if (m == null || !m.Alive || m.Out || m.Npc == null || !m.Npc.gameObject.activeInHierarchy) continue;
                float d = BoardSpace.Flat(m.Npc.transform.position - w.Position);
                if (d < bd) { bd = d; best = m.Npc; }
            }
            return best;
        }

        public static Tutorial Create()
        {
            var t = new Tutorial { Id = Id, Title = "The crystal dungeon" };
            t.GameActive = () => Underground || (t.Flag("lead-out") && t.Steps.Count > 0 && t.Index >= t.Steps.Count - 1);
            t.CanStart = () => Underground;
            t.OnBegin = tt =>
            {
                var w = PlayerWalker.Instance;
                tt.Counters["s0"] = w != null ? w.StepCount : 0;
                tt.Counters["h0"] = w != null ? w.Heading : 0;
            };
            t.OnUpdate = tt =>
            {
                var w = PlayerWalker.Instance;
                if (w != null && w.Heading != tt.Count("h0")) tt.Set("turned");
                var x = X;
                if (x != null && x.Journal != null && MapScreen.IsOpen && x.Journal.Expanded) tt.Set("journal");
                var id = InteriorDirector.Instance;
                if (id != null && id.LeadingOut) tt.Set("lead-out");
            };
            t.OnAction = (tt, m) =>
            {
                if (m.Current is MiraActThing)
                {
                    if (m.State == WorldActionState.Preview) tt.Set("act-preview");
                    if (m.State == WorldActionState.Confirm && m.LastCommitOk) { tt.Set("act-preview"); tt.Set("act-confirm"); }
                }
                else if (m.Current is CompanionCard)
                {
                    if (m.State == WorldActionState.Preview) tt.Set("mate");
                    if (m.State == WorldActionState.Confirm && m.LastCommitOk) { tt.Set("mate"); tt.Set("lead-out"); }
                }
            };
            t.BuildSteps = BuildSteps;
            t.Outro = "That is the way of it: step and turn, ask me to act, know the party, keep the journal. Now let us see the sky again.";
            t.OutroTutorName = "Mira";
            t.OutroSpeaker = TutorialTargets.Npc(NpcIds.Mira);
            return t;
        }

        private static TutorialStep Step(string id, string line, Func<Rect?> target, Func<bool> done, float min = 1.2f) =>
            new TutorialStep { Id = id, TutorName = "Mira", Speaker = TutorialTargets.Npc(NpcIds.Mira), Line = line, Target = target, Done = done, MinSeconds = min };

        private static Rect? Stone(int i)
        {
            var f = FootprintStones.Instance;
            return f != null && f.Visible && i >= 0 && i < f.Stones.Length ? f.Stones[i] : (Rect?)null;
        }

        private static List<TutorialStep> BuildSteps(Tutorial t)
        {
            var step = Step("step", "Down here you move one step at a time. Tap the forward footprint stone, or press W or Up, to take a step.",
                () => Stone(1), () => PlayerWalker.Instance != null && PlayerWalker.Instance.StepCount > t.Count("s0"));
            var turn = Step("turn", "The curved stones turn you left or right on the spot. Tap one, or press A or D, to look around.",
                () => TutorialTargets.Union(Stone(0), Stone(3)), () => t.Flag("turned"));
            var marker = Step("marker", null,
                () => { var m = NearestErrand(); return m != null ? TutorialTargets.Of(m) : null; },
                () => t.Flag("act-preview"));
            marker.LineFn = () => NearestErrand() != null
                ? "Something here is Mira's errand. Tap the ringed marker once and I will offer to act on it."
                : "I have no errand at anything close by. Walk on, and when a marker is mine to read, take, or restore, tap it.";
            var confirm = Step("act-confirm", "Tap it again and I will do it. Only the second tap sends me.",
                () => { var m = NearestErrand(); return m != null ? TutorialTargets.Of(m) : null; },
                () => t.Flag("act-confirm"));
            var mate = Step("companion", "These are my companions. Tap one to read who they are and what they do for the party. Tap elsewhere to close the card.",
                () => { var n = NearestCompanion(); return n != null ? TutorialTargets.Of(n) : null; }, () => t.Flag("mate"));
            var journal = Step("journal", "I keep a journal on my hip. Tap the book to read what we know and where to look next, then close it.",
                () => X != null && X.Book != null ? TutorialTargets.Of(X.Book) : null, () => t.Flag("journal"));
            var lead = Step("lead-out", "When you want the sky again, ask a companion to lead you out: tap them, then tap again. You come back to where you set out.",
                () => { var n = NearestCompanion(); return n != null ? TutorialTargets.Of(n) : null; }, () => t.Flag("lead-out"));
            return new List<TutorialStep> { step, turn, marker, confirm, mate, journal, lead };
        }
    }
}
