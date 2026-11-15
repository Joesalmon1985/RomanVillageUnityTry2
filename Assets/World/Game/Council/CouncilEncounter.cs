using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;
using L = World.Game.CouncilLayout;

namespace World.Game
{
    /// <summary>
    /// The council encounter on the existing encounter host: a fixed forward camera on the gathering, a left camera on
    /// the listening bench, input by pointer taps on props (no on-screen buttons) and key or touch turns. It only sends
    /// council commands and reads <c>WorldState.Council</c>; after each accepted command it steps the village clock one
    /// tick so the command applies and its events arrive (the clock stays paused for the encounter).
    /// </summary>
    public sealed class CouncilEncounter : IEncounter
    {
        public const string Id = "council";
        public static CouncilEncounter Current { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; AiDelay = 0.75f; }

        public enum ViewKind { Forward, Left }
        public enum ListenPhase { None, Intro, Question, Feedback, Done }

        /// <summary>Seconds between rival actions (tests and fast scripts lower it).</summary>
        public static float AiDelay = 0.75f;
        public static float IntroSeconds = 2.4f;

        public string Title => "Village council";
        public bool IsFinished => _finished;
        public int SettlementId { get; private set; }
        public CouncilScene Scene { get; private set; }
        public ViewKind View { get; private set; } = ViewKind.Forward;
        public ListenPhase Listen { get; private set; }
        public int Accepted { get; private set; }
        public int Refused { get; private set; }
        public string LastRefusal { get; private set; }
        public int TapsHandled { get; private set; }
        public string LastTarget { get; private set; }
        public int LastResponseMs { get; private set; }
        public int AnswersSent { get; private set; }
        public int CuesPlayed { get; private set; }
        public string LastCue { get; private set; }
        public bool Handover { get; private set; }
        public string HandoverLine { get; private set; }
        public int OnboardingLines { get; private set; }
        public bool InTransition => _camT < 1f;
        public bool ReadyForAnswer => Listen == ListenPhase.Question && Time.unscaledTime >= _readyAt;

        public SettlementCouncil Council => CouncilApi.Get(_ctx.Sim.State, SettlementId);
        public ElectionState E => Council?.Election;

        EncounterContext _ctx;
        bool _finished;
        Camera _cam; Transform _camParent; Vector3 _camLocalPos; Quaternion _camLocalRot; float _camFov;
        Vector3 _fromPos; Quaternion _fromRot; float _camT = 1f;
        float _lockUntil, _aiAt, _finishAt = -1f, _phaseUntil, _readyAt, _backAt = -1f;
        bool _onHand, _onTray, _onSeal, _carryPending, _leaveSent;
        int _aideBubble, _speakerBubble, _lineCounter;
        EncounterSfx _sfx;
        AudioClip _good, _bad;
        GameObject _sfxRoot;
        int _beginFrame;

        // ------------------------------------------------------------------ lifecycle

        public void Begin(EncounterContext ctx)
        {
            _ctx = ctx; Current = this;
            _beginFrame = Time.frameCount;
            var dir = CouncilDirector.Instance;
            SettlementId = ctx.Request is int id ? id : (dir != null ? dir.HomeSettlement : 0);
            Scene = dir != null ? dir.EnsureScene(SettlementId) : CouncilScene.Current;
            ctx.Clock.StepOnce(); // one tick so the land views clear crops and trees from the stage (the clock is paused for the encounter)
            var sc = Council;
            // Entering a council not yet answered counts as accepting the asker's request; entering after leaving resumes.
            if (sc != null && sc.Status == CouncilStatus.Offered) Do(new CouncilRespond { Settlement = SettlementId, PlayerId = PlayerIds.Human, Help = true });
            sc = Council;
            if (sc != null && !sc.Present) Do(new CouncilResume { Settlement = SettlementId, PlayerId = PlayerIds.Human });

            _cam = ctx.FirstPersonCamera != null ? ctx.FirstPersonCamera : Camera.main;
            _camParent = _cam.transform.parent; _camLocalPos = _cam.transform.localPosition; _camLocalRot = _cam.transform.localRotation; _camFov = _cam.fieldOfView;
            _cam.transform.SetParent(Scene.Root, false);
            PlaceCamera(ViewKind.Forward, true);
            SpeechBubbles.ShowInEncounter = true;
            _sfxRoot = new GameObject("CouncilSfx");
            if (ctx.World != null) _sfxRoot.transform.SetParent(ctx.World.transform, false);
            _sfx = EncounterSfx.Create(_sfxRoot.transform);
            _good = MakeClip(new[] { 660f, 880f }, 0.11f, 0.55f); _bad = MakeClip(new[] { 190f, 150f }, 0.16f, 0.5f);
            _lockUntil = Time.unscaledTime + 0.45f;
            _aiAt = Time.unscaledTime + 1.0f;
            if (ctx.World != null && ctx.World.GodCam != null) { }
        }

