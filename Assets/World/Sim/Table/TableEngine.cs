using System;
using System.Collections.Generic;

namespace World.Sim.Table
{
    /// <summary>
    /// Hooks other modules and the AI packages plug into. Not saved: whoever builds the sim wires them again after a load.
    /// Every hook must be deterministic.
    /// </summary>
    public sealed class TableSeams
    {
        /// <summary>True when a hex produces nothing this roll (TU-2, PL-9). Default: never. The plague wires this.</summary>
        public Func<WorldState, int, bool> HexBlocked = (s, hex) => false;

        /// <summary>Cards a people gives up after a 7: (state, people, count) to counts per raw good summing to count (TU-10).</summary>
        public Func<WorldState, int, int, int[]> ChooseDiscard = TableDefaults.ChooseDiscard;

        /// <summary>An AI seat's setup placement: (state, people) to (corner, hex edge touching it).</summary>
        public Func<WorldState, int, (int corner, int edge)> AiSetup = TableDefaults.SetupPlacement;

        /// <summary>An AI seat plays its turn through the actor (build, trade, offer). Called once per turn. Default: a basic builder.</summary>
        public Action<WorldState, int, TableActor> AiTurn = TableBasicAi.PlayTurn;

        /// <summary>Whether an AI seat accepts an offer addressed to it (TR-6). Default: always declines (G1-2 owns AI trading).</summary>
        public Func<WorldState, int, TradeOffer, bool> AiAnswer = (s, p, o) => false;
    }

    /// <summary>Lets an AI seat act through the same checks as the commands. Each accepted action lengthens its turn (HG-5).</summary>
    public sealed class TableActor
    {
        private readonly TableEngine _e;
        private readonly WorldState _s;
        private readonly EventBuffer _ev;
        public readonly int People;

        internal TableActor(TableEngine e, WorldState s, EventBuffer ev, int people)
        {
            _e = e;
            _s = s;
            _ev = ev;
            People = people;
        }

        public CommandResult PlaceSettlement(int corner) => Run(new PlaceTableSettlement { Corner = corner });
        public CommandResult PlaceRoad(int edge) => Run(new PlaceTableRoad { HexEdge = edge });
        public CommandResult UpgradeCity(int corner) => Run(new UpgradeTableCity { Corner = corner });
        public CommandResult BankTrade(Ware give, Ware get) => Run(new BankTrade { Give = give, Get = get });
        public CommandResult OfferTrade(int to, int[] give, int[] get) => Run(new OfferTrade { To = to, Give = give, Get = get });

        private CommandResult Run(Command c)
        {
            c.PlayerId = People;
            var r = _e.Check(_s, c);
            return r.Ok ? _e.Apply(_s, c, _ev) : r;
        }
    }

    /// <summary>The turn engine: checks and applies the table commands and runs the table each tick.</summary>
    public sealed class TableEngine : ISimSystem
    {
        public readonly TableSeams Seams;

        public TableEngine(TableSeams seams) { Seams = seams ?? new TableSeams(); }

        private static int Raw(Ware w) => (int)w;

        // =====================================================================================================
        // Checks (never mutate)
        // =====================================================================================================

        private static CommandResult No(string code) => TableRefusals.Reject(code);

        /// <summary>table_closed when the table is not running, not_your_turn for an unknown people.</summary>
        private static string Gate(WorldState s, int p)
        {
            var t = s.Table;
            if (t == null || t.Phase == TablePhase.NotOpened || t.Phase == TablePhase.Ended || t.Paused) return TableRefusals.TableClosed;
            if (p < 0 || p >= t.PeopleCount) return TableRefusals.NotYourTurn;
            return null;
        }

        private static string TurnGate(WorldState s, int p, bool setupAllowed)
        {
            string g = Gate(s, p);
            if (g != null) return g;
            var t = s.Table;
            if (t.Current != p) return TableRefusals.NotYourTurn;
            if (t.Phase == TablePhase.Main) return null;
            if (t.Phase == TablePhase.Setup && setupAllowed) return null;
            return TableRefusals.NotYourTurn;
        }

