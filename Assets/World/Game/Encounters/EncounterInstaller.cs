using System;
using System.Collections;
using UnityEngine;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game
{
    /// <summary>
    /// Registers the encounters ("ward_mira", "rhythm_party" = the dance at the fire) and the "duel_dance" AutoPlay script
    /// before the scene loads. No edit to WorldBootstrap is needed. Owner: package D.
    /// </summary>
    public static class EncounterInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            EncounterDirectory.Register(EncounterDirectory.WardMira, () => new WardDuelEncounter());
            EncounterDirectory.Register(EncounterDirectory.RhythmParty, () => new RhythmPartyEncounter());
            AutoPlayScripts.Register(DuelDance.Name, DuelDance.Create);
        }
    }

    /// <summary>
    /// "duel_dance" (was "first_five"): look round the camp at the spawn, walk to Mira, accept her duel and play it to
    /// the end (scripted candidate-filter player), return; walk to the dance circle and talk to Sol, dance a successful
    /// phrase choosing "Spirit call" (built-in autoplayer), return, and check the viewpoint is restored each time and
    /// the village clock did not move during either encounter. Logs "[AutoPlay] duel_dance OK" and quits 0 (in the
    /// editor: stays in Play Mode).
    /// </summary>
    public static class DuelDance
    {
        public const string Name = "duel_dance";
        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static Vector3 _before;
        private static float _beforeYaw;
        private static long _tick;

        private static CampLayout Camp => new CampLayout(B.Sim.State.Board);

        public static AutoPlayScript Create()
        {
            var s = new AutoPlayScript(Name);
            s.Wait(1f)
             .MeasureFps("spawn first person")
             .Turn(-50f, 1.2f).Turn(100f, 2f).Turn(-50f, 1.2f)
             .WalkTo(() => Npc.Find(NpcIds.Mira).transform.position, 2.5f)
             .Wait(0.4f)
             .Check(() => Npc.Find(NpcIds.Mira).IsGreetingShown, "Mira greeting not shown")
             .LookAt(() => Npc.Find(NpcIds.Mira).transform.position + Vector3.up * 1.2f, 0.4f)
             .Call(Remember)
             .Interact()
             .Check(() => DialoguePanel.IsOpen, "Mira's dialogue did not open")
             .Call(() => { if (!DialoguePanel.Choose(0)) throw new Exception("duel option refused"); })
             .Check(() => WardDuelEncounter.Current != null, "Ward duel did not start")
             .Call(MarkTick)
             .Call(() => { var bot = WardBot.Attach(B.gameObject); bot.CastInterval = 2.4f; bot.MaxCastsPerDuel = -1; bot.FixedWard = null; bot.AutoReturn = false; })
             .Step(a => WaitFor(a, () => WardDuelEncounter.Current != null && WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Result, 240f, "duel did not finish"))
             .Call(() => Debug.Log("[AutoPlay] duel_dance duel " + WardDuelEncounter.Current.LastOutcome + " after " + WardDuelEncounter.Current.Session.PlayerHistory.Count + " casts; completed duels " + B.Sim.State.Ward.CompletedDuelsVsMira))
             .Check(() => B.Sim.State.Tick == _tick, "village clock moved during the duel")
             .Wait(1.5f)
             .Call(() => WardDuelEncounter.Current.Return())
             .Wait(0.3f)
             .Call(() => { var bot = B.GetComponent<WardBot>(); if (bot != null) bot.enabled = false; })
             .Check(() => !EncounterHost.Instance.IsRunning, "duel still running after Return")
             .Check(Restored, "viewpoint not restored after the duel")
             // To the dance circle: along the camp side of the circle, round to Sol beyond the fire.
             .WalkTo(() => Camp.DanceCircleCentre - Camp.Forward * 9f + Camp.Right * 4f, 0.8f, false, 40f)
             .WalkTo(() => Camp.DanceCircleCentre + Camp.Right * 8.6f, 0.8f)
             .WalkTo(() => Camp.DanceCircleCentre + Camp.Forward * 3.2f + Camp.Right * 1.2f, 0.5f)
             .LookAt(() => Npc.Find(NpcIds.Host).transform.position + Vector3.up * 1.4f, 0.4f)
             .Call(Remember)
             .Interact()
             .Check(() => DialoguePanel.IsOpen, "Sol's dialogue did not open")
             .Call(() => { if (!DialoguePanel.Choose(0)) throw new Exception("dance option refused"); })
             .Check(() => RhythmPartyEncounter.Current != null, "the dance did not start")
             .Call(MarkTick)
             .Call(() => { var r = RhythmPartyEncounter.Current; r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Balcony; r.AutoSkipBlock = null; })
             .Step(a => WaitFor(a, () => RhythmPartyEncounter.Current != null && RhythmPartyEncounter.Current.Phase == RhythmPartyEncounter.Screen.Result, 60f, "phrase did not finish"))
             .Check(() => RhythmPartyEncounter.Current.LastResult.Success, "dance not a success")
             .Check(() => RhythmPartyEncounter.Current.LastRouteOpened == World.Sim.Encounters.Rhythm.RouteOutcome.Balcony, "spirit call not chosen")
             .Check(() => DanceCircle.Current != null && DanceCircle.Current.LastEffect == DanceCircle.Effect.SpiritCall, "spirits did not rise")
             .Check(() => B.Sim.State.Tick == _tick, "village clock moved during the dance")
             .Call(() => Debug.Log("[AutoPlay] duel_dance phrase: passed " + RhythmPartyEncounter.Current.LastResult.PassedBlocks + " blocks, perfect " + RhythmPartyEncounter.Current.LastResult.Perfect + ", ending " + RhythmPartyEncounter.ChoiceName(SimRoute.Balcony)))
             .Wait(1.5f)
             .Call(() => RhythmPartyEncounter.Current.Return())
             .Wait(0.3f)
             .Check(() => !EncounterHost.Instance.IsRunning, "dance still running after Return")
             .Check(Restored, "viewpoint not restored after the dance")
             .Check(() => DanceCircle.Current.ActiveDancers == 0, "dancers not back at their seats")
             .Log("[AutoPlay] " + Name + " OK")
             .Quit(0);
            return s;
        }

        private static void Remember()
        {
            _before = B.Avatar.Position; _beforeYaw = B.Avatar.Yaw;
        }

        private static void MarkTick() => _tick = B.Sim.State.Tick;

        private static bool Restored() =>
            (B.Avatar.Position - _before).magnitude < 0.001f && Mathf.Abs(Mathf.DeltaAngle(B.Avatar.Yaw, _beforeYaw)) < 0.1f;

        private static IEnumerator WaitFor(AutoPlay a, Func<bool> cond, float timeout, string message)
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
