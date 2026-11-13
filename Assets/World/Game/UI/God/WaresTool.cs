using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Ware overview: all 14 wares with the storehouse's stock, what is in the whole village (stores and carriers), and
    /// which building types make and use each (EconomyApi.ProducersOf / ConsumersOf). Text is rebuilt twice a second.
    /// </summary>
    public sealed class WaresTool : GodTool
    {
        public const string ToolId = "wares";
        private float _refreshAt;
        private readonly string[] _rows = new string[Wares.Count];
        private Vector2 _scroll;

        public WaresTool()
        {
            Id = ToolId; Label = "Wares"; Hotkey = KeyCode.G; Order = 60; PanelWidth = 720f;
            TipText = () => "What the storehouse holds, and which buildings make and use each ware.";
        }

        public override bool HasPanel => true;

        private static string Names(WorldState s, List<BuildingType> types)
        {
            if (types.Count == 0) return "-";
            var n = new List<string>();
            foreach (var t in types) n.Add(EconomyApi.Def(s, t).Name);
            return string.Join(", ", n);
        }

        private void Rebuild(WorldState s)
        {
            _refreshAt = Time.unscaledTime + 0.5f;
            var inWorld = EconomyApi.WaresInWorld(s);
            foreach (var w in Wares.All)
            {
                int store = EconomyApi.StorehouseCount(s, w);
                string uses = Names(s, EconomyApi.ConsumersOf(s, w));
                if (w == Ware.Planks || w == Ware.Bricks) uses += (uses == "-" ? "" : "; ") + "buildings, roads";
                _rows[(int)w] = "<b>" + EconomyLook.WareTag(w) + "</b>   storehouse <b>" + store + "</b>   <color=#BBB0A0>in the village " + inWorld[(int)w] + "</color>\n"
                    + "<size=" + UiStyle.Px(16) + "><color=#D8D0C0>made by " + Names(s, EconomyApi.ProducersOf(s, w)) + ";  used by " + uses + "</color></size>";
            }
        }

        public override void Panel(Rect area)
        {
            var s = WorldBootstrap.Instance.Sim.State;
            if (Time.unscaledTime >= _refreshAt || _rows[0] == null) Rebuild(s);
            float pad = UiStyle.Px(14), y = area.y + pad, w = area.width - 2 * pad;
            GUI.Label(new Rect(area.x + pad, y, w, UiStyle.Px(40)), "Wares", UiStyle.Title);
            y += UiStyle.Px(46);
            var st = new GUIStyle(UiStyle.Small) { richText = true, wordWrap = true, fontSize = UiStyle.Px(19) };
            var view = new Rect(area.x + pad, y, w, area.yMax - y - pad);
            float cw = view.width - UiStyle.Px(18), sq = UiStyle.Px(18);
            var heights = new float[Wares.Count];
            float total = 0;
            for (int i = 0; i < Wares.Count; i++) { heights[i] = st.CalcHeight(new GUIContent(_rows[i]), cw - sq - UiStyle.Px(12)) + UiStyle.Px(8); total += heights[i]; }
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, cw, total));
            float ry = 0;
            for (int i = 0; i < Wares.Count; i++)
            {
                if (i % 2 == 0) GUI.DrawTexture(new Rect(0, ry, cw, heights[i]), UiStyle.SolidTexture(new Color(1f, 1f, 1f, 0.04f)));
                GUI.DrawTexture(new Rect(UiStyle.Px(2), ry + UiStyle.Px(6), sq, sq), UiStyle.SolidTexture(EconomyLook.WareColor((Ware)i)));
                GUI.Label(new Rect(sq + UiStyle.Px(12), ry + UiStyle.Px(3), cw - sq - UiStyle.Px(12), heights[i]), _rows[i], st);
                ry += heights[i];
            }
            GUI.EndScrollView();
        }
    }
}