        public CommandResult Check(WorldState s, Command c)
        {
            string code = Refusal(s, c);
            return code == null ? CommandResult.Accept() : No(code);
        }

        private static string Refusal(WorldState s, Command c)
        {
            var t = s.Table;
            int p = c.PlayerId;
            switch (c)
            {
                case Sacrifice _:
                    if (t == null || t.Phase == TablePhase.Ended) return TableRefusals.TableClosed;
                    return p == t.HumanSeat ? null : TableRefusals.NotYourTurn;

                case LeaveTable _:
                    if (t == null || t.Phase == TablePhase.NotOpened || t.Phase == TablePhase.Ended || t.Paused) return TableRefusals.TableClosed;
                    return p == t.HumanSeat ? null : TableRefusals.NotYourTurn;

                case PlaceTableSettlement ps:
                {
                    string g = TurnGate(s, p, true);
                    if (g != null) return g;
                    if (t.Phase == TablePhase.Setup)
                    {
                        if (t.SetupCorner >= 0) return TableRefusals.InvalidSite; // the road comes next
                        return TableRules.SettlementRefusal(s, p, ps.Corner, false);
                    }
                    return TableRules.SettlementRefusal(s, p, ps.Corner, true)
                           ?? (TableRules.SettlementsLeft(t, p) <= 0 ? TableRefusals.NoPieces : null)
                           ?? (TableRules.CanPay(t.Peoples[p].Hand, TableRules.SettlementCost) ? null : TableRefusals.CannotAfford);
                }

                case PlaceTableRoad pr:
                {
                    string g = TurnGate(s, p, true);
                    if (g != null) return g;
                    if (t.Phase == TablePhase.Setup)
                    {
                        if (t.SetupCorner < 0) return TableRefusals.InvalidSite; // the settlement comes first
                        return TableRules.RoadRefusal(s, p, pr.HexEdge, t.SetupCorner);
                    }
                    return TableRules.RoadRefusal(s, p, pr.HexEdge)
                           ?? (TableRules.RoadsLeft(t, p) <= 0 ? TableRefusals.NoPieces : null)
                           ?? (TableRules.CanPay(t.Peoples[p].Hand, TableRules.RoadCost) ? null : TableRefusals.CannotAfford);
                }

                case UpgradeTableCity uc:
                {
                    string g = TurnGate(s, p, false);
                    if (g != null) return g;
                    if (uc.Corner < 0 || uc.Corner >= t.CornerOwner.Length || t.CornerOwner[uc.Corner] != p || t.CornerLevel[uc.Corner] != 1)
                        return TableRefusals.InvalidSite;
                    if (TableRules.CitiesLeft(t, p) <= 0) return TableRefusals.NoPieces;
                    return TableRules.CanPay(t.Peoples[p].Hand, TableRules.CityCost) ? null : TableRefusals.CannotAfford;
                }

                case BankTrade bt:
                {
                    string g = TurnGate(s, p, false);
                    if (g != null) return g;
                    if (!Wares.IsRaw(bt.Give) || !Wares.IsRaw(bt.Get) || bt.Give == bt.Get) return TableRefusals.BadTrade;
                    return t.Peoples[p].Hand[Raw(bt.Give)] >= TableRules.TradeRate(s, p, bt.Give) ? null : TableRefusals.CannotAfford;
                }

                case OfferTrade ot:
                {
                    string g = TurnGate(s, p, false);
                    if (g != null) return g;
                    if (ot.To == p || ot.To < -1 || ot.To >= t.PeopleCount) return TableRefusals.BadTrade;
                    if (!ValidCards(ot.Give, true) || !ValidCards(ot.Get, true)) return TableRefusals.BadTrade;
                    return TableRules.CanPay(t.Peoples[p].Hand, ot.Give) ? null : TableRefusals.CannotAfford;
                }

                case AnswerOffer ao:
                {
                    string g = Gate(s, p);
                    if (g != null) return g;
                    var o = FindOffer(t, ao.OfferId);
                    if (o == null || o.Status != OfferStatus.Open || t.Phase != TablePhase.Main) return TableRefusals.OfferClosed;
                    if (o.From == p || (o.To >= 0 && o.To != p) || o.DeclinedBy.Contains(p)) return TableRefusals.OfferClosed;
                    if (!ao.Accept) return null;
                    if (!TableRules.CanPay(t.Peoples[p].Hand, o.Get)) return TableRefusals.CannotAfford;
                    return TableRules.CanPay(t.Peoples[o.From].Hand, o.Give) ? null : TableRefusals.OfferClosed;
                }

                case EndTurn _:
                    return TurnGate(s, p, false);

                case DiscardCards dc:
                {
                    string g = Gate(s, p);
                    if (g != null) return g;
                    if (t.Phase != TablePhase.Discard || t.DiscardOwed[p] <= 0) return TableRefusals.NotYourTurn;
                    if (!ValidCards(dc.Cards, false)) return TableRefusals.BadTrade;
                    if (TableRules.HandSize(dc.Cards) != t.DiscardOwed[p]) return TableRefusals.BadTrade;
                    return TableRules.CanPay(t.Peoples[p].Hand, dc.Cards) ? null : TableRefusals.CannotAfford;
                }
            }
            return Reasons.Disabled;
        }

