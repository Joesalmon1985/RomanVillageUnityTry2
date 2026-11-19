using System;

namespace World.Sim.Table
{
    /// <summary>What a free grant gave: "city", "settlement" or "" (nothing), and where.</summary>
    public struct TableGrant
    {
        public string Kind;
        public int Corner;
        public bool Granted => !string.IsNullOrEmpty(Kind);
        public static TableGrant None => new TableGrant { Kind = "", Corner = -1 };
    }

    /// <summary>
    /// Free pieces granted outside the turn (G2-3, the crystal reward CC-4). They ignore costs, turns, the sand and the
    /// table's pause, but keep the board's rules: the distance rule, the piece limits (TU-5), a city only on the people's
    /// own settlement. They work in every table phase except before the table exists, including after the age ended (a
    /// crystal gained in Historic, CC-8); points and the longest road are recomputed as after any build, and a grant that
    /// reaches 10 points ends the age like any build (TU-7).
    /// </summary>
    public static class TableGrants
    {
        public const string City = "city";
        public const string Settlement = "settlement";

        /// <summary>
        /// The people's most productive settlement that may become a city (highest summed pips, then lowest corner), or -1
        /// when it has none or no city piece is left.
        /// </summary>
        public static int BestCityCorner(WorldState s, int people)
        {
            var t = s?.Table;
            if (t == null || people < 0 || people >= t.PeopleCount || TableRules.CitiesLeft(t, people) <= 0) return -1;
            int best = -1, bestPips = -1;
            for (int c = 0; c < t.CornerOwner.Length; c++)
            {
                if (t.CornerOwner[c] != people || t.CornerLevel[c] != 1) continue;
                int pips = TableRules.CornerPips(s.Board, c);
                if (pips > bestPips) { best = c; bestPips = pips; }
            }
            return best;
        }

        /// <summary>
        /// The people's best legal corner for a free settlement: the distance rule holds, no road needed (it is placed for
        /// them); most pips, then most kinds of raw goods, then lowest corner. -1 when none is legal or no settlement piece
        /// is left.
        /// </summary>
        public static int BestSettlementCorner(WorldState s, int people)
        {
            var t = s?.Table;
            if (t == null || people < 0 || people >= t.PeopleCount || TableRules.SettlementsLeft(t, people) <= 0) return -1;
            int best = -1, bestPips = -1, bestKinds = -1;
            foreach (int c in TableRules.LegalSettlementCorners(s, people, false))
            {
                int pips = TableRules.CornerPips(s.Board, c);
                int kinds = TableDefaults.Kinds(s.Board, c);
                if (pips > bestPips || (pips == bestPips && kinds > bestKinds)) { best = c; bestPips = pips; bestKinds = kinds; }
            }
            return best;
        }

        /// <summary>Upgrades the settlement at the corner to a city for free. False (nothing changes) when not allowed.</summary>
        public static bool GrantCity(WorldState s, int people, int corner, EventBuffer ev)
        {
            var t = s?.Table;
            if (t == null || corner < 0 || corner >= t.CornerOwner.Length) return false;
            if (t.CornerOwner[corner] != people || t.CornerLevel[corner] != 1 || TableRules.CitiesLeft(t, people) <= 0) return false;
            t.CornerLevel[corner] = 2;
            ev?.Add(new Built { People = people, Kind = City, Spot = corner, Free = true });
            TableEngine.AfterChange(s, ev ?? new EventBuffer());
            return true;
        }

        /// <summary>Places a settlement for free (distance rule, no road needed). False (nothing changes) when not allowed.</summary>
        public static bool GrantSettlement(WorldState s, int people, int corner, EventBuffer ev)
        {
            var t = s?.Table;
            if (t == null || people < 0 || people >= t.PeopleCount || TableRules.SettlementsLeft(t, people) <= 0) return false;
            if (TableRules.SettlementRefusal(s, people, corner, false) != null) return false;
            t.CornerOwner[corner] = people;
            t.CornerLevel[corner] = 1;
            ev?.Add(new Built { People = people, Kind = Settlement, Spot = corner, Free = true });
            TableEngine.AfterChange(s, ev ?? new EventBuffer());
            return true;
        }

        /// <summary>
        /// The crystal reward of CC-4: a free city at the people's most productive settlement; if none can be upgraded, a
        /// free settlement at its best legal corner; else nothing.
        /// </summary>
        public static TableGrant GrantCityOrSettlement(WorldState s, int people, EventBuffer ev)
        {
            int c = BestCityCorner(s, people);
            if (c >= 0 && GrantCity(s, people, c, ev)) return new TableGrant { Kind = City, Corner = c };
            c = BestSettlementCorner(s, people);
            if (c >= 0 && GrantSettlement(s, people, c, ev)) return new TableGrant { Kind = Settlement, Corner = c };
            return TableGrant.None;
        }
    }
}
