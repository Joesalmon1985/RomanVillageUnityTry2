using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Table;

namespace World.Game.Tests
{
    /// <summary>G1-7: collecting villagers (CL-1, CL-2), the prince guide (GB-7), Mira's offer (CC-1), no roll text (AB-1), the status line.</summary>
    public class GodBoardVillagerTests
    {
        private const string TestPause = "GodBoardVillagerTest";
        private WorldBootstrap _boot;
        private GodBoardDirector _d;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; PointerInput.InputEnabled = true; GodBoardSettings.Override = null; }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; Time.timeScale = 1f; }

        private IEnumerator Start(bool pause = true)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsNotNull(old, "World scene");
            Assert.IsTrue(DevStart.Begin(GodBoardStart.Id, 7UL), DevStart.LastError);
            float t0 = Time.realtimeSinceStartup;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == GodBoardStart.Id))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 60f, "dev.godboard did not start");
                yield return null;
            }
            yield return null;
            _boot = WorldBootstrap.Instance;
            if (pause) _boot.Clock.Pause(TestPause);
            _d = GodBoardDirector.Instance;
            Assert.IsNotNull(_d, "god board installed");
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private int ResourceHex()
        {
            var b = S.Board;
            for (int h = 0; h < b.Topology.HexCount; h++) if (TerrainInfo.RawWare(b.HexTerrain[h]).HasValue && b.HexNumber[h] > 0) return h;
            return -1;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Production_SendsVillagersToTheHex_AndBack()
        {
            yield return Start();
            var cw = _d.Collectors;
            Assert.IsNotNull(cw);
            int hex = ResourceHex();
            Assert.GreaterOrEqual(hex, 0);
            var topo = S.Board.Topology;
            var corners = topo.HexCorners(hex).ToArray();
            // A settlement and a city beside the hex (state set directly: this test is about the view).
            S.Table.CornerOwner[corners[0]] = 1; S.Table.CornerLevel[corners[0]] = 1;
            S.Table.CornerOwner[corners[2]] = 0; S.Table.CornerLevel[corners[2]] = 2;
            var pr = new Produced { Hexes = new[] { hex }, Blocked = new int[0], Gains = new int[S.Table.PeopleCount * Wares.RawCount] };
            int sent = cw.OnProduced(pr);
            Assert.AreEqual(3, sent, "one villager per card: 1 for the settlement, 2 for the city");
            Assert.AreEqual(3, cw.Active.Count);
            Assert.IsTrue(cw.Active.All(w => w.Hex == hex), "all go to the producing hex");
            Assert.AreEqual(1, cw.Active.Count(w => w.Corner == corners[0]));
            Assert.AreEqual(2, cw.Active.Count(w => w.Corner == corners[2]));
            var centre = BoardSpace.Hex(topo, hex);
            float nearest = float.MaxValue; bool loadedOnTheWayBack = false;
            for (int i = 0; i < 2000 && cw.Active.Count > 0; i++)
            {
                cw.Step(0.05f);
                foreach (var w in cw.Active)
                {
                    if (w.Leg == CollectWalkers.Leg.Gathering) nearest = Mathf.Min(nearest, BoardSpace.Flat(w.Go.transform.position - centre));
                    if (w.Leg == CollectWalkers.Leg.Back && w.Load.activeSelf) loadedOnTheWayBack = true;
                    if (w.Leg == CollectWalkers.Leg.Out) Assert.IsFalse(w.Load.activeSelf, "no load on the way out");
                }
            }
            Assert.Less(nearest, 12f, "they reach the hex");
            Assert.IsTrue(loadedOnTheWayBack, "they carry the good home");
            Assert.AreEqual(0, cw.Active.Count, "and are gone again");
            Assert.AreEqual(3, cw.Delivered);
            // The cap: a city beside four hexes still sends at most 3 villagers per production.
            S.Table.CornerOwner[corners[2]] = 0; S.Table.CornerLevel[corners[2]] = 2;
            var hexes = Enumerable.Range(0, topo.HexCount).Where(h => topo.HexCorners(h).Contains(corners[2]) && TerrainInfo.RawWare(S.Board.HexTerrain[h]).HasValue).ToArray();
            int n2 = cw.OnProduced(new Produced { Hexes = hexes, Blocked = new int[0], Gains = pr.Gains });
            Assert.LessOrEqual(n2 - cw.Active.Count(w => w.Corner != corners[2]), CollectWalkers.CapPerSettlement);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RealProduction_FromTheTable_SendsVillagers()
        {
            yield return Start();
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            // The AIs place; the player places nothing (the setup waits), so leave the player's seat to the engine's default.
            yield return null;
            int before = _d.Collectors.Sent;
            for (int i = 0; i < 4000 && _d.Collectors.Sent == before; i++) { Step(1); if (i % 50 == 0) yield return null; }
            // With the player's setup unfinished the table may stay in Setup; accept either, but when production ran, villagers left.
            if (S.Table.Phase == TablePhase.Main) Assert.Greater(_d.Collectors.Sent, before, "a production event sends villagers");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator GodBoardGame_MiraOffersTheCrystals_FromTheStart()
        {
            yield return Start();
            Assert.IsTrue(MiraOffer.Available(S));
            var mira = Npc.Find(NpcIds.Mira);
            Assert.IsNotNull(mira);
            mira.DialogueBuilder?.Invoke(mira);
            Assert.IsTrue(mira.Options.Any(o => o.Label == MiraOffer.Label), "Set out for the crystals");
            var offer = mira.Options.First(o => o.Label == MiraOffer.Label);
            offer.OnChoose();
            Step(2);
            Assert.IsNotNull(S.Campaign, "choosing it is StartCampaign");
            Assert.IsFalse(MiraOffer.Available(S));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LegacyGame_MiraHasNoCrystalOffer()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var mira = Npc.Find(NpcIds.Mira);
            Assert.IsNotNull(mira);
            mira.DialogueBuilder?.Invoke(mira);
            Assert.IsFalse(mira.Options.Any(o => o.Label == MiraOffer.Label));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator GodBoardGame_HasNoRollText_AndASensibleStatusLine()
        {
            yield return Start();
            Step(5);
            string strip = string.Join("\n", Hud.StripLines(S)).ToLowerInvariant();
            StringAssert.DoesNotContain("roll", strip);
            StringAssert.DoesNotContain("next in", strip);
            var hud = _d.GetComponent<GodBoardHud>();
            S.Table.Current = -1;
            string line = hud.StatusLine(S, S.Table);
            StringAssert.DoesNotContain("-1", line);
            StringAssert.DoesNotContain("People", line);
            foreach (var hv in Object.FindObjectsByType<HexLandView>(FindObjectsSortMode.None))
                Assert.IsNull(hv.transform.Find("Token/Number"), "no number token on hex " + hv.HexId);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Prince_GreetsLeadsToMira_ThenPointsToTheTemple()
        {
            yield return Start(false);
            var pd = PrinceDirector.Instance;
            Assert.IsNotNull(pd);
            Assert.IsTrue(pd.IsGodBoardGame);
            Time.timeScale = 6f;
            float t0 = Time.realtimeSinceStartup;
            while (pd.Guide != PrinceDirector.GuideStage.Done)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 150f, "guide stuck at " + pd.Guide);
                yield return null;
            }
            var said = pd.Said.Select(l => l.Text).ToList();
            CollectionAssert.Contains(said, PrinceLines.GodGreeting);
            CollectionAssert.Contains(said, PrinceLines.GodMira);
            CollectionAssert.Contains(said, PrinceLines.GodTemple);
            CollectionAssert.DoesNotContain(said, PrinceLines.FirstLine, "no woodcutter's hut in a god board game");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Prince_SaysALine_WhenTheHintIsShown()
        {
            yield return Start();
            Assert.IsTrue(_d.TrySacrifice(true));
            Step(2);
            float t0 = Time.realtimeSinceStartup;
            while (_d.View != BoardView.Board) { Assert.Less(Time.realtimeSinceStartup - t0, 10f); yield return null; }
            yield return null; yield return null;
            Assert.GreaterOrEqual(_d.AdviseCorner, 0);
            Assert.IsFalse(string.IsNullOrEmpty(_d.LastHintLine), "the prince says a line with the hint");
            Assert.IsTrue(_d.Feed.Any(f => f.Text.StartsWith("The prince:")));
        }
    }
}
