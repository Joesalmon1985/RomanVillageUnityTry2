using System.Linq;
using NUnit.Framework;
using World.Sim.Economy;
using K = World.Sim.Tests.SimTestKit;

namespace World.Sim.Tests
{
    /// <summary>Placement rules and refusal sentences (REFACTOR_SPEC 2.B, 5.2), roads, new settlements, cities and cancelling.</summary>
    public class SettlementTests
    {
        private static void Refused(CommandResult r, string code, string sentence)
        {
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(code, r.Reason);
            Assert.AreEqual(sentence, r.Message);
        }

        [Test]
        public void BuildingRefusals()
        {
            var s = WorldSimFactory.CreateNew(K.DefaultSeed).State;
            Refused(EconomyApi.CheckBuilding(s, 0, (BuildingType)99, 631), "unknown_type", "There is no such building.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.LogPile, 631), "not_placeable", "Log pile comes with a camp and cannot be built.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, -1), "unknown_plot", "There is no plot there.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, 0), "unknown_plot", "There is no plot there.");
            int lonely = s.Board.Topology.Corners.First(c => PlotLayout.IsSettlementCorner(s.Board.Topology, c) && EconomyApi.SettlementAt(s, c) == null);
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, lonely * 9), "no_settlement", "None of your camps owns that plot.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, 97 * 9 + 1), "not_owner", "That belongs to another people.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, 635), "plot_reserved", "That ground is kept for the dance circle.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, 632), "plot_locked", "That plot opens when the camp becomes a city.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, 630), "plot_taken", "Something already stands on that plot.");
            Refused(EconomyApi.CheckBuilding(s, 0, BuildingType.ClayPit, 631), "wrong_terrain", "A clay pit must stand by hills.");
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 0, BuildingType.WoodcutterHut, 631).Ok);
            Assert.IsTrue(EconomyApi.CheckBuilding(s, 0, BuildingType.Sawmill, 631).Ok);
        }

        [Test]
        public void AlreadyBuilt_OneOfEachTypePerSettlement()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            K.Do(sim, new PlaceBuilding { Type = BuildingType.Sawmill, Plot = 631 });
            Refused(EconomyApi.CheckBuilding(sim.State, 0, BuildingType.Sawmill, 634), "already_built", "This camp already has a sawmill.");
        }

        [Test]
        public void Roads_Settlements_Refusals_ThenASecondCamp()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            var t = s.Board.Topology;
            int home = s.Board.StartCorners[0];
            int far = t.HexEdgeCount - 1;
            Refused(EconomyApi.CheckRoad(s, 0, far), "not_connected", "None of your roads reaches there.");
            Refused(EconomyApi.CheckSettlement(s, 0, home), "occupied", "A camp already stands there.");
            int near = t.CornerNeighbours(home).First(c => PlotLayout.IsSettlementCorner(t, c));
            Refused(EconomyApi.CheckSettlement(s, 0, near), "too_close", "Too close to another camp: leave a road's length between camps.");
            int outer = t.Corners.First(c => !PlotLayout.IsSettlementCorner(t, c));
            Refused(EconomyApi.CheckSettlement(s, 0, outer), "bad_corner", "A camp needs a corner where three lands meet.");
            Refused(EconomyApi.CheckCity(s, 0, near), "not_a_settlement", "There is no camp of yours there.");
            Refused(EconomyApi.CheckCity(s, 0, s.Board.StartCorners[1]), "not_owner", "That belongs to another people.");
            int rivalEdge = t.CornerHexEdges(s.Board.StartCorners[1]).First();
            Refused(EconomyApi.CheckRoad(s, 0, rivalEdge), "blocked_by_rival", "A camp of another people blocks that road.");

            // Two roads out of home along a path, then a camp at its end.
            int target = t.Corners.First(c => t.CornerDistance(home, c) == 2 && EconomyApi.CheckSettlement(s, 0, c).Reason == "not_connected"
                && t.CornerNeighbours(c).All(n => EconomyApi.SettlementAt(s, n) == null));
            var path = t.FindCornerPath(home, target);
            Assert.AreEqual(3, path.Length);
            int e1 = t.HexEdgeBetween(path[0], path[1]), e2 = t.HexEdgeBetween(path[1], path[2]);
            Assert.AreEqual(SiteKind.Road, K.Do(sim, new PlaceRoad { HexEdge = e1 }).OfType<SitePlaced>().Single().Kind);
            Refused(EconomyApi.CheckRoad(s, 0, e1), "road_exists", "A road already runs there.");
            Refused(EconomyApi.CheckRoad(s, 0, e2), "not_connected", "None of your roads reaches there.");
            K.Step(sim, 110);
            Assert.AreEqual(RoadState.Built, EconomyApi.RoadOn(s, e1).State);
            K.Do(sim, new PlaceRoad { HexEdge = e2 });
            Assert.AreEqual(0, EconomyApi.RoadOn(s, e2).SourceSettlement);
            K.Step(sim, 110);
            Assert.IsTrue(EconomyApi.CheckSettlement(s, 0, target).Ok);
            var ev = K.Do(sim, new FoundSettlement { Corner = target });
            int id = ev.OfType<SitePlaced>().Single().Id;
            Assert.AreEqual(SettlementState.Site, s.Economy.Settlements[id].State);
            Assert.AreEqual(0, s.Economy.Settlements[id].SourceSettlement);
            ev = K.Step(sim, 310);
            Assert.IsTrue(ev.OfType<SettlementFounded>().Any(f => f.SettlementId == id && f.Corner == target));
            Assert.AreEqual(2, s.Economy.Carriers.Count(c => c.Home == id));
            Assert.AreEqual(2, World.Sim.Factions.FactionApi.Strength(s, 0).Total);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void CityUpgrade_DrawsFromItsOwnStock_AndCancelReturnsGoods()
        {
            var sim = WorldSimFactory.CreateNew(K.DefaultSeed);
            var s = sim.State;
            int home = s.Board.StartCorners[0];
            var st = s.Economy.Settlements[0];
            K.Do(sim, new UpgradeCity { Corner = home });
            Refused(EconomyApi.CheckCity(s, 0, home), "upgrade_in_progress", "This camp is already being raised to a city.");
            Assert.AreEqual("The city is waiting for planks.", EconomyApi.WaitingSentence(s, SiteKind.City, 0));
            st.Stock[(int)Ware.Planks] = 1;
            s.Economy.Ledger.Starting[(int)Ware.Planks] += 1;
            K.Step(sim, 1);
            Assert.AreEqual(1, st.UpgradeDelivered[(int)Ware.Planks]);
            K.Do(sim, new CancelSite { Kind = SiteKind.City, Id = 0 });
            Assert.AreEqual(1, st.Stock[(int)Ware.Planks]);
            Assert.AreEqual(UpgradeState.None, st.Upgrade);
            Refused(sim.Check(new CancelSite { PlayerId = 0, Kind = SiteKind.City, Id = 0 }), "not_a_site", "There is no unfinished site there.");
            Assert.IsNull(K.CheckInvariants(s));

            foreach (var w in new[] { Ware.Planks, Ware.Bricks, Ware.CutStone, Ware.Bread })
            {
                st.Stock[(int)w] += 2;
                s.Economy.Ledger.Starting[(int)w] += 2;
            }
            K.Do(sim, new UpgradeCity { Corner = home });
            var ev = K.Step(sim, 410);
            Assert.IsTrue(ev.OfType<CityUpgraded>().Any());
            Assert.IsTrue(st.IsCity);
            Refused(EconomyApi.CheckCity(s, 0, home), "already_city", "This camp is already a city.");
            Assert.AreEqual(2, World.Sim.Factions.FactionApi.Strength(s, 0).Total);
            Assert.IsNull(K.CheckInvariants(s));
        }

        [Test]
        public void Names()
        {
            var s = WorldSimFactory.CreateNew(K.DefaultSeed).State;
            Assert.AreEqual("the home camp", EconomyApi.SettlementName(s, 0));
            Assert.AreEqual("the Ash Clan's home camp", EconomyApi.SettlementName(s, 1));
            Assert.AreEqual("2 wood + 1 clay", EconomyApi.CostText(BuildingDefs.Get(BuildingType.Farm).Cost));
            Assert.AreEqual("#8B5A2B", EconomyApi.WareColour(Ware.Wood));
            Assert.AreEqual(16, BuildingDefs.All.Count);
            Assert.IsTrue(BuildingDefs.All.All(d => d.Purpose.EndsWith(".")));
            Assert.AreEqual(BuildingType.WoodcutterHut, BuildingDefs.ImprovedFor(Terrain.Forest));
            Assert.AreEqual(BuildingType.RockHeap, BuildingDefs.PrimitiveFor(Terrain.Mountain));
        }
    }
}
