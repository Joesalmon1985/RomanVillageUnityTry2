using World.Sim.Eras;
using World.Sim.Table;

namespace World.Sim.Historic
{
    /// <summary>
    /// The Historic age's system (G1-8 sim part, G4-1, G4-2). Registered for every god board sim by
    /// <see cref="GodBoard.GodBoardModule.Register"/>; it does nothing until <see cref="WorldState.Historic"/> exists.
    /// </summary>
    public static class HistoricModule
    {
        /// <summary>Tick slot: after the table (its age end at the table's slot) and before the war (Combat + 10).</summary>
        public const int Order = SystemOrder.Production + 50;

        public static void Register(WorldSim sim)
        {
            sim.AddSystem(Order, new HistoricSystem());
        }

        /// <summary>
        /// HI-1, HI-2, HI-8 (G1-8 sim part): the age has turned on a god board. The plague has already ended (the caller
        /// ends it first); the Historic chapter is marked ready and the era steps to it at once (ChapterReady, then
        /// ChapterAdvanced), the table stays closed for good, the temples fall, castles and resources begin. The war keeps its
        /// rules (HI-7). Idempotent; does nothing for a fallen island or a table that has not ended.
        /// </summary>
        public static void Switch(WorldState s, int winner, EventBuffer ev)
        {
            if (s?.Table == null || s.Historic != null) return;
            if (s.Table.Phase != TablePhase.Ended || s.Table.IslandFell) return;
            ErasLogic.MarkReady(s, ChapterIds.Historic, ev);
            if (s.Eras != null && s.Eras.Current == Era.Prehistoric && ErasLogic.CheckAdvance(s, ChapterIds.Historic).Ok)
                ErasLogic.Advance(s, ChapterIds.Historic, ev);
            HistoricRules.Begin(s, winner, ev);
        }

        private sealed class HistoricSystem : ISimSystem
        {
            public void Tick(WorldState s, EventBuffer ev) => HistoricRules.Tick(s, ev);
        }
    }
}
