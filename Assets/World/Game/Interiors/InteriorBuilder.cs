using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Builds one interior from its definition (package I5): a rough-hewn rock cave of flat-shaded triangles over the
    /// floor cells (floor, walls and ceiling meet without gaps because every shared point is a function of its place
    /// alone), rubble in dead ends, and the marker objects (stairs and exits with their lights, doors, pedestals, the
    /// altar, carved marks, the tally spot, the threshold, the duel spots, safe point, witness seat, lever, plinth).
    /// Original procedural shapes only.
    /// </summary>
    public static class InteriorBuilder
    {
        public const float CellInradius = 2.5f;
        public static readonly float CellRadius = WalkLattice.Step / Mathf.Sqrt(3f);

        public static readonly Color RockColour = new Color(0.45f, 0.40f, 0.34f);
        public static readonly Color FloorColour = new Color(0.33f, 0.29f, 0.24f);
        public static readonly Color CeilingColour = new Color(0.30f, 0.27f, 0.24f);
        public static readonly Color CutStone = new Color(0.62f, 0.58f, 0.50f);
        public static readonly Color AshDark = new Color(0.20f, 0.19f, 0.18f);

        private static Material _rock;
        /// <summary>The lit, vertex-coloured material of the rock (one per session).</summary>
        public static Material RockMaterial => _rock != null ? _rock : (_rock = MaterialFactory.CreateVertexColored(Color.white, "Interior rock"));

        // ---------------- deterministic noise of a place ----------------

        internal static float Hash(Vector3 p, int salt)
        {
            int x = Mathf.RoundToInt(p.x * 20f), z = Mathf.RoundToInt(p.z * 20f);
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; h *= 0x27d4eb2d; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
        private static float Signed(Vector3 p, int salt) => Hash(p, salt) * 2f - 1f;

        private sealed class Ctx
        {
            public InteriorModel M;
            public bool Floor(Cell c) => M.Floor.Contains(c);
        }

        /// <summary>A shared point of the cave: a hex corner (3 cells round it) or an edge middle (2 cells).</summary>
        private struct Pt
        {
            public Vector3 Base;     // on the lattice at floor height
            public Vector3 Pull;     // towards the open floor (zero in the open)
            public int FloorCount, Cells;
        }

        private static Pt Corner(Ctx x, Cell c, int h)
        {
            float yaw = (60f * h + 30f) * Mathf.Deg2Rad;
            var b = WalkLattice.ToWorld(c) + new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * CellRadius;
            b.y = InteriorSpace.FloorY;
            return Make(x, b, new[] { c, c + WalkLattice.Dir(h), c + WalkLattice.Dir(h + 1) });
        }

        private static Pt Mid(Ctx x, Cell c, int h)
        {
            float yaw = 60f * h * Mathf.Deg2Rad;
            var b = WalkLattice.ToWorld(c) + new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * CellInradius;
            b.y = InteriorSpace.FloorY;
            return Make(x, b, new[] { c, c + WalkLattice.Dir(h) });
        }

        private static Pt Make(Ctx x, Vector3 b, Cell[] cells)
        {
            var p = new Pt { Base = b, Cells = cells.Length };
            var centroid = Vector3.zero;
            foreach (var c in cells) if (x.Floor(c)) { p.FloorCount++; centroid += WalkLattice.ToWorld(c); }
            if (p.FloorCount > 0 && p.FloorCount < cells.Length)
            {
                centroid /= p.FloorCount; centroid.y = b.y;
                var d = centroid - b;
                p.Pull = d.normalized * ((cells.Length == 2 ? 0.30f : 0.22f) + 0.28f * Hash(b, 1));
            }
            return p;
        }

        private static float CeilingHeight(Pt p)
        {
            float n = Signed(p.Base, 2);
            if (p.Cells == 3) return 3.15f + 0.75f * (p.FloorCount - 1) + 0.30f * n;
            return p.FloorCount == 2 ? 4.3f + 0.3f * n : 3.45f + 0.30f * n;
        }

        /// <summary>The point at a height fraction (0 floor, 1 ceiling) of the column standing on a shared point.</summary>
        private static Vector3 At(Pt p, float t, int row)
        {
            float h = CeilingHeight(p);
            var v = p.Base + p.Pull;
            var jit = new Vector3(Signed(p.Base, 10 + row), 0, Signed(p.Base, 20 + row));
            float open = p.FloorCount == p.Cells ? 0.25f : 1f;
            if (row == 0) return v + jit * 0.12f * open + Vector3.up * (0.03f * Signed(p.Base, 30));
            // The walls swell into the passage a little at waist height and lean back under the roof.
            float swell = Mathf.Sin(t * Mathf.PI) * (0.12f + 0.32f * Hash(p.Base, 40 + row));
            var pullDir = p.Pull.sqrMagnitude > 1e-6f ? p.Pull.normalized : Vector3.zero;
            return v + pullDir * swell + jit * 0.28f * open + Vector3.up * (t * h + 0.18f * Signed(p.Base, 50 + row) * (row < 3 ? 1f : 0.5f));
        }

        private static readonly float[] Rows = { 0f, 0.3f, 0.64f, 1f };

        // ---------------- the cave ----------------

        public static void Build(InteriorDirector d, InteriorModel m, List<IInteriorView> views)
        {
            var x = new Ctx { M = m };
            var mb = new MeshBuilder();
            foreach (var c in m.Floor) CellShell(x, mb, c);
            var shell = mb.ToObject("Rock", m.Root, RockMaterial);
            shell.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var deco = new MeshBuilder();
            foreach (var c in m.Floor) Dress(x, deco, c);
            if (deco.VertexCount > 0) deco.ToObject("Rubble", m.Root, RockMaterial).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var chalk = new MeshBuilder();
            WayMarks(d, m, chalk);
            if (chalk.VertexCount > 0) chalk.ToObject("Way marks", m.Root, RockMaterial).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (var md in m.Markers) InteriorProps.Build(d, m, md, views);
        }

        /// <summary>
        /// Pale chalk chevrons on the floor at every fork, pointing the shortest way to this level's nearest way up
        /// (the way you came in): the way out is findable without the map. Doors count as passable.
        /// </summary>
        private static void WayMarks(InteriorDirector d, InteriorModel m, MeshBuilder mb)
        {
            var dist = new Dictionary<Cell, int>();
            var q = new Queue<Cell>();
            foreach (var l in d.Links)
                if (l.Up && l.End.IsInterior && l.End.Interior == m.Id) { dist[l.Cell] = 0; q.Enqueue(l.Cell); }
            if (q.Count == 0) return;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int h = 0; h < 6; h++)
                {
                    var n = c + WalkLattice.Dir(h);
                    if (!m.Floor.Contains(n) || dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            var markerCells = new HashSet<Cell>();
            foreach (var md in m.Markers) markerCells.Add(m.ToCell(md.I, md.J));
            var chalk = new Color(0.86f, 0.84f, 0.76f);
            foreach (var c in m.Floor)
            {
                if (!dist.TryGetValue(c, out int dc) || dc == 0 || markerCells.Contains(c)) continue;
                // A fork: open floor in three directions or more, not all side by side.
                int open = 0, runs = 0;
                for (int h = 0; h < 6; h++)
                {
                    bool o = m.Floor.Contains(c + WalkLattice.Dir(h));
                    if (o) open++;
                    if (o && !m.Floor.Contains(c + WalkLattice.Dir(h - 1))) runs++;
                }
                if (open < 3 || runs < 2) continue;
                int toward = -1;
                for (int h = 0; h < 6; h++)
                    if (dist.TryGetValue(c + WalkLattice.Dir(h), out int dn) && dn == dc - 1) { toward = h; break; }
                if (toward < 0) continue;
                float yaw = WalkLattice.Yaw(toward) * Mathf.Deg2Rad;
                var f = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
                var side = new Vector3(f.z, 0, -f.x);
                var at = WalkLattice.ToWorld(c) + f * 1.2f + Vector3.up * 0.05f;
                at.y = InteriorSpace.FloorY + 0.05f;
                // Two strokes of a chevron and a short tail: ">" pointing the way out.
                mb.Strip(at - f * 0.45f + side * 0.4f, at + f * 0.15f, 0.11f, chalk);
                mb.Strip(at - f * 0.45f - side * 0.4f, at + f * 0.15f, 0.11f, chalk);
                mb.Strip(at - f * 0.95f, at - f * 0.35f, 0.09f, chalk * 0.9f);
            }
        }

        private static void CellShell(Ctx x, MeshBuilder mb, Cell c)
        {
            var centre = WalkLattice.ToWorld(c); centre.y = InteriorSpace.FloorY;
            // The 12 ring points in increasing yaw: edge middle h at 2h, corner h (between h and h+1) at 2h+1.
            var ring = new Pt[12];
            for (int h = 0; h < 6; h++) { ring[2 * h] = Mid(x, c, h); ring[2 * h + 1] = Corner(x, c, h); }
            int floorN = 0;
            for (int h = 0; h < 6; h++) if (x.Floor(c + WalkLattice.Dir(h))) floorN++;

            // Floor: a fan from the centre, each triangle its own shade.
            var fc = centre + Vector3.up * 0.0f;
            for (int k = 0; k < 12; k++)
            {
                var a = At(ring[k], 0, 0); var b = At(ring[(k + 1) % 12], 0, 0);
                mb.Tri(fc, a, b, Shade(FloorColour, (a + b + fc) / 3f, 0.10f));
            }
            // Ceiling: a low dome over the cell.
            float hc = 3.9f + 0.2f * floorN + 0.3f * Signed(centre, 3);
            var cc = centre + Vector3.up * hc + new Vector3(Signed(centre, 4), 0, Signed(centre, 5)) * 0.4f;
            for (int k = 0; k < 12; k++)
            {
                var a = At(ring[k], 1, 3); var b = At(ring[(k + 1) % 12], 1, 3);
                mb.Tri(cc, b, a, Shade(CeilingColour, (a + b + cc) / 3f, 0.12f));
            }
            // Walls towards every rock neighbour: two columns of panels from corner to middle to corner.
            for (int h = 0; h < 6; h++)
            {
                if (x.Floor(c + WalkLattice.Dir(h))) continue;
                var A = ring[(2 * h + 11) % 12]; var M = ring[2 * h]; var B = ring[2 * h + 1];
                WallPanel(mb, A, M);
                WallPanel(mb, M, B);
            }
        }

        private static void WallPanel(MeshBuilder mb, Pt left, Pt right)
        {
            for (int r = 0; r < Rows.Length - 1; r++)
            {
                var a = At(left, Rows[r], r); var b = At(left, Rows[r + 1], r + 1);
                var c = At(right, Rows[r + 1], r + 1); var d = At(right, Rows[r], r);
                var mid = (a + b + c + d) * 0.25f;
                float lift = r == 0 ? 0.82f : r == 1 ? 1f : 0.92f;
                mb.Tri(a, b, c, Shade(RockColour * lift, mid + Vector3.right * 0.1f, 0.16f));
                mb.Tri(a, c, d, Shade(RockColour * lift, mid - Vector3.right * 0.1f, 0.16f));
            }
        }

        internal static Color Shade(Color baseCol, Vector3 at, float amount)
        {
            float n = Signed(at, 7);
            float warm = Signed(at, 8) * 0.04f;
            var c = baseCol * (1f + n * amount);
            c.r += warm; c.b -= warm;
            c.a = 1f;
            return c;
        }

        /// <summary>Loose rock: heaps in dead ends (a fallen working), a few stones along the walls.</summary>
        private static void Dress(Ctx x, MeshBuilder mb, Cell c)
        {
            var centre = WalkLattice.ToWorld(c); centre.y = InteriorSpace.FloorY;
            var rnd = new PropRandom(c.I * 7919 + c.J * 104729 + 17);
            bool marker = false;
            foreach (var md in x.M.Markers) if (x.M.ToCell(md.I, md.J) == c) { marker = true; break; }
            int open = 0, lastOpen = 0;
            for (int h = 0; h < 6; h++) if (x.Floor(c + WalkLattice.Dir(h))) { open++; lastOpen = h; }
            for (int h = 0; h < 6; h++)
            {
                if (x.Floor(c + WalkLattice.Dir(h))) continue;
                if (rnd.Next() > 0.55f) continue;
                float yaw = (60f * h + rnd.Range(-25f, 25f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
                var p = centre + dir * rnd.Range(1.6f, 1.95f);
                mb.Rock(p, rnd.Range(0.18f, 0.42f), Shade(RockColour * 0.9f, p, 0.2f), rnd.Next);
            }
            if (open == 1 && !marker)
            {
                // A dead end: the roof came down here.
                float back = (60f * (lastOpen + 3)) * Mathf.Deg2Rad;
                var bdir = new Vector3(Mathf.Sin(back), 0, Mathf.Cos(back));
                for (int k = 0; k < 9; k++)
                {
                    var side = new Vector3(bdir.z, 0, -bdir.x) * rnd.Range(-1.4f, 1.4f);
                    var p = centre + bdir * rnd.Range(0.3f, 1.8f) + side;
                    mb.Rock(p, rnd.Range(0.35f, 0.85f) * (1.2f - 0.15f * k / 9f), Shade(RockColour * 0.85f, p, 0.25f), rnd.Next);
                }
                var top = centre + bdir * 1.4f;
                mb.Rock(top + Vector3.up * 0.5f, 0.9f, Shade(RockColour * 0.8f, top, 0.2f), rnd.Next);
                mb.Box(centre + bdir * 0.6f + Vector3.up * 0.06f, new Vector3(1.2f, 0.12f, 0.25f), LandLook.DarkWood * 0.8f, BoardSpace.YawOf(bdir) + 70f);
            }
        }
    }
}
