using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Build tool: the menu is <see cref="EconomyApi.BuildMenu"/> (name, cost coloured by what the storehouse holds,
    /// needs, makes and recipe text). Only the legal flags for the selected type glow (<see cref="EconomyApi.LegalBuildFlags"/>,
    /// which is limited to territory); the list is recomputed only when buildings, cities or the type change, never
    /// every frame. Hovering a flag shows the sim's sentence (<c>sim.Check(PlaceBuilding)</c>); a click submits it.
    /// </summary>
    public sealed class BuildTool : GodTool
    {
        public const string ToolId = "build";
        public BuildingType Selected { get; private set; } = BuildingType.Woodcutter;
        public int HoverVertex { get; private set; } = -1;
        public CommandResult HoverResult { get; private set; }
        /// <summary>Legal flags for the selected type (cached; ascending ids).</summary>
        public IReadOnlyList<int> LegalFlags => _legal;
        /// <summary>Number of times the legal flag list was recomputed (tests: not every frame).</summary>
        public int Recomputes { get; private set; }

        private List<int> _legal = new List<int>();
        private readonly HashSet<int> _legalSet = new HashSet<int>();
        private long _signature = long.MinValue;
        private int _forced = -1, _shownHover = -2;
        private Vector2 _scroll;
        private readonly List<GodOverlay.Item> _items = new List<GodOverlay.Item>();

        public BuildTool()
        {
            Id = ToolId; Label = "Build"; Hotkey = KeyCode.B; Order = 10; CapturesClicks = true; PanelWidth = 560f;
            TipText = () => "Plan a building on a green flag. Carriers bring its planks and bricks.";
        }

        public override bool HasPanel => true;

        public override void Activate() { _signature = long.MinValue; _shownHover = -2; base.Activate(); }
        public override void Deactivate() { GodOverlay.Hide(); HoverVertex = -1; _forced = -1; base.Deactivate(); }

        public void SelectType(BuildingType t) { Selected = t; _signature = long.MinValue; _shownHover = -2; }

        /// <summary>Hover a flag without the mouse (captures, scripted demos); -1 returns to the mouse.</summary>
        public void ForceHover(int vertex) { _forced = vertex; }

        public bool IsValid(int v) { Refresh(); return _legalSet.Contains(v); }

        public CommandResult Check(int vertex) => GodCommands.Check(new PlaceBuilding { Type = Selected, Vertex = vertex });

        private static long Signature(WorldState s, BuildingType t)
        {
            long sig = (long)t * 1000003L + s.Economy.Cities.Count * 7919L;
            foreach (var b in s.Economy.Buildings) sig = sig * 31 + (b.State == BuildingState.Removed ? 1 : 2) + b.Vertex * 3;
            return sig;
        }

        /// <summary>Recomputes the legal flags if the buildings, cities or type changed since the last call.</summary>
        public void Refresh()
        {
            var b = WorldBootstrap.Instance; if (b == null) return;
            var s = b.Sim.State;
            long sig = Signature(s, Selected);
            if (sig == _signature) return;
            _signature = sig;
            _legal = EconomyApi.LegalBuildFlags(s, Selected);
            _legalSet.Clear();
            foreach (int v in _legal) _legalSet.Add(v);
            _shownHover = -2;
            Recomputes++;
        }

        /// <summary>The same path as a click: submit PlaceBuilding for the selected type at a flag.</summary>
        public CommandResult TryPlace(int vertex)
        {
            var r = GodCommands.PlaceBuilding(Selected, vertex);
            _signature = long.MinValue;
            return r;
        }

        public override void Update()
        {
            Refresh();
            HoverVertex = _forced >= 0 ? _forced : GodToolbar.MouseOverUi() ? -1 : BoardPicker.VertexUnderMouse();
            if (HoverVertex >= 0) HoverResult = Check(HoverVertex);
            if (HoverVertex != _shownHover) ShowOverlay();
            base.Update();
        }

        public void ShowOverlay()
        {
            Refresh();
            _shownHover = HoverVertex;
            _items.Clear();
            foreach (int v in _legal) _items.Add(new GodOverlay.Item(v, v == HoverVertex ? GodOverlay.Hover : GodOverlay.Valid, v == HoverVertex ? 3.4f : 2.6f));
            if (HoverVertex >= 0 && !_legalSet.Contains(HoverVertex)) _items.Add(new GodOverlay.Item(HoverVertex, GodOverlay.Invalid, 3.0f));
            GodOverlay.ShowVertices(_items);
        }

        public override void WorldClick()
        {
            if (HoverVertex >= 0) TryPlace(HoverVertex);
            base.WorldClick();
        }

        public override void Overlay()
        {
            if (HoverVertex < 0) return;
            var s = WorldBootstrap.Instance.Sim.State;
            var view = EconomyApi.DescribeType(s, Selected);
            string text = HoverResult.Ok
                ? "<b>Plan a " + view.Name.ToLowerInvariant() + " here</b>\nCosts " + view.CostText + ". Click to plan the site."
                : "<color=#FF8A70><b>Can't build a " + view.Name.ToLowerInvariant() + " here.</b></color>\n" + HoverResult.Message;
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : null;
            if (_forced >= 0 && cam != null)
            {
                var sp = cam.WorldToScreenPoint(BoardSpace.Vertex(_forced));
                GodToolbar.Tooltip(text, new Vector2(sp.x, Screen.height - sp.y));
            }
            else GodToolbar.Tooltip(text);
            base.Overlay();
        }

        private static bool CanAfford(WorldState s, BuildingType t)
        {
            var cost = EconomyApi.Def(s, t).Cost;
            for (int w = 0; w < Wares.Count; w++) if (cost[w] > 0 && EconomyApi.StorehouseCount(s, (Ware)w) < cost[w]) return false;
            return true;
        }

        private List<BuildingTypeView> _menu;
        private int _menuFor = -1;

        public override void Panel(Rect area)
        {
            var s = WorldBootstrap.Instance.Sim.State;
            if (_menu == null || _menuFor != Time.frameCount / 30) { _menu = EconomyApi.BuildMenu(s); _menuFor = Time.frameCount / 30; }
            float pad = UiStyle.Px(14), y = area.y + pad, w = area.width - 2 * pad;
            GUI.Label(new Rect(area.x + pad, y, w, UiStyle.Px(40)), "Build", UiStyle.Title);
            y += UiStyle.Px(42);

            // Selected type: everything the sim says about it.
            var sel = EconomyApi.DescribeType(s, Selected);
            var small = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(19), wordWrap = true, richText = true };
            string detail = "<b><color=#F2C060>" + sel.Name + "</color></b>  <size=" + UiStyle.Px(16) + ">(" + sel.Size.ToString().ToLowerInvariant() + ")</size>\n"
                + "<i>" + sel.Purpose + "</i>\n"
                + "Cost: <color=" + (CanAfford(s, Selected) ? "#C8F0B0" : "#FF9A80") + ">" + sel.CostText + "</color>"
                + (string.IsNullOrEmpty(sel.NeedsText) ? "" : "\n" + sel.NeedsText)
                + (string.IsNullOrEmpty(sel.MakesText) ? "" : "\n" + sel.MakesText)
                + (string.IsNullOrEmpty(sel.RecipeText) ? "" : "\nRecipe: " + sel.RecipeText)
                + "\n<color=#BBB0A0>" + _legal.Count + " green flag" + (_legal.Count == 1 ? "" : "s") + " in your territory. Click one; right click or B to stop.</color>";
            float dh = small.CalcHeight(new GUIContent(detail), w - UiStyle.Px(16)) + UiStyle.Px(14);
            var box = new Rect(area.x + pad, y, w, dh);
            GUI.DrawTexture(box, UiStyle.SolidTexture(new Color(0.16f, 0.13f, 0.10f, 0.9f)));
            GUI.Label(new Rect(box.x + UiStyle.Px(8), box.y + UiStyle.Px(7), w - UiStyle.Px(16), dh), detail, small);
            y += dh + UiStyle.Px(10);

            var btn = new GUIStyle(UiStyle.Button) { alignment = TextAnchor.MiddleLeft, fontSize = UiStyle.Px(19), richText = true, wordWrap = false, padding = new RectOffset(UiStyle.Px(10), UiStyle.Px(6), UiStyle.Px(2), UiStyle.Px(2)) };
            var on = new GUIStyle(btn);
            on.normal.background = on.hover.background = UiStyle.SolidTexture(new Color(0.62f, 0.44f, 0.16f, 0.98f));
            var view = new Rect(area.x + pad, y, w, area.yMax - y - pad);
            float rowH = UiStyle.Px(56);
            var content = new Rect(0, 0, view.width - UiStyle.Px(18), _menu.Count * (rowH + UiStyle.Px(4)));
            _scroll = GUI.BeginScrollView(view, _scroll, content);
            float ry = 0;
            foreach (var t in _menu)
            {
                string costCol = CanAfford(s, t.Type) ? "#C8F0B0" : "#FF9A80";
                string line2 = !string.IsNullOrEmpty(t.RecipeText) ? t.RecipeText : t.MakesText.Replace("Makes: ", "");
                if (!string.IsNullOrEmpty(t.TerrainText)) line2 = "by " + t.TerrainText + ": " + line2;
                string text = "<b>" + t.Name + "</b>   <color=" + costCol + ">" + t.CostText + "</color>\n<size=" + UiStyle.Px(15) + "><color=#D8D0C0>" + line2 + "</color></size>";
                if (GUI.Button(new Rect(0, ry, content.width, rowH), text, t.Type == Selected ? on : btn)) SelectType(t.Type);
                ry += rowH + UiStyle.Px(4);
            }
            GUI.EndScrollView();
        }
    }
}
