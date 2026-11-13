using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The walking lattice of REFACTOR_SPEC 2.D: a triangular lattice with 5 m steps, origin at the board centre (centre
    /// of hex 30), aligned with the hex board. Cell (i, j) is at X = 5 * (sqrt 3 / 2) * i, Z = 5 * (i / 2 + j).
    /// Heading h (0..5) has yaw 60 * h (0 = north, clockwise). Owner: package P.
    /// F-0 implements the pure geometry (it is fully defined by the spec); P owns and may extend it.
    /// </summary>
    public static class WalkLattice
    {
        public const float Step = 5f;
        private static readonly float ColumnX = Step * Mathf.Sqrt(3f) * 0.5f;

        /// <summary>A lattice cell (axial coordinates).</summary>
        [Serializable]
        public struct Cell : IEquatable<Cell>
        {
            public int I, J;
            public Cell(int i, int j) { I = i; J = j; }
            public bool Equals(Cell o) => I == o.I && J == o.J;
            public override bool Equals(object obj) => obj is Cell c && Equals(c);
            public override int GetHashCode() => unchecked(I * 73856093 ^ J * 19349663);
            public static bool operator ==(Cell a, Cell b) => a.Equals(b);
            public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
            public static Cell operator +(Cell a, Cell b) => new Cell(a.I + b.I, a.J + b.J);
            public static Cell operator -(Cell a, Cell b) => new Cell(a.I - b.I, a.J - b.J);
            public static Cell operator *(Cell a, int k) => new Cell(a.I * k, a.J * k);
            public override string ToString() => "(" + I + ", " + J + ")";
        }

        private static readonly Cell[] Dirs = { new Cell(0, 1), new Cell(1, 0), new Cell(1, -1), new Cell(0, -1), new Cell(-1, 0), new Cell(-1, 1) };

        /// <summary>Ground position of a cell (y = BoardSpace.GroundY).</summary>
        public static Vector3 ToWorld(Cell c) => new Vector3(ColumnX * c.I, BoardSpace.GroundY, Step * (c.I * 0.5f + c.J));

        /// <summary>Nearest cell to a world point (y ignored).</summary>
        public static Cell ToCell(Vector3 p)
        {
            // Axial coordinates of the point, then cube rounding (the lattice is the centre grid of a hex tiling).
            double q = p.x / ColumnX;
            double r = p.z / Step - q * 0.5;
            double x = q, z = r, y = -x - z;
            double rx = Math.Round(x), ry = Math.Round(y), rz = Math.Round(z);
            double dx = Math.Abs(rx - x), dy = Math.Abs(ry - y), dz = Math.Abs(rz - z);
            if (dx > dy && dx > dz) rx = -ry - rz;
            else if (dy <= dz) rz = -rx - ry;
            return new Cell((int)rx, (int)rz);
        }

        /// <summary>One step in heading h (taken modulo 6).</summary>
        public static Cell Dir(int heading) => Dirs[Mod6(heading)];

        /// <summary>Yaw of heading h in degrees (0 = north, clockwise).</summary>
        public static float Yaw(int heading) => Mod6(heading) * 60f;

        /// <summary>Heading nearest to a yaw in degrees.</summary>
        public static int HeadingOf(float yaw) => Mod6(Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 60f));

        private static int Mod6(int h) => ((h % 6) + 6) % 6;
    }
}
