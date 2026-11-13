using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    /// <summary>
    /// Every strategy and military action the UI can take (panels, God View tools, NPC options, the full_game driver):
    /// each is one command through <see cref="GodCommands.Submit"/>, so refusals come back as the sim's sentence in a
    /// toast. Plus shared text helpers (site names, card lines). Nothing here writes state.
    /// </summary>
    public static class StrategyUi
    {
        private static WorldState S => WorldBootstrap.Instance.Sim.State;

        // ---------------- commands ----------------

        public static CommandResult Pick(int cardId) =>
            GodCommands.Submit(new DraftPick { CardId = cardId }, CardName(cardId) + " goes into your hand. The rival picks at the same time.");

        public static CommandResult BuildCard(int cardId, int cityVertex) =>
            GodCommands.Submit(new BuildCard { CardId = cardId, CityVertex = cityVertex }, CardName(cardId) + " built at " + SiteName(S, cityVertex) + ".");

        public static CommandResult DiscardCard(int cardId) =>
            GodCommands.Submit(new DiscardCard { CardId = cardId }, CardName(cardId) + " discarded for " + S.Strategy.Tuning.DiscardCoin + " coin.");

        public static CommandResult FoundCity(int corner) =>
            GodCommands.Submit(new FoundCity { Vertex = corner }, "A new city is founded. Its hexes now send batches and its land is yours to build on.");

        public static CommandResult MoveHero(int corner) =>
            GodCommands.Submit(new MoveHero { Vertex = corner }, "The hero walks one hex edge, clearing demons on the way.");

        public static CommandResult Trade(Ware give, Ware get, int count = 1) =>
            GodCommands.Submit(new Trade { Give = give, Get = get, Count = count },
                "Traded " + count * StrategyApi.TradeRate(S) + " " + EconomyApi.WareName(give) + " for " + count + " " + EconomyApi.WareName(get) + ".");

        public static CommandResult Sell(Ware w, int count) =>
            GodCommands.Submit(new Sell { Ware = w, Count = count }, "Sold " + count + " " + EconomyApi.WareName(w) + ".");

        public static CommandResult Buy(Ware w, int count = 1) =>
            GodCommands.Submit(new Buy { Ware = w, Count = count }, "Bought " + count + " " + EconomyApi.WareName(w) + ".");

        public static CommandResult OrderWonderStage() =>
            GodCommands.Submit(new BuildWonderStage(), "Wonder stage ordered: carriers bring its goods to the wonder site.");

        public static CommandResult QueueUnit(int barracksId, int count = 1) =>
            GodCommands.Submit(new QueueUnit { BarracksId = barracksId, Count = count }, "Soldier queued: the barracks asks for its weapons and rations.");

        public static CommandResult OrderSquad(int squadId, OrderKind order, int target) =>
            GodCommands.Submit(new OrderSquad { SquadId = squadId, Order = order, Target = target },
                "Squad ordered to " + (order == OrderKind.Attack ? "attack " : "defend ") + SiteName(S, target) + ".");

        public static CommandResult CancelOrder(int squadId) =>
            GodCommands.Submit(new CancelOrder { SquadId = squadId }, "Order cancelled: the squad stops at the next flag.");

        // ---------------- text ----------------

        public static string CardName(int cardId)
        {
            var b = WorldBootstrap.Instance;
            if (b == null) return "The card";
            return StrategyApi.DescribeCard(b.Sim.State, cardId).Name;
        }

        public static string FamilyTag(CardView c) => "<color=" + c.Colour + "><b>" + c.FamilyName + "</b></color>";

        public static Color FamilyColor(CardView c) => ColorUtility.TryParseHtmlString(c.Colour, out var col) ? col : Color.gray;

        /// <summary>Readable name of a flag: a building, a city, the home village, the outpost, else a road flag by terrain.</summary>
        public static string SiteName(WorldState s, int v)
        {
            if (v < 0) return "nowhere";
            if (v == s.Board.HomeCityVertex) return "the home village";
            if (v == s.Board.RivalOutpostVertex) return "the rival outpost";
            int b = EconomyApi.BuildingAt(s, v);
            if (b >= 0) return "the " + EconomyApi.Def(s, s.Economy.Buildings[b].Type).Name.ToLowerInvariant();
            var topo = s.Board.Topology;
            var terr = new List<string>();
            foreach (int h in topo.VertexHexes(v))
            {
                string t = s.Board.HexTerrain[h].ToString().ToLowerInvariant();
                if (!terr.Contains(t)) terr.Add(t);
            }
            string by = terr.Count == 0 ? "" : " by the " + EconomyText.JoinAnd(terr);
            foreach (var c in s.Economy.Cities)
                if (c.Vertex == v) return (c.Owner == PlayerIds.Human ? "your city" : "the rival city") + by;
            return (topo.IsCorner(v) ? "the crossroads" : "the road flag") + by;
        }

        public static string SiteList(WorldState s, IList<int> vs, int max = 4)
        {
            var names = new List<string>();
            for (int i = 0; i < vs.Count && i < max; i++) { string n = SiteName(s, vs[i]); if (!names.Contains(n)) names.Add(n); }
            if (vs.Count > max) names.Add((vs.Count - max) + " more");
            return EconomyText.JoinAnd(names);
        }

        public static string ScoreTooltip(WorldState s)
        {
            var p = StrategyApi.Score(s, PlayerIds.Human);
            var r = StrategyApi.Score(s, PlayerIds.Rival);
            return "<b>You</b>: " + StrategyApi.ScoreText(s, PlayerIds.Human) + "\n<b>Rival</b>: " + StrategyApi.ScoreText(s, PlayerIds.Rival)
                + "\nFirst to " + s.Strategy.Tuning.VictoryPoints + " points wins; " + (p.Total >= r.Total ? "you lead." : "the rival leads.");
        }

        public static int FirstBarracks(WorldState s, bool activeOnly = false)
        {
            foreach (var b in s.Economy.Buildings)
                if (b.Owner == PlayerIds.Human && b.Type == BuildingType.Barracks && b.State != BuildingState.Removed
                    && (!activeOnly || b.State == BuildingState.Active)) return b.Id;
            return -1;
        }
    }

    /// <summary>
    /// Minimal vertical IMGUI layout with a scroll view sized from the previous frame's content height.
    /// <code>var f = Flow.Begin("cards", area); f.Title("Cards"); f.Text("..."); if (f.Button("Build")) ...; f.End();</code>
    /// </summary>
    public sealed class Flow
    {
        private static readonly Dictionary<string, float> Heights = new Dictionary<string, float>();
        private static readonly Dictionary<string, Vector2> Scrolls = new Dictionary<string, Vector2>();
        private static GUIStyle _text, _small, _btn, _btnOn, _title, _head;
        private static int _stylesFor = -1;

        public float X, Y, W;
        private string _key;
        private float _top;
        private Rect _view;

        public static GUIStyle TextStyle { get { Styles(); return _text; } }
        public static GUIStyle SmallStyle { get { Styles(); return _small; } }
        public static GUIStyle ButtonStyle { get { Styles(); return _btn; } }
        public static GUIStyle ButtonOnStyle { get { Styles(); return _btnOn; } }

        private static void Styles()
        {
            if (_stylesFor == Screen.height && _text != null) return;
            _stylesFor = Screen.height;
            _text = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(20), wordWrap = true, richText = true };
            _small = new GUIStyle(_text) { fontSize = UiStyle.Px(17) };
            _small.normal.textColor = new Color(0.85f, 0.81f, 0.74f);
            _btn = new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(19), richText = true, wordWrap = false, padding = new RectOffset(UiStyle.Px(8), UiStyle.Px(8), UiStyle.Px(3), UiStyle.Px(3)) };
            _btnOn = new GUIStyle(_btn);
            _btnOn.normal.background = _btnOn.hover.background = UiStyle.SolidTexture(new Color(0.62f, 0.44f, 0.16f, 0.98f));
            _title = new GUIStyle(UiStyle.Title) { fontSize = UiStyle.Px(30) };
            _head = new GUIStyle(_text) { fontStyle = FontStyle.Bold, fontSize = UiStyle.Px(22) };
            _head.normal.textColor = UiStyle.AccentColor;
        }

        public static Flow Begin(string key, Rect area, float pad = 14f)
        {
            Styles();
            var f = new Flow { _key = key };
            float p = UiStyle.Px(pad);
            f._view = new Rect(area.x + p, area.y + p, area.width - 2 * p, area.height - 2 * p);
            Heights.TryGetValue(key, out float h);
            Scrolls.TryGetValue(key, out var sc);
            bool scroll = h > f._view.height;
            float cw = f._view.width - (scroll ? UiStyle.Px(18) : 0);
            sc = GUI.BeginScrollView(f._view, sc, new Rect(0, 0, cw, Mathf.Max(h, f._view.height)), false, false);
            Scrolls[key] = sc;
            f.X = 0; f.Y = 0; f.W = cw; f._top = 0;
            return f;
        }

        public void End()
        {
            Heights[_key] = Y + UiStyle.Px(6);
            GUI.EndScrollView();
        }

        public static void ResetScroll(string key) => Scrolls.Remove(key);

        public void Title(string t)
        {
            GUI.Label(new Rect(X, Y, W, UiStyle.Px(40)), t, _title);
            Y += UiStyle.Px(44);
        }

        public void Head(string t)
        {
            Y += UiStyle.Px(6);
            float h = _head.CalcHeight(new GUIContent(t), W);
            GUI.Label(new Rect(X, Y, W, h), t, _head);
            Y += h + UiStyle.Px(2);
        }

        public float Text(string t, bool small = false, float indent = 0f)
        {
            if (string.IsNullOrEmpty(t)) return 0;
            var st = small ? _small : _text;
            float h = st.CalcHeight(new GUIContent(t), W - indent);
            GUI.Label(new Rect(X + indent, Y, W - indent, h), t, st);
            Y += h + UiStyle.Px(3);
            return h;
        }

        public void Space(float px = 8f) => Y += UiStyle.Px(px);

        public void Line(Color c)
        {
            GUI.DrawTexture(new Rect(X, Y + UiStyle.Px(3), W, Mathf.Max(1, UiStyle.Px(2))), UiStyle.SolidTexture(c));
            Y += UiStyle.Px(9);
        }

        /// <summary>A row of buttons; returns the index clicked or -1. Disabled entries (enabled[i] false) are greyed.</summary>
        public int Buttons(IList<string> labels, IList<bool> enabled = null, IList<string> tips = null, int on = -1, float height = 36f)
        {
            int n = labels.Count, hit = -1;
            if (n == 0) return -1;
            float gap = UiStyle.Px(6), bh = UiStyle.Px(height);
            float bw = (W - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(X + i * (bw + gap), Y, bw, bh);
                bool was = GUI.enabled;
                GUI.enabled = was && (enabled == null || enabled[i]);
                if (GUI.Button(r, labels[i], i == on ? _btnOn : _btn)) hit = i;
                GUI.enabled = was;
                if (tips != null && !string.IsNullOrEmpty(tips[i]) && r.Contains(Event.current.mousePosition))
                    GodToolbar.Tooltip(tips[i], GUIUtility.GUIToScreenPoint(Event.current.mousePosition));
            }
            Y += bh + UiStyle.Px(6);
            return hit;
        }

        public bool Button(string label, bool enabled = true, string tip = null, float height = 38f) =>
            Buttons(new[] { label }, new[] { enabled }, new[] { tip }, -1, height) == 0;

        /// <summary>A coloured card-like box: draws the background and returns the inner flow position.</summary>
        public Rect Box(float height, Color stripe)
        {
            var r = new Rect(X, Y, W, height);
            GUI.DrawTexture(r, UiStyle.SolidTexture(new Color(1f, 1f, 1f, 0.06f)));
            GUI.DrawTexture(new Rect(r.x, r.y, UiStyle.Px(6), r.height), UiStyle.SolidTexture(stripe));
            return r;
        }
    }
}