        public void End()
        {
            if (_cam != null)
            {
                _cam.transform.SetParent(_camParent, false);
                _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; _cam.fieldOfView = _camFov;
            }
            SpeechBubbles.ShowInEncounter = false;
            foreach (int h in new[] { _aideBubble, _speakerBubble }) if (h != 0) SpeechBubbles.Close(h);
            if (Scene != null) { Scene.ClearPreview(); Scene.HoldPetition = false; Scene.Bench?.HideSpeakers(); }
            var sc = Council;
            if (sc != null && sc.Status == CouncilStatus.InProgress && sc.Election != null && sc.Election.Phase != ElectionPhase.Complete && sc.Present && !_leaveSent)
            {
                // Leaving is safe: the state is kept, an open burst is dropped, a pending push stays pending.
                _ctx.Sim.Submit(new CouncilLeave { Settlement = SettlementId, PlayerId = PlayerIds.Human });
                _leaveSent = true;
            }
            if (_sfxRoot != null) UnityEngine.Object.Destroy(_sfxRoot);
            if (Current == this) Current = null;
            CouncilDirector.Instance?.EncounterEnded(this);
        }

        public void Return() { _finished = true; }

        // ------------------------------------------------------------------ commands

        public CommandResult Do(Command cmd)
        {
            cmd.PlayerId = PlayerIds.Human;
            var r = _ctx.Sim.Submit(cmd);
            if (r.Ok) { Accepted++; _ctx.Clock.StepOnce(); }
            else { Refused++; LastRefusal = r.Reason; }
            return r;
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float dt)
        {
            if (_finished) return;
            var sc = Council; var e = sc?.Election;
            if (e == null) { _finished = true; return; }
            UpdateCamera(dt);
            float now = Time.unscaledTime;
            bool menu = Hud.MenuOpen;
            if (!menu)
            {
                HandleInput(sc, e, now);
                PaceRivals(sc, e, now);
                TickListening(sc, e, now);
                Onboard(sc, e);
                if (_finishAt >= 0f && now >= _finishAt) _finished = true;
                if (_backAt >= 0f && now >= _backAt) { _backAt = -1f; if (View == ViewKind.Left) TurnRight(); }
                // control comes back at a safe boundary: my turn and the burst finished (or none)
                bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat && e.Phase != ElectionPhase.Complete;
                if (mine && View == ViewKind.Left && !InTransition && _backAt < 0f && (e.Listening == null || e.Listening.Phase != ListeningPhase.InProgress)) _backAt = now + 0.5f;
                if (Scene != null) UpdateHover(sc, e);
            }
            CouncilBubbles.Sweep(); // nothing but the council's own bubbles while the council is open
            {
            }
        }

        void UpdateCamera(float dt)
        {
            if (_cam == null || Scene == null) return;
            _cam.fieldOfView = L.CameraFov(Screen.width, Screen.height);
            _camT = Mathf.MoveTowards(_camT, 1f, dt / 0.45f);
            GetPose(View, out var pos, out var rot);
            float k = Mathf.SmoothStep(0, 1, _camT);
            _cam.transform.position = Vector3.Lerp(_fromPos, pos, k);
            _cam.transform.rotation = Quaternion.Slerp(_fromRot, rot, k);
        }

