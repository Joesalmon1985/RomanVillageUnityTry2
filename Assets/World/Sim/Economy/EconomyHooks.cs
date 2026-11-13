using System;
using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>
    /// Cross-module extension points (REFACTOR_SPEC A.4). Held per <see cref="WorldSim"/> (never in WorldState); get it
    /// with <see cref="EconomyApi.Hooks"/> in a module's Register. Lists run in registration order. Delegates must be
    /// deterministic and keep no state outside WorldState.
    /// </summary>
    public sealed class EconomyHooks
    {
        /// <summary>(state, corner) -> true when the corner is demon-held: carriers do not route through it.</summary>
        public readonly List<Func<WorldState, int, bool>> IsCornerBlocked = new List<Func<WorldState, int, bool>>();

        /// <summary>(state, hexEdge) -> true when the road edge is demon-held: carriers do not use it, its site stops.</summary>
        public readonly List<Func<WorldState, int, bool>> IsEdgeBlocked = new List<Func<WorldState, int, bool>>();

        /// <summary>(state, settlementId) -> true when the settlement is demon-held: collectors stop, no shipments, no roll stock.</summary>
        public readonly List<Func<WorldState, int, bool>> IsSettlementHalted = new List<Func<WorldState, int, bool>>();

        /// <summary>After every roll and its stock: (state, events, d1, d2). The demons draw on a 7 here.</summary>
        public readonly List<Action<WorldState, EventBuffer, int, int>> OnRolled = new List<Action<WorldState, EventBuffer, int, int>>();

        /// <summary>After a site completes: (state, events, kind, id).</summary>
        public readonly List<Action<WorldState, EventBuffer, SiteKind, int>> OnSiteCompleted = new List<Action<WorldState, EventBuffer, SiteKind, int>>();

        public bool CornerBlocked(WorldState s, int corner)
        {
            foreach (var f in IsCornerBlocked) if (f(s, corner)) return true;
            return false;
        }

        public bool EdgeBlocked(WorldState s, int hexEdge)
        {
            foreach (var f in IsEdgeBlocked) if (f(s, hexEdge)) return true;
            return false;
        }

        public bool SettlementHalted(WorldState s, int settlementId)
        {
            foreach (var f in IsSettlementHalted) if (f(s, settlementId)) return true;
            return false;
        }

        public void Rolled(WorldState s, EventBuffer ev, int d1, int d2)
        {
            foreach (var a in OnRolled) a(s, ev, d1, d2);
        }

        public void SiteCompleted(WorldState s, EventBuffer ev, SiteKind kind, int id)
        {
            foreach (var a in OnSiteCompleted) a(s, ev, kind, id);
        }
    }
}
