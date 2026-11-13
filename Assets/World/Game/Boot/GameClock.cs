using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>Well-known pause reasons. Any string works; these are the shared ones.</summary>
    public static class PauseReasons
    {
        public const string Encounter = "Encounter";
        public const string Menu = "Menu";
        public const string Draft = "Draft";
    }

    /// <summary>
    /// Steps the WorldSim at WorldSim.TicksPerSecond (10 Hz) while no pause reason is set.
    /// After each tick it drains the sim's events and raises <see cref="Ticked"/> for views.
    /// </summary>
    public sealed class GameClock : MonoBehaviour
    {
        /// <summary>Upper bound on ticks per frame so a long hitch cannot spiral.</summary>
        public int MaxTicksPerFrame = 5;

        public WorldSim Sim { get; private set; }

        /// <summary>Raised after every tick with the events of that tick (possibly empty).</summary>
        public event Action<IReadOnlyList<SimEvent>> Ticked;

        private readonly HashSet<string> _pauseReasons = new HashSet<string>();
        private double _accumulator;

        public bool IsPaused => _pauseReasons.Count > 0;
        public IReadOnlyCollection<string> PauseReasonSet => _pauseReasons;

        public void Bind(WorldSim sim)
        {
            Sim = sim;
            _accumulator = 0;
        }

        public void Pause(string reason)
        {
            if (string.IsNullOrEmpty(reason)) throw new ArgumentException("reason required", nameof(reason));
            _pauseReasons.Add(reason);
        }

        public void Resume(string reason)
        {
            _pauseReasons.Remove(reason);
            if (!IsPaused) _accumulator = 0; // no catch-up burst after a pause
        }

        public bool IsPausedFor(string reason) => _pauseReasons.Contains(reason);

        /// <summary>Advance exactly one tick now (tests, debug). Ignores pause.</summary>
        public void StepOnce()
        {
            if (Sim == null) return;
            Sim.Step();
            Ticked?.Invoke(Sim.DrainEvents());
        }

        private void Update()
        {
            if (Sim == null) return;
            if (IsPaused)
            {
                // Commands apply at the next tick. While the clock waits only for the draft, a pick (or a card built from
                // the hand) would otherwise stay queued forever and the draft would never end: apply it with one tick.
                if (Sim.PendingCommands.Count > 0 && _pauseReasons.Count == 1 && _pauseReasons.Contains(PauseReasons.Draft)) StepOnce();
                return;
            }
            _accumulator += Time.deltaTime;
            double dt = 1.0 / WorldSim.TicksPerSecond;
            int n = 0;
            while (_accumulator >= dt && n < MaxTicksPerFrame)
            {
                _accumulator -= dt;
                StepOnce();
                n++;
            }
            if (n == MaxTicksPerFrame && _accumulator > dt) _accumulator = 0;
        }
    }
}
