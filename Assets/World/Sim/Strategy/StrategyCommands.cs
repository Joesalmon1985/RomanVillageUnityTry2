using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>Found a city on a flag touching one of your roads (2 VP; extends territory and batches).</summary>
    public sealed class FoundCity : Command { public int Vertex; }

    /// <summary>Market: give TradeRate x Count of Give from the storehouse for Count of Get.</summary>
    public sealed class Trade : Command { public Ware Give; public Ware Get; public int Count = 1; }

    /// <summary>Market: sell Count wares from the storehouse for coin (SellWaresPerCoin wares per coin).</summary>
    public sealed class Sell : Command { public Ware Ware; public int Count; }

    /// <summary>Market: buy Count wares into the storehouse for BuyPrice coin each.</summary>
    public sealed class Buy : Command { public Ware Ware; public int Count = 1; }

    /// <summary>Move the hero to an adjacent flag (uses one move this round); arriving clears all demons there.</summary>
    public sealed class MoveHero : Command { public int Vertex; }

    /// <summary>Take a card from your draft hand.</summary>
    public sealed class DraftPick : Command { public int CardId; }

    /// <summary>Build a card from your hand at one of your cities (pays its cost; max 3 per city).</summary>
    public sealed class BuildCard : Command { public int CardId; public int CityVertex; }

    /// <summary>Discard a card from your hand for coin.</summary>
    public sealed class DiscardCard : Command { public int CardId; }

    /// <summary>Order the next wonder stage: its wares are requested to the wonder site and the stage completes on delivery.</summary>
    public sealed class BuildWonderStage : Command { }

    /// <summary>Refusal codes of strategy commands.</summary>
    public static class StrategyRefusals
    {
        public const string GameOver = "game_over";
        public const string NotOwner = "not_owner";
        public const string BadVertex = "bad_vertex";
        public const string Occupied = "occupied";
        public const string TooClose = "too_close";
        public const string NoRoad = "no_road";
        public const string CannotAfford = "cannot_afford";
        public const string SameWare = "same_ware";
        public const string BadCount = "bad_count";
        public const string NoTurnAction = Reasons.NoTurnAction;
        public const string NotAdjacent = "not_adjacent";
        public const string NotDrafting = "not_drafting";
        public const string NotInDraft = "not_in_draft";
        public const string NotInHand = "not_in_hand";
        public const string NoCity = "no_city";
        public const string CityFull = "city_full";
        public const string NoWonder = "no_wonder";
        public const string StageInProgress = "stage_in_progress";
        public const string WonderComplete = "wonder_complete";
        public const string DraftPending = "draft_pending";
        /// <summary>Point-scoring actions (cities, wonder stages) wait for Age I, which begins after the commission.</summary>
        public const string Prologue = "prologue";
    }

    internal static class SGuard
    {
        internal static CommandResult Basic(WorldState s, Command c)
        {
            if (s.Strategy.GameOver) return CommandResult.Reject(StrategyRefusals.GameOver, "The game is over.");
            if (c.PlayerId != PlayerIds.Human) return CommandResult.Reject(StrategyRefusals.NotOwner, "Only the player can do that.");
            return CommandResult.Accept();
        }

        internal static CommandResult Ages(WorldState s)
        {
            if (!s.Strategy.Started)
                return CommandResult.Reject(StrategyRefusals.Prologue, "Finish the quartermaster's commission first: the ages begin after it.");
            return CommandResult.Accept();
        }
    }

    internal sealed class FoundCityHandler : CommandHandler<FoundCity>
    {
        internal static CommandResult Check(WorldState s, int player, int vertex)
        {
            var topo = s.Board.Topology;
            if (vertex < 0 || vertex >= topo.VertexCount) return CommandResult.Reject(StrategyRefusals.BadVertex, "That is not a flag on the map.");
            if (!topo.IsCorner(vertex)) return CommandResult.Reject(StrategyRefusals.BadVertex, "A city must stand on a hex corner.");
            if (EconomyApi.IsVertexOccupied(s, vertex) || vertex == s.Board.WarehouseVertex)
                return CommandResult.Reject(StrategyRefusals.Occupied, "That flag is already taken.");
            foreach (var city in s.Economy.Cities)
                if (topo.HexEdgeBetween(city.Vertex, vertex) >= 0)
                    return CommandResult.Reject(StrategyRefusals.TooClose, city.Owner == PlayerIds.Human
                        ? "A city cannot stand next to another city."
                        : "A city cannot stand next to the rival outpost.");
            bool road = false;
            foreach (int e in topo.VertexEdges(vertex)) if (EconomyApi.HasRoad(s, e)) road = true;
            if (!road) return CommandResult.Reject(StrategyRefusals.NoRoad, "A new city needs one of your roads to reach its flag.");
            var cost = s.Strategy.Tuning.CityCost;
            if (!StrategyLogic.CanPay(s, cost, 0))
                return CommandResult.Reject(StrategyRefusals.CannotAfford,
                    "A city costs " + StrategyLogic.CostText(cost, 0) + "; the storehouse is short of " + StrategyLogic.MissingText(s, cost, 0) + ".");
            return CommandResult.Accept();
        }

        protected override CommandResult Validate(WorldState s, FoundCity c)
        {
            var r = SGuard.Basic(s, c);
            if (r.Ok) r = SGuard.Ages(s);
            return r.Ok ? Check(s, c.PlayerId, c.Vertex) : r;
        }

        protected override CommandResult Apply(WorldState s, FoundCity c, EventBuffer ev)
        {
            StrategyLogic.Pay(s, s.Strategy.Tuning.CityCost, s.Strategy.Ledger.Cities);
            int id = EconomyApi.AddCity(s, PlayerIds.Human, c.Vertex);
            for (int i = 0; i < s.Strategy.Tuning.CarriersPerCity; i++) EconomyApi.AddCarrier(s);
            ev.Add(new CityFounded { PlayerId = PlayerIds.Human, Vertex = c.Vertex, CityId = id });
            return CommandResult.Accept();
        }
    }

    internal sealed class TradeHandler : CommandHandler<Trade>
    {
        protected override CommandResult Validate(WorldState s, Trade c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            if (c.Give == c.Get) return CommandResult.Reject(StrategyRefusals.SameWare, "Choose two different goods to trade.");
            if (c.Count <= 0 || c.Count > 99) return CommandResult.Reject(StrategyRefusals.BadCount, "Trade at least one ware.");
            if ((int)c.Give < 0 || (int)c.Give >= Wares.Count || (int)c.Get < 0 || (int)c.Get >= Wares.Count)
                return CommandResult.Reject(StrategyRefusals.BadCount, "That is not a ware.");
            int need = StrategyLogic.TradeRate(s) * c.Count;
            int have = EconomyApi.StorehouseCount(s, c.Give);
            if (have < need)
                return CommandResult.Reject(StrategyRefusals.CannotAfford,
                    "The trade needs " + need + " " + Wares.DisplayName(c.Give) + " in the storehouse; it has " + have + ".");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, Trade c, EventBuffer ev)
        {
            int need = StrategyLogic.TradeRate(s) * c.Count;
            EconomyApi.TrySpendFromStorehouse(s, c.Give, need);
            EconomyApi.AddToStorehouse(s, c.Get, c.Count);
            s.Strategy.Ledger.TradeGiven[(int)c.Give] += need;
            s.Strategy.Ledger.TradeReceived[(int)c.Get] += c.Count;
            ev.Add(new Traded { Give = c.Give, GiveCount = need, Get = c.Get, GetCount = c.Count });
            return CommandResult.Accept();
        }
    }

    internal sealed class SellHandler : CommandHandler<Sell>
    {
        protected override CommandResult Validate(WorldState s, Sell c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            int per = StrategyLogic.SellWaresPerCoin(s);
            if ((int)c.Ware < 0 || (int)c.Ware >= Wares.Count) return CommandResult.Reject(StrategyRefusals.BadCount, "That is not a ware.");
            if (c.Count <= 0 || c.Count % per != 0)
                return CommandResult.Reject(StrategyRefusals.BadCount, "Sell goods in lots of " + per + " (" + per + " for 1 coin).");
            int have = EconomyApi.StorehouseCount(s, c.Ware);
            if (have < c.Count)
                return CommandResult.Reject(StrategyRefusals.CannotAfford,
                    "The storehouse has only " + have + " " + Wares.DisplayName(c.Ware) + ".");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, Sell c, EventBuffer ev)
        {
            int coin = c.Count / StrategyLogic.SellWaresPerCoin(s);
            EconomyApi.TrySpendFromStorehouse(s, c.Ware, c.Count);
            s.Player(PlayerIds.Human).Coin += coin;
            s.Strategy.Ledger.Sold[(int)c.Ware] += c.Count;
            s.Strategy.Ledger.CoinFromSales += coin;
            ev.Add(new Sold { Ware = c.Ware, Count = c.Count, Coin = coin });
            return CommandResult.Accept();
        }
    }

    internal sealed class BuyHandler : CommandHandler<Buy>
    {
        protected override CommandResult Validate(WorldState s, Buy c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            if ((int)c.Ware < 0 || (int)c.Ware >= Wares.Count) return CommandResult.Reject(StrategyRefusals.BadCount, "That is not a ware.");
            if (c.Count <= 0 || c.Count > 99) return CommandResult.Reject(StrategyRefusals.BadCount, "Buy at least one ware.");
            int cost = c.Count * s.Strategy.Tuning.BuyPrice;
            if (s.Player(PlayerIds.Human).Coin < cost)
                return CommandResult.Reject(StrategyRefusals.CannotAfford, "That costs " + cost + " coin; you have " + s.Player(PlayerIds.Human).Coin + ".");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, Buy c, EventBuffer ev)
        {
            int cost = c.Count * s.Strategy.Tuning.BuyPrice;
            s.Player(PlayerIds.Human).Coin -= cost;
            EconomyApi.AddToStorehouse(s, c.Ware, c.Count);
            s.Strategy.Ledger.TradeReceived[(int)c.Ware] += c.Count;
            s.Strategy.Ledger.CoinSpentBuying += cost;
            ev.Add(new Bought { Ware = c.Ware, Count = c.Count, Coin = cost });
            return CommandResult.Accept();
        }
    }

    internal sealed class MoveHeroHandler : CommandHandler<MoveHero>
    {
        protected override CommandResult Validate(WorldState s, MoveHero c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            var topo = s.Board.Topology;
            if (c.Vertex < 0 || c.Vertex >= topo.VertexCount) return CommandResult.Reject(StrategyRefusals.BadVertex, "That is not a flag on the map.");
            if (s.Strategy.HeroMovesLeft <= 0) return CommandResult.RejectDefault(Reasons.NoTurnAction);
            if (topo.HexEdgeBetween(s.Strategy.HeroVertex, c.Vertex) < 0)
                return CommandResult.Reject(StrategyRefusals.NotAdjacent, "The hero moves one hex edge at a time, to a neighbouring corner.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, MoveHero c, EventBuffer ev)
        {
            var st = s.Strategy;
            int from = st.HeroVertex;
            st.HeroVertex = c.Vertex;
            st.HeroMovesLeft--;
            ev.Add(new HeroMoved { From = from, To = c.Vertex, MovesLeft = st.HeroMovesLeft });
            StrategyLogic.HeroWalk(s, from, c.Vertex, ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class DraftPickHandler : CommandHandler<DraftPick>
    {
        protected override CommandResult Validate(WorldState s, DraftPick c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            if (s.Strategy.DraftStep == 0) return CommandResult.Reject(StrategyRefusals.NotDrafting, "There is no draft running.");
            if (!StrategyLogic.Side(s, PlayerIds.Human).DraftHand.Contains(c.CardId))
                return CommandResult.Reject(StrategyRefusals.NotInDraft, "That card is not in your draft hand.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, DraftPick c, EventBuffer ev)
        {
            StrategyLogic.ApplyPick(s, c.CardId, ev);
            return CommandResult.Accept();
        }
    }

    internal sealed class BuildCardHandler : CommandHandler<BuildCard>
    {
        protected override CommandResult Validate(WorldState s, BuildCard c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            if (!StrategyLogic.Side(s, PlayerIds.Human).Hand.Contains(c.CardId))
                return CommandResult.Reject(StrategyRefusals.NotInHand, "That card is not in your hand.");
            if (!StrategyLogic.IsPlayerCity(s, PlayerIds.Human, c.CityVertex))
                return CommandResult.Reject(StrategyRefusals.NoCity, "Cards are built at one of your cities.");
            if (StrategyLogic.BuiltAt(s, PlayerIds.Human, c.CityVertex) >= s.Strategy.Tuning.MaxCardsPerCity)
                return CommandResult.Reject(StrategyRefusals.CityFull, "That city already holds " + s.Strategy.Tuning.MaxCardsPerCity + " cards.");
            var d = StrategyLogic.Def(s, c.CardId);
            if (!StrategyLogic.CanPay(s, d.Cost, d.CoinCost))
                return CommandResult.Reject(StrategyRefusals.CannotAfford,
                    d.Name + " costs " + StrategyLogic.CostText(d.Cost, d.CoinCost) + "; you are short of " + StrategyLogic.MissingText(s, d.Cost, d.CoinCost) + ".");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, BuildCard c, EventBuffer ev)
        {
            var d = StrategyLogic.Def(s, c.CardId);
            var side = StrategyLogic.Side(s, PlayerIds.Human);
            StrategyLogic.Pay(s, d.Cost, s.Strategy.Ledger.Cards);
            s.Player(PlayerIds.Human).Coin -= d.CoinCost;
            s.Strategy.Ledger.CoinSpentOnCards += d.CoinCost;
            side.Hand.Remove(c.CardId);
            side.Built.Add(new BuiltCard { CardId = c.CardId, CityVertex = c.CityVertex, Tick = s.Tick });
            if (d.HeroMoves > 0) s.Strategy.HeroMovesLeft += d.HeroMoves;
            ev.Add(new CardBuilt { PlayerId = PlayerIds.Human, CardId = c.CardId, DefId = d.Id, CityVertex = c.CityVertex });
            return CommandResult.Accept();
        }
    }

    internal sealed class DiscardCardHandler : CommandHandler<DiscardCard>
    {
        protected override CommandResult Validate(WorldState s, DiscardCard c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            if (!StrategyLogic.Side(s, PlayerIds.Human).Hand.Contains(c.CardId))
                return CommandResult.Reject(StrategyRefusals.NotInHand, "That card is not in your hand.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, DiscardCard c, EventBuffer ev)
        {
            var side = StrategyLogic.Side(s, PlayerIds.Human);
            int coin = s.Strategy.Tuning.DiscardCoin;
            side.Hand.Remove(c.CardId);
            side.Discarded.Add(c.CardId);
            s.Player(PlayerIds.Human).Coin += coin;
            s.Strategy.Ledger.CoinFromDiscards += coin;
            ev.Add(new CardDiscarded { PlayerId = PlayerIds.Human, CardId = c.CardId, DefId = s.Strategy.Cards[c.CardId].DefId, Coin = coin });
            return CommandResult.Accept();
        }
    }

    internal sealed class BuildWonderStageHandler : CommandHandler<BuildWonderStage>
    {
        protected override CommandResult Validate(WorldState s, BuildWonderStage c)
        {
            var r = SGuard.Basic(s, c);
            if (!r.Ok) return r;
            r = SGuard.Ages(s);
            if (!r.Ok) return r;
            var w = s.Strategy.Wonder;
            if (w.StagesDone >= s.Strategy.Tuning.WonderStages.Length)
                return CommandResult.Reject(StrategyRefusals.WonderComplete, "The wonder is complete.");
            if (w.InProgressStage > 0)
                return CommandResult.Reject(StrategyRefusals.StageInProgress, "Wonder stage " + w.InProgressStage + " is still waiting for its goods.");
            if (EconomyApi.FindBuilding(s, BuildingType.WonderSite, true) < 0)
                return CommandResult.Reject(StrategyRefusals.NoWonder, "Build a wonder site first.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, BuildWonderStage c, EventBuffer ev)
        {
            var w = s.Strategy.Wonder;
            int id = w.BuildingId >= 0 && EconomyApi.GetBuilding(s, w.BuildingId).State == BuildingState.Active
                ? w.BuildingId
                : EconomyApi.FindBuilding(s, BuildingType.WonderSite, true);
            w.BuildingId = id;
            w.InProgressStage = w.StagesDone + 1;
            w.StageOrderedTick = s.Tick;
            var cost = s.Strategy.Tuning.WonderStages[w.InProgressStage - 1];
            for (int i = 0; i < Wares.Count; i++)
            {
                // Request only what the site does not already hold.
                int need = cost[i] - EconomyApi.Available(s, id, (Ware)i);
                if (need > 0) EconomyApi.RequestWares(s, id, (Ware)i, need);
            }
            ev.Add(new WonderStageOrdered { Stage = w.InProgressStage, BuildingId = id });
            return CommandResult.Accept();
        }
    }
}