        private static bool ValidCards(int[] cards, bool nonEmpty)
        {
            if (cards == null || cards.Length != Wares.RawCount) return false;
            int sum = 0;
            foreach (int n in cards)
            {
                if (n < 0) return false;
                sum += n;
            }
            return !nonEmpty || sum > 0;
        }

        private static TradeOffer FindOffer(TableState t, int id)
        {
            foreach (var o in t.Offers) if (o.Id == id) return o;
            return null;
        }

        // =====================================================================================================
        // Apply (called after a passing Check)
        // =====================================================================================================

        public CommandResult Apply(WorldState s, Command c, EventBuffer ev)
        {
            var t = s.Table;
            int p = c.PlayerId;
            switch (c)
            {
                case Sacrifice _:
                    if (t.Phase == TablePhase.NotOpened) Open(s, ev);
                    else if (t.Paused)
                    {
                        t.Paused = false;
                        ev.Add(new TableResumed());
                    }
                    break;

                case LeaveTable _:
                    t.Paused = true;
                    ev.Add(new TablePaused());
                    break;

                case PlaceTableSettlement ps:
                    if (t.Phase == TablePhase.Setup)
                    {
                        Place(t, p, ps.Corner);
                        t.SetupCorner = ps.Corner;
                        ev.Add(new Built { People = p, Kind = "settlement", Spot = ps.Corner, Setup = true });
                        AfterChange(s, ev);
                    }
                    else
                    {
                        Pay(t, p, TableRules.SettlementCost);
                        Place(t, p, ps.Corner);
                        ev.Add(new Built { People = p, Kind = "settlement", Spot = ps.Corner });
                        Acted(t, p);
                        AfterChange(s, ev);
                    }
                    break;

                case PlaceTableRoad pr:
                    if (t.Phase == TablePhase.Setup)
                    {
                        t.RoadOwner[pr.HexEdge] = p;
                        ev.Add(new Built { People = p, Kind = "road", Spot = pr.HexEdge, Setup = true });
                        GiveStartingCards(s, p, t.SetupCorner, ev);
                        AfterChange(s, ev);
                        if (t.Phase == TablePhase.Setup) NextSetupSeat(s, ev);
                    }
                    else
                    {
                        Pay(t, p, TableRules.RoadCost);
                        t.RoadOwner[pr.HexEdge] = p;
                        ev.Add(new Built { People = p, Kind = "road", Spot = pr.HexEdge });
                        Acted(t, p);
                        AfterChange(s, ev);
                    }
                    break;

                case UpgradeTableCity uc:
                    Pay(t, p, TableRules.CityCost);
                    t.CornerLevel[uc.Corner] = 2;
                    ev.Add(new Built { People = p, Kind = "city", Spot = uc.Corner });
                    Acted(t, p);
                    AfterChange(s, ev);
                    break;

                case BankTrade bt:
                {
                    int rate = TableRules.TradeRate(s, p, bt.Give);
                    var give = new int[Wares.RawCount];
                    var get = new int[Wares.RawCount];
                    give[Raw(bt.Give)] = rate;
                    get[Raw(bt.Get)] = 1;
                    t.Peoples[p].Hand[Raw(bt.Give)] -= rate;
                    t.Peoples[p].Hand[Raw(bt.Get)] += 1;
                    ev.Add(new Traded { People = p, With = -1, Give = give, Get = get, Rate = rate });
                    Acted(t, p);
                    break;
                }

                case OfferTrade ot:
                {
                    var o = new TradeOffer
                    {
                        Id = t.NextOfferId++,
                        From = p,
                        To = ot.To,
                        Give = (int[])ot.Give.Clone(),
                        Get = (int[])ot.Get.Clone(),
                        TicksLeft = t.Tuning.OfferTicks,
                    };
                    t.Offers.Add(o);
                    ev.Add(new OfferMade { OfferId = o.Id, From = p, To = o.To, Give = (int[])o.Give.Clone(), Get = (int[])o.Get.Clone() });
                    Acted(t, p);
                    break;
                }

                case AnswerOffer ao:
                    Answer(s, FindOffer(t, ao.OfferId), p, ao.Accept, ev);
                    Acted(t, p);
                    break;

                case EndTurn _:
                    EndTurnNow(s, "ended", ev);
                    break;

                case DiscardCards dc:
                    ResolveDiscard(s, p, dc.Cards, false, ev);
                    break;

                default:
                    return No(Reasons.Disabled);
            }
            return CommandResult.Accept();
        }

