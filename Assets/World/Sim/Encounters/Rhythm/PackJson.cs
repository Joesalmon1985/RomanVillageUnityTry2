using System;
using Newtonsoft.Json.Linq;

namespace World.Sim.Encounters.Rhythm
{
    /// <summary>Parser for the Beat Shaper pack.json format (bpm, phrase_beats, loop_frames, stems, tiers "1".."4" with drums and bass).</summary>
    public static class PackJson
    {
        public static Chart Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("pack json is empty");
            var root = JObject.Parse(json);
            var chart = new Chart
            {
                Id = (string)root["id"],
                Label = (string)root["label"],
                Bpm = root["bpm"] != null ? (double)root["bpm"] : 174,
                PhraseBeats = root["phrase_beats"] != null ? (int)root["phrase_beats"] : 32,
                LoopFrames = root["loop_frames"] != null ? (int)root["loop_frames"] : 0,
            };
            var stems = root["stems"] as JObject;
            if (stems != null)
                foreach (var p in stems.Properties()) chart.Stems[p.Name] = (string)p.Value;
            var tiers = root["tiers"] as JObject;
            if (tiers == null) throw new FormatException("pack json has no tiers");
            foreach (var t in tiers.Properties())
            {
                var tier = new TierCues();
                ReadCues(t.Value["drums"] as JArray, tier.Drums, "drums");
                ReadCues(t.Value["bass"] as JArray, tier.Bass, "bass");
                chart.Tiers[t.Name] = tier;
            }
            return chart;
        }

        private static void ReadCues(JArray src, System.Collections.Generic.List<ChartCue> dst, string defaultFamily)
        {
            if (src == null) return;
            foreach (var c in src)
            {
                var a = (JArray)c;
                if (a.Count < 3) throw new FormatException("cue needs [tick, dur_ticks, pad]");
                dst.Add(new ChartCue
                {
                    Tick = (int)a[0],
                    DurTicks = (int)a[1],
                    Pad = (int)a[2],
                    Family = a.Count > 3 ? (string)a[3] : defaultFamily,
                });
            }
        }
    }
}
