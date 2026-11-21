using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The rhythm dance's first-time tutorial (id <c>rhythm</c>), told by Sol at the fire: hit the drums for a block (drum, strip or
    /// keys), watch the block's torch keep, pause on the log seat and resume, read Sol's tally, and tune with the bell between
    /// phrases. Runs inside the dance encounter; the music and the scoring are untouched.
    /// </summary>
    public static class RhythmTutorial
    {
        public const string Id = "rhythm";
        private static RhythmPartyEncounter R => RhythmPartyEncounter.Current;

        private static Camera Cam => Camera.main;
        private static Rect? Point(Func<DanceStageProps, Vector3> f, float side = 0f)
        {
            var r = R; if (r == null || r.Props == null) return null;
            return TutorialTargets.OfPoint(f(r.Props), Cam, side > 0 ? side : Screen.height * 0.14f);
        }

        private static Rect? Drums()
        {
            var r = R; if (r == null || r.Circle == null) return null;
            Rect? u = null;
            for (int p = 1; p <= 4; p++) u = TutorialTargets.Union(u, TutorialTargets.OfPoint(r.Circle.DrumTop(p), Cam, Screen.height * 0.12f));
            return u;
        }

        private static int CurrentTorch()
        {
            var r = R; if (r == null || r.Props == null) return 0;
            for (int i = 0; i < DanceStageProps.TorchCount; i++) if (r.Props.TorchStateOf(i) == DanceStageProps.TorchState.Current) return i;
            for (int i = 0; i < DanceStageProps.TorchCount; i++) if (r.Props.TorchStateOf(i) == DanceStageProps.TorchState.Unlit) return i;
            return 0;
        }

        public static Tutorial Create()
        {
            var t = new Tutorial { Id = Id, Title = "The dance" };
            t.GameActive = () => { var r = R; return r != null && !r.IsFinished && r.Props != null; };
            t.OnUpdate = tt =>
            {
                var r = R; if (r == null) return;
                if (r.OpenCard == RhythmPartyEncounter.Card.Tally) tt.Set("tally");
                if (!double.IsNaN(r.LastTuneMs)) tt.Set("tuned");
            };
            t.BuildSteps = BuildSteps;
            t.Outro = "That is the dance: strike, keep the torches lit, rest when you must, count with me, and tune the bell. Come back whenever you like.";
            t.OutroTutorName = "Sol";
            t.OutroTutorRect = () => Point(p => p.SolPoint);
            return t;
        }

        private static TutorialStep Step(string id, string line, Func<Rect?> target, Func<bool> done, float min = 1.2f) =>
            new TutorialStep { Id = id, TutorName = "Sol", TutorRect = () => Point(p => p.SolPoint), Line = line, Target = target, Done = done, MinSeconds = min };

        private static List<TutorialStep> BuildSteps(Tutorial t)
        {
            var hit = Step("hit", "Four drums, four pads. When an ember reaches a drum, strike it: tap the drum, the strip below it, or press D F J K.",
                Drums, () => { var r = R; return r != null && r.LivePerfect + r.LiveGreat + r.LiveOk > 0; });
            var torch = Step("torch", "Each torch is one block of the song. Play a block well and its torch burns gold; miss it and it dims to an ember.",
                () => Point(p => p.TorchPoint(CurrentTorch())),
                () => { var r = R; if (r == null || r.Props == null) return false; for (int i = 0; i < DanceStageProps.TorchCount; i++) { var s = r.Props.TorchStateOf(i); if (s == DanceStageProps.TorchState.Kept || s == DanceStageProps.TorchState.Missed) return true; } return false; }, 2f);
            var pause = Step("pause", null, () => Point(p => p.SeatPoint),
                () => { var r = R; return r != null && ((r.Pauses > 0 && !r.IsPaused) || r.Phase == RhythmPartyEncounter.Screen.Result); });
            pause.LineFn = () => R != null && R.IsPaused
                ? "The music rests. Strike any drum when you are ready and I will count four before it goes on."
                : "Need a breath? Tap the log seat: the music rests and nothing is lost. Strike a drum to go on.";
            var tally = Step("tally", "I keep count of every strike. Tap me to read how many were Perfect, Great, OK or missed.",
                () => Point(p => p.SolPoint), () => t.Flag("tally"));
            var bell = Step("bell", null, () => R != null && R.Phase == RhythmPartyEncounter.Screen.Result ? Point(p => p.BellPoint) : null,
                () => t.Flag("tuned"), 2f);
            bell.LineFn = () => R != null && R.Phase == RhythmPartyEncounter.Screen.Result
                ? "If the drums feel early or late, tap the tuning bell, then tap along with its eight clicks: I will set your timing."
                : "When the phrase ends, the tuning bell will wait by the fire. Tap it to line up your timing.";
            return new List<TutorialStep> { hit, torch, pause, tally, bell };
        }
    }
}
