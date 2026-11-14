using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Reads a data pack (PRD_CRYSTALS 6.1, Docs/CAMPAIGN_FORMAT.md) into one CampaignDefinition, and hashes a
    /// definition. Owned by package I2 (Sim/Campaign/Definition/**); the A.5 signatures are binding.
    ///
    /// A pack is a folder of JSON files. Each file is one object whose keys are top-level parts of the definition
    /// (<c>Id</c>, <c>Locations</c>, <c>Lines</c>, ...). List parts from several files are appended in file order; any
    /// other part (Id, Title, Status, Rival, Tuning, ...) may be set by one file only. Every file is checked on its own
    /// first, strictly: unknown keys, wrong types, unknown enum names and bad marker references are refused with a
    /// FormatException that names the file, the JSON path and the line (details: <see cref="CampaignLoadError.Of"/>). The loader does not judge
    /// the story; that is <see cref="CampaignValidator"/>'s job.
    /// </summary>
    public static class CampaignLoader
    {
        /// <summary>The file that holds the pack's identity; always read first by <see cref="PackOrder"/>.</summary>
        public const string MainFile = "campaign.json";

        // Top-level parts by name (case-insensitive lookup, canonical spelling kept).
        private static readonly Dictionary<string, FieldInfo> Parts = BuildParts();

        private static Dictionary<string, FieldInfo> BuildParts()
        {
            var map = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in typeof(CampaignDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance)) map[f.Name] = f;
            return map;
        }

        private static bool IsList(FieldInfo f) => typeof(IList).IsAssignableFrom(f.FieldType);

        private static JsonSerializerSettings ReadSettings() => new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Reuse,
            Converters = { new StringEnumConverter { AllowIntegerValues = false }, new MarkerRefConverter() },
        };

        private static JsonSerializerSettings CanonicalSettings() => new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None,
        };

        /// <summary>
        /// Merges the pack's files (names as readFile understands them), in the order given. Use
        /// <see cref="PackOrder"/> for the standard order. A file whose text is empty is skipped. Throws
        /// FormatException naming the file and the JSON path on any problem (details: <see cref="CampaignLoadError.Of"/>).
        /// </summary>
        public static CampaignDefinition LoadFolder(Func<string, string> readFile, IEnumerable<string> files) => FromMerged(Merge(readFile, files));

        /// <summary>
        /// The first half of <see cref="LoadFolder"/>: every file checked and merged into one JSON object, before it is
        /// turned into a definition. For tools and test fixtures that change a pack before reading it.
        /// </summary>
        public static JObject Merge(Func<string, string> readFile, IEnumerable<string> files)
        {
            if (readFile == null) throw new ArgumentNullException(nameof(readFile));
            var merged = new JObject();
            var setBy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (files != null)
            {
                foreach (var file in files)
                {
                    string text;
                    try { text = readFile(file); }
                    catch (Exception e) when (!CampaignLoadError.IsLoadError(e))
                    {
                        throw CampaignLoadError.Create(file, "", 0, 0, "the file could not be read (" + e.Message + ")", e);
                    }
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var obj = ParseObject(file, text);
                    CheckFile(file, obj);
                    foreach (var prop in obj.Properties())
                    {
                        var part = Parts[prop.Name];   // CheckFile has refused unknown parts
                        if (IsList(part))
                        {
                            if (!(merged[part.Name] is JArray list)) merged[part.Name] = list = new JArray();
                            foreach (var item in (JArray)prop.Value) list.Add(item.DeepClone());
                        }
                        else
                        {
                            if (setBy.TryGetValue(part.Name, out var first))
                                throw Fail(file, prop.Value, part.Name + " is already set by " + first + "; only list parts may be split over several files");
                            setBy[part.Name] = file;
                            merged[part.Name] = prop.Value.DeepClone();
                        }
                    }
                }
            }
            return merged;
        }

        /// <summary>The second half of <see cref="LoadFolder"/>: a merged pack object read strictly into a definition.</summary>
        public static CampaignDefinition FromMerged(JObject merged)
        {
            if (merged == null) throw new ArgumentNullException(nameof(merged));
            CampaignDefinition d;
            try
            {
                CheckFile("(merged pack)", merged);
                d = new CampaignDefinition();
                using (var reader = merged.CreateReader()) JsonSerializer.Create(ReadSettings()).Populate(reader, d);
            }
            catch (FormatException e) when (CampaignLoadError.IsLoadError(e)) { throw; }
            catch (JsonException e)
            {
                throw CampaignLoadError.Create("(merged pack)", PathOf(e), 0, 0, Reason(e), e);
            }
            FillLegendCells(d);
            return d;
        }

        /// <summary>
        /// The standard file order of a pack: <see cref="MainFile"/> first, then every other ".json" name in ordinal
        /// order. Names that do not end in ".json" (such as Unity ".meta" files) are left out.
        /// </summary>
        public static List<string> PackOrder(IEnumerable<string> names)
        {
            var rest = new List<string>();
            bool main = false;
            if (names != null)
                foreach (var n in names)
                {
                    if (n == null || !n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(n, MainFile, StringComparison.OrdinalIgnoreCase)) main = true;
                    else rest.Add(n);
                }
            rest.Sort(StringComparer.Ordinal);
            if (main) rest.Insert(0, MainFile);
            return rest;
        }

        /// <summary>Loads every ".json" file directly in a folder on disk, in <see cref="PackOrder"/>.</summary>
        public static CampaignDefinition LoadDirectory(string folder)
        {
            if (folder == null || !Directory.Exists(folder))
                throw CampaignLoadError.Create(folder ?? "(null)", "", 0, 0, "the pack folder does not exist", null);
            var names = new List<string>();
            foreach (var p in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)) names.Add(Path.GetFileName(p));
            return LoadFolder(n => File.ReadAllText(Path.Combine(folder, n)), PackOrder(names));
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

        // ---------------------------------------------------------------- internals

        private static JObject ParseObject(string file, string text)
        {
            JToken token;
            try
            {
                token = JToken.Parse(text, new JsonLoadSettings
                {
                    LineInfoHandling = LineInfoHandling.Load,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Ignore,
                });
            }
            catch (JsonReaderException e)
            {
                throw CampaignLoadError.Create(file, e.Path ?? "", e.LineNumber, e.LinePosition, "this is not valid JSON: " + Reason(e), e);
            }
            if (!(token is JObject obj)) throw Fail(file, token, "a pack file must be one JSON object");
            return obj;
        }

        /// <summary>Checks one file on its own: known parts, lists where lists belong, and a strict typed read.</summary>
        private static void CheckFile(string file, JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                if (!Parts.TryGetValue(prop.Name, out var part))
                    throw Fail(file, prop, "'" + prop.Name + "' is not a part of a campaign definition (parts: " + string.Join(", ", PartNames()) + ")");
                if (!string.Equals(part.Name, prop.Name, StringComparison.Ordinal))
                    throw Fail(file, prop, "write '" + part.Name + "', not '" + prop.Name + "'");
                if (IsList(part) && prop.Value.Type != JTokenType.Array)
                    throw Fail(file, prop.Value, part.Name + " must be a list ([ ... ])");
            }
            try
            {
                var scratch = new CampaignDefinition();
                using (var reader = obj.CreateReader()) JsonSerializer.Create(ReadSettings()).Populate(reader, scratch);
            }
            catch (JsonException e)
            {
                string path = PathOf(e);
                int line = 0, col = 0;
                var at = path.Length > 0 ? obj.SelectToken(path, false) : null;
                if (at is IJsonLineInfo li && li.HasLineInfo()) { line = li.LineNumber; col = li.LinePosition; }
                else if (e is JsonSerializationException se) { line = se.LineNumber; col = se.LinePosition; }
                throw CampaignLoadError.Create(file, path, line, col, Reason(e), e);
            }
        }

        private static IEnumerable<string> PartNames()
        {
            foreach (var f in typeof(CampaignDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance)) yield return f.Name;
        }

        private static FormatException Fail(string file, JToken at, string reason)
        {
            int line = 0, col = 0;
            if (at is IJsonLineInfo li && li.HasLineInfo()) { line = li.LineNumber; col = li.LinePosition; }
            return CampaignLoadError.Create(file, at?.Path ?? "", line, col, reason, null);
        }

        private static string PathOf(JsonException e)
        {
            if (e is JsonSerializationException se && !string.IsNullOrEmpty(se.Path)) return se.Path;
            if (e is JsonReaderException re && !string.IsNullOrEmpty(re.Path)) return re.Path;
            return "";
        }

        /// <summary>The exception's message without Newtonsoft's trailing "Path '...', line n, position m." part.</summary>
        private static string Reason(Exception e)
        {
            string m = e.Message ?? "";
            int cut = m.IndexOf(" Path '", StringComparison.Ordinal);
            if (cut < 0) cut = m.IndexOf(", line ", StringComparison.Ordinal);
            if (cut > 0) m = m.Substring(0, cut);
            return m.TrimEnd(' ', '.', ',');
        }

        /// <summary>Gives each legend marker the cell of its character's first appearance (row then column).</summary>
        private static void FillLegendCells(CampaignDefinition d)
        {
            if (d.Interiors == null) return;
            foreach (var m in d.Interiors)
            {
                if (m?.Legend == null || m.Rows == null) continue;
                foreach (var kv in m.Legend)
                {
                    if (kv.Value == null) continue;
                    bool found = false;
                    for (int j = 0; j < m.Rows.Count && !found; j++)
                    {
                        int i = (m.Rows[j] ?? "").IndexOf(kv.Key);
                        if (i >= 0) { kv.Value.I = i; kv.Value.J = j; found = true; }
                    }
                }
            }
        }

        /// <summary>Reads a MarkerRef from its text form ("location/marker", "location/interior/marker", "") or an object.</summary>
        private sealed class MarkerRefConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(MarkerRef) || objectType == typeof(MarkerRef?);

            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Null:
                        return default(MarkerRef);
                    case JsonToken.String:
                    {
                        string s = (string)reader.Value;
                        if (string.IsNullOrEmpty(s)) return default(MarkerRef);
                        if (MarkerRef.TryParse(s, out var r)) return r;
                        throw Error(reader, "'" + s + "' is not a marker reference (write location/marker on the surface or location/interior/marker inside)");
                    }
                    case JsonToken.StartObject:
                    {
                        var o = JObject.Load(reader);
                        var r = default(MarkerRef);
                        foreach (var p in o.Properties())
                        {
                            string v = p.Value.Type == JTokenType.Null ? "" : p.Value.Type == JTokenType.String ? (string)p.Value
                                : throw new JsonSerializationException("marker reference part '" + p.Name + "' must be text");
                            switch (p.Name)
                            {
                                case "Location": r.Location = v; break;
                                case "Interior": r.Interior = v; break;
                                case "Marker": r.Marker = v; break;
                                default: throw new JsonSerializationException("'" + p.Name + "' is not a part of a marker reference (Location, Interior, Marker)");
                            }
                        }
                        return r;
                    }
                    default:
                        throw Error(reader, "a marker reference must be text such as \"glade/stone\"");
                }
            }

            private static JsonSerializationException Error(JsonReader reader, string message)
            {
                var li = reader as IJsonLineInfo;
                bool has = li != null && li.HasLineInfo();
                return new JsonSerializationException(message, reader.Path, has ? li.LineNumber : 0, has ? li.LinePosition : 0, null);
            }
        }
    }

    /// <summary>Where a pack could not be read: the file, the JSON path and line inside it, and why.</summary>
    public sealed class CampaignLoadError
    {
        public string File = "";
        /// <summary>JSON path inside the file ("" = the whole file), e.g. "Locations[2].Anchor.Kind".</summary>
        public string JsonPath = "";
        /// <summary>1-based; 0 when unknown.</summary>
        public int Line, Column;
        public string Reason = "";

        private const string Key = "World.Sim.Campaign.CampaignLoadError";

        /// <summary>
        /// The loader throws a plain FormatException (its type is pinned by I0's surface test) whose message reads
        /// "file at path (line n, column m): reason"; the parts ride in its Data and come back through <see cref="Of"/>.
        /// </summary>
        public static FormatException Create(string file, string jsonPath, int line, int column, string reason, Exception inner)
        {
            var info = new CampaignLoadError { File = file ?? "", JsonPath = jsonPath ?? "", Line = line, Column = column, Reason = reason ?? "" };
            var e = new FormatException(info.ToString(), inner);
            e.Data[Key] = info;
            return e;
        }

        /// <summary>The details of a loader error, or null for any other exception.</summary>
        public static CampaignLoadError Of(Exception e) => e?.Data[Key] as CampaignLoadError;

        public static bool IsLoadError(Exception e) => Of(e) != null;

        public override string ToString()
        {
            var sb = new StringBuilder(File);
            if (!string.IsNullOrEmpty(JsonPath)) sb.Append(" at ").Append(JsonPath);
            if (Line > 0) sb.Append(" (line ").Append(Line).Append(", column ").Append(Column).Append(')');
            sb.Append(": ").Append(Reason);
            return sb.ToString();
        }
    }
}
