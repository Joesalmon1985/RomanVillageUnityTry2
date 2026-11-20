using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Eras;

namespace World.Game.Tests
{
    /// <summary>
    /// Package H1-5 (PRD_AGES 11.1): grade looks, upgrade scaffolds, towns, outer plot rings and owner colours follow
    /// state. The developer start dev.historic gives grown peoples; upgrades go through the real commands (the
    /// storehouses are topped up first, a stress fixture like <see cref="LandLateGame"/>); the clock is stepped by hand.
    /// The capture test calls the sim's internal seam EconomySeams.TransferSettlement by reflection.
    /// </summary>
    public class GradeViewTests
    {
        private const string TestPause = "GradeTest";
        private WorldBootstrap _boot;
        private LandViews _views;
        private WorldState S => _boot.Sim.State;

        private IEnumerator StartHistoric()
        {
            LandViews.CarrierFeed = SimCarrierFeed.Instance;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsTrue(DevStart.Begin(ScenarioStarts.DevHistoric), DevStart.LastError);
            float until = Time.realtimeSinceStartup + 120f;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == ScenarioStarts.DevHistoric))
            {
                Assert.Less(Time.realtimeSinceStartup, until, "dev.historic did not start");
                yield return null;
            }
            yield return null; yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
            _views = LandViews.Instance;
            Assert.IsNotNull(_views); Assert.IsNotNull(GradeViews.Instance, "GradeViewsInstaller ran");
            GradeViews.Instance.Reconcile();
        }

        [TearDown]
        public void TearDown() { LandViews.CarrierFeed = SimCarrierFeed.Instance; }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }
        private bool StepUntil(Func<bool> done, int max, Action each = null) { for (int i = 0; i < max; i++) { if (done()) return true; _boot.Clock.StepOnce(); each?.Invoke(); } return done(); }

        private void TopUp(int amount = 40)
        {
            foreach (var st in S.Economy.Settlements) if (st.State == SettlementState.Active) for (int w = 0; w < st.Stock.Length; w++) st.Stock[w] += amount;
        }

        private IEnumerable<Building> UpgradeCandidates() =>
            S.Economy.Buildings.Where(b => b.Faction == PlayerIds.Human && b.State == BuildingState.Active && b.Grade == Grade.Prehistoric && !b.Upgrading
                && _boot.Sim.Check(new UpgradeBuilding { PlayerId = PlayerIds.Human, BuildingId = b.Id }).Ok);

        private BuildingDress Dress(int id) => _views.Building(id).GetComponent<BuildingDress>();

        // ---------------- the 16 models ----------------

        [Test]
        public void EachType_Grade1_DiffersFromGrade0_AndStaysInItsFootprint()
        {
            var tooBig = new List<string>();
            int types = Enum.GetValues(typeof(BuildingType)).Length;
            Assert.GreaterOrEqual(types, 16);
            for (int t = 0; t < 16; t++)
            {
                var type = (BuildingType)t;
                var a = new MeshBuilder(); LandShapes.Building(a, type, 0);
                var b = new MeshBuilder(); HistoricShapes.Build(b, type, 0);
                var ma = a.ToMesh("g0"); var mb = b.ToMesh("g1");
                Assert.AreNotEqual(ma.vertexCount, mb.vertexCount, type + ": a different model");
                Assert.Greater(mb.vertexCount, ma.vertexCount * 0.9f, type + ": at least as rich");
                Assert.AreNotEqual(ma.bounds, mb.bounds, type + ": different shape");
                float worst = 0; foreach (var v in mb.vertices) worst = Mathf.Max(worst, new Vector2(v.x, v.z).magnitude);
                if (worst > HistoricShapes.Radius) tooBig.Add(type + " " + worst.ToString("0.00") + " m");
                Assert.Greater(mb.bounds.max.y, 1.5f, type + " has height");
                var c = new MeshBuilder(); HistoricShapes.Build(c, type, 1);
                Assert.AreEqual(mb.vertexCount, c.ToMesh("g1b").vertexCount, type + ": the other people's model has the same shape");
                UnityEngine.Object.Destroy(ma); UnityEngine.Object.Destroy(mb);
            }
            CollectionAssert.IsEmpty(tooBig, "models outside the " + HistoricShapes.Radius + " m footprint: " + string.Join(", ", tooBig));
        }

        // ---------------- upgrade: scaffold, goods, completion, cancel, inspect, rebuild ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator Upgrade_ShowsScaffoldAndGoods_ThenNewLook_Cancel_RemovesIt_RebuildEqualsIncremental()
        {
            yield return StartHistoric();
            TopUp();
            var cands = UpgradeCandidates().ToList();
            Assert.GreaterOrEqual(cands.Count, 3, "dev.historic has buildings to improve");
            var first = cands[0]; var second = cands.First(b => b.Type != first.Type); var third = cands.First(b => b.Id != first.Id && b.Id != second.Id);

            Assert.IsFalse(Dress(first.Id).ScaffoldShown);
            var before = Dress(first.Id).ModelMesh;
            Assert.IsTrue(_boot.Sim.Submit(new UpgradeBuilding { PlayerId = PlayerIds.Human, BuildingId = first.Id }).Ok);
            Step(1);
            var d = Dress(first.Id);
            Assert.IsTrue(first.Upgrading);
            Assert.IsTrue(d.ScaffoldShown, "scaffold appears on UpgradeStarted");
            Assert.AreSame(before, d.ModelMesh, "the building stays recognisable while improved");
            Assert.AreNotEqual(-1f, d.ProgressShown, "progress mark");
            Assert.IsTrue(d.PausedShown || d.BuilderShown, "visibly paused (waiting board) or at work");
            var tag = _views.Building(first.Id).GetComponentInChildren<GradeInspect>();
            Assert.IsNotNull(tag);
            var lines = tag.Lines.ToList();
            Assert.IsTrue(lines.Any(l => l.StartsWith("Being improved")), "the sim's sentence: " + string.Join(" / ", lines));
            Assert.IsTrue(lines.Any(l => l.Contains("being improved to Historic")), string.Join(" / ", lines));

            int bursts = GradeViews.Instance.DustBursts;
            Assert.IsTrue(StepUntil(() => !first.Upgrading, 4000), "the upgrade finishes");
            Assert.AreEqual(Grade.Historic, first.Grade);
            Assert.IsFalse(d.ScaffoldShown, "scaffold goes on completion");
            Assert.AreEqual(Grade.Historic, d.ShownGrade);
            Assert.AreNotSame(before, d.ModelMesh, "the new look");
            Assert.AreEqual(bursts + 1, GradeViews.Instance.DustBursts, "dust and cheer");
            Assert.IsNotNull(_views.Building(first.Id).transform.Find("Upgrade dust and cheer"), "dust-and-cheer object");
            Assert.IsTrue(tag.Title.Contains("Historic"), tag.Title);
            Assert.IsTrue(tag.Lines.Any(l => l == "Historic grade."), string.Join(" / ", tag.Lines));

            // Cancel.
            var d2 = Dress(second.Id); var m2 = d2.ModelMesh;
            Assert.IsTrue(_boot.Sim.Submit(new UpgradeBuilding { PlayerId = PlayerIds.Human, BuildingId = second.Id }).Ok);
            Step(40);
            Assert.IsTrue(d2.ScaffoldShown);
            Assert.IsTrue(_boot.Sim.Submit(new CancelUpgrade { PlayerId = PlayerIds.Human, BuildingId = second.Id }).Ok);
            Step(1);
            Assert.IsFalse(d2.ScaffoldShown, "scaffold goes on cancel");
            Assert.AreEqual(0, d2.GoodsShown); Assert.IsFalse(d2.PausedShown); Assert.IsFalse(d2.BuilderShown);
            Assert.AreSame(m2, d2.ModelMesh); Assert.AreEqual(Grade.Prehistoric, d2.ShownGrade);

            // Rebuilt from state equals incremental, with an upgrade still under way.
            Assert.IsTrue(_boot.Sim.Submit(new UpgradeBuilding { PlayerId = PlayerIds.Human, BuildingId = third.Id }).Ok);
            Step(60);
            Assert.IsTrue(Dress(third.Id).ScaffoldShown);
            var inc = GradeViews.Instance.Snapshot(); var incLand = _views.Snapshot();
            int rebuilds = _views.RebuildCount;
            _views.RebuildFromState();
            yield return null; yield return null;
            GradeViews.Instance.Reconcile();
            Assert.AreEqual(rebuilds + 1, _views.RebuildCount);
            CollectionAssert.AreEqual(inc, GradeViews.Instance.Snapshot(), "grade dress after a rebuild equals incremental");
            CollectionAssert.AreEqual(incLand, _views.Snapshot());
            Assert.AreEqual(Grade.Historic, Dress(first.Id).ShownGrade, "a rebuilt scene shows the right grade");
            Assert.IsTrue(Dress(third.Id).ScaffoldShown);
            Assert.AreEqual(inc.Count(x => x.StartsWith("grade ")), _views.Buildings.Count(), "every building view is dressed");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryBuildingType_ShowsDifferentModel_AtGrade1_OnItsView()
        {
            yield return StartHistoric();
            // Fixture: grade 1 on one building of each type present (the grade-1 model is read from state), then the model differs from grade 0.
            var seen = new HashSet<BuildingType>();
            int n = 0;
            foreach (var b in S.Economy.Buildings.Where(x => x.State == BuildingState.Active && x.Grade == Grade.Prehistoric))
            {
                if (!seen.Add(b.Type)) continue;
                var d = Dress(b.Id); var g0 = d.ModelMesh;
                Assert.IsNotNull(g0, b.Type + " has a model");
                b.Grade = Grade.Historic; // test fixture: the dress is a pure function of state
                GradeViews.Instance.Reconcile();
                Assert.AreNotSame(g0, d.ModelMesh, b.Type + ": grade 1 shows another model");
                Assert.AreNotEqual(g0.vertexCount, d.ModelMesh.vertexCount, b.Type.ToString());
                Assert.LessOrEqual(d.ModelMesh.bounds.extents.x + Mathf.Abs(d.ModelMesh.bounds.center.x), HistoricShapes.Radius, b.Type.ToString());
                n++;
            }
            Assert.GreaterOrEqual(n, 12, "most types are in a grown dev.historic start: " + n);
        }

        // ---------------- towns, outer plots, light ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator Town_ChangesTheCamp_OuterPlotsFollowState_LightBudgetUnchanged()
        {
            yield return StartHistoric();
            var cities = S.Economy.Settlements.Where(c => c.State == SettlementState.Active && c.Faction == PlayerIds.Human && c.IsCity && !c.IsTown).ToList();
            Assert.GreaterOrEqual(cities.Count, 2);
            var city = cities[0]; var other = cities[1];
            var sv = _views.Settlement(city.Id); var dress = sv.GetComponent<SettlementDress>();
            Assert.IsFalse(dress.TownShown); Assert.AreEqual(0, dress.OuterOpenShown, "locked outer plots are not shown");
            Assert.IsNull(sv.transform.Find("TownDress"));
            Assert.AreEqual(0, EconomyApi.PlotsOf(S, city.Id).Count(PlotLayoutOuter.IsOuter));
            int lightsBefore = WorldLights.Registered;
            var baseTotem = sv.Totem;

            TopUp();
            Assert.IsTrue(_boot.Sim.Submit(new UpgradeSettlement { PlayerId = PlayerIds.Human, Corner = city.Corner }).Ok);
            Assert.IsTrue(StepUntil(() => city.IsTown, 6000, () => { if (S.Tick % 50 == 0) TopUp(10); }), "the town is built");
            Step(2);
            Assert.IsTrue(dress.TownShown, "town visuals follow state");
            Assert.IsNotNull(sv.transform.Find("TownDress"));
            Assert.IsTrue(dress.OwnTotemShown, "a town has its own taller totem");
            Assert.IsFalse(baseTotem != null && baseTotem.gameObject.activeSelf, "the city totem is replaced");
            Assert.GreaterOrEqual(dress.TownTorches, 3, "more torches");
            int open = EconomyApi.PlotsOf(S, city.Id).Count(p => PlotLayoutOuter.IsOuter(p) && EconomyApi.Plot(S, p).Use == PlotUse.Free);
            Assert.Greater(open, 0, "a town has open outer plots");
            Assert.AreEqual(open, dress.OuterOpenShown, "one subtle ring and way per open outer plot");
            Assert.AreEqual(open, dress.OuterWaysShown);
            Assert.AreEqual(EconomyApi.PlotsOf(S, city.Id).Count(p => EconomyApi.Plot(S, p).Use == PlotUse.Free), sv.FreePlotRings, "the camp's rings include the open outer plots");
            Assert.AreEqual(0, other.IsTown ? -1 : _views.Settlement(other.Id).GetComponent<SettlementDress>().OuterOpenShown, "a city that is not a town shows no outer plots");
            var tagLines = sv.GetComponentInChildren<SettlementInspect>().Lines.ToList();
            Assert.IsTrue(tagLines.Any(l => l.StartsWith("A town")), string.Join(" / ", tagLines));

            // Rebuilt equals incremental for the town too.
            var inc = GradeViews.Instance.Snapshot();
            _views.RebuildFromState(); yield return null; yield return null; GradeViews.Instance.Reconcile();
            CollectionAssert.AreEqual(inc, GradeViews.Instance.Snapshot());

            // Night: the per-pixel budget is unchanged by the extra torches.
            Assert.AreEqual(6, WorldLights.PixelBudget);
            LightDecorator.Instance.ScanNow(); // the rebuilt views get their fire lights at the next scan (WU-8: the scan is not frame-exact after a rebuild)
            Assert.GreaterOrEqual(WorldLights.Registered, lightsBefore + dress.TownTorches);
            DayNight.Instance.SetHour(23.0);
            LightDecorator.Instance.ScanNow();
            var c = BoardSpace.Vertex(S.Board.Topology, city.Corner);
            _boot.Avatar.Teleport(c + new Vector3(9f, 0.05f, 4f), -115f, 6f);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.LessOrEqual(WorldLights.Instance.PixelLightsNow, WorldLights.PixelBudget);
            Assert.AreEqual(0, WorldLights.Instance.VertexLightsNow);
        }

        // ---------------- capture: new owner ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator Transfer_Recolours_TotemRingAndBuildings_WithoutRebuilding()
        {
            yield return StartHistoric();
            var camp = S.Economy.Settlements.First(c => c.State == SettlementState.Active && c.Faction == PlayerIds.Rival && !c.IsCity);
            var sv = _views.Settlement(camp.Id); var dress = sv.GetComponent<SettlementDress>();
            var bviews = S.Economy.Buildings.Where(b => b.Settlement == camp.Id && b.State == BuildingState.Active).Select(b => _views.Building(b.Id)).Where(v => v != null).ToList();
            Assert.IsNotEmpty(bviews, "the camp has buildings");
            Assert.AreEqual(PlayerIds.Rival, dress.OwnerShown);
            var ringBefore = sv.transform.Find("CampRing").GetComponent<MeshFilter>().sharedMesh;
            int rebuilds = _views.RebuildCount;

            var ev = new EventBuffer();
            var seam = Type.GetType("World.Sim.Economy.EconomySeams, World.Sim").GetMethod("TransferSettlement"); // internal seam, as the campaign overrun calls it
            Assert.IsTrue((bool)seam.Invoke(null, new object[] { S, camp.Id, PlayerIds.Human, ev }), "the sim's capture seam");
            _views.Inject(ev.Items);
            Assert.AreEqual(rebuilds, _views.RebuildCount, "nothing is rebuilt");
            Assert.AreSame(sv, _views.Settlement(camp.Id), "the same camp view");
            Assert.AreEqual(PlayerIds.Human, dress.OwnerShown);
            Assert.AreNotSame(ringBefore, sv.transform.Find("CampRing").GetComponent<MeshFilter>().sharedMesh, "ring stones in the new colours");
            Assert.AreSame(LandLook.Cached("camp/ring/" + PlayerIds.Human, mb => SettlementDress.CampRing(mb, PlayerIds.Human)), sv.transform.Find("CampRing").GetComponent<MeshFilter>().sharedMesh);
            Assert.IsTrue(dress.OwnTotemShown, "a totem of the new owner");
            Assert.IsFalse(sv.Totem.gameObject.activeSelf, "the old people's totem is hidden");
            foreach (var bv in bviews)
                StringAssert.EndsWith("/" + PlayerIds.Human, bv.GetComponent<BuildingDress>().ModelKey, "building colours of " + bv.Type);
            var inc = GradeViews.Instance.Snapshot();
            _views.RebuildFromState(); yield return null; yield return null; GradeViews.Instance.Reconcile();
            CollectionAssert.AreEqual(inc, GradeViews.Instance.Snapshot(), "a rebuilt scene shows the new owner too");
        }
    }
}
