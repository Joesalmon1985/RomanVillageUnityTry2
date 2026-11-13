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

            // Infection deck: the flags within InfectionRadius segments of home (the home territory), excluding
            // the home city (until Age II) and the outpost. Corners always take a demon; an intermediate flag only
            // while it holds a building, a road end or a city (see DrawInfection), so empty ground is skipped.
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

        internal static int SellWaresPerCoin(WorldState s) => Math.Max(1, s.Strategy.Tuning.SellWaresPerCoin);

        /// <summary>Most demons a site holds; one more makes it outbreak (3, Ward Stones 4).</summary>
        internal static int DemonCap(WorldState s) =>
            s.Strategy.Tuning.DemonCap + Effects(s, PlayerIds.Human).DemonCapBonus;

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
            int cities = CityCount(s, player);
            sc.Cards = fx.Vp + fx.VpPerCity * cities;
            foreach (var mc in s.Strategy.Comparisons) sc.Military += player == PlayerIds.Human ? mc.PlayerVp : mc.RivalVp;
            if (player == PlayerIds.Human)
            {
                sc.Cities = cities * t.VpPerCity;
                for (int i = 0; i < s.Strategy.Wonder.StagesDone && i < t.WonderStageVp.Length; i++) sc.Wonder += t.WonderStageVp[i];
                sc.Outpost = MilitaryApi.IsOutpostDestroyed(s) ? t.OutpostVp : 0;
            }
            else
            {
                sc.Plan = Side(s, player).PlanVp;
            }
            sc.Total = sc.Cities + sc.Cards + sc.Wonder + sc.Military + sc.Outpost + sc.Plan;
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
                if (age >= 2 && t.Epidemics) Epidemic(s, ev);
                if (st.GameOver) return;
                Deal(s, age, ev);
            }
            ev?.Add(new StrategyRoundStarted { Age = st.Age, RoundInAge = st.RoundInAge, HeroMoves = st.HeroMovesLeft });
        }

        internal static void OnRoundEnding(WorldState s, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st.GameOver || !st.Started) return;
            int rr = AgeRound(s);

            // End of an age: military comparison on the field as it stands when the bell rings.
            if (st.Age >= 1 && st.RoundInAge >= st.Tuning.RoundsPerAge) CompareMilitary(s, ev);

            // Rival turn: plan points, then garrison and raids.
            if (!MilitaryApi.IsOutpostDestroyed(s) && Array.IndexOf(st.Tuning.RivalPlanRounds, rr) >= 0)
            {
                var side = Side(s, PlayerIds.Rival);
                side.PlanVp += st.Tuning.RivalPlanVp;
                ev?.Add(new RivalScored { Vp = st.Tuning.RivalPlanVp, Total = Score(s, PlayerIds.Rival).Total });
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
            st.LastOutbreaks.Clear();
            // Cards for empty intermediate flags are discarded without effect and do not count as a draw
            // (bounded so a deck of only empty flags cannot loop).
            int skips = st.InfectionDeck.Count + st.InfectionDiscard.Count;
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
                if (!TakesDemons(s, v))
                {
                    if (skips-- <= 0) break;
                    i--;
                    continue;
                }
                st.LastInfection.Add(v);
                // Each drawn card is one "draw": a site outbreaks at most once per draw.
                AddDemon(s, v, new bool[st.Demons.Length], false, ev);
            }
            ev?.Add(new InfectionDrawn { Vertices = st.LastInfection.ToArray() });
        }

        /// <summary>
        /// Epidemic (age change): the bottom card of the deck whose site can hold demons is drawn and its site filled
        /// to EpidemicFill demons (an outbreak if it already held demons, as in Pandemic); then the discard pile,
        /// with that card, is shuffled and put back on top of the deck. The higher draw rate comes with the new age.
        /// </summary>
        internal static void Epidemic(WorldState s, EventBuffer ev)
        {
            var st = s.Strategy;
            st.LastOutbreaks.Clear();
            int idx = -1;
            for (int i = 0; i < st.InfectionDeck.Count && idx < 0; i++) if (TakesDemons(s, st.InfectionDeck[i])) idx = i;
            if (idx < 0) return;
            int v = st.InfectionDeck[idx];
            st.InfectionDeck.RemoveAt(idx);
            st.InfectionDiscard.Add(v);
            st.Epidemics++;
            bool outbreak = false;
            if (v == st.HeroVertex) ev?.Add(new DemonRepelled { Vertex = v });
            else
            {
                int had = st.Demons[v];
                int fill = Math.Min(st.Tuning.EpidemicFill, DemonCap(s));
                if (st.Demons[v] < fill)
                {
                    st.Demons[v] = fill;
                    ev?.Add(new DemonSpawned { Vertex = v, Count = fill });
                }
                if (had > 0)
                {
                    outbreak = true;
                    Outbreak(s, v, new bool[st.Demons.Length], false, ev);
                }
            }
            ev?.Add(new EpidemicStruck { Vertex = v, Age = st.Age, Demons = st.Demons[v], Outbreak = outbreak });
            st.InfectionRng.Shuffle(st.InfectionDiscard);
            st.InfectionDeck.AddRange(st.InfectionDiscard); // top = end of the list
            st.InfectionDiscard.Clear();
        }

        /// <summary>
        /// One demon arrives at a site: repelled on the hero's flag; a site already at the cap outbreaks instead
        /// (at most once per draw: outbroken marks the sites that already did).
        /// </summary>
        internal static void AddDemon(WorldState s, int v, bool[] outbroken, bool chained, EventBuffer ev)
        {
            var st = s.Strategy;
            if (st.GameOver) return;
            if (v == st.HeroVertex)
            {
                ev?.Add(new DemonRepelled { Vertex = v });
                return;
            }
            if (st.Demons[v] >= DemonCap(s))
            {
                Outbreak(s, v, outbroken, chained, ev);
                return;
            }
            st.Demons[v]++;
            ev?.Add(new DemonSpawned { Vertex = v, Count = st.Demons[v] });
        }

        /// <summary>
        /// Outbreak at v: the breach track advances by one, then each connected neighbouring site gains one demon
        /// (a neighbour already at the cap outbreaks in turn, unless it already did in this draw).
        /// </summary>
        internal static void Outbreak(WorldState s, int v, bool[] outbroken, bool chained, EventBuffer ev)
        {
            var st = s.Strategy;
            if (outbroken[v] || st.GameOver) return;
            outbroken[v] = true;
            st.Breaches++;
            st.LastOutbreaks.Add(v);
            var nb = OutbreakNeighbours(s, v);
            ev?.Add(new OutbreakOccurred { Vertex = v, Neighbours = nb.ToArray(), Breaches = st.Breaches, Limit = st.Tuning.BreachLimit, Chained = chained });
            ev?.Add(new BreachAdvanced { Vertex = v, Breaches = st.Breaches, Limit = st.Tuning.BreachLimit });
            if (st.Breaches >= st.Tuning.BreachLimit)
            {
                EndGame(s, GameOutcome.Defeat, OutcomeReasons.Breach, ev);
                return;
            }
            foreach (int n in nb)
            {
                if (st.GameOver) return;
                if (outbroken[n]) continue;
                AddDemon(s, n, outbroken, true, ev);
            }
        }

        /// <summary>
        /// The connected neighbouring sites of a flag (fine graph): walk out along each of its road segments (built
        /// or not) in a straight line, flag by flag, and stop at the first flag that can hold demons
        /// (<see cref="TakesDemons"/>); that flag is a neighbour. Corners always hold demons, so a walk never passes
        /// a corner: a corner has up to three neighbours (one per hex edge: the nearest occupied flag on that edge,
        /// else the next corner) and an intermediate flag two. The rival outpost is never a neighbour. Ascending ids.
        /// </summary>
        internal static List<int> OutbreakNeighbours(WorldState s, int v)
        {
            var topo = s.Board.Topology;
            var list = new List<int>();
            foreach (int first in topo.VertexNeighbours(v))
            {
                int prev = v, cur = first;
                while (cur >= 0 && !TakesDemons(s, cur))
                {
                    // An empty intermediate flag has exactly two neighbours: keep walking straight on.
                    int next = -1;
                    foreach (int w in topo.VertexNeighbours(cur)) if (w != prev) next = w;
                    prev = cur;
                    cur = next;
                }
                if (cur < 0 || cur == v || cur == s.Board.RivalOutpostVertex || list.Contains(cur)) continue;
                list.Add(cur);
            }
            list.Sort();
            return list;
        }

        /// <summary>
        /// A drawn flag takes a demon when it is a hex corner, or an intermediate flag holding a building or
        /// site, a city, a road end, or demons already.
        /// </summary>
        internal static bool TakesDemons(WorldState s, int v)
        {
            var topo = s.Board.Topology;
            if (topo.IsCorner(v) || s.Strategy.Demons[v] > 0) return true;
            if (EconomyApi.IsVertexOccupied(s, v)) return true;
            foreach (int e in topo.VertexEdges(v)) if (EconomyApi.HasRoad(s, e)) return true;
            return false;
        }

        /// <summary>
        /// The hero walks one hex edge: demons on the two intermediate flags it passes are cleared, then those
        /// on the corner it arrives at.
        /// </summary>
        internal static void HeroWalk(WorldState s, int from, int to, EventBuffer ev)
        {
            var topo = s.Board.Topology;
            int he = topo.HexEdgeBetween(from, to);
            if (he >= 0)
            {
                var flags = topo.HexEdgeFlags(he);
                bool forward = flags[0] == from;
                ClearDemons(s, forward ? flags[1] : flags[2], "hero", ev);
                ClearDemons(s, forward ? flags[2] : flags[1], "hero", ev);
            }
            ClearDemons(s, to, "hero", ev);
        }

        /// <summary>One infection on a flag (a draw of its own): repelled by the hero, a demon, or an outbreak when the site is full.</summary>
        internal static void PlaceDemon(WorldState s, int v, EventBuffer ev) =>
            AddDemon(s, v, new bool[s.Strategy.Demons.Length], false, ev);

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

        /// <summary>Rival's deterministic draft preference: shields, then points (denying you), then economy; table order breaks ties.</summary>
        internal static int RivalCardScore(CardDef d)
        {
            int score = d.Shields * 200 + d.Vp * 60 + d.VpPerCity * 40 + d.DamageBonus * 20 + d.CoinPerRound * 15 + d.TradeRateBonus * 10;
            score += (d.ForestBatch + d.HillsBatch + d.FieldsBatch + d.PastureBatch + d.MountainBatch + d.AllBatch + d.ExtraOutput) * 3;
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

        /// <summary>
        /// The rival builds a military (shields) card at its outpost, free, at most one per age and RivalMaxBuilt in
        /// all (its first such pick of the age); it keeps the rest (they only deny you).
        /// </summary>
        internal static void RivalTakeCard(WorldState s, int cardId, EventBuffer ev)
        {
            var rival = Side(s, PlayerIds.Rival);
            var d = Def(s, cardId);
            bool useful = d.Shields > 0; // the rival's points come from its plan; it builds shields
            bool builtThisAge = false;
            foreach (var b in rival.Built) if (s.Strategy.Cards[b.CardId].Age == d.Age) builtThisAge = true;
            if (useful && !builtThisAge && rival.Built.Count < s.Strategy.Tuning.RivalMaxBuilt && !MilitaryApi.IsOutpostDestroyed(s))
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

        /// <summary>"3 planks, 1 bricks" missing from the storehouse for a cost, or null when affordable.</summary>
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
            if (b == null || b.State != BuildingState.Active || b.Blocked) return; // a demon on the site stops the work
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
            int ageBonus = st.Started && st.Age > 0 ? st.Age * st.Tuning.AgeBatchBonus : 0;
            return amount + ageBonus + Effects(s, PlayerIds.Human).BatchBonus(s.Board.HexTerrain[hex]);
        }

        /// <summary>
        /// The robber: a hex touching your cities produces nothing while every one of your cities touching it holds
        /// a demon (no new batch, and its harvesters stop taking units from it).
        /// </summary>
        internal static bool HexRobbed(WorldState s, int hex)
        {
            var st = s.Strategy;
            if (st == null) return false;
            bool anyClear = false, anyCity = false;
            foreach (int v in s.Board.Topology.HexCorners(hex))
            {
                if (!IsPlayerCity(s, PlayerIds.Human, v)) continue;
                anyCity = true;
                if (st.Demons[v] == 0) anyClear = true;
            }
            return anyCity && !anyClear;
        }

        // ---------------- Military comparison ----------------

        /// <summary>
        /// Compare shields with the neighbouring power (the rival): win +1/+3/+5 by age, lose -1, tie nothing.
        /// Tokens are mirrored for the rival. Recorded in Comparisons and as a MilitaryCompared event.
        /// </summary>
        internal static void CompareMilitary(WorldState s, EventBuffer ev)
        {
            var st = s.Strategy;
            var t = st.Tuning;
            int age = st.Age;
            foreach (var c in st.Comparisons) if (c.Age == age) return;
            int ps = MilitaryApi.Shields(s, PlayerIds.Human), rs = MilitaryApi.Shields(s, PlayerIds.Rival);
            int win = age - 1 < t.MilitaryWinVp.Length ? t.MilitaryWinVp[age - 1] : t.MilitaryWinVp[t.MilitaryWinVp.Length - 1];
            var mc = new MilitaryComparison { Age = age, Tick = s.Tick, PlayerShields = ps, RivalShields = rs };
            if (ps > rs) { mc.Result = ComparisonResult.Win; mc.PlayerVp = win; mc.RivalVp = t.MilitaryLossVp; }
            else if (ps < rs) { mc.Result = ComparisonResult.Loss; mc.PlayerVp = t.MilitaryLossVp; mc.RivalVp = win; }
            st.Comparisons.Add(mc);
            ev?.Add(new MilitaryCompared { Age = age, PlayerShields = ps, RivalShields = rs, Result = mc.Result, PlayerVp = mc.PlayerVp, RivalVp = mc.RivalVp });
        }

        /// <summary>Copy the player's card effects that the economy applies (workshop output, road segments) into EconomyState.Bonus.</summary>
        internal static void SyncEconomyBonuses(WorldState s)
        {
            var fx = Effects(s, PlayerIds.Human);
            var b = s.Economy.Bonus ?? (s.Economy.Bonus = new EconomyBonuses());
            b.RoadSegments = fx.RoadSegments;
            b.ExtraOutput = fx.ExtraOutput != null ? (int[])fx.ExtraOutput.Clone() : new int[BuildingDefs.Count];
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
        /// <summary>Military comparison tokens (can be negative).</summary>
        public int Military;
        public int Outpost;
        /// <summary>Rival only: points from its seeded plan.</summary>
        public int Plan;
        public int Total;
    }
}
