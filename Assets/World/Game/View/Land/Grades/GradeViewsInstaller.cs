namespace World.Game
{
    /// <summary>
    /// Package H1-5's entry point (PRD_AGES 11.1): grade visuals for the 16 building types and the town, the outer
    /// plot rings, Historic dressing by grade. Called by <see cref="WorldBootstrap"/> right after
    /// <see cref="LandViewsInstaller"/> (so <see cref="LandViews.Instance"/> exists) and before the lighting.
    /// <b>Shell</b> (historic base, 14 Nov 2026): does nothing yet; buildings of grade 1 and towns look as before.
    /// The way in without editing the land views: subscribe to <see cref="LandViews.Synced"/> and dress
    /// <see cref="LandViews.Buildings"/> and <see cref="LandViews.Settlements"/> from <c>Building.Grade</c>,
    /// <c>Building.Upgrading</c> and <c>Settlement.IsTown</c>; rebuild on <see cref="LandViews.RebuildFromState"/>
    /// (a view is recreated when its <c>BuildingId</c> object changes). The ground of a town's outer plots is already
    /// cleared by <c>LandViews</c>. Owner: H1-5 (all of <c>Game/View/Land/Grades/**</c>).
    /// </summary>
    public static class GradeViewsInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            // H1-5: var views = LandViews.Instance; views.Synced += events => ...;
        }
    }
}
