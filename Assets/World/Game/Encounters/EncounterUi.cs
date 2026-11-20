using System;
using System.Collections.Generic;
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
        private static float _compactFor = -1f;
        private static GUIStyle _huge, _big, _header, _body, _hint, _chip, _chipSmall, _button;

        /// <summary>
        /// Phone-size text (set by the Ward duel while it draws a compact layout, 0 = off): fonts are sized from this unit
        /// (1 = an 844 x 390 window) instead of the 1080p scale, so text stays readable and buttons stay finger-sized.
        /// </summary>
        public static float CompactUnit;

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
            if (_h == Screen.height && _compactFor == CompactUnit && _body != null) return;
            _h = Screen.height; _compactFor = CompactUnit;
            float u = CompactUnit;
            int F(float px1080, float compactPx) => u > 0 ? Mathf.RoundToInt(compactPx * u) : UiStyle.Px(px1080);
            var baseLabel = UiStyle.Label;
            _body = new GUIStyle(baseLabel) { fontSize = F(28, 15), wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            _body.normal.textColor = Text;
            _hint = new GUIStyle(_body) { fontSize = F(23, 13) };
            _hint.normal.textColor = Dim;
            _header = new GUIStyle(_body) { fontSize = F(34, 17), fontStyle = FontStyle.Bold };
            _header.normal.textColor = Gold;
            _big = new GUIStyle(_body) { fontSize = F(50, 26), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _huge = new GUIStyle(_big) { fontSize = F(120, 48) };
            _chip = new GUIStyle(_body) { fontSize = F(24, 14), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _chipSmall = new GUIStyle(_chip) { fontSize = F(20, 12) };
            _button = new GUIStyle(UiStyle.Button) { fontSize = F(28, 16), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
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

    /// <summary>
    /// Where every piece of the Ward duel's panel goes (PRD_CRYSTALS 5.12, I7). Pure arithmetic on a screen size and a safe
    /// area, so tests check the same rectangles the duel draws. Two layouts:
    /// <list type="bullet">
    /// <item>Desktop (height 700 px and up): the 1080p-authored layout as shipped (side panels with full histories, the weave
    /// builder at the bottom centre), scaled by <see cref="UiStyle.Scale"/>.</item>
    /// <item>Compact (landscape phones, e.g. 844 x 390, 932 x 430): inside the safe area with a margin; two short side
    /// cards at the top (name, casts, Ward, the last cast; a tap expands both histories), the title between them, and a
    /// thumb-reach builder at the bottom; every touch target at least <see cref="MinTouch"/> px on both sides.</item>
    /// </list>
    /// </summary>
    public sealed class DuelLayout
    {
        /// <summary>Smallest touch target side, in pixels of a phone-size window (1 px = 1 pt at 844 x 390).</summary>
        public const float MinTouch = 44f;
        /// <summary>Below this height (px) the compact layout is used.</summary>
        public const float CompactBelowHeight = 700f;

        /// <summary>Tests and captures: a simulated safe area in GUI coordinates (top-left origin); null = <see cref="Screen.safeArea"/>.</summary>
        public static Rect? SafeAreaOverride { get => ScreenSafeArea.Override; set => ScreenSafeArea.Override = value; }

        public bool Compact;
        /// <summary>Compact unit (1 at 844 x 390); 0 on the desktop layout.</summary>
        public float U;
        public float ScreenW, ScreenH;
        /// <summary>The safe area (GUI coordinates) and the area actually used (the safe area less a margin).</summary>
        public Rect Safe, Inner;

        public Rect Title, Help, CasterPanel, OpponentPanel, Builder, Go, Toast, Idle, Cue, Footer;
        public readonly Rect[] Slots = new Rect[3];
        public readonly Rect[] Essences = new Rect[4];
        public Rect Result, ResultRematch, ResultReturn;
        public Rect Stakes, StakesGo;
        public Rect HistoryOverlay, HistoryClose;
        /// <summary>Row height of a history row in the side cards (compact) or panels (desktop).</summary>
        public float RowH;

        /// <summary>The safe area of the current screen in GUI coordinates.</summary>
        public static Rect CurrentSafeArea()
        {
            return ScreenSafeArea.Current();
        }

        public static DuelLayout ForScreen(bool help) => Compute(Screen.width, Screen.height, CurrentSafeArea(), help);

        public static DuelLayout Compute(float w, float h, Rect safe, bool help)
        {
            var L = new DuelLayout { ScreenW = w, ScreenH = h, Safe = safe };
            if (h < CompactBelowHeight) L.ComputeCompact(help); else L.ComputeDesktop(help);
            return L;
        }

        private void ComputeDesktop(bool help)
        {
            float k = Mathf.Max(0.5f, ScreenH / 1080f);
            float px(float v) => Mathf.RoundToInt(v * k);
            float w = ScreenW, h = ScreenH;
            Rect Fit(Rect r)
            {
                float m = px(8);
                var s = Safe;
                r.width = Mathf.Min(r.width, s.width - 2 * m);
                r.height = Mathf.Min(r.height, s.height - 2 * m);
                r.x = Mathf.Clamp(r.x, s.x + m, s.xMax - m - r.width);
                r.y = Mathf.Clamp(r.y, s.y + m, s.yMax - m - r.height);
                return r;
            }
            Inner = Safe;
            RowH = px(46);
            Title = Fit(new Rect((w - px(1000)) / 2, px(14), px(1000), px(96)));
            CasterPanel = Fit(new Rect(px(20), px(124), px(510), px(700)));
            OpponentPanel = Fit(new Rect(w - px(20) - px(510), px(124), px(510), px(700)));
            var r = Builder = Fit(new Rect((w - px(760)) / 2, h - px(52) - px(232), px(760), px(232)));
            float x = r.x + px(20), y = r.y + px(14) + px(50);
            for (int i = 0; i < 3; i++) Slots[i] = new Rect(x + i * (px(140) + px(14)), y, px(140), px(64));
            Go = new Rect(r.xMax - px(20) - px(250), y, px(250), px(64));
            y += px(64) + px(18);
            float ew = (r.width - px(40) - px(10) * 3) / 4f;
            for (int i = 0; i < 4; i++) Essences[i] = new Rect(x + i * (ew + px(10)), y, ew, px(64));
            Help = Fit(new Rect(r.x, r.y - px(146), r.width, px(136)));
            Idle = Fit(new Rect(r.x, r.y - px(help ? 206 : 64), r.width, px(54)));
            Toast = Fit(new Rect((w - px(760)) / 2, px(124), px(760), px(56)));
            Cue = Fit(new Rect((w - px(760)) / 2, px(190), px(760), px(56)));
            Footer = new Rect(px(24), h - px(40), w - px(48), px(32));
            Result = Fit(new Rect((w - px(760)) / 2f, (h - px(500)) / 2f + px(60), px(760), px(500)));
            float rx = Result.x + px(30), rw = Result.width - px(60), bw = (rw - px(20)) / 2f, by = Result.yMax - px(86);
            ResultRematch = new Rect(rx, by, bw, px(64));
            ResultReturn = new Rect(rx + bw + px(20), by, bw, px(64));
            Stakes = Fit(new Rect((w - px(980)) / 2f, (h - px(440)) / 2f, px(980), px(440)));
            StakesGo = new Rect(Stakes.center.x - px(220), Stakes.yMax - px(30) - px(72), px(440), px(72));
            HistoryOverlay = Fit(new Rect((w - px(1200)) / 2f, px(124), px(1200), h - px(124) - px(300)));
            HistoryClose = new Rect(HistoryOverlay.xMax - px(20) - px(200), HistoryOverlay.yMax - px(16) - px(64), px(200), px(64));
        }

        private void ComputeCompact(bool help)
        {
            Compact = true;
            float u = U = Mathf.Clamp(Mathf.Min(ScreenH / 390f, ScreenW / 844f), 1f, 1.6f);
            float m = 6f * u, g = 6f * u;
            var S = Inner = new Rect(Safe.x + m, Safe.y + m, Safe.width - 2 * m, Safe.height - 2 * m);
            RowH = 30f * u;

            float sideW = Mathf.Clamp(S.width * 0.27f, 176f * u, 300f * u);
            float sideH = 112f * u;
            CasterPanel = new Rect(S.x, S.y, sideW, sideH);
            OpponentPanel = new Rect(S.xMax - sideW, S.y, sideW, sideH);
            float midX = S.x + sideW + g, midW = S.width - 2 * (sideW + g);
            Title = new Rect(midX, S.y, midW, 44f * u);
            Help = new Rect(midX, S.y + 48f * u, midW, 60f * u);

            float bw = Mathf.Min(S.width, 600f * u), bh = 6f * u + 48f * u + 6f * u + 50f * u + 6f * u;
            var r = Builder = new Rect(S.center.x - bw / 2f, S.yMax - bh, bw, bh);
            float slotW = 62f * u, y = r.y + 6f * u;
            for (int i = 0; i < 3; i++) Slots[i] = new Rect(r.x + 6f * u + i * (slotW + 6f * u), y, slotW, 48f * u);
            float goX = Slots[2].xMax + 10f * u;
            Go = new Rect(goX, y, r.xMax - 6f * u - goX, 48f * u);
            y += 48f * u + 6f * u;
            float ew = (r.width - 12f * u - 3 * 6f * u) / 4f;
            for (int i = 0; i < 4; i++) Essences[i] = new Rect(r.x + 6f * u + i * (ew + 6f * u), y, ew, 50f * u);

            float tw = Mathf.Min(S.width, 520f * u);
            Toast = new Rect(S.center.x - tw / 2f, r.y - 4f * u - 30f * u, tw, 30f * u);
            Idle = new Rect(S.center.x - tw / 2f, Toast.y - 4f * u - 32f * u, tw, 32f * u);
            float cw = Mathf.Min(midW + 2 * g, 440f * u);
            Cue = new Rect(S.center.x - cw / 2f, S.y + (help ? 112f : 52f) * u, cw, 30f * u);
            Footer = Rect.zero;

            float rw = Mathf.Min(S.width, 620f * u);
            Result = new Rect(S.center.x - rw / 2f, S.y, rw, S.height);
            float rbw = (rw - 16f * u - 8f * u) / 2f, rby = Result.yMax - 8f * u - 48f * u;
            ResultRematch = new Rect(Result.x + 8f * u, rby, rbw, 48f * u);
            ResultReturn = new Rect(ResultRematch.xMax + 8f * u, rby, rbw, 48f * u);

            float sw = Mathf.Min(S.width, 600f * u), sh = Mathf.Min(S.height, 270f * u);
            Stakes = new Rect(S.center.x - sw / 2f, S.center.y - sh / 2f, sw, sh);
            StakesGo = new Rect(Stakes.center.x - 140f * u, Stakes.yMax - 8f * u - 50f * u, 280f * u, 50f * u);

            float hw = Mathf.Min(S.width, 720f * u);
            HistoryOverlay = new Rect(S.center.x - hw / 2f, S.y, hw, S.height);
            HistoryClose = new Rect(HistoryOverlay.xMax - 8f * u - 130f * u, HistoryOverlay.yMax - 8f * u - 46f * u, 130f * u, 46f * u);
        }

        /// <summary>Every touch target of the panel (name, rect): the ones a finger must hit.</summary>
        public List<KeyValuePair<string, Rect>> TouchTargets()
        {
            var l = new List<KeyValuePair<string, Rect>>
            {
                Kv("go", Go), Kv("rematch", ResultRematch), Kv("return", ResultReturn), Kv("stakes", StakesGo), Kv("history close", HistoryClose),
            };
            for (int i = 0; i < 3; i++) l.Add(Kv("slot " + i, Slots[i]));
            for (int i = 0; i < 4; i++) l.Add(Kv("essence " + i, Essences[i]));
            if (Compact) { l.Add(Kv("caster card", CasterPanel)); l.Add(Kv("opponent card", OpponentPanel)); }
            return l;
        }

        /// <summary>Every drawn rectangle (name, rect).</summary>
        public List<KeyValuePair<string, Rect>> AllRects()
        {
            var l = TouchTargets();
            l.Add(Kv("title", Title)); l.Add(Kv("help", Help)); l.Add(Kv("caster panel", CasterPanel)); l.Add(Kv("opponent panel", OpponentPanel));
            l.Add(Kv("builder", Builder)); l.Add(Kv("toast", Toast)); l.Add(Kv("idle", Idle)); l.Add(Kv("cue", Cue));
            l.Add(Kv("result", Result)); l.Add(Kv("stakes card", Stakes)); l.Add(Kv("history", HistoryOverlay));
            return l;
        }

        private static KeyValuePair<string, Rect> Kv(string n, Rect r) => new KeyValuePair<string, Rect>(n, r);
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
