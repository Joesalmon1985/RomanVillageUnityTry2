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
        public const string DefaultObjective = "Meet Mira in the square, then follow the music to the warehouse.";

        /// <summary>Sets the objective line shown at the top of the screen (null or empty hides it).</summary>
        public static void SetObjective(string text) => Objective = text;

        /// <summary>The current notice (save, load, pause messages) and whether it is a refusal.</summary>
        public static string NoticeText { get; private set; }
        public static bool NoticeIsError { get; private set; }
        private static float _noticeUntil;

        /// <summary>Shows a one-line notice at the top of the screen in every view, over menus and encounters.</summary>
        public static void Notice(string text, bool error, float seconds = 4f)
        {
            NoticeText = text; NoticeIsError = error; _noticeUntil = Time.unscaledTime + seconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Objective = DefaultObjective; NoticeText = null; _noticeUntil = 0; }

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
            else if (StrategyWindow.IsOpen) StrategyWindow.Close();
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
                                 : "WASD / middle-drag pan   Wheel zoom   Click select   L go here   Tab back   Esc menu and guide";
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
            var r = new Rect((Screen.width - w) / 2, StrategyHud.StripBottom + UiStyle.Px(70), w, h); // below the strip and the What now line
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
            if (MenuPage == Page.Controls) DrawColumns(body, ControlsLeft, ControlsRight);
            else DrawColumns(body, HowToPlayLeft(), HowToPlayRight());

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

        private const string K = "<color=#F2B84D>", KE = "</color>";
        private static string H(string t) => "<b><color=#F2B84D>" + t + "</color></b>\n";

        public static readonly string ControlsLeft =
            H("On foot (first person)") +
            "W A S D walk, Shift run, mouse to look.\n" +
            "E talk or use (the yellow prompt), left click inspect.\n" +
            "Tab switches to God View and back; you stay where you stood.\n\n" +
            H("God View (Tab)") +
            "W A S D or middle-drag to pan, wheel to zoom. H home, M whole island.\n" +
            "Left click selects; right click goes back to Inspect.\n" +
            "Tools (bottom bar): B Build, R Road (X remove), V Inspect, P Problems, G Wares, T Market, C Cards, F City, J Hero, N Army, O Wonder, L Go here (fast travel: click open ground), End Round.\n\n" +
            H("Anywhere") +
            "F5 save, F9 load (one slot). Esc this menu, or closes a dialogue or panel.";

        public static readonly string ControlsRight =
            H("Ward duel (Mira)") +
            "1 2 3 4 add essences, Backspace removes one.\n" +
            "Enter locks your Ward, then Enter or Space casts a guess.\n" +
            "Result: R rematch, Enter or Esc return.\n\n" +
            H("Rhythm party (DJ Sol)") +
            "Pads D F J K: press as notes reach the line; hold long notes.\n" +
            "In the last block, Left arrow picks the bar, Right arrow the balcony.\n" +
            "[ and ] shift the timing by 10 ms if notes feel early or late.\n" +
            "Result: R retry, Enter or Esc return.\n" +
            "The music can't pause: Esc twice during the phrase leaves the party.\n\n" +
            H("Bell") +
            "Ring the bell in the square (E) or press End Round: it starts the next round once the harvest season (60 s) is over.";

        private static string HowToPlayLeft()
        {
            var s = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State : null;
            int planks = s != null ? s.Economy.Tuning.CommissionPlanks : 18, bricks = s != null ? s.Economy.Tuning.CommissionBricks : 18;
            return
                H("1. The square") +
                "Talk to Mira (E) for a practice duel: guess her hidden three-part Ward. After each guess you see how many parts are right (fractures), right but in the wrong place (echoes) and wrong (fades).\n\n" +
                H("2. The warehouse party") +
                "Follow the music to the warehouse and talk to DJ Sol. Hit the falling notes with D F J K. Pass 3 of the 4 blocks to win the crowd; in the last block pick the bar or the balcony, and pass that block to open it.\n\n" +
                H("3. Bram's commission") +
                "Quartermaster Bram (by the storehouse) asks for six buildings (forester, woodcutter, sawmill, clay pit, charcoal kiln, brick kiln) joined by roads, then " + planks + " planks and " + bricks + " bricks. Press Tab, use Build and Road. Carriers walk the goods along your roads.";
        }

        private static string HowToPlayRight()
        {
            var s = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State : null;
            int vp = s != null ? s.Strategy.Tuning.VictoryPoints : 18, breaches = s != null ? s.Strategy.Tuning.BreachLimit : 5;
            int ages = s != null ? s.Strategy.Tuning.Ages : 3, rounds = s != null ? s.Strategy.Tuning.RoundsPerAge : 3;
            return
                H("4. Rounds and ages") +
                "Ring the bell (or End Round) to start a round: numbers are drawn and matching hexes give goods. There are " + ages + " ages of " + rounds + " rounds. Each round starts with one card pick from the draft.\n\n" +
                H("5. Grow and defend") +
                "Build cards at your cities (Cards), found new cities (City), trade (Market), build the wonder (Wonder). Demons appear on flags and block roads: move the hero onto them (Hero). Train soldiers at the barracks and send them at the rival outpost (Army).\n\n" +
                H("6. Winning and losing") +
                "First to " + vp + " points wins. " + breaches + " breaches (demon outbreaks) lose. Otherwise the most points after the last age wins. The line under the top bar always says what to do next.";
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
