using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Plague;
using World.Sim.War;

namespace World.Sim.Tests
{
    /// <summary>War module (PRD_GODBOARD PR-1..PR-4, WA-1..WA-18).</summary>
    public class WarTests
    {
        private static WorldState NewState(int peoples = 3, ulong seed = 7)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            PlagueModule.InitState(sim.State);
            WarModule.InitState(sim.State, peoples);
            return sim.State;
        }

        private static void Run(WorldState s, EventBuffer ev, int ticks, System.Func<WorldState, int, bool> linked = null)
        {
            for (int i = 0; i < ticks; i++) WarRules.Tick(s, ev, linked);
        }

        private static List<T> Of<T>(EventBuffer ev) where T : SimEvent => ev.Items.OfType<T>().ToList();

        private static int HexOfNode(WorldState s, int n) => s.Board.Topology.HexIndex(new HexCoord(PlagueGraphData.HexQ[n - 1], PlagueGraphData.HexR[n - 1]));

        /// <summary>A corner of the node's hex not in <paramref name="used"/>.</summary>
        private static int CornerOfNode(WorldState s, int node, params int[] used)
        {
            foreach (int c in s.Board.Topology.HexCorners(HexOfNode(s, node))) if (!used.Contains(c) && s.Board.Topology.IsCorner(c)) return c;
            Assert.Fail("no corner");
            return -1;
        }

        private static Soldier Place(WorldState s, int people, int node)
        {
            var x = WarRules.NewSoldier(s.War, people, -1, false, null);
            x.Node = node;
            return x;
        }

        private static void SetDemons(WorldState s, int node, int count)
        {
            int kind = PlagueApi.ElementOf(node);
            s.Plague.Demons[node - 1] = count;
            for (int k = 0; k < 4; k++) s.Plague.KindCount[(node - 1) * 4 + k] = 0;
            s.Plague.KindCount[(node - 1) * 4 + kind] = count;
        }

        private static int[] FindCornerWithGoods(WorldState s, int distinctRaws)
        {
            var topo = s.Board.Topology;
            foreach (int c in topo.Corners)
            {
                var raws = new HashSet<int>();
                foreach (int h in topo.VertexHexes(c)) { int r = WarData.RawOfTerrain(s.Board.TerrainAt(h)); if (r >= 0) raws.Add(r); }
                if (raws.Count == distinctRaws) return new[] { c };
            }
            return null;
        }

        // ---- processing

        [Test]
        public void Table_TenGoods_MatchThePrd()
        {
            Assert.AreEqual(10, WarData.GoodCount);
            var pairs = new HashSet<string>();
            for (int g = 0; g < 10; g++) pairs.Add(System.Math.Min(WarData.GoodRawA[g], WarData.GoodRawB[g]) + "-" + System.Math.Max(WarData.GoodRawA[g], WarData.GoodRawB[g]));
            Assert.AreEqual(10, pairs.Count);
            Assert.AreEqual(Stat.Attack, WarData.GoodStat[WarData.GoodOfPair(0, 1)]);   // Wood + Clay
            Assert.AreEqual(Stat.Defence, WarData.GoodStat[WarData.GoodOfPair(0, 3)]);  // Wood + Sheep
            Assert.AreEqual(Stat.Health, WarData.GoodStat[WarData.GoodOfPair(2, 4)]);   // Grain + Stone
            Assert.AreEqual(Stat.Evade, WarData.GoodStat[WarData.GoodOfPair(3, 1)]);    // Clay + Sheep
            Assert.AreEqual(3, WarData.GoodStat.Count(x => x == Stat.Attack));
            Assert.AreEqual(2, WarData.GoodStat.Count(x => x == Stat.Defence));
            Assert.AreEqual(3, WarData.GoodStat.Count(x => x == Stat.Health));
            Assert.AreEqual(2, WarData.GoodStat.Count(x => x == Stat.Evade));
            Assert.AreEqual(-1, WarData.GoodOfPair(1, 1));
        }

