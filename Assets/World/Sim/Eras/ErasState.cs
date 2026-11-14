using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The age and chapter of the island (PRD_AGES 4, A.2). Null only in a state built by hand; read it through AgesApi.</summary>
        public Eras.ErasState Eras;

        /// <summary>What became history: chapter steps, the named fallen, notes and outcomes (PRD_AGES 4.4, A.2).</summary>
        public Eras.HistoryState History;
    }
}

namespace World.Sim.Eras
{
    /// <summary>The four eras of the one island (AG-1). Separate from the archived strategy Age counter (AG-4).</summary>
    public enum Era { Prehistoric = 0, Historic = 1, Modern = 2, Future = 3 }

    /// <summary>How this game was started. Skirmish: the 20-minute game, overrun and time are fatal. Campaign: nothing ends the game (4.3).</summary>
    public enum PlayMode { Skirmish = 0, Campaign = 1, DevStart = 2 }

    /// <summary>Chapter ids (AG-4). Stable strings; never reuse an archived id.</summary>
    public static class ChapterIds
    {
        public const string Prehistoric = "ch.prehistoric";
        public const string Historic = "ch.historic";
        public const string ModernHarbour = "ch.modern.harbour";
        public const string FutureCore = "ch.future.core";

        /// <summary>Every chapter in story order.</summary>
        public static readonly string[] All = { Prehistoric, Historic, ModernHarbour, FutureCore };

        /// <summary>True for a known chapter id.</summary>
        public static bool IsKnown(string id) => id != null && Array.IndexOf(All, id) >= 0;

        /// <summary>
        /// sim-H1-cry-S1 addition: chapters whose step content exists in this build, so AdvanceChapter may enter them
        /// (Prehistoric and Historic now; M1-4 adds ch.modern.harbour, F1-2 ch.future.core). A chapter may be marked ready
        /// before it is built (the crystal ending readies ch.modern.harbour); advancing to it is refused with
        /// chapter_not_built until then.
        /// </summary>
        public static bool IsBuilt(string id) => id == Prehistoric || id == Historic;

        /// <summary>The era a chapter belongs to; Prehistoric for an unknown id.</summary>
        public static Era EraOf(string id)
        {
            switch (id)
            {
                case Historic: return Era.Historic;
                case ModernHarbour: return Era.Modern;
                case FutureCore: return Era.Future;
                default: return Era.Prehistoric;
            }
        }
    }

    /// <summary>Numbers of the eras module (saved with the state).</summary>
    public sealed class ErasTuning
    {
        /// <summary>"Not yet" to the prince's invitation: asked again after this long (4.2; 3 minutes).</summary>
        public int InviteRepeatTicks = 1800;

        /// <summary>
        /// The smith's chest each faction's home storehouse receives at the Historic step (5.5): ware ids (future ids:
        /// 23 Pick, 22 Axe, 24 Hammer, 25 Saw, 26 Sickle, 27 Knife, 28 Tongs, 17 Iron) and counts. A unit whose ware id is
        /// not yet in <see cref="Wares"/> is not delivered (H1 has only wares 0-9; H2-1 makes the chest real).
        /// </summary>
        public int[] SmithsChestWares = { 23, 22, 24, 25, 26, 27, 28, 17 };
        public int[] SmithsChestCounts = { 2, 1, 1, 1, 1, 1, 1, 4 };
    }

    /// <summary>WorldState.Eras (PRD_AGES A.2).</summary>
    public sealed class ErasState
    {
        public ErasTuning Tuning = new ErasTuning();
        public Era Current = Era.Prehistoric;
        public PlayMode Mode = PlayMode.Skirmish;
        public string Chapter = ChapterIds.Prehistoric;
        /// <summary>Chapters that may be advanced to now (raised by ChapterReady), sorted ordinal.</summary>
        public List<string> ReadyChapters = new List<string>();
        public long ChapterStartTick;
        /// <summary>The dev start id (ScenarioStarts), or null.</summary>
        public string StartId;
        /// <summary>Tick the CampaignMayStart event fired (once), -1 before.</summary>
        public long CampaignMayStartTick = -1;
    }

    /// <summary>One chapter step (4.6): From, To, Tick and outcome strings.</summary>
    public sealed class ChapterRecord
    {
        public string From;
        public string To;
        public long Tick;
        /// <summary>Outcome keys and values of the step (for example "harbour.route" -> "A"), sorted ordinal.</summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Reuse)] // keeps the ordinal comparer on load
        public SortedDictionary<string, string> Outcomes = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>A named casualty (4.4: the memorial). Place is where they fell (crystals PRD: "the fallen and where").</summary>
    public sealed class CasualtyRecord
    {
        public string Id;
        public string Name;
        public string Chapter;
        public string Cause;
        public string Place;
        public long Tick;
    }

    /// <summary>
    /// WorldState.History (PRD_AGES A.2). PARTIAL: the crystal campaign package adds its whole CampaignRecord here
    /// from its own folder (crystals PRD N-6), so this file never names its type.
    /// </summary>
    public sealed partial class HistoryState
    {
        public List<ChapterRecord> Chapters = new List<ChapterRecord>();
        public List<CasualtyRecord> Casualties = new List<CasualtyRecord>();
        /// <summary>Plain sentences, e.g. "the old guard: 14 soldiers, best level 6".</summary>
        public List<string> Notes = new List<string>();
        /// <summary>"harbour.route" -> "A", ...</summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public SortedDictionary<string, string> Outcomes = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }
}
