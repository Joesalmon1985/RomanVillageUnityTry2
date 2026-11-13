namespace World.Sim
{
    /// <summary>
    /// Builds a new game. Order is fixed: core state and board, then every module's InitState,
    /// then every module's Register, in the order Economy, Factions, Demons (REFACTOR_SPEC A.4).
    /// </summary>
    public static class WorldSimFactory
    {
        public const int StartingCoin = 0;

        public static WorldSim CreateNew(ulong seed)
        {
            var s = new WorldState
            {
                Seed = seed,
                Tick = 0,
                Rng = new Rng(seed),
                Board = BoardGenerator.CreateMvpBoard(seed),
            };
            for (int id = 0; id < PlayerIds.Count; id++)
            {
                s.Players.Add(new PlayerState
                {
                    Id = id,
                    Name = id == PlayerIds.Human ? "Your people" : "the Ash Clan",
                    Coin = StartingCoin,
                });
            }

            Economy.EconomyModule.InitState(s);
            Factions.FactionsModule.InitState(s);
            Demons.DemonsModule.InitState(s);

            return Save.SaveCodec.CreateSim(s);
        }
    }
}
