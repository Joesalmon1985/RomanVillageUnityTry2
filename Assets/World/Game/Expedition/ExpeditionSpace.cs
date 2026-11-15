using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Where the campaign's places are in the world (package I6). A surface marker is its lattice cell
    /// (<see cref="CampaignPlaces"/>); the home camp's markers follow the camp's own layout (Mira's fire, the sawyer).
    /// An interior cell comes from package I5's <see cref="InteriorSpace"/>; while that is still the I0 shell (every
    /// interior mapped onto the board centre) the cells are laid out by <see cref="StandInOrigin"/> far east of the
    /// island, so the expedition runs alone (see <see cref="ExpeditionStandIn"/>). Reads only.
    /// </summary>
    public static class ExpeditionSpace
    {
        /// <summary>Stand-in interiors (no package I5): world X of the first one, and the spacing between them.</summary>
        public const float StandInX = 2200f, StandInSpacing = 260f;
        /// <summary>How far in front of a thing (stone, pedestal, altar, witness, door) the party's leader stands.</summary>
        public const float PropStandOff = 1.9f;

        private static WorldState S => WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;

        /// <summary>True when package I5's interiors are in the build (the I0 shell puts every interior on the board centre).</summary>
        public static bool RealInteriors(CampaignDefinition d)
        {
            if (d?.Interiors == null || d.Interiors.Count == 0) return false;
            var a = InteriorSpace.ToWorld(d.Interiors[0].Id, 3, 2);
            var stub = WalkLattice.ToWorld(new Cell(3, 2));
            return BoardSpace.Flat(a - stub) > 1f;
        }

        /// <summary>The space a marker lies in: the interior's id, or "" on the surface.</summary>
        public static string SpaceOf(MarkerRef r) => r.IsInterior ? r.Interior : "";

        public static int InteriorIndex(CampaignDefinition d, string interiorId)
        {
            if (d?.Interiors == null) return -1;
            for (int i = 0; i < d.Interiors.Count; i++) if (d.Interiors[i].Id == interiorId) return i;
            return -1;
        }

        /// <summary>World origin of a stand-in interior's local cell (0, 0).</summary>
        public static Vector3 StandInOrigin(int index) => new Vector3(StandInX + StandInSpacing * Mathf.Max(0, index), 0f, 0f);

        /// <summary>World point (floor level) of an interior's local cell.</summary>
        public static Vector3 InteriorCell(CampaignDefinition d, string interiorId, int i, int j)
        {
            if (RealInteriors(d)) return InteriorSpace.ToWorld(interiorId, i, j);
            return StandInOrigin(InteriorIndex(d, interiorId)) + new Vector3((float)Lattice.X(i, j), 0f, (float)Lattice.Z(i, j));
        }

        /// <summary>The local cell of an interior marker. False when the marker is not there.</summary>
        public static bool TryInteriorCell(CampaignDefinition d, MarkerRef r, out int i, out int j)
        {
            i = j = 0;
            var m = d?.Interior(r.Interior);
            return m != null && InteriorMaps.TryFind(m, r.Marker, out i, out j);
        }

        /// <summary>The marker's definition (surface or interior), or null.</summary>
        public static MarkerDef MarkerOf(CampaignDefinition d, MarkerRef r)
        {
            if (d == null || r.IsEmpty) return null;
            if (r.IsInterior)
            {
                var m = d.Interior(r.Interior);
                if (m == null) return null;
                foreach (var md in InteriorMaps.Markers(m)) if (md.Id == r.Marker) return md;
                return null;
            }
            return d.Location(r.Location)?.Markers?.Find(x => x != null && x.Id == r.Marker);
        }

        /// <summary>The surface cell of a marker (anchor plus offset). False inside interiors or when unknown.</summary>
        public static bool TrySurfaceCell(WorldState s, MarkerRef r, out Cell cell)
        {
            cell = default;
            var d = s?.Campaign?.Definition;
            if (d == null || r.IsInterior) return false;
            if (!CampaignPlaces.TryMarkerCell(d, r, s.Board, out int i, out int j)) return false;
            cell = new Cell(i, j);
            return true;
        }

        /// <summary>
        /// World point of a marker: where the thing stands (surface: on the ground; interior: on the floor). The home
        /// camp's markers follow the camp's layout: "fire" is Mira's place by the hearth, "sawyer" the sawyer.
        /// </summary>
        public static Vector3 World(WorldState s, MarkerRef r)
        {
            var d = s?.Campaign?.Definition;
            if (d == null) return Vector3.zero;
            if (r.IsInterior)
            {
                if (TryInteriorCell(d, r, out int i, out int j)) return InteriorCell(d, r.Interior, i, j);
                return InteriorCell(d, r.Interior, 0, 0);
            }
            var loc = d.Location(r.Location);
            if (loc != null && loc.Anchor != null && loc.Anchor.Kind == AnchorKind.HomeCamp)
            {
                var home = HomePoint(s, r.Marker);
                if (home.HasValue) return Ground(home.Value);
            }
            if (TrySurfaceCell(s, r, out var cell)) return Ground(WalkLattice.ToWorld(cell));
            if (loc != null && CampaignPlaces.TryAnchorPoint(d, loc, s.Board, out double x, out double z)) return Ground(new Vector3((float)x, 0, (float)z));
            return Vector3.zero;
        }

        private static CampLayout s_camp;
        private static Board s_campBoard;

        /// <summary>The camp layout of the running world (cached per board).</summary>
        public static CampLayout Camp(WorldState s)
        {
            if (s_camp == null || !ReferenceEquals(s_campBoard, s.Board)) { s_camp = new CampLayout(s.Board); s_campBoard = s.Board; }
            return s_camp;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_camp = null; s_campBoard = null; }

        private static Vector3? HomePoint(WorldState s, string marker)
        {
            var camp = Camp(s);
            switch (marker)
            {
                case "fire": return camp.MiraSpot;
                case "sawyer":
                {
                    var pd = PrinceDirector.Instance;
                    if (pd != null && pd.Sawyer != null) return pd.Sawyer.transform.position;
                    return null;
                }
                default: return null;
            }
        }

        public static Vector3 Ground(Vector3 p) { p.y = PlayerWalker.GroundHeight(p); return p; }

        /// <summary>True for markers that hold a thing the party stands in front of, not on.</summary>
        public static bool IsProp(MarkerDef m) =>
            m != null && (m.Kind == MarkerKind.Pedestal || m.Kind == MarkerKind.Altar || m.Kind == MarkerKind.Inscription
                          || m.Kind == MarkerKind.Witness || m.Kind == MarkerKind.Lever || m.Kind == MarkerKind.Plinth);

        /// <summary>
        /// Where the leader stands at a marker when nothing says where she came from: on it, or for a thing one pace
        /// to the side that has floor (interior) or lies towards the location's safe point (surface).
        /// </summary>
        public static Vector3 DefaultStand(WorldState s, MarkerRef r)
        {
            var d = s?.Campaign?.Definition;
            var at = World(s, r);
            var m = MarkerOf(d, r);
            if (!IsProp(m)) return at;
            if (r.IsInterior)
            {
                var map = d.Interior(r.Interior);
                if (map != null && InteriorMaps.TryFind(map, r.Marker, out int i, out int j))
                    for (int k = 0; k < 6; k++)
                    {
                        int ni = i + InteriorMaps.Steps[2 * k], nj = j + InteriorMaps.Steps[2 * k + 1];
                        if (!InteriorMaps.IsFloor(map, ni, nj) || InteriorMaps.MarkerAt(map, ni, nj) != null) continue;
                        var n = InteriorCell(d, r.Interior, ni, nj);
                        return at + (n - at).normalized * PropStandOff;
                    }
                return at;
            }
            var loc = d.Location(r.Location);
            if (loc != null && loc.Anchor.Kind == AnchorKind.HomeCamp)
            {
                // By a witness at the camp: between the witness and the hearth.
                var hearth = Camp(s).Hearth;
                var dir = hearth - at; dir.y = 0;
                return Ground(at + (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward) * PropStandOff);
            }
            if (loc != null && !string.IsNullOrEmpty(loc.SafePoint))
            {
                var safe = World(s, MarkerRef.Surface(loc.Id, loc.SafePoint));
                var dir = safe - at; dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) return Ground(at + dir.normalized * PropStandOff);
            }
            return Ground(at + Vector3.forward * PropStandOff);
        }

        /// <summary>The surface point a way into an interior comes up at (the surface end of the chain of links), for the map and for rejoining.</summary>
        public static bool TrySurfaceEntrance(WorldState s, string interiorId, out MarkerRef entrance)
        {
            entrance = default;
            var c = s?.Campaign;
            var d = c?.Definition;
            if (d == null) return false;
            // Breadth-first over links from the interior to the first surface marker, open links first.
            for (int pass = 0; pass < 2; pass++)
            {
                var seen = new HashSet<string> { interiorId };
                var queue = new Queue<string>();
                queue.Enqueue(interiorId);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    var map = d.Interior(id);
                    if (map == null) continue;
                    foreach (var md in InteriorMaps.Markers(map))
                    {
                        if (string.IsNullOrEmpty(md.To) || !MarkerRef.TryParse(md.To, out var to)) continue;
                        if (pass == 0 && !LinkOpen(c, new MarkerRef(map.Location, map.Id, md.Id), to)) continue;
                        if (!to.IsInterior) { entrance = to; return true; }
                        if (seen.Add(to.Interior)) queue.Enqueue(to.Interior);
                    }
                }
            }
            return false;
        }

        /// <summary>A link between two markers is open: every OpenWhen of both ends and of a connection joining them is known.</summary>
        public static bool LinkOpen(CampaignState c, MarkerRef a, MarkerRef b)
        {
            var d = c?.Definition;
            if (d == null) return false;
            var ma = MarkerOf(d, a); var mb = MarkerOf(d, b);
            if (ma != null && !string.IsNullOrEmpty(ma.OpenWhen) && !c.Knows(ma.OpenWhen)) return false;
            if (mb != null && !string.IsNullOrEmpty(mb.OpenWhen) && !c.Knows(mb.OpenWhen)) return false;
            foreach (var con in d.Connections)
            {
                if (con == null || string.IsNullOrEmpty(con.OpenWhen)) continue;
                bool joins = (con.A == a && con.B == b) || (con.A == b && con.B == a);
                if (joins && !c.Knows(con.OpenWhen)) return false;
            }
            return true;
        }

        /// <summary>World points of an interior path given as i, j pairs.</summary>
        public static List<Vector3> CellPath(CampaignDefinition d, string interiorId, int[] cells)
        {
            var pts = new List<Vector3>();
            if (cells == null) return pts;
            for (int k = 0; k + 1 < cells.Length; k += 2) pts.Add(InteriorCell(d, interiorId, cells[k], cells[k + 1]));
            return pts;
        }

        /// <summary>True when a world point lies on a floor cell of the interior (nearest cell).</summary>
        public static bool OnFloor(CampaignDefinition d, string interiorId, Vector3 p)
        {
            var map = d?.Interior(interiorId);
            if (map == null) return false;
            var o = InteriorCell(d, interiorId, 0, 0);
            Lattice.CellOf(p.x - o.x, p.z - o.z, out int i, out int j);
            return InteriorMaps.IsFloor(map, i, j);
        }
    }
}
