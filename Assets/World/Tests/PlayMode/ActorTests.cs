using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Bedsit;

namespace World.Game.Tests
{
    /// <summary>
    /// WP-7 of Docs/PRD_CHARACTERS.md: the rigged actor and the factory. The character art is local (git-ignored), so
    /// every test that needs a body ignores itself with a message when the prefab is absent; the factory fallback test
    /// runs everywhere. Bare-scene tests move the root by hand; the in-game tests use the World scene at night.
    /// </summary>
    public class ActorTests
    {
        /// <summary>
        /// Planted-foot speed allowed while the root moves at a steady speed (mean over stance samples): 0.25 m/s or 11
        /// percent of the ground speed, whichever is larger. The authored clips themselves, sampled at their own native
        /// speed in the editor, measure Walk 0.05, WalkBrisk 0.11, Jog 0.35 and Run 0.56 m/s by the same method
        /// (the running gaits carry the slide; the runtime adds little to it).
        /// </summary>
        public const float SlideLimit = 0.25f, SlideShare = 0.11f;

        private static bool HasArt(string key = "prince") => CastRegistry.LoadPrefab(key, CastRegistry.DefaultCostume) != null;
        private static void NeedArt(string key = "prince")
        {
            if (!HasArt(key)) Assert.Ignore("No local character art for " + key + " (Assets/LocalBaked/Characters): the placeholder plays it.");
        }

        private bool _force, _all;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp() { _force = CastRegistry.ForcePlaceholders; _all = CastRegistry.EnableAllRoles; RiggedActor.LodEnabled = false; Time.captureFramerate = 60; }

        [TearDown]
        public void TearDown()
        {
            CastRegistry.ForcePlaceholders = _force; CastRegistry.EnableAllRoles = _all; RiggedActor.LodEnabled = true; Time.captureFramerate = 0;
            foreach (var g in _made) if (g != null) Object.Destroy(g);
            _made.Clear();
            Time.timeScale = 1f;
        }

        private IEnumerator Bare()
        {
            // An empty scene: no world, no player, no lights but one, so the bare actor is all that runs.
            var s = SceneManager.CreateScene("ActorTestsBare" + Random.Range(0, 1 << 30));
            SceneManager.SetActiveScene(s);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var o = SceneManager.GetSceneAt(i);
                // Only the game's scene goes; the test runner's own scene must stay loaded.
                if (o != s && o.isLoaded && o.name == "World") yield return SceneManager.UnloadSceneAsync(o);
            }
            var cam = new GameObject("Test camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 1.6f, 6f); cam.transform.LookAt(new Vector3(0, 1f, 0));
            var sun = new GameObject("Test light").AddComponent<Light>(); sun.type = LightType.Directional;
            yield return null;
        }

        private RiggedActor Make(string id, Vector3 pos, float yaw)
        {
            var a = ActorFactory.Create(id, Color.gray, pos, yaw, null) as RiggedActor;
            Assert.IsNotNull(a, id + " should be rigged");
            _made.Add(a.gameObject);
            return a;
        }

        // ------------------------------------------------------------------ factory

