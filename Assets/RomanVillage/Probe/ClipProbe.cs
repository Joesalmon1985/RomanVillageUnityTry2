using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bedsit;
using Bedsit.Sims2;
using OpenTS2.Content;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RomanVillage.Probe
{
    /// <summary>
    /// M0b spike. Plays each candidate original Sims 2 animation (names in LocalData/m0/probe_candidates.txt) on a dressed Sim and
    /// records: does it resolve, targets/channels/duration, root-motion stride (from the clip's root_trans translation curve and from
    /// the live bone), peak pose change, and a mid-play screenshot. Output: LocalData/m0/clip_probe.csv (+ clip_probe_done.txt).
    /// </summary>
    public class ClipProbe : MonoBehaviour
    {
        public float maxPlaySeconds = 3.5f;
        public uint simId = 12;

        sealed class Row
        {
            public int Idx; public string Name, Status = "OK", Targets = "", Note = "", Shot = "", RootBone = "";
            public float Dur, Dx, Dy, Dz, Horiz, Speed, LiveHoriz, MaxDelta, FootRange;
            public int Channels;
        }

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
        static string F(float f, string fmt = "0.000") => f.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        static string Sanitize(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

        IEnumerator Start()
        {
            var outDir = Path.Combine(Root, "LocalData", "m0");
            Directory.CreateDirectory(outDir);
            var donePath = Path.Combine(outDir, "clip_probe_done.txt");
            var csvPath = Path.Combine(outDir, "clip_probe.csv");
            if (File.Exists(donePath)) File.Delete(donePath);

            if (!Sims2Runtime.Initialise())
            {
                File.WriteAllText(donePath, "FAILED init: " + Sims2Runtime.LastError);
                yield break;
            }

            // The bedsit project only mounts the base game. Expansion/stuff packs hold many of the clips we want
            // (gardening, digging, greetings...): mount each product's TSData/Res/3D folder too.
            int mounted = 0; var mountNotes = new List<string>();
            foreach (ProductFlags pf in Enum.GetValues(typeof(ProductFlags)))
            {
                if (pf == ProductFlags.BaseGame) continue;
                try { Sims2Runtime.MountDirectory(pf, "TSData/Res/3D"); mounted++; }
                catch (Exception e) { mountNotes.Add(pf + ": " + e.GetType().Name); }
            }
            Debug.Log($"[ClipProbe] mounted {mounted} product 3D folders; skipped: {string.Join(", ", mountNotes)}");

            var names = File.ReadAllLines(Path.Combine(outDir, "probe_candidates.txt"))
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).Distinct().ToList();

            // Register every candidate BEFORE creating the actor: the actor registers all IK chains at creation
            // (rebuilding the animation graph mid-playback can crash the editor).
            foreach (var n in names)
                SimAnimationLibrary.Definitions["p:" + n] = new SimAnimationLibrary.Def { Key = "p:" + n, Candidates = new[] { n }, Loop = false, Note = "probe" };

            // stage: side-on camera so forward motion is visible
            var cam = new GameObject("ProbeCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            cam.fieldOfView = 40f;
            cam.transform.position = new Vector3(5.2f, 1.25f, 0f);
            cam.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, 60f, 0f);
            light.intensity = 1.1f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.4f);

            var actor = SimCharacterActor.CreateFromNeighborhood("probe", "N001", simId) ?? SimCharacterActor.CreateNakedBase("probe");
            if (actor == null)
            {
                File.WriteAllText(donePath, "FAILED: could not create a Sim");
                yield break;
            }
            actor.transform.position = new Vector3(0f, 0f, 0.8f);
            actor.FaceTowards(new Vector3(0f, 0f, -10f));   // faces -Z: the camera sees the profile
            actor.PlayIdle();
            yield return new WaitForSeconds(0.6f);

            Transform rootTrans = null;
            actor.Sim.Scenegraph.BoneNamesToTransform.TryGetValue("root_trans", out rootTrans);
            Transform footL = null, footR = null;
            actor.Sim.Scenegraph.BoneNamesToTransform.TryGetValue("l_foot", out footL);
            actor.Sim.Scenegraph.BoneNamesToTransform.TryGetValue("r_foot", out footR);

            File.WriteAllText(csvPath, "idx,name,status,duration_s,targets,auskel_channels,root_bone,root_dx,root_dy,root_dz,root_horiz_m,speed_mps,live_root_horiz_m,max_pose_delta_deg,foot_range_m,gait_speed_mps,screenshot,note\n");
            int idx = 0, ok = 0, missing = 0, failed = 0;
            foreach (var name in names)
            {
                var row = new Row { Idx = ++idx, Name = name };
                var key = "p:" + name;
                Analyse(row, key, actor);

                if (row.Status == "OK")
                {
                    Quiet(() => actor.ResetToIdle(), row);
                    yield return new WaitForSeconds(0.35f);
                    Quaternion[] rest = null; Vector3 p0 = Vector3.zero; bool started = false;
                    Quiet(() => { rest = actor.CapturePose(); p0 = rootTrans != null ? rootTrans.position : Vector3.zero; started = actor.PlayOneShot(key); }, row);
                    if (!started && row.Status == "OK") row.Status = "NOPLAY";

                    if (row.Status == "OK")
                    {
                        float playLen = Mathf.Min(Mathf.Max(row.Dur, 0.5f), maxPlaySeconds);
                        float cap = playLen + 0.3f, shotAt = playLen * 0.45f, t = 0f;
                        bool shotDone = false;
                        float minZL = float.MaxValue, maxZL = float.MinValue, minZR = float.MaxValue, maxZR = float.MinValue;
                        while (t < cap)
                        {
                            yield return null;
                            t += Time.deltaTime;
                            if (footL != null && footR != null && t < row.Dur + 0.05f)
                            {
                                // foot travel along the Sim's forward axis (actor-local Z): in an in-place gait cycle, the planted foot slides
                                // back by one stride per cycle, so range / cycle time is the ground speed that avoids foot sliding.
                                float zl = actor.transform.InverseTransformPoint(footL.position).z, zr = actor.transform.InverseTransformPoint(footR.position).z;
                                minZL = Mathf.Min(minZL, zl); maxZL = Mathf.Max(maxZL, zl); minZR = Mathf.Min(minZR, zr); maxZR = Mathf.Max(maxZR, zr);
                            }
                            Quiet(() => row.MaxDelta = Mathf.Max(row.MaxDelta, SimCharacterActor.MaxPoseDelta(rest, actor.CapturePose())), row);
                            if (!shotDone && t >= shotAt)
                            {
                                Quiet(() => { row.Shot = $"probe_{row.Idx:D3}_{Sanitize(name)}.png"; ScreenshotUtil.Capture(cam, row.Shot, 960, 540); }, row);
                                shotDone = true;
                            }
                            if (!actor.IsPlayingOneShot && t > 0.3f) break;
                        }
                        if (maxZL > minZL && maxZR > minZR) row.FootRange = Mathf.Max(maxZL - minZL, maxZR - minZR);
                        if (rootTrans != null)
                        {
                            var d = rootTrans.position - p0;
                            row.LiveHoriz = new Vector2(d.x, d.z).magnitude;
                        }
                    }
                }
                if (row.Status == "OK") ok++; else if (row.Status == "MISSING") missing++; else failed++;
                File.AppendAllText(csvPath, string.Join(",", row.Idx, Csv(row.Name), row.Status, F(row.Dur, "0.00"), Csv(row.Targets), row.Channels, Csv(row.RootBone),
                    F(row.Dx), F(row.Dy), F(row.Dz), F(row.Horiz), F(row.Speed), F(row.LiveHoriz), F(row.MaxDelta, "0.0"),
                    F(row.FootRange), F(row.Dur > 0.1f ? row.FootRange / row.Dur : 0f), Csv(row.Shot), Csv(row.Note)) + "\n");
            }
            Quiet(() => actor.ResetToIdle(), new Row());
            File.WriteAllText(donePath, $"DONE total={names.Count} ok={ok} missing={missing} failed={failed}");
            Debug.Log($"[ClipProbe] done: total={names.Count} ok={ok} missing={missing} failed={failed}");
        }

        static void Quiet(Action a, Row row)
        {
            try { a(); }
            catch (Exception e) { row.Status = "EXCEPTION"; row.Note += e.GetType().Name + ": " + e.Message.Replace('\n', ' ') + "; "; }
        }

        /// <summary>Data-only analysis of the resolved clip (no yields): targets, channels, duration and root-motion curve.</summary>
        static void Analyse(Row row, string key, SimCharacterActor actor)
        {
            Quiet(() =>
            {
                var res = SimAnimationLibrary.Resolve(key);
                if (res == null) { row.Status = "MISSING"; return; }
                var animTargets = res.Asset.AnimResource.AnimTargets;
                row.Targets = string.Join("|", animTargets.Select(t => t.TagName + ":" + t.Channels.Length));
                var sk = animTargets.FirstOrDefault(t => t.TagName.ToLower() == "auskel");
                row.Channels = sk != null ? sk.Channels.Length : 0;
                row.Dur = res.DurationSeconds;
                if (sk == null) row.Note += "no auskel target; ";
#if UNITY_EDITOR
                var clip = res.Asset.CreateClipFromResource(actor.Sim.Scenegraph.BoneNamesToRelativePaths, actor.Sim.Scenegraph.BlendNamesToRelativePaths, minimumDuration: 0f);
                if (clip == null) return;
                float len = clip.length > 0 ? clip.length : row.Dur;
                var byPath = new Dictionary<string, float[]>();
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!b.propertyName.StartsWith("m_LocalPosition.")) continue;
                    var c = AnimationUtility.GetEditorCurve(clip, b);
                    int axis = b.propertyName.EndsWith("x") ? 0 : b.propertyName.EndsWith("y") ? 1 : 2;
                    if (!byPath.TryGetValue(b.path, out var v)) byPath[b.path] = v = new float[3];
                    v[axis] = c.Evaluate(len) - c.Evaluate(0f);
                }
                string best = byPath.Keys.FirstOrDefault(p => p.EndsWith("root_trans"));
                if (best == null && byPath.Count > 0)
                    best = byPath.OrderByDescending(kv => new Vector2(kv.Value[0], kv.Value[2]).magnitude).First().Key;
                if (best != null)
                {
                    var v = byPath[best];
                    row.RootBone = best.Substring(best.LastIndexOf('/') + 1);
                    row.Dx = v[0]; row.Dy = v[1]; row.Dz = v[2];
                    row.Horiz = new Vector2(v[0], v[2]).magnitude;
                    row.Speed = len > 0 ? row.Horiz / len : 0f;
                }
                UnityEngine.Object.Destroy(clip);
#endif
            }, row);
        }
    }
}
