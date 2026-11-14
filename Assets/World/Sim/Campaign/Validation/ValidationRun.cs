using System;
using System.Collections.Generic;
using System.Globalization;
using World.Sim.Eras;

namespace World.Sim.Campaign
{
    /// <summary>
    /// One validation of one definition (CampaignValidator.Validate). Form, roles, crystals and towers, places,
    /// interiors and narrative are here; the reachability engine and the route checks are in ValidationRoutes.cs.
    /// Problems come out in a fixed order (the order of the checks, then of the definition's lists), each once.
    /// </summary>
    internal sealed partial class ValidationRun
    {
        private static readonly string[] FixedRoles = { "mira", "prince", "rival_leader", "final_enemy", "wizard" };

        private static readonly string[] Services =
        {
            "", CompanionServices.Shield, CompanionServices.Lamp, CompanionServices.Fire, CompanionServices.Water,
            CompanionServices.Earth, CompanionServices.Scout,
        };

        private readonly CampaignDefinition d;
        private readonly ValidationOptions o;
        private readonly List<Problem> problems;
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        public ValidationRun(CampaignDefinition d, ValidationOptions o, List<Problem> problems)
        {
            this.d = d;
            this.o = o;
            this.problems = problems;
        }

        /// <summary>Story checks are errors in a Complete pack and warnings in a Partial one.</summary>
        private bool StoryIsError => d.Status == PackStatus.Complete;

        public void Run()
        {
            Ids();
            Names();
            References();
            Texts();
            Roles();
            CrystalsAndTowers();
            Rival();
            Places();
            Interiors();
            Routes();
            Narrative();
        }

        private void Add(string code, string id, string sentence, bool isError = true)
        {
            string key = code + "|" + id + "|" + sentence;
            if (!seen.Add(key)) return;
            problems.Add(new Problem(code, sentence, id ?? "", isError));
        }

        private static string Q(string s) => "'" + (s ?? "") + "'";

        private static bool Empty(string s) => string.IsNullOrEmpty(s);

        private static IEnumerable<T> Items<T>(List<T> list) where T : class
        {
            if (list == null) yield break;
            foreach (var x in list) if (x != null) yield return x;
        }

        // ---------------------------------------------------------------- ids

        private void Ids()
        {
            // People (roles and companions) share one namespace: a line's Speaker names one of them.
            var people = new List<(string id, string what)>();
            foreach (var r in Items(d.Roles)) people.Add((r.Id, "role"));
            foreach (var k in Items(d.Companions)) people.Add((k.Id, "companion"));
            Unique(people, "person");
            Unique(Ids(d.Locations, x => x.Id, "location"), "location");
            Unique(Ids(d.Interiors, x => x.Id, "interior"), "interior");
            Unique(Ids(d.Connections, x => x.Id, "connection"), "connection");
            Unique(Ids(d.Crystals, x => x.Id, "crystal"), "crystal");
            Unique(Ids(d.Towers, x => x.Id, "tower"), "tower");
            Unique(Ids(d.Clues, x => x.Id, "clue"), "clue");
            Unique(Ids(d.Leads, x => x.Id, "lead"), "lead");
            Unique(Ids(d.Choices, x => x.Id, "choice"), "choice");
            Unique(Ids(d.Encounters, x => x.Id, "encounter"), "encounter");
            Unique(Ids(d.Events, x => x.Id, "quest event"), "quest event");
            Unique(Ids(d.Endings, x => x.Id, "ending"), "ending");
            Unique(Ids(d.Lines, x => x.Id, "line"), "line");
            foreach (var l in Items(d.Locations))
                Unique(Ids(l.Markers, x => x.Id, "marker"), "marker in " + Q(l.Id));
            foreach (var m in Items(d.Interiors))
            {
                var placed = new List<(string, string)>();
                foreach (var md in InteriorMaps.Markers(m)) placed.Add((md.Id, "marker"));
                Unique(placed, "marker in " + Q(m.Id));
            }
            NullEntries();
        }

        private static List<(string, string)> Ids<T>(List<T> list, Func<T, string> id, string what) where T : class
        {
            var r = new List<(string, string)>();
            foreach (var x in Items(list)) r.Add((id(x), what));
            return r;
        }

        private void Unique(List<(string id, string what)> ids, string scope)
        {
            var count = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (id, what) in ids)
            {
                if (Empty(id)) { Add(ProblemCodes.Missing, "", "a " + what + " has no id."); continue; }
                count.TryGetValue(id, out int n);
                count[id] = n + 1;
                if (n + 1 == 2) Add(ProblemCodes.DuplicateId, id, "the " + scope + " id " + Q(id) + " is used more than once.");
            }
        }

        private void NullEntries()
        {
            bool any = false;
            foreach (var l in new System.Collections.IList[] { d.Roles, d.Companions, d.Locations, d.Interiors, d.Connections, d.Crystals, d.Towers, d.Clues, d.Leads, d.Choices, d.Encounters, d.Events, d.Endings, d.Lines })
                foreach (var x in l) if (x == null) any = true;
            if (any) Add(ProblemCodes.Missing, "", "a list holds an empty entry (null).");
        }

        // ---------------------------------------------------------------- names