        [UnityTest]
        public IEnumerator Factory_FallsBackToPlaceholder_AndLogsNothing()
        {
            yield return null;
            var parent = new GameObject("ActorTests factory").transform; _made.Add(parent.gameObject);
            // Unknown ids and roles with no body are placeholders.
            Assert.IsInstanceOf<PlaceholderActor>(ActorFactory.Create("nobody_at_all", Color.red, parent));
            Assert.IsInstanceOf<PlaceholderActor>(ActorFactory.Create("camp_figure_3", Color.red, Vector3.zero, 30f, parent));
            // Forced placeholders (the "before" footage) win over any art.
            CastRegistry.ForcePlaceholders = true;
            Assert.IsInstanceOf<PlaceholderActor>(ActorFactory.Create(NpcIds.Prince, Color.blue, parent));
            CastRegistry.ForcePlaceholders = false;
            var prince = ActorFactory.Create(NpcIds.Prince, Color.blue, new Vector3(3, 0, 0), 90f, parent);
            if (HasArt("prince"))
            {
                Assert.IsInstanceOf<RiggedActor>(prince, "the reviewed prince is rigged when his art exists");
                Assert.AreEqual(90f, prince.Root.eulerAngles.y, 0.5f, "yaw from the factory");
                Assert.IsInstanceOf<RiggedActor>(ActorFactory.Create(NpcIds.Mira, Color.blue, parent));
                Assert.IsInstanceOf<RiggedActor>(ActorFactory.Create(NpcIds.RivalChief, Color.blue, parent));
                // Not reviewed yet: the sawyer stays a placeholder unless the developer flag is on.
                Assert.IsInstanceOf<PlaceholderActor>(ActorFactory.Create(NpcIds.Sawyer, Color.blue, parent));
                CastRegistry.EnableAllRoles = true;
                var saw = ActorFactory.Create(NpcIds.Sawyer, Color.blue, parent);
                if (HasArt("carpenter")) Assert.IsInstanceOf<RiggedActor>(saw, "developer flag enables the rest of the cast");
            }
            else Assert.IsInstanceOf<PlaceholderActor>(prince, "no art: placeholder");
            // Every actor keeps the shared interface.
            Assert.IsTrue(prince.PlayOneShot(ActorKeys.Greet));
            prince.FaceTowards(Vector3.zero); prince.SetVisible(false); prince.SetVisible(true); prince.PlayIdle();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ------------------------------------------------------------------ locomotion

        /// <summary>Mean planted-foot speed while the root moves at <paramref name="speed"/> for a few seconds.</summary>
        private static IEnumerator Slide(RiggedActor a, float speed, float seconds, float[] result)
        {
            var dir = a.Root.forward;
            float t = 0f; Vector3 lastL = a.FootContact(true), lastR = a.FootContact(false);
            float sum = 0f; int n = 0; float worst = 0f;
            while (t < seconds)
            {
                yield return null;
                float dt = Time.deltaTime; t += dt;
                a.Root.position += dir * speed * dt;
                var l = a.FootContact(true); var r = a.FootContact(false);
                if (t > 1.0f) // after the blend has settled
                {
                    bool left = l.y < r.y;
                    var cur = left ? l : r; var prev = left ? lastL : lastR;
                    if (cur.y < 0.02f && dt > 0f)
                    {
                        var d = cur - prev; d.y = 0f;
                        float sp = d.magnitude / dt;
                        sum += sp; n++; worst = Mathf.Max(worst, sp);
                    }
                }
                lastL = l; lastR = r;
            }
            result[0] = n > 0 ? sum / n : 0f; result[1] = worst; result[2] = n;
        }

        [UnityTest]
        public IEnumerator Locomotion_PlaybackMatchesGroundSpeed_FeetStayPlanted()
        {
            NeedArt();
            yield return Bare();
            var a = Make(NpcIds.Prince, Vector3.zero, 0f);
            yield return null;
            var report = new List<string>();
            var fails = new List<string>();
            // The lattice speeds the game uses: walk 1.4, stroll 3 (Npc.StrollSpeed), tag-along 5, lead 7, hurry 8.
            foreach (float v in new[] { 1.4f, 2.2f, 3f, 5f, 7f, 8f })
            {
                var res = new float[3];
                yield return Slide(a, v, 3.5f, res);
                report.Add(v + " m/s: mean " + res[0].ToString("0.000") + " worst " + res[1].ToString("0.00") + " (" + res[2] + " samples)");
                if (res[2] <= 10) fails.Add("stance samples at " + v);
                if (res[0] >= Mathf.Max(SlideLimit, SlideShare * v)) fails.Add("foot slide " + res[0].ToString("0.000") + " at " + v + " m/s");
                if (Mathf.Abs(v - a.GroundSpeed) > 0.2f) fails.Add("measured ground speed " + a.GroundSpeed + " at " + v);
            }
            Debug.Log("[ActorTests] foot slide by speed: " + string.Join("; ", report));
            Assert.IsEmpty(fails, string.Join("; ", fails));
        }

        [UnityTest]
        public IEnumerator Turn_StandingOnLatticeHeadings_FollowsTheTurnClip()
        {
            NeedArt();
            yield return Bare();
            var a = Make(NpcIds.Prince, Vector3.zero, 0f);
            yield return new WaitForSeconds(0.3f);
            // One lattice heading (60 degrees) to the right: the turn clip plays and the root follows its yaw curve.
            a.FaceTowards(a.Root.position + Quaternion.Euler(0, 60f, 0) * Vector3.forward * 5f);
            yield return null; yield return null;
            Assert.IsTrue(a.IsTurningInPlace, "a 60 degree turn from standing plays Turn60R");
            float mid = -1f;
            for (float t = 0; t < 1.5f && a.IsTurningInPlace; t += Time.deltaTime) { if (t > 0.25f && mid < 0f) mid = a.Root.eulerAngles.y; yield return null; }
            Assert.IsFalse(a.IsTurningInPlace, "turn finished");
            Assert.AreEqual(60f, a.Root.eulerAngles.y, 1f, "faces the new heading");
            Assert.That(mid, Is.InRange(5f, 58f), "the yaw eases along the clip's curve, not a snap");
            // Two headings to the left (120 degrees): a longer turn, same clip family.
            a.FaceTowards(a.Root.position + Quaternion.Euler(0, -60f, 0) * Vector3.forward * 5f);
            yield return new WaitForSeconds(1.4f);
            Assert.AreEqual(300f, a.Root.eulerAngles.y, 1.5f, "120 degree turn left");
            // A small correction (20 degrees) turns smoothly without the clip.
            a.FaceTowards(a.Root.position + Quaternion.Euler(0, -40f, 0) * Vector3.forward * 5f);
            yield return null; yield return null;
            Assert.IsFalse(a.IsTurningInPlace, "small turns do not play the turn clip");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(320f, a.Root.eulerAngles.y, 1f);
        }

        [UnityTest]
        public IEnumerator Gaze_ReachesTarget_WithinLimits_EyesFirst()
        {
            NeedArt();
            yield return Bare();
            var a = Make(NpcIds.Mira, Vector3.zero, 0f);
            var rc = a.Character;
            yield return new WaitForSeconds(0.3f);
            var eyes = (rc.EyeL.position + rc.EyeR.position) * 0.5f;
            var target = eyes + Quaternion.Euler(0, 35f, 0) * Vector3.forward * 2.5f + Vector3.up * 0.1f;
            a.SetGaze(target);
            // Eyes arrive first: within a few frames the eyes have turned further than the head.
            yield return new WaitForSeconds(0.08f);
            float eyeYaw = Vector3.SignedAngle(a.Root.forward, Flat(a.Face.EyeForward), Vector3.up);
            var headFwd = Flat(a.Face.EyeCentre - rc.Head.position);
            Assert.Greater(eyeYaw - Vector3.SignedAngle(a.Root.forward, headFwd, Vector3.up), 3f, "eyes lead the head");
            yield return new WaitForSeconds(1.2f);
            var dir = target - a.Face.EyeCentre;
            float err = Vector3.Angle(a.Face.EyeForward, dir);
            Assert.Less(err, 4f, "the gaze reaches a target 35 degrees to the side (error " + err.ToString("0.0") + ")");
            // Behind the shoulder the gaze does not crank round: head turn stays inside its limit.
            a.SetGaze(a.Face.EyeCentre + Quaternion.Euler(0, 150f, 0) * Vector3.forward * 2f);
            yield return new WaitForSeconds(1f);
            float behind = Vector3.Angle(Flat(a.Face.EyeForward), a.Root.forward);
            Assert.Less(behind, rc.Tuning.HeadYaw + rc.Tuning.EyeYaw + 15f, "clamped");
            a.SetGaze((Vector3?)null);
            Debug.Log("[ActorTests] gaze error " + err.ToString("0.00") + " deg; early eye yaw " + eyeYaw.ToString("0.0"));
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

        [UnityTest]
        public IEnumerator Expression_SetAndReset_BlinkRuns()
        {
            NeedArt();
            yield return Bare();
            var a = Make(NpcIds.Prince, Vector3.zero, 0f);
            var rc = a.Character;
            int brow = rc.Shape("BrowRaise.L"), mouth = rc.Shape("MouthOpen"), blink = rc.Shape("Blink.L");
            Assert.IsFalse(a.SetExpression("no_such_face"));
            Assert.IsTrue(a.SetExpression("surprised"));
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(rc.Body.GetBlendShapeWeight(brow), 60f, "brows up");
            Assert.Greater(rc.Body.GetBlendShapeWeight(mouth), 40f, "mouth open");
            Assert.AreEqual("surprised", a.CurrentExpression);
            a.ResetExpression();
            yield return new WaitForSeconds(0.6f);
            Assert.Less(rc.Body.GetBlendShapeWeight(brow), 5f, "back to neutral");
            // Blinks happen every 2.5 to 6 s.
            float max = 0f;
            for (float t = 0; t < 7f; t += Time.deltaTime) { max = Mathf.Max(max, rc.Body.GetBlendShapeWeight(blink)); yield return null; }
            Assert.Greater(max, 80f, "blinked within 7 s");
            // Talking moves the mouth.
            a.SetTalking(true);
            float open = 0f;
            for (float t = 0; t < 1f; t += Time.deltaTime) { yield return null; open = Mathf.Max(open, rc.Body.GetBlendShapeWeight(mouth)); }
            Assert.Greater(open, 20f, "talk ticker");
            a.SetTalking(null);
        }

        [UnityTest]
        public IEnumerator Props_StayOnSockets_ThroughGestures()
        {
            NeedArt();
            yield return Bare();
            var a = Make(NpcIds.Prince, Vector3.zero, 0f);
            var torch = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            torch.localScale = new Vector3(0.05f, 0.4f, 0.05f);
            Assert.IsTrue(a.Attach(torch, "Sock.Hand.R", new Vector3(0, 0.1f, 0)));
            Assert.IsFalse(a.Attach(torch, "Sock.Nowhere"));
            var start = torch.position;
            Assert.IsTrue(a.PlayOneShot(ActorKeys.Greet));
            float moved = 0f;
            var sock = a.Socket("Sock.Hand.R");
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                yield return null;
                moved = Mathf.Max(moved, Vector3.Distance(start, torch.position));
                Assert.AreEqual(0f, Vector3.Distance(sock.TransformPoint(new Vector3(0, 0.1f, 0)), torch.position), 1e-3f, "prop stays on the socket");
            }
            Assert.Greater(moved, 0.15f, "the prop moved with the hand");
            foreach (var s in new[] { "Sock.Hand.L", "Sock.StaffTip", "Sock.SpellOrigin", "Sock.Back", "Sock.Hip.R", "Sock.HeadTop", "Sock.Foot.L", "Sock.Foot.R" })
                Assert.IsNotNull(a.Socket(s), s);
            a.ResetPresentation();
            yield return null;
            Assert.IsTrue(torch == null, "ResetPresentation removes props attached at run time");
            LogAssert.NoUnexpectedReceived();
        }

        // ------------------------------------------------------------------ in the game, at night

        private WorldBootstrap Boot;

        private IEnumerator LoadWorld()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return null; yield return null;
            Boot = WorldBootstrap.Instance;
            Assert.IsNotNull(Boot);
            yield return new WaitForSeconds(1f);
        }

