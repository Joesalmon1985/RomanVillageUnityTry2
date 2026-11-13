using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Pins the public simulation surface of REFACTOR_SPEC appendix A (and the state shapes of 4.3 and the events of 5.3).
    /// Other packages compile against these names: after S-0 the surface may only grow by addition (A.5).
    /// </summary>
    public class ApiSurfaceTests
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
            IA = typeof(int[]), WS = typeof(WorldState), CR = typeof(CommandResult);

        // ---- A.1 ----

        [Test]
        public void A1_CoreEnumsAndConstants()
        {
            Enum<Ware>(("Wood", 0), ("Clay", 1), ("Grain", 2), ("Sheep", 3), ("Stone", 4), ("Planks", 5), ("Bricks", 6), ("Bread", 7), ("Meat", 8), ("CutStone", 9));
            Assert.AreEqual(10, System.Enum.GetValues(typeof(Ware)).Length);
            Const(typeof(Wares), "Count", 10);
            Const(typeof(Wares), "RawCount", 5);
            Fields(typeof(Wares), ("All", typeof(Ware[])), ("Raw", typeof(Ware[])));
            Method(typeof(Wares), "IsRaw", B, typeof(Ware));
            Method(typeof(Wares), "DisplayName", S, typeof(Ware));
            Method(typeof(Wares), "Title", S, typeof(Ware));
            Const(typeof(PlayerIds), "Human", 0);
            Const(typeof(PlayerIds), "Rival", 1);
            Const(typeof(PlayerIds), "Count", 2);
            Fields(typeof(Board), ("Radius", I), ("HexTerrain", typeof(Terrain[])), ("HexNumber", IA), ("StartCorners", IA),
                ("SuggestedSettlementCorners", IA), ("ReservedPlots", IA));
            Assert.AreEqual(typeof(BoardTopology), typeof(Board).GetProperty("Topology").PropertyType);
            Assert.IsNull(typeof(Board).GetField("HomeCityVertex"), "removed (4.3)");
        }

        [Test]
        public void A1_PlotLayout()
        {
            var t = typeof(PlotLayout);
            Const(t, "PlotsPerCorner", 9);
            Const(t, "SlotsPerSector", 3);
            Const(t, "CitySlot", 2);
            Const(t, "PlotRadius", 5.0);
            Const(t, "StorehouseRadius", 3.0);
            Const(t, "StockPatchRadius", 6.0);
            Const(t, "DanceCircleRadius", 7.0);
            Const(t, "StepLength", 5.0);
            var bt = typeof(BoardTopology);
            Method(t, "PlotId", I, I, I, I);
            Method(t, "CornerOf", I, I);
            Method(t, "SectorOf", I, I);
            Method(t, "SlotOf", I, I);
            Method(t, "IsSettlementCorner", B, bt, I);
            Method(t, "SectorHex", I, bt, I, I);
            Method(t, "SectorOfHex", I, bt, I, I);
            Method(t, "PlotHex", I, bt, I);
            Method(t, "PlotX", D, bt, I);
            Method(t, "PlotZ", D, bt, I);
            Method(t, "PlotYaw", D, bt, I);
            Method(t, "PathPoints", typeof(double[]), bt, I);
            Method(t, "LocalSteps", I, I);
        }

        [Test]
        public void A1_EconomyTypes()
        {
            Enum<BuildingType>(("LogPile", 0), ("ClayScrape", 1), ("GrainBasket", 2), ("ThornFold", 3), ("RockHeap", 4),
                ("WoodcutterHut", 5), ("ClayPit", 6), ("Farm", 7), ("ShepherdHut", 8), ("Quarry", 9),
                ("Sawmill", 10), ("Kiln", 11), ("Bakery", 12), ("Smokehouse", 13), ("Stonecutter", 14), ("WarriorLodge", 15));
            Enum<BuildingClass>(("PrimitiveCollector", 0), ("ImprovedCollector", 1), ("Workshop", 2), ("Lodge", 3));
            Enum<BuildingState>(("Site", 0), ("Constructing", 1), ("Active", 2), ("Retiring", 3), ("Retired", 4));
            Enum<SettlementState>(("Site", 0), ("Active", 1));
            Enum<UpgradeState>(("None", 0), ("Site", 1), ("Constructing", 2));
            Enum<RoadState>(("Site", 0), ("Constructing", 1), ("Built", 2));
            Enum<SiteKind>(("Building", 0), ("Road", 1), ("Settlement", 2), ("City", 3));
            Enum<PlaceKind>(("Storehouse", 0), ("Building", 1), ("RoadSite", 2), ("SettlementSite", 3));
            Enum<JobState>(("Waiting", 0), ("ToPickup", 1), ("Carrying", 2));
            Enum<CarrierState>(("Idle", 0), ("ToPickup", 1), ("Carrying", 2), ("Returning", 3));

            Fields(typeof(BuildingDef), ("Type", typeof(BuildingType)), ("Name", S), ("Purpose", S), ("Class", typeof(BuildingClass)),
                ("Terrain", typeof(Terrain)), ("Cost", IA), ("Inputs", IA), ("Outputs", IA), ("Yield", I), ("WorkTicks", I),
                ("BuildTicks", I), ("Placeable", B));
            Const(typeof(BuildingDefs), "Count", 16);
            Method(typeof(BuildingDefs), "Get", typeof(BuildingDef), typeof(BuildingType));
            Assert.AreEqual(typeof(IReadOnlyList<BuildingDef>), typeof(BuildingDefs).GetProperty("All").PropertyType);
            Method(typeof(BuildingDefs), "PrimitiveFor", typeof(BuildingType), typeof(Terrain));
            Method(typeof(BuildingDefs), "ImprovedFor", typeof(BuildingType), typeof(Terrain));

            var tu = new EconomyTuning();
            Fields(typeof(EconomyTuning), ("RollIntervalTicks", I), ("FirstRollTick", I), ("BatchSize", I), ("HexCap", I), ("StartMature", I),
                ("GrowTicks", IA), ("CarrierTicksPerStep", I), ("ShipmentSize", I), ("InputBuffer", I), ("CollectorStore", I),
                ("MealTicks", I), ("MealRetryTicks", I), ("WarriorsPerLodge", I), ("StockTarget", IA), ("StartStock", IA),
                ("RoadCost", IA), ("SettlementCost", IA), ("CityCost", IA), ("WarriorCost", IA), ("RoadBuildTicks", I),
                ("SettlementBuildTicks", I), ("CityBuildTicks", I), ("StartCarriers", I), ("NewSettlementCarriers", I),
                ("CarriersPerBuilding", I), ("CarriersPerCity", I));
            Assert.AreEqual(100, tu.RollIntervalTicks);
            Assert.AreEqual(100, tu.FirstRollTick);
            Assert.AreEqual(4, tu.BatchSize);
            Assert.AreEqual(12, tu.HexCap);
            Assert.AreEqual(4, tu.StartMature);
            Assert.IsTrue(tu.GrowTicks.All(g => g == 750));
            Assert.AreEqual(15, tu.CarrierTicksPerStep);
            Assert.AreEqual(4, tu.ShipmentSize);
            Assert.AreEqual(2, tu.InputBuffer);
            Assert.AreEqual(4, tu.CollectorStore);
            Assert.AreEqual(900, tu.MealTicks);
            Assert.AreEqual(100, tu.MealRetryTicks);
            Assert.AreEqual(3, tu.WarriorsPerLodge);
            Assert.AreEqual(100, tu.RoadBuildTicks);
            Assert.AreEqual(300, tu.SettlementBuildTicks);
            Assert.AreEqual(400, tu.CityBuildTicks);
            Assert.AreEqual(3, tu.StartCarriers);
            Assert.AreEqual(2, tu.NewSettlementCarriers);
        }

        [Test]
        public void A1_FactionAndDemonTypes()
        {
            Enum<ProposalKind>(("Building", 0), ("Road", 1), ("Settlement", 2), ("City", 3), ("Banish", 4));
            Enum<GameOutcome>(("None", 0), ("Victory", 1), ("Defeat", 2), ("Draw", 3));
            Const(typeof(OutcomeReasons), "Overrun", "overrun");
            Const(typeof(OutcomeReasons), "Time", "time");
            Const(typeof(Speakers), "Prince", "prince");
            Const(typeof(Speakers), "Sawyer", "sawyer");
            Const(typeof(Speakers), "Merchant", "merchant");
            Fields(typeof(FactionTuning), ("StrengthPerSettlement", I), ("StrengthPerCity", I), ("StrengthPerFedWarrior", I),
                ("OverrunMargin", I), ("WarningTicks", I), ("TimeLimitTicks", I), ("PlanIntervalTicks", I), ("QueueLength", I),
                ("DeclineCooldownTicks", I), ("MaxOpenSites", I), ("RivalCastDelayTicks", I), ("ExpansionReachEdges", I), ("CastRangeMetres", D));
            var ft = new FactionTuning();
            Assert.AreEqual(new[] { 1, 2, 1, 3, 900, 12000, 20, 3, 600, 3, 450, 3 }, new[] { ft.StrengthPerSettlement, ft.StrengthPerCity,
                ft.StrengthPerFedWarrior, ft.OverrunMargin, ft.WarningTicks, ft.TimeLimitTicks, ft.PlanIntervalTicks, ft.QueueLength,
                ft.DeclineCooldownTicks, ft.MaxOpenSites, ft.RivalCastDelayTicks, ft.ExpansionReachEdges });
            Assert.AreEqual(15.0, ft.CastRangeMetres);

            Enum<DemonSiteKind>(("Corner", 0), ("HexEdge", 1));
            Fields(typeof(DemonTuning), ("GraceTicks", I), ("Cap", I), ("PerSeven", I));
            var dt = new DemonTuning();
            Assert.AreEqual(new[] { 1800, 3, 1 }, new[] { dt.GraceTicks, dt.Cap, dt.PerSeven });
        }

        // ---- 4.3 state shapes ----

        [Test]
        public void StateShapes()
        {
            Fields(typeof(WorldState), ("Seed", typeof(ulong)), ("Tick", L), ("Rng", typeof(Rng)), ("Board", typeof(Board)),
                ("Players", typeof(List<PlayerState>)), ("Economy", typeof(EconomyState)), ("Factions", typeof(FactionsState)),
                ("Demons", typeof(DemonsState)));
            Fields(typeof(PlayerState), ("Id", I), ("Name", S), ("Coin", I));

            Fields(typeof(EconomyState), ("Tuning", typeof(EconomyTuning)), ("Dice", typeof(DiceState)), ("Hexes", typeof(HexStock[])),
                ("Settlements", typeof(List<Settlement>)), ("Buildings", typeof(List<Building>)), ("Roads", typeof(List<Road>)),
                ("RoadOnEdge", IA), ("Jobs", typeof(List<Job>)), ("NextJobId", I), ("Carriers", typeof(List<Carrier>)),
                ("Warriors", typeof(List<Warrior>)), ("Ledger", typeof(WareLedger)));
            Fields(typeof(DiceState), ("Deck", IA), ("Rng", typeof(Rng)), ("NextRollTick", L), ("RollCount", I), ("LastD1", I),
                ("LastD2", I), ("LastRollTick", L));
            Fields(typeof(HexStock), ("Hex", I), ("Mature", I), ("Young", typeof(List<StockCohort>)));
            Fields(typeof(StockCohort), ("Ware", typeof(Ware)), ("Count", I), ("AddedTick", L), ("MatureTick", L));
            Fields(typeof(Settlement), ("Id", I), ("Faction", I), ("Corner", I), ("State", typeof(SettlementState)), ("IsCity", B),
                ("Upgrade", typeof(UpgradeState)), ("SourceSettlement", I), ("BuildProgress", I), ("UpgradeProgress", I),
                ("Stock", IA), ("Reserved", IA), ("Incoming", IA), ("Delivered", IA), ("UpgradeDelivered", IA),
                ("FoundedTick", L), ("Reason", S), ("ReasonWare", I));
            Fields(typeof(Building), ("Id", I), ("Type", typeof(BuildingType)), ("Faction", I), ("Settlement", I), ("Plot", I), ("Hex", I),
                ("State", typeof(BuildingState)), ("Stock", IA), ("Reserved", IA), ("Incoming", IA), ("Delivered", IA),
                ("BuildProgress", I), ("WorkProgress", I), ("WorkHex", I), ("Reason", S), ("ReasonWare", I));
            Fields(typeof(Road), ("Id", I), ("Faction", I), ("HexEdge", I), ("State", typeof(RoadState)), ("SourceSettlement", I),
                ("Delivered", IA), ("Incoming", IA), ("BuildProgress", I), ("Reason", S), ("ReasonWare", I));
            Fields(typeof(Job), ("Id", I), ("Faction", I), ("Settlement", I), ("Ware", typeof(Ware)), ("Count", I),
                ("FromKind", typeof(PlaceKind)), ("FromId", I), ("ToKind", typeof(PlaceKind)), ("ToId", I), ("IsShipment", B),
                ("State", typeof(JobState)), ("CarrierId", I), ("Reason", S), ("CreatedTick", L));
            Fields(typeof(Carrier), ("Id", I), ("Faction", I), ("Home", I), ("State", typeof(CarrierState)), ("JobId", I), ("HasLoad", B),
                ("Load", typeof(Ware)), ("LoadCount", I), ("LegFromKind", typeof(PlaceKind)), ("LegFromId", I),
                ("LegToKind", typeof(PlaceKind)), ("LegToId", I), ("LegCorners", IA), ("LegTicks", I), ("Progress", I));
            Fields(typeof(Warrior), ("Id", I), ("Faction", I), ("Lodge", I), ("Settlement", I), ("Fed", B), ("NextMealTick", L));
            Fields(typeof(WareLedger), ("Starting", IA), ("Collected", IA), ("Produced", IA), ("Consumed", IA),
                ("RecipeIn", typeof(int[][])), ("RecipeOut", typeof(int[][])), ("Construction", IA), ("Training", IA), ("Meals", IA),
                ("StockAdded", IA), ("StockLost", IA), ("StockTaken", IA));

            Fields(typeof(FactionsState), ("Tuning", typeof(FactionTuning)), ("Factions", typeof(List<Faction>)),
                ("Proposals", typeof(List<Proposal>)), ("NextProposalId", I), ("Declined", typeof(List<DeclinedProposal>)),
                ("Warning", typeof(OverrunWarning)), ("GameOver", B), ("Outcome", typeof(GameOutcome)), ("OutcomeReason", S),
                ("EndedTick", L), ("Casts", IA), ("OpeningDone", typeof(bool[])));
            Fields(typeof(Faction), ("Id", I), ("Name", S), ("Auto", B), ("Strength", I), ("Settlements", I), ("Cities", I),
                ("FedWarriors", I), ("HungryWarriors", I), ("NextPlanTick", L));
            Fields(typeof(Proposal), ("Id", I), ("Faction", I), ("Kind", typeof(ProposalKind)), ("BuildingType", I), ("Settlement", I),
                ("Plot", I), ("HexEdge", I), ("Corner", I), ("DemonSiteKind", typeof(DemonSiteKind)), ("DemonSiteId", I),
                ("Reason", S), ("Speaker", S), ("Score", I), ("Rank", I), ("CreatedTick", L), ("TopSinceTick", L));
            Assert.AreEqual(S, typeof(Proposal).GetProperty("Key").PropertyType);
            Fields(typeof(DeclinedProposal), ("Faction", I), ("Key", S), ("UntilTick", L));
            Fields(typeof(OverrunWarning), ("Active", B), ("Attacker", I), ("Defender", I), ("StartTick", L), ("EndTick", L));

            Fields(typeof(DemonsState), ("Tuning", typeof(DemonTuning)), ("Corner", IA), ("Edge", IA), ("Rng", typeof(Rng)),
                ("Placed", I), ("Banished", I), ("Outbreaks", I));
        }

        // ---- A.2, 5.2 ----

        [Test]
        public void A2_CommandsAndRefusals()
        {
            Command<CastSpell>(("ProposalId", I));
            Command<DeclineProposal>(("ProposalId", I));
            Command<PlaceBuilding>(("Type", typeof(BuildingType)), ("Plot", I));
            Command<PlaceRoad>(("HexEdge", I));
            Command<FoundSettlement>(("Corner", I));
            Command<UpgradeCity>(("Corner", I));
            Command<CancelSite>(("Kind", typeof(SiteKind)), ("Id", I));
            Command<Banish>(("SiteKind", typeof(DemonSiteKind)), ("SiteId", I));
            Fields(typeof(Command), ("PlayerId", I));

            var economy = new Dictionary<string, string>
            {
                { "GameOver", "game_over" }, { "NotOwner", "not_owner" }, { "UnknownType", "unknown_type" }, { "NotPlaceable", "not_placeable" },
                { "UnknownPlot", "unknown_plot" }, { "NoSettlement", "no_settlement" }, { "PlotTaken", "plot_taken" },
                { "PlotLocked", "plot_locked" }, { "PlotReserved", "plot_reserved" }, { "WrongTerrain", "wrong_terrain" },
                { "AlreadyBuilt", "already_built" }, { "BadCorner", "bad_corner" }, { "Occupied", "occupied" }, { "TooClose", "too_close" },
                { "NotConnected", "not_connected" }, { "RoadExists", "road_exists" }, { "BlockedByRival", "blocked_by_rival" },
                { "NotASettlement", "not_a_settlement" }, { "AlreadyCity", "already_city" }, { "UpgradeInProgress", "upgrade_in_progress" },
                { "NotASite", "not_a_site" },
            };
            foreach (var kv in economy) Const(typeof(EconomyRefusals), kv.Key, kv.Value);
            Const(typeof(FactionRefusals), "GameOver", "game_over");
            Const(typeof(FactionRefusals), "UnknownProposal", "unknown_proposal");
            Const(typeof(FactionRefusals), "NoLongerLegal", "no_longer_legal");
            Const(typeof(DemonRefusals), "GameOver", "game_over");
            Const(typeof(DemonRefusals), "NotOwner", "not_owner");
            Const(typeof(DemonRefusals), "NoDemons", "no_demons");
        }

        [Test]
        public void A2_EveryCommandHasAHandler()
        {
            var sim = WorldSimFactory.CreateNew(1);
            foreach (var t in new[] { typeof(CastSpell), typeof(DeclineProposal), typeof(PlaceBuilding), typeof(PlaceRoad),
                         typeof(FoundSettlement), typeof(UpgradeCity), typeof(CancelSite), typeof(Banish) })
                Assert.IsTrue(sim.HasHandler(t), t.Name);
            var loaded = SaveCodec.Load(SaveCodec.ToJson(sim.State), out _);
            Assert.IsTrue(loaded.HasHandler(typeof(CastSpell)) && loaded.HasHandler(typeof(Banish)) && loaded.HasHandler(typeof(PlaceBuilding)));
        }

        // ---- 5.3 events ----

        [Test]
        public void Events()
        {
            Type W = typeof(Ware), PK = typeof(PlaceKind), SK = typeof(SiteKind), DK = typeof(DemonSiteKind);
            Event<DiceRolled>(("D1", I), ("D2", I), ("Sum", I), ("RollIndex", I));
            Event<StockAdded>(("Hex", I), ("Ware", W), ("Count", I), ("MatureTick", L));
            Event<StockLost>(("Hex", I), ("Ware", W), ("Count", I));
            Event<StockMatured>(("Hex", I), ("Ware", W), ("Count", I));
            Event<CollectStarted>(("BuildingId", I), ("Hex", I));
            Event<Collected>(("BuildingId", I), ("Hex", I), ("Ware", W), ("Amount", I));
            Event<ProductionStarted>(("BuildingId", I));
            Event<ProductionCycle>(("BuildingId", I), ("Ware", W), ("Amount", I));
            Event<SitePlaced>(("Kind", SK), ("Id", I), ("Faction", I));
            Event<SiteCompleted>(("Kind", SK), ("Id", I), ("Faction", I));
            Event<SiteCancelled>(("Kind", SK), ("Id", I), ("Faction", I));
            Event<BuildingStateChanged>(("BuildingId", I), ("State", typeof(BuildingState)), ("Reason", S), ("ReasonWare", I));
            Event<BuildingRetired>(("BuildingId", I));
            Event<SettlementFounded>(("SettlementId", I), ("Corner", I), ("Faction", I));
            Event<CityUpgraded>(("SettlementId", I), ("Corner", I), ("Faction", I));
            Event<RoadBuilt>(("RoadId", I), ("HexEdge", I), ("Faction", I));
            Event<JobCreated>(("JobId", I), ("Ware", W), ("Count", I), ("FromKind", PK), ("FromId", I), ("ToKind", PK), ("ToId", I), ("IsShipment", B));
            Event<CarrierLegStarted>(("CarrierId", I), ("FromKind", PK), ("FromId", I), ("ToKind", PK), ("ToId", I), ("LegTicks", I));
            Event<WarePickedUp>(("JobId", I), ("CarrierId", I), ("Ware", W), ("Count", I));
            Event<WareDelivered>(("JobId", I), ("CarrierId", I), ("Ware", W), ("Count", I), ("ToKind", PK), ("ToId", I));
            Event<JobStalled>(("JobId", I), ("Reason", S));
            Event<WarriorTrained>(("WarriorId", I), ("Lodge", I), ("Faction", I));
            Event<WarriorFed>(("WarriorId", I), ("Ware", W));
            Event<WarriorHungry>(("WarriorId", I));

            Event<ProposalAdded>(("ProposalId", I), ("Faction", I), ("Rank", I));
            Event<ProposalsReordered>(("Faction", I), ("TopProposalId", I));
            Event<ProposalRemoved>(("ProposalId", I), ("Faction", I), ("Why", S));
            Event<SpellCast>(("ProposalId", I), ("Faction", I), ("Kind", typeof(ProposalKind)), ("BuildingType", I), ("Plot", I),
                ("HexEdge", I), ("Corner", I), ("DemonSiteKind", DK), ("DemonSiteId", I));
            Event<StrengthChanged>(("Faction", I), ("Strength", I));
            Event<OverrunWarningStarted>(("Attacker", I), ("Defender", I), ("EndTick", L));
            Event<OverrunWarningCancelled>(("Attacker", I), ("Defender", I));
            Event<FactionOverrun>(("Attacker", I), ("Defender", I));
            Event<GameEnded>(("Outcome", typeof(GameOutcome)), ("Reason", S), ("PlayerStrength", I), ("RivalStrength", I));

            Event<DemonPlaced>(("SiteKind", DK), ("SiteId", I), ("Count", I));
            Event<DemonOutbreak>(("SiteKind", DK), ("SiteId", I), ("SpreadKinds", typeof(DemonSiteKind[])), ("SpreadIds", IA));
            Event<DemonsBanished>(("SiteKind", DK), ("SiteId", I), ("Count", I), ("Faction", I));
        }

        // ---- A.3 read helpers ----

        [Test]
        public void A3_EconomyReadHelpers()
        {
            Fields(typeof(DiceView), ("D1", I), ("D2", I), ("Sum", I), ("RollIndex", I), ("Tick", L));
            Fields(typeof(HexStockView), ("Hex", I), ("Terrain", typeof(Terrain)), ("Ware", typeof(Ware?)), ("Number", I), ("Pips", I),
                ("Mature", I), ("Young", I), ("Stage0", I), ("Stage1", I), ("Stage2", I), ("Cap", I), ("NextMatureTick", L),
                ("YoungVisible", B), ("Stopped", B));
            Fields(typeof(PlotView), ("Plot", I), ("Corner", I), ("Sector", I), ("Slot", I), ("Hex", I), ("Terrain", typeof(Terrain)),
                ("X", D), ("Z", D), ("Yaw", D), ("SettlementId", I), ("Use", typeof(PlotUse)), ("BuildingId", I));
            Enum<PlotUse>(("NoSettlement", 0), ("Locked", 1), ("Reserved", 2), ("Free", 3), ("Site", 4), ("Building", 5));
            Fields(typeof(BuildingInfo), ("Title", S), ("Status", S), ("RecipeText", S), ("CostText", S), ("StockLines", typeof(string[])));

            var t = typeof(EconomyApi);
            Method(t, "LastRoll", typeof(DiceView), WS);
            Method(t, "TicksToNextRoll", L, WS);
            Method(t, "Pips", I, I);
            Method(t, "HexStock", typeof(HexStockView), WS, I);
            Method(t, "Settlements", typeof(IReadOnlyList<Settlement>), WS);
            Method(t, "SettlementAt", typeof(Settlement), WS, I);
            Method(t, "SettlementName", S, WS, I);
            Method(t, "StockOf", I, WS, I, typeof(Ware));
            Method(t, "Plot", typeof(PlotView), WS, I);
            Method(t, "PlotsOf", IA, WS, I);
            Method(t, "Def", typeof(BuildingDef), typeof(BuildingType));
            Method(t, "DescribeBuilding", typeof(BuildingInfo), WS, I);
            Method(t, "WaitingSentence", S, WS, typeof(SiteKind), I);
            Method(t, "RoadOn", typeof(Road), WS, I);
            Method(t, "CarrierPosition", B, WS, I, D, D.MakeByRefType(), D.MakeByRefType(), D.MakeByRefType());
            Method(t, "DescribeCarrier", S, WS, I);
            Method(t, "DescribeJob", S, WS, I);
            Method(t, "CheckBuilding", CR, WS, I, typeof(BuildingType), I);
            Method(t, "CheckRoad", CR, WS, I, I);
            Method(t, "CheckSettlement", CR, WS, I, I);
            Method(t, "CheckCity", CR, WS, I, I);
            Method(t, "WareName", S, typeof(Ware));
            Method(t, "WareTitle", S, typeof(Ware));
            Method(t, "WareColour", S, typeof(Ware));
            Method(t, "CostText", S, IA);
        }

        [Test]
        public void A3_FactionAndDemonReadHelpers()
        {
            Fields(typeof(StrengthView), ("Faction", I), ("Settlements", I), ("Cities", I), ("FedWarriors", I), ("HungryWarriors", I), ("Total", I));
            Fields(typeof(WarningView), ("Active", B), ("Attacker", I), ("Defender", I), ("TicksLeft", L), ("Sentence", S));
            Fields(typeof(OutcomeView), ("GameOver", B), ("Outcome", typeof(GameOutcome)), ("Reason", S), ("EndedTick", L), ("Sentence", S));
            var t = typeof(FactionApi);
            Method(t, "Proposals", typeof(IReadOnlyList<Proposal>), WS, I);
            Method(t, "Top", typeof(Proposal), WS, I);
            Method(t, "GetProposal", typeof(Proposal), WS, I);
            Method(t, "ProposalTitle", S, WS, I);
            Method(t, "ProposalTarget", B, WS, I, D.MakeByRefType(), D.MakeByRefType(), D.MakeByRefType());
            Method(t, "ProposalAt", I, WS, I, D, D);
            Method(t, "CastRangeMetres", D, WS);
            Method(t, "Strength", typeof(StrengthView), WS, I);
            Method(t, "Warning", typeof(WarningView), WS);
            Method(t, "Outcome", typeof(OutcomeView), WS);
            Method(t, "Headline", S, WS);

            Fields(typeof(DemonSiteView), ("Kind", typeof(DemonSiteKind)), ("Id", I), ("Count", I), ("Faction", I), ("X", D), ("Z", D), ("Effect", S));
            Method(typeof(DemonApi), "DemonsAt", I, WS, typeof(DemonSiteKind), I);
            Method(typeof(DemonApi), "DemonSites", typeof(IReadOnlyList<DemonSiteView>), WS);
            Method(typeof(DemonApi), "SiteName", S, WS, typeof(DemonSiteKind), I);
        }

        // ---- A.4 modules, 5 system order, 7 save ----

        [Test]
        public void A4_ModulesHooksAndSystemOrder()
        {
            foreach (var m in new[] { typeof(EconomyModule), typeof(FactionsModule), typeof(DemonsModule) })
            {
                Method(m, "InitState", typeof(void), WS);
                Method(m, "Register", typeof(void), typeof(WorldSim));
            }
            foreach (var h in new[] { "IsCornerBlocked", "IsEdgeBlocked", "IsSettlementHalted", "OnRolled", "OnSiteCompleted" })
                Assert.IsNotNull(typeof(EconomyHooks).GetField(h), "EconomyHooks." + h);
            Method(typeof(EconomyApi), "Hooks", typeof(EconomyHooks), typeof(WorldSim));
            Method(typeof(WorldSimFactory), "CreateNew", typeof(WorldSim), typeof(ulong));
            Method(typeof(SaveCodec), "CreateSim", typeof(WorldSim), WS);
            Const(typeof(SaveCodec), "CurrentVersion", 4);
            Const(typeof(SaveCodec), "OldestVersion", 4);
            Const(typeof(SystemOrder), "Dice", 100);
            Const(typeof(SystemOrder), "Growth", 150);
            Const(typeof(SystemOrder), "Collect", 200);
            Const(typeof(SystemOrder), "Production", 300);
            Const(typeof(SystemOrder), "Construction", 400);
            Const(typeof(SystemOrder), "Jobs", 500);
            Const(typeof(SystemOrder), "Carriers", 600);
            Const(typeof(SystemOrder), "Demons", 1100);
            Const(typeof(SystemOrder), "Brain", 1150);
            Const(typeof(SystemOrder), "Strength", 1200);
            Const(typeof(SystemOrder), "Diagnostics", 1300);
        }

        [Test]
        public void ArchivedNamespaces_AreGone()
        {
            var asm = typeof(WorldState).Assembly;
            Assert.IsFalse(asm.GetTypes().Any(x => x.Namespace == "World.Sim.Strategy" || x.Namespace == "World.Sim.Military"),
                "Strategy and Military live in Archive/");
        }
    }
}
