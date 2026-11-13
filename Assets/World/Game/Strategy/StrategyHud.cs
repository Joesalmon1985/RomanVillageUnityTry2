using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    /// <summary>
    /// A centred strategy window (draft modal, and the first-person NPC panels). In first person it frees the cursor
    /// with an avatar lock while open. One window at a time; Esc or Close shuts it.
    /// </summary>
    public sealed class StrategyWindow : MonoBehaviour
    {
        public const string AvatarLock = "StrategyWindow";
        public static StrategyWindow Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;
        public static StrategyPanels.Kind Current => Instance != null ? Instance._kind : StrategyPanels.Kind.Draft;
        public static Rect ScreenRect { get; private set; }

        private bool _open;
        private StrategyPanels.Kind _kind;
        private WorldBootstrap _boot;

        public void Bind(WorldBootstrap boot) { Instance = this; _boot = boot; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Open(StrategyPanels.Kind k)
        {
            var w = Instance;
            if (w == null) return;
            if (DialoguePanel.IsOpen) DialoguePanel.Close();
            w._kind = k; w._open = true;
            Flow.ResetScroll("window");
            w._boot.Avatar.AddLock(AvatarLock);
        }

        public static void Close()
        {
            var w = Instance;
            if (w == null || !w._open) return;
            w._open = false;
            ScreenRect = Rect.zero;
            w._boot.Avatar.RemoveLock(AvatarLock);
            if (w._kind == StrategyPanels.Kind.Draft && StrategyHud.Instance != null) StrategyHud.Instance.DraftDismissed = true;
        }

        private void OnGUI()
        {
            if (!_open || _boot == null || Hud.MenuOpen) { if (!_open) ScreenRect = Rect.zero; return; }
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            GUI.depth = -8;
            var s = _boot.Sim.State;
            float w = Mathf.Min(UiStyle.Px(940), Screen.width - UiStyle.Px(40));
            float top = UiStyle.Px(140);
            float h = Mathf.Min(UiStyle.Px(820), Screen.height - top - UiStyle.Px(150));
            var r = new Rect((Screen.width - w) / 2, top, w, h);
            ScreenRect = r;
            GodToolbar.AddUiRect(r);
            UiStyle.DrawSolidPanel(r);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, UiStyle.Px(4)), UiStyle.SolidTexture(UiStyle.AccentColor));
            float pad = UiStyle.Px(16);
            GUI.Label(new Rect(r.x + pad, r.y + UiStyle.Px(10), w - UiStyle.Px(300), UiStyle.Px(44)), StrategyPanels.TitleOf(_kind), new GUIStyle(UiStyle.Title) { fontSize = UiStyle.Px(32) });
            string close = _kind == StrategyPanels.Kind.Draft && StrategyApi.DraftPending(s) ? "Look at the map" : "Close  [Esc]";
            if (GUI.Button(new Rect(r.xMax - pad - UiStyle.Px(230), r.y + UiStyle.Px(12), UiStyle.Px(230), UiStyle.Px(40)), close, Flow.ButtonStyle)) { Close(); return; }
            var body = new Rect(r.x, r.y + UiStyle.Px(56), r.width, r.height - UiStyle.Px(56));
            var f = Flow.Begin("window", body);
            StrategyPanels.Draw(_kind, f, s);
            f.End();
        }
    }

    /// <summary>
    /// Strategy presentation in both views: the round / age / score strip (full in God View, compact in first person)
    /// with the "What now?" line; toasts and banners for strategy and military events; the draft pause
    /// (<see cref="PauseReasons.Draft"/> while a draft is pending) and draft modal; the end-of-game screen.
    /// </summary>
    public sealed class StrategyHud : MonoBehaviour
    {
        public static StrategyHud Instance { get; private set; }

        /// <summary>The current hint ("Pick a card from the draft.").</summary>
        public string WhatNow { get; private set; } = "";
        public bool EndScreenOpen { get; private set; }
        public bool DraftDismissed { get; set; }
        /// <summary>Events seen (for tests): type name -> count.</summary>
        public readonly Dictionary<string, int> Seen = new Dictionary<string, int>();
        public string LastEventText { get; private set; }

        private WorldBootstrap _boot;
        private float _refreshAt;
        private int _draftKey = -1;
        private bool _gameOverSeen;
        private string _round, _roundShort, _points, _coin, _power, _infection;
        private int _breaches, _limit;
        private bool _showScore;
        private Rect _pointsRect;

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            boot.Clock.Ticked += OnTicked;
            if (boot.Sim.State.Strategy.GameOver) _gameOverSeen = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) { _boot.Clock.Ticked -= OnTicked; _boot.Clock.Resume(PauseReasons.Draft); }
            if (_boot != null && _boot.Avatar != null) _boot.Avatar.RemoveLock("EndScreen");
        }

        private WorldState S => _boot.Sim.State;
        private bool God => _boot.Views != null && _boot.Views.CurrentView == ViewMode.God;

        // ---------------- per frame ----------------

        private void Update()
        {
            if (_boot == null) return;
            var s = S;
            bool draft = StrategyApi.DraftPending(s) && !s.Strategy.GameOver;
            if (draft && !_boot.Clock.IsPausedFor(PauseReasons.Draft)) _boot.Clock.Pause(PauseReasons.Draft);
            else if (!draft && _boot.Clock.IsPausedFor(PauseReasons.Draft)) _boot.Clock.Resume(PauseReasons.Draft);

            int key = draft ? s.Strategy.DraftAge * 10 + s.Strategy.DraftStep : -1;
            if (key != _draftKey) { _draftKey = key; DraftDismissed = false; }
            // The draft opens itself in God View (in first person, Scribe Pell or Tab opens it).
            if (draft && God && !DraftDismissed && !StrategyWindow.IsOpen && !EndScreenOpen && !Hud.MenuOpen) StrategyWindow.Open(StrategyPanels.Kind.Draft);
            if (!draft && StrategyWindow.IsOpen && StrategyWindow.Current == StrategyPanels.Kind.Draft) StrategyWindow.Close();

            if (s.Strategy.GameOver && !_gameOverSeen) { _gameOverSeen = true; OpenEndScreen(); }
            if (Time.unscaledTime >= _refreshAt) Refresh();
        }

        public void OpenEndScreen()
        {
            EndScreenOpen = true;
            StrategyWindow.Close();
            _boot.Avatar.AddLock("EndScreen");
        }

        public void CloseEndScreen()
        {
            EndScreenOpen = false;
            _boot.Avatar.RemoveLock("EndScreen");
        }

        /// <summary>Rebuilds the strip text and the hint (twice a second, not per frame).</summary>
        public void Refresh()
        {
            _refreshAt = Time.unscaledTime + 0.5f;
            var s = S;
            var st = StrategyApi.Status(s);
            var e = s.Economy;
            string nums = e.LastDraw != null && e.LastDraw.Length > 0 ? string.Join(" ", e.LastDraw) : "-";
            _round = (st.Started ? "<b>Age " + StrategyApi.Roman(st.Age) + "</b> round " + st.RoundInAge + "/" + st.RoundsPerAge : "<b>Prologue</b>")
                     + "  <color=#BBB0A0>(round " + e.Round + ", numbers " + nums + ")</color>";
            _roundShort = st.Started ? "<b>Age " + StrategyApi.Roman(st.Age) + "</b> round " + st.RoundInAge + "/" + st.RoundsPerAge : "<b>Prologue</b>";
            var p = StrategyApi.Score(s, PlayerIds.Human); var r = StrategyApi.Score(s, PlayerIds.Rival);
            _points = "You <b><color=#9CD0FF>" + p.Total + "</color></b> : <b><color=#FF9A80>" + r.Total + "</color></b> Rival  <color=#BBB0A0>of " + st.VictoryPoints + "</color>";
            _coin = "Coin <b>" + st.Coin + "</b>";
            var pw = MilitaryApi.Power(s);
            _power = pw.Demand == 0 && pw.Supply == 0 ? "Power -" : "Power " + (pw.Supply >= pw.Demand ? "<b>" : "<color=#FF8070><b>") + pw.Supply + "/" + pw.Demand + (pw.Supply >= pw.Demand ? "</b>" : "</b></color>");
            _infection = st.Started ? "<color=#BBB0A0>Next draw " + st.NextInfection + "</color>" : "";
            _breaches = st.Breaches; _limit = st.BreachLimit;
            WhatNow = ComputeWhatNow(s, st);
            if (st.Started) Hud.SetObjective(st.Headline + (st.GameOver ? "" : "  First to " + st.VictoryPoints + " points wins; " + st.BreachLimit + " breaches lose."));
        }

        // ---------------- what now ----------------

        private string ComputeWhatNow(WorldState s, StrategyStatus st)
        {
            if (st.GameOver) return st.Headline + " Start a new game or keep looking around.";
            if (st.DraftPending) return "Pick a card from the draft" + (God ? "." : " (talk to Scribe Pell, or Tab to God View).");
            if (!st.Started)
            {
                if (!s.Economy.Commission.Complete)
                    return EconomyFirstPerson.Instance != null ? "Commission: " + EconomyFirstPerson.Instance.NextStep(s).Sentence : "Finish Quartermaster Bram's commission.";
                return "The commission is done: end the round (End Round or the bell) to begin Age I.";
            }
            int cap = StrategyApi.DemonCap(s);
            foreach (var kv in StrategyApi.Demons(s))
                if (kv.Value >= cap && st.HeroMovesLeft > 0) return kv.Value + " demons at " + StrategyUi.SiteName(s, kv.Key) + ": one more and it outbreaks (breach " + st.Breaches + " of " + st.BreachLimit + "). Move the hero there (Hero tool).";
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Owner != PlayerIds.Human || b.State == BuildingState.Removed || b.Reason != Reasons.DemonOnRoute) continue;
                string name = EconomyApi.Def(s, b.Type).Name.ToLowerInvariant();
                if (StrategyApi.DemonsAt(s, b.Vertex) > 0) return "A demon sits on the " + name + "'s flag: " + (st.HeroMovesLeft > 0 ? "move the hero there (Hero tool)." : "the hero moves again next round; a squad can clear it too.");
                return "A demon is on the road to the " + name + ": " + (st.HeroMovesLeft > 0 ? "move the hero (Hero tool)." : "the hero moves again next round.");
            }
            int bid = StrategyUi.FirstBarracks(s, true);
            if (bid >= 0)
            {
                var info = MilitaryApi.DescribeBarracks(s, bid);
                if (info.Queued > 0 && !string.IsNullOrEmpty(info.Reason)) return info.Status;
            }
            var room = StrategyApi.CitiesWithCardRoom(s);
            foreach (var c in StrategyApi.Hand(s))
                if (c.Affordable && room.Count > 0) return "You can build " + c.Name + " from your hand (Cards): " + c.EffectText;
            if (StrategyApi.LegalCitySites(s).Count > 0) return "You can found a city now (" + StrategyApi.CityCostText(s) + "): +" + s.Strategy.Tuning.VpPerCity + " points and more land (City tool).";
            var w = StrategyApi.Wonder(s);
            if (w.CanOrder && GodCommands.Check(new BuildWonderStage()).Ok) return "Order wonder stage " + (w.StagesDone + 1) + " (Wonder): " + w.Status;
            foreach (var pr in EconomyText.Problems(s)) if (!pr.IsJob) return pr.Title + ": " + pr.Sentence;
            var sh = MilitaryApi.ShieldSummary(s);
            if (sh.PlayerShields <= sh.RivalShields && sh.RoundsLeftInAge <= 1) return sh.Sentence;
            return "The village is working. End the round (End Round or the bell) when this round's batches are used up.";
        }

        // ---------------- events ----------------

        private void Count(SimEvent e)
        {
            string k = e.GetType().Name;
            Seen[k] = Seen.TryGetValue(k, out int n) ? n + 1 : 1;
        }

        private void Say(string text, bool bad)
        {
            LastEventText = text;
            EconomyHud.Toast(text, bad);
        }

        private float _lastOutpostToast;

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            if (events.Count == 0) return;
            var s = S;
            string big = null;
            foreach (var e in events)
            {
                switch (e)
                {
                    case AgeStarted a: Count(e); big = "Age " + StrategyApi.Roman(a.Age) + " begins. Pick cards from the draft; demons now arrive at every round end."; break;
                    case StrategyRoundStarted r: Count(e); Say("Age " + StrategyApi.Roman(r.Age) + ", round " + r.RoundInAge + ": the hero has " + r.HeroMoves + " moves.", false); break;
                    case InfectionDrawn d: Count(e); if (d.Vertices != null && d.Vertices.Length > 0) Say("Infection: demons come to " + StrategyUi.SiteList(s, d.Vertices) + ".", true); break;
                    case DemonRepelled dr: Count(e); Say("The hero repelled a demon at " + StrategyUi.SiteName(s, dr.Vertex) + ".", false); break;
                    case DemonsCleared dc: Count(e); Say((dc.By == "squad" ? "A squad" : "The hero") + " cleared " + dc.Count + (dc.Count == 1 ? " demon" : " demons") + " at " + StrategyUi.SiteName(s, dc.Vertex) + ".", false); break;
                    case OutbreakOccurred o:
                        Count(e);
                        Say("Outbreak at " + StrategyUi.SiteName(s, o.Vertex) + (o.Neighbours != null && o.Neighbours.Length > 0 ? ": demons spread to " + StrategyUi.SiteList(s, o.Neighbours) : "") + ". Breach " + o.Breaches + " of " + o.Limit + ".", true);
                        break;
                    case EpidemicStruck ep: Count(e); big = "Epidemic! " + char.ToUpper(StrategyUi.SiteName(s, ep.Vertex)[0]) + StrategyUi.SiteName(s, ep.Vertex).Substring(1) + " fills with " + ep.Demons + " demons" + (ep.Outbreak ? " and breaks out" : "") + "."; break;
                    case MilitaryCompared m:
                        Count(e);
                        big = "End of Age " + StrategyApi.Roman(m.Age) + ": your shields " + m.PlayerShields + " against the rival's " + m.RivalShields + ". "
                              + (m.Result == ComparisonResult.Win ? "You win " + m.PlayerVp + " points." : m.Result == ComparisonResult.Loss ? "You lose " + (-m.PlayerVp) + " point" + (m.PlayerVp == -1 ? "" : "s") + "; the rival gains " + m.RivalVp + "." : "A tie: no points.");
                        break;
                    case DraftDealt dd: Count(e); break;
                    case DraftPicked dp: Count(e); if (dp.PlayerId == PlayerIds.Rival) Say("The rival took " + StrategyApi.DescribeCard(s, dp.CardId).Name + ".", false); break;
                    case CardBuilt cb: Count(e); if (cb.PlayerId == PlayerIds.Rival) { var cv = StrategyApi.DescribeCard(s, cb.CardId); Say("The rival built " + cv.Name + " at its outpost: " + cv.EffectText, true); } break;
                    case CityFounded cf: Count(e); break;
                    case WonderStageCompleted wc: Count(e); big = "Wonder stage " + wc.Stage + " complete: +" + wc.Vp + " points."; break;
                    case RivalScored rs: Count(e); Say("The rival's plan scores " + rs.Vp + " points (" + rs.Total + " in all).", true); break;
                    case UnitTrained ut: Count(e); Say("A soldier is trained (" + MilitaryApi.Squads(s, PlayerIds.Human).Count + " squad" + (MilitaryApi.Squads(s, PlayerIds.Human).Count == 1 ? "" : "s") + " ready). Army tool: give orders.", false); break;
                    case QueueStateChanged q: Count(e); if (!string.IsNullOrEmpty(q.Reason)) Say(MilitaryApi.DescribeBarracks(s, q.BarracksId).Status, true); break;
                    case SquadEngaged se: Count(e); Say("Squads fight at " + StrategyUi.SiteName(s, se.Vertex) + ".", false); break;
                    case UnitDied ud: Count(e); Say((IsMine(s, ud.SquadId) ? "One of your soldiers" : "A rival soldier") + " fell at " + StrategyUi.SiteName(s, ud.Vertex) + ".", IsMine(s, ud.SquadId)); break;
                    case SquadDestroyed sd: Count(e); Say(sd.Owner == PlayerIds.Human ? "Your squad was destroyed." : "A rival squad was destroyed.", sd.Owner == PlayerIds.Human); break;
                    case OutpostDamaged od:
                        Count(e);
                        if (Time.unscaledTime - _lastOutpostToast > 2f) { _lastOutpostToast = Time.unscaledTime; Say("The rival outpost is hit: " + od.Hp + "/" + od.MaxHp + ".", false); }
                        break;
                    case OutpostDestroyed _: Count(e); big = "The rival outpost falls! +" + s.Strategy.Tuning.OutpostVp + " points; the rival's plan and raids stop."; break;
                    case RaidLaunched rl: Count(e); Say("Rival raiders march on " + StrategyUi.SiteName(s, rl.Target) + ".", true); break;
                    case RaidOccupied ro: Count(e); Say("Raiders hold " + StrategyUi.SiteName(s, ro.Vertex) + ": carriers cannot pass. Send a squad (Army tool).", true); break;
                    case GameEnded ge: Count(e); big = StrategyApi.Headline(s); break;
                    case HeroMoved _: case SquadMoved _: case SquadArrived _: case SquadOrdered _: case UnitHit _: case PowerChanged _: case BreachAdvanced _: Count(e); break;
                }
            }
            if (big != null) { LastEventText = big; EconomyHud.Banner(big, 7f, true); }
            _refreshAt = 0;
        }

        private static bool IsMine(WorldState s, int squadId)
        {
            foreach (var q in s.Military.Squads) if (q.Id == squadId) return q.Owner == PlayerIds.Human;
            return false;
        }

        // ---------------- drawing ----------------

        private GUIStyle _strip, _hint, _big;
        private int _stylesFor = -1;

        public static float StripBottom => UiStyle.Px(66) + UiStyle.Px(34) + UiStyle.Px(32);

        private void OnGUI()
        {
            if (_boot == null || Hud.MenuOpen || _round == null) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            GUI.depth = -4;
            if (_stylesFor != Screen.height)
            {
                _stylesFor = Screen.height;
                _strip = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(20), alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true };
                _hint = new GUIStyle(_strip) { fontSize = UiStyle.Px(19), wordWrap = true, alignment = TextAnchor.MiddleCenter };
                _hint.normal.textColor = new Color(1f, 0.86f, 0.5f);
                _big = new GUIStyle(UiStyle.Title) { fontSize = UiStyle.Px(64), alignment = TextAnchor.MiddleCenter };
            }
            if (EndScreenOpen) { DrawEndScreen(); return; }
            DrawStrip();
        }

        private void DrawStrip()
        {
            var s = S;
            bool god = God;
            float pad = UiStyle.Px(10), gap = UiStyle.Px(16);
            float W(string x) => string.IsNullOrEmpty(x) ? 0 : _strip.CalcSize(new GUIContent(x)).x;
            float boxes = _limit * (UiStyle.Px(15) + UiStyle.Px(3)) + UiStyle.Px(10);
            float fixedW = W(_points) + W("Breach") + boxes + W(_breaches + "/" + _limit) + W(_coin) + W(_power) + gap * 6 + 2 * pad;
            float maxW = Screen.width - UiStyle.Px(40);
            string round = _round, infection = _infection;
            if (fixedW + W(round) + W(infection) + gap > maxW) infection = null;
            if (fixedW + W(round) > maxW) round = _roundShort;
            float w = Mathf.Clamp(fixedW + W(round) + (infection != null ? W(infection) + gap : 0), UiStyle.Px(god ? 940 : 900), maxW);
            float rowH = UiStyle.Px(34);
            var r = new Rect((Screen.width - w) / 2, UiStyle.Px(66), w, rowH);
            GodToolbar.AddUiRect(r);
            GUI.DrawTexture(r, UiStyle.SolidTexture(new Color(0.08f, 0.07f, 0.06f, 0.86f)));
            float x = r.x + pad;
            x = Seg(x, r, round) + gap;
            var ps = _strip.CalcSize(new GUIContent(_points));
            _pointsRect = new Rect(x - UiStyle.Px(4), r.y + UiStyle.Px(3), ps.x + UiStyle.Px(8), r.height - UiStyle.Px(6));
            bool over = _pointsRect.Contains(Event.current.mousePosition);
            GUI.DrawTexture(_pointsRect, UiStyle.SolidTexture(new Color(1f, 1f, 1f, over || _showScore ? 0.16f : 0.07f)));
            if (GUI.Button(_pointsRect, GUIContent.none, GUIStyle.none)) _showScore = !_showScore;
            x = Seg(x, r, _points) + gap;
            // Breach track: one box per step up to the limit.
            x = Seg(x, r, "Breach") + UiStyle.Px(6);
            float bs = UiStyle.Px(15);
            for (int i = 0; i < _limit; i++)
            {
                var b = new Rect(x, r.y + (r.height - bs) / 2, bs, bs);
                GUI.DrawTexture(b, UiStyle.SolidTexture(i < _breaches ? (i >= _limit - 2 ? new Color(1f, 0.25f, 0.2f) : new Color(0.85f, 0.45f, 0.95f)) : new Color(1f, 1f, 1f, 0.18f)));
                x += bs + UiStyle.Px(3);
            }
            x = Seg(x + UiStyle.Px(4), r, _breaches + "/" + _limit) + gap;
            x = Seg(x, r, _coin) + gap;
            x = Seg(x, r, _power) + gap;
            if (!string.IsNullOrEmpty(infection)) Seg(x, r, infection);
            if (over) GodToolbar.Tooltip(StrategyUi.ScoreTooltip(s), new Vector2(_pointsRect.x, r.yMax + UiStyle.Px(4)));

            // What now?
            string hint = "<b>What now?</b>  " + WhatNow;
            float hh = Mathf.Max(UiStyle.Px(30), _hint.CalcHeight(new GUIContent(hint), w - 2 * pad) + UiStyle.Px(4));
            var hr = new Rect(r.x, r.yMax, w, hh);
            GodToolbar.AddUiRect(hr);
            GUI.DrawTexture(hr, UiStyle.SolidTexture(new Color(0.12f, 0.09f, 0.04f, 0.86f)));
            GUI.Label(new Rect(hr.x + pad, hr.y, hr.width - 2 * pad - (StrategyApi.DraftPending(s) ? UiStyle.Px(150) : 0), hr.height), hint, _hint);
            if (StrategyApi.DraftPending(s) && !StrategyWindow.IsOpen)
                if (GUI.Button(new Rect(hr.xMax - UiStyle.Px(146), hr.y + UiStyle.Px(2), UiStyle.Px(140), hr.height - UiStyle.Px(4)), "Open draft", Flow.ButtonStyle)) StrategyWindow.Open(StrategyPanels.Kind.Draft);
            if (s.Strategy.GameOver && !EndScreenOpen)
                if (GUI.Button(new Rect(hr.xMax - UiStyle.Px(146), hr.y + UiStyle.Px(2), UiStyle.Px(140), hr.height - UiStyle.Px(4)), "Results", Flow.ButtonStyle)) OpenEndScreen();

            if (_showScore)
            {
                string t = StrategyUi.ScoreTooltip(s);
                float bw = UiStyle.Px(620);
                float bh = Flow.TextStyle.CalcHeight(new GUIContent(t), bw - 2 * pad) + 2 * pad;
                var br = new Rect(_pointsRect.x, hr.yMax + UiStyle.Px(4), bw, bh);
                GodToolbar.AddUiRect(br);
                UiStyle.DrawSolidPanel(br);
                GUI.Label(new Rect(br.x + pad, br.y + pad, bw - 2 * pad, bh), t, Flow.TextStyle);
            }
        }

        private float Seg(float x, Rect r, string text)
        {
            var sz = _strip.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(x, r.y, sz.x + 2, r.height), text, _strip);
            return x + sz.x;
        }

        /// <summary>Last new-game seed chosen from the end screen (tests).</summary>
        public static long LastNewGameSeed;

        private void DrawEndScreen()
        {
            var s = S;
            float w = Mathf.Min(UiStyle.Px(1000), Screen.width - UiStyle.Px(40)), pad = UiStyle.Px(28);
            float h = Mathf.Min(UiStyle.Px(660), Screen.height - UiStyle.Px(80));
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GodToolbar.AddUiRect(r);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), UiStyle.SolidTexture(new Color(0, 0, 0, 0.45f)));
            UiStyle.DrawSolidPanel(r);
            var o = s.Strategy.Outcome;
            var col = o == GameOutcome.Victory ? new Color(0.45f, 0.95f, 0.45f) : o == GameOutcome.Defeat ? new Color(1f, 0.4f, 0.3f) : UiStyle.AccentColor;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, UiStyle.Px(6)), UiStyle.SolidTexture(col));
            float y = r.y + pad;
            _big.normal.textColor = col;
            GUI.Label(new Rect(r.x, y, w, UiStyle.Px(80)), o == GameOutcome.Victory ? "Victory" : o == GameOutcome.Defeat ? "Defeat" : "Draw", _big);
            y += UiStyle.Px(86);
            var center = new GUIStyle(Flow.TextStyle) { alignment = TextAnchor.UpperCenter, fontSize = UiStyle.Px(24) };
            GUI.Label(new Rect(r.x + pad, y, w - 2 * pad, UiStyle.Px(70)), StrategyApi.Headline(s) + "\n<size=" + UiStyle.Px(19) + "><color=#BBB0A0>Game minute " + (s.Strategy.EndedTick >= 0 ? (s.Strategy.EndedTick / (60f * WorldSim.TicksPerSecond)).ToString("0.0") : (s.Tick / (60f * WorldSim.TicksPerSecond)).ToString("0.0")) + ", round " + s.Economy.Round + ".</color></size>", center);
            y += UiStyle.Px(84);

            // Score table.
            var p = StrategyApi.Score(s, PlayerIds.Human); var rv = StrategyApi.Score(s, PlayerIds.Rival);
            string[] rows = { "Cities", "Cards", "Wonder", "Military", "Outpost", "Rival plan", "Total" };
            int[] a = { p.Cities, p.Cards, p.Wonder, p.Military, p.Outpost, p.Plan, p.Total };
            int[] b = { rv.Cities, rv.Cards, rv.Wonder, rv.Military, rv.Outpost, rv.Plan, rv.Total };
            float c0 = r.x + w * 0.25f, c1 = r.x + w * 0.55f, c2 = r.x + w * 0.72f, rh = UiStyle.Px(32);
            var st = new GUIStyle(Flow.TextStyle) { fontSize = UiStyle.Px(22) };
            GUI.Label(new Rect(c1, y, UiStyle.Px(160), rh), "<b>You</b>", st);
            GUI.Label(new Rect(c2, y, UiStyle.Px(160), rh), "<b>Rival</b>", st);
            y += rh;
            for (int i = 0; i < rows.Length; i++)
            {
                bool total = i == rows.Length - 1;
                if (total) { GUI.DrawTexture(new Rect(c0, y + UiStyle.Px(2), w * 0.6f, UiStyle.Px(2)), UiStyle.SolidTexture(new Color(1, 1, 1, 0.3f))); y += UiStyle.Px(6); }
                GUI.Label(new Rect(c0, y, UiStyle.Px(260), rh), total ? "<b>" + rows[i] + "</b>" : rows[i], st);
                GUI.Label(new Rect(c1, y, UiStyle.Px(160), rh), total ? "<b>" + a[i] + "</b>" : a[i].ToString(), st);
                GUI.Label(new Rect(c2, y, UiStyle.Px(160), rh), total ? "<b>" + b[i] + "</b>" : b[i].ToString(), st);
                y += rh;
            }
            y += UiStyle.Px(8);
            var cmp = new List<string>();
            foreach (var c in StrategyApi.Comparisons(s)) cmp.Add("Age " + StrategyApi.Roman(c.Age) + " " + c.PlayerShields + "-" + c.RivalShields + " " + c.Result.ToString().ToLowerInvariant());
            string extra = "Shields at the age ends: " + (cmp.Count == 0 ? "none" : string.Join(", ", cmp)) + ".  Breaches " + s.Strategy.Breaches + " of " + s.Strategy.Tuning.BreachLimit
                + ".  Soldiers trained " + s.Military.UnitsTrained + ".  Outpost " + (s.Military.Outpost.Destroyed ? "destroyed" : "standing") + ".";
            GUI.Label(new Rect(r.x + pad, y, w - 2 * pad, UiStyle.Px(64)), extra, new GUIStyle(Flow.SmallStyle) { alignment = TextAnchor.UpperCenter });

            float bw = UiStyle.Px(320), bh = UiStyle.Px(58), by = r.yMax - pad - bh;
            if (GUI.Button(new Rect(r.x + w / 2 - bw - UiStyle.Px(12), by, bw, bh), "New game (fresh seed)", UiStyle.Button))
            {
                LastNewGameSeed = System.DateTime.Now.Ticks % 2000000000L + 1;
                CloseEndScreen();
                _boot.NewGame(LastNewGameSeed);
            }
            if (GUI.Button(new Rect(r.x + w / 2 + UiStyle.Px(12), by, bw, bh), "Keep looking around", UiStyle.Button)) CloseEndScreen();
        }
    }
}
