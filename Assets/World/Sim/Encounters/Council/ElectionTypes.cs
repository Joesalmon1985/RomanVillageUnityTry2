using System.Collections.Generic;
using Newtonsoft.Json;

namespace World.Sim.Encounters.Council
{
    /// <summary>Fixed numbers of the election (brief section 3).</summary>
    public static class CouncilRules
    {
        public const int Seats = 4;
        public const int FirstTrack = 2;
        public const int LastTrack = 12;
        public const int TrackCount = 11;
        public const int Catspaws = 3;
        public const int ClaimsToWin = 3;

        /// <summary>Places on tracks 2..12.</summary>
        public static readonly int[] TrackLengths = { 3, 5, 7, 9, 11, 13, 11, 9, 7, 5, 3 };

        public static bool IsTrack(int track) => track >= FirstTrack && track <= LastTrack;
        public static int Length(int track) => TrackLengths[track - FirstTrack];
        public static int Index(int track) => track - FirstTrack;
    }

    public enum ElectionPhase
    {
        /// <summary>The active candidate may push (press on) or, after an advance, bank.</summary>
        Ready = 0,
        /// <summary>A push is pending: its trays await one choice.</summary>
        Choosing = 1,
        /// <summary>A push had no legal move and the spare petition is in hand: spend it or accept the setback.</summary>
        BustPending = 2,
        Complete = 3,
    }

    /// <summary>Both: tapping the tray commits every carving in it. Either: each carving is a separate choice (one is taken, the other is left behind).</summary>
    public enum TrayMode { Both = 0, Either = 1 }

    /// <summary>One carving (one advance on a dispute track). Track 0 means an empty socket.</summary>
    public sealed class TraySocket
    {
        public int Track;
        public int Steps;
        [JsonIgnore] public bool IsEmpty => Track == 0;
    }

    /// <summary>One option made from a single push: two sockets. The presentation never sees dice, only this.</summary>
    public sealed class Tray
    {
        /// <summary>The two pair sums of the pairing this tray came from (SumA is the lower).</summary>
        public int SumA, SumB;
        public TrayMode Mode;
        public TraySocket[] Sockets = new TraySocket[2];
    }

    /// <summary>Saved listening-burst rules (adjustable; copied into each election so a save replays identically).</summary>
    public sealed class ListeningTuning
    {
        public int Responses = 5;
        /// <summary>Effective accuracy (percent, speed bonus included) needed for the petition.</summary>
        public int AccuracyThresholdPercent = 85;
        /// <summary>Accuracy points added per correct answer at full speed. 0 turns the speed bonus off.</summary>
        public int SpeedBonusPercent = 5;
        public int FastMs = 1500;
        public int SlowMs = 4000;
        /// <summary>Bursts that may be started per election while no petition is earned.</summary>
        public int MaxBursts = 2;

        public ListeningTuning Copy() => (ListeningTuning)MemberwiseClone();
    }

    /// <summary>Everything about one election. Plain data: serialises with the world and resumes exactly.</summary>
    public sealed class ElectionState
    {
        public ulong Seed;
        /// <summary>The election's own stream: starter and every push. Nothing else draws from it.</summary>
        public Rng Rolls = new Rng();
        /// <summary>Stream for AI decisions (noise, impulse). Separate so it never shifts a roll.</summary>
        public Rng AiRng = new Rng();
        /// <summary>Stream that seeds listening bursts. Separate so listening never shifts a roll.</summary>
        public Rng ListenRng = new Rng();

        public int Starter;
        public int Active;
        /// <summary>The seat the player advises, or -1 when all four are AI.</summary>
        public int PlayerSeat = -1;
        /// <summary>Personality id by seat.</summary>
        public string[] Personality = new string[CouncilRules.Seats];

        /// <summary>Winner seat by track (index track-2), -1 = open.</summary>
        public int[] Claimed = NewClaimed();
        /// <summary>Banked place by seat * 11 + (track-2); 0 = none.</summary>
        public int[] Banked = new int[CouncilRules.Seats * CouncilRules.TrackCount];
        /// <summary>The active candidate's catspaw places by track-2 (0 = no catspaw there).</summary>
        public int[] Temp = new int[CouncilRules.TrackCount];

        public ElectionPhase Phase = ElectionPhase.Ready;
        /// <summary>Trays of the pending push (empty unless Phase is Choosing). Dice are never stored.</summary>
        public List<Tray> Pending = new List<Tray>();
        /// <summary>Counts pushes; a choice must quote the pending push's id.</summary>
        public int PushId;
        /// <summary>Counts accepted actions; an action may quote it to be refused when stale.</summary>
        public int ActionSeq;
        public bool HasAdvanced;
        public int SittingPushes, SittingAdvances;

