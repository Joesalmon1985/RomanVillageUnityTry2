using System;
using System.Collections.Generic;
using Bedsit;
using UnityEngine;
using World.Sim.Encounters.Rhythm;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

namespace World.Game
{
    /// <summary>
    /// Rhythm party with DJ Sol ("rhythm_party", PRD 4.3): a 2 s count-in, then one 64-beat phrase at 174 BPM on a
    /// four-lane highway (pads D F J K). The final block offers the bar or the balcony (Left/Right); at beat 64 a passing
    /// final block opens the chosen route. Rules live in PhraseSession; audio runs on the DSP clock via StemPlayer.
    /// Keyboard, the injected-input hook and the built-in autoplayer all go through HandlePress/HandleRelease.
    /// </summary>
    public sealed class RhythmPartyEncounter : IEncounter
    {
        public enum Screen { CountIn, Playing, Result }

        /// <summary>Test hooks (null = normal): tuning, and the Resources folder of the stems (a missing folder uses SynthAudio).</summary>
        public static RhythmTuning TuningOverride;
        public static string StemDirOverride;
        public static RhythmPartyEncounter Current { get; private set; }

        public const float FallSeconds = 1.1f;
        public const float ViewYawOffset = 24f;          // RHYTHM_API: must be <= 1.2 s
        public const string CalibrationPref = "World.Rhythm.CalibrationMs";
        public static readonly KeyCode[] PadKeys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        public static readonly string[] StageNames = { "Drums", "Bass in", "Drums back", "Bass up" };

        public string Title => "Rhythm party with DJ Sol";
        public bool IsFinished { get; private set; }
        public Screen Phase { get; private set; }
        public RhythmTuning Tuning { get; private set; }
        public Chart Chart { get; private set; }
        public PhraseSession Session { get; private set; }
        public StemPlayer Player { get; private set; }
        public StemSet Stems { get; private set; }
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
        private bool _prevDoorMusic, _prevPan = true;
        private Npc _dj;
        private Vector3 _djScale = Vector3.one;
        private readonly List<ICharacterActor> _dancers = new List<ICharacterActor>();
        private readonly List<Light> _beatLights = new List<Light>();
        private int _beginFrame;
        private bool _menuWasOpen;
        private readonly float[] _laneFlash = new float[5];
        private readonly bool[] _padDown = new bool[5];
        private sealed class Pop { public int Pad; public string Text; public Color Color; public float Born; }
        private readonly List<Pop> _pops = new List<Pop>();
        private sealed class Spark { public int Pad; public float Born; public Color Color; }
        private readonly List<Spark> _sparks = new List<Spark>();
        private string _banner; private float _bannerUntil; private Color _bannerColor;
        private int _lastBeatCheered = int.MinValue;

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

            _root = new GameObject("RhythmParty");
            if (ctx.World != null) _root.transform.SetParent(ctx.World.transform, false);
            Player = StemPlayer.Create(_root.transform, Stems);
            _sfx = EncounterSfx.Create(_root.transform);

            // Stand on the dance floor facing the decks, turned a little so DJ Sol stays visible left of the note highway.
            ctx.PlaceAvatar(ctx.Stage.Position, ctx.Stage.Yaw + ViewYawOffset, -4f);
            var w = ctx.Warehouse;
            if (w != null) { _prevDoorMusic = w.IsDoorMusicOn; if (AudioGuard.CanWriteControls) w.SetDoorMusic(false); }
            if (ctx.World != null && ctx.World.GodCam != null) { _prevPan = ctx.World.GodCam.KeyboardPan; ctx.World.GodCam.KeyboardPan = false; }
            _dj = ctx.Npc != null ? ctx.Npc : Npc.Find(NpcIds.Host);
            if (_dj != null) { _djScale = _dj.transform.localScale; _dj.Actor?.FaceTowards(ctx.Stage.Position); }
            BuildCrowd(ctx);
            StartPhrase();
        }

