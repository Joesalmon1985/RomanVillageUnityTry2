using System;
using System.Collections.Generic;
using World.Sim.Demons;
using World.Sim.Eras;
using World.Sim.Ledger;

namespace World.Sim.Campaign
{
    /// <summary>
    /// The campaign's behaviour (PRD_CRYSTALS section 5): command checks, command effects and the per-tick step. OWNED BY
    /// PACKAGE I1 (Sim/Campaign/Runtime/**). The file is split by subject: this one holds the checks, the commands, the
    /// start, the outcome table, restoration, the endings and the shared steps; <c>CampaignPlaces</c> routes and legs;
    /// <c>CampaignRival</c> the expedition clock, plan and reports; <c>CampaignLeads</c> facts, clues, leads and Mira's
    /// queue; <c>CampaignParty</c> the party's modes (travel, acting, choices, thresholds, waits), the scout and the
    /// overrun link. Everything is deterministic and kept in the saved state.
    /// </summary>
    internal static partial class CampaignRuntime
    {
        private static CampaignState Run(WorldState s) => s?.Campaign?.Definition != null ? s.Campaign : null;

        // ---------------------------------------------------------------- checks

        public static CommandResult Check(WorldState s, Command cmd)
        {
            if (s == null || cmd == null) return CommandResult.Reject(Reasons.Disabled, "That action is not available.");
            if (cmd is StartCampaign start) return CheckStart(s, start);

            var c = Run(s);
            if (c == null) return CampaignRefusals.Reject(CampaignRefusals.NoCampaign);
            // A second resolution of a settled attempt is "already settled" even after the run has ended.
            if (cmd is ResolveEncounter re0 && IsResolved(c, re0.AttemptId) && (c.Attempt == null || c.Attempt.Id != re0.AttemptId))
                return CampaignRefusals.Reject(CampaignRefusals.AlreadyResolved);
            if (c.Phase != CampaignPhase.Running) return CampaignRefusals.Reject(CampaignRefusals.CampaignOver);
            var d = c.Definition;
            var p = c.Party ?? new PartyState();

            switch (cmd)
            {
                case SetCompany _:
                    return CommandResult.Accept();
                case AcceptLead al:
                    // Mira is busy while a duel is pending or she is in the middle of something at a place.
                    if (!HasLead(c, al.LeadId) || p.Mode == PartyMode.InEncounter || p.Mode == PartyMode.Acting)
                        return CampaignRefusals.Reject(CampaignRefusals.UnknownLead);
                    return CommandResult.Accept();
                case DeclineLead dl:
                    return HasLead(c, dl.LeadId) ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.UnknownLead);
                case AdviseMira am:
                {
                    var ch = d.Choice(am.ChoiceId);
                    bool open = p.Mode == PartyMode.AskingChoice && ch != null && p.ChoiceId == am.ChoiceId
                        && am.Answer >= 0 && am.Answer < (ch.Answers?.Count ?? 0);
                    return open ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.NotAtChoice);
                }
                case SuggestScout ss:
                {
                    if (d.Location(ss.LocationId) == null) return CampaignRefusals.Reject(CampaignRefusals.UnknownPlace);
                    if (c.Scout != null && c.Scout.Out) return CampaignRefusals.Reject(CampaignRefusals.ScoutOut, ScoutName(c));
                    return CampaignApi.ScoutCompanion(c) != null ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.NoScout);
                }
                case CommitEncounter ce:
                {
                    var e = d.Encounter(ce.EncounterId);
                    if (e == null || e.Kind != EncounterKind.Lethal || p.Mode != PartyMode.AtThreshold || p.EncounterId != ce.EncounterId
                        || c.Attempt != null)
                        return CampaignRefusals.Reject(CampaignRefusals.NotAtThreshold);
                    if (c.Scout != null && c.Scout.Out) return CampaignRefusals.Reject(CampaignRefusals.ScoutOut, ScoutName(c));
                    // Mira never starts a lethal duel alone (CR-19).
                    return p.PlayerPresent ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.PlayerNeeded);
                }
                case WithdrawFromEncounter we:
                    return p.Mode == PartyMode.AtThreshold && p.EncounterId == we.EncounterId
                        ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.NotAtThreshold);
                case ResolveEncounter re:
                    if (c.Attempt != null && c.Attempt.Id == re.AttemptId) return CommandResult.Accept();
                    return CampaignRefusals.Reject(IsResolved(c, re.AttemptId) ? CampaignRefusals.AlreadyResolved : CampaignRefusals.UnknownAttempt);
                case RestartAttempt ra:
                    if (c.Attempt != null && c.Attempt.Id == ra.AttemptId) return CommandResult.Accept();
                    return CampaignRefusals.Reject(IsResolved(c, ra.AttemptId) ? CampaignRefusals.AlreadyResolved : CampaignRefusals.UnknownAttempt);
                case ConfirmRestore cr:
                {
                    var st = c.Crystal(cr.Crystal);
                    if (st == null || st.Custody != Custody.Carried) return CampaignRefusals.Reject(CampaignRefusals.NotCarried);
                    var cd = d.Crystal(cr.Crystal);
                    var tower = cd != null ? d.Tower(cd.Tower) : null;
                    if (tower == null || p.At.IsInterior || p.At.Location != tower.Location || p.Mode == PartyMode.Travelling)
                        return CampaignRefusals.Reject(CampaignRefusals.WrongTower);
                    // The fourth crystal is set by winning the last tower's duel (5.8), never by "Set it".
                    if (FinaleHolds(c, cr.Crystal) || p.Mode == PartyMode.InEncounter) return CampaignRefusals.Reject(CampaignRefusals.NotAtThreshold);
                    // She does not restore without the player (5.5).
                    return p.PlayerPresent ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.PlayerNeeded);
                }
                case DevSetRivalProgress _:
                case DevRivalNextStep _:
                case DevSetCompanion _:
                    return CommandResult.Accept();
                case DevSetCustody dc:
                    return c.Crystal(dc.Crystal) != null ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.NotCarried);
                default:
                    return CommandResult.Reject(Reasons.Disabled, "That action is not available.");
            }
        }

        private static CommandResult CheckStart(WorldState s, StartCampaign start)
        {
            if (Run(s)?.Phase == CampaignPhase.Running) return CampaignRefusals.Reject(CampaignRefusals.CampaignRunning);
            if (start.Definition == null) return CampaignRefusals.Reject(CampaignRefusals.InvalidDefinition, "no campaign was given.");
            foreach (var problem in CampaignValidator.Validate(start.Definition))
                if (problem != null && problem.IsError) return CampaignRefusals.Reject(CampaignRefusals.InvalidDefinition, problem.Sentence);
            return CommandResult.Accept();
        }

        private static bool HasLead(CampaignState c, int id) => c.Leads != null && c.Leads.Exists(l => l.Id == id);

        private static bool IsResolved(CampaignState c, int attemptId) => c.Resolved != null && c.Resolved.Exists(r => r.Id == attemptId);

        private static string ScoutName(CampaignState c)
        {
            string id = c.Scout?.CompanionId;
            return (string.IsNullOrEmpty(id) ? null : c.Definition.Companion(id)?.Name) ?? CampaignApi.ScoutCompanion(c)?.Name;
        }

        // ---------------------------------------------------------------- effects

        public static CommandResult Apply(WorldState s, Command cmd, EventBuffer ev)
        {
            if (cmd is StartCampaign start)
            {
                Start(s, start.Definition, ev);
                return CommandResult.Accept();
            }
            var c = Run(s);
            if (c == null) return CampaignRefusals.Reject(CampaignRefusals.NoCampaign);
            switch (cmd)
            {
                case SetCompany sc:
                    SetCompanyNow(s, c, sc.Present, ev);
                    break;
                case AcceptLead al:
                    TakeLead(s, c, c.Leads.Find(l => l.Id == al.LeadId), true, ev);
                    break;
                case DeclineLead dl:
                    Decline(s, c, dl.LeadId, ev);
                    break;
                case AdviseMira am:
                    Answer(s, c, am.ChoiceId, am.Answer, true, ev);
                    break;
                case SuggestScout ss:
                    SendScout(s, c, ss.LocationId, false, ev);
                    break;
                case CommitEncounter ce:
                    Commit(s, c, ce.EncounterId, ev);
                    break;
                case WithdrawFromEncounter we:
                    Withdraw(s, c, c.Definition.Encounter(we.EncounterId), "withdraw", ev);
                    break;
                case ResolveEncounter re:
                    Resolve(s, c, re.AttemptId, re.Result, ev);
                    break;
                case RestartAttempt _:
                {
                    var a = c.Attempt;
                    a.Restarts++;
                    a.Seed = c.Rng.NextULong();
                    ev?.Add(Committed(c, a));
                    break;
                }
                case ConfirmRestore cr:
                    BeginRestore(s, c, cr.Crystal, ev);
                    break;
                case DevSetRivalProgress dr:
                    c.RivalProgress = Math.Max(0, dr.Value);
                    break;
                case DevRivalNextStep _:
                    RivalNextStepNow(s, c, ev);
                    break;
                case DevSetCustody dc:
                {
                    var st = c.Crystal(dc.Crystal);
                    var at = st.At;
                    if (dc.To == Custody.AtRivalAltar || dc.To == Custody.RivalTransit)
                    {
                        var step = RivalStepFor(c, dc.Crystal);
                        if (step != null) at = dc.To == Custody.AtRivalAltar ? step.Altar : (c.Definition.Crystal(dc.Crystal)?.Site ?? at);
                    }
                    else if (dc.To == Custody.Carried) at = c.Party.At;
                    else if (dc.To == Custody.Restored) at = TowerRef(c, dc.Crystal);
                    else if (dc.To == Custody.AtSite) at = c.Definition.Crystal(dc.Crystal)?.Site ?? at;
                    if (SetCustody(s, dc.Crystal, dc.To, at, ev))
                    {
                        if (dc.To == Custody.Restored && !c.RestoredOrder.Contains(dc.Crystal)) c.RestoredOrder.Add(dc.Crystal);
                        if (dc.To != Custody.Restored) c.RestoredOrder.Remove(dc.Crystal);
                        ApplyIslandEffects(s, c, ev);
                    }
                    break;
                }
                case DevSetCompanion dk:
                {
                    var k = c.Companion(dk.Id);
                    if (k == null) break;
                    k.Alive = dk.Alive;
                    k.FellAt = dk.Alive ? "" : "dev";
                    k.FellPlace = "";
                    k.FellTick = dk.Alive ? -1 : s.Tick;
                    c.ReplanNow = true;
                    break;
                }
            }
            return CommandResult.Accept();
        }

        /// <summary>Builds the run state from the definition and switches the island to campaign mode (CR-1, 5.9).</summary>
        internal static void Start(WorldState s, CampaignDefinition d, EventBuffer ev)
        {
            var c = new CampaignState
            {
                Definition = d,
                DefinitionHash = CampaignLoader.Hash(d),
                Phase = CampaignPhase.Running,
                StartTick = s.Tick,
                Rng = (s.Rng ?? new Rng(s.Seed)).Fork("campaign:" + (d.Id ?? "")),
                BaseSevensPerDraw = s.Demons?.Tuning?.SevensPerDraw ?? 1,
                Crystals = new CrystalState[4],
                Beliefs = new Belief[4],
                ReplanNow = true,
            };
            var econ = s.Economy?.Tuning;
            if (econ != null)
            {
                c.BaseGrowTicks = econ.GrowTicks != null ? (int[])econ.GrowTicks.Clone() : null;
                c.BaseCarrierTicksPerStep = econ.CarrierTicksPerStep;
            }
            var stolen = d.Rival?.StolenAtStart ?? new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var e = (Element)i;
                var cd = d.Crystal(e);
                var site = cd?.Site ?? default;
                c.Crystals[i] = new CrystalState { Custody = Custody.AtSite, At = site, SinceTick = s.Tick };
                c.Beliefs[i] = new Belief { Custody = Custody.AtSite, At = site, AsOfTick = s.Tick, Source = SourceKind.Opening };
            }
            foreach (var k in d.Companions) c.Companions.Add(new CompanionState { Id = k.Id, Alive = true });

            c.Party = new PartyState { Mode = PartyMode.Idle, PlayerPresent = true, ModeSinceTick = s.Tick, At = Home(d) };
            var plan = d.Rival?.Steps ?? new List<RivalStepDef>();
            c.Rival = new RivalState { Phase = plan.Count > 0 ? RivalPhase.Marching : RivalPhase.PlanDone, Step = 0 };

            s.Campaign = c;
            ErasModule.EnterCampaign(s);
            RegisterLandmarks(s, d);
            ev?.Add(new CampaignStarted { DefinitionId = d.Id });

            // Crystals already on an altar (6.3: a seed may start one stolen).
            foreach (var id in stolen)
            {
                var cd = d.Crystal(id);
                if (cd == null) continue;
                var step = plan.Find(x => x.Crystal == id);
                SetCustody(s, cd.Element, Custody.AtRivalAltar, step?.Altar ?? cd.Site, ev);
            }

            // Opening clues: facts the party knows from the start (5.4).
            foreach (var clue in d.Clues)
                if (clue.Source?.Kind == SourceKind.Opening && (clue.Requires == null || clue.Requires.Count == 0))
                    foreach (var f in clue.Gives) Learn(c, f, SourceKind.Opening, ev);

            // The expedition sets out on its first step (skipping crystals already on an altar).
            StartRivalStep(s, c, 0, ev);
        }

        /// <summary>The landmarks of appendix C: lm.tower.* at each tower's corner, lm.altar.* at each altar location's hex.</summary>
        private static void RegisterLandmarks(WorldState s, CampaignDefinition d)
        {
            var topo = s.Board?.Topology;
            if (topo == null) return;
            var towerCorners = LandmarkApi.TowerCorners(topo);
            foreach (var t in d.Towers)
            {
                var loc = d.Location(t.Location);
                if (loc == null) continue;
                var lm = new Landmark { Id = "lm.tower." + Lower(t.Element) };
                if (!Place(topo, loc.Anchor, lm)) lm.Corner = towerCorners[(int)t.Element];
                LandmarkApi.Register(s, lm);
            }
            foreach (var cd in d.Crystals)
            {
                var step = d.Rival?.Steps?.Find(x => x.Crystal == cd.Id);
                var loc = step != null ? d.Location(step.Altar.Location) : null;
                if (loc == null) continue;
                var lm = new Landmark { Id = "lm.altar." + Lower(cd.Element) };
                if (Place(topo, loc.Anchor, lm)) LandmarkApi.Register(s, lm);
            }
        }

        private static bool Place(BoardTopology topo, AnchorDef a, Landmark lm)
        {
            if (a == null) return false;
            switch (a.Kind)
            {
                case AnchorKind.RimCorner:
                    if (!topo.IsCorner(a.Corner)) return false;
                    lm.Corner = a.Corner;
                    return true;
                case AnchorKind.MeadowHexCentre:
                case AnchorKind.HomeCamp:
                {
                    int hex = topo.HexIndex(new HexCoord(a.Q, a.R));
                    if (hex < 0) return false;
                    lm.Hex = hex;
                    return true;
                }
                default: return false;
            }
        }

        /// <summary>"earth", "fire", "water", "air" (ledger keys "crystal:&lt;element&gt;", landmark ids).</summary>
        internal static string Lower(Element e) => e.ToString().ToLowerInvariant();

        // ---------------------------------------------------------------- attempts and the outcome table (5.6)

        private static void Commit(WorldState s, CampaignState c, string encounterId, EventBuffer ev)
        {
            var a = new AttemptState
            {
                Id = c.NextAttemptId++,
                EncounterId = encounterId,
                Volunteer = CampaignApi.NextVolunteerOf(c)?.Id ?? "",
                Seed = c.Rng.NextULong(),
                CommittedTick = s.Tick,
            };
            c.Attempt = a;
            c.Party.HoldAtThreshold = false;
            c.Party.WithdrawnFrom = "";
            SetMode(s, c, PartyMode.InEncounter, ev);
            ev?.Add(Committed(c, a));
        }

        private static EncounterCommitted Committed(CampaignState c, AttemptState a) => new EncounterCommitted
        {
            AttemptId = a.Id, EncounterId = a.EncounterId, Volunteer = a.Volunteer, Seed = a.Seed,
            Profile = c.Definition.Encounter(a.EncounterId)?.Profile ?? "",
        };

        /// <summary>
        /// Settles the pending attempt once (CR-29): the record, the event, then the outcome table, all in this one step.
        /// The attempt is cleared first, so a second resolution can never find it.
        /// </summary>
        private static void Resolve(WorldState s, CampaignState c, int attemptId, DuelResult result, EventBuffer ev)
        {
            var a = c.Attempt;
            if (a == null || a.Id != attemptId) return;
            c.Attempt = null;
            c.Resolved.Add(new AttemptRecord { Id = a.Id, EncounterId = a.EncounterId, Result = result, Volunteer = a.Volunteer, Tick = s.Tick });
            ev?.Add(new EncounterResolved { AttemptId = a.Id, EncounterId = a.EncounterId, Result = result });
            ApplyOutcome(s, c, a, result, ev);
        }

        /// <summary>The outcome table of 5.6.</summary>
        private static void ApplyOutcome(WorldState s, CampaignState c, AttemptState a, DuelResult result, EventBuffer ev)
        {
            var d = c.Definition;
            var e = d.Encounter(a.EncounterId);
            var p = c.Party;
            p.EncounterId = "";
            c.ReplanNow = true;
            string who = e != null && !string.IsNullOrEmpty(e.OpponentName) ? e.OpponentName : "the keeper";
            string place = CampaignTokens.PlaceName(d, p.At);
            switch (result)
            {
                case DuelResult.Victory:
                {
                    if (e != null && !c.CompletedEncounters.Contains(e.Id)) InsertSorted(c.CompletedEncounters, e.Id);
                    Journal(s, c, "Mira broke the Ward of " + who + " at " + place + ".", ev);
                    p.Purpose = RoutePurpose.None;
                    SetMode(s, c, PartyMode.Idle, ev);
                    if (e != null)
                        foreach (var fx in e.OnVictory ?? new List<EffectDef>()) ApplyEffect(s, c, fx, ev);
                    if (e != null && e.BoundTo == EncounterBindings.LastTower)
                    {
                        // Victory at the last tower restores the fourth crystal at once and ends the campaign (5.8).
                        var last = RemainingCrystal(c);
                        if (last.HasValue && c.Crystal(last.Value).Custody == Custody.Carried) Restore(s, c, last.Value, ev);
                        if (c.Phase == CampaignPhase.Running) End(s, c, true, ev);
                        return;
                    }
                    if (c.Phase == CampaignPhase.Running && p.Mode == PartyMode.Idle) Settle(s, c, ev);
                    return;
                }
                case DuelResult.Defeat:
                case DuelResult.Surrender:
                {
                    var vol = c.Companion(a.Volunteer);
                    if (vol == null || !vol.Alive)
                    {
                        var next = CampaignApi.NextVolunteerOf(c);
                        vol = next != null ? c.Companion(next.Id) : null;
                    }
                    if (vol == null)
                    {
                        // A lethal defeat with nobody left to cover the retreat ends the run (CR-30).
                        Journal(s, c, "Mira fell at " + place + ", and there was nobody left to hold the way out.", ev);
                        End(s, c, false, ev);
                        return;
                    }
                    vol.Alive = false;
                    vol.FellAt = a.EncounterId ?? "";
                    vol.FellTick = s.Tick;
                    vol.Cause = result == DuelResult.Surrender ? FallCause.Surrender : FallCause.Defeat;
                    vol.FellPlace = place;
                    ev?.Add(new CompanionFell { CompanionId = vol.Id, EncounterId = a.EncounterId });
                    string name = d.Companion(vol.Id)?.Name ?? vol.Id;
                    Journal(s, c, name + " held the way out at " + place + " so the others could get away.", ev);
                    PlaceAt(s, c, SafeOf(c, e), "defeat", ev);
                    Charge(s, c, d.Tuning.DefeatCharge, RivalCharges.Defeat, ev);
                    return;
                }
                default:
                {
                    // Clash or stalemate: nobody dies; a living retreat, cheaper while the shield-bearer lives.
                    Journal(s, c, "Neither side broke at " + place + ". The party fell back.", ev);
                    PlaceAt(s, c, SafeOf(c, e), "draw", ev);
                    bool shield = false;
                    foreach (var k in d.Companions)
                        if (k.Service == CompanionServices.Shield && (c.Companion(k.Id)?.Alive ?? false)) shield = true;
                    Charge(s, c, shield ? d.Tuning.DrawChargeWithShield : d.Tuning.DrawCharge, RivalCharges.Draw, ev);
                    return;
                }
            }
        }

        /// <summary>Puts the party at a place at once (a retreat after a duel), Idle there.</summary>
        private static void PlaceAt(WorldState s, CampaignState c, MarkerRef at, string why, EventBuffer ev)
        {
            var p = c.Party;
            if (!at.IsEmpty) p.At = at;
            p.Leg = null;
            p.Route.Clear();
            p.Pending.Clear();
            p.Purpose = RoutePurpose.None;
            p.LeadId = -1;
            p.LeadDefId = "";
            p.EncounterId = "";
            p.HoldAtThreshold = false;
            ev?.Add(new PartyRetreated { To = p.At, Why = why });
            SetMode(s, c, PartyMode.Idle, ev);
        }

        /// <summary>The run ends (5.8): phase, ending id, the record, CampaignEnded.</summary>
        private static void End(WorldState s, CampaignState c, bool won, EventBuffer ev)
        {
            if (c.Phase != CampaignPhase.Running) return;
            var outcome = won ? CampaignPhase.Won : CampaignPhase.Lost;
            var ending = c.Definition.Endings?.Find(x => x.Outcome == outcome);
            c.EndingId = ending?.Id ?? "";
            c.Phase = outcome;
            c.Leads.Clear();
            Journal(s, c, won ? "The four crystals are home. The island is held." : "The towers are going out.", ev);
            ev?.Add(new CampaignEnded { Won = won, EndingId = c.EndingId, Record = CampaignApi.Record(s) });
        }

        // ---------------------------------------------------------------- restoration (5.7) and the finale (5.8)

        private static void BeginRestore(WorldState s, CampaignState c, Element e, EventBuffer ev)
        {
            var p = c.Party;
            p.Pending.Clear();
            p.Route.Clear();
            p.Leg = null;
            p.ActingAction = QuestAction.Restore;
            p.ActingCrystal = c.Definition.Crystal(e)?.Id ?? "";
            p.ActingTicks = Math.Max(0, c.Definition.Tuning.RestoreTicks);
            p.ActingProgress = 0;
            p.Studying = false;
            p.BeganTick = s.Tick;
            if (p.ActingTicks <= 0) { Restore(s, c, e, ev); if (c.Phase == CampaignPhase.Running) Settle(s, c, ev); return; }
            SetMode(s, c, PartyMode.Acting, ev);
        }

        /// <summary>
        /// One restoration, in one step: custody Restored (the player's point), the order, the event, the belief, the
        /// island's change, the journal, Restore quest events at the tower; four restored ends the campaign.
        /// </summary>
        internal static void Restore(WorldState s, CampaignState c, Element e, EventBuffer ev)
        {
            var st = c.Crystal(e);
            if (st == null || st.Custody != Custody.Carried) return;
            var d = c.Definition;
            var cd = d.Crystal(e);
            var at = TowerRef(c, e);
            SetCustody(s, e, Custody.Restored, at, ev);
            if (!c.RestoredOrder.Contains(e)) c.RestoredOrder.Add(e);
            ev?.Add(new CrystalRestored { Crystal = e, Tower = cd?.Tower ?? "", Count = c.RestoredOrder.Count });
            SetBelief(s, c, e, Custody.Restored, at, SourceKind.Marker, ev);
            ApplyIslandEffects(s, c, ev);
            var tower = cd != null ? d.Tower(cd.Tower) : null;
            Journal(s, c, "Mira set " + (cd?.Name ?? e.ToString()) + " in " + (d.Location(tower?.Location ?? "")?.Name ?? "its tower") + ". The tower is lit.", ev);
            FireEvents(s, c, c.Party.At, QuestAction.Restore, ev);
            CheckLastTower(s, c, ev);
            if (CountCustody(c, Custody.Restored) >= 4) End(s, c, true, ev);
        }

        /// <summary>The tower place of a crystal (its location's goal marker).</summary>
        internal static MarkerRef TowerRef(CampaignState c, Element e)
        {
            var d = c.Definition;
            var cd = d.Crystal(e);
            var tower = cd != null ? d.Tower(cd.Tower) : null;
            return tower != null ? LocationGoal(d.Location(tower.Location)) : default;
        }

        /// <summary>The one crystal not yet restored, when exactly one is left.</summary>
        internal static Element? RemainingCrystal(CampaignState c)
        {
            Element? left = null;
            for (int i = 0; i < 4; i++)
            {
                var st = c.Crystal((Element)i);
                if (st == null || st.Custody == Custody.Restored) continue;
                if (left.HasValue) return null;
                left = (Element)i;
            }
            return left;
        }

        /// <summary>The finale encounter (bound to the last tower), null when the pack has none.</summary>
        internal static EncounterDef LastTowerEncounter(CampaignDefinition d) =>
            d.Encounters?.Find(x => x.Kind == EncounterKind.Lethal && x.BoundTo == EncounterBindings.LastTower);

        /// <summary>Three crystals are restored, this is the fourth and the finale stands at its tower (not yet won).</summary>
        internal static bool FinaleHolds(CampaignState c, Element e)
        {
            var fin = LastTowerEncounter(c.Definition);
            if (fin == null || c.CompletedEncounters.Contains(fin.Id)) return false;
            return CountCustody(c, Custody.Restored) == 3 && RemainingCrystal(c) == e;
        }

        internal static int CountCustody(CampaignState c, Custody k)
        {
            int n = 0;
            for (int i = 0; i < 4; i++) if (c.Crystal((Element)i)?.Custody == k) n++;
            return n;
        }

        /// <summary>
        /// The island's change (5.7), always recomputed from the saved base values so nothing stacks: demons draw every
        /// max(1, base + restored - on altars) sevens (never again with four restored, and the board is cleared); the
        /// Deepstone makes Hills and Mountain grow BoonPercent faster, the Tideglass Fields and Pasture, the Skyshard
        /// takes one tick off each carrier step. The Kilnheart's workshop boon waits for EconomyTuning.CycleBonusPermille
        /// (PRD_AGES H1-2), which does not exist yet.
        /// </summary>
        internal static void ApplyIslandEffects(WorldState s, CampaignState c, EventBuffer ev)
        {
            int restored = CountCustody(c, Custody.Restored);
            int onAltar = CountCustody(c, Custody.AtRivalAltar);
            var demons = s.Demons;
            if (demons?.Tuning != null)
            {
                if (restored >= 4)
                {
                    demons.Tuning.SevensPerDraw = int.MaxValue;
                    for (int k = 0; k < (demons.Corner?.Length ?? 0); k++)
                        if (demons.Corner[k] > 0) DemonLogic.Banish(s, DemonLogic.SiteFaction(s, DemonSiteKind.Corner, k), DemonSiteKind.Corner, k, ev);
                    for (int k = 0; k < (demons.Edge?.Length ?? 0); k++)
                        if (demons.Edge[k] > 0) DemonLogic.Banish(s, DemonLogic.SiteFaction(s, DemonSiteKind.HexEdge, k), DemonSiteKind.HexEdge, k, ev);
                }
                else demons.Tuning.SevensPerDraw = Math.Max(1, c.BaseSevensPerDraw + restored - onAltar);
            }
            var econ = s.Economy?.Tuning;
            if (econ == null) return;
            if (c.BaseGrowTicks == null && econ.GrowTicks != null) c.BaseGrowTicks = (int[])econ.GrowTicks.Clone();
            if (c.BaseCarrierTicksPerStep < 0) c.BaseCarrierTicksPerStep = econ.CarrierTicksPerStep;
            bool earth = c.Crystal(Element.Earth)?.Custody == Custody.Restored;
            bool water = c.Crystal(Element.Water)?.Custody == Custody.Restored;
            bool air = c.Crystal(Element.Air)?.Custody == Custody.Restored;
            int pct = Math.Max(0, c.Definition.Tuning.BoonPercent);
            if (c.BaseGrowTicks != null && econ.GrowTicks != null)
            {
                for (int t = 0; t < econ.GrowTicks.Length && t < c.BaseGrowTicks.Length; t++)
                {
                    var terrain = (Terrain)t;
                    bool boon = (earth && (terrain == Terrain.Hills || terrain == Terrain.Mountain))
                        || (water && (terrain == Terrain.Fields || terrain == Terrain.Pasture));
                    econ.GrowTicks[t] = boon ? Math.Max(1, c.BaseGrowTicks[t] * 100 / (100 + pct)) : c.BaseGrowTicks[t];
                }
            }
            econ.CarrierTicksPerStep = Math.Max(1, c.BaseCarrierTicksPerStep - (air ? 1 : 0));
        }

        // ---------------------------------------------------------------- shared steps

        /// <summary>
        /// One custody change (5.2): one event, guarded by the state it starts from. Pays the ledger in the same step:
        /// Restored pays the player "crystal:&lt;element&gt;"; the first AtRivalAltar pays the Ash Clan the same key.
        /// False (and nothing changes) when the crystal is already in that custody.
        /// </summary>
        internal static bool SetCustody(WorldState s, Element e, Custody to, MarkerRef at, EventBuffer ev)
        {
            var c = Run(s);
            var st = c?.Crystal(e);
            if (st == null || st.Custody == to) return false;
            var from = st.Custody;
            st.Custody = to;
            st.At = at;
            st.SinceTick = s.Tick;
            ev?.Add(new CrystalCustodyChanged { Crystal = e, From = from, To = to, At = at });
            string key = "crystal:" + Lower(e);
            if (to == Custody.AtRivalAltar && !c.EverOnAltar.Contains(e))
            {
                c.EverOnAltar.Add(e);
                LedgerLogic.Award(s, PlayerIds.Rival, key, 1, ev);
            }
            if (to == Custody.Restored) LedgerLogic.Award(s, PlayerIds.Human, key, 1, ev);
            if (from == Custody.AtRivalAltar || to == Custody.AtRivalAltar) ApplyIslandEffects(s, c, ev);
            c.ReplanNow = true;
            return true;
        }

        /// <summary>Adds a fact once (kept sorted ordinal) and raises FactLearned. False if already known.</summary>
        internal static bool Learn(CampaignState c, string fact, SourceKind source, EventBuffer ev)
        {
            if (string.IsNullOrEmpty(fact)) return false;
            int i = c.Facts.BinarySearch(fact, StringComparer.Ordinal);
            if (i >= 0) return false;
            c.Facts.Insert(~i, fact);
            ev?.Add(new FactLearned { Fact = fact, Source = source });
            c.ReplanNow = true;
            return true;
        }

        internal static void SetMode(WorldState s, CampaignState c, PartyMode mode, EventBuffer ev)
        {
            if (c.Party.Mode == mode) return;
            c.Party.Mode = mode;
            c.Party.ModeSinceTick = s.Tick;
            ev?.Add(new PartyModeChanged { Mode = mode });
        }

        /// <summary>One journal sentence (CR-20), at the party's place.</summary>
        internal static void Journal(WorldState s, CampaignState c, string text, EventBuffer ev)
        {
            if (string.IsNullOrEmpty(text)) return;
            c.Journal.Add(new JournalEntry { Tick = s.Tick, Text = text, Place = c.Party?.At.Location ?? "" });
            ev?.Add(new JournalAdded { Text = text });
        }

        /// <summary>The party's belief about a crystal; BeliefChanged when custody or place differ.</summary>
        internal static void SetBelief(WorldState s, CampaignState c, Element e, Custody k, MarkerRef at, SourceKind source, EventBuffer ev)
        {
            var b = c.BeliefOf(e);
            if (b == null) return;
            bool changed = b.Custody != k || !Same(b.At, at);
            b.Custody = k;
            b.At = at;
            b.AsOfTick = s.Tick;
            b.Source = source;
            if (changed) { ev?.Add(new BeliefChanged { Crystal = e }); c.ReplanNow = true; }
        }

        internal static void SetBeliefTruth(WorldState s, CampaignState c, Element e, SourceKind source, EventBuffer ev)
        {
            var st = c.Crystal(e);
            if (st != null) SetBelief(s, c, e, st.Custody, st.At, source, ev);
        }

        private static void InsertSorted(List<string> list, string x)
        {
            int i = list.BinarySearch(x, StringComparer.Ordinal);
            if (i < 0) list.Insert(~i, x);
        }

        // ---------------------------------------------------------------- tick

        /// <summary>
        /// The campaign slot (1250, after Strength). While Running: the expedition clock's +1 (5.1); the overrun link
        /// (CampOverrun raised earlier this tick); the party; the scout; the rival; reports reaching the party; Mira's own
        /// scouting; then Mira's plan (every PlanInterval, or at once after a change). The party moves before the rival,
        /// so a party arriving at a pedestal in the same tick as the rival wins the tie.
        /// </summary>
        public static void Tick(WorldState s, EventBuffer ev)
        {
            var c = Run(s);
            if (c == null || c.Phase != CampaignPhase.Running) return;
            c.RivalProgress++;
            ScanOverruns(s, c, ev);
            if (c.Phase != CampaignPhase.Running) return;
            TickParty(s, c, ev);
            if (c.Phase != CampaignPhase.Running) return;
            TickScout(s, c, ev);
            TickRival(s, c, ev);
            DeliverReports(s, c, ev);
            AutoScout(s, c, ev);
            int interval = Math.Max(1, c.Definition.Tuning.PlanInterval);
            if (c.ReplanNow || (s.Tick - c.StartTick) % interval == 0) Plan(s, c, ev);
        }
    }
}
