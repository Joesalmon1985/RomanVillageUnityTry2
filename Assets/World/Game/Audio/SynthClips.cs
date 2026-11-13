using UnityEngine;

namespace World.Game
{
    /// <summary>Original procedurally synthesised clips (no third-party audio).</summary>
    public static class SynthClips
    {
        public const int SampleRate = 44100;

        /// <summary>Two-bar 174 BPM drum loop: kick on 1 and the "and" of 3 (two-step), snare on 2 and 4, hats on eighths, sub bass.</summary>
        public static AudioClip DrumLoop174()
        {
            const double bpm = 174.0;
            double beat = 60.0 / bpm;
            int beats = 8;
            int n = (int)System.Math.Round(beat * beats * SampleRate);
            var data = new float[n];
            uint seed = 0x2468ACE1u;
            float Noise() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFF) / 32768f - 1f; }

            void Kick(double t0)
            {
                int s0 = (int)(t0 * SampleRate); double phase = 0;
                for (int i = 0; i < (int)(0.32 * SampleRate) && s0 + i < n; i++)
                {
                    double t = i / (double)SampleRate;
                    double f = 45 + 110 * System.Math.Exp(-t * 28);
                    phase += 2 * System.Math.PI * f / SampleRate;
                    data[s0 + i] += (float)(System.Math.Sin(phase) * System.Math.Exp(-t * 9) * 0.9);
                }
            }
            void Snare(double t0)
            {
                int s0 = (int)(t0 * SampleRate);
                for (int i = 0; i < (int)(0.22 * SampleRate) && s0 + i < n; i++)
                {
                    double t = i / (double)SampleRate;
                    double body = System.Math.Sin(2 * System.Math.PI * 190 * t) * System.Math.Exp(-t * 30) * 0.4;
                    data[s0 + i] += (float)(body + Noise() * System.Math.Exp(-t * 18) * 0.35);
                }
            }
            void Hat(double t0, float amp)
            {
                int s0 = (int)(t0 * SampleRate); float prev = 0;
                for (int i = 0; i < (int)(0.05 * SampleRate) && s0 + i < n; i++)
                {
                    double t = i / (double)SampleRate;
                    float x = Noise(); float hp = x - prev; prev = x;
                    data[s0 + i] += (float)(hp * System.Math.Exp(-t * 90) * amp);
                }
            }
            for (int bar = 0; bar < 2; bar++)
            {
                double b0 = bar * 4 * beat;
                Kick(b0); Kick(b0 + 2.5 * beat);
                Snare(b0 + beat); Snare(b0 + 3 * beat);
                for (int e = 0; e < 8; e++) Hat(b0 + e * beat * 0.5, e % 2 == 1 ? 0.22f : 0.12f);
            }
            // Sub bass following the kicks.
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double inBar = t % (4 * beat);
                double f = inBar < 2.5 * beat ? 55.0 : 41.2;
                double env = 0.5 + 0.5 * System.Math.Cos(2 * System.Math.PI * (t / beat));
                data[i] += (float)(System.Math.Sin(2 * System.Math.PI * f * t) * 0.18 * env);
            }
            Normalize(data, 0.85f);
            var clip = AudioClip.Create("Warehouse174", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A bronze bell strike (inharmonic partials, long decay).</summary>
        public static AudioClip Bell()
        {
            int n = (int)(2.8 * SampleRate);
            var data = new float[n];
            double[] partials = { 1.0, 2.0, 2.4, 3.0, 4.2, 5.4 };
            double[] amps = { 0.5, 0.3, 0.25, 0.15, 0.1, 0.06 };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate, v = 0;
                for (int p = 0; p < partials.Length; p++)
                    v += System.Math.Sin(2 * System.Math.PI * 392 * partials[p] * t) * amps[p] * System.Math.Exp(-t * (1.2 + p * 0.8));
                data[i] = (float)v;
            }
            Normalize(data, 0.8f);
            var clip = AudioClip.Create("TownBell", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] d, float peak)
        {
            float m = 0;
            foreach (var x in d) m = Mathf.Max(m, Mathf.Abs(x));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }
    }
}
