using System.Collections.Generic;

namespace World.Sim.Historic
{
    /// <summary>Static data of one Historic building kind (HI-5). Ware arrays are indexed by ware id.</summary>
    public sealed class HistoricDef
    {
        public HistoricKind Kind;
        public string Name;
        public string Purpose;
        /// <summary>Hex terrains the building may stand on.</summary>
        public Terrain[] Terrains;
        public int[] Cost;
        /// <summary>Inputs per cycle (workshops: kiln, sheep farm); all zero for the gatherers.</summary>
        public int[] Inputs;
        public Ware Output;
        /// <summary>Output units per finished cycle (the woodcutter's per tree is <see cref="HistoricTuning.WoodPerTree"/>).</summary>
        public int OutputCount;
        public int WorkTicks;
        public int BuildTicks;
        /// <summary>Woodcutters and foresters work their own hex and its neighbours.</summary>
        public bool ReachesNeighbours;
    }

    /// <summary>The Historic building table (data; new kinds go at the end).</summary>
    public static class HistoricDefs
    {
        public const int Count = 7;

        private static readonly HistoricDef[] Table = Build();

        public static IReadOnlyList<HistoricDef> All => Table;

        public static HistoricDef Get(HistoricKind k) => (int)k >= 0 && (int)k < Count ? Table[(int)k] : null;

        private static int[] W(params (Ware w, int n)[] items)
        {
            var a = new int[Wares.Count];
            foreach (var (w, n) in items) a[(int)w] += n;
            return a;
        }

        private static HistoricDef[] Build()
        {
            var none = new int[Wares.Count];
            return new[]
            {
                new HistoricDef { Kind = HistoricKind.Woodcutter, Name = "Woodcutter's house", Purpose = "Fells grown trees for wood.",
                    Terrains = new[] { Terrain.Forest, Terrain.Meadow }, Cost = W((Ware.Wood, 3)), Inputs = none, Output = Ware.Wood,
                    OutputCount = 0, WorkTicks = 250, BuildTicks = 300, ReachesNeighbours = true },
                new HistoricDef { Kind = HistoricKind.Forester, Name = "Forester's house", Purpose = "Plants saplings where trees were felled.",
                    Terrains = new[] { Terrain.Forest, Terrain.Meadow }, Cost = W((Ware.Wood, 3)), Inputs = none, Output = Ware.Wood,
                    OutputCount = 0, WorkTicks = 150, BuildTicks = 300, ReachesNeighbours = true },
                new HistoricDef { Kind = HistoricKind.Quarry, Name = "Quarry", Purpose = "Breaks stone out of the rock until the rock is gone.",
                    Terrains = new[] { Terrain.Mountain, Terrain.Hills }, Cost = W((Ware.Wood, 4)), Inputs = none, Output = Ware.Stone,
                    OutputCount = 1, WorkTicks = 150, BuildTicks = 300 },
                new HistoricDef { Kind = HistoricKind.ClayPit, Name = "Clay pit", Purpose = "Digs clay out of the hills.",
                    Terrains = new[] { Terrain.Hills }, Cost = W((Ware.Wood, 3)), Inputs = none, Output = Ware.Clay,
                    OutputCount = 1, WorkTicks = 150, BuildTicks = 300 },
                new HistoricDef { Kind = HistoricKind.Farm, Name = "Farm", Purpose = "Sows and harvests the fields for grain.",
                    Terrains = new[] { Terrain.Fields }, Cost = W((Ware.Wood, 4)), Inputs = none, Output = Ware.Grain,
                    OutputCount = 0, WorkTicks = 100, BuildTicks = 300 },
                new HistoricDef { Kind = HistoricKind.SheepFarm, Name = "Sheep farm", Purpose = "Raises sheep on the pasture, fed with grain from a farm.",
                    Terrains = new[] { Terrain.Pasture, Terrain.Meadow }, Cost = W((Ware.Wood, 4)), Inputs = W((Ware.Grain, 1)), Output = Ware.Sheep,
                    OutputCount = 1, WorkTicks = 300, BuildTicks = 300 },
                new HistoricDef { Kind = HistoricKind.Kiln, Name = "Kiln", Purpose = "Fires clay with wood into bricks.",
                    Terrains = new[] { Terrain.Meadow, Terrain.Forest, Terrain.Hills, Terrain.Fields, Terrain.Mountain, Terrain.Pasture },
                    Cost = W((Ware.Wood, 4), (Ware.Stone, 2)), Inputs = W((Ware.Clay, 2), (Ware.Wood, 1)), Output = Ware.Bricks,
                    OutputCount = 2, WorkTicks = 300, BuildTicks = 300 },
            };
        }

        public static bool Allows(HistoricDef d, Terrain t)
        {
            foreach (var x in d.Terrains) if (x == t) return true;
            return false;
        }
    }
}
