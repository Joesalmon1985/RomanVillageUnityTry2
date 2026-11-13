using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package V's entry point (REFACTOR_SPEC 8.0, 8.4, appendix B): terrain and land views. Called third by
    /// <see cref="WorldBootstrap"/>. Owner: package V.
    ///
    /// F-0: not quite empty. It builds the sky and the terrain (<see cref="TerrainBuilder"/>, also V's), so V can change
    /// TerrainBuilder's signature without touching WorldBootstrap. The clearings are interim: a 12 m circle round each
    /// starting corner.
    /// </summary>
    public static class LandViewsInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            var board = boot.Sim.State.Board;
            var t = board.Topology;
            TerrainBuilder.BuildSky(boot.transform, boot.Sun, boot.MainCamera);
            var clear = new System.Collections.Generic.List<Vector3>();
            foreach (int c in board.StartCorners)
            {
                var p = BoardSpace.Vertex(t, c);
                clear.Add(new Vector3(p.x, p.z, 12f));
            }
            TerrainBuilder.Build(boot.transform, board, clear, out var overlay);
            if (boot.Views != null) boot.Views.AddGodViewOnly(overlay);
        }
    }
}
