using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Which lattice cells the player may step into (REFACTOR_SPEC 2.D "Blocked cells") and paths between cells.
    /// Owner: package P. A cell is blocked when it lies
    /// <list type="bullet">
    /// <item>beyond <see cref="BoardSpace.BoardRadius"/> + 4 m from the board centre;</item>
    /// <item>within 3 m of a finished settlement's corner (storehouse and hearth);</item>
    /// <item>within 5 m of a plot holding a building that is constructing, active or retiring (empty plots and sites
    /// stay walkable, so the player can stand on them to cast);</item>
    /// <item>within a registered static blocker (mountain peaks, the dance fire and drums).</item>
    /// </list>
    /// Trees, crops and people never block; main roads and hex-edge lines stay walkable. The state-derived part is read
    /// from <c>WorldBootstrap.Instance.Sim.State</c> (read only) and cached per tick; "within r" is strict, so with 5 m
    /// steps a settlement or building blocks exactly its own cell.
    /// </summary>
    public static class WalkGrid
    {
        public const float BoardMargin = 4f, SettlementRadius = 3f, BuildingRadius = 5f;

        private struct Blocker { public Vector3 Centre; public float Radius; public string Owner; }
        private static readonly List<Blocker> _static = new List<Blocker>();
        private static readonly HashSet<Cell> _stateBlocked = new HashSet<Cell>();
        private static readonly List<Vector3> _stateCentres = new List<Vector3>();
        private static readonly List<float> _stateRadii = new List<float>();
        private static WorldState _cachedFor;
        private static long _cachedTick = -1;
        private static int _cachedCounts = -1;

        /// <summary>Overrides the state used for the sim-derived blockers (tests); null = the running world's state.</summary>
        public static WorldState StateOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _static.Clear(); _stateBlocked.Clear(); _stateCentres.Clear(); _stateRadii.Clear();
            _cachedFor = null; _cachedTick = -1; _cachedCounts = -1; StateOverride = null;
        }

        private static WorldState State
        {
            get
            {
                if (StateOverride != null) return StateOverride;
                var b = WorldBootstrap.Instance;
                return b != null && b.Sim != null ? b.Sim.State : null;
            }
        }

        public static bool IsBlocked(Cell c) => WhyBlocked(c) != null;

        /// <summary>Why a cell is blocked ("edge", "camp", "building", or the static blocker's owner), or null when free.</summary>
        public static string WhyBlocked(Cell c)
        {
            var p = WalkLattice.ToWorld(c);
            if (BoardSpace.Flat(p) > BoardSpace.BoardRadius + BoardMargin) return "edge";
            foreach (var b in _static)
                if (BoardSpace.Flat(p - b.Centre) < b.Radius) return b.Owner ?? "static";
            Refresh();
            for (int i = 0; i < _stateCentres.Count; i++)
                if (BoardSpace.Flat(p - _stateCentres[i]) < _stateRadii[i] - 0.001f)
                    return _stateRadii[i] <= SettlementRadius ? "camp" : "building";
            return null;
        }

        /// <summary>Blocks every cell within <paramref name="radius"/> metres of <paramref name="centre"/> (mountain peaks, the dance fire and drums).</summary>
        public static void AddStaticBlocker(Vector3 centre, float radius, string owner) =>
            _static.Add(new Blocker { Centre = centre, Radius = radius, Owner = owner });

        public static void RemoveStaticBlockers(string owner) => _static.RemoveAll(b => b.Owner == owner);

        /// <summary>Number of registered static blockers (tests, diagnostics).</summary>
        public static int StaticBlockerCount => _static.Count;

        /// <summary>Forgets the cached state-derived blockers (they are rebuilt on the next query).</summary>
        public static void Invalidate() { _cachedFor = null; _cachedTick = -1; }

        private static void Refresh()
        {
            var s = State;
            if (s == null || s.Economy == null) { _stateCentres.Clear(); _stateRadii.Clear(); _cachedFor = null; return; }
            int counts = s.Economy.Settlements.Count * 7919 + s.Economy.Buildings.Count;
            if (ReferenceEquals(s, _cachedFor) && s.Tick == _cachedTick && counts == _cachedCounts) return;
            _cachedFor = s; _cachedTick = s.Tick; _cachedCounts = counts;
            _stateCentres.Clear(); _stateRadii.Clear();
            var t = s.Board.Topology;
            foreach (var st in s.Economy.Settlements)
            {
                if (st.State != SettlementState.Active) continue;
                _stateCentres.Add(BoardSpace.Vertex(t, st.Corner));
                _stateRadii.Add(SettlementRadius);
            }
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State != BuildingState.Constructing && b.State != BuildingState.Active && b.State != BuildingState.Retiring) continue;
                if (!PlotLayout.IsValidPlot(t, b.Plot)) continue;
                _stateCentres.Add(new Vector3((float)PlotLayout.PlotX(t, b.Plot), BoardSpace.GroundY, (float)PlotLayout.PlotZ(t, b.Plot)));
                _stateRadii.Add(BuildingRadius);
            }
        }

        /// <summary>Lattice distance in steps (hex distance on the axial coordinates).</summary>
        public static int Distance(Cell a, Cell b)
        {
            int di = a.I - b.I, dj = a.J - b.J;
            return (System.Math.Abs(di) + System.Math.Abs(dj) + System.Math.Abs(di + dj)) / 2;
        }

        /// <summary>
        /// Cells from <paramref name="from"/> (exclusive) to <paramref name="to"/> (inclusive) avoiding blocked cells
        /// (A*, 6 neighbours, ties broken by heading order so paths are deterministic), or null if none within
        /// <paramref name="maxNodes"/> expanded nodes. An empty list when from == to. The goal itself may not be blocked.
        /// </summary>
        public static List<Cell> FindPath(Cell from, Cell to, int maxNodes = 20000)
        {
            if (from == to) return new List<Cell>();
            if (IsBlocked(to)) return null;
            var open = new SortedSet<(int f, int h, int seq, Cell c)>(Comparer<(int f, int h, int seq, Cell c)>.Create((x, y) =>
                x.f != y.f ? x.f.CompareTo(y.f) : x.h != y.h ? x.h.CompareTo(y.h) : x.seq.CompareTo(y.seq)));
            var g = new Dictionary<Cell, int> { [from] = 0 };
            var came = new Dictionary<Cell, Cell>();
            var closed = new HashSet<Cell>();
            int seq = 0;
            open.Add((Distance(from, to), Distance(from, to), seq++, from));
            int expanded = 0;
            while (open.Count > 0)
            {
                var cur = open.Min; open.Remove(cur);
                if (cur.c == to)
                {
                    var path = new List<Cell>();
                    for (var c = to; c != from; c = came[c]) path.Add(c);
                    path.Reverse();
                    return path;
                }
                if (!closed.Add(cur.c)) continue;
                if (++expanded > maxNodes) return null;
                int gc = g[cur.c];
                for (int h = 0; h < 6; h++)
                {
                    var n = cur.c + WalkLattice.Dir(h);
                    if (closed.Contains(n)) continue;
                    if (n != to && IsBlocked(n)) continue;
                    int ng = gc + 1;
                    if (g.TryGetValue(n, out int old) && old <= ng) continue;
                    g[n] = ng; came[n] = cur.c;
                    int hh = Distance(n, to);
                    open.Add((ng + hh, hh, seq++, n));
                }
            }
            return null;
        }

        /// <summary>The free cell nearest to <paramref name="c"/> (itself if free), searching rings up to <paramref name="maxRing"/>; null if none.</summary>
        public static Cell? NearestFree(Cell c, int maxRing = 6)
        {
            if (!IsBlocked(c)) return c;
            for (int r = 1; r <= maxRing; r++)
            {
                // Walk the ring of radius r: start r steps in heading 4, then r steps in each of the six headings.
                var cur = c + WalkLattice.Dir(4) * r;
                Cell? best = null; float bestD = float.MaxValue;
                var centre = WalkLattice.ToWorld(c);
                for (int side = 0; side < 6; side++)
                    for (int k = 0; k < r; k++)
                    {
                        if (!IsBlocked(cur))
                        {
                            float d = BoardSpace.Flat(WalkLattice.ToWorld(cur) - centre);
                            if (d < bestD - 1e-4f) { bestD = d; best = cur; }
                        }
                        cur = cur + WalkLattice.Dir(side);
                    }
                if (best.HasValue) return best;
            }
            return null;
        }
    }
}