        public void End()
        {
            if (Player != null) Player.StopAll();
            var w = _ctx != null ? _ctx.Warehouse : null;
            if (w != null)
            {
                foreach (SimRoute r in new[] { SimRoute.Bar, SimRoute.Balcony })
                    foreach (var l in w.RouteLights(ToGame(r))) if (l != null) l.enabled = w.IsRouteOpen(ToGame(r));
                if (AudioGuard.CanWriteControls) w.SetDoorMusic(_prevDoorMusic);
            }
            if (_ctx != null && _ctx.World != null && _ctx.World.GodCam != null) _ctx.World.GodCam.KeyboardPan = _prevPan;
            if (_dj != null) { _dj.transform.localScale = _djScale; _dj.Actor?.PlayIdle(); }
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (Current == this) Current = null;
        }

        private void BuildCrowd(EncounterContext ctx)
        {
            var w = ctx.Warehouse;
            if (w == null) return;
            var floor = w.DanceFloorPosition;
            var fwd = ctx.Stage.Forward;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            // Fits the 13 m wide hall (dance floor 8 x 10 m) of the resized warehouse.
            Vector3[] offs = { right * -2.6f + fwd * 3.0f, right * 1.4f + fwd * 4.0f, right * -1.8f - fwd * 2.2f, right * 2.2f - fwd * 1.8f, right * -3.8f + fwd * 0.8f, right * 3.9f - fwd * 0.6f };
            Color[] cols = { new Color(0.9f, 0.3f, 0.5f), new Color(0.3f, 0.8f, 0.6f), new Color(0.95f, 0.75f, 0.2f), new Color(0.5f, 0.4f, 0.95f), new Color(0.95f, 0.45f, 0.2f), new Color(0.3f, 0.6f, 0.95f) };
            for (int i = 0; i < offs.Length; i++)
            {
                var p = floor + offs[i];
                var a = ActorFactory.Create("dancer" + i, cols[i], p, BoardSpace.YawOf(w.DecksPosition - p), _root.transform);
                a.Root.name = "Dancer " + i;
                _dancers.Add(a);
            }
            Color[] lc = { new Color(1f, 0.2f, 0.6f), new Color(0.2f, 0.7f, 1f), new Color(1f, 0.8f, 0.2f) };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("BeatLight" + i);
                go.transform.SetParent(_root.transform, false);
                go.transform.position = floor + Vector3.up * 3.2f + right * (i - 1) * 4.5f + fwd * 1.5f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.range = 9f; l.intensity = 0f; l.color = lc[i];
                _beatLights.Add(l);
            }
        }

        /// <summary>Starts (or restarts) the phrase: new session, stems scheduled after the count-in.</summary>
        private void StartPhrase()
        {
            Attempts++;
            Session = new PhraseSession(Tuning, Chart);
            Phase = Screen.CountIn;
            LastResult = null;
            LastRouteOpened = RouteOutcome.None;
            _queue.Clear(); _autoQueued.Clear(); _pops.Clear(); _sparks.Clear();
            for (int i = 0; i < 5; i++) { _padDown[i] = false; _laneFlash[i] = 0; }
            PressesHandled = 0; JudgementsSeen = 0;
            LivePerfect = LiveGreat = LiveOk = LiveMiss = 0;
            _banner = null;
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

        /// <summary>The one path for pad presses (keys, injected inputs, autoplayer).</summary>
        public Judgement HandlePress(int pad, double songTime)
        {
            if (Session == null || Session.IsEnded || pad < 1 || pad > 4) return null;
            _laneFlash[pad] = 1f;
            _padDown[pad] = true;
            PressesHandled++;
            var j = Session.Press(pad, songTime);
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
                    }
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) ChooseRoute(SimRoute.Bar);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) ChooseRoute(SimRoute.Balcony);
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
            Animate(unscaledDt);
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
                            foreach (var d in _dancers) d.PlayOneShot(ActorKeys.Cheer);
                            _dj?.Actor?.PlayOneShot(ActorKeys.Clap);
                            ShowBanner("Block " + (bd.Block + 1) + " passed!", EncounterUi.Good);
                        }
                        else ShowBanner("Block " + (bd.Block + 1) + " missed - the mix holds", EncounterUi.Bad);
                        break;
                    case BassRevealed br:
                        Player.ScheduleBassAndOther(br.SongTime);
                        break;
                    case RouteWindowOpened _:
                        ShowBanner("Last block: choose the bar or the balcony!", EncounterUi.Gold, 2.5f);
                        break;
                    case PhraseEnded pe: OnEnded(pe.Result); break;
                }
            }
        }

        private void OnJudgement(Judgement j)
        {
            JudgementsSeen++;
            string text; Color col; bool hit = true;
            switch (j.Kind)
            {
                case JudgementKind.Perfect: text = "Perfect"; col = new Color(1f, 0.85f, 0.3f); LivePerfect++; break;
                case JudgementKind.Great: text = "Great"; col = new Color(0.45f, 1f, 0.55f); LiveGreat++; break;
                case JudgementKind.Ok: text = "OK"; col = new Color(0.5f, 0.75f, 1f); LiveOk++; break;
                case JudgementKind.Miss: text = "Miss"; col = EncounterUi.Bad; hit = false; LiveMiss++; break;
                case JudgementKind.HoldComplete: text = "Held!"; col = new Color(1f, 0.85f, 0.3f); break;
                case JudgementKind.HoldPartial: text = "Released"; col = EncounterUi.Dim; hit = false; break;
                default: return; // strays cost nothing and show nothing
            }
            _pops.Add(new Pop { Pad = j.Pad, Text = text, Color = col, Born = Time.unscaledTime });
            if (hit) _sparks.Add(new Spark { Pad = j.Pad, Born = Time.unscaledTime, Color = col });
        }

        private void OnEnded(PhraseResult r)
        {
            LastResult = r;
            LastRouteOpened = r.RouteOpened;
            Phase = Screen.Result;
            _queue.Clear();
            var w = _ctx != null ? _ctx.Warehouse : null;
            if (w != null && r.RouteOpened != RouteOutcome.None)
            {
                var route = r.RouteOpened == RouteOutcome.Bar ? World.Game.Route.Bar : World.Game.Route.Balcony;
                w.SetRouteOpen(route, true);
                _sfx.Play(EncounterSfx.CheerClip, 0.8f);
                foreach (var d in _dancers) d.PlayOneShot(ActorKeys.Cheer);
            }
            _dj?.Actor?.PlayOneShot(r.Success ? ActorKeys.Cheer : ActorKeys.Clap);
        }

        private void ShowBanner(string s, Color c, float seconds = 1.6f) { _banner = s; _bannerColor = c; _bannerUntil = Time.unscaledTime + seconds; }

        private static World.Game.Route ToGame(SimRoute r) => r == SimRoute.Bar ? World.Game.Route.Bar : World.Game.Route.Balcony;

        private void Animate(float dt)
        {
            for (int p = 1; p <= 4; p++)
            {
                _laneFlash[p] = Mathf.Max(0, _laneFlash[p] - dt * 7f);
            }
            double t = double.IsNaN(SongTime) ? 0 : SongTime;
            double beat = Session.SecondsToBeat(t);
            bool live = Phase != Screen.Result && t >= Session.BeatToSeconds(-4);
            float phase = (float)(beat - Math.Floor(beat));
            float pulse = live ? Mathf.Pow(1f - phase, 3f) : 0.15f;
            for (int i = 0; i < _beatLights.Count; i++)
            {
                var l = _beatLights[i];
                l.intensity = live ? 0.6f + 3.2f * pulse : 0.4f;
                if (live) l.color = Color.HSVToRGB(Mathf.Repeat((float)Math.Floor(beat) * 0.13f + i * 0.33f, 1f), 0.75f, 1f);
            }
            // Bob DJ and dancers on the beat (a squash on the downbeat, slight sway).
            float squash = live ? 1f - 0.05f * pulse : 1f;
            if (_dj != null) _dj.transform.localScale = new Vector3(_djScale.x * (2f - squash), _djScale.y * squash, _djScale.z * (2f - squash));
            for (int i = 0; i < _dancers.Count; i++)
            {
                var a = _dancers[i].Root;
                float s = live ? 1f - 0.07f * Mathf.Pow(1f - Mathf.Repeat(phase + i * 0.11f, 1f), 3f) : 1f;
                a.localScale = new Vector3(2f - s, s, 2f - s);
                if (live) a.localRotation = Quaternion.Euler(0, a.localRotation.eulerAngles.y + Mathf.Sin((float)beat * Mathf.PI) * 0.4f, 0);
            }
            int wholeBeat = (int)Math.Floor(beat);
            if (live && wholeBeat >= 0 && wholeBeat % 8 == 0 && wholeBeat != _lastBeatCheered && _dancers.Count > 0)
            {
                _lastBeatCheered = wholeBeat;
                _dancers[(wholeBeat / 8) % _dancers.Count].PlayOneShot(ActorKeys.Clap);
            }
            // Doorway preview of the pick during the route window.
            var w = _ctx != null ? _ctx.Warehouse : null;
            if (w != null && Phase != Screen.Result)
            {
                bool window = Session.RouteWindow != RouteWindowState.NotYet;
                foreach (SimRoute r in new[] { SimRoute.Bar, SimRoute.Balcony })
                {
                    var gr = ToGame(r);
                    if (w.IsRouteOpen(gr)) continue;
                    bool on = window && Session.CurrentRoute == r && (Session.RouteWindow == RouteWindowState.Locked || phase < 0.55f);
                    foreach (var l in w.RouteLights(gr)) if (l != null) l.enabled = on;
                }
            }
        }

        // ------------------------------------------------------------------ GUI

        private static Color PadColor(int pad, string family)
        {
            if (family == "bass") return pad <= 2 ? new Color(0.70f, 0.45f, 1.0f) : new Color(0.35f, 0.95f, 0.75f);
            return pad == 1 ? new Color(1.0f, 0.55f, 0.20f) : new Color(0.30f, 0.80f, 1.0f);
        }

        public void DrawGui()
        {
            if (Session == null) return;
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            double t = double.IsNaN(SongTime) ? 0 : SongTime;
            double beat = Session.SecondsToBeat(t);

            if (Phase == Screen.Result) { DrawResult(); DrawFooter(); return; }

            // Highway.
            float laneW = px(122), hwW = laneW * 4, top = px(150), hitY = sh - px(230);
            float hwX = (sw - hwW) / 2f;
            var hw = new Rect(hwX - px(10), top - px(10), hwW + px(20), hitY - top + px(130));
            EncounterUi.Fill(hw, new Color(0.03f, 0.02f, 0.06f, 0.78f));
            float pps = (hitY - top) / FallSeconds;
            float Y(double time) => hitY - (float)((time - t) * pps);

            for (int p = 1; p <= 4; p++)
            {
                var lane = new Rect(hwX + (p - 1) * laneW, top, laneW, hitY - top + px(10));
                float flash = Mathf.Max(_laneFlash[p], _padDown[p] ? 0.45f : 0f);
                EncounterUi.Fill(lane, new Color(1, 1, 1, 0.03f + 0.16f * flash));
                if (p > 1) EncounterUi.Fill(new Rect(lane.x, top, Mathf.Max(1, px(2)), lane.height), new Color(1, 1, 1, 0.12f));
            }
            // Beat grid: beat lines, bars brighter, blocks gold.
            for (int b = (int)Math.Floor(beat) - 1; b <= (int)Math.Ceiling(beat + Session.SecondsToBeat(FallSeconds)) + 1; b++)
            {
                if (b < -4 || b > Tuning.PhraseBeats) continue;
                float y = Y(Session.BeatToSeconds(b));
                if (y < top || y > hitY) continue;
                Color c = b % Tuning.BlockBeats == 0 ? new Color(1f, 0.8f, 0.3f, 0.55f) : b % 4 == 0 ? new Color(1, 1, 1, 0.28f) : new Color(1, 1, 1, 0.09f);
                EncounterUi.Fill(new Rect(hwX, y - px(1), hwW, Mathf.Max(1, px(b % 4 == 0 ? 3 : 2))), c);
            }
            // Notes.
            foreach (var c in Session.AllCues)
            {
                if (c.EndTime < t - 0.3 || c.Time > t + FallSeconds + 0.05) continue;
                bool hitDone = c.State == CueState.Done && c.Credit > 0 && !c.IsHold;
                if (hitDone) continue;
                bool missed = c.State == CueState.Done && c.Credit <= 0;
                var col = PadColor(c.Pad, c.Family);
                if (missed) col = new Color(0.4f, 0.4f, 0.45f, 0.7f);
                float lx = hwX + (c.Pad - 1) * laneW;
                if (c.IsHold)
                {
                    double headT = c.State == CueState.Holding ? t : c.Time;
                    if (c.State == CueState.Done && c.Credit > 0) continue;
                    float yTail = Mathf.Max(top, Y(c.EndTime)), yHead = Mathf.Min(hitY + px(40), Y(headT));
                    if (yHead > yTail)
                    {
                        var body = new Color(col.r, col.g, col.b, c.State == CueState.Holding ? 0.85f : 0.55f);
                        EncounterUi.Fill(new Rect(lx + laneW * 0.28f, yTail, laneW * 0.44f, yHead - yTail), body);
                    }
                    if (c.State != CueState.Holding) DrawNote(lx, Y(c.Time), laneW, col);
                    else DrawNote(lx, hitY, laneW, Color.Lerp(col, Color.white, 0.5f));
                }
                else DrawNote(lx, Y(c.Time), laneW, col);
            }
            // Hit line and pads.
            EncounterUi.Fill(new Rect(hwX - px(6), hitY - px(3), hwW + px(12), px(6)), new Color(1f, 0.85f, 0.4f, 0.95f));
            string[] keys = { "D", "F", "J", "K" };
            for (int p = 1; p <= 4; p++)
            {
                var pr = new Rect(hwX + (p - 1) * laneW + px(10), hitY + px(18), laneW - px(20), px(74));
                bool down = _padDown[p] || _laneFlash[p] > 0.5f;
                EncounterUi.Fill(pr, down ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(0.15f, 0.13f, 0.2f, 0.95f));
                EncounterUi.Label(pr, keys[p - 1], EncounterUi.Big, down ? Color.black : EncounterUi.Text, TextAnchor.MiddleCenter);
            }
            // Sparks and judgement pops.
            float now = Time.unscaledTime;
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i]; float age = now - s.Born;
                if (age > 0.3f) { _sparks.RemoveAt(i); continue; }
                float cx = hwX + (s.Pad - 0.5f) * laneW, u = age / 0.3f;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f + 0.3f;
                    float r = px(18) + u * px(70);
                    float sz = px(10) * (1f - u) + 1;
                    EncounterUi.Fill(new Rect(cx + Mathf.Cos(a) * r - sz / 2, hitY + Mathf.Sin(a) * r * 0.6f - sz / 2, sz, sz), new Color(s.Color.r, s.Color.g, s.Color.b, 1f - u));
                }
            }
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var pop = _pops[i]; float age = now - pop.Born;
                if (age > 0.6f) { _pops.RemoveAt(i); continue; }
                var pr = new Rect(hwX + (pop.Pad - 1) * laneW - px(30), hitY - px(80) - age * px(70), laneW + px(60), px(44));
                EncounterUi.Shadow(pr, pop.Text, EncounterUi.Header, new Color(pop.Color.r, pop.Color.g, pop.Color.b, 1f - age / 0.6f), TextAnchor.MiddleCenter);
            }

            // Count-in.
            if (t < 0)
            {
                string n = beat < -4 ? "Get ready" : Mathf.CeilToInt((float)-beat).ToString();
                var cr = new Rect(hwX, top + px(120), hwW, px(200));
                EncounterUi.Shadow(cr, n, beat < -4 ? EncounterUi.Big : EncounterUi.Huge, EncounterUi.Gold, TextAnchor.MiddleCenter);
                EncounterUi.Shadow(new Rect(hwX - px(100), top + px(330), hwW + px(200), px(40)), "Hit D F J K as the notes reach the line", EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleCenter);
            }

            DrawLeftPanel(new Rect(px(24), top, px(380), px(520)));
            DrawRightPanel(new Rect(sw - px(24) - px(380), top, px(380), px(320)), beat);

            // Route prompt.
            if (Session.RouteWindow != RouteWindowState.NotYet)
            {
                var rr = EncounterUi.Fit(new Rect((sw - px(900)) / 2f, px(52), px(900), px(86)));
                EncounterUi.Panel(rr, new Color(0.05f, 0.04f, 0.10f, 0.92f));
                bool locked = Session.RouteWindow == RouteWindowState.Locked;
                EncounterUi.Label(new Rect(rr.x + px(20), rr.y, px(200), rr.height), locked ? "Locked:" : "Choose:", EncounterUi.Header, EncounterUi.Gold);
                var barR = new Rect(rr.x + px(220), rr.y + px(10), px(300), rr.height - px(20));
                var balR = new Rect(rr.x + px(560), rr.y + px(10), px(320), rr.height - px(20));
                bool bar = Session.CurrentRoute == SimRoute.Bar;
                EncounterUi.Fill(bar ? barR : balR, new Color(1f, 0.78f, 0.3f, 0.95f));
                EncounterUi.Label(barR, "<-  Bar", EncounterUi.Header, bar ? Color.black : EncounterUi.Dim, TextAnchor.MiddleCenter);
                EncounterUi.Label(balR, "Balcony  ->", EncounterUi.Header, !bar ? Color.black : EncounterUi.Dim, TextAnchor.MiddleCenter);
            }
            else
            {
                var tr = EncounterUi.Fit(new Rect((sw - px(900)) / 2f, px(60), px(900), px(64)));
                EncounterUi.Shadow(tr, "Rhythm party  -  DJ Sol, 174 BPM", EncounterUi.Header, EncounterUi.Gold, TextAnchor.MiddleCenter);
            }

            if (_banner != null && now < _bannerUntil)
            {
                var br = new Rect(hwX - px(200), top + px(40), hwW + px(400), px(60));
                EncounterUi.Shadow(br, _banner, EncounterUi.Header, _bannerColor, TextAnchor.MiddleCenter);
            }
            DrawFooter();
        }

        private static void DrawNote(float lx, float y, float laneW, Color col)
        {
            float px(float v) => EncounterUi.Px(v);
            var r = new Rect(lx + px(8), y - px(15), laneW - px(16), px(30));
            EncounterUi.Fill(r, col);
            EncounterUi.Fill(new Rect(r.x, r.y, r.width, px(6)), Color.Lerp(col, Color.white, 0.6f));
        }

        private void DrawLeftPanel(Rect r)
        {
            float px(float v) => EncounterUi.Px(v);
            if (r.x < px(8)) r.x = px(8);
            EncounterUi.Panel(r);
            float x = r.x + px(18), w = r.width - px(36), y = r.y + px(12);
            EncounterUi.Label(new Rect(x, y, w, px(40)), "Blocks", EncounterUi.Header);
            y += px(48);
            for (int b = 0; b < Tuning.BlockCount; b++)
            {
                var row = new Rect(x, y, w, px(52));
                var res = Session.BlockResultOf(b);
                bool cur = b == Session.CurrentBlock && SongTime >= 0;
                EncounterUi.Fill(row, cur ? new Color(1f, 0.8f, 0.3f, 0.22f) : new Color(1, 1, 1, 0.05f));
                string fam = Session.FamilyOfBlock(b);
                EncounterUi.Label(new Rect(row.x + px(12), row.y, px(200), row.height), "Block " + (b + 1) + (fam != null ? "  <size=" + (int)px(20) + ">" + (fam == "bass" ? "bass" : "drums") + "</size>" : ""), EncounterUi.Body);
                string mark = res == null ? (cur ? "playing" : "") : res.Passed ? "PASS" : "MISS";
                Color mc = res == null ? EncounterUi.Dim : res.Passed ? EncounterUi.Good : EncounterUi.Bad;
                EncounterUi.Label(new Rect(row.x, row.y, row.width - px(12), row.height), "<b>" + mark + "</b>", EncounterUi.Body, mc, TextAnchor.MiddleRight);
                y += px(58);
            }
            y += px(10);
            EncounterUi.Label(new Rect(x, y, w, px(40)), "Mix", EncounterUi.Header);
            y += px(46);
            int stage = Session.CurrentStage;
            for (int s = 0; s < StageNames.Length; s++)
            {
                var row = new Rect(x, y, w, px(40));
                bool on = s == stage;
                if (on) EncounterUi.Fill(row, new Color(0.5f, 0.35f, 1f, 0.35f));
                EncounterUi.Label(new Rect(row.x + px(12), row.y, row.width, row.height), (on ? "<b>" : "") + StageNames[s] + (on ? "</b>" : ""), EncounterUi.Body, on ? Color.white : EncounterUi.Dim);
                y += px(44);
            }
        }

        private void DrawRightPanel(Rect r, double beat)
        {
            float px(float v) => EncounterUi.Px(v);
            if (r.xMax > UnityEngine.Screen.width - px(8)) r.x = UnityEngine.Screen.width - px(8) - r.width;
            EncounterUi.Panel(r);
            float x = r.x + px(18), w = r.width - px(36), y = r.y + px(12);
            int b = Mathf.Clamp((int)Math.Floor(beat) + 1, 0, Tuning.PhraseBeats);
            EncounterUi.Label(new Rect(x, y, w, px(40)), "Beat", EncounterUi.Header);
            EncounterUi.Label(new Rect(x, y, w, px(40)), (beat < 0 ? "-" : b.ToString()) + " / " + Tuning.PhraseBeats, EncounterUi.Header, EncounterUi.Text, TextAnchor.MiddleRight);
            y += px(52);
            string[] names = { "Perfect", "Great", "OK", "Miss" };
            int[] vals = { LivePerfect, LiveGreat, LiveOk, LiveMiss };
            Color[] cols = { new Color(1f, 0.85f, 0.3f), new Color(0.45f, 1f, 0.55f), new Color(0.5f, 0.75f, 1f), EncounterUi.Bad };
            for (int i = 0; i < 4; i++)
            {
                EncounterUi.Label(new Rect(x, y, w, px(40)), names[i], EncounterUi.Body, cols[i]);
                EncounterUi.Label(new Rect(x, y, w, px(40)), vals[i].ToString(), EncounterUi.Body, EncounterUi.Text, TextAnchor.MiddleRight);
                y += px(42);
            }
            y += px(8);
            EncounterUi.Label(new Rect(x, y, w, px(34)), Stems.IsSynth ? "Music: synth fallback" : "Music: Lv1 Warehouse", EncounterUi.Hint);
        }

        private void DrawResult()
        {
            float px(float v) => EncounterUi.Px(v);
            var r = LastResult;
            if (r == null) return;
            var rect = EncounterUi.Centered(800, 540, 20);
            EncounterUi.Panel(rect, new Color(0.05f, 0.04f, 0.08f, 0.94f));
            float x = rect.x + px(30), w = rect.width - px(60), y = rect.y + px(16);
            EncounterUi.Label(new Rect(rect.x, y, rect.width, px(72)), r.Success ? "Party success!" : "Not quite a party", EncounterUi.Big, r.Success ? EncounterUi.Good : EncounterUi.Gold, TextAnchor.MiddleCenter);
            y += px(80);
            EncounterUi.Label(new Rect(rect.x, y, rect.width, px(40)), "Blocks passed: <b>" + r.PassedBlocks + " / " + Tuning.BlockCount + "</b>", EncounterUi.Body, null, TextAnchor.MiddleCenter);
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
            string route; Color rc;
            if (r.RouteOpened == RouteOutcome.Bar) { route = "The bar is open - walk in through the left doorway."; rc = EncounterUi.Good; }
            else if (r.RouteOpened == RouteOutcome.Balcony) { route = "The balcony is open - take the ramp on the right."; rc = EncounterUi.Good; }
            else { route = "Final block missed: stay on the floor and try again."; rc = EncounterUi.Bad; }
            EncounterUi.Label(new Rect(rect.x + px(20), y, rect.width - px(40), px(44)), route, EncounterUi.Body, rc, TextAnchor.MiddleCenter);
            float by = rect.yMax - px(90), hbw = (w - px(20)) / 2f;
            if (EncounterUi.KeyButton(new Rect(x, by, hbw, px(66)), "Retry  [R]")) Retry();
            if (EncounterUi.KeyButton(new Rect(x + hbw + px(20), by, hbw, px(66)), "Return  [Enter / Esc]")) Return();
        }

        private void DrawFooter()
        {
            float px(float v) => EncounterUi.Px(v);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            var r = new Rect(px(24), sh - px(46), sw - px(48), px(36));
            string cal = (Tuning.CalibrationMs >= 0 ? "+" : "") + Tuning.CalibrationMs.ToString("0") + " ms";
            EncounterUi.Shadow(r, "Pads D F J K     Route <- ->     Calibration: " + cal + "  ( [ -10 ms   ] +10 ms )     Tab camera     Esc pause", EncounterUi.Hint, EncounterUi.Text, TextAnchor.MiddleLeft);
        }
    }
}
