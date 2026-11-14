using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using World.Sim.Ledger;
using World.Sim.Save;
using static World.Sim.Tests.CampaignRunKit;

namespace World.Sim.Tests
{
    /// <summary>
    /// The seams between the two simulation lanes, closed at sim-H1-cry-S1 (the crystal campaign on top of the ages' H1):
    /// the save registers the campaign, the start gate, the Kilnheart's boon, the restored ending readies the Modern
    /// chapter, the prince builds alone while the player follows Mira, a real overrun reaches the campaign in its tick,
    /// and crystal points go through the ledger.
    /// </summary>
    public class CampaignSeamTests
    {
        /// <summary>dev.historic, then the campaign from a pack (the Slice 1 test start of PRD_CRYSTALS 3.9).</summary>
        public static WorldSim HistoricWith(string pack, ulong seed, System.Action<CampaignDefinition> edit = null)
        {
            var sim = ScenarioStarts.Create(ScenarioStarts.DevHistoric, seed);
            var d = PackFiles.Load(pack);
            edit?.Invoke(d);
            var r = sim.Submit(new StartCampaign { Definition = d });
            Assert.IsTrue(r.Ok, r.ToString());
            sim.Step();
            sim.DrainEvents();
            return sim;
        }

        [Test]
        public void SaveCodec_CreateSim_RegistersTheCampaign_Idempotently()
        {
            var sim = CampaignSamples.Started(3);
            var loaded = SaveCodec.Load(SaveCodec.ToJson(sim.State), out _);
            foreach (var t in CampaignModule.CommandTypes) Assert.IsTrue(loaded.HasHandler(t), t.Name);
            Assert.AreEqual(1, loaded.Systems.Count(x => x is CampaignSystem));
            int n = loaded.Systems.Count;
            CampaignModule.Register(loaded);
            Assert.AreEqual(n, loaded.Systems.Count, "Register stays idempotent");
            Assert.AreEqual(WorldSimFactory.CreateNew(3).Systems.Count, n, "a new game and a loaded save have the same systems");
            // The loaded campaign runs: the clock goes on.
            long p = loaded.State.Campaign.RivalProgress;
            loaded.Step(10);
            Assert.AreEqual(p + 10, loaded.State.Campaign.RivalProgress);
        }

        [Test]
        public void StartGate_StoryGameNeedsHandoverAndHistoric_DevStartDoesNot()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var sim = WorldSimFactory.CreateNew(5);
            var s = sim.State;
            Assert.AreEqual("campaign_not_yet", sim.Check(new StartCampaign { Definition = d }).Reason, "a new story game");
            Assert.AreEqual("The expedition waits until the prince leads the camp and the Historic age begins.",
                sim.Check(new StartCampaign { Definition = d }).Message);
            Assert.AreEqual("invalid_definition", sim.Check(new StartCampaign()).Reason, "the definition is checked first");

