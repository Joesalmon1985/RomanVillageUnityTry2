namespace World.Sim
{
    /// <summary>
    /// Town plots (PRD_AGES 5.3, A.4; added by H1-0): outer slots 3 and 4 of each sector, 45 m along the spoke and 17 m to
    /// the left (slot 3) and right (slot 4). Ids continue after the existing range: 1350 + corner * 6 + sector * 2 + (slot - 3).
    /// Existing plot ids do not change. Pure; ids and geometry are real. H1-2 folds outer plots into PlotLayout's
    /// PlotX/PlotZ/PlotYaw/PathPoints, EconomyApi.Plot and placement (owns Sim/Board/PlotLayout.cs).
    /// </summary>
    public static class PlotLayoutOuter
    {
        public const int OuterBase = 1350;
        public const int OuterPerCorner = 6;
        public const int OuterPerSector = 2;
        public const int FirstOuterSlot = 3;
        public const int LastOuterSlot = 4;
        /// <summary>Metres along the spoke from the corner, and to each side.</summary>
        public const double OuterSpoke = 45, OuterSide = 17;

        /// <summary>The outer plot id, or -1 for a slot other than 3 or 4, a sector outside 0..2 or a negative corner.</summary>
        public static int OuterPlotId(int corner, int sector, int slot)
        {
            if (corner < 0 || sector < 0 || sector > 2 || slot < FirstOuterSlot || slot > LastOuterSlot) return -1;
            return OuterBase + corner * OuterPerCorner + sector * OuterPerSector + (slot - FirstOuterSlot);
        }

        public static bool IsOuter(int plot) => plot >= OuterBase;

        public static int CornerOf(int plot) => (plot - OuterBase) / OuterPerCorner;

        public static int SectorOf(int plot) => (plot - OuterBase) % OuterPerCorner / OuterPerSector;

        /// <summary>3 or 4.</summary>
        public static int SlotOf(int plot) => (plot - OuterBase) % OuterPerSector + FirstOuterSlot;

        /// <summary>Highest outer plot id + 1 on a topology.</summary>
        public static int PlotCount(BoardTopology t) => t == null ? OuterBase : OuterBase + t.CornerCount * OuterPerCorner;

        /// <summary>True for an outer plot of a settlement corner.</summary>
        public static bool IsValidOuterPlot(BoardTopology t, int plot) =>
            IsOuter(plot) && plot < PlotCount(t) && PlotLayout.IsSettlementCorner(t, CornerOf(plot));

        /// <summary>
        /// Centre of an outer plot: c + 45 b +/- 17 left, with b the unit spoke towards the sector's hex centre and left
        /// = (-b.z, b.x). False (0, 0) for an invalid plot.
        /// </summary>
        public static bool Centre(BoardTopology t, int plot, out double x, out double z)
        {
            x = z = 0;
            if (!IsValidOuterPlot(t, plot)) return false;
            int corner = CornerOf(plot);
            int hex = PlotLayout.SectorHex(t, corner, SectorOf(plot));
            if (hex < 0) return false;
            double cx = t.VertexX(corner), cz = t.VertexZ(corner);
            double bx = (t.HexX(hex) - cx) / BoardTopology.HexEdgeLength, bz = (t.HexZ(hex) - cz) / BoardTopology.HexEdgeLength;
            double lx = -bz, lz = bx;
            double side = SlotOf(plot) == FirstOuterSlot ? OuterSide : -OuterSide;
            x = cx + OuterSpoke * bx + side * lx;
            z = cz + OuterSpoke * bz + side * lz;
            return true;
        }

        /// <summary>Length of the local path of an outer plot in 5 m steps: 45 m along the spoke, then 17 m aside (62 m).</summary>
        public const int LocalSteps = 13;

        /// <summary>Two outer plots closer than this (centre to centre) cannot both be used: the first town keeps the ground.</summary>
        public const double MinSpacing = 15;

        /// <summary>Local path from the storehouse (the corner) to the plot centre: [corner, spoke point at 45 m, plot]. Empty for an invalid plot.</summary>
        public static double[] PathPoints(BoardTopology t, int plot)
        {
            if (!Centre(t, plot, out double px, out double pz)) return new double[0];
            int corner = CornerOf(plot);
            int hex = PlotLayout.SectorHex(t, corner, SectorOf(plot));
            double cx = t.VertexX(corner), cz = t.VertexZ(corner);
            double bx = (t.HexX(hex) - cx) / BoardTopology.HexEdgeLength, bz = (t.HexZ(hex) - cz) / BoardTopology.HexEdgeLength;
            return new[] { cx, cz, cx + OuterSpoke * bx, cz + OuterSpoke * bz, px, pz };
        }

        /// <summary>
        /// Outer plots of other settlement corners within <see cref="MinSpacing"/> of this one (ascending). They exist only
        /// where two camps share a hex from corners two edges apart (the first town keeps the ground).
        /// </summary>
        public static System.Collections.Generic.List<int> Rivals(BoardTopology t, int plot)
        {
            var list = new System.Collections.Generic.List<int>();
            if (!Centre(t, plot, out double x, out double z)) return list;
            int corner = CornerOf(plot);
            int hex = PlotLayout.SectorHex(t, corner, SectorOf(plot));
            foreach (int c in t.HexCorners(hex))
            {
                if (c == corner || !PlotLayout.IsSettlementCorner(t, c)) continue;
                for (int sec = 0; sec < 3; sec++)
                    for (int slot = FirstOuterSlot; slot <= LastOuterSlot; slot++)
                    {
                        int o = OuterPlotId(c, sec, slot);
                        if (!Centre(t, o, out double ox, out double oz)) continue;
                        double d = System.Math.Sqrt((ox - x) * (ox - x) + (oz - z) * (oz - z));
                        if (d < MinSpacing) list.Add(o);
                    }
            }
            list.Sort();
            return list;
        }

        /// <summary>Facing in degrees (0 = north, clockwise): towards the spoke point at 45 m.</summary>
        public static double Yaw(BoardTopology t, int plot)
        {
            if (!Centre(t, plot, out double px, out double pz)) return 0;
            int corner = CornerOf(plot);
            int hex = PlotLayout.SectorHex(t, corner, SectorOf(plot));
            double cx = t.VertexX(corner), cz = t.VertexZ(corner);
            double bx = (t.HexX(hex) - cx) / BoardTopology.HexEdgeLength, bz = (t.HexZ(hex) - cz) / BoardTopology.HexEdgeLength;
            return PlotLayout.Yaw(cx + OuterSpoke * bx - px, cz + OuterSpoke * bz - pz);
        }
    }
}