        void GetPose(ViewKind v, out Vector3 pos, out Quaternion rot)
        {
            var root = Scene.Root;
            if (v == ViewKind.Forward) { pos = root.TransformPoint(L.CamPos); rot = root.rotation * L.CamRot; }
            else { pos = root.TransformPoint(Scene.Bench.CamPosLocal); rot = root.rotation; }
        }

        void PlaceCamera(ViewKind v, bool instant)
        {
            GetPose(v, out var pos, out var rot);
            if (instant || _cam == null) { _fromPos = pos; _fromRot = rot; _camT = 1f; _cam.transform.SetPositionAndRotation(pos, rot); return; }
            _fromPos = _cam.transform.position; _fromRot = _cam.transform.rotation; _camT = 0f;
        }

        // ------------------------------------------------------------------ input

        void HandleInput(SettlementCouncil sc, ElectionState e, float now)
        {
            if (Time.frameCount == _beginFrame) return;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.Keypad7)) TurnLeft();
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.Keypad9)) TurnRight();
            if (Input.GetMouseButtonDown(0)) TapScreen(Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                if (!TapScreen(t.position))
                {
                    int strip = TouchStrips.StripAt(t.position);
                    if (strip == 1) TurnLeft(); else if (strip == 4) TurnRight();
                }
            }
        }

        string TargetAt(Vector2 safe)
        {
            if (View == ViewKind.Forward)
            {
                foreach (var kv in L.ForwardTargets()) if (kv.Value.Contains(safe)) return kv.Key;
                for (int i = 0; i < 3; i++) if (L.Tray(i).Contains(safe)) return "tray " + i;
                return null;
            }
            if (L.SlabSame.Contains(safe)) return "same";
            if (L.SlabDifferent.Contains(safe)) return "different";
            if (L.RightLantern.Contains(safe)) return "rightlantern";
            return null;
        }

        /// <summary>A pointer tap at a screen point (pixels, origin bottom-left). True when it landed on a target. Real clicks, touches, AutoPlay and tests all come here.</summary>
        public bool TapScreen(Vector2 screenBL)
        {
            if (_finished) return false;
            var safe = L.ToSafe(screenBL, Screen.width, Screen.height);
            string target = TargetAt(safe);
            if (target == null) return false;
            LastTarget = target;
            float now = Time.unscaledTime;
            if (now < _lockUntil || InTransition || Hud.MenuOpen) return true; // swallowed: a held tap or a tap during a camera turn
            var sc = Council; var e = sc?.Election; if (e == null) return true;
            if (Handover) { if (now > _finishAt - 3.5f) _finished = true; return true; }
            TapsHandled++;
            if (View == ViewKind.Forward) ForwardTap(sc, e, target, safe);
            else LeftTap(sc, e, target);
            return true;
        }

        void ForwardTap(SettlementCouncil sc, ElectionState e, string target, Vector2 safe)
        {
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
            if (target == "lantern") { TurnLeft(); return; }
            if (!Scene.Settled) return;
            switch (target)
            {
                case "hand":
                    if (mine && e.Phase == ElectionPhase.Ready) Send(new CouncilPush { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
                    break;
                case "seal":
                    if (mine && e.Phase == ElectionPhase.Ready && e.HasAdvanced) Send(new CouncilBank { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
                    else if (mine && e.Phase == ElectionPhase.BustPending) Send(new CouncilAcceptBust { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
                    break;
                case "petition":
                    if (mine && e.Phase == ElectionPhase.BustPending && e.PetitionAvailable) Send(new CouncilSpendPetition { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
                    break;
                default:
                    int tray = -1, socket = -1;
                    if (target.StartsWith("tray ")) tray = int.Parse(target.Substring(5));
                    else if (target.StartsWith("socket ")) { tray = target[7] - '0'; socket = target[8] == 'b' ? 1 : 0; }
                    if (tray < 0 || !mine || e.Phase != ElectionPhase.Choosing || tray >= e.Pending.Count) break;
                    var t = e.Pending[tray];
                    if (t.Mode == TrayMode.Both) Send(new CouncilChoose { Settlement = SettlementId, PushId = e.PushId, Tray = tray, Socket = -1, ExpectedSeq = e.ActionSeq });
                    else if (socket >= 0 && !t.Sockets[socket].IsEmpty) Send(new CouncilChoose { Settlement = SettlementId, PushId = e.PushId, Tray = tray, Socket = socket, ExpectedSeq = e.ActionSeq });
                    break;
            }
        }

        void Send(Command c)
        {
            var r = Do(c);
            if (r.Ok) { _lockUntil = Time.unscaledTime + 0.3f; Scene.ClearPreview(); }
        }

        /// <summary>Taps the centre of a named target of the current view (AutoPlay and tests: the same path as a real tap).</summary>
        public bool TapTarget(string name)
        {
            var list = View == ViewKind.Forward ? L.ForwardTargets() : L.LeftTargets();
            Rect r = default; bool found = false;
            foreach (var kv in list) if (kv.Key == name) { r = kv.Value; found = true; }
            if (!found && name.StartsWith("tray ")) { r = L.Tray(int.Parse(name.Substring(5))); found = true; }
            if (!found && name == "rightlantern") { r = L.RightLantern; found = true; }
            if (!found) return false;
            var px = L.ToPixels(r, Screen.width, Screen.height);
            return TapScreen(new Vector2(px.center.x, Screen.height - px.center.y));
        }

        // ------------------------------------------------------------------ hover preview

        void UpdateHover(SettlementCouncil sc, ElectionState e)
        {
            if (View != ViewKind.Forward || e.Phase != ElectionPhase.Choosing || e.PlayerSeat != e.Active) { Scene.ClearPreview(); return; }
            var safe = L.ToSafe(Input.mousePosition, Screen.width, Screen.height);
            string target = TargetAt(safe);
            if (target == null) { Scene.ClearPreview(); return; }
            PreviewTarget(e, target);
        }

        public void PreviewTarget(ElectionState e, string target)
        {
            int tray = -1, socket = -1;
            if (target.StartsWith("tray ")) tray = int.Parse(target.Substring(5));
            else if (target.StartsWith("socket ")) { tray = target[7] - '0'; socket = target[8] == 'b' ? 1 : 0; }
            if (tray < 0 || tray >= e.Pending.Count) { Scene.ClearPreview(); return; }
            var t = e.Pending[tray];
            var tracks = new List<int>();
            for (int s = 0; s < 2; s++) if ((socket < 0 || socket == s) && !t.Sockets[s].IsEmpty) tracks.Add(t.Sockets[s].Track);
            Scene.SetPreview(tracks, tray, socket);
        }

        // ------------------------------------------------------------------ rivals

        void PaceRivals(SettlementCouncil sc, ElectionState e, float now)
        {
            if (e.Phase == ElectionPhase.Complete) return;
            bool rival = e.PlayerSeat < 0 || e.Active != e.PlayerSeat;
            if (!rival || InTransition && View == ViewKind.Forward) return;
            if (now < _aiAt || !Scene.Settled) return;
            var r = Do(new CouncilAiStep { Settlement = SettlementId });
            _aiAt = now + AiDelay;
        }

        // ------------------------------------------------------------------ turning

        public bool TurnLeft()
        {
            if (View != ViewKind.Forward || InTransition || _finished) return false;
            var sc = Council; var e = sc?.Election; if (e == null || e.Phase == ElectionPhase.Complete) return false;
            bool rival = e.PlayerSeat >= 0 && e.Active != e.PlayerSeat;
            bool open = e.Listening != null && e.Listening.Phase == ListeningPhase.InProgress;
            if (!rival && !open) return false; // listening is for the rivals' turns
            View = ViewKind.Left;
            PlaceCamera(ViewKind.Left, false);
            Scene.ClearPreview(); Scene.CloseBubbles();
            _lockUntil = Time.unscaledTime + 0.5f;
            BeginListening(sc, e);
            return true;
        }

        public bool TurnRight()
        {
            if (View != ViewKind.Left || InTransition) return false;
            View = ViewKind.Forward;
            PlaceCamera(ViewKind.Forward, false);
            _lockUntil = Time.unscaledTime + 0.5f;
            CloseBenchBubbles();
            Listen = ListenPhase.None;
            Scene.Bench.HideSpeakers();
            var e = E;
            if (e != null && e.Listening != null && e.Listening.Phase == ListeningPhase.InProgress && e.PlayerSeat >= 0 && e.Active == e.PlayerSeat)
                Do(new CouncilAbandonListening { Settlement = SettlementId });
            if (_carryPending) { _carryPending = false; Scene.HoldPetition = false; Scene.BeginCarry(); }
            return true;
        }

        // ------------------------------------------------------------------ listening (left view)

        void CloseBenchBubbles()
        {
            if (_aideBubble != 0) SpeechBubbles.Close(_aideBubble); _aideBubble = 0;
            if (_speakerBubble != 0) SpeechBubbles.Close(_speakerBubble); _speakerBubble = 0;
        }

        void BeginListening(SettlementCouncil sc, ElectionState e)
        {
            var bench = Scene.Bench;
            bool open = e.Listening != null && e.Listening.Phase == ListeningPhase.InProgress;
            bool canStart = !open && e.PlayerSeat >= 0 && e.Active != e.PlayerSeat && !e.PetitionEarned && e.ListenBursts < e.ListeningRules.MaxBursts;
            if (canStart) Do(new CouncilStartListening { Settlement = SettlementId });
            open = e.Listening != null && e.Listening.Phase == ListeningPhase.InProgress;
            if (!open)
            {
                Listen = ListenPhase.Done;
                string why = e.PetitionEarned ? "You already hold your petition." : "There is nothing more to hear just now.";
                _aideBubble = CouncilBubbles.Say(bench.AideTalk, why, 4f);
                return;
            }
            // Hear the line before the one to judge (the reference, or the previous speaker when resuming), then the new line.
            Listen = ListenPhase.Intro;
            _phaseUntil = Time.unscaledTime + IntroSeconds;
            var ref0 = CouncilApi.ListeningLineOf(e.Listening.LineIds[e.Listening.Index]);
            bench.ShowSpeaker(ref0.Speaker, _lineCounter++);
            bench.SpeakerGesture(e.Listening.Tags[e.Listening.Index]);
            _speakerBubble = CouncilBubbles.Say(bench.SpeakerTalk, ref0.Text, IntroSeconds + 0.6f);
            _aideBubble = CouncilBubbles.Say(bench.AideTalk, "Listen to how they say it.", IntroSeconds);
        }

        void TickListening(SettlementCouncil sc, ElectionState e, float now)
        {
            if (View != ViewKind.Left || Listen == ListenPhase.None || Listen == ListenPhase.Done) return;
            var ls = e.Listening;
            if (ls == null) return;
            var bench = Scene.Bench;
            if (Listen == ListenPhase.Intro && now >= _phaseUntil && !InTransition) ShowQuestion(e);
            else if (Listen == ListenPhase.Feedback && now >= _phaseUntil)
            {
                if (ls.Phase == ListeningPhase.InProgress) ShowQuestion(e);
                else { Listen = ListenPhase.Done; _backAt = now + 1.8f; }
            }
        }

        void ShowQuestion(ElectionState e)
        {
            var ls = e.Listening; var bench = Scene.Bench;
            if (ls.Phase != ListeningPhase.InProgress) { Listen = ListenPhase.Done; return; }
            var line = CouncilApi.ListeningLineOf(ls.LineIds[ls.Index + 1]);
            CloseBenchBubbles();
            bench.ShowSpeaker(line.Speaker, _lineCounter++);
            bench.SpeakerGesture(ls.Tags[ls.Index + 1]);
            if (ls.Index + 2 < ls.LineIds.Length) bench.ShowNext(CouncilApi.ListeningLineOf(ls.LineIds[ls.Index + 2]).Speaker, _lineCounter++);
            _speakerBubble = CouncilBubbles.Say(bench.SpeakerTalk, line.Text, 60f);
            _readyAt = Time.unscaledTime + 0.45f; // readable and ready
            Listen = ListenPhase.Question;
        }

        void LeftTap(SettlementCouncil sc, ElectionState e, string target)
        {
            if (target == "rightlantern") { TurnRight(); return; }
            if (Listen != ListenPhase.Question || Time.unscaledTime < _readyAt) return;
            bool same = target == "same";
            int ms = Mathf.Clamp(Mathf.RoundToInt((Time.unscaledTime - _readyAt) * 1000f), 1, ListeningSession.MaxResponseMs);
            AnswerListening(same ? ListeningChoice.Same : ListeningChoice.Different, ms);
        }

        /// <summary>Answers the current line (the slab taps, scripts and tests). Response time is in milliseconds.</summary>
        public CommandResult AnswerListening(ListeningChoice choice, int responseMs)
        {
            var e = E;
            Listen = ListenPhase.Feedback; _phaseUntil = Time.unscaledTime + 1.0f;
            LastResponseMs = responseMs; AnswersSent++;
            Scene.Bench.PulseSlab(choice == ListeningChoice.Same);
            if (_speakerBubble != 0) { SpeechBubbles.Close(_speakerBubble); _speakerBubble = 0; }
            var r = Do(new CouncilAnswerListening { Settlement = SettlementId, Choice = choice, ResponseMs = responseMs });
            if (!r.Ok) Listen = ListenPhase.Question;
            return r;
        }

        // ------------------------------------------------------------------ events (forwarded by the director)

        public void OnEvent(CouncilEvent ev)
        {
            var bench = Scene != null ? Scene.Bench : null;
            switch (ev)
            {
                case CouncilListeningAnswered a:
                    bench?.AideReact(a.Correct);
                    Cue(a.Correct);
                    break;
                case CouncilListeningFinished f:
                    if (_aideBubble != 0) SpeechBubbles.Close(_aideBubble);
                    _aideBubble = CouncilBubbles.Say(bench.AideTalk, f.Result.Earned ? "That is enough. I will take a petition to the table." : "Not quite this time. Go back to your table.", 3.5f);
                    break;
                case CouncilPetitionEarned _:
                    _carryPending = true; Scene.HoldPetition = true;
                    break;
                case CouncilBustPending _:
                    break;
                case CouncilOfficeHandedOver h:
                    Handover = true; HandoverLine = "I will serve as " + h.Office + ". Thank you for sitting with us.";
                    Scene.CandidateSay(HandoverLine, 4.6f);
                    _finishAt = Time.unscaledTime + 5.5f;
                    if (View == ViewKind.Left) { TurnRight(); }
                    break;
            }
        }

        // ------------------------------------------------------------------ onboarding

        void Onboard(SettlementCouncil sc, ElectionState e)
        {
            if (View != ViewKind.Forward || e.PlayerSeat < 0 || e.Active != e.PlayerSeat || InTransition) return;
            if (!_onHand && e.Phase == ElectionPhase.Ready && Scene.Settled) { _onHand = true; OnboardingLines++; Scene.CandidateSay("Tap my open hand and the catspaws go on.", 5f); }
            else if (!_onTray && e.Phase == ElectionPhase.Choosing && Scene.Settled) { _onTray = true; OnboardingLines++; Scene.CandidateSay("Tap a tray. Its carvings show which disputes move.", 5f); }
            else if (!_onSeal && e.HasAdvanced && e.Phase == ElectionPhase.Ready && Scene.Settled) { _onSeal = true; OnboardingLines++; Scene.CandidateSay("Tap the seal to keep these places for good.", 5f); }
        }

        // ------------------------------------------------------------------ sound (synthesised)

        void Cue(bool correct)
        {
            CuesPlayed++; LastCue = correct ? "good" : "bad";
            if (_sfx != null) _sfx.Play(correct ? _good : _bad, 0.5f);
        }

        static AudioClip MakeClip(float[] freqs, float noteSeconds, float vol)
        {
            int sr = 22050; int per = Mathf.RoundToInt(sr * noteSeconds);
            var data = new float[per * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)sr, env = Mathf.Min(1f, i / 200f) * Mathf.Pow(1f - i / (float)per, 1.5f);
                    float s = Mathf.Sin(2 * Mathf.PI * freqs[n] * t);
                    if (freqs[n] < 300f) s = Mathf.Sign(s) * 0.5f + 0.5f * s; // a rough buzz for a miss
                    data[n * per + i] = s * env * vol;
                }
            var c = AudioClip.Create("council cue", data.Length, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        // ------------------------------------------------------------------ gui (nothing in normal play)

        public void DrawGui() { }
    }
}
