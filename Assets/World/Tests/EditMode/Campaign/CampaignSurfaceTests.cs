using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Encounters.Ward;
using World.Sim.Eras;
using World.Sim.Ledger;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Pins the simulation surface of PRD_CRYSTALS appendix A as landed by I0 (tag cry-I0-sim), and checks that the
    /// first-version behaviour is safe: a null campaign changes nothing, refusals are exact, an attempt resolves once, a
    /// running campaign survives a save. After I0 this surface may only grow (changes logged in Docs/CAMPAIGN_API.md).
    /// </summary>
    public class CampaignSurfaceTests
    {
        private const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        private static void Fields(Type t, params (string name, Type type)[] fields)
        {
            foreach (var (name, type) in fields)
            {
                var f = t.GetField(name, Pub);
                Assert.IsNotNull(f, t.Name + "." + name + " is a public field");
                Assert.AreEqual(type, f.FieldType, t.Name + "." + name);
            }
        }

        private static void Method(Type t, string name, Type ret, params Type[] ps)
        {
            var m = t.GetMethod(name, Pub, null, ps, null);
            Assert.IsNotNull(m, t.Name + "." + name + "(" + string.Join(", ", ps.Select(p => p.Name)) + ")");
            Assert.IsTrue(m.IsStatic && m.IsPublic, t.Name + "." + name + " is public static");
            Assert.AreEqual(ret, m.ReturnType, t.Name + "." + name + " returns");
        }

        private static void Enum<T>(params string[] names) where T : struct
        {
            CollectionAssert.AreEqual(names, System.Enum.GetNames(typeof(T)), typeof(T).Name + " members in order");
            for (int i = 0; i < names.Length; i++) Assert.AreEqual(i, (int)System.Enum.Parse(typeof(T), names[i]), typeof(T).Name + "." + names[i]);
        }

        private static void Command<T>(params (string name, Type type)[] fields) where T : Command
        {
            Assert.IsTrue(typeof(T).IsPublic && !typeof(T).IsAbstract && typeof(T).Namespace == "World.Sim.Campaign", typeof(T).Name);
            Assert.IsNotNull(typeof(T).GetConstructor(Type.EmptyTypes), typeof(T).Name + " has a default constructor");
            Fields(typeof(T), fields);
        }

        private static void Event<T>(params (string name, Type type)[] fields) where T : SimEvent
        {
            Assert.IsTrue(typeof(T).IsPublic && typeof(T).Namespace == "World.Sim.Campaign", typeof(T).Name);
            Fields(typeof(T), fields);
        }

        private static readonly Type I = typeof(int), L = typeof(long), U = typeof(ulong), B = typeof(bool), S = typeof(string), D = typeof(double),
            WS = typeof(WorldState), MR = typeof(MarkerRef), LS = typeof(List<string>), LE = typeof(List<Element>), E = typeof(Element);

        // ---------------------------------------------------------------- A.1

        [Test]
        public void A1_Enums()
        {
            Enum<Element>("Earth", "Fire", "Water", "Air");
            Enum<Custody>("AtSite", "RivalTransit", "AtRivalAltar", "Carried", "Restored");
            Enum<PackStatus>("Partial", "Complete");
            Enum<LocationKind>("Camp", "Site", "Tower", "Watchtower", "Shore");
            Enum<AnchorKind>("HomeCamp", "MeadowHexCentre", "RimCorner");
            Enum<MarkerKind>("Exit", "Stairs", "Door", "Pedestal", "Altar", "Threshold", "SafePoint", "Inscription", "Witness", "Lever", "DuelCaster", "DuelOpponent", "Plinth");
            Enum<SourceKind>("Witness", "Marker", "Companion", "Scout", "Report", "Wizard", "Opening");
            Enum<QuestAction>("Arrive", "Talk", "Examine", "Search", "Open", "Light", "Take", "Answer", "Restore");
            Enum<EncounterKind>("Lethal", "Friendly", "Practice");
            Enum<EffectKind>("SetFact", "TakeCrystal", "OfferChoice", "Journal", "PlaceParty", "RivalCharge", "SetBelief");
            Enum<SurvivorBand>("All", "Some", "None");
        }

        [Test]
        public void A1_Definition()
        {
            Fields(typeof(CampaignDefinition), ("FormatVersion", I), ("Id", S), ("Title", S), ("Status", typeof(PackStatus)), ("Template", S),
                ("Seed", U), ("GeneratorVersion", S), ("Roles", typeof(List<RoleDef>)), ("Companions", typeof(List<CompanionDef>)),
                ("Locations", typeof(List<LocationDef>)), ("Interiors", typeof(List<InteriorDef>)), ("Connections", typeof(List<ConnectionDef>)),
                ("Crystals", typeof(List<CrystalDef>)), ("Towers", typeof(List<TowerDef>)), ("Rival", typeof(RivalPlanDef)),
                ("Clues", typeof(List<ClueDef>)), ("Leads", typeof(List<LeadDef>)), ("Choices", typeof(List<ChoiceDef>)),
                ("Encounters", typeof(List<EncounterDef>)), ("Events", typeof(List<QuestEventDef>)), ("Endings", typeof(List<EndingDef>)),
                ("Lines", typeof(List<LineDef>)), ("Tuning", typeof(CampaignTuning)));
            Assert.AreEqual(1, CampaignDefinition.CurrentFormatVersion);
            Assert.AreEqual(1, new CampaignDefinition().FormatVersion);
            Fields(typeof(MarkerRef), ("Location", S), ("Interior", S), ("Marker", S));
            Assert.IsTrue(typeof(MarkerRef).IsValueType);
            Fields(typeof(MarkerDef), ("Id", S), ("Kind", typeof(MarkerKind)), ("I", I), ("J", I), ("To", S), ("OpenWhen", S), ("Crystal", S), ("Name", S));
            Fields(typeof(RoleDef), ("Id", S), ("ActorId", S), ("Name", S));
            Fields(typeof(CompanionDef), ("Id", S), ("Name", S), ("Role", S), ("Line", S), ("VolunteerOrder", I), ("Service", S), ("ActorId", S));
            Fields(typeof(LocationDef), ("Id", S), ("Name", S), ("Kind", typeof(LocationKind)), ("Anchor", typeof(AnchorDef)), ("Markers", typeof(List<MarkerDef>)), ("SafePoint", S));
            Fields(typeof(AnchorDef), ("Kind", typeof(AnchorKind)), ("Q", I), ("R", I), ("Corner", I));
            Fields(typeof(InteriorDef), ("Id", S), ("Name", S), ("Rows", LS), ("Legend", typeof(SortedDictionary<char, MarkerDef>)));
            Fields(typeof(ConnectionDef), ("Id", S), ("A", MR), ("B", MR), ("OpenWhen", S));
            Fields(typeof(CrystalDef), ("Id", S), ("Element", E), ("Name", S), ("Site", MR), ("Tower", S), ("Underground", B));
            Fields(typeof(TowerDef), ("Id", S), ("Location", S), ("Element", E), ("Sign", S), ("BeamColour", S));
            Fields(typeof(RivalPlanDef), ("LeaderRole", S), ("Steps", typeof(List<RivalStepDef>)), ("StolenAtStart", LS));
            Fields(typeof(RivalStepDef), ("Crystal", S), ("MarchCost", I), ("CarryCost", I), ("Altar", MR));
            Fields(typeof(ClueDef), ("Id", S), ("Source", typeof(ClueSourceDef)), ("Requires", LS), ("Gives", LS), ("Essential", B), ("Fallback", S));
            Fields(typeof(ClueSourceDef), ("Kind", typeof(SourceKind)), ("Ref", S));
            Fields(typeof(LeadDef), ("Id", S), ("Title", S), ("Goal", typeof(GoalDef)), ("Requires", LS), ("DoneWhen", S), ("ReasonLine", S), ("Priority", I), ("Crystal", S));
            Fields(typeof(GoalDef), ("At", MR), ("Action", typeof(QuestAction)));
            Fields(typeof(ChoiceDef), ("Id", S), ("QuestionLine", S), ("Answers", typeof(List<AnswerDef>)), ("RevealedBy", MR), ("WrongLine", S), ("Sets", LS));
            Fields(typeof(AnswerDef), ("Line", S), ("Correct", B));
            Fields(typeof(EncounterDef), ("Id", S), ("Name", S), ("Kind", typeof(EncounterKind)), ("Profile", S), ("OpponentActorId", S), ("OpponentName", S),
                ("Threshold", MR), ("SafePoint", MR), ("Stage", typeof(DuelStageDef)), ("OnVictory", typeof(List<EffectDef>)), ("Alternate", S), ("BoundTo", S));
            Fields(typeof(DuelStageDef), ("Caster", MR), ("Opponent", MR), ("Volunteer", MR));
            Fields(typeof(EffectDef), ("Kind", typeof(EffectKind)), ("Crystal", S), ("Ref", S), ("At", MR), ("Amount", I));
            Fields(typeof(QuestEventDef), ("Id", S), ("At", MR), ("Action", typeof(QuestAction)), ("Requires", LS), ("Sets", LS),
                ("Effects", typeof(List<EffectDef>)), ("Lines", LS), ("Once", B), ("Guards", LS));
            Fields(typeof(EndingDef), ("Id", S), ("Outcome", typeof(CampaignPhase)), ("Lines", LS), ("Variants", typeof(List<EndingVariantDef>)));
            Fields(typeof(EndingVariantDef), ("Survivors", typeof(SurvivorBand)), ("Lines", LS));
            Fields(typeof(LineDef), ("Id", S), ("Speaker", S), ("Text", S), ("Guards", LS), ("Topic", S));
            Assert.AreEqual("last_tower", EncounterBindings.LastTower);
            CollectionAssert.AreEqual(new[] { "shield", "lamp", "fire", "water", "earth", "scout" },
                new[] { CompanionServices.Shield, CompanionServices.Lamp, CompanionServices.Fire, CompanionServices.Water, CompanionServices.Earth, CompanionServices.Scout });
        }

        [Test]
        public void A1_Tuning_StartingNumbers()
        {
            var t = new CampaignTuning();
            var want = new Dictionary<string, int>
            {
                { "PartyTicksPerStep", 7 }, { "ScoutTicksPerStep", 4 }, { "ReadTicks", 50 }, { "SearchTicks", 150 }, { "TakeTicks", 50 }, { "RestoreTicks", 100 },
                { "LeadPatience", 450 }, { "SoloDecide", 100 }, { "ChoicePatience", 300 }, { "SoloStudyTicks", 200 }, { "ThresholdWait", 900 },
                { "DeclineCooldown", 600 }, { "PlanInterval", 20 }, { "QueueLength", 3 }, { "StaleTicks", 3000 }, { "ReportDelay", 300 }, { "SearchCost", 300 },
                { "DefeatCharge", 900 }, { "DrawCharge", 600 }, { "DrawChargeWithShield", 300 }, { "WrongAnswerCharge", 300 },
                { "CampOverrunCharge", 1800 }, { "AshCampLostPause", 900 }, { "TideCycle", 2400 }, { "TideDry", 900 }, { "BoonPercent", 20 },
            };
            foreach (var kv in want)
            {
                var f = typeof(CampaignTuning).GetField(kv.Key, Pub);
                Assert.IsNotNull(f, kv.Key);
                Assert.AreEqual(I, f.FieldType, kv.Key);
                Assert.AreEqual(kv.Value, f.GetValue(t), kv.Key);
            }
        }

        [Test]
        public void MarkerRef_TextForm()
        {
            Assert.IsTrue(MarkerRef.TryParse("glade/stair", out var a));
            Assert.AreEqual(MarkerRef.Surface("glade", "stair"), a);
            Assert.IsFalse(a.IsInterior);
            Assert.IsTrue(MarkerRef.TryParse("glade/quarry/exit", out var b));
            Assert.AreEqual(new MarkerRef("glade", "quarry", "exit"), b);
            Assert.AreEqual("glade/quarry/exit", b.ToString());
            Assert.IsFalse(MarkerRef.TryParse("glade", out _));
            Assert.IsFalse(MarkerRef.TryParse("a//b", out _));
            Assert.IsTrue(default(MarkerRef).IsEmpty);
            Assert.AreEqual(new MarkerRef("x", null, "y"), new MarkerRef("x", "", "y"), "null and empty interior are the same place");
        }

        // ---------------------------------------------------------------- A.2

        [Test]
        public void A2_State()
        {
            Enum<CampaignPhase>("Running", "Won", "Lost");
            Enum<PartyMode>("Idle", "Travelling", "Acting", "AskingChoice", "AtThreshold", "InEncounter", "WaitingForPlayer");
            Enum<RivalPhase>("Marching", "Carrying", "Searching", "Paused", "PlanDone", "AtLastTower");
            Enum<DuelResult>("Victory", "Defeat", "Clash", "Stalemate", "Surrender");
            Enum<FallCause>("Defeat", "Surrender");
            Enum<RivalReportKind>("Marching", "Seized", "OnAltar", "Searching", "AtLastTower");
            Fields(typeof(WorldState), ("Campaign", typeof(CampaignState)));
            Assert.IsNull(WorldSimFactory.CreateNew(1).State.Campaign, "no campaign by default");
            Fields(typeof(CampaignState), ("FormatVersion", I), ("Definition", typeof(CampaignDefinition)), ("DefinitionHash", S),
                ("Phase", typeof(CampaignPhase)), ("StartTick", L), ("EndingId", S), ("RivalProgress", L), ("Rival", typeof(RivalState)),
                ("Crystals", typeof(CrystalState[])), ("Beliefs", typeof(Belief[])), ("Party", typeof(PartyState)),
                ("Companions", typeof(List<CompanionState>)), ("Facts", LS), ("Leads", typeof(List<LeadState>)), ("NextLeadId", I),
                ("Declined", typeof(List<DeclinedLead>)), ("Attempt", typeof(AttemptState)), ("NextAttemptId", I),
                ("Resolved", typeof(List<AttemptRecord>)), ("Scout", typeof(ScoutState)), ("Journal", typeof(List<JournalEntry>)),
                ("RestoredOrder", LE), ("FiredEvents", LS), ("BaseSevensPerDraw", I), ("Rng", typeof(Rng)),
                ("Reports", typeof(List<RivalReport>)), ("EverOnAltar", LE));
            Assert.AreEqual(1, new CampaignState().FormatVersion);
            Fields(typeof(RivalState), ("Phase", typeof(RivalPhase)), ("Step", I), ("StepProgress", L), ("PausedUntilProgress", L), ("At", MR));
            Fields(typeof(CrystalState), ("Custody", typeof(Custody)), ("At", MR), ("SinceTick", L));
            Fields(typeof(Belief), ("Custody", typeof(Custody)), ("At", MR), ("AsOfTick", L), ("Source", typeof(SourceKind)));
            Fields(typeof(PartyState), ("Mode", typeof(PartyMode)), ("At", MR), ("PlayerPresent", B), ("Leg", typeof(LegState)), ("LeadId", I),
                ("ChoiceId", S), ("EncounterId", S), ("ModeSinceTick", L));
            Fields(typeof(LegState), ("From", MR), ("To", MR), ("Ticks", I), ("Progress", I), ("Cells", typeof(int[])));
            Fields(typeof(CompanionState), ("Id", S), ("Alive", B), ("FellAt", S), ("FellTick", L), ("Cause", typeof(FallCause)));
            Fields(typeof(LeadState), ("Id", I), ("DefId", S), ("Score", I), ("Rank", I), ("TopSinceTick", L));
            Fields(typeof(DeclinedLead), ("DefId", S), ("UntilTick", L));
            Fields(typeof(AttemptState), ("Id", I), ("EncounterId", S), ("Volunteer", S), ("Seed", U), ("Restarts", I), ("CommittedTick", L));
            Fields(typeof(AttemptRecord), ("Id", I), ("EncounterId", S), ("Result", typeof(DuelResult)), ("Volunteer", S), ("Tick", L));
            Fields(typeof(ScoutState), ("Out", B), ("CompanionId", S), ("Target", S), ("Leg", typeof(LegState)));
            Fields(typeof(JournalEntry), ("Tick", L), ("Text", S), ("Place", S));
            Fields(typeof(RivalReport), ("Crystal", E), ("Kind", typeof(RivalReportKind)), ("At", MR), ("Tick", L), ("DueTick", L));
            // Partial so I1 can add fields from Sim/Campaign/Runtime (checked by name: partial leaves no metadata).
            Assert.IsTrue(typeof(CampaignState).IsSealed);
        }

        // ---------------------------------------------------------------- A.3

        [Test]
        public void A3_Commands()
        {
            Command<StartCampaign>(("Definition", typeof(CampaignDefinition)));
            Command<SetCompany>(("Present", B));
            Command<AcceptLead>(("LeadId", I));
            Command<DeclineLead>(("LeadId", I));
            Command<AdviseMira>(("ChoiceId", S), ("Answer", I));
            Command<SuggestScout>(("LocationId", S));
            Command<CommitEncounter>(("EncounterId", S));
            Command<WithdrawFromEncounter>(("EncounterId", S));
            Command<ResolveEncounter>(("AttemptId", I), ("Result", typeof(DuelResult)));
            Command<RestartAttempt>(("AttemptId", I));
            Command<ConfirmRestore>(("Crystal", E));
            Command<DevSetRivalProgress>(("Value", L));
            Command<DevRivalNextStep>();
            Command<DevSetCustody>(("Crystal", E), ("To", typeof(Custody)));
            Command<DevSetCompanion>(("Id", S), ("Alive", B));
            Assert.AreEqual(15, CampaignModule.CommandTypes.Length);
            Assert.IsNull(typeof(CampaignModule).Assembly.GetType("World.Sim.Campaign.Advise"), "the campaign's command is AdviseMira, never Advise");
        }

        [Test]
        public void A3_Refusals_ExactSentences()
        {
            var want = new Dictionary<string, string>
            {
                { "no_campaign", "No expedition is under way." },
                { "campaign_running", "An expedition is already under way." },
                { "campaign_over", "The expedition is over." },
                { "invalid_definition", "That campaign cannot be played: it has no towers." },
                { "unknown_lead", "Mira has moved on from that." },
                { "not_at_choice", "Nobody is asking that now." },
                { "not_at_threshold", "There is no fight to start here." },
                { "scout_out", "Wait for Tansy Reed to come back first." },
                { "no_scout", "There is nobody left to send." },
                { "unknown_attempt", "That duel is not the one under way." },
                { "already_resolved", "That duel is already settled." },
                { "not_carried", "Mira is not carrying that crystal." },
                { "wrong_tower", "This is not its tower." },
                { "player_needed", "Mira waits for you before she does that." },
                { "unknown_place", "Mira does not know that place." },
            };
            CollectionAssert.AreEquivalent(want.Keys, CampaignRefusals.All);
            foreach (var kv in want)
            {
                string arg = kv.Key == "invalid_definition" ? "it has no towers." : kv.Key == "scout_out" ? "Tansy Reed" : null;
                Assert.AreEqual(kv.Value, CampaignRefusals.Sentence(kv.Key, arg), kv.Key);
                var r = CampaignRefusals.Reject(kv.Key, arg);
                Assert.IsFalse(r.Ok);
                Assert.AreEqual(kv.Key, r.Reason);
            }
        }

        // ---------------------------------------------------------------- A.4

        [Test]
        public void A4_Events()
        {
            Event<CampaignStarted>(("DefinitionId", S));
            Event<LeadAdded>(("LeadId", I), ("DefId", S), ("Rank", I));
            Event<LeadsReordered>(("TopLeadId", I));
            Event<LeadRemoved>(("LeadId", I), ("Why", S));
            Event<PartySetOut>(("LeadId", I), ("From", MR), ("To", MR), ("LegTicks", I));
            Event<PartyArrived>(("At", MR));
            Event<PartyModeChanged>(("Mode", typeof(PartyMode)));
            Event<QuestEventFired>(("EventId", S));
            Event<FactLearned>(("Fact", S), ("Source", typeof(SourceKind)));
            Event<ChoiceOffered>(("ChoiceId", S));
            Event<ChoiceAnswered>(("ChoiceId", S), ("Answer", I), ("Correct", B), ("ByPlayer", B));
            Event<CrystalCustodyChanged>(("Crystal", E), ("From", typeof(Custody)), ("To", typeof(Custody)), ("At", MR));
            Event<BeliefChanged>(("Crystal", E));
            Event<RivalStepStarted>(("Step", I), ("Crystal", E));
            Event<RivalReported>(("Crystal", E), ("Kind", typeof(RivalReportKind)), ("At", MR));
            Event<RivalAdvanced>(("Amount", L), ("Why", S));
            Event<RivalPaused>(("Until", L));
            Event<EncounterThreshold>(("EncounterId", S), ("Volunteer", S));
            Event<EncounterCommitted>(("AttemptId", I), ("EncounterId", S), ("Volunteer", S), ("Seed", U), ("Profile", S));
            Event<EncounterResolved>(("AttemptId", I), ("EncounterId", S), ("Result", typeof(DuelResult)));
            Event<CompanionFell>(("CompanionId", S), ("EncounterId", S));
            Event<PartyRetreated>(("To", MR), ("Why", S));
            Event<CrystalRestored>(("Crystal", E), ("Tower", S), ("Count", I));
            Event<ScoutSent>(("CompanionId", S), ("Target", S));
            Event<ScoutReported>(("Crystal", E));
            Event<ScoutReturned>();
            Event<JournalAdded>(("Text", S));
            Event<CampaignEnded>(("Won", B), ("EndingId", S), ("Record", typeof(CampaignRecord)));
            CollectionAssert.AreEqual(new[] { "defeat", "draw", "wrong_answer", "camp_overrun", "dev" },
                new[] { RivalCharges.Defeat, RivalCharges.Draw, RivalCharges.WrongAnswer, RivalCharges.CampOverrun, RivalCharges.Dev });
        }

        // ---------------------------------------------------------------- A.5

        [Test]
        public void A5_ReadHelpers_Module_Loader_Validator()
        {
            var a = typeof(CampaignApi);
            Method(a, "IsRunning", B, WS);
            Method(a, "Crystal", typeof(CrystalView), WS, E);
            Method(a, "Belief", typeof(BeliefView), WS, E);
            Method(a, "Leads", typeof(IReadOnlyList<LeadView>), WS);
            Method(a, "TopLead", typeof(LeadView), WS);
            Method(a, "Party", typeof(PartyView), WS);
            var pp = a.GetMethod("PartyPosition", Pub);
            Assert.IsNotNull(pp);
            CollectionAssert.AreEqual(new[] { WS, D, MR.MakeByRefType(), MR.MakeByRefType(), D.MakeByRefType() }, pp.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.IsTrue(pp.GetParameters().Skip(2).All(p => p.IsOut));
            Assert.AreEqual(B, pp.ReturnType);
            Method(a, "LegCells", typeof(int[]), WS);
            Method(a, "Companions", typeof(IReadOnlyList<CompanionView>), WS);
            Method(a, "NextVolunteer", typeof(CompanionView), WS);
            Method(a, "Attempt", typeof(AttemptView), WS);
            Method(a, "RivalView", typeof(RivalKnown), WS);
            Method(a, "Journal", typeof(IReadOnlyList<JournalEntry>), WS);
            Method(a, "WhereIsMira", S, WS);
            Method(a, "Line", S, WS, S);
            Method(a, "LinesFor", typeof(IReadOnlyList<string>), WS, S, S);
            Method(a, "IsOpen", B, WS, S);
            Method(a, "CausewayDry", B, WS);
            Method(a, "Check", typeof(CommandResult), WS, typeof(Command));
            Method(a, "Record", typeof(CampaignRecord), WS);

            Fields(typeof(CampaignRecord), ("EndingId", S), ("DefinitionId", S), ("Seed", U), ("RestoredOrder", LE), ("EverOnAltar", LE),
                ("Survivors", LS), ("Fallen", typeof(List<FallenRecord>)));
            Fields(typeof(FallenRecord), ("Id", S), ("Name", S), ("EncounterId", S), ("Place", S), ("Tick", L), ("Cause", typeof(FallCause)));
            Fields(typeof(HistoryState), ("CampaignRecord", typeof(CampaignRecord)));
            Fields(typeof(CrystalView), ("Element", E), ("Id", S), ("Name", S), ("Custody", typeof(Custody)), ("At", MR), ("SinceTick", L), ("Tower", S));
            Fields(typeof(BeliefView), ("Element", E), ("Custody", typeof(Custody)), ("At", MR), ("AsOfTick", L), ("Source", typeof(SourceKind)), ("Stale", B), ("WhereToLook", S));
            Fields(typeof(LeadView), ("Id", I), ("DefId", S), ("Title", S), ("Reason", S), ("Score", I), ("Rank", I), ("Crystal", S), ("Goal", MR), ("Action", typeof(QuestAction)));
            Fields(typeof(PartyView), ("Mode", typeof(PartyMode)), ("At", MR), ("PlayerPresent", B), ("LeadId", I), ("ChoiceId", S), ("EncounterId", S), ("ModeSinceTick", L), ("To", MR));
            Fields(typeof(CompanionView), ("Id", S), ("Name", S), ("Role", S), ("Line", S), ("Service", S), ("ActorId", S), ("VolunteerOrder", I), ("Alive", B), ("FellAt", S), ("FellTick", L));
            Fields(typeof(AttemptView), ("Id", I), ("EncounterId", S), ("Volunteer", S), ("Seed", U), ("Restarts", I), ("CommittedTick", L), ("Profile", S), ("Kind", typeof(EncounterKind)));
            Fields(typeof(RivalKnown), ("Reports", typeof(List<RivalReport>)), ("LastReportTick", L));

            Method(typeof(CampaignModule), "Register", typeof(void), typeof(WorldSim));
            Method(typeof(CampaignLoader), "LoadFolder", typeof(CampaignDefinition), typeof(Func<string, string>), typeof(IEnumerable<string>));
            Method(typeof(CampaignLoader), "Hash", S, typeof(CampaignDefinition));
            Method(typeof(CampaignValidator), "Validate", typeof(IReadOnlyList<Problem>), typeof(CampaignDefinition));
            Fields(typeof(Problem), ("Code", S), ("Sentence", S), ("Id", S), ("IsError", B));
            Assert.AreEqual(18, ProblemCodes.All.Length);
            StringAssert.Contains("rival_plan", string.Join(",", ProblemCodes.All));
            Method(typeof(InteriorMaps), "IsFloor", B, typeof(InteriorDef), I, I);
            Method(typeof(InteriorMaps), "Path", typeof(int[]), typeof(InteriorDef), I, I, I, I, typeof(Func<string, bool>));
            Method(typeof(Lattice), "CellOf", typeof(void), D, D, I.MakeByRefType(), I.MakeByRefType());
            Method(typeof(Lattice), "Distance", I, I, I, I, I);
            Assert.AreEqual("World.Sim", typeof(Lattice).Namespace);

            Fields(typeof(WardOpponentProfile), ("Id", S), ("DisplayName", S), ("FirstCastMinMs", I), ("FirstCastMaxMs", I), ("ThinkMinMs", I),
                ("ThinkMaxMs", I), ("MistakeRatePercent", I), ("Pool", typeof(Essence[])));
            Fields(typeof(WardDuelSpec), ("Tuning", typeof(WardTuning)), ("Pool", typeof(Essence[])), ("Seed", U));
            Method(typeof(WardTuning), "For", typeof(WardTuning), typeof(WardOpponentProfile), typeof(WardTuning));
            Method(typeof(WardDuelSession), "Create", typeof(WardDuelSession), typeof(WardDuelSpec));
            Method(typeof(DuelResults), "TryFrom", B, typeof(WardOutcome), typeof(DuelResult).MakeByRefType());
            Method(typeof(CampaignGuards), "Holds", B, typeof(CampaignState), S);
            Method(typeof(CampaignTokens), "Fill", S, typeof(CampaignState), S);
        }

        [Test]
        public void Module_RegisteredAtCampaignSlot_Idempotent()
        {
            var sim = WorldSimFactory.CreateNew(2);
            Assert.IsTrue(sim.HasHandler(typeof(StartCampaign)));
            foreach (var t in CampaignModule.CommandTypes) Assert.IsTrue(sim.HasHandler(t), t.Name);
            int n = sim.Systems.Count;
            Assert.AreEqual(1, sim.Systems.Count(x => x is CampaignSystem));
            CampaignModule.Register(sim);
            Assert.AreEqual(n, sim.Systems.Count, "a second Register does nothing");

            Assert.AreEqual(1250, SystemOrder.Campaign);
        }

        // ---------------------------------------------------------------- null campaign is inert

        [Test]
        public void NullCampaign_HelpersAreEmpty_CommandsRefused()
        {
            var sim = WorldSimFactory.CreateNew(4);
            var s = sim.State;
            Assert.IsFalse(CampaignApi.IsRunning(s));
            Assert.IsNull(CampaignApi.Crystal(s, Element.Earth));
            Assert.IsNull(CampaignApi.Belief(s, Element.Air));
            Assert.AreEqual(0, CampaignApi.Leads(s).Count);
            Assert.IsNull(CampaignApi.TopLead(s));
            Assert.IsNull(CampaignApi.Party(s));
            Assert.IsFalse(CampaignApi.PartyPosition(s, 0.5, out _, out _, out var t));
            Assert.AreEqual(0, t);
            Assert.AreEqual(0, CampaignApi.LegCells(s).Length);
            Assert.AreEqual(0, CampaignApi.Companions(s).Count);
            Assert.IsNull(CampaignApi.NextVolunteer(s));
            Assert.IsNull(CampaignApi.Attempt(s));
            Assert.AreEqual(0, CampaignApi.RivalView(s).Reports.Count);
            Assert.AreEqual(0, CampaignApi.Journal(s).Count);
            Assert.AreEqual("", CampaignApi.WhereIsMira(s));
            Assert.IsNull(CampaignApi.Line(s, "mira_open_1"));
            Assert.AreEqual(0, CampaignApi.LinesFor(s, "mira", "").Count);
            Assert.IsFalse(CampaignApi.IsOpen(s, "glade_stair"));
            Assert.IsFalse(CampaignApi.CausewayDry(s));
            Assert.IsNull(CampaignApi.Record(s));
            Assert.IsNull(CampaignApi.Record(null));
            Assert.IsFalse(CampaignApi.IsRunning(null));

            var cmds = new Command[]
            {
                new SetCompany { Present = false }, new AcceptLead { LeadId = 1 }, new DeclineLead { LeadId = 1 },
                new AdviseMira { ChoiceId = "air_test", Answer = 1 }, new SuggestScout { LocationId = "glade" },
                new CommitEncounter { EncounterId = "keeper_earth" }, new WithdrawFromEncounter { EncounterId = "keeper_earth" },
                new ResolveEncounter { AttemptId = 1, Result = DuelResult.Victory }, new RestartAttempt { AttemptId = 1 },
                new ConfirmRestore { Crystal = Element.Earth }, new DevSetRivalProgress { Value = 5 }, new DevRivalNextStep(),
                new DevSetCustody { Crystal = Element.Earth, To = Custody.Carried }, new DevSetCompanion { Id = "odo", Alive = false },
            };
            foreach (var c in cmds)
            {
                var r = sim.Submit(c);
                Assert.IsFalse(r.Ok, c.GetType().Name);
                Assert.AreEqual("no_campaign", r.Reason, c.GetType().Name);
                Assert.AreEqual("no_campaign", CampaignApi.Check(s, c).Reason);
            }
            var bad = sim.Submit(new StartCampaign());
            Assert.AreEqual("invalid_definition", bad.Reason);
        }

        /// <summary>
        /// I0 acceptance: a null campaign changes nothing. A 20-minute game with the campaign module registered gives the
        /// same state JSON, tick for tick, as the same game without it.
        /// </summary>
        [Test]
        public void NullCampaign_TwentyMinuteRun_IdenticalStateJson()
        {
            var with = WorldSimFactory.CreateNew(11);
            // The same game wired by hand with every module but the campaign (SaveCodec.CreateSim's list without it).
            var without = new WorldSim(SaveCodec.FromJson(SaveCodec.ToJson(with.State)).State);
            Economy.EconomyModule.Register(without);
            Factions.FactionsModule.Register(without);
            Demons.DemonsModule.Register(without);
            ErasModule.Register(without);
            LedgerModule.Register(without);
            Assert.IsFalse(without.HasHandler(typeof(StartCampaign)));
            for (int minute = 0; minute < 20; minute++)
            {
                with.Step(600);
                without.Step(600);
                Assert.AreEqual(SaveCodec.StateJson(without.State), SaveCodec.StateJson(with.State), "minute " + (minute + 1));
            }
            Assert.IsNull(with.State.Campaign);
            StringAssert.Contains("\"Campaign\":null", SaveCodec.StateJson(with.State));
            StringAssert.Contains("\"CampaignRecord\":null", SaveCodec.StateJson(with.State));
        }

        /// <summary>N-1 fallback: the automatic casting in Brain is keyed on Faction.Auto, so a test start can let the prince's people build by themselves.</summary>
        [Test]
        public void N1_PlayersPeopleCastByThemselves_WhenAutoIsSet()
        {
            int Casts(bool auto)
            {
                var sim = WorldSimFactory.CreateNew(5);
                sim.State.Factions.Factions[PlayerIds.Human].Auto = auto;
                return SimTestKit.Step(sim, 6000).OfType<Factions.SpellCast>().Count(e => e.Faction == PlayerIds.Human);
            }
            Assert.AreEqual(0, Casts(false), "by default the player's people wait for the player");
            Assert.Greater(Casts(true), 0, "with Auto they cast their own proposals");
        }

        // ---------------------------------------------------------------- StartCampaign

        [Test]
        public void Start_BuildsTheRun_SwitchesCampaignMode_RegistersLandmarks()
        {
            var sim = WorldSimFactory.CreateNew(7);
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = CampaignSamples.Skeleton() }).Ok);
            var ev = SimTestKit.Step(sim, 1);
            var s = sim.State;
            var c = s.Campaign;
            Assert.IsNotNull(c);
            Assert.AreEqual(1, ev.OfType<CampaignStarted>().Count());
            Assert.AreEqual("sample_v1", ev.OfType<CampaignStarted>().Single().DefinitionId);
            Assert.IsTrue(CampaignApi.IsRunning(s));
            Assert.IsTrue(AgesApi.IsCampaign(s), "StartCampaign switches the island to campaign mode");
            Assert.AreEqual(CampaignLoader.Hash(CampaignSamples.Skeleton()), c.DefinitionHash);
            Assert.AreEqual(64, c.DefinitionHash.Length);
            Assert.AreEqual(1, c.StartTick);
            Assert.AreEqual(4, c.Crystals.Length);
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
            {
                Assert.AreEqual(Custody.AtSite, CampaignApi.Crystal(s, e).Custody, e.ToString());
                Assert.AreEqual(Custody.AtSite, CampaignApi.Belief(s, e).Custody);
                Assert.IsFalse(CampaignApi.Belief(s, e).Stale);
            }
            Assert.AreEqual(new MarkerRef("glade", "quarry", "pedestal"), CampaignApi.Crystal(s, Element.Earth).At);
            CollectionAssert.AreEqual(CampaignSamples.CompanionIds, CampaignApi.Companions(s).Select(k => k.Id).ToArray());
            Assert.IsTrue(CampaignApi.Companions(s).All(k => k.Alive));
            Assert.AreEqual("garrick", CampaignApi.NextVolunteer(s).Id);
            Assert.AreEqual(PartyMode.Idle, CampaignApi.Party(s).Mode);
            Assert.AreEqual(MarkerRef.Surface("home", "fire"), CampaignApi.Party(s).At);
            Assert.IsTrue(CampaignApi.Party(s).PlayerPresent);
            CollectionAssert.AreEqual(new[] { "earth.marks_heard" }, c.Facts, "opening clue");
            Assert.AreEqual(RivalPhase.Marching, c.Rival.Phase);
            Assert.AreEqual(s.Demons.Tuning.SevensPerDraw, c.BaseSevensPerDraw);

            var towers = LandmarkApi.TowerCorners(s.Board.Topology);
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
                Assert.AreEqual(towers[(int)e], LandmarkApi.Get(s, "lm.tower." + e.ToString().ToLowerInvariant())?.Corner, "tower " + e);
            var topo = s.Board.Topology;
            Assert.AreEqual(topo.HexIndex(new HexCoord(-2, -2)), LandmarkApi.Get(s, LandmarkIds.AltarEarth)?.Hex);
            Assert.AreEqual(topo.HexIndex(new HexCoord(2, -3)), LandmarkApi.Get(s, LandmarkIds.AltarWater)?.Hex);
            Assert.AreEqual(topo.HexIndex(new HexCoord(1, 3)), LandmarkApi.Get(s, LandmarkIds.AltarAir)?.Hex);
            Assert.AreEqual(topo.HexIndex(new HexCoord(-4, 2)), LandmarkApi.Get(s, LandmarkIds.AltarFire)?.Hex);

            // The expedition clock: +1 per tick while running (5.1).
            long p0 = c.RivalProgress;
            sim.Step(30);
            Assert.AreEqual(p0 + 30, c.RivalProgress);

            var again = sim.Submit(new StartCampaign { Definition = CampaignSamples.Skeleton() });
            Assert.AreEqual("campaign_running", again.Reason);
        }

        [Test]
        public void Start_RefusesAnInvalidDefinition()
        {
            var sim = WorldSimFactory.CreateNew(7);
            var d = CampaignSamples.Skeleton();
            d.FormatVersion = 99;
            var r = sim.Submit(new StartCampaign { Definition = d });
            Assert.AreEqual("invalid_definition", r.Reason);
            StringAssert.StartsWith("That campaign cannot be played: ", r.Message);
            Assert.AreEqual(ProblemCodes.BadVersion, CampaignValidator.Validate(d)[0].Code);
            Assert.AreEqual(0, CampaignValidator.Validate(CampaignSamples.Skeleton()).Count(p => p.IsError));
            Assert.AreEqual(ProblemCodes.Missing, CampaignValidator.Validate(null)[0].Code);
        }

        [Test]
        public void Start_StolenAtStart_IsOnItsAltar_AndPaysTheAshClan()
        {
            var d = CampaignSamples.Skeleton();
            d.Rival.StolenAtStart.Add("earth");
            var sim = WorldSimFactory.CreateNew(7);
            sim.Submit(new StartCampaign { Definition = d });
            var ev = SimTestKit.Step(sim, 1);
            var s = sim.State;
            Assert.AreEqual(Custody.AtRivalAltar, CampaignApi.Crystal(s, Element.Earth).Custody);
            Assert.AreEqual(new MarkerRef("ash_cut", "quarry", "altar"), CampaignApi.Crystal(s, Element.Earth).At);
            Assert.AreEqual(Custody.AtSite, CampaignApi.Belief(s, Element.Earth).Custody, "the party does not know yet");
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Rival, "crystal:earth"));
            Assert.AreEqual(1, ev.OfType<PointsAwarded>().Count(p => p.Faction == PlayerIds.Rival && p.Key == "crystal:earth"));
            CollectionAssert.AreEqual(new[] { Element.Earth }, s.Campaign.EverOnAltar);
        }

        // ---------------------------------------------------------------- custody and points

        [Test]
        public void Custody_OneEventPerChange_PointsPaidOnce()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            var ev = SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.AtRivalAltar });
            Assert.AreEqual(1, ev.OfType<CrystalCustodyChanged>().Count());
            Assert.AreEqual(1, LedgerApi.Points(s, PlayerIds.Rival));
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Carried });
            ev = SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.AtRivalAltar });
            Assert.AreEqual(1, LedgerApi.Points(s, PlayerIds.Rival), "the Ash key is paid once, the first time only");
            Assert.AreEqual(0, SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.AtRivalAltar }).OfType<CrystalCustodyChanged>().Count(), "no change, no event");
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Fire, To = Custody.Restored });
            Assert.AreEqual(1, LedgerApi.Points(s, PlayerIds.Human));
            Assert.IsTrue(LedgerApi.IsPaid(s, PlayerIds.Human, "crystal:fire"));
            CollectionAssert.AreEqual(new[] { Element.Fire }, s.Campaign.EverOnAltar);
            Assert.AreEqual(new[] { Element.Fire }, CampaignApi.Record(s).EverOnAltar.ToArray());
        }

        // ---------------------------------------------------------------- attempts

        private static void PutAtThreshold(WorldState s, string encounter)
        {
            // Test set-up only: in play the runtime (I1) brings the party here.
            s.Campaign.Party.Mode = PartyMode.AtThreshold;
            s.Campaign.Party.EncounterId = encounter;
        }

        [Test]
        public void Attempt_CommitAndResolve_AppliesOnce()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            Assert.AreEqual("not_at_threshold", sim.Submit(new CommitEncounter { EncounterId = "keeper_earth" }).Reason);
            PutAtThreshold(s, "keeper_earth");
            Assert.AreEqual("not_at_threshold", sim.Submit(new CommitEncounter { EncounterId = "keeper_fire" }).Reason);
            s.Campaign.Party.PlayerPresent = false;
            Assert.AreEqual("player_needed", sim.Submit(new CommitEncounter { EncounterId = "keeper_earth" }).Reason, "Mira never commits alone");
            s.Campaign.Party.PlayerPresent = true;

            var ev = SimTestKit.Do(sim, new CommitEncounter { EncounterId = "keeper_earth" });
            var committed = ev.OfType<EncounterCommitted>().Single();
            Assert.AreEqual(1, committed.AttemptId);
            Assert.AreEqual("garrick", committed.Volunteer);
            Assert.AreEqual("keeper", committed.Profile);
            Assert.AreEqual(PartyMode.InEncounter, CampaignApi.Party(s).Mode);
            var view = CampaignApi.Attempt(s);
            Assert.AreEqual(1, view.Id);
            Assert.AreEqual(committed.Seed, view.Seed);
            Assert.AreEqual(EncounterKind.Lethal, view.Kind);

            // Restart on Continue: a new seed, no penalty.
            ev = SimTestKit.Do(sim, new RestartAttempt { AttemptId = 1 });
            Assert.AreEqual(1, CampaignApi.Attempt(s).Restarts);
            Assert.AreNotEqual(committed.Seed, CampaignApi.Attempt(s).Seed);
            Assert.AreEqual(1, ev.OfType<EncounterCommitted>().Count());

            Assert.AreEqual("unknown_attempt", sim.Submit(new ResolveEncounter { AttemptId = 2, Result = DuelResult.Victory }).Reason);
            // Two resolutions in the same tick (a second callback): the second is refused at the tick boundary.
            Assert.IsTrue(sim.Submit(new ResolveEncounter { AttemptId = 1, Result = DuelResult.Defeat }).Ok);
            Assert.IsTrue(sim.Submit(new ResolveEncounter { AttemptId = 1, Result = DuelResult.Defeat }).Ok);
            ev = SimTestKit.Step(sim, 1);
            Assert.AreEqual(1, ev.OfType<EncounterResolved>().Count());
            var rejected = ev.OfType<CommandRejected>().Single();
            Assert.AreEqual("already_resolved", rejected.Result.Reason);
            Assert.AreEqual(1, s.Campaign.Resolved.Count);
            Assert.AreEqual(DuelResult.Defeat, s.Campaign.Resolved[0].Result);
            Assert.AreEqual("garrick", s.Campaign.Resolved[0].Volunteer);
            Assert.IsNull(CampaignApi.Attempt(s));
            Assert.AreEqual("already_resolved", sim.Submit(new ResolveEncounter { AttemptId = 1, Result = DuelResult.Victory }).Reason);
            Assert.AreEqual("already_resolved", sim.Submit(new RestartAttempt { AttemptId = 1 }).Reason);
            // Even after the run ended, a late duplicate is "already settled".
            s.Campaign.Phase = CampaignPhase.Lost;
            Assert.AreEqual("already_resolved", sim.Submit(new ResolveEncounter { AttemptId = 1, Result = DuelResult.Victory }).Reason);
            Assert.AreEqual("campaign_over", sim.Submit(new SetCompany { Present = true }).Reason);
        }

        [Test]
        public void Refusals_FromState()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            Assert.AreEqual("unknown_lead", sim.Submit(new AcceptLead { LeadId = 99 }).Reason);
            Assert.AreEqual("unknown_lead", sim.Submit(new DeclineLead { LeadId = 99 }).Reason);
            Assert.AreEqual("not_at_choice", sim.Submit(new AdviseMira { ChoiceId = "air_test", Answer = 1 }).Reason);
            s.Campaign.Party.Mode = PartyMode.AskingChoice;
            s.Campaign.Party.ChoiceId = "air_test";
            Assert.IsTrue(sim.Submit(new AdviseMira { ChoiceId = "air_test", Answer = 1 }).Ok);
            Assert.AreEqual("not_at_choice", sim.Submit(new AdviseMira { ChoiceId = "air_test", Answer = 3 }).Reason, "only three answers");
            s.Campaign.Party.Mode = PartyMode.Idle;

            Assert.AreEqual("unknown_place", sim.Submit(new SuggestScout { LocationId = "nowhere" }).Reason);
            Assert.IsTrue(sim.Submit(new SuggestScout { LocationId = "cliff" }).Ok);
            s.Campaign.Scout.Out = true;
            s.Campaign.Scout.CompanionId = "tansy";
            var r = sim.Submit(new SuggestScout { LocationId = "cliff" });
            Assert.AreEqual("scout_out", r.Reason);
            Assert.AreEqual("Wait for Tansy Reed to come back first.", r.Message);
            PutAtThreshold(s, "keeper_earth");
            Assert.AreEqual("scout_out", sim.Submit(new CommitEncounter { EncounterId = "keeper_earth" }).Reason, "the party regroups before a lethal duel");
            s.Campaign.Scout.Out = false;
            s.Campaign.Companion("tansy").Alive = false;
            Assert.AreEqual("no_scout", sim.Submit(new SuggestScout { LocationId = "cliff" }).Reason);
            Assert.IsTrue(sim.Submit(new WithdrawFromEncounter { EncounterId = "keeper_earth" }).Ok);
            Assert.AreEqual("not_at_threshold", sim.Submit(new WithdrawFromEncounter { EncounterId = "keeper_air" }).Reason);
            Assert.AreEqual("not_at_threshold", sim.Submit(new CommitEncounter { EncounterId = "orrin_demo" }).Reason, "only lethal encounters make attempts");

            Assert.AreEqual("not_carried", sim.Submit(new ConfirmRestore { Crystal = Element.Earth }).Reason);
            s.Campaign.Crystal(Element.Earth).Custody = Custody.Carried;
            s.Campaign.Party.Mode = PartyMode.Idle;
            Assert.AreEqual("wrong_tower", sim.Submit(new ConfirmRestore { Crystal = Element.Earth }).Reason);
            s.Campaign.Party.At = MarkerRef.Surface("tower_fire", "door");
            Assert.AreEqual("wrong_tower", sim.Submit(new ConfirmRestore { Crystal = Element.Earth }).Reason);
            s.Campaign.Party.At = MarkerRef.Surface("tower_earth", "door");
            Assert.IsTrue(sim.Submit(new ConfirmRestore { Crystal = Element.Earth }).Ok);
            s.Campaign.Party.PlayerPresent = false;
            Assert.AreEqual("player_needed", sim.Submit(new ConfirmRestore { Crystal = Element.Earth }).Reason, "she does not restore without the player");
        }

        [Test]
        public void SetCompany_And_DevCommands()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            SimTestKit.Do(sim, new SetCompany { Present = false });
            Assert.IsFalse(CampaignApi.Party(s).PlayerPresent);
            SimTestKit.Do(sim, new SetCompany { Present = true });
            Assert.IsTrue(CampaignApi.Party(s).PlayerPresent);
            SimTestKit.Do(sim, new DevSetRivalProgress { Value = 4000 });
            Assert.AreEqual(4001, s.Campaign.RivalProgress, "set at the tick boundary, then that tick's +1");
            SimTestKit.Do(sim, new DevSetCompanion { Id = "garrick", Alive = false });
            Assert.AreEqual("ivo", CampaignApi.NextVolunteer(s).Id);
            var rec = CampaignApi.Record(s);
            Assert.AreEqual(5, rec.Survivors.Count);
            Assert.AreEqual("garrick", rec.Fallen.Single().Id);
            Assert.AreEqual("Garrick Holt", rec.Fallen.Single().Name);
            Assert.AreEqual("sample_v1", rec.DefinitionId);
            SimTestKit.Do(sim, new DevSetCompanion { Id = "garrick", Alive = true });
            Assert.AreEqual("garrick", CampaignApi.NextVolunteer(s).Id);
            foreach (var k in CampaignSamples.CompanionIds) SimTestKit.Do(sim, new DevSetCompanion { Id = k, Alive = false });
            Assert.IsNull(CampaignApi.NextVolunteer(s), "zero companions: no volunteer");
            Assert.AreEqual(0, CampaignApi.Record(s).Survivors.Count);
        }

        // ---------------------------------------------------------------- save

        [Test]
        public void Save_RunningCampaign_RoundTripsAndContinuesIdentically()
        {
            var sim = CampaignSamples.Started(9);
            var s = sim.State;
            PutAtThreshold(s, "keeper_earth");
            SimTestKit.Do(sim, new CommitEncounter { EncounterId = "keeper_earth" });
            sim.Step(20);
            string json = SaveCodec.ToJson(s);
            var loaded = SaveCodec.Load(json, out _);
            CampaignModule.Register(loaded);   // until SaveCodec.CreateSim registers it (requested of PRD_AGES)
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(loaded.State));
            Assert.AreEqual(1, CampaignApi.Attempt(loaded.State).Id, "the pending attempt is in the save");
            Assert.AreEqual('P', loaded.State.Campaign.Definition.Interior("quarry").Rows[1][3]);
            Assert.AreEqual(MarkerKind.Door, loaded.State.Campaign.Definition.Interior("quarry").Legend['X'].Kind);
            Assert.AreEqual(s.Campaign.DefinitionHash, CampaignLoader.Hash(loaded.State.Campaign.Definition), "the saved definition is the same definition");
            foreach (var x in new[] { sim, loaded })
            {
                x.Submit(new ResolveEncounter { AttemptId = 1, Result = DuelResult.Victory });
                x.Step(50);
            }
            Assert.AreEqual(SaveCodec.StateJson(s), SaveCodec.StateJson(loaded.State));
        }

        // ---------------------------------------------------------------- read helpers

        [Test]
        public void Lines_GuardsSpeakersTokens()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            Assert.AreEqual("the Deepstone lies under old workings, at the Old Quarry.", CampaignApi.Line(s, "mira_open_1"));
            Assert.AreEqual("If this goes badly, Garrick Holt holds the way out.", CampaignApi.Line(s, "mira_stakes"));
            Assert.AreEqual("0 set, 6 still with us.", CampaignApi.Line(s, "mira_count"));
            Assert.IsNotNull(CampaignApi.Line(s, "odo_marks"));
            Assert.IsNull(CampaignApi.Line(s, "mira_for_garrick"));
            Assert.IsNull(CampaignApi.Line(s, "mira_one_lit"));
            Assert.IsNull(CampaignApi.Line(s, "no_such_line"));
            SimTestKit.Do(sim, new DevSetCompanion { Id = "odo", Alive = false });
            SimTestKit.Do(sim, new DevSetCompanion { Id = "garrick", Alive = false });
            Assert.IsNull(CampaignApi.Line(s, "odo_marks"), "no dead character speaks");
            Assert.IsNull(CampaignApi.Line(s, "line_odo"));
            Assert.AreEqual("Garrick held the stair.", CampaignApi.Line(s, "mira_for_garrick"));
            Assert.AreEqual("If this goes badly, Ivo Quill holds the way out.", CampaignApi.Line(s, "mira_stakes"));
            SimTestKit.Do(sim, new DevSetCustody { Crystal = Element.Air, To = Custody.AtRivalAltar });
            s.Campaign.Crystal(Element.Air).At = MarkerRef.Surface("cliff", "altar");
            Assert.AreEqual("Your Skyshard is at the Cliff Stair.", CampaignApi.Line(s, "orrin_skyshard"));
            s.Campaign.RestoredOrder.Add(Element.Fire);
            Assert.AreEqual("One tower lit.", CampaignApi.Line(s, "mira_one_lit"));
            CollectionAssert.AreEqual(new[] { "1 set, 4 still with us.", "One tower lit." }, CampaignApi.LinesFor(s, "mira", "status"));
            Assert.IsTrue(CampaignGuards.TryParse("custody: earth = AtRivalAltar", out var k, out var subj, out var val));
            Assert.AreEqual(("custody", "earth", "AtRivalAltar"), (k, subj, val));
            Assert.IsFalse(CampaignGuards.TryParse("weather: rain", out _, out _, out _));
            Assert.AreEqual("{nonsense.token}", CampaignTokens.Fill(s.Campaign, "{nonsense.token}"), "unknown tokens are left for the validator");
        }

        [Test]
        public void Belief_GoesStale_WithWhereToLook_RivalViewShowsOnlyDueReports()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            s.Campaign.Reports.Add(new RivalReport { Crystal = Element.Fire, Kind = RivalReportKind.Marching, At = MarkerRef.Surface("kiln", "pedestal"), Tick = s.Tick, DueTick = s.Tick + 300 });
            Assert.AreEqual(0, CampaignApi.RivalView(s).Reports.Count, "not yet reached the party");
            sim.Step(3001);
            var b = CampaignApi.Belief(s, Element.Earth);
            Assert.IsTrue(b.Stale);
            Assert.AreEqual("The last word put the Deepstone at the Old Quarry. Tansy Reed could go and look.", b.WhereToLook);
            var rv = CampaignApi.RivalView(s);
            Assert.AreEqual(1, rv.Reports.Count);
            Assert.AreEqual(s.Campaign.Reports[0].DueTick, rv.LastReportTick);
            SimTestKit.Do(sim, new DevSetCompanion { Id = "tansy", Alive = false });
            Assert.AreEqual("The last word put the Deepstone at the Old Quarry. Orrin may know more.", CampaignApi.Belief(s, Element.Earth).WhereToLook);
            Assert.AreEqual("Mira is at the home camp.", CampaignApi.WhereIsMira(s));
        }

        [Test]
        public void PartyPosition_LegCells_CausewayAndConnections()
        {
            var sim = CampaignSamples.Started();
            var s = sim.State;
            var p = s.Campaign.Party;
            p.Mode = PartyMode.Travelling;
            p.Leg = new LegState { From = MarkerRef.Surface("home", "fire"), To = MarkerRef.Surface("glade", "stone"), Ticks = 10, Progress = 4, Cells = new[] { 1, 2, 3, 4 } };
            Assert.IsTrue(CampaignApi.PartyPosition(s, 0.5, out var from, out var to, out var t));
            Assert.AreEqual(MarkerRef.Surface("home", "fire"), from);
            Assert.AreEqual(MarkerRef.Surface("glade", "stone"), to);
            Assert.AreEqual(0.45, t, 1e-9);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, CampaignApi.LegCells(s));
            Assert.AreEqual("Mira is on her way to the Stonemark Glade.", CampaignApi.WhereIsMira(s));

            Assert.IsFalse(CampaignApi.IsOpen(s, "glade_stair"));
            s.Campaign.Facts.Add("earth.stair_open");
            s.Campaign.Facts.Sort(StringComparer.Ordinal);
            Assert.IsTrue(CampaignApi.IsOpen(s, "glade_stair"));
            Assert.IsFalse(CampaignApi.IsOpen(s, "no_such"));

            // The tide: dry for 900 ticks at the start of every 2400 from the campaign's start.
            long start = s.Campaign.StartTick;
            Assert.AreEqual((s.Tick - start) % 2400 < 900, CampaignApi.CausewayDry(s));
            int dry = 0;
            for (int i = 0; i < 2400; i++) { sim.Step(); if (CampaignApi.CausewayDry(s)) dry++; }
            Assert.AreEqual(900, dry);
        }

        // ---------------------------------------------------------------- interiors and the lattice

        [Test]
        public void Interiors_FloorMarkersAndPaths()
        {
            var m = CampaignSamples.Skeleton().Interior("quarry");
            Assert.IsFalse(InteriorMaps.IsFloor(m, 0, 0));
            Assert.IsTrue(InteriorMaps.IsFloor(m, 1, 1));
            Assert.IsTrue(InteriorMaps.IsFloor(m, 3, 1), "a legend cell is floor");
            Assert.IsFalse(InteriorMaps.IsFloor(m, 40, 1));
            Assert.IsFalse(InteriorMaps.IsFloor(m, 1, -1));
            Assert.IsTrue(InteriorMaps.TryFind(m, "exit", out int ei, out int ej));
            Assert.AreEqual((3, 4), (ei, ej));
            Assert.IsTrue(InteriorMaps.TryFind(m, "pedestal", out int pi, out int pj));
            Assert.AreEqual((3, 1), (pi, pj));
            Assert.AreEqual(6, InteriorMaps.Markers(m).Count);
            Assert.AreEqual(InteriorMaps.FloorCells(m).Length / 2, CountFloor(m));

            var path = InteriorMaps.Path(m, ei, ej, pi, pj, null);
            Assert.Greater(path.Length, 0);
            Assert.AreEqual((ei, ej), (path[0], path[1]));
            Assert.AreEqual((pi, pj), (path[path.Length - 2], path[path.Length - 1]));
            for (int k = 2; k < path.Length; k += 2)
            {
                Assert.AreEqual(1, Lattice.Distance(path[k - 2], path[k - 1], path[k], path[k + 1]), "one of the six moves");
                Assert.IsTrue(InteriorMaps.IsFloor(m, path[k], path[k + 1]));
            }
            CollectionAssert.AreEqual(path, InteriorMaps.Path(m, ei, ej, pi, pj, null), "deterministic");

            Assert.IsTrue(InteriorMaps.TryFind(m, "door", out int di, out int dj));
            Assert.AreEqual(0, InteriorMaps.Path(m, ei, ej, di, dj, null).Length, "a barred door is shut");
            Assert.AreEqual(0, InteriorMaps.Path(m, ei, ej, di, dj, f => false).Length);
            Assert.Greater(InteriorMaps.Path(m, ei, ej, di, dj, f => f == "earth.ash_cut_open").Length, 0, "open once its fact is known");
            Assert.AreEqual(0, InteriorMaps.Path(m, ei, ej, 0, 0, null).Length, "rock is never reached");
        }

        private static int CountFloor(InteriorDef m)
        {
            int n = 0;
            for (int j = 0; j < m.Rows.Count; j++) for (int i = 0; i < m.Rows[j].Length; i++) if (m.Rows[j][i] != '#') n++;
            return n;
        }

        /// <summary>The Sim lattice has the walker's cells (WalkLattice: X = 5 * sqrt3/2 * i, Z = 5 * (i/2 + j), cube rounding).</summary>
        [Test]
        public void Lattice_SameCellsAsTheWalker()
        {
            double colX = 5 * Math.Sqrt(3) / 2;
            for (int i = -60; i <= 60; i += 3)
                for (int j = -60; j <= 60; j += 5)
                {
                    double x = colX * i, z = 5 * (i * 0.5 + j);
                    Assert.AreEqual(x, Lattice.X(i, j), 1e-9);
                    Assert.AreEqual(z, Lattice.Z(i, j), 1e-9);
                    Lattice.CellOf(x, z, out int ci, out int cj);
                    Assert.AreEqual((i, j), (ci, cj));
                    Lattice.CellOf(x + 1.2, z - 1.4, out ci, out cj);
                    Assert.AreEqual((i, j), (ci, cj), "a point within a cell rounds to it");
                }
            // Hex centres are lattice cells (the board and the lattice share the origin at the centre of hex 30).
            var topo = BoardTopology.Get(4);
            for (int h = 0; h < topo.HexCount; h++)
            {
                Lattice.CellOf(topo.HexX(h), topo.HexZ(h), out int hi, out int hj);
                Assert.AreEqual(topo.HexX(h), Lattice.X(hi, hj), 1e-6, "hex " + h);
                Assert.AreEqual(topo.HexZ(h), Lattice.Z(hi, hj), 1e-6, "hex " + h);
            }
            Assert.AreEqual(0, Lattice.Distance(2, 3, 2, 3));
            int[] steps = { 0, 1, 1, 0, 1, -1, 0, -1, -1, 0, -1, 1 };
            for (int d = 0; d < 6; d++) Assert.AreEqual(1, Lattice.Distance(0, 0, steps[2 * d], steps[2 * d + 1]));
            Assert.AreEqual(2, Lattice.Distance(0, 0, 1, 1));
            Assert.AreEqual(7, Lattice.Distance(-3, 2, 4, -5));
            Assert.AreEqual(Lattice.Distance(4, -5, -3, 2), Lattice.Distance(-3, 2, 4, -5));
        }

        // ---------------------------------------------------------------- loader and hash

        [Test]
        public void Loader_MergesFiles_HashIsStable()
        {
            var files = new Dictionary<string, string>
            {
                { "campaign.json", "{ \"FormatVersion\": 1, \"Id\": \"tiny\", \"Status\": \"Complete\", \"Companions\": [ { \"Id\": \"garrick\", \"VolunteerOrder\": 1 } ], \"Tuning\": { \"StaleTicks\": 1234 } }" },
                { "earth.json", "{ \"Locations\": [ { \"Id\": \"glade\", \"Kind\": \"Site\", \"Anchor\": { \"Kind\": \"MeadowHexCentre\", \"Q\": 0, \"R\": -1 } } ], \"Interiors\": [ { \"Id\": \"q\", \"Rows\": [ \"#E#\" ], \"Legend\": { \"E\": { \"Id\": \"exit\", \"Kind\": \"Exit\", \"To\": \"glade/stair\" } } } ] }" },
                { "fire.json", "{ \"Companions\": [ { \"Id\": \"ivo\", \"VolunteerOrder\": 2 } ] }" },
            };
            var d = CampaignLoader.LoadFolder(f => files[f], new[] { "campaign.json", "earth.json", "fire.json" });
            Assert.AreEqual("tiny", d.Id);
            Assert.AreEqual(PackStatus.Complete, d.Status);
            CollectionAssert.AreEqual(new[] { "garrick", "ivo" }, d.Companions.Select(c => c.Id).ToArray(), "lists from several files are appended in file order");
            Assert.AreEqual(1234, d.Tuning.StaleTicks);
            Assert.AreEqual(7, d.Tuning.PartyTicksPerStep, "unset tuning keeps its starting value");
            Assert.AreEqual(MarkerKind.Exit, d.Interior("q").Legend['E'].Kind);
            Assert.AreEqual(AnchorKind.MeadowHexCentre, d.Location("glade").Anchor.Kind);
            Assert.IsTrue(InteriorMaps.IsFloor(d.Interior("q"), 1, 0));

            string h = CampaignLoader.Hash(d);
            Assert.AreEqual(64, h.Length);
            Assert.AreEqual(h, CampaignLoader.Hash(CampaignLoader.LoadFolder(f => files[f], new[] { "campaign.json", "earth.json", "fire.json" })));
            d.Title = "changed";
            Assert.AreNotEqual(h, CampaignLoader.Hash(d));
            Assert.AreEqual("", CampaignLoader.Hash(null));
            Assert.Throws<FormatException>(() => CampaignLoader.LoadFolder(f => "{ not json", new[] { "bad.json" }));
            Assert.AreEqual(CampaignLoader.Hash(CampaignSamples.Skeleton()), CampaignLoader.Hash(CampaignSamples.Skeleton()));
        }

        // ---------------------------------------------------------------- Ward opponent

        [Test]
        public void Ward_ProfileFillsMiraFields_CreateEqualsConstructor()
        {
            var keeper = new WardOpponentProfile
            {
                Id = "keeper", DisplayName = "a keeper", FirstCastMinMs = 4000, FirstCastMaxMs = 7000, ThinkMinMs = 8000, ThinkMaxMs = 12000,
                MistakeRatePercent = 15, Pool = new[] { Essence.Flame, Essence.Frost, Essence.Vine },
            };
            var baseTuning = WardTuning.Practice();
            var t = WardTuning.For(keeper, baseTuning);
            Assert.AreNotSame(baseTuning, t);
            Assert.AreEqual((4000, 7000, 8000, 12000, 15), (t.MiraFirstCastMinMs, t.MiraFirstCastMaxMs, t.MiraThinkMinMs, t.MiraThinkMaxMs, t.MiraMistakeRatePercent));
            Assert.AreEqual(baseTuning.MaxCasts, t.MaxCasts);
            Assert.AreEqual(3000, baseTuning.MiraFirstCastMinMs, "the base is not changed");
            Assert.AreEqual(WardTuning.Practice().MiraThinkMinMs, WardTuning.For(null).MiraThinkMinMs);

            var a = WardDuelSession.Create(new WardDuelSpec { Tuning = t, Pool = keeper.Pool, Seed = 42 });
            var b = new WardDuelSession(t, keeper.Pool, 42);
            int[] ward = { 0, 1, 2 };
            Assert.IsTrue(a.SetPlayerWard(ward));
            Assert.IsTrue(b.SetPlayerWard(ward));
            for (int k = 0; k < 400 && a.Phase != WardPhase.Complete; k++) { a.Advance(250); b.Advance(250); }
            Assert.AreEqual(b.Outcome, a.Outcome);
            Assert.AreEqual(b.RivalHistory.Count, a.RivalHistory.Count);
            Assert.IsNotNull(WardDuelSession.Create(null), "a null spec falls back to practice");

            Assert.IsTrue(DuelResults.TryFrom(WardOutcome.Forfeited, out var r));
            Assert.AreEqual(DuelResult.Surrender, r);
            Assert.IsTrue(DuelResults.TryFrom(WardOutcome.Clash, out r));
            Assert.AreEqual(DuelResult.Clash, r);
            Assert.IsFalse(DuelResults.TryFrom(WardOutcome.None, out _));
        }
    }
}