        private static void Place(TableState t, int p, int corner)
        {
            t.CornerOwner[corner] = p;
            t.CornerLevel[corner] = 1;
        }

        private static void Pay(TableState t, int p, int[] cost)
        {
            for (int g = 0; g < Wares.RawCount; g++) t.Peoples[p].Hand[g] -= cost[g];
        }

        /// <summary>An accepted action: refills the player's sand (HG-3) or lengthens an AI's turn (HG-5).</summary>
        private static void Acted(TableState t, int p)
        {
            if (t.Phase != TablePhase.Main || t.Current != p) return;
            if (p == t.HumanSeat)
            {
                t.Sand = Math.Min(t.Tuning.TurnCapTicks, t.Sand + t.Tuning.ActionRefillTicks);
                t.IdleTicks = 0;
            }
            else t.AiActions++;
        }

        // =====================================================================================================
        // Flow
        // =====================================================================================================

        /// <summary>First opening (GB-5): the setup round starts with seat 0.</summary>
        internal void Open(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            t.Phase = TablePhase.Setup;
            t.SetupSeat = 0;
            ev.Add(new TableOpened());
            BeginSetupSeat(s, ev);
        }

        private static void BeginSetupSeat(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            t.Current = t.SetupSeat;
            t.SetupCorner = -1;
            t.TurnTicks = 0;
            ev.Add(new TurnStarted { People = t.Current, Round = 0, Setup = true });
        }

        private void NextSetupSeat(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            ev.Add(new TurnEnded { People = t.Current, Reason = "ended" });
            t.SetupSeat++;
            if (t.SetupSeat < t.PeopleCount)
            {
                BeginSetupSeat(s, ev);
                return;
            }
            // Turn order fixed by seed (TU-1).
            var order = new List<int>();
            for (int i = 0; i < t.PeopleCount; i++) order.Add(i);
            t.RollRng.Shuffle(order);
            t.TurnOrder = order.ToArray();
            t.TurnIndex = 0;
            t.Round = 1;
            t.SetupCorner = -1;
            BeginTurn(s, ev);
        }

