namespace World.Sim.Economy
{
    /// <summary>
    /// Build a road on one segment (BoardTopology fine edge). Must touch the player's network. Every
    /// Tuning.RoadSegmentsPerCost segments cost Tuning.RoadCost from the storehouse, paid on the first of them.
    /// </summary>
    public sealed class PlaceRoad : Command { public int Edge; }

    /// <summary>Remove a road. Carriers on it stop at the next flag and wait for a route.</summary>
    public sealed class RemoveRoad : Command { public int Edge; }

    /// <summary>Plan a building: becomes a construction site that requests its cost wares.</summary>
    public sealed class PlaceBuilding : Command { public BuildingType Type; public int Vertex; }

    /// <summary>Cancel a construction site (Site or Constructing). Delivered wares return to the storehouse at once.</summary>
    public sealed class CancelSite : Command { public int BuildingId; }

    /// <summary>Order in which a building's deliveries are served.</summary>
    public sealed class SetPriority : Command { public int BuildingId; public Priority Priority; }

    /// <summary>Switch an active building on or off (off: no work, no input requests, reason disabled).</summary>
    public sealed class ToggleBuilding : Command { public int BuildingId; public bool Enabled; }

    /// <summary>End the round: OnRoundEnding hooks run, Round increments, and the next round starts this tick.</summary>
    public sealed class EndRound : Command { }

    internal static class Guard
    {
        internal static CommandResult Player(Command c) =>
            c.PlayerId == PlayerIds.Human
                ? CommandResult.Accept()
                : CommandResult.Reject(EconomyRefusals.NotOwner, "Only the player's village can do that.");

        internal static CommandResult Own(WorldState s, Command c, int id, out Building b)
        {
            b = EconomyLogic.Get(s, id);
            var p = Player(c);
            if (!p.Ok) return p;
            if (!EconomyLogic.IsLive(b))
                return CommandResult.Reject(EconomyRefusals.UnknownBuilding, "That building does not exist.");
            if (b.Owner != c.PlayerId) return CommandResult.Reject(EconomyRefusals.NotOwner, "That building is not yours.");
            return CommandResult.Accept();
        }
    }

    internal sealed class PlaceRoadHandler : CommandHandler<PlaceRoad>
    {
        protected override CommandResult Validate(WorldState s, PlaceRoad c)
        {
            var p = Guard.Player(c);
            if (!p.Ok) return p;
            var topo = s.Board.Topology;
            if (c.Edge < 0 || c.Edge >= topo.EdgeCount)
                return CommandResult.Reject(EconomyRefusals.BadPlace, "That is not a road place on the map.");
            if (s.Economy.Roads[c.Edge])
                return CommandResult.Reject(EconomyRefusals.RoadExists, "There is already a road there.");
            int a = topo.EdgeVertexA(c.Edge), b = topo.EdgeVertexB(c.Edge);
            if (!EconomyLogic.OnNetwork(s, c.PlayerId, a) && !EconomyLogic.OnNetwork(s, c.PlayerId, b))
                return CommandResult.Reject(EconomyRefusals.NotConnected, "A road must connect to your roads, city or buildings.");
            var cost = EconomyLogic.RoadCostFor(s, 1);
            if (!EconomyLogic.CanPay(EconomyLogic.Storehouse(s), cost))
                return CommandResult.Reject(EconomyRefusals.NotEnoughWares,
                    "A road needs " + EconomyLogic.CostText(cost) + " in the storehouse (it pays for "
                    + s.Economy.Tuning.RoadSegmentsPerCost + " segments).");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, PlaceRoad c, EventBuffer ev)
        {
            EconomyLogic.PayRoadSegment(s);
            s.Economy.Roads[c.Edge] = true;
            ev.Add(new RoadPlaced { Edge = c.Edge, PlayerId = c.PlayerId });
            return CommandResult.Accept();
        }
    }