        private void Names()
        {
            foreach (var r in Items(d.Roles)) if (Empty(r.Name)) Add(ValidationCodes.NoName, r.Id, "the role " + Q(r.Id) + " has no name.");
            foreach (var k in Items(d.Companions))
            {
                if (Empty(k.Name)) Add(ValidationCodes.NoName, k.Id, "the companion " + Q(k.Id) + " has no name.");
                if (Empty(k.Role)) Add(ValidationCodes.NoName, k.Id, "the companion " + Q(k.Id) + " has no role.");
            }
            foreach (var l in Items(d.Locations))
            {
                if (Empty(l.Name)) Add(ValidationCodes.NoName, l.Id, "the location " + Q(l.Id) + " has no name.");
                foreach (var m in Items(l.Markers))
                    if (Empty(m.Name)) Add(ValidationCodes.NoName, l.Id + "/" + m.Id, "the marker " + Q(l.Id + "/" + m.Id) + " has no name.");
            }
            foreach (var m in Items(d.Interiors))
            {
                if (Empty(m.Name)) Add(ValidationCodes.NoName, m.Id, "the interior " + Q(m.Id) + " has no name.");
                if (m.Legend != null)
                    foreach (var kv in m.Legend)
                        if (kv.Value != null && Empty(kv.Value.Name)) Add(ValidationCodes.NoName, m.Id + "/" + kv.Value.Id, "the marker " + Q(m.Id + "/" + kv.Value.Id) + " has no name.");
            }
            foreach (var c in Items(d.Crystals)) if (Empty(c.Name)) Add(ValidationCodes.NoName, c.Id, "the crystal " + Q(c.Id) + " has no name.");
            foreach (var e in Items(d.Encounters)) if (Empty(e.Name)) Add(ValidationCodes.NoName, e.Id, "the encounter " + Q(e.Id) + " has no name.");
            foreach (var l in Items(d.Leads)) if (Empty(l.Title)) Add(ValidationCodes.NoName, l.Id, "the lead " + Q(l.Id) + " has no title.");
            if (Empty(d.Id)) Add(ProblemCodes.Missing, "", "the pack has no id.");
            if (Empty(d.Title)) Add(ValidationCodes.NoName, d.Id, "the pack has no title.");
        }

        // ---------------------------------------------------------------- references

        /// <summary>A marker reference resolved to its place: a surface marker of a location, or a cell of an interior.</summary>
        internal sealed class Spot
        {
            public string Location;
            public InteriorDef Interior;
            public MarkerDef Marker;
            public int I, J;
            public bool IsInterior => Interior != null;
            /// <summary>Equal for every reference to the same marker (the Location part of an interior reference is ignored).</summary>
            public string Key => IsInterior ? "I:" + Interior.Id + "/" + Marker.Id : "S:" + Location + "/" + Marker.Id;
        }

        private readonly Dictionary<string, Spot> spotCache = new Dictionary<string, Spot>(StringComparer.Ordinal);

        /// <summary>Resolves a reference; null when any part is missing. Warns when an interior reference names another location than the interior's.</summary>
        internal Spot Resolve(MarkerRef r)
        {
            if (r.IsEmpty || Empty(r.Location) || Empty(r.Marker)) return null;
            string k = r.ToString();
            if (spotCache.TryGetValue(k, out var cached)) return cached;
            Spot s = null;
            var loc = d.Location(r.Location);
            if (loc != null)
            {
                if (!r.IsInterior)
                {
                    var m = loc.Markers?.Find(x => x != null && x.Id == r.Marker);
                    if (m != null) s = new Spot { Location = loc.Id, Marker = m, I = m.I, J = m.J };
                }
                else
                {
                    var interior = d.Interior(r.Interior);
                    if (interior != null)
                    {
                        foreach (var md in InteriorMaps.Markers(interior))
                            if (md.Id == r.Marker) { s = new Spot { Location = loc.Id, Interior = interior, Marker = md, I = md.I, J = md.J }; break; }
                        if (s != null && !Empty(interior.Location) && interior.Location != r.Location)
                            Add(ValidationCodes.InteriorLocation, interior.Id, "the reference " + Q(k) + " files " + Q(interior.Id) + " under " + Q(r.Location)
                                + ", but the interior's location is " + Q(interior.Location) + ".", false);
                    }
                }
            }
            spotCache[k] = s;
            return s;
        }

        /// <summary>Resolves the text form of a reference (MarkerDef.To, clue sources).</summary>
        internal Spot ResolveText(string text) => MarkerRef.TryParse(text, out var r) ? Resolve(r) : null;

        private void Ref(MarkerRef r, string owner, string what, bool required = true)
        {
            if (r.IsEmpty)
            {
                if (required) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names no " + what + ".");
                return;
            }
            if (Resolve(r) == null) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names the " + what + " " + Q(r.ToString()) + ", which does not exist.");
        }

        private void RefText(string text, string owner, string what)
        {
            if (Empty(text)) { Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names no " + what + "."); return; }
            if (!MarkerRef.TryParse(text, out var r))
            {
                Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names the " + what + " " + Q(text) + ", which is not a marker reference.");
                return;
            }
            Ref(r, owner, what);
        }

        private void LineRef(string lineId, string owner, string what, bool required = true)
        {
            if (Empty(lineId)) { if (required) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " has no " + what + "."); return; }
            if (d.Line(lineId) == null) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names the line " + Q(lineId) + ", which does not exist.");
        }

        private void CrystalRef(string id, string owner, bool required)
        {
            if (Empty(id)) { if (required) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names no crystal."); return; }
            if (d.Crystal(id) == null) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " names the crystal " + Q(id) + ", which does not exist.");
        }

        private bool IsPerson(string id) => d.Role(id) != null || d.Companion(id) != null;

