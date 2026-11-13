using System;
using System.Collections.Generic;

namespace World.Sim.Encounters.Ward
{
    public enum WardPhase { Setup, Active, Complete }

    public enum WardOutcome { None, Victory, Defeat, Stalemate, Clash, Forfeited }

    public sealed class WardCastEntry
    {
        public int[] Weave;
        public int Fractures;
        public int Echoes;
        public int Fades;
    }

    public abstract class WardEvent { public int AtMs; }
    public sealed class PlayerCastEvent : WardEvent { public WardCastEntry Entry; public bool Auto; }
    public sealed class RivalCastEvent : WardEvent { public WardCastEntry Entry; }
    public sealed class DuelEndedEvent : WardEvent { public WardOutcome Outcome; }

    public static class WardReasons
    {
        public const string Recovering = "recovering";
        public const string NoCasts = "no_casts";
        public const string BadWeave = "bad_weave";
        public const string NotActive = "not_active";
    }

    public sealed class CastResult
    {
        public bool Ok;
        public string Reason = "";
        public string Message = "";
        public int Fractures;
        public int Echoes;
        public int Fades;
        public bool Broken;
        public int MsUntilNextCast;
    }

    /// <summary>Time-stepped Ward duel against Mira. Integer milliseconds, deterministic, independent of the world sim.</summary>
    public sealed class WardDuelSession
    {
        readonly WardTuning _t;
        readonly int[] _pool;
        readonly Rng _timing, _idle;
        readonly WardRival _rival;
        readonly int[] _rivalWard;
        int[] _playerWard;
        readonly List<WardCastEntry> _playerHistory = new List<WardCastEntry>();
        readonly List<WardCastEntry> _rivalHistory = new List<WardCastEntry>();
        readonly List<WardEvent> _events = new List<WardEvent>();
        int _now, _recoveryUntil, _idleAt, _rivalNextAt;
        bool _playerBroke, _rivalBroke;

        public WardPhase Phase { get; private set; }
        public WardOutcome Outcome { get; private set; }
        public int ElapsedMs => _now;
        public int PlayerCastsLeft => _t.MaxCasts - _playerHistory.Count;
        public int RivalCastsLeft => _t.MaxCasts - _rivalHistory.Count;
        public int MsUntilPlayerMayCast => Math.Max(0, _recoveryUntil - _now);
        public IReadOnlyList<WardCastEntry> PlayerHistory => _playerHistory;
        public IReadOnlyList<WardCastEntry> RivalHistory => _rivalHistory;

        /// <summary>Null until the duel is complete.</summary>
        public int[] RevealedPlayerWard => Phase == WardPhase.Complete && _playerWard != null ? (int[])_playerWard.Clone() : null;
        /// <summary>Null until the duel is complete.</summary>
        public int[] RevealedRivalWard => Phase == WardPhase.Complete ? (int[])_rivalWard.Clone() : null;

        public WardDuelSession(WardTuning tuning, Essence[] pool, ulong seed)
        {
            _t = tuning ?? new WardTuning();
            _pool = new int[pool.Length];
            for (int i = 0; i < pool.Length; i++) _pool[i] = (int)pool[i];
            var master = new Rng(seed);
            _timing = master.Fork("timing");
            _idle = master.Fork("idle");
            _rival = new WardRival(_t, pool, master.Fork("rival"));
            _rivalWard = _rival.ChooseWard();
            Phase = WardPhase.Setup;
            Outcome = WardOutcome.None;
        }

