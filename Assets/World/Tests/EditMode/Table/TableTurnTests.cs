using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Table;
using static World.Sim.Tests.TableTestKit;

namespace World.Sim.Tests
{
    /// <summary>TU-1, TU-2, TU-10, HG-1..HG-6, GB-4: the roll, production, sevens and discards, the hourglass, leaving.</summary>
    public class TableTurnTests
    {
        /// <summary>A resource hex beside the player's first camp, and its number.</summary>
        private static (int hex, int number) HomeHex(WorldState s)
        {
            foreach (int h in s.Board.Topology.VertexHexes(StartCorners[0]))
                if (TerrainInfo.RawWare(s.Board.HexTerrain[h]).HasValue && s.Board.HexNumber[h] > 0) return (h, s.Board.HexNumber[h]);
            Assert.Fail("home has no resource hex");
            return (-1, 0);
        }

        private static int[] ExpectedGains(WorldState s, int roll, Func<int, bool> blocked)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            var gains = new int[t.PeopleCount * 5];
            for (int c = 0; c < topo.CornerCount; c++)
            {
                if (t.CornerOwner[c] < 0) continue;
                foreach (int h in topo.VertexHexes(c))
                {
                    var w = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                    if (s.Board.HexNumber[h] != roll || !w.HasValue || blocked(h)) continue;
                    gains[t.CornerOwner[c] * 5 + (int)w.Value] += t.CornerLevel[c];
                }
            }
            return gains;
        }

