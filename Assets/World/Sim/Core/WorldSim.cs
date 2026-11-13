using System;
using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>Per-tick system. Must be deterministic: no wall clock, no unordered enumeration.</summary>
    public interface ISimSystem
    {
        void Tick(WorldState s, EventBuffer ev);
    }

    /// <summary>
    /// Tick order slots (BUILD_PLAN section 3). Systems run in ascending order; equal orders run in
    /// registration order. Use these constants (optionally +1..+99 to sub-order inside a slot).
    /// </summary>
    public static class SystemOrder
    {
        // Prehistoric refactor (REFACTOR_SPEC section 5): Dice 100, Growth 150, Collect 200, Production 300,
        // Construction 400, Jobs 500, Carriers 600, Demons 1100, Brain 1150, Strength 1200, Diagnostics 1300.
        public const int Dice = 100;
        public const int Growth = 150;
        public const int Collect = 200;
        public const int Brain = 1150;
        public const int Strength = 1200;

        // Older names, kept (same slots).
        public const int Rounds = 100;          // rounds and batches
        public const int Harvest = 200;         // harvest workers
        public const int Production = 300;
        public const int Construction = 400;
        public const int Jobs = 500;            // job creation
        public const int Carriers = 600;        // carrier assignment and movement
        public const int Power = 700;
        public const int Barracks = 800;        // barracks queue
        public const int Squads = 900;          // squad orders and movement
        public const int Combat = 1000;
        public const int Demons = 1100;         // demons and blocking
        public const int Score = 1200;          // score and end checks
        public const int Diagnostics = 1300;
    }

    /// <summary>
    /// The simulation: state, ordered systems, command handlers, command gate and event buffer.
    /// Submit validates immediately and queues; queued commands apply at the start of the next Step.
    /// </summary>
    public sealed class WorldSim
    {
        public const int TicksPerSecond = 10;

        public WorldState State { get; }

        private readonly List<(int order, int index, ISimSystem system)> _systems = new List<(int, int, ISimSystem)>();
        private readonly Dictionary<Type, ICommandHandler> _handlers = new Dictionary<Type, ICommandHandler>();
        private readonly List<Command> _pending = new List<Command>();
        private readonly EventBuffer _events = new EventBuffer();

        public WorldSim(WorldState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Commands accepted by Submit and not yet applied (in submission order).</summary>
        public IReadOnlyList<Command> PendingCommands => _pending;

        public IReadOnlyList<ISimSystem> Systems
        {
            get
            {
                var list = new List<ISimSystem>(_systems.Count);
                foreach (var s in _systems) list.Add(s.system);
                return list;
            }
        }

        public void AddSystem(int order, ISimSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            _systems.Add((order, _systems.Count, system));
            _systems.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.index.CompareTo(b.index));
        }

        public void RegisterHandler(ICommandHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(handler.CommandType))
                throw new InvalidOperationException("Handler already registered for " + handler.CommandType.Name);
            _handlers.Add(handler.CommandType, handler);
        }

        public bool HasHandler(Type commandType) => _handlers.ContainsKey(commandType);

        /// <summary>Validate without queuing (for UI previews). Never mutates state.</summary>
        public CommandResult Check(Command c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (!_handlers.TryGetValue(c.GetType(), out var h))
                return CommandResult.Reject(Reasons.Disabled, "That action is not available.");
            return h.Validate(State, c);
        }

        /// <summary>Validate now; if accepted, queue for the next tick boundary.</summary>
        public CommandResult Submit(Command c)
        {
            var r = Check(c);
            if (r.Ok) _pending.Add(c);
            return r;
        }

        /// <summary>
        /// One tick: Tick += 1, apply queued commands in submission order (each re-checked), then run
        /// systems in order. Events are stamped with the new Tick value.
        /// </summary>
        public void Step()
        {
            State.Tick++;
            _events.CurrentTick = State.Tick;

            if (_pending.Count > 0)
            {
                var batch = new List<Command>(_pending);
                _pending.Clear();
                foreach (var c in batch)
                {
                    var h = _handlers[c.GetType()];
                    var check = h.Validate(State, c);
                    var r = check.Ok ? h.Apply(State, c, _events) : check;
                    if (r.Ok) _events.Add(new CommandApplied { Command = c });
                    else _events.Add(new CommandRejected { Command = c, Result = r });
                }
            }

            foreach (var s in _systems) s.system.Tick(State, _events);
        }

        public void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++) Step();
        }

        /// <summary>Events since the last drain, in emission order.</summary>
        public IReadOnlyList<SimEvent> DrainEvents() => _events.Drain();
    }
}
