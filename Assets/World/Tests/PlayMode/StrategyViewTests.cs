using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game.Tests
{
    /// <summary>
    /// Strategy and military in the scene: the draft through the panel's command path, cities, demons and the hero,
    /// power on the barracks, a squad attack, the end screen and a new game rebuilt from state. The game is advanced
    /// by the full_game driver (the same UI paths a player uses) with GameClock.StepOnce; every check reads the sim's
    /// state and helpers, never fixed ticks or tuned numbers.
    /// </summary>
    public class StrategyViewTests
    {
        private const string TestPause = "Test";
        private WorldBootstrap _boot;
        private FullGameDriver _driver;
        private WorldState S => _boot.Sim.State;
        private StrategyViews V => StrategyViews.Instance;

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            Assert.IsNotNull(_boot);
            Assert.IsNotNull(V, "StrategyViews installed");
            _boot.Clock.Pause(TestPause);
            _driver = new FullGameDriver(_boot.Sim, false, true);
        }

        private void Step(int n) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        /// <summary>Plays with the driver until cond holds (or the game ends); yields a frame every 40 ticks.</summary>
        private IEnumerator Drive(Func<bool> cond, int maxTicks, string what, FullGameDriver d = null)
        {
            d ??= _driver;
            int n = 0;
            while (!cond() && n < maxTicks && !S.Strategy.GameOver)
            {
                d.Act();
                _boot.Clock.StepOnce();
                if (++n % 40 == 0) yield return null;
            }
            Assert.IsTrue(cond(), what + " not reached after " + n + " ticks (" + StrategyApi.Headline(S) + ")");
        }

        [UnityTest]
        public IEnumerator Draft_PickThroughThePanel_UnblocksEndRound()
        {
            yield return Load();
            yield return Drive(() => StrategyApi.DraftPending(S), 20000, "the Age I draft");
            yield return null;
            Assert.IsTrue(StrategyHud.Instance.PausedForDraft && _boot.Clock.IsPausedFor(PauseReasons.Draft), "the draft pauses the clock");
            Step((int)EconomyApi.BellTicksLeft(S));
            var refused = GodCommands.CheckEndRound();
            Assert.AreEqual(StrategyRefusals.DraftPending, refused.Reason, "End Round waits for the pick");
            StringAssert.Contains(refused.Message, GodToolbar.Find(EconomyGodTools.EndRoundId).Tip, "the button says why");

            // The modal opens in God View with the hand; the pick goes through the panel's command path.
            _boot.Views.SetView(ViewMode.God);
            yield return null;
            Assert.IsTrue(StrategyWindow.IsOpen && StrategyWindow.Current == StrategyPanels.Kind.Draft, "draft modal open in God View");
            var hand = StrategyApi.DraftHand(S);
            Assert.AreEqual(S.Strategy.Sides[PlayerIds.Human].DraftHand.Count, hand.Count);
            Assert.IsTrue(hand.All(c => !string.IsNullOrEmpty(c.FamilyName) && !string.IsNullOrEmpty(c.CostText) && !string.IsNullOrEmpty(c.EffectText)), "family, cost and effect shown");
            int handBefore = S.Strategy.Sides[PlayerIds.Human].Hand.Count;
            var r = StrategyUi.Pick(hand[0].CardId);
            Assert.IsTrue(r.Ok, r.Message);
            Step(1);
            yield return null;
            Assert.IsFalse(StrategyApi.DraftPending(S));
            Assert.AreEqual(handBefore + 1, S.Strategy.Sides[PlayerIds.Human].Hand.Count, "the card is in the hand");
            Assert.IsFalse(_boot.Clock.IsPausedFor(PauseReasons.Draft), "the clock runs again");
            Assert.IsFalse(StrategyWindow.IsOpen, "the modal closes");
            Assert.AreEqual(1, StrategyHud.Instance.Seen.TryGetValue("DraftPicked", out int picks) ? Math.Min(1, picks) : 0, "picks seen by the HUD");
            Assert.IsTrue(GodCommands.CheckEndRound().Ok, "End Round is accepted after the pick: " + GodCommands.CheckEndRound().Message);
        }

        [UnityTest]
        public IEnumerator FoundingACity_ShowsACityView_AndChangesTheScore()
        {
            yield return Load();
            yield return Drive(() => S.Strategy.Started && !StrategyApi.DraftPending(S), 20000, "Age I");
            int cities = StrategyApi.PlayerCities(S).Count;
            int score = StrategyApi.Score(S, PlayerIds.Human).Cities;
            Assert.AreEqual(cities - 1, V.CityViewCount, "one view per city besides home");

            // The City tool highlights exactly the sim's legal corners (cost ignored, coloured by affordability).
            _boot.Views.SetView(ViewMode.God);
            var tool = (CityTool)GodToolbar.Instance.Activate(StrategyGodTools.CityId);
            tool.Refresh();
            CollectionAssert.AreEquivalent(StrategyApi.LegalCitySites(S, true), tool.LegalIgnoringCost);
            var refused = StrategyUi.FoundCity(S.Board.HomeCityVertex);
            Assert.IsFalse(refused.Ok);
            Assert.AreEqual(refused.Message, EconomyHud.LastToast, "refusal sentence shown");

            yield return Drive(() => StrategyApi.PlayerCities(S).Count > cities, 30000, "a second city");
            Step(1);
            yield return null;
            int v = StrategyApi.PlayerCities(S).Last();
            Assert.IsNotNull(V.City(v), "city view on its corner");
            Assert.Less((V.City(v).transform.position - BoardSpace.Vertex(v)).magnitude, 0.01f);
            Assert.AreEqual(score + S.Strategy.Tuning.VpPerCity, StrategyApi.Score(S, PlayerIds.Human).Cities, "the city scores");
            _boot.Views.SetView(ViewMode.God);
            yield return null;
            Assert.IsTrue(V.City(v).GetComponentsInChildren<Transform>(true).Any(t => t.name == "Ring" && t.gameObject.activeInHierarchy), "territory ring in God View");
        }

        [UnityTest]
        public IEnumerator Demon_ShowsAView_StallsADelivery_HeroMoveClearsIt()
        {
            yield return Load();
            var careless = new FullGameDriver(_boot.Sim, false, true) { IgnoreDemons = true };
            Func<Building> stalled = () => S.Economy.Buildings.FirstOrDefault(b => b.Owner == PlayerIds.Human && b.State != BuildingState.Removed
                && b.Type != BuildingType.Storehouse && StrategyApi.DemonsAt(S, b.Vertex) > 0);
            yield return Drive(() => stalled() != null, 30000, "a demon on a building's flag", careless);
            Step(1);
            yield return null;
            var b0 = stalled();
            int flag = b0.Vertex;
            Assert.IsNotNull(V.Demon(flag), "demon view on the flag");
            Assert.AreEqual(StrategyApi.DemonsAt(S, flag), V.Demon(flag).Count);
            Assert.Greater(V.Demon(flag).GetComponentsInChildren<Renderer>().Length, 3, "a visible creature");
            Assert.IsTrue(b0.Blocked || b0.Reason == Reasons.DemonOnRoute, "the building is stopped");
            Assert.IsTrue(EconomyText.Problems(S).Any(p => !p.IsJob && p.Id == b0.Id), "listed in Problems");
            Assert.AreEqual(EconomyBadge.Blocked, EconomyViews.Instance.Building(b0.Id).Badge, "red badge");

            // Walk the hero there with the Hero tool (one hex edge per move), refilling moves at round ends.
            _boot.Views.SetView(ViewMode.God);
            var hero = (HeroTool)GodToolbar.Instance.Activate(StrategyGodTools.HeroId);
            var topo = S.Board.Topology;
            int guard = 0;
            while (StrategyApi.DemonsAt(S, flag) > 0 && guard++ < 60)
            {
                if (StrategyApi.DraftPending(S)) { StrategyUi.Pick(StrategyApi.DraftHand(S)[0].CardId); Step(1); continue; }
                if (S.Strategy.HeroMovesLeft == 0)
                {
                    Step((int)EconomyApi.BellTicksLeft(S));
                    Assert.IsTrue(GodCommands.EndRound().Ok, GodCommands.LastResult.Message);
                    Step(3);
                    continue;
                }
                hero.Refresh();
                int next = NextHeroCorner(topo, S.Strategy.HeroVertex, flag);
                CollectionAssert.Contains(hero.Targets, next, "the tool offers the step");
                var r = hero.TryMove(next);
                Assert.IsTrue(r.Ok, r.Message);
                Step(1);
                yield return null;
            }
            Step(1);
            yield return null;
            Assert.AreEqual(0, StrategyApi.DemonsAt(S, flag), "the hero cleared the flag");
            Assert.IsNull(V.Demon(flag), "the demon view is gone");
            Assert.IsTrue(StrategyHud.Instance.Seen.ContainsKey("DemonsCleared"));
            Assert.IsFalse(S.Economy.Buildings[b0.Id].Blocked, "the building runs again");
        }

        /// <summary>Next corner on the way to clear a flag: a corner directly, an intermediate flag by walking its hex edge.</summary>
        private static int NextHeroCorner(BoardTopology topo, int hero, int flag)
        {
            if (topo.IsCorner(flag)) return topo.FindCornerPath(hero, flag)[1];
            int he = topo.HexEdgeOfFlag(flag);
            int a = topo.HexEdgeCornerA(he), b = topo.HexEdgeCornerB(he);
            if (hero == a) return b;
            if (hero == b) return a;
            var pa = topo.FindCornerPath(hero, a); var pb = topo.FindCornerPath(hero, b);
            return (pa.Length <= pb.Length ? pa : pb)[1];
        }

        [UnityTest]
        public IEnumerator NoPower_IsShownOnTheBarracks_AndCleared()
        {
            yield return Load();
            yield return Drive(() => StrategyUi.FirstBarracks(S, true) >= 0 && EconomyApi.FindBuilding(S, BuildingType.PowerPlant, true) >= 0
                                     && S.Military.Queues.Any(q => q.Orders.Count > 0 && q.Orders[0].Supplied), 40000, "a barracks training a soldier");
            int bid = StrategyUi.FirstBarracks(S, true);
            int plant = EconomyApi.FindBuilding(S, BuildingType.PowerPlant, true);
            Assert.IsTrue(GodCommands.Toggle(plant, false).Ok, "switch the plant off from its inspect buttons");
            int n = 0;
            while (MilitaryApi.IsPowered(S, bid) && n++ < 50) Step(1);
            yield return null;
            Assert.IsFalse(MilitaryApi.IsPowered(S, bid), "no power");
            Assert.IsNotNull(V.Power(bid), "power sign on the barracks");
            Assert.IsTrue(V.Power(bid).ShowsNoPower, "the sign shows no power");
            StringAssert.Contains(Reasons.Sentence(Reasons.NoPower, "The barracks"), MilitaryApi.DescribeBarracks(S, bid).Status);
            Assert.IsTrue(GodCommands.Toggle(plant, true).Ok);
            yield return Drive(() => MilitaryApi.IsPowered(S, bid) || !S.Military.Queues.Any(q => q.BuildingId == bid && q.Orders.Count > 0 && q.Orders[0].Supplied), 3000, "power back");
            Step(6);
            yield return null;
            Assert.IsFalse(V.Power(bid) != null && V.Power(bid).ShowsNoPower, "the no-power sign is cleared");
        }

        [UnityTest]
        public IEnumerator SquadOrderedToAttack_Walks_AndTheFightResolves()
        {
            yield return Load();
            yield return Drive(() => MilitaryApi.Squads(S, PlayerIds.Human).Any(x => x.Units >= 2 && x.Order == OrderKind.None && !x.Moving), 50000, "a squad of two");
            var sq = MilitaryApi.Squads(S, PlayerIds.Human).First(x => x.Units >= 2);
            _boot.Views.SetView(ViewMode.God);
            GodToolbar.Instance.Activate(StrategyGodTools.ArmyId);
            var r = StrategyUi.OrderSquad(sq.Id, OrderKind.Attack, S.Board.RivalOutpostVertex);
            Assert.IsTrue(r.Ok, r.Message);
            int hp0 = S.Military.Outpost.Hp;
            Step(3);
            yield return null;
            var view = V.Squad(sq.Id);
            Assert.IsNotNull(view, "squad view");
            Assert.AreEqual(sq.Units, view.FigureCount, "one figure per soldier");
            var p0 = view.transform.position;
            Step(S.Military.Tuning.TicksPerEdge);
            yield return null;
            Assert.Greater((view.transform.position - p0).magnitude, 1f, "the squad walks");
            bool engagedSeen = false;
            int guard = 0;
            while (guard++ < 30000)
            {
                var me = MilitaryLogicView(sq.Id);
                if (me == null || !me.Alive || S.Military.Outpost.Destroyed || S.Military.Outpost.Hp < hp0) break;
                if (V.Squad(sq.Id) != null && V.Squad(sq.Id).Engaged) engagedSeen = true;
                if (StrategyApi.DraftPending(S)) StrategyUi.Pick(StrategyApi.DraftHand(S)[0].CardId);
                Step(1);
                if (guard % 40 == 0) yield return null;
            }
            Assert.IsTrue(engagedSeen || StrategyHud.Instance.Seen.ContainsKey("SquadEngaged"), "the fight was shown");
            Assert.IsTrue(StrategyHud.Instance.Seen.ContainsKey("UnitHit") || StrategyHud.Instance.Seen.ContainsKey("OutpostDamaged"), "blows were struck");
            Assert.IsTrue(S.Military.Outpost.Hp < hp0 || MilitaryLogicView(sq.Id) == null || !MilitaryLogicView(sq.Id).Alive, "the attack resolved");
        }

        private Squad MilitaryLogicView(int id) => S.Military.Squads.FirstOrDefault(q => q.Id == id);

        [UnityTest]
        public IEnumerator GameOver_ShowsTheEndScreen_NewGameRebuildsFromState()
        {
            yield return Load();
            yield return Drive(() => S.Strategy.GameOver, 60000, "an ending");
            yield return null;
            Assert.AreNotEqual(GameOutcome.None, S.Strategy.Outcome);
            Assert.IsTrue(StrategyHud.Instance.EndScreenOpen, "end screen");
            StringAssert.Contains(StrategyApi.Headline(S), StrategyHud.Instance.WhatNow);
            Assert.IsTrue(StrategyHud.Instance.Seen.ContainsKey("GameEnded"));

            // New game with a fresh seed: the scene reloads and every view comes from the new state.
            long seed = _boot.Seed + 17;
            StrategyHud.Instance.CloseEndScreen();
            _boot.NewGame(seed);
            yield return null;
            yield return null;
            yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
            Assert.AreEqual(seed, _boot.Seed, "fresh seed");
            Assert.IsFalse(S.Strategy.GameOver);
            Assert.IsFalse(StrategyHud.Instance.EndScreenOpen);
            _driver = new FullGameDriver(_boot.Sim, false, true);
            yield return Drive(() => S.Strategy.Started && !StrategyApi.DraftPending(S) && MilitaryApi.Squads(S).Count > 0, 20000, "Age I in the new game");
            int cities = V.CityViewCount, demons = V.DemonViewCount, squads = V.SquadViewCount;
            int buildings = EconomyViews.Instance.BuildingViewCount, roads = EconomyViews.Instance.RoadViewCount;
            V.RebuildFromState();
            EconomyViews.Instance.RebuildFromState();
            yield return null;
            Assert.AreEqual(cities, V.CityViewCount); Assert.AreEqual(demons, V.DemonViewCount); Assert.AreEqual(squads, V.SquadViewCount);
            Assert.AreEqual(buildings, EconomyViews.Instance.BuildingViewCount); Assert.AreEqual(roads, EconomyViews.Instance.RoadViewCount);
            Assert.AreEqual(StrategyApi.Demons(S).Count, V.DemonViewCount, "demon views match state");
            Assert.AreEqual(MilitaryApi.Squads(S).Count, V.SquadViewCount, "squad views match state");
            Assert.IsNotNull(V.Hero, "hero shown");
        }
    }
}
