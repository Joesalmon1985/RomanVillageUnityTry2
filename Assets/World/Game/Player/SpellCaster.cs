using System;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// Ground press to <c>CastSpell</c> (REFACTOR_SPEC 2.C.2, appendix B): finds the open proposal of the player's people
    /// whose press area holds the point (<c>FactionApi.ProposalAt</c>), checks the player is within
    /// <c>FactionApi.CastRangeMetres</c> of its target, and submits the command. Owner: package P.
    /// F-0 stub: refuses every press.
    /// </summary>
    public static class SpellCaster
    {
        /// <summary>Raised on every attempt: proposal id (-1 if none) and the result.</summary>
        public static event Action<int, CommandResult> CastAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => CastAttempted = null;

        public static CommandResult TryCastAt(Vector3 ground)
        {
            var r = CommandResult.Reject("not_implemented", "Casting arrives with package P.");
            CastAttempted?.Invoke(-1, r);
            return r;
        }
    }
}
