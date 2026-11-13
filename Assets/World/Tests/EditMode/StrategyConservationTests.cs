using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;
using K = World.Sim.Tests.StrategyTestKit;

namespace World.Sim.Tests
{
    /// <summary>Ware conservation and all invariants over 20,000 ticks of random valid and invalid commands (economy, strategy, military).</summary>
    public class StrategyConservationTests
    {
        [Test]
        public void Conservation_20000Ticks_RandomCommandsIncludingStrategyAndMilitary()
        {
            var sim = K.NewSim(4242, ageAtOnce: true, ticksPerEdge: 20);
            var s = sim.State;
            var topo = s.Board.Topology;
            // A long game: no ending by rounds, points or breaches, so commands keep being exercised.
            s.Strategy.Tuning.RoundsPerAge = 40;
            s.Strategy.Tuning.VictoryPoints = 999;
            s.Strategy.Tuning.BreachLimit = 999;
            var rng = new Rng(777);
            var script = new CommissionScript(sim);
            int accepted = 0, refused = 0;
            var kinds = new System.Collections.Generic.Dictionary<string, int>();

            for (int t = 0; t < 20000; t++)
            {
                if (StrategyApi.DraftPending(s) && rng.Chance(1, 3))
                {
                    var pick = sim.Submit(new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[rng.NextInt(s.Strategy.Sides[0].DraftHand.Count)] });
                    string pk = "DraftPick:" + (pick.Ok ? "ok" : pick.Reason);
                    kinds[pk] = kinds.TryGetValue(pk, out int pn) ? pn + 1 : 1;
                }
                script.Act();
                if (t % 150 == 0) K.Give(s, (Ware)rng.NextInt(Wares.Count), 1 + rng.NextInt(4), Ware.Planks, 3, Ware.Stone, 2);
                if (t % 500 == 0) s.Player(0).Coin += 3;
                if (rng.Chance(1, 4))
                {
                    var c = RandomCommand(s, rng);
                    var r = sim.Submit(c);
                    if (r.Ok) accepted++;
                    else refused++;
                    string k = c.GetType().Name + (r.Ok ? ":ok" : ":" + r.Reason);
                    kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
                }
                sim.Step();
                sim.DrainEvents();
                var err = K.CheckInvariants(s);
                if (err != null) Assert.Fail("tick " + s.Tick + ": " + err);
            }
            TestContext.WriteLine("accepted " + accepted + ", refused " + refused);
            foreach (var kv in kinds.OrderBy(k => k.Key)) TestContext.WriteLine(kv.Key + " " + kv.Value);
            Assert.Greater(accepted, 300);
            Assert.Greater(refused, 300);
            foreach (var name in new[] { "FoundCity:ok", "Trade:ok", "MoveHero:ok", "DraftPick:ok", "BuildCard:ok", "DiscardCard:ok" })
                Assert.IsTrue(kinds.ContainsKey(name), name + " was exercised");
            Assert.Greater(s.Economy.Round, 10);
        }

        private static Command RandomCommand(WorldState s, Rng rng)
        {
            var topo = s.Board.Topology;
            int v = rng.NextInt(topo.VertexCount + 2) - 1;
            var hand = s.Strategy.Sides[0].Hand;
            int anyCard = rng.NextInt(s.Strategy.Cards.Count + 2) - 1;
            int handCard = hand.Count > 0 && rng.Chance(3, 4) ? hand[rng.NextInt(hand.Count)] : anyCard;
            var cities = StrategyApi.PlayerCities(s);
            int squads = s.Military.Squads.Count;
            switch (rng.NextInt(16))
            {
                case 0:
                {
                    // Prefer flags that only lack a road or are legal.
                    var legal = StrategyApi.LegalCitySites(s, true);
                    if (legal.Count > 0 && rng.Chance(1, 2)) return new FoundCity { Vertex = legal[rng.NextInt(legal.Count)] };
                    return new FoundCity { Vertex = v };
                }
                case 1: return new Trade { Give = (Ware)rng.NextInt(Wares.Count), Get = (Ware)rng.NextInt(Wares.Count), Count = rng.NextInt(3) };
                case 2: return new Sell { Ware = (Ware)rng.NextInt(Wares.Count), Count = rng.NextInt(5) };
                case 3: return new Buy { Ware = (Ware)rng.NextInt(Wares.Count), Count = rng.NextInt(3) };
                case 4:
                case 5:
                {
                    var n = topo.VertexNeighbours(s.Strategy.HeroVertex);
                    return new MoveHero { Vertex = rng.Chance(4, 5) ? n[rng.NextInt(n.Count)] : v };
                }
                case 6:
                    return new BuildCard { CardId = handCard, CityVertex = cities.Count > 0 && rng.Chance(4, 5) ? cities[rng.NextInt(cities.Count)] : v };
                case 7: return new DiscardCard { CardId = handCard };
                case 8: return new BuildWonderStage { PlayerId = rng.Chance(1, 8) ? PlayerIds.Rival : PlayerIds.Human };
                case 9: return new PlaceBuilding { Type = (BuildingType)rng.NextInt(14), Vertex = v };
                case 10: return new PlaceRoad { Edge = rng.NextInt(topo.EdgeCount + 2) - 1 };
                case 11: return new QueueUnit { BarracksId = rng.NextInt(s.Economy.Buildings.Count + 1), Count = rng.NextInt(3) };
                case 12: return new OrderSquad { SquadId = rng.NextInt(squads + 1), Order = (OrderKind)rng.NextInt(4), Target = v };
                case 13: return new CancelOrder { SquadId = rng.NextInt(squads + 1) };
                case 14: return new DraftPick { CardId = anyCard };
                default: return new EndRound { PlayerId = rng.Chance(1, 8) ? PlayerIds.Rival : PlayerIds.Human };
            }
        }
    }
}
