using System.Collections.Generic;
using UnityEngine;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Which lattice cells the player may step into (REFACTOR_SPEC 2.D) and paths between cells. Owner: package P.
    /// F-0 stub: static blockers are recorded (V and D can register theirs now); <see cref="IsBlocked"/> only knows the
    /// board limit and the static blockers, and <see cref="FindPath"/> returns null. P implements the full rules.
    /// </summary>
    public static class WalkGrid
    {
        private struct Blocker { public Vector3 Centre; public float Radius; public string Owner; }
        private static readonly List<Blocker> _static = new List<Blocker>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _static.Clear();

        public static bool IsBlocked(Cell c)
        {
            var p = WalkLattice.ToWorld(c);
            if (BoardSpace.Flat(p) > BoardSpace.BoardRadius + 4f) return true;
            foreach (var b in _static)
                if (BoardSpace.Flat(p - b.Centre) < b.Radius) return true;
            return false;
        }

        /// <summary>Blocks every cell within <paramref name="radius"/> metres of <paramref name="centre"/> (mountain peaks, the dance fire and drums).</summary>
        public static void AddStaticBlocker(Vector3 centre, float radius, string owner) =>
            _static.Add(new Blocker { Centre = centre, Radius = radius, Owner = owner });

        public static void RemoveStaticBlockers(string owner) => _static.RemoveAll(b => b.Owner == owner);

        /// <summary>Cells from <paramref name="from"/> (exclusive) to <paramref name="to"/> (inclusive), or null if none. F-0 stub: always null.</summary>
        public static List<Cell> FindPath(Cell from, Cell to, int maxNodes = 20000) => null;
    }
}
