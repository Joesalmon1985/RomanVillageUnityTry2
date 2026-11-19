using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Factions;
using World.Sim.GodBoard;
using World.Sim.Plague;
using World.Sim.Save;
using World.Sim.Table;
using TableEpidemic = World.Sim.Table.Epidemic;
using PlagueEpidemic = World.Sim.Plague.Epidemic;

namespace World.Sim.Tests
{
    /// <summary>G0-2: the ruleset switch, and the table and the plague wired into a god board game (sim, save, seams).</summary>
    public class GodBoardWiringTests
    {
        private const ulong Seed = 20261119;
        private const int MaxTicks = 60000;
        private const ulong SmokeSeed = 21;

        /// <summary>Event types only the legacy rules raise (dice clock, stock growth, proposals and spells, strength and overrun, demons).</summary>
        private static readonly Type[] LegacyEvents =
        {
            typeof(DiceRolled), typeof(StockAdded), typeof(StockLost), typeof(StockMatured),
            typeof(ProposalAdded), typeof(ProposalsReordered), typeof(ProposalRemoved), typeof(SpellCast),
            typeof(StrengthChanged), typeof(OverrunWarningStarted), typeof(OverrunWarningCancelled), typeof(FactionOverrun), typeof(GameEnded),
            typeof(Demons.DemonPlaced), typeof(Demons.DemonOutbreak), typeof(Demons.DemonsBanished),
        };

        private static readonly string[] LegacySystems = { "DiceSystem", "GrowthSystem", "BrainSystem", "StrengthSystem" };

        private static WorldSim GodBoard(ulong seed = Seed, int humanSeat = PlayerIds.Human)
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

        private static void AssertNoLegacy(IEnumerable<SimEvent> log)
        {
            foreach (var e in log)
                Assert.IsFalse(LegacyEvents.Contains(e.GetType()), "a legacy system ran: " + e.GetType().Name + " at tick " + e.Tick);
        }

        // ---- The switch ----

        [Test]
        public void Legacy_IsUnchanged_NoTableNoPlague()
        {
            var sim = WorldSimFactory.CreateNew(Seed);
            Assert.AreEqual(Ruleset.Legacy, sim.State.Ruleset);
            Assert.IsNull(sim.State.Table);
            Assert.IsNull(sim.State.Plague);
            Assert.IsNull(GodBoardModule.Seams(sim));
            Assert.IsTrue(sim.HasHandler(typeof(CastSpell)));
            Assert.IsTrue(sim.HasHandler(typeof(Demons.Banish)));
            Assert.IsFalse(sim.HasHandler(typeof(Sacrifice)));
            var names = sim.Systems.Select(x => x.GetType().Name).ToList();
            foreach (var n in LegacySystems) CollectionAssert.Contains(names, n);
            Assert.Greater(sim.State.Factions.Proposals.Count, 0, "the opening proposal");
        }

        [Test]
        public void GodBoard_RecordsRuleset_AndStartsTableAndPlague()
        {
            var sim = GodBoard();
            var s = sim.State;
            Assert.AreEqual(Ruleset.GodBoard, s.Ruleset);
            Assert.AreEqual(4, s.PeopleCount);
            Assert.AreEqual(4, s.Peoples.Count);
            var t = s.Table;
            Assert.IsNotNull(t);
            Assert.AreEqual(4, t.PeopleCount);
            Assert.AreEqual(PlayerIds.Human, t.HumanSeat);
            Assert.AreEqual(TablePhase.NotOpened, t.Phase);
            for (int p = 0; p < 4; p++)
            {
                Assert.AreEqual(s.Board.StartCorners[p], t.Peoples[p].StartCorner);
                Assert.AreEqual(p, t.CornerOwner[s.Board.StartCorners[p]]);
            }
            Assert.AreEqual(s.Board.Ports.Count, t.Ports.Count);
            Assert.IsNotNull(s.Plague);
            Assert.AreEqual(PlagueGraphData.NodeCount, s.Plague.Deck.Count);
            Assert.AreEqual(0, PlagueApi.TotalDemons(s));
        }

