namespace World.Game
{
    /// <summary>
    /// Package P's F1 developer tools (God View camera and toolbar on the new commands; REFACTOR_SPEC 2.D, 8.2): adds the
    /// <see cref="GodToolbar"/> and registers <see cref="DevTools"/>. Called last by <see cref="WorldBootstrap"/>.
    /// Owner: package P.
    /// </summary>
    public static class DevToolsInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            DevTools.Register();
            var bar = boot.gameObject.AddComponent<GodToolbar>();
            bar.Bind(boot);
        }
    }
}
