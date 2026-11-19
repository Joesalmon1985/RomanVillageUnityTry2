using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The god board's on-screen controls (GB-3), the start of G1-6: the turn ribbon with points per people, the player's hand
    /// (counts per good with icons, never numbers on hexes), the hourglass as a shrinking bar, "End turn" and "Return to the
    /// world", the adviser switch, zoom buttons, the event feed and a status line. IMGUI, laid out for a 390 px tall phone
    /// landscape (844 x 390) and scaled by Screen.height / 390; all targets are at least 28 design pixels tall (about 28 px at
    /// 844 x 390). Mouse and touch both work (IMGUI takes touches as presses).
    /// G1-6 fills <see cref="TradePanel"/> (trade panel, offer cards, cost card) into <see cref="TradeArea"/>.
    /// </summary>
    public sealed class GodBoardHud : MonoBehaviour
    {
        /// <summary>The free region (design pixels from the right and bottom edges) reserved for the trade panel and offer cards (G1-6).</summary>
        public static readonly Vector2 TradeAreaSize = new Vector2(230f, 150f);
        /// <summary>G1-6 hook: draws the trade panel into the given rect (screen pixels, GUI space). Null until G1-6.</summary>
        public static Action<Rect> TradePanel;

        public static readonly Color[] GoodColours =
        {
            new Color(0.36f, 0.62f, 0.30f), new Color(0.80f, 0.45f, 0.25f), new Color(0.92f, 0.78f, 0.30f), new Color(0.93f, 0.93f, 0.90f), new Color(0.58f, 0.60f, 0.66f),
        };
        public static readonly string[] GoodNames = { "Wood", "Clay", "Grain", "Sheep", "Stone" };

        private GodBoardDirector _d;
        private readonly List<Rect> _rects = new List<Rect>();
        private Texture2D[] _icons;
        private GUIStyle _label, _bold, _button;
        private float _u = 1f;

        /// <summary>The rects drawn last frame (GUI space, y from the top): presses inside them are not board presses.</summary>
        public IReadOnlyList<Rect> Rects => _rects;
        /// <summary>Texts drawn last frame (tests and captures).</summary>
        public readonly List<string> Texts = new List<string>();

        private void Awake() { _d = GetComponent<GodBoardDirector>(); }

        /// <summary>Other components' controls on the board (the plague toggle, G2-2): presses inside them are not board presses.</summary>
        public static Func<Vector2, bool> ExtraOver;

        public bool Over(Vector2 screenPos)
        {
            if (ExtraOver != null) foreach (Func<Vector2, bool> f in ExtraOver.GetInvocationList()) if (f(screenPos)) return true;
            var g = new Vector2(screenPos.x, Screen.height - screenPos.y);
            foreach (var r in _rects) if (r.Contains(g)) return true;
            return false;
        }

        public Rect TradeArea => new Rect(Screen.width - (TradeAreaSize.x + 6f) * _u, Screen.height - (TradeAreaSize.y + 6f) * _u, TradeAreaSize.x * _u, TradeAreaSize.y * _u);

        // ---------------------------------------------------------------- drawing helpers (design pixels)

        private Rect R(float x, float y, float w, float h) => new Rect(x * _u, y * _u, w * _u, h * _u);
        private Rect RR(float fromRight, float y, float w, float h) => new Rect(Screen.width - (fromRight + w) * _u, y * _u, w * _u, h * _u);
        private Rect RB(float x, float fromBottom, float w, float h) => new Rect(x * _u, Screen.height - (fromBottom + h) * _u, w * _u, h * _u);

        private void Styles()
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow };
                _bold = new GUIStyle(_label) { fontStyle = FontStyle.Bold };
                _button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
        }

        private void Fill(Rect r, Color c) { GUI.DrawTexture(r, UiStyle.SolidTexture(c)); _rects.Add(r); }
        private void FillNoHit(Rect r, Color c) => GUI.DrawTexture(r, UiStyle.SolidTexture(c));

        private void Frame(Rect r, Color c, float th)
        {
            FillNoHit(new Rect(r.x, r.y, r.width, th), c); FillNoHit(new Rect(r.x, r.yMax - th, r.width, th), c);
            FillNoHit(new Rect(r.x, r.y, th, r.height), c); FillNoHit(new Rect(r.xMax - th, r.y, th, r.height), c);
        }

        private void Text(Rect r, string s, float size, Color c, bool bold = false, TextAnchor a = TextAnchor.MiddleLeft)
        {
            var st = bold ? _bold : _label;
            st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * _u));
            st.alignment = a;
            st.normal.textColor = new Color(0, 0, 0, 0.9f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
            Texts.Add(s);
        }

        private bool Button(Rect r, string s, bool enabled, float size = 12f)
        {
            _rects.Add(r);
            _button.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * _u));
            var old = GUI.enabled; GUI.enabled = enabled;
            bool hit = GUI.Button(r, s, _button);
            GUI.enabled = old;
            Texts.Add("[" + s + "]");
            return hit;
        }

        // ---------------------------------------------------------------- icons

        private static Texture2D Icon(int good)
        {
            const int N = 24;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++) px[y * N + x] = IconPixel(good, x + 0.5f, y + 0.5f);
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        private static Color IconPixel(int good, float x, float y)
        {
            var clear = new Color(0, 0, 0, 0);
            switch (good)
            {
                case 0: // a tree
                    if (x > 10 && x < 14 && y > 2 && y < 8) return new Color(0.42f, 0.27f, 0.14f);
                    if (y >= 7 && y < 22 && Mathf.Abs(x - 12f) < (22f - y) * 0.62f + 0.5f) return new Color(0.16f, 0.42f, 0.18f);
                    return clear;
                case 1: // a brick
                    if (x > 3 && x < 21 && y > 6 && y < 18)
                    {
                        bool mortar = Mathf.Abs(y - 12f) < 0.7f || (y < 12f ? Mathf.Abs(x - 12f) < 0.7f : Mathf.Abs(x - 7.5f) < 0.7f || Mathf.Abs(x - 16.5f) < 0.7f);
                        return mortar ? new Color(0.45f, 0.22f, 0.12f) : new Color(0.72f, 0.32f, 0.16f);
                    }
                    return clear;
                case 2: // an ear of wheat
                    if (Mathf.Abs(x - 12f) < 0.8f && y > 2 && y < 20) return new Color(0.62f, 0.50f, 0.15f);
                    for (int k = 0; k < 4; k++)
                    {
                        float cy = 9f + k * 3.4f;
                        if (Mathf.Pow((x - 8.3f) / 3.2f, 2) + Mathf.Pow((y - cy) / 1.7f, 2) < 1f) return new Color(0.88f, 0.70f, 0.14f);
                        if (Mathf.Pow((x - 15.7f) / 3.2f, 2) + Mathf.Pow((y - cy) / 1.7f, 2) < 1f) return new Color(0.88f, 0.70f, 0.14f);
                    }
                    if (Mathf.Pow((x - 12f) / 2f, 2) + Mathf.Pow((y - 21f) / 2.4f, 2) < 1f) return new Color(0.88f, 0.70f, 0.14f);
                    return clear;
                case 3: // a sheep
                    if ((x - 11f) * (x - 11f) + (y - 12f) * (y - 12f) < 50f) return new Color(0.96f, 0.95f, 0.92f);
                    if ((x - 18.5f) * (x - 18.5f) + (y - 13f) * (y - 13f) < 11f) return new Color(0.22f, 0.20f, 0.20f);
                    if (x > 6 && x < 8 && y > 2 && y < 7) return new Color(0.22f, 0.20f, 0.20f);
                    if (x > 13 && x < 15 && y > 2 && y < 7) return new Color(0.22f, 0.20f, 0.20f);
                    return clear;
                default: // a stone
                {
                    float dx = (x - 12f) / 9.5f, dy = (y - 11f) / 7.5f;
                    if (dx * dx + dy * dy < 1f) return y < 9f ? new Color(0.36f, 0.38f, 0.42f) : new Color(0.62f, 0.64f, 0.70f);
                    return clear;
                }
            }
        }

        private Texture2D IconOf(int g)
        {
            if (_icons == null) _icons = new Texture2D[Wares.RawCount];
            if (_icons[g] == null) _icons[g] = Icon(g);
            return _icons[g];
        }

        private void OnDestroy() { if (_icons != null) foreach (var t in _icons) if (t != null) Destroy(t); }

        // ---------------------------------------------------------------- the HUD

        private void OnGUI()
        {
            _rects.Clear(); Texts.Clear();
            if (_d == null || _d.View != BoardView.Board || _d.T == null) return;
            Styles();
            _u = Mathf.Max(0.6f, Screen.height / 390f);
            GUI.depth = -10;
            var s = _d.S; var t = _d.T;
            float W = Screen.width / _u;
            Ribbon(s, t);
            Status(s, t);
            Controls(t, W);
            Hand(s, t);
            Feed();
            if (TradePanel != null) TradePanel(TradeArea);
        }

        private void Ribbon(WorldState s, TableState t)
        {
            var order = new List<int>();
            if (t.Phase == TablePhase.Main || t.Phase == TablePhase.Discard) order.AddRange(t.TurnOrder);
            if (order.Count == 0) for (int p = 0; p < t.PeopleCount; p++) order.Add(p);
            float x = 6f;
            for (int i = 0; i < order.Count; i++)
            {
                int p = order[i];
                var r = R(x, 6f, 98f, 32f);
                Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.78f));
                People pe = null; foreach (var q in s.Peoples) if (q.Id == p) pe = q;
                var col = pe != null ? TempleView.PeopleColour(pe) : Color.white;
                FillNoHit(R(x, 6f, 7f, 32f), col);
                bool current = t.Current == p && !t.Paused && t.Phase != TablePhase.NotOpened;
                if (current) Frame(r, new Color(1f, 0.85f, 0.3f), Mathf.Max(2f, 2f * _u));
                Text(R(x + 10f, 7f, 86f, 15f), _d.PeopleShort(p), 11f, UiStyle.TextColor, true);
                int pts = p < _d.Points.Length ? _d.Points[p] : 0;
                int cards = TableRules.HandSize(t.Peoples[p].Hand);
                string sub = p == _d.Human ? pts + " pts" : pts + " pts, " + cards + " cards";
                Text(R(x + 10f, 22f, 86f, 15f), sub, 10f, new Color(0.9f, 0.86f, 0.75f));
                x += 100f;
            }
        }

        public string StatusLine(WorldState s, TableState t)
        {
            if (t.Phase == TablePhase.Ended) return "The god board is closed.";
            if (t.Paused) return "The board is waiting.";
            string who = t.Current == _d.Human ? "Your turn" : _d.PeopleShort(t.Current) + " play";
            if (t.Phase == TablePhase.Setup)
            {
                if (t.Current != _d.Human) return _d.PeopleShort(t.Current) + " choose their camp.";
                return t.SetupCorner < 0 ? "Place a settlement: tap a gold spot." : "Now a road beside it: tap a gold bar.";
            }
            if (t.Phase == TablePhase.Discard) return "Sickness: cards are discarded.";
            if (t.Current == _d.Human) return "Your turn: build, or end it.";
            return who + ".";
        }

        private void Status(WorldState s, TableState t)
        {
            string line = _d.HasMessage ? _d.Message : StatusLine(s, t);
            var r = R(6f, 42f, 320f, 20f);
            FillNoHit(r, new Color(0.08f, 0.07f, 0.06f, 0.55f));
            Text(new Rect(r.x + 4f * _u, r.y, r.width, r.height), line, 11f, _d.HasMessage ? new Color(1f, 0.7f, 0.55f) : UiStyle.TextColor, _d.HasMessage);
        }

        private void Controls(TableState t, float W)
        {
            bool main = t.Phase == TablePhase.Main && t.Current == _d.Human && !t.Paused;
            // The hourglass: a bar that shrinks as the sand falls.
            var bar = RR(6f, 6f, 132f, 12f);
            Fill(bar, new Color(0.08f, 0.07f, 0.06f, 0.78f));
            float frac = main ? Mathf.Clamp01(t.Sand / (float)t.Tuning.TurnCapTicks) : 0f;
            if (main)
            {
                var c = frac > 0.3f ? new Color(0.95f, 0.72f, 0.30f) : new Color(0.9f, 0.3f, 0.2f);
                FillNoHit(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * frac, bar.height - 4), c);
            }
            Text(RR(6f, 18f, 132f, 12f), main ? Mathf.CeilToInt(t.Sand / (float)WorldSim.TicksPerSecond) + " s of sand" : (t.Phase == TablePhase.Setup ? "No hourglass in setup" : "Hourglass waits"), 9f, new Color(0.9f, 0.86f, 0.75f), false, TextAnchor.MiddleRight);
            if (Button(RR(6f, 32f, 132f, 34f), "End turn", main, 13f)) _d.EndTurn();
            if (Button(RR(6f, 70f, 132f, 34f), "Return to the world", true, 12f)) _d.Leave();
            if (Button(RR(6f, 108f, 132f, 28f), GodBoardDirector.AdviserOn ? "Hint: on" : "Hint: off", true, 11f)) GodBoardDirector.AdviserOn = !GodBoardDirector.AdviserOn;
            if (Button(RR(6f, 140f, 64f, 28f), "Zoom -", true, 11f)) _d.Zoom(1f / 1.4f);
            if (Button(RR(74f, 140f, 64f, 28f), "Zoom +", true, 11f)) _d.Zoom(1.4f);
        }

        private void Hand(WorldState s, TableState t)
        {
            int me = _d.Human;
            var hand = t.Peoples[me].Hand;
            float x = 6f;
            for (int g = 0; g < Wares.RawCount; g++)
            {
                var r = RB(x, 6f, 52f, 44f);
                Fill(r, new Color(GoodColours[g].r * 0.35f, GoodColours[g].g * 0.35f, GoodColours[g].b * 0.35f, 0.88f));
                FillNoHit(new Rect(r.x, r.y, r.width, 3f * _u), GoodColours[g]);
                GUI.DrawTexture(new Rect(r.x + 4f * _u, r.y + 10f * _u, 22f * _u, 22f * _u), IconOf(g));
                Text(new Rect(r.x + 28f * _u, r.y + 8f * _u, 22f * _u, 26f * _u), hand[g].ToString(), 17f, UiStyle.TextColor, true, TextAnchor.MiddleCenter);
                x += 54f;
            }
            Text(RB(x + 4f, 30f, 150f, 14f), "Roads left " + TableRules.RoadsLeft(t, me), 10f, new Color(0.9f, 0.86f, 0.75f));
            Text(RB(x + 4f, 18f, 150f, 14f), "Camps left " + TableRules.SettlementsLeft(t, me) + ", cities " + TableRules.CitiesLeft(t, me), 10f, new Color(0.9f, 0.86f, 0.75f));
        }

        private void Feed()
        {
            float y = 66f;
            var feed = _d.Feed;
            int from = Mathf.Max(0, feed.Count - 4);
            for (int i = from; i < feed.Count; i++)
            {
                float age = Time.unscaledTime - feed[i].Time;
                if (age > 14f) continue;
                float a = Mathf.Clamp01((14f - age) / 4f);
                var r = R(6f, y, 250f, 15f);
                FillNoHit(r, new Color(0.08f, 0.07f, 0.06f, 0.5f * a));
                Text(new Rect(r.x + 3f * _u, r.y, r.width, r.height), feed[i].Text, 10f, new Color(0.96f, 0.93f, 0.86f, a));
                y += 16f;
            }
        }
    }
}
