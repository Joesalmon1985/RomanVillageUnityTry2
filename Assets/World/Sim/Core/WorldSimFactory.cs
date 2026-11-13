namespace World.Sim
{
    /// <summary>
    /// Builds a new game. Order is fixed: core state and board, then every module's InitState,
    /// then every module's Register. Modules edit only their own XModule.cs, never this file.
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
                    Name = id == PlayerIds.Human ? "Player" : "Rival",
                    Coin = StartingCoin,
                });
            }

            Economy.EconomyModule.InitState(s);
            Strategy.StrategyModule.InitState(s);
            Military.MilitaryModule.InitState(s);

            var sim = new WorldSim(s);
            Economy.EconomyModule.Register(sim);
            Strategy.StrategyModule.Register(sim);
            Military.MilitaryModule.Register(sim);
            return sim;
        }
    }
}
