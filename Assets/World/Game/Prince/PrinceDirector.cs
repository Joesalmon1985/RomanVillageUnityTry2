using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;
using World.Sim.Demons;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The prince, the sawyer and the merchant, the pitch loop and the first scene (REFACTOR_SPEC 2.C.2, 2.C.5, 6.3,
    /// 8.5, appendix B). Owner: package N. Reads state, submits only <c>DeclineProposal</c> (casts go through
    /// <see cref="SpellCaster"/> when the player presses the ground), never pauses the clock; every timer runs on game
    /// time (sim ticks), so the world and the conversation keep the same pace at any time scale.
    ///
    /// The loop: the top proposal of your people is pitched by its speaker (<c>Proposal.Speaker</c>: prince, sawyer,
    /// merchant). The speaker walks up, faces you from a few metres to one side of your view and asks, with the
    /// replies "Show me" (he leads, you follow), "Not this" (decline: the next proposal is pitched) and "Later" (he
    /// waits a minute). At the spot he stands aside, points at the ground, says why in the proposal's reason sentence
    /// and waits for the cast; he waits and calls when you lag, and comes back to fetch you when you wander off. An
    /// unanswered pitch comes back sooner and keener each time (40, 30, 20 s; urgent under an overrun warning); after
    /// three he goes quiet for 90 s and walks behind you. When he has nothing to show, the prince walks a few metres
    /// behind you. The game's end plays as a scene in bubbles.
    /// </summary>
    public sealed class PrinceDirector : MonoBehaviour
    {
        public static PrinceDirector Instance { get; private set; }

        public Npc Prince { get; private set; }
        public Npc Sawyer { get; private set; }
        public Npc Merchant { get; private set; }
        /// <summary>The builder called over in the first scene (null before).</summary>
        public Npc Builder { get; private set; }

        // ---------------- tuning (game seconds, metres) ----------------
        public static float HurrySpeed = 8f;
        public static float LeadSpeed = 7f;
        public static float TagAlongSpeed = 5f;
        public static float PitchDistance = 8f;
        public static float AskTimeout = 15f;
        public static float[] RepitchAfter = { 40f, 30f, 20f };
        public static float WarningRepitch = 15f;
        public static float ResignedSeconds = 90f, ResignedUnderWarning = 30f;
        public static float LaterSeconds = 60f;
        public static float LagDistance = 18f, ResumeDistance = 12f, FetchDistance = 50f;
        public static float CallEvery = 8f, NudgeEvery = 12f;
        public static int UnansweredBeforeResigned = 3;
        /// <summary>The first scene starts once the clock has run this many ticks (the prince walks up).</summary>
        public const int StartTick = 10;

        // ---------------- first scene steps (saved by F as extras key "scene.first") ----------------
        public const int SceneNotStarted = 0, SceneAsked = 1, SceneLeading = 2, SceneAtPlot = 3, SceneBuilding = 4, SceneFirstLoad = 5, SceneDone = 6;

        /// <summary>Step of the first scene (saved in the extras key <c>scene.first</c>).</summary>
        public int FirstSceneStep { get; private set; }

        /// <summary>What the pitching villager is doing.</summary>
        public enum Phase { Waiting, Approach, Asking, Leading, AtSpot, Celebrating, Resigned, Ending }
        public Phase CurrentPhase { get; private set; }
        /// <summary>The villager pitching now (null while waiting).</summary>
        public Npc ActiveSpeaker => _p?.Who;
        /// <summary>The proposal being pitched, led to or waited at (-1 if none).</summary>
        public int PitchedProposal => _p != null ? _p.ProposalId : -1;
        /// <summary>The open question bubble of the current pitch (0 if none).</summary>
        public int AskHandle => _ask != 0 && SpeechBubbles.IsOpen(_ask) ? _ask : 0;
        /// <summary>Where the leading villager stops (stands aside of the target).</summary>
        public Vector3 StandSpot => _p != null ? _p.Stand : Vector3.zero;
        /// <summary>True while the leading villager stopped to wait for the player.</summary>
        public bool WaitingForPlayer => _p != null && _p.Waiting;
        /// <summary>Pitches since the last answer (or cast).</summary>
        public int Unanswered { get; private set; }
        public PitchMood Mood { get; private set; }
        public int PitchCount { get; private set; }
        public int Declines { get; private set; }
        public int Fetches { get; private set; }
        public int Calls { get; private set; }
        public int ResignedCount { get; private set; }
        /// <summary>The ending scene has started.</summary>
        public bool EndingPlayed => CurrentPhase == Phase.Ending;

        /// <summary>One line said (tests, AutoPlay reports).</summary>
        public struct Line { public long Tick; public string Speaker; public string Text; public bool Ask; }
        public readonly List<Line> Said = new List<Line>();
        public event Action<Line> Spoke;

        /// <summary>A cast of your people: when it was first pitched and when it was cast (ticks; pitched -1 if never pitched).</summary>
        public struct CastRecord { public int ProposalId; public ProposalKind Kind; public string Speaker; public long PitchedTick; public long CastTick; }
        public readonly List<CastRecord> Casts = new List<CastRecord>();
        private readonly Dictionary<int, long> _pitchedAt = new Dictionary<int, long>();
        private readonly Dictionary<int, string> _speakerOf = new Dictionary<int, string>();

        // ---------------- internals ----------------

        private sealed class Pitch
        {
            public int ProposalId = -1;
            public Npc Who;
            public bool FirstScene, GoAhead, Fetching, Waiting, Explained;
            public Vector3 Target, Stand;
            public float LostSince = -1f, LastCall = -100f, LastNudge, SpotSince, CelebrateUntil;
            public Action OnArrive;
        }

        private WorldBootstrap _boot;
        private CampLayout _camp;
        private Pitch _p;
        private int _ask, _callAsk;
        private float _askedAt;
        private bool _askShown;
        private float _nextPitchAt, _laterUntil = -1f, _resignedUntil, _lastPitchEnd = -100f;
        private int _laterId = -1, _declinedId = -1, _lastPitchedId = -1;
        private Npc _resignedWho;
        private readonly Dictionary<Npc, Voice> _voices = new Dictionary<Npc, Voice>();
        private readonly Dictionary<Npc, Vector3> _homes = new Dictionary<Npc, Vector3>();
        private readonly List<SimEvent> _events = new List<SimEvent>();
        private Transform _tail;
        private bool _started, _restored;
        private int _firstAsks;
        private int _hutBuilding = -1, _hutPlot = -1, _loadCarrier = -1;
        private bool _woodSaid;
        private int _busySaid;
        private float _stepSince;
        private Vector3 _builderSpot;
        private float _nextHammer;
        private float _hudAt;

        private WorldState S => _boot.Sim.State;
        private PlayerWalker Walker => PlayerWalker.Instance;
        private Vector3 PlayerPos => Walker != null ? Walker.Position : Vector3.zero;
        /// <summary>Game seconds since the start (ticks / 10).</summary>
        public float Now => _boot != null ? S.Tick / (float)WorldSim.TicksPerSecond : 0f;

        private void Awake() { Instance = this; }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
            SpellCaster.CastAttempted -= OnCastAttempted;
        }

        /// <summary>Called by <see cref="PrinceInstaller"/>: spawns the villagers and starts listening.</summary>
        public void Begin(WorldBootstrap boot)
        {
            _boot = boot;
            var s = S;
            _camp = new CampLayout(s.Board);
            var start = WalkLattice.ToWorld(_camp.PrinceStart);
            var spawn = WalkLattice.ToWorld(_camp.Spawn);
            Prince = PrinceVillagers.SpawnPrince(start, BoardSpace.YawOf(spawn - start));
            var sawyerHome = PrinceVillagers.HomeSpot(s, _camp, 1);
            var merchantHome = PrinceVillagers.HomeSpot(s, _camp, 3);
            if (BoardSpace.Flat(sawyerHome - merchantHome) < 4f) merchantHome = PrinceVillagers.HomeSpot(s, _camp, 4);
            Sawyer = PrinceVillagers.SpawnSawyer(sawyerHome, BoardSpace.YawOf(_camp.Hearth - sawyerHome));
            Merchant = PrinceVillagers.SpawnMerchant(merchantHome, BoardSpace.YawOf(_camp.Hearth - merchantHome));
            _homes[Prince] = start; _homes[Sawyer] = sawyerHome; _homes[Merchant] = merchantHome;
            int seed = unchecked((int)s.Seed);
            _voices[Prince] = new Voice(NpcIds.Prince, seed ^ 0x51);
            _voices[Sawyer] = new Voice(NpcIds.Sawyer, seed ^ 0x52);
            _voices[Merchant] = new Voice(NpcIds.Merchant, seed ^ 0x53);
            foreach (var n in new[] { Prince, Sawyer, Merchant }) n.OnTapped = OnVillagerTapped;
            _tail = new GameObject("PrinceTagAlong").transform;
            _tail.SetParent(transform, false);
            boot.Clock.Ticked += OnTicked;
            SpellCaster.CastAttempted += OnCastAttempted;
            // A game already past the opening (a load, or the F1 tools cast it) has no first scene left.
            if (s.Factions != null && s.Factions.OpeningDone != null && s.Factions.OpeningDone[PlayerIds.Human]) FirstSceneStep = SceneDone;
        }

        /// <summary>Called by the save system (F) after a load: resumes the first scene at that step.</summary>
        public void RestoreFirstScene(int step)
        {
            FirstSceneStep = Mathf.Clamp(step, SceneNotStarted, SceneDone);
            _restored = true;
        }

        private void OnTicked(IReadOnlyList<SimEvent> events) { if (events != null) _events.AddRange(events); }

        // =====================================================================================================
        // Frame loop
        // =====================================================================================================

        private void Update()
        {
            if (_boot == null || _boot.Sim == null || Prince == null) return;
            bool hold = _boot.Clock.IsPaused;
            foreach (var n in Villagers()) if (n != null) n.Hold = hold;
            if (hold) return;
            var s = S;
            if (s.Tick < StartTick) return;
            if (!_started) { _started = true; if (_restored) ApplyRestore(); }

            ProcessEvents();
            if (CurrentPhase == Phase.Ending) { UpdateEnding(); return; }
            var o = FactionApi.Outcome(s);
            if (o.GameOver) { StartEnding(o); return; }

            Mood = MoodNow();
            UpdateCompanions();
            if (FirstSceneStep < SceneDone) UpdateFirstScene();
            else UpdateLoop();
            UpdateBuilder();
            if (Time.unscaledTime >= _hudAt) { _hudAt = Time.unscaledTime + 0.5f; Hud.SetObjective(HudLine()); }
        }

        private IEnumerable<Npc> Villagers()
        {
            yield return Prince; yield return Sawyer; yield return Merchant;
            if (Builder != null) yield return Builder;
            foreach (var n in _ashParty) yield return n;
        }

        private PitchMood MoodNow()
        {
            var w = FactionApi.Warning(S);
            if (w.Active && w.Defender == PlayerIds.Human) return PitchMood.Urgent;
            if (Unanswered <= 0) return PitchMood.Calm;
            return Unanswered == 1 ? PitchMood.Keen : PitchMood.Pressing;
        }

        // =====================================================================================================
        // Companions: who walks where when not pitching
        // =====================================================================================================

        private bool IsPitching(Npc n) => _p != null && _p.Who == n && CurrentPhase != Phase.Waiting && CurrentPhase != Phase.Resigned;

        private void UpdateCompanions()
        {
            var w = Walker;
            if (w == null) return;
            var fwd = w.transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            var right = new Vector3(fwd.z, 0, -fwd.x);
            bool resigned = CurrentPhase == Phase.Resigned;
            // Behind and a little to the left: never in the way, never filling the view.
            float back = resigned ? 8f : 7f;
            _tail.position = w.Position - fwd * back - right * 3f;
            foreach (var n in new[] { Prince, Sawyer, Merchant })
            {
                if (n == null || IsPitching(n)) continue;
                // The prince tags along once the first scene is over (during it, the scene places him); a speaker who
                // went quiet after unanswered pitches walks behind the player too.
                bool tagAlong = FirstSceneStep >= SceneDone && (n == Prince || (resigned && n == _resignedWho));
                if (n == Prince && !tagAlong) continue;
                if (tagAlong)
                {
                    float d = BoardSpace.Flat(n.transform.position - w.Position);
                    n.WalkSpeed = d > 20f ? HurrySpeed : TagAlongSpeed;
                    n.Follow(_tail, 1.5f);
                    n.PointAt(null);
                }
                else if (n != Prince)
                {
                    var home = _homes[n];
                    if (BoardSpace.Flat(n.transform.position - home) > 0.6f)
                    {
                        if (!n.IsWalking || !n.Goal.HasValue || BoardSpace.Flat(n.Goal.Value - home) > 0.6f) { n.WalkSpeed = Npc.StrollSpeed + 1f; n.WalkTo(home); }
                    }
                    else if (!n.IsWalking) n.FaceTowards(_camp.Hearth);
                    n.PointAt(null);
                }
            }
        }

        // =====================================================================================================
        // The pitch loop (after the first scene)
        // =====================================================================================================

        private void UpdateLoop()
        {
            switch (CurrentPhase)
            {
                case Phase.Approach: UpdateApproach(); return;
                case Phase.Asking: UpdateAsking(); return;
                case Phase.Leading: UpdateLeading(); return;
                case Phase.AtSpot: UpdateAtSpot(); return;
                case Phase.Celebrating:
                    if (Now >= _p.CelebrateUntil) EndPitch(Now + 1f);
                    return;
                case Phase.Resigned:
                    if (Now >= _resignedUntil || (Mood == PitchMood.Urgent && Now >= _resignedUntil - ResignedSeconds + ResignedUnderWarning))
                    {
                        CurrentPhase = Phase.Waiting; _nextPitchAt = Now; _resignedWho = null;
                    }
                    return;
            }
            // Waiting: pitch the top proposal when it is new, or again when its interval is up.
            var s = S;
            var top = FactionApi.Top(s, PlayerIds.Human);
            if (top == null) return;
            if (top.Id == _declinedId) return; // the brain has not re-planned yet
            if (top.Id == _laterId && Now < _laterUntil) return;
            bool changed = top.Id != _lastPitchedId;
            float gap = Unanswered > 0 ? 15f : 2f;
            if (Now < _nextPitchAt && !(changed && Now - _lastPitchEnd >= gap)) return;
            StartPitch(top, false);
        }

        private void StartPitch(Proposal top, bool firstScene)
        {
            _p = new Pitch { ProposalId = top.Id, Who = SpeakerFor(top), FirstScene = firstScene };
            _lastPitchedId = top.Id;
            _speakerOf[top.Id] = _p.Who.Id;
            _p.OnArrive = firstScene ? (Action)AskFirst : AskPitch;
            CurrentPhase = Phase.Approach;
        }

        /// <summary>The villager who pitches a proposal (REFACTOR_SPEC 6.3: <c>Proposal.Speaker</c>).</summary>
        public Npc SpeakerFor(Proposal p)
        {
            if (p == null) return Prince;
            switch (p.Speaker)
            {
                case Speakers.Sawyer: return Sawyer != null ? Sawyer : Prince;
                case Speakers.Merchant: return Merchant != null ? Merchant : Prince;
                default: return Prince;
            }
        }

        private void EndPitch(float nextAt)
        {
            CloseAsks();
            if (_p != null && _p.Who != null) _p.Who.PointAt(null);
            if (_p != null && !_p.GoAhead && Walker != null && _p.Who != null && ReferenceEquals(Walker.Following, _p.Who)) Walker.StopAuto();
            _p = null;
            CurrentPhase = Phase.Waiting;
            _nextPitchAt = nextAt;
            _lastPitchEnd = Now;
        }

        private void CloseAsks()
        {
            if (_ask != 0) { SpeechBubbles.Close(_ask); _ask = 0; }
            if (_callAsk != 0) { SpeechBubbles.Close(_callAsk); _callAsk = 0; }
        }

        // ---------------- approach ----------------

        private void UpdateApproach()
        {
            var who = _p.Who;
            var me = PlayerPos;
            // Before asking, follow changes of the top proposal (not for the first scene or a fetch).
            if (!_p.FirstScene && !_p.Fetching)
            {
                var top = FactionApi.Top(S, PlayerIds.Human);
                if (top == null) { EndPitch(Now); return; }
                if (top.Id != _p.ProposalId)
                {
                    _p.ProposalId = top.Id; _lastPitchedId = top.Id;
                    var sp = SpeakerFor(top);
                    _speakerOf[top.Id] = sp.Id;
                    if (sp != who) { who.StopWalking(); _p.Who = who = sp; }
                }
            }
            float d = BoardSpace.Flat(who.transform.position - me);
            if (d <= PitchDistance + 2.5f)
            {
                who.StopWalking();
                who.FaceTowards(me);
                var then = _p.OnArrive;
                _p.OnArrive = null;
                then?.Invoke();
                return;
            }
            var spot = PitchSpot(who);
            who.WalkSpeed = HurrySpeed;
            if (!who.IsWalking || !who.Goal.HasValue || BoardSpace.Flat(who.Goal.Value - spot) > 4f) who.WalkTo(spot);
        }

        /// <summary>A point a few metres from the player, 30 degrees to the side of the view nearer the villager.</summary>
        private Vector3 PitchSpot(Npc who)
        {
            var w = Walker;
            var fwd = w.transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            var a = Quaternion.Euler(0, 30f, 0) * fwd;
            var b = Quaternion.Euler(0, -30f, 0) * fwd;
            var pa = w.Position + a * PitchDistance;
            var pb = w.Position + b * PitchDistance;
            var p = BoardSpace.Flat(pa - who.transform.position) <= BoardSpace.Flat(pb - who.transform.position) ? pa : pb;
            var c = WalkLattice.ToCell(p);
            if (WalkGrid.IsBlocked(c)) { var f = WalkGrid.NearestFree(c, 3); if (f.HasValue) p = WalkLattice.ToWorld(f.Value); }
            return p;
        }

        // ---------------- asking ----------------

        private void AskPitch()
        {
            var s = S;
            var prop = FactionApi.GetProposal(s, _p.ProposalId);
            if (prop == null) { EndPitch(Now); return; }
            var v = VoiceOf(_p.Who);
            string text = _p.Fetching ? v.Pick("fetch") : v.Pitch(Mood, KindOf(prop), Args(prop));
            Ask(_p.Who, text, new[] { PrinceLines.ShowMe, PrinceLines.NotThis, PrinceLines.Later }, OnPitchReply);
        }

        private void Ask(Npc who, string text, IList<string> replies, Action<int> onReply)
        {
            CloseAsks();
            ClearQueue(who);
            _ask = SpeechBubbles.Ask(who, text, replies, onReply, 0f);
            Record(who, text, true);
            _askedAt = Now;
            _askShown = false;
            CurrentPhase = Phase.Asking;
            PitchCount++;
            if (_p != null && _p.ProposalId >= 0 && !_pitchedAt.ContainsKey(_p.ProposalId)) _pitchedAt[_p.ProposalId] = S.Tick;
            who.Actor?.PlayOneShot(_p != null && _p.Fetching ? ActorKeys.Beckon : ActorKeys.Greet);
        }

        private void UpdateAsking()
        {
            var who = _p.Who;
            who.FaceTowards(PlayerPos);
            if (_ask != 0 && SpeechBubbles.IsShowing(_ask)) _askShown = true;
            float d = BoardSpace.Flat(who.transform.position - PlayerPos);
            bool gone = _ask == 0 || !SpeechBubbles.IsOpen(_ask);
            if (gone || Now - _askedAt >= AskTimeout || d > 40f)
            {
                if (!gone) SpeechBubbles.Close(_ask);
                _ask = 0;
                OnUnanswered();
                return;
            }
            // The player walked a little away: keep within talking distance, but do not chase into their face.
            if (d > PitchDistance + 6f)
            {
                who.WalkSpeed = HurrySpeed;
                var spot = PitchSpot(who);
                if (!who.IsWalking || !who.Goal.HasValue || BoardSpace.Flat(who.Goal.Value - spot) > 4f) who.WalkTo(spot);
            }
            else if (who.IsWalking && d <= PitchDistance + 1f) who.StopWalking();
        }

        private void OnUnanswered()
        {
            Unanswered++;
            var who = _p != null ? _p.Who : Prince;
            EndPitch(Now + RepitchInterval());
            if (Unanswered % UnansweredBeforeResigned == 0)
            {
                Say(who, VoiceOf(who).Pick("resigned"));
                CurrentPhase = Phase.Resigned;
                ResignedCount++;
                _resignedWho = who;
                _resignedUntil = Now + ResignedSeconds;
            }
        }

        private float RepitchInterval()
        {
            if (MoodNow() == PitchMood.Urgent) return WarningRepitch;
            int i = Mathf.Clamp(Unanswered - 1, 0, RepitchAfter.Length - 1);
            return RepitchAfter[i];
        }

        private void OnPitchReply(int i)
        {
            _ask = 0;
            if (_p == null) return;
            if (i < 0) return; // timed out / speaker gone: UpdateAsking counts it
            Unanswered = 0;
            switch (i)
            {
                case 0: StartLead(false); break;
                case 1: Decline(); break;
                default:
                {
                    var who = _p.Who;
                    Say(who, VoiceOf(who).Pick("later"));
                    _laterId = _p.ProposalId; _laterUntil = Now + LaterSeconds;
                    EndPitch(Now + LaterSeconds);
                    break;
                }
            }
        }

        private void Decline()
        {
            var who = _p.Who;
            int id = _p.ProposalId;
            var r = _boot.Sim.Submit(new DeclineProposal { PlayerId = PlayerIds.Human, ProposalId = id });
            if (r.Ok) { Declines++; _declinedId = id; }
            Say(who, VoiceOf(who).Pick("decline"));
            EndPitch(Now + 0.5f);
        }

        // ---------------- leading ----------------

        private void StartLead(bool goAhead)
        {
            var s = S;
            var who = _p.Who;
            if (!FactionApi.ProposalTarget(s, _p.ProposalId, out double x, out double z, out _)) { EndPitch(Now); return; }
            _p.Target = new Vector3((float)x, 0, (float)z);
            _p.Target.y = PlayerWalker.GroundHeight(_p.Target);
            _p.Stand = StandFor(_p.Target, who.transform.position);
            _p.GoAhead = goAhead;
            _p.Fetching = false;
            _p.Waiting = false;
            _p.LostSince = -1f;
            who.WalkSpeed = LeadSpeed;
            who.PointAt(null);
            who.WalkTo(_p.Stand);
            CurrentPhase = Phase.Leading;
            if (_p.FirstScene) FirstSceneStep = SceneLeading;
            if (!goAhead)
            {
                Say(who, VoiceOf(who).Pick("lead"));
                if (Walker != null) Walker.Follow(who);
            }
        }

        /// <summary>
        /// Where a leading villager stops: a free cell one or two steps past the target, to the side of the way in,
        /// so the player following two cells behind ends next to the target (within cast range) and the target's
        /// ground stays clear to press.
        /// </summary>
        private static Vector3 StandFor(Vector3 target, Vector3 from)
        {
            var tc = WalkLattice.ToCell(target);
            var dir = target - from; dir.y = 0;
            if (dir.sqrMagnitude < 1e-3f) dir = Vector3.forward;
            int h = WalkLattice.HeadingOf(BoardSpace.YawOf(dir));
            var tries = new[] { WalkLattice.Dir(h + 1), WalkLattice.Dir(h - 1), WalkLattice.Dir(h) + WalkLattice.Dir(h + 1), WalkLattice.Dir(h) + WalkLattice.Dir(h - 1), WalkLattice.Dir(h + 2), WalkLattice.Dir(h - 2), WalkLattice.Dir(h) };
            foreach (var d in tries)
            {
                var c = tc + d;
                if (!WalkGrid.IsBlocked(c)) return WalkLattice.ToWorld(c);
            }
            var f = WalkGrid.NearestFree(tc, 3);
            return f.HasValue ? WalkLattice.ToWorld(f.Value) : target;
        }

        private void UpdateLeading()
        {
            var who = _p.Who;
            if (FactionApi.GetProposal(S, _p.ProposalId) == null) { Expired(); return; }
            float dWho = BoardSpace.Flat(who.transform.position - _p.Stand);
            if (dWho < 0.3f && !who.IsWalking) { ArriveAtSpot(); return; }
            if (_p.GoAhead) { if (!who.IsWalking) who.WalkTo(_p.Stand); return; }
            float d = BoardSpace.Flat(who.transform.position - PlayerPos);
            if (!_p.Waiting && d > LagDistance)
            {
                _p.Waiting = true;
                who.StopWalking();
            }
            if (_p.Waiting)
            {
                who.FaceTowards(PlayerPos);
                if (d <= ResumeDistance)
                {
                    _p.Waiting = false;
                    if (_callAsk != 0) { SpeechBubbles.Close(_callAsk); _callAsk = 0; }
                    who.WalkTo(_p.Stand);
                }
                else if (Now - _p.LastCall >= CallEvery) Call(who);
            }
            CheckLost(d);
        }

        /// <summary>The villager calls the player over (a question with "Coming", so it shows the off-screen pointer).</summary>
        private void Call(Npc who)
        {
            _p.LastCall = Now;
            Calls++;
            if (_callAsk != 0) SpeechBubbles.Close(_callAsk);
            string text = VoiceOf(who).Pick(CurrentPhase == Phase.AtSpot ? "far" : "wait");
            ClearQueue(who);
            _callAsk = SpeechBubbles.Ask(who, text, new[] { PrinceLines.Coming }, i =>
            {
                _callAsk = 0;
                if (i == 0 && Walker != null && _p != null && _p.Who != null) Walker.Follow(_p.Who);
            }, 0f);
            Record(who, text, true);
            who.Actor?.PlayOneShot(ActorKeys.Beckon);
        }

        /// <summary>The player wandered far off: after a while (or at once when very far) the villager comes back to fetch them.</summary>
        private void CheckLost(float d)
        {
            if (d <= FetchDistance) { _p.LostSince = -1f; return; }
            if (_p.LostSince < 0) _p.LostSince = Now;
            if (Now - _p.LostSince >= 8f || d > FetchDistance * 1.6f) Fetch();
        }

        private void Fetch()
        {
            Fetches++;
            CloseAsks();
            _p.Fetching = true;
            _p.Waiting = false;
            _p.LostSince = -1f;
            _p.Who.PointAt(null);
            _p.OnArrive = _p.FirstScene ? (Action)AskFirstFetch : AskPitch;
            CurrentPhase = Phase.Approach;
        }

        // ---------------- at the spot ----------------

        private void ArriveAtSpot()
        {
            var who = _p.Who;
            var prop = FactionApi.GetProposal(S, _p.ProposalId);
            if (prop == null) { Expired(); return; }
            CurrentPhase = Phase.AtSpot;
            _p.SpotSince = Now; _p.LastNudge = Now; _p.LastCall = Now - CallEvery * 0.5f;
            who.FaceTowards(PlayerPos);
            who.PointAt(_p.Target + Vector3.up * 0.2f);
            if (_callAsk != 0) { SpeechBubbles.Close(_callAsk); _callAsk = 0; }
            if (_p.FirstScene) FirstSceneStep = SceneAtPlot;
            if (!_p.Explained)
            {
                _p.Explained = true;
                Say(who, prop.Reason);
                Say(who, _p.FirstScene ? VoiceOf(who).Pick("first.press") : VoiceOf(who).Pick("press"), -1f, true);
            }
        }

        private void UpdateAtSpot()
        {
            var who = _p.Who;
            if (FactionApi.GetProposal(S, _p.ProposalId) == null) { Expired(); return; }
            who.FaceTowards(PlayerPos);
            who.PointAt(_p.Target + Vector3.up * 0.2f);
            float dTarget = BoardSpace.Flat(_p.Target - PlayerPos);
            float dWho = BoardSpace.Flat(who.transform.position - PlayerPos);
            bool inRange = SpellCaster.InRange(_p.ProposalId);
            if (inRange)
            {
                if (_callAsk != 0) { SpeechBubbles.Close(_callAsk); _callAsk = 0; }
                if (Now - _p.LastNudge >= NudgeEvery) { _p.LastNudge = Now; Say(who, VoiceOf(who).Pick("nudge")); }
            }
            else if (dTarget > 25f && Now - _p.LastCall >= CallEvery) Call(who);
            CheckLost(Mathf.Min(dWho, dTarget));
        }

        private void Expired()
        {
            var who = _p.Who;
            Say(who, VoiceOf(who).Pick("expired"));
            EndPitch(Now + 1f);
        }

        // ---------------- reactions to casts ----------------

        private void OnCastAttempted(int proposalId, CommandResult r)
        {
            if (r.Ok || Prince == null || CurrentPhase == Phase.Ending) return;
            if (r.Reason == SpellCaster.NoProposal) return;
            var who = _p != null && _p.Who != null ? _p.Who : Prince;
            if (r.Reason == SpellCaster.TooFar) Say(who, VoiceOf(who).Pick("toofar"));
            else Say(who, VoiceOf(who).Pick("refused"));
        }

        private void OnSpellCast(SpellCast e)
        {
            if (e.Faction != PlayerIds.Human) return;
            Casts.Add(new CastRecord
            {
                ProposalId = e.ProposalId, Kind = e.Kind,
                Speaker = _speakerOf.TryGetValue(e.ProposalId, out var sp) ? sp : "",
                PitchedTick = _pitchedAt.TryGetValue(e.ProposalId, out var t) ? t : -1, CastTick = e.Tick,
            });
            Unanswered = 0;
            bool opening = e.Kind == ProposalKind.Building && e.BuildingType == (int)BuildingType.WoodcutterHut && FirstSceneStep < SceneBuilding;
            if (opening) { FirstSceneCast(e); return; }
            var who = _p != null && _p.ProposalId == e.ProposalId ? _p.Who : Prince;
            if (_p != null && _p.ProposalId == e.ProposalId)
            {
                CloseAsks();
                who.PointAt(null);
                who.FaceTowards(PlayerPos);
                who.Actor?.PlayOneShot(ActorKeys.Cheer);
                Say(who, VoiceOf(who).Pick("cast"));
                _p.CelebrateUntil = Now + 3f;
                CurrentPhase = Phase.Celebrating;
                if (Walker != null && ReferenceEquals(Walker.Following, who)) Walker.StopAuto();
            }
            else if (BoardSpace.Flat(Prince.transform.position - PlayerPos) < 40f) Say(Prince, VoiceOf(Prince).Pick("cast"));
            if (_p == null) _nextPitchAt = Now + 2f;
        }

        // =====================================================================================================
        // The first scene (REFACTOR_SPEC 2.C.5)
        // =====================================================================================================

        private Proposal OpeningProposal()
        {
            foreach (var p in FactionApi.Proposals(S, PlayerIds.Human))
                if (p.Kind == ProposalKind.Building && p.BuildingType == (int)BuildingType.WoodcutterHut) return p;
            return null;
        }

        private void UpdateFirstScene()
        {
            var s = S;
            if (FirstSceneStep <= SceneAtPlot)
            {
                if (s.Factions.OpeningDone != null && s.Factions.OpeningDone[PlayerIds.Human] && _p == null) { FirstSceneStep = SceneDone; return; }
                switch (CurrentPhase)
                {
                    case Phase.Approach: UpdateApproach(); return;
                    case Phase.Asking: UpdateAsking(); return;
                    case Phase.Leading: UpdateLeading(); return;
                    case Phase.AtSpot: UpdateAtSpot(); return;
                    case Phase.Resigned:
                        if (Now >= _resignedUntil) { CurrentPhase = Phase.Waiting; _nextPitchAt = Now; }
                        else TagAlongPrince(8f);
                        return;
                }
                if (Now < _nextPitchAt) { TagAlongPrince(7f); return; }
                var open = OpeningProposal();
                if (open == null) return; // the brain has not planned yet
                StartPitch(open, true);
                _p.Who = Prince;
                return;
            }
            if (FirstSceneStep == SceneBuilding)
            {
                if (CurrentPhase == Phase.Celebrating && Now >= _p.CelebrateUntil) { _p = null; CurrentPhase = Phase.Waiting; }
                // The prince stays by the site with the player.
                if (_hutBuilding >= 0 && _hutBuilding < s.Economy.Buildings.Count)
                {
                    var b = s.Economy.Buildings[_hutBuilding];
                    if (b.State == BuildingState.Active)
                    {
                        FirstSceneStep = SceneFirstLoad; _stepSince = Now;
                        Say(Prince, VoiceOf(Prince).Pick("first.done"));
                        Prince.PointAt(PlotCentre(b.Plot) + Vector3.up);
                        return;
                    }
                    if (b.State == BuildingState.Cancelled) { FirstSceneStep = SceneDone; return; }
                }
                StayNearSite();
                if (Now - _stepSince > 240f) FirstSceneStep = SceneDone; // safety: never hold the loop forever
                return;
            }
            if (FirstSceneStep == SceneFirstLoad)
            {
                StayNearSite();
                if (_loadCarrier >= 0)
                {
                    var t = CarrierTransform(_loadCarrier);
                    if (t != null) Prince.PointAt(t.position + Vector3.up);
                }
                if (_loadCarrier < 0 && Now - _stepSince > 30f + 60f * _busySaid && _busySaid < 2)
                { _busySaid++; Say(Prince, VoiceOf(Prince).Pick("first.busy")); }
                if (_ask != 0 && !SpeechBubbles.IsOpen(_ask)) _ask = 0;
                if (_ask != 0 && Now - _askedAt > 25f) { SpeechBubbles.Close(_ask); _ask = 0; FinishFirstScene(); return; }
                if (Now - _stepSince > 240f) { FinishFirstScene(); }
            }
        }

        private void TagAlongPrince(float back)
        {
            Prince.WalkSpeed = BoardSpace.Flat(Prince.transform.position - PlayerPos) > 20f ? HurrySpeed : TagAlongSpeed;
            Prince.Follow(_tail, 1.5f);
        }

        private void StayNearSite()
        {
            if (_p != null) return;
            float d = BoardSpace.Flat(Prince.transform.position - PlayerPos);
            if (d > 14f) { Prince.WalkSpeed = d > 20f ? HurrySpeed : TagAlongSpeed; Prince.Follow(Walker.transform, 9f); }
            else if (!Prince.IsWalking) Prince.FaceTowards(PlayerPos);
        }

        private void AskFirst()
        {
            Prince.FaceTowards(PlayerPos);
            string text = _firstAsks == 0 ? PrinceLines.FirstLine : VoiceOf(Prince).Pick("first.again");
            _firstAsks++;
            Ask(Prince, text, new[] { PrinceLines.ReplyLead, PrinceLines.ReplyWhat, PrinceLines.ReplyCatchUp }, OnFirstReply);
            if (FirstSceneStep < SceneAsked) FirstSceneStep = SceneAsked;
        }

        private void AskFirstFetch()
        {
            string text = VoiceOf(Prince).Pick("fetch");
            Ask(Prince, text, new[] { PrinceLines.ReplyLead, PrinceLines.ReplyCatchUp }, i => OnFirstTwoReply(i));
        }

        private void OnFirstReply(int i)
        {
            _ask = 0;
            if (_p == null || i < 0) return;
            Unanswered = 0;
            if (i == 0) { StartLead(false); return; }
            if (i == 2) { StartLead(true); return; }
            // "What's it for?": the reason, then the two other replies.
            var open = FactionApi.GetProposal(S, _p.ProposalId);
            string reason = open != null ? open.Reason : "A woodcutter's hut brings in twice the wood of the log pile.";
            Ask(Prince, reason, new[] { PrinceLines.ReplyLead, PrinceLines.ReplyCatchUp }, OnFirstTwoReply);
            PitchCount--; // the same pitch, answered with a question
        }

        private void OnFirstTwoReply(int i)
        {
            _ask = 0;
            if (_p == null || i < 0) return;
            Unanswered = 0;
            StartLead(i == 1);
        }

        private void FirstSceneCast(SpellCast e)
        {
            CloseAsks();
            if (Walker != null && ReferenceEquals(Walker.Following, Prince)) Walker.StopAuto();
            FirstSceneStep = SceneBuilding;
            _stepSince = Now;
            _hutPlot = e.Plot;
            _hutBuilding = -1;
            foreach (var b in S.Economy.Buildings)
                if (b.Plot == e.Plot && b.Faction == PlayerIds.Human && b.Type == BuildingType.WoodcutterHut && b.State != BuildingState.Cancelled) _hutBuilding = b.Id;
            Prince.PointAt(null);
            Prince.Actor?.PlayOneShot(ActorKeys.Beckon);
            Say(Prince, VoiceOf(Prince).Pick("first.cast"));
            if (_p == null) _p = new Pitch { Who = Prince, ProposalId = e.ProposalId, FirstScene = true };
            _p.CelebrateUntil = Now + 3f;
            CurrentPhase = Phase.Celebrating;
            CallBuilder();
        }

        private void FinishFirstScene()
        {
            FirstSceneStep = SceneDone;
            Prince.PointAt(null);
            _p = null;
            CurrentPhase = Phase.Waiting;
            _nextPitchAt = Now + 3f;
        }

        // ---------------- the builder ----------------

        private void CallBuilder()
        {
            if (_hutPlot < 0) return;
            var t = S.Board.Topology;
            var plot = PlotCentre(_hutPlot);
            var corner = BoardSpace.Vertex(t, PlotLayout.CornerOf(_hutPlot));
            // Work spot: 3.5 m from the plot centre on the camp's side.
            var toCorner = corner - plot; toCorner.y = 0;
            _builderSpot = plot + toCorner.normalized * 3.5f;
            if (Builder == null)
            {
                var c = WalkLattice.ToCell(corner);
                var from = WalkGrid.NearestFree(c + WalkLattice.Dir(WalkLattice.HeadingOf(BoardSpace.YawOf(plot - corner))), 2);
                var at = from.HasValue ? WalkLattice.ToWorld(from.Value) : corner;
                Builder = PrinceVillagers.SpawnBuilder(at, BoardSpace.YawOf(plot - at));
                _voices[Builder] = new Voice(NpcIds.Builder, unchecked((int)S.Seed) ^ 0x54);
                Builder.Doing = BuilderDoing;
            }
            Builder.WalkSpeed = 4.5f;
            Builder.WalkTo(_builderSpot);
            Say(Builder, VoiceOf(Builder).Pick("called"));
            _builderArrived = false;
        }

        private bool _builderArrived;

        private void UpdateBuilder()
        {
            if (Builder == null) return;
            var s = S;
            Building b = _hutBuilding >= 0 && _hutBuilding < s.Economy.Buildings.Count ? s.Economy.Buildings[_hutBuilding] : null;
            bool working = b != null && (b.State == BuildingState.Site || b.State == BuildingState.Constructing);
            if (working)
            {
                if (!Builder.IsWalking && BoardSpace.Flat(Builder.transform.position - _builderSpot) < 0.5f)
                {
                    if (!_builderArrived) { _builderArrived = true; Say(Builder, VoiceOf(Builder).Pick("arrive")); }
                    Builder.FaceTowards(PlotCentre(b.Plot));
                    if (Now >= _nextHammer && b.State == BuildingState.Constructing) { _nextHammer = Now + 1f; Builder.Actor?.PlayOneShot(ActorKeys.Hammer); }
                }
            }
            else
            {
                var rest = _homes.TryGetValue(Builder, out var h) ? h : Vector3.zero;
                if (!_homes.ContainsKey(Builder)) { rest = PrinceVillagers.HomeSpot(s, _camp, 5); _homes[Builder] = rest; }
                if (BoardSpace.Flat(Builder.transform.position - rest) > 0.6f && (!Builder.IsWalking || !Builder.Goal.HasValue || BoardSpace.Flat(Builder.Goal.Value - rest) > 0.6f))
                { Builder.WalkSpeed = Npc.StrollSpeed; Builder.WalkTo(rest); }
            }
        }

        private string BuilderDoing()
        {
            var s = S;
            if (_hutBuilding >= 0 && _hutBuilding < s.Economy.Buildings.Count)
            {
                var b = s.Economy.Buildings[_hutBuilding];
                if (b.State == BuildingState.Site || b.State == BuildingState.Constructing)
                {
                    string w = EconomyApi.WaitingSentence(s, SiteKind.Building, b.Id);
                    return "I'm putting up the " + EconomyApi.Def(b.Type).Name.ToLowerInvariant() + ". " + (w ?? "It's going up now.");
                }
            }
            return VoiceOf(Builder).Pick("idle");
        }

        // =====================================================================================================
        // Events
        // =====================================================================================================

        private void ProcessEvents()
        {
            if (_events.Count == 0) return;
            var list = new List<SimEvent>(_events);
            _events.Clear();
            var s = S;
            foreach (var ev in list)
            {
                switch (ev)
                {
                    case SpellCast sc: OnSpellCast(sc); break;
                    case ProposalRemoved pr:
                        if (pr.Faction == PlayerIds.Human && pr.Why == "expired" && _p != null && _p.ProposalId == pr.ProposalId && !_p.FirstScene
                            && (CurrentPhase == Phase.Leading || CurrentPhase == Phase.AtSpot || CurrentPhase == Phase.Asking)) Expired();
                        if (pr.ProposalId == _declinedId && pr.Why != "declined") _declinedId = -1;
                        break;
                    case ProposalsReordered ro:
                        if (ro.Faction == PlayerIds.Human && _declinedId >= 0 && ro.TopProposalId != _declinedId) _declinedId = -1;
                        break;
                    case SiteCompleted sc2:
                        if (sc2.Faction == PlayerIds.Human && sc2.Kind == SiteKind.Building && FirstSceneStep >= SceneDone && sc2.Id < s.Economy.Buildings.Count && Near(Prince, 60f) && !IsPitching(Prince))
                        {
                            var name = EconomyApi.Def(s.Economy.Buildings[sc2.Id].Type).Name;
                            Say(Prince, VoiceOf(Prince).Pick("done", Args1("name", name)));
                        }
                        break;
                    case WareDelivered wd:
                        if (FirstSceneStep == SceneBuilding && !_woodSaid && wd.ToKind == PlaceKind.Building && wd.ToId == _hutBuilding)
                        { _woodSaid = true; Say(Prince, VoiceOf(Prince).Pick("first.wood")); }
                        if (FirstSceneStep == SceneFirstLoad && _loadCarrier >= 0 && wd.CarrierId == _loadCarrier && wd.ToKind == PlaceKind.Storehouse)
                        {
                            Say(Prince, VoiceOf(Prince).Pick("first.home"));
                            if (Walker != null && Walker.Following != null) Walker.StopAuto();
                            FinishFirstScene();
                        }
                        break;
                    case WarePickedUp wp:
                        if (FirstSceneStep == SceneFirstLoad && _loadCarrier < 0 && _hutBuilding >= 0 && FromHut(wp.JobId)) FirstLoad(wp.CarrierId);
                        break;
                    case OverrunWarningStarted ws:
                    {
                        var w = FactionApi.Warning(s);
                        string secs = ((w.TicksLeft + 9) / 10).ToString();
                        if (ws.Defender == PlayerIds.Human)
                        {
                            Say(Prince, VoiceOf(Prince).Pick("warn.defend", Args1("secs", secs)), 7f);
                            if (CurrentPhase == Phase.Waiting || CurrentPhase == Phase.Resigned) { CurrentPhase = Phase.Waiting; _nextPitchAt = Now + 2f; }
                        }
                        else Say(Prince, VoiceOf(Prince).Pick("warn.attack", Args1("secs", secs)), 7f);
                        break;
                    }
                    case OverrunWarningCancelled wc:
                        Say(Prince, VoiceOf(Prince).Pick(wc.Defender == PlayerIds.Human ? "warn.cancel" : "warn.lost"));
                        break;
                    case DemonPlaced dp:
                        if (DemonFaction(dp.SiteKind, dp.SiteId) == PlayerIds.Human && Near(Prince, 80f))
                            Say(Prince, VoiceOf(Prince).Pick("demon", Args1("site", DemonApi.SiteName(s, dp.SiteKind, dp.SiteId))));
                        break;
                }
            }
        }

        private bool FromHut(int jobId)
        {
            foreach (var j in S.Economy.Jobs)
                if (j.Id == jobId) return j.FromKind == PlaceKind.Building && j.FromId == _hutBuilding;
            return false;
        }

        private void FirstLoad(int carrierId)
        {
            _loadCarrier = carrierId;
            var t = CarrierTransform(carrierId);
            if (t != null) Prince.PointAt(t.position + Vector3.up);
            Prince.FaceTowards(PlayerPos);
            if (_p == null) _p = new Pitch { Who = Prince, FirstScene = true };
            Ask(Prince, VoiceOf(Prince).Pick("first.load"), new[] { PrinceLines.Follow, PrinceLines.NotNow }, i =>
            {
                _ask = 0;
                if (i == 0)
                {
                    var f = CarrierFollowable(carrierId);
                    if (f != null && Walker != null) Walker.Follow(f);
                    else FinishFirstScene();
                }
                else if (i == 1) { Say(Prince, VoiceOf(Prince).Pick("first.notnow")); FinishFirstScene(); }
            });
            CurrentPhase = Phase.Waiting; // the question is not a pitch: no unanswered count
            _p = null;
        }

        private int DemonFaction(DemonSiteKind kind, int id)
        {
            foreach (var d in DemonApi.DemonSites(S)) if (d.Kind == kind && d.Id == id) return d.Faction;
            return -1;
        }

        private bool Near(Npc n, float m) => n != null && BoardSpace.Flat(n.transform.position - PlayerPos) <= m;

        // =====================================================================================================
        // Endings (REFACTOR_SPEC 2.C.3): a scene in bubbles
        // =====================================================================================================

        private readonly List<Npc> _ashParty = new List<Npc>();
        private readonly List<(float at, Npc who, string text, float secs)> _script = new List<(float, Npc, string, float)>();
        private float _endStart;
        /// <summary>The outcome the ending scene plays (None before the end).</summary>
        public GameOutcome EndingOutcome { get; private set; }

        private void StartEnding(OutcomeView o)
        {
            CloseAsks();
            if (_p != null && _p.Who != null) _p.Who.PointAt(null);
            _p = null;
            CurrentPhase = Phase.Ending;
            EndingOutcome = o.Outcome;
            if (Walker != null) Walker.StopAuto();
            foreach (var n in Villagers()) if (n != null) ClearQueue(n);
            var me = PlayerPos;
            var fwd = Walker != null ? Walker.transform.forward : Vector3.forward; fwd.y = 0; fwd.Normalize();
            var right = new Vector3(fwd.z, 0, -fwd.x);
            // Everyone who speaks stands in view.
            Place(Prince, me + fwd * 7f - right * 3f, me);
            _endStart = Time.unscaledTime;
            _script.Clear();
            var v = VoiceOf(Prince);
            string kind = o.Reason == OutcomeReasons.Overrun
                ? (o.Outcome == GameOutcome.Victory ? "end.win" : "end.loss")
                : (o.Outcome == GameOutcome.Victory ? "end.timewin" : o.Outcome == GameOutcome.Defeat ? "end.timeloss" : "end.draw");
            var lines = PrinceLines.Bank(NpcIds.Prince, kind);
            if (o.Outcome == GameOutcome.Defeat && o.Reason == OutcomeReasons.Overrun)
            {
                // The Ash Clan walks up: their war leader speaks first.
                var chief = PrinceVillagers.SpawnAsh(NpcIds.RivalChief, "Ash Clan war leader", true, me + fwd * 30f + right * 4f, BoardSpace.YawOf(-fwd));
                _ashParty.Add(chief);
                for (int i = 0; i < 2; i++)
                    _ashParty.Add(PrinceVillagers.SpawnAsh("ash_warrior" + i, "Ash Clan warrior", false, me + fwd * 33f + right * (i == 0 ? 8f : 0f), BoardSpace.YawOf(-fwd)));
                chief.WalkSpeed = 4f; chief.WalkTo(me + fwd * 9f + right * 4f);
                _ashParty[1].WalkSpeed = 4f; _ashParty[1].WalkTo(me + fwd * 11f + right * 7f);
                _ashParty[2].WalkSpeed = 4f; _ashParty[2].WalkTo(me + fwd * 11f + right * 1.5f);
                var rv = PrinceLines.Bank("rival", "end.loss");
                _script.Add((0.5f, Prince, lines[0], 6f));
                _script.Add((5.5f, chief, rv[0], 6f));
                _script.Add((11f, Prince, lines[1], 6f));
                _script.Add((16.5f, chief, rv[1], 6f));
                _script.Add((22f, Prince, lines[2], 8f));
            }
            else
            {
                if (o.Outcome == GameOutcome.Victory)
                {
                    Place(Sawyer, me + fwd * 9f + right * 3f, me);
                    Place(Merchant, me + fwd * 10f + right * 0.5f, me);
                }
                for (int i = 0; i < lines.Length; i++) _script.Add((0.5f + i * 5.5f, Prince, lines[i], 6f));
                if (o.Outcome == GameOutcome.Victory && o.Reason == OutcomeReasons.Overrun)
                {
                    _script.Add((3f, Sawyer, PrinceLines.Bank(NpcIds.Sawyer, "end.win")[0], 5f));
                    _script.Add((8.5f, Merchant, PrinceLines.Bank(NpcIds.Merchant, "end.win")[0], 5f));
                }
            }
            // The plain sentence of what happened, hanging in front of the player for good.
            _script.Add((_script.Count > 0 ? _script[_script.Count - 1].at + 6.5f : 1f, null, o.Sentence, 100000f));
            Record(null, "[ending] " + o.Outcome + " " + o.Reason, false);
        }

        private void Place(Npc n, Vector3 at, Vector3 face)
        {
            if (n == null) return;
            var c = WalkLattice.ToCell(at);
            if (WalkGrid.IsBlocked(c)) { var f = WalkGrid.NearestFree(c, 3); if (f.HasValue) at = WalkLattice.ToWorld(f.Value); }
            if (BoardSpace.Flat(n.transform.position - at) > 40f)
            {
                at.y = PlayerWalker.GroundHeight(at);
                n.StopWalking();
                n.PlaceAt(at, BoardSpace.YawOf(face - at));
            }
            else { n.WalkSpeed = HurrySpeed; n.WalkTo(at); }
        }

        private int _scriptDone;

        private void UpdateEnding()
        {
            float t = Time.unscaledTime - _endStart;
            var me = PlayerPos;
            foreach (var n in Villagers()) if (n != null && !n.IsWalking) { n.FaceTowards(me); }
            while (_scriptDone < _script.Count && t >= _script[_scriptDone].at / Mathf.Max(1f, Time.timeScale))
            {
                var e = _script[_scriptDone++];
                if (e.who == null) { SpeechBubbles.Say(null, e.text, e.secs); Record(null, e.text, false); }
                else
                {
                    Say(e.who, e.text, e.secs);
                    var key = EndingOutcome == GameOutcome.Victory ? ActorKeys.Cheer : EndingOutcome == GameOutcome.Defeat && _ashParty.Contains(e.who) ? ActorKeys.Greet : ActorKeys.Surprise;
                    e.who.Actor?.PlayOneShot(key);
                }
            }
        }

        /// <summary>True once every line of the ending scene has been said.</summary>
        public bool EndingFinished => CurrentPhase == Phase.Ending && _scriptDone >= _script.Count;

        // =====================================================================================================
        // Talking
        // =====================================================================================================

        private Voice VoiceOf(Npc n)
        {
            if (n != null && _voices.TryGetValue(n, out var v)) return v;
            return _voices[Prince];
        }

        private static Dictionary<string, string> Args1(string k, string v) => new Dictionary<string, string> { [k] = v };

        /// <summary>
        /// Says a line in the villager's bubble. By default it replaces what the villager was saying (shown or queued,
        /// except an open question), so a reaction is never stuck behind stale lines; <paramref name="queue"/> keeps
        /// it behind them (the explanation at the spot is two lines).
        /// </summary>
        private void Say(Npc who, string text, float seconds = -1f, bool queue = false)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            var v = VoiceOf(who);
            if (text == v.Last) return; // never the same sentence twice in a row
            if (!queue)
            {
                var drop = new List<int>();
                foreach (var b in SpeechBubbles.All)
                    if (ReferenceEquals(b.Who, who) && b.Handle != _ask && b.Handle != _callAsk) drop.Add(b.Handle);
                foreach (int h in drop) SpeechBubbles.Close(h);
            }
            if (seconds <= 0f) seconds = Mathf.Clamp(2.2f + text.Length * 0.055f, 3f, 7.5f);
            SpeechBubbles.Say(who, text, seconds / Mathf.Max(1f, Time.timeScale));
            Record(who, text, false);
        }

        private void Record(Npc who, string text, bool ask)
        {
            if (who != null && _voices.TryGetValue(who, out var v)) v.Said(text);
            var line = new Line { Tick = S.Tick, Speaker = who != null ? who.Id : "", Text = text, Ask = ask };
            Said.Add(line);
            Spoke?.Invoke(line);
        }

        /// <summary>Drops a villager's queued (not yet shown) bubbles so a new line does not wait behind stale ones.</summary>
        private static void ClearQueue(Npc who)
        {
            var drop = new List<int>();
            foreach (var b in SpeechBubbles.All)
                if (ReferenceEquals(b.Who, who) && !SpeechBubbles.IsShown(b)) drop.Add(b.Handle);
            foreach (int h in drop) SpeechBubbles.Close(h);
        }

        private void OnVillagerTapped(Npc n)
        {
            if (CurrentPhase == Phase.Ending) return;
            if (_p != null && _p.Who == n && _ask != 0) { SpeechBubbles.Expand(_ask); return; }
            if (_p != null && _p.Who == n && _callAsk != 0) { SpeechBubbles.Expand(_callAsk); return; }
            if (_p == null && FirstSceneStep >= SceneDone && CurrentPhase == Phase.Waiting)
            {
                var top = FactionApi.Top(S, PlayerIds.Human);
                if (top != null && SpeakerFor(top) == n && top.Id != _declinedId) { StartPitch(top, false); return; }
            }
            if (_p != null && _p.Who == n && CurrentPhase == Phase.AtSpot)
            {
                var prop = FactionApi.GetProposal(S, _p.ProposalId);
                if (prop != null) { Say(n, prop.Reason); return; }
            }
            Say(n, VoiceOf(n).Pick("idle"));
        }

        // =====================================================================================================
        // Words for proposals
        // =====================================================================================================

        /// <summary>The bank kind of a proposal: hut, collector, sawmill, workshop, lodge, road, camp, city, banish.</summary>
        public static string KindOf(Proposal p)
        {
            switch (p.Kind)
            {
                case ProposalKind.Road: return "road";
                case ProposalKind.Settlement: return "camp";
                case ProposalKind.City: return "city";
                case ProposalKind.Banish: return "banish";
            }
            var t = (BuildingType)p.BuildingType;
            if (t == BuildingType.WoodcutterHut) return "hut";
            if (t == BuildingType.Sawmill) return "sawmill";
            if (t == BuildingType.WarriorLodge) return "lodge";
            var def = BuildingDefs.Get(t);
            return def != null && def.Class == BuildingClass.ImprovedCollector ? "collector" : "workshop";
        }

        private Dictionary<string, string> Args(Proposal p)
        {
            var s = S;
            var a = new Dictionary<string, string>();
            string title = FactionApi.ProposalTitle(s, p.Id);
            switch (p.Kind)
            {
                case ProposalKind.Building:
                {
                    var def = BuildingDefs.Get((BuildingType)p.BuildingType);
                    string name = def != null ? def.Name.ToLowerInvariant() : "building";
                    a["thing"] = "a " + name;
                    a["name"] = name;
                    string camp = p.Settlement >= 0 ? EconomyApi.SettlementName(s, p.Settlement) : "the camp";
                    string where = "at " + camp;
                    if (def != null && def.IsCollector && p.Plot >= 0)
                        where = "by the " + TerrainInfo.DisplayName(EconomyApi.Plot(s, p.Plot).Terrain) + " " + where;
                    a["where"] = where;
                    a["camp"] = camp;
                    break;
                }
                case ProposalKind.Road:
                    a["thing"] = "a " + LowerFirst(title);
                    a["where"] = "";
                    break;
                case ProposalKind.Settlement:
                    a["thing"] = "a " + LowerFirst(title);
                    a["where"] = "by the " + EconomyApi.CornerTerrains(s, p.Corner);
                    break;
                case ProposalKind.City:
                {
                    var st = EconomyApi.SettlementAt(s, p.Corner);
                    a["camp"] = st != null ? EconomyApi.SettlementName(s, st.Id) : "the camp";
                    break;
                }
                case ProposalKind.Banish:
                    a["site"] = DemonApi.SiteName(s, p.DemonSiteKind, p.DemonSiteId);
                    break;
            }
            return a;
        }

        private static string LowerFirst(string t) => string.IsNullOrEmpty(t) ? t : char.ToLowerInvariant(t[0]) + t.Substring(1);

        private static Vector3 PlotCentre(int plot)
        {
            var t = WorldBootstrap.Instance.Sim.State.Board.Topology;
            var p = new Vector3((float)PlotLayout.PlotX(t, plot), 0, (float)PlotLayout.PlotZ(t, plot));
            p.y = PlayerWalker.GroundHeight(p);
            return p;
        }

        // ---------------- carriers (V's views, else the N placeholder) ----------------

        /// <summary>The view of a carrier to follow: package V's <see cref="LandViews.CarrierView"/>, else N's placeholder.</summary>
        public static IFollowable CarrierFollowable(int carrierId)
        {
            var t = LandViews.Instance != null ? LandViews.Instance.CarrierView(carrierId) : null;
            if (t != null) { var f = t.GetComponentInParent<IFollowable>(); if (f != null) return f; }
            return NPlaceholderViews.Instance != null ? NPlaceholderViews.Instance.Carrier(carrierId) : null;
        }

        public static Transform CarrierTransform(int carrierId)
        {
            var f = CarrierFollowable(carrierId);
            return f != null ? f.FollowTransform : null;
        }

        /// <summary>The ground mark of a proposal: V's <see cref="LandViews.ProposalMarker"/>, else N's placeholder.</summary>
        public static Transform ProposalMark(int proposalId)
        {
            var t = LandViews.Instance != null ? LandViews.Instance.ProposalMarker(proposalId) : null;
            if (t != null) return t;
            return NPlaceholderViews.Instance != null ? NPlaceholderViews.Instance.ProposalMark(proposalId) : null;
        }

        // ---------------- HUD (one line, H) ----------------

        /// <summary>"What the prince wants" for the HUD strip.</summary>
        public string HudLine()
        {
            var s = S;
            var o = FactionApi.Outcome(s);
            if (o.GameOver) return o.Sentence;
            Proposal p = _p != null ? FactionApi.GetProposal(s, _p.ProposalId) : null;
            if (p == null) p = FactionApi.Top(s, PlayerIds.Human);
            if (p == null) return Prince.DisplayName + ": nothing to build right now.";
            var who = SpeakerFor(p);
            string where = "";
            if (FactionApi.ProposalTarget(s, p.Id, out double x, out double z, out _))
                where = " (" + Mathf.RoundToInt(BoardSpace.Flat(new Vector3((float)x, 0, (float)z) - PlayerPos)) + " m)";
            string verb = CurrentPhase == Phase.Leading && _p != null && _p.Who == who ? " is leading you to: "
                : CurrentPhase == Phase.AtSpot && _p != null && _p.Who == who ? " waits for your spell: " : " wants: ";
            return who.DisplayName + verb + FactionApi.ProposalTitle(s, p.Id) + where;
        }

        // ---------------- load ----------------

        private void ApplyRestore()
        {
            var s = S;
            if (FirstSceneStep >= SceneBuilding && FirstSceneStep < SceneDone)
            {
                // Find the hut; if it stands already, wait for its first load; if not, call the builder again.
                foreach (var b in s.Economy.Buildings)
                    if (b.Faction == PlayerIds.Human && b.Type == BuildingType.WoodcutterHut && b.State != BuildingState.Cancelled && b.State != BuildingState.Retired) { _hutBuilding = b.Id; _hutPlot = b.Plot; }
                if (_hutBuilding < 0) { FirstSceneStep = SceneDone; return; }
                _stepSince = Now;
                var hb = s.Economy.Buildings[_hutBuilding];
                if (hb.State == BuildingState.Active) FirstSceneStep = SceneFirstLoad;
                else { FirstSceneStep = SceneBuilding; CallBuilder(); }
                Place(Prince, PlotCentre(_hutPlot) + Vector3.right * 6f, PlayerPos);
            }
            else if (FirstSceneStep > SceneNotStarted && FirstSceneStep < SceneBuilding)
            {
                // Asked or on the way: he asks again (the scene resumes from the question).
                _firstAsks = 1;
                CurrentPhase = Phase.Waiting;
                _nextPitchAt = Now;
            }
        }
    }
}
