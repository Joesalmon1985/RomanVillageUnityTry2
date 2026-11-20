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
        float _yaw, _pitch, _fov, _yawV, _pitchV, _fovV, _pushV; float _focusUntil; List<int> _focus = new List<int>();
        public float CamYaw => _yaw; public float CamPitch => _pitch; public float CamFov => _fov;
        float _lockUntil, _aiAt, _finishAt = -1f, _phaseUntil, _readyAt, _backAt = -1f;
        bool _onHand, _onTray, _onSeal, _carryPending, _leaveSent, _frameInit;
        int _aideBubble, _speakerBubble, _lineCounter;
        // WU-4: two-tap actions on the shared WorldAction machine
        readonly Dictionary<string, CouncilAction> _acts = new Dictionary<string, CouncilAction>();
        CouncilTablet _tablet; bool _immediate; float _push;
        public CouncilTablet Tablet => _tablet ?? (_tablet = new CouncilTablet(this));
        /// <summary>Tap on the ledger card of an open preview confirms; the card is where the player looks.</summary>
        public int PreviewsShown { get; private set; }
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
            Scene.ViewCamera = _cam;
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
            WorldActions.Cancel(); LedgerCard.Hide(); Scene?.ClearPreview();
            if (_cam != null)
            {
                _cam.transform.SetParent(_camParent, false);
                _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; _cam.fieldOfView = _camFov;
            }
            SpeechBubbles.ShowInEncounter = false;
            foreach (int h in new[] { _aideBubble, _speakerBubble }) if (h != 0) SpeechBubbles.Close(h);
            if (Scene != null) { Scene.ViewCamera = null; Scene.CloseBubbles(); Scene.HoldPetition = false; Scene.Bench?.HideSpeakers(); }
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
            CheckPreview();
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
            }
            CouncilBubbles.Sweep(); // nothing but the council's own bubbles while the council is open
            {
            }
        }

        void UpdateCamera(float dt)
        {
            if (_cam == null || Scene == null) return;
            if (View == ViewKind.Forward) SmoothForward(dt);
            else _cam.fieldOfView = L.CameraFov(Screen.width, Screen.height);
            _camT = Mathf.MoveTowards(_camT, 1f, dt / 0.45f);
            GetPose(View, out var pos, out var rot);
            float k = Mathf.SmoothStep(0, 1, _camT);
            _cam.transform.position = Vector3.Lerp(_fromPos, pos, k);
            _cam.transform.rotation = Quaternion.Slerp(_fromRot, rot, k);
        }

        /// <summary>The places the camera should show now: the proposals being offered, the catspaws' places, a place where something just changed, else all.</summary>
        List<int> WantedFrame()
        {
            var sc = Council; var e = sc?.Election; var l = new List<int>();
            if (e == null) return l;
            if (Scene.Previewing && Scene.PreviewTracks.Count > 0) return Scene.PreviewTracks;
            if (Time.unscaledTime < _focusUntil) return _focus;
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
            if (mine && e.Phase == ElectionPhase.Choosing)
            {
                int pi = Scene.PointedProposal;
                if (pi >= 0 && pi < Scene.Proposals.Count) { foreach (var st in Scene.Proposals[pi].Steps) l.Add(st.Key); }
                else foreach (var p in Scene.Proposals) foreach (var st in p.Steps) if (!l.Contains(st.Key)) l.Add(st.Key);
            }
            else if (mine && (e.Phase == ElectionPhase.Ready || e.Phase == ElectionPhase.BustPending))
                for (int t = 2; t <= 12; t++) if (e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0) l.Add(t);
            return l;
        }

        public void ClearFocus() { _focusUntil = 0f; }

        public void FocusOn(IEnumerable<int> tracks, float seconds)
        {
            _focus = new List<int>(tracks); _focusUntil = Time.unscaledTime + seconds;
        }

        void SmoothForward(float dt)
        {
            bool closer = CouncilScene.CloserCamera(Screen.height);
            Scene.Frame(WantedFrame(), Mathf.Max(0.5f, _cam.aspect), out float yaw, out float pitch, out float fov, out float push, out Vector3 pdir, closer);
            if (!_frameInit) { _yaw = yaw; _pitch = pitch; _fov = fov; _push = push; _frameInit = true; }
            _push = Mathf.SmoothDamp(_push, push, ref _pushV, 0.55f, 30f, dt);
            if (pdir.sqrMagnitude > 0.01f) Scene.PushDir = pdir;
            Scene.CamPush = _push;
            _yaw = Mathf.SmoothDampAngle(_yaw, yaw, ref _yawV, 0.55f, 60f, dt);
            _pitch = Mathf.SmoothDamp(_pitch, pitch, ref _pitchV, 0.55f, 40f, dt);
            _fov = Mathf.SmoothDamp(_fov, fov, ref _fovV, 0.55f, 40f, dt);
            _cam.fieldOfView = _fov;
        }

        void GetPose(ViewKind v, out Vector3 pos, out Quaternion rot)
        {
            var root = Scene.Root;
            if (v == ViewKind.Forward) { pos = Scene.CamPos; rot = Quaternion.Euler(_pitch, _yaw, 0); }
            else { pos = root.TransformPoint(Scene.Bench.CamPosLocal); rot = root.rotation; }
        }

        void PlaceCamera(ViewKind v, bool instant)
        {
            if (v == ViewKind.Forward && !_frameInit)
            {
                Scene.Frame(null, Mathf.Max(0.5f, _cam.aspect), out _yaw, out _pitch, out _fov); _frameInit = true; _cam.fieldOfView = _fov;
            }
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
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E)) && WorldActions.State == WorldActionState.Preview && WorldActions.Machine.Current is CouncilAction ka) TapAction(ka);
            if (Input.GetKeyDown(KeyCode.Backspace)) WorldActions.Cancel();
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

        string LeftTargetAt(Vector2 safe)
        {
            if (L.SlabSame.Contains(safe)) return "same";
            if (L.SlabDifferent.Contains(safe)) return "different";
            if (L.RightLantern.Contains(safe)) return "rightlantern";
            return null;
        }

        /// <summary>A pointer tap at a screen point (pixels, origin bottom-left). True when it landed on a target. Real clicks, touches, AutoPlay and tests all come here.</summary>
        public bool TapScreen(Vector2 screenBL)
        {
            if (_finished) return false;
            string target;
            if (View == ViewKind.Forward) target = CouncilScene.Pick(Scene.Targets(_cam), screenBL);
            else target = LeftTargetAt(L.ToSafe(screenBL, Screen.width, Screen.height));
            var gui = new Vector2(screenBL.x, Screen.height - screenBL.y);
            if (View == ViewKind.Forward && LedgerCard.Contains(gui))
            {
                // the card of a preview is a second place to confirm; a tablet's card just stays
                if (WorldActions.State == WorldActionState.Preview && WorldActions.Machine.Current is IWorldAction open) { TapsHandled++; if (target == null || !(target.StartsWith("note ") || target == "recorder" || target == "ladder")) { TapAction((CouncilAction)open); return true; } }
                else return true;
            }
            if (target == null) { if (View == ViewKind.Forward && WorldActions.Open) WorldActions.TapElsewhere(); return false; }
            LastTarget = target;
            float now = Time.unscaledTime;
            if (now < _lockUntil || InTransition || Hud.MenuOpen) return true; // swallowed: a held tap or a tap during a camera turn
            var sc = Council; var e = sc?.Election; if (e == null) return true;
            if (Handover) { if (now > _finishAt - 3.5f) _finished = true; return true; }
            TapsHandled++;
            if (View == ViewKind.Forward) ForwardTap(sc, e, target);
            else LeftTap(sc, e, target);
            return true;
        }

        void ForwardTap(SettlementCouncil sc, ElectionState e, string target)
        {
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat;
            if (target == "lantern") { WorldActions.Cancel(); TurnLeft(); return; }
            if (target == "tablet") { WorldActions.Inspect(Tablet); return; }
            if (target == "ladder") { if (_immediate) Return(); else TapAction(Act("leave", CouncilAction.Kind.Leave, null)); return; }
            if (!Scene.Settled || !mine) return;
            bool preview = WorldActions.State == WorldActionState.Preview;
            if (target == "hand" || target.StartsWith("paw "))
            {
                if (preview && !_immediate) { WorldActions.Cancel(); return; }  // the tap that cancels does nothing else
                if (e.Phase == ElectionPhase.Ready) Send(new CouncilPush { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
            }
            else if (target == "seal" || target == "recorder" || target.StartsWith("lead "))
            {
                if (e.Phase == ElectionPhase.Ready && e.HasAdvanced)
                {
                    if (_immediate) Send(new CouncilBank { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
                    else TapAction(Act("bank", CouncilAction.Kind.Bank, null));
                }
                else if (e.Phase == ElectionPhase.BustPending && target == "seal") Send(new CouncilAcceptBust { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
            }
            else if (target == "petition")
            {
                if (e.Phase == ElectionPhase.BustPending && e.PetitionAvailable) Send(new CouncilSpendPetition { Settlement = SettlementId, ExpectedSeq = e.ActionSeq });
            }
            else if (e.Phase == ElectionPhase.Choosing)
            {
                CouncilProposal chosen = null;
                if (target.StartsWith("note ")) { int k = int.Parse(target.Substring(5)); if (k >= 0 && k < Scene.Proposals.Count) chosen = Scene.Proposals[k]; }
                else if (target.StartsWith("place "))
                {
                    int t = int.Parse(target.Substring(6)); int n = 0;
                    foreach (var p in Scene.Proposals) if (p.Touches(t)) { chosen = p; n++; }
                    if (n != 1) chosen = null; // several proposals reach this place: the player taps the note they mean
                }
                if (chosen == null) { if (preview) WorldActions.Cancel(); }
                else if (_immediate) Commit(e, chosen);
                else TapAction(Act("p" + e.PushId + ":" + chosen.Tray + ":" + chosen.Socket, CouncilAction.Kind.Proposal, chosen));
            }
        }

        CouncilAction Act(string key, CouncilAction.Kind kind, CouncilProposal p)
        {
            if (!_acts.TryGetValue(key, out var a)) { a = new CouncilAction(this, kind, p, E != null ? E.PushId : 0); _acts[key] = a; }
            return a;
        }

        /// <summary>One tap on a two-tap action: the first previews, the second (after the machine's double-tap guard) sends once.</summary>
        public WorldTapResult TapAction(CouncilAction a)
        {
            var r = WorldActions.Tap(a);
            if (r == WorldTapResult.Previewed) PreviewsShown++;
            if (r == WorldTapResult.Confirmed) _lockUntil = Time.unscaledTime + 0.3f;
            return r;
        }

        /// <summary>The machine's confirm lands here: send the command for the action (once).</summary>
        public bool CommitAction(CouncilAction a)
        {
            var e = E; if (e == null) return false;
            switch (a.K)
            {
                case CouncilAction.Kind.Proposal: { int before = Accepted; Commit(e, a.P); return Accepted > before; }
                case CouncilAction.Kind.Bank: { int before = Accepted; Send(new CouncilBank { Settlement = SettlementId, ExpectedSeq = e.ActionSeq }); return Accepted > before; }
                default: Return(); return true;
            }
        }

        /// <summary>A preview that no longer fits the state (the turn passed, the trays changed) is dropped.</summary>
        void CheckPreview()
        {
            var m = WorldActions.Machine;
            if (m.State == WorldActionState.Preview && m.Current is CouncilAction a && !a.Check().Ok) WorldActions.Cancel();
            if (m.State != WorldActionState.Preview && Scene != null && Scene.Previewing && !_confirming) Scene.ClearPreview();
        }
        bool _confirming;

        void Commit(ElectionState e, CouncilProposal p)
        {
            var t = e.Pending[p.Tray];
            if (t.Mode == TrayMode.Both) Send(new CouncilChoose { Settlement = SettlementId, PushId = e.PushId, Tray = p.Tray, Socket = -1, ExpectedSeq = e.ActionSeq });
            else Send(new CouncilChoose { Settlement = SettlementId, PushId = e.PushId, Tray = p.Tray, Socket = p.Socket, ExpectedSeq = e.ActionSeq });
        }

        void Send(Command c)
        {
            var r = Do(c);
            if (r.Ok) _lockUntil = Time.unscaledTime + 0.3f;
        }

        /// <summary>Old one-tap path for tests and scripts that do not care about the preview: a note, the seal or a lead commits at once.</summary>
        public bool TapTargetNow(string name)
        {
            _immediate = true;
            try { return TapTarget(name); } finally { _immediate = false; }
        }

        /// <summary>Taps the centre of a named target of the current view (AutoPlay and tests: the same path as a real tap).</summary>
        public bool TapTarget(string name)
        {
            if (View == ViewKind.Forward)
            {
                foreach (var t in Scene.Targets(_cam)) if (t.Name == name) return TapScreen(t.Px);
                return false;
            }
            Rect r = default; bool found = false;
            foreach (var kv in L.LeftTargets()) if (kv.Key == name) { r = kv.Value; found = true; }
            if (!found && name == "rightlantern") { r = L.RightLantern; found = true; }
            if (!found) return false;
            var px = L.ToPixels(r, Screen.width, Screen.height);
            return TapScreen(new Vector2(px.center.x, Screen.height - px.center.y));
        }

        // ------------------------------------------------------------------ rivals

        void PaceRivals(SettlementCouncil sc, ElectionState e, float now)
        {
            if (e.Phase == ElectionPhase.Complete) return;
            bool rival = e.PlayerSeat < 0 || e.Active != e.PlayerSeat;
            if (!rival || InTransition && View == ViewKind.Forward) return;
            if (now < _aiAt || !(Scene.Settled || now > _aiAt + 2.5f)) return;
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
            Scene.CloseBubbles();
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
                case CouncilBust b: FocusOn(TracksOf(b.Lost), 3.4f); break;
                case CouncilBanked bk: FocusOn(TracksOf(bk.Places), 3.0f); break;
                case CouncilTrackClaimed cl: FocusOn(new[] { cl.Track }, 3.6f); break;
                case CouncilOfficeHandedOver h:
                    Handover = true; HandoverLine = "I will serve as " + h.Office + ". Thank you for sitting with us.";
                    Scene.CandidateSay(HandoverLine, 4.6f);
                    _finishAt = Time.unscaledTime + 5.5f;
                    if (View == ViewKind.Left) { TurnRight(); }
                    break;
            }
        }

        // ------------------------------------------------------------------ onboarding

        static List<int> TracksOf(IEnumerable<BankedPlace> items)
        {
            var l = new List<int>();
            foreach (var it in items) if (!l.Contains(it.Track)) l.Add(it.Track);
            return l;
        }

        void Onboard(SettlementCouncil sc, ElectionState e)
        {
            if (View != ViewKind.Forward || e.PlayerSeat < 0 || e.Active != e.PlayerSeat || InTransition) return;
            if (!_onHand && e.Phase == ElectionPhase.Ready && Scene.Settled) { _onHand = true; OnboardingLines++; Scene.CandidateSay("Tap my hand to press on.", 4f); Scene.Press("hand"); }
            else if (!_onTray && e.Phase == ElectionPhase.Choosing && Scene.Settled) { _onTray = true; OnboardingLines++; Scene.CandidateSay("Tap the place I point at.", 4f); }
            else if (!_onSeal && e.HasAdvanced && e.Phase == ElectionPhase.Ready && Scene.Settled) { _onSeal = true; OnboardingLines++; Scene.CandidateSay("Tap my seal to settle.", 4f); }
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