            Assert.IsTrue(AutonomyLogic.Handover(s, PlayerIds.Human, null));
            Assert.AreEqual("campaign_not_yet", sim.Check(new StartCampaign { Definition = d }).Reason, "handover alone is not enough");
            Assert.IsTrue(SimTestKit.Do(sim, new AdvanceChapter { To = ChapterIds.Historic }).OfType<ChapterAdvanced>().Any(), "Show me");
            Assert.IsTrue(AgesApi.CampaignMayStart(s));
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = d }).Ok, "after the handover and the Historic step");
            sim.Step();
            Assert.IsTrue(CampaignApi.IsRunning(s));
            Assert.AreEqual(PlayMode.Campaign, AgesApi.Mode(s));

            var dev = ScenarioStarts.Create(ScenarioStarts.DevHistoric, 5);
            Assert.AreEqual(PlayMode.DevStart, AgesApi.Mode(dev.State));
            Assert.IsTrue(dev.Check(new StartCampaign { Definition = PackFiles.Load(PackFiles.Authored) }).Ok, "dev.historic + StartCampaign");

            var bare = CampaignSamples.DevGame(5);
            Assert.IsTrue(bare.Check(new StartCampaign { Definition = d }).Ok, "any developer start");
        }

        [Test]
        public void BeginAgain_AfterAnEnding_IsNotGated()
        {
            var sim = CampaignSamples.Started(4);
            foreach (var k in CampaignSamples.CompanionIds) SimTestKit.Do(sim, new DevSetCompanion { Id = k, Alive = false });
            sim.State.Campaign.Phase = CampaignPhase.Lost;   // as after the terminal defeat
            sim.State.Eras.Mode = PlayMode.Campaign;
            Assert.IsTrue(sim.Check(new StartCampaign { Definition = CampaignSamples.Skeleton() }).Ok, "Begin again");
        }

        [Test]
        public void Kilnheart_UsesCycleBonusPermille_FromTheSavedBase_NeverStacks()
        {
            var sim = CampaignSamples.DevGame(6);
            var tu = sim.State.Economy.Tuning;
            tu.CycleBonusPermille[(int)BuildingType.Kiln] = 100;   // another source's value before the campaign
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = CampaignSamples.Skeleton() }).Ok);
            sim.Step();
            int kiln = (int)BuildingType.Kiln, smoke = (int)BuildingType.Smokehouse;
            var before = (int[])tu.CycleBonusPermille.Clone();
            Assert.AreEqual(100, before[kiln]);
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Restored });
            Assert.AreEqual(100 + 166, tu.CycleBonusPermille[kiln], "20% faster: 1000 * 20 / 120 permille off the cycle");
            Assert.AreEqual(166, tu.CycleBonusPermille[smoke]);
            for (int t = 0; t < tu.CycleBonusPermille.Length; t++)
                if (t != kiln && t != smoke) Assert.AreEqual(before[t], tu.CycleBonusPermille[t], "only the wood-burning workshops");
            // The cycle is 100/120 as long (within a tick of rounding) on top of the base.
            var def = BuildingDefs.Get(BuildingType.Smokehouse);
            int plain = GradeRules(tu, def, 0);
            Assert.AreEqual(plain * (1000 - 166) / 1000, GradeRules(tu, def, 166));
            // Other boons and changes recompute from the base: never stacked.
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Restored });
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Water, To = Custody.AtRivalAltar });
            Assert.AreEqual(266, tu.CycleBonusPermille[kiln]);
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Carried });
            CollectionAssert.AreEqual(before, tu.CycleBonusPermille, "removed by recomputing from the base");
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Restored });
            Assert.AreEqual(266, tu.CycleBonusPermille[kiln]);
            // Saved and loaded: the base is in the save.
            var loaded = SaveLoad(sim);
            CollectionAssert.AreEqual(sim.State.Campaign.BaseCycleBonusPermille, loaded.State.Campaign.BaseCycleBonusPermille);
            Assert.AreEqual(266, loaded.State.Economy.Tuning.CycleBonusPermille[kiln]);
        }

        private static int GradeRules(EconomyTuning tu, BuildingDef def, int bonus)
        {
            var copy = new EconomyTuning { CycleBonusPermille = new int[BuildingDefs.Count] };
            copy.CycleBonusPermille[(int)def.Type] = bonus;
            return GradeLogic.CycleTicks(copy, def, Grade.Prehistoric);
        }

        /// <summary>Skeleton pack on dev.historic, all four collected and three restored, at the last tower's finale.</summary>
        private static WorldSim AtFinale(ulong seed)
        {
            var sim = HistoricWith(PackFiles.Skeleton, seed, d => d.Tuning.LeadPatience = int.MaxValue);
            Quiet(sim);
            Follow(sim, "earth_site");
            Follow(sim, "fire_site");
            Follow(sim, "water_site");
            Follow(sim, "air_cairn");
            Do(sim, new AdviseMira { ChoiceId = "air_wind", Answer = 1 });
            Follow(sim, "air_site");
            foreach (var e in new[] { Element.Earth, Element.Fire, Element.Water })
            {
                Follow(sim, e.ToString().ToLowerInvariant() + "_tower");
                Do(sim, new ConfirmRestore { Crystal = e });
                Until(sim, () => Held(sim, e) == Custody.Restored, 300, "restored");
            }
            Follow(sim, "air_tower");
            Assert.AreEqual(PartyMode.AtThreshold, Mode(sim));
            return sim;
        }

        [Test]
        public void RestoredEnding_RecordsHistory_ReadiesModern_AdvanceRefusedCleanly_FourPointsNoUnlock()
        {
            var sim = AtFinale(8);
            var s = sim.State;
            Assert.IsNull(s.History.CampaignRecord);
            var a = Do(sim, new CommitEncounter { EncounterId = "finale" }).OfType<EncounterCommitted>().Single();
            var ev = Do(sim, new ResolveEncounter { AttemptId = a.AttemptId, Result = DuelResult.Victory });
            var ended = ev.OfType<CampaignEnded>().Single();
            Assert.IsTrue(ended.Won);
            var ready = ev.OfType<ChapterReady>().Single();
            Assert.AreEqual(ChapterIds.ModernHarbour, ready.Chapter);
            Assert.AreEqual(ended.Tick, ready.Tick, "in the same tick");
            Assert.IsNotNull(s.History.CampaignRecord);
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(CampaignApi.Record(s)), Newtonsoft.Json.JsonConvert.SerializeObject(s.History.CampaignRecord));
            CollectionAssert.AreEqual(new[] { Element.Earth, Element.Fire, Element.Water, Element.Air }, s.History.CampaignRecord.RestoredOrder);
            CollectionAssert.Contains(AgesApi.ReadyChapters(s), ChapterIds.ModernHarbour);
            Assert.IsFalse(AgesApi.IsReady(s, ChapterIds.ModernHarbour), "ready, but not built in this build");
            var refused = sim.Check(new AdvanceChapter { To = ChapterIds.ModernHarbour });
            Assert.AreEqual("chapter_not_built", refused.Reason);
            Assert.AreEqual("That time is not written yet.", refused.Message);
            Assert.AreEqual(Era.Historic, AgesApi.Current(s));
            // The record survives a save; nothing is raised twice.
            var loaded = SaveLoad(sim);
            Assert.IsNotNull(loaded.State.History.CampaignRecord);
            Assert.AreEqual(0, SimTestKit.Step(loaded, 100).OfType<ChapterReady>().Count());
            // Points: 4 to the player, none to the Ash Clan (nothing reached an altar); the roster stays locked.
            Assert.AreEqual(4, LedgerApi.Points(s, PlayerIds.Human));
            Assert.AreEqual(0, LedgerApi.Points(s, PlayerIds.Rival));
            Assert.IsFalse(LedgerApi.RosterUnlocked(s, PlayerIds.Human));
            Assert.IsFalse(LedgerApi.RosterUnlocked(s, PlayerIds.Rival));
        }

        [Test]
        public void TerminalDefeat_DoesNotReadyModern()
        {
            var sim = AtFinale(9);
            foreach (var k in CampaignSamples.CompanionIds) Do(sim, new DevSetCompanion { Id = k, Alive = false });
            var a = Do(sim, new CommitEncounter { EncounterId = "finale" }).OfType<EncounterCommitted>().Single();
            var ev = Do(sim, new ResolveEncounter { AttemptId = a.AttemptId, Result = DuelResult.Defeat });
            Assert.IsFalse(ev.OfType<CampaignEnded>().Single().Won);
            Assert.AreEqual(0, ev.OfType<ChapterReady>().Count());
            Assert.IsNull(sim.State.History.CampaignRecord);
        }

        [Test]
        public void ThePrinceBuildsAlone_WhileThePlayerFollowsMira()
        {
            var sim = HistoricWith(PackFiles.Authored, 3, d => d.Tuning.LeadPatience = int.MaxValue);
            var s = sim.State;
            var me = s.Factions.Factions[PlayerIds.Human];
            Assert.IsTrue(FactionApi.IsAutonomous(s, PlayerIds.Human));
            int autoBefore = me.AutoCasts, castsBefore = s.Factions.Casts[PlayerIds.Human];
            long t0 = s.Tick;
            var all = new List<SimEvent>();
            all.AddRange(Follow(sim, "earth_stone"));
            all.AddRange(Follow(sim, "earth_marks"));
            all.AddRange(Follow(sim, "earth_root"));
            all.AddRange(Follow(sim, "earth_tower"));
            all.AddRange(Do(sim, new ConfirmRestore { Crystal = Element.Earth }));
            all.AddRange(Until(sim, () => Held(sim, Element.Earth) == Custody.Restored, 300, "restored"));
            Assert.Greater(s.Tick - t0, 900, "the whole Earth route, following Mira");
            Assert.Greater(me.AutoCasts, autoBefore, "the prince cast his own proposals");
            Assert.AreEqual(me.AutoCasts - autoBefore, s.Factions.Casts[PlayerIds.Human] - castsBefore, "every cast of the player's people was the prince's");
            Assert.Greater(all.OfType<SpellCast>().Count(e => e.Faction == PlayerIds.Human), 0);
            Assert.Greater(all.OfType<ActionAnnounced>().Count(e => e.Faction == PlayerIds.Human), 0);
            Assert.IsNull(SimTestKit.CheckInvariants(s));
        }

        private static void ForceOverrun(WorldSim sim, int attacker)
        {
            var f = sim.State.Factions;
            f.Tuning.OverrunMargin = -1000000;   // the warning cannot be cancelled by strength
            f.Factions[0].SetbackUntilTick = f.Factions[1].SetbackUntilTick = -1;
            f.Warning.Active = true;
            f.Warning.Attacker = attacker;
            f.Warning.Defender = 1 - attacker;
            f.Warning.StartTick = sim.State.Tick;
            f.Warning.EndTick = sim.State.Tick + 1;
        }

        [Test]
        public void RealOverrun_OfYourCamp_ReachesTheCampaignInItsTick_ChargedOnce()
        {
            var sim = HistoricWith(PackFiles.Authored, 4);
            var s = sim.State;
            var c = s.Campaign;
            Follow(sim, "earth_stone");
            int margin = s.Factions.Tuning.OverrunMargin;
            ForceOverrun(sim, PlayerIds.Rival);
            long p = c.RivalProgress;
            sim.Step();
            var ev = sim.DrainEvents().ToList();
            s.Factions.Tuning.OverrunMargin = margin;
            var co = ev.OfType<CampOverrun>().Single();
            Assert.AreEqual((PlayerIds.Rival, PlayerIds.Human), (co.Attacker, co.Defender));
            Assert.AreEqual(CampOverrunKind.CampLost, co.Kind, "dev.historic: a camp other than the home is taken");
            var charge = ev.OfType<RivalAdvanced>().Single(x => x.Why == RivalCharges.CampOverrun);
            Assert.AreEqual(co.Tick, charge.Tick, "the same tick");
            Assert.AreEqual(1800, charge.Amount);
            Assert.AreEqual(p + 1 + 1800, c.RivalProgress);
            Assert.AreEqual("camp_overrun", ev.OfType<PartyRetreated>().Single().Why);
            Assert.IsFalse(s.Factions.GameOver);
            Assert.AreEqual(6, CampaignApi.Companions(s).Count(k => k.Alive), "nobody dies");
            // Once: neither later ticks nor a save and load apply it again.
            var later = SimTestKit.Step(sim, 200);
            Assert.AreEqual(0, later.OfType<RivalAdvanced>().Count(x => x.Why == RivalCharges.CampOverrun));
            var loaded = SaveLoad(sim);
            Assert.AreEqual(0, SimTestKit.Step(loaded, 50).OfType<RivalAdvanced>().Count(x => x.Why == RivalCharges.CampOverrun));
        }

        [Test]
        public void RealOverrun_OfAnAshCamp_PausesTheExpedition()
        {
            var sim = HistoricWith(PackFiles.Authored, 4);
            var s = sim.State;
            var c = s.Campaign;
            sim.Step(100);
            int margin = s.Factions.Tuning.OverrunMargin;
            ForceOverrun(sim, PlayerIds.Human);
            long stepProgress = c.Rival.StepProgress;
            sim.Step();
            var ev = sim.DrainEvents().ToList();
            s.Factions.Tuning.OverrunMargin = margin;
            var co = ev.OfType<CampOverrun>().Single();
            Assert.AreEqual((PlayerIds.Human, PlayerIds.Rival, CampOverrunKind.CampLost), (co.Attacker, co.Defender, co.Kind), "an Ash camp lost");
            var paused = ev.OfType<RivalPaused>().Single();
            Assert.AreEqual(co.Tick, paused.Tick);
            Assert.AreEqual(RivalPhase.Paused, c.Rival.Phase);
            Assert.AreEqual(0, ev.OfType<RivalAdvanced>().Count());
            sim.Step(850);
            Assert.AreEqual(stepProgress, c.Rival.StepProgress, "the expedition stands still for 900 ticks of progress");
            sim.Step(100);
            Assert.AreEqual(RivalPhase.Marching, c.Rival.Phase);
            Assert.Greater(c.Rival.StepProgress, stepProgress);
        }

        [Test]
        public void CrystalPoints_ThroughTheLedger_OnePerRestoration_OnePerFirstAltar()
        {
            var sim = HistoricWith(PackFiles.Authored, 2, d => d.Tuning.LeadPatience = int.MaxValue);
            var s = sim.State;
            int h0 = LedgerApi.Points(s, PlayerIds.Human), r0 = LedgerApi.Points(s, PlayerIds.Rival);
            var all = new List<SimEvent>();
            all.AddRange(Do(sim, new DevRivalNextStep()));
            all.AddRange(Until(sim, () => Held(sim, Element.Earth) == Custody.AtRivalAltar, 2000, "on the altar"));
            Assert.AreEqual(r0 + 1, LedgerApi.Points(s, PlayerIds.Rival));
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Rival, "crystal:earth"));
            // Recovered, put back by the dev tool, recovered again: the Ash point is paid once.
            all.AddRange(Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Carried }));
            all.AddRange(Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.AtRivalAltar }));
            Assert.AreEqual(r0 + 1, LedgerApi.Points(s, PlayerIds.Rival));
            all.AddRange(Do(sim, new DevSetCustody { Crystal = Element.Earth, To = Custody.Restored }));
            Assert.AreEqual(h0 + 1, LedgerApi.Points(s, PlayerIds.Human));
            Assert.AreEqual(1, all.OfType<PointsAwarded>().Count(p => p.Faction == PlayerIds.Human && p.Key == "crystal:earth"));
            Assert.AreEqual(1, all.OfType<PointsAwarded>().Count(p => p.Faction == PlayerIds.Rival && p.Key == "crystal:earth"));
            Assert.AreEqual(0, all.OfType<RosterUnlocked>().Count());
            Assert.Less(LedgerApi.Points(s, PlayerIds.Human), LedgerApi.RosterThreshold);
        }
    }
}
