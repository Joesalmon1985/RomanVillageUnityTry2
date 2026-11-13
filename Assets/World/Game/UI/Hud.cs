using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// The HUD strip (H toggles it, off at the start; REFACTOR_SPEC 2.D "Screen"): game time and the last roll, your
    /// strength against the Ash Clan's, the overrun warning countdown when one runs, the stock of your nearest camp and
    /// your people's proposal queue (read only), all read from the sim helpers. Without it the screen shows only the
    /// world and speech bubbles. Also the notice line (save, load and pause messages; shown above everything) and the
    /// Esc pause menu with the guide pages, Save / Load / New game / Quit (pauses the clock with PauseReasons.Menu).
    /// During the rhythm phrase Esc does not pause (the music runs on the audio clock): it asks for a second Esc,
    /// which leaves the party. Owner: package P.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public const string AvatarLock = "Menu";
        public const float PhraseEscWindow = 3f;
        public static Hud Instance { get; private set; }
        public static bool MenuOpen => Instance != null && Instance._menu;
        public static string Objective { get; private set; } = DefaultObjective;
        public const string DefaultObjective = "Look around the home camp. Mira waits by the hearth for a practice duel.";

        /// <summary>Sets the objective line shown at the top of the screen (null or empty hides it).</summary>
        public static void SetObjective(string text) => Objective = text;

        /// <summary>The current notice (save, load, pause messages) and whether it is a refusal.</summary>
        public static string NoticeText { get; private set; }

        /// <summary>REFACTOR_SPEC appendix B: the HUD strip is shown (H toggles it, off at the start).</summary>
        public static bool Visible { get; set; }
        public const KeyCode ToggleKey = KeyCode.H;
        public static bool NoticeIsError { get; private set; }
        private static float _noticeUntil;

        /// <summary>Shows a one-line notice at the top of the screen in every view, over menus and encounters.</summary>
        public static void Notice(string text, bool error, float seconds = 4f)
        {
            NoticeText = text; NoticeIsError = error; _noticeUntil = Time.unscaledTime + seconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Objective = DefaultObjective; NoticeText = null; _noticeUntil = 0; Visible = false; }

        public enum Page { Controls, HowToPlay }
        public Page MenuPage = Page.Controls;

        private bool _menu;
        private WorldBootstrap _boot;
        private string _armed; // a button waiting for its confirming second click
        private float _phraseEscAt = -10f;

        private void Awake() { Instance = this; _boot = GetComponent<WorldBootstrap>(); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_menu && _boot != null && _boot.Clock != null) _boot.Clock.Resume(PauseReasons.Menu);
        }

        public void OpenMenu()
        {
            if (_menu) return;
            if (InRhythmPhrase()) { RefusePauseInPhrase(); return; }
            _menu = true; _armed = null;
            if (_boot != null) { _boot.Clock.Pause(PauseReasons.Menu); _boot.Avatar.AddLock(AvatarLock); }
        }

        public void CloseMenu()
        {
            if (!_menu) return;
            _menu = false; _armed = null;
            if (_boot != null) { _boot.Clock.Resume(PauseReasons.Menu); _boot.Avatar.RemoveLock(AvatarLock); }
        }

        /// <summary>True while the rhythm party's count-in or phrase is playing (the audio cannot pause).</summary>
        public static bool InRhythmPhrase()
        {
            var host = EncounterHost.Instance;
            var r = RhythmPartyEncounter.Current;
            return host != null && host.IsRunning && r != null && host.Current == r && !r.IsFinished && r.Phase != RhythmPartyEncounter.Screen.Result;
        }

        /// <summary>Esc during the phrase: the first press explains, a second press within a few seconds leaves the party.</summary>
        private void RefusePauseInPhrase()
        {
            if (Time.unscaledTime - _phraseEscAt <= PhraseEscWindow)
            {
                _phraseEscAt = -10f;
                EncounterHost.Instance.Abort();
                Notice("You left the party. Talk to DJ Sol to try again.", false);
                return;
            }
            _phraseEscAt = Time.unscaledTime;
            Notice("The music can't pause during the phrase. Press Esc again to leave the party.", true, PhraseEscWindow);
        }

        private void Update()
        {
            bool god = _boot != null && _boot.Views != null && _boot.Views.CurrentView == ViewMode.God;
            if (Input.GetKeyDown(ToggleKey) && !_menu && !god && !MapScreen.IsOpen) Visible = !Visible;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_menu) CloseMenu();
            else if (MapScreen.IsOpen) MapScreen.Close();
            else if (DialoguePanel.IsOpen) DialoguePanel.Close();
            else OpenMenu();
        }

        private void OnGUI()
        {
            if (_boot == null || _boot.Views == null) return;
            bool fp = _boot.Views.CurrentView == ViewMode.FirstPerson;
            bool encounter = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            if (Visible && fp && !_menu && !encounter && !MapScreen.IsOpen) DrawStrip(_boot.Sim.State);
            if (_menu) DrawMenu();
            DrawNotice();
        }

        // ---------------- HUD strip (H) ----------------

        /// <summary>Lines of the HUD strip, as drawn (tests, captures). Read from the sim helpers only.</summary>
        public static List<string> StripLines(WorldState s)
        {
            var lines = new List<string>();
            long secs = s.Tick / WorldSim.TicksPerSecond;
            var roll = EconomyApi.LastRoll(s);
            string rolled = s.Economy.Dice.RollCount > 0
                ? "last roll <b>" + roll.Sum + "</b> (" + roll.D1 + "+" + roll.D2 + ")" : "no roll yet";
            lines.Add("<b>" + (secs / 60) + ":" + (secs % 60).ToString("00") + "</b>   " + rolled + ", next in " + (EconomyApi.TicksToNextRoll(s) + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond + " s");
            var me = FactionApi.Strength(s, PlayerIds.Human);
            var them = FactionApi.Strength(s, PlayerIds.Rival);
            lines.Add("Strength: your people <b>" + me.Total + "</b>, the Ash Clan <b>" + them.Total + "</b>");
            var o = FactionApi.Outcome(s);
            if (o.GameOver) lines.Add("<color=#F2B84D><b>" + o.Sentence + "</b></color>");
            else
            {
                var w = FactionApi.Warning(s);
                if (w.Active) lines.Add("<color=#FF8A70><b>" + w.Sentence + "</b></color>");
            }
            return lines;
        }

        /// <summary>Your camp nearest to the player (settlement id), or -1.</summary>
        public static int NearestCamp(WorldState s, Vector3 from)
        {
            int best = -1; float bestD = float.MaxValue;
            var t = s.Board.Topology;
            foreach (var st in EconomyApi.Settlements(s))
            {
                if (st.Faction != PlayerIds.Human || st.State != SettlementState.Active) continue;
                float d = BoardSpace.Flat(BoardSpace.Vertex(t, st.Corner) - from);
                if (d < bestD) { bestD = d; best = st.Id; }
            }
            return best;
        }

        private GUIStyle _stripStyle, _stripTitle;
        private int _stripFor = -1;

        private void DrawStrip(WorldState s)
        {
            if (_stripFor != Screen.height || _stripStyle == null)
            {
                _stripFor = Screen.height;
                _stripStyle = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(20), wordWrap = false, richText = true };
                _stripTitle = new GUIStyle(_stripStyle) { fontStyle = FontStyle.Bold };
                _stripTitle.normal.textColor = UiStyle.AccentColor;
            }
            float x = UiStyle.Px(20), y = UiStyle.Px(16), w = UiStyle.Px(560), lh = UiStyle.Px(27);
            var lines = StripLines(s);
            var walker = PlayerWalker.Instance;
            int camp = walker != null ? NearestCamp(s, walker.Position) : -1;
            var props = FactionApi.Proposals(s, PlayerIds.Human);
            int stockRows = camp >= 0 ? 2 : 0;
            float h = UiStyle.Px(16) + lh * (lines.Count + 1 + stockRows + 1 + Mathf.Max(1, props.Count));
            var panel = new Rect(x, y, w, h);
            UiStyle.DrawPanel(panel);
            float cx = x + UiStyle.Px(14), cy = y + UiStyle.Px(8), cw = w - UiStyle.Px(28);
            foreach (var l in lines) { GUI.Label(new Rect(cx, cy, cw, lh), l, _stripStyle); cy += lh; }
            cy += lh * 0.25f;
            if (camp >= 0)
            {
                GUI.Label(new Rect(cx, cy, cw, lh), "Stock at " + EconomyApi.SettlementName(s, camp), _stripTitle); cy += lh;
                float sx = cx, sq = UiStyle.Px(14);
                foreach (var ware in Wares.All)
                {
                    int n = EconomyApi.StockOf(s, camp, ware);
                    if (n <= 0 && !Wares.IsRaw(ware)) continue;
                    string txt = EconomyApi.WareTitle(ware) + " <b>" + n + "</b>";
                    var sz = _stripStyle.CalcSize(new GUIContent(txt));
                    if (sx + sq + sz.x > cx + cw) break;
                    GUI.DrawTexture(new Rect(sx, cy + (lh - sq) / 2, sq, sq), UiStyle.SolidTexture(ColorUtility.TryParseHtmlString(EconomyApi.WareColour(ware), out var wc) ? wc : Color.gray));
                    GUI.Label(new Rect(sx + sq + UiStyle.Px(4), cy, sz.x, lh), txt, _stripStyle);
                    sx += sq + sz.x + UiStyle.Px(14);
                }
                cy += lh;
            }
            GUI.Label(new Rect(cx, cy, cw, lh), "Your people's proposals", _stripTitle); cy += lh;
            if (props.Count == 0) GUI.Label(new Rect(cx, cy, cw, lh), "<i>none open</i>", _stripStyle);
            for (int i = 0; i < props.Count; i++)
            {
                float d = SpellCaster.DistanceTo(props[i].Id);
                string dist = float.IsInfinity(d) ? "" : "  <color=#BBB0A0>" + Mathf.RoundToInt(d) + " m</color>";
                GUI.Label(new Rect(cx, cy, cw, lh), (i + 1) + ". " + FactionApi.ProposalTitle(s, props[i].Id) + "  <color=#BBB0A0>(" + props[i].Speaker + ")</color>" + dist, _stripStyle);
                cy += lh;
            }
            if (!string.IsNullOrEmpty(Objective) && Objective != DefaultObjective)
            {
                var r = new Rect(x, panel.yMax + UiStyle.Px(8), w, lh + UiStyle.Px(10));
                UiStyle.DrawPanel(r);
                GUI.Label(new Rect(cx, r.y + UiStyle.Px(5), cw, lh), Objective, _stripStyle);
            }
        }

        private void DrawNotice()
        {
            if (string.IsNullOrEmpty(NoticeText) || Time.unscaledTime > _noticeUntil) return;
            GUI.depth = -30;
            var st = new GUIStyle(UiStyle.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            st.normal.textColor = NoticeIsError ? new Color(1f, 0.62f, 0.5f) : new Color(0.6f, 1f, 0.6f);
            float w = Mathf.Min(UiStyle.Px(1100), Screen.width - UiStyle.Px(40));
            float h = st.CalcHeight(new GUIContent(NoticeText), w - UiStyle.Px(40)) + UiStyle.Px(20);
            var r = new Rect((Screen.width - w) / 2, UiStyle.Px(80), w, h); // below the objective line
            UiStyle.DrawSolidPanel(r);
            GUI.Label(new Rect(r.x + UiStyle.Px(20), r.y, w - UiStyle.Px(40), h), NoticeText, st);
        }

        // ---------------- pause menu ----------------

        private void DrawMenu()
        {
            GUI.depth = -20;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), UiStyle.SolidTexture(new Color(0, 0, 0, 0.5f)));
            float w = Mathf.Min(UiStyle.Px(1240), Screen.width - UiStyle.Px(30)), h = Mathf.Min(UiStyle.Px(900), Screen.height - UiStyle.Px(150));
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2 + UiStyle.Px(40), w, h);
            UiStyle.DrawSolidPanel(r);
            float pad = UiStyle.Px(26), y = r.y + pad * 0.6f;
            bool encounter = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;

            GUI.Label(new Rect(r.x + pad, y, UiStyle.Px(300), UiStyle.Px(50)), "Paused", UiStyle.Title);
            // Page tabs.
            float tw = UiStyle.Px(230), th = UiStyle.Px(46), tx = r.xMax - pad - 2 * tw - UiStyle.Px(12);
            if (Tab(new Rect(tx, y, tw, th), "Controls", MenuPage == Page.Controls)) MenuPage = Page.Controls;
            if (Tab(new Rect(tx + tw + UiStyle.Px(12), y, tw, th), "How to play", MenuPage == Page.HowToPlay)) MenuPage = Page.HowToPlay;
            y += UiStyle.Px(60);

            float bh = UiStyle.Px(54);
            float bodyH = r.yMax - y - pad - bh - UiStyle.Px(44);
            var body = new Rect(r.x + pad, y, w - 2 * pad, bodyH);
            if (MenuPage == Page.Controls) DrawColumns(body, GuideText.ControlsLeft, GuideText.ControlsRight);
            else DrawColumns(body, GuideText.HowToPlayLeft(_boot.Sim.State), GuideText.HowToPlayRight(_boot.Sim.State));

            // Message line (last save / load result).
            float my = r.yMax - pad - bh - UiStyle.Px(40);
            var save = _boot.Saves;
            string msg = _armed != null ? "<color=#F2B84D>" + ArmedText(_armed) + "</color>"
                       : !string.IsNullOrEmpty(SaveSystem.LastMessage) ? (SaveSystem.LastWasError ? "<color=#FF9A80>" : "<color=#9CF09C>") + SaveSystem.LastMessage + "</color>"
                       : "<color=#BBB0A0>One save slot. F5 saves and F9 loads at any time outside the duel and the party.</color>";
            GUI.Label(new Rect(r.x + pad, my, w - 2 * pad, UiStyle.Px(36)), msg, new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = true });

            // Buttons.
            float by = r.yMax - pad - bh, gap = UiStyle.Px(14);
            int n = 5;
            float bw = (w - 2 * pad - (n - 1) * gap) / n, bx = r.x + pad;
            if (GUI.Button(new Rect(bx, by, bw, bh), "Resume", UiStyle.Button)) CloseMenu();
            bx += bw + gap;
            if (encounter)
            {
                if (GUI.Button(new Rect(bx, by, 2 * bw + gap, bh), "Leave encounter", UiStyle.Button)) { EncounterHost.Instance.Abort(); CloseMenu(); }
                bx += 2 * (bw + gap);
            }
            else
            {
                if (GUI.Button(new Rect(bx, by, bw, bh), "Save  (F5)", UiStyle.Button)) { _armed = null; save?.Save(); }
                bx += bw + gap;
                if (GUI.Button(new Rect(bx, by, bw, bh), _armed == "load" ? "Load: sure?" : "Load  (F9)", UiStyle.Button)) Confirm("load", () => save?.Load());
                bx += bw + gap;
            }
            if (GUI.Button(new Rect(bx, by, bw, bh), _armed == "new" ? "New game: sure?" : "New game", UiStyle.Button))
                Confirm("new", () => { if (encounter) EncounterHost.Instance.Abort(); CloseMenu(); _boot.NewGame(System.DateTime.Now.Ticks % 2000000000L + 1); });
            bx += bw + gap;
            if (GUI.Button(new Rect(bx, by, bw, bh), _armed == "quit" ? "Quit: sure?" : "Quit", UiStyle.Button)) Confirm("quit", () => Quit());
        }

        private void Confirm(string id, System.Action action)
        {
            if (_armed == id) { _armed = null; action(); }
            else _armed = id;
        }

        private static string ArmedText(string id) => id switch
        {
            "load" => "Load the saved game? Anything since the last save is lost. Click Load again to confirm.",
            "new" => "Start a new game with a fresh island? Unsaved progress is lost. Click New game again to confirm.",
            "quit" => "Quit the game? Unsaved progress is lost (F5 saves). Click Quit again to confirm.",
            _ => "",
        };

        private static bool Tab(Rect r, string text, bool on)
        {
            if (on) GUI.DrawTexture(new Rect(r.x, r.yMax - UiStyle.Px(4), r.width, UiStyle.Px(4)), UiStyle.SolidTexture(UiStyle.AccentColor));
            return GUI.Button(r, on ? "<b>" + text + "</b>" : text, UiStyle.Button);
        }

        private static void DrawColumns(Rect body, string left, string right)
        {
            var st = new GUIStyle(UiStyle.Label) { wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            // Shrink the font until both columns fit (small windows).
            float cw = (body.width - UiStyle.Px(30)) / 2;
            for (int size = st.fontSize; size >= Mathf.Max(9, UiStyle.Px(14)); size--)
            {
                st.fontSize = size;
                if (st.CalcHeight(new GUIContent(left), cw) <= body.height && st.CalcHeight(new GUIContent(right), cw) <= body.height) break;
            }
            GUI.Label(new Rect(body.x, body.y, cw, body.height), left, st);
            GUI.Label(new Rect(body.x + cw + UiStyle.Px(30), body.y, cw, body.height), right, st);
        }

        public static void Quit(int exitCode = 0)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
