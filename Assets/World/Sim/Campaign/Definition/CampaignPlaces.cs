using System;
using System.Collections.Generic;
using World.Sim.Eras;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Where a definition's surface places are on the island (PRD_CRYSTALS appendix C, Docs/CAMPAIGN_FORMAT.md
    /// "Places"). Pure and read-only. The island's terrain and start corners are the same on every seed, so these
    /// answers do not depend on the seed. Owned by I2 (Sim/Campaign/Definition/**).
    ///
    /// Anchors: a HomeCamp anchor is the player's start corner (Q, R are ignored); a MeadowHexCentre anchor is the
    /// centre of hex (Q, R); a RimCorner anchor is corner <see cref="AnchorDef.Corner"/>, or with Corner -1 the shore
    /// corner of the appendix C bearing rule for the element of the tower standing there. Every anchor is a cell of the
    /// 5 m lattice (<see cref="Lattice"/>). A surface marker's I, J are lattice offsets from its location's anchor cell.
    /// </summary>
    public static class CampaignPlaces
    {
        /// <summary>Appendix C: a site keeps within 9 m of its Meadow hex centre.</summary>
        public const double SiteRadius = 9.0;

        private static Board _board;
        private static readonly object Gate = new object();

        /// <summary>The fixed island (terrain, start corners, reserved plots). Numbers differ by seed; nothing here uses them.</summary>
        public static Board FixedBoard
        {
            get
            {
                lock (Gate) return _board ?? (_board = BoardGenerator.CreateMvpBoard(0));
            }
        }

        /// <summary>The tower corner of the appendix C bearing rule for an element (earth 17, fire 74, water 2, air 147 on the island).</summary>
        public static int RuleTowerCorner(BoardTopology t, Element e) => LandmarkApi.TowerCorners(t)[(int)e];

        /// <summary>The element of the tower standing at a location, or null when no tower does.</summary>
        public static Element? TowerElementAt(CampaignDefinition d, string locationId)
        {
            if (d?.Towers == null) return null;
            foreach (var t in d.Towers) if (t != null && t.Location == locationId) return t.Element;
            return null;
        }

        /// <summary>The rim corner of a RimCorner anchor (resolving -1 by the bearing rule); -1 for other anchors or none.</summary>
        public static int AnchorCorner(CampaignDefinition d, LocationDef loc, BoardTopology t)
        {
            if (loc?.Anchor == null || t == null || loc.Anchor.Kind != AnchorKind.RimCorner) return -1;
            if (loc.Anchor.Corner >= 0) return t.IsCorner(loc.Anchor.Corner) ? loc.Anchor.Corner : -1;
            var e = TowerElementAt(d, loc.Id);
            return e.HasValue ? RuleTowerCorner(t, e.Value) : -1;
        }

        /// <summary>The anchor's world point (metres, X east, Z north). False when the anchor names nothing on the island.</summary>
        public static bool TryAnchorPoint(CampaignDefinition d, LocationDef loc, Board b, out double x, out double z)
        {
            x = z = 0;
            var t = b?.Topology;
            if (loc?.Anchor == null || t == null) return false;
            switch (loc.Anchor.Kind)
            {
                case AnchorKind.HomeCamp:
                {
                    int c = b.StartCorners != null && b.StartCorners.Length > 0 ? b.StartCorners[0] : -1;
                    if (!t.IsCorner(c)) return false;
                    x = t.VertexX(c);
                    z = t.VertexZ(c);
                    return true;
                }
                case AnchorKind.MeadowHexCentre:
                {
                    int h = t.HexIndex(new HexCoord(loc.Anchor.Q, loc.Anchor.R));
                    if (h < 0) return false;
                    x = t.HexX(h);
                    z = t.HexZ(h);
                    return true;
                }
                case AnchorKind.RimCorner:
                {
                    int c = AnchorCorner(d, loc, t);
                    if (c < 0) return false;
                    x = t.VertexX(c);
                    z = t.VertexZ(c);
                    return true;
                }
                default: return false;
            }
        }

        /// <summary>The anchor's lattice cell.</summary>
        public static bool TryAnchorCell(CampaignDefinition d, LocationDef loc, Board b, out int i, out int j)
        {
            i = j = 0;
            if (!TryAnchorPoint(d, loc, b, out double x, out double z)) return false;
            Lattice.CellOf(x, z, out i, out j);
            return true;
        }

        /// <summary>The lattice cell of a surface marker (anchor cell plus the marker's offset). False inside interiors.</summary>
        public static bool TryMarkerCell(CampaignDefinition d, MarkerRef r, Board b, out int i, out int j)
        {
            i = j = 0;
            if (d == null || r.IsInterior) return false;
            var loc = d.Location(r.Location);
            if (loc == null || !TryAnchorCell(d, loc, b, out int ai, out int aj)) return false;
            if (string.IsNullOrEmpty(r.Marker)) { i = ai; j = aj; return true; }
            var m = loc.Markers?.Find(x => x != null && x.Id == r.Marker);
            if (m == null) return false;
            i = ai + m.I;
            j = aj + m.J;
            return true;
        }

        /// <summary>True when a world point lies inside or on the edge of a board hex (pointy-top hexes of 60 m edges).</summary>
        public static bool OnBoard(BoardTopology t, double x, double z)
        {
            if (t == null) return false;
            const double eps = 1e-6;
            double r = BoardTopology.HexEdgeLength, half = r * BoardTopology.Sqrt3 * 0.5;
            for (int h = 0; h < t.HexCount; h++)
            {
                double dx = Math.Abs(x - t.HexX(h)), dz = Math.Abs(z - t.HexZ(h));
                if (dx <= half + eps && dx / BoardTopology.Sqrt3 + dz <= r + eps) return true;
            }
            return false;
        }

        /// <summary>The distance in metres from a point to the nearest centre of a buildable plot, and that plot (-1 when none).</summary>
        public static double NearestPlot(BoardTopology t, double x, double z, out int plot)
        {
            plot = -1;
            double best = double.MaxValue;
            if (t == null) return best;
            int count = PlotLayout.PlotCount(t);
            for (int p = 0; p < count; p++)
            {
                if (!PlotLayout.IsValidPlot(t, p)) continue;
                double dx = PlotLayout.PlotX(t, p) - x, dz = PlotLayout.PlotZ(t, p) - z, dd = Math.Sqrt(dx * dx + dz * dz);
                if (dd < best) { best = dd; plot = p; }
            }
            return best;
        }

        /// <summary>The landmarks every new board has (hearths, dance circle, harbour), for collision checks.</summary>
        public static List<Landmark> SeededLandmarks()
        {
            var s = new WorldState { Board = BoardGenerator.CreateMvpBoard(0) };
            LandmarkLogic.Seed(s);
            return new List<Landmark>(s.Board.Landmarks);
        }
    }
}
