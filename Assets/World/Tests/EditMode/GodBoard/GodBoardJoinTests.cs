using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.GodBoard;
using World.Sim.Plague;
using World.Sim.Save;
using World.Sim.Table;
using World.Sim.War;

namespace World.Sim.Tests
{
    /// <summary>
    /// G0-3: settlement corners (three hexes meet), the war wired into god board games (garrisons, deliveries, war state,
    /// burning back to the table, save v7), the island's fall as a game over, and WA-11's tie rule flag.
    /// </summary>
    public class GodBoardJoinTests
    {
        private const ulong Seed = 20261119;
        private const int MaxTicks = 80000;

        private static WorldSim GodBoard(ulong seed = Seed, int humanSeat = -1)
        {
            var setup = GameSetup.GodBoard;
            setup.TableHumanSeat = humanSeat;
            return WorldSimFactory.CreateNew(seed, setup);
        }

        private static List<SimEvent> StepUntil(WorldSim sim, Func<bool> done, int maxTicks)
        {
            var log = new List<SimEvent>();
            for (int i = 0; i < maxTicks && !done(); i++)
            {
                sim.Step();
                log.AddRange(sim.DrainEvents());
            }
            return log;
        }

        private static List<SimEvent> Steps(WorldSim sim, int ticks) => StepUntil(sim, () => false, ticks);

        /// <summary>A corner where fewer than three hexes meet and no settlement stands on it or next to it.</summary>
        private static int RimCorner(WorldState s)
        {
            var topo = s.Board.Topology;
            var t = s.Table;
            foreach (int c in topo.Corners)
            {
                if (PlotLayout.IsSettlementCorner(topo, c) || t.CornerOwner[c] >= 0) continue;
                if (topo.CornerNeighbours(c).Any(n => t.CornerOwner[n] >= 0)) continue;
                return c;
            }
            Assert.Fail("no free rim corner");
            return -1;
        }

        // ---- 1. Settlement corners (G0-2 open problem 3) ----

        [Test]
        public void Corners_SetupSettlement_RefusedWhereFewerThanThreeHexesMeet()
        {
            var sim = TableTestKit.Create(wire: TableTestKit.IdleAi);
            TableTestKit.Do(sim, new Sacrifice());
            int rim = RimCorner(sim.State);
            TableTestKit.Refused(sim, new PlaceTableSettlement { Corner = rim }, TableRefusals.InvalidSite);
            // Every corner the rules call legal is a three-hex corner, and there are still plenty of them.
            var legal = TableRules.LegalSettlementCorners(sim.State, 0, false);
            Assert.Greater(legal.Count, 20);
            foreach (int c in legal) Assert.IsTrue(PlotLayout.IsSettlementCorner(sim.State.Board.Topology, c), "corner " + c);
        }

        [Test]
        public void Corners_PlaySettlement_RefusedOnARimCorner_EvenWhenTheRoadReachesIt()
        {
            var sim = TableTestKit.ToHumanTurn();
            var s = sim.State;
            var t = s.Table;
            int rim = RimCorner(s);
            // Give the player a road onto the rim corner and the cards: still refused.
            t.RoadOwner[s.Board.Topology.CornerHexEdges(rim)[0]] = 0;
            TableTestKit.SetHand(t, 0, 2, 2, 2, 2, 2);
            Assert.AreEqual(TableRefusals.InvalidSite, TableRules.SettlementRefusal(s, 0, rim, true));
            TableTestKit.Refused(sim, new PlaceTableSettlement { Corner = rim }, TableRefusals.InvalidSite);
        }

        [Test]
        public void Corners_AiOnlyGames_EverySettlementStandsWhereThreeHexesMeet([Values(1UL, 2UL, 3UL)] ulong seed)
        {
            var sim = GodBoard(seed);
            var topo = sim.State.Board.Topology;
            var log = StepUntil(sim, () => sim.State.Table.Phase == TablePhase.Ended, MaxTicks);
            var built = log.OfType<Built>().Where(b => b.Kind == "settlement").ToList();
            Assert.GreaterOrEqual(built.Count, 4, "at least the setup settlements (the island may fall early)");
            foreach (var b in built) Assert.IsTrue(PlotLayout.IsSettlementCorner(topo, b.Spot), "settlement on corner " + b.Spot);
            // The adviser target never points at a rim corner either.
            for (int p = 0; p < sim.State.Table.PeopleCount; p++)
            {
                var target = TableAi.TargetFor(sim.State, p);
                if (target.Kind == TableTargetKind.Settlement || target.Kind == TableTargetKind.Road)
                    if (target.Site >= 0) Assert.IsTrue(PlotLayout.IsSettlementCorner(topo, target.Site), "target site " + target.Site);
            }
        }

