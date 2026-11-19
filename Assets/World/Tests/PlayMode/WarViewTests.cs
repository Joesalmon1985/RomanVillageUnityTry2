using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Plague;
using World.Sim.War;

namespace World.Game.Tests
{
    /// <summary>G3-3: soldiers, duels, huts, lodges, burning and ruins in a GodBoard game (WA-1..WA-18 views).</summary>
    public class WarViewTests
    {
        private const string TestPause = "WarViewTest";
        private WorldBootstrap _boot;
        private WarViews _v;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; }

        private IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsTrue(DevStart.Begin(GodBoardStart.Id, 7UL), DevStart.LastError);
            float t0 = Time.realtimeSinceStartup;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == GodBoardStart.Id))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 60f, "dev.godboard did not start");
                yield return null;
            }
            yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
            _v = WarViews.Instance;
            Assert.IsNotNull(_v, "war views installed");
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private Soldier Make(int people, int node, int[] lv, int hp = -1, int atk = 14)
        {
            var w = S.War;
            var x = new Soldier { Id = w.NextSoldierId++, People = people, BaseAttack = atk, Node = node };
            for (int i = 0; i < 4; i++) x.Levels[i] = lv[i];
            x.Hp = hp >= 0 ? hp : WarApi.MaxHealth(x);
            w.Soldiers.Add(x);
            return x;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Soldiers_AppearAtGarrisons_WithLodgesAndHuts()
        {
            yield return Start();
            Assert.GreaterOrEqual(S.War.Soldiers.Count, 4, "each home camp has a garrison soldier");
            yield return null; yield return null;
            Assert.AreEqual(S.War.Soldiers.Count, _v.SoldierCount, "one view per soldier");
            foreach (var x in S.War.Soldiers)
            {
                var v = _v.ViewOf(x.Id);
                Assert.IsNotNull(v, "view of soldier " + x.Id);
                Assert.AreEqual(x.People, v.People);
                var corner = BoardSpace.Vertex(S.Board.Topology, x.Corner);
                Assert.Less(BoardSpace.Flat(v.Target - corner), 12f, "stands at its corner");
            }
            for (int p = 0; p < 4; p++)
            {
                var site = _v.SiteOf(S.Board.StartCorners[p]);
                Assert.IsNotNull(site, "site of home camp " + p);
                Assert.IsNotNull(site.Lodge, "war lodge at home camp " + p);
                Assert.AreEqual(3, site.HutCount, "three processing huts at home camp " + p);
            }
            // The five looks follow the highest stat (WA-3) and a tied soldier is a Warrior.
            var looks = new[] { new[] { 3, 0, 0, 0 }, new[] { 0, 3, 0, 0 }, new[] { 0, 0, 3, 0 }, new[] { 0, 0, 0, 3 }, new[] { 1, 1, 0, 0 } };
            var want = new[] { SoldierLook.Brute, SoldierLook.Raider, SoldierLook.Shieldbearer, SoldierLook.Skirmisher, SoldierLook.Warrior };
            for (int i = 0; i < 5; i++) Make(i % 4, 5, looks[i]);
            yield return null; yield return null;
            int k = 0;
            foreach (var x in S.War.Soldiers) if (x.Node == 5) { Assert.AreEqual(want[k], _v.ViewOf(x.Id).Look); k++; }
            Assert.AreEqual(5, k);
            // Walking: a soldier mid-step shows between two nodes and walks.
            var w = Make(0, 5, new[] { 0, 0, 0, 0 });
            int n2 = PlagueApi.Neighbours(5)[0];
            yield return null; yield return null;
            w.Node = 0; w.Moving = true; w.MoveLeft = WarData.MoveTicks / 2; w.Path.Add(n2);
            _v.Pinned.Clear();
            yield return null; yield return null; yield return null;
            var a = WarViews.NodePos(5); var b = WarViews.NodePos(n2);
            var wv = _v.ViewOf(w.Id);
            Assert.IsTrue(wv.SimMoving);
            Assert.AreEqual(SoldierAction.Walk, wv.Action);
            float along = Vector3.Dot(wv.Target - a, (b - a).normalized) / (b - a).magnitude;
            Assert.AreEqual(0.5f, along, 0.05f, "half way along the trail");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Duel_Plays_AndTheLoserIsRemoved()
        {
            yield return Start();
            S.War.Soldiers.Clear(); S.War.Duels.Clear();
            int n = S.War.PeopleCount;
            S.War.AtWar = new bool[n * n]; S.War.AtWar[1] = S.War.AtWar[n] = true;
            var a = Make(0, 5, new[] { 1, 2, 1, 0 }, -1, 16);
            var b = Make(1, 5, new[] { 0, 0, 1, 0 }, 45, 12);
            yield return null; yield return null;
            Assert.AreEqual(2, _v.SoldierCount);
            bool sawHurt = false;
            for (int i = 0; i < 80 && S.War.Soldiers.Count == 2; i++)
            {
                Step(10);
                yield return null;
                var bv = _v.ViewOf(b.Id);
                if (bv != null) sawHurt |= bv.Wear != Wear.Fresh;
            }
            Assert.AreEqual(1, S.War.Soldiers.Count, "one soldier is left");
            Assert.GreaterOrEqual(_v.DuelsStarted, 1);
            Assert.GreaterOrEqual(_v.SwingsPlayed, 2);
            Assert.AreEqual(1, _v.DeathsPlayed);
            Assert.IsTrue(sawHurt, "the weaker soldier showed wear before falling");
            Assert.IsNull(_v.ViewOf(b.Id), "the loser's view left the live set");
            Assert.IsNotNull(_v.ViewOf(a.Id), "the winner stays");
            Assert.GreaterOrEqual(_v.DyingCount, 1, "the loser is falling");
            float t0 = Time.realtimeSinceStartup;
            while (_v.DyingCount > 0) { Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the fallen soldier faded away"); yield return null; }
            Assert.AreEqual(1, _v.SoldierCount);
            Assert.GreaterOrEqual(_v.ViewOf(a.Id).SwingsPlayed, 1);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BurnedSettlement_ShowsARuin_UntilTheCornerReopens()
        {
            yield return Start();
            var topo = S.Board.Topology;
            int corner = -1;
            for (int c = 0; c < topo.CornerCount && corner < 0; c++)
            {
                if (!PlotLayout.IsSettlementCorner(topo, c) || WarApi.SettlementAt(S, c) != null || S.Table.CornerOwner[c] >= 0) continue;
                if (BoardSpace.Flat(BoardSpace.Vertex(topo, c) - BoardSpace.Vertex(topo, S.Board.StartCorners[0])) < 120f) continue;
                WarRules.AddSettlement(S, 1, c, false, false);
                if (WarApi.SettlementAt(S, c).Huts.Count > 0) corner = c; else WarRules.RemoveSettlement(S, c);
            }
            Assert.GreaterOrEqual(corner, 0);
            S.Table.CornerOwner[corner] = 1; S.Table.CornerLevel[corner] = 1;
            Step(2);
            yield return null; yield return null;
            var site = _v.SiteOf(corner);
            Assert.IsNotNull(site, "a settlement gets a site");
            Assert.AreEqual(SiteState.Standing, site.State);
            Assert.IsFalse(site.HasRuinModel);
            Assert.Greater(site.HutCount, 0, "its huts stand beside it");
            // What the war module does on a burning: the settlement goes and a ruin takes its place.
            WarRules.RemoveSettlement(S, corner);
            S.War.Ruins.Add(new Ruin { Corner = corner, People = 1, ExpireClock = S.War.Clock + WarData.RuinTicks });
            yield return null; yield return null;
            Assert.AreEqual(SiteState.Burning, site.State);
            Assert.IsTrue(site.HasFire, "it burns");
            Assert.AreEqual(0, site.HutCount, "the huts are gone");
            float t0 = Time.realtimeSinceStartup;
            while (!site.HasRuinModel) { Assert.Less(Time.realtimeSinceStartup - t0, 12f, "the ruin model appeared"); yield return null; }
            Assert.IsTrue(site.Smoke.Intensity > 0.3f, "smoke over the ruin");
            // The ruin is cleared and the corner reopens: the site goes.
            S.War.Ruins.Clear();
            yield return null; yield return null;
            Assert.IsNull(_v.SiteOf(corner), "no site at a reopened corner");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SoldiersShowAsMarkers_OnTheGodBoard()
        {
            yield return Start();
            yield return null;
            foreach (var v in _v.Soldiers) Assert.IsFalse(v.MarkerShown, "no markers on foot");
            var d = GodBoardDirector.Instance;
            Assert.IsTrue(d.TrySacrifice(true));
            Step(2);
            float t0 = Time.realtimeSinceStartup;
            while (d.View != BoardView.Board) { Assert.Less(Time.realtimeSinceStartup - t0, 10f); yield return null; }
            yield return null; yield return null;
            Assert.IsTrue(_v.Elevated);
            Assert.Greater(_v.SoldierCount, 0);
            foreach (var v in _v.Soldiers) Assert.IsTrue(v.MarkerShown, "marker on the board");
        }
    }
}
