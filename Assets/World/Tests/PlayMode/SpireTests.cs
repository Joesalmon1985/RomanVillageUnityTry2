using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game.Tests
{
    /// <summary>G2-4: a SpireRaised event builds a spire and retires the home circle, casting at a spire starts the rhythm encounter, a missing pack falls back to spire 1's chart.</summary>
    public class SpireTests
    {
        private const string TestPause = "SpireTest";
        private const string OwnerPacks = @"C:\Users\joesa\Documents\Cursor\A1-before\songs\packs";
        private WorldBootstrap _boot;
        private SpireDirector _d;

        [TearDown]
        public void TearDown()
        {
            LocalRhythmPacks.RootOverride = null;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
        }

        private IEnumerator Start()
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
            _boot.Clock.Pause(TestPause);
            _d = SpireDirector.Instance;
            Assert.IsNotNull(_d, "spires installed on a god board game");
        }

        private void Raise(Element e, int people)
        {
            var corner = _boot.Sim.State.Board.StartCorners[people];
            _d.OnTicked(new List<SimEvent> { new SpireRaised { Element = e, People = people, Corner = corner, Kind = "city" } });
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SpireRaised_BuildsSpire_InElementColour_AndRetiresTheHomeCircle()
        {
            yield return Start();
            Assert.AreEqual(0, _d.Count);
            Assert.IsFalse(DanceCircle.Current.Retired, "the home circle stays until the first spire");
            Raise(Element.Fire, 1);
            Assert.AreEqual(1, _d.Count);
            var sp = _d.Stage(0);
            Assert.AreEqual(Element.Fire, sp.Element);
            Assert.AreEqual(SpireDirector.ElementColour(Element.Fire), sp.Colour);
            Assert.IsNotNull(sp.Beam, "light beam");
            Assert.IsNotNull(sp.Circle, "stage");
            Assert.IsTrue(DanceCircle.Current.Retired, "home circle retired (SP-1)");
            Assert.IsFalse(DanceCircle.Current.gameObject.activeSelf);
            Assert.AreNotEqual(SpireDirector.ElementColour(Element.Earth), SpireDirector.ElementColour(Element.Water));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CastingAtASpire_StartsTheRhythmEncounter_AtAnySpire()
        {
            yield return Start();
            Raise(Element.Earth, 1); // a rival's spire (SP-4)
            var sp = _d.Stage(0);
            // Out of range: nothing starts.
            sp.Interact();
            Assert.IsFalse(EncounterHost.Instance.IsRunning);
            _boot.Avatar.PlaceExact(sp.Layout.DancerSpot, sp.Layout.DancerYaw, 0f);
            sp.Interact();
            Assert.IsTrue(EncounterHost.Instance.IsRunning, "the rhythm encounter started");
            var r = EncounterHost.Instance.Current as RhythmPartyEncounter;
            Assert.IsNotNull(r);
            Assert.AreEqual(0, r.SpireIndex);
            Assert.AreSame(sp.Circle, r.Circle, "played on the spire's stage");
            Assert.AreEqual("lv1_warehouse", r.Chart.Id, "spire 1 plays today's chart");
            Assert.AreEqual(174.0, r.Chart.Bpm, 0.01);
            EncounterHost.Instance.Abort();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MissingPack_FallsBackToSpireOnesChart()
        {
            yield return Start();
            LocalRhythmPacks.RootOverride = Path.Combine(Path.GetTempPath(), "no_such_rhythm_packs_" + System.Guid.NewGuid().ToString("N"));
            Raise(Element.Earth, 0); Raise(Element.Fire, 1); Raise(Element.Water, 2); Raise(Element.Air, 3);
            Assert.AreEqual(4, _d.Count);
            for (int i = 1; i < 4; i++)
            {
                Assert.IsTrue(_d.Stage(i).StartDance());
                var r = (RhythmPartyEncounter)EncounterHost.Instance.Current;
                Assert.AreEqual(i, r.SpireIndex);
                Assert.IsTrue(r.PackFellBack, "spire " + (i + 1) + " falls back");
                Assert.AreEqual("lv1_warehouse", r.Chart.Id);
                Assert.AreEqual(174.0, r.Chart.Bpm, 0.01);
                EncounterHost.Instance.Abort();
                yield return null;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LocalPack_LoadsForSpireTwo_WhenTheOwnersFolderIsThere()
        {
            if (!Directory.Exists(OwnerPacks)) Assert.Ignore("the owner's packs folder is not on this machine");
            yield return Start();
            LocalRhythmPacks.RootOverride = OwnerPacks;
            Raise(Element.Earth, 0); Raise(Element.Fire, 1);
            Assert.IsTrue(_d.Stage(1).StartDance());
            var r = (RhythmPartyEncounter)EncounterHost.Instance.Current;
            Assert.IsFalse(r.PackFellBack);
            Assert.AreEqual("lv2_alien_spaceship", r.Chart.Id);
            Assert.IsFalse(r.Stems.IsSynth);
            Assert.Greater(r.Stems.Drums.length, 1f);
            EncounterHost.Instance.Abort();
        }

        [Test]
        public void ParseWav_ReadsSixteenBitStereo()
        {
            var b = new List<byte>();
            void S(string s) { foreach (var c in s) b.Add((byte)c); }
            void I(int v) { b.AddRange(System.BitConverter.GetBytes(v)); }
            void H(short v) { b.AddRange(System.BitConverter.GetBytes(v)); }
            S("RIFF"); I(36 + 8); S("WAVE"); S("fmt "); I(16); H(1); H(2); I(48000); I(48000 * 4); H(4); H(16);
            S("data"); I(8); H(16384); H(-16384); H(0); H(32767);
            Assert.IsTrue(LocalRhythmPacks.ParseWav(b.ToArray(), out int ch, out int rate, out var smp));
            Assert.AreEqual(2, ch); Assert.AreEqual(48000, rate); Assert.AreEqual(4, smp.Length);
            Assert.AreEqual(0.5f, smp[0], 1e-4f); Assert.AreEqual(-0.5f, smp[1], 1e-4f);
        }

        [Test]
        public void PackOrder_IsWarehouse_Spaceship_TemplePlusStorm()
        {
            Assert.IsNull(LocalRhythmPacks.PackIdOfSpire(0));
            Assert.AreEqual("lv2_alien_spaceship", LocalRhythmPacks.PackIdOfSpire(1));
            Assert.AreEqual("gospel_temple", LocalRhythmPacks.PackIdOfSpire(2));
            Assert.AreEqual("gospel_storm", LocalRhythmPacks.PackIdOfSpire(3));
        }
    }
}
