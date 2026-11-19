using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using World.Sim.Plague;

namespace World.Sim.Tests
{
    /// <summary>Plague sim (PRD_GODBOARD PL-1..PL-11, CC-3, HI-8).</summary>
    public class PlagueTests
    {
        private static WorldSim NewSim(ulong seed = 7)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            PlagueModule.InitState(sim.State);
            PlagueModule.Register(sim);
            return sim;
        }

        private static List<SimEvent> Evs(EventBuffer b) => b.Items.ToList();

        private static string FindJson()
        {
            string d = TestContext.CurrentContext.TestDirectory;
            while (d != null && !File.Exists(Path.Combine(d, "Docs", "godboard_plague_graph.json"))) d = Path.GetDirectoryName(d);
            return d == null ? null : Path.Combine(d, "Docs", "godboard_plague_graph.json");
        }

        // ---- graph

        [Test]
        public void Graph_MatchesJson()
        {
            string path = FindJson();
            Assert.NotNull(path, "Docs/godboard_plague_graph.json not found");
            var j = JObject.Parse(File.ReadAllText(path));
            var nodes = (JArray)j["nodes"];
            var trails = (JArray)j["trails"];
            Assert.AreEqual(48, nodes.Count);
            Assert.AreEqual(48, PlagueGraphData.NodeCount);
            Assert.AreEqual(73, trails.Count);
            Assert.AreEqual(73, PlagueGraphData.TrailCount);
            string[] names = { "Earth", "Fire", "Water", "Air" };
            for (int i = 0; i < 48; i++)
            {
                Assert.AreEqual(i + 1, (int)nodes[i]["id"]);
                Assert.AreEqual((string)nodes[i]["element"], names[PlagueGraphData.Element[i]]);
                Assert.AreEqual((int)nodes[i]["q"], PlagueGraphData.HexQ[i]);
                Assert.AreEqual((int)nodes[i]["r"], PlagueGraphData.HexR[i]);
                Assert.AreEqual((float)nodes[i]["x"], PlagueGraphData.X[i], 0.05f);
                Assert.AreEqual((float)nodes[i]["z"], PlagueGraphData.Z[i], 0.05f);
            }
            string[] kinds = { "trail", "pass", "tunnel" };
            for (int t = 0; t < 73; t++)
            {
                Assert.AreEqual((int)trails[t]["a"], PlagueGraphData.TrailA[t]);
                Assert.AreEqual((int)trails[t]["b"], PlagueGraphData.TrailB[t]);
                Assert.AreEqual((string)trails[t]["kind"], kinds[PlagueGraphData.TrailKind[t]]);
            }
        }

        [Test]
        public void Graph_Shape()
        {
            for (int e = 0; e < 4; e++) Assert.AreEqual(12, PlagueGraphData.Element.Count(x => x == e), "element " + e);
            Assert.AreEqual(7, PlagueGraphData.TrailKind.Count(k => k != 0));
            Assert.AreEqual(1, PlagueGraphData.TrailKind.Count(k => k == 2));
            var seen = new HashSet<int> { 1 };
            var q = new Queue<int>(new[] { 1 });
            while (q.Count > 0) foreach (int nb in PlagueApi.Neighbours(q.Dequeue())) if (seen.Add(nb)) q.Enqueue(nb);
            Assert.AreEqual(48, seen.Count);
            for (int n = 1; n <= 48; n++) Assert.That(PlagueApi.Neighbours(n).Length, Is.InRange(1, 5));
        }

        [Test]
        public void Graph_EveryNodeOnAResourceHex_TenHexesCarryTwo()
        {
            var s = NewSim().State;
            var topo = s.Board.Topology;
            var perHex = new Dictionary<int, int>();
            for (int n = 1; n <= 48; n++)
            {
                int h = topo.HexIndex(new HexCoord(PlagueGraphData.HexQ[n - 1], PlagueGraphData.HexR[n - 1]));
                Assert.GreaterOrEqual(h, 0, "node " + n + " off the board");
                Assert.AreNotEqual(Terrain.Meadow, s.Board.TerrainAt(h), "node " + n + " on Meadow");
                perHex[h] = perHex.TryGetValue(h, out var c) ? c + 1 : 1;
            }
            Assert.AreEqual(38, perHex.Count);
            Assert.AreEqual(10, perHex.Values.Count(v => v == 2));
        }

