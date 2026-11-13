using System;
using UnityEngine;
using World.Sim.Encounters.Rhythm;

namespace World.Game
{
    /// <summary>The three rhythm stems: the owner's Lv1 Warehouse loops from Resources, or the synthesised fallback.</summary>
    public sealed class StemSet
    {
        public const string DefaultDir = "Rhythm/lv1_warehouse";
        public AudioClip Drums, Bass, Other;
        /// <summary>True when the Resources clips were missing and <see cref="SynthAudio"/> made the stems.</summary>
        public bool IsSynth;
        public string Source;

        /// <summary>Loads drums/bass/other from Resources/&lt;dir&gt;; falls back to synthesised loops matching the chart.</summary>
        public static StemSet Load(string dir = DefaultDir, Chart chartForFallback = null)
        {
            var d = Resources.Load<AudioClip>(dir + "/drums");
            var b = Resources.Load<AudioClip>(dir + "/bass");
            var o = Resources.Load<AudioClip>(dir + "/other");
            if (d != null && b != null)
            {
                foreach (var c in new[] { d, b, o })
                    if (c != null && c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
                return new StemSet { Drums = d, Bass = b, Other = o, IsSynth = false, Source = "Resources/" + dir };
            }
            return Synth(chartForFallback ?? FallbackChart.Create());
        }

        public static StemSet Synth(Chart chart)
        {
            return new StemSet
            {
                Drums = SynthAudio.DrumLoop(chart), Bass = SynthAudio.BassLoop(chart), Other = SynthAudio.PadLoop(),
                IsSynth = true, Source = "SynthAudio (fallback)",
            };
        }
    }

    /// <summary>
    /// Plays the rhythm stems on the audio DSP clock. All drums start on one scheduled dspTime (song time 0); bass and
    /// "other" start together, from their loop start, on the bass-reveal downbeat (PhraseSession's bass origin). Stems
    /// loop for the whole phrase and stop sample-accurately at its end. Song time = dspTime - start, smoothed between
    /// DSP buffer updates with the real-time clock (falls back to real time when the audio clock does not run).
    /// Gains follow targets with linear ramps of <see cref="RampSeconds"/>. Count-in clicks are scheduled too.
    /// </summary>
    public sealed class StemPlayer : MonoBehaviour
    {
        public const float RampSeconds = 0.35f;
        /// <summary>Scheduling margin so the first sample is never late.</summary>
        public const double ScheduleMargin = 0.15;
        public const float StemVolume = 0.9f;

        public StemSet Stems { get; private set; }
        public AudioSource Drums { get; private set; }
        public AudioSource Bass { get; private set; }
        public AudioSource Other { get; private set; }

        /// <summary>dspTime of song time 0 (beat 0). NaN before Schedule.</summary>
        public double StartDsp { get; private set; } = double.NaN;
        public double DrumsStartDsp { get; private set; } = double.NaN;
        public double BassStartDsp { get; private set; } = double.NaN;
        public double OtherStartDsp { get; private set; } = double.NaN;
        public double EndDsp { get; private set; } = double.NaN;
        public bool IsScheduled => !double.IsNaN(StartDsp);
        /// <summary>True when the audio DSP clock did not advance and song time follows the real-time clock instead.</summary>
        public bool UsingRealtimeFallback { get; private set; }
        public float TargetDrums { get; private set; }
        public float TargetBass { get; private set; }
        public float TargetOther { get; private set; }
        public int ClicksScheduled { get; private set; }

        private AudioSource[] _clicks = new AudioSource[0];
        private double _lastDsp = -1, _realAtDsp, _lastSong = double.NegativeInfinity;
        private double _fallbackOffset; // realtime - dsp when the fallback engaged
        private double _dspStallSince = -1;

        public static StemPlayer Create(Transform parent, StemSet stems)
        {
            var go = new GameObject("StemPlayer");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<StemPlayer>();
            p.Stems = stems;
            p.Drums = p.Source("Drums", stems.Drums);
            p.Bass = p.Source("Bass", stems.Bass);
            p.Other = p.Source("Other", stems.Other);
            return p;
        }

        private AudioSource Source(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 0;
            s.bypassReverbZones = true;
            return s;
        }

        /// <summary>Real-time seconds of DSP time now (the raw clock, not smoothed).</summary>
        public static double DspNow => AudioSettings.dspTime;

        /// <summary>
        /// Schedules a phrase: song time 0 at dspTime + leadIn (+ margin), drums from there, stop at phraseSeconds,
        /// and one click per given count-in song time (negative). Returns the start dspTime.
        /// </summary>
        public double Schedule(double leadInSeconds, double phraseSeconds, double[] clickSongTimes)
        {
            StopAll();
            double now = ClockDsp();
            StartDsp = now + leadInSeconds + ScheduleMargin;
            EndDsp = StartDsp + phraseSeconds;
            DrumsStartDsp = StartDsp;
            BassStartDsp = OtherStartDsp = double.NaN;
            _lastSong = double.NegativeInfinity;
            if (Drums.clip != null && AudioGuard.CanWriteControls)
            {
                Drums.volume = TargetDrums;
                Drums.PlayScheduled(DrumsStartDsp);
                Drums.SetScheduledEndTime(EndDsp);
            }
            ClicksScheduled = 0;
            if (clickSongTimes != null && AudioGuard.CanWriteControls)
            {
                if (_clicks.Length < clickSongTimes.Length)
                {
                    var hi = SynthAudio.Click(true); var lo = SynthAudio.Click(false);
                    var arr = new AudioSource[clickSongTimes.Length];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        arr[i] = i < _clicks.Length ? _clicks[i] : Source("Click" + i, null);
                        arr[i].loop = false;
                        arr[i].clip = i == 0 ? hi : lo;
                        arr[i].volume = 0.55f;
                    }
                    _clicks = arr;
                }
                for (int i = 0; i < clickSongTimes.Length; i++)
                {
                    _clicks[i].PlayScheduled(StartDsp + clickSongTimes[i]);
                    ClicksScheduled++;
                }
            }
            return StartDsp;
        }