        private static void GiveStartingCards(WorldState s, int p, int corner, EventBuffer ev)
        {
            var cards = new int[Wares.RawCount];
            foreach (int h in s.Board.Topology.VertexHexes(corner))
            {
                var w = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                if (w.HasValue) cards[(int)w.Value]++;
            }
            for (int g = 0; g < Wares.RawCount; g++) s.Table.Peoples[p].Hand[g] += cards[g];
            ev.Add(new StartingCards { People = p, Cards = cards });
        }

        private void BeginTurn(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            t.Current = t.TurnOrder[t.TurnIndex];
            t.TurnTicks = 0;
            t.AiActions = 0;
            t.AiPlayed = false;
            t.Sand = Math.Min(t.Tuning.TurnBaseTicks, t.Tuning.TurnCapTicks);
            t.IdleTicks = 0;
            ev.Add(new TurnStarted { People = t.Current, Round = t.Round });
            Roll(s, ev);
        }

        /// <summary>The hidden roll (TU-1): 2d6 from the table's stream, then production or a 7.</summary>
        private void Roll(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            int roll = t.RollRng.NextRange(1, 7) + t.RollRng.NextRange(1, 7);
            t.LastRoll = roll;
            if (roll == 7) StartDiscards(s, ev);
            else
            {
                Produce(s, roll, ev);
                t.Phase = TablePhase.Main;
            }
        }

        /// <summary>Production (TU-2): 1 card per settlement, 2 per city, beside each matching resource hex that is not blocked.</summary>
        private void Produce(WorldState s, int roll, EventBuffer ev)
        {
            var t = s.Table;
            var b = s.Board;
            var topo = b.Topology;
            var hexes = new List<int>();
            var blocked = new List<int>();
            var gains = new int[t.PeopleCount * Wares.RawCount];
            for (int h = 0; h < topo.HexCount; h++)
            {
                if (b.HexNumber[h] != roll) continue;
                var w = TerrainInfo.RawWare(b.HexTerrain[h]);
                if (!w.HasValue) continue;
                if (Seams.HexBlocked(s, h))
                {
                    blocked.Add(h);
                    continue;
                }
                hexes.Add(h);
                foreach (int c in topo.HexCorners(h))
                {
                    int owner = t.CornerOwner[c];
                    if (owner < 0) continue;
                    int n = t.CornerLevel[c];
                    t.Peoples[owner].Hand[(int)w.Value] += n;
                    gains[owner * Wares.RawCount + (int)w.Value] += n;
                }
            }
            ev.Add(new Produced { Hexes = hexes.ToArray(), Blocked = blocked.ToArray(), Gains = gains });
        }

        /// <summary>A 7 (TU-10): peoples above the limit owe half their cards; AI seats choose at once, the player has DiscardTicks.</summary>
        private void StartDiscards(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            t.DiscardOwed = new int[t.PeopleCount];
            bool any = false;
            for (int p = 0; p < t.PeopleCount; p++)
            {
                int n = TableRules.HandSize(t.Peoples[p].Hand);
                if (n > t.Tuning.DiscardOver)
                {
                    t.DiscardOwed[p] = n / 2;
                    any = true;
                }
            }
            if (!any)
            {
                FinishSeven(s, ev);
                return;
            }
            t.Phase = TablePhase.Discard;
            t.DiscardTicksLeft = t.Tuning.DiscardTicks;
            ev.Add(new DiscardRequired { Owed = (int[])t.DiscardOwed.Clone() });
            for (int p = 0; p < t.PeopleCount; p++)
                if (t.DiscardOwed[p] > 0 && p != t.HumanSeat) AutoDiscard(s, p, ev);
        }

