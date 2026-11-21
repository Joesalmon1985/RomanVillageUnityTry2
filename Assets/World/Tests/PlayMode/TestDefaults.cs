using NUnit.Framework;

namespace World.Game.Tests
{
    /// <summary>
    /// Opening vista (21 Nov 2026): a new game is now the god board hub opening on the vista. The PlayMode tests written
    /// before that load the World scene expecting the legacy prehistoric economy and the walking view, so the whole test
    /// namespace runs with the legacy default and no vista. <see cref="OpeningVistaTests"/> switches both back for its own tests.
    /// </summary>
    [SetUpFixture]
    public sealed class TestDefaults
    {
        [OneTimeSetUp]
        public void Before() { WorldBootstrap.DefaultStart = DevStart.LegacyId; OpeningVista.Mode = VistaMode.Off; }

        [OneTimeTearDown]
        public void After() { WorldBootstrap.DefaultStart = GodBoardStart.Id; OpeningVista.Mode = VistaMode.On; }
    }
}
