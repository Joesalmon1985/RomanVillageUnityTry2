using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>What a <see cref="LedgerCard"/> says: a title, lines of our own text and, while previewing, the question.
    /// Callers give words and counts only (section 3 of WORLD_UI_PLAN); never dice, number tokens, roll totals or odds (AB-1).</summary>
    public sealed class LedgerContent
    {
        public string Title;
        public readonly List<string> Lines = new List<string>();
        /// <summary>The preview question ("Sacrifice a lamb?"), or null.</summary>
        public string Question;
        /// <summary>Why the action is refused or closed, or null.</summary>
        public string Refusal;

        /// <summary>Every line in reading order (title, lines, question, refusal): the same text the Clear view layer reads.</summary>
        public IEnumerable<string> AllText()
        {
            if (!string.IsNullOrEmpty(Title)) yield return Title;
            foreach (var l in Lines) yield return l;
            if (!string.IsNullOrEmpty(Question)) yield return Question;
            if (!string.IsNullOrEmpty(Refusal)) yield return Refusal;
        }

        public static LedgerContent From(IInspectable thing, string question, string refusal)
        {
            var c = new LedgerContent { Question = question, Refusal = refusal };
            if (thing != null)
            {
                c.Title = thing.Title;
                var lines = thing.Lines;
                if (lines != null) foreach (var l in lines) if (!string.IsNullOrEmpty(l)) c.Lines.Add(l);
            }
            return c;
        }
    }

    /// <summary>AB-1 guard for ledger text: words that would speak of dice, pips or odds.</summary>
    public static class LedgerText
    {
        private static readonly string[] Forbidden = { "%", "odds", "chance", "probab", "dice", " die ", "pips", "roll total", "number token" };

        /// <summary>The first forbidden word found in the text, or null.</summary>
        public static string Violation(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var t = " " + text.ToLowerInvariant() + " ";
            foreach (var f in Forbidden) if (t.Contains(f)) return f.Trim();
            return null;
        }

        public static string Violation(LedgerContent c)
        {
            if (c == null) return null;
            foreach (var l in c.AllText()) { var v = Violation(l); if (v != null) return v; }
            return null;
        }
    }

    public enum LedgerMode { Inspect, Preview, Refused }

    /// <summary>Where the card goes on a screen, and its text size (pure; EditMode and PlayMode tests check it at 844 x 390).</summary>
    public struct LedgerLayout
    {
        public Rect Card;
        public int FontSize, TitleSize;
        public float Pad;

        /// <summary>Smallest body text in pixels (readable on a 390 px tall phone screen).</summary>
        public const int MinFont = 15;
        public const int MaxFont = 30;

        /// <summary>The card for a screen, its safe area (GUI coordinates), the anchor's screen point (GUI, or null) and its text.</summary>
        public static LedgerLayout Compute(float screenW, float screenH, Rect safe, Vector2? anchor, LedgerContent content)
        {
            var L = new LedgerLayout();
            L.FontSize = Mathf.Clamp(Mathf.RoundToInt(screenH * 0.042f), MinFont, MaxFont);
            L.TitleSize = Mathf.RoundToInt(L.FontSize * 1.25f);
            L.Pad = Mathf.Round(L.FontSize * 0.7f);
            float w = Mathf.Min(safe.width * 0.48f, L.FontSize * 26f);
            w = Mathf.Max(w, Mathf.Min(safe.width - 2 * L.Pad, L.FontSize * 14f));
            float charsPerLine = Mathf.Max(8f, (w - 2 * L.Pad) / (L.FontSize * 0.52f));
            float lines = 0;
            if (content != null)
                foreach (var l in content.Lines) lines += Mathf.Max(1f, Mathf.Ceil(l.Length / charsPerLine));
            float extra = 0;
            if (content != null && !string.IsNullOrEmpty(content.Question)) extra += Mathf.Ceil(content.Question.Length / charsPerLine) * L.FontSize * 1.35f + L.Pad * 0.5f;
            if (content != null && !string.IsNullOrEmpty(content.Refusal)) extra += Mathf.Ceil(content.Refusal.Length / charsPerLine) * L.FontSize * 1.35f + L.Pad * 0.5f;
            float h = L.Pad * 2 + L.TitleSize * 1.4f + lines * L.FontSize * 1.35f + extra;
            h = Mathf.Max(h, WorldHitTarget.MinSide(screenH));
            h = Mathf.Min(h, safe.height - 2 * L.Pad);
            // Beside the anchor, on the side with more room; centred when no anchor.
            float x, y;
            if (anchor.HasValue)
            {
                var a = anchor.Value;
                bool right = a.x < safe.center.x;
                x = right ? a.x + L.Pad * 2 : a.x - L.Pad * 2 - w;
                y = a.y - h * 0.5f;
            }
            else { x = safe.center.x - w / 2; y = safe.center.y - h / 2; }
            x = Mathf.Clamp(x, safe.xMin + L.Pad, safe.xMax - L.Pad - w);
            y = Mathf.Clamp(y, safe.yMin + L.Pad, safe.yMax - L.Pad - h);
            L.Card = new Rect(x, y, w, h);
            return L;
        }
    }

    /// <summary>
    /// The one in-world readable tablet of WU-0: it unfolds from the tapped object (a tether from the object to a
    /// stone-coloured tablet beside it), holds the caller's title and lines, and while previewing the question of the
    /// action. Tapping the tablet in a preview confirms (as tapping the object again); tapping elsewhere closes it.
    /// The content is public (<see cref="Content"/>, <see cref="Changed"/>) so the future Clear view layer shows the
    /// same text. Drawn by <see cref="LedgerCardView"/>.
    /// </summary>
    public static class LedgerCard
    {
        public static LedgerContent Content { get; private set; }
        public static LedgerMode Mode { get; private set; }
        public static Transform Anchor { get; private set; }
        public static bool IsOpen => Content != null;
        /// <summary>Unscaled time the card was opened (for the unfold).</summary>
        public static float OpenedAt { get; private set; }
        /// <summary>The card's rect drawn last frame (GUI coordinates); empty when closed.</summary>
        public static Rect LastRect { get; internal set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Content = null; Anchor = null; Changed = null; LastRect = default; }

        public static void Show(LedgerContent content, Transform anchor, LedgerMode mode)
        {
#if UNITY_EDITOR
            var v = LedgerText.Violation(content);
            if (v != null) Debug.LogWarning("LedgerCard: text speaks of '" + v + "' (AB-1): " + string.Join(" / ", content.AllText()));
#endif
            Content = content; Anchor = anchor; Mode = mode; OpenedAt = Time.unscaledTime;
            Changed?.Invoke();
        }

        public static void Hide()
        {
            if (Content == null) return;
            Content = null; Anchor = null; LastRect = default;
            Changed?.Invoke();
        }

        /// <summary>True when a GUI point is on the open card.</summary>
        public static bool Contains(Vector2 gui) => IsOpen && LastRect.width > 0 && LastRect.Contains(gui);

        /// <summary>The layout for the current screen.</summary>
        public static LedgerLayout CurrentLayout(Camera cam)
        {
            Vector2? a = null;
            if (Anchor != null && cam != null)
            {
                var s = cam.WorldToScreenPoint(Anchor.position);
                if (s.z > 0) a = new Vector2(s.x, Screen.height - s.y);
            }
            return LedgerLayout.Compute(Screen.width, Screen.height, ScreenSafeArea.Current(), a, Content);
        }
    }

    /// <summary>The simulated or real safe area for every screen layout (generalised from <see cref="DuelLayout.SafeAreaOverride"/>).</summary>
    public static class ScreenSafeArea
    {
        /// <summary>Tests and captures: a simulated safe area in GUI coordinates (top-left origin); null = <see cref="Screen.safeArea"/>.</summary>
        public static Rect? Override;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Override = null; }

        public static Rect Current()
        {
            if (Override.HasValue) return Override.Value;
            var sa = Screen.safeArea;
            if (sa.width <= 0 || sa.height <= 0) return new Rect(0, 0, Screen.width, Screen.height);
            return new Rect(sa.x, Screen.height - (sa.y + sa.height), sa.width, sa.height);
        }

        /// <summary>A landscape phone's notch safe area scaled to a screen: the side insets of a 844 x 390 phone
        /// (47 px each side, 21 px at the bottom for the home bar), in GUI coordinates.</summary>
        public static Rect SimulatedNotch(float w, float h)
        {
            float k = h / 390f;
            float side = Mathf.Round(47f * k), bottom = Mathf.Round(21f * k);
            return new Rect(side, 0, w - 2 * side, h - bottom);
        }
    }

    /// <summary>Draws the <see cref="LedgerCard"/>. Added by <see cref="PlayerInstaller"/>.</summary>
    public sealed class LedgerCardView : MonoBehaviour
    {
        public const float UnfoldSeconds = 0.18f;
        private static readonly Color Stone = new Color(0.24f, 0.21f, 0.18f, 0.95f);
        private static readonly Color Rim = new Color(0.62f, 0.52f, 0.36f, 1f);
        private static readonly Color Ink = new Color(0.97f, 0.93f, 0.84f, 1f);
        private static readonly Color Ask = new Color(1f, 0.82f, 0.4f, 1f);
        private static readonly Color No = new Color(1f, 0.62f, 0.5f, 1f);
        private Texture2D _stone, _rim;
        private GUIStyle _title, _body, _ask, _no;

        private void OnGUI()
        {
            if (!LedgerCard.IsOpen) return;
            var c = LedgerCard.Content;
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            var L = LedgerCard.CurrentLayout(cam);
            if (_stone == null) { _stone = UiStyle.SolidTexture(Stone); _rim = UiStyle.SolidTexture(Rim); }
            if (_title == null || _body.fontSize != L.FontSize)
            {
                _body = new GUIStyle(GUI.skin.label) { fontSize = L.FontSize, wordWrap = true, richText = false };
                _body.normal.textColor = Ink;
                _title = new GUIStyle(_body) { fontSize = L.TitleSize, fontStyle = FontStyle.Bold };
                _ask = new GUIStyle(_body) { fontStyle = FontStyle.Bold }; _ask.normal.textColor = Ask;
                _no = new GUIStyle(_body); _no.normal.textColor = No;
            }
            GUI.depth = -8;
            // Unfold: the tablet grows from the anchor.
            float t = Mathf.Clamp01((Time.unscaledTime - LedgerCard.OpenedAt) / UnfoldSeconds);
            var full = L.Card;
            Vector2 from = full.center;
            if (LedgerCard.Anchor != null && cam != null)
            {
                var s = cam.WorldToScreenPoint(LedgerCard.Anchor.position);
                if (s.z > 0) from = new Vector2(s.x, Screen.height - s.y);
            }
            var r = new Rect(Vector2.Lerp(from, full.position, t), full.size * Mathf.Max(0.05f, t));
            if (from != full.center)
            {
                // Tether from the object to the tablet.
                var mid = new Vector2(full.center.x < from.x ? full.xMax : full.xMin, full.center.y);
                DrawLine(from, mid, Mathf.Max(2f, L.FontSize * 0.15f));
            }
            float rimW = Mathf.Max(2f, L.FontSize * 0.18f);
            GUI.DrawTexture(new Rect(r.x - rimW, r.y - rimW, r.width + 2 * rimW, r.height + 2 * rimW), _rim);
            GUI.DrawTexture(r, _stone);
            LedgerCard.LastRect = full;
            if (t < 1f) return;
            float x = r.x + L.Pad, w = r.width - 2 * L.Pad, y = r.y + L.Pad;
            if (!string.IsNullOrEmpty(c.Title)) { float h = _title.CalcHeight(new GUIContent(c.Title), w); GUI.Label(new Rect(x, y, w, h), c.Title, _title); y += h + L.Pad * 0.3f; }
            foreach (var l in c.Lines) { float h = _body.CalcHeight(new GUIContent(l), w); GUI.Label(new Rect(x, y, w, h), l, _body); y += h; }
            if (!string.IsNullOrEmpty(c.Question)) { y += L.Pad * 0.4f; float h = _ask.CalcHeight(new GUIContent(c.Question), w); GUI.Label(new Rect(x, y, w, h), c.Question, _ask); y += h; }
            if (!string.IsNullOrEmpty(c.Refusal)) { y += L.Pad * 0.4f; float h = _no.CalcHeight(new GUIContent(c.Refusal), w); GUI.Label(new Rect(x, y, w, h), c.Refusal, _no); }
        }

        private void DrawLine(Vector2 a, Vector2 b, float width)
        {
            var m = GUI.matrix;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float len = (b - a).magnitude;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2, len, width), _rim);
            GUI.matrix = m;
        }
    }
}
