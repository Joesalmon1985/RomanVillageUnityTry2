using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>The autonomous prince: announce window, answers, advice in the scores, Explain (PRD_AGES 8.4; package H1-1).</summary>
    public class AutonomyTests
    {
        /// <summary>A new game whose player's people have just handed over (the milestone applied directly).</summary>
        private static WorldSim HandedOver(out List<SimEvent> ev, ulong seed = K.DefaultSeed)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            s.Demons.Tuning.GraceTicks = int.MaxValue;
            var buf = new EventBuffer();
            Assert.IsTrue(AutonomyLogic.Handover(s, 0, buf));
            Assert.IsFalse(AutonomyLogic.Handover(s, 0, buf), "once");
            ev = K.Step(sim, 1);
            return sim;
        }

        [Test]
        public void Surface_GrowsOnly()
        {
            Assert.AreEqual(5, (int)ProposalKind.Upgrade);
            Assert.AreEqual(6, (int)ProposalKind.Town);
            var f = typeof(Faction);
            foreach (var n in new[] { "Towns", "AnnouncedProposalId", "AnnounceExecuteTick", "AutoCasts", "LastAutoCastTick" }) Assert.IsNotNull(f.GetField(n), n);
            foreach (var n in new[] { "AutoPaceTicks", "UpgradeScore", "TownScore" }) Assert.IsNotNull(typeof(FactionTuning).GetField(n), n);
            Assert.IsNotNull(typeof(FactionApi).GetMethod("Announced"));
            Assert.IsNotNull(typeof(FactionApi).GetMethod("IsAutonomous"));
            Assert.IsNotNull(typeof(ScenarioStarts).GetMethod("HistoricStartReached"));
            var t = new FactionTuning();
            Assert.AreEqual((150, 450), (t.AnnounceTicks, t.AutoPaceTicks));
        }

        [Test]
        public void ThePrinceAnnounces_ThenActsAfterTheWindow()
        {
            var sim = HandedOver(out var ev);
            var s = sim.State;
            var ann = ev.OfType<ActionAnnounced>().Single();
            var p = FactionApi.Announced(s, 0);
            Assert.AreEqual(ann.ProposalId, p.Id);
            Assert.AreEqual(FactionApi.Top(s, 0).Id, p.Id, "his top proposal");
            Assert.AreEqual(ann.Tick + 150, ann.ExecuteTick);
            var key = p.Key;
            var before = K.Step(sim, (int)(ann.ExecuteTick - s.Tick - 1));
            Assert.IsEmpty(before.OfType<SpellCast>(), "nothing happens inside the announce window");
            var at = K.Step(sim, 1);
            var cast = at.OfType<SpellCast>().Single();
            Assert.AreEqual((ann.ProposalId, ann.ExecuteTick), (cast.ProposalId, cast.Tick));
            Assert.AreEqual(1, s.Factions.Factions[0].AutoCasts);
            Assert.AreEqual(1, s.Factions.Casts[0]);
            // The next announcement waits for the pace (45 s after his own action).
            var next = K.Step(sim, 460).OfType<ActionAnnounced>().ToList();
            Assert.AreEqual(1, next.Count);
            Assert.AreEqual(cast.Tick + 450, next[0].Tick);
            Assert.AreNotEqual(key, FactionApi.GetProposal(s, next[0].ProposalId).Key);
        }

        [Test]
        public void APlayersCast_SkipsTheWait_AndDoesNotDelayThePrince()
        {
            var sim = HandedOver(out var ev);
            var s = sim.State;
            var ann = ev.OfType<ActionAnnounced>().Single();
            var got = K.Do(sim, new CastSpell { ProposalId = ann.ProposalId });
            Assert.AreEqual(ann.ProposalId, got.OfType<SpellCast>().Single().ProposalId, "cast at once");
            Assert.AreEqual(0, s.Factions.Factions[0].AutoCasts);
            var again = got.Concat(K.Step(sim, 30)).OfType<ActionAnnounced>().ToList();
            Assert.AreEqual(1, again.Count, "he announces the next action straight away: the player's cast is extra");
            Assert.AreNotEqual(ann.ProposalId, again[0].ProposalId);
        }

        [Test]
        public void Veto_DropsTheAnnouncedAction_ForItsCooldown()
        {
            var sim = HandedOver(out var ev);
            var s = sim.State;
            var p = FactionApi.Announced(s, 0);
            string key = p.Key;
            K.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Veto, ProposalId = p.Id });
            Assert.IsNull(FactionApi.GetProposal(s, p.Id));
            var after = K.Step(sim, 599);
            Assert.IsFalse(after.OfType<SpellCast>().Any(c => Proposal.MakeKey(c.Kind, c.BuildingType, c.Plot, c.HexEdge, c.Corner, c.DemonSiteKind, c.DemonSiteId) == key),
                "not carried out while the veto lasts");
            Assert.IsFalse(after.OfType<ActionAnnounced>().Any(x => FactionApi.GetProposal(s, x.ProposalId)?.Key == key), "nor announced again");
        }

        [Test]
        public void Favour_ExecutesAtOnce()
        {
            var sim = HandedOver(out _);
            var s = sim.State;
            var ann = FactionApi.Announced(s, 0);
            var other = FactionApi.Proposals(s, 0).First(x => x.Id != ann.Id && sim.Check(new CastSpell { PlayerId = 0, ProposalId = x.Id }).Ok);
            var got = K.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Favour, ProposalId = other.Id });
            Assert.AreEqual(other.Id, got.OfType<SpellCast>().Single().ProposalId);
            Assert.AreEqual(AdviceKind.Favour, got.OfType<AdviceTaken>().Single().Kind);
        }

        [Test]
        public void PriorityAndFocus_RaiseTheirCandidates_WhileInForce()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var top = FactionApi.Top(s, 0);
            Assert.AreEqual((int)BuildingType.WoodcutterHut, top.BuildingType);
            int score = top.Score;
            K.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.FocusWare, Ware = (int)Ware.Wood });
            K.Step(sim, 20);
            var hut = FactionApi.Proposals(s, 0).Single(p => p.Key == top.Key);
            Assert.AreEqual(score + 30, hut.Score, "focus on wood: +30 to the woodcutter");
            K.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Priority, Priority = PriorityClass.Economy });
            K.Step(sim, 20);
            Assert.AreEqual(score + 70, FactionApi.Proposals(s, 0).Single(p => p.Key == top.Key).Score, "economy first: +40 more");
            StringAssert.Contains("You asked me to focus on wood and to put the economy first.", FactionApi.Explain(s, 0).Why);
            K.Step(sim, 3000);
            Assert.IsEmpty(FactionApi.AdviceInForce(s, 0), "advice lasts 5 minutes");

            Assert.IsTrue(AutonomyLogic.InClass(PriorityClass.Expansion, ProposalKind.Road, -1));
            Assert.IsTrue(AutonomyLogic.InClass(PriorityClass.Military, ProposalKind.Building, (int)BuildingType.WarriorLodge));
            Assert.IsFalse(AutonomyLogic.InClass(PriorityClass.Military, ProposalKind.Building, (int)BuildingType.Sawmill));
            Assert.IsTrue(AutonomyLogic.ServesWare(s, (int)Ware.Planks, ProposalKind.Building, (int)BuildingType.Sawmill, -1), "maker");
            Assert.IsTrue(AutonomyLogic.ServesWare(s, (int)Ware.Planks, ProposalKind.Building, (int)BuildingType.WoodcutterHut, -1), "maker of its input");
            Assert.IsFalse(AutonomyLogic.ServesWare(s, (int)Ware.Planks, ProposalKind.Building, (int)BuildingType.Bakery, -1));
        }

        [Test]
        public void PriorityTrade_IsDeclinedWithAReason()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var ev = K.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Priority, Priority = PriorityClass.Trade });
            Assert.AreEqual("We have nothing to trade with yet.", ev.OfType<AdviceDeclined>().Single().Sentence);
            Assert.IsEmpty(FactionApi.AdviceInForce(sim.State, 0));
            Assert.AreEqual("advice_not_yet", sim.Check(new Advise { PlayerId = 0, Faction = 0, Kind = AdviceKind.AttackTarget }).Reason);
            Assert.AreEqual("not_owner", sim.Check(new Advise { PlayerId = 0, Faction = 1, Kind = AdviceKind.Priority }).Reason);
        }

        [Test]
        public void Explain_SaysWhatHeDoesAndWhy_InPlainEnglish()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var x = FactionApi.Explain(s, 0);
            Assert.AreEqual("I propose to build a Woodcutter's hut at the home camp: cast it when you are ready.", x.Doing);
            Assert.AreEqual(Brain.OpeningReason, x.Why);
            Assert.IsNull(x.Blocked);

            sim = HandedOver(out var ev);
            s = sim.State;
            x = FactionApi.Explain(s, 0);
            StringAssert.StartsWith("I am about to build a Woodcutter's hut at the home camp in 15 s, unless you say otherwise.", x.Doing);
            K.Step(sim, 150);
            x = FactionApi.Explain(s, 0);
            StringAssert.StartsWith("Next I will ", x.Doing);
            StringAssert.EndsWith(", unless you cast it first.", x.Doing);
            Assert.AreEqual(FactionApi.Top(s, 0).Reason, x.Why);
            // Three sites open: he says what holds him up.
            s.Factions.Tuning.MaxOpenSites = 1;
            Assert.AreEqual(1, Brain.OpenSites(s, 0));
            StringAssert.StartsWith("We have 1 sites open; I start nothing new until one is finished.", FactionApi.Explain(s, 0).Blocked);
            s.Factions.GameOver = true;
            Assert.AreEqual("Nothing: the game is over.", FactionApi.Explain(s, 0).Doing);
        }

        [Test]
        public void TheTimeLimit_EndsOnlyThePrehistoricSkirmish()
        {
            foreach (var era in new[] { Era.Prehistoric, Era.Historic })
            {
                var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
                var s = sim.State;
                s.Eras.Current = era;
                s.Factions.Tuning.TimeLimitTicks = 50;
                var ev = K.Step(sim, 100);
                Assert.AreEqual(era == Era.Prehistoric, ev.OfType<GameEnded>().Any(), era.ToString());
            }
        }

        [Test]
        public void InTheHistoricAge_ThePrinceProposesImprovements_AndAFavourStartsOne()
        {
            var sim = UpgradeTests.Historic();
            var s = sim.State;
            var saw = UpgradeTests.BuildActive(sim, BuildingType.Sawmill, 631);
            UpgradeTests.GiveGrade(s, 0, BuildingType.Sawmill);
            s.Factions.Tuning.UpgradeScore = 500; // make it the pitch
            K.Step(sim, 20);
            var up = FactionApi.Top(s, 0);
            Assert.AreEqual(ProposalKind.Upgrade, up.Kind);
            Assert.AreEqual((int)BuildingType.Sawmill, up.BuildingType);
            StringAssert.StartsWith("Improve the Sawmill at the home camp", FactionApi.ProposalTitle(s, up.Id));
            Assert.IsTrue(FactionApi.ProposalTarget(s, up.Id, out _, out _, out _));
            var ev = K.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Favour, ProposalId = up.Id });
            Assert.AreEqual(saw.Id, ev.OfType<UpgradeStarted>().Single().BuildingId);
            Assert.IsTrue(saw.Upgrading);
            K.Step(sim, 20);
            Assert.IsFalse(FactionApi.Proposals(s, 0).Any(p => p.Kind == ProposalKind.Upgrade && p.Plot == saw.Plot), "not proposed again while it is improved");
            Assert.IsNull(K.CheckInvariants(s));

            // Prehistoric: never.
            var pre = WorldSimFactory.CreateNew(K.DefaultSeed);
            pre.State.Factions.Tuning.UpgradeScore = 500;
            K.Step(pre, 3000);
            Assert.IsFalse(FactionApi.Proposals(pre.State, 1).Any(p => p.Kind == ProposalKind.Upgrade || p.Kind == ProposalKind.Town));
        }
    }
}