        [Test]
        public void Huts_OnePerPairOfDistinctRawGoods()
        {
            var s = NewState();
            foreach (int distinct in new[] { 1, 2, 3 })
            {
                var f = FindCornerWithGoods(s, distinct);
                if (f == null) continue;
                var huts = WarData.HutGoodsAt(s.Board, f[0]);
                Assert.AreEqual(distinct == 1 ? 0 : distinct == 2 ? 1 : 3, huts.Count, "distinct " + distinct);
                Assert.IsTrue(WarRules.AddSettlement(s, 0, f[0]));
                Assert.AreEqual(huts.Count, WarApi.SettlementAt(s, f[0]).Huts.Count);
                WarRules.RemoveSettlement(s, f[0]);
            }
            Assert.NotNull(FindCornerWithGoods(s, 3), "board has a three-good corner");
        }

        [Test]
        public void Huts_BuildIn300_ThenConvertOneOneToOneEvery200()
        {
            var s = NewState();
            int c = FindCornerWithGoods(s, 2)[0];
            WarRules.AddSettlement(s, 0, c);
            var st = WarApi.SettlementAt(s, c);
            var hut = st.Huts[0];
            var ev = new EventBuffer();
            WarRules.Deliver(s, c, WarData.GoodRawA[hut.Good], 2);
            Run(s, ev, 299);
            Assert.AreEqual(1, hut.BuildLeft);
            Run(s, ev, 1);
            Assert.AreEqual(1, Of<HutBuilt>(ev).Count);
            Assert.AreEqual(0, st.Proc[hut.Good]);
            WarRules.Deliver(s, c, WarData.GoodRawB[hut.Good], 1);
            Run(s, ev, 199);
            Assert.AreEqual(0, st.Proc.Sum());
            Run(s, ev, 1);
            Assert.AreEqual(1, st.Proc.Sum());
            Assert.AreEqual(1, Of<GoodProcessed>(ev).Count);
            // one input of the other kind is gone: the second cycle never completes
            Run(s, ev, 400);
            Assert.AreEqual(1, Of<GoodProcessed>(ev).Count);
        }

