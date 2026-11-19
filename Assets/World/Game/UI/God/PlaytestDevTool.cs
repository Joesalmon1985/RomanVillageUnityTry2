using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The F1 developer tool "Playtest": jumps straight into the Ward duel with Mira or the rhythm party dance with
    /// Sol, for manually testing either minigame without walking to them and talking first. Starts the same encounter
    /// normal dialogue starts (<see cref="EncounterDirectory.TryStart(string, Npc)"/>); nothing here mutates the sim.
    /// Never used by normal play.
    /// </summary>
    public static class PlaytestDevTool
    {
        public const string ToolId = "playtest";
        private static string s_last;

        public static void Register()
        {
            GodToolbar.Register(new GodTool
            {
                Id = ToolId, Label = "Playtest", Order = 97, Hotkey = KeyCode.J, PanelWidth = 480, DrawPanel = Panel,
                TipText = () => "Playtest: jump straight into the Ward duel or the rhythm party dance.",
            });
        }

        private static void Panel(GodTool t, Rect a)
        {
            var b = WorldBootstrap.Instance;
            float x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), y = a.y + UiStyle.Px(10), bh = UiStyle.Px(40), gap = UiStyle.Px(6);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(56)), "Starts the encounter at once, as if you had just tapped them and picked the first reply.", UiStyle.Small);
            y += UiStyle.Px(60);
            bool running = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            if (GUI.Button(new Rect(x, y, w, bh), "Ward duel with Mira", UiStyle.Button) && b != null)
                Start(EncounterDirectory.WardMira, Npc.Find(NpcIds.Mira), "Ward duel");
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, w, bh), "Rhythm party with Sol", UiStyle.Button) && b != null)
                Start(EncounterDirectory.RhythmParty, Npc.Find(NpcIds.Host), "Rhythm party");
            y += bh + gap;
            if (running) GUI.Label(new Rect(x, y, w, UiStyle.Px(30)), "An encounter is already running.", UiStyle.Small);
            else if (s_last != null) GUI.Label(new Rect(x, y, w, UiStyle.Px(30)), s_last, UiStyle.Small);
        }

        private static void Start(string encounterId, Npc npc, string label)
        {
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) { s_last = label + ": an encounter is already running."; return; }
            if (npc == null) { s_last = label + ": the NPC is not in the scene."; return; }
            ViewSwitcher.Instance?.SetView(ViewMode.FirstPerson);
            s_last = EncounterDirectory.TryStart(encounterId, npc) ? label + " started." : label + " refused to start.";
        }
    }
}
