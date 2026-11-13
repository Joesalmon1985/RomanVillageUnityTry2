using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>
    /// Entry point of the Strategy module: ages and rounds, cities, market, demons and hero, draft and
    /// cards, wonder, score and endings. InitState creates the state; Register adds systems, handlers and
    /// economy hooks (call it again on a sim built around a loaded state: hooks are not saved).
    /// </summary>
    public static class StrategyModule
    {
        public static void Register(WorldSim sim)
        {
            var hooks = EconomyApi.Hooks(sim);
            hooks.OnRoundEnding.Add(StrategyLogic.OnRoundEnding);
            hooks.OnRoundStarted.Add(StrategyLogic.OnRoundStarted);
            hooks.BatchModifiers.Add(StrategyLogic.ModifyBatch);
            hooks.IsVertexBlocked.Add((s, v) => s.Strategy != null && s.Strategy.Demons[v] > 0);
            hooks.EndRoundGuards.Add(StrategyLogic.EndRoundGuard);

            sim.AddSystem(SystemOrder.Construction + 50, new WonderSystem());
            sim.AddSystem(SystemOrder.Score, new ScoreSystem());

            sim.RegisterHandler(new FoundCityHandler());
            sim.RegisterHandler(new TradeHandler());
            sim.RegisterHandler(new SellHandler());
            sim.RegisterHandler(new BuyHandler());
            sim.RegisterHandler(new MoveHeroHandler());
            sim.RegisterHandler(new DraftPickHandler());
            sim.RegisterHandler(new BuildCardHandler());
            sim.RegisterHandler(new DiscardCardHandler());
            sim.RegisterHandler(new BuildWonderStageHandler());
        }

        public static void InitState(WorldState s)
        {
            StrategyLogic.Init(s);
        }
    }

    /// <summary>Completes an ordered wonder stage once all its wares are in the wonder site's store.</summary>
    internal sealed class WonderSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => StrategyLogic.TickWonder(s, ev);
    }

    /// <summary>Score slot: first side to the target points ends the game.</summary>
    internal sealed class ScoreSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => StrategyLogic.CheckPoints(s, ev);
    }
}
