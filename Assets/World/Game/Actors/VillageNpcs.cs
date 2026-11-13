using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>Places the village people and their starter dialogue. Later agents edit Lines/Options at runtime.</summary>
    public static class VillageNpcs
    {
        public static void Spawn(WorldBootstrap boot)
        {
            var L = boot.Layout;
            var w = boot.Warehouse;
            float Face(Vector3 from, Vector3 to) => BoardSpace.YawOf(to - from);

            var mira = Npc.Spawn(NpcIds.Mira, "Mira", "Wizard. Practises Ward duels in the square.",
                "Fancy a practice duel? I promise to go easy. Mostly.", new Color(0.45f, 0.25f, 0.70f), L.MiraSpot, Face(L.MiraSpot, L.Spawn));
            mira.Lines.Add("A Ward is three loci of essence. Guess mine before I guess yours.");
            mira.Lines.Add("Each cast tells you fractures, echoes and fades. Nothing more.");
            mira.Options.Add(DialogueOption.Encounter("Practice duel", EncounterDirectory.WardMira));
            mira.Options.Add(DialogueOption.Leave("Maybe later"));

            var door = Npc.Spawn(NpcIds.Doorman, "Juno", "Party host at the warehouse door.",
                "Party's on! Come in, DJ Sol is at the decks.", new Color(0.95f, 0.35f, 0.55f), L.DoormanSpot, Face(L.DoormanSpot, L.Spawn));
            door.Lines.Add("Tonight's a 174 BPM night. Find DJ Sol at the decks at the back.");
            door.Lines.Add("Play well and the crowd opens the bar or the balcony.");
            door.Options.Add(DialogueOption.Do("Head inside", () => Hud.SetObjective("Find DJ Sol at the decks inside the warehouse.")));
            door.Options.Add(DialogueOption.Leave("Not yet"));

            var hostYaw = w.DecksYaw;
            var host = Npc.Spawn(NpcIds.Host, "DJ Sol", "Plays the decks inside the warehouse.",
                "One phrase at 174. Keep up and the crowd opens a new room.", new Color(0.20f, 0.75f, 0.85f), w.DecksPosition, hostYaw);
            host.Lines.Add("Four pads: D, F, J, K. Hit the cues as they land.");
            host.Lines.Add("In the last block, pick the bar or the balcony.");
            host.Options.Add(DialogueOption.Encounter("Play the phrase", EncounterDirectory.RhythmParty));
            host.Options.Add(DialogueOption.Leave("Just looking"));
            host.Stage = new EncounterStage(w.DanceFloorPosition, hostYaw + 180f);

            var qmYaw = Face(L.QuartermasterSpot, L.Home);
            var qm = Npc.Spawn(NpcIds.Quartermaster, "Quartermaster Bram", "Runs the storehouse. Buildings and roads.",
                "We need planks and stone. Roads first, then we build.", new Color(0.75f, 0.55f, 0.20f), L.QuartermasterSpot, qmYaw);
            qm.Lines.Add("Carriers walk the roads between flags. No road, no delivery.");
            qm.Options.Add(DialogueOption.Leave());

            var keeper = Npc.Spawn(NpcIds.MarketKeeper, "Hesta", "Market keeper. Trade.",
                "Fresh grain, fair prices. Trading opens soon.", new Color(0.30f, 0.60f, 0.30f), L.KeeperSpot, L.MarketYaw);
            keeper.Lines.Add("Come back when the stalls are stocked.");
            keeper.Options.Add(DialogueOption.Leave());

            var cap = Npc.Spawn(NpcIds.Captain, "Captain Orla", "Captain. Units and orders.",
                "The outpost to the west is quiet. For now.", new Color(0.65f, 0.15f, 0.15f), L.CaptainSpot, Face(L.CaptainSpot, L.Home));
            cap.Lines.Add("When the barracks stand, I'll drill your squads.");
            cap.Options.Add(DialogueOption.Leave());

            var scribe = Npc.Spawn(NpcIds.Scribe, "Scribe Pell", "Scribe. Cards and the wonder.",
                "My ledgers are nearly ready. Cards and wonder plans soon.", new Color(0.25f, 0.35f, 0.70f), L.ScribeSpot, Face(L.ScribeSpot, L.Home));
            scribe.Lines.Add("Every card has a cost in carried wares. Nothing is free.");
            scribe.Options.Add(DialogueOption.Leave());
        }
    }
}
