using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Plague;

namespace World.Game
{
    /// <summary>
    /// The plague's own on-screen controls (G2-2, PL-12): the "Plague" toggle (a touch control; the key is V), and while the
    /// overlay is on a panel with the outbreak counter (of 8), the rate track, the top of the discard pile and the colour
    /// of the four regions. On foot it also shows the banish button and its cooldown (PL-11). A separate component from
    /// <see cref="GodBoardHud"/>; it registers its rectangles with <see cref="GodBoardHud.ExtraOver"/> so presses on it are
    /// not board presses. IMGUI scaled by Screen.height / 390.
    /// </summary>
    public sealed class PlagueHud : MonoBehaviour
    {
        /// <summary>
        /// The Clear view setting (WORLD_UI_PLAN Q-2): the old HUD as a fallback. Off by default: the world carries the plague
        /// (the lodge, the shrines, the board's totem; V toggles the overlay) and nothing is drawn here. On: the "Plague" and
        /// "Banish" buttons and the counter panel are back. Stored in PlayerPrefs "world.clearview".
        /// </summary>
        public static bool ClearView
        {
            get { try { return PlayerPrefs.GetInt("world.clearview", 0) == 1; } catch { return s_clear; } }
            set { s_clear = value; try { PlayerPrefs.SetInt("world.clearview", value ? 1 : 0); } catch { } }
        }
        private static bool s_clear;
        private static System.Func<bool> s_isOn; private static System.Action s_toggle;

        private PlagueViews _v;
        private readonly List<Rect> _rects = new List<Rect>();
        private GUIStyle _label, _button;
        private float _u = 1f;
        /// <summary>Texts drawn last frame (tests and captures).</summary>
        public readonly List<string> Texts = new List<string>();
        public bool OnBoard { get; private set; }

        private void Awake() { _v = GetComponent<PlagueViews>(); GodBoardHud.ExtraOver += Over; }
        private void OnDestroy() { GodBoardHud.ExtraOver -= Over; }

        private bool Over(Vector2 screen)
        {
            var g = new Vector2(screen.x, Screen.height - screen.y);
            foreach (var r in _rects) if (r.Contains(g)) return true;
            return false;
        }

        private Rect R(float x, float y, float w, float h) => new Rect(x * _u, y * _u, w * _u, h * _u);
        private Rect RR(float fromRight, float y, float w, float h) => new Rect(Screen.width - (fromRight + w) * _u, y * _u, w * _u, h * _u);

        private void Fill(Rect r, Color c, bool hit = false)
        {
            GUI.DrawTexture(r, UiStyle.SolidTexture(c));
            if (hit) _rects.Add(r);
        }