        private void References()
        {
            foreach (var k in Items(d.Companions))
            {
                LineRef(k.Line, k.Id, "line");
                if (Array.IndexOf(Services, k.Service ?? "") < 0)
                    Add(ProblemCodes.DanglingReference, k.Id, "the companion " + Q(k.Id) + " has the service " + Q(k.Service) + ", which is not one of " + string.Join(", ", Services, 1, Services.Length - 1) + ".");
                if (Empty(k.ActorId)) Add(ProblemCodes.Missing, k.Id, "the companion " + Q(k.Id) + " has no actor id.");
            }
            foreach (var l in Items(d.Locations))
            {
                if (!Empty(l.SafePoint) && l.Markers?.Find(m => m != null && m.Id == l.SafePoint) == null)
                    Add(ProblemCodes.DanglingReference, l.Id, "the location " + Q(l.Id) + " names the safe point " + Q(l.SafePoint) + ", which is not one of its markers.");
                foreach (var m in Items(l.Markers)) MarkerRefs(m, l.Id + "/" + m.Id, l.Id);
            }
            foreach (var interior in Items(d.Interiors))
            {
                if (!Empty(interior.Location) && d.Location(interior.Location) == null)
                    Add(ProblemCodes.DanglingReference, interior.Id, "the interior " + Q(interior.Id) + " is filed under the location " + Q(interior.Location) + ", which does not exist.");
                if (interior.Legend != null)
                    foreach (var kv in interior.Legend) if (kv.Value != null) MarkerRefs(kv.Value, interior.Id + "/" + kv.Value.Id, null);
            }
            foreach (var c in Items(d.Connections))
            {
                Ref(c.A, c.Id, "marker");
                Ref(c.B, c.Id, "marker");
                var a = Resolve(c.A);
                var b = Resolve(c.B);
                if (a != null && b != null && !(Leads(a, b) || Leads(b, a)))
                    Add(ProblemCodes.DanglingReference, c.Id, "the connection " + Q(c.Id) + " joins " + Q(c.A.ToString()) + " and " + Q(c.B.ToString()) + ", but neither marker leads to the other.");
            }
            foreach (var c in Items(d.Crystals))
            {
                Ref(c.Site, c.Id, "site");
                var s = Resolve(c.Site);
                if (s != null && (s.Marker.Kind != MarkerKind.Pedestal || (!Empty(s.Marker.Crystal) && s.Marker.Crystal != c.Id)))
                    Add(ProblemCodes.DanglingReference, c.Id, "the crystal " + Q(c.Id) + " names the site " + Q(c.Site.ToString()) + ", which is not its pedestal.");
            }
            foreach (var t in Items(d.Towers))
            {
                if (d.Location(t.Location) == null) Add(ProblemCodes.DanglingReference, t.Id, "the tower " + Q(t.Id) + " stands at " + Q(t.Location) + ", which is not a location.");
                else if (d.Location(t.Location).Kind != LocationKind.Tower) Add(ProblemCodes.DanglingReference, t.Id, "the tower " + Q(t.Id) + " stands at " + Q(t.Location) + ", which is not a Tower location.");
                LineRef(t.Sign, t.Id, "door sign");
                if (Empty(t.BeamColour)) Add(ProblemCodes.Missing, t.Id, "the tower " + Q(t.Id) + " has no beam colour.");
            }
            foreach (var c in Items(d.Clues))
            {
                ClueSource(c);
                if (!Empty(c.Fallback) && d.Clue(c.Fallback) == null) Add(ProblemCodes.DanglingReference, c.Id, "the clue " + Q(c.Id) + " names the fallback " + Q(c.Fallback) + ", which does not exist.");
                if (c.Gives == null || c.Gives.Count == 0) Add(ProblemCodes.Missing, c.Id, "the clue " + Q(c.Id) + " gives no fact.");
                // CR-10: an essential clue needs a way that no companion's death can close.
                if (c.Essential)
                {
                    var fb = d.Clue(c.Fallback);
                    bool held = c.Source?.Kind == SourceKind.Companion;
                    if (held && (fb == null || fb.Source?.Kind == SourceKind.Companion))
                        Add(ProblemCodes.CompanionOnlyClue, c.Id, "the essential clue " + Q(c.Id) + " is held only by a companion; give it a fallback whose source is not a companion.");
                    else if (!held && fb != null && fb.Source?.Kind == SourceKind.Companion)
                        Add(ProblemCodes.CompanionOnlyClue, c.Id, "the essential clue " + Q(c.Id) + " falls back on a companion; a fallback's source is never a companion.");
                }
            }
            foreach (var l in Items(d.Leads))
            {
                Ref(l.Goal?.At ?? default, l.Id, "goal marker");
                LineRef(l.ReasonLine, l.Id, "reason line");
                CrystalRef(l.Crystal, l.Id, false);
                if (Empty(l.DoneWhen)) Add(ProblemCodes.Missing, l.Id, "the lead " + Q(l.Id) + " has no DoneWhen fact.");
            }
            foreach (var c in Items(d.Choices))
            {
                LineRef(c.QuestionLine, c.Id, "question line");
                LineRef(c.WrongLine, c.Id, "wrong-answer line", false);
                Ref(c.RevealedBy, c.Id, "marker that reveals the answer", false);
                int n = c.Answers?.Count ?? 0, right = 0;
                foreach (var a in Items(c.Answers))
                {
                    LineRef(a.Line, c.Id, "answer line");
                    if (a.Correct) right++;
                    var line = d.Line(a.Line);
                    if (line != null && (line.Text ?? "").Length > CampaignValidator.MaxReplyChars)
                        Add(ValidationCodes.LineTooLong, line.Id, "the reply " + Q(line.Id) + " is longer than " + CampaignValidator.MaxReplyChars + " characters.");
                }
                if (n < 1 || n > 3 || right != 1)
                    Add(ValidationCodes.BadChoice, c.Id, "the choice " + Q(c.Id) + " has " + n + " answers and " + right + " correct; it needs one to three answers, exactly one correct.");
            }
            int finales = 0;
            foreach (var e in Items(d.Encounters))
            {
                bool finale = e.BoundTo == EncounterBindings.LastTower;
                if (finale) finales++;
                if (!Empty(e.BoundTo) && !finale) Add(ValidationCodes.BadEncounter, e.Id, "the encounter " + Q(e.Id) + " is bound to " + Q(e.BoundTo) + "; write \"\" or \"" + EncounterBindings.LastTower + "\".");
                if (Empty(e.Profile)) Add(ValidationCodes.BadEncounter, e.Id, "the encounter " + Q(e.Id) + " names no Ward opponent profile.");
                else if (o.KnownProfiles != null && !o.KnownProfiles.Contains(e.Profile))
                    Add(ProblemCodes.DanglingReference, e.Id, "the encounter " + Q(e.Id) + " names the Ward profile " + Q(e.Profile) + ", which does not exist.");
                if (Empty(e.OpponentActorId) || Empty(e.OpponentName)) Add(ValidationCodes.BadEncounter, e.Id, "the encounter " + Q(e.Id) + " has no opponent actor or opponent name.");
                if (finale && e.Kind != EncounterKind.Lethal) Add(ValidationCodes.BadEncounter, e.Id, "the finale " + Q(e.Id) + " must be Lethal.");
                if (!finale)
                {
                    Ref(e.Threshold, e.Id, "threshold");
                    Ref(e.SafePoint, e.Id, "safe point");
                    if (e.Kind == EncounterKind.Lethal)
                    {
                        Ref(e.Stage?.Caster ?? default, e.Id, "duel caster marker");
                        Ref(e.Stage?.Opponent ?? default, e.Id, "duel opponent marker");
                        Ref(e.Stage?.Volunteer ?? default, e.Id, "volunteer marker", false);
                    }
                }
                foreach (var fx in Items(e.OnVictory)) Effect(fx, e.Id);
                if (!Empty(e.Alternate) && d.Event(e.Alternate) == null) Add(ProblemCodes.DanglingReference, e.Id, "the encounter " + Q(e.Id) + " names the alternate route " + Q(e.Alternate) + ", which is not a quest event.");
            }
            if (finales > 1) Add(ValidationCodes.BadEncounter, "", "there are " + finales + " finales bound to the last tower; there must be one.");
            foreach (var ev in Items(d.Events))
            {
                Ref(ev.At, ev.Id, "marker");
                foreach (var l in Items(ev.Lines)) LineRef(l, ev.Id, "line");
                foreach (var fx in Items(ev.Effects)) Effect(fx, ev.Id);
                foreach (var g in Items(ev.Guards)) Guard(g, ev.Id);
            }
            foreach (var en in Items(d.Endings))
            {
                foreach (var l in Items(en.Lines)) LineRef(l, en.Id, "line");
                foreach (var v in Items(en.Variants)) foreach (var l in Items(v.Lines)) LineRef(l, en.Id, "line");
            }
            foreach (var line in Items(d.Lines))
            {
                if (!Empty(line.Speaker) && !IsPerson(line.Speaker))
                    Add(ProblemCodes.DanglingReference, line.Id, "the line " + Q(line.Id) + " is spoken by " + Q(line.Speaker) + ", who is neither a role nor a companion.");
                foreach (var g in Items(line.Guards)) Guard(g, line.Id);
            }
        }