        private void AutoDiscard(WorldState s, int p, EventBuffer ev)
        {
            var t = s.Table;
            int owed = t.DiscardOwed[p];
            var cards = Seams.ChooseDiscard(s, p, owed);
            if (cards == null || cards.Length != Wares.RawCount || TableRules.HandSize(cards) != owed || !TableRules.CanPay(t.Peoples[p].Hand, cards))
                cards = TableDefaults.ChooseDiscard(s, p, owed); // a broken hook never breaks the rule
            ResolveDiscard(s, p, cards, true, ev);
        }

        private void ResolveDiscard(WorldState s, int p, int[] cards, bool auto, EventBuffer ev)
        {
            var t = s.Table;
            Pay(t, p, cards);
            t.DiscardOwed[p] = 0;
            ev.Add(new Discarded { People = p, Cards = (int[])cards.Clone(), Auto = auto });
            foreach (int n in t.DiscardOwed) if (n > 0) return;
            FinishSeven(s, ev);
        }

        private static void FinishSeven(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            ev.Add(new Epidemic { People = t.Current, Round = t.Round });
            t.Phase = TablePhase.Main;
            t.TurnTicks = 0; // the wait for discards does not use up an AI's turn
        }

        private void EndTurnNow(WorldState s, string reason, EventBuffer ev)
        {
            var t = s.Table;
            ExpireOffers(t, ev);
            ev.Add(new TurnEnded { People = t.Current, Reason = reason });
            t.TurnIndex++;
            if (t.TurnIndex >= t.TurnOrder.Length)
            {
                t.TurnIndex = 0;
                t.Round++;
            }
            BeginTurn(s, ev);
        }

        private static void ExpireOffers(TableState t, EventBuffer ev)
        {
            foreach (var o in t.Offers)
            {
                if (o.Status != OfferStatus.Open) continue;
                o.Status = OfferStatus.Expired;
                ev.Add(new OfferAnswered { OfferId = o.Id, By = -1, Status = OfferStatus.Expired });
            }
            t.Offers.RemoveAll(o => o.Status != OfferStatus.Open);
        }

        private static void Answer(WorldState s, TradeOffer o, int p, bool accept, EventBuffer ev)
        {
            var t = s.Table;
            if (accept)
            {
                var from = t.Peoples[o.From].Hand;
                var to = t.Peoples[p].Hand;
                for (int g = 0; g < Wares.RawCount; g++)
                {
                    from[g] += o.Get[g] - o.Give[g];
                    to[g] += o.Give[g] - o.Get[g];
                }
                o.Status = OfferStatus.Accepted;
                o.AnsweredBy = p;
                ev.Add(new OfferAnswered { OfferId = o.Id, By = p, Status = OfferStatus.Accepted });
                ev.Add(new Traded { People = o.From, With = p, Give = (int[])o.Give.Clone(), Get = (int[])o.Get.Clone(), OfferId = o.Id });
                return;
            }
            o.AnsweredBy = p;
            if (o.To >= 0)
            {
                o.Status = OfferStatus.Declined;
                ev.Add(new OfferAnswered { OfferId = o.Id, By = p, Status = OfferStatus.Declined });
                return;
            }
            if (!o.DeclinedBy.Contains(p)) o.DeclinedBy.Add(p);
            if (o.DeclinedBy.Count >= t.PeopleCount - 1)
            {
                o.Status = OfferStatus.Declined;
                ev.Add(new OfferAnswered { OfferId = o.Id, By = -1, Status = OfferStatus.Declined });
            }
        }

