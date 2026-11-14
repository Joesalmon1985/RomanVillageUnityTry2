using UnityEngine;

namespace World.Game
{
    /// <summary>What the map shows (PRD_CRYSTALS appendix B, I5): the island, or the interior the player is in.</summary>
    public enum MapMode { Island = 0, Interior = 1 }

    /// <summary>
    /// Extra content on the map screen (PRD_CRYSTALS appendix B, I5): the expedition's party and sites (I6), later the
    /// ages' map taps (H2-7). Added with <see cref="MapScreen.AddLayer"/>; packages never edit <c>MapScreen.cs</c>.
    /// <see cref="Draw"/> runs inside the map's OnGUI after the built-in content and before the player's arrow; use
    /// <see cref="MapScreenView.WorldToGui"/>. <see cref="Tap"/> gets a tap on the map first: return true to take it
    /// (the walk target is then not set). Created by crystals I0 (Unity half); owner I5.
    /// </summary>
    public interface IMapLayer
    {
        void Draw(MapScreenView view);
        bool Tap(Vector3 world);
    }
}
