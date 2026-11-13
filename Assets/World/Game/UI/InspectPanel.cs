using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// F1 developer tools only: shows the thing selected in the God View (<see cref="PointerInput.Selected"/>, title and
    /// lines) on the right. In normal play a tap shows a small inspect bubble instead (<see cref="SpeechBubbles.Inspect"/>).
    /// </summary>
    public sealed class InspectPanel : MonoBehaviour
    {
        public static InspectPanel Instance { get; private set; }
        /// <summary>Screen rect (GUI coordinates, y down) of the panel last frame; empty when hidden.</summary>
        public static Rect ScreenRect { get; private set; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>True if the mouse is over the panel (so clicks do not select through it).</summary>
        public static bool ContainsMouse()
        {
            var m = Input.mousePosition;
            return ScreenRect.Contains(new Vector2(m.x, Screen.height - m.y));
        }

        private void OnGUI()
        {
            var sel = PointerInput.Selected;
            bool god = ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God;
            if (!god || sel == null || (sel is Object o && o == null) || Hud.MenuOpen) { ScreenRect = Rect.zero; return; }
            float w = UiStyle.Px(460), pad = UiStyle.Px(18);
            var small = UiStyle.Small;
            float h = pad + UiStyle.Px(50);
            foreach (var l in sel.Lines) h += small.CalcHeight(new GUIContent(l), w - 2 * pad) + UiStyle.Px(4);
            h += pad;
            var r = new Rect(Screen.width - w - UiStyle.Px(24), UiStyle.Px(90), w, h);
            ScreenRect = r;
            UiStyle.DrawPanel(r);
            float y = r.y + pad;
            var title = new GUIStyle(UiStyle.Title) { fontSize = UiStyle.Px(32) };
            GUI.Label(new Rect(r.x + pad, y, w - 2 * pad - UiStyle.Px(40), UiStyle.Px(44)), sel.Title, title);
            if (GUI.Button(new Rect(r.xMax - pad - UiStyle.Px(38), y, UiStyle.Px(38), UiStyle.Px(38)), "x", UiStyle.Button)) { PointerInput.Selected = null; return; }
            y += UiStyle.Px(50);
            foreach (var l in sel.Lines)
            {
                float lh = small.CalcHeight(new GUIContent(l), w - 2 * pad);
                GUI.Label(new Rect(r.x + pad, y, w - 2 * pad, lh), l, small);
                y += lh + UiStyle.Px(4);
            }
        }
    }
}
