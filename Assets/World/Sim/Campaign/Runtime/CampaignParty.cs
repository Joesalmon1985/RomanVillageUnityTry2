using System;
using System.Collections.Generic;
using World.Sim.Eras;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Mira and the party (5.5): the modes, travel on the lattice, acting at a place, choices, thresholds, waits, the
    /// scout, joining and leaving, and the camp-overrun link (5.9).
    ///
    /// What she does alone (the player away) and what she never does: she takes leads after SoloDecide instead of
    /// LeadPatience, acts, takes unguarded crystals, answers a choice only after studying the marker that reveals it
    /// (she never guesses), and sends the scout when a belief is stale. She never commits a lethal encounter and never
    /// restores a crystal: those commands need the player present. At a threshold she waits ThresholdWait, withdraws to
    /// the safe point, takes another open lead if there is one, and otherwise goes back to the threshold and holds there
    /// until the player comes or a lead opens.
    /// </summary>
    internal static partial class CampaignRuntime
    {
        // ---------------------------------------------------------------- encounter places

        /// <summary>Where the party stands before an encounter (for the finale: the last crystal's tower).</summary>
        internal static MarkerRef ThresholdOf(CampaignState c, EncounterDef e)
        {
            if (e == null) return default;
            if (e.BoundTo == EncounterBindings.LastTower)
            {
                var last = RemainingCrystal(c);
                return last.HasValue ? TowerRef(c, last.Value) : default;
            }
            return e.Threshold;
        }

        /// <summary>Where a retreat from an encounter goes: its safe point (the finale: its tower's safe point).</summary>
        internal static MarkerRef SafeOf(CampaignState c, EncounterDef e)
        {
            if (e == null) return c.Party.At;
            if (!e.SafePoint.IsEmpty) return e.SafePoint;
            if (e.BoundTo == EncounterBindings.LastTower)
            {
                var last = RemainingCrystal(c);
                var cd = last.HasValue ? c.Definition.Crystal(last.Value) : null;
                var tower = cd != null ? c.Definition.Tower(cd.Tower) : null;
                var loc = tower != null ? c.Definition.Location(tower.Location) : null;
                if (loc != null) return LocationSafe(loc);
            }
            var th = ThresholdOf(c, e);
            return th.IsEmpty ? c.Party.At : th;
        }

        /// <summary>A lethal encounter still stands: a keeper while its crystal is on the altar; the finale while it holds; any other until won.</summary>
        internal static bool EncounterActive(CampaignState c, EncounterDef e)
        {
            if (e == null || e.Kind != EncounterKind.Lethal || c.CompletedEncounters.Contains(e.Id)) return false;
            if (e.BoundTo == EncounterBindings.LastTower)
            {
                var last = RemainingCrystal(c);
                return last.HasValue && FinaleHolds(c, last.Value) && c.Crystal(last.Value).Custody == Custody.Carried;
            }
            string k = KeeperCrystal(e);
            if (k.Length == 0) return true;
            var cd = c.Definition.Crystal(k);
            return cd != null && c.Crystal(cd.Element)?.Custody == Custody.AtRivalAltar;
        }

        /// <summary>The active lethal encounter whose threshold is this place (the finale: anywhere at the last crystal's tower).</summary>
        private static EncounterDef ActiveEncounterAt(CampaignState c, MarkerRef at)
        {
            var d = c.Definition;
            foreach (var e in d.Encounters ?? new List<EncounterDef>())
                if (e != null && string.IsNullOrEmpty(e.BoundTo) && Same(e.Threshold, at) && EncounterActive(c, e)) return e;
            var fin = LastTowerEncounter(d);
            if (fin != null && !at.IsInterior && EncounterActive(c, fin))
            {
                var th = ThresholdOf(c, fin);
                if (!th.IsEmpty && th.Location == at.Location) return fin;
            }
            return null;
        }

        private static CrystalDef PedestalCrystal(CampaignState c, MarkerRef at) => c.Definition.Crystals?.Find(x => x != null && Same(x.Site, at));

        /// <summary>A carried crystal whose tower is the location the party stands at.</summary>
        private static Element? CarriedTowerHere(CampaignState c)
        {
            var p = c.Party;
            if (p.At.IsInterior) return null;
            for (int i = 0; i < 4; i++)
            {
                var e = (Element)i;
                if (c.Crystal(e)?.Custody != Custody.Carried) continue;
                var cd = c.Definition.Crystal(e);
                var tower = cd != null ? c.Definition.Tower(cd.Tower) : null;
                if (tower != null && tower.Location == p.At.Location) return e;
            }
            return null;
        }

        private static LeadState TopLeadState(CampaignState c)
        {
            LeadState best = null;
            foreach (var l in c.Leads) if (best == null || l.Rank < best.Rank) best = l;
            return best;
        }

        // ---------------------------------------------------------------- travel

        /// <summary>
        /// Sets the party on a route to a place. While travelling the current leg is finished first and the new route
        /// starts from its end. False (nothing changes) when the place cannot be reached.
        /// </summary>
        private static bool SetOut(WorldState s, CampaignState c, MarkerRef goal, RoutePurpose purpose, int leadId, string leadDefId, QuestAction action, EventBuffer ev)
        {
            var p = c.Party;
            bool moving = p.Mode == PartyMode.Travelling && p.Leg != null;
            var origin = moving ? p.Leg.To : p.At;
            var route = new RouteMap(s, c, origin).PathTo(goal);
            if (route == null) return false;
            p.Purpose = purpose;
            p.LeadId = leadId;
            p.LeadDefId = leadDefId ?? "";
            p.LeadAction = action;
            p.Pending.Clear();
            p.HoldAtThreshold = false;
            p.Studying = false;
            if (p.Mode == PartyMode.AtThreshold) p.EncounterId = "";
            if (purpose != RoutePurpose.Study && purpose != RoutePurpose.ChoiceReturn) p.ChoiceId = "";
            p.Route = route;
            if (moving) return true;
            if (route.Count == 0) { OnFinalArrival(s, c, ev, true); return true; }
            SetMode(s, c, PartyMode.Travelling, ev);
            StartNextLeg(s, c, ev);
            return true;
        }

        private static void StartNextLeg(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            var to = p.Route[0];
            p.Route.RemoveAt(0);
            p.Leg = MakeLeg(s, c, p.At, to, c.Definition.Tuning.PartyTicksPerStep);
            p.BeganTick = s.Tick;
            ev?.Add(new PartySetOut { LeadId = p.LeadId, From = p.At, To = to, LegTicks = p.Leg.Ticks });
        }

        private static void TickParty(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            switch (p.Mode)
            {
                case PartyMode.Travelling:
                    if (p.BeganTick == s.Tick) return;
                    if (p.Leg == null)
                    {
                        if (p.Route.Count > 0) StartNextLeg(s, c, ev); else OnFinalArrival(s, c, ev);
                        return;
                    }
                    p.Leg.Progress++;
                    if (p.Leg.Progress < p.Leg.Ticks) return;
                    var leg = p.Leg;
                    p.At = leg.To;
                    p.Leg = null;
                    ev?.Add(new PartyArrived { At = p.At });
                    if (!Visit(s, c, leg, ev)) return;
                    if (p.Route.Count > 0) StartNextLeg(s, c, ev); else OnFinalArrival(s, c, ev);
                    return;
                case PartyMode.Acting:
                    if (p.BeganTick == s.Tick) return;
                    p.ActingProgress++;
                    if (p.ActingProgress >= p.ActingTicks) CompleteActing(s, c, ev);
                    return;
                case PartyMode.AskingChoice:
                    TickChoice(s, c, ev);
                    return;
                case PartyMode.AtThreshold:
                    TickThreshold(s, c, ev);
                    return;
                case PartyMode.WaitingForPlayer:
                    TickWaiting(s, c, ev);
                    return;
                case PartyMode.Idle:
                    TickIdle(s, c, ev);
                    return;
            }
        }

        /// <summary>
        /// The end of a leg (CAMPAIGN_FORMAT 7): Arrive quest events and the clues of every marker the leg passed over
        /// inside an interior and of the marker it ended on. False when that stopped the route (an event asked a
        /// question, ended the run, or moved the party).
        /// </summary>
        private static bool Visit(WorldState s, CampaignState c, LegState leg, EventBuffer ev)
        {
            var p = c.Party;
            var d = c.Definition;
            var places = new List<MarkerRef>();
            if (leg.To.IsInterior && leg.Cells != null && leg.Cells.Length > 4)
            {
                var m = d.Interior(leg.To.Interior);
                for (int k = 2; k + 2 < leg.Cells.Length; k += 2)
                {
                    var md = InteriorMaps.MarkerAt(m, leg.Cells[k], leg.Cells[k + 1]);
                    if (md != null) places.Add(new MarkerRef(leg.To.Location, leg.To.Interior, md.Id));
                }
            }
            places.Add(leg.To);
            var at = p.At;
            foreach (var place in places)
            {
                FireEvents(s, c, place, QuestAction.Arrive, ev);
                GiveCluesAt(c, place, ev);
                if (c.Phase != CampaignPhase.Running || p.Mode != PartyMode.Travelling || !Same(p.At, at))
                {
                    p.Route.Clear();
                    if (!Same(p.At, at)) p.Purpose = RoutePurpose.None;   // moved by a PlaceParty effect: the errand is off
                    if (c.Phase == CampaignPhase.Running && p.Mode == PartyMode.Travelling) OnFinalArrival(s, c, ev);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The route's end: what the party does there depends on why it came. Arrive events and clues fire here only when
        /// no leg was walked (otherwise they fired at the leg's end).
        /// </summary>
        private static void OnFinalArrival(WorldState s, CampaignState c, EventBuffer ev, bool arriveEvents = false)
        {
            var p = c.Party;
            p.Leg = null;
            p.Route.Clear();
            p.ArriveSignature = Signature(c);
            switch (p.Purpose)
            {
                case RoutePurpose.Study:
                    BeginStudy(s, c, ev);
                    return;
                case RoutePurpose.ChoiceReturn:
                    p.Purpose = RoutePurpose.None;
                    AnswerSolo(s, c, ev);
                    return;
                case RoutePurpose.Retreat:
                    p.Purpose = RoutePurpose.None;
                    p.LeadId = -1;
                    p.LeadDefId = "";
                    SetMode(s, c, !string.IsNullOrEmpty(p.WithdrawnFrom) && !p.PlayerPresent ? PartyMode.WaitingForPlayer : PartyMode.Idle, ev);
                    c.ReplanNow = true;
                    return;
                case RoutePurpose.Hold:
                {
                    p.Purpose = RoutePurpose.None;
                    var e = c.Definition.Encounter(p.WithdrawnFrom);
                    if (e != null && EncounterActive(c, e) && Same(ThresholdOf(c, e), p.At)) { EnterThreshold(s, c, e, true, ev); return; }
                    p.WithdrawnFrom = "";
                    Settle(s, c, ev);
                    return;
                }
            }
            if (arriveEvents)
            {
                FireEvents(s, c, p.At, QuestAction.Arrive, ev);
                GiveCluesAt(c, p.At, ev);
                if (c.Phase != CampaignPhase.Running || p.Mode == PartyMode.AskingChoice) return;
            }
            p.Pending.Clear();
            if (p.Purpose == RoutePurpose.Lead && p.LeadAction != QuestAction.Arrive && p.LeadAction != QuestAction.Restore) p.Pending.Add(p.LeadAction);
            var cd = PedestalCrystal(c, p.At);
            if (cd != null)
            {
                var want = c.Crystal(cd.Element)?.Custody == Custody.AtSite ? QuestAction.Take : QuestAction.Examine;
                if (!p.Pending.Contains(want)) p.Pending.Add(want);
            }
            NextAction(s, c, ev);
        }

        private static int ActionTicks(CampaignState c, QuestAction a)
        {
            var t = c.Definition.Tuning;
            switch (a)
            {
                case QuestAction.Arrive:
                case QuestAction.Answer: return 0;
                case QuestAction.Search: return Math.Max(0, t.SearchTicks);
                case QuestAction.Take: return Math.Max(0, t.TakeTicks);
                case QuestAction.Restore: return Math.Max(0, t.RestoreTicks);
                default: return Math.Max(0, t.ReadTicks);
            }
        }

        /// <summary>Starts the next action at this place, or settles when there is none.</summary>
        private static void NextAction(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            while (p.Pending.Count > 0)
            {
                var a = p.Pending[0];
                p.Pending.RemoveAt(0);
                int ticks = ActionTicks(c, a);
                p.ActingAction = a;
                p.ActingTicks = ticks;
                p.ActingProgress = 0;
                p.Studying = false;
                p.BeganTick = s.Tick;
                if (ticks > 0) { SetMode(s, c, PartyMode.Acting, ev); return; }
                CompleteAction(s, c, a, ev);
                if (c.Phase != CampaignPhase.Running || p.Mode == PartyMode.AskingChoice) return;
            }
            Settle(s, c, ev);
        }

        private static void CompleteActing(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            if (p.ActingAction == QuestAction.Restore)
            {
                var cd = c.Definition.Crystal(p.ActingCrystal);
                p.ActingCrystal = "";
                if (cd != null) Restore(s, c, cd.Element, ev);
                if (c.Phase == CampaignPhase.Running) Settle(s, c, ev);
                return;
            }
            if (p.Studying) { StudyDone(s, c, ev); return; }
            CompleteAction(s, c, p.ActingAction, ev);
            if (c.Phase != CampaignPhase.Running || p.Mode == PartyMode.AskingChoice) return;
            NextAction(s, c, ev);
        }

        /// <summary>An action at a place is done: its quest events, the clues heard here, and the pedestal (take or see it empty).</summary>
        private static void CompleteAction(WorldState s, CampaignState c, QuestAction a, EventBuffer ev)
        {
            var p = c.Party;
            var at = p.At;
            if (a == QuestAction.Answer)
            {
                var oc = c.OpenChoices.Find(x => Same(x.At, at));
                if (oc != null) { OfferChoice(s, c, oc.ChoiceId, ev); return; }
            }
            FireEvents(s, c, at, a, ev);
            if (c.Phase != CampaignPhase.Running) return;
            if (a != QuestAction.Take && a != QuestAction.Restore) GiveCluesAt(c, at, ev);
            var cd = PedestalCrystal(c, at);
            if (cd == null) return;
            var st = c.Crystal(cd.Element);
            if (a == QuestAction.Take && st.Custody == Custody.AtSite) TakeCrystal(s, c, cd.Element, ev);
            else if (a == QuestAction.Examine && (st.Custody == Custody.RivalTransit || st.Custody == Custody.AtRivalAltar))
                Journal(s, c, "The pedestal at " + CampaignTokens.PlaceName(c.Definition, at) + " was empty. " + Cap(cd.Name) + " is gone.", ev);
            // Seeing the pedestal tells the party the truth about its crystal.
            SetBeliefTruth(s, c, cd.Element, SourceKind.Marker, ev);
        }

        /// <summary>Nothing more to do here: a threshold, a tower with its crystal, or Idle; the followed lead is finished.</summary>
        private static void Settle(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            p.Pending.Clear();
            p.Studying = false;
            var e = ActiveEncounterAt(c, p.At);
            if (e != null)
            {
                FinishLead(s, c, true, ev);
                EnterThreshold(s, c, e, false, ev);
                return;
            }
            string followed = p.LeadDefId;
            FinishLead(s, c, false, ev);
            p.Purpose = RoutePurpose.None;
            var tw = CarriedTowerHere(c);
            if (tw.HasValue)
            {
                // Alone, she waits at a tower once; she does not walk from tower to tower until the player is back.
                if (!p.PlayerPresent && !string.IsNullOrEmpty(followed)) MarkSpent(c, followed, true);
                bool wasWaiting = p.Mode == PartyMode.WaitingForPlayer;
                SetMode(s, c, PartyMode.WaitingForPlayer, ev);
                if (!wasWaiting)
                {
                    var cd = c.Definition.Crystal(tw.Value);
                    Journal(s, c, "Mira is waiting for you at " + CampaignTokens.PlaceName(c.Definition, p.At) + " to set " + (cd?.Name ?? "the crystal") + ".", ev);
                }
                return;
            }
            SetMode(s, c, !string.IsNullOrEmpty(p.WithdrawnFrom) && !p.PlayerPresent ? PartyMode.WaitingForPlayer : PartyMode.Idle, ev);
        }

        // ---------------------------------------------------------------- waiting, offering, thresholds

        /// <summary>The top lead after its patience (the player's LeadPatience, or SoloDecide when she is alone). True when she set out.</summary>
        private static bool GoOnWithTopLead(WorldState s, CampaignState c, EventBuffer ev, long since)
        {
            var top = TopLeadState(c);
            if (top == null) return false;
            var p = c.Party;
            long patience = p.PlayerPresent ? c.Definition.Tuning.LeadPatience : c.Definition.Tuning.SoloDecide;
            if (s.Tick - Math.Max(top.TopSinceTick, since) < patience) return false;
            TakeLead(s, c, top, false, ev);
            return true;
        }

        private static void TickIdle(WorldState s, CampaignState c, EventBuffer ev) => GoOnWithTopLead(s, c, ev, c.Party.ModeSinceTick);

        private static void TickWaiting(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            if (GoOnWithTopLead(s, c, ev, p.ModeSinceTick)) return;
            if (p.PlayerPresent || string.IsNullOrEmpty(p.WithdrawnFrom)) return;
            // Withdrawn, alone, nothing else open: back to the threshold to hold there (5.5).
            var e = c.Definition.Encounter(p.WithdrawnFrom);
            if (e == null || !EncounterActive(c, e)) { p.WithdrawnFrom = ""; SetMode(s, c, PartyMode.Idle, ev); return; }
            if (TopLeadState(c) == null && s.Tick - p.ModeSinceTick >= c.Definition.Tuning.SoloDecide)
                SetOut(s, c, ThresholdOf(c, e), RoutePurpose.Hold, -1, "", QuestAction.Arrive, ev);
        }

        private static void EnterThreshold(WorldState s, CampaignState c, EncounterDef e, bool hold, EventBuffer ev)
        {
            var p = c.Party;
            p.EncounterId = e.Id;
            p.WaitSinceTick = s.Tick;
            p.HoldAtThreshold = hold && !p.PlayerPresent;
            p.Purpose = RoutePurpose.None;
            SetMode(s, c, PartyMode.AtThreshold, ev);
            ev?.Add(new EncounterThreshold { EncounterId = e.Id, Volunteer = CampaignApi.NextVolunteerOf(c)?.Id ?? "" });
            string who = string.IsNullOrEmpty(e.OpponentName) ? "The enemy" : Cap(e.OpponentName);
            string place = CampaignTokens.PlaceName(c.Definition, p.At);
            Journal(s, c, who + " stands before the party at " + place + "." + (p.PlayerPresent ? "" : " Mira will not fight without you; she waits there."), ev);
        }

        private static void TickThreshold(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            var e = c.Definition.Encounter(p.EncounterId);
            if (e == null || !EncounterActive(c, e))
            {
                // Nothing to fight any more (an alternate route took the crystal back).
                p.EncounterId = "";
                p.HoldAtThreshold = false;
                Settle(s, c, ev);
                return;
            }
            if (p.HoldAtThreshold)
            {
                if (p.PlayerPresent) { p.HoldAtThreshold = false; p.WaitSinceTick = s.Tick; return; }
                GoOnWithTopLead(s, c, ev, p.WaitSinceTick);
                return;
            }
            if (s.Tick - p.WaitSinceTick >= c.Definition.Tuning.ThresholdWait) Withdraw(s, c, e, "threshold_wait", ev);
        }

        /// <summary>Back from a threshold to the encounter's safe point, on foot, before committing: safe, no cost (5.6).</summary>
        private static void Withdraw(WorldState s, CampaignState c, EncounterDef e, string why, EventBuffer ev)
        {
            var p = c.Party;
            var safe = SafeOf(c, e);
            p.EncounterId = "";
            p.HoldAtThreshold = false;
            p.WithdrawnFrom = p.PlayerPresent ? "" : (e?.Id ?? "");
            ev?.Add(new PartyRetreated { To = safe, Why = why });
            string place = CampaignTokens.PlaceName(c.Definition, safe);
            Journal(s, c, "Mira stepped back to " + (place.Length > 0 ? place : "safety") + (p.PlayerPresent ? "." : " and waits for you there."), ev);
            if (!SetOut(s, c, safe, RoutePurpose.Retreat, -1, "", QuestAction.Arrive, ev))
            {
                p.At = safe;
                p.Purpose = RoutePurpose.Retreat;
                OnFinalArrival(s, c, ev);
            }
        }

        // ---------------------------------------------------------------- choices (5.4, 5.5)

        internal static void OfferChoice(WorldState s, CampaignState c, string choiceId, EventBuffer ev)
        {
            var ch = c.Definition.Choice(choiceId);
            if (ch == null) return;
            if (ch.Sets != null && ch.Sets.Count > 0 && AllKnown(c, ch.Sets)) return;   // already answered
            var p = c.Party;
            if (!c.OpenChoices.Exists(x => x.ChoiceId == choiceId)) c.OpenChoices.Add(new OpenChoice { ChoiceId = choiceId, At = p.At });
            p.ChoiceId = choiceId;
            p.ChoiceAt = p.At;
            p.WaitSinceTick = s.Tick;
            p.Pending.Clear();
            p.Studying = false;
            SetMode(s, c, PartyMode.AskingChoice, ev);
            ev?.Add(new ChoiceOffered { ChoiceId = choiceId });
            Journal(s, c, CampaignApi.LineOf(c, ch.QuestionLine) ?? "A question was put to Mira at " + CampaignTokens.PlaceName(c.Definition, p.At) + ".", ev);
            c.ReplanNow = true;
        }

        private static void TickChoice(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            var ch = c.Definition.Choice(p.ChoiceId);
            if (ch == null) { p.ChoiceId = ""; Settle(s, c, ev); return; }
            // The player is asked; no answer in ChoicePatience and she treats it as away.
            if (p.PlayerPresent && s.Tick - p.WaitSinceTick < c.Definition.Tuning.ChoicePatience) return;
            // Alone she finds the answer in the world first; she never guesses (5.5, 12.12).
            if (ch.RevealedBy.IsEmpty || CorrectAnswer(ch) < 0) { LeaveChoice(s, c, ev); return; }
            if (Same(ch.RevealedBy, p.At)) { BeginStudy(s, c, ev); return; }
            if (!SetOut(s, c, ch.RevealedBy, RoutePurpose.Study, p.LeadId, p.LeadDefId, p.LeadAction, ev)) LeaveChoice(s, c, ev);
            else Journal(s, c, "Mira went to look for the answer at " + CampaignTokens.PlaceName(c.Definition, ch.RevealedBy) + ".", ev);
        }

        private static int CorrectAnswer(ChoiceDef ch)
        {
            for (int i = 0; i < (ch.Answers?.Count ?? 0); i++) if (ch.Answers[i] != null && ch.Answers[i].Correct) return i;
            return -1;
        }

        /// <summary>She cannot find the answer alone: the choice stays open (an "@choice:" lead brings her back) and she moves on.</summary>
        private static void LeaveChoice(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            Journal(s, c, "Mira could not find the answer by herself at " + CampaignTokens.PlaceName(c.Definition, p.ChoiceAt) + ".", ev);
            p.ChoiceId = "";
            Settle(s, c, ev);
        }

        private static void BeginStudy(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            p.Purpose = RoutePurpose.None;
            p.Pending.Clear();
            p.ActingAction = QuestAction.Examine;
            p.ActingTicks = Math.Max(0, c.Definition.Tuning.SoloStudyTicks);
            p.ActingProgress = 0;
            p.Studying = true;
            p.BeganTick = s.Tick;
            if (p.ActingTicks <= 0) { StudyDone(s, c, ev); return; }
            SetMode(s, c, PartyMode.Acting, ev);
        }

        private static void StudyDone(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            p.Studying = false;
            Journal(s, c, "Mira studied the marks at " + CampaignTokens.PlaceName(c.Definition, p.At) + " and found the answer.", ev);
            if (string.IsNullOrEmpty(p.ChoiceId)) { Settle(s, c, ev); return; }
            if (Same(p.ChoiceAt, p.At)) { AnswerSolo(s, c, ev); return; }
            if (!SetOut(s, c, p.ChoiceAt, RoutePurpose.ChoiceReturn, p.LeadId, p.LeadDefId, p.LeadAction, ev)) LeaveChoice(s, c, ev);
        }

        private static void AnswerSolo(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            var ch = c.Definition.Choice(p.ChoiceId);
            int right = ch != null ? CorrectAnswer(ch) : -1;
            if (right < 0) { p.ChoiceId = ""; Settle(s, c, ev); return; }
            Answer(s, c, ch.Id, right, false, ev);
        }

        /// <summary>
        /// One answer to the open choice, the same step for the player's advice and Mira's own answer (CR-21). Right: the
        /// facts, the Answer quest events, then she settles. Wrong: WrongAnswerCharge on the expedition clock and the
        /// choice stays open (look round and try again).
        /// </summary>
        private static void Answer(WorldState s, CampaignState c, string choiceId, int index, bool byPlayer, EventBuffer ev)
        {
            var d = c.Definition;
            var ch = d.Choice(choiceId);
            if (ch == null || index < 0 || index >= (ch.Answers?.Count ?? 0)) return;
            var p = c.Party;
            bool correct = ch.Answers[index]?.Correct ?? false;
            ev?.Add(new ChoiceAnswered { ChoiceId = choiceId, Answer = index, Correct = correct, ByPlayer = byPlayer });
            string said = CampaignApi.LineOf(c, ch.Answers[index]?.Line);
            string place = CampaignTokens.PlaceName(d, p.ChoiceAt);
            if (!correct)
            {
                Charge(s, c, d.Tuning.WrongAnswerCharge, RivalCharges.WrongAnswer, ev);
                Journal(s, c, CampaignApi.LineOf(c, ch.WrongLine) ?? "That was not the answer.", ev);
                p.WaitSinceTick = s.Tick;
                c.ReplanNow = true;
                return;
            }
            c.OpenChoices.RemoveAll(x => x.ChoiceId == choiceId);
            p.ChoiceId = "";
            foreach (var f in ch.Sets ?? new List<string>()) Learn(c, f, SourceKind.Witness, ev);
            Journal(s, c, (byPlayer ? "On your advice Mira answered" : "Mira answered") + (said != null ? " \"" + said + "\"" : "") + " at " + place + ", and it was right.", ev);
            FireEvents(s, c, p.At, QuestAction.Answer, ev);
            if (c.Phase != CampaignPhase.Running || !string.IsNullOrEmpty(p.ChoiceId)) return;   // an event asked the next question
            Settle(s, c, ev);
        }

        // ---------------------------------------------------------------- company

        private static void SetCompanyNow(WorldState s, CampaignState c, bool present, EventBuffer ev)
        {
            var p = c.Party;
            bool was = p.PlayerPresent;
            p.PlayerPresent = present;
            string place = CampaignTokens.PlaceName(c.Definition, p.At);
            if (present && !was)
            {
                p.WithdrawnFrom = "";
                c.Spent.RemoveAll(x => x.Signature == AwaySpent);
                if (p.Mode == PartyMode.AtThreshold) { p.HoldAtThreshold = false; p.WaitSinceTick = s.Tick; }
                if (p.Mode == PartyMode.AskingChoice) p.WaitSinceTick = s.Tick;
                Journal(s, c, "You rejoined Mira at " + place + ".", ev);
            }
            else if (!present && was) Journal(s, c, "You left Mira at " + place + ". She goes on without you.", ev);
            c.ReplanNow = true;
        }

        // ---------------------------------------------------------------- the scout (5.5)

        private static LegState ScoutLeg(WorldState s, CampaignState c, string fromLocation, string toLocation)
        {
            var d = c.Definition;
            int steps = 0;
            var a = d.Location(fromLocation);
            var b = d.Location(toLocation);
            if (a != null && b != null)
            {
                AnchorCell(s, d, a, out int ai, out int aj);
                AnchorCell(s, d, b, out int bi, out int bj);
                steps = Lattice.Distance(ai, aj, bi, bj);
            }
            return new LegState
            {
                From = MarkerRef.Surface(fromLocation ?? "", ""), To = MarkerRef.Surface(toLocation ?? "", ""),
                Ticks = Math.Max(1, steps * Math.Max(1, d.Tuning.ScoutTicksPerStep)), Progress = 0, Cells = new int[0],
            };
        }

        private static string PartyLocation(CampaignState c)
        {
            var at = c.Party.At;
            return !string.IsNullOrEmpty(at.Location) ? at.Location : Home(c.Definition).Location ?? "";
        }

        /// <summary>One scout at a time (the companion with the scout service): out, search, back, then her report.</summary>
        private static void SendScout(WorldState s, CampaignState c, string locationId, bool bySelf, EventBuffer ev)
        {
            var k = CampaignApi.ScoutCompanion(c);
            var loc = c.Definition.Location(locationId);
            if (k == null || loc == null || c.Scout.Out) return;
            var sc = c.Scout;
            sc.Out = true;
            sc.CompanionId = k.Id;
            sc.Target = locationId;
            sc.Phase = 1;
            sc.Timer = 0;
            sc.Found.Clear();
            sc.Leg = ScoutLeg(s, c, PartyLocation(c), locationId);
            ev?.Add(new ScoutSent { CompanionId = k.Id, Target = locationId });
            Journal(s, c, (bySelf ? "Mira sent " + k.Name + " to look at " : k.Name + " ran to look at ") + loc.Name + ".", ev);
        }

        private static void TickScout(WorldState s, CampaignState c, EventBuffer ev)
        {
            var sc = c.Scout;
            if (sc == null || !sc.Out) return;
            var d = c.Definition;
            switch (sc.Phase)
            {
                case 1:
                    if (sc.Leg != null && ++sc.Leg.Progress < sc.Leg.Ticks) return;
                    sc.Phase = 2;
                    sc.Timer = Math.Max(1, d.Tuning.SearchTicks);
                    return;
                case 2:
                    if (--sc.Timer > 0) return;
                    sc.Found.Clear();
                    for (int i = 0; i < 4; i++)
                    {
                        var e = (Element)i;
                        var st = c.Crystal(e);
                        var b = c.BeliefOf(e);
                        if (st == null || st.Custody == Custody.Carried) continue;
                        if (st.At.Location == sc.Target || (b != null && b.At.Location == sc.Target)) sc.Found.Add(e);
                    }
                    sc.Phase = 3;
                    sc.Leg = ScoutLeg(s, c, sc.Target, PartyLocation(c));
                    return;
                default:
                {
                    if (sc.Leg != null && ++sc.Leg.Progress < sc.Leg.Ticks) return;
                    var name = d.Companion(sc.CompanionId)?.Name ?? "The scout";
                    foreach (var e in sc.Found)
                    {
                        SetBeliefTruth(s, c, e, SourceKind.Scout, ev);
                        ev?.Add(new ScoutReported { Crystal = e });
                    }
                    foreach (var clue in d.Clues ?? new List<ClueDef>())
                        if (clue.Source?.Kind == SourceKind.Scout && clue.Source.Ref == sc.Target) GiveClue(c, clue, ev);
                    string where = d.Location(sc.Target)?.Name ?? sc.Target;
                    Journal(s, c, name + " came back from " + where + (sc.Found.Count > 0 ? " with news." : " with nothing new."), ev);
                    sc.Out = false;
                    sc.Phase = 0;
                    sc.Leg = null;
                    ev?.Add(new ScoutReturned());
                    c.ReplanNow = true;
                    return;
                }
            }
        }

        /// <summary>Alone, Mira sends the scout when what the party believes about a crystal has gone stale (5.5).</summary>
        private static void AutoScout(WorldState s, CampaignState c, EventBuffer ev)
        {
            var p = c.Party;
            if (p.PlayerPresent || c.Scout.Out || p.Mode == PartyMode.InEncounter || CampaignApi.ScoutCompanion(c) == null) return;
            var d = c.Definition;
            for (int i = 0; i < 4; i++)
            {
                var b = c.BeliefOf((Element)i);
                if (b == null || b.Custody == Custody.Carried || b.Custody == Custody.Restored) continue;
                if (s.Tick - b.AsOfTick <= d.Tuning.StaleTicks) continue;
                if (b.At.IsEmpty || d.Location(b.At.Location) == null) continue;
                SendScout(s, c, b.At.Location, true, ev);
                return;
            }
        }

        // ---------------------------------------------------------------- the faction game (5.9)

        /// <summary>CampOverrun events raised earlier in this tick (Strength runs before the campaign slot), in order.</summary>
        private static void ScanOverruns(WorldState s, CampaignState c, EventBuffer ev)
        {
            var items = ev?.Items;
            if (items == null || items.Count == 0) return;
            int first = items.Count;
            while (first > 0 && items[first - 1].Tick == s.Tick) first--;
            for (int i = first; i < items.Count; i++)
                if (items[i] is CampOverrun co) OnCampOverrun(s, c, co.Attacker, co.Defender, ev);
        }

        /// <summary>
        /// The overrun link (CR-31): a camp of the player's lost or the home camp looted costs CampOverrunCharge, and the
        /// party drops what it is doing and retreats to the home camp on foot (an uncommitted threshold is abandoned; an
        /// open choice stays open). Nobody dies. An Ash camp taken by the player's people pauses the expedition.
        /// </summary>
        internal static void OnCampOverrun(WorldState s, CampaignState c, int attacker, int defender, EventBuffer ev)
        {
            if (c.Phase != CampaignPhase.Running) return;
            var p = c.Party;
            if (defender == PlayerIds.Human)
            {
                Charge(s, c, c.Definition.Tuning.CampOverrunCharge, RivalCharges.CampOverrun, ev);
                if (c.Attempt != null || p.Mode == PartyMode.InEncounter) return;
                var home = Home(c.Definition);
                p.WithdrawnFrom = "";
                p.EncounterId = "";
                p.ChoiceId = "";
                p.HoldAtThreshold = false;
                ev?.Add(new PartyRetreated { To = home, Why = "camp_overrun" });
                Journal(s, c, "The Ash Clan overran one of your camps. Mira turned the party back to " + CampaignTokens.PlaceName(c.Definition, home) + ".", ev);
                if (!SetOut(s, c, home, RoutePurpose.Retreat, -1, "", QuestAction.Arrive, ev))
                {
                    p.At = home;
                    p.Leg = null;
                    p.Route.Clear();
                    p.Purpose = RoutePurpose.Retreat;
                    OnFinalArrival(s, c, ev);
                }
            }
            else if (defender == PlayerIds.Rival && attacker == PlayerIds.Human)
            {
                PauseRival(s, c, ev);
                Journal(s, c, "Your people took an Ash camp. The Ash expedition has stopped.", ev);
            }
        }
    }
}
