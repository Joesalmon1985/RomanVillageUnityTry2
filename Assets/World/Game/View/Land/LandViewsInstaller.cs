using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package V's entry point (REFACTOR_SPEC 8.0, 8.4, appendix B), called third by <see cref="WorldBootstrap"/>:
    /// the sky, the terrain (<see cref="TerrainBuilder"/>, with a clearing for the stock patch at every hex centre; the
    /// camps' clearings follow the state) and <see cref="LandViews"/>. Owner: package V.
    /// </summary>
    public static class LandViewsInstaller
    {
        /// <summary>Radius kept free of trees, rocks and crops round every hex centre (the 6 m stock patch and its fence).</summary>
        public const float PatchClearing = 8.5f;

        public static void Install(WorldBootstrap boot)
        {
            var board = boot.Sim.State.Board;
            var t = board.Topology;
            TerrainBuilder.BuildSky(boot.transform, boot.Sun, boot.MainCamera);
            var clear = new List<Vector3>();
            for (int h = 0; h < t.HexCount; h++)
            {
                var c = BoardSpace.Hex(t, h);
                clear.Add(new Vector3(c.x, c.z, PatchClearing));
            }
            TerrainBuilder.Build(boot.transform, board, clear, out var overlay);
            if (boot.Views != null) boot.Views.AddGodViewOnly(overlay);
            LandViews.Create(boot);
        }
    }
}
