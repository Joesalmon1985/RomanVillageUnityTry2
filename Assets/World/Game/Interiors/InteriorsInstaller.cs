namespace World.Game
{
    /// <summary>
    /// Package I5's entry point (PRD_CRYSTALS appendix B): interiors in the game (open walk-grid regions, stairs and
    /// fades, interior lights, the interior map, "Lead me out"). Called by <see cref="WorldBootstrap"/> after
    /// <see cref="PrinceInstaller"/> and before <see cref="ExpeditionInstaller"/>. Creates the
    /// <see cref="InteriorDirector"/> (which builds the running campaign's interiors at once, so a save made underground
    /// puts the player back in place), the interior lighting, the interior map layer and the AutoPlay script
    /// <c>quarry_walk</c>. With no campaign running it builds nothing and changes nothing. Owner: I5
    /// (all of <c>Game/Interiors/**</c>).
    /// </summary>
    public static class InteriorsInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            InteriorDirector.Create(boot);
            InteriorScripts.Register();
        }
    }
}
