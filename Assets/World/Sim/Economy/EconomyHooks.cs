using System;
using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>
    /// Extension points for other modules. Held per <see cref="WorldSim"/> (never in WorldState);
    /// get it with <see cref="EconomyApi.Hooks"/> inside your module's Register. Lists run in
    /// registration order. Delegates must be deterministic and must not keep state outside WorldState.
    /// </summary>
    public sealed class EconomyHooks
    {
        /// <summary>Called when EndRound applies, before Round increments (rival turn, infection...).</summary>
        public readonly List<Action<WorldState, EventBuffer>> OnRoundEnding = new List<Action<WorldState, EventBuffer>>();

        /// <summary>Called after the round-start draw and its batches (draft, ages...).</summary>
        public readonly List<Action<WorldState, EventBuffer>> OnRoundStarted = new List<Action<WorldState, EventBuffer>>();

        /// <summary>
        /// Applied in order to every batch of the round draw: (state, hex, ware, amount) -> new amount.
        /// Runs before the forester halving. Not applied by direct <see cref="EconomyApi.AuthoriseBatch"/> calls.
        /// </summary>
        public readonly List<Func<WorldState, int, Ware, int, int>> BatchModifiers = new List<Func<WorldState, int, Ware, int, int>>();

        /// <summary>(state, vertex) -> true when the flag is blocked (demons). Carriers do not route through or onto it.</summary>
        public readonly List<Func<WorldState, int, bool>> IsVertexBlocked = new List<Func<WorldState, int, bool>>();

        /// <summary>(state, buildingId) -> powered, for NeedsPower buildings. Default: always powered.</summary>
        public Func<WorldState, int, bool> IsPowered = (s, id) => true;

        internal bool[] ComputeBlocked(WorldState s)
        {
            if (IsVertexBlocked.Count == 0) return null;
            int n = s.Board.Topology.VertexCount;
            bool[] b = null;
            for (int v = 0; v < n; v++)
            {
                foreach (var f in IsVertexBlocked)
                {
                    if (!f(s, v)) continue;
                    if (b == null) b = new bool[n];
                    b[v] = true;
                    break;
                }
            }
            return b;
        }

        internal bool Powered(WorldState s, Building b)
        {
            var def = s.Economy.Tuning.Def(b.Type);
            return !def.NeedsPower || IsPowered == null || IsPowered(s, b.Id);
        }
    }
}
