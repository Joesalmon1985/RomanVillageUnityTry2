namespace World.Sim
{
    /// <summary>Carried goods. Coin is NOT a ware: it is <see cref="PlayerState.Coin"/>.</summary>
    public enum Ware
    {
        Timber = 0,
        Planks = 1,
        Stone = 2,
        Grain = 3,
        Flour = 4,
        Rations = 5,
        Ore = 6,
        Ingots = 7,
    }

    public static class Wares
    {
        public const int Count = 8;

        public static readonly Ware[] All =
        {
            Ware.Timber, Ware.Planks, Ware.Stone, Ware.Grain,
            Ware.Flour, Ware.Rations, Ware.Ore, Ware.Ingots,
        };

        public static string DisplayName(Ware w) => w.ToString().ToLowerInvariant();
    }
}
