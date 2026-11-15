using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using World.Game;

namespace World.Characters.Editor
{
    /// <summary>
    /// WP-6 of Docs/PRD_CHARACTERS.md: turns the synced character art into game-ready assets with no clicking.
    /// Run with <c>unity command script run --file Assets/Editor/Characters/CharacterBuilder.cs --entry World.Characters.Editor.CharacterBuilder.Run</c>
    /// or the menu World/Characters/Build all.
    ///
    /// 1. Animation files (tracked): Humanoid avatar from each file's own skeleton; clip settings come from
    ///    <see cref="CharacterImport"/>. The upper-body avatar mask.
    /// 2. Per character and costume (local): role-slot materials from the manifest colours (World/Lit with the night
    ///    properties), Humanoid avatar (Generic fallback), one merged skinned mesh with one submesh per role slot
    ///    (hidden pieces dropped, face blend shapes kept), a prefab in Resources/Characters with a <see cref="RiggedCharacter"/>
    ///    holding sockets, face data and the resolved clip set.
    /// Writes a report to Library/CharacterBuild.txt and returns it.
    /// </summary>
    public static class CharacterBuilder
    {
        public static readonly string[] SlotOrder = { "skin", "hair", "cloth_main", "cloth_trim", "leather", "metal", "eye_white", "eye_dark", "iris", "emissive" };

        [MenuItem("World/Characters/Build all")]
        public static void Menu() { Debug.Log(Run()); }

        public static string Run() => Build(null);

        public static string Build(string[] onlyKeys)
        {
            var log = new StringBuilder();
            if (!CharacterPaths.LocalArtPresent)
            {
                log.AppendLine("No local character art (" + CharacterPaths.Manifest + "); run Tools/characters/sync_unity_local.py first. Nothing built.");
                return Finish(log);
            }
            var manifest = JObject.Parse(File.ReadAllText(CharacterPaths.Manifest));
            var humanMap = HumanMap();
            AssetDatabase.StartAssetEditing();
            try { EnsureFolders(); }
            finally { AssetDatabase.StopAssetEditing(); }

            // 1. Animation files.
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var path in Directory.GetFiles(CharacterPaths.Animation, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                string res = EnsureHumanoid(path, humanMap, false, out _);
                log.AppendLine("anim " + Path.GetFileName(path) + ": " + res);
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    if (!c.name.StartsWith("__preview__")) clips[c.name] = c;
            }
            EnsureUpperBodyMask();
            log.AppendLine("clips: " + clips.Count);

            // 2. Characters.
            var hidden = HiddenMaterial();
            foreach (var kv in (JObject)manifest["characters"])
            {
                string key = kv.Key;
                if (onlyKeys != null && !onlyKeys.Contains(key)) continue;
                var ch = (JObject)kv.Value;
                foreach (var b in (JObject)ch["builds"])
                {
                    string costume = b.Key;
                    string model = CharacterPaths.ModelPath(key, costume);
                    if (!File.Exists(model)) { log.AppendLine(key + "@" + costume + ": no model"); continue; }
                    try { log.AppendLine(BuildCharacter(key, costume, ch, (JObject)b.Value, model, humanMap, clips, hidden)); }
                    catch (Exception e) { log.AppendLine(key + "@" + costume + ": FAILED " + e.Message); Debug.LogException(e); }
                }
            }
            AssetDatabase.SaveAssets();
            return Finish(log);
        }

        private static string Finish(StringBuilder log)
        {
            var s = log.ToString();
            try { File.WriteAllText("Library/CharacterBuild.txt", s); } catch { }
            return s;
        }

        private static void EnsureFolders()
        {
            foreach (var f in new[] { CharacterPaths.Materials, CharacterPaths.Meshes, CharacterPaths.Prefabs, CharacterPaths.Masks })
                Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
        }

