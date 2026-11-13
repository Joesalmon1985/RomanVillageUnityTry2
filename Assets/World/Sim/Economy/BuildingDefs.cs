using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>Building types (REFACTOR_SPEC 3.2). Ids are binding.</summary>
    public enum BuildingType
    {
        LogPile = 0,
        ClayScrape = 1,
        GrainBasket = 2,
        ThornFold = 3,
        RockHeap = 4,
        WoodcutterHut = 5,
        ClayPit = 6,
        Farm = 7,
        ShepherdHut = 8,
        Quarry = 9,
        Sawmill = 10,
        Kiln = 11,
        Bakery = 12,
        Smokehouse = 13,
        Stonecutter = 14,
        WarriorLodge = 15,
    }

    public enum BuildingClass
    {
        /// <summary>Comes with a settlement, one per resource sector; not placeable.</summary>
        PrimitiveCollector,
        /// <summary>Doubles the yield; retires the primitive collector of its hex when finished.</summary>
        ImprovedCollector,
        /// <summary>Recipe: inputs from the storehouse to outputs.</summary>
        Workshop,
        /// <summary>Warrior lodge: trains warriors from Bread and Meat.</summary>
        Lodge,
    }

    /// <summary>Static data of one building type. Arrays are indexed by ware id (length <see cref="Wares.Count"/>).</summary>
    public sealed class BuildingDef
    {
        public BuildingType Type;
        public string Name;
        /// <summary>One sentence: what it is for (the brain's reasons reuse it).</summary>
        public string Purpose;
        public BuildingClass Class;
        /// <summary>Terrain a collector collects from (Meadow for workshops and the lodge).</summary>
        public Terrain Terrain;
        /// <summary>Construction cost per ware.</summary>
        public int[] Cost;
        /// <summary>Inputs per cycle per ware (workshops, lodge). Collectors take 1 mature hex unit instead.</summary>
        public int[] Inputs;
        /// <summary>Outputs per cycle per ware (collectors: Yield of their raw ware). The lodge outputs a warrior.</summary>
        public int[] Outputs;
        /// <summary>Collectors: wares per mature unit taken (1 primitive, 2 improved). 0 otherwise.</summary>
        public int Yield;
        public int WorkTicks;
        public int BuildTicks;
        public bool Placeable;

        public bool IsCollector => Class == BuildingClass.PrimitiveCollector || Class == BuildingClass.ImprovedCollector;
    }

    /// <summary>The building table (REFACTOR_SPEC 3.2, 3.3).</summary>
    public static class BuildingDefs
    {
        public const int Count = 16;

        private static readonly BuildingDef[] Table = Build();

        public static IReadOnlyList<BuildingDef> All => Table;

        /// <summary>The def of a type, or null for an id out of range.</summary>
        public static BuildingDef Get(BuildingType t) => (int)t >= 0 && (int)t < Count ? Table[(int)t] : null;

        public static bool IsValid(BuildingType t) => (int)t >= 0 && (int)t < Count;

        /// <summary>The primitive collector of a terrain; (BuildingType)(-1) for Meadow.</summary>
        public static BuildingType PrimitiveFor(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return BuildingType.LogPile;
                case Terrain.Hills: return BuildingType.ClayScrape;
                case Terrain.Fields: return BuildingType.GrainBasket;
                case Terrain.Pasture: return BuildingType.ThornFold;
                case Terrain.Mountain: return BuildingType.RockHeap;
                default: return (BuildingType)(-1);
            }
        }

        /// <summary>The improved collector of a terrain; (BuildingType)(-1) for Meadow.</summary>
        public static BuildingType ImprovedFor(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return BuildingType.WoodcutterHut;
                case Terrain.Hills: return BuildingType.ClayPit;
                case Terrain.Fields: return BuildingType.Farm;
                case Terrain.Pasture: return BuildingType.ShepherdHut;
                case Terrain.Mountain: return BuildingType.Quarry;
                default: return (BuildingType)(-1);
            }
        }

        private static int[] W(params (Ware w, int n)[] items)
        {
            var a = new int[Wares.Count];
            foreach (var (w, n) in items) a[(int)w] += n;
            return a;
        }

        private static BuildingDef Collector(BuildingType t, string name, Terrain terrain, bool improved, string purpose, int[] cost)
        {
            var ware = TerrainInfo.RawWare(terrain).Value;
            int yield = improved ? 2 : 1;
            return new BuildingDef
            {
                Type = t, Name = name, Purpose = purpose,
                Class = improved ? BuildingClass.ImprovedCollector : BuildingClass.PrimitiveCollector,
                Terrain = terrain, Cost = cost ?? new int[Wares.Count], Inputs = new int[Wares.Count],
                Outputs = W((ware, yield)), Yield = yield, WorkTicks = improved ? 60 : 100,
                BuildTicks = improved ? 150 : 0, Placeable = improved,
            };
        }

        private static BuildingDef Workshop(BuildingType t, string name, string purpose, int[] cost, int[] inputs, int[] outputs, int ticks,
            BuildingClass cls = BuildingClass.Workshop) => new BuildingDef
            {
                Type = t, Name = name, Purpose = purpose, Class = cls, Terrain = Terrain.Meadow,
                Cost = cost, Inputs = inputs, Outputs = outputs, Yield = 0, WorkTicks = ticks, BuildTicks = 150, Placeable = true,
            };

        private static BuildingDef[] Build()
        {
            var tier1Cheap = W((Ware.Wood, 2));
            var tier1 = W((Ware.Wood, 2), (Ware.Clay, 1));
            var tier2 = W((Ware.Planks, 2), (Ware.Bricks, 1));
            return new[]
            {
                Collector(BuildingType.LogPile, "Log pile", Terrain.Forest, false, "The log pile gathers fallen wood from the forest.", null),
                Collector(BuildingType.ClayScrape, "Clay scrape", Terrain.Hills, false, "The clay scrape digs clay out of the hills.", null),
                Collector(BuildingType.GrainBasket, "Gleaners' basket", Terrain.Fields, false, "The gleaners gather grain from the fields.", null),
                Collector(BuildingType.ThornFold, "Thorn fold", Terrain.Pasture, false, "The thorn fold pens sheep from the pasture.", null),
                Collector(BuildingType.RockHeap, "Rock heap", Terrain.Mountain, false, "The rock heap breaks stone from the mountain.", null),
                Collector(BuildingType.WoodcutterHut, "Woodcutter's hut", Terrain.Forest, true,
                    "A woodcutter's hut brings in twice the wood of the log pile, and wood builds everything.", tier1Cheap),
                Collector(BuildingType.ClayPit, "Clay pit", Terrain.Hills, true,
                    "A clay pit brings in twice the clay of the scrape, for roads, camps and bricks.", W((Ware.Wood, 2))),
                Collector(BuildingType.Farm, "Farm", Terrain.Fields, true,
                    "A farm brings in twice the grain of the gleaners, for new camps and bread.", tier1),
                Collector(BuildingType.ShepherdHut, "Shepherd's hut", Terrain.Pasture, true,
                    "A shepherd's hut brings in twice the sheep of the thorn fold, for new camps and meat.", tier1),
                Collector(BuildingType.Quarry, "Quarry", Terrain.Mountain, true,
                    "A quarry brings in twice the stone of the rock heap, for cut stone.", tier1),
                Workshop(BuildingType.Sawmill, "Sawmill", "A sawmill turns wood into planks for bigger buildings and a city.",
                    tier1, W((Ware.Wood, 1)), W((Ware.Planks, 1)), 100),
                Workshop(BuildingType.Kiln, "Kiln", "A kiln fires clay and wood into bricks for bigger buildings and a city.",
                    W((Ware.Wood, 1), (Ware.Clay, 2)), W((Ware.Clay, 2), (Ware.Wood, 1)), W((Ware.Bricks, 2)), 150),
                Workshop(BuildingType.Bakery, "Bakery", "A bakery turns grain into bread to feed warriors and raise a city.",
                    tier2, W((Ware.Grain, 1)), W((Ware.Bread, 1)), 100),
                Workshop(BuildingType.Smokehouse, "Smokehouse", "A smokehouse turns sheep and wood into meat for warriors.",
                    tier2, W((Ware.Sheep, 1), (Ware.Wood, 1)), W((Ware.Meat, 2)), 150),
                Workshop(BuildingType.Stonecutter, "Stonecutter", "A stonecutter shapes stone into cut stone to raise a city.",
                    tier2, W((Ware.Stone, 1)), W((Ware.CutStone, 1)), 150),
                Workshop(BuildingType.WarriorLodge, "Warrior lodge", "A warrior lodge trains warriors from bread and meat to make us stronger.",
                    W((Ware.Planks, 2), (Ware.Bricks, 2)), W((Ware.Bread, 1), (Ware.Meat, 1)), new int[Wares.Count], 300, BuildingClass.Lodge),
            };
        }
    }
}
