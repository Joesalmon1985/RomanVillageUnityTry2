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
        /// <summary>Result of a long random-command run.</summary>
        internal sealed class RandomRun
        {
            public ulong Seed;
            public int Accepted, Refused, Ticks;
            public System.Collections.Generic.SortedDictionary<string, int> Kinds = new System.Collections.Generic.SortedDictionary<string, int>(System.StringComparer.Ordinal);
            public WorldState State;
        }

        /// <summary>
        /// A long game driven by the commission script plus random valid and invalid commands of every module (and
        /// random gifts of wares and coin). All invariants are checked every tick; the run fails on the first error.
        /// </summary>
        internal static RandomRun Run(ulong seed, ulong commandSeed, int ticks)
        {
            var sim = K.NewSim(seed, ageAtOnce: true, ticksPerEdge: 20);
            var s = sim.State;
            // A long game: no ending by rounds, points or breaches, so commands keep being exercised.
            s.Strategy.Tuning.RoundsPerAge = 400;
            s.Strategy.Tuning.VictoryPoints = 999;
            s.Strategy.Tuning.BreachLimit = int.MaxValue;
            // Demons keep coming (draws, outbreaks, an epidemic or two) but slowly enough that random hero moves
            // and squads keep the village usable.
            s.Strategy.Tuning.InfectionPerAge = 0;
            s.Strategy.Tuning.DemonCap = 5;
            var rng = new Rng(commandSeed);
            var script = new CommissionScript(sim);
            var run = new RandomRun { Seed = seed, Ticks = ticks, State = s };
            var kinds = run.Kinds;

            for (int t = 0; t < ticks; t++)
            {
                if (StrategyApi.DraftPending(s) && rng.Chance(1, 3))
                {
                    var pick = sim.Submit(new DraftPick { CardId = s.Strategy.Sides[0].DraftHand[rng.NextInt(s.Strategy.Sides[0].DraftHand.Count)] });
                    string pk = "DraftPick:" + (pick.Ok ? "ok" : pick.Reason);
                    kinds[pk] = kinds.TryGetValue(pk, out int pn) ? pn + 1 : 1;
                }
                script.Act();
                if (t % 150 == 0) K.Give(s, (Ware)rng.NextInt(Wares.Count), 1 + rng.NextInt(4), Ware.Planks, 3, Ware.Bricks, 2);
                if (t % 500 == 0) s.Player(0).Coin += 3;
                // A squad sweep every 20 s keeps the village working: random hero moves alone would leave every building
                // under demons for good and production (which this run must exercise) would stop.
                if (t % 200 == 199)
                    for (int v = 0; v < s.Strategy.Demons.Length; v++)
                        if (s.Strategy.Demons[v] > 0) StrategyApi.ClearDemonsBySquad(s, v, new EventBuffer());
                if (rng.Chance(1, 4))
                {
                    var c = RandomCommand(s, rng);
                    var r = sim.Submit(c);
                    if (r.Ok) run.Accepted++;
                    else run.Refused++;
                    string k = c.GetType().Name + (r.Ok ? ":ok" : ":" + r.Reason);
                    kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
                }
                sim.Step();
                sim.DrainEvents();
                var err = K.CheckInvariants(s);
                if (err != null) Assert.Fail("seed " + seed + " tick " + s.Tick + ": " + err);
            }
            return run;
        }

        [Test]
        public void Conservation_20000Ticks_RandomCommandsIncludingStrategyAndMilitary()
        {
            var run = Run(4242, 777, 20000);
            var s = run.State;
            TestContext.WriteLine("accepted " + run.Accepted + ", refused " + run.Refused + "; breaches " + s.Strategy.Breaches);
            foreach (var kv in run.Kinds) TestContext.WriteLine(kv.Key + " " + kv.Value);
            Assert.Greater(run.Accepted, 300);
            Assert.Greater(run.Refused, 300);
            foreach (var name in new[] { "FoundCity:ok", "Trade:ok", "MoveHero:ok", "DraftPick:ok", "BuildCard:ok", "DiscardCard:ok" })
                Assert.IsTrue(run.Kinds.ContainsKey(name), name + " was exercised");
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
                    // Prefer flags that are legal; else often lay the next road segment towards a city corner
                    // two hex edges out (on the fine map city corners lie beyond the home territory).
                    var legal = StrategyApi.LegalCitySites(s, true);
                    if (legal.Count > 0 && rng.Chance(1, 2)) return new FoundCity { Vertex = legal[rng.NextInt(legal.Count)] };
                    if (rng.Chance(1, 2))
                    {
                        int target = topo.Corners.Where(c => StrategyApi.CheckCity(s, c).Reason == StrategyRefusals.NoRoad
                            && EconomyTestKit.PathToNetwork(s, c)?.Length <= 8).DefaultIfEmpty(-1).First();
                        var road = target >= 0 ? EconomyTestKit.RoadsToConnect(s, target) : null;
                        if (road != null && road.Count > 0) return new PlaceRoad { Edge = road[road.Count - 1] };
                    }
                    return new FoundCity { Vertex = v };
                }
                case 1: return new Trade { Give = (Ware)rng.NextInt(Wares.Count), Get = (Ware)rng.NextInt(Wares.Count), Count = rng.NextInt(3) };
                case 2: return new Sell { Ware = (Ware)rng.NextInt(Wares.Count), Count = rng.NextInt(5) };
                case 3: return new Buy { Ware = (Ware)rng.NextInt(Wares.Count), Count = rng.NextInt(3) };
                case 4:
                case 5:
                {
                    var n = topo.CornerNeighbours(s.Strategy.HeroVertex);
                    return new MoveHero { Vertex = rng.Chance(4, 5) ? n[rng.NextInt(n.Count)] : v };
                }
                case 6:
                    return new BuildCard { CardId = handCard, CityVertex = cities.Count > 0 && rng.Chance(4, 5) ? cities[rng.NextInt(cities.Count)] : v };
                case 7: return new DiscardCard { CardId = handCard };
                case 8: return new BuildWonderStage { PlayerId = rng.Chance(1, 8) ? PlayerIds.Rival : PlayerIds.Human };
                case 9: return new PlaceBuilding { Type = (BuildingType)rng.NextInt(BuildingDefs.Count), Vertex = v };
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
