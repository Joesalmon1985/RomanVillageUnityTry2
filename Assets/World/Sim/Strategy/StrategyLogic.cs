using System;
using System.Collections.Generic;
using World.Sim.Economy;
using World.Sim.Military;

namespace World.Sim.Strategy
{
    /// <summary>Strategy rules shared by hooks, systems, commands and the API. Deterministic.</summary>
    internal static class StrategyLogic
    {
        // ---------------- Setup ----------------

        internal static void Init(WorldState s)
        {
            var topo = s.Board.Topology;
            var st = new StrategyState();
            s.Strategy = st;
            st.Demons = new int[topo.VertexCount];
            st.HeroVertex = s.Board.HomeCityVertex;
            st.HeroMovesLeft = st.Tuning.HeroMovesPerRound;
            for (int p = 0; p < PlayerIds.Count; p++) st.Sides.Add(new SideState { PlayerId = p });

            // The rival outpost is a city for the distance rule (and occupies its flag).
            if (s.Board.RivalOutpostVertex >= 0) EconomyApi.AddCity(s, PlayerIds.Rival, s.Board.RivalOutpostVertex);

            // Infection deck: flags near home, excluding the home city (until Age II) and the outpost.
            st.InfectionRng = s.Rng.Fork("strategy.infection");
            int home = s.Board.HomeCityVertex;
            for (int v = 0; v < topo.VertexCount; v++)
            {
                if (v == home || v == s.Board.RivalOutpostVertex) continue;
                int d = topo.Distance(home, v);
                if (d >= 0 && d <= st.Tuning.InfectionRadius) st.InfectionDeck.Add(v);
            }
            st.InfectionRng.Shuffle(st.InfectionDeck);

            // Card decks per age (each instance appears once).
            var draftRng = s.Rng.Fork("strategy.draft");
            for (int age = 1; age <= st.Tuning.Ages; age++)
            {
                var deck = new AgeDeck { Age = age };
                foreach (var def in CardTable.ForAge(age))
                {
                    var ci = new CardInstance { Id = st.Cards.Count, DefId = def.Id, Age = age };
                    st.Cards.Add(ci);
                    deck.CardIds.Add(ci.Id);
                }
                draftRng.Shuffle(deck.CardIds);
                st.Decks.Add(deck);
            }
        }

        // ---------------- Lookups ----------------

        internal static CardDef Def(WorldState s, int cardId) =>
            cardId >= 0 && cardId < s.Strategy.Cards.Count ? CardTable.Get(s.Strategy.Cards[cardId].DefId) : null;

        internal static SideState Side(WorldState s, int player) => s.Strategy.Sides[player];

        internal static CardEffects Effects(WorldState s, int player)
        {
            var fx = new CardEffects();
            if (s.Strategy == null) return fx;
            foreach (var b in Side(s, player).Built)
            {
                var d = Def(s, b.CardId);
                if (d != null) fx.Add(d);
            }
            return fx;
        }

        internal static int CityCount(WorldState s, int player)
        {
            int n = 0;
            foreach (var c in s.Economy.Cities) if (c.Owner == player) n++;
            return n;
        }

        internal static bool IsPlayerCity(WorldState s, int player, int vertex)
        {
            foreach (var c in s.Economy.Cities) if (c.Owner == player && c.Vertex == vertex) return true;
            return false;
        }

        internal static int BuiltAt(WorldState s, int player, int vertex)
        {
            int n = 0;
            foreach (var b in Side(s, player).Built) if (b.CityVertex == vertex) n++;
            return n;
        }

        internal static int TotalRounds(WorldState s) => s.Strategy.Tuning.Ages * s.Strategy.Tuning.RoundsPerAge;

        /// <summary>Round counted from the start of Age I (1-based), 0 in the prologue.</summary>
        internal static int AgeRound(WorldState s) => s.Strategy.Started ? s.Economy.Round - s.Strategy.FirstRound + 1 : 0;

        internal static int TradeRate(WorldState s)
        {
            var t = s.Strategy.Tuning;
            int r = t.TradeRate - Effects(s, PlayerIds.Human).TradeRateBonus;
            if (EconomyApi.FindBuilding(s, BuildingType.Market, true) >= 0) r--;
            return Math.Max(t.MinTradeRate, r);
        }

