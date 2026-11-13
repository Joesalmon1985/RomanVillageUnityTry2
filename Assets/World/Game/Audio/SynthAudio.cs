using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim.Encounters.Rhythm;

namespace World.Game
{
    /// <summary>
    /// Original procedurally generated encounter audio (no third-party samples): the fallback drum, bass and pad
    /// loops for the rhythm party (174 BPM, 32 beats, aligned to the chart's cues), count-in clicks, a crowd cheer,
    /// and small Ward duel sounds. All clips are mono at <see cref="Rate"/>.
    /// </summary>
    public static class SynthAudio
    {
        public const int Rate = 48000;
        public const double Bpm = 174.0;
        public const int LoopBeats = 32;

        /// <summary>Frames in a loop of the given beats at 174 BPM and 48 kHz (32 beats = 529655, like the owner's stems).</summary>
        public static int LoopFrames(int beats = LoopBeats) => (int)Math.Round(beats * 60.0 / Bpm * Rate);

        private static double BeatSec => 60.0 / Bpm;
        private static double TickSec(int tick) => tick / 960.0 * BeatSec;

        private sealed class Noise
        {
            private uint _s;
            public Noise(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFF) / 32768f - 1f; }
        }

        /// <summary>Drum stem: kick on every pad-1 cue, snare on every pad-4 cue of the chart's tier, hats on eighths.</summary>
        public static AudioClip DrumLoop(Chart chart, string tier = "1")
        {
            int n = LoopFrames();
            var d = new float[n];
            var noise = new Noise(0x13579BDu);
            TierCues cues = null;
            if (chart != null) chart.Tiers.TryGetValue(tier, out cues);
            var kicks = new List<int>(); var snares = new List<int>();
            if (cues != null)
                foreach (var c in cues.Drums) { if (c.Pad == 1) kicks.Add(c.Tick); else snares.Add(c.Tick); }
            else
                for (int b = 0; b < LoopBeats; b += 2) { kicks.Add(b * 960); snares.Add((b + 1) * 960); }
            foreach (int t in kicks) Kick(d, TickSec(t));
            foreach (int t in snares) Snare(d, TickSec(t), noise);
            for (int e = 0; e < LoopBeats * 2; e++) Hat(d, e * BeatSec * 0.5, e % 2 == 1 ? 0.20f : 0.10f, noise);
            Normalize(d, 0.8f);
            return Make("SynthDrums174", d);
        }

        /// <summary>Bass stem: a sub/saw note per bass cue (pitch per pad), lasting the cue (taps get a short note).</summary>
        public static AudioClip BassLoop(Chart chart, string tier = "1")
        {
            int n = LoopFrames();
            var d = new float[n];
            TierCues cues = null;
            if (chart != null) chart.Tiers.TryGetValue(tier, out cues);
            double[] padHz = { 0, 43.65, 49.0, 55.0, 65.41 }; // F1, G1, A1, C2
            if (cues != null && cues.Bass.Count > 0)
            {
                foreach (var c in cues.Bass)
                {
                    double len = c.DurTicks > 0 ? TickSec(c.DurTicks) : BeatSec * 0.9;
                    BassNote(d, TickSec(c.Tick), len, padHz[Mathf.Clamp(c.Pad, 1, 4)]);
                }
            }
            else
            {
                for (int b = 0; b < LoopBeats; b += 4) BassNote(d, b * BeatSec, BeatSec * 3.5, padHz[1 + (b / 4) % 4]);
            }
            Normalize(d, 0.75f);
            return Make("SynthBass174", d);
        }

