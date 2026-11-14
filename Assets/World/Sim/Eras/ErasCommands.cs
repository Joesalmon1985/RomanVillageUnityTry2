namespace World.Sim.Eras
{
    /// <summary>
    /// Steps the island to a ready chapter, atomically in one tick (AG-5, 4.6). Refused with a reason when the chapter is
    /// unknown or not ready; a refused advance changes nothing. PlayerId = 0 (the player's people decide the story).
    /// </summary>
    public sealed class AdvanceChapter : Command { public string To; }

    /// <summary>Refusal codes of the eras module and their exact sentences (PRD_AGES A.2).</summary>
    public static class ErasRefusals
    {
        public const string ChapterNotReady = "chapter_not_ready";
        public const string ChapterUnknown = "chapter_unknown";
        public const string GameOver = "game_over";

        public static string Sentence(string code)
        {
            switch (code)
            {
                case ChapterNotReady: return "That time has not come yet.";
                case ChapterUnknown: return "There is no such chapter.";
                case GameOver: return "The game is over.";
                default: return code;
            }
        }

        public static CommandResult Reject(string code) => CommandResult.Reject(code, Sentence(code));
    }

    /// <summary>How an overrun in the campaign ended (4.3): a non-home camp captured, or the home camp looted.</summary>
    public enum CampOverrunKind { CampLost = 0, Looted = 1 }

    // Events (PRD_AGES A.2; Tick is stamped).

    /// <summary>A chapter may now be advanced to (AdvanceChapter will be accepted).</summary>
    public sealed class ChapterReady : SimEvent { public string Chapter; }

    /// <summary>The island stepped from one chapter to the next (the game layer auto-saves on this event).</summary>
    public sealed class ChapterAdvanced : SimEvent { public string From; public string To; }

    /// <summary>A faction reached the handover milestone (4.2): its first workshop-made good reached a construction. Once per faction.</summary>
    public sealed class PrinceHandover : SimEvent { public int Faction; }

    /// <summary>Crystals PRD N-1: AgesApi.CampaignMayStart turned true (Historic era and the player's handover done). Once per game.</summary>
    public sealed class CampaignMayStart : SimEvent { }

    /// <summary>
    /// Crystals PRD N-2 (4.3): in the campaign an overrun completed. CampLost: SettlementId changed owner. Looted: the
    /// defender's home (SettlementId) lost half its stock and its buildings stop for a while. The game does not end.
    /// </summary>
    public sealed class CampOverrun : SimEvent
    {
        public int Attacker;
        public int Defender;
        public int SettlementId;
        public CampOverrunKind Kind;
    }
}
