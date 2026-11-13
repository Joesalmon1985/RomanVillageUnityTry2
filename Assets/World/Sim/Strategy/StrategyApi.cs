using System.Collections.Generic;
using World.Sim.Economy;

namespace World.Sim.Strategy
{
    /// <summary>A card as a panel shows it.</summary>
    public struct CardView
    {
        public int CardId;
        public string DefId;
        public string Name;
        public int Age;
        public string CostText;
        public string EffectText;
        /// <summary>Built cards: the city flag; else -1.</summary>
        public int CityVertex;
        /// <summary>Hand cards: affordable right now.</summary>
        public bool Affordable;
    }

    /// <summary>A flag and why a city can or cannot be founded there.</summary>
    public struct CitySite
    {
        public int Vertex;
        public bool Legal;
        /// <summary>Refusal code (too_close, no_road, occupied, cannot_afford) or null.</summary>
        public string Reason;
        public string Message;
    }

    /// <summary>Wonder progress for the wonder panel.</summary>
    public struct WonderInfo
    {
        public int StagesDone;
        public int StageCount;
        public int InProgressStage;
        public int BuildingId;
        /// <summary>One sentence, naming the missing wares while a stage is waiting.</summary>
        public string Status;
        /// <summary>"planks 4/6" per ware of the stage in progress (or the next stage).</summary>
        public string[] Lines;
        public bool CanOrder;
    }

    /// <summary>Snapshot of the round, age and ending for the HUD.</summary>
    public struct StrategyStatus
    {
        public bool Started;
        public int Age;
        public int Ages;
        public int RoundInAge;
        public int RoundsPerAge;
        public int RoundsLeft;
        public bool DraftPending;
        public bool GameOver;
        public GameOutcome Outcome;
        public string OutcomeReason;
        public string Headline;
        public int Breaches;
        public int BreachLimit;
        public int HeroVertex;
        public int HeroMovesLeft;
        public int NextInfection;
        public int Coin;
    }

    /// <summary>Public read surface of the strategy layer (plus the few writes other modules need).</summary>
    public static class StrategyApi
    {
        // ---- Status ----

        public static StrategyStatus Status(WorldState s)
        {
            var st = s.Strategy;
            var t = st.Tuning;
            int total = StrategyLogic.TotalRounds(s);
            var r = new StrategyStatus
            {
                Started = st.Started,
                Age = st.Age,
                Ages = t.Ages,
                RoundInAge = st.RoundInAge,
                RoundsPerAge = t.RoundsPerAge,
                RoundsLeft = st.Started ? System.Math.Max(0, total - StrategyLogic.AgeRound(s) + 1) : total,
                DraftPending = DraftPending(s),
                GameOver = st.GameOver,
                Outcome = st.Outcome,
                OutcomeReason = st.OutcomeReason,
                Breaches = st.Breaches,
                BreachLimit = t.BreachLimit,
                HeroVertex = st.HeroVertex,
                HeroMovesLeft = st.HeroMovesLeft,
                NextInfection = st.Started ? StrategyLogic.InfectionCount(s) : 0,
                Coin = s.Player(PlayerIds.Human).Coin,
            };
            r.Headline = Headline(s);
            return r;
        }

        /// <summary>"Prologue: finish the quartermaster's commission.", "Age II, round 1 of 2.", "Victory: 8 points to 5."</summary>
        public static string Headline(WorldState s)
        {
            var st = s.Strategy;
            if (st.GameOver)
            {
                int p = Score(s, PlayerIds.Human).Total, r = Score(s, PlayerIds.Rival).Total;
                string why;
                switch (st.OutcomeReason)
                {
                    case OutcomeReasons.Breach: why = "the demons broke through " + st.Breaches + " times"; break;
                    case OutcomeReasons.AgeEnd: why = "points compared after Age " + Roman(st.Tuning.Ages); break;
                    case OutcomeReasons.Outpost: why = "the rival outpost fell"; break;
                    default: why = "first to " + st.Tuning.VictoryPoints + " points"; break;
                }
                string o = st.Outcome == GameOutcome.Victory ? "Victory" : st.Outcome == GameOutcome.Defeat ? "Defeat" : "Draw";
                return o + ": " + p + " points to " + r + " (" + why + ").";
            }
            if (!st.Started) return "Prologue: finish the quartermaster's commission.";
            return "Age " + Roman(st.Age) + ", round " + st.RoundInAge + " of " + st.Tuning.RoundsPerAge + ".";
        }

        public static string Roman(int n) => n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n.ToString();

        public static bool DraftPending(WorldState s) => s.Strategy.DraftStep > 0;

        /// <summary>Round counted from the start of Age I (1-based); 0 in the prologue.</summary>
        public static int AgeRound(WorldState s) => StrategyLogic.AgeRound(s);

