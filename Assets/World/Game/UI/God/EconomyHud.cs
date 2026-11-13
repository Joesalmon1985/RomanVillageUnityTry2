using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>An inspectable that stands for one economy building or site (its view, sign or worker).</summary>
    public interface IEconomyBuilding : IInspectable
    {
        int BuildingId { get; }
    }

    /// <summary>
    /// Economy feedback in both views: command toasts (accepted / refused with the sim's sentence), the round-start
    /// banner, the commission banner, and the priority / switch / cancel buttons under the inspect panel for a
    /// selected building or site (God View, or first person with the cursor free).
    /// </summary>
    public sealed class EconomyHud : MonoBehaviour
    {
        public static EconomyHud Instance { get; private set; }

        private struct ToastItem { public string Text; public bool Error; public float Until; }
        private static readonly List<ToastItem> _toasts = new List<ToastItem>();
        public static string LastToast { get; private set; }
        public static bool LastToastWasError { get; private set; }

        public static string BannerText { get; private set; }
        public static bool BannerBig { get; private set; }
        private static float _bannerUntil;
        /// <summary>Last round-start banner text ("Round 3: numbers 1 2 4. Forest batch 12, Hills batch 12.").</summary>
        public static string LastRoundBanner { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _toasts.Clear(); LastToast = null; BannerText = null; LastRoundBanner = null; }

        private WorldBootstrap _boot;

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            boot.Clock.Ticked += OnTicked;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
        }

        public static void Toast(string text, bool error)
        {
            if (string.IsNullOrEmpty(text)) return;
            LastToast = text; LastToastWasError = error;
            _toasts.Add(new ToastItem { Text = text, Error = error, Until = Time.unscaledTime + (error ? 5f : 3.5f) });
            while (_toasts.Count > 4) _toasts.RemoveAt(0);
        }

        public static void Banner(string text, float seconds, bool big = false)
        {
            BannerText = text; BannerBig = big; _bannerUntil = Time.unscaledTime + seconds;
        }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            RoundStarted round = null;
            List<BatchAuthorised> batches = null;
            bool commission = false;
            foreach (var e in events)
            {
                if (e is RoundStarted r) round = r;
                else if (e is BatchAuthorised b) (batches ??= new List<BatchAuthorised>()).Add(b);
                else if (e is CommissionCompleted) commission = true;
            }
            var s = _boot.Sim.State;
            if (round != null)
            {
                var parts = new List<string>();
                if (batches != null)
                    foreach (var b in batches)
                    {
                        var raw = TerrainInfo.RawWare(s.Board.HexTerrain[b.Hex]);
                        parts.Add(s.Board.HexTerrain[b.Hex] + " " + b.Amount + (raw.HasValue ? " " + EconomyApi.WareName(raw.Value) : ""));
                    }
                string nums = round.Numbers == null || round.Numbers.Length == 0 ? "none" : string.Join(" ", round.Numbers);
                LastRoundBanner = "Round " + round.Round + ": numbers " + nums + ". "
                    + (parts.Count > 0 ? "Batches: " + string.Join(", ", parts) + "." : "No batch next to your cities.");
                Banner(LastRoundBanner, 6f);
            }
            if (commission)
            {
                var names = new List<string>();
                foreach (var t in EconomyLook.CommissionTypes) names.Add(EconomyApi.DescribeType(s, t).Name.ToLowerInvariant());
                Banner("Commission complete! The " + EconomyText.JoinAnd(names) + " are working, and the storehouse holds "
                    + s.Economy.Tuning.CommissionPlanks + " planks and " + s.Economy.Tuning.CommissionBricks + " bricks. Talk to Quartermaster Bram.", 9f, true);
            }
        }

        // ---------------- drawing ----------------

        private void OnGUI()
        {
            if (_boot == null || Hud.MenuOpen) return;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return;
            GUI.depth = -5;
            float now = Time.unscaledTime;
            _toasts.RemoveAll(t => t.Until < now);

            if (!string.IsNullOrEmpty(BannerText) && now < _bannerUntil)
            {
                var st = new GUIStyle(BannerBig ? UiStyle.Title : UiStyle.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
                if (BannerBig) st.fontSize = UiStyle.Px(30); else st.normal.textColor = UiStyle.AccentColor;
                float w = UiStyle.Px(BannerBig ? 1150 : 1000);
                float h = st.CalcHeight(new GUIContent(BannerText), w - UiStyle.Px(40)) + UiStyle.Px(BannerBig ? 36 : 20);
                var r = new Rect((Screen.width - w) / 2, UiStyle.Px(BannerBig ? 170 : 72), w, h);
                UiStyle.DrawSolidPanel(r);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, UiStyle.Px(4)), UiStyle.SolidTexture(BannerBig ? new Color(0.35f, 0.92f, 0.35f) : UiStyle.AccentColor));
                GUI.Label(new Rect(r.x + UiStyle.Px(20), r.y, w - UiStyle.Px(40), h), BannerText, st);
            }

            // Toasts above the toolbar (God View) or above the hint line (first person).
            float y = (_boot.Views.CurrentView == ViewMode.God ? GodToolbar.BarTop : Screen.height - UiStyle.Px(60)) - UiStyle.Px(12);
            var ts = new GUIStyle(UiStyle.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = UiStyle.Px(22) };
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                float w = UiStyle.Px(900);
                float h = ts.CalcHeight(new GUIContent(t.Text), w - UiStyle.Px(30)) + UiStyle.Px(14);
                y -= h + UiStyle.Px(6);
                var r = new Rect((Screen.width - w) / 2, y, w, h);
                GUI.DrawTexture(r, UiStyle.SolidTexture(t.Error ? new Color(0.35f, 0.08f, 0.06f, 0.92f) : new Color(0.08f, 0.22f, 0.10f, 0.92f)));
                GUI.DrawTexture(new Rect(r.x, r.y, UiStyle.Px(6), r.height), UiStyle.SolidTexture(t.Error ? new Color(1f, 0.35f, 0.25f) : new Color(0.4f, 0.95f, 0.45f)));
                GUI.Label(new Rect(r.x + UiStyle.Px(15), r.y, w - UiStyle.Px(30), h), t.Text, ts);
            }

            DrawBuildingActions();
        }

        /// <summary>Priority / switch / cancel buttons under the inspect panel for a selected building or site.</summary>
        private void DrawBuildingActions()
        {
            var picker = Picker.Instance;
            if (picker == null || !(picker.Selected is IEconomyBuilding sel)) return;
            if (sel is Object o && o == null) return;
            var panel = InspectPanel.ScreenRect;
            if (panel.width <= 0) return;
            var s = _boot.Sim.State;
            var b = EconomyApi.GetBuilding(s, sel.BuildingId);
            if (b == null || b.State == BuildingState.Removed || b.Type == BuildingType.Storehouse) return;
            bool cursorFree = Cursor.lockState != CursorLockMode.Locked;
            float pad = UiStyle.Px(14), bh = UiStyle.Px(46);
            bool site = EconomyLook.IsSite(b);
            float h = !cursorFree ? UiStyle.Px(80) : pad * 2 + (site ? bh : bh * 2 + UiStyle.Px(8) + UiStyle.Px(30));
            var r = new Rect(panel.x, panel.yMax + UiStyle.Px(8), panel.width, h);
            GodToolbar.AddUiRect(r);
            UiStyle.DrawPanel(r);
            if (!cursorFree)
            {
                GUI.Label(new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, h - 2 * pad), "Read the sign by its flag (E), or Tab to God View, to change it.", UiStyle.Small);
                return;
            }
            var btn = new GUIStyle(UiStyle.Button) { fontSize = UiStyle.Px(21) };
            var on = new GUIStyle(btn);
            on.normal.background = on.hover.background = UiStyle.SolidTexture(new Color(0.62f, 0.44f, 0.16f, 0.98f));
            float x = r.x + pad, yy = r.y + pad, w = r.width - 2 * pad;
            if (site)
            {
                if (GUI.Button(new Rect(x, yy, w, bh), "Cancel site (materials return)", btn)) GodCommands.CancelSite(b.Id);
                return;
            }
            GUI.Label(new Rect(x, yy, w, UiStyle.Px(28)), "Priority", UiStyle.Small);
            yy += UiStyle.Px(30);
            float third = (w - UiStyle.Px(12)) / 3;
            for (int i = 0; i < 3; i++)
            {
                var p = (Priority)i;
                if (GUI.Button(new Rect(x + i * (third + UiStyle.Px(6)), yy, third, bh), p.ToString(), b.Priority == p ? on : btn) && b.Priority != p)
                    GodCommands.SetPriority(b.Id, p);
            }
            yy += bh + UiStyle.Px(8);
            if (GUI.Button(new Rect(x, yy, w, bh), b.Enabled ? "Switch off" : "Switch on", b.Enabled ? btn : on)) GodCommands.Toggle(b.Id, !b.Enabled);
        }
    }
}