        [UnityTest]
        public IEnumerator InGame_NightMaterials_CarriedLight_StopOnTheLattice()
        {
            NeedArt();
            yield return LoadWorld();
            Assert.Greater(DayNight.Instance.Night, 0.9f, "night start");
            var prince = PrinceDirector.Instance.Prince;
            var mira = Npc.Find(NpcIds.Mira);
            Assert.IsInstanceOf<RiggedActor>(prince.Actor, "prince rigged");
            Assert.IsInstanceOf<RiggedActor>(mira.Actor, "Mira rigged");
            foreach (var n in new[] { prince, mira })
            {
                var r = ((RiggedActor)n.Actor).Character.Body;
                foreach (var m in r.sharedMaterials)
                    if (!m.name.Contains("eye_dark")) Assert.Greater(m.GetFloat("_SelfLit"), 0.1f, n.Id + " " + m.name + " night self-light");
            }
            yield return new WaitForSeconds(1f);
            var deco = Object.FindFirstObjectByType<LightDecorator>();
            Assert.IsNotNull(deco != null ? deco.PrinceLight : null, "the prince's carried light");
            Assert.IsTrue(deco.PrinceLight.transform.IsChildOf(prince.transform), "carried by the rigged prince");
            // A rigged villager on the lattice: two steps ahead, it starts (weight shift), walks, stops exactly on the cell.
            var start = WalkLattice.ToWorld(WalkLattice.ToCell(Boot.Avatar.Position) + WalkLattice.Dir(0) * 3);
            var chief = Npc.Spawn(NpcIds.RivalChief, "Test champion", "", "", Color.gray, start, 0f);
            chief.Directed = true;
            var actor = (RiggedActor)chief.Actor;
            yield return new WaitForSeconds(0.5f);
            var goal = WalkLattice.ToWorld(WalkLattice.ToCell(start) + WalkLattice.Dir(1) * 2);
            chief.WalkSpeed = Npc.StrollSpeed;
            chief.WalkTo(goal);
            float t0 = Time.time; bool sawLead = false, sawStop = false;
            while (chief.IsWalking && Time.time - t0 < 15f)
            {
                if (actor.Phase_ == "Lead") sawLead = true;
                if (actor.Phase_ == "Stop") sawStop = true;
                yield return null;
            }
            float took = Time.time - t0;
            Assert.IsFalse(chief.IsWalking, "arrived");
            Assert.Less(BoardSpace.Flat(chief.transform.position - goal), 0.02f, "stops on the cell");
            Assert.IsTrue(sawLead, "weight shift before the first step");
            Assert.IsTrue(sawStop, "stop clip near the goal");
            Assert.Less(took, 10f / Npc.StrollSpeed + 1.5f, "the start and stop cost little time (" + took.ToString("0.00") + " s)");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual("Stand", actor.Phase_);
            Debug.Log("[ActorTests] lattice walk of 10 m at " + Npc.StrollSpeed + " m/s took " + took.ToString("0.00") + " s");
            Object.Destroy(chief.gameObject);
        }

