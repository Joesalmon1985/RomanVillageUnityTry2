using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Road tool, one 20 m segment per click. Build mode: the segments next to your network (road ends, buildings,
    /// sites, cities) that the sim would accept glow green; Remove mode (X, or the panel button): your roads glow
    /// orange. Hovering a segment shows the sim's sentence and what the next segment costs (the road cost rule:
    /// one payment covers several segments). Candidate lists are rebuilt only when roads, buildings, cities or the
    /// storehouse's planks and bricks change, never every frame, and only around the network.
    /// </summary>
    public sealed class RoadTool : GodTool
    {
        public const string ToolId = "road";
        public bool RemoveMode { get; private set; }
        public int HoverEdge { get; private set; } = -1;
        public CommandResult HoverResult { get; private set; }
        public IReadOnlyList<int> Candidates => _candidates;
        public int Recomputes { get; private set; }

        private readonly List<int> _candidates = new List<int>();
        private readonly HashSet<int> _valid = new HashSet<int>();
        private readonly List<GodOverlay.Item> _items = new List<GodOverlay.Item>();
        private long _signature = long.MinValue;
        private int _shownHover = -2;
        private bool _shownMode;

        public RoadTool()
        {
            Id = ToolId; Label = "Road"; Hotkey = KeyCode.R; Order = 20; CapturesClicks = true; PanelWidth = 470f;
            TipText = () => "Build or remove road segments (20 m each). X switches between building and removing.";
        }

        public override bool HasPanel => true;
        public override float PanelHeight(float width) => UiStyle.Px(360);
        public override void Activate() { _signature = long.MinValue; _shownHover = -2; base.Activate(); }
        public override void Deactivate() { GodOverlay.Hide(); HoverEdge = -1; base.Deactivate(); }

        public void SetRemoveMode(bool remove) { if (remove != RemoveMode) { RemoveMode = remove; _signature = long.MinValue; } }

        public CommandResult Check(int edge) => RemoveMode ? GodCommands.Check(new RemoveRoad { Edge = edge }) : GodCommands.Check(new PlaceRoad { Edge = edge });
        public bool IsValid(int e) { Refresh(); return _valid.Contains(e); }

        private long Signature(WorldState s)
        {
            var e = s.Economy;
            var v = EconomyViews.Instance;
            long sig = (RemoveMode ? 1 : 2) + e.RoadCredit * 17L + EconomyApi.StorehouseCount(s, Ware.Planks) * 131L + EconomyApi.StorehouseCount(s, Ware.Bricks) * 1031L + e.Cities.Count * 7L;
            if (v != null) foreach (var r in v.Roads) if (r != null) sig = sig * 31 + r.Edge + 1;
            foreach (var b in e.Buildings) sig = sig * 31 + (b.State == BuildingState.Removed ? 1 : 2) + b.Vertex * 3;
            return sig;
        }

        /// <summary>Rebuilds the candidate segments around the network if anything relevant changed.</summary>
        public void Refresh()
        {
            var boot = WorldBootstrap.Instance; if (boot == null) return;
            var s = boot.Sim.State;
            long sig = Signature(s);
            if (sig == _signature) return;
            _signature = sig;
            Recomputes++;
            _candidates.Clear(); _valid.Clear();
            var topo = s.Board.Topology;
            var views = EconomyViews.Instance;
            if (RemoveMode)
            {
                if (views != null) foreach (var r in views.Roads) if (r != null) { _candidates.Add(r.Edge); _valid.Add(r.Edge); }
            }
            else
            {
                var flags = new HashSet<int>();
                foreach (var c in s.Economy.Cities) if (c.Owner == PlayerIds.Human) flags.Add(c.Vertex);
                foreach (var b in s.Economy.Buildings) if (b.State != BuildingState.Removed && b.Owner == PlayerIds.Human) flags.Add(b.Vertex);
                if (views != null) foreach (var r in views.Roads) if (r != null) { flags.Add(topo.EdgeVertexA(r.Edge)); flags.Add(topo.EdgeVertexB(r.Edge)); }
                var seen = new HashSet<int>();
                foreach (int v in flags)
                    foreach (int e in topo.VertexEdges(v))
                    {
                        if (!seen.Add(e) || s.Economy.Roads[e]) continue;
                        if (Check(e).Ok) { _candidates.Add(e); _valid.Add(e); }
                    }
                _candidates.Sort();
            }
            _shownHover = -2;
        }

        /// <summary>The same path as a click on a segment (build or remove, by mode).</summary>
        public CommandResult TryAt(int edge)
        {
            var r = RemoveMode ? GodCommands.RemoveRoad(edge) : GodCommands.PlaceRoad(edge);
            _signature = long.MinValue;
            return r;
        }

        public CommandResult Place(int edge) { SetRemoveMode(false); return TryAt(edge); }
        public CommandResult Remove(int edge) { SetRemoveMode(true); return TryAt(edge); }

        public override void Update()
        {
            if (Input.GetKeyDown(KeyCode.X)) SetRemoveMode(!RemoveMode);
            Refresh();
            HoverEdge = GodToolbar.MouseOverUi() ? -1 : BoardPicker.EdgeUnderMouse();
            if (HoverEdge >= 0) HoverResult = Check(HoverEdge);
            if (HoverEdge != _shownHover || _shownMode != RemoveMode)
            {
                _shownHover = HoverEdge; _shownMode = RemoveMode;
                _items.Clear();
                var col = RemoveMode ? GodOverlay.Existing : GodOverlay.Valid;
                foreach (int e in _candidates) _items.Add(new GodOverlay.Item(e, e == HoverEdge ? GodOverlay.Hover : col, e == HoverEdge ? 3.6f : 2.4f));
                if (HoverEdge >= 0 && !_valid.Contains(HoverEdge)) _items.Add(new GodOverlay.Item(HoverEdge, GodOverlay.Invalid, 3.0f));
                GodOverlay.ShowEdges(_items);
            }
            base.Update();
        }

        public override void WorldClick()
        {
            if (HoverEdge >= 0) TryAt(HoverEdge);
            base.WorldClick();
        }

        /// <summary>"free (already paid for)" or "1 planks and 1 bricks (pays for 3 segments)".</summary>
        public static string NextSegmentCost(WorldState s)
        {
            var cost = EconomyApi.RoadCostFor(s, 1);
            bool free = true;
            foreach (int n in cost) if (n > 0) free = false;
            int per = s.Economy.Tuning.RoadSegmentsPerCost + (s.Economy.Bonus != null ? s.Economy.Bonus.RoadSegments : 0);
            return free ? "free (already paid for)" : EconomyApi.CostText(cost) + " (pays for " + per + " segments)";
        }

        public override void Overlay()
        {
            if (HoverEdge < 0) return;
            var s = WorldBootstrap.Instance.Sim.State;
            if (HoverResult.Ok)
                GodToolbar.Tooltip(RemoveMode ? "<b>Remove this road segment</b>\nCarriers on it stop at the next flag with their load until a route returns."
                                              : "<b>Build a road segment here</b>\nThis segment: " + NextSegmentCost(s) + ". Click to build.");
            else GodToolbar.Tooltip("<color=#FF8A70><b>" + (RemoveMode ? "Can't remove." : "Can't build a road here.") + "</b></color>\n" + HoverResult.Message);
            base.Overlay();
        }

        public override void Panel(Rect area)
        {
            var s = WorldBootstrap.Instance.Sim.State;
            float pad = UiStyle.Px(14), y = area.y + pad, w = area.width - 2 * pad;
            GUI.Label(new Rect(area.x + pad, y, w, UiStyle.Px(40)), "Road", UiStyle.Title);
            y += UiStyle.Px(46);
            var btn = new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(20) };
            var on = new GUIStyle(btn);
            on.normal.background = on.hover.background = UiStyle.SolidTexture(new Color(0.62f, 0.44f, 0.16f, 0.98f));
            float bw = (w - UiStyle.Px(8)) / 2;
            if (GUI.Button(new Rect(area.x + pad, y, bw, UiStyle.Px(44)), "Build", RemoveMode ? btn : on)) SetRemoveMode(false);
            if (GUI.Button(new Rect(area.x + pad + bw + UiStyle.Px(8), y, bw, UiStyle.Px(44)), "Remove  [X]", RemoveMode ? on : btn)) SetRemoveMode(true);
            y += UiStyle.Px(54);
            var small = new GUIStyle(UiStyle.Small) { wordWrap = true, richText = true };
            string text = RemoveMode
                ? "Your roads glow orange. Click one to remove it. Shipments on it stop at the next flag and say why; nothing is refunded."
                : "Green segments touch your roads, buildings or city. " + EconomyText.RoadCostRule(s) + "\nNext segment: <b>" + NextSegmentCost(s) + "</b>.";
            GUI.Label(new Rect(area.x + pad, y, w, area.yMax - y - pad), text, small);
        }
    }
}
