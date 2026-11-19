using World.Sim;

namespace World.Game
{
    /// <summary>
    /// The developer start of the god board (G1-5): <c>-start dev.godboard [-seed n]</c> or F1 "Starts". A new game on the
    /// GodBoard ruleset (dealt board, four peoples, the table closed until the player sacrifices at their altar), the human on
    /// seat 0. The legacy start is unchanged. <see cref="DevStart"/> builds it like any other developer start.
    /// </summary>
    public static class GodBoardStart
    {
        public const string Id = "dev.godboard";

        public static WorldSim Create(ulong seed)
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = 0;
            return WorldSimFactory.CreateNew(seed, setup);
        }
    }
}
