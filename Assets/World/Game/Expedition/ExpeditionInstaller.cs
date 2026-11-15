using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// Package I6's entry point (PRD_CRYSTALS appendix B): the expedition director (Mira's loop, the six companions,
    /// leaving and rejoining, the cues), the site and tower views, the Ash runners, the journal and the map layer, and
    /// the AutoPlay scripts <c>earth_first</c>, <c>earth_stolen</c>, <c>mira_alone</c> and <c>expedition_captures</c>
    /// (<see cref="ExpeditionScripts"/>). Called by <see cref="WorldBootstrap"/> after <see cref="InteriorsInstaller"/>
    /// and before <see cref="DevToolsInstaller"/>. The director exists only while the world has a campaign: it is
    /// created at once when the scene is built on a state with one (a load, a developer start that already started
    /// it), or on the tick <c>StartCampaign</c> is applied. With no campaign nothing is created and nothing changes.
    /// Owner: I6 (all of <c>Game/Expedition/**</c>; from cry-J0 the set-piece packages add <c>Sites/**</c>).
    /// </summary>
    public static class ExpeditionInstaller
    {
        /// <summary>True runs the world without the expedition's views even when a campaign runs (tests of other packages).</summary>
        public static bool Disabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Disabled = false;

        public static void Install(WorldBootstrap boot)
        {
            ExpeditionScripts.Register();
            if (Disabled) return;
            // The prince holds pitches that are not urgent while Mira has the player's ear (PRD 5.5, 5.9).
            PrinceDirector.Hush = () => ExpeditionDirector.Instance != null && ExpeditionDirector.Instance.MiraHasTheFloor;
            if (boot.Sim.State.Campaign != null) Create(boot, null);
            boot.Clock.Ticked += events =>
            {
                if (Disabled || boot == null || boot.Sim == null || boot.Sim.State.Campaign == null) return;
                bool started = false;
                if (events != null) foreach (var e in events) if (e is CampaignStarted) { started = true; break; }
                var d = ExpeditionDirector.Instance;
                if (d != null && started)
                {
                    // "Begin again": a new campaign on this world. Out with the old views.
                    Object.DestroyImmediate(d.gameObject);
                    d = null;
                }
                if (d == null) Create(boot, events);
            };
        }

        private static void Create(WorldBootstrap boot, IReadOnlyList<SimEvent> startEvents)
        {
            var go = new GameObject("Expedition director");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<ExpeditionDirector>();
            d.Begin(boot);
            // The tick that started the campaign also carried the first word (the Ash runners' opening report).
            if (startEvents != null) d.Feed(startEvents);
        }
    }
}
