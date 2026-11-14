using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Economy;
using World.Sim.Eras;
using World.Sim.Factions;
using World.Sim.Ledger;

namespace World.Sim.Tests
{
    /// <summary>
    /// Pins the slice H1 surface of PRD_AGES appendix A (A.1 system order, A.2 eras and landmarks, A.3 ledger skeleton,
    /// A.4 grades, A.5 autonomy and advice) as landed by H1-0. After H1-0 it may only grow by addition (A.9).
    /// </summary>
    public class ApiSurfaceAgesTests
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
            Assert.AreEqual(ret, m.ReturnType, t.Name + "." + name + " returns");
        }

        private static void Const(Type t, string name, object value)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(f, t.Name + "." + name);
            Assert.AreEqual(value, f.GetValue(null), t.Name + "." + name);
        }

        private static void Enum<T>(params (string name, int value)[] members) where T : struct
        {
            var names = System.Enum.GetNames(typeof(T));
            foreach (var (name, value) in members)
            {
                CollectionAssert.Contains(names, name, typeof(T).Name);
                Assert.AreEqual(value, (int)System.Enum.Parse(typeof(T), name), typeof(T).Name + "." + name);
            }
        }

        private static void Command<T>(params (string name, Type type)[] fields) where T : Command
        {
            Assert.IsTrue(typeof(T).IsPublic && !typeof(T).IsAbstract, typeof(T).Name);
            Assert.IsNotNull(typeof(T).GetConstructor(Type.EmptyTypes), typeof(T).Name + " has a default constructor");
            Fields(typeof(T), fields);
        }

        private static void Event<T>(params (string name, Type type)[] fields) where T : SimEvent
        {
            Assert.IsTrue(typeof(T).IsPublic, typeof(T).Name);
            Fields(typeof(T), fields);
        }

        private static readonly Type I = typeof(int), L = typeof(long), D = typeof(double), B = typeof(bool), S = typeof(string),
            IA = typeof(int[]), WS = typeof(WorldState), CR = typeof(CommandResult), BT = typeof(BoardTopology);

        // ---- A.1 ----

        [Test]
        public void A1_SystemOrder()
        {
            Const(typeof(SystemOrder), "Eras", 50);
            Const(typeof(SystemOrder), "Industry", 650);
            Const(typeof(SystemOrder), "Power", 700);
            Const(typeof(SystemOrder), "Barracks", 800);
            Const(typeof(SystemOrder), "Squads", 900);
            Const(typeof(SystemOrder), "Combat", 1000);
            Const(typeof(SystemOrder), "RtsAi", 1160);
            Const(typeof(SystemOrder), "Campaign", 1250);
            Const(typeof(SystemOrder), "Vision", 1270);
            Const(typeof(SystemOrder), "Scenario", 1280);
        }

        // ---- A.2 ----

        [Test]
        public void A2_ErasHistoryLandmarks()
        {
            Enum<Era>(("Prehistoric", 0), ("Historic", 1), ("Modern", 2), ("Future", 3));
            Enum<Eras.PlayMode>(("Skirmish", 0), ("Campaign", 1), ("DevStart", 2));
            Const(typeof(ChapterIds), "Prehistoric", "ch.prehistoric");
            Const(typeof(ChapterIds), "Historic", "ch.historic");
            Const(typeof(ChapterIds), "ModernHarbour", "ch.modern.harbour");
            Const(typeof(ChapterIds), "FutureCore", "ch.future.core");
            Fields(typeof(WorldState), ("Eras", typeof(ErasState)), ("History", typeof(HistoryState)), ("Ledger", typeof(LedgerState)),
                ("Campaign", typeof(CampaignState)));
            Fields(typeof(CampaignState), ("FormatVersion", I));
            Fields(typeof(ErasState), ("Tuning", typeof(ErasTuning)), ("Current", typeof(Era)), ("Mode", typeof(Eras.PlayMode)), ("Chapter", S),
                ("ReadyChapters", typeof(List<string>)), ("ChapterStartTick", L), ("StartId", S), ("CampaignMayStartTick", L));
            Fields(typeof(ErasTuning), ("InviteRepeatTicks", I), ("SmithsChestWares", IA), ("SmithsChestCounts", IA));
            Assert.AreEqual(1800, new ErasTuning().InviteRepeatTicks);
            Fields(typeof(HistoryState), ("Chapters", typeof(List<ChapterRecord>)), ("Casualties", typeof(List<CasualtyRecord>)),
                ("Notes", typeof(List<string>)), ("Outcomes", typeof(SortedDictionary<string, string>)));
            Fields(typeof(ChapterRecord), ("From", S), ("To", S), ("Tick", L), ("Outcomes", typeof(SortedDictionary<string, string>)));
            Fields(typeof(CasualtyRecord), ("Id", S), ("Name", S), ("Chapter", S), ("Cause", S), ("Place", S), ("Tick", L));
            Fields(typeof(Landmark), ("Id", S), ("Corner", I), ("Hex", I), ("HexEdge", I), ("Plot", I), ("State", S));
            Fields(typeof(Board), ("Landmarks", typeof(List<Landmark>)));
            foreach (var (n, v) in new[] { ("HomeHearth", "lm.home_hearth"), ("AshHearth", "lm.ash_hearth"), ("DanceCircle", "lm.dance_circle"),
                         ("Harbour", "lm.harbour"), ("TowerEarth", "lm.tower.earth"), ("TowerWater", "lm.tower.water"), ("TowerAir", "lm.tower.air"),
                         ("TowerFire", "lm.tower.fire"), ("AltarEarth", "lm.altar.earth"), ("AltarWater", "lm.altar.water"),
                         ("AltarAir", "lm.altar.air"), ("AltarFire", "lm.altar.fire"), ("Relay", "lm.relay"), ("District", "lm.district") })
                Const(typeof(LandmarkIds), n, v);
            Method(typeof(LandmarkApi), "Get", typeof(Landmark), WS, S);
            Method(typeof(LandmarkApi), "All", typeof(IReadOnlyList<Landmark>), WS);
            Method(typeof(LandmarkApi), "Register", typeof(void), WS, typeof(Landmark));
            Method(typeof(LandmarkApi), "ShoreCorner", I, BT, D);
            Method(typeof(LandmarkApi), "TowerCorners", IA, BT);
            Method(typeof(LandmarkApi), "HarbourCorner", I, BT, I);

            Command<AdvanceChapter>(("To", S));
            Const(typeof(ErasRefusals), "ChapterNotReady", "chapter_not_ready");
            Const(typeof(ErasRefusals), "ChapterUnknown", "chapter_unknown");
            Assert.AreEqual("That time has not come yet.", ErasRefusals.Sentence("chapter_not_ready"));
            Assert.AreEqual("There is no such chapter.", ErasRefusals.Sentence("chapter_unknown"));
            Enum<CampOverrunKind>(("CampLost", 0), ("Looted", 1));
            Event<ChapterReady>(("Chapter", S));
            Event<ChapterAdvanced>(("From", S), ("To", S));
            Event<PrinceHandover>(("Faction", I));
            Event<Eras.CampaignMayStart>();
            Event<CampOverrun>(("Attacker", I), ("Defender", I), ("SettlementId", I), ("Kind", typeof(CampOverrunKind)));

            var a = typeof(AgesApi);
            Method(a, "Current", typeof(Era), WS);
            Method(a, "Chapter", S, WS);
            Method(a, "Mode", typeof(Eras.PlayMode), WS);
            Method(a, "IsReady", B, WS, S);
            Method(a, "ReadyChapters", typeof(IReadOnlyList<string>), WS);
            Method(a, "Landmark", typeof(Landmark), WS, S);
            Method(a, "HandoverTick", L, WS, I);
            Method(a, "CampaignMayStart", B, WS);
            Method(a, "IsCampaign", B, WS);
            Method(a, "AtLeast", B, WS, typeof(Era));
            Method(a, "EraName", S, typeof(Era));
            Method(typeof(ErasModule), "InitState", typeof(void), WS);
            Method(typeof(ErasModule), "EnsureState", typeof(void), WS);
            Method(typeof(ErasModule), "Register", typeof(void), typeof(WorldSim));
            Method(typeof(ErasModule), "EnterCampaign", typeof(void), WS);
            Method(typeof(ScenarioStarts), "Create", typeof(WorldSim), typeof(string), typeof(ulong), typeof(IDictionary<string, string>));
            Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(ScenarioStarts).GetProperty("Ids").PropertyType);
            Const(typeof(ScenarioStarts), "DevHistoric", "dev.historic");
            Const(typeof(ScenarioStarts), "DevModernHarbour", "dev.modern.harbour");
            Const(typeof(ScenarioStarts), "DevModernSkirmish", "dev.modern.skirmish");
            Const(typeof(ScenarioStarts), "DevFutureCore", "dev.future.core");
            // Crystals N-4: the named modifiers the campaign may write exist.
            Fields(typeof(Demons.DemonTuning), ("SevensPerDraw", I));
            Fields(typeof(EconomyTuning), ("GrowTicks", IA), ("CarrierTicksPerStep", I));
        }

        // ---- A.3 ----

        [Test]
        public void A3_LedgerSkeleton()
        {
            Fields(typeof(LedgerState), ("Factions", typeof(List<FactionLedger>)));
            Fields(typeof(FactionLedger), ("Faction", I), ("Points", I), ("Paid", typeof(SortedSet<string>)), ("Credits", L), ("ReservedCredits", L),
                ("CreditCap", L), ("RosterUnlocked", B), ("RosterUnlockedTick", L), ("CreditsEarnedTrade", L), ("CreditsEarnedRefinery", L),
                ("CreditsSpent", L));
            Const(typeof(LedgerApi), "RosterThreshold", 10);
            Method(typeof(LedgerApi), "Of", typeof(FactionLedger), WS, I);
            Method(typeof(LedgerApi), "AvailableCredits", L, WS, I);
            Method(typeof(LedgerApi), "IsPaid", B, WS, I, S);
            Method(typeof(LedgerApi), "Points", I, WS, I);
            Method(typeof(LedgerApi), "RosterUnlocked", B, WS, I);
            Method(typeof(LedgerApi), "CapGroup", S, S);
            Method(typeof(LedgerApi), "CapOf", I, S);
            Const(typeof(LedgerRefusals), "RosterLocked", "roster_locked");
            Const(typeof(LedgerRefusals), "NoCredits", "no_credits");
            Assert.AreEqual("Our people need 10 victory points to build that (8 of 10).", LedgerRefusals.Sentence("roster_locked", 8));
            Assert.AreEqual("We need 40 more credits.", LedgerRefusals.Sentence("no_credits", 40));
            Event<PointsAwarded>(("Faction", I), ("Key", S), ("Points", I), ("Total", I));
            Event<RosterUnlocked>(("Faction", I));
            Event<CreditsChanged>(("Faction", I), ("Delta", L), ("Reason", S));
        }

        // ---- A.4 ----

        [Test]
        public void A4_GradesTownsOuterPlots()
        {
            Enum<Grade>(("Prehistoric", 0), ("Historic", 1), ("Modern", 2));
            Enum<SiteKind>(("BuildingUpgrade", 4));
            Enum<PlaceKind>(("BuildingUpgrade", 4));
            Fields(typeof(Building), ("Grade", typeof(Grade)), ("Upgrading", B), ("UpgradeDelivered", IA), ("UpgradeIncoming", IA),
                ("UpgradeProgress", L), ("UpgradeCredits", L));
            Fields(typeof(Settlement), ("IsTown", B), ("IsDistrict", B));
            Fields(typeof(GradeDef), ("To", typeof(Grade)), ("Goods", IA), ("Credits", I), ("Ticks", I), ("Benefit", S), ("CycleTicks", I),
                ("YieldPerUnit", I), ("InputBufferCycles", I), ("Garrison", I), ("Sight", I));
            Fields(typeof(BuildingDef), ("BornGrade", typeof(Grade)), ("Grades", typeof(GradeDef[])), ("ToolWare", I), ("Era", typeof(Era)));
            Fields(typeof(WareLedger), ("Looted", IA));
            Command<UpgradeBuilding>(("BuildingId", I));
            Command<CancelUpgrade>(("BuildingId", I));
            Command<UpgradeSettlement>(("Corner", I));
            Event<UpgradeStarted>(("BuildingId", I), ("Grade", typeof(Grade)));
            Event<BuildingUpgraded>(("BuildingId", I), ("Grade", typeof(Grade)));
            Event<UpgradeCancelled>(("BuildingId", I));
            Event<TownUpgraded>(("SettlementId", I), ("Corner", I), ("Faction", I));
            Event<DistrictUpgraded>(("SettlementId", I), ("Corner", I), ("Faction", I));
            foreach (var (n, v, sentence) in new[] {
                         ("WrongEra", "wrong_era", "That needs the Historic age."), ("MaxGrade", "max_grade", "It cannot be improved further."),
                         ("UnknownBuilding", "unknown_building", "There is no building there."), ("NotActive", "not_active", "It must be finished first."),
                         ("NotUpgrading", "not_upgrading", "Nothing is being improved there."), ("NotACity", "not_a_city", "This camp must become a city first."),
                         ("PlotBlockedByNeighbour", "plot_blocked_by_neighbour", "A neighbouring town already uses that ground.") })
            {
                Const(typeof(EconomyRefusals), n, v);
                Assert.AreEqual(sentence, EconomyRefusals.Sentence(v, "Historic"));
            }
            Assert.AreEqual("That building is already being improved.", EconomyRefusals.Sentence("upgrade_in_progress", "building"));
            Assert.AreEqual("This city is already being raised to a town.", EconomyRefusals.Sentence("upgrade_in_progress", "town"));
            Assert.AreEqual("This camp is already being raised to a city.", EconomyRefusals.Sentence("upgrade_in_progress"));

            var tu = new EconomyTuning();
            Assert.AreEqual(new[] { 4, 4, 2, 4 }, new[] { tu.TownCost[(int)Ware.Planks], tu.TownCost[(int)Ware.Bricks], tu.TownCost[(int)Ware.Bread], tu.TownCost[(int)Ware.CutStone] });
            Assert.AreEqual(new[] { 24, 600, 2, 6, 400, 400, 2, 8 }, new[] { tu.TownToolWare, tu.TownBuildTicks, tu.CarriersPerTown, tu.TownShipmentSize,
                tu.DistrictCredits, tu.DistrictBuildTicks, tu.CarriersPerDistrict, tu.DistrictShipmentSize });

            // The grade table of 5.6 for the 16 types.
            foreach (var d in BuildingDefs.All)
            {
                Assert.AreEqual(Grade.Prehistoric, d.BornGrade, d.Name);
                Assert.AreEqual(Era.Prehistoric, d.Era, d.Name);
                Assert.AreEqual(3, d.Grades.Length, d.Name);
                for (int g = 0; g < 3; g++) Assert.AreEqual((Grade)g, d.Grades[g].To, d.Name);
                Assert.AreEqual(Wares.Count, d.Grades[1].Goods.Length);
                Assert.Greater(d.Grades[1].Goods.Sum(), 0, d.Name + " grade 1 costs goods");
                Assert.AreEqual(0, d.Grades[1].Credits, d.Name);
                Assert.AreEqual(0, d.Grades[2].Goods.Sum(), d.Name + " grade 2 costs no goods");
                Assert.Greater(d.Grades[2].Credits, 0, d.Name + " grade 2 costs credits");
                Assert.IsFalse(string.IsNullOrEmpty(d.Grades[1].Benefit), d.Name);
            }
            var saw = BuildingDefs.Get(BuildingType.Sawmill);
            Assert.AreEqual(new[] { 65, 3, 150, 50, GradeDefs.Saw }, new[] { saw.Grades[1].CycleTicks, saw.Grades[1].InputBufferCycles, saw.Grades[2].Credits, saw.Grades[2].CycleTicks, saw.ToolWare });
            Assert.AreEqual(3, BuildingDefs.Get(BuildingType.Farm).Grades[1].YieldPerUnit);
            Assert.AreEqual(70, BuildingDefs.Get(BuildingType.LogPile).Grades[1].CycleTicks);
            Assert.AreEqual(1, BuildingDefs.Get(BuildingType.LogPile).Grades[1].Goods[(int)Ware.Planks]);
            Assert.AreEqual(-1, BuildingDefs.Get(BuildingType.Bakery).ToolWare);

            var o = typeof(PlotLayoutOuter);
            Const(o, "OuterBase", 1350);
            Method(o, "OuterPlotId", I, I, I, I);
            Method(o, "IsOuter", B, I);
            Method(o, "CornerOf", I, I);
            Method(o, "SectorOf", I, I);
            Method(o, "SlotOf", I, I);
            Method(o, "IsValidOuterPlot", B, BT, I);
            Method(o, "Centre", B, BT, I, D.MakeByRefType(), D.MakeByRefType());
            Method(o, "Yaw", D, BT, I);
            Assert.AreEqual(1350 + 70 * 6 + 1 * 2 + 1, PlotLayoutOuter.OuterPlotId(70, 1, 4));
            Assert.AreEqual(-1, PlotLayoutOuter.OuterPlotId(70, 1, 2));
            for (int c = 0; c < 150; c++)
                for (int sec = 0; sec < 3; sec++)
                    for (int slot = 3; slot <= 4; slot++)
                    {
                        int p = PlotLayoutOuter.OuterPlotId(c, sec, slot);
                        Assert.IsTrue(PlotLayoutOuter.IsOuter(p));
                        Assert.AreEqual((c, sec, slot), (PlotLayoutOuter.CornerOf(p), PlotLayoutOuter.SectorOf(p), PlotLayoutOuter.SlotOf(p)));
                    }
            Assert.IsFalse(PlotLayoutOuter.IsOuter(1349));

            Fields(typeof(EconomyHooks), ("OnConstructionDelivery", typeof(List<Action<WorldState, EventBuffer, int, SiteKind, int, Ware, int>>)));
        }

        // ---- A.5 ----

        [Test]
        public void A5_AutonomyAndAdvice()
        {
            Fields(typeof(Faction), ("HandoverTick", L), ("AnnounceTicks", I), ("Personality", S), ("Advice", typeof(List<AdviceRecord>)),
                ("SetbackUntilTick", L));
            Fields(typeof(FactionsState), ("Halts", typeof(List<SettlementHalt>)));
            Fields(typeof(SettlementHalt), ("SettlementId", I), ("UntilTick", L));
            Enum<AdviceKind>(("Priority", 0), ("Favour", 1), ("Veto", 2), ("FocusWare", 3), ("AttackTarget", 4), ("HoldTarget", 5), ("ScoutTarget", 6));
            Enum<PriorityClass>(("Economy", 0), ("Expansion", 1), ("Military", 2), ("Defence", 3), ("Trade", 4));
            Command<Advise>(("Faction", I), ("Kind", typeof(AdviceKind)), ("Priority", typeof(PriorityClass)), ("ProposalId", I), ("Ware", I),
                ("TargetKind", I), ("TargetId", I), ("Cell", I));
            Fields(typeof(AdviceRecord), ("Kind", typeof(AdviceKind)), ("Priority", typeof(PriorityClass)), ("Ware", I), ("TargetKind", I),
                ("TargetId", I), ("Cell", I), ("UntilTick", L));
            Fields(typeof(Explanation), ("Doing", S), ("Why", S), ("Blocked", S), ("TopProposalIds", IA));
            Method(typeof(FactionApi), "Explain", typeof(Explanation), WS, I);
            Method(typeof(FactionApi), "AdviceInForce", typeof(IReadOnlyList<AdviceRecord>), WS, I);
            Event<AdviceTaken>(("Faction", I), ("Kind", typeof(AdviceKind)));
            Event<AdviceDeclined>(("Faction", I), ("Kind", typeof(AdviceKind)), ("Sentence", S));
            Event<ActionAnnounced>(("Faction", I), ("ProposalId", I), ("ExecuteTick", L));
            foreach (var (n, v) in new[] { ("NotOwner", "not_owner"), ("UnknownAdvice", "unknown_advice"), ("UnknownWare", "unknown_ware"), ("AdviceNotYet", "advice_not_yet") })
                Const(typeof(FactionRefusals), n, v);
            Const(typeof(Personalities), "Passive", "passive");
            Const(typeof(Personalities), "Balanced", "balanced");
            Const(typeof(Personalities), "Aggressive", "aggressive");
            var ft = new FactionTuning();
            Assert.AreEqual(new[] { 150, 3000, 1800, 40, 3000, 30, 3000, 600, 3000, 3 }, new[] { ft.AnnounceTicks, ft.SetbackGraceTicks, ft.LootHaltTicks,
                ft.AdvicePriorityBonus, ft.AdvicePriorityTicks, ft.AdviceFocusBonus, ft.AdviceFocusTicks, ft.AdviceVetoTicks, ft.AdviceHoldTicks, ft.StrengthPerTown });
            var fa = new Faction();
            Assert.AreEqual(-1, fa.HandoverTick);
            Assert.AreEqual(150, fa.AnnounceTicks);
            Assert.AreEqual("balanced", fa.Personality);
        }

        [Test]
        public void EveryNewCommandHasAHandler_AlsoAfterLoad()
        {
            var types = new[] { typeof(AdvanceChapter), typeof(UpgradeBuilding), typeof(CancelUpgrade), typeof(UpgradeSettlement), typeof(Advise) };
            var sim = WorldSimFactory.CreateNew(1);
            foreach (var t in types) Assert.IsTrue(sim.HasHandler(t), t.Name);
            var loaded = Save.SaveCodec.Load(Save.SaveCodec.ToJson(sim.State), out _);
            foreach (var t in types) Assert.IsTrue(loaded.HasHandler(t), t.Name + " after load");
        }
    }

    /// <summary>The safe first-version behaviour of the H1-0 surface: a valid new game, refusals, no exceptions, old behaviour kept.</summary>
    public class AgesFirstVersionTests
    {
        [Test]
        public void NewGame_IsAPrehistoricSkirmish_WithTheStartingLandmarks()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var s = sim.State;
            Assert.AreEqual(Era.Prehistoric, AgesApi.Current(s));
            Assert.AreEqual(Eras.PlayMode.Skirmish, AgesApi.Mode(s));
            Assert.IsFalse(AgesApi.IsCampaign(s));
            Assert.IsFalse(AgesApi.CampaignMayStart(s));
            Assert.IsNull(s.Campaign);
            Assert.AreEqual(2, s.Ledger.Factions.Count);
            Assert.AreEqual(2000, s.Ledger.Factions[0].CreditCap);
            Assert.AreEqual(70, AgesApi.Landmark(s, LandmarkIds.HomeHearth).Corner);
            Assert.AreEqual(97, AgesApi.Landmark(s, LandmarkIds.AshHearth).Corner);
            Assert.AreEqual(635, AgesApi.Landmark(s, LandmarkIds.DanceCircle).Plot);
            var t = s.Board.Topology;
            var towers = LandmarkApi.TowerCorners(t);
            Assert.AreEqual(4, towers.Distinct().Count(), "four distinct tower corners");
            foreach (int c in towers) Assert.Less(t.VertexHexes(c).Count, 3, "towers on rim corners");
            int harbour = AgesApi.Landmark(s, LandmarkIds.Harbour).Corner;
            Assert.Less(t.VertexHexes(harbour).Count, 3, "harbour on a rim corner");
            CollectionAssert.DoesNotContain(towers, harbour);
            Assert.IsNull(AgesApi.Landmark(s, LandmarkIds.TowerEarth), "towers are registered by the campaign only");
            var ids = LandmarkApi.All(s).Select(l => l.Id).ToList();
            CollectionAssert.AreEqual(ids.OrderBy(x => x, StringComparer.Ordinal).ToList(), ids);
            foreach (var b in s.Economy.Buildings) Assert.AreEqual(Grade.Prehistoric, b.Grade);
            Assert.IsTrue(s.Factions.Factions.All(f => f.HandoverTick == -1));
        }

        [Test]
        public void Refusals_InPrehistoric()
        {
            var sim = WorldSimFactory.CreateNew(1);
            var s = sim.State;
            Assert.AreEqual("chapter_not_ready", sim.Check(new AdvanceChapter { To = ChapterIds.Historic }).Reason);
            Assert.AreEqual("chapter_unknown", sim.Check(new AdvanceChapter { To = "ch.nowhere" }).Reason);
            Assert.AreEqual("chapter_unknown", sim.Check(new AdvanceChapter { To = null }).Reason);
            var mine = s.Economy.Buildings.First(b => b.Faction == 0 && b.State == BuildingState.Active);
            var theirs = s.Economy.Buildings.First(b => b.Faction == 1);
            var r = sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = mine.Id });
            Assert.AreEqual("wrong_era", r.Reason);
            Assert.AreEqual("That needs the Historic age.", r.Message);
            Assert.AreEqual("not_owner", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = theirs.Id }).Reason);
            Assert.AreEqual("unknown_building", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = 9999 }).Reason);
            Assert.AreEqual("unknown_building", sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = -1 }).Reason);
            Assert.AreEqual("not_upgrading", sim.Check(new CancelUpgrade { PlayerId = 0, BuildingId = mine.Id }).Reason);
            Assert.AreEqual("not_a_city", sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = 70 }).Reason);
            Assert.AreEqual("not_a_settlement", sim.Check(new UpgradeSettlement { PlayerId = 0, Corner = 97 }).Reason);
            Assert.AreEqual("advice_not_yet", sim.Check(new Advise { PlayerId = 0, Faction = 0, Kind = AdviceKind.AttackTarget }).Reason);
            Assert.AreEqual("not_owner", sim.Check(new Advise { PlayerId = 0, Faction = 1, Kind = AdviceKind.Priority }).Reason);
            Assert.AreEqual("unknown_ware", sim.Check(new Advise { PlayerId = 0, Faction = 0, Kind = AdviceKind.FocusWare, Ware = 99 }).Reason);
            Assert.AreEqual("unknown_proposal", sim.Check(new Advise { PlayerId = 0, Faction = 0, Kind = AdviceKind.Favour, ProposalId = 9999 }).Reason);
            Assert.AreEqual("unknown_advice", sim.Check(new Advise { PlayerId = 0, Faction = 0, Kind = (AdviceKind)42 }).Reason);
        }

        [Test]
        public void DevHistoric_IsValid_RaisesCampaignMayStart_AndUpgradesAreOpen()
        {
            var sim = ScenarioStarts.Create(ScenarioStarts.DevHistoric, 7);
            Assert.IsNotNull(sim);
            Assert.IsNull(ScenarioStarts.Create(ScenarioStarts.DevModernHarbour, 7), "not built yet");
            Assert.IsNull(ScenarioStarts.Create("nonsense", 7));
            var s = sim.State;
            Assert.AreEqual(Era.Historic, AgesApi.Current(s));
            Assert.AreEqual(ChapterIds.Historic, AgesApi.Chapter(s));
            Assert.AreEqual(Eras.PlayMode.DevStart, AgesApi.Mode(s));
            Assert.AreEqual(0, AgesApi.HandoverTick(s, 0));
            Assert.IsTrue(AgesApi.CampaignMayStart(s));
            var ev = SimTestKit.Step(sim, 5);
            Assert.AreEqual(1, ev.OfType<Eras.CampaignMayStart>().Count());
            Assert.AreEqual(1, s.Eras.CampaignMayStartTick);
            Assert.AreEqual(0, SimTestKit.Step(sim, 5).OfType<Eras.CampaignMayStart>().Count(), "once");
            var mine = s.Economy.Buildings.First(b => b.Faction == 0 && b.State == BuildingState.Active);
            Assert.IsTrue(sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = mine.Id }).Ok, "H1-2: grade 1 is real in Historic");
            ErasModule.EnterCampaign(s);
            Assert.IsTrue(AgesApi.IsCampaign(s));
            SimTestKit.Step(sim, 600); // autonomous people keep playing, no exception
            Assert.IsFalse(s.Factions.GameOver);
        }

        [Test]
        public void AdvanceChapter_WhenReady_StepsAtomically()
        {
            var sim = WorldSimFactory.CreateNew(4);
            var s = sim.State;
            var ev = new EventBuffer();
            Assert.IsTrue(ErasLogic.MarkReady(s, ChapterIds.Historic, ev));
            Assert.IsFalse(ErasLogic.MarkReady(s, ChapterIds.Historic, ev), "once");
            Assert.IsFalse(ErasLogic.MarkReady(s, ChapterIds.Prehistoric, ev), "not the current chapter");
            Assert.AreEqual(1, ev.Items.OfType<ChapterReady>().Count());
            Assert.IsTrue(AgesApi.IsReady(s, ChapterIds.Historic));
            var got = SimTestKit.Do(sim, new AdvanceChapter { To = ChapterIds.Historic });
            var adv = got.OfType<ChapterAdvanced>().Single();
            Assert.AreEqual((ChapterIds.Prehistoric, ChapterIds.Historic), (adv.From, adv.To));
            Assert.AreEqual(Era.Historic, AgesApi.Current(s));
            Assert.AreEqual(s.Tick, s.Eras.ChapterStartTick);
            Assert.IsEmpty(s.Eras.ReadyChapters);
            Assert.AreEqual(ChapterIds.Historic, s.History.Chapters.Single().To);
            Assert.IsFalse(AgesApi.CampaignMayStart(s), "no handover yet");
            Assert.AreEqual("chapter_not_ready", sim.Check(new AdvanceChapter { To = ChapterIds.Historic }).Reason);
            var mine = s.Economy.Buildings.First(b => b.Faction == 0 && b.State == BuildingState.Active);
            Assert.IsTrue(sim.Check(new UpgradeBuilding { PlayerId = 0, BuildingId = mine.Id }).Ok, "Historic: past the era check, H1-2 accepts grade 1");
        }

        [Test]
        public void Ledger_AwardIsIdempotent_Capped_AndUnlocksAtTen()
        {
            var s = WorldSimFactory.CreateNew(5).State;
            var ev = new EventBuffer();
            Assert.IsTrue(LedgerLogic.Award(s, 0, "crystal:earth", 1, ev));
            Assert.IsFalse(LedgerLogic.Award(s, 0, "crystal:earth", 1, ev), "paid once");
            Assert.IsTrue(LedgerLogic.Award(s, 1, "crystal:earth", 1, ev), "each faction its own ledger");
            for (int i = 0; i < 6; i++) LedgerLogic.Award(s, 0, "camp:" + i, 1, ev);
            Assert.AreEqual(4, LedgerApi.PaidInGroup(s, 0, "camp"), "cap 4");
            Assert.AreEqual("first:iron", LedgerApi.CapGroup("first:iron"));
            Assert.AreEqual(5, LedgerApi.Points(s, 0));
            Assert.IsFalse(LedgerApi.RosterUnlocked(s, 0));
            for (int i = 0; i < 5; i++) LedgerLogic.Award(s, 0, "mission:test:" + i, 1, ev);
            Assert.AreEqual(10, LedgerApi.Points(s, 0));
            Assert.IsTrue(LedgerApi.RosterUnlocked(s, 0));
            Assert.IsFalse(LedgerApi.RosterUnlocked(s, 1));
            Assert.AreEqual(1, ev.Items.OfType<RosterUnlocked>().Count());
            Assert.AreEqual(11, ev.Items.OfType<PointsAwarded>().Count()); // 2 crystal, 4 camp, 5 mission
        }

        [Test]
        public void Advice_FavourCasts_VetoDeclines_PriorityIsRecorded()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var s = sim.State;
            var top = FactionApi.Top(s, 0);
            var ex = FactionApi.Explain(s, 0);
            Assert.AreEqual(FactionApi.ProposalTitle(s, top.Id), ex.Doing);
            Assert.AreEqual(top.Reason, ex.Why);
            Assert.AreEqual(top.Id, ex.TopProposalIds[0]);
            var ev = SimTestKit.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Favour, ProposalId = top.Id });
            Assert.AreEqual(1, ev.OfType<SpellCast>().Count());
            Assert.AreEqual(1, ev.OfType<AdviceTaken>().Count());
            sim.Step(20);
            var next = FactionApi.Top(s, 0);
            ev = SimTestKit.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Veto, ProposalId = next.Id });
            Assert.AreEqual("declined", ev.OfType<ProposalRemoved>().Single().Why);
            SimTestKit.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.Priority, Priority = PriorityClass.Expansion });
            SimTestKit.Do(sim, new Advise { Faction = 0, Kind = AdviceKind.FocusWare, Ware = (int)Ware.Stone });
            var inForce = FactionApi.AdviceInForce(s, 0);
            Assert.AreEqual(2, inForce.Count);
            Assert.AreEqual(s.Tick - 1 + 3000, inForce[0].UntilTick);
            Assert.AreEqual(FactionApi.Explain(null, 0).TopProposalIds.Length, 0, "null-safe");
        }

        [Test]
        public void CampaignOverrun_LootsTheHome_NoGameEnd_ThenGrace()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var s = sim.State;
            ErasModule.EnterCampaign(s);
            var tu = s.Factions.Tuning;
            tu.WarningGraceTicks = 0;
            tu.OverrunMargin = 0; // the rival "leads" at once: an overrun warning against the player
            tu.WarningTicks = 10;
            tu.TimeLimitTicks = 50;
            var ev = SimTestKit.Step(sim, 40);
            var co = ev.OfType<CampOverrun>().ToList();
            Assert.AreEqual(1, co.Count, string.Join(",", ev.Select(e => e.GetType().Name).Distinct()));
            Assert.AreEqual(CampOverrunKind.Looted, co[0].Kind);
            Assert.AreEqual(CampaignRules.HomeSettlement(s, co[0].Defender), co[0].SettlementId);
            Assert.IsFalse(s.Factions.GameOver);
            Assert.AreEqual(0, ev.OfType<Factions.GameEnded>().Count());
            Assert.Greater(s.Economy.Ledger.Looted.Sum(), 0);
            Assert.IsTrue(CampaignRules.IsHalted(s, co[0].SettlementId));
            int defender = co[0].Defender;
            Assert.AreEqual(co[0].Tick + 3000, s.Factions.Factions[defender].SetbackUntilTick);
            SimTestKit.Step(sim, 100);
            Assert.IsFalse(s.Factions.GameOver, "no time limit in the campaign");
            var w = Economy.EconomyLogic.WaresInWorld(s);
            var l = s.Economy.Ledger;
            for (int i = 0; i < Wares.Count; i++)
                Assert.AreEqual(l.Starting[i] + l.Collected[i] + l.Produced[i], l.Consumed[i] + w[i], "ware balance " + (Ware)i);
        }

        [Test]
        public void Skirmish_OverrunStaysFatal()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var tu = sim.State.Factions.Tuning;
            tu.WarningGraceTicks = 0;
            tu.OverrunMargin = 0;
            tu.WarningTicks = 10;
            var ev = SimTestKit.Step(sim, 40);
            Assert.IsTrue(sim.State.Factions.GameOver);
            Assert.AreEqual(0, ev.OfType<CampOverrun>().Count());
        }

        [Test]
        public void ConstructionDeliveryHook_SeesTheFirstPlanks()
        {
            var sim = WorldSimFactory.CreateNew(SimTestKit.DefaultSeed);
            var seen = new List<(int f, SiteKind k, Ware w)>();
            Economy.EconomyApi.Hooks(sim).OnConstructionDelivery.Add((st, e, f, k, id, w, n) => seen.Add((f, k, w)));
            var top = FactionApi.Top(sim.State, 0);
            SimTestKit.Do(sim, new CastSpell { ProposalId = top.Id });
            SimTestKit.Step(sim, 600);
            Assert.IsTrue(seen.Any(x => x.f == 0 && x.k == SiteKind.Building), "deliveries to the player's building site");
        }

        [Test]
        public void OuterPlots_GeometryFollowsTheRule()
        {
            var t = WorldSimFactory.CreateNew(1).State.Board.Topology;
            int p3 = PlotLayoutOuter.OuterPlotId(70, 0, 3), p4 = PlotLayoutOuter.OuterPlotId(70, 0, 4);
            Assert.IsTrue(PlotLayoutOuter.Centre(t, p3, out double x3, out double z3));
            Assert.IsTrue(PlotLayoutOuter.Centre(t, p4, out double x4, out double z4));
            double cx = t.VertexX(70), cz = t.VertexZ(70);
            Assert.AreEqual(Math.Sqrt(45 * 45 + 17 * 17), Math.Sqrt((x3 - cx) * (x3 - cx) + (z3 - cz) * (z3 - cz)), 1e-6);
            Assert.AreEqual(34, Math.Sqrt((x3 - x4) * (x3 - x4) + (z3 - z4) * (z3 - z4)), 1e-6);
            int slot2 = PlotLayout.PlotId(70, 0, 2);
            double d2 = Math.Sqrt(Math.Pow(x3 - PlotLayout.PlotX(t, slot2), 2) + Math.Pow(z3 - PlotLayout.PlotZ(t, slot2), 2));
            Assert.AreEqual(17, d2, 1e-6, "17 m from slot 2 (5.3)");
            Assert.IsFalse(PlotLayoutOuter.Centre(t, PlotLayoutOuter.OuterPlotId(0, 0, 3), out _, out _) && !PlotLayout.IsSettlementCorner(t, 0));
        }
    }
}
