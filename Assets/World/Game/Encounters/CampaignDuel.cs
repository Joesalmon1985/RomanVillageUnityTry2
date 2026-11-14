using System;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// The crystal campaign's duel glue (PRD_CRYSTALS appendix B, package I7): turns the sim's
    /// <see cref="EncounterCommitted"/> into a <see cref="WardDuelRequest"/>, starts "ward_campaign", and sends exactly
    /// one <c>ResolveEncounter</c> when it ends. <b>I0 shell</b> (Unity half of crystals I0, 14 Nov 2026): the encounter
    /// id is registered by <see cref="EncounterInstaller"/> through <see cref="Create"/>, which today is the practice
    /// duel; <see cref="Start"/> does nothing yet. Owner: I7 (this file is new and wholly I7's).
    /// </summary>
    public static class CampaignDuel
    {
        /// <summary>Raised once per attempt with the result the duel produced (I7).</summary>
        public static event Action<int, DuelResult> Resolved;

        /// <summary>The factory registered under <see cref="EncounterDirectory.WardCampaign"/> (I7 may replace what it builds).</summary>
        public static IEncounter Create() => new WardDuelEncounter();

        /// <summary>Starts the duel the sim committed. I0 STUB: returns false and starts nothing (I7).</summary>
        public static bool Start(EncounterCommitted ev) => false;

        /// <summary>For I7: raises <see cref="Resolved"/>.</summary>
        internal static void RaiseResolved(int attemptId, DuelResult result) => Resolved?.Invoke(attemptId, result);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Resolved = null;
    }
}
