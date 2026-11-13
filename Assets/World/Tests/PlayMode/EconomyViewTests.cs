using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;

namespace World.Game.Tests
{
    /// <summary>
    /// Road economy in the scene: views reconciled from state, carriers with loads mid-road, God View tools and their
    /// refusals, an interrupted shipment, first-person access, and the commission played through the tools. The sim is
    /// stepped with GameClock.StepOnce (clock paused with a "Test" reason) so every tick reaches the views. Fixtures use
    /// the unified-economy ids (Woodcutter, Clay pit, Brick kiln; Logs, Planks, Bricks) and read board ids from state.
    /// </summary>
    public class EconomyViewTests
    {
        private const string TestPause = "Test";
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;
        private EconomyViews V => EconomyViews.Instance;

        private IEnumerator Load(bool pause = true)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot, "WorldBootstrap.Instance");
            Assert.IsNotNull(V, "EconomyViews installed");
            if (pause) _boot.Clock.Pause(TestPause);
        }

        private void Step(int n) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }
        private int RoadCount() => S.Economy.Roads.Count(r => r);
        private int LiveBuildings() => S.Economy.Buildings.Count(b => b.State != BuildingState.Removed && b.Type != BuildingType.Storehouse);

        [UnityTest]
        public IEnumerator Views_OnePerBuildingRoadAndCarrier_FromState()
        {
            yield return Load();
            Step(2);
            Assert.AreEqual(2, RoadCount(), "starter demonstration road has two segments");
            Assert.AreEqual(RoadCount(), V.RoadViewCount, "one road view per segment");
            Assert.AreEqual(RoadCount(), Object.FindObjectsByType<RoadView>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(S.Economy.Carriers.Count, V.CarrierViewCount, "one carrier view per carrier");
            Assert.AreEqual(LiveBuildings(), V.BuildingViewCount, "the forester site");
            Assert.IsTrue(V.HasFlag(S.Economy.DemoVertexA) && V.HasFlag(S.Economy.DemoVertexB) && V.HasFlag(S.Board.StorehouseVertex), "flags at the road ends");
            foreach (var b in S.Economy.Buildings.Where(b => b.Type != BuildingType.Storehouse && b.State != BuildingState.Removed))
                Assert.AreEqual(b.State, V.Building(b.Id).ShownState, "view state of " + b.Type);

            // Storehouse lines show the starter stock; building lines carry the sim's sentence.
            var lines = string.Join("\n", VillageBuilder.Storehouse.Lines);
            StringAssert.Contains("Planks</color> " + EconomyApi.StorehouseCount(S, Ware.Planks), lines);
            var site = V.Buildings.First();
            StringAssert.Contains(EconomyApi.WaitingFor(S, site.BuildingId).Sentence, string.Join("\n", site.Lines));

            // Batches authorised at the first round start are marked, on every hex that has one.
            int hexes = S.Economy.HexBatchRemaining.Count(n => n > 0);
            Assert.Greater(hexes, 0, "round 1 authorised batches");
            Assert.AreEqual(hexes, V.BatchMarkerCount, "one batch marker per hex with units left");
        }

        [UnityTest]
        public IEnumerator EveryBuildingType_HasDistinctModel_InsideItsFootprint()
        {
            yield return Load();
            var root = new GameObject("ModelTest").transform;
            root.position = new Vector3(0, -200f, 0);
            var signatures = new HashSet<string>();
            foreach (BuildingType t in System.Enum.GetValues(typeof(BuildingType)))
            {
                var holder = new GameObject(t.ToString()).transform;
                holder.SetParent(root, false);
                var m = BuildingModels.Build(t, holder);
                var rs = m.Root.GetComponentsInChildren<Renderer>();
                Assert.Greater(rs.Length, 0, t + " has renderers");
                var bounds = rs[0].bounds;
                foreach (var r in rs) bounds.Encapsulate(r.bounds);
                float half = BuildingPlots.HalfFor(EconomyApi.Def(S, t).Size);
                var c = bounds.center - holder.position;
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(c.x - bounds.extents.x), Mathf.Abs(c.x + bounds.extents.x)), half + 0.45f, t + " fits its footprint in x");
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(c.z - bounds.extents.z), Mathf.Abs(c.z + bounds.extents.z)), half + 0.45f, t + " fits its footprint in z");
                Assert.GreaterOrEqual(bounds.min.y - holder.position.y, -0.05f, t + " does not sink");
                int verts = m.Root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.vertexCount);
                var sz = bounds.size;
                Assert.IsTrue(signatures.Add(verts + ":" + rs.Length + ":" + Mathf.RoundToInt(sz.x * 10) + "x" + Mathf.RoundToInt(sz.y * 10) + "x" + Mathf.RoundToInt(sz.z * 10)),
                    t + " looks different from the others (vertices, parts and size)");
            }
            Object.Destroy(root.gameObject);

            // Every placeable type on every flag of the home territory: clear of the road lines and the village.
            var topo = S.Board.Topology;
            var flags = Enumerable.Range(0, topo.VertexCount).Where(v => EconomyApi.IsInTerritory(S, PlayerIds.Human, v)
                && v != S.Board.WarehouseVertex && !S.Economy.Cities.Any(c => c.Vertex == v)).ToList();
            Assert.Greater(flags.Count, 10);
            foreach (var view in EconomyApi.BuildMenu(S))
                foreach (int v in flags)
                {
                    var p = BuildingPlots.For(S, _boot.Layout, v, view.Type);
                    var q = Quaternion.Euler(0, p.Yaw, 0);
                    foreach (var corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) })
                    {
                        var w = p.Centre + q * (corner * p.Half);
                        foreach (int he in topo.HexSides(BoardSpace.HexAt(p.Centre)))
                        {
                            BoardSpace.HexEdgeEnds(topo, he, out var a, out var b);
                            Assert.GreaterOrEqual(BoardSpace.DistanceToSegment(w, a, b), TerrainBuilder.RoadMargin - 0.01f, view.Name + " on flag " + v + " stays off the road line");
                        }
                    }
                    Assert.AreEqual(0f, BuildingPlots.VillageOverlap(_boot.Layout, p.Centre, p.Half * 1.42f), 0.01f, view.Name + " on flag " + v + " clear of the village");
                }
        }

        [UnityTest]
        public IEnumerator RebuildFromState_ReproducesTheScene()
        {
            yield return Load();
            Step(40);
            int roads = V.RoadViewCount, carriers = V.CarrierViewCount, buildings = V.BuildingViewCount, flags = V.FlagViewCount, batches = V.BatchMarkerCount;
            var positions = V.Buildings.ToDictionary(b => b.BuildingId, b => b.transform.position);
            V.DestroyAllViews();
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<RoadView>(FindObjectsSortMode.None).Length, "all road views destroyed");
            Assert.AreEqual(0, Object.FindObjectsByType<CarrierView>(FindObjectsSortMode.None).Length, "all carrier views destroyed");
            V.RebuildFromState();
            yield return null;
            Assert.AreEqual(roads, V.RoadViewCount);
            Assert.AreEqual(carriers, V.CarrierViewCount);
            Assert.AreEqual(buildings, V.BuildingViewCount);
            Assert.AreEqual(flags, V.FlagViewCount);
            Assert.AreEqual(batches, V.BatchMarkerCount);
            foreach (var b in V.Buildings) Assert.Less((b.transform.position - positions[b.BuildingId]).magnitude, 0.001f, "same plot after rebuild");
            // Carriers are placed from state alone.
            foreach (var c in S.Economy.Carriers.Where(c => c.CurrentEdge >= 0))
            {
                var cv = V.Carrier(c.Id);
                var a = BoardSpace.Vertex(c.FromVertex); var bb = BoardSpace.Vertex(c.ToVertex);
                Assert.Less(BoardSpace.DistanceToSegment(cv.transform.position, a, bb), 1f, "carrier " + c.Id + " on its segment after rebuild");
            }
            // Roads are not rescanned every tick.
            int scans = V.RoadScans;
            Step(30);
            Assert.AreEqual(scans, V.RoadScans, "no road rescans without road events");
        }

        [UnityTest]
        public IEnumerator CarrierWithLoad_IsVisibleMidRoad_AndInterpolates()
        {
            yield return Load();
            Step(1);
            var c = S.Economy.Carriers.First(x => x.HasLoad && x.CurrentEdge >= 0);
            int edge = c.CurrentEdge;
            var view = V.Carrier(c.Id);
            Assert.IsTrue(view.HasLoad && view.CrateVisible, "the load crate shows");
            var crate = view.transform.Find("Load/Crate").GetComponent<Renderer>();
            Assert.AreEqual((Color32)EconomyLook.WareColor(c.Load), (Color32)crate.sharedMaterial.color, "crate in the ware's colour");
            var a = BoardSpace.Vertex(c.FromVertex);
            var b = BoardSpace.Vertex(c.ToVertex);
            float last = -1f;
            int samples = 0;
            bool sawMidway = false;
            for (int frame = 0; frame < 400 && S.Economy.Carriers[c.Id].CurrentEdge == edge; frame++)
            {
                if (frame % 3 == 0) Step(1);
                yield return null;
                if (S.Economy.Carriers[c.Id].CurrentEdge != edge) break;
                var p = view.transform.position;
                var ab = b - a; ab.y = 0;
                var ap = p - a; ap.y = 0;
                float t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
                float lateral = (ap - ab * t).magnitude;
                Assert.That(t, Is.InRange(-0.001f, 1.001f), "on the segment between the two flags");
                Assert.LessOrEqual(lateral, RoadView.Width * 0.5f, "within the road width");
                Assert.GreaterOrEqual(t, last - 1e-4f, "moves monotonically from flag to flag");
                if (t > 0.35f && t < 0.65f) sawMidway = true;
                last = t;
                samples++;
            }
            Assert.Greater(samples, S.Economy.Tuning.TicksPerEdge, "sampled between ticks (interpolated)");
            Assert.IsTrue(sawMidway, "seen mid-road with its load");

            // God View shows the load from above: a disc in the ware colour and a larger crate.
            _boot.Views.SetView(ViewMode.God);
            yield return null;
            var disc = view.transform.Find("GodDisc");
            Assert.IsTrue(disc.gameObject.activeInHierarchy || !view.HasLoad, "God View disc under the loaded carrier");
        }

        [UnityTest]
        public IEnumerator InvalidPlacement_IsRefusedWithTheSimSentence()
        {
            yield return Load();
            Step(1);
            _boot.Views.SetView(ViewMode.God);
            var build = (BuildTool)GodToolbar.Instance.Activate(BuildTool.ToolId);
            Assert.IsNotNull(build);
            build.SelectType(BuildingType.Woodcutter);
            build.Refresh();
            CollectionAssert.AreEqual(EconomyApi.LegalBuildFlags(S, BuildingType.Woodcutter), build.LegalFlags.ToList(), "highlights are the sim's legal flags");
            Assert.IsTrue(build.LegalFlags.All(v => EconomyApi.IsInTerritory(S, PlayerIds.Human, v)), "all in territory");
            build.ShowOverlay();
            Assert.AreEqual(build.LegalFlags.Count, GodOverlay.VisibleCount(GodOverlay.Valid), "one green disc per legal flag, nothing else");
            int recomputes = build.Recomputes;
            for (int i = 0; i < 5; i++) { build.Update(); yield return null; }
            Assert.AreEqual(recomputes, build.Recomputes, "legal flags are cached, not recomputed every frame");

            int bad = -1;
            for (int v = 0; v < S.Board.Topology.VertexCount && bad < 0; v++)
                if (EconomyApi.IsInTerritory(S, PlayerIds.Human, v) && EconomyApi.BuildingAt(S, v) < 0 && v != S.Board.WarehouseVertex
                    && !S.Economy.Cities.Any(c => c.Vertex == v) && !build.IsValid(v)) bad = v;
            Assert.GreaterOrEqual(bad, 0, "a free flag in territory away from any forest");
            var result = build.TryPlace(bad);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("A woodcutter must stand next to a forest.", result.Message);
            Assert.AreEqual(result.Message, EconomyHud.LastToast, "toast shows the refusal sentence");
            Assert.IsTrue(EconomyHud.LastToastWasError);

            // A road segment that touches nothing is refused with its sentence too.
            var road = (RoadTool)GodToolbar.Instance.Activate(RoadTool.ToolId);
            road.Refresh();
            int far = -1;
            for (int e = 0; e < S.Board.Topology.EdgeCount && far < 0; e++)
                if (!S.Economy.Roads[e] && road.Check(e).Reason == EconomyRefusals.NotConnected) far = e;
            Assert.GreaterOrEqual(far, 0);
            Assert.IsFalse(road.Candidates.Contains(far), "unconnected segments are not highlighted");
            var rr = road.Place(far);
            Assert.IsFalse(rr.Ok);
            Assert.AreEqual("A road must connect to your roads, city or buildings.", EconomyHud.LastToast);

            // Occupied flag.
            build = (BuildTool)GodToolbar.Instance.Activate(BuildTool.ToolId);
            build.SelectType(BuildingType.Forester);
            var occ = build.TryPlace(S.Economy.DemoVertexB);
            Assert.IsFalse(occ.Ok);
            StringAssert.Contains("already taken", EconomyHud.LastToast);
        }

        [UnityTest]
        public IEnumerator RemovingTheRoadUnderAShipment_ShowsNoRoad_RebuildingDeliversOnce()
        {
            yield return Load();
            var e = S.Economy;
            var topo = S.Board.Topology;
            Step(1);
            var carrier = e.Carriers.First(c => c.HasLoad && c.JobId >= 0);
            int jobId = carrier.JobId;
            var job = EconomyLook.Job(S, jobId);
            int siteId = job.DestId;
            int edge = topo.EdgeBetween(e.DemoVertexA, e.DemoVertexB);

            _boot.Views.SetView(ViewMode.God);
            var road = (RoadTool)GodToolbar.Instance.Activate(RoadTool.ToolId);
            Assert.IsTrue(road.Remove(edge).Ok, "road removed through the tool");
            Step(1);
            Assert.IsNull(V.Road(edge), "its view is gone");

            for (int i = 0; i < 300 && EconomyLook.Job(S, jobId)?.Reason != Reasons.NoRoad; i++) Step(1);
            yield return null;
            Assert.AreEqual(-1, carrier.CurrentEdge, "carrier stopped at a flag");
            Assert.IsTrue(carrier.HasLoad, "keeps its load");
            Assert.IsTrue(V.Carrier(carrier.Id).CrateVisible, "the stopped carrier still shows its load");
            var info = EconomyApi.DescribeJob(S, jobId);
            Assert.AreEqual(Reasons.NoRoad, info.Reason);
            Assert.AreEqual("No road connects the Storehouse to the Forester site.", info.Status);
            StringAssert.Contains(info.Status, string.Join("\n", V.Carrier(carrier.Id).Lines), "carrier inspect shows the sentence");
            StringAssert.Contains(info.Status, string.Join("\n", V.Building(siteId).Lines), "site inspect shows the sentence");

            // Listed under Problems; clicking it focuses and selects the carrier.
            var problems = EconomyGodTools.Problems;
            problems.Refresh();
            var entry = problems.Current.FirstOrDefault(p => p.IsJob && p.Id == jobId);
            Assert.AreEqual(info.Status, entry.Sentence, "job listed under Problems with its sentence");
            var selected = ProblemsTool.Focus(entry);
            Assert.AreSame(V.Carrier(carrier.Id), selected);
            Assert.AreSame(V.Carrier(carrier.Id), _boot.Picker.Selected);

            // Rebuild: the shipment arrives once and the site count rises by exactly the deliveries made.
            var site = e.Buildings[siteId];
            int delivered = 0, prevStock = site.Stock[(int)Ware.Planks];
            bool deltaOk = true;
            System.Action<IReadOnlyList<SimEvent>> watch = evs =>
            {
                int toSite = 0;
                foreach (var ev in evs)
                    if (ev is WareDelivered d)
                    {
                        if (d.JobId == jobId) delivered++;
                        if (d.BuildingId == siteId) toSite++;
                    }
                int now = site.Stock[(int)Ware.Planks];
                if (toSite > 0 && now - prevStock != toSite) deltaOk = false;
                prevStock = now;
            };
            V.Synced += watch;
            Assert.IsTrue(road.Place(edge).Ok, "road rebuilt through the tool: " + GodCommands.LastResult.Message);
            for (int i = 0; i < 400 && delivered == 0; i++) Step(1);
            Step(120);
            V.Synced -= watch;
            Assert.AreEqual(1, delivered, "the same shipment arrives exactly once");
            Assert.IsTrue(deltaOk, "destination count rises by exactly one per arriving shipment");
            Assert.IsNull(EconomyLook.Job(S, jobId), "job finished");
            Assert.IsNotNull(V.Road(edge), "road view back");
        }

        [UnityTest]
        public IEnumerator FirstPerson_BramPointsTheWay_BellEndsTheRound_SignOffersCommands()
        {
            yield return Load();
            Step(2);
            var qm = Npc.Find(NpcIds.Quartermaster);
            qm.Interact();
            yield return null;
            var text = string.Join("\n", DialoguePanel.Instance.CurrentLines);
            StringAssert.Contains("buildings 0/6", text);
            StringAssert.Contains("planks", text);
            StringAssert.Contains("bricks", text);
            StringAssert.Contains("woodcutter", EconomyFirstPerson.Instance.LastHint, "first step: the woodcutter");
            Assert.AreEqual("Show me where", DialoguePanel.Instance.CurrentOptions[0].Label);
            DialoguePanel.Choose(0);
            Assert.IsNotNull(EconomyFirstPerson.Instance.LastBeacon, "a beacon marks the spot");
            Assert.IsTrue(BeaconRegistry.All.Contains(EconomyFirstPerson.Instance.LastBeacon));
            qm.Interact();
            DialoguePanel.Choose(1);
            yield return null;
            Assert.AreEqual(ViewMode.God, _boot.Views.CurrentView, "building plans open God View");
            Assert.AreEqual(BuildTool.ToolId, GodToolbar.Instance.Active.Id, "with the Build tool");
            Assert.AreEqual(BuildingType.Woodcutter, ((BuildTool)GodToolbar.Instance.Active).Selected, "preselected");
            _boot.Views.SetView(ViewMode.FirstPerson);

            // The bell ends the round, exactly like End Round.
            int round = S.Economy.Round;
            VillageBuilder.Bell.Interact();
            Assert.IsTrue(GodCommands.LastCommand is EndRound);
            Assert.IsTrue(GodCommands.LastResult.Ok, GodCommands.LastResult.Message);
            Step(1);
            Assert.AreEqual(round + 1, S.Economy.Round);

            // The sign selects the building and offers the same commands.
            var site = V.Buildings.First();
            site.Sign.Interact();
            yield return null;
            Assert.AreSame(site, _boot.Picker.Selected, "sign selects the building (same inspect panel)");
            Assert.IsTrue(DialoguePanel.Instance.CurrentOptions.Any(o => o.Label.StartsWith("Cancel this site")));
            DialoguePanel.Close();
        }

        [UnityTest]
        public IEnumerator Commission_CompletesInScene_ThroughTheScript()
        {
            yield return Load(pause: false);
            Assert.IsTrue(AutoPlayScripts.TryCreate("commission", out var script), "commission script registered");
            _boot.AutoPlay.Run(script);
            float until = Time.realtimeSinceStartup + 240f;
            while (_boot.AutoPlay.IsRunning && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(_boot.AutoPlay.IsRunning, "script finished in time");
            Assert.IsFalse(_boot.AutoPlay.Failed, _boot.AutoPlay.FailMessage);
            Assert.IsTrue(S.Economy.Commission.Complete);
            Assert.IsTrue(EconomyFirstPerson.Instance.QuartermasterAcknowledged);
            StringAssert.StartsWith("Round ", EconomyHud.LastRoundBanner);
            foreach (var t in EconomyLook.CommissionTypes)
            {
                int id = EconomyApi.FindBuilding(S, t, activeOnly: true);
                Assert.GreaterOrEqual(id, 0, t + " active");
                Assert.AreEqual(BuildingState.Active, V.Building(id).ShownState, t + " view shows it active");
                Assert.IsNotNull(V.Building(id).Model, t + " has its model");
            }
            Assert.AreEqual(RoadCount(), V.RoadViewCount, "road views match roads at the end");
            StringAssert.Contains("Commission complete", Hud.Objective);

            // Age I begins at the next round start with a draft; until a card is picked, End Round (button or bell)
            // is refused and the refusal shows as the sim's sentence.
            _boot.Clock.Pause(TestPause);
            for (int i = 0; i < 20 && S.Economy.RoundStartPending; i++) Step(1);
            Assert.IsTrue(GodCommands.EndRound().Ok, GodCommands.LastResult.Message);
            Step(2);
            Assert.IsTrue(World.Sim.Strategy.StrategyApi.DraftPending(S), "Age I draft dealt");
            VillageBuilder.Bell.Interact();
            Assert.IsFalse(GodCommands.LastResult.Ok);
            Assert.AreEqual("draft_pending", GodCommands.LastResult.Reason);
            Assert.AreEqual(GodCommands.LastResult.Message, EconomyHud.LastToast, "the bell shows the sim's sentence");
            GodToolbar.Instance.Activate(EconomyGodTools.EndRoundId);
            Assert.AreEqual("draft_pending", GodCommands.LastResult.Reason, "the End Round button is refused the same way");
            StringAssert.Contains(GodCommands.LastResult.Message, GodToolbar.Find(EconomyGodTools.EndRoundId).Tip, "its tooltip says why");
        }
    }
}
