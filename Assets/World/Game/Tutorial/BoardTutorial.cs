using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The hex board's first-turn tutorial (id <c>board</c>): sacrifice at the altar, place the setup camp and road (ghost, then
    /// confirm), read the baskets and the build costs, build or trade, end the turn with the hourglass, leave by the lamb stone.
    /// A 7 (the discard sack) and an envoy's offer are tips said when they happen. The tutor is the temple keeper at the altar and the prince
    /// on the ledge once the board is open. Conditions come from the sim's events and the table state; nothing here sends
    /// a command and the AI seats keep playing.
    /// </summary>
    public static class BoardTutorial
    {
        public const string Id = "board";
        private const float NearAltarMetres = 24f;
        private static GodBoardDirector D => GodBoardDirector.Instance;

        private static bool NearAltar()
        {
            var d = D; var w = PlayerWalker.Instance;
            if (d == null || d.OwnTemple == null || w == null) return false;
            return BoardSpace.Flat(d.OwnTemple.transform.position - w.Position) <= NearAltarMetres;
        }

        private static int Own(int[] arr, int me) { int n = 0; if (arr != null) foreach (int v in arr) if (v == me) n++; return n; }

        public static Tutorial Create()
        {
            var t = new Tutorial { Id = Id, Title = "The god board" };
            t.GameActive = () =>
            {
                var d = D; if (d == null || d.T == null || d.GameOver) return false;
                if (d.IsOpen || d.View == BoardView.Falling) return true;
                // At the altar the tutorial speaks only for its first step (sacrifice) and after the last (come back).
                return NearAltar() && (t.Index == 0 || t.Index >= t.Steps.Count - 1);
            };
            t.CanStart = () => D != null && D.T != null && !D.GameOver && (D.IsOpen || NearAltar());
            t.OnSim = OnSim;
            t.BuildSteps = BuildSteps;
            t.BuildTips = BuildTips;
            t.Outro = "That is a whole turn. Build, trade, tip the hourglass, and watch the island pay. Tap the altar to come back any time.";
            t.OutroTutorName = "The temple keeper";
            t.OutroSpeaker = TutorialTargets.Npc("temple_keeper");
            return t;
        }

        private static void OnSim(Tutorial t, SimEvent e)
        {
            var d = D; if (d == null || d.T == null) return;
            int me = d.Human;
            switch (e)
            {
                case Built b when b.People == me:
                    if (b.Setup && b.Kind == "settlement") t.Set("setup-settlement");
                    else if (b.Setup && b.Kind == "road") t.Set("setup-road");
                    else if (!b.Setup) t.Set("built");
                    break;
                case Traded tr when tr.People == me: t.Set("traded"); break;
                case TurnEnded te when te.People == me && d.T.Phase == TablePhase.Main && t.Flag("setup-road"): t.Set("turn-ended"); break;
                case TablePaused _: t.Set("left"); break;
                case TableResumed _: t.Flags.Remove("left"); break;
                case Epidemic _: t.Set("epidemic"); break;
            }
        }

        private static TutorialStep Step(string id, bool ledgeTutor, string line, Func<Rect?> target, Func<bool> done, float min = 1.2f)
        {
            var s = new TutorialStep { Id = id, TutorName = ledgeTutor ? "The prince" : "The temple keeper", Line = line, Target = target, Done = done, MinSeconds = min };
            if (ledgeTutor) s.TutorRect = () => TutorialTargets.Ledge("prince");
            else s.Speaker = TutorialTargets.Npc("temple_keeper"); // at the altar the keeper speaks; on the board the prince on the ledge does
            return s;
        }

        private static Rect? Baskets() => TutorialTargets.Union(TutorialTargets.Ledge("basket:0"), TutorialTargets.Ledge("basket:1"), TutorialTargets.Ledge("basket:2"), TutorialTargets.Ledge("basket:3"), TutorialTargets.Ledge("basket:4"));
        private static Rect? Bench() => TutorialTargets.Union(TutorialTargets.Ledge("bench:0"), TutorialTargets.Ledge("bench:1"), TutorialTargets.Ledge("bench:2"));
        private static Rect? Altar()
        {
            var d = D; if (d == null || d.OwnTemple == null) return null;
            return TutorialTargets.OfPoint(d.OwnTemple.AltarSpot.position, Screen.height * 0.18f);
        }
        private static Rect? Corner() { var d = D; return d != null && d.AdviseCorner >= 0 ? TutorialTargets.OfScreenPx(d.ScreenOfCorner(d.AdviseCorner)) : null; }
        private static Rect? Edge() { var d = D; return d != null && d.EdgeSpots.Count > 0 ? TutorialTargets.OfScreenPx(d.ScreenOfEdge(d.EdgeSpots[0])) : null; }

        private static bool Settled() { var d = D; return d != null && d.T != null && (d.T.Phase == TablePhase.Main || d.T.Phase == TablePhase.Discard || (d.T.Phase == TablePhase.Setup && Own(d.T.CornerOwner, d.Human) >= 2)); }
        private static bool Pathed() { var d = D; return d != null && d.T != null && (d.T.Phase == TablePhase.Main || d.T.Phase == TablePhase.Discard || (d.T.Phase == TablePhase.Setup && Own(d.T.RoadOwner, d.Human) >= 1)); }
        private static bool Ghost() { var d = D; return d != null && d.Pieces != null && d.Pieces.HasGhost; }

        private static List<TutorialStep> BuildSteps(Tutorial t)
        {
            return new List<TutorialStep>
            {
                Step("sacrifice", false, "Welcome to the temple. Tap the altar to offer a lamb, then tap it again. The god board opens above the island.", Altar,
                    () => D != null && (D.IsOpen || (D.T != null && D.T.Phase != TablePhase.NotOpened))),
                Step("settle-preview", true, "Place your first camp. Tap a glowing spot once: a ghost camp shows where it would stand.", Corner,
                    () => t.Flag("setup-settlement") || Settled() || Ghost()),
                Step("settle-confirm", true, "Happy with it? Tap the same spot again to build the camp.", Corner,
                    () => t.Flag("setup-settlement") || Settled()),
                Step("road-preview", true, "Now a road out of your camp. Tap a glowing edge to see the ghost road.", Edge,
                    () => t.Flag("setup-road") || Pathed() || (t.Flag("setup-settlement") && Ghost())),
                Step("road-confirm", true, "Tap the same edge again to lay the road.", Edge,
                    () => t.Flag("setup-road") || Pathed()),
                Step("baskets", true, "These baskets are your hand: logs, bricks, sheaves, fleece and stones. Tap one to read exactly what you hold.", Baskets,
                    () => t.SawInspect("Offering baskets")),
                Step("costs", true, "The builder's bench shows what a road, a camp and a city cost. Tap it to read the costs.", Bench,
                    () => t.SawInspect("bench")),
                Step("wait-turn", true, "The other peoples place their camps now. When the sand runs in the hourglass, it is your turn.", () => TutorialTargets.Ledge("hourglass"),
                    () => D != null && D.Model.CanAct, 2f),
                Step("act", true, "On your turn, build with what you hold, or tap the trader to swap four of one good for one you need.", () => TutorialTargets.Ledge("trader"),
                    () => t.Flag("built") || t.Flag("traded") || t.Flag("turn-ended")),
                Step("end-turn", true, "Finished? Tap the hourglass: it tips. Tap it again to end your turn.", () => TutorialTargets.Ledge("hourglass"),
                    () => t.Flag("turn-ended") || t.DidCommit("hourglass")),
                Step("leave", true, "To go home, tap the lamb stone twice. The table waits and the sand freezes until you sacrifice again.", () => TutorialTargets.Ledge("lamb"),
                    () => t.Flag("left") || (D != null && !D.IsOpen)),
            };
        }

        private static List<TutorialTip> BuildTips(Tutorial t)
        {
            return new List<TutorialTip>
            {
                new TutorialTip
                {
                    Id = "discard", TutorName = "The prince", TutorRect = () => TutorialTargets.Ledge("prince"),
                    Line = "The gods are angry, the board's seven. You hold more than seven goods, so half must go in the sack. Tap baskets to fill it, then tap the sack.",
                    Target = () => TutorialTargets.Ledge("sack"),
                    Trigger = () => D != null && D.IsOpen && D.Model.MustDiscard,
                    Resolved = () => D == null || !D.Model.MustDiscard, Seconds = 40f,
                },
                new TutorialTip
                {
                    Id = "seven", TutorName = "The prince", TutorRect = () => TutorialTargets.Ledge("prince"),
                    Line = "The gods are angry: that is the board's seven. Sickness runs through the camps. The healer's lodge shows how bad it is.",
                    Target = () => TutorialTargets.Ledge("totem"),
                    Trigger = () => t.Flag("epidemic") && D != null && D.IsOpen && !D.Model.MustDiscard,
                    Resolved = () => false, Seconds = 9f,
                },
                new TutorialTip
                {
                    Id = "envoy", TutorName = "The prince", TutorRect = () => TutorialTargets.Ledge("prince"),
                    Line = "An envoy brings an offer. Tap them to read it, then accept, or wave them away with the raised palm.",
                    Target = () => { var d = D; if (d == null || d.Model.Incoming.Count == 0) return null; return TutorialTargets.Ledge("envoy:" + d.Model.Incoming[0].Id); },
                    Trigger = () => D != null && D.IsOpen && D.Model.Incoming.Count > 0,
                    Resolved = () => D == null || D.Model.Incoming.Count == 0, Seconds = 14f,
                },
            };
        }
    }
}
