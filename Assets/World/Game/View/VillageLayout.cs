using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Where the village pieces stand, derived from the board (home vertex H and warehouse vertex W).
    /// Axis: Out = unit direction H to W; Right = Out rotated 90 degrees clockwise (seen from above).
    /// All positions are on the ground (y = BoardSpace.GroundY).
    /// </summary>
    public sealed class VillageLayout
    {
        public const float SquareRadius = 15f;

        public Vector3 Home, WarehouseVertex, Out, Right;
        public Vector3 SquareCentre;
        public Vector3 Spawn; public float SpawnYaw;
        public Vector3 WarehouseDoor; public float WarehouseYaw; // yaw of the building's outward (+Z local) axis
        public Vector3 Storehouse; public float StorehouseYaw;
        public Vector3 Market; public float MarketYaw;
        public Vector3 Bell;
        public Vector3 MiraSpot, DoormanSpot, QuartermasterSpot, KeeperSpot, CaptainSpot, ScribeSpot;

        /// <summary>Circles (x, z, radius) kept clear of trees, rocks and crops.</summary>
        public readonly List<Vector3> Clear = new List<Vector3>();

        public VillageLayout(Board board)
        {
            var t = board.Topology;
            Home = BoardSpace.Vertex(t, board.HomeCityVertex);
            WarehouseVertex = BoardSpace.Vertex(t, board.WarehouseVertex);
            Out = (WarehouseVertex - Home); Out.y = 0; Out.Normalize();
            Right = new Vector3(Out.z, 0, -Out.x);
            SquareCentre = Home;

            WarehouseDoor = WarehouseVertex + Out * 2f;
            WarehouseYaw = BoardSpace.YawOf(-Out);

            Spawn = Home - Out * 5f - Right * 2f;
            SpawnYaw = BoardSpace.YawOf(Out);

            Storehouse = Home + Right * 21f - Out * 1f;
            StorehouseYaw = BoardSpace.YawOf(-Right);
            Market = Home - Right * 13f - Out * 6f;
            MarketYaw = BoardSpace.YawOf(Right);
            Bell = Home + Right * 6f + Out * 2f;

            MiraSpot = Home - Right * 6f + Out * 1f;
            DoormanSpot = WarehouseDoor - Out * 5f + Right * 2.5f;
            QuartermasterSpot = Storehouse - Right * 6.5f - Out * 2.5f;
            KeeperSpot = Market - Right * 1.6f;
            CaptainSpot = Home + Right * 8f - Out * 10f;
            ScribeSpot = Home - Right * 4f - Out * 12f;

            Clear.Add(new Vector3(Home.x, Home.z, SquareRadius + 3f));
            Clear.Add(new Vector3(Storehouse.x, Storehouse.z, 9f));
            Clear.Add(new Vector3(Market.x, Market.z, 6f));
            // Warehouse footprint (about 32 x 30 m behind the door) as a chain of circles.
            for (int i = 0; i <= 4; i++)
            {
                var c = WarehouseDoor + Out * (i * 7f);
                Clear.Add(new Vector3(c.x, c.z, 15f));
                var l = c + Right * 8f; // bar annex side
                Clear.Add(new Vector3(l.x, l.z, 12f));
            }
            // Path between square and door.
            var mid = (Home + WarehouseDoor) * 0.5f;
            Clear.Add(new Vector3(mid.x, mid.z, 8f));
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
