using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// Plot ids and geometry (REFACTOR_SPEC 4.1, 4.2). Pure. Plot id = corner * 9 + sector * 3 + slot, where sector
    /// (0..2) is the index of the hex in the corner's adjacent hex ids sorted ascending. Plot ids exist for every
    /// interior corner whether or not a settlement stands there. Positions are in metres, X east, Z north.
    /// </summary>
    public static class PlotLayout
    {
        public const int PlotsPerCorner = 9, SlotsPerSector = 3, CitySlot = 2;

        public const double PlotRadius = 5, StorehouseRadius = 3, StockPatchRadius = 6, DanceCircleRadius = 7, StepLength = 5;

        /// <summary>Distance from the corner along the spoke to slots 0 and 1 (their spoke point), and to slot 2.</summary>
        public const double SideSlotSpoke = 30, CitySlotSpoke = 45;

        /// <summary>Sideways offset of slots 0 (left) and 1 (right) from the spoke: 5 * sqrt(3) m.</summary>
        public static readonly double SideOffset = 5 * Math.Sqrt(3.0);

        public static int PlotId(int corner, int sector, int slot) => corner * PlotsPerCorner + sector * SlotsPerSector + slot;

        public static int CornerOf(int plot) => plot / PlotsPerCorner;

        public static int SectorOf(int plot) => plot % PlotsPerCorner / SlotsPerSector;

        public static int SlotOf(int plot) => plot % SlotsPerSector;

        /// <summary>Highest plot id + 1 on a topology.</summary>
        public static int PlotCount(BoardTopology t) => t == null ? 0 : t.CornerCount * PlotsPerCorner;

        /// <summary>True for a corner where three board hexes meet (only those may hold a settlement).</summary>
        public static bool IsSettlementCorner(BoardTopology t, int corner) =>
            t != null && t.IsCorner(corner) && t.VertexHexes(corner).Count == 3;

        /// <summary>True when the plot id belongs to a settlement corner and its slot and sector are in range.</summary>
        public static bool IsValidPlot(BoardTopology t, int plot) =>
            plot >= 0 && plot < PlotCount(t) && IsSettlementCorner(t, CornerOf(plot));

        /// <summary>The hex of a corner's sector (adjacent hex ids ascending), or -1.</summary>
        public static int SectorHex(BoardTopology t, int corner, int sector)
        {
            if (t == null || !t.IsCorner(corner) || sector < 0) return -1;
            var hs = SortedHexes(t, corner);
            return sector < hs.Length ? hs[sector] : -1;
        }

        /// <summary>The sector of a hex at a corner, or -1 if the hex is not adjacent.</summary>
        public static int SectorOfHex(BoardTopology t, int corner, int hex)
        {
            if (t == null || !t.IsCorner(corner)) return -1;
            return Array.IndexOf(SortedHexes(t, corner), hex);
        }

        /// <summary>The hex whose sector holds the plot, or -1.</summary>
        public static int PlotHex(BoardTopology t, int plot) =>
            plot < 0 || t == null ? -1 : SectorHex(t, CornerOf(plot), SectorOf(plot));

        public static double PlotX(BoardTopology t, int plot)
        {
            Centre(t, plot, out double x, out _);
            return x;
        }

        public static double PlotZ(BoardTopology t, int plot)
        {
            Centre(t, plot, out _, out double z);
            return z;
        }

        /// <summary>Facing in degrees, 0 = north, clockwise: slots 0 and 1 face their spoke point, slot 2 faces the corner.</summary>
        public static double PlotYaw(BoardTopology t, int plot)
        {
            if (!Spoke(t, plot, out double cx, out double cz, out double bx, out double bz)) return 0;
            Centre(t, plot, out double px, out double pz);
            double tx, tz;
            if (SlotOf(plot) == CitySlot) { tx = cx; tz = cz; }
            else { tx = cx + SideSlotSpoke * bx; tz = cz + SideSlotSpoke * bz; }
            return Yaw(tx - px, tz - pz);
        }

        /// <summary>
        /// Local path from the storehouse (the corner) to the plot centre as x0,z0,x1,z1,...: slots 0 and 1
        /// [corner, spoke point at 30 m, plot]; slot 2 [corner, plot]. Empty for an invalid plot.
        /// </summary>
        public static double[] PathPoints(BoardTopology t, int plot)
        {
            if (!Spoke(t, plot, out double cx, out double cz, out double bx, out double bz)) return new double[0];
            Centre(t, plot, out double px, out double pz);
            if (SlotOf(plot) == CitySlot) return new[] { cx, cz, px, pz };
            return new[] { cx, cz, cx + SideSlotSpoke * bx, cz + SideSlotSpoke * bz, px, pz };
        }

        /// <summary>Length of the local path in 5 m steps: 8 for slots 0 and 1, 9 for slot 2.</summary>
        public static int LocalSteps(int plot) => SlotOf(plot) == CitySlot ? 9 : 8;

        /// <summary>Yaw in degrees (0 = north, clockwise) of a direction (dx east, dz north); 0 for a zero vector.</summary>
        public static double Yaw(double dx, double dz)
        {
            if (dx == 0 && dz == 0) return 0;
            double y = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (y < 0) y += 360;
            if (y >= 360) y -= 360;
            return Math.Round(y, 9);
        }

        // ---- internals ----

        private static readonly Dictionary<(int radius, int corner), int[]> SortedCache = new Dictionary<(int, int), int[]>();

        private static int[] SortedHexes(BoardTopology t, int corner)
        {
            lock (SortedCache)
            {
                if (SortedCache.TryGetValue((t.Radius, corner), out var cached)) return cached;
                var list = new List<int>(t.VertexHexes(corner));
                list.Sort();
                var arr = list.ToArray();
                SortedCache[(t.Radius, corner)] = arr;
                return arr;
            }
        }

        /// <summary>Corner position and unit spoke towards the sector's hex centre. False for an invalid plot.</summary>
        private static bool Spoke(BoardTopology t, int plot, out double cx, out double cz, out double bx, out double bz)
        {
            cx = cz = bx = bz = 0;
            if (t == null || plot < 0 || plot >= PlotCount(t)) return false;
            int corner = CornerOf(plot);
            int hex = SectorHex(t, corner, SectorOf(plot));
            if (hex < 0) return false;
            cx = t.VertexX(corner);
            cz = t.VertexZ(corner);
            double dx = t.HexX(hex) - cx, dz = t.HexZ(hex) - cz;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len <= 0) return false;
            bx = dx / len;
            bz = dz / len;
            return true;
        }

        private static void Centre(BoardTopology t, int plot, out double x, out double z)
        {
            if (!Spoke(t, plot, out double cx, out double cz, out double bx, out double bz))
            {
                x = z = 0;
                return;
            }
            int slot = SlotOf(plot);
            if (slot == CitySlot)
            {
                x = cx + CitySlotSpoke * bx;
                z = cz + CitySlotSpoke * bz;
                return;
            }
            // left = (-b.z, b.x): 90 degrees counter-clockwise seen from above.
            double lx = -bz, lz = bx;
            double side = slot == 0 ? SideOffset : -SideOffset;
            x = cx + SideSlotSpoke * bx + side * lx;
            z = cz + SideSlotSpoke * bz + side * lz;
        }
    }
}
