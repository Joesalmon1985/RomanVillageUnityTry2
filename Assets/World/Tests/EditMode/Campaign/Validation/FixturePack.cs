using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using World.Sim.Campaign;

namespace World.Sim.Tests
{
    /// <summary>
    /// A broken fixture (PRD_CRYSTALS 6.5): a small edit of a shipped pack, in a JSON file of this shape:
    /// <c>{ "Why": "...", "Base": "skeleton_v1", "Expect": "duplicate_id", "Patch": [ { "Op": "add|replace|remove", "Path": "/Locations/kiln/Anchor", "Value": ... } ] }</c>.
    /// A path walks the merged pack: an object step is a key, a list step is the Id of an entry (or its index), and "-"
    /// appends (add only). Docs/CAMPAIGN_FORMAT.md describes it for fixture writers.
    /// </summary>
    public sealed class FixturePack
    {
        public string Name, Why, Base, Expect;
        public JArray Patch;

        public static FixturePack Read(string path)
        {
            var o = JObject.Parse(File.ReadAllText(path));
            return new FixturePack
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Why = (string)o["Why"],
                Base = (string)o["Base"],
                Expect = (string)o["Expect"],
                Patch = (JArray)o["Patch"] ?? new JArray(),
            };
        }

        public static List<string> All()
        {
            var list = new List<string>();
            foreach (var p in Directory.GetFiles(PackFiles.FixtureFolder)) if (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) list.Add(p);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>The base pack with this fixture's edits applied.</summary>
        public CampaignDefinition Build()
        {
            var merged = MergedPack(Base);
            foreach (var op in Patch) Apply(merged, (JObject)op);
            return CampaignLoader.FromMerged(merged);
        }

        public static JObject MergedPack(string pack)
        {
            string folder = PackFiles.PackFolder(pack);
            var names = new List<string>();
            foreach (var p in Directory.GetFiles(folder, "*.json")) names.Add(Path.GetFileName(p));
            return CampaignLoader.Merge(n => File.ReadAllText(Path.Combine(folder, n)), CampaignLoader.PackOrder(names));
        }

        public static void Apply(JObject root, JObject op)
        {
            string kind = (string)op["Op"];
            var steps = ((string)op["Path"]).Trim('/').Split('/');
            JToken parent = root;
            for (int k = 0; k < steps.Length - 1; k++) parent = Child(parent, steps[k]);
            string last = steps[steps.Length - 1];
            var value = op["Value"]?.DeepClone();
            switch (kind)
            {
                case "add":
                    if (parent is JArray arr && last == "-") arr.Add(value);
                    else ((JObject)parent)[last] = value;
                    break;
                case "replace":
                    if (parent is JArray list) Child(list, last).Replace(value);
                    else
                    {
                        if (((JObject)parent)[last] == null) throw new InvalidOperationException("nothing to replace at " + op["Path"]);
                        ((JObject)parent)[last] = value;
                    }
                    break;
                case "remove":
                    if (parent is JArray items) Child(items, last).Remove();
                    else if (!((JObject)parent).Remove(last)) throw new InvalidOperationException("nothing to remove at " + op["Path"]);
                    break;
                default:
                    throw new InvalidOperationException("unknown op " + kind);
            }
        }

        private static JToken Child(JToken t, string step)
        {
            if (t is JObject o)
                return o[step] ?? throw new InvalidOperationException("no key " + step);
            if (t is JArray a)
            {
                foreach (var item in a) if (item is JObject io && (string)io["Id"] == step) return item;
                if (int.TryParse(step, out int i) && i >= 0 && i < a.Count) return a[i];
                throw new InvalidOperationException("no entry " + step);
            }
            throw new InvalidOperationException("cannot step into " + step);
        }
    }
}