        /// <summary>"Other" stem: a soft two-chord pad that changes every 8 beats.</summary>
        public static AudioClip PadLoop()
        {
            int n = LoopFrames();
            var d = new float[n];
            double[][] chords = { new[] { 174.61, 220.0, 261.63 }, new[] { 196.0, 246.94, 293.66 } };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                int seg = (int)(t / (8 * BeatSec));
                double local = t - seg * 8 * BeatSec;
                double env = Math.Min(1.0, local / 0.4) * Math.Min(1.0, (8 * BeatSec - local) / 0.4);
                var ch = chords[seg % 2];
                double v = 0;
                foreach (var f in ch) v += Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(2 * Math.PI * f * 2.003 * t);
                d[i] = (float)(v * env * (0.6 + 0.4 * Math.Sin(2 * Math.PI * t / BeatSec)));
            }
            Normalize(d, 0.35f);
            return Make("SynthPad174", d);
        }

        /// <summary>Count-in click (higher on the accent).</summary>
        public static AudioClip Click(bool accent)
        {
            int n = (int)(0.06 * Rate);
            var d = new float[n];
            double f = accent ? 2000 : 1400;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                d[i] = (float)(Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t * 70));
            }
            Normalize(d, 0.7f);
            return Make(accent ? "ClickHi" : "ClickLo", d);
        }

        /// <summary>A short crowd cheer: band-limited noise swell with a few whistles.</summary>
        public static AudioClip Cheer()
        {
            int n = (int)(1.6 * Rate);
            var d = new float[n];
            var noise = new Noise(0xC0FFEEu);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                float x = noise.Next();
                lp += 0.18f * (x - lp); lp2 += 0.05f * (lp - lp2);
                double env = Math.Min(1.0, t / 0.15) * Math.Exp(-Math.Max(0, t - 0.4) * 2.2);
                double v = (lp - lp2) * 2.2 * env;
                v += Math.Sin(2 * Math.PI * (1800 + 400 * Math.Sin(t * 9)) * t) * 0.08 * Math.Max(0, Math.Sin(Math.PI * Math.Min(1, t / 0.9)));
                d[i] = (float)v;
            }
            Normalize(d, 0.6f);
            return Make("Cheer", d);
        }

        /// <summary>Rising shimmer for a Ward cast.</summary>
        public static AudioClip CastWhoosh()
        {
            int n = (int)(0.55 * Rate);
            var d = new float[n];
            var noise = new Noise(0x5EEDu);
            double phase = 0; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                double f = 300 + 900 * t / 0.55;
                phase += 2 * Math.PI * f / Rate;
                lp += 0.3f * (noise.Next() - lp);
                double env = Math.Sin(Math.PI * t / 0.55);
                d[i] = (float)((Math.Sin(phase) * 0.4 + lp * 0.5) * env);
            }
            Normalize(d, 0.5f);
            return Make("CastWhoosh", d);
        }

        /// <summary>Glassy crack for a fracture landing on a Ward.</summary>
        public static AudioClip Crack()
        {
            int n = (int)(0.4 * Rate);
            var d = new float[n];
            var noise = new Noise(0xBADC0DEu);
            double[] p = { 1320, 2210, 3170, 4430 };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate, v = noise.Next() * Math.Exp(-t * 60) * 0.8;
                for (int k = 0; k < p.Length; k++) v += Math.Sin(2 * Math.PI * p[k] * t) * Math.Exp(-t * (9 + 4 * k)) * 0.25;
                d[i] = (float)v;
            }
            Normalize(d, 0.6f);
            return Make("Crack", d);
        }

        // ---------------------------------------------------------------- voices

        private static void Kick(float[] d, double t0)
        {
            int s0 = (int)Math.Round(t0 * Rate); double phase = 0;
            for (int i = 0; i < (int)(0.30 * Rate) && s0 + i < d.Length; i++)
            {
                double t = i / (double)Rate;
                double f = 48 + 120 * Math.Exp(-t * 30);
                phase += 2 * Math.PI * f / Rate;
                d[s0 + i] += (float)(Math.Sin(phase) * Math.Exp(-t * 10) * 1.0 + (i < 60 ? 0.3 : 0));
            }
        }

        private static void Snare(float[] d, double t0, Noise noise)
        {
            int s0 = (int)Math.Round(t0 * Rate);
            for (int i = 0; i < (int)(0.24 * Rate) && s0 + i < d.Length; i++)
            {
                double t = i / (double)Rate;
                double body = Math.Sin(2 * Math.PI * 185 * t) * Math.Exp(-t * 28) * 0.45;
                d[s0 + i] += (float)(body + noise.Next() * Math.Exp(-t * 16) * 0.45);
            }
        }

        private static void Hat(float[] d, double t0, float amp, Noise noise)
        {
            int s0 = (int)Math.Round(t0 * Rate); float prev = 0;
            for (int i = 0; i < (int)(0.045 * Rate) && s0 + i < d.Length; i++)
            {
                double t = i / (double)Rate;
                float x = noise.Next(); float hp = x - prev; prev = x;
                d[s0 + i] += (float)(hp * Math.Exp(-t * 95) * amp);
            }
        }

        private static void BassNote(float[] d, double t0, double len, double hz)
        {
            int s0 = (int)Math.Round(t0 * Rate);
            int count = (int)(len * Rate);
            double ph = 0;
            for (int i = 0; i < count && s0 + i < d.Length; i++)
            {
                double t = i / (double)Rate;
                ph += hz / Rate; ph -= Math.Floor(ph);
                double saw = 2 * ph - 1;
                double sub = Math.Sin(2 * Math.PI * ph);
                double env = Math.Min(1.0, t / 0.008) * Math.Min(1.0, (len - t) / 0.03) * (0.75 + 0.25 * Math.Exp(-t * 6));
                d[s0 + i] += (float)((sub * 0.8 + saw * 0.25) * env);
            }
        }

        private static void Normalize(float[] d, float peak)
        {
            float m = 0;
            foreach (var x in d) m = Mathf.Max(m, Mathf.Abs(x));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
