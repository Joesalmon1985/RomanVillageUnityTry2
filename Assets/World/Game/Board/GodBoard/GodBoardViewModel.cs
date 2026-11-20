using System;
using System.Collections.Generic;
using World.Sim;
using World.Sim.Table;
using World.Sim.War;

namespace World.Game
{
    /// <summary>What the three buildable kinds look like to the player now (WU-2): cost, pieces left, affordability, legality.</summary>
    public struct KindView
    {
        public BuildKind Kind;
        public string Name;
        public int[] Cost;
        public int Left, Max;
        /// <summary>The hand covers the cost (ignores whose turn it is).</summary>
        public bool CanPay;
        /// <summary>Your turn, pieces left and the hand pays: the board lights spots of this kind.</summary>
        public bool Affordable;
        /// <summary>Goods the hand lacks (count per good), zeros when affordable.</summary>
        public int[] Missing;
    }

    /// <summary>
    /// The presentation-neutral model of the god board for the human (WU-2, split out of <c>GodBoardHud</c>): hand, costs and
    /// affordability, pieces left, discard need, points, hands, longest road, war state, bank and port rates, offers, the
    /// hourglass. Built from the sim state only (no Unity objects), so EditMode tests can check it against the sim, and both
    /// the altar ledge and the Clear view HUD read the same object, so they can never disagree. Counts and words only: no
    /// dice, number tokens, roll totals or odds (AB-1).
    /// </summary>
    public sealed class GodBoardViewModel
    {
        public static readonly string[] GoodNames = { "Wood", "Clay", "Grain", "Sheep", "Stone" };
        /// <summary>The offering baskets' names for the five goods.</summary>
        public static readonly string[] BasketNames = { "logs", "clay bricks", "sheaves", "fleece", "stones" };
        public static readonly BuildKind[] Kinds = { BuildKind.Road, BuildKind.Settlement, BuildKind.City };
        public static readonly string[] KindNames = { "Road", "Camp", "City" };

        public readonly WorldState S;
        public readonly TableState T;
        public readonly int Human;
        public readonly bool Valid;

        public TablePhase Phase;
        public bool Paused, Ended, IslandFell;
        /// <summary>It is the human's main turn and the table runs.</summary>
        public bool CanAct;
        /// <summary>It is the human's turn in the setup or the main round.</summary>
        public bool MyTurn;
        public int Current, Round, WinPoints, Winner;
        public int[] Hand = new int[Wares.RawCount];
        public int HandSize;
        public KindView[] Kind = new KindView[3];
        public int PeopleCount;
        public int[] Points = new int[0], Cards = new int[0], RoadLength = new int[0], Soldiers = new int[0];
        public int LongestHolder;
        public int LongestRoadMin;
        public int[] TurnOrder = new int[0];
        /// <summary>The human's bank rate for each good (4, or 3 or 2 at a jetty you own).</summary>
        public int[] BankRate = new int[Wares.RawCount];
        public int OwnedJetties;
        public int BankRateBase, GenericPortRate, GoodPortRate;
        public int DiscardOwed, DiscardTicksLeft, DiscardTicksMax;
        public bool MustDiscard;
        public int Sand, SandMax;
        /// <summary>Sand as a fraction of the cap, 0 when it is not your main turn.</summary>
        public float SandFraction;
        public List<TradeOffer> Incoming = new List<TradeOffer>();
        public TradeOffer OwnOffer;
        public int OfferTicks;
        /// <summary>Pairs (a, b) with a &lt; b that are at war.</summary>
        public List<int[]> WarPairs = new List<int[]>();