        /// <summary>contract.json humanoid_map: our bone name to Unity human bone name.</summary>
        public static Dictionary<string, string> HumanMap()
        {
            var path = "Tools/characters/contract.json";
            var d = new Dictionary<string, string>();
            if (File.Exists(path))
            {
                var c = JObject.Parse(File.ReadAllText(path));
                foreach (var kv in (JObject)c["humanoid_map"]) d[kv.Key] = (string)kv.Value;
            }
            return d;
        }

        // ---------------- avatars ----------------

        /// <summary>
        /// Makes the FBX at <paramref name="path"/> Humanoid with an explicit bone map (no auto-mapping). Falls back to
        /// Generic when Unity rejects the description. Returns a one-line report.
        /// </summary>
        public static string EnsureHumanoid(string path, Dictionary<string, string> map, bool force, out bool human)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            human = false;
            if (!force && imp.animationType == ModelImporterAnimationType.Human && avatar != null && avatar.isValid && avatar.isHuman)
            {
                human = true;
                return "Humanoid (kept)";
            }
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var all = root.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(all.Select(t => t.name));
            var bones = map.Where(kv => names.Contains(kv.Key)).Select(kv => new HumanBone
            {
                boneName = kv.Key, humanName = kv.Value, limit = new HumanLimit { useDefaultValues = true },
            }).ToArray();
            var skeleton = all.Select(t => new SkeletonBone
            {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
            }).ToArray();
            var hd = new HumanDescription
            {
                human = bones, skeleton = skeleton,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.humanDescription = hd;
            imp.SaveAndReimport();
            avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar != null && avatar.isValid && avatar.isHuman)
            {
                human = true;
                return "Humanoid (" + bones.Length + " bones mapped)";
            }
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.motionNodeName = "";
            imp.SaveAndReimport();
            return "Generic (Humanoid rejected the rig)";
        }

