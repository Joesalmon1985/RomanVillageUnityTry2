using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// The validator's reachability engine and the checks built on it: interior ways in and out, every site
    /// discoverable (with and without companions), every theft recoverable, every restoration order, the endings.
    ///
    /// The model (Docs/CAMPAIGN_FORMAT.md, "How the validator reads a story"): the party starts on the surface, where
    /// every location can be walked to. It enters interiors through links (an Exit, Stairs or Door marker whose To names
    /// another marker), each open when its facts are known (the OpenWhen of both markers and of any connection joining
    /// them); inside, a Door cell is passable when its fact is known. Mira goes only where a lead sends her: a marker is
    /// visited when a lead whose facts are known has its goal there and the marker can be reached. A quest event fires
    /// when its marker is visited with its action (an Arrive event on any visit) and its facts and guards hold; a clue
    /// is given when its facts are known and its source is at hand (a visited marker, a role, a living companion, the
    /// scout, a report of that crystal's theft). A keeper encounter is won once its threshold is visited while its
    /// crystal is on the altar. Facts only grow, so the closure is a fixed point.
    /// </summary>
    internal sealed partial class ValidationRun
    {
        private static readonly Custody[] AtSiteSet = { Custody.AtSite, Custody.Carried, Custody.Restored };
        private static readonly Custody[] StolenSet = { Custody.RivalTransit, Custody.AtRivalAltar, Custody.Carried, Custody.Restored };

        private sealed class Link
        {
            public Spot From, To;
            public List<string> Facts = new List<string>();
        }

        private sealed class Reach
        {
            public bool Surface;
            public readonly HashSet<string> Cells = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>What the world is like in one analysis: which crystals the rival holds, whether companions live, how many are restored (-1 = any).</summary>
        private sealed class Scenario
        {
            public HashSet<string> Stolen = new HashSet<string>(StringComparer.Ordinal);
            public bool Companions = true;
            public int Restored = -1;
        }

        private sealed class Closure
        {
            public HashSet<string> Facts;
            public readonly HashSet<string> Obtained = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Restorable = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Thresholds = new HashSet<string>(StringComparer.Ordinal);
            public Reach Reach;
        }

        private List<Link> links;

        private static string CellKey(InteriorDef m, int i, int j) => m.Id + ":" + i + "," + j;

        private static bool IsLinkKind(MarkerDef m) =>
            m.Kind == MarkerKind.Exit || m.Kind == MarkerKind.Stairs || (m.Kind == MarkerKind.Door && !string.IsNullOrEmpty(m.To));

        // ---------------------------------------------------------------- links

        private void BuildLinks()
        {
            links = new List<Link>();
            var froms = new List<Spot>();
            foreach (var l in Items(d.Locations))
                foreach (var m in Items(l.Markers))
                    if (IsLinkKind(m)) froms.Add(new Spot { Location = l.Id, Marker = m, I = m.I, J = m.J });
            foreach (var interior in Items(d.Interiors))
                foreach (var m in InteriorMaps.Markers(interior))
                    if (IsLinkKind(m)) froms.Add(new Spot { Location = interior.Location ?? "", Interior = interior, Marker = m, I = m.I, J = m.J });
            foreach (var from in froms)
            {
                var to = ResolveText(from.Marker.To);
                if (to == null) continue;   // reported as a dangling reference
                var link = new Link { From = from, To = to };
                AddFact(link.Facts, from.Marker.OpenWhen);
                AddFact(link.Facts, to.Marker.OpenWhen);
                foreach (var c in Items(d.Connections))
                {
                    var a = Resolve(c.A);
                    var b = Resolve(c.B);
                    if (a == null || b == null) continue;
                    if ((a.Key == from.Key && b.Key == to.Key) || (a.Key == to.Key && b.Key == from.Key)) AddFact(link.Facts, c.OpenWhen);
                }
                links.Add(link);
            }
        }

        private static void AddFact(List<string> facts, string f)
        {
            if (!string.IsNullOrEmpty(f) && !facts.Contains(f)) facts.Add(f);
        }

        private static bool Open(Link l, Func<string, bool> known)
        {
            foreach (var f in l.Facts) if (!known(f)) return false;
            return true;
        }

        private static bool Passable(InteriorDef m, int i, int j, Func<string, bool> known)
        {
            if (!InteriorMaps.IsFloor(m, i, j)) return false;
            var md = InteriorMaps.MarkerAt(m, i, j);
            return md == null || md.Kind != MarkerKind.Door || string.IsNullOrEmpty(md.OpenWhen) || known(md.OpenWhen);
        }

        /// <summary>Everything reachable from a start (null = the surface) with the given facts.</summary>
        private Reach ReachFrom(Spot start, Func<string, bool> known)
        {
            var r = new Reach();
            var queue = new Queue<(InteriorDef m, int i, int j)>();
            bool surfaceQueued = false;

            void EnterSurface()
            {
                if (r.Surface) return;
                r.Surface = true;
                surfaceQueued = true;
            }

            void EnterCell(InteriorDef m, int i, int j, bool isStart)
            {
                if (!isStart && !Passable(m, i, j, known)) return;
                if (isStart && !InteriorMaps.IsFloor(m, i, j)) return;
                if (r.Cells.Add(CellKey(m, i, j))) queue.Enqueue((m, i, j));
            }

            if (start == null || !start.IsInterior) EnterSurface();
            else EnterCell(start.Interior, start.I, start.J, true);

            while (surfaceQueued || queue.Count > 0)
            {
                if (surfaceQueued)
                {
                    surfaceQueued = false;
                    foreach (var l in links)
                        if (!l.From.IsInterior && l.To.IsInterior && Open(l, known)) EnterCell(l.To.Interior, l.To.I, l.To.J, false);
                    continue;
                }
                var (m, i, j) = queue.Dequeue();
                for (int k = 0; k < 6; k++) EnterCell(m, i + InteriorMaps.Steps[2 * k], j + InteriorMaps.Steps[2 * k + 1], false);
                foreach (var l in links)
                {
                    if (!l.From.IsInterior || l.From.Interior != m || l.From.I != i || l.From.J != j || !Open(l, known)) continue;
                    if (l.To.IsInterior) EnterCell(l.To.Interior, l.To.I, l.To.J, false);
                    else EnterSurface();
                }
            }
            return r;
        }

        private static bool Reached(Reach r, Spot s) => s != null && (s.IsInterior ? r.Cells.Contains(CellKey(s.Interior, s.I, s.J)) : r.Surface);

        // ---------------------------------------------------------------- the closure

        private static bool AllKnown(List<string> facts, HashSet<string> known)
        {
            if (facts == null) return true;
            foreach (var f in facts) if (!string.IsNullOrEmpty(f) && !known.Contains(f)) return false;
            return true;
        }

        private bool Holds(string guard, Scenario sc, HashSet<string> facts)
        {
            if (!CampaignGuards.TryParse(guard, out var kind, out var subject, out var value)) return false;
            switch (kind)
            {
                case CampaignGuards.Alive: return sc.Companions && d.Companion(subject) != null;
                case CampaignGuards.Dead: return !sc.Companions && d.Companion(subject) != null;
                case CampaignGuards.Fact: return facts.Contains(subject);
                case CampaignGuards.NoFact: return true;   // true until the fact is learned: some moment before it exists
                case CampaignGuards.Custody:
                    if (!Enum.TryParse(value, false, out Custody c)) return false;
                    return Array.IndexOf(sc.Stolen.Contains(subject) ? StolenSet : AtSiteSet, c) >= 0;
                case CampaignGuards.Restored:
                    return sc.Restored < 0 || (int.TryParse(subject, out int n) && n == sc.Restored);
                default: return false;
            }
        }

        private static string KeeperCrystal(EncounterDef e)
        {
            foreach (var fx in Items(e.OnVictory)) if (fx.Kind == EffectKind.TakeCrystal) return fx.Crystal;
            return null;
        }

        private bool SourceAvailable(ClueDef c, Scenario sc, HashSet<string> visits)
        {
            var s = c.Source ?? new ClueSourceDef();
            string r = s.Ref ?? "";
            switch (s.Kind)
            {
                case SourceKind.Opening: return true;
                case SourceKind.Companion: return sc.Companions && d.Companion(r) != null;
                case SourceKind.Scout:
                    if (!sc.Companions) return false;
                    foreach (var k in Items(d.Companions)) if (k.Service == CompanionServices.Scout) return true;
                    return false;
                case SourceKind.Report: return sc.Stolen.Contains(r);
                case SourceKind.Witness:
                case SourceKind.Wizard:
                case SourceKind.Marker:
                {
                    if (s.Kind != SourceKind.Marker && d.Role(r) != null) return true;
                    var spot = ResolveText(r);
                    return spot != null && visits.Contains(spot.Key + "|*");
                }
                default: return false;
            }
        }

        private Closure Close(Scenario sc, IEnumerable<string> start)
        {
            var cl = new Closure { Facts = new HashSet<string>(start ?? new string[0], StringComparer.Ordinal) };
            var F = cl.Facts;
            bool changed = true;
            var visits = new HashSet<string>(StringComparer.Ordinal);
            while (changed)
            {
                changed = false;
                cl.Reach = ReachFrom(null, F.Contains);
                visits.Clear();
                foreach (var l in Items(d.Leads))
                {
                    if (!AllKnown(l.Requires, F)) continue;
                    var spot = Resolve(l.Goal?.At ?? default);
                    if (spot == null || !Reached(cl.Reach, spot)) continue;
                    visits.Add(spot.Key + "|" + l.Goal.Action);
                    visits.Add(spot.Key + "|*");
                }
                foreach (var c in Items(d.Clues))
                {
                    if (!AllKnown(c.Requires, F) || !SourceAvailable(c, sc, visits)) continue;
                    foreach (var g in Items(c.Gives)) if (F.Add(g)) changed = true;
                }
                foreach (var ev in Items(d.Events))
                {
                    var spot = Resolve(ev.At);
                    if (spot == null) continue;
                    bool fires = visits.Contains(spot.Key + "|" + ev.Action) || (ev.Action == QuestAction.Arrive && visits.Contains(spot.Key + "|*"));
                    if (!fires || !AllKnown(ev.Requires, F)) continue;
                    bool guards = true;
                    foreach (var g in Items(ev.Guards)) if (!Holds(g, sc, F)) guards = false;
                    if (!guards) continue;
                    foreach (var f in Items(ev.Sets)) if (F.Add(f)) changed = true;
                    foreach (var fx in Items(ev.Effects))
                    {
                        if (fx.Kind == EffectKind.SetFact && F.Add(fx.Ref ?? "")) changed = true;
                        if (fx.Kind == EffectKind.TakeCrystal && !string.IsNullOrEmpty(fx.Crystal)) cl.Obtained.Add(fx.Crystal);
                        if (fx.Kind == EffectKind.OfferChoice)
                        {
                            var ch = d.Choice(fx.Ref);
                            if (ch?.Answers != null && ch.Answers.Exists(a => a != null && a.Correct))
                                foreach (var f in Items(ch.Sets)) if (F.Add(f)) changed = true;
                        }
                    }
                }
                foreach (var e in Items(d.Encounters))
                {
                    if (e.Kind != EncounterKind.Lethal || !string.IsNullOrEmpty(e.BoundTo)) continue;
                    var spot = Resolve(e.Threshold);
                    if (spot == null || !visits.Contains(spot.Key + "|*")) continue;
                    string crystal = KeeperCrystal(e);
                    if (crystal != null && !sc.Stolen.Contains(crystal)) continue;   // the keeper guards an empty altar
                    cl.Thresholds.Add(e.Id);
                    foreach (var fx in Items(e.OnVictory))
                    {
                        if (fx.Kind == EffectKind.SetFact && F.Add(fx.Ref ?? "")) changed = true;
                        if (fx.Kind == EffectKind.TakeCrystal && !string.IsNullOrEmpty(fx.Crystal)) cl.Obtained.Add(fx.Crystal);
                    }
                }
            }
            foreach (var c in Items(d.Crystals))
            {
                var tower = d.Tower(c.Tower);
                if (tower == null) continue;
                foreach (var l in Items(d.Leads))
                    if (l.Goal != null && l.Goal.Action == QuestAction.Restore && !l.Goal.At.IsInterior && l.Goal.At.Location == tower.Location
                        && Resolve(l.Goal.At) != null && AllKnown(l.Requires, F))
                        cl.Restorable.Add(c.Id);
            }
            return cl;
        }

        private Scenario Base(bool companions, string alsoStolen = null, int restored = -1)
        {
            var sc = new Scenario { Companions = companions, Restored = restored };
            foreach (var id in Items(d.Rival.StolenAtStart)) sc.Stolen.Add(id);
            if (alsoStolen != null) sc.Stolen.Add(alsoStolen);
            return sc;
        }

        // ---------------------------------------------------------------- the checks

        private void Routes()
        {
            BuildLinks();
            bool story = StoryIsError;

            // Links lead both ways (a one-way stair is a level with no way back).
            foreach (var l in links)
            {
                var back = ResolveText(l.To.Marker.To);
                if (!IsLinkKind(l.To.Marker) || back == null || back.Key != l.From.Key)
                    Add(ProblemCodes.NoWayOut, l.From.Marker.Id, "the " + l.From.Marker.Kind + " " + Q(Name(l.From)) + " leads to " + Q(Name(l.To)) + ", but there is no way back from it.");
            }

            // Everything any run can learn, for the interior checks ("doors whose facts can be known by then").
            var all = new HashSet<string>(StringComparer.Ordinal);
            all.UnionWith(Close(Base(true), null).Facts);
            foreach (var step in Items(d.Rival.Steps)) if (d.Crystal(step.Crystal) != null) all.UnionWith(Close(Base(true, step.Crystal), null).Facts);
            Func<string, bool> allKnown = all.Contains;

            var fromSurface = ReachFrom(null, allKnown);
            foreach (var m in Items(d.Interiors))
            {
                var cells = InteriorMaps.FloorCells(m);
                bool entered = false;
                for (int k = 0; k < cells.Length; k += 2) if (fromSurface.Cells.Contains(CellKey(m, cells[k], cells[k + 1]))) entered = true;
                if (cells.Length > 0 && !entered) Add(ProblemCodes.NoWayOut, m.Id, "there is no way into the interior " + Q(m.Id) + " from the surface.");
                foreach (var md in InteriorMaps.Markers(m))
                {
                    if (md.Kind != MarkerKind.Pedestal && md.Kind != MarkerKind.Altar && md.Kind != MarkerKind.Threshold && md.Kind != MarkerKind.SafePoint) continue;
                    var spot = new Spot { Location = m.Location ?? "", Interior = m, Marker = md, I = md.I, J = md.J };
                    if (!ReachFrom(spot, allKnown).Surface)
                        Add(ProblemCodes.NoWayOut, m.Id + "/" + md.Id, "from the " + md.Kind + " " + Q(m.Id + "/" + md.Id) + " there is no way back to the surface.");
                }
            }

            // Each crystal: discovered first, and recovered when stolen; both also with no companion alive.
            var broken = new HashSet<string>(StringComparer.Ordinal);
            var stolenAtStart = new HashSet<string>(Items(d.Rival.StolenAtStart), StringComparer.Ordinal);
            foreach (var c in Items(d.Crystals))
            {
                if (string.IsNullOrEmpty(c.Id) || Resolve(c.Site) == null) { broken.Add(c.Id ?? ""); continue; }
                if (!stolenAtStart.Contains(c.Id))
                {
                    if (!Close(Base(true), null).Obtained.Contains(c.Id))
                    {
                        Add(ProblemCodes.UnreachableClue, c.Id, "from the opening facts nothing leads the party to " + Q(Name(c.Site)) + " to take " + c.Name + ".", story);
                        broken.Add(c.Id);
                        continue;
                    }
                    if (!Close(Base(false), null).Obtained.Contains(c.Id))
                        Add(ProblemCodes.CompanionOnlyClue, c.Id, "with no companion alive nothing leads the party to " + c.Name + "; an essential step depends on a companion.", story);
                }
                if (StepFor(c.Id) == null) continue;
                var keepers = new List<EncounterDef>();
                foreach (var e in Items(d.Encounters))
                    if (e.Kind == EncounterKind.Lethal && string.IsNullOrEmpty(e.BoundTo) && KeeperCrystal(e) == c.Id) keepers.Add(e);
                // An alternate recovery: an event that takes the crystal while it is on the altar.
                bool alternate = false;
                foreach (var ev in Items(d.Events))
                    foreach (var fx in Items(ev.Effects))
                        if (fx.Kind == EffectKind.TakeCrystal && fx.Crystal == c.Id)
                            foreach (var g in Items(ev.Guards))
                                if (CampaignGuards.TryParse(g, out var kind, out var subject, out var value) && kind == CampaignGuards.Custody
                                    && subject == c.Id && value == nameof(Custody.AtRivalAltar))
                                    alternate = true;
                if (keepers.Count == 0 && !alternate)
                {
                    Add(ProblemCodes.NoRecovery, c.Id, "no lethal keeper encounter wins " + c.Name + " back from its altar.", story);
                    broken.Add(c.Id);
                    continue;
                }
                bool keeperOk = alternate;
                foreach (var e in keepers) if (Resolve(e.Threshold) != null && Resolve(e.SafePoint) != null) keeperOk = true;
                if (!keeperOk)
                {
                    Add(ProblemCodes.NoRecovery, c.Id, "the keeper encounter of " + c.Name + " has no threshold or no safe point.", story);
                    broken.Add(c.Id);
                    continue;
                }
                if (!Close(Base(true, c.Id), null).Obtained.Contains(c.Id))
                {
                    Add(ProblemCodes.NoRecovery, c.Id, "if the Ash Clan takes " + c.Name + ", no trail leads from the empty pedestal to its altar " + Q(StepFor(c.Id).Altar.ToString()) + ".", story);
                    broken.Add(c.Id);
                    continue;
                }
                if (!Close(Base(false, c.Id), null).Obtained.Contains(c.Id))
                    Add(ProblemCodes.CompanionOnlyClue, c.Id, "with no companion alive the trail to " + c.Name + " on its altar cannot be followed.", story);
            }

            Orders(broken, story);
            Endings(story);
        }

        private string Name(Spot s) => s.IsInterior ? s.Location + "/" + s.Interior.Id + "/" + s.Marker.Id : s.Location + "/" + s.Marker.Id;

        private string Name(MarkerRef r) => r.ToString();

        /// <summary>Every restoration order of the routes that work reaches the finale at the tower of its fourth crystal.</summary>
        private void Orders(HashSet<string> broken, bool story)
        {
            // Routes that work and whose tower is sound (a missing, shared or mismatched tower is reported already).
            var routes = new List<CrystalDef>();
            var towerUse = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in Items(d.Crystals)) if (!string.IsNullOrEmpty(c.Tower)) { towerUse.TryGetValue(c.Tower, out int u); towerUse[c.Tower] = u + 1; }
            foreach (var c in Items(d.Crystals))
            {
                var tower = d.Tower(c.Tower);
                if (broken.Contains(c.Id) || tower == null || tower.Element != c.Element || towerUse[c.Tower] > 1) continue;
                routes.Add(c);
            }
            if (routes.Count == 0) return;
            var order = new int[routes.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            bool failed = false;
            do
            {
                var facts = new HashSet<string>(StringComparer.Ordinal);
                var names = new List<string>();
                foreach (int i in order) names.Add(routes[i].Id);
                for (int k = 0; k < order.Length; k++)
                {
                    var c = routes[order[k]];
                    var sc = Base(true, null, k);
                    if (d.Rival.StolenAtStart != null && d.Rival.StolenAtStart.Contains(c.Id)) sc.Stolen.Add(c.Id);
                    var cl = Close(sc, facts);
                    if (!cl.Obtained.Contains(c.Id) || !cl.Restorable.Contains(c.Id))
                    {
                        Add(ValidationCodes.FinaleUnreachable, c.Id, "restoring in the order " + string.Join(", ", names) + " gets stuck at " + c.Name
                            + (cl.Obtained.Contains(c.Id) ? ": no lead takes it to its tower with Action Restore." : ": it cannot be found then."), story);
                        failed = true;
                        break;
                    }
                    facts = cl.Facts;
                }
            } while (!failed && NextPermutation(order));
            // With fewer than four sound routes the reason is reported already (a route, a tower, the crystal count).
        }

        private void Endings(bool story)
        {
            bool finale = false;
            foreach (var e in Items(d.Encounters)) if (e.Kind == EncounterKind.Lethal && e.BoundTo == EncounterBindings.LastTower) finale = true;
            if (!finale) Add(ValidationCodes.FinaleUnreachable, "", "there is no Lethal finale encounter bound to \"" + EncounterBindings.LastTower + "\".", story);

            bool won = false, lost = false;
            foreach (var e in Items(d.Endings))
            {
                if (e.Outcome == CampaignPhase.Won) won = true;
                if (e.Outcome == CampaignPhase.Lost) lost = true;
            }
            if (!won) Add(ValidationCodes.NoEnding, "","there is no restored ending (an ending with Outcome Won).", story);
            if (!lost) Add(ValidationCodes.NoEnding, "", "there is no terminal defeat (an ending with Outcome Lost).", story);
            bool lethal = false;
            foreach (var e in Items(d.Encounters)) if (e.Kind == EncounterKind.Lethal) lethal = true;
            if (lost && !lethal) Add(ValidationCodes.NoEnding, "", "there is no lethal encounter, so the terminal defeat can never happen.", story);
        }

        /// <summary>Lexicographic next permutation in place; false after the last.</summary>
        private static bool NextPermutation(int[] a)
        {
            int i = a.Length - 2;
            while (i >= 0 && a[i] >= a[i + 1]) i--;
            if (i < 0) return false;
            int j = a.Length - 1;
            while (a[j] <= a[i]) j--;
            (a[i], a[j]) = (a[j], a[i]);
            Array.Reverse(a, i + 1, a.Length - i - 1);
            return true;
        }
    }
}
