using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// WU-6 (WORLD_UI_PLAN 2.6 CRY-MOVE, owner Q-5): four faint carved stones lying in the touch band, one per strip:
    /// turn left, step forward, step back, turn right. They are the same four moves on the hex lattice (no strafe, no
    /// quarter turns) and only show where the band is. Shown underground and for the first minutes on the surface;
    /// <see cref="Mode"/> (saved) can keep them always or hide them. A stone lights while its strip is held. Never
    /// drawn in God View, a menu, the map, or an encounter (the strips are drums there).
    /// </summary>
    public sealed class FootprintStones : MonoBehaviour
    {
        public enum StoneMode { Auto = 0, Always = 1, Never = 2 }

        public const string PrefKey = "World.UI.FootprintStones";
        /// <summary>Real seconds on the surface during which Auto shows the stones.</summary>
        public static float EarlySeconds = 180f;

        public static FootprintStones Instance { get; private set; }

        public static StoneMode Mode
        {
            get { try { return (StoneMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2); } catch { return StoneMode.Auto; } }
            set { try { PlayerPrefs.SetInt(PrefKey, (int)value); } catch { } }
        }

        private Texture2D[] _tex;
        private float _since;
        private float _alpha;

        /// <summary>The four stones' rectangles last drawn or computed (GUI coordinates, top-left origin), index 0 = strip 1.</summary>
        public Rect[] Stones { get; } = new Rect[4];
        /// <summary>True while the stones are drawn.</summary>
        public bool Visible { get; private set; }

        public static FootprintStones Create(Transform parent)
        {
            var go = new GameObject("Footprint stones");
            go.transform.SetParent(parent, false);
            WorldZone.Tag(go, WorldZones.CryMove);
            return go.AddComponent<FootprintStones>();
        }

        private void Awake() { Instance = this; _since = Time.unscaledTime; }
        private void OnDestroy() { if (Instance == this) Instance = null; if (_tex != null) foreach (var t in _tex) if (t != null) Destroy(t); }

        /// <summary>Whether the stones should be shown now (the rule, separate from the drawing).</summary>
        public bool ShouldShow()
        {
            if (PlayerWalker.Instance == null) return false;
            if (Hud.MenuOpen || MapScreen.IsOpen) return false;
            if (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God) return false;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return false;
            // P2a (WU-8 open problem 3): not while the camera rises to the god board, while it is up, nor while it falls back
            if (GodBoardDirector.Instance != null && GodBoardDirector.Instance.View != BoardView.Altar) return false;
            switch (Mode)
            {
                case StoneMode.Never: return false;
                case StoneMode.Always: return true;
                default: return Interiors.Current != null || Time.unscaledTime - _since < EarlySeconds;
            }
        }

        /// <summary>The four stone rectangles for a screen (GUI coordinates), each inside its quarter of the band and the safe area.</summary>
        public static Rect[] Layout(float w, float h, Rect safe)
        {
            var r = new Rect[4];
            float band = h * TouchStrips.BandFraction;
            float side = Mathf.Min(w / 4f * 0.62f, band * 0.7f);
            for (int i = 0; i < 4; i++)
            {
                float cx = (i + 0.5f) * w / 4f;
                cx = Mathf.Clamp(cx, safe.xMin + side / 2f, safe.xMax - side / 2f);
                float cy = h - band * 0.5f;
                cy = Mathf.Min(cy, safe.yMax - side / 2f);
                r[i] = new Rect(cx - side / 2f, cy - side / 2f, side, side);
            }
            return r;
        }

        private void Update()
        {
            Visible = ShouldShow();
            _alpha = Mathf.MoveTowards(_alpha, Visible ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (Screen.width > 0) { var l = Layout(Screen.width, Screen.height, ScreenSafeArea.Current()); for (int i = 0; i < 4; i++) Stones[i] = l[i]; }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _alpha <= 0.01f) return;
            if (_tex == null) _tex = BuildTextures();
            GUI.depth = 20;
            var old = GUI.color;
            for (int i = 0; i < 4; i++)
            {
                bool held = TouchStrips.Held(i + 1);
                GUI.color = new Color(1f, held ? 0.95f : 0.9f, held ? 0.7f : 0.82f, _alpha * (held ? 0.85f : 0.34f));
                GUI.DrawTexture(Stones[i], _tex[i], ScaleMode.ScaleToFit, true);
            }
            GUI.color = old;
        }

        // ---------------- the carved textures ----------------

        private static Texture2D[] BuildTextures()
        {
            var t = new Texture2D[4];
            t[0] = Slab(Glyph.TurnLeft); t[1] = Slab(Glyph.Foot); t[2] = Slab(Glyph.FootBack); t[3] = Slab(Glyph.TurnRight);
            return t;
        }

        private enum Glyph { TurnLeft, Foot, FootBack, TurnRight }
        private const int N = 128;

        private static Texture2D Slab(Glyph g)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Footprint stone " + g };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    // a rounded slab
                    float d = RoundBox(u, v, 0.86f, 0.86f, 0.28f);
                    if (d > 0.02f) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                    float edge = Mathf.Clamp01(-d / 0.10f);
                    float grain = 0.5f + 0.5f * Mathf.Sin(u * 19f + v * 7f) * Mathf.Sin(v * 23f - u * 5f);
                    float b = Mathf.Lerp(0.34f, 0.5f, edge) + grain * 0.04f;
                    float carve = GlyphMask(g, u, v);
                    if (carve > 0f) b = Mathf.Lerp(b, 0.12f, carve);
                    byte c = (byte)Mathf.Clamp(b * 255f, 0, 255);
                    px[y * N + x] = new Color32(c, (byte)(c * 0.96f), (byte)(c * 0.88f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) / 0.12f);
        }

        private static float GlyphMask(Glyph g, float u, float v)
        {
            switch (g)
            {
                case Glyph.Foot: return Footprint(u, v);
                case Glyph.FootBack: return Footprint(u, -v);
                case Glyph.TurnRight: return Turn(u, v);
                default: return Turn(-u, v);
            }
        }

        /// <summary>A bare footprint, toes up (v is up).</summary>
        private static float Footprint(float u, float v)
        {
            float m = Ellipse(u, v, 0.02f, -0.22f, 0.24f, 0.40f);
            m = Mathf.Max(m, Ellipse(u, v, -0.17f, 0.40f, 0.075f, 0.09f));
            m = Mathf.Max(m, Ellipse(u, v, -0.05f, 0.47f, 0.07f, 0.085f));
            m = Mathf.Max(m, Ellipse(u, v, 0.08f, 0.47f, 0.065f, 0.08f));
            m = Mathf.Max(m, Ellipse(u, v, 0.20f, 0.42f, 0.06f, 0.07f));
            m = Mathf.Max(m, Ellipse(u, v, 0.29f, 0.33f, 0.05f, 0.06f));
            return m;
        }

        /// <summary>A clockwise arc with an arrowhead (a turn to the right).</summary>
        private static float Turn(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(u, v) * Mathf.Rad2Deg; // 0 = up, clockwise positive
            float m = 0f;
            if (ang > -120f && ang < 60f) m = Mathf.Clamp01((0.11f - Mathf.Abs(r - 0.48f)) / 0.04f);
            // arrowhead at the end of the arc (ang 60): a triangle pointing along the tangent
            float a = 60f * Mathf.Deg2Rad;
            float ex = Mathf.Sin(a) * 0.48f, ey = Mathf.Cos(a) * 0.48f;
            float tx = Mathf.Cos(a), ty = -Mathf.Sin(a); // clockwise tangent
            float px = u - ex, py = v - ey;
            float along = px * tx + py * ty, across = Mathf.Abs(-px * ty + py * tx);
            if (along > -0.05f && along < 0.26f && across < 0.22f * (0.26f - along) / 0.31f + 0.0f) m = 1f;
            return m;
        }
    }
}
