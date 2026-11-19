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
        /// <summary>The version the file was written with (FromJson migrates Version up to CurrentVersion). Not saved.</summary>
        [JsonIgnore]
        public int LoadedVersion;
        /// <summary>Sim tick at save (copy of State.Tick, for slot lists).</summary>
        public long Tick;
        public PlayerPose Player;
        /// <summary>Free-form game-layer data (camera mode, UI state...). Keys are sorted on save.</summary>
        public SortedDictionary<string, string> Extras = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public WorldState State;
    }

    /// <summary>
    /// Save and load. Save between ticks: commands still queued in the sim (not yet applied) are not part of
    /// the state and are dropped. Everything else (stock, sites, shipments in flight, dice deck, proposals,
    /// warnings, demons, random state, Ward progress) is in <see cref="WorldState"/>.
    /// </summary>
    public static class SaveCodec
    {
        /// <summary>
        /// 6 = the god board (G0-2): WorldState.Ruleset, Table and Plague (null in a Legacy game), Board.Ports, the peoples
        /// list. Version 5 loads by migration as a Legacy game.
        /// 5 = the later ages, slice H1 (PRD_AGES A.2-A.5): WorldState.Eras, History, Ledger and Campaign (null = none),
        /// Board.Landmarks, building grades, town fields, advice and handover fields. Version 4 loads by migration
        /// (<see cref="SaveMigrations"/>) as a Prehistoric skirmish.
        /// 4 = prehistoric refactor (dice and hex stock, settlements with plots, main roads, factions, demons). Older saves are refused.
        /// 3 = economy rebalance (harvest season: EconomyState.RoundStartedTick; one draft pick per round:
        /// StrategyState.DraftNextStep; 24 cards; economy targets and per-terrain batch sizes in the tuning).
        /// 2 = unified economy (14 wares, 20 building types, outbreaks, military comparison). Older saves are refused.
        /// </summary>
        public const int CurrentVersion = 6;

        /// <summary>Oldest save version this build reads.</summary>
        public const int OldestVersion = 4;

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

        /// <summary>Parse a save. Throws <see cref="FormatException"/> for an empty, unreadable, older or newer save.</summary>
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
            if (m.Version < OldestVersion) throw new FormatException("The save is from an older version (" + m.Version + ") with a different world.");
            if (m.Extras == null) m.Extras = new SortedDictionary<string, string>(StringComparer.Ordinal);
            SaveMigrations.Upgrade(m);
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
            PeopleDefs.Ensure(loaded);   // G0-1: saves from before the peoples list get it from their players
            var sim = new WorldSim(loaded);
            Economy.EconomyModule.Register(sim);
            Factions.FactionsModule.Register(sim);
            // G0-2: the legacy demons run only on the legacy ruleset; the god board has the plague instead.
            if (loaded.Ruleset == Ruleset.Legacy) Demons.DemonsModule.Register(sim);
            Eras.ErasModule.Register(sim);
            Ledger.LedgerModule.Register(sim);
            // The crystal campaign (PRD_CRYSTALS A.5): a loaded save keeps its campaign system and handlers. Idempotent.
            Campaign.CampaignModule.Register(sim);
            // The village council (elections and listening): command handlers only, state is WorldState.Council. Idempotent.
            Encounters.Council.CouncilModule.Register(sim);
            // The god board (G0-2): the table and the plague, with their seams wired again (they are not saved).
            if (loaded.Ruleset == Ruleset.GodBoard) GodBoard.GodBoardModule.Register(sim);
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
