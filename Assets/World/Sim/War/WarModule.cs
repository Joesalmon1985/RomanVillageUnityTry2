using System;

namespace World.Sim.War
{
    /// <summary>Runs <see cref="WarRules.Tick"/> as a per-tick system.</summary>
    public sealed class WarSystem : ISimSystem
    {
        /// <summary>PR-3 stand-in: is the settlement road-linked to its lodge? Null = always.</summary>
        public Func<WorldState, int, bool> Linked;

        public void Tick(WorldState s, EventBuffer ev) => WarRules.Tick(s, ev, Linked);
    }

    /// <summary>Entry point of the war module. On god board games GodBoardModule initialises it (after the plague) and registers it (G0-3).</summary>
    public static class WarModule
    {
        /// <summary>Fresh war state for a number of peoples: no settlements, peace, own Rng forked from the world seed.</summary>
        public static void InitState(WorldState s, int peopleCount)
        {
            s.War = new WarState
            {
                PeopleCount = peopleCount,
                Rng = s.Rng.Fork("war"),
                Points = new int[peopleCount],
                AtWar = new bool[peopleCount * peopleCount],
            };
        }

        /// <summary>Adds the war system after the combat slot of the tick order.</summary>
        public static WarSystem Register(WorldSim sim, Func<WorldState, int, bool> linked = null)
        {
            var sys = new WarSystem { Linked = linked };
            sim.AddSystem(SystemOrder.Combat + 10, sys);
            return sys;
        }
    }
}
