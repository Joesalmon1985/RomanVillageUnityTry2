using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>Something that happened during a tick, for views to animate. Modules define subclasses.</summary>
    public abstract class SimEvent
    {
        /// <summary>Tick during which the event happened. Stamped by <see cref="EventBuffer.Add"/>.</summary>
        public long Tick;
    }

    /// <summary>Collects events in the order systems emit them.</summary>
    public sealed class EventBuffer
    {
        private readonly List<SimEvent> _items = new List<SimEvent>();

        /// <summary>Tick stamped onto added events (set by WorldSim at the start of each Step).</summary>
        public long CurrentTick;

        public IReadOnlyList<SimEvent> Items => _items;
        public int Count => _items.Count;

        public void Add(SimEvent e)
        {
            e.Tick = CurrentTick;
            _items.Add(e);
        }

        public List<SimEvent> Drain()
        {
            var copy = new List<SimEvent>(_items);
            _items.Clear();
            return copy;
        }
    }

    /// <summary>A queued command failed its re-check at the tick boundary and was not applied.</summary>
    public sealed class CommandRejected : SimEvent
    {
        public Command Command;
        public CommandResult Result;
    }

    /// <summary>A queued command was applied at the tick boundary.</summary>
    public sealed class CommandApplied : SimEvent
    {
        public Command Command;
    }
}