    internal sealed class RemoveRoadHandler : CommandHandler<RemoveRoad>
    {
        protected override CommandResult Validate(WorldState s, RemoveRoad c)
        {
            var p = Guard.Player(c);
            if (!p.Ok) return p;
            if (c.Edge < 0 || c.Edge >= s.Economy.Roads.Length || !s.Economy.Roads[c.Edge])
                return CommandResult.Reject(EconomyRefusals.NoRoadHere, "There is no road there to remove.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, RemoveRoad c, EventBuffer ev)
        {
            s.Economy.Roads[c.Edge] = false;
            ev.Add(new RoadRemoved { Edge = c.Edge, PlayerId = c.PlayerId });
            return CommandResult.Accept();
        }
    }

    internal sealed class PlaceBuildingHandler : CommandHandler<PlaceBuilding>
    {
        protected override CommandResult Validate(WorldState s, PlaceBuilding c) =>
            EconomyLogic.CheckPlacement(s, c.PlayerId, c.Type, c.Vertex);

        protected override CommandResult Apply(WorldState s, PlaceBuilding c, EventBuffer ev)
        {
            var b = EconomyLogic.AddBuilding(s, c.Type, c.PlayerId, c.Vertex, BuildingState.Site);
            ev.Add(new SitePlaced { BuildingId = b.Id, Type = b.Type, Vertex = b.Vertex });
            return CommandResult.Accept();
        }
    }

    internal sealed class CancelSiteHandler : CommandHandler<CancelSite>
    {
        protected override CommandResult Validate(WorldState s, CancelSite c)
        {
            var r = Guard.Own(s, c, c.BuildingId, out var b);
            if (!r.Ok) return r;
            if (b.State != BuildingState.Site && b.State != BuildingState.Constructing)
                return CommandResult.Reject(EconomyRefusals.NotASite, "Only construction sites can be cancelled.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CancelSite c, EventBuffer ev)
        {
            EconomyLogic.CancelSite(s, s.Economy.Buildings[c.BuildingId], ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class SetPriorityHandler : CommandHandler<SetPriority>
    {
        protected override CommandResult Validate(WorldState s, SetPriority c)
        {
            var r = Guard.Own(s, c, c.BuildingId, out _);
            if (!r.Ok) return r;
            if (c.Priority < Priority.Low || c.Priority > Priority.High)
                return CommandResult.Reject(EconomyRefusals.BadPlace, "That is not a priority.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, SetPriority c, EventBuffer ev)
        {
            s.Economy.Buildings[c.BuildingId].Priority = c.Priority;
            return CommandResult.Accept();
        }
    }

    internal sealed class ToggleBuildingHandler : CommandHandler<ToggleBuilding>
    {
        protected override CommandResult Validate(WorldState s, ToggleBuilding c)
        {
            var r = Guard.Own(s, c, c.BuildingId, out var b);
            if (!r.Ok) return r;
            if (EconomyLogic.Def(s, b).Kind == BuildingKind.Store)
                return CommandResult.Reject(EconomyRefusals.NotToggleable, "The storehouse cannot be switched off.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, ToggleBuilding c, EventBuffer ev)
        {
            var b = s.Economy.Buildings[c.BuildingId];
            if (b.Enabled == c.Enabled) return CommandResult.Accept();
            b.Enabled = c.Enabled;
            ev.Add(new BuildingStateChanged { BuildingId = b.Id, State = b.State, Enabled = b.Enabled, Reason = b.Reason, ReasonWare = b.ReasonWare });
            return CommandResult.Accept();
        }
    }

    internal sealed class EndRoundHandler : CommandHandler<EndRound>
    {
        private readonly EconomyHooks _hooks;
        public EndRoundHandler(EconomyHooks hooks) { _hooks = hooks; }

        protected override CommandResult Validate(WorldState s, EndRound c)
        {
            var p = Guard.Player(c);
            if (!p.Ok) return p;
            if (s.Economy.RoundStartPending)
                return CommandResult.Reject(EconomyRefusals.RoundStarting, "The new round is still starting.");
            foreach (var g in _hooks.EndRoundGuards)
            {
                var r = g(s);
                if (!r.Ok) return r;
            }
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, EndRound c, EventBuffer ev)
        {
            foreach (var a in _hooks.OnRoundEnding) a(s, ev);
            s.Economy.Round++;
            s.Economy.RoundStartPending = true;
            return CommandResult.Accept();
        }
    }
}
