using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Watches the audio output so game code can stop touching audio controls when the device is gone.
    /// Background (M2): when Windows' default audio device changes or fails, FMOD falls back to a suspended/emulated
    /// output ("FMOD failed to switch back to normal output, audio output stays suspended") and Unity mixes without its
    /// realtime thread; a later control update (filter parameter, volume, Play) during a mix then raises the fatal
    /// "ADTM: Attempting to update control while a mix is ongoing, but realtime thread is disabled" dialog.
    /// Rule for World.Game: write audio parameters only when <see cref="CanWriteControls"/> and only when the value changes.
    /// </summary>
    public static class AudioGuard
    {
        /// <summary>Seconds without DSP clock progress before the output counts as stalled.</summary>
        public const float StallSeconds = 0.35f;
        /// <summary>Seconds of steady DSP progress after a device change before audio counts as recovered.</summary>
        public const float RecoverSeconds = 1.5f;

        public static bool OutputStalled { get; private set; }
        public static bool DeviceChanged { get; private set; }
        public static int DeviceChangeCount { get; private set; }

        /// <summary>True when it is safe to change audio state (volumes, filter parameters, Play/Stop/PlayScheduled).</summary>
        public static bool CanWriteControls => !OutputStalled && !DeviceChanged && !AudioListener.pause && Driver.Alive;

        private static double _lastDsp = -1;
        private static float _lastAdvance, _steadySince = -1;
        private static bool _warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OutputStalled = false; DeviceChanged = false; DeviceChangeCount = 0;
            _lastDsp = -1; _lastAdvance = 0; _steadySince = -1; _warned = false;
            AudioSettings.OnAudioConfigurationChanged -= OnConfigChanged;
            AudioSettings.OnAudioConfigurationChanged += OnConfigChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Driver.Instance != null) return;
            var go = new GameObject("AudioGuard") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            Driver.Instance = go.AddComponent<Driver>();
        }

        private static void OnConfigChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged) return;
            DeviceChanged = true;
            DeviceChangeCount++;
            _steadySince = -1;
            Debug.LogWarning("[AudioGuard] audio device changed: game audio controls are frozen until the output runs steadily again");
        }

        internal static void Sample()
        {
            double dsp = AudioSettings.dspTime;
            float now = Time.realtimeSinceStartup;
            if (dsp != _lastDsp)
            {
                _lastDsp = dsp; _lastAdvance = now;
                if (OutputStalled) { OutputStalled = false; Debug.Log("[AudioGuard] audio output running again"); }
                if (DeviceChanged)
                {
                    if (_steadySince < 0) _steadySince = now;
                    else if (now - _steadySince > RecoverSeconds) { DeviceChanged = false; _steadySince = -1; }
                }
            }
            else if (_lastDsp >= 0 && now - _lastAdvance > StallSeconds && !OutputStalled)
            {
                OutputStalled = true;
                _steadySince = -1;
                if (!_warned) { _warned = true; Debug.LogWarning("[AudioGuard] audio DSP clock stalled (no output device?): game audio controls are frozen"); }
            }
        }

        /// <summary>Samples the DSP clock early in every frame.</summary>
        [DefaultExecutionOrder(-10000)]
        internal sealed class Driver : MonoBehaviour
        {
            internal static Driver Instance;
            internal static bool Alive => Instance != null;
            private void Update() => Sample();
            private void OnDestroy() { if (Instance == this) Instance = null; }
        }
    }
}