        private void Text(Rect r, string s, float size, Color c, bool bold = false, TextAnchor a = TextAnchor.MiddleLeft)
        {
            _label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * _u));
            _label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _label.alignment = a;
            _label.normal.textColor = new Color(0, 0, 0, 0.9f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), s, _label);
            _label.normal.textColor = c;
            GUI.Label(r, s, _label);
            Texts.Add(s);
        }

        private bool Button(Rect r, string s, bool on, float size = 11f)
        {
            _rects.Add(r);
            _button.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * _u));
            var old = GUI.backgroundColor;
            if (on) GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            bool hit = GUI.Button(r, s, _button);
            GUI.backgroundColor = old;
            Texts.Add("[" + s + "]");
            return hit;
        }

        private bool WorldHudAllowed()
        {
            if (Hud.MenuOpen || MapScreen.IsOpen || DialoguePanel.IsOpen) return false;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return false;
            var b = _v.Boot;
            if (b == null || b.Views == null || b.Views.CurrentView != ViewMode.FirstPerson) return false;
            return true;
        }

        private void OnGUI()
        {
            _rects.Clear(); Texts.Clear();
            if (_v == null || _v.P == null) return;
            var dir = _v.Dir;
            OnBoard = dir != null && dir.View == BoardView.Board;
            // World first: without the Clear view setting nothing is drawn and the board's round icon is not offered.
            if (!ClearView)
            {
                if (GodBoardHud.PlagueToggle == s_toggle) GodBoardHud.PlagueToggle = null;
                if (GodBoardHud.PlagueIsOn == s_isOn) GodBoardHud.PlagueIsOn = null;
                return;
            }
            if (!OnBoard && !WorldHudAllowed()) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow };
                _button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            _u = Mathf.Max(0.6f, Screen.height / 390f);
            GUI.depth = -12;
            // On the god board the toggle is a round icon drawn by GodBoardHud (G1-6b, slot 2 along the top-right edge).
            s_isOn = () => _v != null && _v.Overlay; s_toggle = () => _v.ToggleOverlay();
            GodBoardHud.PlagueIsOn = s_isOn;
            GodBoardHud.PlagueToggle = s_toggle;
            if (!OnBoard)
            {
                var toggle = RR(6f, 6f, 92f, 32f);
                if (Button(toggle, _v.Overlay ? "Plague: on" : "Plague", _v.Overlay, 12f)) _v.ToggleOverlay();
            }
            if (_v.Overlay) Panel(OnBoard ? RR(6f, 44f, 200f, 80f) : RR(6f, 42f, 200f, 80f));
            if (!OnBoard) BanishControl();
        }

        private void Panel(Rect r)
        {
            var p = _v.P;
            Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.82f), true);
            float x = r.x + 5f * _u, y = r.y + 2f * _u;
            // Outbreaks: eight pips.
            Text(new Rect(x, y, 62f * _u, 15f * _u), "Outbreaks", 10f, UiStyle.TextColor, true);
            for (int i = 0; i < PlagueRules.OutbreaksToFall; i++)
            {
                var c = new Rect(x + (64f + i * 14f) * _u, y + 3f * _u, 11f * _u, 9f * _u);
                Fill(c, i < p.Outbreaks ? new Color(0.95f, 0.2f, 0.12f) : new Color(0.25f, 0.22f, 0.2f));
            }
            Text(new Rect(x + 178f * _u, y, 20f * _u, 15f * _u), p.Outbreaks + "/" + PlagueRules.OutbreaksToFall, 9f, new Color(0.95f, 0.8f, 0.7f), false, TextAnchor.MiddleLeft);
            y += 16f * _u;
            // The rate track.
            Text(new Rect(x, y, 40f * _u, 15f * _u), "Rate", 10f, UiStyle.TextColor, true);
            for (int i = 0; i < PlagueRules.RateTrack.Length; i++)
            {
                var c = new Rect(x + (40f + i * 17f) * _u, y + 1f * _u, 15f * _u, 13f * _u);
                bool cur = i == p.RateIndex;
                Fill(c, cur ? new Color(0.95f, 0.75f, 0.25f) : (i < p.RateIndex ? new Color(0.45f, 0.30f, 0.15f) : new Color(0.25f, 0.22f, 0.2f)));
                Text(c, PlagueRules.RateTrack[i].ToString(), 9f, cur ? Color.black : new Color(0.9f, 0.86f, 0.75f), true, TextAnchor.MiddleCenter);
            }
            y += 16f * _u;
            // Top of the discard pile.
            string top = "none yet";
            Color topCol = new Color(0.6f, 0.58f, 0.55f);
            if (p.Discards != null && p.Discards.Count > 0)
            {
                int node = p.Discards[p.Discards.Count - 1];
                int e = PlagueApi.ElementOf(node);
                top = PlagueViews.ElementName[e] + ", node " + node;
                topCol = Color.Lerp(PlagueViews.ElementColour[e], Color.white, 0.3f);
            }
            Text(new Rect(x, y, 60f * _u, 15f * _u), "Last card", 10f, UiStyle.TextColor, true);
            Fill(new Rect(x + 62f * _u, y + 3f * _u, 9f * _u, 9f * _u), topCol);
            Text(new Rect(x + 74f * _u, y, 124f * _u, 15f * _u), top, 10f, topCol);
            y += 16f * _u;
            // Regions.
            for (int e = 0; e < 4; e++)
            {
                float cx = x + e * 48f * _u;
                Fill(new Rect(cx, y + 3f * _u, 8f * _u, 8f * _u), PlagueViews.ElementColour[e]);
                Text(new Rect(cx + 10f * _u, y, 38f * _u, 15f * _u), PlagueViews.ElementName[e], 9f, new Color(0.9f, 0.86f, 0.75f));
            }
        }

        private void BanishControl()
        {
            int near = _v.NodeNearPlayer;
            long left = _v.CooldownTicksLeft;
            bool show = near > 0 || left > 0;
            if (!show) return;
            var r = RR(6f, 128f, 130f, 36f);
            string label = left > 0 ? "Banish: " + ((left + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond) + " s" : "Banish [B]";
            Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.6f));
            if (left > 0)
            {
                float frac = Mathf.Clamp01(left / (float)PlagueRules.BanishCooldownTicks);
                Fill(new Rect(r.x + 2, r.yMax - 6f * _u, (r.width - 4) * (1f - frac), 4f * _u), new Color(0.95f, 0.75f, 0.25f));
            }
            if (Button(r, label, near > 0 && left <= 0, 12f)) _v.TryBanishNear();
        }
    }
}
