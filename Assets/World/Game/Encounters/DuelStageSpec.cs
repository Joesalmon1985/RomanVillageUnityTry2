using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Where the people of a Ward duel stand and who they are (PRD_CRYSTALS appendix B, N-9; PRD_CHARACTERS 7.1).
    /// Actor ids are <see cref="NpcIds"/> or cast ids; a null caster is the anonymous player wizard, a null volunteer
    /// means nobody stands in. Positions are world points on the ground. Created by crystals I0 (Unity half); I7 and,
    /// from tag cry-I7, characters WP-9 read it. Additive changes only.
    /// </summary>
    public sealed class DuelStageSpec
    {
        /// <summary>Who casts for the player (Mira in the campaign); null = the anonymous player wizard.</summary>
        public string CasterActorId;
        /// <summary>The opponent (Mira in the practice duel, a keeper, the champion).</summary>
        public string OpponentActorId;
        /// <summary>The companion who volunteered to stand in the duel (lethal duels); null = none.</summary>
        public string VolunteerActorId;

        public Vector3 CasterPos;
        public Vector3 OpponentPos;
        public Vector3 VolunteerPos;

        public bool HasCaster => !string.IsNullOrEmpty(CasterActorId);
        public bool HasVolunteer => !string.IsNullOrEmpty(VolunteerActorId);

        public override string ToString() =>
            "stage caster " + (CasterActorId ?? "player") + " at " + CasterPos + ", opponent " + (OpponentActorId ?? "?") + " at " + OpponentPos
            + (HasVolunteer ? ", volunteer " + VolunteerActorId + " at " + VolunteerPos : "");
    }
}
