using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// What a started encounter is asked to be (PRD_CRYSTALS appendix B). Passed as
    /// <see cref="EncounterContext.Request"/> by <see cref="EncounterDirectory.TryStart(string, Npc, object)"/>; a null
    /// request means the encounter behaves exactly as before (the practice duel with Mira, the dance).
    /// Created by crystals I0 (Unity half); filled by I7 (<see cref="CampaignDuel"/>) and read by
    /// <see cref="WardDuelEncounter"/> from I7 on. Additive changes only.
    /// </summary>
    public sealed class WardDuelRequest
    {
        /// <summary>Lethal (a keeper, a companion's life), Friendly (no death), Practice (Mira's duel).</summary>
        public EncounterKind Mode = EncounterKind.Practice;
        /// <summary>The sim's attempt id (<c>EncounterCommitted.AttemptId</c>); -1 outside the campaign.</summary>
        public int AttemptId = -1;
        /// <summary>The campaign's encounter id (<c>EncounterDef.Id</c>); null outside the campaign.</summary>
        public string EncounterId;
        /// <summary>Ward opponent profile id (<c>ward_opponents.json</c>, e.g. "keeper_earth"); null = practice Mira.</summary>
        public string ProfileId;
        /// <summary>Duel seed (<c>EncounterCommitted.Seed</c>).</summary>
        public ulong Seed;
        /// <summary>Display name of the volunteering companion, or null.</summary>
        public string VolunteerName;
        /// <summary>Display name of the opponent ("Mira" in practice).</summary>
        public string OpponentName;
        /// <summary>Who stands where; null = the encounter's own staging.</summary>
        public DuelStageSpec Stage;

        public bool IsCampaign => AttemptId >= 0;

        /// <summary>A request built from the sim's commit event (mode and names are the caller's: I7 fills them from the definition).</summary>
        public static WardDuelRequest From(EncounterCommitted ev, EncounterKind mode)
        {
            return new WardDuelRequest
            {
                Mode = mode,
                AttemptId = ev != null ? ev.AttemptId : -1,
                EncounterId = ev?.EncounterId,
                ProfileId = ev?.Profile,
                Seed = ev != null ? ev.Seed : 0UL,
            };
        }

        public override string ToString() =>
            "WardDuelRequest(" + Mode + ", attempt " + AttemptId + ", " + (EncounterId ?? "-") + ", profile " + (ProfileId ?? "-") + ", seed " + Seed + ")";
    }
}
