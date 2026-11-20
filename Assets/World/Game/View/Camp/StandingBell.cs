using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// P2a: the tuning bell as a standing prop at a dance circle (the home camp's and every crystal spire's), so the tap-along
    /// can be done outside a dance (the owner's test 5.4 found the bell only on the result screen). Tap it: a ledger tablet
    /// shows the kept offset and asks "Tune your drum timing?"; tap again and eight clicks sound half a second apart; tap along
    /// anywhere (a click, a touch, D F J K or Space). The median offset is kept in the same PlayerPrefs key as the dance's own
    /// bell and as the [ and ] keys, so every later dance uses it. Presentation only: no sim command is sent. While a dance
    /// runs its own bell stands in the same place, so this one hides and cannot be tapped.
    /// </summary>
    public sealed class StandingBell : MonoBehaviour, IWorldAction, IWorldActionAnchor
    {
        public DanceCircle Circle { get; private set; }
        public Transform Anchor => transform;
        public float KeyReach => 5f;
        public string Title => "The tuning bell";
        public string PreviewLine => "Tune your drum timing?";
        public IEnumerable<string> Lines
        {
            get
            {
                yield return IsTuning ? "Tap along with the clicks: " + (_tune != null ? _tune.Matched : 0) + " of " + TapAlongCalibration.ClickCount + "." : "Eight clicks, half a second apart. Tap along anywhere.";
                yield return "Your offset is " + RhythmPartyEncounter.SignedMs(OffsetMs) + (_lastMs.HasValue ? " (just kept)." : " (kept).");
            }
        }

        /// <summary>The kept offset in ms (the dance reads the same key).</summary>
        public static double OffsetMs => PlayerPrefs.GetFloat(RhythmPartyEncounter.CalibrationPref, 0f);

        public bool IsTuning => _tune != null;
        public int Rings { get; private set; }
        /// <summary>True while the bell is shown and can be tapped (hidden while a dance runs or when the circle is retired).</summary>
        public bool Available { get; private set; }

        private TapAlongCalibration _tune;
        private readonly List<AudioSource> _clicks = new List<AudioSource>();
        private Transform _swing;
        private float _swingV, _swingA;
        private GameObject _visual;
        private Light _lamp;
        private double? _lastMs;
        private bool _inputWas;

        public static StandingBell Create(DanceCircle circle)
        {
            if (circle == null) return null;
            var go = new GameObject("Standing tuning chime");
            go.transform.SetParent(circle.transform, false);
            go.transform.position = circle.Layout.CirclePoint(34f, 5.9f);
            var b = go.AddComponent<StandingBell>();
            b.Circle = circle;
            b.BuildVisual();
            WorldZone.Tag(go, WorldZones.RhyTune);
            return b;
        }

        private static GameObject Prim(PrimitiveType t, string name, Transform parent, Color c, Vector3 pos, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Shared(c);
            return g;
        }

        private void BuildVisual()
        {
            _visual = new GameObject("Chime visual"); _visual.transform.SetParent(transform, false);
            var v = _visual.transform;
            Prim(PrimitiveType.Cylinder, "Post", v, new Color(0.46f, 0.34f, 0.23f), new Vector3(0, 1.0f, 0), new Vector3(0.14f, 1.0f, 0.14f));
            Prim(PrimitiveType.Cube, "Beam", v, new Color(0.46f, 0.34f, 0.23f), new Vector3(0, 1.95f, 0), new Vector3(0.9f, 0.1f, 0.1f));
            _swing = new GameObject("Chime swing").transform; _swing.SetParent(v, false); _swing.localPosition = new Vector3(0, 1.9f, 0);
            Prim(PrimitiveType.Sphere, "Chime", _swing, new Color(0.86f, 0.62f, 0.25f), new Vector3(0, -0.28f, 0), new Vector3(0.45f, 0.5f, 0.45f));
            Prim(PrimitiveType.Sphere, "Clapper", _swing, new Color(0.35f, 0.25f, 0.15f), new Vector3(0, -0.55f, 0), new Vector3(0.12f, 0.12f, 0.12f));
            // a small lantern on the beam so the bell is found at night
            var lamp = Prim(PrimitiveType.Sphere, "Lantern", v, Color.white, new Vector3(0.38f, 1.72f, 0), new Vector3(0.14f, 0.14f, 0.14f));
            lamp.GetComponent<Renderer>().sharedMaterial = MaterialFactory.SharedUnlit(new Color(1f, 0.78f, 0.4f));
            var lg = new GameObject("Lantern light"); lg.transform.SetParent(v, false); lg.transform.localPosition = new Vector3(0.38f, 1.8f, 0);
            _lamp = lg.AddComponent<Light>(); _lamp.type = LightType.Point; _lamp.range = 5.5f; _lamp.intensity = 1.1f; _lamp.color = new Color(1f, 0.75f, 0.4f); _lamp.shadows = LightShadows.None;
            // the tap volume: a box round post and bell, bigger than the drawing (at least the shared hit size is enforced by WorldActions' screen bounds)
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.2f, 0f); box.size = new Vector3(1.6f, 2.8f, 1.6f);
        }

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() { WorldActions.Unregister(this); CancelTune(); }

        public WorldActionCheck Check()
        {
            if (!Available) return WorldActionCheck.Disable("Not now.");
            if (IsTuning) return WorldActionCheck.Disable("Tap along with the clicks.");
            return WorldActionCheck.Allowed;
        }

        public void ShowPreview(bool on) { }

        public bool Commit() { return StartTune(); }

        /// <summary>Starts the eight clicks (the same tap-along the dance's bell uses).</summary>
        public bool StartTune()
        {
            if (IsTuning || !Available) return false;
            Ring();
            double first = AudioSettings.dspTime + 1.2;
            _tune = new TapAlongCalibration(first);
            _lastMs = null;
            var hi = SynthAudio.Click(true); var lo = SynthAudio.Click(false);
            while (_clicks.Count < TapAlongCalibration.ClickCount)
            {
                var go = new GameObject("Tune click " + _clicks.Count); go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>(); a.spatialBlend = 0f; a.volume = 0.6f; a.playOnAwake = false; _clicks.Add(a);
            }
            if (AudioGuard.CanWriteControls)
                for (int i = 0; i < TapAlongCalibration.ClickCount; i++) { _clicks[i].clip = i % 4 == 0 ? hi : lo; _clicks[i].PlayScheduled(_tune.ClickTimes[i]); }
            // taps belong to the tap-along now: the world must not walk or pick things meanwhile
            _inputWas = PointerInput.InputEnabled; PointerInput.InputEnabled = false;
            Refresh();
            return true;
        }

        /// <summary>A tap-along tap at a dsp time (tests inject theirs).</summary>
        public bool TuneTap(double dspTime)
        {
            if (_tune == null) return false;
            bool ok = _tune.Tap(dspTime);
            if (ok) Ring();
            Refresh();
            return ok;
        }

        /// <summary>The click times of the running tune (tests).</summary>
        public double[] ClickTimes => _tune != null ? _tune.ClickTimes : null;

        /// <summary>Ends a running tune at once as if its last click had passed (tests).</summary>
        public void FinishNow() { if (_tune != null) FinishTune(); }

        private void CancelTune()
        {
            if (_tune == null) return;
            _tune = null;
            foreach (var c in _clicks) if (c != null) c.Stop();
            PointerInput.InputEnabled = _inputWas;
        }

        private void FinishTune()
        {
            var t = _tune; _tune = null;
            PointerInput.InputEnabled = _inputWas;
            var c = new LedgerContent { Title = Title };
            if (t.TryResult(out double ms))
            {
                PlayerPrefs.SetFloat(RhythmPartyEncounter.CalibrationPref, (float)ms); PlayerPrefs.Save();
                _lastMs = ms;
                c.Lines.Add("Your drum runs " + RhythmPartyEncounter.SignedMs(ms) + " off the beat. Kept for every dance.");
            }
            else c.Lines.Add("I only heard " + t.Matched + " taps. Tap the bell and try again.");
            LedgerCard.Show(c, transform, LedgerMode.Inspect, this);
        }

        public void Ring() { _swingV = 7f; Rings++; }

        private void Refresh()
        {
            if (!IsTuning) return;
            var c = new LedgerContent { Title = Title };
            foreach (var l in Lines) c.Lines.Add(l);
            LedgerCard.Show(c, transform, LedgerMode.Inspect, this);
        }

        private void Update()
        {
            bool dance = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            bool hide = dance || (Circle != null && Circle.Retired);
            Available = !hide;
            if (_visual != null && _visual.activeSelf == hide) _visual.SetActive(!hide);
            var col = GetComponent<Collider>(); if (col != null) col.enabled = !hide;
            if (hide) { CancelTune(); return; }
            float dt = Time.unscaledDeltaTime;
            _swingA += _swingV * dt; _swingV += (-_swingA * 60f - _swingV * 2.2f) * dt;
            if (_swing != null) _swing.localRotation = Quaternion.Euler(0, 0, _swingA * Mathf.Rad2Deg * 0.15f);
            if (_tune == null) return;
            double now = AudioSettings.dspTime;
            bool tap = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
            if (Input.touchCount > 0) for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) tap = true;
            foreach (var k in new[] { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K }) if (Input.GetKeyDown(k)) tap = true;
            if (tap) TuneTap(now);
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelTune(); LedgerCard.Hide(); return; }
            if (_tune != null && _tune.IsOver(now)) FinishTune();
        }
    }
}
