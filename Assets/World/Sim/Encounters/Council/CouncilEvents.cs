using System.Collections.Generic;

namespace World.Sim.Encounters.Council
{
    /// <summary>Base of every council event. Settlement is stamped by the module (-1 when the engine runs standalone).</summary>
    public abstract class CouncilEvent : SimEvent
    {
        public int Settlement = -1;
        /// <summary>The candidate seat the event is about (-1 when none).</summary>
        public int Seat = -1;
    }

    /// <summary>A push was made. Carries no values: dice are never exposed.</summary>
    public sealed class CouncilPushMade : CouncilEvent { public int PushId; public bool FromPetition; }

    /// <summary>The options of a push (empty = no legal move).</summary>
    public sealed class CouncilOptionsOffered : CouncilEvent { public int PushId; public List<Tray> Trays = new List<Tray>(); }

    public sealed class AdvanceStep
    {
        public int Track;
        public int From;
        public int To;
        public bool NewCatspaw;
        public bool ReachedTop;
    }

    /// <summary>One tray or carving was committed; Steps lists the real catspaw movements.</summary>
    public sealed class CouncilAdvanceMade : CouncilEvent
    {
        public int PushId;
        public int Tray;
        public int Socket;
        public List<AdvanceStep> Steps = new List<AdvanceStep>();
    }

    public sealed class BankedPlace { public int Track; public int Place; }

    public sealed class CouncilBanked : CouncilEvent { public List<BankedPlace> Places = new List<BankedPlace>(); }

    /// <summary>The candidate's unbanked progress was dropped and the turn passes (the TurnPassed event follows).</summary>
    public sealed class CouncilBust : CouncilEvent { public List<BankedPlace> Lost = new List<BankedPlace>(); }

    /// <summary>A push had no legal move and the petition is in hand: waiting for spend or accept.</summary>
    public sealed class CouncilBustPending : CouncilEvent { public int PushId; }

    public sealed class CouncilPetitionSpent : CouncilEvent { }

    public sealed class CouncilTrackClaimed : CouncilEvent
    {
        public int Track;
        /// <summary>Seats whose banked marker on the track was removed.</summary>
        public List<int> RemovedFrom = new List<int>();
    }

    public sealed class CouncilTurnPassed : CouncilEvent { public int From; public int To; public int Sitting; }

    public sealed class CouncilElectionWon : CouncilEvent { }

    public sealed class CouncilListeningStarted : CouncilEvent { public int Burst; }

    public sealed class CouncilListeningAnswered : CouncilEvent
    {
        public int Index;
        public bool Correct;
        public int ResponseMs;
        public int ReferenceTag;
        public int LineTag;
    }

    public sealed class CouncilListeningFinished : CouncilEvent { public ListeningResult Result; }

    public sealed class CouncilListeningLeft : CouncilEvent { public bool Abandoned; }

    public sealed class CouncilPetitionEarned : CouncilEvent { }

    // Lifecycle (module level).
    public sealed class CouncilOffered : CouncilEvent { public int AskerSeat; }
    public sealed class CouncilStarted : CouncilEvent { public bool PlayerHelps; public int Starter; }
    public sealed class CouncilLeft : CouncilEvent { }
    public sealed class CouncilResumed : CouncilEvent { }
    /// <summary>The office changed hands (once per settlement).</summary>
    public sealed class CouncilOfficeHandedOver : CouncilEvent { public string WinnerName; public string Office; }
}
