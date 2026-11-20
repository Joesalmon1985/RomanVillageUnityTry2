using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Encounters.Council;
using World.Sim.Table;

namespace World.Game.Tests
{
    /// <summary>WU-1a: the hub's places at night: each destination exists, is tagged and its entry starts its game or says its honest placeholder.</summary>
    public class HubTests
    {
        private const string TestPause = "HubTest";
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;

        [SetUp] public void SetUp() { GodBoardDirector.RiseSeconds = 0.05f; CouncilDirector.Disabled = true; }
        [TearDown] public void TearDown() { GodBoardDirector.RiseSeconds = 2.4f; CouncilDirector.Disabled = false; }

        private static readonly string[] HubZones = { WorldZones.HubTemple, WorldZones.HubCouncil, WorldZones.HubRhythm, WorldZones.HubHealer, WorldZones.HubCave, WorldZones.HubDuel };

        private IEnumerator Start(string id)
        {
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null;
            var old = WorldBootstrap.Instance;
            Assert.IsNotNull(old, "World scene");
            Assert.IsTrue(DevStart.Begin(id, 7UL), DevStart.LastError);
            float t0 = Time.realtimeSinceStartup;
            while (!(WorldBootstrap.Instance != null && WorldBootstrap.Instance != old && DevStart.Current == id))
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 120f, id + " did not start");
                yield return null;
            }
            yield return null;
            _boot = WorldBootstrap.Instance;
            _boot.Clock.Pause(TestPause);
            if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false; // the prince's guide would walk the player away
            PlayerWalker.Instance.StopAuto();
        }

        private void Step(int n = 1) { for (int i = 0; i < n; i++) _boot.Clock.StepOnce(); }

        private static IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond()) { Assert.Less(Time.realtimeSinceStartup - t0, seconds, what); yield return null; }
        }

        private static float Bearing(Vector3 from, float yaw, Vector3 to) { var d = to - from; d.y = 0; return Mathf.DeltaAngle(yaw, BoardSpace.YawOf(d)); }

        private static Npc Need(string id) { var n = Npc.Find(id); Assert.IsNotNull(n, "villager " + id); return n; }

        [UnityTest, Timeout(180000)]
        public IEnumerator Hub_AtNight_EveryDestinationExists_IsTagged_AndFramedFromTheSpawn()
        {
            yield return Start(GodBoardStart.Id);
            Assert.Greater(DayNight.Instance.Night, 0.5f, "the hub opens at night");
            var layout = new CampLayout(S.Board);
            Assert.IsTrue(layout.Hub);
            foreach (var z in HubZones) Assert.IsNotNull(HubInstaller.Zone(z), "zone " + z);
            int hubZones = 0; foreach (var wz in HubInstaller.Root.GetComponentsInChildren<WorldZone>()) if (wz.ZoneId.StartsWith("HUB-")) hubZones++; // the lodge's PLG-* props (WU-3) are tagged below the hub too
            Assert.AreEqual(6, hubZones);
            // The people of each place.
            Need("temple_keeper"); Need("temple_builder_0"); Need("temple_builder_1");
            Need("council_steward");
            Need("healer"); Need("healer_assistant"); Need("healer_runner");
            Need("ring_referee"); Need("ring_sparrer_0"); Need("ring_sparrer_1");
            Need(NpcIds.Mira); Need("camp_companion_0"); Need("camp_companion_1"); Need(NpcIds.Host);
            Assert.IsNotNull(HealersLodge.Instance); Assert.IsNotNull(DuelRing.Instance); Assert.IsNotNull(ExpeditionCamp.Instance); Assert.IsNotNull(TempleForecourt.Instance);
            Assert.IsNotNull(GodBoardDirector.Instance.OwnTemple.GetComponentInChildren<TempleAltar>(), "the altar's own path stays");

            // The first view (half of the 70 degree vertical view on a 2:1 phone is about 56 degrees each way): temple, tower, circle, Mira.
            var spawn = WalkLattice.ToWorld(layout.Spawn); float yaw = WalkLattice.Yaw(layout.SpawnHeading);
            Assert.AreEqual(layout.SpawnHeading, PlayerWalker.Instance.Heading, "the walker starts on the layout's heading");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, HubInstaller.Zone(WorldZones.HubTemple).transform.position)), 56f, "temple in the first view");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, CouncilDirector.Instance.Site.Position)), 56f, "tower in the first view");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, layout.DanceCircleCentre)), 56f, "circle in the first view");
            Assert.Less(Mathf.Abs(Bearing(spawn, yaw, layout.MiraSpot)), 56f, "Mira in the first view");
            // The ring and the lodge are one turn away (60 to 150 degrees off the spawn heading).
            foreach (var z in new[] { WorldZones.HubHealer, WorldZones.HubDuel })
            {
                float b = Mathf.Abs(Bearing(spawn, yaw, HubInstaller.Zone(z).transform.position));
                Assert.That(b, Is.InRange(56f, 150f), z + " is one turn away");
            }
            // The lodge keeps away from the council's herbs place.
            Assert.Greater(BoardSpace.Flat(HubInstaller.Zone(WorldZones.HubHealer).transform.position - CouncilDirector.Instance.Site.Loc[4]), 20f);
            // The legacy look is unchanged: no hub on a board that was not dealt.
            Assert.IsNotNull(GameObject.Find("Hub ground"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Temple_KeeperWalksToTheAltar_AndTheOfferingOpensTheTable()
        {
            yield return Start(GodBoardStart.Id);
            var f = TempleForecourt.Instance;
            var keeper = f.Keeper;
            keeper.DialogueBuilder(keeper);
            Assert.AreEqual("Make the offering", keeper.Options[0].Label);
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase);
            // The player walks up: the keeper greets and walks to the altar.
            PlayerWalker.Instance.Teleport(f.Frame.W(0, 10f), f.Frame.Yaw + 180f, 6f);
            yield return Until(() => f.Greeted && BoardSpace.Flat(keeper.transform.position - f.AltarApproach) < 1.6f, 20f, "keeper greets and walks to the altar");
            // The offering: the keeper takes the same path as the altar (Sacrifice).
            keeper.Options[0].OnChoose();
            yield return Until(() => !f.Offering, 20f, "the offering finishes");
            Step(2);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase, "Sacrifice opened the table");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Temple_DoorwayTap_IsTheSamePathAsTheAltar()
        {
            yield return Start(GodBoardStart.Id);
            var f = TempleForecourt.Instance;
            var door = f.GetComponentInChildren<TempleDoorway>();
            Assert.IsNotNull(door, "the doorway is a hit target");
            PlayerWalker.Instance.Teleport(f.Frame.W(0, 12f), f.Frame.Yaw + 180f, 6f);
            door.Interact();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(TablePhase.NotOpened, S.Table.Phase, "the first tap only previews");
            door.Interact();
            Step(2);
            Assert.AreEqual(TablePhase.Setup, S.Table.Phase);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Council_StewardSaysNoDisputeYet_AndOpensWhenOneIsCalled()
        {
            yield return Start(GodBoardStart.Id);
            var st = CouncilSteward.Instance; Assert.IsNotNull(st);
            var dir = CouncilDirector.Instance;
            var tower = dir.Site.Position;
            Assert.Less(BoardSpace.Flat(st.Steward.transform.position - dir.StewardSpot), 0.5f, "the steward stands at the tower foot");
            Assert.Less(BoardSpace.Flat(st.Steward.transform.position - tower), 8f);
            if (!CouncilSteward.CanHold(S, dir.HomeSettlement))
            {
                st.Steward.DialogueBuilder(st.Steward);
                Assert.AreEqual(CouncilSteward.NotYet, st.Steward.Lines[0], "the honest placeholder");
                Assert.AreEqual("No dispute is called yet.", dir.StewardOpen(st.Steward));
            }
            // A council in progress with the player helping: the steward opens it through the existing accept path.
            CouncilFixture.StageExample(S, dir.HomeSettlement, 0);
            Step(2);
            Assert.IsTrue(CouncilSteward.CanHold(S, dir.HomeSettlement), "a council is called");
            st.Steward.DialogueBuilder(st.Steward);
            Assert.AreEqual("Hear the dispute", st.Steward.Options[0].Label);
            Assert.IsNull(dir.StewardOpen(st.Steward));
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "the council encounter started");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Rhythm_DrumStartsTheDance_ThenSolLeadsToTheSpires()
        {
            yield return Start(GodBoardStart.Id);
            var circle = DanceCircle.Current;
            var starter = circle.GetComponentsInChildren<DanceStarter>(true);
            Assert.GreaterOrEqual(starter.Length, 5, "the fire ring and four drums start the dance");
            var sol = Need(NpcIds.Host);
            sol.DialogueBuilder(sol);
            Assert.AreEqual("Dance at the fire", sol.Options[0].Label);
            starter[1].Interact();
            Assert.IsNotNull(RhythmPartyEncounter.Current, "a drum started the dance");
            EncounterHost.Instance.Abort();
            yield return null;

            // After spire 1 the home circle is cold and Sol offers to lead.
            var sd = SpireDirector.Instance;
            sd.Raise(Element.Earth, 0, S.Board.StartCorners[0], false);
            Assert.IsTrue(circle.Retired);
            sol.DialogueBuilder(sol);
            Assert.AreEqual("Lead me to the spires", sol.Options[0].Label);
            Assert.IsTrue(SolGuide.Instance.LeadToSpire());
            Assert.IsNotNull(PlayerWalker.Instance.Following, "the player follows Sol");
            Assert.Less(BoardSpace.Flat(sol.transform.position - HubInstaller.Zone(WorldZones.HubRhythm).transform.position), 20f, "Sol stays at the cold circle until he leads");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Healer_AndRunner_SayTheTruth_AndTheLodgeIsOrdinaryInHistoric()
        {
            yield return Start(GodBoardStart.Id);
            var lodge = HealersLodge.Instance;
            Assert.IsFalse(string.IsNullOrEmpty(lodge.HealerSays()));
            Assert.IsFalse(lodge.HealerSays().Contains("gone"));
            lodge.Healer.DialogueBuilder(lodge.Healer);
            Assert.AreEqual(lodge.HealerSays(), lodge.Healer.Lines[0]);
            lodge.SendRunner("Sickness has broken out on the island!");
            yield return Until(() => BoardSpace.Flat(lodge.Runner.transform.position - lodge.Door) < 3f, 20f, "the runner reaches the lodge");
            Assert.AreEqual(1, lodge.RunnerTrips);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Historic_ShowsTheTempleRuin_AndTheLodgeWithoutThePlague()
        {
            yield return Start(GodBoardStart.HistoricId);
            Assert.IsNotNull(S.Historic);
            Assert.IsNull(GodBoardDirector.Instance.OwnTemple, "no temple stands in Historic");
            var f = TempleForecourt.Instance;
            yield return Until(() => f.RuinShown, 5f, "the ruin shows");
            Assert.IsNotNull(f.Ruin.GetComponent<CampPropInfo>(), "the ruin is tappable");
            f.Keeper.DialogueBuilder(f.Keeper);
            StringAssert.Contains("age has turned", f.Keeper.Lines[0]);
            Assert.AreEqual(1, f.Keeper.Options.Count, "no offering in Historic");
            var lodge = HealersLodge.Instance;
            Assert.IsFalse(lodge.PlagueRuns);
            StringAssert.Contains("sickness is gone", lodge.HealerSays());
            Assert.IsNotNull(HubInstaller.Zone(WorldZones.HubHealer));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Cave_MiraCampHasCompanions_AndHerOfferStands()
        {
            yield return Start(GodBoardStart.Id);
            var mira = Need(NpcIds.Mira);
            mira.DialogueBuilder(mira);
            Assert.IsTrue(mira.Options.Any(o => o.Label == MiraOffer.Label), "her conversation is the entry");
            Assert.IsTrue(mira.Options.Any(o => o.Label == "Practice duel"));
            Assert.IsNotNull(ExpeditionCamp.Instance.GetComponentInChildren<MapTap>(), "the map opens her conversation");
            foreach (var c in ExpeditionCamp.Instance.Companions) Assert.Less(BoardSpace.Flat(c.transform.position - mira.transform.position), 6f, "companions wait by her");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Duel_RefereeOffersPracticeDuel_MiraWalksOver_AndTheEncounterStarts()
        {
            yield return Start(GodBoardStart.Id);
            var ring = DuelRing.Instance;
            Assert.IsNotNull(ring.GetComponentInChildren<RingTap>());
            ring.Referee.DialogueBuilder(ring.Referee);
            Assert.AreEqual("Practice duel", ring.Referee.Options[0].Label);
            var mira = Need(NpcIds.Mira);
            var home = mira.transform.position;
            Assert.IsTrue(ring.StartDuel());
            yield return Until(() => ring.State == DuelRing.Errand.Dueling, 40f, "Mira walks over");
            Assert.IsNotNull(WardDuelEncounter.Current, "the existing Ward duel started");
            Assert.Less(BoardSpace.Flat(mira.HomePosition - ring.MiraStand), 1.6f, "Mira stands at the ring's gate");
            EncounterHost.Instance.Abort();
            yield return Until(() => ring.State == DuelRing.Errand.None, 60f, "Mira walks back");
            Assert.Less(BoardSpace.Flat(mira.transform.position - home), 2f);
        }

        /// <summary>WU-7: the duel's gate leaves by preview then confirm and the player is back at the ring where he stood; Mira walks home.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Duel_GateLeavesByTwoTaps_AndTheHubViewIsBackAtTheRing()
        {
            yield return Start(GodBoardStart.Id);
            GodBoardSettings.Override = false;
            try
            {
                var ring = DuelRing.Instance;
                var mira = Need(NpcIds.Mira);
                var home = mira.transform.position;
                var av = _boot.Avatar;
                Vector3 pos = av.Position; float yaw = av.Yaw, pitch = av.Pitch;
                Assert.IsTrue(ring.StartDuel());
                yield return Until(() => ring.State == DuelRing.Errand.Dueling, 40f, "Mira walks over");
                var e = WardDuelEncounter.Current;
                Assert.IsNotNull(e);
                yield return null; yield return null;
                Assert.IsTrue(e.Props.RigVisible, "the stones stand at the ring");
                Assert.IsNotNull(Object.FindFirstObjectByType<DuelWorldProps>());
                yield return DuelWorldTaps.Tap(e, DuelTarget.Gate);
                Assert.IsTrue(EncounterHost.Instance.IsRunning, "one tap on the gate only previews");
                yield return new WaitForSecondsRealtime(0.32f);
                yield return DuelWorldTaps.Tap(e, DuelTarget.Gate);
                yield return Until(() => !EncounterHost.Instance.IsRunning, 3f, "the gate leaves");
                yield return Until(() => ring.State == DuelRing.Errand.None, 60f, "Mira walks back to the camp");
                Assert.Less(BoardSpace.Flat(mira.transform.position - home), 2f, "Mira is home");
                Assert.Less(Vector3.Distance(av.Position, pos), 0.05f, "the player stands where he stood at the ring");
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(av.Yaw, yaw)), 0.5f);
                Assert.Less(Mathf.Abs(av.Pitch - pitch), 0.5f);
                yield return null;
                Assert.IsNull(Object.FindFirstObjectByType<DuelWorldProps>(), "the stones are gone");
                Assert.IsNotNull(DuelRing.Instance, "the ring stands");
            }
            finally { GodBoardSettings.Override = null; }
        }
    }
}
