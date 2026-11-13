namespace World.Sim.Economy
{
    /// <summary>
    /// Economy numbers (REFACTOR_SPEC 2.A, 2.B, 3.3). Saved with the state (WorldState.Economy.Tuning), so tests may
    /// change them per game. Ware arrays are indexed by ware id (length <see cref="Wares.Count"/>); GrowTicks by
    /// <see cref="Terrain"/> (length <see cref="TerrainInfo.Count"/>).
    /// </summary>
    public sealed class EconomyTuning
    {
        // Dice and hex stock.
        public int RollIntervalTicks = 100;
        public int FirstRollTick = 100;
        public int BatchSize = 4;
        public int HexCap = 12;
        public int StartMature = 4;
        public int[] GrowTicks = { 750, 750, 750, 750, 750, 750 };

        // Logistics.
        public int CarrierTicksPerStep = 15;
        public int ShipmentSize = 4;
        public int InputBuffer = 2;
        public int CollectorStore = 4;
        /// <summary>A job waiting this long is served before the serving order (2.B.1), oldest first, so no demand starves (0 = off).</summary>
        public int JobAgeTicks = 300;

        // Warriors.
        public int MealTicks = 900;
        public int MealRetryTicks = 100;
        public int WarriorsPerLodge = 3;

        /// <summary>A workshop does not start a cycle while its storehouse holds this much of its output (0 = no target).</summary>
        public int[] StockTarget = Arr((Ware.Planks, 6), (Ware.Bricks, 6), (Ware.Bread, 4), (Ware.Meat, 4), (Ware.CutStone, 4));

        /// <summary>Starting storehouse stock of each starting settlement.</summary>
        public int[] StartStock = Arr((Ware.Wood, 4), (Ware.Clay, 2), (Ware.Grain, 2), (Ware.Sheep, 2));

        public int[] RoadCost = Arr((Ware.Wood, 1), (Ware.Clay, 1));
        public int[] SettlementCost = Arr((Ware.Wood, 1), (Ware.Clay, 1), (Ware.Grain, 1), (Ware.Sheep, 1));
        public int[] CityCost = Arr((Ware.Planks, 2), (Ware.Bricks, 2), (Ware.CutStone, 2), (Ware.Bread, 2));
        public int[] WarriorCost = Arr((Ware.Bread, 1), (Ware.Meat, 1));

        public int RoadBuildTicks = 100;
        public int SettlementBuildTicks = 300;
        public int CityBuildTicks = 400;

        public int StartCarriers = 3;
        public int NewSettlementCarriers = 2;
        public int CarriersPerBuilding = 1;
        public int CarriersPerCity = 1;

        /// <summary>
        /// The values a new game uses: the spec's starting values (the field initialisers, kept as written in
        /// REFACTOR_SPEC) with the changes S made in tuning (PROGRESS.md, tuning log): carriers walk 5 m in 10 ticks,
        /// 5 carriers at a starting camp and 3 at a new one, 4 sheep in the starting stock.
        /// </summary>
        public static EconomyTuning Tuned()
        {
            var t = new EconomyTuning();
            t.CarrierTicksPerStep = 10;
            t.StartCarriers = 5;
            t.NewSettlementCarriers = 3;
            t.StartStock[(int)Ware.Sheep] = 4;
            return t;
        }

        /// <summary>Growth time of a terrain's stock (750 when the table is short).</summary>
        public int GrowFor(Terrain t) => GrowTicks != null && (int)t >= 0 && (int)t < GrowTicks.Length ? GrowTicks[(int)t] : 750;

        private static int[] Arr(params (Ware w, int n)[] items)
        {
            var a = new int[Wares.Count];
            foreach (var (w, n) in items) a[(int)w] = n;
            return a;
        }
    }
}
