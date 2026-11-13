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
        public CardFamily Family;
        /// <summary>"Raw and crafted", "Commerce", "Civic", "Military", "Lore".</summary>
        public string FamilyName;
        /// <summary>Suggested card colour "#RRGGBB" by family.</summary>
        public string Colour;
        public string CostText;
        public string EffectText;
        /// <summary>Built cards: the city flag; else -1.</summary>
        public int CityVertex;
        /// <summary>Hand cards: affordable right now.</summary>
        public bool Affordable;
        /// <summary>What is missing to build it ("1 rations, 2 coin"), or null when affordable.</summary>
        public string MissingText;
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
        /// <summary>Points per stage (index = stage - 1).</summary>
        public int[] StageVp;
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
        public int VictoryPoints;
    }

    /// <summary>What the demons will do next: deck, discard, draw rate, sites about to outbreak.</summary>
    public struct InfectionForecast
    {
        /// <summary>Cards left in the draw pile and in the discard pile.</summary>
        public int DeckSize;
        public int DiscardSize;
        /// <summary>Discarded sites (already drawn since the last reshuffle), ascending.</summary>
        public int[] Discard;
        /// <summary>Cards drawn at the end of this round.</summary>
        public int NextDraws;
        /// <summary>Most demons a site holds; one more outbreaks.</summary>
        public int DemonCap;
        /// <summary>Sites holding DemonCap demons: drawing one of them, or an outbreak next to it, outbreaks.</summary>
        public int[] SitesAtCap;
        /// <summary>Sites at the cap whose card is in the discard pile (safe until the next reshuffle or epidemic).</summary>
        public int[] SitesAtCapInDiscard;
        public int Breaches;
        public int BreachLimit;
        public int Epidemics;
        /// <summary>True when the next round start begins a new age (and so brings an epidemic).</summary>
        public bool EpidemicNextRound;
        /// <summary>"2 cards drawn at round end; 9 in the deck, 6 in the discard; 1 site at 3 demons; breaches 1 of 6."</summary>
        public string Sentence;
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
                VictoryPoints = t.VictoryPoints,
            };
            r.Headline = Headline(s);
            return r;
        }

        /// <summary>"Prologue: finish the quartermaster's commission.", "Age II, round 1 of 2.", "Victory: 12 points to 5."</summary>
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

        /// <summary>Score breakdowns of both sides: [0] player, [1] rival.</summary>
        public static ScoreBreakdown[] Scores(WorldState s) => new[] { Score(s, PlayerIds.Human), Score(s, PlayerIds.Rival) };

        /// <summary>"Cities 4, wonder 2, cards 1, military 1, outpost 0: 8 of 12." (rival: "Plan 2, cards 1, military -1: 2 of 12.")</summary>
        public static string ScoreText(WorldState s, int player)
        {
            var sc = Score(s, player);
            int target = s.Strategy.Tuning.VictoryPoints;
            if (player == PlayerIds.Human)
                return "Cities " + sc.Cities + ", wonder " + sc.Wonder + ", cards " + sc.Cards + ", military " + sc.Military + ", outpost " + sc.Outpost + ": " + sc.Total + " of " + target + ".";
            return "Plan " + sc.Plan + ", cards " + sc.Cards + ", military " + sc.Military + ": " + sc.Total + " of " + target + ".";
        }

        /// <summary>Every age-end military comparison so far (age, shields, result, tokens).</summary>
        public static List<MilitaryComparison> Comparisons(WorldState s) => new List<MilitaryComparison>(s.Strategy.Comparisons);

        // ---- Cities ----

        public static CommandResult CheckCity(WorldState s, int vertex) => FoundCityHandler.Check(s, PlayerIds.Human, vertex);

        /// <summary>Every flag with its city verdict (index = flag id; intermediate flags are refused bad_vertex, cities stand on corners).</summary>
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

        /// <summary>Corners where a city can be founded now (ascending), ignoring cost when ignoreCost is true.</summary>
        public static List<int> LegalCitySites(WorldState s, bool ignoreCost = false)
        {
            var list = new List<int>();
            foreach (int v in s.Board.Topology.Corners)
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

        /// <summary>Most demons a site holds; one more makes it outbreak (3; 4 with Ward Stones).</summary>
        public static int DemonCap(WorldState s) => StrategyLogic.DemonCap(s);

        /// <summary>Infection cards drawn at the end of this round (2, 3, 4 by age; Beacon Tower -1).</summary>
        public static int InfectionCount(WorldState s) => StrategyLogic.InfectionCount(s);

        /// <summary>Sites that receive a demon when this flag outbreaks (see StrategyLogic.OutbreakNeighbours for the rule).</summary>
        public static List<int> OutbreakNeighbours(WorldState s, int vertex) => StrategyLogic.OutbreakNeighbours(s, vertex);

        /// <summary>True when a drawn card for this flag would place a demon (corners; intermediate flags holding something).</summary>
        public static bool TakesDemons(WorldState s, int vertex) => StrategyLogic.TakesDemons(s, vertex);

        /// <summary>True when the robber stops this hex (every one of your cities touching it holds a demon).</summary>
        public static bool IsHexRobbed(WorldState s, int hex) => StrategyLogic.HexRobbed(s, hex);

        public static InfectionForecast Forecast(WorldState s)
        {
            var st = s.Strategy;
            int cap = DemonCap(s);
            var atCap = new List<int>();
            var atCapDiscard = new List<int>();
            for (int v = 0; v < st.Demons.Length; v++)
            {
                if (st.Demons[v] < cap) continue;
                atCap.Add(v);
                if (st.InfectionDiscard.Contains(v)) atCapDiscard.Add(v);
            }
            var discard = new List<int>(st.InfectionDiscard);
            discard.Sort();
            var f = new InfectionForecast
            {
                DeckSize = st.InfectionDeck.Count,
                DiscardSize = st.InfectionDiscard.Count,
                Discard = discard.ToArray(),
                NextDraws = st.Started ? StrategyLogic.InfectionCount(s) : 0,
                DemonCap = cap,
                SitesAtCap = atCap.ToArray(),
                SitesAtCapInDiscard = atCapDiscard.ToArray(),
                Breaches = st.Breaches,
                BreachLimit = st.Tuning.BreachLimit,
                Epidemics = st.Epidemics,
                EpidemicNextRound = st.Started && st.Tuning.Epidemics && st.RoundInAge >= st.Tuning.RoundsPerAge && st.Age < st.Tuning.Ages,
            };
            f.Sentence = (st.Started ? f.NextDraws + " infection cards drawn at round end; " : "No infection before Age I; ")
                + f.DeckSize + " in the deck, " + f.DiscardSize + " in the discard; "
                + atCap.Count + (atCap.Count == 1 ? " site" : " sites") + " at " + cap + " demons; breaches " + st.Breaches + " of " + st.Tuning.BreachLimit + "."
                + (f.EpidemicNextRound ? " An epidemic strikes when the next age begins." : "");
            return f;
        }

        /// <summary>Corners the hero can move to now, one hex edge away (empty when out of moves).</summary>
        public static List<int> HeroMoveTargets(WorldState s)
        {
            var l = new List<int>();
            if (s.Strategy.HeroMovesLeft <= 0 || s.Strategy.GameOver) return l;
            foreach (int v in s.Board.Topology.CornerNeighbours(s.Strategy.HeroVertex)) l.Add(v);
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
                Family = d.Family,
                FamilyName = CardTable.FamilyName(d.Family),
                Colour = CardTable.FamilyColour(d.Family),
                CostText = StrategyLogic.CostText(d.Cost, d.CoinCost),
                EffectText = d.Text,
                CityVertex = city,
                Affordable = StrategyLogic.CanPay(s, d.Cost, d.CoinCost),
                MissingText = StrategyLogic.MissingText(s, d.Cost, d.CoinCost),
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

        /// <summary>Every card of the game (one per card id) in a family, by age then table order.</summary>
        public static List<CardView> CardsByFamily(WorldState s, CardFamily family)
        {
            var ids = new List<int>();
            foreach (var c in s.Strategy.Cards)
                if (CardTable.Get(c.DefId).Family == family) ids.Add(c.Id);
            ids.Sort((a, b) =>
            {
                var da = CardTable.Get(s.Strategy.Cards[a].DefId);
                var db = CardTable.Get(s.Strategy.Cards[b].DefId);
                if (da.Age != db.Age) return da.Age.CompareTo(db.Age);
                return System.Array.IndexOf(CardTable.All, da).CompareTo(System.Array.IndexOf(CardTable.All, db));
            });
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
                StageVp = (int[])t.WonderStageVp.Clone(),
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
