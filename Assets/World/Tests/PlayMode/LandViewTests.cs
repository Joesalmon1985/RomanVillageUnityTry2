using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game.Tests
{
    /// <summary>
    /// Package V (REFACTOR_SPEC 8.4): the land views follow the state. Each test loads the World scene, pauses the clock
    /// and steps it by hand (<see cref="GameClock.StepOnce"/>, so every tick goes through the same reconcile as in
    /// play). Growth beyond what the first-version brain proposes uses the direct build commands of 5.1
    /// (<see cref="LandLateGame"/>). Carriers and shipments are checked with a fake feed until the real logistics land.
    /// </summary>
    public class LandViewTests
    {
        private const string TestPause = "LandTest";
        private WorldBootstrap _boot;
        private LandViews _views;
        private WorldState S => _boot.Sim.State;

        private IEnumerator Load()
        {
            LandViews.CarrierFeed = SimCarrierFeed.Instance;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot);
            _boot.Clock.Pause(TestPause);
            _views = LandViews.Instance;
            Assert.IsNotNull(_views, "LandViews installed");
        }

        [TearDown]
        public void TearDown() { LandViews.CarrierFeed = SimCarrierFeed.Instance; }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private void Grow(int ticks, int mine, int theirs, bool topUp = true) =>
            LandLateGame.Grow(_boot.Sim, ticks, new[] { mine, theirs }, topUp, () => _boot.Clock.StepOnce());

        // ---------------- views per state item ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator Views_ExistPerStateItem_ForBothPeoples()
        {
            yield return Load();
            Grow(2500, 4, 4);
            yield return null;
            var e = S.Economy;
            int[] camps = new int[2];
            foreach (var st in e.Settlements)
            {
                if (st.State == SettlementState.Cancelled) { Assert.IsNull(_views.Settlement(st.Id)); continue; }
                var v = _views.Settlement(st.Id);
                Assert.IsNotNull(v, "camp view " + st.Id);
                Assert.AreEqual(st.State, v.ShownState, "camp state " + st.Id);
                Assert.AreEqual(st.IsCity, v.ShownCity, "city look " + st.Id);
                Assert.Less(BoardSpace.Flat(v.transform.position - BoardSpace.Vertex(S.Board.Topology, st.Corner)), 0.01f);
                if (st.State == SettlementState.Active)
                {
                    camps[st.Faction]++;
                    int free = 0;
                    foreach (int p in EconomyApi.PlotsOf(S, st.Id)) if (EconomyApi.Plot(S, p).Use == PlotUse.Free) free++;
                    Assert.AreEqual(free, v.FreePlotRings, "empty plot rings of camp " + st.Id);
                }
            }
            Assert.Greater(camps[0], 1, "your people grew"); Assert.Greater(camps[1], 1, "the Ash Clan grew");
            foreach (var b in e.Buildings)
            {
                bool gone = b.State == BuildingState.Retired || b.State == BuildingState.Cancelled;
                var v = _views.Building(b.Id);
                if (gone) { Assert.IsNull(v, "no view for retired building " + b.Id); continue; }
                Assert.IsNotNull(v, "building view " + b.Id);
                Assert.AreEqual(b.State, v.ShownState, "building state " + b.Id);
                Assert.Less(BoardSpace.Flat(v.transform.position - BoardSpace.Plot(S.Board.Topology, b.Plot)), 0.01f, "on its plot");
            }
            foreach (var r in e.Roads)
            {
                var v = _views.Road(r.Id);
                if (r.State == RoadState.Cancelled) { Assert.IsNull(v); continue; }
                Assert.IsNotNull(v, "road view " + r.Id);
                Assert.AreEqual(r.State, v.ShownState, "road state " + r.Id);
            }
            int carriers = 0;
            foreach (var _ in _views.Carriers) carriers++;
            Assert.AreEqual(e.Carriers.Count, carriers, "one view per carrier");
            foreach (var w in e.Warriors) Assert.IsNotNull(_views.Building(w.Lodge), "lodge of warrior " + w.Id);
            var props = FactionApi.Proposals(S, PlayerIds.Human);
            int markers = 0;
            foreach (var _ in _views.Markers) markers++;
            Assert.AreEqual(props.Count, markers, "one marker per open proposal of your people");
            for (int i = 0; i < props.Count; i++)
            {
                var m = _views.Marker(props[i].Id);
                Assert.IsNotNull(m);
                Assert.AreEqual(i == 0, m.IsTop, "top marker");
                Assert.IsTrue(FactionApi.ProposalTarget(S, props[i].Id, out double x, out double z, out _));
                Assert.Less(BoardSpace.Flat(m.Target - LandLook.Ground(x, z)), 0.01f, "marker on the proposal target");
                Assert.AreSame(m.transform, _views.ProposalMarker(props[i].Id));
            }
            foreach (var b in e.Buildings)
                if (b.State == BuildingState.Active) { Assert.AreSame(_views.Building(b.Id).transform, _views.BuildingView(b.Id), "appendix B lookup"); break; }
            Assert.IsNull(_views.BuildingView(99999));
        }

        // ---------------- stock ----------------

        private void AssertHexesMatch(string when)
        {
            for (int h = 0; h < S.Board.Topology.HexCount; h++)
            {
                var v = EconomyApi.HexStock(S, h);
                var hv = _views.Hex(h);
                if (!v.Ware.HasValue) continue;
                int m = Mathf.Min(v.Mature, v.Cap);
                Assert.AreEqual(m, hv.ShownMature, when + ": mature on hex " + h);
                if (v.YoungVisible)
                {
                    Assert.AreEqual(v.Stage2, hv.ShownStage2, when + ": stage 2 on hex " + h);
                    Assert.AreEqual(v.Stage1, hv.ShownStage1, when + ": stage 1 on hex " + h);
                    Assert.AreEqual(v.Stage0, hv.ShownStage0, when + ": stage 0 on hex " + h);
                }
                else
                {
                    Assert.AreEqual(0, hv.ShownStage0 + hv.ShownStage1 + hv.ShownStage2, when + ": nothing young drawn on mined hex " + h);
                    Assert.AreEqual(v.Mature > 0, hv.ReadyMarkShown, when + ": ready mark on mined hex " + h);
                }
                Assert.AreEqual(v.Stopped, hv.ShownStopped, when + ": stopped mark on hex " + h);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator StockStages_MatchHexState_AfterRolls()
        {
            yield return Load();
            AssertHexesMatch("start");
            int stagesSeen = 0;
            var seen = new HashSet<int>();
            for (int i = 0; i < 24; i++)
            {
                Step(50);
                AssertHexesMatch("tick " + S.Tick);
                for (int h = 0; h < S.Board.Topology.HexCount; h++)
                {
                    var hv = _views.Hex(h);
                    if (hv.ShownStage0 > 0) seen.Add(0);
                    if (hv.ShownStage1 > 0) seen.Add(1);
                    if (hv.ShownStage2 > 0) seen.Add(2);
                }
            }
            stagesSeen = seen.Count;
            Assert.AreEqual(3, stagesSeen, "all three young stages were drawn somewhere");
            Assert.GreaterOrEqual(EconomyApi.LastRoll(S).RollIndex, 10, "rolls happened");
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CollectorHarvest_ReducesVisibleMatureStock()
        {
            yield return Load();
            var before = new int[S.Board.Topology.HexCount];
            int checkedHarvests = 0;
            IReadOnlyList<SimEvent> last = null;
            _views.Synced += ev => last = ev;
            for (int i = 0; i < 3000 && checkedHarvests < 3; i++)
            {
                for (int h = 0; h < before.Length; h++) before[h] = _views.Hex(h).ShownMature;
                Step();
                bool other = false; var hexes = new List<int>();
                foreach (var ev in last)
                {
                    if (ev is CollectStarted c) hexes.Add(c.Hex);
                    if (ev is StockMatured || ev is StockAdded) other = true;
                }
                if (hexes.Count == 0 || other) continue;
                foreach (int h in hexes)
                {
                    Assert.Less(_views.Hex(h).ShownMature, before[h], "a harvest takes one visible unit on hex " + h + " at tick " + S.Tick);
                    Assert.AreEqual(EconomyApi.HexStock(S, h).Mature, _views.Hex(h).ShownMature);
                }
                checkedHarvests++;
            }
            Assert.GreaterOrEqual(checkedHarvests, 1, "saw a harvest");
            // The gatherer of a working collector heads for the stock patch.
            BuildingView working = null;
            foreach (var b in _views.Buildings) if (b.IsCollector && b.Working) { working = b; break; }
            if (working == null) for (int i = 0; i < 200 && working == null; i++) { Step(); foreach (var b in _views.Buildings) if (b.IsCollector && b.Working) { working = b; break; } }
            Assert.IsNotNull(working, "a collector is working");
            yield return null; yield return null;
            Assert.IsTrue(working.GathererOut, "its gatherer walks to the patch while it harvests");
        }

        // ---------------- sites ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator BuildingSite_BecomesFinishedBuilding()
        {
            yield return Load();
            var top = FactionApi.Top(S, PlayerIds.Human);
            Assert.IsNotNull(top, "an opening proposal");
            Assert.IsNotNull(_views.ProposalMarker(top.Id), "its cast-here mark");
            Assert.IsTrue(_boot.Sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = top.Id }).Ok);
            Step();
            Building site = null;
            foreach (var b in S.Economy.Buildings) if (b.Plot == top.Plot) site = b;
            Assert.IsNotNull(site);
            var v = _views.Building(site.Id);
            Assert.IsNotNull(v, "site view");
            Assert.IsTrue(v.ShownState == BuildingState.Site || v.ShownState == BuildingState.Constructing);
            Assert.Greater(v.Site.DeliveredShown + v.Site.MissingShown, 0, "goods shown at the site");
            Assert.IsNull(_views.ProposalMarker(top.Id), "the mark goes when the spell is cast");
            for (int i = 0; i < 400 && site.State != BuildingState.Active; i++)
            {
                Step();
                if (site.State == BuildingState.Constructing) Assert.IsNotNull(v.Site.Builder, "a builder works on the site");
            }
            Assert.AreEqual(BuildingState.Active, site.State);
            Assert.AreEqual(BuildingState.Active, v.ShownState);
            Assert.AreEqual(1f, v.ShownBuildFraction, 1e-4f);
            Assert.AreEqual(0, v.Site.DeliveredShown + v.Site.MissingShown, "site goods cleared");
            yield return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RoadSite_BecomesBuiltRoad()
        {
            yield return Load();
            var t = S.Board.Topology;
            int edge = -1;
            foreach (int he in t.CornerHexEdges(S.Board.StartCorners[0]))
                if (EconomyApi.CheckRoad(S, PlayerIds.Human, he).Ok) { edge = he; break; }
            Assert.GreaterOrEqual(edge, 0);
            Assert.IsTrue(_boot.Sim.Submit(new PlaceRoad { PlayerId = PlayerIds.Human, HexEdge = edge }).Ok);
            Step();
            var road = EconomyApi.RoadOn(S, edge);
            var v = _views.Road(road.Id);
            Assert.IsNotNull(v);
            Assert.AreNotEqual(RoadState.Built, v.ShownState, "a road site first");
            BoardSpace.HexEdgeEnds(t, edge, out var a, out var b);
            Assert.Less(BoardSpace.Flat(v.transform.position - (a + b) * 0.5f), 0.01f, "along the whole hex edge");
            for (int i = 0; i < 400 && road.State != RoadState.Built; i++) Step();
            Assert.AreEqual(RoadState.Built, road.State);
            Assert.AreEqual(RoadState.Built, v.ShownState);
            Assert.AreEqual(1f, v.ShownLaid, 1e-4f);
            yield return null;
        }

        // ---------------- demons ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator Demon_Appears_AndHeldCampShowsStopped()
        {
            yield return Load();
            int corner = -1;
            for (int i = 0; i < 12000 && corner < 0; i++)
            {
                Step();
                foreach (var d in DemonApi.DemonSites(S)) if (d.Kind == DemonSiteKind.Corner) { corner = d.Id; break; }
            }
            Assert.GreaterOrEqual(corner, 0, "a demon landed on a camp");
            var dv = _views.Demons(DemonSiteKind.Corner, corner);
            Assert.IsNotNull(dv, "demon view");
            Assert.AreEqual(DemonApi.DemonsAt(S, DemonSiteKind.Corner, corner), dv.CountShown);
            var st = EconomyApi.SettlementAt(S, corner);
            var sv = _views.Settlement(st.Id);
            Assert.IsTrue(sv.ShownHeld, "the held camp looks stopped");
            // While held, none of its collectors starts a cycle.
            int started = 0;
            System.Action<IReadOnlyList<SimEvent>> watch = evs =>
            {
                foreach (var ev in evs)
                    if (ev is CollectStarted c && S.Economy.Buildings[c.BuildingId].Settlement == st.Id) started++;
            };
            _views.Synced += watch;
            for (int i = 0; i < 60 && DemonApi.DemonsAt(S, DemonSiteKind.Corner, corner) > 0; i++) Step();
            _views.Synced -= watch;
            Assert.AreEqual(0, started, "a demon-held camp's collectors stop");
            Assert.IsTrue(sv.ShownHeld);
            AssertHexesMatch("held");
            // Banish: the demons and the stopped look go.
            var r = _boot.Sim.Submit(new Banish { PlayerId = st.Faction, SiteKind = DemonSiteKind.Corner, SiteId = corner });
            Assert.IsTrue(r.Ok, r.ToString());
            Step();
            Assert.IsNull(_views.Demons(DemonSiteKind.Corner, corner), "demons gone");
            Assert.IsFalse(sv.ShownHeld, "the camp works again");
            yield return null;
        }

        // ---------------- warning and overrun ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator Warning_ShowsWarCamp_AndOverrunBurnsTheDefender()
        {
            yield return Load();
            bool warcamp = false;
            for (int i = 0; i < 40 && !S.Factions.GameOver; i++)
            {
                Grow(150, 1, 7);
                if (S.Factions.Warning.Active)
                {
                    Assert.IsNotNull(_views.WarCamp, "war camp while the warning runs");
                    Assert.AreEqual(S.Factions.Warning.Attacker, _views.WarCamp.Attacker);
                    warcamp = true;
                }
            }
            Assert.IsTrue(warcamp, "a warning ran");
            Assert.IsTrue(S.Factions.GameOver, "the game ended");
            Assert.AreEqual(OutcomeReasons.Overrun, S.Factions.OutcomeReason);
            Assert.IsNotNull(_views.WarCamp, "the war camp stays after the overrun");
            int loser = S.Factions.Outcome == GameOutcome.Victory ? PlayerIds.Rival : PlayerIds.Human;
            foreach (var sv in _views.Settlements)
                if (sv.Faction == loser && sv.ShownState == SettlementState.Active) Assert.IsTrue(sv.ShownOverrun, "the overrun camp burns");
            yield return null;
        }

        // ---------------- rebuild ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator RebuildFromState_EqualsIncremental()
        {
            yield return Load();
            Grow(1800, 3, 3);
            yield return null;
            var a = _views.Snapshot();
            int rebuilds = _views.RebuildCount;
            _views.RebuildFromState();
            yield return null;
            var b = _views.Snapshot();
            Assert.AreEqual(rebuilds + 1, _views.RebuildCount);
            Assert.Greater(a.Count, 80, "a sizeable scene");
            CollectionAssert.AreEqual(a, b, "rebuilt from state equals the incrementally updated views");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Tick_LooksOnlyAtChangedOrGrowingHexes()
        {
            yield return Load();
            Step(30);
            int before = _views.HexSyncs;
            Step(10); // no roll in these ticks (rolls every 100)
            int per = (_views.HexSyncs - before) / 10;
            Assert.Less(per, S.Board.Topology.HexCount, "not every hex every tick");
        }

        // ---------------- carriers (fake feed until the real logistics land) ----------------

        /// <summary>Fake carriers: one on a local trip along a plot path with a wood bundle, one porter on a main-road edge with 3 clay.</summary>
        private sealed class FakeFeed : ICarrierFeed
        {
            public float U;
            public Vector3 A0, A1, B0, B1;
            public bool PorterMoving = true;
            public int Count(WorldState s) => 3;
            public bool TryGet(WorldState s, int id, double frac, out CarrierPose pose)
            {
                Position(s, id, frac, out pose.X, out pose.Z, out pose.Yaw);
                pose.Faction = id == 1 ? PlayerIds.Rival : PlayerIds.Human; pose.Home = id == 1 ? 1 : 0;
                pose.Moving = id != 2 && (id != 1 || PorterMoving);
                pose.HasLoad = id != 2; pose.Load = id == 0 ? Ware.Wood : Ware.Clay; pose.LoadCount = id == 0 ? 1 : 3;
                pose.Shipment = id == 1;
                return true;
            }
            public bool Position(WorldState s, int id, double frac, out double x, out double z, out double yaw)
            {
                var p = id == 0 ? Vector3.Lerp(A0, A1, U) : Vector3.Lerp(B0, B1, U);
                var d = id == 0 ? A1 - A0 : B1 - B0;
                x = p.x; z = p.z; yaw = BoardSpace.YawOf(d);
                return true;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FakeDriver_CarrierAndPorter_FollowFeed_WithLoads()
        {
            yield return Load();
            var t = S.Board.Topology;
            var path = BoardSpace.PlotPath(t, 630);
            int home = S.Board.StartCorners[0];
            int edge = t.CornerHexEdges(home)[0];
            BoardSpace.HexEdgeEnds(t, edge, out var e0, out var e1);
            var feed = new FakeFeed { A0 = path[0], A1 = path[1], B0 = e0, B1 = e1 };
            LandViews.CarrierFeed = feed;
            _views.RebuildFromState();
            var walker = _views.Carrier(0); var porter = _views.Carrier(1); var idle = _views.Carrier(2);
            Assert.IsNotNull(walker); Assert.IsNotNull(porter); Assert.IsNotNull(idle);
            Assert.IsNull(_views.Carrier(3), "only the feed's carriers");
            Assert.AreSame(walker.transform, _views.CarrierView(0), "appendix B lookup");
            Assert.IsTrue(walker.Figure.HasLoad && !walker.Shipment, "carrier with a bundle");
            Assert.IsTrue(porter.Figure.HasLoad && porter.Shipment, "porter with a basket");
            Assert.IsFalse(idle.Figure.HasLoad, "idle carrier carries nothing");
            for (int k = 0; k <= 10; k++)
            {
                feed.U = k / 10f;
                yield return null;
                feed.Position(S, 0, 0, out double x0, out double z0, out _);
                feed.Position(S, 1, 0, out double x1, out double z1, out _);
                Assert.Less(BoardSpace.Flat(walker.transform.position - LandLook.Ground(x0, z0)), 0.05f, "carrier on the feed position at u " + feed.U);
                Assert.Less(BoardSpace.Flat(porter.transform.position - LandLook.Ground(x1, z1)), 0.05f, "porter on the feed position at u " + feed.U);
            }
            // Idle carriers wait beside their storehouse, outside the 3 m store and clear of nothing else.
            float d = BoardSpace.Flat(idle.transform.position - BoardSpace.Vertex(t, home));
            Assert.That(d, Is.InRange(3.5f, 6f), "idle carrier beside the storehouse");
            // Carrier events go through the same reconcile path without errors.
            _views.Inject(new List<SimEvent>
            {
                new CarrierLegStarted { CarrierId = 0, FromKind = PlaceKind.Building, FromId = 0, ToKind = PlaceKind.Storehouse, ToId = 0, LegTicks = 120 },
                new WarePickedUp { JobId = 1, CarrierId = 0, Ware = Ware.Wood, Count = 1 },
                new WareDelivered { JobId = 1, CarrierId = 0, Ware = Ware.Wood, Count = 1, ToKind = PlaceKind.Storehouse, ToId = 0 },
            });
            // The porter stops: it goes to its idle spot; the feed swap back restores the sim's carriers.
            feed.PorterMoving = false;
            _views.Inject(new List<SimEvent>());
            yield return null;
            Assert.Greater(BoardSpace.Flat(porter.transform.position - LandLook.Ground(e0.x, e0.z)), 0.0f);
            LandViews.CarrierFeed = SimCarrierFeed.Instance;
            _views.RebuildFromState();
            int n = 0; foreach (var _ in _views.Carriers) n++;
            Assert.AreEqual(S.Economy.Carriers.Count, n);
            Assert.IsTrue(_views.Carrier(0) is IFollowable, "carriers are followable");
        }

        // ---------------- inspect sentences ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Inspectables_GivePlainSentences()
        {
            yield return Load();
            Step(150);
            var list = new List<IInspectable> { _views.Hex(22), _views.Settlement(0), _views.Building(0), _views.Carrier(0) };
            var top = FactionApi.Top(S, PlayerIds.Human);
            if (top != null) list.Add(_views.Marker(top.Id));
            foreach (var i in list)
            {
                Assert.IsNotNull(i);
                Assert.IsFalse(string.IsNullOrEmpty(i.Title));
                int lines = 0;
                foreach (var l in i.Lines) { Assert.IsFalse(string.IsNullOrEmpty(l)); Assert.IsTrue(l.EndsWith(".") || l.EndsWith(":"), "sentence: " + l); lines++; }
                Assert.That(lines, Is.InRange(1, 2), i.Title + " gives one or two sentences");
                Assert.IsNotNull((i as Component).GetComponentInChildren<Collider>(), i.Title + " is pickable");
            }
        }
    }

    /// <summary>
    /// Test-only driver that grows both peoples to a late-game size with the direct build commands of 5.1 (the F1
    /// tools' commands): camps on legal corners reached by roads, a building on every free plot, cities, lodges.
    /// Pure sim (no UnityEngine), so the same file also runs in a scratch console. It only submits commands; the sim
    /// does the rest.
    /// </summary>
    public static class LandLateGame
    {
        /// <summary>Steps the sim <paramref name="ticks"/> ticks, issuing build commands for both peoples every 10 ticks.</summary>
        /// <param name="maxSettlements">Most camps (sites included) per people, indexed by people.</param>
        /// <param name="topUp">Stress fixture only: keeps at least 8 of every ware in each finished camp's storehouse
        /// (breaks the ware ledger; never used where conservation matters), so growth is not limited by goods.</param>
        /// <param name="step">How to advance one tick (default: the sim directly; tests pass the GameClock so views follow).</param>
        public static void Grow(WorldSim sim, int ticks, int[] maxSettlements, bool topUp = false, System.Action step = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                if (sim.State.Factions != null && sim.State.Factions.GameOver) return;
                if (topUp && sim.State.Tick % 50 == 0)
                    foreach (var st in sim.State.Economy.Settlements)
                        if (st.State == SettlementState.Active)
                            for (int w = 0; w < Wares.Count; w++) if (st.Stock[w] < 8) st.Stock[w] = 8;
                if (sim.State.Tick % 10 == 0)
                    for (int f = 0; f < PlayerIds.Count; f++) Issue(sim, f, maxSettlements[f]);
                if (step != null) step();
                else { sim.Step(); sim.DrainEvents(); }
            }
        }

        public static void Grow(WorldSim sim, int ticks, int maxSettlementsPerFaction = 6, bool topUp = false) =>
            Grow(sim, ticks, new[] { maxSettlementsPerFaction, maxSettlementsPerFaction }, topUp);

        private static int OpenSites(WorldState s, int f)
        {
            int n = 0;
            var e = s.Economy;
            foreach (var b in e.Buildings) if (b.Faction == f && (b.State == BuildingState.Site || b.State == BuildingState.Constructing)) n++;
            foreach (var r in e.Roads) if (r.Faction == f && (r.State == RoadState.Site || r.State == RoadState.Constructing)) n++;
            foreach (var st in e.Settlements)
                if (st.Faction == f && (st.State == SettlementState.Site || st.Upgrade != UpgradeState.None)) n++;
            return n;
        }

        private static void Issue(WorldSim sim, int f, int maxSettlements)
        {
            var s = sim.State;
            var e = s.Economy;
            var t = s.Board.Topology;
            if (OpenSites(s, f) >= 4) return;

            int active = 0;
            foreach (var st in e.Settlements) if (st.Faction == f && st.State != SettlementState.Cancelled) active++;

            // 1. A camp on any legal corner.
            if (active < maxSettlements)
                for (int c = 0; c < t.CornerCount; c++)
                    if (EconomyApi.CheckSettlement(s, f, c).Ok) { sim.Submit(new FoundSettlement { PlayerId = f, Corner = c }); return; }

            // 2. Buildings on free plots of finished camps (improved collectors first, then workshops).
            foreach (var st in e.Settlements)
            {
                if (st.Faction != f || st.State != SettlementState.Active) continue;
                foreach (int plot in EconomyApi.PlotsOf(s, st.Id))
                {
                    if (EconomyApi.Plot(s, plot).Use != PlotUse.Free) continue;
                    for (int ty = 5; ty < BuildingDefs.Count; ty++)
                        if (EconomyApi.CheckBuilding(s, f, (BuildingType)ty, plot).Ok)
                        {
                            sim.Submit(new PlaceBuilding { PlayerId = f, Type = (BuildingType)ty, Plot = plot });
                            return;
                        }
                }
            }

            // 3. Cities.
            foreach (var st in e.Settlements)
                if (st.Faction == f && st.State == SettlementState.Active && EconomyApi.CheckCity(s, f, st.Corner).Ok)
                {
                    sim.Submit(new UpgradeCity { PlayerId = f, Corner = st.Corner });
                    return;
                }

            // 4. A road: prefer one that leads towards a corner two edges from the network (a future camp).
            if (active < maxSettlements)
            {
                int best = -1, bestScore = int.MinValue;
                for (int he = 0; he < t.HexEdgeCount; he++)
                {
                    if (!EconomyApi.CheckRoad(s, f, he).Ok) continue;
                    int a = t.HexEdgeCornerA(he), b = t.HexEdgeCornerB(he);
                    int score = 0;
                    foreach (int c in new[] { a, b })
                    {
                        if (PlotLayout.IsSettlementCorner(t, c)) score += 2;
                        foreach (int n in t.CornerNeighbours(c)) if (PlotLayout.IsSettlementCorner(t, n)) score++;
                    }
                    score = score * 1000 - he % 997;
                    if (score > bestScore) { bestScore = score; best = he; }
                }
                if (best >= 0) sim.Submit(new PlaceRoad { PlayerId = f, HexEdge = best });
            }
        }

        /// <summary>Stress fixture only: fills every resource hex to its cap with mature stock (breaks the stock ledger).</summary>
        public static void FillHexesToCap(WorldState s)
        {
            var e = s.Economy;
            foreach (var h in e.Hexes)
            {
                if (TerrainInfo.RawWare(s.Board.HexTerrain[h.Hex]) == null) continue;
                int young = 0;
                foreach (var c in h.Young) young += c.Count;
                h.Mature = System.Math.Max(0, e.Tuning.HexCap - young);
            }
        }

        /// <summary>Summary line for logs.</summary>
        public static string Describe(WorldState s)
        {
            var e = s.Economy;
            int[] camps = new int[2], cities = new int[2], blds = new int[2], roads = new int[2];
            foreach (var st in e.Settlements) if (st.State == SettlementState.Active) { camps[st.Faction]++; if (st.IsCity) cities[st.Faction]++; }
            foreach (var b in e.Buildings) if (b.State != BuildingState.Retired && b.State != BuildingState.Cancelled) blds[b.Faction]++;
            foreach (var r in e.Roads) if (r.State == RoadState.Built) roads[r.Faction]++;
            return "tick " + s.Tick + ": camps " + camps[0] + "/" + camps[1] + ", cities " + cities[0] + "/" + cities[1]
                + ", buildings " + blds[0] + "/" + blds[1] + ", roads " + roads[0] + "/" + roads[1]
                + ", carriers " + e.Carriers.Count + ", warriors " + e.Warriors.Count;
        }
    }
}
