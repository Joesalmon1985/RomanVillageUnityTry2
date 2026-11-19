using System;
using System.Collections.Generic;

namespace World.Sim.Plague
{
    /// <summary>Banish spell (PL-11). Node = plague node id (1-based). Kind = demon kind to remove, -1 = the commonest kind there.</summary>
    public sealed class Banish : Command { public int Node; public int Kind = -1; }

    /// <summary>
    /// The plague rules (PRD_GODBOARD PL-5..PL-11, CC-3, HI-8) as plain functions the turn engine calls. Nothing here
    /// runs by itself: there is no per-tick system. Deterministic from <see cref="PlagueState.Rng"/>.
    /// </summary>
    public static class PlagueRules
    {
        /// <summary>Rate track (PL-7).</summary>
        public static readonly int[] RateTrack = { 1, 1, 2, 2, 2, 3, 3, 3, 4 };
        public const int GraceRounds = 6;
        public const int BanishCooldownTicks = 300;
        public const int MaxDemons = 3;
        public const int OutbreaksToFall = 8;
        public const int EpidemicDemons = 3;

        /// <summary>The once-per-round draw (PL-5): rate cards, 1 demon each (2 for a strengthened kind, none for a cured one).</summary>
        public static bool InfectRound(WorldState s, EventBuffer ev)
        {
            var p = Live(s);
            if (p == null) return false;
            p.Rounds++;
            int rate = RateTrack[p.RateIndex];
            for (int i = 0; i < rate && !p.IslandFell; i++)
            {
                int card = DrawTop(p);
                p.Discards.Add(card);
                int kind = PlagueApi.ElementOf(card);
                int n = p.Strengthened[kind] ? 2 : 1;
                for (int k = 0; k < n && !p.IslandFell; k++) Place(p, card, kind, "draw", ev);
            }
            return true;
        }

        /// <summary>An epidemic (PL-6). Does nothing before <see cref="GraceRounds"/> rounds have been drawn. Returns whether it happened.</summary>
        public static bool Epidemic(WorldState s, EventBuffer ev)
        {
            var p = Live(s);
            if (p == null || p.Rounds < GraceRounds) return false;
            p.Epidemics++;
            p.RateIndex = Math.Min(p.RateIndex + 1, RateTrack.Length - 1);
            int card = DrawBottom(p);
            p.Discards.Add(card);
            ev?.Add(new Epidemic { Node = card, RateIndex = p.RateIndex, Rate = RateTrack[p.RateIndex] });
            int kind = PlagueApi.ElementOf(card);
            for (int k = 0; k < EpidemicDemons && !p.IslandFell; k++) Place(p, card, kind, "epidemic", ev);
            // The discards, shuffled, go on top of what is left of the deck.
            p.Rng.Shuffle(p.Discards);
            p.Deck.InsertRange(0, p.Discards);
            p.Discards.Clear();
            return true;
        }

        /// <summary>Removes one demon from a node (no cooldown check; the command checks it). Kind -1 = the commonest kind there.</summary>
        public static bool BanishAt(WorldState s, int node, int kind, EventBuffer ev)
        {
            var p = s?.Plague;
            if (p == null || p.Ended || !PlagueApi.IsNode(node) || p.Demons[node - 1] <= 0) return false;
            if (kind < 0)
            {
                int best = 0;
                for (int k = 0; k < 4; k++) if (p.KindCount[(node - 1) * 4 + k] > p.KindCount[(node - 1) * 4 + best]) best = k;
                kind = best;
            }
            if (kind > 3 || p.KindCount[(node - 1) * 4 + kind] <= 0) return false;
            p.KindCount[(node - 1) * 4 + kind]--;
            p.Demons[node - 1]--;
            p.Banished++;
            ev?.Add(new Banished { Node = node, Kind = kind, Left = p.Demons[node - 1] });
            CheckEradicated(s, kind, ev);
            return true;
        }

        /// <summary>A restored crystal cures its kind (CC-3). No new demons of it are placed and it never chains.</summary>
        public static bool Cure(WorldState s, int element, EventBuffer ev)
        {
            var p = Live(s);
            if (p == null || element < 0 || element > 3 || p.Cured[element]) return false;
            p.Cured[element] = true;
            p.Strengthened[element] = false;
            ev?.Add(new Cured { Element = element });
            CheckEradicated(s, element, ev);
            return true;
        }

        /// <summary>A crystal on an Ash altar strengthens its kind: each draw places 2 demons (CC-3). Refused for a cured kind.</summary>
        public static bool Strengthen(WorldState s, int element, EventBuffer ev)
        {
            var p = Live(s);
            if (p == null || element < 0 || element > 3 || p.Cured[element] || p.Strengthened[element]) return false;
            p.Strengthened[element] = true;
            ev?.Add(new Strengthened { Element = element });
            return true;
        }

        /// <summary>The Historic switch (HI-8): clears every demon and stops everything. Idempotent.</summary>
        public static bool End(WorldState s, EventBuffer ev)
        {
            var p = s?.Plague;
            if (p == null || p.Ended) return false;
            int cleared = PlagueApi.TotalDemons(s);
            Array.Clear(p.Demons, 0, p.Demons.Length);
            Array.Clear(p.KindCount, 0, p.KindCount.Length);
            p.Deck.Clear();
            p.Discards.Clear();
            p.Ended = true;
            ev?.Add(new PlagueEnded { Cleared = cleared });
            return true;
        }

        // ---- internals

        private static PlagueState Live(WorldState s)
        {
            var p = s?.Plague;
            return p == null || p.Ended || p.IslandFell ? null : p;
        }

