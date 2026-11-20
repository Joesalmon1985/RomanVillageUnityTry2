using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// WU-1a: builds the hub of the world-first UI on a dealt (god board) island: a level green clearing round the home camp and
    /// each destination (temple forecourt, council steward, dance circle's marker, healer's lodge, Mira's expedition camp, duel
    /// ring), each tagged with a <see cref="HubZoneTag"/>. Called after the council director so the tower is known. Legacy games
    /// get nothing (<see cref="CampLayout.Hub"/> is false). Reads the board; never touches state. Owner: WU-1.
    /// </summary>
    public static class HubInstaller
    {
        public static Transform Root { get; private set; }
        public static CampLayout Layout { get; private set; }
        public const float CellSize = 6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Root = null; Layout = null; }

        public static void Install(WorldBootstrap boot)
        {
            var layout = new CampLayout(boot.Sim.State.Board);
            if (!layout.Hub) return;
            Layout = layout;
            Root = new GameObject("Hub").transform;
            Root.SetParent(boot.transform, false);
            BuildGround(Root, layout);

            if (layout.TryPlace(HubZoneTag.Temple, out var temple))
            {
                TempleForecourt.Build(Root, boot, temple);
                HubZoneTag.Add(Root, HubZoneTag.Temple, temple.Position, temple.Radius);
            }
            var dir = CouncilDirector.Instance;
            if (dir != null && CouncilSteward.Build(Root, boot) != null)
                HubZoneTag.Add(Root, HubZoneTag.Council, dir.Site.Position, 8f);
            HubZoneTag.Add(Root, HubZoneTag.Rhythm, layout.DanceCircleCentre, 8f);
            if (layout.TryPlace(HubZoneTag.Healer, out var lodge))
            {
                HealersLodge.Build(Root, boot, lodge);
                HubZoneTag.Add(Root, HubZoneTag.Healer, lodge.Position, lodge.Radius);
            }
            ExpeditionCamp.Build(Root, layout);
            HubZoneTag.Add(Root, HubZoneTag.Cave, layout.MiraSpot, 6f);
            if (layout.TryPlace(HubZoneTag.Duel, out var ring))
            {
                DuelRing.Build(Root, ring, layout);
                HubZoneTag.Add(Root, HubZoneTag.Duel, ring.Position, ring.Radius);
            }
        }

        /// <summary>One level, plain green ground over the island part of the hub radius (cells whose corners are all on the island).</summary>
        private static void BuildGround(Transform root, CampLayout layout)
        {
            var t = layout.Hub ? WorldBootstrap.Instance.Sim.State.Board.Topology : null;
            if (t == null) return;
            var mb = new MeshBuilder();
            var rnd = new PropRandom(4242);
            var c = layout.Hearth;
            int n = Mathf.CeilToInt(CampLayout.HubRadius / CellSize);
            const float y = 0.012f;
            for (int i = -n; i < n; i++)
                for (int j = -n; j < n; j++)
                {
                    float x0 = c.x + i * CellSize, z0 = c.z + j * CellSize, x1 = x0 + CellSize, z1 = z0 + CellSize;
                    float mx = (x0 + x1) * 0.5f - c.x, mz = (z0 + z1) * 0.5f - c.z;
                    if (mx * mx + mz * mz > CampLayout.HubRadius * CampLayout.HubRadius) continue;
                    if (t.HexAt(x0, z0) < 0 || t.HexAt(x1, z0) < 0 || t.HexAt(x0, z1) < 0 || t.HexAt(x1, z1) < 0 || t.HexAt((x0 + x1) * 0.5f, (z0 + z1) * 0.5f) < 0) continue;
                    var col = Color.Lerp(HubArt.Grass, HubArt.Grass * 1.12f, rnd.Next() * 0.5f);
                    mb.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), col);
                }
            if (mb.VertexCount == 0) return;
            var go = mb.ToObject("Hub ground", root, MaterialFactory.CreateVertexColored(Color.white, "Hub ground"));
            go.layer = 2;
        }
    }
}
