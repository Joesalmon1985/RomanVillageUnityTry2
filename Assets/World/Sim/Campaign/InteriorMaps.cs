using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Interiors as lattice regions (5.10, A.5). Column i and row j of <see cref="InteriorDef.Rows"/> are lattice
    /// coordinates in the interior's own frame, so the six step directions are the surface's (WalkLattice: (0,1), (1,0),
    /// (1,-1), (0,-1), (-1,0), (-1,1)). '#' (and ' ' or anything outside the rows) is rock; '.' and every legend character
    /// are floor. A Door marker blocks its cell until its OpenWhen fact is known. Deterministic: neighbours are tried in
    /// that fixed order, so equal-length paths always come out the same.
    /// </summary>
    public static class InteriorMaps
    {
        public const int MaxSize = 16;
        public const int MaxFloorCells = 80;

        /// <summary>The six lattice steps as (di, dj), heading 0 to 5.</summary>
        public static readonly int[] Steps = { 0, 1, 1, 0, 1, -1, 0, -1, -1, 0, -1, 1 };

        private static char At(InteriorDef m, int i, int j)
        {
            if (m?.Rows == null || j < 0 || j >= m.Rows.Count) return '#';
            var row = m.Rows[j];
            return row == null || i < 0 || i >= row.Length ? '#' : row[i];
        }

        /// <summary>The cell is inside the rows and not rock.</summary>
        public static bool IsFloor(InteriorDef m, int i, int j)
        {
            char ch = At(m, i, j);
            return ch == '.' || (ch != '#' && ch != ' ' && m.Legend != null && m.Legend.ContainsKey(ch));
        }

        /// <summary>The legend marker on a cell, null when none.</summary>
        public static MarkerDef MarkerAt(InteriorDef m, int i, int j)
        {
            char ch = At(m, i, j);
            return ch != '.' && ch != '#' && m?.Legend != null && m.Legend.TryGetValue(ch, out var md) ? md : null;
        }

        /// <summary>
        /// Every legend marker placed on the map with its cell filled in (I, J from the rows), in row then column order.
        /// A legend character used on several cells gives several markers (the validator reports duplicate ids).
        /// </summary>
        public static List<MarkerDef> Markers(InteriorDef m)
        {
            var list = new List<MarkerDef>();
            if (m?.Rows == null || m.Legend == null) return list;
            for (int j = 0; j < m.Rows.Count; j++)
            {
                var row = m.Rows[j] ?? "";
                for (int i = 0; i < row.Length; i++)
                {
                    if (!m.Legend.TryGetValue(row[i], out var md) || md == null) continue;
                    list.Add(new MarkerDef
                    {
                        Id = md.Id, Kind = md.Kind, I = i, J = j, To = md.To, OpenWhen = md.OpenWhen, Crystal = md.Crystal, Name = md.Name,
                    });
                }
            }
            return list;
        }

        /// <summary>The cell of a marker by id; false when it is not on the map.</summary>
        public static bool TryFind(InteriorDef m, string markerId, out int i, out int j)
        {
            foreach (var md in Markers(m))
                if (md.Id == markerId) { i = md.I; j = md.J; return true; }
            i = j = 0;
            return false;
        }

        /// <summary>Floor cells, in row then column order, as i,j pairs.</summary>
        public static int[] FloorCells(InteriorDef m)
        {
            var list = new List<int>();
            if (m?.Rows == null) return list.ToArray();
            for (int j = 0; j < m.Rows.Count; j++)
                for (int i = 0; i < (m.Rows[j]?.Length ?? 0); i++)
                    if (IsFloor(m, i, j)) { list.Add(i); list.Add(j); }
            return list.ToArray();
        }

        /// <summary>
        /// Shortest path over floor cells from (i0, j0) to (i1, j1) using the six steps, as i,j pairs including both ends.
        /// A Door cell is passable only when factKnown(its OpenWhen) is true (factKnown null = every door shut). Empty when
        /// there is no path or either end is not floor.
        /// </summary>
        public static int[] Path(InteriorDef m, int i0, int j0, int i1, int j1, Func<string, bool> factKnown)
        {
            if (!Passable(m, i0, j0, factKnown, true) || !Passable(m, i1, j1, factKnown, false)) return new int[0];
            int h = m.Rows.Count, w = 0;
            foreach (var r in m.Rows) w = Math.Max(w, r?.Length ?? 0);
            if (i0 == i1 && j0 == j1) return new[] { i0, j0 };
            var prev = new int[w * h];
            for (int k = 0; k < prev.Length; k++) prev[k] = -2;
            var queue = new Queue<int>();
            int start = j0 * w + i0, goal = j1 * w + i1;
            prev[start] = -1;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (cur == goal) break;
                int ci = cur % w, cj = cur / w;
                for (int d = 0; d < 6; d++)
                {
                    int ni = ci + Steps[2 * d], nj = cj + Steps[2 * d + 1];
                    if (ni < 0 || nj < 0 || ni >= w || nj >= h) continue;
                    int n = nj * w + ni;
                    if (prev[n] != -2 || !Passable(m, ni, nj, factKnown, false)) continue;
                    prev[n] = cur;
                    queue.Enqueue(n);
                }
            }
            if (prev[goal] == -2) return new int[0];
            var cells = new List<int>();
            for (int cur = goal; cur != -1; cur = prev[cur]) cells.Add(cur);
            cells.Reverse();
            var path = new int[cells.Count * 2];
            for (int k = 0; k < cells.Count; k++) { path[2 * k] = cells[k] % w; path[2 * k + 1] = cells[k] / w; }
            return path;
        }

        // The start cell is always passable if it is floor (the party may stand in an open doorway when it shuts).
        private static bool Passable(InteriorDef m, int i, int j, Func<string, bool> factKnown, bool isStart)
        {
            if (!IsFloor(m, i, j)) return false;
            if (isStart) return true;
            var md = MarkerAt(m, i, j);
            if (md == null || md.Kind != MarkerKind.Door || string.IsNullOrEmpty(md.OpenWhen)) return true;
            return factKnown != null && factKnown(md.OpenWhen);
        }
    }
}
