using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Facts, clues, quest events, leads and Mira's queue (5.4). A clue gives its facts when its requirements are known
    /// and its source speaks: an opening clue at once, a companion while that companion lives, a witness, marker or the
    /// wizard when the party acts at that marker, a scout when she reports from that place, a report when word of that
    /// crystal arrives. A lead is open when its facts are known, its goal is not done, it is not declined or spent, its
    /// crystal still needs it and its goal can be reached. Every PlanInterval ticks (and at once after a change) Mira
    /// scores the open leads (Priority, +30 if the rival is reported marching on that crystal, +20 if the party carries
    /// that crystal and the lead is its tower, minus 1 per 20 steps of travel), keeps the best QueueLength, and keeps
    /// queue ids stable while a lead stays in the queue.
    ///
    /// Where the pack has no lead for something the runtime must always be able to do, it makes one (ids start with
    /// '@', never written into the definition): fetch a crystal the party believes is on its pedestal ("@take:"),
    /// win back a crystal the party knows is on an altar ("@recover:"), take a carried crystal to its tower
    /// ("@restore:"), hear a clue from a source that is not a companion ("@clue:", so every essential clue stays
    /// reachable with no companion left) and go back to a choice left unanswered ("@choice:"). Each is made only when no
    /// authored lead already has that goal, so an authored pack keeps its own chain.
    /// </summary>
    internal static partial class CampaignRuntime
    {
        internal const string ImplicitPrefix = "@";
        /// <summary>A SpentLead signature meaning "spent until the player rejoins" (a tower she already waited at alone).</summary>
        internal const long AwaySpent = long.MinValue;
        private const int TakePriority = 50, RecoverPriority = 60, RestorePriority = 60, CluePriority = 45, ChoicePriority = 55;

        // ---------------------------------------------------------------- facts and clues

        /// <summary>Gives a clue's facts if its requirements are known (false when it gave nothing new).</summary>
        internal static bool GiveClue(CampaignState c, ClueDef clue, EventBuffer ev)
        {
            if (clue == null || !AllKnown(c, clue.Requires)) return false;
            bool any = false;
            foreach (var f in clue.Gives ?? new List<string>()) any |= Learn(c, f, clue.Source?.Kind ?? SourceKind.Witness, ev);
            return any;
        }

        private static bool AllKnown(CampaignState c, List<string> facts)
        {
            if (facts == null) return true;
            foreach (var f in facts) if (!string.IsNullOrEmpty(f) && !c.Knows(f)) return false;
            return true;
        }

        /// <summary>Clues that need nobody to go anywhere: opening clues with requirements, and living companions' clues.</summary>
        private static void AutoClues(CampaignState c, EventBuffer ev)
        {
            for (int round = 0; round < 4; round++)
            {
                bool any = false;
                foreach (var clue in c.Definition.Clues ?? new List<ClueDef>())
                {
                    var k = clue.Source?.Kind;
                    if (k == SourceKind.Opening) any |= GiveClue(c, clue, ev);
                    else if (k == SourceKind.Companion && (c.Companion(clue.Source.Ref)?.Alive ?? false)) any |= GiveClue(c, clue, ev);
                }
                if (!any) return;
            }
        }

        /// <summary>The marker a witness, marker or wizard clue is heard at (false for other sources or a bad ref).</summary>
        private static bool ClueMarker(ClueDef clue, out MarkerRef at)
        {
            at = default;
            var k = clue?.Source?.Kind;
            return (k == SourceKind.Witness || k == SourceKind.Marker || k == SourceKind.Wizard) && MarkerRef.TryParse(clue.Source.Ref, out at);
        }

        /// <summary>Clues heard by acting at this marker.</summary>
        private static void GiveCluesAt(CampaignState c, MarkerRef at, EventBuffer ev)
        {
            foreach (var clue in c.Definition.Clues ?? new List<ClueDef>())
                if (ClueMarker(clue, out var m) && Same(m, at)) GiveClue(c, clue, ev);
        }

        // ---------------------------------------------------------------- quest events and effects

        /// <summary>Fires every quest event at this place for this action whose facts and guards hold, in definition order.</summary>
        internal static void FireEvents(WorldState s, CampaignState c, MarkerRef at, QuestAction action, EventBuffer ev)
        {
            foreach (var qe in c.Definition.Events ?? new List<QuestEventDef>())
            {
                if (c.Phase != CampaignPhase.Running) return;
                if (qe == null || qe.Action != action || !Same(qe.At, at)) continue;
                if (qe.Once && c.FiredEvents.BinarySearch(qe.Id ?? "", StringComparer.Ordinal) >= 0) continue;
                if (!AllKnown(c, qe.Requires) || !CampaignGuards.AllHold(c, qe.Guards)) continue;
                if (qe.Once) InsertSorted(c.FiredEvents, qe.Id ?? "");
                ev?.Add(new QuestEventFired { EventId = qe.Id });
                foreach (var f in qe.Sets ?? new List<string>()) Learn(c, f, SourceKind.Marker, ev);
                string said = null;
                foreach (var lineId in qe.Lines ?? new List<string>()) { said = CampaignApi.LineOf(c, lineId); if (said != null) break; }
                Journal(s, c, said ?? GenericDeed(c, action, at), ev);
                foreach (var fx in qe.Effects ?? new List<EffectDef>()) ApplyEffect(s, c, fx, ev);
                c.ReplanNow = true;
            }
        }

        private static string GenericDeed(CampaignState c, QuestAction action, MarkerRef at)
        {
            string place = CampaignTokens.PlaceName(c.Definition, at);
            string verb;
            switch (action)
            {
                case QuestAction.Arrive: verb = "reached"; break;
                case QuestAction.Talk: verb = "talked with people at"; break;
                case QuestAction.Search: verb = "searched"; break;
                case QuestAction.Open: verb = "opened the way at"; break;
                case QuestAction.Light: verb = "lit the fire at"; break;
                case QuestAction.Take: verb = "took what lay at"; break;
                case QuestAction.Answer: verb = "gave her answer at"; break;
                case QuestAction.Restore: verb = "set a crystal at"; break;
                default: verb = "studied the marks at"; break;
            }
            return "Mira " + verb + " " + (place.Length > 0 ? place : "the place") + ".";
        }

        internal static void ApplyEffect(WorldState s, CampaignState c, EffectDef fx, EventBuffer ev)
        {
            if (fx == null || c.Phase != CampaignPhase.Running) return;
            var d = c.Definition;
            switch (fx.Kind)
            {
                case EffectKind.SetFact:
                    Learn(c, fx.Ref, SourceKind.Marker, ev);
                    break;
                case EffectKind.TakeCrystal:
                {
                    var cd = d.Crystal(fx.Crystal);
                    if (cd != null) TakeCrystal(s, c, cd.Element, ev);
                    break;
                }
                case EffectKind.OfferChoice:
                    OfferChoice(s, c, fx.Ref, ev);
                    break;
                case EffectKind.Journal:
                    Journal(s, c, CampaignApi.LineOf(c, fx.Ref), ev);
                    break;
                case EffectKind.PlaceParty:
                    if (!fx.At.IsEmpty)
                    {
                        var p = c.Party;
                        p.At = fx.At;
                        p.Leg = null;
                        p.Route.Clear();
                        ev?.Add(new PartyArrived { At = fx.At });
                    }
                    break;
                case EffectKind.RivalCharge:
                    Charge(s, c, fx.Amount, RivalCharges.WrongAnswer, ev);
                    break;
                case EffectKind.SetBelief:
                {
                    var cd = d.Crystal(fx.Crystal);
                    if (cd != null) SetBeliefTruth(s, c, cd.Element, SourceKind.Marker, ev);
                    break;
                }
            }
        }

        /// <summary>The party takes a crystal from its pedestal or (after a won duel, or an alternate route) from an altar.</summary>
        internal static bool TakeCrystal(WorldState s, CampaignState c, Element e, EventBuffer ev)
        {
            var st = c.Crystal(e);
            if (st == null || (st.Custody != Custody.AtSite && st.Custody != Custody.AtRivalAltar)) return false;
            bool fromAltar = st.Custody == Custody.AtRivalAltar;
            var at = c.Party.At;
            SetCustody(s, e, Custody.Carried, at, ev);
            SetBelief(s, c, e, Custody.Carried, at, SourceKind.Marker, ev);
            var name = c.Definition.Crystal(e)?.Name ?? e.ToString();
            string place = CampaignTokens.PlaceName(c.Definition, at);
            Journal(s, c, "Mira took " + name + (fromAltar ? " from the Ash altar at " : " from its pedestal at ") + (place.Length > 0 ? place : "the site") + ".", ev);
            return true;
        }

        // ---------------------------------------------------------------- leads

        /// <summary>A number that changes whenever the party learns or achieves something (spent leads wait for it to change).</summary>
        internal static long Signature(CampaignState c)
        {
            long h = c.Facts.Count;
            h = h * 31 + c.FiredEvents.Count;
            h = h * 31 + c.CompletedEncounters.Count;
            h = h * 31 + c.OpenChoices.Count;
            int alive = 0;
            foreach (var k in c.Companions) if (k.Alive) alive++;
            h = h * 31 + alive;
            for (int i = 0; i < 4; i++) h = h * 7 + (int)(c.Crystal((Element)i)?.Custody ?? 0);
            return h;
        }

        /// <summary>The lead definition behind a queue entry: the pack's, or one the runtime makes (ids starting with '@').</summary>
        internal static LeadDef LeadDefOf(CampaignState c, string defId) => c?.Definition?.Lead(defId) ?? ImplicitLead(c, defId);

        /// <summary>
        /// The runtime-made lead for an '@' id (null otherwise): "@take:&lt;crystal&gt;", "@recover:&lt;crystal&gt;",
        /// "@restore:&lt;crystal&gt;", "@clue:&lt;clue&gt;", "@choice:&lt;choice&gt;". Built fresh from the definition.
        /// </summary>
        internal static LeadDef ImplicitLead(CampaignState c, string defId)
        {
            var d = c?.Definition;
            if (d == null || string.IsNullOrEmpty(defId) || !defId.StartsWith(ImplicitPrefix, StringComparison.Ordinal)) return null;
            int colon = defId.IndexOf(':');
            if (colon < 0) return null;
            string kind = defId.Substring(1, colon - 1), arg = defId.Substring(colon + 1);
            switch (kind)
            {
                case "take":
                {
                    var cd = d.Crystal(arg);
                    if (cd == null) return null;
                    string place = CampaignTokens.PlaceName(d, cd.Site);
                    return new LeadDef
                    {
                        Id = defId, Title = "Fetch " + cd.Name, Goal = new GoalDef { At = cd.Site, Action = QuestAction.Take },
                        Priority = TakePriority, Crystal = cd.Id, ReasonText = Cap(cd.Name) + " should still be at " + place + ".",
                    };
                }
                case "recover":
                {
                    var cd = d.Crystal(arg);
                    var e = cd != null ? KeeperOf(d, cd.Id) : null;
                    if (e == null) return null;
                    string keeper = string.IsNullOrEmpty(e.OpponentName) ? "Its keeper" : e.OpponentName;
                    return new LeadDef
                    {
                        Id = defId, Title = "Win back " + cd.Name, Goal = new GoalDef { At = e.Threshold, Action = QuestAction.Arrive },
                        Priority = RecoverPriority, Crystal = cd.Id,
                        ReasonText = Cap(keeper) + " holds " + cd.Name + " at " + CampaignTokens.PlaceName(d, e.Threshold) + ".",
                    };
                }
                case "restore":
                {
                    var cd = d.Crystal(arg);
                    var tower = cd != null ? d.Tower(cd.Tower) : null;
                    var loc = tower != null ? d.Location(tower.Location) : null;
                    if (loc == null) return null;
                    return new LeadDef
                    {
                        Id = defId, Title = "Set " + cd.Name, Goal = new GoalDef { At = LocationGoal(loc), Action = QuestAction.Arrive },
                        Priority = RestorePriority, Crystal = cd.Id, ReasonText = Cap(cd.Name) + " belongs in " + loc.Name + ".",
                    };
                }
                case "clue":
                {
                    var clue = d.Clue(arg);
                    if (!ClueMarker(clue, out var at)) return null;
                    string place = CampaignTokens.PlaceName(d, at);
                    bool marker = clue.Source.Kind == SourceKind.Marker;
                    return new LeadDef
                    {
                        Id = defId, Title = (marker ? "Read the marks at " : "Ask at ") + place,
                        Goal = new GoalDef { At = at, Action = marker ? QuestAction.Search : QuestAction.Talk }, Priority = CluePriority,
                        ReasonText = marker ? "The marks at " + place + " may show the way." : "Someone at " + place + " may know more.",
                    };
                }
                case "choice":
                {
                    var oc = c.OpenChoices.Find(x => x.ChoiceId == arg);
                    if (oc == null || d.Choice(arg) == null) return null;
                    return new LeadDef
                    {
                        Id = defId, Title = "Back to the question", Goal = new GoalDef { At = oc.At, Action = QuestAction.Answer },
                        Priority = ChoicePriority, ReasonText = "A question at " + CampaignTokens.PlaceName(d, oc.At) + " is still unanswered.",
                    };
                }
                default: return null;
            }
        }

        private static string Cap(string x) => string.IsNullOrEmpty(x) ? "" : char.ToUpperInvariant(x[0]) + x.Substring(1);

        /// <summary>The keeper encounter whose victory takes this crystal (lethal, at a place), null when none.</summary>
        internal static EncounterDef KeeperOf(CampaignDefinition d, string crystalId)
        {
            foreach (var e in d.Encounters ?? new List<EncounterDef>())
            {
                if (e == null || e.Kind != EncounterKind.Lethal || e.BoundTo == EncounterBindings.LastTower) continue;
                foreach (var fx in e.OnVictory ?? new List<EffectDef>())
                    if (fx != null && fx.Kind == EffectKind.TakeCrystal && fx.Crystal == crystalId) return e;
            }
            return null;
        }

        /// <summary>The crystal a keeper encounter guards ("" when none).</summary>
        internal static string KeeperCrystal(EncounterDef e)
        {
            foreach (var fx in e?.OnVictory ?? new List<EffectDef>())
                if (fx != null && fx.Kind == EffectKind.TakeCrystal) return fx.Crystal ?? "";
            return "";
        }

        private static bool AuthoredGoal(CampaignDefinition d, Func<MarkerRef, bool> match)
        {
            foreach (var l in d.Leads ?? new List<LeadDef>()) if (l?.Goal != null && match(l.Goal.At)) return true;
            return false;
        }

        /// <summary>The '@' leads that may apply now (openness is checked like any lead's).</summary>
        private static List<string> ImplicitIds(CampaignState c)
        {
            var d = c.Definition;
            var ids = new List<string>();
            foreach (var cd in d.Crystals ?? new List<CrystalDef>())
            {
                if (cd == null) continue;
                var st = c.Crystal(cd.Element);
                var b = c.BeliefOf(cd.Element);
                if (st == null || b == null) continue;
                if (b.Custody == Custody.AtSite && Same(b.At, cd.Site) && !AuthoredGoal(d, g => Same(g, cd.Site)))
                    ids.Add("@take:" + cd.Id);
                var keeper = KeeperOf(d, cd.Id);
                if (b.Custody == Custody.AtRivalAltar && keeper != null && !keeper.Threshold.IsEmpty && !c.CompletedEncounters.Contains(keeper.Id)
                    && !AuthoredGoal(d, g => Same(g, keeper.Threshold)))
                    ids.Add("@recover:" + cd.Id);
                var tower = d.Tower(cd.Tower);
                if (st.Custody == Custody.Carried && tower != null && !AuthoredGoal(d, g => !g.IsInterior && g.Location == tower.Location))
                    ids.Add("@restore:" + cd.Id);
            }
            foreach (var clue in d.Clues ?? new List<ClueDef>())
            {
                if (!ClueMarker(clue, out var at) || !AllKnown(c, clue.Requires) || AllKnown(c, clue.Gives)) continue;
                if (!AuthoredGoal(d, g => Same(g, at))) ids.Add("@clue:" + clue.Id);
            }
            foreach (var oc in c.OpenChoices) ids.Add("@choice:" + oc.ChoiceId);
            return ids;
        }

        /// <summary>Everything but reachability: facts, done, declined, spent, the crystal still needs it, Mira may take it.</summary>
        private static bool LeadOpen(WorldState s, CampaignState c, LeadDef l, long signature)
        {
            if (l?.Goal == null || l.Goal.At.IsEmpty) return false;
            if (!AllKnown(c, l.Requires)) return false;
            if (!string.IsNullOrEmpty(l.DoneWhen) && c.Knows(l.DoneWhen)) return false;
            foreach (var dl in c.Declined) if (dl.DefId == l.Id && dl.UntilTick > s.Tick) return false;
            foreach (var sp in c.Spent)
                if (sp.DefId == l.Id && (sp.Signature == signature || (sp.Signature == AwaySpent && !c.Party.PlayerPresent))) return false;
            var d = c.Definition;
            var p = c.Party;
            if (!string.IsNullOrEmpty(l.Crystal))
            {
                var cd = d.Crystal(l.Crystal);
                var st = cd != null ? c.Crystal(cd.Element) : null;
                if (st != null)
                {
                    if (st.Custody == Custody.Restored) return false;
                    var tower = d.Tower(cd.Tower);
                    bool toTower = tower != null && !l.Goal.At.IsInterior && l.Goal.At.Location == tower.Location;
                    if (st.Custody == Custody.Carried && !toTower) return false;
                    // Never send the party to the tower door it is already standing at with its crystal.
                    if (toTower && st.Custody == Custody.Carried && p.Mode != PartyMode.Travelling && Same(p.At, l.Goal.At)) return false;
                }
            }
            if (l.Id.StartsWith("@choice:", StringComparison.Ordinal) && p.Mode == PartyMode.AskingChoice && Same(p.ChoiceAt, l.Goal.At)) return false;
            // While the player is away she does not walk back into the fight she stepped back from (5.5).
            if (!p.PlayerPresent && !string.IsNullOrEmpty(p.WithdrawnFrom))
            {
                var e = d.Encounter(p.WithdrawnFrom);
                if (e != null && Same(ThresholdOf(c, e), l.Goal.At)) return false;
            }
            return true;
        }

        private struct Candidate
        {
            public string Id;
            public LeadDef Def;
            public int Score;
        }

        /// <summary>Every open, reachable lead with its score, best first (score, then id ordinal).</summary>
        private static List<Candidate> Candidates(WorldState s, CampaignState c)
        {
            var d = c.Definition;
            var p = c.Party;
            var origin = p.Mode == PartyMode.Travelling && p.Leg != null ? p.Leg.To : p.At;
            var map = new RouteMap(s, c, origin);
            long sig = Signature(c);
            var list = new List<Candidate>();
            void Consider(LeadDef l)
            {
                if (!LeadOpen(s, c, l, sig)) return;
                int steps = map.StepsTo(l.Goal.At);
                if (steps < 0) return;
                int score = l.Priority - steps / 20;
                var cd = string.IsNullOrEmpty(l.Crystal) ? null : d.Crystal(l.Crystal);
                if (cd != null)
                {
                    if (ReportedMarching(s, c, cd.Element)) score += 30;
                    var tower = d.Tower(cd.Tower);
                    if (c.Crystal(cd.Element)?.Custody == Custody.Carried && tower != null && !l.Goal.At.IsInterior && l.Goal.At.Location == tower.Location)
                        score += 20;
                }
                list.Add(new Candidate { Id = l.Id, Def = l, Score = score });
            }
            foreach (var l in d.Leads ?? new List<LeadDef>()) if (l != null && !string.IsNullOrEmpty(l.Id)) Consider(l);
            foreach (var id in ImplicitIds(c)) { var l = ImplicitLead(c, id); if (l != null) Consider(l); }
            list.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>The newest report about this crystal that has reached the party says the Ash runners are marching on it.</summary>
        private static bool ReportedMarching(WorldState s, CampaignState c, Element e)
        {
            RivalReport last = null;
            for (int i = 0; i < c.ReportsRaised && i < c.Reports.Count; i++)
                if (c.Reports[i].Crystal == e) last = c.Reports[i];
            return last != null && last.Kind == RivalReportKind.Marching;
        }

        /// <summary>Mira's plan (5.4): auto clues, then the queue of the best QueueLength open leads, ids kept stable.</summary>
        private static void Plan(WorldState s, CampaignState c, EventBuffer ev)
        {
            c.ReplanNow = false;
            AutoClues(c, ev);
            c.Declined.RemoveAll(x => x.UntilTick <= s.Tick);
            long sig = Signature(c);
            c.Spent.RemoveAll(x => x.Signature != sig && !(x.Signature == AwaySpent && !c.Party.PlayerPresent));
            var cands = Candidates(s, c);
            int n = Math.Max(1, c.Definition.Tuning.QueueLength);
            var top = cands.Count > n ? cands.GetRange(0, n) : cands;

            int oldTop = -1;
            foreach (var l in c.Leads) if (l.Rank == 0) oldTop = l.Id;
            var next = new List<LeadState>();
            foreach (var l in c.Leads)
            {
                if (top.Exists(x => x.Id == l.DefId)) continue;
                var def = LeadDefOf(c, l.DefId);
                string why = def != null && !string.IsNullOrEmpty(def.DoneWhen) && c.Knows(def.DoneWhen) ? "done"
                    : cands.Exists(x => x.Id == l.DefId) ? "dropped" : "closed";
                ev?.Add(new LeadRemoved { LeadId = l.Id, Why = why });
            }
            for (int rank = 0; rank < top.Count; rank++)
            {
                var cand = top[rank];
                var l = c.Leads.Find(x => x.DefId == cand.Id);
                bool added = l == null;
                if (added) l = new LeadState { Id = c.NextLeadId++, DefId = cand.Id, TopSinceTick = -1 };
                l.Score = cand.Score;
                l.Rank = rank;
                next.Add(l);
                if (added) ev?.Add(new LeadAdded { LeadId = l.Id, DefId = l.DefId, Rank = rank });
            }
            c.Leads = next;
            int newTop = next.Count > 0 ? next[0].Id : -1;
            if (newTop != oldTop)
            {
                if (newTop >= 0) next[0].TopSinceTick = s.Tick;
                ev?.Add(new LeadsReordered { TopLeadId = newTop });
            }
        }

        /// <summary>"Not that way": the lead leaves the queue and waits DeclineCooldown (5.5).</summary>
        private static void Decline(WorldState s, CampaignState c, int leadId, EventBuffer ev)
        {
            var l = c.Leads.Find(x => x.Id == leadId);
            if (l == null) return;
            c.Leads.Remove(l);
            c.Declined.RemoveAll(x => x.DefId == l.DefId);
            c.Declined.Add(new DeclinedLead { DefId = l.DefId, UntilTick = s.Tick + Math.Max(0, c.Definition.Tuning.DeclineCooldown) });
            ev?.Add(new LeadRemoved { LeadId = l.Id, Why = "declined" });
            for (int i = 0; i < c.Leads.Count; i++) c.Leads[i].Rank = i;
            if (c.Leads.Count > 0) c.Leads[0].TopSinceTick = s.Tick;
            ev?.Add(new LeadsReordered { TopLeadId = c.Leads.Count > 0 ? c.Leads[0].Id : -1 });
            c.ReplanNow = true;
        }

        /// <summary>Sets out on a lead ("Lead the way", or Mira going on by herself when nobody answers).</summary>
        private static void TakeLead(WorldState s, CampaignState c, LeadState l, bool byPlayer, EventBuffer ev)
        {
            if (l == null) return;
            var def = LeadDefOf(c, l.DefId);
            if (def?.Goal == null) return;
            if (!byPlayer)
            {
                string place = CampaignTokens.PlaceName(c.Definition, def.Goal.At);
                Journal(s, c, "Mira went on by herself to " + (place.Length > 0 ? place : "the next place") + ": " + (def.Title ?? "") + ".", ev);
            }
            if (!SetOut(s, c, def.Goal.At, RoutePurpose.Lead, l.Id, def.Id, def.Goal.Action, ev)) MarkSpent(c, def.Id);
        }

        private static void MarkSpent(CampaignState c, string defId, bool away = false)
        {
            c.Spent.RemoveAll(x => x.DefId == defId);
            c.Spent.Add(new SpentLead { DefId = defId, Signature = away ? AwaySpent : Signature(c) });
            c.ReplanNow = true;
        }

        /// <summary>The followed lead has been acted on: done, or spent when the visit changed nothing.</summary>
        private static void FinishLead(WorldState s, CampaignState c, bool progressed, EventBuffer ev)
        {
            var p = c.Party;
            if (string.IsNullOrEmpty(p.LeadDefId)) { p.LeadId = -1; return; }
            var def = LeadDefOf(c, p.LeadDefId);
            bool done = def != null && !string.IsNullOrEmpty(def.DoneWhen) && c.Knows(def.DoneWhen);
            if (!done && !progressed && Signature(c) == p.ArriveSignature) MarkSpent(c, p.LeadDefId);
            p.LeadId = -1;
            p.LeadDefId = "";
            c.ReplanNow = true;
        }
    }
}
