using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// The council in the normal game. Watches the sim: when the home village qualifies for an election it submits
    /// <c>CouncilOffer</c>; one candidate walks up and asks for help in a speech bubble (accept, decline, ask what it is).
    /// Accepting walks to the gathering and opens the encounter; declining lets the four play on their own (visible to a
    /// passer-by, resolved in the background otherwise). Keeps the gathering scene in step with the state, routes the
    /// council events to it, and leaves a candidate at the gathering so a player who left can sit down again. Never
    /// writes state. The approach waits while a pitch bubble is open so it never competes with the prince.
    /// </summary>
    public sealed class CouncilDirector : MonoBehaviour
    {
        public static CouncilDirector Instance { get; private set; }
        /// <summary>No council approach (tests, -nocouncil).</summary>
        public static bool Disabled;
        /// <summary>Game seconds after the village first qualifies before the candidate comes.</summary>
        public static float ApproachDelay = 12f;
        public const string AskerId = "council_asker";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Disabled = false; ApproachDelay = 12f; s_site = null; s_siteSeed = 0; }

        public enum Stage { Idle, Walking, Asking, Explaining, Leading, Waiting, Declined, Gathered }

        public WorldBootstrap Boot { get; private set; }
        public CouncilScene Scene { get; private set; }
        public Stage Approach { get; private set; }
        public Npc Asker { get; private set; }
        public int HomeSettlement { get; private set; }
        public int Offered { get; private set; }
        public int ApproachesStarted { get; private set; }
        public string LastSaid { get; private set; }
        public bool Qualifies { get; private set; }
        public float QualifiedSince { get; private set; } = -1f;
        public int BackgroundSteps { get; private set; }

        static CouncilSite s_site; static ulong s_siteSeed;
        int _askHandle; float _nextCheck, _nextTry, _declineAt = -1f, _leadStart;
        bool _offerPending; float _offerSubmittedAt;
        int _tickCounter;
        bool _encounterHidden;

        public static bool CommandLineDisabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-nocouncil") >= 0;

        public static void Install(WorldBootstrap boot)
        {
            var go = new GameObject("CouncilDirector");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<CouncilDirector>();
            d.Boot = boot;
            Instance = d;
            CouncilDevTool.Register();
        }

        void Start()
        {
            if (Boot == null) Boot = WorldBootstrap.Instance;
            Boot.Clock.Ticked += OnTicked;
            HomeSettlement = FindHome();
        }

        void OnDestroy()
        {
            if (Boot != null && Boot.Clock != null) Boot.Clock.Ticked -= OnTicked;
            if (Instance == this) Instance = null;
        }

        int FindHome()
        {
            var e = Boot.Sim.State.Economy;
            if (e != null) foreach (var st in e.Settlements) if (st.Faction == PlayerIds.Human && st.State == SettlementState.Active) return st.Id;
            return 0;
        }

        public CouncilSite Site
        {
            get
            {
                ulong seed = Boot.Sim.State.Seed;
                if (s_site == null || s_siteSeed != seed) { s_site = CouncilSite.Find(Boot.Sim.State); s_siteSeed = seed; Debug.Log("[Council] site: " + s_site.Why + " at " + s_site.Position); }
                return s_site;
            }
        }

        public CouncilScene EnsureScene(int settlement)
        {
            if (Scene != null && Scene.SettlementId == settlement) return Scene;
            Scene = CouncilScene.Build(transform, settlement, Site);
            return Scene;
        }

        public Vector3 GatherSpot => Scene != null ? Scene.ToWorld(new Vector3(-9f * CouncilLayout.Scale, 0f, 3f * CouncilLayout.Scale)) : Site.ToWorld(new Vector3(-9f * Site.Scale, 0, 3f * Site.Scale));

        // ------------------------------------------------------------------ events

        void OnTicked(IReadOnlyList<SimEvent> events)
        {
            _tickCounter++;
            var sc = CouncilApi.Get(Boot.Sim.State, HomeSettlement);
            foreach (var ev in events)
            {
                if (!(ev is CouncilEvent ce)) continue;
                if (ev is CouncilOffered) { _offerPending = false; Offered++; }
                if (ce.Settlement != HomeSettlement) continue;
                if (Scene != null) Scene.Handle(ce, sc);
                CouncilEncounter.Current?.OnEvent(ce);
            }
            // Rivals play on their own when the player declined (visible if nearby, quickly when far away).
            if (sc != null && sc.Status == CouncilStatus.InProgress && !sc.PlayerHelps && sc.Election != null && sc.Election.Phase != ElectionPhase.Complete
                && !(EncounterHost.Instance != null && EncounterHost.Instance.IsRunning))
            {
                bool near = Scene != null && PlayerWalker.Instance != null && (PlayerWalker.Instance.Position - Scene.Root.position).magnitude < 45f;
                if (!near || _tickCounter % 7 == 0)
                    if (Boot.Sim.Submit(new CouncilAiStep { Settlement = HomeSettlement, PlayerId = PlayerIds.Human }).Ok) BackgroundSteps++;
            }
        }

        public void EncounterEnded(CouncilEncounter enc)
        {
            ShowAsker(true);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (Boot == null || Boot.Sim == null) return;
            float now = Time.time;
            if (now < _nextCheck) { TickApproach(); return; }
            _nextCheck = now + 0.25f;
            var s = Boot.Sim.State;
            HomeSettlement = FindHome();
            var sc = CouncilApi.Get(s, HomeSettlement);
            if (sc != null && (sc.Status == CouncilStatus.InProgress || sc.Status == CouncilStatus.Decided) && (Scene == null || Scene.SettlementId != HomeSettlement)) EnsureScene(HomeSettlement);
            if (Disabled || CommandLineDisabled) return;

            if (sc == null)
            {
                Qualifies = CouncilApi.CanOffer(s, HomeSettlement);
                if (!Qualifies) { QualifiedSince = -1f; return; }
                if (QualifiedSince < 0f) QualifiedSince = Boot.Clock != null ? (float)s.Tick / WorldSim.TicksPerSecond : 0f;
                float gameSeconds = (float)s.Tick / WorldSim.TicksPerSecond;
                if (_offerPending && now - _offerSubmittedAt > 3f) _offerPending = false;
                if (!_offerPending && gameSeconds - QualifiedSince >= ApproachDelay && Gate())
                {
                    if (Boot.Sim.Submit(new CouncilOffer { Settlement = HomeSettlement, PlayerId = PlayerIds.Human }).Ok) { _offerPending = true; _offerSubmittedAt = now; }
                }
            }
            else if (sc.Status == CouncilStatus.Offered) { if (Asker == null && Gate() && now >= _nextTry) StartApproach(sc); }
            else if (sc.Status == CouncilStatus.InProgress)
            {
                if (sc.PlayerHelps && Asker == null && Approach != Stage.Leading && !(EncounterHost.Instance != null && EncounterHost.Instance.IsRunning)) SpawnGatherer(sc);
            }
            else if (sc.Status == CouncilStatus.Decided) { if (Asker != null && Approach == Stage.Gathered) { Destroy(Asker.gameObject); Asker = null; } }
        }

        /// <summary>True when it is a calm moment to approach: nothing else asking, not in an encounter, the prince's opening done.</summary>
        public bool Gate()
        {
            var host = EncounterHost.Instance;
            if (host != null && host.IsRunning) return false;
            if (Hud.MenuOpen || MapScreen.IsOpen) return false;
            var views = ViewSwitcher.Instance; if (views != null && views.CurrentView != ViewMode.FirstPerson) return false;
            var pr = PrinceDirector.Instance;
            if (pr != null)
            {
                if (pr.FirstSceneStep < PrinceDirector.SceneDone) return false;
                if (pr.CurrentPhase != PrinceDirector.Phase.Waiting && pr.CurrentPhase != PrinceDirector.Phase.Resigned) return false;
            }
            foreach (int h in SpeechBubbles.OpenHandles()) if (h != _askHandle) return false; // no pitch or other bubble is open
            var w = PlayerWalker.Instance; if (w == null || !w.CanMove) return false;
            return true;
        }

        // ------------------------------------------------------------------ the approach

        void StartApproach(SettlementCouncil sc)
        {
            var seat = sc.AskerSeat;
            var info = sc.Candidates[seat];
            var player = PlayerWalker.Instance;
            var site = Site;
            var from = site.ToWorld(new Vector3(0, 0, 8f * site.Scale));
            var tun = CouncilLayout.EmblemColor(CouncilLayout.EmblemColours[seat]);
            Asker = Npc.Spawn(AskerId, info.Name, "Candidate for " + sc.Office + " (" + info.Occupation + ")", "May I ask for your help?", Color.Lerp(tun, new Color(0.5f, 0.4f, 0.3f), 0.35f), from, site.Yaw);
            Asker.Directed = true; Asker.WalkSpeed = 8f;
            var dir = BoardSpace.DirOf(player.Yaw);
            var target = player.Position + dir * 5f;
            Asker.WalkTo(target);
            Approach = Stage.Walking; ApproachesStarted++;
        }

        void TickApproach()
        {
            if (Asker == null) return;
            var sc = CouncilApi.Get(Boot.Sim.State, HomeSettlement);
            var player = PlayerWalker.Instance;
            switch (Approach)
            {
                case Stage.Walking:
                    if (sc == null || sc.Status != CouncilStatus.Offered) { Dismiss(); return; }
                    if (!Gate()) { Asker.StopWalking(); Asker.FaceTowards(player.Position); return; } // wait: another bubble is open
                    if (!Asker.IsWalking || (Asker.transform.position - player.Position).magnitude < 6.5f)
                    {
                        Asker.StopWalking(); Asker.FaceTowards(player.Position);
                        AskFirst(sc);
                    }
                    else if (Asker.Goal.HasValue && (Asker.Goal.Value - (player.Position + BoardSpace.DirOf(player.Yaw) * 5f)).magnitude > 12f) Asker.WalkTo(player.Position + BoardSpace.DirOf(player.Yaw) * 5f);
                    break;
                case Stage.Leading:
                    // the candidate leads to the gathering; the player follows; the encounter opens when both are there
                    if (!Asker.IsWalking && (Asker.transform.position - player.Position).magnitude < 10f || Time.time - _leadStart > 40f)
                    {
                        Approach = Stage.Gathered;
                        player.StopAuto();
                        if (EncounterHost.Instance != null && !EncounterHost.Instance.IsRunning) StartEncounter(Asker);
                    }
                    break;
                case Stage.Declined:
                    if (_declineAt >= 0f && Time.time >= _declineAt) { Dismiss(); }
                    break;
                case Stage.Waiting:
                    if (Time.time >= _declineAt) Dismiss();
                    break;
            }
        }

        void AskFirst(SettlementCouncil sc)
        {
            Approach = Stage.Asking;
            var info = sc.Candidates[sc.AskerSeat];
            string text = "I am " + info.Name + ", the " + info.Occupation + ". Four of us stand for " + sc.Office + ". Will you advise me?";
            LastSaid = text;
            _askHandle = SpeechBubbles.Ask(Asker, text, new[] { "Gladly", "Not today", "What is it?" }, OnReply, 40f);
        }

        void AskAgain(SettlementCouncil sc)
        {
            Approach = Stage.Explaining;
            string text = "We argue eleven village disputes at the fire. Whoever settles three becomes " + sc.Office + ". Will you help me?";
            LastSaid = text;
            _askHandle = SpeechBubbles.Ask(Asker, text, new[] { "Gladly", "Not today" }, OnReplyExplained, 40f);
        }

        void OnReply(int i)
        {
            var sc = CouncilApi.Get(Boot.Sim.State, HomeSettlement);
            if (Asker == null || sc == null) return;
            if (i == 0) Accept(sc);
            else if (i == 1) Decline(sc);
            else if (i == 2) AskAgain(sc);
            else TimedOut();
        }

        void OnReplyExplained(int i)
        {
            var sc = CouncilApi.Get(Boot.Sim.State, HomeSettlement);
            if (Asker == null || sc == null) return;
            if (i == 0) Accept(sc); else if (i == 1) Decline(sc); else TimedOut();
        }

        void TimedOut()
        {
            // no answer: step back, try again later (the offer stays open)
            _nextTry = Time.time + 45f;
            Approach = Stage.Waiting; _declineAt = Time.time + 2f;
            Asker.WalkTo(Site.ToWorld(new Vector3(0, 0, 8f * Site.Scale)));
        }

        public bool Accept(SettlementCouncil sc)
        {
            var r = Boot.Sim.Submit(new CouncilRespond { Settlement = HomeSettlement, PlayerId = PlayerIds.Human, Help = true });
            if (!r.Ok) return false;
            LastSaid = "Then follow me to the fire.";
            SpeechBubbles.Say(Asker, LastSaid, 3f);
            EnsureScene(HomeSettlement);
            Approach = Stage.Leading; _leadStart = Time.time;
            Asker.WalkTo(GatherSpot);
            PlayerWalker.Instance.Follow(Asker);
            return true;
        }

        public bool Decline(SettlementCouncil sc)
        {
            var r = Boot.Sim.Submit(new CouncilRespond { Settlement = HomeSettlement, PlayerId = PlayerIds.Human, Help = false });
            if (!r.Ok) return false;
            LastSaid = "Then we will settle it among ourselves.";
            SpeechBubbles.Say(Asker, LastSaid, 3f);
            Approach = Stage.Declined; _declineAt = Time.time + 4f;
            Asker.WalkTo(Site.ToWorld(new Vector3(0, 0, 8f * Site.Scale)));
            return true;
        }

        void Dismiss()
        {
            if (Asker != null) Destroy(Asker.gameObject);
            Asker = null; Approach = Stage.Idle;
        }

        void StartEncounter(Npc npc)
        {
            // the candidate stays at the gathering so the player can sit down again later
            EncounterDirectory.TryStart(CouncilEncounter.Id, npc, HomeSettlement);
            MakeGatherer(npc);
            ShowAsker(false);
        }

        void SpawnGatherer(SettlementCouncil sc)
        {
            var info = sc.Candidates[sc.AskerSeat];
            var site = Site;
            var tun = CouncilLayout.EmblemColor(CouncilLayout.EmblemColours[sc.AskerSeat]);
            var npc = Npc.Spawn(AskerId, info.Name, "Candidate for " + sc.Office + " (" + info.Occupation + ")", "Shall we sit with the council?", Color.Lerp(tun, new Color(0.5f, 0.4f, 0.3f), 0.35f), GatherSpot, site.Yaw);
            Asker = npc; Approach = Stage.Gathered;
            MakeGatherer(npc);
        }

        void MakeGatherer(Npc npc)
        {
            npc.Directed = true; npc.Hold = true; npc.StopWalking();
            npc.Options.Clear(); npc.Lines.Clear();
            npc.Lines.Add("The disputes wait by the fire.");
            npc.Options.Add(DialogueOption.Encounter("Sit with the council", CouncilEncounter.Id));
            npc.Options.Add(DialogueOption.Leave("Not now"));
            npc.Greeting = "Shall we sit with the council?";
            Approach = Stage.Gathered;
        }

        void ShowAsker(bool visible)
        {
            if (Asker != null && Asker.Actor is PlaceholderActor pa) pa.SetVisible(visible);
        }

        // ------------------------------------------------------------------ the encounter id

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterEncounter()
        {
            EncounterDirectory.Register(CouncilEncounter.Id, () => new CouncilEncounter());
        }
    }
}
