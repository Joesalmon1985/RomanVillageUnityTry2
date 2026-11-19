using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using World.Sim.Eras;

namespace World.Sim.Encounters.Council
{
    public sealed class EmblemDef { public string Id, Name, Shape, Colour; }

    /// <summary>A data-driven candidate temperament: when to stop and which tray to take.</summary>
    public sealed class CouncilPersonality
    {
        public string Id, Name, Description;
        /// <summary>Press on while the chance (percent) that the next push is a bust is at most this.</summary>
        public double BustTolerance;
        /// <summary>Never bank before this many advances in the sitting (unless a track top is in hand).</summary>
        public int MinAdvances;
        /// <summary>Always bank at this many advances.</summary>
        public int MaxAdvances;
        /// <summary>Chance (percent) to bank at once when a catspaw stands on the top place.</summary>
        public int BankAtTopPercent;
        /// <summary>Tolerance added when another candidate has more claimed disputes.</summary>
        public double TrailingBoost;
        /// <summary>Tolerance removed when this candidate leads in claimed disputes.</summary>
        public double LeadingCaution;
        /// <summary>Tolerance added while the player's spare petition is in hand (scripted players only).</summary>
        public double PetitionBoost;
        /// <summary>Chance (percent) to take the opposite of the press-on decision.</summary>
        public int Impulse;
        public double WeightStep, WeightProgress, WeightTop, WeightNewCatspaw, WeightCentral, WeightAdvanced, Noise;

        public CouncilPersonality Copy() => (CouncilPersonality)MemberwiseClone();
    }

    public sealed class ListeningLine
    {
        public string Id;
        public Intention Tag;
        public string Speaker;
        public string Text;
    }

    public sealed class DisputeDef
    {
        public string Id;
        public int Track;
        /// <summary>The dispute pattern (withheld resource, absent worker, reluctant witness, disputed ownership, forest dispute).</summary>
        public string Pattern;
        public string Role;
        /// <summary>Occupation ids of residents who may lead it; "*" accepts any resident.</summary>
        public string[] Occupations = new string[0];
        /// <summary>Title of the local delegate who stands in when no resident matches.</summary>
        public string FallbackTitle;
        /// <summary>Prop name by age key (prehistoric, historic).</summary>
        public Dictionary<string, string> Props = new Dictionary<string, string>();
        public string Silhouette;
        /// <summary>Lines keyed opening, progress, setback, revisit, agreement, acknowledgement.</summary>
        public Dictionary<string, string[]> Lines = new Dictionary<string, string[]>();
    }

    public sealed class FallbackDefs
    {
        public string DelegateTitle = "local delegate";
        public string Note;
        public List<string> DelegateNames = new List<string>();
    }

    /// <summary>Council content (disputes, dialogue, props, offices, personalities, listening lines, listening rules).</summary>
    public sealed class CouncilContent
    {
        public const string OpeningLine = "opening", ProgressLine = "progress", SetbackLine = "setback",
            RevisitLine = "revisit", AgreementLine = "agreement", AcknowledgementLine = "acknowledgement";
        public static readonly string[] LineKinds = { OpeningLine, ProgressLine, SetbackLine, RevisitLine, AgreementLine, AcknowledgementLine };

        public int Version = 1;
        public Dictionary<string, string> Offices = new Dictionary<string, string>();
        public List<EmblemDef> Emblems = new List<EmblemDef>();
        public List<CouncilPersonality> Personalities = new List<CouncilPersonality>();
        public ListeningTuning Listening = new ListeningTuning();
        public FallbackDefs Fallbacks = new FallbackDefs();
        public List<string> Names = new List<string>();
        public List<DisputeDef> Disputes = new List<DisputeDef>();
        public List<ListeningLine> ListeningLines = new List<ListeningLine>();

        static CouncilContent _builtIn;

        /// <summary>The shipped content; equals StreamingAssets/world/council/council_content.json (a test pins that).</summary>
        public static CouncilContent BuiltIn => _builtIn ?? (_builtIn = Parse(CouncilContentDefaults.Json));

        public static CouncilContent Parse(string json)
        {
            var c = JsonConvert.DeserializeObject<CouncilContent>(json) ?? new CouncilContent();
            c.Validate();
            return c;
        }

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);

        /// <summary>Throws when the content cannot run an election (missing tracks, lines or emblems).</summary>
        public void Validate()
        {
            if (Emblems.Count < CouncilRules.Seats) throw new FormatException("council content needs four emblems");
            if (Personalities.Count < CouncilRules.Seats) throw new FormatException("council content needs four personalities");
            for (int t = CouncilRules.FirstTrack; t <= CouncilRules.LastTrack; t++)
                if (Dispute(t) == null) throw new FormatException("council content has no dispute for track " + t);
            foreach (var d in Disputes)
                foreach (var k in LineKinds)
                    if (!d.Lines.TryGetValue(k, out var v) || v == null || v.Length < 2)
                        throw new FormatException("dispute " + d.Id + " needs two '" + k + "' lines");
            for (int i = 0; i < 5; i++)
            {
                int n = 0;
                foreach (var l in ListeningLines) if ((int)l.Tag == i) n++;
                if (n < 5) throw new FormatException("council content needs listening lines for every intention");
            }
        }

        public DisputeDef Dispute(int track)
        {
            foreach (var d in Disputes) if (d.Track == track) return d;
            return null;
        }

        public CouncilPersonality Personality(string id)
        {
            foreach (var p in Personalities) if (p.Id == id) return p;
            return Personalities.Count > 0 ? Personalities[0] : null;
        }

        /// <summary>"prehistoric" or "historic" (Modern and Future use the Historic props and wording).</summary>
        public static string AgeKey(Era era) => era == Era.Prehistoric ? "prehistoric" : "historic";

        public static string OfficeKey(Era era)
        {
            switch (era)
            {
                case Era.Prehistoric: return "prehistoric";
                case Era.Historic: return "historic";
                case Era.Modern: return "modern";
                default: return "future";
            }
        }

        public string Office(Era era) => Offices.TryGetValue(OfficeKey(era), out var o) ? o : "chief";

        public string Prop(DisputeDef d, Era era)
        {
            if (d.Props.TryGetValue(AgeKey(era), out var p)) return p;
            foreach (var kv in d.Props) return kv.Value;
            return "object";
        }

        /// <summary>A dispute line of a kind; the variant index wraps.</summary>
        public static string Line(DisputeDef d, string kind, int variant)
        {
            if (!d.Lines.TryGetValue(kind, out var v) || v == null || v.Length == 0) return "";
            return v[((variant % v.Length) + v.Length) % v.Length];
        }

        /// <summary>Fills {candidate}, {lead}, {prop}, {office} and {winner} in a line.</summary>
        public static string Fill(string line, string candidate, string lead, string prop, string office, string winner)
        {
            if (line == null) return "";
            return line.Replace("{candidate}", candidate ?? "").Replace("{lead}", lead ?? "").Replace("{prop}", prop ?? "")
                .Replace("{office}", office ?? "").Replace("{winner}", winner ?? "");
        }

        public ListeningLine ListeningLine(string id)
        {
            foreach (var l in ListeningLines) if (l.Id == id) return l;
            return null;
        }
    }
}
