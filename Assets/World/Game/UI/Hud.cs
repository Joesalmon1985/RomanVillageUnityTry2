using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Heads-up display: crosshair, interaction prompt, view name, clock state, objective line, a notice line (save,
    /// load and pause messages; shown above everything) and the Esc pause menu with the controls and a short
    /// "how to play" guide, Save / Load / New game / Quit (pauses the clock with PauseReasons.Menu). IMGUI via UiStyle.
    /// During the rhythm phrase Esc does not pause (the music runs on the audio clock): it asks for a second Esc,
    /// which leaves the party.
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

        /// <summary>
        /// REFACTOR_SPEC appendix B: the HUD strip is shown (H toggles it, off at the start). F-0 stub: P implements the
        /// strip and the H key; nothing reads this yet.
        /// </summary>
        public static bool Visible { get; set; }
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
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_menu) CloseMenu();
            else if (DialoguePanel.IsOpen) DialoguePanel.Close();
            else OpenMenu();
        }

        private void OnGUI()
        {
            if (_boot == null || _boot.Views == null) return;
            var views = _boot.Views;
            bool fp = views.CurrentView == ViewMode.FirstPerson;
            bool encounter = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;

            // Top-left: view and clock.
            float x = UiStyle.Px(24), y = UiStyle.Px(18);
            string view = fp ? "First person" : "God View";
            UiStyle.ShadowLabel(new Rect(x, y, UiStyle.Px(700), UiStyle.Px(36)), "<b>" + view + "</b>   <size=" + UiStyle.Px(20) + ">Tab to switch</size>", UiStyle.Label);
            var clock = _boot.Clock;
            string state = clock.IsPaused ? "paused (" + string.Join(", ", clock.PauseReasonSet) + ")" : "running";
            UiStyle.ShadowLabel(new Rect(x, y + UiStyle.Px(36), UiStyle.Px(700), UiStyle.Px(30)), "Village clock " + state + "   tick " + _boot.Sim.State.Tick, UiStyle.Small);

            // Objective.
            if (!string.IsNullOrEmpty(Objective) && !encounter)
            {
                var s = new GUIStyle(UiStyle.Label) { alignment = TextAnchor.MiddleCenter };
                float w = UiStyle.Px(1000);
                var r = new Rect((Screen.width - w) / 2, UiStyle.Px(16), w, UiStyle.Px(46));
                UiStyle.DrawPanel(r);
                GUI.Label(r, Objective, s);
            }

            // Crosshair and prompt.
            if (fp && !_menu && !DialoguePanel.IsOpen && !encounter)
            {
                float cx = Screen.width / 2f, cy = Screen.height / 2f, len = UiStyle.Px(10), th = Mathf.Max(2, UiStyle.Px(3));
                var tex = UiStyle.SolidTexture(new Color(1, 1, 1, 0.85f));
                var sh = UiStyle.SolidTexture(new Color(0, 0, 0, 0.6f));
                GUI.DrawTexture(new Rect(cx - len - 1, cy - th / 2 - 1, 2 * len + 2, th + 2), sh);
                GUI.DrawTexture(new Rect(cx - th / 2 - 1, cy - len - 1, th + 2, 2 * len + 2), sh);
                GUI.DrawTexture(new Rect(cx - len, cy - th / 2, 2 * len, th), tex);
                GUI.DrawTexture(new Rect(cx - th / 2, cy - len, th, 2 * len), tex);

                var picker = Picker.Instance;
                if (picker != null && picker.Target != null)
                {
                    var ps = new GUIStyle(UiStyle.Label) { alignment = TextAnchor.MiddleCenter };
                    string text = "<color=#F2B84D><b>[E]</b></color>  " + picker.Target.Prompt;
                    float w = UiStyle.Px(700);
                    var r = new Rect(cx - w / 2, cy + UiStyle.Px(40), w, UiStyle.Px(46));
                    UiStyle.DrawPanel(new Rect(cx - ps.CalcSize(new GUIContent(text)).x / 2 - UiStyle.Px(18), r.y, ps.CalcSize(new GUIContent(text)).x + UiStyle.Px(36), r.height));
                    GUI.Label(r, text, ps);
                }
            }

            // Bottom-left hint.
            if (!_menu && !DialoguePanel.IsOpen && !encounter)
            {
                string hint = fp ? "WASD move   Shift run   Mouse look   E use   Click inspect   F5 save   F9 load   Esc menu and guide"
                                 : "WASD / middle-drag pan   Wheel zoom   Click select   Tab back   Esc menu and guide";
                UiStyle.ShadowLabel(new Rect(x, Screen.height - UiStyle.Px(46), UiStyle.Px(1400), UiStyle.Px(32)), hint, UiStyle.Small);
            }

            if (_menu) DrawMenu();
            DrawNotice();
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
