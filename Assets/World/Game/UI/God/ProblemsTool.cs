using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>Inspect panel content for one delivery job (selected from the Problems list when no carrier holds it).</summary>
    public sealed class JobInspectable : IInspectable
    {
        public readonly int JobId;
        public JobInspectable(int jobId) { JobId = jobId; }
        public string Title => WorldBootstrap.Instance == null ? "Delivery" : EconomyApi.DescribeJob(WorldBootstrap.Instance.Sim.State, JobId).Title;
        public IEnumerable<string> Lines => WorldBootstrap.Instance == null ? new string[0] : EconomyText.JobLines(WorldBootstrap.Instance.Sim.State, JobId);
    }

    /// <summary>
    /// Problems panel: every stalled site, building and delivery with the sim's reason sentence. Clicking an entry
    /// focuses the God camera on it and selects it. The list is rebuilt from state at most every 0.4 s (it walks the
    /// buildings and open jobs, never the flags or segments).
    /// </summary>
    public sealed class ProblemsTool : GodTool
    {
        public const string ToolId = "problems";

        /// <summary>Resolves a building id / carrier id to the scene object to select (set by the economy views).</summary>
        public static Func<int, Component> BuildingResolver, CarrierResolver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { BuildingResolver = null; CarrierResolver = null; }

        public List<EconomyProblem> Current { get; private set; } = new List<EconomyProblem>();
        private float _refreshAt;
        private Vector2 _scroll;

        public ProblemsTool()
        {
            Id = ToolId; Label = "Problems"; Hotkey = KeyCode.P; Order = 50; PanelWidth = 640f;
            BadgeText = () => { RefreshIfDue(); return Current.Count > 0 ? "<color=#FFB060>(" + Current.Count + ")</color>" : null; };
            TipText = () => "Everything that is stalled, with the reason. Click an entry to look at it.";
        }

        public override bool HasPanel => true;

        public void Refresh()
        {
            var b = WorldBootstrap.Instance;
            if (b != null) Current = EconomyText.Problems(b.Sim.State);
            _refreshAt = Time.unscaledTime + 0.4f;
        }

        private void RefreshIfDue() { if (Time.unscaledTime >= _refreshAt) Refresh(); }

        /// <summary>Focuses the God camera on a problem and selects it (building view, carrier, or the job itself).</summary>
        public static IInspectable Focus(EconomyProblem p)
        {
            var boot = WorldBootstrap.Instance;
            if (boot == null) return null;
            var s = boot.Sim.State;
            IInspectable target = null;
            Vector3 at = boot.Layout.Home;
            if (!p.IsJob)
            {
                var b = EconomyApi.GetBuilding(s, p.Id);
                if (b != null) at = BoardSpace.Vertex(b.Vertex);
                var c = BuildingResolver?.Invoke(p.Id);
                if (c != null) { target = c as IInspectable; at = c.transform.position; }
            }
            else
            {
                var j = EconomyLook.Job(s, p.Id);
                var cv = j != null && j.CarrierId >= 0 ? CarrierResolver?.Invoke(j.CarrierId) : null;
                if (cv != null) { target = cv as IInspectable; at = cv.transform.position; }
                else
                {
                    target = new JobInspectable(p.Id);
                    var dst = j != null ? EconomyApi.GetBuilding(s, j.DestId) : null;
                    if (dst != null) at = BoardSpace.Vertex(dst.Vertex);
                }
            }
            if (boot.Views.CurrentView != ViewMode.God) boot.Views.SetView(ViewMode.God);
            boot.GodCam.SetFocus(at, Mathf.Min(boot.GodCam.Size, 45f));
            if (target != null) boot.Picker.Select(target);
            return target;
        }

        public override void Panel(Rect area)
        {
            RefreshIfDue();
            float pad = UiStyle.Px(14), y = area.y + pad;
            GUI.Label(new Rect(area.x + pad, y, area.width - 2 * pad, UiStyle.Px(40)), "Problems", UiStyle.Title);
            y += UiStyle.Px(46);
            if (Current.Count == 0)
            {
                GUI.Label(new Rect(area.x + pad, y, area.width - 2 * pad, UiStyle.Px(80)), "Nothing is stalled. Every site, building and delivery is progressing.", UiStyle.Small);
                return;
            }
            var hint = new GUIStyle(UiStyle.Small) { fontSize = UiStyle.Px(18) };
            GUI.Label(new Rect(area.x + pad, y, area.width - 2 * pad, UiStyle.Px(30)), Current.Count + " stalled. Click an entry to look at it.", hint);
            y += UiStyle.Px(34);
            var btn = new GUIStyle(UiStyle.Button) { alignment = TextAnchor.UpperLeft, fontSize = UiStyle.Px(19), richText = true, wordWrap = true, padding = new RectOffset(UiStyle.Px(10), UiStyle.Px(8), UiStyle.Px(6), UiStyle.Px(6)) };
            var view = new Rect(area.x + pad, y, area.width - 2 * pad, area.yMax - y - pad);
            float w = view.width - UiStyle.Px(18);
            var heights = new float[Current.Count];
            float total = 0;
            for (int i = 0; i < Current.Count; i++)
            {
                heights[i] = Mathf.Max(UiStyle.Px(58), btn.CalcHeight(new GUIContent(Text(Current[i])), w));
                total += heights[i] + UiStyle.Px(6);
            }
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, w, total));
            float ry = 0;
            for (int i = 0; i < Current.Count; i++)
            {
                if (GUI.Button(new Rect(0, ry, w, heights[i]), Text(Current[i]), btn)) Focus(Current[i]);
                ry += heights[i] + UiStyle.Px(6);
            }
            GUI.EndScrollView();
        }

        private static string Text(EconomyProblem p) =>
            "<b>" + (p.IsJob ? "Delivery: " : "") + p.Title + "</b>\n<color=" + EconomyText.Warn + ">" + p.Sentence + "</color>";
    }
}
