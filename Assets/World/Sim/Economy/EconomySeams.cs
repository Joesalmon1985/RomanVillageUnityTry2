namespace World.Sim.Economy
{
    /// <summary>
    /// Economy operations the factions and eras modules call across the module seam (PRD_AGES 4.3, 4.4; added by H1-0).
    /// Sim code only. Each keeps the ware balance of REFACTOR_SPEC 4.4.
    /// </summary>
    internal static class EconomySeams
    {
        /// <summary>
        /// Campaign overrun of a home camp (4.3, kind Looted): the storehouse loses half of each ware (rounded down), never
        /// units already reserved for jobs. Counted as Consumed and Looted. Returns the units lost. REAL from H1-0.
        /// </summary>
        public static int LootSettlement(WorldState s, int settlementId)
        {
            var e = s?.Economy;
            var st = e == null ? null : EconomyLogic.GetSettlement(s, settlementId);
            if (st == null) return 0;
            int total = 0;
            for (int w = 0; w < Wares.Count; w++)
            {
                int lose = System.Math.Min(st.Stock[w] / 2, st.Stock[w] - st.Reserved[w]);
                if (lose <= 0) continue;
                st.Stock[w] -= lose;
                e.Ledger.Consumed[w] += lose;
                e.Ledger.Looted[w] += lose;
                total += lose;
            }
            return total;
        }

        /// <summary>
        /// Campaign overrun of a non-home camp (4.3, kind CampLost): the settlement and all its buildings and stock change
        /// owner to newFaction; its open jobs are cancelled with goods returned to the old owner's home where possible;
        /// its carriers change side. Returns true when done.
        /// H1-0 STUB: returns false and changes nothing (the caller then loots the defender's home instead). H1-2 implements it
        /// (it owns Sim/Economy); the H1 merge order (0, 2, 1) lands it before H1-1's CampOverrunTests are integrated.
        /// </summary>
        public static bool TransferSettlement(WorldState s, int settlementId, int newFaction, EventBuffer ev)
        {
            return false;
        }
    }
}
