using System.Collections.Generic;
using UnityEngine;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// The expedition in play (PRD_CRYSTALS appendix B, I6): Mira leading, the companions walking in file, what was
    /// said. <b>I0 shell</b> (14 Nov 2026): the names the save (I8), the duel (I7) and tests code against; nothing
    /// creates it yet, so <see cref="Instance"/> is null. Owner: I6.
    /// </summary>
    public sealed class ExpeditionDirector : MonoBehaviour
    {
        /// <summary>One line said in the expedition (speech bubbles), for tests and the journal.</summary>
        public struct Line { public long Tick; public string Speaker; public string Text; }

        public static ExpeditionDirector Instance { get; private set; }

        /// <summary>Mira (the camp's Mira, taken over while the campaign runs).</summary>
        public Npc Mira { get; private set; }
        /// <summary>The six companions in roster order (present or away).</summary>
        public IReadOnlyList<Npc> Companions => _companions;
        /// <summary>The party's mode as last shown (from <c>CampaignApi.Party</c>).</summary>
        public PartyMode Phase { get; private set; }
        /// <summary>Every line said, in order.</summary>
        public IReadOnlyList<Line> Said => _said;

        private readonly List<Npc> _companions = new List<Npc>();
        private readonly List<Line> _said = new List<Line>();

        /// <summary>The save's <c>scene.expedition</c> step (I8 extra). I0 STUB.</summary>
        public int SceneStep { get; private set; }
        /// <summary>Restores the saved scene step after a load. I0 STUB.</summary>
        public void RestoreScene(int step) { SceneStep = step; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
