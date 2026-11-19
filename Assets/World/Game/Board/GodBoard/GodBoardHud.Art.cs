using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// G1-6b: the god board HUD's own small art and drawing primitives (our own pictures, drawn in code): round buttons,
    /// glyphs (hourglass, pieces, arrows), goods cards, rings that drain. Everything is IMGUI in design pixels (844 x 390).
    /// </summary>
    public sealed partial class GodBoardHud
    {
        public const int GHourglass = 0, GHouse = 1, GCity = 2, GRoad = 3, GCard = 4, GHelmet = 5, GDroplet = 6, GSwap = 7, GCheck = 8, GCross = 9, GMinus = 10, GPlus = 11, GBulb = 12, GScroll = 13, GCount = 14;
        private static readonly string[] GlyphNames = { "hourglass", "camp", "city", "road", "cards", "soldiers", "plague", "trade", "ok", "cancel", "less", "more", "hint", "log" };

        private Texture2D[] _glyphs;
        private Texture2D _discTex;

        private static float Seg(float x, float y, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy));
            float px = x0 + t * dx - x, py = y0 + t * dy - y;
            return Mathf.Sqrt(px * px + py * py);
        }

        private static bool GlyphPixel(int id, float x, float y)
        {
            switch (id)
            {
                case GHourglass:
                {
                    if (y > 26 && y < 29.5f && x > 7 && x < 25) return true;
                    if (y > 2.5f && y < 6 && x > 7 && x < 25) return true;
                    if (y < 6 || y > 26) return false;
                    float w = Mathf.Abs(y - 16f) * 0.62f + 1.2f;
                    return Mathf.Abs(Mathf.Abs(x - 16f) - w) < 1.3f;
                }
                case GHouse:
                    if (x > 8 && x < 24 && y > 4 && y < 15 && !(x > 14 && x < 18 && y < 10)) return true;
                    return y >= 14 && y < 27 && Mathf.Abs(x - 16f) < (27f - y) * 1.15f;
                case GCity:
                    if (x > 4 && x < 28 && y > 4 && y < 14) return true;
                    if (x > 16 && x < 28 && y >= 14 && y < 27) return true;
                    return y >= 13 && y < 22 && x < 16 && Mathf.Abs(x - 9f) < (22f - y) * 1.0f;
                case GRoad:
                    return Seg(x, y, 5, 9, 27, 23) < 3.4f && !(Seg(x, y, 8, 11, 24, 21) < 0.7f && ((int)(x * 0.5f) % 2 == 0));
                case GCard:
                    return x > 8 && x < 24 && y > 4 && y < 28;
                case GHelmet:
                    if (y >= 13 && (x - 16f) * (x - 16f) + (y - 13f) * (y - 13f) < 100f) return true;
                    return y > 8 && y < 13 && x > 4 && x < 28;
                case GDroplet:
                    if ((x - 16f) * (x - 16f) + (y - 11f) * (y - 11f) < 64f) return true;
                    return y >= 11 && y < 28 && Mathf.Abs(x - 16f) < (28f - y) * 0.47f;
                case GSwap:
                    return Seg(x, y, 6, 21, 25, 21) < 1.5f || Seg(x, y, 25, 21, 18, 26) < 1.5f || Seg(x, y, 25, 21, 18, 16) < 1.5f
                        || Seg(x, y, 7, 11, 26, 11) < 1.5f || Seg(x, y, 7, 11, 14, 16) < 1.5f || Seg(x, y, 7, 11, 14, 6) < 1.5f;
                case GCheck:
                    return Seg(x, y, 6, 16, 13, 8) < 2.6f || Seg(x, y, 13, 8, 27, 25) < 2.6f;
                case GCross:
                    return Seg(x, y, 7, 7, 25, 25) < 2.6f || Seg(x, y, 7, 25, 25, 7) < 2.6f;
                case GMinus:
                    return Seg(x, y, 8, 16, 24, 16) < 2.6f;
                case GPlus:
                    return Seg(x, y, 8, 16, 24, 16) < 2.6f || Seg(x, y, 16, 8, 16, 24) < 2.6f;
                case GBulb:
                    if ((x - 16f) * (x - 16f) + (y - 17f) * (y - 17f) < 60f) return true;
                    return x > 12 && x < 20 && y > 5 && y < 11;
                default: // a scroll: three lines
                    return x > 6 && x < 26 && (Mathf.Abs(y - 9f) < 1.6f || Mathf.Abs(y - 16f) < 1.6f || Mathf.Abs(y - 23f) < 1.6f);
            }
        }

        private Texture2D Glyph(int id)
        {
            if (_glyphs == null) _glyphs = new Texture2D[GCount];
            if (_glyphs[id] != null) return _glyphs[id];
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++) if (GlyphPixel(id, x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f)) hit++;
                    px[y * N + x] = new Color(1, 1, 1, hit / 4f);
                }
            tex.SetPixels(px); tex.Apply();
            return _glyphs[id] = tex;
        }

        private Texture2D DiscTexture()
        {
            if (_discTex != null) return _discTex;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - N / 2f) * (x + 0.5f - N / 2f) + (y + 0.5f - N / 2f) * (y + 0.5f - N / 2f));
                    px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(N / 2f - d));
                }
            tex.SetPixels(px); tex.Apply();
            return _discTex = tex;
        }

        private void ArtDestroy()
        {
            if (_glyphs != null) foreach (var t in _glyphs) if (t != null) Destroy(t);
            if (_discTex != null) Destroy(_discTex);
        }

        // ---------------------------------------------------------------- primitives

        private void Disc(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, DiscTexture()); GUI.color = old; }

        private void GlyphAt(Rect r, int id, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Glyph(id)); GUI.color = old; }

        private static Rect Sub(Rect r, float u, float dx, float dy, float w, float h) => new Rect(r.x + dx * u, r.y + dy * u, w * u, h * u);

        /// <summary>An arc (a ring section) of the given radius and thickness, clockwise from the top, covering <paramref name="frac"/> of the circle.</summary>
        private void Arc(Vector2 c, float radius, float thick, float frac, Color col)
        {
            const int N = 64;
            int n = Mathf.RoundToInt(Mathf.Clamp01(frac) * N);
            if (n <= 0) return;
            var old = GUI.matrix; var oc = GUI.color; GUI.color = col;
            float seg = 2f * Mathf.PI * radius / N * 1.25f;
            var tex = UiStyle.SolidTexture(Color.white);
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) * 360f / N;
                float rad = a * Mathf.Deg2Rad;
                var p = new Vector2(c.x + radius * Mathf.Sin(rad), c.y - radius * Mathf.Cos(rad));
                GUI.matrix = old;
                GUIUtility.RotateAroundPivot(a, p);
                GUI.DrawTexture(new Rect(p.x - seg * 0.5f, p.y - thick * 0.5f, seg, thick), tex);
            }
            GUI.matrix = old; GUI.color = oc;
        }

        /// <summary>A round button with a glyph. Returns true when pressed (never while a modal is up unless forced).</summary>
        private bool RoundButton(Rect r, int glyph, string name, Color bg, bool enabled, bool lit = false, bool force = false, string text = null)
        {
            _rects.Add(r);
            float pad = r.width * 0.2f;
            var shade = new Color(0f, 0f, 0f, 0.5f);
            Disc(new Rect(r.x + 1f, r.y + 2f * _u, r.width, r.height), shade);
            Disc(r, enabled ? bg : new Color(bg.r * 0.45f, bg.g * 0.45f, bg.b * 0.45f, bg.a));
            if (lit) { Disc(new Rect(r.x - 2f * _u, r.y - 2f * _u, r.width + 4f * _u, r.height + 4f * _u), Gold); Disc(r, enabled ? bg : bg * 0.45f); }
            var ink = enabled ? new Color(1f, 0.97f, 0.88f) : new Color(0.75f, 0.72f, 0.66f);
            if (glyph >= 0) GlyphAt(new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad), glyph, ink);
            if (text != null) Text(r, text, r.width / _u * 0.5f, ink, true, TextAnchor.MiddleCenter);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            Texts.Add("[" + name + "]");
            return hit && enabled && (!_under || force);
        }

        /// <summary>A goods card: the picture, a count badge top right. Dim when <paramref name="count"/> is 0 and <paramref name="dimEmpty"/>.</summary>
        private void GoodsCard(Rect r, int good, int count, bool dim, bool lifted = false, bool badge = true, Color? badgeColour = null)
        {
            var c = GoodColours[good];
            FillNoHit(new Rect(r.x - 1.5f * _u, r.y - 1.5f * _u, r.width + 3f * _u, r.height + 3f * _u), lifted ? Gold : new Color(0.96f, 0.92f, 0.82f));
            FillNoHit(r, new Color(c.r * 0.32f + 0.08f, c.g * 0.32f + 0.08f, c.b * 0.32f + 0.08f, 1f));
            FillNoHit(new Rect(r.x, r.y, r.width, r.height * 0.13f), c);
            float s = Mathf.Min(r.width * 0.72f, r.height * 0.5f);
            GUI.DrawTexture(new Rect(r.x + (r.width - s) * 0.5f, r.y + r.height * 0.26f, s, s), IconOf(good));
            if (badge)
            {
                float b = Mathf.Min(r.width * 0.46f, 17f * _u);
                var br = new Rect(r.xMax - b * 0.75f, r.y - b * 0.3f, b, b);
                Disc(new Rect(br.x - 1f, br.y - 1f, br.width + 2f, br.height + 2f), new Color(0.96f, 0.92f, 0.82f));
                Disc(br, badgeColour ?? new Color(0.14f, 0.12f, 0.10f));
                Text(br, count.ToString(), b / _u * 0.62f, UiStyle.TextColor, true, TextAnchor.MiddleCenter);
            }
            if (dim) FillNoHit(new Rect(r.x - 1.5f * _u, r.y - 1.5f * _u, r.width + 3f * _u, r.height + 3f * _u), new Color(0.05f, 0.04f, 0.04f, 0.58f));
        }

        private Color PeopleCol(int p)
        {
            foreach (var q in _d.S.Peoples) if (q.Id == p) return TempleView.PeopleColour(q);
            return Color.white;
        }
    }
}
