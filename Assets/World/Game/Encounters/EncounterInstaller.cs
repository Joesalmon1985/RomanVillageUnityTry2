using System;
using System.Collections;
using UnityEngine;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game
{
    /// <summary>
    /// Registers the Milestone 2 encounters ("ward_mira", "rhythm_party") and the "first_five" AutoPlay script before the
    /// scene loads. No edit to WorldBootstrap is needed.
    /// </summary>
    public static class EncounterInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            EncounterDirectory.Register(EncounterDirectory.WardMira, () => new WardDuelEncounter());
            EncounterDirectory.Register(EncounterDirectory.RhythmParty, () => new RhythmPartyEncounter());
            AutoPlayScripts.Register("first_five", FirstFive.Create);
        }
    }

    /// <summary>
    /// "first_five": the boot tour, then accept Mira's duel and play it to the end (scripted candidate-filter player),
    /// walk to the decks, play a successful phrase choosing the balcony (built-in autoplayer), walk onto the balcony and
    /// back to the square. Logs "[AutoPlay] first_five OK" and quits 0 (in the editor: stays in Play Mode).
    /// </summary>
    public static class FirstFive
    {
        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static Vector3 _before;
        private static float _beforeYaw;

        private static Vector3 W(float x, float z) => B.Warehouse.transform.TransformPoint(new Vector3(x, 0, z));

        public static AutoPlayScript Create()
        {
            var s = new AutoPlayScript("first_five");
            // Boot tour (without quitting).
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
             .Call(() => { var bot = WardBot.Attach(B.gameObject); bot.CastInterval = 2.4f; bot.MaxCastsPerDuel = -1; bot.FixedWard = null; bot.AutoReturn = false; })
             .Step(a => WaitFor(a, () => WardDuelEncounter.Current != null && WardDuelEncounter.Current.Phase == WardDuelEncounter.Screen.Result, 240f, "duel did not finish"))
             .Call(() => Debug.Log("[AutoPlay] first_five duel " + WardDuelEncounter.Current.LastOutcome + " after " + WardDuelEncounter.Current.Session.PlayerHistory.Count + " casts; completed duels " + B.Sim.State.Ward.CompletedDuelsVsMira))
             .Wait(1.5f)
             .Call(() => WardDuelEncounter.Current.Return())
             .Wait(0.3f)
             .Call(() => { var bot = B.GetComponent<WardBot>(); if (bot != null) bot.enabled = false; })
             .Check(() => !EncounterHost.Instance.IsRunning, "duel still running after Return")
             .Check(Restored, "avatar not restored after the duel")
             // To the decks.
             .WalkTo(() => B.Warehouse.OutsideDoorPosition, 1.0f, true)
             .WalkTo(() => B.Warehouse.DecksFrontPosition, 0.3f)
             .LookAt(() => Npc.Find(NpcIds.Host).transform.position + Vector3.up * 1.4f, 0.4f)
             .Call(Remember)
             .Interact()
             .Check(() => DialoguePanel.IsOpen, "DJ Sol's dialogue did not open")
             .Call(() => { if (!DialoguePanel.Choose(0)) throw new Exception("party option refused"); })
             .Check(() => RhythmPartyEncounter.Current != null, "rhythm party did not start")
             .Call(() => { var r = RhythmPartyEncounter.Current; r.AutoPlayEnabled = true; r.AutoRoute = SimRoute.Balcony; r.AutoSkipBlock = null; })
             .Step(a => WaitFor(a, () => RhythmPartyEncounter.Current != null && RhythmPartyEncounter.Current.Phase == RhythmPartyEncounter.Screen.Result, 60f, "phrase did not finish"))
             .Check(() => RhythmPartyEncounter.Current.LastResult.Success, "party not a success")
             .Check(() => B.Warehouse.IsRouteOpen(Route.Balcony), "balcony did not open")
             .Call(() => Debug.Log("[AutoPlay] first_five phrase: passed " + RhythmPartyEncounter.Current.LastResult.PassedBlocks + " blocks, perfect " + RhythmPartyEncounter.Current.LastResult.Perfect + ", route " + RhythmPartyEncounter.Current.LastRouteOpened))
             .Wait(1.5f)
             .Call(() => RhythmPartyEncounter.Current.Return())
             .Wait(0.3f)
             .Check(() => !EncounterHost.Instance.IsRunning, "party still running after Return")
             .Check(Restored, "avatar not restored after the party")
             // Onto the balcony and back.
             .WalkTo(() => B.Warehouse.RouteApproachPosition(Route.Balcony), 0.8f)
             .WalkTo(() => B.Warehouse.RampFootPosition, 0.6f)
             .WalkTo(() => B.Warehouse.RampHeadPosition, 0.6f)
             .WalkTo(() => B.Warehouse.RouteInsidePosition(Route.Balcony), 0.8f)
             .Check(() => B.Avatar.Position.y > WarehouseSet.BalconyHeight - 0.1f, "not on the balcony deck")
             .Wait(0.5f)
             .WalkTo(() => B.Warehouse.RampHeadPosition, 0.6f)
             .WalkTo(() => B.Warehouse.RampFootPosition, 0.6f)
             .WalkTo(() => B.Warehouse.RouteApproachPosition(Route.Balcony), 0.8f)
             .WalkTo(() => B.Warehouse.OutsideDoorPosition, 1.0f)
             .WalkTo(() => B.Layout.Spawn, 1.5f, true, 40f)
             .Log("[AutoPlay] first_five OK")
             .Quit(0);
            return s;
        }

        private static void Remember()
        {
            _before = B.Avatar.Position; _beforeYaw = B.Avatar.Yaw;
        }

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
