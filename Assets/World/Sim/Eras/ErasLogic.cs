using System;

namespace World.Sim.Eras
{
    /// <summary>
    /// Chapter readiness and the one-shot step (PRD_AGES 4.6). Sim code only. H1-0 FIRST VERSION: readiness, the era and
    /// chapter switch, the history record and the events are real; the content of each step (the smith's chest at the
    /// Historic step, the transfer tables of the time jumps) is an H1-0 STUB in <see cref="ApplyStepContent"/>.
    /// </summary>
    internal static class ErasLogic
    {
        /// <summary>
        /// Marks a chapter as ready (raises ChapterReady once). Returns false for an unknown chapter, the current one, or
        /// one already ready. H1-1 calls it at the player's handover (ch.historic); the crystal ending raises ch.modern.harbour.
        /// </summary>
        public static bool MarkReady(WorldState s, string chapter, EventBuffer ev)
        {
            var e = s?.Eras;
            if (e == null || !ChapterIds.IsKnown(chapter)) return false;
            if (string.Equals(e.Chapter, chapter, StringComparison.Ordinal)) return false;
            if (e.ReadyChapters.Contains(chapter)) return false;
            e.ReadyChapters.Add(chapter);
            e.ReadyChapters.Sort(string.CompareOrdinal);
            ev?.Add(new ChapterReady { Chapter = chapter });
            return true;
        }

        public static CommandResult CheckAdvance(WorldState s, string to)
        {
            if (s?.Eras == null || !ChapterIds.IsKnown(to)) return ErasRefusals.Reject(ErasRefusals.ChapterUnknown);
            if (s.Factions != null && s.Factions.GameOver) return ErasRefusals.Reject(ErasRefusals.GameOver);
            if (!s.Eras.ReadyChapters.Contains(to)) return ErasRefusals.Reject(ErasRefusals.ChapterNotReady);
            return CommandResult.Accept();
        }

        /// <summary>Applies a validated step in one tick: content, era and chapter, history, event (4.6 order).</summary>
        public static void Advance(WorldState s, string to, EventBuffer ev)
        {
            var e = s.Eras;
            string from = e.Chapter;
            ApplyStepContent(s, from, to, ev);
            e.Current = ChapterIds.EraOf(to);
            e.Chapter = to;
            e.ChapterStartTick = s.Tick;
            e.ReadyChapters.Remove(to);
            if (s.History == null) s.History = new HistoryState();
            s.History.Chapters.Add(new ChapterRecord { From = from, To = to, Tick = s.Tick });
            ev?.Add(new ChapterAdvanced { From = from, To = to });
        }

        /// <summary>
        /// H1-0 STUB: what each step carries and grants. Prehistoric to Historic: the smith's chest (H1-1; only wares that
        /// exist in <see cref="Wares"/> can be delivered, so nothing in H1). Historic to Modern: transfer table 4.4 (M1-4).
        /// Modern to Future: transfer table 4.5 (F1-2). Must stay deterministic and never throw.
        /// </summary>
        internal static void ApplyStepContent(WorldState s, string from, string to, EventBuffer ev)
        {
        }
    }

    /// <summary>Eras slot (50): raises CampaignMayStart once, the first tick it holds (crystals PRD N-1).</summary>
    internal sealed class ErasSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var e = s.Eras;
            if (e == null || e.CampaignMayStartTick >= 0) return;
            if (!AgesApi.CampaignMayStart(s)) return;
            e.CampaignMayStartTick = s.Tick;
            ev.Add(new CampaignMayStart());
        }
    }

    internal sealed class AdvanceChapterHandler : CommandHandler<AdvanceChapter>
    {
        protected override CommandResult Validate(WorldState s, AdvanceChapter c) => ErasLogic.CheckAdvance(s, c.To);

        protected override CommandResult Apply(WorldState s, AdvanceChapter c, EventBuffer ev)
        {
            ErasLogic.Advance(s, c.To, ev);
            return CommandResult.Accept();
        }
    }

    /// <summary>Entry point of the Eras module (same shape as the refactor's modules, REFACTOR_SPEC A.4).</summary>
    public static class ErasModule
    {
        /// <summary>A new game: Prehistoric skirmish at ch.prehistoric, empty history, the starting landmarks.</summary>
        public static void InitState(WorldState s)
        {
            s.Eras = new ErasState();
            s.History = new HistoryState();
            LandmarkLogic.Seed(s);
        }

        /// <summary>
        /// Fills what an older save lacks (save migration v4 to v5): a Prehistoric skirmish, empty history, the starting
        /// landmarks. Keeps anything present.
        /// </summary>
        public static void EnsureState(WorldState s)
        {
            if (s == null) return;
            if (s.Eras == null) s.Eras = new ErasState();
            if (s.History == null) s.History = new HistoryState();
            LandmarkLogic.Seed(s);
        }

        /// <summary>
        /// Switches the island to campaign mode (4.3 rules on: an overrun never ends the game, no time limit). Called by
        /// the crystals' StartCampaign handler only (sim code, inside a command's Apply). The PRD's "Eras.EnterCampaign":
        /// a class named Eras cannot sit beside the namespace World.Sim.Eras, so it lives here (PRD_AGES A.9).
        /// </summary>
        public static void EnterCampaign(WorldState s)
        {
            if (s == null) return;
            if (s.Eras == null) s.Eras = new ErasState();
            s.Eras.Mode = PlayMode.Campaign;
        }

        public static void Register(WorldSim sim)
        {
            sim.AddSystem(SystemOrder.Eras, new ErasSystem());
            sim.RegisterHandler(new AdvanceChapterHandler());
        }
    }
}
