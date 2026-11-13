using UnityEngine;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Registers the economy's God View tools (left to right): Build (10), Road (20), Select/Inspect (40),
    /// Problems (50), Wares (60), End Round (90, an action). Strategy and military agents add theirs with
    /// <see cref="GodToolbar.Register"/> in the gaps (see Docs/ECONOMY_UI.md).
    /// </summary>
    public static class EconomyGodTools
    {
        public const string EndRoundId = "end_round";

        public static void RegisterDefaults()
        {
            GodToolbar.Register(new BuildTool());
            GodToolbar.Register(new RoadTool());
            GodToolbar.Register(new WaresTool());
            GodToolbar.Register(new GodTool
            {
                Id = GodToolbar.SelectId, Label = "Inspect", Hotkey = KeyCode.V, Order = 40,
                TipText = () => "Click a building, site, carrier, road or hex to inspect it.",
            });
            GodToolbar.Register(new ProblemsTool());
            GodToolbar.Register(new GodTool
            {
                Id = EndRoundId, Label = "End Round", Order = 90, IsAction = true,
                OnRun = _ => GodCommands.EndRound(),
                BadgeText = () => BellBadge(),
                TipText = () =>
                {
                    var r = GodCommands.CheckEndRound();
                    return r.Ok ? "Ring in the next round: new numbers are drawn and batches authorised. Unused batch units carry over (up to the cap)."
                                : "<color=#FF9A80>" + r.Message + "</color>";
                },
            });
        }

        /// <summary>"(23 s)" while the harvest season still holds the bell (EconomyApi.BellTicksLeft), else null.</summary>
        public static string BellBadge()
        {
            var b = WorldBootstrap.Instance;
            if (b == null) return null;
            long t = EconomyApi.BellTicksLeft(b.Sim.State);
            return t > 0 ? "<color=#BBB0A0>(" + ((t + World.Sim.WorldSim.TicksPerSecond - 1) / World.Sim.WorldSim.TicksPerSecond) + " s)</color>" : null;
        }

        public static ProblemsTool Problems => GodToolbar.Find(ProblemsTool.ToolId) as ProblemsTool;
        public static BuildTool Build => GodToolbar.Find(BuildTool.ToolId) as BuildTool;
        public static RoadTool Road => GodToolbar.Find(RoadTool.ToolId) as RoadTool;

        /// <summary>Switches to God View with the Build tool open (optionally preselecting a type) framing the village.</summary>
        public static void OpenBuildPlans(BuildingType? type = null)
        {
            var boot = WorldBootstrap.Instance;
            if (boot == null || GodToolbar.Instance == null) return;
            boot.Views.SetView(ViewMode.God);
            var t = GodToolbar.Instance.Activate(BuildTool.ToolId) as BuildTool;
            if (t != null && type.HasValue) t.SelectType(type.Value);
        }
    }
}
