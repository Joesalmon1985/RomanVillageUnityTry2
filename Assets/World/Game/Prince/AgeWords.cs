using World.Sim;
using World.Sim.Eras;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// Wording of the overrun by age (PRD_AGES 4.3; H1-4). Only the 20-minute Prehistoric skirmish ends by overrun; in
    /// every other game an overrun is a setback (a camp is lost). The sim's sentences (<c>FactionApi.Warning</c>,
    /// <c>Headline</c>) keep the old words, so the map, the HUD strip and the war camp's label read them through here.
    /// Read only.
    /// </summary>
    public static class AgeWords
    {
        /// <summary>True when an overrun is a setback, not a defeat.</summary>
        public static bool Setback(WorldState s) => !(AgesApi.Mode(s) == PlayMode.Skirmish && AgesApi.Current(s) == Era.Prehistoric);

        /// <summary>The warning's sentence as the age words it.</summary>
        public static string Warning(WorldState s, WarningView w)
        {
            if (!w.Active || !Setback(s)) return w.Sentence;
            long secs = (w.TicksLeft + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond;
            return w.Attacker == PlayerIds.Human
                ? "Your people are gathering against the Ash Clan: in " + secs + " s they take a camp unless the Ash Clan grows stronger."
                : "The Ash Clan is gathering against your people: " + secs + " s to grow stronger, or we lose a camp.";
        }

        /// <summary>One line: the ending, else the warning, else the strengths.</summary>
        public static string Headline(WorldState s)
        {
            var o = FactionApi.Outcome(s);
            if (o.GameOver) return o.Sentence;
            var w = FactionApi.Warning(s);
            return w.Active ? Warning(s, w) : FactionApi.Headline(s);
        }

        /// <summary>The map's strength line: "A lead of N overruns the other people" reads "takes a camp" in a setback age.</summary>
        public static string StrengthLine(WorldState s)
        {
            string t = MapScreenView.StrengthLine(s);
            return Setback(s) ? t.Replace("overruns the other people", "takes a camp from the other people") : t;
        }
    }
}
