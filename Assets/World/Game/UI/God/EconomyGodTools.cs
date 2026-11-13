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
                TipText = () =>
                {
                    var r = GodCommands.CheckEndRound();
                    return r.Ok ? "Ring in the next round: new numbers are drawn and batches authorised. Unused batch units carry over (up to the cap)."
                                : "<color=#FF9A80>" + r.Message + "</color>";
                },
            });
        }

        public static ProblemsTool Problems => GodToolbar.Find(ProblemsTool.ToolId) as ProblemsTool;
    }
}
