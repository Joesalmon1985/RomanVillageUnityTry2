using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// The God View management frame: a compact toolbar along the bottom (one button per registered
    /// <see cref="GodTool"/>), a status strip (round, drawn numbers, the storehouse wares that are in stock, coin, and
    /// any registered <see cref="StatusItems"/>), the active tool's panel on the left and its world overlay. One tool
    /// is active at a time ("select" by default). Extension points: <see cref="Register"/> and
    /// <see cref="StatusItems"/> (see Docs/ECONOMY_UI.md).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GodToolbar : MonoBehaviour
    {
        public const string SelectId = "select";
        public static GodToolbar Instance { get; private set; }

        private static readonly List<GodTool> _tools = new List<GodTool>();
        public static IReadOnlyList<GodTool> Tools => _tools;

        /// <summary>
        /// Extra rich-text items drawn at the right end of the status strip (strategy: age, points, breaches).
        /// Return null or "" to hide an item this frame.
        /// </summary>
        public static readonly List<Func<string>> StatusItems = new List<Func<string>>();

        public GodTool Active { get; private set; }

        private WorldBootstrap _boot;
        private bool _pickerDisabledByUs;
        private static readonly List<KeyValuePair<int, Rect>> _uiRects = new List<KeyValuePair<int, Rect>>();
        private static string _tooltip;
        private static int _tooltipFrame = -1;
        private static Vector2? _tooltipAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _tools.Clear(); _uiRects.Clear(); StatusItems.Clear(); }

        /// <summary>Adds a tool (or replaces the one with the same Id). Safe before or after the toolbar exists.</summary>
        public static void Register(GodTool tool)
        {
            if (tool == null || string.IsNullOrEmpty(tool.Id)) return;
            for (int i = 0; i < _tools.Count; i++)
                if (_tools[i].Id == tool.Id)
                {
                    if (Instance != null && Instance.Active == _tools[i]) Instance.Activate(SelectId);
                    _tools.RemoveAt(i); break;
                }
            _tools.Add(tool);
            _tools.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
        }

        public static void Unregister(string id)
        {
            for (int i = 0; i < _tools.Count; i++) if (_tools[i].Id == id) { _tools.RemoveAt(i); return; }
        }

        public static GodTool Find(string id)
        {
            foreach (var t in _tools) if (t.Id == id) return t;
            return null;
        }

        public static T Find<T>() where T : GodTool
        {
            foreach (var t in _tools) if (t is T x) return x;
            return null;
        }

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            if (boot.Views != null) boot.Views.ViewChanged += OnViewChanged;
            Activate(SelectId);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Views != null) _boot.Views.ViewChanged -= OnViewChanged;
        }

        private void OnViewChanged(ViewMode m)
        {
            if (m != ViewMode.God && Active != null && Active.CapturesClicks) Activate(SelectId);
        }

        /// <summary>Makes a tool active (an action tool runs once instead). Returns the tool or null.</summary>
        public GodTool Activate(string id)
        {
            var t = Find(id);
            if (t == null) return null;
            if (t.IsAction) { if (t.Available) t.Run(); return t; }
            if (Active == t) return t;
            var old = Active;
            Active = t;
            old?.Deactivate();
            t.Activate();
            return t;
        }

        public bool IsGod => _boot != null && _boot.Views != null && _boot.Views.CurrentView == ViewMode.God;

        // ---------------- UI rects and tooltip ----------------

        /// <summary>Marks a screen rect (GUI coordinates) as UI this frame, so clicks there do not reach the world.</summary>
        public static void AddUiRect(Rect r) => _uiRects.Add(new KeyValuePair<int, Rect>(Time.frameCount, r));

        public static bool MouseOverUi()
        {
            var m = Input.mousePosition;
            var p = new Vector2(m.x, Screen.height - m.y);
            int f = Time.frameCount;
            for (int i = _uiRects.Count - 1; i >= 0; i--)
            {
                if (_uiRects[i].Key < f - 2) { _uiRects.RemoveAt(i); continue; }
                if (_uiRects[i].Value.Contains(p)) return true;
            }
            return InspectPanel.ContainsMouse();
        }

        /// <summary>Shows a tooltip by the mouse this frame (call from a tool's overlay).</summary>
        public static void Tooltip(string text) { _tooltip = text; _tooltipFrame = Time.frameCount; _tooltipAt = null; }

        /// <summary>Shows a tooltip at a screen point (GUI coordinates) this frame.</summary>
        public static void Tooltip(string text, Vector2 guiPoint) { _tooltip = text; _tooltipFrame = Time.frameCount; _tooltipAt = guiPoint; }

        // ---------------- input ----------------

        private bool Blocked => Hud.MenuOpen || DialoguePanel.IsOpen || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning);

        private void Update()
        {
            if (_boot == null) return;
            bool god = IsGod;
            bool overUi = god && MouseOverUi();
            bool capture = god && Active != null && Active.CapturesClicks;
            bool wantPickerOff = god && (overUi || capture);
            var picker = _boot.Picker;
            if (picker != null)
            {
                if (wantPickerOff && picker.InputEnabled) { picker.InputEnabled = false; _pickerDisabledByUs = true; }
                else if (!wantPickerOff && _pickerDisabledByUs) { picker.InputEnabled = true; _pickerDisabledByUs = false; }
            }
            if (!god || Blocked) return;

            foreach (var t in _tools)
                if (t.Hotkey != KeyCode.None && Input.GetKeyDown(t.Hotkey) && t.Available)
                {
                    if (!t.IsAction && Active == t) Activate(SelectId); else Activate(t.Id);
                }
            if (Input.GetMouseButtonDown(1) && Active != null && Active.Id != SelectId) Activate(SelectId);

            Active?.Update();
            if (capture && !overUi && Input.GetMouseButtonDown(0)) Active.WorldClick();
        }

        // ---------------- drawing ----------------

        private GUIStyle _btn, _btnOn, _btnSmall, _btnOnSmall, _strip;

        public static float BarHeight => UiStyle.Px(56) + UiStyle.Px(40) + UiStyle.Px(18);
        /// <summary>Top of the toolbar in GUI coordinates.</summary>
        public static float BarTop => Screen.height - UiStyle.Px(50) - BarHeight;
        /// <summary>Top and bottom of the left panel column (GUI coordinates).</summary>
        /// <summary>The active tool's left panel (GUI coordinates) while it is drawn; empty otherwise.</summary>
        public static Rect ActivePanelRect => _panelFrame >= Time.frameCount - 1 ? PanelRect : Rect.zero;
        private static Rect PanelRect;
        private static int _panelFrame = -10;
        public static float PanelTop => UiStyle.Px(150); // below the strategy strip
        public static float PanelBottom => BarTop - UiStyle.Px(12);

        private void OnGUI()
        {
            if (!IsGod || Hud.MenuOpen) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            EnsureStyles();
            var s = _boot.Sim.State;

            // Active tool panel (left column) and overlay.
            if (Active != null && Active.HasPanel && !DialoguePanel.IsOpen)
            {
                float w = UiStyle.Px(Active.PanelWidth);
                float top = PanelTop, bottom = PanelBottom;
                float h = Active.PanelHeight(w);
                if (h < 0) h = bottom - top; else h = Mathf.Min(h, bottom - top);
                var area = new Rect(UiStyle.Px(20), top, w, h);
                PanelRect = area; _panelFrame = Time.frameCount;
                AddUiRect(area);
                UiStyle.DrawPanel(area);
                Active.Panel(area);
            }
            Active?.Overlay();

            // Toolbar.
            float barW = Mathf.Min(Screen.width - UiStyle.Px(40), UiStyle.Px(1840));
            var bar = new Rect((Screen.width - barW) / 2, BarTop, barW, BarHeight);
            AddUiRect(bar);
            UiStyle.DrawPanel(bar);
            float pad = UiStyle.Px(8), bh = UiStyle.Px(56);
            float x = bar.x + pad;
            int n = _tools.Count;
            float bw = n == 0 ? 0 : (bar.width - pad * (n + 1)) / n;
            foreach (var t in _tools)
            {
                string label = t.Label;
                string badge = t.Badge;
                if (!string.IsNullOrEmpty(badge)) label += " " + badge;
                string withKey = t.Hotkey != KeyCode.None ? label + " <size=" + UiStyle.Px(15) + "><color=#BBB0A0>[" + KeyName(t.Hotkey) + "]</color></size>" : label;
                var style = Active == t ? _btnOn : _btn;
                // Many tools: drop the hotkey hint, then shrink the font, so no label is clipped.
                float room = bw - UiStyle.Px(10);
                if (style.CalcSize(new GUIContent(withKey)).x <= room) label = withKey;
                else if (style.CalcSize(new GUIContent(label)).x > room) style = Active == t ? _btnOnSmall : _btnSmall;
                bool was = GUI.enabled;
                GUI.enabled = t.Available;
                var br = new Rect(x, bar.y + pad, bw, bh);
                if (GUI.Button(br, label, style))
                {
                    if (!t.IsAction && Active == t && t.Id != SelectId) Activate(SelectId); else Activate(t.Id);
                }
                GUI.enabled = was;
                if (!string.IsNullOrEmpty(t.Tip) && br.Contains(Event.current.mousePosition)) Tooltip(t.Tip);
                x += bw + pad;
            }

            // Status strip.
            var strip = new Rect(bar.x + pad, bar.y + pad * 2 + bh, bar.width - pad * 2, UiStyle.Px(40));
            DrawStatusStrip(strip, s);

            // Tooltip.
            if (_tooltipFrame >= Time.frameCount - 1 && !string.IsNullOrEmpty(_tooltip)) DrawTooltip(_tooltip, _tooltipAt);
        }

        private static string KeyName(KeyCode k) => k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9 ? ((int)(k - KeyCode.Alpha0)).ToString() : k.ToString();

        private void DrawStatusStrip(Rect r, WorldState s)
        {
            var e = s.Economy;
            var store = e.Buildings[e.StorehouseId];
            float x = r.x;
            string round = "<b>Round " + e.Round + "</b>  numbers " + (e.LastDraw != null && e.LastDraw.Length > 0 ? string.Join(" ", e.LastDraw) : "-");
            var rs = _strip.CalcSize(new GUIContent(round));
            GUI.Label(new Rect(x, r.y, rs.x, r.height), round, _strip);
            x += rs.x + UiStyle.Px(20);

            // Right-hand items first (coin and registered items), so wares fill what is left.
            var right = new List<string> { "Coin <b>" + s.Player(PlayerIds.Human).Coin + "</b>" };
            foreach (var f in StatusItems)
            {
                string t = null;
                try { t = f(); } catch (Exception ex) { Debug.LogException(ex); }
                if (!string.IsNullOrEmpty(t)) right.Add(t);
            }
            float rx = r.xMax;
            for (int i = right.Count - 1; i >= 0; i--)
            {
                var sz = _strip.CalcSize(new GUIContent(right[i]));
                rx -= sz.x;
                GUI.Label(new Rect(rx, r.y, sz.x, r.height), right[i], _strip);
                rx -= UiStyle.Px(18);
            }

            float sq = UiStyle.Px(15);
            int hidden = 0;
            foreach (var w in Wares.All)
            {
                int n = store.Stock[(int)w];
                if (n <= 0 && w != Ware.Planks && w != Ware.Bricks) continue;
                string t = EconomyApi.WareTitle(w) + " <b>" + n + "</b>";
                var ts = _strip.CalcSize(new GUIContent(t));
                if (x + sq + ts.x + UiStyle.Px(6) > rx) { hidden++; continue; }
                GUI.DrawTexture(new Rect(x, r.y + (r.height - sq) / 2, sq, sq), UiStyle.SolidTexture(EconomyLook.WareColor(w)));
                GUI.Label(new Rect(x + sq + UiStyle.Px(4), r.y, ts.x, r.height), t, _strip);
                x += sq + ts.x + UiStyle.Px(14);
            }
            if (hidden > 0 && x + UiStyle.Px(60) < rx) GUI.Label(new Rect(x, r.y, UiStyle.Px(80), r.height), "+" + hidden, _strip);
        }

        public static void DrawTooltip(string text, Vector2? at = null)
        {
            var st = new GUIStyle(UiStyle.Small) { wordWrap = true, richText = true };
            float w = UiStyle.Px(540);
            var content = new GUIContent(text);
            float h = st.CalcHeight(content, w - UiStyle.Px(20)) + UiStyle.Px(14);
            var m = at ?? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            float x = Mathf.Min(m.x + UiStyle.Px(22), Screen.width - w - 4);
            float y = Mathf.Min(m.y + UiStyle.Px(18), Screen.height - h - 4);
            if (y + h > BarTop && m.y < BarTop + BarHeight && m.y > BarTop) y = BarTop - h - UiStyle.Px(6); // above the toolbar
            var r = new Rect(x, y, w, h);
            UiStyle.DrawSolidPanel(r);
            GUI.Label(new Rect(r.x + UiStyle.Px(10), r.y + UiStyle.Px(7), w - UiStyle.Px(20), h), content, st);
        }

        private int _stylesFor = -1;
        private void EnsureStyles()
        {
            if (_stylesFor == Screen.height && _btn != null) return;
            _stylesFor = Screen.height;
            _btn = new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(22), richText = true, wordWrap = false, padding = new RectOffset(UiStyle.Px(6), UiStyle.Px(6), UiStyle.Px(4), UiStyle.Px(4)) };
            _btnOn = new GUIStyle(_btn);
            _btnOn.normal.background = _btnOn.hover.background = UiStyle.SolidTexture(new Color(0.62f, 0.44f, 0.16f, 0.98f));
            _btnOn.normal.textColor = _btnOn.hover.textColor = Color.white;
            _btnSmall = new GUIStyle(_btn) { fontSize = UiStyle.Px(17), padding = new RectOffset(UiStyle.Px(2), UiStyle.Px(2), UiStyle.Px(4), UiStyle.Px(4)) };
            _btnOnSmall = new GUIStyle(_btnOn) { fontSize = UiStyle.Px(17), padding = new RectOffset(UiStyle.Px(2), UiStyle.Px(2), UiStyle.Px(4), UiStyle.Px(4)) };
            _strip = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(19), alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true };
        }
    }
}
