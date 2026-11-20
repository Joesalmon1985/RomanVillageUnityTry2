using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
    }
}
