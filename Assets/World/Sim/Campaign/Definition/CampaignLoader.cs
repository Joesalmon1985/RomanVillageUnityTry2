using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Reads a data pack (6.1) into one CampaignDefinition, and hashes a definition. OWNED BY PACKAGE I2 from tag
    /// cry-I0-sim (Sim/Campaign/Definition/**); the signatures are binding (A.5).
    /// I0 version: LoadFolder merges the files in the order given, each a JSON object with any of the definition's
    /// top-level fields (lists from several files are appended, single values are overwritten); enums may be written by
    /// name. No checks: that is the validator's job. Hash is final: SHA-256 of the canonical JSON, lower-case hex.
    /// </summary>
    public static class CampaignLoader
    {
        private static JsonSerializerSettings ReadSettings() => new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Reuse,
            Converters = { new StringEnumConverter() },
        };

        private static JsonSerializerSettings CanonicalSettings() => new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None,
        };

        /// <summary>
        /// Merges the pack's files (paths as readFile understands them, e.g. "campaign.json", "earth.json", ...).
        /// I0 STUB of I2's loader: throws FormatException with the file name for unreadable JSON; never validates.
        /// </summary>
        public static CampaignDefinition LoadFolder(Func<string, string> readFile, IEnumerable<string> files)
        {
            if (readFile == null) throw new ArgumentNullException(nameof(readFile));
            var d = new CampaignDefinition();
            if (files == null) return d;
            var settings = ReadSettings();
            foreach (var f in files)
            {
                string text = readFile(f);
                if (string.IsNullOrWhiteSpace(text)) continue;
                try
                {
                    JsonConvert.PopulateObject(text, d, settings);
                }
                catch (JsonException e)
                {
                    throw new FormatException(f + " could not be read: " + e.Message, e);
                }
            }
            return d;
        }

        /// <summary>The definition as canonical JSON (the same settings as the save; lists keep their order).</summary>
        public static string CanonicalJson(CampaignDefinition d) => d == null ? "" : JsonConvert.SerializeObject(d, CanonicalSettings());

        /// <summary>SHA-256 of the canonical JSON as 64 lower-case hex digits ("" for null). The same definition always gives the same hash.</summary>
        public static string Hash(CampaignDefinition d)
        {
            if (d == null) return "";
            byte[] bytes;
            using (var sha = SHA256.Create()) bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalJson(d)));
            var sb = new StringBuilder(64);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