        // ---- Score ----

        public static ScoreBreakdown Score(WorldState s, int player) => StrategyLogic.Score(s, player);

        /// <summary>"Cities 4, cards 1, wonder 1, outpost 0: 6 of 8."</summary>
        public static string ScoreText(WorldState s, int player)
        {
            var sc = Score(s, player);
            int target = s.Strategy.Tuning.VictoryPoints;
            if (player == PlayerIds.Human)
                return "Cities " + sc.Cities + ", cards " + sc.Cards + ", wonder " + sc.Wonder + ", outpost " + sc.Outpost + ": " + sc.Total + " of " + target + ".";
            return "Plan " + sc.Plan + ", cards " + sc.Cards + ": " + sc.Total + " of " + target + ".";
        }

        // ---- Cities ----

        public static CommandResult CheckCity(WorldState s, int vertex) => FoundCityHandler.Check(s, PlayerIds.Human, vertex);

        /// <summary>Every flag with its city verdict (Legal ones first are not sorted: index = vertex id).</summary>
        public static List<CitySite> CitySites(WorldState s)
        {
            var list = new List<CitySite>();
            for (int v = 0; v < s.Board.Topology.VertexCount; v++)
            {
                var r = CheckCity(s, v);
                list.Add(new CitySite { Vertex = v, Legal = r.Ok, Reason = r.Reason, Message = r.Message });
            }
            return list;
        }

        /// <summary>Flags where a city can be founded now (ascending), ignoring cost when ignoreCost is true.</summary>
        public static List<int> LegalCitySites(WorldState s, bool ignoreCost = false)
        {
            var list = new List<int>();
            for (int v = 0; v < s.Board.Topology.VertexCount; v++)
            {
                var r = CheckCity(s, v);
                if (r.Ok || (ignoreCost && r.Reason == StrategyRefusals.CannotAfford)) list.Add(v);
            }
            return list;
        }

        public static string CityCostText(WorldState s) => StrategyLogic.CostText(s.Strategy.Tuning.CityCost, 0);

        /// <summary>The player's city flags (home first).</summary>
        public static List<int> PlayerCities(WorldState s)
        {
            var l = new List<int>();
            foreach (var c in s.Economy.Cities) if (c.Owner == PlayerIds.Human) l.Add(c.Vertex);
            return l;
        }

        // ---- Market ----

        /// <summary>Wares given for one ware received.</summary>
        public static int TradeRate(WorldState s) => StrategyLogic.TradeRate(s);

        public static int SellWaresPerCoin(WorldState s) => StrategyLogic.SellWaresPerCoin(s);

        public static int BuyPrice(WorldState s) => s.Strategy.Tuning.BuyPrice;

        /// <summary>"Trade 4 of one good for 1 of another; sell 2 for 1 coin; buy 1 for 3 coin."</summary>
        public static string MarketText(WorldState s) =>
            "Trade " + TradeRate(s) + " of one good for 1 of another; sell " + SellWaresPerCoin(s) + " for 1 coin; buy 1 for " + BuyPrice(s) + " coin.";

        // ---- Demons and hero ----

        /// <summary>(vertex, demons) for every flag holding demons, ascending.</summary>
        public static List<KeyValuePair<int, int>> Demons(WorldState s)
        {
            var l = new List<KeyValuePair<int, int>>();
            var d = s.Strategy.Demons;
            for (int v = 0; v < d.Length; v++) if (d[v] > 0) l.Add(new KeyValuePair<int, int>(v, d[v]));
            return l;
        }

        public static int DemonsAt(WorldState s, int vertex) =>
            vertex >= 0 && vertex < s.Strategy.Demons.Length ? s.Strategy.Demons[vertex] : 0;

        public static int BreachThreshold(WorldState s) => StrategyLogic.BreachThreshold(s);

        public static int InfectionCount(WorldState s) => StrategyLogic.InfectionCount(s);

        /// <summary>Flags the hero can move to now (empty when out of moves).</summary>
        public static List<int> HeroMoveTargets(WorldState s)
        {
            var l = new List<int>();
            if (s.Strategy.HeroMovesLeft <= 0 || s.Strategy.GameOver) return l;
            foreach (int v in s.Board.Topology.VertexNeighbours(s.Strategy.HeroVertex)) l.Add(v);
            return l;
        }

        /// <summary>Squads clear demons on arrival (called by the military module).</summary>
        public static int ClearDemonsBySquad(WorldState s, int vertex, EventBuffer ev) => StrategyLogic.ClearDemons(s, vertex, "squad", ev);

        // ---- Cards ----

        public static CardEffects Effects(WorldState s, int player) => StrategyLogic.Effects(s, player);

        public static CardDef CardDefOf(WorldState s, int cardId) => StrategyLogic.Def(s, cardId);

