using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Encounters.Council;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>The council in the world: roster, eligibility, commands, events and saving.</summary>
    public class CouncilModuleTests
    {
        static readonly CouncilContent C = CouncilContent.BuiltIn;

        /// <summary>A new world with a player settlement that qualifies for an election.</summary>
        public static WorldSim Rig(out int settlement, ulong first = 1)
        {
            for (ulong seed = first; seed < first + 80; seed++)
            {
                var sim = WorldSimFactory.CreateNew(seed);
                foreach (var st in sim.State.Economy.Settlements)
                {
                    if (st.Faction != PlayerIds.Human || st.State != SettlementState.Active) continue;
                    if (CouncilApi.CanOffer(sim.State, st.Id)) { settlement = st.Id; return sim; }
                }
            }
            Assert.Fail("no qualifying settlement in 80 seeds");
            settlement = -1;
            return null;
        }

        public static Command ToCommand(ElectionAction a, int settlement)
        {
            switch (a.Kind)
            {
                case ActionKind.Push: return new CouncilPush { Settlement = settlement };
                case ActionKind.Choose: return new CouncilChoose { Settlement = settlement, PushId = a.PushId, Tray = a.Tray, Socket = a.Socket };
                case ActionKind.Bank: return new CouncilBank { Settlement = settlement };
                case ActionKind.AcceptBust: return new CouncilAcceptBust { Settlement = settlement };
                default: return new CouncilSpendPetition { Settlement = settlement };
            }
        }

        /// <summary>One command (the player's candidate acts with the scripted bot, otherwise an AI step). Returns events.</summary>
        public static List<SimEvent> StepOnce(WorldSim sim, int settlement)
        {
            var sc = CouncilApi.Get(sim.State, settlement);
            var e = sc.Election;
            Command cmd;
            if (e.Active == e.PlayerSeat) cmd = ToCommand(CouncilAi.Decide(e, C.Personality(e.Personality[e.Active])), settlement);
            else cmd = new CouncilAiStep { Settlement = settlement };
            return SimTestKit.Do(sim, cmd);
        }

        static WorldSim Started(out int settlement, bool help = true)
        {
            var sim = Rig(out settlement);
            SimTestKit.Do(sim, new CouncilOffer { Settlement = settlement });
            SimTestKit.Do(sim, new CouncilRespond { Settlement = settlement, Help = help });
            return sim;
        }

        [Test]
        public void FewerThanFourEligibleResidents_NoElectionIsOffered()
        {
            var sim = Rig(out int id);
            var s = sim.State;
            // strip the village down: one storekeeper is all that remains
            s.Economy.Buildings.RemoveAll(b => b.Settlement == id);
            s.Economy.Carriers.RemoveAll(c => c.Home == id);
            Assert.IsFalse(CouncilApi.CanOffer(s, id));
            var r = sim.Check(new CouncilOffer { Settlement = id });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(CouncilReasons.NotEligible, r.Reason);
            Assert.AreEqual(CouncilStatus.None, CouncilApi.StatusOf(s, id));
            // three residents are still too few, even if everyone wants to stand
            for (int i = 0; i < 2; i++) s.Economy.Carriers.Add(new Carrier { Id = 9000 + i, Faction = 0, Home = id });
            Assert.IsFalse(CouncilApi.CanOffer(s, id));
        }

        [Test]
        public void RosterIsDeterministic_NamedAndOnlyReadsTheEconomy()
        {
            var sim = Rig(out int id);
            var s = sim.State;
            string before = JsonConvert.SerializeObject(s.Economy);
            var a = CouncilRoster.Build(s, id, C);
            var b = CouncilRoster.Build(s, id, C);
            Assert.AreEqual(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
            Assert.AreEqual(a.Count, a.Select(r => r.Name).Distinct().Count(), "names are unique in a village");
            Assert.That(a.Select(r => r.Occupation), Does.Contain("storekeeper"));
            int active = s.Economy.Buildings.Count(x => x.Settlement == id && x.State == BuildingState.Active);
            int carriers = s.Economy.Carriers.Count(x => x.Home == id);
            Assert.AreEqual(active + carriers + 1, a.Count);
            foreach (var r in a) Assert.IsTrue(C.Names.Contains(r.Name));
            CouncilSetup.Offer(s, id, C); // no tick runs, so any change would be the council's
            Assert.AreEqual(before, JsonConvert.SerializeObject(s.Economy), "the council adds no workers or stock");
        }

        [Test]
        public void Offer_BindsFourRealResidents_AndElevenLeads()
        {
            var sim = Rig(out int id);
            var s = sim.State;
            var ev = SimTestKit.Do(sim, new CouncilOffer { Settlement = id });
            Assert.IsInstanceOf<CouncilOffered>(ev.OfType<CouncilOffered>().Single());
            var sc = CouncilApi.Get(s, id);
            Assert.AreEqual(CouncilStatus.Offered, sc.Status);
            Assert.AreEqual(4, sc.Candidates.Count);
            Assert.AreEqual(4, sc.Candidates.Select(x => x.Name).Distinct().Count());
            for (int seat = 0; seat < 4; seat++) Assert.AreEqual(C.Emblems[seat].Id, sc.Candidates[seat].Emblem);
            Assert.That(sc.AskerSeat, Is.InRange(0, 3));
            Assert.AreEqual("chief", sc.Office, "the prehistoric camp has a chief");

            var roster = CouncilRoster.Build(s, id, C);
            var keys = new HashSet<string>(roster.Select(r => r.Key));
            var seen = new HashSet<string>();
            foreach (var cand in sc.Candidates)
            {
                Assert.IsTrue(keys.Contains(cand.ResidentKey));
                Assert.IsTrue(seen.Add(cand.ResidentKey));
                Assert.IsTrue(roster.Single(r => r.Key == cand.ResidentKey).WantsToStand);
            }
            Assert.AreEqual(11, sc.Disputes.Count);
            foreach (var b in sc.Disputes)
            {
                var d = C.Dispute(b.Track);
                Assert.AreEqual(d.Id, b.DisputeId);
                Assert.AreEqual(C.Prop(d, Era.Prehistoric), b.Prop);
                if (b.IsDelegate)
                {
                    Assert.IsNull(b.LeadResidentKey);
                    Assert.AreEqual(d.FallbackTitle, b.LeadTitle);
                }
                else
                {
                    Assert.IsTrue(keys.Contains(b.LeadResidentKey));
                    Assert.IsTrue(seen.Add(b.LeadResidentKey), "no resident leads twice or stands and leads");
                    var res = roster.Single(r => r.Key == b.LeadResidentKey);
                    Assert.AreEqual(res.Name, b.LeadName);
                    Assert.IsTrue(d.Occupations.Contains("*") || d.Occupations.Contains(res.OccupationId));
                }
            }
        }

        [Test]
        public void DisputeFacts_NeverContradictTheSettlement()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var sim = WorldSimFactory.CreateNew(seed);
                foreach (var st in sim.State.Economy.Settlements.Where(x => x.State == SettlementState.Active))
                {
                    if (!CouncilApi.CanOffer(sim.State, st.Id)) continue;
                    var sc = CouncilSetup.Offer(sim.State, st.Id, C);
                    var types = new HashSet<BuildingType>(sim.State.Economy.Buildings.Where(b => b.Settlement == st.Id && b.State == BuildingState.Active).Select(b => b.Type));
                    foreach (var b in sc.Disputes.Where(x => !x.IsDelegate))
                    {
                        if (b.LeadTitle == "quarrier") Assert.IsTrue(types.Contains(BuildingType.Quarry) || types.Contains(BuildingType.RockHeap), "a quarrier needs stone works");
                        if (b.LeadTitle == "woodcutter") Assert.IsTrue(types.Contains(BuildingType.LogPile) || types.Contains(BuildingType.WoodcutterHut));
                        if (b.LeadTitle == "sawyer") Assert.IsTrue(types.Contains(BuildingType.Sawmill));
                    }
                    // nobody is a miner or healer: this economy has no mine and no healer's hut
                    Assert.IsFalse(sc.Disputes.Any(x => x.Track == 6 && !x.IsDelegate));
                    Assert.IsTrue(sc.Disputes.Single(x => x.Track == 2).IsDelegate);
                    sim.State.Council.ElectionsOffered = 0;
                }
            }
        }

        [Test]
        public void Age_ChangesOfficeAndProps()
        {
            var sim = Rig(out int id);
            sim.State.Eras.Current = Era.Historic;
            SimTestKit.Do(sim, new CouncilOffer { Settlement = id });
            var sc = CouncilApi.Get(sim.State, id);
            Assert.AreEqual("reeve", sc.Office);
            Assert.AreEqual("deed scroll", sc.Disputes.Single(x => x.Track == 12).Prop);
            Assert.AreEqual("broken oar", sc.Disputes.Single(x => x.Track == 2).Prop);
            Assert.AreEqual("mayor", C.Office(Era.Modern));
        }

        [Test]
        public void OfferAndRespond_Lifecycle()
        {
            var sim = Rig(out int id);
            var s = sim.State;
            Assert.AreEqual(CouncilReasons.WrongStatus, sim.Check(new CouncilRespond { Settlement = id, Help = true }).Reason);
            SimTestKit.Do(sim, new CouncilOffer { Settlement = id });
            Assert.AreEqual(CouncilReasons.WrongStatus, sim.Check(new CouncilOffer { Settlement = id }).Reason);
            Assert.AreEqual(CouncilReasons.WrongStatus, sim.Check(new CouncilPush { Settlement = id }).Reason);
            var ev = SimTestKit.Do(sim, new CouncilRespond { Settlement = id, Help = true });
            var sc = CouncilApi.Get(s, id);
            Assert.AreEqual(CouncilStatus.InProgress, sc.Status);
            Assert.AreEqual(sc.AskerSeat, sc.Election.PlayerSeat, "the player advises the candidate who asked");
            Assert.AreEqual(4, sc.Election.Personality.Distinct().Count());
            Assert.IsTrue(ev.OfType<CouncilStarted>().Single().PlayerHelps);
            Assert.AreEqual(CouncilReasons.WrongStatus, sim.Check(new CouncilRespond { Settlement = id, Help = false }).Reason);
        }

        [Test]
        public void OnlyTheOwnerMayOfferTheirVillage()
        {
            var sim = Rig(out int id);
            var r = sim.Check(new CouncilOffer { Settlement = id, PlayerId = PlayerIds.Rival });
            Assert.AreEqual(CouncilReasons.NotYourSettlement, r.Reason);
            Assert.AreEqual(CouncilReasons.UnknownSettlement, sim.Check(new CouncilOffer { Settlement = 999 }).Reason);
        }

        [Test]
        public void DecliningHelp_LetsAllFourCandidatesPlayThemselves()
        {
            var sim = Started(out int id, help: false);
            var sc = CouncilApi.Get(sim.State, id);
            Assert.AreEqual(-1, sc.Election.PlayerSeat);
            Assert.AreEqual(CouncilReasons.NoPlayer, sim.Check(new CouncilPush { Settlement = id }).Reason);
            Assert.AreEqual(CouncilReasons.NoPlayer, sim.Check(new CouncilStartListening { Settlement = id }).Reason);
            int guard = 0;
            var seats = new HashSet<int>();
            while (CouncilApi.StatusOf(sim.State, id) == CouncilStatus.InProgress && guard++ < 6000)
            {
                seats.Add(sc.Election.Active);
                SimTestKit.Do(sim, new CouncilAiStep { Settlement = id });
            }
            Assert.AreEqual(CouncilStatus.Decided, sc.Status);
            Assert.AreEqual(4, seats.Count);
        }

        [Test]
        public void AiStep_IsRefusedOnThePlayersTurn_AndPlayerActionsOnAnAiTurn()
        {
            var sim = Started(out int id);
            var e = CouncilApi.Get(sim.State, id).Election;
            if (e.Active == e.PlayerSeat)
            {
                Assert.AreEqual(CouncilReasons.NotYourTurn, sim.Check(new CouncilAiStep { Settlement = id }).Reason);
            }
            else
            {
                Assert.AreEqual(CouncilReasons.NotYourTurn, sim.Check(new CouncilPush { Settlement = id }).Reason);
                Assert.IsTrue(sim.Check(new CouncilAiStep { Settlement = id }).Ok);
            }
        }

        [Test]
        public void FullElection_DecidesOnce_RecordsTheOfficeHolder_AndEmitsEvents()
        {
            var sim = Started(out int id);
            var all = new List<SimEvent>();
            int guard = 0;
            while (CouncilApi.StatusOf(sim.State, id) == CouncilStatus.InProgress && guard++ < 6000) all.AddRange(StepOnce(sim, id));
            var sc = CouncilApi.Get(sim.State, id);
            Assert.AreEqual(CouncilStatus.Decided, sc.Status);
            Assert.IsTrue(sc.Applied);
            var office = CouncilApi.OfficeHolderOf(sim.State, id);
            Assert.IsNotNull(office);
            Assert.AreEqual(sc.Candidates[sc.WinnerSeat].Name, office.Name);
            Assert.AreEqual("chief", office.Office);
            Assert.AreEqual(1, sim.State.Council.Offices.Count);
            Assert.AreEqual(1, all.OfType<CouncilOfficeHandedOver>().Count());
            Assert.AreEqual(1, all.OfType<CouncilElectionWon>().Count());
            Assert.Greater(all.OfType<CouncilPushMade>().Count(), 10);
            Assert.Greater(all.OfType<CouncilOptionsOffered>().Count(), 10);
            Assert.Greater(all.OfType<CouncilAdvanceMade>().Count(), 5);
            Assert.Greater(all.OfType<CouncilBanked>().Count(), 3);
            Assert.AreEqual(3, all.OfType<CouncilTrackClaimed>().Count(x => x.Seat == sc.WinnerSeat));
            Assert.IsTrue(all.OfType<CouncilTurnPassed>().Any());
            Assert.IsTrue(all.All(x => !(x is CouncilEvent ce) || ce.Settlement == id));
            // everything afterwards is refused
            Assert.AreEqual(CouncilReasons.AlreadyDecided, sim.Check(new CouncilPush { Settlement = id }).Reason);
            Assert.AreEqual(CouncilReasons.AlreadyDecided, sim.Check(new CouncilAiStep { Settlement = id }).Reason);
            Assert.AreEqual(CouncilReasons.AlreadyDecided, sim.Check(new CouncilOffer { Settlement = id }).Reason);
        }

        [Test]
        public void ElectionResults_StayApartFromVictoryPoints()
        {
            var sim = Started(out int id);
            var before = sim.State.Players.Select(p => p.Coin).ToArray();
            int guard = 0;
            while (CouncilApi.StatusOf(sim.State, id) == CouncilStatus.InProgress && guard++ < 6000) StepOnce(sim, id);
            Assert.AreEqual(before.Length, sim.State.Players.Count);
            CollectionAssert.AreEqual(before, sim.State.Players.Select(p => p.Coin).ToArray());
        }

        [Test]
        public void SaveAndLoad_AtEveryPhase_ContinuesIdentically()
        {
            var sim = Started(out int id);
            var phases = new HashSet<ElectionPhase>();
            int guard = 0;
            while (CouncilApi.StatusOf(sim.State, id) == CouncilStatus.InProgress && guard++ < 6000)
            {
                var e = CouncilApi.Get(sim.State, id).Election;
                phases.Add(e.Phase);
                string json = SaveCodec.ToJson(sim.State);
                var loaded = SaveCodec.Load(json, out _);
                Assert.AreEqual(SaveCodec.StateJson(sim.State), SaveCodec.StateJson(loaded.State));
                StepOnce(sim, id);
                StepOnce(loaded, id);
                Assert.AreEqual(SaveCodec.StateJson(sim.State), SaveCodec.StateJson(loaded.State), "step " + guard);
                if (guard > 400 && phases.Contains(ElectionPhase.Choosing)) break; // enough steps through every phase
            }
            Assert.IsTrue(phases.Contains(ElectionPhase.Ready));
            Assert.IsTrue(phases.Contains(ElectionPhase.Choosing), "a pending push was saved and reloaded");
        }

        [Test]
        public void PendingPushAndItsTrays_ReloadExactly()
        {
            var sim = Started(out int id);
            for (int i = 0; i < 4000; i++)
            {
                var e = CouncilApi.Get(sim.State, id).Election;
                if (e.Active == e.PlayerSeat && e.Phase == ElectionPhase.Choosing) break;
                StepOnce(sim, id);
            }
            var cur = CouncilApi.Get(sim.State, id).Election;
            Assert.AreEqual(ElectionPhase.Choosing, cur.Phase);
            var loaded = SaveCodec.Load(SaveCodec.ToJson(sim.State), out _);
            var le = CouncilApi.Get(loaded.State, id).Election;
            Assert.AreEqual(cur.PushId, le.PushId);
            Assert.AreEqual(cur.Pending.Count, le.Pending.Count);
            Assert.AreEqual(JsonConvert.SerializeObject(cur.Pending), JsonConvert.SerializeObject(le.Pending));
            // the player can pick from the reloaded choice
            var tray = le.Pending[0];
            var cmd = new CouncilChoose { Settlement = id, PushId = le.PushId, Tray = 0, Socket = tray.Mode == TrayMode.Either ? (tray.Sockets[0].IsEmpty ? 1 : 0) : -1 };
            Assert.IsTrue(loaded.Submit(cmd).Ok);
        }

        [Test]
        public void WinnerIsAppliedOnce_AcrossSaveAndLoad()
        {
            var sim = Started(out int id);
            int guard = 0;
            while (CouncilApi.StatusOf(sim.State, id) == CouncilStatus.InProgress && guard++ < 6000) StepOnce(sim, id);
            var loaded = SaveCodec.Load(SaveCodec.ToJson(sim.State), out _);
            Assert.AreEqual(1, loaded.State.Council.Offices.Count);
            var loaded2 = SaveCodec.Load(SaveCodec.ToJson(loaded.State), out _);
            loaded2.Step(30);
            var events = loaded2.DrainEvents();
            Assert.IsFalse(events.Any(x => x is CouncilEvent));
            Assert.AreEqual(1, loaded2.State.Council.Offices.Count);
            Assert.AreEqual(CouncilReasons.AlreadyDecided, loaded2.Check(new CouncilOffer { Settlement = id }).Reason);
            Assert.AreEqual(CouncilApi.OfficeHolderOf(sim.State, id).Name, CouncilApi.OfficeHolderOf(loaded2.State, id).Name);
        }

        [Test]
        public void OlderSaves_WithoutACouncil_StillLoad()
        {
            var sim = WorldSimFactory.CreateNew(5);
            var m = JObject.Parse(SaveCodec.ToJson(sim.State));
            ((JObject)m["State"]).Remove("Council");
            var loaded = SaveCodec.Load(m.ToString(), out var model);
            Assert.IsNotNull(loaded.State.Council);
            Assert.AreEqual(0, loaded.State.Council.Settlements.Count);
            Assert.IsTrue(loaded.HasHandler(typeof(CouncilOffer)), "a loaded sim registers the council commands");
            Assert.AreEqual(SaveCodec.CurrentVersion, model.Version);
        }

        [Test]
        public void LeaveAndResume_KeepEverything_AndAbandonAnOpenBurst()
        {
            var sim = Started(out int id);
            var sc = CouncilApi.Get(sim.State, id);
            SimTestKit.Do(sim, new CouncilStartListening { Settlement = id });
            SimTestKit.Do(sim, new CouncilAnswerListening { Settlement = id, Choice = ListeningChoice.Same, ResponseMs = 1200 });
            string rolls = JsonConvert.SerializeObject(sc.Election.Rolls);
            var ev = SimTestKit.Do(sim, new CouncilLeave { Settlement = id });
            Assert.IsFalse(sc.Present);
            Assert.AreEqual(ListeningPhase.Abandoned, sc.Election.Listening.Phase);
            Assert.AreEqual(1, sc.Election.ListenAbandons);
            Assert.IsTrue(ev.OfType<CouncilLeft>().Any());
            Assert.AreEqual(CouncilReasons.WrongPhase, sim.Check(new CouncilLeave { Settlement = id }).Reason);
            var loaded = SaveCodec.Load(SaveCodec.ToJson(sim.State), out _);
            Assert.IsFalse(CouncilApi.Get(loaded.State, id).Present);
            SimTestKit.Do(loaded, new CouncilResume { Settlement = id });
            Assert.IsTrue(CouncilApi.Get(loaded.State, id).Present);
            Assert.AreEqual(rolls, JsonConvert.SerializeObject(CouncilApi.Get(loaded.State, id).Election.Rolls));
        }

        [Test]
        public void ListeningCommands_EarnAndSpendThePetition_ThroughTheCommandPattern()
        {
            var sim = Started(out int id);
            var sc = CouncilApi.Get(sim.State, id);
            var all = new List<SimEvent>();
            all.AddRange(SimTestKit.Do(sim, new CouncilStartListening { Settlement = id }));
            for (int i = 0; i < 5; i++)
            {
                var l = sc.Election.Listening;
                all.AddRange(SimTestKit.Do(sim, new CouncilAnswerListening
                {
                    Settlement = id, Choice = ListeningSession.IsSame(l, i) ? ListeningChoice.Same : ListeningChoice.Different, ResponseMs = 800,
                }));
            }
            Assert.AreEqual(5, all.OfType<CouncilListeningAnswered>().Count());
            Assert.AreEqual(1, all.OfType<CouncilListeningFinished>().Count());
            Assert.AreEqual(1, all.OfType<CouncilPetitionEarned>().Count());
            Assert.IsTrue(sc.Election.PetitionAvailable);
            Assert.AreEqual(CouncilReasons.PetitionAlreadyEarned, sim.Check(new CouncilStartListening { Settlement = id }).Reason);
            // stage a bust for the player's candidate and spend the petition
            var e = sc.Election;
            e.Active = e.PlayerSeat;
            e.Phase = ElectionPhase.BustPending;
            var ev = SimTestKit.Do(sim, new CouncilSpendPetition { Settlement = id });
            Assert.AreEqual(1, ev.OfType<CouncilPetitionSpent>().Count());
            Assert.IsTrue(e.PetitionSpent);
            e.Phase = ElectionPhase.BustPending;
            Assert.AreEqual(CouncilReasons.NoPetition, sim.Check(new CouncilSpendPetition { Settlement = id }).Reason);
        }

        [Test]
        public void SkippingListening_IsRecordedAndHarmless()
        {
            var sim = Started(out int id);
            var e = CouncilApi.Get(sim.State, id).Election;
            string rolls = JsonConvert.SerializeObject(e.Rolls);
            SimTestKit.Do(sim, new CouncilSkipListening { Settlement = id });
            Assert.AreEqual(1, e.ListenSkips);
            Assert.AreEqual(rolls, JsonConvert.SerializeObject(e.Rolls));
            Assert.IsFalse(e.PetitionEarned);
        }

        [Test]
        public void TheModuleRegistersEveryCommand_AndIsIdempotent()
        {
            var sim = WorldSimFactory.CreateNew(3);
            foreach (var t in new[] { typeof(CouncilOffer), typeof(CouncilRespond), typeof(CouncilPush), typeof(CouncilChoose), typeof(CouncilBank),
                typeof(CouncilAcceptBust), typeof(CouncilSpendPetition), typeof(CouncilAiStep), typeof(CouncilStartListening),
                typeof(CouncilAnswerListening), typeof(CouncilAbandonListening), typeof(CouncilSkipListening), typeof(CouncilLeave), typeof(CouncilResume) })
                Assert.IsTrue(sim.HasHandler(t), t.Name);
            CouncilModule.Register(sim); // no throw
        }

        [Test]
        public void EventsNeverExposeDiceValues()
        {
            foreach (var t in typeof(CouncilEvent).Assembly.GetTypes().Where(x => typeof(CouncilEvent).IsAssignableFrom(x)))
                foreach (var f in t.GetFields())
                    Assert.IsFalse(f.Name.ToLowerInvariant().Contains("dice") || f.Name.ToLowerInvariant().Contains("values"), t.Name + "." + f.Name);
            foreach (var f in typeof(ElectionState).GetFields())
                Assert.IsFalse(f.Name.ToLowerInvariant().Contains("dice") || f.Name.ToLowerInvariant().Contains("values"), f.Name);
        }
    }
}
