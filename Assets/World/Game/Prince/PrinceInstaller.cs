using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package N's entry point (REFACTOR_SPEC 8.0, 8.5, appendix B): the prince, the sawyer and the merchant, the pitch
    /// loop and the first scene (<see cref="PrinceDirector"/>), the AutoPlay scripts <c>first_scene</c>,
    /// <c>follow_prince</c>, <c>prompt_game</c>, <c>ignore_prince</c> and <c>prince_captures</c>
    /// (<see cref="PrinceScripts"/>), and, while package V's land views are missing, N's placeholder views
    /// (<see cref="NPlaceholderViews"/>). Called fourth by <see cref="WorldBootstrap"/>.
    /// </summary>
    public static class PrinceInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            NPlaceholderViews.InstallIfNeeded(boot);
            var go = new GameObject("Prince director");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<PrinceDirector>();
            d.Begin(boot);
            PrinceScripts.Register();
        }
    }
}
