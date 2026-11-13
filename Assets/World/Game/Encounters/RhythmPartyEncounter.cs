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

        /// <summary>When each cue's drum first lit this attempt: cue index -> (pad, song time). For tests and captures.</summary>
        public readonly Dictionary<int, (int Pad, double SongTime)> LitAt = new Dictionary<int, (int, double)>();

        // Built-in autoplayer (AutoPlay script, captures, tests): presses exactly at cue times through the same path as keys.
        public bool AutoPlayEnabled;
        public Func<int, bool> AutoSkipBlock;
        public SimRoute? AutoRoute;

        private struct Input_ { public double T; public int Pad; public bool Press; }
        private readonly List<Input_> _queue = new List<Input_>();
        private readonly HashSet<int> _autoQueued = new HashSet<int>();

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
            var pack = Resources.Load<TextAsset>((StemDirOverride ?? StemSet.DefaultDir) + "/pack");
            Chart = null;
            if (pack != null)
            {
                try { Chart = PackJson.Parse(pack.text); }
                catch (Exception e) { Debug.LogWarning("[Rhythm] pack.json unreadable, using the built-in chart: " + e.Message); }
            }
            if (Chart == null) Chart = FallbackChart.Create();
            Stems = StemSet.Load(StemDirOverride ?? StemSet.DefaultDir, Chart);
            if (Stems.IsSynth) Debug.Log("[Rhythm] stems missing: playing the synthesised fallback loops");

            _root = new GameObject("DanceEncounter");
            if (ctx.World != null) _root.transform.SetParent(ctx.World.transform, false);
            Player = StemPlayer.Create(_root.transform, Stems);
            _sfx = EncounterSfx.Create(_root.transform);

            Circle = DanceCircle.Current;
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
            _sol = ctx.Npc != null ? ctx.Npc : Npc.Find(NpcIds.Host);
            if (_sol != null) { _solScale = _sol.transform.localScale; _sol.Actor?.FaceTowards(Circle.Layout.DancerSpot); }
            StartPhrase();
        }

        public void End()
        {
            if (Player != null) Player.StopAll();
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
            _banner = null;
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
            bool ok = Session.ChooseRoute(r, t);
            DrainSession();
            return ok;
        }

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

            if (Phase != Screen.Result)
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
                        // Touch strips 1-4 (P's TouchStrips; the same strips are the four moves on foot).
                        if (TouchStrips.Pressed(p) && !_touchDown[p]) { _touchDown[p] = true; HandlePress(p, now); }
                        else if (_touchDown[p] && !TouchStrips.Held(p)) { _touchDown[p] = false; HandleRelease(p, now); }
                    }
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) ChooseRoute(SimRoute.Bar);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) ChooseRoute(SimRoute.Balcony);
                    if (Session.RouteWindow == RouteWindowState.Open && Input.GetMouseButtonDown(0)) ChooseByTap(Input.mousePosition);
                }
                if (AutoRoute.HasValue && Session.RouteWindow == RouteWindowState.Open && Session.CurrentRoute != AutoRoute.Value) ChooseRoute(AutoRoute.Value);
                Session.Advance(now);
                DrainSession();
                if (Phase == Screen.CountIn && now >= 0) Phase = Screen.Playing;
                var g = Session.CurrentGains;
                Player.SetTargets(g.Drums, g.Bass, g.Other);
                LastGains = g;
            }
            else if (!blocked)
            {
                if (Input.GetKeyDown(KeyCode.R)) Retry();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Return();
                else if (Input.GetKeyDown(KeyCode.Escape)) { EncounterKeys.ConsumeEscape(); Return(); }
            }
            if (!blocked)
            {
                if (Input.GetKeyDown(KeyCode.LeftBracket)) AdjustCalibration(-10);
                if (Input.GetKeyDown(KeyCode.RightBracket)) AdjustCalibration(+10);
            }
            Animate();
        }

        /// <summary>A tap above the touch band during the choice: left of the fire = Fire leap, right = Spirit call.</summary>
        private void ChooseByTap(Vector2 screen)
        {
            if (TouchStrips.StripAt(screen) != 0) return;
            var cam = Camera.main;
            if (cam == null) return;
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
                        }
                        else ShowBanner("Block " + (bd.Block + 1) + " missed - the dance holds back", EncounterUi.Bad);
                        break;
                    case BassRevealed br:
                        Player.ScheduleBassAndOther(br.SongTime);
                        BassJoined = true;
                        ShowBanner("Sol's big drum: the bass joins in!", EncounterUi.Gold, 2.2f);
                        break;
                    case RouteWindowOpened _:
                        ShowBanner("Last block: " + FireLeapName + " or " + SpiritCallName + "?", EncounterUi.Gold, 2.5f);
                        break;
                    case PhraseEnded pe: OnEnded(pe.Result); break;
                }
            }
        }

        private void OnJudgement(Judgement j)
        {
            JudgementsSeen++;
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
        }

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

            if (Phase == Screen.Result)
            {
                // Let the ending play in the open for a moment before the panel covers the fire.
                if (LastResult != null && LastResult.RouteOpened != RouteOutcome.None && Time.unscaledTime - _endedAt < EndingShowSeconds)
                {
                    string title = LastResult.RouteOpened == RouteOutcome.Bar ? FireLeapName + "!" : SpiritCallName + "!";
                    EncounterUi.Shadow(new Rect(0, px(150), sw, px(90)), title, EncounterUi.Big, LastResult.RouteOpened == RouteOutcome.Bar ? new Color(1f, 0.62f, 0.25f) : new Color(0.6f, 0.78f, 1f), TextAnchor.MiddleCenter);
                }
                else DrawResult();
                DrawFooter();
                return;
            }
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

        /// <summary>A slim strip at the top: blocks with PASS/MISS, the mix stage, the beat and the judgement counts.</summary>
        private void DrawStrip()
        {
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width;
            var r = new Rect(px(16), px(92), sw - px(32), px(48));
            EncounterUi.Fill(r, new Color(0.05f, 0.04f, 0.03f, 0.55f));
            float x = r.x + px(12);
            EncounterUi.Label(new Rect(x, r.y, px(330), r.height), "Dance at the fire  <size=" + (int)px(20) + ">Sol, 174 BPM</size>", EncounterUi.Body, EncounterUi.Gold);
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
