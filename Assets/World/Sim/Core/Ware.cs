namespace World.Sim
{
    /// <summary>
    /// Carried goods (REFACTOR_SPEC 3.1). Ids 0-4 are the raw goods, one per resource terrain (collected from hex
    /// stock); 5-9 are worked goods made in workshops. Id 10 is reserved for Ore (a later age). Coin is NOT a
    /// ware: it is <see cref="PlayerState.Coin"/> (unused).
    /// </summary>
    public enum Ware
    {
        // Raw (collected from hex stock).
        Wood = 0,
        Clay = 1,
        Grain = 2,
        Sheep = 3,
        Stone = 4,
        // Worked.
        Planks = 5,
        Bricks = 6,
        Bread = 7,
        Meat = 8,
        CutStone = 9,
        // 10 = Ore, reserved for a later age.
    }

    public static class Wares
    {
        public const int Count = 10;

        /// <summary>Number of raw wares (ids 0..RawCount-1).</summary>
        public const int RawCount = 5;

        public static readonly Ware[] All =
        {
            Ware.Wood, Ware.Clay, Ware.Grain, Ware.Sheep, Ware.Stone,
            Ware.Planks, Ware.Bricks, Ware.Bread, Ware.Meat, Ware.CutStone,
        };

        public static readonly Ware[] Raw = { Ware.Wood, Ware.Clay, Ware.Grain, Ware.Sheep, Ware.Stone };

        public static bool IsRaw(Ware w) => (int)w >= 0 && (int)w < RawCount;

        /// <summary>Lower-case name used inside sentences ("wood", "cut stone").</summary>
        public static string DisplayName(Ware w) => w == Ware.CutStone ? "cut stone" : w.ToString().ToLowerInvariant();

        /// <summary>Capitalised name for labels and headings ("Wood", "Cut stone").</summary>
        public static string Title(Ware w) => w == Ware.CutStone ? "Cut stone" : w.ToString();
    }
}