        // ---- 2. The war in a god board game ----

        [Test]
        public void War_NewGodBoardGame_HomeCampsWithLodgesAndGarrisons_AtPeace()
        {
            var sim = GodBoard(humanSeat: PlayerIds.Human);
            var s = sim.State;
            Assert.IsNotNull(s.War);
            Assert.AreEqual(4, s.War.PeopleCount);
            Assert.AreEqual(4, s.War.Settlements.Count);
            for (int p = 0; p < 4; p++)
            {
                var st = WarApi.SettlementAt(s, s.Table.Peoples[p].StartCorner);
                Assert.IsNotNull(st, "home camp of " + p);
                Assert.AreEqual(p, st.People);
                Assert.IsTrue(st.IsHome, "WA-15: home camps are flagged");
                Assert.IsNotNull(WarApi.LodgeOf(s, p), "WA-1: a lodge at the home camp");
                Assert.AreEqual(1, WarApi.PresentAt(s, st.Corner, p), "WA-4: one garrison soldier");
            }
            Assert.IsFalse(s.War.AtWar.Any(x => x), "everyone tied at the start: peace (default tie rule)");
            Assert.AreEqual(TieRule.NeverWhenTied, s.War.TiedLeadersFight, "balance F1: tied leaders never fight");
            Assert.AreEqual(3, s.Plague.InfectEvery, "balance F1: the infection draws every 3rd round");
            Assert.IsTrue(sim.Systems.Any(x => x is WarSystem), "the war system is registered");
            Assert.IsNull(WorldSimFactory.CreateNew(Seed).State.War, "a legacy game has no war");
        }

        [Test]
        public void War_FollowsTheTable_SetupAndBuilds_AddGarrisonsAndCities()
        {
            var sim = GodBoard();
            var s = sim.State;
            // After the setup round: 8 settlements, 8 garrison soldiers (WA-4).
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            Assert.AreEqual(8, s.War.Settlements.Count);
            Assert.AreEqual(8, s.War.Soldiers.Count);
            // Play on until someone has a city.
            StepUntil(sim, () => s.Table.CornerLevel.Any(l => l == 2) || s.Table.Phase == TablePhase.Ended, MaxTicks);
            AssertWarMatchesTable(s);
            int cities = s.Table.CornerLevel.Count(l => l == 2);
            Assert.Greater(cities, 0);
            foreach (var st in s.War.Settlements.Where(x => x.IsCity))
                Assert.GreaterOrEqual(WarApi.PresentAt(s, st.Corner, st.People) + s.War.Soldiers.Count(x => x.People == st.People && x.Corner != st.Corner), 2);
        }

        private static void AssertWarMatchesTable(WorldState s)
        {
            var t = s.Table;
            int owned = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                if (t.CornerOwner[c] < 0) continue;
                owned++;
                var st = WarApi.SettlementAt(s, c);
                Assert.IsNotNull(st, "war settlement at " + c);
                Assert.AreEqual(t.CornerOwner[c], st.People);
                Assert.AreEqual(t.CornerLevel[c] == 2, st.IsCity, "city at " + c);
                Assert.AreEqual(c == t.Peoples[st.People].StartCorner, st.IsHome, "home at " + c);
            }
            Assert.AreEqual(owned, s.War.Settlements.Count);
            for (int p = 0; p < t.PeopleCount; p++) Assert.AreEqual(t.Peoples[p].Points, s.War.Points[p], "war points of " + p);
        }

