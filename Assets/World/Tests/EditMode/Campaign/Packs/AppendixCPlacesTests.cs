using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using World.Sim.Campaign;
using World.Sim.Eras;

namespace World.Sim.Tests
{
    /// <summary>
    /// PRD_CRYSTALS appendix C, pinned on the default board: every authored place is where the appendix puts it, on
    /// a legal anchor (a Meadow hex with no number, or a rim corner), on a lattice cell, walkable (on the island, off
    /// every plot and camp), and not shared. The towers follow the bearing rule (earth 17, fire 74, water 2, air 147).
    /// </summary>
    public class AppendixCPlacesTests
    {
        private static readonly Dictionary<string, (int q, int r)> Sites = new Dictionary<string, (int, int)>
        {
            { "glade", (0, -1) }, { "ash_cut", (-2, -2) }, { "kiln", (4, -1) }, { "forge", (-4, 2) },
            { "causeway", (1, -4) }, { "cistern", (2, -3) }, { "shoulder", (3, 1) }, { "cliff", (1, 3) },
        };

        /// <summary>Places of appendix C that later packages author (Gannet Watch, Old Marl's landing).</summary>
        private static readonly (int q, int r)[] LaterSites = { (2, 2), (0, -4) };

        [Test]
        public void Towers_FollowTheBearingRule_OnRimCorners()
        {
            var b = BoardGenerator.CreateMvpBoard(1);
            var t = b.Topology;
            CollectionAssert.AreEqual(new[] { 17, 74, 2, 147 }, LandmarkApi.TowerCorners(t));
            var d = PackFiles.Load(PackFiles.Authored);
            var corners = new HashSet<int>();
            foreach (var e in new[] { Element.Earth, Element.Fire, Element.Water, Element.Air })
            {
                var tower = d.Towers.Single(x => x.Element == e);
                Assert.AreEqual("tower_" + e.ToString().ToLowerInvariant(), tower.Id);
                var loc = d.Location(tower.Location);
                Assert.AreEqual(LocationKind.Tower, loc.Kind);
                Assert.AreEqual(AnchorKind.RimCorner, loc.Anchor.Kind);
                int c = CampaignPlaces.AnchorCorner(d, loc, t);
                Assert.AreEqual(CampaignPlaces.RuleTowerCorner(t, e), c, e + " tower corner");
                Assert.Less(t.VertexHexes(c).Count, 3, "a rim corner, where no camp may stand");
                CollectionAssert.DoesNotContain(b.StartCorners, c);
                Assert.IsTrue(corners.Add(c), "not shared");
                // A lattice cell exactly, and every marker walkable.
                Lattice.CellOf(t.VertexX(c), t.VertexZ(c), out int i, out int j);
                Assert.AreEqual(t.VertexX(c), Lattice.X(i, j), 1e-6);
                Assert.AreEqual(t.VertexZ(c), Lattice.Z(i, j), 1e-6);
                foreach (var m in loc.Markers) AssertWalkable(t, i + m.I, j + m.J, loc.Id + "/" + m.Id);
            }
        }

        [Test]
        public void Sites_AreOnTheirMeadowHexes_WithinNineMetres_AndWalkable()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var t = CampaignPlaces.FixedBoard.Topology;
            var hexes = new HashSet<int>();
            foreach (var kv in Sites)
            {
                var loc = d.Location(kv.Key);
                Assert.IsNotNull(loc, kv.Key);
                Assert.AreEqual(AnchorKind.MeadowHexCentre, loc.Anchor.Kind, kv.Key);
                Assert.AreEqual(kv.Value, (loc.Anchor.Q, loc.Anchor.R), kv.Key);
                int h = t.HexIndex(new HexCoord(loc.Anchor.Q, loc.Anchor.R));
                Assert.IsTrue(hexes.Add(h), kv.Key + " shares a hex");
                foreach (ulong seed in new ulong[] { 1, 2, 77, 9001 })
                {
                    var b = BoardGenerator.CreateMvpBoard(seed);
                    Assert.AreEqual(Terrain.Meadow, b.HexTerrain[h], kv.Key);
                    Assert.AreEqual(0, b.HexNumber[h], kv.Key + " has no number on seed " + seed);
                }
                Assert.IsTrue(CampaignPlaces.TryAnchorCell(d, loc, CampaignPlaces.FixedBoard, out int i, out int j));
                Assert.AreEqual(t.HexX(h), Lattice.X(i, j), 1e-6, "the hex centre is a lattice cell");
                Assert.AreEqual(t.HexZ(h), Lattice.Z(i, j), 1e-6);
                foreach (var m in loc.Markers)
                {
                    double x = Lattice.X(i + m.I, j + m.J), z = Lattice.Z(i + m.I, j + m.J);
                    Assert.LessOrEqual(Math.Sqrt((x - t.HexX(h)) * (x - t.HexX(h)) + (z - t.HexZ(h)) * (z - t.HexZ(h))), CampaignPlaces.SiteRadius, loc.Id + "/" + m.Id);
                    AssertWalkable(t, i + m.I, j + m.J, loc.Id + "/" + m.Id);
                }
            }
            foreach (var (q, r) in LaterSites)
            {
                int h = t.HexIndex(new HexCoord(q, r));
                Assert.AreEqual(Terrain.Meadow, CampaignPlaces.FixedBoard.HexTerrain[h], "appendix C place " + q + "," + r);
                Assert.IsFalse(hexes.Contains(h));
            }
        }

