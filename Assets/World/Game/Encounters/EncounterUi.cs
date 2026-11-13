using System;
using System.IO;
using UnityEngine;
using World.Sim.Encounters.Rhythm;
using World.Sim.Encounters.Ward;

namespace World.Game
{
    /// <summary>Large, readable IMGUI pieces shared by the Ward duel and the rhythm party (authored at 1080p, scaled).</summary>
    public static class EncounterUi
    {
        public static readonly Color Gold = new Color(0.98f, 0.78f, 0.32f);
        public static readonly Color Text = UiStyle.TextColor;
        public static readonly Color Dim = new Color(0.75f, 0.72f, 0.66f);
        public static readonly Color Good = new Color(0.45f, 0.92f, 0.50f);
        public static readonly Color Bad = new Color(1.0f, 0.42f, 0.38f);
        public static readonly Color PanelBg = new Color(0.07f, 0.06f, 0.08f, 0.86f);
        public static readonly Color PanelEdge = new Color(0.98f, 0.78f, 0.32f, 0.55f);

        private static int _h = -1;
        private static GUIStyle _huge, _big, _header, _body, _hint, _chip, _chipSmall, _button;

        public static GUIStyle Huge { get { Ensure(); return _huge; } }
        public static GUIStyle Big { get { Ensure(); return _big; } }
        public static GUIStyle Header { get { Ensure(); return _header; } }
        public static GUIStyle Body { get { Ensure(); return _body; } }
        public static GUIStyle Hint { get { Ensure(); return _hint; } }
        public static GUIStyle Chip { get { Ensure(); return _chip; } }
        public static GUIStyle ChipSmall { get { Ensure(); return _chipSmall; } }
        public static GUIStyle Button { get { Ensure(); return _button; } }

        public static float Px(float v) => UiStyle.Px(v);