        internal static int SellWaresPerCoin(WorldState s) =>
            Effects(s, PlayerIds.Human).SellOneForOne ? 1 : Math.Max(1, s.Strategy.Tuning.SellWaresPerCoin);

        internal static int BreachThreshold(WorldState s) =>
            s.Strategy.Tuning.BreachThreshold + Effects(s, PlayerIds.Human).BreachThresholdBonus;

        internal static int InfectionCount(WorldState s)
        {
            var t = s.Strategy.Tuning;
            int age = Math.Max(1, s.Strategy.Age);
            return Math.Max(1, t.InfectionBase + (age - 1) * t.InfectionPerAge - Effects(s, PlayerIds.Human).InfectionMinus);
        }

        internal static int HeroMovesPerRound(WorldState s) =>
            s.Strategy.Tuning.HeroMovesPerRound + Effects(s, PlayerIds.Human).HeroMoves;

        // ---------------- Score ----------------

        internal static ScoreBreakdown Score(WorldState s, int player)
        {
            var t = s.Strategy.Tuning;
            var sc = new ScoreBreakdown { PlayerId = player };
            var fx = Effects(s, player);
            if (player == PlayerIds.Human)
            {
                int cities = CityCount(s, player);
                sc.Cities = cities * t.VpPerCity;
                sc.Cards = fx.Vp + fx.VpPerExtraCity * Math.Min(2, Math.Max(0, cities - 1));
                for (int i = 0; i < s.Strategy.Wonder.StagesDone && i < t.WonderStageVp.Length; i++) sc.Wonder += t.WonderStageVp[i];
                sc.Outpost = MilitaryApi.IsOutpostDestroyed(s) ? t.OutpostVp : 0;
            }
            else
            {
                sc.Plan = Side(s, player).PlanVp;
                sc.Cards = fx.Vp;
            }
            sc.Total = sc.Cities + sc.Cards + sc.Wonder + sc.Outpost + sc.Plan;
            return sc;
        }

        internal static void EndGame(WorldState s, GameOutcome outcome, string reason, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st.GameOver) return;
            st.GameOver = true;
            st.Outcome = outcome;
            st.OutcomeReason = reason;
            st.EndedTick = s.Tick;
            ev?.Add(new GameEnded
            {
                Outcome = outcome,
                Reason = reason,
                PlayerVp = Score(s, PlayerIds.Human).Total,
                RivalVp = Score(s, PlayerIds.Rival).Total,
            });
        }

