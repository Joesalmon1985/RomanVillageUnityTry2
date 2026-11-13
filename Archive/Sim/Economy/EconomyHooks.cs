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

        /// <summary>
        /// Extra checks for the EndRound command (draft pending, game over...): (state) -> Accept or a refusal.
        /// The first refusal wins. Must not mutate state.
        /// </summary>
        public readonly List<Func<WorldState, CommandResult>> EndRoundGuards = new List<Func<WorldState, CommandResult>>();

        /// <summary>(state, buildingId) -> powered, for NeedsPower buildings. Default: always powered.</summary>
        public Func<WorldState, int, bool> IsPowered = (s, id) => true;

        /// <summary>(state) -> true while some building needs power; a power plant only lights a new unit of fuel then. Default: always.</summary>
        public Func<WorldState, bool> PowerDemanded = s => true;

        /// <summary>
        /// (state, hex) -> true when the hex produces nothing (the robber: a demon on every player city touching it).
        /// A stopped hex gets no round batch and its harvesters do not take units from it.
        /// </summary>
        public readonly List<Func<WorldState, int, bool>> IsHexStopped = new List<Func<WorldState, int, bool>>();

        /// <summary>True when any IsVertexBlocked hook blocks the flag.</summary>
        public bool Blocks(WorldState s, int vertex)
        {
            foreach (var f in IsVertexBlocked) if (f(s, vertex)) return true;
            return false;
        }

        /// <summary>True when any IsHexStopped hook stops the hex.</summary>
        public bool HexStopped(WorldState s, int hex)
        {
            foreach (var f in IsHexStopped) if (f(s, hex)) return true;
            return false;
        }

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
