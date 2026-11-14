using System;
using System.Collections.Generic;
using World.Sim.Factions;

namespace World.Sim.Tests
{
    public enum PlayerStyle
    {
        /// <summary>Follows the prince: 5 s talk + walk at 12.5 m/s from the last cast target + 5 s, then casts the top proposal.</summary>
        Prompt,
        /// <summary>Casts the top proposal a fixed delay after it became the pitch (and the player was free).</summary>
        FixedDelay,
        /// <summary>Casts the top proposal 90 s after it became the pitch.</summary>
        Slow,
        /// <summary>Declines each new top proposal once (after the talk), then casts the next one the prompt way.</summary>
        Decliner,
        /// <summary>Never answers the prince.</summary>
        Ignore,
        /// <summary>Casts a random open proposal at random intervals (conservation runs).</summary>
        Random,
    }

    /// <summary>
    /// A modelled human for whole-game tests (REFACTOR_SPEC 8.1 proof). Call <see cref="Act"/> before every Step. It only
    /// uses what the real player has: the open proposals of its people, CastSpell and DeclineProposal.
    /// </summary>
    public sealed class ScriptedPlayer
    {
        public const double TalkSeconds = 5, WalkSpeed = 12.5, PressSeconds = 5;

        private readonly WorldSim _sim;
        private readonly PlayerStyle _style;
        private readonly double _delaySeconds;
        private readonly Rng _rng;
        private int _plannedId = -1;
        private long _castTick = long.MaxValue;
        private bool _declineNext = true;
        private double _x, _z;
        private long _nextRandom;

        public int Casts, Declines;
        /// <summary>Tick of each successful cast submission.</summary>
        public readonly List<long> CastTicks = new List<long>();
        /// <summary>Straight-line metres from the previous cast target (the home corner for the first) to each cast target.</summary>
        public readonly List<double> CastDistances = new List<double>();
        /// <summary>When set, banish proposals are never cast (paired runs: the cost of an unbanished demon).</summary>
        public bool SkipBanish;

        public ScriptedPlayer(WorldSim sim, PlayerStyle style, double delaySeconds = 0, ulong seed = 1)
        {
            _sim = sim;
            _style = style;
            _delaySeconds = delaySeconds;
            _rng = new Rng(seed).Fork("scripted.player");
            var s = sim.State;
            int home = s.Board.StartCorners[0];
            _x = s.Board.Topology.VertexX(home);
            _z = s.Board.Topology.VertexZ(home);
        }

        public static string Name(PlayerStyle style, double delay) =>
            style == PlayerStyle.FixedDelay ? "delay " + delay.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " s" : style.ToString().ToLowerInvariant();

        public void Act()
        {
            var s = _sim.State;
            if (s.Factions.GameOver || _style == PlayerStyle.Ignore) return;
            if (_style == PlayerStyle.Random) { ActRandom(); return; }

            var top = FactionApi.Top(s, PlayerIds.Human);
            if (top == null) { _plannedId = -1; return; }
            if (top.Id != _plannedId)
            {
                _plannedId = top.Id;
                FactionApi.ProposalTarget(s, top.Id, out double tx, out double tz, out _);
                double walk = Math.Sqrt((tx - _x) * (tx - _x) + (tz - _z) * (tz - _z)) / WalkSpeed;
                double secs;
                switch (_style)
                {
                    case PlayerStyle.FixedDelay: secs = _delaySeconds; break;
                    case PlayerStyle.Slow: secs = 90; break;
                    case PlayerStyle.Decliner when _declineNext: secs = TalkSeconds; break;
                    default: secs = TalkSeconds + walk + PressSeconds; break;
                }
                _castTick = s.Tick + (long)Math.Round(secs * WorldSim.TicksPerSecond);
            }
            if (s.Tick < _castTick) return;

            var p = FactionApi.GetProposal(s, _plannedId);
            if (p == null) { _plannedId = -1; return; }
            if (_style == PlayerStyle.Decliner && _declineNext)
            {
                if (_sim.Submit(new DeclineProposal { PlayerId = PlayerIds.Human, ProposalId = p.Id }).Ok) Declines++;
                _declineNext = false;
            }
            else
            {
                FactionApi.ProposalTarget(s, p.Id, out double tx, out double tz, out _);
                if (SkipBanish && p.Kind == ProposalKind.Banish)
                {
                    // Pretends the pitch was not heard: waits for the next top proposal.
                    _castTick = long.MaxValue;
                    return;
                }
                if (_sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = p.Id }).Ok)
                {
                    Casts++;
                    CastTicks.Add(s.Tick);
                    CastDistances.Add(Math.Sqrt((tx - _x) * (tx - _x) + (tz - _z) * (tz - _z)));
                    _x = tx;
                    _z = tz;
                }
                _declineNext = true;
            }
            _plannedId = -1;
            _castTick = long.MaxValue;
        }

        private void ActRandom()
        {
            var s = _sim.State;
            if (s.Tick < _nextRandom) return;
            _nextRandom = s.Tick + 50 + _rng.NextInt(400);
            var open = FactionApi.Proposals(s, PlayerIds.Human);
            if (open.Count == 0) return;
            var p = open[_rng.NextInt(open.Count)];
            if (_sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = p.Id }).Ok)
            {
                Casts++;
                CastTicks.Add(s.Tick);
            }
        }
    }
}