        public GodBoardViewModel(WorldState s, int human)
        {
            S = s; Human = human;
            T = s != null ? s.Table : null;
            if (T == null || human < 0 || human >= T.Peoples.Count) return;
            Valid = true;
            Phase = T.Phase; Paused = T.Paused; Ended = T.Phase == TablePhase.Ended; IslandFell = T.IslandFell;
            Current = T.Current; Round = T.Round; WinPoints = T.Tuning.WinPoints; Winner = T.Winner;
            PeopleCount = T.PeopleCount;
            TurnOrder = (int[])T.TurnOrder.Clone();
            LongestHolder = T.LongestRoadHolder; LongestRoadMin = T.Tuning.LongestRoadMin;
            MyTurn = !Paused && Current == human && (Phase == TablePhase.Setup || Phase == TablePhase.Main);
            CanAct = Phase == TablePhase.Main && Current == human && !Paused;
            var me = T.Peoples[human];
            Array.Copy(me.Hand, Hand, Wares.RawCount);
            HandSize = TableRules.HandSize(Hand);
            for (int k = 0; k < 3; k++)
            {
                var cost = k == 0 ? TableRules.RoadCost : k == 1 ? TableRules.SettlementCost : TableRules.CityCost;
                int left = k == 0 ? TableRules.RoadsLeft(T, human) : k == 1 ? TableRules.SettlementsLeft(T, human) : TableRules.CitiesLeft(T, human);
                int max = k == 0 ? T.Tuning.MaxRoads : k == 1 ? T.Tuning.MaxSettlements : T.Tuning.MaxCities;
                var miss = new int[Wares.RawCount];
                for (int g = 0; g < Wares.RawCount; g++) miss[g] = Math.Max(0, cost[g] - Hand[g]);
                bool pay = TableRules.CanPay(Hand, cost);
                Kind[k] = new KindView { Kind = Kinds[k], Name = KindNames[k], Cost = cost, Left = left, Max = max, CanPay = pay, Affordable = CanAct && left > 0 && pay, Missing = miss };
            }
            Points = new int[PeopleCount]; Cards = new int[PeopleCount]; RoadLength = new int[PeopleCount]; Soldiers = new int[PeopleCount];
            for (int p = 0; p < PeopleCount; p++)
            {
                Points[p] = TableRules.Points(s, p);
                Cards[p] = TableRules.HandSize(T.Peoples[p].Hand);
                RoadLength[p] = T.Peoples[p].RoadLength;
            }
            var sol = s.War != null ? s.War.Soldiers : null;
            if (sol != null) foreach (var x in sol) if (x.People >= 0 && x.People < PeopleCount) Soldiers[x.People]++;
            for (int a = 0; a < PeopleCount; a++)
                for (int b = a + 1; b < PeopleCount; b++)
                    if (WarApi.AtWar(s, a, b)) WarPairs.Add(new[] { a, b });
            BankRateBase = T.Tuning.BankRate; GenericPortRate = T.Tuning.GenericPortRate; GoodPortRate = T.Tuning.GoodPortRate;
            for (int g = 0; g < Wares.RawCount; g++) BankRate[g] = TableRules.TradeRate(s, human, (Ware)g);
            foreach (var port in T.Ports) if (TableRules.UsesPort(T, human, port)) OwnedJetties++;
            DiscardTicksMax = T.Tuning.DiscardTicks;
            MustDiscard = Phase == TablePhase.Discard && !Paused && human < T.DiscardOwed.Length && T.DiscardOwed[human] > 0;
            DiscardOwed = MustDiscard ? T.DiscardOwed[human] : 0;
            DiscardTicksLeft = MustDiscard ? T.DiscardTicksLeft : 0;
            Sand = T.Sand; SandMax = Math.Max(1, T.Tuning.TurnCapTicks);
            SandFraction = CanAct ? Math.Min(1f, Math.Max(0f, Sand / (float)SandMax)) : 0f;
            OfferTicks = Math.Max(1, T.Tuning.OfferTicks);
            if (!Paused && Phase == TablePhase.Main)
            {
                foreach (var o in T.Offers)
                    if (o.Status == OfferStatus.Open && o.From != human && (o.To == human || o.To < 0) && !o.DeclinedBy.Contains(human)) Incoming.Add(o);
                Incoming.Sort((x, y) => x.TicksLeft.CompareTo(y.TicksLeft));
            }
            foreach (var o in T.Offers) if (o.Status == OfferStatus.Open && o.From == human) { OwnOffer = o; break; }
        }

        /// <summary>The spots the board would light for a kind now (legal and affordable), or 0 if the kind is not offered.</summary>
        public int LegalSpotCount(BuildKind k)
        {
            if (!Valid || !CanAct) return 0;
            int i = Array.IndexOf(Kinds, k);
            if (i < 0 || !Kind[i].Affordable) return 0;
            switch (k)
            {
                case BuildKind.Road: return TableRules.LegalRoadEdges(S, Human).Count;
                case BuildKind.Settlement: return TableRules.LegalSettlementCorners(S, Human, true).Count;
                default:
                    int n = 0;
                    for (int c = 0; c < T.CornerOwner.Length; c++) if (T.CornerOwner[c] == Human && T.CornerLevel[c] == 1) n++;
                    return n;
            }
        }

        public KindView KindOf(BuildKind k) { int i = Array.IndexOf(Kinds, k); return i >= 0 ? Kind[i] : default; }

        public int GoodsNeeded(int[] cost) { int n = 0; foreach (int c in cost) n += c; return n; }

        public bool AtWar(int a, int b) { foreach (var p in WarPairs) if ((p[0] == a && p[1] == b) || (p[0] == b && p[1] == a)) return true; return false; }

        /// <summary>The people's display name from the sim's list ("You" for the human).</summary>
        public string NameOf(int p)
        {
            if (S != null) foreach (var pe in S.Peoples)
                    if (pe.Id == p)
                    {
                        if (p == Human) return "You";
                        string n = pe.Name ?? "People";
                        if (n.StartsWith("the ")) n = n.Substring(4);
                        return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : "People";
                    }
            return "People " + p;
        }

        public int SandSeconds => (int)Math.Ceiling(Sand / (double)WorldSim.TicksPerSecond);
    }
}
