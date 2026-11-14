using World.Sim;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// The pause-menu guide pages (Controls, How to play) for the prehistoric game. Owner: package F (REFACTOR_SPEC
    /// appendix B, 8.6). The same text, longer, is in Docs/PLAY_GUIDE.md.
    /// </summary>
    public static class GuideText
    {
        private static string H(string t) => "<b><color=#F2B84D>" + t + "</color></b>\n";

        public static readonly string ControlsLeft =
            H("Walking") +
            "W or Up: one step forward.  S or Down: one step back.\n" +
            "A or Left, D or Right: turn a sixth of a circle.\n" +
            "The number pad works too (8, 2, 4, 6). Hold a key to keep going. There is no mouse look.\n\n" +
            H("Pointer") +
            "The mouse is always free. Click a person to talk or to follow them, a thing to see what it is, " +
            "and the glowing ring on the ground to cast when you stand within 15 m of it. " +
            "Right click (or any movement key) stops following.\n\n" +
            H("Speech bubbles") +
            "Click a reply under a bubble, or press 1, 2 or 3.";

        public static readonly string ControlsRight =
            H("Keys") +
            "Tab: the map (click a spot, close the map, and you walk there).\n" +
            "H: game time, the last roll, both peoples' strength, your nearest camp's stock.\n" +
            "F5 save, F9 load (one slot). Esc: this menu.\n" +
            "F1: developer tools (not needed to play).\n\n" +
            H("Ward duel (Mira)") +
            "1 2 3 4 add essences, Backspace removes one. Enter locks your Ward, then Enter or Space casts a guess. " +
            "After the duel: R rematch, Enter or Esc return.\n\n" +
            H("Dance (Sol)") +
            "Drums D F J K, left to right: strike as the ember lands, hold while embers keep coming. " +
            "Last block: Left arrow fire leap, Right arrow spirit call. H shows the score strip.";

        public static string HowToPlayLeft(WorldState s) =>
            H("1. The dice and the land") +
            "Two dice roll every 10 seconds. A hex whose number comes up, with a camp on one of its corners, grows young " +
            "stock (saplings, seedlings, lambs, clay, stone). It ripens in about 75 seconds; a camp's collecting " +
            "building then gathers it and carriers walk it to the camp's store.\n\n" +
            H("2. The prince and your spells") +
            "You command nothing. The prince, Holt the sawyer and Wenna the merchant come to you with what they want " +
            "to build. Answer \"Show me\" and follow, or walk to the gold ring yourself, stand within 15 m and click " +
            "inside it: that spell starts the work. \"Not this\" drops a proposal for a minute; \"Later\" puts it off. " +
            "Click a villager to hear their proposal again.";

        public static string HowToPlayRight(WorldState s)
        {
            string strength = "";
            if (s != null && s.Factions != null)
            {
                var me = FactionApi.Strength(s, PlayerIds.Human); var them = FactionApi.Strength(s, PlayerIds.Rival);
                strength = " Now: your people " + me.Total + ", the Ash Clan " + them.Total + ".";
            }
            return
                H("3. Winning and losing") +
                "A camp counts 1, a city 2, a fed warrior 1. A people that leads by 3 raises a war camp and, if the lead " +
                "holds for 90 seconds, overruns the other. No warning comes in the first 8 minutes; at 20 minutes the " +
                "stronger people wins." + strength + " Ignore the prince and the Ash Clan overruns you.\n\n" +
                H("4. Demons") +
                "Later, a roll of 7 can put a demon on a camp or a road of yours: the camp stops gathering, the road is " +
                "closed. Walk there and click the blue ring to banish it.\n\n" +
                H("5. Mira and Sol") +
                "Mira by the home camp offers a Ward duel; Sol at the fire in the fields offers the dance. The world waits " +
                "during both.";
        }
    }
}
