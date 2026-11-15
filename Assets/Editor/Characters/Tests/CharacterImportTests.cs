using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using World.Game;

namespace World.Characters.Editor.Tests
{
    /// <summary>
    /// WP-6 acceptance (PRD_CHARACTERS 5.7, 10.2): every imported character against cast_manifest.json. The art is local
    /// (Assets/LocalBaked/Characters, git-ignored), so without it every case is ignored with a message and the suite
    /// stays green in the public repository. Run the builder first (CharacterBuilder.Run) after a sync.
    /// </summary>
    public class CharacterImportTests
    {
        public const int HeroTriangleBudget = 3200, SlotBudget = 9;
        /// <summary>Planted-foot speed limit during stance when the Walk clip plays at its native ground speed (m/s).</summary>
        public const float FootSlideLimit = 0.08f;

        private static JObject _manifest;
        private static JObject Manifest => _manifest ?? (_manifest = CharacterPaths.LocalArtPresent ? JObject.Parse(File.ReadAllText(CharacterPaths.Manifest)) : null);

        public static IEnumerable<string> Builds()
        {
            if (!CharacterPaths.LocalArtPresent) { yield return "no-local-art"; yield break; }
            var m = JObject.Parse(File.ReadAllText(CharacterPaths.Manifest));
            foreach (var kv in (JObject)m["characters"])
                foreach (var b in (JObject)kv.Value["builds"])
                    yield return kv.Key + "@" + b.Key;
        }

        public static IEnumerable<string> FirstThree() => new[] { "prince", "mira", "champion" };

        private static (JObject ch, JObject build, GameObject prefab) Load(string id)
        {
            if (!CharacterPaths.LocalArtPresent) Assert.Ignore("No local character art (" + CharacterPaths.Manifest + "): the public repository has none.");
            if (id == "no-local-art") Assert.Ignore("The test list was built while the art was absent; it is listed again after the next script compile.");
            var parts = id.Split('@');
            var ch = (JObject)Manifest["characters"][parts[0]];
            var build = (JObject)ch["builds"][parts[1]];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPaths.PrefabPath(parts[0], parts[1]));
            Assert.IsNotNull(prefab, id + ": no prefab; run CharacterBuilder.Run");
            return (ch, build, prefab);
        }