        private static void Ensure()
        {
            if (_h == Screen.height && _body != null) return;
            _h = Screen.height;
            var baseLabel = UiStyle.Label;
            _body = new GUIStyle(baseLabel) { fontSize = UiStyle.Px(28), wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            _body.normal.textColor = Text;
            _hint = new GUIStyle(_body) { fontSize = UiStyle.Px(23) };
            _hint.normal.textColor = Dim;
            _header = new GUIStyle(_body) { fontSize = UiStyle.Px(34), fontStyle = FontStyle.Bold };
            _header.normal.textColor = Gold;
            _big = new GUIStyle(_body) { fontSize = UiStyle.Px(50), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _huge = new GUIStyle(_big) { fontSize = UiStyle.Px(120) };
            _chip = new GUIStyle(_body) { fontSize = UiStyle.Px(24), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _chipSmall = new GUIStyle(_chip) { fontSize = UiStyle.Px(20) };
            _button = new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(28), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        public static void Fill(Rect r, Color c) => GUI.DrawTexture(r, UiStyle.SolidTexture(c));

        /// <summary>Dark panel with a thin gold edge.</summary>
        public static void Panel(Rect r, Color? bg = null)
        {
            Fill(r, bg ?? PanelBg);
            float e = Mathf.Max(1, Px(2));
            var c = PanelEdge;
            Fill(new Rect(r.x, r.y, r.width, e), c);
            Fill(new Rect(r.x, r.yMax - e, r.width, e), c);
            Fill(new Rect(r.x, r.y, e, r.height), c);
            Fill(new Rect(r.xMax - e, r.y, e, r.height), c);
        }

        public static void Label(Rect r, string text, GUIStyle style, Color? color = null, TextAnchor? align = null)
        {
            var old = style.normal.textColor; var oldA = style.alignment;
            if (color.HasValue) style.normal.textColor = color.Value;
            if (align.HasValue) style.alignment = align.Value;
            GUI.Label(r, text, style);
            style.normal.textColor = old; style.alignment = oldA;
        }

        /// <summary>Label with a drop shadow (for text drawn over the 3D scene).</summary>
        public static void Shadow(Rect r, string text, GUIStyle style, Color? color = null, TextAnchor? align = null)
        {
            float o = Mathf.Max(1, Px(3));
            Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style, new Color(0, 0, 0, 0.85f), align);
            Label(r, text, style, color, align);
        }

        public static bool KeyButton(Rect r, string label) => GUI.Button(r, label, Button);

        /// <summary>Clamps a rect inside the screen with a margin.</summary>
        public static Rect Fit(Rect r, float margin = 8f)
        {
            float m = Px(margin);
            r.width = Mathf.Min(r.width, Screen.width - 2 * m);
            r.height = Mathf.Min(r.height, Screen.height - 2 * m);
            r.x = Mathf.Clamp(r.x, m, Screen.width - m - r.width);
            r.y = Mathf.Clamp(r.y, m, Screen.height - m - r.height);
            return r;
        }

        public static Rect Centered(float w, float h, float yOffset = 0)
            => Fit(new Rect((Screen.width - Px(w)) / 2f, (Screen.height - Px(h)) / 2f + Px(yOffset), Px(w), Px(h)));

        // ---------------------------------------------------------------- essences

        public static Color EssenceColor(int e)
        {
            switch ((Essence)e)
            {
                case Essence.Flame: return new Color(1.0f, 0.42f, 0.16f);
                case Essence.Frost: return new Color(0.42f, 0.78f, 1.0f);
                case Essence.Vine: return new Color(0.36f, 0.85f, 0.32f);
                case Essence.Light: return new Color(1.0f, 0.93f, 0.62f);
                default: return Color.gray;
            }
        }

        /// <summary>A coloured essence chip; null essence (-1) draws an empty slot.</summary>
        public static void EssenceChip(Rect r, int essence, string caption = null, bool small = false)
        {
            if (essence < 0)
            {
                Fill(r, new Color(1, 1, 1, 0.08f));
                Label(r, caption ?? "?", Chip, Dim);
                return;
            }
            var c = EssenceColor(essence);
            Fill(r, new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.95f));
            Fill(new Rect(r.x, r.yMax - Mathf.Max(2, Px(4)), r.width, Mathf.Max(2, Px(4))), c);
            Label(r, caption ?? EssenceInfo.DisplayName((Essence)essence), small ? ChipSmall : Chip, Color.white);
        }
    }

    /// <summary>Encounter key handling shared by the duel and the party: Esc consumed by an encounter never opens the pause menu.</summary>
    public static class EncounterKeys
    {
        private static int _escFrame = -1;

        /// <summary>Call when the encounter used Esc this frame (e.g. Return); the pause menu that Hud may open in the same frame is closed again.</summary>
        public static void ConsumeEscape()
        {
            _escFrame = Time.frameCount;
            EncounterEscGuard.Ensure();
        }

        internal static bool EscapeConsumedThisFrame => _escFrame == Time.frameCount;
    }

    /// <summary>Closes a pause menu opened by the same Esc press that an encounter consumed (Hud reads Esc independently).</summary>
    public sealed class EncounterEscGuard : MonoBehaviour
    {
        private static EncounterEscGuard _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var host = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.gameObject : null;
            if (host == null) return;
            _instance = host.GetComponent<EncounterEscGuard>();
            if (_instance == null) _instance = host.AddComponent<EncounterEscGuard>();
        }

        private void LateUpdate()
        {
            if (EncounterKeys.EscapeConsumedThisFrame && Hud.MenuOpen && Hud.Instance != null) Hud.Instance.CloseMenu();
        }
    }

    /// <summary>Optional tuning overrides from StreamingAssets/world/ward.json and rhythm.json.</summary>
    public static class EncounterTuningFiles
    {
        public static string Dir => Path.Combine(Application.streamingAssetsPath, "world");

        public static string ReadText(string file)
        {
            try
            {
                string p = Path.Combine(Dir, file);
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Encounters] could not read " + file + ": " + e.Message);
                return null;
            }
        }

        /// <summary>The practice tuning (WardTuning.Practice) with ward.json applied on top.</summary>
        public static WardTuning Ward()
        {
            try { return WardTuning.FromJson(ReadText("ward.json"), WardTuning.Practice()); }
            catch (Exception e) { Debug.LogWarning("[Encounters] ward.json ignored: " + e.Message); return WardTuning.Practice(); }
        }

        /// <summary>RhythmTuning defaults with rhythm.json applied on top.</summary>
        public static RhythmTuning Rhythm()
        {
            try { return RhythmTuning.FromJson(ReadText("rhythm.json")); }
            catch (Exception e) { Debug.LogWarning("[Encounters] rhythm.json ignored: " + e.Message); return new RhythmTuning(); }
        }
    }
}
