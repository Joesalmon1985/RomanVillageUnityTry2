using System;

namespace World.Sim.Ledger
{
    /// <summary>Read helpers for the ledger (PRD_AGES A.3). Static, null-safe, never mutate.</summary>
    public static class LedgerApi
    {
        /// <summary>Points that open the Modern roster (6.3).</summary>
        public const int RosterThreshold = 10;

        /// <summary>The credit cap before refineries and depots (6.2).</summary>
        public const long BaseCreditCap = 2000;

        /// <summary>The faction's ledger, or null.</summary>
        public static FactionLedger Of(WorldState s, int faction)
        {
            var list = s?.Ledger?.Factions;
            if (list == null || faction < 0 || faction >= list.Count) return null;
            return list[faction];
        }

        public static int Points(WorldState s, int faction) => Of(s, faction)?.Points ?? 0;

        /// <summary>Credits - ReservedCredits (0 without a ledger).</summary>
        public static long AvailableCredits(WorldState s, int faction)
        {
            var l = Of(s, faction);
            return l == null ? 0 : l.Credits - l.ReservedCredits;
        }

        public static bool IsPaid(WorldState s, int faction, string key) => key != null && (Of(s, faction)?.Paid.Contains(key) ?? false);

        public static bool RosterUnlocked(WorldState s, int faction) => Of(s, faction)?.RosterUnlocked ?? false;

        /// <summary>
        /// The cap group of a milestone key (6.1): the text before the first ':' ("camp", "city", "town", "capture",
        /// "crystal", "mission"), except "first:*" keys, each its own group (the whole key). Null for an empty key.
        /// </summary>
        public static string CapGroup(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int i = key.IndexOf(':');
            if (i < 0) return key;
            string head = key.Substring(0, i);
            return head == "first" ? key : head;
        }

        /// <summary>How many keys of a cap group may pay (6.1); int.MaxValue for "mission" (as authored) and unknown groups.</summary>
        public static int CapOf(string group)
        {
            if (group == null) return 0;
            if (group.StartsWith("first:", StringComparison.Ordinal)) return 1;
            switch (group)
            {
                case "camp": return 4;
                case "city": return 2;
                case "town": return 2;
                case "capture": return 3;
                case "crystal": return 4;
                default: return int.MaxValue;
            }
        }

        /// <summary>Keys of that group already paid to the faction.</summary>
        public static int PaidInGroup(WorldState s, int faction, string group)
        {
            var l = Of(s, faction);
            if (l == null || group == null) return 0;
            int n = 0;
            foreach (var k in l.Paid) if (CapGroup(k) == group) n++;
            return n;
        }
    }

    /// <summary>
    /// Writes the ledger (sim code only). Award is REAL from H1-0 (idempotent keys, caps, the unlock); the milestone
    /// sources are H2-3, crystal keys are paid by the crystal campaign package, credits are H2-1 and M1-2.
    /// </summary>
    internal static class LedgerLogic
    {
        /// <summary>
        /// Pays a milestone once (6.1). False if the key is already paid, its group is at its cap (cap &lt; 0: the table
        /// in <see cref="LedgerApi.CapOf"/>), points are not positive, or the faction has no ledger. Raises PointsAwarded and,
        /// at the first tick Points reaches the threshold, RosterUnlocked.
        /// </summary>
        public static bool Award(WorldState s, int faction, string key, int points, EventBuffer ev, int cap = -1)
        {
            var l = LedgerApi.Of(s, faction);
            if (l == null || string.IsNullOrEmpty(key) || points <= 0 || l.Paid.Contains(key)) return false;
            string group = LedgerApi.CapGroup(key);
            int limit = cap >= 0 ? cap : LedgerApi.CapOf(group);
            if (LedgerApi.PaidInGroup(s, faction, group) >= limit) return false;
            l.Paid.Add(key);
            l.Points += points;
            ev?.Add(new PointsAwarded { Faction = faction, Key = key, Points = points, Total = l.Points });
            if (!l.RosterUnlocked && l.Points >= LedgerApi.RosterThreshold)
            {
                l.RosterUnlocked = true;
                l.RosterUnlockedTick = s.Tick;
                ev?.Add(new RosterUnlocked { Faction = faction });
            }
            return true;
        }
    }

    /// <summary>Entry point of the Ledger module (no systems or commands in H1).</summary>
    public static class LedgerModule
    {
        public static void InitState(WorldState s)
        {
            s.Ledger = new LedgerState();
            EnsureState(s);
        }

        /// <summary>One ledger per faction (save migration v4 to v5 and new games). Keeps ledgers present.</summary>
        public static void EnsureState(WorldState s)
        {
            if (s == null) return;
            if (s.Ledger == null) s.Ledger = new LedgerState();
            for (int f = s.Ledger.Factions.Count; f < s.PeopleCount; f++) s.Ledger.Factions.Add(new FactionLedger { Faction = f });
        }

        public static void Register(WorldSim sim)
        {
        }
    }
}
