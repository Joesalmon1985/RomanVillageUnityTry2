using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim.Encounters.Rhythm;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game
{
    /// <summary>
    /// The dance at the fire with Sol ("rhythm_party", REFACTOR_SPEC 2.E): a 2 s count-in, then one 64-beat phrase at
    /// 174 BPM played on four drums round the fire (D F J K, or touch strips 1-4). A cue lights its drum from cue time
    /// minus <see cref="FallSeconds"/> (the lead the lanes used to have) while an ember flies from the fire to that drum,
    /// landing on the cue time; holds keep the drum lit with a stream of embers; each judgement flashes the drum. The
    /// final block offers the ending choice, "Fire leap" (Left, <see cref="SimRoute.Bar"/>) or "Spirit call" (Right,
    /// <see cref="SimRoute.Balcony"/>), cosmetic and not saved. Rules live in PhraseSession (unchanged); audio runs on the
    /// DSP clock via StemPlayer. Keyboard, touch strips, the injected-input hook and the built-in autoplayer all go
    /// through HandlePress/HandleRelease. Visuals are drawn by <see cref="DanceCircle"/>. Owner: package D.
    /// </summary>
    public sealed class RhythmPartyEncounter : IEncounter
    {
        public enum Screen { CountIn, Playing, Result }

        /// <summary>Test hooks (null = normal): tuning, and the Resources folder of the stems (a missing folder uses SynthAudio).</summary>
        public static RhythmTuning TuningOverride;
        public static string StemDirOverride;
        public static RhythmPartyEncounter Current { get; private set; }

        /// <summary>How long before its time a cue appears (drum lights, ember leaves the fire). RHYTHM_API: at most 1.2 s.</summary>
        public const float FallSeconds = 1.1f;
        /// <summary>Look down at the drums (FirstPersonAvatar: positive pitch looks down).</summary>
        public const float ViewPitch = 15f;
        public const string CalibrationPref = "World.Rhythm.CalibrationMs";
        public const string FireLeapName = "Fire leap", SpiritCallName = "Spirit call";
        public static readonly KeyCode[] PadKeys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        public static readonly string[] StageNames = { "Drums", "Bass joins", "Drums back", "Bass up" };

        public static string ChoiceName(SimRoute r) => r == SimRoute.Bar ? FireLeapName : SpiritCallName;

        public string Title => "Dance at the fire with Sol";
        public bool IsFinished { get; private set; }
        public Screen Phase { get; private set; }
        public RhythmTuning Tuning { get; private set; }
        public Chart Chart { get; private set; }
        public PhraseSession Session { get; private set; }
        public StemPlayer Player { get; private set; }
        public StemSet Stems { get; private set; }
        public DanceCircle Circle { get; private set; }
        public int Attempts { get; private set; }
        /// <summary>Song time sampled this frame (seconds, negative in the count-in).</summary>
        public double SongTime { get; private set; } = double.NaN;
        public PhraseResult LastResult { get; private set; }
        public RouteOutcome LastRouteOpened { get; private set; }
        public int PressesHandled { get; private set; }
        public int JudgementsSeen { get; private set; }
        public int BlocksCheered { get; private set; }
        /// <summary>Judgement counts of the current attempt (taps and hold heads; misses include missed holds).</summary>
        public int LivePerfect { get; private set; }
        public int LiveGreat { get; private set; }
        public int LiveOk { get; private set; }
        public int LiveMiss { get; private set; }
        /// <summary>Gain targets as last sent to the stems.</summary>
        public StemGains LastGains { get; private set; }
        public bool BassJoined { get; private set; }

        // ---- WU-5: world props, tally, pause, tuning bell, log seat ----
        public enum PauseStage { None, Paused, Counting }
        public enum Card { None, Tally, Progress, Tune }
        /// <summary>Performers, block torches, tuning bell and seat (built for the dance, removed with it).</summary>
        public DanceStageProps Props { get; private set; }
        public PauseStage PauseState { get; private set; }
        public bool IsPaused => PauseState != PauseStage.None;
        /// <summary>The four-count number on screen after a resume (4 3 2 1), 0 otherwise.</summary>
        public int ResumeCount { get; private set; }
        public int Pauses { get; private set; }
        /// <summary>True after the first tap on the log seat that asks to leave; the second tap (within <see cref="LeaveConfirmSeconds"/>) leaves.</summary>
        public bool LeaveArmed { get; private set; }
        public bool Left { get; private set; }
        public const float LeaveConfirmSeconds = 4f;
        private readonly WorldActionMachine _seatMachine = new WorldActionMachine { PreviewTimeoutSeconds = LeaveConfirmSeconds };
        private LogSeatAction _seatAction;
        private string _ledgerKey;
        /// <summary>Judgement counts of this attempt as Sol keeps them: every judgement event, including strays and hold ends.</summary>
        public int TallyPerfect { get; private set; }
        public int TallyGreat { get; private set; }
        public int TallyOk { get; private set; }
        public int TallyMiss { get; private set; }
        public int TallyStray { get; private set; }
        public int TallyHoldComplete { get; private set; }
        public int TallyHoldPartial { get; private set; }
        public Card OpenCard { get; private set; }
        public double LastTuneMs { get; private set; } = double.NaN;
        public bool TuneActive => _tune != null;
        public double[] TuneClickTimes => _tune != null ? _tune.ClickTimes : null;
        public int TuneMatched => _tune != null ? _tune.Matched : 0;
        private TapAlongCalibration _tune;
        private readonly List<AudioSource> _tuneClicks = new List<AudioSource>();
        private readonly List<Vector2> _tapQueue = new List<Vector2>();
        private int _lastResumeCount;
        private bool _shapeHooked;

        /// <summary>When each cue's drum first lit this attempt: cue index -> (pad, song time). For tests and captures.</summary>
        public readonly Dictionary<int, (int Pad, double SongTime)> LitAt = new Dictionary<int, (int, double)>();

        // Built-in autoplayer (AutoPlay script, captures, tests): presses exactly at cue times through the same path as keys.
        public bool AutoPlayEnabled;
        public Func<int, bool> AutoSkipBlock;
        public SimRoute? AutoRoute;

        private struct Input_ { public double T; public int Pad; public bool Press; }
        private readonly List<Input_> _queue = new List<Input_>();
        private readonly HashSet<int> _autoQueued = new HashSet<int>();

        private RhythmRequest _request;
        /// <summary>0-based spire the dance is played at; -1 at the home camp's circle.</summary>
        public int SpireIndex { get; private set; } = -1;
        /// <summary>The chart's pack id actually playing (the built-in "lv1_warehouse" or a local pack).</summary>
        public string PackId { get; private set; }
        /// <summary>True when a spire's own pack was wanted but missing, so spire 1's chart and track play.</summary>
        public bool PackFellBack { get; private set; }

        private EncounterContext _ctx;
        private GameObject _root;
        private EncounterSfx _sfx;
        private bool _prevPan = true;
        private Npc _sol;
        private Vector3 _solScale = Vector3.one;
        private GameObject _ownCircle;
        private int _beginFrame;
        private bool _menuWasOpen;
        private readonly bool[] _padDown = new bool[5];
        private readonly bool[] _touchDown = new bool[5];
        private sealed class Pop { public int Pad; public string Text; public Color Color; public float Born; }
        private readonly List<Pop> _pops = new List<Pop>();
        private string _banner; private float _bannerUntil; private Color _bannerColor;
        private int _lastBeatCheered = int.MinValue, _lastCountBeat = int.MinValue, _lastBassBeat = int.MinValue;

        // ------------------------------------------------------------------ lifecycle

        public void Begin(EncounterContext ctx)
        {
            _ctx = ctx;
            Current = this;
            _beginFrame = Time.frameCount;
            Tuning = TuningOverride ?? EncounterTuningFiles.Rhythm();
            if (TuningOverride == null) Tuning.CalibrationMs = PlayerPrefs.GetFloat(CalibrationPref, (float)Tuning.CalibrationMs);
            _request = ctx.Request as RhythmRequest;
            SpireIndex = _request != null ? _request.SpireIndex : -1;
            Chart = null; Stems = null;
            PackId = null; PackFellBack = false;
            if (SpireIndex >= 1 && StemDirOverride == null)
            {
                // Spires 2 to 4 play the owner's local packs; a missing folder or pack falls back to spire 1's track (SP-3).
                string want = LocalRhythmPacks.PackIdOfSpire(SpireIndex);
                if (LocalRhythmPacks.TryLoad(want, out var lc, out var ls)) { Chart = lc; Stems = ls; PackId = want; }
                else { PackFellBack = true; Debug.Log("[Rhythm] spire " + (SpireIndex + 1) + ": local pack '" + want + "' not available, playing spire 1's track"); }
            }
            if (Chart == null)
            {
                var pack = Resources.Load<TextAsset>((StemDirOverride ?? StemSet.DefaultDir) + "/pack");
                if (pack != null)
                {
                    try { Chart = PackJson.Parse(pack.text); }
                    catch (Exception e) { Debug.LogWarning("[Rhythm] pack.json unreadable, using the built-in chart: " + e.Message); }
                }
                if (Chart == null) Chart = FallbackChart.Create();
                Stems = StemSet.Load(StemDirOverride ?? StemSet.DefaultDir, Chart);
                if (PackId == null) PackId = Chart.Id;
            }
            if (Stems.IsSynth) Debug.Log("[Rhythm] stems missing: playing the synthesised fallback loops");

            _root = new GameObject("DanceEncounter");
            if (ctx.World != null) _root.transform.SetParent(ctx.World.transform, false);
            Player = StemPlayer.Create(_root.transform, Stems);
            _sfx = EncounterSfx.Create(_root.transform);

            Circle = _request != null && _request.Stage != null ? _request.Stage.Circle : DanceCircle.Current;
            if (Circle != null && Circle.Retired && SpireDirector.Instance != null && SpireDirector.Instance.Count > 0)
                Circle = SpireDirector.Instance.Spires[0].Circle; // SP-1: the home circle is retired, Sol's dance moves to the first spire
            if (Circle == null && ctx.Sim != null)
            {
                // No camp in this scene (should not happen in the game): stand a circle up for the dance only.
                Circle = DanceCircle.Build(_root.transform, new CampLayout(ctx.Sim.State.Board));
                _ownCircle = Circle.gameObject;
            }
            Circle.ResetDance();
            // Stand at the dancer's spot facing the fire, drums 1-4 left to right in front.
            ctx.PlaceAvatar(Circle.Layout.DancerSpot, Circle.Layout.DancerYaw, ViewPitch);
            if (ctx.World != null && ctx.World.GodCam != null) { _prevPan = ctx.World.GodCam.KeyboardPan; ctx.World.GodCam.KeyboardPan = false; }
            _sol = _request != null ? null : (ctx.Npc != null ? ctx.Npc : Npc.Find(NpcIds.Host)); // Sol stays at the home camp
            if (_sol != null) { _solScale = _sol.transform.localScale; _sol.Actor?.FaceTowards(Circle.Layout.DancerSpot); }
            SpeechBubbles.ShowInEncounter = true;
            Props = DanceStageProps.Build(_root.transform, Circle);
            _seatAction = new LogSeatAction(this);
            if (_sol != null) Props.SolPoint = _sol.transform.position + Vector3.up * 1.6f;
            TouchStrips.ShapeResolver = ShapeAt; _shapeHooked = true;
            StartPhrase();
        }

        private int ShapeAt(Vector2 screen) => DanceStageProps.DrumAt(Camera.main, Circle, screen);

        /// <summary>
        /// True when the dance may draw its text overlay (score strip, key hints, judgement words, banners, choice bar):
        /// only with the HUD on (H). Without it the dance reads through the world: drums, embers, fire, dancers and
        /// Sol's speech bubble (<see cref="SolSaid"/>).
        /// </summary>
        public static bool OverlayOn => Hud.Visible;
        /// <summary>The last line Sol called out by speech bubble (block kept or missed, the bass, the ending choice).</summary>
        public string SolSaid { get; private set; }
        public int SolLines { get; private set; }
        private int _solBubble;

        private void SolSay(string text, float seconds)
        {
            SolSaid = text; SolLines++;
            if (_solBubble != 0) SpeechBubbles.Close(_solBubble);
            _solBubble = SpeechBubbles.Say(_sol, text, seconds);
        }

        public void End()
        {
            if (_solBubble != 0) SpeechBubbles.Close(_solBubble);
            _solBubble = 0;
            SpeechBubbles.ShowInEncounter = false;
            if (Player != null) Player.StopAll();
            HideLedger();
            if (_shapeHooked && TouchStrips.ShapeResolver == (System.Func<Vector2, int>)ShapeAt) TouchStrips.ShapeResolver = null;
            _shapeHooked = false;
            foreach (var c in _tuneClicks) if (c != null) c.Stop();
            if (Stems != null && Stems.Source != null && Stems.Source.StartsWith("local pack"))
            { foreach (var c in new[] { Stems.Drums, Stems.Bass, Stems.Other }) if (c != null) UnityEngine.Object.Destroy(c); }
            if (_ctx != null && _ctx.World != null && _ctx.World.GodCam != null) _ctx.World.GodCam.KeyboardPan = _prevPan;
            if (_sol != null) { _sol.transform.localScale = _solScale; _sol.Actor?.PlayIdle(); }
            if (Circle != null && _ownCircle == null) Circle.ResetDance();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (Current == this) Current = null;
        }

        /// <summary>Starts (or restarts) the phrase: new session, stems scheduled after the count-in.</summary>
        private void StartPhrase()
        {
            Attempts++;
            Session = new PhraseSession(Tuning, Chart);
            Phase = Screen.CountIn;
            LastResult = null;
            LastRouteOpened = RouteOutcome.None;
            BassJoined = false;
            _queue.Clear(); _autoQueued.Clear(); _pops.Clear(); LitAt.Clear();
            for (int i = 0; i < 5; i++) { _padDown[i] = false; _touchDown[i] = false; }
            PressesHandled = 0; JudgementsSeen = 0;
            LivePerfect = LiveGreat = LiveOk = LiveMiss = 0;
            TallyPerfect = TallyGreat = TallyOk = TallyMiss = TallyStray = TallyHoldComplete = TallyHoldPartial = 0;
            PauseState = PauseStage.None; ResumeCount = 0; _seatMachine.Cancel(); OpenCard = Card.None; _tune = null; _lastResumeCount = 0;
            if (Props != null) Props.ClearTorches();
            _banner = null;
            SolSay(Attempts > 1 ? "Again! Strike each drum as its ember lands." : "Strike each drum as its ember lands. Four beats, then we dance!", 3.2f);
            _lastCountBeat = _lastBassBeat = _lastBeatCheered = int.MinValue;
            Circle.ResetDance();
            var g = Session.CurrentGains;
            Player.SetTargets(g.Drums, g.Bass, g.Other);
            Player.SnapToTargets();
            LastGains = g;
            var clicks = new double[4];
            for (int i = 0; i < 4; i++) clicks[i] = Session.BeatToSeconds(i - 4);
            Player.Schedule(Tuning.LeadInSeconds, Session.BeatToSeconds(Tuning.PhraseBeats), clicks);
            SongTime = Player.SongTime;
            Session.Advance(SongTime);
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Injects a pad press at a song time; it is handled when the song reaches that time (or at once if past).</summary>
        public void InjectPress(int pad, double songTime) => Enqueue(pad, songTime, true);
        public void InjectRelease(int pad, double songTime) => Enqueue(pad, songTime, false);

        private void Enqueue(int pad, double t, bool press)
        {
            var e = new Input_ { T = t, Pad = pad, Press = press };
            int i = _queue.Count;
            while (i > 0 && (_queue[i - 1].T > t || (_queue[i - 1].T == t && !press && _queue[i - 1].Press))) i--;
            _queue.Insert(i, e);
        }

        /// <summary>The one path for pad presses (keys, touch strips, injected inputs, autoplayer).</summary>
        public Judgement HandlePress(int pad, double songTime)
        {
            if (Session == null || Session.IsEnded || pad < 1 || pad > 4) return null;
            _padDown[pad] = true;
            PressesHandled++;
            var j = Session.Press(pad, songTime);
            if (j == null || j.Kind == JudgementKind.Stray) Circle.Flash(pad, new Color(0.9f, 0.85f, 0.75f), 0.15f, false);
            DrainSession();
            return j;
        }

        public Judgement HandleRelease(int pad, double songTime)
        {
            if (Session == null || Session.IsEnded || pad < 1 || pad > 4) return null;
            _padDown[pad] = false;
            var j = Session.Release(pad, songTime);
            DrainSession();
            return j;
        }

        /// <summary>The ending choice: <see cref="SimRoute.Bar"/> = "Fire leap", <see cref="SimRoute.Balcony"/> = "Spirit call". False outside beats 48-60.5.</summary>
        public bool ChooseRoute(SimRoute r)
        {
            if (Session == null || Session.IsEnded) return false;
            double t = double.IsNaN(SongTime) ? Player.SongTime : SongTime;
            var before = Session.CurrentRoute;
            bool ok = Session.ChooseRoute(r, t);
            if (ok && (Session.CurrentRoute != before || !_choiceSaid)) { _choiceSaid = true; SolSay(r == SimRoute.Bar ? "The fire leap it is!" : "The spirit call it is!", 2.2f); }
            DrainSession();
            return ok;
        }

        private bool _choiceSaid;

        public bool Retry()
        {
            if (Phase != Screen.Result) return false;
            StartPhrase();
            return true;
        }

        public bool Return()
        {
            if (Phase != Screen.Result) return false;
            IsFinished = true;
            return true;
        }

        public void AdjustCalibration(double deltaMs)
        {
            Tuning.CalibrationMs = Math.Max(-250, Math.Min(250, Tuning.CalibrationMs + deltaMs));
            PlayerPrefs.SetFloat(CalibrationPref, (float)Tuning.CalibrationMs);
            PlayerPrefs.Save();
        }

        /// <summary>Sets the input offset (ms, clamped) and keeps it for the next dance and the next session.</summary>
        public void SetCalibration(double ms)
        {
            Tuning.CalibrationMs = Math.Max(-250, Math.Min(250, ms));
            PlayerPrefs.SetFloat(CalibrationPref, (float)Tuning.CalibrationMs);
            PlayerPrefs.Save();
        }

        public int CuesJudged
        {
            get
            {
                if (Session == null) return 0;
                int n = 0;
                foreach (var c in Session.AllCues) if (c.State == CueState.Done) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ frame

        public void Tick(float unscaledDt)
        {
            if (IsFinished || Session == null) return;
            bool menu = Hud.MenuOpen;
            bool blocked = menu || _menuWasOpen || Time.frameCount == _beginFrame;
            _menuWasOpen = menu;

            if (!blocked) PollTaps();
            _seatMachine.Tick(Time.unscaledTime);
            if (IsFinished) return;

            if (Phase != Screen.Result && IsPaused)
            {
                TickPaused(blocked);
            }
            else if (Phase != Screen.Result)
            {
                double now = Player.SongTime;
                SongTime = now;
                if (AutoPlayEnabled) QueueAutoplay();
                while (_queue.Count > 0 && _queue[0].T <= now && !Session.IsEnded)
                {
                    var e = _queue[0]; _queue.RemoveAt(0);
                    if (e.Press) HandlePress(e.Pad, e.T); else HandleRelease(e.Pad, e.T);
                }
                if (!blocked)
                {
                    for (int p = 1; p <= 4; p++)
                    {
                        if (Input.GetKeyDown(PadKeys[p - 1])) { _padDown[p] = true; HandlePress(p, now); }
                        if (Input.GetKeyUp(PadKeys[p - 1])) { _padDown[p] = false; HandleRelease(p, now); }
                        // Touch strips 1-4 and the drums' own shapes (TouchStrips.PadAt; the strips are also the four moves on foot).
                        if (TouchStrips.Pressed(p) && !_touchDown[p]) { _touchDown[p] = true; HandlePress(p, now); }
                        else if (_touchDown[p] && !TouchStrips.Held(p)) { _touchDown[p] = false; HandleRelease(p, now); }
                    }
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) ChooseRoute(SimRoute.Bar);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) ChooseRoute(SimRoute.Balcony);
                    if (Session.RouteWindow == RouteWindowState.Open && Input.GetMouseButtonDown(0)) ChooseByTap(Input.mousePosition);
                    if (Input.GetKeyDown(KeyCode.P)) Pause();
                }
                if (AutoRoute.HasValue && Session.RouteWindow == RouteWindowState.Open && Session.CurrentRoute != AutoRoute.Value) ChooseRoute(AutoRoute.Value);
                Session.Advance(now);
                DrainSession();
                if (Phase == Screen.CountIn && now >= 0) Phase = Screen.Playing;
                var g = Session.CurrentGains;
                Player.SetTargets(g.Drums, g.Bass, g.Other);
                LastGains = g;
            }
            else
            {
                if (_tune != null) TickTune(blocked);
                else if (!blocked)
                {
                    if (Input.GetKeyDown(KeyCode.R)) Retry();
                    else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Return();
                    else if (Input.GetKeyDown(KeyCode.Escape)) { EncounterKeys.ConsumeEscape(); Return(); }
                }
            }
            if (!blocked && _tune == null)
            {
                if (Input.GetKeyDown(KeyCode.LeftBracket)) AdjustCalibration(-10);
                if (Input.GetKeyDown(KeyCode.RightBracket)) AdjustCalibration(+10);
            }
            Animate();
            SyncLedger();
        }

        // ------------------------------------------------------------------ WU-5: world taps, pause, tally, bell, seat

        /// <summary>Tap at a screen point (pixels, origin bottom-left) as the pointer would deliver it; tests and captures use this.</summary>
        public void InjectTap(Vector2 screen) => _tapQueue.Add(screen);

        private void PollTaps()
        {
            if (Input.GetMouseButtonDown(0)) _tapQueue.Add(Input.mousePosition);
            for (int i = 0; i < Input.touchCount; i++) { var t = Input.GetTouch(i); if (t.phase == TouchPhase.Began) _tapQueue.Add(t.position); }
            if (_tapQueue.Count == 0) return;
            var taps = _tapQueue.ToArray(); _tapQueue.Clear();
            foreach (var tp in taps) { TapAt(tp); if (IsFinished) return; }
        }

        /// <summary>What a world tap hit: a drum shape or strip, Sol, a torch, the bell, the log seat, or nothing.</summary>
        public enum TapKind { Nothing, Drum, Sol, Torch, Bell, Seat }

        public TapKind TapAt(Vector2 screen)
        {
            var cam = Camera.main;
            int pad = TouchStrips.PadAt(screen);
            int torch = -1;
            var target = pad == 0 && Props != null ? Props.TargetAt(cam, screen, out torch) : DanceStageProps.Target.None;
            // A drum or strip: not a world command, except that a paused dance resumes when a drum is struck.
            if (pad > 0)
            {
                _seatMachine.TapElsewhere(Time.unscaledTime);
                if (PauseState == PauseStage.Paused) Resume();
                return TapKind.Drum;
            }
            switch (target)
            {
                case DanceStageProps.Target.Seat: TapLogSeat(); return TapKind.Seat;
                case DanceStageProps.Target.Sol: _seatMachine.TapElsewhere(Time.unscaledTime); ToggleCard(Card.Tally); return TapKind.Sol;
                case DanceStageProps.Target.Torch: _seatMachine.TapElsewhere(Time.unscaledTime); ToggleCard(Card.Progress); SolSay(ProgressText(), 4f); return TapKind.Torch;
                case DanceStageProps.Target.Bell: _seatMachine.TapElsewhere(Time.unscaledTime); TapBell(); return TapKind.Bell;
            }
            _seatMachine.TapElsewhere(Time.unscaledTime);
            if (_tune != null) { TuneTap(Player.SmoothedNow); return TapKind.Nothing; }
            if (OpenCard != Card.None) OpenCard = Card.None;
            return TapKind.Nothing;
        }

        private void ToggleCard(Card c) { OpenCard = OpenCard == c ? Card.None : c; }

        /// <summary>
        /// The log seat: during a phrase the first tap pauses (the owner's mid-song pause); paused or between phrases a tap
        /// asks to leave and the second tap confirms (stops the audio and ends the dance). Moves to the shared WorldAction later.
        /// </summary>
        public void TapLogSeat()
        {
            if (Phase != Screen.Result && !IsPaused) { Pause(); return; }
            // Preview, then confirm: the shared WU-0 state machine (a private instance while the encounter blocks the world's own).
            _seatMachine.Tap(_seatAction, Time.unscaledTime);
        }

        /// <summary>The log seat as a WU-0 world action: preview "Leave the dance?", confirm leaves. Moves to the shared WorldActions once it runs during encounters.</summary>
        private sealed class LogSeatAction : IWorldAction
        {
            private readonly RhythmPartyEncounter _r;
            public LogSeatAction(RhythmPartyEncounter r) { _r = r; }
            public string Title => "The log seat";
            public IEnumerable<string> Lines { get { yield return "Rest here, or leave the dance."; } }
            public string PreviewLine => "Leave the dance?";
            public WorldActionCheck Check() => WorldActionCheck.Allowed;
            public void ShowPreview(bool on) { _r.LeaveArmed = on; _r.SyncLedger(); }
            public bool Commit() { _r.LeaveDance(); return true; }
        }

        private void HideLedger() { if (_ledgerKey != null) { LedgerCard.Hide(); _ledgerKey = null; } }

        /// <summary>Shows the open card, or the leave question, on the shared ledger tablet; hides it when nothing is open.</summary>
        private void SyncLedger()
        {
            if (Props == null) return;
            string key; string title; Transform anchor; LedgerMode mode = LedgerMode.Inspect; string question = null; string[] lines;
            if (LeaveArmed) { key = "leave"; title = _seatAction.Title; anchor = Props.SeatAnchor; mode = LedgerMode.Preview; question = _seatAction.PreviewLine; lines = new[] { "Rest here, or leave the dance." }; }
            else if (OpenCard != Card.None)
            {
                lines = CardText.Split('\n');
                title = OpenCard == Card.Tally ? "Sol's tally" : OpenCard == Card.Progress ? "The block torches" : "The tuning bell";
                anchor = OpenCard == Card.Tally ? Props.SolAnchor : OpenCard == Card.Progress ? Props.TorchAnchor(2) : Props.BellAnchor;
                key = OpenCard + "|" + CardText;
            }
            else { HideLedger(); return; }
            if (key == _ledgerKey) return;
            _ledgerKey = key;
            var c = new LedgerContent { Title = title, Question = question };
            foreach (var l in lines) c.Lines.Add(l);
            LedgerCard.Show(c, anchor, mode);
        }

        /// <summary>Stops the audio and ends the dance at once (the confirmed exit).</summary>
        public void LeaveDance()
        {
            LeaveArmed = false; Left = true; HideLedger();
            if (Player != null) Player.StopAll();
            foreach (var c in _tuneClicks) if (c != null) c.Stop();
            _queue.Clear();
            IsFinished = true;
        }

        /// <summary>Pauses the phrase at the current song time: the audio stops, song time and the session hold still, scoring is untouched.</summary>
        public bool Pause()
        {
            if (Session == null || Session.IsEnded || Phase == Screen.Result || IsPaused || Player == null || !Player.IsScheduled) return false;
            double t = Player.SongTime;
            Session.Advance(t); DrainSession();
            Player.Pause(t);
            SongTime = t;
            PauseState = PauseStage.Paused; Pauses++;
            _seatMachine.Cancel();
            SolSay("Rest a moment. Strike a drum when you are ready and I count four.", 5f);
            return true;
        }

        /// <summary>Resumes after a count of four, one beat apart; the phrase continues exactly where it stopped.</summary>
        public bool Resume()
        {
            if (PauseState != PauseStage.Paused) return false;
            Player.Resume(4, Session.BeatToSeconds(1));
            PauseState = PauseStage.Counting; _lastResumeCount = 0; ResumeCount = 4;
            _seatMachine.Cancel();
            return true;
        }

        private void TickPaused(bool blocked)
        {
            SongTime = Player.SongTime; // frozen at the paused time until the four-count is over
            for (int p = 1; p <= 4; p++) TouchStrips.Pressed(p); // sample (and drop) strip presses while paused so none is delivered after the resume
            if (PauseState == PauseStage.Paused)
            {
                ResumeCount = 0;
                if (blocked) return;
                for (int p = 1; p <= 4; p++) if (Input.GetKeyDown(PadKeys[p - 1])) { Resume(); return; }
                if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Space)) Resume();
                return;
            }
            double beatSec = Session.BeatToSeconds(1);
            double left = Player.ResumeAtDsp - Player.ClockNow;
            int n = Mathf.Clamp(Mathf.CeilToInt((float)(left / beatSec) - 1e-4f), 1, 4);
            // The clicks are scheduled one beat apart; the number follows them (4 at the first click, 1 at the last).
            if (n != _lastResumeCount) { _lastResumeCount = n; ResumeCount = n; Circle.CountPulse(); _sol?.Actor?.PlayOneShot(ActorKeys.Clap); }
            if (Player.TryFinishResume())
            {
                PauseState = PauseStage.None; ResumeCount = 0;
                SongTime = Player.SongTime;
            }
        }

        private void TapBell()
        {
            Props.RingBell();
            if (Phase != Screen.Result) { SolSay("Tune the bell between dances, not in the middle of one.", 3f); return; }
            if (_tune != null) return;
            StartTune();
        }

        /// <summary>Starts the tap-along: eight clicks half a second apart; tap along on any drum, key or the ground. Only between phrases.</summary>
        public bool StartTune()
        {
            if (Phase != Screen.Result || _tune != null) return false;
            double now = Player.ClockNow;
            double first = now + 1.2;
            _tune = new TapAlongCalibration(first);
            OpenCard = Card.Tune;
            LastTuneMs = double.NaN;
            var hi = SynthAudio.Click(true); var lo = SynthAudio.Click(false);
            while (_tuneClicks.Count < TapAlongCalibration.ClickCount)
            {
                var go = new GameObject("Tune click " + _tuneClicks.Count); go.transform.SetParent(_root.transform, false);
                var a = go.AddComponent<AudioSource>(); a.spatialBlend = 0f; a.volume = 0.6f; a.playOnAwake = false; _tuneClicks.Add(a);
            }
            if (AudioGuard.CanWriteControls)
                for (int i = 0; i < TapAlongCalibration.ClickCount; i++)
                { _tuneClicks[i].clip = i % 4 == 0 ? hi : lo; _tuneClicks[i].PlayScheduled(_tune.ClickTimes[i]); }
            SolSay("Eight clicks. Tap along on any drum.", 3.5f);
            return true;
        }

        /// <summary>A tap-along tap at an audio-clock time.</summary>
        public bool TuneTap(double clockTime)
        {
            if (_tune == null) return false;
            bool ok = _tune.Tap(clockTime);
            if (ok) Props.RingBell();
            return ok;
        }

        private void TickTune(bool blocked)
        {
            if (!blocked)
            {
                double t = Player.SmoothedNow;
                for (int p = 1; p <= 4; p++)
                {
                    if (Input.GetKeyDown(PadKeys[p - 1])) TuneTap(t);
                    if (TouchStrips.Pressed(p)) TuneTap(t);
                }
                if (Input.GetKeyDown(KeyCode.Escape)) { EncounterKeys.ConsumeEscape(); CancelTune(); return; }
            }
            if (_tune != null && _tune.IsOver(Player.SmoothedNow)) FinishTune();
        }

        private void CancelTune() { _tune = null; if (OpenCard == Card.Tune) OpenCard = Card.None; foreach (var c in _tuneClicks) if (c != null) c.Stop(); }

        private void FinishTune()
        {
            var t = _tune; _tune = null;
            if (t.TryResult(out double ms))
            {
                SetCalibration(ms);
                LastTuneMs = Tuning.CalibrationMs;
                SolSay("Your drum runs " + SignedMs(Tuning.CalibrationMs) + " off the beat. Kept.", 4f);
            }
            else
            {
                SolSay("I only heard " + t.Matched + " taps. Ring the bell and try again.", 4f);
            }
        }

        public static string SignedMs(double ms) => (ms >= 0 ? "+" : "") + ms.ToString("0") + " ms";

        /// <summary>Sol's tally: exact judgement counts of this attempt, blocks kept and, once it is over, the result.</summary>
        public string TallyText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Perfect ").Append(TallyPerfect).Append("   Great ").Append(TallyGreat).Append("   OK ").Append(TallyOk).Append("   Miss ").Append(TallyMiss).Append('\n');
            sb.Append("Strays ").Append(TallyStray).Append("   Holds kept ").Append(TallyHoldComplete).Append(" of ").Append(TallyHoldComplete + TallyHoldPartial).Append('\n');
            int kept = 0, decided = 0;
            for (int b = 0; b < Tuning.BlockCount; b++) { var r = Session.BlockResultOf(b); if (r != null) { decided++; if (r.Passed) kept++; } }
            sb.Append("Blocks kept ").Append(kept).Append(" of ").Append(decided).Append(" judged (").Append(Tuning.BlockCount).Append(" in the phrase)");
            if (LastResult != null)
            {
                sb.Append('\n').Append(LastResult.Success ? "The dance is alive." : "The dance faltered.");
                if (LastResult.RouteOpened != RouteOutcome.None) sb.Append(' ').Append(ChoiceName(LastResult.RouteOpened == RouteOutcome.Bar ? SimRoute.Bar : SimRoute.Balcony)).Append('.');
            }
            return sb.ToString();
        }

        /// <summary>The block torches' answer: track, tempo, which block of how many, the mix stage.</summary>
        public string ProgressText()
        {
            int stage = Mathf.Clamp(Session.CurrentStage, 0, StageNames.Length - 1);
            string where = Phase == Screen.Result ? "Phrase over" : SongTime < 0 ? "Count-in" : "Block " + (Session.CurrentBlock + 1) + " of " + Tuning.BlockCount;
            return "Track " + (PackId ?? Chart.Id) + ", " + Math.Round(Chart.Bpm) + " BPM. " + where + ". Mix: " + StageNames[stage] + ".";
        }

        /// <summary>The text of the open card (for the screen and for tests).</summary>
        public string CardText
        {
            get
            {
                switch (OpenCard)
                {
                    case Card.Tally: return TallyText();
                    case Card.Progress: return ProgressText();
                    case Card.Tune:
                        return _tune != null ? "Tap along with the clicks: " + _tune.Matched + " of " + TapAlongCalibration.ClickCount + "\nOffset now " + SignedMs(Tuning.CalibrationMs)
                            : "Offset " + SignedMs(Tuning.CalibrationMs) + " (kept)";
                    default: return "";
                }
            }
        }

        /// <summary>A tap above the touch band during the choice: left of the fire = Fire leap, right = Spirit call.</summary>
        private void ChooseByTap(Vector2 screen)
        {
            if (TouchStrips.PadAt(screen) != 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (Props != null && Props.TargetAt(cam, screen, out _) != DanceStageProps.Target.None) return;
            var f = cam.WorldToScreenPoint(Circle.FireTop);
            if (f.z <= 0) return;
            ChooseRoute(screen.x < f.x ? SimRoute.Bar : SimRoute.Balcony);
        }

        private void QueueAutoplay()
        {
            foreach (var c in Session.UpcomingCues())
            {
                if (_autoQueued.Contains(c.Index)) continue;
                _autoQueued.Add(c.Index);
                if (AutoSkipBlock != null && AutoSkipBlock(c.Block)) continue;
                double cal = Tuning.CalibrationMs / 1000.0; // a perfect player's presses arrive "late" by the calibration
                InjectPress(c.Pad, c.Time + cal);
                if (c.IsHold) InjectRelease(c.Pad, c.EndTime + cal);
            }
        }

        private void DrainSession()
        {
            foreach (var ev in Session.DrainEvents())
            {
                switch (ev)
                {
                    case JudgementEvent je: OnJudgement(je.Judgement); break;
                    case BlockDecided bd:
                        if (bd.Passed)
                        {
                            BlocksCheered++;
                            _sfx.Play(EncounterSfx.CheerClip, 0.55f);
                            Circle.Cheer();
                            _sol?.Actor?.PlayOneShot(ActorKeys.Clap);
                            ShowBanner("Block " + (bd.Block + 1) + " kept the beat!", EncounterUi.Good);
                            SolSay(KeptLines[bd.Block % KeptLines.Length], 2.2f);
                        }
                        else
                        {
                            ShowBanner("Block " + (bd.Block + 1) + " missed - the dance holds back", EncounterUi.Bad);
                            SolSay(MissedLines[bd.Block % MissedLines.Length], 2.4f);
                            Circle.Falter();
                        }
                        break;
                    case BassRevealed br:
                        Player.ScheduleBassAndOther(br.SongTime);
                        BassJoined = true;
                        ShowBanner("Sol's big drum: the bass joins in!", EncounterUi.Gold, 2.2f);
                        SolSay("Hear my big drum? The bass joins in!", 2.4f);
                        break;
                    case RouteWindowOpened _:
                        ShowBanner("Last block: " + FireLeapName + " or " + SpiritCallName + "?", EncounterUi.Gold, 2.5f);
                        _choiceSaid = false;
                        SolSay("Last block! Leap the fire (left of it) or call the spirits (right)?", 4.5f);
                        break;
                    case PhraseEnded pe: OnEnded(pe.Result); break;
                }
            }
        }

        private void OnJudgement(Judgement j)
        {
            JudgementsSeen++;
            switch (j.Kind)
            {
                case JudgementKind.Perfect: TallyPerfect++; break;
                case JudgementKind.Great: TallyGreat++; break;
                case JudgementKind.Ok: TallyOk++; break;
                case JudgementKind.Miss: TallyMiss++; break;
                case JudgementKind.Stray: TallyStray++; break;
                case JudgementKind.HoldComplete: TallyHoldComplete++; break;
                case JudgementKind.HoldPartial: TallyHoldPartial++; break;
            }
            if (Props != null && j.Kind != JudgementKind.Miss && j.Kind != JudgementKind.Stray && j.Kind != JudgementKind.HoldPartial) Props.Strike(j.Pad);
            string text; Color col;
            switch (j.Kind)
            {
                case JudgementKind.Perfect: text = "Perfect"; col = new Color(1f, 0.82f, 0.25f); LivePerfect++; Circle.Flash(j.Pad, col, 1f, true); break;
                case JudgementKind.Great: text = "Great"; col = new Color(0.45f, 1f, 0.55f); LiveGreat++; Circle.Flash(j.Pad, col, 0.7f, true); break;
                case JudgementKind.Ok: text = "OK"; col = new Color(0.55f, 0.75f, 1f); LiveOk++; Circle.Flash(j.Pad, col, 0.4f, false); break;
                case JudgementKind.Miss: text = "Miss"; col = EncounterUi.Bad; LiveMiss++; Circle.Flash(j.Pad, col, 0f, false, true); break;
                case JudgementKind.HoldComplete: text = "Held!"; col = new Color(1f, 0.82f, 0.25f); Circle.Flash(j.Pad, col, 1f, true); break;
                case JudgementKind.HoldPartial: text = "Let go"; col = EncounterUi.Dim; Circle.Flash(j.Pad, col, 0f, false, true); break;
                default: return; // strays cost nothing and show nothing
            }
            _pops.Add(new Pop { Pad = j.Pad, Text = text, Color = col, Born = Time.unscaledTime });
        }

        private void OnEnded(PhraseResult r)
        {
            LastResult = r;
            LastRouteOpened = r.RouteOpened;
            Phase = Screen.Result;
            _queue.Clear();
            _endedAt = Time.unscaledTime;
            if (r.RouteOpened != RouteOutcome.None)
            {
                Circle.PlayEffect(r.RouteOpened == RouteOutcome.Bar ? DanceCircle.Effect.FireLeap : DanceCircle.Effect.SpiritCall);
                _sfx.Play(EncounterSfx.CheerClip, 0.8f);
            }
            _sol?.Actor?.PlayOneShot(r.Success ? ActorKeys.Cheer : ActorKeys.Clap);
            SolSay(r.RouteOpened == RouteOutcome.Bar ? "Over the fire they go!" : r.RouteOpened == RouteOutcome.Balcony ? "The spirits rise!" : r.Success ? "The dance is alive!" : "The beat slipped away. Again?", 3f);
        }

        private static readonly string[] KeptLines = { "That's the beat! The dancers are up.", "You kept it! More of them join.", "Yes! The whole circle moves.", "Kept! The fire roars." };
        private static readonly string[] MissedLines = { "The beat slipped. The dancers hold back.", "Lost it there. Listen for the kick.", "Slipped. The fire sinks a little.", "The last one slipped." };

        /// <summary>Seconds the ending (leap or spirits) plays before the result panel covers the fire.</summary>
        public const float EndingShowSeconds = 2.6f;
        private float _endedAt = -100f;

        private void ShowBanner(string s, Color c, float seconds = 1.6f) { _banner = s; _bannerColor = c; _bannerUntil = Time.unscaledTime + seconds; }

        public static Color PadColor(int pad, string family)
        {
            if (family == "bass") return pad <= 2 ? new Color(0.75f, 0.45f, 1.0f) : new Color(0.35f, 0.95f, 0.75f);
            return pad == 1 ? new Color(1.0f, 0.55f, 0.20f) : new Color(0.30f, 0.80f, 1.0f);
        }

        /// <summary>Per frame: cue glow and embers on the drums, count-in taps, the bass drum, the dancers and the choice marks.</summary>
        private void Animate()
        {
            double t = double.IsNaN(SongTime) ? 0 : SongTime;
            double beat = Session.SecondsToBeat(t);
            bool live = Phase != Screen.Result && t >= Session.BeatToSeconds(-4);
            float phase = (float)(beat - Math.Floor(beat));
            int wholeBeat = (int)Math.Floor(beat);

            float[] glow = new float[5]; bool[] held = new bool[5]; Color[] col = new Color[5]; float[] ring = { -1, -1, -1, -1, -1 };
            if (Phase != Screen.Result)
            {
                foreach (var c in Session.UpcomingCues())
                {
                    if (c.State == CueState.Done) continue; // judged: the flash took over
                    var pc = PadColor(c.Pad, c.Family);
                    if (c.State == CueState.Holding)
                    {
                        glow[c.Pad] = 1f; held[c.Pad] = true; col[c.Pad] = pc;
                        for (double tau = Math.Max(t, c.Time); tau <= Math.Min(c.EndTime, t + FallSeconds); tau += 0.06)
                            Circle.AddEmber(Circle.EmberPath(c.Pad, (float)DanceCircle.Approach(tau, t, FallSeconds)), 0.045f, new Color(pc.r, pc.g, pc.b, 0.8f));
                        continue;
                    }
                    double u = DanceCircle.Approach(c.Time, t, FallSeconds);
                    if (u < 0) continue;
                    if (!LitAt.ContainsKey(c.Index)) LitAt[c.Index] = (c.Pad, t);
                    float uc = Mathf.Clamp01((float)u);
                    float g = 0.25f + 0.75f * uc * uc;
                    if (g >= glow[c.Pad]) { glow[c.Pad] = g; col[c.Pad] = pc; ring[c.Pad] = uc; }
                    // The ember: head plus a short fading trail behind it.
                    for (int k = 0; k < 4; k++)
                    {
                        float uk = uc - k * 0.035f;
                        if (uk < 0) break;
                        Circle.AddEmber(Circle.EmberPath(c.Pad, uk), (0.06f + 0.06f * uk) * (1f - k * 0.2f), new Color(pc.r, pc.g, pc.b, 1f - k * 0.25f));
                    }
                    if (c.IsHold)
                        for (double tau = c.Time + 0.06; tau <= Math.Min(c.EndTime, t + FallSeconds); tau += 0.06)
                            Circle.AddEmber(Circle.EmberPath(c.Pad, (float)DanceCircle.Approach(tau, t, FallSeconds)), 0.045f, new Color(pc.r, pc.g, pc.b, 0.75f));
                }
            }
            for (int p = 1; p <= 4; p++) Circle.SetDrum(p, glow[p], col[p], held[p], ring[p]);
            if (Props != null)
            {
                // Performers: the pose follows the family of each drum's next cue from the moment it appears until it is judged.
                for (int p = 1; p <= 4; p++)
                {
                    string fam = null; double bestT = double.MaxValue;
                    if (Phase != Screen.Result)
                        foreach (var c in Session.UpcomingCues())
                        {
                            if (c.Pad != p || c.State == CueState.Done) continue;
                            if (c.State != CueState.Holding && t < c.Time - FallSeconds) continue;
                            if (c.Time < bestT) { bestT = c.Time; fam = c.Family; }
                        }
                    Props.SetPose(p, DanceStageProps.PoseForFamily(fam));
                }
                // Block torches: the playing block flickers, a kept block burns gold, a missed one is a dull ember.
                for (int i = 0; i < DanceStageProps.TorchCount; i++)
                {
                    var res = Session.BlockResultOf(i);
                    var st = res != null ? (res.Passed ? DanceStageProps.TorchState.Kept : DanceStageProps.TorchState.Missed)
                        : (Phase != Screen.Result && i == Session.CurrentBlock && t >= 0 ? DanceStageProps.TorchState.Current : DanceStageProps.TorchState.Unlit);
                    if (Props.TorchStateOf(i) != st) Props.SetTorch(i, st);
                }
            }

            // Count-in: the four clicks tap every drum; Sol beats time.
            if (live && t < 0 && wholeBeat >= -4 && wholeBeat != _lastCountBeat)
            {
                _lastCountBeat = wholeBeat;
                Circle.CountPulse();
                _sol?.Actor?.PlayOneShot(ActorKeys.Clap);
            }
            // After the bass joins, Sol's big drum sounds on every beat.
            if (live && Session.HasBassReveal && beat >= Session.BassRevealBeat && wholeBeat != _lastBassBeat && LastGains.Bass > 0.05)
            {
                _lastBassBeat = wholeBeat;
                Circle.StrikeBigDrum(0.4f + 0.6f * (float)LastGains.Bass);
            }
            Circle.SetDance(live, beat, Session.CurrentStage);
            bool window = Phase != Screen.Result && Session.RouteWindow != RouteWindowState.NotYet;
            Circle.SetChoice(window, Session.CurrentRoute == SimRoute.Bar, Session.RouteWindow == RouteWindowState.Locked, phase);

            // Sol bobs on the beat; a dancer claps every 8 beats.
            float pulse = live ? Mathf.Pow(1f - phase, 3f) : 0f;
            float squash = live ? 1f - 0.05f * pulse : 1f;
            if (_sol != null) _sol.transform.localScale = new Vector3(_solScale.x * (2f - squash), _solScale.y * squash, _solScale.z * (2f - squash));
            if (live && wholeBeat >= 0 && wholeBeat % 8 == 0 && wholeBeat != _lastBeatCheered)
            {
                _lastBeatCheered = wholeBeat;
                Circle.Clap(wholeBeat / 8);
            }
        }

        // ------------------------------------------------------------------ GUI

        public void DrawGui()
        {
            if (Session == null) return;
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            double t = double.IsNaN(SongTime) ? 0 : SongTime;
            double beat = Session.SecondsToBeat(t);

            DrawWorldReadouts();
            if (Phase == Screen.Result)
            {
                // Let the ending play in the open for a moment before the panel covers the fire.
                if (LastResult != null && LastResult.RouteOpened != RouteOutcome.None && Time.unscaledTime - _endedAt < EndingShowSeconds)
                {
                    string title = LastResult.RouteOpened == RouteOutcome.Bar ? FireLeapName + "!" : SpiritCallName + "!";
                    if (OverlayOn) EncounterUi.Shadow(new Rect(0, px(150), sw, px(90)), title, EncounterUi.Big, LastResult.RouteOpened == RouteOutcome.Bar ? new Color(1f, 0.62f, 0.25f) : new Color(0.6f, 0.78f, 1f), TextAnchor.MiddleCenter);
                }
                else if (OpenCard == Card.None) DrawResult();
                if (OverlayOn) DrawFooter();
                return;
            }
            // Everything below is the optional overlay (H): the drums, embers, fire, dancers and Sol's bubble carry the dance.
            if (!OverlayOn) { _pops.Clear(); return; }
            var cam = Camera.main;

            // Judgement words rise from the drum that was struck.
            float now = Time.unscaledTime;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var pop = _pops[i]; float age = now - pop.Born;
                if (age > 0.6f) { _pops.RemoveAt(i); continue; }
                if (cam == null) continue;
                var sp = cam.WorldToScreenPoint(Circle.DrumTop(pop.Pad) + Vector3.up * 0.9f);
                if (sp.z <= 0) continue;
                var pr = new Rect(sp.x - px(110), sh - sp.y - px(30) - age * px(70), px(220), px(44));
                EncounterUi.Shadow(pr, pop.Text, EncounterUi.Header, new Color(pop.Color.r, pop.Color.g, pop.Color.b, 1f - age / 0.6f), TextAnchor.MiddleCenter);
            }

            // Count-in.
            if (t < 0)
            {
                string n = beat < -4 ? "Get ready" : Mathf.CeilToInt((float)-beat).ToString();
                var cr = new Rect(0, px(170), sw, px(170));
                EncounterUi.Shadow(cr, n, beat < -4 ? EncounterUi.Big : EncounterUi.Huge, EncounterUi.Gold, TextAnchor.MiddleCenter);
                EncounterUi.Shadow(new Rect(0, px(340), sw, px(44)), "Strike each drum as its ember lands:  D  F  J  K, left to right", EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
            }

            DrawStrip();

            // Ending choice.
            if (Session.RouteWindow != RouteWindowState.NotYet)
            {
                var rr = EncounterUi.Fit(new Rect((sw - px(940)) / 2f, px(150), px(940), px(80)));
                EncounterUi.Panel(rr, new Color(0.05f, 0.04f, 0.03f, 0.88f));
                bool locked = Session.RouteWindow == RouteWindowState.Locked;
                EncounterUi.Label(new Rect(rr.x + px(20), rr.y, px(200), rr.height), locked ? "Chosen:" : "Choose:", EncounterUi.Header, EncounterUi.Gold);
                var leapR = new Rect(rr.x + px(220), rr.y + px(10), px(330), rr.height - px(20));
                var spiritR = new Rect(rr.x + px(580), rr.y + px(10), px(340), rr.height - px(20));
                bool leap = Session.CurrentRoute == SimRoute.Bar;
                EncounterUi.Fill(leap ? leapR : spiritR, leap ? new Color(1f, 0.6f, 0.2f, 0.95f) : new Color(0.45f, 0.65f, 1f, 0.95f));
                EncounterUi.Label(leapR, "<-  " + FireLeapName, EncounterUi.Header, leap ? Color.black : EncounterUi.Dim, TextAnchor.MiddleCenter);
                EncounterUi.Label(spiritR, SpiritCallName + "  ->", EncounterUi.Header, !leap ? Color.black : EncounterUi.Dim, TextAnchor.MiddleCenter);
            }

            if (_banner != null && now < _bannerUntil)
            {
                var br = new Rect(0, px(240), sw, px(60));
                EncounterUi.Shadow(br, _banner, EncounterUi.Header, _bannerColor, TextAnchor.MiddleCenter);
            }
            DrawFooter();
        }

        /// <summary>
        /// What taps on world objects open (Sol's tally, the torches' progress, the bell's tap-along), the pause label and
        /// the resume count. Drawn whether or not the H overlay is on: they answer a tap, they are not a permanent HUD.
        /// </summary>
        private void DrawWorldReadouts()
        {
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width;
            if (PauseState == PauseStage.Paused)
            {
                var r = EncounterUi.Fit(new Rect((sw - px(700)) / 2f, px(300), px(700), px(120)), 6);
                r = PausedRectClearOfLedger(r);
                EncounterUi.Panel(r, new Color(0.06f, 0.05f, 0.04f, 0.9f));
                EncounterUi.Label(new Rect(r.x, r.y + px(6), r.width, px(52)), "Paused", EncounterUi.Header, EncounterUi.Gold, TextAnchor.MiddleCenter);
                EncounterUi.Label(new Rect(r.x, r.y + px(60), r.width, px(50)), "Strike any drum to go on after a count of four", EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
            }
            else if (PauseState == PauseStage.Counting && ResumeCount > 0)
                EncounterUi.Shadow(new Rect(0, px(150), sw, px(170)), ResumeCount.ToString(), EncounterUi.Huge, EncounterUi.Gold, TextAnchor.MiddleCenter);
        }

        /// <summary>P2a: the "Paused" label never sits behind the open ledger tablet (Sol's tally at small sizes): it moves above, below or beside it.</summary>
        public static Rect PausedRectClearOfLedger(Rect r)
        {
            if (!LedgerCard.IsOpen) return r;
            var card = LedgerCard.CurrentLayout(Camera.main).Card;
            if (!r.Overlaps(card)) return r;
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            float top = EncounterUi.Px(150f);
            var cands = new[]
            {
                new Rect(r.x, card.yMin - r.height - 4f, r.width, r.height),
                new Rect(r.x, card.yMax + 4f, r.width, r.height),
                new Rect(card.xMin - r.width - 4f, r.y, r.width, r.height),
                new Rect(card.xMax + 4f, r.y, r.width, r.height),
            };
            foreach (var c in cands)
                if (c.yMin >= top - 1f && c.yMax <= sh && c.xMin >= 0f && c.xMax <= sw && !c.Overlaps(card)) return c;
            // no room at full size: a narrower label on the side with more room
            float roomL = card.xMin, roomR = sw - card.xMax;
            float w = Mathf.Max(EncounterUi.Px(220f), Mathf.Min(r.width, Mathf.Max(roomL, roomR) - 8f));
            return new Rect(roomL >= roomR ? Mathf.Max(0f, card.xMin - w - 4f) : card.xMax + 4f, r.y, w, r.height);
        }

        /// <summary>A slim strip at the top: blocks with PASS/MISS, the mix stage, the beat and the judgement counts.</summary>
        private void DrawStrip()
        {
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width;
            var r = new Rect(px(16), px(92), sw - px(32), px(48));
            EncounterUi.Fill(r, new Color(0.05f, 0.04f, 0.03f, 0.55f));
            float x = r.x + px(12);
            EncounterUi.Label(new Rect(x, r.y, px(330), r.height), "Dance at the fire  <size=" + (int)px(20) + ">" + (_request != null ? "Spire " + (SpireIndex + 1) : "Sol") + ", " + System.Math.Round(Chart.Bpm) + " BPM</size>", EncounterUi.Body, EncounterUi.Gold);
            x += px(340);
            for (int b = 0; b < Tuning.BlockCount; b++)
            {
                var res = Session.BlockResultOf(b);
                bool cur = b == Session.CurrentBlock && SongTime >= 0;
                var chip = new Rect(x, r.y + px(7), px(120), r.height - px(14));
                Color bg = res == null ? (cur ? new Color(1f, 0.8f, 0.3f, 0.45f) : new Color(1, 1, 1, 0.08f)) : res.Passed ? new Color(0.2f, 0.6f, 0.3f, 0.9f) : new Color(0.6f, 0.2f, 0.2f, 0.9f);
                EncounterUi.Fill(chip, bg);
                string mark = res == null ? (cur ? "playing" : "") : res.Passed ? "PASS" : "MISS";
                EncounterUi.Label(chip, (b + 1) + "  " + mark, EncounterUi.ChipSmall, Color.white, TextAnchor.MiddleCenter);
                x += px(128);
            }
            x += px(12);
            EncounterUi.Label(new Rect(x, r.y, px(220), r.height), "Mix: <b>" + StageNames[Mathf.Clamp(Session.CurrentStage, 0, StageNames.Length - 1)] + "</b>", EncounterUi.Body, EncounterUi.Text);
            int bt = Mathf.Clamp((int)Math.Floor(Session.SecondsToBeat(double.IsNaN(SongTime) ? 0 : SongTime)) + 1, 0, Tuning.PhraseBeats);
            string counts = "Beat " + (SongTime < 0 ? "-" : bt.ToString()) + "/" + Tuning.PhraseBeats + "    <color=#FFD040>Perfect " + LivePerfect + "</color>  <color=#70FF8C>Great " + LiveGreat + "</color>  <color=#8CBFFF>OK " + LiveOk + "</color>  <color=#FF6B60>Miss " + LiveMiss + "</color>";
            EncounterUi.Label(new Rect(r.x, r.y, r.width - px(12), r.height), counts, EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleRight);
        }

        private void DrawResult()
        {
            float px(float v) => EncounterUi.Px(v);
            var r = LastResult;
            if (r == null) return;
            var rect = EncounterUi.Centered(800, 540, 20);
            EncounterUi.Panel(rect, new Color(0.06f, 0.05f, 0.04f, 0.92f));
            float x = rect.x + px(30), w = rect.width - px(60), y = rect.y + px(16);
            EncounterUi.Label(new Rect(rect.x, y, rect.width, px(72)), r.Success ? "The dance is alive!" : "The dance faltered", EncounterUi.Big, r.Success ? EncounterUi.Good : EncounterUi.Gold, TextAnchor.MiddleCenter);
            y += px(80);
            EncounterUi.Label(new Rect(rect.x, y, rect.width, px(40)), "Blocks kept: <b>" + r.PassedBlocks + " / " + Tuning.BlockCount + "</b>", EncounterUi.Body, null, TextAnchor.MiddleCenter);
            y += px(50);
            float bw = px(150), gap = px(16), bx = rect.x + (rect.width - (bw * 4 + gap * 3)) / 2f;
            for (int b = 0; b < Tuning.BlockCount; b++)
            {
                var res = Session.BlockResultOf(b);
                bool pass = res != null && res.Passed;
                var br = new Rect(bx + b * (bw + gap), y, bw, px(56));
                EncounterUi.Fill(br, pass ? new Color(0.2f, 0.6f, 0.3f, 0.9f) : new Color(0.6f, 0.2f, 0.2f, 0.9f));
                EncounterUi.Label(br, (b + 1) + "  " + (pass ? "PASS" : "MISS"), EncounterUi.Chip, Color.white);
            }
            y += px(76);
            EncounterUi.Label(new Rect(rect.x, y, rect.width, px(40)),
                "Perfect " + r.Perfect + "    Great " + r.Great + "    OK " + r.Ok + "    Miss " + r.Miss + "    Holds " + r.HoldComplete + "/" + (r.HoldComplete + r.HoldPartial),
                EncounterUi.Body, null, TextAnchor.MiddleCenter);
            y += px(60);
            string ending; Color rc;
            if (r.RouteOpened == RouteOutcome.Bar) { ending = FireLeapName + ": the dancers leapt the fire!"; rc = EncounterUi.Good; }
            else if (r.RouteOpened == RouteOutcome.Balcony) { ending = SpiritCallName + ": the spirits rose from the fire."; rc = EncounterUi.Good; }
            else { ending = "The last block slipped: dance again to finish it."; rc = EncounterUi.Bad; }
            EncounterUi.Label(new Rect(rect.x + px(20), y, rect.width - px(40), px(44)), ending, EncounterUi.Body, rc, TextAnchor.MiddleCenter);
            float by = rect.yMax - px(90), hbw = (w - px(20)) / 2f;
            if (EncounterUi.KeyButton(new Rect(x, by, hbw, px(66)), "Dance again  [R]")) Retry();
            if (EncounterUi.KeyButton(new Rect(x + hbw + px(20), by, hbw, px(66)), "Return  [Enter / Esc]")) Return();
        }

        private void DrawFooter()
        {
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            var r = new Rect(px(24), sh - px(46), sw - px(48), px(36));
            string cal = (Tuning.CalibrationMs >= 0 ? "+" : "") + Tuning.CalibrationMs.ToString("0") + " ms";
            EncounterUi.Shadow(r, "Drums D F J K     Ending <- ->     Calibration: " + cal + "  ( [ -10 ms   ] +10 ms )     Esc pause", EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleLeft);
        }
    }
}