        [Test]
        public void BlackForge_IsTheWestRimMeadowHexNearestTheAshStart()
        {
            var b = CampaignPlaces.FixedBoard;
            var t = b.Topology;
            int rival = b.StartCorners[1];
            int best = -1;
            double bestD = double.MaxValue;
            for (int h = 0; h < t.HexCount; h++)
            {
                var c = t.Hex(h);
                bool rim = Math.Max(Math.Abs(c.Q), Math.Max(Math.Abs(c.R), Math.Abs(c.Q + c.R))) == BoardGenerator.MvpRadius;
                if (!rim || b.HexTerrain[h] != Terrain.Meadow || t.HexX(h) >= 0) continue;
                double dx = t.HexX(h) - t.VertexX(rival), dz = t.HexZ(h) - t.VertexZ(rival), dd = Math.Round(Math.Sqrt(dx * dx + dz * dz), 6);
                if (dd < bestD) { bestD = dd; best = h; }   // ties: lower hex id
            }
            Assert.AreEqual(new HexCoord(-4, 2), t.Hex(best));
            var forge = PackFiles.Load(PackFiles.Authored).Location("forge");
            Assert.AreEqual((-4, 2), (forge.Anchor.Q, forge.Anchor.R));
        }

        [Test]
        public void HomeCamp_IsThePlayersStartCorner()
        {
            var d = PackFiles.Load(PackFiles.Authored);
            var b = CampaignPlaces.FixedBoard;
            Assert.IsTrue(CampaignPlaces.TryAnchorPoint(d, d.Location("home"), b, out double x, out double z));
            Assert.AreEqual(b.Topology.VertexX(b.StartCorners[0]), x, 1e-9);
            Assert.AreEqual(b.Topology.VertexZ(b.StartCorners[0]), z, 1e-9);
        }

        private static void AssertWalkable(BoardTopology t, int i, int j, string what)
        {
            double x = Lattice.X(i, j), z = Lattice.Z(i, j);
            Assert.IsTrue(CampaignPlaces.OnBoard(t, x, z), what + " is on the island");
            Assert.Greater(CampaignPlaces.NearestPlot(t, x, z, out _), PlotLayout.PlotRadius, what + " is off every plot");
            foreach (int c in CampaignPlaces.FixedBoard.StartCorners)
            {
                double dx = t.VertexX(c) - x, dz = t.VertexZ(c) - z;
                Assert.Greater(Math.Sqrt(dx * dx + dz * dz), PlotLayout.StorehouseRadius, what + " is off the camps");
            }
        }
    }

    /// <summary>
    /// Appendix C: the campaign's tower and altar landmarks never coincide with the landmarks of PRD_AGES (harbour,
    /// hearths, dance circle now; relay and district once M1 and F1 place them, checked whenever they exist).
    /// </summary>
    public class LandmarkCollisionTests
    {
        [TestCase(PackFiles.Authored)]
        [TestCase(PackFiles.Skeleton)]
        public void CampaignLandmarks_DoNotCollideWithTheAgesLandmarks(string pack)
        {
            var sim = CampaignSamples.DevGame(21);
            Assert.IsTrue(sim.Submit(new StartCampaign { Definition = PackFiles.Load(pack) }).Ok);
            sim.Step();
            var s = sim.State;
            var t = s.Board.Topology;
            CollectionAssert.AreEqual(new[] { 17, 74, 2, 147 },
                new[] { "earth", "fire", "water", "air" }.Select(e => LandmarkApi.Get(s, "lm.tower." + e).Corner).ToArray());
            var mine = LandmarkApi.All(s).Where(l => l.Id.StartsWith("lm.tower.") || l.Id.StartsWith("lm.altar.")).ToList();
            Assert.AreEqual(8, mine.Count);
            var theirs = LandmarkApi.All(s).Where(l => !mine.Contains(l)).ToList();
            CollectionAssert.IsSubsetOf(new[] { LandmarkIds.Harbour, LandmarkIds.HomeHearth, LandmarkIds.AshHearth, LandmarkIds.DanceCircle }, theirs.Select(l => l.Id).ToArray());
            foreach (var a in mine)
                foreach (var b in theirs)
                {
                    Assert.IsFalse(a.Corner >= 0 && a.Corner == b.Corner, a.Id + " and " + b.Id + " share corner " + a.Corner);
                    Assert.IsFalse(a.Hex >= 0 && a.Hex == b.Hex, a.Id + " and " + b.Id + " share hex " + a.Hex);
                    if (a.Hex >= 0 && b.Plot >= 0) Assert.AreNotEqual(a.Hex, PlotLayout.PlotHex(t, b.Plot), a.Id + " holds " + b.Id);
                    if (a.Hex >= 0 && b.Corner >= 0 && (b.Id == LandmarkIds.Relay || b.Id == LandmarkIds.District))
                        CollectionAssert.DoesNotContain(t.HexCorners(a.Hex), b.Corner, a.Id + " and " + b.Id);
                }
            if (pack == PackFiles.Authored)
                Assert.AreEqual(t.HexIndex(new HexCoord(-2, -2)), LandmarkApi.Get(s, LandmarkIds.AltarEarth).Hex, "the Ash cut hex");
        }
    }
}