        [Test]
        public void GodBoard_LegacySystemsAndCommandsAreOff()
        {
            var sim = GodBoard();
            var names = sim.Systems.Select(x => x.GetType().Name).ToList();
            foreach (var n in LegacySystems) CollectionAssert.DoesNotContain(names, n);
            CollectionAssert.Contains(names, "TableEngine");
            Assert.IsFalse(sim.HasHandler(typeof(CastSpell)), "no spells for building");
            Assert.IsFalse(sim.HasHandler(typeof(DeclineProposal)));
            Assert.IsFalse(sim.HasHandler(typeof(Demons.Banish)), "no legacy demons");
            Assert.IsTrue(sim.HasHandler(typeof(Plague.Banish)));
            Assert.IsTrue(sim.HasHandler(typeof(Sacrifice)));
            Assert.AreEqual(0, sim.State.Factions.Proposals.Count, "no opening proposal");
            Assert.IsFalse(sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = 1 }).Ok);
        }

        [Test]
        public void GodBoard_BeforeTheSacrifice_NoRollsNoTurns_NoLegacyEvents()
        {
            var sim = GodBoard();
            var log = StepUntil(sim, () => false, 3000); // 5 minutes: the legacy rules would have rolled 30 times
            AssertNoLegacy(log);
            Assert.AreEqual(0, sim.State.Economy.Dice.RollCount);
            Assert.AreEqual(TablePhase.NotOpened, sim.State.Table.Phase);
            Assert.IsFalse(log.OfType<TurnStarted>().Any());
            Assert.IsFalse(sim.State.Factions.GameOver, "no 20-minute limit and no overrun");
            Assert.AreEqual(0, sim.State.Plague.Rounds);
        }

        [Test]
        public void GodBoard_Sacrifice_OpensTheSetupRound()
        {
            var sim = GodBoard();
            Assert.IsTrue(sim.Submit(new Sacrifice { PlayerId = PlayerIds.Human }).Ok);
            sim.Step();
            var evs = sim.DrainEvents();
            Assert.IsTrue(evs.OfType<TableOpened>().Any());
            Assert.AreEqual(TablePhase.Setup, sim.State.Table.Phase);
        }

        // ---- Ports ----

        [Test]
        public void Ports_MapToTheSettlementCornersOfTheirMeadowHex()
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var sim = GodBoard(seed);
                var s = sim.State;
                var topo = s.Board.Topology;
                Assert.AreEqual(9, s.Table.Ports.Count);
                for (int i = 0; i < s.Table.Ports.Count; i++)
                {
                    var tp = s.Table.Ports[i];
                    var bp = s.Board.Ports[i];
                    Assert.AreEqual(bp.Hex, tp.Hex);
                    Assert.AreEqual(Terrain.Meadow, s.Board.HexTerrain[bp.Hex]);
                    var expected = topo.HexCorners(bp.Hex).Where(c => PlotLayout.IsSettlementCorner(topo, c)).OrderBy(c => c).ToArray();
                    Assert.Greater(expected.Length, 0, "port hex " + bp.Hex + " has a settlement corner");
                    CollectionAssert.AreEqual(expected, tp.Corners);
                    CollectionAssert.AreEqual(bp.Corners, tp.Corners, "same as the board's own list");
                    Assert.AreEqual(expected[0], tp.CornerA);
                    Assert.AreEqual(bp.Good.HasValue ? (int)bp.Good.Value : -1, tp.Good);
                    Assert.AreEqual(bp.Ratio, tp.Good < 0 ? s.Table.Tuning.GenericPortRate : s.Table.Tuning.GoodPortRate);
                }
            }
        }

        [Test]
        public void Ports_EveryCornerOfThePortGivesItsRate()
        {
            var sim = GodBoard();
            var s = sim.State;
            var t = s.Table;
            int checkedCorners = 0;
            foreach (var port in t.Ports)
            {
                var good = port.Good < 0 ? Ware.Wood : (Ware)port.Good;
                foreach (int c in port.Corners)
                {
                    if (t.CornerOwner[c] >= 0) continue;
                    Assert.AreEqual(t.Tuning.BankRate, TableRules.TradeRate(s, 0, good));
                    t.CornerOwner[c] = 0;
                    t.CornerLevel[c] = 1;
                    Assert.AreEqual(port.Good < 0 ? t.Tuning.GenericPortRate : t.Tuning.GoodPortRate, TableRules.TradeRate(s, 0, good),
                        "corner " + c + " of the port on hex " + port.Hex);
                    t.CornerOwner[c] = -1;
                    t.CornerLevel[c] = 0;
                    checkedCorners++;
                }
            }
            Assert.Greater(checkedCorners, 9);
        }

        // ---- Seams ----

        [Test]
        public void Seams_HexBlocked_IsThePlague()
        {
            var sim = GodBoard();
            var s = sim.State;
            var seams = GodBoardModule.Seams(sim);
            Assert.IsNotNull(seams);
            int node = 1;
            int hex = s.Board.Topology.HexIndex(new HexCoord(PlagueGraphData.HexQ[0], PlagueGraphData.HexR[0]));
            Assert.IsFalse(seams.HexBlocked(s, hex));
            s.Plague.Demons[node - 1] = 1;
            s.Plague.KindCount[(node - 1) * 4 + PlagueApi.ElementOf(node)] = 1;
            Assert.IsTrue(seams.HexBlocked(s, hex));
            Assert.IsTrue(PlagueApi.HexBlocked(s, hex));
        }

        [Test]
        public void AiOnlyGame_PlagueFollowsTheTable_RoundsSevensAndBlockedHexes()
        {
            var sim = GodBoard(humanSeat: -1);
            var s = sim.State;
            var log = new List<SimEvent>();
            for (int i = 0; i < MaxTicks && s.Table.Phase != TablePhase.Ended; i++)
            {
                sim.Step();
                var evs = sim.DrainEvents();
                log.AddRange(evs);
                // PL-5: one infection draw after every finished round (while the island stands).
                if (s.Table.Round >= 1 && !s.Plague.IslandFell && s.Table.Phase != TablePhase.Ended)
                    Assert.AreEqual(s.Table.Round - 1, s.Plague.Rounds, "tick " + s.Tick);
                // TU-2, PL-9: a blocked hex gives nothing; a produced hex had no demons.
                foreach (var p in evs.OfType<Produced>())
                {
                    foreach (int h in p.Hexes) Assert.IsFalse(PlagueApi.HexBlocked(s, h), "hex " + h + " produced with demons");
                    foreach (int h in p.Blocked) Assert.IsTrue(PlagueApi.HexBlocked(s, h));
                }
            }
            int sevens = log.OfType<TableEpidemic>().Count();
            int lateSevens = log.OfType<TableEpidemic>().Count(e => e.Round > PlagueRules.GraceRounds + 1);
            Assert.Greater(sevens, 0);
            Assert.Greater(log.OfType<Infected>().Count(), 0);
            if (lateSevens > 0 && !s.Plague.IslandFell) Assert.Greater(log.OfType<PlagueEpidemic>().Count(), 0, "a 7 after the grace rounds is an epidemic");
            Assert.LessOrEqual(log.OfType<PlagueEpidemic>().Count(), sevens);
            TestContext.Out.WriteLine("rounds " + s.Table.Round + ", sevens " + sevens + ", epidemics " + s.Plague.Epidemics
                                      + ", outbreaks " + s.Plague.Outbreaks + ", blocked productions " + log.OfType<Produced>().Sum(p => p.Blocked.Length));
        }

        // ---- The smoke test (G0-2 point 4) ----

        private static (WorldSim sim, List<SimEvent> log) PlayToAgeEnded(ulong seed)
        {
            var sim = GodBoard(seed, humanSeat: -1);
            var log = StepUntil(sim, () => sim.State.Table.Phase == TablePhase.Ended, MaxTicks);
            return (sim, log);
        }

        [Test]
        public void Smoke_AiOnlyGodBoard_PlaysToAgeEnded_Deterministically_WithThePlague_AndNoLegacySystem()
        {
            // With nobody banishing (no war, no player) the island falls in 39 of seeds 1-40; seed 21 is one where it
            // stands, so the age ends with the plague still live (see Docs/progress/G0-2.md).
            var (a, logA) = PlayToAgeEnded(SmokeSeed);
            var (b, logB) = PlayToAgeEnded(SmokeSeed);
            var s = a.State;
            Assert.AreEqual(TablePhase.Ended, s.Table.Phase, "the age ended");
            var ended = logA.OfType<AgeEnded>().Single();
            Assert.AreEqual(s.Table.Winner, ended.Winner);
            Assert.GreaterOrEqual(TableRules.Points(s, ended.Winner), 10);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State), "same seed, same game");
            Assert.AreEqual(logA.Count, logB.Count);

            // The plague ran, and ended with the age (HI-8).
            Assert.Greater(logA.OfType<Infected>().Count(), 0, "demons were placed");
            Assert.IsFalse(s.Plague.IslandFell, "pick a seed where the island stands");
            Assert.IsTrue(s.Plague.Ended);
            Assert.AreEqual(0, PlagueApi.TotalDemons(s));
            Assert.AreEqual(1, logA.OfType<PlagueEnded>().Count());
            // HI-1: the Historic chapter is ready for the step (the step itself is G1-8's).
            CollectionAssert.Contains(s.Eras.ReadyChapters, Eras.ChapterIds.Historic);
            Assert.IsTrue(logA.OfType<Eras.ChapterReady>().Any(e => e.Chapter == Eras.ChapterIds.Historic));

            // No legacy system ran.
            AssertNoLegacy(logA);
            Assert.AreEqual(0, s.Economy.Dice.RollCount);
            Assert.AreEqual(0, s.Factions.Proposals.Count);
            Assert.IsFalse(s.Factions.GameOver);
            Assert.IsFalse(s.Factions.Warning.Active);
            foreach (int d in s.Demons.Corner) Assert.AreEqual(0, d);
            foreach (int d in s.Demons.Edge) Assert.AreEqual(0, d);

            // After the end nothing on the table moves and the plague stays ended.
            var after = StepUntil(a, () => false, 500);
            Assert.IsFalse(after.OfType<TurnStarted>().Any());
            Assert.IsFalse(after.OfType<Infected>().Any());
            TestContext.Out.WriteLine("winner " + ended.Winner + " with " + ended.Points + " in round " + s.Table.Round + " at tick " + ended.Tick
                                      + "; epidemics " + s.Plague.Epidemics + ", outbreaks " + s.Plague.Outbreaks + ", plague rounds " + s.Plague.Rounds);
        }

        // ---- Save ----

        [Test]
        public void Save_GodBoardMidTurnMidPlague_LoadsAndContinuesIdentically()
        {
            var a = GodBoard(humanSeat: -1);
            var s = a.State;
            // Play into the plague: past the grace rounds with demons on the island, in the middle of a turn.
            StepUntil(a, () => s.Plague.Rounds >= PlagueRules.GraceRounds + 1 && PlagueApi.TotalDemons(s) > 0
                               && s.Table.Phase == TablePhase.Main && s.Table.TurnTicks > 5, MaxTicks);
            Assert.AreEqual(TablePhase.Main, s.Table.Phase, "mid-turn");
            Assert.Greater(PlagueApi.TotalDemons(s), 0, "mid-plague");
            a.DrainEvents();

            string json = SaveCodec.ToJson(s);
            StringAssert.Contains("\"Ruleset\":1", json);
            StringAssert.Contains("\"Table\":{", json);
            StringAssert.Contains("\"Plague\":{", json);
            var b = SaveCodec.Load(json, out var model);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.LoadedVersion);
            Assert.AreEqual(Ruleset.GodBoard, b.State.Ruleset);
            Assert.AreEqual(4, b.State.Peoples.Count);
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(b.State));

            // The seams are wired again on load.
            var seams = GodBoardModule.Seams(b);
            Assert.IsNotNull(seams);
            Assert.IsNotNull(seams.OnEpidemic);
            Assert.IsNotNull(seams.OnRoundEnded);
            Assert.IsNotNull(seams.OnAgeEnded);
            // Same systems (the AI-only table's one-shot AutoOpen is only added while the table is not yet open).
            CollectionAssert.AreEqual(a.Systems.Select(x => x.GetType().Name).Where(n => n != "AutoOpen").ToList(),
                b.Systems.Select(x => x.GetType().Name).ToList());

            for (int i = 0; i < 1500; i++)
            {
                a.Step();
                b.Step();
                var ea = a.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick).ToList();
                var eb = b.DrainEvents().Select(e => e.GetType().Name + "@" + e.Tick).ToList();
                CollectionAssert.AreEqual(ea, eb, "events at tick " + a.State.Tick);
            }
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State));
            Assert.Greater(b.State.Plague.Rounds, PlagueRules.GraceRounds + 1, "the plague moved on after the load");
        }

        [Test]
        public void Save_Version5_LoadsAsLegacy()
        {
            var a = WorldSimFactory.CreateNew(Seed);
            a.Step(300);
            var root = JObject.Parse(SaveCodec.ToJson(a.State));
            root["Version"] = 5;
            var st = (JObject)root["State"];
            foreach (var n in new[] { "Ruleset", "Table", "Plague" }) st.Remove(n);
            var b = SaveCodec.Load(root.ToString(Newtonsoft.Json.Formatting.None), out var model);
            Assert.AreEqual(5, model.LoadedVersion);
            Assert.AreEqual(SaveCodec.CurrentVersion, model.Version);
            Assert.AreEqual(Ruleset.Legacy, b.State.Ruleset);
            Assert.IsNull(b.State.Table);
            Assert.IsNull(b.State.Plague);
            Assert.IsTrue(b.HasHandler(typeof(CastSpell)));
            Assert.IsTrue(b.HasHandler(typeof(Demons.Banish)));
            a.Step(600);
            b.Step(600);
            Assert.AreEqual(SaveCodec.StateJson(a.State), SaveCodec.StateJson(b.State), "a v5 game plays on identically");
        }

        [Test]
        public void Save_Legacy_RoundTrips_WithoutTableOrPlague()
        {
            var a = WorldSimFactory.CreateNew(Seed);
            a.Step(200);
            var b = SaveCodec.Load(SaveCodec.ToJson(a.State), out _);
            Assert.AreEqual(Ruleset.Legacy, b.State.Ruleset);
            Assert.IsNull(b.State.Table);
            Assert.IsNull(GodBoardModule.Seams(b));
        }
    }
}
