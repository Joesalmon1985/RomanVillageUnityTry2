namespace World.Sim
{
    /// <summary>
    /// Carried goods (ECONOMY_DESIGN section 3). The first five are the raw resources, one per resource
    /// terrain; the rest are made from them. Coin is NOT a ware: it is <see cref="PlayerState.Coin"/>.
    /// </summary>
    public enum Ware
    {
        // Raw (harvested from authorised batches).
        Logs = 0,
        Clay = 1,
        Wheat = 2,
        Sheep = 3,
        Ore = 4,
        // Processed.
        Planks = 5,
        Coal = 6,
        Bricks = 7,
        Flour = 8,
        Bread = 9,
        Meat = 10,
        Rations = 11,
        Iron = 12,
        Weapons = 13,
    }

    public static class Wares
    {
        public const int Count = 14;

        /// <summary>Number of raw wares (ids 0..RawCount-1).</summary>
        public const int RawCount = 5;

        public static readonly Ware[] All =
        {
            Ware.Logs, Ware.Clay, Ware.Wheat, Ware.Sheep, Ware.Ore,
            Ware.Planks, Ware.Coal, Ware.Bricks, Ware.Flour, Ware.Bread, Ware.Meat, Ware.Rations, Ware.Iron, Ware.Weapons,
        };

        public static readonly Ware[] Raw = { Ware.Logs, Ware.Clay, Ware.Wheat, Ware.Sheep, Ware.Ore };

        public static bool IsRaw(Ware w) => (int)w < RawCount;

        /// <summary>Lower-case name used inside sentences ("logs", "bricks").</summary>
        public static string DisplayName(Ware w) => w.ToString().ToLowerInvariant();

        /// <summary>Capitalised name for labels and headings ("Logs", "Bricks").</summary>
        public static string Title(Ware w) => w.ToString();
    }
}
