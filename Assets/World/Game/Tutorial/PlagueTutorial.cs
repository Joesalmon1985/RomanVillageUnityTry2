using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The plague's first-time tutorial (id <c>plague</c>), told by the healer at her lodge: ask "How bad is it?", read the
    /// lanterns, the rate stick and the vessels, walk to a shrine with demons and banish one (preview, then confirm), and watch
    /// the charm cool. If no demon stands anywhere yet, the banish step waits and says so. Nothing here sends a command.
    /// </summary>
    public static class PlagueTutorial
    {
        public const string Id = "plague";
        private const float StartMetres = 14f;
        private static PlagueViews P => PlagueViews.Instance;
        private static HealersLodge Lodge => HealersLodge.Instance;

        private static float DistToHealer()
        {
            var l = Lodge; var w = PlayerWalker.Instance;
            if (l == null || l.Healer == null || w == null) return float.MaxValue;
            return BoardSpace.Flat(l.Healer.transform.position - w.Position);
        }

        private static bool InPlace()
        {
            if (P == null || Lodge == null || !Lodge.PlagueRuns) return false;
            var d = GodBoardDirector.Instance;
            if (d != null && d.View != BoardView.Altar) return false;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return false;
            return true;
        }

        /// <summary>The nearest shrine that holds demons, as the player sees it (null when none).</summary>
        public static NodeView NearestDemonShrine()
        {
            var p = P; var w = PlayerWalker.Instance;
            if (p == null) return null;
            NodeView best = null; float bd = float.MaxValue;
            foreach (var n in p.Nodes)
            {
                if (n == null || n.Total <= 0) continue;
                float d = w != null ? BoardSpace.Flat(n.transform.position - w.Position) : 0f;
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        public static Tutorial Create()
        {
            var t = new Tutorial { Id = Id, Title = "The plague" };
            t.GameActive = () =>
            {
                if (!InPlace()) return false;
                if (t.Steps.Count > 0 && t.Index >= IndexOf(t, "banish-preview"))
                {
                    // The walk to the shrines. While no demon stands anywhere the step waits: it speaks in her yard and is silent elsewhere.
                    if (t.Current != null && t.Current.Id == "banish-preview" && NearestDemonShrine() == null) return DistToHealer() <= StartMetres * 1.6f;
                    return true;
                }
                return DistToHealer() <= StartMetres * 1.6f;
            };
            t.CanStart = () => InPlace() && DistToHealer() <= StartMetres;
            t.OnBegin = tt => { tt.Counters["b0"] = P != null ? P.BanishesSent : 0; };
            t.BuildSteps = BuildSteps;
            t.Outro = "That is how we fight it. Read the post, banish at the shrines, keep the lanterns dark. I will be here.";
            t.OutroTutorName = "The healer";
            t.OutroSpeaker = TutorialTargets.Npc("healer");
            return t;
        }

        private static int IndexOf(Tutorial t, string id) { for (int i = 0; i < t.Steps.Count; i++) if (t.Steps[i].Id == id) return i; return int.MaxValue; }

        private static TutorialStep Step(string id, string line, Func<Rect?> target, Func<bool> done, float min = 1.2f) =>
            new TutorialStep { Id = id, TutorName = "The healer", Speaker = TutorialTargets.Npc("healer"), Line = line, Target = target, Done = done, MinSeconds = min };

        private static Rect? OfLodge(Func<LodgeInspect> f) { var l = f(); return l != null ? TutorialTargets.Of(l) : null; }

        private static List<TutorialStep> BuildSteps(Tutorial t)
        {
            var steps = new List<TutorialStep>
            {
                Step("ask", "Sickness walks the island. Tap me, then ask \"How bad is it?\"", () => Lodge != null && Lodge.Healer != null ? TutorialTargets.Of(Lodge.Healer) : null,
                    () => Lodge != null && Lodge.HowBadAsked > 0),
                Step("lanterns", "The lanterns on the post count the outbreaks: one is lit for each, and at eight the island falls. Tap the post.",
                    () => OfLodge(() => LodgeTable.Instance != null ? LodgeTable.Instance.Post : null), () => t.SawInspect("Warning post")),
                Step("stick", "The rate stick shows how many shrines are struck each round. The peg climbs as the sickness spreads. Tap the stick.",
                    () => OfLodge(() => LodgeTable.Instance != null ? LodgeTable.Instance.Stick : null), () => t.SawInspect("Rate stick")),
                Step("vessels", "Four vessels, one for each element: clear when cured, lidded when gone, cracked when it grows stronger. Tap one.",
                    () => { var lt = LodgeTable.Instance; if (lt == null) return null; return TutorialTargets.Union(TutorialTargets.Of(lt.Vessels[0]), TutorialTargets.Of(lt.Vessels[1]), TutorialTargets.Of(lt.Vessels[2]), TutorialTargets.Of(lt.Vessels[3])); },
                    () => t.SawInspect(" vessel")),
            };
            var preview = Step("banish-preview", null,
                () => { var n = NearestDemonShrine(); return n != null && P != null ? TutorialTargets.OfPoint(P.NodePosition(n.Id) + Vector3.up * 1.2f, Screen.height * 0.16f) : null; },
                () => t.DidPreview(" shrine") || BanishedNow(t));
            preview.LineFn = () => NearestDemonShrine() != null
                ? "Demons stand on a shrine's cairn. Walk to one and tap it once: they flinch and a card asks if you want to banish one."
                : "No demon stands on a shrine yet. When the runner brings word of an outbreak, one will. Look for the cairns, then tap one.";
            steps.Add(preview);
            steps.Add(Step("banish-confirm", "Tap the same shrine again to banish one demon.",
                () => { var n = NearestDemonShrine(); return n != null && P != null ? TutorialTargets.OfPoint(P.NodePosition(n.Id) + Vector3.up * 1.2f, Screen.height * 0.16f) : null; },
                () => BanishedNow(t)));
            var charm = Step("charm", "See the charm in your hand dim while it cools. It glows again when it is ready.",
                () => BanishCharm.Instance != null && BanishCharm.Instance.Held ? TutorialTargets.Of(BanishCharm.Instance) : null, null);
            charm.AutoSeconds = 7f;
            steps.Add(charm);
            return steps;
        }

        private static bool BanishedNow(Tutorial t) => P != null && P.BanishesSent > t.Count("b0");
    }
}
