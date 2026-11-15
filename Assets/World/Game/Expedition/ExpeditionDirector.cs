using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The expedition in play (PRD_CRYSTALS 5.5, appendix B, package I6): Mira leading the crystal expedition on her
    /// own initiative, the six companions walking with her, her conversation with the player in speech bubbles, the
    /// player leaving and rejoining, the cues while away, and the staging of what the sim decides (legs, actions,
    /// thresholds, a companion covering a retreat, the restoration). Modelled on <see cref="PrinceDirector"/>:
    /// approach, ask, lead with the player following, act at the spot, call when the player lags. The differences
    /// (5.5): Mira needs no spell, does not pester, and goes on alone when not answered.
    ///
    /// It reads <c>WorldState.Campaign</c> through <see cref="CampaignApi"/>, animates the tick's events and sends
    /// commands (<c>SetCompany</c>, <c>AcceptLead</c>, <c>DeclineLead</c>, <c>AdviseMira</c>, <c>CommitEncounter</c>,
    /// <c>WithdrawFromEncounter</c>, <c>ConfirmRestore</c>, and <c>ResolveEncounter</c> only through the duel stand-in
    /// while package I7's duel is not in the build). It never writes state. The party is where the sim says it is:
    /// a leg is walked along the lattice in exactly the leg's ticks (the pace comes from <c>PartySetOut</c>, never from
    /// a number here). Everything is rebuilt from state after a load. Split over ExpeditionDirector.Party.cs (bodies,
    /// legs, formation, gestures, the retreat) and ExpeditionDirector.Talk.cs (lines, questions, company, cues).
    /// Created by <see cref="ExpeditionInstaller"/> only while a campaign exists.
    /// </summary>
    public sealed partial class ExpeditionDirector : MonoBehaviour
    {
        /// <summary>One line said in the expedition (speech bubbles), for tests and the journal.</summary>
        public struct Line { public long Tick; public string Speaker; public string Text; }

        public static ExpeditionDirector Instance { get; private set; }

        /// <summary>Mira (the camp's Mira, taken over while the campaign runs).</summary>
        public Npc Mira { get; private set; }
        /// <summary>The six companions in roster order (the fallen stay in the list, lying where they fell).</summary>
        public IReadOnlyList<Npc> Companions => _companionNpcs;
        /// <summary>The party's mode as last shown (from <c>CampaignApi.Party</c>).</summary>
        public PartyMode Phase { get; private set; }
        /// <summary>Every line said, in order.</summary>
        public IReadOnlyList<Line> Said => _said;

        /// <summary>Scene steps (the save's <c>scene.expedition</c> extra, package I8).</summary>
        public const int SceneNotStarted = 0, SceneOpening = 1, SceneUnderWay = 2;
        /// <summary>The save's <c>scene.expedition</c> step: 0 before the opening, 1 during it, 2 once Mira has asked "Come with me?".</summary>
        public int SceneStep { get; private set; }
        /// <summary>Restores the saved scene step after a load (a step under 2 plays the opening again from its question).</summary>
        public void RestoreScene(int step) { SceneStep = Mathf.Clamp(step, SceneNotStarted, SceneUnderWay); _sceneRestored = true; }

        // ---------------- tuning (game seconds, metres; the party's pace is the sim's) ----------------
        /// <summary>Mira coming to the player for the opening.</summary>
        public static float ApproachSpeed = 5f;
        /// <summary>She stops this far in front of the player to talk.</summary>
        public static float TalkDistance = 5.5f;
        /// <summary>The player counts as lagging beyond this (she calls), as the prince does.</summary>
        public static float LagDistance = 22f;
        public static float CallEvery = 8f;
        /// <summary>Beyond this for <see cref="AwaySeconds"/> the player has left her (PRD 5.5: 60 m, 20 s).</summary>
        public static float AwayDistance = 60f, AwaySeconds = 20f;
        /// <summary>Within this an answer to one of her questions rejoins (PRD 5.5).</summary>
        public static float AnswerDistance = 15f;
        /// <summary>Her questions are asked when the player is within this (else she waits; the map says where).</summary>
        public static float AskDistance = 30f;
        /// <summary>The opening starts once the campaign has run this many ticks.</summary>
        public const int OpeningStartTicks = 5;

        private WorldBootstrap _boot;
        private readonly List<SimEvent> _events = new List<SimEvent>();
        private readonly List<Npc> _companionNpcs = new List<Npc>();
        private readonly List<Line> _said = new List<Line>();
        private float _lastTickTime;
        private bool _begun, _sceneRestored;
        private Transform _root;

        private WorldState S => _boot.Sim.State;
        private CampaignState C => _boot.Sim.State.Campaign;
        private CampaignDefinition D => _boot.Sim.State.Campaign.Definition;
        private PlayerWalker Walker => PlayerWalker.Instance;
        /// <summary>Game seconds since the world began (ticks / 10).</summary>
        public float Now => _boot != null ? S.Tick / (float)WorldSim.TicksPerSecond : 0f;
        /// <summary>Fraction of a tick since the last one (0 while the clock is paused).</summary>
        private double TickFraction => _boot.Clock.IsPaused ? 0 : Mathf.Clamp01((Time.time - _lastTickTime) * WorldSim.TicksPerSecond);

        /// <summary>The site and tower views (stone, stair, towers, pedestals, altars, keepers, crystals).</summary>
        public ExpeditionSites Sites { get; private set; }
        /// <summary>The Ash expedition's runners and the word of them.</summary>
        public ExpeditionAsh Ash { get; private set; }
        /// <summary>The journal page and the HUD line.</summary>
        public ExpeditionJournal Journal { get; private set; }
        /// <summary>The expedition's layer on the Tab map.</summary>
        public ExpeditionMapLayer MapLayer { get; private set; }

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            if (MapLayer != null) MapScreen.RemoveLayer(MapLayer);
            if (Walker != null) Walker.RemoveLock(RailLock);
            if (_duelPaused && _boot != null && _boot.Clock != null) _boot.Clock.Resume(DuelPause);
        }

        /// <summary>Called by <see cref="ExpeditionInstaller"/> once a campaign exists: takes Mira over, spawns the six, builds the views.</summary>
        public void Begin(WorldBootstrap boot)
        {
            if (_begun) return;
            _begun = true;
            _boot = boot;
            _root = transform;
            _lastTickTime = Time.time;
            var p = C.Party;
            Phase = p.Mode;

            Mira = Npc.Find(NpcIds.Mira);
            if (Mira == null)
            {
                // A world without the camp's Mira (tests): the expedition brings her.
                var at = ExpeditionSpace.World(S, p.At);
                Mira = Npc.Spawn(NpcIds.Mira, D.Role("mira")?.Name ?? "Mira", "Wizard. She leads the expedition.", "", new Color(0.45f, 0.25f, 0.70f), at, 0f);
            }
            Mira.Directed = true;
            Mira.OnTapped = OnMiraTapped;
            Mira.Greeting = "";
            if (DayNight.Instance != null) _miraLight = LightDecorator.CarryLight(Mira.transform);
            DressMira();

            SpawnCompanions();
            Sites = ExpeditionSites.Create(this, boot, _root);
            Ash = ExpeditionAsh.Create(this, boot, _root);
            Journal = ExpeditionJournal.Create(this, boot, _root);
            MapLayer = new ExpeditionMapLayer(this, boot);
            MapScreen.AddLayer(MapLayer);

            boot.Clock.Ticked += OnTicked;
            InitFromState();
            Debug.Log("[Expedition] director ready: " + D.Id + ", party " + p.Mode + " at " + p.At + ", player " + (p.PlayerPresent ? "present" : "away")
                      + ", interiors " + (ExpeditionSpace.RealInteriors(D) ? "package I5" : "stand-in"));
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            _lastTickTime = Time.time;
            if (events != null) _events.AddRange(events);
        }

        /// <summary>Events of a tick the director did not hear itself (the tick that started the campaign).</summary>
        public void Feed(IReadOnlyList<SimEvent> events) { if (events != null) _events.AddRange(events); }

        /// <summary>
        /// Stand-in interiors only: the player taps an open way down. The viewpoint is led to the party below; with
        /// nobody down there, there is nothing to be led to.
        /// </summary>
        public void GoDownStandIn(ExpeditionStair st)
        {
            if (MiraSpace.Length == 0) { Cue("Nobody is down there now. " + CampaignApi.WhereIsMira(S)); return; }
            StartRail();
        }

        /// <summary>Sends a command (the only way this package acts on the world). Returns the sim's answer.</summary>
        private CommandResult Send(Command c)
        {
            var r = _boot.Sim.Submit(c);
            if (!r.Ok) Debug.Log("[Expedition] " + c.GetType().Name + " refused: " + r.Reason + " (" + r.Message + ")");
            LastCommand = c.GetType().Name + (r.Ok ? "" : " refused " + r.Reason);
            Commands.Add(c);
            return r;
        }

        /// <summary>The last command sent, with its refusal code if refused (tests).</summary>
        public string LastCommand { get; private set; }
        /// <summary>Every command this director sent, in order (tests: commands only, and which).</summary>
        public readonly List<Command> Commands = new List<Command>();

        // =====================================================================================================
        // Frame loop
        // =====================================================================================================

        private void Update()
        {
            if (!_begun || _boot == null || _boot.Sim == null || C == null || Mira == null) return;
            ProcessEvents();
            var p = C.Party;
            Phase = p.Mode;
            bool paused = _boot.Clock.IsPaused;
            bool encounter = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            Mira.Hold = paused;
            UpdateParty(paused || encounter);
            if (encounter) return;
            UpdateCompany();
            UpdateTalk();
            UpdateCues();
        }

        private void LateUpdate()
        {
            if (!_begun || C == null) return;
            UpdateRail();
        }

        // =====================================================================================================
        // Events of the tick
        // =====================================================================================================

        private void ProcessEvents()
        {
            if (_events.Count == 0) return;
            var list = new List<SimEvent>(_events);
            _events.Clear();
            foreach (var e in list)
            {
                try { OnEvent(e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private void OnEvent(SimEvent e)
        {
            switch (e)
            {
                case PartySetOut so: OnSetOut(so); break;
                case PartyArrived ar: OnArrived(ar); break;
                case PartyModeChanged mc: OnModeChanged(mc.Mode); break;
                case QuestEventFired qe: OnQuestEvent(qe.EventId); break;
                case ChoiceOffered _: _choiceAsked = false; break;
                case ChoiceAnswered ca: OnChoiceAnswered(ca); break;
                case EncounterThreshold th: OnThreshold(th); break;
                case EncounterCommitted ec: OnCommitted(ec); break;
                case EncounterResolved er: OnResolved(er); break;
                case CompanionFell cf: OnCompanionFell(cf); break;
                case PartyRetreated pr: OnRetreated(pr); break;
                case CrystalCustodyChanged cc: OnCustody(cc); break;
                case CrystalRestored cr: OnRestored(cr); break;
                case RivalReported rr: OnRivalReported(rr); break;
                case JournalAdded ja: OnJournal(ja); break;
                case ScoutSent ss: OnScoutSent(ss); break;
                case ScoutReturned _: OnScoutReturned(); break;
                case LeadRemoved lr: if (lr.LeadId == _askLead && _askKind == AskLead) CloseAsk(); break;
                case CampaignEnded ce: OnEnded(ce); break;
            }
        }

        /// <summary>After <see cref="Begin"/> and after a load: every view from state alone.</summary>
        private void InitFromState()
        {
            var c = C; var p = c.Party;
            // The opening has been played once Mira has done or been told anything.
            bool started = p.BeganTick >= 0 || c.FiredEvents.Count > 0 || !p.PlayerPresent || p.Mode != PartyMode.Idle || S.Tick - c.StartTick > 600;
            if (!_sceneRestored) SceneStep = started ? SceneUnderWay : SceneNotStarted;
            foreach (var m in _mates)
            {
                var st = c.Companion(m.Id);
                if (st != null && !st.Alive) LayFallen(m, FellPoint(m, st), FellSpace(st));
                m.Out = c.Scout != null && c.Scout.Out && c.Scout.CompanionId == m.Id;
            }
            PlaceFromState(true);
        }
    }
}