        public static CardView DescribeCard(WorldState s, int cardId)
        {
            var d = StrategyLogic.Def(s, cardId);
            if (d == null) return new CardView { CardId = cardId, Name = "Unknown card", CityVertex = -1 };
            int city = -1;
            foreach (var side in s.Strategy.Sides)
                foreach (var b in side.Built)
                    if (b.CardId == cardId) city = b.CityVertex;
            return new CardView
            {
                CardId = cardId,
                DefId = d.Id,
                Name = d.Name,
                Age = d.Age,
                CostText = StrategyLogic.CostText(d.Cost, d.CoinCost),
                EffectText = d.Text,
                CityVertex = city,
                Affordable = StrategyLogic.CanPay(s, d.Cost, d.CoinCost),
            };
        }

        public static List<CardView> Hand(WorldState s, int player = PlayerIds.Human) => Views(s, s.Strategy.Sides[player].Hand);

        public static List<CardView> DraftHand(WorldState s) => Views(s, s.Strategy.Sides[PlayerIds.Human].DraftHand);

        public static List<CardView> BuiltCards(WorldState s, int player)
        {
            var ids = new List<int>();
            foreach (var b in s.Strategy.Sides[player].Built) ids.Add(b.CardId);
            return Views(s, ids);
        }

        /// <summary>Cards built at one city flag.</summary>
        public static List<CardView> CardsAtCity(WorldState s, int player, int cityVertex)
        {
            var ids = new List<int>();
            foreach (var b in s.Strategy.Sides[player].Built) if (b.CityVertex == cityVertex) ids.Add(b.CardId);
            return Views(s, ids);
        }

        /// <summary>Your cities that can still hold a card.</summary>
        public static List<int> CitiesWithCardRoom(WorldState s)
        {
            var l = new List<int>();
            foreach (int v in PlayerCities(s))
                if (StrategyLogic.BuiltAt(s, PlayerIds.Human, v) < s.Strategy.Tuning.MaxCardsPerCity) l.Add(v);
            return l;
        }

        private static List<CardView> Views(WorldState s, List<int> ids)
        {
            var l = new List<CardView>();
            foreach (int id in ids) l.Add(DescribeCard(s, id));
            return l;
        }

        // ---- Wonder ----

        public static WonderInfo Wonder(WorldState s)
        {
            var w = s.Strategy.Wonder;
            var t = s.Strategy.Tuning;
            int site = w.BuildingId >= 0 ? w.BuildingId : EconomyApi.FindBuilding(s, BuildingType.WonderSite, true);
            var info = new WonderInfo
            {
                StagesDone = w.StagesDone,
                StageCount = t.WonderStages.Length,
                InProgressStage = w.InProgressStage,
                BuildingId = site,
            };
            int shown = w.InProgressStage > 0 ? w.InProgressStage : w.StagesDone + 1;
            var lines = new List<string>();
            var missing = new List<string>();
            if (shown <= t.WonderStages.Length)
            {
                var cost = t.WonderStages[shown - 1];
                for (int i = 0; i < Wares.Count; i++)
                {
                    if (cost[i] <= 0) continue;
                    int have = site >= 0 && w.InProgressStage > 0 ? System.Math.Min(cost[i], EconomyApi.Available(s, site, (Ware)i)) : 0;
                    lines.Add(Wares.DisplayName((Ware)i) + " " + have + "/" + cost[i]);
                    if (have < cost[i]) missing.Add((cost[i] - have) + " " + Wares.DisplayName((Ware)i));
                }
            }
            info.Lines = lines.ToArray();
            if (w.StagesDone >= t.WonderStages.Length) info.Status = "The wonder is complete.";
            else if (w.InProgressStage > 0) info.Status = "Wonder stage " + w.InProgressStage + " is waiting for " + string.Join(", ", missing) + ".";
            else if (EconomyApi.FindBuilding(s, BuildingType.WonderSite, true) < 0) info.Status = "Build a wonder site to start the wonder.";
            else
            {
                info.CanOrder = !s.Strategy.GameOver;
                info.Status = "Stage " + (w.StagesDone + 1) + " needs " + StrategyLogic.CostText(t.WonderStages[w.StagesDone], 0) + " delivered to the wonder site.";
            }
            return info;
        }

        // ---- Ledger ----

        /// <summary>Wares that left the storehouse through strategy actions (cities, cards, trade given, sales) plus wonder stages, per ware.</summary>
        public static int[] WaresSpent(WorldState s)
        {
            var l = s.Strategy.Ledger;
            var t = new int[Wares.Count];
            for (int w = 0; w < Wares.Count; w++) t[w] = l.Cities[w] + l.Cards[w] + l.Wonder[w] + l.TradeGiven[w] + l.Sold[w];
            return t;
        }
    }
}
