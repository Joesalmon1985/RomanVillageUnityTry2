using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package N's entry point (REFACTOR_SPEC 8.0, 8.5, appendix B): the prince, the sawyer and the merchant, the pitch
    /// loop and the first scene (<see cref="PrinceDirector"/>), the AutoPlay scripts <c>first_scene</c>,
    /// <c>follow_prince</c>, <c>prompt_game</c>, <c>ignore_prince</c> and <c>prince_captures</c>
    /// (<see cref="PrinceScripts"/>). The land views are package V's (<see cref="LandViews"/>). Called by
    /// <see cref="WorldBootstrap"/> after the lighting.
    /// </summary>
    public static class PrinceInstaller
    {
        /// <summary>
        /// True runs the world without the prince and his villagers (tests and the "cast_all" AutoPlay script, which
        /// plays the queue by itself; also the command line switch <c>-noprince</c>). Reset on every domain load.
        /// </summary>
        public static bool Disabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Disabled = false;

        public static bool NoPrince =>
            Disabled || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noprince") >= 0;

        public static void Install(WorldBootstrap boot)
        {
            PrinceScripts.Register();
            if (NoPrince) return;
            var go = new GameObject("Prince director");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<PrinceDirector>();
            d.Begin(boot);
        }
    }
}