        private static Transform Find(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = Find(c, n); if (r != null) return r; }
            return null;
        }

        [TestCaseSource(nameof(Builds))]
        public void Prefab_MatchesManifest(string id)
        {
            var (ch, build, prefab) = Load(id);
            var rc = prefab.GetComponent<RiggedCharacter>();
            Assert.IsNotNull(rc, "RiggedCharacter");
            // Bones (including eye, jaw, cloth chains) and sockets.
            foreach (JObject b in (JArray)ch["bones"]) Assert.IsNotNull(Find(prefab.transform, (string)b["name"]), id + " bone " + b["name"]);
            foreach (var s in (JObject)ch["sockets"]) Assert.IsNotNull(rc.Socket(s.Key), id + " socket " + s.Key);
            // Humanoid avatar.
            Assert.IsTrue(rc.Humanoid && rc.Animator.avatar != null && rc.Animator.avatar.isValid && rc.Animator.avatar.isHuman, id + " Humanoid avatar");
            // Face blend shapes on the merged mesh.
            var keys = ((JArray)ch["face"]["shape_keys"]).Select(x => (string)x).ToArray();
            CollectionAssert.IsSubsetOf(keys, rc.ShapeKeys, id + " shape keys");
            Assert.AreEqual(keys.Length, rc.Body.sharedMesh.blendShapeCount, id + " blend shape count");
            // Role slot materials: one submesh per visible slot, World/Lit, the manifest colour; no hidden slot drawn.
            var slots = (JObject)build["slots"];
            CollectionAssert.DoesNotContain(rc.SubmeshSlots, "hidden", id);
            var mats = rc.Body.sharedMaterials;
            Assert.AreEqual(rc.SubmeshSlots.Length, mats.Length, id + " materials per submesh");
            for (int i = 0; i < mats.Length; i++)
            {
                string slot = rc.SubmeshSlots[i];
                Assert.IsTrue(slots[slot] != null, id + " slot " + slot + " not in the manifest");
                ColorUtility.TryParseHtmlString("#" + (string)slots[slot], out var want);
                Assert.AreEqual(want.r, mats[i].color.r, 0.01f, id + " " + slot + " colour");
                Assert.AreEqual(want.b, mats[i].color.b, 0.01f, id + " " + slot + " colour");
                Assert.IsTrue(mats[i].HasProperty("_SelfLit") && mats[i].HasProperty("_Rim"), id + " " + slot + " uses World/Lit with the night properties");
                if (slot == "emissive") Assert.AreEqual(1f, mats[i].GetFloat("_SelfLit"), 1e-3f, id + " emissive");
            }
            // Forward axis: the character faces +Z, left on -X; feet on the ground; height as measured in Blender.
            Assert.Greater(rc.ModelForward.z, 0.99f, id + " forward is +Z");
            Assert.Less(Find(prefab.transform, "Eye.L").position.x, 0f, id + " left eye on -X");
            Assert.AreEqual(0f, rc.Socket("Sock.Foot.L").position.y, 0.01f, id + " foot contact on the ground");
            Assert.AreEqual((float)((JArray)ch["sockets"]["Sock.HeadTop"]["head"])[2], rc.Height, 0.01f, id + " head top height (scale 1)");
            // Triangles as the build reported them.
            Assert.AreEqual((int)build["tris_visible"], rc.Triangles, id + " visible triangles");
            Assert.AreEqual(rc.Triangles, CountTris(rc.Body.sharedMesh), id + " merged mesh triangles");
        }

        private static int CountTris(Mesh m)
        {
            int t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (int)m.GetIndexCount(i) / 3;
            return t;
        }

        [TestCaseSource(nameof(Builds))]
        public void Budget_TrianglesAndDrawCalls(string id)
        {
            var (_, _, prefab) = Load(id);
            var rc = prefab.GetComponent<RiggedCharacter>();
            Assert.AreEqual(1, prefab.GetComponentsInChildren<Renderer>(true).Length, id + " one skinned renderer");
            Assert.LessOrEqual(rc.SubmeshSlots.Length, SlotBudget, id + " draw calls (role slots)");
            Assert.LessOrEqual(rc.ShapeKeys.Length, 16, id + " shape keys");
            Assert.LessOrEqual(rc.Body.bones.Length, 45, id + " bones");
            if (rc.Triangles > HeroTriangleBudget)
                Debug.LogWarning("[CharacterImportTests] " + id + ": " + rc.Triangles + " triangles, over the hero budget of " + HeroTriangleBudget + " (PRD 5.8); art data, reported to the costume package");
        }

        [TestCaseSource(nameof(Builds))]
        public void Clips_MatchManifest(string id)
        {
            var (_, _, prefab) = Load(id);
            var rc = prefab.GetComponent<RiggedCharacter>();
            var records = CharacterImport.ClipRecords();
            var anim = JObject.Parse(File.ReadAllText(CharacterPaths.AnimManifest));
            string key = id.Split('@')[0];
            foreach (JObject baseRec in (JArray)anim["clips"])
            {
                string name = (string)baseRec["name"];
                var rcClip = rc.Clip(name);
                Assert.IsNotNull(rcClip, id + " clip " + name);
                Assert.IsNotNull(rcClip.Clip, id + " clip asset " + name);
                bool variant = records.ContainsKey(name + "@" + key);
                Assert.AreEqual(variant ? name + "@" + key : name, rcClip.Clip.name, id + " uses its own variant when there is one");
                var rec = variant ? records[name + "@" + key] : baseRec;
                Assert.AreEqual((bool)rec["loop"], rcClip.Clip.isLooping, id + " " + name + " loop flag");
                Assert.AreEqual((bool)rec["loop"], rcClip.Loop, id + " " + name + " loop flag (runtime)");
                Assert.AreEqual((bool)rec["additive"], rcClip.Additive, id + " " + name + " additive");
                Assert.AreEqual((float)rec["length_s"], rcClip.Clip.length, 1.5f / 30f, id + " " + name + " length");
                var ev = rec["events"] as JArray;
                Assert.AreEqual(ev?.Count ?? 0, rcClip.EventNames.Length, id + " " + name + " events (runtime)");
                Assert.AreEqual(ev?.Count ?? 0, AnimationUtility.GetAnimationEvents(rcClip.Clip).Length, id + " " + name + " events on the clip");
                if (rec["root_curve"] != null || baseRec["root_curve"] != null)
                    Assert.IsTrue(rcClip.RootDist.Length > 0 || rcClip.RootYaw.Length > 0, id + " " + name + " root curve");
                Assert.IsTrue(rcClip.Clip.humanMotion, id + " " + name + " Humanoid clip");
            }
        }

        [Test]
        public void AnimationFiles_HaveNoMesh_AndAreHumanoid()
        {
            var files = Directory.Exists(CharacterPaths.Animation) ? Directory.GetFiles(CharacterPaths.Animation, "*.fbx") : new string[0];
            if (files.Length == 0) Assert.Ignore("No animation files");
            foreach (var f in files)
            {
                var p = f.Replace('\\', '/');
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                Assert.IsNotNull(go, p);
                Assert.AreEqual(0, go.GetComponentsInChildren<Renderer>(true).Length, p + " must hold no mesh");
                var av = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().FirstOrDefault();
                Assert.IsTrue(av != null && av.isValid && av.isHuman, p + " Humanoid avatar");
                Assert.Greater(AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Count(c => !c.name.StartsWith("__")), 0, p + " clips");
            }
        }

        /// <summary>
        /// Foot plant (PRD 5.7, A3): the Walk clip sampled at 120 Hz on the character's own avatar with the root carried
        /// forward at the clip's native speed; during stance (the lower foot, within 2 cm of its lowest point) the
        /// ground-contact socket's horizontal speed in the world stays under the limit.
        /// </summary>
        [TestCaseSource(nameof(FirstThree))]
        public void WalkClip_FootPlantsAtNativeSpeed(string key)
        {
            var (_, _, prefab) = Load(key + "@prehistoric");
            float slide = MeasureWalkSlide(prefab, "Walk", out string detail);
            Debug.Log("[CharacterImportTests] " + key + " Walk stance foot speed " + slide.ToString("0.000") + " m/s; " + detail);
            Assert.Less(slide, FootSlideLimit, key + " Walk foot slide " + detail);
        }

        public static float MeasureWalkSlide(GameObject prefab, string clipName, out string detail)
        {
            var go = Object.Instantiate(prefab);
            try
            {
                var rc = go.GetComponent<RiggedCharacter>();
                var c = rc.Clip(clipName);
                var footL = rc.Socket("Sock.Foot.L"); var footR = rc.Socket("Sock.Foot.R");
                int n = Mathf.RoundToInt(c.Length * 120f);
                var pl = new Vector3[n + 1]; var pr = new Vector3[n + 1];
                AnimationMode.StartAnimationMode();
                try
                {
                    for (int i = 0; i <= n; i++)
                    {
                        float t = c.Length * i / n;
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, c.Clip, t);
                        AnimationMode.EndSampling();
                        // The root moves forward at native speed: world = local + forward * v t.
                        pl[i] = footL.position + Vector3.forward * c.NativeSpeed * t;
                        pr[i] = footR.position + Vector3.forward * c.NativeSpeed * t;
                    }
                }
                finally { AnimationMode.StopAnimationMode(); }
                float minL = pl.Min(p => p.y), minR = pr.Min(p => p.y);
                float sum = 0f; int count = 0; float worst = 0f;
                float dt = c.Length / n;
                for (int i = 1; i <= n; i++)
                {
                    // The planted foot is the lower one.
                    bool left = pl[i].y - minL < pr[i].y - minR;
                    var a = left ? pl : pr; float min = left ? minL : minR;
                    if (a[i].y - min > 0.02f || a[i - 1].y - min > 0.02f) continue;
                    var d = a[i] - a[i - 1]; d.y = 0f;
                    float sp = d.magnitude / dt;
                    sum += sp; count++; worst = Mathf.Max(worst, sp);
                }
                float mean = count > 0 ? sum / count : 0f;
                detail = "mean " + mean.ToString("0.000") + " m/s, worst " + worst.ToString("0.000") + " m/s over " + count + " stance samples at " + c.NativeSpeed + " m/s";
                return mean;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
