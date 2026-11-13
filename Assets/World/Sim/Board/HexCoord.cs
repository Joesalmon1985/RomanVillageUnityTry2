using System;

namespace World.Sim
{
    /// <summary>Axial hex coordinate (pointy-top). s = -q - r.</summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;

        /// <summary>Distance in hex steps.</summary>
        public int DistanceTo(HexCoord o) =>
            (Math.Abs(Q - o.Q) + Math.Abs(R - o.R) + Math.Abs(S - o.S)) / 2;

        public int Ring => (Math.Abs(Q) + Math.Abs(R) + Math.Abs(S)) / 2;

        /// <summary>Axial neighbour directions, index 0..5 (E, NE, NW, W, SW, SE with +Z as north).</summary>
        public static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
            new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1),
        };

        public HexCoord Neighbour(int dir) => new HexCoord(Q + Directions[dir].Q, R + Directions[dir].R);

        public bool Equals(HexCoord o) => Q == o.Q && R == o.R;
        public override bool Equals(object obj) => obj is HexCoord o && Equals(o);
        public override int GetHashCode() => (Q * 397) ^ R;
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => "(" + Q + "," + R + ")";
    }

    public enum Terrain
    {
        Meadow = 0,
        Forest = 1,
        Hills = 2,
        Fields = 3,
        Mountain = 4,
        /// <summary>Grazing land. Yields no ware yet (the ware rework adds Sheep); it already gets production numbers.</summary>
        Pasture = 5,
    }

    public static class TerrainInfo
    {
        /// <summary>Raw ware a terrain yields, or null for Meadow and (for now) Pasture.</summary>
        public static Ware? RawWare(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return Ware.Timber;
                case Terrain.Hills: return Ware.Stone;
                case Terrain.Fields: return Ware.Grain;
                case Terrain.Mountain: return Ware.Ore;
                default: return null;
            }
        }

        /// <summary>True for terrains that receive production numbers (every terrain except Meadow, Pasture included).</summary>
        public static bool IsResource(Terrain t) => t != Terrain.Meadow;
    }
}