        [Test]
        public void Graph_OnMeadowFreeSetOfBoardZero()
        {
            var b = BoardGenerator.CreateMvpBoard(0);
            var topo = b.Topology;
            for (int n = 1; n <= 48; n++)
            {
                int h = topo.HexIndex(new HexCoord(PlagueGraphData.HexQ[n - 1], PlagueGraphData.HexR[n - 1]));
                Assert.AreNotEqual(Terrain.Meadow, b.TerrainAt(h), "node " + n);
            }
        }

        // ---- state and determinism

        [Test]
        public void InitState_DeckHas48Cards_RateOne_NoDemons()
        {
            var s = NewSim().State;
            Assert.AreEqual(48, s.Plague.Deck.Distinct().Count());
            Assert.AreEqual(1, PlagueApi.Rate(s));
            Assert.AreEqual(0, PlagueApi.TotalDemons(s));
            Assert.IsTrue(PlagueApi.Active(s));
        }

        [Test]
        public void Deterministic_FromTheSeed()
        {
            string Run(ulong seed)
            {
                var s = NewSim(seed).State;
                var ev = new EventBuffer();
                for (int i = 0; i < 30 && !s.Plague.IslandFell; i++) { PlagueRules.InfectRound(s, ev); if (i % 3 == 2) PlagueRules.Epidemic(s, ev); }
                return string.Join(",", s.Plague.Demons) + "|" + string.Join(",", s.Plague.Deck) + "|" + s.Plague.Outbreaks + "|" + ev.Count;
            }
            Assert.AreEqual(Run(5), Run(5));
            Assert.AreNotEqual(Run(5), Run(6));
        }

        // ---- infect

        [Test]
        public void InfectRound_DrawsRateCards_OneDemonEach()
        {
            var s = NewSim().State;
            var ev = new EventBuffer();
            Assert.IsTrue(PlagueRules.InfectRound(s, ev));
            Assert.AreEqual(1, PlagueApi.TotalDemons(s));
            Assert.AreEqual(1, s.Plague.Discards.Count);
            Assert.AreEqual(47, s.Plague.Deck.Count);
            var inf = Evs(ev).OfType<Infected>().Single();
            Assert.AreEqual(s.Plague.Discards[0], inf.Node);
            Assert.AreEqual(PlagueApi.ElementOf(inf.Node), inf.Kind);
            s.Plague.RateIndex = 5; // rate 3
            PlagueRules.InfectRound(s, ev);
            Assert.AreEqual(4, PlagueApi.TotalDemons(s));
            Assert.AreEqual(4, s.Plague.Discards.Count);
            Assert.AreEqual(2, s.Plague.Rounds);
        }

        [Test]
        public void InfectRound_Strengthened_TwoDemons_Cured_None()
        {
            var s = NewSim().State;
            int top = s.Plague.Deck[0];
            int kind = PlagueApi.ElementOf(top);
            Assert.IsTrue(PlagueRules.Strengthen(s, kind, null));
            PlagueRules.InfectRound(s, null);
            Assert.AreEqual(2, PlagueApi.DemonsAt(s, top));

            var s2 = NewSim().State;
            top = s2.Plague.Deck[0];
            kind = PlagueApi.ElementOf(top);
            Assert.IsTrue(PlagueRules.Cure(s2, kind, null));
            var ev = new EventBuffer();
            PlagueRules.InfectRound(s2, ev);
            Assert.AreEqual(0, PlagueApi.DemonsAt(s2, top));
            Assert.IsEmpty(Evs(ev).OfType<Infected>());
            Assert.AreEqual(1, s2.Plague.Discards.Count, "the card is still discarded");
        }

        [Test]
        public void EmptyDeck_RefillsFromDiscards()
        {
            var s = NewSim().State;
            for (int i = 0; i < 48; i++) PlagueRules.InfectRound(s, null);
            if (s.Plague.IslandFell) Assert.Pass("fell before the deck ran out");
            Assert.AreEqual(0, s.Plague.Deck.Count);
            PlagueRules.InfectRound(s, null);
            Assert.AreEqual(1, s.Plague.Discards.Count);
            Assert.AreEqual(47, s.Plague.Deck.Count);
        }

        // ---- epidemic

