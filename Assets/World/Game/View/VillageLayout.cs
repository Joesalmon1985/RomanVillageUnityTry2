using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Where the village pieces stand, derived from the board: home corner H and warehouse flag W (40 m out along a
    /// hex edge). Axis: Out = unit direction H to W; Right = Out rotated 90 degrees clockwise (seen from above).
    /// All positions are on the ground (y = BoardSpace.GroundY).
    ///
    /// The village is compact (everything within about 55 m of H) and stays off the road plots: buildings sit in
    /// the three sectors between the hex edges that meet at H, at least about 9 m from any flag and 7 m from the
    /// hex-edge lines (roads). The warehouse stands beside flag W in the hex interior to the Right of the H-W edge,
    /// its door facing back towards the square, so the corner 60 m out stays free.
    /// </summary>
    public sealed class VillageLayout
    {
        public const float SquareRadius = 12f;
        /// <summary>Radius of the clearing around the village kept free of trees, rocks and crops.</summary>
        public const float ClearingRadius = 58f;

        public Vector3 Home, WarehouseVertex, Out, Right;
        public Vector3 SquareCentre, VillageCentre;
        public Vector3 Spawn; public float SpawnYaw;
        public Vector3 WarehouseDoor; public float WarehouseYaw; // yaw of the building's outward (+Z local) axis
        public Vector3 Storehouse; public float StorehouseYaw;
        public Vector3 Market; public float MarketYaw;
        public Vector3 Bell;
        public Vector3 MiraSpot, DoormanSpot, QuartermasterSpot, KeeperSpot, CaptainSpot, ScribeSpot;
        /// <summary>Small houses around the square (position, yaw of the door side).</summary>
        public readonly List<(Vector3 pos, float yaw)> Cottages = new List<(Vector3, float)>();
        /// <summary>Rival outpost corner (world).</summary>
        public Vector3 Outpost;

        /// <summary>Circles (x, z, radius) kept clear of trees, rocks and crops.</summary>
        public readonly List<Vector3> Clear = new List<Vector3>();

        /// <summary>Footprint half-sizes used by tests and clearing: (centre, half extents, yaw).</summary>
        public const float CottageWidth = 6f, CottageDepth = 5f, StorehouseWidth = 8f, StorehouseDepth = 6f;

        public VillageLayout(Board board)
        {
            var t = board.Topology;
            Home = BoardSpace.Vertex(t, board.HomeCityVertex);
            WarehouseVertex = BoardSpace.Vertex(t, board.WarehouseVertex);
            Outpost = board.RivalOutpostVertex >= 0 ? BoardSpace.Vertex(t, board.RivalOutpostVertex) : Home;
            Out = (WarehouseVertex - Home); Out.y = 0; Out.Normalize();
            Right = new Vector3(Out.z, 0, -Out.x);
            Vector3 At(float right, float @out) => Home + Right * right + Out * @out;

            SquareCentre = At(0, 5);
            VillageCentre = At(3, 14);

            // Warehouse: door 28 m out and 9 m to the right, facing the square; the hall runs 22 m further out.
            WarehouseDoor = At(9, 28);
            WarehouseYaw = BoardSpace.YawOf(-Out);

            MiraSpot = At(-3, 15);
            DoormanSpot = At(6, 24);
            Spawn = At(-1, 8);
            SpawnYaw = BoardSpace.YawOf((WarehouseDoor + MiraSpot) * 0.5f - Spawn);

            Storehouse = At(18, 10);
            StorehouseYaw = BoardSpace.YawOf(SquareCentre - Storehouse);
            Market = At(-14, 9);
            MarketYaw = BoardSpace.YawOf(SquareCentre - Market);
            Bell = At(5, 3);

            var sFwd = BoardSpace.DirOf(StorehouseYaw); var sSide = new Vector3(sFwd.z, 0, -sFwd.x);
            QuartermasterSpot = Storehouse + sFwd * 5f + sSide * 1.5f;
            KeeperSpot = Market - BoardSpace.DirOf(MarketYaw) * 1.6f;
            CaptainSpot = At(8, -4);
            ScribeSpot = At(-6, -3);

            foreach (var (r, o) in new[] { (-10f, 21.7f), (-22f, -2f), (-8f, -18f), (8f, -18f), (22f, 0f) })
            {
                var p = At(r, o);
                Cottages.Add((p, BoardSpace.YawOf(SquareCentre - p)));
            }

            // One wide clearing so the village reads as a settlement in open ground, plus the outpost's clearing.
            Clear.Add(new Vector3(VillageCentre.x, VillageCentre.z, ClearingRadius));
            Clear.Add(new Vector3(Outpost.x, Outpost.z, 24f));
        }

        public bool IsClear(Vector3 p, float margin = 0f)
        {
            foreach (var c in Clear)
            {
                float dx = p.x - c.x, dz = p.z - c.y;
                if (dx * dx + dz * dz < (c.z + margin) * (c.z + margin)) return false;
            }
            return true;
        }
    }
}
