using System;
using System.Collections.Generic;

namespace World.Sim.Eras
{
    /// <summary>
    /// Read helpers for the ages (PRD_AGES A.2; crystals PRD N-1; characters PRD's age switch hook). Static, null-safe,
    /// never mutate. A state without Eras reads as a Prehistoric skirmish.
    /// </summary>
    public static class AgesApi
    {
        public static Era Current(WorldState s) => s?.Eras?.Current ?? Era.Prehistoric;

        public static string Chapter(WorldState s) => s?.Eras?.Chapter ?? ChapterIds.Prehistoric;

        public static PlayMode Mode(WorldState s) => s?.Eras?.Mode ?? PlayMode.Skirmish;

        /// <summary>
        /// True when AdvanceChapter to this chapter would be accepted now (ready, built in this build, game not over). A
        /// chapter can be in <see cref="ReadyChapters"/> before it is built (ch.modern.harbour after the crystal ending).
        /// </summary>
        public static bool IsReady(WorldState s, string chapter) =>
            s?.Eras != null && chapter != null && s.Eras.ReadyChapters.Contains(chapter) && ChapterIds.IsBuilt(chapter)
            && !(s.Factions?.GameOver ?? false);

        /// <summary>Chapters ready to advance to, sorted ordinal (empty when none).</summary>
        public static IReadOnlyList<string> ReadyChapters(WorldState s) =>
            (IReadOnlyList<string>)s?.Eras?.ReadyChapters ?? Array.Empty<string>();

        public static Landmark Landmark(WorldState s, string id) => LandmarkApi.Get(s, id);

        /// <summary>Tick of the faction's handover milestone (4.2), -1 if not yet (or no such faction).</summary>
        public static long HandoverTick(WorldState s, int faction)
        {
            var list = s?.Factions?.Factions;
            if (list == null || faction < 0 || faction >= list.Count) return -1;
            return list[faction].HandoverTick;
        }

        /// <summary>
        /// Crystals PRD N-1: the island is in the Historic age (or later) and the player's people have handed over. On the
        /// god board (PRD_GODBOARD CC-1) the gate is gone: Mira may set out from the first minute.
        /// </summary>
        public static bool CampaignMayStart(WorldState s) =>
            Campaign.CampaignRuleset.GodBoard(s) || (Current(s) >= Era.Historic && HandoverTick(s, PlayerIds.Human) >= 0);

        /// <summary>The campaign rules of 4.3 apply (overrun never ends the game, no time limit): Mode is Campaign.</summary>
        public static bool IsCampaign(WorldState s) => Mode(s) == PlayMode.Campaign;

        /// <summary>True from that era on (e.g. AtLeast(s, Era.Historic) for grade 1 upgrades).</summary>
        public static bool AtLeast(WorldState s, Era era) => Current(s) >= era;

        /// <summary>"the Prehistoric age", "the Historic age", ... as used in refusal sentences ("That needs the Historic age.").</summary>
        public static string EraName(Era era)
        {
            switch (era)
            {
                case Era.Historic: return "Historic";
                case Era.Modern: return "Modern";
                case Era.Future: return "Future";
                default: return "Prehistoric";
            }
        }
    }
}
