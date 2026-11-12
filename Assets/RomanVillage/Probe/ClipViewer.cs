using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bedsit;
using Bedsit.Sims2;
using OpenTS2.Content;
using UnityEngine;

namespace RomanVillage.Probe
{
    /// <summary>
    /// Player-friendly viewer for the M0b clip spike: cycles through the candidate original Sims 2 animations (Resources/probe_candidates.txt)
    /// on a dressed Sim with an on-screen label. Reads the Sims 2 install at run time (config.json next to the exe); no EA content is in the build.
    /// Keys: Left/Right = previous/next, Space = auto-advance on/off, C = camera, F12 = screenshot, Esc = quit.  "-selftest" = run the first clips and quit.
    /// </summary>
    public class ClipViewer : MonoBehaviour
    {
        const int SelfTestClips = 14;

        SimCharacterActor _actor;
        Camera _cam;
        readonly List<string> _names = new List<string>();
        string _error, _label = "Starting...", _status = "";
        bool _auto = true, _skip, _selfTest;
        int _jump, _camMode;

        IEnumerator Start()
        {
            _selfTest = Environment.GetCommandLineArgs().Contains("-selftest");

            if (!Sims2Runtime.Initialise())
            {
                _error = Sims2Runtime.LastError ?? "initialisation failed";
                Debug.LogError("[Viewer] " + _error);
                yield break;
            }
            MountExpansions();

            var ta = Resources.Load<TextAsset>("probe_candidates");
            if (ta != null)
                _names.AddRange(ta.text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).Distinct());
            if (_names.Count == 0) { _error = "no clip list found (Resources/probe_candidates)"; yield break; }

            // Register every clip BEFORE creating the actor (it registers all IK chains up front; rebuilding mid-play can crash).
            foreach (var n in _names)
                SimAnimationLibrary.Definitions["p:" + n] = new SimAnimationLibrary.Def { Key = "p:" + n, Candidates = new[] { n }, Loop = false, Note = "viewer" };

            BuildStage();
            _actor = SimCharacterActor.CreateFromNeighborhood("viewer", "N001", 12) ?? SimCharacterActor.CreateNakedBase("viewer");
            if (_actor == null) { _error = "could not create a Sim (is the N001 Characters folder under user_dir?)"; yield break; }
            _actor.transform.position = new Vector3(0f, 0f, 0.8f);
            _actor.FaceTowards(new Vector3(0f, 0f, -10f));
            _actor.PlayIdle();
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[Viewer] ready: {_names.Count} clips, selftest={_selfTest}");

            int i = 0, played = 0;
            while (true)
            {
                yield return PlayClip(i);
                played++;
                if (_selfTest && played >= SelfTestClips)
                {
                    Debug.Log("[Viewer] selftest done");
                    Application.Quit();
                    yield break;
                }
                if (_jump != 0) { i = (i + _jump + _names.Count) % _names.Count; _jump = 0; }
                else if (_auto) i = (i + 1) % _names.Count;
                // auto off + no jump = replay the same clip
            }
        }

        static void MountExpansions()
        {
            int mounted = 0;
            foreach (ProductFlags pf in Enum.GetValues(typeof(ProductFlags)))
            {
                if (pf == ProductFlags.BaseGame) continue;
                try { Sims2Runtime.MountDirectory(pf, "TSData/Res/3D"); mounted++; } catch (Exception) { /* product not installed */ }
            }
            Debug.Log($"[Viewer] mounted {mounted} product 3D folders");
        }

