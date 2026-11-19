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

        /// <summary>G4-3 dev jump: an all-AI table played to the end; the world opens in Historic (temples already down).</summary>
        public const string HistoricId = "dev.godboard.historic";
        /// <summary>G4-3 dev jump: the same, one tick before the age ends, so the switch (temples falling, castles starting) plays live.</summary>
        public const string EveId = "dev.godboard.eve";

        /// <summary>Plays an AI-only god board game (first seed from <paramref name="seed"/> whose island stands) to the end of the age; with <paramref name="eve"/> stops one tick before.</summary>
        public static WorldSim CreateHistoric(ulong seed, bool eve)
        {
            for (ulong k = seed; k < seed + 60; k++)
            {
                var setup = GameSetup.GodBoard;
                setup.TableHumanSeat = -1;
                var sim = WorldSimFactory.CreateNew(k, setup);
                for (int i = 0; i < 60000 && sim.State.Table.Phase != World.Sim.Table.TablePhase.Ended; i++) { sim.Step(); sim.DrainEvents(); }
                if (sim.State.Historic == null) continue;
                if (!eve) return sim;
                long end = sim.State.Tick;
                var again = WorldSimFactory.CreateNew(k, setup);
                while (again.State.Tick < end - 1) { again.Step(); again.DrainEvents(); }
                return again;
            }
            return null;
        }

        public static WorldSim Create(ulong seed)
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = 0;
            return WorldSimFactory.CreateNew(seed, setup);
        }
    }
}
