using UnityEngine;
using World.Sim;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Where things stand at the home camp (REFACTOR_SPEC 2.E, 8.3, appendix B), derived from the board alone, so any
    /// package can build one: <c>new CampLayout(state.Board)</c>. Owner: package D.
    ///
    /// F-0 first version (D refines it): home corner = <c>Board.StartCorners[0]</c>; hearth on the corner; dance circle
    /// at the reserved plot (<c>Board.ReservedPlots[0]</c>) with four drums on a 4 m circle and the dancer 6 m from the
    /// fire on the side towards the corner; spawn 4 lattice steps from the corner along the Forest spoke, facing the
    /// prince's start, which is 4 steps further on (20 m away, on the hex-edge line); Mira 2 + 2 steps from the corner
    /// between the Forest spoke and the next hex edge (about 17 m from the corner, 10 m from the spawn, off every plot).
    /// </summary>
    public sealed class CampLayout
    {
        public const float DrumRadius = 4f, DancerDistance = 6f, DrummerDistance = 5.5f;

        public int HomeCorner { get; }
        /// <summary>The home storehouse and hearth (the corner itself, on the ground).</summary>
        public Vector3 Hearth { get; }
        public Vector3 DanceCircleCentre { get; }
        public Vector3 DancerSpot { get; }
        /// <summary>The dancer faces the fire.</summary>
        public float DancerYaw { get; }
        public Vector3 MiraSpot { get; }
        public Vector3 DrummerSpot { get; }
        public Cell Spawn { get; }
        public int SpawnHeading { get; }
        public Cell PrinceStart { get; }

        private readonly Vector3 _forward, _right;

        public CampLayout(Board board)
        {
            var t = board.Topology;
            HomeCorner = board.StartCorners != null && board.StartCorners.Length > 0 ? board.StartCorners[0] : 0;
            Hearth = BoardSpace.Vertex(t, HomeCorner);

            // Forest sector of the home corner (the first sector if there is no forest).
            int forestHex = PlotLayout.SectorHex(t, HomeCorner, 0);
            for (int k = 0; k < 3; k++)
            {
                int h = PlotLayout.SectorHex(t, HomeCorner, k);
                if (h >= 0 && board.HexTerrain[h] == World.Sim.Terrain.Forest) { forestHex = h; break; }
            }
            var corner = WalkLattice.ToCell(Hearth);
            int hf = WalkLattice.HeadingOf(BoardSpace.YawOf(BoardSpace.Hex(t, forestHex) - Hearth));
            Spawn = corner + WalkLattice.Dir(hf) * 4;
            SpawnHeading = (hf + 4) % 6;
            PrinceStart = Spawn + WalkLattice.Dir(SpawnHeading) * 4;
            MiraSpot = WalkLattice.ToWorld(corner + WalkLattice.Dir(hf) * 2 + WalkLattice.Dir(hf + 5) * 2);

            int reserved = board.ReservedPlots != null && board.ReservedPlots.Length > 0 ? board.ReservedPlots[0] : -1;
            DanceCircleCentre = reserved >= 0
                ? new Vector3((float)PlotLayout.PlotX(t, reserved), BoardSpace.GroundY, (float)PlotLayout.PlotZ(t, reserved))
                : Hearth;
            var toCorner = Hearth - DanceCircleCentre; toCorner.y = 0;
            var u = toCorner.sqrMagnitude > 1e-4f ? toCorner.normalized : Vector3.forward;
            DancerSpot = DanceCircleCentre + u * DancerDistance;
            _forward = -u;
            _right = new Vector3(_forward.z, 0, -_forward.x);
            DancerYaw = BoardSpace.YawOf(_forward);
            DrummerSpot = DanceCircleCentre + _forward * DrummerDistance;
        }

        /// <summary>Drum 1-4, left to right as seen from <see cref="DancerSpot"/>, on the near half of the drum circle.</summary>
        public Vector3 DrumPosition(int pad1to4)
        {
            float a = (-67.5f + 45f * (Mathf.Clamp(pad1to4, 1, 4) - 1)) * Mathf.Deg2Rad;
            return DanceCircleCentre + (-_forward * Mathf.Cos(a) + _right * Mathf.Sin(a)) * DrumRadius;
        }
    }
}