        [Test]
        public void Carrying_OnlyWhenLinked_HomeHandsOverAtOnce()
        {
            var s = NewState();
            int home = CornerOfNode(s, 1);
            int far = CornerOfNode(s, 20);
            WarRules.AddSettlement(s, 0, home, false, true);
            WarRules.AddSettlement(s, 0, far);
            var st = WarApi.SettlementAt(s, far);
            st.Proc[0] = 1;
            WarApi.SettlementAt(s, home).Proc[1] = 1;
            var ev = new EventBuffer();
            Run(s, ev, 5, (w, c) => false);
            Assert.AreEqual(1, st.Proc[0], "unlinked keeps its goods");
            Assert.AreEqual(0, WarApi.SettlementAt(s, home).Proc[1]);
            Assert.AreEqual(1, WarApi.LodgeOf(s, 0).Queue.Count);
            Run(s, ev, WarData.CarryTicks + 2, (w, c) => true);
            Assert.AreEqual(0, st.Proc[0]);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, WarApi.LodgeOf(s, 0).Queue);
        }

        // ---- lodge and soldiers

        [Test]
        public void Lodge_ThreeGoods_TrainOneSoldierAfter600_EachGoodRaisesItsStat()
        {
            var s = NewState();
            int home = CornerOfNode(s, 1);
            WarRules.AddSettlement(s, 0, home, false, true);
            var l = WarApi.LodgeOf(s, 0);
            l.Queue.AddRange(new[] { 0, 1, 2 });   // three Attack goods
            var ev = new EventBuffer();
            Run(s, ev, 599);
            Assert.AreEqual(0, Of<SoldierTrained>(ev).Count);
            Run(s, ev, 1);
            var t = Of<SoldierTrained>(ev).Single();
            Assert.AreEqual(SoldierLook.Raider, t.Look);
            var x = WarApi.SoldierById(s, t.Soldier);
            Assert.AreEqual(3, x.Levels[Stat.Attack]);
            Assert.AreEqual(WarApi.AttackOf(x), x.BaseAttack + 12);
            Assert.IsTrue(x.Field);
            Assert.AreEqual(2, WarApi.PresentAt(s, home, 0)); // base garrison + the new soldier
        }

        [Test]
        public void Lodge_LevelsCapAtThree_AndSecondBatchWaits()
        {
            var s = NewState();
            WarRules.AddSettlement(s, 0, CornerOfNode(s, 1), false, true);
            var l = WarApi.LodgeOf(s, 0);
            l.Queue.AddRange(new[] { 0, 0, 0, 5, 5, 5 });
            var ev = new EventBuffer();
            Run(s, ev, 600);
            Assert.AreEqual(1, Of<SoldierTrained>(ev).Count);
            Run(s, ev, 599);
            Assert.AreEqual(1, Of<SoldierTrained>(ev).Count, "next batch takes a full 600");
            Run(s, ev, 1);
            Assert.AreEqual(2, Of<SoldierTrained>(ev).Count);
            Assert.AreEqual(3, WarApi.SoldierById(s, Of<SoldierTrained>(ev)[1].Soldier).Levels[Stat.Health]);
        }

        [Test]
        public void Stats_FollowWa2_AndLooksFollowWa3()
        {
            var x = new Soldier { BaseAttack = 14, Levels = new[] { 0, 0, 0, 0 } };
            Assert.AreEqual(100, WarApi.MaxHealth(x));
            Assert.AreEqual(14, WarApi.AttackOf(x));
            Assert.AreEqual(0, WarApi.DefencePct(x));
            Assert.AreEqual(20, WarApi.EvadePct(x));
            Assert.AreEqual(SoldierLook.Warrior, WarApi.LookOf(x));
            x.Levels = new[] { 3, 1, 2, 1 };
            Assert.AreEqual(190, WarApi.MaxHealth(x));
            Assert.AreEqual(18, WarApi.AttackOf(x));
            Assert.AreEqual(16, WarApi.DefencePct(x));
            Assert.AreEqual(28, WarApi.EvadePct(x));
            Assert.AreEqual(SoldierLook.Brute, WarApi.LookOf(x));
            x.Levels = new[] { 0, 0, 2, 1 }; Assert.AreEqual(SoldierLook.Shieldbearer, WarApi.LookOf(x));
            x.Levels = new[] { 0, 0, 1, 2 }; Assert.AreEqual(SoldierLook.Skirmisher, WarApi.LookOf(x));
            x.Levels = new[] { 0, 2, 1, 0 }; Assert.AreEqual(SoldierLook.Raider, WarApi.LookOf(x));
            x.Levels = new[] { 2, 2, 1, 0 }; Assert.AreEqual(SoldierLook.Warrior, WarApi.LookOf(x));
        }

        [Test]
        public void Garrison_BaseSoldierPerSettlement_CityAddsOne()
        {
            var s = NewState();
            int a = CornerOfNode(s, 1), b = CornerOfNode(s, 20);
            WarRules.AddSettlement(s, 0, a);
            WarRules.AddSettlement(s, 1, b, true);
            Assert.AreEqual(1, WarApi.PresentAt(s, a, 0));
            Assert.AreEqual(2, WarApi.PresentAt(s, b, 1));
            Assert.IsTrue(WarRules.UpgradeCity(s, a));
            Assert.AreEqual(2, WarApi.PresentAt(s, a, 0));
            Assert.IsFalse(WarRules.UpgradeCity(s, a));
            Assert.IsFalse(WarRules.AddSettlement(s, 0, a), "occupied");
            Assert.IsFalse(WarRules.AddSettlement(s, 9, CornerOfNode(s, 5)), "no such people");
        }

        // ---- duels

        [Test]
        public void Duel_DamageFormula_EvadeAndDeterminism()
        {
            var rng = new Rng(1);
            var a = new Combatant(100, 14, 0, 0);
            var b = new Combatant(100, 12, 24, 100);
            Assert.AreEqual(0, DuelRules.Swing(a, b, rng, out bool hit)); Assert.IsFalse(hit, "100% evade");
            b.EvadePct = 0;
            Assert.AreEqual(14 * 76 / 100, DuelRules.Swing(a, b, rng, out hit)); Assert.IsTrue(hit);
            int w1 = DuelRules.Resolve(new Combatant(100, 14, 0, 20), new Combatant(100, 14, 0, 20), new Rng(5), out int a1, out int b1);
            int w2 = DuelRules.Resolve(new Combatant(100, 14, 0, 20), new Combatant(100, 14, 0, 20), new Rng(5), out int a2, out int b2);
            Assert.AreEqual(w1, w2); Assert.AreEqual(a1, a2); Assert.AreEqual(b1, b2);
            Assert.AreEqual(0, w1 == 0 ? b1 : a1, "loser at 0");
        }

        [Test]
        public void Duel_StrongerSoldierWinsMostOfTheTime()
        {
            int strongWins = 0;
            for (ulong seed = 1; seed <= 50; seed++)
                if (DuelRules.Resolve(new Combatant(190, 20, 16, 28), new Combatant(100, 12, 0, 20), new Rng(seed), out _, out _) == 0) strongWins++;
            Assert.GreaterOrEqual(strongWins, 45);
        }

        [Test]
        public void Wear_CoarseStates()
        {
            Assert.AreEqual(Wear.Fresh, DuelRules.WearOf(100, 100));
            Assert.AreEqual(Wear.Fresh, DuelRules.WearOf(61, 100));
            Assert.AreEqual(Wear.Hurt, DuelRules.WearOf(60, 100));
            Assert.AreEqual(Wear.Hurt, DuelRules.WearOf(31, 100));
            Assert.AreEqual(Wear.BadlyHurt, DuelRules.WearOf(30, 100));
        }

        [Test]
        public void Duel_Ticking_AtWarSoldiersFightOneOnOneAlternating_UntilOneDies()
        {
            var s = NewState(2);
            WarRules.SetPoints(s, new[] { 0, 2 }, null);
            var a = Place(s, 0, 5); var b = Place(s, 1, 5);
            var c = Place(s, 1, 5); // queues
            var ev = new EventBuffer();
            Run(s, ev, 1);
            Assert.AreEqual(1, Of<DuelStarted>(ev).Count);
            Assert.AreEqual(1, s.War.Duels.Count);
            Assert.AreEqual(0, c.DuelId, "the third waits its turn");
            Run(s, ev, 400);
            var swings = Of<DuelSwing>(ev);
            Assert.Greater(swings.Count, 3);
            Assert.AreEqual(swings[0].Attacker, swings[2].Attacker);
            Assert.AreNotEqual(swings[0].Attacker, swings[1].Attacker);
            Assert.AreEqual(2, Of<SoldierDied>(ev).Count, "winner of the first duel meets the queued soldier, or dies");
        }

        [Test]
        public void Duel_Peace_SoldiersPassWithoutFighting()
        {
            var s = NewState(3);
            WarRules.SetPoints(s, new[] { 3, 2, 2 }, null); // lead of 1: peace
            Place(s, 0, 5); Place(s, 1, 5);
            var ev = new EventBuffer();
            Run(s, ev, 300);
            Assert.AreEqual(0, Of<DuelStarted>(ev).Count);
            Assert.AreEqual(2, s.War.Soldiers.Count);
        }

        [Test]
        public void Duel_EndsWhenPeaceComes()
        {
            var s = NewState(2);
            WarRules.SetPoints(s, new[] { 0, 2 }, null);
            Place(s, 0, 5); Place(s, 1, 5);
            var ev = new EventBuffer();
            Run(s, ev, 5);
            Assert.AreEqual(1, s.War.Duels.Count);
            WarRules.SetPoints(s, new[] { 1, 2 }, ev);
            Run(s, ev, 2);
            Assert.AreEqual(0, s.War.Duels.Count);
            Assert.AreEqual("peace", Of<DuelEnded>(ev).Single().Reason);
            Assert.AreEqual(2, s.War.Soldiers.Count);
        }

        // ---- war state

        [Test]
        public void WarState_LeaderByTwoOrMore_AllOthersAtWarWithIt()
        {
            var s = NewState(4);
            var ev = new EventBuffer();
            WarRules.SetPoints(s, new[] { 5, 3, 3, 1 }, ev);
            for (int p = 1; p < 4; p++) Assert.IsTrue(WarApi.AtWar(s, 0, p) && WarApi.AtWar(s, p, 0));
            Assert.IsFalse(WarApi.AtWar(s, 1, 2));
            Assert.AreEqual(0, Of<WarStateChanged>(ev).Single().Leader);
            WarRules.SetPoints(s, new[] { 5, 3, 3, 1 }, ev);
            Assert.AreEqual(1, Of<WarStateChanged>(ev).Count, "unchanged state emits nothing");
        }

        [Test]
        public void WarState_LeadOfExactlyOne_IsPeace()
        {
            var s = NewState(4);
            WarRules.SetPoints(s, new[] { 4, 3, 3, 1 }, null);
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) Assert.IsFalse(WarApi.AtWar(s, a, b));
        }

        [Test]
        public void WarState_TiedLeadersAreAtWarWithEachOther_OthersAtPeace()
        {
            var s = NewState(4);
            s.War.TiedLeadersFight = TieRule.OnlyWhenNotEveryoneTied; // the pre-F1 reading; new games default to NeverWhenTied
            WarRules.SetPoints(s, new[] { 6, 6, 2, 1 }, null);
            Assert.IsTrue(WarApi.AtWar(s, 0, 1));
            Assert.IsFalse(WarApi.AtWar(s, 0, 2));
            Assert.IsFalse(WarApi.AtWar(s, 2, 3));
            Assert.AreEqual(-1, WarApi.Leader(s));
        }

        [Test]
        public void WarState_TiedLeadersNeverFight_ByDefault_ALeadOfTwoStillDoes()
        {
            var s = NewState(4);
            Assert.AreEqual(TieRule.NeverWhenTied, s.War.TiedLeadersFight);
            WarRules.SetPoints(s, new[] { 6, 6, 2, 1 }, null);
            Assert.IsFalse(s.War.AtWar.Any(x => x), "WA-11 (balance F1): peoples tied at the top never fight");
            WarRules.SetPoints(s, new[] { 6, 3, 3, 3 }, null);
            Assert.IsTrue(WarApi.AtWar(s, 0, 1) && WarApi.AtWar(s, 0, 2) && WarApi.AtWar(s, 0, 3));
        }

        [Test]
        public void WarState_ChangeBackToPeace_EmitsEmptyPairs()
        {
            var s = NewState(3);
            var ev = new EventBuffer();
            WarRules.SetPoints(s, new[] { 5, 1, 1 }, ev);
            WarRules.SetPoints(s, new[] { 5, 4, 1 }, ev);
            var last = Of<WarStateChanged>(ev).Last();
            Assert.AreEqual(0, last.Pairs.Length);
        }

        // ---- movement and purging

        [Test]
        public void Movement_StepsNodeToNodeAlongTrails_OnePerMoveTicks()
        {
            var s = NewState(2);
            var x = Place(s, 0, 1);
            int n2 = PlagueApi.Neighbours(1)[0];
            int n3 = PlagueApi.Neighbours(n2).First(n => n != 1);
            x.Path = new List<int> { n2, n3 };
            var ev = new EventBuffer();
            Run(s, ev, 1);
            Assert.IsTrue(x.Moving);
            Run(s, ev, WarData.MoveTicks - 1);
            Assert.IsTrue(x.Moving);
            Run(s, ev, 1);
            Assert.AreEqual(2, Of<SoldierStepped>(ev).Count, "arrived at the first node and set off at once");
            Assert.AreEqual(n2, Of<SoldierStepped>(ev)[1].From);
            Run(s, ev, WarData.MoveTicks);
            Assert.AreEqual(n3, x.Node);
            Assert.AreEqual(2, Of<SoldierStepped>(ev).Count);
        }

        [Test]
        public void Movement_CannotPassNodeWithThreeDemons_UntilPurged()
        {
            var s = NewState(2);
            var x = Place(s, 0, 1);
            int mid = PlagueApi.Neighbours(1)[0];
            int far = PlagueApi.Neighbours(mid).First(n => n != 1);
            SetDemons(s, mid, 3);
            x.Path = new List<int> { mid, far };
            var ev = new EventBuffer();
            Run(s, ev, 200);
            Assert.AreEqual(1, x.Node, "blocked: stays put and drops the order");
            Assert.AreEqual(0, x.Path.Count);
            SetDemons(s, mid, 2);
            x.Path = new List<int> { mid, far };
            Run(s, ev, 200);
            Assert.AreEqual(far, x.Node, "fewer than 3 does not block");
        }

        [Test]
        public void Purge_OneDemonPerPurgeTicks_ThenFree()
        {
            var s = NewState(2);
            var x = Place(s, 0, 7);
            SetDemons(s, 7, 2);
            x.Order = OrderKind.Purge; x.OrderTarget = 7; x.PurgeLeft = WarData.PurgeTicks;
            var ev = new EventBuffer();
            Run(s, ev, 149);
            Assert.AreEqual(2, PlagueApi.DemonsAt(s, 7));
            Run(s, ev, 1);
            Assert.AreEqual(1, PlagueApi.DemonsAt(s, 7));
            Run(s, ev, 150);
            Assert.AreEqual(0, PlagueApi.DemonsAt(s, 7));
            Assert.AreEqual(2, Of<DemonPurged>(ev).Count);
            Assert.AreEqual(0, x.PurgeLeft);
            Assert.AreEqual(2, s.Plague.Banished);
        }

        // ---- brain, garrison rule

        [Test]
        public void Brain_SendsSoldierToDemonsOnOwnHex_ButNeverEmptiesTheGarrison()
        {
            var s = NewState(2);
            int c = CornerOfNode(s, 3);
            WarRules.AddSettlement(s, 0, c);
            SetDemons(s, 3, 1);
            var ev = new EventBuffer();
            Run(s, ev, 1000);
            Assert.AreEqual(1, PlagueApi.DemonsAt(s, 3), "lone soldier stays (WA-5)");
            Assert.AreEqual(1, WarApi.PresentAt(s, c, 0));
            WarRules.UpgradeCity(s, c);
            Run(s, ev, 1000);
            Assert.AreEqual(0, PlagueApi.DemonsAt(s, 3));
            Assert.AreEqual(1, Of<DemonPurged>(ev).Count);
            Assert.AreEqual(2, WarApi.PresentAt(s, c, 0), "the purger walks home afterwards");
        }

        [Test]
        public void Brain_OwnDemonsBeforeOthersDemons()
        {
            var s = NewState(3);
            int ca = CornerOfNode(s, 3);
            int cb = CornerOfNode(s, 30);
            WarRules.AddSettlement(s, 0, ca, true);
            WarRules.AddSettlement(s, 1, cb);
            SetDemons(s, 30, 1);
            SetDemons(s, 3, 1);
            var ev = new EventBuffer();
            Run(s, ev, WarData.ArmyPlanTicks);
            var mover = s.War.Soldiers.First(x => x.People == 0 && x.Order != OrderKind.None);
            Assert.AreEqual(OrderKind.Purge, mover.Order);
            Assert.AreEqual(3, mover.OrderTarget);
        }

        [Test]
        public void Brain_IgnoresDemonsTouchingOnlyTheLeader()
        {
            var s = NewState(3);
            int ca = CornerOfNode(s, 3);
            int cb = CornerOfNode(s, 30);
            WarRules.AddSettlement(s, 0, ca, true);
            WarRules.AddSettlement(s, 1, cb);
            SetDemons(s, 30, 1);
            WarRules.SetPoints(s, new[] { 1, 3, 0 }, null);  // people 1 leads by 2; hex of node 30 touches only people 1
            var ev = new EventBuffer();
            Run(s, ev, WarData.ArmyPlanTicks);
            Assert.IsFalse(s.War.Soldiers.Any(x => x.People == 0 && x.Order == OrderKind.Purge));
        }

        [Test]
        public void Brain_AtPeace_DoesNotAttack_AtWarTargetsWeakestSettlement()
        {
            var s = NewState(2);
            int ca = CornerOfNode(s, 3);
            int cb = CornerOfNode(s, 5);
            WarRules.AddSettlement(s, 0, ca, true);
            WarRules.AddSettlement(s, 1, cb, true);
            var ev = new EventBuffer();
            Run(s, ev, WarData.ArmyPlanTicks);
            Assert.IsFalse(s.War.Soldiers.Any(x => x.Order == OrderKind.Attack));
            WarRules.SetPoints(s, new[] { 0, 2 }, ev);
            Run(s, ev, WarData.ArmyPlanTicks);
            var attacker = s.War.Soldiers.First(x => x.People == 0 && x.Order == OrderKind.Attack);
            Assert.AreEqual(cb, attacker.OrderTarget);
        }

        // ---- burning and looting

        private static WorldState BurnScene(bool homeVictim, out int victimCorner, out EventBuffer ev)
        {
            var s = NewState(2);
            int ca = CornerOfNode(s, 3);
            int cb = CornerOfNode(s, 5);
            WarRules.AddSettlement(s, 0, ca, false, true);
            WarRules.AddSettlement(s, 1, cb, false, homeVictim);
            victimCorner = cb;
            var victim = s.War.Soldiers.First(x => x.People == 1);
            WarRules.RemoveSoldier(s, victim);
            WarRules.Deliver(s, cb, 0, 4);
            WarApi.SettlementAt(s, cb).Proc[2] = 2;
            WarRules.SetPoints(s, new[] { 0, 2 }, null);
            ev = new EventBuffer();
            var attacker = s.War.Soldiers.First(x => x.People == 0);
            attacker.Corner = -1; attacker.Node = 5;
            attacker.Order = OrderKind.Attack; attacker.OrderTarget = cb;
            attacker.Path = new List<int> { -(cb + 1) };
            return s;
        }

        [Test]
        public void Burn_EnemyReachesUnguardedSettlement_BurnedWithRuin_ThenFreeAfter1800()
        {
            var s = BurnScene(false, out int cb, out var ev);
            Run(s, ev, 100);
            var e = Of<SettlementBurned>(ev).Single();
            Assert.AreEqual(1, e.People);
            Assert.AreEqual(1, e.PointsLost);
            Assert.AreEqual(6, e.GoodsLost);
            Assert.IsNull(WarApi.SettlementAt(s, cb));
            Assert.IsTrue(WarApi.IsRuin(s, cb));
            Assert.IsFalse(WarRules.AddSettlement(s, 1, cb), "ruin blocks founding");
            Assert.AreEqual(0, s.War.Soldiers.Count(x => x.Corner == cb), "attacker steps back to a node");
            Run(s, ev, WarData.RuinTicks);
            Assert.IsFalse(WarApi.IsRuin(s, cb));
            Assert.AreEqual(1, Of<RuinCleared>(ev).Count);
            Assert.IsTrue(WarRules.AddSettlement(s, 1, cb));
        }

        [Test]
        public void Burn_NotWhileAnyDefenderStands()
        {
            var s = NewState(2);
            int ca = CornerOfNode(s, 3);
            int cb = CornerOfNode(s, 5);
            WarRules.AddSettlement(s, 0, ca, false, true);
            WarRules.AddSettlement(s, 1, cb);
            WarRules.SetPoints(s, new[] { 0, 2 }, null);
            var a = Place(s, 0, 5);
            a.Order = OrderKind.Attack; a.OrderTarget = cb; a.Path = new List<int> { -(cb + 1) };
            var ev = new EventBuffer();
            Run(s, ev, 60);
            Assert.AreEqual(0, Of<SettlementBurned>(ev).Count, "the garrison duels first");
            Assert.AreEqual(1, Of<DuelStarted>(ev).Count);
        }

        [Test]
        public void Loot_HomeCampLosesGoodsButIsNeverBurned_AttackerWithdraws()
        {
            var s = BurnScene(true, out int cb, out var ev);
            Run(s, ev, 100);
            var e = Of<CampLooted>(ev).Single();
            Assert.AreEqual(4, e.GoodsLost, "the 2 processed goods already went to the lodge");
            Assert.AreEqual(0, Of<SettlementBurned>(ev).Count);
            var st = WarApi.SettlementAt(s, cb);
            Assert.NotNull(st);
            Assert.AreEqual(0, WarApi.GoodsInStore(st));
            Assert.IsFalse(WarApi.IsRuin(s, cb));
            Assert.AreEqual(0, s.War.Soldiers.Count(x => x.People == 0 && x.Corner == cb));
        }

        [Test]
        public void Peace_EnemySoldierAtSettlement_DoesNothing()
        {
            var s = BurnScene(false, out int cb, out var ev);
            WarRules.SetPoints(s, new[] { 1, 2 }, ev);
            var a = s.War.Soldiers.First(x => x.People == 0);
            a.Path = new List<int>();
            a.Order = OrderKind.None;
            a.Node = 0; a.Corner = cb;
            Run(s, ev, 100);
            Assert.AreEqual(0, Of<SettlementBurned>(ev).Count);
            Assert.NotNull(WarApi.SettlementAt(s, cb));
        }

        // ---- scripted skirmish and determinism

        private static string Skirmish(ulong seed, out List<SimEvent> events)
        {
            var s = NewState(3, seed);
            var ev = new EventBuffer();
            int h0 = CornerOfNode(s, 3), h1 = CornerOfNode(s, 5);
            WarRules.AddSettlement(s, 0, h0, true, true);
            WarRules.AddSettlement(s, 1, h1, true, true);
            WarRules.AddSettlement(s, 2, CornerOfNode(s, 20), true);
            WarApi.LodgeOf(s, 0).Queue.AddRange(new[] { 0, 3, 5, 1, 1, 8 });
            WarApi.LodgeOf(s, 1).Queue.AddRange(new[] { 2, 4, 6 });
            SetDemons(s, 3, 2); SetDemons(s, 4, 1);
            WarRules.SetPoints(s, new[] { 2, 4, 2 }, ev);
            for (int i = 0; i < 4000; i++) { WarRules.Tick(s, ev, null); if (i == 1500) WarRules.SetPoints(s, new[] { 5, 4, 2 }, ev); }
            events = ev.Items.ToList();
            var sb = new System.Text.StringBuilder();
            foreach (var e in events) sb.Append(e.Tick).Append(':').Append(e.GetType().Name).Append(';');
            foreach (var x in s.War.Soldiers) sb.Append(x.Id).Append(',').Append(x.People).Append(',').Append(x.Hp).Append(',').Append(x.Node).Append(',').Append(x.Corner).Append(';');
            sb.Append(PlagueApi.TotalDemons(s));
            return sb.ToString();
        }

        [Test]
        public void Skirmish_Scripted_ProducesTrainingDuelsAndPurges()
        {
            Skirmish(7, out var events);
            Assert.AreEqual(3, events.OfType<SoldierTrained>().Count());
            Assert.Greater(events.OfType<DuelStarted>().Count(), 0);
            Assert.Greater(events.OfType<SoldierDied>().Count(), 0);
            Assert.Greater(events.OfType<DemonPurged>().Count(), 0);
            Assert.GreaterOrEqual(events.OfType<WarStateChanged>().Count(), 2);
        }

        [Test]
        public void Skirmish_IsDeterministicFromTheSeed()
        {
            string a = Skirmish(7, out _);
            string b = Skirmish(7, out _);
            string c = Skirmish(8, out _);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void System_RunsInsideAWorldSim_AndStateRoundTripsThroughJson()
        {
            var sim = WorldSimFactory.CreateNew(3);
            PlagueModule.InitState(sim.State);
            WarModule.InitState(sim.State, 2);
            WarModule.Register(sim);
            WarRules.AddSettlement(sim.State, 0, CornerOfNode(sim.State, 3), true, true);
            sim.Step();
            Assert.AreEqual(1, sim.State.War.Clock);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(sim.State.War);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<WarState>(json);
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(back), json);
        }
    }
}