        [Test]
        public void Production_SettlementOne_CityTwo_NoRollInEvent()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            var (hex, number) = HomeHex(s);
            t.CornerLevel[StartCorners[0]] = 2; // the first camp is a city now
            ClearHands(t);
            ForceNextRoll(t, number);
            var evs = Do(sim, new EndTurn());
            var prod = Single<Produced>(evs);
            Assert.Contains(hex, prod.Hexes);
            var expected = ExpectedGains(s, number, h => false);
            CollectionAssert.AreEqual(expected, prod.Gains);
            var good = (int)TerrainInfo.RawWare(s.Board.HexTerrain[hex]).Value;
            Assert.GreaterOrEqual(t.Peoples[0].Hand[good], 2, "a city takes 2 cards");
            for (int p = 0; p < 4; p++)
                for (int g = 0; g < 5; g++) Assert.AreEqual(expected[p * 5 + g], t.Peoples[p].Hand[g]);
            foreach (int h in prod.Hexes) Assert.AreNotEqual(Terrain.Meadow, s.Board.HexTerrain[h], "Meadow never produces");
            Assert.AreEqual(number, t.LastRoll, "the roll stays in state for F1");
        }

        [Test]
        public void Production_SettlementTakesOneCard()
        {
            var sim = ToHumanTurn();
            var s = sim.State;
            var t = T(sim);
            var (hex, number) = HomeHex(s);
            ClearHands(t);
            ForceNextRoll(t, number);
            var prod = Single<Produced>(Do(sim, new EndTurn()));
            int good = (int)TerrainInfo.RawWare(s.Board.HexTerrain[hex]).Value;
            Assert.AreEqual(ExpectedGains(s, number, h => false)[good], prod.Gains[good]);
            Assert.GreaterOrEqual(prod.Gains[good], 1);
        }

        [Test]
        public void Production_BlockedHexProducesNothing()
        {
            int blockedHex = -1;
            var sim = ToHumanTurn(wire: seams => { IdleAi(seams); seams.HexBlocked = (st, h) => h == blockedHex; });
            var s = sim.State;
            var t = T(sim);
            var (hex, number) = HomeHex(s);
            blockedHex = hex;
            ClearHands(t);
            ForceNextRoll(t, number);
            var prod = Single<Produced>(Do(sim, new EndTurn()));
            Assert.Contains(hex, prod.Blocked);
            Assert.IsFalse(prod.Hexes.Contains(hex));
            CollectionAssert.AreEqual(ExpectedGains(s, number, h => h == hex), prod.Gains);
        }

        [Test]
        public void MeadowAndUnnumberedHexes_ScoreNoPips()
        {
            Assert.AreEqual(0, TableRules.Pips(0));
            Assert.AreEqual(0, TableRules.Pips(7));
            Assert.AreEqual(5, TableRules.Pips(6));
            Assert.AreEqual(1, TableRules.Pips(12));
        }

        [Test]
        public void Seven_NoProduction_EpidemicEvent()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            ClearHands(t);
            SetHand(t, 0, 2, 2, 2, 1, 0); // 7 cards: not over the limit
            ForceNextRoll(t, 7);
            var evs = Do(sim, new EndTurn());
            Assert.AreEqual(0, Count<Produced>(evs));
            Assert.AreEqual(0, Count<DiscardRequired>(evs));
            var ep = Single<Epidemic>(evs);
            Assert.AreEqual(t.Current, ep.People);
            Assert.AreEqual(TablePhase.Main, t.Phase);
            Assert.AreEqual(7, TableRules.HandSize(t.Peoples[0].Hand));
        }

        [Test]
        public void NoPlayerFacingEventCarriesTheRoll()
        {
            var types = typeof(TableModule).Assembly.GetTypes().Where(x => x.Namespace == "World.Sim.Table" && typeof(SimEvent).IsAssignableFrom(x));
            Assert.Greater(types.Count(), 10);
            foreach (var type in types)
                foreach (var f in type.GetFields())
                    Assert.IsFalse(f.Name.Contains("Roll") || f.Name == "Number" || f.Name == "Dice" || f.Name == "Total", type.Name + "." + f.Name);
        }

        [Test]
        public void Seven_DiscardHalf_AiAtOnce_PlayerChooses()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            ClearHands(t);
            SetHand(t, 0, 3, 3, 2, 0, 0); // 8 -> owes 4
            SetHand(t, 1, 9, 0, 0, 0, 0); // 9 -> owes 4
            SetHand(t, 2, 2, 2, 2, 1, 0); // 7 -> owes nothing
            ForceNextRoll(t, 7);
            var evs = Do(sim, new EndTurn());
            CollectionAssert.AreEqual(new[] { 4, 4, 0, 0 }, Single<DiscardRequired>(evs).Owed);
            var ai = Single<Discarded>(evs);
            Assert.AreEqual((1, true), (ai.People, ai.Auto));
            CollectionAssert.AreEqual(Cards(5), t.Peoples[1].Hand);
            Assert.AreEqual(0, Count<Epidemic>(evs), "the epidemic waits for the discards");
            Assert.AreEqual(TablePhase.Discard, t.Phase);
            Refused(sim, new EndTurn(), TableRefusals.NotYourTurn, t.Current);
            Refused(sim, new DiscardCards { Cards = Cards(1, 1, 1) }, TableRefusals.BadTrade);
            Refused(sim, new DiscardCards { Cards = Cards(0, 0, 0, 4) }, TableRefusals.CannotAfford);
            Refused(sim, new DiscardCards { Cards = Cards(4) }, TableRefusals.NotYourTurn, 2);
            evs = Do(sim, new DiscardCards { Cards = Cards(1, 1, 2) });
            Assert.IsFalse(Single<Discarded>(evs).Auto);
            Single<Epidemic>(evs);
            CollectionAssert.AreEqual(Cards(2, 2), t.Peoples[0].Hand);
            Assert.AreEqual(TablePhase.Main, t.Phase);
            CollectionAssert.AreEqual(Cards(2, 2, 2, 1), t.Peoples[2].Hand);
        }

        [Test]
        public void Seven_PlayerTimesOut_DefaultChooserDiscards()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            ClearHands(t);
            SetHand(t, 0, 5, 1, 1, 1, 1); // 9 -> owes 4
            ForceNextRoll(t, 7);
            Do(sim, new EndTurn());
            Assert.AreEqual(TablePhase.Discard, t.Phase);
            var evs = Step(sim, t.Tuning.DiscardTicks - 2);
            Assert.AreEqual(0, Count<Discarded>(evs));
            evs = Step(sim, 2);
            var d = Single<Discarded>(evs);
            Assert.IsTrue(d.Auto);
            CollectionAssert.AreEqual(Cards(4), d.Cards, "largest pile first");
            Single<Epidemic>(evs);
            Assert.AreEqual(TablePhase.Main, t.Phase);
        }

        [Test]
        public void Hourglass_BaseIdleRateRefillCap()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            var tu = t.Tuning;
            Assert.AreEqual(tu.TurnBaseTicks, t.Sand);
            Step(sim, tu.IdleAfterTicks);
            Assert.AreEqual(tu.TurnBaseTicks - tu.IdleAfterTicks, t.Sand, "1x while recently active");
            Step(sim, 10);
            Assert.AreEqual(tu.TurnBaseTicks - tu.IdleAfterTicks - 10 * tu.IdleRate, t.Sand, "3x when idle");
            int before = t.Sand;
            SetHand(t, 0, 8, 0, 0, 0, 0);
            Do(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay });
            Assert.AreEqual(before + tu.ActionRefillTicks - 1, t.Sand, "an action refills");
            t.Sand = tu.TurnCapTicks - 20;
            Do(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay });
            Assert.AreEqual(tu.TurnCapTicks - 1, t.Sand, "capped");
        }

        [Test]
        public void Hourglass_RunsOut_TurnEnds_TapEndsToo()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => t.Current != 0, 1000, log));
            Assert.AreEqual("sand", log.OfType<TurnEnded>().Single().Reason);
            int expectedTicks = t.Tuning.IdleAfterTicks + (int)Math.Ceiling((t.Tuning.TurnBaseTicks - t.Tuning.IdleAfterTicks) / (double)t.Tuning.IdleRate);
            Assert.LessOrEqual(log.Count(e => e is TurnEnded) , 1);

            var sim2 = ToHumanTurn();
            var evs = Do(sim2, new EndTurn());
            Assert.AreEqual("ended", Single<TurnEnded>(evs).Reason);
            Assert.AreNotEqual(0, T(sim2).Current);
            Refused(sim2, new EndTurn(), TableRefusals.NotYourTurn);
            Assert.Greater(expectedTicks, 0);
        }

        [Test]
        public void Hourglass_RunsOutAfterExpectedTicks()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            var tu = t.Tuning;
            int n = 0;
            while (t.Current == 0 && n < 2000) { sim.Step(); sim.DrainEvents(); n++; }
            int expected = tu.IdleAfterTicks + (tu.TurnBaseTicks - tu.IdleAfterTicks + tu.IdleRate - 1) / tu.IdleRate;
            Assert.AreEqual(expected, n);
        }

        [Test]
        public void AiTurn_TakesBaseTicksPlusTenPerAction()
        {
            int actions = 2;
            var sim = Create(humanSeat: -1, wire: seams => seams.AiTurn = (s, p, a) =>
            {
                s.Table.Peoples[p].Hand = Cards(8);
                for (int i = 0; i < actions; i++) Assert.IsTrue(a.BankTrade(Ware.Wood, Ware.Stone).Ok);
            });
            var t = T(sim);
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => log.OfType<TurnEnded>().Count(e => e.Reason == "ai") >= 2, 2000, log));
            var starts = log.OfType<TurnStarted>().Where(e => !e.Setup).ToList();
            var ends = log.OfType<TurnEnded>().Where(e => e.Reason == "ai").ToList();
            Assert.AreEqual(t.Tuning.AiTurnTicks + actions * t.Tuning.AiActionTicks, ends[0].Tick - starts[0].Tick);
            Assert.AreEqual(2, log.OfType<Traded>().Count(e => e.People == starts[0].People));
        }

        [Test]
        public void LeaveTable_FreezesEverything_SacrificeResumes()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            Step(sim, 5);
            var evs = Do(sim, new LeaveTable());
            Single<TablePaused>(evs);
            int sand = t.Sand, current = t.Current, turnTicks = t.TurnTicks;
            long active = t.ActiveTicks;
            Assert.AreEqual(0, Step(sim, 3000).Count);
            Assert.AreEqual((sand, current, turnTicks, active), (t.Sand, t.Current, t.TurnTicks, t.ActiveTicks));
            SetHand(t, 0, 8, 0, 0, 0, 0);
            Refused(sim, new BankTrade { Give = Ware.Wood, Get = Ware.Clay }, TableRefusals.TableClosed);
            Refused(sim, new EndTurn(), TableRefusals.TableClosed);
            Refused(sim, new LeaveTable(), TableRefusals.TableClosed);
            evs = Do(sim, new Sacrifice());
            Single<TableResumed>(evs);
            Assert.AreEqual(0, t.Current);
            Assert.AreEqual(sand - 1, t.Sand, "the sand runs again from where it stopped");
        }

        [Test]
        public void LeaveTable_DuringAiTurn_AiWaits()
        {
            var sim = ToHumanTurn();
            var t = T(sim);
            Do(sim, new EndTurn());
            int ai = t.Current;
            Assert.AreNotEqual(0, ai);
            Do(sim, new LeaveTable());
            Step(sim, 1000);
            Assert.AreEqual(ai, t.Current);
            Do(sim, new Sacrifice());
            Assert.IsTrue(StepUntil(sim, () => t.Current != ai, 200));
        }

        [Test]
        public void Rounds_CountUp_EveryPeoplePlaysOncePerRound()
        {
            var sim = Create(humanSeat: -1, wire: IdleAi);
            var t = T(sim);
            var log = new List<SimEvent>();
            Assert.IsTrue(StepUntil(sim, () => t.Round == 4, 5000, log));
            var turns = log.OfType<TurnStarted>().Where(e => !e.Setup).ToList();
            Assert.AreEqual(13, turns.Count, "twelve turns, then round 4 starts");
            for (int r = 0; r < 3; r++)
                CollectionAssert.AreEqual(t.TurnOrder, turns.Skip(4 * r).Take(4).Select(e => e.People).ToArray());
        }
    }
}