        void BuildStage()
        {
            _cam = new GameObject("ViewerCamera").AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            _cam.fieldOfView = 40f;
            ApplyCamera();
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, 60f, 0f);
            light.intensity = 1.1f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            // A primitive's default material uses a shader that is stripped from player builds (renders magenta);
            // Sprites/Default is in the always-included list.
            var floorMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.42f, 0.44f, 0.40f) };
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        }

        void ApplyCamera()
        {
            var look = new Vector3(0f, 0.95f, 0.8f);
            Vector3 pos = _camMode == 0 ? new Vector3(5.2f, 1.25f, 0.8f)       // side-on: shows forward motion
                        : _camMode == 1 ? new Vector3(0f, 1.3f, -3.4f)          // front
                                        : new Vector3(3.0f, 1.5f, -2.2f);       // three-quarter
            _cam.transform.position = pos;
            _cam.transform.LookAt(look);
        }

        IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s && !_skip) { yield return null; t += Time.deltaTime; }
        }

        IEnumerator PlayClip(int i)
        {
            var name = _names[i];
            var key = "p:" + name;
            _skip = false;
            _label = $"[{i + 1}/{_names.Count}]  {name.Replace("_anim", "")}";
            var res = SimAnimationLibrary.Resolve(key);
            if (res == null)
            {
                _status = "MISSING: not found in the mounted Sims 2 packages";
                Debug.Log($"[Viewer] {i + 1} {name} MISSING");
                yield return Wait(1.0f);
                yield break;
            }
            float dur = res.DurationSeconds;
            _actor.ResetToIdle();
            yield return Wait(0.3f);

            if (dur < 0.05f)
            {
                _status = "static pose / arm overlay: needs a prop and a walk under it";
                Debug.Log($"[Viewer] {i + 1} {name} static");
                _actor.ScrubTo(key, 0f);
                MaybeShot(i);
                yield return Wait(2.5f);
                _actor.ResetToIdle();
                yield break;
            }

            _status = dur < 2.5f ? $"{dur:0.00}s cycle, looped (animation plays in place)" : $"{dur:0.0}s";
            Debug.Log($"[Viewer] {i + 1} {name} {dur:0.00}s");
            float target = dur < 2.5f ? 3.0f : Mathf.Min(dur, 8f);
            float t = 0f; bool shot = false;
            while (t < target && !_skip)
            {
                if (!_actor.IsPlayingOneShot) _actor.PlayOneShot(key);
                yield return null;
                t += Time.deltaTime;
                if (!shot && t > Mathf.Min(1.0f, target * 0.5f)) { MaybeShot(i); shot = true; }
            }
            _actor.ResetToIdle();
        }

        void MaybeShot(int i)
        {
            if (!_selfTest) return;
            try
            {
                ScreenshotUtil.Capture(_cam, $"viewer_selftest_{i + 1:D2}.png", 960, 540);
                // full window including the on-screen labels (written at the end of the frame)
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScreenshotUtil.EvidenceDir, $"viewer_window_{i + 1:D2}.png"));
            }
            catch (Exception e) { Debug.LogWarning("[Viewer] screenshot failed: " + e.Message); }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
            if (_error != null) return;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { _jump = 1; _skip = true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { _jump = -1; _skip = true; }
            if (Input.GetKeyDown(KeyCode.Space)) _auto = !_auto;
            if (Input.GetKeyDown(KeyCode.C) && _cam != null) { _camMode = (_camMode + 1) % 3; ApplyCamera(); }
            if (Input.GetKeyDown(KeyCode.F12) && _cam != null)
                Debug.Log("[Viewer] screenshot: " + ScreenshotUtil.Capture(_cam, $"viewer_{DateTime.Now:HHmmss}.png", 1280, 720));
        }

        static void Shadowed(Rect r, string text, GUIStyle s)
        {
            var c = s.normal.textColor;
            s.normal.textColor = new Color(0, 0, 0, 0.85f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, s);
            s.normal.textColor = c;
            GUI.Label(r, text, s);
        }

        void OnGUI()
        {
            int fs = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.034f));
            var s = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold, wordWrap = true };
            s.normal.textColor = Color.white;
            if (_error != null)
            {
                Shadowed(new Rect(24, 24, Screen.width - 48, Screen.height - 48),
                    "Could not start Sims 2 content:\n\n" + _error +
                    "\n\nCopy config.example.json to config.json next to this exe and set game_dir and user_dir (see README.txt).\n\nEsc quits.", s);
                return;
            }
            Shadowed(new Rect(24, 16, Screen.width - 48, fs * 1.6f), _label, s);
            s.fontStyle = FontStyle.Normal; s.fontSize = Mathf.RoundToInt(fs * 0.8f);
            Shadowed(new Rect(24, 16 + fs * 1.5f, Screen.width - 48, fs * 1.4f), _status, s);
            Shadowed(new Rect(24, Screen.height - fs * 1.8f, Screen.width - 48, fs * 1.6f),
                $"Left/Right: prev/next    Space: auto-advance {(_auto ? "ON" : "OFF (loops this clip)")}    C: camera    F12: screenshot    Esc: quit", s);
        }
    }
}
