namespace World.Sim.Economy
{
    /// <summary>
    /// Economy tuning with defaults in code. Lives in <c>EconomyState.Tuning</c> (serialised with the
    /// state) so tests and later JSON overrides can change it. Ware arrays are indexed by (int)Ware.
    /// Numbers marked "tuned" were set by the headless balance runs (Docs/ECONOMY_PROOF.md).
    /// </summary>
    public sealed class EconomyTuning
    {
        /// <summary>Ticks a carrier needs to walk one road segment (20 m; 10 ticks = 1 s).</summary>
        public int TicksPerEdge = 30;
        /// <summary>A carrier that delivered waits at that flag this long for a new job before walking back to the storehouse.</summary>
        public int CarrierWaitTicks = 150;
        /// <summary>Carriers created at the storehouse at start.</summary>
        public int Carriers = 8;
        /// <summary>Carriers that join at the storehouse whenever a building is finished (the village grows with its economy).</summary>
        public int CarriersPerBuilding = 2;
        /// <summary>Raw units per authorised batch before modifiers and the forester rule.</summary>
        public int BatchSize = 12;
        /// <summary>Unharvested batch units carry over between rounds up to this cap per hex.</summary>
        public int BatchCap = 24;
        /// <summary>Distinct production numbers drawn per round (from 1..4).</summary>
        public int NumbersDrawn = 3;
        /// <summary>Prologue starter sequence (home forest every round, hills and fields in turn) lasts until the commission, at most this many rounds.</summary>
        public int StarterRounds = 8;
        /// <summary>Territory = flags within this many segments (walking the fine graph, roads not needed) of one of the player's city corners. 4 = 80 m.</summary>
        public int TerritorySegments = 4;
        /// <summary>Recipe buildings keep this many cycles of input on hand or on the way.</summary>
        public int InputBuffer = 3;
        /// <summary>Harvesters with an upkeep (the Mine) keep this many units of it on hand or on the way.</summary>
        public int UpkeepBuffer = 2;
        /// <summary>
        /// A job still waiting for pickup that has had no route for this many ticks is released (reservation
        /// freed so the wares can be spent, e.g. on the road that fixes it); demand re-creates it later.
        /// </summary>
        public int ReleaseStalledJobTicks = 150;
        /// <summary>A producer whose output feeds a local recipe building keeps up to this many units instead of sending them to the storehouse.</summary>
        public int HoldForConsumer = 4;
        /// <summary>Power plant keeps this many coal on hand or on the way.</summary>
        public int FuelBuffer = 2;
        /// <summary>Ticks of one harvest cycle (one raw unit).</summary>
        public int HarvestTicks = 80;
        /// <summary>Cost taken from the storehouse for every <see cref="RoadSegmentsPerCost"/> road segments placed (design: 1 Planks + 1 Bricks).</summary>
        public int[] RoadCost = EconomyTuning.W(Ware.Planks, 1, Ware.Bricks, 1);
        /// <summary>
        /// Road segments paid for by one <see cref="RoadCost"/>: the first segment pays, the next ones use the
        /// credit (<see cref="EconomyState.RoadCredit"/>). 3 = one hex edge (60 m) per payment.
        /// </summary>
        public int RoadSegmentsPerCost = 3;
        /// <summary>
        /// Storehouse stock at start (one Planks of it is already on a carrier at tick 0). Tuned: enough Planks and
        /// Bricks to place the six commission buildings and their roads before any brick is made, a little Coal so the
        /// brick kiln can start before the charcoal kiln, and Rations for the first city.
        /// </summary>
        public int[] StarterStock = EconomyTuning.W(Ware.Planks, 16, Ware.Bricks, 6, Ware.Coal, 2, Ware.Rations, 4);
        /// <summary>
        /// Construction cost per size class (index = (int)BuildingSize). The building table copies these at creation;
        /// after changing them call <see cref="ApplySizeCosts"/>.
        /// </summary>
        public int[][] SizeCost = BuildingDefs.DefaultSizeCosts();
        /// <summary>Commission: planks and bricks in the storehouse (with the six commission buildings active and connected).</summary>
        public int CommissionPlanks = 10;
        public int CommissionBricks = 10;
        /// <summary>Building table, index = (int)BuildingType. Array (not list) so JSON replaces it.</summary>
        public BuildingDef[] Buildings = BuildingDefs.CreateDefault();

