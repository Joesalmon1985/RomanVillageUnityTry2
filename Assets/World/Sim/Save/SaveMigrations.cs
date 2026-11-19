using System;
using System.Collections.Generic;

namespace World.Sim.Save
{
    /// <summary>
    /// Save migration framework (PRD_AGES AG-52): one step per version from <see cref="SaveCodec.OldestVersion"/> up to
    /// <see cref="SaveCodec.CurrentVersion"/>, applied in order to the deserialised state. Fields a newer build added and
    /// an older save lacks keep their initialisers (Newtonsoft builds every object with its constructor); a step fills
    /// whole new module states and anything whose default would be wrong. Later slices append their step (5 to 6 by H2-0,
    /// 6 to 7 by M1-0, 7 to 8 by F1-0) and bump CurrentVersion; a step must be deterministic and must not run the sim.
    /// </summary>
    public static class SaveMigrations
    {
        /// <summary>Steps by the version they upgrade from: Steps[v] turns a version-v state into a version v+1 state.</summary>
        private static readonly SortedDictionary<int, Action<WorldState>> Steps = new SortedDictionary<int, Action<WorldState>>
        {
            { 4, V4ToV5 },
            { 5, V5ToV6 },
        };

        /// <summary>Migrates a model read by FromJson in place: LoadedVersion = the file's version, Version = CurrentVersion.</summary>
        public static void Upgrade(SaveModel m)
        {
            if (m == null) return;
            m.LoadedVersion = m.Version;
            for (int v = m.Version; v < SaveCodec.CurrentVersion; v++)
            {
                if (!Steps.TryGetValue(v, out var step)) throw new FormatException("The save cannot be upgraded from version " + v + ".");
                step(m.State);
            }
            m.Version = SaveCodec.CurrentVersion;
        }

        /// <summary>The steps this build has, as "from" versions (for tests).</summary>
        public static IReadOnlyCollection<int> StepVersions => Steps.Keys;

        /// <summary>
        /// v4 (prehistoric refactor) to v5 (ages H1): a Prehistoric skirmish at ch.prehistoric, empty history, one empty
        /// ledger per people, the starting landmarks, no campaign. Everything else (grades 0, no handover, no advice, no
        /// towns) is the field initialisers, which are exactly the old behaviour.
        /// </summary>
        private static void V4ToV5(WorldState s)
        {
            Eras.ErasModule.EnsureState(s);
            Ledger.LedgerModule.EnsureState(s);
            s.Campaign = null;
        }

        /// <summary>
        /// v5 (ages) to v6 (god board, G0-2): every older save is a Legacy game. It has no table and no plague; its peoples
        /// list is filled from the players (<see cref="PeopleDefs.Ensure"/>, also run by CreateSim).
        /// </summary>
        private static void V5ToV6(WorldState s)
        {
            s.Ruleset = Ruleset.Legacy;
            s.Table = null;
            s.Plague = null;
            PeopleDefs.Ensure(s);
        }
    }
}
