using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>A building type as the build menu shows it.</summary>
    public struct BuildingTypeView
    {
        public BuildingType Type;
        public string Name;
        /// <summary>One line: what it is for.</summary>
        public string Purpose;
        public BuildingSize Size;
        /// <summary>"2 planks and 1 bricks".</summary>
        public string CostText;
        /// <summary>"Needs: 2 logs per cycle." / "Needs: a forest next to it." / "Needs: 1 rations per 2 ore." ("" when nothing).</summary>
        public string NeedsText;
        /// <summary>"Makes: 1 coal." / "Makes: logs from the forest's batch." / "Makes: power." ("" when nothing).</summary>
        public string MakesText;
        /// <summary>"2 logs -> 1 coal (4 s)" for recipes, "forest batch -> 1 logs (3 s)" for harvesters; "" otherwise.</summary>
        public string RecipeText;
        /// <summary>Terrain the flag must touch, or null.</summary>
        public string TerrainText;
        public bool NeedsPower;
        public bool Placeable;
    }

    /// <summary>What a building (or site) is waiting for, for tooltips and the inspect panel.</summary>
    public struct WaitInfo
    {
        public int BuildingId;
        /// <summary>Stall reason code (<see cref="Reasons"/>) or null while working or idle.</summary>
        public string Reason;
        /// <summary>Ware the reason names, or -1.</summary>
        public int Ware;
        /// <summary>True while a work cycle, construction or fuel is running.</summary>
        public bool Working;
        /// <summary>Plain sentence: "Sawmill is waiting for logs.", "A demon on its flag stops the Mine.", "Brick kiln is working."</summary>
        public string Sentence;
    }

    public static partial class EconomyApi
    {
        // ---- Wares ----

        /// <summary>Lower-case ware name for sentences ("logs").</summary>
        public static string WareName(Ware w) => Wares.DisplayName(w);

        /// <summary>Capitalised ware name for labels ("Logs").</summary>
        public static string WareTitle(Ware w) => Wares.Title(w);

        /// <summary>Suggested colour per ware as "#RRGGBB" (for icons, carrier loads and stock bars).</summary>
        public static string WareColour(Ware w)
        {
            switch (w)
            {
                case Ware.Logs: return "#7A4E2D";
                case Ware.Clay: return "#B5653A";
                case Ware.Wheat: return "#E3C04A";
                case Ware.Sheep: return "#EDEDE4";
                case Ware.Ore: return "#5E6670";
                case Ware.Planks: return "#C89B5E";
                case Ware.Coal: return "#2B2B2B";
                case Ware.Bricks: return "#A33A2A";
                case Ware.Flour: return "#F2EBD9";
                case Ware.Bread: return "#C98A3D";
                case Ware.Meat: return "#B5474B";
                case Ware.Rations: return "#6E8B3D";
                case Ware.Iron: return "#9AA3AD";
                default: return "#4A5A78"; // Weapons
            }
        }

        // ---- Building types ----

        public static BuildingTypeView DescribeType(WorldState s, BuildingType t)
        {
            var d = Def(s, t);
            var v = new BuildingTypeView
            {
                Type = t,
                Name = d.Name,
                Purpose = d.Purpose ?? "",
                Size = d.Size,
                CostText = EconomyLogic.CostText(d.Cost),
                NeedsPower = d.NeedsPower,
                Placeable = d.Placeable,
                TerrainText = d.NeedsTerrain ? TerrainWord(d.Terrain) : null,
                RecipeText = RecipeText(s, t),
            };
            var needs = new List<string>();
            if (d.NeedsTerrain) needs.Add("a " + TerrainWord(d.Terrain) + " hex next to it, drawn in a round");
            string inputs = EconomyLogic.CostText(d.Inputs);
            bool hasInputs = inputs != "nothing";
            switch (d.Kind)
            {
                case BuildingKind.Producer: if (hasInputs) needs.Add(inputs + " per cycle"); break;
                case BuildingKind.PowerPlant: if (hasInputs) needs.Add(inputs + " per " + Seconds(d.FuelTicks) + " of power"); break;
                case BuildingKind.Harvester:
                    if (hasInputs) needs.Add(inputs + " per " + System.Math.Max(1, d.CyclesPerInput) + " " + Wares.DisplayName(TerrainInfo.RawWare(d.Terrain).Value));
                    break;
            }
            if (d.NeedsPower) needs.Add("power");
            v.NeedsText = needs.Count == 0 ? "" : "Needs: " + string.Join("; ", needs) + ".";
            switch (d.Kind)
            {
                case BuildingKind.Producer: v.MakesText = "Makes: " + EconomyLogic.CostText(d.Outputs) + " per cycle."; break;
                case BuildingKind.Harvester: v.MakesText = "Makes: " + Wares.DisplayName(TerrainInfo.RawWare(d.Terrain).Value) + " from the " + TerrainWord(d.Terrain) + "'s batch."; break;
                case BuildingKind.PowerPlant: v.MakesText = "Makes: power for the smelter and the barracks."; break;
                case BuildingKind.Forester: v.MakesText = "Makes: forest batches full size."; break;
                default:
                    v.MakesText = t == BuildingType.Barracks ? "Makes: soldiers." : t == BuildingType.WonderSite ? "Makes: wonder stages." : t == BuildingType.Market ? "Makes: a better market rate." : "";
                    break;
            }
            return v;
        }

        /// <summary>Every placeable building type, in id order (the build menu).</summary>
        public static List<BuildingTypeView> BuildMenu(WorldState s)
        {
            var l = new List<BuildingTypeView>();
            foreach (var d in s.Economy.Tuning.Buildings) if (d.Placeable) l.Add(DescribeType(s, d.Type));
            return l;
        }

        /// <summary>"2 logs -> 1 coal (4 s)"; harvesters "1 forest batch unit -> 2 logs (3 s)", mine "..., 1 rations per 2"; "" for others.</summary>
        public static string RecipeText(WorldState s, BuildingType t)
        {
            var d = Def(s, t);
            switch (d.Kind)
            {
                case BuildingKind.Producer:
                {
                    var outs = (int[])d.Outputs.Clone();
                    var bonus = s.Economy.Bonus != null && s.Economy.Bonus.ExtraOutput != null ? s.Economy.Bonus.ExtraOutput[(int)t] : 0;
                    for (int w = 0; w < Wares.Count && bonus > 0; w++) if (outs[w] > 0) { outs[w] += bonus; bonus = 0; }
                    return EconomyLogic.CostText(d.Inputs) + " -> " + EconomyLogic.CostText(outs) + " (" + Seconds(d.WorkTicks) + ")";
                }
                case BuildingKind.Harvester:
                {
                    var raw = TerrainInfo.RawWare(d.Terrain).Value;
                    string r = "1 " + TerrainWord(d.Terrain) + " batch unit -> " + System.Math.Max(1, d.Outputs[(int)raw]) + " " + Wares.DisplayName(raw) + " (" + Seconds(s.Economy.Tuning.HarvestTicks) + ")";
                    if (EconomyLogic.CostText(d.Inputs) != "nothing") r += ", " + EconomyLogic.CostText(d.Inputs) + " per " + System.Math.Max(1, d.CyclesPerInput);
                    return r;
                }
                case BuildingKind.PowerPlant:
                    return EconomyLogic.CostText(d.Inputs) + " -> power for " + Seconds(d.FuelTicks);
                default: return "";
            }
        }

        /// <summary>Flags where a building of this type may be placed now (ascending ids), with roads not required.</summary>
        public static List<int> LegalBuildFlags(WorldState s, BuildingType t)
        {
            var l = new List<int>();
            for (int v = 0; v < s.Board.Topology.VertexCount; v++)
                if (EconomyLogic.CheckPlacement(s, PlayerIds.Human, t, v).Ok) l.Add(v);
            return l;
        }

        /// <summary>Building types whose recipe or harvest makes this ware.</summary>
        public static List<BuildingType> ProducersOf(WorldState s, Ware w)
        {
            var l = new List<BuildingType>();
            foreach (var d in s.Economy.Tuning.Buildings)
                if (d.Outputs[(int)w] > 0 && (d.Kind == BuildingKind.Producer || d.Kind == BuildingKind.Harvester)) l.Add(d.Type);
            return l;
        }

        /// <summary>Building types that use this ware up as a recipe input, upkeep or fuel.</summary>
        public static List<BuildingType> ConsumersOf(WorldState s, Ware w)
        {
            var l = new List<BuildingType>();
            foreach (var d in s.Economy.Tuning.Buildings) if (d.Inputs[(int)w] > 0) l.Add(d.Type);
            return l;
        }

        // ---- Waiting for ----

        /// <summary>What a building or site is waiting for, in one sentence (same rules as Describe).</summary>
        public static WaitInfo WaitingFor(WorldState s, int buildingId)
        {
            var b = EconomyLogic.Get(s, buildingId);
            if (b == null) return new WaitInfo { BuildingId = buildingId, Ware = -1, Sentence = "That building does not exist." };
            var info = Describe(s, buildingId);
            bool working = b.State == BuildingState.Constructing || b.WorkProgress > 0
                           || (EconomyLogic.Def(s, b).Kind == BuildingKind.PowerPlant && b.FuelLeft > 0);
            return new WaitInfo { BuildingId = buildingId, Reason = b.Reason, Ware = b.ReasonWare, Working = working && b.Reason == null, Sentence = info.Status };
        }

        private static string TerrainWord(Terrain t) => t.ToString().ToLowerInvariant();

        private static string Seconds(int ticks)
        {
            int tenths = ticks * 10 / WorldSim.TicksPerSecond;
            return tenths % 10 == 0 ? (tenths / 10) + " s" : (tenths / 10) + "." + (tenths % 10) + " s";
        }
    }
}