        /// <summary>After any change on the board: longest road, points, and the end of the age (TU-6, TU-7).</summary>
        private static void AfterChange(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            var tu = t.Tuning;
            int best = 0;
            for (int p = 0; p < t.PeopleCount; p++)
            {
                t.Peoples[p].RoadLength = TableRules.LongestRoad(s, p);
                best = Math.Max(best, t.Peoples[p].RoadLength);
            }
            int holder = t.LongestRoadHolder;
            if (best < tu.LongestRoadMin) holder = -1;
            else if (holder < 0 || t.Peoples[holder].RoadLength != best)
            {
                int count = 0, who = -1;
                for (int p = 0; p < t.PeopleCount; p++)
                    if (t.Peoples[p].RoadLength == best) { count++; if (who < 0) who = p; }
                holder = count == 1 ? who : -1; // a tie without the holder: nobody holds it
            }
            if (holder != t.LongestRoadHolder)
            {
                ev.Add(new LongestRoadChanged { Holder = holder, Previous = t.LongestRoadHolder, Length = best });
                t.LongestRoadHolder = holder;
            }

            int top = -1, topPts = -1;
            for (int p = 0; p < t.PeopleCount; p++)
            {
                int pts = TableRules.Points(s, p);
                if (pts != t.Peoples[p].Points)
                {
                    ev.Add(new PointsChanged { People = p, Points = pts, Delta = pts - t.Peoples[p].Points });
                    t.Peoples[p].Points = pts;
                }
                if (pts > topPts || (pts == topPts && p == t.Current)) { top = p; topPts = pts; }
            }
            if (topPts >= tu.WinPoints && t.Phase != TablePhase.Ended)
            {
                t.Winner = top;
                t.Phase = TablePhase.Ended;
                ExpireOffers(t, ev);
                ev.Add(new AgeEnded { Winner = top, Points = topPts });
            }
        }

        // =====================================================================================================
        // Tick
        // =====================================================================================================

        public void Tick(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            if (t == null || t.Paused || t.Phase == TablePhase.NotOpened || t.Phase == TablePhase.Ended) return;
            t.ActiveTicks++;
            t.TurnTicks++;

            TickOffers(s, ev);
            if (t.Phase == TablePhase.Ended) return;

            switch (t.Phase)
            {
                case TablePhase.Setup:
                    if (t.Current != t.HumanSeat && t.TurnTicks >= t.Tuning.AiSetupTicks) AiSetupPlace(s, ev);
                    break;

                case TablePhase.Discard:
                    if (t.HumanSeat >= 0 && t.DiscardOwed[t.HumanSeat] > 0 && --t.DiscardTicksLeft <= 0) AutoDiscard(s, t.HumanSeat, ev);
                    break;

                case TablePhase.Main:
                    if (t.Current == t.HumanSeat) TickSand(s, ev);
                    else TickAi(s, ev);
                    break;
            }
        }

        private void TickSand(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            t.IdleTicks++;
            t.Sand -= t.IdleTicks <= t.Tuning.IdleAfterTicks ? 1 : t.Tuning.IdleRate;
            if (t.Sand <= 0)
            {
                t.Sand = 0;
                EndTurnNow(s, "sand", ev);
            }
        }

        private void TickAi(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            if (!t.AiPlayed)
            {
                t.AiPlayed = true;
                Seams.AiTurn?.Invoke(s, t.Current, new TableActor(this, s, ev, t.Current));
                if (t.Phase != TablePhase.Main) return; // the age may have ended
            }
            if (t.TurnTicks >= t.Tuning.AiTurnTicks + t.Tuning.AiActionTicks * t.AiActions) EndTurnNow(s, "ai", ev);
        }

        private void AiSetupPlace(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            int p = t.Current;
            var (corner, edge) = Seams.AiSetup(s, p);
            if (TableRules.SettlementRefusal(s, p, corner, false) != null) (corner, edge) = TableDefaults.SetupPlacement(s, p);
            var r = Apply(s, new PlaceTableSettlement { PlayerId = p, Corner = corner }, ev);
            if (!r.Ok) return;
            if (TableRules.RoadRefusal(s, p, edge, corner) != null)
            {
                var legal = TableRules.LegalRoadEdges(s, p, corner);
                if (legal.Count == 0) return; // cannot happen on a real board
                edge = legal[0];
            }
            Apply(s, new PlaceTableRoad { PlayerId = p, HexEdge = edge }, ev);
        }

