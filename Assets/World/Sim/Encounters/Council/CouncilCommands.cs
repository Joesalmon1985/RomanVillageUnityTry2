using System;
using System.Collections.Generic;

namespace World.Sim.Encounters.Council
{
    /// <summary>Base of every council command: the settlement whose council it addresses.</summary>
    public abstract class CouncilCommand : Command
    {
        public int Settlement;
    }

    /// <summary>A resident approaches: an election is offered for the settlement (needs four residents who want to stand).</summary>
    public sealed class CouncilOffer : CouncilCommand { }

    /// <summary>Answers an offer: Help = advise the asking candidate, otherwise leave all four to play themselves.</summary>
    public sealed class CouncilRespond : CouncilCommand { public bool Help; }

    /// <summary>Base of the player's election actions. ExpectedSeq (optional, -1 = off) refuses a stale action.</summary>
    public abstract class CouncilActionCommand : CouncilCommand
    {
        public int ExpectedSeq = -1;
        internal abstract ElectionAction Build(int seat);
    }

    /// <summary>Press on: draw a fresh push.</summary>
    public sealed class CouncilPush : CouncilActionCommand
    {
        internal override ElectionAction Build(int seat) => ElectionAction.Push(seat);
    }

    /// <summary>Commit one tray (Both) or one carving of a tray (Either: Socket 0 or 1) of the pending push.</summary>
    public sealed class CouncilChoose : CouncilActionCommand
    {
        public int PushId;
        public int Tray;
        public int Socket = -1;
        internal override ElectionAction Build(int seat) => ElectionAction.Choose(seat, PushId, Tray, Socket);
    }

    public sealed class CouncilBank : CouncilActionCommand
    {
        internal override ElectionAction Build(int seat) => ElectionAction.Bank(seat);
    }

    /// <summary>Accept the setback after a push with no legal move (when the spare petition is in hand).</summary>
    public sealed class CouncilAcceptBust : CouncilActionCommand
    {
        internal override ElectionAction Build(int seat) => ElectionAction.AcceptBust(seat);
    }

    public sealed class CouncilSpendPetition : CouncilActionCommand
    {
        internal override ElectionAction Build(int seat) => ElectionAction.SpendPetition(seat);
    }

    /// <summary>One AI action for the active seat (refused when the active candidate is the player's). Call repeatedly to play a rival's turn.</summary>
    public sealed class CouncilAiStep : CouncilCommand { }

    public sealed class CouncilStartListening : CouncilCommand { }

    /// <summary>One answer in the burst. ResponseMs is measured by the caller from when the line was readable.</summary>
    public sealed class CouncilAnswerListening : CouncilCommand { public ListeningChoice Choice; public int ResponseMs; }

    public sealed class CouncilAbandonListening : CouncilCommand { }

    public sealed class CouncilSkipListening : CouncilCommand { }

    /// <summary>The player leaves the council (any burst under way is dropped). State is kept.</summary>
    public sealed class CouncilLeave : CouncilCommand { }

    public sealed class CouncilResume : CouncilCommand { }

    // ------------------------------------------------------------------ handlers

    internal sealed class CouncilContext
    {
        public CouncilContent Content;
    }

    internal static class CouncilOps
    {
        public static SettlementCouncil Find(WorldState s, int settlement)
        {
            if (s.Council == null) return null;
            foreach (var c in s.Council.Settlements) if (c.SettlementId == settlement) return c;
            return null;
        }

        public static CommandResult ToResult(ActionResult r) =>
            r.Ok ? CommandResult.Accept() : CommandResult.Reject(r.Reason, r.Message);

        public static CommandResult InProgress(WorldState s, CouncilCommand c, out SettlementCouncil sc)
        {
            sc = Find(s, c.Settlement);
            if (sc == null) return CommandResult.Reject(CouncilReasons.WrongStatus, "There is no election here.");
            if (sc.Status != CouncilStatus.InProgress) return CommandResult.Reject(
                sc.Status == CouncilStatus.Decided ? CouncilReasons.AlreadyDecided : CouncilReasons.WrongStatus, "The election is not in progress.");
            return CommandResult.Accept();
        }

        public static void Emit(WorldState s, SettlementCouncil sc, List<CouncilEvent> from, EventBuffer ev)
        {
            foreach (var e in from) { e.Settlement = sc.SettlementId; ev.Add(e); }
            if (sc.Election.Phase == ElectionPhase.Complete && !sc.Applied) Decide(s, sc, ev);
        }

        /// <summary>Records the winner exactly once.</summary>
        static void Decide(WorldState s, SettlementCouncil sc, EventBuffer ev)
        {
            sc.Applied = true;
            sc.Status = CouncilStatus.Decided;
            sc.WinnerSeat = sc.Election.Winner;
            sc.DecidedTick = s.Tick;
            var w = sc.Candidates[sc.WinnerSeat];
            bool has = false;
            foreach (var o in s.Council.Offices) if (o.Settlement == sc.SettlementId) has = true;
            if (!has)
                s.Council.Offices.Add(new OfficeHolder
                {
                    Settlement = sc.SettlementId, Name = w.Name, ResidentKey = w.ResidentKey, Occupation = w.Occupation,
                    Office = sc.Office, ElectionNo = sc.ElectionNo, Tick = s.Tick, Seat = sc.WinnerSeat,
                });
            ev.Add(new CouncilOfficeHandedOver { Settlement = sc.SettlementId, Seat = sc.WinnerSeat, WinnerName = w.Name, Office = sc.Office });
        }
    }

