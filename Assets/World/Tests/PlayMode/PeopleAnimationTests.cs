using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using World.Sim.War;

namespace World.Game.Tests
{
    /// <summary>
    /// P1 polish (people): the placeholder body's procedural walk cycle (limbs move while walking, stop when standing, the
    /// stride follows the ground covered), different idle phases between people, and the jointed mesh figures of the
    /// board's crowds. Bare scene; the roots are moved by hand.
    /// </summary>
    public class PeopleAnimationTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp] public void SetUp() { Time.captureFramerate = 60; PlaceholderActor.OffscreenLod = false; }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0; PlaceholderActor.OffscreenLod = true;
            foreach (var g in _made) if (g != null) Object.Destroy(g);
            _made.Clear();
        }

        private IEnumerator Bare()
        {
            var s = SceneManager.CreateScene("PeopleAnimBare" + Random.Range(0, 1 << 30));
            SceneManager.SetActiveScene(s);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var o = SceneManager.GetSceneAt(i);
                if (o != s && o.isLoaded && o.name == "World") yield return SceneManager.UnloadSceneAsync(o);
            }
            yield return null;
        }

        private PlaceholderActor Make(string id, Vector3 at)
        {
            var a = PlaceholderActor.Build(id, new Color(0.5f, 0.4f, 0.3f), null);
            a.transform.position = at;
            _made.Add(a.gameObject);
            return a;
        }

        [UnityTest]
        public IEnumerator WalkCycle_MovesLimbsWhileWalking_AndStopsWhenStanding()
        {
            yield return Bare();
            var a = Make("p1_walker", Vector3.zero);
            yield return null; yield return null;
            // Walk along +Z at 1.4 m/s for 2 s: the legs must open and close (several steps), the arms swing.
            float maxSwing = 0f, minSwing = 999f; int crossings = 0; float last = 0f;
            Transform legL = a.transform.Find("Body/LegL");
            Assert.IsNotNull(legL, "the placeholder has a left leg pivot");
            for (int f = 0; f < 120; f++)
            {
                a.transform.position += Vector3.forward * 1.4f / 60f;
                yield return null;
                if (f < 20) continue;
                maxSwing = Mathf.Max(maxSwing, a.LegSwing); minSwing = Mathf.Min(minSwing, a.LegSwing);
                float x = Mathf.DeltaAngle(0f, legL.localEulerAngles.x);
                if (f > 20 && Mathf.Sign(x) != Mathf.Sign(last)) crossings++;
                last = x;
            }
            Assert.Greater(maxSwing, 25f, "the legs open while walking");
            Assert.Less(minSwing, 12f, "and close again (a cycle, not a held stride)");
            Assert.GreaterOrEqual(crossings, 3, "the left leg swings back and forth several times in 1.7 s");
            // Stand still: within a second the legs come together and stay.
            for (int f = 0; f < 60; f++) yield return null;
            float still = 0f;
            for (int f = 0; f < 30; f++) { yield return null; still = Mathf.Max(still, a.LegSwing); }
            Assert.Less(still, 1.5f, "the legs are together when standing");
        }

        [UnityTest]
        public IEnumerator WalkCycle_StepsFollowGroundCovered()
        {
            yield return Bare();
            // Two walkers at the same speed for the same time take the same number of steps; one twice as fast covers
            // twice the ground with longer and more frequent steps (the cadence rises with speed).
            var slow = Make("p1_slow", new Vector3(-3, 0, 0));
            var fast = Make("p1_fast", new Vector3(3, 0, 0));
            yield return null; yield return null;
            Transform ls = slow.transform.Find("Body/LegL"), lf = fast.transform.Find("Body/LegL");
            int cs = 0, cf = 0; float pls = 0f, plf = 0f;
            for (int f = 0; f < 180; f++)
            {
                slow.transform.position += Vector3.forward * 1.2f / 60f;
                fast.transform.position += Vector3.forward * 2.4f / 60f;
                yield return null;
                if (f < 20) continue;
                float xs = Mathf.DeltaAngle(0f, ls.localEulerAngles.x), xf = Mathf.DeltaAngle(0f, lf.localEulerAngles.x);
                if (f > 20 && Mathf.Sign(xs) != Mathf.Sign(pls)) cs++;
                if (f > 20 && Mathf.Sign(xf) != Mathf.Sign(plf)) cf++;
                pls = xs; plf = xf;
            }
            Assert.Greater(cf, cs, "the faster walker steps more often");
            Assert.Less(cf, cs * 2, "but less than twice as often: its steps are longer");
        }

        [UnityTest]
        public IEnumerator Running_LeansForwardAndSwingsWider()
        {
            yield return Bare();
            var a = Make("p1_runner", Vector3.zero);
            yield return null; yield return null;
            float walkMax = 0f, runMax = 0f;
            for (int f = 0; f < 90; f++) { a.transform.position += Vector3.forward * 1.4f / 60f; yield return null; if (f > 30) walkMax = Mathf.Max(walkMax, a.LegSwing); }
            for (int f = 0; f < 90; f++) { a.transform.position += Vector3.forward * 7f / 60f; yield return null; if (f > 30) runMax = Mathf.Max(runMax, a.LegSwing); }
            Assert.Greater(runMax, walkMax + 15f, "a runner's stride is much wider than a walker's");
            float pitch = Mathf.DeltaAngle(0f, a.transform.Find("Body").localEulerAngles.x);
            Assert.Greater(pitch, 6f, "and leans forward");
        }

        [UnityTest]
        public IEnumerator IdlePhases_DifferBetweenPeople()
        {
            yield return Bare();
            var people = new List<PlaceholderActor>();
            for (int i = 0; i < 6; i++) people.Add(Make("p1_idle_" + i, new Vector3(i * 2f, 0, 0)));
            var phases = new HashSet<int>();
            foreach (var p in people) phases.Add(Mathf.RoundToInt(p.IdlePhase * 1000f));
            Assert.AreEqual(people.Count, phases.Count, "every person has their own idle phase");
            // And the bodies are visibly out of step: the sideways weight shift differs at the same moment.
            for (int f = 0; f < 90; f++) yield return null;
            float minX = 99f, maxX = -99f;
            foreach (var p in people) { float x = p.transform.Find("Body").localPosition.x; minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            Assert.Greater(maxX - minX, 0.01f, "standing people shift their weight at different times");
            // A person standing still keeps the legs together (the idle never walks on the spot).
            foreach (var p in people) Assert.Less(p.LegSwing, 1.5f, p.ActorId + " stands with the legs together");
        }

        [UnityTest]
        public IEnumerator TalkAndLook_MoveArmsAndHead()
        {
            yield return Bare();
            var a = Make("p1_talker", Vector3.zero);
            for (int f = 0; f < 10; f++) yield return null;
            var armR = a.Arm(true); var head = a.HeadPivot;
            a.Talking = true;
            float maxArm = 0f;
            for (int f = 0; f < 90; f++) { yield return null; maxArm = Mathf.Max(maxArm, -Mathf.DeltaAngle(0f, armR.localEulerAngles.x)); }
            Assert.Greater(maxArm, 40f, "a talker lifts the hands");
            a.Talking = false;
            a.LookAt(new Vector3(5f, 1.6f, 1f));
            for (int f = 0; f < 60; f++) yield return null;
            Assert.Greater(Mathf.DeltaAngle(0f, head.localEulerAngles.y), 35f, "the head turns towards the looked-at point (to the right)");
            a.SetCarry(PlaceholderActor.CarryStyle.Arms, Color.red);
            yield return null;
            Assert.IsNotNull(a.transform.Find("Body/Load"), "a carried load shows");
            a.SetCarry(PlaceholderActor.CarryStyle.None, Color.red);
            yield return null;
            Assert.IsNull(a.transform.Find("Body/Load"), "and goes when dropped");
        }

        [UnityTest]
        public IEnumerator MeshFigure_StridesWithDistance_AndStandsStill()
        {
            yield return Bare();
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
            var mb = new MeshBuilder(); mb.Box(Vector3.up, Vector3.one * 0.5f, Color.white); var m = mb.ToMesh("t");
            var f = MeshFigure.Build("fig", null, mat, m, m, m, 0.8f, 0.15f, 1.5f, 0.4f, 0);
            _made.Add(f.Root.gameObject);
            float max = 0f;
            for (int i = 0; i < 90; i++) { f.Step(1f / 60f, 1.4f / 60f, MeshFigure.Hold.Swing, i / 60f); if (i > 20) max = Mathf.Max(max, f.LegSwing); }
            Assert.Greater(max, 20f, "the figure's legs swing while it covers ground");
            for (int i = 0; i < 90; i++) f.Step(1f / 60f, 0f, MeshFigure.Hold.Swing, 1.5f + i / 60f);
            Assert.Less(f.LegSwing, 1.5f, "and close when it stands");
            yield return null;
        }

        // ------------------------------------------------------------------ P3

        [UnityTest]
        public IEnumerator Elbows_BendInSwingCarryStrikeAndGesture()
        {
            yield return Bare();
            var a = Make("p3_elbows", Vector3.zero);
            yield return null; yield return null;
            Assert.IsNotNull(a.Forearm(true), "the arm has a forearm on an elbow pivot");
            Assert.IsNotNull(a.transform.Find("Body/ArmR/ElbowR/Hand"), "the hand hangs from the forearm");
            // Walking: the elbow bends more as the arm swings forward than as it swings back.
            float min = 999f, max = 0f;
            for (int f = 0; f < 120; f++)
            {
                a.transform.position += Vector3.forward * 1.4f / 60f;
                yield return null;
                if (f < 30) continue;
                min = Mathf.Min(min, a.ElbowBend(true)); max = Mathf.Max(max, a.ElbowBend(true));
            }
            Assert.Greater(max - min, 8f, "the elbow bends and straightens with the swing");
            for (int f = 0; f < 60; f++) yield return null;
            // Carrying in the arms bends both elbows round the load.
            a.SetCarry(PlaceholderActor.CarryStyle.Arms, Color.red);
            yield return null; yield return null;
            Assert.Greater(a.ElbowBend(false), 40f, "the left elbow bends round a carried load");
            Assert.Greater(a.ElbowBend(true), 40f, "and the right");
            a.SetCarry(PlaceholderActor.CarryStyle.None, Color.red);
            // A staff blow: elbows cocked on the wind-up, nearly straight through the hit.
            Assert.IsTrue(a.PlayOneShot(ActorKeys.Strike));
            float cocked = 0f, straight = 999f;
            for (int f = 0; f < 45; f++) { yield return null; cocked = Mathf.Max(cocked, a.ElbowBend(true)); if (f > 18 && f < 30) straight = Mathf.Min(straight, a.ElbowBend(true)); }
            Assert.Greater(cocked, 70f, "the elbow cocks behind the head");
            Assert.Less(straight, 25f, "and straightens through the blow");
            // Block and gesture bend the elbows too.
            Assert.IsTrue(a.PlayOneShot(ActorKeys.Block));
            float block = 0f; for (int f = 0; f < 40; f++) { yield return null; block = Mathf.Max(block, a.ElbowBend(true)); }
            Assert.Greater(block, 70f, "a guard holds the forearms up");
            Assert.IsTrue(a.PlayOneShot(ActorKeys.Gesture));
            float gest = 0f; for (int f = 0; f < 70; f++) { yield return null; gest = Mathf.Max(gest, a.ElbowBend(false)); }
            Assert.Greater(gest, 30f, "an explaining gesture bends the elbows");
        }

        [UnityTest]
        public IEnumerator MeshFigure_HasKnees_ThatBendInTheStride()
        {
            yield return Bare();
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
            var mb = new MeshBuilder(); mb.Box(new Vector3(0, -0.4f, 0), new Vector3(0.2f, 0.8f, 0.2f), Color.white); var leg = mb.ToMesh("leg");
            var f = MeshFigure.Build("fig", null, mat, leg, leg, leg, 0.8f, 0.15f, 1.5f, 0.4f, 0);
            _made.Add(f.Root.gameObject);
            Assert.IsNotNull(f.KneeL, "the figure has knees");
            Assert.AreEqual(f.LegL, f.KneeL.parent, "the shin hangs from the thigh");
            var thigh = f.LegLFilter.sharedMesh.bounds; var shin = f.ShinLFilter.sharedMesh.bounds;
            Assert.AreEqual(0.4f, thigh.size.y, 0.01f, "the thigh is the upper half of the leg");
            Assert.AreEqual(0.4f, shin.size.y, 0.01f, "the shin the lower half");
            float max = 0f;
            for (int i = 0; i < 90; i++) { f.Step(1f / 60f, 1.4f / 60f, MeshFigure.Hold.Swing, i / 60f); if (i > 20) max = Mathf.Max(max, f.KneeBend); }
            Assert.Greater(max, 20f, "a knee bends as its leg swings through");
            for (int i = 0; i < 90; i++) f.Step(1f / 60f, 0f, MeshFigure.Hold.Swing, 1.5f + i / 60f);
            Assert.Less(f.KneeBend, 6f, "and straightens when standing");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Carrier_GaitIsCappedAtAWalk_WhileTheRootKeepsTheTripTiming()
        {
            yield return Bare();
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
            var mb = new MeshBuilder(); mb.Box(new Vector3(0, -0.4f, 0), new Vector3(0.2f, 0.8f, 0.2f), Color.white); var m = mb.ToMesh("leg");
            var f = MeshFigure.Build("carrier", null, mat, m, m, m, 0.8f, 0.15f, 1.5f, 0.4f, 0);
            _made.Add(f.Root.gameObject);
            f.MaxGaitSpeed = HistoricDirector.CarrierGaitCap;
            // A trip 104 m long in 7 s (a hex step with the sim's carry ticks) is 15 m/s along the ground.
            float maxSp = 0f, maxSwing = 0f;
            for (int i = 0; i < 120; i++) { f.Step(1f / 60f, 15f / 60f, MeshFigure.Hold.Load, i / 60f); if (i > 30) { maxSp = Mathf.Max(maxSp, f.Speed); maxSwing = Mathf.Max(maxSwing, f.LegSwing); } }
            Assert.Less(maxSp, Gait.RunFrom, "the carrier's legs show a walk, not a run");
            Assert.Less(maxSwing, 2f * Gait.Amplitude(Gait.RunFrom), "with a walking stride");
            Assert.Greater(maxSwing, 15f, "but still striding");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Soldier_Death_StaggersFallsLiesStill_ThenDissolves_WithoutShrinking()
        {
            yield return Bare();
            var parent = new GameObject("soldiers"); _made.Add(parent);
            var v = SoldierView.Create(parent.transform, new Soldier { Id = 4, People = 0 }, Color.blue, SoldierLook.Warrior);
            v.SnapTo(Vector3.zero); v.Target = Vector3.zero;
            yield return null; yield return null;
            var scale0 = v.BodyScale;
            var torso = v.transform.Find("Body/Torso").GetComponent<Renderer>();
            float stand = torso.bounds.max.y;
            v.Die();
            float t = 0f, maxPitch = 0f, staggerZ = 0f; bool fadedBeforeLying = false;
            while (t < SoldierView.FadeStart - 0.05f)
            {
                yield return null; t += Time.deltaTime;
                maxPitch = Mathf.Max(maxPitch, Mathf.Abs(v.BodyPitch));
                if (t < SoldierView.StaggerEnd) staggerZ = Mathf.Max(staggerZ, Mathf.Abs(v.transform.Find("Body").localPosition.z));
                if (v.Dissolved > 0f) fadedBeforeLying = true;
            }
            Assert.Greater(staggerZ, 0.2f, "the soldier staggers a step first");
            Assert.Greater(maxPitch, 75f, "then falls flat");
            Assert.Less(torso.bounds.max.y, stand * 0.45f, "and lies on the ground");
            Assert.IsFalse(fadedBeforeLying, "lying still, not yet fading");
            Assert.IsFalse(v.Finished);
            float minD = 1f;
            while (!v.Finished) { yield return null; Assert.AreEqual(scale0, v.BodyScale, "the body never shrinks"); minD = Mathf.Min(minD, v.Dissolved); }
            Assert.Greater(v.Dissolved, 0.9f, "it has dissolved away when finished");
            var r = torso.sharedMaterial;
            Assert.IsTrue(r.IsKeywordEnabled("WORLD_DISSOLVE"), "with the dissolving material");
        }

        [Test]
        public void EveryOneShotKey_PlaysOnThePlaceholder_AndHasARiggedClipOrFallback()
        {
            var a = PlaceholderActor.Build("p3_keys", Color.gray, null);
            _made.Add(a.gameObject);
            // Keys the rigged actor maps itself (P1 and earlier); every newer key must have fallback clips.
            var mapped = new System.Collections.Generic.HashSet<string> { ActorKeys.Wave, ActorKeys.Greet, ActorKeys.Beckon, ActorKeys.Cheer, ActorKeys.Surprise, ActorKeys.Clap, ActorKeys.Stretch, ActorKeys.Hammer, ActorKeys.Cast };
            foreach (var fi in typeof(ActorKeys).GetFields())
            {
                var key = (string)fi.GetValue(null);
                if (key == ActorKeys.Idle) continue;
                Assert.IsTrue(a.PlayOneShot(key), key + " plays on the placeholder");
                if (mapped.Contains(key)) continue;
                var clips = RiggedActor.FallbackClips(key);
                Assert.IsNotNull(clips, key + " has rigged fallback clips");
                Assert.Greater(clips.Length, 0, key);
            }
        }
    }
}
