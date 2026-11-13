namespace World.Sim.Economy
{
    // Direct build commands (REFACTOR_SPEC 5.1): used by the F1 developer tools, tests and CastSpell. PlayerId = people.

    /// <summary>Building site on a plot.</summary>
    public sealed class PlaceBuilding : Command { public BuildingType Type; public int Plot; }

    /// <summary>Main-road site on a hex edge; the source settlement is chosen by rule.</summary>
    public sealed class PlaceRoad : Command { public int HexEdge; }

    /// <summary>Settlement site on a corner; the source settlement is chosen by rule.</summary>
    public sealed class FoundSettlement : Command { public int Corner; }

    /// <summary>City upgrade of the settlement on a corner.</summary>
    public sealed class UpgradeCity : Command { public int Corner; }

    /// <summary>Cancels an unfinished site; delivered goods go back to the source storehouse. City: Id = settlement id.</summary>
    public sealed class CancelSite : Command { public SiteKind Kind; public int Id; }

    /// <summary>Refusal codes of the economy commands (REFACTOR_SPEC 5.2) and their exact sentences.</summary>
    public static class EconomyRefusals
    {
        public const string GameOver = "game_over";
        public const string NotOwner = "not_owner";
        public const string UnknownType = "unknown_type";
        public const string NotPlaceable = "not_placeable";
        public const string UnknownPlot = "unknown_plot";
        public const string NoSettlement = "no_settlement";
        public const string PlotTaken = "plot_taken";
        public const string PlotLocked = "plot_locked";
        public const string PlotReserved = "plot_reserved";
        public const string WrongTerrain = "wrong_terrain";
        public const string AlreadyBuilt = "already_built";
        public const string BadCorner = "bad_corner";
        public const string Occupied = "occupied";
        public const string TooClose = "too_close";
        public const string NotConnected = "not_connected";
        public const string RoadExists = "road_exists";
        public const string BlockedByRival = "blocked_by_rival";
        public const string NotASettlement = "not_a_settlement";
        public const string AlreadyCity = "already_city";
        public const string UpgradeInProgress = "upgrade_in_progress";
        public const string NotASite = "not_a_site";

        /// <summary>The exact sentence for a code; args fill {Name}/{name}/{terrain} in order.</summary>
        public static string Sentence(string code, params string[] args)
        {
            string a0 = args != null && args.Length > 0 ? args[0] : "";
            string a1 = args != null && args.Length > 1 ? args[1] : "";
            switch (code)
            {
                case GameOver: return "The game is over.";
                case NotOwner: return "That belongs to another people.";
                case UnknownType: return "There is no such building.";
                case NotPlaceable: return a0 + " comes with a camp and cannot be built.";
                case UnknownPlot: return "There is no plot there.";
                case NoSettlement: return "None of your camps owns that plot.";
                case PlotTaken: return "Something already stands on that plot.";
                case PlotLocked: return "That plot opens when the camp becomes a city.";
                case PlotReserved: return "That ground is kept for the dance circle.";
                case WrongTerrain: return "A " + a0 + " must stand by " + a1 + ".";
                case AlreadyBuilt: return "This camp already has a " + a0 + ".";
                case BadCorner: return "A camp needs a corner where three lands meet.";
                case Occupied: return "A camp already stands there.";
                case TooClose: return "Too close to another camp: leave a road's length between camps.";
                case NotConnected: return "None of your roads reaches there.";
                case RoadExists: return "A road already runs there.";
                case BlockedByRival: return "A camp of another people blocks that road.";
                case NotASettlement: return "There is no camp of yours there.";
                case AlreadyCity: return "This camp is already a city.";
                case UpgradeInProgress: return "This camp is already being raised to a city.";
                case NotASite: return "There is no unfinished site there.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code, params string[] args) => CommandResult.Reject(code, Sentence(code, args));
    }

    internal sealed class PlaceBuildingHandler : CommandHandler<PlaceBuilding>
    {
        protected override CommandResult Validate(WorldState s, PlaceBuilding c) => EconomyApi.CheckBuilding(s, c.PlayerId, c.Type, c.Plot);

        protected override CommandResult Apply(WorldState s, PlaceBuilding c, EventBuffer ev)
        {
            EconomyLogic.PlaceBuildingSite(s, c.PlayerId, c.Type, c.Plot, ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class PlaceRoadHandler : CommandHandler<PlaceRoad>
    {
        protected override CommandResult Validate(WorldState s, PlaceRoad c) => EconomyApi.CheckRoad(s, c.PlayerId, c.HexEdge);

        protected override CommandResult Apply(WorldState s, PlaceRoad c, EventBuffer ev)
        {
            EconomyLogic.PlaceRoadSite(s, c.PlayerId, c.HexEdge, ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class FoundSettlementHandler : CommandHandler<FoundSettlement>
    {
        protected override CommandResult Validate(WorldState s, FoundSettlement c) => EconomyApi.CheckSettlement(s, c.PlayerId, c.Corner);

        protected override CommandResult Apply(WorldState s, FoundSettlement c, EventBuffer ev)
        {
            EconomyLogic.PlaceSettlementSite(s, c.PlayerId, c.Corner, ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class UpgradeCityHandler : CommandHandler<UpgradeCity>
    {
        protected override CommandResult Validate(WorldState s, UpgradeCity c) => EconomyApi.CheckCity(s, c.PlayerId, c.Corner);

        protected override CommandResult Apply(WorldState s, UpgradeCity c, EventBuffer ev)
        {
            EconomyLogic.PlaceCitySite(s, c.PlayerId, c.Corner, ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class CancelSiteHandler : CommandHandler<CancelSite>
    {
        protected override CommandResult Validate(WorldState s, CancelSite c) => EconomyLogic.CheckCancel(s, c.PlayerId, c.Kind, c.Id);

        protected override CommandResult Apply(WorldState s, CancelSite c, EventBuffer ev)
        {
            EconomyLogic.Cancel(s, c.Kind, c.Id, ev);
            return CommandResult.Accept();
        }
    }
}
