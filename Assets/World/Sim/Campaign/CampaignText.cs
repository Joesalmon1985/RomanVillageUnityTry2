using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace World.Sim.Campaign
{
    /// <summary>
    /// The guard grammar of lines and quest events (6.1), fixed by I0 so the runtime, the validator and pack writers agree.
    /// A guard is "kind: subject" or "kind: subject = value" (spaces optional):
    /// <c>alive: odo</c>, <c>dead: garrick</c>, <c>fact: earth.stolen_seen</c>, <c>nofact: earth.stair_open</c>,
    /// <c>custody: earth = AtRivalAltar</c>, <c>restored: 2</c> (exactly that many crystals restored).
    /// All guards of a line must hold. An unknown kind never holds.
    /// </summary>
    public static class CampaignGuards
    {
        public const string Alive = "alive", Dead = "dead", Fact = "fact", NoFact = "nofact", Custody = "custody", Restored = "restored";

        public static readonly string[] Kinds = { Alive, Dead, Fact, NoFact, Custody, Restored };

        public static bool TryParse(string guard, out string kind, out string subject, out string value)
        {
            kind = subject = value = "";
            if (string.IsNullOrWhiteSpace(guard)) return false;
            int colon = guard.IndexOf(':');
            if (colon <= 0) return false;
            kind = guard.Substring(0, colon).Trim();
            string rest = guard.Substring(colon + 1);
            int eq = rest.IndexOf('=');
            if (eq >= 0)
            {
                subject = rest.Substring(0, eq).Trim();
                value = rest.Substring(eq + 1).Trim();
            }
            else subject = rest.Trim();
            return kind.Length > 0 && subject.Length > 0 && Array.IndexOf(Kinds, kind) >= 0;
        }

        /// <summary>True when the guard holds in this run (false for a malformed guard or no campaign).</summary>
        public static bool Holds(CampaignState c, string guard)
        {
            if (c?.Definition == null || !TryParse(guard, out var kind, out var subject, out var value)) return false;
            switch (kind)
            {
                case Alive: return c.Companion(subject)?.Alive ?? false;
                case Dead: { var k = c.Companion(subject); return k != null && !k.Alive; }
                case Fact: return c.Knows(subject);
                case NoFact: return !c.Knows(subject);
                case Custody:
                {
                    var def = c.Definition.Crystal(subject);
                    if (def == null || !Enum.TryParse(value, false, out Custody want)) return false;
                    return c.Crystal(def.Element)?.Custody == want;
                }
                case Restored:
                    return int.TryParse(subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && (c.RestoredOrder?.Count ?? 0) == n;
                default: return false;
            }
        }

        public static bool AllHold(CampaignState c, IList<string> guards)
        {
            if (guards == null) return true;
            foreach (var g in guards) if (!Holds(c, g)) return false;
            return true;
        }
    }

    /// <summary>
    /// Tokens a line's text may hold (6.1). A crystal's place may only come from a token, never fixed text.
    /// <c>{crystal.&lt;id&gt;.name}</c>, <c>{crystal.&lt;id&gt;.place}</c> (where the party believes it is),
    /// <c>{crystal.&lt;id&gt;.trueplace}</c> (where it is: only for witnesses who know, such as Orrin),
    /// <c>{crystal.&lt;id&gt;.tower}</c>, <c>{companion.&lt;id&gt;.name}</c>, <c>{role.&lt;id&gt;.name}</c>,
    /// <c>{volunteer.name}</c> (the next volunteer, "nobody" when none), <c>{count.restored}</c>, <c>{count.alive}</c>.
    /// An unknown token is left as written (the validator reports it).
    /// </summary>
    public static class CampaignTokens
    {
        /// <summary>Fills every token of a text from the run state.</summary>
        public static string Fill(CampaignState c, string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0 || c?.Definition == null) return text;
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) { sb.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf('}', open + 1);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                string token = text.Substring(open + 1, close - open - 1);
                sb.Append(Resolve(c, token) ?? "{" + token + "}");
                i = close + 1;
            }
            return sb.ToString();
        }

        /// <summary>The value of one token (without braces), null when unknown.</summary>
        public static string Resolve(CampaignState c, string token)
        {
            var d = c?.Definition;
            if (d == null || string.IsNullOrEmpty(token)) return null;
            var p = token.Split('.');
            switch (p[0])
            {
                case "volunteer":
                    if (p.Length == 2 && p[1] == "name")
                    {
                        var v = CampaignApi.NextVolunteerOf(c);
                        return v != null ? v.Name : "nobody";
                    }
                    return null;
                case "count":
                    if (p.Length != 2) return null;
                    if (p[1] == "restored") return (c.RestoredOrder?.Count ?? 0).ToString(CultureInfo.InvariantCulture);
                    if (p[1] == "alive")
                    {
                        int n = 0;
                        foreach (var k in c.Companions) if (k.Alive) n++;
                        return n.ToString(CultureInfo.InvariantCulture);
                    }
                    return null;
                case "companion":
                    return p.Length == 3 && p[2] == "name" ? d.Companion(p[1])?.Name : null;
                case "role":
                    return p.Length == 3 && p[2] == "name" ? d.Role(p[1])?.Name : null;
                case "crystal":
                {
                    if (p.Length != 3) return null;
                    var cd = d.Crystal(p[1]);
                    if (cd == null) return null;
                    switch (p[2])
                    {
                        case "name": return cd.Name;
                        case "place": return PlaceName(d, c.BeliefOf(cd.Element)?.At ?? cd.Site);
                        case "trueplace": return PlaceName(d, c.Crystal(cd.Element)?.At ?? cd.Site);
                        case "tower":
                        {
                            var t = d.Tower(cd.Tower);
                            return t != null ? d.Location(t.Location)?.Name ?? t.Id : null;
                        }
                        default: return null;
                    }
                }
                default: return null;
            }
        }

        /// <summary>The display name of a place: the interior's name inside one, else the location's ("" if unknown).</summary>
        public static string PlaceName(CampaignDefinition d, MarkerRef at)
        {
            if (d == null || at.IsEmpty) return "";
            if (at.IsInterior)
            {
                var m = d.Interior(at.Interior);
                if (m != null && !string.IsNullOrEmpty(m.Name)) return m.Name;
            }
            return d.Location(at.Location)?.Name ?? "";
        }
    }
}