        public BuildingDef Def(BuildingType t) => Buildings[(int)t];

        /// <summary>Rewrite every placeable building's cost from <see cref="SizeCost"/>.</summary>
        public void ApplySizeCosts()
        {
            foreach (var d in Buildings) if (d.Placeable) d.Cost = (int[])SizeCost[(int)d.Size].Clone();
        }

        /// <summary>Ware array from (ware, count) pairs: W(Ware.Planks, 2, Ware.Bricks, 1).</summary>
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
        public BuildingSize Size;
        /// <summary>Construction cost per ware (from the size class).</summary>
        public int[] Cost = new int[Wares.Count];
        /// <summary>When true the flag must touch a hex of <see cref="Terrain"/> (BoardTopology.VertexHexes).</summary>
        public bool NeedsTerrain;
        public Terrain Terrain;
        /// <summary>
        /// Recipe inputs per cycle (power plant: fuel per burn; harvester: upkeep paid once every
        /// <see cref="CyclesPerInput"/> cycles, e.g. the Mine's Rations).
        /// </summary>
        public int[] Inputs = new int[Wares.Count];
        /// <summary>Harvesters with inputs: harvest cycles one payment of Inputs covers (Mine: 1 Rations per 2 Ore).</summary>
        public int CyclesPerInput = 1;
        /// <summary>Recipe outputs per cycle (harvester: its raw ware). Pushed out by carriers.</summary>
        public int[] Outputs = new int[Wares.Count];
        /// <summary>Ticks per recipe cycle (harvesters use Tuning.HarvestTicks).</summary>
        public int WorkTicks;
        /// <summary>Output stock limit; at or above it the building reports output_full.</summary>
        public int Capacity = 8;
        public bool NeedsPower;
        public int BuildTicks = 100;
        /// <summary>Power plant: ticks one fuel unit lasts.</summary>
        public int FuelTicks;
        public bool Placeable = true;
        /// <summary>One line for the build menu: what the building is for.</summary>
        public string Purpose;
    }

    /// <summary>The default building table (original names and numbers).</summary>
    public static class BuildingDefs
    {
        /// <summary>Number of building types (rows of the table).</summary>
        public const int Count = 20;

        /// <summary>Planks and Bricks by size (tuned): Small 1 P; Medium 1 P + 1 B; Large 2 P + 1 B; Huge 2 P + 2 B.</summary>
        public static int[][] DefaultSizeCosts() => new[]
        {
            EconomyTuning.W(Ware.Planks, 1),
            EconomyTuning.W(Ware.Planks, 1, Ware.Bricks, 1),
            EconomyTuning.W(Ware.Planks, 2, Ware.Bricks, 1),
            EconomyTuning.W(Ware.Planks, 2, Ware.Bricks, 2),
        };

