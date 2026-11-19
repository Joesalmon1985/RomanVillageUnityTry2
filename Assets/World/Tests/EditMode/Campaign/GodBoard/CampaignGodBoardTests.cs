using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Eras;
using World.Sim.Ledger;
using World.Sim.Plague;
using World.Sim.Table;

namespace World.Sim.Tests
{
    /// <summary>
    /// G2-3: the crystal campaign on the god board (PRD_GODBOARD section 8, CC-1 to CC-9). The god board world is built
    /// here by hand (four peoples on a dealt board, the turn table and the plague) because the factory does not wire the
    /// table and the plague yet (G0-2); the campaign runs the authored pack.
    /// </summary>
    public class CampaignGodBoardTests
    {
        /// <summary>A god board game: four peoples, a dealt board, the table (not opened) and the plague.</summary>
        internal static WorldSim GodGame(ulong seed = 7)
        {
            var sim = WorldSimFactory.CreateNew(seed, GameSetup.GodBoard);
            var s = sim.State;
            TableModule.InitState(s, s.PeopleCount, s.Board.StartCorners);
            TableModule.Register(sim);
            PlagueModule.InitState(s);
            PlagueModule.Register(sim);
            return sim;
        }

        internal static WorldSim Started(ulong seed = 7)
        {
            var sim = GodGame(seed);
            var r = sim.Submit(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) });
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            sim.DrainEvents();
            return sim;
        }

        private static List<SimEvent> Do(WorldSim sim, Command c) => SimTestKit.Do(sim, c);

        private static List<SimEvent> SetCustody(WorldSim sim, Element e, Custody to) =>
            Do(sim, new DevSetCustody { Crystal = e, To = to });

        private static int Home(WorldSim sim) => sim.State.Board.StartCorners[PlayerIds.Human];

        // ------------------------------------------------------------------ CC-1

        [Test]
        public void CC1_CampaignMayStart_FromTheFirstMinute_InPrehistoric()
        {
            var sim = GodGame();
            var s = sim.State;
            Assert.AreEqual(Era.Prehistoric, AgesApi.Current(s));
            Assert.AreNotEqual(PlayMode.DevStart, AgesApi.Mode(s));
            Assert.IsTrue(CampaignRuleset.GodBoard(s));
            Assert.IsTrue(AgesApi.CampaignMayStart(s), "no handover or Historic gate on the god board");
            var r = sim.Submit(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) });
            Assert.IsTrue(r.Ok, r.ToString());
            var ev = SimTestKit.Step(sim, 1);
            Assert.AreEqual(1, ev.OfType<CampaignStarted>().Count());
            Assert.AreEqual(CampaignPhase.Running, s.Campaign.Phase);
        }

        [Test]
        public void CC1_LegacyGame_KeepsTheGate()
        {
            var sim = WorldSimFactory.CreateNew(7);
            Assert.IsFalse(CampaignRuleset.GodBoard(sim.State));
            Assert.IsFalse(AgesApi.CampaignMayStart(sim.State));
            var r = sim.Submit(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(CampaignRefusals.CampaignNotYet, r.Reason);
        }

        // ------------------------------------------------------------------ CC-2

        [Test]
        public void CC2_TheAshClock_RunsFromStart_WhileTheTableIsPaused()
        {
            var sim = Started();
            var s = sim.State;
            Assert.AreEqual(1, s.Campaign.RivalProgress, "one tick since StartCampaign");
            Do(sim, new Sacrifice());
            Do(sim, new LeaveTable());
            Assert.IsTrue(s.Table.Paused);
            long p0 = s.Campaign.RivalProgress;
            SimTestKit.Step(sim, 600);
            Assert.IsTrue(s.Table.Paused);
            Assert.AreEqual(p0 + 600, s.Campaign.RivalProgress, "+1 per world tick while the table is paused");
        }

        // ------------------------------------------------------------------ CC-3

        [Test]
        public void CC3_Restored_CuresItsKind_FirstAltar_Strengthens()
        {
            var sim = Started();
            var s = sim.State;
            var ev = SetCustody(sim, Element.Earth, Custody.Restored);
            Assert.IsTrue(PlagueApi.IsCured(s, (int)Element.Earth));
            Assert.AreEqual(1, ev.OfType<Cured>().Count(x => x.Element == (int)Element.Earth));
            ev = SetCustody(sim, Element.Fire, Custody.AtRivalAltar);
            Assert.IsTrue(PlagueApi.IsStrengthened(s, (int)Element.Fire));
            Assert.AreEqual(1, ev.OfType<Strengthened>().Count(x => x.Element == (int)Element.Fire));
            // Won back and restored: the cure replaces the strengthening.
            SetCustody(sim, Element.Fire, Custody.Carried);
            SetCustody(sim, Element.Fire, Custody.Restored);
            Assert.IsTrue(PlagueApi.IsCured(s, (int)Element.Fire));
            Assert.IsFalse(PlagueApi.IsStrengthened(s, (int)Element.Fire));
            Assert.IsFalse(PlagueApi.IsCured(s, (int)Element.Water));
        }

        [Test]
        public void CC3_NoEffect_WhenThePlagueHasEnded()
        {
            var sim = Started();
            var s = sim.State;
            PlagueRules.End(s, null);
            var ev = SetCustody(sim, Element.Water, Custody.Restored);
            Assert.IsFalse(PlagueApi.IsCured(s, (int)Element.Water));
            ev.AddRange(SetCustody(sim, Element.Air, Custody.AtRivalAltar));
            Assert.IsFalse(PlagueApi.IsStrengthened(s, (int)Element.Air));
            Assert.AreEqual(0, ev.OfType<Cured>().Count() + ev.OfType<Strengthened>().Count());
            Assert.AreEqual(2, ev.OfType<SpireRaised>().Count(), "the reward still comes (CC-8)");
        }

        // ------------------------------------------------------------------ CC-4, CC-5

        [Test]
        public void CC4_Restored_GivesThePlayerAFreeCity_AtItsMostProductiveSettlement_WithASpire()
        {
            var sim = Started();
            var s = sim.State;
            var t = s.Table;
            // A second settlement of the player's with more pips than the first camp, placed through the grant rules.
            int rich = TableGrants.BestSettlementCorner(s, PlayerIds.Human);
            Assert.GreaterOrEqual(rich, 0);
            Assert.IsTrue(TableGrants.GrantSettlement(s, PlayerIds.Human, rich, null));
            int expect = TableRules.CornerPips(s.Board, rich) > TableRules.CornerPips(s.Board, Home(sim)) ? rich
                : TableRules.CornerPips(s.Board, rich) < TableRules.CornerPips(s.Board, Home(sim)) ? Home(sim) : Math.Min(rich, Home(sim));
            Assert.AreEqual(expect, TableGrants.BestCityCorner(s, PlayerIds.Human));
            int hand = TableRules.HandSize(t.Peoples[0].Hand);
            int pts = t.Peoples[0].Points;

            var ev = SetCustody(sim, Element.Earth, Custody.Restored);
            Assert.AreEqual(2, t.CornerLevel[expect], "a city");
            Assert.AreEqual(hand, TableRules.HandSize(t.Peoples[0].Hand), "free");
            Assert.AreEqual(pts + 1, t.Peoples[0].Points);
            var built = ev.OfType<Built>().Single();
            Assert.IsTrue(built.Free);
            Assert.AreEqual(("city", expect, 0), (built.Kind, built.Spot, built.People));
            var spire = ev.OfType<SpireRaised>().Single();
            Assert.AreEqual((Element.Earth, 0, expect, "city"), (spire.Element, spire.People, spire.Corner, spire.Kind));
            Assert.AreEqual(1, s.Campaign.Spires.Count);
            Assert.AreEqual(expect, s.Campaign.Spires[0].Corner);
        }

        [Test]
        public void CC4_NoSettlementToUpgrade_GivesAFreeSettlement_AtTheBestLegalCorner()
        {
            var sim = Started();
            var s = sim.State;
            var t = s.Table;
            SetCustody(sim, Element.Earth, Custody.Restored);
            Assert.AreEqual(2, t.CornerLevel[Home(sim)], "the first camp became a city");
            int best = TableGrants.BestSettlementCorner(s, PlayerIds.Human);
            Assert.AreNotEqual(-1, best);
            Assert.IsNull(TableRules.SettlementRefusal(s, PlayerIds.Human, best, false));
            var ev = SetCustody(sim, Element.Water, Custody.Restored);
            Assert.AreEqual((PlayerIds.Human, 1), (t.CornerOwner[best], t.CornerLevel[best]));
            var spire = ev.OfType<SpireRaised>().Single();
            Assert.AreEqual((best, "settlement"), (spire.Corner, spire.Kind));
            // No legal corner beats it.
            foreach (int c in TableRules.LegalSettlementCorners(s, PlayerIds.Human, false))
                Assert.LessOrEqual(TableRules.CornerPips(s.Board, c), TableRules.CornerPips(s.Board, best) + 0, "corner " + c);
        }

        [Test]
        public void CC4_NoCityPieces_GivesASettlement_NoPiecesAtAll_GivesNothing()
        {
            var sim = Started();
            var s = sim.State;
            var t = s.Table;
            t.Tuning.MaxCities = 0;
            var ev = SetCustody(sim, Element.Earth, Custody.Restored);
            Assert.AreEqual(1, t.CornerLevel[Home(sim)], "no city piece left");
            Assert.AreEqual("settlement", ev.OfType<SpireRaised>().Single().Kind);
            t.Tuning.MaxSettlements = TableRules.CountSettlements(t, 0);
            ev = SetCustody(sim, Element.Water, Custody.Restored);
            Assert.AreEqual(0, ev.OfType<SpireRaised>().Count());
            Assert.AreEqual(0, ev.OfType<Built>().Count());
            Assert.IsTrue(PlagueApi.IsCured(s, (int)Element.Water), "the cure does not depend on the reward");
        }

        [Test]
        public void CC4_FirstAltar_RewardsTheAshClan_OnceOnly()
        {
            var sim = Started();
            var s = sim.State;
            var t = s.Table;
            int ash = s.Board.StartCorners[PlayerIds.AshClan];
            var ev = SetCustody(sim, Element.Earth, Custody.AtRivalAltar);
            Assert.AreEqual(2, t.CornerLevel[ash]);
            var spire = ev.OfType<SpireRaised>().Single();
            Assert.AreEqual((PlayerIds.AshClan, ash), (spire.People, spire.Corner));
            SetCustody(sim, Element.Earth, Custody.Carried);
            ev = SetCustody(sim, Element.Earth, Custody.AtRivalAltar);
            Assert.AreEqual(0, ev.OfType<SpireRaised>().Count(), "only the first time on an altar");
        }

        [Test]
        public void CC4_Grants_RespectTheRules()
        {
            var sim = GodGame();
            var s = sim.State;
            var topo = s.Board.Topology;
            int home = Home(sim);
            Assert.IsFalse(TableGrants.GrantSettlement(s, 0, topo.CornerNeighbours(home)[0], null), "distance rule");
            Assert.IsFalse(TableGrants.GrantSettlement(s, 0, home, null), "occupied");
            Assert.IsFalse(TableGrants.GrantCity(s, 0, s.Board.StartCorners[1], null), "not ours");
            Assert.IsTrue(TableGrants.GrantCity(s, 0, home, null));
            Assert.IsFalse(TableGrants.GrantCity(s, 0, home, null), "already a city");
        }

        [Test]
        public void CC4_AGrantReachingTenPoints_EndsTheAge()
        {
            var sim = GodGame();
            var s = sim.State;
            var t = s.Table;
            var buf = new EventBuffer();
            for (int i = 0; i < 4; i++) Assert.IsTrue(TableGrants.GrantSettlement(s, 0, TableGrants.BestSettlementCorner(s, 0), buf));
            Assert.AreEqual(5, t.Peoples[0].Points);
            for (int i = 0; i < 4; i++) Assert.IsTrue(TableGrants.GrantCity(s, 0, TableGrants.BestCityCorner(s, 0), buf));
            Assert.AreEqual(9, t.Peoples[0].Points);
            Assert.AreEqual(0, buf.Items.OfType<AgeEnded>().Count());
            Assert.AreEqual(-1, TableGrants.BestCityCorner(s, 0), "no city piece left (4)");
            Assert.IsTrue(TableGrants.GrantSettlement(s, 0, TableGrants.BestSettlementCorner(s, 0), buf), "a city gave its settlement piece back");
            Assert.AreEqual(1, buf.Items.OfType<AgeEnded>().Count());
            Assert.AreEqual(TablePhase.Ended, t.Phase);
        }

        // ------------------------------------------------------------------ CC-6

        [Test]
        public void CC6_CrystalKeys_PayNoPoints_OnTheGodBoard()
        {
            var sim = Started();
            var s = sim.State;
            SetCustody(sim, Element.Earth, Custody.Restored);
            SetCustody(sim, Element.Fire, Custody.AtRivalAltar);
            Assert.IsFalse(LedgerApi.IsPaid(s, PlayerIds.Human, "crystal:earth"));
            Assert.IsFalse(LedgerApi.IsPaid(s, PlayerIds.AshClan, "crystal:fire"));
            Assert.AreEqual(0, LedgerApi.Points(s, PlayerIds.Human));
            Assert.AreEqual(0, LedgerApi.Points(s, PlayerIds.AshClan));
        }

        [Test]
        public void CC6_Legacy_StillPays_AndGrantsNothing()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            var ev = SetCustody(sim, Element.Earth, Custody.Restored);
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Human, "crystal:earth"));
            Assert.AreEqual(0, ev.OfType<SpireRaised>().Count());
            Assert.IsNull(s.Table);
        }

        // ------------------------------------------------------------------ CC-7

        [Test]
        public void CC7_PlayerSettlementBurned_TurnsThePartyHome_AndCharges900()
        {
            var sim = Started();
            var s = sim.State;
            var c = s.Campaign;
            Assert.AreEqual(900, c.Definition.Tuning.HomeTroubleCharge);
            // Get the party away from home first.
            SimTestKit.StepUntil(sim, () => c.Party.Mode == PartyMode.Travelling, 3000);
            Assert.AreEqual(PartyMode.Travelling, c.Party.Mode);
            long p0 = c.RivalProgress;
            var buf = new EventBuffer { CurrentTick = s.Tick };
            Assert.IsFalse(CampaignHooks.OnSettlementBurned(s, PlayerIds.Elves, 5, buf), "not ours");
            Assert.AreEqual(p0, c.RivalProgress);
            Assert.IsTrue(CampaignHooks.OnSettlementBurned(s, PlayerIds.Human, 5, buf));
            Assert.AreEqual(p0 + 900, c.RivalProgress);
            Assert.IsFalse(CampaignHooks.OnSettlementBurned(s, PlayerIds.Human, 6, buf), "once per tick");
            Assert.AreEqual(p0 + 900, c.RivalProgress);
            var adv = buf.Items.OfType<RivalAdvanced>().Single();
            Assert.AreEqual((900L, RivalCharges.HomeTrouble), (adv.Amount, adv.Why));
            Assert.AreEqual("home_trouble", buf.Items.OfType<PartyRetreated>().Single().Why);
            Assert.AreEqual("settlement_burned", buf.Items.OfType<HomeTrouble>().Single().Why);
            Assert.AreEqual(RoutePurpose.Retreat, c.Party.Purpose);
            var home = MarkerRef.Surface("home", "fire");
            SimTestKit.StepUntil(sim, () => c.Party.Mode != PartyMode.Travelling, 6000);
            Assert.AreEqual(home, c.Party.At, "walked home");
        }

        [Test]
        public void CC7_OutbreakOnAHomeHex_IsTrouble_ElsewhereIsNot()
        {
            var sim = Started();
            var s = sim.State;
            var c = s.Campaign;
            var home = CampaignHooks.HomeNodes(s);
            Assert.IsNotEmpty(home, "the home camp's hexes carry plague nodes");
            int far = Enumerable.Range(1, PlagueGraphData.NodeCount).First(n => !home.Contains(n));
            long p0 = c.RivalProgress;
            var buf = new EventBuffer { CurrentTick = s.Tick };
            Assert.IsFalse(CampaignHooks.OnOutbreak(s, far, buf));
            Assert.AreEqual(p0, c.RivalProgress);
            Assert.IsTrue(CampaignHooks.OnOutbreak(s, home[0], buf));
            Assert.AreEqual(p0 + 900, c.RivalProgress);
            Assert.AreEqual(home[0], buf.Items.OfType<HomeTrouble>().Single().Node);
        }

        /// <summary>Raises one event at a set tick in a slot before the campaign's (as the war and the table do).</summary>
        private sealed class Raise : ISimSystem
        {
            public long At;
            public Func<SimEvent> Make;
            public void Tick(WorldState s, EventBuffer ev) { if (s.Tick == At) ev.Add(Make()); }
        }

        [Test]
        public void CC7_TheCampaignSlot_ReadsTheWarAndPlagueEvents_OfItsTick()
        {
            var sim = Started();
            var s = sim.State;
            var c = s.Campaign;
            int node = CampaignHooks.HomeNodes(s)[0];
            sim.AddSystem(SystemOrder.Combat + 20, new Raise { At = s.Tick + 5, Make = () => new War.SettlementBurned { People = PlayerIds.Human, Corner = 9 } });
            sim.AddSystem(SystemOrder.Combat + 21, new Raise { At = s.Tick + 50, Make = () => new Plague.Infected { Node = node, Kind = 0, Count = 1, Reason = "spread" } });
            sim.AddSystem(SystemOrder.Combat + 22, new Raise { At = s.Tick + 80, Make = () => new Plague.Infected { Node = node, Kind = 0, Count = 1, Reason = "draw" } });
            var ev = SimTestKit.Step(sim, 100);
            var trouble = ev.OfType<HomeTrouble>().ToList();
            Assert.AreEqual(2, trouble.Count, "a burning and a spread; a plain draw is not an outbreak");
            Assert.AreEqual("settlement_burned", trouble[0].Why);
            Assert.AreEqual("outbreak", trouble[1].Why);
            Assert.AreEqual(2, ev.OfType<RivalAdvanced>().Count(x => x.Why == RivalCharges.HomeTrouble));
        }

        [Test]
        public void CC7_LegacyOverruns_DoNotTouchTheGodBoardCampaign()
        {
            var sim = Started();
            var s = sim.State;
            long p0 = s.Campaign.RivalProgress;
            sim.AddSystem(SystemOrder.Combat + 20, new Raise { At = s.Tick + 3, Make = () => new Eras.CampOverrun { Attacker = 1, Defender = 0 } });
            var ev = SimTestKit.Step(sim, 10);
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count());
            Assert.AreEqual(p0 + 10, s.Campaign.RivalProgress);
        }

        // ------------------------------------------------------------------ CC-8, CC-9

        private static void ToHistoric(WorldSim sim)
        {
            var s = sim.State;
            s.Eras.Current = Era.Historic;
            s.Table.Phase = TablePhase.Ended;
            PlagueRules.End(s, null);
        }

        [Test]
        public void CC8_TheCampaignSurvivesTheSwitchIntoHistoric()
        {
            var sim = Started();
            var s = sim.State;
            ToHistoric(sim);
            long p0 = s.Campaign.RivalProgress;
            SimTestKit.Step(sim, 300);
            Assert.AreEqual(CampaignPhase.Running, s.Campaign.Phase);
            Assert.AreEqual(p0 + 300, s.Campaign.RivalProgress);
            var ev = SetCustody(sim, Element.Earth, Custody.Restored);
            Assert.AreEqual(1, ev.OfType<SpireRaised>().Count(), "a crystal gained in Historic still gives its city");
            Assert.AreEqual(2, s.Table.CornerLevel[Home(sim)]);
            Assert.IsFalse(PlagueApi.IsCured(s, 0), "no plague left to cure (HI-8)");
        }

        [Test]
        public void CC8_TheCampaignCanStartForTheFirstTimeInHistoric()
        {
            var sim = GodGame();
            ToHistoric(sim);
            var r = sim.Submit(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) });
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            Assert.AreEqual(CampaignPhase.Running, sim.State.Campaign.Phase);
        }

        [Test]
        public void CC9_TerminalDefeat_EndsTheCampaign_NotTheGame()
        {
            var sim = Started();
            var s = sim.State;
            var end = typeof(CampaignRuntime).GetMethod("End", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(end);
            var buf = new EventBuffer { CurrentTick = s.Tick };
            end.Invoke(null, new object[] { s, s.Campaign, false, buf });
            var ended = buf.Items.OfType<CampaignEnded>().Single();
            Assert.IsFalse(ended.Won);
            Assert.IsTrue(ended.GameGoesOn);
            Assert.AreEqual(CampaignPhase.Lost, s.Campaign.Phase);
            // The game goes on: the table still plays, and a new expedition may set out later.
            Do(sim, new Sacrifice());
            Assert.AreEqual(TablePhase.Setup, s.Table.Phase);
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) }).Ok);
        }

        [Test]
        public void GodBoardCampaign_SavedStateRoundTrips()
        {
            var sim = Started();
            SetCustody(sim, Element.Earth, Custody.Restored);
            var json = Save.SaveCodec.StateJson(sim.State);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignState>(Newtonsoft.Json.JsonConvert.SerializeObject(sim.State.Campaign));
            Assert.AreEqual(1, back.Spires.Count);
            Assert.AreEqual(sim.State.Campaign.Spires[0].Corner, back.Spires[0].Corner);
            Assert.IsNotEmpty(json);
        }
    }
}