        /// <summary>Upper body for gestures while walking: head, arms and hands; legs, hips and spine stay with locomotion.</summary>
        private static void EnsureUpperBodyMask()
        {
            var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(CharacterPaths.UpperBodyMask);
            bool create = m == null;
            if (create) m = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var p = (AvatarMaskBodyPart)i;
                bool on = p == AvatarMaskBodyPart.Head || p == AvatarMaskBodyPart.LeftArm || p == AvatarMaskBodyPart.RightArm
                          || p == AvatarMaskBodyPart.LeftFingers || p == AvatarMaskBodyPart.RightFingers
                          || p == AvatarMaskBodyPart.LeftHandIK || p == AvatarMaskBodyPart.RightHandIK;
                m.SetHumanoidBodyPartActive(p, on);
            }
            if (create) AssetDatabase.CreateAsset(m, CharacterPaths.UpperBodyMask);
            else EditorUtility.SetDirty(m);
        }

        // ---------------- materials ----------------

        private static Material HiddenMaterial()
        {
            string path = CharacterPaths.Materials + "/Role_hidden.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find("World/ActorHidden");
            m = new Material(sh) { name = "Role_hidden" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// One World/Lit material per role slot of this costume, coloured from the manifest. Night readability comes
        /// from the lighting package's own properties (_SelfLit, _Rim, multiplied by the global _WorldNight):
        /// RiggedActor applies LightShapes' Person variant at run time; the eyes and the glow keep their own values here.
        /// </summary>
        private static Dictionary<string, Material> SlotMaterials(string key, string costume, JObject slots)
        {
            string dir = CharacterPaths.Materials + "/" + key + "@" + costume;
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
            var lit = Resources.Load<Material>(MaterialFactory.LitPath);
            var d = new Dictionary<string, Material>();
            foreach (var kv in slots)
            {
                string slot = kv.Key;
                ColorUtility.TryParseHtmlString("#" + (string)kv.Value, out var col);
                string path = dir + "/Role_" + slot + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool create = m == null;
                if (create) m = new Material(lit);
                m.shader = lit.shader;
                m.name = "Role_" + slot;
                m.color = col;
                m.SetFloat("_SelfLit", slot == "emissive" ? 1f : 0f);
                m.SetColor("_Rim", Color.black);
                m.enableInstancing = true;
                if (create) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                d[slot] = m;
            }
            return d;
        }

        // ---------------- characters ----------------

        private static string BuildCharacter(string key, string costume, JObject ch, JObject build, string modelPath,
            Dictionary<string, string> map, Dictionary<string, AnimationClip> clips, Material hidden)
        {
            var mats = SlotMaterials(key, costume, (JObject)build["slots"]);
            // Remap the FBX's Role_* materials to the project ones, so the model asset itself never shows imported materials.
            var imp = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            bool dirty = false;
            var wanted = new Dictionary<string, Material>();
            foreach (var kv in mats) wanted["Role_" + kv.Key] = kv.Value;
            wanted["Role_hidden"] = hidden;
            var existing = imp.GetExternalObjectMap();
            foreach (var kv in wanted)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                if (!existing.TryGetValue(id, out var cur) || cur != kv.Value) { imp.AddRemap(id, kv.Value); dirty = true; }
            }
            if (dirty) imp.SaveAndReimport();
            string rig = EnsureHumanoid(modelPath, map, false, out bool human);

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            var go = UnityEngine.Object.Instantiate(src);
            go.name = key + "@" + costume;
            try
            {
                var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var mesh = MergeMesh(key, costume, smrs, out var slotsOut, out int tris);
                var refSmr = smrs.First(s => s.name == CharacterImport.MeshPrefix + "Body");
                var bones = refSmr.bones;
                var hips = Find(go.transform, "Hips");
                foreach (var s in smrs) UnityEngine.Object.DestroyImmediate(s.gameObject);
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(go.transform, false);
                var smr = bodyGo.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = hips;
                smr.localBounds = new Bounds(hips.InverseTransformPoint(go.transform.position + Vector3.up * 1.0f), new Vector3(1.6f, 2.4f, 1.6f));
                smr.sharedMaterials = slotsOut.Select(s => mats.TryGetValue(s, out var m) ? m : hidden).ToArray();
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Auto;
                smr.skinnedMotionVectors = false;
                smr.allowOcclusionWhenDynamic = true;

                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.runtimeAnimatorController = null;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                anim.updateMode = AnimatorUpdateMode.Normal;

                var col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0); col.height = 1.8f; col.radius = 0.35f;

                var rc = go.AddComponent<RiggedCharacter>();
                rc.Key = key; rc.Costume = costume; rc.Humanoid = human;
                rc.Animator = anim; rc.Body = smr; rc.SubmeshSlots = slotsOut.ToArray();
                rc.Triangles = tris;
                rc.UpperBody = AssetDatabase.LoadAssetAtPath<AvatarMask>(CharacterPaths.UpperBodyMask);
                rc.TalkClip = key == "champion" ? "Talk_Emphasis" : key == "prince" || key == "mira" ? "Talk_B" : "Talk_A";
                FillBones(rc, go.transform, ch);
                FillFace(rc, ch, mesh);
                var missing = FillClips(rc, key, clips);
                var headTop = rc.Socket("Sock.HeadTop");
                rc.Height = headTop != null ? headTop.position.y - go.transform.position.y : 1.8f;
                var eyeMid = (rc.EyeL.position + rc.EyeR.position) * 0.5f;
                var fwd = eyeMid - rc.Head.position; fwd.y = 0;
                rc.ModelForward = go.transform.InverseTransformDirection(fwd.normalized);

                string prefabPath = CharacterPaths.PrefabPath(key, costume);
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                return key + "@" + costume + ": " + rig + ", tris " + tris + ", slots " + string.Join(",", slotsOut)
                       + ", clips " + rc.Clips.Length + (missing.Count > 0 ? " (missing: " + string.Join(",", missing) + ")" : "")
                       + ", forward " + rc.ModelForward.ToString("F2") + ", height " + rc.Height.ToString("F2");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }

        private static void FillBones(RiggedCharacter rc, Transform root, JObject ch)
        {
            rc.Hips = Find(root, "Hips"); rc.Spine = Find(root, "Spine"); rc.Chest = Find(root, "Chest");
            rc.Neck = Find(root, "Neck"); rc.Head = Find(root, "Head"); rc.Jaw = Find(root, "Jaw");
            rc.EyeL = Find(root, "Eye.L"); rc.EyeR = Find(root, "Eye.R");
            rc.HandL = Find(root, "Hand.L"); rc.HandR = Find(root, "Hand.R");
            rc.FootL = Find(root, "Foot.L"); rc.FootR = Find(root, "Foot.R");
            rc.Cape = new[] { "Cape.1", "Cape.2", "Cape.3" }.Select(n => Find(root, n)).Where(t => t != null).ToArray();
            rc.Hood = new[] { "Hood.1", "Hood.2" }.Select(n => Find(root, n)).Where(t => t != null).ToArray();
            rc.Skirt = new[] { "Skirt.F", "Skirt.B", "Skirt.L", "Skirt.R" }.Select(n => Find(root, n)).Where(t => t != null).ToArray();
            var names = ((JObject)ch["sockets"]).Properties().Select(p => p.Name).OrderBy(n => n).ToArray();
            rc.SocketNames = names;
            rc.Sockets = names.Select(n => Find(root, n)).ToArray();
        }

        private static void FillFace(RiggedCharacter rc, JObject ch, Mesh mesh)
        {
            var face = (JObject)ch["face"];
            var channels = (JObject)face["shape_key_channels"];
            rc.ShapeKeys = new string[mesh.blendShapeCount];
            rc.ShapeChannels = new string[mesh.blendShapeCount];
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                rc.ShapeKeys[i] = mesh.GetBlendShapeName(i);
                rc.ShapeChannels[i] = (string)channels?[rc.ShapeKeys[i]] ?? "mouth";
            }
            var ex = new List<RigExpression>();
            foreach (var kv in (JObject)face["expressions"])
            {
                var e = (JObject)kv.Value;
                var keys = (JObject)e["keys"];
                ex.Add(new RigExpression
                {
                    Name = kv.Key,
                    Keys = keys.Properties().Select(p => p.Name).ToArray(),
                    Weights = keys.Properties().Select(p => (float)p.Value).ToArray(),
                    JawDeg = (float?)e["jaw_deg"] ?? 0f,
                    BlinkRate = (float?)e["blink_rate"] ?? 1f,
                    GazeDown = (string)e["gaze"] == "down",
                });
            }
            rc.Expressions = ex.ToArray();
            var t = (JObject)face["tuning"];
            var ft = new RigFaceTuning();
            if (t != null)
            {
                var bl = (JObject)t["blink"]; var g = (JObject)t["gain"]; var gz = (JObject)t["gaze"]; var tk = (JObject)t["talk"];
                if (bl != null)
                {
                    ft.BlinkClose = (float?)bl["close_s"] ?? ft.BlinkClose; ft.BlinkHold = (float?)bl["hold_s"] ?? ft.BlinkHold;
                    ft.BlinkOpen = (float?)bl["open_s"] ?? ft.BlinkOpen; ft.DoubleBlink = (float?)bl["double_chance"] ?? ft.DoubleBlink;
                    if (bl["interval_s"] is JArray iv) { ft.BlinkMin = (float)iv[0]; ft.BlinkMax = (float)iv[1]; }
                }
                if (g != null)
                {
                    ft.GainBlink = (float?)g["blink"] ?? 1f; ft.GainBrow = (float?)g["brow"] ?? 1f;
                    ft.GainJaw = (float?)g["jaw"] ?? 1f; ft.GainMouth = (float?)g["mouth"] ?? 1f;
                }
                if (gz != null)
                {
                    ft.EyeYaw = (float?)gz["eye_yaw_limit_deg"] ?? ft.EyeYaw; ft.EyePitch = (float?)gz["eye_pitch_limit_deg"] ?? ft.EyePitch;
                    ft.HeadYaw = (float?)gz["head_yaw_limit_deg"] ?? ft.HeadYaw; ft.HeadPitch = (float?)gz["head_pitch_limit_deg"] ?? ft.HeadPitch;
                    ft.HeadShare = (float?)gz["head_share"] ?? ft.HeadShare; ft.ChestShare = (float?)gz["chest_share"] ?? ft.ChestShare;
                    ft.EyeLead = (float?)gz["eye_lead_s"] ?? ft.EyeLead; ft.HeadLag = (float?)gz["head_lag_s"] ?? ft.HeadLag;
                    ft.Saccade = (float?)gz["saccade_s"] ?? ft.Saccade;
                }
                if (tk != null)
                {
                    ft.TalkKey = (string)tk["key"] ?? ft.TalkKey; ft.TalkRate = (float?)tk["rate_hz"] ?? ft.TalkRate;
                    if (tk["weight"] is JArray w) { ft.TalkWeightMin = (float)w[0]; ft.TalkWeightMax = (float)w[1]; }
                    if (tk["jaw_deg"] is JArray j) { ft.TalkJawMin = (float)j[0]; ft.TalkJawMax = (float)j[1]; }
                }
            }
            rc.Tuning = ft;
        }

        /// <summary>Base clip set from anim_manifest.json; the character's own "Name@key" variant replaces a base clip when it exists.</summary>
        private static List<string> FillClips(RiggedCharacter rc, string key, Dictionary<string, AnimationClip> clips)
        {
            var records = CharacterImport.ClipRecords();
            var anim = JObject.Parse(File.ReadAllText(CharacterPaths.AnimManifest));
            var list = new List<RigClip>();
            var missing = new List<string>();
            foreach (JObject baseRec in (JArray)anim["clips"])
            {
                string name = (string)baseRec["name"];
                string variant = name + "@" + key;
                bool hasVariant = clips.ContainsKey(variant) && records.ContainsKey(variant);
                var rec = hasVariant ? records[variant] : baseRec;
                if (!clips.TryGetValue(hasVariant ? variant : name, out var clip)) { missing.Add(name); continue; }
                var rcd = new RigClip
                {
                    Name = name, Clip = clip,
                    Loop = (bool?)rec["loop"] == true, Additive = (bool?)rec["additive"] == true,
                    Length = (float?)rec["length_s"] ?? clip.length, NativeSpeed = (float?)rec["native_speed_mps"] ?? 0f,
                    ExitPhase = (float?)rec["exit_phase"] ?? -1f, EnterPhase = (float?)rec["enter_phase"] ?? -1f,
                };
                // Root curves: the variant's when it has one, else the base clip's.
                var rcurve = (rec["root_curve"] ?? baseRec["root_curve"]) as JObject;
                if (rcurve != null)
                {
                    if (rcurve["dist_m"] is JArray dm) rcd.RootDist = dm.Select(x => (float)x).ToArray();
                    if (rcurve["yaw_deg"] is JArray yd) rcd.RootYaw = yd.Select(x => (float)x).ToArray();
                }
                float fps = (float?)rec["fps"] ?? 30f;
                if (rec["hold_frames"] is JArray hf) rcd.Hold = new Vector2((float)hf[0] / fps, (float)hf[1] / fps);
                if (rec["events"] is JArray ev)
                {
                    rcd.EventNames = ev.Select(e => (string)e["name"]).ToArray();
                    rcd.EventTimes = ev.Select(e => ((float?)e["frame"] ?? 0f) / fps).ToArray();
                }
                list.Add(rcd);
            }
            rc.Clips = list.ToArray();
            return missing;
        }

        // ---------------- merged mesh ----------------

        /// <summary>
        /// One skinned mesh from every part and overlay: same bones and bind poses, one submesh per role slot (in
        /// <see cref="SlotOrder"/>), pieces on Role_hidden dropped, the Head's blend shapes carried over.
        /// </summary>
        private static Mesh MergeMesh(string key, string costume, SkinnedMeshRenderer[] smrs, out List<string> slots, out int tris)
        {
            var refSmr = smrs.First(s => s.name == CharacterImport.MeshPrefix + "Body");
            var refBones = refSmr.bones.Select(b => b.name).ToArray();
            var refBind = refSmr.sharedMesh.bindposes;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var bySlot = new Dictionary<string, List<int>>();
            var shapes = new List<(string name, float w, int offset, Vector3[] dv)>();
            foreach (var s in smrs)
            {
                var m = s.sharedMesh;
                if (m == null) continue;
                int offset = verts.Count;
                var remap = s.bones.Select(b => Array.IndexOf(refBones, b.name)).ToArray();
                if (remap.Any(i => i < 0)) throw new Exception(s.name + " has a bone outside Body's bone list");
                var bind = m.bindposes;
                for (int i = 0; i < bind.Length; i++)
                    if (!Approximately(bind[i], refBind[remap[i]])) throw new Exception(s.name + " bind pose differs from Body's for " + s.bones[i].name);
                if (s.transform.localPosition.sqrMagnitude > 1e-8f || Quaternion.Angle(s.transform.localRotation, Quaternion.identity) > 0.01f)
                    throw new Exception(s.name + " is not at the model origin");
                verts.AddRange(m.vertices); norms.AddRange(m.normals);
                var uv = m.uv; uvs.AddRange(uv.Length == m.vertexCount ? uv : new Vector2[m.vertexCount]);
                foreach (var bw in m.boneWeights)
                {
                    var w = bw;
                    w.boneIndex0 = remap[w.boneIndex0]; w.boneIndex1 = remap[w.boneIndex1];
                    w.boneIndex2 = remap[w.boneIndex2]; w.boneIndex3 = remap[w.boneIndex3];
                    weights.Add(w);
                }
                var smats = s.sharedMaterials;
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    string slot = sm < smats.Length && smats[sm] != null ? smats[sm].name.Replace("Role_", "") : "hidden";
                    int sp = slot.IndexOf(' '); if (sp > 0) slot = slot.Substring(0, sp);
                    if (slot == "hidden") continue;
                    if (!bySlot.TryGetValue(slot, out var list)) bySlot[slot] = list = new List<int>();
                    foreach (var idx in m.GetTriangles(sm)) list.Add(idx + offset);
                }
                for (int b = 0; b < m.blendShapeCount; b++)
                {
                    var dv = new Vector3[m.vertexCount];
                    m.GetBlendShapeFrameVertices(b, 0, dv, null, null);
                    shapes.Add((m.GetBlendShapeName(b), m.GetBlendShapeFrameWeight(b, 0), offset, dv));
                }
            }
            slots = SlotOrder.Where(bySlot.ContainsKey).Concat(bySlot.Keys.Where(k => !SlotOrder.Contains(k))).ToList();
            var mesh = new Mesh { name = key + "@" + costume };
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = refBind;
            mesh.subMeshCount = slots.Count;
            tris = 0;
            for (int i = 0; i < slots.Count; i++) { mesh.SetTriangles(bySlot[slots[i]], i); tris += bySlot[slots[i]].Count / 3; }
            foreach (var sh in shapes)
            {
                var dv = new Vector3[verts.Count];
                Array.Copy(sh.dv, 0, dv, sh.offset, sh.dv.Length);
                mesh.AddBlendShapeFrame(sh.name, sh.w, dv, null, null);
            }
            mesh.RecalculateBounds();
            string path = CharacterPaths.Meshes + "/" + key + "@" + costume + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(mesh, old); return old; }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > 1e-4f) return false;
            return true;
        }
    }
}
