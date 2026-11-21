using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The god board's on-screen controls (G1-6b layout, from <c>Docs/GODBOARD_UI.md</c>): people panels across the top with a
    /// points badge and the hourglass ring on the active one, the hand as a fan of goods cards at the bottom centre, the
    /// costs card and spare-piece stacks at the bottom left, one big round hourglass button at the bottom right, small round
    /// icon buttons along the top right, a collapsible log at the bottom left, toasts and big moment cards. The trade and
    /// discard modals, offer cards and the end panel are in the other parts. IMGUI in design pixels, scaled by
    /// Screen.height / 390; touch and mouse both work. No numbers on hexes, no odds.
    /// </summary>
    public sealed partial class GodBoardHud : MonoBehaviour
    {
        /// <summary>The free region (design pixels from the right and bottom edges) reserved for other components' controls (kept for the older hook).</summary>
        public static readonly Vector2 TradeAreaSize = new Vector2(230f, 150f);
        /// <summary>Older hook: draws into the given rect (screen pixels, GUI space). Null unless something sets it.</summary>
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
        /// <summary>Texts drawn last frame (tests and captures). Buttons appear as [name].</summary>
        public readonly List<string> Texts = new List<string>();

        /// <summary>The log tab (bottom left) is open.</summary>
        public bool LogOpen { get; private set; }
        private int _logSeen;

        private void Awake() { _d = GetComponent<GodBoardDirector>(); if (_d != null) GodBoardNightLight.Attach(_d); }

        /// <summary>Other components' controls on the board (the plague toggle, G2-2): presses inside them are not board presses.</summary>
        public static Func<Vector2, bool> ExtraOver;
        /// <summary>The plague overlay's toggle (G2-2), drawn here as a round icon at slot 2 when the plague module sets these.</summary>
        public static Func<bool> PlagueIsOn;
        public static Action PlagueToggle;

        public bool Over(Vector2 screenPos)
        {
            if (ExtraOver != null) foreach (Func<Vector2, bool> f in ExtraOver.GetInvocationList()) if (f(screenPos)) return true;
            var g = new Vector2(screenPos.x, Screen.height - screenPos.y);
            if (TutorialDirector.Contains(g)) return true; // the tutor's bubble
            foreach (var r in _rects) if (r.Contains(g)) return true;
            return false;
        }

        public Rect TradeArea => new Rect(Screen.width - (TradeAreaSize.x + 6f) * _u, Screen.height - (TradeAreaSize.y + 6f) * _u, TradeAreaSize.x * _u, TradeAreaSize.y * _u);

        /// <summary>Slot k of the small round icon buttons along the top-right edge (0 is the rightmost): the same place for the plague toggle.</summary>
        public static Rect TopIcon(int slot, float u) => new Rect(Screen.width - (6f + 30f + slot * 35f) * u, 6f * u, 30f * u, 30f * u);

        public void SetLogOpen(bool open) { LogOpen = open; if (open) _logSeen = _d.FeedTotal; }

        // ---------------------------------------------------------------- drawing helpers (design pixels)

        private Rect R(float x, float y, float w, float h) => new Rect(x * _u, y * _u, w * _u, h * _u);
        private Rect RR(float fromRight, float y, float w, float h) => new Rect(Screen.width - (fromRight + w) * _u, y * _u, w * _u, h * _u);
        private Rect RB(float x, float fromBottom, float w, float h) => new Rect(x * _u, Screen.height - (fromBottom + h) * _u, w * _u, h * _u);
        private Rect RRB(float fromRight, float fromBottom, float w, float h) => new Rect(Screen.width - (fromRight + w) * _u, Screen.height - (fromBottom + h) * _u, w * _u, h * _u);

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

        private void Text(Rect r, string s, float size, Color c, bool bold = false, TextAnchor a = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var st = bold ? _bold : _label;
            st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * _u));
            st.alignment = a;
            st.wordWrap = wrap;
            st.normal.textColor = new Color(0, 0, 0, 0.9f * c.a);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
            st.wordWrap = false;
            Texts.Add(s);
        }

        private bool Button(Rect r, string s, bool enabled, float size = 12f, bool force = false)
        {
            _rects.Add(r);
            _button.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * _u));
            var old = GUI.enabled; GUI.enabled = enabled && (!_under || force);
            bool hit = GUI.Button(r, s, _button);
            GUI.enabled = old;
            Texts.Add("[" + s + "]");
            return hit;
        }

        /// <summary>An invisible press area over a rect (it still blocks board presses).</summary>
        private bool Tap(Rect r, bool enabled = true, bool force = false)
        {
            _rects.Add(r);
            return GUI.Button(r, GUIContent.none, GUIStyle.none) && enabled && (!_under || force);
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

        private void OnDestroy() { if (_icons != null) foreach (var t in _icons) if (t != null) Destroy(t); ArtDestroy(); }

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
            bool clear = GodBoardSettings.ClearView;
            _under = clear ? DiscardVisible || GameOverShown : GameOverShown;
            if (!DiscardVisible && _pickActive) { System.Array.Clear(DiscardPick, 0, DiscardPick.Length); _pickActive = false; }
            if (LogOpen) _logSeen = _d.FeedTotal;
            if (clear)
            {
                People(s, t);
                Status(s, t, W);
                IconRow(t);
                Costs(s, t);
                Stacks(s, t);
                Hand(s, t, W);
                HourglassButton(t);
                Toasts(W);
                LogTab();
                OfferCards(s, t);
                if (TradeOpen && !GameOverShown) TradeModal(s, t, W);
                if (DiscardVisible) DiscardModal(s, t, W);
            }
            else
            {
                if (_d.HasMessage) Status(s, t, W);
                LedgeBlocks();
                if (TradeOpen && TradeTab == 1 && !GameOverShown) TradeModal(s, t, W);
            }
            MomentCardDraw(W);
            if (GameOverShown) GameOverModal(s, t, W);
            if (TradePanel != null) TradePanel(TradeArea);
        }

        // ---------------------------------------------------------------- people panels

        private const float PanelW = 156f, PanelH = 42f;

        private int SoldiersOf(WorldState s, int p) => p >= 0 && p < Model.Soldiers.Length ? Model.Soldiers[p] : 0;

        private void People(WorldState s, TableState t)
        {
            var order = new List<int>();
            if (t.Phase == TablePhase.Main || t.Phase == TablePhase.Discard) order.AddRange(t.TurnOrder);
            if (order.Count == 0) for (int p = 0; p < t.PeopleCount; p++) order.Add(p);
            for (int i = 0; i < order.Count; i++)
            {
                int p = order[i];
                bool current = t.Current == p && !t.Paused && t.Phase != TablePhase.NotOpened && t.Phase != TablePhase.Ended;
                float x = 6f + i * (PanelW + 3f), y = current ? 2f : 8f, h = current ? PanelH + 5f : PanelH;
                var r = R(x, y, PanelW, h);
                var col = PeopleCol(p);
                Fill(r, new Color(0.08f, 0.07f, 0.06f, current ? 0.92f : 0.78f));
                FillNoHit(new Rect(r.x, r.y, r.width, 4f * _u), col);
                FillNoHit(new Rect(r.x, r.y, 4f * _u, r.height), col);
                if (current) Frame(r, Gold, Mathf.Max(2f, 2f * _u));
                // emblem: a banner disc with the first letter
                var em = Sub(r, _u, 8f, 10f, 22f, 22f);
                Disc(new Rect(em.x - _u, em.y - _u, em.width + 2f * _u, em.height + 2f * _u), new Color(0.96f, 0.92f, 0.82f));
                Disc(em, col);
                string nm = _d.PeopleShort(p);
                Text(em, nm.Length > 0 ? nm.Substring(0, 1) : "?", 12f, Color.white, true, TextAnchor.MiddleCenter);
                Text(Sub(r, _u, 34f, 4f, 56f, 15f), nm, 11f, UiStyle.TextColor, true);
                // small counters: cards held, longest road, soldiers
                int cards = TableRules.HandSize(t.Peoples[p].Hand);
                float cx = 34f, cy = 22f + (current ? 3f : 0f);
                Counter(r, cx, cy, GCard, cards.ToString());
                Counter(r, cx + 29f, cy, GRoad, t.Peoples[p].RoadLength.ToString());
                Counter(r, cx + 58f, cy, GHelmet, SoldiersOf(s, p).ToString());
                // the points badge with the hourglass ring round it
                var bc = new Vector2(r.xMax - 22f * _u, r.y + (current ? 25f : 22f) * _u);
                float br = 16f * _u;
                if (current)
                {
                    float frac = t.Current == _d.Human && t.Phase == TablePhase.Main ? Mathf.Clamp01(t.Sand / (float)t.Tuning.TurnCapTicks) : 1f;
                    Arc(bc, br + 3.5f * _u, 3.5f * _u, 1f, new Color(0.25f, 0.22f, 0.16f));
                    Arc(bc, br + 3.5f * _u, 3.5f * _u, frac, frac > 0.3f ? Gold : new Color(0.95f, 0.3f, 0.2f));
                }
                Disc(new Rect(bc.x - br - _u, bc.y - br - _u, 2f * br + 2f * _u, 2f * br + 2f * _u), new Color(0.96f, 0.92f, 0.82f));
                Disc(new Rect(bc.x - br, bc.y - br, 2f * br, 2f * br), Color.Lerp(col, Color.black, 0.25f));
                Text(new Rect(bc.x - br, bc.y - br, 2f * br, 2f * br), PointsOf(p).ToString(), 18f, Color.white, true, TextAnchor.MiddleCenter);
                if (t.LongestRoadHolder == p)
                {
                    var m = new Rect(r.x + 18f * _u, r.yMax - 13f * _u, 18f * _u, 18f * _u);
                    Disc(m, Gold);
                    GlyphAt(new Rect(m.x + 2f * _u, m.y + 2f * _u, 14f * _u, 14f * _u), GRoad, new Color(0.2f, 0.12f, 0.04f));
                }
            }
        }

        private void Counter(Rect panel, float dx, float dy, int glyph, string n)
        {
            GlyphAt(Sub(panel, _u, dx, dy, 13f, 13f), glyph, new Color(0.92f, 0.86f, 0.72f));
            Text(Sub(panel, _u, dx + 13f, dy - 1f, 15f, 15f), n, 10f, UiStyle.TextColor, true);
        }

        public string StatusLine(WorldState s, TableState t)
        {
            if (t.Phase == TablePhase.Ended) return "The god board is closed.";
            if (t.Paused) return "The board is waiting.";
            if (t.Current < 0 || t.Current >= t.PeopleCount) return t.Phase == TablePhase.NotOpened ? "Sacrifice a lamb at your altar to open the board." : "The peoples are gathering.";
            string who = t.Current == _d.Human ? "Your turn" : _d.PeopleShort(t.Current) + " play";
            if (t.Phase == TablePhase.Setup)
            {
                if (t.Current != _d.Human) return _d.PeopleShort(t.Current) + " choose their camp.";
                return t.SetupCorner < 0 ? "Place a settlement: tap a gold spot." : "Now a road beside it: tap a gold bar.";
            }
            if (t.Phase == TablePhase.Discard) return DiscardVisible ? "Sickness: choose the cards to give up." : "Sickness: cards are discarded.";
            if (t.Current == _d.Human)
            {
                if (_d.BuildMode == BuildKind.Road) return "Tap a gold bar to lay a road.";
                if (_d.BuildMode == BuildKind.Settlement) return "Tap a gold spot to found a camp.";
                if (_d.BuildMode == BuildKind.City) return "Tap one of your camps to make it a city.";
                return "Your turn: build, or end it.";
            }
            return who + ".";
        }

        private void Status(WorldState s, TableState t, float W)
        {
            string line = _d.HasMessage ? _d.Message : StatusLine(s, t);
            float w = 300f;
            var r = R((W - w) * 0.5f + 40f, 55f, w, 18f);
            FillNoHit(r, new Color(0.08f, 0.07f, 0.06f, 0.55f));
            Text(r, line, 11f, _d.HasMessage ? new Color(1f, 0.7f, 0.55f) : UiStyle.TextColor, _d.HasMessage, TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- icon row (top right)

        private void IconRow(TableState t)
        {
            var dark = new Color(0.16f, 0.13f, 0.10f, 0.95f);
            if (RoundButton(TopIcon(0, _u), GHouse, "Return to the world", dark, true, false, true)) _d.Leave();
            if (RoundButton(TopIcon(1, _u), GBulb, GodBoardDirector.AdviserOn ? "Hint: on" : "Hint: off", dark, true, GodBoardDirector.AdviserOn)) GodBoardDirector.AdviserOn = !GodBoardDirector.AdviserOn;
            if (PlagueToggle != null)
            {
                bool on = PlagueIsOn != null && PlagueIsOn();
                if (RoundButton(TopIcon(2, _u), GDroplet, on ? "Plague: on" : "Plague", dark, true, on)) PlagueToggle();
            }
            if (RoundButton(TopIcon(3, _u), GPlus, "Zoom in", dark, true)) _d.Zoom(1.4f);
            if (RoundButton(TopIcon(4, _u), GMinus, "Zoom out", dark, true)) _d.Zoom(1f / 1.4f);
        }

        // ---------------------------------------------------------------- hand, costs, stacks, hourglass

        private void Hand(WorldState s, TableState t, float W)
        {
            int me = _d.Human;
            var hand = t.Peoples[me].Hand;
            const float CW = 46f, CH = 64f, Rad = 200f;
            float cx = W * 0.5f - 4f, baseY = 346f;
            var pivot = new Vector2(cx * _u, (baseY + Rad) * _u);
            var centres = new Vector2[Wares.RawCount];
            var angles = new float[Wares.RawCount];
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float a = (g - 2) * 11f;
                angles[g] = a;
                float rad = a * Mathf.Deg2Rad;
                bool lift = (BankGive == g && TradeOpen && TradeTab == 0) || OfferGive[g] > 0 || DiscardPick[g] > 0;
                centres[g] = new Vector2(pivot.x + Rad * _u * Mathf.Sin(rad), pivot.y - Rad * _u * Mathf.Cos(rad) - (lift ? 12f * _u : 0f));
            }
            var old = GUI.matrix;
            for (int g = 0; g < Wares.RawCount; g++)
            {
                bool lift = (BankGive == g && TradeOpen && TradeTab == 0) || OfferGive[g] > 0 || DiscardPick[g] > 0;
                GUI.matrix = old;
                GUIUtility.RotateAroundPivot(angles[g], centres[g]);
                GoodsCard(new Rect(centres[g].x - CW * 0.5f * _u, centres[g].y - CH * 0.5f * _u, CW * _u, CH * _u), g, hand[g], hand[g] == 0, lift, false);
            }
            GUI.matrix = old;
            for (int g = 0; g < Wares.RawCount; g++)
            {
                float rad = angles[g] * Mathf.Deg2Rad;
                var off = new Vector2((CW * 0.5f - 3f) * _u, (-CH * 0.5f + 5f) * _u);
                var rot = new Vector2(off.x * Mathf.Cos(rad) - off.y * Mathf.Sin(rad), off.x * Mathf.Sin(rad) + off.y * Mathf.Cos(rad));
                var bp = centres[g] + rot;
                float b = 16f * _u;
                var br = new Rect(bp.x - b * 0.5f, bp.y - b * 0.5f, b, b);
                Disc(new Rect(br.x - _u, br.y - _u, b + 2f * _u, b + 2f * _u), new Color(0.96f, 0.92f, 0.82f));
                Disc(br, hand[g] > 0 ? new Color(0.14f, 0.12f, 0.10f) : new Color(0.35f, 0.3f, 0.25f));
                Text(br, hand[g].ToString(), 10f, UiStyle.TextColor, true, TextAnchor.MiddleCenter);
            }
            _rects.Add(new Rect((cx - 125f) * _u, (baseY - 36f) * _u, 250f * _u, 80f * _u));
        }

        private static readonly BuildKind[] Kinds = { BuildKind.Road, BuildKind.Settlement, BuildKind.City };
        private static readonly string[] KindNames = { "Road", "Camp", "City" };
        private static readonly int[] KindGlyph = { GRoad, GHouse, GCity };

        private bool Affordable(TableState t, int kind) => Model.Kind[kind].Affordable;

        private static int[] CostOf(int kind) => kind == 0 ? TableRules.RoadCost : kind == 1 ? TableRules.SettlementCost : TableRules.CityCost;

        private void Costs(WorldState s, TableState t)
        {
            var r = RB(6f, 6f, 154f, 100f);
            Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.85f));
            Frame(r, new Color(0.96f, 0.92f, 0.82f, 0.6f), Mathf.Max(1f, _u));
            var mine = PeopleCol(_d.Human);
            for (int k = 0; k < 3; k++)
            {
                var row = Sub(r, _u, 4f, 4f + k * 31f, 146f, 29f);
                bool ok = Affordable(t, k);
                bool active = _d.BuildMode == Kinds[k];
                FillNoHit(row, ok ? new Color(0.36f, 0.30f, 0.12f, 0.95f) : new Color(0.16f, 0.14f, 0.12f, 0.9f));
                if (active) Frame(row, Gold, Mathf.Max(2f, 2f * _u));
                else if (ok) Frame(row, new Color(0.9f, 0.75f, 0.3f, 0.8f), Mathf.Max(1f, _u));
                var pc = ok ? mine : mine * 0.5f; pc.a = 1f;
                GlyphAt(Sub(row, _u, 3f, 3f, 23f, 23f), KindGlyph[k], pc);
                float x = 30f;
                var cost = CostOf(k);
                for (int g = 0; g < Wares.RawCount; g++)
                    for (int n = 0; n < cost[g]; n++)
                    {
                        var old = GUI.color; GUI.color = ok ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                        GUI.DrawTexture(Sub(row, _u, x, 6f, 17f, 17f), IconOf(g));
                        GUI.color = old;
                        x += 22f;
                    }
                if (Tap(row, true, false))
                {
                    if (ok) _d.SetBuildMode(Kinds[k]);
                    else _d.Say(t.Phase == TablePhase.Main && t.Current == _d.Human ? "Not enough goods for a " + KindNames[k].ToLowerInvariant() + " yet." : "Wait for your turn.");
                }
                Texts.Add("[" + KindNames[k] + "]");
            }
        }

        private void Stacks(WorldState s, TableState t)
        {
            int me = _d.Human;
            int[] left = { TableRules.RoadsLeft(t, me), TableRules.SettlementsLeft(t, me), TableRules.CitiesLeft(t, me) };
            int[] max = { t.Tuning.MaxRoads, t.Tuning.MaxSettlements, t.Tuning.MaxCities };
            var col = PeopleCol(me);
            for (int k = 0; k < 3; k++)
            {
                float x = 166f + k * 22f;
                float chip = 62f / max[k];
                for (int i = 0; i < Mathf.Min(left[k], max[k]); i++)
                {
                    var c = RB(x, 28f + i * chip, 18f, Mathf.Max(2f, chip - 1f));
                    FillNoHit(c, Color.Lerp(col, Color.black, 0.55f));
                    FillNoHit(new Rect(c.x + _u, c.y + _u * 0.5f, c.width - 2f * _u, Mathf.Max(1f, c.height - 1.5f * _u)), col);
                }
                GlyphAt(RB(x + 1f, 8f, 16f, 16f), KindGlyph[k], new Color(0.92f, 0.86f, 0.72f));
            }
        }

        private void HourglassButton(TableState t)
        {
            bool main = t.Phase == TablePhase.Main && t.Current == _d.Human && !t.Paused;
            var r = RRB(8f, 8f, 70f, 70f);
            float frac = main ? Mathf.Clamp01(t.Sand / (float)t.Tuning.TurnCapTicks) : 0f;
            var bg = main ? new Color(0.42f, 0.28f, 0.10f, 1f) : new Color(0.16f, 0.13f, 0.10f, 0.95f);
            bool hit = RoundButton(r, GHourglass, "End turn", bg, main, false);
            var c = r.center;
            Arc(c, r.width * 0.5f - 3f * _u, 3.5f * _u, 1f, new Color(0.25f, 0.22f, 0.16f));
            if (main) Arc(c, r.width * 0.5f - 3f * _u, 3.5f * _u, frac, frac > 0.3f ? Gold : new Color(0.95f, 0.3f, 0.2f));
            // the sand in the two chambers
            float pad = r.width * 0.2f, k = (r.width - 2f * pad) / 32f;
            float gx = r.x + pad, gy = r.yMax - pad; // grid origin at the bottom-left of the glyph
            var sand = main ? new Color(0.98f, 0.82f, 0.35f) : new Color(0.5f, 0.45f, 0.35f);
            float top = main ? frac : 0f;
            FillNoHit(new Rect(gx + 11f * k, gy - (17f + 8f * top) * k, 10f * k, 8f * top * k), sand);
            FillNoHit(new Rect(gx + 10f * k, gy - (6f + 8f * (1f - top)) * k, 12f * k, 8f * (1f - top) * k), sand);
            if (hit) _d.EndTurn();
            // the trade button beside it
            var tr = RRB(8f + 70f + 8f, 20f, 38f, 38f);
            if (RoundButton(tr, GSwap, "Trade", new Color(0.16f, 0.13f, 0.10f, 0.95f), true, TradeOpen, true)) { if (TradeOpen) CloseTrade(); else OpenTrade(); }
        }

        // ---------------------------------------------------------------- toasts, log

        private void Toasts(float W)
        {
            var feed = _d.Feed;
            float y = 78f;
            int shown = 0;
            for (int i = feed.Count - 1; i >= 0 && shown < 2; i--)
            {
                float age = Time.unscaledTime - feed[i].Time;
                if (age > 4.5f) break;
                float a = Mathf.Clamp01((4.5f - age) / 1.2f);
                var r = R((W - 270f) * 0.5f + 40f, y, 270f, 16f);
                FillNoHit(r, new Color(0.08f, 0.07f, 0.06f, 0.6f * a));
                Text(r, feed[i].Text, 10f, new Color(0.96f, 0.93f, 0.86f, a), false, TextAnchor.MiddleCenter);
                y += 18f; shown++;
            }
        }

        private void LogTab()
        {
            var feed = _d.Feed;
            int unread = Mathf.Max(0, _d.FeedTotal - _logSeen);
            var tab = RB(6f, 110f, 66f, 20f);
            Fill(tab, new Color(0.12f, 0.10f, 0.08f, 0.92f));
            Frame(tab, new Color(0.96f, 0.92f, 0.82f, 0.5f), Mathf.Max(1f, _u));
            GlyphAt(Sub(tab, _u, 4f, 3f, 14f, 14f), GScroll, new Color(0.92f, 0.86f, 0.72f));
            Text(Sub(tab, _u, 22f, 0f, 40f, 20f), "Log", 11f, UiStyle.TextColor, true);
            if (GUI.Button(tab, GUIContent.none, GUIStyle.none) && !_under) SetLogOpen(!LogOpen);
            Texts.Add("[Log]");
            if (!LogOpen && unread > 0)
            {
                var b = new Rect(tab.xMax - 9f * _u, tab.y - 6f * _u, 16f * _u, 16f * _u);
                Disc(b, new Color(0.85f, 0.25f, 0.18f));
                Text(b, Mathf.Min(9, unread).ToString() + (unread > 9 ? "+" : ""), 9f, Color.white, true, TextAnchor.MiddleCenter);
            }
            if (!LogOpen) return;
            var p = RB(6f, 132f, 264f, 128f);
            Fill(p, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            Frame(p, new Color(0.96f, 0.92f, 0.82f, 0.5f), Mathf.Max(1f, _u));
            int n = Mathf.Min(8, feed.Count);
            for (int i = 0; i < n; i++)
            {
                var line = feed[feed.Count - n + i];
                Text(Sub(p, _u, 6f, 4f + i * 15.5f, 254f, 15f), line.Text, 10f, new Color(0.96f, 0.93f, 0.86f));
            }
            if (n == 0) Text(Sub(p, _u, 6f, 4f, 250f, 15f), "Nothing yet.", 10f, Soft);
        }

        // ---------------------------------------------------------------- moment cards

        private void MomentCardDraw(float W)
        {
            var m = _d.Moment;
            if (string.IsNullOrEmpty(m.Kind)) return;
            float age = Time.unscaledTime - m.Time;
            if (age < 0f || age > m.Duration) return;
            float a = age < 0.35f ? age / 0.35f : Mathf.Clamp01((m.Duration - age) / 0.7f);
            Color tint; int glyph;
            switch (m.Kind)
            {
                case "epidemic": tint = new Color(0.55f, 0.10f, 0.10f); glyph = GDroplet; break;
                case "crystal": tint = new Color(0.12f, 0.45f, 0.50f); glyph = GCity; break;
                case "burned": tint = new Color(0.60f, 0.28f, 0.08f); glyph = GHouse; break;
                default: tint = new Color(0.45f, 0.34f, 0.10f); glyph = GBulb; break;
            }
            if (m.Kind == "epidemic") FillNoHit(new Rect(0, 0, Screen.width, Screen.height), new Color(0.12f, 0.02f, 0.02f, 0.45f * a));
            bool small = m.Kind == "prince";
            float w = small ? 250f : 290f, h = small ? 54f : 112f;
            float lift = (1f - a) * 12f;
            var r = R((W - w) * 0.5f + (small ? 40f : 0f), (small ? 80f : 120f) + lift, w, h);
            Color Al(Color c) { c.a *= a; return c; }
            FillNoHit(new Rect(r.x - 3f * _u, r.y - 3f * _u, r.width + 6f * _u, r.height + 6f * _u), Al(new Color(0.96f, 0.92f, 0.82f)));
            FillNoHit(r, Al(new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 1f)));
            if (small)
            {
                var sd = Sub(r, _u, 6f, 6f, 42f, 42f);
                Disc(sd, Al(tint));
                GlyphAt(new Rect(sd.x + 8f * _u, sd.y + 8f * _u, sd.width - 16f * _u, sd.height - 16f * _u), glyph, new Color(1f, 0.96f, 0.88f, a));
                Text(Sub(r, _u, 56f, 4f, w - 62f, 16f), m.Title, 12f, new Color(1f, 0.9f, 0.6f, a), true);
                Text(Sub(r, _u, 56f, 20f, w - 62f, 32f), m.Text, 11f, new Color(0.96f, 0.93f, 0.86f, a), false, TextAnchor.UpperLeft, true);
                return;
            }
            FillNoHit(new Rect(r.x, r.y, r.width, 30f * _u), Al(tint));
            Text(Sub(r, _u, 10f, 0f, w - 20f, 30f), m.Title, 18f, new Color(1f, 0.96f, 0.88f, a), true, TextAnchor.MiddleCenter);
            var disc = Sub(r, _u, 14f, 40f, 58f, 58f);
            Disc(disc, Al(tint));
            GlyphAt(new Rect(disc.x + 10f * _u, disc.y + 10f * _u, disc.width - 20f * _u, disc.height - 20f * _u), glyph, new Color(1f, 0.96f, 0.88f, a));
            Text(Sub(r, _u, 82f, 38f, w - 92f, 66f), m.Text, 12f, new Color(0.96f, 0.93f, 0.86f, a), false, TextAnchor.MiddleLeft, true);
        }

        private int PointsOf(int p) => p >= 0 && p < Model.Points.Length ? Model.Points[p] : 0;

        /// <summary>The view model both the ledge and this Clear view read (rebuilt once per frame).</summary>
        public GodBoardViewModel Model => _d.Model;
    }
}
