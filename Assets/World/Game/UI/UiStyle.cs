using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Shared IMGUI look. Sizes are authored for a 1080 px tall screen and scaled by Screen.height.
    /// Call only from OnGUI. Styles rebuild automatically when the screen height changes.
    /// </summary>
    public static class UiStyle
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.06f, 0.82f);
        public static readonly Color TextColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        public static readonly Color AccentColor = new Color(0.95f, 0.72f, 0.30f, 1f);
        public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.85f);

        private static int _builtForHeight = -1;
        private static GUIStyle _label, _title, _small, _button, _panel;
        private static readonly Dictionary<Color32, Texture2D> Textures = new Dictionary<Color32, Texture2D>();

        /// <summary>Scale factor relative to a 1080 px tall screen (never below 0.5).</summary>
        public static float Scale => Mathf.Max(0.5f, Screen.height / 1080f);

        /// <summary>Scaled pixel size for a size authored at 1080p.</summary>
        public static int Px(float px1080) => Mathf.RoundToInt(px1080 * Scale);

        public static GUIStyle Label { get { Ensure(); return _label; } }
        public static GUIStyle Title { get { Ensure(); return _title; } }
        public static GUIStyle Small { get { Ensure(); return _small; } }
        public static GUIStyle Button { get { Ensure(); return _button; } }
        public static GUIStyle Panel { get { Ensure(); return _panel; } }

        /// <summary>1x1 solid texture, cached per colour.</summary>
        public static Texture2D SolidTexture(Color c)
        {
            Color32 key = c;
            if (Textures.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            Textures[key] = t;
            return t;
        }

        /// <summary>Draws a nearly opaque panel (for menus and dialogue over busy scenes).</summary>
        public static void DrawSolidPanel(Rect r)
        {
            GUI.DrawTexture(r, SolidTexture(new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f)));
        }

        /// <summary>Draws a filled background panel.</summary>
        public static void DrawPanel(Rect r) => GUI.Box(r, GUIContent.none, Panel);

        /// <summary>Label with a 1-2 px drop shadow so it reads over the 3D world.</summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style = null)
        {
            style = style ?? Label;
            var old = style.normal.textColor;
            int o = Mathf.Max(1, Px(2));
            style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        private static void Ensure()
        {
            if (_builtForHeight == Screen.height && _label != null) return;
            _builtForHeight = Screen.height;

            _label = new GUIStyle(GUI.skin.label) { fontSize = Px(26), wordWrap = true, richText = true };
            _label.normal.textColor = TextColor;

            _title = new GUIStyle(_label) { fontSize = Px(38), fontStyle = FontStyle.Bold };
            _title.normal.textColor = AccentColor;

            _small = new GUIStyle(_label) { fontSize = Px(20) };

            _button = new GUIStyle(GUI.skin.button) { fontSize = Px(26), padding = new RectOffset(Px(16), Px(16), Px(8), Px(8)) };
            _button.normal.background = SolidTexture(new Color(0.22f, 0.18f, 0.14f, 0.95f));
            _button.hover.background = SolidTexture(new Color(0.34f, 0.27f, 0.18f, 0.95f));
            _button.active.background = SolidTexture(new Color(0.45f, 0.34f, 0.18f, 0.95f));
            _button.normal.textColor = _button.hover.textColor = _button.active.textColor = TextColor;

            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(Px(16), Px(16), Px(12), Px(12)) };
            _panel.normal.background = SolidTexture(PanelColor);
        }
    }
}
