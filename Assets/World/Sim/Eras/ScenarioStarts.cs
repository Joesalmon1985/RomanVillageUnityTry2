using System.Collections.Generic;

namespace World.Sim.Eras
{
    /// <summary>
    /// Standalone starts (PRD_AGES 4.7, AG-7): a full sim for a chapter without playing earlier ones. Ordinary new games
    /// otherwise; reachable from F1 and the command line. Unknown or not yet built ids return null (never throw). Mode is
    /// DevStart; a campaign started on it switches to Campaign through ErasModule.EnterCampaign. Options are per start
    /// (M1 adds "modernOutcome" for dev.future.core); H1 reads none.
    ///
    /// H1-0 STUB for dev.historic: a new game switched to Historic at tick 0 with both factions handed over (HandoverTick 0)
    /// and the player's people autonomous (Auto). The authored contents of 4.7 (home city, 2 camps, workshops, improved
    /// collectors, smith's chest, the rival similar) arrive with H1-1 (owner of Sim/Eras after H1-0). M1-4 and F1-2 add
    /// their starts here (additively).
    /// </summary>
    public static class ScenarioStarts
    {
        public const string DevHistoric = "dev.historic";
        public const string DevModernHarbour = "dev.modern.harbour";
        public const string DevModernSkirmish = "dev.modern.skirmish";
        public const string DevFutureCore = "dev.future.core";

        private static readonly string[] Available = { DevHistoric };

        /// <summary>Start ids that Create can build now (grows with M1 and F1).</summary>
        public static IReadOnlyList<string> Ids => Available;

        public static WorldSim Create(string startId, ulong seed, IDictionary<string, string> options = null)
        {
            switch (startId)
            {
                case DevHistoric: return DevHistoricStart(seed);
                default: return null;
            }
        }

        private static WorldSim DevHistoricStart(ulong seed)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            s.Eras.Mode = PlayMode.DevStart;
            s.Eras.StartId = DevHistoric;
            s.Eras.Current = Era.Historic;
            s.Eras.Chapter = ChapterIds.Historic;
            s.Eras.ChapterStartTick = 0;
            s.History.Chapters.Add(new ChapterRecord { From = ChapterIds.Prehistoric, To = ChapterIds.Historic, Tick = 0 });
            foreach (var fa in s.Factions.Factions)
            {
                fa.HandoverTick = 0;
                fa.Auto = true;
            }
            return sim;
        }
    }
}
