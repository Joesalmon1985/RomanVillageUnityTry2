using System;
using UnityEngine;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Which interior the player is in, and moving between the surface and interiors (PRD_CRYSTALS appendix B, I5).
    /// <b>I0 shell</b> (14 Nov 2026): the names others code against, with no interiors behind them: the player is
    /// always on the surface, <see cref="Enter"/> and <see cref="LeadOut"/> do nothing. Owner: I5.
    /// </summary>
    public static class Interiors
    {
        /// <summary>The interior the player is in, or null on the surface.</summary>
        public static string Current { get; private set; }
        /// <summary>Raised with the new <see cref="Current"/> (null = back on the surface).</summary>
        public static event Action<string> Changed;

        /// <summary>Walks the player to a marker (surface or interior). I0 STUB.</summary>
        public static void Enter(MarkerRef to) { }
        /// <summary>Back up to the surface by the interior's way out. I0 STUB.</summary>
        public static void Exit() { }
        /// <summary>"Lead me out": walks the player to the way out. I0 STUB.</summary>
        public static void LeadOut() { }
        /// <summary>Builds the interiors of a definition (floors, stairs, lights). I0 STUB.</summary>
        public static void Build(CampaignDefinition definition) { }
        /// <summary>Rebuilds from the loaded state (after a load or a new game). I0 STUB.</summary>
        public static void RebuildFromState() { }

        /// <summary>For I5: sets <see cref="Current"/> and raises <see cref="Changed"/>.</summary>
        internal static void SetCurrent(string interiorId)
        {
            if (Current == interiorId) return;
            Current = interiorId;
            Changed?.Invoke(interiorId);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; Changed = null; }
    }

    /// <summary>
    /// Where interiors lie on the walk lattice (PRD_CRYSTALS appendix B, I5): each interior has its own block of cells
    /// beyond the board. <b>I0 shell</b>: no interior is placed; <see cref="TryLocal"/> is always false. Owner: I5.
    /// </summary>
    public static class InteriorSpace
    {
        /// <summary>The lattice cell of an interior's local (0, 0). I0 STUB: the board centre.</summary>
        public static Cell Origin(int interiorIndex) => new Cell(0, 0);
        /// <summary>World point of an interior's local cell. I0 STUB: the surface point of that cell from the origin.</summary>
        public static Vector3 ToWorld(string interiorId, int i, int j) => WalkLattice.ToWorld(new Cell(i, j));
        /// <summary>The interior and local cell of a world cell. I0 STUB: false (everything is surface).</summary>
        public static bool TryLocal(Cell world, out string interiorId, out int i, out int j)
        {
            interiorId = null; i = j = 0;
            return false;
        }
    }
}
