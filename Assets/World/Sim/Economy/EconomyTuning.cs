namespace World.Sim.Economy
{
    /// <summary>
    /// Economy tuning with defaults in code. Lives in <c>EconomyState.Tuning</c> (serialised with the
    /// state) so tests and later JSON overrides can change it. Ware arrays are indexed by (int)Ware.
    /// </summary>
    public sealed class EconomyTuning
    {
        /// <summary>Ticks a carrier needs to walk one road edge (10 ticks = 1 s).</summary>
        public int TicksPerEdge = 60;
        /// <summary>Carriers created at the storehouse at start.</summary>
        public int Carriers = 4;
        /// <summary>Raw units per authorised batch before modifiers and the forester rule.</summary>
        public int BatchSize = 6;
        /// <summary>Unharvested batch units carry over between rounds up to this cap per hex.</summary>
        public int BatchCap = 12;
        /// <summary>Distinct production numbers drawn per round (from 1..4).</summary>
        public int NumbersDrawn = 2;
        /// <summary>Territory = vertices within this many edges of one of the player's cities.</summary>
        public int TerritoryRadius = 3;
        /// <summary>Recipe buildings keep this many cycles of input on hand or on the way.</summary>
        public int InputBuffer = 3;
        /// <summary>
        /// A job still waiting for pickup that has had no route for this many ticks is released (reservation
        /// freed so the wares can be spent, e.g. on the road that fixes it); demand re-creates it later.
        /// </summary>
        public int ReleaseStalledJobTicks = 150;
        /// <summary>A producer whose output feeds a local recipe building keeps up to this many units instead of sending them to the storehouse.</summary>
        public int HoldForConsumer = 4;
        /// <summary>Power plant keeps this many timber on hand or on the way.</summary>
        public int FuelBuffer = 2;
        /// <summary>Ticks of one harvest cycle (one raw unit).</summary>
        public int HarvestTicks = 30;
        /// <summary>Cost of one road edge, taken from the storehouse.</summary>
        public int[] RoadCost = EconomyTuning.W(Ware.Planks, 1);
        /// <summary>Storehouse stock at start (one Planks of it is already on a carrier at tick 0).</summary>
        public int[] StarterStock = EconomyTuning.W(Ware.Planks, 14, Ware.Stone, 4);
        /// <summary>Building table, index = (int)BuildingType. Array (not list) so JSON replaces it.</summary>
        public BuildingDef[] Buildings = BuildingDefs.CreateDefault();

        public BuildingDef Def(BuildingType t) => Buildings[(int)t];

        /// <summary>Ware array from (ware, count) pairs: W(Ware.Planks, 2, Ware.Stone, 1).</summary>
        public static int[] W(params object[] pairs)
        {
            var a = new int[Wares.Count];
            for (int i = 0; i + 1 < pairs.Length; i += 2) a[(int)(Ware)pairs[i]] += (int)pairs[i + 1];
            return a;
        }
    }

    /// <summary>One row of the building table.</summary>
    public sealed class BuildingDef
    {
        public BuildingType Type;
        public string Name;
        public BuildingKind Kind;
        /// <summary>Construction cost per ware.</summary>
        public int[] Cost = new int[Wares.Count];
        /// <summary>When true the vertex must touch a hex of <see cref="Terrain"/>.</summary>
        public bool NeedsTerrain;
        public Terrain Terrain;
        /// <summary>Recipe inputs per cycle (power plant: fuel ware).</summary>
        public int[] Inputs = new int[Wares.Count];
        /// <summary>Recipe outputs per cycle (harvester: its raw ware). Pushed out by carriers.</summary>
        public int[] Outputs = new int[Wares.Count];
        /// <summary>Ticks per recipe cycle (harvesters use Tuning.HarvestTicks).</summary>
        public int WorkTicks;
        /// <summary>Output stock limit; at or above it the building reports output_full.</summary>
        public int Capacity = 6;
        public bool NeedsPower;
        public int BuildTicks = 50;
        /// <summary>Power plant: ticks one fuel unit lasts.</summary>
        public int FuelTicks;
        public bool Placeable = true;
    }

    /// <summary>The default building table (original names and numbers).</summary>
    public static class BuildingDefs
    {
        public static BuildingDef[] CreateDefault()
        {
            var t = new BuildingDef[14];
            t[0] = Def(BuildingType.Storehouse, "Storehouse", BuildingKind.Store, EconomyTuning.W());
            t[0].Placeable = false;
            t[0].Capacity = int.MaxValue;
            t[1] = Def(BuildingType.Forester, "Forester", BuildingKind.Forester, EconomyTuning.W(Ware.Planks, 2));
            Terr(t[1], Terrain.Forest);
            t[2] = Def(BuildingType.Woodcutter, "Woodcutter", BuildingKind.Harvester, EconomyTuning.W(Ware.Planks, 2));
            Terr(t[2], Terrain.Forest);
            t[2].Outputs = EconomyTuning.W(Ware.Timber, 1);
            t[3] = Def(BuildingType.Sawmill, "Sawmill", BuildingKind.Producer, EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 1));
            Recipe(t[3], Ware.Timber, Ware.Planks, 30);
            t[4] = Def(BuildingType.Quarry, "Quarry", BuildingKind.Harvester, EconomyTuning.W(Ware.Planks, 2));
            Terr(t[4], Terrain.Hills);
            t[4].Outputs = EconomyTuning.W(Ware.Stone, 1);
            t[5] = Def(BuildingType.Farm, "Farm", BuildingKind.Harvester, EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 1));
            Terr(t[5], Terrain.Fields);
            t[5].Outputs = EconomyTuning.W(Ware.Grain, 1);
            t[6] = Def(BuildingType.GrainMill, "Grain mill", BuildingKind.Producer, EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 2));
            Recipe(t[6], Ware.Grain, Ware.Flour, 40);
            t[7] = Def(BuildingType.Bakery, "Bakery", BuildingKind.Producer, EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 2));
            Recipe(t[7], Ware.Flour, Ware.Rations, 40);
            t[8] = Def(BuildingType.OrePit, "Ore pit", BuildingKind.Harvester, EconomyTuning.W(Ware.Planks, 3, Ware.Stone, 1));
            Terr(t[8], Terrain.Mountain);
            t[8].Outputs = EconomyTuning.W(Ware.Ore, 1);
            t[9] = Def(BuildingType.Refinery, "Refinery", BuildingKind.Producer, EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 3));
            Recipe(t[9], Ware.Ore, Ware.Ingots, 60);
            t[9].NeedsPower = true;
            t[10] = Def(BuildingType.PowerPlant, "Power plant", BuildingKind.PowerPlant, EconomyTuning.W(Ware.Planks, 3, Ware.Stone, 2));
            t[10].Inputs = EconomyTuning.W(Ware.Timber, 1);
            t[10].FuelTicks = 300;
            t[11] = Def(BuildingType.Barracks, "Barracks", BuildingKind.Plain, EconomyTuning.W(Ware.Planks, 4, Ware.Stone, 3));
            t[11].NeedsPower = true;
            t[12] = Def(BuildingType.Market, "Market", BuildingKind.Plain, EconomyTuning.W(Ware.Planks, 3, Ware.Stone, 2));
            t[13] = Def(BuildingType.WonderSite, "Wonder site", BuildingKind.Plain, EconomyTuning.W(Ware.Planks, 2, Ware.Stone, 2));
            return t;
        }

        private static BuildingDef Def(BuildingType type, string name, BuildingKind kind, int[] cost) =>
            new BuildingDef { Type = type, Name = name, Kind = kind, Cost = cost };

        private static void Terr(BuildingDef d, Terrain t)
        {
            d.NeedsTerrain = true;
            d.Terrain = t;
        }

        private static void Recipe(BuildingDef d, Ware input, Ware output, int ticks)
        {
            d.Inputs = EconomyTuning.W(input, 1);
            d.Outputs = EconomyTuning.W(output, 1);
            d.WorkTicks = ticks;
        }
    }
}