        /// <summary>Starts bass and other together from their loop start at the given song time (the bass-reveal downbeat).</summary>
        public void ScheduleBassAndOther(double songTime)
        {
            if (!IsScheduled || !double.IsNaN(BassStartDsp)) return;
            double at = StartDsp + songTime;
            double now = ClockDsp();
            if (at < now + 0.01) at = now + 0.01; // too late to be exact: start as soon as possible (logged)
            if (at != StartDsp + songTime) Debug.LogWarning("[StemPlayer] bass scheduled late by " + ((at - StartDsp - songTime) * 1000).ToString("0.0") + " ms");
            BassStartDsp = OtherStartDsp = at;
            if (!AudioGuard.CanWriteControls) return;
            if (Bass.clip != null) { Bass.PlayScheduled(at); Bass.SetScheduledEndTime(EndDsp); }
            if (Other.clip != null) { Other.PlayScheduled(at); Other.SetScheduledEndTime(EndDsp); }
        }

        /// <summary>Song time in seconds now (negative during the count-in). NaN before Schedule. Monotonic.</summary>
        public double SongTime
        {
            get
            {
                if (!IsScheduled) return double.NaN;
                double t = SmoothedDsp() - StartDsp;
                if (t < _lastSong) t = _lastSong;
                _lastSong = t;
                return t;
            }
        }

        public void SetTargets(double drums, double bass, double other)
        {
            TargetDrums = (float)drums * StemVolume;
            TargetBass = (float)bass * StemVolume;
            TargetOther = (float)other * StemVolume;
        }

        /// <summary>Sets the current volumes to the targets at once (phrase start).</summary>
        public void SnapToTargets()
        {
            if (!AudioGuard.CanWriteControls) return;
            SetVolume(Drums, TargetDrums); SetVolume(Bass, TargetBass); SetVolume(Other, TargetOther);
        }

        // Audio controls are written only when they change and only while the output is healthy (see AudioGuard).
        private static void SetVolume(AudioSource s, float v)
        {
            if (s != null && s.volume != v) s.volume = v;
        }

        public void StopAll()
        {
            if (AudioGuard.CanWriteControls)
            {
                foreach (var s in new[] { Drums, Bass, Other }) if (s != null && s.isPlaying) s.Stop();
                foreach (var c in _clicks) if (c != null && c.isPlaying) c.Stop();
            }
            StartDsp = DrumsStartDsp = BassStartDsp = OtherStartDsp = EndDsp = double.NaN;
        }

        public bool AnyPlaying
        {
            get
            {
                foreach (var s in new[] { Drums, Bass, Other }) if (s != null && s.isPlaying) return true;
                return false;
            }
        }

        private void Update()
        {
            SmoothedDsp();
            if (!AudioGuard.CanWriteControls) return;
            float step = Time.unscaledDeltaTime / RampSeconds * StemVolume;
            SetVolume(Drums, Mathf.MoveTowards(Drums.volume, TargetDrums, step));
            SetVolume(Bass, Mathf.MoveTowards(Bass.volume, TargetBass, step));
            SetVolume(Other, Mathf.MoveTowards(Other.volume, TargetOther, step));
        }

        private void OnDestroy() => StopAll();

        // The clock: dspTime, extrapolated with real time between DSP buffer updates.
        private double ClockDsp()
        {
            return UsingRealtimeFallback ? Time.realtimeSinceStartupAsDouble - _fallbackOffset : AudioSettings.dspTime;
        }

        private double SmoothedDsp()
        {
            double real = Time.realtimeSinceStartupAsDouble;
            if (UsingRealtimeFallback) return real - _fallbackOffset;
            double dsp = AudioSettings.dspTime;
            if (dsp != _lastDsp)
            {
                _lastDsp = dsp; _realAtDsp = real; _dspStallSince = -1;
                return dsp;
            }
            if (_dspStallSince < 0) _dspStallSince = real;
            else if (real - _dspStallSince > 0.75)
            {
                // No audio device (or audio disabled): follow real time from here, keeping the clock continuous.
                UsingRealtimeFallback = true;
                _fallbackOffset = real - dsp;
                Debug.LogWarning("[StemPlayer] audio DSP clock is not advancing; song time follows the real-time clock");
                return dsp;
            }
            double ext = real - _realAtDsp;
            return dsp + Math.Min(ext, 0.1); // never run more than 100 ms ahead of the last DSP update
        }
    }
}