        public static BuildingDef[] CreateDefault()
        {
            var size = DefaultSizeCosts();
            var t = new BuildingDef[Count];
            t[0] = Def(BuildingType.Storehouse, "Storehouse", BuildingKind.Store, BuildingSize.Huge, "Holds the village's goods; carriers start and end here.");
            t[0].Cost = new int[Wares.Count];
            t[0].Placeable = false;
            t[0].Capacity = int.MaxValue;

            t[1] = Def(BuildingType.Forester, "Forester", BuildingKind.Forester, BuildingSize.Small, "Plants trees: forest batches it touches are not halved.");
            Terr(t[1], Terrain.Forest);
            t[2] = Harvester(BuildingType.Woodcutter, "Woodcutter", Terrain.Forest, "Fells the forest's batch into logs (2 logs per batch unit).");
            t[2].Outputs = EconomyTuning.W(Ware.Logs, 2); // tuned: logs feed both planks and coal, so a forest unit yields 2
            t[3] = Recipe(BuildingType.Sawmill, "Sawmill", BuildingSize.Medium, 30, "Saws logs into planks.", Ware.Planks, 1, Ware.Logs, 1);
            t[4] = Harvester(BuildingType.ClayPit, "Clay pit", Terrain.Hills, "Digs the hills' batch into clay.");
            t[5] = Harvester(BuildingType.Farm, "Farm", Terrain.Fields, "Reaps the fields' batch into wheat.");
            t[6] = Recipe(BuildingType.Mill, "Mill", BuildingSize.Medium, 30, "Grinds wheat into flour.", Ware.Flour, 1, Ware.Wheat, 1);
            t[7] = Recipe(BuildingType.Bakery, "Bakery", BuildingSize.Medium, 40, "Bakes flour into bread.", Ware.Bread, 2, Ware.Flour, 1);
            t[8] = Harvester(BuildingType.Mine, "Mine", Terrain.Mountain, "Digs the mountain's batch into ore; the miners eat rations.");
            t[8].Size = BuildingSize.Medium;
            t[8].Inputs = EconomyTuning.W(Ware.Rations, 1);
            t[8].CyclesPerInput = 2;
            t[9] = Recipe(BuildingType.Smelter, "Smelter", BuildingSize.Large, 50, "Smelts ore with coal into iron; needs power.", Ware.Iron, 1, Ware.Ore, 1, Ware.Coal, 1);
            t[9].NeedsPower = true;
            t[10] = Def(BuildingType.PowerPlant, "Power plant", BuildingKind.PowerPlant, BuildingSize.Large, "Burns coal to power the smelter and the barracks.");
            t[10].Inputs = EconomyTuning.W(Ware.Coal, 1);
            t[10].FuelTicks = 600;
            t[11] = Def(BuildingType.Barracks, "Barracks", BuildingKind.Plain, BuildingSize.Huge, "Trains soldiers from delivered weapons and rations; needs power.");
            t[11].NeedsPower = true;
            t[12] = Def(BuildingType.Market, "Market", BuildingKind.Plain, BuildingSize.Large, "Improves the market rate by 1.");
            t[13] = Def(BuildingType.WonderSite, "Wonder site", BuildingKind.Plain, BuildingSize.Huge, "Receives the goods for each wonder stage.");
            t[14] = Harvester(BuildingType.Sheepfold, "Sheepfold", Terrain.Pasture, "Herds the pasture's batch into sheep.");
            t[15] = Recipe(BuildingType.Butcher, "Butcher", BuildingSize.Medium, 40, "Turns sheep into meat.", Ware.Meat, 2, Ware.Sheep, 1);
            t[16] = Recipe(BuildingType.Tavern, "Tavern", BuildingSize.Medium, 40, "Packs bread and meat into rations.", Ware.Rations, 2, Ware.Bread, 1, Ware.Meat, 1);
            t[17] = Recipe(BuildingType.CharcoalKiln, "Charcoal kiln", BuildingSize.Medium, 40, "Burns logs into coal.", Ware.Coal, 1, Ware.Logs, 2);
            t[18] = Recipe(BuildingType.BrickKiln, "Brick kiln", BuildingSize.Medium, 40, "Fires clay with coal into bricks.", Ware.Bricks, 2, Ware.Clay, 1, Ware.Coal, 1);
            t[19] = Recipe(BuildingType.Smithy, "Smithy", BuildingSize.Large, 50, "Forges iron and planks into weapons.", Ware.Weapons, 1, Ware.Iron, 1, Ware.Planks, 1);
            for (int i = 1; i < Count; i++) t[i].Cost = (int[])size[(int)t[i].Size].Clone();
            return t;
        }

        private static BuildingDef Def(BuildingType type, string name, BuildingKind kind, BuildingSize size, string purpose) =>
            new BuildingDef { Type = type, Name = name, Kind = kind, Size = size, Purpose = purpose };

        private static void Terr(BuildingDef d, Terrain t)
        {
            d.NeedsTerrain = true;
            d.Terrain = t;
        }

        private static BuildingDef Harvester(BuildingType type, string name, Terrain terrain, string purpose)
        {
            var d = Def(type, name, BuildingKind.Harvester, BuildingSize.Small, purpose);
            Terr(d, terrain);
            d.Outputs = EconomyTuning.W(TerrainInfo.RawWare(terrain).Value, 1);
            return d;
        }

        /// <summary>Recipe row: output ware and count, then (input ware, count) pairs.</summary>
        private static BuildingDef Recipe(BuildingType type, string name, BuildingSize size, int ticks, string purpose, Ware output, int outCount, params object[] inputs)
        {
            var d = Def(type, name, BuildingKind.Producer, size, purpose);
            d.Inputs = EconomyTuning.W(inputs);
            d.Outputs = EconomyTuning.W(output, outCount);
            d.WorkTicks = ticks;
            return d;
        }
    }
}
