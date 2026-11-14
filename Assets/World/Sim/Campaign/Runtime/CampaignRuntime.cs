using System;
using System.Collections.Generic;
using World.Sim.Eras;
using World.Sim.Ledger;

namespace World.Sim.Campaign
{
    /// <summary>
    /// The campaign's behaviour: command checks, command effects and the per-tick step. OWNED BY PACKAGE I1 from tag
    /// cry-I0-sim (Sim/Campaign/Runtime/**); I0 seeded it with a safe first version. Everything marked "I0 STUB" is
    /// accepted and deterministic but has no effect yet. Real in I0: every refusal of A.3; StartCampaign (state from the
    /// definition, stolen-at-start crystals, opening facts, campaign mode, landmarks); SetCompany; CommitEncounter and the
    /// attempt; ResolveEncounter's once-only bookkeeping; RestartAttempt; the +1 per tick of the expedition clock; the
    /// custody step with its ledger points (SetCustody); the dev commands except DevRivalNextStep.
    /// </summary>
    internal static class CampaignRuntime
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
                    return HasLead(c, al.LeadId) ? CommandResult.Accept() : CampaignRefusals.Reject(CampaignRefusals.UnknownLead);
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
                    c.Party.PlayerPresent = sc.Present;
                    break;
                case CommitEncounter ce:
                    Commit(s, c, ce.EncounterId, ev);
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
                case DevSetRivalProgress dr:
                    c.RivalProgress = Math.Max(0, dr.Value);
                    break;
                case DevSetCustody dc:
                {
                    var st = c.Crystal(dc.Crystal);
                    SetCustody(s, dc.Crystal, dc.To, st.At, ev);
                    break;
                }
                case DevSetCompanion dk:
                {
                    var k = c.Companion(dk.Id);
                    if (k == null) break;
                    k.Alive = dk.Alive;
                    k.FellAt = dk.Alive ? "" : "dev";
                    k.FellTick = dk.Alive ? -1 : s.Tick;
                    break;
                }
                // I0 STUB: accepted with no effect until I1 (leads, Mira's advice, scouting, withdrawal, restoration, rival step).
                case AcceptLead _:
                case DeclineLead _:
                case AdviseMira _:
                case SuggestScout _:
                case WithdrawFromEncounter _:
                case ConfirmRestore _:
                case DevRivalNextStep _:
                    break;
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
            };
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

            var home = d.Locations.Find(l => l.Kind == LocationKind.Camp);
            c.Party = new PartyState
            {
                Mode = PartyMode.Idle, PlayerPresent = true, ModeSinceTick = s.Tick,
                At = home != null ? MarkerRef.Surface(home.Id, home.SafePoint ?? "") : default,
            };
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
            SetMode(s, c, PartyMode.InEncounter, ev);
            ev?.Add(Committed(c, a));
        }

        private static EncounterCommitted Committed(CampaignState c, AttemptState a) => new EncounterCommitted
        {
            AttemptId = a.Id, EncounterId = a.EncounterId, Volunteer = a.Volunteer, Seed = a.Seed,
            Profile = c.Definition.Encounter(a.EncounterId)?.Profile ?? "",
        };

        /// <summary>Settles the pending attempt once: the record, the event, then the outcome table (5.6).</summary>
        private static void Resolve(WorldState s, CampaignState c, int attemptId, DuelResult result, EventBuffer ev)
        {
            var a = c.Attempt;
            c.Attempt = null;
            c.Resolved.Add(new AttemptRecord { Id = a.Id, EncounterId = a.EncounterId, Result = result, Volunteer = a.Volunteer, Tick = s.Tick });
            ev?.Add(new EncounterResolved { AttemptId = a.Id, EncounterId = a.EncounterId, Result = result });
            ApplyOutcome(s, c, a, result, ev);
        }

        /// <summary>
        /// I0 STUB: the outcome table of 5.6 (victory effects, the volunteer's death, retreat, rival charges, terminal
        /// defeat) is I1's. I0 only returns the party to Idle where it stands.
        /// </summary>
        private static void ApplyOutcome(WorldState s, CampaignState c, AttemptState a, DuelResult result, EventBuffer ev)
        {
            SetMode(s, c, PartyMode.Idle, ev);
        }

        // ---------------------------------------------------------------- shared steps (for I1)

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
            return true;
        }

        internal static void SetMode(WorldState s, CampaignState c, PartyMode mode, EventBuffer ev)
        {
            if (c.Party.Mode == mode) return;
            c.Party.Mode = mode;
            c.Party.ModeSinceTick = s.Tick;
            ev?.Add(new PartyModeChanged { Mode = mode });
        }

        // ---------------------------------------------------------------- tick

        /// <summary>
        /// The campaign slot. Real in I0: the expedition clock's +1 per tick while Running (5.1). I0 STUB: the rival plan,
        /// reports, Mira's brain, legs, acting, waits, the scout and restoration are I1's.
        /// </summary>
        public static void Tick(WorldState s, EventBuffer ev)
        {
            var c = Run(s);
            if (c == null || c.Phase != CampaignPhase.Running) return;
            c.RivalProgress++;
        }
    }
}
