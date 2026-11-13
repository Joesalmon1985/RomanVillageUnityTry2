using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Heads-up display: crosshair, interaction prompt, view name, clock state, objective line and the
    /// Esc pause panel (pauses the clock with PauseReasons.Menu). IMGUI via UiStyle.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public const string AvatarLock = "Menu";
        public static Hud Instance { get; private set; }
        public static bool MenuOpen => Instance != null && Instance._menu;
        public static string Objective { get; private set; } = "Meet Mira in the square, then follow the music to the warehouse.";

        /// <summary>Sets the objective line shown at the top of the screen (null or empty hides it).</summary>
        public static void SetObjective(string text) => Objective = text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Objective = "Meet Mira in the square, then follow the music to the warehouse.";

        private bool _menu;
        private WorldBootstrap _boot;

        private void Awake() { Instance = this; _boot = GetComponent<WorldBootstrap>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void OpenMenu()
        {
            if (_menu) return;
            _menu = true;
            if (_boot != null) { _boot.Clock.Pause(PauseReasons.Menu); _boot.Avatar.AddLock(AvatarLock); }
        }

        public void CloseMenu()
        {
            if (!_menu) return;
            _menu = false;
            if (_boot != null) { _boot.Clock.Resume(PauseReasons.Menu); _boot.Avatar.RemoveLock(AvatarLock); }
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
                string hint = fp ? "WASD move   Shift run   Mouse look   E use   Click inspect   Esc menu"
                                 : "WASD / middle-drag pan   Wheel zoom   Click select   Tab back   Esc menu";
                UiStyle.ShadowLabel(new Rect(x, Screen.height - UiStyle.Px(46), UiStyle.Px(1200), UiStyle.Px(32)), hint, UiStyle.Small);
            }

            if (_menu) DrawMenu();
        }

        private void DrawMenu()
        {
            GUI.depth = -20;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), UiStyle.SolidTexture(new Color(0, 0, 0, 0.45f)));
            float w = UiStyle.Px(760), h = UiStyle.Px(560);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            UiStyle.DrawSolidPanel(r);
            float pad = UiStyle.Px(28), y = r.y + pad;
            GUI.Label(new Rect(r.x + pad, y, w, UiStyle.Px(50)), "Paused", UiStyle.Title);
            y += UiStyle.Px(62);
            string[] rows =
            {
                "<b>First person</b>",
                "W A S D  walk      Shift  run      Mouse  look",
                "E  talk / use      Left click  inspect",
                "<b>God View</b>",
                "Tab  switch view (you stay where you stood)",
                "W A S D or middle-drag  pan      Wheel  zoom",
                "Left click  select",
                "<b>Anywhere</b>",
                "Esc  this menu / close a dialogue",
            };
            foreach (var row in rows)
            {
                GUI.Label(new Rect(r.x + pad, y, w - 2 * pad, UiStyle.Px(34)), row, UiStyle.Label);
                y += UiStyle.Px(38);
            }
            y += UiStyle.Px(14);
            float bw = (w - 3 * pad) / 2;
            if (GUI.Button(new Rect(r.x + pad, y, bw, UiStyle.Px(56)), "Resume", UiStyle.Button)) CloseMenu();
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning)
            {
                if (GUI.Button(new Rect(r.x + 2 * pad + bw, y, bw, UiStyle.Px(56)), "Leave encounter", UiStyle.Button)) { EncounterHost.Instance.Abort(); CloseMenu(); }
            }
            else if (GUI.Button(new Rect(r.x + 2 * pad + bw, y, bw, UiStyle.Px(56)), "Quit", UiStyle.Button)) Quit();
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