        /// <summary>True when the spot's marker leads (To) to the other spot.</summary>
        private bool Leads(Spot from, Spot to)
        {
            var target = ResolveText(from.Marker.To);
            return target != null && target.Key == to.Key;
        }

        private void MarkerRefs(MarkerDef m, string owner, string location)
        {
            bool link = m.Kind == MarkerKind.Exit || m.Kind == MarkerKind.Stairs;
            if (link || (m.Kind == MarkerKind.Door && !Empty(m.To))) RefText(m.To, owner, "way through");
            if (m.Kind == MarkerKind.Pedestal || m.Kind == MarkerKind.Altar) CrystalRef(m.Crystal, owner, false);
            else if (!Empty(m.Crystal)) CrystalRef(m.Crystal, owner, false);
        }

        private void ClueSource(ClueDef c)
        {
            var s = c.Source ?? new ClueSourceDef();
            string r = s.Ref ?? "";
            switch (s.Kind)
            {
                case SourceKind.Opening:
                    if (!Empty(r) && d.Role(r) == null) Add(ProblemCodes.DanglingReference, c.Id, "the opening clue " + Q(c.Id) + " names " + Q(r) + ", which is not a role.");
                    break;
                case SourceKind.Companion:
                    if (d.Companion(r) == null) Add(ProblemCodes.DanglingReference, c.Id, "the clue " + Q(c.Id) + " is held by " + Q(r) + ", who is not a companion.");
                    break;
                case SourceKind.Report:
                    CrystalRef(r, c.Id, true);
                    break;
                case SourceKind.Scout:
                    if (d.Location(r) == null && ResolveText(r) == null) Add(ProblemCodes.DanglingReference, c.Id, "the scout clue " + Q(c.Id) + " names " + Q(r) + ", which is neither a location nor a marker.");
                    break;
                case SourceKind.Witness:
                case SourceKind.Wizard:
                    if (d.Role(r) == null) RefText(r, c.Id, "witness");
                    break;
                case SourceKind.Marker:
                    RefText(r, c.Id, "marker");
                    break;
            }
        }

