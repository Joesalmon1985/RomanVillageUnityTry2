namespace World.Game
{
    /// <summary>
    /// Package I5's entry point (PRD_CRYSTALS appendix B): interiors in the game (open walk-grid regions, stairs and
    /// fades, interior lights, the interior map, "Lead me out"). Called by <see cref="WorldBootstrap"/> after
    /// <see cref="PrinceInstaller"/> and before <see cref="ExpeditionInstaller"/>. <b>I0 shell</b> (14 Nov 2026): does
    /// nothing yet. Owner: I5 (all of <c>Game/Interiors/**</c>).
    /// </summary>
    public static class InteriorsInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            // I5: build the interiors of the running campaign (Interiors.Build) and follow Interiors.Changed.
        }
    }
}