        [UnityTest]
        public IEnumerator InGame_FrameRate_ThreeRiggedPlusCrowd_AtNight()
        {
            NeedArt();
            Time.captureFramerate = 0;
            yield return LoadWorld();
            int vs = QualitySettings.vSyncCount; int target = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            try
            {
                var at = WalkLattice.ToWorld(WalkLattice.ToCell(Boot.Avatar.Position) + WalkLattice.Dir(0) * 2);
                var chief = Npc.Spawn(NpcIds.RivalChief, "Test champion", "", "", Color.gray, at, 180f);
                var prince = PrinceDirector.Instance.Prince;
                int rigged = Object.FindObjectsByType<RiggedActor>(FindObjectsSortMode.None).Length;
                Assert.GreaterOrEqual(rigged, 3, "three rigged characters");
                int crowd = Object.FindObjectsByType<PlaceholderActor>(FindObjectsSortMode.None).Length + Object.FindObjectsByType<LandFigure>(FindObjectsSortMode.None).Length;
                yield return new WaitForSeconds(1f);
                int frames = 0; float t0 = Time.realtimeSinceStartup; float worst = 0f;
                while (Time.realtimeSinceStartup - t0 < 4f) { yield return null; frames++; worst = Mathf.Max(worst, Time.unscaledDeltaTime); }
                float fps = frames / (Time.realtimeSinceStartup - t0);
                Debug.Log("[ActorTests] fps " + fps.ToString("0") + " (worst frame " + (worst * 1000f).ToString("0.0") + " ms) at " + Screen.width + "x" + Screen.height + ", rigged " + rigged + ", crowd " + crowd + ", night " + DayNight.Instance.Night.ToString("0.00"));
                Assert.Greater(fps, 60f, "frame rate with three rigged characters at night");
                Object.Destroy(chief.gameObject);
            }
            finally { QualitySettings.vSyncCount = vs; Application.targetFrameRate = target; }
        }
    }
}
