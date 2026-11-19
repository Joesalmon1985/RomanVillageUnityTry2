namespace World.Sim
{
    /// <summary>
    /// Builds a new game. Order is fixed: core state and board, then every module's InitState,
    /// then every module's Register, in the order Economy, Factions, Demons (REFACTOR_SPEC A.4), then Eras and
    /// Ledger (PRD_AGES A.2, A.3).
    /// </summary>
    public static class WorldSimFactory
    {
        public const int StartingCoin = 0;

        /// <summary>A legacy game: two peoples on the fixed MVP board (<see cref="GameSetup.Legacy"/>).</summary>
        public static WorldSim CreateNew(ulong seed) => CreateNew(seed, GameSetup.Legacy);

        /// <summary>A new game with an explicit setup (number of peoples, which board).</summary>
        public static WorldSim CreateNew(ulong seed, GameSetup setup)
        {
            if (setup == null) setup = GameSetup.Legacy;
            if (setup.Peoples < 1 || setup.Peoples > PlayerIds.MaxCount)
                throw new System.ArgumentOutOfRangeException(nameof(setup), "A game has 1 to " + PlayerIds.MaxCount + " peoples.");
            var s = new WorldState
            {
                Seed = seed,
                Tick = 0,
                Rng = new Rng(seed),
                Board = setup.Board == BoardKind.Dealt ? BoardGenerator.CreateDealtBoard(seed) : BoardGenerator.CreateMvpBoard(seed),
            };
            s.Peoples = PeopleDefs.FirstN(setup.Peoples);
            for (int id = 0; id < setup.Peoples; id++)
            {
                s.Players.Add(new PlayerState
                {
                    Id = id,
                    Name = s.Peoples[id].Name,
                    Coin = StartingCoin,
                });
            }

            Economy.EconomyModule.InitState(s);
            Factions.FactionsModule.InitState(s);
            Demons.DemonsModule.InitState(s);
            // Later ages (PRD_AGES A.2, A.3; H1-0): appended after the refactor's modules so the earlier modules'
            // starting state, random streams and first plans are exactly as before.
            Eras.ErasModule.InitState(s);
            Ledger.LedgerModule.InitState(s);

            var sim = Save.SaveCodec.CreateSim(s);
            Campaign.CampaignModule.Register(sim);   // PRD_CRYSTALS I0 (idempotent; loaded saves need the same line in SaveCodec.CreateSim)
            return sim;
        }
    }

    /// <summary>Which board a new game gets.</summary>
    public enum BoardKind
    {
        /// <summary>The fixed MVP terrain table with dealt numbers (legacy ruleset, BD-6).</summary>
        Legacy = 0,
        /// <summary>The god board deal (BD-1 .. BD-5): fresh terrain and numbers, four fair start corners.</summary>
        Dealt = 1,
    }

    /// <summary>Options for <see cref="WorldSimFactory.CreateNew(ulong, GameSetup)"/>.</summary>
    public sealed class GameSetup
    {
        /// <summary>Number of peoples (1 .. <see cref="PlayerIds.MaxCount"/>). Peoples beyond the board's start corners get no first camp.</summary>
        public int Peoples = PlayerIds.LegacyCount;
        public BoardKind Board = BoardKind.Legacy;

        /// <summary>Two peoples on the fixed MVP board: every game before G0-1, and the legacy tests.</summary>
        public static GameSetup Legacy => new GameSetup { Peoples = PlayerIds.LegacyCount, Board = BoardKind.Legacy };

        /// <summary>The god board: four peoples (player, Ash Clan, Elves, Orcs) on a dealt board.</summary>
        public static GameSetup GodBoard => new GameSetup { Peoples = PlayerIds.GodBoardCount, Board = BoardKind.Dealt };
    }
}
