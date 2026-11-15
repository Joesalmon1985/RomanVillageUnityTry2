using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace World.Sim.Encounters.Council
{
    /// <summary>
    /// The election rules (brief section 3). Pure and deterministic: state in, events out, no presentation.
    /// Players and AI candidates act only through <see cref="Validate"/> and <see cref="Apply"/>.
    /// </summary>
    public static class ElectionEngine
    {
        /// <summary>A new election. Seats are fixed; the starter is a fair seeded choice. Personalities may be null.</summary>
        public static ElectionState Create(ulong seed, int playerSeat, string[] personalities = null, ListeningTuning listening = null)
        {
            var master = new Rng(seed);
            var e = new ElectionState
            {
                Seed = seed,
                Rolls = master.Fork("rolls"),
                AiRng = master.Fork("ai"),
                ListenRng = master.Fork("listen"),
                PlayerSeat = playerSeat,
                ListeningRules = listening != null ? listening.Copy() : new ListeningTuning(),
            };
            e.Starter = e.Rolls.NextInt(CouncilRules.Seats);
            e.Active = e.Starter;
            if (personalities != null)
                for (int i = 0; i < CouncilRules.Seats && i < personalities.Length; i++) e.Personality[i] = personalities[i];
            return e;
        }

        // ---------------------------------------------------------------- legality

        static bool CanStep(ElectionState e, int track, int free)
        {
            int ti = track - 2;
            if (e.Claimed[ti] >= 0) return false;
            if (e.Temp[ti] > 0) return e.Temp[ti] < CouncilRules.TrackLengths[ti];
            return free > 0 && e.BankedAt(e.Active, track) < CouncilRules.TrackLengths[ti];
        }

        static Tray MakeTray(ElectionState e, int a, int b)
        {
            int free = e.CatspawsLeft;
            var tray = new Tray { SumA = a, SumB = b };
            if (a == b)
            {
                int ti = a - 2;
                if (e.Claimed[ti] >= 0) return null;
                int len = CouncilRules.TrackLengths[ti];
                int at;
                if (e.Temp[ti] > 0) at = e.Temp[ti];
                else
                {
                    if (free < 1) return null;
                    at = e.BankedAt(e.Active, a);
                }
                int steps = Math.Min(2, len - at);
                if (steps <= 0) return null;
                tray.Mode = TrayMode.Both;
                tray.Sockets[0] = new TraySocket { Track = a, Steps = 1 };
                tray.Sockets[1] = steps == 2 ? new TraySocket { Track = a, Steps = 1 } : new TraySocket();
                return tray;
            }
            bool ua = CanStep(e, a, free), ub = CanStep(e, b, free);
            if (!ua && !ub) return null;
            tray.Sockets[0] = ua ? new TraySocket { Track = a, Steps = 1 } : new TraySocket();
            tray.Sockets[1] = ub ? new TraySocket { Track = b, Steps = 1 } : new TraySocket();
            bool newA = e.Temp[a - 2] == 0, newB = e.Temp[b - 2] == 0;
            tray.Mode = ua && ub && newA && newB && free == 1 ? TrayMode.Either : TrayMode.Both;
            return tray;
        }

        static string Key(Tray t)
        {
            var sb = new StringBuilder();
            sb.Append(t.Mode == TrayMode.Either ? "E" : "B");
            var parts = new List<string>();
            if (t.Mode == TrayMode.Both)
            {
                var steps = new SortedDictionary<int, int>();
                foreach (var s in t.Sockets) if (!s.IsEmpty) steps[s.Track] = (steps.TryGetValue(s.Track, out int n) ? n : 0) + s.Steps;
                foreach (var kv in steps) parts.Add(kv.Key + ":" + kv.Value);
            }
            else
            {
                foreach (var s in t.Sockets) if (!s.IsEmpty) parts.Add(s.Track + ":" + s.Steps);
                parts.Sort(StringComparer.Ordinal);
            }
            return sb.Append('|').Append(string.Join("|", parts)).ToString();
        }

        /// <summary>The three pairings of four values, resolved against the current state. Up to three trays; equivalent moves merged.</summary>
        public static List<Tray> BuildTrays(ElectionState e, int v0, int v1, int v2, int v3)
        {
            var list = new List<Tray>();
            var keys = new HashSet<string>();
            int[,] pairs = { { v0 + v1, v2 + v3 }, { v0 + v2, v1 + v3 }, { v0 + v3, v1 + v2 } };
            for (int i = 0; i < 3; i++)
            {
                int x = pairs[i, 0], y = pairs[i, 1];
                var t = MakeTray(e, Math.Min(x, y), Math.Max(x, y));
                if (t == null) continue;
                if (keys.Add(Key(t))) list.Add(t);
            }
            return list;
        }

        // Class of each track for the bust-odds memo: 0 closed, 1 free (no catspaw), 2 catspaw, 3 catspaw at the top.
        static readonly ConcurrentDictionary<int, int> BustMemo = new ConcurrentDictionary<int, int>();

        /// <summary>Number of the 1296 equally likely pushes that would have no legal move now.</summary>
        public static int BustOutcomes(ElectionState e)
        {
            int key = 0;
            for (int i = 0; i < CouncilRules.TrackCount; i++)
            {
                int c = e.Claimed[i] >= 0 ? 0 : e.Temp[i] == 0 ? 1 : e.Temp[i] >= CouncilRules.TrackLengths[i] ? 3 : 2;
                key |= c << (2 * i);
            }
            if (BustMemo.TryGetValue(key, out int n)) return n;
            n = 0;
            for (int a = 1; a <= 6; a++)
                for (int b = 1; b <= 6; b++)
                    for (int c = 1; c <= 6; c++)
                        for (int d = 1; d <= 6; d++)
                            if (BuildTrays(e, a, b, c, d).Count == 0) n++;
            BustMemo[key] = n;
            return n;
        }

        /// <summary>Chance (percent) that the next push has no legal move.</summary>
        public static double BustPercent(ElectionState e) => BustOutcomes(e) * 100.0 / 1296.0;

        // ---------------------------------------------------------------- actions

        public static ActionResult Validate(ElectionState e, ElectionAction a)
        {
            if (e == null || a == null) return ActionResult.Reject(CouncilReasons.WrongPhase, "There is no election.");
            if (e.Phase == ElectionPhase.Complete) return ActionResult.Reject(CouncilReasons.ElectionOver, "The election is over.");
            if (a.Seat != e.Active) return ActionResult.Reject(CouncilReasons.NotYourTurn, "It is not that candidate's turn.");
            if (a.ExpectedSeq >= 0 && a.ExpectedSeq != e.ActionSeq) return ActionResult.Reject(CouncilReasons.StaleAction, "That action is out of date.");
            switch (a.Kind)
            {
                case ActionKind.Push:
                    if (e.Phase != ElectionPhase.Ready) return ActionResult.Reject(CouncilReasons.WrongPhase, "A push cannot be made now.");
                    return ActionResult.Accept();
                case ActionKind.Choose:
                    {
                        if (e.Phase != ElectionPhase.Choosing) return ActionResult.Reject(CouncilReasons.WrongPhase, "There is no pending push.");
                        if (a.PushId != e.PushId) return ActionResult.Reject(CouncilReasons.StalePush, "That push is no longer pending.");
                        if (a.Tray < 0 || a.Tray >= e.Pending.Count) return ActionResult.Reject(CouncilReasons.BadTray, "There is no such tray.");
                        var tray = e.Pending[a.Tray];
                        if (tray.Mode == TrayMode.Either)
                        {
                            if (a.Socket < 0) return ActionResult.Reject(CouncilReasons.SocketRequired, "Choose one of the two carvings.");
                            if (a.Socket > 1 || tray.Sockets[a.Socket].IsEmpty) return ActionResult.Reject(CouncilReasons.BadSocket, "That socket is empty.");
                        }
                        else if (a.Socket > 1) return ActionResult.Reject(CouncilReasons.BadSocket, "There is no such socket.");
                        return ActionResult.Accept();
                    }
                case ActionKind.Bank:
                    if (e.Phase != ElectionPhase.Ready) return ActionResult.Reject(CouncilReasons.WrongPhase, "Banking is not possible now.");
                    if (!e.HasAdvanced) return ActionResult.Reject(CouncilReasons.NothingToBank, "There is nothing to bank yet.");
                    return ActionResult.Accept();
                case ActionKind.AcceptBust:
                    if (e.Phase != ElectionPhase.BustPending) return ActionResult.Reject(CouncilReasons.WrongPhase, "There is no setback to accept.");
                    return ActionResult.Accept();
                case ActionKind.SpendPetition:
                    if (e.Phase != ElectionPhase.BustPending) return ActionResult.Reject(CouncilReasons.WrongPhase, "The petition is only spent after a setback.");
                    if (!e.PetitionAvailable || e.Active != e.PlayerSeat) return ActionResult.Reject(CouncilReasons.NoPetition, "There is no spare petition.");
                    return ActionResult.Accept();
            }
            return ActionResult.Reject(CouncilReasons.WrongPhase, "Unknown action.");
        }

        /// <summary>Validates and applies one action, appending events. A refused action changes nothing.</summary>
        public static ActionResult Apply(ElectionState e, ElectionAction a, List<CouncilEvent> events)
        {
            var r = Validate(e, a);
            if (!r.Ok) return r;
            if (events == null) events = new List<CouncilEvent>();
            e.ActionSeq++;
            switch (a.Kind)
            {
                case ActionKind.Push: Push(e, events, false); break;
                case ActionKind.Choose: Choose(e, a, events); break;
                case ActionKind.Bank: Bank(e, events); break;
                case ActionKind.AcceptBust: Bust(e, events); break;
                case ActionKind.SpendPetition:
                    e.PetitionSpent = true;
                    events.Add(new CouncilPetitionSpent { Seat = e.Active });
                    Push(e, events, true);
                    break;
            }
            return r;
        }

        static void Push(ElectionState e, List<CouncilEvent> ev, bool fromPetition)
        {
            e.PushId++;
            e.Pushes++;
            e.SittingPushes++;
            int v0 = e.Rolls.NextRange(1, 7), v1 = e.Rolls.NextRange(1, 7), v2 = e.Rolls.NextRange(1, 7), v3 = e.Rolls.NextRange(1, 7);
            ev.Add(new CouncilPushMade { Seat = e.Active, PushId = e.PushId, FromPetition = fromPetition });
            var trays = BuildTrays(e, v0, v1, v2, v3);
            ev.Add(new CouncilOptionsOffered { Seat = e.Active, PushId = e.PushId, Trays = trays });
            if (trays.Count > 0)
            {
                e.Pending = new List<Tray>(trays);
                e.Phase = ElectionPhase.Choosing;
                return;
            }
            e.Pending = new List<Tray>();
            if (e.PetitionAvailable && e.Active == e.PlayerSeat)
            {
                e.Phase = ElectionPhase.BustPending;
                ev.Add(new CouncilBustPending { Seat = e.Active, PushId = e.PushId });
                return;
            }
            Bust(e, ev);
        }

        static void Choose(ElectionState e, ElectionAction a, List<CouncilEvent> ev)
        {
            var tray = e.Pending[a.Tray];
            var steps = new SortedDictionary<int, int>();
            if (tray.Mode == TrayMode.Either) steps[tray.Sockets[a.Socket].Track] = tray.Sockets[a.Socket].Steps;
            else foreach (var s in tray.Sockets) if (!s.IsEmpty) steps[s.Track] = (steps.TryGetValue(s.Track, out int n) ? n : 0) + s.Steps;
            var made = new CouncilAdvanceMade { Seat = e.Active, PushId = e.PushId, Tray = a.Tray, Socket = a.Socket };
            foreach (var kv in steps)
            {
                int ti = kv.Key - 2;
                bool isNew = e.Temp[ti] == 0;
                int from = isNew ? e.BankedAt(e.Active, kv.Key) : e.Temp[ti];
                int to = from + kv.Value;
                e.Temp[ti] = to;
                made.Steps.Add(new AdvanceStep { Track = kv.Key, From = from, To = to, NewCatspaw = isNew, ReachedTop = to == CouncilRules.TrackLengths[ti] });
            }
            e.Pending = new List<Tray>();
            e.Phase = ElectionPhase.Ready;
            e.HasAdvanced = true;
            e.SittingAdvances++;
            e.Advances++;
            ev.Add(made);
        }

        static void Bank(ElectionState e, List<CouncilEvent> ev)
        {
            int seat = e.Active;
            var banked = new CouncilBanked { Seat = seat };
            var claims = new List<int>();
            for (int ti = 0; ti < CouncilRules.TrackCount; ti++)
            {
                if (e.Temp[ti] <= 0) continue;
                e.Banked[seat * CouncilRules.TrackCount + ti] = e.Temp[ti];
                banked.Places.Add(new BankedPlace { Track = ti + 2, Place = e.Temp[ti] });
                if (e.Temp[ti] == CouncilRules.TrackLengths[ti]) claims.Add(ti);
                e.Temp[ti] = 0;
            }
            e.Banks++;
            ev.Add(banked);
            foreach (int ti in claims)
            {
                e.Claimed[ti] = seat;
                var claim = new CouncilTrackClaimed { Seat = seat, Track = ti + 2 };
                for (int s = 0; s < CouncilRules.Seats; s++)
                {
                    if (s == seat) continue;
                    int idx = s * CouncilRules.TrackCount + ti;
                    if (e.Banked[idx] > 0) { e.Banked[idx] = 0; claim.RemovedFrom.Add(s); }
                }
                ev.Add(claim);
            }
            if (e.ClaimsOf(seat) >= CouncilRules.ClaimsToWin)
            {
                e.Winner = seat;
                e.Phase = ElectionPhase.Complete;
                e.HasAdvanced = false;
                ev.Add(new CouncilElectionWon { Seat = seat });
                return;
            }
            PassTurn(e, ev);
        }

        static void Bust(ElectionState e, List<CouncilEvent> ev)
        {
            var bust = new CouncilBust { Seat = e.Active };
            for (int ti = 0; ti < CouncilRules.TrackCount; ti++)
            {
                if (e.Temp[ti] > 0) bust.Lost.Add(new BankedPlace { Track = ti + 2, Place = e.Temp[ti] });
                e.Temp[ti] = 0;
            }
            e.Busts++;
            ev.Add(bust);
            PassTurn(e, ev);
        }

        static void PassTurn(ElectionState e, List<CouncilEvent> ev)
        {
            int from = e.Active;
            e.Active = (e.Active + 1) % CouncilRules.Seats;
            e.Phase = ElectionPhase.Ready;
            e.Pending = new List<Tray>();
            e.HasAdvanced = false;
            e.SittingPushes = 0;
            e.SittingAdvances = 0;
            e.Sittings++;
            ev.Add(new CouncilTurnPassed { Seat = from, From = from, To = e.Active, Sitting = e.Sittings });
        }

        /// <summary>The advances a tray tap would commit, as (track, steps) pairs ascending; for read-only previews.</summary>
        public static List<KeyValuePair<int, int>> MovesOf(Tray tray, int socket)
        {
            var steps = new SortedDictionary<int, int>();
            if (tray.Mode == TrayMode.Either) { if (socket >= 0 && socket <= 1 && !tray.Sockets[socket].IsEmpty) steps[tray.Sockets[socket].Track] = tray.Sockets[socket].Steps; }
            else foreach (var s in tray.Sockets) if (!s.IsEmpty) steps[s.Track] = (steps.TryGetValue(s.Track, out int n) ? n : 0) + s.Steps;
            return new List<KeyValuePair<int, int>>(steps);
        }
    }
}
