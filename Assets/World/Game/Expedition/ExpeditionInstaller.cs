namespace World.Game
{
    /// <summary>
    /// Package I6's entry point (PRD_CRYSTALS appendix B): the expedition director (Mira's loop, the six companions,
    /// the journal, the map layer, site and tower views). Called by <see cref="WorldBootstrap"/> after
    /// <see cref="InteriorsInstaller"/> and before <see cref="DevToolsInstaller"/>. <b>I0 shell</b> (14 Nov 2026): does
    /// nothing yet, so a game with a running campaign (dev.historic plus StartCampaign) shows no campaign views.
    /// Owner: I6 (all of <c>Game/Expedition/**</c>; from cry-J0 the set-piece packages add <c>Sites/**</c>).
    /// </summary>
    public static class ExpeditionInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            // I6: create the ExpeditionDirector when Sim.State.Campaign is not null (and when StartCampaign succeeds),
            // add its map layer with MapScreen.AddLayer, and take over Mira's dialogue through Npc.Options.
        }
    }
}
