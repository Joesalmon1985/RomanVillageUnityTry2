using System.Collections.Generic;

namespace World.Sim.War
{
    /// <summary>Soldier stat indices (WA-2). Used for levels and for the stat a processed good raises.</summary>
    public static class Stat
    {
        public const int Health = 0, Attack = 1, Defence = 2, Evade = 3, Count = 4;
    }

    /// <summary>
    /// Data tables and tuning of the war module (PRD_GODBOARD sections 5 and 6). Raw goods: 0 Wood, 1 Clay, 2 Grain, 3 Sheep,
    /// 4 Stone. Good names are placeholders the owner may change; they are data only.
    /// </summary>
    public static class WarData
    {
        public const int RawCount = 5;
        public const int GoodCount = 10;
        public static readonly string[] RawNames = { "Wood", "Clay", "Grain", "Sheep", "Stone" };

        public static readonly string[] GoodNames =
        {
            "Fire-hardened spear", "Hafted stone axe", "Flint knife on sinew", "Hide shield", "Clay-plate vest",
            "Hearth cake", "Cheese and dried meat", "Millstone bread", "Ochre and tallow paint", "Straw-and-mud cloak",
        };
        public static readonly int[] GoodRawA = { 0, 0, 3, 0, 1, 0, 2, 2, 1, 1 };
        public static readonly int[] GoodRawB = { 1, 4, 4, 3, 4, 2, 3, 4, 3, 2 };
        public static readonly int[] GoodStat =
        {
            Stat.Attack, Stat.Attack, Stat.Attack, Stat.Defence, Stat.Defence, Stat.Health, Stat.Health, Stat.Health, Stat.Evade, Stat.Evade,
        };

        // PR-1, PR-2, PR-3
        public const int HutBuildTicks = 300;
        public const int HutCycleTicks = 200;
        /// <summary>Flat stand-in for the carriers (PR-3): ticks to bring one processed good to the lodge from a linked settlement.</summary>
        public const int CarryTicks = 100;

        // WA-1, WA-2
        public const int GoodsPerSoldier = 3;
        public const int TrainTicks = 600;
        public const int MaxLevel = 3;
        public const int BaseHealth = 100, HealthPerLevel = 30;
        public const int AttackMin = 12, AttackMax = 16, AttackPerLevel = 4;
        public const int DefencePerLevel = 8;
        public const int BaseEvade = 20, EvadePerLevel = 8;

        // WA-8..WA-18
        /// <summary>Ticks between two swings of a duel (1 s).</summary>
        public const int SwingTicks = 10;
        public const int PurgeTicks = 150;
        public const int ArmyPlanTicks = 50;
        public const int RuinTicks = 1800;
        /// <summary>Ticks for one step along a trail or between a node and a settlement (placeholder pace).</summary>
        public const int MoveTicks = 40;
        /// <summary>Lead in points that starts a war against the leader (WA-11).</summary>
        public static int WarLead = 2;
        /// <summary>Garrison soldiers a new settlement starts with (a city has one more). Proof variants only; the rule is 1 (WA-4).</summary>
        public static int SettlementSoldiers = 1;

        /// <summary>The raw good a terrain yields, or -1 (Meadow and anything else).</summary>
        public static int RawOfTerrain(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return 0;
                case Terrain.Hills: return 1;
                case Terrain.Fields: return 2;
                case Terrain.Pasture: return 3;
                case Terrain.Mountain: return 4;
                default: return -1;
            }
        }

        /// <summary>The processed good of a pair of distinct raw goods (either order), or -1.</summary>
        public static int GoodOfPair(int a, int b)
        {
            for (int g = 0; g < GoodCount; g++)
                if ((GoodRawA[g] == a && GoodRawB[g] == b) || (GoodRawA[g] == b && GoodRawB[g] == a)) return g;
            return -1;
        }

        /// <summary>PR-1: the processed goods a corner's huts make, one per pair of distinct raw goods among its non-Meadow hexes. Sorted by good id.</summary>
        public static List<int> HutGoodsAt(Board board, int corner)
        {
            var raws = new SortedSet<int>();
            var result = new List<int>();
            var topo = board?.Topology;
            if (topo == null || !topo.IsCorner(corner)) return result;
            foreach (int h in topo.VertexHexes(corner))
            {
                int r = RawOfTerrain(board.TerrainAt(h));
                if (r >= 0) raws.Add(r);
            }
            var list = new List<int>(raws);
            var goods = new SortedSet<int>();
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++) goods.Add(GoodOfPair(list[i], list[j]));
            result.AddRange(goods);
            return result;
        }
    }
}
