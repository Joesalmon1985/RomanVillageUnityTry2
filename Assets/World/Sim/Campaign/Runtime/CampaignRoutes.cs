using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Places, routes and legs (5.5, 5.10). A place is a marker: on the surface its cell is the location's anchor cell
    /// plus the marker's offset; inside an interior it is the marker's own cell, and every interior is its own space
    /// (keyed by interior id: the location part of an interior ref only names the way in). Spaces are joined by links
    /// (CAMPAIGN_FORMAT 5): a connection's two ends, or a marker and the marker its To names; a link works both ways and
    /// is open when every OpenWhen of its markers and connections is known. A route is a shortest path (Dijkstra over the start, the goal and the portal ends; walking within a space costs lattice steps
    /// on the surface or the breadth-first path over open cells inside, a portal costs one step). Ties break by node
    /// order, which is fixed by the definition, so routes are deterministic.
    /// </summary>
    internal static partial class CampaignRuntime
    {
        /// <summary>The same place: same interior and marker inside, same location and marker on the surface.</summary>
        internal static bool Same(MarkerRef a, MarkerRef b)
        {
            if (a.IsInterior != b.IsInterior) return false;
            if (a.IsInterior) return string.Equals(a.Interior, b.Interior, StringComparison.Ordinal) && string.Equals(a.Marker ?? "", b.Marker ?? "", StringComparison.Ordinal);
            return string.Equals(a.Location ?? "", b.Location ?? "", StringComparison.Ordinal) && string.Equals(a.Marker ?? "", b.Marker ?? "", StringComparison.Ordinal);
        }

        /// <summary>The lattice cell of a location's anchor, as the format package places it (CampaignPlaces; the board centre when there is none).</summary>
        internal static void AnchorCell(WorldState s, CampaignDefinition d, LocationDef loc, out int i, out int j)
        {
            if (!CampaignPlaces.TryAnchorCell(d, loc, s?.Board, out i, out j)) { i = 0; j = 0; }
        }

        /// <summary>A place resolved to its space and cell.</summary>
        private struct Node
        {
            public MarkerRef Ref;
            /// <summary>"" = the surface, else the interior id.</summary>
            public string Space;
            public int I, J;
        }

        private static bool NodeOf(WorldState s, CampaignDefinition d, MarkerRef r, out Node n)
        {
            n = new Node { Ref = r, Space = "" };
            if (d == null || r.IsEmpty) return false;
            if (r.IsInterior)
            {
                var m = d.Interior(r.Interior);
                if (m == null || !InteriorMaps.TryFind(m, r.Marker, out int ii, out int jj)) return false;
                n.Space = r.Interior;
                n.I = ii;
                n.J = jj;
                return true;
            }
            var loc = d.Location(r.Location);
            if (loc == null) return false;
            AnchorCell(s, d, loc, out int ai, out int aj);
            var mk = string.IsNullOrEmpty(r.Marker) ? null : loc.Markers?.Find(x => x.Id == r.Marker);
            n.I = ai + (mk?.I ?? 0);
            n.J = aj + (mk?.J ?? 0);
            return true;
        }

        /// <summary>Steps between two places in one space (-1 = different spaces or no open path).</summary>
        private static int WalkSteps(CampaignState c, Node a, Node b, out int[] cells)
        {
            cells = null;
            if (!string.Equals(a.Space, b.Space, StringComparison.Ordinal)) return -1;
            if (a.Space.Length == 0) return Lattice.Distance(a.I, a.J, b.I, b.J);
            var m = c.Definition.Interior(a.Space);
            var path = InteriorMaps.Path(m, a.I, a.J, b.I, b.J, c.Knows);
            if (path.Length == 0) return -1;
            cells = path;
            return path.Length / 2 - 1;
        }

        /// <summary>The marker a reference names (surface or interior legend), null when none.</summary>
        internal static MarkerDef MarkerOf(CampaignDefinition d, MarkerRef r)
        {
            if (d == null || r.IsEmpty) return null;
            if (r.IsInterior)
            {
                var m = d.Interior(r.Interior);
                if (m?.Legend == null) return null;
                foreach (var kv in m.Legend) if (kv.Value != null && kv.Value.Id == r.Marker) return kv.Value;
                return null;
            }
            return d.Location(r.Location)?.Markers?.Find(x => x != null && x.Id == r.Marker);
        }

        /// <summary>
        /// Every open link, in definition order (CAMPAIGN_FORMAT 5, "Links"): the two ends of a connection, and every Exit,
        /// Stairs or Door marker with the marker its To names. A link joins its ends both ways and is open when every
        /// OpenWhen of its two markers and of every connection joining them is known. Interior references are filed under
        /// the interior's Location when it declares one.
        /// </summary>
        private static List<(MarkerRef a, MarkerRef b, bool twoWay)> Portals(CampaignState c)
        {
            var d = c.Definition;
            var pairs = new List<(MarkerRef a, MarkerRef b)>();
            void Add(MarkerRef a, MarkerRef b)
            {
                if (a.IsEmpty || b.IsEmpty || Same(a, b)) return;
                foreach (var (x, y) in pairs) if ((Same(x, a) && Same(y, b)) || (Same(x, b) && Same(y, a))) return;
                pairs.Add((a, b));
            }
            bool IsPortal(MarkerKind k) => k == MarkerKind.Exit || k == MarkerKind.Stairs || k == MarkerKind.Door;
            foreach (var con in d.Connections ?? new List<ConnectionDef>())
                if (con != null) Add(con.A, con.B);
            foreach (var loc in d.Locations ?? new List<LocationDef>())
                foreach (var mk in loc.Markers ?? new List<MarkerDef>())
                    if (mk != null && IsPortal(mk.Kind) && MarkerRef.TryParse(mk.To, out var to)) Add(MarkerRef.Surface(loc.Id, mk.Id), to);
            foreach (var m in d.Interiors ?? new List<InteriorDef>())
            {
                if (m?.Legend == null) continue;
                foreach (var kv in m.Legend)
                {
                    var md = kv.Value;
                    if (md == null || !IsPortal(md.Kind) || !MarkerRef.TryParse(md.To, out var to)) continue;
                    string loc = !string.IsNullOrEmpty(m.Location) ? m.Location : !to.IsInterior ? to.Location : "";
                    Add(new MarkerRef(loc, m.Id, md.Id), to);
                }
            }
            bool Open(string fact) => string.IsNullOrEmpty(fact) || c.Knows(fact);
            var list = new List<(MarkerRef, MarkerRef, bool)>();
            foreach (var (a, b) in pairs)
            {
                bool open = Open(MarkerOf(d, a)?.OpenWhen) && Open(MarkerOf(d, b)?.OpenWhen);
                foreach (var con in d.Connections ?? new List<ConnectionDef>())
                    if (con != null && ((Same(con.A, a) && Same(con.B, b)) || (Same(con.A, b) && Same(con.B, a))) && !Open(con.OpenWhen)) open = false;
                if (open) list.Add((a, b, true));
            }
            return list;
        }

        /// <summary>Shortest routes from one place to anything (built once per question; small graphs).</summary>
        private sealed class RouteMap
        {
            private readonly WorldState _s;
            private readonly CampaignState _c;
            private readonly List<Node> _nodes = new List<Node>();
            private readonly long[] _dist;
            private readonly int[] _prev;
            public readonly bool Ok;

            public RouteMap(WorldState s, CampaignState c, MarkerRef from)
            {
                _s = s;
                _c = c;
                var d = c.Definition;
                if (!NodeOf(s, d, from, out var start)) { _dist = new long[0]; _prev = new int[0]; return; }
                _nodes.Add(start);
                var pairs = new List<(int, int, bool)>();
                foreach (var (a, b, twoWay) in Portals(c))
                {
                    if (!NodeOf(s, d, a, out var na) || !NodeOf(s, d, b, out var nb)) continue;
                    int ia = IndexOf(na), ib = IndexOf(nb);
                    if (ia != ib) pairs.Add((ia, ib, twoWay));
                }
                int n = _nodes.Count;
                _dist = new long[n];
                _prev = new int[n];
                var done = new bool[n];
                for (int k = 0; k < n; k++) { _dist[k] = long.MaxValue; _prev[k] = -1; }
                _dist[0] = 0;
                for (int round = 0; round < n; round++)
                {
                    int u = -1;
                    for (int k = 0; k < n; k++)
                        if (!done[k] && _dist[k] != long.MaxValue && (u < 0 || _dist[k] < _dist[u])) u = k;
                    if (u < 0) break;
                    done[u] = true;
                    for (int v = 0; v < n; v++)
                    {
                        if (done[v]) continue;
                        int w = WalkSteps(c, _nodes[u], _nodes[v], out _);
                        foreach (var (pa, pb, twoWay) in pairs)
                            if ((pa == u && pb == v) || (twoWay && pa == v && pb == u)) { w = w < 0 ? 1 : Math.Min(w, 1); }
                        if (w < 0) continue;
                        if (_dist[u] + w < _dist[v]) { _dist[v] = _dist[u] + w; _prev[v] = u; }
                    }
                }
                Ok = true;
            }

            private int IndexOf(Node x)
            {
                for (int k = 0; k < _nodes.Count; k++)
                    if (_nodes[k].Space == x.Space && _nodes[k].I == x.I && _nodes[k].J == x.J && Same(_nodes[k].Ref, x.Ref)) return k;
                _nodes.Add(x);
                return _nodes.Count - 1;
            }

            /// <summary>Steps to a place (-1 = unreachable); via = the last node before the final walk.</summary>
            public int StepsTo(MarkerRef goal, out int via, out Node g)
            {
                via = -1;
                if (!Ok || !NodeOf(_s, _c.Definition, goal, out g)) { g = default; return -1; }
                long best = long.MaxValue;
                for (int k = 0; k < _nodes.Count; k++)
                {
                    if (_dist[k] == long.MaxValue) continue;
                    int w = WalkSteps(_c, _nodes[k], g, out _);
                    if (w < 0) continue;
                    if (_dist[k] + w < best) { best = _dist[k] + w; via = k; }
                }
                return best == long.MaxValue ? -1 : (int)Math.Min(int.MaxValue, best);
            }

            public int StepsTo(MarkerRef goal) => StepsTo(goal, out _, out _);

            /// <summary>The waypoints from the start (excluded) to the goal (included); null when unreachable.</summary>
            public List<MarkerRef> PathTo(MarkerRef goal)
            {
                if (StepsTo(goal, out int via, out _) < 0) return null;
                var chain = new List<MarkerRef>();
                for (int k = via; k > 0; k = _prev[k]) chain.Add(_nodes[k].Ref);
                chain.Reverse();
                chain.Add(goal);
                // Drop waypoints that are the place just reached (a portal end that is also the goal, or the start).
                var route = new List<MarkerRef>();
                var last = _nodes[0].Ref;
                foreach (var r in chain)
                    if (!Same(r, last)) { route.Add(r); last = r; }
                return route;
            }
        }

        /// <summary>Lattice steps of the shortest open route between two places, -1 when there is none.</summary>
        internal static int RouteSteps(WorldState s, CampaignState c, MarkerRef from, MarkerRef to) => new RouteMap(s, c, from).StepsTo(to);

        /// <summary>One leg between two consecutive waypoints: a walk within a space, or a portal crossing (one step).</summary>
        private static LegState MakeLeg(WorldState s, CampaignState c, MarkerRef from, MarkerRef to, int ticksPerStep)
        {
            var leg = new LegState { From = from, To = to, Ticks = Math.Max(1, ticksPerStep), Progress = 0, Cells = new int[0] };
            var d = c.Definition;
            if (!NodeOf(s, d, from, out var a) || !NodeOf(s, d, to, out var b)) return leg;
            int steps = WalkSteps(c, a, b, out var cells);
            if (steps < 0) return leg;   // a portal crossing
            leg.Ticks = Math.Max(1, steps * ticksPerStep);
            if (cells != null && a.Space.Length > 0) leg.Cells = cells;
            return leg;
        }

        /// <summary>The surface marker of a location used as a goal there: its inscription (a tower's door), else its safe point, else its first marker.</summary>
        internal static MarkerRef LocationGoal(LocationDef loc)
        {
            if (loc == null) return default;
            var mk = loc.Markers?.Find(m => m.Kind == MarkerKind.Inscription);
            string id = mk?.Id;
            if (string.IsNullOrEmpty(id)) id = loc.SafePoint;
            if (string.IsNullOrEmpty(id) && loc.Markers != null && loc.Markers.Count > 0) id = loc.Markers[0].Id;
            return MarkerRef.Surface(loc.Id, id ?? "");
        }

        /// <summary>A location's safe point on the surface.</summary>
        internal static MarkerRef LocationSafe(LocationDef loc) =>
            loc == null ? default : MarkerRef.Surface(loc.Id, string.IsNullOrEmpty(loc.SafePoint) ? (LocationGoal(loc).Marker ?? "") : loc.SafePoint);

        /// <summary>The expedition's base: the first Camp location's safe point (N-3).</summary>
        internal static MarkerRef Home(CampaignDefinition d)
        {
            var home = d.Locations?.Find(l => l.Kind == LocationKind.Camp) ?? (d.Locations != null && d.Locations.Count > 0 ? d.Locations[0] : null);
            return home != null ? MarkerRef.Surface(home.Id, home.SafePoint ?? "") : default;
        }
    }
}
