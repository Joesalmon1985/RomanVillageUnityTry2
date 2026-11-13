using World.Sim.Economy;

namespace World.Sim.Military
{
    /// <summary>Queue Count units at a barracks: their rations and ingots are requested and must be delivered there.</summary>
    public sealed class QueueUnit : Command { public int BarracksId; public int Count = 1; }

    /// <summary>Give a player squad a delegated order: Defend (walk to the flag and hold it) or Attack (walk there and fight).</summary>
    public sealed class OrderSquad : Command { public int SquadId; public OrderKind Order; public int Target; }

    /// <summary>Cancel a squad's order: it stops at the next flag.</summary>
    public sealed class CancelOrder : Command { public int SquadId; }

    /// <summary>Refusal codes of military commands.</summary>
    public static class MilitaryRefusals
    {
        public const string GameOver = "game_over";
        public const string NotOwner = "not_owner";
        public const string NoBarracks = "no_barracks";
        public const string QueueFull = "queue_full";
        public const string BadCount = "bad_count";
        public const string UnknownSquad = "unknown_squad";
        public const string BadOrder = "bad_order";
        public const string BadVertex = "bad_vertex";
        public const string NoOrder = "no_order";
    }

    internal static class MGuard
    {
        internal static CommandResult Basic(WorldState s, Command c)
        {
            if (s.Strategy != null && s.Strategy.GameOver) return CommandResult.Reject(MilitaryRefusals.GameOver, "The game is over.");
            if (c.PlayerId != PlayerIds.Human) return CommandResult.Reject(MilitaryRefusals.NotOwner, "Only the player can do that.");
            return CommandResult.Accept();
        }

        internal static CommandResult Squad(WorldState s, Command c, int id, out Squad sq)
        {
            sq = MilitaryLogic.GetSquad(s, id);
            var r = Basic(s, c);
            if (!r.Ok) return r;
            if (sq == null || !sq.Alive || sq.Owner != PlayerIds.Human || sq.Units.Count == 0)
                return CommandResult.Reject(MilitaryRefusals.UnknownSquad, "That squad does not exist.");
            return CommandResult.Accept();
        }
    }

    internal sealed class QueueUnitHandler : CommandHandler<QueueUnit>
    {
        protected override CommandResult Validate(WorldState s, QueueUnit c)
        {
            var r = MGuard.Basic(s, c);
            if (!r.Ok) return r;
            var b = EconomyApi.GetBuilding(s, c.BarracksId);
            if (b == null || b.Type != BuildingType.Barracks || b.Owner != PlayerIds.Human || b.State != BuildingState.Active)
                return CommandResult.Reject(MilitaryRefusals.NoBarracks, "Units are trained at a finished barracks.");
            if (c.Count <= 0) return CommandResult.Reject(MilitaryRefusals.BadCount, "Queue at least one unit.");
            var q = MilitaryLogic.Queue(s, b.Id, false);
            int len = q == null ? 0 : q.Orders.Count;
            if (len + c.Count > s.Military.Tuning.MaxQueue)
                return CommandResult.Reject(MilitaryRefusals.QueueFull, "The barracks queue holds at most " + s.Military.Tuning.MaxQueue + " units.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, QueueUnit c, EventBuffer ev)
        {
            var q = MilitaryLogic.Queue(s, c.BarracksId, true);
            int rations = MilitaryLogic.UnitRations(s);
            int ingots = s.Military.Tuning.UnitIngots;
            for (int i = 0; i < c.Count; i++)
            {
                q.Orders.Add(new QueuedUnit { Rations = rations, Ingots = ingots });
                if (rations > 0) EconomyApi.RequestWares(s, c.BarracksId, Ware.Rations, rations);
                if (ingots > 0) EconomyApi.RequestWares(s, c.BarracksId, Ware.Ingots, ingots);
            }
            ev.Add(new UnitQueued { BarracksId = c.BarracksId, QueueLength = q.Orders.Count });
            return CommandResult.Accept();
        }
    }

    internal sealed class OrderSquadHandler : CommandHandler<OrderSquad>
    {
        protected override CommandResult Validate(WorldState s, OrderSquad c)
        {
            var r = MGuard.Squad(s, c, c.SquadId, out _);
            if (!r.Ok) return r;
            if (c.Order != OrderKind.Defend && c.Order != OrderKind.Attack)
                return CommandResult.Reject(MilitaryRefusals.BadOrder, "A squad can defend a flag or attack one.");
            if (c.Target < 0 || c.Target >= s.Board.Topology.VertexCount)
                return CommandResult.Reject(MilitaryRefusals.BadVertex, "That is not a flag on the map.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, OrderSquad c, EventBuffer ev)
        {
            var sq = s.Military.Squads[c.SquadId];
            sq.Order = c.Order;
            sq.Target = c.Target;
            ev.Add(new SquadOrdered { SquadId = sq.Id, Order = c.Order, Target = c.Target });
            return CommandResult.Accept();
        }
    }

    internal sealed class CancelOrderHandler : CommandHandler<CancelOrder>
    {
        protected override CommandResult Validate(WorldState s, CancelOrder c)
        {
            var r = MGuard.Squad(s, c, c.SquadId, out var sq);
            if (!r.Ok) return r;
            if (sq.Order == OrderKind.None) return CommandResult.Reject(MilitaryRefusals.NoOrder, "That squad has no order.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CancelOrder c, EventBuffer ev)
        {
            var sq = s.Military.Squads[c.SquadId];
            sq.Order = OrderKind.None;
            sq.Target = -1;
            ev.Add(new SquadOrdered { SquadId = sq.Id, Order = OrderKind.None, Target = -1 });
            return CommandResult.Accept();
        }
    }
}
