using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// F1 developer tool "Council": makes an election available at once in the home village (developer staging, see
    /// <see cref="CouncilFixture"/>), asks the candidate to come now, or stages the brief's example trays and opens the
    /// council. Never used by normal play.
    /// </summary>
    public static class CouncilDevTool
    {
        public const string ToolId = "council";

        public static void Register()
        {
            GodToolbar.Register(new GodTool
            {
                Id = ToolId, Label = "Council", Order = 96, PanelWidth = 520, DrawPanel = Panel,
                TipText = () => "Council: make an election available at once (developer staging).",
            });
        }

        static void Panel(GodTool t, Rect a)
        {
            var b = WorldBootstrap.Instance; var d = CouncilDirector.Instance;
            float x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), y = a.y + UiStyle.Px(10), bh = UiStyle.Px(40);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(70)), "Developer staging: edits the village directly so a test election can start. Normal play only sends commands.", UiStyle.Small);
            y += UiStyle.Px(76);
            int home = d != null ? d.HomeSettlement : 0;
            if (GUI.Button(new Rect(x, y, w, bh), "Make the home village eligible and ask now", UiStyle.Button) && b != null)
            {
                int n = CouncilFixture.MakeEligible(b.Sim.State, home);
                CouncilDirector.ApproachDelay = 0f;
                Hud.Notice("Council: " + n + " residents could stand. The candidate will come.", false, 4f);
            }
            y += bh + UiStyle.Px(6);
            if (GUI.Button(new Rect(x, y, w, bh), "Stage the example trays (4+9, 5+8, 6+7) and open", UiStyle.Button) && b != null && d != null)
            {
                CouncilDirector.Disabled = true;
                CouncilFixture.StageExample(b.Sim.State, home, 0);
                ViewSwitcher.Instance?.SetView(ViewMode.FirstPerson);
                EncounterDirectory.TryStart(CouncilEncounter.Id, null, home);
            }
            y += bh + UiStyle.Px(6);
            var sc = b != null ? CouncilApi.Get(b.Sim.State, home) : null;
            GUI.Label(new Rect(x, y, w, UiStyle.Px(80)), sc == null ? "No election yet. Eligible: " + (b != null && CouncilApi.CanOffer(b.Sim.State, home)) : "Election: " + sc.Status + (sc.Election != null ? ", phase " + sc.Election.Phase + ", claims " + sc.Election.ClaimsOf(0) + "/" + sc.Election.ClaimsOf(1) + "/" + sc.Election.ClaimsOf(2) + "/" + sc.Election.ClaimsOf(3) : ""), UiStyle.Small);
        }
    }
}