    internal sealed class OfferHandler : CommandHandler<CouncilOffer>
    {
        readonly CouncilContext _ctx;
        public OfferHandler(CouncilContext ctx) { _ctx = ctx; }

        protected override CommandResult Validate(WorldState s, CouncilOffer c)
        {
            var st = CouncilRoster.ActiveSettlement(s, c.Settlement);
            if (st == null) return CommandResult.Reject(CouncilReasons.UnknownSettlement, "There is no such settlement.");
            if (st.Faction != c.PlayerId) return CommandResult.Reject(CouncilReasons.NotYourSettlement, "That settlement is not yours.");
            var existing = CouncilOps.Find(s, c.Settlement);
            if (existing != null)
                return CommandResult.Reject(existing.Status == CouncilStatus.Decided ? CouncilReasons.AlreadyDecided : CouncilReasons.WrongStatus,
                    "This village already has an election.");
            if (!CouncilRoster.IsEligible(s, c.Settlement, _ctx.Content, out var reason))
                return CommandResult.Reject(reason, "Too few residents want to stand.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CouncilOffer c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilSetup.Offer(s, c.Settlement, _ctx.Content);
            s.Council.Settlements.Add(sc);
            s.Council.Settlements.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
            ev.Add(new CouncilOffered { Settlement = c.Settlement, Seat = sc.AskerSeat, AskerSeat = sc.AskerSeat });
            return v;
        }
    }

    internal sealed class RespondHandler : CommandHandler<CouncilRespond>
    {
        readonly CouncilContext _ctx;
        public RespondHandler(CouncilContext ctx) { _ctx = ctx; }

        protected override CommandResult Validate(WorldState s, CouncilRespond c)
        {
            var sc = CouncilOps.Find(s, c.Settlement);
            if (sc == null || sc.Status != CouncilStatus.Offered) return CommandResult.Reject(CouncilReasons.WrongStatus, "No election is waiting for an answer.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CouncilRespond c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            CouncilSetup.Start(sc, c.Help, _ctx.Content);
            ev.Add(new CouncilStarted { Settlement = sc.SettlementId, Seat = sc.Election.PlayerSeat, PlayerHelps = c.Help, Starter = sc.Election.Starter });
            return v;
        }
    }

    internal sealed class ActionHandler<T> : CommandHandler<T> where T : CouncilActionCommand
    {
        protected override CommandResult Validate(WorldState s, T c)
        {
            var r = CouncilOps.InProgress(s, c, out var sc);
            if (!r.Ok) return r;
            if (sc.Election.PlayerSeat < 0) return CommandResult.Reject(CouncilReasons.NoPlayer, "You are not advising a candidate.");
            var a = c.Build(sc.Election.PlayerSeat);
            a.ExpectedSeq = c.ExpectedSeq;
            return CouncilOps.ToResult(ElectionEngine.Validate(sc.Election, a));
        }

        protected override CommandResult Apply(WorldState s, T c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            var a = c.Build(sc.Election.PlayerSeat);
            a.ExpectedSeq = c.ExpectedSeq;
            var events = new List<CouncilEvent>();
            var r = ElectionEngine.Apply(sc.Election, a, events);
            CouncilOps.Emit(s, sc, events, ev);
            return CouncilOps.ToResult(r);
        }
    }

    internal sealed class AiStepHandler : CommandHandler<CouncilAiStep>
    {
        readonly CouncilContext _ctx;
        public AiStepHandler(CouncilContext ctx) { _ctx = ctx; }

        protected override CommandResult Validate(WorldState s, CouncilAiStep c)
        {
            var r = CouncilOps.InProgress(s, c, out var sc);
            if (!r.Ok) return r;
            var e = sc.Election;
            if (e.Phase == ElectionPhase.Complete) return CommandResult.Reject(CouncilReasons.ElectionOver, "The election is over.");
            if (e.Active == e.PlayerSeat) return CommandResult.Reject(CouncilReasons.NotYourTurn, "It is your candidate's turn.");
            return CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CouncilAiStep c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            var e = sc.Election;
            var p = _ctx.Content.Personality(e.Personality[e.Active]);
            var events = new List<CouncilEvent>();
            var r = ElectionEngine.Apply(e, CouncilAi.Decide(e, p), events);
            CouncilOps.Emit(s, sc, events, ev);
            return CouncilOps.ToResult(r);
        }
    }

    internal sealed class StartListeningHandler : CommandHandler<CouncilStartListening>
    {
        readonly CouncilContext _ctx;
        public StartListeningHandler(CouncilContext ctx) { _ctx = ctx; }

