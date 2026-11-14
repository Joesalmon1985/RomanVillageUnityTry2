using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package L's entry point (Docs/LIGHTING.md), called by <see cref="WorldBootstrap"/> after the land views:
    /// the day-night cycle (<see cref="DayNight"/>), the light registry and budget (<see cref="WorldLights"/>) and the
    /// decorator that gives the other packages' views their fires, torches and night-readable materials
    /// (<see cref="LightDecorator"/>).
    /// </summary>
    public static class LightingInstaller
    {
        public static Transform Root { get; private set; }

        public static void Install(WorldBootstrap boot)
        {
            Root = new GameObject("Lighting").transform;
            Root.SetParent(boot.transform, false);
            DayNight.Create(boot);
            WorldLights.Create(Root);
            LightDecorator.Create(boot, Root);
        }
    }
}