        [Test]
        public void Epidemic_NothingInGrace_ThenRateUp_DiscardsShuffledOnTop()
        {
            var s = NewSim().State;
            Assert.IsFalse(PlagueRules.Epidemic(s, null));
            Assert.AreEqual(0, s.Plague.RateIndex);
            for (int i = 0; i < PlagueRules.GraceRounds; i++) PlagueRules.InfectRound(s, null);
            var discards = new List<int>(s.Plague.Discards);
            int bottom = s.Plague.Deck.Last();
            int deckBefore = s.Plague.Deck.Count;
            var ev = new EventBuffer();
            Assert.IsTrue(PlagueRules.Epidemic(s, ev));
            Assert.AreEqual(1, s.Plague.RateIndex);
            Assert.AreEqual(bottom, Evs(ev).OfType<Epidemic>().Single().Node);
            Assert.AreEqual(0, s.Plague.Discards.Count);
            Assert.AreEqual(deckBefore - 1 + discards.Count + 1, s.Plague.Deck.Count);
            CollectionAssert.AreEquivalent(discards.Concat(new[] { bottom }), s.Plague.Deck.Take(discards.Count + 1));
        }

        [Test]
        public void Epidemic_BottomNodeGetsThreeDemons()
        {
            var s = NewSim().State;
            s.Plague.Rounds = PlagueRules.GraceRounds;
            int bottom = s.Plague.Deck.Last();
            var ev = new EventBuffer();
            PlagueRules.Epidemic(s, ev);
            Assert.AreEqual(3, PlagueApi.DemonsAt(s, bottom));
            Assert.AreEqual(3, Evs(ev).OfType<Infected>().Count(i => i.Reason == "epidemic"));
        }

