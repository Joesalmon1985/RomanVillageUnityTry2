using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Which interior the player is in, and moving between the surface and interiors (PRD_CRYSTALS 5.10, appendix B,
    /// package I5). The interiors of the running campaign's definition are built far to the east on the walk lattice
    /// (<see cref="InteriorSpace"/>); <see cref="InteriorDirector"/> builds them, keeps them in step with the sim and moves
    /// the player through stairs and exits. Everything here reads <c>WorldState</c> and <see cref="CampaignApi"/>; nothing
    /// writes it. With no campaign nothing is built and <see cref="Current"/> stays null. Owner: I5.
    /// </summary>
    public static class Interiors
    {
        /// <summary>The interior the player is in, or null on the surface.</summary>
        public static string Current { get; private set; }
        /// <summary>Raised with the new <see cref="Current"/> (null = back on the surface).</summary>
        public static event Action<string> Changed;
        /// <summary>Raised when the player starts going through a stair, exit or open door way (from, to).</summary>
        public static event Action<MarkerRef, MarkerRef> Crossing;
        /// <summary>Raised after the interiors were built or rebuilt (or cleared: the count is 0).</summary>
        public static event Action Built;

        /// <summary>Tests: the facts the interiors read instead of the campaign's (null = the running state's).</summary>
        public static Func<string, bool> FactsOverride;

        private static InteriorDirector D => InteriorDirector.Instance;

        /// <summary>The built interiors in definition order (empty with no campaign).</summary>
        public static IReadOnlyList<InteriorModel> All => D != null ? D.Models : (IReadOnlyList<InteriorModel>)Array.Empty<InteriorModel>();
        /// <summary>A built interior by id, or null.</summary>
        public static InteriorModel Get(string id) => D != null ? D.Model(id) : null;
        /// <summary>Index of a built interior (its place east of the island), or -1.</summary>
        public static int IndexOf(string id) { var m = Get(id); return m != null ? m.Index : -1; }
        /// <summary>Every stair, exit and door way, both ends (surface ends included).</summary>
        public static IReadOnlyList<InteriorLink> Links => D != null ? D.Links : (IReadOnlyList<InteriorLink>)Array.Empty<InteriorLink>();
        /// <summary>True while the player goes through a stair or exit (the fade).</summary>
        public static bool IsCrossing => D != null && D.IsCrossing;
        /// <summary>True while "Lead me out" walks the player.</summary>
        public static bool IsLeadingOut => D != null && D.LeadingOut;

        /// <summary>Puts the player (with the fade) on a marker: an interior marker or a surface one. False when unknown.</summary>
        public static bool Enter(MarkerRef to) => D != null && D.EnterAt(to);
        /// <summary>Back up to the surface at once (with the fade) by the nearest open way out. False on the surface or with none.</summary>
        public static bool Exit() => D != null && D.ExitNow();
        /// <summary>"Lead me out": walks the player to the nearest open way up and on through every level to the surface. False with none.</summary>
        public static bool LeadOut() => D != null && D.StartLeadOut();
        /// <summary>Builds the interiors of a definition (floors, walls, stairs, lights, walk regions). Null clears them.</summary>
        public static void Build(CampaignDefinition definition) { if (D != null) D.BuildFor(definition); }
        /// <summary>Rebuilds from the running state (after a load or a new game) and re-reads the player's place.</summary>
        public static void RebuildFromState() { if (D != null) D.RebuildFromState(); }

        /// <summary>A fact the interiors go by (doors, stairs): the override in tests, else the running campaign's.</summary>
        public static bool Knows(string fact)
        {
            if (string.IsNullOrEmpty(fact)) return true;
            if (FactsOverride != null) return FactsOverride(fact);
            var s = WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;
            return s?.Campaign != null && s.Campaign.Knows(fact);
        }

        // ---------------- places (for the expedition director and the duel staging) ----------------

        /// <summary>The world cell of a marker (surface or interior). False when it cannot be placed.</summary>
        public static bool TryCell(MarkerRef r, out Cell cell)
        {
            cell = default;
            if (D == null) return false;
            return D.TryMarkerCell(r, out cell);
        }

        /// <summary>The world point (on the floor or ground) of a marker. False when it cannot be placed.</summary>
        public static bool TryWorld(MarkerRef r, out Vector3 world)
        {
            world = default;
            if (!TryCell(r, out var c)) return false;
            world = PlayerWalker.CellPosition(c);
            return true;
        }

        /// <summary>
        /// The world path (cell centres, both ends included) over an interior's floor from one local cell to another,
        /// through doors whose facts are known (<see cref="InteriorMaps.Path"/>). Empty when there is none.
        /// </summary>
        public static List<Vector3> PathWorld(string interiorId, int i0, int j0, int i1, int j1)
        {
            var list = new List<Vector3>();
            var m = Get(interiorId);
            if (m == null) return list;
            var p = InteriorMaps.Path(m.Def, i0, j0, i1, j1, Knows);
            for (int k = 0; k + 1 < p.Length; k += 2) list.Add(m.World(p[k], p[k + 1]));
            return list;
        }

        /// <summary>A door marker of an interior is open (its fact is known). True for a marker that is no door.</summary>
        public static bool IsDoorOpen(string interiorId, string markerId)
        {
            var m = Get(interiorId);
            var md = m?.Marker(markerId);
            if (md == null || md.Kind != MarkerKind.Door) return true;
            return Knows(md.OpenWhen);
        }

        /// <summary>The link (stair, exit, door way) at a marker, or null.</summary>
        public static InteriorLink LinkAt(MarkerRef r)
        {
            foreach (var l in Links) if (l.End == r) return l;
            return null;
        }

        /// <summary>
        /// Where the campaign's party is now in the world (between ticks too): along its leg (interior legs follow the
        /// leg's cells), or at its place. False with no campaign or a place that cannot be placed. heading is the
        /// direction of travel (zero when standing).
        /// </summary>
        public static bool PartyWorld(WorldState s, double tickFraction, out Vector3 position, out Vector3 heading, out string interiorId)
        {
            position = heading = default; interiorId = null;
            if (s?.Campaign == null || D == null) return false;
            if (!CampaignApi.PartyPosition(s, tickFraction, out var from, out var to, out double t))
            {
                interiorId = from.IsInterior ? from.Interior : null;
                return TryWorld(from, out position);
            }
            var cells = CampaignApi.LegCells(s);
            if (cells.Length >= 4 && from.IsInterior && Get(from.Interior) != null)
            {
                var m = Get(from.Interior);
                interiorId = m.Id;
                int n = cells.Length / 2;
                double f = t * (n - 1);
                int k = Math.Min(n - 2, (int)Math.Floor(f));
                float u = (float)(f - k);
                var a = m.World(cells[2 * k], cells[2 * k + 1]);
                var b = m.World(cells[2 * k + 2], cells[2 * k + 3]);
                position = Vector3.Lerp(a, b, u);
                heading = (b - a).normalized;
                return true;
            }
            // A crossing (stair, exit) or a surface leg: from one end to the other in a straight line, or on the same
            // side the whole way when the two ends lie in different spaces.
            bool okA = TryWorld(from, out var pa), okB = TryWorld(to, out var pb);
            if (!okA && !okB) return false;
            bool sameSpace = (from.Interior ?? "") == (to.Interior ?? "");
            if (!sameSpace)
            {
                bool first = t < 0.5;
                interiorId = first ? (from.IsInterior ? from.Interior : null) : (to.IsInterior ? to.Interior : null);
                position = first ? (okA ? pa : pb) : (okB ? pb : pa);
                return true;
            }
            interiorId = from.IsInterior ? from.Interior : null;
            if (!okA) pa = pb; if (!okB) pb = pa;
            position = Vector3.Lerp(pa, pb, (float)t);
            heading = (pb - pa).sqrMagnitude > 1e-4f ? (pb - pa).normalized : Vector3.zero;
            return true;
        }

        /// <summary>The local cells of an interior the player has seen (map), as i,j pairs. Empty when none.</summary>
        public static IReadOnlyCollection<Cell> Explored(string interiorId) => D != null ? D.ExploredOf(interiorId) : (IReadOnlyCollection<Cell>)Array.Empty<Cell>();

        /// <summary>
        /// The game-layer extra a save may keep under the key "interior" (package I8's <c>SaveSystem</c>): the cells
        /// explored in each interior. The player's place itself is the walker's cell, which the save already keeps.
        /// </summary>
        public static string SaveExtra() => D != null ? D.WriteExplored() : "";
        /// <summary>Restores <see cref="SaveExtra"/> after a load.</summary>
        public static void LoadExtra(string text) { if (D != null) D.ReadExplored(text); }

        /// <summary>For I5: sets <see cref="Current"/> and raises <see cref="Changed"/>.</summary>
        internal static void SetCurrent(string interiorId)
        {
            if (Current == interiorId) return;
            Current = interiorId;
            Changed?.Invoke(interiorId);
        }

        internal static void RaiseCrossing(MarkerRef from, MarkerRef to) => Crossing?.Invoke(from, to);
        internal static void RaiseBuilt() => Built?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; Changed = null; Crossing = null; Built = null; FactsOverride = null; }
    }

    /// <summary>
    /// Where interiors lie on the walk lattice (PRD_CRYSTALS appendix B, I5): each interior has its own block of 16 by
    /// 16 cells 1.3 km east of the board centre, one block every 200 m to the north, in definition order. The lattice
    /// is the surface's, so the six steps and the 60 degree turns are the same. Owner: I5.
    /// </summary>
    public static class InteriorSpace
    {
        /// <summary>Lattice cell of interior 0's local (0, 0): x 1299 m, z 0.</summary>
        public const int OriginI = 300, OriginJ = -150;
        /// <summary>Rows between two interiors' origins (200 m to the north).</summary>
        public const int Spacing = 40;
        /// <summary>Height of every interior floor (the walker's eye stays 1.7 m above it).</summary>
        public const float FloorY = 0f;

        /// <summary>The lattice cell of an interior's local (0, 0).</summary>
        public static Cell Origin(int interiorIndex) => new Cell(OriginI, OriginJ + Spacing * interiorIndex);

        /// <summary>The world cell of an interior's local cell (its built index; interior 0's when it is not built).</summary>
        public static Cell ToCell(string interiorId, int i, int j)
        {
            int k = Interiors.IndexOf(interiorId);
            return Origin(k < 0 ? 0 : k) + new Cell(i, j);
        }

        /// <summary>World point (floor height) of an interior's local cell.</summary>
        public static Vector3 ToWorld(string interiorId, int i, int j)
        {
            var p = WalkLattice.ToWorld(ToCell(interiorId, i, j));
            p.y = FloorY;
            return p;
        }

        /// <summary>The interior and local cell of a world cell inside a built interior's block (floor or rock). False elsewhere.</summary>
        public static bool TryLocal(Cell world, out string interiorId, out int i, out int j)
        {
            foreach (var m in Interiors.All)
            {
                int di = world.I - m.Origin.I, dj = world.J - m.Origin.J;
                if (di >= 0 && dj >= 0 && di < m.Width && dj < m.Height)
                {
                    interiorId = m.Id; i = di; j = dj;
                    return true;
                }
            }
            interiorId = null; i = j = 0;
            return false;
        }
    }

    /// <summary>One built interior: its definition, its place on the lattice and its scene objects.</summary>
    public sealed class InteriorModel
    {
        public string Id, Name, Location;
        public int Index;
        public InteriorDef Def;
        public Cell Origin;
        public int Width, Height;
        /// <summary>Floor cells (world lattice cells).</summary>
        public readonly HashSet<Cell> Floor = new HashSet<Cell>();
        /// <summary>Markers with their local cells filled in.</summary>
        public List<MarkerDef> Markers = new List<MarkerDef>();
        public Transform Root;
        /// <summary>Depth below the surface (1 = reached from the surface; deeper levels count stairs down).</summary>
        public int Depth = 1;

        public Cell ToCell(int i, int j) => Origin + new Cell(i, j);
        public Vector3 World(int i, int j) { var p = WalkLattice.ToWorld(ToCell(i, j)); p.y = InteriorSpace.FloorY; return p; }
        public bool IsFloor(int i, int j) => InteriorMaps.IsFloor(Def, i, j);
        public MarkerDef Marker(string id) => Markers.Find(m => m.Id == id);
        public MarkerRef Ref(string markerId) => new MarkerRef(Location ?? "", Id, markerId);
        /// <summary>World centre of the block's floor.</summary>
        public Vector3 Centre
        {
            get
            {
                if (Floor.Count == 0) return WalkLattice.ToWorld(Origin);
                var sum = Vector3.zero;
                foreach (var c in Floor) sum += WalkLattice.ToWorld(c);
                return sum / Floor.Count;
            }
        }
    }

    /// <summary>One end of a stair, exit or door way (the other end is <see cref="To"/>).</summary>
    public sealed class InteriorLink
    {
        public MarkerRef End, To;
        public MarkerKind Kind;
        public string Name;
        /// <summary>The end's world cell.</summary>
        public Cell Cell;
        /// <summary>Heading the player faces on arriving here (away from the stair, into the open).</summary>
        public int OutHeading;
        /// <summary>Going through this end leads up (to the surface or a shallower level).</summary>
        public bool Up;
        public bool OnSurface => !End.IsInterior;
        /// <summary>Every fact that must be known for this link to be open (both ends' OpenWhen and the connections').</summary>
        public readonly List<string> Needs = new List<string>();
        public bool IsOpen { get { foreach (var f in Needs) if (!Interiors.Knows(f)) return false; return true; } }
        public override string ToString() => End + " -> " + To;
    }
}
