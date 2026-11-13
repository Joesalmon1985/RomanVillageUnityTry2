using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>One piece of camp dressing: what it is, where it stands, which way it faces and how far it reaches.</summary>
    public struct CampProp
    {
        public string Kind;
        public Vector3 Position;
        public float Yaw;
        /// <summary>Footprint radius in metres (everything the prop draws stays inside it).</summary>
        public float Radius;
        public CampProp(string kind, Vector3 position, float yaw, float radius) { Kind = kind; Position = position; Yaw = yaw; Radius = radius; }
    }

    /// <summary>
    /// Where things stand at the home camp (REFACTOR_SPEC 2.E, 8.3, appendix B), derived from the board alone, so any
    /// package can build one: <c>new CampLayout(state.Board)</c>. Owner: package D.
    ///
    /// Home corner = <c>Board.StartCorners[0]</c>; the hearth is the corner itself (V draws the store pile and hearth).
    /// Dance circle at the reserved plot (<c>Board.ReservedPlots[0]</c>): fire at the plot centre, four drums on a 4 m
    /// circle on the half towards the corner, the dancer 6 m from the fire on the corner side facing it (drums 1-4 left
    /// to right in front), Sol the drummer 5.5 m beyond the fire facing the dancer. Spawn: 4 lattice steps from the
    /// corner along the Forest spoke, facing the prince's start 4 steps further on (on a hex-edge line, 20 m away).
    /// Mira stands 15.5 m from the corner in the open wedge just beyond that hex edge (out of the forest), facing the
    /// spawn, so her Ward duel is staged on open ground with the camp behind her.
    ///
    /// Camp dressing (<see cref="Dressing"/>) stands only in the six wedges between a spoke (local path) and the next hex
    /// edge (main road) at 9-21 m from the corner, where every plot, path, road line and stock patch is far away; the
    /// test <c>DanceTests</c> checks every prop with <see cref="Obstruction"/>.
    /// </summary>
    public sealed class CampLayout
    {
        public const float DrumRadius = 4f, DancerDistance = 6f, DrummerDistance = 5.5f;
        /// <summary>Sol stands a little right of the line through the fire, so the flames do not hide him from the dancer.</summary>
        public const float DrummerAngle = 14f;
        /// <summary>Dancers' home spots (by the log seats) and their dancing ring.</summary>
        public const float SeatRadius = 6.5f, DanceRingRadius = 4.9f;
        /// <summary>Blocker radii registered with <see cref="WalkGrid"/> (cells within them cannot be entered).</summary>
        public const float FireBlockRadius = 2.5f, DrumBlockRadius = 1.3f;
        /// <summary>Clearances used by <see cref="Obstruction"/> (metres beyond a plot's, patch's or corner's own radius).</summary>
        public const float PlotMargin = 2f, StockMargin = 2f, EdgeMargin = 3f, PathMargin = 2.5f, HomeCornerClear = 7f, CornerClear = 4f;

        public int HomeCorner { get; }
        /// <summary>The home storehouse and hearth (the corner itself, on the ground).</summary>
        public Vector3 Hearth { get; }
        public int ReservedPlot { get; }
        public Vector3 DanceCircleCentre { get; }
        public Vector3 DancerSpot { get; }
        /// <summary>The dancer faces the fire.</summary>
        public float DancerYaw { get; }
        /// <summary>Unit vectors of the dancer's view: <see cref="Forward"/> from the dancer to the fire, <see cref="Right"/> to its right.</summary>
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public Vector3 MiraSpot { get; }
        /// <summary>Mira faces the spawn (the Ward duel is staged along that line).</summary>
        public float MiraYaw { get; }
        public Vector3 DrummerSpot { get; }
        /// <summary>Sol faces the fire and the dancer.</summary>
        public float DrummerYaw { get; }
        /// <summary>Ground marks of the ending choice: left of the fire = "Fire leap", right = "Spirit call" (seen from the dancer).</summary>
        public Vector3 FireLeapMark => DanceCircleCentre - Right * 2.4f;
        public Vector3 SpiritCallMark => DanceCircleCentre + Right * 2.4f;
        public Cell Spawn { get; }
        public int SpawnHeading { get; }
        public Cell PrinceStart { get; }
        /// <summary>The home camp's dressing: hide tents, lean-to, drying racks, totems, wood pile, and where the camp's figures stand.</summary>
        public IReadOnlyList<CampProp> Dressing => _dressing;
        /// <summary>
        /// Ground the terrain should keep free of trees, crops, rocks and mounds, as circles (x, z, radius), the same
        /// format as <c>TerrainBuilder.Build</c>'s clearings: the dance circle (8.5 m), every dressing prop (its footprint
        /// plus 1.5 m), Mira's duel ground (Mira to the duel camera), the spawn, the prince's walk from his start to the
        /// spawn and the sightline from the spawn to Mira. Package V's terrain honours them (appendix B, B.1).
        /// </summary>
        public IReadOnlyList<Vector3> Clearings => _clearings;

        private readonly List<CampProp> _dressing = new List<CampProp>();
        private readonly List<Vector3> _clearings = new List<Vector3>();
        private readonly Vector3[] _spoke = new Vector3[3];

        public CampLayout(Board board)
        {
            var t = board.Topology;
            HomeCorner = board.StartCorners != null && board.StartCorners.Length > 0 ? board.StartCorners[0] : 0;
            Hearth = BoardSpace.Vertex(t, HomeCorner);
            for (int k = 0; k < 3; k++)
            {
                int h = PlotLayout.SectorHex(t, HomeCorner, k);
                var d = h >= 0 ? BoardSpace.Hex(t, h) - Hearth : Vector3.forward;
                d.y = 0;
                _spoke[k] = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            }

            // Forest sector of the home corner (the first sector if there is no forest).
            int forestSector = 0;
            for (int k = 0; k < 3; k++)
            {
                int h = PlotLayout.SectorHex(t, HomeCorner, k);
                if (h >= 0 && board.HexTerrain[h] == World.Sim.Terrain.Forest) { forestSector = k; break; }
            }
            var corner = WalkLattice.ToCell(Hearth);
            int hf = WalkLattice.HeadingOf(BoardSpace.YawOf(_spoke[forestSector]));
            Spawn = corner + WalkLattice.Dir(hf) * 4;
            SpawnHeading = (hf + 4) % 6;
            PrinceStart = Spawn + WalkLattice.Dir(SpawnHeading) * 4;
            _forestYaw = BoardSpace.YawOf(_spoke[forestSector]);
            var toPrince = WalkLattice.ToWorld(PrinceStart) - Hearth; toPrince.y = 0;
            _princeSide = Mathf.DeltaAngle(_forestYaw, BoardSpace.YawOf(toPrince)) >= 0f ? 1 : -1;
            // Mira: in the open wedge just beyond the prince's hex edge (out of the forest), 15.5 m from the corner, in
            // view at the spawn (about 25 m away, 20 degrees left of ahead) and facing it, so the Ward duel is staged on
            // open ground towards the spawn.
            MiraSpot = W(90f, 15.5f);
            MiraYaw = BoardSpace.YawOf(WalkLattice.ToWorld(Spawn) - MiraSpot);

            ReservedPlot = board.ReservedPlots != null && board.ReservedPlots.Length > 0 ? board.ReservedPlots[0] : -1;
            DanceCircleCentre = ReservedPlot >= 0
                ? new Vector3((float)PlotLayout.PlotX(t, ReservedPlot), BoardSpace.GroundY, (float)PlotLayout.PlotZ(t, ReservedPlot))
                : Hearth + _spoke[0] * 45f;
            var toCorner = Hearth - DanceCircleCentre; toCorner.y = 0;
            var u = toCorner.sqrMagnitude > 1e-4f ? toCorner.normalized : Vector3.back;
            DancerSpot = DanceCircleCentre + u * DancerDistance;
            Forward = -u;
            Right = new Vector3(Forward.z, 0, -Forward.x);
            DancerYaw = BoardSpace.YawOf(Forward);
            DrummerSpot = CirclePoint(DrummerAngle, DrummerDistance);
            DrummerYaw = BoardSpace.YawOf(DancerSpot - DrummerSpot);

            BuildDressing();
            BuildClearings();
        }

        /// <summary>Drum 1-4, left to right as seen from <see cref="DancerSpot"/>, on the near half of the drum circle.</summary>
        public Vector3 DrumPosition(int pad1to4)
        {
            float a = (-67.5f + 45f * (Mathf.Clamp(pad1to4, 1, 4) - 1)) * Mathf.Deg2Rad;
            return DanceCircleCentre + (-Forward * Mathf.Cos(a) + Right * Mathf.Sin(a)) * DrumRadius;
        }

        /// <summary>A point on the dance circle at angle <paramref name="thetaDeg"/> from <see cref="Forward"/> (clockwise seen from above).</summary>
        public Vector3 CirclePoint(float thetaDeg, float radius)
        {
            float a = thetaDeg * Mathf.Deg2Rad;
            return DanceCircleCentre + (Forward * Mathf.Cos(a) + Right * Mathf.Sin(a)) * radius;
        }

        /// <summary>Unit direction from the home corner along sector 0-2's spoke (towards its hex centre).</summary>
        public Vector3 SpokeDir(int sector) => _spoke[Mathf.Clamp(sector, 0, 2)];

        // ------------------------------------------------------------------ dressing

        private float _forestYaw; private int _princeSide = 1;

        /// <summary>
        /// A point <paramref name="r"/> metres from the corner at <paramref name="relDeg"/> degrees from the Forest spoke,
        /// measured towards the prince's hex edge (so 30 = the forest wedge on the prince's line, 90 = the next sector's
        /// wedge beyond that edge, -30 = the forest wedge on the other side, ...). Wedge centres are at odd multiples of 30.
        /// </summary>
        private Vector3 W(float relDeg, float r, float lateral = 0f)
        {
            var d = BoardSpace.DirOf(_forestYaw + _princeSide * relDeg);
            var across = new Vector3(d.z, 0, -d.x);
            return Hearth + d * r + across * lateral;
        }

        private void Add(string kind, Vector3 p, Vector3 faceTowards, float radius)
        {
            var f = faceTowards - p; f.y = 0;
            _dressing.Add(new CampProp(kind, p, BoardSpace.YawOf(f), radius));
        }

        /// <summary>
        /// Six wedges between a spoke (local path) and a hex edge (main road) round the corner. The forest wedges hold
        /// only the wood pile (the forest's trees stand there); the duel wedge just beyond the prince's hex edge holds Mira
        /// with a drying rack behind her; tents, a lean-to, a rack and totems fill the rest; two totems flank the way into
        /// the dance circle.
        /// </summary>
        private void BuildDressing()
        {
            var h0 = Hearth;
            // Tents and the lean-to face the hearth; the camp is a ring of shelters round the fire at the corner.
            Add("tent", W(150f, 13f), h0, 2.4f);
            Add("tent", W(-90f, 13f), h0, 2.4f);
            Add("leanto", W(-150f, 12.5f), h0, 2.3f);
            Add("tent", W(-150f, 19.5f), h0, 2.4f);
            // Drying racks side on to the hearth (a rack's long axis is across its facing): one behind Mira, one by a tent.
            Add("rack", W(90f, 21.5f), h0, 2.0f);
            Add("rack", W(-90f, 19.5f), h0, 2.0f);
            Add("woodpile", W(-30f, 14f), h0, 1.5f);
            Add("totem", W(150f, 20f), h0, 0.8f);
            // A pair of totems flanking the way into the dance circle (off the slot-2 path along the spoke).
            var gate = DanceCircleCentre - Forward * ((float)PlotLayout.DanceCircleRadius + 1.2f);
            Add("totem", gate - Right * 3.6f, gate, 0.8f);
            Add("totem", gate + Right * 3.6f, gate, 0.8f);

            // The camp's figures: a hide scraper at the rack behind Mira, a fire tender by the lean-to, a child by a tent.
            Add("figure", W(90f, 19f, 1.5f), W(90f, 21.5f), 0.6f);
            Add("figure", W(-150f, 9.5f, 0.8f), h0, 0.6f);
            Add("figure", W(150f, 9.5f, 0.8f), W(150f, 13f), 0.6f);
        }

        private void Clear(Vector3 p, float r) => _clearings.Add(new Vector3(p.x, p.z, r));

        private void ClearLine(Vector3 a, Vector3 b, float r)
        {
            float len = BoardSpace.Flat(b - a);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / r));
            for (int i = 0; i <= n; i++) Clear(Vector3.Lerp(a, b, i / (float)n), r);
        }

        private void BuildClearings()
        {
            Clear(DanceCircleCentre, (float)PlotLayout.DanceCircleRadius + 1.5f);
            foreach (var p in _dressing) Clear(p.Position, p.Radius + 1.5f);
            // Mira's duel: the player stands 5.5 m in front of her and the camera 3 m further back (WardDuelEncounter).
            var spawn = WalkLattice.ToWorld(Spawn);
            var toSpawn = spawn - MiraSpot; toSpawn.y = 0; toSpawn.Normalize();
            ClearLine(MiraSpot - toSpawn * 2f, MiraSpot + toSpawn * 9.5f, 4.5f);
            ClearLine(spawn, MiraSpot, 3f);
            ClearLine(spawn, WalkLattice.ToWorld(PrinceStart), 4f);
            Clear(spawn, 6f);
        }

        // ------------------------------------------------------------------ clearance

        /// <summary>
        /// Null when a disc of <paramref name="radius"/> at <paramref name="p"/> keeps clear of every plot (5 m + 2 m), stock
        /// patch (6 m + 2 m), hex-edge line (main road, 3 m), local path of the home camp (2.5 m), the home corner (7 m),
        /// any other corner (4 m) and the dance circle (7 m); otherwise a sentence naming the first thing it touches.
        /// The dance circle's own parts are checked with <paramref name="insideCircle"/> = true (they must stay within it).
        /// </summary>
        public static string Obstruction(Board board, Vector3 p, float radius, bool insideCircle = false)
        {
            var t = board.Topology;
            p.y = 0;
            int home = board.StartCorners != null && board.StartCorners.Length > 0 ? board.StartCorners[0] : 0;
            int reserved = board.ReservedPlots != null && board.ReservedPlots.Length > 0 ? board.ReservedPlots[0] : -1;
            Vector3 Flat(Vector3 v) { v.y = 0; return v; }

            if (reserved >= 0)
            {
                var c = new Vector3((float)PlotLayout.PlotX(t, reserved), 0, (float)PlotLayout.PlotZ(t, reserved));
                float d = (p - c).magnitude;
                if (insideCircle) { if (d + radius > PlotLayout.DanceCircleRadius + 0.01f) return "outside the dance circle (" + d.ToString("0.0") + " m from its centre)"; }
                else if (d < PlotLayout.DanceCircleRadius + radius) return "on the dance circle";
            }
            for (int c = 0; c < t.CornerCount; c++)
            {
                var cp = Flat(BoardSpace.Vertex(t, c));
                float d = (p - cp).magnitude;
                if (d > 200f) continue;
                float clear = c == home ? HomeCornerClear : CornerClear;
                if (d < clear + radius) return "too near corner " + c + " (" + d.ToString("0.0") + " m)";
                if (!PlotLayout.IsSettlementCorner(t, c)) continue;
                for (int k = 0; k < PlotLayout.PlotsPerCorner; k++)
                {
                    int plot = PlotLayout.PlotId(c, k / 3, k % 3);
                    if (plot == reserved) continue;
                    var pc = new Vector3((float)PlotLayout.PlotX(t, plot), 0, (float)PlotLayout.PlotZ(t, plot));
                    if ((p - pc).magnitude < PlotLayout.PlotRadius + PlotMargin + radius) return "on plot " + plot;
                }
                if (c != home) continue;
                for (int k = 0; k < PlotLayout.PlotsPerCorner; k++)
                {
                    int plot = PlotLayout.PlotId(c, k / 3, k % 3);
                    if (plot == reserved && insideCircle) continue; // the circle sits at the end of its own path
                    var pts = PlotLayout.PathPoints(t, plot);
                    for (int i = 0; i + 3 < pts.Length; i += 2)
                    {
                        var a = new Vector3((float)pts[i], 0, (float)pts[i + 1]);
                        var b = new Vector3((float)pts[i + 2], 0, (float)pts[i + 3]);
                        if (BoardSpace.DistanceToSegment(p, a, b) < PathMargin + radius) return "on the local path to plot " + plot;
                    }
                }
            }
            for (int h = 0; h < t.HexCount; h++)
            {
                var hc = Flat(BoardSpace.Hex(t, h));
                if ((p - hc).magnitude < PlotLayout.StockPatchRadius + StockMargin + radius) return "on the stock patch of hex " + h;
            }
            for (int e = 0; e < t.HexEdgeCount; e++)
            {
                BoardSpace.HexEdgeEnds(t, e, out var a, out var b);
                if (BoardSpace.DistanceToSegment(p, Flat(a), Flat(b)) < EdgeMargin + radius) return "on the hex-edge line " + e;
            }
            return null;
        }
    }
}