        bool Legal(int[] w)
        {
            if (w == null || w.Length != _t.Loci) return false;
            for (int i = 0; i < w.Length; i++)
            {
                bool ok = false;
                for (int j = 0; j < _pool.Length; j++) if (_pool[j] == w[i]) { ok = true; break; }
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Locks in the player's secret Ward and starts the duel. False if illegal or not in Setup.</summary>
        public bool SetPlayerWard(int[] ward)
        {
            if (Phase != WardPhase.Setup || !Legal(ward)) return false;
            _playerWard = (int[])ward.Clone();
            Phase = WardPhase.Active;
            _now = 0;
            _recoveryUntil = 0;
            _idleAt = _t.IdleAutoCastMs;
            _rivalNextAt = _timing.NextRange(_t.MiraFirstCastMinMs, _t.MiraFirstCastMaxMs + 1);
            return true;
        }

        public CastResult PlayerCast(int[] weave)
        {
            if (Phase != WardPhase.Active) return Refuse(WardReasons.NotActive, "The duel is not under way.");
            if (_playerBroke || PlayerCastsLeft <= 0) return Refuse(WardReasons.NoCasts, "You have no casts left.");
            if (!Legal(weave)) return Refuse(WardReasons.BadWeave, "A weave needs " + _t.Loci + " essences from this duel's pool.");
            int wait = MsUntilPlayerMayCast;
            if (wait > 0) return Refuse(WardReasons.Recovering, "Still recovering: " + wait + " ms until the next cast.");
            var entry = DoPlayerCast(weave, false);
            bool broke = _playerBroke;
            Evaluate();
            return new CastResult
            {
                Ok = true, Fractures = entry.Fractures, Echoes = entry.Echoes, Fades = entry.Fades,
                Broken = broke, MsUntilNextCast = MsUntilPlayerMayCast
            };
        }

        CastResult Refuse(string code, string msg) =>
            new CastResult { Ok = false, Reason = code, Message = msg, MsUntilNextCast = MsUntilPlayerMayCast };

        WardCastEntry DoPlayerCast(int[] weave, bool auto)
        {
            var s = WardScorer.Score(_rivalWard, weave);
            var e = new WardCastEntry { Weave = (int[])weave.Clone(), Fractures = s.fractures, Echoes = s.echoes, Fades = s.fades };
            _playerHistory.Add(e);
            if (s.broken) _playerBroke = true;
            _recoveryUntil = _now + _t.PlayerRecoveryMs;
            _idleAt = _now + _t.IdleAutoCastMs;
            _events.Add(new PlayerCastEvent { AtMs = _now, Entry = e, Auto = auto });
            return e;
        }

        void DoRivalCast()
        {
            var weave = _rival.NextCast();
            var s = WardScorer.Score(_playerWard, weave);
            var e = new WardCastEntry { Weave = weave, Fractures = s.fractures, Echoes = s.echoes, Fades = s.fades };
            _rivalHistory.Add(e);
            _rival.Observe(weave, s.fractures, s.echoes);
            if (s.broken) _rivalBroke = true;
            _events.Add(new RivalCastEvent { AtMs = _now, Entry = e });
            _rivalNextAt = _now + _timing.NextRange(_t.MiraThinkMinMs, _t.MiraThinkMaxMs + 1);
        }

        /// <summary>
        /// Moves the duel clock forward. Casts due inside the step happen in time order (player first on a tie);
        /// the end is judged after the whole step, so two breaks in one step are a Clash.
        /// </summary>
        public void Advance(int dtMs)
        {
            if (Phase != WardPhase.Active || dtMs < 0) return;
            int target = _now + dtMs;
            while (true)
            {
                bool playerDue = !_playerBroke && PlayerCastsLeft > 0 && _idleAt <= target;
                bool rivalDue = !_rivalBroke && RivalCastsLeft > 0 && _rivalNextAt <= target;
                if (!playerDue && !rivalDue) break;
                if (playerDue && (!rivalDue || _idleAt <= _rivalNextAt))
                {
                    _now = Math.Max(_now, _idleAt);
                    DoPlayerCast(RandomWeave(_idle), true);
                }
                else
                {
                    _now = Math.Max(_now, _rivalNextAt);
                    DoRivalCast();
                }
            }
            _now = target;
            Evaluate();
        }

        int[] RandomWeave(Rng r)
        {
            var w = new int[_t.Loci];
            for (int i = 0; i < w.Length; i++) w[i] = _pool[r.NextInt(_pool.Length)];
            return w;
        }

        void Evaluate()
        {
            if (Phase != WardPhase.Active) return;
            if (_playerBroke && _rivalBroke) End(WardOutcome.Clash);
            else if (_playerBroke) End(WardOutcome.Victory);
            else if (_rivalBroke) End(WardOutcome.Defeat);
            else if (PlayerCastsLeft <= 0 && RivalCastsLeft <= 0) End(WardOutcome.Stalemate);
        }

        void End(WardOutcome o)
        {
            Outcome = o;
            Phase = WardPhase.Complete;
            _events.Add(new DuelEndedEvent { AtMs = _now, Outcome = o });
        }

        /// <summary>Leaves the duel. Not a completed duel; both Wards are revealed.</summary>
        public void Forfeit()
        {
            if (Phase == WardPhase.Complete) return;
            End(WardOutcome.Forfeited);
        }

        /// <summary>Events since the last call, oldest first.</summary>
        public List<WardEvent> DrainEvents()
        {
            var r = new List<WardEvent>(_events);
            _events.Clear();
            return r;
        }
    }
}
