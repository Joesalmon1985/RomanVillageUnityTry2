using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Encounters.Ward;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The crystal campaign's duel glue (PRD_CRYSTALS 5.6, 5.12, appendix B, package I7). It turns the sim's
    /// <see cref="EncounterCommitted"/> into a <see cref="WardDuelRequest"/> (mode, profile, attempt, seed, names and the
    /// stage read from the definition's markers), starts <c>ward_campaign</c> through the <see cref="EncounterHost"/>, and
    /// sends exactly one <c>ResolveEncounter</c> per attempt at the duel's end event (then one clock step, so the outcome
    /// table is applied before the result screen can be left).
    /// <para>
    /// Staging is automatic: a per-scene driver listens to <see cref="GameClock.Ticked"/> and stages every
    /// <see cref="EncounterCommitted"/> (so I6 only sends <c>CommitEncounter</c>; calling <see cref="Start"/> as well is
    /// harmless: the same attempt and seed is never staged twice). A pending attempt with no duel running (the game was
    /// closed or the scene reloaded mid-duel) is continued with <c>RestartAttempt</c>: a new seed, no penalty.
    /// </para>
    /// Friendly and practice duels never create an attempt (<see cref="Friendly"/>, <see cref="Practice"/>).
    /// Owner: I7 (this file); from tag cry-I7 the Ward files pass to characters WP-9, this one stays with the crystals lane.
    /// </summary>
    public static class CampaignDuel
    {
        /// <summary>Raised once per attempt, after the sim applied the result (the outcome table has run). I8 writes the checkpoint here.</summary>
        public static event Action<int, DuelResult> Resolved;
        /// <summary>Raised when a campaign-mode duel (lethal or friendly) has been staged (I6 hides its own party bodies while it runs).</summary>
        public static event Action<WardDuelRequest> Staged;

        /// <summary>Stage every EncounterCommitted automatically (tests may turn it off to stage by hand).</summary>
        public static bool AutoStage = true;
        /// <summary>Continue a pending attempt with RestartAttempt when no duel runs (after a load or an abort).</summary>
        public static bool AutoRestart = true;
        /// <summary>Seconds a pending attempt must sit without a duel before it is restarted (a scene change may be under way).</summary>
        public const float RestartDelay = 0.5f;

        /// <summary>Accepted ResolveEncounter commands sent by this game session (tests).</summary>
        public static int ResolvesSent { get; private set; }
        /// <summary>RestartAttempt commands sent by this game session (tests).</summary>
        public static int RestartsSent { get; private set; }
        /// <summary>The request of the last staged duel (tests, captures).</summary>
        public static WardDuelRequest LastRequest { get; private set; }

        private static readonly HashSet<int> s_resolved = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Resolved = null; Staged = null; AutoStage = true; AutoRestart = true;
            ResolvesSent = 0; RestartsSent = 0; LastRequest = null; s_resolved.Clear();
        }

        /// <summary>Tests: clears the counters and the resolved set (a new game session in the same Play Mode).</summary>
        public static void ResetSession()
        {
            ResolvesSent = 0; RestartsSent = 0; LastRequest = null; s_resolved.Clear(); AutoStage = true; AutoRestart = true; s_opponents = null;
        }

        /// <summary>The factory registered under <see cref="EncounterDirectory.WardCampaign"/>.</summary>
        public static IEncounter Create() => new WardDuelEncounter();

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static WorldState S => B != null && B.Sim != null ? B.Sim.State : null;

        // ------------------------------------------------------------------ starting

        /// <summary>
        /// Starts the duel the sim committed (lethal: a keeper or the finale). True if it is running now (also when this
        /// attempt and seed were already staged). False with no campaign, no such attempt, or another encounter running.
        /// </summary>
        public static bool Start(EncounterCommitted ev)
        {
            var req = RequestFor(S, ev);
            return req != null && StartRequest(req);
        }

        /// <summary>Starts any request (lethal, friendly or practice) through the encounter host.</summary>
        public static bool StartRequest(WardDuelRequest req, Npc source = null)
        {
            if (req == null || EncounterHost.Instance == null) return false;
            var cur = WardDuelEncounter.Current;
            if (cur != null && cur.Request != null && req.IsCampaign && cur.Request.AttemptId == req.AttemptId && cur.Request.Seed == req.Seed)
                return true; // already staged (the driver and a caller both reacted to one commit)
            if (req.IsCampaign && s_resolved.Contains(req.AttemptId)) return false;
            if (source == null) source = Npc.Find(NpcIds.Mira);
            bool ok = EncounterDirectory.TryStart(EncounterDirectory.WardCampaign, source, req);
            if (ok)
            {
                LastRequest = req;
                Debug.Log("[CampaignDuel] staged " + req + " " + (req.Stage != null ? req.Stage.ToString() : "default stage"));
                if (req.Mode != EncounterKind.Practice) Staged?.Invoke(req);
            }
            return ok;
        }

        /// <summary>The request for a commit event: mode and names from the definition, the stage from its markers. Null if unknown.</summary>
        public static WardDuelRequest RequestFor(WorldState s, EncounterCommitted ev)
        {
            var c = s?.Campaign;
            var d = c?.Definition;
            if (ev == null || d == null) return null;
            var e = d.Encounter(ev.EncounterId);
            var req = WardDuelRequest.From(ev, e != null ? e.Kind : EncounterKind.Lethal);
            if (string.IsNullOrEmpty(req.ProfileId)) req.ProfileId = e?.Profile;
            var prof = Opponents().GetOrDefault(req.ProfileId);
            req.OpponentName = !string.IsNullOrEmpty(e?.OpponentName) ? e.OpponentName : prof?.DisplayName ?? "the keeper";
            var vol = string.IsNullOrEmpty(ev.Volunteer) ? null : d.Companion(ev.Volunteer);
            req.VolunteerName = vol?.Name;
            req.Stage = StageFor(s, e, vol);
            return req;
        }

        /// <summary>A friendly duel (Orrin's demonstration, the champion preset): Mira casts, nobody is at risk, no attempt.</summary>
        public static WardDuelRequest Friendly(string profileId, string opponentName = null, string opponentActorId = null, DuelStageSpec stage = null, ulong seed = 0)
        {
            var prof = Opponents().GetOrDefault(profileId);
            return new WardDuelRequest
            {
                Mode = EncounterKind.Friendly, AttemptId = -1, ProfileId = profileId, Seed = seed,
                OpponentName = opponentName ?? prof?.DisplayName ?? "the stranger",
                Stage = stage ?? StageNearPlayer(opponentActorId ?? profileId, null),
            };
        }

        /// <summary>The practice duel with Mira as a request (identical to starting <c>ward_mira</c> with no request).</summary>
        public static WardDuelRequest Practice() => new WardDuelRequest { Mode = EncounterKind.Practice, ProfileId = WardOpponents.PracticeMira, OpponentName = "Mira" };

        // ------------------------------------------------------------------ stage

        /// <summary>Who stands where, from the encounter's stage markers (surface or interior). Null fields fall back to a stage at the player.</summary>
        public static DuelStageSpec StageFor(WorldState s, EncounterDef e, CompanionDef volunteer)
        {
            var d = s?.Campaign?.Definition;
            var spec = new DuelStageSpec
            {
                CasterActorId = NpcIds.Mira,
                OpponentActorId = !string.IsNullOrEmpty(e?.OpponentActorId) ? e.OpponentActorId : e?.Profile ?? "keeper",
                VolunteerActorId = volunteer != null ? (!string.IsNullOrEmpty(volunteer.ActorId) ? volunteer.ActorId : volunteer.Id) : null,
            };
            MarkerRef caster = e?.Stage?.Caster ?? default, opp = e?.Stage?.Opponent ?? default, vol = e?.Stage?.Volunteer ?? default;
            if (e != null && e.BoundTo == EncounterBindings.LastTower && s.Campaign.Party != null)
            {
                // The finale is staged at whichever tower the party stands at (every tower has caster and opponent markers).
                string loc = s.Campaign.Party.At.Location;
                caster = MarkerRef.Surface(loc, "caster"); opp = MarkerRef.Surface(loc, "opponent"); vol = s.Campaign.Party.At;
            }
            bool ok = TryMarkerWorld(s, caster, out spec.CasterPos) & TryMarkerWorld(s, opp, out spec.OpponentPos);
            if (!ok)
            {
                var fb = StageNearPlayer(spec.OpponentActorId, spec.VolunteerActorId);
                fb.VolunteerActorId = spec.VolunteerActorId;
                return fb;
            }
            if (!spec.HasVolunteer || !TryMarkerWorld(s, vol, out spec.VolunteerPos) || (spec.VolunteerPos - spec.CasterPos).sqrMagnitude < 1f)
                spec.VolunteerPos = DefaultVolunteerPos(spec.CasterPos, spec.OpponentPos);
            return spec;
        }

        /// <summary>World point of a marker: surface markers from the location's anchor cell, interior markers through <see cref="InteriorSpace"/> (I5).</summary>
        public static bool TryMarkerWorld(WorldState s, MarkerRef r, out Vector3 p)
        {
            p = default;
            var d = s?.Campaign?.Definition;
            if (d == null || string.IsNullOrEmpty(r.Location) && string.IsNullOrEmpty(r.Interior)) return false;
            if (r.IsInterior)
            {
                var m = d.Interior(r.Interior);
                if (m == null || !InteriorMaps.TryFind(m, r.Marker, out int ii, out int jj)) return false;
                p = InteriorSpace.ToWorld(r.Interior, ii, jj);
                return true;
            }
            if (!CampaignPlaces.TryMarkerCell(d, r, s.Board, out int i, out int j)) return false;
            p = WalkLattice.ToWorld(new Cell(i, j));
            p.y = PlayerWalker.GroundHeight(p);
            return true;
        }

        /// <summary>A stage in front of the player's viewpoint (friendly duels away from a site, and the fallback).</summary>
        public static DuelStageSpec StageNearPlayer(string opponentActorId, string volunteerActorId)
        {
            var b = B;
            Vector3 at = b != null && b.Avatar != null ? b.Avatar.Position : Vector3.zero;
            Vector3 fwd = b != null && b.Avatar != null ? BoardSpace.DirOf(b.Avatar.Yaw) : Vector3.forward;
            at.y = PlayerWalker.GroundHeight(at);
            var caster = at + fwd * 2.5f;
            var opp = caster + fwd * WardDuelEncounter.DuelDistance;
            caster.y = PlayerWalker.GroundHeight(caster); opp.y = PlayerWalker.GroundHeight(opp);
            return new DuelStageSpec
            {
                CasterActorId = NpcIds.Mira, OpponentActorId = opponentActorId, VolunteerActorId = volunteerActorId,
                CasterPos = caster, OpponentPos = opp, VolunteerPos = DefaultVolunteerPos(caster, opp),
            };
        }

        /// <summary>Behind Mira and to her left: the way out, in frame of the over-the-shoulder view.</summary>
        public static Vector3 DefaultVolunteerPos(Vector3 caster, Vector3 opponent)
        {
            var f = opponent - caster; f.y = 0; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            var left = Vector3.Cross(f, Vector3.up).normalized;
            return caster - f * 1.6f + left * 2.4f;
        }

        // ------------------------------------------------------------------ lines

        /// <summary>The volunteer's first name ("Garrick" for "Garrick Holt"), or null.</summary>
        public static string FirstName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int sp = name.IndexOf(' ');
            return sp > 0 ? name.Substring(0, sp) : name;
        }

        /// <summary>The stakes line before a lethal duel (I6 may also use it in Mira's threshold bubble).</summary>
        public static string StakesLine(WardDuelRequest r)
        {
            if (r == null || r.Mode != EncounterKind.Lethal) return null;
            string v = FirstName(r.VolunteerName);
            return v != null ? v + " will hold the way out." : "Nobody is left to hold the way out. If Mira's Ward breaks, it ends here.";
        }

        /// <summary>The confirmation of "Leave encounter" in a lethal duel (PRD 5.6).</summary>
        public static string SurrenderLine(WardDuelRequest r)
        {
            string v = FirstName(r?.VolunteerName);
            return (v ?? "Mira") + " will pay for this. Leave?";
        }

        // ------------------------------------------------------------------ resolving

        /// <summary>
        /// Sends ResolveEncounter for the attempt once (a second call, a second end event or a stale attempt is a no-op), steps
        /// the clock one tick so the outcome table applies now, then raises <see cref="Resolved"/>. True if it was sent.
        /// </summary>
        internal static bool SendResolve(int attemptId, DuelResult result)
        {
            var b = B;
            if (attemptId < 0 || b == null || b.Sim == null) return false;
            if (!s_resolved.Add(attemptId)) { Debug.Log("[CampaignDuel] attempt " + attemptId + " already resolved here: " + result + " not sent again"); return false; }
            var pending = CampaignApi.Attempt(b.Sim.State);
            if (pending == null || pending.Id != attemptId)
            {
                Debug.LogWarning("[CampaignDuel] attempt " + attemptId + " is not the pending attempt; " + result + " not sent");
                return false;
            }
            var r = b.Sim.Submit(new ResolveEncounter { AttemptId = attemptId, Result = result });
            if (!r.Ok) { Debug.LogWarning("[CampaignDuel] ResolveEncounter refused: " + r.Reason); return false; }
            ResolvesSent++;
            b.Clock.StepOnce(); // applied at the end event, before the presentation hold (PRD 5.6 step 3)
            Debug.Log("[CampaignDuel] attempt " + attemptId + " resolved: " + result + " at tick " + b.Sim.State.Tick);
            RaiseResolved(attemptId, result);
            return true;
        }

        internal static void RaiseResolved(int attemptId, DuelResult result) => Resolved?.Invoke(attemptId, result);

        /// <summary>True when this attempt was already resolved by this game session.</summary>
        public static bool WasResolved(int attemptId) => s_resolved.Contains(attemptId);

        /// <summary>
        /// Continues a pending attempt that has no duel (after a load, or a duel left without surrender): RestartAttempt with
        /// a new seed and no penalty; the commit event it raises stages the duel. True if sent.
        /// </summary>
        public static bool RestartPending()
        {
            var b = B;
            if (b == null || b.Sim == null || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning)) return false;
            var a = CampaignApi.Attempt(b.Sim.State);
            if (a == null || s_resolved.Contains(a.Id)) return false;
            var r = b.Sim.Submit(new RestartAttempt { AttemptId = a.Id });
            if (!r.Ok) { Debug.LogWarning("[CampaignDuel] RestartAttempt refused: " + r.Reason); return false; }
            RestartsSent++;
            Debug.Log("[CampaignDuel] restarting attempt " + a.Id + " (restarts " + a.Restarts + " -> " + (a.Restarts + 1) + ")");
            b.Clock.StepOnce(); // EncounterCommitted again -> staged by the driver
            if (!AutoStage) return true;
            return WardDuelEncounter.Current != null;
        }

        // ------------------------------------------------------------------ menu

        private static float s_armedAt = -100f;

        /// <summary>
        /// Draws the pause menu's "Leave encounter" for a lethal duel (one line in Hud): first press asks
        /// "&lt;Volunteer&gt; will pay for this. Leave?", the second surrenders (counts as a defeat). Returns false, drawing
        /// nothing, for every other encounter (Hud then draws its own harmless button).
        /// </summary>
        public static bool DrawLeaveButton(Rect r, Action closeMenu)
        {
            var e = WardDuelEncounter.Current;
            if (e == null || !e.IsLethal || EncounterHost.Instance == null || EncounterHost.Instance.Current != e) return false;
            bool armed = Time.unscaledTime - s_armedAt < 6f;
            if (e.Phase == WardDuelEncounter.Screen.Result)
            {
                if (GUI.Button(r, "Leave encounter", UiStyle.Button)) { e.Return(); closeMenu?.Invoke(); }
                return true;
            }
            string label = armed ? SurrenderLine(e.Request) : "Leave encounter";
            if (GUI.Button(r, label, UiStyle.Button))
            {
                if (!armed) s_armedAt = Time.unscaledTime;
                else { s_armedAt = -100f; e.Surrender(); closeMenu?.Invoke(); }
            }
            return true;
        }

        /// <summary>The armed state of the surrender confirmation (tests).</summary>
        public static bool SurrenderArmed => Time.unscaledTime - s_armedAt < 6f;
        internal static void ArmSurrender() => s_armedAt = Time.unscaledTime;

        // ------------------------------------------------------------------ data

        private static WardOpponents s_opponents;

        /// <summary>The opponent table: StreamingAssets/world/ward_opponents.json, or the built-in table.</summary>
        public static WardOpponents Opponents()
        {
            if (s_opponents != null) return s_opponents;
            try
            {
                var json = EncounterTuningFiles.ReadText("ward_opponents.json");
                s_opponents = json != null ? WardOpponents.Parse(json) : WardOpponents.BuiltIn();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CampaignDuel] ward_opponents.json ignored: " + ex.Message);
                s_opponents = WardOpponents.BuiltIn();
            }
            return s_opponents;
        }

        // ------------------------------------------------------------------ per scene

        /// <summary>Called by <see cref="EncounterInstaller"/> after each scene load: binds the driver to the new world.</summary>
        internal static void BindScene()
        {
            var b = B;
            if (b == null || b.GetComponent<CampaignDuelDriver>() != null) return;
            b.gameObject.AddComponent<CampaignDuelDriver>();
        }
    }

    /// <summary>Per-scene half of <see cref="CampaignDuel"/>: stages commits from the clock's events and restarts a pending attempt.</summary>
    public sealed class CampaignDuelDriver : MonoBehaviour
    {
        private WorldBootstrap _boot;
        private float _noDuelSince = -1f;

        private void Start()
        {
            _boot = GetComponent<WorldBootstrap>();
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked += OnTicked;
        }

        private void OnDestroy()
        {
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            if (!CampaignDuel.AutoStage) return;
            foreach (var ev in events)
                if (ev is EncounterCommitted ec) CampaignDuel.Start(ec);
        }

        private void Update()
        {
            if (!CampaignDuel.AutoRestart || _boot == null || _boot.Sim == null) return;
            bool running = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            var a = running ? null : CampaignApi.Attempt(_boot.Sim.State);
            if (a == null || Hud.MenuOpen) { _noDuelSince = -1f; return; }
            if (_noDuelSince < 0f) { _noDuelSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - _noDuelSince < CampaignDuel.RestartDelay) return;
            _noDuelSince = -1f;
            CampaignDuel.RestartPending();
        }
    }
}
