using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Ports on the Meadow rim as a jetty with a trader's boat (god board plan AB-4). A picture sign says the trade: a
    /// ring of all five goods for 3:1, the one good for 2:1. Pictures only, never a number (AB-1). Original primitives.
    /// </summary>
    public static class PortMarkers
    {
        public static Color GoodColor(Ware w)
        {
            switch (w)
            {
                case Ware.Wood: return new Color(0.45f, 0.28f, 0.14f);
                case Ware.Clay: return new Color(0.72f, 0.34f, 0.20f);
                case Ware.Grain: return new Color(0.92f, 0.78f, 0.25f);
                case Ware.Sheep: return new Color(0.97f, 0.96f, 0.92f);
                default: return new Color(0.55f, 0.55f, 0.58f);
            }
        }

        /// <summary>Builds every port of the board under a new "Ports" object (one merged mesh) and returns it.</summary>
        public static GameObject Build(Board board, Transform parent, Material mat)
        {
            var root = new GameObject("Ports");
            root.transform.SetParent(parent, false);
            var mb = new MeshBuilder();
            var t = board.Topology;
            for (int i = 0; i < board.Ports.Count; i++)
            {
                var p = board.Ports[i];
                var c = BoardSpace.Hex(t, p.Hex);
                var dir = c; dir.y = 0;
                dir = dir.sqrMagnitude < 1f ? Vector3.forward : dir.normalized;
                AddPort(mb, c + dir * (BoardSpace.HexSize * 0.97f), dir, p.Good, new PropRandom(777 + i));
            }
            if (mb.VertexCount > 0) mb.ToObject("PortMesh", root.transform, mat);
            return root;
        }

        /// <summary>One jetty: planks run out from <paramref name="start"/> along <paramref name="dir"/>, boat moored at the end, sign at the root.</summary>
        public static void AddPort(MeshBuilder mb, Vector3 start, Vector3 dir, Ware? good, PropRandom rnd)
        {
            float yaw = BoardSpace.YawOf(dir);
            var side = new Vector3(dir.z, 0, -dir.x);
            var plank = new Color(0.55f, 0.40f, 0.24f); var post = new Color(0.30f, 0.20f, 0.12f);
            const float length = 100f, width = 6f;
            for (int k = 0; k < 20; k++)
            {
                var p = start + dir * (k * length / 20f + 2f) + Vector3.up * 0.7f;
                mb.Box(p, new Vector3(width, 0.35f, length / 20f * 0.85f), plank * rnd.Range(0.9f, 1.08f), yaw);
            }
            for (int k = 0; k <= 5; k++)
                foreach (float s in new[] { -1f, 1f })
                    mb.Cylinder(start + dir * (k * length / 5f) + side * (s * width * 0.5f), 0.45f, 0.4f, 2.2f, 6, post);
            // Trader's boat alongside the far end: hull, mast and a square sail.
            var bc = start + dir * (length - 12f) + side * 10f;
            mb.Box(bc + Vector3.up * 0.9f, new Vector3(5f, 1.8f, 16f), new Color(0.40f, 0.26f, 0.16f), yaw);
            mb.Box(bc + dir * 8.5f + Vector3.up * 1.2f, new Vector3(3f, 1.4f, 3f), new Color(0.40f, 0.26f, 0.16f), yaw + 45f);
            mb.Cylinder(bc + Vector3.up * 1.8f, 0.3f, 0.25f, 11f, 6, post);
            mb.Box(bc + Vector3.up * 8f + side * 0.1f, new Vector3(0.25f, 5.5f, 8f), new Color(0.93f, 0.88f, 0.74f), yaw);
            // Sign on a post at the shore end, a flat board facing the sea so the god camera reads it.
            var sp = start + side * 6f;
            mb.Cylinder(sp, 0.35f, 0.3f, 8f, 6, post);
            mb.Box(sp + Vector3.up * 9f, new Vector3(8f, 0.5f, 8f), new Color(0.93f, 0.90f, 0.80f), yaw);
            var top = sp + Vector3.up * 9.4f;
            if (good.HasValue) Icon(mb, top, good.Value, 2.6f, yaw);
            else
            {
                // Any good at 3:1: all five goods in a ring.
                var wares = new[] { Ware.Wood, Ware.Clay, Ware.Grain, Ware.Sheep, Ware.Stone };
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 2f / 5f;
                    Icon(mb, top + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.6f, wares[k], 1.2f, yaw + k * 20f);
                }
            }
        }

        private static void Icon(MeshBuilder mb, Vector3 p, Ware w, float s, float yaw)
        {
            var c = GoodColor(w);
            switch (w)
            {
                case Ware.Wood: // two logs
                    mb.Box(p + Vector3.up * s * 0.3f + new Vector3(0, 0, -s * 0.5f), new Vector3(s * 2.2f, s * 0.6f, s * 0.7f), c, yaw);
                    mb.Box(p + Vector3.up * s * 0.3f + new Vector3(0, 0, s * 0.5f), new Vector3(s * 2.2f, s * 0.6f, s * 0.7f), c * 0.85f, yaw);
                    break;
                case Ware.Clay: mb.Box(p + Vector3.up * s * 0.4f, new Vector3(s * 2f, s * 0.8f, s * 1.1f), c, yaw); break; // brick
                case Ware.Grain: // wheat stook
                    mb.Cone(p, s * 0.9f, s * 2f, 6, c);
                    break;
                case Ware.Sheep: // woolly lump
                    mb.Box(p + Vector3.up * s * 0.5f, new Vector3(s * 1.6f, s, s * 1.2f), c, yaw);
                    mb.Box(p + Vector3.up * s * 0.6f + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, s * 0.9f), new Vector3(s * 0.6f, s * 0.6f, s * 0.6f), new Color(0.16f, 0.14f, 0.13f), yaw);
                    break;
                default: mb.Rock(p, s, c, () => 0.5f); break; // stone
            }
        }
    }
}