        private void Effect(EffectDef fx, string owner)
        {
            switch (fx.Kind)
            {
                case EffectKind.SetFact:
                    if (Empty(fx.Ref)) Add(ProblemCodes.Missing, owner, Q(owner) + " sets an empty fact.");
                    break;
                case EffectKind.TakeCrystal:
                case EffectKind.SetBelief:
                    CrystalRef(fx.Crystal, owner, true);
                    break;
                case EffectKind.OfferChoice:
                    if (d.Choice(fx.Ref) == null) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " offers the choice " + Q(fx.Ref) + ", which does not exist.");
                    break;
                case EffectKind.Journal:
                    LineRef(fx.Ref, owner, "journal line");
                    break;
                case EffectKind.PlaceParty:
                    Ref(fx.At, owner, "place");
                    break;
                case EffectKind.RivalCharge:
                    if (fx.Amount <= 0) Add(ProblemCodes.Missing, owner, Q(owner) + " charges the rival " + fx.Amount + "; a charge must be positive.");
                    break;
            }
        }

        private void Guard(string g, string owner)
        {
            if (!CampaignGuards.TryParse(g, out var kind, out var subject, out var value))
            {
                Add(ValidationCodes.BadGuard, owner, Q(owner) + " has the guard " + Q(g) + ", which is not one of " + string.Join(", ", CampaignGuards.Kinds) + ".");
                return;
            }
            switch (kind)
            {
                case CampaignGuards.Alive:
                case CampaignGuards.Dead:
                    if (d.Companion(subject) == null) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " has the guard " + Q(g) + ", but " + Q(subject) + " is not a companion.");
                    break;
                case CampaignGuards.Custody:
                    if (d.Crystal(subject) == null) Add(ProblemCodes.DanglingReference, owner, Q(owner) + " has the guard " + Q(g) + ", but " + Q(subject) + " is not a crystal.");
                    if (!Enum.TryParse(value, false, out Custody c) || !Enum.IsDefined(typeof(Custody), c) || int.TryParse(value, out _))
                        Add(ValidationCodes.BadGuard, owner, Q(owner) + " has the guard " + Q(g) + "; the custody must be AtSite, RivalTransit, AtRivalAltar, Carried or Restored.");
                    break;
                case CampaignGuards.Restored:
                    if (!int.TryParse(subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 0 || n > 4)
                        Add(ValidationCodes.BadGuard, owner, Q(owner) + " has the guard " + Q(g) + "; restored takes a count from 0 to 4.");
                    break;
            }
        }

        // ---------------------------------------------------------------- texts: tokens and length

        private void Texts()
        {
            foreach (var line in Items(d.Lines))
            {
                string t = line.Text ?? "";
                if (t.Trim().Length == 0) Add(ProblemCodes.Missing, line.Id, "the line " + Q(line.Id) + " has no text.");
                if (t.Length > CampaignValidator.MaxLineChars)
                    Add(ValidationCodes.LineTooLong, line.Id, "the line " + Q(line.Id) + " has " + t.Length + " characters; a bubble takes " + CampaignValidator.MaxLineChars + ".");
                foreach (var token in Tokens(t, line.Id)) Token(token, line.Id);
            }
        }

        /// <summary>The tokens of a text (without braces); reports an unclosed or stray brace.</summary>
        private List<string> Tokens(string text, string owner)
        {
            var list = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i), close = text.IndexOf('}', i);
                if (open < 0)
                {
                    if (close >= 0) Add(ValidationCodes.BadToken, owner, "the line " + Q(owner) + " has a '}' with no '{'.");
                    break;
                }
                if (close >= 0 && close < open) { Add(ValidationCodes.BadToken, owner, "the line " + Q(owner) + " has a '}' with no '{'."); break; }
                int end = text.IndexOf('}', open + 1);
                if (end < 0) { Add(ValidationCodes.BadToken, owner, "the line " + Q(owner) + " has a '{' that is never closed."); break; }
                list.Add(text.Substring(open + 1, end - open - 1));
                i = end + 1;
            }
            return list;
        }

        private void Token(string token, string owner)
        {
            var p = token.Split('.');
            bool ok;
            switch (p[0])
            {
                case "volunteer": ok = p.Length == 2 && p[1] == "name"; break;
                case "count": ok = p.Length == 2 && (p[1] == "restored" || p[1] == "alive"); break;
                case "companion":
                    ok = p.Length == 3 && p[2] == "name";
                    if (ok && d.Companion(p[1]) == null) { Add(ProblemCodes.DanglingReference, owner, "the line " + Q(owner) + " names the companion " + Q(p[1]) + ", which does not exist."); return; }
                    break;
                case "role":
                    ok = p.Length == 3 && p[2] == "name";
                    if (ok && d.Role(p[1]) == null) { Add(ProblemCodes.DanglingReference, owner, "the line " + Q(owner) + " names the role " + Q(p[1]) + ", which does not exist."); return; }
                    break;
                case "crystal":
                    ok = p.Length == 3 && (p[2] == "name" || p[2] == "place" || p[2] == "trueplace" || p[2] == "tower");
                    if (ok && d.Crystal(p[1]) == null) { Add(ProblemCodes.DanglingReference, owner, "the line " + Q(owner) + " names the crystal " + Q(p[1]) + ", which does not exist."); return; }
                    break;
                default: ok = false; break;
            }
            if (!ok) Add(ValidationCodes.BadToken, owner, "the line " + Q(owner) + " has the token {" + token + "}, which is not one of the tokens of the format.");
        }

        // ---------------------------------------------------------------- roles and companions

        private void Roles()
        {
            foreach (var id in FixedRoles)
            {
                var r = d.Role(id);
                if (r == null || Empty(r.ActorId)) Add(ValidationCodes.RoleUnbound, id, "the role " + Q(id) + " is not bound to an actor.");
            }
            foreach (var r in Items(d.Roles)) if (Empty(r.ActorId)) Add(ValidationCodes.RoleUnbound, r.Id, "the role " + Q(r.Id) + " is not bound to an actor.");
            int n = 0;
            foreach (var _ in Items(d.Companions)) n++;
            if (n != 6)
            {
                Add(ProblemCodes.CompanionCount, "", "there are " + n + " companions; there must be six.");
                return;
            }
            var orders = new List<int>();
            foreach (var k in Items(d.Companions)) orders.Add(k.VolunteerOrder);
            orders.Sort();
            for (int i = 0; i < 6; i++)
                if (orders[i] != i + 1)
                {
                    Add(ProblemCodes.VolunteerOrder, "", "the volunteer orders are " + string.Join(", ", orders) + "; they must be 1 to 6, each once.");
                    break;
                }
        }

        // ---------------------------------------------------------------- crystals and towers

        private void CrystalsAndTowers()
        {
            var elements = new HashSet<Element>();
            int n = 0;
            foreach (var c in Items(d.Crystals)) { n++; elements.Add(c.Element); }
            if (n != 4 || elements.Count != 4) Add(ValidationCodes.CrystalCount, "", "there are " + n + " crystals of " + elements.Count + " elements; there must be four, one per element.");

            var users = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var c in Items(d.Crystals))
            {
                if (Empty(c.Tower) || d.Tower(c.Tower) == null) continue;
                if (!users.TryGetValue(c.Tower, out var l)) users[c.Tower] = l = new List<string>();
                l.Add(c.Id);
            }
            bool underground = false;
            foreach (var c in Items(d.Crystals))
            {
                if (Empty(c.Tower)) Add(ProblemCodes.MissingTower, c.Id, "the crystal " + Q(c.Id) + " has no tower.");
                else if (d.Tower(c.Tower) == null) Add(ProblemCodes.MissingTower, c.Id, "the crystal " + Q(c.Id) + " names the tower " + Q(c.Tower) + ", which does not exist.");
                else if (users[c.Tower].Count > 1)
                    Add(ProblemCodes.SharedTower, c.Tower, "the tower " + Q(c.Tower) + " is named by " + string.Join(" and ", users[c.Tower]) + "; each crystal has its own tower.");
                else if (d.Tower(c.Tower).Element != c.Element)
                    Add(ProblemCodes.MissingTower, c.Id, "the crystal " + Q(c.Id) + " (" + c.Element + ") names the " + d.Tower(c.Tower).Element + " tower " + Q(c.Tower) + "; its tower must match.");
                if (c.Underground) underground = true;
                if (!c.Site.IsEmpty && c.Underground != c.Site.IsInterior)
                    Add(ValidationCodes.Underground, c.Id, "the crystal " + Q(c.Id) + " is " + (c.Underground ? "" : "not ") + "marked Underground, but its site is " + (c.Site.IsInterior ? "inside an interior." : "on the surface."));
            }
            if (n > 0 && !underground) Add(ValidationCodes.Underground, "", "no crystal starts underground; at least one must.");

            foreach (var t in Items(d.Towers))
            {
                var loc = d.Location(t.Location);
                if (loc == null) continue;
                bool caster = false, opponent = false, safe = false;
                foreach (var m in Items(loc.Markers))
                {
                    if (m.Kind == MarkerKind.DuelCaster) caster = true;
                    if (m.Kind == MarkerKind.DuelOpponent) opponent = true;
                    if (m.Kind == MarkerKind.SafePoint) safe = true;
                }
                if (!caster || !opponent || !safe)
                    Add(ProblemCodes.NoFinaleStage, t.Id, "the tower " + Q(t.Id) + " has no " + (!caster ? "DuelCaster marker" : !opponent ? "DuelOpponent marker" : "safe point") + "; every tower must be able to stage the finale.");
            }
        }

        // ---------------------------------------------------------------- the rival plan

        private void Rival()
        {
            var plan = d.Rival;
            if (Empty(plan.LeaderRole) || d.Role(plan.LeaderRole) == null)
                Add(ProblemCodes.RivalPlan, "", "the rival plan is led by " + Q(plan.LeaderRole) + ", which is not a role.");
            var named = new HashSet<string>(StringComparer.Ordinal);
            int k = 0;
            foreach (var step in Items(plan.Steps))
            {
                string owner = "rival step " + (k + 1);
                k++;
                if (d.Crystal(step.Crystal) == null) { Add(ProblemCodes.RivalPlan, step.Crystal, "the " + owner + " names the crystal " + Q(step.Crystal) + ", which does not exist."); continue; }
                if (!named.Add(step.Crystal)) Add(ProblemCodes.RivalPlan, step.Crystal, "the rival plan names " + Q(step.Crystal) + " twice.");
                if (step.MarchCost <= 0 || step.CarryCost <= 0) Add(ProblemCodes.RivalPlan, step.Crystal, "the " + owner + " has a march or carry cost that is not positive.");
                var altar = Resolve(step.Altar);
                if (altar == null) Add(ProblemCodes.RivalPlan, step.Crystal, "the " + owner + " names the altar " + Q(step.Altar.ToString()) + ", which does not exist.");
                else if (altar.Marker.Kind != MarkerKind.Altar || (!Empty(altar.Marker.Crystal) && altar.Marker.Crystal != step.Crystal))
                    Add(ProblemCodes.RivalPlan, step.Crystal, "the " + owner + " names " + Q(step.Altar.ToString()) + ", which is not an altar for " + Q(step.Crystal) + ".");
            }
            foreach (var id in Items(plan.StolenAtStart))
                if (!named.Contains(id)) Add(ProblemCodes.RivalPlan, id, "the crystal " + Q(id) + " is stolen at the start but no plan step names its altar.");
        }

        private RivalStepDef StepFor(string crystal) => d.Rival?.Steps?.Find(s => s != null && s.Crystal == crystal);

        // ---------------------------------------------------------------- places on the island

        private void Places()
        {
            var b = CampaignPlaces.FixedBoard;
            var t = b.Topology;
            var landmarks = CampaignPlaces.SeededLandmarks();
            var hexOwner = new Dictionary<int, string>();
            var cornerOwner = new Dictionary<int, string>();
            int homes = 0;
            foreach (var l in Items(d.Locations))
            {
                var a = l.Anchor;
                if (a == null) { Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " has no anchor."); continue; }
                switch (a.Kind)
                {
                    case AnchorKind.HomeCamp:
                        homes++;
                        if (l.Kind != LocationKind.Camp) Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " is anchored at the home camp but is not a Camp.");
                        if (homes == 2) Add(ProblemCodes.BadAnchor, l.Id, "two locations are anchored at the home camp.");
                        break;
                    case AnchorKind.MeadowHexCentre:
                        MeadowAnchor(l, b, t, hexOwner, landmarks);
                        break;
                    case AnchorKind.RimCorner:
                        RimAnchor(l, b, t, cornerOwner, landmarks);
                        break;
                    default:
                        Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " has an unknown anchor kind.");
                        break;
                }
            }
            if (homes == 0) Add(ProblemCodes.BadAnchor, "", "no location is anchored at the home camp; the party needs a base.");
        }

        private void MeadowAnchor(LocationDef l, Board b, BoardTopology t, Dictionary<int, string> hexOwner, List<Landmark> landmarks)
        {
            var a = l.Anchor;
            int h = t.HexIndex(new HexCoord(a.Q, a.R));
            string where = "(" + a.Q + ", " + a.R + ")";
            if (h < 0) { Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " is anchored at hex " + where + ", which is not on the island."); return; }
            if (b.HexTerrain[h] != Terrain.Meadow)
            {
                Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " is anchored on the " + b.HexTerrain[h] + " hex " + where + "; sites stand on Meadow hexes, which have no number and no stock.");
                return;
            }
            if (hexOwner.TryGetValue(h, out var other)) Add(ProblemCodes.BadAnchor, l.Id, "the locations " + Q(other) + " and " + Q(l.Id) + " share the hex " + where + ".");
            else hexOwner[h] = l.Id;
            foreach (var lm in landmarks)
                if (lm.Hex == h || (lm.Plot >= 0 && PlotLayout.PlotHex(t, lm.Plot) == h))
                    Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " shares the hex " + where + " with the landmark " + Q(lm.Id) + ".");
            double cx = t.HexX(h), cz = t.HexZ(h);
            Lattice.CellOf(cx, cz, out int ai, out int aj);
            foreach (var m in Items(l.Markers))
            {
                double x = Lattice.X(ai + m.I, aj + m.J), z = Lattice.Z(ai + m.I, aj + m.J);
                double dist = Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (dist > CampaignPlaces.SiteRadius + 1e-9)
                    Add(ProblemCodes.BadAnchor, l.Id + "/" + m.Id, "the marker " + Q(l.Id + "/" + m.Id) + " is " + dist.ToString("0.0", CultureInfo.InvariantCulture) + " m from its hex centre; a site keeps within " + CampaignPlaces.SiteRadius + " m.");
                else PlotClear(l, m, t, x, z);
            }
        }

        private void RimAnchor(LocationDef l, Board b, BoardTopology t, Dictionary<int, string> cornerOwner, List<Landmark> landmarks)
        {
            int c = CampaignPlaces.AnchorCorner(d, l, t);
            if (c < 0)
            {
                Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " is anchored at corner " + l.Anchor.Corner + ", which is not a corner of the island"
                    + (l.Anchor.Corner < 0 ? " (the bearing rule needs a tower at the location)." : "."));
                return;
            }
            if (t.VertexHexes(c).Count >= 3)
            {
                Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " is anchored at corner " + c + ", where three hexes meet; only rim corners hold towers and shore places.");
                return;
            }
            if (b.StartCorners != null && Array.IndexOf(b.StartCorners, c) >= 0) Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " is anchored at a start corner.");
            if (cornerOwner.TryGetValue(c, out var other)) Add(ProblemCodes.BadAnchor, l.Id, "the locations " + Q(other) + " and " + Q(l.Id) + " share the corner " + c + ".");
            else cornerOwner[c] = l.Id;
            foreach (var lm in landmarks)
                if (lm.Corner == c) Add(ProblemCodes.BadAnchor, l.Id, "the location " + Q(l.Id) + " stands on corner " + c + ", which is the landmark " + Q(lm.Id) + ".");
            Lattice.CellOf(t.VertexX(c), t.VertexZ(c), out int ai, out int aj);
            foreach (var m in Items(l.Markers))
            {
                double x = Lattice.X(ai + m.I, aj + m.J), z = Lattice.Z(ai + m.I, aj + m.J);
                if (!CampaignPlaces.OnBoard(t, x, z))
                    Add(ProblemCodes.BadAnchor, l.Id + "/" + m.Id, "the marker " + Q(l.Id + "/" + m.Id) + " stands off the island.", false);
                PlotClear(l, m, t, x, z);
            }
        }

        private void PlotClear(LocationDef l, MarkerDef m, BoardTopology t, double x, double z)
        {
            double dist = CampaignPlaces.NearestPlot(t, x, z, out int plot);
            if (plot >= 0 && dist <= PlotLayout.PlotRadius)
                Add(ProblemCodes.BadAnchor, l.Id + "/" + m.Id, "the marker " + Q(l.Id + "/" + m.Id) + " stands on plot " + plot + ".");
        }

        // ---------------------------------------------------------------- interiors (shape; the ways out are in ValidationRoutes)

        private void Interiors()
        {
            foreach (var m in Items(d.Interiors))
            {
                int h = m.Rows?.Count ?? 0, w = 0, floor = 0;
                foreach (var r in Items(m.Rows)) w = Math.Max(w, r.Length);
                if (h == 0 || w == 0) { Add(ValidationCodes.BadInterior, m.Id, "the interior " + Q(m.Id) + " has no rows."); continue; }
                if (h > InteriorMaps.MaxSize || w > InteriorMaps.MaxSize)
                    Add(ValidationCodes.BadInterior, m.Id, "the interior " + Q(m.Id) + " is " + w + " by " + h + " cells; the limit is " + InteriorMaps.MaxSize + " by " + InteriorMaps.MaxSize + ".");
                if (m.Legend != null)
                    foreach (var kv in m.Legend)
                    {
                        if (kv.Key == '#' || kv.Key == '.' || kv.Key == ' ')
                            Add(ValidationCodes.BadInterior, m.Id, "the interior " + Q(m.Id) + " has a legend entry for " + Q(kv.Key.ToString()) + ", which is reserved for rock or floor.");
                        if (kv.Value == null) { Add(ProblemCodes.Missing, m.Id, "the interior " + Q(m.Id) + " has an empty legend entry for " + Q(kv.Key.ToString()) + "."); continue; }
                        bool placed = false;
                        foreach (var r in Items(m.Rows)) if (r.IndexOf(kv.Key) >= 0) placed = true;
                        if (!placed) Add(ValidationCodes.BadInterior, m.Id, "the legend entry " + Q(kv.Key.ToString()) + " of " + Q(m.Id) + " is never placed on the map.", false);
                    }
                var cells = InteriorMaps.FloorCells(m);
                floor = cells.Length / 2;
                if (floor == 0) { Add(ValidationCodes.BadInterior, m.Id, "the interior " + Q(m.Id) + " has no floor."); continue; }
                if (floor > InteriorMaps.MaxFloorCells)
                    Add(ValidationCodes.BadInterior, m.Id, "the interior " + Q(m.Id) + " has " + floor + " floor cells; the limit is " + InteriorMaps.MaxFloorCells + ".");
                // Connected, with every door open: a walled-off room is a mistake whatever the facts.
                int reached = Flood(m, cells[0], cells[1]);
                if (reached != floor)
                    Add(ProblemCodes.InteriorDisconnected, m.Id, "the interior " + Q(m.Id) + " has " + (floor - reached) + " floor cells that cannot be reached from the rest.");
            }
        }

        /// <summary>Floor cells reachable from (i, j) with every door open.</summary>
        private static int Flood(InteriorDef m, int i0, int j0)
        {
            var seenCells = new HashSet<long>();
            var q = new Queue<(int, int)>();
            seenCells.Add(Pack(i0, j0));
            q.Enqueue((i0, j0));
            while (q.Count > 0)
            {
                var (i, j) = q.Dequeue();
                for (int k = 0; k < 6; k++)
                {
                    int ni = i + InteriorMaps.Steps[2 * k], nj = j + InteriorMaps.Steps[2 * k + 1];
                    if (!InteriorMaps.IsFloor(m, ni, nj) || !seenCells.Add(Pack(ni, nj))) continue;
                    q.Enqueue((ni, nj));
                }
            }
            return seenCells.Count;
        }

        internal static long Pack(int i, int j) => ((long)i << 32) ^ (uint)j;

        // ---------------------------------------------------------------- narrative

        private void Narrative()
        {
            var crystalWords = new List<(string word, CrystalDef c)>();
            foreach (var c in Items(d.Crystals))
            {
                string w = Bare(c.Name);
                if (w.Length >= 3) crystalWords.Add((w, c));
            }
            var placeWords = new List<string> { "pedestal", "altar" };
            foreach (var l in Items(d.Locations)) { string w = Bare(l.Name); if (w.Length >= 4) placeWords.Add(w); }
            foreach (var m in Items(d.Interiors)) { string w = Bare(m.Name); if (w.Length >= 4) placeWords.Add(w); }

            foreach (var line in Items(d.Lines))
            {
                var k = d.Companion(line.Speaker);
                if (k != null && (line.Guards == null || !line.Guards.Exists(g => CampaignGuards.TryParse(g, out var kind, out var subject, out _) && kind == CampaignGuards.Alive && subject == k.Id)))
                    Add(ProblemCodes.DeadSpeaker, line.Id, "the line " + Q(line.Id) + " is spoken by the companion " + Q(k.Id) + " but has no \"alive: " + k.Id + "\" guard.");

                string text = StripTokens(line.Text ?? "", out bool namesCrystalByToken);
                string lower = text.ToLowerInvariant();
                bool namesCrystal = namesCrystalByToken;
                foreach (var (word, _) in crystalWords) if (lower.Contains(word)) namesCrystal = true;
                if (!namesCrystal) continue;
                string place = null;
                foreach (var w in placeWords) if (lower.Contains(w)) { place = w; break; }
                if (place == null) continue;
                bool bound = line.Guards != null && line.Guards.Exists(g => CampaignGuards.TryParse(g, out var kind, out _, out _)
                    && (kind == CampaignGuards.Custody || kind == CampaignGuards.Fact));
                if (!bound)
                    Add(ProblemCodes.FixedLocationText, line.Id, "the line " + Q(line.Id) + " names a crystal and the place " + Q(place) + " as fixed text; use {crystal.<id>.place} or bind it with a custody or fact guard.");
            }
        }

        /// <summary>Lower-case name without a leading "the ".</summary>
        private static string Bare(string name)
        {
            string s = (name ?? "").Trim().ToLowerInvariant();
            return s.StartsWith("the ", StringComparison.Ordinal) ? s.Substring(4) : s;
        }

        /// <summary>The text with tokens removed; tells whether a {crystal.x.name} token was among them.</summary>
        private static string StripTokens(string text, out bool crystalName)
        {
            crystalName = false;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                int close = open < 0 ? -1 : text.IndexOf('}', open + 1);
                if (open < 0 || close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i).Append(' ');
                string token = text.Substring(open + 1, close - open - 1);
                if (token.StartsWith("crystal.", StringComparison.Ordinal) && token.EndsWith(".name", StringComparison.Ordinal)) crystalName = true;
                i = close + 1;
            }
            return sb.ToString();
        }
    }
}
