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
        /// <summary>Grazing land: yields Sheep.</summary>
        Pasture = 5,
    }

    public static class TerrainInfo
    {
        /// <summary>Number of terrains (length of per-terrain tables such as EconomyTuning.GrowTicks).</summary>
        public const int Count = 6;

        /// <summary>Raw ware a terrain yields (Forest Wood, Hills Clay, Fields Grain, Pasture Sheep, Mountain Stone), or null for Meadow.</summary>
        public static Ware? RawWare(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return Ware.Wood;
                case Terrain.Hills: return Ware.Clay;
                case Terrain.Fields: return Ware.Grain;
                case Terrain.Pasture: return Ware.Sheep;
                case Terrain.Mountain: return Ware.Stone;
                default: return null;
            }
        }

        /// <summary>The terrain that yields a raw ware (inverse of RawWare), or Meadow for worked wares.</summary>
        public static Terrain TerrainOf(Ware w)
        {
            switch (w)
            {
                case Ware.Wood: return Terrain.Forest;
                case Ware.Clay: return Terrain.Hills;
                case Ware.Grain: return Terrain.Fields;
                case Ware.Sheep: return Terrain.Pasture;
                case Ware.Stone: return Terrain.Mountain;
                default: return Terrain.Meadow;
            }
        }

        /// <summary>Lower-case terrain name for sentences ("forest", "hills").</summary>
        public static string DisplayName(Terrain t) => t.ToString().ToLowerInvariant();

        /// <summary>True for mined terrains (Hills, Mountain): young stock is not drawn.</summary>
        public static bool IsMined(Terrain t) => t == Terrain.Hills || t == Terrain.Mountain;

        /// <summary>True for terrains that receive production numbers (every terrain except Meadow, Pasture included).</summary>
        public static bool IsResource(Terrain t) => t != Terrain.Meadow;
    }
}
