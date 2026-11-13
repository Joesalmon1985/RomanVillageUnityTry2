using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Road economy presentation: one call from WorldBootstrap installs the God View toolbar and tools and the
    /// economy HUD (toasts, banners, building actions).
    /// </summary>
    public static class EconomyInstaller
    {
        public static GameObject Install(WorldBootstrap boot)
        {
            var go = new GameObject("Economy");
            go.transform.SetParent(boot.transform, false);
            EconomyGodTools.RegisterDefaults();
            go.AddComponent<GodToolbar>().Bind(boot);
            go.AddComponent<EconomyHud>().Bind(boot);
            return go;
        }
    }
}