        public int Sittings = 1, Pushes, Advances, Banks, Busts;
        public int Winner = -1;

        public bool PetitionEarned;
        public bool PetitionSpent;
        [JsonIgnore] public bool PetitionAvailable => PetitionEarned && !PetitionSpent;

        public ListeningTuning ListeningRules = new ListeningTuning();
        /// <summary>The burst in progress or the last finished one (null before any).</summary>
        public ListeningState Listening;
        public int ListenBursts, ListenSkips, ListenAbandons;

        public static int[] NewClaimed()
        {
            var c = new int[CouncilRules.TrackCount];
            for (int i = 0; i < c.Length; i++) c[i] = -1;
            return c;
        }

        public int BankedAt(int seat, int track) => Banked[seat * CouncilRules.TrackCount + (track - 2)];
        public int ClaimsOf(int seat)
        {
            int n = 0;
            foreach (int c in Claimed) if (c == seat) n++;
            return n;
        }
        [JsonIgnore] public int CatspawsInPlay { get { int n = 0; foreach (int t in Temp) if (t > 0) n++; return n; } }
        [JsonIgnore] public int CatspawsLeft => CouncilRules.Catspaws - CatspawsInPlay;
    }

    public enum ActionKind { Push = 0, Choose = 1, Bank = 2, AcceptBust = 3, SpendPetition = 4 }

    /// <summary>One election action. Players and AI both submit these and nothing else.</summary>
    public sealed class ElectionAction
    {
        public ActionKind Kind;
        public int Seat;
        /// <summary>Choose: the pending push's id.</summary>
        public int PushId;
        public int Tray;
        /// <summary>Choose on an Either tray: the socket tapped (0 or 1). Otherwise -1.</summary>
        public int Socket = -1;
        /// <summary>Optional: refuse when the election has moved on (ActionSeq differs). -1 = do not check.</summary>
        public int ExpectedSeq = -1;

        public static ElectionAction Push(int seat) => new ElectionAction { Kind = ActionKind.Push, Seat = seat };
        public static ElectionAction Choose(int seat, int pushId, int tray, int socket = -1) =>
            new ElectionAction { Kind = ActionKind.Choose, Seat = seat, PushId = pushId, Tray = tray, Socket = socket };
        public static ElectionAction Bank(int seat) => new ElectionAction { Kind = ActionKind.Bank, Seat = seat };
        public static ElectionAction AcceptBust(int seat) => new ElectionAction { Kind = ActionKind.AcceptBust, Seat = seat };
        public static ElectionAction SpendPetition(int seat) => new ElectionAction { Kind = ActionKind.SpendPetition, Seat = seat };
    }

    /// <summary>Refusal codes of the council module (module-defined, not in the closed <see cref="Reasons"/> list).</summary>
    public static class CouncilReasons
    {
        public const string ElectionOver = "election_over";
        public const string NotYourTurn = "not_your_turn";
        public const string WrongPhase = "wrong_phase";
        public const string StaleAction = "stale_action";
        public const string StalePush = "stale_push";
        public const string BadTray = "bad_tray";
        public const string SocketRequired = "socket_required";
        public const string BadSocket = "bad_socket";
        public const string NothingToBank = "nothing_to_bank";
        public const string NoPetition = "no_petition";
        public const string NoPlayer = "no_player";
        public const string PetitionAlreadyEarned = "petition_already_earned";
        public const string ListeningActive = "listening_active";
        public const string ListeningNone = "listening_none";
        public const string ListeningLimit = "listening_limit";
        public const string BadAnswer = "bad_answer";
        public const string NotEligible = "not_eligible";
        public const string WrongStatus = "wrong_status";
        public const string UnknownSettlement = "unknown_settlement";
        public const string NotYourSettlement = "not_your_settlement";
        public const string AlreadyDecided = "already_decided";
    }

    public readonly struct ActionResult
    {
        public readonly bool Ok;
        public readonly string Reason;
        public readonly string Message;
        public ActionResult(bool ok, string reason, string message) { Ok = ok; Reason = reason; Message = message; }
        public static ActionResult Accept() => new ActionResult(true, null, null);
        public static ActionResult Reject(string reason, string message) => new ActionResult(false, reason, message);
        public override string ToString() => Ok ? "Ok" : Reason + ": " + Message;
    }
}
