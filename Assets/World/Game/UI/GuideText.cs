using World.Sim;

namespace World.Game
{
    /// <summary>
    /// The pause-menu guide pages (Controls, How to play). Owner: package F (REFACTOR_SPEC appendix B); F-1 rewrites the
    /// text for the real keys once P, D, V and N have landed. The F-0 text below describes the interim build honestly.
    /// </summary>
    public static class GuideText
    {
        private static string H(string t) => "<b><color=#F2B84D>" + t + "</color></b>\n";

        public static readonly string ControlsLeft =
            H("On foot (interim)") +
            "W A S D walk, Shift run, mouse to look.\n" +
            "E talk or use (the yellow prompt), left click inspect.\n" +
            "Tab switches to the overhead view and back; you stay where you stood.\n\n" +
            H("Overhead view (Tab)") +
            "W A S D or middle-drag to pan, wheel to zoom. H home, M whole island.\n\n" +
            H("Anywhere") +
            "F5 save, F9 load (one slot). Esc this menu, or closes a dialogue.";

        public static readonly string ControlsRight =
            H("Ward duel (Mira)") +
            "1 2 3 4 add essences, Backspace removes one.\n" +
            "Enter locks your Ward, then Enter or Space casts a guess.\n" +
            "Result: R rematch, Enter or Esc return.\n\n" +
            H("Dance circle") +
            "Coming back with the dance circle by the fire (drums D F J K).";

        public static string HowToPlayLeft(WorldState s) =>
            H("1. The island") +
            "Two peoples, yours and the Ash Clan, each start with a camp on a hex corner. Dice roll by themselves every 10 seconds; hexes whose number comes up grow young stock that camps beside them collect.\n\n" +
            H("2. Mira") +
            "Talk to Mira (E) by the home camp for a practice duel: guess her hidden three-part Ward. After each guess you see how many parts are right (fractures), right but in the wrong place (echoes) and wrong (fades).";

        public static string HowToPlayRight(WorldState s) =>
            H("3. Work in progress") +
            "The prince, his proposals and spells, the camps' buildings and carriers, and the dance circle are being built. " +
            "For now the island runs on its own: watch the clock and the dice.";
    }
}
