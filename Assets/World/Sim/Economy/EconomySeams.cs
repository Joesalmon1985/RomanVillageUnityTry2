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
        /// owner to newFaction. Identity (ids, plots, grades, an upgrade under way) and inventories (storehouse, building
        /// stock, delivered goods) are kept; no ware is created or lost, so the balance of 4.4 holds. Open jobs that are still
        /// valid (carriers of the camp serving the camp and its buildings) go on; the others end: goods not yet picked up
        /// stay where they are (the reservation is released), goods already on a carrier are brought to the carrier's own
        /// storehouse (the new owner's for the camp's carriers, the old owner's for the others). Carriers and warriors of
        /// the camp change side. Road and camp sites of the old owner that drew from this camp get a new source (or none).
        /// Raises SettlementTransferred. Returns false (nothing changed) for an unknown, unfinished or already owned camp
        /// or a faction that is not a people.
        /// </summary>
        public static bool TransferSettlement(WorldState s, int settlementId, int newFaction, EventBuffer ev)
        {
            var e = s?.Economy;
            var st = e == null ? null : EconomyLogic.GetSettlement(s, settlementId);
            if (st == null || st.State != SettlementState.Active || !EconomyLogic.ValidFaction(s, newFaction) || st.Faction == newFaction) return false;
            int old = st.Faction;

            // Jobs first, while ownership is still the old one.
            foreach (var j in new System.Collections.Generic.List<Job>(e.Jobs))
            {
                bool fromCamp = j.Settlement == st.Id;
                bool toCamp = InCamp(s, st, j.ToKind, j.ToId);
                if (!fromCamp && !toCamp) continue;
                if (fromCamp && toCamp) { j.Faction = newFaction; continue; } // still valid: the camp serving itself
                if (j.State == JobState.Carrying)
                {
                    // Goods on a carrier: bring them to the carrier's own storehouse, as when a site is cancelled.
                    var inc = Logistics.IncomingArray(s, j.ToKind, j.ToId);
                    if (inc != null) inc[(int)j.Ware] -= j.Count;
                    var c = e.Carriers[j.CarrierId];
                    j.ToKind = PlaceKind.Storehouse;
                    j.ToId = c.Home;
                    e.Settlements[c.Home].Incoming[(int)j.Ware] += j.Count;
                    if (fromCamp) j.Faction = newFaction;
                }
                else Logistics.Release(s, j);
            }
            st.Faction = newFaction;
            foreach (var b in e.Buildings)
                if (b.Settlement == st.Id) b.Faction = newFaction;
            foreach (var c in e.Carriers)
                if (c.Home == st.Id) c.Faction = newFaction;
            foreach (var w in e.Warriors)
                if (w.Settlement == st.Id) w.Faction = newFaction;
            foreach (var r in e.Roads)
                if (r.State == RoadState.Site && r.SourceSettlement == st.Id)
                    r.SourceSettlement = EconomyLogic.NearestConnectedSettlement(s, r.Faction, new[] { s.Board.Topology.HexEdgeCornerA(r.HexEdge), s.Board.Topology.HexEdgeCornerB(r.HexEdge) });
            foreach (var o in e.Settlements)
                if (o.State == SettlementState.Site && o.SourceSettlement == st.Id)
                    o.SourceSettlement = EconomyLogic.NearestConnectedSettlement(s, o.Faction, new[] { o.Corner });
            ev?.Add(new SettlementTransferred { SettlementId = st.Id, Corner = st.Corner, FromFaction = old, ToFaction = newFaction });
            return true;
        }

        private static bool InCamp(WorldState s, Settlement st, PlaceKind kind, int id)
        {
            switch (kind)
            {
                case PlaceKind.Storehouse: return id == st.Id;
                case PlaceKind.Building:
                case PlaceKind.BuildingUpgrade:
                {
                    var b = EconomyLogic.GetBuilding(s, id);
                    return b != null && b.Settlement == st.Id;
                }
            }
            return false;
        }
    }
}