        protected override CommandResult Validate(WorldState s, CouncilStartListening c)
        {
            var r = CouncilOps.InProgress(s, c, out var sc);
            return r.Ok ? CouncilOps.ToResult(ElectionListening.ValidateStart(sc.Election)) : r;
        }

        protected override CommandResult Apply(WorldState s, CouncilStartListening c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            var events = new List<CouncilEvent>();
            var r = ElectionListening.Start(sc.Election, _ctx.Content.ListeningLines, events);
            CouncilOps.Emit(s, sc, events, ev);
            return CouncilOps.ToResult(r);
        }
    }

    internal sealed class AnswerListeningHandler : CommandHandler<CouncilAnswerListening>
    {
        protected override CommandResult Validate(WorldState s, CouncilAnswerListening c)
        {
            var r = CouncilOps.InProgress(s, c, out var sc);
            return r.Ok ? CouncilOps.ToResult(ListeningSession.ValidateAnswer(sc.Election.Listening, (int)c.Choice, c.ResponseMs)) : r;
        }

        protected override CommandResult Apply(WorldState s, CouncilAnswerListening c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            var events = new List<CouncilEvent>();
            var r = ElectionListening.Answer(sc.Election, (int)c.Choice, c.ResponseMs, events);
            CouncilOps.Emit(s, sc, events, ev);
            return CouncilOps.ToResult(r);
        }
    }

    internal sealed class AbandonListeningHandler : CommandHandler<CouncilAbandonListening>
    {
        protected override CommandResult Validate(WorldState s, CouncilAbandonListening c)
        {
            var r = CouncilOps.InProgress(s, c, out var sc);
            if (!r.Ok) return r;
            var l = sc.Election.Listening;
            return l != null && l.Phase == ListeningPhase.InProgress ? CommandResult.Accept()
                : CommandResult.Reject(CouncilReasons.ListeningNone, "There is no burst to leave.");
        }

        protected override CommandResult Apply(WorldState s, CouncilAbandonListening c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            var events = new List<CouncilEvent>();
            var r = ElectionListening.Abandon(sc.Election, events);
            CouncilOps.Emit(s, sc, events, ev);
            return CouncilOps.ToResult(r);
        }
    }

    internal sealed class SkipListeningHandler : CommandHandler<CouncilSkipListening>
    {
        protected override CommandResult Validate(WorldState s, CouncilSkipListening c)
        {
            var r = CouncilOps.InProgress(s, c, out var sc);
            if (!r.Ok) return r;
            return sc.Election.PlayerSeat < 0 ? CommandResult.Reject(CouncilReasons.NoPlayer, "You are not advising a candidate.") : CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CouncilSkipListening c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            var events = new List<CouncilEvent>();
            var r = ElectionListening.Skip(sc.Election, events);
            CouncilOps.Emit(s, sc, events, ev);
            return CouncilOps.ToResult(r);
        }
    }

    internal sealed class LeaveHandler : CommandHandler<CouncilLeave>
    {
        protected override CommandResult Validate(WorldState s, CouncilLeave c)
        {
            var sc = CouncilOps.Find(s, c.Settlement);
            if (sc == null || (sc.Status != CouncilStatus.InProgress && sc.Status != CouncilStatus.Offered))
                return CommandResult.Reject(CouncilReasons.WrongStatus, "There is nothing to leave.");
            return sc.Present ? CommandResult.Accept() : CommandResult.Reject(CouncilReasons.WrongPhase, "You have already left.");
        }

        protected override CommandResult Apply(WorldState s, CouncilLeave c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            var sc = CouncilOps.Find(s, c.Settlement);
            sc.Present = false;
            if (sc.Election != null && sc.Election.Listening != null && sc.Election.Listening.Phase == ListeningPhase.InProgress)
            {
                var events = new List<CouncilEvent>();
                ElectionListening.Abandon(sc.Election, events);
                CouncilOps.Emit(s, sc, events, ev);
            }
            ev.Add(new CouncilLeft { Settlement = sc.SettlementId });
            return v;
        }
    }

    internal sealed class ResumeHandler : CommandHandler<CouncilResume>
    {
        protected override CommandResult Validate(WorldState s, CouncilResume c)
        {
            var sc = CouncilOps.Find(s, c.Settlement);
            if (sc == null || (sc.Status != CouncilStatus.InProgress && sc.Status != CouncilStatus.Offered))
                return CommandResult.Reject(CouncilReasons.WrongStatus, "There is nothing to resume.");
            return sc.Present ? CommandResult.Reject(CouncilReasons.WrongPhase, "You are already there.") : CommandResult.Accept();
        }

        protected override CommandResult Apply(WorldState s, CouncilResume c, EventBuffer ev)
        {
            var v = Validate(s, c);
            if (!v.Ok) return v;
            CouncilOps.Find(s, c.Settlement).Present = true;
            ev.Add(new CouncilResumed { Settlement = c.Settlement });
            return v;
        }
    }
}
