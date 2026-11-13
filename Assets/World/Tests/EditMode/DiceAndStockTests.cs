using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Dice, hex stock, growth, cap and collecting (REFACTOR_SPEC 2.A).</summary>
    public class DiceAndStockTests
    {
        private static WorldSim NewSim(ulong seed = K.DefaultSeed) => WorldSimFactory.CreateNew(seed);

        private static int HexOf(WorldState s, int corner, Terrain t) =>
            s.Board.Topology.VertexHexes(corner).First(h => s.Board.HexTerrain[h] == t);

        [Test]
        public void FirstRollAtTick100_ThenEvery100()
        {
            var sim = NewSim();
            Assert.AreEqual(36, sim.State.Economy.Dice.Deck.Length);
            Assert.AreEqual(-1, EconomyApi.LastRoll(sim.State).RollIndex);
            Assert.AreEqual(100, EconomyApi.TicksToNextRoll(sim.State));
            var ev = K.Step(sim, 99);
            Assert.IsEmpty(ev.OfType<DiceRolled>());
            ev = K.Step(sim, 1);
            var roll = ev.OfType<DiceRolled>().Single();
            Assert.AreEqual(100, roll.Tick);
            Assert.AreEqual(0, roll.RollIndex);
            Assert.AreEqual(roll.D1 + roll.D2, roll.Sum);
            var view = EconomyApi.LastRoll(sim.State);
            Assert.AreEqual(roll.Sum, view.Sum);
            Assert.AreEqual(100, view.Tick);
            Assert.AreEqual(100, EconomyApi.TicksToNextRoll(sim.State));
            ev = K.Step(sim, 300);
            CollectionAssert.AreEqual(new long[] { 200, 300, 400 }, ev.OfType<DiceRolled>().Select(d => d.Tick).ToArray());
        }

        [Test]
        public void DeckOf36_EveryOutcomeOncePerDeck()
        {
            var sim = NewSim(5);
            var ev = K.Step(sim, 100 * 72);
            var rolls = ev.OfType<DiceRolled>().ToList();
            Assert.AreEqual(72, rolls.Count);
            for (int deck = 0; deck < 2; deck++)
            {
                var outcomes = rolls.Skip(deck * 36).Take(36).Select(r => r.D1 * 10 + r.D2).OrderBy(x => x).ToList();
                var expected = new List<int>();
                for (int a = 1; a <= 6; a++) for (int b = 1; b <= 6; b++) expected.Add(a * 10 + b);
                CollectionAssert.AreEqual(expected, outcomes, "deck " + deck);
            }
        }

        [Test]
        public void Pips()
        {
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 2, 3, 4, 5, 0, 5, 4, 3, 2, 1, 0 },
                Enumerable.Range(0, 14).Select(EconomyApi.Pips).ToArray());
        }

        [Test]
        public void StartingStock_FourMatureNextToEachStartingCamp()
        {
            var s = NewSim().State;
            var t = s.Board.Topology;
            var start = new HashSet<int>(s.Board.StartCorners.SelectMany(c => t.VertexHexes(c)));
            for (int h = 0; h < t.HexCount; h++)
            {
                var v = EconomyApi.HexStock(s, h);
                Assert.AreEqual(start.Contains(h) ? 4 : 0, v.Mature, "hex " + h);
                Assert.AreEqual(0, v.Young);
                Assert.AreEqual(12, v.Cap);
            }
            CollectionAssert.AreEqual(new[] { 4, 2, 2, 2, 0, 0, 0, 0, 0, 0 }, s.Economy.Settlements[0].Stock);
            Assert.AreEqual(3, s.Economy.Carriers.Count(c => c.Home == 0));
            Assert.AreEqual(3, s.Economy.Buildings.Count(b => b.Settlement == 0));
            Assert.AreEqual(630, s.Economy.Buildings.Single(b => b.Settlement == 0 && b.Type == BuildingType.LogPile).Plot);
        }

        [Test]
        public void RollAddsYoungStock_GrowsInThreeStages_Matures()
        {
            var sim = NewSim();
            var s = sim.State;
            int forest = HexOf(s, s.Board.StartCorners[0], Terrain.Forest); // number 6
            K.RigNextRoll(s, 2, 4);
            var ev = K.Step(sim, 1);
            var added = ev.OfType<StockAdded>().Where(a => a.Hex == forest).Single();
            Assert.AreEqual(4, added.Count);
            Assert.AreEqual(Ware.Wood, added.Ware);
            Assert.AreEqual(s.Tick + 750, added.MatureTick);
            Assert.IsTrue(ev.OfType<StockAdded>().Any(a => a.Hex == HexOf(s, s.Board.StartCorners[1], Terrain.Forest)), "the rival's forest 6 too");
            var v = EconomyApi.HexStock(s, forest);
            Assert.AreEqual(4, v.Young);
            Assert.AreEqual(4, v.Stage0);
            Assert.IsTrue(v.YoungVisible);
            Assert.AreEqual(s.Tick + 750, v.NextMatureTick);
            K.Step(sim, 250);
            Assert.AreEqual(4, EconomyApi.HexStock(s, forest).Stage1);
            K.Step(sim, 250);
            Assert.AreEqual(4, EconomyApi.HexStock(s, forest).Stage2);
            ev = K.Step(sim, 250);
            Assert.AreEqual(4, ev.OfType<StockMatured>().Where(m => m.Hex == forest).Sum(m => m.Count));
            Assert.IsFalse(s.Economy.Hexes[forest].Young.Any(c => c.AddedTick == added.Tick), "the rigged batch is mature (later rolls may add more)");
            Assert.IsFalse(EconomyApi.HexStock(s, HexOf(s, s.Board.StartCorners[0], Terrain.Hills)).YoungVisible, "hills are mined");
        }

        [Test]
        public void Cap_ExcessIsLost()
        {
            var sim = NewSim();
            var s = sim.State;
            int fields = HexOf(s, s.Board.StartCorners[0], Terrain.Fields); // number 5
            s.Economy.Hexes[fields].Mature = 10;
            s.Economy.Ledger.StockAdded[(int)Ware.Grain] += 6; // keep the ledger whole for the test edit
            K.RigNextRoll(s, 1, 4);
            var ev = K.Step(sim, 1);
            Assert.AreEqual(2, ev.OfType<StockAdded>().Single(a => a.Hex == fields).Count);
            Assert.AreEqual(2, ev.OfType<StockLost>().Single(a => a.Hex == fields).Count);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void NoAdjacentCamp_OrDemonHeld_NoStock_AndSevenAddsNothing()
        {
            var sim = NewSim();
            var s = sim.State;
            var t = s.Board.Topology;
            int lonely = Enumerable.Range(0, t.HexCount).First(h => s.Board.HexNumber[h] == 9
                && t.HexCorners(h).All(c => EconomyApi.SettlementAt(s, c) == null));
            K.RigNextRoll(s, 4, 5);
            Assert.IsFalse(K.Step(sim, 1).OfType<StockAdded>().Any(a => a.Hex == lonely));

            s.Demons.Corner[s.Board.StartCorners[0]] = 1;
            int homeForest = HexOf(s, s.Board.StartCorners[0], Terrain.Forest);
            Assert.IsTrue(EconomyApi.HexStock(s, homeForest).Stopped);
            K.RigNextRoll(s, 3, 3);
            var ev = K.Step(sim, 1);
            Assert.IsFalse(ev.OfType<StockAdded>().Any(a => a.Hex == homeForest), "demon-held: no stock");
            Assert.IsTrue(ev.OfType<StockAdded>().Any(a => a.Hex == HexOf(s, s.Board.StartCorners[1], Terrain.Forest)));

            K.RigNextRoll(s, 3, 4);
            ev = K.Step(sim, 1);
            Assert.AreEqual(7, ev.OfType<DiceRolled>().Single().Sum);
            Assert.IsEmpty(ev.OfType<StockAdded>());
        }

        [Test]
        public void Collectors_TakeMatureUnits_YieldIntoTheStorehouse()
        {
            var sim = NewSim();
            var s = sim.State;
            int forest = HexOf(s, s.Board.StartCorners[0], Terrain.Forest);
            var logPile = s.Economy.Buildings.Single(b => b.Settlement == 0 && b.Type == BuildingType.LogPile);
            var ev = K.Step(sim, 1);
            Assert.IsTrue(ev.OfType<CollectStarted>().Any(c => c.BuildingId == logPile.Id && c.Hex == forest));
            Assert.AreEqual(3, s.Economy.Hexes[forest].Mature);
            ev = K.Step(sim, 99);
            var col = ev.OfType<Collected>().Single(c => c.BuildingId == logPile.Id);
            Assert.AreEqual(1, col.Amount);
            Assert.AreEqual(Ware.Wood, col.Ware);
            K.Step(sim, 400);
            Assert.AreEqual(0, s.Economy.Hexes[forest].Mature, "four units taken");
            Assert.AreEqual(Reasons.NoBatch, logPile.Reason);
            Assert.AreEqual("Log pile is waiting for forest stock to grow.", EconomyApi.DescribeBuilding(s, logPile.Id).Status);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void HexStockAndWares_Conserved_Over3000Ticks()
        {
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var sim = NewSim(seed);
                for (int i = 0; i < 30; i++)
                {
                    sim.Step(100);
                    sim.DrainEvents();
                    Assert.IsNull(K.CheckInvariants(sim.State), "seed " + seed + " tick " + sim.State.Tick);
                }
            }
        }
    }
}
