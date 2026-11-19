using System;

namespace World.Sim.Historic
{
    /// <summary>Read-only queries for views and proofs.</summary>
    public static class HistoricApi
    {
        public static bool Active(WorldState s) => s?.Historic != null;

        /// <summary>Castle stages raised by a people (0 to 3).</summary>
        public static int CastleStages(WorldState s, int p) =>
            s?.Historic != null && p >= 0 && p < s.Historic.Peoples.Count ? s.Historic.Peoples[p].Castle.StagesDone : 0;

        /// <summary>HI-6 measure: the longest stretch (ticks) in which no wood reached the people's store, the open stretch included.</summary>
        public static long LongestWoodGap(WorldState s, int p)
        {
            var hp = s.Historic.Peoples[p];
            return Math.Max(hp.LongestWoodGap, s.Tick - hp.LastWoodTick);
        }

        /// <summary>The longest stretch the people's store held no wood, the open stretch included.</summary>
        public static long LongestWoodEmpty(WorldState s, int p)
        {
            var hp = s.Historic.Peoples[p];
            return hp.WoodEmptySince >= 0 ? Math.Max(hp.LongestWoodEmpty, s.Tick - hp.WoodEmptySince) : hp.LongestWoodEmpty;
        }

        /// <summary>Trees of a stage on a hex.</summary>
        public static int Trees(WorldState s, int hex, TreeStage stage)
        {
            int n = 0;
            foreach (int t in s.Historic.Hexes[hex].Trees) if (t == (int)stage) n++;
            return n;
        }

        /// <summary>Trees of a stage on the whole island.</summary>
        public static int Trees(WorldState s, TreeStage stage)
        {
            int n = 0;
            for (int h = 0; h < s.Historic.Hexes.Length; h++) n += Trees(s, h, stage);
            return n;
        }

        /// <summary>
        /// Ware balance per ware: Starting + Produced - Consumed - stores - outputs - workshop inputs on hand - carried.
        /// Zero for every ware when the books balance.
        /// </summary>
        public static int[] Imbalance(WorldState s)
        {
            var h = s.Historic;
            var r = new int[Wares.Count];
            for (int w = 0; w < Wares.Count; w++) r[w] = h.Ledger.Starting[w] + h.Ledger.Produced[w] - h.Ledger.Consumed[w];
            foreach (var hp in h.Peoples) for (int w = 0; w < Wares.Count; w++) r[w] -= hp.Stock[w];
            foreach (var b in h.Buildings)
            {
                r[(int)HistoricDefs.Get(b.Kind).Output] -= b.Output;
                if (b.State != HistoricBuildingState.Site) for (int w = 0; w < Wares.Count; w++) r[w] -= b.Delivered[w];
            }
            foreach (var j in h.Jobs) r[(int)j.Ware]--;
            return r;
        }
    }
}