        [Test]
        public void War_Production_DeliversTheSameRawGoodsToTheStore_CardsStaySeparate()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            int checkedRolls = 0;
            for (int i = 0; i < 20000 && checkedRolls < 5; i++)
            {
                var rawBefore = s.War.Settlements.ToDictionary(x => x.Corner, x => (int[])x.Raw.Clone());
                var handsBefore = s.Table.Peoples.Select(p => p.Hand.Sum()).ToArray();
                sim.Step();
                var evs = sim.DrainEvents();
                var produced = evs.OfType<Produced>().SingleOrDefault();
                if (produced == null || produced.Gains.Sum() == 0) continue;
                if (evs.OfType<GoodProcessed>().Any() || evs.OfType<SettlementBurned>().Any() || evs.OfType<CampLooted>().Any() || evs.OfType<Traded>().Any()
                    || evs.OfType<Built>().Any() || evs.OfType<Discarded>().Any()) continue;
                // Per people and raw good: the store gained exactly the cards the people got (CL-2).
                for (int p = 0; p < s.Table.PeopleCount; p++)
                    for (int g = 0; g < WarData.RawCount; g++)
                    {
                        int gained = s.War.Settlements.Where(x => x.People == p && rawBefore.ContainsKey(x.Corner)).Sum(x => x.Raw[g] - rawBefore[x.Corner][g]);
                        Assert.AreEqual(produced.Gains[p * Wares.RawCount + g], gained, "people " + p + " good " + g);
                    }
                // Cards reached the hand (the table's production is unchanged).
                for (int p = 0; p < s.Table.PeopleCount; p++)
                {
                    int cards = 0;
                    for (int g = 0; g < WarData.RawCount; g++) cards += produced.Gains[p * Wares.RawCount + g];
                    Assert.AreEqual(handsBefore[p] + cards, s.Table.Peoples[p].Hand.Sum());
                }
                checkedRolls++;
            }
            Assert.AreEqual(5, checkedRolls, "five production rolls checked");
        }

        [Test]
        public void War_Burning_RemovesTheSettlementFromTheTable_PointsDrop_CornerReopensAfterTheRuin()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            sim.DrainEvents();
            var t = s.Table;
            t.Paused = true; // the table holds still (the world, the war and the sync go on)
            int victim = 1, attacker = 0;
            int corner = Enumerable.Range(0, t.CornerOwner.Length).First(c => t.CornerOwner[c] == victim && c != t.Peoples[victim].StartCorner);
            int pointsBefore = t.Peoples[victim].Points;
            // The garrison is gone and an enemy soldier stands at the corner, at war.
            s.War.Soldiers.RemoveAll(x => x.Corner == corner);
            var raider = s.War.Soldiers.First(x => x.People == attacker);
            raider.Corner = corner; raider.Node = 0; raider.Path.Clear(); raider.Order = OrderKind.None;
            s.War.AtWar[attacker * 4 + victim] = s.War.AtWar[victim * 4 + attacker] = true;

            var log = Steps(sim, 3);
            var burned = log.OfType<SettlementBurned>().Single();
            Assert.AreEqual(corner, burned.Corner);
            var lost = log.OfType<SettlementLost>().Single();
            Assert.AreEqual(victim, lost.People);
            Assert.AreEqual("burned", lost.Cause);
            Assert.AreEqual(-1, t.CornerOwner[corner]);
            Assert.AreEqual(pointsBefore - 1, t.Peoples[victim].Points, "TU-6: the points are lost at once");
            Assert.AreEqual(-1, log.OfType<PointsChanged>().Single(x => x.People == victim).Delta);
            Assert.AreEqual(t.Peoples[victim].Points, s.War.Points[victim], "the war state follows (WA-11)");
            CollectionAssert.Contains(t.ClosedCorners, corner);
            Assert.AreEqual(TableRefusals.InvalidSite, TableRules.SettlementRefusal(s, victim, corner, false), "the ruin closes the corner");

            log = StepUntil(sim, () => !t.ClosedCorners.Contains(corner), WarData.RuinTicks + 10);
            Assert.AreEqual(corner, log.OfType<CornerReopened>().Single().Corner);
            Assert.IsFalse(WarApi.IsRuin(s, corner));
            Assert.AreNotEqual(TableRefusals.InvalidSite, TableRules.SettlementRefusal(s, victim, corner, false), "open again under the normal rules");
        }

        [Test]
        public void War_HomeCamp_IsLootedNeverBurned_TableKeepsIt()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            sim.DrainEvents();
            var t = s.Table;
            t.Paused = true;
            int victim = 2, attacker = 3;
            int home = t.Peoples[victim].StartCorner;
            WarRules.Deliver(s, home, 0, 3);
            s.War.Soldiers.RemoveAll(x => x.Corner == home);
            var raider = s.War.Soldiers.First(x => x.People == attacker);
            raider.Corner = home; raider.Node = 0; raider.Path.Clear(); raider.Order = OrderKind.None;
            s.War.AtWar[attacker * 4 + victim] = s.War.AtWar[victim * 4 + attacker] = true;
            var log = Steps(sim, 3);
            Assert.AreEqual(1, log.OfType<CampLooted>().Count());
            Assert.IsFalse(log.OfType<SettlementBurned>().Any());
            Assert.IsFalse(log.OfType<SettlementLost>().Any());
            Assert.AreEqual(victim, t.CornerOwner[home]);
        }

        [Test]
        public void War_Linked_FollowsTheOwnRoadsToTheLodge()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            var t = s.Table;
            for (int p = 0; p < 4; p++)
            {
                Assert.IsTrue(GodBoardModule.Linked(s, t.Peoples[p].StartCorner), "the lodge's own camp");
                // The setup settlement has one road that does not reach the home camp.
                int second = Enumerable.Range(0, t.CornerOwner.Length).First(c => t.CornerOwner[c] == p && c != t.Peoples[p].StartCorner);
                Assert.IsFalse(GodBoardModule.Linked(s, second), "setup settlement of " + p + " is not road-linked yet");
            }
        }

        // ---- G2-3 grants (crystal rewards) reach the war and the age's end ----

        [Test]
        public void Grants_FreeCityAndSettlement_GetTheirGarrisons()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            sim.DrainEvents();
            var t = s.Table;
            t.Paused = true;
            int p = 1;
            int city = TableGrants.BestCityCorner(s, p);
            int soldiersBefore = s.War.Soldiers.Count;
            var ev = new EventBuffer();
            Assert.IsTrue(TableGrants.GrantCity(s, p, city, ev));
            int corner = TableGrants.BestSettlementCorner(s, p);
            Assert.IsTrue(TableGrants.GrantSettlement(s, p, corner, ev));
            Steps(sim, 1);
            Assert.IsTrue(WarApi.SettlementAt(s, city).IsCity, "WA-4: the free city is a city in the war");
            var st = WarApi.SettlementAt(s, corner);
            Assert.IsNotNull(st, "the free settlement is in the war");
            Assert.AreEqual(p, st.People);
            Assert.AreEqual(1, WarApi.PresentAt(s, corner, p), "its start soldier");
            Assert.AreEqual(soldiersBefore + 2, s.War.Soldiers.Count, "+1 for the city, +1 for the settlement");
            Assert.AreEqual(t.Peoples[p].Points, s.War.Points[p], "the war state follows the grant");
            Assert.IsTrue(GodBoardModule.WarInSync(s));
        }

        [Test]
        public void Grants_ReachingTenPoints_EndTheAge_WithTheSameHandling()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            sim.DrainEvents();
            var t = s.Table;
            int p = 2;
            // Settlements and cities until one more grant reaches 10 points.
            var ev = new EventBuffer();
            for (int i = 0; i < 20 && TableRules.Points(s, p) < 9; i++)
                if (!TableGrants.GrantCityOrSettlement(s, p, ev).Granted) break;
            Assert.AreEqual(TablePhase.Main, t.Phase);
            if (TableRules.Points(s, p) < 9) Assert.Inconclusive("could not grant up to 9 points");
            Steps(sim, 1);
            Assert.IsFalse(s.Plague.Ended);
            var ev2 = new EventBuffer();
            while (t.Phase != TablePhase.Ended && TableGrants.GrantCityOrSettlement(s, p, ev2).Granted) { }
            Assert.AreEqual(TablePhase.Ended, t.Phase, "a grant ended the age");
            Assert.AreEqual(p, t.Winner);
            Assert.IsFalse(s.Plague.Ended, "the hook did not run (static AfterChange)");
            var log = Steps(sim, 1);
            Assert.IsTrue(s.Plague.Ended, "HI-8: the plague ends");
            Assert.AreEqual(1, log.OfType<PlagueEnded>().Count());
            Assert.AreEqual(Eras.Era.Historic, s.Eras.Current, "G4-2: the island steps into Historic at once (ready, then advanced)");
            Assert.IsTrue(log.OfType<Eras.ChapterReady>().Any(e => e.Chapter == Eras.ChapterIds.Historic));
            Assert.IsFalse(t.IslandFell);
            Assert.AreEqual(0, Steps(sim, 50).OfType<PlagueEnded>().Count(), "once only");
        }

        // ---- TR-4 with points changing while an offer is open (a burning or a grant) ----

        [Test]
        public void Tr4_AnAiOffer_IsClosedToAnAnswererThatBecameLeading()
        {
            var sim = TableTestKit.ToHumanTurn(wire: seams => { TableTestKit.IdleAi(seams); seams.AiRefusal = TableAi.Refusal; });
            var t = TableTestKit.T(sim);
            var s = sim.State;
            // An open offer from AI seat 1 to the player, made while points were level...
            var o = new TradeOffer { Id = t.NextOfferId++, From = 1, To = 0, Give = TableTestKit.Cards(sheep: 1), Get = TableTestKit.Cards(1), TicksLeft = 100 };
            t.Offers.Add(o);
            TableTestKit.SetHand(t, 0, 1, 0, 0, 0, 0);
            TableTestKit.SetHand(t, 1, 0, 0, 0, 1, 0);
            // ...then the player alone leads (people 1 lost a settlement, as after a burning).
            int lostCorner = Enumerable.Range(0, t.CornerOwner.Length).First(c => t.CornerOwner[c] == 1 && c != t.Peoples[1].StartCorner);
            t.CornerOwner[lostCorner] = -1; t.CornerLevel[lostCorner] = 0;
            Assert.IsTrue(TableAi.IsLeading(s, 0) && !TableAi.IsLeading(s, 1));
            TableTestKit.Refused(sim, new AnswerOffer { OfferId = o.Id, Accept = true }, TableRefusals.OfferClosed);
            // Declining is still allowed.
            Assert.IsTrue(TableTestKit.Submit(sim, new AnswerOffer { OfferId = o.Id, Accept = false }).Ok);
        }

        // ---- 3. The island falls (PL-10) ----

        private static void PrimeOutbreaks(WorldState s)
        {
            var pl = s.Plague;
            pl.Outbreaks = 7;
            for (int i = 0; i < pl.Demons.Length; i++)
            {
                pl.Demons[i] = 3;
                int el = PlagueApi.ElementOf(i + 1);
                for (int k = 0; k < 4; k++) pl.KindCount[i * 4 + k] = k == el ? 3 : 0;
            }
        }

        [Test]
        public void IslandFalls_GameLost_TableClosedForGood_WarStopped()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 2, MaxTicks);
            sim.DrainEvents();
            PrimeOutbreaks(s);
            var log = StepUntil(sim, () => s.Table.Phase == TablePhase.Ended, MaxTicks);
            Assert.IsTrue(s.Plague.IslandFell);
            var lost = log.OfType<GameLost>().Single();
            Assert.AreEqual("island_fell", lost.Cause);
            Assert.AreEqual(1, log.OfType<IslandFell>().Count());
            Assert.IsTrue(s.Table.IslandFell);
            Assert.AreEqual(-1, s.Table.Winner);
            Assert.IsFalse(log.OfType<AgeEnded>().Any(), "nobody wins");
            Assert.IsTrue(s.War.Stopped);
            // The fall happened at the round's infection: no turn started after it.
            int fallIndex = log.FindIndex(e => e is GameLost);
            Assert.IsFalse(log.Skip(fallIndex).OfType<TurnStarted>().Any());

            long warClock = s.War.Clock;
            int round = s.Table.Round;
            var after = Steps(sim, 2000);
            Assert.IsFalse(after.OfType<TurnStarted>().Any());
            Assert.IsFalse(after.OfType<Produced>().Any());
            Assert.IsFalse(after.OfType<SoldierStepped>().Any() || after.OfType<DuelStarted>().Any() || after.OfType<SettlementBurned>().Any());
            Assert.AreEqual(warClock, s.War.Clock, "the war is stopped");
            Assert.AreEqual(round, s.Table.Round);
            Assert.AreEqual(1, log.Concat(after).OfType<GameLost>().Count());
        }

        [Test]
        public void IslandFalls_OnAnEpidemic_AlsoEndsTheGame_AndCommandsAreRefused()
        {
            var sim = GodBoard(humanSeat: PlayerIds.Human);
            var s = sim.State;
            TableTestKit.Do(sim, new Sacrifice());
            TableTestKit.HumanSetup(sim);
            StepUntil(sim, () => s.Table.Phase == TablePhase.Main && s.Table.Current == 0 && s.Plague.Rounds >= PlagueRules.GraceRounds, MaxTicks * 4);
            Assert.AreEqual(0, s.Table.Current);
            sim.DrainEvents();
            PrimeOutbreaks(s);
            // The player ends the turn; the next roll is forced to 7: an epidemic past the grace rounds outbreaks at once.
            TableTestKit.ForceNextRoll(s.Table, 7);
            var log = TableTestKit.Do(sim, new EndTurn());
            log.AddRange(StepUntil(sim, () => s.Table.Phase == TablePhase.Ended, 2000));
            Assert.AreEqual(1, log.OfType<GameLost>().Count());
            Assert.IsTrue(s.Table.IslandFell);
            TableTestKit.Refused(sim, new EndTurn(), TableRefusals.TableClosed);
            TableTestKit.Refused(sim, new Sacrifice(), TableRefusals.TableClosed);
        }

        [Test]
        public void IslandFalls_SafetyNet_ThePlagueFallingOutsideTheTableStillEndsIt()
        {
            var sim = GodBoard();
            var s = sim.State;
            StepUntil(sim, () => s.Table.Round >= 1, MaxTicks);
            s.Plague.IslandFell = true;
            var log = Steps(sim, 2);
            Assert.AreEqual(1, log.OfType<GameLost>().Count());
            Assert.AreEqual(TablePhase.Ended, s.Table.Phase);
            Assert.IsTrue(s.War.Stopped);
        }

        // ---- 4. WA-11's tie rule (owner question) ----

        private static WorldState WarOnly(int people)
        {
            var s = new WorldState { Seed = Seed, Rng = new Rng(Seed), Board = BoardGenerator.CreateMvpBoard(Seed) };
            WarModule.InitState(s, people);
            return s;
        }

        [Test]
        public void TieRule_Literal_EveryoneTiedIsAtWar_DefaultIsPeace()
        {
            var lit = WarOnly(4);
            lit.War.TiedLeadersFight = TieRule.Literal;
            WarRules.SetPoints(lit, new[] { 2, 2, 2, 2 }, null);
            Assert.IsTrue(WarApi.AtWar(lit, 0, 1) && WarApi.AtWar(lit, 2, 3), "literal WA-11: all tied, all at war");

            var def = WarOnly(4);
            Assert.AreEqual(TieRule.NeverWhenTied, def.War.TiedLeadersFight, "balance F1 default");
            WarRules.SetPoints(def, new[] { 2, 2, 2, 2 }, null);
            Assert.IsFalse(def.War.AtWar.Any(x => x));
            var mid = WarOnly(4);
            mid.War.TiedLeadersFight = TieRule.OnlyWhenNotEveryoneTied;
            WarRules.SetPoints(mid, new[] { 2, 2, 2, 2 }, null);
            Assert.IsFalse(mid.War.AtWar.Any(x => x));
        }

        [Test]
        public void TieRule_BothModes_TiedLeadersAboveSomeoneFight([Values(TieRule.Literal, TieRule.OnlyWhenNotEveryoneTied)] TieRule rule)
        {
            var s = WarOnly(4);
            s.War.TiedLeadersFight = rule;
            WarRules.SetPoints(s, new[] { 4, 4, 3, 2 }, null);
            Assert.IsTrue(WarApi.AtWar(s, 0, 1));
            Assert.IsFalse(WarApi.AtWar(s, 0, 2) || WarApi.AtWar(s, 2, 3) || WarApi.AtWar(s, 1, 3));
            WarRules.SetPoints(s, new[] { 5, 3, 3, 3 }, null); // lead of 2: everyone against the leader
            Assert.IsTrue(WarApi.AtWar(s, 1, 0) && WarApi.AtWar(s, 2, 0) && WarApi.AtWar(s, 3, 0));
            Assert.IsFalse(WarApi.AtWar(s, 1, 2));
            WarRules.SetPoints(s, new[] { 3, 3, 3 }, null); // fewer points than peoples: the missing one counts as 0, so not everyone is tied
            Assert.IsTrue(WarApi.AtWar(s, 0, 1) && WarApi.AtWar(s, 1, 2));
            Assert.IsFalse(WarApi.AtWar(s, 0, 3));
        }

        // ---- Save (v7) ----

        [Test]
        public void Save_GodBoardWithTheWar_RoundTrips_AndContinuesIdentically()
        {
            var a = GodBoard();
            var s = a.State;
            // Into the war: soldiers trained or moving, demons on the island, mid-turn.
            StepUntil(a, () => (s.War.Trained > 0 || s.War.Purged > 0) && s.Table.Phase == TablePhase.Main && s.Table.TurnTicks > 5, MaxTicks);
            Assert.AreNotEqual(TablePhase.Ended, s.Table.Phase);
            a.DrainEvents();
            string json = SaveCodec.ToJson(s);
            StringAssert.Contains("\"War\":{", json);
            var b = SaveCodec.Load(json, out var model);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.LoadedVersion);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State));
            CollectionAssert.AreEqual(a.Systems.Select(x => x.GetType().Name).Where(n => n != "AutoOpen").ToList(),
                b.Systems.Select(x => x.GetType().Name).ToList());
            var seams = GodBoardModule.Seams(b);
            Assert.IsNotNull(seams.OnBoardChanged);
            Assert.IsNotNull(seams.OnProduced);
            Assert.IsNotNull(seams.Engine);
            for (int i = 0; i < 3000; i++)
            {
                a.Step();
                b.Step();
                var ea = a.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick).ToList();
                var eb = b.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick).ToList();
                CollectionAssert.AreEqual(ea, eb, "events at tick " + a.State.Tick);
            }
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
        }

        [Test]
        public void Save_Version6GodBoard_GetsAWarBuiltFromItsTable()
        {
            var a = GodBoard();
            StepUntil(a, () => a.State.Table.Round >= 3, MaxTicks);
            a.State.War = null;
            string v6 = SaveCodec.ToJson(a.State).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":6");
            var b = SaveCodec.Load(v6, out var model);
            Assert.AreEqual(6, model.LoadedVersion);
            Assert.IsNotNull(b.State.War);
            AssertWarMatchesTable(b.State);
            Assert.IsTrue(b.Systems.Any(x => x is WarSystem));
            Steps(b, 500); // and it plays on
        }

        [Test]
        public void Save_BalanceF1_InfectEveryRoundTrips_AndAVersion8SaveKeepsTheOldBalance()
        {
            var a = GodBoard();
            Assert.AreEqual(3, a.State.Plague.InfectEvery, "new game: every 3rd round");
            Assert.AreEqual(TieRule.NeverWhenTied, a.State.War.TiedLeadersFight);
            a.State.Plague.InfectEvery = 2;
            var b = SaveCodec.Load(SaveCodec.ToJson(a.State), out var model);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.LoadedVersion);
            Assert.AreEqual(2, b.State.Plague.InfectEvery, "the saved field round-trips");
            Assert.AreEqual(TieRule.NeverWhenTied, b.State.War.TiedLeadersFight);

            // A version 8 file has no InfectEvery and the tie rule it was written with: it keeps drawing every round.
            a.State.War.TiedLeadersFight = TieRule.OnlyWhenNotEveryoneTied;
            string json = SaveCodec.ToJson(a.State).Replace("\"Version\":" + SaveCodec.CurrentVersion, "\"Version\":8");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"InfectEvery\":[0-9]+,?", "");
            StringAssert.DoesNotContain("InfectEvery", json);
            var c = SaveCodec.Load(json, out var m8);
            Assert.AreEqual(8, m8.LoadedVersion);
            Assert.AreEqual(1, c.State.Plague.InfectEvery, "old behaviour: infection every round");
            Assert.AreEqual(TieRule.OnlyWhenNotEveryoneTied, c.State.War.TiedLeadersFight, "the saved tie rule is kept");
            Steps(c, 300);
        }

        [Test]
        public void Save_Legacy_HasNoWar()
        {
            var a = WorldSimFactory.CreateNew(Seed);
            var b = SaveCodec.Load(SaveCodec.ToJson(a.State), out _);
            Assert.IsNull(b.State.War);
            Assert.IsFalse(b.Systems.Any(x => x is WarSystem));
        }
    }
}
