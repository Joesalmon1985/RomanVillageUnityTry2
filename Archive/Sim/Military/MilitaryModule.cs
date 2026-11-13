using World.Sim.Economy;

namespace World.Sim.Military
{
    /// <summary>
    /// Entry point of the Military module: power allocation, barracks queues, squads and orders, fixed-tick
    /// combat, the rival outpost and raids. Register sets the economy's IsPowered predicate and the raid
    /// blocking predicate (hooks are per sim, so call Register again on a sim built around a loaded state).
    /// </summary>
    public static class MilitaryModule
    {
        public static void Register(WorldSim sim)
        {
            var hooks = EconomyApi.Hooks(sim);
            hooks.IsPowered = MilitaryLogic.IsPowered;
            hooks.PowerDemanded = MilitaryLogic.PowerDemanded;
            hooks.IsVertexBlocked.Add(MilitaryLogic.RaidBlocks);

            sim.AddSystem(SystemOrder.Power + 50, new PowerEventSystem());
            sim.AddSystem(SystemOrder.Barracks, new BarracksSystem());
            sim.AddSystem(SystemOrder.Squads, new SquadSystem());
            sim.AddSystem(SystemOrder.Combat, new CombatSystem());

            sim.RegisterHandler(new QueueUnitHandler());
            sim.RegisterHandler(new OrderSquadHandler());
            sim.RegisterHandler(new CancelOrderHandler());
        }

        public static void InitState(WorldState s)
        {
            MilitaryLogic.Init(s);
        }
    }

    internal sealed class PowerEventSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => MilitaryLogic.TickPowerEvents(s, ev, s.Military.Unpowered);
    }

    internal sealed class BarracksSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => MilitaryLogic.TickBarracks(s, ev);
    }

    internal sealed class SquadSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => MilitaryLogic.TickSquads(s, ev);
    }

    internal sealed class CombatSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => MilitaryLogic.TickCombat(s, ev);
    }
}
