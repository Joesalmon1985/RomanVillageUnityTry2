namespace World.Sim
{
    /// <summary>
    /// Which rules a game runs (PRD_GODBOARD G0-2). Saved in <see cref="WorldState.Ruleset"/>; a save written before G0-2
    /// has none and loads as <see cref="Legacy"/>.
    /// </summary>
    public enum Ruleset
    {
        /// <summary>The prehistoric refactor: dice on a clock, growing hex stock, the prince's proposals and spells,
        /// strength, overrun and the 20-minute limit, the legacy demons.</summary>
        Legacy = 0,

        /// <summary>
        /// The god board ("BoardRules" in the PRD): the table's turn engine (<see cref="Table"/>) and the plague
        /// (<see cref="Plague"/>) replace the dice clock, stock growth, proposals and spells for building, strength,
        /// overrun, the time limit and the legacy demons. Those systems stay in code but are not registered.
        /// </summary>
        GodBoard = 1,
    }

    public sealed partial class WorldState
    {
        /// <summary>The rules this game runs (G0-2). Default Legacy, so every older save keeps its rules.</summary>
        public Ruleset Ruleset = Ruleset.Legacy;

        /// <summary>True when the game runs the god board rules.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsGodBoard => Ruleset == Ruleset.GodBoard;
    }
}
