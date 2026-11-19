using System;
using System.IO;
using UnityEngine;
using World.Sim.Encounters.Rhythm;

namespace World.Game
{
    /// <summary>
    /// The owner's own rhythm packs for the crystal spires (SP-2, SP-3), read at run time from a LOCAL folder that is never
    /// part of the repo. The folder is a Beat Shaper packs folder: <c>&lt;root&gt;/&lt;pack id&gt;/pack.json</c> with the stems
    /// named inside pack.json (<c>../_shared/drums_xxx.wav</c>) resolved against the pack's folder.
    /// Where the root comes from, first hit wins: <see cref="RootOverride"/> (tests), the environment variable
    /// <c>WORLD_RHYTHM_PACKS</c>, the key <c>"rhythmPacksDir"</c> of the git-ignored <c>config.json</c> beside <c>Assets</c>.
    /// Any problem (no folder, no pack, no stems, unreadable) returns false and the caller falls back to spire 1's chart and
    /// track. Stems are 16-bit PCM, 24-bit PCM or 32-bit float WAV files.
    /// </summary>
    public static class LocalRhythmPacks
    {
        public const string EnvVar = "WORLD_RHYTHM_PACKS", ConfigKey = "rhythmPacksDir";

        /// <summary>The pack played at spire n (0-based, by order of rising). Spire 1 (index 0) plays the built-in Lv1 Warehouse.</summary>
        public static readonly string[] PackOfSpire = { null, "lv2_alien_spaceship", "gospel_temple", "gospel_storm" };

        /// <summary>Test hook: when not null, the packs root (an empty string means "no folder").</summary>
        public static string RootOverride;

        [Serializable] private sealed class Config { public string rhythmPacksDir; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => RootOverride = null;

        public static string PackIdOfSpire(int spireIndex) => spireIndex >= 1 && spireIndex < PackOfSpire.Length ? PackOfSpire[spireIndex] : null;

        /// <summary>The packs root, or null when none is set or it does not exist.</summary>
        public static string Root()
        {
            string r = RootOverride;
            if (r == null) r = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrEmpty(r)) r = FromConfig();
            if (string.IsNullOrEmpty(r)) return null;
            try { r = Path.GetFullPath(r); } catch (Exception) { return null; }
            return Directory.Exists(r) ? r : null;
        }

        private static string FromConfig()
        {
            try
            {
                foreach (var p in new[]
                {
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "config.json")),
                    Path.Combine(Environment.CurrentDirectory, "config.json"),
                })
                {
                    if (!File.Exists(p)) continue;
                    var c = JsonUtility.FromJson<Config>(File.ReadAllText(p));
                    if (c != null && !string.IsNullOrEmpty(c.rhythmPacksDir)) return c.rhythmPacksDir;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Rhythm] config.json unreadable: " + e.Message); }
            return null;
        }

        /// <summary>Loads the pack's chart and stems from the local folder. False (and both null) when anything is missing.</summary>
        public static bool TryLoad(string packId, out Chart chart, out StemSet stems)
        {
            chart = null; stems = null;
            if (string.IsNullOrEmpty(packId)) return false;
            try
            {
                var root = Root();
                if (root == null) return false;
                var dir = Path.Combine(root, packId);
                var json = Path.Combine(dir, "pack.json");
                if (!File.Exists(json)) return false;
                var c = PackJson.Parse(File.ReadAllText(json));
                AudioClip Clip(string key)
                {
                    if (!c.Stems.TryGetValue(key, out var rel) || string.IsNullOrEmpty(rel)) return null;
                    var path = Path.GetFullPath(Path.Combine(dir, rel));
                    if (!File.Exists(path)) return null;
                    return LoadWav(path, packId + "_" + key);
                }
                var d = Clip("drums"); var b = Clip("bass"); var o = Clip("other");
                if (d == null || b == null)
                {
                    if (d != null) UnityEngine.Object.Destroy(d);
                    if (b != null) UnityEngine.Object.Destroy(b);
                    if (o != null) UnityEngine.Object.Destroy(o);
                    return false;
                }
                chart = c;
                stems = new StemSet { Drums = d, Bass = b, Other = o, IsSynth = false, Source = "local pack " + packId };
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rhythm] local pack '" + packId + "' not usable: " + e.Message);
                chart = null; stems = null;
                return false;
            }
        }

        /// <summary>Reads a RIFF WAV (PCM 16/24 bit or float 32 bit) into a non-streamed AudioClip; null when it cannot be read.</summary>
        public static AudioClip LoadWav(string path, string name)
        {
            var bytes = File.ReadAllBytes(path);
            if (!ParseWav(bytes, out int channels, out int rate, out float[] samples)) return null;
            var clip = AudioClip.Create(name, samples.Length / channels, channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>The WAV header and samples (interleaved floats). Pure; used by tests.</summary>
        public static bool ParseWav(byte[] b, out int channels, out int rate, out float[] samples)
        {
            channels = 0; rate = 0; samples = null;
            if (b == null || b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E') return false;
            int fmtTag = 0, bits = 0, dataAt = -1, dataLen = 0;
            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = "" + (char)b[pos] + (char)b[pos + 1] + (char)b[pos + 2] + (char)b[pos + 3];
                int len = BitConverter.ToInt32(b, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    fmtTag = BitConverter.ToUInt16(b, body);
                    channels = BitConverter.ToUInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToUInt16(b, body + 14);
                    if (fmtTag == 0xFFFE && len >= 26) fmtTag = BitConverter.ToUInt16(b, body + 24); // extensible: sub-format
                }
                else if (id == "data") { dataAt = body; dataLen = Math.Min(len < 0 ? int.MaxValue : len, b.Length - body); break; }
                if (len < 0) return false;
                pos = body + len + (len & 1);
            }
            if (dataAt < 0 || channels < 1 || rate < 8000) return false;
            int bytesPer = bits / 8;
            if (!((fmtTag == 1 && (bits == 16 || bits == 24)) || (fmtTag == 3 && bits == 32))) return false;
            int n = dataLen / bytesPer;
            n -= n % channels;
            samples = new float[n];
            for (int i = 0; i < n; i++)
            {
                int o = dataAt + i * bytesPer;
                if (fmtTag == 3) samples[i] = BitConverter.ToSingle(b, o);
                else if (bits == 16) samples[i] = BitConverter.ToInt16(b, o) / 32768f;
                else samples[i] = (((b[o + 2] << 24) | (b[o + 1] << 16) | (b[o] << 8)) >> 8) / 8388608f;
            }
            return true;
        }
    }
}
