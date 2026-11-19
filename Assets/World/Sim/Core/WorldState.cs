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

        /// <summary>Index = player id = people id. <see cref="PlayerIds.Human"/> = 0, <see cref="PlayerIds.Rival"/> = 1, ...</summary>
        public List<PlayerState> Players = new List<PlayerState>();

        /// <summary>
        /// The peoples of this game (PRD_GODBOARD PE-1), index = people id, same length as <see cref="Players"/>. Saves
        /// written before G0-1 have none; <see cref="PeopleDefs.Ensure"/> fills them from the players on load.
        /// </summary>
        public List<People> Peoples = new List<People>();

        public PlayerState Player(int id) => Players[id];

        /// <summary>Number of peoples in this game (2 in a legacy game, 4 on the god board). Never assume a fixed count.</summary>
        public int PeopleCount => Players.Count;

        /// <summary>True for a people id of this game.</summary>
        public bool IsPeople(int id) => id >= 0 && id < Players.Count;
    }

    /// <summary>Well-known people ids (PRD_GODBOARD PE-1). The count is per game: <see cref="WorldState.PeopleCount"/>.</summary>
    public static class PlayerIds
    {
        /// <summary>The player's people.</summary>
        public const int Human = 0;
        /// <summary>The Ash Clan, the campaign rival.</summary>
        public const int Rival = 1;
        public const int AshClan = Rival;
        public const int Elves = 2;
        public const int Orcs = 3;

        /// <summary>Peoples in a legacy (two-people) game.</summary>
        public const int LegacyCount = 2;
        /// <summary>Peoples on the god board now (PE-1).</summary>
        public const int GodBoardCount = 4;
        /// <summary>Most peoples the sim supports (PE-1: up to 6 later).</summary>
        public const int MaxCount = 6;

        /// <summary>Old fixed count, kept only so Game code still compiles. Sim code uses <see cref="WorldState.PeopleCount"/>.</summary>
        [System.Obsolete("Use WorldState.PeopleCount (G0-1: the number of peoples is per game).")]
        public const int Count = LegacyCount;
    }

    /// <summary>Per-player state. PARTIAL like WorldState: modules may add fields in their own folder.</summary>
    public sealed partial class PlayerState
    {
        public int Id;
        public string Name;
        public int Coin;
    }
}
