using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// The shared first-time tutorial runner. One <see cref="Tutorial"/> per game (hex board, plague, council, rhythm, dungeon,
    /// duel); each is a list of steps, a tutor who is a person already in that place, a short line in a speech bubble, a pulsing
    /// ring on what to tap, and a condition read from the game that ends the step. The director only talks and highlights: it
    /// never sends a command and never blocks the game (the sim keeps its own rules). A step whose condition is already true when
    /// it comes up is skipped, so doing things out of order just moves the tutorial on. "Skip" in the bubble ends the game's
    /// tutorial for good; finished and skipped tutorials are remembered per game (<see cref="TutorialStore"/>) and can be played
    /// again from the Esc menu ("Show me how again"). The Esc menu is a system menu; nothing here is a game menu.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Instance { get; private set; }
        /// <summary>Tutorials run only while this is true. Tests switch it off (<c>TutorialTestSetup</c>) and turn it on in <c>TutorialTests</c>.</summary>
        public static bool Enabled = true;
        public const float OutroSeconds = 5f;
        /// <summary>In a replay a step that is already true is read for this long before it goes on.</summary>
        public const float ReplayReadSeconds = 4f;
        /// <summary>Tests: scales every reading time (a step's minimum, an automatic step, the outro) so a playthrough takes seconds.</summary>
        public static float TimeScale = 1f;

        public readonly List<Tutorial> Games = new List<Tutorial>();
        public Tutorial Active { get; private set; }
        /// <summary>The line on the bubble now (null when nothing shows).</summary>
        public string ShownLine { get; private set; }
        public string ShownTutor { get; private set; }
        public string ShownStepId { get; private set; }
        /// <summary>Screen rects (GUI coordinates) from the last draw: the bubble, its Skip reply, the ring.</summary>
        public Rect BubbleRect { get; private set; }
        public Rect SkipRect { get; private set; }
        public Rect RingRect { get; private set; }
        public bool RingShown { get; private set; }
        public bool BubbleShown { get; private set; }
        public bool TipShown => _tip != null;
        /// <summary>Games finished or skipped in this session, by id (tests).</summary>
        public readonly List<string> Ended = new List<string>();

        private float _shownAt = -1f, _outroAt = -1f;
        private bool _outro;
        private TutorialTip _tip; private float _tipAt;
        private GameClock _clock;
        private GUIStyle _text, _name, _skip, _step;
        private int _stylesFor = -1;
        private Texture2D _tail;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate() { Ensure(); }

        public static TutorialDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Tutorials");
            DontDestroyOnLoad(go);
            return go.AddComponent<TutorialDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Games.Add(BoardTutorial.Create());
            Games.Add(PlagueTutorial.Create());
            Games.Add(CouncilTutorial.Create());
            Games.Add(RhythmTutorial.Create());
            Games.Add(DungeonTutorial.Create());
            Games.Add(DuelTutorial.Create());
            WorldActions.Machine.Changed += OnAction;
            LedgerCard.Changed += OnLedger;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            WorldActions.Machine.Changed -= OnAction;
            LedgerCard.Changed -= OnLedger;
            if (_clock != null) _clock.Ticked -= OnTicked;
        }

        public Tutorial Game(string id) { foreach (var g in Games) if (g.Id == id) return g; return null; }

        // ------------------------------------------------------------------ control

        /// <summary>Ends the running tutorial for good ("Skip" in the bubble): remembered as done.</summary>
        public void Skip()
        {
            var t = Active;
            if (t == null) return;
            Finish(t, true);
        }

        /// <summary>The Esc menu's "Show me how again": forgets that the game's tutorial was done and starts it when the player is in that place.</summary>
        public void Replay(string id)
        {
            var t = Game(id);
            if (t == null) return;
            if (Active != null && Active != t) Stop(Active);
            TutorialStore.SetDone(id, false);
            if (Active == t) Stop(t);
            t.Reset();
            t.Replay = true;
            _replayWanted = id;
        }

        private string _replayWanted;
        private WorldBootstrap _bootSeen;

        /// <summary>Forgets the progress of every tutorial that is not finished (a new world, tests). Done flags in <see cref="TutorialStore"/> are kept.</summary>
        public void ResetProgress()
        {
            if (Active != null) Stop(Active);
            foreach (var g in Games) { g.Finished = false; g.Replay = false; g.Running = false; g.Index = 0; g.Steps.Clear(); g.Tips.Clear(); g.Flags.Clear(); g.Counters.Clear(); g.Inspected.Clear(); g.Committed.Clear(); g.Previewed.Clear(); }
            Ended.Clear();
        }

        private void Stop(Tutorial t)
        {
            t.Running = false;
            if (Active == t) Active = null;
            _outro = false; _tip = null; ClearShown();
        }

        private void Finish(Tutorial t, bool skipped)
        {
            TutorialStore.SetDone(t.Id, true);
            t.Finished = true; t.Replay = false;
            Ended.Add(t.Id + (skipped ? ":skipped" : ":done"));
            Stop(t);
        }

        private void ClearShown() { ShownLine = null; ShownTutor = null; ShownStepId = null; BubbleShown = false; RingShown = false; }

        /// <summary>A tap or click at a GUI point (top-left origin): true when it landed on the Skip reply (and skipped).</summary>
        public bool TapAt(Vector2 gui)
        {
            if (Active == null || !BubbleShown || !SkipRect.Contains(gui)) return false;
            Skip();
            return true;
        }

        /// <summary>True when a GUI point is over the bubble (so a press there does not reach the world).</summary>
        public static bool Contains(Vector2 gui)
        {
            var d = Instance;
            return d != null && d.BubbleShown && d.BubbleRect.Contains(gui);
        }

        // ------------------------------------------------------------------ hooks

        private void OnAction(WorldActionMachine m)
        {
            var t = Active;
            if (t == null || !t.Running || m.Current == null) return;
            string title = m.Current.Title ?? "";
            if (m.State == WorldActionState.Confirm && m.LastCommitOk) t.Committed.Add(title);
            t.OnAction?.Invoke(t, m);
        }

        /// <summary>Every ledger tablet that opens, from the shared machine or a private one (the duel's, the dance's): what was read, what was previewed.</summary>
        private void OnLedger()
        {
            var t = Active;
            if (t == null || !t.Running) return;
            var c = LedgerCard.Content;
            if (c == null) return;
            string title = c.Title ?? "";
            if (LedgerCard.Mode == LedgerMode.Preview) t.Previewed.Add(title);
            else t.Inspected.Add(title);
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            var t = Active;
            if (t == null || !t.Running || t.OnSim == null) return;
            foreach (var e in events) t.OnSim(t, e);
        }

        private void EnsureClock()
        {
            var b = WorldBootstrap.Instance;
            var c = b != null ? b.Clock : null;
            if (c == _clock) return;
            if (_clock != null) _clock.Ticked -= OnTicked;
            _clock = c;
            if (_clock != null) _clock.Ticked += OnTicked;
        }

        // ------------------------------------------------------------------ the loop

        private void Update()
        {
            if (!Enabled) { if (Active != null) Stop(Active); return; }
            EnsureClock();
            if (WorldBootstrap.Instance == null) { ClearShown(); return; }
            if (WorldBootstrap.Instance != _bootSeen) { _bootSeen = WorldBootstrap.Instance; ResetProgress(); } // a new world: the steps start over (what is remembered as done stays)

            // Away from its place a tutorial waits; another game's tutorial may start meanwhile (its progress is kept).
            if (Active != null && !Safe(Active.GameActive))
            {
                var paused = Active;
                if (paused.OnUpdate != null) { try { paused.OnUpdate(paused); } catch (Exception) { } }
                foreach (var t in Games)
                {
                    if (t == paused || TutorialStore.IsDone(t.Id) || t.Finished) continue;
                    if (!Safe(t.GameActive) || !Safe(t.CanStart ?? t.GameActive)) continue;
                    Suspend(paused);
                    break;
                }
            }

            if (Active == null)
            {
                foreach (var t in Games)
                {
                    if (TutorialStore.IsDone(t.Id) || t.Finished) continue;
                    if (!Safe(t.GameActive) || !Safe(t.CanStart ?? t.GameActive)) continue;
                    if (_replayWanted == t.Id) _replayWanted = null;
                    Begin(t);
                    break;
                }
                if (Active == null) { ClearShown(); return; }
            }

            PollSkipPress();
            Tick(Active);
        }

        private static bool Safe(Func<bool> f) { try { return f == null || f(); } catch (Exception) { return false; } }

        private void Suspend(Tutorial t)
        {
            t.Running = false;
            if (Active == t) Active = null;
            _outro = false; _tip = null; ClearShown();
        }

        private void Begin(Tutorial t)
        {
            if (t.Steps.Count == 0 || t.Index == 0) t.Reset();
            t.Running = true;
            Active = t;
            _shownAt = -1f; _outro = false; _outroAt = -1f; _tip = null;
            t.OnBegin?.Invoke(t);
        }

        private void PollSkipPress()
        {
            if (!BubbleShown) return;
            Vector2 pos; bool pressed;
            if (Input.touchCount > 0) { var tc = Input.GetTouch(0); pos = tc.position; pressed = tc.phase == TouchPhase.Began; }
            else { pos = Input.mousePosition; pressed = Input.GetMouseButtonDown(0); }
            if (pressed) TapAt(new Vector2(pos.x, Screen.height - pos.y));
        }

        private void Tick(Tutorial t)
        {
            if (t.OnUpdate != null) { try { t.OnUpdate(t); } catch (Exception) { } }
            if (!Safe(t.GameActive)) { ClearShown(); _shownAt = -1f; return; } // away from the place: paused where it is
            float now = Time.unscaledTime;
            if (Hud.MenuOpen) return;

            // Tips (a 7, an envoy) come first, outside the order.
            if (_tip != null)
            {
                bool resolved = _tip.Resolved != null && Safe(_tip.Resolved);
                if (resolved || now - _tipAt > _tip.Seconds * TimeScale) { _tip = null; _shownAt = -1f; }
            }
            if (_tip == null && !_outro)
                foreach (var tip in t.Tips)
                {
                    if (tip.Shown) continue;
                    bool trig = false; try { trig = tip.Trigger != null && tip.Trigger(); } catch (Exception) { }
                    if (!trig) continue;
                    tip.Shown = true; _tip = tip; _tipAt = now; break;
                }

            if (_outro)
            {
                if (now - _outroAt >= OutroSeconds * TimeScale) { Finish(t, false); return; }
                Show("outro", t.OutroTutorName, t.OutroSpeaker, t.OutroTutorRect, t.Outro, null);
                return;
            }

            // Advance over steps whose condition already holds (done out of order, or done right now).
            int guard = 0;
            while (guard++ < 40)
            {
                var s = t.Current;
                if (s == null) break;
                bool done;
                if (s.AutoSeconds > 0f) done = _shownAt >= 0f && now - _shownAt >= s.AutoSeconds * TimeScale && _tip == null;
                else done = s.Done != null && Safe(s.Done);
                if (!done) break;
                if (t.Replay && _shownAt < 0f) break; // a replay shows every step, even one that already holds
                float need = (_doneAtShow ? ReplayReadSeconds : s.MinSeconds) * TimeScale;
                if (_shownAt >= 0f && now - _shownAt < need) break; // it was said: let it be read
                t.Index++; _shownAt = -1f; _entered = null; _doneAtShow = false;
            }
            var step = t.Current;
            if (step == null)
            {
                if (_tip != null) { Show(_tip.Id, _tip.TutorName, _tip.Speaker, _tip.TutorRect, _tip.Line, _tip.Target); return; } // a tip is said before the outro
                if (string.IsNullOrEmpty(t.Outro)) { Finish(t, false); return; }
                _outro = true; _outroAt = now; return;
            }
            if (_shownAt < 0f)
            {
                _shownAt = now;
                _doneAtShow = t.Replay && step.AutoSeconds <= 0f && step.Done != null && Safe(step.Done);
            }
            if (!ReferenceEquals(_entered, step)) { _entered = step; try { step.OnEnter?.Invoke(); } catch (Exception) { } }
            if (_tip != null) Show(_tip.Id, _tip.TutorName, _tip.Speaker, _tip.TutorRect, _tip.Line, _tip.Target);
            else
            {
                string line = step.Line;
                if (step.LineFn != null) { try { line = step.LineFn(); } catch (Exception) { } }
                Show(step.Id, step.TutorName, step.Speaker, step.TutorRect, line, step.Target);
            }
        }

        private TutorialStep _entered;
        private bool _doneAtShow;

        // ------------------------------------------------------------------ what is shown

        private string _line, _tutor, _id; private Func<ISpeaker> _speakerFn; private Func<Rect?> _tutorRectFn, _targetFn;

        private void Show(string id, string tutor, Func<ISpeaker> speaker, Func<Rect?> tutorRect, string line, Func<Rect?> target)
        {
            _id = id; _tutor = tutor; _speakerFn = speaker; _tutorRectFn = tutorRect; _line = line; _targetFn = target;
            ShownLine = line; ShownTutor = tutor; ShownStepId = id;
        }

        // ------------------------------------------------------------------ drawing (IMGUI; a ring and a speech bubble only)

        private void EnsureStyles()
        {
            if (_stylesFor == Screen.height && _text != null) return;
            _stylesFor = Screen.height;
            int fs = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.046f), 15, 28);
            _text = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            _text.normal.textColor = new Color(0.14f, 0.10f, 0.07f, 1f);
            _name = new GUIStyle(_text) { fontSize = Mathf.Max(13, fs - 3), fontStyle = FontStyle.Bold, wordWrap = false };
            _name.normal.textColor = new Color(0.55f, 0.30f, 0.08f);
            _skip = new GUIStyle(_text) { fontSize = Mathf.Max(14, fs - 2), alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _step = new GUIStyle(_name) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleRight };
            _step.normal.textColor = new Color(0.45f, 0.36f, 0.26f);
        }

        private Rect? SafeRect(Func<Rect?> f) { try { return f != null ? f() : null; } catch (Exception) { return null; } }

        private void OnGUI()
        {
            BubbleShown = false; RingShown = false;
            if (!Enabled || Active == null || ShownLine == null || Hud.MenuOpen || MapScreen.IsOpen) return;
            if (!Safe(Active.GameActive)) return;
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            EnsureStyles();
            GUI.depth = -9;
            var safe = ScreenSafeArea.Current();
            float h = Screen.height;

            // The ring on the target.
            Rect? target = SafeRect(_targetFn);
            Rect avoid = default; bool haveAvoid = false;
            if (target.HasValue && target.Value.width > 0f && !float.IsNaN(target.Value.x))
            {
                var r = target.Value;
                float minSide = Mathf.Max(WorldHitTarget.MinSide(h), h * 0.09f);
                if (r.width < minSide) r = new Rect(r.center.x - minSide / 2, r.y, minSide, r.height);
                if (r.height < minSide) r = new Rect(r.x, r.center.y - minSide / 2, r.width, minSide);
                float pad = h * 0.012f;
                r = new Rect(r.x - pad, r.y - pad, r.width + 2 * pad, r.height + 2 * pad);
                if (r.xMax > 0 && r.yMax > 0 && r.x < Screen.width && r.y < Screen.height)
                {
                    RingRect = r; RingShown = true; avoid = r; haveAvoid = true;
                    DrawRing(r);
                }
            }

            // The bubble: over the tutor's head if it is on screen, else at the top of the screen; never over the ring.
            string head = string.IsNullOrEmpty(_tutor) ? "" : _tutor;
            float bw = Mathf.Min(safe.width * 0.46f, Mathf.Max(h * 1.0f, 300f));
            float padx = h * 0.03f, pady = h * 0.02f;
            float textH = _text.CalcHeight(new GUIContent(_line), bw - 2 * padx);
            float nameH = _name.CalcHeight(new GUIContent(head.Length > 0 ? head : " "), bw);
            float skipH = Mathf.Max(h * 0.085f, 30f), skipW = Mathf.Max(h * 0.2f, 70f);
            float bh = pady + nameH + textH + skipH + pady * 0.6f;
            var size = new Vector2(bw, bh);
            Vector2? anchor = TutorScreenPoint();
            var rect = PlaceBubble(size, anchor, haveAvoid ? avoid : (Rect?)null, safe);
            BubbleRect = rect; BubbleShown = true;

            // paper, edge, name, text, Skip
            DrawBox(rect, new Color(0.98f, 0.95f, 0.86f, 0.97f), new Color(0.30f, 0.22f, 0.14f, 0.95f));
            GUI.Label(new Rect(rect.x + padx, rect.y + pady * 0.6f, bw - 2 * padx, nameH), head, _name);
            if (Active.Steps.Count > 0 && !_outro && _tip == null)
                GUI.Label(new Rect(rect.xMax - padx - 90, rect.y + pady * 0.6f, 90, nameH), Mathf.Min(Active.Index + 1, Active.Steps.Count) + " / " + Active.Steps.Count, _step);
            GUI.Label(new Rect(rect.x + padx, rect.y + pady * 0.6f + nameH, bw - 2 * padx, textH), _line, _text);
            var sk = new Rect(rect.xMax - padx - skipW, rect.yMax - pady * 0.5f - skipH, skipW, skipH);
            SkipRect = sk;
            DrawBox(sk, new Color(0.99f, 0.86f, 0.55f, 0.97f), new Color(0.55f, 0.35f, 0.12f, 0.95f));
            GUI.Label(sk, "Skip", _skip);
        }

        private Vector2? TutorScreenPoint()
        {
            var tr = SafeRect(_tutorRectFn);
            if (tr.HasValue && tr.Value.width > 0f) return new Vector2(tr.Value.center.x, tr.Value.y);
            ISpeaker sp = null;
            try { sp = _speakerFn != null ? _speakerFn() : null; } catch (Exception) { }
            if (sp == null || sp.Head == null) return null;
            var cam = TutorialTargets.SceneCamera();
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(sp.Head.position);
            if (s.z <= 0f || s.x < 0 || s.x > Screen.width || s.y < 0 || s.y > Screen.height) return null;
            return new Vector2(s.x, Screen.height - s.y);
        }

        private static float Overlap(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), hh = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && hh > 0f ? w * hh : 0f;
        }

        private Rect PlaceBubble(Vector2 size, Vector2? anchor, Rect? avoid, Rect safe)
        {
            float m = Screen.height * 0.02f;
            var cands = new List<Rect>();
            if (anchor.HasValue && _tutorRectFn == null)
            {
                var a = anchor.Value;
                cands.Add(new Rect(a.x - size.x / 2, a.y - size.y - m, size.x, size.y));          // above the head
                cands.Add(new Rect(a.x + m * 2, a.y - size.y / 2, size.x, size.y));              // right
                cands.Add(new Rect(a.x - size.x - m * 2, a.y - size.y / 2, size.x, size.y));     // left
                cands.Add(new Rect(a.x - size.x / 2, a.y + m * 6, size.x, size.y));               // below
            }
            cands.Add(new Rect(safe.x + m, safe.y + m, size.x, size.y));                           // top left
            cands.Add(new Rect(safe.xMax - size.x - m, safe.y + m, size.x, size.y));              // top right
            cands.Add(new Rect(safe.center.x - size.x / 2, safe.y + m, size.x, size.y));          // top centre
            cands.Add(new Rect(safe.x + m, safe.yMax - size.y - m, size.x, size.y));              // bottom left
            cands.Add(new Rect(safe.xMax - size.x - m, safe.yMax - size.y - m, size.x, size.y));  // bottom right
            Rect best = cands[0]; float bestCost = float.MaxValue;
            Rect? ledger = LedgerCard.IsOpen ? LedgerCard.LastRect : (Rect?)null;
            for (int i = 0; i < cands.Count; i++)
            {
                var c = cands[i];
                // keep inside the safe area
                float dx = Mathf.Max(0f, safe.xMin - c.xMin) - Mathf.Max(0f, c.xMax - safe.xMax);
                float dy = Mathf.Max(0f, safe.yMin - c.yMin) - Mathf.Max(0f, c.yMax - safe.yMax);
                c.x += dx; c.y += dy;
                float cost = i * 1f;
                if (avoid.HasValue) cost += Overlap(c, avoid.Value) * 50f;
                if (ledger.HasValue) cost += Overlap(c, ledger.Value) * 20f;
                if (cost < bestCost) { bestCost = cost; best = c; }
            }
            return best;
        }

        private static void DrawBox(Rect r, Color fill, Color edge)
        {
            var w = Texture2D.whiteTexture;
            GUI.DrawTexture(r, w, ScaleMode.StretchToFill, true, 0f, fill, Vector4.zero, new Vector4(10, 10, 10, 10));
            GUI.DrawTexture(r, w, ScaleMode.StretchToFill, true, 0f, edge, new Vector4(2, 2, 2, 2), new Vector4(10, 10, 10, 10));
        }

        private void DrawRing(Rect r)
        {
            var w = Texture2D.whiteTexture;
            float t = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            float k = Screen.height / 390f;
            float thick = Mathf.Max(3f, 3.5f * k);
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            var gold = new Color(1f, 0.82f, 0.30f, 0.65f + 0.35f * pulse);
            GUI.DrawTexture(r, w, ScaleMode.StretchToFill, true, 0f, gold, new Vector4(thick, thick, thick, thick), new Vector4(radius, radius, radius, radius));
            // a second ring that grows and fades
            float g = (t * 0.9f) % 1f; float grow = g * Screen.height * 0.035f;
            var r2 = new Rect(r.x - grow, r.y - grow, r.width + 2 * grow, r.height + 2 * grow);
            var c2 = new Color(1f, 0.85f, 0.4f, (1f - g) * 0.55f);
            float rad2 = Mathf.Min(r2.width, r2.height) * 0.5f;
            GUI.DrawTexture(r2, w, ScaleMode.StretchToFill, true, 0f, c2, new Vector4(thick * 0.6f, thick * 0.6f, thick * 0.6f, thick * 0.6f), new Vector4(rad2, rad2, rad2, rad2));
        }
    }
}