        [Test]
        public void RateTrack_Values_AndStopsAtTheEnd()
        {
            var s = NewSim().State;
            s.Plague.Rounds = PlagueRules.GraceRounds;
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 2, 3, 3, 3, 4 }, PlagueRules.RateTrack);
            for (int i = 0; i < 12 && !s.Plague.IslandFell; i++) PlagueRules.Epidemic(s, null);
            Assert.LessOrEqual(s.Plague.RateIndex, 8);
        }

        // ---- outbreaks

        private static void Fill(PlagueState p, int node, int kind, int n)
        {
            p.Demons[node - 1] = n;
            p.KindCount[(node - 1) * 4 + kind] = n;
        }

        private static void RigTop(PlagueState p, int node)
        {
            p.Deck.Remove(node);
            p.Deck.Insert(0, node);
        }

        [Test]
        public void Outbreak_SpreadsOneToEveryNeighbour_OfTheSourcesKind()
        {
            var s = NewSim().State;
            int src = 1;
            int kind = PlagueApi.ElementOf(src);
            Fill(s.Plague, src, kind, 3);
            RigTop(s.Plague, src);
            var ev = new EventBuffer();
            PlagueRules.InfectRound(s, ev);
            Assert.AreEqual(1, s.Plague.Outbreaks);
            Assert.AreEqual(3, PlagueApi.DemonsAt(s, src), "stays at 3");
            foreach (int nb in PlagueApi.Neighbours(src))
            {
                Assert.AreEqual(1, PlagueApi.DemonsAt(s, nb));
                Assert.AreEqual(1, PlagueApi.DemonsOfKindAt(s, nb, kind));
            }
            Assert.AreEqual(PlagueApi.Neighbours(src).Length, Evs(ev).OfType<Infected>().Count(i => i.Reason == "spread"));
        }

        [Test]
        public void Outbreak_Chains_EachNodeOnce_CounterPerNode()
        {
            var s = NewSim().State;
            int a = 1, b = PlagueApi.Neighbours(1)[0];
            int kind = PlagueApi.ElementOf(a);
            Fill(s.Plague, a, kind, 3);
            Fill(s.Plague, b, kind, 3);
            RigTop(s.Plague, a);
            var ev = new EventBuffer();
            PlagueRules.InfectRound(s, ev);
            Assert.AreEqual(2, s.Plague.Outbreaks);
            var outs = Evs(ev).OfType<Outbreak>().ToList();
            CollectionAssert.AreEqual(new[] { a, b }, outs.Select(o => o.Node));
            CollectionAssert.AreEqual(new[] { 0, 1 }, outs.Select(o => o.Chain));
            Assert.AreEqual(3, PlagueApi.DemonsAt(s, a), "the source is not refilled by its neighbour's outbreak");
            Assert.AreEqual(3, PlagueApi.DemonsAt(s, b));
        }

        [Test]
        public void Outbreak_FullRegion_ChainsThroughAll_UntilTheIslandFalls()
        {
            var s = NewSim().State;
            for (int n = 1; n <= 12; n++) Fill(s.Plague, n, 0, 3);
            RigTop(s.Plague, 1);
            PlagueRules.InfectRound(s, null);
            Assert.AreEqual(8, s.Plague.Outbreaks, "stops at the 8th: the island falls");
            Assert.IsTrue(s.Plague.IslandFell);
        }

        [Test]
        public void Outbreak_FullSmallChain_CountsEachNodeOnce()
        {
            var s = NewSim().State;
            // three full nodes in a row along trails: 1 and two of its neighbours.
            int a = 1;
            var nbs = PlagueApi.Neighbours(a);
            int kind = PlagueApi.ElementOf(a);
            Fill(s.Plague, a, kind, 3);
            Fill(s.Plague, nbs[0], kind, 3);
            if (nbs.Length > 1) Fill(s.Plague, nbs[1], kind, 3);
            int full = nbs.Length > 1 ? 3 : 2;
            RigTop(s.Plague, a);
            PlagueRules.InfectRound(s, null);
            Assert.AreEqual(full, s.Plague.Outbreaks);
        }

        [Test]
        public void CuredKind_DoesNotChain_OrPlace()
        {
            var s = NewSim().State;
            int src = 1, kind = PlagueApi.ElementOf(src);
            Fill(s.Plague, src, kind, 3);
            PlagueRules.Cure(s, kind, null);
            RigTop(s.Plague, src);
            PlagueRules.InfectRound(s, null);
            Assert.AreEqual(0, s.Plague.Outbreaks);
            foreach (int nb in PlagueApi.Neighbours(src)) Assert.AreEqual(0, PlagueApi.DemonsAt(s, nb));
        }

        [Test]
        public void IslandFalls_AtEight_ThenEverythingStops()
        {
            var s = NewSim().State;
            var ev = new EventBuffer();
            s.Plague.Outbreaks = 7;
            Fill(s.Plague, 1, 0, 3);
            RigTop(s.Plague, 1);
            PlagueRules.InfectRound(s, ev);
            Assert.IsTrue(s.Plague.IslandFell);
            Assert.AreEqual(8, s.Plague.Outbreaks);
            Assert.AreEqual(1, Evs(ev).OfType<IslandFell>().Count());
            Assert.IsFalse(PlagueRules.InfectRound(s, ev));
            Assert.IsFalse(PlagueRules.Epidemic(s, ev));
            Assert.IsFalse(PlagueApi.Active(s));
        }

        [Test]
        public void ManyRoundsWithEpidemics_EventuallyFalls()
        {
            var s = NewSim(11).State;
            int rounds = 0;
            while (!s.Plague.IslandFell && rounds < 500) { PlagueRules.InfectRound(s, null); PlagueRules.Epidemic(s, null); rounds++; }
            Assert.IsTrue(s.Plague.IslandFell, "no cures: the island must fall");
            Assert.GreaterOrEqual(s.Plague.Outbreaks, 8);
        }

        // ---- queries

        [Test]
        public void HexBlocked_WhileAnyNodeOnTheHexHoldsADemon()
        {
            var s = NewSim().State;
            var topo = s.Board.Topology;
            int hex = topo.HexIndex(new HexCoord(-1, -1));
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, PlagueApi.NodesOfHex(s, hex));
            Assert.IsFalse(PlagueApi.HexBlocked(s, hex));
            Fill(s.Plague, 4, PlagueApi.ElementOf(4), 1);
            Assert.IsTrue(PlagueApi.HexBlocked(s, hex));
            Assert.IsFalse(PlagueApi.HexBlocked(s, topo.HexIndex(new HexCoord(3, -3))), "another hex is still clean");
            Assert.IsFalse(PlagueApi.HexBlocked(s, -1));
        }

        [Test]
        public void CanWalkThrough_FalseAtThree()
        {
            var s = NewSim().State;
            Assert.IsTrue(PlagueApi.CanWalkThrough(s, 5));
            Fill(s.Plague, 5, 0, 2);
            Assert.IsTrue(PlagueApi.CanWalkThrough(s, 5));
            Fill(s.Plague, 5, 0, 3);
            Assert.IsFalse(PlagueApi.CanWalkThrough(s, 5));
        }

        [Test]
        public void Neighbours_AreSymmetric()
        {
            for (int n = 1; n <= 48; n++) foreach (int m in PlagueApi.Neighbours(n)) CollectionAssert.Contains(PlagueApi.Neighbours(m), n);
        }

        // ---- banish

        [Test]
        public void Banish_RemovesOne_ThenCooldown()
        {
            var sim = NewSim();
            var s = sim.State;
            Fill(s.Plague, 9, PlagueApi.ElementOf(9), 2);
            Fill(s.Plague, 10, PlagueApi.ElementOf(10), 1);
            var r = sim.Submit(new Banish { PlayerId = PlayerIds.Human, Node = 9 });
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            var evs = sim.DrainEvents();
            Assert.AreEqual(1, PlagueApi.DemonsAt(s, 9));
            Assert.AreEqual(1, evs.OfType<Banished>().Count());
            var again = sim.Check(new Banish { PlayerId = PlayerIds.Human, Node = 10 });
            Assert.IsFalse(again.Ok);
            Assert.AreEqual(PlagueRefusals.BanishCooling, again.Reason);
            Assert.AreEqual(300, PlagueRules.BanishCooldownTicks);
            Assert.AreEqual(300, PlagueApi.BanishCooldownLeft(s));
            sim.Step(299);
            Assert.IsFalse(sim.Check(new Banish { PlayerId = PlayerIds.Human, Node = 10 }).Ok);
            sim.Step();
            Assert.IsTrue(sim.Check(new Banish { PlayerId = PlayerIds.Human, Node = 10 }).Ok);
        }

        [Test]
        public void Banish_Refusals_ArePlainSentences()
        {
            var sim = NewSim();
            var none = sim.Check(new Banish { Node = 3 });
            Assert.AreEqual(PlagueRefusals.NoDemons, none.Reason);
            Assert.AreEqual("There are no demons here.", none.Message);
            Assert.AreEqual(PlagueRefusals.UnknownNode, sim.Check(new Banish { Node = 99 }).Reason);
            PlagueRules.End(sim.State, null);
            Assert.AreEqual(PlagueRefusals.PlagueOver, sim.Check(new Banish { Node = 3 }).Reason);
        }

        // ---- cure, eradicate, end

        [Test]
        public void Cure_ThenBanishLastDemon_Eradicates()
        {
            var s = NewSim().State;
            var ev = new EventBuffer();
            Fill(s.Plague, 2, 0, 1);
            Assert.IsTrue(PlagueRules.Cure(s, 0, ev));
            Assert.IsFalse(PlagueRules.Cure(s, 0, ev), "once");
            Assert.AreEqual(1, Evs(ev).OfType<Cured>().Count());
            Assert.IsFalse(PlagueApi.IsEradicated(s, 0), "one demon is left");
            Assert.IsTrue(PlagueRules.BanishAt(s, 2, -1, ev));
            Assert.IsTrue(PlagueApi.IsEradicated(s, 0));
            Assert.AreEqual(1, Evs(ev).OfType<Eradicated>().Count());
        }

        [Test]
        public void Cure_WithNoDemons_EradicatesAtOnce()
        {
            var s = NewSim().State;
            PlagueRules.Cure(s, 3, null);
            Assert.IsTrue(PlagueApi.IsEradicated(s, 3));
        }

        [Test]
        public void Strengthen_NotForCured_CureClearsIt()
        {
            var s = NewSim().State;
            Assert.IsTrue(PlagueRules.Strengthen(s, 1, null));
            Assert.IsTrue(PlagueApi.IsStrengthened(s, 1));
            Assert.IsTrue(PlagueRules.Cure(s, 1, null));
            Assert.IsFalse(PlagueApi.IsStrengthened(s, 1));
            Assert.IsFalse(PlagueRules.Strengthen(s, 1, null));
        }

        [Test]
        public void End_ClearsAndStops()
        {
            var s = NewSim().State;
            Fill(s.Plague, 1, 0, 3);
            Fill(s.Plague, 20, 1, 2);
            var ev = new EventBuffer();
            Assert.IsTrue(PlagueRules.End(s, ev));
            Assert.AreEqual(5, Evs(ev).OfType<PlagueEnded>().Single().Cleared);
            Assert.AreEqual(0, PlagueApi.TotalDemons(s));
            Assert.IsFalse(PlagueRules.InfectRound(s, ev));
            Assert.IsFalse(PlagueRules.Epidemic(s, ev));
            Assert.IsFalse(PlagueRules.Cure(s, 0, ev));
            Assert.IsFalse(PlagueRules.End(s, ev), "idempotent");
        }

        [Test]
        public void State_RoundTripsThroughJson()
        {
            var s = NewSim().State;
            for (int i = 0; i < 8; i++) PlagueRules.InfectRound(s, null);
            PlagueRules.Epidemic(s, null);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(s.Plague);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<PlagueState>(json);
            Assert.AreEqual(json, Newtonsoft.Json.JsonConvert.SerializeObject(back));
        }
    }
}
