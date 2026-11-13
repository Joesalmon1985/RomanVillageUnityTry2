using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// The complete, serialisable simulation state. PARTIAL: each module declares its own fields
    /// in its own folder (e.g. Economy/WorldState.Economy.cs). Plain data only: public fields or
    /// auto-properties, no references to systems, no UnityEngine types.
    /// </summary>
    public sealed partial class WorldState
    {
        public ulong Seed;

        /// <summary>Number of completed <see cref="WorldSim.Step"/> calls. 10 ticks = 1 second.</summary>
        public long Tick;

        /// <summary>Shared deterministic RNG. Prefer <c>Rng.Fork("module")</c> for module-local streams.</summary>
        public Rng Rng = new Rng();

        public Board Board;

        /// <summary>Index = player id. <see cref="PlayerIds.Human"/> = 0, <see cref="PlayerIds.Rival"/> = 1.</summary>
        public List<PlayerState> Players = new List<PlayerState>();

        public PlayerState Player(int id) => Players[id];
    }

    public static class PlayerIds
    {
        public const int Human = 0;
        public const int Rival = 1;
        public const int Count = 2;
    }

    /// <summary>Per-player state. PARTIAL like WorldState: modules may add fields in their own folder.</summary>
    public sealed partial class PlayerState
    {
        public int Id;
        public string Name;
        public int Coin;
    }
}
