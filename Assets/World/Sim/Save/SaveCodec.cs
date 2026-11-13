using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace World.Sim.Save
{
    /// <summary>Player avatar pose stored with a save (game-layer data, not simulation state).</summary>
    public sealed class PlayerPose
    {
        public double X;
        public double Y;
        public double Z;
        /// <summary>Degrees.</summary>
        public double Yaw;
        public double Pitch;
    }

    /// <summary>One save slot: version, the whole simulation state, and game-layer extras supplied by the caller.</summary>
    public sealed class SaveModel
    {
        public int Version = SaveCodec.CurrentVersion;
        /// <summary>Sim tick at save (copy of State.Tick, for slot lists).</summary>
        public long Tick;
        public PlayerPose Player;
        /// <summary>Free-form game-layer data (camera mode, UI state...). Keys are sorted on save.</summary>
        public SortedDictionary<string, string> Extras = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public WorldState State;
    }

    /// <summary>
    /// Save and load. Save between ticks: commands still queued in the sim (not yet applied) are not part of
    /// the state and are dropped. Everything else (stores, shipments in flight, queues, rounds and ages, draft,
    /// infection deck, squads, random state, Ward progress) is in <see cref="WorldState"/>.
    /// </summary>
    public static class SaveCodec
    {
        public const int CurrentVersion = 1;

        private static JsonSerializerSettings Settings() => new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            // Replace (not append to) lists and arrays that constructors pre-fill.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static string ToJson(WorldState state, PlayerPose pose = null, IDictionary<string, string> extras = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var m = new SaveModel { Tick = state.Tick, Player = pose, State = state };
            if (extras != null) foreach (var kv in extras) m.Extras[kv.Key] = kv.Value;
            return JsonConvert.SerializeObject(m, Settings());
        }

        /// <summary>Parse a save. Throws <see cref="FormatException"/> for an empty, unreadable or newer save.</summary>
        public static SaveModel FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("The save is empty.");
            SaveModel m;
            try
            {
                m = JsonConvert.DeserializeObject<SaveModel>(json, Settings());
            }
            catch (JsonException e)
            {
                throw new FormatException("The save could not be read: " + e.Message, e);
            }
            if (m == null || m.State == null) throw new FormatException("The save holds no world.");
            if (m.Version > CurrentVersion) throw new FormatException("The save is from a newer version (" + m.Version + ").");
            if (m.Extras == null) m.Extras = new SortedDictionary<string, string>(StringComparer.Ordinal);
            return m;
        }

        /// <summary>Serialise only the world state (same settings as a save), e.g. to compare two states.</summary>
        public static string StateJson(WorldState state) => JsonConvert.SerializeObject(state, Settings());

        /// <summary>
        /// A working sim around a loaded state: registers every module's systems, handlers and economy hooks
        /// without re-running InitState (same order as <see cref="WorldSimFactory.CreateNew"/>).
        /// </summary>
        public static WorldSim CreateSim(WorldState loaded)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            var sim = new WorldSim(loaded);
            Economy.EconomyModule.Register(sim);
            Strategy.StrategyModule.Register(sim);
            Military.MilitaryModule.Register(sim);
            return sim;
        }

        /// <summary>FromJson then CreateSim.</summary>
        public static WorldSim Load(string json, out SaveModel model)
        {
            model = FromJson(json);
            return CreateSim(model.State);
        }
    }
}