        private static void Refill(PlagueState p)
        {
            if (p.Deck.Count > 0) return;
            if (p.Discards.Count > 0) { p.Deck.AddRange(p.Discards); p.Discards.Clear(); }
            else for (int i = 1; i <= PlagueGraphData.NodeCount; i++) p.Deck.Add(i);
            p.Rng.Shuffle(p.Deck);
        }

        private static int DrawTop(PlagueState p)
        {
            Refill(p);
            int c = p.Deck[0];
            p.Deck.RemoveAt(0);
            return c;
        }

        private static int DrawBottom(PlagueState p)
        {
            Refill(p);
            int c = p.Deck[p.Deck.Count - 1];
            p.Deck.RemoveAt(p.Deck.Count - 1);
            return c;
        }

        /// <summary>Puts one demon of a kind on a node: a cured kind is ignored, a fourth demon is an outbreak (PL-8).</summary>
        private static void Place(PlagueState p, int node, int kind, string reason, EventBuffer ev)
        {
            if (p.Cured[kind]) return;
            if (p.Demons[node - 1] < MaxDemons) { Add(p, node, kind, reason, ev); return; }
            RunOutbreak(p, node, kind, ev);
        }

        private static void Add(PlagueState p, int node, int kind, string reason, EventBuffer ev)
        {
            p.Demons[node - 1]++;
            p.KindCount[(node - 1) * 4 + kind]++;
            ev?.Add(new Infected { Node = node, Kind = kind, Count = 1, Reason = reason });
        }

        /// <summary>
        /// One chain (PL-8). Breadth first in node-id order, each node outbreaks at most once per chain; the counter goes up
        /// by 1 per outbreaking node. A cured kind never gets here.
        /// </summary>
        private static void RunOutbreak(PlagueState p, int source, int kind, EventBuffer ev)
        {
            var done = new HashSet<int> { source };
            var queue = new Queue<int>();
            queue.Enqueue(source);
            int chain = 0;
            Break(p, source, kind, chain++, ev);
            while (queue.Count > 0 && !p.IslandFell)
            {
                int n = queue.Dequeue();
                foreach (int nb in PlagueApi.Neighbours(n))
                {
                    if (p.IslandFell) return;
                    if (done.Contains(nb)) continue;
                    if (p.Demons[nb - 1] < MaxDemons) { Add(p, nb, kind, "spread", ev); continue; }
                    done.Add(nb);
                    queue.Enqueue(nb);
                    Break(p, nb, kind, chain++, ev);
                    if (p.IslandFell) return;
                }
            }
        }

        private static void Break(PlagueState p, int node, int kind, int chain, EventBuffer ev)
        {
            p.Outbreaks++;
            ev?.Add(new Outbreak { Node = node, Kind = kind, Chain = chain, Total = p.Outbreaks });
            if (p.Outbreaks >= OutbreaksToFall && !p.IslandFell)
            {
                p.IslandFell = true;
                ev?.Add(new IslandFell { Outbreaks = p.Outbreaks });
            }
        }

        private static void CheckEradicated(WorldState s, int kind, EventBuffer ev)
        {
            var p = s.Plague;
            if (p.Cured[kind] && !p.Eradicated[kind] && PlagueApi.DemonsOfKind(s, kind) == 0)
            {
                p.Eradicated[kind] = true;
                ev?.Add(new Eradicated { Element = kind });
            }
        }
    }

    internal sealed class BanishHandler : CommandHandler<Banish>
    {
        protected override CommandResult Validate(WorldState s, Banish c)
        {
            var p = s.Plague;
            if (p == null || p.Ended || p.IslandFell) return PlagueRefusals.Reject(PlagueRefusals.PlagueOver);
            if (!PlagueApi.IsNode(c.Node)) return PlagueRefusals.Reject(PlagueRefusals.UnknownNode);
            if (PlagueApi.DemonsAt(s, c.Node) <= 0 || (c.Kind >= 0 && PlagueApi.DemonsOfKindAt(s, c.Node, c.Kind) <= 0))
                return PlagueRefusals.Reject(PlagueRefusals.NoDemons);
            if (s.Tick < p.BanishReadyTick) return PlagueRefusals.Reject(PlagueRefusals.BanishCooling, p.BanishReadyTick - s.Tick);
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, Banish c, EventBuffer ev)
        {
            var r = Validate(s, c);
            if (!r.Ok) return r;
            PlagueRules.BanishAt(s, c.Node, c.Kind, ev);
            s.Plague.BanishReadyTick = s.Tick + PlagueRules.BanishCooldownTicks;
            return CommandResult.Accept();
        }
    }

    /// <summary>Entry point of the Plague module. Not wired into WorldSimFactory yet (G2-1 core); the turn engine package does that.</summary>
    public static class PlagueModule
    {
        /// <summary>Fresh plague: no demons, a shuffled 48-card deck, rate 1, own Rng forked from the world seed.</summary>
        public static void InitState(WorldState s)
        {
            var p = new PlagueState
            {
                Demons = new int[PlagueGraphData.NodeCount],
                KindCount = new int[PlagueGraphData.NodeCount * 4],
                Deck = new List<int>(),
                Discards = new List<int>(),
                Cured = new bool[4],
                Eradicated = new bool[4],
                Strengthened = new bool[4],
                Rng = s.Rng.Fork("plague"),
            };
            for (int i = 1; i <= PlagueGraphData.NodeCount; i++) p.Deck.Add(i);
            p.Rng.Shuffle(p.Deck);
            s.Plague = p;
        }

        public static void Register(WorldSim sim)
        {
            sim.RegisterHandler(new BanishHandler());
        }
    }
}