        private void TickOffers(WorldState s, EventBuffer ev)
        {
            var t = s.Table;
            bool changed = false;
            for (int i = 0; i < t.Offers.Count; i++)
            {
                var o = t.Offers[i];
                if (o.Status != OfferStatus.Open) continue;
                o.Age++;
                o.TicksLeft--;
                if (o.Age == t.Tuning.AiAnswerTicks) AiAnswers(s, o, ev);
                if (o.Status == OfferStatus.Open && o.TicksLeft <= 0)
                {
                    o.Status = OfferStatus.Expired;
                    ev.Add(new OfferAnswered { OfferId = o.Id, By = -1, Status = OfferStatus.Expired });
                }
                if (o.Status != OfferStatus.Open) changed = true;
            }
            if (changed) t.Offers.RemoveAll(o => o.Status != OfferStatus.Open);
        }

        /// <summary>AI seats answer AiAnswerTicks after an offer is made (TR-2): ascending id, the first that accepts takes it.</summary>
        private void AiAnswers(WorldState s, TradeOffer o, EventBuffer ev)
        {
            var t = s.Table;
            for (int p = 0; p < t.PeopleCount; p++)
            {
                if (p == o.From || p == t.HumanSeat || (o.To >= 0 && o.To != p)) continue;
                if (o.Status != OfferStatus.Open) return;
                bool yes = Seams.AiAnswer(s, p, o);
                var check = Check(s, new AnswerOffer { PlayerId = p, OfferId = o.Id, Accept = yes });
                if (!check.Ok) yes = false;
                if (yes || Check(s, new AnswerOffer { PlayerId = p, OfferId = o.Id, Accept = false }).Ok) Answer(s, o, p, yes, ev);
            }
        }
    }

    /// <summary>Default choosers: deterministic, simple, replaced by the AI package (G1-2) where it wants.</summary>
    public static class TableDefaults
    {
        /// <summary>Discards one card at a time from the largest pile (ties: lowest good id).</summary>
        public static int[] ChooseDiscard(WorldState s, int p, int count)
        {
            var hand = (int[])s.Table.Peoples[p].Hand.Clone();
            var cards = new int[Wares.RawCount];
            for (int i = 0; i < count; i++)
            {
                int best = -1;
                for (int g = 0; g < Wares.RawCount; g++) if (hand[g] > 0 && (best < 0 || hand[g] > hand[best])) best = g;
                if (best < 0) break;
                hand[best]--;
                cards[best]++;
            }
            return cards;
        }

        /// <summary>The legal corner with the most pips (ties: more different goods, then lowest id) and its lowest legal road.</summary>
        public static (int corner, int edge) SetupPlacement(WorldState s, int p)
        {
            int best = -1, bestPips = -1, bestKinds = -1;
            foreach (int c in TableRules.LegalSettlementCorners(s, p, false))
            {
                int pips = TableRules.CornerPips(s.Board, c);
                int kinds = Kinds(s.Board, c);
                if (pips > bestPips || (pips == bestPips && kinds > bestKinds)) { best = c; bestPips = pips; bestKinds = kinds; }
            }
            int edge = -1;
            if (best >= 0)
                foreach (int e in s.Board.Topology.CornerHexEdges(best))
                    if (s.Table.RoadOwner[e] < 0) { edge = e; break; }
            return (best, edge);
        }

        internal static int Kinds(Board b, int corner)
        {
            int mask = 0;
            foreach (int h in b.Topology.VertexHexes(corner))
            {
                var w = TerrainInfo.RawWare(b.HexTerrain[h]);
                if (w.HasValue) mask |= 1 << (int)w.Value;
            }
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }
    }
}
