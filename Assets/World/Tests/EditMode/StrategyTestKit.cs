using System;
using System.Collections.Generic;
using System.Linq;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>Helpers for strategy and military tests: setup, draft, rounds, invariants.</summary>
    internal static class StrategyTestKit
    {
        /// <summary>New game; ageAtOnce starts Age I with round 1 (no prologue); fast carriers by default.</summary>
        public static WorldSim NewSim(ulong seed = EconomyTestKit.DefaultSeed, bool ageAtOnce = true, int ticksPerEdge = 5)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            sim.State.Strategy.Tuning.StartAfterCommission = !ageAtOnce;
            sim.State.Economy.Tuning.TicksPerEdge = ticksPerEdge;
            return sim;
        }

        public static void Give(WorldState s, params object[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2) EconomyApi.AddToStorehouse(s, (Ware)pairs[i], (int)pairs[i + 1]);
        }

        public static List<SimEvent> Step(WorldSim sim, int n = 1)
        {
            var all = new List<SimEvent>();
            for (int i = 0; i < n; i++)
            {
                sim.Step();
                all.AddRange(sim.DrainEvents());
            }
            return all;
        }

        public static List<SimEvent> Do(WorldSim sim, Command c)
        {
            if (c.PlayerId == 0) c.PlayerId = PlayerIds.Human;
            EconomyTestKit.Submit(sim, c);
            return Step(sim);
        }

        /// <summary>Pick the first card offered until the draft is over.</summary>
        public static List<SimEvent> FinishDraft(WorldSim sim)
        {
            var evs = new List<SimEvent>();
            while (StrategyApi.DraftPending(sim.State))
                evs.AddRange(Do(sim, new DraftPick { CardId = sim.State.Strategy.Sides[0].DraftHand[0] }));
            return evs;
        }

        /// <summary>Finish any draft, end the round and step once (the next round starts in the same tick).</summary>
        public static List<SimEvent> EndRound(WorldSim sim)
        {
            var evs = FinishDraft(sim);
            evs.AddRange(Do(sim, new EndRound()));
            return evs;
        }

        public static int CardOf(WorldState s, string defId) => s.Strategy.Cards.First(c => c.DefId == defId).Id;

        /// <summary>Test setup: move a card into the player's hand from wherever it is (not from the built lists).</summary>
        public static void ForceIntoHand(WorldState s, string defId)
        {
            int id = CardOf(s, defId);
            foreach (var side in s.Strategy.Sides)
            {
                side.Hand.Remove(id);
                side.DraftHand.Remove(id);
                side.Discarded.Remove(id);
            }
            s.Strategy.Sides[0].Hand.Add(id);
        }

        /// <summary>Place a building on a chosen vertex (or the best one), connect it and wait until it is Active.</summary>
        public static int Build(WorldSim sim, BuildingType t, Func<int, bool> filter = null) => EconomyTestKit.Build(sim, t, filter);

        /// <summary>Build roads along a shortest all-edge path from the network to v (each one touching the network).</summary>
        public static void RoadTo(WorldSim sim, int v)
        {
            var s = sim.State;
            var path = EconomyTestKit.PathToNetwork(s, v);
            if (path == null) return;
            var edges = s.Board.Topology.PathEdges(path).ToList();
            edges.Reverse();
            foreach (int e in edges)
                if (!s.Economy.Roads[e]) Do(sim, new PlaceRoad { Edge = e });
        }

        /// <summary>
        /// The corner at the far end of the demonstration road's hex edge (one hex edge = 3 segments from home;
        /// the demo road covers the first 2 segments: home -> DemoVertexA -> DemoVertexB -> DemoCorner).
        /// </summary>
        public static int DemoCorner(WorldState s)
        {
            var topo = s.Board.Topology;
            int he = topo.HexEdgeOfFlag(s.Economy.DemoVertexA);
            var f = topo.HexEdgeFlags(he);
            return f[0] == s.Board.HomeCityVertex ? f[3] : f[0];
        }

        /// <summary>
        /// Roads out to a corner two hex edges from home that is a legal city site except for cost:
        /// home -> demo road -> DemoCorner -> next corner. Returns that corner.
        /// </summary>
        public static int RoadToSecondCitySite(WorldSim sim)
        {
            var s = sim.State;
            var topo = s.Board.Topology;
            int c = DemoCorner(s);
            int site = topo.CornerNeighbours(c).First(v => v != s.Board.HomeCityVertex
                && StrategyApi.CheckCity(s, v).Reason == StrategyRefusals.NoRoad);
            RoadTo(sim, site);
            return site;
        }

        /// <summary>Null when all economy, strategy and military invariants hold.</summary>
        public static string CheckInvariants(WorldState s)
        {
            var err = EconomyTestKit.CheckInvariants(s);
            if (err != null) return err;
            var st = s.Strategy;
            var led = s.Economy.Ledger;
            var spent = StrategyApi.WaresSpent(s);
            for (int w = 0; w < Wares.Count; w++)
            {
                if (spent[w] + s.Military.ConsumedForUnits[w] > led.Consumed[w]) return "strategy/military spending of " + (Ware)w + " exceeds economy consumption";
                if (st.Ledger.TradeReceived[w] > led.Added[w]) return "trade received " + (Ware)w + " exceeds economy additions";
            }
            foreach (var p in s.Players) if (p.Coin < 0) return "negative coin for " + p.Name;
            for (int v = 0; v < st.Demons.Length; v++)
                if (st.Demons[v] < 0 || st.Demons[v] > StrategyApi.DemonCap(s)) return "demons out of range at " + v;
            if (st.Breaches > st.Tuning.BreachLimit) return "breaches over the limit";
            if (st.Breaches >= st.Tuning.BreachLimit && !st.GameOver) return "breach limit reached without an ending";
            // Every card id in at most one place.
            var seen = new HashSet<int>();
            foreach (var side in st.Sides)
            {
                foreach (int id in side.Hand.Concat(side.DraftHand).Concat(side.Discarded).Concat(side.Built.Select(b => b.CardId)))
                    if (!seen.Add(id)) return "card " + id + " is in two places";
                if (side.Built.GroupBy(b => b.CityVertex).Any(g => g.Count() > st.Tuning.MaxCardsPerCity && side.PlayerId == 0)) return "city over card limit";
            }
            if (st.InfectionDeck.Count + st.InfectionDiscard.Count != st.InfectionDeck.Concat(st.InfectionDiscard).Distinct().Count())
                return "infection card duplicated";
            var m = s.Military;
            if (m.Outpost.Hp < 0 || m.Outpost.Hp > m.Outpost.MaxHp) return "outpost hp out of range";
            foreach (var sq in m.Squads)
            {
                if (!sq.Alive) continue;
                if (sq.Units.Any(u => u.Hp <= 0)) return "dead unit in squad " + sq.Id;
                if (sq.Owner == PlayerIds.Human && sq.Units.Count > m.Tuning.MaxSquadSize) return "squad over size";
            }
            foreach (var q in m.Queues) if (q.Reason != null && !Reasons.IsKnown(q.Reason)) return "unknown queue reason " + q.Reason;
            return null;
        }
    }
}