        internal static void CheckPoints(WorldState s, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st.GameOver || !st.Started) return;
            int p = Score(s, PlayerIds.Human).Total, r = Score(s, PlayerIds.Rival).Total;
            int target = st.Tuning.VictoryPoints;
            if (p < target && r < target) return;
            if (p > r)
            {
                bool outpostNow = MilitaryApi.OutpostDestroyedTick(s) == s.Tick;
                EndGame(s, GameOutcome.Victory, outpostNow ? OutcomeReasons.Outpost : OutcomeReasons.Points, ev);
            }
            else if (r > p) EndGame(s, GameOutcome.Defeat, OutcomeReasons.Points, ev);
            else EndGame(s, GameOutcome.Draw, OutcomeReasons.Points, ev);
        }

        internal static void CompareAtAgeEnd(WorldState s, EventBuffer ev)
        {
            int p = Score(s, PlayerIds.Human).Total, r = Score(s, PlayerIds.Rival).Total;
            var o = p > r ? GameOutcome.Victory : p < r ? GameOutcome.Defeat : GameOutcome.Draw;
            EndGame(s, o, OutcomeReasons.AgeEnd, ev);
        }

        // ---------------- Rounds ----------------

        internal static void OnRoundStarted(WorldState s, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st.GameOver) return;
            var t = st.Tuning;
            if (!st.Started)
            {
                bool go = !t.StartAfterCommission || s.Economy.Commission.Complete || s.Economy.Round > t.MaxPrologueRounds;
                if (!go) return;
                st.Started = true;
                st.FirstRound = s.Economy.Round;
            }
            int rr = s.Economy.Round - st.FirstRound;
            int age = rr / Math.Max(1, t.RoundsPerAge) + 1;
            if (age > t.Ages) return;
            st.Age = age;
            st.RoundInAge = rr % Math.Max(1, t.RoundsPerAge) + 1;
            var fx = Effects(s, PlayerIds.Human);
            st.HeroMovesLeft = HeroMovesPerRound(s);
            if (fx.CoinPerRound > 0)
            {
                s.Player(PlayerIds.Human).Coin += fx.CoinPerRound;
                st.Ledger.CoinFromCards += fx.CoinPerRound;
            }
            if (st.RoundInAge == 1)
            {
                ev?.Add(new AgeStarted { Age = age, Round = s.Economy.Round });
                if (age == 2)
                {
                    int home = s.Board.HomeCityVertex;
                    if (!st.InfectionDeck.Contains(home) && !st.InfectionDiscard.Contains(home))
                        st.InfectionDeck.Insert(st.InfectionRng.NextInt(st.InfectionDeck.Count + 1), home);
                }
                Deal(s, age, ev);
            }
            ev?.Add(new StrategyRoundStarted { Age = st.Age, RoundInAge = st.RoundInAge, HeroMoves = st.HeroMovesLeft });
        }

        internal static void OnRoundEnding(WorldState s, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st.GameOver || !st.Started) return;
            int rr = AgeRound(s);

            // Rival turn: plan points, then garrison and raids.
            if (!MilitaryApi.IsOutpostDestroyed(s) && Array.IndexOf(st.Tuning.RivalPlanRounds, rr) >= 0)
            {
                var side = Side(s, PlayerIds.Rival);
                side.PlanVp++;
                ev?.Add(new RivalScored { Vp = 1, Total = Score(s, PlayerIds.Rival).Total });
            }
            MilitaryApi.RivalRoundEnd(s, ev);

            // Infection.
            DrawInfection(s, InfectionCount(s), ev);
            if (st.GameOver) return;

            CheckPoints(s, ev);
            if (!st.GameOver && rr >= TotalRounds(s)) CompareAtAgeEnd(s, ev);
        }

        // ---------------- Demons ----------------

        internal static void DrawInfection(WorldState s, int count, EventBuffer ev)
        {
            var st = s.Strategy;
            st.LastInfection.Clear();
            for (int i = 0; i < count && !st.GameOver; i++)
            {
                if (st.InfectionDeck.Count == 0)
                {
                    st.InfectionDeck.AddRange(st.InfectionDiscard);
                    st.InfectionDiscard.Clear();
                    st.InfectionRng.Shuffle(st.InfectionDeck);
                }
                if (st.InfectionDeck.Count == 0) break;
                int v = st.InfectionDeck[st.InfectionDeck.Count - 1];
                st.InfectionDeck.RemoveAt(st.InfectionDeck.Count - 1);
                st.InfectionDiscard.Add(v);
                st.LastInfection.Add(v);
                PlaceDemon(s, v, ev);
            }
            ev?.Add(new InfectionDrawn { Vertices = st.LastInfection.ToArray() });
        }

        /// <summary>One infection on a flag: repelled by the hero, a demon, or a breach when the flag is full.</summary>
        internal static void PlaceDemon(WorldState s, int v, EventBuffer ev)
        {
            var st = s.Strategy;
            if (v == st.HeroVertex)
            {
                ev?.Add(new DemonRepelled { Vertex = v });
                return;
            }
            if (st.Demons[v] >= BreachThreshold(s))
            {
                st.Breaches++;
                ev?.Add(new BreachAdvanced { Vertex = v, Breaches = st.Breaches, Limit = st.Tuning.BreachLimit });
                if (st.Breaches >= st.Tuning.BreachLimit) EndGame(s, GameOutcome.Defeat, OutcomeReasons.Breach, ev);
                return;
            }
            st.Demons[v]++;
            ev?.Add(new DemonSpawned { Vertex = v, Count = st.Demons[v] });
        }

        internal static int ClearDemons(WorldState s, int v, string by, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st == null || v < 0 || v >= st.Demons.Length || st.Demons[v] == 0) return 0;
            int n = st.Demons[v];
            st.Demons[v] = 0;
            ev?.Add(new DemonsCleared { Vertex = v, Count = n, By = by });
            return n;
        }

        // ---------------- Draft ----------------

        internal static void Deal(WorldState s, int age, EventBuffer ev)
        {
            var st = s.Strategy;
            if (age < 1 || age > st.Decks.Count) return;
            var deck = st.Decks[age - 1].CardIds;
            int n = st.Tuning.DraftHandSize;
            var human = Side(s, PlayerIds.Human);
            var rival = Side(s, PlayerIds.Rival);
            human.DraftHand.Clear();
            rival.DraftHand.Clear();
            for (int i = 0; i < n && i < deck.Count; i++) human.DraftHand.Add(deck[i]);
            for (int i = n; i < 2 * n && i < deck.Count; i++) rival.DraftHand.Add(deck[i]);
            st.DraftAge = age;
            st.DraftStep = human.DraftHand.Count > 0 ? 1 : 0;
            ev?.Add(new DraftDealt { Age = age, PlayerCards = human.DraftHand.ToArray() });
        }

        /// <summary>Rival's deterministic preference: points, then military, then economy; table order breaks ties.</summary>
        internal static int RivalCardScore(CardDef d)
        {
            int score = d.Vp * 100 + d.VpPerExtraCity * 20 + d.DamageBonus * 50 + d.CoinPerRound * 15 + d.TradeRateBonus * 10;
            score += (d.ForestBatch + d.HillsBatch + d.FieldsBatch + d.MountainBatch + d.AllBatch) * 3;
            score *= 100;
            score -= Array.IndexOf(CardTable.All, d);
            return score;
        }

        internal static int RivalChoice(WorldState s)
        {
            var hand = Side(s, PlayerIds.Rival).DraftHand;
            int best = -1, bestScore = int.MinValue;
            foreach (int id in hand)
            {
                int sc = RivalCardScore(Def(s, id));
                if (sc > bestScore || (sc == bestScore && id < best))
                {
                    best = id;
                    bestScore = sc;
                }
            }
            return best;
        }

        internal static void ApplyPick(WorldState s, int cardId, EventBuffer ev)
        {
            var st = s.Strategy;
            var human = Side(s, PlayerIds.Human);
            var rival = Side(s, PlayerIds.Rival);
            int step = st.DraftStep;
            human.DraftHand.Remove(cardId);
            human.Hand.Add(cardId);
            ev?.Add(new DraftPicked { PlayerId = PlayerIds.Human, CardId = cardId, DefId = st.Cards[cardId].DefId, Step = step });

            int rc = RivalChoice(s);
            if (rc >= 0)
            {
                rival.DraftHand.Remove(rc);
                ev?.Add(new DraftPicked { PlayerId = PlayerIds.Rival, CardId = rc, DefId = st.Cards[rc].DefId, Step = step });
                RivalTakeCard(s, rc, ev);
            }

            if (step < st.Tuning.DraftPicks && human.DraftHand.Count > 0)
            {
                var tmp = human.DraftHand;
                human.DraftHand = rival.DraftHand;
                rival.DraftHand = tmp;
                st.DraftStep = step + 1;
                ev?.Add(new DraftPassed { PlayerCards = human.DraftHand.ToArray() });
                return;
            }
            var discarded = new List<int>();
            discarded.AddRange(human.DraftHand);
            discarded.AddRange(rival.DraftHand);
            human.Discarded.AddRange(human.DraftHand);
            rival.Discarded.AddRange(rival.DraftHand);
            human.DraftHand.Clear();
            rival.DraftHand.Clear();
            st.DraftStep = 0;
            ev?.Add(new DraftFinished { Age = st.DraftAge, DiscardedCards = discarded.ToArray() });
        }

        /// <summary>The rival builds point and military cards at its outpost (up to RivalMaxBuilt), keeps the rest.</summary>
        internal static void RivalTakeCard(WorldState s, int cardId, EventBuffer ev)
        {
            var rival = Side(s, PlayerIds.Rival);
            var d = Def(s, cardId);
            bool useful = d.Vp > 0 || d.DamageBonus > 0;
            if (useful && rival.Built.Count < s.Strategy.Tuning.RivalMaxBuilt && !MilitaryApi.IsOutpostDestroyed(s))
            {
                rival.Built.Add(new BuiltCard { CardId = cardId, CityVertex = s.Board.RivalOutpostVertex, Tick = s.Tick });
                ev?.Add(new CardBuilt { PlayerId = PlayerIds.Rival, CardId = cardId, DefId = d.Id, CityVertex = s.Board.RivalOutpostVertex });
            }
            else rival.Hand.Add(cardId);
        }

        // ---------------- Payments ----------------

        internal static bool CanPay(WorldState s, int[] cost, int coin)
        {
            if (s.Player(PlayerIds.Human).Coin < coin) return false;
            for (int w = 0; w < Wares.Count; w++)
                if (cost[w] > 0 && EconomyApi.StorehouseCount(s, (Ware)w) < cost[w]) return false;
            return true;
        }

        /// <summary>Spend wares from the storehouse and add them to the given ledger row.</summary>
        internal static void Pay(WorldState s, int[] cost, int[] ledgerRow)
        {
            for (int w = 0; w < Wares.Count; w++)
            {
                if (cost[w] <= 0) continue;
                if (!EconomyApi.TrySpendFromStorehouse(s, (Ware)w, cost[w]))
                    throw new InvalidOperationException("payment checked but not possible");
                ledgerRow[w] += cost[w];
            }
        }

        internal static string CostText(int[] cost, int coin)
        {
            var parts = new List<string>();
            for (int w = 0; w < Wares.Count; w++) if (cost != null && cost[w] > 0) parts.Add(cost[w] + " " + Wares.DisplayName((Ware)w));
            if (coin > 0) parts.Add(coin + " coin");
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        /// <summary>"3 planks and 1 stone" missing from the storehouse for a cost, or null when affordable.</summary>
        internal static string MissingText(WorldState s, int[] cost, int coin)
        {
            var parts = new List<string>();
            for (int w = 0; w < Wares.Count; w++)
            {
                int have = EconomyApi.StorehouseCount(s, (Ware)w);
                if (cost != null && cost[w] > have) parts.Add((cost[w] - have) + " " + Wares.DisplayName((Ware)w));
            }
            int c = s.Player(PlayerIds.Human).Coin;
            if (coin > c) parts.Add((coin - c) + " coin");
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        // ---------------- Wonder ----------------

        internal static void TickWonder(WorldState s, EventBuffer ev)
        {
            var w = s.Strategy.Wonder;
            if (w.InProgressStage <= 0) return;
            var b = EconomyApi.GetBuilding(s, w.BuildingId);
            if (b == null || b.State != BuildingState.Active) return;
            var cost = s.Strategy.Tuning.WonderStages[w.InProgressStage - 1];
            for (int i = 0; i < Wares.Count; i++) if (cost[i] > 0 && b.Available((Ware)i) < cost[i]) return;
            for (int i = 0; i < Wares.Count; i++)
            {
                if (cost[i] <= 0) continue;
                EconomyApi.TryConsume(s, b.Id, (Ware)i, cost[i]);
                s.Strategy.Ledger.Wonder[i] += cost[i];
            }
            int stage = w.InProgressStage;
            w.StagesDone = stage;
            w.InProgressStage = 0;
            var vp = s.Strategy.Tuning.WonderStageVp;
            ev?.Add(new WonderStageCompleted { Stage = stage, Vp = stage - 1 < vp.Length ? vp[stage - 1] : 0 });
        }

        // ---------------- Hooks ----------------

        internal static int ModifyBatch(WorldState s, int hex, Ware ware, int amount)
        {
            var st = s.Strategy;
            if (st == null) return amount;
            // A demon on a city flag stops that city's production: a hex only counts cities without demons.
            bool anyClear = false, anyCity = false;
            foreach (int v in s.Board.Topology.HexVertices(hex))
            {
                if (!IsPlayerCity(s, PlayerIds.Human, v)) continue;
                anyCity = true;
                if (st.Demons[v] == 0) anyClear = true;
            }
            if (anyCity && !anyClear) return 0;
            int ageBonus = st.Started && st.Age > 0 ? st.Age * st.Tuning.AgeBatchBonus : 0;
            return amount + ageBonus + Effects(s, PlayerIds.Human).BatchBonus(s.Board.HexTerrain[hex]);
        }

        internal static CommandResult EndRoundGuard(WorldState s)
        {
            var st = s.Strategy;
            if (st == null) return CommandResult.Accept();
            if (st.GameOver) return CommandResult.Reject(StrategyRefusals.GameOver, "The game is over.");
            if (st.DraftStep > 0) return CommandResult.Reject(StrategyRefusals.DraftPending, "Pick a card from the draft first.");
            return CommandResult.Accept();
        }
    }

    /// <summary>Victory points of one side, by source.</summary>
    public struct ScoreBreakdown
    {
        public int PlayerId;
        public int Cities;
        public int Cards;
        public int Wonder;
        public int Outpost;
        /// <summary>Rival only: points from its seeded plan.</summary>
        public int Plan;
        public int Total;
    }
}
